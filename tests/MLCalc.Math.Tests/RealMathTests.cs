namespace MLCalc.Math.Tests;

public class RealMathTests
{
	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 1)]
	[InlineData(5, 120)]
	[InlineData(10, 3628800)]
	public void FactorialOfIntegers(double n, double expected)
	{
		Assert.Equal(expected, RealMath.Factorial(n));
	}

	[Fact]
	public void FactorialOfHalfIsGammaOfThreeHalves()
	{
		Assert.Equal(System.Math.Sqrt(System.Math.PI) / 2, RealMath.Factorial(0.5), 12);
	}

	[Fact]
	public void FactorialOfNegativeThrows()
	{
		Assert.Equal(MathError.NegativeNotAllowed, Assert.Throws<MathException>(() => RealMath.Factorial(-1)).Error);
	}

	[Fact]
	public void LargeFactorialOverflowsToInfinity()
	{
		Assert.Equal(double.PositiveInfinity, RealMath.Factorial(1000));
	}

	[Theory]
	[InlineData(5, 2, 10)]
	[InlineData(10, 0, 1)]
	[InlineData(10, 10, 1)]
	[InlineData(52, 5, 2598960)]
	[InlineData(3, 5, 0)]
	public void Combinations(double n, double k, double expected)
	{
		Assert.Equal(expected, RealMath.Comb(n, k));
	}

	[Theory]
	[InlineData(5, 2, 20)]
	[InlineData(10, 0, 1)]
	[InlineData(6, 6, 720)]
	[InlineData(3, 5, 0)]
	public void Permutations(double n, double k, double expected)
	{
		Assert.Equal(expected, RealMath.Perm(n, k));
	}

	[Fact]
	public void CombinationsRejectNegativeAndNonInteger()
	{
		Assert.Throws<MathException>(() => RealMath.Comb(-1, 2));
		Assert.Throws<MathException>(() => RealMath.Perm(5.5, 2));
	}

	[Theory]
	[InlineData(7, 3, 1)]
	[InlineData(-7, 3, -1)]
	[InlineData(7, -3, 1)]
	[InlineData(7.5, 2, 1.5)]
	public void ModKeepsSignOfDividend(double x, double y, double expected)
	{
		Assert.Equal(expected, RealMath.Mod(x, y));
	}

	[Theory]
	[InlineData(7, 2, 3)]
	[InlineData(-7, 2, -3)]
	public void DivTruncatesTowardZero(double x, double y, double expected)
	{
		Assert.Equal(expected, RealMath.Div(x, y));
	}

	[Fact]
	public void ModAndDivByZeroThrow()
	{
		Assert.Throws<MathException>(() => RealMath.Mod(1, 0));
		Assert.Throws<MathException>(() => RealMath.Div(1, 0));
	}
}
