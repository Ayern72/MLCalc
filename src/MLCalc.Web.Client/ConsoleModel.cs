using MLCalc.Web.Protocol;

namespace MLCalc.Web.Client;

/// <summary>What a console line is, for its style.</summary>
enum ConsoleLineKind
{
	/// <summary>What the user typed, after the prompt.</summary>
	Input,
	/// <summary>A value, e.g. <c>x = 3</c>.</summary>
	Value,
	/// <summary>Text from the program.</summary>
	Text,
	/// <summary>An error in the program.</summary>
	Error,
	/// <summary>A message from the app itself, e.g. that a program was stopped.</summary>
	Info,
}

/// <summary>One entry in the console; the text may have several lines.</summary>
sealed record ConsoleLine(ConsoleLineKind Kind, string Text);

/// <summary>
/// The console without its user interface: the lines shown, unfinished input, and the history of
/// typed lines. The page sends what <see cref="Submit"/> returns to the server and passes the
/// answers back in.
/// </summary>
sealed class ConsoleModel
{
	/// <summary>Older lines are dropped beyond this, to keep the page fast.</summary>
	public const int MaxLines = 2_000;

	const string MainPrompt = ">>";
	const string ContinuationPrompt = "...";

	readonly List<ConsoleLine> lines = [];
	readonly List<string> unfinished = [];
	readonly List<string> history = [];
	int historyIndex;
	string? lastLine;
	string? lastSource;

	public IReadOnlyList<ConsoleLine> Lines => lines;

	/// <summary><c>&gt;&gt;</c>, or <c>...</c> while an unfinished program waits for more lines.</summary>
	public string Prompt => unfinished.Count == 0 ? MainPrompt : ContinuationPrompt;

	/// <summary>True while an unfinished program waits for more lines.</summary>
	public bool IsContinuing => unfinished.Count > 0;

	/// <summary>
	/// The user pressed Enter with <paramref name="line"/>, which may hold several lines when pasted.
	/// Returns the program to run, or null when there is nothing to run (an empty line).
	/// </summary>
	public string? Submit(string line)
	{
		Add(ConsoleLineKind.Input, $"{Prompt} {line}");
		if (line.Length > 0 && (history.Count == 0 || history[^1] != line))
			history.Add(line);
		historyIndex = history.Count;
		if (line.Trim().Length == 0 && unfinished.Count == 0)
			return null;
		lastLine = line;
		// An empty line after unfinished input runs it anyway, which reports what is missing.
		lastSource = string.Join('\n', [.. unfinished, line]);
		return lastSource;
	}

	/// <summary>A script the user opened: runs as a whole, even when it is unfinished.</summary>
	public string OpenScript(string fileName, string text)
	{
		Add(ConsoleLineKind.Info, $"Running {fileName}");
		unfinished.Clear();
		lastLine = null;
		// A last empty line makes the server run it even when unfinished, and report what is missing.
		lastSource = text.EndsWith('\n') ? text + "\n" : text + "\n\n";
		return lastSource;
	}

	/// <summary>Esc while nothing runs: forgets unfinished input.</summary>
	public void DiscardUnfinished()
	{
		if (unfinished.Count == 0)
			return;
		unfinished.Clear();
		Add(ConsoleLineKind.Info, "Discarded the unfinished input.");
	}

	/// <summary>The previous (<paramref name="step"/> = −1) or next (+1) line of the history; null when there is none.</summary>
	public string? Recall(int step)
	{
		int index = historyIndex + step;
		if (index < 0 || index > history.Count)
			return null;
		historyIndex = index;
		return index == history.Count ? "" : history[index];
	}

	public void Apply(OutputMessage message)
	{
		foreach (var entry in message.Entries)
		{
			switch (entry.Kind)
			{
				case OutputKind.Clear:
					lines.Clear();
					break;
				case OutputKind.Value:
					Add(ConsoleLineKind.Value, entry.Text);
					break;
				default:
					Add(ConsoleLineKind.Text, entry.Text);
					break;
			}
		}
	}

	/// <summary>The end of a run: keeps unfinished input, or reports errors and stops.</summary>
	public void Apply(RunFinishedMessage message, Limits limits)
	{
		if (message.Status == RunStatus.Incomplete)
		{
			if (lastLine is not null)
				unfinished.Add(lastLine);
			return;
		}
		string? source = lastSource;
		unfinished.Clear();
		lastLine = null;
		lastSource = null;
		switch (message.Status)
		{
			case RunStatus.Error:
				foreach (var error in message.Errors ?? [])
					Add(ConsoleLineKind.Error, Describe(error, source));
				break;
			case RunStatus.Cancelled:
				Add(ConsoleLineKind.Info, "Stopped.");
				break;
			case RunStatus.TimeLimit:
				Add(ConsoleLineKind.Info, $"Stopped: a program may run for at most {limits.RunSeconds} seconds here.");
				break;
			case RunStatus.Busy:
				Add(ConsoleLineKind.Info, "Another program is still running.");
				break;
		}
		if (message.OutputTruncated)
			Add(ConsoleLineKind.Info, $"Only the first {limits.MaxOutputEntries} lines of output are shown.");
	}

	/// <summary>
	/// An error: for one line of input a caret under the place, as the desktop app shows it;
	/// otherwise the line and column.
	/// </summary>
	string Describe(RunError error, string? source)
	{
		if (error.Line == 0)
			return error.Message;
		bool oneLine = source is not null && !source.TrimEnd('\n').Contains('\n');
		if (oneLine)
			return new string(' ', MainPrompt.Length + error.Column) + "^\n" + error.Message;
		return $"Line {error.Line}, column {error.Column}: {error.Message}";
	}

	public void AddInfo(string text) => Add(ConsoleLineKind.Info, text);

	void Add(ConsoleLineKind kind, string text)
	{
		lines.Add(new ConsoleLine(kind, text));
		if (lines.Count > MaxLines)
			lines.RemoveRange(0, lines.Count - MaxLines);
	}
}
