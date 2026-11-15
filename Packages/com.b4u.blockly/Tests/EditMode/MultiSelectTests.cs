using System.Linq;
using Blockly.Editing;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>Editing several stacks at once: copy, paste, delete, disable, move.</summary>
    public class MultiSelectTests
    {
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static string Say(string text, int x, int y, string next = "")
            => "<block type=\"text_print\" x=\"" + x + "\" y=\"" + y + "\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">" + text + "</field></block></value>" + next + "</block>";

        static string Then(string text) => "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">" + text + "</field></block></value></block></next>";

        static Workspace Load(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        static int Count(Workspace ws) => ws.AllBlocks().Count();

        [Test]
        public void StacksMapAnyBlockToItsTopAndDropDuplicates()
        {
            var ws = Load(Say("a", 0, 0, Then("b")) + Say("c", 0, 100));
            var inner = ws.TopBlocks[0].GetInputTargetBlock("TEXT");
            var stacks = WorkspaceEditing.Stacks(new[] { inner, ws.TopBlocks[0].NextBlock, ws.TopBlocks[1] });
            CollectionAssert.AreEqual(new[] { ws.TopBlocks[0], ws.TopBlocks[1] }, stacks);
        }

        [Test]
        public void CopyAndPasteManyKeepsStacksWholeAndTheirArrangement()
        {
            var ws = Load(Say("a", 10, 10, Then("b")) + Say("c", 110, 60));
            string clip = WorkspaceEditing.CopyMany(ws, ws.TopBlocks.ToList());
            var pasted = WorkspaceEditing.PasteMany(ws, clip, 10, 10);
            Assert.AreEqual(2, pasted.Count);
            Assert.AreEqual(4, ws.TopBlocks.Count);
            Assert.IsNotNull(pasted[0].NextBlock, "the follower came with the stack");
            Assert.AreEqual(100, pasted[1].X - pasted[0].X, 0.01);
            Assert.AreEqual(50, pasted[1].Y - pasted[0].Y, 0.01);
            Assert.AreEqual(Count(ws), ws.AllBlocks().Select(b => b.Id).Distinct().Count(), "new ids, no clashes");
        }

        [Test]
        public void PasteManyAlsoAcceptsTheTextOfASingleBlock()
        {
            var ws = Load(Say("a", 0, 0));
            string one = WorkspaceEditing.Copy(ws, ws.TopBlocks[0]);
            var pasted = WorkspaceEditing.PasteMany(ws, one, 0, 0);
            Assert.AreEqual(1, pasted.Count);
            Assert.AreEqual(2, ws.TopBlocks.Count);
        }

        [Test]
        public void PasteManyOfJunkChangesNothing()
        {
            var ws = Load(Say("a", 0, 0));
            Assert.AreEqual(0, WorkspaceEditing.PasteMany(ws, "not json", 0, 0).Count);
            Assert.AreEqual(0, WorkspaceEditing.PasteMany(ws, "{\"blocks\":[{\"block\":{\"type\":\"no_such_block\"}}]}", 0, 0).Count);
            Assert.AreEqual(1, ws.TopBlocks.Count);
        }

        [Test]
        public void DeleteManyRemovesWholeStacks()
        {
            var ws = Load(Say("a", 0, 0, Then("b")) + Say("c", 0, 100) + Say("d", 0, 200));
            int n = WorkspaceEditing.DeleteMany(ws, null, new[] { ws.TopBlocks[0], ws.TopBlocks[1] });
            Assert.AreEqual(2, n);
            Assert.AreEqual(1, ws.TopBlocks.Count);
            Assert.AreEqual(2, Count(ws), "only the print of d and its text are left");
        }

        [Test]
        public void DeleteManySurvivesACallThatWentWithItsDefinition()
        {
            var ws = Load("<block type=\"procedures_defnoreturn\" x=\"0\" y=\"0\"><field name=\"NAME\">go</field></block>" +
                          "<block type=\"procedures_callnoreturn\" x=\"0\" y=\"100\"><mutation name=\"go\"></mutation></block>");
            var def = ws.TopBlocks[0]; var call = ws.TopBlocks[1];
            int n = WorkspaceEditing.DeleteMany(ws, null, new[] { def, call });
            Assert.AreEqual(1, n, "the call was already removed with the definition");
            Assert.AreEqual(0, ws.TopBlocks.Count);
        }

        [Test]
        public void MoveStacksMovesAllByTheSameAmount()
        {
            var ws = Load(Say("a", 10, 10) + Say("c", 50, 70));
            WorkspaceEditing.MoveStacks(ws.TopBlocks, 5, -3);
            Assert.AreEqual(15, ws.TopBlocks[0].X); Assert.AreEqual(7, ws.TopBlocks[0].Y);
            Assert.AreEqual(55, ws.TopBlocks[1].X); Assert.AreEqual(67, ws.TopBlocks[1].Y);
        }

        [Test]
        public void SetDisabledManyDisablesEveryStatementInTheStacksAndNothingElse()
        {
            var ws = Load(Say("a", 0, 0, Then("b")) + Say("c", 0, 100));
            WorkspaceEditing.SetDisabledMany(new[] { ws.TopBlocks[0] }, true);
            Assert.IsTrue(ws.TopBlocks[0].Disabled);
            Assert.IsTrue(ws.TopBlocks[0].NextBlock.Disabled);
            Assert.IsFalse(ws.TopBlocks[1].Disabled);
            Assert.IsFalse(ws.TopBlocks[0].GetInputTargetBlock("TEXT").Disabled, "a value block is part of its parent");
            WorkspaceEditing.SetDisabledMany(new[] { ws.TopBlocks[0] }, false);
            Assert.IsFalse(ws.TopBlocks[0].Disabled);
        }
    }
}
