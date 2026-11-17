# MLCalc.Scripting

An embeddable interpreter for the language of the [MLCalc](https://github.com/Ayern72/MLCalc)
calculator. Every value is a complex matrix; the language has variables, `if`/`while`/`for`,
ranges, indexing, about a hundred built-in functions, and numerical analysis on functions given
as text (`integral("x", "x^2", 0, 1)`). Hosts can add their own functions and commands.

The package contains the `MLCalc.Interpreter` and `MLCalc.Language` (lexer, parser and syntax tree)
assemblies, and depends on [MLCalc.Numerics](https://github.com/Ayern72/MLCalc). The plot commands
come with the `MLCalc.Plotting` package.

This is a pre-release, made as part of a demonstration project; the API may still change.

## Example

```csharp
using MLCalc.Interpreter;
using MLCalc.LinearAlgebra;

var interpreter = new MLCalcInterpreter(new ConsoleOutput());

// A function of the host, callable from programs.
interpreter.AddFunction("hypot", call =>
{
	call.RequireCount(2);
	return Math.Sqrt(call.Real(0) * call.Real(0) + call.Real(1) * call.Real(1));
}, isPure: true);

var result = interpreter.Execute("""
	A = [2 1; 1 3];
	x = A \ [3; 5]
	h = hypot(3, 4)
	q = integral("t", "hypot(t, 1)", 0, 1);
	""");
if (!result.Success)
	Console.WriteLine(string.Join(Environment.NewLine, result.Messages()));

interpreter.TryGetVariable("q", out ComplexMatrix q);

sealed class ConsoleOutput : IConsoleOutput
{
	public void WriteValue(string name, ComplexMatrix value) => Console.WriteLine(ResultFormatter.Format(name, value));
	public void WriteLine(string text) => Console.WriteLine(text);
	public void Clear() { }
}
```

An added function reads its arguments through the `CallContext` (`Real`, `Matrix`, `String`, …),
which reports a wrong argument as an error in the program, pointing at that argument. A function
marked `isPure` depends only on its arguments; commands added with `AddCommand` give no value.

## Options

`InterpreterOptions` decides what programs may do:

| Option | Default | Meaning |
|---|---|---|
| `Scripts` | none | Where `.m` script files come from (`ExecuteFile`, `run("file.m")`, a line that is a file name). Without it, programs cannot read files. `FileScriptSource.Instance` reads them from disk; a host can supply its own `IScriptSource` |
| `MaxMatrixElements` | 25 000 000 | The largest matrix or range while a program runs |
| `MaxScriptDepth` | 32 | How deeply scripts may run other scripts |
| `RandomSeed` | none | A fixed seed for `rand` |

`Execute` takes a `CancellationToken`; long loops and matrix operations stop soon after it is
cancelled, and the result then has `Cancelled` set.

## Thread safety

An `MLCalcInterpreter` is not thread-safe: it holds the variables and the state of the running
program, so use each instance from one thread at a time, for example one per user session.
Separate instances share nothing and can run at the same time on different threads. A program
runs on the thread that calls `Execute`; to keep a user interface responsive, call it from a
background thread and cancel it through the token.

## Installing

The package is published to GitHub Packages. Add the registry as a package source; GitHub Packages
needs a personal access token with the `read:packages` scope, even for public packages:

```
dotnet nuget add source https://nuget.pkg.github.com/Ayern72/index.json --name mlcalc --username <github user> --password <token>
dotnet add package MLCalc.Scripting --prerelease
```

The language is described in the
[user's manual](https://github.com/Ayern72/MLCalc/blob/main/docs/manual.md). The source code and
the license (MIT) are at [github.com/Ayern72/MLCalc](https://github.com/Ayern72/MLCalc).
