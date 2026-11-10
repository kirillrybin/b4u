using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>
    /// The exported JavaScript must do what the program does. Every fixture program of ReferenceProgramTests is exported, run in
    /// Node and compared with the output real Blockly produced for it. Skipped when Node is not installed.
    /// </summary>
    public class CodeGenerationTests
    {
        static string Dir => Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs");

        static Workspace Load(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            return ws;
        }

        static string Js(Workspace ws) => CodeLanguage.JavaScript.Generate(ws, new CodeOptions { Print = "print" });

        static string FindTool(string name)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Concat(new[] { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin" }))
            {
                try { var p = Path.Combine(dir, name); if (File.Exists(p)) return p; } catch (ArgumentException) { }
            }
            return null;
        }

        static string RunNode(string code)
            => RunTool("node", ".js", "var lines = [];\nfunction print(s) { lines.push(String(s)); }\n" + code + "\nprocess.stdout.write(lines.join('\\n') + '\\n');\n", code);

        /// <summary>The JavaScript way of writing a value (true, null, 2 for 2.0, a list joined by commas), so Python prints what the interpreter printed.</summary>
        const string PythonShow = @"import sys
_lines = []
def show(x):
    def fmt(x):
        if x is True: return 'true'
        if x is False: return 'false'
        if x is None: return 'null'
        if isinstance(x, float):
            if x != x: return 'NaN'
            if x in (float('inf'), float('-inf')): return 'Infinity' if x > 0 else '-Infinity'
            if x == int(x) and abs(x) < 1e21: return str(int(x))
            return repr(x)
        if isinstance(x, list): return ','.join('' if e is None else fmt(e) for e in x)
        return str(x)
    _lines.append(fmt(x))
";

        static string RunPython(string code)
            => RunTool("python3", ".py", PythonShow + "\n" + code + "\n\nsys.stdout.write('\\n'.join(_lines) + '\\n')\n", code);

        static string RunTool(string tool, string extension, string program, string generated)
        {
            string exe = FindTool(tool);
            if (exe == null) Assert.Ignore(tool + " is not installed");
            string file = Path.Combine(Path.GetTempPath(), "blockly-export-" + Guid.NewGuid().ToString("N") + extension);
            File.WriteAllText(file, program);
            try
            {
                var psi = new ProcessStartInfo(exe, "\"" + file + "\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                using (var p = Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    Assert.AreEqual(0, p.ExitCode, tool + " failed: " + err + "\n" + generated);
                    return output.Replace("\r\n", "\n");
                }
            }
            finally { File.Delete(file); }
        }

        public static IEnumerable<string> Programs() => ReferenceProgramTests.Programs();

        [TestCaseSource(nameof(Programs))]
        public void ExportedJavaScriptPrintsWhatBlocklyPrints(string name)
        {
            string expected = File.ReadAllText(Path.Combine(Dir, name + ".expected.txt")).Replace("\r\n", "\n");
            string code = Js(Load(name));
            Assert.AreEqual(expected, RunNode(code), code);
        }

        /// <summary>
        /// Output lines that Python cannot print the way JavaScript does, per program: the Python of the block does what Python does
        /// (1 / 0 raises, an index out of range raises, "" cannot split, 1 == '1' is false, an unset variable is None, 1e-7 is 1e-07).
        /// The rest of the output must match.
        /// </summary>
        static readonly Dictionary<string, int[]> PythonDiffers = new Dictionary<string, int[]>
        {
            { "arithmetic", new[] { 6, 9 } },            // 1 / 0 raises; 1e-07
            { "lists", new[] { 12 } },                   // [8] of a list of four raises
            { "lists_more", new[] { 7, 12 } },           // split by "" raises; equal keys keep their order in Python's stable sort
            { "logic", new[] { 9 } },                    // 1 == '1' is false
            { "math", new[] { 5, 8 } },                  // math.log10(1000) is exactly 3; sqrt(-1) raises
            { "number_format", new[] { 2, 3, 14 } },     // 1e-07, 1e-06, 5e-08
            { "text_more", new[] { 5, 6 } },             // 'hello'[8] raises
            { "variables", new[] { 1 } },                // an unset variable is None, not undefined
        };

        [TestCaseSource(nameof(Programs))]
        public void ExportedPythonPrintsWhatBlocklyPrints(string name)
        {
            string[] expected = File.ReadAllText(Path.Combine(Dir, name + ".expected.txt")).Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            string code = CodeLanguage.Python.Generate(Load(name), new CodeOptions { Print = "show" });
            if (!PythonDiffers.TryGetValue(name, out var skip))
            {
                Assert.AreEqual(string.Join("\n", expected) + "\n", RunPython(code), code);
                return;
            }
            // one statement may fail without stopping the rest: a printing line that raises prints a marker
            var lines = code.Split('\n').Select(l => l.StartsWith("show(") ? "try:\n    " + l + "\nexcept Exception as e:\n    show('<' + type(e).__name__ + '>')" : l);
            string[] actual = RunPython(string.Join("\n", lines)).TrimEnd('\n').Split('\n');
            var differing = new List<int>();
            for (int i = 0; i < Math.Max(expected.Length, actual.Length); i++)
                if (i >= expected.Length || i >= actual.Length || expected[i] != actual[i]) differing.Add(i + 1);
            CollectionAssert.AreEqual(skip, differing, "lines that differ (1-based); expected " + string.Join(",", skip) + "\n" + string.Join("\n", differing.Select(n => n + ": " + (n <= expected.Length ? expected[n - 1] : "-") + "  |  " + (n <= actual.Length ? actual[n - 1] : "-"))) + "\n" + code);
        }

        /// <summary>Programs whose export is, letter for letter, the code real Blockly generates (print aside).</summary>
        static readonly string[] SameAsBlockly =
            { "arithmetic", "break_continue", "if_else", "lists_more", "logic", "loops", "math", "number_format", "text", "text_more", "variables" };

        [TestCaseSource(nameof(SameAsBlockly))]
        public void TheTextIsTheOneBlocklyGenerates(string name)
        {
            string real = File.ReadAllText(Path.Combine(Dir, name + ".js")).Replace("\r\n", "\n");
            string mine = CodeLanguage.JavaScript.Generate(Load(name), new CodeOptions { Print = "window.alert" });
            Assert.AreEqual(Squeeze(real), Squeeze(mine));
        }

        static string Squeeze(string code) => System.Text.RegularExpressions.Regex.Replace(code.Trim(), @"\n\n+", "\n\n");

        [Test]
        public void TheCodeReadsLikeBlocklysOwn()
        {
            var ws = Load("if_else");
            string code = Js(ws);
            StringAssert.StartsWith("var x;\n\n\nx = 5;\nif (x < 3) {\n  print('small');\n} else if (x < 6) {", code);
        }

        [Test]
        public void ProceduresAreWrittenAsFunctionsBeforeTheCode()
        {
            string code = Js(Load("procedures"));
            StringAssert.Contains("function fact(n) {\n  if (n <= 1) {\n    return 1;\n  }\n  return n * fact(n - 1);\n}", code);
            Assert.Less(code.IndexOf("function greet"), code.IndexOf("greet('world');"));
        }

        [Test]
        public void PythonUsesIndentsPassAndGlobals()
        {
            string code = CodeLanguage.Python.Generate(Load("procedures"));
            StringAssert.Contains("def fact(n):\n    if n <= 1:\n        return 1\n    return n * fact(n - 1)", code);
            StringAssert.Contains("def firstOver(limit):\n    global i\n    for i in range(1, 101):", code);
            StringAssert.Contains("greet('world')\n", code);
            code = CodeLanguage.Python.Generate(Load("if_else"));
            StringAssert.StartsWith("x = None\n\n\nx = 5\nif x < 3:\n    print('small')\nelif x < 6:", code);
            StringAssert.Contains("else:\n    print('else-branch')", code);
        }

        [Test]
        public void AnEmptyBodyIsWrittenAsPassAndImportsComeFirst()
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, "{\"blocks\":{\"languageVersion\":0,\"blocks\":[{\"type\":\"controls_repeat_ext\",\"id\":\"r\",\"x\":0,\"y\":0,"
                + "\"inputs\":{\"TIMES\":{\"shadow\":{\"type\":\"math_number\",\"fields\":{\"NUM\":3}}}},"
                + "\"next\":{\"block\":{\"type\":\"text_print\",\"inputs\":{\"TEXT\":{\"block\":{\"type\":\"math_single\",\"fields\":{\"OP\":\"ROOT\"},"
                + "\"inputs\":{\"NUM\":{\"shadow\":{\"type\":\"math_number\",\"fields\":{\"NUM\":16}}}}}}}}}}]}}");
            Assert.AreEqual("import math\n\n\nfor count in range(3):\n    pass\nprint(math.sqrt(16))\n", CodeLanguage.Python.Generate(ws));
        }

        [Test]
        public void BothLanguagesAreOffered()
        {
            CollectionAssert.AreEqual(new[] { "JavaScript", "Python" }, CodeLanguage.All.Select(l => l.Name).ToArray());
            CollectionAssert.AreEqual(new[] { ".js", ".py" }, CodeLanguage.All.Select(l => l.FileExtension).ToArray());
        }

        [Test]
        public void ThePrintFunctionCanBeChosen()
        {
            var ws = Load("if_else");
            StringAssert.Contains("console.log('done');", CodeLanguage.JavaScript.Generate(ws));
            StringAssert.Contains("alert('done');", CodeLanguage.JavaScript.Generate(ws, new CodeOptions { Print = "alert" }));
        }

        [Test]
        public void ABlockWithoutAGeneratorIsReportedAndOneCanBeAdded()
        {
            var reg = new BlockRegistry();
            reg.RegisterJson("[{\"type\":\"my_say\",\"message0\":\"say\",\"previousStatement\":null,\"nextStatement\":null,\"colour\":160}]");
            var ws = new Workspace(reg);
            ws.TopBlocks.Add(ws.CreateBlock("my_say"));
            var e = Assert.Throws<NotSupportedException>(() => CodeLanguage.JavaScript.Generate(ws));
            StringAssert.Contains("my_say", e.Message);
            CodeLanguage.JavaScript.Statement("my_say", (g, b) => "say();\n");
            try { Assert.AreEqual("say();\n", CodeLanguage.JavaScript.Generate(ws)); }
            finally { CodeLanguage.JavaScript.Statement("my_say", null); }
        }
    }
}
