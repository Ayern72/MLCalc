using System.Numerics;
using MLCalc.Math;

namespace MLCalc.LinearAlgebra.Tests;

public class ComplexMatrixTests
{
	static readonly ComplexMatrix A = ComplexMatrix.FromRows(new double[,] { { 1, 2 }, { 3, 4 } });

	[Fact]
	public void IndexerIsRowMajorAndZeroBased()
	{
		Assert.Equal(2, A[0, 1]);
		Assert.Equal(3, A[1, 0]);
		Assert.Equal(3, A[2]);
	}

	[Fact]
	public void IndexOutsideMatrixThrowsIndexError()
	{
		Assert.Equal(MathError.Index, Assert.Throws<MathException>(() => A[2, 0]).Error);
	}

	[Fact]
	public void AdditionBroadcastsScalars()
	{
		Assert.Equal(ComplexMatrix.FromRows(new double[,] { { 11, 12 }, { 13, 14 } }), A + 10);
		Assert.Equal(ComplexMatrix.FromRows(new double[,] { { 9, 8 }, { 7, 6 } }), 10 - A);
	}

	[Fact]
	public void ElementWiseOperationsRequireMatchingSizes()
	{
		var ex = Assert.Throws<MathException>(() => A + ComplexMatrix.RowVector(1, 2, 3));
		Assert.Equal(MathError.Dimension, ex.Error);
	}

	[Fact]
	public void MatrixProduct()
	{
		var product = A * ComplexMatrix.ColumnVector(1, 1);
		Assert.Equal(ComplexMatrix.ColumnVector(3, 7), product);
	}

	[Fact]
	public void MatrixProductOfComplexValues()
	{
		var z = ComplexMatrix.FromRows(new Complex[,] { { new(1, 1), 0 }, { 0, new(0, 1) } });
		var expected = ComplexMatrix.FromRows(new Complex[,] { { new(0, 2), 0 }, { 0, -1 } });
		Assert.Equal(expected, z * z);
	}

	[Fact]
	public void ScalarTimesComplexMatrixIsCorrect()
	{
		// The original multiplied real and imaginary parts separately for scalar·matrix.
		var result = ComplexMatrix.Scalar(new Complex(0, 1)) * ComplexMatrix.RowVector(2, 3);
		Assert.Equal(ComplexMatrix.Create(1, 2, [new Complex(0, 2), new Complex(0, 3)]), result);
	}

	[Fact]
	public void IncompatibleProductThrows()
	{
		Assert.Throws<MathException>(() => A * ComplexMatrix.RowVector(1, 2));
	}

	[Fact]
	public void TransposeDoesNotConjugate()
	{
		var z = ComplexMatrix.Create(1, 2, [new Complex(1, 2), 3]);
		var t = z.Transpose();
		Assert.Equal(2, t.Rows);
		Assert.Equal(new Complex(1, 2), t[0, 0]);
		Assert.Equal(new Complex(1, -2), z.ConjugateTranspose()[0, 0]);
	}

	[Fact]
	public void DeterminantAndInverse()
	{
		Assert.Equal(-2, A.Determinant().Real, 12);
		Assert.True((A * A.Inverse()).ApproximatelyEquals(ComplexMatrix.Identity(2), 1e-12));
	}

	[Fact]
	public void LeftAndRightDivision()
	{
		var b = ComplexMatrix.ColumnVector(5, 11);
		Assert.True(ComplexMatrix.LeftDivide(A, b).ApproximatelyEquals(ComplexMatrix.ColumnVector(1, 2), 1e-12));
		var row = ComplexMatrix.RowVector(7, 10);
		Assert.True(ComplexMatrix.RightDivide(row, A).ApproximatelyEquals(ComplexMatrix.RowVector(1, 2), 1e-12));
	}

