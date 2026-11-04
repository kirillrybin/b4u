using System.Collections.Generic;
using System.Linq;
using Blockly.Editing;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class CopyPasteTests
    {
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

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

        const string Stack =
            "<variables><variable id=\"va\">a</variable></variables>" +
            "<block type=\"variables_set\" x=\"40\" y=\"50\"><field name=\"VAR\" id=\"va\">a</field><value name=\"VALUE\"><block type=\"math_number\"><field name=\"NUM\">7</field></block></value>" +
            "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"variables_get\"><field name=\"VAR\" id=\"va\">a</field></block></value></block></next></block>";

        [Test]
        public void ACopyHasTheBlockAndItsInputsButNotItsFollowersAndNoIds()
        {
            var ws = FromXml(Stack);
            var set = ws.TopBlocks[0];
            string text = WorkspaceEditing.Copy(ws, set);
            StringAssert.Contains("variables_set", text);
            StringAssert.Contains("math_number", text);
            StringAssert.DoesNotContain("text_print", text);
            StringAssert.DoesNotContain("\"x\"", text);
            foreach (var b in ws.AllBlocks()) StringAssert.DoesNotContain("\"" + b.Id + "\"", text.Replace("\"va\"", ""), "block id " + b.Id + " leaked");
            StringAssert.Contains("\"va\"", text, "a variable is referenced by id");
        }

        [Test]
        public void PastingMakesAnIndependentTopBlockWithFreshIdsOffsetFromTheOriginal()
        {
            var ws = FromXml(Stack);
            var set = ws.TopBlocks[0];
            string text = WorkspaceEditing.Copy(ws, set);
            int before = ws.AllBlocks().Count();

            var pasted = WorkspaceEditing.Paste(ws, text, (float)set.X, (float)set.Y);
            Assert.IsNotNull(pasted);
            Assert.AreEqual(2, ws.TopBlocks.Count);
            Assert.AreEqual(before + 2, ws.AllBlocks().Count(), "the set block and its number, not the print after it");
            Assert.AreEqual(set.X + WorkspaceEditing.PasteOffset, pasted.X);
            Assert.AreEqual(set.Y + WorkspaceEditing.PasteOffset, pasted.Y);
            Assert.AreEqual(ws.AllBlocks().Count(), ws.AllBlocks().Select(b => b.Id).Distinct().Count(), "ids stay unique");
            Assert.IsNull(pasted.NextBlock);
            Assert.AreSame(ws.GetVariableById("va"), ws.GetVariableById(pasted.GetField("VAR").Value), "same variable, not a new one");
            Assert.AreEqual(1, ws.Variables.Count);
        }

        [Test]
        public void PastingTwiceDoesNotStackTheCopiesOnTopOfEachOther()
        {
            var ws = FromXml(Stack);
            var set = ws.TopBlocks[0];
            string text = WorkspaceEditing.Copy(ws, set);
            var a = WorkspaceEditing.Paste(ws, text, (float)set.X, (float)set.Y);
            var b = WorkspaceEditing.Paste(ws, text, (float)set.X, (float)set.Y);
            Assert.AreNotEqual(a.X, b.X);
            Assert.AreNotEqual(a.Y, b.Y);
        }

        [Test]
        public void ACopiedExpressionRunsLikeTheOriginal()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">hi</field></block></value></block>");
            Assert.AreEqual("hi", Run(ws));
            var print = ws.TopBlocks[0];
            var copy = WorkspaceEditing.Paste(ws, WorkspaceEditing.Copy(ws, print), 0, 0);
            ws.TopBlocks.Remove(copy);
            print.Next.Connect(copy.Previous);                       // chain the copy under the original
            Assert.AreEqual("hi,hi", Run(ws));
        }

        [Test]
        public void ACopiedProcedureDefinitionGetsADifferentName()
        {
            var ws = FromXml("<block type=\"procedures_defnoreturn\" x=\"0\" y=\"0\"><field name=\"NAME\">greet</field></block>");
            var def = ws.TopBlocks[0];
            var pasted = WorkspaceEditing.Paste(ws, WorkspaceEditing.Copy(ws, def), 0, 0);
            Assert.AreNotEqual("greet", pasted.GetFieldValue("NAME"));
            Assert.AreEqual(2, ws.AllBlocks().Select(b => b.GetFieldValue("NAME")).Distinct().Count());
        }

        [Test]
        public void PastingGarbageLeavesTheWorkspaceAlone()
        {
            var ws = FromXml(Stack);
            string before = WorkspaceJson.Save(ws);
            Assert.IsNull(WorkspaceEditing.Paste(ws, null, 0, 0));
            Assert.IsNull(WorkspaceEditing.Paste(ws, "", 0, 0));
            Assert.IsNull(WorkspaceEditing.Paste(ws, "not json", 0, 0));
            Assert.IsNull(WorkspaceEditing.Paste(ws, "[1,2]", 0, 0));
            Assert.IsNull(WorkspaceEditing.Paste(ws, "{\"type\":\"no_such_block\"}", 0, 0));
            Assert.AreEqual(before, WorkspaceJson.Save(ws));
        }

        [Test]
        public void ACopyOfAShadowCopiesItsOwner()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><shadow type=\"text\"><field name=\"TEXT\">sh</field></shadow></value></block>");
            var shadow = ws.TopBlocks[0].Inputs.First(i => i.Name == "TEXT").Connection.Shadow;
            Assert.IsNotNull(shadow);
            StringAssert.Contains("text_print", WorkspaceEditing.Copy(ws, shadow));
        }

        [Test]
        public void CopyKeepsNestedStatementBlocksIncludingTheirOwnFollowers()
        {
            var ws = FromXml("<block type=\"controls_repeat_ext\" x=\"0\" y=\"0\"><value name=\"TIMES\"><block type=\"math_number\"><field name=\"NUM\">2</field></block></value>" +
                "<statement name=\"DO\"><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">a</field></block></value>" +
                "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">b</field></block></value></block></next></block></statement></block>");
            var pasted = WorkspaceEditing.Paste(ws, WorkspaceEditing.Copy(ws, ws.TopBlocks[0]), 0, 0);
            Assert.AreEqual(2, pasted.Descendants().Count(b => b.Type == "text_print"));
            Assert.AreEqual(ws.AllBlocks().Count(), ws.AllBlocks().Select(b => b.Id).Distinct().Count());
        }
    }
}
