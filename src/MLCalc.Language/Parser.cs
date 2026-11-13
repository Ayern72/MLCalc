using System.Collections.Immutable;

namespace MLCalc.Language;

/// <summary>The result of parsing: the syntax tree plus any syntax errors.</summary>
/// <param name="Text">The parsed source.</param>
/// <param name="Root">The syntax tree. Statements with errors are left out.</param>
/// <param name="Diagnostics">Syntax errors in source order.</param>
/// <param name="IsIncomplete">
/// True when the input ended in the middle of a construct (an open <c>begin</c>, bracket or
/// parenthesis, or an <c>if</c>/<c>while</c> without a body), so a console can ask for more lines.
/// </param>
public sealed record ParseResult(SourceText Text, ProgramSyntax Root, ImmutableArray<Diagnostic> Diagnostics, bool IsIncomplete)
{
	public bool HasErrors => !Diagnostics.IsEmpty;
}

/// <summary>
/// Recursive-descent parser for the MLCalc language. Operator precedence follows MATLAB, from
/// lowest to highest: <c>or xor | ||</c>; <c>and &amp; &amp;&amp;</c>; comparisons; ranges <c>a:b</c>;
/// <c>+ -</c>; <c>* / \ .* ./ .\ mod div</c>; unary <c>+ - not ~</c>; <c>^ .^</c>; postfix
/// <c>' .' !</c>. Control flow comes in two styles, chosen by what follows the header:
/// the original Pascal-like <c>if … then … else …</c>, <c>while … do …</c>, <c>for v = a to b do …</c>
/// with a single statement or <c>begin … end</c> as body; and MATLAB-style blocks without
/// <c>then</c>/<c>do</c> that run to <c>end</c>: <c>if … elseif … else … end</c>, <c>while … end</c>,
/// <c>for v = range … end</c>.
/// </summary>
public sealed class Parser
{
	readonly IReadOnlyList<Token> tokens;
	readonly List<Diagnostic> diagnostics = [];
	/// <summary>Innermost bracket context: true inside <c>[ ]</c>, false inside <c>( )</c>.</summary>
	readonly Stack<bool> matrixContext = new();
	int position;
	bool isIncomplete;

	Parser(IReadOnlyList<Token> tokens) => this.tokens = tokens;

	public static ParseResult Parse(string text)
	{
		var (tokens, lexerDiagnostics) = Lexer.Tokenize(text);
		var parser = new Parser(tokens);
		var root = parser.ParseProgram(text.Length);
		var diagnostics = lexerDiagnostics.Concat(parser.diagnostics).OrderBy(d => d.Span.Start).ToImmutableArray();
		return new ParseResult(new SourceText(text), root, diagnostics, parser.isIncomplete);
	}

	sealed class ParseError(Diagnostic diagnostic) : Exception(diagnostic.Message)
	{
		public Diagnostic Diagnostic { get; } = diagnostic;
	}

	// ───────────── Token helpers ─────────────

	Token Current => tokens[position];

	Token Peek(int offset) => tokens[Math.Min(position + offset, tokens.Count - 1)];

	bool At(TokenKind kind) => Current.Kind == kind;

	bool AtAny(params ReadOnlySpan<TokenKind> kinds) => kinds.Contains(Current.Kind);

	Token Advance()
	{
		var token = Current;
		if (token.Kind != TokenKind.EndOfFile)
			position++;
		return token;
	}

	Token Expect(TokenKind kind, string description)
	{
		if (At(kind))
			return Advance();
		throw Error($"Expected {description}" + (At(TokenKind.EndOfFile) ? "." : $" but found {Describe(Current)}."));
	}

	ParseError Error(string message)
	{
		if (At(TokenKind.EndOfFile))
			isIncomplete = true;
		return new ParseError(new Diagnostic(Current.Span, message));
	}

	static string Describe(Token token) => token.Kind switch
	{
		TokenKind.EndOfFile => "end of input",
		TokenKind.NewLine => "end of line",
		TokenKind.String => "a string",
		_ => $"'{token.Text}'",
	};

