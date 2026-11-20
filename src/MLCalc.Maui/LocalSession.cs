using MLCalc.Web.Protocol;
using MLCalc.Web.Ui;

namespace MLCalc.Maui;

/// <summary>
/// The page's session in the app: the workspace's messages, given to the page as events, with no
/// network in between. It is always connected.
/// </summary>
sealed class LocalSession(LocalWorkspace host) : IMLCalcSession, IMLCalcClient
{
	/// <summary>No limits; the page mentions them only when one stops a program, which none does here.</summary>
	static readonly Limits NoLimits = new(0, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

	public ConnectionState State => ConnectionState.Connected;

	public event Action<WelcomeMessage>? Welcome;
	public event Action<OutputMessage>? Output;
	public event Action<FigureMessage>? FigureChanged;
	public event Action<RunFinishedMessage>? RunFinished;
	public event Action<DownloadMessage>? Download;

	/// <summary>Never raised: the session is always connected.</summary>
	public event Action? StateChanged { add { } remove { } }

	public Task StartAsync()
	{
		bool isNew = host.Attach(this);
		return host.Workspace.SendWelcomeAsync("local", isNew, NoLimits);
	}

	public Task RunAsync(RunRequest request) => host.Workspace.StartAsync(request);

	public Task CancelAsync()
	{
		host.Workspace.Cancel();
		return Task.CompletedTask;
	}

	public async Task SetCameraAsync(CameraUpdate update)
	{
		try
		{
			await host.Workspace.SetCameraAsync(update);
		}
		catch (ArgumentException)
		{
			// Not a valid camera: the workspace keeps its own.
		}
	}

	public ValueTask DisposeAsync()
	{
		host.Detach(this);
		return ValueTask.CompletedTask;
	}

	// ───────────── IMLCalcClient: the workspace's messages ─────────────

	Task IMLCalcClient.Welcome(WelcomeMessage message) => Raise(Welcome, message);
	Task IMLCalcClient.Output(OutputMessage message) => Raise(Output, message);
	Task IMLCalcClient.FigureChanged(FigureMessage message) => Raise(FigureChanged, message);
	Task IMLCalcClient.RunFinished(RunFinishedMessage message) => Raise(RunFinished, message);
	Task IMLCalcClient.Download(DownloadMessage message) => Raise(Download, message);

	static Task Raise<T>(Action<T>? handler, T message)
	{
		handler?.Invoke(message);
		return Task.CompletedTask;
	}
}
