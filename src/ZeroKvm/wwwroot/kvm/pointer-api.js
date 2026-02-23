
/**
 * @typedef {'BootMouse' | 'AbsoluteMouse'} KvmPointerType
 *
 * @typedef {Object} KvmPointerEventRequest
 * @property {KvmPointerType} type
 * @property {KvmPointerEvent[]} events
 * @property {boolean} [reset]
 *
 * @typedef {Object} KvmPointerEvent
 * @property {bool} [left]
 * @property {bool} [middle]
 * @property {bool} [right]
 * @property {number} [x]
 * @property {number} [y]
 * @property {number} [wheel]
 * @property {number} [delay]
 */

import { delay } from './utils.js';

/** @type {Record<KvmPointerType, KvmPointerEvent[]>} */
const pendingPointerEvents = {
	BootMouse: [],
	AbsoluteMouse: [],
};
let isSendindEvents = false;

/**
 * @param {KvmPointerType} type
 * @param {KvmPointerEvent[]} events
 * @returns {Promise<void>}
 */
export async function sendPointerEvents(type, events) {
	const pendingEvents = pendingPointerEvents[type];
	if (!pendingEvents) {
		throw new TypeError('Invalid pointer type');
	}

	pendingEvents.push(...events);

	while (isSendindEvents) {
		await delay(1);
	}

	if (pendingEvents.length === 0) {
		return;
	}

	const requestEvents = pendingEvents.splice(0);
	isSendindEvents = true;
	try {
		if (type === 'BootMouse') {
			mergeRelativeMoves(requestEvents);
		}

		await post({
			type,
			events: requestEvents,
		});
	} finally {
		isSendindEvents = false;
	}

	/**
	 * @param {KvmPointerEvent[]} events
	 */
	function mergeRelativeMoves(events) {
		for (let i = 1; i < events.length; i++) {
			if (isMoveEvent(events[i]) && isMoveEvent(events[i - 1])) {
				const x = events[i - 1].x + events[i].x;
				const y = events[i - 1].y + events[i].y;
				if (Math.abs(x) <= 127 && Math.abs(y) <= 127) {
					events[i - 1].x = x;
					events[i - 1].y = y;
					events.splice(i, 1);
					i--;
				}
			}
		}
	}

	/**
	 * @param {KvmPointerEvent} event
	 */
	function isMoveEvent(event) {
		return typeof event.x === 'number' && typeof event.y === 'number' && Object.keys(event).length === 2;
	}
}

/**
 * @param {KvmPointerType} type
 * @returns {Promise<void>}
 */
export async function sendPointerReset(type) {
	const pendingEvents = pendingPointerEvents[type];
	if (!pendingEvents) {
		throw new TypeError('Invalid pointer type');
	}

	while (pendingEvents.length > 0) {
		await delay(10);
	}

	await post({
		type,
		events: [
			{
				left: false,
			},
		],
		reset: true,
	});
}

/**
 * @param {KvmPointerEventRequest} request
 * @returns {Promise<void>}
 */
export async function post(request) {
	const response = await fetch('/kvm/pointer', {
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

/**
 * @param {number} delta
 * @param {number} deltaMode
 */
export function getWheelAmount(delta, deltaMode) {
	switch (deltaMode) {
		case 0: // DOM_DELTA_PIXEL
		case 1: // DOM_DELTA_LINE
			return delta < 0 ? 1 : delta > 0 ? -1 : 0;
		default:
			throw new TypeError();
	}
}
