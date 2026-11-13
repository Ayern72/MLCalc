using System.Collections.Immutable;

namespace MLCalc.Plotting;

/// <summary>Whether the axes show real data (x, y) or the complex plane (Re, Im).</summary>
public enum AxesKind
{
	Cartesian,
	Complex,
}

/// <summary>An immutable picture of one plot window, for rendering.</summary>
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
	ImmutableArray<PlotItem> Items)
{
	public static Axes Default { get; } = new(AxesKind.Cartesian, -10, 10, -10, 10, "", "", "", false, 1, []);
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

	readonly Lock gate = new();
	Axes[] axes = [Axes.Default];
	int rows = 1, columns = 1, currentRow, currentColumn;
	long version;

	/// <summary>Raised after every change, on the thread that made it.</summary>
	public event EventHandler? Changed;

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
	public void SelectAxes(int row, int column)
	{
		lock (gate)
		{
			if (row < 1 || column < 1 || row > rows || column > columns)
				throw new ArgumentOutOfRangeException(nameof(row), $"There is no plot window ({row},{column}); the grid is {rows}×{columns}.");
		}
		Update(() =>
		{
			currentRow = row - 1;
			currentColumn = column - 1;
		});
	}

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

	/// <summary>Removes all items from the current axes and resets its pen (<c>clear</c>).</summary>
	public void ClearCurrent() => ModifyCurrent(Clear);

	/// <summary>Removes everything from every axes, keeping the grid and the ranges.</summary>
	public void ClearAll() => Update(() =>
	{
		for (int i = 0; i < axes.Length; i++)
			axes[i] = Clear(axes[i]);
	});

	public void SetPen(int pen) => ModifyCurrent(a => a with { Pen = Pens.Normalize(pen) });

	public void SetTitle(string title) => ModifyCurrent(a => a with { Title = title });
	public void SetXTitle(string title) => ModifyCurrent(a => a with { XTitle = title });
	public void SetYTitle(string title) => ModifyCurrent(a => a with { YTitle = title });
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

	/// <summary>Adds a single dot in the current pen without advancing it (<c>dot</c>).</summary>
	public void AddDot(double x, double y) => ModifyCurrent(a =>
	{
		var point = new PlotPoint(x, y);
		if (!a.Items.IsEmpty && a.Items[^1] is DotCloud cloud && cloud.Pen == a.Pen)
			return a with { Items = a.Items.SetItem(a.Items.Length - 1, cloud with { Points = cloud.Points.Add(point) }) };
		return a with { Items = a.Items.Add(new DotCloud(a.Pen, [point])) };
	});

	void AddWithNextPen(AxesKind kind, Func<int, PlotItem> create) => ModifyCurrent(a =>
	{
		// Switching between real and complex axes clears the window, as in the original.
		var target = a.Kind == kind ? a : Clear(a) with { Kind = kind };
		return target with { Items = target.Items.Add(create(target.Pen)), Pen = Pens.Normalize(target.Pen + 1) };
	});

	static Axes Clear(Axes a) => a with { Items = [], Pen = 1 };

	static void RequireRange(double min, double max)
	{
		if (!(min < max) || double.IsInfinity(min) || double.IsInfinity(max))
			throw new ArgumentException($"Invalid range: the lower limit ({min}) must be less than the upper limit ({max}).");
		// The axes divide the span into ticks and pixels, so it must be a finite, normal number:
		// -1e308..1e308 overflows to infinity and a tiny span such as 0..1e-320 underflows.
		if (!double.IsNormal(max - min))
			throw new ArgumentException($"Invalid range: the span from {min} to {max} is too large or too small.");
	}

	void ModifyCurrent(Func<Axes, Axes> change) => Update(() =>
	{
		int index = currentRow * columns + currentColumn;
		axes[index] = change(axes[index]);
	});

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
