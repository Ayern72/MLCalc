using System.Globalization;
using System.Numerics;
using MLCalc.Interpreter;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Mathematics;

namespace MLCalc.Plotting;

/// <summary>Settings for the plot commands added by <see cref="PlottingInterpreterExtensions.AddPlotting"/>.</summary>
public sealed record PlottingOptions
{
	/// <summary>The default settings.</summary>
	public static PlottingOptions Default { get; } = new();

	/// <summary>
	/// Whether <c>savefig("plot.svg")</c> writes SVG files, relative to the interpreter's
	/// <see cref="MLCalcInterpreter.WorkingDirectory"/>. Off by default, so that programs cannot
	/// write files unless the host allows it; the host can add its own formats with
	/// <see cref="PlotCommands.RegisterFigureFormat"/>.
	/// </summary>
	public bool WriteSvgFiles { get; init; }

	/// <summary>The most grid lines each way in a surface from <c>fplot3d</c>; the default is 200.</summary>
	public int MaxSurfaceGridLines
	{
		get;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, 2);
			field = value;
		}
	} = 200;

	/// <summary>The most points in a curve from <c>fplot3d</c>; the default is 100 000.</summary>
	public int MaxCurvePoints
	{
		get;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, 2);
			field = value;
		}
	} = 100_000;
}

/// <summary>
/// Saves a figure to a file: (figure, full path, width, height). Returns the line <c>savefig</c>
/// shows in the console, or null for "Saved" and the path; a web server, for example, sends the
/// file to the browser and says so.
/// </summary>
public delegate string? FigureSaver(FigureSnapshot figure, string path, double width, double height);

/// <summary>
/// The plot commands of the MLCalc language (<c>plot</c>, <c>fplot</c>, <c>fplot3d</c>,
/// <c>xrange</c>, <c>view</c>, <c>savefig</c> and the others), which draw into a <see cref="Figure"/>.
/// Add them to an interpreter with <see cref="PlottingInterpreterExtensions.AddPlotting"/>.
/// </summary>
/// <remarks>
/// The commands run on the interpreter's thread and change the figure there. A front-end can
/// render the figure on another thread at the same time: <see cref="Figure.Snapshot"/> and
/// <see cref="Figure.Changed"/> are safe to use from any thread.
/// </remarks>
public sealed class PlotCommands
{
	/// <summary>The most pixels in an image saved by <c>savefig</c> in a bitmap format such as PNG.</summary>
	const double MaxImagePixels = 64_000_000;

	readonly MLCalcInterpreter interpreter;
	readonly Figure figure;
	readonly PlottingOptions options;
	readonly Dictionary<string, FigureSaver> formats = new(StringComparer.OrdinalIgnoreCase);

	PlotCommands(MLCalcInterpreter interpreter, Figure figure, PlottingOptions options)
	{
		this.interpreter = interpreter;
		this.figure = figure;
		this.options = options;
		if (options.WriteSvgFiles)
			formats[".svg"] = (snapshot, path, width, height) =>
			{
				SvgWriter.Save(snapshot, path, width, height);
				return null;
			};
		RegisterCommands();
	}

	internal static PlotCommands Create(MLCalcInterpreter interpreter, Figure figure, PlottingOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(interpreter);
		ArgumentNullException.ThrowIfNull(figure);
		return new PlotCommands(interpreter, figure, options ?? PlottingOptions.Default);
	}

	/// <summary>The figure the commands draw into.</summary>
	public Figure Figure => figure;

	/// <summary>
	/// Adds an image format to <c>savefig</c>, e.g. ".png" in the desktop app, or replaces one. The
	/// saver is called on the interpreter's thread with the full path of the file.
	/// </summary>
	public void RegisterFigureFormat(string extension, FigureSaver saver)
	{
		ArgumentNullException.ThrowIfNull(saver);
		if (!extension.StartsWith('.'))
			extension = "." + extension;
		formats[extension] = saver;
	}

	/// <summary>Adds a command under one or more names; the body returns null, as built-in commands do.</summary>
	void Add(Func<CallContext, ComplexMatrix?> command, params string[] names)
	{
		foreach (var name in names)
			interpreter.AddCommand(name, call => command(call));
	}

