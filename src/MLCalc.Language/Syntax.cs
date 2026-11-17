using System.Collections.Immutable;

namespace MLCalc.Language;

/// <summary>Base of all syntax nodes. <see cref="ToString"/> renders the node as an S-expression.</summary>
public abstract record SyntaxNode(SourceSpan Span)
{
	/// <summary>The node as an S-expression, e.g. <c>(+ 1 (* 2 x))</c>.</summary>
	public sealed override string ToString() => SyntaxPrinter.Print(this);
}

// ───────────── Expressions ─────────────

/// <summary>Base of all expressions: anything that can give a value.</summary>
public abstract record Expression(SourceSpan Span) : SyntaxNode(Span);

/// <summary>A real number literal, or an imaginary literal such as <c>2i</c>.</summary>
public sealed record NumberLiteral(SourceSpan Span, double Value, bool IsImaginary = false) : Expression(Span);

/// <summary>A double-quoted string, used for function arguments such as <c>fplot("x", "sin(x)")</c>.</summary>
public sealed record StringLiteral(SourceSpan Span, string Value) : Expression(Span);

/// <summary>A variable, constant, function or command name. Name lookup is case-insensitive.</summary>
public sealed record Identifier(SourceSpan Span, string Name) : Expression(Span);

/// <summary>A bare <c>:</c> argument meaning "all rows" or "all columns", as in <c>t(1,:)</c>.</summary>
public sealed record ColonAll(SourceSpan Span) : Expression(Span);

/// <summary>A bare operator symbol used as an argument, such as the line style in <c>plot(x, y, -)</c>.</summary>
public sealed record StyleLiteral(SourceSpan Span, string Symbol) : Expression(Span);

/// <summary>A prefix operator.</summary>
public enum UnaryOperator
{
	/// <summary>Unary plus <c>+</c>, which changes nothing.</summary>
	Plus,
	/// <summary>Negation <c>-</c>.</summary>
	Negate,
	/// <summary>Logical not: <c>not</c> or <c>~</c>.</summary>
	Not,
}

/// <summary>A prefix operator applied to an operand, e.g. <c>-x</c>.</summary>
public sealed record UnaryExpression(SourceSpan Span, UnaryOperator Operator, Expression Operand) : Expression(Span);

/// <summary>An infix operator.</summary>
public enum BinaryOperator
{
	/// <summary>Sum <c>+</c>.</summary>
	Add,
	/// <summary>Difference <c>-</c>.</summary>
	Subtract,
	/// <summary>Matrix product <c>*</c>.</summary>
	Multiply,
	/// <summary>Matrix right division <c>/</c>.</summary>
	Divide,
	/// <summary>Matrix left division <c>\</c>.</summary>
	LeftDivide,
	/// <summary>Matrix power <c>^</c>.</summary>
	Power,
	/// <summary>Element-wise product <c>.*</c>.</summary>
	ElementMultiply,
	/// <summary>Element-wise quotient <c>./</c>.</summary>
	ElementDivide,
	/// <summary>Element-wise left division <c>.</c>.</summary>
	ElementLeftDivide,
	/// <summary>Element-wise power <c>.^</c>.</summary>
	ElementPower,
	/// <summary>Remainder <c>mod</c>, with the sign of the number divided.</summary>
	Modulo,
	/// <summary>Integer division <c>div</c>.</summary>
	IntegerDivide,
	/// <summary>Comparison <c>==</c>.</summary>
	Equal,
	/// <summary>Comparison <c>~=</c> or <c>&lt;&gt;</c>.</summary>
	NotEqual,
	/// <summary>Comparison <c>&lt;</c>.</summary>
	Less,
	/// <summary>Comparison <c>&lt;=</c>.</summary>
	LessOrEqual,
	/// <summary>Comparison <c>&gt;</c>.</summary>
	Greater,
	/// <summary>Comparison <c>&gt;=</c>.</summary>
	GreaterOrEqual,
	/// <summary>Logical and: <c>and</c>, <c>&amp;</c> or <c>&amp;&amp;</c>; both sides are evaluated.</summary>
	And,
	/// <summary>Logical or: <c>or</c>, <c>|</c> or <c>||</c>; both sides are evaluated.</summary>
	Or,
	/// <summary>Exclusive or <c>xor</c>.</summary>
	Xor,
}

/// <summary>An infix operator applied to two operands, e.g. <c>a * b</c>.</summary>
public sealed record BinaryExpression(SourceSpan Span, BinaryOperator Operator, Expression Left, Expression Right) : Expression(Span);

/// <summary>An operator written after its operand.</summary>
public enum PostfixOperator
{
	/// <summary>Conjugate transpose <c>'</c>.</summary>
	ConjugateTranspose,
	/// <summary>Plain transpose <c>.'</c>.</summary>
	Transpose,
	/// <summary>Factorial <c>!</c>.</summary>
	Factorial,
}

/// <summary>A postfix operator applied to an operand, e.g. <c>A'</c> or <c>5!</c>.</summary>
public sealed record PostfixExpression(SourceSpan Span, PostfixOperator Operator, Expression Operand) : Expression(Span);

/// <summary>A range <c>start:end</c> or <c>start:step:end</c>.</summary>
public sealed record RangeExpression(SourceSpan Span, Expression Start, Expression? Step, Expression End) : Expression(Span);

/// <summary>
/// <c>name(arguments)</c>: a function call or an index into a variable. Which one is decided at run
/// time.
/// </summary>
public sealed record ApplyExpression(SourceSpan Span, Identifier Target, ImmutableArray<Expression> Arguments) : Expression(Span);

/// <summary>A matrix literal <c>[a, b; c, d]</c>, as a list of rows.</summary>
public sealed record MatrixLiteral(SourceSpan Span, ImmutableArray<ImmutableArray<Expression>> Rows) : Expression(Span);

// ───────────── Statements ─────────────

/// <summary>Base of all statements.</summary>
public abstract record Statement(SourceSpan Span) : SyntaxNode(Span);

/// <summary>An expression or command. <paramref name="IsSilent"/> is true when it ends with <c>;</c>.</summary>
public sealed record ExpressionStatement(SourceSpan Span, Expression Expression, bool IsSilent) : Statement(Span);

/// <summary><c>name = value</c>, or <c>name(indices) = value</c> when <paramref name="Indices"/> is not empty.</summary>
public sealed record AssignmentStatement(SourceSpan Span, Identifier Target, ImmutableArray<Expression> Indices, Expression Value, bool IsSilent) : Statement(Span);

/// <summary><c>if condition then body [else body]</c>.</summary>
public sealed record IfStatement(SourceSpan Span, Expression Condition, Statement Then, Statement? Else) : Statement(Span);

/// <summary><c>while condition do body</c>.</summary>
public sealed record WhileStatement(SourceSpan Span, Expression Condition, Statement Body) : Statement(Span);

/// <summary>
/// <c>for name = start to end do body</c> (with <paramref name="End"/>), or <c>for name = values do body</c>,
/// which iterates over the columns of <paramref name="Start"/>.
/// </summary>
public sealed record ForStatement(SourceSpan Span, Identifier Variable, Expression Start, Expression? End, Statement Body) : Statement(Span);

/// <summary><c>begin statements end</c>.</summary>
public sealed record BlockStatement(SourceSpan Span, ImmutableArray<Statement> Statements) : Statement(Span);

/// <summary>A whole script or console input.</summary>
public sealed record ProgramSyntax(SourceSpan Span, ImmutableArray<Statement> Statements) : SyntaxNode(Span);
