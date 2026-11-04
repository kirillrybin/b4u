var i, j;


for (i = 1; i <= 10; i++) {
  if (i == 3) {
    continue;
  }
  if (i == 6) {
    break;
  }
  window.alert(i);
}
window.alert('after');
for (var count = 0; count < 2; count++) {
  for (j = 1; j <= 3; j++) {
    if (j == 2) {
      break;
    }
    window.alert('j=' + String(j));
  }
}
