const { def, call, ifReturn, B, chain, VAR, num, txt, bool, print, get, set, arith, cmp, list, join, printAll, workspace } = require('./dsl');

const P = {};

P.arithmetic = workspace(printAll(
  arith('ADD', num(1), num(2)), arith('MINUS', num(7), num(10)), arith('MULTIPLY', num(3), num(4)),
  arith('DIVIDE', num(9), num(2)), arith('POWER', num(2), num(10)), arith('DIVIDE', num(1), num(0)),
  arith('ADD', num(0.1), num(0.2)), arith('POWER', num(10), num(21)), arith('POWER', num(10), num(-7)),
  arith('MULTIPLY', num(123456789), num(1000000000000)), arith('DIVIDE', num(-1), num(3)),
  arith('MINUS', num(0), num(0))));

P.logic = workspace(printAll(
  cmp('EQ', num(1), num(1)), cmp('NEQ', num(1), num(2)), cmp('LT', num(1), num(2)), cmp('LTE', num(2), num(2)),
  cmp('GT', num(1), num(2)), cmp('GTE', num(3), num(2)), cmp('EQ', txt('a'), txt('a')), cmp('LT', txt('a'), txt('b')),
  cmp('EQ', num(1), txt('1')), cmp('EQ', B('logic_null'), B('logic_null')),
  B('logic_operation', { f: { OP: 'AND' }, v: { A: bool(true), B: bool(false) } }),
  B('logic_operation', { f: { OP: 'OR' }, v: { A: bool(false), B: bool(true) } }),
  B('logic_negate', { v: { BOOL: bool(false) } }),
  B('logic_ternary', { v: { IF: bool(true), THEN: txt('yes'), ELSE: txt('no') } }),
  B('logic_ternary', { v: { IF: bool(false), THEN: txt('yes'), ELSE: txt('no') } }),
  B('logic_null'), bool(true)));

P.if_else = workspace(chain(
  set('x', num(5)),
  B('controls_if', {
    m: { elseif: 2, else: 1 },
    v: { IF0: cmp('LT', get('x'), num(3)), IF1: cmp('LT', get('x'), num(6)), IF2: cmp('LT', get('x'), num(9)) },
    s: { DO0: print(txt('small')), DO1: print(txt('medium')), DO2: print(txt('large')), ELSE: print(txt('huge')) },
  }),
  set('x', num(100)),
  B('controls_if', {
    m: { elseif: 1, else: 1 },
    v: { IF0: cmp('LT', get('x'), num(3)), IF1: cmp('LT', get('x'), num(6)) },
    s: { DO0: print(txt('small')), DO1: print(txt('medium')), ELSE: print(txt('else-branch')) },
  }),
  B('controls_if', { v: { IF0: bool(false) }, s: { DO0: print(txt('never')) } }),
  print(txt('done'))));

P.loops = workspace(chain(
  B('controls_repeat_ext', { v: { TIMES: num(3) }, s: { DO: print(txt('rep')) } }),
  B('controls_repeat', { f: { TIMES: 2 }, s: { DO: print(txt('rep2')) } }),
  set('n', num(0)),
  B('controls_whileUntil', { f: { MODE: 'WHILE' }, v: { BOOL: cmp('LT', get('n'), num(3)) },
    s: { DO: chain(print(get('n')), B('math_change', { f: { VAR: VAR('n') }, v: { DELTA: num(1) } })) } }),
  B('controls_whileUntil', { f: { MODE: 'UNTIL' }, v: { BOOL: cmp('GTE', get('n'), num(5)) },
    s: { DO: chain(print(get('n')), B('math_change', { f: { VAR: VAR('n') }, v: { DELTA: num(1) } })) } }),
  B('controls_for', { f: { VAR: VAR('i') }, v: { FROM: num(1), TO: num(5), BY: num(2) }, s: { DO: print(get('i')) } }),
  B('controls_for', { f: { VAR: VAR('i') }, v: { FROM: num(5), TO: num(1), BY: num(2) }, s: { DO: print(get('i')) } }),
  B('controls_for', { f: { VAR: VAR('i') }, v: { FROM: num(0), TO: num(1), BY: num(0.25) }, s: { DO: print(get('i')) } }),
  B('controls_forEach', { f: { VAR: VAR('item') }, v: { LIST: list(txt('a'), txt('b'), txt('c')) }, s: { DO: print(get('item')) } })));

