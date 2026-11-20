// The projection of a 3D window (Projection3D.cs): the box set by the x, y and z ranges is scaled to
// the cube −1..1 on each axis, then seen through a perspective camera with z up. The arithmetic is
// done in the same order as in C#, so that the drawings match to the last digit written.
import { clamp, type ScreenRect } from './layout';
import type { Axes, Point3 } from './model';

/** A vector in the cube coordinates of a 3D window. */
export class Vec3 {
	constructor(readonly x: number, readonly y: number, readonly z: number) {
	}

	plus(b: Vec3): Vec3 {
		return new Vec3(this.x + b.x, this.y + b.y, this.z + b.z);
	}

	minus(b: Vec3): Vec3 {
		return new Vec3(this.x - b.x, this.y - b.y, this.z - b.z);
	}

	times(s: number): Vec3 {
		return new Vec3(this.x * s, this.y * s, this.z * s);
	}

	dot(b: Vec3): number {
		return this.x * b.x + this.y * b.y + this.z * b.z;
	}

	cross(b: Vec3): Vec3 {
		return new Vec3(this.y * b.z - this.z * b.y, this.z * b.x - this.x * b.z, this.x * b.y - this.y * b.x);
	}

	get length(): number {
		return Math.sqrt(this.dot(this));
	}

	get normalized(): Vec3 {
		return this.times(1 / this.length);
	}

	get isFinite(): boolean {
		return Number.isFinite(this.x) && Number.isFinite(this.y) && Number.isFinite(this.z);
	}

	/** A key for sets: equal vectors give equal keys (0 and -0 alike, as C#'s equality). */
	get key(): string {
		return `${this.x + 0},${this.y + 0},${this.z + 0}`;
	}
}

/** A point on the screen; y grows downwards. */
export interface ScreenPoint {
	readonly x: number;
	readonly y: number;
}

/** The camera's field of view across the shorter side of the screen rectangle, in degrees. */
export const fieldOfView = 30;

/** The near clipping plane, in cube units in front of the camera. */
const near = 1e-3;

/** A relative camera without a distance moves just far enough back for the whole box to fit, with this margin. */
const fitMargin = 1.04;

const tolerance = 1e-9;

/** The half-spaces n·p + c ≥ 0 that bound what is drawn, except the near plane. */
const boxPlanes: readonly (readonly [Vec3, number])[] = [
	[new Vec3(-1, 0, 0), 1 + tolerance],
	[new Vec3(1, 0, 0), 1 + tolerance],
	[new Vec3(0, -1, 0), 1 + tolerance],
	[new Vec3(0, 1, 0), 1 + tolerance],
	[new Vec3(0, 0, -1), 1 + tolerance],
	[new Vec3(0, 0, 1), 1 + tolerance],
];

/** A data point in cube coordinates: the box from the ranges becomes −1..1 on each axis. */
export function toCube(axes: Axes, p: Point3): Point3 {
	return {
		x: 2 * (p.x - axes.xMin) / (axes.xMax - axes.xMin) - 1,
		y: 2 * (p.y - axes.yMin) / (axes.yMax - axes.yMin) - 1,
		z: 2 * (p.z - axes.zMin) / (axes.zMax - axes.zMin) - 1,
	};
}

export class Projection3D {
	readonly screen: ScreenRect;
	/** Whether the camera is at a point set with camposition, rather than placed relative to the box. */
	readonly isPlaced: boolean;
	readonly eye: Vec3;
	readonly #axes: Axes;
	readonly #target: Vec3;
	readonly #right: Vec3;
	readonly #up: Vec3;
	readonly #forward: Vec3;
	readonly #scale: number;
	readonly #centerX: number;
	readonly #centerY: number;

