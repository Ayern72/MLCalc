using MLCalc.Interpreter;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Plotting;
using MLCalc.Web.Protocol;
using Microsoft.AspNetCore.SignalR;

namespace MLCalc.Web.Server;

/// <summary>
/// One user's workspace: an interpreter with its variables and a figure. Programs run one at a
/// time on a worker thread; while one runs, its output is sent in batches every
/// <see cref="WebOptions.FlushInterval"/>, then <c>RunFinished</c>. All messages to the client go
/// through <see cref="sendLock"/>, so they arrive in the order they were produced.
/// </summary>
sealed class Session : IConsoleOutput, IDisposable
{
	readonly WebOptions options;
	readonly TimeProvider time;
	readonly SemaphoreSlim runSlots;
	readonly IHubContext<MLCalcHub, IMLCalcClient> hub;
	readonly ILogger logger;
	readonly MLCalcInterpreter interpreter;
	readonly Lock gate = new();
	readonly SemaphoreSlim sendLock = new(1, 1);
	readonly CancellationTokenSource lifetime = new();
	/// <summary>What the client has of the figure; used only under <see cref="sendLock"/>.</summary>
	readonly FigureEncoder encoder = new();

	string? connectionId;
	Task running = Task.CompletedTask;
	CancellationTokenSource? cancel;
	List<OutputEntry> pending = [];
	int outputCount;
	bool outputTruncated;

	public Session(string id, WebOptions options, TimeProvider time, SemaphoreSlim runSlots, IHubContext<MLCalcHub, IMLCalcClient> hub, ILogger logger)
	{
		Id = id;
		this.options = options;
		this.time = time;
		this.runSlots = runSlots;
		this.hub = hub;
		this.logger = logger;
		LastActive = time.GetUtcNow();
		Figure = new Figure(options.MaxPointsPerWindow);
		// No script files: programs come from the client as text (see InterpreterOptions.Scripts).
		interpreter = new MLCalcInterpreter(this, new InterpreterOptions { MaxMatrixElements = options.MaxMatrixElements });
		interpreter.AddPlotting(Figure, new PlottingOptions
		{
			MaxSurfaceGridLines = options.MaxSurfaceGridLines,
			MaxCurvePoints = options.MaxCurvePoints,
		});
	}

	public string Id { get; }

	public Figure Figure { get; }

	/// <summary>When the session was last used: connected, ran or finished a program.</summary>
	public DateTimeOffset LastActive { get; private set; }

	public bool IsRunning
	{
		get
		{
			lock (gate)
				return !running.IsCompleted;
		}
	}

	void Touch() => LastActive = time.GetUtcNow();

	/// <summary>Sends this session's messages to <paramref name="connection"/> from now on.</summary>
	public void Attach(string connection)
	{
		lock (gate)
			connectionId = connection;
		Touch();
	}

	/// <summary>Stops sending to <paramref name="connection"/>, unless a newer connection has taken over.</summary>
	public void Detach(string connection)
	{
		lock (gate)
		{
			if (connectionId == connection)
				connectionId = null;
		}
	}

	IMLCalcClient? Client
	{
		get
		{
			lock (gate)
				return connectionId is null ? null : hub.Clients.Client(connectionId);
		}
	}

	/// <summary>
	/// Sends <c>Welcome</c> with every plot window to the connection attached last; later figure
	/// messages only send what changed since.
	/// </summary>
	public Task SendWelcomeAsync(bool isNew) => SendAsync(client => client.Welcome(new WelcomeMessage(
		Id, isNew, IsRunning, [.. interpreter.BuiltinNames],
		new Limits(options.RunSeconds, options.MaxMatrixElements, options.MaxPointsPerWindow, options.MaxOutputEntries, options.MaxSourceLength),
		encoder.Full(Figure.Snapshot()))));

	/// <summary>
	/// Starts <paramref name="request"/>, or answers at once when it cannot run: another program is
	/// running, the text is too long, or it is unfinished. Returns when the program has started.
	/// </summary>
	public async Task StartAsync(RunRequest request)
	{
		Touch();
		var immediate = Check(request);
		if (immediate is not null)
		{
			await SendAsync(client => client.RunFinished(immediate));
			return;
		}
		lock (gate)
		{
			if (!running.IsCompleted)
				immediate = new RunFinishedMessage(request.Id, RunStatus.Busy);
			else
			{
				cancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
				pending = [];
				outputCount = 0;
				outputTruncated = false;
				running = RunAsync(request, cancel);
			}
		}
		if (immediate is not null)
			await SendAsync(client => client.RunFinished(immediate));
	}

	RunFinishedMessage? Check(RunRequest request)
	{
		if (request.Source.Length > options.MaxSourceLength)
			return new RunFinishedMessage(request.Id, RunStatus.Error,
				[new RunError($"The program is too long: {request.Source.Length} characters; the limit is {options.MaxSourceLength}.", 0, 0, 0)]);
		// An empty last line runs an unfinished program anyway, which reports what is missing.
		int lastLine = request.Source.LastIndexOf('\n');
		bool lastLineEmpty = lastLine >= 0 && string.IsNullOrWhiteSpace(request.Source[(lastLine + 1)..]);
		if (!lastLineEmpty && Parser.Parse(request.Source).IsIncomplete)
			return new RunFinishedMessage(request.Id, RunStatus.Incomplete);
		return null;
	}

