import { getSetting, linkSelectSetting, linkVisibilityToggleButtonSetting, setSetting } from './settings.js';
import { getElementById, hasTouchscreen, queryElements } from './utils.js';

const screenContainer = getElementById('screenContainer', HTMLDivElement);
const screenCanvas = getElementById('screenCanvas', HTMLCanvasElement);
const overlayGripButton = getElementById('overlayGripButton', HTMLButtonElement);
const overlayArea = getElementById('overlayArea', HTMLDivElement);
const overlayContainer = getElementById('overlayContainer', HTMLDivElement);
const overlayDropAreas = queryElements('.overlay-drop-area', HTMLDivElement);
const showToolbarButton = getElementById('showToolbarButton', HTMLButtonElement);
const overlayStyleSelect = getElementById('overlayStyleSelect', HTMLSelectElement);
const overlayPositionSelect = getElementById('overlayPositionSelect', HTMLSelectElement);

const POSITIONS = ['left', 'right', 'top', 'bottom'];
const POSITION_SETTING = 'overlayPosition';
const OFFSET_SETTING = 'overlayOffset';

const STYLES = [...overlayStyleSelect.options].map(o => o.value);

const offsetStartPosition = { x: 0, y: 0, offset: 0 };
let currentPosition = 'left';
let currentOffset = 0;

/**
 * @param {string} value
 */
function setStyle(value) {
	overlayStyleSelect.value = value;
	for (const style of STYLES) {
		overlayArea.classList.toggle(style, style === value);
	}
}

/**
 * @param {boolean} value
 */
function setDragging(value) {
	overlayArea.classList.toggle('dragging', value);
	onScreenSizeChanged();

	if (value) {
		window.addEventListener('dragover', updateOffset, {
			capture: true,
			passive: true,
		});
	} else {
		window.removeEventListener('dragover', updateOffset);
		setSetting(OFFSET_SETTING, currentOffset);
	}
}

/**
 * @param {HTMLDivElement} target
 * @param {boolean} saveSettings
 */
function setPosition(target, saveSettings) {
	for (const position of POSITIONS) {
		if (overlayArea.classList.toggle(position, target.classList.contains(position))) {
			currentPosition = position;
			currentOffset = 0;
			overlayPositionSelect.value = position;
			if (saveSettings) {
				setSetting(POSITION_SETTING, position);
				setSetting(OFFSET_SETTING, 0);
			}
		}
	}
}

/**
 * @param {string} position
 * @returns {HTMLDivElement}
 */
function getDropArea(position) {
	for (const area of overlayDropAreas) {
		if (area.classList.contains(position)) {
			return area;
		}
	}

	throw new Error();
}

/**
 * @param {PointerEvent} event
 */
function updateOffset(event) {
	const isVerticalMove = currentPosition === 'left' || currentPosition === 'right';
	const offset = offsetStartPosition.offset + (isVerticalMove ? event.screenY - offsetStartPosition.y : event.screenX - offsetStartPosition.x);
	const distance = isVerticalMove ? event.screenX - offsetStartPosition.x : event.screenY - offsetStartPosition.y;

	if (Math.abs(distance) < 50) {
		setOffset(offset);
	}
}

/**
 * @param {number} offset
 */
function setOffset(offset) {
	overlayArea.style.setProperty('--overlay-offset', offset + 'px');
	overlayArea.style.setProperty('--overlay-offset-clamp-x', Math.ceil(overlayContainer.offsetWidth / 2) + 'px');
	overlayArea.style.setProperty('--overlay-offset-clamp-y', Math.ceil(overlayContainer.offsetHeight / 2) + 'px');
	currentOffset = offset;
}

/**
 * @param {string} position
 * @param {HTMLDivElement} dropArea
 * @returns {[margin: number, fixedDistance: number]}
 */
