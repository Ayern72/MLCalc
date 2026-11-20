using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MLCalc.Interpreter;
using MLCalc.Plotting;
using MLCalc.Web.Client;
using MLCalc.Web.Protocol;
using MLCalc.Web.Server;

namespace MLCalc.Web.Tests;

/// <summary>
/// The test cases shared with the TypeScript client (tests/fixtures): plot windows with the SVG that
/// the C# code draws for them, and .NET's formatting of tricky numbers. The TypeScript tests must
/// produce the same, so both clients draw the same pictures. Here they are checked against the C#
/// code; with the environment variable MLCALC_UPDATE_FIXTURES=1 they are written anew instead, after
/// a deliberate change to the drawing, so that the change shows up as a diff to review.
/// </summary>
public class SharedFixtureTests
{
	static readonly JsonSerializerOptions Json = WebProtocol.CreateJsonOptions();

	static readonly JsonSerializerOptions FileJson = new(WebProtocol.CreateJsonOptions())
	{
		WriteIndented = true,
		IndentCharacter = '\t',
		IndentSize = 1,
		NewLine = "\r\n",
		// Readable SVG text in the files: < > & and quotes as themselves.
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	static bool Update => Environment.GetEnvironmentVariable("MLCALC_UPDATE_FIXTURES") == "1";

	/// <summary>A plot window made by an MLCalc program, drawn at a size.</summary>
	public sealed record DrawingCase(string Name, string Program, double Width = 400, double Height = 300, bool IsCurrent = false, bool Preview = false);

	public static readonly TheoryData<DrawingCase> Drawings =
	[
		new("line", "t = 0:0.5:6; plot(t, sin(t))"),
		new("function-with-poles", "xrange(-5, 5); yrange(-4, 4); fplot(\"x\", \"tan(x)\")"),
		new("markers", "xrange(0, 4); yrange(0, 7); plot([1 2 3], [4 5 6], +); plot([1 2 3], [6 5 4], *); plot([1.5 2.5], [1 2], \".\")"),
		new("dots-and-pens", "xrange(0, 1); yrange(0, 1); for k = 1:40 do begin pen(1 + mod(k, 3)); dot(k / 41, mod(k * 7, 41) / 41) end"),
		new("grid-and-titles", "xrange(-1, 5); yrange(-2, 2); grid; title(\"Waves <&> 'quoted'\"); xlabel(\"time\"); ylabel(\"height\"); fplot(\"x\", \"sin(3*x)\")"),
		new("complex-plane", "cplot(exp(1i * (0:0.2:6.4)) .* (1 + (0:0.2:6.4) / 4))"),
		new("far-outside", "xrange(0, 1); yrange(0, 1); plot([-1e6 0.5 1e9], [0.5 1e12 0.5]); plot([0.2 0.4 0.6], [-3 0.5 1e300], +)"),
		new("small-window", "fplot(\"x\", \"x^2/10\")", Width: 120, Height: 90),
		new("tiny-window", "fplot(\"x\", \"x\")", Width: 60, Height: 50),
		new("current-window", "initdisplay(1, 2); plot([1 2 3], [3 1 2])", IsCurrent: true),
		new("odd-ranges", "xrange(-0.0031, 0.0072); yrange(1250000, 3125000); plot([-0.002 0.007], [1300000 3000000])", Width: 523, Height: 317),
		new("huge-and-tiny-ticks", "xrange(1e-7, 3e-7); yrange(-2e20, 5e20); plot([1.5e-7 2.5e-7], [0 1e20])"),
		new("gaps", "x = 0:0.25:4; y = sqrt(x - 2); plot(x, real(y)); fplot(\"x\", \"1/(x - 1)\")"),
		new("polynomial", "xrange(-3, 3); yrange(-10, 10); polyplot([1 0 -4 1])", Width: 640, Height: 480),
	];

	[Theory]
	[MemberData(nameof(Drawings))]
	public void DrawingsMatchTheFixtures(DrawingCase drawing)
	{
		var plotter = new Plotter();
		plotter.Run(drawing.Program);
		var figure = plotter.Figure.Snapshot();
		var axes = figure[figure.CurrentRow, figure.CurrentColumn];

		// As a client gets it: through the protocol and its JSON (single precision), and back.
		var window = JsonSerializer.Deserialize<PlotWindow>(JsonSerializer.Serialize(FigureEncoder.ToWindow(axes), Json), Json)!;
		string svg = SvgWriter.RenderWindow(FigureState.ToAxes(window), drawing.Width, drawing.Height, "w", drawing.IsCurrent, drawing.Preview);

		var fixture = new JsonObject
		{
			["name"] = drawing.Name,
			["program"] = drawing.Program,
			["width"] = drawing.Width,
			["height"] = drawing.Height,
			["isCurrent"] = drawing.IsCurrent,
			["preview"] = drawing.Preview,
			["window"] = JsonSerializer.SerializeToNode(window, Json),
			// One string per line, so that a change shows up as a readable diff.
			["svg"] = new JsonArray([.. svg.TrimEnd('\n').Split('\n').Select(line => (JsonNode)line)]),
		};
		Check(Path.Combine("drawings", drawing.Name + ".json"), fixture);
	}

	/// <summary>
	/// .NET's formatting of numbers as the SVG text uses it ("0.##", "G6", "G5", the default round-trip
	/// format, Math.Round to 2 decimals), and the axis ticks, for values where JavaScript's own
	/// formatting would differ.
	/// </summary>
	[Fact]
	public void NumberFormatsMatchTheFixture()
	{
		var random = new Random(20261120);
		double[] chosen =
		[
			0, -0.0, 1, -1, 0.5, 1.5, 2.5, -2.5, 0.125, 0.005, 0.015, 0.025, 0.035, 1.005, 2.675, 1.115, 1.125, -1.125,
			0.001, -0.001, -0.004, -0.005, -0.006, 0.0049999, 0.1 + 0.2, 1.0 / 3, 2.0 / 3, -2.0 / 3, 99.995, 99.994999,
			437.5, 523.0 / 2, 62, 18, 1.5, 2.2, -2.2, 3.5, 7, 1.1, 0.35 * 11, 11 * 0.35,
			123456, 1234567, 999999.5, 999995, 9999995, 99999.95, 0.000123456789, 1e-4, 1e-5, 1.5e-5, 1e-7, 2.5e-7,
			1e14, 1e15, 123456789012345.6, 1e16, 1e20, 5e20, -2e20, 1e21, 1.7976931348623157e308, 5e-324, 4.9406564584124654e-324,
			1250000, 3125000, 0.0072, -0.0031, Math.PI, Math.E, -Math.PI * 1e9, 6.02214076e23, 1.602176634e-19,
			1e17, 1e18, 1e19, 9.99e15, 1.2345678901234567e16, 1.2345678901234567e17, 123456789012345678, 1.5e17, -1e17, 0.00012, 0.000012,
		];
		var values = chosen
			.Concat(Enumerable.Range(0, 60).Select(_ => (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-8, 12))))
			.Concat(Enumerable.Range(0, 40).Select(_ => Math.Round((random.NextDouble() - 0.5) * 2000, 3) + 0.005))
			.ToList();

		var cases = new JsonArray();
		foreach (double value in values)
			cases.Add(new JsonObject
			{
				["value"] = value,
				["fixed2"] = value.ToString("0.##", CultureInfo.InvariantCulture),
				["g6"] = value.ToString("G6", CultureInfo.InvariantCulture),
				["g5"] = value.ToString("G5", CultureInfo.InvariantCulture),
				["roundTrip"] = value.ToString(CultureInfo.InvariantCulture),
				["round2"] = Math.Round(value, 2),
			});

		(double Min, double Max, int Count)[] axes =
		[
			(-10, 10, 4), (-10, 10, 5), (0, 1, 6), (-0.0031, 0.0072, 5), (1250000, 3125000, 5), (1e-7, 3e-7, 4),
			(-2e20, 5e20, 4), (0.1, 0.7, 6), (-1, 1, 3), (3, 3.000001, 4), (-1e308, 1e308, 4), (1e15, 1e15 + 1, 4),
			(-5, 5, 10), (2, 1, 4), (0, 6.4, 7), (-0.30000000000000004, 0.30000000000000004, 6),
		];
		var ticks = new JsonArray();
		foreach (var (min, max, count) in axes)
			ticks.Add(new JsonObject
			{
				["min"] = min,
				["max"] = max,
				["count"] = count,
				["ticks"] = new JsonArray([.. AxisTicks.Compute(min, max, count).Select(t => (JsonNode)t)]),
			});

		Check("number-format.json", new JsonObject { ["numbers"] = cases, ["ticks"] = ticks });
	}

	/// <summary>Compares the file with what the C# code makes, or writes it (MLCALC_UPDATE_FIXTURES=1).</summary>
	static void Check(string relativePath, JsonNode fixture)
	{
		string path = Path.Combine(FixtureFolder, relativePath);
		string expected = fixture.ToJsonString(FileJson) + "\r\n";
		if (Update || !File.Exists(path))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, expected);
			if (!Update)
				Assert.Fail($"{relativePath} did not exist and was written; run the tests again.");
			return;
		}
		string actual = File.ReadAllText(path);
		Assert.True(actual == expected,
			$"{relativePath} differs from what the C# code makes now. If the change is deliberate, run the tests with MLCALC_UPDATE_FIXTURES=1 and review the diff.");
	}

	/// <summary>tests/fixtures in the repository, found from the test's folder.</summary>
	static string FixtureFolder { get; } = FindFixtureFolder();

	static string FindFixtureFolder()
	{
		for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
			if (File.Exists(Path.Combine(folder.FullName, "MLCalc.slnx")))
				return Path.Combine(folder.FullName, "tests", "fixtures");
		throw new InvalidOperationException("The repository's root (MLCalc.slnx) was not found above " + AppContext.BaseDirectory);
	}

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
}
