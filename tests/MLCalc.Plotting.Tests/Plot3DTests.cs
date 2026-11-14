namespace MLCalc.Plotting.Tests;

public class Figure3DTests
{
	static readonly Point3[] Line = [new(0, 0, 0), new(1, 1, 1)];

	[Fact]
	public void A3DPlotSwitchesTheWindowTo3DAndClearsIt()
	{
		var figure = new Figure();
		figure.AddSeries([new(0, 0)], PlotStyle.Line);
		figure.AddSeries3D(Line, PlotStyle.Line);
		Assert.Equal(AxesKind.Space, figure.Current.Kind);
		Assert.IsType<PointSeries3D>(Assert.Single(figure.Current.Items));
		// The pen is kept: plot used pen 1, plot3d pen 2.
		Assert.Equal(2, ((PointSeries3D)figure.Current.Items[0]).Pen);
		Assert.Equal(3, figure.Current.Pen);
	}

	[Fact]
	public void APenChosenBeforeTheFirst3DCommandIsUsed()
	{
		var figure = new Figure();
		figure.SetPen(5);
		figure.AddDot3D(1, 2, 3);
		Assert.Equal(5, figure.Current.Items[0].Pen);
		figure.SetPen(7);
		figure.AddComplexSeries([new(1, 1)], PlotStyle.Cross);
		Assert.Equal(7, figure.Current.Items[0].Pen);
	}

	[Fact]
	public void A2DPlotSwitchesA3DWindowBackAndClearsIt()
	{
		var figure = new Figure();
		figure.AddSeries3D(Line, PlotStyle.Line);
		figure.AddFunction("x", x => x);
		Assert.Equal(AxesKind.Cartesian, figure.Current.Kind);
		Assert.IsType<FunctionSeries>(Assert.Single(figure.Current.Items));

		figure.AddDot3D(1, 2, 3);
		figure.AddDot(1, 2);
		Assert.Equal(AxesKind.Cartesian, figure.Current.Kind);
		Assert.IsType<DotCloud>(Assert.Single(figure.Current.Items));
	}

	[Fact]
	public void Dots3DWithTheSamePenAreCollectedWithoutAdvancingThePen()
	{
		var figure = new Figure();
		figure.AddDot3D(1, 2, 3);
		figure.AddDot3D(4, 5, 6);
		var cloud = Assert.IsType<DotCloud3D>(Assert.Single(figure.Current.Items));
		Assert.Equal([new Point3(1, 2, 3), new Point3(4, 5, 6)], cloud.Points);
		Assert.Equal(1, figure.Current.Pen);
		Assert.Equal(2, figure.Current.PointCount);
	}

	[Fact]
	public void ZRangeClearsTheWindowAndIsValidated()
	{
		var figure = new Figure();
		figure.AddSeries3D(Line, PlotStyle.Line);
		figure.SetZRange(0, 5);
		Assert.Empty(figure.Current.Items);
		Assert.Equal((0, 5), (figure.Current.ZMin, figure.Current.ZMax));
		Assert.Equal(AxesKind.Space, figure.Current.Kind);
		Assert.Throws<ArgumentException>(() => figure.SetZRange(1, 1));
		Assert.Throws<ArgumentException>(() => figure.SetZRange(0, double.PositiveInfinity));
	}

	[Fact]
	public void ClearKeepsTheRangesAndTheCamera()
	{
		var figure = new Figure();
		figure.SetZRange(0, 5);
		figure.SetView(30, 40);
		figure.AddSeries3D(Line, PlotStyle.Line);
		figure.ClearCurrent();
		Assert.Equal((0, 5), (figure.Current.ZMin, figure.Current.ZMax));
		Assert.Equal((30, 40), (figure.Current.Camera.Azimuth, figure.Current.Camera.Elevation));
		Assert.Equal(AxesKind.Space, figure.Current.Kind);
	}

