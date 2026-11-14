using System.Numerics;

namespace MLCalc.Math.Tests;

/// <summary>
/// Results that the second review found wrong: cot and coth were NaN for large arguments, complex
/// atan and atanh lost half their digits for small results, and Γ gave NaN instead of ∞ or a pole.
/// Expected values are closed forms or series (atanh(z) = z + z³/3 + …), not the implementation.
/// </summary>
public class AccuracyTests
{
	const double HalfPi = System.Math.PI / 2;

	/// <summary>Each part within <paramref name="relative"/> of the expected part, relative to that part (or absolute when it is 0).</summary>
	static void AssertParts(Complex expected, Complex actual, double relative = 1e-15)
	{
		static bool Close(double e, double a, double tolerance) =>
			e == a || System.Math.Abs(e - a) <= tolerance * System.Math.Max(System.Math.Abs(e), e == 0 ? 1 : 0);
		Assert.True(Close(expected.Real, actual.Real, relative) && Close(expected.Imaginary, actual.Imaginary, relative),
			$"Expected {expected}, got {actual}");
	}

	public static TheoryData<string, Complex, Complex> LargeArguments() => new()
	{
		{ "coth", 800, 1 },
		{ "coth", -800, -1 },
		{ "coth", 1e300, 1 },
		{ "coth", new Complex(800, 1), 1 },
		{ "cot", new Complex(0, 1000), -ComplexMath.I },
		{ "cot", new Complex(0, -1000), ComplexMath.I },
		{ "cot", new Complex(1, 1000), -ComplexMath.I },
	};

	[Theory]
	[MemberData(nameof(LargeArguments))]
	public void CotAndCothTendToTheirLimits(string function, Complex z, Complex expected)
	{
		var actual = function == "cot" ? ComplexMath.Cot(z) : ComplexMath.Coth(z);
		Assert.True(Complex.Abs(expected - actual) <= 1e-15, $"{function}({z}): expected {expected}, got {actual}");
	}

	[Fact]
	public void CotAndCothAgreeWithTheQuotientForModerateArguments()
	{
		// The quotient is accurate relative to |z|, not in each part, so compare the difference to |z|.
		static void AssertNear(Complex expected, Complex actual) =>
			Assert.True(Complex.Abs(expected - actual) <= 1e-14 * Complex.Abs(expected), $"Expected {expected}, got {actual}");
		foreach (var z in new Complex[] { new(2, 3), new(-0.5, 0.25), new(1, -4), 0.3 })
		{
			AssertNear(Complex.Cos(z) / Complex.Sin(z), ComplexMath.Cot(z));
			AssertNear(Complex.Cosh(z) / Complex.Sinh(z), ComplexMath.Coth(z));
		}
		AssertParts(1e10, ComplexMath.Coth(1e-10));
	}

	[Fact]
	public void CotAndCothOfZeroAreDivisionsByZero()
	{
		Assert.Equal(MathError.DivisionByZero, Assert.Throws<MathException>(() => ComplexMath.Cot(0)).Error);
		Assert.Equal(MathError.DivisionByZero, Assert.Throws<MathException>(() => ComplexMath.Coth(0)).Error);
	}

	public static TheoryData<string, Complex, Complex> SmallResults() => new()
	{
		// atan(iy) = π/2 + i·atanh(1/y) for y > 1, and atanh(1e-10) = 1e-10 to double precision.
		{ "atan", new Complex(0, 1e10), new(HalfPi, 1e-10) },
		{ "atan", new Complex(0, -1e10), new(-HalfPi, -1e-10) },
		// Near 0 the z³/3 term is below double precision.
		{ "atan", new Complex(1e-10, 1e-10), new(1e-10, 1e-10) },
		{ "atanh", new Complex(1e-8, 1e-8), new(1e-8, 1e-8) },
		{ "atanh", new Complex(-1e-200, 3e-200), new(-1e-200, 3e-200) },
		// acot(z) = atan(1/z), and 1/(1e10 + 1e10i) = 5e-11 − 5e-11i.
		{ "acot", new Complex(1e10, 1e10), new(5e-11, -5e-11) },
		{ "acoth", new Complex(1e10, 1e10), new(5e-11, -5e-11) },
	};

