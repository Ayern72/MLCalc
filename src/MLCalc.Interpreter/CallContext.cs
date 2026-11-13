using System.Numerics;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Math;
using MLCalc.Plotting;

namespace MLCalc.Interpreter;

/// <summary>A built-in function or command. Commands return null; functions return a value.</summary>
internal delegate ComplexMatrix? Builtin(CallContext call);

/// <summary>
/// Arguments of a built-in call. Arguments are kept as syntax and evaluated on request, so that
/// functions can take strings (<c>fplot("x", "sin(x)")</c>), names (<c>del x</c>) or style symbols.
/// </summary>
internal sealed class CallContext(MLCalcInterpreter interpreter, string name, IReadOnlyList<Expression> arguments, SourceSpan span)
{
	public MLCalcInterpreter Interpreter { get; } = interpreter;
	public string Name { get; } = name;
	public IReadOnlyList<Expression> Arguments { get; } = arguments;
	public SourceSpan Span { get; } = span;
	public int Count => Arguments.Count;

	public Figure Figure => Interpreter.Figure;
	public IConsoleOutput Output => Interpreter.Output;

	public RuntimeError Error(string message, int? argument = null) =>
		new($"{Name}: {message}", argument is { } i && i < Count ? Arguments[i].Span : Span);

	public void RequireCount(int min, int? max = null)
	{
		int upper = max ?? min;
		if (Count >= min && Count <= upper)
			return;
		string expected = min == upper ? $"{min}" : upper == int.MaxValue ? $"at least {min}" : $"{min} to {upper}";
		throw Error($"expected {expected} argument{(upper == 1 ? "" : "s")}, got {Count}.");
	}

	public ComplexMatrix Matrix(int index) => Interpreter.Evaluate(Arguments[index]);

	public Complex Scalar(int index)
	{
		var value = Matrix(index);
		return value.IsScalar ? value.ScalarValue : throw Error($"argument {index + 1} must be a scalar.", index);
	}

	public double Real(int index)
	{
		var value = Scalar(index);
		return ComplexMath.IsReal(value) ? value.Real : throw Error($"argument {index + 1} must be real.", index);
	}

	public int Integer(int index, int min = int.MinValue)
	{
		double value = Real(index);
		if (value != System.Math.Floor(value) || value < min || value > int.MaxValue)
			throw Error(min == 0 ? $"argument {index + 1} must be a non-negative integer."
				: min == 1 ? $"argument {index + 1} must be a positive integer."
				: $"argument {index + 1} must be an integer.", index);
		return (int)value;
	}

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

	/// <summary>A plot style: <c>-</c> (line), <c>+</c> (crosses), <c>*</c> (stars), <c>.</c> (dots), bare or as a string.</summary>
	public PlotStyle Style(int index)
	{
		if (index >= Count)
			return PlotStyle.Line;
		string symbol = Arguments[index] switch
		{
			StyleLiteral s => s.Symbol,
			StringLiteral s => s.Value,
			_ => throw Error($"argument {index + 1} must be a plot style: -, +, * or \".\".", index),
		};
		return symbol switch
		{
			"-" => PlotStyle.Line,
			"+" => PlotStyle.Cross,
			"*" => PlotStyle.Star,
			"." or "!" => PlotStyle.Dot,
			_ => throw Error($"unknown plot style '{symbol}'; use -, +, * or \".\".", index),
		};
	}

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
}
