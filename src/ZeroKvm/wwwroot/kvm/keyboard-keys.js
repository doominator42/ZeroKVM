
/**
 * JS keys:
 * - https://developer.mozilla.org/en-US/docs/Web/API/UI_Events/Keyboard_event_code_values
 * - https://developer.mozilla.org/en-US/docs/Web/API/KeyboardEvent/keyCode
 *
 * USB HID scan codes:
 * - https://aeb.win.tue.nl/linux/kbd/scancodes-14.html
 * - https://gist.github.com/MightyPork/6da26e382a7ad91b5496ee55fdc73db2
 *
 * @type {Map<string, number>}
 */
const CODE_TO_SCAN_CODE = new Map([
	// KeyboardEvent.code
	['AltLeft', 226], // Left Alt
	['AltRight', 230], // Right Alt
	['ArrowDown', 81], // Down Arrow
	['ArrowLeft', 80], // Left Arrow
	['ArrowRight', 79], // Right Arrow
	['ArrowUp', 82], // Up Arrow
	['AudioVolumeDown', 129], // Volume Down
	['AudioVolumeMute', 228], // Mute
	['AudioVolumeUp', 128], // Volume Up
	['Backquote', 53], // `
	['Backslash', 49], // \
	['Backspace', 42], // Backspace
	['BracketLeft', 47], // [
	['BracketRight', 48], // ]
	['CapsLock', 57], // Caps Lock
	['Comma', 54], // ,
	['ContextMenu', 118], // Menu
	['ControlLeft', 224], // Left Ctrl
	['ControlRight', 228], // Right Ctrl
	['Delete', 76], // Delete
	['Digit0', 39], // 0
	['Digit1', 30], // 1
	['Digit2', 31], // 2
	['Digit3', 32], // 3
	['Digit4', 33], // 4
	['Digit5', 34], // 5
	['Digit6', 35], // 6
	['Digit7', 36], // 7
	['Digit8', 37], // 8
	['Digit9', 38], // 9
	['Equal', 46], // =
	['End', 77], // End
	['Enter', 40], // Enter
	['Escape', 41], // Esc
	['F1', 58], // F1
	['F2', 59], // F2
	['F3', 60], // F3
	['F4', 61], // F4
	['F5', 62], // F5
	['F6', 63], // F6
	['F7', 64], // F7
	['F8', 65], // F8
	['F9', 66], // F9
	['F10', 67], // F10
	['F11', 68], // F11
	['F12', 69], // F12
	['F13', 104], // F13
	['F14', 105], // F14
	['F15', 106], // F15
	['F16', 107], // F16
	['F17', 108], // F17
	['F18', 109], // F18
	['F19', 110], // F19
	['F20', 111], // F20
	['F21', 112], // F21
	['F22', 113], // F22
	['F23', 114], // F23
	['F24', 115], // F24
	['Home', 74], // Home
	['Insert', 73], // Insert
	['IntlBackslash', 100], // ...
	['KeyA', 4], // a
	['KeyB', 5], // b
	['KeyC', 6], // c
	['KeyD', 7], // d
	['KeyE', 8], // e
	['KeyF', 9], // f
	['KeyG', 10], // g
	['KeyH', 11], // h
	['KeyI', 12], // i
	['KeyJ', 13], // j
	['KeyK', 14], // k
	['KeyL', 15], // l
	['KeyM', 16], // m
	['KeyN', 17], // n
	['KeyO', 18], // o
	['KeyP', 19], // p
	['KeyQ', 20], // q
	['KeyR', 21], // r
	['KeyS', 22], // s
	['KeyT', 23], // t
	['KeyU', 24], // u
	['KeyV', 25], // v
	['KeyW', 26], // w
	['KeyX', 27], // x
	['KeyY', 28], // y
	['KeyZ', 29], // z
	['MetaLeft', 227], // Left GUI
	['MetaRight', 231], // Right GUI
	['Minus', 45], // -
	['NumLock', 83], // Num Lock
	['Numpad0', 98], // Keypad 0
	['Numpad1', 89], // Keypad 1
	['Numpad2', 90], // Keypad 2
	['Numpad3', 91], // Keypad 3
	['Numpad4', 92], // Keypad 4
	['Numpad5', 93], // Keypad 5
	['Numpad6', 94], // Keypad 6
	['Numpad7', 95], // Keypad 7
	['Numpad8', 96], // Keypad 8
	['Numpad9', 97], // Keypad 9
	['NumpadAdd', 87], // Keypad +
	['NumpadComma', 133], // Keypad ,
	['NumpadDecimal', 99], // Keypad .
	['NumpadDivide', 84], // Keypad /
	['NumpadEnter', 88], // Keypad Enter
	['NumpadEqual', 103], // Keypad =
	['NumpadMultiply', 85], // Keypad *
	['NumpadSubtract', 86], // Keypad -
	['Quote', 52], // '
	['PageDown', 78], // Page Down
	['PageUp', 75], // Page Up
	['Pause', 72], // Pause Break
	['Period', 55], // .
	['PrintScreen', 70], // Print Screen
	['ScrollLock', 71], // Scroll Lock
	['Semicolon', 51], // ;
	['ShiftLeft', 225], // Left Shift
	['ShiftRight', 229], // Right Shift
	['Slash', 56], // /
	['Space', 44], // Spacebar
	['Tab', 43], // Tab

	// KeyboardEvent.code (compatibility)
	['OSLeft', 227], // Left GUI
	['OSRight', 231], // Right GUI
	['VolumeDown', 129], // Volume Down
	['VolumeMute', 228], // Mute
	['VolumeUp', 128], // Volume Up

	// KeyboardEvent.key
	['Again', 121], // Again
	['Alt', 226], // Left Alt
	['AltGraph', 230], // Right Alt
	['Cancel', 155], // Cancel
	['Clear', 156], // Clear
	['Control', 224], // Left Control
	['Copy', 124], // Copy
	['Cut', 123], // Cut
	['Execute', 116], // Execute
	['Find', 126], // Find
	['Help', 117], // Help
	['Meta', 227], // Left GUI
	['Paste', 125], // Paste
	['Redo', 121], // Again
	['Select', 119], // Select
	['Shift', 225], // Left Shift
	['Super', 227], // Left GUI
	['Undo', 122], // Undo
	['\x20', 44], // Spacebar
]);

