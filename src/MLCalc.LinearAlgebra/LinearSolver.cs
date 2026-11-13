using System.Numerics;
using MLCalc.Math;

namespace MLCalc.LinearAlgebra;

/// <summary>Gaussian elimination with partial pivoting (the original <c>Gauss</c>, extended to complex values).</summary>
public static class LinearSolver
{
	/// <summary>Relative pivot size below which a matrix is treated as singular.</summary>
	const double SingularityTolerance = 1e-13;

	/// <summary>Solves A·X = B, where A is square and B has as many rows as A.</summary>
	public static ComplexMatrix Solve(ComplexMatrix a, ComplexMatrix b)
	{
		if (!a.IsSquare)
			throw new MathException(MathError.Dimension, $"The coefficient matrix must be square, got {a.Rows}×{a.Columns}.");
		if (b.Rows != a.Rows)
			throw new MathException(MathError.Dimension, $"The right-hand side must have {a.Rows} rows, got {b.Rows}.");

		int n = a.Rows, m = b.Columns;
		var lu = ToArray(a);
		var x = ToArray(b);
		double scale = MaxAbs(lu);

		for (int col = 0; col < n; col++)
		{
			int pivot = FindPivot(lu, col, n);
			if (Complex.Abs(lu[pivot, col]) <= SingularityTolerance * scale)
				throw new MathException(MathError.SingularMatrix, "The equation system has no unique solution (the matrix is singular).");
			SwapRows(lu, pivot, col);
			SwapRows(x, pivot, col);

			for (int row = col + 1; row < n; row++)
			{
				var factor = lu[row, col] / lu[col, col];
				if (factor == Complex.Zero)
					continue;
				for (int k = col; k < n; k++)
					lu[row, k] -= factor * lu[col, k];
				for (int k = 0; k < m; k++)
					x[row, k] -= factor * x[col, k];
			}
		}

		for (int row = n - 1; row >= 0; row--)
			for (int k = 0; k < m; k++)
			{
				var sum = x[row, k];
				for (int j = row + 1; j < n; j++)
					sum -= lu[row, j] * x[j, k];
				x[row, k] = sum / lu[row, row];
			}

		return ComplexMatrix.FromRows(x);
	}

	public static Complex Determinant(ComplexMatrix a)
	{
		if (!a.IsSquare)
			throw new MathException(MathError.Dimension, $"The determinant needs a square matrix, got {a.Rows}×{a.Columns}.");
		int n = a.Rows;
		var lu = ToArray(a);
		Complex det = Complex.One;
		for (int col = 0; col < n; col++)
		{
			int pivot = FindPivot(lu, col, n);
			if (lu[pivot, col] == Complex.Zero)
				return Complex.Zero;
			if (pivot != col)
			{
				SwapRows(lu, pivot, col);
				det = -det;
			}
			det *= lu[col, col];
			for (int row = col + 1; row < n; row++)
			{
				var factor = lu[row, col] / lu[col, col];
				for (int k = col; k < n; k++)
					lu[row, k] -= factor * lu[col, k];
			}
		}
		return det;
	}

	static int FindPivot(Complex[,] m, int col, int n)
	{
		int best = col;
		for (int row = col + 1; row < n; row++)
			if (Complex.Abs(m[row, col]) > Complex.Abs(m[best, col]))
				best = row;
		return best;
	}

	static void SwapRows(Complex[,] m, int r1, int r2)
	{
		if (r1 == r2)
			return;
		for (int c = 0; c < m.GetLength(1); c++)
			(m[r1, c], m[r2, c]) = (m[r2, c], m[r1, c]);
	}

	static Complex[,] ToArray(ComplexMatrix matrix)
	{
		var result = new Complex[matrix.Rows, matrix.Columns];
		for (int r = 0; r < matrix.Rows; r++)
			for (int c = 0; c < matrix.Columns; c++)
				result[r, c] = matrix[r, c];
		return result;
	}

	static double MaxAbs(Complex[,] m)
	{
		double max = 0;
		foreach (var value in m)
			max = System.Math.Max(max, Complex.Abs(value));
		return max == 0 ? 1 : max;
	}
}
