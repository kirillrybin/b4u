using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Blockly
{
    /// <summary>JS "undefined": the value of unset variables and out-of-range list reads.</summary>
    public sealed class Undefined
    {
        public static readonly Undefined Value = new Undefined();
        Undefined() { }
        public override string ToString() => "undefined";
    }

    /// <summary>
    /// Runtime values are plain objects: double, string, bool, List&lt;object&gt;, null (JS null), Undefined.
    /// These helpers reproduce the JavaScript semantics Blockly's JS generator relies on, so programs
    /// behave the same as the code Blockly itself would generate.
    /// </summary>
    public static class JsValue
    {
        public static bool Truthy(object v)
        {
            switch (v)
            {
                case null: return false;
                case Undefined _: return false;
                case bool b: return b;
                case double d: return d != 0 && !double.IsNaN(d);
                case string s: return s.Length > 0;
                default: return true;
            }
        }

        public static double ToNumber(object v)
        {
            switch (v)
            {
                case null: return 0;
                case Undefined _: return double.NaN;
                case bool b: return b ? 1 : 0;
                case double d: return d;
                case string s: return ParseNumber(s);
                case List<object> l:
                    return l.Count == 0 ? 0 : l.Count == 1 ? ToNumber(l[0]) : double.NaN;
                default: return double.NaN;
            }
        }

        /// <summary>
        /// ECMAScript StringToNumber: surrounding whitespace is ignored, "" is 0, accepts decimal / exponent / ±Infinity /
        /// 0x 0o 0b literals, and anything else (including trailing junk like "12abc") is NaN.
        /// </summary>
        public static double ParseNumber(string text)
        {
            string s = text.Trim();
            if (s.Length == 0) return 0;
            if (s == "Infinity" || s == "+Infinity") return double.PositiveInfinity;
            if (s == "-Infinity") return double.NegativeInfinity;

            if (s.Length > 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X' || s[1] == 'o' || s[1] == 'O' || s[1] == 'b' || s[1] == 'B'))
            {
                int radix = char.ToLowerInvariant(s[1]) == 'x' ? 16 : char.ToLowerInvariant(s[1]) == 'o' ? 8 : 2;
                double r = 0;
                for (int k = 2; k < s.Length; k++)
                {
                    int d = HexDigit(s[k]);
                    if (d < 0 || d >= radix) return double.NaN;
                    r = r * radix + d;
                }
                return r;
            }

            // StrDecimalLiteral: [+-] ( digits [. digits?] | . digits ) [ (e|E) [+-] digits ]
            int i = 0, n = s.Length;
            if (s[i] == '+' || s[i] == '-') i++;
            int intStart = i;
            while (i < n && s[i] >= '0' && s[i] <= '9') i++;
            int intDigits = i - intStart, fracDigits = 0;
            if (i < n && s[i] == '.')
            {
                i++;
                int fs = i;
                while (i < n && s[i] >= '0' && s[i] <= '9') i++;
                fracDigits = i - fs;
            }
            if (intDigits + fracDigits == 0) return double.NaN;
            bool negativeExponent = false;
            if (i < n && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < n && (s[i] == '+' || s[i] == '-')) { negativeExponent = s[i] == '-'; i++; }
                int es = i;
                while (i < n && s[i] >= '0' && s[i] <= '9') i++;
                if (i == es) return double.NaN;
            }
            if (i != n) return double.NaN;

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            // Mono throws OverflowException-style failures for huge exponents where modern .NET returns ±Infinity
            bool negative = s[0] == '-';
            if (negativeExponent) return negative ? -0.0 : 0.0;
            return negative ? double.NegativeInfinity : double.PositiveInfinity;
        }

        static int HexDigit(char c)
            => c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;

        public static string ToStr(object v)
        {
            switch (v)
            {
                case null: return "null";
                case Undefined _: return "undefined";
                case bool b: return b ? "true" : "false";
                case double d: return NumberToString(d);
                case string s: return s;
                case List<object> l: return JoinList(l, ",");
                default: return v.ToString();
            }
        }

        /// <summary>Array.prototype.join: null and undefined become the empty string.</summary>
        public static string JoinList(List<object> l, string sep)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < l.Count; i++)
            {
                if (i > 0) sb.Append(sep);
                if (l[i] != null && !(l[i] is Undefined)) sb.Append(ToStr(l[i]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Shortest decimal string that parses back to exactly <paramref name="d"/>. double.ToString("R") is not
        /// portable: modern .NET returns the shortest form but Unity's Mono/IL2CPP return 17 digits
        /// (0.1+0.2 -> "0.30000000000000004" everywhere, but 1/3 -> "0.33333333333333331" in Unity).
        /// </summary>
        public static string Shortest(double d)
        {
            for (int digits = 15; digits < 17; digits++)
            {
                string s = d.ToString("G" + digits, CultureInfo.InvariantCulture);
                if (double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture) == d) return s;
            }
            return d.ToString("G17", CultureInfo.InvariantCulture);
        }

        /// <summary>Number.prototype.toString() (ECMAScript Number::toString).</summary>
        public static string NumberToString(double d)
        {
            if (double.IsNaN(d)) return "NaN";
            if (d == 0) return "0";
            if (double.IsPositiveInfinity(d)) return "Infinity";
            if (double.IsNegativeInfinity(d)) return "-Infinity";
            string sign = d < 0 ? "-" : "";
            string r = Shortest(Math.Abs(d));

            int exp = 0;
            int eIdx = r.IndexOfAny(new[] { 'E', 'e' });
            string mant = r;
            if (eIdx >= 0) { exp = int.Parse(r.Substring(eIdx + 1), CultureInfo.InvariantCulture); mant = r.Substring(0, eIdx); }
            int dot = mant.IndexOf('.');
            string digits = dot < 0 ? mant : mant.Remove(dot, 1);
            int n = (dot < 0 ? mant.Length : dot) + exp;          // position of the decimal point
            int lead = 0;
            while (lead < digits.Length - 1 && digits[lead] == '0') lead++;
            digits = digits.Substring(lead); n -= lead;
            digits = digits.TrimEnd('0');
            if (digits.Length == 0) return "0";
            int k = digits.Length;

            if (k <= n && n <= 21) return sign + digits + new string('0', n - k);
            if (0 < n && n <= 21) return sign + digits.Substring(0, n) + "." + digits.Substring(n);
            if (-6 < n && n <= 0) return sign + "0." + new string('0', -n) + digits;
            int e = n - 1;
            string es = (e < 0 ? "-" : "+") + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
            return sign + (k == 1 ? digits : digits[0] + "." + digits.Substring(1)) + "e" + es;
        }

        /// <summary>JS == (abstract equality) for the value kinds used here.</summary>
        public static bool LooseEquals(object a, object b)
        {
            bool an = a == null || a is Undefined, bn = b == null || b is Undefined;
            if (an || bn) return an && bn;
            if (a is List<object> || b is List<object>) return ReferenceEquals(a, b);
            if (a is string sa && b is string sb) return sa == sb;
            return ToNumber(a) == ToNumber(b);
        }

        /// <summary>JS &lt; &gt; &lt;= &gt;=: strings compare ordinally, everything else numerically.</summary>
        public static int? Compare(object a, object b)
        {
            if (a is string sa && b is string sb) return Math.Sign(string.CompareOrdinal(sa, sb));
            double x = ToNumber(a), y = ToNumber(b);
            if (double.IsNaN(x) || double.IsNaN(y)) return null;
            return x < y ? -1 : x > y ? 1 : 0;
        }

        /// <summary>JS ===.</summary>
        public static bool StrictEquals(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is Undefined || b is Undefined) return a is Undefined && b is Undefined;
            if (a is double x && b is double y) return x == y;
            if (a is string s && b is string t) return s == t;
            if (a is bool p && b is bool q) return p == q;
            return ReferenceEquals(a, b);
        }

        /// <summary>
        /// Array/String.prototype.slice index normalisation: negative indices count from the end, everything clamps,
        /// fractions truncate. <paramref name="end"/> null means "to the end". Returns [start, end) with end &gt;= start.
        /// </summary>
        public static void SliceBounds(int length, double start, double? end, out int from, out int to)
        {
            from = Rel(start, length);
            to = end.HasValue ? Rel(end.Value, length) : length;
            if (to < from) to = from;
        }

        static int Rel(double x, int len)
        {
            if (double.IsNaN(x)) x = 0;
            x = x < 0 ? Math.Ceiling(x) : Math.Floor(x);      // ToIntegerOrInfinity
            if (x < 0) return (int)Math.Max(len + x, 0);
            return (int)Math.Min(x, len);
        }

        /// <summary>
        /// Array.prototype.sort as V8 runs it for short arrays (run detection + binary insertion). Matters when a
        /// comparator is inconsistent, e.g. Blockly's text compare never returns 0, so tie order must match V8.
        /// Longer arrays use a stable merge sort, which agrees with V8 for consistent comparators.
        /// </summary>
        public static List<object> Sort(List<object> input, Func<object, object, double> compare)
        {
            var a = new List<object>(input);
            int n = a.Count;
            if (n < 2) return a;
            Func<object, object, int> cmp = (x, y) =>
            {
                double v = compare(x, y);
                return double.IsNaN(v) ? 0 : v < 0 ? -1 : v > 0 ? 1 : 0;
            };
            if (n >= 64)
            {
                var indexed = new List<KeyValuePair<int, object>>();
                for (int i = 0; i < n; i++) indexed.Add(new KeyValuePair<int, object>(i, a[i]));
                indexed.Sort((p, q) => { int c = cmp(p.Value, q.Value); return c != 0 ? c : p.Key.CompareTo(q.Key); });
                return indexed.ConvertAll(kv => kv.Value);
            }

            // CountAndMakeRun
            int run = 2;
            bool descending = cmp(a[1], a[0]) < 0;
            object prev = a[1];
            for (int i = 2; i < n; i++)
            {
                int order = cmp(a[i], prev);
                if (descending ? order >= 0 : order < 0) break;
                prev = a[i];
                run++;
            }
            if (descending) a.Reverse(0, run);

            // BinaryInsertionSort
            for (int start = run; start < n; start++)
            {
                int left = 0, right = start;
                object pivot = a[start];
                while (left < right)
                {
                    int mid = left + ((right - left) >> 1);
                    if (cmp(pivot, a[mid]) < 0) right = mid; else left = mid + 1;
                }
                for (int p = start; p > left; p--) a[p] = a[p - 1];
                a[left] = pivot;
            }
            return a;
        }

        /// <summary>JS + operator: string concatenation if either side is a string/list, otherwise numeric.</summary>
        public static object Add(object a, object b)
        {
            if (a is string || b is string || a is List<object> || b is List<object>) return ToStr(a) + ToStr(b);
            return ToNumber(a) + ToNumber(b);
        }
    }
}
