using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Blockly
{
    /// <summary>How tightly Python operators bind (the numbers of Blockly's own Python generator).</summary>
    static class PyOrder
    {
        public const double Atomic = 0, Member = 2.1, FunctionCall = 2.2, UnarySign = 4, Multiplicative = 5, Additive = 6,
            Relational = 11, LogicalNot = 12, LogicalAnd = 13, LogicalOr = 14, Conditional = 15, None = 99;
    }

    /// <summary>
    /// The Python 3 text of the built-in blocks. Where Python's own operators would not do what the block does in the interpreter
    /// (rounding half up, the sign of a remainder) the code says so with math.floor / math.fmod, so the exported program
    /// prints what the program printed in Unity.
    /// </summary>
    static class PythonGenerators
    {
        static readonly string[] ReservedWords =
        {
            "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del", "elif", "else",
            "except", "finally", "for", "from", "global", "if", "import", "in", "is", "lambda", "nonlocal", "not", "or", "pass", "raise",
            "return", "try", "while", "with", "yield", "print", "len", "list", "str", "int", "float", "min", "max", "sum", "sorted", "reversed",
            "range", "map", "round", "type", "math", "random", "Number", "object", "set", "dict", "tuple", "bool", "abs", "input", "id", "all",
            "any", "zip", "enumerate", "open", "format", "next", "iter", "hash", "dir", "vars", "help", "exit", "quit", "isinstance", "bytes",
        };

        internal static CodeLanguage Create()
        {
            var l = new CodeLanguage("Python") { Indent = "    ", FileExtension = ".py", DefaultPrint = "print", CommentPrefix = "# ", Quote = Quote };
            foreach (var w in ReservedWords) l.Reserved.Add(w);
            l.Assemble = (g, variables, defs, code) =>
            {
                var parts = new List<string>();
                if (g.Imports.Count > 0) parts.Add(string.Join("\n", g.Imports));
                if (variables.Count > 0) parts.Add(string.Join("\n", variables.Select(v => v + " = None")));
                parts.AddRange(defs);
                return parts.Count == 0 ? code : string.Join("\n\n", parts) + "\n\n\n" + code;
            };
            Logic(l); Loops(l); Math_(l); Text(l); Lists(l); Variables(l); Procedures(l); Colour_(l);
            return l;
        }

        // ---------------------------------------------------------------- helpers
        static Code C(string text, double order) => new Code(text, order);

        static string V(CodeGenerator g, Block b, string input, double order, string def)
        {
            var s = g.ValueToCode(b, input, order);
            return s.Length == 0 ? def : s;
        }

        /// <summary>The statements of an input, or pass when there are none (Python needs a body).</summary>
        static string Body(CodeGenerator g, Block b, string input)
        {
            var s = g.StatementToCode(b, input);
            return s.Length == 0 ? g.Language.Indent + "pass\n" : s;
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
                    default:
                        if (c < ' ') sb.Append("\\x").Append(((int)c).ToString("x2")); else sb.Append(c);
                        break;
                }
            }
            return sb.Append('\'').ToString();
        }

        static readonly Regex Identifier = new Regex(@"^\w+$");

        static Code ForceString(string value)
            => Regex.IsMatch(value, @"^\s*'([^'\\]|\\.)*'\s*$") ? C(value, PyOrder.Atomic) : C("str(" + value + ")", PyOrder.FunctionCall);

        static string N(double d) => JsValue.NumberToString(d);

        // ---------------------------------------------------------------- logic
        static void Logic(CodeLanguage l)
        {
            l.Value("logic_boolean", (g, b) => C(b.GetFieldValue("BOOL") == "TRUE" ? "True" : "False", PyOrder.Atomic));
            l.Value("logic_null", (g, b) => C("None", PyOrder.Atomic));
            l.Value("logic_negate", (g, b) => C("not " + V(g, b, "BOOL", PyOrder.LogicalNot, "True"), PyOrder.LogicalNot));
            l.Value("logic_ternary", (g, b) => C(
                V(g, b, "THEN", PyOrder.Conditional, "None") + " if " + V(g, b, "IF", PyOrder.Conditional, "False") + " else " + V(g, b, "ELSE", PyOrder.Conditional, "None"),
                PyOrder.Conditional));
            l.Value("logic_operation", (g, b) =>
            {
                bool and = b.GetFieldValue("OP") == "AND";
                double order = and ? PyOrder.LogicalAnd : PyOrder.LogicalOr;
                string a = g.ValueToCode(b, "A", order), c = g.ValueToCode(b, "B", order);
                if (a.Length == 0 && c.Length == 0) { a = "False"; c = "False"; }
                else { string def = and ? "True" : "False"; if (a.Length == 0) a = def; if (c.Length == 0) c = def; }
                return C(a + (and ? " and " : " or ") + c, order);
            });
            l.Value("logic_compare", (g, b) =>
            {
                string op;
                switch (b.GetFieldValue("OP"))
                {
                    case "EQ": op = "=="; break;
                    case "NEQ": op = "!="; break;
                    case "LT": op = "<"; break;
                    case "LTE": op = "<="; break;
                    case "GT": op = ">"; break;
                    default: op = ">="; break;
                }
                return C(V(g, b, "A", PyOrder.Relational, "0") + " " + op + " " + V(g, b, "B", PyOrder.Relational, "0"), PyOrder.Relational);
            });
            l.Statement("controls_if", (g, b) =>
            {
                var sb = new StringBuilder();
                for (int n = 0; b.GetInput("IF" + n) != null; n++)
                    sb.Append(n == 0 ? "if " : "elif ").Append(V(g, b, "IF" + n, PyOrder.None, "False")).Append(":\n").Append(Body(g, b, "DO" + n));
                if (b.GetInput("ELSE") != null) sb.Append("else:\n").Append(Body(g, b, "ELSE"));
                return sb.ToString();
            });
        }

        // ---------------------------------------------------------------- loops
        const string UpRange = @"def %NAME%(start, stop, step):
    while start <= stop:
        yield start
        start += abs(step)";

        const string DownRange = @"def %NAME%(start, stop, step):
    while start >= stop:
        yield start
        start -= abs(step)";

        static void Loops(CodeLanguage l)
        {
            StatementGenerator repeat = (g, b) =>
            {
                string repeats = b.GetInput("TIMES")?.Connection != null
                    ? V(g, b, "TIMES", PyOrder.None, "0")
                    : N(Math.Floor(JsValue.ToNumber(b.GetFieldValue("TIMES"))));
                repeats = CodeGenerator.IsNumber(repeats) ? repeats : "int(" + repeats + ")";
                string loopVar = g.NewName("count");
                return "for " + loopVar + " in range(" + repeats + "):\n" + Body(g, b, "DO");
            };
            l.Statement("controls_repeat_ext", repeat);
            l.Statement("controls_repeat", repeat);
            l.Statement("controls_whileUntil", (g, b) =>
            {
                bool until = b.GetFieldValue("MODE") == "UNTIL";
                string cond = V(g, b, "BOOL", until ? PyOrder.LogicalNot : PyOrder.None, "False");
                return "while " + (until ? "not " : "") + cond + ":\n" + Body(g, b, "DO");
            });
            l.Statement("controls_for", (g, b) =>
            {
                string v = g.VariableName(b.GetFieldValue("VAR"));
                string from = V(g, b, "FROM", PyOrder.None, "0"), to = V(g, b, "TO", PyOrder.None, "0"), by = V(g, b, "BY", PyOrder.None, "1");
                string branch = Body(g, b, "DO");
                bool whole = Regex.IsMatch(from, @"^-?\d+$") && Regex.IsMatch(to, @"^-?\d+$") && Regex.IsMatch(by, @"^-?\d+$");
                if (whole)
                {
                    long f = long.Parse(from), t = long.Parse(to), step = Math.Abs(long.Parse(by));
                    bool up = f <= t;
                    string range = up ? "range(" + f + ", " + (t + 1) + (step != 1 ? ", " + step : "") + ")"
                                      : "range(" + f + ", " + (t - 1) + ", " + -step + ")";
                    if (step == 0) range = "range(" + f + ", " + (t + 1) + ")";
                    return "for " + v + " in " + range + ":\n" + branch;
                }
                string code = "", start = from, end = to;
                if (!CodeGenerator.IsNumber(from) && !Identifier.IsMatch(from)) { start = g.NewName(v + "_start"); code += start + " = " + from + "\n"; }
                if (!CodeGenerator.IsNumber(to) && !Identifier.IsMatch(to)) { end = g.NewName(v + "_end"); code += end + " = " + to + "\n"; }
                string inc = g.NewName(v + "_inc");
                code += inc + " = abs(" + by + ")\n";
                string up2 = g.Helper("upRange", UpRange), down = g.Helper("downRange", DownRange);
                return code + "for " + v + " in (" + up2 + "(" + start + ", " + end + ", " + inc + ") if " + start + " <= " + end + " else " + down + "(" + start + ", " + end + ", " + inc + ")):\n" + branch;
            });
            l.Statement("controls_forEach", (g, b) =>
            {
                string v = g.VariableName(b.GetFieldValue("VAR"));
                return "for " + v + " in " + V(g, b, "LIST", PyOrder.None, "[]") + ":\n" + Body(g, b, "DO");
            });
            l.Statement("controls_flow_statements", (g, b) => b.GetFieldValue("FLOW") == "BREAK" ? "break\n" : "continue\n");
        }

        // ---------------------------------------------------------------- math
        static void Math_(CodeLanguage l)
        {
            l.Value("math_number", (g, b) =>
            {
                double d = JsValue.ToNumber(b.GetFieldValue("NUM"));
                if (double.IsPositiveInfinity(d)) return C("float('inf')", PyOrder.FunctionCall);
                if (double.IsNegativeInfinity(d)) return C("-float('inf')", PyOrder.UnarySign);
                if (double.IsNaN(d)) return C("float('nan')", PyOrder.FunctionCall);
                return C(N(d), d < 0 || (d == 0 && 1 / d < 0) ? PyOrder.UnarySign : PyOrder.Atomic);
            });
            l.Value("math_arithmetic", (g, b) =>
            {
                string op = b.GetFieldValue("OP");
                g.AddImport("import math");
                if (op == "POWER") return C("math.pow(" + V(g, b, "A", PyOrder.None, "0") + ", " + V(g, b, "B", PyOrder.None, "0") + ")", PyOrder.FunctionCall);
                string sym; double order;
                switch (op)
                {
                    case "ADD": sym = " + "; order = PyOrder.Additive; break;
                    case "MINUS": sym = " - "; order = PyOrder.Additive; break;
                    case "MULTIPLY": sym = " * "; order = PyOrder.Multiplicative; break;
                    default: sym = " / "; order = PyOrder.Multiplicative; break;
                }
                return C(V(g, b, "A", order, "0") + sym + V(g, b, "B", order, "0"), order);
            });
            l.Value("math_single", (g, b) =>
            {
                string op = b.GetFieldValue("OP");
                if (op == "NEG")
                {
                    string neg = V(g, b, "NUM", PyOrder.UnarySign, "0");
                    if (neg.StartsWith("-")) neg = " " + neg;
                    return C("-" + neg, PyOrder.UnarySign);
                }
                g.AddImport("import math");
                string arg = V(g, b, "NUM", PyOrder.None, "0");
                switch (op)
                {
                    case "ROOT": return C("math.sqrt(" + arg + ")", PyOrder.FunctionCall);
                    case "ABS": return C("math.fabs(" + arg + ")", PyOrder.FunctionCall);
                    case "LN": return C("math.log(" + arg + ")", PyOrder.FunctionCall);
                    case "LOG10": return C("math.log10(" + arg + ")", PyOrder.FunctionCall);
                    case "EXP": return C("math.exp(" + arg + ")", PyOrder.FunctionCall);
                    default: return C("math.pow(10, " + arg + ")", PyOrder.FunctionCall);
                }
            });
            l.Value("math_trig", (g, b) =>
            {
                string op = b.GetFieldValue("OP");
                g.AddImport("import math");
                if (op == "SIN" || op == "COS" || op == "TAN")
                    return C("math." + op.ToLowerInvariant() + "(" + V(g, b, "NUM", PyOrder.Multiplicative, "0") + " / 180.0 * math.pi)", PyOrder.FunctionCall);
                return C("math.a" + op.Substring(1).ToLowerInvariant() + "(" + V(g, b, "NUM", PyOrder.None, "0") + ") / math.pi * 180", PyOrder.Multiplicative);
            });
            l.Value("math_constant", (g, b) =>
            {
                string c = b.GetFieldValue("CONSTANT") ?? b.GetFieldValue("OP");
                if (c == "INFINITY") return C("float('inf')", PyOrder.FunctionCall);
                g.AddImport("import math");
                switch (c)
                {
                    case "PI": return C("math.pi", PyOrder.Member);
                    case "E": return C("math.e", PyOrder.Member);
                    case "GOLDEN_RATIO": return C("(1 + math.sqrt(5)) / 2", PyOrder.Multiplicative);
                    case "SQRT2": return C("math.sqrt(2)", PyOrder.FunctionCall);
                    default: return C("math.sqrt(1.0 / 2)", PyOrder.FunctionCall);
                }
            });
            l.Value("math_number_property", (g, b) =>
            {
                string prop = b.GetFieldValue("PROPERTY");
                string n = V(g, b, "NUMBER_TO_CHECK", prop == "PRIME" || prop == "ODD" ? PyOrder.None : PyOrder.Multiplicative, "0");
                switch (prop)
                {
                    case "PRIME":
                        g.AddImport("import math");
                        return C(g.Helper("math_isPrime", MathIsPrime) + "(" + n + ")", PyOrder.FunctionCall);
                    case "EVEN": return C(n + " % 2 == 0", PyOrder.Relational);
                    case "ODD": g.AddImport("import math"); return C("math.fmod(" + n + ", 2) == 1", PyOrder.Relational);
                    case "WHOLE": return C(n + " % 1 == 0", PyOrder.Relational);
                    case "POSITIVE": return C(n + " > 0", PyOrder.Relational);
                    case "NEGATIVE": return C(n + " < 0", PyOrder.Relational);
                    default:
                        string divisor = V(g, b, "DIVISOR", PyOrder.Multiplicative, "0");
                        return divisor == "0" ? C("False", PyOrder.Atomic) : C(n + " % " + divisor + " == 0", PyOrder.Relational);
                }
            });
            l.Value("math_round", (g, b) =>
            {
                g.AddImport("import math");
                string op = b.GetFieldValue("OP");
                if (op == "ROUND") return C("math.floor(" + V(g, b, "NUM", PyOrder.Additive, "0") + " + 0.5)", PyOrder.FunctionCall);     // half rounds up, as in the interpreter
                return C("math." + (op == "ROUNDUP" ? "ceil" : "floor") + "(" + V(g, b, "NUM", PyOrder.None, "0") + ")", PyOrder.FunctionCall);
            });
            l.Value("math_modulo", (g, b) =>
            {
                g.AddImport("import math");                                                       // the sign of the dividend, as in the interpreter
                return C("math.fmod(" + V(g, b, "DIVIDEND", PyOrder.None, "0") + ", " + V(g, b, "DIVISOR", PyOrder.None, "0") + ")", PyOrder.FunctionCall);
            });
            l.Value("math_constrain", (g, b) => C(
                "min(max(" + V(g, b, "VALUE", PyOrder.None, "0") + ", " + V(g, b, "LOW", PyOrder.None, "0") + "), " + V(g, b, "HIGH", PyOrder.None, "float('inf')") + ")", PyOrder.FunctionCall));
            l.Value("math_random_int", (g, b) =>
            {
                g.AddImport("import random");
                return C(g.Helper("math_random_int", MathRandomInt) + "(" + V(g, b, "FROM", PyOrder.None, "0") + ", " + V(g, b, "TO", PyOrder.None, "0") + ")", PyOrder.FunctionCall);
            });
            l.Value("math_random_float", (g, b) => { g.AddImport("import random"); return C("random.random()", PyOrder.FunctionCall); });
            l.Value("math_atan2", (g, b) =>
            {
                g.AddImport("import math");
                return C("math.atan2(" + V(g, b, "Y", PyOrder.None, "0") + ", " + V(g, b, "X", PyOrder.None, "0") + ") / math.pi * 180", PyOrder.Multiplicative);
            });
            l.Value("math_on_list", (g, b) =>
            {
                string op = b.GetFieldValue("OP");
                string list = V(g, b, "LIST", PyOrder.None, "[]");
                switch (op)
                {
                    case "SUM": return C("sum(" + list + ")", PyOrder.FunctionCall);
                    case "MIN": return C("min(" + list + ")", PyOrder.FunctionCall);
                    case "MAX": return C("max(" + list + ")", PyOrder.FunctionCall);
                    case "AVERAGE": return C(g.Helper("math_mean", MathMean) + "(" + list + ")", PyOrder.FunctionCall);
                    case "MEDIAN": return C(g.Helper("math_median", MathMedian) + "(" + list + ")", PyOrder.FunctionCall);
                    case "MODE": return C(g.Helper("math_modes", MathModes) + "(" + list + ")", PyOrder.FunctionCall);
                    case "STD_DEV": g.AddImport("import math"); return C(g.Helper("math_standard_deviation", MathStandardDeviation) + "(" + list + ")", PyOrder.FunctionCall);
                    default: g.AddImport("import random"); return C("random.choice(" + list + ")", PyOrder.FunctionCall);
                }
            });
            l.Statement("math_change", (g, b) =>
            {
                g.AddImport("from numbers import Number");
                string v = g.VariableName(b.GetFieldValue("VAR"));
                return v + " = (" + v + " if isinstance(" + v + ", Number) else 0) + " + V(g, b, "DELTA", PyOrder.Additive, "0") + "\n";
            });
        }

        const string MathIsPrime = @"def %NAME%(n):
    # https://en.wikipedia.org/wiki/Primality_test#Naive_methods
    # If n is not a number but a string, try parsing it.
    if type(n) not in (int, float):
        try:
            n = float(n)
        except:
            return False
    if n == 2 or n == 3:
        return True
    # False if n is negative, is 1, or not whole, or if n is divisible by 2 or 3.
    if n <= 1 or n % 1 != 0 or n % 2 == 0 or n % 3 == 0:
        return False
    # Check all the numbers of form 6k +/- 1, up to sqrt(n).
    for x in range(6, int(math.sqrt(n)) + 2, 6):
        if n % (x - 1) == 0 or n % (x + 1) == 0:
            return False
    return True";

        const string MathRandomInt = @"def %NAME%(a, b):
    if a > b:
        # Swap a and b to ensure a is smaller.
        a, b = b, a
    return random.randint(a, b)";

        const string MathMean = @"def %NAME%(myList):
    localList = [e for e in myList if type(e) == int or type(e) == float]
    if not localList: return
    return float(sum(localList)) / len(localList)";

        const string MathMedian = @"def %NAME%(myList):
    localList = sorted([e for e in myList if type(e) == int or type(e) == float])
    if not localList: return
    if len(localList) % 2 == 0:
        return (localList[len(localList) // 2 - 1] + localList[len(localList) // 2]) / 2.0
    else:
        return localList[(len(localList) - 1) // 2]";

        const string MathModes = @"def %NAME%(some_list):
    modes = []
    # Using a lists of [item, count] to keep count rather than dict
    # to avoid ""unhashable"" errors when the counted item is itself a list or dict.
    counts = []
    maxCount = 1
    for item in some_list:
        found = False
        for count in counts:
            if count[0] == item:
                count[1] += 1
                maxCount = max(maxCount, count[1])
                found = True
        if not found:
            counts.append([item, 1])
    for counted_item, item_count in counts:
        if item_count == maxCount:
            modes.append(counted_item)
    return modes";

        const string MathStandardDeviation = @"def %NAME%(numbers):
    n = len(numbers)
    if n == 0: return
    mean = float(sum(numbers)) / n
    variance = sum((x - mean) ** 2 for x in numbers) / n
    return math.sqrt(variance)";

        // ---------------------------------------------------------------- text
        static void Text(CodeLanguage l)
        {
            l.Value("text", (g, b) => C(Quote(b.GetFieldValue("TEXT") ?? ""), PyOrder.Atomic));
            l.Value("text_join", (g, b) =>
            {
                int n = 0;
                while (b.GetInput("ADD" + n) != null) n++;
                if (n == 0) return C("''", PyOrder.Atomic);
                if (n == 1) return ForceString(V(g, b, "ADD0", PyOrder.None, "''"));
                var parts = new List<string>();
                for (int i = 0; i < n; i++) parts.Add(ForceString(V(g, b, "ADD" + i, PyOrder.None, "''")).Text);
                return C("''.join([" + string.Join(", ", parts) + "])", PyOrder.FunctionCall);
            });
            l.Statement("text_append", (g, b) =>
            {
                string v = g.VariableName(b.GetFieldValue("VAR"));
                return v + " = str(" + v + ") + " + ForceString(V(g, b, "TEXT", PyOrder.Additive, "''")).Text + "\n";
            });
            l.Value("text_length", (g, b) => C("len(" + V(g, b, "VALUE", PyOrder.None, "''") + ")", PyOrder.FunctionCall));
            l.Value("text_isEmpty", (g, b) => C("not len(" + V(g, b, "VALUE", PyOrder.None, "''") + ")", PyOrder.LogicalNot));
            l.Value("text_indexOf", (g, b) =>
            {
                string op = b.GetFieldValue("END") == "FIRST" ? "find" : "rfind";
                string find = V(g, b, "FIND", PyOrder.None, "''"), text = V(g, b, "VALUE", PyOrder.Member, "''");
                return C(text + "." + op + "(" + find + ") + 1", PyOrder.Additive);
            });
            l.Value("text_changeCase", (g, b) =>
            {
                string text = V(g, b, "TEXT", PyOrder.Member, "''");
                switch (b.GetFieldValue("CASE"))
                {
                    case "UPPERCASE": return C(text + ".upper()", PyOrder.FunctionCall);
                    case "LOWERCASE": return C(text + ".lower()", PyOrder.FunctionCall);
                    default: return C(text + ".title()", PyOrder.FunctionCall);
                }
            });
            l.Value("text_trim", (g, b) =>
            {
                string text = V(g, b, "TEXT", PyOrder.Member, "''");
                switch (b.GetFieldValue("MODE"))
                {
                    case "LEFT": return C(text + ".lstrip()", PyOrder.FunctionCall);
                    case "RIGHT": return C(text + ".rstrip()", PyOrder.FunctionCall);
                    default: return C(text + ".strip()", PyOrder.FunctionCall);
                }
            });
            l.Value("text_replace", (g, b) => C(
                V(g, b, "TEXT", PyOrder.Member, "''") + ".replace(" + V(g, b, "FROM", PyOrder.None, "''") + ", " + V(g, b, "TO", PyOrder.None, "''") + ")", PyOrder.FunctionCall));
            l.Value("text_reverse", (g, b) => C(V(g, b, "TEXT", PyOrder.Member, "''") + "[::-1]", PyOrder.Member));
            l.Value("text_count", (g, b) => C(V(g, b, "TEXT", PyOrder.Member, "''") + ".count(" + V(g, b, "SUB", PyOrder.None, "''") + ")", PyOrder.FunctionCall));
            l.Statement("text_print", (g, b) => g.Print + "(" + V(g, b, "TEXT", PyOrder.None, "''") + ")\n");
            l.Value("text_charAt", (g, b) =>
            {
                string where = b.GetFieldValue("WHERE") ?? "FROM_START";
                string text = V(g, b, "VALUE", where == "RANDOM" ? PyOrder.None : PyOrder.Member, "''");
                switch (where)
                {
                    case "FIRST": return C(text + "[0]", PyOrder.Member);
                    case "LAST": return C(text + "[-1]", PyOrder.Member);
                    case "FROM_START": return C(text + "[" + Adjusted(g, b, "AT", 0, false) + "]", PyOrder.Member);
                    case "FROM_END": return C(text + "[" + Adjusted(g, b, "AT", 1, true) + "]", PyOrder.Member);
                    default: g.AddImport("import random"); return C("random.choice(" + text + ")", PyOrder.FunctionCall);
                }
            });
            l.Value("text_getSubstring", (g, b) => SliceOf(g, b, V(g, b, "STRING", PyOrder.Member, "''")));
        }

        // ---------------------------------------------------------------- lists
        /// <summary>
        /// The index of an AT input as Python counts (from 0, negative from the end): shifted by <paramref name="delta"/>, negated when
        /// asked, in brackets when a bigger expression needs them.
        /// </summary>
        static string Adjusted(CodeGenerator g, Block b, string input, int delta, bool negate)
        {
            delta--;                                         // the blocks count from 1
            double inner = delta > 0 ? PyOrder.Additive : delta < 0 ? PyOrder.Additive : negate ? PyOrder.UnarySign : PyOrder.None;
            string at = V(g, b, input, inner, "1");
            if (CodeGenerator.IsNumber(at))
            {
                double d = JsValue.ToNumber(at) + delta;
                return N(negate ? -d : d);
            }
            if (delta > 0) at += " + " + delta;
            else if (delta < 0) at += " - " + -delta;
            if (negate) at = delta != 0 ? "-(" + at + ")" : "-" + at;
            return at;
        }

        /// <summary>seq[start:end] for the WHERE1/AT1 and WHERE2/AT2 inputs of a substring or a sublist.</summary>
        static Code SliceOf(CodeGenerator g, Block b, string seq)
        {
            string w1 = b.GetFieldValue("WHERE1"), w2 = b.GetFieldValue("WHERE2");
            string start = "", end = "";
            if (w1 == "FROM_START") start = Adjusted(g, b, "AT1", 0, false);
            else if (w1 == "FROM_END") start = Adjusted(g, b, "AT1", 1, true);
            if (w2 == "FROM_START") end = Adjusted(g, b, "AT2", 1, false);
            else if (w2 == "FROM_END") { end = Adjusted(g, b, "AT2", 0, true); if (end == "0" || end == "-0") end = ""; }
            return C(seq + "[" + start + ":" + end + "]", PyOrder.Member);
        }

        static void Lists(CodeLanguage l)
        {
            l.Value("lists_create_empty", (g, b) => C("[]", PyOrder.Atomic));
            l.Value("lists_create_with", (g, b) =>
            {
                var items = new List<string>();
                for (int i = 0; b.GetInput("ADD" + i) != null; i++) items.Add(V(g, b, "ADD" + i, PyOrder.None, "None"));
                return C("[" + string.Join(", ", items) + "]", PyOrder.Atomic);
            });
            l.Value("lists_repeat", (g, b) => C("[" + V(g, b, "ITEM", PyOrder.None, "None") + "] * " + V(g, b, "NUM", PyOrder.Multiplicative, "0"), PyOrder.Multiplicative));
            l.Value("lists_length", (g, b) => C("len(" + V(g, b, "VALUE", PyOrder.None, "[]") + ")", PyOrder.FunctionCall));
            l.Value("lists_isEmpty", (g, b) => C("not len(" + V(g, b, "VALUE", PyOrder.None, "[]") + ")", PyOrder.LogicalNot));
            l.Value("lists_indexOf", (g, b) =>
            {
                bool first = b.GetFieldValue("END") == "FIRST";
                string find = V(g, b, "FIND", PyOrder.None, "''"), list = V(g, b, "VALUE", PyOrder.None, "[]");
                string fn = first ? g.Helper("first_index", FirstIndex) : g.Helper("last_index", LastIndex);
                return C(fn + "(" + list + ", " + find + ")", PyOrder.FunctionCall);
            });
            l.Value("lists_reverse", (g, b) => C("list(reversed(" + V(g, b, "LIST", PyOrder.None, "[]") + "))", PyOrder.FunctionCall));
            l.Value("lists_getSublist", (g, b) => SliceOf(g, b, V(g, b, "LIST", PyOrder.Member, "[]")));
            l.Value("lists_split", (g, b) =>
            {
                string delim = V(g, b, "DELIM", PyOrder.None, "''");
                if (b.GetFieldValue("MODE") == "JOIN")
                    return C(delim + ".join(map(str, " + V(g, b, "INPUT", PyOrder.None, "[]") + "))", PyOrder.FunctionCall);
                return C(V(g, b, "INPUT", PyOrder.Member, "''") + ".split(" + delim + ")", PyOrder.FunctionCall);
            });
            l.Value("lists_sort", (g, b) =>
            {
                string list = V(g, b, "LIST", PyOrder.None, "[]");
                bool reverse = b.GetFieldValue("DIRECTION") != "1";
                return C(g.Helper("lists_sort", ListsSort) + "(" + list + ", \"" + b.GetFieldValue("TYPE") + "\", " + (reverse ? "True" : "False") + ")", PyOrder.FunctionCall);
            });
            l.Value("lists_getIndex", (g, b) => GetIndex(g, b));
            l.Statement("lists_getIndex", (g, b) => GetIndex(g, b).Text + "\n");
            l.Statement("lists_setIndex", SetIndex);
        }

        const string FirstIndex = @"def %NAME%(my_list, elem):
    try: index = my_list.index(elem) + 1
    except: index = 0
    return index";

        const string LastIndex = @"def %NAME%(my_list, elem):
    try: index = len(my_list) - my_list[::-1].index(elem)
    except: index = 0
    return index";

        const string ListsSort = @"def %NAME%(my_list, type, reverse):
    def try_float(s):
        try:
            return float(s)
        except:
            return 0
    key_funcs = {
        ""NUMERIC"": try_float,
        ""TEXT"": str,
        ""IGNORE_CASE"": lambda s: str(s).lower()
    }
    key_func = key_funcs[type]
    list_cpy = list(my_list)
    return sorted(list_cpy, key=key_func, reverse=reverse)";

        const string RemoveRandomItem = @"def %NAME%(myList):
    x = int(random.random() * len(myList))
    return myList.pop(x)";

        static Code GetIndex(CodeGenerator g, Block b)
        {
            string mode = b.GetFieldValue("MODE") ?? "GET", where = b.GetFieldValue("WHERE") ?? "FROM_START";
            string list = V(g, b, "VALUE", where == "RANDOM" ? PyOrder.None : PyOrder.Member, "[]");
            bool get = mode == "GET";
            double order = get ? PyOrder.Member : PyOrder.FunctionCall;
            switch (where)
            {
                case "FIRST": return C(get ? list + "[0]" : list + ".pop(0)", order);
                case "LAST": return C(get ? list + "[-1]" : list + ".pop()", order);
                case "FROM_START":
                    {
                        string at = Adjusted(g, b, "AT", 0, false);
                        return C(get ? list + "[" + at + "]" : list + ".pop(" + at + ")", order);
                    }
                case "FROM_END":
                    {
                        string at = Adjusted(g, b, "AT", 1, true);
                        return C(get ? list + "[" + at + "]" : list + ".pop(" + at + ")", order);
                    }
                default:
                    g.AddImport("import random");
                    return get ? C("random.choice(" + list + ")", PyOrder.FunctionCall)
                               : C(g.Helper("lists_remove_random_item", RemoveRandomItem) + "(" + list + ")", PyOrder.FunctionCall);
            }
        }

        static string SetIndex(CodeGenerator g, Block b)
        {
            string list = V(g, b, "LIST", PyOrder.Member, "[]");
            string mode = b.GetFieldValue("MODE") ?? "SET", where = b.GetFieldValue("WHERE") ?? "FROM_START";
            string value = V(g, b, "TO", PyOrder.None, "None");
            bool insert = mode == "INSERT";
            string code = "";
            switch (where)
            {
                case "FIRST":
                    return insert ? list + ".insert(0, " + value + ")\n" : list + "[0] = " + value + "\n";
                case "LAST":
                    return insert ? list + ".append(" + value + ")\n" : list + "[-1] = " + value + "\n";
                case "FROM_START":
                    {
                        string at = Adjusted(g, b, "AT", 0, false);
                        return insert ? list + ".insert(" + at + ", " + value + ")\n" : list + "[" + at + "] = " + value + "\n";
                    }
                case "FROM_END":
                    {
                        string at = Adjusted(g, b, "AT", 1, true);
                        return insert ? list + ".insert(" + at + ", " + value + ")\n" : list + "[" + at + "] = " + value + "\n";
                    }
                default:
                    {
                        g.AddImport("import random");
                        if (!Identifier.IsMatch(list)) { string tmp = g.NewName("tmp_list"); code += tmp + " = " + list + "\n"; list = tmp; }
                        string x = "random.randrange(len(" + list + "))";
                        return code + (insert ? list + ".insert(" + x + ", " + value + ")\n" : list + "[" + x + "] = " + value + "\n");
                    }
            }
        }

        // ---------------------------------------------------------------- variables
        static void Variables(CodeLanguage l)
        {
            l.Value("variables_get", (g, b) => C(g.VariableName(b.GetFieldValue("VAR")), PyOrder.Atomic));
            l.Statement("variables_set", (g, b) => g.VariableName(b.GetFieldValue("VAR")) + " = " + V(g, b, "VALUE", PyOrder.None, "0") + "\n");
        }

        // ---------------------------------------------------------------- procedures
        static void Procedures(CodeLanguage l)
        {
            StatementGenerator define = (g, b) =>
            {
                string name = g.ProcedureName(ProcedureInfo.Name(b));
                g.BeginScope();
                var parameters = g.ParameterNames(b);
                string body = g.StatementToCode(b, "STACK");
                string ret = V(g, b, "RETURN", PyOrder.None, "");
                if (ret.Length > 0) ret = g.Language.Indent + "return " + ret + "\n";
                var used = g.EndScope();
                var globals = used.Where(n => !parameters.Contains(n)).ToList();
                string head = globals.Count > 0 ? g.Language.Indent + "global " + string.Join(", ", globals) + "\n" : "";
                string all = head + body + ret;
                if (body.Length == 0 && ret.Length == 0) all = head + g.Language.Indent + "pass\n";
                g.AddDefinition(g.CommentLines(b) + "def " + name + "(" + string.Join(", ", parameters) + "):\n" + all.TrimEnd('\n'));
                return "";
            };
            l.Statement("procedures_defnoreturn", define);
            l.Statement("procedures_defreturn", define);
            Func<CodeGenerator, Block, string> call = (g, b) =>
            {
                var args = new List<string>();
                for (int i = 0; b.GetInput("ARG" + i) != null; i++) args.Add(V(g, b, "ARG" + i, PyOrder.None, "None"));
                return g.ProcedureName(ProcedureInfo.Name(b)) + "(" + string.Join(", ", args) + ")";
            };
            l.Statement("procedures_callnoreturn", (g, b) => call(g, b) + "\n");
            l.Value("procedures_callreturn", (g, b) => C(call(g, b), PyOrder.FunctionCall));
            l.Statement("procedures_ifreturn", (g, b) =>
            {
                string code = "if " + V(g, b, "CONDITION", PyOrder.None, "False") + ":\n";
                code += b.GetInput("VALUE") != null
                    ? g.Language.Indent + "return " + V(g, b, "VALUE", PyOrder.None, "None") + "\n"
                    : g.Language.Indent + "return\n";
                return code;
            });
        }

        // ---------------------------------------------------------------- colour
        static void Colour_(CodeLanguage l)
        {
            l.Value("colour_picker", (g, b) => C(Quote(Colour.Normalize(b.GetFieldValue("COLOUR")) ?? "#000000"), PyOrder.Atomic));
            l.Value("colour_random", (g, b) => { g.AddImport("import random"); return C(g.Helper("colour_random", ColourRandom) + "()", PyOrder.FunctionCall); });
            l.Value("colour_rgb", (g, b) => C(
                g.Helper("colour_rgb", ColourRgb) + "(" + V(g, b, "RED", PyOrder.None, "0") + ", " + V(g, b, "GREEN", PyOrder.None, "0") + ", " + V(g, b, "BLUE", PyOrder.None, "0") + ")", PyOrder.FunctionCall));
            l.Value("colour_blend", (g, b) => C(
                g.Helper("colour_blend", ColourBlend) + "(" + V(g, b, "COLOUR1", PyOrder.None, "'#000000'") + ", " + V(g, b, "COLOUR2", PyOrder.None, "'#000000'") + ", " + V(g, b, "RATIO", PyOrder.None, "0.5") + ")", PyOrder.FunctionCall));
        }

        const string ColourRandom = @"def %NAME%():
    return '#%06x' % random.randint(0, 2**24 - 1)";

        const string ColourRgb = @"def %NAME%(r, g, b):
    r = int(min(100, max(0, r)) * 2.55 + 0.5)
    g = int(min(100, max(0, g)) * 2.55 + 0.5)
    b = int(min(100, max(0, b)) * 2.55 + 0.5)
    return '#%02x%02x%02x' % (r, g, b)";

        const string ColourBlend = @"def %NAME%(colour1, colour2, ratio):
    r1, r2 = int(colour1[1:3], 16), int(colour2[1:3], 16)
    g1, g2 = int(colour1[3:5], 16), int(colour2[3:5], 16)
    b1, b2 = int(colour1[5:7], 16), int(colour2[5:7], 16)
    ratio = min(1, max(0, ratio))
    r = int(r1 * (1 - ratio) + r2 * ratio + 0.5)
    g = int(g1 * (1 - ratio) + g2 * ratio + 0.5)
    b = int(b1 * (1 - ratio) + b2 * ratio + 0.5)
    return '#%02x%02x%02x' % (r, g, b)";
    }
}
