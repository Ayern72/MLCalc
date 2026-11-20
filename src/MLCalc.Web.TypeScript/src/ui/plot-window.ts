import { formatG5 } from '../plotting/format';
import { plotArea, PlotTransform, right, bottom } from '../plotting/layout';
import type { Axes } from '../plotting/model';
import { renderWindow } from '../plotting/svg-writer';
import { element } from './dom';

/** What a window needs from its pane. */
export interface PlotWindowHost {
	/** Text for the status bar: what is under the mouse, or empty. */
	status(text: string): void;
}

/**
 * One plot window, drawn as SVG with the same code as the C# SVG export (ported). A transparent
 * layer on top takes the mouse, so that positions are measured from the window's corner.
 * (The Blazor client's PlotWindowView.razor.)
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
	#gridLabel = '';

	constructor(host: PlotWindowHost) {
		this.#host = host;
		this.#drawing = element('div');
		this.#mouse = element('div', { class: 'plot-mouse' });
		this.element = element('div', { class: 'plot-window' }, this.#drawing, this.#mouse);
		this.#mouse.addEventListener('pointermove', event => this.#host.status(this.#describe(event.offsetX, event.offsetY)));
		this.#mouse.addEventListener('pointerleave', () => this.#host.status(''));
	}

	/** Draws again only when the window, its size or its frame changed. */
	update(axes: Axes, width: number, height: number, isCurrent: boolean, id: string, gridLabel: string): void {
		this.#gridLabel = gridLabel;
		if (axes === this.#axes && width === this.#width && height === this.#height && isCurrent === this.#isCurrent && id === this.#id)
			return;
		this.#axes = axes;
		this.#width = width;
		this.#height = height;
		this.#isCurrent = isCurrent;
		this.#id = id;
		this.#mouse.classList.toggle('turnable', axes.kind === 'space');
		this.#draw(axes, false);
	}

	#draw(axes: Axes, preview: boolean): void {
		// The text comes from our own writer, which escapes all text it puts in.
		this.#drawing.innerHTML = this.#width >= 1 && this.#height >= 1
			? renderWindow(axes, this.#width, this.#height, this.#id, this.#isCurrent, preview)
			: '';
	}

	/** The status bar text for the mouse at (x, y) in the window, as in the desktop app. */
	#describe(x: number, y: number): string {
		const axes = this.#axes;
		if (axes === null || axes.kind === 'space')
			return '';
		const plot = plotArea({ left: 0, top: 0, width: this.#width, height: this.#height });
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
