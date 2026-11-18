using MLCalc.LinearAlgebra;
using MLCalc.Plotting;

namespace MLCalc.Interpreter.Tests;

/// <summary>What a host can do: add functions and commands, turn script files off, set limits.</summary>
public class HostApiTests
{
	static MLCalcInterpreter Bare(InterpreterOptions? options = null) => new(new RecordingOutput(), options);

	[Fact]
	public void AnAddedFunctionCanBeCalledAndUsedInFunctionsGivenAsText()
	{
		var interpreter = Bare();
		interpreter.AddFunction("twice", call =>
		{
			call.RequireCount(1);
			return call.Matrix(0) * 2;
		}, isPure: true);
		Assert.True(interpreter.Execute("y = twice([1 2]) + 1; q = integral(\"x\", \"twice(x)\", 0, 1);").Success);
		Assert.True(interpreter.TryGetVariable("y", out var y) && y.Equals(ComplexMatrix.RowVector(3, 5)));
		Assert.True(interpreter.TryGetVariable("q", out var q) && Math.Abs(q.ScalarValue.Real - 1) < 1e-12);
		Assert.Contains("twice", interpreter.BuiltinNames);
	}

	[Fact]
	public void ErrorsFromAnAddedFunctionPointAtTheArgument()
	{
		var interpreter = Bare();
		interpreter.AddFunction("half", call => call.Real(0) / 2);
		var result = interpreter.Execute("half([1 2])");
		Assert.Equal("half: argument 1 must be a scalar.", result.Error?.Message);
		Assert.Equal(5, result.Error!.Span.Start);
	}

	[Fact]
	public void AnAddedCommandGivesNoValueAndIsNotAllowedInFunctionsGivenAsText()
	{
		var output = new RecordingOutput();
		var interpreter = new MLCalcInterpreter(output);
		interpreter.AddCommand("hello", call => call.Output.WriteLine("Hello, " + call.String(0)));
		Assert.True(interpreter.Execute("hello(\"world\")").Success);
		Assert.Equal(["Hello, world"], output.Lines);
		Assert.False(interpreter.TryGetVariable("ans", out _));
		Assert.Contains("hello", interpreter.Execute("integral(\"x\", \"hello(x)\", 0, 1)").Error?.Message);
	}

	[Theory]
	[InlineData("")]
	[InlineData("2x")]
	[InlineData("my fun")]
	[InlineData("while")]
	[InlineData("sin")]   // already a function
	[InlineData("SIN")]   // names are case-insensitive
	public void NamesMustBeNewAndValid(string name)
	{
		var interpreter = Bare();
		Assert.Throws<ArgumentException>(() => interpreter.AddFunction(name, _ => 1));
		Assert.Throws<ArgumentException>(() => interpreter.AddCommand(name, _ => { }));
	}

	[Fact]
	public void WithoutScriptFilesNoFileIsRead()
	{
		var directory = Directory.CreateTempSubdirectory("mlcalc-test-");
		try
		{
			File.WriteAllText(Path.Combine(directory.FullName, "a.m"), "x = 1;");
			var interpreter = Bare();
			interpreter.WorkingDirectory = directory.FullName;
			Assert.Equal("Script files cannot be run here.", interpreter.ExecuteFile("a.m").Error?.Message);
			Assert.Equal("Script files cannot be run here.", interpreter.Execute("a.m").Error?.Message);
			Assert.Equal("Script files cannot be run here.", interpreter.Execute("run(\"a.m\")").Error?.Message);
			Assert.False(interpreter.TryGetVariable("x", out _));
			// The text of a script can still be run.
			Assert.True(interpreter.Execute(File.ReadAllText(Path.Combine(directory.FullName, "a.m"))).Success);
		}
		finally
		{
			directory.Delete(recursive: true);
		}
	}

