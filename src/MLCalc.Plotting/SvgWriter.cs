using System.Globalization;
using System.Security;
using System.Text;

namespace MLCalc.Plotting;

/// <summary>Renders a figure as a standalone SVG document, matching the desktop view.</summary>
public static class SvgWriter
{
	const double MarkerSize = 3.5, DotSize = 2.2;

	/// <summary>The figure as an SVG document of the given size in pixels.</summary>
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
				RenderAxes(svg, figure[r, c], PlotLayout.Cell(figure, r, c, width, height), current, F($"clip{r * figure.Columns + c}"), preview: false);
			}
		svg.Append("</svg>\n");
		return svg.ToString();
	}

	/// <summary>
	/// One plot window as an SVG document of the given size in pixels, e.g. for a web page that
	/// shows each window as its own element. <paramref name="id"/> starts the ids inside the SVG,
	/// which must be unique on a page. <paramref name="preview"/> draws large 3D items thinned out,
	/// for redrawing quickly while the camera moves (see <see cref="Scene3D.Preview(Axes)"/>).
	/// </summary>
	public static string RenderWindow(Axes axes, double width, double height, string id = "w", bool isCurrent = false, bool preview = false)
	{
		if (!(width >= 1 && height >= 1 && width <= 16000 && height <= 16000))
			throw new ArgumentOutOfRangeException(nameof(width), "The image size must be between 1 and 16000.");
		var svg = new StringBuilder();
		svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" font-family=\"Segoe UI, Helvetica, Arial, sans-serif\">\n");
		svg.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{height}\" fill=\"white\"/>\n");
		RenderAxes(svg, axes, new ScreenRect(0, 0, width, height), isCurrent, id + "-clip", preview);
		svg.Append("</svg>\n");
		return svg.ToString();
	}

	/// <summary>Writes the figure to an SVG file.</summary>
	public static void Save(FigureSnapshot figure, string path, double width = 800, double height = 600) =>
		File.WriteAllText(path, Render(figure, width, height));

	static string ColorOf(int pen) => "#" + Pens.ColorOf(pen).ToString("x6", CultureInfo.InvariantCulture);

	/// <summary>A 3D window: the primitives from <see cref="Scene3D"/>, in order.</summary>
	static void RenderSpace(StringBuilder svg, Axes axes, ScreenRect cell, bool isCurrent, string clip, bool preview)
	{
		var bounds = cell;
		svg.Append(F($"<clipPath id=\"{clip}\"><rect x=\"{bounds.Left}\" y=\"{bounds.Top}\" width=\"{bounds.Width}\" height=\"{bounds.Height}\"/></clipPath>\n"));
		svg.Append(F($"<g clip-path=\"url(#{clip})\">\n"));
		foreach (var primitive in Scene3D.Build(axes, cell, preview))
			switch (primitive)
			{
				case ScenePolyline line:
				{
					var (color, width, extra) = line.Stroke switch
					{
						SceneStroke.Grid => ("#e2e2e2", 1.0, " stroke-dasharray=\"4 3\""),
						SceneStroke.Frame or SceneStroke.Tick => ("#999", 1.0, ""),
						_ => (ColorOf(line.Pen), 1.5, " stroke-linejoin=\"round\""),
					};
					svg.Append(F($"<path d=\"{PathData(line.Points, close: false)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{width}\"{extra}/>\n"));
					break;
				}
				case ScenePolygon polygon:
					svg.Append(F($"<path d=\"{PathData(polygon.Points, close: true)}\" fill=\"white\" stroke=\"{ColorOf(polygon.Pen)}\" stroke-width=\"1\" stroke-linejoin=\"round\"/>\n"));
					break;
				case SceneMarkers markers:
					if (markers.Style == PlotStyle.Dot)
						DotPath(svg, markers.Points.Select(p => (Math.Round(p.X, 2), Math.Round(p.Y, 2))), ColorOf(markers.Pen));
					else
						MarkerPath(svg, markers.Points.Select(p => (p.X, p.Y)), ColorOf(markers.Pen), markers.Style == PlotStyle.Star);
					break;
				case SceneText text:
					Text(svg, text.Text, text.At.X, text.At.Y + text.Size * 0.35, text.Size,
						text.Align switch { TextAlign.Start => "start", TextAlign.End => "end", _ => "middle" },
						text.Bold ? " font-weight=\"600\"" : "");
					break;
			}
		svg.Append("</g>\n");
		if (isCurrent)
			svg.Append(F($"<rect x=\"{cell.Left + 3}\" y=\"{cell.Top + 3}\" width=\"{cell.Width - 6}\" height=\"{cell.Height - 6}\" fill=\"none\" stroke=\"#1f77b4\" stroke-width=\"2\"/>\n"));
	}

	static string PathData(IReadOnlyList<ScreenPoint> points, bool close)
	{
		var path = new StringBuilder();
		for (int i = 0; i < points.Count; i++)
			path.Append(F($"{(i == 0 ? "M" : "L")}{points[i].X:0.##} {points[i].Y:0.##}"));
		if (close)
			path.Append('Z');
		return path.ToString();
	}

	static void RenderAxes(StringBuilder svg, Axes axes, ScreenRect cell, bool isCurrent, string clip, bool preview)
	{
		if (axes.Kind == AxesKind.Space)
		{
			RenderSpace(svg, axes, cell, isCurrent, clip, preview);
			return;
		}
		var plot = PlotLayout.PlotArea(cell);
		if (plot.Width < 20 || plot.Height < 20)
			return;
		var t = new PlotTransform(axes, plot);
		var xTicks = AxisTicks.Compute(axes.XMin, axes.XMax, Math.Max(2, (int)(plot.Width / 90)));
		var yTicks = AxisTicks.Compute(axes.YMin, axes.YMax, Math.Max(2, (int)(plot.Height / 50)));

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

	static void Markers(StringBuilder svg, IEnumerable<PlotPoint> points, PlotTransform t, string color, bool star) =>
		MarkerPath(svg, Visible(points, t), color, star);

	static void MarkerPath(StringBuilder svg, IEnumerable<(double X, double Y)> points, string color, bool star)
	{
		var path = new StringBuilder();
		foreach (var (x, y) in points)
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

	static void Dots(StringBuilder svg, IEnumerable<PlotPoint> points, PlotTransform t, string color) =>
		DotPath(svg, Visible(points, t), color);

	static void DotPath(StringBuilder svg, IEnumerable<(double X, double Y)> points, string color)
	{
		var path = new StringBuilder();
		foreach (var (x, y) in points.Distinct())
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
				yield return (Math.Round(x, 2), Math.Round(y, 2));
		}
	}

	static void Line(StringBuilder svg, double x1, double y1, double x2, double y2, string color, double width, string extra = "") =>
		svg.Append(F($"<line x1=\"{x1:0.##}\" y1=\"{y1:0.##}\" x2=\"{x2:0.##}\" y2=\"{y2:0.##}\" stroke=\"{color}\" stroke-width=\"{width}\"{extra}/>\n"));

	static void Text(StringBuilder svg, string text, double x, double y, double size, string anchor, string extra = "") =>
		svg.Append(F($"<text x=\"{x:0.##}\" y=\"{y:0.##}\" font-size=\"{size}\" fill=\"#333\" text-anchor=\"{anchor}\"{extra}>{SecurityElement.Escape(text)}</text>\n"));

	static string F(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
