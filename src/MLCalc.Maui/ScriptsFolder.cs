using System.Reflection;
using MLCalc.Interpreter;

namespace MLCalc.Maui;

/// <summary>
/// The app's scripts folder, in the app's own storage: the working directory of programs, so that a
/// script is run by typing its name and <c>savefig</c> writes there. On the first start it gets the
/// sample scripts of the Amiga original, which come with the app.
/// </summary>
public static class ScriptsFolder
{
	/// <summary>The folder.</summary>
	public static string Path { get; } = System.IO.Path.Combine(FileSystem.AppDataDirectory, "scripts");

	/// <summary>Makes the folder, with the samples, unless it is there already (so a deleted sample stays deleted).</summary>
	public static void Create()
	{
		if (Directory.Exists(Path))
			return;
		Directory.CreateDirectory(Path);
		var assembly = typeof(ScriptsFolder).Assembly;
		foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith("samples/", StringComparison.Ordinal)))
		{
			using var sample = assembly.GetManifestResourceStream(resource)!;
			using var file = File.Create(System.IO.Path.Combine(Path, resource["samples/".Length..]));
			sample.CopyTo(file);
		}
	}

	/// <summary>The scripts and other files in the folder, by name.</summary>
	public static IReadOnlyList<string> Files() =>
		[.. Directory.EnumerateFiles(Path).Select(System.IO.Path.GetFileName).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)];

	/// <summary>The full path of <paramref name="name"/> in the folder; throws for a name that would lead outside it.</summary>
	public static string PathOf(string name)
	{
		if (name != System.IO.Path.GetFileName(name) || name is "" or "." or "..")
			throw new ArgumentException($"'{name}' is not a file name.", nameof(name));
		return System.IO.Path.Combine(Path, name);
	}
}

/// <summary>
/// Reads scripts from files, finding them whatever the case of their names, as on Windows and the
/// Amiga: the samples are called FEIGENBA.m and the like, and Android's file names are case-sensitive.
/// </summary>
sealed class CaseInsensitiveScripts : IScriptSource
{
	public string? Read(string fullPath)
	{
		if (File.Exists(fullPath))
			return File.ReadAllText(fullPath);
		string? folder = System.IO.Path.GetDirectoryName(fullPath);
		string name = System.IO.Path.GetFileName(fullPath);
		if (folder is null || !Directory.Exists(folder))
			return null;
		string? match = Directory.EnumerateFiles(folder)
			.FirstOrDefault(file => string.Equals(System.IO.Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase));
		return match is null ? null : File.ReadAllText(match);
	}
}
