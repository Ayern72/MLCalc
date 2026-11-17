using System.Collections.Immutable;

namespace MLCalc.Plotting;

/// <summary>What a line in a 3D scene is: part of the box, a grid line, a tick, or data in a pen's colour.</summary>
public enum SceneStroke
{
	/// <summary>The edges of the box.</summary>
	Frame,
	/// <summary>Grid lines on the back walls.</summary>
	Grid,
	/// <summary>Tick marks.</summary>
	Tick,
	/// <summary>A plotted item, in its pen's colour.</summary>
	Pen,
}

/// <summary>Horizontal alignment of a text label relative to its anchor point.</summary>
public enum TextAlign
{
	/// <summary>The text starts at the anchor.</summary>
	Start,
	/// <summary>The text is centred on the anchor.</summary>
	Middle,
	/// <summary>The text ends at the anchor.</summary>
	End,
}

/// <summary>Something to draw in screen coordinates; renderers draw the primitives in order.</summary>
public abstract record ScenePrimitive;

/// <summary>A connected line. <c>Pen</c> is used when <c>Stroke</c> is <see cref="SceneStroke.Pen"/>.</summary>
public sealed record ScenePolyline(SceneStroke Stroke, int Pen, ImmutableArray<ScreenPoint> Points) : ScenePrimitive;

/// <summary>A piece of a surface: filled with the background colour and outlined in the pen's colour, hiding what is behind it.</summary>
public sealed record ScenePolygon(int Pen, ImmutableArray<ScreenPoint> Points) : ScenePrimitive;

/// <summary>Markers (crosses, stars) or dots at points, in the pen's colour.</summary>
public sealed record SceneMarkers(int Pen, PlotStyle Style, ImmutableArray<ScreenPoint> Points) : ScenePrimitive;

/// <summary>A label, vertically centred on <c>At</c>.</summary>
public sealed record SceneText(string Text, ScreenPoint At, TextAlign Align, double Size, bool Bold = false) : ScenePrimitive;

/// <summary>
/// Turns a 3D window into 2D primitives, so that every renderer (desktop and SVG) draws the same
/// picture. The three sides of the box facing away from the camera are drawn (with grid lines when
/// the grid is on), then surfaces from back to front, so nearer parts hide farther ones, then curves
/// and points, then the ticks and labels.
/// </summary>
public static class Scene3D
{
	/// <summary>Room around the box for tick labels and axis titles.</summary>
	const double Margin = 50, TitleRoom = 30;

	/// <summary>
	/// An edge shorter than this on the screen, or at less than about 25° to the line of sight
	/// (cosine above <see cref="EndOnCosine"/>), is seen nearly end-on and gets no tick labels.
	/// </summary>
	const double MinLabelledEdge = 40, EndOnCosine = 0.9;

	/// <summary>The rectangle the box is fitted into, inside a grid cell.</summary>
	public static ScreenRect ViewArea(ScreenRect cell) => new(
		cell.Left + Margin,
		cell.Top + TitleRoom + Margin / 2,
		Math.Max(1, cell.Width - 2 * Margin),
		Math.Max(1, cell.Height - TitleRoom - 1.5 * Margin));

	/// <summary>In a preview, surfaces have at most this many grid lines each way, and curves and clouds at most <see cref="PreviewPoints"/> points.</summary>
	public const int PreviewGridLines = 40, PreviewPoints = 20_000;

	/// <summary>
	/// The primitives for a 3D window in the rectangle <paramref name="cell"/>. A
	/// <paramref name="preview"/> draws large surfaces, curves and clouds from a part of their points
	/// (see <see cref="Preview(Axes)"/>), quickly enough to follow the mouse while the camera is turned.
	/// </summary>
	public static ImmutableArray<ScenePrimitive> Build(Axes axes, ScreenRect cell, bool preview = false)
	{
		if (preview)
			axes = Preview(axes);
		var scene = ImmutableArray.CreateBuilder<ScenePrimitive>();
		var area = ViewArea(cell);
		if (area.Width >= 20 && area.Height >= 20)
			AddBox(scene, axes, area);
		// Last, so that a camera close to a surface does not hide the title.
		if (axes.Title.Length > 0)
			scene.Add(new SceneText(axes.Title, new ScreenPoint(cell.Left + cell.Width / 2, cell.Top + 16), TextAlign.Middle, 13, Bold: true));
		return scene.ToImmutable();
	}

