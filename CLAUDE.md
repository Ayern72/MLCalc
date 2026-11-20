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

`tests/fixtures` holds test cases shared by C# and TypeScript (drawings and .NET number formatting),
written by `tests/MLCalc.Web.Tests/SharedFixtureTests.cs`. After a deliberate drawing change, run
the .NET tests with `MLCALC_UPDATE_FIXTURES=1` to write them anew.

`global.json` pins SDK 10. The tests run under a culture with a decimal comma (`tests/TestCulture.cs`).

## Commits

The repository owner makes the commits; give the `git` commands rather than running `git commit`.
Commit messages end with a `Co-Authored-By` line for Claude.
