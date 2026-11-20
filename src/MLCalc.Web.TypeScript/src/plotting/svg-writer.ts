// One plot window as SVG text, the same to the character as SvgWriter.RenderWindow in
// src/MLCalc.Plotting (checked against tests/fixtures/drawings, which the C# code writes).
import { formatFixed2 as f2, formatRoundTrip as r, roundTo } from './format';
import { axisTicks, bottom, clamp, formatTick, plotArea, PlotTransform, right, segments, type ScreenRect } from './layout';
import { penColor, type Axes, type PlotItem, type PlotPoint } from './model';

const markerSize = 3.5, dotSize = 2.2;

/** An item drawn in a 3D window; set by the 3D module, which needs the projection (milestone 3). */
export type SpaceRenderer = (svg: string[], axes: Axes, cell: ScreenRect, isCurrent: boolean, clip: string, preview: boolean) => void;

let renderSpace: SpaceRenderer = () => {
};

export function setSpaceRenderer(renderer: SpaceRenderer): void {
	renderSpace = renderer;
}

/**
 * One window as its own SVG document, width × height pixels. id makes the clip path's name unique
 * on the page; isCurrent frames the window as the one plot commands draw in; preview draws large
 * 3D items thinned out while the camera moves.
 */
export function renderWindow(axes: Axes, width: number, height: number, id = 'w', isCurrent = false, preview = false): string {
	if (!(width >= 1 && height >= 1 && width <= 16000 && height <= 16000))
		throw new RangeError('The image size must be between 1 and 16000.');
	const svg: string[] = [];
	svg.push(`<svg xmlns="http://www.w3.org/2000/svg" width="${r(width)}" height="${r(height)}" viewBox="0 0 ${r(width)} ${r(height)}" font-family="Segoe UI, Helvetica, Arial, sans-serif">\n`);
	svg.push(`<rect width="${r(width)}" height="${r(height)}" fill="white"/>\n`);
	renderAxes(svg, axes, { left: 0, top: 0, width, height }, isCurrent, id + '-clip', preview);
	svg.push('</svg>\n');
	return svg.join('');
}

function renderAxes(svg: string[], axes: Axes, cell: ScreenRect, isCurrent: boolean, clip: string, preview: boolean): void {
	if (axes.kind === 'space') {
		renderSpace(svg, axes, cell, isCurrent, clip, preview);
		return;
	}
	const plot = plotArea(cell);
	if (plot.width < 20 || plot.height < 20)
		return;
	const t = new PlotTransform(axes, plot);
	const xTicks = axisTicks(axes.xMin, axes.xMax, Math.max(2, Math.trunc(plot.width / 90)));
	const yTicks = axisTicks(axes.yMin, axes.yMax, Math.max(2, Math.trunc(plot.height / 50)));

	if (axes.showGrid) {
		for (const x of xTicks)
			line(svg, t.toScreenX(x), plot.top, t.toScreenX(x), bottom(plot), '#e2e2e2', 1, ' stroke-dasharray="4 3"');
		for (const y of yTicks)
			line(svg, plot.left, t.toScreenY(y), right(plot), t.toScreenY(y), '#e2e2e2', 1, ' stroke-dasharray="4 3"');
	}
	if (axes.xMin < 0 && axes.xMax > 0)
		line(svg, t.toScreenX(0), plot.top, t.toScreenX(0), bottom(plot), '#bbb', 1);
	if (axes.yMin < 0 && axes.yMax > 0)
		line(svg, plot.left, t.toScreenY(0), right(plot), t.toScreenY(0), '#bbb', 1);

	svg.push(`<clipPath id="${clip}"><rect x="${r(plot.left)}" y="${r(plot.top)}" width="${r(plot.width)}" height="${r(plot.height)}"/></clipPath>\n`);
	svg.push(`<g clip-path="url(#${clip})">\n`);
	for (const item of axes.items)
		renderItem(svg, item, t, plot);
	svg.push('</g>\n');

	svg.push(`<rect x="${r(plot.left)}" y="${r(plot.top)}" width="${r(plot.width)}" height="${r(plot.height)}" fill="none" stroke="${isCurrent ? '#1f77b4' : '#999'}" stroke-width="${isCurrent ? 2 : 1}"/>\n`);

	for (const x of xTicks) {
		const sx = t.toScreenX(x);
		line(svg, sx, bottom(plot), sx, bottom(plot) + 4, '#999', 1);
		text(svg, formatTick(x), sx, bottom(plot) + 17, 11, 'middle');
	}
	for (const y of yTicks) {
		const sy = t.toScreenY(y);
		line(svg, plot.left - 4, sy, plot.left, sy, '#999', 1);
		text(svg, formatTick(y), plot.left - 7, sy + 4, 11, 'end');
	}

	const complex = axes.kind === 'complex';
	const xTitle = axes.xTitle.length > 0 ? axes.xTitle : complex ? 'Re' : '';
	const yTitle = axes.yTitle.length > 0 ? axes.yTitle : complex ? 'Im' : '';
	const centerX = plot.left + plot.width / 2, centerY = plot.top + plot.height / 2;
	if (axes.title.length > 0)
		text(svg, axes.title, centerX, cell.top + 19, 13, 'middle', ' font-weight="600"');
	if (xTitle.length > 0)
		text(svg, xTitle, centerX, bottom(plot) + 36, 12, 'middle');
	if (yTitle.length > 0)
		text(svg, yTitle, cell.left + 16, centerY, 12, 'middle', ` transform="rotate(-90 ${r(cell.left + 16)} ${r(centerY)})"`);
}

