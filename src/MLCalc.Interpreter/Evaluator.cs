using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Math;

namespace MLCalc.Interpreter;

public sealed partial class MLCalcInterpreter
{
	/// <summary>Constants; a variable with the same name (e.g. <c>i</c> as a loop counter) takes precedence.</summary>
	internal static readonly IReadOnlyDictionary<string, ComplexMatrix> Constants = new Dictionary<string, ComplexMatrix>(StringComparer.OrdinalIgnoreCase)
	{
		["pi"] = System.Math.PI,
		["i"] = Complex.ImaginaryOne,
		["j"] = Complex.ImaginaryOne,
		["eps"] = double.BitIncrement(1.0) - 1.0,
		["inf"] = double.PositiveInfinity,
		["nan"] = double.NaN,
	};

	/// <summary>Evaluates an expression that must produce a value.</summary>
	internal ComplexMatrix Evaluate(Expression expression)
	{
		// The parser limits nesting, but check the stack anyway: running out of it would end the
		// process, while this throws an exception that is reported as an error.
		RuntimeHelpers.EnsureSufficientExecutionStack();
		return EvaluateStatement(expression) ?? throw NotAValue(expression);
	}

	internal static RuntimeError NotAValue(Expression expression)
	{
		// Name the command rather than printing the (possibly simplified) syntax tree.
		string name = expression switch
		{
			Identifier id => id.Name,
			ApplyExpression apply => apply.Target.Name,
			_ => SyntaxPrinter.Print(expression),
		};
		return new($"'{name}' is a command and does not return a value.", expression.Span);
	}

	/// <summary>Evaluates an expression or command; commands return null.</summary>
	ComplexMatrix? EvaluateStatement(Expression expression)
	{
		switch (expression)
		{
			case FoldedConstant constant:
				return constant.Value;
			case CompiledExpression compiled:
				return compiled.Code(compiledArgument!);
			case Identifier id:
				if (TryLookup(id.Name, out var value))
					return value;
				return Call(id, [], id.Span);
			case ApplyExpression apply:
				if (TryLookupVariable(apply.Target.Name, out var variable))
				{
					var indices = apply.Arguments.Select(EvaluateIndex).ToArray();
					return Guard(apply.Span, () => Indexing.Get(variable, indices));
				}
				return Call(apply.Target, apply.Arguments, apply.Span);
			default:
				return EvaluateValue(expression);
		}
	}

	internal bool TryLookupVariable(string name, out ComplexMatrix value)
	{
		if (locals is not null && locals.TryGetValue(name, out value!))
			return true;
		return variables.TryGetValue(name, out value!);
	}

	internal bool TryLookup(string name, out ComplexMatrix value) =>
		TryLookupVariable(name, out value) || Constants.TryGetValue(name, out value!);

	ComplexMatrix? Call(Identifier name, IReadOnlyList<Expression> arguments, SourceSpan span)
	{
		if (!builtins.TryGet(name.Name, out var builtin))
			throw new RuntimeError($"Unknown function or variable '{name.Name}'.", name.Span);
		return CallBuiltin(builtin, name.Name, arguments, span);
	}

	/// <summary>Calls a built-in that has already been looked up; math errors are reported at <paramref name="span"/>.</summary>
	internal ComplexMatrix? CallBuiltin(Builtin builtin, string name, IReadOnlyList<Expression> arguments, SourceSpan span)
	{
		var context = new CallContext(this, name, arguments, span);
		try
		{
			return builtin(context);
		}
		catch (Exception ex) when (ToRuntimeError(ex, span) is { } error)
		{
			throw error;
		}
	}

	ComplexMatrix EvaluateValue(Expression expression)
	{
		switch (expression)
		{
			case NumberLiteral n:
				return n.IsImaginary ? new Complex(0, n.Value) : n.Value;
			case StringLiteral s:
				throw new RuntimeError("Strings can only be used as arguments to functions such as fplot or title.", s.Span);
			case ColonAll c:
				throw new RuntimeError("':' can only be used as an index, as in t(1,:).", c.Span);
			case StyleLiteral s:
				throw new RuntimeError($"'{s.Symbol}' can only be used as a plot style argument.", s.Span);
			case UnaryExpression u:
				return Operators.Unary(u.Operator, Evaluate(u.Operand));
			case BinaryExpression b:
			{
				var left = Evaluate(b.Left);
				var right = Evaluate(b.Right);
				return Operators.Binary(b.Operator, left, right, b.Span);
			}
			case PostfixExpression p:
				return Operators.Postfix(p.Operator, Evaluate(p.Operand), p.Span);
			case RangeExpression r:
				return MakeRange(r, Evaluate(r.Start), r.Step is null ? null : Evaluate(r.Step), Evaluate(r.End));
			case MatrixLiteral m:
				return Concatenate(m, m.Rows.Select(row => row.Select(Evaluate).ToList()).ToList());
			default:
				return EvaluateStatement(expression)
					?? throw new RuntimeError("This expression does not return a value.", expression.Span);
		}
	}

