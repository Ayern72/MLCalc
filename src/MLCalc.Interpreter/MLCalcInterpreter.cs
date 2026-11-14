using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Math;
using MLCalc.Plotting;

namespace MLCalc.Interpreter;

/// <summary>
/// Runs MLCalc programs: console input or <c>.m</c> scripts. Variables persist between calls.
/// Names (variables, functions, commands, keywords) are case-insensitive, as in the original.
/// </summary>
public sealed partial class MLCalcInterpreter
{
	readonly Dictionary<string, ComplexMatrix> variables = new(StringComparer.OrdinalIgnoreCase);
	readonly Builtins builtins;
	Dictionary<string, ComplexMatrix>? locals;
	CancellationToken cancellation;
	int scriptDepth;

	/// <summary>The token of the running program, for operations that can run for a long time.</summary>
	internal CancellationToken Cancellation => cancellation;

	public MLCalcInterpreter(Figure figure, IConsoleOutput output, int? randomSeed = null)
	{
		Figure = figure;
		Output = output;
		Random = randomSeed is { } seed ? new Random(seed) : new Random();
		builtins = new Builtins();
	}

	public Figure Figure { get; }
	public IConsoleOutput Output { get; }
	internal Random Random { get; }

	/// <summary>Folder that relative script paths are resolved against.</summary>
	public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;

	public IReadOnlyDictionary<string, ComplexMatrix> Variables => variables;

	/// <summary>Names of all built-in functions and commands.</summary>
	public IEnumerable<string> BuiltinNames => builtins.Names;

	readonly Dictionary<string, FigureSaver> figureFormats = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Saves a figure to a file: (figure, full path, width, height).</summary>
	public delegate void FigureSaver(FigureSnapshot figure, string path, double width, double height);

	/// <summary>
	/// Lets a front-end add an image format for <c>savefig</c>, e.g. ".png" in the desktop app.
	/// SVG is always available. The saver is called on the interpreter's thread.
	/// </summary>
	public void RegisterFigureFormat(string extension, FigureSaver saver)
	{
		if (!extension.StartsWith('.'))
			extension = "." + extension;
		figureFormats[extension] = saver;
	}

	internal bool TryGetFigureFormat(string extension, out FigureSaver saver) => figureFormats.TryGetValue(extension, out saver!);

	internal IEnumerable<string> FigureFormats => figureFormats.Keys.Prepend(".svg").Distinct(StringComparer.OrdinalIgnoreCase);

	public bool TryGetVariable(string name, out ComplexMatrix value) => variables.TryGetValue(name, out value!);

	public void SetVariable(string name, ComplexMatrix value) => variables[name] = value;

	public bool DeleteVariable(string name) => variables.Remove(name);

	public void ClearVariables() => variables.Clear();

	/// <summary>
	/// Parses and runs <paramref name="source"/>. If the source has syntax errors nothing is run.
	/// A line consisting only of a file name ending in <c>.m</c> runs that script, as in the original.
	/// </summary>
	public ExecutionResult Execute(string source, CancellationToken cancellationToken = default)
	{
		if (ScriptFileName().Match(source.Trim()) is { Success: true } match && File.Exists(ResolvePath(match.Value)))
			return ExecuteFile(match.Value, cancellationToken);

		var parse = Parser.Parse(source);
		if (parse.HasErrors)
			return new ExecutionResult(parse, null);

		var previous = cancellation;
		cancellation = cancellationToken;
		try
		{
			foreach (var statement in parse.Root.Statements)
				Execute(statement);
			return new ExecutionResult(parse, null);
		}
		catch (RuntimeError error)
		{
			return new ExecutionResult(parse, error);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new ExecutionResult(parse, null, Cancelled: true);
		}
		finally
		{
			cancellation = previous;
		}
	}

	/// <summary>How deeply scripts may run other scripts (with <c>run</c> or by typing a file name).</summary>
	public const int MaxScriptDepth = 32;

