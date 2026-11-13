using System.Numerics;
using MLCalc.LinearAlgebra;
using MLCalc.Math;

namespace MLCalc.Interpreter;

/// <summary>
/// MATLAB-style indexing with one-based indices: <c>x(k)</c> uses column-major linear indexing,
/// <c>x(r, c)</c> selects rows and columns, and <c>:</c> selects all. Assignment grows the matrix
/// as needed, filling new elements with zeros.
/// </summary>
internal static class Indexing
{
	public static ComplexMatrix Get(ComplexMatrix source, IndexArgument[] indices)
	{
		switch (indices.Length)
		{
			case 0:
				return source;
			case 1:
			{
				var index = indices[0];
				int[] linear = index.IsAll ? Enumerable.Range(0, source.Count).ToArray() : ToZeroBased(index, source.Count, "Index");
				var values = linear.Select(k => ColumnMajor(source, k)).ToArray();
				var (rows, columns) = index.IsAll ? (values.Length, 1)
					: source.Rows == 1 ? (1, values.Length)
					: source.Columns == 1 ? (values.Length, 1)
					: (index.Values!.Rows, index.Values.Columns);
				return ComplexMatrix.Create(rows, columns, values);
			}
			case 2:
			{
				int[] rows = indices[0].IsAll ? Enumerable.Range(0, source.Rows).ToArray() : ToZeroBased(indices[0], source.Rows, "Row index");
				int[] columns = indices[1].IsAll ? Enumerable.Range(0, source.Columns).ToArray() : ToZeroBased(indices[1], source.Columns, "Column index");
				return ComplexMatrix.Create(rows.Length, columns.Length, (r, c) => source[rows[r], columns[c]]);
			}
			default:
				throw new MathException(MathError.Index, "At most two indices are supported.");
		}
	}

	public static ComplexMatrix Assign(ComplexMatrix? current, IndexArgument[] indices, ComplexMatrix value)
	{
		current ??= ComplexMatrix.Zeros(0, 0);
		return indices.Length switch
		{
			1 => AssignLinear(current, indices[0], value),
			2 => AssignRowsColumns(current, indices[0], indices[1], value),
			_ => throw new MathException(MathError.Index, "Assignment needs one or two indices."),
		};
	}

	static ComplexMatrix AssignRowsColumns(ComplexMatrix current, IndexArgument rowIndex, IndexArgument columnIndex, ComplexMatrix value)
	{
		int[] rows = rowIndex.IsAll
			? Enumerable.Range(0, current.IsEmpty ? value.Rows : current.Rows).ToArray()
			: ToZeroBased(rowIndex, int.MaxValue, "Row index");
		int[] columns = columnIndex.IsAll
			? Enumerable.Range(0, current.IsEmpty ? value.Columns : current.Columns).ToArray()
			: ToZeroBased(columnIndex, int.MaxValue, "Column index");

		var source = ValueSource(value, rows.Length, columns.Length);
		int newRows = System.Math.Max(current.Rows, rows.Length == 0 ? 0 : rows.Max() + 1);
		int newColumns = System.Math.Max(current.Columns, columns.Length == 0 ? 0 : columns.Max() + 1);
		var data = Grow(current, newRows, newColumns);
		for (int r = 0; r < rows.Length; r++)
			for (int c = 0; c < columns.Length; c++)
				data[rows[r] * newColumns + columns[c]] = source(r, c);
		return ComplexMatrix.Create(newRows, newColumns, data);
	}

	static ComplexMatrix AssignLinear(ComplexMatrix current, IndexArgument index, ComplexMatrix value)
	{
		int[] linear = index.IsAll ? Enumerable.Range(0, current.Count).ToArray() : ToZeroBased(index, int.MaxValue, "Index");
		int needed = linear.Length == 0 ? 0 : linear.Max() + 1;

		int rows = current.Rows, columns = current.Columns;
		if (needed > current.Count)
		{
			if (current.IsEmpty || current.Rows == 1)
				(rows, columns) = (1, needed);
			else if (current.Columns == 1)
				(rows, columns) = (needed, 1);
			else
				throw new MathException(MathError.Index, $"Index {needed} is outside the {current.Rows}×{current.Columns} matrix; use two indices to grow a matrix.");
		}

		var data = Grow(current, rows, columns);
		var source = ValueSource(value, linear.Length, 1);
		for (int i = 0; i < linear.Length; i++)
		{
			int k = linear[i];
			data[(k % rows) * columns + k / rows] = source(i, 0);
		}
		return ComplexMatrix.Create(rows, columns, data);
	}

	/// <summary>Reads the assigned value as a rows×columns block; a scalar is repeated.</summary>
	static Func<int, int, Complex> ValueSource(ComplexMatrix value, int rows, int columns)
	{
		if (value.IsScalar)
		{
			var scalar = value.ScalarValue;
			return (_, _) => scalar;
		}
		if (value.Rows == rows && value.Columns == columns)
			return (r, c) => value[r, c];
		if (value.Count == rows * columns && value.IsVector)
			return (r, c) => value[c * rows + r];
		throw new MathException(MathError.Dimension,
			$"Cannot assign a {value.Rows}×{value.Columns} matrix to {rows}×{columns} elements.");
	}

	/// <summary>A copy of the matrix enlarged to rows×columns; refuses sizes over <see cref="ComplexMatrix.MaxElements"/>.</summary>
	static Complex[] Grow(ComplexMatrix current, int rows, int columns)
	{
		var data = new Complex[ComplexMatrix.ElementCount(rows, columns)];
		for (int r = 0; r < current.Rows; r++)
			for (int c = 0; c < current.Columns; c++)
				data[r * columns + c] = current[r, c];
		return data;
	}

	static Complex ColumnMajor(ComplexMatrix m, int k) => m[k % m.Rows, k / m.Rows];

	static int[] ToZeroBased(IndexArgument index, int limit, string what)
	{
		var values = index.Values!.AsSpan();
		var result = new int[values.Length];
		for (int i = 0; i < values.Length; i++)
		{
			var v = values[i];
			if (v.Imaginary != 0 || v.Real != System.Math.Floor(v.Real) || v.Real < 1)
				throw new MathException(MathError.Index, $"{what} must be a positive integer, got {ComplexMatrix.Format(v)}.");
			if (v.Real > limit)
				throw new MathException(MathError.Index, FormattableString.Invariant($"{what} {v.Real:G10} exceeds the size {limit}."));
			result[i] = (int)v.Real - 1;
		}
		return result;
	}
}
