using MLCalc.Web.Protocol;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace MLCalc.Web.Client;

/// <summary>The state of the connection to the server, for the status bar.</summary>
enum ConnectionState
{
	Connecting,
	Connected,
	Reconnecting,
	Failed,
}

/// <summary>
/// The connection to the server. It keeps the session id in the browser tab's sessionStorage, so
/// that a reconnect, or reloading the page, continues the same session. When the connection drops
/// it connects again with that id, waiting a little longer each time.
/// </summary>
sealed class MLCalcConnection(NavigationManager navigation, IJSRuntime js) : IAsyncDisposable
{
	const string SessionKey = "mlcalc-session";
	static readonly TimeSpan[] RetryDelays = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

	HubConnection? connection;
	string? sessionId;
	bool disposed;

	public ConnectionState State { get; private set; } = ConnectionState.Connecting;

	public event Action<WelcomeMessage>? Welcome;
	public event Action<OutputMessage>? Output;
	public event Action<RunFinishedMessage>? RunFinished;
	public event Action<DownloadMessage>? Download;
	public event Action? StateChanged;

	public async Task StartAsync()
	{
		sessionId = await js.InvokeAsync<string?>("sessionStorage.getItem", SessionKey);
		await ConnectAsync();
	}

	async Task ConnectAsync()
	{
		for (int attempt = 0; !disposed; attempt++)
		{
			await Task.Delay(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)]);
			try
			{
				var next = Build();
				await next.StartAsync();
				connection = next;
				SetState(ConnectionState.Connected);
				return;
			}
			catch (Exception)
			{
				SetState(attempt < 3 ? ConnectionState.Reconnecting : ConnectionState.Failed);
			}
		}
	}

	HubConnection Build()
	{
		string path = WebProtocol.Path.TrimStart('/') + (sessionId is null ? "" : $"?{WebProtocol.SessionParameter}={Uri.EscapeDataString(sessionId)}");
		var hub = new HubConnectionBuilder()
			.WithUrl(navigation.ToAbsoluteUri(path))
			.AddJsonProtocol(json => WebProtocol.Configure(json.PayloadSerializerOptions))
			.Build();
		hub.On<WelcomeMessage>(nameof(IMLCalcClient.Welcome), async message =>
		{
			sessionId = message.SessionId;
			await js.InvokeVoidAsync("sessionStorage.setItem", SessionKey, message.SessionId);
			Welcome?.Invoke(message);
		});
		hub.On<OutputMessage>(nameof(IMLCalcClient.Output), message => Output?.Invoke(message));
		hub.On<RunFinishedMessage>(nameof(IMLCalcClient.RunFinished), message => RunFinished?.Invoke(message));
		hub.On<DownloadMessage>(nameof(IMLCalcClient.Download), message => Download?.Invoke(message));
		hub.Closed += async _ =>
		{
			if (disposed || connection != hub)
				return;
			SetState(ConnectionState.Reconnecting);
			await ConnectAsync();
		};
		return hub;
	}

	void SetState(ConnectionState state)
	{
		State = state;
		StateChanged?.Invoke();
	}

	public Task RunAsync(RunRequest request) =>
		connection?.InvokeAsync(ServerMethods.Run, request) ?? Task.CompletedTask;

	public Task CancelAsync() =>
		connection?.InvokeAsync(ServerMethods.Cancel) ?? Task.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		disposed = true;
		if (connection is not null)
			await connection.DisposeAsync();
	}
}