	/// <summary>Stops the running program, if any.</summary>
	public void Cancel()
	{
		Touch();
		lock (gate)
			cancel?.Cancel();
	}

	async Task RunAsync(RunRequest request, CancellationTokenSource userCancel)
	{
		using var timeLimit = new CancellationTokenSource(TimeSpan.FromSeconds(options.RunSeconds), time);
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(userCancel.Token, timeLimit.Token);
		using var stopPublishing = new CancellationTokenSource();
		var publishing = PublishWhileRunningAsync(stopPublishing.Token);
		RunFinishedMessage finished;
		try
		{
			// Waiting for a free slot counts against the time limit.
			await runSlots.WaitAsync(stop.Token);
			ExecutionResult result;
			try
			{
				result = await Task.Run(() => interpreter.Execute(request.Source, stop.Token));
			}
			finally
			{
				runSlots.Release();
			}
			finished = result.Cancelled ? Stopped(request.Id, timeLimit, userCancel) : Finished(request.Id, result);
		}
		catch (OperationCanceledException) when (stop.IsCancellationRequested)
		{
			finished = Stopped(request.Id, timeLimit, userCancel);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "A program in session {Session} failed unexpectedly.", Id);
			finished = new RunFinishedMessage(request.Id, RunStatus.Error, [new RunError($"Internal error: {ex.Message}", 0, 0, 0)]);
		}
		finally
		{
			stopPublishing.Cancel();
			await publishing;
		}

		await FlushAsync();
		bool truncated;
		lock (gate)
			truncated = outputTruncated;
		Touch();
		await SendAsync(client => client.RunFinished(finished with { OutputTruncated = truncated }));
		lock (gate)
		{
			cancel = null;
			userCancel.Dispose();
		}
	}

	static RunFinishedMessage Stopped(string id, CancellationTokenSource timeLimit, CancellationTokenSource userCancel) =>
		new(id, timeLimit.IsCancellationRequested && !userCancel.IsCancellationRequested ? RunStatus.TimeLimit : RunStatus.Cancelled);

	static RunFinishedMessage Finished(string id, ExecutionResult result)
	{
		if (result.Success)
			return new RunFinishedMessage(id, RunStatus.Ok);
		var text = result.Parse.Text;
		var errors = result.Parse.Diagnostics.Select(d => Error(d.Message, d.Span)).ToList();
		if (result.Error is not null)
			errors.Add(Error(result.Error.Message, result.Error.Span));
		return new RunFinishedMessage(id, RunStatus.Error, errors);

		RunError Error(string message, SourceSpan span)
		{
			if (text.Text.Length == 0)
				return new RunError(message, 0, 0, 0);
			var position = text.GetLinePosition(Math.Min(span.Start, text.Text.Length));
			return new RunError(message, position.Line + 1, position.Column + 1, span.Length);
		}
	}

	async Task PublishWhileRunningAsync(CancellationToken stop)
	{
		using var timer = new PeriodicTimer(options.FlushInterval, time);
		try
		{
			while (await timer.WaitForNextTickAsync(stop))
				await FlushAsync();
		}
		catch (OperationCanceledException) when (stop.IsCancellationRequested)
		{
		}
	}

	/// <summary>Sends the output written since the last flush, then what changed in the plots.</summary>
	async Task FlushAsync()
	{
		List<OutputEntry> entries;
		lock (gate)
		{
			entries = pending;
			pending = [];
		}
		await SendAsync(async client =>
		{
			if (entries.Count > 0)
				await client.Output(new OutputMessage(entries));
			if (encoder.Changes(Figure.Snapshot()) is { } changes)
				await client.FigureChanged(changes);
		});
	}

	/// <summary>
	/// Sends a message to the connected client, in order with the others. Without a connection the
	/// message is dropped: a client that reconnects gets a new <c>Welcome</c>.
	/// </summary>
	async Task SendAsync(Func<IMLCalcClient, Task> send)
	{
		await sendLock.WaitAsync();
		try
		{
			if (Client is { } client)
				await send(client);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Could not send to session {Session}.", Id);
		}
		finally
		{
			sendLock.Release();
		}
	}

	// ───────────── IConsoleOutput: called on the program's thread ─────────────

	void IConsoleOutput.WriteValue(string name, ComplexMatrix value) => Add(OutputKind.Value, ResultFormatter.Format(name, value));

	void IConsoleOutput.WriteLine(string text) => Add(OutputKind.Text, text);

	void IConsoleOutput.Clear()
	{
		lock (gate)
			pending = [new OutputEntry(OutputKind.Clear, "")];
	}

	void Add(OutputKind kind, string text)
	{
		lock (gate)
		{
			if (outputCount >= options.MaxOutputEntries)
			{
				outputTruncated = true;
				return;
			}
			outputCount++;
			pending.Add(new OutputEntry(kind, text));
		}
	}

	/// <summary>Stops a running program; the session is not used afterwards.</summary>
	public void Dispose() => lifetime.Cancel();
}
