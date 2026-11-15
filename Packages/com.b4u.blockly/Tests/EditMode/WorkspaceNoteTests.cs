using Blockly.Editing;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>Notes on the workspace (Blockly's workspace comments): saved with the program, read from Blockly files, never run or exported.</summary>
    public class WorkspaceNoteTests
    {
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";
        const string Print = "<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">hello</field></block></value></block>";

        static Workspace Xml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        [Test]
        public void AJsonRoundTripKeepsTheNote()
        {
            var ws = Xml(Print);
            var n = WorkspaceCommentEditing.Add(ws, 30, 40);
            n.Text = "remember\nthis"; n.Collapsed = true; n.W = 200; n.H = 80;
            var back = TestSupport.NewWorkspace();
            WorkspaceJson.Load(back, WorkspaceJson.Save(ws));
            Assert.AreEqual(1, back.Comments.Count);
            var c = back.Comments[0];
            Assert.AreEqual(n.Id, c.Id);
            Assert.AreEqual("remember\nthis", c.Text);
            Assert.AreEqual(30, c.X); Assert.AreEqual(40, c.Y);
            Assert.AreEqual(200, c.W); Assert.AreEqual(80, c.H);
            Assert.IsTrue(c.Collapsed);
            Assert.AreEqual(1, back.TopBlocks.Count);
        }

        [Test]
        public void AFileWithOnlyNotesLoads()
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, "{\"workspaceComments\":[{\"id\":\"a\",\"x\":5,\"y\":6,\"width\":120,\"height\":60,\"text\":\"hi\"}]}");
            Assert.AreEqual(0, ws.TopBlocks.Count);
            Assert.AreEqual("hi", ws.Comments[0].Text);
            Assert.AreEqual(120, ws.Comments[0].W);
        }

        [Test]
        public void AWorkspaceWithoutNotesWritesNoNotesKey()
        {
            StringAssert.DoesNotContain("workspaceComments", WorkspaceJson.Save(Xml(Print)));
        }

        [Test]
        public void ABlocklyXmlNoteLoads()
        {
            var ws = Xml("<comment id=\"n1\" x=\"10\" y=\"20\" w=\"150\" h=\"90\" collapsed=\"true\">note text</comment>" + Print);
            var c = ws.Comments[0];
            Assert.AreEqual("n1", c.Id); Assert.AreEqual("note text", c.Text);
            Assert.AreEqual(10, c.X); Assert.AreEqual(20, c.Y); Assert.AreEqual(150, c.W); Assert.AreEqual(90, c.H);
            Assert.IsTrue(c.Collapsed);
            Assert.AreEqual(1, ws.TopBlocks.Count);
        }

        [Test]
        public void ClearAndRestoreReplaceTheNotes()
        {
            var ws = Xml(Print);
            WorkspaceCommentEditing.Add(ws, 1, 2);
            string saved = WorkspaceJson.Save(ws);
            WorkspaceCommentEditing.Add(ws, 3, 4);
            WorkspaceJson.Restore(ws, saved);
            Assert.AreEqual(1, ws.Comments.Count);
            ws.Clear();
            Assert.AreEqual(0, ws.Comments.Count);
        }

        [Test]
        public void EditingChangesTheNoteAndReportsWhetherItDid()
        {
            var ws = Xml(Print);
            var n = WorkspaceCommentEditing.Add(ws, 10, 10);
            Assert.AreEqual(WorkspaceCommentEditing.DefaultWidth, n.W);
            Assert.IsTrue(WorkspaceCommentEditing.Move(n, 50, 60));
            Assert.IsFalse(WorkspaceCommentEditing.Move(n, 50, 60));
            Assert.IsTrue(WorkspaceCommentEditing.SetText(n, "x"));
            Assert.IsFalse(WorkspaceCommentEditing.SetText(n, "x"));
            Assert.IsTrue(WorkspaceCommentEditing.SetCollapsed(n, true));
            Assert.IsFalse(WorkspaceCommentEditing.SetCollapsed(n, true));
            Assert.IsTrue(WorkspaceCommentEditing.Remove(ws, n));
            Assert.AreEqual(0, ws.Comments.Count);
        }

        [Test]
        public void NewNotesGetDistinctIds()
        {
            var ws = Xml(Print);
            var a = WorkspaceCommentEditing.Add(ws, 0, 0);
            var b = WorkspaceCommentEditing.Add(ws, 0, 0);
            WorkspaceCommentEditing.Remove(ws, a);
            var c = WorkspaceCommentEditing.Add(ws, 0, 0);
            Assert.AreNotEqual(b.Id, c.Id);
        }

        [Test]
        public void NotesAreNotInTheExportedCode()
        {
            var ws = Xml(Print);
            string without = CodeLanguage.JavaScript.Generate(ws, new CodeOptions { Print = "print" });
            WorkspaceCommentEditing.Add(ws, 0, 0).Text = "secret note";
            string with = CodeLanguage.JavaScript.Generate(ws, new CodeOptions { Print = "print" });
            Assert.AreEqual(without, with);
        }
    }
}