	/// <summary>A plot style: <c>-</c> (line), <c>+</c> (crosses), <c>*</c> (stars), <c>.</c> (dots), bare or as a string.</summary>
	static PlotStyle Style(CallContext call, int index)
	{
		if (index >= call.Count)
			return PlotStyle.Line;
		string symbol = call.Arguments[index] switch
		{
			StyleLiteral s => s.Symbol,
			StringLiteral s => s.Value,
			_ => throw call.Error($"argument {index + 1} must be a plot style: -, +, * or \".\".", index),
		};
		return symbol switch
		{
			"-" => PlotStyle.Line,
			"+" => PlotStyle.Cross,
			"*" => PlotStyle.Star,
			"." or "!" => PlotStyle.Dot,
			_ => throw call.Error($"unknown plot style '{symbol}'; use -, +, * or \".\".", index),
		};
	}

	/// <summary>
	/// A 3D point given as three real numbers starting at <paramref name="index"/>, or as one real
	/// vector of three elements, e.g. <c>camposition(20, -30, 10)</c> or <c>camposition([20 -30 10])</c>.
	/// </summary>
	static Point3 Point(CallContext call, int index)
	{
		if (call.Count == index + 3)
			return new Point3(call.Real(index), call.Real(index + 1), call.Real(index + 2));
		var value = call.Matrix(index);
		if (call.Count != index + 1 || !value.IsVector || value.Count != 3)
			throw call.Error("expected a point: x, y and z, or a vector [x y z].");
		var span = value.AsSpan();
		for (int k = 0; k < 3; k++)
			if (!ComplexMath.IsReal(span[k]))
				throw call.Error("the point must be real.", index);
		return new Point3(span[0].Real, span[1].Real, span[2].Real);
	}

