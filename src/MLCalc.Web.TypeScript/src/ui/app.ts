import { ConsoleModel } from '../console-model';
import { MLCalcConnection, type ConnectionState } from '../connection';
import type { Limits, WelcomeMessage } from '../protocol';
import { ConsoleView } from './console-view';
import { element } from './dom';
import { download } from './download';

declare const __MLCALC_VERSION__: string;

/** This page's build, set by vite.config.ts; the server's is in Welcome.version. */
export const buildVersion = __MLCALC_VERSION__;

const manualUrl = 'https://github.com/Ayern72/MLCalc/blob/main/docs/manual.md';

/**
 * The page: toolbar, console, plots and status bar, with the same structure and CSS classes as the
 * Blazor client (src/MLCalc.Web.Client/App.razor), whose stylesheet it uses.
 */
export class App {
	readonly #console = new ConsoleModel();
	readonly #connection: MLCalcConnection;

	readonly #openLabel: HTMLLabelElement;
	readonly #openInput: HTMLInputElement;
	readonly #stopButton: HTMLButtonElement;
	readonly #functionsButton: HTMLButtonElement;
	readonly #updateBar: HTMLDivElement;
	readonly #functions: HTMLDivElement;
	readonly #output: HTMLDivElement;
	readonly #prompt: HTMLSpanElement;
	readonly #input: HTMLTextAreaElement;
	readonly #status: HTMLSpanElement;
	readonly #consoleView: ConsoleView;

	readonly panes: HTMLElement;
	readonly divider: HTMLDivElement;
	readonly plots: HTMLElement;

	#welcome: WelcomeMessage | null = null;
	#running = false;
	#runCounter = 0;
	#mouseStatus = '';

	constructor(root: HTMLElement) {
		this.#openInput = element('input', { type: 'file', accept: '.m,.txt' });
		this.#openLabel = element('label', { class: 'button', title: 'Run a .m script from your computer' }, 'Open script…', this.#openInput);
		this.#stopButton = element('button', { class: 'button', title: 'Stop the running program (Esc)' }, 'Stop');
		const clearButton = element('button', { class: 'button', title: 'Clear the console' }, 'Clear');
		this.#functionsButton = element('button', { class: 'button', title: 'List all functions and commands' }, 'Functions');
		const toolbar = element('header', { class: 'toolbar' },
			element('span', { class: 'brand', title: `MLCalc ${buildVersion} (TypeScript client)` }, 'MLCalc'),
			this.#openLabel, this.#stopButton, clearButton, this.#functionsButton,
			element('a', { class: 'help', href: manualUrl, target: '_blank', rel: 'noopener' }, 'Manual'));

		const reload = element('button', { class: 'button' }, 'Reload');
		reload.addEventListener('click', () => location.reload());
		this.#updateBar = element('div', { class: 'update-bar', role: 'status', hidden: '' },
			'MLCalc has been updated since this page was loaded.', reload);

		this.#functions = element('div', { class: 'functions', hidden: '' });
		this.#output = element('div', { class: 'output' });
		this.#prompt = element('span', { class: 'prompt' }, '>>');
		this.#input = element('textarea', { rows: '1', spellcheck: 'false', autocomplete: 'off', 'aria-label': 'Input' });
		const consoleSection = element('section', { class: 'console', 'aria-label': 'Console' },
			this.#functions, this.#output, element('div', { class: 'input-row' }, this.#prompt, this.#input));
		this.divider = element('div', {
			class: 'divider', role: 'separator', tabindex: '0',
			'aria-label': 'Divider between the console and the plots', title: 'Drag to resize; double-click to reset',
		});
		this.plots = element('section', { class: 'plots', 'aria-label': 'Plots' });
		this.panes = element('main', { class: 'panes' }, consoleSection, this.divider, this.plots);

		this.#status = element('span');
		const page = element('div', { class: 'page' }, toolbar, this.#updateBar, this.panes, element('footer', { class: 'status' }, this.#status));
		root.replaceChildren(page);

		this.#consoleView = new ConsoleView(this.#output, this.#console);
		this.#openInput.addEventListener('change', () => void this.#openScript());
		this.#stopButton.addEventListener('click', () => void this.#cancel());
		clearButton.addEventListener('click', () => {
			this.#console.clear();
			this.#update();
		});
		this.#functionsButton.addEventListener('click', () => {
			this.#functions.hidden = !this.#functions.hidden;
			this.#functionsButton.classList.toggle('active', !this.#functions.hidden);
		});
		this.#input.addEventListener('keydown', event => void this.#onKeyDown(event));
		this.#input.addEventListener('input', () => this.#sizeInput());

