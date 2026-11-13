using System.Collections.Immutable;
using MLCalc.Language;
using MLCalc.LinearAlgebra;

namespace MLCalc.Interpreter;

/// <summary>A sub-expression that has been evaluated in advance and replaced by its value.</summary>
internal sealed record FoldedConstant(SourceSpan Span, ComplexMatrix Value) : Expression(Span);

/// <summary>
/// Simplifies a function given as text, such as the <c>"sin(pi/4)*a*x^2"</c> in
/// <c>fplot("x", "sin(pi/4)*a*x^2")</c>, before it is evaluated many times. Every branch of the
/// syntax tree that does not depend on the function's variable is evaluated once and replaced by
/// its value, so <c>sin(pi/4)*a</c> above is computed once instead of at every point.
/// </summary>
/// <remarks>
/// <para>A branch is constant when it uses only numbers, constants, variables other than the
/// function's variable, and pure built-in functions (<see cref="Builtins.IsPure"/>). Other variables
/// can be treated as constants because an expression cannot assign to anything while it is being
/// evaluated. <c>rand</c>, commands and functions that take functions as text are never folded.</para>
/// <para>A constant branch whose evaluation fails (for example <c>1/0</c>) is left in place, so the
/// error still happens, with the same message and position, when the function is evaluated.</para>
/// <para>This is the parse-tree reduction (<c>Reduce_Tree</c>) planned in the original MLCalc.
/// Operands are not reordered, so results are identical to evaluating the original tree.</para>
/// </remarks>
internal sealed class ConstantFolder(MLCalcInterpreter interpreter, Builtins builtins, string variable)
{
	/// <summary>Returns the expression with every constant branch replaced by its value.</summary>
	public Expression Fold(Expression expression) => Visit(expression).Expression;

	/// <param name="Expression">The (possibly) simplified expression.</param>
	/// <param name="IsConstant">The expression is a <see cref="FoldedConstant"/>.</param>
	/// <param name="IsFixed">
	/// The expression is not a value but does not vary either: a string, a <c>:</c> or a plot style.
	/// A call whose arguments are all constant or fixed can still be folded.
	/// </param>
	readonly record struct Result(Expression Expression, bool IsConstant, bool IsFixed = false)
	{
		public bool IsInvariant => IsConstant || IsFixed;
	}

	Result Visit(Expression expression)
	{
		switch (expression)
		{
			case FoldedConstant:
				return new Result(expression, true);
			case NumberLiteral:
				return TryFold(expression);
			case StringLiteral or ColonAll or StyleLiteral:
				return new Result(expression, false, IsFixed: true);
			case Identifier id:
				return IsVariable(id.Name) || !interpreter.TryLookup(id.Name, out _)
					? new Result(expression, false) // the function's variable, a command, or an unknown name
					: TryFold(expression);
			case UnaryExpression u:
			{
				var operand = Visit(u.Operand);
				return Rebuild(u with { Operand = operand.Expression }, operand.IsConstant);
			}
			case BinaryExpression b:
			{
				var left = Visit(b.Left);
				var right = Visit(b.Right);
				return Rebuild(b with { Left = left.Expression, Right = right.Expression }, left.IsConstant && right.IsConstant);
			}
			case PostfixExpression p:
			{
				var operand = Visit(p.Operand);
				return Rebuild(p with { Operand = operand.Expression }, operand.IsConstant);
			}
			case RangeExpression r:
			{
				var start = Visit(r.Start);
				var step = r.Step is null ? (Result?)null : Visit(r.Step);
				var end = Visit(r.End);
				bool constant = start.IsConstant && end.IsConstant && (step?.IsConstant ?? true);
				return Rebuild(r with { Start = start.Expression, Step = step?.Expression, End = end.Expression }, constant);
			}
			case MatrixLiteral m:
			{
				bool constant = true;
				var rows = m.Rows.Select(row => row.Select(element =>
				{
					var result = Visit(element);
					constant &= result.IsConstant;
					return result.Expression;
				}).ToImmutableArray()).ToImmutableArray();
				return Rebuild(m with { Rows = rows }, constant);
			}
			case ApplyExpression a:
				return VisitApply(a);
			default:
				return new Result(expression, false);
		}
	}

	Result VisitApply(ApplyExpression apply)
	{
		string name = apply.Target.Name;
		bool indexing = !IsVariable(name) && interpreter.TryLookupVariable(name, out _);
		bool foldableCall = indexing || (!IsVariable(name) && builtins.IsPure(name));

		if (!foldableCall)
		{
			// Fold inside the arguments, but leave bare names alone: commands such as del(x)
			// read them as names rather than values.
			var arguments = apply.Arguments.Select(argument => argument is Identifier ? argument : Visit(argument).Expression).ToImmutableArray();
			return new Result(apply with { Arguments = arguments }, false);
		}

		bool invariant = true;
		var folded = apply.Arguments.Select(argument =>
		{
			var result = Visit(argument);
			invariant &= result.IsInvariant;
			return result.Expression;
		}).ToImmutableArray();
		return Rebuild(apply with { Arguments = folded }, invariant);
	}

	bool IsVariable(string name) => string.Equals(name, variable, StringComparison.OrdinalIgnoreCase);

	/// <summary>Evaluates a rebuilt node whose children are all constant; otherwise keeps it.</summary>
	Result Rebuild(Expression rebuilt, bool childrenConstant) =>
		childrenConstant ? TryFold(rebuilt) : new Result(rebuilt, false);

	/// <summary>
	/// Evaluates the expression now. If that fails, the expression is kept so the same error
	/// happens when the function is evaluated.
	/// </summary>
	Result TryFold(Expression expression)
	{
		try
		{
			return new Result(new FoldedConstant(expression.Span, interpreter.Evaluate(expression)), true);
		}
		catch (RuntimeError)
		{
			return new Result(expression, false);
		}
	}
}
