namespace MLCalc.Plotting.Tests;

public class FigureTests
{
	[Fact]
	public void DefaultsToOneWindowFromMinusTenToTen()
	{
		var snapshot = new Figure().Snapshot();
		Assert.Equal((1, 1), (snapshot.Rows, snapshot.Columns));
		var axes = snapshot[0, 0];
		Assert.Equal((-10, 10, -10, 10), (axes.XMin, axes.XMax, axes.YMin, axes.YMax));
	}

	[Fact]
	public void InitializeCreatesGridAndSelectWorksOneBased()
	{
		var figure = new Figure();
		figure.Initialize(1, 3);
		figure.SelectAxes(1, 3);
		figure.SetTitle("third");
		var snapshot = figure.Snapshot();
		Assert.Equal(3, snapshot.Axes.Length);
		Assert.Equal("third", snapshot[0, 2].Title);
		Assert.Equal("", snapshot[0, 0].Title);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(4, 1)]
	public void InitializeRejectsInvalidGrid(int rows, int columns)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new Figure().Initialize(rows, columns));
	}

	[Fact]
	public void SelectOutsideGridThrows()
	{
		var figure = new Figure();
		figure.Initialize(2, 2);
		Assert.Throws<ArgumentOutOfRangeException>(() => figure.SelectAxes(3, 1));
	}

	[Fact]
	public void EachSeriesUsesCurrentPenThenAdvances()
	{
		var figure = new Figure();
		figure.SetPen(5);
		figure.AddSeries([new(0, 0), new(1, 1)], PlotStyle.Line);
		figure.AddSeries([new(0, 1)], PlotStyle.Cross);
		var axes = figure.Current;
		Assert.Equal([5, 6], axes.Items.Select(i => i.Pen));
		Assert.Equal(7, axes.Pen);
	}

	[Fact]
	public void PenWrapsAround()
	{
		Assert.Equal(1, Pens.Normalize(16));
		Assert.Equal(15, Pens.Normalize(0));
	}

	[Fact]
	public void SettingRangeClearsItems()
	{
		var figure = new Figure();
		figure.AddSeries([new(0, 0)], PlotStyle.Line);
		figure.SetXRange(0, 5);
		Assert.Empty(figure.Current.Items);
		Assert.Equal((0, 5), (figure.Current.XMin, figure.Current.XMax));
	}

	[Fact]
	public void InvalidRangeThrows()
	{
		Assert.Throws<ArgumentException>(() => new Figure().SetYRange(2, 1));
	}

	[Fact]
	public void DotsWithSamePenAreCollected()
	{
		var figure = new Figure();
		figure.AddDot(1, 2);
		figure.AddDot(3, 4);
		figure.SetPen(4);
		figure.AddDot(5, 6);
		var items = figure.Current.Items;
		Assert.Equal(2, items.Length);
		Assert.Equal(2, ((DotCloud)items[0]).Points.Count);
	}

	[Fact]
	public void FunctionIsSampledOverCurrentXRange()
	{
		var figure = new Figure();
		figure.SetXRange(0, 2);
		figure.AddFunction("x^2", x => x * x);
		var series = Assert.IsType<FunctionSeries>(Assert.Single(figure.Current.Items));
		Assert.Equal(Figure.FunctionSamples, series.Points.Length);
		Assert.Equal(new PlotPoint(0, 0), series.Points[0]);
		Assert.Equal(4, series.Points[^1].Y, 12);
	}

	[Fact]
	public void ComplexPlotAfterRealPlotClearsWindow()
	{
		var figure = new Figure();
		figure.AddSeries([new(0, 0)], PlotStyle.Line);
		figure.AddComplexSeries([new(1, 1)], PlotStyle.Cross);
		Assert.Equal(AxesKind.Complex, figure.Current.Kind);
		Assert.Single(figure.Current.Items);
	}

	[Fact]
	public void ChangesRaiseEventAndBumpVersion()
	{
		var figure = new Figure();
		int raised = 0;
		figure.Changed += (_, _) => raised++;
		long before = figure.Snapshot().Version;
		figure.ShowGrid();
		Assert.Equal(1, raised);
		Assert.True(figure.Snapshot().Version > before);
	}

	[Fact]
	public void SnapshotsAreNotAffectedByLaterChanges()
	{
		var figure = new Figure();
		var snapshot = figure.Snapshot();
		figure.AddDot(1, 1);
		Assert.Empty(snapshot[0, 0].Items);
	}
}

public class TransformAndTickTests
{
	[Fact]
	public void MapsWorldToScreenWithYUp()
	{
		var axes = Axes.Default with { XMin = 0, XMax = 10, YMin = 0, YMax = 100 };
		var t = new PlotTransform(axes, new ScreenRect(50, 20, 200, 100));
		Assert.Equal(50, t.ToScreenX(0));
		Assert.Equal(250, t.ToScreenX(10));
		Assert.Equal(120, t.ToScreenY(0));
		Assert.Equal(20, t.ToScreenY(100));
		Assert.Equal(5, t.ToWorldX(t.ToScreenX(5)), 12);
		Assert.Equal(42, t.ToWorldY(t.ToScreenY(42)), 12);
	}

	[Theory]
	[InlineData(0.7, 1)]
	[InlineData(1.5, 2)]
	[InlineData(3, 5)]
	[InlineData(7, 10)]
	[InlineData(0.03, 0.05)]
	public void NiceSteps(double rough, double expected)
	{
		Assert.Equal(expected, AxisTicks.NiceStep(rough), 12);
	}

	[Fact]
	public void TicksAreNiceAndInsideRange()
	{
		Assert.Equal([-10, -5, 0, 5, 10], AxisTicks.Compute(-10, 10, 4));
		Assert.Equal([0, 0.1, 0.2, 0.3], AxisTicks.Compute(0, 0.3, 3));
	}

	[Fact]
	public void EmptyRangeHasNoTicks()
	{
		Assert.Empty(AxisTicks.Compute(1, 1));
	}
}
