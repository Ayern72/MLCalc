using MLCalc.Plotting;
using static MLCalc.Interpreter.Tests.TestHost;

namespace MLCalc.Interpreter.Tests;

public class Plot3DTests
{
	readonly TestHost host = new();

	Axes Current => host.Figure.Current;

	[Fact]
	public void Fplot3dDrawsASurfaceOverTheRanges()
	{
		host.Run("xrange(0, 2); yrange(-1, 1); fplot3d(\"x\", \"y\", \"x^2 + y\")");
		var surface = Assert.IsType<Surface>(Assert.Single(Current.Items));
		Assert.Equal(AxesKind.Space, Current.Kind);
		Assert.Equal((40, 40), (surface.XCount, surface.YCount));
		Assert.Equal("x^2 + y", surface.Label);
		var corner = surface[39, 39];
		Assert.Equal((2, 1, 5), (corner.X, corner.Y, corner.Z));
		var p = surface[13, 7];
		Assert.Equal(p.X * p.X + p.Y, p.Z, 12);
	}

	[Fact]
	public void Fplot3dTakesTheNumberOfGridLines()
	{
		host.Run("fplot3d(\"u\", \"v\", \"u*v\", 5)");
		Assert.Equal(5, Assert.IsType<Surface>(Assert.Single(Current.Items)).XCount);
	}

	[Theory]
	[InlineData("fplot3d(\"x\", \"y\", \"x\", 1)", "between 2 and 200")]
	[InlineData("fplot3d(\"x\", \"y\", \"x\", 201)", "between 2 and 200")]
	[InlineData("fplot3d(\"x\", \"y\", \"x\", 2.5)", "integer")]
	[InlineData("fplot3d(\"x\", \"X\", \"x\")", "different names")]
	[InlineData("fplot3d(\"x\", \"y\")", "3 to 4 arguments")]
	[InlineData("fplot3d(x, \"y\", \"x\")", "string")]
	[InlineData("fplot3d(\"x\", \"y\", \"[x y]\")", "must return a scalar")]
	[InlineData("fplot3d(\"x\", \"y\", \"x(3)\")", "x(3)")]
	[InlineData("fplot3d(\"x\", \"y\", \"x + \")", "In \"x + \"")]
	[InlineData("fplot3d(\"x\", \"y\", \"plot(x)\")", "is a command")]
	public void Fplot3dErrors(string program, string message)
	{
		Assert.Contains(message, host.Fail(program).Message);
	}

	[Fact]
	public void UndefinedPointsBecomeHoles()
	{
		// x = y exactly on the diagonal of the grid, where 1/(x - y) is a division by zero.
		host.Run("fplot3d(\"x\", \"y\", \"1/(x - y)\", 5)");
		var surface = (Surface)Assert.Single(Current.Items);
		Assert.True(double.IsNaN(surface[2, 2].Z));
		Assert.Equal(1 / (surface[3, 1].X - surface[3, 1].Y), surface[3, 1].Z, 12);
	}

	[Fact]
	public void TheVariablesDoNotTouchYourVariablesAndOthersCanBeUsed()
	{
		host.Run("x = 7; a = 3; fplot3d(\"x\", \"y\", \"a*x + y\", 3)");
		AssertClose(7, host["x"]);
		var surface = (Surface)Assert.Single(Current.Items);
		Assert.Equal(3 * -10 + -10, surface[0, 0].Z);
	}

	[Fact]
	public void TwoVariableFunctionsWorkInsideBuiltInCalls()
	{
		// The arguments of max and sum use both variables, so they are evaluated through compiled code.
		host.Run("fplot3d(\"x\", \"y\", \"max([x y]) + sum([x; y])\", 3)");
		var surface = (Surface)Assert.Single(Current.Items);
		var p = surface[2, 0]; // x = 10, y = -10
		Assert.Equal(10 + 0, p.Z);
	}