P.break_continue = workspace(chain(
  B('controls_for', { f: { VAR: VAR('i') }, v: { FROM: num(1), TO: num(10), BY: num(1) }, s: { DO: chain(
    B('controls_if', { v: { IF0: cmp('EQ', get('i'), num(3)) }, s: { DO0: B('controls_flow_statements', { f: { FLOW: 'CONTINUE' } }) } }),
    B('controls_if', { v: { IF0: cmp('EQ', get('i'), num(6)) }, s: { DO0: B('controls_flow_statements', { f: { FLOW: 'BREAK' } }) } }),
    print(get('i'))) } }),
  print(txt('after')),
  B('controls_repeat_ext', { v: { TIMES: num(2) }, s: { DO: B('controls_for', { f: { VAR: VAR('j') }, v: { FROM: num(1), TO: num(3), BY: num(1) }, s: { DO: chain(
    B('controls_if', { v: { IF0: cmp('EQ', get('j'), num(2)) }, s: { DO0: B('controls_flow_statements', { f: { FLOW: 'BREAK' } }) } }),
    print(join(txt('j='), get('j')))) } }) } })));

const mathSingle = (op, n) => B('math_single', { f: { OP: op }, v: { NUM: num(n) } });
const trig = (op, n) => B('math_trig', { f: { OP: op }, v: { NUM: num(n) } });
const prop = (p, n, d) => B('math_number_property', d === undefined
  ? { f: { PROPERTY: p }, v: { NUMBER_TO_CHECK: num(n) } }
  : { m: { divisor_input: 'true' }, f: { PROPERTY: p }, v: { NUMBER_TO_CHECK: num(n), DIVISOR: num(d) } });
const onList = (op, ...xs) => B('math_on_list', { f: { OP: op }, v: { LIST: list(...xs.map(num)) } });

P.math = workspace(printAll(
  mathSingle('ROOT', 16), mathSingle('ABS', -5), mathSingle('NEG', 5), mathSingle('LN', 1), mathSingle('LOG10', 1000),
  mathSingle('EXP', 0), mathSingle('POW10', 3), mathSingle('ROOT', -1),
  trig('SIN', 90), trig('COS', 60), trig('TAN', 45), trig('ASIN', 1), trig('ACOS', 0), trig('ATAN', 1),
  ...['PI', 'E', 'GOLDEN_RATIO', 'SQRT2', 'SQRT1_2', 'INFINITY'].map(c => B('math_constant', { f: { CONSTANT: c } })),
  prop('EVEN', 4), prop('ODD', 3), prop('ODD', -3), prop('PRIME', 7), prop('PRIME', 9), prop('PRIME', 1), prop('WHOLE', 2.5),
  prop('POSITIVE', 3), prop('NEGATIVE', 3), prop('DIVISIBLE_BY', 10, 5), prop('DIVISIBLE_BY', 10, 3),
  ...['ROUND', 'ROUNDUP', 'ROUNDDOWN'].flatMap(op => [2.5, -2.5, 1.2, -1.2].map(n => B('math_round', { f: { OP: op }, v: { NUM: num(n) } }))),
  B('math_modulo', { v: { DIVIDEND: num(10), DIVISOR: num(3) } }), B('math_modulo', { v: { DIVIDEND: num(-10), DIVISOR: num(3) } }),
  B('math_constrain', { v: { VALUE: num(50), LOW: num(1), HIGH: num(10) } }),
  B('math_constrain', { v: { VALUE: num(-5), LOW: num(1), HIGH: num(10) } }),
  onList('SUM', 1, 2, 3, 4), onList('MIN', 4, 2, 9), onList('MAX', 4, 2, 9), onList('AVERAGE', 1, 2, 3, 4),
  onList('MEDIAN', 5, 1, 3), onList('MEDIAN', 4, 1, 3, 2), onList('MODE', 1, 2, 2, 3, 3), onList('STD_DEV', 2, 4, 4, 4, 5, 5, 7, 9)));

