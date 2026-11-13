using MLCalc.Language;
using MLCalc.LinearAlgebra;

namespace MLCalc.Interpreter;

/// <summary>Where the interpreter writes results and messages.</summary>
public interface IConsoleOutput
{
	/// <summary>A value to display, e.g. after <c>x = 3</c> or <c>1 + 2</c> (name "ans").</summary>
	void WriteValue(string name, ComplexMatrix value);

	/// <summary>Plain text, e.g. from <c>list</c> or <c>disp</c>.</summary>
	void WriteLine(string text);

	/// <summary>Clears the console (<c>cls</c>).</summary>
	void Clear();
}

/// <summary>An error raised while running a program, with the location of the code that caused it.</summary>
public sealed class RuntimeError(string message, SourceSpan span, Exception? inner = null) : Exception(message, inner)
{
	public SourceSpan Span { get; } = span;
}

/// <summary>The outcome of <see cref="MLCalcInterpreter.Execute(string, CancellationToken)"/>.</summary>
/// <param name="Parse">The parse result; when it has errors, nothing was executed.</param>
/// <param name="Error">The runtime error that stopped execution, if any.</param>
/// <param name="Cancelled">True when execution was stopped through the cancellation token.</param>
public sealed record ExecutionResult(ParseResult Parse, RuntimeError? Error, bool Cancelled = false)
{
	public bool Success => !Parse.HasErrors && Error is null && !Cancelled;

	/// <summary>All error messages with one-based line:column positions.</summary>
	public IEnumerable<string> Messages()
	{
		foreach (var diagnostic in Parse.Diagnostics)
			yield return $"{Parse.Text.GetLinePosition(diagnostic.Span.Start)}: {diagnostic.Message}";
		if (Error is not null)
			yield return $"{Parse.Text.GetLinePosition(Error.Span.Start)}: {Error.Message}";
		if (Cancelled)
			yield return "Stopped.";
	}
}
