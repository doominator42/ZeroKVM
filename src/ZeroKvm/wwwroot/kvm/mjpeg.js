import { readExactly } from './utils.js';

/**
 * @typedef {Object} FetchMjpegOptions
 * @property {AbortSignal} [signal]
 * @property {(width: number, height: number) => void} [onResize]
 * @property {() => Promise<void>} [onReady]
 */

/**
 * @param {string} url
 * @param {HTMLCanvasElement} canvas
 * @param {FetchMjpegOptions} [options]
 * @returns {Promise<void>}
 */
export async function fetchMjpegRects(url, canvas, options) {
	const CONTENT_TYPE = 'image/mjpeg+rects';

	const { signal, onReady, onResize } = options ?? {};
	const context2d = canvas.getContext('2d');
	if (!context2d) {
		throw new Error('Invalid canvas');
	}

	context2d.imageSmoothingEnabled = false;

	const response = await fetch(url, {
		headers: {
			'Accept': CONTENT_TYPE,
		},
		signal,
	});

	if (!response.ok) {
		throw new Error(response.status.toString() + ' ' + response.statusText);
	} else if (response.headers.get('Content-Type') !== CONTENT_TYPE) {
		throw new Error('Received invalid content type from server');
	}

	const reader = response.body.getReader({ mode: 'byob' });
	const signalCallback = () => {
		reader.releaseLock();
		response.body.cancel().catch(() => {});
	}

	await onReady();

	try {
		signal?.addEventListener('abort', signalCallback);
		await renderMjpegRects(reader, context2d, (width, height) => {
			if (canvas.width !== width || canvas.height !== height) {
				canvas.width = width;
				canvas.height = height;
				if (onResize) {
					onResize(width, height);
				}
			}
		});
	} finally {
		signal?.removeEventListener('abort', signalCallback);
		signalCallback();
	}
}

/**
 *
 * @param {ReadableStreamBYOBReader} reader
 * @param {CanvasRenderingContext2D} context2d
 * @param {((width: number, height: number) => void) | undefined} onResolutionChange
 * @returns {Promise<void>}
 */
async function renderMjpegRects(reader, context2d, onResolutionChange = undefined) {
	const HEADER_SIZE = 12;
	let buffer = new ArrayBuffer(8 * 1024 * 1024);
	let currentWidth = 0;
	let currentHeight = 0;

	while (true) {
		const headerBuffer = await readExactly(reader, buffer, HEADER_SIZE);
		if (headerBuffer.done || !headerBuffer.value || headerBuffer.value.length !== HEADER_SIZE) {
			return;
		}

		buffer = headerBuffer.value.buffer;

		const headerView = new DataView(buffer, 0, HEADER_SIZE);
		const screenWidth = headerView.getUint16(0, true);
		const screenHeight = headerView.getUint16(2, true);
		const rectX = headerView.getUint16(4, true);
		const rectY = headerView.getUint16(6, true);
		const length = headerView.getUint32(8, true);
		if (length > buffer.byteLength) {
			throw new Error('Image too big');
		}

		if (screenWidth !== currentWidth || screenHeight !== currentHeight) {
			currentWidth = screenWidth;
			currentHeight = screenHeight;
			if (onResolutionChange) {
				onResolutionChange(screenWidth, screenHeight);
			}
		}

		const imageBuffer = await readExactly(reader, buffer, length);
		if (imageBuffer.done || !imageBuffer.value || imageBuffer.value.length !== length) {
			return;
		}

		buffer = imageBuffer.value.buffer;

		const bitmap = await createImageBitmap(new Blob([imageBuffer.value], { type: 'image/jpeg' }));
		try {
			context2d.drawImage(bitmap, rectX, rectY);
		} finally {
			bitmap.close();
		}
	}
}
