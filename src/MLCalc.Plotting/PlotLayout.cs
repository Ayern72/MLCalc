namespace MLCalc.Plotting;

/// <summary>Layout and geometry shared by all renderers, so every front-end draws the same picture.</summary>
public static class PlotLayout
{
	public const double MarginLeft = 62, MarginRight = 18, MarginTop = 28, MarginBottom = 44;

	/// <summary>The rectangle of one grid cell in a figure drawn at the given size.</summary>
	public static ScreenRect Cell(FigureSnapshot figure, int row, int column, double width, double height)
	{
		double cellWidth = width / figure.Columns, cellHeight = height / figure.Rows;
		return new ScreenRect(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
	}

	/// <summary>The data area inside a cell, leaving room for tick labels and titles.</summary>
	public static ScreenRect PlotArea(ScreenRect cell) => new(
		cell.Left + MarginLeft,
		cell.Top + MarginTop,
		System.Math.Max(1, cell.Width - MarginLeft - MarginRight),
		System.Math.Max(1, cell.Height - MarginTop - MarginBottom));

	/// <summary>
	/// Splits a curve into connected runs: breaks at non-finite points and, when
	/// <paramref name="breakAtJumps"/> is set, where the curve jumps from beyond one y limit to
	/// beyond the other (a pole such as tan at π/2).
	/// </summary>
	public static IEnumerable<IReadOnlyList<PlotPoint>> Segments(IEnumerable<PlotPoint> points, Axes axes, bool breakAtJumps)
	{
		var current = new List<PlotPoint>();
		int previousSide = 0;
		foreach (var p in points)
		{
			if (!double.IsFinite(p.X) || !double.IsFinite(p.Y))
			{
				if (current.Count > 0)
					yield return current;
				current = [];
				continue;
			}
			int side = p.Y > axes.YMax ? 1 : p.Y < axes.YMin ? -1 : 0;
			if (breakAtJumps && current.Count > 0 && side != 0 && side == -previousSide)
			{
				yield return current;
				current = [];
			}
			previousSide = side;
			current.Add(p);
		}
		if (current.Count > 0)
			yield return current;
	}

	/// <summary>Keeps far-away screen points at a finite distance; renderers misbehave with huge coordinates.</summary>
	public static (double X, double Y) Clamp(double x, double y, ScreenRect plot)
	{
		double margin = 10 * System.Math.Max(plot.Width, plot.Height);
		return (System.Math.Clamp(x, plot.Left - margin, plot.Right + margin), System.Math.Clamp(y, plot.Top - margin, plot.Bottom + margin));
	}

	/// <summary>Formats a tick value compactly (invariant culture).</summary>
	public static string FormatTick(double value) => value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
}
