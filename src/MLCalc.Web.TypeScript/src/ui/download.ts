import type { DownloadMessage } from '../protocol';

/** Saves a file from the server (savefig) the way the browser saves downloads. */
export function download(message: DownloadMessage): void {
	const url = URL.createObjectURL(new Blob([message.content], { type: message.mediaType }));
	const link = document.createElement('a');
	link.href = url;
	link.download = message.fileName;
	document.body.append(link);
	link.click();
	link.remove();
	setTimeout(() => URL.revokeObjectURL(url), 10000);
}
