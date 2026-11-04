var who, n, limit, a, b, g, p0, i, p1;

function greet(who) {
  window.alert('hello ' + String(who));
}

function fact(n) {
  if (n <= 1) {
    return 1;
  }
  return n * fact(n - 1);
}

function fib(n) {
  return n < 2 ? n : fib(n - 1) + fib(n - 2);
}

function firstOver(limit) {
  for (i = 1; i <= 100; i++) {
    if (i * i > limit) {
      return i;
    }
  }
  return -1;
}

function add(a, b) {
  return a + b;
}

function noop() {
  window.alert('noop ran');
}

function clobber(g) {
  g = 999;
  window.alert(g);
}


greet('world');
noop();
window.alert(fact(5));
window.alert(fact(10));
window.alert(fib(15));
window.alert(firstOver(50));
window.alert(firstOver(100000));
window.alert(add(2, 3));
window.alert(add('a', 'b'));
g = 1;
clobber(5);
window.alert(g);
