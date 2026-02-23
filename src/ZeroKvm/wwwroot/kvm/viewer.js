import { addAsyncClickListener, addSelectOptions, addTouchClickListener, debug, delay, disableEvents, lockScrollTemporarily, getElementById, initDrop, linkCheckboxes, linkCssClass, linkInputValueToDisabled, hasTouchscreen, clamp } from './utils.js';
import { fetchMjpegRects } from './mjpeg.js';
import { getWheelAmount, sendPointerEvents, sendPointerReset } from './pointer-api.js';
import { createKeyboardSequenceEvents, getHidScanCodeFromKeyboardEvent, getLayout, getHidScanCodesFromText, jsKeyToHidScanCode, KEYBOARD_LAYOUTS, loadLayout, isNonCharInput } from './keyboard-keys.js';
import { getKeyboardLeds, sendKeyboardEvents, sendKeyboardReset } from './keyboard-api.js';
import { linkCheckboxGroupSetting, linkCheckboxSetting, linkInputSetting, linkSelectSetting, linkVisibilityToggleButtonSetting } from './settings.js';
import { init as initOverlay, onScreenSizeChanged } from './overlay.js';
import { fetchEvents } from './events-api.js';
import { attachUsb, detachUsb, getUsbState } from './usb-api.js';

const screenContainer = getElementById('screenContainer', HTMLDivElement);
const screenCanvas = getElementById('screenCanvas', HTMLCanvasElement);
const pointerTypeSelect = getElementById('pointerTypeSelect', HTMLSelectElement);
const pointerEnabledCheckbox = getElementById('pointerEnabledCheckbox', HTMLInputElement);
const pointerMoveLateCheckbox = getElementById('pointerMoveLateCheckbox', HTMLInputElement);
const showLocalPointerCheckbox = getElementById('showLocalPointerCheckbox', HTMLInputElement);
const keyboardEnabledCheckbox = getElementById('keyboardEnabledCheckbox', HTMLInputElement);
const keyboardLayoutSelect = getElementById('keyboardLayoutSelect', HTMLSelectElement);
const sendTextInput = getElementById('sendTextInput', HTMLTextAreaElement);
const sendTextButton = getElementById('sendTextButton', HTMLButtonElement);
const fitScreenCheckbox = getElementById('fitScreenCheckbox', HTMLInputElement);
const toggleFullscreenButton = getElementById('toggleFullscreenButton', HTMLButtonElement);
const mjpegQualityInput = getElementById('mjpegQualityInput', HTMLInputElement);
const mjpegSubsamplingSelect = getElementById('mjpegSubsamplingSelect', HTMLSelectElement);
const capsLockButton = getElementById('capsLockButton', HTMLButtonElement);
const numLockButton = getElementById('numLockButton', HTMLButtonElement);
const showToolbarButton = getElementById('showToolbarButton', HTMLButtonElement);
const toolbarPanel = getElementById('toolbarPanel', HTMLDivElement);
const attachUsbButton = getElementById('attachUsbButton', HTMLButtonElement);
const detachUsbButton = getElementById('detachUsbButton', HTMLButtonElement);

const mjpegVideo = {
	abortController: new AbortController(),
	quality: 90,
	subsampling: '444',
};

/**
 * @returns {boolean}
 */
function screenHasFocus() {
	return document.activeElement === screenCanvas && document.hasFocus();
}

const virtualKeyButtons = [...document.querySelectorAll('button[data-vkey]')].map(button => ({
	/** @type {HTMLButtonElement} */
	button,
	vkey: button.getAttribute('data-vkey') ?? '',
}));

/** @type {Set<number>} */
const keyboardDownKeys = new Set();

/**
 * @param {KeyboardEvent} event
 * @param {boolean} isDown
 * @returns {Promise<void>}
 */
