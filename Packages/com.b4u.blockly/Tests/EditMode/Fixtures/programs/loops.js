var n, i, item;


for (var count = 0; count < 3; count++) {
  window.alert('rep');
}
for (var count2 = 0; count2 < 2; count2++) {
  window.alert('rep2');
}
n = 0;
while (n < 3) {
  window.alert(n);
  n = (typeof n === 'number' ? n : 0) + 1;
}
while (!(n >= 5)) {
  window.alert(n);
  n = (typeof n === 'number' ? n : 0) + 1;
}
for (i = 1; i <= 5; i += 2) {
  window.alert(i);
}
for (i = 5; i >= 1; i -= 2) {
  window.alert(i);
}
for (i = 0; i <= 1; i += 0.25) {
  window.alert(i);
}
var item_list = ['a', 'b', 'c'];
for (var item_index in item_list) {
  item = item_list[item_index];
  window.alert(item);
}