/**
 * @param {string} start
 * @param {string} end
 * @param {string} [prefix]
 */
function decSeq(start, end, prefix) {
	const chars = [];
	prefix ??= '';
	for (let i = start; i < end; i++) {
		chars.push(prefix + i);
	}

	return chars;
}

const NON_CHAR_INPUT_KEYS = new Set([
	// KeyboardEvent.code
	'AltLeft',
	'AltRight',
	'ArrowDown',
	'ArrowLeft',
	'ArrowRight',
	'ArrowUp',
	'AudioVolumeDown',
	'AudioVolumeMute',
	'AudioVolumeUp',
	'CapsLock',
	'ContextMenu',
	'ControlLeft',
	'ControlRight',
	'End',
	'Escape',
	...decSeq(1, 24, 'F'),
	'Home',
	'Insert',
	'MetaLeft',
	'MetaRight',
	'NumLock',
	'PageDown',
	'PageUp',
	'Pause',
	'PrintScreen',
	'ScrollLock',
	'Tab',

	// KeyboardEvent.code (compatibility)
	'OSLeft',
	'OSRight',
	'VolumeDown',
	'VolumeMute',
	'VolumeUp',

	// KeyboardEvent.key
	'Alt',
	'AltGraph',
	'Control',
	'Meta',
	'Super',
]);

/**
 * Generated from https://kbdlayout.info/
 * See ~/src/ZeroKvm/dl-kbd.sh
 * @type {[id: string, label: string][]}
 */