	[Fact]
	public void SurfaceSamplesTheFunctionOverTheRanges()
	{
		var figure = new Figure();
		figure.SetXRange(0, 3);
		figure.SetYRange(-1, 1);
		figure.AddSurface("x*y", (x, y) => x * y, 4);
		var surface = Assert.IsType<Surface>(Assert.Single(figure.Current.Items));
		Assert.Equal((4, 4), (surface.XCount, surface.YCount));
		Assert.Equal(new Point3(0, -1, 0), surface[0, 0]);
		Assert.Equal(new Point3(3, -1, -3), surface[3, 0]);
		Assert.Equal(new Point3(3, 1, 3), surface[3, 3]);
		Assert.Equal(1, surface[1, 3].X, 12);
		Assert.Equal(16, figure.Current.PointCount);
	}

	[Fact]
	public void ASurfaceCountsAllItsPointsAgainstTheLimit()
	{
		var figure = new Figure(maxPointsPerWindow: 1000);
		int calls = 0;
		var error = Assert.Throws<ArgumentException>(() => figure.AddSurface("0", (_, _) => calls++, 40));
		Assert.Contains("at most 1000 points", error.Message);
		Assert.Equal(0, calls); // refused before sampling
		figure.AddSurface("0", (_, _) => 0, 30);
		Assert.Equal(900, figure.Current.PointCount);
	}

	[Fact]
	public void CameraIsRelativeByDefault()
	{
		var camera = new Figure().Current.Camera;
		Assert.Equal((-37.5, 30), (camera.Azimuth, camera.Elevation));
		Assert.Null(camera.Position);
		Assert.Null(camera.Target);
		Assert.Null(camera.Distance);
	}

	[Theory]
	[InlineData(0, 91)]
	[InlineData(0, -91)]
	[InlineData(double.NaN, 0)]
	[InlineData(0, double.NaN)]
	[InlineData(double.PositiveInfinity, 0)]
	public void ViewAnglesAreValidated(double azimuth, double elevation)
	{
		Assert.Throws<ArgumentException>(() => new Figure().SetView(azimuth, elevation));
	}

	[Fact]
	public void TheCameraCannotBeInsideTheBox()
	{
		var figure = new Figure();
		var error = Assert.Throws<ArgumentException>(() => figure.SetCameraPosition(new Point3(5, 5, 5)));
		Assert.Contains("outside the box", error.Message);
		Assert.Throws<ArgumentException>(() => figure.SetCameraPosition(new Point3(double.NaN, 50, 0)));
		figure.SetCameraPosition(new Point3(10.5, 0, 0));
		Assert.Equal(new Point3(10.5, 0, 0), figure.Current.Camera.Position);
	}

	[Fact]
	public void TheTargetCannotBeWhereTheCameraIs()
	{
		var figure = new Figure();
		figure.SetCameraPosition(new Point3(20, 0, 0));
		Assert.Throws<ArgumentException>(() => figure.SetCameraTarget(new Point3(20, 0, 0)));
		figure.SetCameraTarget(new Point3(0, 5, 0));
		Assert.Equal(new Point3(0, 5, 0), figure.Current.Camera.Target);
	}

	[Fact]
	public void AFixedCameraStaysWhereItIsWhenTheRangesChange()
	{
		var figure = new Figure();
		figure.SetCameraPosition(new Point3(30, -40, 20));
		figure.SetZRange(-100, 100);
		figure.SetXRange(-50, 50);
		Assert.Equal(new Point3(30, -40, 20), figure.Current.Camera.Position);
		// Now inside the box: still drawn, without an error.
		Assert.NotEmpty(Scene3D.Build(figure.Current with { Kind = AxesKind.Space }, new ScreenRect(0, 0, 400, 300)));
	}

