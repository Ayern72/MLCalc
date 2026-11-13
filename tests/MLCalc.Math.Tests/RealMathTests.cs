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

	[Theory]
	[InlineData(60, 30)]
	[InlineData(100, 50)]
	[InlineData(1000, 3)]
	[InlineData(67, 33)]
	public void CombinationsAreAccurateBeyondExactIntegers(int n, int k)
	{
		// Above 2^53 doubles cannot hold every integer, but the result must still be correct to
		// within rounding; compare with the exact value.
		var exact = System.Numerics.BigInteger.One;
		for (int i = 1; i <= k; i++)
			exact = exact * (n - k + i) / i;
		double expected = (double)exact;
		Assert.True(System.Math.Abs(RealMath.Comb(n, k) - expected) <= 1e-12 * expected, $"comb({n},{k})");
	}

	[Fact]
	public void PermutationsAreExactWhileTheyFit()
	{
		Assert.Equal(3628800, RealMath.Perm(10, 10));
		Assert.Equal(9.33262154439441e157, RealMath.Perm(100, 100), 9.33262154439441e157 * 1e-12);
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
