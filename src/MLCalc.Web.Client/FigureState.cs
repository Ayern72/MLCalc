using System.Collections.Immutable;
using MLCalc.Plotting;
using MLCalc.Web.Protocol;

namespace MLCalc.Web.Client;

/// <summary>
/// The plot windows as the client has them, in the plot model's own types, so that the client draws
/// them with the same code as the desktop app and the SVG export. <see cref="Apply"/> takes the
/// server's figure messages; a window that did not change keeps the same <see cref="Axes"/>
/// object, so its drawing can be kept.
/// </summary>
public sealed class FigureState
{
	Axes[] windows = [Axes.Default];

	public int Rows { get; private set; } = 1;
	public int Columns { get; private set; } = 1;
	public int CurrentRow { get; private set; }
	public int CurrentColumn { get; private set; }
	public long Version { get; private set; } = -1;

	public Axes this[int row, int column] => windows[row * Columns + column];

	public FigureSnapshot Snapshot => new(Rows, Columns, CurrentRow, CurrentColumn, [.. windows], Version);

	public void Apply(FigureMessage message)
	{
		if (message.Rows != Rows || message.Columns != Columns)
		{
			Rows = message.Rows;
			Columns = message.Columns;
			windows = [.. Enumerable.Repeat(Axes.Default, Rows * Columns)];
		}
		CurrentRow = message.CurrentRow;
		CurrentColumn = message.CurrentColumn;
		Version = message.Version;
		foreach (var update in message.Windows)
		{
			if (update.Index < 0 || update.Index >= windows.Length)
				continue;
			if (update.Replace is { } window)
				windows[update.Index] = ToAxes(window);
			else if (update.Append is { } append)
				windows[update.Index] = Append(windows[update.Index], append);
		}
	}

	static Axes Append(Axes axes, WindowAppend append)
	{
		var items = axes.Items.ToBuilder();
		if (append.LastDots is { } dots && items.Count > 0)
		{
			items[^1] = items[^1] switch
			{
				DotCloud cloud => cloud with { Points = cloud.Points.AddRange(Points2D(dots)) },
				DotCloud3D cloud => cloud with { Points = cloud.Points.AddRange(Points3D(dots)) },
				var other => other,
			};
		}
		items.AddRange(append.Items.Select(ToItem));
		return axes with { Items = items.ToImmutable(), Pen = append.Pen };
	}

	// ───────────── Protocol → plot model ─────────────

	public static Axes ToAxes(PlotWindow window) => new Axes(
		window.Kind switch
		{
			WindowKind.Complex => AxesKind.Complex,
			WindowKind.Space => AxesKind.Space,
			_ => AxesKind.Cartesian,
		},
		window.XMin, window.XMax, window.YMin, window.YMax,
		window.Title, window.XTitle, window.YTitle, window.Grid, window.Pen,
		[.. window.Items.Select(ToItem)])
	{
		ZMin = window.ZMin,
		ZMax = window.ZMax,
		ZTitle = window.ZTitle,
		Camera = new Camera(window.Camera.Azimuth, window.Camera.Elevation, window.Camera.Distance,
			ToPoint(window.Camera.Position), ToPoint(window.Camera.Target)),
	};

	static Point3? ToPoint(double[]? p) => p is [var x, var y, var z] ? new Point3(x, y, z) : null;

	public static PlotItem ToItem(PlotItemData item)
	{
		var points = new PointsData(item.X, item.Y, item.Z);
		return item.Type switch
		{
			ItemType.Series => new PointSeries(item.Pen, ToStyle(item.Style), [.. Points2D(points)]),
			ItemType.Function => new FunctionSeries(item.Pen, item.Label ?? "", [.. Points2D(points)]),
			ItemType.Dots => new DotCloud(item.Pen, [.. Points2D(points)]),
			ItemType.Series3D => new PointSeries3D(item.Pen, ToStyle(item.Style), [.. Points3D(points)]),
			ItemType.Dots3D => new DotCloud3D(item.Pen, [.. Points3D(points)]),
			ItemType.Surface => new Surface(item.Pen, item.Label ?? "", item.XCount ?? 0, item.YCount ?? 0, [.. Points3D(points)]),
			_ => throw new NotSupportedException($"Unknown item type {item.Type}."),
		};
	}

	static PlotStyle ToStyle(SeriesStyle? style) => style switch
	{
		SeriesStyle.Cross => PlotStyle.Cross,
		SeriesStyle.Star => PlotStyle.Star,
		SeriesStyle.Dot => PlotStyle.Dot,
		_ => PlotStyle.Line,
	};

	static double Coordinate(float? value) => value ?? double.NaN;

	static IEnumerable<PlotPoint> Points2D(PointsData points)
	{
		for (int i = 0; i < points.X.Length; i++)
			yield return new PlotPoint(Coordinate(points.X[i]), Coordinate(points.Y[i]));
	}

	static IEnumerable<Point3> Points3D(PointsData points)
	{
		for (int i = 0; i < points.X.Length; i++)
			yield return new Point3(Coordinate(points.X[i]), Coordinate(points.Y[i]), Coordinate(points.Z?[i]));
	}
}
