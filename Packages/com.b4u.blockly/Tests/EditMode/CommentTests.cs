using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>Comments on blocks: saved with the program, drawn as an icon, written into the exported code.</summary>
    public class CommentTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static Workspace Xml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        const string Print = "<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">hello</field></block></value></block>";

        static Workspace Commented(string comment, bool pinned = false)
        {
            var ws = Xml(Print);
            ws.TopBlocks[0].Comment = comment;
            ws.TopBlocks[0].CommentPinned = pinned;
            return ws;
        }

        // ---------------------------------------------------------------- the file formats
        [Test]
        public void AJsonRoundTripKeepsTheCommentAndWhetherItIsOpen()
        {
            var back = TestSupport.NewWorkspace();
            WorkspaceJson.Load(back, WorkspaceJson.Save(Commented("say hi\nthen stop", true)));
            Assert.AreEqual("say hi\nthen stop", back.TopBlocks[0].Comment);
            Assert.IsTrue(back.TopBlocks[0].CommentPinned);
        }

        [Test]
        public void ABlockWithoutACommentWritesNoIconsAndAnEmptyCommentIsKept()
        {
            StringAssert.DoesNotContain("icons", WorkspaceJson.Save(Xml(Print)));
            var back = TestSupport.NewWorkspace();
            WorkspaceJson.Load(back, WorkspaceJson.Save(Commented("")));
            Assert.AreEqual("", back.TopBlocks[0].Comment);
        }

        [Test]
        public void ABlocklyJsonCommentLoadsWithItsSizeIgnored()
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, "{\"blocks\":{\"blocks\":[{\"type\":\"text_print\",\"x\":0,\"y\":0,\"icons\":{\"comment\":{\"text\":\"note\",\"pinned\":true,\"height\":80,\"width\":160}}}]}}");
            Assert.AreEqual("note", ws.TopBlocks[0].Comment);
            Assert.IsTrue(ws.TopBlocks[0].CommentPinned);
        }

        [Test]
        public void ABlocklyXmlCommentLoads()
        {
            var ws = Xml("<block type=\"text_print\" x=\"0\" y=\"0\"><comment pinned=\"true\" h=\"80\" w=\"160\">a note</comment></block>");
            Assert.AreEqual("a note", ws.TopBlocks[0].Comment);
            Assert.IsTrue(ws.TopBlocks[0].CommentPinned);
            Assert.IsNull(Xml(Print).TopBlocks[0].Comment);
        }

        [Test]
        public void ACopyPasteAndUndoSnapshotKeepTheComment()
        {
            var ws = Commented("kept");
            var pasted = WorkspaceEditing.Paste(ws, WorkspaceEditing.Copy(ws, ws.TopBlocks[0]), 10, 10);
            Assert.AreEqual("kept", pasted.Comment);
            var history = new UndoHistory(ws);
            string before = history.Snapshot();
            ws.TopBlocks[0].Comment = "changed";
            history.Commit(before);
            history.Undo();
            Assert.AreEqual("kept", ws.TopBlocks.First(b => b.Y == 0).Comment);
        }

        // ---------------------------------------------------------------- layout
        [Test]
        public void ACommentAddsAnIconAtTheStartOfTheFirstRowAndWidensTheBlock()
        {
            var plain = new BlockLayouter(Xml(Print), new FixedWidth()).LayoutWorkspace();
            var ws = Commented("note");
            var res = new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace();
            var box = res.ByBlock[ws.TopBlocks[0]];
            var plainBox = plain.Boxes[0];
            Assert.IsNotNull(box.CommentIcon);
            Assert.IsNull(plainBox.CommentIcon);
            Assert.AreEqual(BlockLayouter.CommentIconSize, box.CommentIcon.W);
            Assert.Less(box.CommentIcon.X, box.Fields[0].X, "the icon comes before the first field");
            Assert.AreEqual(plainBox.W + BlockLayouter.CommentIconSize + BlockGeometry.Current.ItemGap, box.W, 0.01f);
        }

        [Test]
        public void WithCommentsOffTheBlockIsLaidOutAsIfItHadNone()
        {
            var plain = new BlockLayouter(Xml(Print), new FixedWidth()).LayoutWorkspace().Boxes[0];
            var ws = Commented("note");
            var off = new BlockLayouter(ws, new FixedWidth()) { ShowComments = false }.LayoutWorkspace().ByBlock[ws.TopBlocks[0]];
            Assert.IsNull(off.CommentIcon);
            Assert.AreEqual(plain.W, off.W, 0.01f);
            Assert.AreEqual(plain.Fields[0].X, off.Fields[0].X, 0.01f);
        }

        [Test]
        public void ACollapsedBlockKeepsItsIcon()
        {
            var ws = Commented("note");
            ws.TopBlocks[0].Collapsed = true;
            Assert.IsNotNull(new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace().ByBlock[ws.TopBlocks[0]].CommentIcon);
        }

        // ---------------------------------------------------------------- code
        static string Js(Workspace ws, CodeOptions o = null) => CodeLanguage.JavaScript.Generate(ws, o ?? new CodeOptions());
        static string Py(Workspace ws, CodeOptions o = null) => CodeLanguage.Python.Generate(ws, o ?? new CodeOptions());

        [Test]
        public void TheCommentIsWrittenAboveTheBlocksCode()
        {
            var ws = Commented("say hello");
            Assert.AreEqual("// say hello\nconsole.log('hello');\n", Js(ws));
            Assert.AreEqual("# say hello\nprint('hello')\n", Py(ws));
        }

        [Test]
        public void ALongCommentIsCutAtSpacesAndEveryLineOfItIsACommentLine()
        {
            var ws = Commented("one two three four five six seven eight nine ten eleven twelve thirteen fourteen\nsecond line");
            var lines = Js(ws).Split('\n').Where(l => l.StartsWith("//")).ToList();
            Assert.AreEqual(3, lines.Count);
            Assert.IsTrue(lines.All(l => l.Length <= 3 + CodeGenerator.CommentWrap));
            Assert.AreEqual("// second line", lines[2]);
        }

        [Test]
        public void ACommentInsideABodyIsIndented()
        {
            var ws = Xml("<block type=\"controls_repeat_ext\" x=\"0\" y=\"0\"><value name=\"TIMES\"><block type=\"math_number\"><field name=\"NUM\">2</field></block></value>" +
                         "<statement name=\"DO\">" + Print.Replace(" x=\"0\" y=\"0\"", "") + "</statement></block>");
            ws.TopBlocks[0].GetInputTargetBlock("DO").Comment = "inner";
            StringAssert.Contains("\n  // inner\n  console.log('hello');", Js(ws));
            StringAssert.Contains("\n    # inner\n    print('hello')", Py(ws));
        }

        [Test]
        public void AnEmptyCommentWritesNothingAndTheOptionTurnsCommentsOff()
        {
            Assert.AreEqual("console.log('hello');\n", Js(Commented("")));
            Assert.AreEqual("console.log('hello');\n", Js(Commented("   ")));
            Assert.AreEqual("console.log('hello');\n", Js(Commented("note"), new CodeOptions { Comments = false }));
            Assert.AreEqual("print('hello')\n", Py(Commented("note"), new CodeOptions { Comments = false }));
        }

        [Test]
        public void AProcedureDefinitionCarriesItsCommentAboveTheFunction()
        {
            var ws = Xml("<block type=\"procedures_defnoreturn\" x=\"0\" y=\"0\"><field name=\"NAME\">greet</field><statement name=\"STACK\">" +
                         Print.Replace(" x=\"0\" y=\"0\"", "") + "</statement></block>" +
                         "<block type=\"procedures_callnoreturn\" x=\"0\" y=\"200\"><mutation name=\"greet\"></mutation></block>");
            ws.TopBlocks.First(b => b.Type == "procedures_defnoreturn").Comment = "greets";
            string js = Js(ws);
            StringAssert.Contains("// greets\nfunction greet() {", js);
            StringAssert.DoesNotContain("// greets\ngreet();", js, "the comment is not repeated at the call");
            StringAssert.Contains("# greets\ndef greet():", Py(ws));
        }
    }
}
