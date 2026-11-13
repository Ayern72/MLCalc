using System.Globalization;
using System.Numerics;
using System.Text;
using MLCalc.Math;

namespace MLCalc.LinearAlgebra;

/// <summary>
/// An immutable dense matrix of complex numbers, stored row-major. This is MLCalc's single value
/// type: scalars are 1×1 matrices and real numbers have a zero imaginary part.
/// </summary>
public sealed class ComplexMatrix : IEquatable<ComplexMatrix>
{
	readonly Complex[] data;

	public int Rows { get; }
	public int Columns { get; }
	public int Count => data.Length;

	ComplexMatrix(int rows, int columns, Complex[] data)
	{
		Rows = rows;
		Columns = columns;
		this.data = data;
	}

	/// <summary>Creates a matrix from row-major data. The array is copied.</summary>
	public static ComplexMatrix Create(int rows, int columns, ReadOnlySpan<Complex> rowMajorData)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(rows);
		ArgumentOutOfRangeException.ThrowIfNegative(columns);
		if (rowMajorData.Length != rows * columns)
			throw new ArgumentException($"Expected {rows * columns} elements, got {rowMajorData.Length}.", nameof(rowMajorData));
		return new ComplexMatrix(rows, columns, rowMajorData.ToArray());
	}

	public static ComplexMatrix Create(int rows, int columns, Func<int, int, Complex> element)
	{
		var values = new Complex[rows * columns];
		for (int r = 0; r < rows; r++)
			for (int c = 0; c < columns; c++)
				values[r * columns + c] = element(r, c);
		return new ComplexMatrix(rows, columns, values);
	}

	public static ComplexMatrix FromRows(Complex[,] values) =>
		Create(values.GetLength(0), values.GetLength(1), (r, c) => values[r, c]);

	public static ComplexMatrix FromRows(double[,] values) =>
		Create(values.GetLength(0), values.GetLength(1), (r, c) => values[r, c]);

	public static ComplexMatrix Scalar(Complex value) => new(1, 1, [value]);

	public static ComplexMatrix RowVector(params ReadOnlySpan<double> values)
	{
		var array = new Complex[values.Length];
		for (int i = 0; i < values.Length; i++)
			array[i] = values[i];
		return new ComplexMatrix(1, values.Length, array);
	}

	public static ComplexMatrix ColumnVector(params ReadOnlySpan<double> values) => RowVector(values).Transpose();

	public static ComplexMatrix Zeros(int rows, int columns) => new(rows, columns, new Complex[rows * columns]);

	public static ComplexMatrix Ones(int rows, int columns) => Create(rows, columns, (_, _) => Complex.One);

	public static ComplexMatrix Identity(int size) => Create(size, size, (r, c) => r == c ? Complex.One : Complex.Zero);

	public static implicit operator ComplexMatrix(Complex value) => Scalar(value);
	public static implicit operator ComplexMatrix(double value) => Scalar(value);

	/// <summary>Element at zero-based row and column.</summary>
	public Complex this[int row, int column]
	{
		get
		{
			if ((uint)row >= (uint)Rows || (uint)column >= (uint)Columns)
				throw new MathException(MathError.Index, $"Index ({row + 1},{column + 1}) is outside a {Rows}×{Columns} matrix.");
			return data[row * Columns + column];
		}
	}

	/// <summary>Element at zero-based row-major linear index.</summary>
	public Complex this[int index]
	{
		get
		{
			if ((uint)index >= (uint)data.Length)
				throw new MathException(MathError.Index, $"Index {index + 1} is outside a matrix with {data.Length} elements.");
			return data[index];
		}
	}

	public ReadOnlySpan<Complex> AsSpan() => data;

	public bool IsScalar => Rows == 1 && Columns == 1;
	public bool IsEmpty => data.Length == 0;
	public bool IsSquare => Rows == Columns;
	public bool IsVector => Rows == 1 || Columns == 1;
	public bool IsReal => data.All(ComplexMath.IsReal);

	/// <summary>The single element of a 1×1 matrix.</summary>
	public Complex ScalarValue => IsScalar
		? data[0]
		: throw new MathException(MathError.Dimension, $"Expected a scalar, got a {Rows}×{Columns} matrix.");

	/// <summary>Joins matrices side by side (<c>[A, B]</c>). Empty matrices are ignored; the rest must have equal row counts.</summary>
	public static ComplexMatrix HorizontalConcat(IEnumerable<ComplexMatrix> parts)
	{
		var list = parts.Where(p => !p.IsEmpty).ToList();
		if (list.Count == 0)
			return Zeros(0, 0);
		if (list.Count == 1)
			return list[0];
		int rows = list[0].Rows;
		if (list.Any(p => p.Rows != rows))
			throw new MathException(MathError.Dimension, "Matrices placed side by side must have the same number of rows.");
		int columns = list.Sum(p => p.Columns);
		var result = new Complex[rows * columns];
		int offset = 0;
		foreach (var part in list)
		{
			for (int r = 0; r < rows; r++)
				part.data.AsSpan(r * part.Columns, part.Columns).CopyTo(result.AsSpan(r * columns + offset));
			offset += part.Columns;
		}
		return new ComplexMatrix(rows, columns, result);
	}

	/// <summary>Stacks matrices on top of each other (<c>[A; B]</c>). Empty matrices are ignored; the rest must have equal column counts.</summary>
	public static ComplexMatrix VerticalConcat(IEnumerable<ComplexMatrix> parts)
	{
		var list = parts.Where(p => !p.IsEmpty).ToList();
		if (list.Count == 0)
			return Zeros(0, 0);
		if (list.Count == 1)
			return list[0];
		int columns = list[0].Columns;
		if (list.Any(p => p.Columns != columns))
			throw new MathException(MathError.Dimension, "Matrices stacked vertically must have the same number of columns.");
		return new ComplexMatrix(list.Sum(p => p.Rows), columns, list.SelectMany(p => p.data).ToArray());
	}

	public ComplexMatrix Row(int row) => Create(1, Columns, (_, c) => this[row, c]);
	public ComplexMatrix Column(int column) => Create(Rows, 1, (r, _) => this[r, column]);

	/// <summary>Applies a function to every element.</summary>
	public ComplexMatrix Map(Func<Complex, Complex> function)
	{
		var result = new Complex[data.Length];
		for (int i = 0; i < data.Length; i++)
			result[i] = function(data[i]);
		return new ComplexMatrix(Rows, Columns, result);
	}

	/// <summary>
	/// Combines two matrices element by element. Matrices must have the same size, or one of them
	/// must be a scalar, which is then paired with every element of the other.
	/// </summary>
	public static ComplexMatrix ElementWise(ComplexMatrix a, ComplexMatrix b, Func<Complex, Complex, Complex> function)
	{
		if (a.Rows == b.Rows && a.Columns == b.Columns)
		{
			var result = new Complex[a.Count];
			for (int i = 0; i < result.Length; i++)
				result[i] = function(a.data[i], b.data[i]);
			return new ComplexMatrix(a.Rows, a.Columns, result);
		}
		if (a.IsScalar)
		{
			var x = a.data[0];
			return b.Map(y => function(x, y));
		}
		if (b.IsScalar)
		{
			var y = b.data[0];
			return a.Map(x => function(x, y));
		}
		throw DimensionMismatch(a, b);
	}

	public static ComplexMatrix operator +(ComplexMatrix a, ComplexMatrix b) => ElementWise(a, b, (x, y) => x + y);
	public static ComplexMatrix operator -(ComplexMatrix a, ComplexMatrix b) => ElementWise(a, b, (x, y) => x - y);
	public static ComplexMatrix operator -(ComplexMatrix a) => a.Map(ComplexMath.Negate);

	public ComplexMatrix ElementMultiply(ComplexMatrix other) => ElementWise(this, other, ComplexMath.Multiply);
	public ComplexMatrix ElementDivide(ComplexMatrix other) => ElementWise(this, other, ComplexMath.Divide);
	public ComplexMatrix ElementPower(ComplexMatrix other) => ElementWise(this, other, ComplexMath.Pow);

	/// <summary>Matrix product. A scalar operand multiplies every element.</summary>
	public static ComplexMatrix operator *(ComplexMatrix a, ComplexMatrix b)
	{
		if (a.IsScalar || b.IsScalar)
			return a.ElementMultiply(b);
		if (a.Columns != b.Rows)
			throw new MathException(MathError.Dimension, $"Cannot multiply a {a.Rows}×{a.Columns} matrix by a {b.Rows}×{b.Columns} matrix.");
		var result = new Complex[a.Rows * b.Columns];
		for (int r = 0; r < a.Rows; r++)
			for (int c = 0; c < b.Columns; c++)
			{
				Complex sum = Complex.Zero;
				for (int k = 0; k < a.Columns; k++)
					sum += ComplexMath.Multiply(a.data[r * a.Columns + k], b.data[k * b.Columns + c]);
				result[r * b.Columns + c] = sum;
			}
		return new ComplexMatrix(a.Rows, b.Columns, result);
	}

	/// <summary>
	/// Matrix power (MATLAB <c>A^p</c>): the scalar power for 1×1 operands; for a square matrix an
	/// integer power by repeated squaring, where a negative power uses the inverse.
	/// </summary>
	public static ComplexMatrix Power(ComplexMatrix a, ComplexMatrix p)
	{
		if (a.IsScalar && p.IsScalar)
			return ComplexMath.Pow(a.ScalarValue, p.ScalarValue);
		if (!p.IsScalar)
			throw new MathException(MathError.Dimension, "The exponent of a matrix power must be a scalar; use .^ for element-wise powers.");
		if (!a.IsSquare)
			throw new MathException(MathError.Dimension, $"Matrix power needs a square matrix, got {a.Rows}×{a.Columns}; use .^ for element-wise powers.");
		var exponent = p.ScalarValue;
		if (!ComplexMath.IsReal(exponent) || exponent.Real != System.Math.Floor(exponent.Real) || System.Math.Abs(exponent.Real) > int.MaxValue)
			throw new MathException(MathError.Domain, "A matrix can only be raised to an integer power.");

		long n = (long)exponent.Real;
		var factor = n < 0 ? a.Inverse() : a;
		n = System.Math.Abs(n);
		var result = Identity(a.Rows);
		while (n > 0)
		{
			if ((n & 1) == 1)
				result *= factor;
			n >>= 1;
			if (n > 0)
				factor *= factor;
		}
		return result;
	}

	/// <summary>Plain transpose (no conjugation): MATLAB <c>.'</c>, and the original MLCalc <c>'</c>.</summary>
	public ComplexMatrix Transpose() => Create(Columns, Rows, (r, c) => data[c * Columns + r]);

	public ComplexMatrix ConjugateTranspose() => Transpose().Map(Complex.Conjugate);

	/// <summary>Solves A·X = B for X (MATLAB <c>A\B</c>).</summary>
	public static ComplexMatrix LeftDivide(ComplexMatrix a, ComplexMatrix b) =>
		a.IsScalar ? b.ElementDivide(a) : LinearSolver.Solve(a, b);

	/// <summary>Solves X·B = A for X (MATLAB <c>A/B</c>).</summary>
	public static ComplexMatrix RightDivide(ComplexMatrix a, ComplexMatrix b) =>
		b.IsScalar ? a.ElementDivide(b) : LinearSolver.Solve(b.Transpose(), a.Transpose()).Transpose();

	public Complex Determinant() => LinearSolver.Determinant(this);

	public ComplexMatrix Inverse() => LinearSolver.Solve(this, Identity(Rows));

	static MathException DimensionMismatch(ComplexMatrix a, ComplexMatrix b) =>
		new(MathError.Dimension, $"Matrix dimensions do not agree: {a.Rows}×{a.Columns} and {b.Rows}×{b.Columns}.");

	public bool Equals(ComplexMatrix? other) =>
		other is not null && Rows == other.Rows && Columns == other.Columns && data.AsSpan().SequenceEqual(other.data);

	public override bool Equals(object? obj) => Equals(obj as ComplexMatrix);

	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(Rows);
		hash.Add(Columns);
		foreach (var value in data)
			hash.Add(value);
		return hash.ToHashCode();
	}

	/// <summary>True when the matrices have the same size and all elements differ by at most <paramref name="tolerance"/>.</summary>
	public bool ApproximatelyEquals(ComplexMatrix other, double tolerance)
	{
		if (Rows != other.Rows || Columns != other.Columns)
			return false;
		for (int i = 0; i < data.Length; i++)
			if (Complex.Abs(data[i] - other.data[i]) > tolerance)
				return false;
		return true;
	}

	public override string ToString()
	{
		if (IsScalar)
			return Format(data[0]);
		var builder = new StringBuilder();
		for (int r = 0; r < Rows; r++)
		{
			if (r > 0)
				builder.AppendLine();
			for (int c = 0; c < Columns; c++)
			{
				if (c > 0)
					builder.Append('\t');
				builder.Append(Format(data[r * Columns + c]));
			}
		}
		return builder.ToString();
	}

	/// <summary>Formats a complex number as "a", "bi" or "a+bi" using invariant culture.</summary>
	public static string Format(Complex value, string numberFormat = "G10")
	{
		string Number(double x) => double.IsPositiveInfinity(x) ? "Inf"
			: double.IsNegativeInfinity(x) ? "-Inf"
			: x.ToString(numberFormat, CultureInfo.InvariantCulture);

		string re = Number(value.Real);
		if (value.Imaginary == 0)
			return re;
		string im = Number(System.Math.Abs(value.Imaginary));
		if (value.Real == 0)
			return (value.Imaginary < 0 ? "-" : "") + im + "i";
		return re + (value.Imaginary < 0 ? "-" : "+") + im + "i";
	}
}
