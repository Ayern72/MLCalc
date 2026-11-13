using System.Globalization;

namespace MLCalc.Language;

/// <summary>
/// Converts MLCalc source text into tokens. Keywords are case-insensitive, <c>%</c> starts a comment,
/// strings use double quotes, and <c>...</c> continues a line. Newlines inside parentheses are
/// ignored; inside brackets they separate matrix rows.
/// </summary>
public sealed class Lexer
{
	static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.OrdinalIgnoreCase)
	{
		["if"] = TokenKind.If,
		["then"] = TokenKind.Then,
		["else"] = TokenKind.Else,
		["elseif"] = TokenKind.Elseif,
		["while"] = TokenKind.While,
		["do"] = TokenKind.Do,
		["for"] = TokenKind.For,
		["to"] = TokenKind.To,
		["begin"] = TokenKind.Begin,
		["end"] = TokenKind.End,
		["and"] = TokenKind.And,
		["or"] = TokenKind.Or,
		["xor"] = TokenKind.Xor,
		["not"] = TokenKind.Not,
		["mod"] = TokenKind.Mod,
		["div"] = TokenKind.Div,
	};

	readonly string text;
	readonly List<Diagnostic> diagnostics = [];
	readonly Stack<char> nesting = new();
	int position;

	Lexer(string text) => this.text = text;

	public static (IReadOnlyList<Token> Tokens, IReadOnlyList<Diagnostic> Diagnostics) Tokenize(string text)
	{
		var lexer = new Lexer(text);
		var tokens = new List<Token>();
		Token token;
		do
		{
			token = lexer.Next();
			tokens.Add(token);
		}
		while (token.Kind != TokenKind.EndOfFile);
		return (tokens, lexer.diagnostics);
	}

	char Current => Peek(0);

	char Peek(int offset) => position + offset < text.Length ? text[position + offset] : '\0';

	bool InsideParentheses => nesting.Count > 0 && nesting.Peek() == '(';

	Token Next()
	{
		bool space = SkipTrivia();
		int start = position;

		if (position >= text.Length)
			return new Token(TokenKind.EndOfFile, new SourceSpan(start, 0), "", HasLeadingSpace: space);

		char c = Current;
		if (c == '\n' || c == '\r')
		{
			position += c == '\r' && Peek(1) == '\n' ? 2 : 1;
			return Make(TokenKind.NewLine, start, space);
		}
		if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(Peek(1))))
			return LexNumber(start, space);
		if (char.IsLetter(c) || c == '_')
			return LexIdentifier(start, space);
		if (c == '"')
			return LexString(start, space);

		var kind = LexOperator();
		if (kind == TokenKind.Bad)
		{
			position++;
			diagnostics.Add(new Diagnostic(new SourceSpan(start, 1), $"Unexpected character '{c}'."));
		}
		return Make(kind, start, space);
	}

	Token Make(TokenKind kind, int start, bool space, double value = 0) =>
		new(kind, SourceSpan.FromBounds(start, position), text[start..position], value, space);

	/// <summary>Skips whitespace, comments, continuations and (inside parentheses) newlines.</summary>
	bool SkipTrivia()
	{
		int start = position;
		while (position < text.Length)
		{
			char c = Current;
			if (c is ' ' or '\t' or '\f' or '\v')
				position++;
			else if (c == '%')
			{
				while (position < text.Length && Current is not ('\n' or '\r'))
					position++;
			}
			else if (c == '.' && Peek(1) == '.' && Peek(2) == '.')
			{
				// Line continuation: skip the rest of the line, including the newline.
				while (position < text.Length && Current != '\n')
					position++;
				if (position < text.Length)
					position++;
			}
			else if ((c is '\n' or '\r') && InsideParentheses)
				position++;
			else
				break;
		}
		return position > start;
	}

	Token LexNumber(int start, bool space)
	{
		while (char.IsAsciiDigit(Current))
			position++;
		// A dot followed by an operator character belongs to the operator: 3.*x is 3 .* x.
		if (Current == '.' && Peek(1) is not ('*' or '/' or '\\' or '^' or '\''))
		{
			position++;
			while (char.IsAsciiDigit(Current))
				position++;
		}
		if (Current is 'e' or 'E')
		{
			int exponent = Peek(1) is '+' or '-' ? 2 : 1;
			if (char.IsAsciiDigit(Peek(exponent)))
			{
				position += exponent;
				while (char.IsAsciiDigit(Current))
					position++;
			}
		}

		double value = double.Parse(text.AsSpan(start, position - start), NumberStyles.Float, CultureInfo.InvariantCulture);
		if (Current is 'i' or 'j' && !IsIdentifierPart(Peek(1)))
		{
			position++;
			return Make(TokenKind.ImaginaryNumber, start, space, value);
		}
		if (IsIdentifierPart(Current))
		{
			int badStart = position;
			while (IsIdentifierPart(Current))
				position++;
			diagnostics.Add(new Diagnostic(SourceSpan.FromBounds(badStart, position), "Invalid number."));
			return Make(TokenKind.Bad, start, space);
		}
		return Make(TokenKind.Number, start, space, value);
	}

	Token LexIdentifier(int start, bool space)
	{
		while (IsIdentifierPart(Current))
			position++;
		var kind = Keywords.TryGetValue(text[start..position], out var keyword) ? keyword : TokenKind.Identifier;
		return Make(kind, start, space);
	}

	static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

	Token LexString(int start, bool space)
	{
		position++;
		int contentStart = position;
		while (position < text.Length && Current is not ('"' or '\n' or '\r'))
			position++;
		string content = text[contentStart..position];
		if (Current == '"')
			position++;
		else
			diagnostics.Add(new Diagnostic(SourceSpan.FromBounds(start, position), "Unterminated string."));
		return new Token(TokenKind.String, SourceSpan.FromBounds(start, position), content, 0, space);
	}

	TokenKind LexOperator()
	{
		char c = Current, next = Peek(1);
		switch (c)
		{
			case '(':
			case '[':
				nesting.Push(c);
				position++;
				return c == '(' ? TokenKind.OpenParen : TokenKind.OpenBracket;
			case ')':
			case ']':
				if (nesting.Count > 0)
					nesting.Pop();
				position++;
				return c == ')' ? TokenKind.CloseParen : TokenKind.CloseBracket;
		}

		(TokenKind Kind, int Length) result = (c, next) switch
		{
			('.', '*') => (TokenKind.DotStar, 2),
			('.', '/') => (TokenKind.DotSlash, 2),
			('.', '\\') => (TokenKind.DotBackslash, 2),
			('.', '^') => (TokenKind.DotCaret, 2),
			('.', '\'') => (TokenKind.DotApostrophe, 2),
			('=', '=') => (TokenKind.EqualsEquals, 2),
			('~', '=') => (TokenKind.NotEquals, 2),
			('<', '>') => (TokenKind.NotEquals, 2),
			('<', '=') => (TokenKind.LessEquals, 2),
			('>', '=') => (TokenKind.GreaterEquals, 2),
			('&', '&') => (TokenKind.AmpersandAmpersand, 2),
			('|', '|') => (TokenKind.PipePipe, 2),
			('+', _) => (TokenKind.Plus, 1),
			('-', _) => (TokenKind.Minus, 1),
			('*', _) => (TokenKind.Star, 1),
			('/', _) => (TokenKind.Slash, 1),
			('\\', _) => (TokenKind.Backslash, 1),
			('^', _) => (TokenKind.Caret, 1),
			('\'', _) => (TokenKind.Apostrophe, 1),
			('!', _) => (TokenKind.Bang, 1),
			('=', _) => (TokenKind.Equals, 1),
			('<', _) => (TokenKind.Less, 1),
			('>', _) => (TokenKind.Greater, 1),
			('&', _) => (TokenKind.Ampersand, 1),
			('|', _) => (TokenKind.Pipe, 1),
			('~', _) => (TokenKind.Tilde, 1),
			(',', _) => (TokenKind.Comma, 1),
			(';', _) => (TokenKind.Semicolon, 1),
			(':', _) => (TokenKind.Colon, 1),
			_ => (TokenKind.Bad, 0),
		};
		position += result.Length;
		return result.Kind;
	}
}
