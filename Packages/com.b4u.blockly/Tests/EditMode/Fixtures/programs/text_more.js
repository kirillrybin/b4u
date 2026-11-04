function textCount(haystack, needle) {
  if (needle.length === 0) {
    return haystack.length + 1;
  } else {
    return haystack.split(needle).length - 1;
  }
}


window.alert('hello'.charAt(1));
window.alert('hello'.slice(-1).charAt(0));
window.alert('hello'.charAt(0));
window.alert('hello'.slice(-1));
window.alert('hello'.charAt(8));
window.alert('hello'.slice(-9).charAt(0));
window.alert('abcdef'.slice(1, 4));
window.alert('abcdef');
window.alert('abcdef'.slice(1, 'abcdef'.length));
window.alert('abcdef'.slice(0, 'abcdef'.length - 1));
window.alert('abcdef'.slice('abcdef'.length - 3, 'abcdef'.length - 0));
window.alert('abcdef'.slice(4, 2));
window.alert('abc'.slice(0, 99));
window.alert(textCount('banana', 'an'));
window.alert(textCount('aaaa', 'aa'));
window.alert(textCount('abc', ''));
window.alert(''.indexOf('') + 1);
