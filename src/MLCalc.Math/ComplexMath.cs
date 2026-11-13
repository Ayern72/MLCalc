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

	/// <summary>Argument in (-π, π]; 0 for z = 0.</summary>
	public static double Arg(Complex z) => z == Complex.Zero ? 0 : System.Math.Atan2(z.Imaginary, z.Real);

	public static Complex Conj(Complex z) => Complex.Conjugate(z);

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

	/// <summary>z^w; real when z ≥ 0, or when z is a negative real and w an integer.</summary>
	public static Complex Pow(Complex z, Complex w)
	{
		if (IsReal(z) && IsReal(w) && (z.Real >= 0 || w.Real == System.Math.Floor(w.Real)))
			return System.Math.Pow(z.Real, w.Real);
		if (z == Complex.Zero)
			return w.Real > 0 ? Complex.Zero : throw new MathException(MathError.DivisionByZero, "Zero raised to a non-positive power.");
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

	public static Complex Asin(Complex z) =>
		IsReal(z) && System.Math.Abs(z.Real) <= 1 ? System.Math.Asin(z.Real) : -I * Ln(I * z + Sqrt(1 - z * z));

	public static Complex Acos(Complex z) =>
		IsReal(z) && System.Math.Abs(z.Real) <= 1 ? System.Math.Acos(z.Real) : System.Math.PI / 2 - Asin(z);

	public static Complex Atan(Complex z)
	{
		if (IsReal(z))
			return System.Math.Atan(z.Real);
		if (z == I || z == -I)
			throw new MathException(MathError.Domain, "atan is undefined at ±i.");
		return I / 2 * Ln((I + z) / (I - z));
	}

	/// <summary>Inverse cotangent, atan(1/z); acot(0) = π/2.</summary>
	public static Complex Acot(Complex z) => z == Complex.Zero ? System.Math.PI / 2 : Atan(1 / z);

	public static Complex Asinh(Complex z) => IsReal(z) ? System.Math.Asinh(z.Real) : Ln(z + Sqrt(z * z + 1));

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