	/** The projection of a 3D window drawn in screen. */
	constructor(axes: Axes, screen: ScreenRect) {
		this.#axes = axes;
		this.screen = screen;
		this.#centerX = screen.left + screen.width / 2;
		this.#centerY = screen.top + screen.height / 2;
		const tan = Math.tan(fieldOfView / 2 * Math.PI / 180);
		const shorter = Math.max(1, Math.min(screen.width, screen.height));
		this.#scale = shorter / 2 / tan;

		const camera = axes.camera;
		const target = camera.target !== null ? this.cube(camera.target) : new Vec3(0, 0, 0);
		this.#target = target;
		const az = camera.azimuth * Math.PI / 180, el = camera.elevation * Math.PI / 180;
		// The horizontal direction the camera faces, used for "up" when it looks straight down or up.
		let facing = new Vec3(-Math.sin(az), Math.cos(az), 0);
		let toCamera: Vec3;
		const offset = camera.position !== null ? this.cube(camera.position).minus(target) : null;
		if (offset !== null && offset.length > 0 && offset.isFinite) {
			toCamera = offset.normalized;
			facing = new Vec3(0, 1, 0);
		} else {
			// Azimuth 0 looks from −y towards +y, 90 from +x; elevation is the angle above the x-y plane.
			toCamera = new Vec3(Math.sin(az) * Math.cos(el), -Math.cos(az) * Math.cos(el), Math.sin(el));
			if (el < 0)
				facing = facing.times(-1);
		}

		const forward = toCamera.times(-1);
		const z = new Vec3(0, 0, 1);
		const upward = z.minus(forward.times(z.dot(forward)));
		let up = upward.length > 1e-9 ? upward.normalized : facing;
		const right = forward.cross(up).normalized;
		up = right.cross(forward);
		this.#forward = forward;
		this.#right = right;
		this.#up = up;

		const placed = camera.position !== null ? this.cube(camera.position) : null;
		if (placed !== null && placed.isFinite && placed.minus(target).length > 0) {
			this.eye = placed;
			this.isPlaced = true;
		} else {
			const distance = camera.distance ?? this.#fitDistance(target, tan * screen.width / shorter / fitMargin, tan * screen.height / shorter / fitMargin);
			this.eye = target.plus(toCamera.times(distance));
			this.isPlaced = false;
		}
	}

	/** The distance from the camera to the point it looks at, in units of half the box. */
	get distanceToTarget(): number {
		return this.eye.minus(this.#target).length;
	}

	/** Where the camera is, in data coordinates. */
	get cameraPosition(): Point3 {
		return this.#fromCube(this.eye);
	}

	/** The point the camera looks at, in data coordinates. */
	get targetPosition(): Point3 {
		return this.#fromCube(this.#target);
	}

	#fromCube(c: Vec3): Point3 {
		const axes = this.#axes;
		return {
			x: axes.xMin + (c.x + 1) / 2 * (axes.xMax - axes.xMin),
			y: axes.yMin + (c.y + 1) / 2 * (axes.yMax - axes.yMin),
			z: axes.zMin + (c.z + 1) / 2 * (axes.zMax - axes.zMin),
		};
	}

