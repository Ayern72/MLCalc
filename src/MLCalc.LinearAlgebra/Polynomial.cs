using System.Numerics;
using MLCalc.Math;

namespace MLCalc.LinearAlgebra;

/// <summary>
/// Polynomials with coefficients in ascending order of power, as in the original MLCalc:
/// c = [c0, c1, c2] means c0 + c1·x + c2·x².
/// </summary>
public static class Polynomial
{
	/// <summary>Evaluates the polynomial at every element of <paramref name="x"/> (the original <c>polyval</c>).</summary>
	public static ComplexMatrix Evaluate(ComplexMatrix coefficients, ComplexMatrix x)
	{
		RequireVector(coefficients, "coefficients");
		var c = coefficients.AsSpan().ToArray();
		return x.Map(value => Evaluate(c, value));
	}

	/// <summary>Evaluates the polynomial at a single point using Horner's method.</summary>
	public static Complex Evaluate(ReadOnlySpan<Complex> coefficients, Complex x)
	{
		Complex result = Complex.Zero;
		for (int i = coefficients.Length - 1; i >= 0; i--)
			result = result * x + coefficients[i];
		return result;
	}

	/// <summary>
	/// Least-squares fit of a polynomial of the given degree to the points (x, y) (the original
	/// <c>polyfit</c>). Returns a row vector of degree + 1 coefficients in ascending order.
	/// </summary>
	public static ComplexMatrix Fit(ComplexMatrix x, ComplexMatrix y, int degree)
	{
		RequireVector(x, "x");
		RequireVector(y, "y");
		if (x.Count != y.Count)
			throw new MathException(MathError.Dimension, $"x and y must have the same number of elements ({x.Count} and {y.Count}).");
		if (degree < 0)
			throw new MathException(MathError.Domain, "The polynomial degree must be non-negative.");
		if (x.Count <= degree)
			throw new MathException(MathError.Domain, $"A degree {degree} fit needs more than {degree} points, got {x.Count}.");

		// Vandermonde matrix V with V[i, j] = x_i^j; solve the normal equations (Vᴴ·V)·c = Vᴴ·y.
		var v = ComplexMatrix.Create(x.Count, degree + 1, (i, j) => ComplexMath.Pow(x[i], j));
		var vh = v.ConjugateTranspose();
		var column = ComplexMatrix.Create(y.Count, 1, (i, _) => y[i]);
		return LinearSolver.Solve(vh * v, vh * column).Transpose();
	}

	static void RequireVector(ComplexMatrix m, string name)
	{
		if (!m.IsVector)
			throw new MathException(MathError.Dimension, $"{name} must be a vector, got a {m.Rows}×{m.Columns} matrix.");
	}
}
