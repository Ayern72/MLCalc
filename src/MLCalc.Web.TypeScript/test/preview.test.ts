// The thinned-out preview of a 3D window, with the plot model's limits and with other ones (as
// Scene3D.Preview in C#; the shared drawing cases cover the default limits too).
import { describe, expect, it } from 'vitest';
import { defaultAxes, type Axes, type Point3 } from '../src/plotting/model';
import { previewGridLines, previewOf } from '../src/plotting/scene3d';

function surface(count: number): Axes {
	const points: Point3[] = [];
	for (let j = 0; j < count; j++)
		for (let i = 0; i < count; i++)
			points.push({ x: i, y: j, z: i * j });
	return { ...defaultAxes, kind: 'space', items: [{ type: 'surface', pen: 1, label: 'f', xCount: count, yCount: count, points }] };
}

describe('previewOf', () => {
	it('leaves a surface within the limits alone', () => {
		const axes = surface(previewGridLines);
		expect(previewOf(axes).items[0]).toBe(axes.items[0]);
	});

	it('can thin out further, keeping the first and last grid lines', () => {
		const item = previewOf(surface(previewGridLines), 20, 1000).items[0]!;
		if (item.type !== 'surface')
			throw new Error('not a surface');
		expect(item.xCount).toBeLessThanOrEqual(20);
		expect(item.points.length).toBe(item.xCount * item.yCount);
		expect(item.points[item.points.length - 1]).toEqual({ x: 39, y: 39, z: 39 * 39 });
	});

	it('needs at least two grid lines', () => {
		expect(() => previewOf(surface(4), 1, 1000)).toThrow(RangeError);
	});
});
