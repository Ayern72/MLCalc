using MLCalc.Plotting;
using static MLCalc.Interpreter.Tests.TestHost;

namespace MLCalc.Interpreter.Tests;

public class PlotCommandTests
{
	readonly TestHost host = new();

	[Fact]
	public void PlotAddsSeriesWithStyle()
	{
		host.Run("plot([1 2 3], [4 5 6], +)");
		var series = Assert.IsType<PointSeries>(Assert.Single(host.Figure.Current.Items));
		Assert.Equal(PlotStyle.Cross, series.Style);
		Assert.Equal([new PlotPoint(1, 4), new PlotPoint(2, 5), new PlotPoint(3, 6)], series.Points.ToArray());
	}

	[Fact]
	public void PlotOfMatrixDrawsEachRow()
	{
		host.Run("plot([1 2], [1 2; 3 4; 5 6])");
		Assert.Equal(3, host.Figure.Current.Items.Length);
	}

	[Fact]
	public void PlotWithMismatchedLengthsIsAnError()
	{
		Assert.Contains("x has 2 elements", host.Fail("plot([1 2], [1 2 3])").Message);
	}

	[Fact]
	public void RangesTitlesAndPen()
	{
		host.Run("xrange(0, 5); yrange(-1, 1); title(\"T\"); xtitle(\"X\"); ytitle(\"Y\"); pen(4); grid");
		var axes = host.Figure.Current;
		Assert.Equal((0, 5, -1, 1), (axes.XMin, axes.XMax, axes.YMin, axes.YMax));
		Assert.Equal(("T", "X", "Y", 4, true), (axes.Title, axes.XTitle, axes.YTitle, axes.Pen, axes.ShowGrid));
	}

	[Fact]
	public void InvalidRangeIsAnError()
	{
		Assert.Contains("lower limit", host.Fail("xrange(1, 0)").Message);
	}

	[Fact]
	public void FplotSamplesFunctionAndLeavesGapsWhereUndefined()
	{
		host.Run("xrange(-1, 1); fplot(\"x\", \"1/x\")");
		var series = Assert.IsType<FunctionSeries>(Assert.Single(host.Figure.Current.Items));
		Assert.Equal("1/x", series.Label);
		Assert.Equal(-1, series.Points[0].Y, 12);
		Assert.False(host.Interpreter.TryGetVariable("x", out _));
	}

	[Fact]
	public void FplotReportsNonMathErrors()
	{
		Assert.Contains("nope", host.Fail("fplot(\"x\", \"nope(x)\")").Message);
	}

	[Fact]
	public void PolyplotAndCplot()
	{
		host.Run("polyplot([1 0 1])");
		Assert.IsType<FunctionSeries>(Assert.Single(host.Figure.Current.Items));
		host.Run("cplot([1+i, 2-i], +)");
		Assert.Equal(AxesKind.Complex, host.Figure.Current.Kind);
		var series = Assert.IsType<PointSeries>(Assert.Single(host.Figure.Current.Items));
		Assert.Equal(new PlotPoint(2, -1), series.Points[1]);
	}

	[Fact]
	public void SavefigUsesRegisteredFormatsAndRejectsOthers()
	{
		var saved = new List<(string Path, double Width, double Height, int Items)>();
		host.Interpreter.RegisterFigureFormat("png", (figure, path, width, height) => saved.Add((path, width, height, figure[0, 0].Items.Length)));
		host.Interpreter.WorkingDirectory = Path.GetTempPath();
		host.Run("dot(1, 1); savefig(\"a.PNG\", 300, 200)");
		var (path, width, height, items) = Assert.Single(saved);
		Assert.Equal(Path.Combine(Path.GetTempPath(), "a.PNG"), path);
		Assert.Equal((300, 200, 1), (width, height, items));

		var error = host.Fail("savefig(\"a.gif\")");
		Assert.Contains("unsupported file type '.gif'; use .svg or .png", error.Message);
		Assert.Contains("between 50 and 16000", host.Fail("savefig(\"a.png\", 10, 10)").Message);
	}

	[Fact]
	public void GridOfWindows()
	{
		host.Run("initdisplay(2, 2); currentwin(2, 1); dot(1, 1)");
		var snapshot = host.Figure.Snapshot();
		Assert.Equal((1, 0), (snapshot.CurrentRow, snapshot.CurrentColumn));
		Assert.Single(snapshot[1, 0].Items);
		Assert.Contains("no plot window", host.Fail("currentwin(3, 1)").Message);
	}
}

public class ScriptTests
{
	static string SamplesDirectory => Path.Combine(AppContext.BaseDirectory, "samples");

	static TestHost RunSample(string name)
	{
		var host = new TestHost();
		host.Interpreter.WorkingDirectory = SamplesDirectory;
		var result = host.Interpreter.ExecuteFile(name);
		Assert.True(result.Success, string.Join("\n", result.Messages()));
		return host;
	}

