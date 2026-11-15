using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Blockly
{
    /// <summary>How tightly JavaScript operators bind (the numbers of Blockly's own JavaScript generator).</summary>
    static class JsOrder
    {
        public const double Atomic = 0, Member = 1.2, FunctionCall = 2, UnaryNegation = 4.3, LogicalNot = 4.4,
            Multiplication = 5.1, Division = 5.2, Modulus = 5.3, Subtraction = 6.1, Addition = 6.2,
            Relational = 8, Equality = 9, LogicalAnd = 13, LogicalOr = 14, Conditional = 15, Assignment = 16, Comma = 18, None = 99;
    }

    /// <summary>The JavaScript text of the built-in blocks, written to read like the code Blockly itself generates.</summary>
    static class JavaScriptGenerators
    {
        static readonly string[] ReservedWords =
        {
            "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do", "else", "enum", "export", "extends",
            "false", "finally", "for", "function", "if", "implements", "import", "in", "instanceof", "interface", "let", "new", "null", "package",
            "private", "protected", "public", "return", "static", "super", "switch", "this", "throw", "true", "try", "typeof", "var", "void",
            "while", "with", "yield", "await", "async", "arguments", "eval", "undefined", "NaN", "Infinity", "Math", "Number", "String", "Array",
            "Object", "Boolean", "Date", "JSON", "RegExp", "Error", "Symbol", "Map", "Set", "parseInt", "parseFloat", "isNaN", "isFinite",
            "console", "window", "document", "globalThis", "name", "length",
        };

        internal static CodeLanguage Create()
        {
            var l = new CodeLanguage("JavaScript") { Indent = "  ", FileExtension = ".js", DefaultPrint = "console.log", Quote = Quote };
            foreach (var w in ReservedWords) l.Reserved.Add(w);
            l.Assemble = (g, variables, defs, code) =>
            {
                var parts = new List<string>();
                if (variables.Count > 0) parts.Add("var " + string.Join(", ", variables) + ";");
                parts.AddRange(defs);
                return parts.Count == 0 ? code : string.Join("\n\n", parts) + "\n\n\n" + code;
            };
            Logic(l); Loops(l); Math_(l); Text(l); Lists(l); Variables(l); Procedures(l); Colour_(l);
            return l;
        }

        // ---------------------------------------------------------------- helpers
        static Code C(string text, double order) => new Code(text, order);

        /// <summary>The code of an input, or <paramref name="def"/> when it is empty.</summary>
        static string V(CodeGenerator g, Block b, string input, double order, string def)
        {
            var s = g.ValueToCode(b, input, order);
            return s.Length == 0 ? def : s;
        }

        static string Quote(string s)
        {
            var sb = new StringBuilder("'");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\u2028': sb.Append("\\u2028"); break;
                    case '\u2029': sb.Append("\\u2029"); break;
                    default:
                        if (c < ' ') sb.Append("\\x").Append(((int)c).ToString("x2")); else sb.Append(c);
                        break;
                }
            }
            return sb.Append('\'').ToString();
        }

        static readonly Regex Identifier = new Regex(@"^\w+$");

        /// <summary>Wraps in String(…) unless it already is a string literal.</summary>
        static Code ForceString(string value)
            => Regex.IsMatch(value, @"^\s*'([^']|\\')*'\s*$") ? C(value, JsOrder.Atomic) : C("String(" + value + ")", JsOrder.FunctionCall);

        static string N(double d) => JsValue.NumberToString(d);

        // ---------------------------------------------------------------- logic
        static void Logic(CodeLanguage l)
        {
            l.Value("logic_boolean", (g, b) => C(b.GetFieldValue("BOOL") == "TRUE" ? "true" : "false", JsOrder.Atomic));
            l.Value("logic_null", (g, b) => C("null", JsOrder.Atomic));
            l.Value("logic_negate", (g, b) => C("!" + V(g, b, "BOOL", JsOrder.LogicalNot, "true"), JsOrder.LogicalNot));
            l.Value("logic_ternary", (g, b) => C(
                V(g, b, "IF", JsOrder.Conditional, "false") + " ? " + V(g, b, "THEN", JsOrder.Conditional, "null") + " : " + V(g, b, "ELSE", JsOrder.Conditional, "null"),
                JsOrder.Conditional));
            l.Value("logic_operation", (g, b) =>
            {
                bool and = b.GetFieldValue("OP") == "AND";
                double order = and ? JsOrder.LogicalAnd : JsOrder.LogicalOr;
                string a = g.ValueToCode(b, "A", order), c = g.ValueToCode(b, "B", order);
                if (a.Length == 0 && c.Length == 0) { a = "false"; c = "false"; }
                else { string def = and ? "true" : "false"; if (a.Length == 0) a = def; if (c.Length == 0) c = def; }
                return C(a + (and ? " && " : " || ") + c, order);
            });
            l.Value("logic_compare", (g, b) =>
            {
                string op; double order = JsOrder.Relational;
                switch (b.GetFieldValue("OP"))
                {
                    case "EQ": op = "=="; order = JsOrder.Equality; break;
                    case "NEQ": op = "!="; order = JsOrder.Equality; break;
                    case "LT": op = "<"; break;
                    case "LTE": op = "<="; break;
                    case "GT": op = ">"; break;
                    default: op = ">="; break;
                }
                return C(V(g, b, "A", order, "0") + " " + op + " " + V(g, b, "B", order, "0"), order);
            });
            l.Statement("controls_if", (g, b) =>
            {
                var sb = new StringBuilder();
                for (int n = 0; b.GetInput("IF" + n) != null; n++)
                {
                    sb.Append(n > 0 ? " else " : "").Append("if (").Append(V(g, b, "IF" + n, JsOrder.None, "false")).Append(") {\n")
                      .Append(g.StatementToCode(b, "DO" + n)).Append('}');
                }
                if (b.GetInput("ELSE") != null) sb.Append(" else {\n").Append(g.StatementToCode(b, "ELSE")).Append('}');
                return sb.Append('\n').ToString();
            });
        }

        // ---------------------------------------------------------------- loops
        static void Loops(CodeLanguage l)
        {
            StatementGenerator repeat = (g, b) =>
            {
                string repeats = b.GetInput("TIMES")?.Connection != null
                    ? V(g, b, "TIMES", JsOrder.Assignment, "0")
                    : N(JsValue.ToNumber(b.GetFieldValue("TIMES")));
                string branch = g.StatementToCode(b, "DO"), code = "", endVar = repeats;
                string loopVar = g.NewName("count");
                if (!CodeGenerator.IsNumber(repeats))
                {
                    endVar = g.NewName("repeat_end");
                    code += "var " + endVar + " = " + repeats + ";\n";
                }
                return code + "for (var " + loopVar + " = 0; " + loopVar + " < " + endVar + "; " + loopVar + "++) {\n" + branch + "}\n";
            };
            l.Statement("controls_repeat_ext", repeat);
            l.Statement("controls_repeat", repeat);
            l.Statement("controls_whileUntil", (g, b) =>
            {
                bool until = b.GetFieldValue("MODE") == "UNTIL";
                string cond = V(g, b, "BOOL", until ? JsOrder.LogicalNot : JsOrder.None, "false");
                if (until) cond = "!" + cond;
                return "while (" + cond + ") {\n" + g.StatementToCode(b, "DO") + "}\n";
            });
            l.Statement("controls_for", (g, b) =>
            {
                string v = g.VariableName(b.GetFieldValue("VAR"));
                string from = V(g, b, "FROM", JsOrder.Assignment, "0"), to = V(g, b, "TO", JsOrder.Assignment, "0"), by = V(g, b, "BY", JsOrder.Assignment, "1");
                string branch = g.StatementToCode(b, "DO");
                if (CodeGenerator.IsNumber(from) && CodeGenerator.IsNumber(to) && CodeGenerator.IsNumber(by))
                {
                    bool up = JsValue.ToNumber(from) <= JsValue.ToNumber(to);
                    double step = Math.Abs(JsValue.ToNumber(by));
                    string inc = step == 1 ? (up ? v + "++" : v + "--") : v + (up ? " += " : " -= ") + N(step);
                    return "for (" + v + " = " + from + "; " + v + (up ? " <= " : " >= ") + to + "; " + inc + ") {\n" + branch + "}\n";
                }
                string code = "", startVar = from, endVar = to;
                if (!CodeGenerator.IsNumber(from) && !Identifier.IsMatch(from)) { startVar = g.NewName(v + "_start"); code += "var " + startVar + " = " + from + ";\n"; }
                if (!CodeGenerator.IsNumber(to) && !Identifier.IsMatch(to)) { endVar = g.NewName(v + "_end"); code += "var " + endVar + " = " + to + ";\n"; }
                string incVar = g.NewName(v + "_inc");
                code += "var " + incVar + " = " + (CodeGenerator.IsNumber(by) ? N(Math.Abs(JsValue.ToNumber(by))) : "Math.abs(" + by + ")") + ";\n";
                code += "if (" + startVar + " > " + endVar + ") {\n" + g.Language.Indent + incVar + " = -" + incVar + ";\n}\n";
                return code + "for (" + v + " = " + startVar + "; " + incVar + " >= 0 ? " + v + " <= " + endVar + " : " + v + " >= " + endVar + "; " + v + " += " + incVar + ") {\n" + branch + "}\n";
            });
            l.Statement("controls_forEach", (g, b) =>
            {
                string v = g.VariableName(b.GetFieldValue("VAR"));
                string list = V(g, b, "LIST", JsOrder.Assignment, "[]"), branch = g.StatementToCode(b, "DO"), code = "", listVar = list;
                if (!Identifier.IsMatch(list)) { listVar = g.NewName(v + "_list"); code += "var " + listVar + " = " + list + ";\n"; }
                string index = g.NewName(v + "_index");
                branch = g.Language.Indent + v + " = " + listVar + "[" + index + "];\n" + branch;
                return code + "for (var " + index + " in " + listVar + ") {\n" + branch + "}\n";
            });
            l.Statement("controls_flow_statements", (g, b) => b.GetFieldValue("FLOW") == "BREAK" ? "break;\n" : "continue;\n");
        }

        // ---------------------------------------------------------------- math
        static void Math_(CodeLanguage l)
        {
            l.Value("math_number", (g, b) =>
            {
                double d = JsValue.ToNumber(b.GetFieldValue("NUM"));
                return C(N(d), d < 0 || (d == 0 && 1 / d < 0) ? JsOrder.UnaryNegation : JsOrder.Atomic);
            });
            l.Value("math_arithmetic", (g, b) =>
            {
                string op = b.GetFieldValue("OP");
                if (op == "POWER") return C("Math.pow(" + V(g, b, "A", JsOrder.Comma, "0") + ", " + V(g, b, "B", JsOrder.Comma, "0") + ")", JsOrder.FunctionCall);
                string sym; double order;
                switch (op)
                {
                    case "ADD": sym = " + "; order = JsOrder.Addition; break;
                    case "MINUS": sym = " - "; order = JsOrder.Subtraction; break;
                    case "MULTIPLY": sym = " * "; order = JsOrder.Multiplication; break;
                    default: sym = " / "; order = JsOrder.Division; break;
                }
                return C(V(g, b, "A", order, "0") + sym + V(g, b, "B", order, "0"), order);
            });
            l.Value("math_single", (g, b) =>
            {
                string op = b.GetFieldValue("OP");
                if (op == "NEG")
                {
                    string neg = V(g, b, "NUM", JsOrder.UnaryNegation, "0");
                    if (neg.StartsWith("-")) neg = " " + neg;
                    return C("-" + neg, JsOrder.UnaryNegation);
                }
                string arg = V(g, b, "NUM", op == "LOG10" ? JsOrder.Division : JsOrder.None, "0");
                switch (op)
                {
                    case "ROOT": return C("Math.sqrt(" + arg + ")", JsOrder.FunctionCall);
                    case "ABS": return C("Math.abs(" + arg + ")", JsOrder.FunctionCall);
                    case "LN": return C("Math.log(" + arg + ")", JsOrder.FunctionCall);
                    case "LOG10": return C("Math.log(" + arg + ") / Math.log(10)", JsOrder.Division);
                    case "EXP": return C("Math.exp(" + arg + ")", JsOrder.FunctionCall);
                    default: return C("Math.pow(10," + arg + ")", JsOrder.FunctionCall);
                }
            });
            l.Value("math_trig", (g, b) =>
            {
                string op = b.GetFieldValue("OP");
                if (op == "SIN" || op == "COS" || op == "TAN")
                    return C("Math." + op.ToLowerInvariant() + "(" + V(g, b, "NUM", JsOrder.Division, "0") + " / 180 * Math.PI)", JsOrder.FunctionCall);
                return C("Math.a" + op.Substring(1).ToLowerInvariant() + "(" + V(g, b, "NUM", JsOrder.None, "0") + ") / Math.PI * 180", JsOrder.Division);
            });
            l.Value("math_constant", (g, b) =>
            {
                switch (b.GetFieldValue("CONSTANT") ?? b.GetFieldValue("OP"))
                {
                    case "PI": return C("Math.PI", JsOrder.Member);
                    case "E": return C("Math.E", JsOrder.Member);
                    case "GOLDEN_RATIO": return C("(1 + Math.sqrt(5)) / 2", JsOrder.Division);
                    case "SQRT2": return C("Math.SQRT2", JsOrder.Member);
                    case "SQRT1_2": return C("Math.SQRT1_2", JsOrder.Member);
                    default: return C("Infinity", JsOrder.Atomic);
                }
            });
            l.Value("math_number_property", (g, b) =>
            {
                string prop = b.GetFieldValue("PROPERTY");
                string n = V(g, b, "NUMBER_TO_CHECK", prop == "PRIME" ? JsOrder.None : JsOrder.Modulus, "0");
                switch (prop)
                {
                    case "PRIME":
                        return C(g.Helper("mathIsPrime", MathIsPrime) + "(" + n + ")", JsOrder.FunctionCall);
                    case "EVEN": return C(n + " % 2 === 0", JsOrder.Equality);
                    case "ODD": return C(n + " % 2 === 1", JsOrder.Equality);
                    case "WHOLE": return C(n + " % 1 === 0", JsOrder.Equality);
                    case "POSITIVE": return C(n + " > 0", JsOrder.Relational);
                    case "NEGATIVE": return C(n + " < 0", JsOrder.Relational);
                    default:
                        string divisor = V(g, b, "DIVISOR", JsOrder.Modulus, "0");
                        return divisor == "0" ? C("false", JsOrder.Atomic) : C(n + " % " + divisor + " === 0", JsOrder.Equality);
                }
            });
            l.Value("math_round", (g, b) =>
            {
                string fn = b.GetFieldValue("OP") == "ROUND" ? "round" : b.GetFieldValue("OP") == "ROUNDUP" ? "ceil" : "floor";
                return C("Math." + fn + "(" + V(g, b, "NUM", JsOrder.None, "0") + ")", JsOrder.FunctionCall);
            });
            l.Value("math_modulo", (g, b) => C(V(g, b, "DIVIDEND", JsOrder.Modulus, "0") + " % " + V(g, b, "DIVISOR", JsOrder.Modulus, "0"), JsOrder.Modulus));
            l.Value("math_constrain", (g, b) => C(
                "Math.min(Math.max(" + V(g, b, "VALUE", JsOrder.Comma, "0") + ", " + V(g, b, "LOW", JsOrder.Comma, "0") + "), " + V(g, b, "HIGH", JsOrder.Comma, "Infinity") + ")",
                JsOrder.FunctionCall));
            l.Value("math_random_int", (g, b) => C(
                g.Helper("mathRandomInt", MathRandomInt) + "(" + V(g, b, "FROM", JsOrder.Comma, "0") + ", " + V(g, b, "TO", JsOrder.Comma, "0") + ")", JsOrder.FunctionCall));
            l.Value("math_random_float", (g, b) => C("Math.random()", JsOrder.FunctionCall));
            l.Value("math_atan2", (g, b) => C(
                "Math.atan2(" + V(g, b, "Y", JsOrder.Comma, "0") + ", " + V(g, b, "X", JsOrder.Comma, "0") + ") / Math.PI * 180", JsOrder.Division));
            l.Value("math_on_list", (g, b) =>
            {
                string op = b.GetFieldValue("OP");
                string list = V(g, b, "LIST", op == "SUM" || op == "MIN" || op == "MAX" ? JsOrder.Member : JsOrder.None, "[]");
                switch (op)
                {
                    case "SUM": return C(list + ".reduce(function(x, y) {return x + y;}, 0)", JsOrder.FunctionCall);
                    case "MIN": return C("Math.min.apply(null, " + list + ")", JsOrder.FunctionCall);
                    case "MAX": return C("Math.max.apply(null, " + list + ")", JsOrder.FunctionCall);
                    case "AVERAGE": return C(g.Helper("mathMean", MathMean) + "(" + list + ")", JsOrder.FunctionCall);
                    case "MEDIAN": return C(g.Helper("mathMedian", MathMedian) + "(" + list + ")", JsOrder.FunctionCall);
                    case "MODE": return C(g.Helper("mathModes", MathModes) + "(" + list + ")", JsOrder.FunctionCall);
                    case "STD_DEV": return C(g.Helper("mathStandardDeviation", MathStandardDeviation) + "(" + list + ")", JsOrder.FunctionCall);
                    default: return C(g.Helper("mathRandomList", MathRandomList) + "(" + list + ")", JsOrder.FunctionCall);
                }
            });
            l.Statement("math_change", (g, b) =>
            {
                string v = g.VariableName(b.GetFieldValue("VAR"));
                return v + " = (typeof " + v + " === 'number' ? " + v + " : 0) + " + V(g, b, "DELTA", JsOrder.Addition, "0") + ";\n";
            });
        }

        const string MathIsPrime = @"function %NAME%(n) {
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
}";

        const string MathRandomInt = @"function %NAME%(a, b) {
  if (a > b) {
    // Swap a and b to ensure a is smaller.
    var c = a;
    a = b;
    b = c;
  }
  return Math.floor(Math.random() * (b - a + 1) + a);
}";

        const string MathMean = @"function %NAME%(myList) {
  return myList.reduce(function(x, y) {return x + y;}, 0) / myList.length;
}";

        const string MathMedian = @"function %NAME%(myList) {
  var localList = myList.filter(function (x) {return typeof x === 'number';});
  if (!localList.length) return null;
  localList.sort(function(a, b) {return b - a;});
  if (localList.length % 2 === 0) {
    return (localList[localList.length / 2 - 1] + localList[localList.length / 2]) / 2;
  } else {
    return localList[(localList.length - 1) / 2];
  }
}";

        const string MathModes = @"function %NAME%(values) {
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
}";

        const string MathStandardDeviation = @"function %NAME%(numbers) {
  var n = numbers.length;
  if (!n) return null;
  var mean = numbers.reduce(function(x, y) {return x + y;}) / n;
  var variance = 0;
  for (var j = 0; j < n; j++) {
    variance += Math.pow(numbers[j] - mean, 2);
  }
  variance /= n;
  return Math.sqrt(variance);
}";

        const string MathRandomList = @"function %NAME%(list) {
  var x = Math.floor(Math.random() * list.length);
  return list[x];
}";

        // ---------------------------------------------------------------- text
        static void Text(CodeLanguage l)
        {
            l.Value("text", (g, b) => C(Quote(b.GetFieldValue("TEXT") ?? ""), JsOrder.Atomic));
            l.Value("text_join", (g, b) =>
            {
                int n = 0;
                while (b.GetInput("ADD" + n) != null) n++;
                if (n == 0) return C("''", JsOrder.Atomic);
                if (n == 1) return ForceString(V(g, b, "ADD0", JsOrder.None, "''"));
                if (n == 2)
                {
                    var a = ForceString(V(g, b, "ADD0", JsOrder.Addition, "''"));
                    var c = ForceString(V(g, b, "ADD1", JsOrder.Addition, "''"));
                    return C(a.Text + " + " + c.Text, JsOrder.Addition);
                }
                var parts = new List<string>();
                for (int i = 0; i < n; i++) parts.Add(V(g, b, "ADD" + i, JsOrder.Comma, "''"));
                return C("[" + string.Join(",", parts) + "].join('')", JsOrder.FunctionCall);
            });
            l.Statement("text_append", (g, b) =>
                g.VariableName(b.GetFieldValue("VAR")) + " += " + ForceString(V(g, b, "TEXT", JsOrder.Assignment, "''")).Text + ";\n");
            l.Value("text_length", (g, b) => C(V(g, b, "VALUE", JsOrder.Member, "''") + ".length", JsOrder.Member));
            l.Value("text_isEmpty", (g, b) => C("!" + V(g, b, "VALUE", JsOrder.Member, "''") + ".length", JsOrder.LogicalNot));
            l.Value("text_indexOf", (g, b) =>
            {
                string op = b.GetFieldValue("END") == "FIRST" ? "indexOf" : "lastIndexOf";
                string find = V(g, b, "FIND", JsOrder.None, "''"), text = V(g, b, "VALUE", JsOrder.Member, "''");
                return C(text + "." + op + "(" + find + ") + 1", JsOrder.Addition);
            });
            l.Value("text_changeCase", (g, b) =>
            {
                string text = V(g, b, "TEXT", JsOrder.Member, "''");
                switch (b.GetFieldValue("CASE"))
                {
                    case "UPPERCASE": return C(text + ".toUpperCase()", JsOrder.FunctionCall);
                    case "LOWERCASE": return C(text + ".toLowerCase()", JsOrder.FunctionCall);
                    default: return C(g.Helper("textToTitleCase", TextToTitleCase) + "(" + text + ")", JsOrder.FunctionCall);
                }
            });
            l.Value("text_trim", (g, b) =>
            {
                string text = V(g, b, "TEXT", JsOrder.Member, "''");
                switch (b.GetFieldValue("MODE"))
                {
                    case "LEFT": return C(text + ".replace(/^[\\s\\xa0]+/, '')", JsOrder.FunctionCall);
                    case "RIGHT": return C(text + ".replace(/[\\s\\xa0]+$/, '')", JsOrder.FunctionCall);
                    default: return C(text + ".trim()", JsOrder.FunctionCall);
                }
            });
            l.Value("text_replace", (g, b) => C(
                g.Helper("textReplace", TextReplace) + "(" + V(g, b, "TEXT", JsOrder.Comma, "''") + ", " + V(g, b, "FROM", JsOrder.Comma, "''") + ", " + V(g, b, "TO", JsOrder.Comma, "''") + ")",
                JsOrder.FunctionCall));
            l.Value("text_reverse", (g, b) => C(V(g, b, "TEXT", JsOrder.Member, "''") + ".split('').reverse().join('')", JsOrder.FunctionCall));
            l.Value("text_count", (g, b) => C(
                g.Helper("textCount", TextCount) + "(" + V(g, b, "TEXT", JsOrder.Comma, "''") + ", " + V(g, b, "SUB", JsOrder.Comma, "''") + ")", JsOrder.FunctionCall));
            l.Statement("text_print", (g, b) => g.Print + "(" + V(g, b, "TEXT", JsOrder.None, "''") + ");\n");
            l.Value("text_charAt", (g, b) =>
            {
                string where = b.GetFieldValue("WHERE") ?? "FROM_START";
                string text = V(g, b, "VALUE", where == "RANDOM" ? JsOrder.None : JsOrder.Member, "''");
                switch (where)
                {
                    case "FIRST": return C(text + ".charAt(0)", JsOrder.FunctionCall);
                    case "LAST": return C(text + ".slice(-1)", JsOrder.FunctionCall);
                    case "FROM_START": return C(text + ".charAt(" + Adjusted(g, b, "AT", 0, false, JsOrder.None) + ")", JsOrder.FunctionCall);
                    case "FROM_END": return C(text + ".slice(" + Adjusted(g, b, "AT", 1, true, JsOrder.None) + ").charAt(0)", JsOrder.FunctionCall);
                    default: return C(g.Helper("textRandomLetter", TextRandomLetter) + "(" + text + ")", JsOrder.FunctionCall);
                }
            });
            l.Value("text_getSubstring", (g, b) =>
            {
                string w1 = b.GetFieldValue("WHERE1"), w2 = b.GetFieldValue("WHERE2");
                string text = V(g, b, "STRING", JsOrder.Member, "''");
                if (w1 == "FIRST" && w2 == "LAST") return C(text, JsOrder.Atomic);
                bool simple = Regex.IsMatch(text, @"^'?\w+'?$") || (w1 != "FROM_END" && w1 != "LAST" && w2 != "FROM_END" && w2 != "LAST");
                return Slice(g, b, text, w1, w2, simple, ".length");
            });
        }

        const string TextToTitleCase = @"function %NAME%(str) {
  return str.replace(/\S+/g,
      function(txt) {return txt[0].toUpperCase() + txt.substring(1).toLowerCase();});
}";

        const string TextReplace = @"function %NAME%(haystack, needle, replacement) {
  needle = needle.replace(/([-()\[\]{}+?*.$\^|,:#<!\\])/g, '\\$1')
                 .replace(/\x08/g, '\\x08');
  return haystack.replace(new RegExp(needle, 'g'), replacement);
}";

        const string TextCount = @"function %NAME%(haystack, needle) {
  if (needle.length === 0) {
    return haystack.length + 1;
  } else {
    return haystack.split(needle).length - 1;
  }
}";

        const string TextRandomLetter = @"function %NAME%(text) {
  var x = Math.floor(Math.random() * text.length);
  return text[x];
}";

        // ---------------------------------------------------------------- lists
        /// <summary>
        /// The index of an AT input as the language counts (from 0): shifted by <paramref name="delta"/> (and negated when asked), with
        /// brackets only when the result would bind looser than <paramref name="order"/>.
        /// </summary>
        static string Adjusted(CodeGenerator g, Block b, string input, int delta, bool negate, double order)
        {
            delta--;                                         // the blocks count from 1
            double inner = delta > 0 ? JsOrder.Addition : delta < 0 ? JsOrder.Subtraction : negate ? JsOrder.UnaryNegation : order;
            string at = V(g, b, input, inner, "1");
            double outer = order;
            if (CodeGenerator.IsNumber(at))
            {
                double d = JsValue.ToNumber(at) + delta;
                return N(negate ? -d : d);
            }
            if (delta > 0) { at += " + " + delta; outer = JsOrder.Addition; }
            else if (delta < 0) { at += " - " + -delta; outer = JsOrder.Subtraction; }
            if (negate) { at = delta != 0 ? "-(" + at + ")" : "-" + at; outer = JsOrder.UnaryNegation; }
            return Math.Floor(outer) >= Math.Floor(order) ? "(" + at + ")" : at;
        }

        /// <summary>A part of a text or a list between two positions (WHERE1/AT1 to WHERE2/AT2).</summary>
        static Code Slice(CodeGenerator g, Block b, string seq, string w1, string w2, bool simple, string length)
        {
            if (simple)
            {
                string at1, at2;
                switch (w1)
                {
                    case "FROM_START": at1 = Adjusted(g, b, "AT1", 0, false, JsOrder.None); break;
                    case "FROM_END": at1 = seq + length + " - " + Adjusted(g, b, "AT1", 1, false, JsOrder.Subtraction); break;
                    default: at1 = "0"; break;
                }
                switch (w2)
                {
                    case "FROM_START": at2 = Adjusted(g, b, "AT2", 1, false, JsOrder.None); break;
                    case "FROM_END": at2 = seq + length + " - " + Adjusted(g, b, "AT2", 0, false, JsOrder.Subtraction); break;
                    default: at2 = seq + length; break;
                }
                return C(seq + ".slice(" + at1 + ", " + at2 + ")", JsOrder.FunctionCall);
            }
            var args = new List<string> { seq };
            if (w1 == "FROM_START" || w1 == "FROM_END") args.Add(Adjusted(g, b, "AT1", 0, false, JsOrder.None));
            if (w2 == "FROM_START" || w2 == "FROM_END") args.Add(Adjusted(g, b, "AT2", 0, false, JsOrder.None));
            string start = w1 == "FROM_START" ? "at1" : w1 == "FROM_END" ? "sequence.length - 1 - at1" : "0";
            string end = w2 == "FROM_START" ? "at2 + 1" : w2 == "FROM_END" ? "sequence.length - 1 - at2 + 1" : "sequence.length - 1 + 1";
            var ps = new List<string> { "sequence" };
            if (w1 != "FIRST") ps.Add("at1");
            if (w2 != "LAST") ps.Add("at2");
            string baseName = "subsequence" + Where(w1) + Where(w2);
            string fn = g.Helper(baseName, "function %NAME%(" + string.Join(", ", ps) + ") {\n  var start = " + start + ";\n  var end = " + end + ";\n  return sequence.slice(start, end);\n}");
            return C(fn + "(" + string.Join(", ", args) + ")", JsOrder.FunctionCall);
        }

        static string Where(string w) => w == "FROM_START" ? "FromStart" : w == "FROM_END" ? "FromEnd" : w == "FIRST" ? "First" : "Last";

        static void Lists(CodeLanguage l)
        {
            l.Value("lists_create_empty", (g, b) => C("[]", JsOrder.Atomic));
            l.Value("lists_create_with", (g, b) =>
            {
                var items = new List<string>();
                for (int i = 0; b.GetInput("ADD" + i) != null; i++) items.Add(V(g, b, "ADD" + i, JsOrder.Comma, "null"));
                return C("[" + string.Join(", ", items) + "]", JsOrder.Atomic);
            });
            l.Value("lists_repeat", (g, b) => C(
                g.Helper("listsRepeat", ListsRepeat) + "(" + V(g, b, "ITEM", JsOrder.Comma, "null") + ", " + V(g, b, "NUM", JsOrder.Comma, "0") + ")", JsOrder.FunctionCall));
            l.Value("lists_length", (g, b) => C(V(g, b, "VALUE", JsOrder.Member, "[]") + ".length", JsOrder.Member));
            l.Value("lists_isEmpty", (g, b) => C("!" + V(g, b, "VALUE", JsOrder.Member, "[]") + ".length", JsOrder.LogicalNot));
            l.Value("lists_indexOf", (g, b) =>
            {
                string op = b.GetFieldValue("END") == "FIRST" ? "indexOf" : "lastIndexOf";
                string find = V(g, b, "FIND", JsOrder.None, "''"), list = V(g, b, "VALUE", JsOrder.Member, "[]");
                return C(list + "." + op + "(" + find + ") + 1", JsOrder.Addition);
            });
            l.Value("lists_reverse", (g, b) => C(V(g, b, "LIST", JsOrder.Member, "[]") + ".slice().reverse()", JsOrder.FunctionCall));
            l.Value("lists_getSublist", (g, b) =>
            {
                string w1 = b.GetFieldValue("WHERE1"), w2 = b.GetFieldValue("WHERE2");
                string list = V(g, b, "LIST", JsOrder.Member, "[]");
                if (w1 == "FIRST" && w2 == "LAST") return C(list + ".slice(0)", JsOrder.FunctionCall);
                bool simple = Identifier.IsMatch(list) || (w1 != "FROM_END" && w2 == "FROM_START");
                return Slice(g, b, list, w1, w2, simple, ".length");
            });
            l.Value("lists_split", (g, b) =>
            {
                string delim = V(g, b, "DELIM", JsOrder.None, "''");
                bool join = b.GetFieldValue("MODE") == "JOIN";
                string input = V(g, b, "INPUT", JsOrder.Member, join ? "[]" : "''");
                return C(input + (join ? ".join(" : ".split(") + delim + ")", JsOrder.FunctionCall);
            });
            l.Value("lists_sort", (g, b) =>
            {
                string list = V(g, b, "LIST", JsOrder.Member, "[]");
                int direction = b.GetFieldValue("DIRECTION") == "1" ? 1 : -1;
                string compare = g.Helper("listsGetSortCompare", ListsGetSortCompare);
                return C(list + ".slice().sort(" + compare + "(\"" + b.GetFieldValue("TYPE") + "\", " + direction + "))", JsOrder.FunctionCall);
            });
            l.Value("lists_getIndex", (g, b) => GetIndex(g, b));
            l.Statement("lists_getIndex", (g, b) => GetIndex(g, b).Text + ";\n");
            l.Statement("lists_setIndex", SetIndex);
        }

        const string ListsRepeat = @"function %NAME%(value, n) {
  var array = [];
  for (var i = 0; i < n; i++) {
    array[i] = value;
  }
  return array;
}";

        const string ListsGetSortCompare = @"function %NAME%(type, direction) {
  var compareFuncs = {
    'NUMERIC': function(a, b) {
        return Number(a) - Number(b); },
    'TEXT': function(a, b) {
        return String(a) > String(b) ? 1 : -1; },
    'IGNORE_CASE': function(a, b) {
        return String(a).toLowerCase() > String(b).toLowerCase() ? 1 : -1; },
  };
  var compare = compareFuncs[type];
  return function(a, b) { return compare(a, b) * direction; };
}";

        const string ListsGetRandomItem = @"function %NAME%(list, remove) {
  var x = Math.floor(Math.random() * list.length);
  if (remove) {
    return list.splice(x, 1)[0];
  } else {
    return list[x];
  }
}";

        static Code GetIndex(CodeGenerator g, Block b)
        {
            string mode = b.GetFieldValue("MODE") ?? "GET", where = b.GetFieldValue("WHERE") ?? "FROM_START";
            string list = V(g, b, "VALUE", where == "RANDOM" ? JsOrder.None : JsOrder.Member, "[]");
            double order = mode == "GET" ? JsOrder.Member : JsOrder.FunctionCall;
            switch (where)
            {
                case "FIRST":
                    return C(mode == "GET" ? list + "[0]" : list + ".shift()", order);
                case "LAST":
                    return C(mode == "GET" ? list + ".slice(-1)[0]" : list + ".pop()", order);
                case "FROM_START":
                    {
                        string at = Adjusted(g, b, "AT", 0, false, JsOrder.None);
                        return C(mode == "GET" ? list + "[" + at + "]" : list + ".splice(" + at + ", 1)" + (mode == "GET_REMOVE" ? "[0]" : ""), order);
                    }
                case "FROM_END":
                    {
                        string at = Adjusted(g, b, "AT", 1, true, JsOrder.None);
                        return C(mode == "GET" ? list + ".slice(" + at + ")[0]" : list + ".splice(" + at + ", 1)" + (mode == "GET_REMOVE" ? "[0]" : ""), order);
                    }
                default:
                    return C(g.Helper("listsGetRandomItem", ListsGetRandomItem) + "(" + list + ", " + (mode != "GET" ? "true" : "false") + ")", JsOrder.FunctionCall);
            }
        }

        static string SetIndex(CodeGenerator g, Block b)
        {
            string list = V(g, b, "LIST", JsOrder.Member, "[]");
            string mode = b.GetFieldValue("MODE") ?? "SET", where = b.GetFieldValue("WHERE") ?? "FROM_START";
            string value = V(g, b, "TO", JsOrder.Assignment, "null");
            bool insert = mode == "INSERT";
            string code = "";
            // a list that is not a plain name is stored first when the code uses it twice
            Action cache = () =>
            {
                if (Identifier.IsMatch(list)) return;
                string tmp = g.NewName("tmpList");
                code += "var " + tmp + " = " + list + ";\n";
                list = tmp;
            };
            switch (where)
            {
                case "FIRST":
                    return insert ? list + ".unshift(" + value + ");\n" : list + "[0] = " + value + ";\n";
                case "LAST":
                    if (insert) return list + ".push(" + value + ");\n";
                    cache();
                    return code + list + "[" + list + ".length - 1] = " + value + ";\n";
                case "FROM_START":
                    {
                        string at = Adjusted(g, b, "AT", 0, false, JsOrder.None);
                        return insert ? list + ".splice(" + at + ", 0, " + value + ");\n" : list + "[" + at + "] = " + value + ";\n";
                    }
                case "FROM_END":
                    {
                        string at = Adjusted(g, b, "AT", 1, false, JsOrder.Subtraction);
                        cache();
                        return code + (insert
                            ? list + ".splice(" + list + ".length - " + at + ", 0, " + value + ");\n"
                            : list + "[" + list + ".length - " + at + "] = " + value + ";\n");
                    }
                default:
                    {
                        cache();
                        string x = g.NewName("tmpX");
                        code += "var " + x + " = Math.floor(Math.random() * " + list + ".length);\n";
                        return code + (insert ? list + ".splice(" + x + ", 0, " + value + ");\n" : list + "[" + x + "] = " + value + ";\n");
                    }
            }
        }

        // ---------------------------------------------------------------- variables
        static void Variables(CodeLanguage l)
        {
            l.Value("variables_get", (g, b) => C(g.VariableName(b.GetFieldValue("VAR")), JsOrder.Atomic));
            l.Statement("variables_set", (g, b) => g.VariableName(b.GetFieldValue("VAR")) + " = " + V(g, b, "VALUE", JsOrder.Assignment, "0") + ";\n");
        }

        // ---------------------------------------------------------------- procedures
        static void Procedures(CodeLanguage l)
        {
            StatementGenerator define = (g, b) =>
            {
                string name = g.ProcedureName(ProcedureInfo.Name(b));
                string body = g.StatementToCode(b, "STACK");
                string ret = V(g, b, "RETURN", JsOrder.None, "");
                if (ret.Length > 0) ret = g.Language.Indent + "return " + ret + ";\n";
                g.AddDefinition(g.CommentLines(b) + "function " + name + "(" + string.Join(", ", g.ParameterNames(b)) + ") {\n" + body + ret + "}");
                return "";
            };
            l.Statement("procedures_defnoreturn", define);
            l.Statement("procedures_defreturn", define);
            Func<CodeGenerator, Block, string> call = (g, b) =>
            {
                var args = new List<string>();
                for (int i = 0; b.GetInput("ARG" + i) != null; i++) args.Add(V(g, b, "ARG" + i, JsOrder.None, "null"));
                return g.ProcedureName(ProcedureInfo.Name(b)) + "(" + string.Join(", ", args) + ")";
            };
            l.Statement("procedures_callnoreturn", (g, b) => call(g, b) + ";\n");
            l.Value("procedures_callreturn", (g, b) => C(call(g, b), JsOrder.FunctionCall));
            l.Statement("procedures_ifreturn", (g, b) =>
            {
                string code = "if (" + V(g, b, "CONDITION", JsOrder.None, "false") + ") {\n";
                code += b.GetInput("VALUE") != null
                    ? g.Language.Indent + "return " + V(g, b, "VALUE", JsOrder.None, "null") + ";\n"
                    : g.Language.Indent + "return;\n";
                return code + "}\n";
            });
        }

        // ---------------------------------------------------------------- colour
        static void Colour_(CodeLanguage l)
        {
            l.Value("colour_picker", (g, b) => C(Quote(Colour.Normalize(b.GetFieldValue("COLOUR")) ?? "#000000"), JsOrder.Atomic));
            l.Value("colour_random", (g, b) => C(g.Helper("colourRandom", ColourRandom) + "()", JsOrder.FunctionCall));
            l.Value("colour_rgb", (g, b) => C(
                g.Helper("colourRgb", ColourRgb) + "(" + V(g, b, "RED", JsOrder.Comma, "0") + ", " + V(g, b, "GREEN", JsOrder.Comma, "0") + ", " + V(g, b, "BLUE", JsOrder.Comma, "0") + ")",
                JsOrder.FunctionCall));
            l.Value("colour_blend", (g, b) => C(
                g.Helper("colourBlend", ColourBlend) + "(" + V(g, b, "COLOUR1", JsOrder.Comma, "'#000000'") + ", " + V(g, b, "COLOUR2", JsOrder.Comma, "'#000000'") + ", " + V(g, b, "RATIO", JsOrder.Comma, "0.5") + ")",
                JsOrder.FunctionCall));
        }

        const string ColourRandom = @"function %NAME%() {
  var num = Math.floor(Math.random() * Math.pow(2, 24));
  return '#' + ('00000' + num.toString(16)).substr(-6);
}";

        const string ColourRgb = @"function %NAME%(r, g, b) {
  r = Math.max(Math.min(Number(r), 100), 0) * 2.55;
  g = Math.max(Math.min(Number(g), 100), 0) * 2.55;
  b = Math.max(Math.min(Number(b), 100), 0) * 2.55;
  r = ('0' + (Math.round(r) || 0).toString(16)).slice(-2);
  g = ('0' + (Math.round(g) || 0).toString(16)).slice(-2);
  b = ('0' + (Math.round(b) || 0).toString(16)).slice(-2);
  return '#' + r + g + b;
}";

        const string ColourBlend = @"function %NAME%(c1, c2, ratio) {
  ratio = Math.max(Math.min(Number(ratio), 1), 0);
  var r1 = parseInt(c1.substring(1, 3), 16);
  var g1 = parseInt(c1.substring(3, 5), 16);
  var b1 = parseInt(c1.substring(5, 7), 16);
  var r2 = parseInt(c2.substring(1, 3), 16);
  var g2 = parseInt(c2.substring(3, 5), 16);
  var b2 = parseInt(c2.substring(5, 7), 16);
  var r = Math.round(r1 * (1 - ratio) + r2 * ratio);
  var g = Math.round(g1 * (1 - ratio) + g2 * ratio);
  var b = Math.round(b1 * (1 - ratio) + b2 * ratio);
  r = ('0' + (r || 0).toString(16)).slice(-2);
  g = ('0' + (g || 0).toString(16)).slice(-2);
  b = ('0' + (b || 0).toString(16)).slice(-2);
  return '#' + r + g + b;
}";
    }
}
