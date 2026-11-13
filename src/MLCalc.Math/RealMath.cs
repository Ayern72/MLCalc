namespace MLCalc.Math;

/// <summary>Real-valued scalar functions.</summary>
public static class RealMath
{
	/// <summary>n! for non-negative integers; Γ(x + 1) for other non-negative values.</summary>
	public static double Factorial(double x)
	{
		if (x < 0)
			throw new MathException(MathError.NegativeNotAllowed, "Factorial of a negative number.");
		if (x != System.Math.Floor(x))
			return Gamma(x + 1);
		double result = 1;
		for (double i = 2; i <= x && !double.IsInfinity(result); i++)
			result *= i;
		return result;
	}

	/// <summary>The gamma function (Lanczos approximation, g = 7).</summary>
	public static double Gamma(double x)
	{
		if (x < 0.5)
			return System.Math.PI / (System.Math.Sin(System.Math.PI * x) * Gamma(1 - x));
		x -= 1;
		double a = LanczosCoefficients[0];
		double t = x + 7.5;
		for (int i = 1; i < LanczosCoefficients.Length; i++)
			a += LanczosCoefficients[i] / (x + i);
		// t^(x+½)·e^(−t), with the power split in two halves around the exponential: t^(x+½) alone
		// overflows for x > 141 although the product is finite up to Γ(171.6).
		double halfPower = System.Math.Pow(t, (x + 0.5) / 2);
		return System.Math.Sqrt(2 * System.Math.PI) * a * halfPower * System.Math.Exp(-t) * halfPower;
	}

	static readonly double[] LanczosCoefficients =
	[
		0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313,
		-176.61502916214059, 12.507343278686905, -0.13857109526572012,
		9.9843695780195716e-6, 1.5056327351493116e-7,
	];

	/// <summary>Number of combinations, n choose k. Zero when k &gt; n.</summary>
	public static double Comb(double n, double k)
	{
		RequireNonNegativeIntegers(n, k, "comb");
		if (k > n)
			return 0;
		k = System.Math.Min(k, n - k);
		double result = 1;
		// The result at least doubles every step while i ≤ k ≤ n/2, so it overflows within about a
		// thousand steps; stopping there keeps huge arguments such as comb(1e12, 5e11) instant.
		for (double i = 1; i <= k && !double.IsInfinity(result); i++)
			result = result * (n - k + i) / i;
		return System.Math.Round(result);
	}

	/// <summary>Number of permutations of k items chosen from n. Zero when k &gt; n.</summary>
	public static double Perm(double n, double k)
	{
		RequireNonNegativeIntegers(n, k, "perm");
		if (k > n)
			return 0;
		double result = 1;
		// Stop once the product has overflowed: perm(1e12, 1e12) would otherwise loop for hours.
		for (double i = n; i > n - k && !double.IsInfinity(result); i--)
			result *= i;
		return result;
	}

	/// <summary>Remainder with the sign of the dividend (C <c>fmod</c>), as the original <c>mod</c>.</summary>
	public static double Mod(double x, double y)
	{
		if (y == 0)
			throw new MathException(MathError.DivisionByZero, "Division by zero.");
		return x % y;
	}

	/// <summary>Integer division truncated toward zero.</summary>
	public static double Div(double x, double y)
	{
		if (y == 0)
			throw new MathException(MathError.DivisionByZero, "Division by zero.");
		return System.Math.Truncate(x / y);
	}

	static void RequireNonNegativeIntegers(double n, double k, string name)
	{
		if (n < 0 || k < 0 || n != System.Math.Floor(n) || k != System.Math.Floor(k))
			throw new MathException(MathError.Domain, name + " requires non-negative integers.");
	}
}