	[Theory]
	[MemberData(nameof(SmallResults))]
	public void InverseTangentsAreAccurateForSmallResults(string function, Complex z, Complex expected) =>
		AssertParts(expected, Apply(function, z));

	public static TheoryData<string, Complex, Complex> ModerateAndHugeArguments() => new()
	{
		// atanh(z) = ½·ln((1 + z)/(1 − z)); for z = (1 + i)/2 the quotient is 1 + 2i.
		{ "atanh", new Complex(0.5, 0.5), new(System.Math.Log(5) / 4, System.Math.Atan(2) / 2) },
		{ "atanh", new Complex(-0.5, -0.5), new(-System.Math.Log(5) / 4, -System.Math.Atan(2) / 2) },
		// atan(z) = −i·atanh(iz); for z = (1 + i)/2, iz = (−1 + i)/2.
		{ "atan", new Complex(0.5, 0.5), new(System.Math.Atan(2) / 2, System.Math.Log(5) / 4) },
		// For huge |z|, atanh(z) = 1/z ± iπ/2: 1/(1e300 + 1e300i) has real part 5e-301.
		{ "atanh", new Complex(1e300, 1e300), new(5e-301, HalfPi) },
		{ "atanh", new Complex(1e300, -1e300), new(5e-301, -HalfPi) },
		{ "atanh", -1e200, new(-1e-200, HalfPi) },
		{ "atanh", double.PositiveInfinity, new(0, HalfPi) },
		{ "atan", new Complex(double.PositiveInfinity, 1), new(HalfPi, 0) },
	};

	[Theory]
	[MemberData(nameof(ModerateAndHugeArguments))]
	public void InverseTangentsForModerateAndHugeArguments(string function, Complex z, Complex expected) =>
		AssertParts(expected, Apply(function, z), 1e-14);

	[Fact]
	public void AtanIsOdd()
	{
		foreach (var z in new Complex[] { new(1, 2), new(-3, 0.5), new(0, 3), new(2, -1e-5) })
			Assert.Equal(-ComplexMath.Atan(z), ComplexMath.Atan(-z));
	}

	static Complex Apply(string function, Complex z) => function switch
	{
		"atan" => ComplexMath.Atan(z),
		"atanh" => ComplexMath.Atanh(z),
		"acot" => ComplexMath.Acot(z),
		"acoth" => ComplexMath.Acoth(z),
		_ => throw new ArgumentException(function),
	};

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-2)]
	[InlineData(-170)]
	public void GammaHasPolesAtNonPositiveIntegers(double x) => Assert.True(double.IsNaN(RealMath.Gamma(x)));

	[Theory]
	[InlineData(1000.5)]
	[InlineData(1e15 + 0.5)]
	[InlineData(double.PositiveInfinity)]
	public void FactorialOfLargeNonIntegersIsInfinity(double x) => Assert.Equal(double.PositiveInfinity, RealMath.Factorial(x));

	[Fact]
	public void GammaOverflowsToInfinityWithoutNaN()
	{
		// Γ is increasing here and exceeds the largest double near 171.62.
		double previous = 0;
		for (double x = 170; x <= 800; x += 0.05)
		{
			double gamma = RealMath.Gamma(x);
			Assert.False(double.IsNaN(gamma), $"Γ({x}) is NaN");
			Assert.True(gamma >= previous, $"Γ({x}) = {gamma} is less than Γ({x - 0.05}) = {previous}");
			previous = gamma;
		}
		Assert.True(double.IsFinite(RealMath.Gamma(171.6)));
		Assert.Equal(double.PositiveInfinity, RealMath.Gamma(171.7));
	}
}
