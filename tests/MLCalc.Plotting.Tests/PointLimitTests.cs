namespace MLCalc.Plotting.Tests;

/// <summary>
/// Plot items stay until a window is cleared, so the number of points per window is limited
/// (found in the memory review: a dot loop retained about 40 MB per second).
/// </summary>
public class PointLimitTests
{
	[Fact]
	public void TheDefaultLimitIsTwoMillionPoints()
	{
		Assert.Equal(2_000_000, new Figure().MaxPointsPerWindow);
	}

	[Fact]
	public void DotsUpToTheLimitAreAcceptedAndThenRefused()
	{
		var figure = new Figure(maxPointsPerWindow: 10);
		for (int k = 0; k < 10; k++)
			figure.AddDot(k, k);
		Assert.Equal(10, figure.Current.PointCount);
		var error = Assert.Throws<ArgumentException>(() => figure.AddDot(10, 10));
		Assert.Equal("The plot window is full: it can hold at most 10 points. Clear it (clear, xrange or yrange) before plotting more.", error.Message);
		Assert.Equal(10, figure.Current.PointCount);
	}

	[Fact]
	public void SeriesAndCurvesCountTogetherWithDots()
	{
		var figure = new Figure(maxPointsPerWindow: 1500);
		figure.AddSeries([new(0, 0), new(1, 1), new(2, 2)], PlotStyle.Line);
		figure.AddDot(5, 5);
		figure.AddFunction("x", x => x);
		Assert.Equal(3 + 1 + Figure.FunctionSamples, figure.Current.PointCount);
		Assert.Throws<ArgumentException>(() => figure.AddFunction("x", x => x));
		Assert.Equal(3, figure.Current.Items.Length);
	}

	[Fact]
	public void ClearingAWindowMakesRoomAgain()
	{
		var figure = new Figure(maxPointsPerWindow: 3);
		for (int k = 0; k < 3; k++)
			figure.AddDot(k, k);
		figure.ClearCurrent();
		Assert.Equal(0, figure.Current.PointCount);
		figure.AddDot(1, 1);
		figure.SetXRange(0, 1);
		Assert.Equal(0, figure.Current.PointCount);
		figure.AddSeries([new(0, 0), new(1, 1), new(2, 2)], PlotStyle.Cross);
		figure.AddComplexSeries([new(0, 0)], PlotStyle.Cross); // switching to complex axes clears the window
		Assert.Equal(1, figure.Current.PointCount);
	}

	[Fact]
	public void EachWindowHasItsOwnLimit()
	{
		var figure = new Figure(maxPointsPerWindow: 2);
		figure.Initialize(1, 2);
		figure.AddDot(0, 0);
		figure.AddDot(1, 1);
		figure.SelectAxes(1, 2);
		figure.AddDot(0, 0);
		figure.AddDot(1, 1);
		Assert.Throws<ArgumentException>(() => figure.AddDot(2, 2));
	}

	[Fact]
	public void TheLimitMustBePositive()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new Figure(maxPointsPerWindow: 0));
	}
}
