namespace MLCalc.Plotting.Tests;

public class LayoutAndSvgTests
{
	static readonly Axes Unit = Axes.Default with { YMin = -1, YMax = 1 };

	[Fact]
	public void SegmentsBreakAtNonFinitePoints()
	{
		var segments = PlotLayout.Segments([new(0, 0), new(1, 0), new(2, double.NaN), new(3, 0)], Unit, breakAtJumps: false).ToList();
		Assert.Equal([2, 1], segments.Select(s => s.Count));
	}

	[Fact]
	public void SegmentsBreakAtPolesOnlyWhenAsked()
	{
		PlotPoint[] pole = [new(0, 0.5), new(1, 50), new(2, -50), new(3, -0.5)];
		Assert.Single(PlotLayout.Segments(pole, Unit, breakAtJumps: false));
		Assert.Equal(2, PlotLayout.Segments(pole, Unit, breakAtJumps: true).Count());
	}

	[Fact]
	public void PlotAreaLeavesMarginsInsideCell()
	{
		var area = PlotLayout.PlotArea(new ScreenRect(0, 0, 800, 600));
		Assert.Equal(new ScreenRect(62, 28, 720, 528), area);
	}

	[Fact]
	public void SvgContainsOneElementPerItemTitlesAndEscapedText()
	{
		var figure = new Figure();
		figure.SetTitle("a < b & c");
		figure.AddSeries([new(0, 0), new(5, 5)], PlotStyle.Line);
		figure.AddSeries([new(1, 1)], PlotStyle.Cross);
		figure.AddDot(2, 2);
		string svg = SvgWriter.Render(figure.Snapshot());
		Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\"", svg);
		Assert.EndsWith("</svg>\n", svg);
		Assert.Equal(3, svg.Split("<path").Length - 1);
		Assert.Contains("a &lt; b &amp; c", svg);
		var document = System.Xml.Linq.XDocument.Parse(svg);
		Assert.Equal("svg", document.Root!.Name.LocalName);
	}

	[Fact]
	public void SvgDrawsEveryWindowOfAGrid()
	{
		var figure = new Figure();
		figure.Initialize(2, 3);
		string svg = SvgWriter.Render(figure.Snapshot(), 900, 600);
		Assert.Equal(6, svg.Split("<clipPath").Length - 1);
	}

	[Fact]
	public void SvgUsesInvariantNumbers()
	{
		var culture = Thread.CurrentThread.CurrentCulture;
		try
		{
			Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("sv-SE");
			var figure = new Figure();
			figure.SetXRange(0, 0.5);
			string svg = SvgWriter.Render(figure.Snapshot(), 801.5, 600);
			Assert.Contains("801.5", svg);
			Assert.DoesNotContain("0,1<", svg);
		}
		finally
		{
			Thread.CurrentThread.CurrentCulture = culture;
		}
	}
}
