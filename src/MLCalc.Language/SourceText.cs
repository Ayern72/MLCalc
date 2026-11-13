namespace MLCalc.Language;

/// <summary>A range of characters in the source text.</summary>
public readonly record struct SourceSpan(int Start, int Length)
{
	public int End => Start + Length;

	public static SourceSpan FromBounds(int start, int end) => new(start, end - start);

	public override string ToString() => $"[{Start}..{End})";
}

/// <summary>A zero-based line and column.</summary>
public readonly record struct LinePosition(int Line, int Column)
{
	/// <summary>Formats as one-based "line:column" for display.</summary>
	public override string ToString() => $"{Line + 1}:{Column + 1}";
}

/// <summary>Source code with line lookup.</summary>
public sealed class SourceText
{
	readonly int[] lineStarts;

	public string Text { get; }

	public SourceText(string text)
	{
		Text = text;
		var starts = new List<int> { 0 };
		for (int i = 0; i < text.Length; i++)
			if (text[i] == '\n')
				starts.Add(i + 1);
		lineStarts = [.. starts];
	}

	public int LineCount => lineStarts.Length;

	public LinePosition GetLinePosition(int position)
	{
		int line = Array.BinarySearch(lineStarts, position);
		if (line < 0)
			line = ~line - 1;
		return new LinePosition(line, position - lineStarts[line]);
	}

	public string ToString(SourceSpan span) => Text.Substring(span.Start, span.Length);

	public override string ToString() => Text;
}

/// <summary>A syntax error with its location.</summary>
public sealed record Diagnostic(SourceSpan Span, string Message)
{
	public override string ToString() => $"{Span}: {Message}";
}
