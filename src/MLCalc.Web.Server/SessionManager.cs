using System.Collections.Concurrent;
using System.Security.Cryptography;
using MLCalc.Web.Protocol;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace MLCalc.Web.Server;

/// <summary>
/// The sessions, by id. A connection asks for its session by id; when there is none (new user,
/// or the session was removed after being idle) a new one is made, up to <see cref="WebOptions.MaxSessions"/>.
/// </summary>
sealed class SessionManager(
	IOptions<WebOptions> options,
	TimeProvider time,
	IHubContext<MLCalcHub, IMLCalcClient> hub,
	ILogger<SessionManager> logger) : IDisposable
{
	readonly ConcurrentDictionary<string, Session> sessions = new();
	readonly Lock creating = new();
	readonly SemaphoreSlim runSlots = new(Math.Max(1, options.Value.MaxConcurrentRuns));

	public int Count => sessions.Count;

	public Session? Find(string id) => sessions.TryGetValue(id, out var session) ? session : null;

	/// <summary>
	/// The session with <paramref name="requestedId"/>, or a new one; null when the server already
	/// has as many sessions as it may.
	/// </summary>
	public (Session Session, bool IsNew)? Attach(string? requestedId, string connectionId)
	{
		if (requestedId is not null && Find(requestedId) is { } existing)
		{
			existing.Attach(connectionId);
			return (existing, false);
		}
		lock (creating)
		{
			if (sessions.Count >= options.Value.MaxSessions)
				return null;
			// 128 random bits: the id is what gives access to the session.
			string id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
			var session = new Session(id, options.Value, time, runSlots, hub, logger);
			sessions[id] = session;
			session.Attach(connectionId);
			logger.LogInformation("Session {Session} started; {Count} sessions.", id, sessions.Count);
			return (session, true);
		}
	}

	/// <summary>Removes sessions without activity for <see cref="WebOptions.IdleTimeout"/>, except those running a program.</summary>
	public int RemoveIdle()
	{
		var limit = time.GetUtcNow() - options.Value.IdleTimeout;
		int removed = 0;
		foreach (var (id, session) in sessions)
		{
			if (session.LastActive > limit || session.IsRunning || !sessions.TryRemove(id, out _))
				continue;
			session.Dispose();
			removed++;
			logger.LogInformation("Session {Session} removed after being idle.", id);
		}
		return removed;
	}

	public void Dispose()
	{
		foreach (var session in sessions.Values)
			session.Dispose();
		sessions.Clear();
	}
}

/// <summary>Removes idle sessions every <see cref="WebOptions.CleanupInterval"/>.</summary>
sealed class IdleSessionCleanup(SessionManager sessions, IOptions<WebOptions> options, TimeProvider time) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(options.Value.CleanupInterval, time);
		while (await timer.WaitForNextTickAsync(stoppingToken))
			sessions.RemoveIdle();
	}
}