	[Fact]
	public void ViewAfterAFixedCameraKeepsTheDistanceToTheTarget()
	{
		var figure = new Figure();
		figure.SetCameraPosition(new Point3(30, 0, 0)); // 3 half-boxes from the centre
		figure.SetView(45, 10);
		var camera = figure.Current.Camera;
		Assert.Null(camera.Position);
		Assert.Equal((45, 10), (camera.Azimuth, camera.Elevation));
		Assert.Equal(3, camera.Distance!.Value, 12);
	}

	[Fact]
	public void ResetViewRestoresTheDefaultCamera()
	{
		var figure = new Figure();
		figure.SetCameraPosition(new Point3(30, 0, 0));
		figure.SetCameraTarget(new Point3(1, 2, 3));
		figure.ResetView();
		Assert.Equal(Camera.Default, figure.Current.Camera);
	}
}

public class ProjectionTests
{
	static readonly ScreenRect Screen = new(100, 50, 400, 300);

	static Axes Space(Camera? camera = null) => Axes.Default with { Kind = AxesKind.Space, Camera = camera ?? Camera.Default };

	static ScreenPoint Project(Axes axes, double x, double y, double z) =>
		new Projection3D(axes, Screen).Project(new Point3(x, y, z)) ?? throw new Xunit.Sdk.XunitException("behind the camera");

	[Fact]
	public void TheTargetIsInTheMiddleOfTheScreen()
	{
		var centre = Project(Space(), 0, 0, 0);
		Assert.Equal(300, centre.X, 9);
		Assert.Equal(200, centre.Y, 9);
		var target = Space(Camera.Default with { Target = new Point3(5, -5, 2) });
		var p = Project(target, 5, -5, 2);
		Assert.Equal((300, 200), (System.Math.Round(p.X, 9), System.Math.Round(p.Y, 9)));
	}

	[Theory]
	[InlineData(-37.5, 30)]
	[InlineData(0, 0)]
	[InlineData(90, -45)]
	[InlineData(200, 89)]
	[InlineData(0, 90)]
	[InlineData(0, -90)]
	public void TheWholeBoxFitsOnTheScreen(double azimuth, double elevation)
	{
		var axes = Space(Camera.Default with { Azimuth = azimuth, Elevation = elevation });
		foreach (var x in new[] { -10.0, 10 })
			foreach (var y in new[] { -10.0, 10 })
				foreach (var z in new[] { -10.0, 10 })
				{
					var p = Project(axes, x, y, z);
					Assert.InRange(p.X, Screen.Left, Screen.Right);
					Assert.InRange(p.Y, Screen.Top, Screen.Bottom);
				}
	}

	[Fact]
	public void FromTheFrontXIsToTheRightAndZUp()
	{
		// view(0, 0): from −y towards +y.
		var axes = Space(Camera.Default with { Azimuth = 0, Elevation = 0 });
		var centre = Project(axes, 0, 0, 0);
		Assert.True(Project(axes, 5, 0, 0).X > centre.X + 10);
		Assert.True(Project(axes, 0, 0, 5).Y < centre.Y - 10);
		Assert.Equal(centre.X, Project(axes, 0, 5, 0).X, 9);
	}

	[Fact]
	public void FromTheSideYIsToTheRight()
	{
		// view(90, 0): from +x, as in MATLAB.
		var axes = Space(Camera.Default with { Azimuth = 90, Elevation = 0 });
		Assert.True(Project(axes, 0, 5, 0).X > Project(axes, 0, 0, 0).X + 10);
	}

	[Fact]
	public void FromAboveYIsUp()
	{
		// view(2): from straight above, with x to the right and y up, as in MATLAB.
		var axes = Space(Camera.Default with { Azimuth = 0, Elevation = 90 });
		var centre = Project(axes, 0, 0, 0);
		Assert.True(Project(axes, 5, 0, 0).X > centre.X + 10);
		Assert.True(Project(axes, 0, 5, 0).Y < centre.Y - 10);
	}

