using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Blockly.Json
{
    /// <summary>
    /// Minimal dependency-free JSON reader/writer (IL2CPP/WebGL safe, no reflection).
    /// Values map to: Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double, bool, null.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var p = new Parser(text);
            p.SkipWs();
            var v = p.ReadValue();
            p.SkipWs();
            if (!p.End) throw p.Error("Unexpected trailing characters");
            return v;
        }

        public static string Write(object value, bool indented = false)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, indented, 0);
            return sb.ToString();
        }

        // ---- typed accessors (tolerant: return default on missing/mismatch) ----
        public static Dictionary<string, object> AsObject(object o) => o as Dictionary<string, object>;
        public static List<object> AsArray(object o) => o as List<object>;

        public static string GetString(Dictionary<string, object> d, string key, string def = null)
        {
            if (d != null && d.TryGetValue(key, out var v) && v != null)
            {
                if (v is string s) return s;
                if (v is double n) return JsValue.Shortest(n);
                if (v is bool b) return b ? "true" : "false";
            }
            return def;
        }

        public static double GetNumber(Dictionary<string, object> d, string key, double def = 0)
            => d != null && d.TryGetValue(key, out var v) && v is double n ? n : def;

        public static bool GetBool(Dictionary<string, object> d, string key, bool def = false)
            => d != null && d.TryGetValue(key, out var v) && v is bool b ? b : def;

        public static Dictionary<string, object> GetObject(Dictionary<string, object> d, string key)
            => d != null && d.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> GetArray(Dictionary<string, object> d, string key)
            => d != null && d.TryGetValue(key, out var v) ? v as List<object> : null;

        // ---- writer ----
        static void WriteValue(StringBuilder sb, object v, bool ind, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double d: WriteNumber(sb, d); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case float f: WriteNumber(sb, f); break;
                case Dictionary<string, object> obj:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in obj)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        NewLine(sb, ind, depth + 1);
                        WriteString(sb, kv.Key);
                        sb.Append(ind ? ": " : ":");
                        WriteValue(sb, kv.Value, ind, depth + 1);
                    }
                    if (!first) NewLine(sb, ind, depth);
                    sb.Append('}');
                    break;
                case List<object> arr:
                    sb.Append('[');
                    for (int k = 0; k < arr.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        NewLine(sb, ind, depth + 1);
                        WriteValue(sb, arr[k], ind, depth + 1);
                    }
                    if (arr.Count > 0) NewLine(sb, ind, depth);
                    sb.Append(']');
                    break;
                default: throw new ArgumentException("Unsupported JSON type: " + v.GetType());
            }
        }

        static void NewLine(StringBuilder sb, bool ind, int depth)
        {
            if (!ind) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        static void WriteNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            if (d == Math.Floor(d) && Math.Abs(d) < 1e15) sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
            else sb.Append(JsValue.Shortest(d));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- parser ----
        sealed class Parser
        {
            readonly string s;
            int i;
            public Parser(string text) { s = text; i = text.Length > 0 && text[0] == '﻿' ? 1 : 0; }
            public bool End => i >= s.Length;

            public Exception Error(string msg) => new FormatException($"JSON: {msg} at position {i}");

            public void SkipWs() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            public object ReadValue()
            {
                if (End) throw Error("Unexpected end");
                char c = s[i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            void Expect(string lit)
            {
                if (string.CompareOrdinal(s, i, lit, 0, lit.Length) != 0) throw Error("Invalid literal");
                i += lit.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>();
                i++; SkipWs();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (End || s[i] != '"') throw Error("Expected string key");
                    string key = ReadString();
                    SkipWs();
                    if (End || s[i] != ':') throw Error("Expected ':'");
                    i++; SkipWs();
                    d[key] = ReadValue();
                    SkipWs();
                    if (End) throw Error("Unterminated object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw Error("Expected ',' or '}'");
                }
            }

            List<object> ReadArray()
            {
                var a = new List<object>();
                i++; SkipWs();
                if (i < s.Length && s[i] == ']') { i++; return a; }
                while (true)
                {
                    SkipWs();
                    a.Add(ReadValue());
                    SkipWs();
                    if (End) throw Error("Unterminated array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return a; }
                    throw Error("Expected ',' or ']'");
                }
            }

            string ReadString()
            {
                i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (End) throw Error("Unterminated string");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (End) throw Error("Bad escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Error("Bad unicode escape");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw Error("Bad escape");
                    }
                }
            }

            double ReadNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (start == i) throw Error("Unexpected character '" + s[i] + "'");
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    throw Error("Invalid number");
                return n;
            }
        }
    }
}