async function onKeyboardKey(event, isDown) {
	if (!(keyboardEnabledCheckbox.checked && usbAttached)) {
		return;
	} else if ((screenCanvas.contentEditable === 'true' && !isNonCharInput(event))) {
		if (!isDown) {
			event.preventDefault();
		}

		debug(log => {
			log(isDown ? 'charinput:keydown' : 'charinput:keyup', {
				code: event.code,
				key: event.key,
				location: event.location,
				ctrlKey: event.ctrlKey,
				shiftKey: event.shiftKey,
				altKey: event.altKey,
				metaKey: event.metaKey,
				repeat: event.repeat,
			});
		});

		return;
	}

	event.preventDefault();
	if (event.repeat || event.key === 'Process') {
		return;
	}

	const scanCode = getHidScanCodeFromKeyboardEvent(event, getSelectedLayout());
	debug(log => {
		log(isDown ? 'keydown' : 'keyup', {
			code: event.code,
			key: event.key,
			location: event.location,
			ctrlKey: event.ctrlKey,
			shiftKey: event.shiftKey,
			altKey: event.altKey,
			metaKey: event.metaKey,
			scanCode: scanCode,
		});
	});

	if (scanCode === 0) {
		console.warn(`Key '${event.code || event.key}' has no scan code defined`);
	} else {
		await sendKeyboardEventsWithUpdate([{
			scanCode,
			isDown,
		}]);
	}
}

/**
 * @param {InputEvent} event
 */
async function onCharInput(event) {
	if (!(keyboardEnabledCheckbox.checked && usbAttached)) {
		return;
	}

	const layout = getSelectedLayout();
	if (!layout) {
		// TODO: toast
		return;
	}

	const typedKey = getTypedKey(event);
	const typedText = getTypedText(event);
	const unsupportedTextIndices = [];
	const scanCodes = typedKey ?
		jsKeyToHidScanCode(typedKey) :
		getHidScanCodesFromText(typedText, layout, unsupportedTextIndices);

	debug(log => {
		log('charinput', {
			inputType: event.inputType,
			data: event.data,
			scanCodes: scanCodes,
		});
	});

	for (const unsupportedTextIndex of unsupportedTextIndices) {
		console.warn(`Character ${typedText[unsupportedTextIndex]} has no scan code defined`);
	}

	if (scanCodes === 0 || (Array.isArray(scanCodes) && scanCodes.length === 0)) {
		console.warn(`Input ${event.inputType} with data '${event.data}' has no scan code defined`);
	} else {
		await sendKeyboardEventsWithUpdate(scanCodesToKvmEvents(scanCodes));
	}

	/**
	 * @param {InputEvent} event
	 */
	function getTypedText(event) {
		switch (event.inputType) {
			case 'insertText':
			case 'insertReplacementText':
				return event.data ?? '';
			default:
				return '';
		}
	}

	/**
	 * @param {InputEvent} event
	 */
	function getTypedKey(event) {
		switch (event.inputType) {
			case 'deleteContentBackward':
				return 'Backspace';
			case 'deleteContentForward':
				return 'Delete';
			case 'insertLineBreak':
			case 'insertParagraph':
				return 'Enter';
			default:
				return undefined;
		}
	}
}

function getSelectedLayout() {
	return keyboardLayoutSelect.value ? getLayout(keyboardLayoutSelect.value) : undefined;
}

/**
 * @param {number | number[][]} scanCodes
 */
function scanCodesToKvmEvents(scanCodes) {
	if (typeof scanCodes === 'number') {
		scanCodes = [[scanCodes]];
	}

	/** @type {import('./keyboard-api.js').KvmKeyEvent[]} */
	const events = [];
	for (const sequence of scanCodes) {
		for (let i = 0; i < sequence.length; i++) {
			events.push({
				scanCode: sequence[i],
				isDown: true,
			});
		}

		for (let i = sequence.length - 1; i >= 0; i--) {
			events.push({
				scanCode: sequence[i],
				isDown: false,
			});
		}
	}

	return events;
}

/**
 * @param {import('./keyboard-api.js').KvmKeyEvent[]} events
 * @returns {Promise<void>}
 */
async function sendKeyboardEventsWithUpdate(events) {
	for (const event of events) {
		if (event.isDown) {
			keyboardDownKeys.add(event.scanCode);
		}
	}

	updateKeyButtons();
	await sendKeyboardEvents(events);

	for (const event of events) {
		if (!event.isDown) {
			keyboardDownKeys.delete(event.scanCode);
		}
	}

	updateKeyButtons();
}

/** @type {Set<number>} */
const pointerDownButtons = new Set();