export const KEYBOARD_LAYOUTS = [
	['kbdadlm', 'ADLaM'],
	['kbdal', 'Albanian'],
	['kbda1', 'Arabic (101)'],
	['kbda2', 'Arabic (102)'],
	['kbda3', 'Arabic (102) AZERTY'],
	['kbdarme', 'Armenian Eastern (Legacy)'],
	['kbdarmph', 'Armenian Phonetic'],
	['kbdarmty', 'Armenian Typewriter'],
	['kbdarmw', 'Armenian Western (Legacy)'],
	['kbdinasa', 'Assamese - INSCRIPT'],
	['kbdazst', 'Azerbaijani (Standard)'],
	['kbdaze', 'Azerbaijani Cyrillic'],
	['kbdazel', 'Azerbaijani Latin'],
	['kbdinben', 'Bangla'],
	['kbdinbe2', 'Bangla - INSCRIPT'],
	['kbdinbe1', 'Bangla - INSCRIPT (Legacy)'],
	['kbdbash', 'Bashkir'],
	['kbdblr', 'Belarusian'],
	['kbdbene', 'Belgian (Comma)'],
	['kbdbe', 'Belgian (Period)'],
	['kbdbe', 'Belgian French'],
	['kbdbhc', 'Bosnian (Cyrillic)'],
	['kbdbug', 'Buginese'],
	['kbdbulg', 'Bulgarian'],
	['kbdus', 'Bulgarian (Latin)'],
	['kbdbgph1', 'Bulgarian (Phonetic Traditional)'],
	['kbdbgph', 'Bulgarian (Phonetic)'],
	['kbdbu', 'Bulgarian (Typewriter)'],
	['kbdca', 'Canadian French'],
	['kbdfc', 'Canadian French (Legacy)'],
	['kbdcan', 'Canadian Multilingual Standard'],
	['kbdtzm', 'Central Atlas Tamazight'],
	['kbdkurd', 'Central Kurdish'],
	['kbdcmk', 'Colemak'],
	['kbdcz', 'Czech'],
	['kbdcz1', 'Czech (QWERTY)'],
	['kbdcz2', 'Czech Programmers'],
	['kbdcher', 'Cherokee Nation'],
	['kbdcherp', 'Cherokee Phonetic'],
	['kbdus', 'Chinese (Simplified) - US'],
	['kbdus', 'Chinese (Simplified, Singapore) - US'],
	['kbdus', 'Chinese (Traditional) - US'],
	['kbdus', 'Chinese (Traditional, Hong Kong S.A.R.) - US'],
	['kbdus', 'Chinese (Traditional, Macao S.A.R.) - US'],
	['kbdda', 'Danish'],
	['kbdindev', 'Devanagari - INSCRIPT'],
	['kbddiv1', 'Divehi Phonetic'],
	['kbddiv2', 'Divehi Typewriter'],
	['kbdne', 'Dutch'],
	['kbddzo', 'Dzongkha'],
	['kbdinen', 'English (India)'],
	['kbdest', 'Estonian'],
	['kbdfo', 'Faeroese'],
	['kbdfi', 'Finnish'],
	['kbdfi1', 'Finnish with Sami'],
	['kbdfr', 'French (Legacy, AZERTY)'],
	['kbdfrna', 'French (Standard, AZERTY)'],
	['kbdfrnb', 'French (Standard, BÉPO)'],
	['kbdfthrk', 'Futhark'],
	['kbdgeoer', 'Georgian (Ergonomic)'],
	['kbdgeo', 'Georgian (Legacy)'],
	['kbdgeome', 'Georgian (MES)'],
	['kbdgeooa', 'Georgian (Old Alphabets)'],
	['kbdgeoqw', 'Georgian (QWERTY)'],
	['kbdgr', 'German'],
	['kbdgr1', 'German (IBM)'],
	['kbdgre1', 'German Extended (E1)'],
	['kbdgre2', 'German Extended (E2)'],
	['kbdgthc', 'Gothic'],
	['kbdhe', 'Greek'],
	['kbdhe220', 'Greek (220)'],
	['kbdhela2', 'Greek (220) Latin'],
	['kbdhe319', 'Greek (319)'],
	['kbdhela3', 'Greek (319) Latin'],
	['kbdgkl', 'Greek Latin'],
	['kbdhept', 'Greek Polytonic'],
	['kbdgrlnd', 'Greenlandic'],
	['kbdgn', 'Guarani'],
	['kbdinguj', 'Gujarati'],
	['kbdhau', 'Hausa'],
	['kbdhaw', 'Hawaiian'],
	['kbdheb', 'Hebrew'],
	['kbdhebl3', 'Hebrew (Standard)'],
	['kbdhebsi', 'Hebrew (Standard, 2018)'],
	['kbdinhin', 'Hindi Traditional'],
	['kbdhu', 'Hungarian'],
	['kbdhu1', 'Hungarian 101-key'],
	['kbdic', 'Icelandic'],
	['kbdibo', 'Igbo'],
	['kbdiulat', 'Inuktitut - Latin'],
	['kbdinuk2', 'Inuktitut - Naqittaut'],
	['kbdinuk3', 'Inuktitut - Nattilik'],
	['kbdir', 'Irish'],
	['kbdit', 'Italian'],
	['kbdit142', 'Italian (142)'],
	['kbdjpn', 'Japanese'],
	['kbdjav', 'Javanese'],
	['kbdinkan', 'Kannada'],
	['kbdkaz', 'Kazakh'],
	['kbdkhmr', 'Khmer'],
	['kbdkni', 'Khmer (NIDA)'],
	['kbdkor', 'Korean'],
	['kbdkyr', 'Kyrgyz Cyrillic'],
	['kbdlao', 'Lao'],
	['kbdla', 'Latin American'],
	['kbdlv', 'Latvian'],
	['kbdlv1', 'Latvian (QWERTY)'],
	['kbdlvst', 'Latvian (Standard)'],
	['kbdlisub', 'Lisu (Basic)'],
	['kbdlisus', 'Lisu (Standard)'],
	['kbdlt1', 'Lithuanian'],
	['kbdlt', 'Lithuanian IBM'],
	['kbdlt2', 'Lithuanian Standard'],
	['kbdsf', 'Luxembourgish'],
	['kbdmac', 'Macedonian'],
	['kbdmacst', 'Macedonian - Standard'],
	['kbdinmal', 'Malayalam'],
	['kbdmlt47', 'Maltese 47-Key'],
	['kbdmlt48', 'Maltese 48-Key'],
	['kbdmaori', 'Maori'],
	['kbdinmar', 'Marathi'],
	['kbdmonmo', 'Mongolian (Mongolian Script)'],
	['kbdmon', 'Mongolian Cyrillic'],
	['kbdmyan', 'Myanmar (Phonetic order)'],
	['kbdmyan', 'Myanmar (Visual order)'],
	['kbdnko', 'N\'Ko'],
	['kbdnepr', 'Nepali'],
	['kbdntl', 'New Tai Lue'],
	['kbdno', 'Norwegian'],
	['kbdno1', 'Norwegian with Sami'],
	['kbdmaori', 'NZ Aotearoa'],
	['kbdinori', 'Odia'],
	['kbdogham', 'Ogham'],
	['kbdolch', 'Ol Chiki'],
	['kbdoldit', 'Old Italic'],
	['kbdosa', 'Osage'],
	['kbdosm', 'Osmanya'],
	['kbdpash', 'Pashto (Afghanistan)'],
	['kbdfa', 'Persian'],
	['kbdfar', 'Persian (Standard)'],
	['kbdphags', 'Phags-pa'],
	['kbdpl', 'Polish (214)'],
	['kbdpl1', 'Polish (Programmers)'],
	['kbdpo', 'Portuguese'],
	['kbdbr', 'Portuguese (Brazil ABNT)'],
	['kbdbr', 'Portuguese (Brazil ABNT2)'],
	['kbdinpun', 'Punjabi'],
	['kbdro', 'Romanian (Legacy)'],
	['kbdropr', 'Romanian (Programmers)'],
	['kbdrost', 'Romanian (Standard)'],
	['kbdru', 'Russian'],
	['kbdrum', 'Russian - Mnemonic'],
	['kbdru1', 'Russian (Typewriter)'],
	['kbdyak', 'Sakha'],
	['kbdsmsfi', 'Sami Extended Finland-Sweden'],
	['kbdsmsno', 'Sami Extended Norway'],
	['kbdgae', 'Scottish Gaelic'],
	['kbdycc', 'Serbian (Cyrillic)'],
	['kbdycl', 'Serbian (Latin)'],
	['kbdnso', 'Sesotho sa Leboa'],
	['kbdnso', 'Setswana'],
	['kbdsn1', 'Sinhala'],
	['kbdsw09', 'Sinhala - Wij 9'],
	['kbdsl', 'Slovak'],
	['kbdsl1', 'Slovak (QWERTY)'],
	['kbdcr', 'Slovenian'],
	['kbdsora', 'Sora'],
	['kbdsorex', 'Sorbian Extended'],
	['kbdsors1', 'Sorbian Standard'],
	['kbdsorst', 'Sorbian Standard (Legacy)'],
	['kbdsp', 'Spanish'],
	['kbdes', 'Spanish Variation'],
	['kbdcr', 'Standard'],
	['kbdsw', 'Swedish'],
	['kbdfi1', 'Swedish with Sami'],
	['kbdsf', 'Swiss French'],
	['kbdsg', 'Swiss German'],
	['kbdsyr1', 'Syriac'],
	['kbdsyr2', 'Syriac Phonetic'],
	['kbdtaile', 'Tai Le'],
	['kbdtajik', 'Tajik'],
	['kbdintam', 'Tamil'],
	['kbdtam99', 'Tamil 99'],
	['kbdinen', 'Tamil Anjal'],
	['kbdtt102', 'Tatar'],
	['kbdtat', 'Tatar (Legacy)'],
	['kbdintel', 'Telugu'],
	['kbdth0', 'Thai Kedmanee'],
	['kbdth2', 'Thai Kedmanee (non-ShiftLock)'],
	['kbdth1', 'Thai Pattachote'],
	['kbdth3', 'Thai Pattachote (non-ShiftLock)'],
	['kbdtiprc', 'Tibetan (PRC)'],
	['kbdtiprd', 'Tibetan (PRC) - Updated'],
	['kbdtifi', 'Tifinagh (Basic)'],
	['kbdtifi2', 'Tifinagh (Extended)'],
	['kbdmons2', 'Traditional Mongolian (MNS)'],
	['kbdmonst', 'Traditional Mongolian (Standard)'],
	['kbdtuf', 'Turkish F'],
	['kbdtuq', 'Turkish Q'],
	['kbdturme', 'Turkmen'],
	['kbdur', 'Ukrainian'],
	['kbdur1', 'Ukrainian (Enhanced)'],
	['kbduk', 'United Kingdom'],
	['kbdukx', 'United Kingdom Extended'],
	['kbddv', 'United States-Dvorak'],
	['kbdusl', 'United States-Dvorak for left hand'],
	['kbdusr', 'United States-Dvorak for right hand'],
	['kbdusx', 'United States-International'],
	['kbdurdu', 'Urdu'],
	['kbdus', 'US'],
	['kbdusa', 'US English Table for IBM Arabic 238_L'],
	['kbdughr1', 'Uyghur'],
	['kbdughr', 'Uyghur (Legacy)'],
	['kbduzb', 'Uzbek Cyrillic'],
	['kbdvntc', 'Vietnamese'],
	['kbdwol', 'Wolof'],
	['kbdyba', 'Yoruba'],
];

