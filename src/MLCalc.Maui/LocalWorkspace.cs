using MLCalc.Hosting;
using MLCalc.Plotting;
using MLCalc.Web.Protocol;

namespace MLCalc.Maui;

/// <summary>
/// The app's one workspace, with no limits, as in the desktop app. Its messages go to the page
/// attached last; while there is none (Android is making the window anew) they are dropped, and the
/// next page gets the whole figure in its <c>Welcome</c>.
/// </summary>
public sealed class LocalWorkspace : IDisposable
{
	IMLCalcClient? client;
	bool attachedBefore;

	public LocalWorkspace()
	{
		ScriptsFolder.Create();
		Workspace = new Workspace(new WorkspaceOptions
		{
			// Scripts from the scripts folder, by name or with run("file.m"), as in the desktop app.
			Interpreter = new() { Scripts = new CaseInsensitiveScripts() },
		}, () => Volatile.Read(ref client));
		Workspace.Interpreter.WorkingDirectory = ScriptsFolder.Path;
		// savefig("plot.svg") writes into the scripts folder (relative to the working directory),
		// and says so without the long path of the app's storage.
		Workspace.Plotting.RegisterFigureFormat(".svg", (snapshot, path, width, height) =>
		{
			File.WriteAllText(path, SvgWriter.Render(snapshot, width, height));
			return $"Saved {Path.GetFileName(path)}.";
		});
		// savefig("plot.png"): the page draws the PNG from SVG, as only the web view can, and
		// writes it at the path given here.
		Workspace.Plotting.RegisterFigureFormat(".png", (snapshot, path, width, height) =>
		{
			Workspace.Download(new DownloadMessage(path, "image/png", SvgWriter.Render(snapshot, width, height)));
			return $"Saved {Path.GetFileName(path)}.";
		});
	}

	public Workspace Workspace { get; }

	/// <summary>Sends the messages to <paramref name="page"/> from now on; true the first time, when the workspace is new.</summary>
	public bool Attach(IMLCalcClient page)
	{
		Volatile.Write(ref client, page);
		bool isNew = !attachedBefore;
		attachedBefore = true;
		return isNew;
	}

	/// <summary>Stops sending to <paramref name="page"/>, unless another page has taken over.</summary>
	public void Detach(IMLCalcClient page) => Interlocked.CompareExchange(ref client, null, page);

	public void Dispose() => Workspace.Dispose();
}
