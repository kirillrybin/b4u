using System.Linq;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>A collapsed block is drawn as one line with a summary; what is inside it stays in the program.</summary>
    public class CollapsedBlockTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static Workspace Load(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        const string Print = "<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">hello</field></block></value>" +
                             "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">bye</field></block></value></block></next></block>";

        [Test]
        public void ACollapsedBlockShowsASummaryAndItsInputsAreNotLaidOut()
        {
            var ws = Load(Print);
            var first = ws.TopBlocks[0];
            var open = new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace();
            first.Collapsed = true;
            var res = new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace();
            var box = res.ByBlock[first];
            Assert.AreEqual(1, box.Fields.Count);
            Assert.AreEqual("print hello", box.Fields[0].Text);
            Assert.IsFalse(box.Fields[0].Editable);
            Assert.IsFalse(res.ByBlock.ContainsKey(first.GetInputTargetBlock("TEXT")));
            Assert.IsTrue(res.ByBlock.ContainsKey(first.NextBlock), "the block after it is a block of its own and stays");
            Assert.IsTrue(box.Connections.ContainsKey(first.Next), "it keeps its connections");
            Assert.IsTrue(open.ByBlock.ContainsKey(first.GetInputTargetBlock("TEXT")));
        }

        [Test]
        public void ALongSummaryIsCutAndAnEmptyInputShowsAQuestionMark()
        {
            var ws = Load("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">" + new string('x', 80) + "</field></block></value></block>" +
                          "<block type=\"text_print\" x=\"0\" y=\"100\"></block>");
            foreach (var b in ws.TopBlocks) b.Collapsed = true;
            var res = new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace();
            var long1 = res.ByBlock[ws.TopBlocks[0]].Fields[0].Text;
            Assert.AreEqual(BlockLayouter.MaxCollapsedText, long1.Length);
            Assert.IsTrue(long1.EndsWith("…"));
            Assert.AreEqual("print ?", res.ByBlock[ws.TopBlocks[1]].Fields[0].Text);
        }

        [Test]
        public void ACollapsedBlockStillRunsWithItsContents()
        {
            var ws = Load(Print);
            ws.TopBlocks[0].Collapsed = true;
            var lines = new System.Collections.Generic.List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion(100000);
            CollectionAssert.AreEqual(new[] { "hello", "bye" }, lines);
        }

        [Test]
        public void TheCollapsedStateRoundTripsThroughJson()
        {
            var ws = Load(Print);
            ws.TopBlocks[0].Collapsed = true;
            var back = TestSupport.NewWorkspace();
            WorkspaceJson.Load(back, WorkspaceJson.Save(ws));
            Assert.IsTrue(back.TopBlocks[0].Collapsed);
            Assert.IsFalse(back.TopBlocks[0].NextBlock.Collapsed);
        }
    }
}
