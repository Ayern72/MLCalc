namespace MLCalc.Mathematics;

/// <summary>Error categories raised by the MLCalc libraries (replaces the original <c>Serror</c> codes).</summary>
public enum MathError
{
	/// <summary>Division by zero.</summary>
	DivisionByZero,
	/// <summary>A complex number where only real numbers are allowed.</summary>
	ComplexNotAllowed,
	/// <summary>A negative number where it is not allowed, e.g. a negative factorial.</summary>
	NegativeNotAllowed,
	/// <summary>An argument outside the function's domain.</summary>
	Domain,
	/// <summary>Matrices whose sizes do not fit the operation.</summary>
	Dimension,
	/// <summary>An index outside the matrix, or not a positive whole number.</summary>
	Index,
	/// <summary>A singular, or nearly singular, matrix.</summary>
	SingularMatrix,
	/// <summary>No solution was found, e.g. no root near the starting point.</summary>
	NoSolution,
	/// <summary>Two functions do not intersect near the starting point.</summary>
	NoIntersection,
	/// <summary>A result would be too large (too many matrix elements, too many range elements).</summary>
	TooLarge,
}

/// <summary>Thrown when a mathematical operation cannot be performed.</summary>
public class MathException(MathError error, string message) : Exception(message)
{
	/// <summary>The category of the error.</summary>
	public MathError Error { get; } = error;
}