P.number_format = workspace(printAll(
  num(1e21), num(1e-7), num(0.000001), num(123456789012345680000), num(1.5e300), num(-0.5), num(100), num(0.1),
  arith('MULTIPLY', num(1e21), num(10)), arith('DIVIDE', num(1), num(3)), arith('DIVIDE', num(2), num(3)),
  arith('POWER', num(2), num(53)), arith('POWER', num(2), num(70)), arith('DIVIDE', num(5), num(1e8))));

const textBin = (t, args) => B(t, args);
P.text = workspace(chain(
  printAll(
    join(txt('a'), txt('b'), txt('c')), join(txt('n='), num(5)), join(txt('only')), join(),
    B('text_length', { v: { VALUE: txt('hello') } }), B('text_length', { v: { VALUE: txt('') } }),
    B('text_isEmpty', { v: { VALUE: txt('') } }), B('text_isEmpty', { v: { VALUE: txt('x') } }),
    B('text_indexOf', { f: { END: 'FIRST' }, v: { VALUE: txt('banana'), FIND: txt('an') } }),
    B('text_indexOf', { f: { END: 'LAST' }, v: { VALUE: txt('banana'), FIND: txt('an') } }),
    B('text_indexOf', { f: { END: 'FIRST' }, v: { VALUE: txt('banana'), FIND: txt('zz') } }),
    ...['UPPERCASE', 'LOWERCASE', 'TITLECASE'].map(c => B('text_changeCase', { f: { CASE: c }, v: { TEXT: txt('hello wORLD foo') } })),
    ...['BOTH', 'LEFT', 'RIGHT'].map(m => join(txt('['), B('text_trim', { f: { MODE: m }, v: { TEXT: txt('  pad  ') } }), txt(']'))),
    B('text_replace', { v: { FROM: txt('a'), TO: txt('o'), TEXT: txt('banana') } }),
    B('text_reverse', { v: { TEXT: txt('abc') } })),
  set('s', txt('x')),
  B('text_append', { f: { VAR: VAR('s') }, v: { TEXT: txt('y') } }),
  B('text_append', { f: { VAR: VAR('s') }, v: { TEXT: num(3) } }),
  print(get('s'))));

const getIdx = (mode, where, l, at) => B('lists_getIndex', { m: { statement: mode === 'REMOVE' ? 'true' : 'false', at: at !== undefined ? 'true' : 'false' },
  f: { MODE: mode, WHERE: where }, v: at !== undefined ? { VALUE: l, AT: num(at) } : { VALUE: l } });
const setIdx = (mode, where, name, val, at) => B('lists_setIndex', { m: { at: at !== undefined ? 'true' : 'false' },
  f: { MODE: mode, WHERE: where }, v: at !== undefined ? { LIST: get(name), AT: num(at), TO: val } : { LIST: get(name), TO: val } });
const abc = () => list(txt('a'), txt('b'), txt('c'), txt('d'));

