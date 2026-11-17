namespace MLCalc.Plotting;

/// <summary>A vector in the normalised box ("cube") coordinates of a 3D window.</summary>
internal readonly record struct Vec3(double X, double Y, double Z)
{
	public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
	public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
	public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
	public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
	public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
	public double Length => Math.Sqrt(Dot(this));
	public Vec3 Normalized => this * (1 / Length);
	public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}

/// <summary>A point on the screen (device coordinates; y grows downward).</summary>
public readonly record struct ScreenPoint(double X, double Y);

/// <summary>
/// Maps the data coordinates of a 3D window to a screen rectangle. The box set by the x, y and z
/// ranges is first scaled to the cube −1..1 on each axis (so very different ranges still give a
/// sensible picture), then seen through a perspective camera (see <see cref="Camera"/>) with z up.
/// </summary>
public sealed class Projection3D
{
	/// <summary>The camera's field of view across the shorter side of the screen rectangle, in degrees.</summary>
	public const double FieldOfView = 30;

	/// <summary>The near clipping plane, in cube units in front of the camera.</summary>
	const double Near = 1e-3;

	/// <summary>A relative camera without a distance moves just far enough back for the whole box to fit, with this margin.</summary>
	const double FitMargin = 1.04;

	readonly Axes axes;
	readonly Vec3 eye, target, right, up, forward;
	readonly double scale, centerX, centerY;

	/// <summary>The projection of a 3D window drawn in <paramref name="screen"/>.</summary>
	public Projection3D(Axes axes, ScreenRect screen)
	{
		this.axes = axes;
		Screen = screen;
		centerX = screen.Left + screen.Width / 2;
		centerY = screen.Top + screen.Height / 2;
		double tan = Math.Tan(FieldOfView / 2 * Math.PI / 180);
		double shorter = Math.Max(1, Math.Min(screen.Width, screen.Height));
		scale = shorter / 2 / tan;

		var camera = axes.Camera;
		target = camera.Target is { } t ? Cube(t) : new Vec3(0, 0, 0);
		double az = camera.Azimuth * Math.PI / 180, el = camera.Elevation * Math.PI / 180;
		// The horizontal direction the camera faces, used for "up" when it looks straight down or up.
		var facing = new Vec3(-Math.Sin(az), Math.Cos(az), 0);
		Vec3 toCamera;
		if (camera.Position is { } position && (Cube(position) - target) is { Length: > 0 } offset && offset.IsFinite)
		{
			toCamera = offset.Normalized;
			facing = new Vec3(0, 1, 0);
		}
		else
		{
			// Azimuth 0 looks from −y towards +y, 90 from +x; elevation is the angle above the x-y plane.
			toCamera = new Vec3(Math.Sin(az) * Math.Cos(el), -Math.Cos(az) * Math.Cos(el), Math.Sin(el));
			if (el < 0)
				facing *= -1;
		}

		forward = toCamera * -1;
		var z = new Vec3(0, 0, 1);
		var upward = z - forward * z.Dot(forward);
		up = upward.Length > 1e-9 ? upward.Normalized : facing;
		right = forward.Cross(up).Normalized;
		up = right.Cross(forward);

		if (camera.Position is { } p && Cube(p) is { IsFinite: true } placed && (placed - target).Length > 0)
		{
			eye = placed;
			IsPlaced = true;
		}
		else
		{
			double distance = camera.Distance ?? FitDistance(target, tan * screen.Width / shorter / FitMargin, tan * screen.Height / shorter / FitMargin);
			eye = target + toCamera * distance;
		}
	}

	/// <summary>The screen rectangle the camera's view fills.</summary>
	public ScreenRect Screen { get; }

	internal Vec3 Eye => eye;

	/// <summary>Whether the camera is at a point set with <c>camposition</c>, rather than placed relative to the box.</summary>
	public bool IsPlaced { get; }

