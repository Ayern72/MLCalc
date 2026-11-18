// Small helpers for the Blazor page; everything else is C#.
window.mlcalc = {
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
};
