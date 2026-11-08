// Node harness: runs the preview page's own script against a stubbed DOM,
// then drives the controls and checks the direction maths + session bookkeeping.
// Usage:  node preview/tests/check-preview.mjs
import fs from 'node:fs';

const html = fs.readFileSync(new URL('../plant-draw-preview.html', import.meta.url), 'utf8');

// ---- build a minimal DOM stub from the real markup -------------------------
const ctx = {
  createLinearGradient: () => ({ addColorStop() {} }),
  clearRect(){}, fillRect(){}, beginPath(){}, arc(){}, fill(){}, stroke(){},
  moveTo(){}, lineTo(){}, closePath(){}, fillText(){}, setLineDash(){}, measureText: () => ({ width: 10 })
};

function makeEl(id) {
  return {
    id, tagName: 'DIV', value: '', className: '', textContent: '', attrs: {},
    style: {}, listeners: {},
    width: 720, height: 720,
    addEventListener(type, fn) { (this.listeners[type] = this.listeners[type] || []).push(fn); },
    getAttribute(n) { return this.attrs[n] !== undefined ? this.attrs[n] : null; },
    setAttribute(n, v) { this.attrs[n] = v; },
    classList: { toggle() {}, add() {}, remove() {} },
    getBoundingClientRect() { return { left: 0, top: 0, width: 720, height: 720 }; },
    getContext() { return ctx; }
  };
}

const elements = {};
function byId(id) {
  if (!elements[id]) elements[id] = makeEl(id);
  return elements[id];
}

// buttons from the markup, so handlers map to real data-* values
const dataButtons = [];
for (const m of html.matchAll(/data-(act|frame|axis|quick)="([^"]+)"/g)) {
  const b = makeEl('btn-' + m[1] + '-' + m[2]);
  b.tagName = 'BUTTON';
  b.attrs['data-' + m[1]] = m[2];
  dataButtons.push(b);
}

const document = {
  getElementById: byId,
  querySelectorAll(sel) {
    const m = sel.match(/\[data-(\w+)\]/);
    return m ? dataButtons.filter(b => b.attrs['data-' + m[1]] !== undefined) : [];
  },
  createElement: () => makeEl('created')
};

const capturedWindow = { listeners: {} };
const window = {
  addEventListener(type, fn) { (capturedWindow.listeners[type] = capturedWindow.listeners[type] || []).push(fn); }
};

byId('distance').value = '1000';
byId('units').value = 'mm';
byId('view').width = 900; byId('view').height = 720;

// ---- run the page script ---------------------------------------------------
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
new Function('document', 'window', 'requestAnimationFrame', script)(document, window, fn => fn());

// ---- assertions -----------------------------------------------------------
let failures = 0;
function check(name, actual, expected) {
  const ok = String(actual) === String(expected);
  if (!ok) { failures++; console.log('FAIL', name, '->', JSON.stringify(actual), 'expected', JSON.stringify(expected)); }
  else { console.log('ok  ', name, '=', actual); }
}
function contains(name, actual, needle) {
  const ok = String(actual).includes(needle);
  if (!ok) { failures++; console.log('FAIL', name, '->', JSON.stringify(actual), 'should contain', JSON.stringify(needle)); }
  else { console.log('ok  ', name, '=', actual); }
}

console.log('--- initial state (N, 1000 mm, project north) ---');
check('direction', byId('r-direction').textContent, 'North (+Y)');
check('bearing/rise', byId('r-bearing').textContent, '0.0 deg  /  +0.0 deg');
check('delta', byId('r-delta').textContent, 'E 0  N 1000  Z 0 mm');
check('next point', byId('r-next').textContent, 'E 0  N 1000  Z 0');

console.log('--- draw one segment north ---');
const draw = byId('draw');
draw.listeners.click[0]({});
check('session', byId('r-session').textContent, '1 segment(s), 1000.0 mm');
check('anchor advanced', byId('r-anchor').textContent, 'E 0  N 1000  Z 0');
contains('status', byId('status').textContent, 'Segment drawn');