	SourceSpan SpanFrom(int start) => SourceSpan.FromBounds(start, tokens[Math.Max(position - 1, 0)].Span.End);

	bool InMatrix => matrixContext.Count > 0 && matrixContext.Peek();

	T InContext<T>(bool matrix, Func<T> parse)
	{
		matrixContext.Push(matrix);
		try
		{
			return parse();
		}
		finally
		{
			matrixContext.Pop();
		}
	}

	// ───────────── Statements ─────────────

	ProgramSyntax ParseProgram(int length)
	{
		var statements = ImmutableArray.CreateBuilder<Statement>();
		while (true)
		{
			SkipSeparators();
			if (At(TokenKind.EndOfFile))
				break;
			TryParseStatement(statements);
		}
		return new ProgramSyntax(new SourceSpan(0, length), statements.ToImmutable());
	}

	void TryParseStatement(ImmutableArray<Statement>.Builder statements)
	{
		try
		{
			statements.Add(ParseStatement());
		}
		catch (ParseError error)
		{
			diagnostics.Add(error.Diagnostic);
			Synchronize();
		}
	}

	/// <summary>Skips to the end of the current statement after an error.</summary>
	void Synchronize()
	{
		matrixContext.Clear();
		int start = position;
		while (!AtAny(TokenKind.NewLine, TokenKind.Semicolon, TokenKind.EndOfFile, TokenKind.End))
			Advance();
		if (position == start && !At(TokenKind.EndOfFile))
			Advance();
	}

	void SkipSeparators()
	{
		while (AtAny(TokenKind.NewLine, TokenKind.Semicolon, TokenKind.Comma))
			Advance();
	}

	void SkipNewLines()
	{
		while (AtAny(TokenKind.NewLine, TokenKind.Semicolon))
			Advance();
	}

	Statement ParseStatement() => Current.Kind switch
	{
		TokenKind.If => ParseIf(),
		TokenKind.While => ParseWhile(),
		TokenKind.For => ParseFor(),
		TokenKind.Begin => ParseBlock(),
		TokenKind.Then or TokenKind.Do or TokenKind.Else or TokenKind.Elseif or TokenKind.End or TokenKind.To =>
			throw Error($"Unexpected {Describe(Current)}."),
		_ => ParseSimpleStatement(),
	};

	/// <summary>
	/// <c>if c then s [else s]</c> (original style), or the MATLAB style
	/// <c>if c … elseif c … else … end</c> when the condition is not followed by <c>then</c>.
	/// </summary>
	Statement ParseIf()
	{
		int start = Expect(TokenKind.If, "'if'").Span.Start;
		var condition = ParseExpression();
		if (!At(TokenKind.Then))
			return ParseMatlabIf(start, condition);
		Advance();
		var then = ParseBody();

		int beforeElse = position;
		SkipNewLines();
		if (!At(TokenKind.Else))
		{
			position = beforeElse;
			return new IfStatement(SpanFrom(start), condition, then, null);
		}
		Advance();
		var @else = At(TokenKind.If) ? ParseIf() : ParseBody();
		return new IfStatement(SpanFrom(start), condition, then, @else);
	}

	/// <summary>The rest of a MATLAB-style if after its condition; an elseif chain shares the final <c>end</c>.</summary>
	Statement ParseMatlabIf(int start, Expression condition)
	{
		EndBlockHeader("'then'");
		var then = ParseStatementsUntil(TokenKind.Else, TokenKind.Elseif, TokenKind.End);
		Statement? @else = null;
		if (At(TokenKind.Elseif))
		{
			int elseifStart = Advance().Span.Start;
			var elseifCondition = ParseExpression();
			@else = ParseMatlabIf(elseifStart, elseifCondition);
			return new IfStatement(SpanFrom(start), condition, then, @else);
		}
		if (At(TokenKind.Else))
		{
			Advance();
			@else = ParseStatementsUntil(TokenKind.End);
		}
		Expect(TokenKind.End, "'end' to close 'if'");
		return new IfStatement(SpanFrom(start), condition, then, @else);
	}

