using System.Threading.Channels;
using MLCalc.Web.Protocol;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using MLCalc.Web.Server;
using Microsoft.Extensions.DependencyInjection;

namespace MLCalc.Web.Tests;

/// <summary>The server in memory, with settings changed for a test.</summary>
// Any type of the server assembly names it; Program would be ambiguous with the client's.
sealed class WebTestServer : WebApplicationFactory<WebOptions>
{
	readonly Action<WebOptions>? configure;
	readonly TimeProvider? clock;

	public WebTestServer(Action<WebOptions>? configure = null, TimeProvider? clock = null)
	{
		this.configure = configure;
		this.clock = clock;
	}

	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		builder.UseSetting("Logging:LogLevel:Default", "Warning");
		builder.ConfigureServices(services =>
		{
			services.PostConfigure<WebOptions>(options => configure?.Invoke(options));
			if (clock is not null)
				services.AddSingleton(clock);
		});
	}

	/// <summary>A connected client; <paramref name="session"/> continues an existing session.</summary>
	public async Task<TestClient> ConnectAsync(string? session = null)
	{
		var client = new TestClient(this, session);
		await client.StartAsync();
		return client;
	}
}

/// <summary>
/// A SignalR client of the in-memory server that records every message in arrival order, as a
/// browser would receive them.
/// </summary>
sealed class TestClient : IAsyncDisposable
{
	static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
	readonly HubConnection connection;
	readonly Channel<object> messages = Channel.CreateUnbounded<object>();

	public TestClient(WebTestServer server, string? session)
	{
		var url = new Uri(server.Server.BaseAddress, WebProtocol.Path + (session is null ? "" : $"?{WebProtocol.SessionParameter}={session}"));
		connection = new HubConnectionBuilder()
			.WithUrl(url, options =>
			{
				options.HttpMessageHandlerFactory = _ => server.Server.CreateHandler();
				options.Transports = HttpTransportType.LongPolling;
			})
			.AddJsonProtocol(json => WebProtocol.Configure(json.PayloadSerializerOptions))
			.Build();
		connection.On<WelcomeMessage>(nameof(IMLCalcClient.Welcome), Record);
		connection.On<OutputMessage>(nameof(IMLCalcClient.Output), Record);
		connection.On<RunFinishedMessage>(nameof(IMLCalcClient.RunFinished), Record);
		connection.On<DownloadMessage>(nameof(IMLCalcClient.Download), Record);
		// A closed connection ends the messages, so that a test waiting for one fails at once.
		connection.Closed += error =>
		{
			messages.Writer.TryComplete(error ?? new InvalidOperationException("The connection was closed."));
			return Task.CompletedTask;
		};
	}

	void Record(object message) => messages.Writer.TryWrite(message);

	public async Task StartAsync()
	{
		await connection.StartAsync();
		Welcome = await NextAsync<WelcomeMessage>();
	}

	public WelcomeMessage Welcome { get; private set; } = null!;

	public Task RunAsync(string source, string id = "1") => connection.InvokeAsync(ServerMethods.Run, new RunRequest(id, source));

	public Task CancelAsync() => connection.InvokeAsync(ServerMethods.Cancel);

	/// <summary>The next message, which must be a <typeparamref name="T"/>.</summary>
	public async Task<T> NextAsync<T>()
	{
		using var timeout = new CancellationTokenSource(Timeout);
		var message = await messages.Reader.ReadAsync(timeout.Token);
		return Assert.IsType<T>(message);
	}

	/// <summary>Runs <paramref name="source"/> and collects the output up to its <c>RunFinished</c>.</summary>
	public async Task<(List<OutputEntry> Output, RunFinishedMessage Finished)> RunToEndAsync(string source)
	{
		await RunAsync(source);
		var output = new List<OutputEntry>();
		while (true)
		{
			using var timeout = new CancellationTokenSource(Timeout);
			switch (await messages.Reader.ReadAsync(timeout.Token))
			{
				case OutputMessage o:
					output.AddRange(o.Entries);
					break;
				case RunFinishedMessage finished:
					return (output, finished);
			}
		}
	}

	/// <summary>The text of the value and text entries.</summary>
	public static string[] Texts(IEnumerable<OutputEntry> entries) => [.. entries.Where(e => e.Kind != OutputKind.Clear).Select(e => e.Text)];

	public ValueTask DisposeAsync() => connection.DisposeAsync();
}
