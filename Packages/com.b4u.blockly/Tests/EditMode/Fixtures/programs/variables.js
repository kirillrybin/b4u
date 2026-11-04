var unset, a, fresh, t, b;


window.alert(unset);
a = 1;
window.alert(a);
a = (typeof a === 'number' ? a : 0) + 41;
window.alert(a);
fresh = (typeof fresh === 'number' ? fresh : 0) + 5;
window.alert(fresh);
t = 'str';
t = (typeof t === 'number' ? t : 0) + 1;
window.alert(t);
b = a;
a = 0;
window.alert([a,',',b].join(''));
