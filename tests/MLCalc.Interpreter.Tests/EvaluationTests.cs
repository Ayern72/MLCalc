using System.Numerics;
using MLCalc.LinearAlgebra;
using static MLCalc.Interpreter.Tests.TestHost;

namespace MLCalc.Interpreter.Tests;

public class EvaluationTests
{
	readonly TestHost host = new();

	[Theory]
	[InlineData("1 + 2 * 3", 7)]
	[InlineData("-2^2", -4)]
	[InlineData("2^-1", 0.5)]
	[InlineData("7 mod 3", 1)]
	[InlineData("7 div 2", 3)]
	[InlineData("5!", 120)]
	[InlineData("sin(pi/2)", 1)]
	[InlineData("abs(3+4i)", 5)]
	[InlineData("sqr(3)", 9)]
	[InlineData("xroot(-8, 3)", -2)]
	[InlineData("comb(5, 2)", 10)]
	[InlineData("ln(exp(2))", 2)]
	[InlineData("log10(1000)", 3)]
	[InlineData("1e-3 * 1000", 1)]
	[InlineData("(1 < 2) + (2 == 2) + (3 ~= 3)", 2)]
	[InlineData("1 and 0 or 1", 1)]
	[InlineData("not 0", 1)]
	[InlineData("1 xor 1", 0)]
	public void Scalars(string expression, double expected)
	{
		AssertClose(expected, host.Eval(expression));
	}

	[Fact]
	public void ComplexArithmetic()
	{
		Assert.Equal(new Complex(-1, 0), host.Eval("i^2").ScalarValue);
		Assert.Equal(new Complex(0, 2), host.Eval("sqrt(-4)").ScalarValue);
		Assert.Equal(new Complex(1, -2), host.Eval("(1+2i)'").ScalarValue);
	}

	[Fact]
	public void MatrixOperatorsFollowMatlab()
	{
		AssertMatrix(new double[,] { { 3 }, { 7 } }, host.Eval("[1, 2; 3, 4] * [1; 1]"));
		AssertMatrix(new double[,] { { 3, 8 } }, host.Eval("[1 2] .* [3 4]"));
		AssertMatrix(new double[,] { { 7, 10 }, { 15, 22 } }, host.Eval("[1 2; 3 4]^2"));
		AssertMatrix(new double[,] { { 1, 4 }, { 9, 16 } }, host.Eval("[1 2; 3 4].^2"));
		AssertMatrix(new double[,] { { 1 }, { 2 } }, host.Eval("[1 2; 3 4] \\ [5; 11]"));
		AssertMatrix(new double[,] { { 2, 4 } }, host.Eval("[1 2] * 2"));
		AssertMatrix(new double[,] { { 1, 3 }, { 2, 4 } }, host.Eval("[1 2; 3 4]'"));
	}

	[Fact]
	public void MatrixProductWithIncompatibleSizesReportsTheOperator()
	{
		var error = host.Fail("x = [1 2] * [3 4]");
		Assert.Contains("multiply", error.Message);
		Assert.Equal(4, error.Span.Start);
	}

	[Fact]
	public void MatrixLiteralsConcatenate()
	{
		host.Run("a = [1 2]; b = [a, 3; 4 5 6]");
		AssertMatrix(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } }, host["b"]);
	}

	[Fact]
	public void Ranges()
	{
		AssertMatrix(new double[,] { { 1, 2, 3, 4 } }, host.Eval("1:4"));
		AssertMatrix(new double[,] { { 0, 0.5, 1 } }, host.Eval("0:0.5:1"));
		AssertMatrix(new double[,] { { 3, 2, 1 } }, host.Eval("3:-1:1"));
		Assert.True(host.Eval("3:1").IsEmpty);
	}

	[Fact]
	public void Constants()
	{
		AssertClose(System.Math.PI, host.Eval("PI"));
		Assert.Equal(Complex.ImaginaryOne, host.Eval("i").ScalarValue);
		Assert.True(host.Eval("eps").ScalarValue.Real is > 2e-16 and < 3e-16);
	}

	[Fact]
	public void VariableNamedIShadowsImaginaryUnit()
	{
		host.Run("i = 5;");
		AssertClose(6, host.Eval("i + 1"));
		AssertClose(-1, host.Eval("j^2"));
	}

	[Fact]
	public void NamesAreCaseInsensitive()
	{
		host.Run("deltar = 0.1; r = 2 * Deltar;");
		AssertClose(0.2, host["R"]);
		AssertClose(1, host.Eval("SIN(Pi/2)"));
	}

	[Fact]
	public void ResultsAreWrittenUnlessSilent()
	{
		host.Run("x = 3\ny = 4;\nx + y\nx");
		Assert.Equal(["x", "ans", "x"], host.Output.Values.Select(v => v.Name));
		AssertClose(7, host["ans"]);
	}

	[Fact]
	public void CommandsDoNotChangeAns()
	{
		host.Run("5; xrange(0, 1)");
		AssertClose(5, host["ans"]);
		Assert.Empty(host.Output.Values);
	}
}