		this.#connection = new MLCalcConnection({
			welcome: message => this.#onWelcome(message),
			output: message => {
				this.#console.applyOutput(message);
				this.#update();
			},
			figureChanged: () => {
				// Plots come with milestone 2.
			},
			runFinished: message => {
				this.#console.applyRunFinished(message, this.#limits);
				this.#setRunning(false);
				this.#update();
			},
			download,
			stateChanged: state => {
				if (state !== 'connected')
					this.#running = false;
				this.#update();
			},
		});

		this.#console.addInfo('MLCalc — try  [1 2; 3 4] * [1; 1]   or   fplot("x", "sin(x)*x")');
		this.#console.addInfo('Up/Down recall earlier input, Shift+Enter starts a new line, Esc stops a running program.');
		this.#update();
	}

	start(): Promise<void> {
		return this.#connection.start();
	}

	/** Shows what is under the mouse in a plot window; empty shows the app's state again. */
	showMouseStatus(text: string): void {
		this.#mouseStatus = text;
		this.#status.textContent = text.length > 0 ? text : this.#statusText;
	}

	get #connected(): boolean {
		return this.#connection.state === 'connected' && this.#welcome !== null;
	}

	get #canRun(): boolean {
		return this.#connected && !this.#running;
	}

	get #limits(): Limits {
		return this.#welcome?.limits ?? { runSeconds: 60, maxMatrixElements: 0, maxPointsPerWindow: 0, maxOutputEntries: 0, maxSourceLength: Number.MAX_SAFE_INTEGER };
	}

	get #statusText(): string {
		const state: ConnectionState = this.#connection.state;
		switch (state) {
			case 'connecting': return 'Connecting…';
			case 'reconnecting': return 'Connection lost; reconnecting…';
			case 'failed': return 'Cannot reach the server; still trying…';
			default: return this.#running ? 'Running… (Esc or Stop to stop it)' : 'Ready';
		}
	}

	#onWelcome(message: WelcomeMessage): void {
		const reconnected = this.#welcome !== null;
		this.#welcome = message;
		this.#running = message.running;
		// The server comes from another build than this page: it was updated while the page was
		// open (or the browser kept old files). Offer a reload rather than reloading at once,
		// which would lose the console.
		this.#updateBar.hidden = message.version === buildVersion;
		this.#functions.replaceChildren(...message.builtinNames.map(name => element('code', {}, name)));
		if (reconnected && message.newSession)
			this.#console.addInfo('The server started a new session: earlier variables and plots are gone.');
		else if (reconnected && message.running)
			this.#console.addInfo('Reconnected; the program is still running. Output written while the connection was lost is not shown.');
		this.#update();
	}

	async #onKeyDown(event: KeyboardEvent): Promise<void> {
		const recall = (event.key === 'ArrowUp' || event.key === 'ArrowDown') && !this.#input.value.includes('\n');
		if ((event.key === 'Enter' && !event.shiftKey) || recall || event.key === 'Escape')
			event.preventDefault();
		if (event.key === 'Enter' && !event.shiftKey) {
			await this.#submit();
		} else if (recall) {
			const recalled = this.#console.recall(event.key === 'ArrowUp' ? -1 : 1);
			if (recalled !== null) {
				this.#input.value = recalled;
				this.#sizeInput();
			}
		} else if (event.key === 'Escape') {
			if (this.#running) {
				await this.#cancel();
			} else {
				this.#console.discardUnfinished();
				this.#input.value = '';
				this.#update();
			}
		}
	}

	async #submit(): Promise<void> {
		if (!this.#canRun)
			return;
		const line = this.#input.value.replace(/[\r\n]+$/, '');
		this.#input.value = '';
		const source = this.#console.submit(line);
		this.#update();
		if (source !== null)
			await this.#run(source);
	}

	async #openScript(): Promise<void> {
		const file = this.#openInput.files?.[0];
		this.#openInput.value = '';
		if (file === undefined || !this.#canRun)
			return;
		// A character takes at most 4 bytes in UTF-8.
		const maxBytes = Math.min(this.#limits.maxSourceLength * 4, 4 * 1024 * 1024);
		if (file.size > maxBytes) {
			this.#console.addInfo(`${file.name} is too large to run here.`);
			this.#update();
			return;
		}
		const text = (await file.text()).replaceAll('\r\n', '\n');
		const source = this.#console.openScript(file.name, text);
		this.#update();
		await this.#run(source);
	}

	async #run(source: string): Promise<void> {
		this.#running = true;
		this.#update();
		try {
			await this.#connection.run({ id: String(++this.#runCounter), source });
		} catch (error) {
			this.#running = false;
			this.#console.addInfo(`Could not reach the server: ${error instanceof Error ? error.message : String(error)}`);
			this.#update();
		}
	}

	async #cancel(): Promise<void> {
		if (this.#running)
			await this.#connection.cancel();
	}

	#setRunning(running: boolean): void {
		const wasRunning = this.#running;
		this.#running = running;
		// The box was read-only while the program ran; give it the focus back.
		if (wasRunning && !running)
			queueMicrotask(() => this.#input.focus());
	}

	/** Brings the page in line with the state: the console, the controls and the status bar. */
	#update(): void {
		this.#consoleView.sync();
		const canRun = this.#canRun;
		this.#openLabel.classList.toggle('disabled', !canRun);
		this.#openInput.disabled = !canRun;
		this.#stopButton.disabled = !this.#running;
		this.#prompt.textContent = this.#console.prompt;
		this.#input.disabled = !this.#connected;
		this.#input.readOnly = this.#running;
		this.#input.placeholder = this.#console.isContinuing ? '' : 'Type an expression, e.g. [1 2; 3 4] * [1; 1]';
		this.#sizeInput();
		this.#status.textContent = this.#mouseStatus.length > 0 ? this.#mouseStatus : this.#statusText;
	}

	/** One to eight rows, as many as the input has lines. */
	#sizeInput(): void {
		const lines = this.#input.value.split('\n').length;
		this.#input.rows = Math.min(Math.max(lines, 1), 8);
	}
}