	static void AddBox(ImmutableArray<ScenePrimitive>.Builder scene, Axes axes, ScreenRect area)
	{
		var view = new Projection3D(axes, area);
		var eye = view.Eye;
		// The sides of the box facing away from the camera, one per axis: −1 or +1.
		int backX = eye.X >= 0 ? -1 : 1, backY = eye.Y >= 0 ? -1 : 1, backZ = eye.Z >= 0 ? -1 : 1;

		// The edges the ticks go on: for x and y the bottom edges nearest the camera (the top ones
		// when looking from below), for z the leftmost vertical edge.
		var xEdge = (new Vec3(-1, -backY, backZ), new Vec3(1, -backY, backZ));
		var yEdge = (new Vec3(-backX, -1, backZ), new Vec3(-backX, 1, backZ));
		var (zx, zy) = ZTickEdge(view);
		var zEdge = (new Vec3(zx, zy, -1), new Vec3(zx, zy, 1));
		var xTicks = Ticks(axes.XMin, axes.XMax, view, xEdge);
		var yTicks = Ticks(axes.YMin, axes.YMax, view, yEdge);
		var zTicks = Ticks(axes.ZMin, axes.ZMax, view, zEdge);
		double Cx(double x) => 2 * (x - axes.XMin) / (axes.XMax - axes.XMin) - 1;
		double Cy(double y) => 2 * (y - axes.YMin) / (axes.YMax - axes.YMin) - 1;
		double Cz(double z) => 2 * (z - axes.ZMin) / (axes.ZMax - axes.ZMin) - 1;

		if (axes.ShowGrid)
		{
			foreach (var y in yTicks)
				Line(scene, view, SceneStroke.Grid, new Vec3(backX, Cy(y), -1), new Vec3(backX, Cy(y), 1));
			foreach (var z in zTicks)
			{
				Line(scene, view, SceneStroke.Grid, new Vec3(backX, -1, Cz(z)), new Vec3(backX, 1, Cz(z)));
				Line(scene, view, SceneStroke.Grid, new Vec3(-1, backY, Cz(z)), new Vec3(1, backY, Cz(z)));
			}
			foreach (var x in xTicks)
			{
				Line(scene, view, SceneStroke.Grid, new Vec3(Cx(x), backY, -1), new Vec3(Cx(x), backY, 1));
				Line(scene, view, SceneStroke.Grid, new Vec3(Cx(x), -1, backZ), new Vec3(Cx(x), 1, backZ));
			}
			foreach (var y in yTicks)
				Line(scene, view, SceneStroke.Grid, new Vec3(-1, Cy(y), backZ), new Vec3(1, Cy(y), backZ));
		}

		// The edges of the three back sides (nine edges: each pair of sides shares one).
		var edges = new HashSet<(Vec3, Vec3)>();
		foreach (var (axis, side) in new[] { (0, backX), (1, backY), (2, backZ) })
			for (int k = 0; k < 4; k++)
			{
				var corners = SideCorners(axis, side);
				var a = corners[k];
				var b = corners[(k + 1) % 4];
				if (edges.Add((a, b)) && edges.Add((b, a)))
					Line(scene, view, SceneStroke.Frame, a, b);
			}

		AddSurfaces(scene, view, axes);
		foreach (var item in axes.Items)
			switch (item)
			{
				case PointSeries3D { Style: PlotStyle.Line } series:
					AddCurve(scene, view, series.Pen, series.Points);
					break;
				case PointSeries3D series:
					AddMarkers(scene, view, series.Pen, series.Style, series.Points);
					break;
				case DotCloud3D cloud:
					AddMarkers(scene, view, cloud.Pen, PlotStyle.Dot, cloud.Points);
					break;
			}

		// An axis seen (nearly) end-on, such as z from straight above, would have its labels on top
		// of each other.
		if (!IsEndOn(view, xEdge))
			AddAxisLabels(scene, view, xTicks, x => new Vec3(Cx(x), -backY, backZ), new Vec3(0, -backY, backZ), Title(axes.XTitle, "x"));
		if (!IsEndOn(view, yEdge))
			AddAxisLabels(scene, view, yTicks, y => new Vec3(-backX, Cy(y), backZ), new Vec3(-backX, 0, backZ), Title(axes.YTitle, "y"));
		if (!IsEndOn(view, zEdge))
			AddAxisLabels(scene, view, zTicks, z => new Vec3(zx, zy, Cz(z)), new Vec3(zx, zy, 0), Title(axes.ZTitle, "z"));
	}

