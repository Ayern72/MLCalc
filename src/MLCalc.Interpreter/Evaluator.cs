using System.Numerics;
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
	internal ComplexMatrix Evaluate(Expression expression) =>
		EvaluateStatement(expression)
		?? throw new RuntimeError($"'{SyntaxPrinter.Print(expression)}' is a command and does not return a value.", expression.Span);

	/// <summary>Evaluates an expression or command; commands return null.</summary>
	ComplexMatrix? EvaluateStatement(Expression expression)
	{
		switch (expression)
		{
			case FoldedConstant constant:
				return constant.Value;
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
		var context = new CallContext(this, name.Name, arguments, span);
		return Guard(span, () => builtin(context));
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
			{
				var operand = Evaluate(u.Operand);
				return u.Operator switch
				{
					UnaryOperator.Negate => -operand,
					UnaryOperator.Not => operand.Map(x => x == Complex.Zero ? 1 : 0),
					_ => operand,
				};
			}
			case BinaryExpression b:
			{
				var left = Evaluate(b.Left);
				var right = Evaluate(b.Right);
				return Guard(b.Span, () => Operators.Binary(b.Operator, left, right));
			}
			case PostfixExpression p:
			{
				var operand = Evaluate(p.Operand);
				return Guard(p.Span, () => p.Operator switch
				{
					PostfixOperator.ConjugateTranspose => operand.ConjugateTranspose(),
					PostfixOperator.Transpose => operand.Transpose(),
					_ => operand.Map(x => ComplexMath.IsReal(x)
						? RealMath.Factorial(x.Real)
						: throw new MathException(MathError.ComplexNotAllowed, "Factorial of a complex number.")),
				});
			}
			case RangeExpression r:
				return EvaluateRange(r);
			case MatrixLiteral m:
				return Guard(m.Span, () => ComplexMatrix.VerticalConcat(
					m.Rows.Select(row => ComplexMatrix.HorizontalConcat(row.Select(Evaluate).ToList())).ToList()));
			default:
				return EvaluateStatement(expression)
					?? throw new RuntimeError("This expression does not return a value.", expression.Span);
		}
	}

	ComplexMatrix EvaluateRange(RangeExpression range)
	{
		double start = RealScalar(Evaluate(range.Start), range.Start.Span, "The start of a range");
		double step = range.Step is null ? 1 : RealScalar(Evaluate(range.Step), range.Step.Span, "The step of a range");
		double end = RealScalar(Evaluate(range.End), range.End.Span, "The end of a range");
		if (step == 0 || double.IsNaN(start + step + end))
			throw new RuntimeError("The step of a range must be a non-zero number.", range.Span);

		double count = System.Math.Floor((end - start) / step + 1e-10) + 1;
		if (count <= 0)
			return ComplexMatrix.Zeros(1, 0);
		if (count > 100_000_000)
			throw new RuntimeError("The range has too many elements.", range.Span);
		return ComplexMatrix.Create(1, (int)count, (_, k) => start + k * step);
	}

	IndexArgument EvaluateIndex(Expression expression) =>
		expression is ColonAll ? IndexArgument.All : new IndexArgument(Evaluate(expression), expression.Span);

	/// <summary>
	/// Compiles a function given as text, such as <c>fplot("x", "sin(x)")</c>: evaluating it binds
	/// <paramref name="variable"/> to the argument while leaving the global variables untouched.
	/// Branches that do not depend on <paramref name="variable"/> are evaluated once, here
	/// (see <see cref="ConstantFolder"/>).
	/// </summary>
	internal Func<ComplexMatrix, ComplexMatrix> CompileFunction(string variable, string text, SourceSpan span)
	{
		var parse = Parser.Parse(text);
		if (parse.HasErrors)
			throw new RuntimeError($"In \"{text}\": {parse.Diagnostics[0].Message}", span);
		if (parse.Root.Statements is not [ExpressionStatement { Expression: var parsed }])
			throw new RuntimeError($"\"{text}\" must be a single expression.", span);

		var expression = FoldConstants(parsed, variable);

		return argument =>
		{
			var previous = locals;
			locals = new Dictionary<string, ComplexMatrix>(StringComparer.OrdinalIgnoreCase) { [variable] = argument };
			try
			{
				cancellation.ThrowIfCancellationRequested();
				return Evaluate(expression);
			}
			catch (RuntimeError error)
			{
				throw new RuntimeError($"In \"{text}\": {error.Message}", span, error);
			}
			finally
			{
				locals = previous;
			}
		};
	}

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

/// <summary>The binary operators, with MATLAB meanings.</summary>
internal static class Operators
{
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
