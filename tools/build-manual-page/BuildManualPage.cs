#:package Markdig
// Builds the user's manual web page: converts docs/manual.md into a single HTML page using
// template.html (next to this file).
//
// Run from anywhere:   dotnet run tools/build-manual-page/BuildManualPage.cs
// Optional arguments:  [manual.md] [template.html] [output.html]
// Default output:      artifacts/manual.html (ignored by git)
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

string toolDirectory = ThisDirectory();
string repository = Path.GetFullPath(Path.Combine(toolDirectory, "..", ".."));
string manualPath = args.Length > 0 ? args[0] : Path.Combine(repository, "docs", "manual.md");
string templatePath = args.Length > 1 ? args[1] : Path.Combine(toolDirectory, "template.html");
string outputPath = args.Length > 2 ? args[2] : Path.Combine(repository, "artifacts", "manual.html");

string markdown = File.ReadAllText(manualPath).Replace("\r\n", "\n");
string template = File.ReadAllText(templatePath);

// The page header replaces the title, and the sidebar replaces the written contents list.
markdown = Regex.Replace(markdown, @"\A# .*\n", "");
markdown = Regex.Replace(markdown, @"## Contents\n.*?\n---\n", "", RegexOptions.Singleline);

var pipeline = new MarkdownPipelineBuilder()
	.UsePipeTables()
	.UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
	.Build();
var document = Markdown.Parse(markdown, pipeline);

// Sidebar: all h2 headings, with the h3 headings of the numbered chapters nested below them.
var toc = new StringBuilder("\t\t\t\t<ol>\n");
bool openSub = false;
foreach (var heading in document.Descendants<HeadingBlock>())
{
	if (heading.Level is not (2 or 3))
		continue;
	string id = heading.GetAttributes().Id ?? "";
	string text = WebUtility.HtmlEncode(PlainText(heading.Inline));
	if (heading.Level == 2)
	{
		if (openSub)
		{
			toc.Append("\t\t\t\t\t</ol></li>\n");
			openSub = false;
		}
		toc.Append($"\t\t\t\t\t<li><a href=\"#{id}\">{text}</a>");
		// Only the function reference and plotting chapters have numbered subsections worth listing.
		if (text.StartsWith("7.") || text.StartsWith("8."))
		{
			toc.Append("<ol>\n");
			openSub = true;
		}
		else
			toc.Append("</li>\n");
	}
	else if (openSub)
		toc.Append($"\t\t\t\t\t\t<li><a href=\"#{id}\">{text}</a></li>\n");
}
if (openSub)
	toc.Append("\t\t\t\t\t</ol></li>\n");
toc.Append("\t\t\t\t</ol>");

string html = document.ToHtml(pipeline);

// Links to other repository documents point to GitHub.
html = html.Replace("href=\"grammar.md\"", "href=\"https://github.com/Ayern72/MLCalc/blob/main/docs/grammar.md\"")
	.Replace("href=\"../LICENSE\"", "href=\"https://github.com/Ayern72/MLCalc/blob/main/LICENSE\"");

// The disclaimer at the top is a notice, not a quotation.
html = html.Replace("<blockquote>", "<aside class=\"notice\" role=\"note\">").Replace("</blockquote>", "</aside>");

// Wide tables scroll inside their own container.
html = html.Replace("<table>", "<div class=\"table-wrap\"><table>").Replace("</table>", "</table></div>");

// Console transcripts: prompts, input and errors as in the MLCalc console; % comments greyed.
html = Regex.Replace(html, @"<pre><code>(.*?)</code></pre>", m =>
{
	var lines = m.Groups[1].Value.TrimEnd('\n').Split('\n').Select(line =>
	{
		var prompt = Regex.Match(line, @"^(&gt;&gt;|\.\.\.) (.*)$");
		if (prompt.Success)
			return $"<span class=\"p\">{prompt.Groups[1].Value}</span> <span class=\"in\">{prompt.Groups[2].Value}</span>";
		if (Regex.IsMatch(line, @"^\d+:\d+: "))
			return $"<span class=\"err\">{line}</span>";
		return Regex.Replace(line, @"(%.*)$", "<span class=\"cm\">$1</span>");
	});
	return "<pre><code>" + string.Join("\n", lines) + "</code></pre>";
}, RegexOptions.Singleline);

// Colour swatches in the pen table (the palette from MLCalc.Plotting.Pens).
string[] palette = ["1f77b4", "d62728", "2ca02c", "ff7f0e", "9467bd", "8c564b", "e377c2", "17becf",
	"bcbd22", "7f7f7f", "393b79", "ad494a", "637939", "8c6d31", "7b4173"];
int penSection = html.IndexOf("id=\"83-pens-and-colours\"", StringComparison.Ordinal);
int nextSection = penSection < 0 ? -1 : html.IndexOf("<h3", penSection + 1, StringComparison.Ordinal);
if (nextSection < 0)
{
	Console.Error.WriteLine("Section \"8.3 Pens and colours\" not found; was it renamed?");
	return 1;
}
string pens = Regex.Replace(html[penSection..nextSection], @"<td>(\d+)</td>\s*<td>([a-z ]+)</td>", m =>
{
	int pen = int.Parse(m.Groups[1].Value);
	return $"<td>{pen}</td>\n<td><span class=\"swatch\" style=\"background:#{palette[pen - 1]}\" aria-hidden=\"true\"></span>{m.Groups[2].Value}</td>";
});
html = html[..penSection] + pens + html[nextSection..];

// Every link within the page must have a target.
var ids = Regex.Matches(html, "id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
var missing = Regex.Matches(html + toc, "href=\"#([^\"]+)\"").Select(m => m.Groups[1].Value).Where(id => !ids.Contains(id)).Distinct().ToList();
if (missing.Count > 0)
{
	Console.Error.WriteLine("Broken links within the page: #" + string.Join(", #", missing));
	return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, template.Replace("{{TOC}}", toc.ToString()).Replace("{{BODY}}", html.TrimEnd()));
Console.WriteLine($"Wrote {outputPath}: {Regex.Matches(toc.ToString(), "<li>").Count} contents entries, {Regex.Matches(html, "class=\"p\"").Count} console prompts.");
return 0;

static string PlainText(ContainerInline? inline)
{
	var text = new StringBuilder();
	if (inline is null)
		return "";
	foreach (var child in inline)
		text.Append(child switch
		{
			LiteralInline literal => literal.Content.ToString(),
			CodeInline code => code.Content,
			ContainerInline container => PlainText(container),
			_ => "",
		});
	return text.ToString();
}

static string ThisDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
