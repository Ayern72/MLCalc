using System.Text;
using MLCalc.LinearAlgebra;

namespace MLCalc.Interpreter;

/// <summary>Formats values for the console, e.g. <c>x = 3</c> or a matrix with aligned columns.</summary>
public static class ResultFormatter
{
	/// <summary>"name = value" for scalars; "name =" followed by the indented matrix otherwise.</summary>
	public static string Format(string name, ComplexMatrix value)
	{
		if (value.IsScalar)
			return $"{name} = {ComplexMatrix.Format(value.ScalarValue)}";
		if (value.IsEmpty)
			return $"{name} = []";
		return $"{name} ={Environment.NewLine}{FormatValue(value)}";
	}

	/// <summary>A matrix as right-aligned columns, each row indented.</summary>
	public static string FormatValue(ComplexMatrix value)
	{
		if (value.IsEmpty)
			return "[]";
		if (value.IsScalar)
			return ComplexMatrix.Format(value.ScalarValue);

		var cells = new string[value.Rows, value.Columns];
		var widths = new int[value.Columns];
		for (int r = 0; r < value.Rows; r++)
			for (int c = 0; c < value.Columns; c++)
			{
				cells[r, c] = ComplexMatrix.Format(value[r, c]);
				widths[c] = System.Math.Max(widths[c], cells[r, c].Length);
			}

		var builder = new StringBuilder();
		for (int r = 0; r < value.Rows; r++)
		{
			if (r > 0)
				builder.AppendLine();
			builder.Append("  ");
			for (int c = 0; c < value.Columns; c++)
				builder.Append(' ', c == 0 ? 0 : 2).Append(cells[r, c].PadLeft(widths[c]));
		}
		return builder.ToString();
	}
}