const pointerPosition = {
	x: NaN,
	y: NaN,
};
const lastPointerMovePosition = {
	x: NaN,
	y: NaN,
};

/**
 * @param {MouseEvent} event
 */
function setPointerPositionFromMouseEvent(event) {
	if (!isRelativePointer()) {
		pointerPosition.x = Math.round(event.offsetX / screenCanvas.offsetWidth * screenCanvas.width);
		pointerPosition.y = Math.round(event.offsetY / screenCanvas.offsetHeight * screenCanvas.height);
	}
}

/**
 * @param {Touch} touch
 * @param {number} [moveDeltaX]
 * @param {number} [moveDeltaY]
 */
function setPointerPositionFromTouch(touch, moveDeltaX, moveDeltaY) {
	if (!isRelativePointer()) {
		const screenRect = screenCanvas.getBoundingClientRect();
		if (typeof moveDeltaX === 'number' && typeof moveDeltaY === 'number' && isFinite(moveDeltaX) && isFinite(moveDeltaY)) {
			pointerPosition.x += Math.round(moveDeltaX / screenRect.width * screenCanvas.width);
			pointerPosition.y += Math.round(moveDeltaY / screenRect.height * screenCanvas.height);
		} else {
			pointerPosition.x = Math.round((touch.clientX - screenRect.left) / screenRect.width * screenCanvas.width);
			pointerPosition.y = Math.round((touch.clientY - screenRect.top) / screenRect.height * screenCanvas.height);
		}
	}
}

function isPointerMoveLate() {
	return pointerMoveLateCheckbox.checked && pointerDownButtons.size === 0 && !isRelativePointer();
}

/**
 * @param {string} [pointerType]
 */
function isRelativePointer(pointerType) {
	pointerType ??= pointerTypeSelect.value;
	return pointerType === 'BootMouse';
}

/**
 * @param {MouseEvent} event
 * @param {boolean} isDown
 */
async function onMouseButton(event, isDown) {
	if (!(pointerEnabledCheckbox.checked && screenHasFocus() && usbAttached)) {
		return;
	}

	debug(log => {
		log(isDown ? 'mousedown' : 'mouseup', {
			button: event.button,
			buttons: event.buttons,
			offsetX: event.offsetX,
			offsetY: event.offsetY,
			movementX: event.movementX,
			movementY: event.movementY,
		});
	});

	const button = event.button;
	if (isDown) {
		pointerDownButtons.add(button);
		event.preventDefault();
	} else if (pointerDownButtons.delete(button)) {
		event.preventDefault();
	}

	if (button >= 0 && button <= 2) {
		setPointerPositionFromMouseEvent(event);
		await sendPointerEvents(pointerTypeSelect.value, [{
			left: button === 0 ? isDown : undefined,
			middle: button === 1 ? isDown : undefined,
			right: button === 2 ? isDown : undefined,
			...isRelativePointer() ? undefined : {
				x: pointerPosition.x,
				y: pointerPosition.y,
			},
		}]);
	}
}

/**
 * @param {number} button
 * @param {boolean} [isDown]
 */
async function onTouchPointerButton(button, isDown) {
	if (!(pointerEnabledCheckbox.checked && screenHasFocus() && usbAttached)) {
		return;
	}

	debug(log => {
		log(isDown === undefined ? 'touch:click' : isDown ? 'touch:mousedown' : 'touch:mouseup', {
			button,
		});
	});

	if (isDown !== undefined) {
		if (isDown) {
			pointerDownButtons.add(button);
		} else {
			pointerDownButtons.delete(button);
		}
	}

	if (button >= 0 && button <= 2) {
		await sendPointerEvents(
			pointerTypeSelect.value,
			isDown === undefined ?
				[createEvent(button, true), createEvent(button, false)] :
				[createEvent(button, isDown)]);
	}

	/**
	 * @param {number} button
	 * @param {boolean} isDown
	 */
	function createEvent(button, isDown) {
		return {
			left: button === 0 ? isDown : undefined,
			middle: button === 1 ? isDown : undefined,
			right: button === 2 ? isDown : undefined,
		};
	}
}

/**
 * @param {MouseEvent} event
 */
