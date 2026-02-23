const STORAGE_KEY = 'settings';

/** @type {Record<string, any>} */
let currentSettings = location.hash ? parseHash(location.hash) : parseStorage();

/**
 * @param {string} key
 * @param {HTMLInputElement} checkbox
 * @param {(checked: boolean) => void} [onChange]
 */
export function linkCheckboxSetting(key, checkbox, onChange) {
	checkbox.addEventListener('change', () => {
		if (onChange) {
			onChange(checkbox.checked);
		}

		setSetting(key, checkbox.checked);
	});

	const checked = getSetting(key);
	if (typeof checked === 'boolean' && checkbox.checked !== checked) {
		checkbox.checked = checked;
		if (onChange) {
			onChange(checked);
		}
	}
}

/**
 * @param {HTMLInputElement} parent
 * @param {Record<string, HTMLInputElement | { input: HTMLInputElement, onChange?: (checked: boolean) => void}>} children
 * @param {(checked: boolean) => void} [onParentChange]
 */
export function linkCheckboxGroupSetting(parent, children, onParentChange) {
	parent.addEventListener('change', () => {
		if (parent.indeterminate) {
			return;
		}

		if (onParentChange) {
			onParentChange(parent.checked);
		}

		for (const key in children) {
			setSetting(key, parent.checked);
		}
	});

	for (const key in children) {
		const child = children[key];
		const { input, onChange } = child instanceof HTMLInputElement ? { input: child } : child;
		linkCheckboxSetting(key, input, onChange);
	}
}

/**
 * @param {string} key
 * @param {HTMLInputElement} input
 * @param {(value: string | number, done: boolean) => void} [onChange]
 */
export function linkInputSetting(key, input, onChange) {
	if (onChange) {
		input.addEventListener('input', () => {
			const value = convertValue(input.value, input.type);
			if ((value ?? undefined) !== undefined) {
				onChange(value, false);
			}
		});
	}

	input.addEventListener('change', () => {
		const value = convertValue(input.value, input.type);
		if ((value ?? undefined) !== undefined) {
			if (onChange) {
				onChange(value, true);
			}

			setSetting(key, value);
		}
	});

	const value = getSetting(key);
	if (typeof value === (input.type === 'number' ? 'number' : 'string') && String(value) !== input.value) {
		input.value = String(value);
		if (onChange) {
			onChange(value, true);
		}
	}

	/**
	 * @param {string} value
	 * @param {string} type
	 * @returns {string | number | null}
	 */
	function convertValue(value, type) {
		return value ? type === 'number' ? Number(value) : value : null;
	}
}

/**
 * @param {string} key
 * @param {HTMLSelectElement} select
 * @param {(value: string) => void} [onChange]
 */
export function linkSelectSetting(key, select, onChange) {
	select.addEventListener('change', () => {
		const value = select.value;
		if (onChange) {
			onChange(value);
		}

		setSetting(key, value);
	});

	const value = getSetting(key);
	if (typeof value === 'string' && value !== select.value) {
		select.value = value;
		if (onChange) {
			onChange(value);
		}
	}
}

/**
 * @param {string} key
 * @param {HTMLButtonElement} select
 * @param {HTMLElement} target
 * @param {string} [className='hidden']
 * @param {(visible: boolean) => void} [onClick]
 */
export function linkVisibilityToggleButtonSetting(key, button, target, className, onClick) {
	className ??= 'hidden';
	button.addEventListener('click', () => {
		let visible = !target.classList.toggle(className);
		if (onClick) {
			onClick(visible);
		}

		setSetting(key, visible);
	});

	const visible = getSetting(key);
	if (typeof visible === 'boolean') {
		target.classList.toggle(className, !visible);
	}
}

/**
 * @param {string} key
 * @returns {any}
 */
export function getSetting(key) {
	return currentSettings[key];
}

/**
 * @param {string} key
 * @param {any} value
 */
export function setSetting(key, value) {
	const settings = currentSettings;
	settings[key] = structuredClone(value);
	try {
		localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
	} catch (e) {}

	history.replaceState(undefined, '', hashifyObject(settings));
}

/**
 * @param {string} hash
 * @returns {Record<string, any>}
 */
function parseHash(hash) {
	if (hash.startsWith('#')) {
		hash = hash.slice(1);
	}

	hash = decodeURIComponent(hash);
	if (!hash.startsWith('{')) {
		return {};
	}

	try {
		return JSON.parse(hash);
	} catch (e) {
		return {};
	}
}

/**
 * @param {Record<string, any>} obj
 */
function hashifyObject(obj) {
	const hash = encodeURIComponent(JSON.stringify(obj));
	return '#' + hash
		.replaceAll('%7B', '{')
		.replaceAll('%7D', '}')
		.replaceAll('%22', '\"')
		.replaceAll('%3A', ':')
		.replaceAll('%2C', ',');
}

/**
 * @returns {Record<string, any>}
 */
function parseStorage() {
	try {
		const settingsJson = localStorage.getItem(STORAGE_KEY);
		if (settingsJson && settingsJson.startsWith('{')) {
			return JSON.parse(settingsJson);
		}
	} catch (e) {}

	return {};
}
