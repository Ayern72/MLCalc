// Small helpers for the Blazor page; everything else is C#.
window.mlcalc = {
	// Saves a file from the server (savefig) the way the browser saves downloads.
	download(fileName, mediaType, content) {
		const url = URL.createObjectURL(new Blob([content], { type: mediaType }));
		const link = document.createElement('a');
		link.href = url;
		link.download = fileName;
		document.body.appendChild(link);
		link.click();
		link.remove();
		setTimeout(() => URL.revokeObjectURL(url), 10000);
	},

	// Keys the page handles itself, so the browser must not also act on them: Enter runs (Shift+Enter
	// starts a new line), Up/Down recall earlier input when the box has one line, Esc stops.
	setupInput(element) {
		element.addEventListener('keydown', e => {
			const recall = (e.key === 'ArrowUp' || e.key === 'ArrowDown') && !element.value.includes('\n');
			if ((e.key === 'Enter' && !e.shiftKey) || recall || e.key === 'Escape')
				e.preventDefault();
		});
	},

	focus(element) {
		element.focus();
		element.setSelectionRange(element.value.length, element.value.length);
	},

	scrollToEnd(element) {
		element.scrollTop = element.scrollHeight;
	},

	// While a 3D window is turned, its pointer moves keep coming when the pointer leaves the window.
	// A pointer the browser does not know (e.g. one already lifted) is ignored.
	capturePointer(element, pointerId) {
		try {
			element.setPointerCapture(pointerId);
		} catch {
		}
	},

	releasePointer(element, pointerId) {
		try {
			if (element.hasPointerCapture(pointerId))
				element.releasePointerCapture(pointerId);
		} catch {
		}
	},

	// Tells .NET the element's size when it changes, at most once per animation frame.
	observeSize(element, dotnet) {
		let pending = false;
		const report = () => {
			pending = false;
			dotnet.invokeMethodAsync('SetSize', element.clientWidth, element.clientHeight);
		};
		element._mlcalcObserver = new ResizeObserver(() => {
			if (!pending) {
				pending = true;
				requestAnimationFrame(report);
			}
		});
		element._mlcalcObserver.observe(element);
	},

	unobserveSize(element) {
		element?._mlcalcObserver?.disconnect();
	},
};
