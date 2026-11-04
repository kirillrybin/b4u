var s;

function textToTitleCase(str) {
  return str.replace(/\S+/g,
      function(txt) {return txt[0].toUpperCase() + txt.substring(1).toLowerCase();});
}

function textReplace(haystack, needle, replacement) {
  needle = needle.replace(/([-()\[\]{}+?*.$\^|,:#<!\\])/g, '\\$1')
                 .replace(/\x08/g, '\\x08');
  return haystack.replace(new RegExp(needle, 'g'), replacement);
}


window.alert(['a','b','c'].join(''));
window.alert('n=' + String(5));
window.alert('only');
window.alert('');
window.alert('hello'.length);
window.alert(''.length);
window.alert(!''.length);
window.alert(!'x'.length);
window.alert('banana'.indexOf('an') + 1);
window.alert('banana'.lastIndexOf('an') + 1);
window.alert('banana'.indexOf('zz') + 1);
window.alert('hello wORLD foo'.toUpperCase());
window.alert('hello wORLD foo'.toLowerCase());
window.alert(textToTitleCase('hello wORLD foo'));
window.alert(['[','  pad  '.trim(),']'].join(''));
window.alert(['[','  pad  '.replace(/^[\s\xa0]+/, ''),']'].join(''));
window.alert(['[','  pad  '.replace(/[\s\xa0]+$/, ''),']'].join(''));
window.alert(textReplace('banana', 'a', 'o'));
window.alert('abc'.split('').reverse().join(''));
s = 'x';
s += 'y';
s += String(3);
window.alert(s);
