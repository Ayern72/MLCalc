// A 3D window as 2D primitives (Scene3D.cs): the three sides of the box facing away from the camera
// (with grid lines when the grid is on), surfaces from back to front so nearer parts hide farther
// ones, then curves and points, then ticks and labels.
import { formatTick, axisTicks, type ScreenRect } from './layout';
import { isFinite3, type Axes, type PlotItem, type PlotStyle, type Point3 } from './model';
import { Projection3D, Vec3, type ScreenPoint } from './projection3d';

export type SceneStroke = 'frame' | 'grid' | 'tick' | 'pen';
export type TextAlign = 'start' | 'middle' | 'end';

export type ScenePrimitive =
	| { readonly kind: 'polyline'; readonly stroke: SceneStroke; readonly pen: number; readonly points: readonly ScreenPoint[] }
	/** A piece of a surface: filled with the background and outlined in the pen's colour. */
	| { readonly kind: 'polygon'; readonly pen: number; readonly points: readonly ScreenPoint[] }
	| { readonly kind: 'markers'; readonly pen: number; readonly style: PlotStyle; readonly points: readonly ScreenPoint[] }
	/** A label, vertically centred on at. */
	| { readonly kind: 'text'; readonly text: string; readonly at: ScreenPoint; readonly align: TextAlign; readonly size: number; readonly bold: boolean };

/** Room around the box for tick labels and axis titles. */
const margin = 50, titleRoom = 30;

/** An edge shorter than this on the screen, or seen at a small angle, gets no tick labels. */
const minLabelledEdge = 40, endOnCosine = 0.9;

/** In a preview, surfaces have at most this many grid lines each way, curves and clouds this many points. */
export const previewGridLines = 40, previewPoints = 20000;

/** The rectangle the box is fitted into, inside a grid cell. */
export function viewArea(cell: ScreenRect): ScreenRect {
	return {
		left: cell.left + margin,
		top: cell.top + titleRoom + margin / 2,
		width: Math.max(1, cell.width - 2 * margin),
		height: Math.max(1, cell.height - titleRoom - 1.5 * margin),
	};
}

/** The primitives for a 3D window in the rectangle cell; a preview draws large items thinned out. */
export function buildScene(axes: Axes, cell: ScreenRect, preview = false): ScenePrimitive[] {
	if (preview)
		axes = previewOf(axes);
	const scene: ScenePrimitive[] = [];
	const area = viewArea(cell);
	if (area.width >= 20 && area.height >= 20)
		addBox(scene, axes, area);
	// Last, so that a camera close to a surface does not hide the title.
	if (axes.title.length > 0)
		scene.push({ kind: 'text', text: axes.title, at: { x: cell.left + cell.width / 2, y: cell.top + 16 }, align: 'middle', size: 13, bold: true });
	return scene;
}

type Edge = readonly [Vec3, Vec3];

