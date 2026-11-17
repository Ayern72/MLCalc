using System.Windows.Media;
using System.Windows.Media.Imaging;
using MLCalc.Plotting;

namespace MLCalc.Wpf.Tests;

/// <summary>The desktop renderer, drawn off-screen as File › Export Plot does.</summary>
public class PlotExportTests
{
	/// <summary>WPF drawing needs a single-threaded apartment.</summary>
	static T OnStaThread<T>(Func<T> action)
	{
		T result = default!;
		Exception? error = null;
		var thread = new Thread(() =>
		{
			try
			{
				result = action();
			}
			catch (Exception ex)
			{
				error = ex;
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();
		if (error is not null)
			throw new InvalidOperationException("Rendering failed.", error);
		return result;
	}

	/// <summary>The number of pixels within a small distance of the colour of a pen.</summary>
	static int PixelsOfPen(BitmapSource bitmap, int pen)
	{
		uint rgb = Pens.ColorOf(pen);
		var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
		int stride = converted.PixelWidth * 4;
		var pixels = new byte[stride * converted.PixelHeight];
		converted.CopyPixels(pixels, stride, 0);
		int count = 0;
		for (int k = 0; k < pixels.Length; k += 4)
			if (Math.Abs(pixels[k + 2] - (int)((rgb >> 16) & 0xFF)) < 24
				&& Math.Abs(pixels[k + 1] - (int)((rgb >> 8) & 0xFF)) < 24
				&& Math.Abs(pixels[k] - (int)(rgb & 0xFF)) < 24)
				count++;
		return count;
	}

	[Fact]
	public void Draws3DWindows()
	{
		var figure = new Figure();
		figure.Initialize(1, 2);
		figure.SetTitle("surface");
		figure.ShowGrid();
		figure.AddSurface("x*y", (x, y) => x * y / 10, 20);
		figure.AddSeries3D([new(-10, -10, -10), new(10, 10, 10)], PlotStyle.Line);
		figure.SelectAxes(1, 2);
		figure.AddDot3D(0, 0, 0);
		figure.SetPen(3);
		figure.AddSeries3D([new(5, 5, 5), new(-5, 5, 5)], PlotStyle.Star);
		// The current window is outlined in the colour of pen 1; keep that out of the right half.
		figure.SelectAxes(1, 1);

		var (surface, curve, dot, star) = OnStaThread(() =>
		{
			var bitmap = PlotExporter.Render(figure.Snapshot(), 800, 400);
			// The left half has the surface (pen 1) and the line (pen 2); the right half the dot
			// (pen 1, drawn small) and the stars (pen 3).
			var left = new CroppedBitmap(bitmap, new System.Windows.Int32Rect(0, 0, 400, 400));
			var right = new CroppedBitmap(bitmap, new System.Windows.Int32Rect(400, 0, 400, 400));
			return (PixelsOfPen(left, 1), PixelsOfPen(left, 2), PixelsOfPen(right, 1), PixelsOfPen(right, 3));
		});
		Assert.True(surface > 2000, $"surface: {surface} pixels");
		Assert.True(curve > 100, $"curve: {curve} pixels");
		Assert.True(star > 10, $"stars: {star} pixels");
		Assert.InRange(dot, 2, 20);
	}
}