	void RegisterCommands()
	{
		Add(call =>
		{
			call.RequireCount(2);
			figure.Initialize(call.Integer(0, min: 1), call.Integer(1, min: 1));
			return null;
		}, "initdisplay");
		Add(call =>
		{
			call.RequireCount(2);
			figure.SelectAxes(call.Integer(0, min: 1), call.Integer(1, min: 1));
			return null;
		}, "currentwin");
		Add(call =>
		{
			call.RequireCount(2);
			figure.SetXRange(call.Real(0), call.Real(1));
			return null;
		}, "xrange");
		Add(call =>
		{
			call.RequireCount(2);
			figure.SetYRange(call.Real(0), call.Real(1));
			return null;
		}, "yrange");
		Add(call =>
		{
			call.RequireCount(1);
			figure.SetPen(call.Integer(0, min: 0));
			return null;
		}, "pen");
		Add(call =>
		{
			call.RequireCount(0);
			figure.ClearCurrent();
			return null;
		}, "clear", "clf");
		Add(call =>
		{
			call.RequireCount(0, 1);
			figure.ShowGrid(call.Count == 0 || call.Real(0) != 0);
			return null;
		}, "grid");
		Add(call => SetText(call, figure.SetTitle), "title");
		Add(call => SetText(call, figure.SetXTitle), "xtitle", "xlabel");
		Add(call => SetText(call, figure.SetYTitle), "ytitle", "ylabel");
		Add(call =>
		{
			call.RequireCount(2);
			figure.AddDot(call.Real(0), call.Real(1));
			return null;
		}, "dot");
		Add(Plot, "plot");
		Add(call =>
		{
			call.RequireCount(1, 2);
			var z = call.Matrix(0);
			figure.AddComplexSeries(z.AsSpan().ToArray().Select(v => new PlotPoint(v.Real, v.Imaginary)), Style(call, 1));
			return null;
		}, "cplot");
		Add(call =>
		{
			call.RequireCount(2);
			var function = PlotFunction(call.RealFunction(0, 1));
			figure.AddFunction(call.String(1), function);
			return null;
		}, "fplot");
		RegisterPlot3DCommands();
		Add(call =>
		{
			call.RequireCount(1);
			var coefficients = call.Matrix(0);
			if (!coefficients.IsVector)
				throw call.Error("the coefficients must be a vector.", 0);
			var c = coefficients.AsSpan().ToArray();
			figure.AddFunction(PolynomialLabel(c), x => Polynomial.Evaluate(c, x).Real);
			return null;
		}, "polyplot");
		Add(call =>
		{
			call.Output.WriteLine("print: printing is not supported in this version; use savefig(\"plot.svg\").");
			return null;
		}, "print");

		// savefig("plot.svg") or savefig("plot.png", width, height): the formats come from the options and
		// the host (RegisterFigureFormat).
		Add(call =>
		{
			call.RequireCount(1, 3);
			string path = call.String(0);
			// A ':' in the file name would write to an NTFS alternate data stream ("a.txt:b.svg"
			// creates a.txt), getting around the file type check below.
			if (Path.GetFileName(path).Contains(':'))
				throw call.Error("the file name must not contain ':'.", 0);
			string extension = Path.GetExtension(path);
			if (formats.Count == 0)
				throw call.Error("saving figures is not available here.");
			if (!formats.TryGetValue(extension, out var saver))
				throw call.Error($"unsupported file type '{extension}'; use {string.Join(" or ", formats.Keys)}.", 0);
			bool bitmap = !extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);
			double width = call.Count > 1 ? call.Real(1) : 800;
			double height = call.Count > 2 ? call.Real(2) : 600;
			// Written so that NaN is rejected too (every comparison with NaN is false).
			if (!(width >= 50 && height >= 50 && width <= 16000 && height <= 16000))
				throw call.Error("the size must be between 50 and 16000.", 1);
			// A bitmap of 16000×16000 takes 1 GB, rendered in one piece; SVG has no such cost.
			if (bitmap && width * height > MaxImagePixels)
				throw call.Error($"an image can have at most {MaxImagePixels / 1_000_000} million pixels (for example 8000×8000).", 1);
			string fullPath = call.Interpreter.ResolvePath(path);
			string? message;
			try
			{
				message = saver(figure.Snapshot(), fullPath, width, height);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				throw call.Error(ex.Message, 0);
			}
			call.Output.WriteLine(message ?? $"Saved {fullPath}");
			return null;
		}, "savefig");
	}

	/// <summary>The default number of grid lines each way in a surface from <c>fplot3d</c>.</summary>
	const int DefaultSurfaceSamples = 40;

	void RegisterPlot3DCommands()
	{
		Add(call =>
		{
			call.RequireCount(2);
			figure.SetZRange(call.Real(0), call.Real(1));
			return null;
		}, "zrange");
		Add(call => SetText(call, figure.SetZTitle), "ztitle", "zlabel");
		Add(call =>
		{
			call.RequireCount(3);
			figure.AddDot3D(call.Real(0), call.Real(1), call.Real(2));
			return null;
		}, "dot3d");
		Add(Plot3D, "plot3d");
		Add(call => call.Count >= 5 ? FPlot3DCurve(call) : FPlot3DSurface(call), "fplot3d");
		Add(call =>
		{
			call.RequireCount(0, 2);
			if (call.Count == 2)
				figure.SetView(call.Real(0), call.Real(1));
			else if (call.Count == 0 || call.Real(0) == 3)
				figure.ResetView();
			else if (call.Real(0) == 2)
				figure.SetView(0, 90);
			else
				throw call.Error("use view(az, el), view(2) (from above) or view(3) (the default).", 0);
			return null;
		}, "view");
		Add(call =>
		{
			call.RequireCount(1, 3);
			figure.SetCameraPosition(Point(call, 0));
			return null;
		}, "camposition");
		Add(call =>
		{
			call.RequireCount(1, 3);
			figure.SetCameraTarget(Point(call, 0));
			return null;
		}, "camtarget");
	}

	/// <summary>fplot3d("x", "y", "f(x, y)" [, n]): a surface over the x and y ranges on an n×n grid.</summary>
	ComplexMatrix? FPlot3DSurface(CallContext call)
	{
		if (call.Count is not (3 or 4))
			throw call.Error(FPlot3DUsage);
		int samples = DefaultSurfaceSamples;
		if (call.Count == 4)
		{
			samples = call.Integer(3, min: 1);
			if (samples < 2 || samples > options.MaxSurfaceGridLines)
				throw call.Error($"the number of grid lines must be between 2 and {options.MaxSurfaceGridLines}.", 3);
		}
		var function = call.RealFunction2(0, 1, 2);
		figure.AddSurface(call.String(2), (x, y) =>
		{
			try
			{
				return function(x, y);
			}
			catch (RuntimeError error) when (IsUndefinedHere(error))
			{
				return double.NaN;
			}
		}, samples);
		return null;
	}

	/// <summary>
	/// fplot3d("t", "x(t)", "y(t)", "z(t)", t0, t1 [, n]): a curve through the points (x(t), y(t), z(t))
	/// for n values of t from t0 to t1. Points where an expression is undefined become gaps.
	/// </summary>
	ComplexMatrix? FPlot3DCurve(CallContext call)
	{
		if (call.Count is not (6 or 7))
			throw call.Error(FPlot3DUsage);
		double t0 = call.Real(4), t1 = call.Real(5);
		if (!double.IsFinite(t0))
			throw call.Error("the start of the range must be a finite number.", 4);
		if (!double.IsFinite(t1))
			throw call.Error("the end of the range must be a finite number.", 5);
		if (t0 == t1)
			throw call.Error("the range must not be empty: t0 and t1 are equal.", 5);
		int samples = Figure.FunctionSamples;
		if (call.Count == 7)
		{
			samples = call.Integer(6, min: 1);
			if (samples < 2 || samples > options.MaxCurvePoints)
				throw call.Error($"the number of points must be between 2 and {options.MaxCurvePoints}.", 6);
		}
		var x = PlotFunction(call.RealFunction(0, 1));
		var y = PlotFunction(call.RealFunction(0, 2));
		var z = PlotFunction(call.RealFunction(0, 3));
		var points = new Point3[samples];
		double step = (t1 - t0) / (samples - 1);
		for (int k = 0; k < samples; k++)
		{
			double t = k == samples - 1 ? t1 : t0 + k * step;
			points[k] = new Point3(x(t), y(t), z(t));
		}
		figure.AddSeries3D(points, PlotStyle.Line);
		return null;
	}

	const string FPlot3DUsage = "use fplot3d(\"x\", \"y\", \"f(x, y)\") or fplot3d(\"x\", \"y\", \"f(x, y)\", n) for a surface, "
		+ "or fplot3d(\"t\", \"x(t)\", \"y(t)\", \"z(t)\", t0, t1) or fplot3d(…, t0, t1, n) for a curve.";

	/// <summary>
	/// plot3d(x, y, z [, style]) with vectors of the same length, or matrices of the same size whose
	/// rows are drawn as separate curves; or plot3d(P [, style]) with one point per row of an n×3
	/// matrix (or per column of a 3×n one).
	/// </summary>
	ComplexMatrix? Plot3D(CallContext call)
	{
		call.RequireCount(1, 4);
		var curves = new List<Point3[]>();
		PlotStyle style;
		if (call.Count <= 2)
		{
			var p = call.Matrix(0);
			style = Style(call, 1);
			if (p.Columns == 3)
				curves.Add([.. Enumerable.Range(0, p.Rows).Select(r => new Point3(p[r, 0].Real, p[r, 1].Real, p[r, 2].Real))]);
			else if (p.Rows == 3)
				curves.Add([.. Enumerable.Range(0, p.Columns).Select(c => new Point3(p[0, c].Real, p[1, c].Real, p[2, c].Real))]);
			else
				throw call.Error($"the points must be an n×3 matrix (one point per row), not {p.Rows}×{p.Columns}.", 0);
		}
		else
		{
			var x = call.Matrix(0);
			var y = call.Matrix(1);
			var z = call.Matrix(2);
			style = Style(call, 3);
			if (x.IsVector && y.IsVector && z.IsVector)
			{
				if (y.Count != x.Count || z.Count != x.Count)
					throw call.Error($"x, y and z must have the same number of elements; they have {x.Count}, {y.Count} and {z.Count}.");
				var (xs, ys, zs) = (x.AsSpan().ToArray(), y.AsSpan().ToArray(), z.AsSpan().ToArray());
				curves.Add([.. Enumerable.Range(0, xs.Length).Select(k => new Point3(xs[k].Real, ys[k].Real, zs[k].Real))]);
			}
			else
			{
				if (y.Rows != x.Rows || y.Columns != x.Columns || z.Rows != x.Rows || z.Columns != x.Columns)
					throw call.Error($"x, y and z must be vectors of the same length or matrices of the same size; they are {x.Rows}×{x.Columns}, {y.Rows}×{y.Columns} and {z.Rows}×{z.Columns}.");
				for (int r = 0; r < x.Rows; r++)
					curves.Add([.. Enumerable.Range(0, x.Columns).Select(c => new Point3(x[r, c].Real, y[r, c].Real, z[r, c].Real))]);
			}
		}
		foreach (var curve in curves)
			figure.AddSeries3D(curve, style);
		return null;
	}

	static ComplexMatrix? SetText(CallContext call, Action<string> set)
	{
		call.RequireCount(1);
		set(call.String(0));
		return null;
	}

	/// <summary>
	/// plot(y), plot(x, y) or plot(x, y, style). When y is a matrix, each row (or column, when its
	/// length matches x) is drawn as its own series.
	/// </summary>
	ComplexMatrix? Plot(CallContext call)
	{
		call.RequireCount(1, 3);
		ComplexMatrix x, y;
		PlotStyle style;
		if (call.Count == 1)
		{
			y = call.Matrix(0);
			x = ComplexMatrix.Create(1, y.IsVector ? y.Count : y.Columns, (_, c) => c + 1);
			style = PlotStyle.Line;
		}
		else
		{
			x = call.Matrix(0);
			y = call.Matrix(1);
			style = Style(call, 2);
		}

		if (!x.IsVector)
			throw call.Error("x must be a vector.", 0);
		var xs = x.AsSpan().ToArray().Select(v => v.Real).ToArray();
		int n = xs.Length;

		IEnumerable<double[]> series;
		if (y.IsVector && y.Count == n)
			series = [y.AsSpan().ToArray().Select(v => v.Real).ToArray()];
		else if (y.Columns == n)
			series = Enumerable.Range(0, y.Rows).Select(r => Enumerable.Range(0, n).Select(c => y[r, c].Real).ToArray());
		else if (y.Rows == n)
			series = Enumerable.Range(0, y.Columns).Select(c => Enumerable.Range(0, n).Select(r => y[r, c].Real).ToArray());
		else
			throw call.Error($"x has {n} elements but y is {y.Rows}×{y.Columns}.", 1);

		foreach (var ys in series)
			figure.AddSeries(xs.Zip(ys, (a, b) => new PlotPoint(a, b)), style);
		return null;
	}

	/// <summary>
	/// Plots the real part. Points where the function is undefined (division by zero, a logarithm
	/// of zero, …) become gaps; errors in the expression itself (a wrong index or size) are reported,
	/// since they would otherwise just leave the plot empty.
	/// </summary>
	static Func<double, double> PlotFunction(Func<double, double> function) => x =>
	{
		try
		{
			return function(x);
		}
		catch (RuntimeError error) when (IsUndefinedHere(error))
		{
			return double.NaN;
		}
	};

	static bool IsUndefinedHere(Exception error)
	{
		for (Exception? e = error; e is not null; e = e.InnerException)
			if (e is MathException math)
				return math.Error is not (MathError.Index or MathError.Dimension or MathError.TooLarge);
		return false;
	}

	static string PolynomialLabel(Complex[] coefficients) =>
		string.Join(" + ", coefficients.Select((c, k) => k switch
		{
			0 => ComplexMatrix.Format(c, "G4"),
			1 => ComplexMatrix.Format(c, "G4") + "·x",
			_ => ComplexMatrix.Format(c, "G4") + "·x^" + k.ToString(CultureInfo.InvariantCulture),
		}));

}

/// <summary>Adds the plot commands to an interpreter.</summary>
public static class PlottingInterpreterExtensions
{
	/// <summary>
	/// Adds the plot commands to <paramref name="interpreter"/>, drawing into <paramref name="figure"/>.
	/// </summary>
	/// <param name="interpreter">The interpreter that gets the commands.</param>
	/// <param name="figure">The plot windows the commands draw into.</param>
	/// <param name="options">Limits and file output; <see cref="PlottingOptions.Default"/> when null.</param>
	/// <returns>The commands, for adding image formats to <c>savefig</c>.</returns>
	/// <exception cref="ArgumentException">The interpreter already has commands with these names.</exception>
	public static PlotCommands AddPlotting(this MLCalcInterpreter interpreter, Figure figure, PlottingOptions? options = null) =>
		PlotCommands.Create(interpreter, figure, options);
}
