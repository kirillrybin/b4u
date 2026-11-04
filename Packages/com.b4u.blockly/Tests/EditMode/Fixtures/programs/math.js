function mathIsPrime(n) {
  // https://en.wikipedia.org/wiki/Primality_test#Naive_methods
  if (n == 2 || n == 3) {
    return true;
  }
  // False if n is NaN, negative, is 1, or not whole.
  // And false if n is divisible by 2 or 3.
  if (isNaN(n) || n <= 1 || n % 1 !== 0 || n % 2 === 0 || n % 3 === 0) {
    return false;
  }
  // Check all the numbers of form 6k +/- 1, up to sqrt(n).
  for (var x = 6; x <= Math.sqrt(n) + 1; x += 6) {
    if (n % (x - 1) === 0 || n % (x + 1) === 0) {
      return false;
    }
  }
  return true;
}

function mathMean(myList) {
  return myList.reduce(function(x, y) {return x + y;}, 0) / myList.length;
}

function mathMedian(myList) {
  var localList = myList.filter(function (x) {return typeof x === 'number';});
  if (!localList.length) return null;
  localList.sort(function(a, b) {return b - a;});
  if (localList.length % 2 === 0) {
    return (localList[localList.length / 2 - 1] + localList[localList.length / 2]) / 2;
  } else {
    return localList[(localList.length - 1) / 2];
  }
}

function mathModes(values) {
  var modes = [];
  var counts = [];
  var maxCount = 0;
  for (var i = 0; i < values.length; i++) {
    var value = values[i];
    var found = false;
    var thisCount;
    for (var j = 0; j < counts.length; j++) {
      if (counts[j][0] === value) {
        thisCount = ++counts[j][1];
        found = true;
        break;
      }
    }
    if (!found) {
      counts.push([value, 1]);
      thisCount = 1;
    }
    maxCount = Math.max(thisCount, maxCount);
  }
  for (var j = 0; j < counts.length; j++) {
    if (counts[j][1] === maxCount) {
      modes.push(counts[j][0]);
    }
  }
  return modes;
}

function mathStandardDeviation(numbers) {
  var n = numbers.length;
  if (!n) return null;
  var mean = numbers.reduce(function(x, y) {return x + y;}) / n;
  var variance = 0;
  for (var j = 0; j < n; j++) {
    variance += Math.pow(numbers[j] - mean, 2);
  }
  variance /= n;
  return Math.sqrt(variance);
}


window.alert(Math.sqrt(16));
window.alert(Math.abs(-5));
window.alert(-5);
window.alert(Math.log(1));
window.alert(Math.log(1000) / Math.log(10));
window.alert(Math.exp(0));
window.alert(Math.pow(10,3));
window.alert(Math.sqrt(-1));
window.alert(Math.sin(90 / 180 * Math.PI));
window.alert(Math.cos(60 / 180 * Math.PI));
window.alert(Math.tan(45 / 180 * Math.PI));
window.alert(Math.asin(1) / Math.PI * 180);
window.alert(Math.acos(0) / Math.PI * 180);
window.alert(Math.atan(1) / Math.PI * 180);
window.alert(Math.PI);
window.alert(Math.E);
window.alert((1 + Math.sqrt(5)) / 2);
window.alert(Math.SQRT2);
window.alert(Math.SQRT1_2);
window.alert(Infinity);
window.alert(4 % 2 === 0);
window.alert(3 % 2 === 1);
window.alert(-3 % 2 === 1);
window.alert(mathIsPrime(7));
window.alert(mathIsPrime(9));
window.alert(mathIsPrime(1));
window.alert(2.5 % 1 === 0);
window.alert(3 > 0);
window.alert(3 < 0);
window.alert(10 % 5 === 0);
window.alert(10 % 3 === 0);
window.alert(Math.round(2.5));
window.alert(Math.round(-2.5));
window.alert(Math.round(1.2));
window.alert(Math.round(-1.2));
window.alert(Math.ceil(2.5));
window.alert(Math.ceil(-2.5));
window.alert(Math.ceil(1.2));
window.alert(Math.ceil(-1.2));
window.alert(Math.floor(2.5));
window.alert(Math.floor(-2.5));
window.alert(Math.floor(1.2));
window.alert(Math.floor(-1.2));
window.alert(10 % 3);
window.alert(-10 % 3);
window.alert(Math.min(Math.max(50, 1), 10));
window.alert(Math.min(Math.max(-5, 1), 10));
window.alert([1, 2, 3, 4].reduce(function(x, y) {return x + y;}, 0));
window.alert(Math.min.apply(null, [4, 2, 9]));
window.alert(Math.max.apply(null, [4, 2, 9]));
window.alert(mathMean([1, 2, 3, 4]));
window.alert(mathMedian([5, 1, 3]));
window.alert(mathMedian([4, 1, 3, 2]));
window.alert(mathModes([1, 2, 2, 3, 3]));
window.alert(mathStandardDeviation([2, 4, 4, 4, 5, 5, 7, 9]));
