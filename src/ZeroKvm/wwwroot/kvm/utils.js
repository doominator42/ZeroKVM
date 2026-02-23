/**
 * @template T
 * @param {string} id
 * @param {{ new() => T; prototype: T }} type
 * @returns {T}
 */
export function getElementById(id, type) {
	const element = document.getElementById(id);
	if (!(element instanceof type)) {
		throw new Error('Element ' + id + ' not found or wrong type');
	}

	return element;
}

/**
 * @template T
 * @param {string} selector
 * @param {{ new() => T; prototype: T }} type
 * @returns {T[]}
 */
export function queryElements(selector, type) {
	const elements = [...document.querySelectorAll(selector)];
	if (elements.length === 0 || !elements.every(e => e instanceof type)) {
		throw new Error('Elements ' + selector + ' not found or wrong type');
	}

	return elements;
}

export const debug = new URLSearchParams(location.search).get('debug') ?
	(() => {
		/**
		 * @param {(log: (...data: any[]) => void)} callback
		 */
		function debug(callback) {
			callback(log);
		}

		function log(...data) {
			console.log(...data);
		}

		debug.log = log;
		return debug;
	})() : (() => {
		function debug() { }
		debug.log = () => { }
		return debug;
	})();

/**
 * @param {HTMLElement} element
 * @param {(keyof HTMLElementEventMap)[]} eventNames
 */
export function disableEvents(element, eventNames) {
	for (const name of eventNames) {
		element.addEventListener(name, (event) => {
			try {
				event.preventDefault();
				event.stopImmediatePropagation();
			} catch (e) { }
		}, {
			capture: true,
			passive: false,
		});
	}
}

/**
 * @param {HTMLInputElement} parent
 * @param {HTMLInputElement[]} children
 */
export function linkCheckboxes(parent, children) {
	parent.addEventListener('change', () => {
		for (const child of children) {
			child.checked = parent.checked;
		}
	});

	for (const child of children) {
		linkChild(child);
	}

	updateParent();

	/**
	 * @param {HTMLInputElement} child
	 */
	function linkChild(child) {
		child.addEventListener('change', () => {
			updateParent();
		});
	}

	function updateParent() {
		const anyChecked = children.some(c => c.checked);
		const anyUnchecked = children.some(c => !c.checked);
		parent.checked = anyChecked;
		parent.indeterminate = anyChecked && anyUnchecked;
	}
}

/**
 * @param {string} name
 * @param {HTMLInputElement} input
 * @param {string} [unit]
 * @param {HTMLElement} [varElement]
 */
export function linkCssVariable(name, input, unit, varElement) {
	if (!name.startsWith('-')) {
		name = '--' + name;
	}

	unit ??= '';
	varElement ??= document.body;
	input.addEventListener('input', () => {
		updateVar();
	});

	updateVar();

	function updateVar() {
		varElement.style.setProperty(name, input.value + unit);
	}
}

/**
 * @param {string} name
 * @param {HTMLInputElement} input
 * @param {HTMLElement} target
 * @param {boolean} [invserse=false]
 */
export function linkCssClass(name, input, target, invserse) {
	input.addEventListener('change', () => {
		update();
	});

	update();

	function update() {
		target.classList.toggle(name, invserse ? !input.checked : input.checked);
	}
}

/**
 * @param {HTMLInputElement} checkbox
 * @param {HTMLButtonElement | HTMLFieldSetElement | HTMLOptGroupElement | HTMLOptionElement | HTMLSelectElement | HTMLTextAreaElement | HTMLInputElement} target
 * @param {boolean} [inverse]
 */
export function linkCheckboxToDisabled(checkbox, target, inverse) {
	checkbox.addEventListener('change', () => {
		update();
	});

	update();

	function update() {
		target.disabled = inverse ? !checkbox.checked : checkbox.checked;
	}
}

/**
 * @param {(HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement)[]} inputs
 * @param {HTMLButtonElement | HTMLFieldSetElement | HTMLOptGroupElement | HTMLOptionElement | HTMLSelectElement | HTMLTextAreaElement | HTMLInputElement} target
 * @param {(...values: string[]) => boolean} [predicate]
 */
