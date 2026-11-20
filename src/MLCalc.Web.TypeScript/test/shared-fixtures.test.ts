// The test cases shared with the C# code (tests/fixtures, written by SharedFixtureTests.cs): the
// TypeScript port must format numbers and draw windows exactly as the C# code does.
import { describe, expect, it } from 'vitest';
import { toAxes } from '../src/figure-state';
import { formatFixed2, formatG5, formatG6, formatRoundTrip, roundTo } from '../src/plotting/format';
import { axisTicks } from '../src/plotting/layout';
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
}

const drawings = Object.values(import.meta.glob<DrawingFixture>('../../../tests/fixtures/drawings/*.json', { eager: true, import: 'default' }));

describe('number formatting, as .NET', () => {
	for (const { value, fixed2, g6, g5, roundTrip, round2 } of numberFormat.numbers) {
		it(`formats ${Object.is(value, -0) ? '-0' : value}`, () => {
			expect(formatFixed2(value)).toBe(fixed2);
			expect(formatG6(value)).toBe(g6);
			expect(formatG5(value)).toBe(g5);
			expect(formatRoundTrip(value)).toBe(roundTrip);
			expect(Object.is(roundTo(value, 2), round2) || roundTo(value, 2) === round2).toBe(true);
			expect(formatRoundTrip(roundTo(value, 2))).toBe(formatRoundTrip(round2));
		});
	}
});

describe('axis ticks, as .NET', () => {
	for (const { min, max, count, ticks } of numberFormat.ticks)
		it(`ticks ${min} to ${max}, about ${count}`, () => expect(axisTicks(min, max, count)).toEqual(ticks));
});

describe('drawings, as the C# SvgWriter', () => {
	it('has the shared cases', () => expect(drawings.length).toBeGreaterThanOrEqual(10));
	for (const drawing of drawings) {
		it(`draws ${drawing.name}`, () => {
			const svg = renderWindow(toAxes(drawing.window), drawing.width, drawing.height, 'w', drawing.isCurrent, drawing.preview);
			expect(svg.replace(/\n$/, '').split('\n')).toEqual(drawing.svg);
		});
	}
});
