# MLCalc

> **Disclaimer:** MLCalc is a personal project, provided "as is" and without any warranty. Like
> all software, it may contain bugs, so its results can be wrong. Do not use MLCalc where an
> incorrect result is not acceptable, for example in medical, safety-critical, engineering,
> financial or legal work. If a result matters, check it with another method or tool. The full
> terms are in the [MIT license](LICENSE).

MLCalc is a scientific calculator and plotting program with its own small scripting language.
It began in high school as a simple graph-drawing program for the Amiga, written in C. When its
author started studying computer science, the courses used MATLAB, which was not available for the
Amiga, so MLCalc grew matrices and a similar syntax to make the same work possible at home. This
repository is a modern C#/.NET re-implementation, with a WPF desktop application and a
cross-platform command-line tool.

Every value in MLCalc is a complex matrix: scalars are 1×1 matrices and real numbers are complex
numbers with a zero imaginary part.

![MLCalc running interactively](docs/screenshot.png)

## The language

```
A = [1, 2; 3, 4]           % matrices: ',' or space between elements, ';' or newline between rows
A * [1; 1]                  % matrix product; .* ./ .^ work element by element
z = (1 + 2i)^2              % complex numbers
t = 0:0.1:2*pi;             % ranges; ';' suppresses output
x = A(2, :)                 % indexing is 1-based; ':' selects a whole row or column

for k = 1:5                 % block style: if … elseif … else … end, while … end, for … end
	if mod(k, 2) == 0
		disp("even")
	else
		disp(k)
	end
end

k = 0;
while k < 3 do              % the original style: if … then … else, while … do, for k = 1 to 10 do,
begin                       % with a single statement or begin … end as the body
	k = k + 1
end

xrange(-6.3, 6.3); yrange(-6, 6); grid
fplot("x", "x*sin(x)")      % functions are given as strings
plot(t, sin(t), +)          % styles: - (line), + (crosses), * (stars)
p = polyfit(t, cos(t), 4); polyplot(p)
fplot3d("x", "y", "sin(x)*cos(y)")   % 3D: plot3d, dot3d, zrange, view, camposition, camtarget
solve("x", "x^2 - 2", 1)    % also integral, derivata, isect, extreme, feval
```

