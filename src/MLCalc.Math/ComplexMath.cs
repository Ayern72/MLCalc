using System.Numerics;

namespace MLCalc.Math;

/// <summary>
/// Complex scalar functions. Built on <see cref="Complex"/>, adding the functions it lacks and
/// keeping results exactly real whenever the mathematically correct result is real.
/// </summary>
public static class ComplexMath
{
	public static readonly Complex I = Complex.ImaginaryOne;

	public static bool IsReal(Complex z) => z.Imaginary == 0;

	public static double Abs(Complex z) => Complex.Abs(z);

	/// <summary>
	/// Argument in (-π, π]; 0 for z = 0. A zero imaginary part counts as +0 even when it is -0, so
	/// negative reals have argument π (not -π) and logarithms and roots stay on the principal branch.
	/// </summary>
	public static double Arg(Complex z) =>
		z == Complex.Zero ? 0 : System.Math.Atan2(z.Imaginary == 0 ? 0.0 : z.Imaginary, z.Real);

	/// <summary>Negation that keeps real numbers exactly real (no -0 imaginary part).</summary>
	public static Complex Negate(Complex z) => IsReal(z) ? -z.Real : -z;

	public static Complex Conj(Complex z) => Complex.Conjugate(z);

	/// <summary>
	/// Product that stays exactly real for real operands, so that e.g. ∞·2 is ∞ rather than ∞ + NaN·i
	/// (complex multiplication would compute ∞·0 for the imaginary part).
	/// </summary>
	public static Complex Multiply(Complex a, Complex b) =>
		IsReal(a) && IsReal(b) ? a.Real * b.Real : a * b;

	public static Complex Divide(Complex a, Complex b)
	{
		if (b == Complex.Zero)
			throw new MathException(MathError.DivisionByZero, "Division by zero.");
		if (IsReal(a) && IsReal(b))
			return a.Real / b.Real;
		// Complex division overflows inside when parts are near the largest double, so that
		// (1e308 + 1e308i)/(1e308 + 1e308i) would be NaN; scaling both by 2⁻⁶⁴ keeps the quotient.
		double largest = System.Math.Max(MaxPart(a), MaxPart(b));
		if (largest > 1e300 && double.IsFinite(largest))
		{
			a = new Complex(System.Math.ScaleB(a.Real, -64), System.Math.ScaleB(a.Imaginary, -64));
			b = new Complex(System.Math.ScaleB(b.Real, -64), System.Math.ScaleB(b.Imaginary, -64));
		}
		return a / b;
	}

	static double MaxPart(Complex z) => System.Math.Max(System.Math.Abs(z.Real), System.Math.Abs(z.Imaginary));

	/// <summary>Above this size, the inverse hyperbolic functions use their asymptotic forms, where the exact formulas overflow.</summary>
	const double Huge = 1e150;

	/// <summary>ln z for any size of z: |z| itself may overflow, so the largest part is taken out first.</summary>
	static Complex LnOfHuge(Complex z)
	{
		double scale = MaxPart(z);
		if (double.IsInfinity(scale))
			return new Complex(double.PositiveInfinity, Arg(z));
		return new Complex(System.Math.Log(scale) + System.Math.Log(Complex.Abs(new Complex(z.Real / scale, z.Imaginary / scale))), Arg(z));
	}

	public static Complex Exp(Complex z) => IsReal(z) ? System.Math.Exp(z.Real) : Complex.Exp(z);

	/// <summary>Natural logarithm (principal branch).</summary>
	public static Complex Ln(Complex z)
	{
		if (z == Complex.Zero)
			throw new MathException(MathError.Domain, "Logarithm of zero.");
		return IsReal(z) && z.Real > 0 ? System.Math.Log(z.Real) : new Complex(System.Math.Log(Abs(z)), Arg(z));
	}

	/// <summary>Base-10 logarithm.</summary>
	public static Complex Log10(Complex z) => IsReal(z) && z.Real > 0 ? System.Math.Log10(z.Real) : Ln(z) / System.Math.Log(10);

