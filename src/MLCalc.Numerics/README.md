# MLCalc.Numerics

Numerical mathematics with complex numbers, from the [MLCalc](https://github.com/Ayern72/MLCalc)
calculator. The package contains three assemblies:

| Assembly | Contents |
|---|---|
| `MLCalc.Mathematics` | Real and complex scalar functions (`ComplexMath`, `RealMath`) with principal values, and the `MathException` error type |
| `MLCalc.LinearAlgebra` | `ComplexMatrix`, an immutable matrix of complex numbers, with products, powers, Gauss elimination (`LinearSolver`), determinants, inverses and polynomials (`Polynomial`) |
| `MLCalc.Numerics` | Integration and differentiation (`Calculus`), root finding, intersections and extrema (`RootFinding`) |

This is a pre-release, made as part of a demonstration project; the API may still change.

## Example

```csharp
using System.Numerics;
using MLCalc.LinearAlgebra;
using MLCalc.Mathematics;
using MLCalc.Numerics;

// Solve A·x = b.
var a = ComplexMatrix.FromRows(new double[,] { { 2, 1 }, { 1, 3 } });
var b = ComplexMatrix.ColumnVector(3, 5);
ComplexMatrix x = LinearSolver.Solve(a, b);        // [0.8; 1.4]

// Complex functions stay on the principal branch.
Complex w = ComplexMath.Asin(2);                    // 1.5708 - 1.3170i

// Numerical analysis on ordinary functions.
double area = Calculus.Integrate(t => Math.Sin(t), 0, Math.PI);     // 2
double root = RootFinding.FindRootNear(t => t * t - 2, 1);          // 1.41421356…

// Least-squares polynomial; coefficients from the constant term up.
var xs = ComplexMatrix.RowVector(0, 1, 2, 3);
var ys = ComplexMatrix.RowVector(1, 3, 7, 13);
ComplexMatrix p = Polynomial.Fit(xs, ys, 2);        // [1 1 1]: 1 + x + x²
```

Errors such as division by zero or a singular matrix are thrown as `MathException`, whose `Error`
property tells the kind of error.

## Limits and thread safety

A matrix may have at most `ComplexMatrix.MaxElements` (25 million) elements, which take 400 MB.
`ComplexMatrix.LimitElements(n)` lowers the limit on the current thread until the returned object
is disposed, for example while handling a request from a web user.

All types can be used from several threads at the same time: `ComplexMatrix` is immutable, and the
functions keep no state between calls. The element limit set by `LimitElements` belongs to the
thread that set it. Long operations (`LinearSolver`, `Polynomial.Fit`, matrix products and powers)
take a `CancellationToken`.

## Installing

The package is published to GitHub Packages. Add the registry as a package source; GitHub Packages
needs a personal access token with the `read:packages` scope, even for public packages:

```
dotnet nuget add source https://nuget.pkg.github.com/Ayern72/index.json --name mlcalc --username <github user> --password <token>
dotnet add package MLCalc.Numerics --prerelease
```

The source code, the user's manual and the license (MIT) are at
[github.com/Ayern72/MLCalc](https://github.com/Ayern72/MLCalc).