	[Fact]
	public void ElementCountIsLimitedWithoutIntegerOverflow()
	{
		Assert.Equal(ComplexMatrix.MaxElements, ComplexMatrix.ElementCount(5000, 5000));
		Assert.Equal(MathError.TooLarge, Assert.Throws<MathException>(() => ComplexMatrix.ElementCount(5000, 5001)).Error);
		// 65536 × 65536 wraps around to 0 in 32-bit arithmetic.
		Assert.Equal(MathError.TooLarge, Assert.Throws<MathException>(() => ComplexMatrix.Zeros(65536, 65536)).Error);
		Assert.Equal(MathError.TooLarge, Assert.Throws<MathException>(() => ComplexMatrix.Ones(46341, 46341)).Error);
		Assert.Equal(MathError.Dimension, Assert.Throws<MathException>(() => ComplexMatrix.ElementCount(-1, 2)).Error);
	}

	[Fact]
	public void ProductAndConcatenationRespectTheLimit()
	{
		var tall = ComplexMatrix.Zeros(10_000, 1);
		var wide = ComplexMatrix.Zeros(1, 10_000);
		Assert.Equal(MathError.TooLarge, Assert.Throws<MathException>(() => tall * wide).Error);
		var big = ComplexMatrix.Zeros(1, 20_000_000);
		Assert.Equal(MathError.TooLarge, Assert.Throws<MathException>(() => ComplexMatrix.HorizontalConcat([big, big])).Error);
		Assert.Equal(MathError.TooLarge, Assert.Throws<MathException>(() => ComplexMatrix.VerticalConcat([big, big])).Error);
	}

	[Fact]
	public void ConcatenationJoinsAndSkipsEmpty()
	{
		var joined = ComplexMatrix.HorizontalConcat([A, ComplexMatrix.ColumnVector(5, 6), ComplexMatrix.Zeros(0, 0)]);
		Assert.Equal(ComplexMatrix.FromRows(new double[,] { { 1, 2, 5 }, { 3, 4, 6 } }), joined);
		var stacked = ComplexMatrix.VerticalConcat([A, ComplexMatrix.RowVector(7, 8)]);
		Assert.Equal(ComplexMatrix.FromRows(new double[,] { { 1, 2 }, { 3, 4 }, { 7, 8 } }), stacked);
		Assert.Throws<MathException>(() => ComplexMatrix.HorizontalConcat([A, ComplexMatrix.RowVector(1)]));
	}

	[Fact]
	public void MatrixPowerIsRepeatedMatrixProduct()
	{
		Assert.Equal(A * A * A, ComplexMatrix.Power(A, 3));
		Assert.Equal(ComplexMatrix.Identity(2), ComplexMatrix.Power(A, 0));
		Assert.True(ComplexMatrix.Power(A, -2).ApproximatelyEquals((A * A).Inverse(), 1e-12));
	}

	[Fact]
	public void ScalarPowerUsesComplexPower()
	{
		Assert.Equal(ComplexMatrix.Scalar(-8), ComplexMatrix.Power(-2, 3));
	}

	[Fact]
	public void MatrixPowerRejectsNonSquareAndNonIntegerExponents()
	{
		Assert.Equal(MathError.Dimension, Assert.Throws<MathException>(() => ComplexMatrix.Power(ComplexMatrix.RowVector(1, 2), 2)).Error);
		Assert.Equal(MathError.Domain, Assert.Throws<MathException>(() => ComplexMatrix.Power(A, 0.5)).Error);
	}

	[Theory]
	[InlineData(3, 0, "3")]
	[InlineData(0, 2, "2i")]
	[InlineData(0, -1, "-1i")]
	[InlineData(1.5, -2, "1.5-2i")]
	[InlineData(-1, 0.25, "-1+0.25i")]
	[InlineData(double.PositiveInfinity, 0, "Inf")]
	[InlineData(double.NegativeInfinity, 0, "-Inf")]
	[InlineData(double.NaN, 0, "NaN")]
	[InlineData(1, double.NegativeInfinity, "1-Infi")]
	public void FormatsComplexNumbers(double re, double im, string expected)
	{
		Assert.Equal(expected, ComplexMatrix.Format(new Complex(re, im)));
	}

	[Fact]
	public void ToStringSeparatesColumnsWithTabsAndRowsWithLines()
	{
		Assert.Equal("1\t2" + Environment.NewLine + "3\t4", A.ToString());
	}
}
