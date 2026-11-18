using System.Text.Json;
using System.Text.Json.Serialization;

namespace MLCalc.Web.Protocol;

/// <summary>
/// Where clients connect and how the messages are written. The server and the .NET client use
/// <see cref="Configure"/>; other clients follow docs/web-protocol.md, which shows the same JSON.
/// </summary>
public static class WebProtocol
{
	/// <summary>The path of the SignalR hub.</summary>
	public const string Path = "/hub";

	/// <summary>The query parameter that names the session to continue, e.g. <c>/hub?session=…</c>.</summary>
	public const string SessionParameter = "session";

	/// <summary>
	/// The JSON settings of the protocol: camelCase names, enums as the strings given by their
	/// <see cref="JsonStringEnumMemberNameAttribute"/>, and absent optional fields left out.
	/// </summary>
	public static void Configure(JsonSerializerOptions options)
	{
		options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
		options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
	}

	/// <summary>New JSON settings for the protocol, e.g. for tests.</summary>
	public static JsonSerializerOptions CreateJsonOptions()
	{
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		Configure(options);
		return options;
	}
}

/// <summary>
/// The messages the server sends; a SignalR client listens for each by the method's name
/// (<c>welcome</c>, <c>output</c>, … in camelCase as in any SignalR client: the names are matched
/// without regard to case).
/// </summary>
public interface IMLCalcClient
{
	/// <summary>Sent when a client connects or reconnects.</summary>
	Task Welcome(WelcomeMessage message);

	/// <summary>Console output from the running program, in batches.</summary>
	Task Output(OutputMessage message);

	/// <summary>A program has finished, or did not start.</summary>
	Task RunFinished(RunFinishedMessage message);

	/// <summary>A file for the user to save, from <c>savefig</c>.</summary>
	Task Download(DownloadMessage message);
}

/// <summary>The methods a client calls on the server.</summary>
public static class ServerMethods
{
	/// <summary><c>Run(RunRequest)</c>: runs a program; see <see cref="RunRequest"/>.</summary>
	public const string Run = "Run";

	/// <summary><c>Cancel()</c>: stops the running program, if any.</summary>
	public const string Cancel = "Cancel";
}

// ───────────── Client → server ─────────────

/// <summary>
/// A program to run: console input or the text of a script the user opened. When the program is
/// unfinished (an open block, bracket or parenthesis) and its last line is not empty, nothing runs
/// and the reply is <see cref="RunStatus.Incomplete"/>, so that a client can collect more lines; an
/// empty last line runs it anyway, which reports what is missing.
/// </summary>
/// <param name="Id">Chosen by the client; returned in <see cref="RunFinishedMessage.Id"/>.</param>
/// <param name="Source">The program text.</param>
public sealed record RunRequest(string Id, string Source);

// ───────────── Server → client ─────────────

/// <summary>The first message on every connection.</summary>
/// <param name="SessionId">The session: pass it as <c>?session=</c> when reconnecting to keep the variables and plots.</param>
/// <param name="NewSession">True when a new session was started, also when the one asked for no longer existed.</param>
/// <param name="Running">True when a program of this session is running (after a reconnect).</param>
/// <param name="BuiltinNames">The names of all functions and commands, sorted.</param>
/// <param name="Limits">What programs may do on this server.</param>
public sealed record WelcomeMessage(string SessionId, bool NewSession, bool Running, IReadOnlyList<string> BuiltinNames, Limits Limits);

/// <summary>The limits a program runs under.</summary>
/// <param name="RunSeconds">How long a program may run before it is stopped.</param>
/// <param name="MaxMatrixElements">The largest matrix or range.</param>
/// <param name="MaxPointsPerWindow">The most points in one plot window.</param>
/// <param name="MaxOutputEntries">The most console entries one run may write; the rest are left out.</param>
/// <param name="MaxSourceLength">The longest program text accepted, in characters.</param>
public sealed record Limits(int RunSeconds, int MaxMatrixElements, int MaxPointsPerWindow, int MaxOutputEntries, int MaxSourceLength);

/// <summary>Console output, oldest first.</summary>
public sealed record OutputMessage(IReadOnlyList<OutputEntry> Entries);

/// <summary>One console entry.</summary>
/// <param name="Kind">What the entry is.</param>
/// <param name="Text">The text, possibly several lines; empty for <see cref="OutputKind.Clear"/>.</param>
public sealed record OutputEntry(OutputKind Kind, string Text);

/// <summary>The kinds of console entries.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OutputKind>))]
public enum OutputKind
{
	/// <summary>A value, already formatted, e.g. <c>x = 3</c> or a matrix.</summary>
	[JsonStringEnumMemberName("value")]
	Value,

	/// <summary>Plain text, e.g. from <c>disp</c> or <c>who</c>.</summary>
	[JsonStringEnumMemberName("text")]
	Text,

	/// <summary>The console is to be cleared (<c>cls</c>).</summary>
	[JsonStringEnumMemberName("clear")]
	Clear,
}

/// <summary>How a run ended.</summary>
/// <param name="Id">The <see cref="RunRequest.Id"/>.</param>
/// <param name="Status">How it ended.</param>
/// <param name="Errors">The errors, for <see cref="RunStatus.Error"/>; otherwise left out.</param>
/// <param name="OutputTruncated">True when the program wrote more than <see cref="Limits.MaxOutputEntries"/> entries and the rest were left out.</param>
public sealed record RunFinishedMessage(string Id, RunStatus Status, IReadOnlyList<RunError>? Errors = null, bool OutputTruncated = false);

/// <summary>The ways a run can end.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RunStatus>))]
public enum RunStatus
{
	/// <summary>The program ran to the end.</summary>
	[JsonStringEnumMemberName("ok")]
	Ok,

	/// <summary>A syntax error (nothing ran) or a runtime error (the program stopped there).</summary>
	[JsonStringEnumMemberName("error")]
	Error,

	/// <summary>Stopped by <c>Cancel</c>.</summary>
	[JsonStringEnumMemberName("cancelled")]
	Cancelled,

	/// <summary>Stopped after running for <see cref="Limits.RunSeconds"/>.</summary>
	[JsonStringEnumMemberName("timeLimit")]
	TimeLimit,

	/// <summary>The program is unfinished; nothing ran. See <see cref="RunRequest"/>.</summary>
	[JsonStringEnumMemberName("incomplete")]
	Incomplete,

	/// <summary>Another program of this session is still running; nothing ran.</summary>
	[JsonStringEnumMemberName("busy")]
	Busy,
}

/// <summary>An error in a program, with its place in the program's text.</summary>
/// <param name="Message">What is wrong.</param>
/// <param name="Line">One-based line; 0 when the error has no place (e.g. a limit).</param>
/// <param name="Column">One-based column; 0 when the error has no place.</param>
/// <param name="Length">The number of characters the error covers.</param>
public sealed record RunError(string Message, int Line, int Column, int Length);

/// <summary>A file for the user to save.</summary>
/// <param name="FileName">The suggested file name, without a folder.</param>
/// <param name="MediaType">E.g. <c>image/svg+xml</c>.</param>
/// <param name="Content">The file's text.</param>
public sealed record DownloadMessage(string FileName, string MediaType, string Content);
