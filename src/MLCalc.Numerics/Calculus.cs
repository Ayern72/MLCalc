using MLCalc.Math;

namespace MLCalc.Numerics;

/// <summary>Numerical integration and differentiation of real functions.</summary>
public static class Calculus
{
	/// <summary>
	/// Definite integral of <paramref name="f"/> from <paramref name="a"/> to <paramref name="b"/> by
	/// adaptive Gauss–Kronrod quadrature (7 and 15 points, as in QUADPACK's QAG): the interval with
	/// the largest error estimate is halved until the estimated total error is below the tolerance.
	/// The work goes where it is needed, so a jump or a kink costs a few dozen extra subintervals
	/// around it; the earlier Simpson rule, which halved every subinterval at once, needed 33 million
	/// function values for a jump and still missed the tolerance.
	/// </summary>
	/// <param name="f">The integrand.</param>
	/// <param name="a">Lower limit.</param>
	/// <param name="b">Upper limit (may be less than <paramref name="a"/>).</param>
	/// <param name="relativeTolerance">The estimated error must be at most this times max(1, |result|).</param>
	/// <param name="maxIntervals">The most subintervals to use (15 function values each) before giving up.</param>
	public static double Integrate(Func<double, double> f, double a, double b, double relativeTolerance = 1e-12, int maxIntervals = 100_000)
	{
		ArgumentNullException.ThrowIfNull(f);
		if (!double.IsFinite(a) || !double.IsFinite(b))
			throw new MathException(MathError.Domain, "The limits of an integral must be finite numbers.");
		if (a == b)
			return 0;
		if (a > b)
			return -Integrate(f, b, a, relativeTolerance, maxIntervals);

		// Largest error first.
		var intervals = new PriorityQueue<Segment, double>(Comparer<double>.Create((x, y) => y.CompareTo(x)));
		var whole = Kronrod(f, a, b);
		intervals.Enqueue(whole, whole.Error);
		double total = whole.Value, error = whole.Error;

		while (true)
		{
			if (error <= relativeTolerance * System.Math.Max(1, System.Math.Abs(total)))
			{
				// The running sums can drift as large terms are added and removed; confirm with fresh ones.
				(total, error) = (0, 0);
				foreach (var (segment, _) in intervals.UnorderedItems)
				{
					total += segment.Value;
					error += segment.Error;
				}
				if (error <= relativeTolerance * System.Math.Max(1, System.Math.Abs(total)))
					return total;
			}

			var worst = intervals.Dequeue();
			double mid = worst.A + (worst.B - worst.A) / 2;
			if (intervals.Count + 2 > maxIntervals || !(worst.A < mid && mid < worst.B))
				throw new MathException(MathError.NoSolution,
					"The integral did not converge; the function may be singular or oscillate strongly on the interval.");
			var left = Kronrod(f, worst.A, mid);
			var right = Kronrod(f, mid, worst.B);
			total += left.Value + right.Value - worst.Value;
			error += left.Error + right.Error - worst.Error;
			intervals.Enqueue(left, left.Error);
			intervals.Enqueue(right, right.Error);
		}
	}

	readonly record struct Segment(double A, double B, double Value, double Error);

	// Kronrod nodes on [0, 1] (the last is the centre); the Gauss nodes are those with odd index.
	static readonly double[] KronrodNodes =
	[
		0.991455371120812639206854697526329, 0.949107912342758524526189684047851,
		0.864864423359769072789712788640926, 0.741531185599394439863864773280788,
		0.586087235467691130294144845693013, 0.405845151377397166906606412076961,
		0.207784955007898467600689403773245, 0,
	];

	static readonly double[] KronrodWeights =
	[
		0.022935322010529224963732008058970, 0.063092092629978553290700663189204,
		0.104790010322250183839876322541518, 0.140653259715525918745189590510238,
		0.169004726639267902826583426598550, 0.190350578064785409913256402421014,
		0.204432940075298892414161999234649, 0.209482141084727828012999174891714,
	];

	static readonly double[] GaussWeights =
	[
		0.129484966168869693270611432679082, 0.279705391489276667901467771423780,
		0.381830050505118944950369775488975, 0.417959183673469387755102040816327,
	];

	/// <summary>The 15-point Kronrod estimate on [a, b], with its difference from the 7-point Gauss estimate as the error.</summary>
	static Segment Kronrod(Func<double, double> f, double a, double b)
	{
		double centre = a + (b - a) / 2, half = (b - a) / 2;
		double fc = f(centre);
		double kronrod = KronrodWeights[7] * fc, gauss = GaussWeights[3] * fc;
		for (int j = 0; j < 7; j++)
		{
			double dx = half * KronrodNodes[j];
			double sum = f(centre - dx) + f(centre + dx);
			kronrod += KronrodWeights[j] * sum;
			if (j % 2 == 1)
				gauss += GaussWeights[j / 2] * sum;
		}
		double value = kronrod * half;
		if (double.IsNaN(value))
			throw new MathException(MathError.Domain, "The function is not defined everywhere on the interval.");
		if (double.IsInfinity(value))
			throw new MathException(MathError.NoSolution,
				"The integral did not converge; the function may be singular or oscillate strongly on the interval.");
		return new Segment(a, b, value, System.Math.Abs((kronrod - gauss) * half));
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

		if (double.IsNaN(best) || !(bestError <= 1e-4 * System.Math.Max(1, System.Math.Abs(best))) || HasKink(f, x, scale, best))
			throw new MathException(MathError.Domain, string.Create(System.Globalization.CultureInfo.InvariantCulture,
				$"The derivative at x = {x:G10} could not be determined; the function may not be differentiable there."));
		return best;
	}

	/// <summary>
	/// True when the slopes on the two sides of <paramref name="x"/> differ. Central differences cannot
	/// see a kink that is symmetric about x, such as abs(x) at 0: the two sides cancel exactly and the
	/// estimate looks perfect. One-sided differences differ there by the jump in slope however small
	/// the step, while for a smooth function they differ by about f''(x)·h, which shrinks with h.
	/// </summary>
	static bool HasKink(Func<double, double> f, double x, double scale, double slope)
	{
		double Gap(double h, out double noise)
		{
			double f0 = f(x), right = f(x + h), left = f(x - h);
			// The rounding error of the differences, from the size of the function values.
			noise = 4 * 2.220446049250313e-16 * System.Math.Max(System.Math.Abs(f0), System.Math.Max(System.Math.Abs(right), System.Math.Abs(left))) / h;
			return (right - f0) / h - (f0 - left) / h;
		}

		double coarse = Gap(1e-6 * scale, out _);
		double fine = Gap(1e-7 * scale, out double noise);
		return System.Math.Abs(fine) > System.Math.Max(1e-4 * System.Math.Max(1, System.Math.Abs(slope)), 10 * noise)
			&& System.Math.Abs(fine) > 0.5 * System.Math.Abs(coarse);
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
