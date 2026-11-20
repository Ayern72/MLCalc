import { describe, expect, it } from 'vitest';
import { ConsoleModel, maxLines } from '../src/console-model';
import type { Limits } from '../src/protocol';

const limits: Limits = { runSeconds: 60, maxMatrixElements: 2_000_000, maxPointsPerWindow: 200_000, maxOutputEntries: 5000, maxSourceLength: 200_000 };

describe('ConsoleModel', () => {
	it('shows input after the prompt and returns it to run', () => {
		const console = new ConsoleModel();
		expect(console.submit('1 + 2')).toBe('1 + 2');
		expect(console.lines.at(-1)).toEqual({ kind: 'input', text: '>> 1 + 2' });
	});

	it('runs nothing for an empty line', () => {
		const console = new ConsoleModel();
		expect(console.submit('  ')).toBeNull();
	});

	it('collects unfinished input and continues with the ... prompt', () => {
		const console = new ConsoleModel();
		console.submit('for k = 1:3');
		console.applyRunFinished({ id: '1', status: 'incomplete', outputTruncated: false }, limits);
		expect(console.prompt).toBe('...');
		expect(console.submit('disp(k)')).toBe('for k = 1:3\ndisp(k)');
		console.applyRunFinished({ id: '2', status: 'incomplete', outputTruncated: false }, limits);
		expect(console.submit('end')).toBe('for k = 1:3\ndisp(k)\nend');
		console.applyRunFinished({ id: '3', status: 'ok', outputTruncated: false }, limits);
		expect(console.prompt).toBe('>>');
	});

	it('puts a caret under the place of an error in one line of input', () => {
		const console = new ConsoleModel();
		console.submit('1 + * 2');
		console.applyRunFinished({ id: '1', status: 'error', outputTruncated: false, errors: [{ message: 'Unexpected *.', line: 1, column: 5, length: 1 }] }, limits);
		expect(console.lines.at(-1)).toEqual({ kind: 'error', text: '       ^\nUnexpected *.' });
	});

	it('gives the line and column of an error in a script', () => {
		const console = new ConsoleModel();
		console.openScript('a.m', 'x = 1\ny = (\n');
		console.applyRunFinished({ id: '1', status: 'error', outputTruncated: false, errors: [{ message: 'Missing ).', line: 2, column: 6, length: 0 }] }, limits);
		expect(console.lines.at(-1)?.text).toBe('Line 2, column 6: Missing ).');
	});

	it('runs an opened script whole, with an empty last line', () => {
		const console = new ConsoleModel();
		expect(console.openScript('a.m', 'x = 1')).toBe('x = 1\n\n');
		expect(console.openScript('b.m', 'x = 1\n')).toBe('x = 1\n\n');
	});

	it('recalls earlier input with Up and Down', () => {
		const console = new ConsoleModel();
		console.submit('a');
		console.submit('b');
		console.submit('b');
		expect(console.recall(-1)).toBe('b');
		expect(console.recall(-1)).toBe('a');
		expect(console.recall(-1)).toBeNull();
		expect(console.recall(1)).toBe('b');
		expect(console.recall(1)).toBe('');
		expect(console.recall(1)).toBeNull();
	});

	it('says why a run stopped', () => {
		const console = new ConsoleModel();
		console.applyRunFinished({ id: '1', status: 'timeLimit', outputTruncated: true }, limits);
		expect(console.lines.map(line => line.text)).toEqual([
			'Stopped: a program may run for at most 60 seconds here.',
			'Only the first 5000 lines of output are shown.',
		]);
	});

	it('clears on a clear entry and keeps at most maxLines lines', () => {
		const console = new ConsoleModel();
		console.applyOutput({ entries: [{ kind: 'value', text: 'x = 1' }, { kind: 'clear', text: '' }, { kind: 'text', text: 'after' }] });
		expect(console.lines).toEqual([{ kind: 'text', text: 'after' }]);
		console.applyOutput({ entries: Array.from({ length: maxLines + 10 }, (_, i) => ({ kind: 'text' as const, text: String(i) })) });
		expect(console.lines).toHaveLength(maxLines);
		expect(console.lines[0]?.text).toBe('10');
	});
});
