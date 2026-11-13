using System.Globalization;
using System.Security;
using System.Text;

namespace MLCalc.Plotting;

/// <summary>Renders a figure as a standalone SVG document, matching the desktop view.</summary>
public static class SvgWriter
{
	const double MarkerSize = 3.5, DotSize = 2.2;

	public static string Render(FigureSnapshot figure, double width = 800, double height = 600)
	{
		// Written so that NaN is rejected too (every comparison with NaN is false).
		if (!(width >= 50 && height >= 50 && width <= 16000 && height <= 16000))
			throw new ArgumentOutOfRangeException(nameof(width), "The image size must be between 50 and 16000.");

		var svg = new StringBuilder();
		svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" font-family=\"Segoe UI, Helvetica, Arial, sans-serif\">\n");
		svg.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{height}\" fill=\"white\"/>\n");
		bool showCurrent = figure.Rows * figure.Columns > 1;
		for (int r = 0; r < figure.Rows; r++)
			for (int c = 0; c < figure.Columns; c++)
			{
				bool current = showCurrent && r == figure.CurrentRow && c == figure.CurrentColumn;
				RenderAxes(svg, figure[r, c], PlotLayout.Cell(figure, r, c, width, height), current, r * figure.Columns + c);
			}
		svg.Append("</svg>\n");
		return svg.ToString();
	}

	public static void Save(FigureSnapshot figure, string path, double width = 800, double height = 600) =>
		File.WriteAllText(path, Render(figure, width, height));

	static void RenderAxes(StringBuilder svg, Axes axes, ScreenRect cell, bool isCurrent, int index)
	{
		var plot = PlotLayout.PlotArea(cell);
		if (plot.Width < 20 || plot.Height < 20)
			return;
		var t = new PlotTransform(axes, plot);
		var xTicks = AxisTicks.Compute(axes.XMin, axes.XMax, System.Math.Max(2, (int)(plot.Width / 90)));
		var yTicks = AxisTicks.Compute(axes.YMin, axes.YMax, System.Math.Max(2, (int)(plot.Height / 50)));

		if (axes.ShowGrid)
		{
			foreach (var x in xTicks)
				Line(svg, t.ToScreenX(x), plot.Top, t.ToScreenX(x), plot.Bottom, "#e2e2e2", 1, " stroke-dasharray=\"4 3\"");
			foreach (var y in yTicks)
				Line(svg, plot.Left, t.ToScreenY(y), plot.Right, t.ToScreenY(y), "#e2e2e2", 1, " stroke-dasharray=\"4 3\"");
		}
		if (axes.XMin < 0 && axes.XMax > 0)
			Line(svg, t.ToScreenX(0), plot.Top, t.ToScreenX(0), plot.Bottom, "#bbb", 1);
		if (axes.YMin < 0 && axes.YMax > 0)
			Line(svg, plot.Left, t.ToScreenY(0), plot.Right, t.ToScreenY(0), "#bbb", 1);

		string clip = $"clip{index}";
		svg.Append(F($"<clipPath id=\"{clip}\"><rect x=\"{plot.Left}\" y=\"{plot.Top}\" width=\"{plot.Width}\" height=\"{plot.Height}\"/></clipPath>\n"));
		svg.Append(F($"<g clip-path=\"url(#{clip})\">\n"));
		foreach (var item in axes.Items)
			RenderItem(svg, item, t, plot);
		svg.Append("</g>\n");

		svg.Append(F($"<rect x=\"{plot.Left}\" y=\"{plot.Top}\" width=\"{plot.Width}\" height=\"{plot.Height}\" fill=\"none\" stroke=\"{(isCurrent ? "#1f77b4" : "#999")}\" stroke-width=\"{(isCurrent ? 2 : 1)}\"/>\n"));

		foreach (var x in xTicks)
		{
			double sx = t.ToScreenX(x);
			Line(svg, sx, plot.Bottom, sx, plot.Bottom + 4, "#999", 1);
			Text(svg, PlotLayout.FormatTick(x), sx, plot.Bottom + 17, 11, "middle");
		}
		foreach (var y in yTicks)
		{
			double sy = t.ToScreenY(y);
			Line(svg, plot.Left - 4, sy, plot.Left, sy, "#999", 1);
			Text(svg, PlotLayout.FormatTick(y), plot.Left - 7, sy + 4, 11, "end");
		}

		bool complex = axes.Kind == AxesKind.Complex;
		string xTitle = axes.XTitle.Length > 0 ? axes.XTitle : complex ? "Re" : "";
		string yTitle = axes.YTitle.Length > 0 ? axes.YTitle : complex ? "Im" : "";
		double centerX = plot.Left + plot.Width / 2, centerY = plot.Top + plot.Height / 2;
		if (axes.Title.Length > 0)
			Text(svg, axes.Title, centerX, cell.Top + 19, 13, "middle", " font-weight=\"600\"");
		if (xTitle.Length > 0)
			Text(svg, xTitle, centerX, plot.Bottom + 36, 12, "middle");
		if (yTitle.Length > 0)
			Text(svg, yTitle, cell.Left + 16, centerY, 12, "middle", F($" transform=\"rotate(-90 {cell.Left + 16} {centerY})\""));
	}

	static void RenderItem(StringBuilder svg, PlotItem item, PlotTransform t, ScreenRect plot)
	{
		string color = "#" + Pens.ColorOf(item.Pen).ToString("x6", CultureInfo.InvariantCulture);
		switch (item)
		{
			case PointSeries { Style: PlotStyle.Line } series:
				Polyline(svg, PlotLayout.Segments(series.Points, t.Axes, breakAtJumps: false), t, plot, color);
				break;
			case FunctionSeries function:
				Polyline(svg, PlotLayout.Segments(function.Points, t.Axes, breakAtJumps: true), t, plot, color);
				break;
			case PointSeries { Style: PlotStyle.Dot } series:
				Dots(svg, series.Points, t, color);
				break;
			case PointSeries series:
				Markers(svg, series.Points, t, color, series.Style == PlotStyle.Star);
				break;
			case DotCloud cloud:
				Dots(svg, cloud.Points, t, color);
				break;
		}
	}

	static void Polyline(StringBuilder svg, IEnumerable<IReadOnlyList<PlotPoint>> segments, PlotTransform t, ScreenRect plot, string color)
	{
		var path = new StringBuilder();
		foreach (var segment in segments)
			for (int i = 0; i < segment.Count; i++)
			{
				var (x, y) = PlotLayout.Clamp(t.ToScreenX(segment[i].X), t.ToScreenY(segment[i].Y), plot);
				path.Append(F($"{(i == 0 ? "M" : "L")}{x:0.##} {y:0.##} "));
			}
		if (path.Length > 0)
			svg.Append(F($"<path d=\"{path.ToString().TrimEnd()}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.5\" stroke-linejoin=\"round\"/>\n"));
	}

	static void Markers(StringBuilder svg, IEnumerable<PlotPoint> points, PlotTransform t, string color, bool star)
	{
		var path = new StringBuilder();
		foreach (var (x, y) in Visible(points, t))
		{
			path.Append(F($"M{x - MarkerSize:0.##} {y:0.##}h{2 * MarkerSize}M{x:0.##} {y - MarkerSize:0.##}v{2 * MarkerSize}"));
			if (star)
			{
				double d = MarkerSize * 0.75;
				path.Append(F($"M{x - d:0.##} {y - d:0.##}l{2 * d:0.##} {2 * d:0.##}M{x - d:0.##} {y + d:0.##}l{2 * d:0.##} {-2 * d:0.##}"));
			}
		}
		if (path.Length > 0)
			svg.Append(F($"<path d=\"{path}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.5\"/>\n"));
	}

	static void Dots(StringBuilder svg, IEnumerable<PlotPoint> points, PlotTransform t, string color)
	{
		var path = new StringBuilder();
		foreach (var (x, y) in Visible(points, t).Distinct())
			path.Append(F($"M{x - DotSize / 2:0.##} {y - DotSize / 2:0.##}h{DotSize}v{DotSize}h{-DotSize}z"));
		if (path.Length > 0)
			svg.Append(F($"<path d=\"{path}\" fill=\"{color}\"/>\n"));
	}

	static IEnumerable<(double X, double Y)> Visible(IEnumerable<PlotPoint> points, PlotTransform t)
	{
		var s = t.Screen;
		foreach (var p in points)
		{
			if (!double.IsFinite(p.X) || !double.IsFinite(p.Y))
				continue;
			double x = t.ToScreenX(p.X), y = t.ToScreenY(p.Y);
			if (x >= s.Left - MarkerSize && x <= s.Right + MarkerSize && y >= s.Top - MarkerSize && y <= s.Bottom + MarkerSize)
				yield return (System.Math.Round(x, 2), System.Math.Round(y, 2));
		}
	}

	static void Line(StringBuilder svg, double x1, double y1, double x2, double y2, string color, double width, string extra = "") =>
		svg.Append(F($"<line x1=\"{x1:0.##}\" y1=\"{y1:0.##}\" x2=\"{x2:0.##}\" y2=\"{y2:0.##}\" stroke=\"{color}\" stroke-width=\"{width}\"{extra}/>\n"));

	static void Text(StringBuilder svg, string text, double x, double y, double size, string anchor, string extra = "") =>
		svg.Append(F($"<text x=\"{x:0.##}\" y=\"{y:0.##}\" font-size=\"{size}\" fill=\"#333\" text-anchor=\"{anchor}\"{extra}>{SecurityElement.Escape(text)}</text>\n"));

	static string F(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
