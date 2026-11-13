# Benchmarks

## FunctionEvaluation.cs

Measures how fast functions given as text are evaluated — the expressions in `fplot`, `integral`,
`solve` and similar commands, which are evaluated at many points. Run it from the repository root:

```
dotnet run benchmarks/FunctionEvaluation.cs
```

It is a single-file .NET program (requires the .NET 10 SDK) that uses the interpreter directly. It
times three workloads and prints the median and fastest of seven rounds, after a warm-up:

| Workload | What it shows |
|---|---|
| `fplot` with constant parts | an expression with parts that do not depend on `x` (`sin(pi/4)*a` and so on) |
| `integral` with constant parts | the same expression, evaluated many more times by Simpson's rule |
| `fplot` with nothing to fold | an expression where every part depends on `x` |

The last line checks that the integral still has its expected value, so a speed-up that changes
results is noticed.

## Results so far

Windows 11, .NET 10, 16 logical processors; median times. Run the benchmark on your own machine
to compare versions — absolute times vary between machines.

| Workload | Tree walking | Constant folding | Folding + compiled delegates |
|---|---|---|---|
| `fplot`, constant parts (50 × 1000 points) | 218 ms | 76 ms | 30 ms |
| `integral`, constant parts (5 runs) | 800 ms | 301 ms | 142 ms |
| `fplot`, nothing to fold (50 × 1000 points) | 93 ms | 88 ms | 41 ms |

- **Tree walking:** the expression's syntax tree is evaluated from scratch at every point.
- **Constant folding** (`ConstantFolder`): branches that do not depend on the variable are
  evaluated once and replaced by their value.
- **Compiled delegates** (`FunctionCompiler`): the folded tree is turned into .NET closures once,
  so evaluating it no longer walks the tree or looks up names.
