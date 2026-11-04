using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class InterpreterTests
    {
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static string Print(string text, string next = "")
            => "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">" + text + "</field></block></value>"
               + (next == "" ? "" : "<next>" + next + "</next>") + "</block>";

        static (Interpreter it, List<string> output) Make(string xmlBody)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + xmlBody + "</xml>");
            var output = new List<string>();
            return (new Interpreter(ws) { Print = output.Add }, output);
        }

        [Test]
        public void StepRunsOneStatementAtATime()
        {
            var (it, output) = Make(Print("a", Print("b", Print("c"))));
            it.Start();
            var seen = new List<int>();
            while (it.Step()) seen.Add(output.Count);
            Assert.AreEqual(new[] { "a", "b", "c" }, output.ToArray());
            // output grows progressively, not all at once
            Assert.Greater(new HashSet<int>(seen).Count, 1);
            Assert.IsFalse(it.IsRunning);
            Assert.IsNull(it.Error);
        }

        [Test]
        public void StopHaltsExecution()
        {
            var (it, output) = Make(Print("a", Print("b", Print("c"))));
            it.Start();
            it.Step();
            it.Stop();
            Assert.IsFalse(it.IsRunning);
            Assert.IsFalse(it.Step());
            Assert.Less(output.Count, 3);
        }

        [Test]
        public void InfiniteLoopIsStoppedByStepLimit()
        {
            var (it, _) = Make(
                "<block type=\"controls_whileUntil\"><field name=\"MODE\">WHILE</field>" +
                "<value name=\"BOOL\"><block type=\"logic_boolean\"><field name=\"BOOL\">TRUE</field></block></value></block>");
            it.Start();
            Assert.IsFalse(it.RunToCompletion(500));
            Assert.AreEqual("Step limit exceeded", it.Error);
        }

        [Test]
        public void RunForYieldsBackToTheFrameWhileProgramContinues()
        {
            var (it, _) = Make(
                "<block type=\"controls_whileUntil\"><field name=\"MODE\">WHILE</field>" +
                "<value name=\"BOOL\"><block type=\"logic_boolean\"><field name=\"BOOL\">TRUE</field></block></value></block>");
            it.Start();
            Assert.IsTrue(it.RunFor(5));      // budget spent, program still alive: the frame is not blocked forever
            Assert.IsTrue(it.IsRunning);
            Assert.Greater(it.StepCount, 0);
            it.Stop();
        }

        [Test]
        public void BlockExecutingFiresInProgramOrder()
        {
            var (it, _) = Make(Print("a", Print("b")));
            var seen = new List<string>();
            it.BlockExecuting += b => seen.Add(b.GetInputTargetBlock("TEXT").GetFieldValue("TEXT"));
            it.Start();
            it.RunToCompletion();
            Assert.AreEqual(new[] { "a", "b" }, seen.ToArray());
        }

        [Test]
        public void TopBlocksRunTopToBottom()
        {
            var (it, output) = Make(
                "<block type=\"text_print\" x=\"0\" y=\"200\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">second</field></block></value></block>" +
                "<block type=\"text_print\" x=\"0\" y=\"10\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">first</field></block></value></block>");
            it.Start();
            it.RunToCompletion();
            Assert.AreEqual(new[] { "first", "second" }, output.ToArray());
        }

        [Test]
        public void DisabledBlocksAreSkipped()
        {
            var (it, output) = Make(Print("a", Print("b", Print("c"))).Replace("<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">b", "<block type=\"text_print\" disabled=\"true\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">b"));
            it.Start();
            it.RunToCompletion();
            Assert.AreEqual(new[] { "a", "c" }, output.ToArray());
        }

        [Test]
        public void MissingHandlerReportsErrorInsteadOfCrashing()
        {
            var ws = TestSupport.NewWorkspace();
            ws.Registry.RegisterJson("{\"type\":\"my_custom\",\"message0\":\"custom\",\"previousStatement\":null,\"nextStatement\":null,\"colour\":0}");
            WorkspaceXml.Load(ws, "<xml " + Ns + "><block type=\"my_custom\"></block></xml>");
            var it = new Interpreter(ws);
            it.Start();
            Assert.IsFalse(it.RunToCompletion());
            StringAssert.Contains("my_custom", it.Error);
        }

        [Test]
        public void CustomBlocksCanBeAddedToTheRegistry()
        {
            var ws = TestSupport.NewWorkspace();
            ws.Registry.RegisterJson("{\"type\":\"my_say\",\"message0\":\"say %1\",\"args0\":[{\"type\":\"input_value\",\"name\":\"WHAT\"}],\"previousStatement\":null,\"nextStatement\":null,\"colour\":0}");
            var handlers = new HandlerRegistry();
            StandardHandlersForTests.Copy(handlers);
            handlers.Simple("my_say", (it, b) => it.Print("said:" + JsValue.ToStr(it.EvalInput(b, "WHAT", ""))));
            WorkspaceXml.Load(ws, "<xml " + Ns + "><block type=\"my_say\"><value name=\"WHAT\"><block type=\"text\"><field name=\"TEXT\">hi</field></block></value></block></xml>");
            var output = new List<string>();
            var run = new Interpreter(ws, handlers) { Print = output.Add };
            run.Start();
            run.RunToCompletion();
            Assert.AreEqual(new[] { "said:hi" }, output.ToArray());
        }
    }

    public class RecursionTests
    {
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";
        static string Num(int n) => "<block type=\"math_number\"><field name=\"NUM\">" + n + "</field></block>";
        static string Var(string name) => "<block type=\"variables_get\"><field name=\"VAR\" id=\"id_" + name + "\">" + name + "</field></block>";
        static string Arg(string name) => "<arg name=\"" + name + "\" varid=\"id_" + name + "\"></arg>";

        // sum(n) = n <= 0 ? 0 : n + sum(n - 1)      (recursion through an expression)
        const string SumDef =
            "<block type=\"procedures_defreturn\"><mutation><arg name=\"n\" varid=\"id_n\"></arg></mutation><field name=\"NAME\">sum</field>" +
            "<value name=\"RETURN\"><block type=\"logic_ternary\">" +
            "<value name=\"IF\"><block type=\"logic_compare\"><field name=\"OP\">LTE</field><value name=\"A\">" + "VARN" + "</value><value name=\"B\">" + "ZERO" + "</value></block></value>" +
            "<value name=\"THEN\">ZERO</value>" +
            "<value name=\"ELSE\"><block type=\"math_arithmetic\"><field name=\"OP\">ADD</field><value name=\"A\">VARN</value><value name=\"B\">" +
            "<block type=\"procedures_callreturn\"><mutation name=\"sum\"><arg name=\"p\"></arg></mutation><value name=\"ARG0\"><block type=\"math_arithmetic\"><field name=\"OP\">MINUS</field><value name=\"A\">VARN</value><value name=\"B\">ONE</value></block></value></block>" +
            "</value></block></value></block></value></block>";

        static string Sum() => SumDef.Replace("VARN", Var("n")).Replace("ZERO", Num(0)).Replace("ONE", Num(1));

        static string PrintCallSum(int n)
            => "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"procedures_callreturn\"><mutation name=\"sum\"><arg name=\"p\"></arg></mutation><value name=\"ARG0\">" + Num(n) + "</value></block></value></block>";

        // inf() = inf() + 1
        const string InfDef =
            "<block type=\"procedures_defreturn\"><field name=\"NAME\">inf</field><value name=\"RETURN\"><block type=\"math_arithmetic\"><field name=\"OP\">ADD</field>" +
            "<value name=\"A\"><block type=\"procedures_callreturn\"><mutation name=\"inf\"></mutation></block></value><value name=\"B\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value></block></value></block>";

        // loop(): statement recursion with no base case
        const string LoopDef =
            "<block type=\"procedures_defnoreturn\"><field name=\"NAME\">loop</field><statement name=\"STACK\"><block type=\"procedures_callnoreturn\"><mutation name=\"loop\"></mutation></block></statement></block>";

        static (string output, string error) RunSmallStack(string body)
        {
            string output = null, error = null;
            var t = new Thread(() =>
            {
                var ws = TestSupport.NewWorkspace();
                WorkspaceXml.Load(ws, "<xml " + Ns + "><variables><variable id=\"id_n\">n</variable></variables>" + body + "</xml>");
                var lines = new List<string>();
                var it = new Interpreter(ws) { Print = lines.Add };
                it.Start();
                it.RunToCompletion();
                output = string.Join("|", lines);
                error = it.Error;
            }, 512 * 1024);   // smaller than Unity's main-thread stack on every platform
            t.Start();
            t.Join();
            return (output, error);
        }

        [Test]
        public void RecursionBelowTheLimitWorksOnASmallStack()
        {
            var (output, error) = RunSmallStack(Sum() + PrintCallSum(150));
            Assert.IsNull(error);
            Assert.AreEqual((150 * 151 / 2).ToString(), output);
        }

        [Test]
        public void InfiniteExpressionRecursionReportsErrorInsteadOfOverflowing()
        {
            var (_, error) = RunSmallStack(InfDef +
                "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"procedures_callreturn\"><mutation name=\"inf\"></mutation></block></value></block>");
            Assert.AreEqual("Maximum call stack size exceeded", error);
        }

        [Test]
        public void InfiniteStatementRecursionReportsErrorInsteadOfOverflowing()
        {
            var (_, error) = RunSmallStack(LoopDef + "<block type=\"procedures_callnoreturn\"><mutation name=\"loop\"></mutation></block>");
            Assert.AreEqual("Maximum call stack size exceeded", error);
        }

        [Test]
        public void CallingAnUndefinedProcedureIsAnError()
        {
            var (_, error) = RunSmallStack("<block type=\"procedures_callnoreturn\"><mutation name=\"nope\"></mutation></block>");
            Assert.AreEqual("nope is not defined", error);
        }
    }

    static class StandardHandlersForTests
    {
        // A fresh registry has no handlers; custom blocks here only need "text".
        public static void Copy(HandlerRegistry r) => r.Expression("text", (it, b) => b.GetFieldValue("TEXT"));
    }
}
