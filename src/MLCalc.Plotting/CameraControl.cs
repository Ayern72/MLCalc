using System.Globalization;

namespace MLCalc.Plotting;

/// <summary>
/// Turning and moving a 3D window's camera with the mouse, independent of any user interface: the
/// front-end feeds in the mouse movement and passes the result to <see cref="Figure"/>.
/// </summary>
public static class CameraControl
{
	/// <summary>How far a drag turns the camera: a drag of 720 pixels goes once around.</summary>
	public const double DegreesPerPixel = 0.5;

	/// <summary>How much one notch of the mouse wheel (a delta of 120) moves the camera towards its target.</summary>
	public const double WheelStep = 0.85;

	/// <summary>
	/// The camera angles after dragging the mouse by (<paramref name="dx"/>, <paramref name="dy"/>)
	/// pixels from where the drag began, when the angles were <paramref name="azimuth"/> and
	/// <paramref name="elevation"/>. The box follows the mouse: dragging right turns its front to the
	/// right, dragging down tips its top towards you. The elevation stops at ±90°; the azimuth is
	/// kept between −180° and 180°. Both are rounded to a tenth of a degree, so that the numbers shown
	/// can be typed into <c>view</c> to get the same picture.
	/// </summary>
	public static (double Azimuth, double Elevation) Drag(double azimuth, double elevation, double dx, double dy)
	{
		double az = Math.Round(Math.IEEERemainder(azimuth - dx * DegreesPerPixel, 360), 1);
		if (az <= -180)
			az += 360;
		double el = Math.Round(Math.Clamp(elevation + dy * DegreesPerPixel, -90, 90), 1);
		return (az == 0 ? 0 : az, el == 0 ? 0 : el);
	}

	/// <summary>
	/// The factor for <see cref="Figure.MoveCamera"/> from a mouse wheel delta: turning the wheel away
	/// from you (a positive delta) moves the camera closer.
	/// </summary>
	public static double WheelFactor(int delta) => Math.Pow(WheelStep, delta / 120.0);

	/// <summary>
	/// The camera as a command that sets it, for the status bar: <c>view(az, el)</c> while the
	/// camera is placed relative to the box at the default distance, otherwise
	/// <c>camposition(x, y, z)</c>. <paramref name="cell"/> is the window's rectangle on the screen.
	/// </summary>
	public static string Describe(Axes axes, ScreenRect cell)
	{
		var camera = axes.Camera;
		if (camera.Position is null && camera.Distance is null)
			return FormattableString.Invariant($"view({Format(camera.Azimuth)}, {Format(camera.Elevation)})");
		var p = new Projection3D(axes, Scene3D.ViewArea(cell)).CameraPosition;
		// Rounding leaves 1e-15 where the camera is level with the middle of the box; show 0.
		return FormattableString.Invariant(
			$"camposition({Format(p.X, axes.XMax - axes.XMin)}, {Format(p.Y, axes.YMax - axes.YMin)}, {Format(p.Z, axes.ZMax - axes.ZMin)})");
	}

	static string Format(double value, double span) => Format(Math.Abs(value) < span * 1e-9 ? 0 : value);

	static string Format(double value)
	{
		string text = value.ToString("G4", CultureInfo.InvariantCulture);
		return text == "-0" ? "0" : text;
	}
}