console.log('--- press E (east) and draw ---');
capturedWindow.listeners.keydown[0]({ key: 'e', target: { tagName: 'BODY' }, preventDefault() {} });
check('direction', byId('r-direction').textContent, 'East (+X)');
draw.listeners.click[0]({});
check('session', byId('r-session').textContent, '2 segment(s), 2000.0 mm');
check('anchor advanced', byId('r-anchor').textContent, 'E 1000  N 1000  Z 0');

console.log('--- axis lock Z via keyboard then draw ---');
capturedWindow.listeners.keydown[0]({ key: 'z', target: { tagName: 'BODY' }, preventDefault() {} });
check('direction', byId('r-direction').textContent, 'Locked +Z');
draw.listeners.click[0]({});
check('session', byId('r-session').textContent, '3 segment(s), 3000.0 mm');
check('anchor z', byId('r-anchor').textContent, 'E 1000  N 1000  Z 1000');

console.log('--- undo twice ---');
const undo = dataButtons.find(b => b.attrs['data-act'] === 'undo');
undo.listeners.click[0]({});
undo.listeners.click[0]({});
check('session after undo', byId('r-session').textContent, '1 segment(s), 1000.0 mm');
check('anchor after undo', byId('r-anchor').textContent, 'E 0  N 1000  Z 0');

console.log('--- bearing 300 deg + elevation 30 ---');
capturedWindow.listeners.keydown[0]({ key: 'z', target: { tagName: 'BODY' }, preventDefault() {} }); // unlock Z
byId('bearing').value = '300';
dataButtons.find(b => b.attrs['data-act'] === 'apply-bearing').listeners.click[0]({});
byId('elevation').value = '30';
dataButtons.find(b => b.attrs['data-act'] === 'apply-elevation').listeners.click[0]({});
contains('direction', byId('r-direction').textContent, 'N 60.0 deg W, 30.0 deg up');
contains('bearing', byId('r-bearing').textContent, '300.0 deg');
contains('bearing', byId('r-bearing').textContent, '+30.0 deg');
console.log('   delta:', byId('r-delta').textContent);

console.log('--- ortho snap ---');
byId('bearing').value = '';
dataButtons.find(b => b.attrs['data-act'] === 'clear-bearing').listeners.click[0]({});
dataButtons.find(b => b.attrs['data-act'] === 'clear-elevation').listeners.click[0]({});
byId('ortho').listeners.click[0]({}); // ortho on
byId('r-direction'); // no-op
const neSectorIndex = 1;
// click the NE sector on the compass: bearing 45 deg, at radius (inner+outer)/2
const compass = byId('compass');
const cx = 360, cy = 360, outer = 720 / 2 - 14, inner = outer * 0.63;
const r = (inner + outer) / 2;
const theta = (45 - 90) * Math.PI / 180;
compass.listeners.click[0]({ clientX: cx + r * Math.cos(theta), clientY: cy + r * Math.sin(theta) });
check('ortho snaps NE to East', byId('r-direction').textContent, 'East (+X)');
contains('status after draw', (draw.listeners.click[0]({}), byId('status').textContent), 'Ortho snapped 45 deg to 90 deg');

console.log('--- true north frame (20 deg) ---');
dataButtons.find(b => b.attrs['data-frame'] === 'true').listeners.click[0]({});
console.log('   direction:', byId('r-direction').textContent);
check('true north label', byId('r-direction').textContent, 'East');

console.log('--- axis lock label in rotated frame ---');
capturedWindow.listeners.keydown[0]({ key: 'x', target: { tagName: 'BODY' }, preventDefault() {} });
check('lock label', byId('r-direction').textContent, 'Locked +X');
capturedWindow.listeners.keydown[0]({ key: 'x', target: { tagName: 'BODY' }, preventDefault() {} });
dataButtons.find(b => b.attrs['data-frame'] === 'project').listeners.click[0]({});

console.log('--- clear all ---');
dataButtons.find(b => b.attrs['data-act'] === 'clear').listeners.click[0]({});
check('session cleared', byId('r-session').textContent, '0 segment(s), 0.0 mm');

console.log(failures === 0 ? '\nALL PREVIEW TESTS PASSED' : '\n' + failures + ' FAILURE(S)');
process.exit(failures ? 1 : 0);