	/// <summary><c>while c do s</c> (original style), or MATLAB style <c>while c … end</c>.</summary>
	Statement ParseWhile()
	{
		int start = Expect(TokenKind.While, "'while'").Span.Start;
		var condition = ParseExpression();
		Statement body;
		if (At(TokenKind.Do))
		{
			Advance();
			body = ParseBody();
		}
		else
			body = ParseMatlabBody("'do'", "while");
		return new WhileStatement(SpanFrom(start), condition, body);
	}

	/// <summary>
	/// After the header of a MATLAB-style block, the condition must end with a new line, ',' or ';'.
	/// </summary>
	void EndBlockHeader(string originalKeyword)
	{
		if (AtAny(TokenKind.NewLine, TokenKind.Comma, TokenKind.Semicolon))
		{
			Advance();
			return;
		}
		throw Error(At(TokenKind.EndOfFile)
			? "Expected the body of the block."
			: $"Expected {originalKeyword} or the end of the line, but found {Describe(Current)}.");
	}

	/// <summary>A MATLAB-style body: statements up to and including <c>end</c>.</summary>
	Statement ParseMatlabBody(string originalKeyword, string construct)
	{
		EndBlockHeader(originalKeyword);
		var body = ParseStatementsUntil(TokenKind.End);
		Expect(TokenKind.End, $"'end' to close '{construct}'");
		return body;
	}

	/// <summary>Statements up to (not including) one of <paramref name="terminators"/>.</summary>
	BlockStatement ParseStatementsUntil(params TokenKind[] terminators)
	{
		int start = Current.Span.Start;
		var statements = ImmutableArray.CreateBuilder<Statement>();
		while (true)
		{
			SkipSeparators();
			if (AtAny(terminators))
				break;
			if (At(TokenKind.EndOfFile))
				throw Error("Expected 'end' to close the block.");
			TryParseStatement(statements);
		}
		int end = statements.Count > 0 ? statements[^1].Span.End : start;
		return new BlockStatement(SourceSpan.FromBounds(start, System.Math.Max(start, end)), statements.ToImmutable());
	}

	Statement ParseFor()
	{
		int start = Expect(TokenKind.For, "'for'").Span.Start;
		var nameToken = Expect(TokenKind.Identifier, "a loop variable");
		var variable = new Identifier(nameToken.Span, nameToken.Text);
		Expect(TokenKind.Equals, "'='");
		var first = ParseExpression();
		Expression? last = null;
		if (At(TokenKind.To))
		{
			Advance();
			last = ParseExpression();
		}
		Statement body;
		if (At(TokenKind.Do))
		{
			Advance();
			body = ParseBody();
		}
		else
			body = ParseMatlabBody("'do'", "for");
		return new ForStatement(SpanFrom(start), variable, first, last, body);
	}

	/// <summary>The body of if/while/for: a single statement or a begin…end block, possibly on the next line.</summary>
	Statement ParseBody()
	{
		SkipNewLines();
		if (At(TokenKind.EndOfFile))
			throw Error("Expected a statement.");
		return At(TokenKind.Begin) ? ParseBlock() : ParseStatement();
	}

	Statement ParseBlock()
	{
		int start = Expect(TokenKind.Begin, "'begin'").Span.Start;
		var statements = ImmutableArray.CreateBuilder<Statement>();
		while (true)
		{
			SkipSeparators();
			if (At(TokenKind.End))
			{
				Advance();
				break;
			}
			if (At(TokenKind.EndOfFile))
			{
				isIncomplete = true;
				diagnostics.Add(new Diagnostic(Current.Span, "Expected 'end' to close 'begin'."));
				break;
			}
			TryParseStatement(statements);
		}
		return new BlockStatement(SpanFrom(start), statements.ToImmutable());
	}

