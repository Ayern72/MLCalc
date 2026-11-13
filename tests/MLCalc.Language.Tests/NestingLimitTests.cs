namespace MLCalc.Language.Tests;

/// <summary>
/// Deeply nested input used to overflow the stack, which ends the process. The parser now refuses
/// more than <see cref="Parser.MaxDepth"/> levels with an ordinary syntax error.
/// </summary>
public class NestingLimitTests
{
	static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

	/// <summary>Source code with <paramref name="n"/> levels of the given kind of nesting.</summary>
	static string Build(string kind, int n) => kind switch
	{
		"parentheses" => "x = " + Repeat("(", n) + "1" + Repeat(")", n),
		"brackets" => "x = " + Repeat("[", n) + "1" + Repeat("]", n),
		"calls" => "x = " + Repeat("sin(", n) + "1" + Repeat(")", n),
		"unary minus" => "x = " + Repeat("-", n) + "1",
		"exponent signs" => "x = 2^" + Repeat("-", n) + "1",
		"sum chain" => "x = 1" + Repeat("+1", n),
		"power chain" => "x = 1" + Repeat("^1", n),
		"transposes" => "x = 1" + Repeat("'", n),
		"begin blocks" => Repeat("begin ", n) + "x = 1" + Repeat(" end", n),
		"if blocks" => Repeat("if 1\n", n) + "x = 1\n" + Repeat("end\n", n),
		"elseif chain" => "if 0\nx = 0\n" + Repeat("elseif 0\nx = 0\n", n) + "end",
		"else if chain" => "if 0 then x = 0\n" + Repeat("else if 0 then x = 0\n", n),
		_ => throw new ArgumentException(kind),
	};

	public static TheoryData<string> Kinds() =>
	[
		"parentheses", "brackets", "calls", "unary minus", "exponent signs", "sum chain",
		"power chain", "transposes", "begin blocks", "if blocks", "elseif chain", "else if chain",
	];

	[Theory]
	[MemberData(nameof(Kinds))]
	public void NestingBelowTheLimitParses(string kind)
	{
		var result = Parser.Parse(Build(kind, 240));
		Assert.True(result.Diagnostics.IsEmpty, kind + ": " + string.Join("; ", result.Diagnostics));
	}

	[Theory]
	[MemberData(nameof(Kinds))]
	public void NestingAboveTheLimitIsAnError(string kind)
	{
		var result = Parser.Parse(Build(kind, 10_000));
		Assert.Contains(result.Diagnostics, d => d.Message.Contains("nested too deeply"));
	}

	[Fact]
	public void ParsingContinuesAfterTooDeepNesting()
	{
		var result = Parser.Parse(Build("parentheses", 300) + "\ny = 2");
		Assert.Single(result.Diagnostics);
		Assert.False(result.IsIncomplete);
		Assert.Equal("(= y 2)", result.Root.ToString());
	}

	[Fact]
	public void LongMatrixLiteralsAreNotNesting()
	{
		// Elements and rows are siblings, not nested, so large data tables stay allowed.
		string row = string.Join(", ", Enumerable.Range(1, 500));
		var result = Parser.Parse("t = [" + string.Join("\n", Enumerable.Repeat(row, 500)) + "]");
		Assert.True(result.Diagnostics.IsEmpty);
	}
}