	/// <summary>Builds the rows of a matrix literal from its evaluated elements.</summary>
	internal static ComplexMatrix Concatenate(MatrixLiteral literal, List<List<ComplexMatrix>> rows) =>
		Guard(literal.Span, () => ComplexMatrix.VerticalConcat(rows.Select(ComplexMatrix.HorizontalConcat).ToList()));

	/// <summary>The value of a range <c>start:step:end</c> from its evaluated parts.</summary>
	internal static ComplexMatrix MakeRange(RangeExpression range, ComplexMatrix startValue, ComplexMatrix? stepValue, ComplexMatrix endValue)
	{
		double start = RealScalar(startValue, range.Start.Span, "The start of a range");
		double step = stepValue is null ? 1 : RealScalar(stepValue, range.Step!.Span, "The step of a range");
		double end = RealScalar(endValue, range.End.Span, "The end of a range");
		if (step == 0 || double.IsNaN(start + step + end))
			throw new RuntimeError("The step of a range must be a non-zero number.", range.Span);

		double count = System.Math.Floor((end - start) / step + 1e-10) + 1;
		if (count <= 0)
			return ComplexMatrix.Zeros(1, 0);
		if (!(count <= ComplexMatrix.MaxElements))
			throw new RuntimeError(string.Create(CultureInfo.InvariantCulture,
				$"The range would have {count:0} elements; the limit is {ComplexMatrix.MaxElements}."), range.Span);
		return ComplexMatrix.Create(1, (int)count, (_, k) => start + k * step);
	}

	IndexArgument EvaluateIndex(Expression expression) =>
		expression is ColonAll ? IndexArgument.All : new IndexArgument(Evaluate(expression), expression.Span);

	/// <summary>
	/// Compiles a function given as text, such as <c>fplot("x", "sin(x)")</c>, into a delegate.
	/// Calling it evaluates the expression with <paramref name="variable"/> bound to the argument,
	/// leaving the global variables untouched. Branches that do not depend on
	/// <paramref name="variable"/> are evaluated once (see <see cref="ConstantFolder"/>), and the
	/// rest is turned into delegates (see <see cref="FunctionCompiler"/>).
	/// </summary>
	internal Func<ComplexMatrix, ComplexMatrix> CompileFunction(string variable, string text, SourceSpan span)
	{
		var parse = Parser.Parse(text);
		if (parse.HasErrors)
			throw new RuntimeError($"In \"{text}\": {parse.Diagnostics[0].Message}", span);
		if (parse.Root.Statements is not [ExpressionStatement { Expression: var parsed }])
			throw new RuntimeError($"\"{text}\" must be a single expression.", span);

		var code = new FunctionCompiler(this, builtins, variable).Compile(FoldConstants(parsed, variable));

		return argument =>
		{
			// Built-in functions called from the compiled code read their arguments back through
			// CompiledExpression nodes, which need the current value of the variable.
			var previous = compiledArgument;
			compiledArgument = argument;
			try
			{
				cancellation.ThrowIfCancellationRequested();
				return code(argument);
			}
			catch (RuntimeError error)
			{
				throw new RuntimeError($"In \"{text}\": {error.Message}", span, error);
			}
			finally
			{
				compiledArgument = previous;
			}
		};
	}

	/// <summary>The argument of the compiled function currently being evaluated.</summary>
	ComplexMatrix? compiledArgument;

	/// <summary>
	/// Folds constant branches of a function's expression. It runs with no local variables bound,
	/// so constants are computed from the same variables the function sees when it is evaluated.
	/// </summary>
	internal Expression FoldConstants(Expression expression, string variable)
	{
		var previous = locals;
		locals = new Dictionary<string, ComplexMatrix>(StringComparer.OrdinalIgnoreCase);
		try
		{
			return new ConstantFolder(this, builtins, variable).Fold(expression);
		}
		finally
		{
			locals = previous;
		}
	}
}

