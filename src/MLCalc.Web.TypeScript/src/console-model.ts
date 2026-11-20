import type { Limits, OutputMessage, RunError, RunFinishedMessage } from './protocol';

/** What a console line is, for its style (the CSS class). */
export type ConsoleLineKind = 'input' | 'value' | 'text' | 'error' | 'info';

/** One entry in the console; the text may have several lines. Compared by identity. */
export interface ConsoleLine {
	readonly kind: ConsoleLineKind;
	readonly text: string;
}

/** Older lines are dropped beyond this, to keep the page fast. */
export const maxLines = 2000;

const mainPrompt = '>>';
const continuationPrompt = '...';

/**
 * The console without its user interface: the lines shown, unfinished input, and the history of
 * typed lines. The page sends what submit returns to the server and passes the answers back in.
 * (The same as the Blazor client's ConsoleModel.cs.)
 */
export class ConsoleModel {
	readonly #lines: ConsoleLine[] = [];
	readonly #unfinished: string[] = [];
	readonly #history: string[] = [];
	#historyIndex = 0;
	#lastLine: string | null = null;
	#lastSource: string | null = null;

	get lines(): readonly ConsoleLine[] {
		return this.#lines;
	}

	/** ">>", or "..." while an unfinished program waits for more lines. */
	get prompt(): string {
		return this.#unfinished.length === 0 ? mainPrompt : continuationPrompt;
	}

	/** True while an unfinished program waits for more lines. */
	get isContinuing(): boolean {
		return this.#unfinished.length > 0;
	}

	/**
	 * The user pressed Enter with line, which may hold several lines when pasted. Returns the
	 * program to run, or null when there is nothing to run (an empty line).
	 */
	submit(line: string): string | null {
		this.#add('input', `${this.prompt} ${line}`);
		if (line.length > 0 && (this.#history.length === 0 || this.#history.at(-1) !== line))
			this.#history.push(line);
		this.#historyIndex = this.#history.length;
		if (line.trim().length === 0 && this.#unfinished.length === 0)
			return null;
		this.#lastLine = line;
		// An empty line after unfinished input runs it anyway, which reports what is missing.
		this.#lastSource = [...this.#unfinished, line].join('\n');
		return this.#lastSource;
	}

	/** A script the user opened: runs as a whole, even when it is unfinished. */
	openScript(fileName: string, text: string): string {
		this.#add('info', `Running ${fileName}`);
		this.#unfinished.length = 0;
		this.#lastLine = null;
		// A last empty line makes the server run it even when unfinished, and report what is missing.
		this.#lastSource = text.endsWith('\n') ? text + '\n' : text + '\n\n';
		return this.#lastSource;
	}

	/** Esc while nothing runs: forgets unfinished input. */
	discardUnfinished(): void {
		if (this.#unfinished.length === 0)
			return;
		this.#unfinished.length = 0;
		this.#add('info', 'Discarded the unfinished input.');
	}

	/** The previous (step = −1) or next (+1) line of the history; null when there is none. */
	recall(step: -1 | 1): string | null {
		const index = this.#historyIndex + step;
		if (index < 0 || index > this.#history.length)
			return null;
		this.#historyIndex = index;
		return index === this.#history.length ? '' : this.#history[index] ?? '';
	}

	applyOutput(message: OutputMessage): void {
		for (const entry of message.entries) {
			switch (entry.kind) {
				case 'clear':
					this.#lines.length = 0;
					break;
				case 'value':
					this.#add('value', entry.text);
					break;
				default:
					this.#add('text', entry.text);
					break;
			}
		}
	}

	/** The end of a run: keeps unfinished input, or reports errors and stops. */
	applyRunFinished(message: RunFinishedMessage, limits: Limits): void {
		if (message.status === 'incomplete') {
			if (this.#lastLine !== null)
				this.#unfinished.push(this.#lastLine);
			return;
		}
		const source = this.#lastSource;
		this.#unfinished.length = 0;
		this.#lastLine = null;
		this.#lastSource = null;
		switch (message.status) {
			case 'error':
				for (const error of message.errors ?? [])
					this.#add('error', describe(error, source));
				break;
			case 'cancelled':
				this.#add('info', 'Stopped.');
				break;
			case 'timeLimit':
				this.#add('info', `Stopped: a program may run for at most ${limits.runSeconds} seconds here.`);
				break;
			case 'busy':
				this.#add('info', 'Another program is still running.');
				break;
		}
		if (message.outputTruncated)
			this.#add('info', `Only the first ${limits.maxOutputEntries} lines of output are shown.`);
	}

	addInfo(text: string): void {
		this.#add('info', text);
	}

	clear(): void {
		this.#lines.length = 0;
	}

	#add(kind: ConsoleLineKind, text: string): void {
		this.#lines.push({ kind, text });
		if (this.#lines.length > maxLines)
			this.#lines.splice(0, this.#lines.length - maxLines);
	}
}

/**
 * An error: for one line of input a caret under the place, as the desktop app shows it;
 * otherwise the line and column.
 */
function describe(error: RunError, source: string | null): string {
	if (error.line === 0)
		return error.message;
	const oneLine = source !== null && !source.replace(/\n+$/, '').includes('\n');
	if (oneLine)
		return ' '.repeat(mainPrompt.length + error.column) + '^\n' + error.message;
	return `Line ${error.line}, column ${error.column}: ${error.message}`;
}
