using System.Globalization;
using System.Numerics;
using MLCalc.LinearAlgebra;
using MLCalc.Math;
using MLCalc.Numerics;
using MLCalc.Plotting;

namespace MLCalc.Interpreter;

/// <summary>The built-in functions and commands, looked up case-insensitively.</summary>
internal sealed class Builtins
{
	readonly Dictionary<string, Builtin> table = new(StringComparer.OrdinalIgnoreCase);
	readonly HashSet<string> pure = new(StringComparer.OrdinalIgnoreCase);
	bool registeringPure;

	public IEnumerable<string> Names => table.Keys.Order(StringComparer.OrdinalIgnoreCase);

	public bool TryGet(string name, out Builtin builtin) => table.TryGetValue(name, out builtin!);

	/// <summary>
	/// True for functions whose result depends only on their arguments and that have no side
	/// effects, so a call with constant arguments can be evaluated once (see <see cref="ConstantFolder"/>).
	/// </summary>
	public bool IsPure(string name) => pure.Contains(name);

	public Builtins()
	{
		// Element-wise and matrix functions are pure, except rand.
		registeringPure = true;
		RegisterElementFunctions();
		RegisterMatrixFunctions();
		registeringPure = false;
		pure.Remove("rand");

		// Not marked pure: functions given as text (they run their own evaluations), and commands.
		RegisterNumericFunctions();
		RegisterPlotCommands();
		RegisterConsoleCommands();
	}

	void Add(Builtin builtin, params string[] names)
	{
		foreach (var name in names)
		{
			table.Add(name, builtin);
			if (registeringPure)
				pure.Add(name);
		}
	}

	void Element(Func<Complex, Complex> function, params string[] names) => Add(call =>
	{
		call.RequireCount(1);
		return call.Matrix(0).Map(function);
	}, names);

	void Element2(Func<Complex, Complex, Complex> function, params string[] names) => Add(call =>
	{
		call.RequireCount(2);
		return ComplexMatrix.ElementWise(call.Matrix(0), call.Matrix(1), function);
	}, names);

	static Func<Complex, Complex, Complex> RealOnly(Func<double, double, double> function, string name) =>
		(x, y) => ComplexMath.IsReal(x) && ComplexMath.IsReal(y)
			? function(x.Real, y.Real)
			: throw new MathException(MathError.ComplexNotAllowed, $"{name} requires real arguments.");

	// ───────────── Element-wise functions ─────────────

	void RegisterElementFunctions()
	{
		Element(ComplexMath.Sin, "sin");
		Element(ComplexMath.Cos, "cos");
		Element(ComplexMath.Tan, "tan");
		Element(ComplexMath.Cot, "cot");
		Element(ComplexMath.Asin, "asin");
		Element(ComplexMath.Acos, "acos");
		Element(ComplexMath.Atan, "atan");
		Element(ComplexMath.Acot, "acot");
		Element(ComplexMath.Sinh, "sinh");
		Element(ComplexMath.Cosh, "cosh");
		Element(ComplexMath.Tanh, "tanh");
		Element(ComplexMath.Coth, "coth");
		Element(ComplexMath.Asinh, "asinh");
		Element(ComplexMath.Acosh, "acosh");
		Element(ComplexMath.Atanh, "atanh");
		Element(ComplexMath.Acoth, "acoth");
		Element(ComplexMath.Exp, "exp");
		Element(ComplexMath.Ln, "ln", "log");
		Element(ComplexMath.Log10, "log10");
		Element(ComplexMath.Sqrt, "sqrt");
		Element(z => z * z, "sqr");
		Element(z => ComplexMath.Abs(z), "abs");
		Element(z => ComplexMath.Arg(z), "arg", "angle");
		Element(ComplexMath.Conj, "conj");
		Element(z => z.Real, "re", "real");
		Element(z => z.Imaginary, "im", "imag");
		Element(z => new Complex(System.Math.Round(z.Real, MidpointRounding.AwayFromZero), System.Math.Round(z.Imaginary, MidpointRounding.AwayFromZero)), "round");
		Element(z => new Complex(System.Math.Ceiling(z.Real), System.Math.Ceiling(z.Imaginary)), "ceil");
		Element(z => new Complex(System.Math.Floor(z.Real), System.Math.Floor(z.Imaginary)), "floor");
		Element(z => new Complex(System.Math.Truncate(z.Real), System.Math.Truncate(z.Imaginary)), "fix");
		Element(z => ComplexMath.IsReal(z) ? System.Math.Sign(z.Real) : z / ComplexMath.Abs(z), "sign");
		Element(z => ComplexMath.IsReal(z) ? RealMath.Factorial(z.Real) : throw new MathException(MathError.ComplexNotAllowed, "Factorial of a complex number."), "factorial");
		Element2(ComplexMath.Root, "xroot");
		Element2(RealOnly(RealMath.Comb, "comb"), "comb");
		Element2(RealOnly(RealMath.Perm, "perm"), "perm");
		Element2(RealOnly(RealMath.Mod, "mod"), "mod", "rem");
		Element2(RealOnly(RealMath.Div, "div"), "div");
	}