	/// <summary>Runs a script file. Relative paths are resolved against <see cref="WorkingDirectory"/>.</summary>
	public ExecutionResult ExecuteFile(string path, CancellationToken cancellationToken = default)
	{
		string fullPath = ResolvePath(path);
		// Checked here so that it covers both run("file.m") and a script containing a file name;
		// a script that runs itself would otherwise recurse until the process crashes.
		if (scriptDepth >= MaxScriptDepth)
			return Failure("Scripts are nested too deeply (does a script run itself?).");

		string source;
		try
		{
			source = File.ReadAllText(fullPath);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return Failure($"Cannot read '{path}': {ex.Message}");
		}

		var previousDirectory = WorkingDirectory;
		WorkingDirectory = Path.GetDirectoryName(fullPath) ?? WorkingDirectory;
		scriptDepth++;
		try
		{
			return Execute(source, cancellationToken);
		}
		finally
		{
			scriptDepth--;
			WorkingDirectory = previousDirectory;
		}
	}

	/// <summary>A result for an error that happens before any code is parsed.</summary>
	static ExecutionResult Failure(string message) => new(Parser.Parse(""), new RuntimeError(message, default));

	internal string ResolvePath(string path) => Path.GetFullPath(Path.Combine(WorkingDirectory, path));

	[GeneratedRegex(@"^[^\s""(),;=]+\.m$", RegexOptions.IgnoreCase)]
	private static partial Regex ScriptFileName();

	/// <summary>Runs a script from inside another program (<c>run("file.m")</c>), propagating its errors.</summary>
	internal void RunNestedScript(string path, SourceSpan span)
	{
		string fullPath = ResolvePath(path);
		if (!File.Exists(fullPath))
			throw new RuntimeError($"Cannot find the script '{path}'.", span);

		var result = ExecuteFile(fullPath, cancellation);
		cancellation.ThrowIfCancellationRequested();
		var message = result.Messages().FirstOrDefault();
		if (message is not null)
			throw new RuntimeError($"In {Path.GetFileName(fullPath)} at {message}", span);
	}

	// ───────────── Statements ─────────────

	/// <summary>
	/// Runs a statement. Any unexpected exception (one that is not already a <see cref="RuntimeError"/>
	/// or a cancellation) is reported as an error at this statement instead of ending the program.
	/// </summary>
	void Execute(Statement statement)
	{
		try
		{
			RuntimeHelpers.EnsureSufficientExecutionStack();
			ExecuteCore(statement);
		}
		catch (Exception ex) when (ex is not RuntimeError and not OperationCanceledException)
		{
			throw InternalError(ex, statement.Span);
		}
	}

	/// <summary>The error to report for an unexpected exception: running out of stack or memory, or a bug.</summary>
	public static RuntimeError InternalError(Exception exception, SourceSpan span) => exception switch
	{
		InsufficientExecutionStackException => new RuntimeError("The program is nested too deeply.", span, exception),
		OutOfMemoryException => new RuntimeError("There is not enough memory for this calculation.", span, exception),
		_ => new RuntimeError($"Internal error ({exception.GetType().Name}): {exception.Message}", span, exception),
	};

	void ExecuteCore(Statement statement)
	{
		cancellation.ThrowIfCancellationRequested();
		switch (statement)
		{
			case ExpressionStatement s:
				ExecuteExpressionStatement(s);
				break;
			case AssignmentStatement s:
				ExecuteAssignment(s);
				break;
			case IfStatement s:
				if (IsTrue(s.Condition))
					Execute(s.Then);
				else if (s.Else is not null)
					Execute(s.Else);
				break;
			case WhileStatement s:
				while (IsTrue(s.Condition))
				{
					cancellation.ThrowIfCancellationRequested();
					Execute(s.Body);
				}
				break;
			case ForStatement s:
				ExecuteFor(s);
				break;
			case BlockStatement s:
				foreach (var inner in s.Statements)
					Execute(inner);
				break;
			default:
				throw new RuntimeError($"Unsupported statement {statement.GetType().Name}.", statement.Span);
		}
	}

