import { describe as describeCamera, drag, turn, wheelFactor, zoom } from '../plotting/camera';
import { formatG5 } from '../plotting/format';
import { bottom, plotArea, PlotTransform, right, type ScreenRect } from '../plotting/layout';
import { defaultCamera, type Axes, type Camera, type Point3 } from '../plotting/model';
import { Projection3D } from '../plotting/projection3d';
import { renderWindow } from '../plotting/svg-writer';
import { element } from './dom';

/** What a window needs from its pane. */
export interface PlotWindowHost {
	/** Text for the status bar: what is under the mouse, or empty. */
	status(text: string): void;
	/** The user turned, zoomed or reset the 3D window at (row, column). */
	camera(row: number, column: number, camera: Camera): void;
}

/** How long after the last wheel turn the full view is drawn and sent. */
const wheelSettle = 250;

/**
 * One plot window, drawn as SVG with the same code as the C# SVG export (ported). A transparent
 * layer on top takes the mouse, so that positions are measured from the window's corner. In a 3D
 * window, dragging turns the camera and the wheel moves it closer or further away, as in the
 * desktop app; on a touch screen, one finger turns and two fingers pinch. While it moves, large
 * items are drawn thinned out. (The Blazor client's PlotWindowView.razor.)
 */
export class PlotWindowView {
	readonly element: HTMLDivElement;
	readonly #drawing: HTMLDivElement;
	readonly #mouse: HTMLDivElement;
	readonly #host: PlotWindowHost;

	#axes: Axes | null = null;
	#width = 0;
	#height = 0;
	#isCurrent = false;
	#id = 'w';
	#row = 0;
	#column = 0;
	#gridLabel = '';

	/** The window with the camera the user is turning; null when the window is as given. */
	#turned: Axes | null = null;
	/** A drag in progress: the angles and pointer position where it began. */
	#drag: { start: Axes; azimuth: number; elevation: number; x: number; y: number; pointer: number } | null = null;
	/** The fingers on a 3D window, by pointer id, where they are now. */
	readonly #touches = new Map<number, { x: number; y: number }>();
	/** A pinch in progress: the window and the distance between the two fingers when it began. */
	#pinch: { start: Axes; distance: number } | null = null;
	#wheelTimer: ReturnType<typeof setTimeout> | null = null;

	constructor(host: PlotWindowHost) {
		this.#host = host;
		this.#drawing = element('div');
		this.#mouse = element('div', { class: 'plot-mouse' });
		this.element = element('div', { class: 'plot-window' }, this.#drawing, this.#mouse);
		this.#mouse.addEventListener('pointerdown', event => this.#onPointerDown(event));
		this.#mouse.addEventListener('pointermove', event => this.#onPointerMove(event));
		this.#mouse.addEventListener('pointerup', event => this.#onPointerUp(event));
		this.#mouse.addEventListener('pointercancel', event => this.#onPointerUp(event));
		this.#mouse.addEventListener('pointerleave', () => {
			if (this.#drag === null)
				this.#host.status('');
		});
		// Not passive: in a 3D window the wheel zooms instead of scrolling the page.
		this.#mouse.addEventListener('wheel', event => this.#onWheel(event), { passive: false });
		this.#mouse.addEventListener('dblclick', () => this.#onDoubleClick());
	}

