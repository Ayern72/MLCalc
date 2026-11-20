# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A C#/.NET 10 re-implementation of MLCalc, a calculator and plotting program with its own scripting
language, first written in C for the Amiga in the 1990s. It is a demonstration project, not a
product. See README.md for the projects, docs/manual.md for the language and docs/grammar.md for
the syntax.

## Conventions

- **Indentation:** one tab per indentation step, never spaces, in all source files (C#, XAML,
  Razor, project files, JSON, CSS, JavaScript, TypeScript). YAML does not allow tabs, so the
  workflows and `compose.yaml` use two spaces. In Markdown, list continuations use spaces.
- **Line endings:** end lines with **CRLF** (`\r\n`), not LF, in files you create or edit.
- **Libraries without Windows dependencies:** everything in `src/` except `MLCalc.Wpf` targets
  plain `net10.0` and must stay usable on Linux and in the browser.
- **Documentation:** the libraries in `src/` must have XML comments on every public member
  (CS1591 is an error there); the applications and tests are exempt (see `Directory.Build.props`).
- **Warnings are errors** in every project.
- **MATLAB:** describe MLCalc on its own terms; mention MATLAB only as the historical inspiration.
- **TypeScript client** (`src/MLCalc.Web.TypeScript`): strict TypeScript, no UI framework; the only
  runtime dependency is `@microsoft/signalr`, and packages are pinned to exact versions. Its
  `src/plotting` is a line-by-line port of `src/MLCalc.Plotting`: change both together, keeping the
  arithmetic in the same order, then rewrite the shared test cases (below) and check that both
  pass.
- **One page, three hosts:** `src/MLCalc.Web.Ui` is the page of the Blazor web client and of the
  Android app (`src/MLCalc.Maui`); the TypeScript client copies its look and behaviour. A change to
  the page is a change to both web clients (keep them alike) and to the app. What only the app has
  (scripts folder, files, clipboard) goes through `IAppHost`, which the web does not register.
- **Android app:** keep platform code in `src/MLCalc.Maui/Platforms/Android` and use MAUI's
  cross-platform APIs elsewhere, so that an iOS version stays possible.

## Build and test

```
dotnet build MLCalc.slnx
dotnet test MLCalc.slnx
dotnet run --project src/MLCalc.Wpf
dotnet run --project src/MLCalc.Cli -- -e "1 + 2"
dotnet run --project src/MLCalc.Web.Server       # web app at http://localhost:5080
```

The TypeScript client (Node.js 26), in `src/MLCalc.Web.TypeScript`:

```
npm ci
npm run check && npm test && npm run build    # build goes to src/MLCalc.Web.Server/ts-client
```

The Android app (MAUI workload `maui-android`, JDK 21, Android SDK; `JAVA_HOME` and `ANDROID_HOME`
set), not in `MLCalc.slnx`. `-t:Run` installs and starts it on a phone connected over USB:

```
dotnet build src/MLCalc.Maui -f net10.0-android                    # or MLCalc.App.slnx
dotnet build src/MLCalc.Maui -t:Run -f net10.0-android -c Release
```

Debug builds are interpreted and noticeably slower; measure speed in Release
(`-p:WebViewDebugging=true` lets `chrome://inspect` or `adb forward` reach the page).

`tests/fixtures` holds test cases shared by C# and TypeScript (drawings and .NET number formatting),
written by `tests/MLCalc.Web.Tests/SharedFixtureTests.cs`. After a deliberate drawing change, run
the .NET tests with `MLCALC_UPDATE_FIXTURES=1` to write them anew.

`global.json` pins SDK 10. The tests run under a culture with a decimal comma (`tests/TestCulture.cs`).

## Dependency updates

Dependabot (`.github/dependabot.yml`) opens weekly pull requests for NuGet (central versions in
`Directory.Packages.props`), npm (`src/MLCalc.Web.TypeScript`, exact versions), GitHub Actions
(pinned to commits, with the version in a comment) and the Docker base images. When asked to look
at one: CI must pass, including the shared drawing test cases; an update of TypeScript, Vite or
Vitest may need code changes, not only a new lock file. An update of the MAUI packages
(`Microsoft.Maui.Controls`, `Microsoft.AspNetCore.Components.WebView.Maui`) may need a newer MAUI
workload locally: `dotnet workload update` in the repository's folder.

## Commits

The repository owner makes the commits; give the `git` commands rather than running `git commit`.
Commit messages end with a `Co-Authored-By` line for Claude.
