import { readResponseAsJson, toSearch } from './utils.js';

/**
 * @typedef {Object} UsbStateResponse
 * @property {boolean} attached
 */

/**
 * @param {AbortSignal} [signal]
 * @returns {Promise<UsbStateResponse>}
 */
export async function getUsbState(signal) {
	const response = await fetch('/kvm/usb/state', {
		signal,
		headers: {
			'Accept': 'application/json',
		},
	});

	if (!response.ok) {
		throw new Error(response.status.toString() + ' ' + response.statusText);
	}

	return await readResponseAsJson(response);
}

/**
 * @param {number} [timeout]
 * @param {AbortSignal} [signal]
 * @returns {Promise<UsbStateResponse>}
 */
export async function attachUsb(timeout, signal) {
	const response = await fetch('/kvm/usb/attach' + toSearch({ timeout }), {
		signal,
		method: 'POST',
		headers: {
			'Accept': 'application/json',
		},
	});

	if (!response.ok) {
		throw new Error(response.status.toString() + ' ' + response.statusText);
	}

	return await readResponseAsJson(response);
}

/**
 * @param {number} [timeout]
 * @param {AbortSignal} [signal]
 * @returns {Promise<UsbStateResponse>}
 */
export async function detachUsb(timeout, signal) {
	const response = await fetch('/kvm/usb/detach' + toSearch({ timeout }), {
		signal,
		method: 'POST',
		headers: {
			'Accept': 'application/json',
		},
	});

	if (!response.ok) {
		throw new Error(response.status.toString() + ' ' + response.statusText);
	}

	return await readResponseAsJson(response);
}