	/// <summary>The distance from the camera to the point it looks at, in units of half the box.</summary>
	public double DistanceToTarget => (eye - target).Length;

	/// <summary>Where the camera is, in data coordinates.</summary>
	public Point3 CameraPosition => FromCube(eye);

	/// <summary>The point the camera looks at, in data coordinates.</summary>
	public Point3 TargetPosition => FromCube(target);

	Point3 FromCube(Vec3 c) => new(
		axes.XMin + (c.X + 1) / 2 * (axes.XMax - axes.XMin),
		axes.YMin + (c.Y + 1) / 2 * (axes.YMax - axes.YMin),
		axes.ZMin + (c.Z + 1) / 2 * (axes.ZMax - axes.ZMin));

	/// <summary>
	/// The azimuth and elevation, in degrees, that the camera looks from, as <c>view</c> takes them.
	/// For a camera set with <c>camposition</c> they follow from where it is.
	/// </summary>
	public static (double Azimuth, double Elevation) ViewAngles(Axes axes)
	{
		var camera = axes.Camera;
		if (camera.Position is { } p)
		{
			var target = camera.Target is { } t ? ToCube(axes, t) : new Point3(0, 0, 0);
			var position = ToCube(axes, p);
			var offset = new Vec3(position.X - target.X, position.Y - target.Y, position.Z - target.Z);
			if (offset.IsFinite && offset.Length > 0)
			{
				var d = offset.Normalized;
				double elevation = Math.Asin(Math.Clamp(d.Z, -1, 1)) * 180 / Math.PI;
				// Straight above or below, the direction says nothing about the azimuth.
				double azimuth = Math.Abs(d.Z) > 1 - 1e-12 ? camera.Azimuth : Math.Atan2(d.X, -d.Y) * 180 / Math.PI;
				return (azimuth, elevation);
			}
		}
		return (camera.Azimuth, camera.Elevation);
	}

	/// <summary>A data point in cube coordinates: the box from the ranges becomes −1..1 on each axis.</summary>
	public static Point3 ToCube(Axes axes, Point3 p) => new(
		2 * (p.X - axes.XMin) / (axes.XMax - axes.XMin) - 1,
		2 * (p.Y - axes.YMin) / (axes.YMax - axes.YMin) - 1,
		2 * (p.Z - axes.ZMin) / (axes.ZMax - axes.ZMin) - 1);

	internal Vec3 Cube(Point3 p)
	{
		var c = ToCube(axes, p);
		return new Vec3(c.X, c.Y, c.Z);
	}

	/// <summary>The distance from an absolute camera to its target, in cube units (half the box).</summary>
	public static double CameraDistance(Axes axes)
	{
		var target = axes.Camera.Target is { } t ? ToCube(axes, t) : new Point3(0, 0, 0);
		var position = axes.Camera.Position is { } p ? ToCube(axes, p) : target;
		double distance = new Vec3(position.X - target.X, position.Y - target.Y, position.Z - target.Z).Length;
		return double.IsFinite(distance) && distance > 0 ? distance : throw new ArgumentException("The camera has no position.");
	}

	/// <summary>The depth of a cube point in front of the camera.</summary>
	internal double Depth(Vec3 c) => (c - eye).Dot(forward);

	/// <summary>The screen position of a cube point; the point must be in front of the camera.</summary>
	internal ScreenPoint ToScreen(Vec3 c)
	{
		var d = c - eye;
		double depth = d.Dot(forward);
		var (x, y) = PlotLayout.Clamp(centerX + scale * d.Dot(right) / depth, centerY - scale * d.Dot(up) / depth, Screen);
		return new ScreenPoint(x, y);
	}

	/// <summary>The screen position of a data point, or null when it is behind the camera.</summary>
	public ScreenPoint? Project(Point3 p)
	{
		var c = Cube(p);
		return c.IsFinite && Depth(c) >= Near ? ToScreen(c) : null;
	}