async function onMouseMove(event) {
	if (!(pointerEnabledCheckbox.checked && screenHasFocus()) || isPointerMoveLate()) {
		return;
	}

	debug(log => {
		log('mousemove', {
			button: event.button,
			buttons: event.buttons,
			offsetX: event.offsetX,
			offsetY: event.offsetY,
			movementX: event.movementX,
			movementY: event.movementY,
		});
	});

	await sendPointerEvents(pointerTypeSelect.value, createEvents());

	/**
	 * @returns {import('./pointer-api.js').KvmPointerEvent[]}
	 */
	function createEvents() {
		switch (pointerTypeSelect.value) {
			case 'BootMouse':
				return [{
					x: clamp(Math.round(event.movementX), -127, 127),
					y: clamp(Math.round(event.movementY), -127, 127),
				}];

			case 'AbsoluteMouse':
				setPointerPositionFromMouseEvent(event);
				return [{
					x: pointerPosition.x,
					y: pointerPosition.y,
				}];
		}
	}
}

/**
 * @param {Touch} touch
 */
async function onTouchPointerMove(touch) {
	if (!(pointerEnabledCheckbox.checked && screenHasFocus()) || isPointerMoveLate()) {
		return;
	}

	debug(log => {
		log('touch:mousemove', {
			clientX: touch.clientX,
			clientY: touch.clientY,
		});
	});

	await sendPointerEvents(pointerTypeSelect.value, createEvents());

	/**
	 * @returns {import('./pointer-api.js').KvmPointerEvent[]}
	 */
	function createEvents() {
		let deltaX = Math.round(touch.clientX - lastPointerMovePosition.x);
		let deltaY = Math.round(touch.clientY - lastPointerMovePosition.y);
		lastPointerMovePosition.x = touch.clientX;
		lastPointerMovePosition.y = touch.clientY;

		switch (pointerTypeSelect.value) {
			case 'BootMouse':
				/** @type {import('./pointer-api.js').KvmPointerEvent[]} */
				const events = [];
				while (isFinite(deltaX) && isFinite(deltaY) && (deltaX != 0 || deltaY != 0)) {
					const x = clamp(deltaX, -127, 127);
					const y = clamp(deltaY, -127, 127);
					deltaX -= x;
					deltaY -= y;
					events.push({
						x,
						y,
					});
				}

				return events;

			case 'AbsoluteMouse':
				if (isNaN(pointerPosition.x) || isNaN(pointerPosition.y)) {
					setPointerPositionFromTouch(touch);
				} else if (isNaN(deltaX) || isNaN(deltaY)) {
					return [];
				} else {
					setPointerPositionFromTouch(touch, deltaX, deltaY);
				}

				return [{
					x: pointerPosition.x,
					y: pointerPosition.y,
				}];
		}
	}
}

async function onChangePointerType() {
	attachUsbButton.disabled = true;
	try {
		await sendPointerReset(pointerTypeSelect.value);
	} finally {
		attachUsbButton.disabled = false;
	}
}

async function releaseAllInput() {
	if (keyboardDownKeys.size > 0) {
		keyboardDownKeys.clear();
		await sendKeyboardReset();
		updateKeyButtons();
	}

	if (pointerDownButtons.size > 0) {
		pointerDownButtons.clear();
		await sendPointerReset(pointerTypeSelect.value);
	}
}

function updateKeyButtons() {
	for (const btn of virtualKeyButtons) {
		if (/^[a-zA-Z0-9]+(?::toggle)?$/.test(btn.vkey)) {
			const scanCode = jsKeyToHidScanCode(btn.vkey.split(':')[0]);
			if (scanCode !== 0) {
				btn.button.classList.toggle('pressed', keyboardDownKeys.has(scanCode));
			}
		}
	}
}

let usbAttached = false;
/**
 * @param {import('./usb-api.js').UsbStateResponse} state
 */
function onUsbStateChange(state) {
	usbAttached = state.attached;
	detachUsbButton.disabled = !state.attached;
	document.body.setAttribute('data-usb-attached', state.attached ? '1' : '0');
}

/**
 * @param {import('./keyboard-api.js').KeyboardLedsResponse} leds
 */
function onKeyboardLedsChange(leds) {
	capsLockButton.classList.toggle('locked', !!leds.capsLock);
	numLockButton.classList.toggle('locked', !!leds.numLock);
}

