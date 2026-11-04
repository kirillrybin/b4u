using System.Collections.Generic;
using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class ProcedureEditingTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        // greet(): prints "hi"; main: greet()
        const string NoParams =
            "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"0\"><field name=\"NAME\">greet</field><statement name=\"STACK\">" +
            "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">hi</field></block></value></block></statement></block>" +
            "<block type=\"procedures_callnoreturn\" x=\"0\" y=\"200\"><mutation name=\"greet\"></mutation></block>";

        static Workspace FromXml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        static string Run(Workspace ws)
        {
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion(100000);
            return string.Join(",", lines) + (it.Error != null ? "!" + it.Error : "");
        }

        static BlockLayouter Layouter(Workspace ws) => new BlockLayouter(ws, new FixedWidth());
        static Block Def(Workspace ws) => ws.AllBlocks().First(ProcedureInfo.IsDefinition);
        static Block Call(Workspace ws) => ws.AllBlocks().First(b => b.Type == "procedures_callnoreturn");
        static bool Set(Workspace ws, params string[] names) => ProcedureEditing.SetParams(ws, Layouter(ws), Def(ws), names);

        [Test]
        public void ParsingAndValidatingNames()
        {
            CollectionAssert.AreEqual(new[] { "a", "b c", "d" }, ProcedureEditing.ParseNames(" a ,b   c,, d "));
            Assert.AreEqual(0, ProcedureEditing.ParseNames("").Count);
            Assert.AreEqual(0, ProcedureEditing.ParseNames(null).Count);
            Assert.IsNull(ProcedureEditing.Validate("a, b"));
            Assert.IsNull(ProcedureEditing.Validate(""));
            StringAssert.Contains("Duplicate", ProcedureEditing.Validate("a, b, a"));
        }

        [Test]
        public void AddingParametersGivesTheDefinitionAndItsCallsMatchingShapes()
        {
            var ws = FromXml(NoParams);
            Assert.IsTrue(Set(ws, "x", "y"));
            CollectionAssert.AreEqual(new[] { "x", "y" }, ProcedureEditing.CurrentNames(Def(ws)));
            Assert.IsTrue(Def(ws).GetInput("TOPROW").Fields.Any(f => f.IsLabel && f.Value == "with: x, y"));
            var call = Call(ws);
            Assert.IsNotNull(call.GetInput("ARG0")); Assert.IsNotNull(call.GetInput("ARG1")); Assert.IsNull(call.GetInput("ARG2"));
            Assert.AreEqual("x", call.GetInput("ARG0").Fields.First(f => f.IsLabel).Value);
            Assert.IsNotNull(ws.GetVariableByName("x"));
            Assert.AreEqual("hi", Run(ws), "the program still runs");
        }

        [Test]
        public void ParametersSurviveASaveAndLoad()
        {
            var ws = FromXml(NoParams);
            Set(ws, "x", "y");
            string json = WorkspaceJson.Save(ws);
            var again = TestSupport.NewWorkspace();
            WorkspaceJson.Load(again, json);
            CollectionAssert.AreEqual(new[] { "x", "y" }, ProcedureEditing.CurrentNames(Def(again)));
            Assert.AreEqual(json, WorkspaceJson.Save(again));
        }

        [Test]
        public void ARenamedParameterKeepsItsVariableSoTheBodyFollows()
        {
            var ws = FromXml(
                "<variables><variable id=\"vn\">n</variable></variables>" +
                "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"0\"><mutation><arg name=\"n\" varid=\"vn\"></arg></mutation><field name=\"NAME\">show</field><statement name=\"STACK\">" +
                "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"variables_get\"><field name=\"VAR\" id=\"vn\">n</field></block></value></block></statement></block>" +
                "<block type=\"procedures_callnoreturn\" x=\"0\" y=\"200\"><mutation name=\"show\"><arg name=\"n\"></arg></mutation>" +
                "<value name=\"ARG0\"><block type=\"math_number\"><field name=\"NUM\">5</field></block></value></block>");
            Assert.AreEqual("5", Run(ws));
            Assert.IsTrue(Set(ws, "count"));
            Assert.AreEqual("count", ws.GetVariableById("vn").Name, "the variable was renamed, not replaced");
            Assert.AreEqual("count", Call(ws).GetInput("ARG0").Fields.First(f => f.IsLabel).Value);
            Assert.AreEqual("5", Run(ws), "the argument still reaches the body");
            Assert.IsNotNull(Call(ws).GetInput("ARG0").Connection.Target, "the plugged value stayed");
        }

        [Test]
        public void RemovingAParameterFreesTheBlockPluggedIntoItsInput()
        {
            var ws = FromXml(
                "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"0\"><mutation><arg name=\"a\"></arg><arg name=\"b\"></arg></mutation><field name=\"NAME\">f</field></block>" +
                "<block type=\"procedures_callnoreturn\" x=\"0\" y=\"200\"><mutation name=\"f\"><arg name=\"a\"></arg><arg name=\"b\"></arg></mutation>" +
                "<value name=\"ARG0\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value>" +
                "<value name=\"ARG1\"><block type=\"math_number\"><field name=\"NUM\">2</field></block></value></block>");
            int blocks = ws.AllBlocks().Count();
            Assert.IsTrue(Set(ws, "a"));
            Assert.IsNull(Call(ws).GetInput("ARG1"));
            Assert.AreEqual("1", Call(ws).GetInputTargetBlock("ARG0").GetFieldValue("NUM"));
            Assert.AreEqual(blocks, ws.AllBlocks().Count(), "no block was lost");
            Assert.AreEqual(3, ws.TopBlocks.Count, "the freed number is a top-level block now");
            BlockDraggerTests.AssertModelIsSound(ws, blocks, "remove parameter");
        }

        [Test]
        public void ClearingTheParametersWorksAndSameNamesChangeNothing()
        {
            var ws = FromXml(NoParams);
            Assert.IsFalse(Set(ws), "already empty");
            Set(ws, "x");
            Assert.IsFalse(Set(ws, "x"));
            Assert.IsTrue(Set(ws));
            Assert.AreEqual(0, ProcedureEditing.CurrentNames(Def(ws)).Count);
            Assert.IsNull(Call(ws).GetInput("ARG0"));
            Assert.IsFalse(Def(ws).GetInput("TOPROW").Fields.Any(f => f.IsLabel && f.Value.StartsWith("with: ")));
        }

        [Test]
        public void ANewParameterReachesTheBodyThroughAVariable()
        {
            var ws = FromXml(NoParams);
            Set(ws, "msg");
            var print = ws.AllBlocks().First(b => b.Type == "text_print");
            var text = print.GetInputTargetBlock("TEXT");
            text.Previous?.Disconnect(); text.Output?.Disconnect();
            var get = ws.CreateBlock("variables_get");
            get.GetField("VAR").Value = ws.GetVariableByName("msg").Id;
            print.GetInput("TEXT").Connection.Connect(get.Output);
            var arg = ws.CreateBlock("text");
            arg.GetField("TEXT").Value = "hello";
            Call(ws).GetInput("ARG0").Connection.Connect(arg.Output);
            Assert.AreEqual("hello", Run(ws));
        }

        [Test]
        public void ANameAlreadyUsedByAnotherVariableReusesThatVariable()
        {
            var ws = FromXml("<variables><variable id=\"vz\">z</variable></variables>" + NoParams);
            Set(ws, "p");
            Assert.IsTrue(Set(ws, "z"));
            Assert.AreEqual("vz", ProcedureInfo.Params(Def(ws))[0].Id);
            Assert.IsNotNull(ws.GetVariableByName("p"), "the old variable is untouched");
        }

        [Test]
        public void OnlyCallsOfThatProcedureChange()
        {
            var ws = FromXml(NoParams +
                "<block type=\"procedures_defnoreturn\" x=\"300\" y=\"0\"><field name=\"NAME\">other</field></block>" +
                "<block type=\"procedures_callnoreturn\" x=\"300\" y=\"200\"><mutation name=\"other\"></mutation></block>");
            Set(ws, "x");
            var otherCall = ws.AllBlocks().First(b => b.Type == "procedures_callnoreturn" && ProcedureInfo.Name(b) == "other");
            Assert.IsNull(otherCall.GetInput("ARG0"));
        }

        [Test]
        public void ANonDefinitionIsRefused()
        {
            var ws = FromXml(NoParams);
            Assert.IsFalse(ProcedureEditing.SetParams(ws, Layouter(ws), Call(ws), new[] { "x" }));
        }
    }
}
