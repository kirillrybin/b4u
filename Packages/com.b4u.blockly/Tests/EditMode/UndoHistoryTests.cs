using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class UndoHistoryTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static string Dir => Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs");

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

        /// <summary>Runs one user action as the UI does: snapshot, act, commit.</summary>
        static bool Act(UndoHistory h, Action action)
        {
            string before = h.Snapshot();
            action();
            return h.Commit(before);
        }

        // ---------------------------------------------------------------- ids
        [Test]
        public void NewBlocksAfterALoadNeverReuseALoadedId()
        {
            var ws = TestSupport.NewWorkspace();
            ws.TopBlocks.Add(ws.CreateBlock("text_print"));
            ws.TopBlocks.Add(ws.CreateBlock("text_print"));
            var again = TestSupport.NewWorkspace();
            WorkspaceJson.Load(again, WorkspaceJson.Save(ws));
            again.TopBlocks.Add(again.CreateBlock("text_print"));
            var ids = again.AllBlocks().Select(b => b.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "ids: " + string.Join(",", ids));
        }

        [Test]
        public void VariableIdsStayUniqueEvenAfterDeletes()
        {
            var ws = TestSupport.NewWorkspace();
            var layouter = new BlockLayouter(ws, new FixedWidth());
            var made = new List<Variable>();
            for (int i = 0; i < 6; i++) made.Add(VariableEditing.Create(ws, "n" + i, out _));
            VariableEditing.Delete(ws, layouter, made[2].Id);          // 5 left, but ids go up to v6
            var fresh = VariableEditing.Create(ws, "fresh", out _);
            var ids = ws.Variables.Select(v => v.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "ids: " + string.Join(",", ids));
            Assert.AreEqual("fresh", ws.GetVariableById(fresh.Id).Name, "the new variable can be found by its own id");
        }

        // ---------------------------------------------------------------- basics
        [Test]
        public void ACommittedChangeCanBeUndoneAndRedone()
        {
            var ws = FromXml("<block type=\"text\" x=\"0\" y=\"0\"><field name=\"TEXT\">old</field></block>");
            var h = new UndoHistory(ws);
            var text = ws.TopBlocks[0];
            string original = h.Snapshot();

            Assert.IsTrue(Act(h, () => FieldEditing.SetValue(ws, text, text.GetField("TEXT"), "new")));
            string edited = h.Snapshot();
            Assert.IsTrue(h.CanUndo); Assert.IsFalse(h.CanRedo);

            Assert.IsTrue(h.Undo());
            Assert.AreEqual(original, h.Snapshot());
            Assert.AreEqual("old", ws.TopBlocks[0].GetFieldValue("TEXT"));
            Assert.IsFalse(h.CanUndo); Assert.IsTrue(h.CanRedo);

            Assert.IsTrue(h.Redo());
            Assert.AreEqual(edited, h.Snapshot());
            Assert.AreEqual("new", ws.TopBlocks[0].GetFieldValue("TEXT"));
        }

        [Test]
        public void AnActionThatChangedNothingLeavesNoStep()
        {
            var ws = FromXml("<block type=\"text\" x=\"0\" y=\"0\"><field name=\"TEXT\">same</field></block>");
            var h = new UndoHistory(ws);
            var text = ws.TopBlocks[0];
            Assert.IsFalse(Act(h, () => FieldEditing.SetValue(ws, text, text.GetField("TEXT"), "same")));
            Assert.IsFalse(Act(h, () => { }));
            Assert.IsFalse(h.CanUndo);
            Assert.IsFalse(h.Undo(), "nothing to undo");
            Assert.IsFalse(h.Redo(), "nothing to redo");
        }

        [Test]
        public void ANewChangeAfterUndoForgetsTheRedoBranch()
        {
            var ws = FromXml("<block type=\"text\" x=\"0\" y=\"0\"><field name=\"TEXT\">a</field></block>");
            var h = new UndoHistory(ws);
            Act(h, () => FieldEditing.SetValue(ws, ws.TopBlocks[0], ws.TopBlocks[0].GetField("TEXT"), "b"));
            h.Undo();
            Assert.IsTrue(h.CanRedo);
            Act(h, () => FieldEditing.SetValue(ws, ws.TopBlocks[0], ws.TopBlocks[0].GetField("TEXT"), "c"));
            Assert.IsFalse(h.CanRedo, "the 'b' branch is gone");
            h.Undo();
            Assert.AreEqual("a", ws.TopBlocks[0].GetFieldValue("TEXT"));
        }

        [Test]
        public void TheWorkspaceObjectIsKeptSoViewsAndRunnersStayValid()
        {
            var ws = FromXml("<block type=\"text\" x=\"0\" y=\"0\"><field name=\"TEXT\">a</field></block>");
            var h = new UndoHistory(ws);
            var kept = ws; var registry = ws.Registry;
            Act(h, () => FieldEditing.SetValue(ws, ws.TopBlocks[0], ws.TopBlocks[0].GetField("TEXT"), "b"));
            h.Undo();
            Assert.AreSame(kept, ws);
            Assert.AreSame(registry, ws.Registry);
            Assert.AreEqual(1, ws.TopBlocks.Count);
        }

        [Test]
        public void TheHistoryIsCappedAtItsLimitDroppingTheOldest()
        {
            var ws = FromXml("<block type=\"math_number\" x=\"0\" y=\"0\"><field name=\"NUM\">0</field></block>");
            var h = new UndoHistory(ws, limit: 5);
            for (int i = 1; i <= 12; i++) Act(h, () => FieldEditing.SetValue(ws, ws.TopBlocks[0], ws.TopBlocks[0].GetField("NUM"), i.ToString()));
            Assert.AreEqual(5, h.UndoCount);
            while (h.Undo()) { }
            Assert.AreEqual("7", ws.TopBlocks[0].GetFieldValue("NUM"), "steps 8..12 could be undone, so we end at the state after step 7");
        }

        [Test]
        public void ChangedIsRaisedForEveryTransition()
        {
            var ws = FromXml("<block type=\"text\" x=\"0\" y=\"0\"><field name=\"TEXT\">a</field></block>");
            var h = new UndoHistory(ws);
            int n = 0; h.Changed += () => n++;
            Act(h, () => FieldEditing.SetValue(ws, ws.TopBlocks[0], ws.TopBlocks[0].GetField("TEXT"), "b"));
            h.Undo(); h.Redo();
            Assert.AreEqual(3, n);
            Act(h, () => { });
            Assert.AreEqual(3, n, "a no-op action is silent");
        }

        // ---------------------------------------------------------------- every editing operation is undoable
        static void AssertUndoRedoRestores(Workspace ws, UndoHistory h, Action action, string what)
        {
            string before = h.Snapshot();
            string programBefore = Run(ws);
            action();
            Assert.IsTrue(h.Commit(before), what + ": the action should have changed something");
            string after = h.Snapshot();
            string programAfter = Run(ws);

            Assert.IsTrue(h.Undo(), what);
            Assert.AreEqual(before, h.Snapshot(), what + ": undo restores the exact previous state");
            Assert.AreEqual(programBefore, Run(ws), what + ": and the program behaves as before");
            Assert.IsTrue(h.Redo(), what);
            Assert.AreEqual(after, h.Snapshot(), what + ": redo restores the edited state");
            Assert.AreEqual(programAfter, Run(ws), what);
        }

        const string Program =
            "<variables><variable id=\"va\">a</variable><variable id=\"vb\">b</variable></variables>" +
            "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"300\"><field name=\"NAME\">foo</field><statement name=\"STACK\"><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">in foo</field></block></value></block></statement></block>" +
            "<block type=\"variables_set\" x=\"0\" y=\"0\"><field name=\"VAR\" id=\"va\">a</field><value name=\"VALUE\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value><next>" +
            "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"variables_get\"><field name=\"VAR\" id=\"va\">a</field></block></value><next>" +
            "<block type=\"procedures_callnoreturn\"><mutation name=\"foo\"></mutation><next>" +
            "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"lists_getIndex\"><mutation statement=\"false\" at=\"true\"></mutation><field name=\"MODE\">GET</field><field name=\"WHERE\">FROM_START</field>" +
            "<value name=\"VALUE\"><block type=\"lists_create_with\"><mutation items=\"2\"></mutation><value name=\"ADD0\"><block type=\"text\"><field name=\"TEXT\">x</field></block></value><value name=\"ADD1\"><block type=\"text\"><field name=\"TEXT\">y</field></block></value></block></value>" +
            "<value name=\"AT\"><block type=\"math_number\"><field name=\"NUM\">2</field></block></value></block></value></block></next></block></next></block></next></block>";

        static (Workspace ws, UndoHistory h, BlockLayouter l, BlockDragger d) Setup()
        {
            var ws = FromXml(Program);
            var l = new BlockLayouter(ws, new FixedWidth());
            return (ws, new UndoHistory(ws), l, new BlockDragger(ws, l));
        }

        static Block One(Workspace ws, string type) => ws.AllBlocks().First(b => b.Type == type);

        [Test]
        public void UndoRevertsADragThatConnectsABlock()
        {
            var (ws, h, l, d) = Setup();
            var print = ws.TopBlocks.First(b => b.Type == "variables_set").NextBlock;           // print a
            AssertUndoRedoRestores(ws, h, () =>
            {
                var mover = One(ws, "text_print");
                Assert.IsTrue(d.Begin(mover));
                d.MoveTo(600, 600);
                d.End();
            }, "drag a stack away");
            Assert.NotNull(print);
        }

        [Test]
        public void UndoRevertsSpawningFromTheToolboxAndDroppingIt()
        {
            var (ws, h, l, d) = Setup();
            var toolbox = TestSupport.NewToolbox();
            var spec = toolbox.Categories.SelectMany(c => c.Blocks).First(b => (string)b["type"] == "math_arithmetic");
            string before = h.Snapshot();
            var block = WorkspaceEditing.Spawn(ws, spec);
            block.X = 500; block.Y = 500; ws.TopBlocks.Add(block);
            Assert.IsTrue(d.Begin(block, isNew: true));
            d.MoveTo(520, 520);
            d.End();
            Assert.IsTrue(h.Commit(before), "spawn + drop is one step");
            Assert.AreEqual(1, h.UndoCount);
            h.Undo();
            Assert.AreEqual(before, h.Snapshot(), "a single undo removes the new block again");
        }

        [Test]
        public void ACancelledToolboxDragLeavesNothingToUndo()
        {
            var (ws, h, l, d) = Setup();
            var spec = TestSupport.NewToolbox().Categories.SelectMany(c => c.Blocks).First(b => (string)b["type"] == "controls_if");
            Assert.IsFalse(Act(h, () =>
            {
                var block = WorkspaceEditing.Spawn(ws, spec); block.X = 400; block.Y = 400; ws.TopBlocks.Add(block);
                d.Begin(block, isNew: true); d.MoveTo(450, 450); d.Cancel();
            }));
            Assert.IsFalse(h.CanUndo);
        }

        [Test]
        public void UndoRevertsADeleteIntoTheTrash()
        {
            var (ws, h, l, d) = Setup();
            AssertUndoRedoRestores(ws, h, () => { d.Begin(One(ws, "procedures_callnoreturn")); d.Delete(); }, "trash a stack");
        }

        [Test]
        public void UndoRevertsDeletingABlockWithTheDeleteKey()
        {
            var (ws, h, l, d) = Setup();
            AssertUndoRedoRestores(ws, h, () => WorkspaceEditing.Delete(ws, l, One(ws, "variables_set"), heal: true), "delete with heal");
        }

        [Test]
        public void UndoRevertsTextNumberAndDropdownEdits()
        {
            var (ws, h, l, d) = Setup();
            var num = ws.AllBlocks().First(b => b.Type == "math_number");
            AssertUndoRedoRestores(ws, h, () => FieldEditing.SetValue(ws, num, num.GetField("NUM"), "42"), "number");
            var text = ws.AllBlocks().First(b => b.Type == "text");
            AssertUndoRedoRestores(ws, h, () => FieldEditing.SetValue(ws, text, text.GetField("TEXT"), "changed ✓"), "text");
        }

        [Test]
        public void UndoRevertsADropdownThatChangedTheBlocksShape()
        {
            var (ws, h, l, d) = Setup();
            var get = One(ws, "lists_getIndex");
            AssertUndoRedoRestores(ws, h, () => FieldEditing.SetValue(ws, get, get.GetField("WHERE"), "FIRST", l), "WHERE first drops the AT block");
            Assert.IsNull(get.GetInput("AT"), "(redo state is current)");
            h.Undo();
            Assert.IsNotNull(One(ws, "lists_getIndex").GetInput("AT"), "the index input and the number that was in it are back");
            Assert.AreEqual("2", One(ws, "lists_getIndex").GetInputTargetBlock("AT").GetFieldValue("NUM"));
            h.Redo();
            var get2 = One(ws, "lists_getIndex");
            AssertUndoRedoRestores(ws, h, () => FieldEditing.SetValue(ws, get2, get2.GetField("MODE"), "REMOVE", l), "MODE remove makes it a statement");
        }

        [Test]
        public void UndoRevertsVariableCreateRenameAndDelete()
        {
            var (ws, h, l, d) = Setup();
            AssertUndoRedoRestores(ws, h, () => VariableEditing.Create(ws, "fresh", out _), "create");
            AssertUndoRedoRestores(ws, h, () => VariableEditing.Rename(ws, "va", "alpha", out _), "rename");
            AssertUndoRedoRestores(ws, h, () => VariableEditing.Delete(ws, l, "va"), "delete (takes its blocks along)");
            Assert.IsNull(ws.GetVariableById("va"));
            h.Undo();
            Assert.NotNull(ws.GetVariableById("va"), "the variable and every block that used it are back");
            Assert.AreEqual(2, VariableEditing.CountUses(ws, "va"), "both the set and the get that used it are back");
        }

        [Test]
        public void UndoRevertsRenamingAProcedureAndItsCallers()
        {
            var (ws, h, l, d) = Setup();
            var def = ws.TopBlocks.First(b => ProcedureInfo.IsDefinition(b));
            AssertUndoRedoRestores(ws, h, () => FieldEditing.SetValue(ws, def, def.GetField("NAME"), "greet"), "rename procedure");
            h.Undo();
            Assert.AreEqual("foo", One(ws, "procedures_callnoreturn").ExtraState["name"], "the call block names the procedure it was calling");
            Assert.AreEqual("1,in foo,y", Run(ws), "the call reaches the procedure again, so its output is in the middle");
        }

        // ---------------------------------------------------------------- reference programs
        public static IEnumerable<string> Programs()
            => Directory.GetFiles(Dir, "*.expected.txt").Select(f => Path.GetFileName(f).Replace(".expected.txt", "")).OrderBy(x => x);

        [TestCaseSource(nameof(Programs))]
        public void RestoringASnapshotIsExactForEveryReferenceProgram(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            string snap = WorkspaceJson.Save(ws);
            string output = Run(ws);
            WorkspaceJson.Restore(ws, snap);
            Assert.AreEqual(snap, WorkspaceJson.Save(ws), "save -> restore -> save is stable");
            Assert.AreEqual(output, Run(ws), "and the restored program runs identically");
            Assert.AreEqual(string.Join(",", File.ReadAllLines(Path.Combine(Dir, name + ".expected.txt"))), Run(ws), "and it still matches what real Blockly printed");
        }

        // ---------------------------------------------------------------- random edits mixed with undo / redo
        public static string[] FuzzPrograms => new[] { "loops", "procedures", "lists", "if_else", "variables", "text_more" };

        [TestCaseSource(nameof(FuzzPrograms))]
        public void RandomEditsWithUndoAndRedoAlwaysLandOnTheRecordedStates(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            var layouter = new BlockLayouter(ws, new FixedWidth());
            var dragger = new BlockDragger(ws, layouter);
            var h = new UndoHistory(ws, limit: 10000);
            var toolbox = TestSupport.NewToolbox();
            var rng = new Random(2024 + name.Length * 13);

            var states = new List<string> { h.Snapshot() };      // an independent model of the history
            int cursor = 0, edits = 0, undos = 0, redos = 0, cancelled = 0;

            void Commit(string before)
            {
                if (!h.Commit(before)) { cancelled++; return; }
                states.RemoveRange(cursor + 1, states.Count - cursor - 1);      // a new edit drops the redo branch
                states.Add(h.Snapshot());
                cursor++; edits++;
            }

            for (int step = 0; step < 160; step++)
            {
                string ctx = name + " step " + step;
                int op = rng.Next(12);
                string before = h.Snapshot();
                var all = ws.AllBlocks().ToList();

                if (op <= 2 && all.Count > 0)                                   // move / connect an existing stack
                {
                    var b = all[rng.Next(all.Count)];
                    if (dragger.Begin(b))
                    {
                        dragger.MoveTo(rng.Next(0, 700), rng.Next(0, 700));
                        if (rng.Next(5) == 0) dragger.Cancel(); else dragger.End();
                    }
                    Commit(before);
                }
                else if (op == 3 || op == 4)                                    // pull something out of the toolbox
                {
                    var cat = toolbox.Categories[rng.Next(toolbox.Categories.Count)];
                    var specs = Flyout.Specs(cat, ws);
                    var block = WorkspaceEditing.Spawn(ws, specs[rng.Next(specs.Count)]);
                    block.X = rng.Next(0, 600); block.Y = rng.Next(0, 600);
                    ws.TopBlocks.Add(block);
                    dragger.Begin(block, isNew: true);
                    dragger.MoveTo(rng.Next(0, 700), rng.Next(0, 700));
                    if (rng.Next(3) == 0) dragger.Cancel(); else dragger.End();
                    Commit(before);
                }
                else if (op == 5 && all.Count > 0)                              // trash
                {
                    var b = all[rng.Next(all.Count)];
                    if (dragger.Begin(b)) dragger.Delete();
                    Commit(before);
                }
                else if (op == 6 && all.Count > 0)                              // Delete key
                {
                    WorkspaceEditing.Delete(ws, layouter, all[rng.Next(all.Count)], heal: true);
                    Commit(before);
                }
                else if (op == 7)                                               // text / number / dropdown / variable pickers
                {
                    var fields = all.SelectMany(b => b.AllFields().Select(f => (b, f))).Where(x => FieldEditing.IsEditableText(x.f) || FieldEditing.IsDropdown(x.f)).ToList();
                    if (fields.Count > 0)
                    {
                        var (b, f) = fields[rng.Next(fields.Count)];
                        if (FieldEditing.IsEditableText(f)) FieldEditing.SetValue(ws, b, f, f.Spec.FieldType == "field_number" ? rng.Next(0, 99).ToString() : "t" + rng.Next(0, 99), layouter);
                        else
                        {
                            var items = FieldEditing.GetDropdownItems(ws, f).Where(x => x.Action == DropdownAction.Select).ToList();
                            if (items.Count > 0) FieldEditing.SetValue(ws, b, f, items[rng.Next(items.Count)].Value, layouter);
                        }
                    }
                    Commit(before);
                }
                else if (op == 8)                                               // variables
                {
                    int what = rng.Next(3);
                    if (what == 0) VariableEditing.Create(ws, "v" + step, out _);
                    else if (ws.Variables.Count > 0 && what == 1) VariableEditing.Rename(ws, ws.Variables[rng.Next(ws.Variables.Count)].Id, "r" + step, out _);
                    else if (ws.Variables.Count > 1) VariableEditing.Delete(ws, layouter, ws.Variables[rng.Next(ws.Variables.Count)].Id);
                    Commit(before);
                }
                else if (op == 9 || op == 10)                                   // undo
                {
                    bool expected = cursor > 0;
                    Assert.AreEqual(expected, h.Undo(), ctx + ": undo availability");
                    if (expected) { cursor--; undos++; }
                    if (expected && rng.Next(2) == 0)                               // often step straight back with redo, so redo is always exercised
                    {
                        Assert.IsTrue(h.Redo(), ctx + ": redo right after undo");
                        cursor++; redos++;
                    }
                }
                else                                                            // redo
                {
                    bool expected = cursor < states.Count - 1;
                    Assert.AreEqual(expected, h.Redo(), ctx + ": redo availability");
                    if (expected) { cursor++; redos++; }
                }

                Assert.AreEqual(states[cursor], h.Snapshot(), ctx + ": the workspace is in the recorded state #" + cursor);
                Assert.AreEqual(cursor > 0, h.CanUndo, ctx);
                Assert.AreEqual(cursor < states.Count - 1, h.CanRedo, ctx);
                BlockDraggerTests.AssertModelIsSound(ws, ws.AllBlocks().Count(), ctx);
            }

            // walk all the way back, then all the way forward: every state is hit exactly
            while (h.CanUndo) { h.Undo(); cursor--; Assert.AreEqual(states[cursor], h.Snapshot(), name + ": walking back"); }
            Assert.AreEqual(states[0], h.Snapshot(), "undoing everything returns to the loaded program");
            while (h.CanRedo) { h.Redo(); cursor++; Assert.AreEqual(states[cursor], h.Snapshot(), name + ": walking forward"); }
            Assert.AreEqual(states[states.Count - 1], h.Snapshot());

            Assert.Greater(edits, 15, name + ": enough real edits happened");
            Assert.Greater(undos, 5, name); Assert.Greater(redos, 1, name); Assert.Greater(cancelled, 0, name + ": some actions changed nothing and left no step");
        }
    }
}
