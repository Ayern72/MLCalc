import type { FigureState } from '../figure-state';
import { element } from './dom';
import { PlotWindowView, type PlotWindowHost } from './plot-window';

/**
 * The grid of plot windows. It measures itself, so each window is drawn at its real size in
 * pixels, and redraws when its size or the figure changes. (The Blazor client's PlotPane.razor.)
 */
export class PlotPane {
	readonly element: HTMLDivElement;
	readonly #figure: FigureState;
	readonly #host: PlotWindowHost;
	#views: PlotWindowView[] = [];
	#width = 0;
	#height = 0;
	#pending = false;

	constructor(figure: FigureState, host: PlotWindowHost) {
		this.#figure = figure;
		this.#host = host;
		this.element = element('div', { class: 'plot-grid' });
		// At most once per animation frame, also while the divider is dragged.
		new ResizeObserver(() => {
			if (this.#pending)
				return;
			this.#pending = true;
			requestAnimationFrame(() => {
				this.#pending = false;
				this.#width = Math.floor(this.element.clientWidth);
				this.#height = Math.floor(this.element.clientHeight);
				this.update();
			});
		}).observe(this.element);
	}

	/** Brings the windows in line with the figure. */
	update(): void {
		const figure = this.#figure;
		const count = figure.rows * figure.columns;
		if (this.#views.length !== count) {
			this.#views = Array.from({ length: count }, () => new PlotWindowView(this.#host));
			this.element.replaceChildren(...this.#views.map(view => view.element));
		}
		this.element.style.gridTemplateRows = `repeat(${figure.rows}, 1fr)`;
		this.element.style.gridTemplateColumns = `repeat(${figure.columns}, 1fr)`;
		if (this.#width <= 0 || this.#height <= 0)
			return;
		const several = count > 1;
		for (let row = 0; row < figure.rows; row++)
			for (let column = 0; column < figure.columns; column++)
				this.#views[row * figure.columns + column]!.update(
					figure.window(row, column), row, column,
					this.#width / figure.columns,
					this.#height / figure.rows,
					several && row === figure.currentRow && column === figure.currentColumn,
					several ? `window (${row + 1},${column + 1})   ` : '');
	}
}
