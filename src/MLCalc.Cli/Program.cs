using System.Text;
using MLCalc.Cli;

Console.OutputEncoding = Encoding.UTF8;
var app = new CliApp(Console.In, Console.Out, Console.Error, isConsole: !Console.IsOutputRedirected);

// Ctrl+C stops a running program; when nothing is running it exits as usual.
Console.CancelKeyPress += (_, e) =>
{
	if (app.CancelRunning())
		e.Cancel = true;
};

return app.Run(args, Console.IsInputRedirected);
