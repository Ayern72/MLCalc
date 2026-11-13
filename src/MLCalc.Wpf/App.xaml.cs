using System.Windows;
using System.Windows.Threading;

namespace MLCalc.Wpf;

/// <summary>
/// Starts the main window. Command line: <c>MLCalc [--run script.m]</c> runs a script at startup.
/// </summary>
public partial class App : Application
{
	void OnStartup(object sender, StartupEventArgs e)
	{
		DispatcherUnhandledException += OnUnhandledException;

		string? script = null;
		for (int i = 0; i < e.Args.Length; i++)
		{
			if (e.Args[i] is "--run" or "-r" && i + 1 < e.Args.Length)
				script = e.Args[++i];
			else if (e.Args[i].EndsWith(".m", StringComparison.OrdinalIgnoreCase))
				script = e.Args[i];
		}
		new MainWindow(string.IsNullOrWhiteSpace(script) ? null : script).Show();
	}

	/// <summary>
	/// A last resort: errors in programs are reported in the console, so this only catches bugs in
	/// the application itself. Show the problem and keep running rather than closing the window.
	/// </summary>
	void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		MessageBox.Show(MainWindow, $"An unexpected error occurred:\n\n{e.Exception.Message}", "MLCalc",
			MessageBoxButton.OK, MessageBoxImage.Error);
		e.Handled = true;
	}
}