function addBox(scene: ScenePrimitive[], axes: Axes, area: ScreenRect): void {
	const view = new Projection3D(axes, area);
	const eye = view.eye;
	// The sides of the box facing away from the camera, one per axis: −1 or +1.
	const backX = eye.x >= 0 ? -1 : 1, backY = eye.y >= 0 ? -1 : 1, backZ = eye.z >= 0 ? -1 : 1;

	// The edges the ticks go on: for x and y the bottom edges nearest the camera (the top ones when
	// looking from below), for z the leftmost vertical edge.
	const xEdge: Edge = [new Vec3(-1, -backY, backZ), new Vec3(1, -backY, backZ)];
	const yEdge: Edge = [new Vec3(-backX, -1, backZ), new Vec3(-backX, 1, backZ)];
	const [zx, zy] = zTickEdge(view);
	const zEdge: Edge = [new Vec3(zx, zy, -1), new Vec3(zx, zy, 1)];
	const xTicks = ticks(axes.xMin, axes.xMax, view, xEdge);
	const yTicks = ticks(axes.yMin, axes.yMax, view, yEdge);
	const zTicks = ticks(axes.zMin, axes.zMax, view, zEdge);
	const cx = (x: number) => 2 * (x - axes.xMin) / (axes.xMax - axes.xMin) - 1;
	const cy = (y: number) => 2 * (y - axes.yMin) / (axes.yMax - axes.yMin) - 1;
	const cz = (z: number) => 2 * (z - axes.zMin) / (axes.zMax - axes.zMin) - 1;

	if (axes.showGrid) {
		for (const y of yTicks)
			line(scene, view, 'grid', new Vec3(backX, cy(y), -1), new Vec3(backX, cy(y), 1));
		for (const z of zTicks) {
			line(scene, view, 'grid', new Vec3(backX, -1, cz(z)), new Vec3(backX, 1, cz(z)));
			line(scene, view, 'grid', new Vec3(-1, backY, cz(z)), new Vec3(1, backY, cz(z)));
		}
		for (const x of xTicks) {
			line(scene, view, 'grid', new Vec3(cx(x), backY, -1), new Vec3(cx(x), backY, 1));
			line(scene, view, 'grid', new Vec3(cx(x), -1, backZ), new Vec3(cx(x), 1, backZ));
		}
		for (const y of yTicks)
			line(scene, view, 'grid', new Vec3(-1, cy(y), backZ), new Vec3(1, cy(y), backZ));
	}

	// The edges of the three back sides (nine edges: each pair of sides shares one).
	const edges = new Set<string>();
	for (const [axis, side] of [[0, backX], [1, backY], [2, backZ]] as const)
		for (let k = 0; k < 4; k++) {
			const corners = sideCorners(axis, side);
			const a = corners[k]!;
			const b = corners[(k + 1) % 4]!;
			const forwardKey = `${a.key}|${b.key}`, backwardKey = `${b.key}|${a.key}`;
			// As C#'s edges.Add((a, b)) && edges.Add((b, a)): the second is tried only when the first was new.
			if (!edges.has(forwardKey)) {
				edges.add(forwardKey);
				if (!edges.has(backwardKey)) {
					edges.add(backwardKey);
					line(scene, view, 'frame', a, b);
				}
			}
		}

	addSurfaces(scene, view, axes);
	for (const item of axes.items) {
		if (item.type === 'series3d' && item.style === 'line')
			addCurve(scene, view, item.pen, item.points);
		else if (item.type === 'series3d')
			addMarkers(scene, view, item.pen, item.style, item.points);
		else if (item.type === 'dots3d')
			addMarkers(scene, view, item.pen, 'dot', item.points);
	}

	// An axis seen (nearly) end-on, such as z from straight above, would have its labels on top of each other.
	if (!isEndOn(view, xEdge))
		addAxisLabels(scene, view, xTicks, x => new Vec3(cx(x), -backY, backZ), new Vec3(0, -backY, backZ), title(axes.xTitle, 'x'));
	if (!isEndOn(view, yEdge))
		addAxisLabels(scene, view, yTicks, y => new Vec3(-backX, cy(y), backZ), new Vec3(-backX, 0, backZ), title(axes.yTitle, 'y'));
	if (!isEndOn(view, zEdge))
		addAxisLabels(scene, view, zTicks, z => new Vec3(zx, zy, cz(z)), new Vec3(zx, zy, 0), title(axes.zTitle, 'z'));
}

function title(text: string, fallback: string): string {
	return text.length > 0 ? text : fallback;
}

/** Tick values for an axis, about one per 50 pixels of the edge they are drawn on. */
function ticks(min: number, max: number, view: Projection3D, edge: Edge): number[] {
	return axisTicks(min, max, Math.min(Math.max(Math.trunc(edgeLength(view, edge) / 50), 3), 8));
}

function isEndOn(view: Projection3D, edge: Edge): boolean {
	if (edgeLength(view, edge) < minLabelledEdge)
		return true;
	const sight = edge[0].plus(edge[1]).times(0.5).minus(view.eye).normalized;
	const direction = edge[1].minus(edge[0]).normalized;
	return Math.abs(sight.dot(direction)) > endOnCosine;
}

/** The length of an edge of the box on the screen, or 0 when it is (partly) behind the camera. */
function edgeLength(view: Projection3D, edge: Edge): number {
	if (view.depth(edge[0]) <= 0 || view.depth(edge[1]) <= 0)
		return 0;
	const a = view.toScreen(edge[0]);
	const b = view.toScreen(edge[1]);
	return Math.sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
}

/** The vertical edge of the box that the z ticks go on: the leftmost on the screen. */
function zTickEdge(view: Projection3D): [number, number] {
	let best: [number, number] = [-1, -1];
	let left = Number.POSITIVE_INFINITY;
	for (const [x, y] of [[-1, -1], [1, -1], [1, 1], [-1, 1]] as const) {
		const middle = new Vec3(x, y, 0);
		if (view.depth(middle) <= 0)
			continue;
		const screenX = view.toScreen(middle).x;
		if (screenX < left) {
			left = screenX;
			best = [x, y];
		}
	}
	return best;
}

