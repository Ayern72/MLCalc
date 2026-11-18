using MLCalc.Plotting;
using MLCalc.Web.Protocol;

namespace MLCalc.Web.Server;

/// <summary>
/// Turns figure snapshots into messages, sending only what changed since the last one: windows
/// that are the same object as before are left out; a window that only gained items, or points in
/// its last dot cloud, is sent as an <see cref="WindowAppend"/>; any other change sends the window
/// whole. Not thread-safe: the session uses it under its send lock.
/// </summary>
sealed class FigureEncoder
{
	FigureSnapshot? sent;

	/// <summary>Every window, whole; later changes are relative to this.</summary>
	public FigureMessage Full(FigureSnapshot now)
	{
		sent = now;
		return Message(now, [.. now.Axes.Select((axes, index) => new WindowUpdate(index, Replace: ToWindow(axes)))]);
	}

	/// <summary>What changed since the last message; null when nothing did.</summary>
	public FigureMessage? Changes(FigureSnapshot now)
	{
		if (sent is null || sent.Rows != now.Rows || sent.Columns != now.Columns)
			return Full(now);
		if (sent.Version == now.Version)
			return null;
		var before = sent;
		sent = now;
		var updates = new List<WindowUpdate>();
		for (int index = 0; index < now.Axes.Length; index++)
			if (Diff(before.Axes[index], now.Axes[index], index) is { } update)
				updates.Add(update);
		return Message(now, updates);
	}

	static FigureMessage Message(FigureSnapshot figure, IReadOnlyList<WindowUpdate> windows) =>
		new(figure.Version, figure.Rows, figure.Columns, figure.CurrentRow, figure.CurrentColumn, windows);

	/// <summary>
	/// The client set the camera of window <paramref name="index"/> itself (<c>SetCamera</c>): from now
	/// on it is counted as sent, so the window is not sent back for it.
	/// </summary>
	public void ClientHasCamera(int index, Camera camera)
	{
		if (sent is null || index < 0 || index >= sent.Axes.Length)
			return;
		sent = sent with { Axes = sent.Axes.SetItem(index, sent.Axes[index] with { Camera = camera }) };
	}

	static WindowUpdate? Diff(Axes before, Axes now, int index)
	{
		if (ReferenceEquals(before, now))
			return null;
		var append = Appended(before, now);
		if (append is { Items.Count: 0, LastDots: null } && append.Pen == before.Pen)
			return null;   // an equal window, e.g. after the client's own camera change
		return append is not null
			? new WindowUpdate(index, Append: append)
			: new WindowUpdate(index, Replace: ToWindow(now));
	}

	/// <summary>The additions when <paramref name="now"/> is <paramref name="before"/> with more items or dots; otherwise null.</summary>
	static WindowAppend? Appended(Axes before, Axes now)
	{
		// Everything but the items and the pen must be the same: compare with those made equal.
		if (before with { Items = now.Items, PointCount = now.PointCount, Pen = now.Pen } != now)
			return null;
		var old = before.Items;
		var items = now.Items;
		if (items.Length < old.Length)
			return null;
		for (int i = 0; i < old.Length - 1; i++)
			if (!ReferenceEquals(old[i], items[i]))
				return null;

		PointsData? lastDots = null;
		if (old.Length > 0 && !ReferenceEquals(old[^1], items[old.Length - 1]))
		{
			// Only a dot cloud changes in place, by growing: dot and dot3d add to the last one.
			switch (old[^1], items[old.Length - 1])
			{
				case (DotCloud a, DotCloud b) when a.Pen == b.Pen && b.Points.Count >= a.Points.Count:
					lastDots = Points(b.Points.GetRange(a.Points.Count, b.Points.Count - a.Points.Count));
					break;
				case (DotCloud3D a, DotCloud3D b) when a.Pen == b.Pen && b.Points.Count >= a.Points.Count:
					lastDots = Points(b.Points.GetRange(a.Points.Count, b.Points.Count - a.Points.Count));
					break;
				default:
					return null;
			}
		}
		return new WindowAppend(now.Pen, [.. items.Skip(old.Length).Select(ToItem)], lastDots);
	}

