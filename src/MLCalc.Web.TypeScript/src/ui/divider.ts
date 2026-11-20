/**
 * The divider between the console and the plots (as setupDivider in the Blazor client's mlcalc.js,
 * with the same CSS variables and storage keys, so both clients share the split). Dragging it, or
 * the arrow keys when it has the focus, moves it; a double-click puts it back. The split is kept per
 * orientation in the browser, as the share of the first pane: the console in landscape, the plots
 * in portrait. The panes' sizes change through CSS variables, and the plot pane redraws by itself.
 */
export function setupDivider(divider: HTMLElement, panes: HTMLElement): void {
	const portrait = window.matchMedia('(orientation: portrait)');
	const key = () => portrait.matches ? 'mlcalc-split-portrait' : 'mlcalc-split-landscape';
	const defaults = { landscape: 1 / 2.4, portrait: 1.2 / 2.2 };
	/** Each pane keeps at least 15 %. */
	const limit = (share: number) => Math.min(0.85, Math.max(0.15, share));

	const apply = (share: number | null) => {
		const [first, second] = portrait.matches ? ['--plots-portrait', '--console-portrait'] : ['--console-landscape', '--plots-landscape'];
		if (share === null) {
			panes.style.removeProperty(first);
			panes.style.removeProperty(second);
		} else {
			panes.style.setProperty(first, `${(share * 100).toFixed(2)}fr`);
			panes.style.setProperty(second, `${((1 - share) * 100).toFixed(2)}fr`);
		}
	};
	// Browser storage can be unavailable (some private windows); the split is then not kept.
	const stored = (): number | null => {
		try {
			const share = Number.parseFloat(localStorage.getItem(key()) ?? '');
			return share > 0 && share < 1 ? share : null;
		} catch {
			return null;
		}
	};
	const store = (share: number | null) => {
		try {
			if (share === null)
				localStorage.removeItem(key());
			else
				localStorage.setItem(key(), share.toFixed(4));
		} catch {
			// See stored.
		}
	};
	/** The share of the first pane with the divider at (x, y). */
	const shareAt = (x: number, y: number) => {
		const box = panes.getBoundingClientRect();
		const style = getComputedStyle(panes);
		const pad = (name: string) => Number.parseFloat(style.getPropertyValue(name));
		if (portrait.matches) {
			const gap = divider.offsetHeight;
			return limit((y - box.top - pad('padding-top') - gap / 2) / (box.height - pad('padding-top') - pad('padding-bottom') - gap));
		}
		const gap = divider.offsetWidth;
		return limit((x - box.left - pad('padding-left') - gap / 2) / (box.width - pad('padding-left') - pad('padding-right') - gap));
	};
	const current = () => stored() ?? (portrait.matches ? defaults.portrait : defaults.landscape);
	const update = () => {
		apply(stored());
		divider.setAttribute('aria-orientation', portrait.matches ? 'horizontal' : 'vertical');
	};

	// While dragging: the share, and how far from the divider's middle it was taken hold of, so that
	// it does not jump to the pointer.
	let share: number | null = null;
	let grab = 0;
	divider.addEventListener('pointerdown', event => {
		if (event.button !== 0)
			return;
		event.preventDefault();
		divider.setPointerCapture(event.pointerId);
		divider.classList.add('dragging');
		share = current();
		grab = shareAt(event.clientX, event.clientY) - share;
	});
	divider.addEventListener('pointermove', event => {
		if (share === null || !divider.hasPointerCapture(event.pointerId))
			return;
		share = limit(shareAt(event.clientX, event.clientY) - grab);
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
	divider.addEventListener('keydown', event => {
		const steps: Record<string, number> = { ArrowLeft: -0.05, ArrowUp: -0.05, ArrowRight: 0.05, ArrowDown: 0.05 };
		const step = steps[event.key];
		if (step === undefined)
			return;
		event.preventDefault();
		const moved = limit(current() + step);
		apply(moved);
		store(moved);
	});
	portrait.addEventListener('change', update);
	update();
}