export function linkInputValueToDisabled(inputs, target, predicate) {
	for (const input of inputs) {
		input.addEventListener('input', () => {
			update();
		});
	}

	update();

	function update() {
		target.disabled = predicate ? predicate(...inputs.map(i => i.value)) : inputs.some(i => !i.value);
	}
}

/**
 * @param {HTMLSelectElement} select
 * @param {[value: string, label: string][]} options
 */
export function addSelectOptions(select, options) {
	for (const option of options) {
		const optionElement = document.createElement('option');
		optionElement.value = option[0];
		optionElement.textContent = option[1];
		select.appendChild(optionElement);
	}
}

// TODO: add fallback support for ReadableStreamDefaultReader, Safari does not support ReadableStreamBYOBReader
/**
 *
 * @param {ReadableStreamBYOBReader} reader
 * @param {ArrayBuffer} buffer
 * @param {number} count
 * @returns {Promise<ReadableStreamReadResult<Uint8Array>>}
 */
export async function readExactly(reader, buffer, count) {
	let readCount = 0;
	while (readCount < count) {
		const { done, value } = await reader.read(new Uint8Array(buffer, readCount, count - readCount), { min: count - readCount });
		if (!value) {
			return {
				done,
				value,
			};
		}

		readCount += value.length;
		buffer = value.buffer;

		if (done) {
			return {
				done: true,
				value: readCount === 0 ? undefined : new Uint8Array(buffer, 0, readCount),
			};
		}
	}

	return {
		done: false,
		value: new Uint8Array(buffer, 0, readCount),
	};
}

/**
 * @param {number} timeoutMs
 * @returns {Promise<void>}
 */
export function delay(timeoutMs) {
	return new Promise((resolve) => {
		setTimeout(() => {
			resolve();
		}, timeoutMs);
	});
}

/**
 * @param {Response} response
 * @returns {Promise<any>}
 */
export async function readResponseAsJson(response) {
	if (!/^application\/json(?:$|;)/i.test(response.headers.get('Content-Type') ?? '')) {
		throw new Error('Invalid response');
	}

	return await response.json();
}

/**
 * @param {Record<string, string | number | boolean | Date | null | undefined>} params
 * @returns {string}
 */
export function toSearch(params) {
	/** @type {[key: string, value: string][]} */
	const entries = [];
	for (const key in params) {
		const value = stringifyValue(params[key]);
		if (value !== undefined) {
			entries.push([key, value]);
		}
	}

	const search = new URLSearchParams(entries).toString();
	return search ? '?' + search : '';

	/**
	 * @param {string | number | boolean | Date | null | undefined} value
	 * @returns {string | undefined}
	 */
	function stringifyValue(value) {
		switch (typeof value) {
			case 'string':
				return value;
			case 'number':
				return value.toString();
			case 'boolean':
				return value ? 'true' : 'false';
			case 'object':
				return value instanceof Date ? value.toISOString() : undefined;
			default:
				return undefined;
		}
	}
}

/**
 * @param {HTMLButtonElement} button
 * @param {(event: MouseEvent) => Promise<void>} callback
 */
export async function addAsyncClickListener(button, callback) {
	const RUNNING_CLASS = 'async-running';
	let running = false;
	button.addEventListener('click', async (event) => {
		if (running) {
			event.preventDefault();
			return;
		}

		button.classList.add(RUNNING_CLASS);
		running = true;
		try {
			return await callback(event);
		} finally {
			running = false;
			button.classList.remove(RUNNING_CLASS);
		}
	});
}

/**
 * @param {HTMLElement} element
 * @param {(event: TouchEvent) => boolean} predicate
 * @param {() => void} callback
 */