/**
 * @typedef {Object} KeyboardLayout
 * @property {boolean} rightAltIsAltGr
 * @property {boolean} shiftCancelsCapsLock
 * @property {Record<string, number | number[][]>} charMap
 */

/** @type {Map<string, KeyboardLayout>} */
const loadedLayouts = new Map();

/**
 * @param {string} code
 * @param {string} [key]
 * @param {number} [location]
 */
export function jsKeyToHidScanCode(code, key, location) { // TODO: location
	return (code ? CODE_TO_SCAN_CODE.get(code) : undefined) ??
		(key ? CODE_TO_SCAN_CODE.get(key) : undefined) ?? 0;
}

/**
 * @param {KeyboardEvent} event
 */
export function isNonCharInput(event) {
	return event.ctrlKey || event.altKey ||
		NON_CHAR_INPUT_KEYS.has(event.code || event.key || '');
}

/**
 * @param {KeyboardEvent} event
 * @param {KeyboardLayout} [layout]
 */
export function getHidScanCodeFromKeyboardEvent(event, layout) {
	const scanCode = jsKeyToHidScanCode(event.code ?? '', event.key, event.location);
	if (scanCode === 0 && layout && !event.code && event.key.length === 1) {
		const scanCodes = getHidScanCodesFromText(event.key, layout);
		if (scanCodes.length === 1 && scanCodes[0].length === 1) {
			return scanCodes[0][0];
		}
	}

	return scanCode;
}

