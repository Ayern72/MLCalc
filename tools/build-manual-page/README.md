# build-manual-page

Builds the web page version of the [user's manual](../../docs/manual.md): a single HTML file
with a contents sidebar, console-style examples and light and dark themes.

```
dotnet run tools/build-manual-page/BuildManualPage.cs
```

This writes `artifacts/manual.html` (ignored by git). Optional arguments override the paths:

```
dotnet run tools/build-manual-page/BuildManualPage.cs -- [manual.md] [template.html] [output.html]
```

## Files

| File | Purpose |
|---|---|
| `BuildManualPage.cs` | single-file .NET program (requires the .NET 10 SDK); converts the Markdown with [Markdig](https://github.com/xoofx/markdig) |
| `template.html` | the page around the manual: header, styles, sidebar and the script that highlights the current section; `{{TOC}}` and `{{BODY}}` are filled in |

## What the conversion does

- Removes the manual's title and written contents list; the page header and sidebar replace them.
- Builds the sidebar from the headings, listing the subsections of chapters 7 and 8.
- Styles console transcripts: `>>` and `...` prompts, input, error lines and `%` comments.
- Adds colour swatches to the pen table in section 8.3, using the palette from `MLCalc.Plotting.Pens`.
- Shows the disclaimer as a notice and points links to other repository files (`grammar.md`,
  `LICENSE`) to GitHub.
- Fails if a link within the page has no target, for example after a heading was renamed.

CI runs the tool on every push, so a change to the manual that breaks the page is noticed.
