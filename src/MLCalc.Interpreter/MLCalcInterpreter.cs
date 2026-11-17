using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MLCalc.Language;
using MLCalc.LinearAlgebra;
using MLCalc.Mathematics;

namespace MLCalc.Interpreter;

/// <summary>
/// Runs MLCalc programs: console input or <c>.m</c> scripts. Variables persist between calls.
/// Names (variables, functions, commands, keywords) are case-insensitive, as in the original.
/// </summary>
/// <remarks>
/// An interpreter is not thread-safe: it holds the variables and the state of the running program,
/// so use each instance from one thread at a time. Several interpreters can run at the same time on
/// different threads; they share nothing. A program runs on the thread that calls
/// <see cref="Execute(string, CancellationToken)"/>, and stops early when its cancellation token is cancelled.
/// </remarks>
public sealed partial class MLCalcInterpreter
{
	readonly Dictionary<string, ComplexMatrix> variables = new(StringComparer.OrdinalIgnoreCase);
	readonly Builtins builtins;
	Dictionary<string, ComplexMatrix>? locals;
	CancellationToken cancellation;
	int scriptDepth;

	/// <summary>The token of the running program, for operations that can run for a long time.</summary>
	internal CancellationToken Cancellation => cancellation;

	/// <summary>Creates an interpreter that writes results to <paramref name="output"/>.</summary>
	/// <param name="output">Receives the values and messages that programs show.</param>
	/// <param name="options">Script files and limits; <see cref="InterpreterOptions.Default"/> when null.</param>
	public MLCalcInterpreter(IConsoleOutput output, InterpreterOptions? options = null)
	{
		Output = output;
		Options = options ?? InterpreterOptions.Default;
		Random = Options.RandomSeed is { } seed ? new Random(seed) : new Random();
		builtins = new Builtins();
	}

	/// <summary>Where the values and messages of programs go.</summary>
	public IConsoleOutput Output { get; }

	/// <summary>The settings given when the interpreter was created.</summary>
	public InterpreterOptions Options { get; }

	internal Random Random { get; }

	/// <summary>
	/// The folder that relative paths are resolved against, e.g. in <c>run("file.m")</c>. While a
	/// script runs, it is the script's folder.
	/// </summary>
	public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;

	/// <summary>The variables, by name (case-insensitive).</summary>
	public IReadOnlyDictionary<string, ComplexMatrix> Variables => variables;

	/// <summary>Names of all built-in functions and commands, including those added by the host.</summary>
	public IEnumerable<string> BuiltinNames => builtins.Names;

	/// <summary>
	/// Adds a function that programs can call, e.g. <c>myfun(1, 2)</c>. It reads its arguments
	/// through the <see cref="CallContext"/> and returns a value, which becomes <c>ans</c> when
	/// the call stands alone. It reports errors by throwing <see cref="CallContext.Error"/>.
	/// </summary>
	/// <param name="name">A name that is not a keyword or an existing function or command.</param>
	/// <param name="function">Computes the result.</param>
	/// <param name="isPure">
	/// True if the result depends only on the arguments and the function has no side effects; a call
	/// with constant arguments inside a function given as text is then evaluated only once.
	/// </param>
	/// <exception cref="ArgumentException">The name is not valid or already in use.</exception>
	public void AddFunction(string name, Func<CallContext, ComplexMatrix> function, bool isPure = false)
	{
		ArgumentNullException.ThrowIfNull(function);
		builtins.Define(name, isPure ? BuiltinKind.PureFunction : BuiltinKind.Function,
			call => function(call) ?? throw new InvalidOperationException($"The function '{call.Name}' returned null."));
	}

	/// <summary>
	/// Adds a command: something a program does rather than computes, such as drawing or
	/// printing. Commands give no value and cannot be used in functions given as text.
	/// </summary>
	/// <param name="name">A name that is not a keyword or an existing function or command.</param>
	/// <param name="command">Carries out the command.</param>
	/// <exception cref="ArgumentException">The name is not valid or already in use.</exception>
	public void AddCommand(string name, Action<CallContext> command)
	{
		ArgumentNullException.ThrowIfNull(command);
		builtins.Define(name, BuiltinKind.Command, call =>
		{
			command(call);
			return null;
		});
	}

	/// <summary>Gets a variable; false if there is none with that name.</summary>
	public bool TryGetVariable(string name, out ComplexMatrix value) => variables.TryGetValue(name, out value!);

