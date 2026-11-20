# MLCalc web app: the TypeScript client

The second client of the MLCalc web app, written in TypeScript without a UI framework, against the
protocol in [docs/web-protocol.md](../../docs/web-protocol.md) alone. It looks and behaves like the
Blazor client, whose page (`src/MLCalc.Web.Ui`) gives it its stylesheet and icon, and the server
serves it at `/ts/`.

## Commands

Needs Node.js 26.

```
npm ci           # the exact package versions in package-lock.json
npm run check    # type check (strict), including the tests and vite.config.ts
npm test         # unit tests and the test cases shared with C#
npm run build    # type check and build into ../MLCalc.Web.Server/ts-client
npm run dev      # Vite with hot reload at http://localhost:5173/ts/
```

`npm run dev` sends `/hub` to the server at <http://localhost:5080>, so run the server as well:
`dotnet run --project src/MLCalc.Web.Server`. The Docker image builds the client in a stage of its
own (see the Dockerfile).

## How it is made

| Folder or file | What it is | Ported from |
|---|---|---|
| `src/protocol.ts` | The messages, as TypeScript types | `docs/web-protocol.md` |
| `src/connection.ts` | The SignalR connection, session id and reconnecting | `MLCalcConnection.cs` |
| `src/console-model.ts` | Console lines, history, unfinished input, error markers | `ConsoleModel.cs` |
| `src/figure-state.ts` | The plot windows from the figure messages | `FigureState.cs` |
| `src/plotting/` | The plot model, axis ticks, layout, 3D projection and scene, camera control, SVG writer, and .NET-style number formatting | `src/MLCalc.Plotting` |
| `src/ui/` | The page: toolbar, console, plot windows (turning, zooming, pinching), divider, downloads | `MainPage.razor`, `PlotWindowView.razor`, `mlcalc.js` (in `src/MLCalc.Web.Ui`) |

The only runtime dependency is `@microsoft/signalr`. The page is built from DOM elements with the
Blazor client's structure and CSS classes; plot windows are drawn as SVG text.

## Drawing the same as C#

`src/plotting` is a line-by-line port of the C# drawing code, with the arithmetic in the same order,
so that both clients produce the same SVG to the character. The C# tests write test cases to
`tests/fixtures` (windows with the SVG drawn for them, the camera descriptions, and .NET's
formatting of tricky numbers), and `test/shared-fixtures.test.ts` checks the port against them.

Where JavaScript differs from .NET — single-precision coordinates, rounding, negative zero, when
numbers get an exponent — `src/plotting/format.ts` does it the .NET way; the protocol document
lists the rules.

## The version

`vite.config.ts` makes the build's version as the .NET build does: `VersionPrefix` from
`Directory.Build.props`, `+`, and the commit (`SOURCE_REVISION`, set by the Docker build, or else
`git rev-parse HEAD`). When the server's `Welcome.version` differs, the page offers to reload.
