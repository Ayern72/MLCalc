namespace MLCalc.Web.Ui;

/// <summary>
/// What the app adds to the page, where programs run on the device as in the desktop app: a folder
/// of scripts, saving and sharing files, and the clipboard. The web has none, and the page then
/// shows none of it. Registered as a service; the page looks for it.
/// </summary>
public interface IAppHost
{
	/// <summary>The names of the scripts in the scripts folder, which is the working directory of programs.</summary>
	IReadOnlyList<string> Scripts();

	/// <summary>Lets the user pick a script anywhere on the device and copies it into the scripts folder; its name, or null when none was picked.</summary>
	Task<string?> ImportScriptAsync();

	/// <summary>Sends a script from the scripts folder to another app, such as mail or a file manager.</summary>
	Task ShareScriptAsync(string name);

	/// <summary>Deletes a script from the scripts folder.</summary>
	void DeleteScript(string name);

	/// <summary>Sends a file the page made, such as an exported plot, to another app, or saves it.</summary>
	Task ShareFileAsync(string fileName, string mediaType, byte[] content);

	/// <summary>Writes a file from <c>savefig</c> at <paramref name="path"/>, a full path in the scripts folder.</summary>
	Task SaveFileAsync(string path, byte[] content);

	/// <summary>Puts text on the clipboard.</summary>
	Task CopyTextAsync(string text);
}
