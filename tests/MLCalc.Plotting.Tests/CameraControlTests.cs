namespace MLCalc.Plotting.Tests;

/// <summary>Turning and moving a 3D camera with the mouse.</summary>
public class CameraControlTests
{
	static readonly ScreenRect Cell = new(0, 0, 400, 300);

	/// <summary>A 2×2 grid whose window (1,2) (zero-based (0,1)) is 3D; the current window stays (1,1).</summary>
	static Figure GridWith3DWindow()
	{
		var figure = new Figure();
		figure.Initialize(2, 2);
		figure.SelectAxes(1, 2);
		figure.AddDot3D(0, 0, 0);
		figure.SelectAxes(1, 1);
		return figure;
	}

	[Theory]
	[InlineData(0, 0, -37.5, 30)]
	[InlineData(10, 0, -42.5, 30)]     // right: the front of the box moves right
	[InlineData(0, 10, -37.5, 35)]     // down: the top tips towards you
	[InlineData(0, 200, -37.5, 90)]    // stops straight above
	[InlineData(0, -300, -37.5, -90)]  // and straight below
	[InlineData(-440, 0, -177.5, 30)]
	[InlineData(-435, 0, 180, 30)]
	[InlineData(285, 0, 180, 30)]      // -180 is written 180
	[InlineData(-445, 0, -175, 30)]    // the azimuth wraps around
	[InlineData(0.26, -0.26, -37.6, 29.9)] // rounded to tenths
	public void DraggingTurnsTheCamera(double dx, double dy, double azimuth, double elevation)
	{
		Assert.Equal((azimuth, elevation), CameraControl.Drag(Camera.DefaultAzimuth, Camera.DefaultElevation, dx, dy));
	}

	[Fact]
	public void TheWheelMovesCloserWhenTurnedAway()
	{
		Assert.Equal(CameraControl.WheelStep, CameraControl.WheelFactor(120), 12);
		Assert.Equal(1 / CameraControl.WheelStep, CameraControl.WheelFactor(-120), 12);
		Assert.Equal(1, CameraControl.WheelFactor(0));
	}

	[Fact]
	public void SetViewChangesTheGivenWindowNotTheCurrentOne()
	{
		var figure = GridWith3DWindow();
		Assert.True(figure.SetView(0, 1, 20, 10));
		var snapshot = figure.Snapshot();
		Assert.Equal((20, 10), (snapshot[0, 1].Camera.Azimuth, snapshot[0, 1].Camera.Elevation));
		Assert.Equal(Camera.Default, snapshot[0, 0].Camera);
		Assert.Equal((0, 0), (snapshot.CurrentRow, snapshot.CurrentColumn));
	}

	[Fact]
	public void OnlyExisting3DWindowsCanBeTurned()
	{
		var figure = GridWith3DWindow();
		long version = figure.Snapshot().Version;
		Assert.False(figure.SetView(0, 0, 20, 10));   // a 2D window
		Assert.False(figure.SetView(2, 0, 20, 10));   // outside the grid
		Assert.False(figure.ResetView(-1, 0));
		Assert.False(figure.MoveCamera(1, 1, 0.5, Cell));
		Assert.Equal(version, figure.Snapshot().Version);
		Assert.Throws<ArgumentException>(() => figure.SetView(0, 1, 0, 91));
		Assert.Throws<ArgumentOutOfRangeException>(() => figure.MoveCamera(0, 1, 0, Cell));
	}

	[Fact]
	public void ResetViewForAWindow()
	{
		var figure = GridWith3DWindow();
		figure.SetView(0, 1, 20, 10);
		Assert.True(figure.ResetView(0, 1));
		Assert.Equal(Camera.Default, figure.Snapshot()[0, 1].Camera);
	}

	[Fact]
	public void TheWheelGivesTheDefaultCameraADistance()
	{
		var figure = GridWith3DWindow();
		double fitted = new Projection3D(figure.Snapshot()[0, 1], Scene3D.ViewArea(Cell)).DistanceToTarget;
		Assert.True(figure.MoveCamera(0, 1, 0.5, Cell));
		var camera = figure.Snapshot()[0, 1].Camera;
		Assert.Equal(fitted * 0.5, camera.Distance!.Value, 9);
		Assert.Equal((Camera.DefaultAzimuth, Camera.DefaultElevation, null), (camera.Azimuth, camera.Elevation, camera.Position));
	}

	[Fact]
	public void TheWheelStopsCloseToTheTargetAndFarAway()
	{
		var figure = GridWith3DWindow();
		for (int k = 0; k < 100; k++)
			figure.MoveCamera(0, 1, 0.5, Cell);
		Assert.Equal(Figure.MinCameraDistance, figure.Snapshot()[0, 1].Camera.Distance);
		for (int k = 0; k < 100; k++)
			figure.MoveCamera(0, 1, 2, Cell);
		Assert.Equal(Figure.MaxCameraDistance, figure.Snapshot()[0, 1].Camera.Distance);
	}