	get #shown(): Axes | null {
		return this.#turned ?? this.#axes;
	}

	get #is3D(): boolean {
		return this.#axes?.kind === 'space';
	}

	get #cell(): ScreenRect {
		return { left: 0, top: 0, width: this.#width, height: this.#height };
	}

	/** Draws again only when the window, its size or its frame changed. */
	update(axes: Axes, row: number, column: number, width: number, height: number, isCurrent: boolean, gridLabel: string): void {
		this.#row = row;
		this.#column = column;
		this.#gridLabel = gridLabel;
		const id = `w${row}${column}`;
		if (axes === this.#axes && width === this.#width && height === this.#height && isCurrent === this.#isCurrent && id === this.#id)
			return;
		// The window as the user left it, now confirmed by the page: already drawn.
		const turned = this.#turned;
		const confirmed = turned !== null && this.#drag === null && this.#pinch === null && this.#wheelTimer === null
			&& sameCamera(axes.camera, turned.camera) && axes.items === turned.items
			&& width === this.#width && height === this.#height && isCurrent === this.#isCurrent && id === this.#id;
		this.#turned = null;
		this.#drag = null;
		this.#pinch = null;
		this.#axes = axes;
		this.#width = width;
		this.#height = height;
		this.#isCurrent = isCurrent;
		this.#id = id;
		this.#mouse.classList.toggle('turnable', axes.kind === 'space');
		if (!confirmed)
			this.#draw(axes, false);
	}

	#draw(axes: Axes, preview: boolean): void {
		// The text comes from our own writer, which escapes all text it puts in.
		this.#drawing.innerHTML = this.#width >= 1 && this.#height >= 1
			? renderWindow(axes, this.#width, this.#height, this.#id, this.#isCurrent, preview)
			: '';
	}

	#onPointerDown(event: PointerEvent): void {
		const shown = this.#shown;
		if (!this.#is3D || shown === null || event.button !== 0)
			return;
		// Keep getting the pointer's moves when it leaves the window during the drag or pinch.
		capture(this.#mouse, event.pointerId);
		if (event.pointerType === 'touch') {
			this.#touches.set(event.pointerId, { x: event.clientX, y: event.clientY });
			if (this.#touches.size === 2) {
				// A second finger: the turn becomes a pinch, from the view as it is now.
				this.#drag = null;
				this.#pinch = { start: shown, distance: this.#fingerDistance() };
				return;
			}
			if (this.#touches.size > 2 || this.#pinch !== null)
				return;
		}
		const [azimuth, elevation] = Projection3D.viewAngles(shown);
		this.#drag = { start: shown, azimuth, elevation, x: event.clientX, y: event.clientY, pointer: event.pointerId };
	}

	/** The distance between the first two fingers on the window, in pixels. */
	#fingerDistance(): number {
		const [a, b] = [...this.#touches.values()];
		return Math.max(1, Math.hypot(a!.x - b!.x, a!.y - b!.y));
	}

	#onPointerMove(event: PointerEvent): void {
		if (this.#touches.has(event.pointerId))
			this.#touches.set(event.pointerId, { x: event.clientX, y: event.clientY });
		if (this.#pinch !== null) {
			if (this.#touches.size < 2)
				return;
			// Fingers apart: closer. Measured from the start, so that the steps do not add up errors.
			this.#show(zoom(this.#pinch.start, this.#pinch.distance / this.#fingerDistance(), this.#cell));
			return;
		}
		const d = this.#drag;
		if (d === null) {
			this.#host.status(this.#describe(event.offsetX, event.offsetY));
			return;
		}
		if (event.pointerId !== d.pointer)
			return;
		const [azimuth, elevation] = drag(d.azimuth, d.elevation, event.clientX - d.x, event.clientY - d.y);
		if (this.#turned !== null && this.#turned.camera.azimuth === azimuth && this.#turned.camera.elevation === elevation)
			return;
		this.#show(turn(d.start, azimuth, elevation));
	}

	#onPointerUp(event: PointerEvent): void {
		const wasTouch = this.#touches.delete(event.pointerId);
		if (this.#pinch !== null) {
			release(this.#mouse, event.pointerId);
			// The pinch ends when a finger is lifted; the finger left on the window does not turn the
			// view, which would jump from where the pinch began.
			this.#pinch = null;
			if (this.#turned !== null)
				this.#commit();
			return;
		}
		const d = this.#drag;
		if (d === null || d.pointer !== event.pointerId) {
			if (wasTouch)
				release(this.#mouse, event.pointerId);
			return;
		}
		this.#drag = null;
		release(this.#mouse, d.pointer);
		if (this.#turned !== null)
			this.#commit();
	}

	#onWheel(event: WheelEvent): void {
		const shown = this.#shown;
		if (!this.#is3D || shown === null)
			return;
		event.preventDefault();
		if (this.#drag !== null)
			return;
		// Browsers give pixels (about 100 per notch) or, with deltaMode 1, lines (3 per notch); the
		// desktop's wheel gives 120 per notch, positive away from the user.
		const notches = event.deltaMode === 1 ? event.deltaY / 3 : event.deltaY / 100;
		if (notches === 0)
			return;
		this.#show(zoom(shown, wheelFactor(Math.round(-notches * 120)), this.#cell));
		// Draw the full view and send the camera once the wheel has been still for a moment.
		if (this.#wheelTimer !== null)
			clearTimeout(this.#wheelTimer);
		this.#wheelTimer = setTimeout(() => {
			this.#wheelTimer = null;
			if (this.#turned !== null)
				this.#commit();
		}, wheelSettle);
	}

	#onDoubleClick(): void {
		const shown = this.#shown;
		if (!this.#is3D || shown === null)
			return;
		// Kept here: committing hands the camera to the page, which updates this window at once.
		const reset = { ...shown, camera: defaultCamera };
		this.#turned = reset;
		this.#commit();
		this.#host.status(this.#gridLabel + describeCamera(reset, this.#cell));
	}

	/** Shows a turned or zoomed view as a quick preview while it moves. */
	#show(axes: Axes): void {
		this.#turned = axes;
		this.#draw(axes, true);
		this.#host.status(this.#gridLabel + describeCamera(axes, this.#cell));
	}

	/** Draws the window in full with the new camera and hands the camera to the page. */
	#commit(): void {
		const turned = this.#turned!;
		this.#draw(turned, false);
		this.#host.camera(this.#row, this.#column, turned.camera);
	}

	/** The status bar text for the mouse at (x, y) in the window, as in the desktop app. */
	#describe(x: number, y: number): string {
		const axes = this.#axes;
		if (axes === null)
			return '';
		if (axes.kind === 'space')
			// A point on the screen is a whole line of points in 3D; show the camera instead.
			return this.#gridLabel + describeCamera(this.#shown!, this.#cell);
		const plot = plotArea(this.#cell);
		if (x < plot.left || x > right(plot) || y < plot.top || y > bottom(plot))
			return '';
		const t = new PlotTransform(axes, plot);
		const worldX = formatG5(t.toWorldX(x));
		const worldY = formatG5(t.toWorldY(y));
		return axes.kind === 'complex'
			? `${this.#gridLabel}Re = ${worldX}   Im = ${worldY}`
			: `${this.#gridLabel}x = ${worldX}   y = ${worldY}`;
	}
}

function sameCamera(a: Camera, b: Camera): boolean {
	return a.azimuth === b.azimuth && a.elevation === b.elevation && a.distance === b.distance
		&& samePoint(a.position, b.position) && samePoint(a.target, b.target);
}

function samePoint(a: Point3 | null, b: Point3 | null): boolean {
	return a === b || (a !== null && b !== null && a.x === b.x && a.y === b.y && a.z === b.z);
}

/** A pointer the browser does not know (e.g. one already lifted, or a synthetic one) is ignored. */
function capture(target: Element, pointerId: number): void {
	try {
		target.setPointerCapture(pointerId);
	} catch {
		// See above.
	}
}

function release(target: Element, pointerId: number): void {
	try {
		if (target.hasPointerCapture(pointerId))
			target.releasePointerCapture(pointerId);
	} catch {
		// See above.
	}
}
