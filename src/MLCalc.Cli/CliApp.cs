using System.Reflection;
using MLCalc.Interpreter;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Plotting;

namespace MLCalc.Cli;

/// <summary>
/// The command-line front-end: an interactive prompt, or batch execution of scripts and
/// expressions. Works on any reader/writer so it can be tested without a console.
/// </summary>
internal sealed class CliApp : IConsoleOutput
{
	public const int Success = 0, Failure = 1, UsageError = 2;

	const string Usage = """
		Usage: mlcalc [options] [script.m ...]

		With no scripts or expressions, starts an interactive prompt. With input piped in,
		runs it as a script.

		Options:
		  -e, --eval <code>    Evaluate code (may be repeated; runs after the scripts)
		  -i, --interactive    Start the prompt after running scripts and expressions
		      --svg <file>     Save the plots to an SVG file when done
		      --size <w>x<h>   Size of the SVG image (default 800x600)
		  -h, --help           Show this help
		      --version        Show the version

		Examples:
		  mlcalc FEIGENBA.m --svg feigenbaum.svg
		  mlcalc -e "A = [1 2; 3 4]" -e "inv(A)"
		  echo "integral(\"x\", \"x^2\", 0, 1)" | mlcalc
		""";

	readonly TextReader input;
	readonly TextWriter output;
	readonly TextWriter error;
	readonly bool isConsole;
	readonly MLCalcInterpreter interpreter;
	readonly Lock gate = new();
	CancellationTokenSource? running;

	public CliApp(TextReader input, TextWriter output, TextWriter error, bool isConsole)
	{
		this.input = input;
		this.output = output;
		this.error = error;
		this.isConsole = isConsole;
		interpreter = new MLCalcInterpreter(Figure, this);
	}

	public Figure Figure { get; } = new();

	public static string Version =>
		typeof(CliApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

	/// <summary>Stops the running program (Ctrl+C). Returns false when nothing is running.</summary>
	public bool CancelRunning()
	{
		lock (gate)
		{
			if (running is null)
				return false;
			running.Cancel();
			return true;
		}
	}

	public int Run(string[] args, bool inputRedirected)
	{
		var scripts = new List<string>();
		var expressions = new List<string>();
		string? svg = null;
		(double Width, double Height) size = (800, 600);
		bool interactive = false;

		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i];
			string? Value()
			{
				if (i + 1 < args.Length)
					return args[++i];
				error.WriteLine($"mlcalc: {arg} needs a value.");
				return null;
			}
			switch (arg)
			{
				case "-h" or "--help" or "/?":
					output.WriteLine(Usage);
					return Success;
				case "--version":
					output.WriteLine($"mlcalc {Version}");
					return Success;
				case "-i" or "--interactive":
					interactive = true;
					break;
				case "-e" or "--eval":
					if (Value() is not { } code)
						return UsageError;
					expressions.Add(code);
					break;
				case "--svg":
					if ((svg = Value()) is null)
						return UsageError;
					break;
				case "--size":
					if (Value() is not { } text || !TryParseSize(text, out size))
					{
						error.WriteLine("mlcalc: --size expects WIDTHxHEIGHT, e.g. 1200x800.");
						return UsageError;
					}
					break;
				default:
					if (arg.StartsWith('-') && arg.Length > 1)
					{
						error.WriteLine($"mlcalc: unknown option '{arg}'. Try 'mlcalc --help'.");
						return UsageError;
					}
					scripts.Add(arg);
					break;
			}
		}

		bool batch = scripts.Count > 0 || expressions.Count > 0;
		int status = Success;
		foreach (var script in scripts)
		{
			if (!File.Exists(script))
			{
				error.WriteLine($"mlcalc: cannot find '{script}'.");
				return Failure;
			}
			if (!Report(Execute(token => interpreter.ExecuteFile(Path.GetFullPath(script), token)), Path.GetFileName(script)))
			{
				status = Failure;
				break;
			}
		}
		if (status == Success)
			foreach (var code in expressions)
				if (!Report(Execute(token => interpreter.Execute(code, token)), "-e"))
				{
					status = Failure;
					break;
				}

		if (!batch && inputRedirected)
		{
			string source = input.ReadToEnd();
			batch = true;
			if (!Report(Execute(token => interpreter.Execute(source, token)), "stdin"))
				status = Failure;
		}