	cube(p: Point3): Vec3 {
		const c = toCube(this.#axes, p);
		return new Vec3(c.x, c.y, c.z);
	}

	/** The depth of a cube point in front of the camera. */
	depth(c: Vec3): number {
		return c.minus(this.eye).dot(this.#forward);
	}

	/** The screen position of a cube point; the point must be in front of the camera. */
	toScreen(c: Vec3): ScreenPoint {
		const d = c.minus(this.eye);
		const depth = d.dot(this.#forward);
		const [x, y] = clamp(this.#centerX + this.#scale * d.dot(this.#right) / depth, this.#centerY - this.#scale * d.dot(this.#up) / depth, this.screen);
		return { x, y };
	}

	/** The screen position of a data point, or null when it is behind the camera. */
	project(p: Point3): ScreenPoint | null {
		const c = this.cube(p);
		return c.isFinite && this.depth(c) >= near ? this.toScreen(c) : null;
	}

	/** Whether a cube point is inside the box (with a little tolerance for rounding) and in front of the camera. */
	isVisible(c: Vec3): boolean {
		return c.isFinite && Math.abs(c.x) <= 1 + tolerance && Math.abs(c.y) <= 1 + tolerance
			&& Math.abs(c.z) <= 1 + tolerance && this.depth(c) >= near;
	}

	/**
	 * Cuts a segment in cube coordinates to the part inside the box and in front of the camera
	 * (Liang–Barsky): the new ends, and whether each was moved; null when nothing is left.
	 */
	clipSegment(a: Vec3, b: Vec3): { a: Vec3; b: Vec3; clippedStart: boolean; clippedEnd: boolean } | null {
		let t0 = 0, t1 = 1;
		for (const [normal, offset] of this.#planes()) {
			const fa = normal.dot(a) + offset, fb = normal.dot(b) + offset;
			if (fa < 0 && fb < 0)
				return null;
			if (fa < 0)
				t0 = Math.max(t0, fa / (fa - fb));
			else if (fb < 0)
				t1 = Math.min(t1, fa / (fa - fb));
			if (t0 > t1)
				return null;
		}
		return {
			a: a.plus(b.minus(a).times(t0)),
			b: a.plus(b.minus(a).times(t1)),
			clippedStart: t0 > 0,
			clippedEnd: t1 < 1,
		};
	}

	/** Cuts a polygon in cube coordinates to the part inside the box and in front of the camera (Sutherland–Hodgman). */
	clipPolygon(polygon: Vec3[]): Vec3[] {
		for (const [normal, offset] of this.#planes()) {
			if (polygon.length === 0)
				break;
			const output: Vec3[] = [];
			for (let i = 0; i < polygon.length; i++) {
				const a = polygon[i]!;
				const b = polygon[(i + 1) % polygon.length]!;
				const fa = normal.dot(a) + offset, fb = normal.dot(b) + offset;
				if (fa >= 0)
					output.push(a);
				if ((fa >= 0) !== (fb >= 0))
					output.push(a.plus(b.minus(a).times(fa / (fa - fb))));
			}
			polygon = output;
		}
		return polygon;
	}

	/** The six sides of the box and the near plane. */
	*#planes(): Generator<readonly [Vec3, number]> {
		yield* boxPlanes;
		yield [this.#forward, -this.eye.dot(this.#forward) - near];
	}

	/**
	 * The smallest distance from the target at which every corner of the box is inside the field of
	 * view, whose half-widths (as tangents) are tanX and tanY.
	 */
	#fitDistance(target: Vec3, tanX: number, tanY: number): number {
		let distance = 0;
		for (let corner = 0; corner < 8; corner++) {
			const q = new Vec3((corner & 1) === 0 ? -1 : 1, (corner & 2) === 0 ? -1 : 1, (corner & 4) === 0 ? -1 : 1).minus(target);
			// The corner's depth is q·forward + distance; it must be at least |x| / tanX and |y| / tanY.
			const needed = Math.max(Math.abs(q.dot(this.#right)) / tanX, Math.abs(q.dot(this.#up)) / tanY);
			distance = Math.max(distance, needed - q.dot(this.#forward) + near);
		}
		return distance;
	}

	/**
	 * The azimuth and elevation, in degrees, that the camera looks from, as view takes them. For a
	 * camera set with camposition they follow from where it is.
	 */
	static viewAngles(axes: Axes): [number, number] {
		const camera = axes.camera;
		if (camera.position !== null) {
			const target = camera.target !== null ? toCube(axes, camera.target) : { x: 0, y: 0, z: 0 };
			const position = toCube(axes, camera.position);
			const offset = new Vec3(position.x - target.x, position.y - target.y, position.z - target.z);
			if (offset.isFinite && offset.length > 0) {
				const d = offset.normalized;
				const elevation = Math.asin(Math.min(Math.max(d.z, -1), 1)) * 180 / Math.PI;
				// Straight above or below, the direction says nothing about the azimuth.
				const azimuth = Math.abs(d.z) > 1 - 1e-12 ? camera.azimuth : Math.atan2(d.x, -d.y) * 180 / Math.PI;
				return [azimuth, elevation];
			}
		}
		return [camera.azimuth, camera.elevation];
	}

	/** The distance from an absolute camera to its target, in cube units (half the box). */
	static cameraDistance(axes: Axes): number {
		const target = axes.camera.target !== null ? toCube(axes, axes.camera.target) : { x: 0, y: 0, z: 0 };
		const position = axes.camera.position !== null ? toCube(axes, axes.camera.position) : target;
		const distance = new Vec3(position.x - target.x, position.y - target.y, position.z - target.z).length;
		if (Number.isFinite(distance) && distance > 0)
			return distance;
		throw new RangeError('The camera has no position.');
	}
}
