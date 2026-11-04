// Records what the real Blockly FieldNumber does with typed input, for several min/max/precision settings.
// The C# FieldEditing must produce the same results (see FieldEditingTests).
const fs = require('fs');
const path = require('path');
const { Blockly } = require('./lib');

const configs = [
  { min: null, max: null, precision: 0 },
  { min: 0, max: 100, precision: 1 },
  { min: -10, max: 10, precision: 0.5 },
  { min: 0, max: 1, precision: 0.01 },
  { min: null, max: null, precision: 0.1 },
  { min: 0, max: null, precision: 1 },
];
const inputs = ['0', '42', '-3.5', '1e3', '  7  ', '', 'abc', '12abc', '1,000', 'Infinity', '-infinity', 'NaN', '.5', '5.', '0x10',
  '1e400', '-0', '0.1', '0.15', '999999', '2.675', '3.14159', '-', '+5', '1.5e-3', '007', '1 2', '１２'];

const num = (v) => (v === null ? undefined : v);
const out = [];
for (const c of configs) {
  const lo = c.min === null ? -1000 : c.min, hi = c.max === null ? 1000 : c.max;
  for (const input of inputs) {
    const a = new Blockly.FieldNumber(lo, num(c.min), num(c.max), c.precision || undefined);
    const b = new Blockly.FieldNumber(hi, num(c.min), num(c.max), c.precision || undefined);
    a.setValue(input); b.setValue(input);
    const valid = a.getValue() !== lo || b.getValue() !== hi;
    out.push({ min: c.min, max: c.max, precision: c.precision, input, valid, value: valid ? String(a.getValue()) : null });
  }
}
const file = path.resolve(__dirname, '../../Packages/com.b4u.blockly/Tests/EditMode/Fixtures/field-number.cases.json');
fs.writeFileSync(file, '[\n' + out.map(o => '  ' + JSON.stringify(o)).join(',\n') + '\n]\n');
console.log('wrote', out.length, 'cases');
const odd = out.filter(o => o.precision === 0 && o.min === null);
console.log(odd.map(o => JSON.stringify(o.input) + ' -> ' + (o.valid ? o.value : 'INVALID')).join('\n'));
