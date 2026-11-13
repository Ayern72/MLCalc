using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MLCalc.Plotting;

namespace MLCalc.Wpf;

/// <summary>Renders figures to images off-screen, using the same drawing code as <see cref="PlotView"/>.</summary>
public static class PlotExporter
{
	/// <summary>
	/// Renders a figure as a bitmap. <paramref name="width"/> and <paramref name="height"/> are in
	/// layout units; <paramref name="scale"/> multiplies the pixel count (2 gives a sharp image for
	/// documents and high-DPI screens). Must be called on the UI thread.
	/// </summary>
	public static BitmapSource Render(FigureSnapshot snapshot, double width, double height, double scale = 1)
	{
		if (width < 50 || height < 50)
			throw new ArgumentOutOfRangeException(nameof(width), "The image must be at least 50×50.");
		if (width * scale > 16000 || height * scale > 16000)
			throw new ArgumentOutOfRangeException(nameof(width), "The image is too large (at most 16000 pixels per side).");

		var view = new PlotView { FixedSnapshot = snapshot };
		var size = new Size(width, height);
		view.Measure(size);
		view.Arrange(new Rect(size));
		view.UpdateLayout();

		var bitmap = new RenderTargetBitmap(
			(int)System.Math.Ceiling(width * scale), (int)System.Math.Ceiling(height * scale),
			96 * scale, 96 * scale, PixelFormats.Pbgra32);
		bitmap.Render(view);
		bitmap.Freeze();
		return bitmap;
	}

	public static void SavePng(BitmapSource bitmap, string path)
	{
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var stream = File.Create(path);
		encoder.Save(stream);
	}

	/// <summary>Saves as PNG or SVG, chosen by the file extension.</summary>
	public static void Save(FigureSnapshot snapshot, string path, double width, double height, double pngScale)
	{
		if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
			SvgWriter.Save(snapshot, path, width, height);
		else
			SavePng(Render(snapshot, width, height, pngScale), path);
	}
}
