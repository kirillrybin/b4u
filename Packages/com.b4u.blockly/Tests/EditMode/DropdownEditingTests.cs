using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class DropdownEditingTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static Workspace FromXml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        static BlockLayouter Layouter(Workspace ws) => new BlockLayouter(ws, new FixedWidth());

        static bool Set(Workspace ws, Block b, string field, string value) => FieldEditing.SetValue(ws, b, b.GetField(field), value, Layouter(ws));

        static string Run(Workspace ws, out string error)
        {
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion(100000);
            error = it.Error;
            return string.Join(",", lines);
        }

        // ---------------------------------------------------------------- shapes, checked against real Blockly
        sealed class Shape
        {
            public string Type; public List<string[]> Steps = new List<string[]>();
            public string[] Inputs; public bool Output, Previous, Next;
            public override string ToString() => Type + " " + string.Join(" then ", Steps.Select(s => s[0] + "=" + s[1]));
        }

        static List<Shape> Shapes()
        {
            var json = (List<object>)Json.Json.Parse(File.ReadAllText(Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "block-shapes.cases.json")));
            return json.Select(o =>
            {
                var d = (Dictionary<string, object>)o;
                var s = new Shape { Type = (string)d["type"], Output = (bool)d["output"], Previous = (bool)d["previous"], Next = (bool)d["next"] };
                s.Inputs = ((List<object>)d["inputs"]).Cast<string>().ToArray();
                foreach (var step in (List<object>)d["steps"]) { var p = (List<object>)step; s.Steps.Add(new[] { (string)p[0], (string)p[1] }); }
                return s;
            }).ToList();
        }

        public static IEnumerable<TestCaseData> ShapeCases()
        {
            int i = 0;
            foreach (var s in Shapes()) yield return new TestCaseData(s).SetName($"Shape_{i++:00}_{s.ToString().Replace(' ', '_')}");
        }

        public sealed class ShapeHolder { }

        [TestCaseSource(nameof(ShapeCases))]
        public void ChangingDropdownsGivesTheSameShapeAsRealBlockly(object expected)
        {
            var s = (Shape)expected;
            var ws = TestSupport.NewWorkspace();
            var block = ws.CreateBlock(s.Type);
            ws.TopBlocks.Add(block);
            foreach (var step in s.Steps) Set(ws, block, step[0], step[1]);

            var inputs = block.Inputs.Where(i => i.Connection != null && i.Name != null).Select(i => i.Name).ToArray();
            CollectionAssert.AreEquivalent(s.Inputs, inputs, "connectable inputs after " + s);
            Assert.AreEqual(s.Output, block.Output != null, "output after " + s);
            Assert.AreEqual(s.Previous, block.Previous != null, "previous after " + s);
            Assert.AreEqual(s.Next, block.Next != null, "next after " + s);
        }

        // ---------------------------------------------------------------- menu contents
        [Test]
        public void DropdownItemsShowCleanLabelsAndMarkTheCurrentValue()
        {
            var ws = TestSupport.NewWorkspace();
            var cmp = ws.CreateBlock("logic_compare");
            var items = FieldEditing.GetDropdownItems(ws, cmp.GetField("OP"));
            Assert.AreEqual(new[] { "=", "≠", "<", "≤", ">", "≥" }, items.Select(i => i.Label).ToArray(), "no invisible direction marks in the labels");
            Assert.AreEqual(new[] { "EQ", "NEQ", "LT", "LTE", "GT", "GTE" }, items.Select(i => i.Value).ToArray());
            Assert.AreEqual(new[] { true, false, false, false, false, false }, items.Select(i => i.Selected).ToArray());
            Assert.IsTrue(items.All(i => i.Action == DropdownAction.Select));
        }

        [Test]
        public void OnlyListedValuesAreAccepted()
        {
            var ws = TestSupport.NewWorkspace();
            var cmp = ws.CreateBlock("logic_compare"); ws.TopBlocks.Add(cmp);
            Assert.IsFalse(Set(ws, cmp, "OP", "BOGUS"));
            Assert.AreEqual("EQ", cmp.GetFieldValue("OP"));
            Assert.IsTrue(Set(ws, cmp, "OP", "GTE"));
            Assert.IsFalse(Set(ws, cmp, "OP", "GTE"), "already that value");
        }

        [Test]
        public void ChoosingAnOperatorChangesWhatTheProgramComputes()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"math_arithmetic\"><field name=\"OP\">ADD</field>" +
                             "<value name=\"A\"><block type=\"math_number\"><field name=\"NUM\">6</field></block></value>" +
                             "<value name=\"B\"><block type=\"math_number\"><field name=\"NUM\">3</field></block></value></block></value></block>");
            var arith = ws.TopBlocks[0].GetInputTargetBlock("TEXT");
            var results = new List<string>();
            foreach (var op in new[] { "ADD", "MINUS", "MULTIPLY", "DIVIDE", "POWER" }) { Set(ws, arith, "OP", op); results.Add(Run(ws, out _)); }
            Assert.AreEqual(new[] { "9", "3", "18", "2", "216" }, results.ToArray());
        }

        // ---------------------------------------------------------------- variables in menus
        const string Vars = "<variables><variable id=\"vb\">b</variable><variable id=\"va\">A</variable><variable id=\"vc\">c</variable></variables>";

        [Test]
        public void VariableMenuListsVariablesAlphabeticallyThenRenameAndDelete()
        {
            var ws = FromXml(Vars + "<block type=\"variables_get\" x=\"0\" y=\"0\"><field name=\"VAR\" id=\"vb\">b</field></block>");
            var items = FieldEditing.GetDropdownItems(ws, ws.TopBlocks[0].GetField("VAR"));
            Assert.AreEqual(new[] { "A", "b", "c", "Rename variable...", "Delete the \"b\" variable" }, items.Select(i => i.Label).ToArray());
            Assert.AreEqual(new[] { false, true, false, false, false }, items.Select(i => i.Selected).ToArray());
            Assert.AreEqual(DropdownAction.RenameVariable, items[3].Action);
            Assert.AreEqual(DropdownAction.DeleteVariable, items[4].Action);
            Assert.AreEqual("vb", items[3].Value); Assert.AreEqual("vb", items[4].Value);
        }

        [Test]
        public void PickingAnotherVariableMakesTheProgramUseIt()
        {
            var ws = FromXml(Vars +
                "<block type=\"variables_set\" x=\"0\" y=\"0\"><field name=\"VAR\" id=\"va\">A</field><value name=\"VALUE\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value><next>" +
                "<block type=\"variables_set\"><field name=\"VAR\" id=\"vb\">b</field><value name=\"VALUE\"><block type=\"math_number\"><field name=\"NUM\">2</field></block></value><next>" +
                "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"variables_get\"><field name=\"VAR\" id=\"va\">A</field></block></value></block></next></block></next></block>");
            var get = ws.AllBlocks().First(b => b.Type == "variables_get");
            Assert.AreEqual("1", Run(ws, out _));
            Assert.IsTrue(Set(ws, get, "VAR", "vb"));
            Assert.AreEqual("2", Run(ws, out _));
            Assert.IsFalse(Set(ws, get, "VAR", "no-such-id"), "unknown variable rejected");
            Assert.AreEqual("vb", get.GetFieldValue("VAR"));
        }

        // ---------------------------------------------------------------- side effects of a change
        static Block Find(Workspace ws, string type) => ws.AllBlocks().First(b => b.Type == type);

        [Test]
        public void ChoosingFirstRemovesTheIndexInputAndThrowsAwayWhatWasInIt()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"lists_getIndex\"><mutation statement=\"false\" at=\"true\"></mutation>" +
                             "<field name=\"MODE\">GET</field><field name=\"WHERE\">FROM_START</field>" +
                             "<value name=\"VALUE\"><block type=\"lists_create_with\"><mutation items=\"1\"></mutation><value name=\"ADD0\"><block type=\"text\"><field name=\"TEXT\">x</field></block></value></block></value>" +
                             "<value name=\"AT\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value></block></value></block>");
            var get = Find(ws, "lists_getIndex");
            int before = ws.AllBlocks().Count();
            Assert.IsNotNull(get.GetInput("AT"));
            Assert.AreEqual("x", Run(ws, out _));

            Assert.IsTrue(Set(ws, get, "WHERE", "FIRST"));
            Assert.IsNull(get.GetInput("AT"));
            Assert.AreEqual(before - 1, ws.AllBlocks().Count(), "the number block that was plugged into AT is gone");
            Assert.AreEqual("x", Run(ws, out _));

            Assert.IsTrue(Set(ws, get, "WHERE", "FROM_END"));
            Assert.IsNotNull(get.GetInput("AT"), "an empty index input comes back");
            Assert.IsNull(get.GetInputTargetBlock("AT"));
        }

        [Test]
        public void DivisibleByAddsADivisorInputAndLeavingItDropsIt()
        {
            var ws = TestSupport.NewWorkspace();
            var prop = ws.CreateBlock("math_number_property"); ws.TopBlocks.Add(prop);
            Assert.IsNull(prop.GetInput("DIVISOR"));
            Set(ws, prop, "PROPERTY", "DIVISIBLE_BY");
            Assert.IsNotNull(prop.GetInput("DIVISOR"));
            var five = ws.CreateBlock("math_number");
            prop.GetInput("DIVISOR").Connection.Connect(five.Output);
            Set(ws, prop, "PROPERTY", "PRIME");
            Assert.IsNull(prop.GetInput("DIVISOR"));
            Assert.AreEqual(0, ws.AllBlocks().Count(b => b == five), "the divisor block went with its input");
        }

        [Test]
        public void RemoveModeTurnsAValueBlockIntoAStatementThatLeavesItsSlot()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"10\" y=\"10\"><value name=\"TEXT\"><block type=\"lists_getIndex\"><mutation statement=\"false\" at=\"false\"></mutation>" +
                             "<field name=\"MODE\">GET</field><field name=\"WHERE\">FIRST</field></block></value></block>");
            var print = ws.TopBlocks[0]; var get = Find(ws, "lists_getIndex");
            var before = Layouter(ws).LayoutWorkspace().ByBlock[get];

            Assert.IsTrue(Set(ws, get, "MODE", "REMOVE"));
            Assert.IsNull(get.Output); Assert.IsNotNull(get.Previous); Assert.IsNotNull(get.Next);
            Assert.IsNull(print.GetInputTargetBlock("TEXT"), "it left the print block's input");
            Assert.Contains(get, ws.TopBlocks.ToList(), "and became a top-level block");
            Assert.Greater(get.X, before.X - 1e-3); Assert.Greater(get.Y, before.Y - 1e-3);

            Assert.IsTrue(Set(ws, get, "MODE", "GET_REMOVE"));
            Assert.IsNotNull(get.Output); Assert.IsNull(get.Previous); Assert.IsNull(get.Next);
            BlockDraggerTests.AssertModelIsSound(ws, ws.AllBlocks().Count(), "after mode round trip");
        }

        [Test]
        public void SwitchingAStatementIndexBlockToAValueHealsTheStackAroundIt()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">a</field></block></value><next>" +
                             "<block type=\"lists_getIndex\"><mutation statement=\"true\" at=\"false\"></mutation><field name=\"MODE\">REMOVE</field><field name=\"WHERE\">FIRST</field><next>" +
                             "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">c</field></block></value></block></next></block></next></block>");
            var get = Find(ws, "lists_getIndex");
            Set(ws, get, "MODE", "GET");
            Assert.AreEqual(2, ws.TopBlocks.Count);
            Assert.AreEqual("a,c", Run(ws, out _), "the two prints are connected again");
            Assert.IsNotNull(get.Output);
        }

        [Test]
        public void ChangingSplitToJoinMovesOutWhatNoLongerFits()
        {
            var ws = FromXml("<block type=\"lists_getIndex\" x=\"0\" y=\"0\"><mutation statement=\"false\" at=\"false\"></mutation><field name=\"MODE\">GET</field><field name=\"WHERE\">FIRST</field>" +
                             "<value name=\"VALUE\"><block type=\"lists_split\"><mutation mode=\"SPLIT\"></mutation><field name=\"MODE\">SPLIT</field>" +
                             "<value name=\"INPUT\"><block type=\"text\"><field name=\"TEXT\">a,b</field></block></value>" +
                             "<value name=\"DELIM\"><block type=\"text\"><field name=\"TEXT\">,</field></block></value></block></value></block>");
            var split = Find(ws, "lists_split");
            int before = ws.AllBlocks().Count();
            Set(ws, split, "MODE", "JOIN");
            Assert.AreEqual(before, ws.AllBlocks().Count(), "nothing was lost");
            Assert.AreEqual(3, ws.TopBlocks.Count, "the index block, the bumped split block and the bumped text block are now three separate stacks");
            BlockDraggerTests.AssertModelIsSound(ws, before, "after switching split to join");
            // JOIN produces text and wants a list input: the text it was holding and its own place in the Array-only slot both had to go
            Assert.IsNull(split.GetInputTargetBlock("INPUT"), "a text block no longer fits a list input");
            Assert.IsNull(Find(ws, "lists_getIndex").GetInputTargetBlock("VALUE"), "and a text result no longer fits a list slot");
        }

        [Test]
        public void EditedShapesSurviveSaveAndLoad()
        {
            var ws = TestSupport.NewWorkspace();
            var get = ws.CreateBlock("lists_getIndex"); ws.TopBlocks.Add(get);
            Set(ws, get, "MODE", "REMOVE"); Set(ws, get, "WHERE", "LAST");
            var prop = ws.CreateBlock("math_number_property"); ws.TopBlocks.Add(prop); prop.Y = 100;
            Set(ws, prop, "PROPERTY", "DIVISIBLE_BY");
            string saved = WorkspaceJson.Save(ws);
            var again = TestSupport.NewWorkspace();
            WorkspaceJson.Load(again, saved);
            Assert.AreEqual(saved, WorkspaceJson.Save(again));
            var g = again.AllBlocks().First(b => b.Type == "lists_getIndex");
            Assert.IsNull(g.Output); Assert.IsNull(g.GetInput("AT"));
            Assert.IsNotNull(again.AllBlocks().First(b => b.Type == "math_number_property").GetInput("DIVISOR"));
        }

        // ---------------------------------------------------------------- variables: create / rename / delete
        [TestCase("count", true)] [TestCase("  my   var  ", true)] [TestCase("", false)] [TestCase("   ", false)]
        [TestCase("A", false)] [TestCase("a", false)] [TestCase("B", false)]
        public void VariableNamesAreTidiedAndMustBeUniqueIgnoringCase(string typed, bool ok)
        {
            var ws = FromXml(Vars);
            var v = VariableEditing.Create(ws, typed, out var error);
            Assert.AreEqual(ok, v != null, error);
            Assert.AreEqual(!ok, error != null);
            if (typed.Trim() == "my   var") Assert.AreEqual("my var", v.Name);
        }

        [Test]
        public void RenamingFollowsEverywhereAndAllowsAChangeOfCase()
        {
            var ws = FromXml(Vars + "<block type=\"variables_get\" x=\"0\" y=\"0\"><field name=\"VAR\" id=\"vb\">b</field></block>");
            Assert.IsTrue(VariableEditing.Rename(ws, "vb", "total", out _));
            Assert.AreEqual("total", ws.GetVariableById(ws.TopBlocks[0].GetFieldValue("VAR")).Name);
            Assert.IsTrue(VariableEditing.Rename(ws, "vb", "TOTAL", out _), "only the case changes: not a clash with itself");
            Assert.IsFalse(VariableEditing.Rename(ws, "vb", "a", out var error), "a is taken (as A)");
            StringAssert.Contains("already exists", error);
            Assert.AreEqual("TOTAL", ws.GetVariableById("vb").Name, "unchanged after a refused rename");
            Assert.IsFalse(VariableEditing.Rename(ws, "vb", "  ", out error));
        }

        [Test]
        public void DeletingAVariableRemovesItsBlocksClosesTheStackAndKeepsTheRestWorking()
        {
            var ws = FromXml(Vars +
                "<block type=\"variables_set\" x=\"0\" y=\"0\"><field name=\"VAR\" id=\"va\">A</field><value name=\"VALUE\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value><next>" +
                "<block type=\"variables_set\"><field name=\"VAR\" id=\"vb\">b</field><value name=\"VALUE\"><block type=\"math_number\"><field name=\"NUM\">2</field></block></value><next>" +
                "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"variables_get\"><field name=\"VAR\" id=\"va\">A</field></block></value></block></next></block></next></block>");
            Assert.AreEqual(2, VariableEditing.CountUses(ws, "va"), "the set and the get");
            Assert.AreEqual(1, VariableEditing.CountUses(ws, "vb"));

            VariableEditing.Delete(ws, Layouter(ws), "vb");
            Assert.IsNull(ws.GetVariableById("vb"));
            Assert.AreEqual(0, VariableEditing.CountUses(ws, "vb"));
            Assert.AreEqual("1", Run(ws, out var error), "the stack closed up around the removed set block");
            Assert.IsNull(error);

            VariableEditing.Delete(ws, Layouter(ws), "va");
            Assert.AreEqual(0, ws.AllBlocks().Count(b => b.Type == "variables_get" || b.Type == "variables_set"));
            Assert.AreEqual(1, ws.AllBlocks().Count(b => b.Type == "text_print"), "the print block stays, with an empty input");
            Assert.AreEqual("c", ws.Variables.Single().Name);
        }

        // ---------------------------------------------------------------- random dropdown / variable edits
        static string Dir => Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs");

        public static string[] Programs => new[] { "lists", "lists_more", "text_more", "math", "logic", "procedures", "loops" };

        [TestCaseSource(nameof(Programs))]
        public void RandomDropdownAndVariableEditsKeepTheWorkspaceConsistent(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            var layouter = Layouter(ws);
            var rng = new Random(4242 + name.Length * 17);
            int changes = 0;

            for (int step = 0; step < 250; step++)
            {
                string ctx = name + " step " + step;
                int before = ws.AllBlocks().Count();
                int op = rng.Next(10);
                if (op < 8)
                {
                    var withMenus = ws.AllBlocks().SelectMany(b => b.AllFields().Where(FieldEditing.IsDropdown).Select(f => (b, f))).ToList();
                    if (withMenus.Count == 0) continue;
                    var (block, field) = withMenus[rng.Next(withMenus.Count)];
                    var items = FieldEditing.GetDropdownItems(ws, field).Where(i => i.Action == DropdownAction.Select).ToList();
                    if (items.Count == 0) continue;
                    var pick = items[rng.Next(items.Count)];
                    if (FieldEditing.SetValue(ws, block, field, pick.Value, layouter)) changes++;
                    Assert.LessOrEqual(ws.AllBlocks().Count(), before, ctx + ": a dropdown change can only remove blocks, never invent them");
                }
                else if (op == 8 && ws.Variables.Count > 0)
                {
                    var v = ws.Variables[rng.Next(ws.Variables.Count)];
                    VariableEditing.Rename(ws, v.Id, "v" + step, out _);
                }
                else if (ws.Variables.Count > 1)
                {
                    var v = ws.Variables[rng.Next(ws.Variables.Count)];
                    int uses = VariableEditing.CountUses(ws, v.Id);
                    VariableEditing.Delete(ws, layouter, v.Id);
                    Assert.AreEqual(0, VariableEditing.CountUses(ws, v.Id), ctx);
                    Assert.LessOrEqual(ws.AllBlocks().Count(), before - uses, ctx + ": every use is gone");
                }
                BlockDraggerTests.AssertModelIsSound(ws, ws.AllBlocks().Count(), ctx);
                var layout = layouter.LayoutWorkspace();
                Assert.AreEqual(ws.AllBlocks().Count(), layout.Boxes.Count(b => !b.IsShadow), ctx + ": still lays out");
                if (step % 15 == 0) Assert.DoesNotThrow(() => { var it = new Interpreter(ws); it.Start(); it.RunToCompletion(30000); }, ctx);
            }

            string saved = WorkspaceJson.Save(ws);
            var again = TestSupport.NewWorkspace();
            WorkspaceJson.Load(again, saved);
            Assert.AreEqual(saved, WorkspaceJson.Save(again), "the edited workspace round-trips");
            Assert.Greater(changes, 20, name + ": the fuzzer should actually have changed dropdowns");
        }
    }
}