public class IndexingTests
{
	readonly TestHost host = new();

	public IndexingTests() => host.Run("A = [1 2 3; 4 5 6]; v = [10 20 30];");

	[Fact]
	public void ElementAccess()
	{
		AssertClose(6, host.Eval("A(2, 3)"));
		AssertClose(20, host.Eval("v(2)"));
		AssertClose(4, host.Eval("A(2)")); // column-major linear index
	}

	[Fact]
	public void ColonSelectsWholeRowsAndColumns()
	{
		AssertMatrix(new double[,] { { 4, 5, 6 } }, host.Eval("A(2,:)"));
		AssertMatrix(new double[,] { { 2 }, { 5 } }, host.Eval("A(:,2)"));
		AssertMatrix(new double[,] { { 1 }, { 4 }, { 2 }, { 5 }, { 3 }, { 6 } }, host.Eval("A(:)"));
	}

	[Fact]
	public void VectorIndices()
	{
		AssertMatrix(new double[,] { { 30, 10 } }, host.Eval("v([3 1])"));
		AssertMatrix(new double[,] { { 2, 3 }, { 5, 6 } }, host.Eval("A(:, 2:3)"));
	}

	[Fact]
	public void AssignmentReplacesElements()
	{
		host.Run("A(1, 2) = 0; v(:) = 7;");
		AssertMatrix(new double[,] { { 1, 0, 3 }, { 4, 5, 6 } }, host["A"]);
		AssertMatrix(new double[,] { { 7, 7, 7 } }, host["v"]);
	}

	[Fact]
	public void AssignmentGrowsMatrices()
	{
		host.Run("w(3) = 1; M(2, 3) = 5; v(5) = 50;");
		AssertMatrix(new double[,] { { 0, 0, 1 } }, host["w"]);
		AssertMatrix(new double[,] { { 0, 0, 0 }, { 0, 0, 5 } }, host["M"]);
		AssertMatrix(new double[,] { { 10, 20, 30, 0, 50 } }, host["v"]);
	}

	[Fact]
	public void AssignRowFromVector()
	{
		host.Run("A(2,:) = [7 8 9];");
		AssertMatrix(new double[,] { { 1, 2, 3 }, { 7, 8, 9 } }, host["A"]);
	}

	[Fact]
	public void IndexOutOfRangeIsAnError()
	{
		var error = host.Fail("x = A(3, 1)");
		Assert.Contains("exceeds", error.Message);
	}

	[Fact]
	public void IndexMustBePositiveInteger()
	{
		Assert.Contains("positive integer", host.Fail("v(0)").Message);
		Assert.Contains("positive integer", host.Fail("v(1.5)").Message);
	}

	[Fact]
	public void SizeMismatchInAssignmentIsAnError()
	{
		Assert.Contains("Cannot assign", host.Fail("A(1,:) = [1 2]").Message);
	}
}

public class ControlFlowTests
{
	readonly TestHost host = new();

	[Fact]
	public void WhileLoop()
	{
		host.Run("k = 1; s = 0;\nwhile k <= 10 do\nbegin\n\ts = s + k;\n\tk = k + 1;\nend");
		AssertClose(55, host["s"]);
	}

