namespace MLCalc.Language;

public enum TokenKind
{
	EndOfFile,
	NewLine,
	Bad,

	Number,
	ImaginaryNumber,
	String,
	Identifier,

	// Keywords (case-insensitive)
	If,
	Then,
	Else,
	While,
	Do,
	For,
	To,
	Begin,
	End,
	And,
	Or,
	Xor,
	Not,
	Mod,
	Div,

	// Operators and punctuation
	Plus,
	Minus,
	Star,
	Slash,
	Backslash,
	Caret,
	DotStar,
	DotSlash,
	DotBackslash,
	DotCaret,
	Apostrophe,
	DotApostrophe,
	Bang,
	Equals,
	EqualsEquals,
	NotEquals,
	Less,
	LessEquals,
	Greater,
	GreaterEquals,
	Ampersand,
	AmpersandAmpersand,
	Pipe,
	PipePipe,
	Tilde,
	OpenParen,
	CloseParen,
	OpenBracket,
	CloseBracket,
	Comma,
	Semicolon,
	Colon,
}

/// <summary>A lexical token.</summary>
/// <param name="Kind">The token kind.</param>
/// <param name="Span">Location in the source.</param>
/// <param name="Text">The source text of the token (the contents, without quotes, for strings).</param>
/// <param name="Value">The numeric value of a number token.</param>
/// <param name="HasLeadingSpace">Whether whitespace precedes the token; used to separate matrix elements.</param>
public sealed record Token(TokenKind Kind, SourceSpan Span, string Text, double Value = 0, bool HasLeadingSpace = false)
{
	public override string ToString() => $"{Kind} '{Text}'";
}