P.lists = workspace(chain(
  printAll(
    abc(), B('lists_create_empty'), list(),
    B('lists_repeat', { v: { ITEM: txt('x'), NUM: num(3) } }),
    B('lists_length', { v: { VALUE: abc() } }), B('lists_isEmpty', { v: { VALUE: abc() } }), B('lists_isEmpty', { v: { VALUE: B('lists_create_empty') } }),
    getIdx('GET', 'FIRST', abc()), getIdx('GET', 'LAST', abc()), getIdx('GET', 'FROM_START', abc(), 2), getIdx('GET', 'FROM_END', abc(), 1),
    getIdx('GET', 'FROM_START', abc(), 9)),
  set('l', abc()),
  print(getIdx('GET_REMOVE', 'FIRST', get('l'))), print(get('l')),
  print(getIdx('GET_REMOVE', 'LAST', get('l'))), print(get('l')),
  print(getIdx('GET_REMOVE', 'FROM_START', get('l'), 1)), print(get('l')),
  set('l', abc()),
  getIdx('REMOVE', 'FROM_END', get('l'), 2), print(get('l')),
  getIdx('REMOVE', 'FIRST', get('l')), print(get('l')),
  set('l', abc()),
  setIdx('SET', 'FIRST', 'l', txt('A')), print(get('l')),
  setIdx('SET', 'LAST', 'l', txt('D')), print(get('l')),
  setIdx('SET', 'FROM_START', 'l', txt('B'), 2), print(get('l')),
  setIdx('SET', 'FROM_END', 'l', txt('C'), 2), print(get('l')),
  setIdx('INSERT', 'FIRST', 'l', txt('0')), print(get('l')),
  setIdx('INSERT', 'LAST', 'l', txt('9')), print(get('l')),
  setIdx('INSERT', 'FROM_START', 'l', txt('s'), 2), print(get('l')),
  setIdx('INSERT', 'FROM_END', 'l', txt('e'), 2), print(get('l'))));

P.variables = workspace(chain(
  print(get('unset')),
  set('a', num(1)), print(get('a')),
  B('math_change', { f: { VAR: VAR('a') }, v: { DELTA: num(41) } }), print(get('a')),
  B('math_change', { f: { VAR: VAR('fresh') }, v: { DELTA: num(5) } }), print(get('fresh')),
  set('t', txt('str')), B('math_change', { f: { VAR: VAR('t') }, v: { DELTA: num(1) } }), print(get('t')),
  set('b', get('a')), set('a', num(0)), print(join(get('a'), txt(','), get('b')))));


// ---- procedures ----
P.procedures = workspace(
  def('greet', ['who'], print(join(txt('hello '), get('who')))),
  def('fact', ['n'], ifReturn(cmp('LTE', get('n'), num(1)), num(1)), arith('MULTIPLY', get('n'), call('fact', [arith('MINUS', get('n'), num(1))], true))),
  def('fib', ['n'], null, B('logic_ternary', { v: { IF: cmp('LT', get('n'), num(2)), THEN: get('n'),
    ELSE: arith('ADD', call('fib', [arith('MINUS', get('n'), num(1))], true), call('fib', [arith('MINUS', get('n'), num(2))], true)) } })),
  def('firstOver', ['limit'], B('controls_for', { f: { VAR: VAR('i') }, v: { FROM: num(1), TO: num(100), BY: num(1) },
    s: { DO: ifReturn(cmp('GT', arith('MULTIPLY', get('i'), get('i')), get('limit')), get('i')) } }), num(-1)),
  def('add', ['a', 'b'], null, arith('ADD', get('a'), get('b'))),
  def('noop', [], print(txt('noop ran'))),
  def('clobber', ['g'], chain(set('g', num(999)), print(get('g')))),
  [
    call('greet', [txt('world')]), call('noop'),
    print(call('fact', [num(5)], true)), print(call('fact', [num(10)], true)),
    print(call('fib', [num(15)], true)), print(call('firstOver', [num(50)], true)), print(call('firstOver', [num(100000)], true)),
    print(call('add', [num(2), num(3)], true)), print(call('add', [txt('a'), txt('b')], true)),
    set('g', num(1)), call('clobber', [num(5)]), print(get('g')),
  ]);

// ---- more text ----
const charAt = (where, t, at) => B('text_charAt', { raw: `<mutation at="${at !== undefined}"></mutation>`, f: { WHERE: where }, v: at !== undefined ? { VALUE: t, AT: num(at) } : { VALUE: t } });
const substr = (w1, a1, w2, a2, t) => B('text_getSubstring', { raw: `<mutation at1="${a1 !== undefined}" at2="${a2 !== undefined}"></mutation>`,
  f: { WHERE1: w1, WHERE2: w2 }, v: Object.assign({ STRING: t }, a1 !== undefined ? { AT1: num(a1) } : {}, a2 !== undefined ? { AT2: num(a2) } : {}) });
