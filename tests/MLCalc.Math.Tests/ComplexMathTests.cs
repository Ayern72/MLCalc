using System.Numerics;

namespace MLCalc.Math.Tests;

public class ComplexMathTests
{
	const double Tolerance = 1e-12;

	public static TheoryData<double, double> SamplePoints => new()
	{
		{ 0.3, 0.4 }, { -0.7, 0.2 }, { 1.5, -2.0 }, { -2.5, -0.5 }, { 0.1, 3.0 }, { 2.0, 0.0001 },
	};

	static void AssertClose(Complex expected, Complex actual, double tolerance = Tolerance)
	{
		Assert.True(Complex.Abs(expected - actual) <= tolerance * System.Math.Max(1, Complex.Abs(expected)),
			$"Expected {expected}, got {actual}");
	}

	[Theory]
	[MemberData(nameof(SamplePoints))]
	public void InverseFunctionsUndoForwardFunctions(double re, double im)
	{
		var z = new Complex(re, im);
		AssertClose(z, ComplexMath.Sin(ComplexMath.Asin(z)));
		AssertClose(z, ComplexMath.Cos(ComplexMath.Acos(z)));
		AssertClose(z, ComplexMath.Tan(ComplexMath.Atan(z)));
		AssertClose(z, ComplexMath.Cot(ComplexMath.Acot(z)));
		AssertClose(z, ComplexMath.Sinh(ComplexMath.Asinh(z)));
		AssertClose(z, ComplexMath.Cosh(ComplexMath.Acosh(z)));
		AssertClose(z, ComplexMath.Tanh(ComplexMath.Atanh(z)));
		AssertClose(z, ComplexMath.Coth(ComplexMath.Acoth(z)));
		AssertClose(z, ComplexMath.Exp(ComplexMath.Ln(z)));
	}

	[Theory]
	[MemberData(nameof(SamplePoints))]
	public void MatchesSystemNumericsWhereAvailable(double re, double im)
	{
		var z = new Complex(re, im);
		AssertClose(Complex.Sin(z), ComplexMath.Sin(z));
		AssertClose(Complex.Tanh(z), ComplexMath.Tanh(z));
		AssertClose(Complex.Asin(z), ComplexMath.Asin(z));
		AssertClose(Complex.Acos(z), ComplexMath.Acos(z));
		AssertClose(Complex.Atan(z), ComplexMath.Atan(z));
		AssertClose(Complex.Log(z), ComplexMath.Ln(z));
		AssertClose(Complex.Sqrt(z), ComplexMath.Sqrt(z));
	}

	[Fact]
	public void SquareRootOfNegativeRealIsExactlyImaginary()
	{
		Assert.Equal(new Complex(0, 2), ComplexMath.Sqrt(-4));
	}

	[Fact]
	public void NegativeBaseWithIntegerExponentStaysReal()
	{
		Assert.Equal(new Complex(-8, 0), ComplexMath.Pow(-2, 3));
		Assert.Equal(new Complex(0.25, 0), ComplexMath.Pow(-2, -2));
	}

	[Fact]
	public void NegativeBaseWithFractionalExponentIsComplex()
	{
		AssertClose(new Complex(0, 1), ComplexMath.Pow(-1, 0.5));
	}

	[Fact]
	public void OddRootOfNegativeRealIsReal()
	{
		Assert.Equal(-2, ComplexMath.Root(-8, 3).Real, 12);
		Assert.Equal(0, ComplexMath.Root(-8, 3).Imaginary);
	}

	[Fact]
	public void ImaginaryUnitSquaredIsMinusOne()
	{
		AssertClose(-1, ComplexMath.Pow(ComplexMath.I, 2));
	}

	[Fact]
	public void IntegerPowersOfComplexNumbersAreExact()
	{
		Assert.Equal(new Complex(-1, 0), ComplexMath.Pow(ComplexMath.I, 2));
		Assert.Equal(new Complex(-4, 0), ComplexMath.Pow(new Complex(1, 1), 4));
		Assert.Equal(new Complex(0, -1), ComplexMath.Pow(ComplexMath.I, -1));
	}

	[Fact]
	public void RealProductWithInfinityStaysReal()
	{
		var product = ComplexMath.Multiply(double.PositiveInfinity, -2);
		Assert.Equal(double.NegativeInfinity, product.Real);
		Assert.Equal(0, product.Imaginary);
	}

	[Fact]
	public void EulerIdentity()
	{
		AssertClose(-1, ComplexMath.Exp(ComplexMath.I * System.Math.PI));
	}

	[Fact]
	public void ArgCoversAllQuadrants()
	{
		Assert.Equal(System.Math.PI / 4, ComplexMath.Arg(new Complex(1, 1)), 12);
		Assert.Equal(3 * System.Math.PI / 4, ComplexMath.Arg(new Complex(-1, 1)), 12);
		Assert.Equal(-3 * System.Math.PI / 4, ComplexMath.Arg(new Complex(-1, -1)), 12);
		Assert.Equal(-System.Math.PI / 2, ComplexMath.Arg(new Complex(0, -1)), 12);
		Assert.Equal(0, ComplexMath.Arg(Complex.Zero));
	}

	[Fact]
	public void DivisionByZeroThrows()
	{
		var ex = Assert.Throws<MathException>(() => ComplexMath.Divide(1, 0));
		Assert.Equal(MathError.DivisionByZero, ex.Error);
	}

	[Fact]
	public void LogarithmOfZeroThrows()
	{
		Assert.Equal(MathError.Domain, Assert.Throws<MathException>(() => ComplexMath.Ln(0)).Error);
	}

	[Fact]
	public void LogOfNegativeRealHasImaginaryPi()
	{
		AssertClose(new Complex(System.Math.Log(2), System.Math.PI), ComplexMath.Ln(-2));
	}

	[Fact]
	public void NegativeZeroImaginaryPartStaysOnPrincipalBranch()
	{
		// -(1 + 0i) in complex arithmetic is -1 - 0i; that must still behave as the real number -1.
		var minusOne = -new Complex(1, 0);
		Assert.Equal(System.Math.PI, ComplexMath.Arg(minusOne));
		AssertClose(new Complex(0, System.Math.PI), ComplexMath.Ln(minusOne));
		AssertClose(ComplexMath.I, ComplexMath.Pow(minusOne, 0.5));
		Assert.False(double.IsNegative(ComplexMath.Negate(1).Imaginary));
	}
}
