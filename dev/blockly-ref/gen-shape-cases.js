// What shape does a block take after dropdown fields are changed? (Real Blockly, headless.)
// The C# FieldEditing + mutators must give the same named inputs and the same output/previous/next connections.
const fs = require('fs');
const path = require('path');
const { Blockly } = require('./lib');

const ws = new Blockly.Workspace();
const out = [];

function shape(b) {
  return {
    inputs: b.inputList.filter(i => i.connection).map(i => i.name).filter(n => n).sort(),   // only inputs something can be plugged into (a hidden AT is a dummy input)
    output: !!b.outputConnection, previous: !!b.previousConnection, next: !!b.nextConnection,
  };
}
function record(type, steps) {
  const b = ws.newBlock(type);
  const done = [];
  for (const [field, value] of steps) {
    b.setFieldValue(value, field);
    done.push([field, value]);
  }
  out.push({ type, steps: done, ...shape(b) });
  b.dispose();
}

const WHERE = ['FROM_START', 'FROM_END', 'FIRST', 'LAST', 'RANDOM'];
for (const w of WHERE) record('lists_getIndex', [['WHERE', w]]);
for (const m of ['GET', 'GET_REMOVE', 'REMOVE']) record('lists_getIndex', [['MODE', m]]);
record('lists_getIndex', [['MODE', 'REMOVE'], ['WHERE', 'FIRST']]);
record('lists_getIndex', [['MODE', 'REMOVE'], ['WHERE', 'FROM_END']]);
record('lists_getIndex', [['MODE', 'REMOVE'], ['MODE', 'GET']]);
record('lists_getIndex', [['WHERE', 'LAST'], ['MODE', 'REMOVE'], ['WHERE', 'FROM_START'], ['MODE', 'GET_REMOVE']]);
for (const w of WHERE) record('lists_setIndex', [['WHERE', w]]);
record('lists_setIndex', [['MODE', 'INSERT'], ['WHERE', 'LAST']]);
for (const w of ['FROM_START', 'FROM_END', 'FIRST', 'LAST', 'RANDOM']) record('text_charAt', [['WHERE', w]]);
for (const w1 of ['FROM_START', 'FROM_END', 'FIRST'])
  for (const w2 of ['FROM_START', 'FROM_END', 'LAST']) {
    record('text_getSubstring', [['WHERE1', w1], ['WHERE2', w2]]);
    record('lists_getSublist', [['WHERE1', w1], ['WHERE2', w2]]);
  }
for (const p of ['EVEN', 'ODD', 'PRIME', 'WHOLE', 'POSITIVE', 'NEGATIVE', 'DIVISIBLE_BY']) record('math_number_property', [['PROPERTY', p]]);
record('math_number_property', [['PROPERTY', 'DIVISIBLE_BY'], ['PROPERTY', 'EVEN']]);
for (const m of ['SPLIT', 'JOIN']) record('lists_split', [['MODE', m]]);
for (const m of ['SPLIT', 'JOIN']) record('math_on_list', [['OP', m === 'SPLIT' ? 'SUM' : 'RANDOM']]);

const file = path.resolve(__dirname, '../../Packages/com.b4u.blockly/Tests/EditMode/Fixtures/block-shapes.cases.json');
fs.writeFileSync(file, '[\n' + out.map(o => '  ' + JSON.stringify(o)).join(',\n') + '\n]\n');
console.log('wrote', out.length, 'cases');
for (const o of out.slice(0, 12)) console.log(JSON.stringify(o.steps).padEnd(48), o.inputs.join(','), 'out=' + o.output, 'prev=' + o.previous, 'next=' + o.next);
