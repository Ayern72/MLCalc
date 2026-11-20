using System.Collections.Concurrent;
using MLCalc.Hosting;
using MLCalc.Web.Protocol;

namespace MLCalc.Web.Tests;

/// <summary>The workspace without the server, as the app uses it: no limits, messages straight to the page.</summary>
public class WorkspaceTests
{
	/// <summary>Collects the messages, as the app's page gets them.</summary>
	sealed class Collector : IMLCalcClient
	{
		readonly TaskCompletionSource<RunFinishedMessage> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public ConcurrentQueue<object> Messages { get; } = new();

		public Task<RunFinishedMessage> Finished => finished.Task.WaitAsync(TimeSpan.FromSeconds(30));

		public Task Welcome(WelcomeMessage message) => Add(message);
		public Task Output(OutputMessage message) => Add(message);
		public Task FigureChanged(FigureMessage message) => Add(message);
		public Task Download(DownloadMessage message) => Add(message);

		public Task RunFinished(RunFinishedMessage message)
		{
			Messages.Enqueue(message);
			finished.TrySetResult(message);
			return Task.CompletedTask;
		}

		Task Add(object message)
		{
			Messages.Enqueue(message);
			return Task.CompletedTask;
		}
	}

	[Fact]
	public async Task ByDefaultThereAreNoServerLimits()
	{
		var client = new Collector();
		using var workspace = new Workspace(new WorkspaceOptions(), () => client);
		// Over the server's 2 million elements and 200,000 characters.
		string source = "A = zeros(1, 3000000); disp(length(A))\n%" + new string('x', 300_000);
		await workspace.StartAsync(new RunRequest("1", source));
		var finished = await client.Finished;
		Assert.Equal(RunStatus.Ok, finished.Status);
		var output = client.Messages.OfType<OutputMessage>().SelectMany(m => m.Entries).Select(e => e.Text);
		Assert.Contains("3000000", output);
	}

	[Fact]
	public async Task WithoutAClientMessagesAreDroppedAndWelcomeHasItAll()
	{
		IMLCalcClient? client = null;
		using var workspace = new Workspace(new WorkspaceOptions(), () => client);
		await workspace.StartAsync(new RunRequest("1", "fplot(\"x\", \"sin(x)\")"));
		while (workspace.IsRunning)
			await Task.Delay(10);

		var collector = new Collector();
		client = collector;
		await workspace.SendWelcomeAsync("local", isNew: false, new Limits(0, 0, 0, 0, 0));
		var welcome = Assert.IsType<WelcomeMessage>(Assert.Single(collector.Messages));
		Assert.Equal(ItemType.Function, Assert.Single(Assert.Single(welcome.Figure.Windows).Replace!.Items).Type);
		Assert.Equal(WebProtocol.BuildVersion, welcome.Version);
	}

	[Fact]
	public async Task AFileFromTheProgramComesAfterItsOutput()
	{
		var client = new Collector();
		using var workspace = new Workspace(new WorkspaceOptions(), () => client);
		workspace.Plotting.RegisterFigureFormat(".txt", (snapshot, path, width, height) =>
		{
			workspace.Download(new DownloadMessage(path, "text/plain", "plot"));
			return "saved";
		});
		await workspace.StartAsync(new RunRequest("1", "disp(1)\nsavefig(\"a.txt\")"));
		Assert.Equal(RunStatus.Ok, (await client.Finished).Status);
		var kinds = client.Messages.Select(m => m.GetType().Name).Where(n => n != nameof(FigureMessage)).ToList();
		Assert.Equal([nameof(OutputMessage), nameof(DownloadMessage), nameof(RunFinishedMessage)], kinds);
	}
}