P.text_more = workspace(printAll(
  charAt('FROM_START', txt('hello'), 2), charAt('FROM_END', txt('hello'), 1), charAt('FIRST', txt('hello')), charAt('LAST', txt('hello')),
  charAt('FROM_START', txt('hello'), 9), charAt('FROM_END', txt('hello'), 9),
  substr('FROM_START', 2, 'FROM_START', 4, txt('abcdef')), substr('FIRST', undefined, 'LAST', undefined, txt('abcdef')),
  substr('FROM_START', 2, 'LAST', undefined, txt('abcdef')), substr('FIRST', undefined, 'FROM_END', 2, txt('abcdef')),
  substr('FROM_END', 3, 'FROM_END', 1, txt('abcdef')), substr('FROM_START', 5, 'FROM_START', 2, txt('abcdef')),
  substr('FROM_START', 1, 'FROM_START', 99, txt('abc')),
  B('text_count', { v: { SUB: txt('an'), TEXT: txt('banana') } }), B('text_count', { v: { SUB: txt('aa'), TEXT: txt('aaaa') } }),
  B('text_count', { v: { SUB: txt(''), TEXT: txt('abc') } }),
  B('text_indexOf', { f: { END: 'FIRST' }, v: { VALUE: txt(''), FIND: txt('') } })));

// ---- more lists ----
const sublist = (w1, a1, w2, a2, l) => B('lists_getSublist', { raw: `<mutation at1="${a1 !== undefined}" at2="${a2 !== undefined}"></mutation>`,
  f: { WHERE1: w1, WHERE2: w2 }, v: Object.assign({ LIST: l }, a1 !== undefined ? { AT1: num(a1) } : {}, a2 !== undefined ? { AT2: num(a2) } : {}) });
const sort = (type, dir, l) => B('lists_sort', { f: { TYPE: type, DIRECTION: dir }, v: { LIST: l } });
const nums = (...xs) => list(...xs.map(num));
const words = (...xs) => list(...xs.map(txt));
P.lists_more = workspace(printAll(
  sublist('FROM_START', 2, 'FROM_START', 3, words('a', 'b', 'c', 'd', 'e')), sublist('FIRST', undefined, 'LAST', undefined, words('a', 'b', 'c')),
  sublist('FROM_END', 3, 'FROM_END', 1, words('a', 'b', 'c', 'd', 'e')), sublist('FROM_START', 2, 'LAST', undefined, words('a', 'b', 'c', 'd')),
  sublist('FROM_START', 4, 'FROM_START', 1, words('a', 'b', 'c', 'd')),
  B('lists_split', { raw: '<mutation mode="SPLIT"></mutation>', f: { MODE: 'SPLIT' }, v: { INPUT: txt('a,b,,c'), DELIM: txt(',') } }),
  B('lists_split', { raw: '<mutation mode="SPLIT"></mutation>', f: { MODE: 'SPLIT' }, v: { INPUT: txt('abc'), DELIM: txt('') } }),
  B('lists_split', { raw: '<mutation mode="JOIN"></mutation>', f: { MODE: 'JOIN' }, v: { INPUT: words('x', 'y', 'z'), DELIM: txt('-') } }),
  sort('NUMERIC', '1', nums(10, 9, 100, 1)), sort('NUMERIC', '-1', nums(10, 9, 100, 1)),
  sort('TEXT', '1', words('b', 'B', 'a', 'A')), sort('IGNORE_CASE', '1', words('b', 'B', 'a', 'A')), sort('TEXT', '1', nums(10, 9, 100, 1)),
  B('lists_indexOf', { f: { END: 'FIRST' }, v: { VALUE: words('a', 'b', 'a'), FIND: txt('a') } }),
  B('lists_indexOf', { f: { END: 'LAST' }, v: { VALUE: words('a', 'b', 'a'), FIND: txt('a') } }),
  B('lists_indexOf', { f: { END: 'FIRST' }, v: { VALUE: words('a'), FIND: txt('z') } }),
  B('lists_reverse', { v: { LIST: words('a', 'b', 'c') } }),
  B('math_atan2', { v: { X: num(1), Y: num(1) } }), B('math_atan2', { v: { X: num(-1), Y: num(0) } })));

module.exports = P;
