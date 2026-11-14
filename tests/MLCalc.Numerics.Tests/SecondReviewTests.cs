using MLCalc.Math;

namespace MLCalc.Numerics.Tests;

/// <summary>
/// Integrals and derivatives the second review found wrong: Simpson's rule needed 33 million function
/// values for a jump and returned a result accurate to 8 digits as if it were exact (or failed), and
/// central differences returned 0 with full confidence at a symmetric kink such as abs(x) at 0.
/// </summary>
public class SecondReviewTests
{
	/// <summary>Integrates and counts the function values used.</summary>
	static (double Value, int Evaluations) Integrate(Func<double, double> f, double a, double b)
	{
		int evaluations = 0;
		double value = Calculus.Integrate(x => { evaluations++; return f(x); }, a, b);
		return (value, evaluations);
	}

	public static TheoryData<string, Func<double, double>, double, double, double> Discontinuous() => new()
	{
		{ "x > 0.5", x => x > 0.5 ? 1 : 0, 0, 1, 0.5 },
		{ "x > 0.3", x => x > 0.3 ? 1 : 0, 0, 1, 0.7 },
		// 0·⅓ + 1·⅓ + 2·⅓
		{ "floor(3x)", x => System.Math.Floor(3 * x), 0, 1, 1 },
		// 0.3²/2 + 0.7²/2
		{ "abs(x - 0.3)", x => System.Math.Abs(x - 0.3), 0, 1, 0.29 },
		{ "sign(x) on [-1, 2]", x => System.Math.Sign(x), -1, 2, 1 },
	};

	[Theory]
	[MemberData(nameof(Discontinuous))]
	public void IntegralsOfJumpsAndKinksAreAccurateAndCheap(string name, Func<double, double> f, double a, double b, double expected)
	{
		var (value, evaluations) = Integrate(f, a, b);
		Assert.True(System.Math.Abs(value - expected) <= 1e-12, $"{name}: expected {expected}, got {value:R}");
		Assert.True(evaluations < 20_000, $"{name}: {evaluations} function values");
	}

	[Fact]
	public void IntegrableSingularitiesAtTheEnds()
	{
		// The Gauss–Kronrod points never include the ends, where these functions are infinite.
		Assert.Equal(2, Integrate(x => 1 / System.Math.Sqrt(x), 0, 1).Value, 9);
		Assert.Equal(-1, Integrate(System.Math.Log, 0, 1).Value, 9);
	}

	[Fact]
	public void FastOscillations()
	{
		Assert.Equal(System.Math.Sin(1000) / 1000, Integrate(x => System.Math.Cos(1000 * x), 0, 1).Value, 12);
	}

	[Fact]
	public void DivergentIntegralIsAnError()
	{
		var ex = Assert.Throws<MathException>(() => Calculus.Integrate(x => 1 / x, 0, 1));
		Assert.Equal(MathError.NoSolution, ex.Error);
	}

	[Fact]
	public void UndefinedIntegrandIsAnError()
	{
		var ex = Assert.Throws<MathException>(() => Calculus.Integrate(System.Math.Sqrt, -1, 1));
		Assert.Equal(MathError.Domain, ex.Error);
	}

	[Theory]
	[InlineData(double.NegativeInfinity, 0)]
	[InlineData(0, double.PositiveInfinity)]
	[InlineData(double.NaN, 1)]
	public void InfiniteLimitsAreAnError(double a, double b)
	{
		var ex = Assert.Throws<MathException>(() => Calculus.Integrate(System.Math.Exp, a, b));
		Assert.Equal(MathError.Domain, ex.Error);
	}

	[Fact]
	public void DerivativeAtASymmetricKinkIsAnError()
	{
		Assert.Equal(MathError.Domain, Assert.Throws<MathException>(() => Calculus.Derivative(System.Math.Abs, 0)).Error);
		Assert.Equal(MathError.Domain, Assert.Throws<MathException>(() => Calculus.Derivative(x => System.Math.Sqrt(System.Math.Abs(x)), 0)).Error);
		Assert.Equal(MathError.Domain, Assert.Throws<MathException>(() => Calculus.Derivative(x => System.Math.Abs(x - 2) * 1e6, 2)).Error);
	}

	[Theory]
	[InlineData(1, 1)]
	[InlineData(-3, -1)]
	public void DerivativeOfAbsAwayFromTheKink(double x, double expected) =>
		Assert.Equal(expected, Calculus.Derivative(System.Math.Abs, x), 9);

	[Fact]
	public void SmoothFunctionsAreNotMistakenForKinks()
	{
		Assert.Equal(0, Calculus.Derivative(x => x * x, 0), 9);
		Assert.Equal(0, Calculus.Derivative(System.Math.Cos, 0), 9);
		Assert.Equal(3, Calculus.Derivative(x => 1e10 * x * x * x, 1) / 1e10, 9);
		Assert.Equal(System.Math.Cos(1e4), Calculus.Derivative(x => System.Math.Sin(1e4 * x), 1) / 1e4, 6);
	}
}