	[Fact]
	public void ThePerspectiveMakesNearerThingsLarger()
	{
		var axes = Space(Camera.Default with { Azimuth = 0, Elevation = 0 });
		// The front edge (y = −10) is nearer the camera than the back edge (y = 10).
		double front = Project(axes, 10, -10, 0).X - Project(axes, -10, -10, 0).X;
		double back = Project(axes, 10, 10, 0).X - Project(axes, -10, 10, 0).X;
		Assert.True(front > back * 1.05);
	}

	[Fact]
	public void ARelativeCameraFollowsTheRanges()
	{
		var small = Space();
		var large = small with { XMin = -100, XMax = 300, ZMin = 0, ZMax = 1 };
		var a = Project(small, 10, 10, 10);
		var b = Project(large, 300, 10, 1);
		Assert.Equal(a.X, b.X, 9);
		Assert.Equal(a.Y, b.Y, 9);
	}

	[Fact]
	public void PointsBehindTheCameraAreNotProjected()
	{
		var axes = Space(Camera.Default with { Position = new Point3(0, -30, 0) });
		Assert.NotNull(new Projection3D(axes, Screen).Project(new Point3(0, 0, 0)));
		Assert.Null(new Projection3D(axes, Screen).Project(new Point3(0, -40, 0)));
	}

	[Fact]
	public void AFixedCameraFurtherAwayMakesTheBoxSmaller()
	{
		double Width(double distance)
		{
			var axes = Space(Camera.Default with { Position = new Point3(0, -distance, 0) });
			return Project(axes, 10, 10, 0).X - Project(axes, -10, 10, 0).X;
		}
		Assert.True(Width(40) > 1.5 * Width(80));
	}
}

public class Scene3DTests
{
	static readonly ScreenRect Cell = new(0, 0, 600, 450);

	static Axes Space() => Axes.Default with { Kind = AxesKind.Space };

	static Surface Flat(int pen, double z) =>
		new(pen, "", 2, 2, [new(-10, -10, z), new(10, -10, z), new(-10, 10, z), new(10, 10, z)]);

	[Fact]
	public void FartherSurfacesAreDrawnFirst()
	{
		// From above (the default elevation is 30°), the surface at z = 5 is nearer.
		var axes = Space() with { Items = [Flat(2, 5), Flat(1, -5)] };
		var polygons = Scene3D.Build(axes, Cell).OfType<ScenePolygon>().ToList();
		Assert.Equal([1, 2], polygons.Select(p => p.Pen));

		var below = axes with { Camera = Camera.Default with { Elevation = -30 } };
		Assert.Equal([2, 1], Scene3D.Build(below, Cell).OfType<ScenePolygon>().Select(p => p.Pen));
	}

	[Fact]
	public void SurfacesAreCutToTheBoxAndHaveHolesWhereUndefined()
	{
		var axes = Space() with { ZMin = 0, ZMax = 1 };
		var steep = new Surface(1, "", 3, 2,
			[new(-10, -10, -1), new(0, -10, 0.5), new(10, -10, double.NaN), new(-10, 10, 2), new(0, 10, 0.5), new(10, 10, 0.5)]);
		var polygons = Scene3D.Build(axes with { Items = [steep] }, Cell).OfType<ScenePolygon>().ToList();
		// The second cell touches the NaN and is left out; the first is cut at z = 0 and z = 1.
		var polygon = Assert.Single(polygons);
		Assert.True(polygon.Points.Length > 4);
	}

	[Fact]
	public void CurvesAreCutWhereTheyLeaveTheBoxAndBreakAtNaN()
	{
		var axes = Space() with
		{
			Items = [new PointSeries3D(3, PlotStyle.Line, [new(0, 0, -20), new(0, 0, 20), new(5, 5, 5), new(double.NaN, 0, 0), new(1, 1, 1), new(2, 2, 2)])],
		};
		var curves = Scene3D.Build(axes, Cell).OfType<ScenePolyline>().Where(l => l.Stroke == SceneStroke.Pen).ToList();
		// −20..20 is cut to −10..10, which leaves the box: a new run starts where it comes back in.
		Assert.Equal(3, curves.Count);
		Assert.All(curves, c => Assert.Equal(3, c.Pen));
		Assert.Equal(2, curves[0].Points.Length);
	}

