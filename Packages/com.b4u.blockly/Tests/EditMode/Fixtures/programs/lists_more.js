function subsequenceFromEndFromEnd(sequence, at1, at2) {
  var start = sequence.length - 1 - at1;
  var end = sequence.length - 1 - at2 + 1;
  return sequence.slice(start, end);
}

function subsequenceFromStartLast(sequence, at1) {
  var start = at1;
  var end = sequence.length - 1 + 1;
  return sequence.slice(start, end);
}

function listsGetSortCompare(type, direction) {
  var compareFuncs = {
    'NUMERIC': function(a, b) {
        return Number(a) - Number(b); },
    'TEXT': function(a, b) {
        return String(a) > String(b) ? 1 : -1; },
    'IGNORE_CASE': function(a, b) {
        return String(a).toLowerCase() > String(b).toLowerCase() ? 1 : -1; },
  };
  var compare = compareFuncs[type];
  return function(a, b) { return compare(a, b) * direction; };
}


window.alert(['a', 'b', 'c', 'd', 'e'].slice(1, 3));
window.alert(['a', 'b', 'c'].slice(0));
window.alert(subsequenceFromEndFromEnd(['a', 'b', 'c', 'd', 'e'], 2, 0));
window.alert(subsequenceFromStartLast(['a', 'b', 'c', 'd'], 1));
window.alert(['a', 'b', 'c', 'd'].slice(3, 1));
window.alert('a,b,,c'.split(','));
window.alert('abc'.split(''));
window.alert(['x', 'y', 'z'].join('-'));
window.alert([10, 9, 100, 1].slice().sort(listsGetSortCompare("NUMERIC", 1)));
window.alert([10, 9, 100, 1].slice().sort(listsGetSortCompare("NUMERIC", -1)));
window.alert(['b', 'B', 'a', 'A'].slice().sort(listsGetSortCompare("TEXT", 1)));
window.alert(['b', 'B', 'a', 'A'].slice().sort(listsGetSortCompare("IGNORE_CASE", 1)));
window.alert([10, 9, 100, 1].slice().sort(listsGetSortCompare("TEXT", 1)));
window.alert(['a', 'b', 'a'].indexOf('a') + 1);
window.alert(['a', 'b', 'a'].lastIndexOf('a') + 1);
window.alert(['a'].indexOf('z') + 1);
window.alert(['a', 'b', 'c'].slice().reverse());
window.alert(Math.atan2(1, 1) / Math.PI * 180);
window.alert(Math.atan2(0, -1) / Math.PI * 180);