	public static TheoryData<string> Samples()
	{
		var data = new TheoryData<string>();
		foreach (var file in Directory.GetFiles(SamplesDirectory, "*.m").Order())
			data.Add(Path.GetFileName(file));
		return data;
	}

	[Theory]
	[MemberData(nameof(Samples))]
	public void EveryOriginalSampleRunsWithoutErrors(string file)
	{
		RunSample(file);
	}

	[Fact]
	public void Feigenbaum()
	{
		// 50 values of r; k is incremented before the test, so it runs 2..21 and a dot is drawn
		// for k > 40/3, i.e. k = 14..21: 8 dots per r.
		var host = RunSample("FEIGENBA.m");
		var cloud = Assert.IsType<DotCloud>(Assert.Single(host.Figure.Current.Items));
		Assert.Equal(400, cloud.Points.Count);
		Assert.Equal(3, cloud.Pen);
		// The logistic map stays in [0, 1] for r ≤ 4 and diverges beyond.
		Assert.All(cloud.Points.Where(p => p.X <= 4), p => Assert.InRange(p.Y, 0, 1));
		Assert.Contains(cloud.Points, p => p.X > 4 && !(p.Y is >= 0 and <= 1));
	}

	[Fact]
	public void FysikPlotsFiveSeriesFromTheDataTable()
	{
		var host = RunSample("FYSIK.m");
		Assert.Equal((6, 45), (host["t"].Rows, host["t"].Columns));
		var items = host.Figure.Current.Items;
		Assert.Equal(5, items.Length);
		Assert.Equal([PlotStyle.Cross, PlotStyle.Cross, PlotStyle.Cross, PlotStyle.Cross, PlotStyle.Line],
			items.Cast<PointSeries>().Select(s => s.Style));
	}

	[Fact]
	public void CosUsesThreeWindows()
	{
		var host = RunSample("COS.m");
		var snapshot = host.Figure.Snapshot();
		Assert.Equal((1, 3), (snapshot.Rows, snapshot.Columns));
		Assert.All(snapshot.Axes, axes => Assert.Single(axes.Items));
		Assert.Equal(1000, snapshot[0, 2].XMax);
		// "j=1" has no semicolon, then the loop prints j once per window.
		Assert.Equal(["j", "j", "j", "j"], host.Output.Values.Select(v => v.Name));
	}

	[Fact]
	public void BisectionScriptConverges()
	{
		var host = RunSample("TEST.m");
		double a = host["a"].ScalarValue.Real;
		double f(double x) => 1 / (x - 2) + 1 / (x - 3) + 1 / (x - 4) - 10;
		Assert.InRange(a, 2.01, 2.1);
		Assert.True(System.Math.Abs(f(a)) < 0.5, $"f({a}) = {f(a)}");
	}

	[Fact]
	public void NestedIfElsePrintsTheRightBranch()
	{
		var host = RunSample("TEST3.m");
		AssertClose(111111111, host.Output.Values.Single(v => v.Name == "ans").Value);
	}

	[Fact]
	public void PolynomialFitScriptDrawsDataFitAndCurves()
	{
		var host = RunSample("TEST5.m");
		var items = host.Figure.Current.Items;
		Assert.Equal(4, items.Length);
		Assert.IsType<PointSeries>(items[0]);
		Assert.All(items.Skip(1), item => Assert.IsType<FunctionSeries>(item));
		// The tplot/fplot formula built from r1..r5 equals the polyplot curve.
		var poly = (FunctionSeries)items[1];
		var formula = (FunctionSeries)items[3];
		for (int k = 0; k < poly.Points.Length; k += 100)
			Assert.Equal(poly.Points[k].Y, formula.Points[k].Y, 9);
	}

	[Fact]
	public void TypingAFileNameRunsTheScript()
	{
		var host = new TestHost();
		host.Interpreter.WorkingDirectory = SamplesDirectory;
		host.Run("TEST1.m");
		Assert.Single(host.Figure.Current.Items);
	}

	[Fact]
	public void RunCommandRunsNestedScript()
	{
		var directory = Directory.CreateTempSubdirectory("mlcalc");
		try
		{
			File.WriteAllText(Path.Combine(directory.FullName, "inner.m"), "y = x * 2;");
			File.WriteAllText(Path.Combine(directory.FullName, "bad.m"), "z = 1;\nz = nope;");
			var host = new TestHost();
			host.Interpreter.WorkingDirectory = directory.FullName;
			host.Run("x = 21; run(\"inner.m\")");
			AssertClose(42, host["y"]);
			Assert.Contains("bad.m at 2:5", host.Fail("run(\"bad.m\")").Message);
			Assert.Contains("Cannot find", host.Fail("run(\"missing.m\")").Message);
		}
		finally
		{
			directory.Delete(recursive: true);
		}
	}
}
