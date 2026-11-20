// Turning and moving a 3D window's camera with the mouse or fingers (CameraControl.cs).
import { formatGeneral, roundTo } from './format';
import type { ScreenRect } from './layout';
import type { Axes } from './model';
import { Projection3D } from './projection3d';
import { viewArea } from './scene3d';

/** How far a drag turns the camera: a drag of 720 pixels goes once around. */
export const degreesPerPixel = 0.5;

/** How much one notch of the mouse wheel (a delta of 120) moves the camera towards its target. */
export const wheelStep = 0.85;

/** The camera's distance to its target, in half-boxes, stays between these. */
export const minCameraDistance = 0.05, maxCameraDistance = 100;

/**
 * The camera angles after dragging by (dx, dy) pixels from where the drag began, when the angles
 * were azimuth and elevation. The box follows the pointer: dragging right turns its front to the
 * right, dragging down tips its top towards you. The elevation stops at ±90°; the azimuth is kept
 * between −180° and 180°. Both are rounded to a tenth of a degree, so that the numbers shown can be
 * typed into view to get the same picture.
 */
export function drag(azimuth: number, elevation: number, dx: number, dy: number): [number, number] {
	let az = roundTo(ieeeRemainder(azimuth - dx * degreesPerPixel, 360), 1);
	if (az <= -180)
		az += 360;
	const el = roundTo(Math.min(Math.max(elevation + dy * degreesPerPixel, -90), 90), 1);
	return [az === 0 ? 0 : az, el === 0 ? 0 : el];
}

/** Math.IEEERemainder: x − y × (x / y rounded to the nearest integer, halves to even). */
function ieeeRemainder(x: number, y: number): number {
	const quotient = x / y;
	let n = Math.round(quotient);
	if (Math.abs(quotient % 1) === 0.5)
		n = 2 * Math.round(quotient / 2);
	return x - y * n;
}

/** The factor for zoom from a wheel delta (120 a notch): turning the wheel away from you moves closer. */
export function wheelFactor(delta: number): number {
	return Math.pow(wheelStep, delta / 120);
}

/**
 * axes with the camera turned to azimuth and elevation (degrees) around its target, as view(az, el)
 * does. The camera is then placed relative to the box; a camera set with camposition keeps its
 * distance to the target.
 */
export function turn(axes: Axes, azimuth: number, elevation: number): Axes {
	if (!Number.isFinite(azimuth) || !(elevation >= -90 && elevation <= 90))
		throw new RangeError('The azimuth must be a number and the elevation between -90 and 90 degrees.');
	const distance = axes.camera.position === null ? axes.camera.distance : Projection3D.cameraDistance(axes);
	return { ...axes, camera: { ...axes.camera, azimuth, elevation, distance, position: null } };
}

/**
 * axes with the camera moved towards its target (factor below 1) or away from it (above 1), keeping
 * its direction. cell is the window's rectangle on the screen: the default camera's distance depends
 * on it. A camera set with camposition stays a fixed point, moved along the line to the target.
 */
export function zoom(axes: Axes, factor: number, cell: ScreenRect): Axes {
	if (!(factor > 0) || !Number.isFinite(factor))
		throw new RangeError('The factor must be a positive number.');
	const view = new Projection3D(axes, viewArea(cell));
	const distance = view.distanceToTarget;
	const moved = Math.min(Math.max(distance * factor, minCameraDistance), maxCameraDistance);
	if (!view.isPlaced)
		return { ...axes, camera: { ...axes.camera, distance: moved } };
	const position = axes.camera.position!;
	const target = view.targetPosition;
	const s = moved / distance;
	return {
		...axes,
		camera: {
			...axes.camera,
			position: {
				x: target.x + (position.x - target.x) * s,
				y: target.y + (position.y - target.y) * s,
				z: target.z + (position.z - target.z) * s,
			},
		},
	};
}

/**
 * The camera as the command that sets it, for the status bar: view(az, el) while the camera is
 * placed relative to the box at the default distance, otherwise camposition(x, y, z).
 */
export function describe(axes: Axes, cell: ScreenRect): string {
	const camera = axes.camera;
	if (camera.position === null && camera.distance === null)
		return `view(${format(camera.azimuth)}, ${format(camera.elevation)})`;
	const p = new Projection3D(axes, viewArea(cell)).cameraPosition;
	// Rounding leaves 1e-15 where the camera is level with the middle of the box; show 0.
	return `camposition(${formatNear(p.x, axes.xMax - axes.xMin)}, ${formatNear(p.y, axes.yMax - axes.yMin)}, ${formatNear(p.z, axes.zMax - axes.zMin)})`;
}

function formatNear(value: number, span: number): string {
	return format(Math.abs(value) < span * 1e-9 ? 0 : value);
}

function format(value: number): string {
	const text = formatGeneral(value, 4);
	return text === '-0' ? '0' : text;
}