	void ExecuteExpressionStatement(ExpressionStatement statement)
	{
		// A bare variable name displays the variable without changing ans, as in MATLAB.
		if (statement.Expression is Identifier id && variables.TryGetValue(id.Name, out var existing))
		{
			if (!statement.IsSilent)
				Output.WriteValue(id.Name, existing);
			return;
		}

		var value = EvaluateStatement(statement.Expression);
		if (value is null)
			return;
		variables["ans"] = value;
		if (!statement.IsSilent)
			Output.WriteValue("ans", value);
	}

	void ExecuteAssignment(AssignmentStatement statement)
	{
		var value = Evaluate(statement.Value);
		string name = statement.Target.Name;
		if (statement.Indices.IsEmpty)
			variables[name] = value;
		else
		{
			variables.TryGetValue(name, out var current);
			variables[name] = Guard(statement.Span, () => Indexing.Assign(current, statement.Indices.Select(EvaluateIndex).ToArray(), value));
		}
		if (!statement.IsSilent)
			Output.WriteValue(name, variables[name]);
	}

	void ExecuteFor(ForStatement statement)
	{
		string name = statement.Variable.Name;
		var first = Evaluate(statement.Start);
		if (statement.End is null)
		{
			// for k = matrix do: iterate over the columns.
			for (int c = 0; c < first.Columns; c++)
			{
				variables[name] = first.Column(c);
				Execute(statement.Body);
			}
			return;
		}

		double start = RealScalar(first, statement.Start.Span, "The start of a for loop");
		double end = RealScalar(Evaluate(statement.End), statement.End.Span, "The end of a for loop");
		for (double k = start; k <= end + 1e-10 * System.Math.Max(1, System.Math.Abs(end)); k++)
		{
			variables[name] = k;
			Execute(statement.Body);
		}
	}

	/// <summary>
	/// A condition is true when it is non-empty and all its elements are non-zero (MATLAB rule). A NaN
	/// element is an error, wherever it is.
	/// </summary>
	bool IsTrue(Expression condition)
	{
		var value = Evaluate(condition);
		bool result = !value.IsEmpty;
		foreach (var element in value.AsSpan())
		{
			if (Complex.IsNaN(element))
				throw new RuntimeError("The condition is NaN, which is neither true nor false.", condition.Span);
			if (element == Complex.Zero)
				result = false;
		}
		return result;
	}

	internal static double RealScalar(ComplexMatrix value, SourceSpan span, string what)
	{
		if (!value.IsScalar)
			throw new RuntimeError($"{what} must be a scalar, got a {value.Rows}×{value.Columns} matrix.", span);
		if (!ComplexMath.IsReal(value.ScalarValue))
			throw new RuntimeError($"{what} must be real.", span);
		return value.ScalarValue.Real;
	}

	/// <summary>Runs <paramref name="action"/>, turning math and argument errors into a <see cref="RuntimeError"/> at <paramref name="span"/>.</summary>
	internal static T Guard<T>(SourceSpan span, Func<T> action)
	{
		try
		{
			return action();
		}
		catch (Exception ex) when (ToRuntimeError(ex, span) is { } error)
		{
			throw error;
		}
	}

	/// <summary>
	/// The runtime error to report for a math or argument error at <paramref name="span"/>; null for
	/// other exceptions, which are not the user's mistake and propagate unchanged.
	/// </summary>
	internal static RuntimeError? ToRuntimeError(Exception exception, SourceSpan span) => exception switch
	{
		MathException math => new RuntimeError(math.Message, span, math),
		// .NET appends " (Parameter 'name')" to the message; users should not see that.
		ArgumentException argument => new RuntimeError(
			argument.ParamName is null ? argument.Message : argument.Message.Replace($" (Parameter '{argument.ParamName}')", ""),
			span, argument),
		_ => null,
	};

	internal static void Guard(SourceSpan span, Action action) => Guard(span, () =>
	{
		action();
		return 0;
	});
}
