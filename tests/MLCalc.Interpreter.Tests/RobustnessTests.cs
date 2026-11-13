using MLCalc.LinearAlgebra;

namespace MLCalc.Interpreter.Tests;

/// <summary>
/// Inputs that used to crash the program or exhaust the computer's memory must be reported as
/// ordinary errors (found in the code review).
/// </summary>
public class RobustnessTests
{
	readonly TestHost host = new();

	[Fact]
	public void SignOfNaNIsNaN()
	{
		Assert.True(double.IsNaN(host.Eval("sign(nan)").ScalarValue.Real));
		var signs = host.Eval("sign([1 nan -2])").AsSpan().ToArray().Select(z => z.Real).ToArray();
		Assert.Equal(1, signs[0]);
		Assert.True(double.IsNaN(signs[1]));
		Assert.Equal(-1, signs[2]);
	}

	[Fact]
	public void NaNInsideSolveIsReportedNotCrashed()
	{
		// The function is NaN in a narrow band around its only root.
		var result = host.Interpreter.Execute("t = [0 nan]; solve(\"x\", \"x - 0.499 + t(1 + (x > 0.4985 and x < 0.4995))\", 0.001)");
		Assert.NotNull(result.Error);
		Assert.Contains("No root found", result.Error.Message);
	}

	[Theory]
	[InlineData("ones(46341)", "46341×46341 matrix would have")]
	[InlineData("zeros(30000, 30000)", "the limit is 25000000")]
	[InlineData("rand(100000)", "the limit is 25000000")]
	[InlineData("x(65536, 65536) = 1", "65536×65536 matrix")]
	[InlineData("y(1e9) = 1", "1×1000000000 matrix")]
	[InlineData("z = 1:1e9", "The range would have 1000000000 elements")]
	[InlineData("ones(5000, 1) * ones(1, 5001)", "5000×5001 matrix")]
	public void OversizedMatricesAreRefused(string code, string expectedMessage)
	{
		var error = host.Fail(code);
		Assert.Contains(expectedMessage, error.Message);
	}

	[Fact]
	public void UnexpectedExceptionsAreReportedAsErrorsAndTheInterpreterKeepsWorking()
	{
		host.Interpreter.RegisterFigureFormat("png", (_, _, _, _) => throw new InvalidOperationException("Simulated failure."));
		host.Interpreter.WorkingDirectory = Path.GetTempPath();
		var error = host.Fail("a = 1;\nsavefig(\"plot.png\")");
		Assert.Equal("Internal error (InvalidOperationException): Simulated failure.", error.Message);
		Assert.Equal(7, error.Span.Start);
		TestHost.AssertClose(1, host["a"]);
		TestHost.AssertClose(3, host.Eval("1 + 2"));
	}

	[Fact]
	public void DeepButAllowedNestingIsEvaluated()
	{
		string nested = new string('(', 250) + "1" + new string(')', 250);
		TestHost.AssertClose(1, host.Eval(nested));
		TestHost.AssertClose(251, host.Eval("1" + string.Concat(Enumerable.Repeat("+1", 250))));
		TestHost.AssertClose(0, host.Eval(string.Concat(Enumerable.Repeat("sin(", 250)) + "0" + new string(')', 250)));
	}

	[Fact]
	public void TooDeepNestingIsASyntaxError()
	{
		var result = host.Interpreter.Execute(new string('(', 10_000) + "1" + new string(')', 10_000));
		Assert.Contains("nested too deeply", Assert.Single(result.Parse.Diagnostics).Message);
	}

	[Fact]
	public void LargeMatricesAreSummarisedInsteadOfPrinted()
	{
		var value = ComplexMatrix.Ones(200, 200);
		Assert.Equal("A = [200×200 matrix, too large to show; show a part of it, e.g. A(1:5, 1:5)]", ResultFormatter.Format("A", value));
		Assert.Equal("[200×200 matrix, too large to show]", ResultFormatter.FormatValue(value));
		Assert.StartsWith("B =", ResultFormatter.Format("B", ComplexMatrix.Ones(100, 100)));
	}

	[Theory]
	[InlineData("savefig(\"plot.svg\", nan)", "the size must be between 50 and 16000")]
	[InlineData("savefig(\"plot.svg\", 800, nan)", "the size must be between 50 and 16000")]
	[InlineData("savefig(\"notes.txt:hidden.svg\")", "the file name must not contain ':'")]
	public void SavefigRejectsBadSizesAndAlternateDataStreams(string code, string expectedMessage)
	{
		host.Interpreter.WorkingDirectory = Path.GetTempPath();
		Assert.Contains(expectedMessage, host.Fail(code).Message);
	}
}

public class ScriptSafetyTests : IDisposable
{
	readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("mlcalc-safety");
	readonly TestHost host = new();

	public ScriptSafetyTests() => host.Interpreter.WorkingDirectory = directory.FullName;

	public void Dispose() => directory.Delete(recursive: true);

	[Fact]
	public void ScriptThatTypesItsOwnNameStopsAtTheNestingLimit()
	{
		File.WriteAllText(Path.Combine(directory.FullName, "self.m"), "self.m");
		var result = host.Interpreter.Execute("self.m");
		Assert.Equal("Scripts are nested too deeply (does a script run itself?).", result.Error?.Message);
	}

	[Fact]
	public void ScriptsRunningEachOtherStopAtTheNestingLimit()
	{
		File.WriteAllText(Path.Combine(directory.FullName, "a.m"), "run(\"b.m\")");
		File.WriteAllText(Path.Combine(directory.FullName, "b.m"), "run(\"a.m\")");
		var result = host.Interpreter.Execute("run(\"a.m\")");
		Assert.Contains("Scripts are nested too deeply", result.Error?.Message);
	}

	[Fact]
	public void ScriptsMayNestUpToTheLimit()
	{
		for (int i = 1; i < MLCalcInterpreter.MaxScriptDepth; i++)
			File.WriteAllText(Path.Combine(directory.FullName, $"s{i}.m"), $"run(\"s{i + 1}.m\")");
		File.WriteAllText(Path.Combine(directory.FullName, $"s{MLCalcInterpreter.MaxScriptDepth}.m"), "deepest = 1;");
		Assert.True(host.Interpreter.ExecuteFile("s1.m").Success);
		TestHost.AssertClose(1, host["deepest"]);
	}

	[Fact]
	public void UnreadableScriptIsReported()
	{
		// A folder cannot be read as a file.
		Directory.CreateDirectory(Path.Combine(directory.FullName, "folder.m"));
		var result = host.Interpreter.ExecuteFile("folder.m");
		Assert.StartsWith("Cannot read 'folder.m'", result.Error?.Message);
	}
}