function renderItem(svg: string[], item: PlotItem, t: PlotTransform, plot: ScreenRect): void {
	const color = penColor(item.pen);
	switch (item.type) {
		case 'series':
			if (item.style === 'line')
				polyline(svg, segments(item.points, t.axes, false), t, plot, color);
			else if (item.style === 'dot')
				dotPath(svg, visible(item.points, t), color);
			else
				markerPath(svg, visible(item.points, t), color, item.style === 'star');
			break;
		case 'function':
			polyline(svg, segments(item.points, t.axes, true), t, plot, color);
			break;
		case 'dots':
			dotPath(svg, visible(item.points, t), color);
			break;
		default:
			// 3D items only appear in 3D windows.
			break;
	}
}

function polyline(svg: string[], runs: readonly (readonly PlotPoint[])[], t: PlotTransform, plot: ScreenRect, color: string): void {
	const path: string[] = [];
	for (const run of runs)
		run.forEach((p, i) => {
			const [x, y] = clamp(t.toScreenX(p.x), t.toScreenY(p.y), plot);
			path.push(`${i === 0 ? 'M' : 'L'}${f2(x)} ${f2(y)} `);
		});
	if (path.length > 0)
		svg.push(`<path d="${path.join('').trimEnd()}" fill="none" stroke="${color}" stroke-width="1.5" stroke-linejoin="round"/>\n`);
}

/** Crosses, or stars (a cross with a diagonal cross), at screen points. */
export function markerPath(svg: string[], points: Iterable<readonly [number, number]>, color: string, star: boolean): void {
	const path: string[] = [];
	for (const [x, y] of points) {
		path.push(`M${f2(x - markerSize)} ${f2(y)}h${r(2 * markerSize)}M${f2(x)} ${f2(y - markerSize)}v${r(2 * markerSize)}`);
		if (star) {
			const d = markerSize * 0.75;
			path.push(`M${f2(x - d)} ${f2(y - d)}l${f2(2 * d)} ${f2(2 * d)}M${f2(x - d)} ${f2(y + d)}l${f2(2 * d)} ${f2(-2 * d)}`);
		}
	}
	if (path.length > 0)
		svg.push(`<path d="${path.join('')}" fill="none" stroke="${color}" stroke-width="1.5"/>\n`);
}

/** Small squares at screen points, each place once. */
export function dotPath(svg: string[], points: Iterable<readonly [number, number]>, color: string): void {
	const path: string[] = [];
	const seen = new Set<string>();
	for (const [x, y] of points) {
		// As C#'s Distinct on (x, y): 0 and -0 are the same place.
		const key = `${x + 0},${y + 0}`;
		if (seen.has(key))
			continue;
		seen.add(key);
		path.push(`M${f2(x - dotSize / 2)} ${f2(y - dotSize / 2)}h${r(dotSize)}v${r(dotSize)}h${r(-dotSize)}z`);
	}
	if (path.length > 0)
		svg.push(`<path d="${path.join('')}" fill="${color}"/>\n`);
}

/** The finite points that are on the screen (or within a marker of it), rounded to 1/100 pixel. */
function* visible(points: readonly PlotPoint[], t: PlotTransform): Generator<readonly [number, number]> {
	const s = t.screen;
	for (const p of points) {
		if (!Number.isFinite(p.x) || !Number.isFinite(p.y))
			continue;
		const x = t.toScreenX(p.x), y = t.toScreenY(p.y);
		if (x >= s.left - markerSize && x <= right(s) + markerSize && y >= s.top - markerSize && y <= bottom(s) + markerSize)
			yield [roundTo(x, 2), roundTo(y, 2)];
	}
}

/** A line, as the 2D windows draw axes, ticks and grid lines. */
export function line(svg: string[], x1: number, y1: number, x2: number, y2: number, color: string, width: number, extra = ''): void {
	svg.push(`<line x1="${f2(x1)}" y1="${f2(y1)}" x2="${f2(x2)}" y2="${f2(y2)}" stroke="${color}" stroke-width="${r(width)}"${extra}/>\n`);
}

export function text(svg: string[], content: string, x: number, y: number, size: number, anchor: string, extra = ''): void {
	svg.push(`<text x="${f2(x)}" y="${f2(y)}" font-size="${r(size)}" fill="#333" text-anchor="${anchor}"${extra}>${escapeXml(content)}</text>\n`);
}

/** As .NET's SecurityElement.Escape. */
function escapeXml(content: string): string {
	return content.replace(/[<>"'&]/g, c => ({ '<': '&lt;', '>': '&gt;', '"': '&quot;', '\'': '&apos;', '&': '&amp;' })[c]!);
}
