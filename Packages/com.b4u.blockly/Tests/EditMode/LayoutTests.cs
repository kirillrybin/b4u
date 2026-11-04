using System;
using System.IO;
using System.Linq;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class LayoutTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }

        static string Dir => Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs");

        static (Workspace ws, LayoutResult layout) Load(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            return (ws, new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace());
        }

        static (Workspace ws, LayoutResult layout) FromXml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml xmlns=\"https://developers.google.com/blockly/xml\">" + body + "</xml>");
            return (ws, new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace());
        }

        public static System.Collections.Generic.IEnumerable<string> Programs()
            => Directory.GetFiles(Dir, "*.expected.txt").Select(f => Path.GetFileName(f).Replace(".expected.txt", "")).OrderBy(x => x);

        [TestCaseSource(nameof(Programs))]
        public void EveryOutlineTriangulatesWithoutLosingArea(string name)
        {
            var (ws, layout) = Load(name);
            Assert.AreEqual(ws.AllBlocks().Count(), layout.Boxes.Count(b => !b.IsShadow), "one box per real block (shadows come on top)");
            foreach (var box in layout.Boxes)
            {
                Assert.GreaterOrEqual(box.Outline.Length, 4, box.Block.Type);
                foreach (var v in box.Outline) Assert.IsFalse(float.IsNaN(v.X) || float.IsNaN(v.Y), box.Block.Type);
                var tris = Geometry.Triangulate(box.Outline);
                float poly = Math.Abs(Geometry.SignedArea(box.Outline));
                float covered = Geometry.TrianglesArea(box.Outline, tris);
                Assert.AreEqual(poly, covered, 0.5f, box.Block.Type + " outline area vs triangles");
                Assert.Greater(box.W, 0); Assert.Greater(box.H, 0);
            }
        }

        [TestCaseSource(nameof(Programs))]
        public void FieldsNeverHideBehindChildBlocks(string name)
        {
            var (ws, layout) = Load(name);
            foreach (var box in layout.Boxes)
                foreach (var d in box.Block.Descendants())
                {
                    if (d == box.Block) continue;
                    // next blocks sit below, not inside; only compare with real children of this block
                    if (d.Previous?.Target != null && d.Previous.Target.Owner == box.Block && box.Block.Next?.Target?.Owner == d) continue;
                    if (!layout.ByBlock.TryGetValue(d, out var child)) continue;
                    foreach (var f in box.Fields)
                    {
                        bool overlapX = f.X < child.X + child.W - 0.5f && child.X < f.X + f.W - 0.5f;
                        bool overlapY = f.Y < child.Y + child.H - 0.5f && child.Y < f.Y + f.H - 0.5f;
                        Assert.IsFalse(overlapX && overlapY, $"{name}: field '{f.Text}' of {box.Block.Type} is covered by child {d.Type}");
                    }
                }
        }

        [Test]
        public void InlineSlotIsAsWideAsTheChildIncludingItsExternalGrandchildren()
        {
            var (ws, layout) = FromXml(
                "<block type=\"lists_isEmpty\"><value name=\"VALUE\"><block type=\"lists_create_with\"><mutation items=\"2\"></mutation>" +
                "<value name=\"ADD0\"><block type=\"text\"><field name=\"TEXT\">aaaaaaaaaa</field></block></value></block></value></block>");
            var parent = layout.ByBlock[ws.TopBlocks[0]];
            var list = layout.ByBlock[parent.Block.GetInputTargetBlock("VALUE")];
            var item = layout.ByBlock[list.Block.GetInputTargetBlock("ADD0")];
            var label = parent.Fields.Find(f => f.Text == "is empty");
            Assert.NotNull(label);
            Assert.GreaterOrEqual(label.X, item.X + item.W - 0.5f, "label sits right of the widest part of the child");
        }

        [TestCaseSource(nameof(Programs))]
        public void CleanUpSeparatesTopBlocksAndKeepsRunOrder(string name)
        {
            var (ws, _) = Load(name);
            string RunOutput()
            {
                var lines = new System.Collections.Generic.List<string>();
                var it = new Interpreter(ws) { Print = lines.Add };
                it.Start(); it.RunToCompletion();
                return string.Join("\n", lines);
            }
            string before = RunOutput();
            var layouter = new BlockLayouter(ws, new FixedWidth());
            layouter.CleanUp();
            var boxes = ws.TopBlocks.Select(t => { var r = layouter.LayoutStack(t, (float)t.X, (float)t.Y); return (top: t, r.Top, r.Bottom); }).ToList();
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    Assert.IsTrue(boxes[i].Bottom <= boxes[j].Top + 0.01f || boxes[j].Bottom <= boxes[i].Top + 0.01f, name + ": top-level stacks overlap after CleanUp");
            Assert.AreEqual(before, RunOutput(), "cleaning up must not change what the program does");
        }

        [Test]
        public void StackedBlocksStartExactlyBelowThePreviousOne()
        {
            var (ws, layout) = FromXml("<block type=\"text_print\" x=\"10\" y=\"20\"><next><block type=\"text_print\"><next><block type=\"text_print\"/></next></block></next></block>");
            var a = layout.ByBlock[ws.TopBlocks[0]];
            var b = layout.ByBlock[a.Block.NextBlock];
            var c = layout.ByBlock[b.Block.NextBlock];
            Assert.AreEqual(a.X, b.X); Assert.AreEqual(a.Y + a.H, b.Y, 1e-3);
            Assert.AreEqual(b.Y + b.H, c.Y, 1e-3);
        }

        [Test]
        public void ExternalChildSitsAtTheRightEdgeAlignedWithTheRow()
        {
            var (ws, layout) = FromXml("<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">hello</field></block></value></block>");
            var parent = layout.ByBlock[ws.TopBlocks[0]];
            var child = layout.ByBlock[parent.Block.GetInputTargetBlock("TEXT")];
            Assert.AreEqual(parent.X + parent.W, child.X, 1e-3, "child's output tab plugs into the parent's right edge");
            Assert.AreEqual(parent.Y, child.Y, 1e-3);
        }

        [Test]
        public void InlineChildSitsInsideItsSlotAndGrowsTheRow()
        {
            var (ws, layout) = FromXml(
                "<block type=\"math_arithmetic\"><field name=\"OP\">ADD</field>" +
                "<value name=\"A\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value>" +
                "<value name=\"B\"><block type=\"math_arithmetic\"><field name=\"OP\">ADD</field></block></value></block>");
            var parent = layout.ByBlock[ws.TopBlocks[0]];
            var inner = layout.ByBlock[parent.Block.GetInputTargetBlock("B")];
            Assert.Greater(inner.X, parent.X); Assert.Less(inner.X + inner.W, parent.X + parent.W);
            Assert.GreaterOrEqual(inner.Y, parent.Y); Assert.LessOrEqual(inner.Y + inner.H, parent.Y + parent.H);
            Assert.AreEqual(2, parent.Slots.Count);
            var empty = ws.CreateBlock("math_arithmetic");
            var emptyBox = new BlockLayouter(ws, new FixedWidth()).LayoutStack(empty, 0, 0).ByBlock[empty];
            Assert.Greater(parent.H, emptyBox.H - 1, "a nested block makes the row at least as tall as an empty one");
        }

        [Test]
        public void StatementChildrenLiveInsideTheCAndPushTheBottomBarDown()
        {
            string Repeat(int prints)
            {
                var inner = string.Concat(Enumerable.Repeat("<block type=\"text_print\">", prints)) ;
                string s = "<block type=\"controls_repeat_ext\"><value name=\"TIMES\"><shadow type=\"math_number\"><field name=\"NUM\">3</field></shadow></value><statement name=\"DO\">";
                for (int i = 0; i < prints; i++) s += "<block type=\"text_print\"><next>";
                for (int i = 0; i < prints; i++) s += "</next></block>";
                return s + "</statement></block>";
            }
            var (ws1, l1) = FromXml(Repeat(1));
            var (ws3, l3) = FromXml(Repeat(3));
            var p1 = l1.ByBlock[ws1.TopBlocks[0]]; var p3 = l3.ByBlock[ws3.TopBlocks[0]];
            var c1 = l1.ByBlock[ws1.TopBlocks[0].GetInputTargetBlock("DO")];
            Assert.Greater(c1.X, p1.X, "child is indented");
            Assert.Greater(c1.Y, p1.Y);
            Assert.Less(c1.Y + c1.H, p1.Y + p1.H, "child fits above the bottom bar");
            Assert.Greater(p3.H, p1.H + 2 * c1.H - 1, "taller stack makes the C taller");
        }

        [Test]
        public void EmptyStatementInputKeepsMinimumHeight()
        {
            var (ws, layout) = FromXml("<block type=\"controls_repeat_ext\"/>");
            var box = layout.ByBlock[ws.TopBlocks[0]];
            Assert.GreaterOrEqual(box.H, Metrics.RowMinHeight + Metrics.StatementMinHeight + Metrics.BottomBarHeight - 1);
        }

        [Test]
        public void TriangulationHandlesAConcaveShape()
        {
            var l = new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(5, 4), new Vec2(0, 10) };   // arrow notch
            var tris = Geometry.Triangulate(l);
            Assert.AreEqual(3, tris.Length / 3);
            Assert.AreEqual(Math.Abs(Geometry.SignedArea(l)), Geometry.TrianglesArea(l, tris), 1e-3f);
        }
    }
}
