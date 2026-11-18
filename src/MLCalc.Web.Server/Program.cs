using MLCalc.Web.Protocol;
using MLCalc.Web.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WebOptions>(builder.Configuration.GetSection(WebOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddHostedService<IdleSessionCleanup>();
builder.Services.AddSignalR(hub =>
	{
		// Room for the longest program text, written as JSON.
		hub.MaximumReceiveMessageSize = 1024 * 1024;
	})
	.AddJsonProtocol(json => WebProtocol.Configure(json.PayloadSerializerOptions));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/healthz");
app.MapHub<MLCalcHub>(WebProtocol.Path);

app.Run();

/// <summary>The entry point; public for the tests' <c>WebApplicationFactory</c>.</summary>
public partial class Program;
