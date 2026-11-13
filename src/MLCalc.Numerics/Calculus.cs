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
			if (halving >= 3 && System.Math.Abs(estimate - lastEstimate) <= relativeTolerance * System.Math.Max(1, System.Math.Abs(estimate)))
				return estimate;
			lastSimpson = simpson;
			lastEstimate = estimate;
		}
		return lastEstimate;
	}

	/// <summary>
	/// Derivative of <paramref name="f"/> at <paramref name="x"/> (the original <c>Derivata</c>), using
	/// Ridders' method: central differences with shrinking steps, extrapolated to step zero, keeping
	/// the estimate with the smallest error.
	/// </summary>
	public static double Derivative(Func<double, double> f, double x)
	{
		ArgumentNullException.ThrowIfNull(f);
		const int Levels = 10;
		const double Shrink = 1.4, Shrink2 = Shrink * Shrink, Safety = 2;

		// Large enough that x + h differs from x even for large |x|.
		double h = System.Math.Max(0.1, System.Math.Abs(x) * 1e-6);
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
		return best;
	}
}
