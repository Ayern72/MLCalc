namespace MLCalc.Plotting;

/// <summary>A rectangle in screen (device) coordinates; y grows downward.</summary>
public readonly record struct ScreenRect(double Left, double Top, double Width, double Height)
{
	public double Right => Left + Width;
	public double Bottom => Top + Height;
}

/// <summary>Maps between world coordinates of an <see cref="Axes"/> and a screen rectangle.</summary>
public readonly struct PlotTransform(Axes axes, ScreenRect screen)
{
	public Axes Axes { get; } = axes;
	public ScreenRect Screen { get; } = screen;

	public double ToScreenX(double x) => Screen.Left + (x - Axes.XMin) / (Axes.XMax - Axes.XMin) * Screen.Width;

	public double ToScreenY(double y) => Screen.Bottom - (y - Axes.YMin) / (Axes.YMax - Axes.YMin) * Screen.Height;

	public double ToWorldX(double screenX) => Axes.XMin + (screenX - Screen.Left) / Screen.Width * (Axes.XMax - Axes.XMin);

	public double ToWorldY(double screenY) => Axes.YMin + (Screen.Bottom - screenY) / Screen.Height * (Axes.YMax - Axes.YMin);

	public bool Contains(PlotPoint p) =>
		p.X >= Axes.XMin && p.X <= Axes.XMax && p.Y >= Axes.YMin && p.Y <= Axes.YMax;
}

/// <summary>Chooses "nice" tick positions (multiples of 1, 2 or 5 × 10ⁿ) for an axis.</summary>
public static class AxisTicks
{
	/// <summary>Tick values covering [min, max], aiming for about <paramref name="targetCount"/> ticks.</summary>
	public static IReadOnlyList<double> Compute(double min, double max, int targetCount = 6)
	{
		if (!(max > min) || targetCount < 1)
			return [];
		double step = NiceStep((max - min) / targetCount);
		double first = Math.Ceiling(min / step) * step;
		// Round to the step's decimals so 3 × 0.1 becomes 0.3, not 0.30000000000000004.
		int decimals = Math.Clamp(-(int)Math.Floor(Math.Log10(step)), 0, 15);
		var ticks = new List<double>();
		for (int i = 0; ; i++)
		{
			double value = first + i * step;
			if (value > max + step * 1e-9)
				break;
			value = Math.Round(value, decimals);
			ticks.Add(value == 0 ? 0 : value);
		}
		return ticks;
	}

	/// <summary>The smallest of 1, 2, 5 × 10ⁿ that is at least <paramref name="rough"/>.</summary>
	public static double NiceStep(double rough)
	{
		double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
		double fraction = rough / magnitude;
		double nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
		return nice * magnitude;
	}
}