	// ───────────── Matrix functions ─────────────

	void RegisterMatrixFunctions()
	{
		Add(call => Fill(call, ComplexMatrix.Ones), "ones");
		Add(call => Fill(call, ComplexMatrix.Zeros), "zeros", "zeroes");
		Add(call =>
		{
			var (rows, columns) = Size(call);
			return rows == columns ? ComplexMatrix.Identity(rows) : ComplexMatrix.Create(rows, columns, (r, c) => r == c ? 1 : 0);
		}, "id", "eye");
		Add(call =>
		{
			var (rows, columns) = call.Count == 0 ? (1, 1) : Size(call);
			var random = call.Interpreter.Random;
			return ComplexMatrix.Create(rows, columns, (_, _) => random.NextDouble());
		}, "rand");
		Add(call =>
		{
			call.RequireCount(1);
			var m = call.Matrix(0);
			return ComplexMatrix.RowVector(m.Rows, m.Columns);
		}, "size");
		Add(call => Dimension(call, m => m.Rows), "row", "rows");
		Add(call => Dimension(call, m => m.Columns), "col", "columns");
		Add(call => Dimension(call, m => m.IsEmpty ? 0 : System.Math.Max(m.Rows, m.Columns)), "length");
		Add(call => Dimension(call, m => m.Count), "numel");
		Add(call =>
		{
			call.RequireCount(1);
			return call.Matrix(0).Determinant();
		}, "det");
		Add(call =>
		{
			call.RequireCount(1);
			return call.Matrix(0).Inverse();
		}, "inv");
		Add(call =>
		{
			call.RequireCount(1);
			return call.Matrix(0).Transpose();
		}, "transpose");
		Add(call =>
		{
			call.RequireCount(2);
			return LinearSolver.Solve(call.Matrix(0), call.Matrix(1));
		}, "gauss");
		Add(call =>
		{
			call.RequireCount(3);
			return Polynomial.Fit(call.Matrix(0), call.Matrix(1), call.Integer(2, min: 0));
		}, "polyfit");
		Add(call =>
		{
			call.RequireCount(2);
			return Polynomial.Evaluate(call.Matrix(0), call.Matrix(1));
		}, "polyval");
		Add(call => Reduce(call, (a, b) => a + b, Complex.Zero), "sum");
		Add(call => Reduce(call, (a, b) => a * b, Complex.One), "prod");
		Add(call => Extreme(call, (a, b) => a.Real > b.Real), "max");
		Add(call => Extreme(call, (a, b) => a.Real < b.Real), "min");
	}

	static ComplexMatrix Fill(CallContext call, Func<int, int, ComplexMatrix> create)
	{
		var (rows, columns) = Size(call);
		return create(rows, columns);
	}

	/// <summary>Size from (n), (rows, columns), or the size of a matrix argument, as in the original ones(A).</summary>
	static (int Rows, int Columns) Size(CallContext call)
	{
		call.RequireCount(1, 2);
		if (call.Count == 2)
			return (call.Integer(0, min: 0), call.Integer(1, min: 0));
		var m = call.Matrix(0);
		if (!m.IsScalar)
			return (m.Rows, m.Columns);
		int n = call.Integer(0, min: 0);
		return (n, n);
	}

	static ComplexMatrix Dimension(CallContext call, Func<ComplexMatrix, int> measure)
	{
		call.RequireCount(1);
		return measure(call.Matrix(0));
	}

	/// <summary>Reduces a vector to a scalar, or each column of a matrix to a row (MATLAB sum/prod).</summary>
	static ComplexMatrix Reduce(CallContext call, Func<Complex, Complex, Complex> combine, Complex seed)
	{
		call.RequireCount(1);
		var m = call.Matrix(0);
		if (m.IsVector)
		{
			var total = seed;
			foreach (var value in m.AsSpan())
				total = combine(total, value);
			return total;
		}
		return ComplexMatrix.Create(1, m.Columns, (_, c) =>
		{
			var total = seed;
			for (int r = 0; r < m.Rows; r++)
				total = combine(total, m[r, c]);
			return total;
		});
	}

