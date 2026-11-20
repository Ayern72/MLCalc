/** Makes an element with a class, attributes and children; a small stand-in for a template. */
export function element<K extends keyof HTMLElementTagNameMap>(
	tag: K,
	attributes: Record<string, string> = {},
	...children: (Node | string)[]
): HTMLElementTagNameMap[K] {
	const result = document.createElement(tag);
	for (const [name, value] of Object.entries(attributes))
		result.setAttribute(name, value);
	result.append(...children);
	return result;
}
