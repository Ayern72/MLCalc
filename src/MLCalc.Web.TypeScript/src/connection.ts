import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import {
	clientMethods, hubPath, serverMethods, sessionParameter,
	type CameraUpdate, type DownloadMessage, type FigureMessage, type OutputMessage, type RunFinishedMessage,
	type RunRequest, type WelcomeMessage,
} from './protocol';

/** The state of the connection, for the status bar. */
export type ConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'failed';

/** What the page does with the server's messages. */
export interface ConnectionHandlers {
	welcome(message: WelcomeMessage): void;
	output(message: OutputMessage): void;
	figureChanged(message: FigureMessage): void;
	runFinished(message: RunFinishedMessage): void;
	download(message: DownloadMessage): void;
	stateChanged(state: ConnectionState): void;
}

const sessionKey = 'mlcalc-session';
const retryDelays = [0, 2000, 5000, 10000];

/**
 * The connection to the server. It keeps the session id in the browser tab's sessionStorage, so
 * that a reconnect, or reloading the page, continues the same session. When the connection drops
 * it connects again with that id, waiting a little longer each time. (As the Blazor client's
 * MLCalcConnection.cs; both clients of a tab share the stored id, so switching between them keeps
 * the workspace.)
 */
export class MLCalcConnection {
	readonly #handlers: ConnectionHandlers;
	#connection: HubConnection | null = null;
	#sessionId: string | null = readSession();
	#stopped = false;
	#state: ConnectionState = 'connecting';

	constructor(handlers: ConnectionHandlers) {
		this.#handlers = handlers;
	}

	get state(): ConnectionState {
		return this.#state;
	}

	start(): Promise<void> {
		return this.#connect();
	}

	run(request: RunRequest): Promise<void> {
		return this.#connection?.invoke(serverMethods.run, request) ?? Promise.resolve();
	}

	cancel(): Promise<void> {
		return this.#connection?.invoke(serverMethods.cancel) ?? Promise.resolve();
	}

	setCamera(update: CameraUpdate): Promise<void> {
		return this.#connection?.invoke(serverMethods.setCamera, update) ?? Promise.resolve();
	}

	async stop(): Promise<void> {
		this.#stopped = true;
		await this.#connection?.stop();
	}

	async #connect(): Promise<void> {
		for (let attempt = 0; !this.#stopped; attempt++) {
			await delay(retryDelays[Math.min(attempt, retryDelays.length - 1)] ?? 0);
			const next = this.#build();
			try {
				await next.start();
				this.#connection = next;
				this.#setState('connected');
				return;
			} catch {
				this.#setState(attempt < 3 ? 'reconnecting' : 'failed');
			}
		}
	}

	#build(): HubConnection {
		const query = this.#sessionId === null ? '' : `?${sessionParameter}=${encodeURIComponent(this.#sessionId)}`;
		const hub = new HubConnectionBuilder()
			.withUrl(hubPath + query)
			.configureLogging(LogLevel.Warning)
			.build();
		hub.on(clientMethods.welcome, (message: WelcomeMessage) => {
			this.#sessionId = message.sessionId;
			writeSession(message.sessionId);
			this.#handlers.welcome(message);
		});
		hub.on(clientMethods.output, (message: OutputMessage) => this.#handlers.output(message));
		hub.on(clientMethods.figureChanged, (message: FigureMessage) => this.#handlers.figureChanged(message));
		hub.on(clientMethods.runFinished, (message: RunFinishedMessage) => this.#handlers.runFinished(message));
		hub.on(clientMethods.download, (message: DownloadMessage) => this.#handlers.download(message));
		hub.onclose(() => {
			if (this.#stopped || this.#connection !== hub)
				return;
			this.#setState('reconnecting');
			void this.#connect();
		});
		return hub;
	}

	#setState(state: ConnectionState): void {
		this.#state = state;
		this.#handlers.stateChanged(state);
	}
}

function delay(milliseconds: number): Promise<void> {
	return new Promise(resolve => setTimeout(resolve, milliseconds));
}

/** sessionStorage can be unavailable (some private windows); then each load gets a new session. */
function readSession(): string | null {
	try {
		return sessionStorage.getItem(sessionKey);
	} catch {
		return null;
	}
}

function writeSession(id: string): void {
	try {
		sessionStorage.setItem(sessionKey, id);
	} catch {
		// See readSession.
	}
}
