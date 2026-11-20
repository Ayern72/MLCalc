import { defaultAxes, type Axes, type AxesKind, type Camera, type PlotItem, type PlotPoint, type PlotStyle, type Point3 } from './plotting/model';
import type { CameraData, Coordinates, FigureMessage, PlotItemData, PlotWindow, PointsData, SeriesStyle, WindowAppend } from './protocol';

/**
 * The plot windows as the client has them, in the plot model, from the server's figure messages.
 * A window that did not change keeps the same Axes object, so its drawing can be kept.
 * (The Blazor client's FigureState.cs.)
 */
export class FigureState {
	#windows: Axes[] = [defaultAxes];
	rows = 1;
	columns = 1;
	currentRow = 0;
	currentColumn = 0;
	version = -1;

	window(row: number, column: number): Axes {
		return this.#windows[row * this.columns + column]!;
	}

	/** The user changed the camera of a 3D window here; false when there is no such window. */
	setCamera(row: number, column: number, camera: Camera): boolean {
		if (row < 0 || column < 0 || row >= this.rows || column >= this.columns || this.window(row, column).kind !== 'space')
			return false;
		this.#windows[row * this.columns + column] = { ...this.window(row, column), camera };
		return true;
	}

	apply(message: FigureMessage): void {
		if (message.rows !== this.rows || message.columns !== this.columns) {
			this.rows = message.rows;
			this.columns = message.columns;
			this.#windows = Array.from({ length: this.rows * this.columns }, () => defaultAxes);
		}
		this.currentRow = message.currentRow;
		this.currentColumn = message.currentColumn;
		this.version = message.version;
		for (const update of message.windows) {
			if (update.index < 0 || update.index >= this.#windows.length)
				continue;
			if (update.replace !== undefined)
				this.#windows[update.index] = toAxes(update.replace);
			else if (update.append !== undefined)
				this.#windows[update.index] = append(this.#windows[update.index]!, update.append);
		}
	}
}

function append(axes: Axes, added: WindowAppend): Axes {
	const items = [...axes.items];
	const last = items.at(-1);
	if (added.lastDots !== undefined && last !== undefined) {
		if (last.type === 'dots')
			items[items.length - 1] = { ...last, points: [...last.points, ...points2D(added.lastDots)] };
		else if (last.type === 'dots3d')
			items[items.length - 1] = { ...last, points: [...last.points, ...points3D(added.lastDots)] };
	}
	items.push(...added.items.map(toItem));
	return { ...axes, items, pen: added.pen };
}

// ───────────── Protocol → plot model ─────────────

export function toAxes(window: PlotWindow): Axes {
	return {
		kind: window.kind satisfies AxesKind,
		xMin: window.xMin, xMax: window.xMax, yMin: window.yMin, yMax: window.yMax, zMin: window.zMin, zMax: window.zMax,
		title: window.title, xTitle: window.xTitle, yTitle: window.yTitle, zTitle: window.zTitle,
		showGrid: window.grid, pen: window.pen,
		items: window.items.map(toItem),
		camera: toCamera(window.camera),
	};
}

export function toCamera(camera: CameraData): Camera {
	return {
		azimuth: camera.azimuth,
		elevation: camera.elevation,
		distance: camera.distance ?? null,
		position: toPoint(camera.position),
		target: toPoint(camera.target),
	};
}

/** A camera in the protocol's form. */
export function toCameraData(camera: Camera): CameraData {
	const result: CameraData = { azimuth: camera.azimuth, elevation: camera.elevation };
	if (camera.distance !== null)
		result.distance = camera.distance;
	if (camera.position !== null)
		result.position = [camera.position.x, camera.position.y, camera.position.z];
	if (camera.target !== null)
		result.target = [camera.target.x, camera.target.y, camera.target.z];
	return result;
}

function toPoint(p: readonly number[] | undefined): Point3 | null {
	return p !== undefined && p.length === 3 ? { x: p[0]!, y: p[1]!, z: p[2]! } : null;
}

export function toItem(item: PlotItemData): PlotItem {
	const points: PointsData = { x: item.x, y: item.y, ...(item.z !== undefined ? { z: item.z } : {}) };
	switch (item.type) {
		case 'series': return { type: 'series', pen: item.pen, style: toStyle(item.style), points: points2D(points) };
		case 'function': return { type: 'function', pen: item.pen, label: item.label ?? '', points: points2D(points) };
		case 'dots': return { type: 'dots', pen: item.pen, points: points2D(points) };
		case 'series3d': return { type: 'series3d', pen: item.pen, style: toStyle(item.style), points: points3D(points) };
		case 'dots3d': return { type: 'dots3d', pen: item.pen, points: points3D(points) };
		case 'surface': return { type: 'surface', pen: item.pen, label: item.label ?? '', xCount: item.xCount ?? 0, yCount: item.yCount ?? 0, points: points3D(points) };
	}
}

function toStyle(style: SeriesStyle | undefined): PlotStyle {
	return style ?? 'line';
}

/**
 * A coordinate: the protocol sends single-precision numbers, so this is the single-precision value
 * (as the .NET clients read it into a float), and null is a gap.
 */
function coordinate(values: Coordinates | undefined, index: number): number {
	const value = values?.[index];
	return value === null || value === undefined ? Number.NaN : Math.fround(value);
}

function points2D(points: PointsData): PlotPoint[] {
	return points.x.map((_, i) => ({ x: coordinate(points.x, i), y: coordinate(points.y, i) }));
}

function points3D(points: PointsData): Point3[] {
	return points.x.map((_, i) => ({ x: coordinate(points.x, i), y: coordinate(points.y, i), z: coordinate(points.z, i) }));
}
