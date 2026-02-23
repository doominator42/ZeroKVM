import { delay, readResponseAsJson } from './utils.js';

/**
 * @typedef {Object} KvmKeyboardEventRequest
 * @property {KvmKeyEvent[]} keys
 * @property {boolean} [reset]
 *
 * @typedef {Object} KvmKeyEvent
 * @property {number} scanCode
 * @property {boolean} isDown
 * @property {number} [delay]
 *
 * @typedef {Object} KeyboardLedsResponse
 * @property {boolean} numLock
 * @property {boolean} capsLock
 * @property {boolean} scrollLock
 * @property {boolean} compose
 * @property {boolean} kana
 */

/** @type {KvmKeyEvent[]} */
const pendingEvents = [];

/**
 * @param {KvmKeyEvent[]} events
 * @returns {Promise<void>}
 */
export async function sendKeyboardEvents(events) {
	const hasPending = pendingEvents.length > 0;
	pendingEvents.push(...events);

	if (hasPending) {
		return;
	}

	do {
		/** @type {KvmKeyboardEventRequest} */
		const request = {
			keys: pendingEvents.slice(),
		};

		try {
			await post(request);
		} finally {
			pendingEvents.splice(0, request.keys.length);
		}
	} while (pendingEvents.length > 0);
}

/**
 * @returns {Promise<void>}
 */
export async function sendKeyboardReset() {
	while (pendingEvents.length > 0) {
		await delay(10);
	}

	await post({
		keys: [
			{
				scanCode: 1,
				isDown: false,
			},
		],
		reset: true,
	});
}

/**
 * @returns {Promise<KeyboardLedsResponse>}
 */
export async function getKeyboardLeds() {
	const response = await fetch('/kvm/keyboard/leds');
	if (!response.ok) {
		throw new Error(response.status.toString() + ' ' + response.statusText);
	}

	const result = await readResponseAsJson(response);
	if (!result || typeof result !== 'object') {
		throw new Error('Invalid response');
	}

	return result;
}

/**
 * @param {KvmKeyboardEventRequest} request
 * @returns {Promise<void>}
 */
async function post(request) {
	const response = await fetch('/kvm/keyboard', {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
		},
		body: JSON.stringify(request),
	});

	if (!response.ok) {
		throw new Error(response.status.toString() + ' ' + response.statusText);
	}
}
