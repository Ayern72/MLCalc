using MLCalc.Math;

namespace MLCalc.Numerics.Tests;

public class CalculusTests
{
	[Theory]
	[InlineData(0, 1, 1.0 / 3)]
	[InlineData(-2, 3, 35.0 / 3)]
	[InlineData(1, 1, 0)]
	public void IntegratesPolynomialsExactly(double a, double b, double expected)
	{
		Assert.Equal(expected, Calculus.Integrate(x => x * x, a, b), 12);
	}

	[Fact]
	public void IntegratesSineOverHalfPeriod()
	{
		Assert.Equal(2, Calculus.Integrate(System.Math.Sin, 0, System.Math.PI), 11);
	}

	[Fact]
	public void IntegralWithReversedLimitsChangesSign()
	{
		Assert.Equal(-1, Calculus.Integrate(System.Math.Exp, System.Math.Log(2), 0), 11);
	}

	[Fact]
	public void IntegratesGaussianBell()
	{
		double result = Calculus.Integrate(x => System.Math.Exp(-x * x), -10, 10);
		Assert.Equal(System.Math.Sqrt(System.Math.PI), result, 10);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1.3)]
	[InlineData(-250)]
	public void DerivativeOfSine(double x)
	{
		Assert.Equal(System.Math.Cos(x), Calculus.Derivative(System.Math.Sin, x), 9);
	}

	[Fact]
	public void DerivativeOfCubic()
	{
		Assert.Equal(27, Calculus.Derivative(x => x * x * x, 3), 9);
	}
}

public class RootFindingTests
{
	[Fact]
	public void FindsSquareRootOfTwoInBracket()
	{
		Assert.Equal(System.Math.Sqrt(2), RootFinding.FindRoot(x => x * x - 2, 0, 2), 13);
	}

	[Fact]
	public void BracketWithoutSignChangeThrows()
	{
		var ex = Assert.Throws<MathException>(() => RootFinding.FindRoot(x => x * x + 1, -1, 1));
		Assert.Equal(MathError.NoSolution, ex.Error);
	}

	[Fact]
	public void FindsRootClosestToStart()
	{
		// sin has roots at multiples of π; start near 3π.
		Assert.Equal(3 * System.Math.PI, RootFinding.FindRootNear(System.Math.Sin, 9.0), 12);
		Assert.Equal(-System.Math.PI, RootFinding.FindRootNear(System.Math.Sin, -3.0), 12);
	}

	[Fact]
	public void FindsRootFarFromStart()
	{
		Assert.Equal(1000, RootFinding.FindRootNear(x => x - 1000, 0), 9);
	}

	[Fact]
	public void ReturnsStartWhenItIsARoot()
	{
		Assert.Equal(2, RootFinding.FindRootNear(x => x - 2, 2));
	}

	[Fact]
	public void SkipsPolesWithoutReportingThemAsRoots()
	{
		// Starting at 1.4, the pole of tan at π/2 (distance 0.17) is reached before the root at 0
		// (distance 1.4); the pole must be skipped.
		Assert.Equal(0, RootFinding.FindRootNear(System.Math.Tan, 1.4), 10);	}

	[Fact]
	public void NoRootThrows()
	{
		Assert.Throws<MathException>(() => RootFinding.FindRootNear(x => x * x + 1, 0, maxSteps: 100));
	}

	[Fact]
	public void FindsIntersectionOfLineAndParabola()
	{
		// x² = x + 2 at x = -1 and x = 2.
		Assert.Equal(2, RootFinding.FindIntersection(x => x * x, x => x + 2, 1.5), 12);
		Assert.Equal(-1, RootFinding.FindIntersection(x => x * x, x => x + 2, -0.5), 12);
	}

	[Fact]
	public void ParallelLinesDoNotIntersect()
	{
		var ex = Assert.Throws<MathException>(() => RootFinding.FindIntersection(x => x, x => x + 1, 0));
		Assert.Equal(MathError.NoIntersection, ex.Error);
	}

	[Fact]
	public void FindsExtremumOfParabola()
	{
		Assert.Equal(1.5, RootFinding.FindExtremum(x => (x - 1.5) * (x - 1.5) + 4, 0), 7);
	}
}