	Statement ParseSimpleStatement()
	{
		int start = Current.Span.Start;
		if (At(TokenKind.Identifier) && (Peek(1).Kind == TokenKind.Equals || (Peek(1).Kind == TokenKind.OpenParen && IsIndexedAssignment())))
		{
			var nameToken = Advance();
			var target = new Identifier(nameToken.Span, nameToken.Text);
			var indices = At(TokenKind.OpenParen) ? ParseArguments() : [];
			Expect(TokenKind.Equals, "'='");
			var value = ParseExpression();
			var span = SpanFrom(start);
			return new AssignmentStatement(span, target, indices, value, EndStatement());
		}

		var expression = ParseExpression();
		var expressionSpan = SpanFrom(start);
		return new ExpressionStatement(expressionSpan, expression, EndStatement());
	}

	/// <summary>Looks past <c>name( … )</c> for an <c>=</c>, distinguishing <c>x(1,k) = k</c> from <c>f(x) == 1</c>.</summary>
	bool IsIndexedAssignment()
	{
		int depth = 0;
		for (int i = position + 1; i < tokens.Count; i++)
		{
			switch (tokens[i].Kind)
			{
				case TokenKind.OpenParen:
				case TokenKind.OpenBracket:
					depth++;
					break;
				case TokenKind.CloseParen:
				case TokenKind.CloseBracket:
					if (--depth == 0)
						return i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.Equals;
					break;
				case TokenKind.NewLine:
				case TokenKind.EndOfFile:
					return false;
			}
		}
		return false;
	}

	/// <summary>Consumes a statement terminator. Returns true when the statement ends with <c>;</c> (no output).</summary>
	bool EndStatement()
	{
		switch (Current.Kind)
		{
			case TokenKind.Semicolon:
				Advance();
				return true;
			case TokenKind.Comma:
			case TokenKind.NewLine:
				Advance();
				return false;
			case TokenKind.EndOfFile:
			case TokenKind.Else:
			case TokenKind.Elseif:
			case TokenKind.End:
				return false;
			default:
				throw Error($"Unexpected {Describe(Current)}; expected the end of the statement.");
		}
	}

	// ───────────── Expressions ─────────────

	public Expression ParseExpression() => ParseOr();

	Expression ParseBinaryLevel(Func<Expression> operand, Func<TokenKind, BinaryOperator?> map)
	{
		var left = operand();
		while (map(Current.Kind) is { } op)
		{
			if (InMatrix && IsMatrixElementBreak())
				break;
			Advance();
			var right = operand();
			left = new BinaryExpression(SourceSpan.FromBounds(left.Span.Start, right.Span.End), op, left, right);
		}
		return left;
	}

	Expression ParseOr() => ParseBinaryLevel(ParseAnd, kind => kind switch
	{
		TokenKind.Or or TokenKind.Pipe or TokenKind.PipePipe => BinaryOperator.Or,
		TokenKind.Xor => BinaryOperator.Xor,
		_ => null,
	});

	Expression ParseAnd() => ParseBinaryLevel(ParseComparison, kind => kind switch
	{
		TokenKind.And or TokenKind.Ampersand or TokenKind.AmpersandAmpersand => BinaryOperator.And,
		_ => null,
	});

	Expression ParseComparison() => ParseBinaryLevel(ParseRange, kind => kind switch
	{
		TokenKind.EqualsEquals => BinaryOperator.Equal,
		TokenKind.NotEquals => BinaryOperator.NotEqual,
		TokenKind.Less => BinaryOperator.Less,
		TokenKind.LessEquals => BinaryOperator.LessOrEqual,
		TokenKind.Greater => BinaryOperator.Greater,
		TokenKind.GreaterEquals => BinaryOperator.GreaterOrEqual,
		_ => null,
	});

	Expression ParseRange()
	{
		var first = ParseAdditive();
		if (!At(TokenKind.Colon))
			return first;
		Advance();
		var second = ParseAdditive();
		if (!At(TokenKind.Colon))
			return new RangeExpression(SourceSpan.FromBounds(first.Span.Start, second.Span.End), first, null, second);
		Advance();
		var third = ParseAdditive();
		return new RangeExpression(SourceSpan.FromBounds(first.Span.Start, third.Span.End), first, second, third);
	}

	Expression ParseAdditive() => ParseBinaryLevel(ParseMultiplicative, kind => kind switch
	{
		TokenKind.Plus => BinaryOperator.Add,
		TokenKind.Minus => BinaryOperator.Subtract,
		_ => null,
	});

