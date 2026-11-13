namespace MLCalc.Math;

/// <summary>Error categories raised by the MLCalc libraries (replaces the original <c>Serror</c> codes).</summary>
public enum MathError
{
	DivisionByZero,
	ComplexNotAllowed,
	NegativeNotAllowed,
	Domain,
	Dimension,
	Index,
	SingularMatrix,
	NoSolution,
	NoIntersection,
	/// <summary>A result would be too large (too many matrix elements, too many range elements).</summary>
	TooLarge,
}

/// <summary>Thrown when a mathematical operation cannot be performed.</summary>
public class MathException(MathError error, string message) : Exception(message)
{
	public MathError Error { get; } = error;
}
