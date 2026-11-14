using MLCalc.LinearAlgebra;
using static MLCalc.Interpreter.Tests.TestHost;

namespace MLCalc.Interpreter.Tests;

/// <summary>Language behaviour decided after the second review.</summary>
public class SecondReviewTests
{
	readonly TestHost host = new();

	/// <summary>NaN was "non-zero, so true": <c>while nan</c> never ended and NaN comparisons chose a branch silently.</summary>
	[Theory]
	[InlineData("if nan then x = 1;")]
	[InlineData("while nan do x = 1;")]
	[InlineData("if [1 nan] then x = 1;")]
	[InlineData("if [0 nan] then x = 1;")]
	[InlineData("if inf - inf, x = 1; else, x = 2; end")]
	public void NaNConditionIsAnError(string program)
	{
		Assert.Contains("NaN", host.Fail(program).Message);
	}

	[Theory]
	[InlineData("not nan")]
	[InlineData("nan and 1")]
	[InlineData("0 and nan")]
	[InlineData("nan or 0")]
	[InlineData("1 xor nan")]
	[InlineData("[1 2 nan] and 1")]
	public void NaNInALogicalOperatorIsAnError(string expression)
	{
		Assert.Contains("NaN cannot be used as a truth value", host.Fail($"x = {expression};").Message);
	}

	[Fact]
	public void NaNInALogicalOperatorInsideAFunctionGivenAsTextIsAnError()
	{
		Assert.Contains("NaN cannot be used as a truth value", host.Fail("integral(\"x\", \"not (x * nan)\", 0, 1)").Message);
	}

	[Fact]
	public void LogicalOperatorsWithoutNaNAreUnchanged()
	{
		AssertMatrix(new double[,] { { 1, 0, 0, 1 } }, host.Eval("[1 0 0 1] and 1"));
		AssertMatrix(new double[,] { { 1, 1, 0, 1 } }, host.Eval("[1 0 0 1] or [0 2 0 0]"));
		AssertClose(1, host.Eval("not 0"));
		AssertClose(0, host.Eval("not 2i"));
		AssertClose(1, host.Eval("2 xor 0"));
		// Comparisons with NaN are false (or true for ~=), so they can be used to test for NaN.
		AssertClose(0, host.Eval("nan == nan"));
		AssertClose(1, host.Eval("nan ~= nan"));
		host.Run("if nan ~= nan, x = 1; else, x = 2; end");
		AssertClose(1, host["x"]);
	}

	[Fact]
	public void SumAndProductOfTheEmptyMatrix()
	{
		AssertClose(0, host.Eval("sum([])"));
		AssertClose(1, host.Eval("prod([])"));
		AssertMatrix(new double[,] { { 0, 0, 0 } }, host.Eval("sum(zeros(0, 3))"));
		AssertMatrix(new double[,] { { 1, 1 } }, host.Eval("prod(zeros(0, 2))"));
		Assert.True(host.Eval("sum(zeros(3, 0))").IsEmpty);
	}

	[Theory]
	[InlineData("x = round(-0.4)", "0")]
	[InlineData("x = 0 * -1", "0")]
	[InlineData("x = ceil(-0.5)", "0")]
	[InlineData("x = -0 + 2i", "2i")]
	public void NegativeZeroIsShownAsZero(string program, string expected)
	{
		host.Run(program);
		Assert.Equal(expected, ComplexMatrix.Format(host["x"].ScalarValue));
	}

	[Fact]
	public void BitmapImagesAreLimitedInPixels()
	{
		var saved = new List<(double Width, double Height)>();
		host.Interpreter.RegisterFigureFormat("png", (_, _, width, height) => saved.Add((width, height)));
		host.Interpreter.WorkingDirectory = Path.GetTempPath();
		Assert.Contains("at most 64 million pixels", host.Fail("savefig(\"a.png\", 16000, 16000)").Message);
		Assert.Contains("at most 64 million pixels", host.Fail("savefig(\"a.png\", 9000, 8000)").Message);
		host.Run("savefig(\"a.png\", 8000, 8000)");
		Assert.Equal((8000, 8000), Assert.Single(saved));
	}

	/// <summary>
	/// The search widens as it goes, so roots closer together than the current step are passed over
	/// (the manual describes this): by the time it reaches ±1.4 from 10⁶ the steps are far larger.
	/// </summary>
	[Fact]
	public void SolvePassesOverRootsCloserTogetherThanItsStep()
	{
		Assert.Contains("No root found near 1000000", host.Fail("solve(\"x\", \"x^2 - 2\", 1e6)").Message);
		AssertClose(System.Math.Sqrt(2), host.Eval("solve(\"x\", \"x^2 - 2\", 1)"), 1e-12);
	}

	/// <summary>Nearly singular matrices and matrices containing Inf are refused rather than solved with noise.</summary>
	[Theory]
	[InlineData("[inf 0; 0 1] \\ [1; 1]")]
	[InlineData("[1 2; 3 4] / [inf 0; 0 1]")]
	[InlineData("inv([inf 0; 0 1])")]
	[InlineData("inv([1 1; 1 1+1e-14])")]
	[InlineData("gauss([1 1; 1 1+1e-14], [1; 2])")]
	public void NearlySingularMatricesAreRefused(string expression)
	{
		Assert.Contains("the matrix is singular", host.Fail($"x = {expression};").Message);
	}

	[Fact]
	public void MatricesJustAboveTheThresholdAreSolved()
	{
		// The pivot 1e-12 is above 1e-13 times the largest element, so this is solved.
		AssertMatrix(new double[,] { { 1e12 + 1, -1e12 }, { -1e12, 1e12 } }, host.Eval("inv([1 1; 1 1+1e-12])"), 1e-3 * 1e12);
	}
}