	[Fact]
	public void IfElse()
	{
		host.Run("a = 3;\nif a > 2 then b = 1 else b = 2;");
		AssertClose(1, host["b"]);
		host.Run("if a > 5 then b = 1\nelse if a > 2 then b = 3\nelse b = 4;");
		AssertClose(3, host["b"]);
	}

	[Fact]
	public void ForToLoop()
	{
		host.Run("s = 0; for k = 1 to 4 do s = s + k;");
		AssertClose(10, host["s"]);
		AssertClose(4, host["k"]);
	}

	[Fact]
	public void ForOverRange()
	{
		host.Run("s = 0; for k = 2:2:8 do s = s + k;");
		AssertClose(20, host["s"]);
	}

	[Fact]
	public void MatlabStyleBlocks()
	{
		const string source = """
			s = 0;
			for k = 1:10
				if mod(k, 2) == 0
					s = s + k;
				elseif k == 5
					s = s + 100;
				else
					s = s - 1;
				end
			end
			n = 0;
			while n < 3, n = n + 1; end
			""";
		host.Run(source);
		// Even numbers 2+4+6+8+10 = 30, plus 100 for k = 5, minus 1 for 1, 3, 7, 9.
		AssertClose(126, host["s"]);
		AssertClose(3, host["n"]);
	}

	[Fact]
	public void MatrixConditionRequiresAllNonZero()
	{
		host.Run("if [1 1 0] then a = 1 else a = 2;");
		AssertClose(2, host["a"]);
	}

	[Fact]
	public void ErrorStopsExecution()
	{
		var result = host.Interpreter.Execute("a = 1;\nb = undefined + 1;\nc = 3;");
		Assert.NotNull(result.Error);
		Assert.Contains("undefined", result.Error.Message);
		Assert.False(host.Interpreter.TryGetVariable("c", out _));
		Assert.Equal("2:5: Unknown function or variable 'undefined'.", result.Messages().Single());
	}

	[Fact]
	public void SyntaxErrorRunsNothing()
	{
		var result = host.Interpreter.Execute("a = 1;\nb = (2 +;");
		Assert.True(result.Parse.HasErrors);
		Assert.False(host.Interpreter.TryGetVariable("a", out _));
	}

	[Fact]
	public void CancellationStopsInfiniteLoop()
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
		var result = host.Interpreter.Execute("k = 0; while 1 do k = k + 1;", cts.Token);
		Assert.True(result.Cancelled);
		Assert.False(result.Success);
	}
}

public class FunctionTests
{
	readonly TestHost host = new();

	[Fact]
	public void MatrixConstructors()
	{
		AssertMatrix(new double[,] { { 1, 1, 1 }, { 1, 1, 1 } }, host.Eval("ones(2, 3)"));
		AssertMatrix(new double[,] { { 0, 0 }, { 0, 0 } }, host.Eval("zeroes(2)"));
		AssertMatrix(new double[,] { { 1, 0 }, { 0, 1 } }, host.Eval("Id(2)"));
		AssertMatrix(new double[,] { { 1, 1 } }, host.Eval("ones([5 6])"));
		var r = host.Eval("rand(2, 3)");
		Assert.Equal((2, 3), (r.Rows, r.Columns));
		Assert.All(r.AsSpan().ToArray(), v => Assert.InRange(v.Real, 0, 1));
	}

	[Fact]
	public void SizesFollowRowsAndColumns()
	{
		host.Run("A = ones(2, 5);");
		AssertMatrix(new double[,] { { 2, 5 } }, host.Eval("size(A)"));
		AssertClose(2, host.Eval("row(A)"));
		AssertClose(5, host.Eval("col(A)"));
		AssertClose(5, host.Eval("length(A)"));
	}

	[Fact]
	public void LinearAlgebra()
	{
		AssertClose(-2, host.Eval("det([1 2; 3 4])"));
		AssertMatrix(new double[,] { { -2, 1 }, { 1.5, -0.5 } }, host.Eval("inv([1 2; 3 4])"));
		AssertMatrix(new double[,] { { 1 }, { 2 } }, host.Eval("gauss([1 2; 3 4], [5; 11])"));
	}

