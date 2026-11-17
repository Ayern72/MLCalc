using System.Collections.Immutable;

namespace MLCalc.Plotting;

/// <summary>Whether the axes show real data (x, y), the complex plane (Re, Im) or 3D data (x, y, z).</summary>
public enum AxesKind
{
	/// <summary>An x-y window.</summary>
	Cartesian,
	/// <summary>The complex plane, from <c>cplot</c>: real part across, imaginary part up.</summary>
	Complex,
	/// <summary>A 3D window with x, y and z axes.</summary>
	Space,
}

/// <summary>
/// An immutable picture of one plot window, for rendering. <c>PointCount</c> is the number of points
/// in <c>Items</c> (dots, series and curves together), checked against <see cref="Figure.MaxPointsPerWindow"/>.
/// </summary>
public sealed record Axes(
	AxesKind Kind,
	double XMin,
	double XMax,
	double YMin,
	double YMax,
	string Title,
	string XTitle,
	string YTitle,
	bool ShowGrid,
	int Pen,
	ImmutableArray<PlotItem> Items,
	int PointCount = 0)
{
	/// <summary>An empty x-y window with both ranges from −10 to 10.</summary>
	public static Axes Default { get; } = new(AxesKind.Cartesian, -10, 10, -10, 10, "", "", "", false, 1, []);

	/// <summary>The z range; used by 3D windows.</summary>
	public double ZMin { get; init; } = -10;

	/// <summary>The z range; used by 3D windows.</summary>
	public double ZMax { get; init; } = 10;

	/// <summary>The z axis title of a 3D window.</summary>
	public string ZTitle { get; init; } = "";

	/// <summary>The camera of a 3D window. Kept when the window is cleared or changes kind.</summary>
	public Camera Camera { get; init; } = Camera.Default;

	/// <summary>How a 3D window is projected onto the screen.</summary>
	public Projection Projection { get; init; } = Projection.Perspective;
}

/// <summary>An immutable picture of the whole figure, for rendering.</summary>
public sealed record FigureSnapshot(int Rows, int Columns, int CurrentRow, int CurrentColumn, ImmutableArray<Axes> Axes, long Version)
{
	/// <summary>The axes at a zero-based grid position.</summary>
	public Axes this[int row, int column] => Axes[row * Columns + column];
}

/// <summary>
/// The plot model: a grid of up to <see cref="MaxGrid"/>×<see cref="MaxGrid"/> plot windows (axes),
/// one of which is current. Commands modify the current axes. Thread-safe: an interpreter can draw
/// on a worker thread while a front-end renders <see cref="Snapshot"/>s raised through <see cref="Changed"/>.
/// </summary>
public sealed class Figure
{
	/// <summary>The largest grid, as in the original (3×3).</summary>
	public const int MaxGrid = 3;

	/// <summary>Number of samples used when plotting a function over the x range.</summary>
	public const int FunctionSamples = 1000;

	/// <summary>
	/// The default for <see cref="MaxPointsPerWindow"/>: 2 million points, about 100 MB. Plot items
	/// stay until the window is cleared, so without a limit a loop calling <c>dot</c> could fill the
	/// computer's memory (and redrawing would become ever slower).
	/// </summary>
	public const int DefaultMaxPointsPerWindow = 2_000_000;

	readonly Lock gate = new();
	Axes[] axes = [Axes.Default];
	int rows = 1, columns = 1, currentRow, currentColumn;
	long version;

