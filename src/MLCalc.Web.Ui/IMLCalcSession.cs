using MLCalc.Web.Protocol;

namespace MLCalc.Web.Ui;

/// <summary>The state of the connection to the workspace, for the status bar.</summary>
public enum ConnectionState
{
	/// <summary>Connecting for the first time.</summary>
	Connecting,
	/// <summary>Connected: programs can run.</summary>
	Connected,
	/// <summary>The connection was lost; connecting again.</summary>
	Reconnecting,
	/// <summary>Several attempts have failed; still trying.</summary>
	Failed,
}

/// <summary>
/// Where the page's programs run: on the web server, over SignalR, or in a workspace in the app
/// itself. Either way the page gets the messages of the web protocol (docs/web-protocol.md), so it
/// has one code path. The events may come on any thread.
/// </summary>
public interface IMLCalcSession : IAsyncDisposable
{
	/// <summary>The state of the connection.</summary>
	ConnectionState State { get; }

	/// <summary>The workspace, on connecting and after each reconnect.</summary>
	event Action<WelcomeMessage>? Welcome;

	/// <summary>Console output from the running program.</summary>
	event Action<OutputMessage>? Output;

	/// <summary>What changed in the plot windows.</summary>
	event Action<FigureMessage>? FigureChanged;

	/// <summary>A program has finished, or did not start.</summary>
	event Action<RunFinishedMessage>? RunFinished;

	/// <summary>A file from <c>savefig</c>, to be saved.</summary>
	event Action<DownloadMessage>? Download;

	/// <summary><see cref="State"/> changed.</summary>
	event Action? StateChanged;

	/// <summary>Connects; <see cref="Welcome"/> follows.</summary>
	Task StartAsync();

	/// <summary>Runs a program; the results come as events.</summary>
	Task RunAsync(RunRequest request);

	/// <summary>Stops the running program, if any.</summary>
	Task CancelAsync();

	/// <summary>Tells the workspace about a camera the user set by turning or zooming a 3D window.</summary>
	Task SetCameraAsync(CameraUpdate update);
}
