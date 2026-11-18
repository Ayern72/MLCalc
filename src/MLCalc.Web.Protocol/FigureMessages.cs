using System.Text.Json.Serialization;

namespace MLCalc.Web.Protocol;

/// <summary>
/// The plot windows, or what changed in them. The figure is a grid of
/// <see cref="Rows"/>×<see cref="Columns"/> windows, numbered row by row from 0. In
/// <see cref="WelcomeMessage"/> every window is sent whole; in <c>FigureChanged</c> only the
/// windows that changed, and windows that are not listed are as before.
/// </summary>
/// <param name="Version">Grows with every change.</param>
/// <param name="Rows">Rows of windows, 1–3.</param>
/// <param name="Columns">Columns of windows, 1–3.</param>
/// <param name="CurrentRow">The window that commands draw into, zero-based.</param>
/// <param name="CurrentColumn">The window that commands draw into, zero-based.</param>
/// <param name="Windows">The changed windows. When the grid's size changes, all windows are sent.</param>
public sealed record FigureMessage(long Version, int Rows, int Columns, int CurrentRow, int CurrentColumn, IReadOnlyList<WindowUpdate> Windows);

/// <summary>A change to one window: exactly one of <see cref="Replace"/> and <see cref="Append"/> is given.</summary>
/// <param name="Index">The window: row × columns + column.</param>
/// <param name="Replace">The whole window, replacing what the client had.</param>
/// <param name="Append">Items to add to the window as the client has it.</param>
public sealed record WindowUpdate(int Index, PlotWindow? Replace = null, WindowAppend? Append = null);

/// <summary>
/// Additions to a window, the common case while a program draws in a loop: first
/// <see cref="LastDots"/> are added to the window's last item, which is a <c>dots</c> or
/// <c>dots3d</c> item; then <see cref="Items"/> are added after it. <see cref="Pen"/> is the window's
/// pen afterwards, which moves on when an item is added.
/// </summary>
public sealed record WindowAppend(int Pen, IReadOnlyList<PlotItemData> Items, PointsData? LastDots = null);

/// <summary>Coordinates as separate arrays; <see cref="Z"/> only for 3D. A null value is a missing point.</summary>
public sealed record PointsData(float?[] X, float?[] Y, float?[]? Z = null);

/// <summary>One plot window.</summary>
/// <param name="Kind">x-y, complex plane or 3D.</param>
/// <param name="XMin">The x range.</param>
/// <param name="XMax">The x range.</param>
/// <param name="YMin">The y range.</param>
/// <param name="YMax">The y range.</param>
/// <param name="ZMin">The z range, used by 3D windows.</param>
/// <param name="ZMax">The z range, used by 3D windows.</param>
/// <param name="Title">The window's title; empty for none.</param>
/// <param name="XTitle">The x axis title; empty for none (complex windows show "Re").</param>
/// <param name="YTitle">The y axis title; empty for none (complex windows show "Im").</param>
/// <param name="ZTitle">The z axis title; empty for none.</param>
/// <param name="Grid">Whether grid lines are shown.</param>
/// <param name="Pen">The pen the next item gets, 1–15.</param>
/// <param name="Camera">The camera of a 3D window.</param>
/// <param name="Items">What is drawn, in drawing order.</param>
public sealed record PlotWindow(
	WindowKind Kind,
	double XMin, double XMax, double YMin, double YMax, double ZMin, double ZMax,
	string Title, string XTitle, string YTitle, string ZTitle,
	bool Grid, int Pen, CameraData Camera, IReadOnlyList<PlotItemData> Items);

/// <summary>The kinds of windows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WindowKind>))]
public enum WindowKind
{
	/// <summary>An x-y window.</summary>
	[JsonStringEnumMemberName("cartesian")]
	Cartesian,

	/// <summary>The complex plane: real part across, imaginary part up.</summary>
	[JsonStringEnumMemberName("complex")]
	Complex,

	/// <summary>A 3D window.</summary>
	[JsonStringEnumMemberName("space")]
	Space,
}

/// <summary>
/// The camera of a 3D window, in data coordinates. Without <see cref="Position"/> it is placed
/// relative to the box at <see cref="Azimuth"/> and <see cref="Elevation"/> (degrees), at
/// <see cref="Distance"/> in units of half the box (null: just far enough for the box to fit).
/// With <see cref="Position"/> ([x, y, z]) it stays at that point. <see cref="Target"/> is the point
/// looked at; null is the middle of the box.
/// </summary>
public sealed record CameraData(double Azimuth, double Elevation, double? Distance = null, double[]? Position = null, double[]? Target = null);

/// <summary>
/// One plotted item. Which fields are given depends on <see cref="Type"/>:
/// <list type="bullet">
/// <item><c>series</c>: points joined or marked by <see cref="Style"/>; in a complex window x is the real part.</item>
/// <item><c>function</c>: a sampled function with its <see cref="Label"/>, drawn as a line.</item>
/// <item><c>dots</c>: single points.</item>
/// <item><c>series3d</c>, <c>dots3d</c>: the same in 3D, with <see cref="Z"/>.</item>
/// <item><c>surface</c>: a grid of <see cref="XCount"/>×<see cref="YCount"/> points, row by row
/// (x changes fastest), with <see cref="Label"/>; a null z is a hole.</item>
/// </list>
/// A null coordinate is a missing point, which breaks a line.
/// </summary>
public sealed record PlotItemData(
	ItemType Type,
	int Pen,
	float?[] X,
	float?[] Y,
	float?[]? Z = null,
	SeriesStyle? Style = null,
	string? Label = null,
	int? XCount = null,
	int? YCount = null);

/// <summary>The kinds of plotted items.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ItemType>))]
public enum ItemType
{
	/// <summary>Points from <c>plot</c> or <c>cplot</c>.</summary>
	[JsonStringEnumMemberName("series")]
	Series,

	/// <summary>A function from <c>fplot</c> or <c>polyplot</c>.</summary>
	[JsonStringEnumMemberName("function")]
	Function,

	/// <summary>Points from <c>dot</c>.</summary>
	[JsonStringEnumMemberName("dots")]
	Dots,

	/// <summary>Points from <c>plot3d</c> or a curve from <c>fplot3d</c>.</summary>
	[JsonStringEnumMemberName("series3d")]
	Series3D,

	/// <summary>Points from <c>dot3d</c>.</summary>
	[JsonStringEnumMemberName("dots3d")]
	Dots3D,

	/// <summary>A surface from <c>fplot3d</c>.</summary>
	[JsonStringEnumMemberName("surface")]
	Surface,
}

/// <summary>How a series is drawn.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SeriesStyle>))]
public enum SeriesStyle
{
	/// <summary>Connected lines.</summary>
	[JsonStringEnumMemberName("line")]
	Line,

	/// <summary>A cross at each point.</summary>
	[JsonStringEnumMemberName("cross")]
	Cross,

	/// <summary>A star at each point.</summary>
	[JsonStringEnumMemberName("star")]
	Star,

	/// <summary>A dot at each point.</summary>
	[JsonStringEnumMemberName("dot")]
	Dot,
}