	/// <param name="maxPointsPerWindow">The most points one plot window may hold; see <see cref="DefaultMaxPointsPerWindow"/>.</param>
	public Figure(int maxPointsPerWindow = DefaultMaxPointsPerWindow)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPointsPerWindow);
		MaxPointsPerWindow = maxPointsPerWindow;
	}

	/// <summary>The most points (dots, series points and curve samples together) one plot window may hold.</summary>
	public int MaxPointsPerWindow { get; }

	/// <summary>Raised after every change, on the thread that made it.</summary>
	public event EventHandler? Changed;

	/// <summary>An immutable copy of the whole figure, safe to render on any thread.</summary>
	public FigureSnapshot Snapshot()
	{
		lock (gate)
			return new FigureSnapshot(rows, columns, currentRow, currentColumn, [.. axes], version);
	}

	/// <summary>The current axes.</summary>
	public Axes Current
	{
		get
		{
			lock (gate)
				return axes[currentRow * columns + currentColumn];
		}
	}

	/// <summary>Splits the figure into a grid of new, empty axes and selects the top-left one (<c>initdisplay</c>).</summary>
	public void Initialize(int rows, int columns)
	{
		if (rows < 1 || columns < 1 || rows > MaxGrid || columns > MaxGrid)
			throw new ArgumentOutOfRangeException(nameof(rows), $"The plot grid must be between 1×1 and {MaxGrid}×{MaxGrid}.");
		Update(() =>
		{
			this.rows = rows;
			this.columns = columns;
			axes = Enumerable.Repeat(Axes.Default, rows * columns).ToArray();
			currentRow = currentColumn = 0;
		});
	}

	/// <summary>Selects the current axes by one-based row and column (<c>currentwin</c>).</summary>
	public void SelectAxes(int row, int column) => Update(() =>
	{
		// Checked under the same lock as the change: a separate check could pass for a 3×3 grid
		// that Initialize (Reset Plots in the desktop app) shrinks before the change is made.
		if (row < 1 || column < 1 || row > rows || column > columns)
			throw new ArgumentOutOfRangeException(nameof(row), $"There is no plot window ({row},{column}); the grid is {rows}×{columns}.");
		currentRow = row - 1;
		currentColumn = column - 1;
	});

	/// <summary>Sets the x range and clears the current axes, as the original <c>xrange</c>.</summary>
	public void SetXRange(double min, double max)
	{
		RequireRange(min, max);
		ModifyCurrent(a => Clear(a) with { XMin = min, XMax = max });
	}

	/// <summary>Sets the y range and clears the current axes, as the original <c>yrange</c>.</summary>
	public void SetYRange(double min, double max)
	{
		RequireRange(min, max);
		ModifyCurrent(a => Clear(a) with { YMin = min, YMax = max });
	}

	/// <summary>Sets the z range (used by 3D windows) and clears the current axes (<c>zrange</c>).</summary>
	public void SetZRange(double min, double max)
	{
		RequireRange(min, max);
		ModifyCurrent(a => Clear(a) with { ZMin = min, ZMax = max });
	}

	/// <summary>
	/// Points the camera from azimuth <paramref name="azimuth"/> and elevation <paramref name="elevation"/>
	/// (degrees) at its target (<c>view(az, el)</c>). The camera is then placed relative to the box; a
	/// camera set with <see cref="SetCameraPosition"/> keeps its distance to the target.
	/// </summary>
	public void SetView(double azimuth, double elevation)
	{
		RequireAngles(azimuth, elevation);
		ModifyCurrent(a => WithView(a, azimuth, elevation));
	}

	/// <summary>Restores the default camera (<c>view(3)</c>).</summary>
	public void ResetView() => ModifyCurrent(a => a with { Camera = Camera.Default });

	/// <summary>
	/// <see cref="SetView(double, double)"/> for the window at a zero-based grid position, current or
	/// not (turning a window with the mouse). Returns false, changing nothing, when there is no such
	/// window or it is not a 3D window: the grid or the window may have changed since the drag began.
	/// </summary>
	public bool SetView(int row, int column, double azimuth, double elevation)
	{
		RequireAngles(azimuth, elevation);
		return ModifyWindow(row, column, a => WithView(a, azimuth, elevation));
	}

	/// <summary><see cref="ResetView()"/> for the window at a zero-based grid position; see <see cref="SetView(int, int, double, double)"/>.</summary>
	public bool ResetView(int row, int column) => ModifyWindow(row, column, a => a with { Camera = Camera.Default });

	/// <summary>
	/// Moves the camera of the 3D window at a zero-based grid position towards its target
	/// (<paramref name="factor"/> below 1) or away from it (above 1), keeping its direction (the mouse
	/// wheel). <paramref name="cell"/> is the window's rectangle on the screen, as given to
	/// <see cref="Scene3D.Build"/>: the default camera's distance depends on it. A camera set with
	/// <c>camposition</c> stays a fixed point, moved along the line to the target. Returns false as
	/// <see cref="SetView(int, int, double, double)"/> does.
	/// </summary>
	public bool MoveCamera(int row, int column, double factor, ScreenRect cell)
	{
		if (!(factor > 0) || !double.IsFinite(factor))
			throw new ArgumentOutOfRangeException(nameof(factor), "The factor must be a positive number.");
		return ModifyWindow(row, column, a =>
		{
			var view = new Projection3D(a, Scene3D.ViewArea(cell));
			double distance = view.DistanceToTarget;
			double moved = Math.Clamp(distance * factor, MinCameraDistance, MaxCameraDistance);
			if (!view.IsPlaced)
				return a with { Camera = a.Camera with { Distance = moved } };
			var position = a.Camera.Position!.Value;
			var target = view.TargetPosition;
			double s = moved / distance;
			return a with { Camera = a.Camera with { Position = new Point3(
				target.X + (position.X - target.X) * s, target.Y + (position.Y - target.Y) * s, target.Z + (position.Z - target.Z) * s) } };
		});
	}

	/// <summary>How close to and far from its target the mouse wheel moves a camera, in units of half the box.</summary>
	public const double MinCameraDistance = 0.05, MaxCameraDistance = 100;

	static void RequireAngles(double azimuth, double elevation)
	{
		if (!double.IsFinite(azimuth) || !(elevation >= -90 && elevation <= 90))
			throw new ArgumentException("The azimuth must be a number and the elevation between -90 and 90 degrees.");
	}

	static Axes WithView(Axes a, double azimuth, double elevation)
	{
		double? distance = a.Camera.Position is null ? a.Camera.Distance : Projection3D.CameraDistance(a);
		return a with { Camera = a.Camera with { Azimuth = azimuth, Elevation = elevation, Distance = distance, Position = null } };
	}

	/// <summary>Puts the camera at a fixed point, in data coordinates, outside the box (<c>camposition</c>).</summary>
	public void SetCameraPosition(Point3 position)
	{
		if (!position.IsFinite)
			throw new ArgumentException("The camera position must be finite numbers.");
		ModifyCurrent(a =>
		{
			var cube = Projection3D.ToCube(a, position);
			if (Math.Abs(cube.X) <= 1 && Math.Abs(cube.Y) <= 1 && Math.Abs(cube.Z) <= 1)
				throw new ArgumentException("The camera must be outside the box set by xrange, yrange and zrange.");
			return a with { Camera = a.Camera with { Position = position } };
		});
	}

	/// <summary>Sets the point the camera looks at, in data coordinates (<c>camtarget</c>).</summary>
	public void SetCameraTarget(Point3 target)
	{
		if (!target.IsFinite)
			throw new ArgumentException("The camera target must be finite numbers.");
		ModifyCurrent(a =>
		{
			if (a.Camera.Position == target)
				throw new ArgumentException("The camera target must not be where the camera is.");
			return a with { Camera = a.Camera with { Target = target } };
		});
	}

	/// <summary>Removes all items from the current axes and resets its pen (<c>clear</c>).</summary>
	public void ClearCurrent() => ModifyCurrent(Clear);

	/// <summary>Removes everything from every axes, keeping the grid and the ranges.</summary>
	public void ClearAll() => Update(() =>
	{
		for (int i = 0; i < axes.Length; i++)
			axes[i] = Clear(axes[i]);
	});

	/// <summary>Selects the pen for what is drawn next in the current window.</summary>
	public void SetPen(int pen) => ModifyCurrent(a => a with { Pen = Pens.Normalize(pen) });

	/// <summary>Sets the title of the current window.</summary>
	public void SetTitle(string title) => ModifyCurrent(a => a with { Title = title });
	/// <summary>Sets the x axis title of the current window.</summary>
	public void SetXTitle(string title) => ModifyCurrent(a => a with { XTitle = title });
	/// <summary>Sets the y axis title of the current window.</summary>
	public void SetYTitle(string title) => ModifyCurrent(a => a with { YTitle = title });
	/// <summary>Sets the z axis title of the current window.</summary>
	public void SetZTitle(string title) => ModifyCurrent(a => a with { ZTitle = title });
	/// <summary>Shows or hides the grid in the current window.</summary>
	public void ShowGrid(bool show = true) => ModifyCurrent(a => a with { ShowGrid = show });

	/// <summary>Adds a series of points in the current pen, then advances the pen (<c>plot</c>).</summary>
	public void AddSeries(IEnumerable<PlotPoint> points, PlotStyle style) =>
		AddWithNextPen(AxesKind.Cartesian, pen => new PointSeries(pen, style, [.. points]));

	/// <summary>Adds complex numbers as points in the complex plane (<c>cplot</c>).</summary>
	public void AddComplexSeries(IEnumerable<PlotPoint> points, PlotStyle style) =>
		AddWithNextPen(AxesKind.Complex, pen => new PointSeries(pen, style, [.. points]));

	/// <summary>
	/// Samples <paramref name="function"/> at <see cref="FunctionSamples"/> points across the current
	/// x range and adds the curve (<c>fplot</c>, <c>polyplot</c>). The function should return NaN
	/// where it is undefined; such points become gaps in the curve.
	/// </summary>
	public void AddFunction(string label, Func<double, double> function)
	{
		var current = Current;
		var points = ImmutableArray.CreateBuilder<PlotPoint>(FunctionSamples);
		double step = (current.XMax - current.XMin) / (FunctionSamples - 1);
		for (int i = 0; i < FunctionSamples; i++)
		{
			double x = current.XMin + i * step;
			points.Add(new PlotPoint(x, function(x)));
		}
		var samples = points.MoveToImmutable();
		AddWithNextPen(AxesKind.Cartesian, pen => new FunctionSeries(pen, label, samples));
	}

	/// <summary>Adds a series of 3D points in the current pen, then advances the pen (<c>plot3d</c>).</summary>
	public void AddSeries3D(IEnumerable<Point3> points, PlotStyle style) =>
		AddWithNextPen(AxesKind.Space, pen => new PointSeries3D(pen, style, [.. points]));

	/// <summary>
	/// Samples z = <paramref name="function"/>(x, y) on an <paramref name="samples"/>×<paramref name="samples"/>
	/// grid across the current x and y ranges and adds the surface (<c>fplot3d</c>). The function
	/// should return NaN where it is undefined; such points become holes.
	/// </summary>
	public void AddSurface(string label, Func<double, double, double> function, int samples)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(samples, 2);
		var current = Current;
		// Checked before sampling, which can take a while.
		RequireRoom(current.Kind == AxesKind.Space ? current : Clear(current), samples * samples);
		var points = ImmutableArray.CreateBuilder<Point3>(samples * samples);
		double xStep = (current.XMax - current.XMin) / (samples - 1), yStep = (current.YMax - current.YMin) / (samples - 1);
		for (int j = 0; j < samples; j++)
		{
			double y = j == samples - 1 ? current.YMax : current.YMin + j * yStep;
			for (int i = 0; i < samples; i++)
			{
				double x = i == samples - 1 ? current.XMax : current.XMin + i * xStep;
				points.Add(new Point3(x, y, function(x, y)));
			}
		}
		var grid = points.MoveToImmutable();
		AddWithNextPen(AxesKind.Space, pen => new Surface(pen, label, samples, samples, grid));
	}

	/// <summary>Adds a single 3D dot in the current pen without advancing it (<c>dot3d</c>).</summary>
	public void AddDot3D(double x, double y, double z) => ModifyCurrent(a =>
	{
		var target = a.Kind == AxesKind.Space ? a : SwitchKind(a, AxesKind.Space);
		RequireRoom(target, 1);
		var point = new Point3(x, y, z);
		if (!target.Items.IsEmpty && target.Items[^1] is DotCloud3D cloud && cloud.Pen == target.Pen)
			return target with { Items = target.Items.SetItem(target.Items.Length - 1, cloud with { Points = cloud.Points.Add(point) }), PointCount = target.PointCount + 1 };
		return target with { Items = target.Items.Add(new DotCloud3D(target.Pen, [point])), PointCount = target.PointCount + 1 };
	});

	/// <summary>Adds a single dot in the current pen without advancing it (<c>dot</c>).</summary>
	public void AddDot(double x, double y) => ModifyCurrent(a =>
	{
		// A 3D window becomes a 2D one; real and complex windows both take dots.
		if (a.Kind == AxesKind.Space)
			a = SwitchKind(a, AxesKind.Cartesian);
		RequireRoom(a, 1);
		var point = new PlotPoint(x, y);
		if (!a.Items.IsEmpty && a.Items[^1] is DotCloud cloud && cloud.Pen == a.Pen)
			return a with { Items = a.Items.SetItem(a.Items.Length - 1, cloud with { Points = cloud.Points.Add(point) }), PointCount = a.PointCount + 1 };
		return a with { Items = a.Items.Add(new DotCloud(a.Pen, [point])), PointCount = a.PointCount + 1 };
	});

	void AddWithNextPen(AxesKind kind, Func<int, PlotItem> create) => ModifyCurrent(a =>
	{
		// Switching between real and complex axes clears the window, as in the original; so does
		// switching between 2D and 3D.
		var target = a.Kind == kind ? a : SwitchKind(a, kind);
		var item = create(target.Pen);
		int points = item switch
		{
			PointSeries series => series.Points.Length,
			FunctionSeries function => function.Points.Length,
			DotCloud cloud => cloud.Points.Count,
			PointSeries3D series => series.Points.Length,
			DotCloud3D cloud => cloud.Points.Count,
			Surface surface => surface.Points.Length,
			_ => 0,
		};
		RequireRoom(target, points);
		return target with { Items = target.Items.Add(item), Pen = Pens.Normalize(target.Pen + 1), PointCount = target.PointCount + points };
	});

	void RequireRoom(Axes a, int points)
	{
		if ((long)a.PointCount + points > MaxPointsPerWindow)
			throw new ArgumentException(FormattableString.Invariant(
				$"The plot window is full: it can hold at most {MaxPointsPerWindow} points. Clear it (clear, xrange or yrange) before plotting more."));
	}

	static Axes Clear(Axes a) => a with { Items = [], Pen = 1, PointCount = 0 };

	/// <summary>
	/// Empties the axes for another kind of plot (2D, complex or 3D). Unlike <see cref="Clear"/>, the pen
	/// is kept, so that <c>pen(3)</c> before the first <c>plot3d</c> in a window takes effect.
	/// </summary>
	static Axes SwitchKind(Axes a, AxesKind kind) => a with { Kind = kind, Items = [], PointCount = 0 };

	static void RequireRange(double min, double max)
	{
		if (!(min < max) || double.IsInfinity(min) || double.IsInfinity(max))
			throw new ArgumentException(FormattableString.Invariant($"Invalid range: the lower limit ({min:G10}) must be less than the upper limit ({max:G10})."));
		// The axes divide the span into ticks and pixels, so it must be a finite, normal number:
		// -1e308..1e308 overflows to infinity and a tiny span such as 0..1e-320 underflows.
		if (!double.IsNormal(max - min))
			throw new ArgumentException(FormattableString.Invariant($"Invalid range: the span from {min:G10} to {max:G10} is too large or too small."));
	}

	void ModifyCurrent(Func<Axes, Axes> change) => Update(() =>
	{
		int index = currentRow * columns + currentColumn;
		axes[index] = change(axes[index]);
	});

	/// <summary>Changes the 3D window at a zero-based grid position; false when there is none there.</summary>
	bool ModifyWindow(int row, int column, Func<Axes, Axes> change)
	{
		lock (gate)
		{
			if (row < 0 || column < 0 || row >= rows || column >= columns || axes[row * columns + column].Kind != AxesKind.Space)
				return false;
			int index = row * columns + column;
			axes[index] = change(axes[index]);
			version++;
		}
		Changed?.Invoke(this, EventArgs.Empty);
		return true;
	}

	void Update(Action change)
	{
		lock (gate)
		{
			change();
			version++;
		}
		Changed?.Invoke(this, EventArgs.Empty);
	}
}
