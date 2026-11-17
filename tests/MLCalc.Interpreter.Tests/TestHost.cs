using System.Numerics;
using MLCalc.LinearAlgebra;
using MLCalc.Plotting;

namespace MLCalc.Interpreter.Tests;

/// <summary>Records everything the interpreter writes.</summary>
sealed class RecordingOutput : IConsoleOutput
{
	public List<(string Name, ComplexMatrix Value)> Values { get; } = [];
	public List<string> Lines { get; } = [];
	public int Clears { get; private set; }

	public void WriteValue(string name, ComplexMatrix value) => Values.Add((name, value));
	public void WriteLine(string text) => Lines.Add(text);
	public void Clear() => Clears++;
}

sealed class TestHost
{
	public Figure Figure { get; } = new();
	public RecordingOutput Output { get; } = new();
	public MLCalcInterpreter Interpreter { get; }

	public PlotCommands Plotting { get; }

	public TestHost()
	{
		Interpreter = new MLCalcInterpreter(Output, new InterpreterOptions { RandomSeed = 1, Scripts = FileScriptSource.Instance });
		Plotting = Interpreter.AddPlotting(Figure, new PlottingOptions { WriteSvgFiles = true });
	}

	/// <summary>Runs source that must succeed.</summary>
	public TestHost Run(string source)
	{
		var result = Interpreter.Execute(source);
		Assert.True(result.Success, string.Join("\n", result.Messages()));
		return this;
	}

	/// <summary>Runs source that must fail at runtime, returning the error.</summary>
	public RuntimeError Fail(string source)
	{
		var result = Interpreter.Execute(source);
		Assert.False(result.Parse.HasErrors, string.Join("\n", result.Messages()));
		return Assert.IsType<RuntimeError>(result.Error);
	}

	/// <summary>
	/// Runs <paramref name="source"/>, cancels it after <paramref name="delayMilliseconds"/>, and asserts
	/// that it stopped as cancelled within <paramref name="limitMilliseconds"/> of the cancellation.
	/// </summary>
	/// <remarks>
	/// The cancellation comes from a thread of its own and the time is measured from it. A
	/// <c>CancellationTokenSource</c> timeout fires on the thread pool, which other tests running in
	/// parallel can keep busy for seconds on a small CI machine; that delay says nothing about how
	/// quickly the interpreter stops.
	/// </remarks>
	public ExecutionResult AssertCancelsPromptly(string source, int delayMilliseconds = 100, int limitMilliseconds = 3000)
	{
		using var cts = new CancellationTokenSource();
		var cancelledAt = new System.Diagnostics.Stopwatch();
		using var done = new ManualResetEventSlim();
		var canceller = new Thread(() =>
		{
			if (!done.Wait(delayMilliseconds))
			{
				cancelledAt.Start();
				cts.Cancel();
			}
		}) { IsBackground = true };
		canceller.Start();
		var result = Interpreter.Execute(source, cts.Token);
		long elapsed = cancelledAt.ElapsedMilliseconds;
		done.Set();
		canceller.Join();
		Assert.True(result.Cancelled, $"The program finished before it was cancelled: {string.Join("\n", result.Messages())}");
		Assert.True(elapsed <= limitMilliseconds, $"It stopped {elapsed} ms after the cancellation.");
		return result;
	}

	public ComplexMatrix this[string name] =>
		Interpreter.TryGetVariable(name, out var value) ? value : throw new KeyNotFoundException(name);

	/// <summary>Evaluates an expression and returns ans.</summary>
	public ComplexMatrix Eval(string expression) => Run(expression + ";")["ans"];

	public static void AssertClose(double expected, ComplexMatrix actual, double tolerance = 1e-9)
	{
		Assert.True(actual.IsScalar, $"Expected a scalar, got {actual.Rows}×{actual.Columns}");
		Assert.True(Complex.Abs(actual.ScalarValue - expected) <= tolerance, $"Expected {expected}, got {actual}");
	}

	public static void AssertMatrix(double[,] expected, ComplexMatrix actual, double tolerance = 1e-9) =>
		Assert.True(ComplexMatrix.FromRows(expected).ApproximatelyEquals(actual, tolerance), $"Got{Environment.NewLine}{actual}");
}
