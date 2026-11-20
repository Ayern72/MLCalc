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
repository is a modern C#/.NET re-implementation, with a WPF desktop application, a
cross-platform command-line tool, a web app and an Android app.

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

## Web app

MLCalc also runs in the browser: an ASP.NET Core server runs the programs, one workspace per
browser tab, and the browser shows the console and draws the plots, turning 3D windows locally.
They talk over SignalR with plain JSON messages, described in
[docs/web-protocol.md](docs/web-protocol.md), so that clients in any language can use the same
server. There are two clients, which look and behave the same:

- **Blazor WebAssembly** (C#, at `/`): draws the plots with the same C# code as the desktop app.
- **TypeScript** (at `/ts/`, no framework, about 30 KB compressed): written against the protocol
  document alone, with the drawing code ported from C#. Test cases shared by both languages
  ([tests/fixtures](tests/fixtures)) check that the two draw the same SVG to the character.

A link in each toolbar switches to the other client and keeps the workspace.

**Try it at https://mlcalc-764948534857.europe-north1.run.app** (the TypeScript client at
[/ts/](https://mlcalc-764948534857.europe-north1.run.app/ts/); the first visit after a quiet period
takes a few seconds while the server starts). To run it yourself:

```
docker compose up --build                       # then open http://localhost:8080
dotnet run --project src/MLCalc.Web.Server      # or without Docker: http://localhost:5080
```

Without Docker, the TypeScript client is served once it has been built (Node.js 26):
`npm ci` and `npm run build` in `src/MLCalc.Web.TypeScript`. Its
[README](src/MLCalc.Web.TypeScript/README.md) describes the project.

The server reads and writes no files: scripts are opened in the browser and sent as text,
`savefig` downloads SVG in the browser, and the container runs on a read-only file system. A
program may run for 60 seconds and use matrices of up to 2 million elements; these and the other
limits are set in `src/MLCalc.Web.Server/appsettings.json` or with environment variables such as
`MLCalc__RunSeconds` (see `compose.yaml`). The [manual](docs/manual.md#12-the-web-app) describes
the page and the differences from the desktop app.

The public site runs on Google Cloud Run and is deployed by the **Deploy** workflow, which is
started by hand and signs in to Google without stored keys. [docs/deploy-gcp.md](docs/deploy-gcp.md)
describes the setup.

## Android app

The Android app does what the desktop app does, with the web app's page: the same layout (the plots
above the console on a phone held upright, with a divider between them), the same touch gestures,
and the same Blazor components (`src/MLCalc.Web.Ui`), shown in a web view by .NET MAUI. The programs
run on the phone itself, so the app works offline and without the web app's limits, and it reads
and writes files in a scripts folder of its own:

- **Scripts** lists the folder, which starts with the Amiga samples; a script runs when you tap its
  name or type it, and scripts can `run` each other. **Open script…** copies a file from the phone
  (or Drive and the like) into the folder and runs it.
- `savefig("plot.png")` and `savefig("plot.svg")` save into the folder; **⋯ › Export plot** and
  **Share** send pictures and scripts to other apps.
- **▲ ▼** recall earlier input, as the arrow keys do on a computer.

The app is not on Google Play. CI builds an APK (`MLCalc-android` under **Artifacts**, see
[Downloads](#downloads)); to install it, open it on the phone and allow your browser or file manager
to install apps. Nothing in the app is specific to Android except a few lines in
`src/MLCalc.Maui/Platforms/Android`, so an iOS version needs mainly a Mac to build it on.

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
| `MLCalc.Web.Protocol` | The messages between the web server and its clients |
| `MLCalc.Web.Server` | ASP.NET Core server of the web app: sessions, limits, the SignalR hub; serves both clients |
| `MLCalc.Hosting` | Runs programs in a workspace and reports them as the web protocol's messages; used by the web server and the Android app |
| `MLCalc.Web.Ui` | The page (console, plots, toolbar) as Blazor components, shared by the web client and the Android app |
| `MLCalc.Web.Client` | Blazor WebAssembly client of the web app: the page, with programs running on the server |
| `MLCalc.Web.TypeScript` | TypeScript client of the web app (Vite; not part of the .NET solution) |
| `MLCalc.Maui` | Android app (.NET MAUI Blazor Hybrid): the page, with programs running on the phone (not part of `MLCalc.slnx`; see `MLCalc.App.slnx`) |

The libraries target `net10.0` with no Windows dependencies, so they can be reused by other
front-ends. Dependencies only point downward: `Mathematics` ← `LinearAlgebra` ← `Numerics` ←
`Interpreter` ← `Plotting` ← `Wpf` / `Cli`. `Language` has no dependencies and is used by
`Interpreter`. The plot commands are a module that a front-end adds to the interpreter
(`interpreter.AddPlotting(figure)`).

An interpreter is not thread-safe: each instance belongs to one thread at a time, such as the
worker thread that runs programs in the desktop app. A `Figure` can be rendered from another thread
while a program draws into it.

`samples/` contains scripts written for the original Amiga version.

## Downloads

Every CI run builds the applications. Open a run under **Actions › CI** and download them from
**Artifacts**: `MLCalc-desktop-win-x64` (the desktop app), `mlcalc-cli-win-x64` and
`mlcalc-cli-linux-x64` (the command-line tool). Each is a zip file with one self-contained program
that needs no .NET installation. On Linux, make it executable first: `chmod +x mlcalc`.
`MLCalc-android` holds the Android app, `MLCalc.apk`. GitHub keeps the files for 30 days, and
downloading them requires a GitHub account.

The APK from a push to `main` is signed with the repository's release key (the secrets
`ANDROID_KEYSTORE_BASE64` and `ANDROID_KEYSTORE_PASSWORD`, key alias `mlcalc`), so that each one
installs as an update of the one before. Other runs sign with a throwaway key; Android then asks
for the installed app to be removed first. The same goes for an app installed from Visual Studio
or `dotnet build -t:Run`, which signs with a key of its own.

## Building

Requires the .NET 10 SDK.

```
dotnet build MLCalc.slnx
dotnet test MLCalc.slnx
dotnet run --project src/MLCalc.Wpf
dotnet run --project src/MLCalc.Cli -- -e "1 + 2"
```

The TypeScript client needs Node.js 26:

```
cd src/MLCalc.Web.TypeScript
npm ci
npm run check    # type check
npm test         # its tests, including the drawings shared with C#
npm run build    # into src/MLCalc.Web.Server/ts-client, which the server serves at /ts/
npm run dev      # with hot reload at http://localhost:5173/ts/; start the server too
```

The Android app needs the MAUI workload (`dotnet workload install maui-android`, run in the
repository's folder so that it goes into the SDK that `global.json` picks), JDK 21 and the Android
SDK; set `JAVA_HOME` and `ANDROID_HOME`. With a phone connected over USB, with USB debugging on:

```
dotnet build src/MLCalc.Maui -t:Run -f net10.0-android          # Debug: quick to build, slower to run
dotnet build src/MLCalc.Maui -t:Run -f net10.0-android -c Release
```

A Debug build runs .NET code in an interpreter, so turning 3D plots is noticeably slower than in a
Release build. In both, `chrome://inspect` on the computer shows the app's page while the phone is
connected (in Release only when built with `-p:WebViewDebugging=true`).

The shared test cases in `tests/fixtures` are written by the C# tests. After a deliberate change
to the drawing, run `dotnet test` with the environment variable `MLCALC_UPDATE_FIXTURES=1` to write
them anew, and review the diff.

Dependencies are kept up to date by Dependabot ([.github/dependabot.yml](.github/dependabot.yml)):
once a week it opens pull requests for the NuGet and npm packages, the GitHub Actions and the
Docker base images, with the minor and patch updates of each kind grouped together, and CI builds
and tests each one. Security updates come at once. New major versions of .NET and Node.js are left
out, as they mean changing the target framework or the toolchain by hand.

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
