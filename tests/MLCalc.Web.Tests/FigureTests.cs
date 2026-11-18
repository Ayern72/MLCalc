using System.Text.Json;
using MLCalc.Interpreter;
using MLCalc.Plotting;
using MLCalc.Web.Client;
using MLCalc.Web.Protocol;
using MLCalc.Web.Server;

namespace MLCalc.Web.Tests;

/// <summary>Plots between server and client: the format, the JSON, and sending only what changed.</summary>
public class FigureTests
{
	static readonly JsonSerializerOptions Json = WebProtocol.CreateJsonOptions();

	/// <summary>An interpreter that draws into a figure, as in a session.</summary>
	sealed class Plotter : IConsoleOutput
	{
		public Figure Figure { get; } = new();
		public MLCalcInterpreter Interpreter { get; }

		public Plotter()
		{
			Interpreter = new MLCalcInterpreter(this);
			Interpreter.AddPlotting(Figure);
		}

		public void Run(string source)
		{
			var result = Interpreter.Execute(source);
			Assert.True(result.Success, string.Join("\n", result.Messages()));
		}

		public void WriteValue(string name, MLCalc.LinearAlgebra.ComplexMatrix value) { }
		public void WriteLine(string text) { }
		public void Clear() { }
	}

	/// <summary>Through JSON and back, as a message travels.</summary>
	static FigureMessage Travel(FigureMessage message) =>
		JsonSerializer.Deserialize<FigureMessage>(JsonSerializer.Serialize(message, Json), Json)!;

	static void AssertSame(Axes expected, Axes actual)
	{
		Assert.Equal((expected.Kind, expected.XMin, expected.XMax, expected.YMin, expected.YMax, expected.ZMin, expected.ZMax),
			(actual.Kind, actual.XMin, actual.XMax, actual.YMin, actual.YMax, actual.ZMin, actual.ZMax));
		Assert.Equal((expected.Title, expected.XTitle, expected.YTitle, expected.ZTitle, expected.ShowGrid, expected.Pen),
			(actual.Title, actual.XTitle, actual.YTitle, actual.ZTitle, actual.ShowGrid, actual.Pen));
		Assert.Equal(expected.Camera, actual.Camera);
		Assert.Equal(expected.Items.Length, actual.Items.Length);
		for (int i = 0; i < expected.Items.Length; i++)
		{
			var (e, a) = (expected.Items[i], actual.Items[i]);
			Assert.Equal(e.GetType(), a.GetType());
			Assert.Equal(e.Pen, a.Pen);
			var (ep, ap) = (Coordinates(e), Coordinates(a));
			Assert.Equal(ep.Length, ap.Length);
			for (int k = 0; k < ep.Length; k++)
				Assert.True(double.IsNaN(ep[k]) ? double.IsNaN(ap[k]) : Math.Abs(ep[k] - ap[k]) <= 1e-6 * Math.Max(1, Math.Abs(ep[k])),
					$"Item {i}, coordinate {k}: expected {ep[k]}, got {ap[k]}.");
		}
	}

	/// <summary>All coordinates of an item, in order, with non-finite values as NaN.</summary>
	static double[] Coordinates(PlotItem item)
	{
		static double N(double v) => double.IsFinite(v) ? v : double.NaN;
		return item switch
		{
			PointSeries s => [.. s.Points.SelectMany(p => new[] { N(p.X), N(p.Y) })],
			FunctionSeries f => [.. f.Points.SelectMany(p => new[] { N(p.X), N(p.Y) })],
			DotCloud d => [.. d.Points.SelectMany(p => new[] { N(p.X), N(p.Y) })],
			PointSeries3D s => [.. s.Points.SelectMany(p => new[] { N(p.X), N(p.Y), N(p.Z) })],
			DotCloud3D d => [.. d.Points.SelectMany(p => new[] { N(p.X), N(p.Y), N(p.Z) })],
			Surface s => [s.XCount, s.YCount, .. s.Points.SelectMany(p => new[] { N(p.X), N(p.Y), N(p.Z) })],
			_ => throw new NotSupportedException(),
		};
	}