/// <summary>An evaluated index: a matrix of one-based indices, or <c>:</c> for all.</summary>
internal readonly record struct IndexArgument(ComplexMatrix? Values, SourceSpan Span)
{
	public static IndexArgument All => new(null, default);
	public bool IsAll => Values is null;
}

/// <summary>
/// The operators, with MATLAB meanings. Shared by the tree-walking evaluator and the compiled
/// functions so both give the same results and the same errors.
/// </summary>
internal static class Operators
{
	public static ComplexMatrix Unary(UnaryOperator op, ComplexMatrix operand) => op switch
	{
		UnaryOperator.Negate => -operand,
		UnaryOperator.Not => operand.Map(x => x == Complex.Zero ? 1 : 0),
		_ => operand,
	};

	/// <summary>A binary operation; math errors are reported at <paramref name="span"/>.</summary>
	public static ComplexMatrix Binary(BinaryOperator op, ComplexMatrix a, ComplexMatrix b, SourceSpan span)
	{
		try
		{
			return Binary(op, a, b);
		}
		catch (Exception ex) when (MLCalcInterpreter.ToRuntimeError(ex, span) is { } error)
		{
			throw error;
		}
	}

	/// <summary>A postfix operation; math errors are reported at <paramref name="span"/>.</summary>
	public static ComplexMatrix Postfix(PostfixOperator op, ComplexMatrix operand, SourceSpan span)
	{
		try
		{
			return op switch
			{
				PostfixOperator.ConjugateTranspose => operand.ConjugateTranspose(),
				PostfixOperator.Transpose => operand.Transpose(),
				_ => operand.Map(x => ComplexMath.IsReal(x)
					? RealMath.Factorial(x.Real)
					: throw new MathException(MathError.ComplexNotAllowed, "Factorial of a complex number.")),
			};
		}
		catch (Exception ex) when (MLCalcInterpreter.ToRuntimeError(ex, span) is { } error)
		{
			throw error;
		}
	}

	public static ComplexMatrix Binary(BinaryOperator op, ComplexMatrix a, ComplexMatrix b) => op switch
	{
		BinaryOperator.Add => a + b,
		BinaryOperator.Subtract => a - b,
		BinaryOperator.Multiply => a * b,
		BinaryOperator.Divide => ComplexMatrix.RightDivide(a, b),
		BinaryOperator.LeftDivide => ComplexMatrix.LeftDivide(a, b),
		BinaryOperator.Power => ComplexMatrix.Power(a, b),
		BinaryOperator.ElementMultiply => a.ElementMultiply(b),
		BinaryOperator.ElementDivide => a.ElementDivide(b),
		BinaryOperator.ElementLeftDivide => b.ElementDivide(a),
		BinaryOperator.ElementPower => a.ElementPower(b),
		BinaryOperator.Modulo => Real(a, b, RealMath.Mod, "mod"),
		BinaryOperator.IntegerDivide => Real(a, b, RealMath.Div, "div"),
		BinaryOperator.Equal => Compare(a, b, (x, y) => x == y),
		BinaryOperator.NotEqual => Compare(a, b, (x, y) => x != y),
		BinaryOperator.Less => Compare(a, b, (x, y) => x.Real < y.Real),
		BinaryOperator.LessOrEqual => Compare(a, b, (x, y) => x.Real <= y.Real),
		BinaryOperator.Greater => Compare(a, b, (x, y) => x.Real > y.Real),
		BinaryOperator.GreaterOrEqual => Compare(a, b, (x, y) => x.Real >= y.Real),
		BinaryOperator.And => Compare(a, b, (x, y) => x != Complex.Zero && y != Complex.Zero),
		BinaryOperator.Or => Compare(a, b, (x, y) => x != Complex.Zero || y != Complex.Zero),
		BinaryOperator.Xor => Compare(a, b, (x, y) => (x != Complex.Zero) != (y != Complex.Zero)),
		_ => throw new NotSupportedException(op.ToString()),
	};

	static ComplexMatrix Compare(ComplexMatrix a, ComplexMatrix b, Func<Complex, Complex, bool> test) =>
		ComplexMatrix.ElementWise(a, b, (x, y) => test(x, y) ? 1 : 0);

	static ComplexMatrix Real(ComplexMatrix a, ComplexMatrix b, Func<double, double, double> f, string name) =>
		ComplexMatrix.ElementWise(a, b, (x, y) => ComplexMath.IsReal(x) && ComplexMath.IsReal(y)
			? f(x.Real, y.Real)
			: throw new MathException(MathError.ComplexNotAllowed, $"'{name}' requires real numbers."));
}
