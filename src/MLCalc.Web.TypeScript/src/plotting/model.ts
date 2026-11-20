// The plot model, as src/MLCalc.Plotting has it (Figure.cs, PlotItems.cs): what the windows hold,
// in data coordinates. Objects are never changed once made; a change makes new ones, so a window
// that did not change keeps the same object and its drawing can be kept.

export type AxesKind = 'cartesian' | 'complex' | 'space';

/** How a series of points is drawn: connected by lines, or as a marker at each point. */
export type PlotStyle = 'line' | 'cross' | 'star' | 'dot';

/** A point in data coordinates; NaN where the protocol had null (a gap). */
export interface PlotPoint {
	readonly x: number;
	readonly y: number;
}

export interface Point3 {
	readonly x: number;
	readonly y: number;
	readonly z: number;
}

export function isFinite3(p: Point3): boolean {
	return Number.isFinite(p.x) && Number.isFinite(p.y) && Number.isFinite(p.z);
}

/** plot, cplot: connected (line) or marked points. */
export interface PointSeries {
	readonly type: 'series';
	readonly pen: number;
	readonly style: PlotStyle;
	readonly points: readonly PlotPoint[];
}

/** fplot, polyplot: a function sampled over the x range; the line breaks at poles. */
export interface FunctionSeries {
	readonly type: 'function';
	readonly pen: number;
	readonly label: string;
	readonly points: readonly PlotPoint[];
}

/** dot(x, y): single dots, collected per pen. */
export interface DotCloud {
	readonly type: 'dots';
	readonly pen: number;
	readonly points: readonly PlotPoint[];
}

export interface PointSeries3D {
	readonly type: 'series3d';
	readonly pen: number;
	readonly style: PlotStyle;
	readonly points: readonly Point3[];
}

export interface DotCloud3D {
	readonly type: 'dots3d';
	readonly pen: number;
	readonly points: readonly Point3[];
}

/** fplot3d: z = f(x, y) on a grid; points[j * xCount + i] is the i-th x and the j-th y. */
export interface Surface {
	readonly type: 'surface';
	readonly pen: number;
	readonly label: string;
	readonly xCount: number;
	readonly yCount: number;
	readonly points: readonly Point3[];
}

export type PlotItem = PointSeries | FunctionSeries | DotCloud | PointSeries3D | DotCloud3D | Surface;

/**
 * The camera of a 3D window. Without a position it is placed relative to the box from azimuth and
 * elevation (degrees), at distance (half-boxes; null: just far enough to see the whole box); with a
 * position (camposition) it stays there. target null means the middle of the box.
 */
export interface Camera {
	readonly azimuth: number;
	readonly elevation: number;
	readonly distance: number | null;
	readonly position: Point3 | null;
	readonly target: Point3 | null;
}

export const defaultCamera: Camera = { azimuth: -37.5, elevation: 30, distance: null, position: null, target: null };

/** One plot window. */
export interface Axes {
	readonly kind: AxesKind;
	readonly xMin: number;
	readonly xMax: number;
	readonly yMin: number;
	readonly yMax: number;
	readonly zMin: number;
	readonly zMax: number;
	readonly title: string;
	readonly xTitle: string;
	readonly yTitle: string;
	readonly zTitle: string;
	readonly showGrid: boolean;
	readonly pen: number;
	readonly items: readonly PlotItem[];
	readonly camera: Camera;
}

export const defaultAxes: Axes = {
	kind: 'cartesian', xMin: -10, xMax: 10, yMin: -10, yMax: 10, zMin: -10, zMax: 10,
	title: '', xTitle: '', yTitle: '', zTitle: '', showGrid: false, pen: 1, items: [], camera: defaultCamera,
};

/** The pen colours (pens 1–15; numbers wrap around), as in the desktop app. */
export const penColors: readonly number[] = [
	0x1F77B4, 0xD62728, 0x2CA02C, 0xFF7F0E, 0x9467BD, 0x8C564B, 0xE377C2, 0x17BECF,
	0xBCBD22, 0x7F7F7F, 0x393B79, 0xAD494A, 0x637939, 0x8C6D31, 0x7B4173,
];

export function normalizePen(pen: number): number {
	const count = penColors.length;
	return ((pen - 1) % count + count) % count + 1;
}

/** A pen's colour as #rrggbb. */
export function penColor(pen: number): string {
	return '#' + penColors[normalizePen(pen) - 1]!.toString(16).padStart(6, '0');
}
