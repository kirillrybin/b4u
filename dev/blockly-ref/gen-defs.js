// Generates Runtime/Resources/Blockly/StandardBlocks.json from the real Blockly block definitions,
// so field names, dropdown values, type checks and colours match upstream exactly.
const fs = require('fs');
const path = require('path');
const { Blockly } = require('./lib');

const TYPES = `logic_boolean logic_null logic_negate logic_ternary logic_operation logic_compare
controls_if controls_repeat_ext controls_repeat controls_whileUntil controls_for controls_forEach controls_flow_statements
math_number math_arithmetic math_single math_trig math_constant math_number_property math_round math_modulo math_constrain
math_random_int math_random_float math_on_list math_change
text text_join text_append text_length text_isEmpty text_indexOf text_changeCase text_trim text_replace text_reverse text_print
lists_create_empty lists_create_with lists_repeat lists_length lists_isEmpty lists_getIndex lists_setIndex
variables_get variables_set
text_charAt text_getSubstring text_count lists_indexOf lists_getSublist lists_split lists_sort lists_reverse math_atan2`.split(/\s+/);

// Blocks whose shape is controlled by a mutator on our side.
const MUTATORS = {
  controls_if: 'controls_if_mutator',
  text_join: 'text_join_mutator',
  lists_create_with: 'lists_create_with_mutator',
  math_number_property: 'math_number_property_mutator',
  lists_setIndex: 'lists_setIndex_mutator',
  lists_getIndex: 'lists_getIndex_mutator',
  text_charAt: 'text_charAt_mutator',
  text_getSubstring: 'text_getSubstring_mutator',
  lists_getSublist: 'lists_getSublist_mutator',
  lists_split: 'lists_split_mutator',
};
const DROP_ALL_INPUTS = new Set(['text_join', 'lists_create_with']);
// Inputs that a mutator adds/removes, per block (everything else, e.g. math_modulo's DIVISOR, is permanent).
const OPTIONAL_INPUTS = {
  lists_getIndex: ['AT'], lists_setIndex: ['AT'], math_number_property: ['DIVISOR'],
  text_charAt: ['AT'], text_getSubstring: ['AT1', 'AT2'], lists_getSublist: ['AT1', 'AT2'],
};

const ws = new Blockly.Workspace();
const style = (b) => {
  const s = Blockly.Themes.Classic.blockStyles[b.getStyleName()];
  if (!s) throw new Error('no style for ' + b.type + ' ' + b.getStyleName());
  return s.colourPrimary;
};

function fieldArg(f) {
  const name = f.name || undefined;
  if (f instanceof Blockly.FieldLabel) {
    return name && f.SERIALIZABLE ? { type: 'field_label_serializable', name, text: f.getText() } : { type: 'field_label', text: f.getText() };
  }
  if (f instanceof Blockly.FieldVariable) return { type: 'field_variable', name, variable: f.getText() };
  if (f instanceof Blockly.FieldDropdown)
    return { type: 'field_dropdown', name, options: f.getOptions(false).map(o => [typeof o[0] === 'string' ? o[0] : (o[0].alt || ''), o[1]]) };
  if (f instanceof Blockly.FieldNumber) {
    const a = { type: 'field_number', name, value: f.getValue() };
    if (isFinite(f.getMin())) a.min = f.getMin();
    if (isFinite(f.getMax())) a.max = f.getMax();
    if (f.getPrecision()) a.precision = f.getPrecision();
    return a;
  }
  if (f instanceof Blockly.FieldCheckbox) return { type: 'field_checkbox', name, checked: f.getValue() === true || f.getValue() === 'TRUE' };
  if (f instanceof Blockly.FieldTextInput) return { type: 'field_input', name, text: f.getValue() };
  throw new Error('unsupported field in ' + f.name);
}

