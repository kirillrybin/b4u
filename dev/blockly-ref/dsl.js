// Tiny DSL that builds Blockly XML so test programs stay readable.
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** B(type, { f: fields, v: value inputs, s: statement inputs, m: mutation attrs }) */
function B(type, { f = {}, v = {}, s = {}, m = null, raw = '' } = {}) {
  const mut = raw ? raw : m ? `<mutation ${Object.entries(m).map(([k, x]) => `${k}="${x}"`).join(' ')}></mutation>` : '';
  const fields = Object.entries(f).map(([k, x]) =>
    x && x.__var ? `<field name="${k}" id="id_${x.__var}">${x.__var}</field>` : `<field name="${k}">${esc(x)}</field>`).join('');
  const vals = Object.entries(v).map(([k, x]) => `<value name="${k}">${x}</value>`).join('');
  const stmts = Object.entries(s).map(([k, x]) => `<statement name="${k}">${x}</statement>`).join('');
  return `<block type="${type}">${mut}${fields}${vals}${stmts}</block>`;
}
/** Chains statement blocks with <next>. */
const flat = xs => xs.flatMap(x => Array.isArray(x) ? flat(x) : [x]);
const chain = (...bs) => flat(bs).reduceRight((acc, b) => acc === null ? b : b.slice(0, -8) + `<next>${acc}</next></block>`, null);

const VAR = name => ({ __var: name });
const num = n => B('math_number', { f: { NUM: n } });
const txt = t => B('text', { f: { TEXT: t } });
const bool = b => B('logic_boolean', { f: { BOOL: b ? 'TRUE' : 'FALSE' } });
const print = x => B('text_print', { v: { TEXT: x } });
const get = n => B('variables_get', { f: { VAR: VAR(n) } });
const set = (n, x) => B('variables_set', { f: { VAR: VAR(n) }, v: { VALUE: x } });
const arith = (op, a, b) => B('math_arithmetic', { f: { OP: op }, v: { A: a, B: b } });
const cmp = (op, a, b) => B('logic_compare', { f: { OP: op }, v: { A: a, B: b } });
const list = (...xs) => B('lists_create_with', { m: { items: xs.length }, v: Object.fromEntries(xs.map((x, i) => ['ADD' + i, x])) });
const join = (...xs) => B('text_join', { m: { items: xs.length }, v: Object.fromEntries(xs.map((x, i) => ['ADD' + i, x])) });
const printAll = (...xs) => xs.map(print); // array = statement sequence; chain()/workspace() link it

/** Wraps blocks into a workspace XML, declaring every variable that is referenced. */
function workspace(...tops) {
  const body = tops.map(t => Array.isArray(t) ? chain(t) : t).join('');
  const names = [...new Set([...body.matchAll(/id="id_(\w+)"/g)].map(m => m[1]))];
  const vars = names.length ? `<variables>${names.map(n => `<variable id="id_${n}">${n}</variable>`).join('')}</variables>` : '';
  return `<xml xmlns="https://developers.google.com/blockly/xml">${vars}${body}</xml>`;
}

/** Procedure helpers. Parameter variables are declared like any other variable (id_<name>). */
const def = (name, params, body, ret) => B(ret === undefined ? 'procedures_defnoreturn' : 'procedures_defreturn', {
  raw: params.length ? `<mutation>${params.map(p => `<arg name="${p}" varid="id_${p}"></arg>`).join('')}</mutation>` : '',
  f: { NAME: name }, s: body ? { STACK: body } : {}, v: ret === undefined ? {} : { RETURN: ret } });
const call = (name, args = [], returns = false) => B(returns ? 'procedures_callreturn' : 'procedures_callnoreturn', {
  raw: `<mutation name="${name}">${args.map((_, i) => `<arg name="p${i}"></arg>`).join('')}</mutation>`,
  v: Object.fromEntries(args.map((a, i) => ['ARG' + i, a])) });
const ifReturn = (cond, value) => B('procedures_ifreturn', { raw: `<mutation value="${value === undefined ? 0 : 1}"></mutation>`,
  v: value === undefined ? { CONDITION: cond } : { CONDITION: cond, VALUE: value } });

module.exports = { def, call, ifReturn, B, chain, VAR, num, txt, bool, print, get, set, arith, cmp, list, join, printAll, workspace };