function init() {
	for (const drop of document.querySelectorAll('.drop')) {
		initDrop(drop);
	}

	linkSelectSetting('pointerType', pointerTypeSelect, (value) => {
		const isRelative = isRelativePointer(value);
		pointerMoveLateCheckbox.disabled = isRelative;
		showLocalPointerCheckbox.disabled = isRelative;
		screenCanvas.classList.toggle('pointer-relative', isRelative);
		onChangePointerType();
	});
	linkCheckboxSetting('pointerEnabled', pointerEnabledCheckbox);
	linkCheckboxSetting('pointerMoveLate', pointerMoveLateCheckbox);
	linkCheckboxSetting('showLocalPointer', showLocalPointerCheckbox);
	linkCssClass('pointer-hidden', showLocalPointerCheckbox, screenCanvas, true);
	linkCheckboxSetting('fitScreen', fitScreenCheckbox, () => {
		setTimeout(() => {
			onScreenSizeChanged();
		}, 1)
	});
	linkCssClass('fit-screen', fitScreenCheckbox, screenContainer);

	addSelectOptions(keyboardLayoutSelect, KEYBOARD_LAYOUTS);
	linkCheckboxSetting('keyboardEnabled', keyboardEnabledCheckbox);
	linkSelectSetting('keyboardLayout', keyboardLayoutSelect, async () => {
		const layoutId = keyboardLayoutSelect.value;
		if (layoutId) {
			try {
				await loadLayout(layoutId);
			} catch (e) {
				console.error(e);
				// TODO: toast
			}
		}
	});

	linkInputSetting('mjpegQuality', mjpegQualityInput, (value, done) => {
		if (done) {
			mjpegVideo.quality = value;
			mjpegVideo.abortController.abort();
		}
	});

	linkSelectSetting('mjpegSubsampling', mjpegSubsamplingSelect, (value) => {
		mjpegVideo.subsampling = value;
		mjpegVideo.abortController.abort();
	});

	linkVisibilityToggleButtonSetting('showToolbar', showToolbarButton, toolbarPanel, 'closed', () => {
		onScreenSizeChanged();
	});

	disableEvents(screenCanvas, [
		'contextmenu',
		'cut',
		'copy',
		'paste',
		'drop',
		'dragover',
	]);

	toggleFullscreenButton.addEventListener('click', async () => {
		if (document.fullscreenElement) {
			await document.exitFullscreen();
		} else {
			await document.body.requestFullscreen();
		}
	});

	window.addEventListener('keydown', (event) => {
		if (event.target === screenCanvas) {
			event.stopPropagation();
			onKeyboardKey(event, true);
		}
	}, {
		capture: true,
	});

	window.addEventListener('keyup', (event) => {
		if (event.target == screenCanvas) {
			event.stopPropagation();
		} else if (keyboardDownKeys.size === 0) {
			return;
		}

		onKeyboardKey(event, false);
	}, {
		capture: true,
	});

	screenCanvas.addEventListener('beforeinput', (event) => {
		event.preventDefault();
		event.stopPropagation();
		lockScrollTemporarily(screenContainer, 50);
		onCharInput(event);
	});

	/** @type {{ touchCount: number }} */
	let screenTouchStart = undefined;
	let screenTouchLeftClickTimeout = 0;
	let screenTouchRightClickTimeout = 0;
	let screenLastTouchStartTime = NaN;
	let screenLastTouchEndTime = NaN;
	let screenGotFocusByPointer = false;

	screenCanvas.addEventListener('mousemove', (event) => {
		onMouseMove(event);
	});

	screenCanvas.addEventListener('mousedown', (event) => {
		if (!screenHasFocus()) {
			debug.log('mousedown: screenGotFocusByPointer = true');
			screenGotFocusByPointer = true;
			return;
		}

		lastPointerMovePosition.x = NaN;
		lastPointerMovePosition.y = NaN;
		onMouseButton(event, true);
	}, {
		capture: true,
	});

	window.addEventListener('mouseup', (event) => {
		if (screenGotFocusByPointer) {
			debug.log('mouseup: screenGotFocusByPointer = false');
			screenGotFocusByPointer = false;
			return;
		}

		lastPointerMovePosition.x = NaN;
		lastPointerMovePosition.y = NaN;
		if (pointerDownButtons.has(event.button)) {
			onMouseButton(event, false);
		}
	}, {
		capture: true,
	});

	screenCanvas.addEventListener('touchstart', async (event) => {
		if (event.touches.length === 1 && event.targetTouches.length === 1) {
			event.preventDefault();
			if (!screenHasFocus()) {
				screenCanvas.focus();
			}

			lastPointerMovePosition.x = NaN;
			lastPointerMovePosition.y = NaN;

			debug(log => {
				log('touchstart', {
					clientX: event.touches[0].clientX,
					clientY: event.touches[0].clientY,
				});
			});

			screenTouchStart = {
				touchCount: event.touches.length,
			};

			const now = performance.now();
			const hasClickTimeout = !!screenTouchLeftClickTimeout;
			clearTimeout(screenTouchLeftClickTimeout);
			screenTouchLeftClickTimeout = 0;
			screenLastTouchStartTime = now;
			if (now - screenLastTouchEndTime < 300 && hasClickTimeout) {
				await onTouchPointerButton(0, true);
			} else {
				clearTimeout(screenTouchRightClickTimeout);
				screenTouchRightClickTimeout = setTimeout(() => {
					screenTouchStart = undefined;
					screenTouchRightClickTimeout = false;
					onTouchPointerButton(2);
				}, 800);
			}
		} else {
			screenTouchStart = undefined;
		}
	});

	screenCanvas.addEventListener('touchend', async (event) => {
		if (event.touches.length === 0) {
			event.preventDefault();
			const touchStart = screenTouchStart;
			screenTouchStart = undefined;
			const now = performance.now();
			screenLastTouchEndTime = now;

			lastPointerMovePosition.x = NaN;
			lastPointerMovePosition.y = NaN;
			debug.log('touchend', touchStart);

			clearTimeout(screenTouchLeftClickTimeout);
			screenTouchLeftClickTimeout = 0;
			clearTimeout(screenTouchRightClickTimeout);
			screenTouchRightClickTimeout = 0;

			if (pointerDownButtons.size > 0) {
				for (const button of pointerDownButtons) {
					await onTouchPointerButton(button, false);
				}

				if (touchStart?.touchCount === 1 && now - screenLastTouchStartTime < 300) {
					await onTouchPointerButton(0);
				}
			} else if (touchStart?.touchCount === 1 && now - screenLastTouchStartTime < 300) {
				screenTouchLeftClickTimeout = setTimeout(() => {
					screenTouchLeftClickTimeout = 0;
					onTouchPointerButton(0);
				}, 300);
			}
		}
	});

	screenCanvas.addEventListener('touchmove', async (event) => {
		if (event.touches.length === 1 && event.targetTouches.length === 1) {
			screenTouchStart = undefined;
			clearTimeout(screenTouchRightClickTimeout);
			screenTouchRightClickTimeout = 0;

			await onTouchPointerMove(event.touches[0]); // TODO: keep cursor centered on screen when scrollable
		}
	});

	window.addEventListener('wheel', (event) => {
		if (!(pointerEnabledCheckbox.checked && event.target === screenCanvas && screenHasFocus() && usbAttached)) {
			return;
		}

		event.preventDefault();
		event.stopPropagation();
		setPointerPositionFromMouseEvent(event);
		sendPointerEvents(pointerTypeSelect.value, [{
			wheel: getWheelAmount(event.deltaY, event.deltaMode),
			...isRelativePointer() ? undefined : {
				x: pointerPosition.x,
				y: pointerPosition.y,
			},
		}]);
	}, {
		capture: true,
		passive: false,
	});

	screenCanvas.addEventListener('focus', async () => {
		if (screenGotFocusByPointer) {
			lockPointerIfNecessary();
		}
	});

	screenCanvas.addEventListener('mouseenter', () => {
		if (screenHasFocus() && document.pointerLockElement !== screenCanvas) {
			lockPointerIfNecessary();
		}
	});

	async function lockPointerIfNecessary() {
		if (isRelativePointer() && !hasTouchscreen()) {
			try {
				await screenCanvas.requestPointerLock({
					unadjustedMovement: true,
				});
			} catch (e) {
				if (e instanceof Error && e.name === 'NotSupportedError') {
					await screenCanvas.requestPointerLock();
				}
			}
		}
	}

	document.addEventListener('pointerlockchange', () => {
		if (document.pointerLockElement !== screenCanvas) {
			screenCanvas.blur();
		}
	});

	screenCanvas.addEventListener('blur', () => {
		if (!screenContainer.contains(document.activeElement)) {
			setTimeout(() => {
				if (!screenHasFocus()) {
					screenCanvas.contentEditable = 'false';
				}
			}, 10);
			setTimeout(() => {
				if (!screenHasFocus()) {
					releaseAllInput();
				}
			}, 500);
		}
	});

	window.addEventListener('blur', () => {
		releaseAllInput();
	});

	linkInputValueToDisabled([keyboardLayoutSelect, sendTextInput], sendTextButton)
	addAsyncClickListener(sendTextButton, async () => {
		const layout = getSelectedLayout();
		if (!layout) {
			debug.log('No layout selected'); // TODO: toast
			return;
		}

		const unsupportedIndices = [];
		const text = sendTextInput.value;
		const events = scanCodesToKvmEvents(getHidScanCodesFromText(text, layout, unsupportedIndices));
		if (unsupportedIndices.length > 0 || events.length === 0) {
			// TODO: show warning
			debug.log('No scan codes for \'' + unsupportedIndices.map(i => text[i]).join() + '\'');
			return;
		}

		await sendKeyboardEvents(events); // TODO: handle network errors
	});

	for (const key of virtualKeyButtons) {
		addAsyncClickListener(key.button, async () => {
			if (!usbAttached) {
				return;
			}

			screenCanvas.focus();
			await sendKeyboardEventsWithUpdate(createKeyboardSequenceEvents(key.vkey, keyboardDownKeys, 20));
		});
	}

	addTouchClickListener(screenContainer, (event) => event.touches.length === 3, () => {
		if (!(keyboardEnabledCheckbox.checked && usbAttached)) {
			return;
		}

		screenCanvas.contentEditable = screenCanvas.contentEditable === 'true' ? 'false' : 'true';
		screenCanvas.focus();
	});

	addAsyncClickListener(attachUsbButton, async () => {
		const state = await attachUsb(10);
		if (!state.attached) {
			// TODO: show error message
		}
	});

	addAsyncClickListener(detachUsbButton, async () => {
		try {
			await releaseAllInput();
		} catch (e) {
			console.error(e);
		}

		const state = await detachUsb(10);
		if (state.attached) {
			// TODO: show error message
		}
	});
}

