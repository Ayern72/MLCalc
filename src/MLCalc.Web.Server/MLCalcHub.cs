using MLCalc.Web.Protocol;
using Microsoft.AspNetCore.SignalR;

namespace MLCalc.Web.Server;

/// <summary>
/// The SignalR hub at <see cref="WebProtocol.Path"/>. Each connection belongs to one session, named
/// by the <c>?session=</c> query parameter when reconnecting. The methods return as soon as the
/// work has started; the results come as messages (see <see cref="IMLCalcClient"/>).
/// </summary>
sealed class MLCalcHub(SessionManager sessions) : Hub<IMLCalcClient>
{
	const string SessionKey = "session";

	public override async Task OnConnectedAsync()
	{
		string? requested = Context.GetHttpContext()?.Request.Query[WebProtocol.SessionParameter];
		await AttachAsync(string.IsNullOrEmpty(requested) ? null : requested);
	}

	public override Task OnDisconnectedAsync(Exception? exception)
	{
		if (Context.Items[SessionKey] is string id)
			sessions.Find(id)?.Detach(Context.ConnectionId);
		return Task.CompletedTask;
	}

	/// <summary>Runs a program; see <see cref="RunRequest"/>.</summary>
	public async Task Run(RunRequest request) => await (await CurrentSessionAsync()).StartAsync(request);

	/// <summary>Stops the running program, if any.</summary>
	public async Task Cancel() => (await CurrentSessionAsync()).Cancel();

	/// <summary>This connection's session; a new one, announced with <c>Welcome</c>, when it was removed after being idle.</summary>
	async Task<Session> CurrentSessionAsync()
	{
		if (Context.Items[SessionKey] is string id && sessions.Find(id) is { } session)
			return session;
		return await AttachAsync(null);
	}

	async Task<Session> AttachAsync(string? requested)
	{
		var attached = sessions.Attach(requested, Context.ConnectionId)
			?? throw new HubException("The server has too many users at the moment; please try again later.");
		Context.Items[SessionKey] = attached.Session.Id;
		await attached.Session.SendWelcomeAsync(attached.IsNew);
		return attached.Session;
	}
}