	/// <summary>max/min of a vector, of each column of a matrix, or element-wise of two arguments (by real part).</summary>
	static ComplexMatrix Extreme(CallContext call, Func<Complex, Complex, bool> better)
	{
		call.RequireCount(1, 2);
		if (call.Count == 2)
			return ComplexMatrix.ElementWise(call.Matrix(0), call.Matrix(1), (a, b) => better(b, a) ? b : a);
		var m = call.Matrix(0);
		if (m.IsEmpty)
			return ComplexMatrix.Zeros(0, 0);
		Complex Pick(IEnumerable<Complex> values) => values.Aggregate((a, b) => better(b, a) ? b : a);
		if (m.IsVector)
			return Pick(m.AsSpan().ToArray());
		return ComplexMatrix.Create(1, m.Columns, (_, c) => Pick(Enumerable.Range(0, m.Rows).Select(r => m[r, c])));
	}

	// ───────────── Numerical analysis (functions given as strings) ─────────────

	void RegisterNumericFunctions()
	{
		// feval("t", "sin(t)", x): evaluates the function at each element of x.
		Add(call =>
		{
			call.RequireCount(3);
			var function = call.Function(0, 1);
			var points = call.Matrix(2);
			var span = call.Arguments[1].Span;
			return points.Map(x =>
			{
				var value = function(x);
				return value.IsScalar ? value.ScalarValue
					: throw new RuntimeError("feval: the function must return a scalar.", span);
			});
		}, "feval");

		// integral("x", "x^2", a, b)
		Add(call =>
		{
			call.RequireCount(4);
			return Calculus.Integrate(call.RealFunction(0, 1), call.Real(2), call.Real(3));
		}, "integral");

		// derivata("x", "x^3", x0)
		Add(call =>
		{
			call.RequireCount(3);
			return Calculus.Derivative(call.RealFunction(0, 1), call.Real(2));
		}, "derivata", "derivative");

		// solve("x", "x^2-2", x0): the root closest to x0
		Add(call =>
		{
			call.RequireCount(3);
			return RootFinding.FindRootNear(call.RealFunction(0, 1), call.Real(2));
		}, "solve");

		// isect("x", "f", "g", x0): where f(x) = g(x), closest to x0
		Add(call =>
		{
			call.RequireCount(4);
			return RootFinding.FindIntersection(call.RealFunction(0, 1), call.RealFunction(0, 2), call.Real(3));
		}, "isect");

		// extreme("x", "f", x0): a local maximum or minimum of f closest to x0
		Add(call =>
		{
			call.RequireCount(3);
			return RootFinding.FindExtremum(call.RealFunction(0, 1), call.Real(2));
		}, "extreme");
	}

	// ───────────── Plotting ─────────────

