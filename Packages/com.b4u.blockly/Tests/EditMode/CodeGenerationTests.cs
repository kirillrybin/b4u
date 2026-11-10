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

        static string FindNode()
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Concat(new[] { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin" }))
            {
                try { var p = Path.Combine(dir, "node"); if (File.Exists(p)) return p; } catch (ArgumentException) { }
            }
            return null;
        }

        static string RunNode(string code)
        {
            string node = FindNode();
            if (node == null) Assert.Ignore("Node is not installed");
            string file = Path.Combine(Path.GetTempPath(), "blockly-export-" + Guid.NewGuid().ToString("N") + ".js");
            File.WriteAllText(file, "var lines = [];\nfunction print(s) { lines.push(String(s)); }\n" + code + "\nprocess.stdout.write(lines.join('\\n') + '\\n');\n");
            try
            {
                var psi = new ProcessStartInfo(node, "\"" + file + "\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                using (var p = Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    Assert.AreEqual(0, p.ExitCode, "node failed: " + err + "\n" + code);
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