	[Fact]
	public void Fplot3dCanBeCancelled()
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var watch = System.Diagnostics.Stopwatch.StartNew();
		var result = host.Interpreter.Execute("fplot3d(\"x\", \"y\", \"sum((1:20000) * x) * y\", 200)", cts.Token);
		Assert.True(result.Cancelled);
		Assert.InRange(watch.ElapsedMilliseconds, 0, 3000);
	}

	[Fact]
	public void Plot3dWithVectors()
	{
		host.Run("t = 0:0.5:2; plot3d(cos(t), sin(t), t)");
		var series = Assert.IsType<PointSeries3D>(Assert.Single(Current.Items));
		Assert.Equal(PlotStyle.Line, series.Style);
		Assert.Equal(5, series.Points.Length);
		Assert.Equal(new Point3(System.Math.Cos(1), System.Math.Sin(1), 1), series.Points[2]);
		Assert.Equal(2, Current.Pen);
	}

	[Fact]
	public void Plot3dWithMatricesDrawsEachRow()
	{
		host.Run("plot3d([1 2; 3 4], [5 6; 7 8], [0 0; 1 1], +)");
		Assert.Equal(2, Current.Items.Length);
		var second = (PointSeries3D)Current.Items[1];
		Assert.Equal(PlotStyle.Cross, second.Style);
		Assert.Equal([new Point3(3, 7, 1), new Point3(4, 8, 1)], second.Points.ToArray());
		Assert.Equal([1, 2], Current.Items.Select(i => i.Pen));
	}

	[Theory]
	[InlineData("plot3d([1 2 3; 4 5 6; 7 8 9; 0 0 0])")]
	[InlineData("plot3d([1 4 7 0; 2 5 8 0; 3 6 9 0])")]
	public void Plot3dWithAMatrixOfPoints(string program)
	{
		host.Run(program);
		var series = (PointSeries3D)Assert.Single(Current.Items);
		Assert.Equal([new Point3(1, 2, 3), new Point3(4, 5, 6), new Point3(7, 8, 9), new Point3(0, 0, 0)], series.Points.ToArray());
	}

	[Fact]
	public void Plot3dTakesAStyleWithAMatrixOfPoints()
	{
		host.Run("plot3d([1 2 3], \".\")");
		Assert.Equal(PlotStyle.Dot, ((PointSeries3D)Assert.Single(Current.Items)).Style);
	}

	[Theory]
	[InlineData("plot3d([1 2], [1 2 3], [1 2 3])", "same number of elements")]
	[InlineData("plot3d(ones(2, 2), ones(2, 3), ones(2, 2))", "same size")]
	[InlineData("plot3d(ones(2, 4))", "n×3")]
	[InlineData("plot3d([1 2 3], 5)", "plot style")]
	[InlineData("plot3d(1, 2, 3, 4, 5)", "1 to 4 arguments")]
	public void Plot3dErrors(string program, string message)
	{
		Assert.Contains(message, host.Fail(program).Message);
	}

	[Fact]
	public void Dot3dAddsDotsWithoutChangingThePen()
	{
		host.Run("dot3d(1, 2, 3); dot3d(4, 5, 6)");
		var cloud = Assert.IsType<DotCloud3D>(Assert.Single(Current.Items));
		Assert.Equal(2, cloud.Points.Count);
		Assert.Equal(1, Current.Pen);
		Assert.Contains("argument 3 must be real", host.Fail("dot3d(1, 2, 3i)").Message);
	}

	[Fact]
	public void TwoDCommandsSwitchA3DWindowBack()
	{
		host.Run("plot3d([1 2 3]); fplot(\"x\", \"x\")");
		Assert.Equal(AxesKind.Cartesian, Current.Kind);
		Assert.IsType<FunctionSeries>(Assert.Single(Current.Items));
	}

	[Fact]
	public void ZrangeAndZlabel()
	{
		host.Run("plot3d([1 2 3]); zrange(0, 100); zlabel(\"height\")");
		Assert.Empty(Current.Items);
		Assert.Equal((0, 100, "height"), (Current.ZMin, Current.ZMax, Current.ZTitle));
		host.Run("ztitle(\"h\")");
		Assert.Equal("h", Current.ZTitle);
		Assert.Contains("Invalid range", host.Fail("zrange(2, 1)").Message);
	}

	[Fact]
	public void ViewSetsTheCameraAngles()
	{
		host.Run("view(30, 45)");
		Assert.Equal((30, 45), (Current.Camera.Azimuth, Current.Camera.Elevation));
		host.Run("view(2)");
		Assert.Equal((0, 90), (Current.Camera.Azimuth, Current.Camera.Elevation));
		host.Run("view(3)");
		Assert.Equal(Camera.Default, Current.Camera);
		host.Run("view(10, 10); view");
		Assert.Equal(Camera.Default, Current.Camera);
		Assert.Contains("view(az, el)", host.Fail("view(4)").Message);
		Assert.Contains("elevation", host.Fail("view(0, 100)").Message);
	}

	[Fact]
	public void CampositionAndCamtarget()
	{
		host.Run("camposition(30, -40, 20); camtarget([1 2 3])");
		Assert.Equal(new Point3(30, -40, 20), Current.Camera.Position);
		Assert.Equal(new Point3(1, 2, 3), Current.Camera.Target);
		Assert.Contains("outside the box", host.Fail("camposition(1, 2, 3)").Message);
		Assert.Contains("expected a point", host.Fail("camposition(1, 2)").Message);
		Assert.Contains("expected a point", host.Fail("camtarget([1 2])").Message);
		Assert.Contains("must be real", host.Fail("camtarget([1 2 3i])").Message);
		Assert.Contains("where the camera is", host.Fail("camtarget(30, -40, 20)").Message);
	}

	[Fact]
	public void ThreeDCommandsCannotBeUsedInFunctionsGivenAsText()
	{
		Assert.Contains("is a command", host.Fail("fplot(\"x\", \"dot3d(x, x, x)\")").Message);
	}

	[Fact]
	public void TplotIsGone()
	{
		Assert.Contains("Unknown function or variable 'tplot'", host.Fail("tplot(\"x\", \"x\")").Message);
	}
}