	/// <summary>Whether a cube point is inside the box (with a little tolerance for rounding) and in front of the camera.</summary>
	internal bool IsVisible(Vec3 c)
	{
		const double Tolerance = 1e-9;
		return c.IsFinite && Math.Abs(c.X) <= 1 + Tolerance && Math.Abs(c.Y) <= 1 + Tolerance
			&& Math.Abs(c.Z) <= 1 + Tolerance && Depth(c) >= Near;
	}

	/// <summary>
	/// Cuts a segment in cube coordinates to the part inside the box and in front of the camera
	/// (Liang–Barsky). Returns false when nothing is left; <paramref name="clippedStart"/> and
	/// <paramref name="clippedEnd"/> tell whether an end was moved.
	/// </summary>
	internal bool ClipSegment(ref Vec3 a, ref Vec3 b, out bool clippedStart, out bool clippedEnd)
	{
		clippedStart = clippedEnd = false;
		double t0 = 0, t1 = 1;
		foreach (var (normal, offset) in Planes())
		{
			double fa = normal.Dot(a) + offset, fb = normal.Dot(b) + offset;
			if (fa < 0 && fb < 0)
				return false;
			if (fa < 0)
				t0 = Math.Max(t0, fa / (fa - fb));
			else if (fb < 0)
				t1 = Math.Min(t1, fa / (fa - fb));
			if (t0 > t1)
				return false;
		}
		var start = a + (b - a) * t0;
		var end = a + (b - a) * t1;
		clippedStart = t0 > 0;
		clippedEnd = t1 < 1;
		a = start;
		b = end;
		return true;
	}

	/// <summary>Cuts a polygon in cube coordinates to the part inside the box and in front of the camera (Sutherland–Hodgman).</summary>
	internal List<Vec3> ClipPolygon(List<Vec3> polygon)
	{
		foreach (var (normal, offset) in Planes())
		{
			if (polygon.Count == 0)
				break;
			var output = new List<Vec3>(polygon.Count + 2);
			for (int i = 0; i < polygon.Count; i++)
			{
				var a = polygon[i];
				var b = polygon[(i + 1) % polygon.Count];
				double fa = normal.Dot(a) + offset, fb = normal.Dot(b) + offset;
				if (fa >= 0)
					output.Add(a);
				if ((fa >= 0) != (fb >= 0))
					output.Add(a + (b - a) * (fa / (fa - fb)));
			}
			polygon = output;
		}
		return polygon;
	}

	/// <summary>The half-spaces n·p + c ≥ 0 that bound what is drawn: the six sides of the box and the near plane.</summary>
	IEnumerable<(Vec3 Normal, double Offset)> Planes()
	{
		const double Tolerance = 1e-9;
		yield return (new Vec3(-1, 0, 0), 1 + Tolerance);
		yield return (new Vec3(1, 0, 0), 1 + Tolerance);
		yield return (new Vec3(0, -1, 0), 1 + Tolerance);
		yield return (new Vec3(0, 1, 0), 1 + Tolerance);
		yield return (new Vec3(0, 0, -1), 1 + Tolerance);
		yield return (new Vec3(0, 0, 1), 1 + Tolerance);
		yield return (forward, -eye.Dot(forward) - Near);
	}

	/// <summary>
	/// The smallest distance from the target at which every corner of the box is inside the field of
	/// view, whose half-widths (as tangents) are <paramref name="tanX"/> and <paramref name="tanY"/>.
	/// </summary>
	double FitDistance(Vec3 target, double tanX, double tanY)
	{
		double distance = 0;
		for (int corner = 0; corner < 8; corner++)
		{
			var q = new Vec3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1) - target;
			// The corner's depth is q·forward + distance; it must be at least |x| / tanX and |y| / tanY.
			double needed = Math.Max(Math.Abs(q.Dot(right)) / tanX, Math.Abs(q.Dot(up)) / tanY);
			distance = Math.Max(distance, needed - q.Dot(forward) + Near);
		}
		return distance;
	}
}