	void RegisterPlotCommands()
	{
		Add(call =>
		{
			call.RequireCount(2);
			call.Figure.Initialize(call.Integer(0, min: 1), call.Integer(1, min: 1));
			return null;
		}, "initdisplay");
		Add(call =>
		{
			call.RequireCount(2);
			call.Figure.SelectAxes(call.Integer(0, min: 1), call.Integer(1, min: 1));
			return null;
		}, "currentwin");
		Add(call =>
		{
			call.RequireCount(2);
			call.Figure.SetXRange(call.Real(0), call.Real(1));
			return null;
		}, "xrange");
		Add(call =>
		{
			call.RequireCount(2);
			call.Figure.SetYRange(call.Real(0), call.Real(1));
			return null;
		}, "yrange");
		Add(call =>
		{
			call.RequireCount(1);
			call.Figure.SetPen(call.Integer(0, min: 0));
			return null;
		}, "pen");
		Add(call =>
		{
			call.RequireCount(0);
			call.Figure.ClearCurrent();
			return null;
		}, "clear", "clf");
		Add(call =>
		{
			call.RequireCount(0, 1);
			call.Figure.ShowGrid(call.Count == 0 || call.Real(0) != 0);
			return null;
		}, "grid");
		Add(call => SetText(call, call.Figure.SetTitle), "title");
		Add(call => SetText(call, call.Figure.SetXTitle), "xtitle", "xlabel");
		Add(call => SetText(call, call.Figure.SetYTitle), "ytitle", "ylabel");
		Add(call =>
		{
			call.RequireCount(2);
			call.Figure.AddDot(call.Real(0), call.Real(1));
			return null;
		}, "dot");
		Add(Plot, "plot");
		Add(call =>
		{
			call.RequireCount(1, 2);
			var z = call.Matrix(0);
			call.Figure.AddComplexSeries(z.AsSpan().ToArray().Select(v => new PlotPoint(v.Real, v.Imaginary)), call.Style(1));
			return null;
		}, "cplot");
		Add(call =>
		{
			call.RequireCount(2);
			var function = PlotFunction(call.RealFunction(0, 1));
			call.Figure.AddFunction(call.String(1), function);
			return null;
		}, "fplot", "tplot");
		Add(call =>
		{
			call.RequireCount(1);
			var coefficients = call.Matrix(0);
			if (!coefficients.IsVector)
				throw call.Error("the coefficients must be a vector.", 0);
			var c = coefficients.AsSpan().ToArray();
			call.Figure.AddFunction(PolynomialLabel(c), x => Polynomial.Evaluate(c, x).Real);
			return null;
		}, "polyplot");
		Add(call =>
		{
			call.Output.WriteLine("print: printing is not supported in this version; use savefig(\"plot.svg\").");
			return null;
		}, "print");

		// savefig("plot.svg") or savefig("plot.png", width, height); formats beyond SVG come from the front-end.
		Add(call =>
		{
			call.RequireCount(1, 3);
			string path = call.String(0);
			string extension = Path.GetExtension(path);
			MLCalcInterpreter.FigureSaver? saver = null;
			if (!extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) && !call.Interpreter.TryGetFigureFormat(extension, out saver))
				throw call.Error($"unsupported file type '{extension}'; use {string.Join(" or ", call.Interpreter.FigureFormats)}.", 0);
			double width = call.Count > 1 ? call.Real(1) : 800;
			double height = call.Count > 2 ? call.Real(2) : 600;
			if (width < 50 || height < 50 || width > 16000 || height > 16000)
				throw call.Error("the size must be between 50 and 16000.", 1);
			string fullPath = call.Interpreter.ResolvePath(path);
			try
			{
				var snapshot = call.Figure.Snapshot();
				if (saver is null)
					SvgWriter.Save(snapshot, fullPath, width, height);
				else
					saver(snapshot, fullPath, width, height);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				throw call.Error(ex.Message, 0);
			}
			call.Output.WriteLine($"Saved {fullPath}");
			return null;
		}, "savefig");
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
	static ComplexMatrix? Plot(CallContext call)
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
			style = call.Style(2);
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
			call.Figure.AddSeries(xs.Zip(ys, (a, b) => new PlotPoint(a, b)), style);
		return null;
	}

	/// <summary>Plots the real part; points where the function is undefined (a math error) become gaps.</summary>
	static Func<double, double> PlotFunction(Func<double, double> function) => x =>
	{
		try
		{
			return function(x);
		}
		catch (RuntimeError error) when (IsMathError(error))
		{
			return double.NaN;
		}
	};

	static bool IsMathError(Exception error)
	{
		for (Exception? e = error; e is not null; e = e.InnerException)
			if (e is MathException)
				return true;
		return false;
	}

	static string PolynomialLabel(Complex[] coefficients) =>
		string.Join(" + ", coefficients.Select((c, k) => k switch
		{
			0 => ComplexMatrix.Format(c, "G4"),
			1 => ComplexMatrix.Format(c, "G4") + "·x",
			_ => ComplexMatrix.Format(c, "G4") + "·x^" + k.ToString(CultureInfo.InvariantCulture),
		}));

	// ───────────── Console and variables ─────────────

	void RegisterConsoleCommands()
	{
		Add(call =>
		{
			call.RequireCount(0);
			var variables = call.Interpreter.Variables;
			if (variables.Count == 0)
			{
				call.Output.WriteLine("No variables.");
				return null;
			}
			foreach (var (name, value) in variables.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
				call.Output.WriteLine($"{name}\t{value.Rows}×{value.Columns}" + (value.IsReal ? "" : "\tcomplex"));
			return null;
		}, "list", "who");
		Add(call =>
		{
			call.RequireCount(1, int.MaxValue);
			for (int i = 0; i < call.Count; i++)
			{
				string name = call.NameArgument(i);
				if (name.Equals("all", StringComparison.OrdinalIgnoreCase))
				{
					call.Interpreter.ClearVariables();
					call.Output.WriteLine("All variables are deleted.");
				}
				else if (call.Interpreter.DeleteVariable(name))
					call.Output.WriteLine($"{name} is deleted.");
				else
					call.Output.WriteLine($"{name} is not a variable.");
			}
			return null;
		}, "del");
		Add(call =>
		{
			call.RequireCount(0);
			call.Output.Clear();
			return null;
		}, "cls", "clc");
		Add(call =>
		{
			call.RequireCount(1);
			if (call.Arguments[0] is Language.StringLiteral s)
				call.Output.WriteLine(s.Value);
			else
				call.Output.WriteLine(ResultFormatter.FormatValue(call.Matrix(0)));
			return null;
		}, "disp");
		Add(call =>
		{
			call.RequireCount(1);
			call.Interpreter.RunNestedScript(call.String(0), call.Span);
			return null;
		}, "run");
	}
}