Names are case-insensitive. **Help › List Functions** shows every function and command.
The [user's manual](docs/manual.md) describes every operator, function and command, and the full
syntax is described in EBNF in [docs/grammar.md](docs/grammar.md).

## Using the app

- Type an expression and press **Enter**. Unfinished input (an open `begin`, bracket or
  `while … do`) continues on the next line with a `...` prompt; an empty line forces evaluation.
- **Up/Down** recall earlier input, **Esc** stops a running script (or discards unfinished input).
- Run a script by typing its file name (e.g. `FEIGENBA.m`), with **File › Run Script** (Ctrl+O), by
  dropping a `.m` file on the window, or from the command line: `MLCalc --run script.m`.
- The original sample scripts are copied next to the executable, in `samples/`.
- `initdisplay(rows, columns)` splits the plot area; `currentwin(r, c)` selects a window.
- **File › Export Plot** (Ctrl+E) saves the plots as PNG (at twice the screen resolution) or SVG;
  **Edit › Copy Plot** (Ctrl+Shift+C) copies them as an image. In scripts, `savefig("plot.png")` or
  `savefig("plot.svg", width, height)` does the same (the command-line tool supports SVG only).

## Command line

`mlcalc` runs on Windows, Linux and macOS. Without arguments it starts an interactive prompt
(`help` for hints, Ctrl+C stops a running program, `exit` quits).

```
mlcalc script.m [more.m …]          run scripts; errors are reported as file:line:column
mlcalc -e "A = [1 2; 3 4]" -e "inv(A)"
echo "solve(\"x\", \"x^2-2\", 1)" | mlcalc
mlcalc FEIGENBA.m --svg feigenbaum.svg --size 1200x800
mlcalc script.m -i                   run a script, then continue interactively
```

The exit code is 0 on success, 1 when a program fails and 2 for invalid options, so `mlcalc` can be
used from shell scripts and builds.

## Differences from the Amiga original

- **Matrix operators.** `*`, `/`, `\` and `^` are matrix operations and `.*`, `./`, `.^`
  are element-wise. The original used `*` and `/` element-wise and `.*` for the matrix product.
  `'` is the conjugate transpose and `.'` the plain transpose.
- Several numerical routines were replaced with more robust methods (Brent's method for roots,
  Ridders' method for derivatives) and bugs in some complex inverse functions were fixed.

## Projects

| Project | Purpose |
|---|---|
| `MLCalc.Mathematics` | Real and complex scalar functions |
| `MLCalc.LinearAlgebra` | Complex matrix type and linear algebra (Gauss elimination, least squares, polynomials) |
| `MLCalc.Numerics` | Numerical analysis: integration, differentiation, root finding |
| `MLCalc.Language` | Lexer, parser and syntax tree for the MLCalc language |
| `MLCalc.Interpreter` | Evaluates MLCalc expressions and scripts; hosts can add functions and commands and decide whether programs may read script files |
| `MLCalc.Plotting` | The plot commands, and a UI-independent plot model (figures, 2D and 3D windows, layout, camera) with SVG export |
| `MLCalc.Wpf` | Windows desktop application |
| `MLCalc.Cli` | Cross-platform command-line tool (`mlcalc`) |

The libraries target `net10.0` with no Windows dependencies, so they can be reused by other
front-ends. Dependencies only point downward: `Mathematics` ← `LinearAlgebra` ← `Numerics` ←
`Interpreter` ← `Plotting` ← `Wpf` / `Cli`. `Language` has no dependencies and is used by
`Interpreter`. The plot commands are a module that a front-end adds to the interpreter
(`interpreter.AddPlotting(figure)`).

An interpreter is not thread-safe: each instance belongs to one thread at a time, such as the
worker thread that runs programs in the desktop app. A `Figure` can be rendered from another thread
while a program draws into it.

`samples/` contains scripts written for the original Amiga version.

## Building

Requires the .NET 10 SDK.

```
dotnet build MLCalc.slnx
dotnet test MLCalc.slnx
dotnet run --project src/MLCalc.Wpf
dotnet run --project src/MLCalc.Cli -- -e "1 + 2"
```

Two single-file tools live outside the solution:

```
dotnet run benchmarks/FunctionEvaluation.cs            # times function evaluation (see benchmarks/README.md)
dotnet run tools/build-manual-page/BuildManualPage.cs  # builds the manual's web page into artifacts/
```

## Packages

The six libraries are published as three NuGet packages. Each package has its own README, shown
on its page in GitHub Packages:

| Package | Projects | README |
|---|---|---|
| `MLCalc.Numerics` | Mathematics, LinearAlgebra, Numerics | [src/MLCalc.Numerics/README.md](src/MLCalc.Numerics/README.md) |
| `MLCalc.Scripting` | Interpreter, Language | [src/MLCalc.Interpreter/README.md](src/MLCalc.Interpreter/README.md) |
| `MLCalc.Plotting` | Plotting | [src/MLCalc.Plotting/README.md](src/MLCalc.Plotting/README.md) |

`Directory.Build.targets` explains how one package carries several projects. The **Packages**
workflow, started by hand from the Actions tab, runs the tests and publishes the packages to GitHub
Packages with a pre-release version such as `0.1.0-preview.7` (the version in
`Directory.Build.props` plus the run number). To build them locally:

```
dotnet pack MLCalc.slnx -c Release --version-suffix preview.1 -o packages
```

To use them from another project, add the registry as a package source. GitHub Packages needs a
personal access token with the `read:packages` scope, even for public packages:

```
dotnet nuget add source https://nuget.pkg.github.com/Ayern72/index.json --name mlcalc --username <github user> --password <token>
```

## License

MIT — see [LICENSE](LICENSE).
