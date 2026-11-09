using System;
using System.Collections.Generic;
using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class ToolboxTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static ToolboxDefinition Toolbox => TestSupport.NewToolbox();

        static Workspace FromXml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        static string Run(Workspace ws, out string error)
        {
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion(200000);
            error = it.Error;
            return string.Join(",", lines);
        }

        static IEnumerable<ToolboxCategory> Categories => Toolbox.Categories;

        // ---------------------------------------------------------------- definition
        [Test]
        public void StandardToolboxHasTheBlocklyCategories()
        {
            var t = Toolbox;
            Assert.AreEqual(new[] { "Logic", "Loops", "Math", "Text", "Lists", "Colour", "Variables", "Functions" }, t.Categories.Select(c => c.Name).ToArray());
            Assert.AreEqual(CategoryKind.Variables, t.Categories.Single(c => c.Name == "Variables").Kind);
            Assert.AreEqual(CategoryKind.Procedures, t.Categories.Single(c => c.Name == "Functions").Kind);
            Assert.AreEqual(Colour.FromHue(210), t.Categories[0].ColourHex);
            foreach (var c in t.Categories.Where(c => c.Kind == CategoryKind.Static)) Assert.Greater(c.Blocks.Count, 3, c.Name);
        }

        [Test]
        public void EveryBlockTypeIsReachableFromTheToolbox()
        {
            var ws = TestSupport.NewWorkspace();
            var inToolbox = new HashSet<string>(Toolbox.Categories.SelectMany(c => c.Blocks).Select(b => (string)b["type"]));
            // these appear only through the dynamic Variables / Functions categories
            foreach (var t in new[] { "variables_get", "variables_set", "math_change", "procedures_callnoreturn", "procedures_callreturn" }) inToolbox.Add(t);
            var missing = ws.Registry.All.Select(d => d.Type).Where(t => !inToolbox.Contains(t)).ToList();
            Assert.IsEmpty(missing, "block types the user could never get: " + string.Join(", ", missing));
        }

        public static IEnumerable<TestCaseData> AllStaticSpecs()
        {
            foreach (var c in Toolbox.Categories.Where(c => c.Kind == CategoryKind.Static || c.Kind == CategoryKind.Procedures))
                for (int i = 0; i < c.Blocks.Count; i++)
                    yield return new TestCaseData(c.Name, i).SetName($"Spec_{c.Name}_{i}_{c.Blocks[i]["type"]}");
        }

        [TestCaseSource(nameof(AllStaticSpecs))]
        public void EveryToolboxBlockSpawnsLaysOutAndRuns(string category, int index)
        {
            var spec = Toolbox.Categories.Single(c => c.Name == category).Blocks[index];
            var ws = TestSupport.NewWorkspace();
            var block = WorkspaceEditing.Spawn(ws, spec);
            Assert.AreEqual((string)spec["type"], block.Type);
            ws.TopBlocks.Add(block);

            // it lays out and its outline can be drawn
            var layout = new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace();
            foreach (var box in layout.Boxes)
            {
                var tris = Geometry.Triangulate(box.Outline);
                Assert.AreEqual(Math.Abs(Geometry.SignedArea(box.Outline)), Geometry.TrianglesArea(box.Outline, tris), 0.5f, box.Block.Type);
            }

            // and it can run (a value block is printed, a statement block runs on its own)
            if (ProcedureInfo.IsDefinition(block) || block.Type == "procedures_ifreturn") return;
            if (block.Output != null)
            {
                ws.TopBlocks.Remove(block);
                var print = ws.CreateBlock("text_print");
                print.GetInput("TEXT").Connection.Connect(block.Output);
                ws.TopBlocks.Add(print);
            }
            Run(ws, out var error);
            Assert.IsNull(error, block.Type + " from the toolbox must be executable");
        }

        // ---------------------------------------------------------------- flyout
        [Test]
        public void FlyoutStacksTheCategoryBlocksWithoutOverlap()
        {
            var main = TestSupport.NewWorkspace();
            var cat = Toolbox.Categories.Single(c => c.Name == "Logic");
            var fly = Flyout.Build(cat, main, new FixedWidth());
            Assert.AreEqual(cat.Blocks.Count, fly.Items.Count);
            Assert.AreEqual(cat.Blocks.Count, fly.Workspace.TopBlocks.Count);
            Assert.AreSame(main.Registry, fly.Workspace.Registry);

            var layouter = new BlockLayouter(fly.Workspace, new FixedWidth());
            var spans = fly.Workspace.TopBlocks.Select(t => { var r = layouter.LayoutStack(t, (float)t.X, (float)t.Y); return (r.Top, r.Bottom); }).ToList();
            for (int i = 1; i < spans.Count; i++) Assert.Greater(spans[i].Top, spans[i - 1].Bottom, "block " + i + " starts below the previous one");
            foreach (var kv in fly.Items) Assert.AreSame(kv.Value, fly.SpecOf(kv.Key));
        }

        [Test]
        public void VariablesCategoryOffersItemWhenThereAreNoVariablesYet()
        {
            var main = TestSupport.NewWorkspace();
            var fly = Flyout.Build(Toolbox.Categories.Single(c => c.Name == "Variables"), main, new FixedWidth());
            Assert.AreEqual(new[] { "variables_set", "math_change", "variables_get" }, fly.Items.Select(i => i.Key.Type).ToArray());
            var get = fly.Items.Last().Key;
            Assert.AreEqual("item", fly.Workspace.GetVariableById(get.GetFieldValue("VAR")).Name);
        }

        [Test]
        public void VariablesCategoryListsEveryExistingVariable()
        {
            var main = FromXml("<variables><variable id=\"va\">a</variable><variable id=\"vb\">b</variable></variables>");
            var fly = Flyout.Build(Toolbox.Categories.Single(c => c.Name == "Variables"), main, new FixedWidth());
            Assert.AreEqual(new[] { "variables_set", "math_change", "variables_get", "variables_get" }, fly.Items.Select(i => i.Key.Type).ToArray());
            Assert.AreEqual("a", fly.Workspace.GetVariableById(fly.Items[0].Key.GetFieldValue("VAR")).Name, "set/change use the first variable");
            Assert.AreEqual(new[] { "a", "b" }, fly.Items.Skip(2).Select(i => fly.Workspace.GetVariableById(i.Key.GetFieldValue("VAR")).Name).ToArray());

            // a block spawned from the flyout refers to the MAIN workspace's variable, not a copy
            var spawned = WorkspaceEditing.Spawn(main, fly.Items[3].Value);
            Assert.AreEqual("vb", spawned.GetFieldValue("VAR"));
            Assert.AreEqual(2, main.Variables.Count, "no duplicate variable was created");
        }

        [Test]
        public void FunctionsCategoryOffersACallForEveryDefinedProcedure()
        {
            var main = FromXml(
                "<variables><variable id=\"vx\">x</variable></variables>" +
                "<block type=\"procedures_defreturn\"><mutation><arg name=\"x\" varid=\"vx\"></arg></mutation><field name=\"NAME\">twice</field></block>" +
                "<block type=\"procedures_defnoreturn\"><field name=\"NAME\">hello</field></block>");
            var fly = Flyout.Build(Toolbox.Categories.Single(c => c.Name == "Functions"), main, new FixedWidth());
            var types = fly.Items.Select(i => i.Key.Type).ToList();
            Assert.AreEqual(new[] { "procedures_defnoreturn", "procedures_defreturn", "procedures_ifreturn", "procedures_callreturn", "procedures_callnoreturn" }, types.ToArray());
            var call = fly.Items.Single(i => i.Key.Type == "procedures_callreturn").Key;
            Assert.AreEqual("twice", ProcedureInfo.Name(call));
            Assert.AreEqual("x", call.GetInput("ARG0").Fields.Single(f => f.IsLabel).Value, "the call block shows its parameter name");
        }

        [Test]
        public void ACallSpawnedFromTheFlyoutReallyCallsTheProcedure()
        {
            var main = FromXml(
                "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"0\"><field name=\"NAME\">hello</field><statement name=\"STACK\">" +
                "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">hi there</field></block></value></block></statement></block>");
            var fly = Flyout.Build(Toolbox.Categories.Single(c => c.Name == "Functions"), main, new FixedWidth());
            var callSpec = fly.Items.Single(i => i.Key.Type == "procedures_callnoreturn").Value;
            var call = WorkspaceEditing.Spawn(main, callSpec);
            main.TopBlocks.Add(call);
            call.Y = 500;
            Assert.AreEqual("hi there", Run(main, out var error));
            Assert.IsNull(error);
        }

        // ---------------------------------------------------------------- spawning
        [Test]
        public void SpawnedBlocksAreIndependentCopies()
        {
            var ws = TestSupport.NewWorkspace();
            var spec = Toolbox.Categories.Single(c => c.Name == "Math").Blocks.First(b => (string)b["type"] == "math_arithmetic");
            var a = WorkspaceEditing.Spawn(ws, spec); var b = WorkspaceEditing.Spawn(ws, spec);
            Assert.AreNotSame(a, b);
            Assert.AreNotEqual(a.Id, b.Id);
            Assert.AreNotSame(a.GetInput("A").Connection.Shadow, b.GetInput("A").Connection.Shadow);
            a.GetInput("A").Connection.Shadow.SetFieldValue("NUM", "42");
            Assert.AreEqual("1", b.GetInput("A").Connection.Shadow.GetFieldValue("NUM"), "editing one copy must not change the next one");
        }

        [Test]
        public void ProcedureDefinitionsGetUniqueNames()
        {
            var ws = TestSupport.NewWorkspace();
            var spec = Toolbox.Categories.Single(c => c.Name == "Functions").Blocks[0];
            var names = new List<string>();
            for (int i = 0; i < 3; i++) { var d = WorkspaceEditing.Spawn(ws, spec); ws.TopBlocks.Add(d); names.Add(d.GetFieldValue("NAME")); }
            Assert.AreEqual(new[] { "do something", "do something2", "do something3" }, names.ToArray());
        }

        [Test]
        public void ForLoopFromTheToolboxGetsItsOwnCounterVariable()
        {
            var ws = TestSupport.NewWorkspace();
            var spec = Toolbox.Categories.Single(c => c.Name == "Loops").Blocks.First(b => (string)b["type"] == "controls_for");
            var loop = WorkspaceEditing.Spawn(ws, spec);
            Assert.AreEqual("i", ws.GetVariableById(loop.GetFieldValue("VAR")).Name);
            ws.TopBlocks.Add(loop);
            var print = ws.CreateBlock("text_print");
            var get = ws.CreateBlock("variables_get"); get.SetFieldValue("VAR", loop.GetFieldValue("VAR"));
            print.GetInput("TEXT").Connection.Connect(get.Output);
            loop.GetInput("DO").Connection.Connect(print.Previous);
            Assert.AreEqual("1,2,3,4,5,6,7,8,9,10", Run(ws, out _), "the shadow defaults 1..10 by 1 drive the loop");
        }

        // ---------------------------------------------------------------- drag from the toolbox, discard, delete
        static Block SpawnAt(Workspace ws, string type, double x, double y)
        {
            var spec = Toolbox.Categories.SelectMany(c => c.Blocks).First(b => (string)b["type"] == type);
            var b = WorkspaceEditing.Spawn(ws, spec);
            b.X = x; b.Y = y;
            ws.TopBlocks.Add(b);
            return b;
        }

        [Test]
        public void ABlockSpawnedAndDroppedOnAnInputBecomesPartOfTheProgram()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"10\" y=\"10\"></block>");
            var layouter = new BlockLayouter(ws, new FixedWidth());
            var dragger = new BlockDragger(ws, layouter);
            var print = ws.TopBlocks[0];
            var num = SpawnAt(ws, "math_number", 300, 300);
            num.SetFieldValue("NUM", "7");

            Assert.IsTrue(dragger.Begin(num, isNew: true));
            var layout = layouter.LayoutWorkspace();
            var target = layout.ByBlock[print].Connections[print.GetInput("TEXT").Connection];
            var mine = layouter.LayoutStack(num, (float)num.X, (float)num.Y).ByBlock[num].Connections[num.Output];
            dragger.MoveTo(num.X + target.X - mine.X + 4, num.Y + target.Y - mine.Y + 4);
            Assert.AreEqual(DropOutcome.Connected, dragger.End());
            Assert.AreEqual("7", Run(ws, out _));
        }

        [Test]
        public void CancellingTheDragOfANewBlockThrowsItAway()
        {
            var ws = TestSupport.NewWorkspace();
            var dragger = new BlockDragger(ws, new BlockLayouter(ws, new FixedWidth()));
            var fresh = SpawnAt(ws, "controls_repeat_ext", 20, 20);
            Assert.IsTrue(dragger.Begin(fresh, isNew: true));
            dragger.MoveTo(100, 100);
            dragger.Cancel();
            Assert.AreEqual(0, ws.TopBlocks.Count);
            Assert.AreEqual(0, ws.AllBlocks().Count());
        }

        [Test]
        public void DeleteRemovesTheWholeDraggedStackAndItsChildren()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"10\" y=\"10\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">a</field></block></value>" +
                             "<next><block type=\"text_print\"><next><block type=\"text_print\"/></next></block></next></block>" +
                             "<block type=\"text_print\" x=\"400\" y=\"10\"></block>");
            var dragger = new BlockDragger(ws, new BlockLayouter(ws, new FixedWidth()));
            var second = ws.TopBlocks[0].NextBlock;
            Assert.IsTrue(dragger.Begin(second));           // takes the 2nd and 3rd block with it
            dragger.MoveTo(600, 600);
            dragger.Delete();
            Assert.IsFalse(dragger.IsDragging);
            Assert.AreEqual(3, ws.AllBlocks().Count(), "first print + its text + the separate print remain");
            Assert.IsNull(ws.TopBlocks[0].NextBlock);
        }

        [Test]
        public void DeletingAProcedureDefinitionAlsoRemovesItsCalls()
        {
            var ws = FromXml(
                "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"300\"><field name=\"NAME\">foo</field></block>" +
                "<block type=\"procedures_defreturn\" x=\"0\" y=\"400\"><field name=\"NAME\">bar</field><value name=\"RETURN\"><block type=\"math_number\"><field name=\"NUM\">5</field></block></value></block>" +
                "<block type=\"text_print\" x=\"10\" y=\"10\"><value name=\"TEXT\"><block type=\"procedures_callreturn\"><mutation name=\"bar\"></mutation></block></value>" +
                "<next><block type=\"procedures_callnoreturn\"><mutation name=\"foo\"></mutation><next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">end</field></block></value></block></next></block></next></block>");
            var dragger = new BlockDragger(ws, new BlockLayouter(ws, new FixedWidth()));
            foreach (var name in new[] { "bar", "foo" })
            {
                var def = ws.TopBlocks.First(b => ProcedureInfo.IsDefinition(b) && b.GetFieldValue("NAME") == name);
                Assert.IsTrue(dragger.Begin(def));
                dragger.Delete();
            }
            Assert.IsFalse(ws.AllBlocks().Any(b => b.Type.StartsWith("procedures_")), "definitions and every call are gone");
            Assert.IsNull(ws.TopBlocks[0].GetInputTargetBlock("TEXT"), "the value call left its input empty");
            Assert.AreEqual("end", ws.TopBlocks[0].NextBlock.GetInputTargetBlock("TEXT").GetFieldValue("TEXT"), "the statement call was removed and the stack healed");
            Assert.AreEqual(",end", Run(ws, out var error));
            Assert.IsNull(error);
        }

        [Test]
        public void DeleteWithHealKeepsTheFollowingBlocks()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"10\" y=\"10\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">a</field></block></value><next>" +
                             "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">b</field></block></value><next>" +
                             "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">c</field></block></value></block></next></block></next></block>");
            var middle = ws.TopBlocks[0].NextBlock;
            WorkspaceEditing.Delete(ws, new BlockLayouter(ws, new FixedWidth()), middle, heal: true);
            Assert.AreEqual("a,c", Run(ws, out _));
            Assert.AreEqual(1, ws.TopBlocks.Count);
        }

        [Test]
        public void DeletingTheFirstBlockWithHealLeavesTheRestWhereItWasDrawn()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"50\" y=\"60\"><next><block type=\"text_print\"/></next></block>");
            var layouter = new BlockLayouter(ws, new FixedWidth());
            var first = ws.TopBlocks[0];
            var follower = first.NextBlock;
            var before = layouter.LayoutWorkspace().ByBlock[follower];

            WorkspaceEditing.Delete(ws, layouter, first, heal: true);

            Assert.AreEqual(1, ws.TopBlocks.Count);
            Assert.AreSame(follower, ws.TopBlocks[0]);
            Assert.AreEqual(1, ws.AllBlocks().Count());
            Assert.AreEqual(before.X, follower.X, 1e-3, "like Blockly, the survivor does not jump to the deleted block's spot");
            Assert.AreEqual(before.Y, follower.Y, 1e-3);
        }
    }
}