	[Fact]
	public void TheWheelMovesAPlacedCameraAlongTheLineToItsTarget()
	{
		var figure = new Figure();
		figure.AddDot3D(0, 0, 0);
		figure.SetCameraTarget(new Point3(0, 0, 5));
		figure.SetCameraPosition(new Point3(40, 0, 5));
		Assert.True(figure.MoveCamera(0, 0, 0.5, Cell));
		var camera = figure.Current.Camera;
		var p = camera.Position!.Value;
		Assert.Equal(20, p.X, 9);
		Assert.Equal(0, p.Y, 9);
		Assert.Equal(5, p.Z, 9);
		Assert.Null(camera.Distance);
	}

	[Fact]
	public void TurningAPlacedCameraStartsFromWhereItIs()
	{
		var figure = new Figure();
		figure.AddDot3D(0, 0, 0);
		// Straight out along +x, a little above: azimuth 90.
		figure.SetCameraPosition(new Point3(30, 0, 30));
		var (azimuth, elevation) = Projection3D.ViewAngles(figure.Current);
		Assert.Equal(90, azimuth, 9);
		Assert.Equal(45, elevation, 9);
		Assert.Equal((Camera.DefaultAzimuth, Camera.DefaultElevation), Projection3D.ViewAngles(Axes.Default));

		// The first turn makes it a camera placed relative to the box, at the same distance.
		double distance = Projection3D.CameraDistance(figure.Current);
		figure.SetView(0, 0, azimuth, elevation);
		var camera = figure.Current.Camera;
		Assert.Null(camera.Position);
		Assert.Equal(distance, camera.Distance!.Value, 9);
		var eye = new Projection3D(figure.Current, Scene3D.ViewArea(Cell)).CameraPosition;
		Assert.Equal(30, eye.X, 9);
		Assert.Equal(0, eye.Y, 9);
		Assert.Equal(30, eye.Z, 9);
	}

	[Fact]
	public void TheStatusBarShowsTheCommandForTheCamera()
	{
		var axes = Axes.Default with { Kind = AxesKind.Space };
		Assert.Equal("view(-37.5, 30)", CameraControl.Describe(axes, Cell));
		Assert.Equal("view(0, -90)", CameraControl.Describe(axes with { Camera = Camera.Default with { Azimuth = -0.0, Elevation = -90 } }, Cell));
		var placed = axes with { Camera = Camera.Default with { Position = new Point3(30, -40, 12.25) } };
		Assert.Equal("camposition(30, -40, 12.25)", CameraControl.Describe(placed, Cell));
		var moved = axes with { Camera = new Camera(90, 0, 4, null, null) };
		Assert.Equal("camposition(40, 0, 0)", CameraControl.Describe(moved, Cell));
	}

	[Fact]
	public void APreviewThinsLargeSurfacesCurvesAndClouds()
	{
		var figure = new Figure();
		figure.AddSurface("f", (x, y) => x + y, 200);
		var curve = Enumerable.Range(0, 50_001).Select(k => k == 30_000 ? new Point3(double.NaN, 0, 0) : new Point3(k / 5000.0, 0, 0)).ToArray();
		figure.AddSeries3D(curve, PlotStyle.Line);
		for (int k = 0; k < 30_000; k++)
			figure.AddDot3D(0, 0, k / 3000.0);

		var preview = Scene3D.Preview(figure.Current);
		var surface = (Surface)preview.Items[0];
		Assert.InRange(surface.XCount, 2, Scene3D.PreviewGridLines);
		Assert.Equal(surface.XCount, surface.YCount);
		// The whole range is still covered.
		Assert.Equal(new Point3(-10, -10, -20), surface[0, 0]);
		Assert.Equal(new Point3(10, 10, 20), surface[surface.XCount - 1, surface.YCount - 1]);
		Assert.Equal(surface.XCount * surface.YCount, surface.Points.Length);

		var thinned = ((PointSeries3D)preview.Items[1]).Points;
		Assert.InRange(thinned.Length, 2, Scene3D.PreviewPoints + 2);
		Assert.Equal(curve[^1], thinned[^1]);
		Assert.Contains(thinned, p => !p.IsFinite);   // the gap stays

		Assert.InRange(((DotCloud3D)preview.Items[2]).Points.Count, 1, Scene3D.PreviewPoints);

		Assert.True(Scene3D.Build(figure.Current, Cell, preview: true).Length < Scene3D.Build(figure.Current, Cell).Length / 10);
	}

	[Fact]
	public void APreviewLeavesSmallItemsAlone()
	{
		var figure = new Figure();
		figure.AddSurface("f", (x, y) => x * y, Scene3D.PreviewGridLines);
		figure.AddSeries3D([new(0, 0, 0), new(1, 1, 1)], PlotStyle.Line);
		var axes = figure.Current;
		var preview = Scene3D.Preview(axes).Items;
		Assert.Equal(axes.Items.Length, preview.Length);
		for (int k = 0; k < preview.Length; k++)
			Assert.Same(axes.Items[k], preview[k]);
		Assert.Equal(Scene3D.Build(axes, Cell).Length, Scene3D.Build(axes, Cell, preview: true).Length);
	}
}
