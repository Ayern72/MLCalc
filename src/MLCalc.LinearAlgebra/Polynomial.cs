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
	/// <remarks>
	/// Fitting powers of x directly is badly conditioned when x is far from 0 (x = 1000…1010) or the
	/// degree is high, and the normal equations VᴴV·c = Vᴴy square that condition number. So x is
	/// first centred and scaled to u = (x − m)/s, which lies in [−1, 1]; the least-squares problem in
	/// u is solved with a QR factorisation (modified Gram–Schmidt); and the result is converted back
	/// to coefficients of powers of x.
	/// </remarks>
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

		int n = x.Count, terms = degree + 1;
		var xs = x.AsSpan().ToArray();

		// Centre and scale: u = (x − m)/s with |u| ≤ 1.
		Complex m = Complex.Zero;
		foreach (var value in xs)
			m += value;
		m /= n;
		double s = xs.Max(value => Complex.Abs(value - m));
		if (s == 0 || !double.IsFinite(s))
			s = 1;

		// Columns of the Vandermonde matrix in u, and the right-hand side.
		var columns = new Complex[terms][];
		for (int j = 0; j < terms; j++)
		{
			columns[j] = new Complex[n];
			for (int i = 0; i < n; i++)
				columns[j][i] = j == 0 ? Complex.One : columns[j - 1][i] * ((xs[i] - m) / s);
		}
		var rhs = y.AsSpan().ToArray();
		var q = SolveLeastSquares(columns, rhs, degree);

		// Back to powers of x: p(x) = Σ q_k·((x − m)/s)^k, expanded with Horner's scheme.
		var p = new Complex[terms];
		p[0] = q[degree];
		for (int k = degree - 1; k >= 0; k--)
		{
			// p ← p·(x − m)/s + q_k
			for (int j = terms - 1; j >= 0; j--)
				p[j] = ((j > 0 ? p[j - 1] : Complex.Zero) - m * p[j]) / s;
			p[0] += q[k];
		}
		return ComplexMatrix.Create(1, terms, p);
	}

	/// <summary>
	/// Least-squares solution of V·c ≈ rhs by modified Gram–Schmidt QR on the columns of V (which are
	/// overwritten), applied to the right-hand side at the same time, then back substitution.
	/// </summary>
	static Complex[] SolveLeastSquares(Complex[][] columns, Complex[] rhs, int degree)
	{
		int terms = columns.Length, n = rhs.Length;
		var r = new Complex[terms, terms];
		var z = new Complex[terms];
		double largest = columns.Max(Norm);

		for (int j = 0; j < terms; j++)
		{
			double norm = Norm(columns[j]);
			if (norm <= 1e-12 * largest)
				throw new MathException(MathError.SingularMatrix,
					$"A degree {degree} polynomial is not determined by these points (too few different x values).");
			r[j, j] = norm;
			for (int i = 0; i < n; i++)
				columns[j][i] /= norm;
			for (int k = j + 1; k < terms; k++)
			{
				r[j, k] = Dot(columns[j], columns[k]);
				for (int i = 0; i < n; i++)
					columns[k][i] -= r[j, k] * columns[j][i];
			}
			z[j] = Dot(columns[j], rhs);
			for (int i = 0; i < n; i++)
				rhs[i] -= z[j] * columns[j][i];
		}

		var c = new Complex[terms];
		for (int j = terms - 1; j >= 0; j--)
		{
			var sum = z[j];
			for (int k = j + 1; k < terms; k++)
				sum -= r[j, k] * c[k];
			c[j] = sum / r[j, j];
		}
		return c;
	}

	/// <summary>The inner product qᴴ·v.</summary>
	static Complex Dot(Complex[] q, Complex[] v)
	{
		Complex sum = Complex.Zero;
		for (int i = 0; i < q.Length; i++)
			sum += Complex.Conjugate(q[i]) * v[i];
		return sum;
	}

	static double Norm(Complex[] v)
	{
		double sum = 0;
		foreach (var value in v)
			sum += value.Real * value.Real + value.Imaginary * value.Imaginary;
		return System.Math.Sqrt(sum);
	}

	static void RequireVector(ComplexMatrix m, string name)
	{
		if (!m.IsVector)
			throw new MathException(MathError.Dimension, $"{name} must be a vector, got a {m.Rows}×{m.Columns} matrix.");
	}
}
