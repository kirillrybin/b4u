var l;

function listsRepeat(value, n) {
  var array = [];
  for (var i = 0; i < n; i++) {
    array[i] = value;
  }
  return array;
}


window.alert(['a', 'b', 'c', 'd']);
window.alert([]);
window.alert([]);
window.alert(listsRepeat('x', 3));
window.alert(['a', 'b', 'c', 'd'].length);
window.alert(!['a', 'b', 'c', 'd'].length);
window.alert(![].length);
window.alert(['a', 'b', 'c', 'd'][0]);
window.alert(['a', 'b', 'c', 'd'].slice(-1)[0]);
window.alert(['a', 'b', 'c', 'd'][1]);
window.alert(['a', 'b', 'c', 'd'].slice(-1)[0]);
window.alert(['a', 'b', 'c', 'd'][8]);
l = ['a', 'b', 'c', 'd'];
window.alert(l.shift());
window.alert(l);
window.alert(l.pop());
window.alert(l);
window.alert(l.splice(0, 1)[0]);
window.alert(l);
l = ['a', 'b', 'c', 'd'];
l.splice(-2, 1);window.alert(l);
l.shift();
window.alert(l);
l = ['a', 'b', 'c', 'd'];
l[0] = 'A';
window.alert(l);
l[l.length - 1] = 'D';
window.alert(l);
l[1] = 'B';
window.alert(l);
l[l.length - 2] = 'C';
window.alert(l);
l.unshift('0');
window.alert(l);
l.push('9');
window.alert(l);
l.splice(1, 0, 's');
window.alert(l);
l.splice(l.length - 2, 0, 'e');
window.alert(l);
