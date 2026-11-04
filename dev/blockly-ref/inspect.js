const { Blockly } = require('./lib');
const ws = new Blockly.Workspace();
for (const t of process.argv.slice(2)) {
  const b = ws.newBlock(t);
  console.log('==', t, 'colour', b.getColour(), 'inline', b.getInputsInline(), 'out', b.outputConnection && b.outputConnection.getCheck(),
    'prev', !!b.previousConnection, 'next', !!b.nextConnection);
  for (const i of b.inputList) {
    console.log('  input', i.name, 'type', i.type, 'check', i.connection && i.connection.getCheck(),
      i.fieldRow.map(f => `${f.constructor.name}:${f.name}=${JSON.stringify(f.getValue())}`).join(' | '));
  }
}
