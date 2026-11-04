using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class ProgramRunnerTests
    {
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static string Print(string text, string next = "")
            => "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">" + text + "</field></block></value>"
               + (next == "" ? "" : "<next>" + next + "</next>") + "</block>";

        static string Text(Block b) => b.GetInputTargetBlock("TEXT")?.GetFieldValue("TEXT");

        sealed class Probe
        {
            public readonly ProgramRunner Runner;
            public readonly List<string> Output = new List<string>();
            public readonly List<string> Highlights = new List<string>();     // text of the print block that was highlighted
            public readonly List<string> Finished = new List<string>();       // "ok" or the error
            public Probe(Workspace ws)
            {
                Runner = new ProgramRunner(ws);
                Runner.Output += Output.Add;
                Runner.CurrentBlockChanged += b => Highlights.Add(b == null ? "-" : (b.Type == "text_print" ? Text(b) : b.Type));
                Runner.Finished += e => Finished.Add(e ?? "ok");
            }
        }

        static Probe FromXml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return new Probe(ws);
        }

        static void PumpUntilDone(ProgramRunner r, int maxFrames = 100000)
        {
            for (int i = 0; i < maxFrames && r.State == RunnerState.Running; i++) r.Pump(0.016f);
        }

        // ---- the same reference programs, but through the runner path the UI uses ----
        static string Dir => Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs");

        public static IEnumerable<string> Programs()
            => Directory.GetFiles(Dir, "*.expected.txt").Select(f => Path.GetFileName(f).Replace(".expected.txt", "")).OrderBy(x => x);

        [TestCaseSource(nameof(Programs))]
        public void RunProducesTheSameOutputAsRealBlockly(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            var p = new Probe(ws);
            p.Runner.Run();
            PumpUntilDone(p.Runner);
            Assert.AreEqual(RunnerState.Finished, p.Runner.State);
            Assert.AreEqual(new[] { "ok" }, p.Finished.ToArray());
            Assert.AreEqual(File.ReadAllText(Path.Combine(Dir, name + ".expected.txt")).Replace("\r\n", "\n"), string.Join("\n", p.Output) + "\n");
            Assert.IsNull(p.Runner.CurrentBlock, "nothing stays highlighted after the program ends");
        }

        // ---- stepping ----
        [Test]
        public void StepPausesOnTheBlockThatIsAboutToRun()
        {
            var p = FromXml(Print("a", Print("b", Print("c"))));
            p.Runner.Step();
            Assert.AreEqual(RunnerState.Paused, p.Runner.State);
            Assert.AreEqual("a", Text(p.Runner.CurrentBlock), "highlighted, but not executed yet");
            Assert.IsEmpty(p.Output);

            p.Runner.Step();
            Assert.AreEqual("b", Text(p.Runner.CurrentBlock));
            Assert.AreEqual(new[] { "a" }, p.Output.ToArray(), "stepping on moved past a");

            p.Runner.Step();
            Assert.AreEqual("c", Text(p.Runner.CurrentBlock));
            Assert.AreEqual(new[] { "a", "b" }, p.Output.ToArray());

            p.Runner.Step();
            Assert.AreEqual(RunnerState.Finished, p.Runner.State);
            Assert.AreEqual(new[] { "a", "b", "c" }, p.Output.ToArray());
            Assert.AreEqual(new[] { "ok" }, p.Finished.ToArray());
            Assert.AreEqual(new[] { "a", "b", "c", "-" }, p.Highlights.ToArray());
        }

        [Test]
        public void PumpDoesNotAdvanceWhilePausedAndRunResumes()
        {
            var p = FromXml(Print("a", Print("b")));
            p.Runner.Step();
            for (int i = 0; i < 50; i++) p.Runner.Pump(0.016f);
            Assert.IsEmpty(p.Output, "paused: frames do nothing");
            p.Runner.Run();
            PumpUntilDone(p.Runner);
            Assert.AreEqual(new[] { "a", "b" }, p.Output.ToArray());
        }

        [Test]
        public void StepWhileRunningPausesIt()
        {
            var p = FromXml(Print("a", Print("b", Print("c"))));
            p.Runner.Run();
            p.Runner.Step();
            Assert.AreEqual(RunnerState.Paused, p.Runner.State);
        }

        // ---- slow mode ----
        [Test]
        public void SlowModeRunsOneStatementPerInterval()
        {
            var p = FromXml(Print("a", Print("b", Print("c"))));
            p.Runner.Slow = true; p.Runner.SlowInterval = 0.25f;
            p.Runner.Run();

            p.Runner.Pump(0.01f);                                   // first block starts immediately
            Assert.AreEqual(new[] { "a" }, p.Highlights.ToArray());
            p.Runner.Pump(0.1f); p.Runner.Pump(0.1f);               // 0.2s < interval: no new block
            Assert.AreEqual(new[] { "a" }, p.Highlights.ToArray());
            p.Runner.Pump(0.1f);                                    // 0.3s: next one
            Assert.AreEqual(new[] { "a", "b" }, p.Highlights.ToArray());
            p.Runner.Pump(0.3f);
            Assert.AreEqual(new[] { "a", "b", "c" }, p.Highlights.ToArray());
        }

        // ---- stop / errors / robustness ----
        [Test]
        public void StopClearsTheHighlightAndHaltsExecution()
        {
            var p = FromXml(Print("a", Print("b", Print("c"))));
            p.Runner.Step(); p.Runner.Step();
            p.Runner.Stop();
            Assert.AreEqual(RunnerState.Idle, p.Runner.State);
            Assert.IsNull(p.Runner.CurrentBlock);
            int before = p.Output.Count;
            for (int i = 0; i < 20; i++) p.Runner.Pump(0.016f);
            Assert.AreEqual(before, p.Output.Count);
            Assert.IsEmpty(p.Finished, "a manual stop is not 'finished'");
        }

        [Test]
        public void RunningAgainAfterTheEndStartsOver()
        {
            var p = FromXml(Print("a"));
            p.Runner.Run(); PumpUntilDone(p.Runner);
            p.Runner.Run(); PumpUntilDone(p.Runner);
            Assert.AreEqual(new[] { "a", "a" }, p.Output.ToArray());
            Assert.AreEqual(new[] { "ok", "ok" }, p.Finished.ToArray());
        }

        [Test]
        public void RuntimeErrorsAreReportedOnce()
        {
            var p = FromXml("<block type=\"procedures_callnoreturn\"><mutation name=\"nope\"></mutation></block>");
            p.Runner.Run(); PumpUntilDone(p.Runner);
            Assert.AreEqual(new[] { "nope is not defined" }, p.Finished.ToArray());
            Assert.AreEqual("nope is not defined", p.Runner.Error);
        }

        [Test]
        public void InfiniteLoopNeverBlocksAFrameAndCanBeStopped()
        {
            var p = FromXml("<block type=\"controls_whileUntil\"><field name=\"MODE\">WHILE</field>" +
                            "<value name=\"BOOL\"><block type=\"logic_boolean\"><field name=\"BOOL\">TRUE</field></block></value></block>");
            p.Runner.BudgetMs = 4;
            p.Runner.Run();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++) p.Runner.Pump(0.016f);
            Assert.Less(sw.ElapsedMilliseconds, 400, "each frame is bounded by the budget");
            Assert.AreEqual(RunnerState.Running, p.Runner.State);
            p.Runner.Stop();
            Assert.AreEqual(RunnerState.Idle, p.Runner.State);
        }

        [Test]
        public void StepOnAStatementlessInfiniteLoopReturnsInsteadOfHanging()
        {
            var p = FromXml("<block type=\"controls_whileUntil\"><field name=\"MODE\">WHILE</field>" +
                            "<value name=\"BOOL\"><block type=\"logic_boolean\"><field name=\"BOOL\">TRUE</field></block></value></block>");
            p.Runner.StepBudgetMs = 20;
            p.Runner.Step();                       // highlights the loop itself
            p.Runner.Step();                       // the body is empty: only the budget gets us back
            Assert.AreEqual(RunnerState.Paused, p.Runner.State);
        }
    }
}
