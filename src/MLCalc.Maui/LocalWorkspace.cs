using MLCalc.Hosting;
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
		Workspace = new Workspace(new WorkspaceOptions(), () => Volatile.Read(ref client));
		Workspace.Interpreter.WorkingDirectory = FileSystem.AppDataDirectory;
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