/** The corners, in order around the side, of the side of the box at side (−1 or +1) on axis. */
function sideCorners(axis: 0 | 1 | 2, side: number): Vec3[] {
	return ([[-1, -1], [1, -1], [1, 1], [-1, 1]] as const).map(([s, t]) =>
		axis === 0 ? new Vec3(side, s, t) : axis === 1 ? new Vec3(s, side, t) : new Vec3(s, t, side));
}

function line(scene: ScenePrimitive[], view: Projection3D, stroke: SceneStroke, a: Vec3, b: Vec3, pen = 0): void {
	const clipped = view.clipSegment(a, b);
	if (clipped !== null)
		scene.push({ kind: 'polyline', stroke, pen, points: [view.toScreen(clipped.a), view.toScreen(clipped.b)] });
}

/**
 * The window with large items thinned out for a quick preview: a surface keeps at most
 * previewGridLines of its grid lines each way (always the first and the last), a curve or cloud at
 * most about previewPoints of its points. A curve keeps its non-finite points, so that its gaps stay gaps.
 * Other limits (gridLines at least 2) thin out further, for a front-end where each preview costs more
 * to show, such as the app.
 */
export function previewOf(axes: Axes, gridLines = previewGridLines, maxPoints = previewPoints): Axes {
	if (gridLines < 2 || maxPoints < 1)
		throw new RangeError('A preview needs at least 2 grid lines and 1 point.');
	return { ...axes, items: axes.items.map(item => previewItem(item, gridLines, maxPoints)) };
}

function previewItem(item: PlotItem, gridLines: number, maxPoints: number): PlotItem {
	if (item.type === 'surface' && (item.xCount > gridLines || item.yCount > gridLines)) {
		const xs = lines(item.xCount, gridLines);
		const ys = lines(item.yCount, gridLines);
		const points: Point3[] = [];
		for (const j of ys)
			for (const i of xs)
				points.push(item.points[j * item.xCount + i]!);
		return { ...item, xCount: xs.length, yCount: ys.length, points };
	}
	if (item.type === 'series3d' && item.points.length > maxPoints) {
		const step = previewStep(item.points.length, maxPoints);
		const points = item.points;
		return { ...item, points: points.filter((p, k) => k % step === 0 || k === points.length - 1 || !isFinite3(p)) };
	}
	if (item.type === 'dots3d' && item.points.length > maxPoints) {
		const step = previewStep(item.points.length, maxPoints);
		return { ...item, points: item.points.filter((_, k) => k % step === 0) };
	}
	return item;
}

function previewStep(count: number, maxPoints: number): number {
	return Math.trunc((count + maxPoints - 1) / maxPoints);
}

/** At most max evenly spread indices from 0 to count − 1, both included. */
function lines(count: number, max: number): number[] {
	if (count <= max)
		return Array.from({ length: count }, (_, k) => k);
	const step = Math.trunc((count - 1 + max - 2) / (max - 1));
	const result: number[] = [];
	for (let k = 0; k < count - 1; k += step)
		result.push(k);
	result.push(count - 1);
	return result;
}

/** Every cell of every surface as a polygon, sorted so that farther ones are drawn first. */
function addSurfaces(scene: ScenePrimitive[], view: Projection3D, axes: Axes): void {
	const pieces: { depth: number; order: number; polygon: ScenePrimitive }[] = [];
	for (const surface of axes.items) {
		if (surface.type !== 'surface')
			continue;
		// Each grid point is shared by up to four cells: convert it once.
		const n = surface.points.length;
		const cube: Vec3[] = new Array<Vec3>(n);
		const depth = new Float64Array(n);
		const visible: boolean[] = new Array<boolean>(n).fill(false);
		const screen: ScreenPoint[] = new Array<ScreenPoint>(n);
		for (let k = 0; k < n; k++) {
			const p = surface.points[k]!;
			if (!isFinite3(p))
				continue;
			cube[k] = view.cube(p);
			visible[k] = view.isVisible(cube[k]!);
			if (visible[k]) {
				depth[k] = view.depth(cube[k]!);
				screen[k] = view.toScreen(cube[k]!);
			}
		}
		for (let j = 0; j + 1 < surface.yCount; j++)
			for (let i = 0; i + 1 < surface.xCount; i++) {
				const c0 = j * surface.xCount + i;
				const corners = [c0, c0 + 1, c0 + 1 + surface.xCount, c0 + surface.xCount];
				let finite = true, inside = true;
				for (const k of corners) {
					finite &&= isFinite3(surface.points[k]!);
					inside &&= visible[k]!;
				}
				if (!finite)
					continue;
				if (inside) {
					// The usual case: nothing to cut.
					const average = (depth[corners[0]!]! + depth[corners[1]!]! + depth[corners[2]!]! + depth[corners[3]!]!) / 4;
					pieces.push({ depth: average, order: pieces.length, polygon: { kind: 'polygon', pen: surface.pen, points: corners.map(k => screen[k]!) } });
					continue;
				}
				const polygon = view.clipPolygon(corners.map(k => cube[k]!));
				if (polygon.length < 3)
					continue;
				let sum = 0;
				for (const c of polygon)
					sum += view.depth(c);
				pieces.push({ depth: sum / polygon.length, order: pieces.length, polygon: { kind: 'polygon', pen: surface.pen, points: polygon.map(c => view.toScreen(c)) } });
			}
	}
	// Farthest first; pieces at the same depth keep their order.
	pieces.sort((a, b) => a.depth !== b.depth ? b.depth - a.depth : a.order - b.order);
	for (const piece of pieces)
		scene.push(piece.polygon);
}