const out = [];
for (const type of TYPES) {
  const b = ws.newBlock(type);
  const def = { type };
  let n = 0;
  if (!DROP_ALL_INPUTS.has(type)) {
    for (const input of b.inputList) {
      if ((OPTIONAL_INPUTS[type] || []).includes(input.name)) continue;
      const args = [];
      for (const f of input.fieldRow) {
        if (f instanceof Blockly.FieldImage) continue; // decorative (text block quotes); the UI draws those
        if (f instanceof Blockly.FieldLabel && !f.SERIALIZABLE && f.getText() === '') continue; // spacer
        args.push(fieldArg(f));
      }
      const ia = { type: input.type === 1 ? 'input_value' : input.type === 3 ? 'input_statement' : 'input_dummy' };
      if (input.name) ia.name = input.name;
      const check = input.connection && input.connection.getCheck();
      if (check) ia.check = check.length === 1 ? check[0] : check;
      args.push(ia);
      def['message' + n] = args.map((_, i) => '%' + (i + 1)).join(' ');
      def['args' + n] = args;
      n++;
    }
  }
  // labels that arrive as plain text are stored inside args as field_label, which is what Blockly's own
  // JSON loader accepts as well.
  if (b.getInputsInline()) def.inputsInline = true;
  const oc = b.outputConnection;
  if (oc) def.output = oc.getCheck() ? (oc.getCheck().length === 1 ? oc.getCheck()[0] : oc.getCheck()) : null;
  if (b.previousConnection) def.previousStatement = b.previousConnection.getCheck() || null;
  if (b.nextConnection) def.nextStatement = b.nextConnection.getCheck() || null;
  def.colour = style(b);
  if (MUTATORS[type]) def.mutator = MUTATORS[type];
  out.push(def);
}

// Procedure blocks build their parameter rows dynamically (see ProcedureDefMutator/ProcedureCallMutator),
// so only the static skeleton is described here.
const proc = Blockly.Themes.Classic.blockStyles.procedure_blocks.colourPrimary;
const top = [{ type: 'field_label', text: 'to' }, { type: 'field_input', name: 'NAME', text: 'do something' }, { type: 'input_dummy', name: 'TOPROW' }];
out.push(
  { type: 'procedures_defnoreturn', message0: '%1 %2 %3', args0: top, message1: '%1 %2', args1: [{ type: 'field_label', text: 'do' }, { type: 'input_statement', name: 'STACK' }], colour: proc, mutator: 'procedures_def_mutator' },
  { type: 'procedures_defreturn', message0: '%1 %2 %3', args0: top, message1: '%1 %2', args1: [{ type: 'field_label', text: 'do' }, { type: 'input_statement', name: 'STACK' }],
    message2: '%1 %2', args2: [{ type: 'field_label', text: 'return' }, { type: 'input_value', name: 'RETURN' }], colour: proc, mutator: 'procedures_def_mutator' },
  { type: 'procedures_callnoreturn', message0: '%1', args0: [{ type: 'input_dummy', name: 'TOPROW' }], previousStatement: null, nextStatement: null, colour: proc, mutator: 'procedures_call_mutator' },
  { type: 'procedures_callreturn', message0: '%1', args0: [{ type: 'input_dummy', name: 'TOPROW' }], output: null, colour: proc, mutator: 'procedures_call_mutator' },
  { type: 'procedures_ifreturn', message0: '%1 %2', args0: [{ type: 'field_label', text: 'if' }, { type: 'input_value', name: 'CONDITION', check: 'Boolean' }], inputsInline: true, previousStatement: null, nextStatement: null, colour: proc, mutator: 'procedures_ifreturn_mutator' },
);

const file = path.resolve(__dirname, '../../Packages/com.b4u.blockly/Runtime/Resources/Blockly/StandardBlocks.json');
fs.writeFileSync(file, '[\n' + out.map(d => '  ' + JSON.stringify(d)).join(',\n') + '\n]\n');
console.log('wrote', out.length, 'definitions ->', file);
