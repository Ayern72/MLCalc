using MLCalc.LinearAlgebra;

namespace MLCalc.Interpreter;

/// <summary>
/// Settings for an <see cref="MLCalcInterpreter"/>: where script files come from and how large a
/// program may grow. The defaults suit an application on the user's own computer, except that
/// script files are off until <see cref="Scripts"/> is set.
/// </summary>
public sealed record InterpreterOptions
{
	/// <summary>The default settings.</summary>
	public static InterpreterOptions Default { get; } = new();

	/// <summary>
	/// Where script files are read from: by <see cref="MLCalcInterpreter.ExecuteFile"/>, by
	/// <c>run("file.m")</c> and by input that is just a file name. Null (the default): there are no
	/// script files, and those three report an error; <see cref="MLCalcInterpreter.Execute(string, CancellationToken)"/> still
	/// runs the text of a script. <see cref="FileScriptSource.Instance"/> reads them from disk.
	/// </summary>
	public IScriptSource? Scripts { get; init; }

	/// <summary>The seed for <c>rand</c>, for repeatable results; null gives a different sequence each time.</summary>
	public int? RandomSeed { get; init; }

	/// <summary>How deeply scripts may run other scripts. The default is 32.</summary>
	public int MaxScriptDepth
	{
		get;
		init
		{
			ArgumentOutOfRangeException.ThrowIfNegative(value);
			field = value;
		}
	} = 32;

	/// <summary>
	/// The largest number of elements in one matrix or range while a program runs; at most
	/// <see cref="ComplexMatrix.MaxElements"/> (25 million, which takes 400 MB), the default.
	/// </summary>
	public int MaxMatrixElements
	{
		get;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, ComplexMatrix.MaxElements);
			field = value;
		}
	} = ComplexMatrix.MaxElements;
}

/// <summary>
/// Supplies the text of script files to an interpreter, so that the host decides whether and
/// where programs may read them.
/// </summary>
public interface IScriptSource
{
	/// <summary>
	/// The text of the script at <paramref name="fullPath"/>, or null when there is no such script.
	/// May throw <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>, which the
	/// interpreter reports as an error in the program.
	/// </summary>
	string? Read(string fullPath);
}

/// <summary>Reads scripts from the file system.</summary>
public sealed class FileScriptSource : IScriptSource
{
	/// <summary>The one instance.</summary>
	public static FileScriptSource Instance { get; } = new();

	FileScriptSource()
	{
	}

	/// <inheritdoc/>
	/// <remarks>A folder with the script's name is read too, so that the error says why it cannot be.</remarks>
	public string? Read(string fullPath) =>
		File.Exists(fullPath) || Directory.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
}