	[Fact]
	public void ScriptsComeFromTheScriptSource()
	{
		var interpreter = Bare(new InterpreterOptions { Scripts = new MemoryScripts() });
		interpreter.WorkingDirectory = MemoryScripts.Folder;
		Assert.True(interpreter.Execute("main.m").Success);
		Assert.True(interpreter.TryGetVariable("y", out var y) && y.ScalarValue == 4);
		Assert.Equal("Cannot find the script 'none.m'.", interpreter.ExecuteFile("none.m").Error?.Message);
	}

	sealed class MemoryScripts : IScriptSource
	{
		public static readonly string Folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "memory"));

		public string? Read(string fullPath) => Path.GetFileName(fullPath) switch
		{
			"main.m" when Path.GetDirectoryName(fullPath) == Folder => "x = 2; run(\"square.m\")",
			"square.m" => "y = x^2;",
			_ => null,
		};
	}

	[Fact]
	public void TheMatrixSizeCanBeLimited()
	{
		var interpreter = Bare(new InterpreterOptions { MaxMatrixElements = 100 });
		Assert.True(interpreter.Execute("a = ones(10);").Success);
		Assert.Equal("A 11×11 matrix would have 121 elements; the limit is 100.", interpreter.Execute("b = ones(11)").Error?.Message);
		Assert.Equal("The range would have 101 elements; the limit is 100.", interpreter.Execute("c = 0:100").Error?.Message);
		// The limit applies only while the program runs.
		Assert.Equal(ComplexMatrix.MaxElements, ComplexMatrix.ElementLimit);
		Assert.Throws<ArgumentOutOfRangeException>(() => new InterpreterOptions { MaxMatrixElements = ComplexMatrix.MaxElements + 1 });
	}

	[Fact]
	public void TheScriptDepthCanBeLimited()
	{
		var interpreter = Bare(new InterpreterOptions { Scripts = new MemoryScripts(), MaxScriptDepth = 1 });
		interpreter.WorkingDirectory = MemoryScripts.Folder;
		Assert.Contains("nested too deeply", interpreter.Execute("main.m").Error?.Message);
	}

	[Fact]
	public void WithoutPlottingThePlotCommandsAreUnknown()
	{
		Assert.Equal("Unknown function or variable 'plot'.", Bare().Execute("plot([1 2])").Error?.Message);
	}

	[Fact]
	public void SavingFiguresMustBeAllowed()
	{
		var interpreter = Bare();
		var plotting = interpreter.AddPlotting(new Figure());
		Assert.Equal("savefig: saving figures is not available here.", interpreter.Execute("savefig(\"plot.svg\")").Error?.Message);

		var saved = new List<string>();
		plotting.RegisterFigureFormat("png", (_, path, _, _) => { saved.Add(Path.GetFileName(path)); return null; });
		Assert.Equal("savefig: unsupported file type '.svg'; use .png.", interpreter.Execute("savefig(\"plot.svg\")").Error?.Message);
		Assert.True(interpreter.Execute("savefig(\"plot.png\")").Success);
		Assert.Equal(["plot.png"], saved);
	}

	[Fact]
	public void ThePlotLimitsCanBeSet()
	{
		var interpreter = Bare();
		interpreter.AddPlotting(new Figure(), new PlottingOptions { MaxSurfaceGridLines = 10, MaxCurvePoints = 50 });
		Assert.Equal("fplot3d: the number of grid lines must be between 2 and 10.",
			interpreter.Execute("fplot3d(\"x\", \"y\", \"x*y\", 11)").Error?.Message);
		Assert.Equal("fplot3d: the number of points must be between 2 and 50.",
			interpreter.Execute("fplot3d(\"t\", \"t\", \"t\", \"t\", 0, 1, 51)").Error?.Message);
		Assert.True(interpreter.Execute("fplot3d(\"x\", \"y\", \"x*y\", 10)").Success);
	}

	[Fact]
	public void PlottingCanOnlyBeAddedOnce()
	{
		var interpreter = Bare();
		interpreter.AddPlotting(new Figure());
		Assert.Throws<ArgumentException>(() => interpreter.AddPlotting(new Figure()));
	}
}
