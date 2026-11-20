using MLCalc.Interpreter;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Plotting;
using MLCalc.Web.Protocol;

namespace MLCalc.Hosting;

/// <summary>
/// One user's workspace: an interpreter with its variables and a figure, which runs programs and
/// reports what they do as the web protocol's messages (<see cref="IMLCalcClient"/>). The web server
/// sends the messages over SignalR; the app gives them straight to its page.
/// </summary>
/// <remarks>
/// Programs run one at a time on a worker thread. While one runs, its output is sent in batches
/// every <see cref="WorkspaceOptions.FlushInterval"/>, then <c>RunFinished</c>. All messages go
/// through one lock, so they arrive in the order they were produced. When the client function gives
/// no client, messages are dropped: a client that comes back gets a new <c>Welcome</c>.
/// </remarks>
public sealed class Workspace : IConsoleOutput, IDisposable
{
	readonly WorkspaceOptions options;
	readonly Func<IMLCalcClient?> client;
	readonly TimeProvider time;
	readonly SemaphoreSlim? runSlots;
	readonly Lock gate = new();
	readonly SemaphoreSlim sendLock = new(1, 1);
	readonly CancellationTokenSource lifetime = new();
	/// <summary>What the client has of the figure; used only under <see cref="sendLock"/>.</summary>
	readonly FigureEncoder encoder = new();

	Task running = Task.CompletedTask;
	CancellationTokenSource? cancel;
	List<OutputEntry> pending = [];
	List<DownloadMessage> downloads = [];
	int outputCount;
	bool outputTruncated;

	/// <summary>Makes a workspace with no variables and an empty figure.</summary>
	/// <param name="options">The limits and the interpreter's options.</param>
	/// <param name="client">Where messages go; called for each message, and may give null when no one listens.</param>
	/// <param name="time">The clock, for the time limit and the flushes; the system clock by default.</param>
	/// <param name="runSlots">Shared by workspaces whose programs should not all run at once; null for no such limit.</param>
	public Workspace(WorkspaceOptions options, Func<IMLCalcClient?> client, TimeProvider? time = null, SemaphoreSlim? runSlots = null)
	{
		this.options = options;
		this.client = client;
		this.time = time ?? TimeProvider.System;
		this.runSlots = runSlots;
		LastActive = this.time.GetUtcNow();
		Figure = new Figure(options.MaxPointsPerWindow);
		Interpreter = new MLCalcInterpreter(this, options.Interpreter);
		Plotting = Interpreter.AddPlotting(Figure, options.Plotting);
	}

	/// <summary>The interpreter; use it only while no program runs, such as to set the working directory.</summary>
	public MLCalcInterpreter Interpreter { get; }

	/// <summary>The plot commands, for adding <c>savefig</c> formats.</summary>
	public PlotCommands Plotting { get; }

	/// <summary>The plot windows.</summary>
	public Figure Figure { get; }

	/// <summary>When the workspace was last used: started, stopped or finished a program, or <see cref="Touch"/>.</summary>
	public DateTimeOffset LastActive { get; private set; }

	/// <summary>Whether a program is running.</summary>
	public bool IsRunning
	{
		get
		{
			lock (gate)
				return !running.IsCompleted;
		}
	}

	/// <summary>A program failed in a way that is not the program's fault, such as a bug in MLCalc; it is also reported to the client.</summary>
	public event Action<Exception>? ProgramFailed;

	/// <summary>A message could not be sent to the client; it is dropped.</summary>
	public event Action<Exception>? SendFailed;

	/// <summary>Counts as activity, for <see cref="LastActive"/>.</summary>
	public void Touch() => LastActive = time.GetUtcNow();

	/// <summary>
	/// Sends <c>Welcome</c> with every plot window; later figure messages only send what changed since.
	/// </summary>
	public Task SendWelcomeAsync(string sessionId, bool isNew, Limits limits) => SendAsync(target => target.Welcome(new WelcomeMessage(
		sessionId, isNew, IsRunning, WebProtocol.BuildVersion, [.. Interpreter.BuiltinNames], limits, encoder.Full(Figure.Snapshot()))));

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
			await SendAsync(target => target.RunFinished(immediate));
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
			await SendAsync(target => target.RunFinished(immediate));
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

