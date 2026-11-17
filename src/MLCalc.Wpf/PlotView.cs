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
	readonly Dictionary<int, Pen> thinPens = [];
	readonly DispatcherTimer timer;
	readonly DispatcherTimer wheelTimer;
	int dirty;
	FigureSnapshot? lastSnapshot;

	/// <summary>What each window was last drawn from, so that windows that have not changed are not drawn again.</summary>
	Dictionary<int, CellDrawing> drawn = [];

	/// <summary>The camera drag in progress, if any.</summary>
	CameraDrag? drag;

	/// <summary>The 3D window being turned or moved with the mouse; drawn as a quick preview until the mouse stops.</summary>
	(int Row, int Column)? previewWindow;

	sealed record CellDrawing(Axes Axes, Rect Cell, bool IsCurrent, bool Preview, double Dpi, Drawing Drawing);

	sealed record CameraDrag(int Row, int Column, Point Start, double Azimuth, double Elevation);

	public PlotView()
	{
		timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Render, (_, _) =>
		{
			if (Interlocked.Exchange(ref dirty, 0) == 1)
				InvalidateVisual();
		}, Dispatcher);
		timer.Stop();
		// The full drawing after the mouse wheel has been still for a moment.
		wheelTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) =>
		{
			wheelTimer!.Stop();
			if (drag is null)
				EndPreview();
		}, Dispatcher);
		wheelTimer.Stop();
		Loaded += (_, _) => timer.Start();
		Unloaded += (_, _) =>
		{
			timer.Stop();
			wheelTimer.Stop();
		};
		ClipToBounds = true;
	}

	public Figure? Figure
	{
		get => (Figure?)GetValue(FigureProperty);
		set => SetValue(FigureProperty, value);
	}

	/// <summary>When set, this snapshot is drawn instead of the live <see cref="Figure"/> (used for exporting).</summary>
	public FigureSnapshot? FixedSnapshot { get; init; }

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

	Rect CellRect(FigureSnapshot snapshot, int row, int column) =>
		ToRect(PlotLayout.Cell(snapshot, row, column, ActualWidth, ActualHeight));

	static Rect PlotRect(Rect cell) => ToRect(PlotLayout.PlotArea(new ScreenRect(cell.Left, cell.Top, cell.Width, cell.Height)));

	static Rect ToRect(ScreenRect r) => new(r.Left, r.Top, r.Width, r.Height);

	static ScreenRect ToScreenRect(Rect r) => new(r.Left, r.Top, r.Width, r.Height);

	static PlotTransform TransformFor(Axes axes, Rect plot) => new(axes, new ScreenRect(plot.Left, plot.Top, plot.Width, plot.Height));

	// ───────────── Rendering ─────────────

	protected override void OnRender(DrawingContext dc)
	{
		dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
		var snapshot = FixedSnapshot ?? Figure?.Snapshot();
		if (snapshot is null || ActualWidth < 10 || ActualHeight < 10)
			return;

		lastSnapshot = snapshot;
		bool showCurrent = snapshot.Rows * snapshot.Columns > 1;
		double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		var cells = new Dictionary<int, CellDrawing>();
		for (int r = 0; r < snapshot.Rows; r++)
			for (int c = 0; c < snapshot.Columns; c++)
			{
				int index = r * snapshot.Columns + c;
				var axes = snapshot[r, c];
				var cell = CellRect(snapshot, r, c);
				bool current = showCurrent && r == snapshot.CurrentRow && c == snapshot.CurrentColumn;
				bool preview = previewWindow == (r, c);
				// Axes are immutable and a snapshot shares the ones that have not changed, so the same
				// object in the same place looks the same.
				if (!drawn.TryGetValue(index, out var cached) || !ReferenceEquals(cached.Axes, axes) || cached.Cell != cell
					|| cached.IsCurrent != current || cached.Preview != preview || cached.Dpi != dpi)
				{
					var drawing = new DrawingGroup();
					using (var context = drawing.Open())
						RenderAxes(context, axes, cell, current, preview);
					drawing.Freeze();
					cached = new CellDrawing(axes, cell, current, preview, dpi, drawing);
				}
				cells[index] = cached;
				dc.DrawDrawing(cached.Drawing);
			}
		drawn = cells;
	}

	/// <summary>A 3D window: the primitives from <see cref="Scene3D"/>, in order.</summary>
	void RenderSpace(DrawingContext dc, Axes axes, Rect cell, bool isCurrent, bool preview)
	{
		double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		dc.PushClip(new RectangleGeometry(cell));
		foreach (var primitive in Scene3D.Build(axes, ToScreenRect(cell), preview))
			switch (primitive)
			{
				case ScenePolyline line:
				{
					var pen = line.Stroke switch
					{
						SceneStroke.Grid => GridPen,
						SceneStroke.Frame => FramePen,
						SceneStroke.Tick => TickPen,
						_ => PenFor(line.Pen).Pen,
					};
					dc.DrawGeometry(null, pen, Polygon(line.Points, close: false));
					break;
				}
				case ScenePolygon polygon:
					dc.DrawGeometry(Background, ThinPenFor(polygon.Pen), Polygon(polygon.Points, close: true));
					break;
				case SceneMarkers markers:
				{
					var (brush, pen) = PenFor(markers.Pen);
					var points = markers.Points.Select(p => new Point(p.X, p.Y));
					if (markers.Style == PlotStyle.Dot)
						dc.DrawGeometry(brush, null, Dots(points));
					else
						dc.DrawGeometry(null, pen, Markers(points, markers.Style == PlotStyle.Star));
					break;
				}
				case SceneText text:
				{
					var formatted = Label(text.Text, text.Size, dpi, text.Bold ? FontWeights.SemiBold : null);
					double x = text.Align switch
					{
						TextAlign.Start => text.At.X,
						TextAlign.End => text.At.X - formatted.Width,
						_ => text.At.X - formatted.Width / 2,
					};
					dc.DrawText(formatted, new Point(x, text.At.Y - formatted.Height / 2));
					break;
				}
			}
		dc.Pop();
		if (isCurrent)
		{
			var outline = cell;
			outline.Inflate(-3, -3);
			if (outline.Width > 0 && outline.Height > 0)
				dc.DrawRectangle(null, CurrentFramePen, outline);
		}
	}

	static StreamGeometry Polygon(IReadOnlyList<ScreenPoint> points, bool close)
	{
		var geometry = new StreamGeometry();
		using (var ctx = geometry.Open())
		{
			ctx.BeginFigure(new Point(points[0].X, points[0].Y), close, close);
			ctx.PolyLineTo([.. points.Skip(1).Select(p => new Point(p.X, p.Y))], true, true);
		}
		geometry.Freeze();
		return geometry;
	}

	/// <summary>The pen's colour, 1 pixel wide: the lines of a surface's mesh.</summary>
	Pen ThinPenFor(int pen)
	{
		pen = Pens.Normalize(pen);
		if (!thinPens.TryGetValue(pen, out var value))
		{
			value = Frozen(new Pen(PenFor(pen).Brush, 1) { LineJoin = PenLineJoin.Round });
			thinPens[pen] = value;
		}
		return value;
	}

	void RenderAxes(DrawingContext dc, Axes axes, Rect cell, bool isCurrent, bool preview)
	{
		if (axes.Kind == AxesKind.Space)
		{
			RenderSpace(dc, axes, cell, isCurrent, preview);
			return;
		}
		var plot = PlotRect(cell);
		if (plot.Width < 20 || plot.Height < 20)
			return;
		var t = TransformFor(axes, plot);
		double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

		var xTicks = AxisTicks.Compute(axes.XMin, axes.XMax, Math.Max(2, (int)(plot.Width / 90)));
		var yTicks = AxisTicks.Compute(axes.YMin, axes.YMax, Math.Max(2, (int)(plot.Height / 50)));

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
			var text = Label(PlotLayout.FormatTick(x), 11, dpi);
			dc.DrawText(text, new Point(sx - text.Width / 2, plot.Bottom + 6));
		}
		foreach (var y in yTicks)
		{
			double sy = t.ToScreenY(y);
			dc.DrawLine(TickPen, new Point(plot.Left - 4, sy), new Point(plot.Left, sy));
			var text = Label(PlotLayout.FormatTick(y), 11, dpi);
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

	/// <summary>A polyline split into runs by <see cref="PlotLayout.Segments"/> (gaps, poles).</summary>
	static StreamGeometry Polyline(IReadOnlyList<PlotPoint> points, PlotTransform t, Rect plot, bool breakAtJumps)
	{
		var bounds = new ScreenRect(plot.Left, plot.Top, plot.Width, plot.Height);
		var geometry = new StreamGeometry();
		using (var ctx = geometry.Open())
		{
			foreach (var segment in PlotLayout.Segments(points, t.Axes, breakAtJumps))
				for (int i = 0; i < segment.Count; i++)
				{
					var (x, y) = PlotLayout.Clamp(t.ToScreenX(segment[i].X), t.ToScreenY(segment[i].Y), bounds);
					if (i == 0)
						ctx.BeginFigure(new Point(x, y), false, false);
					else
						ctx.LineTo(new Point(x, y), true, false);
				}
		}
		geometry.Freeze();
		return geometry;
	}

	static StreamGeometry Markers(IEnumerable<PlotPoint> points, PlotTransform t, Rect plot, bool star) =>
		Markers(Visible(points, t, plot), star);

	static StreamGeometry Markers(IEnumerable<Point> points, bool star)
	{
		var geometry = new StreamGeometry();
		using (var ctx = geometry.Open())
		{
			foreach (var p in points)
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

	static StreamGeometry Dots(IEnumerable<PlotPoint> points, PlotTransform t, Rect plot) => Dots(Visible(points, t, plot));

	static StreamGeometry Dots(IEnumerable<Point> points)
	{
		const double Size = 2.2;
		// Nonzero, not the default even-odd rule: otherwise an even number of dots at the same
		// place (a converged iteration) cancel out and disappear.
		var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
		using (var ctx = geometry.Open())
		{
			foreach (var p in points)
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


	// ───────────── Mouse: coordinates and the 3D camera ─────────────

	/// <summary>The window under a point: zero-based row and column, its rectangle and axes.</summary>
	bool TryFindWindow(FigureSnapshot snapshot, Point position, out int row, out int column, out Rect cell)
	{
		for (row = 0; row < snapshot.Rows; row++)
			for (column = 0; column < snapshot.Columns; column++)
			{
				cell = CellRect(snapshot, row, column);
				if (cell.Contains(position))
					return true;
			}
		row = column = -1;
		cell = Rect.Empty;
		return false;
	}

	/// <summary>The 3D window under the mouse, when the view is live (not drawing a fixed snapshot for export).</summary>
	bool TryFind3DWindow(Point position, out Figure figure, out int row, out int column, out Rect cell)
	{
		figure = Figure!;
		row = column = -1;
		cell = Rect.Empty;
		return FixedSnapshot is null && Figure is not null && lastSnapshot is { } snapshot
			&& TryFindWindow(snapshot, position, out row, out column, out cell) && snapshot[row, column].Kind == AxesKind.Space;
	}

	/// <summary>Shows the camera of a 3D window in the status bar, as the command that sets it.</summary>
	void ReportCamera(FigureSnapshot snapshot, int row, int column, Rect cell)
	{
		if (row >= snapshot.Rows || column >= snapshot.Columns)
			return;
		string grid = snapshot.Rows * snapshot.Columns > 1 ? $"window ({row + 1},{column + 1})   " : "";
		CoordinatesChanged?.Invoke(this, grid + CameraControl.Describe(snapshot[row, column], ToScreenRect(cell)));
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e);
		var position = e.GetPosition(this);
		if (!TryFind3DWindow(position, out var figure, out int row, out int column, out var cell))
			return;
		e.Handled = true;
		if (e.ClickCount == 2)
		{
			// Double-click: back to the default camera, as view(3).
			if (IsMouseCaptured)
				ReleaseMouseCapture();
			figure.ResetView(row, column);
			ReportCamera(figure.Snapshot(), row, column, cell);
			return;
		}
		var (azimuth, elevation) = Projection3D.ViewAngles(lastSnapshot![row, column]);
		drag = new CameraDrag(row, column, position, azimuth, elevation);
		previewWindow = (row, column);
		Cursor = Cursors.SizeAll;
		CaptureMouse();
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonUp(e);
		if (drag is not null)
		{
			e.Handled = true;
			ReleaseMouseCapture();
		}
	}

	protected override void OnLostMouseCapture(MouseEventArgs e)
	{
		base.OnLostMouseCapture(e);
		if (drag is null)
			return;
		drag = null;
		Cursor = null;
		EndPreview();
	}

	/// <summary>Draws the window that was being turned or moved in full again.</summary>
	void EndPreview()
	{
		if (previewWindow is null)
			return;
		previewWindow = null;
		InvalidateVisual();
	}

	protected override void OnMouseWheel(MouseWheelEventArgs e)
	{
		base.OnMouseWheel(e);
		if (!TryFind3DWindow(e.GetPosition(this), out var figure, out int row, out int column, out var cell))
			return;
		e.Handled = true;
		if (!figure.MoveCamera(row, column, CameraControl.WheelFactor(e.Delta), ToScreenRect(cell)))
			return;
		if (drag is null)
			previewWindow = (row, column);
		wheelTimer.Stop();
		wheelTimer.Start();
		ReportCamera(figure.Snapshot(), row, column, cell);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		var position = e.GetPosition(this);
		if (drag is { } d && Figure is { } figure)
		{
			var (azimuth, elevation) = CameraControl.Drag(d.Azimuth, d.Elevation, position.X - d.Start.X, position.Y - d.Start.Y);
			// False when the window is gone or no longer 3D (a script changed it): stop turning it.
			if (!figure.SetView(d.Row, d.Column, azimuth, elevation))
			{
				ReleaseMouseCapture();
				return;
			}
			var snapshot = figure.Snapshot();
			ReportCamera(snapshot, d.Row, d.Column, CellRect(snapshot, d.Row, d.Column));
			return;
		}
		if (lastSnapshot is not { } last || !TryFindWindow(last, position, out int r, out int c, out var cell))
		{
			CoordinatesChanged?.Invoke(this, "");
			return;
		}
		var axes = last[r, c];
		if (axes.Kind == AxesKind.Space)
		{
			// A point on the screen is a whole line of points in 3D; show the camera instead.
			ReportCamera(last, r, c, cell);
			return;
		}
		var plot = PlotRect(cell);
		if (!plot.Contains(position))
		{
			CoordinatesChanged?.Invoke(this, "");
			return;
		}
		var t = TransformFor(axes, plot);
		string x = t.ToWorldX(position.X).ToString("G5", CultureInfo.InvariantCulture);
		string y = t.ToWorldY(position.Y).ToString("G5", CultureInfo.InvariantCulture);
		string grid = last.Rows * last.Columns > 1 ? $"window ({r + 1},{c + 1})   " : "";
		CoordinatesChanged?.Invoke(this, axes.Kind == AxesKind.Complex ? $"{grid}Re = {x}   Im = {y}" : $"{grid}x = {x}   y = {y}");
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