	[Fact]
	public void EveryKindOfWindowAndItemArrivesAsItWasDrawn()
	{
		var plotter = new Plotter();
		plotter.Run("""
			initdisplay(2, 2)
			xrange(-5, 5); yrange(-2, 3); title("Two"); xtitle("t"); ytitle("v"); grid
			plot([1 2 3], [1 nan 3], +); fplot("x", "1/x"); dot(1, 1); dot(2, 2)
			currentwin(1, 2)
			cplot([1+2i, 3-1i], *)
			currentwin(2, 1)
			zrange(0, 4); ztitle("z"); plot3d([0 1], [0 1], [0 1]); dot3d(1, 2, 3); fplot3d("x", "y", "x*y", 5)
			camposition(30, 20, 10); camtarget(1, 1, 1)
			currentwin(2, 2)
			view(20, 40); fplot3d("x", "y", "sqrt(x)", 4)
			""");
		var snapshot = plotter.Figure.Snapshot();
		var state = new FigureState();
		state.Apply(Travel(new FigureEncoder().Full(snapshot)));
		Assert.Equal((2, 2, 1, 1), (state.Rows, state.Columns, state.CurrentRow, state.CurrentColumn));
		for (int r = 0; r < 2; r++)
			for (int c = 0; c < 2; c++)
				AssertSame(snapshot[r, c], state[r, c]);
	}

	[Fact]
	public void TheJsonIsWrittenAsDocumented()
	{
		var item = FigureEncoder.ToItem(new PointSeries(3, PlotStyle.Cross, [new(1, 0.5), new(double.NaN, 2), new(double.PositiveInfinity, 1e300)]));
		Assert.Equal("""{"type":"series","pen":3,"x":[1,null,null],"y":[0.5,2,null],"style":"cross"}""", JsonSerializer.Serialize(item, Json));

		var window = FigureEncoder.ToWindow(Axes.Default with { Kind = AxesKind.Space, Camera = Camera.Default with { Target = new Point3(1, 2, 3) } });
		string json = JsonSerializer.Serialize(window, Json);
		Assert.StartsWith("""{"kind":"space","xMin":-10,"xMax":10,"yMin":-10,"yMax":10,"zMin":-10,"zMax":10,"title":"","xTitle":"","yTitle":"","zTitle":"","grid":false,"pen":1,""", json);
		Assert.EndsWith("\"camera\":{\"azimuth\":-37.5,\"elevation\":30,\"target\":[1,2,3]},\"items\":[]}", json);

		var update = new WindowUpdate(2, Append: new WindowAppend(4, [], new PointsData([1], [2])));
		Assert.Equal("""{"index":2,"append":{"pen":4,"items":[],"lastDots":{"x":[1],"y":[2]}}}""", JsonSerializer.Serialize(update, Json));
	}

	/// <summary>The other messages, as docs/web-protocol.md shows them.</summary>
	[Fact]
	public void TheMessagesAreWrittenAsDocumented()
	{
		Assert.Equal("""{"entries":[{"kind":"value","text":"x = 3"},{"kind":"clear","text":""}]}""",
			JsonSerializer.Serialize(new OutputMessage([new(OutputKind.Value, "x = 3"), new(OutputKind.Clear, "")]), Json));
		Assert.Equal("""{"id":"1","status":"error","errors":[{"message":"Bad.","line":2,"column":9,"length":7}],"outputTruncated":false}""",
			JsonSerializer.Serialize(new RunFinishedMessage("1", RunStatus.Error, [new RunError("Bad.", 2, 9, 7)]), Json));
		Assert.Equal("""{"id":"2","status":"timeLimit","outputTruncated":true}""",
			JsonSerializer.Serialize(new RunFinishedMessage("2", RunStatus.TimeLimit, OutputTruncated: true), Json));
		// < and > are written as \u003C and \u003E and + as \u002B: any JSON parser reads them back as the characters themselves.
		Assert.Equal("""{"fileName":"plot.svg","mediaType":"image/svg\u002Bxml","content":"\u003Csvg/\u003E"}""",
			JsonSerializer.Serialize(new DownloadMessage("plot.svg", "image/svg+xml", "<svg/>"), Json));

		// What a client sends.
		Assert.Equal(new RunRequest("1", "x = 1 + 2"), JsonSerializer.Deserialize<RunRequest>("""{ "id": "1", "source": "x = 1 + 2" }""", Json));
		var camera = JsonSerializer.Deserialize<CameraUpdate>("""{ "row": 0, "column": 1, "camera": { "azimuth": 20, "elevation": 10, "distance": 2.5 } }""", Json)!;
		Assert.Equal((0, 1, 20.0, 10.0, (double?)2.5, (double[]?)null), (camera.Row, camera.Column, camera.Camera.Azimuth, camera.Camera.Elevation, camera.Camera.Distance, camera.Camera.Position));
	}

