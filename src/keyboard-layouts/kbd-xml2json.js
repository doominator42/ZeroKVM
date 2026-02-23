#!/usr/bin/node

/*
Input files: 'XML for processing' from https://kbdlayout.info/
Usage:
  node ./kbd-xml2json.js -s -o kbdname.xml > ../ZeroKvm/wwwroot/kvm/kbd/kbdname.json
  node ./kbd-xml2json.js -s -p "~/Downloads/*.xml" && cp "~/Downloads/*.json" ../ZeroKvm/wwwroot/kvm/kbd/
*/

/**
 * @typedef {Object} LayoutKey
 * @property {string} vk
 * @property {number} sc
 * @property {string} text
 * @property {string[]} [modifiers]
 * @property {string} [deadKeyText]
 */

import process from 'node:process';
import { promises as fs } from 'fs';
import { jsKeyToHidScanCode } from '../ZeroKvm/wwwroot/kvm/keyboard-keys.js';

const VK_TO_JS_CODE = new Map([
  ['VK_MENU', 'AltLeft'],
  ['VK_LMENU', 'AltLeft'],
  ['VK_RMENU', 'AltRight'],
  ['VK_OEM_4', 'BracketLeft'],
  ['VK_OEM_6', 'BracketRight'],
  ['VK_CAPITAL', 'CapsLock'],
  ['VK_OEM_COMMA', 'Comma'],
  ['VK_CONTROL', 'ControlLeft'],
  ['VK_LCONTROL', 'ControlLeft'],
  ['VK_RCONTROL', 'ControlRight'],
  ['VK_OEM_8', 'ControlRight'],
  ['VK_DELETE', 'Delete'],
  ['VK_0', 'Digit0'],
  ['VK_1', 'Digit1'],
  ['VK_2', 'Digit2'],
  ['VK_3', 'Digit3'],
  ['VK_4', 'Digit4'],
  ['VK_5', 'Digit5'],
  ['VK_6', 'Digit6'],
  ['VK_7', 'Digit7'],
  ['VK_8', 'Digit8'],
  ['VK_9', 'Digit9'],
  ['VK_OEM_PLUS', 'Equal'],
  ['VK_RETURN', 'Enter'],
  ['VK_OEM_102', 'IntlBackslash'],
  ['VK_A', 'KeyA'],
  ['VK_B', 'KeyB'],
  ['VK_C', 'KeyC'],
  ['VK_D', 'KeyD'],
  ['VK_E', 'KeyE'],
  ['VK_F', 'KeyF'],
  ['VK_G', 'KeyG'],
  ['VK_H', 'KeyH'],
  ['VK_I', 'KeyI'],
  ['VK_J', 'KeyJ'],
  ['VK_K', 'KeyK'],
  ['VK_L', 'KeyL'],
  ['VK_M', 'KeyM'],
  ['VK_N', 'KeyN'],
  ['VK_O', 'KeyO'],
  ['VK_P', 'KeyP'],
  ['VK_Q', 'KeyQ'],
  ['VK_R', 'KeyR'],
  ['VK_S', 'KeyS'],
  ['VK_T', 'KeyT'],
  ['VK_U', 'KeyU'],
  ['VK_V', 'KeyV'],
  ['VK_W', 'KeyW'],
  ['VK_X', 'KeyX'],
  ['VK_Y', 'KeyY'],
  ['VK_Z', 'KeyZ'],
  ['VK_OEM_MINUS', 'Minus'],
  ['VK_OEM_7', 'Quote'],
  ['VK_OEM_PERIOD', 'Period'],
  ['VK_OEM_1', 'Semicolon'],
  ['VK_SHIFT', 'ShiftLeft'],
  ['VK_LSHIFT', 'ShiftLeft'],
  ['VK_RSHIFT', 'ShiftRight'],
  ['VK_OEM_2', 'Slash'],
  ['VK_SPACE', 'Space'],
  ['VK_TAB', 'Tab'],
]);

