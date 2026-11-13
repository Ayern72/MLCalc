using System.Windows;

namespace MLCalc.Wpf;

/// <summary>
/// Starts the main window. Command line: <c>MLCalc [--run script.m]</c> runs a script at startup.
/// </summary>
public partial class App : Application
{
	void OnStartup(object sender, StartupEventArgs e)
	{
		string? script = null;
		for (int i = 0; i < e.Args.Length; i++)
		{
			if (e.Args[i] is "--run" or "-r" && i + 1 < e.Args.Length)
				script = e.Args[++i];
			else if (e.Args[i].EndsWith(".m", StringComparison.OrdinalIgnoreCase))
				script = e.Args[i];
		}
		new MainWindow(script).Show();
	}
}
