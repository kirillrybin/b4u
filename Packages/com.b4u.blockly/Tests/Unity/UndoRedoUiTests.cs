using System.Collections.Generic;
using System.Linq;
using Blockly.Editing;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>Undo / redo as the user experiences it: every kind of edit is one step, and the screen follows.</summary>
    public class UndoRedoUiTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            foreach (var g in Object.FindObjectsByType<DropdownMenu>(FindObjectsSortMode.None)) Object.DestroyImmediate(g.gameObject);
            foreach (var g in Object.FindObjectsByType<DialogView>(FindObjectsSortMode.None)) Object.DestroyImmediate(g.gameObject);
        }

        const string Xml =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<variables><variable id=\"va\">a</variable><variable id=\"vb\">b</variable></variables>" +
            "<block type=\"variables_set\" x=\"20\" y=\"20\"><field name=\"VAR\" id=\"va\">a</field><value name=\"VALUE\"><block type=\"math_arithmetic\"><field name=\"OP\">ADD</field>" +
            "<value name=\"A\"><block type=\"math_number\"><field name=\"NUM\">6</field></block></value><value name=\"B\"><block type=\"math_number\"><field name=\"NUM\">3</field></block></value></block></value>" +
            "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"variables_get\"><field name=\"VAR\" id=\"va\">a</field></block></value></block></next></block>" +
            "<block type=\"text\" x=\"400\" y=\"20\"><field name=\"TEXT\">hello</field></block>" +
            "<block type=\"text_print\" x=\"20\" y=\"300\"></block></xml>";

        WorkspaceView NewView(string xml = Xml)
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load(xml));
            return view;
        }

        static Block Find(WorkspaceView v, System.Func<Block, bool> p) => v.Workspace.AllBlocks().First(p);
        static Block TextBlock(WorkspaceView v) => Find(v, b => b.Type == "text");
        static string Save(WorkspaceView v) => WorkspaceJson.Save(v.Workspace);
        static bool Shows(WorkspaceView v, string text) => v.GetComponentsInChildren<TMP_Text>().Any(t => t.text == text);

        static void EditText(WorkspaceView v, string value)
        {
            var t = TextBlock(v);
            Assert.IsTrue(v.BeginFieldEdit(t, t.GetField("TEXT")));
            Assert.IsTrue(v.SubmitFieldEdit(value));
        }

        // ---------------------------------------------------------------- basics
        [Test]
        public void ATextEditCanBeUndoneAndRedoneAndTheScreenFollows()
        {
            var view = NewView();
            int edits = 0, history = 0; view.WorkspaceEdited += () => edits++; view.HistoryChanged += () => history++;
            Assert.IsFalse(view.CanUndo);
            EditText(view, "bye");
            Assert.IsTrue(view.CanUndo); Assert.IsFalse(view.CanRedo);
            Assert.IsTrue(Shows(view, "bye"));

            int editsBeforeUndo = edits;
            Assert.IsTrue(view.Undo());
            Assert.AreEqual("hello", TextBlock(view).GetFieldValue("TEXT"));
            Assert.IsTrue(Shows(view, "hello")); Assert.IsFalse(Shows(view, "bye"));
            Assert.Greater(edits, editsBeforeUndo, "listeners (the toolbox) are told the workspace changed");
            Assert.IsFalse(view.CanUndo); Assert.IsTrue(view.CanRedo);

            Assert.IsTrue(view.Redo());
            Assert.AreEqual("bye", TextBlock(view).GetFieldValue("TEXT"));
            Assert.IsTrue(Shows(view, "bye"));
            Assert.Greater(history, 2, "the buttons were told on every change");
        }

        [Test]
        public void EditingKeepsWorkingOnTheBlocksThatUndoRestored()
        {
            var view = NewView();
            var stale = TextBlock(view);
            EditText(view, "one");
            view.Undo();
            Assert.AreNotSame(stale, TextBlock(view), "undo rebuilt the blocks as new objects");
            EditText(view, "two");                                    // uses the fresh objects
            Assert.AreEqual("two", TextBlock(view).GetFieldValue("TEXT"));
            Assert.IsTrue(Shows(view, "two"));
            Assert.IsTrue(view.Undo());
            Assert.AreEqual("hello", TextBlock(view).GetFieldValue("TEXT"));
        }

        [Test]
        public void ANewEditAfterUndoDropsTheRedoBranch()
        {
            var view = NewView();
            EditText(view, "first");
            view.Undo();
            Assert.IsTrue(view.CanRedo);
            EditText(view, "second");
            Assert.IsFalse(view.CanRedo);
        }

        [Test]
        public void SettingTheSameValueOrCancellingLeavesNoStep()
        {
            var view = NewView();
            var t = TextBlock(view);
            view.BeginFieldEdit(t, t.GetField("TEXT"));
            Assert.IsFalse(view.SubmitFieldEdit("hello"));
            view.BeginFieldEdit(t, t.GetField("TEXT"));
            view.CancelFieldEdit();
            Assert.IsFalse(view.CanUndo);
        }

        // ---------------------------------------------------------------- dragging
        Vector2 PointOntoInput(WorkspaceView v, Block target, string input, Block dragged, Vector2 grab)
        {
            var tp = v.Layout.ByBlock[target].Connections[target.GetInput(input).Connection];
            var mp = v.Layout.ByBlock[dragged].Connections[dragged.Output];
            return grab + new Vector2(tp.X - mp.X, tp.Y - mp.Y);
        }

        [Test]
        public void DroppingABlockIntoAnInputIsOneStepAndUndoPutsItBack()
        {
            var view = NewView();
            string before = Save(view);
            var text = TextBlock(view);
            var emptyPrint = view.Workspace.TopBlocks.Last();
            var box = view.Layout.ByBlock[text];
            var grab = new Vector2(box.X + 5, box.Y + 5);
            Assert.IsTrue(view.BeginDrag(text, grab));
            view.DragTo(PointOntoInput(view, emptyPrint, "TEXT", text, grab));
            view.EndDrag();
            Assert.AreSame(text, emptyPrint.GetInputTargetBlock("TEXT"));
            string after = Save(view);

            Assert.IsTrue(view.Undo());
            Assert.AreEqual(before, Save(view), "the text block is a loose block again, exactly where it was");
            Assert.IsFalse(view.CanUndo, "that was a single step");
            Assert.IsTrue(view.Redo());
            Assert.AreEqual(after, Save(view));
        }

        [Test]
        public void ACancelledDragLeavesNothingToUndo()
        {
            var view = NewView();
            var text = TextBlock(view);
            var box = view.Layout.ByBlock[text];
            view.BeginDrag(text, new Vector2(box.X + 5, box.Y + 5));
            view.DragTo(new Vector2(0, 0));
            view.CancelDrag();
            Assert.IsFalse(view.CanUndo);
        }

        [Test]
        public void MovingABlockInTheAirCanBeUndoneToo()
        {
            var view = NewView();
            string before = Save(view);
            var text = TextBlock(view);
            var box = view.Layout.ByBlock[text];
            view.BeginDrag(text, new Vector2(box.X + 5, box.Y + 5));
            view.DragTo(new Vector2(box.X + 205, box.Y + 105));
            view.EndDrag();
            Assert.AreNotEqual(before, Save(view));
            view.Undo();
            Assert.AreEqual(before, Save(view));
        }

        [Test]
        public void ThrowingABlockInTheTrashCanBeUndone()
        {
            var view = NewView();
            string before = Save(view);
            int blocks = view.Workspace.AllBlocks().Count();
            var text = TextBlock(view);
            var box = view.Layout.ByBlock[text];
            view.BeginDrag(text, new Vector2(box.X + 5, box.Y + 5));
            view.SetOverDeleteArea(true);
            view.EndDrag();
            Assert.AreEqual(blocks - 1, view.Workspace.AllBlocks().Count());
            view.Undo();
            Assert.AreEqual(before, Save(view));
            Assert.AreEqual(blocks, view.Workspace.AllBlocks().Count());
            Assert.IsTrue(Shows(view, "hello"), "and it is back on screen");
        }

        // ---------------------------------------------------------------- dropdowns and variables
        [Test]
        public void ADropdownChangeCanBeUndone()
        {
            var view = NewView();
            var arith = Find(view, b => b.Type == "math_arithmetic");
            view.ApplyFieldValue(arith, arith.GetField("OP"), "MULTIPLY");
            Assert.IsTrue(Shows(view, "×"));
            view.Undo();
            Assert.AreEqual("ADD", Find(view, b => b.Type == "math_arithmetic").GetFieldValue("OP"));
            Assert.IsTrue(Shows(view, "+")); Assert.IsFalse(Shows(view, "×"));
        }

        [Test]
        public void VariableCreateRenameAndDeleteAreEachOneUndoableStep()
        {
            var view = NewView();
            string s0 = Save(view);

            view.RequestCreateVariable(); view.ActiveDialog.Submit("fresh");
            string s1 = Save(view);
            view.RequestRenameVariable("va"); view.ActiveDialog.Submit("alpha");
            string s2 = Save(view);
            view.RequestDeleteVariable("va"); view.ActiveDialog.Confirm();            // used twice: asks first
            Assert.IsNull(view.Workspace.GetVariableById("va"));
            Assert.AreEqual(3, view.History.UndoCount);

            view.Undo(); Assert.AreEqual(s2, Save(view), "the variable and its blocks are back");
            Assert.IsTrue(Shows(view, "alpha"));
            view.Undo(); Assert.AreEqual(s1, Save(view));
            view.Undo(); Assert.AreEqual(s0, Save(view));
            Assert.IsNull(view.Workspace.GetVariableByName("fresh"));
            Assert.IsFalse(view.CanUndo);
        }

        // ---------------------------------------------------------------- guards, events, shortcuts
        [Test]
        public void UndoWaitsUntilADragAFieldEditOrADialogIsFinished()
        {
            var view = NewView();
            EditText(view, "step one");

            var text = TextBlock(view);
            view.BeginFieldEdit(text, text.GetField("TEXT"));
            Assert.IsFalse(view.Undo(), "a field is open");
            view.CancelFieldEdit();

            var box = view.Layout.ByBlock[text];
            view.BeginDrag(text, new Vector2(box.X + 5, box.Y + 5));
            Assert.IsFalse(view.Undo(), "a block is being dragged");
            view.CancelDrag();

            view.RequestCreateVariable();
            Assert.IsFalse(view.Undo(), "a dialog is open");
            view.ActiveDialog.Cancel();

            Assert.IsTrue(view.Undo(), "once everything is closed it works");
        }

        [Test]
        public void UndoAndRedoStopARunningProgramLikeAnyOtherEdit()
        {
            var view = NewView();
            EditText(view, "x");
            int stops = 0; view.FieldEditStarted += _ => stops++;
            view.Undo(); view.Redo();
            Assert.AreEqual(2, stops);
        }

        [Test]
        public void KeyboardShortcutsUndoAndRedo()
        {
            var view = NewView();
            EditText(view, "typed");
            Assert.IsFalse(view.HandleShortcut(KeyCode.Z, false, false), "needs Ctrl / Cmd");
            Assert.IsTrue(view.HandleShortcut(KeyCode.Z, true, false));
            Assert.AreEqual("hello", TextBlock(view).GetFieldValue("TEXT"));
            Assert.IsTrue(view.HandleShortcut(KeyCode.Z, true, true), "Ctrl+Shift+Z redoes");
            Assert.AreEqual("typed", TextBlock(view).GetFieldValue("TEXT"));
            view.HandleShortcut(KeyCode.Z, true, false);
            Assert.IsTrue(view.HandleShortcut(KeyCode.Y, true, false), "Ctrl+Y redoes");
            Assert.AreEqual("typed", TextBlock(view).GetFieldValue("TEXT"));
            Assert.IsFalse(view.HandleShortcut(KeyCode.Q, true, false));
        }

        [Test]
        public void ShortcutsDoNotStealTheUndoOfATextField()
        {
            var view = NewView();
            EditText(view, "kept");
            var t = TextBlock(view);
            view.BeginFieldEdit(t, t.GetField("TEXT"));
            Assert.IsFalse(view.HandleShortcut(KeyCode.Z, true, false), "while typing, Ctrl+Z belongs to the input field");
            view.CancelFieldEdit();
            Assert.AreEqual("kept", TextBlock(view).GetFieldValue("TEXT"));
        }

        // ---------------------------------------------------------------- clean up, replacing the workspace, flyouts
        [Test]
        public void ArrangingABlockPileIsUndoableButNotWhenAProgramIsFirstLoaded()
        {
            var view = NewView();
            view.CleanUp(recordUndo: false);
            Assert.IsFalse(view.CanUndo, "loading must not leave something to undo");
            string arranged = Save(view);
            var t = TextBlock(view); t.X = 5; t.Y = 5;               // pile things up again
            view.CleanUp();
            Assert.IsTrue(view.CanUndo);
            view.Undo();
            Assert.AreEqual(5, TextBlock(view).X, 1e-3);
            Assert.AreNotEqual(arranged, Save(view));
        }

        [Test]
        public void ReplacingTheWorkspaceStartsAFreshHistory()
        {
            var view = NewView();
            EditText(view, "changed");
            int notified = 0; view.HistoryChanged += () => notified++;
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            Assert.IsFalse(view.CanUndo); Assert.IsFalse(view.CanRedo);
            Assert.Greater(notified, 0, "the Undo/Redo buttons are refreshed");
        }

        sealed class NoSpawn : IBlockSpawner { public bool TrySpawn(Block b, PointerEventData e, WorkspaceView v) => false; }

        [Test]
        public void AFlyoutHasNoHistory()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.Spawner = new NoSpawn();
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            Assert.IsNull(view.History);
            Assert.IsFalse(view.Undo()); Assert.IsFalse(view.Redo());
        }

        [Test]
        public void TheViewStaysConsistentAfterEveryUndoAndRedo()
        {
            var view = NewView();
            EditText(view, "a");
            var arith = Find(view, b => b.Type == "math_arithmetic");
            view.ApplyFieldValue(arith, arith.GetField("OP"), "POWER");
            view.RequestCreateVariable(); view.ActiveDialog.Submit("zz");
            for (int i = 0; i < 3; i++) { view.Undo(); AssertConsistent(view); }
            for (int i = 0; i < 3; i++) { view.Redo(); AssertConsistent(view); }
        }

        static void AssertConsistent(WorkspaceView v)
        {
            Assert.AreEqual(v.Workspace.AllBlocks().Count(), v.Layout.Boxes.Count(b => !b.IsShadow));
            Assert.AreEqual(v.Layout.Boxes.Count, v.GetComponentsInChildren<BlockDragTarget>().Length, "one object per block on screen");
        }
    }
}
