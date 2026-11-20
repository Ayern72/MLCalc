import type { ConsoleLine, ConsoleModel } from '../console-model';
import { element } from './dom';

/**
 * Shows a ConsoleModel's lines in the output element. sync() adds the lines that are new and
 * removes those the model dropped (cleared, or beyond its limit), so a batch of output costs only
 * what it adds.
 */
export class ConsoleView {
	readonly #output: HTMLElement;
	readonly #model: ConsoleModel;
	readonly #shown: { line: ConsoleLine; node: HTMLElement }[] = [];

	constructor(output: HTMLElement, model: ConsoleModel) {
		this.#output = output;
		this.#model = model;
	}

	sync(): void {
		const lines = this.#model.lines;
		// The model drops lines only at the front (or all of them).
		const first = lines[0];
		let dropped = 0;
		while (dropped < this.#shown.length && this.#shown[dropped]?.line !== first)
			dropped++;
		for (const { node } of this.#shown.splice(0, dropped))
			node.remove();
		for (let i = this.#shown.length; i < lines.length; i++) {
			const line = lines[i]!;
			const node = element('pre', { class: `line ${line.kind}` }, line.text);
			this.#output.append(node);
			this.#shown.push({ line, node });
		}
		this.#output.scrollTop = this.#output.scrollHeight;
	}
}
