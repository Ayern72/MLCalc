namespace MLCalc.Language.Tests;

public class LexerTests
{
	static TokenKind[] Kinds(string text) =>
		Lexer.Tokenize(text).Tokens.Select(t => t.Kind).Where(k => k != TokenKind.EndOfFile).ToArray();

	static Token Single(string text)
	{
		var (tokens, diagnostics) = Lexer.Tokenize(text);
		Assert.Empty(diagnostics);
		Assert.Equal(2, tokens.Count);
		return tokens[0];
	}

	[Theory]
	[InlineData("42", 42)]
	[InlineData("3.25", 3.25)]
	[InlineData(".5", 0.5)]
	[InlineData("1.", 1)]
	[InlineData("1e-3", 0.001)]
	[InlineData("2.5E+2", 250)]
	public void Numbers(string text, double value)
	{
		var token = Single(text);
		Assert.Equal(TokenKind.Number, token.Kind);
		Assert.Equal(value, token.Value);
	}

	[Theory]
	[InlineData("2i")]
	[InlineData("2j")]
	public void ImaginaryNumbers(string text)
	{
		var token = Single(text);
		Assert.Equal(TokenKind.ImaginaryNumber, token.Kind);
		Assert.Equal(2, token.Value);
	}

	[Fact]
	public void DotBeforeOperatorBelongsToOperator()
	{
		Assert.Equal([TokenKind.Number, TokenKind.DotStar, TokenKind.Identifier], Kinds("3.*x"));
		Assert.Equal([TokenKind.Number, TokenKind.DotCaret, TokenKind.Number], Kinds("2.^3"));
	}

	[Fact]
	public void KeywordsAreCaseInsensitive()
	{
		Assert.Equal([TokenKind.If, TokenKind.Then, TokenKind.Begin, TokenKind.End, TokenKind.Mod], Kinds("IF Then begin END mod"));
	}

	[Fact]
	public void IdentifiersKeepTheirSpelling()
	{
		var token = Single("Deltar");
		Assert.Equal(TokenKind.Identifier, token.Kind);
		Assert.Equal("Deltar", token.Text);
	}

	[Fact]
	public void StringsExcludeQuotes()
	{
		var token = Single("\"sin(x)\"");
		Assert.Equal(TokenKind.String, token.Kind);
		Assert.Equal("sin(x)", token.Text);
	}

	[Fact]
	public void UnterminatedStringIsReported()
	{
		Assert.Single(Lexer.Tokenize("\"abc").Diagnostics);
	}

	[Fact]
	public void CommentsAreSkippedButNewLinesRemain()
	{
		Assert.Equal([TokenKind.Identifier, TokenKind.NewLine, TokenKind.Identifier], Kinds("a % comment\nb"));
	}

	[Fact]
	public void NewLinesInsideParenthesesAreIgnored()
	{
		Assert.Equal(
			[TokenKind.Identifier, TokenKind.OpenParen, TokenKind.Number, TokenKind.Comma, TokenKind.Number, TokenKind.CloseParen],
			Kinds("f(1,\n2)"));
	}

	[Fact]
	public void NewLinesInsideBracketsSeparateRows()
	{
		Assert.Equal(
			[TokenKind.OpenBracket, TokenKind.Number, TokenKind.NewLine, TokenKind.Number, TokenKind.CloseBracket],
			Kinds("[1\n2]"));
	}

	[Fact]
	public void LineContinuation()
	{
		Assert.Equal([TokenKind.Identifier, TokenKind.Plus, TokenKind.Identifier], Kinds("a + ...\nb"));
	}

	[Fact]
	public void CarriageReturnLineFeedIsOneNewLine()
	{
		Assert.Equal([TokenKind.Identifier, TokenKind.NewLine, TokenKind.Identifier], Kinds("a\r\nb"));
	}

	[Theory]
	[InlineData("==", TokenKind.EqualsEquals)]
	[InlineData("~=", TokenKind.NotEquals)]
	[InlineData("<>", TokenKind.NotEquals)]
	[InlineData("<=", TokenKind.LessEquals)]
	[InlineData(">=", TokenKind.GreaterEquals)]
	[InlineData(".'", TokenKind.DotApostrophe)]
	[InlineData(".\\", TokenKind.DotBackslash)]
	[InlineData("&&", TokenKind.AmpersandAmpersand)]
	[InlineData("!", TokenKind.Bang)]
	public void Operators(string text, TokenKind kind)
	{
		Assert.Equal(kind, Single(text).Kind);
	}

	[Fact]
	public void TracksLeadingWhitespace()
	{
		var tokens = Lexer.Tokenize("[1 -2]").Tokens;
		Assert.True(tokens[2].HasLeadingSpace);
		Assert.False(tokens[3].HasLeadingSpace);
	}

	[Fact]
	public void UnknownCharacterIsReported()
	{
		var (tokens, diagnostics) = Lexer.Tokenize("a $ b");
		Assert.Equal(TokenKind.Bad, tokens[1].Kind);
		Assert.Equal(new SourceSpan(2, 1), Assert.Single(diagnostics).Span);
	}
}