const SC_TO_JS_CODE = new Map([
  [0x29, 'Backquote'],
  [0x2b, 'Backslash'],
  [0x1a, 'BracketLeft'],
  [0x1b, 'BracketRight'],
  [0x33, 'Comma'],
  [0x0b, 'Digit0'],
  [0x02, 'Digit1'],
  [0x03, 'Digit2'],
  [0x04, 'Digit3'],
  [0x05, 'Digit4'],
  [0x06, 'Digit5'],
  [0x07, 'Digit6'],
  [0x08, 'Digit7'],
  [0x09, 'Digit8'],
  [0x0a, 'Digit9'],
  [0x0d, 'Equal'],
  [0x1c, 'Enter'],
  [0x56, 'IntlBackslash'],
  [0x1e, 'KeyA'],
  [0x30, 'KeyB'],
  [0x2e, 'KeyC'],
  [0x20, 'KeyD'],
  [0x12, 'KeyE'],
  [0x21, 'KeyF'],
  [0x22, 'KeyG'],
  [0x23, 'KeyH'],
  [0x17, 'KeyI'],
  [0x24, 'KeyJ'],
  [0x25, 'KeyK'],
  [0x26, 'KeyL'],
  [0x32, 'KeyM'],
  [0x31, 'KeyN'],
  [0x18, 'KeyO'],
  [0x19, 'KeyP'],
  [0x10, 'KeyQ'],
  [0x13, 'KeyR'],
  [0x1f, 'KeyS'],
  [0x14, 'KeyT'],
  [0x16, 'KeyU'],
  [0x2f, 'KeyV'],
  [0x11, 'KeyW'],
  [0x2d, 'KeyX'],
  [0x15, 'KeyY'],
  [0x2c, 'KeyZ'],
  [0x0c, 'Minus'],
  [0x28, 'Quote'],
  [0x34, 'Period'],
  [0x27, 'Semicolon'],
  [0x35, 'Slash'],
  [0x39, 'Space'],
  [0x0f, 'Tab'],
]);

const SC_NUMPAD = new Set([
  0xe035, // Slash
  0x37, // Multiply
  0x4a, // Subtract
  0x47, // 7
  0x48, // 8
  0x49, // 9
  0x4b, // 4
  0x4c, // 5
  0x4d, // 6
  0x4f, // 1
  0x50, // 2
  0x51, // 3
  0x52, // 0
  0x53, // Period
  0xe01c, // Enter
  0x4e, // Add
]);

const EXCLUDED_CHARS = new Set([
  '',
  '\r',
  '\n',
  '\t',
  '\b', // Backspace
  '\u007f', // Delete
  '\u001b', // Escape
  '\u0003', // Cancel
]);

let outputScanCodes = false;
let toStdOut = false;
let printFileNames = false;
for (const arg of process.argv.slice(2)) {
  if (arg === '-s') {
    outputScanCodes = true;
    continue;
  } else if (arg === '-o') {
    toStdOut = true;
    continue;
  } else if (arg === '-p') {
    printFileNames = true;
    continue;
  }

  if (printFileNames) {
    console.error('%s', arg);
  }

  const xml = await fs.readFile(arg, { encoding: 'utf-8' });
  const layoutElement = parseXml(xml)[0];
  if (!layoutElement) {
    throw new Error('XML is empty');
  }

  const layoutKeys = getLayoutKeyMaps(layoutElement);
  const rightAltIsAltGr = layoutElement.attrs['RightAltIsAltGr'] === 'true';
  const shiftCancelsCapsLock = layoutElement.attrs['ShiftCancelsCapsLock'] === 'true';

  /** @type {Record<string, string | number | number[][]>} */
  const charMap = {};
  for (const key of layoutKeys) {
    if (EXCLUDED_CHARS.has(key.text) || key.modifiers?.includes('VK_CAPITAL')) {
      continue;
    }

    if (!charMap[key.text]) {
      const sequence = getKeySequence(key, layoutKeys, rightAltIsAltGr);
      //console.log('%s %s', JSON.stringify(key.text), sequence);
      charMap[key.text] = sequence ? outputScanCodes ? resolveHidScanCodes(sequence) : sequence : undefined;
    }
  }

  const kbdJson = JSON.stringify({
    rightAltIsAltGr,
    shiftCancelsCapsLock,
    charMap,
  });

  if (toStdOut) {
    console.log('%s', kbdJson);
  } else {
    await fs.writeFile(arg.replace(/\.xml$/i, '') + '.json', kbdJson, { encoding: 'utf-8' });
  }
}

/**
 * @param {string} sequence
 */
function resolveHidScanCodes(sequence) {
  const scanCodes = sequence.split(',').map(seq => {
    return seq.split('+').map(key => {
      const scanCode = jsKeyToHidScanCode(key);
      if (scanCode === 0) {
        throw new Error('No scan code defined for key ' + key);
      }

      return scanCode;
    });
  });

  return scanCodes.length === 1 && scanCodes[0].length === 1 ? scanCodes[0][0] : scanCodes;
}

