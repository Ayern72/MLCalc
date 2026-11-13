using System.Numerics;
using MLCalc.Math;

namespace MLCalc.LinearAlgebra.Tests;

public class SolverAndPolynomialTests
{
	[Fact]
	public void SolvesSystemThatNeedsPivoting()
	{
		// A zero on the diagonal forces a row swap.
		var a = ComplexMatrix.FromRows(new double[,] { { 0, 2, 1 }, { 1, 1, 1 }, { 2, 1, 0 } });
		var expected = ComplexMatrix.ColumnVector(1, 2, 3);
		var b = a * expected;
		Assert.True(LinearSolver.Solve(a, b).ApproximatelyEquals(expected, 1e-12));
	}

	[Fact]
	public void SolvesComplexSystem()
	{
		var a = ComplexMatrix.FromRows(new Complex[,] { { new(1, 1), 2 }, { new(0, -1), new(3, 2) } });
		var expected = ComplexMatrix.Create(2, 1, [new Complex(1, -1), new Complex(0.5, 2)]);
		Assert.True(LinearSolver.Solve(a, a * expected).ApproximatelyEquals(expected, 1e-12));
	}

	[Fact]
	public void SolvesForSeveralRightHandSides()
	{
		var a = ComplexMatrix.FromRows(new double[,] { { 4, 1 }, { 2, 3 } });
		var x = ComplexMatrix.FromRows(new double[,] { { 1, -1 }, { 2, 5 } });
		Assert.True(LinearSolver.Solve(a, a * x).ApproximatelyEquals(x, 1e-12));
	}

	[Fact]
	public void SingularMatrixThrows()
	{
		var a = ComplexMatrix.FromRows(new double[,] { { 1, 2 }, { 2, 4 } });
		var ex = Assert.Throws<MathException>(() => LinearSolver.Solve(a, ComplexMatrix.ColumnVector(1, 2)));
		Assert.Equal(MathError.SingularMatrix, ex.Error);
		Assert.Equal(Complex.Zero, a.Determinant());
	}

