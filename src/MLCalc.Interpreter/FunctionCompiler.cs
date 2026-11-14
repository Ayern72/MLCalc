using MLCalc.Language;
using MLCalc.LinearAlgebra;

namespace MLCalc.Interpreter;

/// <summary>
/// An argument of a built-in call inside a compiled function. The built-in evaluates it like any
/// other argument; evaluating it runs <see cref="Code"/> with the function's current argument.
/// </summary>
internal sealed record CompiledExpression(SourceSpan Span, Func<ComplexMatrix, ComplexMatrix> Code) : Expression(Span);

/// <summary>
/// Turns the expression of a function given as text (after <see cref="ConstantFolder"/>) into a
/// delegate, so evaluating it at many points does not walk the syntax tree each time. Each node
/// becomes a closure; names are resolved once, here, instead of at every evaluation.
/// </summary>
/// <remarks>
/// The closures call the same operator, range and matrix helpers as the tree-walking evaluator, and
/// built-in functions are called with the same arguments in the same way, so results and error
/// messages are identical. Parts that do not involve the function's variable (an unknown name,
/// <c>rand</c>, a command) are left to the interpreter.
/// </remarks>
internal sealed class FunctionCompiler(MLCalcInterpreter interpreter, Builtins builtins, string variable)
{
	public Func<ComplexMatrix, ComplexMatrix> Compile(Expression expression)
	{
		System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();
		switch (expression)
		{
			case FoldedConstant constant:
			{
				var value = constant.Value;
				return _ => value;
			}
			case Identifier id when IsVariable(id.Name):
				return x => x;
			case UnaryExpression unary:
			{
				var operand = Compile(unary.Operand);
				var op = unary.Operator;
				var span = unary.Span;
				return x => Operators.Unary(op, operand(x), span);
			}
			case BinaryExpression binary:
			{
				var left = Compile(binary.Left);
				var right = Compile(binary.Right);
				var op = binary.Operator;
				var span = binary.Span;
				return x =>
				{
					var a = left(x);
					var b = right(x);
					return Operators.Binary(op, a, b, span, interpreter.Cancellation);
				};
			}
			case PostfixExpression postfix:
			{
				var operand = Compile(postfix.Operand);
				var op = postfix.Operator;
				var span = postfix.Span;
				return x => Operators.Postfix(op, operand(x), span);
			}
			case RangeExpression range:
			{
				var start = Compile(range.Start);
				var step = range.Step is null ? null : Compile(range.Step);
				var end = Compile(range.End);
				return x => MLCalcInterpreter.MakeRange(range, start(x), step?.Invoke(x), end(x));
			}
			case MatrixLiteral matrix:
			{
				var rows = matrix.Rows.Select(row => row.Select(Compile).ToArray()).ToArray();
				return x => MLCalcInterpreter.Concatenate(matrix, rows.Select(row => row.Select(element => element(x)).ToList()).ToList());
			}
			case ApplyExpression apply:
				return CompileApply(apply);
			case Identifier id when builtins.IsCommand(id.Name) && !interpreter.TryLookup(id.Name, out _):
				throw CommandNotAllowed(id.Name, id.Span);
			default:
				// Names other than the variable (unknown names, rand, commands) and anything else
				// that does not use the variable: evaluate exactly as the interpreter does.
				return _ => interpreter.Evaluate(expression);
		}
	}

	Func<ComplexMatrix, ComplexMatrix> CompileApply(ApplyExpression apply)
	{
		string name = apply.Target.Name;
		var span = apply.Span;

		// Indexing the function's variable, or a global variable (which cannot change while the
		// function is being evaluated).
		if (IsVariable(name))
		{
			var indices = CompileIndices(apply.Arguments);
			return x => Index(x, indices, x, span);
		}
		if (interpreter.TryLookupVariable(name, out var variableValue))
		{
			var indices = CompileIndices(apply.Arguments);
			return x => Index(variableValue, indices, x, span);
		}

		// A call. The built-in evaluates its own arguments, as usual; arguments that use the
		// variable are passed as CompiledExpression nodes, built once here.
		var target = apply.Target;
		if (!builtins.TryGet(name, out var builtin))
			return _ => throw new RuntimeError($"Unknown function or variable '{name}'.", target.Span);
		if (builtins.IsCommand(name))
			throw CommandNotAllowed(name, target.Span);

		var arguments = apply.Arguments.Select(argument => argument switch
		{
			FoldedConstant or StringLiteral or ColonAll or StyleLiteral => argument,
			Identifier id when !IsVariable(id.Name) => argument, // passed as written, like any other call
			_ => new CompiledExpression(argument.Span, Compile(argument)),
		}).ToArray();
		return _ => interpreter.CallBuiltin(builtin, name, arguments, span) ?? throw MLCalcInterpreter.NotAValue(apply);
	}

	(Func<ComplexMatrix, ComplexMatrix>? Code, SourceSpan Span)[] CompileIndices(IEnumerable<Expression> arguments) =>
		arguments.Select(argument => argument is ColonAll ? (null, argument.Span) : (Compile(argument), argument.Span)).ToArray();

	static ComplexMatrix Index(ComplexMatrix source, (Func<ComplexMatrix, ComplexMatrix>? Code, SourceSpan Span)[] indices, ComplexMatrix x, SourceSpan span)
	{
		var evaluated = new IndexArgument[indices.Length];
		for (int i = 0; i < indices.Length; i++)
			evaluated[i] = indices[i].Code is { } code ? new IndexArgument(code(x), indices[i].Span) : IndexArgument.All;
		try
		{
			return Indexing.Get(source, evaluated);
		}
		catch (Exception ex) when (MLCalcInterpreter.ToRuntimeError(ex, span) is { } error)
		{
			throw error;
		}
	}

	bool IsVariable(string name) => string.Equals(name, variable, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Commands change state (variables, plots, files, the console). A function given as text is
	/// evaluated many times and must not do that, so commands are refused before it runs at all.
	/// </summary>
	static RuntimeError CommandNotAllowed(string name, SourceSpan span) =>
		new($"'{name}' is a command and cannot be used in a function given as text.", span);
}
