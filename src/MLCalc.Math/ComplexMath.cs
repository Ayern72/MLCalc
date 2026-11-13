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
		return IsReal(a) && IsReal(b) ? a.Real / b.Real : a / b;
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
			if (w == Complex.Zero)
				return Complex.One;
			if (w.Real > 0)
				return Complex.Zero;
			throw new MathException(MathError.DivisionByZero, "Zero raised to a negative power is a division by zero.");
		}
		if (IsReal(z) && IsReal(w) && (z.Real >= 0 || w.Real == System.Math.Floor(w.Real)))
			return System.Math.Pow(z.Real, w.Real);
		if (IsReal(w) && w.Real == System.Math.Floor(w.Real) && System.Math.Abs(w.Real) <= 64)
		{
			int n = (int)System.Math.Abs(w.Real);
			Complex result = Complex.One, factor = z;
			for (; n > 0; n >>= 1, factor *= factor)
				if ((n & 1) == 1)
					result *= factor;
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
	public static Complex Cot(Complex z) => Divide(Cos(z), Sin(z));

	public static Complex Sinh(Complex z) => IsReal(z) ? System.Math.Sinh(z.Real) : Complex.Sinh(z);
	public static Complex Cosh(Complex z) => IsReal(z) ? System.Math.Cosh(z.Real) : Complex.Cosh(z);
	public static Complex Tanh(Complex z) => IsReal(z) ? System.Math.Tanh(z.Real) : Complex.Tanh(z);
	public static Complex Coth(Complex z) => Divide(Cosh(z), Sinh(z));

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
	/// Inverse tangent. Complex arguments use <see cref="Complex.Atan"/>, which gives the principal
	/// value on the branch cut (atan(2i) = π/2 + 0.549i, and atan(−z) = −atan(z)) and for large |z|.
	/// </summary>
	public static Complex Atan(Complex z)
	{
		if (IsReal(z))
			return System.Math.Atan(z.Real);
		if (z == I || z == -I)
			throw new MathException(MathError.Domain, "atan is undefined at ±i.");
		return Complex.Atan(z);
	}

	/// <summary>Inverse cotangent, atan(1/z); acot(0) = π/2.</summary>
	public static Complex Acot(Complex z) => z == Complex.Zero ? System.Math.PI / 2 : Atan(1 / z);

	/// <summary>Inverse hyperbolic sine, −i·asin(i·z): accurate for large |z| and odd (asinh(−z) = −asinh(z)).</summary>
	public static Complex Asinh(Complex z) => IsReal(z) ? System.Math.Asinh(z.Real) : -I * Asin(I * z);

	public static Complex Acosh(Complex z) =>
		IsReal(z) && z.Real >= 1 ? System.Math.Acosh(z.Real) : Ln(z + Sqrt(z + 1) * Sqrt(z - 1));

	public static Complex Atanh(Complex z)
	{
		if (IsReal(z) && System.Math.Abs(z.Real) < 1)
			return System.Math.Atanh(z.Real);
		if (z == 1 || z == -1)
			throw new MathException(MathError.Domain, "atanh is undefined at ±1.");
		return Ln((1 + z) / (1 - z)) / 2;
	}

	/// <summary>Inverse hyperbolic cotangent, atanh(1/z); acoth(0) = iπ/2.</summary>
	public static Complex Acoth(Complex z) => z == Complex.Zero ? I * System.Math.PI / 2 : Atanh(1 / z);
}
