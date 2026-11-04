using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Blockly
{
    /// <summary>Behaviour of Blockly's built-in blocks (logic, loops, math, text, lists, variables).</summary>
    static class StandardHandlers
    {
        const double Pi = Math.PI;

        public static void RegisterAll(HandlerRegistry r)
        {
            Logic(r); Loops(r); Math_(r); Text(r); Lists(r); Variables(r); Procedures(r); MoreTextAndLists(r);
        }

        // ---------------------------------------------------------------- logic
        static void Logic(HandlerRegistry r)
        {
            r.Expression("logic_boolean", (it, b) => b.GetFieldValue("BOOL") == "TRUE");
            r.Expression("logic_null", (it, b) => null);
            r.Expression("logic_negate", (it, b) => !JsValue.Truthy(it.EvalInput(b, "BOOL", true)));
            r.Expression("logic_ternary", (it, b) =>
                JsValue.Truthy(it.EvalInput(b, "IF", false)) ? it.EvalInput(b, "THEN", null) : it.EvalInput(b, "ELSE", null));
            r.Expression("logic_operation", (it, b) =>
            {
                var a = it.EvalInput(b, "A", false);
                if (b.GetFieldValue("OP") == "AND")
                    return JsValue.Truthy(a) ? it.EvalInput(b, "B", false) : a;
                return JsValue.Truthy(a) ? a : it.EvalInput(b, "B", false);
            });
            r.Expression("logic_compare", (it, b) =>
            {
                var a = it.EvalInput(b, "A", null);
                var c = it.EvalInput(b, "B", null);
                switch (b.GetFieldValue("OP"))
                {
                    case "EQ": return JsValue.LooseEquals(a, c);
                    case "NEQ": return !JsValue.LooseEquals(a, c);
                    case "LT": return JsValue.Compare(a, c) is int lt && lt < 0;
                    case "LTE": return JsValue.Compare(a, c) is int le && le <= 0;
                    case "GT": return JsValue.Compare(a, c) is int gt && gt > 0;
                    default: return JsValue.Compare(a, c) is int ge && ge >= 0;
                }
            });

            r.Statement("controls_if", ControlsIf);
        }

        static IEnumerator ControlsIf(Interpreter it, Block b)
        {
            for (int i = 0; b.GetInput("IF" + i) != null; i++)
            {
                if (JsValue.Truthy(it.EvalInput(b, "IF" + i, false)))
                {
                    var body = it.RunChain(b.GetInputTargetBlock("DO" + i));
                    while (body.MoveNext()) yield return null;
                    yield break;
                }
            }
            if (b.GetInput("ELSE") != null)
            {
                var body = it.RunChain(b.GetInputTargetBlock("ELSE"));
                while (body.MoveNext()) yield return null;
            }
        }

        // ---------------------------------------------------------------- loops
        /// <summary>Runs a loop body once. Returns false when the loop must end (break / return).</summary>
        static bool AfterBody(Interpreter it)
        {
            switch (it.Pending)
            {
                case Signal.Break: it.Pending = Signal.None; return false;
                case Signal.Continue: it.Pending = Signal.None; return true;
                case Signal.Return: return false;
                default: return true;
            }
        }

        static void Loops(HandlerRegistry r)
        {
            r.Statement("controls_repeat_ext", (it, b) => Repeat(it, b, JsValue.ToNumber(it.EvalInput(b, "TIMES", 0.0))));
            r.Statement("controls_repeat", (it, b) => Repeat(it, b, System.Math.Floor(JsValue.ToNumber(b.GetFieldValue("TIMES")))));
            r.Statement("controls_whileUntil", WhileUntil);
            r.Statement("controls_for", For);
            r.Statement("controls_forEach", ForEach);
            r.Simple("controls_flow_statements", (it, b) =>
                it.Pending = b.GetFieldValue("FLOW") == "BREAK" ? Signal.Break : Signal.Continue);
        }

        static IEnumerator Repeat(Interpreter it, Block b, double times)
        {
            for (double i = 0; i < times; i++)
            {
                var body = it.RunChain(b.GetInputTargetBlock("DO"));
                while (body.MoveNext()) yield return null;
                if (!AfterBody(it)) yield break;
                yield return null; // guarantees progress even with an empty body
            }
        }

        static IEnumerator WhileUntil(Interpreter it, Block b)
        {
            bool until = b.GetFieldValue("MODE") == "UNTIL";
            while (JsValue.Truthy(it.EvalInput(b, "BOOL", false)) != until)
            {
                var body = it.RunChain(b.GetInputTargetBlock("DO"));
                while (body.MoveNext()) yield return null;
                if (!AfterBody(it)) yield break;
                yield return null;
            }
        }

        static IEnumerator For(Interpreter it, Block b)
        {
            string id = b.GetFieldValue("VAR");
            double from = JsValue.ToNumber(it.EvalInput(b, "FROM", 0.0));
            double to = JsValue.ToNumber(it.EvalInput(b, "TO", 0.0));
            double inc = System.Math.Abs(JsValue.ToNumber(it.EvalInput(b, "BY", 1.0)));
            if (from > to) inc = -inc;
            for (double i = from; inc >= 0 ? i <= to : i >= to; i += inc)
            {
                it.SetVariable(id, i);
                var body = it.RunChain(b.GetInputTargetBlock("DO"));
                while (body.MoveNext()) yield return null;
                if (!AfterBody(it)) yield break;
                yield return null;
            }
        }

        static IEnumerator ForEach(Interpreter it, Block b)
        {
            string id = b.GetFieldValue("VAR");
            var list = it.EvalInput(b, "LIST", null) as List<object>;
            if (list == null) yield break;
            for (int i = 0; i < list.Count; i++)
            {
                it.SetVariable(id, list[i]);
                var body = it.RunChain(b.GetInputTargetBlock("DO"));
                while (body.MoveNext()) yield return null;
                if (!AfterBody(it)) yield break;
                yield return null;
            }
        }

        // ---------------------------------------------------------------- math
        static double Num(Interpreter it, Block b, string input, double def = 0) => JsValue.ToNumber(it.EvalInput(b, input, def));

        static void Math_(HandlerRegistry r)
        {
            r.Expression("math_number", (it, b) => JsValue.ToNumber(b.GetFieldValue("NUM")));
            r.Expression("math_arithmetic", (it, b) =>
            {
                var a = it.EvalInput(b, "A", 0.0);
                var c = it.EvalInput(b, "B", 0.0);
                switch (b.GetFieldValue("OP"))
                {
                    case "ADD": return JsValue.Add(a, c);
                    case "MINUS": return JsValue.ToNumber(a) - JsValue.ToNumber(c);
                    case "MULTIPLY": return JsValue.ToNumber(a) * JsValue.ToNumber(c);
                    case "DIVIDE": return JsValue.ToNumber(a) / JsValue.ToNumber(c);
                    default: return System.Math.Pow(JsValue.ToNumber(a), JsValue.ToNumber(c));
                }
            });
            r.Expression("math_single", (it, b) =>
            {
                double x = Num(it, b, "NUM");
                switch (b.GetFieldValue("OP"))
                {
                    case "ROOT": return System.Math.Sqrt(x);
                    case "ABS": return System.Math.Abs(x);
                    case "NEG": return -x;
                    case "LN": return System.Math.Log(x);
                    case "LOG10": return System.Math.Log(x) / System.Math.Log(10);
                    case "EXP": return System.Math.Exp(x);
                    default: return System.Math.Pow(10, x);
                }
            });
            r.Expression("math_trig", (it, b) =>
            {
                double x = Num(it, b, "NUM");
                switch (b.GetFieldValue("OP"))
                {
                    case "SIN": return System.Math.Sin(x / 180 * Pi);
                    case "COS": return System.Math.Cos(x / 180 * Pi);
                    case "TAN": return System.Math.Tan(x / 180 * Pi);
                    case "ASIN": return System.Math.Asin(x) / Pi * 180;
                    case "ACOS": return System.Math.Acos(x) / Pi * 180;
                    default: return System.Math.Atan(x) / Pi * 180;
                }
            });
            r.Expression("math_constant", (it, b) =>
            {
                switch (b.GetFieldValue("CONSTANT") ?? b.GetFieldValue("OP"))
                {
                    case "PI": return Pi;
                    case "E": return System.Math.E;
                    case "GOLDEN_RATIO": return (1 + System.Math.Sqrt(5)) / 2;
                    case "SQRT2": return System.Math.Sqrt(2);
                    case "SQRT1_2": return System.Math.Sqrt(0.5);
                    default: return double.PositiveInfinity;
                }
            });
            r.Expression("math_number_property", (it, b) =>
            {
                double n = Num(it, b, "NUMBER_TO_CHECK");
                switch (b.GetFieldValue("PROPERTY"))
                {
                    case "EVEN": return n % 2 == 0;
                    case "ODD": return n % 2 == 1;
                    case "WHOLE": return n % 1 == 0;
                    case "POSITIVE": return n > 0;
                    case "NEGATIVE": return n < 0;
                    case "DIVISIBLE_BY": return n % Num(it, b, "DIVISOR") == 0;
                    default: return IsPrime(n);
                }
            });
            r.Expression("math_round", (it, b) =>
            {
                double x = Num(it, b, "NUM");
                switch (b.GetFieldValue("OP"))
                {
                    case "ROUND": return System.Math.Floor(x + 0.5);      // JS Math.round: half rounds toward +infinity
                    case "ROUNDUP": return System.Math.Ceiling(x);
                    default: return System.Math.Floor(x);
                }
            });
            r.Expression("math_modulo", (it, b) => Num(it, b, "DIVIDEND") % Num(it, b, "DIVISOR"));
            r.Expression("math_constrain", (it, b) =>
                System.Math.Min(System.Math.Max(Num(it, b, "VALUE"), Num(it, b, "LOW", 0)), Num(it, b, "HIGH", double.PositiveInfinity)));
            r.Expression("math_random_int", (it, b) =>
            {
                double a = Num(it, b, "FROM", 1), c = Num(it, b, "TO", 100);
                if (a > c) { var t = a; a = c; c = t; }
                return System.Math.Floor(it.Random.NextDouble() * (c - a + 1) + a);
            });
            r.Expression("math_random_float", (it, b) => it.Random.NextDouble());
            r.Expression("math_on_list", MathOnList);
            r.Simple("math_change", (it, b) =>
            {
                string id = b.GetFieldValue("VAR");
                var cur = it.GetVariable(id);
                it.SetVariable(id, (cur is double d ? d : 0) + Num(it, b, "DELTA"));
            });
        }

        static bool IsPrime(double n)
        {
            if (n == 2 || n == 3) return true;
            if (n <= 1 || n % 2 == 0 || n % 3 == 0) return false;
            for (double x = 6; x <= System.Math.Sqrt(n) + 1; x += 6)
                if (n % (x - 1) == 0 || n % (x + 1) == 0) return false;
            return true;
        }

        static object MathOnList(Interpreter it, Block b)
        {
            var list = it.EvalInput(b, "LIST", null) as List<object> ?? new List<object>();
            var nums = list.Select(JsValue.ToNumber).ToList();
            switch (b.GetFieldValue("OP"))
            {
                case "SUM": return nums.Sum();
                case "MIN": return nums.Count == 0 ? double.PositiveInfinity : nums.Min();
                case "MAX": return nums.Count == 0 ? double.NegativeInfinity : nums.Max();
                case "AVERAGE": return nums.Sum() / nums.Count;
                case "MEDIAN":
                    {
                        if (nums.Count == 0) return null;
                        var s = nums.OrderByDescending(x => x).ToList();
                        return s.Count % 2 == 0 ? (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2 : s[(s.Count - 1) / 2];
                    }
                case "MODE":
                    {
                        var counts = new List<KeyValuePair<double, int>>();
                        foreach (var x in nums)
                        {
                            int idx = counts.FindIndex(kv => kv.Key == x);
                            if (idx < 0) counts.Add(new KeyValuePair<double, int>(x, 1));
                            else counts[idx] = new KeyValuePair<double, int>(x, counts[idx].Value + 1);
                        }
                        int max = counts.Count == 0 ? 0 : counts.Max(kv => kv.Value);
                        return counts.Where(kv => kv.Value == max).Select(kv => (object)kv.Key).ToList();
                    }
                case "STD_DEV":
                    {
                        if (nums.Count == 0) return null;
                        double mean = nums.Sum() / nums.Count;
                        return System.Math.Sqrt(nums.Sum(x => (x - mean) * (x - mean)) / nums.Count);
                    }
                default: // RANDOM
                    return list.Count == 0 ? null : list[it.Random.Next(list.Count)];
            }
        }

        // ---------------------------------------------------------------- text
        static string Str(Interpreter it, Block b, string input, string def = "") => JsValue.ToStr(it.EvalInput(b, input, def));

        static void Text(HandlerRegistry r)
        {
            r.Expression("text", (it, b) => b.GetFieldValue("TEXT") ?? "");
            r.Expression("text_join", (it, b) =>
            {
                int n = 0;
                while (b.GetInput("ADD" + n) != null) n++;
                if (n == 0) return "";
                if (n == 1) return JsValue.ToStr(it.EvalInput(b, "ADD0", ""));
                var sb = new StringBuilder();
                for (int i = 0; i < n; i++) sb.Append(JsValue.ToStr(it.EvalInput(b, "ADD" + i, "")));
                return sb.ToString();
            });
            r.Simple("text_append", (it, b) =>
            {
                string id = b.GetFieldValue("VAR");
                it.SetVariable(id, JsValue.ToStr(it.GetVariable(id)) + Str(it, b, "TEXT"));
            });
            r.Expression("text_length", (it, b) =>
            {
                var v = it.EvalInput(b, "VALUE", "");
                return v is List<object> l ? l.Count : (double)(v is string s ? s.Length : v == null || v is Undefined ? 0 : JsValue.ToStr(v).Length);
            });
            r.Expression("text_isEmpty", (it, b) =>
            {
                var v = it.EvalInput(b, "VALUE", "");
                return v == null || v is Undefined || (v is string s && s.Length == 0) || (v is List<object> l && l.Count == 0);
            });
            r.Expression("text_indexOf", (it, b) =>
            {
                string text = Str(it, b, "VALUE"), find = Str(it, b, "FIND");
                int idx = b.GetFieldValue("END") == "FIRST"
                    ? text.IndexOf(find, StringComparison.Ordinal)
                    : (find.Length == 0 ? text.Length : text.LastIndexOf(find, StringComparison.Ordinal));
                return (double)(idx + 1);
            });
            r.Expression("text_changeCase", (it, b) =>
            {
                string t = Str(it, b, "TEXT");
                switch (b.GetFieldValue("CASE"))
                {
                    case "UPPERCASE": return t.ToUpperInvariant();
                    case "LOWERCASE": return t.ToLowerInvariant();
                    default: return TitleCase(t);
                }
            });
            r.Expression("text_trim", (it, b) =>
            {
                string t = Str(it, b, "TEXT");
                switch (b.GetFieldValue("MODE"))
                {
                    case "LEFT": return t.TrimStart();
                    case "RIGHT": return t.TrimEnd();
                    default: return t.Trim();
                }
            });
            r.Expression("text_replace", (it, b) =>
            {
                string from = Str(it, b, "FROM"), to = Str(it, b, "TO"), text = Str(it, b, "TEXT");
                return from.Length == 0 ? text : text.Replace(from, to);
            });
            r.Expression("text_reverse", (it, b) =>
            {
                var a = Str(it, b, "TEXT").ToCharArray();
                Array.Reverse(a);
                return new string(a);
            });
            r.Simple("text_print", (it, b) => it.Print(JsValue.ToStr(it.EvalInput(b, "TEXT", ""))));
        }

        static string TitleCase(string t)
        {
            var sb = new StringBuilder();
            bool start = true;
            foreach (char c in t)
            {
                if (char.IsWhiteSpace(c)) { sb.Append(c); start = true; }
                else { sb.Append(start ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c)); start = false; }
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- lists
        static List<object> ListIn(Interpreter it, Block b, string input) => it.EvalInput(b, input, null) as List<object>;

        static void Lists(HandlerRegistry r)
        {
            r.Expression("lists_create_empty", (it, b) => new List<object>());
            r.Expression("lists_create_with", (it, b) =>
            {
                var l = new List<object>();
                for (int i = 0; b.GetInput("ADD" + i) != null; i++) l.Add(it.EvalInput(b, "ADD" + i, null));
                return l;
            });
            r.Expression("lists_repeat", (it, b) =>
            {
                var item = it.EvalInput(b, "ITEM", null);
                int n = (int)Num(it, b, "NUM");
                var l = new List<object>();
                for (int i = 0; i < n; i++) l.Add(item);
                return l;
            });
            r.Expression("lists_length", (it, b) =>
            {
                var v = it.EvalInput(b, "VALUE", null);
                return (double)(v is List<object> l ? l.Count : v is string s ? s.Length : 0);
            });
            r.Expression("lists_isEmpty", (it, b) =>
            {
                var v = it.EvalInput(b, "VALUE", null);
                return v == null || v is Undefined || (v is List<object> l && l.Count == 0) || (v is string s && s.Length == 0);
            });
            r.Expression("lists_getIndex", (it, b) => GetIndex(it, b));
            r.Simple("lists_getIndex", (it, b) => GetIndex(it, b));
            r.Simple("lists_setIndex", SetIndex);
        }

        /// <summary>Resolves a WHERE/AT pair to a zero-based index, or -1 when out of range.</summary>
        static int ResolveIndex(Interpreter it, Block b, int count, bool forInsert)
        {
            switch (b.GetFieldValue("WHERE"))
            {
                case "FIRST": return 0;
                case "LAST": return forInsert ? count : count - 1;
                case "FROM_START": return (int)Num(it, b, "AT", 1) - 1;
                case "FROM_END": return count - (int)Num(it, b, "AT", 1);   // same for get/remove/set/insert
                default: return count == 0 ? -1 : it.Random.Next(count);
            }
        }

        static object GetIndex(Interpreter it, Block b)
        {
            var list = ListIn(it, b, "VALUE") ?? new List<object>();
            string mode = b.GetFieldValue("MODE");
            int idx = ResolveIndex(it, b, list.Count, false);
            bool valid = idx >= 0 && idx < list.Count;
            object value = valid ? list[idx] : Undefined.Value;
            if (valid && mode != "GET") list.RemoveAt(idx);
            return mode == "REMOVE" ? null : value;
        }

        static void SetIndex(Interpreter it, Block b)
        {
            var list = ListIn(it, b, "LIST");
            if (list == null) return;
            var value = it.EvalInput(b, "TO", null);
            if (b.GetFieldValue("MODE") == "INSERT")
            {
                int idx = ResolveIndex(it, b, list.Count, true);
                if (idx < 0) idx = 0;
                if (idx > list.Count) idx = list.Count;
                list.Insert(idx, value);
            }
            else
            {
                int idx = ResolveIndex(it, b, list.Count, false);
                if (idx < 0) return;
                while (list.Count <= idx) list.Add(Undefined.Value);
                list[idx] = value;
            }
        }

        // ---------------------------------------------------------------- variables
        static void Variables(HandlerRegistry r)
        {
            r.Expression("variables_get", (it, b) => it.GetVariable(b.GetFieldValue("VAR")));
            r.Simple("variables_set", (it, b) => it.SetVariable(b.GetFieldValue("VAR"), it.EvalInput(b, "VALUE", Undefined.Value)));
        }

        // ---------------------------------------------------------------- procedures
        static void Procedures(HandlerRegistry r)
        {
            r.Statement("procedures_callnoreturn", (it, b) => it.CallStatement(b));
            r.Expression("procedures_callreturn", (it, b) => it.CallValue(b));
            r.Simple("procedures_ifreturn", (it, b) =>
            {
                if (!JsValue.Truthy(it.EvalInput(b, "CONDITION", false))) return;
                it.ReturnValue = b.GetInput("VALUE") != null ? it.EvalInput(b, "VALUE", Undefined.Value) : Undefined.Value;
                it.Pending = Signal.Return;
            });
        }

        // ---------------------------------------------------------------- slices, sorting, split/join
        /// <summary>Start/end of a substring or sublist exactly as Blockly's JS generator computes them.</summary>
        static void WhereRange(Interpreter it, Block b, int length, string w1, string a1, string w2, string a2, out int from, out int to)
        {
            double start = 0;
            switch (b.GetFieldValue(w1))
            {
                case "FROM_START": start = Num(it, b, a1, 1) - 1; break;
                case "FROM_END": start = length - Num(it, b, a1, 1); break;
                default: start = 0; break;                                  // FIRST
            }
            double? end = null;
            switch (b.GetFieldValue(w2))
            {
                case "FROM_START": end = Num(it, b, a2, 1); break;           // (at - 1) + 1
                case "FROM_END": end = length - Num(it, b, a2, 1) + 1; break;
                default: end = null; break;                                  // LAST
            }
            JsValue.SliceBounds(length, start, end, out from, out to);
        }

        static void MoreTextAndLists(HandlerRegistry r)
        {
            r.Expression("text_charAt", (it, b) =>
            {
                string t = Str(it, b, "VALUE");
                int idx;
                switch (b.GetFieldValue("WHERE"))
                {
                    case "FROM_START":
                        idx = (int)(Num(it, b, "AT", 1) - 1);
                        return idx >= 0 && idx < t.Length ? t[idx].ToString() : "";
                    case "FROM_END":
                        JsValue.SliceBounds(t.Length, -Num(it, b, "AT", 1), null, out idx, out _);
                        return idx < t.Length ? t[idx].ToString() : "";
                    case "FIRST": return t.Length > 0 ? t[0].ToString() : "";
                    case "LAST": return t.Length > 0 ? t[t.Length - 1].ToString() : "";
                    default: return t.Length > 0 ? t[(int)System.Math.Floor(it.Random.NextDouble() * t.Length)].ToString() : "";
                }
            });
            r.Expression("text_getSubstring", (it, b) =>
            {
                string t = Str(it, b, "STRING");
                WhereRange(it, b, t.Length, "WHERE1", "AT1", "WHERE2", "AT2", out int from, out int to);
                return t.Substring(from, to - from);
            });
            r.Expression("lists_getSublist", (it, b) =>
            {
                var l = ListIn(it, b, "LIST") ?? new List<object>();
                WhereRange(it, b, l.Count, "WHERE1", "AT1", "WHERE2", "AT2", out int from, out int to);
                return l.GetRange(from, to - from);
            });
            r.Expression("text_count", (it, b) =>
            {
                string sub = Str(it, b, "SUB"), text = Str(it, b, "TEXT");
                if (sub.Length == 0) return (double)(text.Length + 1);
                int count = 0, pos = 0;
                while ((pos = text.IndexOf(sub, pos, StringComparison.Ordinal)) >= 0) { count++; pos += sub.Length; }
                return (double)count;
            });
            r.Expression("lists_indexOf", (it, b) =>
            {
                var l = ListIn(it, b, "VALUE") ?? new List<object>();
                var item = it.EvalInput(b, "FIND", Undefined.Value);
                bool first = b.GetFieldValue("END") == "FIRST";
                for (int k = 0; k < l.Count; k++)
                {
                    int i = first ? k : l.Count - 1 - k;
                    if (JsValue.StrictEquals(l[i], item)) return (double)(i + 1);
                }
                return 0.0;
            });
            r.Expression("lists_reverse", (it, b) =>
            {
                var l = new List<object>(ListIn(it, b, "LIST") ?? new List<object>());
                l.Reverse();
                return l;
            });
            r.Expression("lists_split", (it, b) =>
            {
                string delim = Str(it, b, "DELIM");
                if (b.GetFieldValue("MODE") == "JOIN")
                    return JsValue.JoinList(ListIn(it, b, "INPUT") ?? new List<object>(), delim);
                string text = Str(it, b, "INPUT");
                var result = new List<object>();
                if (delim.Length == 0) { foreach (char c in text) result.Add(c.ToString()); }
                else foreach (var part in text.Split(new[] { delim }, StringSplitOptions.None)) result.Add(part);
                return result;
            });
            r.Expression("lists_sort", (it, b) =>
            {
                var l = ListIn(it, b, "LIST") ?? new List<object>();
                double dir = JsValue.ToNumber(b.GetFieldValue("DIRECTION"));
                Func<object, object, double> cmp;
                switch (b.GetFieldValue("TYPE"))
                {
                    case "NUMERIC": cmp = (x, y) => JsValue.ToNumber(x) - JsValue.ToNumber(y); break;
                    case "IGNORE_CASE":
                        cmp = (x, y) => string.CompareOrdinal(JsValue.ToStr(x).ToLowerInvariant(), JsValue.ToStr(y).ToLowerInvariant()) > 0 ? 1 : -1; break;
                    default: cmp = (x, y) => string.CompareOrdinal(JsValue.ToStr(x), JsValue.ToStr(y)) > 0 ? 1 : -1; break;
                }
                return JsValue.Sort(l, (x, y) => cmp(x, y) * dir);
            });
            r.Expression("math_atan2", (it, b) => System.Math.Atan2(Num(it, b, "Y"), Num(it, b, "X")) / Pi * 180);
        }
    }
}