function getOverlayAreaMargin(position, dropArea) {
	switch (position) {
		case 'left':
			return [
				screenCanvas.offsetLeft - (dropArea.offsetWidth || overlayArea.offsetWidth),
				screenContainer.offsetWidth / 2 - screenCanvas.offsetLeft,
			];
		case 'right':
			return [
				screenContainer.offsetWidth - screenCanvas.offsetLeft - screenCanvas.offsetWidth - (dropArea.offsetWidth || overlayArea.offsetWidth),
				screenContainer.offsetWidth / 2 - screenCanvas.offsetLeft,
			];
		case 'top':
			return [
				screenCanvas.offsetTop - (dropArea.offsetHeight || overlayArea.offsetHeight),
				screenContainer.offsetHeight / 2 - screenCanvas.offsetTop,
			];
		case 'bottom':
			return [
				screenContainer.offsetHeight - screenCanvas.offsetTop - screenCanvas.offsetHeight - (dropArea.offsetHeight || overlayArea.offsetHeight),
				screenContainer.offsetHeight / 2 - screenCanvas.offsetTop,
			];
		default:
			throw new Error();
	}
}

export function onScreenSizeChanged() {
	const FIXED_CLASS = 'fixed';

	for (const position of POSITIONS) {
		const dropArea = getDropArea(position);
		const [margin, distance] = getOverlayAreaMargin(position, dropArea);
		const canBeFixed = dropArea.classList.contains(FIXED_CLASS) ? (margin >= 1) : (margin > 10);
		if (canBeFixed) {
			screenContainer.style.setProperty('--overlay-distance-' + position, Math.ceil(distance) + 'px');
		} else {
			screenContainer.style.removeProperty('--overlay-distance-' + position);
		}

		dropArea.classList.toggle(FIXED_CLASS, canBeFixed);
		if (overlayArea.classList.contains(position)) {
			overlayArea.classList.toggle(FIXED_CLASS, canBeFixed);
		}
	}
}

export function init() {
	if (hasTouchscreen()) {
		setStyle('slim');
		setPosition(getDropArea('bottom'), false);
	}

	linkVisibilityToggleButtonSetting('showOverlay', overlayGripButton, overlayArea, 'closed');
	linkSelectSetting('overlayStyle', overlayStyleSelect, (value) => {
		setStyle(value);
		onScreenSizeChanged();
	});

	overlayPositionSelect.addEventListener('change', () => {
		setPosition(getDropArea(overlayPositionSelect.value), true);
		onScreenSizeChanged();
	});

	const savedPosition = getSetting(POSITION_SETTING);
	if (savedPosition) {
		setPosition(getDropArea(savedPosition), false);
	}

	const savedOffset = getSetting(OFFSET_SETTING);
	if (savedOffset) {
		setOffset(savedOffset);
	}

	overlayGripButton.addEventListener('dragstart', () => {
		setDragging(true);
	});

	showToolbarButton.addEventListener('dragstart', () => {
		setDragging(true);
	});

	overlayArea.addEventListener('pointerdown', (event) => {
		offsetStartPosition.x = event.screenX;
		offsetStartPosition.y = event.screenY;
		offsetStartPosition.offset = currentOffset; // TODO: breaks with clamping
	}, {
		capture: true,
		passive: false,
	});

	for (const area of overlayDropAreas) {
		area.addEventListener('dragenter', (event) => {
			console.log('dragenter');
			setPosition(area, false);
			offsetStartPosition.x = event.screenX;
			offsetStartPosition.y = event.screenY;
			offsetStartPosition.offset = currentOffset; // TODO: breaks with clamping
		});

		area.addEventListener('dragover', (event) => {
			event.preventDefault();
		});

		area.addEventListener('drop', (event) => {
			console.log('drop');
			event.preventDefault();
			setPosition(area, true);
			setDragging(false);
		});
	}

	window.addEventListener('dragend', () => {
		setDragging(false);
	}, {
		capture: true,
		passive: true,
	});

	let deferredResizeTimeout = 0;
	window.addEventListener('resize', () => {
		onScreenSizeChanged();
		clearTimeout(deferredResizeTimeout);
		deferredResizeTimeout = setTimeout(() => {
			onScreenSizeChanged();
		}, 100);
	});

	onScreenSizeChanged();
}
