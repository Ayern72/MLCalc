using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MLCalc.Plotting;

namespace MLCalc.Wpf;

/// <summary>
/// Renders a <see cref="Plotting.Figure"/>. The figure may change on a worker thread; changes are
/// picked up by a timer, so a running script redraws at most every 40 ms.
/// </summary>
public sealed class PlotView : FrameworkElement
{
	public static readonly DependencyProperty FigureProperty = DependencyProperty.Register(
		nameof(Figure), typeof(Figure), typeof(PlotView),
		new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFigureChanged));

	const double MarginLeft = 62, MarginRight = 18, MarginTop = 28, MarginBottom = 44;
	const double MarkerSize = 3.5;

	static readonly Typeface LabelFace = new("Segoe UI");
	static readonly Brush Background = Frozen(new SolidColorBrush(Colors.White));
	static readonly Brush TextBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
	static readonly Pen FramePen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), 1));
	static readonly Pen CurrentFramePen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x1F, 0x77, 0xB4)), 2));
	static readonly Pen OriginPen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)), 1));
	static readonly Pen GridPen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0xE2, 0xE2, 0xE2)), 1) { DashStyle = DashStyles.Dash });
	static readonly Pen TickPen = FramePen;

	readonly Dictionary<int, (Brush Brush, Pen Pen)> pens = [];
	readonly DispatcherTimer timer;
	int dirty;
	FigureSnapshot? lastSnapshot;

	public PlotView()
	{
		timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Render, (_, _) =>
		{
			if (Interlocked.Exchange(ref dirty, 0) == 1)
				InvalidateVisual();
		}, Dispatcher);
		timer.Stop();
		Loaded += (_, _) => timer.Start();
		Unloaded += (_, _) => timer.Stop();
		ClipToBounds = true;
	}

	public Figure? Figure
	{
		get => (Figure?)GetValue(FigureProperty);
		set => SetValue(FigureProperty, value);
	}

	/// <summary>Raised with the data coordinates under the mouse, or an empty string when outside a plot.</summary>
	public event EventHandler<string>? CoordinatesChanged;

	static void OnFigureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var view = (PlotView)d;
		if (e.OldValue is Figure old)
			old.Changed -= view.OnModelChanged;
		if (e.NewValue is Figure figure)
			figure.Changed += view.OnModelChanged;
	}

	void OnModelChanged(object? sender, EventArgs e) => Interlocked.Exchange(ref dirty, 1);

	static T Frozen<T>(T freezable) where T : Freezable
	{
		freezable.Freeze();
		return freezable;
	}

	(Brush Brush, Pen Pen) PenFor(int pen)
	{
		pen = Pens.Normalize(pen);
		if (!pens.TryGetValue(pen, out var value))
		{
			uint rgb = Pens.ColorOf(pen);
			var brush = Frozen(new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)));
			value = (brush, Frozen(new Pen(brush, 1.5) { LineJoin = PenLineJoin.Round }));
			pens[pen] = value;
		}
		return value;
	}

	// ───────────── Layout ─────────────

	Rect CellRect(FigureSnapshot snapshot, int row, int column)
	{
		double width = ActualWidth / snapshot.Columns, height = ActualHeight / snapshot.Rows;
		return new Rect(column * width, row * height, width, height);
	}

	static Rect PlotRect(Rect cell) => new(
		cell.Left + MarginLeft,
		cell.Top + MarginTop,
		System.Math.Max(1, cell.Width - MarginLeft - MarginRight),
		System.Math.Max(1, cell.Height - MarginTop - MarginBottom));

	static PlotTransform TransformFor(Axes axes, Rect plot) => new(axes, new ScreenRect(plot.Left, plot.Top, plot.Width, plot.Height));

	// ───────────── Rendering ─────────────

	protected override void OnRender(DrawingContext dc)
	{
		dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
		if (Figure is null || ActualWidth < 10 || ActualHeight < 10)
			return;

		var snapshot = lastSnapshot = Figure.Snapshot();
		bool showCurrent = snapshot.Rows * snapshot.Columns > 1;
		for (int r = 0; r < snapshot.Rows; r++)
			for (int c = 0; c < snapshot.Columns; c++)
			{
				bool current = showCurrent && r == snapshot.CurrentRow && c == snapshot.CurrentColumn;
				RenderAxes(dc, snapshot[r, c], CellRect(snapshot, r, c), current);
			}
	}

	void RenderAxes(DrawingContext dc, Axes axes, Rect cell, bool isCurrent)
	{
		var plot = PlotRect(cell);
		if (plot.Width < 20 || plot.Height < 20)
			return;
		var t = TransformFor(axes, plot);
		double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

		var xTicks = AxisTicks.Compute(axes.XMin, axes.XMax, System.Math.Max(2, (int)(plot.Width / 90)));
		var yTicks = AxisTicks.Compute(axes.YMin, axes.YMax, System.Math.Max(2, (int)(plot.Height / 50)));

		if (axes.ShowGrid)
		{
			foreach (var x in xTicks)
				dc.DrawLine(GridPen, new Point(t.ToScreenX(x), plot.Top), new Point(t.ToScreenX(x), plot.Bottom));
			foreach (var y in yTicks)
				dc.DrawLine(GridPen, new Point(plot.Left, t.ToScreenY(y)), new Point(plot.Right, t.ToScreenY(y)));
		}

		// The axes through the origin, as the original Draw_Origo.
		if (axes.XMin < 0 && axes.XMax > 0)
			dc.DrawLine(OriginPen, new Point(t.ToScreenX(0), plot.Top), new Point(t.ToScreenX(0), plot.Bottom));
		if (axes.YMin < 0 && axes.YMax > 0)
			dc.DrawLine(OriginPen, new Point(plot.Left, t.ToScreenY(0)), new Point(plot.Right, t.ToScreenY(0)));

		dc.PushClip(new RectangleGeometry(plot));
		foreach (var item in axes.Items)
			RenderItem(dc, item, t, plot);
		dc.Pop();

		dc.DrawRectangle(null, isCurrent ? CurrentFramePen : FramePen, plot);

		foreach (var x in xTicks)
		{
			double sx = t.ToScreenX(x);
			dc.DrawLine(TickPen, new Point(sx, plot.Bottom), new Point(sx, plot.Bottom + 4));
			var text = Label(FormatTick(x), 11, dpi);
			dc.DrawText(text, new Point(sx - text.Width / 2, plot.Bottom + 6));
		}
		foreach (var y in yTicks)
		{
			double sy = t.ToScreenY(y);
			dc.DrawLine(TickPen, new Point(plot.Left - 4, sy), new Point(plot.Left, sy));
			var text = Label(FormatTick(y), 11, dpi);
			dc.DrawText(text, new Point(plot.Left - 7 - text.Width, sy - text.Height / 2));
		}

		bool complex = axes.Kind == AxesKind.Complex;
		string xTitle = axes.XTitle.Length > 0 ? axes.XTitle : complex ? "Re" : "";
		string yTitle = axes.YTitle.Length > 0 ? axes.YTitle : complex ? "Im" : "";
		if (axes.Title.Length > 0)
		{
			var text = Label(axes.Title, 13, dpi, FontWeights.SemiBold);
			dc.DrawText(text, new Point(plot.Left + (plot.Width - text.Width) / 2, cell.Top + 6));
		}
		if (xTitle.Length > 0)
		{
			var text = Label(xTitle, 12, dpi);
			dc.DrawText(text, new Point(plot.Left + (plot.Width - text.Width) / 2, plot.Bottom + 22));
		}
		if (yTitle.Length > 0)
		{
			var text = Label(yTitle, 12, dpi);
			dc.PushTransform(new RotateTransform(-90, cell.Left + 4, plot.Top + (plot.Height + text.Width) / 2));
			dc.DrawText(text, new Point(cell.Left + 4, plot.Top + (plot.Height + text.Width) / 2));
			dc.Pop();
		}
	}

	void RenderItem(DrawingContext dc, PlotItem item, PlotTransform t, Rect plot)
	{
		var (brush, pen) = PenFor(item.Pen);
		switch (item)
		{
			case PointSeries { Style: PlotStyle.Line } series:
				dc.DrawGeometry(null, pen, Polyline(series.Points, t, plot, breakAtJumps: false));
				break;
			case FunctionSeries function:
				dc.DrawGeometry(null, pen, Polyline(function.Points, t, plot, breakAtJumps: true));
				break;
			case PointSeries series:
				if (series.Style == PlotStyle.Dot)
					dc.DrawGeometry(brush, null, Dots(series.Points, t, plot));
				else
					dc.DrawGeometry(null, pen, Markers(series.Points, t, plot, series.Style == PlotStyle.Star));
				break;
			case DotCloud cloud:
				dc.DrawGeometry(brush, null, Dots(cloud.Points, t, plot));
				break;
		}
	}

	/// <summary>
	/// A polyline that breaks at non-finite values. For functions it also breaks where the curve
	/// jumps from beyond one edge to beyond the other (a pole such as tan at π/2).
	/// </summary>
	static StreamGeometry Polyline(IReadOnlyList<PlotPoint> points, PlotTransform t, Rect plot, bool breakAtJumps)
	{
		var geometry = new StreamGeometry();
		using (var ctx = geometry.Open())
		{
			bool drawing = false;
			int previousSide = 0;
			foreach (var p in points)
			{
				if (!double.IsFinite(p.X) || !double.IsFinite(p.Y))
				{
					drawing = false;
					continue;
				}
				int side = p.Y > t.Axes.YMax ? 1 : p.Y < t.Axes.YMin ? -1 : 0;
				if (breakAtJumps && drawing && side != 0 && side == -previousSide)
					drawing = false;
				previousSide = side;

				var screen = Clamp(new Point(t.ToScreenX(p.X), t.ToScreenY(p.Y)), plot);
				if (drawing)
					ctx.LineTo(screen, true, false);
				else
				{
					ctx.BeginFigure(screen, false, false);
					drawing = true;
				}
			}
		}
		geometry.Freeze();
		return geometry;
	}

	/// <summary>Keeps far-away points at a finite distance; WPF geometry misbehaves with huge coordinates.</summary>
	static Point Clamp(Point p, Rect plot)
	{
		double margin = 10 * System.Math.Max(plot.Width, plot.Height);
		return new Point(
			System.Math.Clamp(p.X, plot.Left - margin, plot.Right + margin),
			System.Math.Clamp(p.Y, plot.Top - margin, plot.Bottom + margin));
	}

	static StreamGeometry Markers(IEnumerable<PlotPoint> points, PlotTransform t, Rect plot, bool star)
	{
		var geometry = new StreamGeometry();
		using (var ctx = geometry.Open())
		{
			foreach (var p in Visible(points, t, plot))
			{
				Segment(ctx, p.X - MarkerSize, p.Y, p.X + MarkerSize, p.Y);
				Segment(ctx, p.X, p.Y - MarkerSize, p.X, p.Y + MarkerSize);
				if (star)
				{
					double d = MarkerSize * 0.75;
					Segment(ctx, p.X - d, p.Y - d, p.X + d, p.Y + d);
					Segment(ctx, p.X - d, p.Y + d, p.X + d, p.Y - d);
				}
			}
		}
		geometry.Freeze();
		return geometry;
	}

	static void Segment(StreamGeometryContext ctx, double x1, double y1, double x2, double y2)
	{
		ctx.BeginFigure(new Point(x1, y1), false, false);
		ctx.LineTo(new Point(x2, y2), true, false);
	}

	static StreamGeometry Dots(IEnumerable<PlotPoint> points, PlotTransform t, Rect plot)
	{
		const double Size = 2.2;
		// Nonzero, not the default even-odd rule: otherwise an even number of dots at the same
		// place (a converged iteration) cancel out and disappear.
		var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
		using (var ctx = geometry.Open())
		{
			foreach (var p in Visible(points, t, plot))
			{
				ctx.BeginFigure(new Point(p.X - Size / 2, p.Y - Size / 2), true, true);
				ctx.PolyLineTo([new Point(p.X + Size / 2, p.Y - Size / 2), new Point(p.X + Size / 2, p.Y + Size / 2), new Point(p.X - Size / 2, p.Y + Size / 2)], false, false);
			}
		}
		geometry.Freeze();
		return geometry;
	}

	static IEnumerable<Point> Visible(IEnumerable<PlotPoint> points, PlotTransform t, Rect plot)
	{
		var bounds = plot;
		bounds.Inflate(MarkerSize, MarkerSize);
		foreach (var p in points)
		{
			if (!double.IsFinite(p.X) || !double.IsFinite(p.Y))
				continue;
			var screen = new Point(t.ToScreenX(p.X), t.ToScreenY(p.Y));
			if (bounds.Contains(screen))
				yield return screen;
		}
	}

	FormattedText Label(string text, double size, double pixelsPerDip, FontWeight? weight = null) =>
		new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
			weight is { } w ? new Typeface(LabelFace.FontFamily, FontStyles.Normal, w, FontStretches.Normal) : LabelFace,
			size, TextBrush, pixelsPerDip);

	static string FormatTick(double value) => value.ToString("G6", CultureInfo.InvariantCulture);

	// ───────────── Mouse coordinates ─────────────

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		var snapshot = lastSnapshot;
		if (snapshot is null)
			return;
		var position = e.GetPosition(this);
		for (int r = 0; r < snapshot.Rows; r++)
			for (int c = 0; c < snapshot.Columns; c++)
			{
				var plot = PlotRect(CellRect(snapshot, r, c));
				if (!plot.Contains(position))
					continue;
				var axes = snapshot[r, c];
				var t = TransformFor(axes, plot);
				string x = t.ToWorldX(position.X).ToString("G5", CultureInfo.InvariantCulture);
				string y = t.ToWorldY(position.Y).ToString("G5", CultureInfo.InvariantCulture);
				string grid = snapshot.Rows * snapshot.Columns > 1 ? $"window ({r + 1},{c + 1})   " : "";
				CoordinatesChanged?.Invoke(this, axes.Kind == AxesKind.Complex ? $"{grid}Re = {x}   Im = {y}" : $"{grid}x = {x}   y = {y}");
				return;
			}
		CoordinatesChanged?.Invoke(this, "");
	}

	protected override void OnMouseLeave(MouseEventArgs e)
	{
		base.OnMouseLeave(e);
		CoordinatesChanged?.Invoke(this, "");
	}

	/// <summary>Makes the whole area hit-testable so mouse moves are received.</summary>
	protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) =>
		new PointHitTestResult(this, hitTestParameters.HitPoint);
}