	/// <summary>
	/// z^w; real when z ≥ 0, or when z is a negative real and w an integer. Small integer powers of
	/// complex numbers use exact repeated multiplication, so i^2 is exactly -1.
	/// </summary>
	public static Complex Pow(Complex z, Complex w)
	{
		// Zero first: 0^-1 is a division by zero, as 1/0 is (Math.Pow would give infinity).
		if (z == Complex.Zero)
		{
			if (Complex.IsNaN(w))
				return double.NaN;
			if (w == Complex.Zero)
				return Complex.One;
			if (w.Real > 0)
				return Complex.Zero;
			throw new MathException(MathError.DivisionByZero, "Zero raised to a negative power is a division by zero.");
		}
		if (IsReal(z) && IsReal(w) && (z.Real >= 0 || w.Real == System.Math.Floor(w.Real)))
			return System.Math.Pow(z.Real, w.Real);
		if (IsReal(z) && IsReal(w) && !double.IsNaN(w.Real))
		{
			// A negative real to a fractional power: |z|^w·e^(iπw). SinPi and CosPi are exact at
			// half-integers, so (-4)^0.5 is exactly 2i, and a zero part stays 0 even when |z|^w is
			// infinite: (-Inf)^0.5 is 0 + Inf·i.
			double magnitude = System.Math.Pow(-z.Real, w.Real), cos = double.CosPi(w.Real), sin = double.SinPi(w.Real);
			return new Complex(cos == 0 ? 0 : magnitude * cos, sin == 0 ? 0 : magnitude * sin);
		}
		if (IsReal(w) && w.Real == System.Math.Floor(w.Real) && System.Math.Abs(w.Real) <= 64)
		{
			// Multiply keeps real intermediate results real, so (1e200·i)^2 is -Inf rather than -Inf + NaN·i.
			int n = (int)System.Math.Abs(w.Real);
			Complex result = Complex.One, factor = z;
			for (; n > 0; n >>= 1, factor = Multiply(factor, factor))
				if ((n & 1) == 1)
					result = Multiply(result, factor);
			return w.Real < 0 ? 1 / result : result;
		}
		return Exp(w * Ln(z));
	}

	public static Complex Sqrt(Complex z)
	{
		if (IsReal(z))
			return z.Real >= 0 ? System.Math.Sqrt(z.Real) : new Complex(0, System.Math.Sqrt(-z.Real));
		return Complex.Sqrt(z);
	}

	/// <summary>The n-th root of z (the original <c>xroot</c>); the real root for odd n and negative real z.</summary>
	public static Complex Root(Complex z, Complex n)
	{
		if (IsReal(z) && IsReal(n) && z.Real < 0 && System.Math.Abs(n.Real % 2) == 1)
			return -System.Math.Pow(-z.Real, 1 / n.Real);
		return Pow(z, Divide(1, n));
	}

	public static Complex Sin(Complex z) => IsReal(z) ? System.Math.Sin(z.Real) : Complex.Sin(z);
	public static Complex Cos(Complex z) => IsReal(z) ? System.Math.Cos(z.Real) : Complex.Cos(z);
	public static Complex Tan(Complex z) => IsReal(z) ? System.Math.Tan(z.Real) : Complex.Tan(z);

	/// <summary>
	/// Cotangent as 1/tan(z): cos(z)/sin(z) would divide two overflowing values for |Im z| &gt; 710
	/// and give NaN, while tan(z) tends to ±i there.
	/// </summary>
	public static Complex Cot(Complex z) => Divide(1, Tan(z));

	public static Complex Sinh(Complex z) => IsReal(z) ? System.Math.Sinh(z.Real) : Complex.Sinh(z);
	public static Complex Cosh(Complex z) => IsReal(z) ? System.Math.Cosh(z.Real) : Complex.Cosh(z);
	public static Complex Tanh(Complex z) => IsReal(z) ? System.Math.Tanh(z.Real) : Complex.Tanh(z);

	/// <summary>
	/// Hyperbolic cotangent as 1/tanh(z): cosh(z)/sinh(z) would divide two overflowing values for
	/// |Re z| &gt; 710 and give NaN, while tanh(z) tends to ±1 there.
	/// </summary>
	public static Complex Coth(Complex z) => Divide(1, Tanh(z));

	/// <summary>
	/// Inverse sine. For real |x| &gt; 1 the result is sign(x)·(π/2 − i·acosh|x|), as in MATLAB;
	/// complex arguments use <see cref="Complex.Asin"/>, which stays accurate for large |z|
	/// (a textbook formula such as −i·ln(iz + √(1−z²)) cancels or overflows there).
	/// </summary>
	public static Complex Asin(Complex z)
	{
		if (!IsReal(z))
			return Complex.Asin(z);
		double x = z.Real;
		if (double.IsNaN(x) || System.Math.Abs(x) <= 1)
			return System.Math.Asin(x);
		return System.Math.Sign(x) * new Complex(System.Math.PI / 2, -System.Math.Acosh(System.Math.Abs(x)));
	}

	/// <summary>Inverse cosine, π/2 − asin(z) for real |x| &gt; 1; <see cref="Complex.Acos"/> for complex z.</summary>
	public static Complex Acos(Complex z)
	{
		if (!IsReal(z))
			return Complex.Acos(z);
		double x = z.Real;
		return double.IsNaN(x) || System.Math.Abs(x) <= 1 ? System.Math.Acos(x) : System.Math.PI / 2 - Asin(z);
	}