	[Fact]
	public void OnlyWhatChangedIsSent()
	{
		var plotter = new Plotter();
		var encoder = new FigureEncoder();
		var state = new FigureState();
		void Step(string source, Action<FigureMessage>? check = null)
		{
			plotter.Run(source);
			var message = encoder.Changes(plotter.Figure.Snapshot());
			Assert.NotNull(message);
			check?.Invoke(message);
			state.Apply(Travel(message));
			var snapshot = plotter.Figure.Snapshot();
			for (int i = 0; i < snapshot.Axes.Length; i++)
				AssertSame(snapshot.Axes[i], state.Snapshot.Axes[i]);
		}

		Step("initdisplay(1, 2)", m => Assert.Equal(2, m.Windows.Count));
		Step("dot(1, 1)", m => Assert.Single(Assert.Single(Assert.Single(m.Windows).Append!.Items).X));
		// More dots in the same cloud: only the new points.
		Step("for k = 2 to 4 do dot(k, k)", m =>
		{
			var append = Assert.Single(m.Windows).Append!;
			Assert.Empty(append.Items);
			Assert.Equal([2f, 3f, 4f], append.LastDots!.X);
		});
		Step("plot([1 2], [3 4])", m => Assert.Single(Assert.Single(m.Windows).Append!.Items));
		// A new range clears the window: sent whole.
		Step("xrange(0, 5)", m => Assert.NotNull(Assert.Single(m.Windows).Replace));
		Step("currentwin(1, 2); fplot(\"x\", \"x^2\")", m => Assert.Equal((1, 0), (Assert.Single(m.Windows).Index, m.CurrentRow)));
		Step("title(\"Squares\")", m => Assert.NotNull(Assert.Single(m.Windows).Replace));
		Step("currentwin(1, 1)", m => Assert.Empty(m.Windows));
		Assert.Null(encoder.Changes(plotter.Figure.Snapshot()));
	}

	[Fact]
	public async Task PlotsArriveBetweenOutputAndRunFinished()
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync();
		Assert.Equal((1, 1), (client.Welcome.Figure.Rows, client.Welcome.Figure.Columns));
		Assert.Empty(Assert.Single(client.Welcome.Figure.Windows).Replace!.Items);

		await client.RunAsync("x = 1\nfplot(\"x\", \"sin(x)\")");
		Assert.Equal("x = 1", Assert.Single((await client.NextAsync<OutputMessage>()).Entries).Text);
		var figure = await client.NextAsync<FigureMessage>();
		var function = Assert.Single(Assert.Single(figure.Windows).Append!.Items);
		Assert.Equal((ItemType.Function, "sin(x)", Figure.FunctionSamples), (function.Type, function.Label, function.X.Length));
		Assert.Equal(RunStatus.Ok, (await client.NextAsync<RunFinishedMessage>()).Status);

		// Reconnecting gets the plots whole.
		await using var again = await server.ConnectAsync(client.Welcome.SessionId);
		Assert.Equal(ItemType.Function, Assert.Single(Assert.Single(again.Welcome.Figure.Windows).Replace!.Items).Type);
	}

	[Fact]
	public void ACameraTheClientSetIsNotSentBack()
	{
		var plotter = new Plotter();
		var encoder = new FigureEncoder();
		plotter.Run("initdisplay(1, 2); currentwin(1, 2); fplot3d(\"x\", \"y\", \"x*y\", 5)");
		encoder.Full(plotter.Figure.Snapshot());

		var camera = new Camera(10, 20, 2, null, null);
		Assert.True(plotter.Figure.SetCamera(0, 1, camera));
		encoder.ClientHasCamera(1, camera);
		Assert.Empty(encoder.Changes(plotter.Figure.Snapshot())!.Windows);

		// What a program draws afterwards is still sent, without the window.
		plotter.Run("dot3d(1, 1, 1)");
		var append = Assert.Single(encoder.Changes(plotter.Figure.Snapshot())!.Windows).Append;
		Assert.Single(append!.Items);
	}

	[Fact]
	public async Task TheServerKeepsTheCameraTheUserSet()
	{
		await using var server = new WebTestServer();
		await using var client = await server.ConnectAsync();
		await client.RunToEndAsync("fplot3d(\"x\", \"y\", \"x*y\", 5)");
		var camera = new CameraData(15, 25, 3);
		await client.SetCameraAsync(new CameraUpdate(0, 0, camera));
		// Ignored: not a camera the commands could make, and not a 3D window.
		await client.SetCameraAsync(new CameraUpdate(0, 0, new CameraData(0, 120)));
		await client.SetCameraAsync(new CameraUpdate(0, 0, new CameraData(0, 0, Position: [1, 2])));
		await client.SetCameraAsync(new CameraUpdate(2, 2, camera));

		// The next program's drawing comes as an addition: the window was not sent back.
		await client.RunAsync("dot3d(1, 1, 1)");
		var update = Assert.Single((await client.NextAsync<FigureMessage>()).Windows);
		Assert.NotNull(update.Append);
		await client.NextAsync<RunFinishedMessage>();

		await using var again = await server.ConnectAsync(client.Welcome.SessionId);
		Assert.Equal(camera, Assert.Single(again.Welcome.Figure.Windows).Replace!.Camera);
	}
}
