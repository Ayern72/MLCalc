using System.Numerics;
using MLCalc.Language;
using MLCalc.LinearAlgebra;

namespace MLCalc.Interpreter.Tests;

public class ConstantFoldingTests
{
	readonly TestHost host = new();

	public ConstantFoldingTests() => host.Run("a = 1.5; b = 0.25; v = [10 20 30];");

	/// <summary>Folds an expression for the variable x and prints the result; folded parts print as "#".</summary>
	string Fold(string expression, string variable = "x")
	{
		var parse = Parser.Parse(expression);
		Assert.False(parse.HasErrors);
		var statement = Assert.IsType<ExpressionStatement>(Assert.Single(parse.Root.Statements));
		return SyntaxPrinter.Print(host.Interpreter.FoldConstants(statement.Expression, variable)).Replace(nameof(FoldedConstant), "#");
	}

	[Theory]
	[InlineData("sin(pi/4)*a*x^2", "(* # (^ x #))")]
	[InlineData("2*pi", "#")]
	[InlineData("x", "x")]
	[InlineData("-x + -a", "(+ (- x) #)")]
	[InlineData("[1 a; x 2]", "[# #; x #]")]
	[InlineData("[1 a; 3 4] * [x; 1]", "(* # [x; #])")]
	[InlineData("sqrt(a^2 + b^2) + x", "(+ # x)")]
	[InlineData("(0:a:3) + x", "(+ # x)")]
	[InlineData("0:a:3 + x", "(: # # (+ # x))")]
	[InlineData("x:a:3", "(: x # #)")]
	[InlineData("5! * x'", "(* # (' x))")]
	public void ConstantBranchesAreReplaced(string expression, string expected)
	{
		Assert.Equal(expected, Fold(expression));
	}

	[Fact]
	public void TheFunctionVariableIsNeverFoldedEvenIfAGlobalHasTheSameName()
	{
		host.Run("x = 99;");
		Assert.Equal("(+ x #)", Fold("x + a"));
		Assert.Equal("(+ X #)", Fold("X + a"));
	}

	[Fact]
	public void ConstantsCanBeShadowedByTheVariable()
	{
		Assert.Equal("(* # i)", Fold("2 * i", variable: "i"));
		Assert.Equal("#", Fold("2 * i", variable: "t"));
	}

	[Fact]
	public void RandAndCommandsAreNotFolded()
	{
		Assert.Equal("(+ rand #)", Fold("rand + 1"));
		Assert.Equal("(+ (rand #) x)", Fold("rand(2) + x"));
	}

	[Fact]
	public void FailingConstantsAreKeptSoTheErrorStillHappens()
	{
		Assert.Equal("(+ x (/ # #))", Fold("x + 1/0"));
		Assert.Equal("(+ x q)", Fold("x + q"));
		var error = host.Fail("feval(\"x\", \"x + 1/0\", 1)");
		Assert.Equal("In \"x + 1/0\": Division by zero.", error.Message);
	}

	[Fact]
	public void IndexingWithConstantIndicesIsFolded()
	{
		Assert.Equal("(* # x)", Fold("v(2) * x"));
		Assert.Equal("(v x)", Fold("v(x)"));
		Assert.Equal("(* # x)", Fold("v(1, :) * x"));
	}

	[Fact]
	public void ArgumentsOfNonPureFunctionsAreFoldedButNamesAreKept()
	{
		Assert.Equal("(+ (feval \"t\" \"t\" #) x)", Fold("feval(\"t\", \"t\", 2*a) + x"));
		Assert.Equal("(del a)", Fold("del(a)"));
	}

	[Fact]
	public void FoldingDoesNotChangeGlobalVariables()
	{
		host.Run("y = feval(\"x\", \"a*x\", 2);");
		Assert.False(host.Interpreter.TryGetVariable("x", out _));
		TestHost.AssertClose(3, host["y"]);
	}