	/// <summary>
	/// Inverse tangent, −i·atanh(i·z) for complex z, so it has the accuracy of <see cref="Atanh"/>
	/// (<see cref="Complex.Atan"/> loses half the digits when the result is small, e.g. atan(1e10·i)).
	/// It is odd, atan(−z) = −atan(z), also on the branch cut: atan(2i) = π/2 + 0.549i and
	/// atan(−2i) = −π/2 − 0.549i.
	/// </summary>
	public static Complex Atan(Complex z)
	{
		if (IsReal(z))
			return System.Math.Atan(z.Real);
		if (z == I || z == -I)
			throw new MathException(MathError.Domain, "atan is undefined at ±i.");
		if (z.Real < 0 || (z.Real == 0 && z.Imaginary < 0))
			return -Atan(-z);
		// i·z and −i·w written out, so that infinite parts do not meet 0·∞.
		var w = Atanh(new Complex(-z.Imaginary, z.Real));
		return new Complex(w.Imaginary, -w.Real);
	}

	/// <summary>Inverse cotangent, atan(1/z); acot(0) = π/2.</summary>
	public static Complex Acot(Complex z) => z == Complex.Zero ? System.Math.PI / 2 : Atan(1 / z);

	/// <summary>
	/// Inverse hyperbolic sine, −i·asin(i·z): accurate for large |z| and odd (asinh(−z) = −asinh(z)).
	/// For huge |z| it is ln(2z) (with the sign of Re z), to which the exact value is equal in double precision.
	/// </summary>
	public static Complex Asinh(Complex z)
	{
		if (IsReal(z))
			return System.Math.Asinh(z.Real);
		if (MaxPart(z) > Huge)
			return z.Real < 0 || (z.Real == 0 && z.Imaginary < 0) ? -Asinh(-z) : System.Math.Log(2) + LnOfHuge(z);
		// i·z and −i·w written out, so that infinite parts do not meet 0·∞.
		var w = Asin(new Complex(-z.Imaginary, z.Real));
		return new Complex(w.Imaginary, -w.Real);
	}

	/// <summary>Inverse hyperbolic cosine; ln(2z) for huge |z|, where z + √(z+1)·√(z−1) overflows.</summary>
	public static Complex Acosh(Complex z)
	{
		if (IsReal(z) && z.Real >= 1)
			return System.Math.Acosh(z.Real);
		if (MaxPart(z) > Huge)
			return System.Math.Log(2) + LnOfHuge(z);
		return Ln(z + Sqrt(z + 1) * Sqrt(z - 1));
	}

	/// <summary>
	/// Inverse hyperbolic tangent. Complex arguments use Kahan's formulas
	/// Re = ¼·log1p(4x/((1−x)² + y²)) and Im = ½·atan2(2y, (1−x)(1+x) − y²), which stay accurate when
	/// the result is small; ½·ln((1+z)/(1−z)) cancels there and loses half the digits. On the real
	/// axis beyond ±1 the imaginary part is +π/2, as in MATLAB: atanh(2) = 0.549 + 1.571i.
	/// </summary>
	public static Complex Atanh(Complex z)
	{
		if (IsReal(z) && System.Math.Abs(z.Real) < 1)
			return System.Math.Atanh(z.Real);
		if (z == 1 || z == -1)
			throw new MathException(MathError.Domain, "atanh is undefined at ±1.");

		double x = z.Real, y = z.Imaginary == 0 ? 0.0 : z.Imaginary;
		if (double.IsNaN(x) || double.IsNaN(y))
			return new Complex(double.NaN, double.NaN);
		double halfPi = y < 0 ? -System.Math.PI / 2 : System.Math.PI / 2;

		// For huge |z| the squares overflow; there atanh(z) = 1/z ± iπ/2 to full precision.
		double scale = System.Math.Max(System.Math.Abs(x), System.Math.Abs(y));
		if (scale > 1e150)
		{
			if (double.IsInfinity(scale))
				return new Complex(0, halfPi);
			double xs = x / scale, ys = y / scale;
			return new Complex(xs / (xs * xs + ys * ys) / scale, halfPi);
		}

		double oneMinusX = 1 - x;
		double re = Log1P(4 * x / (oneMinusX * oneMinusX + y * y)) / 4;
		double im = System.Math.Atan2(2 * y, oneMinusX * (1 + x) - y * y) / 2;
		return new Complex(re, im);
	}

	/// <summary>
	/// ln(1 + x), accurate also for tiny x (Goldberg's method: the rounding error of 1 + x cancels
	/// in the ratio). <c>double.LogP1</c> computes ln(1 + x) directly and loses the digits of small x.
	/// </summary>
	static double Log1P(double x)
	{
		double u = 1 + x;
		return u == 1 ? x : System.Math.Log(u) * x / (u - 1);
	}

	/// <summary>Inverse hyperbolic cotangent, atanh(1/z); acoth(0) = iπ/2.</summary>
	public static Complex Acoth(Complex z) => z == Complex.Zero ? I * System.Math.PI / 2 : Atanh(1 / z);
}
