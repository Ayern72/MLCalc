# MLCalc.Plotting

Plots for the [MLCalc](https://github.com/Ayern72/MLCalc) interpreter: the plot commands of the
language (`plot`, `fplot`, `cplot`, `plot3d`, `fplot3d`, `xrange`, `view`, `savefig` and more), and
the plot model they draw into. The model has no user interface of its own: a `Figure` is a grid of
up to 3×3 plot windows, 2D or 3D, that a front-end renders. `SvgWriter` turns a figure into SVG,
`PlotLayout` and `PlotTransform` place 2D windows on the screen, and `Scene3D` turns a 3D window
into lines, polygons and text for any renderer.

The package depends on MLCalc.Scripting and MLCalc.Numerics. This is a pre-release, made as part
of a demonstration project; the API may still change.

## Example

```csharp
using MLCalc.Interpreter;
using MLCalc.Plotting;

var figure = new Figure();
var interpreter = new MLCalcInterpreter(output);   // output: your IConsoleOutput
interpreter.AddPlotting(figure);

interpreter.Execute("""
	initdisplay(1, 2)
	xrange(-6.3, 6.3); grid
	fplot("x", "x*sin(x)")
	currentwin(1, 2)
	fplot3d("x", "y", "sin(x)*cos(y)")
	""");

File.WriteAllText("plots.svg", SvgWriter.Render(figure.Snapshot(), 1200, 500));
```

## Options and files

`AddPlotting` takes `PlottingOptions`:

| Option | Default | Meaning |
|---|---|---|
| `WriteSvgFiles` | off | Whether `savefig("plot.svg")` writes files, relative to the interpreter's `WorkingDirectory` |
| `MaxSurfaceGridLines` | 200 | The most grid lines each way in an `fplot3d` surface |
| `MaxCurvePoints` | 100 000 | The most points in an `fplot3d` curve |

`AddPlotting` returns the `PlotCommands`, whose `RegisterFigureFormat` adds other formats to
`savefig`, for example PNG, or a format that hands the image to a web browser instead of writing a
file. Without `WriteSvgFiles` and without registered formats, `savefig` reports that saving is not
available. The largest number of points in one window is set with `new Figure(maxPointsPerWindow)`.

## Thread safety

The plot commands run on the interpreter's thread and change the figure there. A front-end may
render at the same time on another thread: `Figure.Snapshot()` returns an immutable copy of the
whole figure, and `Figure.Changed` is raised after every change, on the thread that made it, so a
user interface should move the work to its own thread. Snapshots, `SvgWriter`, `Scene3D` and the
other rendering helpers can be used from any thread.

## Installing

The package is published to GitHub Packages. Add the registry as a package source; GitHub Packages
needs a personal access token with the `read:packages` scope, even for public packages:

```
dotnet nuget add source https://nuget.pkg.github.com/Ayern72/index.json --name mlcalc --username <github user> --password <token>
dotnet add package MLCalc.Plotting --prerelease
```

The plot commands are described in the
[user's manual](https://github.com/Ayern72/MLCalc/blob/main/docs/manual.md#8-plotting). The source
code and the license (MIT) are at [github.com/Ayern72/MLCalc](https://github.com/Ayern72/MLCalc).
