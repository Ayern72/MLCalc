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

	// Keeps the console at its end when it gets smaller (a phone's keyboard opening, the divider), as
	// long as it was at the end: otherwise the latest output would slide out of view.
	keepAtEnd(element) {
		const atEnd = () => element.scrollTop + element.clientHeight >= element.scrollHeight - 4;
		let wasAtEnd = true;
		element.addEventListener('scroll', () => wasAtEnd = atEnd(), { passive: true });
		new ResizeObserver(() => {
			if (wasAtEnd)
				element.scrollTop = element.scrollHeight;
		}).observe(element);
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

	// The divider between the console and the plots. Dragging it (or the arrow keys, when it has
	// the focus) moves it; a double-click puts it back. The split is kept per orientation in the
	// browser, as the share of the first pane (the console in landscape, the plots in portrait).
	// The panes' sizes change through CSS variables, so the plots redraw through observeSize.
	setupDivider(divider, panes) {
		const portrait = window.matchMedia('(orientation: portrait)');
		const key = () => portrait.matches ? 'mlcalc-split-portrait' : 'mlcalc-split-landscape';
		const defaults = { landscape: 1 / 2.4, portrait: 1.2 / 2.2 };
		const apply = share => {
			const [first, second] = portrait.matches ? ['--plots-portrait', '--console-portrait'] : ['--console-landscape', '--plots-landscape'];
			if (share === null) {
				panes.style.removeProperty(first);
				panes.style.removeProperty(second);
			} else {
				panes.style.setProperty(first, `${(share * 100).toFixed(2)}fr`);
				panes.style.setProperty(second, `${((1 - share) * 100).toFixed(2)}fr`);
			}
		};
		const stored = () => {
			try {
				const share = parseFloat(localStorage.getItem(key()));
				return share > 0 && share < 1 ? share : null;
			} catch {
				return null;
			}
		};
		const store = share => {
			try {
				if (share === null)
					localStorage.removeItem(key());
				else
					localStorage.setItem(key(), share.toFixed(4));
			} catch {
			}
		};
		// The share of the first pane with the divider at (x, y); each pane keeps at least 15 %.
		const shareAt = (x, y) => {
			const box = panes.getBoundingClientRect();
			const style = getComputedStyle(panes);
			const gap = portrait.matches ? divider.offsetHeight : divider.offsetWidth;
			const share = portrait.matches
				? (y - box.top - parseFloat(style.paddingTop) - gap / 2) / (box.height - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom) - gap)
				: (x - box.left - parseFloat(style.paddingLeft) - gap / 2) / (box.width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight) - gap);
			return Math.min(0.85, Math.max(0.15, share));
		};
		const current = () => stored() ?? (portrait.matches ? defaults.portrait : defaults.landscape);
		const update = () => {
			apply(stored());
			divider.setAttribute('aria-orientation', portrait.matches ? 'horizontal' : 'vertical');
		};

		// While dragging: the share, and how far from the divider's middle it was taken hold of,
		// so that it does not jump to the pointer.
		let share = null;
		let grab = 0;
		divider.addEventListener('pointerdown', e => {
			if (e.button !== 0)
				return;
			e.preventDefault();
			divider.setPointerCapture(e.pointerId);
			divider.classList.add('dragging');
			share = current();
			grab = shareAt(e.clientX, e.clientY) - share;
		});
		divider.addEventListener('pointermove', e => {
			if (share === null || !divider.hasPointerCapture(e.pointerId))
				return;
			share = Math.min(0.85, Math.max(0.15, shareAt(e.clientX, e.clientY) - grab));
			apply(share);
		});
		const end = () => {
			if (share === null)
				return;
			store(share);
			share = null;
			divider.classList.remove('dragging');
		};
		divider.addEventListener('pointerup', end);
		divider.addEventListener('pointercancel', end);
		divider.addEventListener('dblclick', () => {
			store(null);
			apply(null);
		});
		divider.addEventListener('keydown', e => {
			const step = { ArrowLeft: -0.05, ArrowUp: -0.05, ArrowRight: 0.05, ArrowDown: 0.05 }[e.key];
			if (step === undefined)
				return;
			e.preventDefault();
			const moved = Math.min(0.85, Math.max(0.15, current() + step));
			apply(moved);
			store(moved);
		});
		portrait.addEventListener('change', update);
		update();
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
