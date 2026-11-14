namespace MLCalc.Plotting.Tests;

/// <summary>
/// Axis ranges whose span overflows or underflows used to make the tick calculation loop until the
/// computer ran out of memory (found in the code review).
/// </summary>
public class RangeSafetyTests
{
	[Theory]
	[InlineData(-1e308, 1e308)]
	[InlineData(0, 1e-320)]
	public void RangesWithUnusableSpansAreRefused(double min, double max)
	{
		var figure = new Figure();
		var error = Assert.Throws<ArgumentException>(() => figure.SetXRange(min, max));
		Assert.Contains("too large or too small", error.Message);
		Assert.Throws<ArgumentException>(() => figure.SetYRange(min, max));
	}

	[Fact]
	public void LargeButFiniteSpansAreAllowed()
	{
		var figure = new Figure();
		figure.SetXRange(-1e300, 1e300);
		Assert.NotEmpty(AxisTicks.Compute(figure.Current.XMin, figure.Current.XMax));
	}

	[Theory]
	[InlineData(-1e308, 1e308)]
	[InlineData(double.MinValue, double.MaxValue)]
	public void TicksForOverflowingSpansAreEmpty(double min, double max)
	{
		Assert.Empty(AxisTicks.Compute(min, max));
	}

	[Theory]
	[InlineData(0, 1e-320)]
	[InlineData(1e15, 1e15 + 0.25)]
	public void TicksForTinySpansAreBounded(double min, double max)
	{
		// Few ticks, each different from the one before (not 1001 copies of the same label).
		var ticks = AxisTicks.Compute(min, max);
		Assert.InRange(ticks.Count, 0, 20);
		for (int i = 1; i < ticks.Count; i++)
			Assert.True(ticks[i] > ticks[i - 1], $"tick {i} = {ticks[i]} is not above {ticks[i - 1]}");
	}

	[Theory]
	[InlineData(double.NaN, 600)]
	[InlineData(800, double.NaN)]
	[InlineData(40, 600)]
	[InlineData(800, 20000)]
	public void SvgRejectsInvalidSizes(double width, double height)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => SvgWriter.Render(new Figure().Snapshot(), width, height));
	}
}