	public static TheoryData<string> Expressions() => new()
	{
		"sin(pi/4)*a*x^2 + cos(pi/3)*exp(-b)*x + sqrt(a^2 + b^2)",
		"x*sin(x) + x^2/(1 + x^2)",
		"1/x",
		"ln(x) + log10(a)",
		"(x - a)^2 + 4",
		"-2^2 + x .^ 2",
		"sqrt(x) * i",
		"x mod 2 + a div 1",
		"abs(x) + arg(x) + re(x) + im(x)",
		"conj(x) + sign(x)",
		"sum([1 a; 3 4] * [x; 1])",
		"sum([x a b]) + max(x, pi)",
		"v(2) * x + v(3)",
		"(x > a) + (x == 0) + (a < b)",
		"not x or a and b",
		"factorial(3) * x",
		"comb(5, 2) + perm(5, 2) * x",
		"atanh(x) + acoth(a)",
		"xroot(x, 3) + xroot(-8, 3)",
		"floor(x) + ceil(a) + round(b) + fix(-a)",
		"numel([1 2 3] * x)",
		"sum(inv([a 1; 1 a]) * [x; 1])",
		"det([x 1; 1 a])",
		"polyval([1 a b], x)",
		"x + 1/0",
		"x + nope",
		// Paths specific to compiled functions
		"x(1) * 2",
		"v(round(abs(x)) + 1)",
		"sum(1:x)",
		"sum(x:a:3)",
		"nope(x)",
		"x!",
		"rand * 0 + x",
		"sin(cos(tan(x)))",
	};

	[Fact]
	public void CommandUsedAsValueNamesTheCommand()
	{
		Assert.Equal("'xrange' is a command and does not return a value.", host.Fail("y = xrange(0, 1)").Message);
	}

	[Theory]
	[InlineData("x + del(all)", "del")]
	[InlineData("xrange(0, a) + x", "xrange")]
	[InlineData("x + cls", "cls")]
	[InlineData("pen(2) + x", "pen")]
	[InlineData("dot(x, x) + x", "dot")]
	public void CommandsAreRefusedInFunctionsGivenAsTextBeforeAnythingRuns(string expression, string command)
	{
		var error = host.Fail($"feval(\"x\", \"{expression}\", 1)");
		Assert.Equal($"In \"{expression}\": '{command}' is a command and cannot be used in a function given as text.", error.Message);
		// Nothing ran: the variables are still there and the plot is unchanged.
		TestHost.AssertClose(1.5, host["a"]);
		Assert.Equal(-10, host.Figure.Current.XMin);
	}

	[Fact]
	public void AVariableNamedLikeACommandIsStillAllowed()
	{
		host.Run("grid = 2;");
		TestHost.AssertClose(6, host.Eval("feval(\"x\", \"grid * x\", 3)"));
	}

	[Theory]
	[MemberData(nameof(Expressions))]
	public void FoldedFunctionsGiveTheSameResultsAsUnfoldedEvaluation(string expression)
	{
		string[] points = ["-2", "-0.5", "0", "0.7", "3", "(1+2i)"];
		foreach (string point in points)
		{
			var expected = Reference(expression, point);
			var actual = Folded(expression, point);
			Assert.True(expected.Equals(actual), $"{expression} at x = {point}: expected {expected}, got {actual}");
		}
	}

	/// <summary>Evaluates normally, with x as a global variable (no folding involved).</summary>
	string Reference(string expression, string point)
	{
		host.Interpreter.DeleteVariable("r__");
		var result = host.Interpreter.Execute($"x = {point}; r__ = {expression};");
		host.Interpreter.DeleteVariable("x");
		Assert.False(result.Parse.HasErrors, string.Join("; ", result.Messages()));
		return result.Error is { } error ? "error: " + error.Message : Format(host["r__"]);
	}

	/// <summary>Evaluates through feval, where the expression is compiled and folded.</summary>
	string Folded(string expression, string point)
	{
		host.Interpreter.DeleteVariable("r__");
		var result = host.Interpreter.Execute($"r__ = feval(\"x\", \"{expression}\", {point});");
		Assert.False(result.Parse.HasErrors, string.Join("; ", result.Messages()));
		if (result.Error is { } error)
			return "error: " + error.Message.Replace($"In \"{expression}\": ", "").Replace("feval: the function must return a scalar.", "not a scalar");
		return Format(host["r__"]);
	}

	static string Format(ComplexMatrix value) =>
		value.IsScalar ? ComplexMatrix.Format(value.ScalarValue, "R") : "matrix " + value;
}