	/// <summary>
	/// Inside a matrix, <c>[1 -2]</c> has two elements but <c>[1 - 2]</c> and <c>[1-2]</c> have one:
	/// a sign preceded by a space but not followed by one starts a new element.
	/// </summary>
	bool IsMatrixElementBreak() =>
		AtAny(TokenKind.Plus, TokenKind.Minus) && Current.HasLeadingSpace && !Peek(1).HasLeadingSpace;

	Expression ParseMultiplicative() => ParseBinaryLevel(ParseUnary, kind => kind switch
	{
		TokenKind.Star => BinaryOperator.Multiply,
		TokenKind.Slash => BinaryOperator.Divide,
		TokenKind.Backslash => BinaryOperator.LeftDivide,
		TokenKind.DotStar => BinaryOperator.ElementMultiply,
		TokenKind.DotSlash => BinaryOperator.ElementDivide,
		TokenKind.DotBackslash => BinaryOperator.ElementLeftDivide,
		TokenKind.Mod => BinaryOperator.Modulo,
		TokenKind.Div => BinaryOperator.IntegerDivide,
		_ => null,
	});

	static UnaryOperator? UnaryOperatorFor(TokenKind kind) => kind switch
	{
		TokenKind.Plus => UnaryOperator.Plus,
		TokenKind.Minus => UnaryOperator.Negate,
		TokenKind.Not or TokenKind.Tilde => UnaryOperator.Not,
		_ => null,
	};

	/// <summary>Unary operators bind looser than <c>^</c>, so <c>-2^2</c> is <c>-(2^2)</c>, as in MATLAB.</summary>
	Expression ParseUnary()
	{
		if (UnaryOperatorFor(Current.Kind) is { } op)
		{
			int start = Advance().Span.Start;
			var operand = ParseUnary();
			return new UnaryExpression(SourceSpan.FromBounds(start, operand.Span.End), op, operand);
		}
		return ParsePower();
	}

	Expression ParsePower()
	{
		var left = ParsePostfix();
		while (AtAny(TokenKind.Caret, TokenKind.DotCaret))
		{
			var op = Advance().Kind == TokenKind.Caret ? BinaryOperator.Power : BinaryOperator.ElementPower;
			var right = ParsePowerOperand();
			left = new BinaryExpression(SourceSpan.FromBounds(left.Span.Start, right.Span.End), op, left, right);
		}
		return left;
	}

	/// <summary>The exponent may carry its own sign: <c>2^-1</c>.</summary>
	Expression ParsePowerOperand()
	{
		if (UnaryOperatorFor(Current.Kind) is { } op)
		{
			int start = Advance().Span.Start;
			var operand = ParsePowerOperand();
			return new UnaryExpression(SourceSpan.FromBounds(start, operand.Span.End), op, operand);
		}
		return ParsePostfix();
	}

	Expression ParsePostfix()
	{
		var expression = ParsePrimary();
		while (true)
		{
			PostfixOperator? op = Current.Kind switch
			{
				TokenKind.Apostrophe => PostfixOperator.ConjugateTranspose,
				TokenKind.DotApostrophe => PostfixOperator.Transpose,
				TokenKind.Bang => PostfixOperator.Factorial,
				_ => null,
			};
			if (op is null)
				return expression;
			int end = Advance().Span.End;
			expression = new PostfixExpression(SourceSpan.FromBounds(expression.Span.Start, end), op.Value, expression);
		}
	}

