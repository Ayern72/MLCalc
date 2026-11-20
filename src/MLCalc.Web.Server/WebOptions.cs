namespace MLCalc.Web.Server;

/// <summary>
/// The server's limits, from the "MLCalc" section of appsettings.json. In Docker each can be set
/// with an environment variable such as <c>MLCalc__RunSeconds=30</c>.
/// </summary>
public sealed class WebOptions
{
	/// <summary>The configuration section.</summary>
	public const string Section = "MLCalc";

	/// <summary>A session without activity for this long is removed, with its variables and plots.</summary>
	public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>The most sessions at the same time; further connections are refused.</summary>
	public int MaxSessions { get; set; } = 200;

	/// <summary>The most programs running at the same time, over all sessions; the others wait.</summary>
	public int MaxConcurrentRuns { get; set; } = Environment.ProcessorCount;

	/// <summary>How long a program may run, waiting included, before it is stopped.</summary>
	public int RunSeconds { get; set; } = 60;

	/// <summary>The most console entries one run may write; the rest are left out.</summary>
	public int MaxOutputEntries { get; set; } = 5_000;

	/// <summary>The longest program text accepted, in characters.</summary>
	public int MaxSourceLength { get; set; } = 200_000;

	/// <summary>The largest matrix or range.</summary>
	public int MaxMatrixElements { get; set; } = 2_000_000;

	/// <summary>The most points in one plot window.</summary>
	public int MaxPointsPerWindow { get; set; } = 200_000;

	/// <summary>The most grid lines each way in a surface from <c>fplot3d</c>.</summary>
	public int MaxSurfaceGridLines { get; set; } = 100;

	/// <summary>The most points in a curve from <c>fplot3d</c>.</summary>
	public int MaxCurvePoints { get; set; } = 20_000;

	/// <summary>How often output and plots are sent while a program runs.</summary>
	public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(50);

	/// <summary>How often idle sessions are looked for.</summary>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// The folder with the built TypeScript client (served at <c>/ts/</c>), relative to the content
	/// root. The Docker image has it; a server run from the source has it only after
	/// <c>npm run build</c> in src/MLCalc.Web.TypeScript, which writes it here.
	/// </summary>
	public string TypeScriptClient { get; set; } = "ts-client";
}
