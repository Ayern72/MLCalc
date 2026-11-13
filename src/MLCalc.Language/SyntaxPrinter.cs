using System.Globalization;

namespace MLCalc.Language;

/// <summary>Renders syntax trees as compact S-expressions, e.g. <c>(+ 1 (* 2 3))</c>.</summary>
public static class SyntaxPrinter
{
	public static string Print(SyntaxNode node) => node switch
	{
		NumberLiteral n => Number(n.Value) + (n.IsImaginary ? "i" : ""),
		StringLiteral s => "\"" + s.Value + "\"",
		Identifier id => id.Name,
		ColonAll => ":",
		StyleLiteral style => "`" + style.Symbol,
		UnaryExpression u => $"({Symbol(u.Operator)} {Print(u.Operand)})",
		BinaryExpression b => $"({Symbol(b.Operator)} {Print(b.Left)} {Print(b.Right)})",
		PostfixExpression p => $"({Symbol(p.Operator)} {Print(p.Operand)})",
		RangeExpression r => r.Step is null
			? $"(: {Print(r.Start)} {Print(r.End)})"
			: $"(: {Print(r.Start)} {Print(r.Step)} {Print(r.End)})",
		ApplyExpression a => $"({Print(a.Target)}{Arguments(a.Arguments)})",
		MatrixLiteral m => "[" + string.Join("; ", m.Rows.Select(row => string.Join(" ", row.Select(Print)))) + "]",
		ExpressionStatement e => Print(e.Expression) + (e.IsSilent ? ";" : ""),
		AssignmentStatement a => a.Indices.IsEmpty
			? $"(= {Print(a.Target)} {Print(a.Value)})" + (a.IsSilent ? ";" : "")
			: $"(= ({Print(a.Target)}{Arguments(a.Indices)}) {Print(a.Value)})" + (a.IsSilent ? ";" : ""),
		IfStatement i => i.Else is null
			? $"(if {Print(i.Condition)} {Print(i.Then)})"
			: $"(if {Print(i.Condition)} {Print(i.Then)} {Print(i.Else)})",
		WhileStatement w => $"(while {Print(w.Condition)} {Print(w.Body)})",
		ForStatement f => f.End is null
			? $"(for {Print(f.Variable)} {Print(f.Start)} {Print(f.Body)})"
			: $"(for {Print(f.Variable)} {Print(f.Start)} {Print(f.End)} {Print(f.Body)})",
		BlockStatement b => "{" + string.Join(" ", b.Statements.Select(Print)) + "}",
		ProgramSyntax p => string.Join("\n", p.Statements.Select(Print)),
		_ => node.GetType().Name,
	};

	static string Arguments(IEnumerable<Expression> arguments) =>
		string.Concat(arguments.Select(a => " " + Print(a)));

	static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

	public static string Symbol(UnaryOperator op) => op switch
	{
		UnaryOperator.Plus => "+",
		UnaryOperator.Negate => "-",
		UnaryOperator.Not => "not",
		_ => op.ToString(),
	};

	public static string Symbol(PostfixOperator op) => op switch
	{
		PostfixOperator.ConjugateTranspose => "'",
		PostfixOperator.Transpose => ".'",
		PostfixOperator.Factorial => "!",
		_ => op.ToString(),
	};

	public static string Symbol(BinaryOperator op) => op switch
	{
		BinaryOperator.Add => "+",
		BinaryOperator.Subtract => "-",
		BinaryOperator.Multiply => "*",
		BinaryOperator.Divide => "/",
		BinaryOperator.LeftDivide => "\\",
		BinaryOperator.Power => "^",
		BinaryOperator.ElementMultiply => ".*",
		BinaryOperator.ElementDivide => "./",
		BinaryOperator.ElementLeftDivide => ".\\",
		BinaryOperator.ElementPower => ".^",
		BinaryOperator.Modulo => "mod",
		BinaryOperator.IntegerDivide => "div",
		BinaryOperator.Equal => "==",
		BinaryOperator.NotEqual => "~=",
		BinaryOperator.Less => "<",
		BinaryOperator.LessOrEqual => "<=",
		BinaryOperator.Greater => ">",
		BinaryOperator.GreaterOrEqual => ">=",
		BinaryOperator.And => "and",
		BinaryOperator.Or => "or",
		BinaryOperator.Xor => "xor",
		_ => op.ToString(),
	};
}