	Expression ParsePrimary()
	{
		var token = Current;
		switch (token.Kind)
		{
			case TokenKind.Number:
				Advance();
				return new NumberLiteral(token.Span, token.Value);
			case TokenKind.ImaginaryNumber:
				Advance();
				return new NumberLiteral(token.Span, token.Value, IsImaginary: true);
			case TokenKind.String:
				Advance();
				return new StringLiteral(token.Span, token.Text);
			case TokenKind.Identifier:
			{
				Advance();
				var identifier = new Identifier(token.Span, token.Text);
				// In a matrix, "[f (1)]" is two elements; "[f(1)]" is a call.
				if (!At(TokenKind.OpenParen) || (InMatrix && Current.HasLeadingSpace))
					return identifier;
				var arguments = ParseArguments();
				return new ApplyExpression(SpanFrom(token.Span.Start), identifier, arguments);
			}
			case TokenKind.Mod or TokenKind.Div when Peek(1).Kind == TokenKind.OpenParen:
			{
				// MATLAB's function form mod(a, b) alongside the original operator a mod b.
				Advance();
				var function = new Identifier(token.Span, token.Text);
				var arguments = ParseArguments();
				return new ApplyExpression(SpanFrom(token.Span.Start), function, arguments);
			}
			case TokenKind.OpenParen:
			{
				Advance();
				var inner = InContext(false, ParseExpression);
				Expect(TokenKind.CloseParen, "')'");
				return inner;
			}
			case TokenKind.OpenBracket:
				return ParseMatrix();
			case TokenKind.EndOfFile:
				throw Error("Expected an expression.");
			default:
				throw Error($"Expected an expression but found {Describe(token)}.");
		}
	}

	ImmutableArray<Expression> ParseArguments()
	{
		Expect(TokenKind.OpenParen, "'('");
		var arguments = InContext(false, () =>
		{
			var list = ImmutableArray.CreateBuilder<Expression>();
			if (At(TokenKind.CloseParen))
				return list.ToImmutable();
			while (true)
			{
				list.Add(ParseArgument());
				if (!At(TokenKind.Comma))
					return list.ToImmutable();
				Advance();
			}
		});
		Expect(TokenKind.CloseParen, "',' or ')'");
		return arguments;
	}

	/// <summary>An argument: an expression, a bare <c>:</c>, or a bare style symbol such as <c>-</c> or <c>+</c>.</summary>
	Expression ParseArgument()
	{
		bool endsArgument = Peek(1).Kind is TokenKind.Comma or TokenKind.CloseParen;
		if (endsArgument && At(TokenKind.Colon))
			return new ColonAll(Advance().Span);
		if (endsArgument && AtAny(TokenKind.Plus, TokenKind.Minus, TokenKind.Star, TokenKind.Bang))
		{
			var token = Advance();
			return new StyleLiteral(token.Span, token.Text);
		}
		return ParseExpression();
	}

	Expression ParseMatrix()
	{
		int start = Expect(TokenKind.OpenBracket, "'['").Span.Start;
		var rows = InContext(true, () =>
		{
			var rows = ImmutableArray.CreateBuilder<ImmutableArray<Expression>>();
			var row = ImmutableArray.CreateBuilder<Expression>();
			while (!At(TokenKind.CloseBracket))
			{
				switch (Current.Kind)
				{
					case TokenKind.EndOfFile:
						throw Error("Expected ']' to close the matrix.");
					case TokenKind.Semicolon:
					case TokenKind.NewLine:
						Advance();
						if (row.Count > 0)
						{
							rows.Add(row.ToImmutable());
							row.Clear();
						}
						continue;
					case TokenKind.Comma:
						Advance();
						continue;
				}

				row.Add(ParseExpression());
				if (At(TokenKind.Comma))
					Advance();
				else if (!AtAny(TokenKind.Semicolon, TokenKind.NewLine, TokenKind.CloseBracket, TokenKind.EndOfFile)
					&& !(Current.HasLeadingSpace && CanStartExpression(Current.Kind)))
					throw Error($"Expected ',', ';' or ']' but found {Describe(Current)}.");
			}
			if (row.Count > 0)
				rows.Add(row.ToImmutable());
			return rows.ToImmutable();
		});
		Expect(TokenKind.CloseBracket, "']'");
		return new MatrixLiteral(SpanFrom(start), rows);
	}

	static bool CanStartExpression(TokenKind kind) => kind is TokenKind.Number or TokenKind.ImaginaryNumber
		or TokenKind.String or TokenKind.Identifier or TokenKind.OpenParen or TokenKind.OpenBracket
		or TokenKind.Plus or TokenKind.Minus or TokenKind.Not or TokenKind.Tilde;
}
