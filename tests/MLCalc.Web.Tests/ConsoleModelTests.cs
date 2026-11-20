using MLCalc.Web.Ui;
using MLCalc.Web.Protocol;

namespace MLCalc.Web.Tests;

/// <summary>The browser console's logic: prompts, unfinished input, history and messages.</summary>
public class ConsoleModelTests
{
	static readonly Limits Limits = new(60, 1000, 1000, 5000, 200_000);

	static RunFinishedMessage Finished(RunStatus status, params RunError[] errors) =>
		new("1", status, errors.Length == 0 ? null : errors);

	[Fact]
	public void AnUnfinishedProgramCollectsLinesUntilItRuns()
	{
		var console = new ConsoleModel();
		Assert.Equal("k = 0; while k < 3", console.Submit("k = 0; while k < 3"));
		console.Apply(Finished(RunStatus.Incomplete), Limits);
		Assert.Equal("...", console.Prompt);

		Assert.Equal("k = 0; while k < 3\nk = k + 1;", console.Submit("k = k + 1;"));
		console.Apply(Finished(RunStatus.Incomplete), Limits);
		Assert.Equal("k = 0; while k < 3\nk = k + 1;\nend", console.Submit("end"));
		console.Apply(Finished(RunStatus.Ok), Limits);
		Assert.Equal(">>", console.Prompt);
		Assert.Equal([">> k = 0; while k < 3", "... k = k + 1;", "... end"], console.Lines.Select(l => l.Text));
	}

	[Fact]
	public void AnEmptyLineRunsUnfinishedInputAndOtherwiseNothing()
	{
		var console = new ConsoleModel();
		Assert.Null(console.Submit(""));
		console.Submit("while 1");
		console.Apply(Finished(RunStatus.Incomplete), Limits);
		Assert.Equal("while 1\n", console.Submit(""));
	}

	[Fact]
	public void EscDiscardsUnfinishedInput()
	{
		var console = new ConsoleModel();
		console.Submit("[1 2");
		console.Apply(Finished(RunStatus.Incomplete), Limits);
		console.DiscardUnfinished();
		Assert.Equal(">>", console.Prompt);
		Assert.Equal("1", console.Submit("1"));
	}

	[Fact]
	public void UpAndDownRecallEarlierLines()
	{
		var console = new ConsoleModel();
		console.Submit("a = 1");
		console.Submit("b = 2");
		console.Submit("b = 2");   // repeated lines are kept once
		Assert.Equal("b = 2", console.Recall(-1));
		Assert.Equal("a = 1", console.Recall(-1));
		Assert.Null(console.Recall(-1));
		Assert.Equal("b = 2", console.Recall(1));
		Assert.Equal("", console.Recall(1));
		Assert.Null(console.Recall(1));
	}

	[Fact]
	public void AnErrorInOneLineGetsACaret()
	{
		var console = new ConsoleModel();
		console.Submit("1 +* 2");
		console.Apply(Finished(RunStatus.Error, new RunError("Expected an expression but found '*'.", 1, 4, 1)), Limits);
		var error = console.Lines[^1];
		Assert.Equal(ConsoleLineKind.Error, error.Kind);
		// ">> 1 +* 2": the caret is under the *.
		Assert.Equal("      ^\nExpected an expression but found '*'.", error.Text);
		Assert.Equal('*', console.Lines[^2].Text[error.Text.IndexOf('^')]);
	}

	[Fact]
	public void AnErrorInAScriptNamesTheLine()
	{
		var console = new ConsoleModel();
		console.OpenScript("demo.m", "x = 1\ny = nothere");
		console.Apply(Finished(RunStatus.Error, new RunError("Unknown function or variable 'nothere'.", 2, 5, 7)), Limits);
		Assert.Equal("Line 2, column 5: Unknown function or variable 'nothere'.", console.Lines[^1].Text);
	}

	[Fact]
	public void OpeningAScriptRunsItEvenWhenUnfinished()
	{
		var console = new ConsoleModel();
		Assert.Equal("while 1\n\n", console.OpenScript("a.m", "while 1"));
		Assert.Equal("x = 1\n\n", console.OpenScript("b.m", "x = 1\n"));
	}

	[Fact]
	public void OutputClearAndStatusMessages()
	{
		var console = new ConsoleModel();
		console.Apply(new OutputMessage([new OutputEntry(OutputKind.Value, "x = 1"), new OutputEntry(OutputKind.Text, "hello")]));
		Assert.Equal([ConsoleLineKind.Value, ConsoleLineKind.Text], console.Lines.Select(l => l.Kind));
		console.Apply(new OutputMessage([new OutputEntry(OutputKind.Clear, ""), new OutputEntry(OutputKind.Text, "after")]));
		Assert.Equal(["after"], console.Lines.Select(l => l.Text));

		console.Apply(new RunFinishedMessage("1", RunStatus.TimeLimit, OutputTruncated: true), Limits);
		Assert.Equal(
			["Stopped: a program may run for at most 60 seconds here.", "Only the first 5000 lines of output are shown."],
			console.Lines.Skip(1).Select(l => l.Text));
	}

	[Fact]
	public void OldLinesAreDropped()
	{
		var console = new ConsoleModel();
		console.Apply(new OutputMessage([.. Enumerable.Range(0, ConsoleModel.MaxLines + 10).Select(k => new OutputEntry(OutputKind.Text, $"{k}"))]));
		Assert.Equal(ConsoleModel.MaxLines, console.Lines.Count);
		Assert.Equal("10", console.Lines[0].Text);
	}
}
