using System.Collections.Generic;
using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>The mutators of if, text_join and lists_create_with: parts are added, removed and moved, and what is plugged into them stays where it belongs.</summary>
    public class MutatorEditingTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static Workspace Xml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        static BlockLayouter Lay(Workspace ws) => new BlockLayouter(ws, new FixedWidth());

        static string Num(int n) => "<block type=\"math_number\"><field name=\"NUM\">" + n + "</field></block>";
        static string Say(string t) => "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">" + t + "</field></block></value></block>";
        static string Cond(bool v) => "<block type=\"logic_boolean\"><field name=\"BOOL\">" + (v ? "TRUE" : "FALSE") + "</field></block>";

        // if (false) say a; else if (false) say b; else if (true) say c; else say d
        const string IfBlock =
            "<block type=\"controls_if\" x=\"0\" y=\"0\"><mutation elseif=\"2\" else=\"1\"></mutation>" +
            "<value name=\"IF0\">{c0}</value><statement name=\"DO0\">{s0}</statement>" +
            "<value name=\"IF1\">{c1}</value><statement name=\"DO1\">{s1}</statement>" +
            "<value name=\"IF2\">{c2}</value><statement name=\"DO2\">{s2}</statement>" +
            "<statement name=\"ELSE\">{se}</statement></block>";

        static Workspace IfWorkspace() => Xml(IfBlock
            .Replace("{c0}", Cond(false)).Replace("{s0}", Say("a"))
            .Replace("{c1}", Cond(false)).Replace("{s1}", Say("b"))
            .Replace("{c2}", Cond(true)).Replace("{s2}", Say("c"))
            .Replace("{se}", Say("d")));

        static string Text(Block print) => print.GetInputTargetBlock("TEXT").GetFieldValue("TEXT");
        static string Branch(Block b, string input) => Text(b.GetInputTargetBlock(input));
        static string Run(Workspace ws)
        {
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion(100000);
            return string.Join(",", lines);
        }

        // ---------------------------------------------------------------- if
        [Test]
        public void TheIfBlockListsItsPartsInOrder()
        {
            var parts = MutatorEditing.Parts(IfWorkspace().TopBlocks[0]);
            CollectionAssert.AreEqual(new[] { PartKind.ElseIf, PartKind.ElseIf, PartKind.Else }, parts.Select(p => p.Kind));
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, parts.Select(p => p.Index));
        }

        [Test]
        public void AddingAnElseIfAppendsAnEmptyOneBeforeTheElseAndKeepsEverythingPluggedIn()
        {
            var ws = IfWorkspace(); var b = ws.TopBlocks[0];
            Assert.IsTrue(MutatorEditing.AddElseIf(ws, Lay(ws), b));
            Assert.AreEqual(3, MutatorEditing.ElseIfCount(b));
            Assert.IsNull(b.GetInputTargetBlock("IF3"));
            Assert.IsNull(b.GetInputTargetBlock("DO3"));
            Assert.AreEqual("a", Branch(b, "DO0")); Assert.AreEqual("b", Branch(b, "DO1")); Assert.AreEqual("c", Branch(b, "DO2"));
            Assert.AreEqual("d", Branch(b, "ELSE"), "the else is still last and keeps its body");
            Assert.AreEqual("c", Run(ws), "the program still runs the third branch");
        }

        [Test]
        public void AddingTheElseOnlyWorksOnceAndItIsLast()
        {
            var ws = Xml("<block type=\"controls_if\" x=\"0\" y=\"0\"></block>"); var b = ws.TopBlocks[0];
            Assert.IsTrue(MutatorEditing.AddElse(ws, Lay(ws), b));
            Assert.IsTrue(MutatorEditing.HasElse(b));
            Assert.IsFalse(MutatorEditing.AddElse(ws, Lay(ws), b), "an if has at most one else");
            MutatorEditing.AddElseIf(ws, Lay(ws), b);
            Assert.AreEqual("ELSE", b.Inputs.Last().Name, "an else if added later still goes before the else");
        }

        [Test]
        public void RemovingAnElseIfPullsTheLaterOnesUpAndLeavesItsBlocksInTheWorkspace()
        {
            var ws = IfWorkspace(); var b = ws.TopBlocks[0];
            var gone = b.GetInputTargetBlock("DO1");
            Assert.IsTrue(MutatorEditing.Remove(ws, Lay(ws), b, new MutatorPart(PartKind.ElseIf, 1)));
            Assert.AreEqual(1, MutatorEditing.ElseIfCount(b));
            Assert.AreEqual("c", Branch(b, "DO1"), "the next else if moved up with its blocks");
            Assert.AreEqual("d", Branch(b, "ELSE"));
            Assert.Contains(gone, ws.TopBlocks, "the body of the removed branch is not lost");
            Assert.IsNull(gone.Previous.Target);
        }

        [Test]
        public void RemovingTheElseUnplugsItsBody()
        {
            var ws = IfWorkspace(); var b = ws.TopBlocks[0];
            var body = b.GetInputTargetBlock("ELSE");
            Assert.IsTrue(MutatorEditing.Remove(ws, Lay(ws), b, new MutatorPart(PartKind.Else, 0)));
            Assert.IsFalse(MutatorEditing.HasElse(b));
            Assert.Contains(body, ws.TopBlocks);
        }

        [Test]
        public void MovingAnElseIfCarriesItsConditionAndBodyAndTheFirstBranchAndElseStay()
        {
            var ws = IfWorkspace(); var b = ws.TopBlocks[0];
            Assert.IsTrue(MutatorEditing.Move(ws, Lay(ws), b, new MutatorPart(PartKind.ElseIf, 2), -1));
            Assert.AreEqual("c", Branch(b, "DO1")); Assert.AreEqual("b", Branch(b, "DO2"));
            Assert.AreEqual("TRUE", b.GetInputTargetBlock("IF1").GetFieldValue("BOOL"), "the condition went with its body");
            Assert.AreEqual("a", Branch(b, "DO0")); Assert.AreEqual("d", Branch(b, "ELSE"));
            Assert.AreEqual("c", Run(ws));
            Assert.IsFalse(MutatorEditing.Move(ws, Lay(ws), b, new MutatorPart(PartKind.ElseIf, 1), -1), "the first else if cannot go above the if branch");
            Assert.IsFalse(MutatorEditing.Move(ws, Lay(ws), b, new MutatorPart(PartKind.ElseIf, 2), 1), "nor below the last else if");
            Assert.IsFalse(MutatorEditing.Move(ws, Lay(ws), b, new MutatorPart(PartKind.Else, 0), -1), "the else does not move");
        }

        [Test]
        public void TheReshapedIfSurvivesAJsonRoundTrip()
        {
            var ws = IfWorkspace(); var b = ws.TopBlocks[0];
            MutatorEditing.Remove(ws, Lay(ws), b, new MutatorPart(PartKind.ElseIf, 1));
            var back = TestSupport.NewWorkspace();
            WorkspaceJson.Load(back, WorkspaceJson.Save(ws));
            var nb = back.TopBlocks.First(x => x.Type == "controls_if");
            Assert.AreEqual(1, MutatorEditing.ElseIfCount(nb));
            Assert.AreEqual("c", Branch(nb, "DO1"));
        }

        [Test]
        public void ARemovedElseIfShowsInTheExportedCodeOnlyAsWhatIsLeft()
        {
            var ws = IfWorkspace(); var b = ws.TopBlocks[0];
            MutatorEditing.Remove(ws, Lay(ws), b, new MutatorPart(PartKind.ElseIf, 2));
            string js = CodeLanguage.JavaScript.Generate(ws);
            StringAssert.Contains("else if", js);
            StringAssert.Contains("console.log('b');", js);
            StringAssert.DoesNotContain("console.log('c');", js.Substring(0, js.IndexOf("else {") < 0 ? js.Length : js.IndexOf("else {")));
        }

        // ---------------------------------------------------------------- join and list
        const string Join = "<block type=\"text_join\" x=\"0\" y=\"0\"><mutation items=\"3\"></mutation>" +
                            "<value name=\"ADD0\"><block type=\"text\"><field name=\"TEXT\">x</field></block></value>" +
                            "<value name=\"ADD1\"><block type=\"text\"><field name=\"TEXT\">y</field></block></value>" +
                            "<value name=\"ADD2\"><block type=\"text\"><field name=\"TEXT\">z</field></block></value></block>";

        static string Item(Block b, int i) => b.GetInputTargetBlock("ADD" + i)?.GetFieldValue("TEXT");

        [Test]
        public void AddingAnItemAppendsAnEmptyInput()
        {
            var ws = Xml(Join); var b = ws.TopBlocks[0];
            Assert.IsTrue(MutatorEditing.AddItem(ws, Lay(ws), b));
            Assert.AreEqual(4, MutatorEditing.ItemCount(b));
            Assert.AreEqual("x", Item(b, 0)); Assert.AreEqual("z", Item(b, 2)); Assert.IsNull(Item(b, 3));
        }

        [Test]
        public void RemovingAnItemClosesTheGapAndUnplugsItsBlock()
        {
            var ws = Xml(Join); var b = ws.TopBlocks[0];
            var gone = b.GetInputTargetBlock("ADD1");
            Assert.IsTrue(MutatorEditing.Remove(ws, Lay(ws), b, new MutatorPart(PartKind.Item, 1)));
            Assert.AreEqual(2, MutatorEditing.ItemCount(b));
            Assert.AreEqual("x", Item(b, 0)); Assert.AreEqual("z", Item(b, 1));
            Assert.Contains(gone, ws.TopBlocks);
        }

        [Test]
        public void MovingAnItemSwapsItWithItsNeighbour()
        {
            var ws = Xml(Join); var b = ws.TopBlocks[0];
            Assert.IsTrue(MutatorEditing.Move(ws, Lay(ws), b, new MutatorPart(PartKind.Item, 0), 1));
            Assert.AreEqual("y", Item(b, 0)); Assert.AreEqual("x", Item(b, 1)); Assert.AreEqual("z", Item(b, 2));
            Assert.IsFalse(MutatorEditing.Move(ws, Lay(ws), b, new MutatorPart(PartKind.Item, 0), -1));
            Assert.IsFalse(MutatorEditing.Move(ws, Lay(ws), b, new MutatorPart(PartKind.Item, 2), 1));
        }

        [Test]
        public void AListWithNoItemsShowsTheEmptyLabelAndGrowsAgain()
        {
            var ws = Xml("<block type=\"lists_create_with\" x=\"0\" y=\"0\"><mutation items=\"1\"></mutation></block>"); var b = ws.TopBlocks[0];
            Assert.IsTrue(MutatorEditing.Remove(ws, Lay(ws), b, new MutatorPart(PartKind.Item, 0)));
            Assert.IsNotNull(b.GetInput("EMPTY"));
            Assert.AreEqual(0, MutatorEditing.ItemCount(b));
            Assert.IsTrue(MutatorEditing.AddItem(ws, Lay(ws), b));
            Assert.IsNull(b.GetInput("EMPTY"));
            Assert.AreEqual(1, MutatorEditing.ItemCount(b));
        }

        [Test]
        public void OtherBlocksAndShadowsHaveNoMutatorToOpen()
        {
            var ws = Xml(Say("hi"));
            Assert.IsFalse(MutatorEditing.CanMutate(ws.TopBlocks[0]));
            Assert.IsFalse(MutatorEditing.AddItem(ws, Lay(ws), ws.TopBlocks[0]));
            Assert.IsTrue(MutatorEditing.CanMutate(Xml(Join).TopBlocks[0]));
        }

        // ---------------------------------------------------------------- layout
        [Test]
        public void TheGearIsReservedOnlyWhenAskedForAndOnlyOnBlocksWithAMutator()
        {
            var ws = Xml(Join + Say("p").Replace("<block type=\"text_print\">", "<block type=\"text_print\" x=\"0\" y=\"200\">"));
            var plain = Lay(ws).LayoutWorkspace();
            Assert.IsNull(plain.ByBlock[ws.TopBlocks[0]].MutatorIcon, "off by default");
            var on = new BlockLayouter(ws, new FixedWidth()) { ShowMutators = true }.LayoutWorkspace();
            var join = on.ByBlock[ws.TopBlocks[0]]; var print = on.ByBlock[ws.TopBlocks[1]];
            Assert.IsNotNull(join.MutatorIcon);
            Assert.IsNull(print.MutatorIcon);
            Assert.AreEqual(plain.ByBlock[ws.TopBlocks[0]].W + BlockLayouter.CommentIconSize + BlockGeometry.Current.ItemGap, join.W, 0.01f);
        }

        [Test]
        public void TheGearComesBeforeTheCommentIcon()
        {
            var ws = Xml(Join); ws.TopBlocks[0].Comment = "note";
            var box = new BlockLayouter(ws, new FixedWidth()) { ShowMutators = true }.LayoutWorkspace().ByBlock[ws.TopBlocks[0]];
            Assert.Less(box.MutatorIcon.X, box.CommentIcon.X);
            Assert.Less(box.CommentIcon.X, box.Fields[0].X);
        }
    }
}
