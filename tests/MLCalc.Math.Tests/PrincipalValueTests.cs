using System.Numerics;

namespace MLCalc.Math.Tests;

/// <summary>
/// Principal values on the branch cuts and results for large arguments, with literal expected
/// values. (Round-trip identities such as sin(asin(z)) = z hold for every branch, so they cannot
/// catch a wrong branch.)
/// </summary>
public class PrincipalValueTests
{
	const double HalfPi = System.Math.PI / 2;
	const double Acosh2 = 1.3169578969248167;   // acosh(2) = ln(2 + √3)
	const double Asinh2 = 1.4436354751788103;   // asinh(2) = ln(2 + √5)
	const double Atanh05 = 0.5493061443340548;  // atanh(1/2) = ln(3)/2
	static readonly double LnTwiceE200 = System.Math.Log(2) + 200 * System.Math.Log(10); // asinh(1e200) = ln(2·10²⁰⁰)

	static void AssertClose(Complex expected, Complex actual, double tolerance = 1e-12)
	{
		double scale = System.Math.Max(1, Complex.Abs(expected));
		Assert.True(Complex.Abs(expected - actual) <= tolerance * scale, $"Expected {expected}, got {actual}");
	}

	public static TheoryData<string, Complex, Complex> CutValues() => new()
	{
		// Real arguments outside [-1, 1], with the same conventions as MATLAB.
		{ "asin", 2, new(HalfPi, -Acosh2) },
		{ "asin", -2, new(-HalfPi, Acosh2) },
		{ "acos", 2, new(0, Acosh2) },
		{ "acos", -2, new(System.Math.PI, -Acosh2) },
		{ "acosh", 0.5, new(0, System.Math.PI / 3) },
		{ "acosh", -2, new(Acosh2, System.Math.PI) },
		{ "atanh", 2, new(Atanh05, HalfPi) },
		{ "atanh", -2, new(-Atanh05, HalfPi) },
		// Imaginary arguments on the cuts of atan and asinh.
		{ "atan", new Complex(0, 2), new(HalfPi, Atanh05) },
		{ "atan", new Complex(0, -2), new(-HalfPi, -Atanh05) },
		{ "asinh", new Complex(0, 2), new(Acosh2, HalfPi) },
		{ "asinh", new Complex(0, -2), new(-Acosh2, -HalfPi) },
		{ "asin", new Complex(0, 2), new(0, Asinh2) },
	};

	public static TheoryData<string, Complex, Complex> LargeValues() => new()
	{
		{ "asin", -1e8, new(-HalfPi, 19.113827924512311) },
		{ "asin", -1e7, new(-HalfPi, 16.811242831518264) },
		{ "asin", 1e200, new(HalfPi, -LnTwiceE200) },
		{ "acos", -1e8, new(System.Math.PI, -19.113827924512311) },
		{ "acos", 1e200, new(0, LnTwiceE200) },
		{ "asin", new Complex(0, 1e200), new(0, LnTwiceE200) },
		{ "asin", new Complex(-1e8, 1e-9), new(-HalfPi, 19.113827924512311) },
		{ "asinh", new Complex(-1e8, 1), new(-19.113827924512311, 1e-8) },
		{ "asinh", new Complex(1e200, 1), new(LnTwiceE200, 1e-200) },
		{ "atan", new Complex(0, 1e200), new(HalfPi, 0) },
		{ "atan", new Complex(1e200, 1e200), new(HalfPi, 0) },
		{ "acosh", new Complex(1e200, 1), new(LnTwiceE200, 0) },
	};

	static Complex Apply(string function, Complex z) => function switch
	{
		"asin" => ComplexMath.Asin(z),
		"acos" => ComplexMath.Acos(z),
		"atan" => ComplexMath.Atan(z),
		"asinh" => ComplexMath.Asinh(z),
		"acosh" => ComplexMath.Acosh(z),
		"atanh" => ComplexMath.Atanh(z),
		_ => throw new ArgumentException(function),
	};

	[Theory]
	[MemberData(nameof(CutValues))]
	public void PrincipalValuesOnBranchCuts(string function, Complex z, Complex expected)
	{
		AssertClose(expected, Apply(function, z));
	}

	[Theory]
	[MemberData(nameof(LargeValues))]
	public void LargeArgumentsKeepFullAccuracy(string function, Complex z, Complex expected)
	{
		AssertClose(expected, Apply(function, z), 1e-11);
	}

	public static TheoryData<double, double> OddPoints() => new()
	{
		{ 0.3, 0.4 }, { 1.5, -2.0 }, { 0.1, 3.0 }, { -2.5, 0.5 }, { 1e8, 1 }, { 3, 0 }, { 0, 3 },
	};

	[Theory]
	[MemberData(nameof(OddPoints))]
	public void OddFunctionsAreOdd(double re, double im)
	{
		var z = new Complex(re, im);
		AssertClose(-ComplexMath.Asin(z), ComplexMath.Asin(-z));
		AssertClose(-ComplexMath.Asinh(z), ComplexMath.Asinh(-z));
		AssertClose(-ComplexMath.Atan(z), ComplexMath.Atan(-z));
	}

	[Fact]
	public void PowerOfZero()
	{
		Assert.Equal(Complex.One, ComplexMath.Pow(0, 0));
		Assert.Equal(Complex.Zero, ComplexMath.Pow(0, 2));
		Assert.Equal(Complex.Zero, ComplexMath.Pow(0, new Complex(0.5, 1)));
		Assert.Equal(MathError.DivisionByZero, Assert.Throws<MathException>(() => ComplexMath.Pow(0, -1)).Error);
		Assert.Equal(MathError.DivisionByZero, Assert.Throws<MathException>(() => ComplexMath.Pow(0, -0.5)).Error);
		Assert.Equal(MathError.DivisionByZero, Assert.Throws<MathException>(() => ComplexMath.Root(0, -2)).Error);
	}
}

public class GammaTests
{
	/// <summary>Γ(n + ½) by the recurrence Γ(x + 1) = x·Γ(x), starting from Γ(½) = √π.</summary>
	static double HalfIntegerGamma(int n)
	{
		double value = System.Math.Sqrt(System.Math.PI);
		for (int k = 0; k < n; k++)
			value *= k + 0.5;
		return value;
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(141)]
	[InlineData(142)]
	[InlineData(150)]
	[InlineData(171)]
	public void HalfIntegerValuesMatchTheRecurrence(int n)
	{
		// factorial(x) = Γ(x + 1), so factorial(n − ½) = Γ(n + ½).
		double expected = HalfIntegerGamma(n);
		double actual = RealMath.Factorial(n - 0.5);
		Assert.True(System.Math.Abs(actual - expected) <= 1e-12 * expected, $"Γ({n}.5): expected {expected}, got {actual}");
	}

	[Fact]
	public void LargeArgumentsNoLongerOverflowEarly()
	{
		Assert.Equal(2.25990911e244, RealMath.Factorial(141.5), 2.25990911e244 * 1e-8);
		Assert.True(double.IsFinite(RealMath.Factorial(170.5)));
		Assert.Equal(double.PositiveInfinity, RealMath.Factorial(171.7));
	}

	[Theory]
	[InlineData(0.25)]
	[InlineData(0.75)]
	[InlineData(5.3)]
	[InlineData(-0.5)]
	[InlineData(-2.5)]
	public void GammaSatisfiesTheRecurrence(double x)
	{
		Assert.Equal(1, RealMath.Gamma(x + 1) / (x * RealMath.Gamma(x)), 12);
	}
}
