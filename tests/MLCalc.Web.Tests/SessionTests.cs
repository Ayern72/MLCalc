using MLCalc.Web.Protocol;
using MLCalc.Web.Server;
using Microsoft.Extensions.DependencyInjection;

namespace MLCalc.Web.Tests;

/// <summary>Running programs through the hub: output, errors, stopping, limits and sessions.</summary>
public class SessionTests
{
	[Fact]
	public async Task ANewClientGetsANewSession()
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync();
		var welcome = client.Welcome;
		Assert.True(welcome.NewSession);
		Assert.False(welcome.Running);
		Assert.Equal(WebProtocol.BuildVersion, welcome.Version);
		Assert.Equal(32, welcome.SessionId.Length);
		Assert.Contains("sin", welcome.BuiltinNames);
		Assert.Contains("fplot3d", welcome.BuiltinNames);
		Assert.Equal(60, welcome.Limits.RunSeconds);
	}

	[Fact]
	public async Task OutputComesBeforeRunFinished()
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync();
		var (output, finished) = await client.RunToEndAsync("x = 1 + 2\ndisp(\"done\")\n[1 2; 3 4]");
		Assert.Equal(RunStatus.Ok, finished.Status);
		Assert.Equal("1", finished.Id);
		Assert.Null(finished.Errors);
		Assert.Equal([OutputKind.Value, OutputKind.Text, OutputKind.Value], output.Select(e => e.Kind));
		Assert.Equal("x = 3", output[0].Text);
		Assert.Equal("done", output[1].Text);
		Assert.StartsWith("ans =", output[2].Text);
	}

	[Fact]
	public async Task VariablesLastAcrossRunsAndReconnects()
	{
		await using var server = new WebTestServer();
		string session;
		await using (var first = await server.ConnectAsync())
		{
			session = first.Welcome.SessionId;
			await first.RunToEndAsync("a = 21;");
		}
		await using var second = await server.ConnectAsync(session);
		Assert.False(second.Welcome.NewSession);
		Assert.Equal(session, second.Welcome.SessionId);
		var (output, _) = await second.RunToEndAsync("a * 2");
		Assert.Equal(["ans = 42"], TestClient.Texts(output));
	}

	[Fact]
	public async Task AnUnknownSessionGivesANewOne()
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync("0123456789abcdef0123456789abcdef");
		Assert.True(client.Welcome.NewSession);
		Assert.NotEqual("0123456789abcdef0123456789abcdef", client.Welcome.SessionId);
	}

	[Fact]
	public async Task ErrorsHaveTheirPlaceInTheProgram()
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync();

		var (_, syntax) = await client.RunToEndAsync("x = 1\ny = (2 +* 3)");
		Assert.Equal(RunStatus.Error, syntax.Status);
		var error = Assert.Single(syntax.Errors!);
		Assert.Equal((2, 9), (error.Line, error.Column));   // at the *

		var (output, runtime) = await client.RunToEndAsync("x = 1\ny = x + nothere");
		Assert.Equal(["x = 1"], TestClient.Texts(output));
		Assert.Equal(RunStatus.Error, runtime.Status);
		error = Assert.Single(runtime.Errors!);
		Assert.Equal("Unknown function or variable 'nothere'.", error.Message);
		Assert.Equal((2, 9, 7), (error.Line, error.Column, error.Length));
	}

	[Fact]
	public async Task AnUnfinishedProgramWaitsForMoreLines()
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync();
		var (_, unfinished) = await client.RunToEndAsync("while k < 3");
		Assert.Equal(RunStatus.Incomplete, unfinished.Status);

		var (output, finished) = await client.RunToEndAsync("k = 0;\nwhile k < 3\nk = k + 1;\nend\nk");
		Assert.Equal(RunStatus.Ok, finished.Status);
		Assert.Equal(["k = 3"], TestClient.Texts(output));

		// An empty last line runs it anyway, which reports what is missing.
		var (_, forced) = await client.RunToEndAsync("while k < 3\n");
		Assert.Equal(RunStatus.Error, forced.Status);
	}

	[Fact]
	public async Task CancelStopsARunningProgram()
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync();
		await client.RunAsync("k = 0; while 1 do k = k + 1;", "loop");
		await client.RunAsync("1", "second");
		var busy = await client.NextAsync<RunFinishedMessage>();
		Assert.Equal(("second", RunStatus.Busy), (busy.Id, busy.Status));

		await client.CancelAsync();
		var stopped = await client.NextAsync<RunFinishedMessage>();
		Assert.Equal(("loop", RunStatus.Cancelled), (stopped.Id, stopped.Status));
		var (output, _) = await client.RunToEndAsync("k > 0");
		Assert.Equal(["ans = 1"], TestClient.Texts(output));
	}

	[Fact]
	public async Task AProgramIsStoppedAtTheTimeLimit()
	{
		await using var server = new WebTestServer(options => options.RunSeconds = 1);
		await using var client = await server.ConnectAsync();
		var (_, finished) = await client.RunToEndAsync("while 1 do k = 1;");
		Assert.Equal(RunStatus.TimeLimit, finished.Status);
	}

	[Fact]
	public async Task OutputBeyondTheLimitIsLeftOut()
	{
		await using var server = new WebTestServer(options => options.MaxOutputEntries = 3);
		await using var client = await server.ConnectAsync();
		var (output, finished) = await client.RunToEndAsync("for k = 1 to 10 do disp(k)");
		Assert.Equal(["1", "2", "3"], TestClient.Texts(output));
		Assert.True(finished.OutputTruncated);
		// The count starts again with every run.
		(output, finished) = await client.RunToEndAsync("disp(4)");
		Assert.Equal(["4"], TestClient.Texts(output));
		Assert.False(finished.OutputTruncated);
	}

	[Theory]
	[InlineData("run(\"other.m\")")]
	[InlineData("other.m")]
	public async Task ProgramsCannotReadFilesOnTheServer(string source)
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync();
		var (_, finished) = await client.RunToEndAsync(source);
		Assert.Equal("Script files cannot be run here.", Assert.Single(finished.Errors!).Message);
	}

	[Fact]
	public async Task TheLimitsOfTheServerApply()
	{
		await using var server = new WebTestServer(options =>
		{
			options.MaxMatrixElements = 100;
			options.MaxSourceLength = 20;
		});
		await using var client = await server.ConnectAsync();
		var (_, tooLarge) = await client.RunToEndAsync("a = ones(11);");
		Assert.Contains("the limit is 100", Assert.Single(tooLarge.Errors!).Message);
		var (_, tooLong) = await client.RunToEndAsync("x = 1 + 2 + 3 + 4 + 5 + 6");
		Assert.StartsWith("The program is too long", Assert.Single(tooLong.Errors!).Message);
	}

	[Fact]
	public async Task NoMoreSessionsThanAllowed()
	{
		await using var server = new WebTestServer(options => options.MaxSessions = 1);
		await using var first = await server.ConnectAsync();
		await Assert.ThrowsAnyAsync<Exception>(() => server.ConnectAsync());
		// The first session can reconnect.
		await using var again = await server.ConnectAsync(first.Welcome.SessionId);
		Assert.False(again.Welcome.NewSession);
	}

	[Fact]
	public async Task AnIdleSessionIsRemovedAndANewOneStarted()
	{
		var clock = new ManualClock();
		await using var server = new WebTestServer(options => options.IdleTimeout = TimeSpan.FromMinutes(30), clock);
		await using var client = await server.ConnectAsync();
		await client.RunToEndAsync("a = 1;");
		var sessions = server.Services.GetRequiredService<SessionManager>();

		clock.Advance(TimeSpan.FromMinutes(29));
		Assert.Equal(0, sessions.RemoveIdle());
		clock.Advance(TimeSpan.FromMinutes(2));
		Assert.Equal(1, sessions.RemoveIdle());

		await client.RunAsync("a");
		var welcome = await client.NextAsync<WelcomeMessage>();
		Assert.True(welcome.NewSession);
		var finished = await client.NextAsync<RunFinishedMessage>();
		Assert.Equal("Unknown function or variable 'a'.", Assert.Single(finished.Errors!).Message);
	}

	[Fact]
	public async Task SavefigSendsTheFileToTheBrowser()
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync();
		await client.RunAsync("savefig(\"plot.svg\", 400, 300)");
		Assert.Equal("Sent plot.svg to the browser to save.", Assert.Single((await client.NextAsync<OutputMessage>()).Entries).Text);
		var download = await client.NextAsync<DownloadMessage>();
		Assert.Equal(("plot.svg", "image/svg+xml"), (download.FileName, download.MediaType));
		Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"400\" height=\"300\"", download.Content);
		Assert.Equal(RunStatus.Ok, (await client.NextAsync<RunFinishedMessage>()).Status);

		// Only SVG: there is no bitmap renderer on the server.
		var (_, png) = await client.RunToEndAsync("savefig(\"plot.png\")");
		Assert.Equal("savefig: unsupported file type '.png'; use .svg.", Assert.Single(png.Errors!).Message);
	}

	[Fact]
	public async Task AReconnectedClientGetsTheRestOfARunningProgram()
	{
		await using var server = new WebTestServer();
		await using var first = await server.ConnectAsync();
		await first.RunAsync("disp(\"started\"); k = 0; while 1 do k = k + 1;", "loop");
		Assert.Equal("started", Assert.Single((await first.NextAsync<OutputMessage>()).Entries).Text);

		// The same session from a new connection, as after a lost connection or a reloaded page.
		await using var second = await server.ConnectAsync(first.Welcome.SessionId);
		Assert.True(second.Welcome.Running);
		Assert.False(second.Welcome.NewSession);
		await second.CancelAsync();
		var stopped = await second.NextAsync<RunFinishedMessage>();
		Assert.Equal(("loop", RunStatus.Cancelled), (stopped.Id, stopped.Status));
	}
}

/// <summary>A clock that only moves when told to; timers still use real time.</summary>
sealed class ManualClock : TimeProvider
{
	DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

	public override DateTimeOffset GetUtcNow() => now;

	public void Advance(TimeSpan time) => now += time;
}
