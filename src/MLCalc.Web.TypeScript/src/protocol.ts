// The messages between the MLCalc server and its clients, as described in docs/web-protocol.md.
// Written by hand from that document, which is the contract: the server's C# types
// (src/MLCalc.Web.Protocol) are tested to produce exactly the JSON it shows.

/** The path of the SignalR hub. */
export const hubPath = '/hub';

/** The query parameter that names the session to continue: /hub?session=… */
export const sessionParameter = 'session';

/** The hub methods a client calls. Each returns at once; the results come as messages. */
export const serverMethods = {
	run: 'Run',
	cancel: 'Cancel',
	setCamera: 'SetCamera',
} as const;

/** The messages the server sends, by method name (matched without regard to case). */
export const clientMethods = {
	welcome: 'Welcome',
	output: 'Output',
	figureChanged: 'FigureChanged',
	runFinished: 'RunFinished',
	download: 'Download',
} as const;

// ───────────── Client → server ─────────────

/** Runs console input or the text of an opened script. */
export interface RunRequest {
	id: string;
	source: string;
}

/** The camera of a 3D window after the user turned or zoomed it. */
export interface CameraUpdate {
	row: number;
	column: number;
	camera: CameraData;
}

// ───────────── Server → client ─────────────

export interface WelcomeMessage {
	sessionId: string;
	newSession: boolean;
	running: boolean;
	/** The server's build: version number, "+" and the commit when the build knew it. */
	version: string;
	builtinNames: string[];
	limits: Limits;
	figure: FigureMessage;
}

export interface Limits {
	runSeconds: number;
	maxMatrixElements: number;
	maxPointsPerWindow: number;
	maxOutputEntries: number;
	maxSourceLength: number;
}

export interface OutputMessage {
	entries: OutputEntry[];
}

export type OutputKind = 'value' | 'text' | 'clear';

export interface OutputEntry {
	kind: OutputKind;
	text: string;
}

export type RunStatus = 'ok' | 'error' | 'cancelled' | 'timeLimit' | 'incomplete' | 'busy';

export interface RunFinishedMessage {
	id: string;
	status: RunStatus;
	errors?: RunError[];
	outputTruncated: boolean;
}

export interface RunError {
	message: string;
	/** 1-based; 0 when the error has no place in the source. */
	line: number;
	column: number;
	length: number;
}

export interface DownloadMessage {
	fileName: string;
	mediaType: string;
	content: string;
}

// ───────────── Figures ─────────────

/** The plot windows: whole in Welcome, only the changed ones in FigureChanged. */
export interface FigureMessage {
	version: number;
	rows: number;
	columns: number;
	currentRow: number;
	currentColumn: number;
	windows: WindowUpdate[];
}

/** One window, row by row from 0: either whole (replace) or with items added (append). */
export interface WindowUpdate {
	index: number;
	replace?: PlotWindow;
	append?: WindowAppend;
}

export interface WindowAppend {
	pen: number;
	items: PlotItemData[];
	/** More points for the window's last dot cloud. */
	lastDots?: PointsData;
}

/** Coordinates; null is a number that is not finite: a gap in a series, a hole in a surface. */
export type Coordinates = (number | null)[];

export interface PointsData {
	x: Coordinates;
	y: Coordinates;
	z?: Coordinates;
}

export type WindowKind = 'cartesian' | 'complex' | 'space';

export interface PlotWindow {
	kind: WindowKind;
	xMin: number;
	xMax: number;
	yMin: number;
	yMax: number;
	zMin: number;
	zMax: number;
	title: string;
	xTitle: string;
	yTitle: string;
	zTitle: string;
	grid: boolean;
	pen: number;
	camera: CameraData;
	items: PlotItemData[];
}

export interface CameraData {
	azimuth: number;
	elevation: number;
	distance?: number;
	position?: [number, number, number];
	target?: [number, number, number];
}

export type ItemType = 'series' | 'function' | 'dots' | 'series3d' | 'dots3d' | 'surface';

export type SeriesStyle = 'line' | 'cross' | 'star' | 'dot';

export interface PlotItemData {
	type: ItemType;
	pen: number;
	x: Coordinates;
	y: Coordinates;
	z?: Coordinates;
	style?: SeriesStyle;
	label?: string;
	xCount?: number;
	yCount?: number;
}
