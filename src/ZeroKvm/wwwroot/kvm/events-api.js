import { readResponseAsJson, toSearch } from './utils.js';

/**
 * @typedef {import('./usb-api.js').UsbStateResponse & { $type: 'usb/state' }} UsbStateEvent
 * @typedef {import('./keyboard-api.js').KeyboardLedsResponse & { $type: 'keyboard/leds' }} KeyboardLedsEvent
 *
 * @typedef {UsbStateEvent | KeyboardLedsEvent} AnyEvent
 *
 * @typedef {Object} QueueCreated
 * @property {string} queueId
 */

/**
 * @param {(event: AnyEvent) => void | Promise<void>} callback
 * @param {{ queueId?: string }} state
 * @param {AbortSignal} [signal]
 * @returns {Promise<void>}
 */
export async function fetchEvents(callback, state, signal) {
	while (!signal?.aborted) {
		const response = await fetch('/kvm/events' + toSearch({ queueId: state.queueId }), {
			signal,
			headers: {
				'Accept': 'application/json',
			},
		});

		if (!response.ok) {
			throw new Error(response.status.toString() + ' ' + response.statusText);
		}

		if (response.status === 204) {
			continue;
		}

		/** @type {AnyEvent[] | QueueCreated} */
		const result = await readResponseAsJson(response);
		if (Array.isArray(result)) {
			for (const event of result) {
				try {
					await callback(event);
				} catch (e) {
					console.error(e);
				}
			}
		} else {
			if (!result.queueId) {
				throw new Error('Received invalid queue ID');
			}

			state.queueId = result.queueId;
		}
	}
}
