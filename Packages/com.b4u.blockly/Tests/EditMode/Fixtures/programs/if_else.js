var x;


x = 5;
if (x < 3) {
  window.alert('small');
} else if (x < 6) {
  window.alert('medium');
} else if (x < 9) {
  window.alert('large');
} else {
  window.alert('huge');
}
x = 100;
if (x < 3) {
  window.alert('small');
} else if (x < 6) {
  window.alert('medium');
} else {
  window.alert('else-branch');
}
if (false) {
  window.alert('never');
}
window.alert('done');
