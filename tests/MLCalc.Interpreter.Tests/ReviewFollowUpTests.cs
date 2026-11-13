using System.Globalization;
using MLCalc.Plotting;

namespace MLCalc.Interpreter.Tests;

/// <summary>Smaller findings from the code review: plotting errors, message format, NaN handling.</summary>
public class ReviewFollowUpTests
{
	readonly TestHost host = new();

	[Fact]
	public void FplotReportsIndexAndSizeErrorsInsteadOfDrawingNothing()
	{
		host.Run("v = [1 2 3];");
		Assert.Contains("exceeds the size", host.Fail("fplot(\"x\", \"v(1, 5) * x\")").Message);
		Assert.Contains("multiply", host.Fail("fplot(\"x\", \"[1 2] * [3 4] + x\")").Message);
		Assert.Empty(host.Figure.Current.Items);
	}

	[Fact]
	public void FplotStillLeavesGapsWhereTheFunctionIsUndefined()
	{
		host.Run("xrange(-1, 1); fplot(\"x\", \"1/round(x)\")");
		var series = Assert.IsType<FunctionSeries>(Assert.Single(host.Figure.Current.Items));
		Assert.Contains(series.Points, p => double.IsNaN(p.Y));
		Assert.Contains(series.Points, p => double.IsFinite(p.Y));
	}

	[Fact]
	public void MessagesUseTheSameNumberFormatOnEveryComputer()
	{
		var culture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("sv-SE"); // decimal comma
			Assert.Equal("No root found near 1.5.", host.Fail("solve(\"x\", \"x^2 + 1\", 1.5)").Message);
			Assert.Contains("(2.5) must be less than the upper limit (1.5)", host.Fail("xrange(2.5, 1.5)").Message);
			Assert.Contains("Index 4.5", host.Fail("v = [1 2]; v(4.5) = 1").Message.Replace("must be a positive integer, got ", "Index "));
		}
		finally
		{
			CultureInfo.CurrentCulture = culture;
		}
	}

	[Fact]
	public void AFullPlotWindowIsReportedAsAnError()
	{
		var figure = new Figure(maxPointsPerWindow: 5);
		var interpreter = new MLCalcInterpreter(figure, new RecordingOutput());
		var result = interpreter.Execute("for k = 1:10 do dot(k, k)");
		Assert.Equal("The plot window is full: it can hold at most 5 points. Clear it (clear, xrange or yrange) before plotting more.", result.Error?.Message);
		Assert.True(interpreter.Execute("clear; dot(1, 1)").Success);
	}

	[Fact]
	public void MaxAndMinIgnoreNaN()
	{
		TestHost.AssertClose(3, host.Eval("max([1 nan 3])"));
		TestHost.AssertClose(3, host.Eval("max([nan 1 3])"));
		TestHost.AssertClose(1, host.Eval("min([nan 1 3])"));
		TestHost.AssertMatrix(new double[,] { { 1, 4 } }, host.Eval("max([nan 4], [1 nan])"));
		Assert.True(double.IsNaN(host.Eval("max([nan nan])").ScalarValue.Real));
	}

	[Fact]
	public void ForLoopOverAMatrixVisitsItsColumns()
	{
		host.Run("s = [0; 0]; n = 0;\nfor c = [1 2 3; 10 20 30]\n\ts = s + c; n = n + 1;\nend");
		TestHost.AssertMatrix(new double[,] { { 6 }, { 60 } }, host["s"]);
		TestHost.AssertClose(3, host["n"]);
	}

	[Theory]
	[InlineData("comb(1e12, 5e11)")]
	[InlineData("perm(1e12, 1e12)")]
	public void HugeCombinatoricsOverflowQuickly(string expression)
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		Assert.Equal(double.PositiveInfinity, host.Eval(expression).ScalarValue.Real);
		Assert.True(watch.ElapsedMilliseconds < 2000, $"took {watch.ElapsedMilliseconds} ms");
	}
}
