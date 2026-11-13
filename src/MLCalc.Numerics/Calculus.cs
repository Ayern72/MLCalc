using MLCalc.Math;

namespace MLCalc.Numerics;

/// <summary>Numerical integration and differentiation of real functions.</summary>
public static class Calculus
{
	/// <summary>
	/// Definite integral of <paramref name="f"/> from <paramref name="a"/> to <paramref name="b"/> using
	/// Simpson's rule with repeated interval halving and Richardson extrapolation (Romberg style),
	/// as the original <c>integral</c>.
	/// </summary>
	/// <param name="f">The integrand.</param>
	/// <param name="a">Lower limit.</param>
	/// <param name="b">Upper limit (may be less than <paramref name="a"/>).</param>
	/// <param name="relativeTolerance">Stop when successive estimates agree to this relative precision.</param>
	/// <param name="maxHalvings">Upper bound on interval halvings (2^maxHalvings subintervals).</param>
	public static double Integrate(Func<double, double> f, double a, double b, double relativeTolerance = 1e-12, int maxHalvings = 24)
	{
		ArgumentNullException.ThrowIfNull(f);
		if (a == b)
			return 0;

		double h = (b - a) / 2;
		double ends = f(a) + f(b);
		double odd = f(a + h);
		double lastSimpson = h / 3 * (ends + 4 * odd);
		double lastEstimate = lastSimpson;
		double interior = 0;
		double lastChange = double.PositiveInfinity;
		long points = 1;

		for (int halving = 1; halving <= maxHalvings; halving++)
		{
			interior += odd;
			h /= 2;
			points *= 2;
			odd = 0;
			for (long i = 0; i < points; i++)
				odd += f(a + (2 * i + 1) * h);

			double simpson = h / 3 * (ends + 2 * interior + 4 * odd);
			double estimate = (16 * simpson - lastSimpson) / 15;
			if (double.IsNaN(estimate))
				throw new MathException(MathError.Domain, "The function is not defined everywhere on the interval.");
			lastChange = System.Math.Abs(estimate - lastEstimate) / System.Math.Max(1, System.Math.Abs(estimate));
			if (halving >= 3 && lastChange <= relativeTolerance)
				return estimate;
			lastSimpson = simpson;
			lastEstimate = estimate;
		}

		// Out of halvings. Accept a result that has settled to about 8 digits; anything less would be
		// reported as if it were accurate.
		if (lastChange <= 1e-8)
			return lastEstimate;
		throw new MathException(MathError.NoSolution,
			"The integral did not converge; the function may be singular or oscillate strongly on the interval.");
	}

	/// <summary>
	/// Derivative of <paramref name="f"/> at <paramref name="x"/> (the original <c>Derivata</c>), using
	/// Ridders' method: central differences with shrinking steps, extrapolated to step zero, keeping
	/// the estimate with the smallest error. It starts from several step sizes, so that functions that
	/// vary quickly (such as sin(1000·x)) are resolved, and reports an error when no estimate is
	/// reliable, which happens where the function is not differentiable.
	/// </summary>
	public static double Derivative(Func<double, double> f, double x)
	{
		ArgumentNullException.ThrowIfNull(f);
		// Large enough that x + h differs from x even for large |x|.
		double scale = System.Math.Max(1, System.Math.Abs(x) * 1e-5);
		// Absolute starting steps, plus steps relative to x for small |x|: near a point such as
		// sqrt at x = 1e-4 the function only looks smooth on a scale smaller than x itself.
		var starts = new List<double> { 0.1 * scale, 1e-2 * scale, 1e-3 * scale, 1e-4 * scale };
		if (x != 0 && System.Math.Abs(x) < 1e-3)
			starts.AddRange([System.Math.Abs(x) / 10, System.Math.Abs(x) / 100]);

		double best = double.NaN, bestError = double.PositiveInfinity;
		foreach (double start in starts)
		{
			var (estimate, error) = Ridders(f, x, start);
			if (error < bestError)
				(best, bestError) = (estimate, error);
			if (bestError <= 1e-13 * System.Math.Max(1, System.Math.Abs(best)))
				break;
		}

		if (double.IsNaN(best) || !(bestError <= 1e-4 * System.Math.Max(1, System.Math.Abs(best))))
			throw new MathException(MathError.Domain, string.Create(System.Globalization.CultureInfo.InvariantCulture,
				$"The derivative at x = {x:G10} could not be determined; the function may not be differentiable there."));
		return best;
	}

	/// <summary>One run of Ridders' method from step <paramref name="h"/>: the best estimate and its error.</summary>
	static (double Estimate, double Error) Ridders(Func<double, double> f, double x, double h)
	{
		const int Levels = 10;
		const double Shrink = 1.4, Shrink2 = Shrink * Shrink, Safety = 2;

		var table = new double[Levels, Levels];
		table[0, 0] = (f(x + h) - f(x - h)) / (2 * h);
		double best = table[0, 0], bestError = double.MaxValue;

		for (int i = 1; i < Levels; i++)
		{
			h /= Shrink;
			table[0, i] = (f(x + h) - f(x - h)) / (2 * h);
			double factor = Shrink2;
			for (int j = 1; j <= i; j++)
			{
				table[j, i] = (table[j - 1, i] * factor - table[j - 1, i - 1]) / (factor - 1);
				factor *= Shrink2;
				double error = System.Math.Max(System.Math.Abs(table[j, i] - table[j - 1, i]), System.Math.Abs(table[j, i] - table[j - 1, i - 1]));
				if (error <= bestError)
				{
					bestError = error;
					best = table[j, i];
				}
			}
			if (System.Math.Abs(table[i, i] - table[i - 1, i - 1]) >= Safety * bestError)
				break;
		}
		return (best, bestError);
	}
}
