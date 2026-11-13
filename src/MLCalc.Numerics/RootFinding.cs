using MLCalc.Math;

namespace MLCalc.Numerics;

/// <summary>Root finding for real functions (the original <c>solve</c>, <c>isect</c> and <c>Secant</c>).</summary>
public static class RootFinding
{
	/// <summary>
	/// Finds a root of <paramref name="f"/> inside [a, b], where f(a) and f(b) have opposite signs,
	/// using Brent's method (secant and inverse quadratic interpolation safeguarded by bisection).
	/// </summary>
	public static double FindRoot(Func<double, double> f, double a, double b, double tolerance = 1e-14, int maxIterations = 200)
	{
		ArgumentNullException.ThrowIfNull(f);
		double fa = Defined(f(a), a), fb = Defined(f(b), b);
		if (fa == 0)
			return a;
		if (fb == 0)
			return b;
		if (System.Math.Sign(fa) == System.Math.Sign(fb))
			throw new MathException(MathError.NoSolution, $"f({a}) and f({b}) have the same sign.");

		if (System.Math.Abs(fa) < System.Math.Abs(fb))
		{
			(a, b) = (b, a);
			(fa, fb) = (fb, fa);
		}
		double c = a, fc = fa, d = b - a;
		bool bisected = true;

		for (int i = 0; i < maxIterations; i++)
		{
			double s;
			if (fa != fc && fb != fc)
				s = a * fb * fc / ((fa - fb) * (fa - fc)) + b * fa * fc / ((fb - fa) * (fb - fc)) + c * fa * fb / ((fc - fa) * (fc - fb));
			else
				s = b - fb * (b - a) / (fb - fa);

			double delta = 2 * double.Epsilon + tolerance * System.Math.Abs(b);
			double mid = (3 * a + b) / 4;
			bool outside = !(s > System.Math.Min(mid, b) && s < System.Math.Max(mid, b));
			if (outside
				|| (bisected && System.Math.Abs(s - b) >= System.Math.Abs(b - c) / 2)
				|| (!bisected && System.Math.Abs(s - b) >= System.Math.Abs(c - d) / 2)
				|| (bisected && System.Math.Abs(b - c) < delta)
				|| (!bisected && System.Math.Abs(c - d) < delta))
			{
				s = (a + b) / 2;
				bisected = true;
			}
			else
				bisected = false;

			double fs = Defined(f(s), s);
			d = c;
			c = b;
			fc = fb;
			if (System.Math.Sign(fa) == System.Math.Sign(fs))
			{
				a = s;
				fa = fs;
			}
			else
			{
				b = s;
				fb = fs;
			}
			if (System.Math.Abs(fa) < System.Math.Abs(fb))
			{
				(a, b) = (b, a);
				(fa, fb) = (fb, fa);
			}
			if (fb == 0 || System.Math.Abs(b - a) <= tolerance * System.Math.Max(1, System.Math.Abs(b)))
				return b;
		}
		return b;
	}

	/// <summary>
	/// Finds the root of <paramref name="f"/> closest to <paramref name="start"/>: scans outward in
	/// both directions with an increasing step, starting at <paramref name="step"/>, until the sign
	/// changes, then refines with <see cref="FindRoot(Func{double, double}, double, double, double, int)"/>.
	/// </summary>
	public static double FindRootNear(Func<double, double> f, double start, double step = 0.01, int maxSteps = 2000)
	{
		ArgumentNullException.ThrowIfNull(f);
		if (step <= 0)
			throw new ArgumentOutOfRangeException(nameof(step), "The step must be positive.");

		double f0 = f(start);
		if (f0 == 0)
			return start;

		double left = start, fLeft = f0, right = start, fRight = f0;
		double h = step;
		for (int i = 0; i < maxSteps; i++)
		{
			double nextRight = right + h, fNextRight = f(nextRight);
			if (IsSignChange(fRight, fNextRight) && TryRefine(f, right, fRight, nextRight, fNextRight, out double rightRoot))
				return rightRoot;
			double nextLeft = left - h, fNextLeft = f(nextLeft);
			if (IsSignChange(fNextLeft, fLeft) && TryRefine(f, nextLeft, fNextLeft, left, fLeft, out double leftRoot))
				return leftRoot;
			(left, fLeft, right, fRight) = (nextLeft, fNextLeft, nextRight, fNextRight);
			// Grow slowly so nearby roots are found first but far roots are still reachable.
			if (i >= 256)
				h *= 1.01;
		}
		throw new MathException(MathError.NoSolution, $"No root found near {start}.");
	}

	/// <summary>Finds x near <paramref name="start"/> where f(x) = g(x) (the original <c>isect</c>).</summary>
	public static double FindIntersection(Func<double, double> f, Func<double, double> g, double start, double step = 0.01)
	{
		ArgumentNullException.ThrowIfNull(f);
		ArgumentNullException.ThrowIfNull(g);
		try
		{
			return FindRootNear(x => f(x) - g(x), start, step);
		}
		catch (MathException ex) when (ex.Error == MathError.NoSolution)
		{
			throw new MathException(MathError.NoIntersection, $"The functions do not intersect near {start}.");
		}
	}

	/// <summary>Finds a local extremum of <paramref name="f"/> near <paramref name="start"/> (where f′(x) = 0).</summary>
	public static double FindExtremum(Func<double, double> f, double start, double step = 0.01)
	{
		ArgumentNullException.ThrowIfNull(f);
		return FindRootNear(x => Calculus.Derivative(f, x), start, step);
	}

	/// <summary>
	/// Refines a sign change to a root. Rejects poles (e.g. tan at π/2), where the function
	/// changes sign by passing through infinity: there |f| grows instead of shrinking.
	/// </summary>
	static bool TryRefine(Func<double, double> f, double a, double fa, double b, double fb, out double root)
	{
		try
		{
			root = FindRoot(f, a, b);
		}
		catch (MathException ex) when (ex.Error == MathError.Domain)
		{
			// The function is undefined somewhere in this bracket: no usable root here, keep searching.
			root = double.NaN;
			return false;
		}
		double fRoot = System.Math.Abs(f(root));
		return fRoot <= System.Math.Max(System.Math.Abs(fa), System.Math.Abs(fb));
	}

	/// <summary>Brent's method cannot continue through NaN; report it as an undefined function value.</summary>
	static double Defined(double value, double x) =>
		double.IsNaN(value)
			? throw new MathException(MathError.Domain, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"The function is not defined at x = {x:G10}."))
			: value;

	static bool IsSignChange(double a, double b) =>
		!double.IsNaN(a) && !double.IsNaN(b) && (a == 0 || b == 0 || System.Math.Sign(a) != System.Math.Sign(b))
		&& !(double.IsInfinity(a) || double.IsInfinity(b));
}