		if (interactive || !batch)
			Repl();

		if (svg is not null && !SaveSvg(svg, size))
			status = Failure;
		return status;
	}

	static bool TryParseSize(string text, out (double Width, double Height) size)
	{
		var parts = text.Split('x', 'X', '×');
		size = default;
		if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out double w)
			&& double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out double h) && w >= 50 && h >= 50)
		{
			size = (w, h);
			return true;
		}
		return false;
	}

	bool SaveSvg(string path, (double Width, double Height) size)
	{
		try
		{
			SvgWriter.Save(Figure.Snapshot(), path, size.Width, size.Height);
			error.WriteLine($"Saved {Path.GetFullPath(path)}");
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			error.WriteLine($"mlcalc: cannot save '{path}': {ex.Message}");
			return false;
		}
	}

	ExecutionResult Execute(Func<CancellationToken, ExecutionResult> run)
	{
		using var cancellation = new CancellationTokenSource();
		lock (gate)
			running = cancellation;
		try
		{
			return run(cancellation.Token);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// The interpreter reports errors itself; this is a last resort so that an unexpected
			// exception is shown as an error instead of ending the program.
			return new ExecutionResult(Parser.Parse(""), MLCalcInterpreter.InternalError(ex, default));
		}
		finally
		{
			lock (gate)
				running = null;
		}
	}

	/// <summary>Writes errors as "source:line:column: message". Returns true on success.</summary>
	bool Report(ExecutionResult result, string? source)
	{
		foreach (var message in result.Messages())
			error.WriteLine(source is null ? message : $"{source}:{message}");
		return result.Success;
	}

	// ───────────── Interactive prompt ─────────────

	void Repl()
	{
		output.WriteLine($"MLCalc {Version}. Type an expression, 'help' for hints, or 'exit' to quit.");
		var pending = new List<string>();
		long figureVersion = Figure.Snapshot().Version;
		bool savefigHintShown = false;

		while (true)
		{
			output.Write(pending.Count > 0 ? "... " : ">> ");
			output.Flush();
			string? line = input.ReadLine();
			if (line is null)
			{
				output.WriteLine();
				break;
			}

			if (pending.Count == 0)
			{
				string command = line.Trim();
				if (command is "exit" or "quit")
					break;
				if (command == "help")
				{
					output.WriteLine(Help);
					continue;
				}
			}

			pending.Add(line);
			string source = string.Join(Environment.NewLine, pending);
			if (string.IsNullOrWhiteSpace(source))
			{
				pending.Clear();
				continue;
			}
			// Keep reading while the input is unfinished; an empty line forces evaluation.
			if (!string.IsNullOrWhiteSpace(line) && Parser.Parse(source).IsIncomplete)
				continue;
			pending.Clear();

			Report(Execute(token => interpreter.Execute(source, token)), null);

			long version = Figure.Snapshot().Version;
			if (version != figureVersion && !savefigHintShown)
			{
				output.WriteLine("(The plot changed. Save it with savefig(\"plot.svg\").)");
				savefigHintShown = true;
			}
			figureVersion = version;
		}
	}

	const string Help = """
		  A = [1 2; 3 4]          matrices; * / ^ are matrix operations, .* ./ .^ element-wise
		  z = (1 + 2i)^2          complex numbers; ';' at the end hides the result
		  x = 0:0.1:1             ranges; x(2), A(1,:) index from 1
		  if x > 0 … elseif … else … end      MATLAB-style blocks: if, while, for k = 1:9 … end
		  while k < 3 do begin … end         original style: if … then …, while … do, for … do
		  solve("x", "x^2-2", 1)  numerics: integral, derivata, solve, isect, extreme, feval
		  fplot("x", "sin(x)")    plots: plot, fplot, polyplot, dot, xrange, yrange, initdisplay
		  savefig("plot.svg")     save the plots
		  run("script.m")         run a script (or just type script.m)
		  list, del(x), cls       variables and console
		  Ctrl+C stops a running program; exit quits.
		""";

	// ───────────── IConsoleOutput ─────────────

	void IConsoleOutput.WriteValue(string name, ComplexMatrix value) => output.WriteLine(ResultFormatter.Format(name, value));

	void IConsoleOutput.WriteLine(string text) => output.WriteLine(text);

	void IConsoleOutput.Clear()
	{
		if (isConsole)
			Console.Clear();
	}
}