	[Fact]
	public void SumMaxMin()
	{
		AssertClose(6, host.Eval("sum([1 2 3])"));
		AssertMatrix(new double[,] { { 4, 6 } }, host.Eval("sum([1 2; 3 4])"));
		AssertClose(3, host.Eval("max([1 3 2])"));
		AssertClose(1, host.Eval("min([1 3 2])"));
	}

	[Fact]
	public void Polynomials()
	{
		host.Run("p = polyfit([0 1 2 3], [1 3 4 4], 1);");
		AssertMatrix(new double[,] { { 1.5, 1 } }, host["p"]);
		AssertMatrix(new double[,] { { 1.5, 11.5 } }, host.Eval("polyval(p, [0 10])"));
	}

	[Fact]
	public void FunctionsGivenAsStrings()
	{
		AssertMatrix(new double[,] { { 0, 1, 4 } }, host.Eval("feval(\"t\", \"t^2\", [0 1 2])"));
		AssertClose(1.0 / 3, host.Eval("integral(\"x\", \"x^2\", 0, 1)"));
		AssertClose(27, host.Eval("derivata(\"x\", \"x^3\", 3)"), 1e-7);
		AssertClose(System.Math.Sqrt(2), host.Eval("solve(\"x\", \"x^2 - 2\", 1)"));
		AssertClose(2, host.Eval("isect(\"x\", \"x^2\", \"x + 2\", 1.5)"));
		AssertClose(1.5, host.Eval("extreme(\"x\", \"(x - 1.5)^2\", 0)"), 1e-6);
	}

	[Fact]
	public void StringFunctionsSeeGlobalVariablesButDoNotChangeThem()
	{
		host.Run("a = 10; x = 99;");
		AssertMatrix(new double[,] { { 10, 20 } }, host.Eval("feval(\"x\", \"a * x\", [1 2])"));
		AssertClose(99, host["x"]);
	}

	[Fact]
	public void ErrorInsideStringFunctionMentionsTheString()
	{
		var error = host.Fail("feval(\"x\", \"x + nope\", 1)");
		Assert.Contains("\"x + nope\"", error.Message);
		Assert.Contains("nope", error.Message);
	}

	[Fact]
	public void WrongArgumentCountIsReported()
	{
		Assert.Contains("expected 1 argument", host.Fail("sin(1, 2)").Message);
	}

	[Fact]
	public void UnknownFunctionIsReported()
	{
		var error = host.Fail("y = frobnicate(2)");
		Assert.Equal("Unknown function or variable 'frobnicate'.", error.Message);
		Assert.Equal(new Language.SourceSpan(4, 10), error.Span);
	}

	[Fact]
	public void CommandUsedAsValueIsAnError()
	{
		Assert.Contains("does not return a value", host.Fail("x = xrange(0, 1)").Message);
	}
}

public class ConsoleCommandTests
{
	readonly TestHost host = new();

	[Fact]
	public void ListAndDelete()
	{
		host.Run("b = 1; a = [1 2];\nlist\ndel(a)\nlist");
		Assert.Equal(["a\t1×2", "b\t1×1", "a is deleted.", "b\t1×1"], host.Output.Lines);
	}

	[Fact]
	public void ClsClearsConsole()
	{
		host.Run("cls");
		Assert.Equal(1, host.Output.Clears);
	}

	[Fact]
	public void DispWritesValueOrText()
	{
		host.Run("disp(\"hello\")\ndisp([1 2])");
		Assert.Equal(["hello", "  1  2"], host.Output.Lines);
	}

	[Fact]
	public void FormatterAlignsColumns()
	{
		var value = ComplexMatrix.FromRows(new double[,] { { 1, -20 }, { 300, 4 } });
		Assert.Equal($"A ={Environment.NewLine}    1  -20{Environment.NewLine}  300    4", ResultFormatter.Format("A", value));
		Assert.Equal("z = 1+2i", ResultFormatter.Format("z", new Complex(1, 2)));
	}
}