	[Fact]
	public void NonSquareMatrixThrows()
	{
		var a = ComplexMatrix.FromRows(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } });
		Assert.Equal(MathError.Dimension, Assert.Throws<MathException>(() => LinearSolver.Solve(a, ComplexMatrix.ColumnVector(1, 2))).Error);
	}

	[Fact]
	public void PolynomialCoefficientsAreAscending()
	{
		// 1 + 2x + 3x² at x = 0, 1, 2
		var p = ComplexMatrix.RowVector(1, 2, 3);
		Assert.Equal(ComplexMatrix.RowVector(1, 6, 17), Polynomial.Evaluate(p, ComplexMatrix.RowVector(0, 1, 2)));
	}

	[Fact]
	public void FitRecoversExactPolynomial()
	{
		var x = ComplexMatrix.RowVector(-3, -1, 0, 2, 3, 7);
		var p = ComplexMatrix.RowVector(0.5, -1, 0.25);
		var y = Polynomial.Evaluate(p, x);
		Assert.True(Polynomial.Fit(x, y, 2).ApproximatelyEquals(p, 1e-10));
	}

	[Fact]
	public void FitMatchesKnownLeastSquaresLine()
	{
		// Points (0,1), (1,3), (2,4), (3,4): least-squares line y = 1.5 + x
		var fit = Polynomial.Fit(ComplexMatrix.RowVector(0, 1, 2, 3), ComplexMatrix.RowVector(1, 3, 4, 4), 1);
		Assert.True(fit.ApproximatelyEquals(ComplexMatrix.RowVector(1.5, 1), 1e-12));
	}

	[Fact]
	public void FitOfSampleDataFromTest5()
	{
		// Data from the original TEST5.M; degree 4 through 6 points.
		var x = ComplexMatrix.RowVector(-3, -1, 0, 2, 3, 7);
		var y = ComplexMatrix.RowVector(3.3, 4.5, 2, 1.5, 2, 5.2);
		var p = Polynomial.Fit(x, y, 4);
		Assert.Equal(5, p.Columns);
		Assert.True(p.IsReal);
		AssertLeastSquares(x, y, p);
	}

	/// <summary>
	/// The defining property of a least-squares fit: the residual y − p(x) is orthogonal to every
	/// column xʲ of the Vandermonde matrix (the normal equations). Unlike a bound on the residual,
	/// this fails for any other polynomial.
	/// </summary>
	static void AssertLeastSquares(ComplexMatrix x, ComplexMatrix y, ComplexMatrix p, double tolerance = 1e-9)
	{
		var xs = x.AsSpan().ToArray();
		var residual = (y - Polynomial.Evaluate(p, x)).AsSpan().ToArray();
		double yScale = y.AsSpan().ToArray().Max(Complex.Abs);
		for (int j = 0; j < p.Columns; j++)
		{
			Complex dot = Complex.Zero;
			double scale = 0;
			for (int i = 0; i < xs.Length; i++)
			{
				var power = Complex.Pow(xs[i], j);
				dot += Complex.Conjugate(power) * residual[i];
				scale += Complex.Abs(power) * yScale;
			}
			Assert.True(Complex.Abs(dot) <= tolerance * scale, $"Residual not orthogonal to x^{j}: {dot}");
		}
	}

	[Fact]
	public void FitFarFromZeroIsAccurate()
	{
		// x = 1000…1010: the normal equations of the original implementation were singular here.
		var x = ComplexMatrix.Create(1, 11, (_, k) => 1000 + k);
		var line = Polynomial.Fit(x, x.Map(v => 2 + 3 * v), 1);
		Assert.True(line.ApproximatelyEquals(ComplexMatrix.RowVector(2, 3), 1e-9), line.ToString());

		var y = x.Map(v => v * v * v);
		var cubic = Polynomial.Fit(x, y, 3);
		// The coefficients of x³ for x ≈ 1000 are very sensitive to rounding, but the fitted values are accurate.
		Assert.True(Polynomial.Evaluate(cubic, x).ApproximatelyEquals(y, 1e-9 * 1e9));
		Assert.Equal(1, cubic[3].Real, 6);
	}

	[Fact]
	public void HighDegreeFitThroughExactPoints()
	{
		var x = ComplexMatrix.Create(1, 10, (_, k) => k + 1);
		var y = x.Map(v => Complex.Pow(v, 9));
		var p = Polynomial.Fit(x, y, 9);
		var values = Polynomial.Evaluate(p, x);
		for (int i = 0; i < 10; i++)
			Assert.True(Complex.Abs(values[i] - y[i]) <= 1e-8 * Complex.Abs(y[i]), $"x = {i + 1}: {values[i]} vs {y[i]}");
	}

	[Fact]
	public void FitOfComplexData()
	{
		var x = ComplexMatrix.Create(1, 6, [new(0, 0), new(1, 1), new(2, -1), new(-1, 2), new(3, 0), new(0, -2)]);
		var p = ComplexMatrix.Create(1, 3, [new(1, -1), new(0.5, 2), new(-1, 0.25)]);
		var fit = Polynomial.Fit(x, Polynomial.Evaluate(p, x), 2);
		Assert.True(fit.ApproximatelyEquals(p, 1e-10), fit.ToString());
	}

	[Fact]
	public void FitWithTooFewDifferentXValuesIsRefused()
	{
		var ex = Assert.Throws<MathException>(() => Polynomial.Fit(ComplexMatrix.RowVector(1, 1, 1, 2), ComplexMatrix.RowVector(1, 2, 3, 4), 2));
		Assert.Equal(MathError.SingularMatrix, ex.Error);
	}

	[Fact]
	public void FitNeedsMorePointsThanDegree()
	{
		Assert.Throws<MathException>(() => Polynomial.Fit(ComplexMatrix.RowVector(1, 2), ComplexMatrix.RowVector(1, 2), 2));
	}
}
