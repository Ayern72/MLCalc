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
		var residual = Polynomial.Evaluate(p, x) - y;
		double sumOfSquares = residual.AsSpan().ToArray().Sum(r => r.Real * r.Real);
		Assert.True(sumOfSquares < 3, $"Residual sum of squares {sumOfSquares}");
	}

	[Fact]
	public void FitNeedsMorePointsThanDegree()
	{
		Assert.Throws<MathException>(() => Polynomial.Fit(ComplexMatrix.RowVector(1, 2), ComplexMatrix.RowVector(1, 2), 2));
	}
}
