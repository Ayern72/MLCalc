namespace MLCalc.Language;

/// <summary>A range of characters in the source text.</summary>
public readonly record struct SourceSpan(int Start, int Length)
{
	/// <summary>The position just after the span.</summary>
	public int End => Start + Length;

	/// <summary>The span from <paramref name="start"/> up to, not including, <paramref name="end"/>.</summary>
	public static SourceSpan FromBounds(int start, int end) => new(start, end - start);

	/// <summary>The span as [start..end).</summary>
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

	/// <summary>The whole text.</summary>
	public string Text { get; }

	/// <summary>Wraps <paramref name="text"/> and finds where its lines start.</summary>
	public SourceText(string text)
	{
		Text = text;
		var starts = new List<int> { 0 };
		for (int i = 0; i < text.Length; i++)
			if (text[i] == '\n')
				starts.Add(i + 1);
		lineStarts = [.. starts];
	}

	/// <summary>The number of lines.</summary>
	public int LineCount => lineStarts.Length;

	/// <summary>The zero-based line and column of a position in the text.</summary>
	public LinePosition GetLinePosition(int position)
	{
		int line = Array.BinarySearch(lineStarts, position);
		if (line < 0)
			line = ~line - 1;
		return new LinePosition(line, position - lineStarts[line]);
	}

	/// <summary>The text that <paramref name="span"/> covers.</summary>
	public string ToString(SourceSpan span) => Text.Substring(span.Start, span.Length);

	/// <summary>The whole text.</summary>
	public override string ToString() => Text;
}

/// <summary>A syntax error with its location.</summary>
public sealed record Diagnostic(SourceSpan Span, string Message)
{
	/// <summary>The span and the message.</summary>
	public override string ToString() => $"{Span}: {Message}";
}
