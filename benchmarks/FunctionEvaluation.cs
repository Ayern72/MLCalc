#:project ../src/MLCalc.Interpreter/MLCalc.Interpreter.csproj
#:property Configuration=Release
// Measures how fast functions given as text (fplot, integral, …) are evaluated.
// Run from the repository root:  dotnet run benchmarks/FunctionEvaluation.cs
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using MLCalc.Interpreter;
using MLCalc.LinearAlgebra;
using MLCalc.Plotting;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var interpreter = new MLCalcInterpreter(new Figure(), new NullOutput(), randomSeed: 1);
Run("a = 1.5; b = 0.25;");

// An expression whose constant parts (sin(pi/4)*a, cos(pi/3)*exp(-b), sqrt(a^2 + b^2)) can be
// computed once, and one with nothing to fold, to separate the two optimisations.
const string WithConstants = "sin(pi/4)*a*x^2 + cos(pi/3)*exp(-b)*x + sqrt(a^2 + b^2)";
const string NothingToFold = "x*sin(x) + x^2/(1 + x^2)";

(string Name, string Code, int Repeats)[] workloads =
[
	("fplot, constant parts (50 x 1000 points)", $"clear; fplot(\"x\", \"{WithConstants}\")", 50),
	("integral, constant parts (5 runs)", $"q = integral(\"x\", \"({WithConstants})*sin(7*x)\", 0, 40);", 5),
	("fplot, nothing to fold (50 x 1000 points)", $"clear; fplot(\"x\", \"{NothingToFold}\")", 50),
];

const int Rounds = 7;
Console.WriteLine($".NET {Environment.Version}, {RuntimeInformation.OSDescription}, {Environment.ProcessorCount} logical processors");
Console.WriteLine($"Median and fastest of {Rounds} rounds, after warm-up.");
Console.WriteLine();
Console.WriteLine($"{"Workload",-44} {"median",10} {"fastest",10}");
foreach (var (name, code, repeats) in workloads)
{
	for (int i = 0; i < 3; i++)
		Run(code); // warm-up: lets the JIT compile everything first
	var times = new List<double>();
	for (int round = 0; round < Rounds; round++)
	{
		var watch = Stopwatch.StartNew();
		for (int i = 0; i < repeats; i++)
			Run(code);
		times.Add(watch.Elapsed.TotalMilliseconds);
	}
	times.Sort();
	Console.WriteLine($"{name,-44} {times[Rounds / 2],8:F1}ms {times[0],8:F1}ms");
}

// The result must not change between versions; a different value means the optimisations broke something.
interpreter.TryGetVariable("q", out var q);
Console.WriteLine();
Console.WriteLine($"Check: integral = {q} (expected 225.2453728)");

void Run(string code)
{
	var result = interpreter.Execute(code);
	if (!result.Success)
		throw new InvalidOperationException(string.Join("; ", result.Messages()));
}

sealed class NullOutput : IConsoleOutput
{
	public void WriteValue(string name, ComplexMatrix value) { }
	public void WriteLine(string text) { }
	public void Clear() { }
}
