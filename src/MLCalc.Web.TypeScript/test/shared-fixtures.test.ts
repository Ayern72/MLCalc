// The test cases shared with the C# code (tests/fixtures, written by SharedFixtureTests.cs): the
// TypeScript port must format numbers, draw windows and describe cameras exactly as the C# code does.
import { describe, expect, it } from 'vitest';
import { toAxes } from '../src/figure-state';
import { describe as describeCamera, drag, turn, wheelFactor, zoom } from '../src/plotting/camera';
import { formatFixed2, formatG5, formatG6, formatRoundTrip, roundTo } from '../src/plotting/format';
import { axisTicks } from '../src/plotting/layout';
import { Projection3D } from '../src/plotting/projection3d';
import { renderWindow } from '../src/plotting/svg-writer';
import type { PlotWindow } from '../src/protocol';
import numberFormat from '../../../tests/fixtures/number-format.json';

interface DrawingFixture {
	name: string;
	program: string;
	width: number;
	height: number;
	isCurrent: boolean;
	preview: boolean;
	window: PlotWindow;
	svg: string[];
	/** For 3D windows: the status bar's camera as drawn, after two notches of the wheel, and after a drag. */
	camera?: { describe: string; zoomed: string; dragged: string };
}

const drawings = Object.values(import.meta.glob<DrawingFixture>('../../../tests/fixtures/drawings/*.json', { eager: true, import: 'default' }));

describe('number formatting, as .NET', () => {
	for (const { value, fixed2, g6, g5, roundTrip, round2 } of numberFormat.numbers) {
		it(`formats ${Object.is(value, -0) ? '-0' : value}`, () => {
			expect(formatFixed2(value)).toBe(fixed2);
			expect(formatG6(value)).toBe(g6);
			expect(formatG5(value)).toBe(g5);
			expect(formatRoundTrip(value)).toBe(roundTrip);
			expect(formatRoundTrip(roundTo(value, 2))).toBe(formatRoundTrip(round2));
		});
	}
});

describe('axis ticks, as .NET', () => {
	for (const { min, max, count, ticks } of numberFormat.ticks)
		it(`ticks ${min} to ${max}, about ${count}`, () => expect(axisTicks(min, max, count)).toEqual(ticks));
});

describe('drawings, as the C# SvgWriter', () => {
	it('has the shared cases, 2D and 3D', () => {
		expect(drawings.length).toBeGreaterThanOrEqual(20);
		expect(drawings.some(drawing => drawing.window.kind === 'space')).toBe(true);
	});
	for (const drawing of drawings) {
		it(`draws ${drawing.name}`, () => {
			const svg = renderWindow(toAxes(drawing.window), drawing.width, drawing.height, 'w', drawing.isCurrent, drawing.preview);
			expect(svg.replace(/\n$/, '').split('\n')).toEqual(drawing.svg);
		});
	}
});

describe('cameras, as the C# CameraControl', () => {
	for (const drawing of drawings.filter(drawing => drawing.camera !== undefined)) {
		it(`describes, zooms and turns ${drawing.name}`, () => {
			const axes = toAxes(drawing.window);
			const cell = { left: 0, top: 0, width: drawing.width, height: drawing.height };
			const [azimuth, elevation] = Projection3D.viewAngles(axes);
			const [draggedAzimuth, draggedElevation] = drag(azimuth, elevation, 37, -23);
			expect({
				describe: describeCamera(axes, cell),
				zoomed: describeCamera(zoom(axes, wheelFactor(240), cell), cell),
				dragged: describeCamera(turn(axes, draggedAzimuth, draggedElevation), cell),
			}).toEqual(drawing.camera);
		});
	}
});