/**
 * @param {LayoutKey} key
 * @param {LayoutKey[]} allKeys
 * @param {boolean} rightAltIsAltGr
 */
function getKeySequence(key, allKeys, rightAltIsAltGr) {
  const baseKey = SC_TO_JS_CODE.get(key.sc);
  if (!baseKey) {
    return undefined;
  }

  let sequence = '';
  for (const modifier of (key.modifiers ?? [])) {
    if (rightAltIsAltGr && modifier === 'VK_CONTROL' && key.modifiers.includes('VK_MENU')) {
      continue;
    }

    const modifierKey = rightAltIsAltGr && modifier === 'VK_MENU' && key.modifiers.includes('VK_CONTROL') ? 'AltRight' : VK_TO_JS_CODE.get(modifier);
    if (!modifierKey) {
      return undefined;
    }

    sequence += modifierKey + '+';
  }

  sequence += baseKey;
  if (key.deadKeyText) {
    const nextKeys = allKeys.filter(k => !k.deadKeyText && k.text === key.deadKeyText);
    if (nextKeys.length === 0) {
      //throw new Error('No key sequence for \'' + key.deadKeyText + '\'');
      console.error('No key sequence for \'%s\'', key.deadKeyText);
      return undefined;
    }

    const nextSequence = getKeySequence(nextKeys[0], [], rightAltIsAltGr);
    if (!nextSequence) {
      throw new Error('Invalid dead key: ' + JSON.stringify(nextKeys[0]));
    }

    sequence += ',' + nextSequence;
  }

  return sequence;
}

/**
 * @param {XmlElement} layoutElement
 * @returns {LayoutKey[]}
 */
