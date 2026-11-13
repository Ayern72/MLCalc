# MLCalc

MLCalc is a scientific calculator and plotting program with its own small scripting language.
It was first written in C for the Amiga in the mid-1990s. This repository is a modern C#/.NET
re-implementation, starting with a WPF desktop front-end.

Every value in MLCalc is a complex matrix: scalars are 1×1 matrices and real numbers are complex
numbers with a zero imaginary part.

```
> [1, 2; 3, 4] * [1; 1]
> sin(PI/2)
> fplot("x", "x^2 - 2")
```

## Projects

| Project | Purpose |
|---|---|
| `MLCalc.Math` | Real and complex scalar functions |
| `MLCalc.LinearAlgebra` | Complex matrix type and linear algebra (Gauss elimination, least squares, polynomials) |
| `MLCalc.Numerics` | Numerical analysis: integration, differentiation, root finding |
| `MLCalc.Language` | Lexer, parser and syntax tree for the MLCalc language |
| `MLCalc.Plotting` | UI-independent plot model (figures, axes, series, coordinate transforms) |
| `MLCalc.Interpreter` | Evaluates MLCalc expressions and scripts |
| `MLCalc.Wpf` | Windows desktop application |

The libraries target `net10.0` with no Windows dependencies, so they can be reused by other
front-ends. Dependencies only point downward:
`Math` ← `LinearAlgebra` ← `Numerics` / `Plotting` ← `Interpreter` ← `Wpf`, and `Language` ← `Interpreter`.

`samples/` contains scripts written for the original Amiga version.

## Building

Requires the .NET 10 SDK.

```
dotnet build MLCalc.slnx
dotnet test MLCalc.slnx
dotnet run --project src/MLCalc.Wpf
```

## License

MIT — see [LICENSE](LICENSE).