export function addTouchClickListener(element, predicate, callback) {
	/** @type {{ x: number, y: number }[] | undefined} */
	let startTouches = undefined;
	element.addEventListener('touchstart', (event) => {
		if (startTouches) {
			end();
		}

		if (predicate(event)) {
			event.preventDefault();
			startTouches = [...event.touches].map(t => ({ x: t.clientX, y: t.clientY }));
			window.addEventListener('touchmove', onMove, {
				capture: true,
				passive: true,
			});
		}
	});

	window.addEventListener('touchend', (event) => {
		if (startTouches) {
			event.preventDefault();
			if (event.touches.length < startTouches.length) {
				end();
				if (element.contains(event.target)) {
					callback();
				}
			}
		}
	}, {
		capture: true,
		passive: false,
	});

	window.addEventListener('touchcancel', () => {
		end();
	}, {
		capture: true,
		passive: true,
	});

	/**
	 * @param {TouchEvent} event
	 */
	function onMove(event) {
		const distance = totalDistance(event);
		if (isNaN(distance) || distance > 20) {
			end();
		}
	}

	function end() {
		startTouches = undefined;
		window.removeEventListener('touchmove', onMove);
	}

	/**
	 * @param {TouchEvent} event
	 * @returns {number}
	 */
	function totalDistance(event) {
		if (!startTouches || startTouches.length !== event.touches.length) {
			return NaN;
		}

		let distance = 0;
		for (let i = 0; i < startTouches.length; i++) {
			const dx = event.touches[i].clientX - startTouches[i].x;
			const dy = event.touches[i].clientY - startTouches[i].y;
			distance += Math.sqrt((dx * dx) + (dy * dy));
		}

		return distance;
	}
}

/**
 * @typedef {Object} Drop
 * @property {() => void} position
 */

/**
 * @param {HTMLElement} drop
 * @param {HTMLElement} [toggle]
 * @returns {Drop}
 */
export function initDrop(drop, toggle) {
	toggle ??= drop.previousElementSibling;
	toggle.addEventListener('click', () => {
		position();
		drop.classList.add('open');
	});

	for (let toggle = drop.previousElementSibling; !!toggle; toggle = toggle.previousElementSibling) {
		if (toggle.classList.contains('drop-hover')) {
			toggle.addEventListener('pointerenter', () => {
				position();
			}, {
				capture: true,
				passive: true,
			});
		}
	}

	window.addEventListener('click', onWindowClick, {
		capture: true,
		passive: true,
	});

	window.addEventListener('touchstart', onWindowClick, {
		capture: true,
		passive: true,
	});

	return {
		position,
	};

	function position() {
		const toggleRect = toggle.getBoundingClientRect();
		drop.style.left = Math.min(Math.max(toggleRect.left + toggle.offsetWidth / 2 - drop.offsetWidth / 2, 8), document.body.clientWidth - drop.offsetWidth - 8) + 'px';
	}

	/**
	 * @param {Event} event
	 */
	function onWindowClick(event) {
		if (!drop.contains(event.target) && !toggle.contains(event.target)) {
			drop.classList.remove('open');
		}
	}
}

export function hasTouchscreen() {
	return navigator.maxTouchPoints > 0;
}

/** @type {Map<HTMLElement, { timeout: number; handler; left: number; top: number }>} */
const scrollLockedElements = new Map();

/**
 * @param {HTMLElement} element
 * @param {number} [delay]
 */
export function lockScrollTemporarily(element, delay) {
	let state = scrollLockedElements.get(element);
	if (state) {
		clearTimeout(state.timeout);
	} else {
		state = {
			handler: lockScroll,
			left: element.scrollLeft,
			top: element.scrollTop,
		};
		scrollLockedElements.set(element, state);
		element.addEventListener('scroll', lockScroll);
	}

	state.timeout = setTimeout(() => {
		const state = scrollLockedElements.get(element);
		if (state) {
			element.removeEventListener('scroll', state.handler);
			scrollLockedElements.delete(element);
		}
	}, delay ?? 1);

	function lockScroll() {
		if (element.scrollLeft !== state.left || element.scrollTop !== state.top) {
			element.scrollLeft = state.left;
			element.scrollTop = state.top;
		}
	}
}

/**
 * @param {number} value
 * @param {number} min
 * @param {number} max
 */
export function clamp(value, min, max) {
	return value < min ? min : value > max ? max : value;
}