	/// <summary>Creates or replaces a variable.</summary>
	public void SetVariable(string name, ComplexMatrix value) => variables[name] = value;

	/// <summary>Deletes a variable; false if there was none with that name.</summary>
	public bool DeleteVariable(string name) => variables.Remove(name);

	/// <summary>Deletes all variables.</summary>
	public void ClearVariables() => variables.Clear();

	/// <summary>
	/// Parses and runs <paramref name="source"/>. If the source has syntax errors nothing is run.
	/// A line consisting only of a file name ending in <c>.m</c> runs that script, as in the original;
	/// without <see cref="InterpreterOptions.Scripts"/> such a line is an error.
	/// </summary>
	public ExecutionResult Execute(string source, CancellationToken cancellationToken = default)
	{
		if (ScriptFileName().Match(source.Trim()) is { Success: true } match)
		{
			if (Options.Scripts is null)
				return Failure(NoScriptFiles);
			var script = ReadScript(match.Value);
			if (script.Error is not null)
				return Failure(script.Error);
			if (script.Source is not null)
				return ExecuteScript(script.FullPath, script.Source, cancellationToken);
			// No such file: parse the line as usual, which reports what is wrong with it.
		}

		var parse = Parser.Parse(source);
		if (parse.HasErrors)
			return new ExecutionResult(parse, null);

		using var limit = ComplexMatrix.LimitElements(Options.MaxMatrixElements);
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

	const string NoScriptFiles = "Script files cannot be run here.";

	/// <summary>
	/// Runs a script file from <see cref="InterpreterOptions.Scripts"/>. Relative paths are resolved
	/// against <see cref="WorkingDirectory"/>.
	/// </summary>
	public ExecutionResult ExecuteFile(string path, CancellationToken cancellationToken = default)
	{
		if (Options.Scripts is null)
			return Failure(NoScriptFiles);
		var script = ReadScript(path);
		if (script.Error is not null)
			return Failure(script.Error);
		if (script.Source is null)
			return Failure($"Cannot find the script '{path}'.");
		return ExecuteScript(script.FullPath, script.Source, cancellationToken);
	}

	/// <summary>Reads a script: Source is null when there is none, and Error is set when it cannot be read.</summary>
	(string FullPath, string? Source, string? Error) ReadScript(string path)
	{
		string fullPath = ResolvePath(path);
		try
		{
			return (fullPath, Options.Scripts!.Read(fullPath), null);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return (fullPath, null, $"Cannot read '{path}': {ex.Message}");
		}
	}

	ExecutionResult ExecuteScript(string fullPath, string source, CancellationToken cancellationToken)
	{
		// Checked here so that it covers both run("file.m") and a script containing a file name;
		// a script that runs itself would otherwise recurse until the process crashes.
		if (scriptDepth >= Options.MaxScriptDepth)
			return Failure("Scripts are nested too deeply (does a script run itself?).");

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

	/// <summary>The full path of <paramref name="path"/>, relative to <see cref="WorkingDirectory"/>.</summary>
	public string ResolvePath(string path) => Path.GetFullPath(Path.Combine(WorkingDirectory, path));

	[GeneratedRegex(@"^[^\s""(),;=]+\.m$", RegexOptions.IgnoreCase)]
	private static partial Regex ScriptFileName();

	/// <summary>Runs a script from inside another program (<c>run("file.m")</c>), propagating its errors.</summary>
	internal void RunNestedScript(string path, SourceSpan span)
	{
		if (Options.Scripts is null)
			throw new RuntimeError(NoScriptFiles, span);
		var script = ReadScript(path);
		if (script.Error is not null)
			throw new RuntimeError(script.Error, span);
		if (script.Source is null)
			throw new RuntimeError($"Cannot find the script '{path}'.", span);

		var result = ExecuteScript(script.FullPath, script.Source, cancellation);
		cancellation.ThrowIfCancellationRequested();
		var message = result.Messages().FirstOrDefault();
		if (message is not null)
			throw new RuntimeError($"In {Path.GetFileName(script.FullPath)} at {message}", span);
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
		// A bare variable name displays the variable without changing ans.
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
		for (double k = start; k <= end + 1e-10 * Math.Max(1, Math.Abs(end)); k++)
		{
			variables[name] = k;
			Execute(statement.Body);
		}
	}

	/// <summary>
	/// A condition is true when it is non-empty and all its elements are non-zero. A NaN
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