async function runVideo() {
	/** @type {WakeLockSentinel | undefined} */
	let wakeLock = undefined;
	while (true) {
		try {
			mjpegVideo.abortController = new AbortController();
			await fetchMjpegRects(
				`/kvm/screen.mjpeg?quality=${mjpegVideo.quality}&subsampling=${mjpegVideo.subsampling}`,
				screenCanvas,
				{
					signal: mjpegVideo.abortController.signal,
					async onReady() {
						if (navigator.wakeLock) {
							try {
								wakeLock = await navigator.wakeLock.request();
							} catch (e) {
								console.error(e);
							}
						}
					},
					onResize() {
						onScreenSizeChanged();
					},
				});
		} catch (e) {
			wakeLock?.release();
			if (!mjpegVideo.abortController.signal.aborted) {
				console.error(e);
				await delay(1000);
			}
		}
	}
}

async function runServerEvents() {
	const state = {};
	while (true) {
		try {
			onUsbStateChange(await getUsbState());
			onKeyboardLedsChange(await getKeyboardLeds());

			await fetchEvents((event) => {
				debug.log('server event', event);
				switch (event.$type) {
					case 'usb/state':
						onUsbStateChange(event);
						break;

					case 'keyboard/leds':
						onKeyboardLedsChange(event);
						break;

					default:
						console.error('Received unknown event: ' + event.$type);
						break;
				}
			}, state);
		} catch (e) {
			console.error(e);
			await delay(1000);
		}
	}
}

init();
initOverlay();
runVideo();
runServerEvents();