	static string Title(string title, string fallback) => title.Length > 0 ? title : fallback;

	/// <summary>Tick values for an axis, about one per 50 pixels of the edge they are drawn on.</summary>
	static IReadOnlyList<double> Ticks(double min, double max, Projection3D view, (Vec3 From, Vec3 To) edge) =>
		AxisTicks.Compute(min, max, Math.Clamp((int)(EdgeLength(view, edge) / 50), 3, 8));

	static bool IsEndOn(Projection3D view, (Vec3 From, Vec3 To) edge)
	{
		if (EdgeLength(view, edge) < MinLabelledEdge)
			return true;
		var sight = ((edge.From + edge.To) * 0.5 - view.Eye).Normalized;
		var direction = (edge.To - edge.From).Normalized;
		return Math.Abs(sight.Dot(direction)) > EndOnCosine;
	}

	/// <summary>The length of an edge of the box on the screen, or 0 when it is (partly) behind the camera.</summary>
	static double EdgeLength(Projection3D view, (Vec3 From, Vec3 To) edge)
	{
		if (view.Depth(edge.From) <= 0 || view.Depth(edge.To) <= 0)
			return 0;
		var a = view.ToScreen(edge.From);
		var b = view.ToScreen(edge.To);
		return Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
	}

	/// <summary>The vertical edge of the box that the z ticks go on: the leftmost on the screen.</summary>
	static (int X, int Y) ZTickEdge(Projection3D view)
	{
		(int X, int Y) best = (-1, -1);
		double left = double.PositiveInfinity;
		foreach (var (x, y) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
		{
			var middle = new Vec3(x, y, 0);
			if (view.Depth(middle) <= 0)
				continue;
			double screenX = view.ToScreen(middle).X;
			if (screenX < left)
			{
				left = screenX;
				best = (x, y);
			}
		}
		return best;
	}

	/// <summary>The corners, in order around the side, of the side of the box at <paramref name="side"/> (−1 or +1) on <paramref name="axis"/>.</summary>
	static Vec3[] SideCorners(int axis, int side)
	{
		(double, double)[] square = [(-1, -1), (1, -1), (1, 1), (-1, 1)];
		return square.Select(s => axis switch
		{
			0 => new Vec3(side, s.Item1, s.Item2),
			1 => new Vec3(s.Item1, side, s.Item2),
			_ => new Vec3(s.Item1, s.Item2, side),
		}).ToArray();
	}

	static void Line(ImmutableArray<ScenePrimitive>.Builder scene, Projection3D view, SceneStroke stroke, Vec3 a, Vec3 b, int pen = 0)
	{
		if (view.ClipSegment(ref a, ref b, out _, out _))
			scene.Add(new ScenePolyline(stroke, pen, [view.ToScreen(a), view.ToScreen(b)]));
	}

	/// <summary>
	/// The window with large items thinned out for a quick preview: a surface keeps at most
	/// <see cref="PreviewGridLines"/> of its grid lines each way (always the first and the last), a
	/// curve or cloud at most about <see cref="PreviewPoints"/> of its points. A curve keeps its
	/// non-finite points, so that its gaps stay gaps.
	/// </summary>
	public static Axes Preview(Axes axes) => axes with { Items = [.. axes.Items.Select(PreviewItem)] };

	static PlotItem PreviewItem(PlotItem item)
	{
		switch (item)
		{
			case Surface s when s.XCount > PreviewGridLines || s.YCount > PreviewGridLines:
			{
				var xs = Lines(s.XCount, PreviewGridLines);
				var ys = Lines(s.YCount, PreviewGridLines);
				var points = ImmutableArray.CreateBuilder<Point3>(xs.Count * ys.Count);
				foreach (int j in ys)
					foreach (int i in xs)
						points.Add(s[i, j]);
				return s with { XCount = xs.Count, YCount = ys.Count, Points = points.MoveToImmutable() };
			}
			case PointSeries3D s when s.Points.Length > PreviewPoints:
			{
				int step = Step(s.Points.Length);
				var points = s.Points;
				return s with { Points = [.. points.Where((p, k) => k % step == 0 || k == points.Length - 1 || !p.IsFinite)] };
			}
			case DotCloud3D c when c.Points.Count > PreviewPoints:
			{
				int step = Step(c.Points.Count);
				return c with { Points = [.. c.Points.Where((_, k) => k % step == 0)] };
			}
			default:
				return item;
		}
	}

	static int Step(int count) => (count + PreviewPoints - 1) / PreviewPoints;

	/// <summary>At most <paramref name="max"/> evenly spread indices from 0 to <paramref name="count"/> − 1, both included.</summary>
	static List<int> Lines(int count, int max)
	{
		if (count <= max)
			return [.. Enumerable.Range(0, count)];
		int step = (count - 1 + max - 2) / (max - 1);
		var lines = new List<int>();
		for (int k = 0; k < count - 1; k += step)
			lines.Add(k);
		lines.Add(count - 1);
		return lines;
	}

	/// <summary>Every cell of every surface as a polygon, sorted so that farther ones are drawn first.</summary>
	static void AddSurfaces(ImmutableArray<ScenePrimitive>.Builder scene, Projection3D view, Axes axes)
	{
		var pieces = new List<(double Depth, int Order, ScenePolygon Polygon)>();
		Span<int> corners = stackalloc int[4];
		foreach (var surface in axes.Items.OfType<Surface>())
		{
			// Each grid point is shared by up to four cells: convert it once.
			int n = surface.Points.Length;
			var cube = new Vec3[n];
			var depth = new double[n];
			var visible = new bool[n];
			var screen = new ScreenPoint[n];
			for (int k = 0; k < n; k++)
			{
				var p = surface.Points[k];
				if (!p.IsFinite)
					continue;
				cube[k] = view.Cube(p);
				visible[k] = view.IsVisible(cube[k]);
				if (visible[k])
				{
					depth[k] = view.Depth(cube[k]);
					screen[k] = view.ToScreen(cube[k]);
				}
			}
			for (int j = 0; j + 1 < surface.YCount; j++)
				for (int i = 0; i + 1 < surface.XCount; i++)
				{
					corners[0] = j * surface.XCount + i;
					corners[1] = corners[0] + 1;
					corners[2] = corners[1] + surface.XCount;
					corners[3] = corners[0] + surface.XCount;
					bool finite = true, inside = true;
					foreach (int k in corners)
					{
						finite &= surface.Points[k].IsFinite;
						inside &= visible[k];
					}
					if (!finite)
						continue;
					if (inside)
					{
						// The usual case: nothing to cut.
						double average = (depth[corners[0]] + depth[corners[1]] + depth[corners[2]] + depth[corners[3]]) / 4;
						pieces.Add((average, pieces.Count, new ScenePolygon(surface.Pen,
							[screen[corners[0]], screen[corners[1]], screen[corners[2]], screen[corners[3]]])));
						continue;
					}
					var polygon = view.ClipPolygon([cube[corners[0]], cube[corners[1]], cube[corners[2]], cube[corners[3]]]);
					if (polygon.Count < 3)
						continue;
					pieces.Add((polygon.Average(view.Depth), pieces.Count, new ScenePolygon(surface.Pen, [.. polygon.Select(view.ToScreen)])));
				}
		}
		// Farthest first; pieces at the same depth keep their order.
		pieces.Sort((a, b) => a.Depth != b.Depth ? b.Depth.CompareTo(a.Depth) : a.Order.CompareTo(b.Order));
		foreach (var piece in pieces)
			scene.Add(piece.Polygon);
	}

	/// <summary>A curve, broken at non-finite points and cut where it leaves the box.</summary>
	static void AddCurve(ImmutableArray<ScenePrimitive>.Builder scene, Projection3D view, int pen, ImmutableArray<Point3> points)
	{
		var run = new List<ScreenPoint>();
		void Flush()
		{
			if (run.Count > 1)
				scene.Add(new ScenePolyline(SceneStroke.Pen, pen, [.. run]));
			run.Clear();
		}
		for (int k = 0; k + 1 < points.Length; k++)
		{
			var a = view.Cube(points[k]);
			var b = view.Cube(points[k + 1]);
			if (!a.IsFinite || !b.IsFinite || !view.ClipSegment(ref a, ref b, out bool clippedStart, out bool clippedEnd))
			{
				Flush();
				continue;
			}
			if (clippedStart)
				Flush();
			if (run.Count == 0)
				run.Add(view.ToScreen(a));
			run.Add(view.ToScreen(b));
			if (clippedEnd)
				Flush();
		}
		Flush();
	}

	static void AddMarkers(ImmutableArray<ScenePrimitive>.Builder scene, Projection3D view, int pen, PlotStyle style, IEnumerable<Point3> points)
	{
		var visible = points.Select(view.Cube).Where(view.IsVisible).Select(view.ToScreen).ToImmutableArray();
		if (!visible.IsEmpty)
			scene.Add(new SceneMarkers(pen, style, visible));
	}

	/// <summary>
	/// Ticks and their labels along an edge, and the axis title beside its middle. Labels are pushed
	/// away from the box, in the direction of <paramref name="outward"/> (from the middle of the box to the edge).
	/// </summary>
	static void AddAxisLabels(ImmutableArray<ScenePrimitive>.Builder scene, Projection3D view, IReadOnlyList<double> ticks,
		Func<double, Vec3> position, Vec3 outward, string title)
	{
		foreach (var tick in ticks)
		{
			var at = position(tick);
			if (!view.IsVisible(at) || Direction(view, at, outward) is not { } direction)
				continue;
			var p = view.ToScreen(at);
			scene.Add(new ScenePolyline(SceneStroke.Tick, 0, [p, Offset(p, direction, 5)]));
			// Text is centred on its anchor, so it needs more room below or above the tick than beside it.
			scene.Add(new SceneText(PlotLayout.FormatTick(tick), Offset(p, direction, 9 + 5 * Math.Abs(direction.Y)), Align(direction), 11));
		}
		var middle = outward;
		if (view.IsVisible(middle) && Direction(view, middle, outward) is { } away)
			scene.Add(new SceneText(title, Offset(view.ToScreen(middle), away, 34), TextAlign.Middle, 12));
	}

	/// <summary>The screen direction in which <paramref name="outward"/> points at <paramref name="at"/>, as a unit vector.</summary>
	static ScreenPoint? Direction(Projection3D view, Vec3 at, Vec3 outward)
	{
		var beyond = at + outward * 0.1;
		if (view.Depth(beyond) <= 0)
			return null;
		var a = view.ToScreen(at);
		var b = view.ToScreen(beyond);
		double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Sqrt(dx * dx + dy * dy);
		// Seen end-on: put the label below.
		return length < 1e-6 ? new ScreenPoint(0, 1) : new ScreenPoint(dx / length, dy / length);
	}

	static ScreenPoint Offset(ScreenPoint p, ScreenPoint direction, double distance) =>
		new(p.X + direction.X * distance, p.Y + direction.Y * distance);

	static TextAlign Align(ScreenPoint direction) =>
		direction.X < -0.35 ? TextAlign.End : direction.X > 0.35 ? TextAlign.Start : TextAlign.Middle;
}
