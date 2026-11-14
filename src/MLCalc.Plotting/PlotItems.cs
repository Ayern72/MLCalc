using System.Collections.Immutable;

namespace MLCalc.Plotting;

/// <summary>How a series of points is drawn: connected by lines, or as a marker at each point.</summary>
public enum PlotStyle
{
	Line,
	Cross,
	Star,
	Dot,
}

/// <summary>A point in world (data) coordinates.</summary>
public readonly record struct PlotPoint(double X, double Y);

/// <summary>Something drawn in an <see cref="Axes"/>. Items are immutable.</summary>
/// <param name="Pen">Pen number; front-ends map it to a colour (see <see cref="Pens"/>).</param>
public abstract record PlotItem(int Pen);

/// <summary>
/// A sequence of points (<c>plot</c>, <c>cplot</c>). With <see cref="PlotStyle.Line"/>, consecutive
/// points are connected; a non-finite coordinate (NaN, infinity) breaks the line.
/// </summary>
public sealed record PointSeries(int Pen, PlotStyle Style, ImmutableArray<PlotPoint> Points) : PlotItem(Pen);

/// <summary>
/// A function sampled over the x range that was current when it was plotted (<c>fplot</c>,
/// <c>polyplot</c>). Drawn as a line that breaks at non-finite values.
/// </summary>
public sealed record FunctionSeries(int Pen, string Label, ImmutableArray<PlotPoint> Points) : PlotItem(Pen);

/// <summary>Single dots added one at a time with <c>dot(x, y)</c>, collected per pen.</summary>
public sealed record DotCloud(int Pen, ImmutableList<PlotPoint> Points) : PlotItem(Pen);

/// <summary>A point in 3D world (data) coordinates.</summary>
public readonly record struct Point3(double X, double Y, double Z)
{
	public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}

/// <summary>
/// A sequence of 3D points (<c>plot3d</c>). With <see cref="PlotStyle.Line"/>, consecutive points are
/// connected; a non-finite coordinate breaks the line.
/// </summary>
public sealed record PointSeries3D(int Pen, PlotStyle Style, ImmutableArray<Point3> Points) : PlotItem(Pen);

/// <summary>Single 3D dots added one at a time with <c>dot3d(x, y, z)</c>, collected per pen.</summary>
public sealed record DotCloud3D(int Pen, ImmutableList<Point3> Points) : PlotItem(Pen);

/// <summary>
/// A function z = f(x, y) sampled on a grid (<c>fplot3d</c>). <c>Points[j * XCount + i]</c> is the
/// sample at the i-th x and the j-th y; a non-finite z leaves a hole.
/// </summary>
public sealed record Surface(int Pen, string Label, int XCount, int YCount, ImmutableArray<Point3> Points) : PlotItem(Pen)
{
	public Point3 this[int i, int j] => Points[j * XCount + i];
}

/// <summary>How a 3D window is projected onto the screen. Only perspective for now.</summary>
public enum Projection
{
	Perspective,
}

/// <summary>
/// The camera of a 3D window, in data coordinates. Without a <see cref="Position"/> the camera is
/// placed relative to the box (from <see cref="Azimuth"/> and <see cref="Elevation"/>, in degrees,
/// as MATLAB's <c>view</c>), so it follows when the ranges change; <see cref="Distance"/> is then in
/// units of half the box (null: as close as the whole box fits). With a <see cref="Position"/>
/// (<c>camposition</c>) the camera stays where it was put. <see cref="Target"/> is the point looked
/// at; null means the centre of the box.
/// </summary>
public sealed record Camera(double Azimuth, double Elevation, double? Distance, Point3? Position, Point3? Target)
{
	/// <summary>MATLAB's default 3D view.</summary>
	public const double DefaultAzimuth = -37.5, DefaultElevation = 30;

	public static Camera Default { get; } = new(DefaultAzimuth, DefaultElevation, null, null, null);
}

/// <summary>The default pen palette, matching the spirit of the original 16-colour Amiga screen.</summary>
public static class Pens
{
	/// <summary>Number of distinct pens; pen numbers wrap around.</summary>
	public const int Count = 15;

	/// <summary>RGB colours for pens 1–15 (index 0 = pen 1).</summary>
	public static readonly ImmutableArray<uint> Colors =
	[
		0x1F77B4, 0xD62728, 0x2CA02C, 0xFF7F0E, 0x9467BD, 0x8C564B, 0xE377C2, 0x17BECF,
		0xBCBD22, 0x7F7F7F, 0x393B79, 0xAD494A, 0x637939, 0x8C6D31, 0x7B4173,
	];

	/// <summary>Maps any pen number to 1..<see cref="Count"/>.</summary>
	public static int Normalize(int pen) => ((pen - 1) % Count + Count) % Count + 1;

	/// <summary>The RGB colour of a pen.</summary>
	public static uint ColorOf(int pen) => Colors[Normalize(pen) - 1];
}
