// Layout and geometry shared by all renderers (PlotLayout.cs, PlotTransform.cs).
import { formatG6, powerOfTen, roundTo } from './format';
import type { Axes, PlotPoint } from './model';

/** A rectangle on the screen; y grows downwards. */
export interface ScreenRect {
	readonly left: number;
	readonly top: number;
	readonly width: number;
	readonly height: number;
}

export function right(rect: ScreenRect): number {
	return rect.left + rect.width;
}

export function bottom(rect: ScreenRect): number {
	return rect.top + rect.height;
}

/** Space around a window's plot area, in pixels, for the tick labels and titles. */
export const marginLeft = 62, marginRight = 18, marginTop = 28, marginBottom = 44;

/** The data area inside a cell, leaving room for tick labels and titles. */
export function plotArea(cell: ScreenRect): ScreenRect {
	return {
		left: cell.left + marginLeft,
		top: cell.top + marginTop,
		width: Math.max(1, cell.width - marginLeft - marginRight),
		height: Math.max(1, cell.height - marginTop - marginBottom),
	};
}

/**
 * Splits a curve into connected runs: breaks at non-finite points and, when breakAtJumps is set,
 * where the curve jumps from beyond one y limit to beyond the other (a pole such as tan at π/2).
 */
export function segments(points: readonly PlotPoint[], axes: Axes, breakAtJumps: boolean): PlotPoint[][] {
	const result: PlotPoint[][] = [];
	let current: PlotPoint[] = [];
	let previousSide = 0;
	for (const p of points) {
		if (!Number.isFinite(p.x) || !Number.isFinite(p.y)) {
			if (current.length > 0)
				result.push(current);
			current = [];
			continue;
		}
		const side = p.y > axes.yMax ? 1 : p.y < axes.yMin ? -1 : 0;
		if (breakAtJumps && current.length > 0 && side !== 0 && side === -previousSide) {
			result.push(current);
			current = [];
		}
		previousSide = side;
		current.push(p);
	}
	if (current.length > 0)
		result.push(current);
	return result;
}

/** Keeps far-away screen points at a finite distance; renderers misbehave with huge coordinates. */
export function clamp(x: number, y: number, plot: ScreenRect): [number, number] {
	const margin = 10 * Math.max(plot.width, plot.height);
	return [
		Math.min(Math.max(x, plot.left - margin), right(plot) + margin),
		Math.min(Math.max(y, plot.top - margin), bottom(plot) + margin),
	];
}

/** A tick value as the labels show it. */
export function formatTick(value: number): string {
	return formatG6(value);
}

/** Maps between a window's data coordinates and a rectangle on the screen. */
export class PlotTransform {
	constructor(readonly axes: Axes, readonly screen: ScreenRect) {
	}

	toScreenX(x: number): number {
		return this.screen.left + (x - this.axes.xMin) / (this.axes.xMax - this.axes.xMin) * this.screen.width;
	}

	toScreenY(y: number): number {
		return bottom(this.screen) - (y - this.axes.yMin) / (this.axes.yMax - this.axes.yMin) * this.screen.height;
	}

	toWorldX(screenX: number): number {
		return this.axes.xMin + (screenX - this.screen.left) / this.screen.width * (this.axes.xMax - this.axes.xMin);
	}

	toWorldY(screenY: number): number {
		return this.axes.yMin + (bottom(this.screen) - screenY) / this.screen.height * (this.axes.yMax - this.axes.yMin);
	}
}

/** A safety limit; a normal axis has fewer than 20 ticks. */
const maxTicks = 1000;

/** "Nice" tick values (multiples of 1, 2 or 5 × 10ⁿ) covering [min, max], about targetCount of them. */
export function axisTicks(min: number, max: number, targetCount = 6): number[] {
	if (!(max > min) || targetCount < 1)
		return [];
	const step = niceStep((max - min) / targetCount);
	const first = Math.ceil(min / step) * step;
	// A span that overflows (-1e308..1e308) or underflows gives a step of infinity, 0 or NaN, and
	// the loop below would never end.
	if (!Number.isFinite(step) || step <= 0 || !Number.isFinite(first))
		return [];
	// Round to the step's decimals so 3 × 0.1 becomes 0.3, not 0.30000000000000004.
	const decimals = Math.min(Math.max(-Math.floor(Math.log10(step)), 0), 15);
	const ticks: number[] = [];
	for (let i = 0; i <= maxTicks; i++) {
		let value = first + i * step;
		if (value > max + step * 1e-9)
			break;
		value = roundTo(value, decimals);
		// When the step is below the resolution of the values (1e15 … 1e15 + 0.25), several steps
		// round to the same number; one tick each is enough.
		if (ticks.length > 0 && value <= ticks.at(-1)!)
			continue;
		ticks.push(value === 0 ? 0 : value);
	}
	return ticks;
}

/** The smallest of 1, 2, 5 × 10ⁿ that is at least rough. */
export function niceStep(rough: number): number {
	const magnitude = powerOfTen(Math.floor(Math.log10(rough)));
	const fraction = rough / magnitude;
	const nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
	return nice * magnitude;
}
