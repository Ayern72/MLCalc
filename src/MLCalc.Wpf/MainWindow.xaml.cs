using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MLCalc.Wpf;

public partial class MainWindow : Window
{
	public static readonly RoutedUICommand RunScriptCommand = new("Run Script", nameof(RunScriptCommand), typeof(MainWindow));
	public static readonly RoutedUICommand ExportPlotCommand = new("Export Plot", nameof(ExportPlotCommand), typeof(MainWindow));
	public static readonly RoutedUICommand CopyPlotCommand = new("Copy Plot", nameof(CopyPlotCommand), typeof(MainWindow));

	/// <summary>PNG exports use twice the on-screen resolution so they stay sharp in documents.</summary>
	const double PngScale = 2;

	string? lastExportDirectory;

	readonly MainViewModel viewModel;
	bool scrollPending;

	public MainWindow(string? startupScript = null)
	{
		InitializeComponent();
		viewModel = new MainViewModel(Dispatcher);
		DataContext = viewModel;

		Plot.CoordinatesChanged += (_, text) => viewModel.Coordinates = text;
		viewModel.Entries.CollectionChanged += OnEntriesChanged;
		Loaded += async (_, _) =>
		{
			InputBox.Focus();
			if (startupScript is not null)
				await viewModel.RunScriptAsync(Path.GetFullPath(startupScript));
		};
	}

	/// <summary>Keeps the newest output visible; batched so long-running scripts stay responsive.</summary>
	void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (scrollPending || e.Action != NotifyCollectionChangedAction.Add)
			return;
		scrollPending = true;
		Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
		{
			scrollPending = false;
			if (viewModel.Entries.Count > 0)
				Output.ScrollIntoView(viewModel.Entries[^1]);
		});
	}

	void OnInputKeyDown(object sender, KeyEventArgs e)
	{
		switch (e.Key)
		{
			case Key.Enter:
				if (viewModel.SubmitCommand.CanExecute(null))
					viewModel.SubmitCommand.Execute(null);
				e.Handled = true;
				break;
			case Key.Up:
				viewModel.HistoryPrevious();
				InputBox.CaretIndex = InputBox.Text.Length;
				e.Handled = true;
				break;
			case Key.Down:
				viewModel.HistoryNext();
				InputBox.CaretIndex = InputBox.Text.Length;
				e.Handled = true;
				break;
			case Key.Escape:
				viewModel.Escape();
				e.Handled = true;
				break;
		}
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		// Esc stops a running script even when the input box does not have focus.
		if (e.Key == Key.Escape && viewModel.IsBusy)
		{
			viewModel.Escape();
			e.Handled = true;
		}
		base.OnPreviewKeyDown(e);
	}

	async void OnRunScript(object sender, ExecutedRoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Title = "Run MLCalc Script",
			Filter = "MLCalc scripts (*.m)|*.m|All files (*.*)|*.*",
			InitialDirectory = viewModel.WorkingDirectory,
		};
		if (dialog.ShowDialog(this) == true)
			await viewModel.RunScriptAsync(dialog.FileName);
		InputBox.Focus();
	}

	/// <summary>Saves the plot as PNG or SVG at the size it has on screen.</summary>
	void OnExportPlot(object sender, ExecutedRoutedEventArgs e)
	{
		var dialog = new SaveFileDialog
		{
			Title = "Export Plot",
			Filter = "PNG image (*.png)|*.png|SVG image (*.svg)|*.svg",
			FileName = "plot.png",
			InitialDirectory = lastExportDirectory ?? viewModel.WorkingDirectory,
		};
		if (dialog.ShowDialog(this) != true)
			return;
		lastExportDirectory = Path.GetDirectoryName(dialog.FileName);
		try
		{
			PlotExporter.Save(viewModel.Figure.Snapshot(), dialog.FileName, Plot.ActualWidth, Plot.ActualHeight, PngScale);
			viewModel.Report(ConsoleEntryKind.Info, $"Saved {dialog.FileName}");
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
		{
			viewModel.Report(ConsoleEntryKind.Error, $"Could not save the plot: {ex.Message}");
		}
		InputBox.Focus();
	}

	/// <summary>Copies the plot to the clipboard as an image, at the screen's resolution.</summary>
	void OnCopyPlot(object sender, ExecutedRoutedEventArgs e)
	{
		try
		{
			double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
			Clipboard.SetImage(PlotExporter.Render(viewModel.Figure.Snapshot(), Plot.ActualWidth, Plot.ActualHeight, scale));
			viewModel.Report(ConsoleEntryKind.Info, "The plot was copied to the clipboard.");
		}
		catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.COMException)
		{
			viewModel.Report(ConsoleEntryKind.Error, $"Could not copy the plot: {ex.Message}");
		}
	}

	void OnDragOver(object sender, DragEventArgs e)
	{
		e.Effects = ScriptFrom(e) is not null && !viewModel.IsBusy ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	async void OnDrop(object sender, DragEventArgs e)
	{
		if (ScriptFrom(e) is { } path)
			await viewModel.RunScriptAsync(path);
	}

	static string? ScriptFrom(DragEventArgs e) =>
		e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files && files[0].EndsWith(".m", StringComparison.OrdinalIgnoreCase)
			? files[0]
			: null;

	void OnCanCopy(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Output.Items.Count > 0;

	/// <summary>Copies the selected console lines, or all of them when nothing is selected.</summary>
	void OnCopy(object sender, ExecutedRoutedEventArgs e)
	{
		var lines = (Output.SelectedItems.Count > 0 ? Output.SelectedItems.Cast<ConsoleEntry>() : viewModel.Entries)
			.OrderBy(viewModel.Entries.IndexOf)
			.Select(entry => entry.Text);
		Clipboard.SetText(string.Join(Environment.NewLine, lines));
	}

	void OnExit(object sender, RoutedEventArgs e) => Close();
}
