using System.Numerics;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Mathematics;

namespace MLCalc.Interpreter;

/// <summary>A built-in function or command. Commands return null; functions return a value.</summary>
internal delegate ComplexMatrix? Builtin(CallContext call);

/// <summary>What kind of built-in a name refers to.</summary>
internal enum BuiltinKind
{
	/// <summary>Computes a value.</summary>
	Function,
	/// <summary>Computes a value from its arguments only, without side effects.</summary>
	PureFunction,
	/// <summary>Does something and gives no value.</summary>
	Command,
}

/// <summary>
/// A call of a function or command added with <see cref="MLCalcInterpreter.AddFunction"/> or
/// <see cref="MLCalcInterpreter.AddCommand"/>, and of the built-in ones. The arguments are kept as
/// syntax and evaluated on request, so that functions can take strings
/// (<c>fplot("x", "sin(x)")</c>), names (<c>del x</c>) or style symbols (<c>plot(x, y, +)</c>).
/// The methods that read an argument report a wrong argument as an error in the program.
/// </summary>
public sealed class CallContext
{
	internal CallContext(MLCalcInterpreter interpreter, string name, IReadOnlyList<Expression> arguments, SourceSpan span)
	{
		Interpreter = interpreter;
		Name = name;
		Arguments = arguments;
		Span = span;
	}

	/// <summary>The interpreter running the call.</summary>
	public MLCalcInterpreter Interpreter { get; }

	/// <summary>The name the function or command was called by.</summary>
	public string Name { get; }

	/// <summary>The arguments as written, not yet evaluated.</summary>
	public IReadOnlyList<Expression> Arguments { get; }

	/// <summary>Where the call is in the program, for error messages.</summary>
	public SourceSpan Span { get; }

	/// <summary>The number of arguments.</summary>
	public int Count => Arguments.Count;

	/// <summary>Where the program's values and messages go.</summary>
	public IConsoleOutput Output => Interpreter.Output;

	/// <summary>Cancelled when the program should stop; check it in long calculations.</summary>
	public CancellationToken Cancellation => Interpreter.Cancellation;

	/// <summary>
	/// An error in this call, to throw: the message is prefixed with the name, and points at
	/// argument <paramref name="argument"/> (zero-based) when given, otherwise at the whole call.
	/// </summary>
	public RuntimeError Error(string message, int? argument = null) =>
		new($"{Name}: {message}", argument is { } i && i < Count ? Arguments[i].Span : Span);

	/// <summary>
	/// Checks the number of arguments: exactly <paramref name="min"/>, or from <paramref name="min"/>
	/// to <paramref name="max"/> (<see cref="int.MaxValue"/> for no upper limit).
	/// </summary>
	public void RequireCount(int min, int? max = null)
	{
		int upper = max ?? min;
		if (Count >= min && Count <= upper)
			return;
		string expected = min == upper ? $"{min}" : upper == int.MaxValue ? $"at least {min}" : $"{min} to {upper}";
		throw Error($"expected {expected} argument{(upper == 1 ? "" : "s")}, got {Count}.");
	}

	/// <summary>Evaluates argument <paramref name="index"/> (zero-based).</summary>
	public ComplexMatrix Matrix(int index) => Interpreter.Evaluate(Arguments[index]);

	/// <summary>Evaluates an argument that must be a scalar.</summary>
	public Complex Scalar(int index)
	{
		var value = Matrix(index);
		return value.IsScalar ? value.ScalarValue : throw Error($"argument {index + 1} must be a scalar.", index);
	}

	/// <summary>Evaluates an argument that must be a real scalar.</summary>
	public double Real(int index)
	{
		var value = Scalar(index);
		return ComplexMath.IsReal(value) ? value.Real : throw Error($"argument {index + 1} must be real.", index);
	}

	/// <summary>Evaluates an argument that must be a whole number of at least <paramref name="min"/>.</summary>
	public int Integer(int index, int min = int.MinValue)
	{
		double value = Real(index);
		if (value != Math.Floor(value) || value < min || value > int.MaxValue)
			throw Error(min == 0 ? $"argument {index + 1} must be a non-negative integer."
				: min == 1 ? $"argument {index + 1} must be a positive integer."
				: $"argument {index + 1} must be an integer.", index);
		return (int)value;
	}

	/// <summary>An argument that must be a string in double quotes, e.g. <c>"sin(x)"</c>; strings are not values.</summary>
	public string String(int index) => Arguments[index] switch
	{
		StringLiteral s => s.Value,
		_ => throw Error($"argument {index + 1} must be a string in double quotes.", index),
	};

	/// <summary>A name given as a bare identifier or a string, e.g. <c>del(x)</c> or <c>del("x")</c>.</summary>
	public string NameArgument(int index) => Arguments[index] switch
	{
		Identifier id => id.Name,
		StringLiteral s => s.Value,
		_ => throw Error($"argument {index + 1} must be a name.", index),
	};

	/// <summary>
	/// A function of one variable given as two strings: the variable name and an expression,
	/// e.g. <c>"x", "sin(x)"</c>.
	/// </summary>
	public Func<ComplexMatrix, ComplexMatrix> Function(int variableIndex, int expressionIndex) =>
		Interpreter.CompileFunction(String(variableIndex), String(expressionIndex), Arguments[expressionIndex].Span);

	/// <summary>A real function for numerics: the result must be a scalar; its real part is used.</summary>
	public Func<double, double> RealFunction(int variableIndex, int expressionIndex)
	{
		var function = Function(variableIndex, expressionIndex);
		var span = Arguments[expressionIndex].Span;
		return x =>
		{
			var value = function(x);
			if (!value.IsScalar)
				throw new RuntimeError($"{Name}: the function must return a scalar, got a {value.Rows}×{value.Columns} matrix.", span);
			return value.ScalarValue.Real;
		};
	}

	/// <summary>
	/// A real function of two variables given as three strings, e.g. <c>"x", "y", "sin(x*y)"</c>. The
	/// result must be a scalar; its real part is used.
	/// </summary>
	public Func<double, double, double> RealFunction2(int xIndex, int yIndex, int expressionIndex)
	{
		string x = String(xIndex), y = String(yIndex);
		if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
			throw Error($"the two variables must have different names, not both '{x}'.", yIndex);
		var function = Interpreter.CompileFunction([x, y], String(expressionIndex), Arguments[expressionIndex].Span);
		var span = Arguments[expressionIndex].Span;
		return (a, b) =>
		{
			var value = function([a, b]);
			if (!value.IsScalar)
				throw new RuntimeError($"{Name}: the function must return a scalar, got a {value.Rows}×{value.Columns} matrix.", span);
			return value.ScalarValue.Real;
		};
	}
}