	/// <summary>
	/// Takes a camera the user set in the client. The client has it already, so the encoder is told,
	/// and the window is not sent back; changes a running program makes are still sent. Throws
	/// <see cref="ArgumentException"/> for a camera that is not valid.
	/// </summary>
	public async Task SetCameraAsync(CameraUpdate update)
	{
		Touch();
		await sendLock.WaitAsync();
		try
		{
			var camera = FigureEncoder.ToCamera(update.Camera);
			if (Figure.SetCamera(update.Row, update.Column, camera))
				encoder.ClientHasCamera(update.Row * Figure.Snapshot().Columns + update.Column, camera);
		}
		finally
		{
			sendLock.Release();
		}
	}

	/// <summary>
	/// Sends a file to the client with the program's next output, as the web server does for
	/// <c>savefig</c>. May be called from the program's thread.
	/// </summary>
	public void Download(DownloadMessage file)
	{
		lock (gate)
			downloads.Add(file);
	}

	async Task RunAsync(RunRequest request, CancellationTokenSource userCancel)
	{
		using var timeLimit = options.RunTimeLimit is { } limit ? new CancellationTokenSource(limit, time) : new CancellationTokenSource();
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(userCancel.Token, timeLimit.Token);
		using var stopPublishing = new CancellationTokenSource();
		var publishing = PublishWhileRunningAsync(stopPublishing.Token);
		RunFinishedMessage finished;
		try
		{
			// Waiting for a free slot counts against the time limit.
			if (runSlots is not null)
				await runSlots.WaitAsync(stop.Token);
			ExecutionResult result;
			try
			{
				result = await Task.Run(() => Interpreter.Execute(request.Source, stop.Token));
			}
			finally
			{
				runSlots?.Release();
			}
			finished = result.Cancelled ? Stopped(request.Id, timeLimit, userCancel) : Finished(request.Id, result);
		}
		catch (OperationCanceledException) when (stop.IsCancellationRequested)
		{
			finished = Stopped(request.Id, timeLimit, userCancel);
		}
		catch (Exception ex)
		{
			ProgramFailed?.Invoke(ex);
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
		await SendAsync(target => target.RunFinished(finished with { OutputTruncated = truncated }));
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

	/// <summary>Sends the output written since the last flush, then what changed in the plots, then files from savefig.</summary>
	async Task FlushAsync()
	{
		List<OutputEntry> entries;
		List<DownloadMessage> files;
		lock (gate)
		{
			entries = pending;
			pending = [];
			files = downloads;
			downloads = [];
		}
		await SendAsync(async target =>
		{
			if (entries.Count > 0)
				await target.Output(new OutputMessage(entries));
			if (encoder.Changes(Figure.Snapshot()) is { } changes)
				await target.FigureChanged(changes);
			foreach (var file in files)
				await target.Download(file);
		});
	}

	/// <summary>Sends a message to the client, in order with the others; without a client the message is dropped.</summary>
	async Task SendAsync(Func<IMLCalcClient, Task> send)
	{
		await sendLock.WaitAsync();
		try
		{
			if (client() is { } target)
				await send(target);
		}
		catch (Exception ex)
		{
			SendFailed?.Invoke(ex);
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

	/// <summary>Stops a running program; the workspace is not used afterwards.</summary>
	public void Dispose() => lifetime.Cancel();
}

/// <summary>The limits of a <see cref="Workspace"/>; by default there are none, as in the desktop app.</summary>
public sealed class WorkspaceOptions
{
	/// <summary>How long a program may run, waiting for a run slot included; null for no limit.</summary>
	public TimeSpan? RunTimeLimit { get; init; }

	/// <summary>The most console entries one run may write; the rest are left out.</summary>
	public int MaxOutputEntries { get; init; } = int.MaxValue;

	/// <summary>The longest program text accepted, in characters.</summary>
	public int MaxSourceLength { get; init; } = int.MaxValue;

	/// <summary>The most points in one plot window.</summary>
	public int MaxPointsPerWindow { get; init; } = Figure.DefaultMaxPointsPerWindow;

	/// <summary>How often output and plots are sent while a program runs.</summary>
	public TimeSpan FlushInterval { get; init; } = TimeSpan.FromMilliseconds(50);

	/// <summary>The interpreter's options, such as where scripts come from and the largest matrix.</summary>
	public InterpreterOptions Interpreter { get; init; } = new();

	/// <summary>The plot commands' options; null for their defaults.</summary>
	public PlottingOptions? Plotting { get; init; }
}