	// ───────────── Plot model → protocol ─────────────

	public static PlotWindow ToWindow(Axes axes) => new(
		axes.Kind switch
		{
			AxesKind.Complex => WindowKind.Complex,
			AxesKind.Space => WindowKind.Space,
			_ => WindowKind.Cartesian,
		},
		axes.XMin, axes.XMax, axes.YMin, axes.YMax, axes.ZMin, axes.ZMax,
		axes.Title, axes.XTitle, axes.YTitle, axes.ZTitle,
		axes.ShowGrid, axes.Pen, ToCamera(axes.Camera), [.. axes.Items.Select(ToItem)]);

	/// <summary>A camera from the client; throws <see cref="ArgumentException"/> for points that are not [x, y, z].</summary>
	public static Camera ToCamera(CameraData camera) => new(
		camera.Azimuth, camera.Elevation, camera.Distance, Point(camera.Position), Point(camera.Target));

	static Point3? Point(double[]? p) => p switch
	{
		null => null,
		[var x, var y, var z] => new Point3(x, y, z),
		_ => throw new ArgumentException("A camera point must have three coordinates."),
	};

	static CameraData ToCamera(Camera camera) => new(
		camera.Azimuth, camera.Elevation, camera.Distance,
		camera.Position is { } p ? [p.X, p.Y, p.Z] : null,
		camera.Target is { } t ? [t.X, t.Y, t.Z] : null);

	public static PlotItemData ToItem(PlotItem item) => item switch
	{
		PointSeries s => With(Points(s.Points), ItemType.Series, s.Pen, style: s.Style),
		FunctionSeries f => With(Points(f.Points), ItemType.Function, f.Pen, label: f.Label),
		DotCloud d => With(Points(d.Points), ItemType.Dots, d.Pen),
		PointSeries3D s => With(Points(s.Points), ItemType.Series3D, s.Pen, style: s.Style),
		DotCloud3D d => With(Points(d.Points), ItemType.Dots3D, d.Pen),
		Surface s => With(Points(s.Points), ItemType.Surface, s.Pen, label: s.Label) with { XCount = s.XCount, YCount = s.YCount },
		_ => throw new NotSupportedException($"Unknown plot item {item.GetType().Name}."),
	};

	static PlotItemData With(PointsData points, ItemType type, int pen, PlotStyle? style = null, string? label = null) =>
		new(type, pen, points.X, points.Y, points.Z, style is { } s ? ToStyle(s) : null, label);

	static SeriesStyle ToStyle(PlotStyle style) => style switch
	{
		PlotStyle.Cross => SeriesStyle.Cross,
		PlotStyle.Star => SeriesStyle.Star,
		PlotStyle.Dot => SeriesStyle.Dot,
		_ => SeriesStyle.Line,
	};

	static PointsData Points(IEnumerable<PlotPoint> points)
	{
		var list = points as IReadOnlyCollection<PlotPoint> ?? [.. points];
		var x = new float?[list.Count];
		var y = new float?[list.Count];
		int i = 0;
		foreach (var p in list)
		{
			x[i] = Coordinate(p.X);
			y[i] = Coordinate(p.Y);
			i++;
		}
		return new PointsData(x, y);
	}

	static PointsData Points(IEnumerable<Point3> points)
	{
		var list = points as IReadOnlyCollection<Point3> ?? [.. points];
		var x = new float?[list.Count];
		var y = new float?[list.Count];
		var z = new float?[list.Count];
		int i = 0;
		foreach (var p in list)
		{
			x[i] = Coordinate(p.X);
			y[i] = Coordinate(p.Y);
			z[i] = Coordinate(p.Z);
			i++;
		}
		return new PointsData(x, y, z);
	}

	/// <summary>
	/// A coordinate in single precision, which is plenty on a screen and halves the message; null for
	/// a missing point (NaN, infinite, or beyond what a float holds).
	/// </summary>
	static float? Coordinate(double value)
	{
		float single = (float)value;
		return float.IsFinite(single) ? single : null;
	}
}