	[Fact]
	public void MarkersOutsideTheBoxAreLeftOut()
	{
		var axes = Space() with
		{
			Items = [new PointSeries3D(1, PlotStyle.Cross, [new(0, 0, 0), new(0, 0, 11), new(double.NaN, 0, 0)]), new DotCloud3D(2, [new(1, 2, 3)])],
		};
		var markers = Scene3D.Build(axes, Cell).OfType<SceneMarkers>().ToList();
		Assert.Equal(2, markers.Count);
		Assert.Single(markers[0].Points);
		Assert.Equal(PlotStyle.Dot, markers[1].Style);
	}

	[Fact]
	public void TheBoxHasNineEdgesAndTheAxesAreLabelled()
	{
		var scene = Scene3D.Build(Space() with { Title = "T", ZTitle = "height" }, Cell);
		Assert.Equal(9, scene.OfType<ScenePolyline>().Count(l => l.Stroke == SceneStroke.Frame));
		Assert.DoesNotContain(scene.OfType<ScenePolyline>(), l => l.Stroke == SceneStroke.Grid);
		var texts = scene.OfType<SceneText>().Select(t => t.Text).ToList();
		Assert.Contains("T", texts);
		Assert.Contains("x", texts);
		Assert.Contains("y", texts);
		Assert.Contains("height", texts);
		Assert.Contains("-10", texts);
		Assert.Contains("10", texts);
		Assert.True(scene[^1] is SceneText { Text: "T", Bold: true });
	}

	[Fact]
	public void TheGridIsDrawnOnTheBackSides()
	{
		var scene = Scene3D.Build(Space() with { ShowGrid = true }, Cell);
		Assert.Contains(scene.OfType<ScenePolyline>(), l => l.Stroke == SceneStroke.Grid);
	}

	[Fact]
	public void AnAxisSeenEndOnHasNoLabels()
	{
		// From straight above, the z axis points at the camera.
		var scene = Scene3D.Build(Space() with { Camera = Camera.Default with { Azimuth = 0, Elevation = 90 } }, Cell);
		var texts = scene.OfType<SceneText>().Select(t => t.Text).ToList();
		Assert.Contains("x", texts);
		Assert.Contains("y", texts);
		Assert.DoesNotContain("z", texts);
	}

	[Fact]
	public void EverythingIsInsideTheCell()
	{
		var cell = new ScreenRect(300, 200, 300, 200);
		var axes = Space() with { ShowGrid = true, Title = "T", Items = [Flat(1, 0)] };
		foreach (var primitive in Scene3D.Build(axes, cell))
		{
			IEnumerable<ScreenPoint> points = primitive switch
			{
				ScenePolyline l => l.Points,
				ScenePolygon p => p.Points,
				SceneText t => [t.At],
				_ => [],
			};
			foreach (var p in points)
			{
				Assert.InRange(p.X, cell.Left, cell.Right);
				Assert.InRange(p.Y, cell.Top, cell.Bottom);
			}
		}
	}

	[Fact]
	public void SvgDrawsA3DWindow()
	{
		var figure = new Figure();
		figure.SetTitle("Saddle");
		figure.AddSurface("x*y", (x, y) => x * y / 10, 5);
		figure.AddSeries3D([new(0, 0, 0), new(5, 5, 5)], PlotStyle.Star);
		string svg = SvgWriter.Render(figure.Snapshot());
		Assert.Contains(">Saddle</text>", svg);
		Assert.Contains(">z</text>", svg);
		// 4×4 cells of the surface, filled with the background.
		Assert.Equal(16, svg.Split("fill=\"white\" stroke=\"#1f77b4\"").Length - 1);
	}
}
