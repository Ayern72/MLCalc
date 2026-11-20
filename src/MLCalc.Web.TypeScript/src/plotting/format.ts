// Number formatting as .NET does it, so that the SVG text is the same as the C# drawing code's to
// the character (checked against tests/fixtures/number-format.json, which .NET writes). JavaScript's
// own formatting differs in rounding, negative zero and when it switches to exponents.

/** The decimal digits and exponent of a positive finite number: value ≈ d.ddd × 10^exponent. */
interface Decimal {
	digits: string;
	exponent: number;
}

/**
 * The shortest digits that read back as the number, or fractionDigits + 1 significant digits,
 * correctly rounded with exact halves to even, as .NET rounds them (JavaScript's toExponential
 * rounds exact halves up: 311.125 to five digits is 311.13 there, 311.12 in .NET).
 */
function decimalOf(positive: number, fractionDigits?: number): Decimal {
	if (fractionDigits === undefined)
		return parse(positive.toExponential());
	// A hundred digits are exact for any number whose expansion ends soon enough to be an exact
	// half at this precision.
	const exact = parse(positive.toExponential(99));
	const kept = fractionDigits + 1;
	const tie = exact.digits[kept] === '5' && /^0*$/.test(exact.digits.slice(kept + 1));
	if (!tie)
		return parse(positive.toExponential(fractionDigits));
	const head = exact.digits.slice(0, kept);
	if (Number(head.at(-1)) % 2 === 0)
		return { digits: head, exponent: exact.exponent };
	const rounded = incremented(head);
	return rounded.length > head.length
		? { digits: rounded.slice(0, kept), exponent: exact.exponent + 1 }
		: { digits: rounded, exponent: exact.exponent };
}

/** From toExponential's text, e.g. "1.2345e-7". */
function parse(text: string): Decimal {
	const match = /^(\d)(?:\.(\d+))?e([+-]\d+)$/.exec(text);
	if (match === null)
		throw new Error(`Unexpected exponent form ${text}.`);
	return { digits: match[1]! + (match[2] ?? ''), exponent: Number(match[3]) };
}

/** Adds one to the last digit of a digit string; "999" becomes "1000". */
function incremented(digits: string): string {
	const result = digits.split('');
	for (let i = result.length - 1; i >= 0; i--) {
		if (result[i] !== '9') {
			result[i] = String(Number(result[i]) + 1);
			return result.join('');
		}
		result[i] = '0';
	}
	return '1' + result.join('');
}

/** digits × 10^(exponent − digits.length + 1) written without an exponent, trailing zeros of the fraction left out. */
function fixedText(digits: string, exponent: number): string {
	let integer: string;
	let fraction: string;
	if (exponent >= 0) {
		integer = digits.slice(0, exponent + 1).padEnd(exponent + 1, '0');
		fraction = digits.slice(exponent + 1);
	} else {
		integer = '0';
		fraction = '0'.repeat(-exponent - 1) + digits;
	}
	fraction = fraction.replace(/0+$/, '');
	return fraction.length > 0 ? `${integer}.${fraction}` : integer;
}

/** .NET's "G" layout: fixed while the exponent is above −5 and below switchAt, else d.dddE±XX. */
function generalText(negative: boolean, decimal: Decimal, switchAt: number): string {
	const digits = decimal.digits.replace(/(?<=.)0+$/, '');
	const { exponent } = decimal;
	const sign = negative ? '-' : '';
	if (exponent > -5 && exponent < switchAt)
		return sign + fixedText(digits, exponent);
	const mantissa = digits.length > 1 ? `${digits[0]}.${digits.slice(1)}` : digits;
	return `${sign}${mantissa}E${exponent < 0 ? '-' : '+'}${String(Math.abs(exponent)).padStart(2, '0')}`;
}

function zeroText(value: number): string {
	return Object.is(value, -0) ? '-0' : '0';
}

/**
 * A number in an interpolated C# string ({value}, invariant culture): the shortest text that reads
 * back as the same number, with an exponent from 1E+17 up and below 1E-04.
 */
export function formatRoundTrip(value: number): string {
	if (value === 0)
		return zeroText(value);
	if (!Number.isFinite(value))
		return Number.isNaN(value) ? 'NaN' : value > 0 ? 'Infinity' : '-Infinity';
	return generalText(value < 0, decimalOf(Math.abs(value)), 17);
}

/** value.ToString("G6"), as the tick labels. */
export function formatG6(value: number): string {
	return formatGeneral(value, 6);
}

/** value.ToString("G5"), as the coordinates in the status bar. */
export function formatG5(value: number): string {
	return formatGeneral(value, 5);
}

/** value.ToString("G" + precision): that many significant digits, correctly rounded, e.g. 1.23457E+06. */
export function formatGeneral(value: number, precision: number): string {
	if (value === 0)
		return zeroText(value);
	if (!Number.isFinite(value))
		return formatRoundTrip(value);
	return generalText(value < 0, decimalOf(Math.abs(value), precision - 1), precision);
}

/**
 * value.ToString("0.##"): at most two decimals. .NET first takes 15 significant digits, then rounds
 * those half away from zero, so 1.005 gives "1.01". A negative number that rounds to zero gives "-0".
 */
export function formatFixed2(value: number): string {
	if (value === 0)
		return zeroText(value);
	if (!Number.isFinite(value))
		return formatRoundTrip(value);
	const negative = value < 0;
	const { digits, exponent } = decimalOf(Math.abs(value), 14);
	// The digits that stay: those down to the second decimal.
	const kept = exponent + 1 + 2;
	let result: Decimal;
	if (kept <= 0) {
		if (kept < 0 || digits[0]! < '5')
			return negative ? '-0' : '0';
		result = { digits: '1', exponent: -2 };
	} else if (kept >= digits.length) {
		result = { digits, exponent };
	} else {
		const head = digits.slice(0, kept);
		const up = digits[kept]! >= '5';
		const rounded = up ? incremented(head) : head;
		result = { digits: rounded, exponent: exponent + rounded.length - head.length };
	}
	return (negative ? '-' : '') + fixedText(result.digits, result.exponent);
}

/**
 * Math.Round(value, decimals) (0 to 15 decimals) as .NET computes it: multiplied by 10^decimals,
 * rounded half to even, divided again; from 1e16 up the value is returned as it is. The sign of a
 * zero is kept, so -0.001 gives -0.
 */
export function roundTo(value: number, decimals: number): number {
	if (!(Math.abs(value) < 1e16))
		return value;
	const power = powerOfTen(decimals);
	const result = roundHalfEven(value * power) / power;
	return result === 0 && (value < 0 || Object.is(value, -0)) ? -0 : result;
}

/** Math.Round(value): to the nearest integer, halves to the even one. */
function roundHalfEven(value: number): number {
	const floor = Math.floor(value);
	const fraction = value - floor;
	if (fraction > 0.5)
		return floor + 1;
	if (fraction < 0.5)
		return floor;
	return floor % 2 === 0 ? floor : floor + 1;
}

/** 10^n, exactly as written (Math.pow can be one unit off in the last place). */
export function powerOfTen(n: number): number {
	return Number(`1e${n}`);
}
