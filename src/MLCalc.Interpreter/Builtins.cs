using System.Globalization;
using System.Numerics;
using MLCalc.LinearAlgebra;
using MLCalc.Mathematics;
using MLCalc.Language;
using MLCalc.Numerics;

namespace MLCalc.Interpreter;

/// <summary>The built-in functions and commands, looked up case-insensitively.</summary>
internal sealed class Builtins
{
	readonly Dictionary<string, Builtin> table = new(StringComparer.OrdinalIgnoreCase);
	readonly HashSet<string> pure = new(StringComparer.OrdinalIgnoreCase);
	readonly HashSet<string> commands = new(StringComparer.OrdinalIgnoreCase);
	bool registeringPure, registeringCommands;

	public IEnumerable<string> Names => table.Keys.Order(StringComparer.OrdinalIgnoreCase);

	public bool TryGet(string name, out Builtin builtin) => table.TryGetValue(name, out builtin!);

	/// <summary>
	/// True for functions whose result depends only on their arguments and that have no side
	/// effects, so a call with constant arguments can be evaluated once (see <see cref="ConstantFolder"/>).
	/// </summary>
	public bool IsPure(string name) => pure.Contains(name);

	/// <summary>
	/// True for commands: console and file commands such as <c>del</c> or <c>run</c>, and those
	/// the host adds, such as <c>plot</c>. They change state and are not allowed in functions given as text.
	/// </summary>
	public bool IsCommand(string name) => commands.Contains(name);

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
		registeringCommands = true;
		RegisterConsoleCommands();
		registeringCommands = false;
	}

	/// <summary>Adds a function or command for <see cref="MLCalcInterpreter.AddFunction"/> and <see cref="MLCalcInterpreter.AddCommand"/>.</summary>
	public void Define(string name, BuiltinKind kind, Builtin builtin)
	{
		ArgumentNullException.ThrowIfNull(name);
		var tokens = Lexer.Tokenize(name).Tokens;
		if (tokens.Count != 2 || tokens[0].Kind != TokenKind.Identifier || tokens[0].Text != name)
			throw new ArgumentException($"'{name}' is not a valid name: use letters, digits and underscores, starting with a letter, and not a keyword.", nameof(name));
		if (table.ContainsKey(name))
			throw new ArgumentException($"There is already a function or command named '{name}'.", nameof(name));
		table.Add(name, builtin);
		if (kind == BuiltinKind.PureFunction)
			pure.Add(name);
		if (kind == BuiltinKind.Command)
			commands.Add(name);
	}

	void Add(Builtin builtin, params string[] names)
	{
		foreach (var name in names)
		{
			table.Add(name, builtin);
			if (registeringPure)
				pure.Add(name);
			if (registeringCommands)
				commands.Add(name);
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
		Element(z => new Complex(Math.Round(z.Real, MidpointRounding.AwayFromZero), Math.Round(z.Imaginary, MidpointRounding.AwayFromZero)), "round");
		Element(z => new Complex(Math.Ceiling(z.Real), Math.Ceiling(z.Imaginary)), "ceil");
		Element(z => new Complex(Math.Floor(z.Real), Math.Floor(z.Imaginary)), "floor");
		Element(z => new Complex(Math.Truncate(z.Real), Math.Truncate(z.Imaginary)), "fix");
		Element(z => ComplexMath.IsReal(z) ? (double.IsNaN(z.Real) ? double.NaN : Math.Sign(z.Real)) : z / ComplexMath.Abs(z), "sign");
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
		Add(call => Dimension(call, m => m.IsEmpty ? 0 : Math.Max(m.Rows, m.Columns)), "length");
		Add(call => Dimension(call, m => m.Count), "numel");
		Add(call =>
		{
			call.RequireCount(1);
			return call.Matrix(0).Determinant(call.Cancellation);
		}, "det");
		Add(call =>
		{
			call.RequireCount(1);
			return call.Matrix(0).Inverse(call.Cancellation);
		}, "inv");
		Add(call =>
		{
			call.RequireCount(1);
			return call.Matrix(0).Transpose();
		}, "transpose");
		Add(call =>
		{
			call.RequireCount(2);
			return LinearSolver.Solve(call.Matrix(0), call.Matrix(1), call.Cancellation);
		}, "gauss");
		Add(call =>
		{
			call.RequireCount(3);
			return Polynomial.Fit(call.Matrix(0), call.Matrix(1), call.Integer(2, min: 0), call.Cancellation);
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

	/// <summary>
	/// Reduces a vector to a scalar, or each column of a matrix to a row. The empty matrix
	/// [] reduces to the seed: sum([]) = 0 and prod([]) = 1.
	/// </summary>
	static ComplexMatrix Reduce(CallContext call, Func<Complex, Complex, Complex> combine, Complex seed)
	{
		call.RequireCount(1);
		var m = call.Matrix(0);
		if (m.IsVector || (m.Rows == 0 && m.Columns == 0))
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
	/// <remarks>NaN values are ignored; the result is NaN only when all values are NaN.</remarks>
	static ComplexMatrix Extreme(CallContext call, Func<Complex, Complex, bool> better)
	{
		static bool IsNaN(Complex z) => double.IsNaN(z.Real) || double.IsNaN(z.Imaginary);
		Complex Choose(Complex a, Complex b) => IsNaN(a) ? b : IsNaN(b) ? a : better(b, a) ? b : a;

		call.RequireCount(1, 2);
		if (call.Count == 2)
			return ComplexMatrix.ElementWise(call.Matrix(0), call.Matrix(1), Choose);
		var m = call.Matrix(0);
		if (m.IsEmpty)
			return ComplexMatrix.Zeros(0, 0);
		Complex Pick(IEnumerable<Complex> values) => values.Aggregate(Choose);
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
