using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MLCalc.Interpreter;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Plotting;

namespace MLCalc.Wpf;

public enum ConsoleEntryKind
{
	Input,
	Result,
	Text,
	Error,
	Info,
}

public sealed record ConsoleEntry(ConsoleEntryKind Kind, string Text);

/// <summary>
/// The console and plot state. Programs run on a worker thread; output is marshalled to the UI
/// thread. Input that ends mid-construct (an open begin, bracket, or while … do) continues on the
/// next line.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IConsoleOutput
{
	const int MaxEntries = 10_000;

	readonly Dispatcher dispatcher;
	readonly MLCalcInterpreter interpreter;
	readonly List<string> history = [];
	readonly List<string> pendingLines = [];
	int historyIndex;
	CancellationTokenSource? cancellation;

	public MainViewModel(Dispatcher dispatcher)
	{
		this.dispatcher = dispatcher;
		interpreter = new MLCalcInterpreter(Figure, this);
		string samples = Path.Combine(AppContext.BaseDirectory, "samples");
		interpreter.WorkingDirectory = Directory.Exists(samples) ? samples : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

		Entries.Add(new ConsoleEntry(ConsoleEntryKind.Info, "MLCalc — try  [1 2; 3 4] * [1; 1]   or   fplot(\"x\", \"sin(x)*x\")"));
		Entries.Add(new ConsoleEntry(ConsoleEntryKind.Info, "Run a script by typing its name (e.g. FEIGENBA.m) or with File › Run Script. Esc stops a running script."));
	}

	public Figure Figure { get; } = new();

	public ObservableCollection<ConsoleEntry> Entries { get; } = [];

	public string WorkingDirectory => interpreter.WorkingDirectory;

	[ObservableProperty]
	private string input = "";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Prompt))]
	private bool isContinuation;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CancelCommand))]
	[NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
	private bool isBusy;

	[ObservableProperty]
	private string status = "Ready";

	[ObservableProperty]
	private string coordinates = "";

	public string Prompt => IsContinuation ? "..." : ">>";

	// ───────────── Commands ─────────────

	[RelayCommand(CanExecute = nameof(CanSubmit))]
	private async Task SubmitAsync()
	{
		string line = Input;
		Input = "";
		Add(ConsoleEntryKind.Input, $"{Prompt} {line}");
		if (!string.IsNullOrWhiteSpace(line) && (history.Count == 0 || history[^1] != line))
			history.Add(line);
		historyIndex = history.Count;

		pendingLines.Add(line);
		string source = string.Join(Environment.NewLine, pendingLines);
		if (string.IsNullOrWhiteSpace(source))
		{
			pendingLines.Clear();
			return;
		}

		// Keep reading lines while the input is unfinished; an empty line forces evaluation.
		if (!string.IsNullOrWhiteSpace(line) && Parser.Parse(source).IsIncomplete)
		{
			IsContinuation = true;
			return;
		}
		pendingLines.Clear();
		IsContinuation = false;
		await RunAsync(token => interpreter.Execute(source, token));
	}

	private bool CanSubmit() => !IsBusy;

	[RelayCommand(CanExecute = nameof(IsBusy))]
	private void Cancel() => cancellation?.Cancel();

	/// <summary>Esc while idle discards a half-typed multi-line input.</summary>
	public void Escape()
	{
		if (IsBusy)
		{
			Cancel();
			return;
		}
		if (IsContinuation)
		{
			pendingLines.Clear();
			IsContinuation = false;
			Add(ConsoleEntryKind.Info, "Input discarded.");
		}
		Input = "";
	}

	public void HistoryPrevious()
	{
		if (historyIndex > 0)
			Input = history[--historyIndex];
	}

	public void HistoryNext()
	{
		if (historyIndex < history.Count - 1)
			Input = history[++historyIndex];
		else
		{
			historyIndex = history.Count;
			Input = "";
		}
	}

	public async Task RunScriptAsync(string path)
	{
		if (IsBusy)
			return;
		Add(ConsoleEntryKind.Input, $"{Prompt} run(\"{path}\")");
		await RunAsync(token => interpreter.ExecuteFile(path, token), Path.GetFileName(path));
	}

	[RelayCommand]
	private void ClearConsole() => Entries.Clear();

	[RelayCommand]
	private void ResetPlots() => Figure.Initialize(1, 1);

	[RelayCommand]
	private void ListFunctions()
	{
		Add(ConsoleEntryKind.Info, "Functions and commands:");
		Add(ConsoleEntryKind.Text, string.Join(", ", interpreter.BuiltinNames));
		Add(ConsoleEntryKind.Info, "Constants: pi, i, j, eps, inf, nan.   Variables: list, del(name), del(all).");
	}

	async Task RunAsync(Func<CancellationToken, ExecutionResult> run, string? source = null)
	{
		IsBusy = true;
		Status = "Running…  (Esc to stop)";
		cancellation = new CancellationTokenSource();
		try
		{
			var token = cancellation.Token;
			var result = await Task.Run(() => run(token));
			foreach (var message in result.Messages())
				Add(result.Cancelled ? ConsoleEntryKind.Info : ConsoleEntryKind.Error, source is null ? message : $"{source} {message}");
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Add(ConsoleEntryKind.Error, ex.Message);
		}
		finally
		{
			cancellation.Dispose();
			cancellation = null;
			IsBusy = false;
			Status = "Ready";
		}
	}

	// ───────────── IConsoleOutput (called from the worker thread) ─────────────

	void IConsoleOutput.WriteValue(string name, ComplexMatrix value) => Post(ConsoleEntryKind.Result, ResultFormatter.Format(name, value));

	void IConsoleOutput.WriteLine(string text) => Post(ConsoleEntryKind.Text, text);

	void IConsoleOutput.Clear() => dispatcher.BeginInvoke(Entries.Clear);

	void Post(ConsoleEntryKind kind, string text) => dispatcher.BeginInvoke(() => Add(kind, text));

	void Add(ConsoleEntryKind kind, string text)
	{
		Entries.Add(new ConsoleEntry(kind, text));
		while (Entries.Count > MaxEntries)
			Entries.RemoveAt(0);
	}
}