/** A curve, broken at non-finite points and cut where it leaves the box. */
function addCurve(scene: ScenePrimitive[], view: Projection3D, pen: number, points: readonly Point3[]): void {
	let run: ScreenPoint[] = [];
	const flush = () => {
		if (run.length > 1)
			scene.push({ kind: 'polyline', stroke: 'pen', pen, points: run });
		run = [];
	};
	for (let k = 0; k + 1 < points.length; k++) {
		const a = view.cube(points[k]!);
		const b = view.cube(points[k + 1]!);
		const clipped = a.isFinite && b.isFinite ? view.clipSegment(a, b) : null;
		if (clipped === null) {
			flush();
			continue;
		}
		if (clipped.clippedStart)
			flush();
		if (run.length === 0)
			run.push(view.toScreen(clipped.a));
		run.push(view.toScreen(clipped.b));
		if (clipped.clippedEnd)
			flush();
	}
	flush();
}

function addMarkers(scene: ScenePrimitive[], view: Projection3D, pen: number, style: PlotStyle, points: readonly Point3[]): void {
	const visible = points.map(p => view.cube(p)).filter(c => view.isVisible(c)).map(c => view.toScreen(c));
	if (visible.length > 0)
		scene.push({ kind: 'markers', pen, style, points: visible });
}

/**
 * Ticks and their labels along an edge, and the axis title beside its middle. Labels are pushed away
 * from the box, in the direction of outward (from the middle of the box to the edge).
 */
function addAxisLabels(scene: ScenePrimitive[], view: Projection3D, ticks: readonly number[], position: (value: number) => Vec3, outward: Vec3, title: string): void {
	for (const tick of ticks) {
		const at = position(tick);
		const direction = view.isVisible(at) ? directionAt(view, at, outward) : null;
		if (direction === null)
			continue;
		const p = view.toScreen(at);
		scene.push({ kind: 'polyline', stroke: 'tick', pen: 0, points: [p, offset(p, direction, 5)] });
		// Text is centred on its anchor, so it needs more room below or above the tick than beside it.
		scene.push({ kind: 'text', text: formatTick(tick), at: offset(p, direction, 9 + 5 * Math.abs(direction.y)), align: align(direction), size: 11, bold: false });
	}
	const middle = outward;
	const away = view.isVisible(middle) ? directionAt(view, middle, outward) : null;
	if (away !== null)
		scene.push({ kind: 'text', text: title, at: offset(view.toScreen(middle), away, 34), align: 'middle', size: 12, bold: false });
}

/** The screen direction in which outward points at at, as a unit vector. */
function directionAt(view: Projection3D, at: Vec3, outward: Vec3): ScreenPoint | null {
	const beyond = at.plus(outward.times(0.1));
	if (view.depth(beyond) <= 0)
		return null;
	const a = view.toScreen(at);
	const b = view.toScreen(beyond);
	const dx = b.x - a.x, dy = b.y - a.y, length = Math.sqrt(dx * dx + dy * dy);
	// Seen end-on: put the label below.
	return length < 1e-6 ? { x: 0, y: 1 } : { x: dx / length, y: dy / length };
}

function offset(p: ScreenPoint, direction: ScreenPoint, distance: number): ScreenPoint {
	return { x: p.x + direction.x * distance, y: p.y + direction.y * distance };
}

function align(direction: ScreenPoint): TextAlign {
	return direction.x < -0.35 ? 'end' : direction.x > 0.35 ? 'start' : 'middle';
}
