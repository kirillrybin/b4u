// For each program: save real-Blockly JSON, keep the XML, and record the output of the JS that
// Blockly's own generator produces. The C# tests must reproduce this output exactly.
const fs = require('fs');
const path = require('path');
const { Blockly, javascriptGenerator } = require('./lib');
const programs = require('./programs');

const outDir = path.resolve(__dirname, '../../Packages/com.b4u.blockly/Tests/EditMode/Fixtures/programs');
fs.mkdirSync(outDir, { recursive: true });

for (const [name, xml] of Object.entries(programs)) {
  const ws = new Blockly.Workspace();
  Blockly.Xml.domToWorkspace(Blockly.utils.xml.textToDom(xml), ws);
  const json = Blockly.serialization.workspaces.save(ws);

  javascriptGenerator.init(ws);
  const code = javascriptGenerator.workspaceToCode(ws);
  const lines = [];
  const window = { alert: s => lines.push(String(s)) };
  new Function('window', code)(window);

  fs.writeFileSync(path.join(outDir, name + '.xml'), xml + '\n');
  fs.writeFileSync(path.join(outDir, name + '.json'), JSON.stringify(json, null, 1) + '\n');
  fs.writeFileSync(path.join(outDir, name + '.expected.txt'), lines.join('\n') + '\n');
  fs.writeFileSync(path.join(outDir, name + '.js'), code);
  console.log(name.padEnd(16), lines.length, 'lines');
}
