using MLCalc.Hosting;
using MLCalc.Plotting;
using MLCalc.Web.Protocol;
using Microsoft.AspNetCore.SignalR;

namespace MLCalc.Web.Server;

/// <summary>
/// One user's workspace on the server, with the server's limits, sending its messages to the
/// connection attached last. The running of programs is in <see cref="Workspace"/>, shared with the app.
/// </summary>
sealed class Session : IDisposable
{
	readonly WebOptions options;
	readonly IHubContext<MLCalcHub, IMLCalcClient> hub;
	readonly ILogger logger;
	readonly Workspace workspace;
	readonly Lock gate = new();

	string? connectionId;

	public Session(string id, WebOptions options, TimeProvider time, SemaphoreSlim runSlots, IHubContext<MLCalcHub, IMLCalcClient> hub, ILogger logger)
	{
		Id = id;
		this.options = options;
		this.hub = hub;
		this.logger = logger;
		workspace = new Workspace(new WorkspaceOptions
		{
			RunTimeLimit = TimeSpan.FromSeconds(options.RunSeconds),
			MaxOutputEntries = options.MaxOutputEntries,
			MaxSourceLength = options.MaxSourceLength,
			MaxPointsPerWindow = options.MaxPointsPerWindow,
			FlushInterval = options.FlushInterval,
			// No script files: programs come from the client as text (see InterpreterOptions.Scripts).
			Interpreter = new() { MaxMatrixElements = options.MaxMatrixElements },
			Plotting = new()
			{
				MaxSurfaceGridLines = options.MaxSurfaceGridLines,
				MaxCurvePoints = options.MaxCurvePoints,
			},
		}, () => Client, time, runSlots);
		workspace.ProgramFailed += ex => logger.LogError(ex, "A program in session {Session} failed unexpectedly.", Id);
		workspace.SendFailed += ex => logger.LogWarning(ex, "Could not send to session {Session}.", Id);
		// savefig writes nothing on the server: the SVG goes to the browser, which saves it.
		workspace.Plotting.RegisterFigureFormat(".svg", (snapshot, path, width, height) =>
		{
			string fileName = Path.GetFileName(path);
			workspace.Download(new DownloadMessage(fileName, "image/svg+xml", SvgWriter.Render(snapshot, width, height)));
			return $"Sent {fileName} to the browser to save.";
		});
	}

	public string Id { get; }

	/// <summary>When the session was last used: connected, ran or finished a program.</summary>
	public DateTimeOffset LastActive => workspace.LastActive;

	public bool IsRunning => workspace.IsRunning;

	/// <summary>Sends this session's messages to <paramref name="connection"/> from now on.</summary>
	public void Attach(string connection)
	{
		lock (gate)
			connectionId = connection;
		workspace.Touch();
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
	public Task SendWelcomeAsync(bool isNew) => workspace.SendWelcomeAsync(Id, isNew,
		new Limits(options.RunSeconds, options.MaxMatrixElements, options.MaxPointsPerWindow, options.MaxOutputEntries, options.MaxSourceLength));

	/// <summary>Starts a program; see <see cref="Workspace.StartAsync"/>.</summary>
	public Task StartAsync(RunRequest request) => workspace.StartAsync(request);

	/// <summary>Stops the running program, if any.</summary>
	public void Cancel() => workspace.Cancel();

	/// <summary>Takes a camera the user set in the client; see <see cref="Workspace.SetCameraAsync"/>.</summary>
	public async Task SetCameraAsync(CameraUpdate update)
	{
		try
		{
			await workspace.SetCameraAsync(update);
		}
		catch (ArgumentException ex)
		{
			logger.LogInformation("Ignored a camera for session {Session}: {Message}", Id, ex.Message);
		}
	}

	/// <summary>Stops a running program; the session is not used afterwards.</summary>
	public void Dispose() => workspace.Dispose();
}
