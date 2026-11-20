# The MLCalc web protocol

How a browser client talks to the MLCalc web server. A client in any language can be written from
this document alone. There are two:

- The Blazor client in `src/MLCalc.Web.Client`, with its page in `src/MLCalc.Web.Ui`, uses the C#
  types in `src/MLCalc.Web.Protocol`, which serialise to exactly the JSON shown here.
- The TypeScript client in `src/MLCalc.Web.TypeScript` was written from this document; its
  `src/protocol.ts` has the messages as TypeScript types. It is the example to follow for a client
  outside .NET.

The Android app (`src/MLCalc.Maui`) uses the same messages without a network: its page is the
Blazor client's, and its programs run in the app, in the same `Workspace` (`src/MLCalc.Hosting`)
as on the server, which hands the messages straight to the page.

A client that draws the plots the way the C# code does can check itself against the test cases
in `tests/fixtures` (see [Drawing the plots](#drawing-the-plots)).

The server runs the programs, with one workspace per user (a *session*). The client shows the
console, sends what the user types or the text of a script the user opens, and draws the plots
it receives. Turning a 3D window happens entirely in the client.

## Connection

The server is an ASP.NET Core [SignalR](https://learn.microsoft.com/aspnet/core/signalr/introduction)
hub at the path **`/hub`**, using the **JSON** hub protocol. Use a SignalR client library, for
example `@microsoft/signalr` from npm in JavaScript or TypeScript:

```ts
import * as signalR from "@microsoft/signalr";

const session = sessionStorage.getItem("mlcalc-session");
const connection = new signalR.HubConnectionBuilder()
	.withUrl("/hub" + (session ? `?session=${encodeURIComponent(session)}` : ""))
	.build();
connection.on("Welcome", welcome => sessionStorage.setItem("mlcalc-session", welcome.sessionId));
connection.on("Output", output => { /* … */ });
await connection.start();
await connection.invoke("Run", { id: "1", source: "x = 1 + 2" });
```

JSON rules for every message:

- Names are in camelCase.
- Optional fields that have no value are left out (not sent as `null`). In arrays of
  coordinates, `null` does appear: it is a missing point.
- Enumerations are strings, listed below for each field.
- Some characters in strings, such as `<`, `>` and `+`, are written as Unicode escape sequences,
  as JSON allows; any JSON parser reads them back as the characters themselves.
- Method names are matched without regard to case, as in any SignalR client.

## Sessions

- The first message on every connection is `Welcome`. It carries the session id.
- To continue the same session after a lost connection or a reloaded page, connect with
  `/hub?session=<id>`. The variables, the plots and a running program are kept. Both clients
  keep the id in the browser tab's `sessionStorage` under `mlcalc-session`, so each tab has its own
  session, and switching between the clients in a tab keeps it.
- An unknown or expired id gives a new session; `Welcome.newSession` is then `true`.
- A session without activity for 30 minutes (the server's `IdleTimeout`) is removed. If the
  client is still connected, its next call gets a new `Welcome` with `newSession: true` before
  the answer.
- When the server already has as many sessions as it may (`MaxSessions`), the connection fails
  with the error "The server has too many users at the moment; please try again later."
- Messages produced while no client is connected are dropped. After reconnecting, `Welcome`
  has the whole figure and tells whether a program is still running.

## Client → server

Each method returns as soon as the work has started; the results come as messages. A message
from the client may be at most 1 MB.

### `Run(RunRequest)`

```json
{ "id": "1", "source": "x = [1 2; 3 4] * [1; 1]" }
```

| Field | Type | Meaning |
|---|---|---|
| `id` | string | Chosen by the client; returned in `RunFinished.id` |
| `source` | string | The program: console input, or the text of a script the user opened |

- **Unfinished input.** When the program is unfinished (an open block, bracket or parenthesis)
  and its **last line is not empty**, nothing runs and the answer is `RunFinished` with status
  `incomplete`. The client then shows a continuation prompt and sends the collected lines again
  with the next one. An empty last line runs the program anyway, which reports what is missing,
  so a client that sends a whole script should end it with an empty line.
- **One program at a time.** A `Run` while a program of the session is running gets status
  `busy` and does not run.
- **No files.** Programs cannot read script files on the server: `run("x.m")`, or input that is
  just a file name, is the error "Script files cannot be run here." A client runs a script by
  sending its text.
- Text longer than `limits.maxSourceLength` characters is refused with status `error`.

### `Cancel()`

Stops the running program, if any; it then ends with status `cancelled`.

### `SetCamera(CameraUpdate)`

Sent when the user has turned, zoomed or reset a 3D window in the client, so that `savefig` and
later commands see the same view.

```json
{ "row": 0, "column": 1, "camera": { "azimuth": 20, "elevation": 10, "distance": 2.5 } }
```

`row` and `column` are zero-based; `camera` is as in a [window](#windows). The server does not
send the window back for it. It ignores the update when the window is no longer 3D, or when the
camera is one the commands could not have made: an elevation outside −90 to 90, a distance
outside 0.05 to 100, a position inside the box or at the target, or coordinates that are not
finite numbers.

## Server → client

All messages of a session arrive in the order they were produced. While a program runs, the
server sends what it has gathered every 50 ms: first `Output`, then `FigureChanged`, then any
`Download`. When the program ends, the rest follows, and then `RunFinished`.

### `Welcome`

```json
{
	"sessionId": "3f2a9c0e5b7d4e61a8c2f0b9d7e6a1c4",
	"newSession": true,
	"running": false,
	"version": "0.1.0+2074ec35c4a3ab48e0360eaa53e6d4ae199b5fd8",
	"builtinNames": ["abs", "acos", "…"],
	"limits": {
		"runSeconds": 60,
		"maxMatrixElements": 2000000,
		"maxPointsPerWindow": 200000,
		"maxOutputEntries": 5000,
		"maxSourceLength": 200000
	},
	"figure": { "…": "a figure message with every window whole; see FigureChanged" }
}
```

| Field | Meaning |
|---|---|
| `sessionId` | 32 hexadecimal digits; pass it as `?session=` to reconnect |
| `newSession` | True for a new session, also when the one asked for no longer existed |
| `running` | True when a program of this session is running (after a reconnect) |
| `version` | The server's build: version number, `+` and the commit when the build knew it |
| `builtinNames` | All functions and commands, sorted |
| `limits.runSeconds` | How long a program may run, waiting for a free slot included |
| `limits.maxMatrixElements` | The largest matrix or range |
| `limits.maxPointsPerWindow` | The most points in one plot window |
| `limits.maxOutputEntries` | The most console entries one run may write; the rest are left out |
| `limits.maxSourceLength` | The longest program text accepted |
| `figure` | Every plot window, each as `replace` |

A client that the server itself serves, such as the Blazor and TypeScript clients, is built
together with it and knows its own version (the TypeScript build reads the version number from
`Directory.Build.props` and adds the commit, as the .NET build does). When `version` differs, the server has been updated since the page was
loaded (a reconnect after a deploy): the client offers to reload the page. A client built and served
separately has no version of its own to compare and can ignore the field.

### `Output`

Console output, oldest first.

```json
{ "entries": [ { "kind": "value", "text": "x =\n  3\n  7" }, { "kind": "text", "text": "done" } ] }
```

| `kind` | Meaning |
|---|---|
| `value` | A value, already formatted, e.g. `x = 3` or a matrix on several lines |
| `text` | Text from the program, e.g. from `disp` or `who` |
| `clear` | Clear the console (`cls`); `text` is empty |

Show `text` as is, in a monospace font, keeping its line breaks and spaces.

### `RunFinished`

The end of a run, or the answer to a `Run` that did not start.

```json
{
	"id": "1",
	"status": "error",
	"errors": [ { "message": "Unknown function or variable 'nothere'.", "line": 2, "column": 9, "length": 7 } ],
	"outputTruncated": false
}
```

| `status` | Meaning |
|---|---|
| `ok` | The program ran to the end |
| `error` | A syntax error (nothing ran) or a runtime error (the program stopped there); see `errors` |
| `cancelled` | Stopped by `Cancel` |
| `timeLimit` | Stopped after `limits.runSeconds` |
| `incomplete` | Unfinished input; nothing ran (see `Run`) |
| `busy` | Another program of the session is running; nothing ran |

`errors` is there only for `error`. `line` and `column` are one-based places in `source`, and
`length` is the number of characters the error covers; `line` and `column` are 0 for an error
without a place, such as text that is too long. `outputTruncated` is true when the program wrote
more than `limits.maxOutputEntries` entries and the rest were left out.

### `Download`

A file from `savefig`, for the client to save as a download.

```json
{ "fileName": "plot.svg", "mediaType": "image/svg+xml", "content": "<svg xmlns=…" }
```

`content` is the file's text. Only SVG is possible: `savefig` with another extension is an
error. The console also gets the line "Sent plot.svg to the browser to save."

### `FigureChanged`

What changed in the plot windows.

```json
{
	"version": 12,
	"rows": 1,
	"columns": 2,
	"currentRow": 0,
	"currentColumn": 1,
	"windows": [ { "index": 1, "append": { "pen": 3, "items": [ { "…": "an item" } ] } } ]
}
```

| Field | Meaning |
|---|---|
| `version` | Grows with every change |
| `rows`, `columns` | The grid of windows, 1–3 each |
| `currentRow`, `currentColumn` | The window commands draw into, zero-based; when the grid has more than one window, draw a frame around it |
| `windows` | The windows that changed; the others are as before. May be empty (e.g. only the current window changed) |

When `rows` or `columns` changes, every window is sent with `replace`, and the client starts a new
grid of that size. Each entry of `windows` has an `index` (row × columns + column) and exactly one of:

- **`replace`**: the whole [window](#windows), replacing what the client has.
- **`append`**: additions to the window as the client has it, the common case while a program
  plots in a loop:
  1. `lastDots`, when given (`{ "x": [...], "y": [...] }`, plus `"z"` in 3D), adds points to the
     window's last item, which is then a `dots` or `dots3d` item;
  2. `items` are added after the existing items;
  3. `pen` becomes the window's pen.

## Windows

```json
{
	"kind": "space",
	"xMin": -10, "xMax": 10, "yMin": -10, "yMax": 10, "zMin": -10, "zMax": 10,
	"title": "", "xTitle": "", "yTitle": "", "zTitle": "",
	"grid": false,
	"pen": 1,
	"camera": { "azimuth": -37.5, "elevation": 30 },
	"items": []
}
```

| Field | Meaning |
|---|---|
| `kind` | `cartesian` (an x-y window), `complex` (the complex plane: real part across, imaginary part up) or `space` (3D) |
| `xMin` … `zMax` | The ranges of the axes; z only matters in 3D |
| `title`, `xTitle`, … | Titles; empty for none. A complex window without axis titles shows "Re" and "Im" |
| `grid` | Whether grid lines are shown |
| `pen` | The pen the next item gets |
| `camera` | The camera of a 3D window (below) |
| `items` | What is drawn, in drawing order |

**Camera.** Without `position`, the camera is placed relative to the box: it looks at the
target from `azimuth` and `elevation` (degrees; azimuth 0 looks from −y towards +y, 90 from +x;
elevation is the angle above the x-y plane, −90 to 90), at `distance` in units of half the box
(left out: just far enough for the whole box to fit). With `position` (`[x, y, z]` in data
coordinates) the camera stays at that point. `target` (`[x, y, z]`) is the point looked at; left
out, it is the middle of the box. The default camera is azimuth −37.5°, elevation 30°.

**Items.** Every item has `type`, `pen` and the coordinates `x` and `y` (and `z` in 3D), as
arrays of the same length. A coordinate is a number or `null`; `null` is a missing point, which
breaks a line. Coordinates are sent in single precision.

| `type` | From | Other fields | Drawn as |
|---|---|---|---|
| `series` | `plot`, `cplot` | `style` | Points joined or marked by `style` |
| `function` | `fplot`, `polyplot` | `label` | A line through the points |
| `dots` | `dot` | | A dot at each point |
| `series3d` | `plot3d`, a curve from `fplot3d` | `style`, `z` | As `series`, in 3D |
| `dots3d` | `dot3d` | `z` | Dots in 3D |
| `surface` | `fplot3d` | `label`, `xCount`, `yCount`, `z` | A grid of `xCount` × `yCount` points, row by row (x changes fastest); a `null` z is a hole |

`style` is `line` (joined), `cross`, `star` or `dot` (a marker at each point).

**Pens** are numbered 1–15 (higher numbers wrap around) and have these colours:

| Pen | Colour | Pen | Colour | Pen | Colour |
|---|---|---|---|---|---|
| 1 | `#1f77b4` | 6 | `#8c564b` | 11 | `#393b79` |
| 2 | `#d62728` | 7 | `#e377c2` | 12 | `#ad494a` |
| 3 | `#2ca02c` | 8 | `#17becf` | 13 | `#637939` |
| 4 | `#ff7f0e` | 9 | `#bcbd22` | 14 | `#8c6d31` |
| 5 | `#9467bd` | 10 | `#7f7f7f` | 15 | `#7b4173` |

## Drawing the plots

To draw like the desktop app and the SVG export, follow the C# code in `src/MLCalc.Plotting`:
`PlotLayout` (margins and the plot area), `AxisTicks` (tick values), `PlotTransform` (data to
screen), `SvgWriter` (lines, markers, dots and titles), and for 3D `Scene3D` and `Projection3D`
(the box, the perspective projection and the order of surface patches) and `CameraControl`
(turning with the mouse: 0.5° per pixel; the wheel moves the camera by a factor 0.85 per notch
towards its target). The TypeScript client's `src/plotting` is a line-by-line port.

**Test cases.** `tests/fixtures` holds what the C# code makes, for a client to compare with:

- `drawings/*.json`: a window as this protocol sends it (`window`), a size, and the SVG text the
  C# code draws for it (`svg`, one string per line); for 3D windows also the camera as the status
  bar describes it, after two notches of the wheel, and after a drag (`camera`).
- `number-format.json`: numbers formatted as the SVG text and the status bar use them, and axis
  ticks.

To draw the same SVG to the character, a client must do the arithmetic in the same order as the C#
code, and format numbers the .NET way. In JavaScript this means:

- **Single precision.** Coordinates arrive in single precision; .NET reads them into a `float`.
  Use `Math.fround` on each, or the drawings differ in the last digits.
- **"0.##"** (coordinates): .NET takes 15 significant digits first, then rounds half away from
  zero, so 1.005 gives `1.01`; a negative number that rounds to zero gives `-0`.
- **"G6", "G5", "G4"** (tick labels, status bar): correctly rounded, exact halves to even (311.125
  in "G5" is `311.12`); an exponent from 10⁶ (10⁵, 10⁴) up and below 10⁻⁴, written `1E+06`.
- **Plain numbers** (sizes, widths): the shortest text that reads back as the number, with an
  exponent from 10¹⁷ up and below 10⁻⁴, written `1E+17` or `1E-05`.
- **`Math.Round(x, d)`**: multiply by 10^d, round half to even, divide; from 10¹⁶ up, unchanged.

The TypeScript client's `src/plotting/format.ts` does all of this.

## An example

A client runs `x = 1`, then `fplot("x", "sin(x)")`:

| Direction | Message |
|---|---|
| server → client | `Welcome` with a 1×1 figure whose window has no items |
| client → server | `Run({ "id": "1", "source": "x = 1" })` |
| server → client | `Output({ "entries": [{ "kind": "value", "text": "x = 1" }] })` |
| server → client | `RunFinished({ "id": "1", "status": "ok", "outputTruncated": false })` |
| client → server | `Run({ "id": "2", "source": "fplot(\"x\", \"sin(x)\")" })` |
| server → client | `FigureChanged` with `{ "index": 0, "append": { "pen": 2, "items": [{ "type": "function", "pen": 1, "x": [...], "y": [...], "label": "sin(x)" }] } }` |
| server → client | `RunFinished({ "id": "2", "status": "ok", "outputTruncated": false })` |