/**
 * @param {string} text
 * @param {KeyboardLayout} layout
 * @param {number[]} [unsupportedIndices]
 */
export function getHidScanCodesFromText(text, layout, unsupportedIndices) {
	/** @type {number[][]} */
	const scanCodes = [];
	const charMap = layout.charMap;

	for (let i = 0; i < text.length; i++) {
		/** @type {number | number[][]} */
		let charScanCodes;
		if (i + 1 < text.length) {
			charScanCodes = charMap[text.substring(i, i + 2)];
			if (charScanCodes) {
				i++;
			}
		}

		if (!charScanCodes) {
			switch (text[i]) {
				case '\r':
					break;
				case '\n':
					charScanCodes = 40; // Enter
					break;
				default:
					charScanCodes = charMap[text[i]];
					break;
			}
		}

		if (Array.isArray(charScanCodes)) {
			scanCodes.push(...charScanCodes);
		} else if (charScanCodes > 0) {
			scanCodes.push([charScanCodes]);
		} else {
			unsupportedIndices?.push(i);
		}
	}

	return scanCodes;
}

/**
 * @param {string} sequence
 * @param {Set<number>} downKeys
 * @param {number} [delay]
 */
export function createKeyboardSequenceEvents(sequence, downKeys, delay) {
	/** @type {KvmKeyEvent[]} */
	const events = [];
	delay ??= undefined;

	for (const group of sequence.split(',')) {
		/** @type {[number, string][]} */
		const scanCodes = group.split('+').map(key => {
			const parts = key.split(':');
			const scanCode = jsKeyToHidScanCode(parts[0]);
			if (!key || scanCode === 0) {
				throw new Error('Invalid key sequence');
			}

			return [scanCode, parts[1] ?? ''];
		});

		for (let i = 0; i < scanCodes.length; i++) {
			const arg = scanCodes[i][1];
			if (!arg || arg === 'down') {
				events.push({
					scanCode: scanCodes[i][0],
					isDown: true,
					delay,
				});
			} else if (arg === 'toggle') {
				events.push({
					scanCode: scanCodes[i][0],
					isDown: !downKeys.has(scanCodes[i][0]),
					delay,
				});
			}
		}

		for (let i = scanCodes.length - 1; i >= 0; i--) {
			const arg = scanCodes[i][1];
			if (!arg || arg === 'up') {
				events.push({
					scanCode: scanCodes[i][0],
					isDown: false,
					delay,
				});
			}
		}
	}

	return events;
}

/**
 * @param {string} id
 */
export function getLayout(id) {
	return loadedLayouts.get(id);
}

/**
 * @param {string} id
 */
export async function loadLayout(id) {
	let layout = loadedLayouts.get(id);
	if (!layout) {
		try {
			layout = await fetchLayout();
		} finally {
			if (!loadedLayouts.has(id)) {
				loadedLayouts.set(id, layout);
			}
		}
	}

	return layout;

	async function fetchLayout() {
		/** @type {KeyboardLayout} */
		const layout = (await import(`./kbd/${id}.json`, { with: { type: 'json' } }))?.default;
		if (isObject(layout) && isObject(layout.charMap) && Object.keys(layout.charMap).length > 0) {
			return layout;
		} else {
			throw new Error('Invalid layout');
		}
	}

	function isObject(value) {
		return value && typeof value === 'object' && !Array.isArray(value);
	}
}
