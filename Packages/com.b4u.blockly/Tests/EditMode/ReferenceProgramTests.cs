using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>
    /// Differential tests: every fixture program was exported from real Blockly (JSON + XML) together with
    /// the output of the JavaScript Blockly itself generates. The interpreter must reproduce it exactly.
    /// Regenerate with: cd dev/blockly-ref &amp;&amp; node gen-fixtures.js
    /// </summary>
    public class ReferenceProgramTests
    {
        static string Dir => Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs");

        public static IEnumerable<string> Programs()
            => Directory.GetFiles(Dir, "*.expected.txt").Select(f => Path.GetFileName(f).Replace(".expected.txt", "")).OrderBy(x => x);

        static string Run(Workspace ws)
        {
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start();
            it.RunToCompletion(1_000_000);
            Assert.IsNull(it.Error, "interpreter error");
            return string.Join("\n", lines) + "\n";
        }

        static string Expected(string name) => File.ReadAllText(Path.Combine(Dir, name + ".expected.txt")).Replace("\r\n", "\n");

        [TestCaseSource(nameof(Programs))]
        public void JsonProgramMatchesBlockly(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            Assert.AreEqual(Expected(name), Run(ws));
        }

        [TestCaseSource(nameof(Programs))]
        public void XmlProgramMatchesBlockly(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".xml")));
            Assert.AreEqual(Expected(name), Run(ws));
        }

        [TestCaseSource(nameof(Programs))]
        public void JsonRoundTripPreservesBehaviour(string name)
        {
            var a = TestSupport.NewWorkspace();
            WorkspaceJson.Load(a, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            string saved = WorkspaceJson.Save(a);
            var b = TestSupport.NewWorkspace();
            WorkspaceJson.Load(b, saved);
            Assert.AreEqual(saved, WorkspaceJson.Save(b), "save is stable");
            Assert.AreEqual(Expected(name), Run(b));
        }
    }
}