function getLayoutKeyMaps(layoutElement) {
  if (layoutElement.name !== 'KeyboardLayout' || layoutElement.children[0]?.name !== 'PhysicalKeys') {
    throw new Error('Invalid XML schema, expected <KeyboardLayout><PhysicalKeys>');
  }

  const maps = [];
  for (const pkElement of layoutElement.children[0].children) {
    if (pkElement.name !== 'PK') {
      throw new Error('Invalid XML schema, expected <PK>');
    }

    const vk = pkElement.attrs['VK'];
    const sc = parseInt(pkElement.attrs['SC'] || '0', 16);

    if (vk && sc > 0 && !SC_NUMPAD.has(sc)) {
      for (const resultTag of pkElement.children) {
        if (resultTag.name !== 'Result') {
          throw new Error('Invalid XML schema, expected <Result>');
        }

        const text = getTextAttr(resultTag);
        const modifiers = (resultTag.attrs['With'] || undefined)?.split(' ');

        if (text) {
          maps.push({
            vk,
            sc,
            text,
            modifiers,
          });
        }

        for (const deadKeyTableTag of resultTag.children) {
          if (deadKeyTableTag.name !== 'DeadKeyTable') {
            throw new Error('Invalid XML schema, expected <DeadKeyTable>');
          }

          for (const resultTag of deadKeyTableTag.children) {
            if (resultTag.name !== 'Result') {
              throw new Error('Invalid XML schema, expected <Result>');
            }

            const text = getTextAttr(resultTag);
            const deadKeyText = resultTag.attrs['With'];

            if (text && deadKeyText) {
              maps.push({
                vk,
                sc,
                text,
                modifiers,
                deadKeyText,
              });
            }
          }
        }
      }
    }
  }

  maps.sort((a, b) => {
    if (a.deadKeyText && !b.deadKeyText) {
      return 1;
    } else if (!a.deadKeyText && b.deadKeyText) {
      return -1;
    } else if ((a.modifiers?.length ?? 0) > (b.modifiers?.length ?? 0)) {
      return 1
    } else if ((a.modifiers?.length ?? 0) < (b.modifiers?.length ?? 0)) {
      return -1;
    } else {
      return a.text === b.text ? 0 : a.text > b.text ? 1 : -1;
    }
  });

  return maps;

  /**
   * @param {XmlElement} element
   */
  function getTextAttr(element) {
    const text = element.attrs['Text'] || fromCodePoints(element.attrs['TextCodepoints']);
    if (text === '') {
      throw new Error('Invalid text: \'' + text + '\'');
    }

    return text;
  }

  /**
   * @param {string} attrs
   */
  function getWithAttr(attrs) {
    return decodeEntities(WITH_ATTR_REGEX.exec(attrs)?.[1] ?? '') || undefined;
  }

  /**
   * @param {string | undefined} codepoints
   */
  function fromCodePoints(codepoints) {
    if (!codepoints) {
      return undefined;
    }

    const code = parseInt(codepoints, 16);
    return code > 0 ? String.fromCharCode(code) : undefined;
  }

  /**
   * @param {string} str
   */
  function decodeEntities(str) {
    const ENTITY_REGEX = /&([a-z]+|#[0-9]+);/g;

    return str
      .replace(ENTITY_REGEX, (_, entity) => {
        switch (entity) {
          case 'amp':
            return '&';
          case 'lt':
            return '<';
          case 'gt':
            return '>';
          case 'apos':
            return '\'';
          case 'quot':
            return '"';
          default:
            if (entity[0] === '#') {
              return String.fromCharCode(parseInt(entity.slice(1), 10));
            }

            throw new Error('Invalid entity: &' + entity + ';');
        }
      })
      .replace(/&#(\d+);/gi, (match, numStr) => String.fromCharCode(parseInt(numStr, 10)));
  }
}

/**
 * @typedef {Object} XmlElement
 * @property {string} name
 * @property {Record<string, string>} attrs
 * @property {XmlElement[]} children
 */

/**
 * @param {string} xml
 * @param {{ consumed: number }} state
 */
function parseXml(xml, state) {
  const TAG_START_REGEX = /^<(\w+)(\s[^<>]*|)>/s;
  const TAG_END_REGEX = /^<\/(\w+)>/s;
  const ATTR_REGEX = /^(\w+)="([^"]*)"/s;

  /** @type {XmlElement[]} */
  const elements = [];

  const initialXmlLength = xml.length;
  xml = xml.trim();
  while (xml) {
    const tagEnd = TAG_END_REGEX.exec(xml);
    if (tagEnd) {
      break;
    }

    const tagStart = TAG_START_REGEX.exec(xml);
    if (!tagStart) {
      throw new Error('Invalid XML at: ' + xml.substring(0, 50));
    }

    const attrs = parseAttrs(tagStart[2].endsWith('/') ? tagStart[2].slice(0, -1) : tagStart[2]);
    xml = xml.slice(tagStart[0].length).trimStart();

    const children = [];
    elements.push({
      name: tagStart[1],
      attrs,
      children,
    });

    if (!tagStart[2].endsWith('/')) {
      state ??= { consumed: 0 };
      children.push(...parseXml(xml, state));
      xml = xml.slice(state.consumed).trimStart();

      const tagEnd = TAG_END_REGEX.exec(xml);
      if (!tagEnd || tagEnd[1] !== tagStart[1]) {
        throw new Error('Missing closing tag ' + tagStart[1] + ' at: ' + xml.substring(0, 50));
      }

      xml = xml.slice(tagEnd[0].length).trimStart();
    }
  }

  if (state) {
    state.consumed = initialXmlLength - xml.length;
  }

  return elements;

  /**
   * @param {string} xmlAttrs
   */
  function parseAttrs(xmlAttrs) {
    /** @type {Record<string, string>} */
    const attrs = {};

    xmlAttrs = xmlAttrs.trim();
    while (xmlAttrs) {
      const attr = ATTR_REGEX.exec(xmlAttrs);
      if (!attr) {
        throw new Error('Invalid XML attribute at: ' + xmlAttrs.substring(0, 50));
      }

      attrs[attr[1]] = decodeEntities(attr[2]);
      xmlAttrs = xmlAttrs.slice(attr[0].length).trim();
    }

    return attrs;
  }

  /**
   * @param {string} str
   */
  function decodeEntities(str) {
    const ENTITY_REGEX = /&([a-z]+|#[0-9]+);/g;

    return str
      .replace(ENTITY_REGEX, (_, entity) => {
        switch (entity) {
          case 'amp':
            return '&';
          case 'lt':
            return '<';
          case 'gt':
            return '>';
          case 'apos':
            return '\'';
          case 'quot':
            return '"';
          default:
            if (entity[0] === '#') {
              return String.fromCharCode(parseInt(entity.slice(1), 10));
            }

            throw new Error('Invalid entity: &' + entity + ';');
        }
      })
      .replace(/&#(\d+);/gi, (match, numStr) => String.fromCharCode(parseInt(numStr, 10)));
  }
}
