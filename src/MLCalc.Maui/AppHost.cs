using MLCalc.Web.Ui;

namespace MLCalc.Maui;

/// <summary>
/// The page's scripts folder, files and clipboard, with MAUI's cross-platform APIs (file picker,
/// share sheet, clipboard), which work the same on iOS.
/// </summary>
sealed class AppHost : IAppHost
{
	public IReadOnlyList<string> Scripts() => ScriptsFolder.Files();

	public async Task<string?> ImportScriptAsync()
	{
		var picked = await MainThread.InvokeOnMainThreadAsync(() => FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Open a script" }));
		if (picked is null)
			return null;
		// The name the picker gives, without any folders (some file providers put a path in it).
		string name = Path.GetFileName(picked.FileName);
		await using (var source = await picked.OpenReadAsync())
		await using (var target = File.Create(ScriptsFolder.PathOf(name)))
			await source.CopyToAsync(target);
		return name;
	}

	public Task ShareScriptAsync(string name) =>
		ShareAsync(name, new ShareFile(ScriptsFolder.PathOf(name), "text/plain"));

	public void DeleteScript(string name) => File.Delete(ScriptsFolder.PathOf(name));

	public async Task ShareFileAsync(string fileName, string mediaType, byte[] content)
	{
		// Shared from the cache, which Android may empty: the receiving app takes its own copy.
		string path = Path.Combine(FileSystem.CacheDirectory, fileName);
		await File.WriteAllBytesAsync(path, content);
		await ShareAsync(fileName, new ShareFile(path, mediaType));
	}

	public Task SaveFileAsync(string path, byte[] content)
	{
		if (!Path.GetFullPath(path).StartsWith(ScriptsFolder.Path + Path.DirectorySeparatorChar, StringComparison.Ordinal))
			throw new UnauthorizedAccessException("Files are saved in the scripts folder only.");
		return File.WriteAllBytesAsync(path, content);
	}

	public Task CopyTextAsync(string text) => MainThread.InvokeOnMainThreadAsync(() => Clipboard.Default.SetTextAsync(text));

	static Task ShareAsync(string title, ShareFile file) =>
		MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = file }));
}
