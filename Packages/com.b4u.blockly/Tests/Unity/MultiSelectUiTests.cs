using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Blockly.UI.Tests
{
    /// <summary>Selecting several stacks at once and acting on them together.</summary>
    public class MultiSelectUiTests
    {
        GameObject root;

        [SetUp] public void SetUp() => WorkspaceView.Clipboard = null;

        [TearDown]
        public void TearDown()
        {
            WorkspaceView.Clipboard = null;
            if (root != null) Object.DestroyImmediate(root);
        }

        // three stacks: A (two prints), B, C
        const string Xml =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<block type=\"text_print\" x=\"20\" y=\"20\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">a1</field></block></value>" +
            "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">a2</field></block></value></block></next></block>" +
            "<block type=\"text_print\" x=\"20\" y=\"200\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">b</field></block></value></block>" +
            "<block type=\"text_print\" x=\"20\" y=\"360\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">c</field></block></value></block></xml>";

        WorkspaceView NewView()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
            viewport.sizeDelta = new Vector2(3000, 3000);
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(viewport, false);
            var vrt = (RectTransform)go.transform;
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0, 1);
            vrt.anchoredPosition = Vector2.zero;
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            return view;
        }

        static Block A(WorkspaceView v) => v.Workspace.TopBlocks[0];
        static Block B(WorkspaceView v) => v.Workspace.TopBlocks[1];
        static Block C(WorkspaceView v) => v.Workspace.TopBlocks[2];
        static int Count(WorkspaceView v, string text) => v.Workspace.AllBlocks().Count(b => b.GetFieldValue("TEXT") == text);

        [Test]
        public void ToggleAddsAndRemovesStacksAndAPlainSelectReplacesThem()
        {
            var v = NewView();
            v.Select(A(v));
            Assert.IsTrue(v.ToggleInSelection(B(v)));
            Assert.IsTrue(v.IsMultiSelected);
            CollectionAssert.AreEquivalent(new[] { A(v), B(v) }, v.SelectedStacks);
            v.ToggleInSelection(C(v));
            Assert.AreEqual(3, v.SelectedStacks.Count);
            v.ToggleInSelection(C(v));
            Assert.AreEqual(2, v.SelectedStacks.Count);
            v.ToggleInSelection(A(v));
            Assert.IsFalse(v.IsMultiSelected, "one stack left is a plain selection");
            Assert.AreSame(B(v), v.SelectedBlock);
            v.ToggleInSelection(C(v));
            v.Select(C(v));
            Assert.IsFalse(v.IsMultiSelected);
            Assert.AreSame(C(v), v.SelectedBlock);
        }

        [Test]
        public void ABlockInsideAStackSelectsTheWholeStack()
        {
            var v = NewView();
            v.Select(B(v));
            v.ToggleInSelection(A(v).NextBlock.GetInputTargetBlock("TEXT"));
            CollectionAssert.AreEquivalent(new[] { A(v), B(v) }, v.SelectedStacks);
        }

        [Test]
        public void EveryBlockOfASelectedStackGetsAnOutlineAndTheSingleOutlineHides()
        {
            var v = NewView();
            v.Select(A(v)); v.ToggleInSelection(B(v));
            Assert.IsFalse(v.transform.Find("selection").gameObject.activeSelf);
            int outlines = v.transform.Cast<Transform>().Count(t => t.name.StartsWith("selection ") && t.gameObject.activeSelf);
            Assert.AreEqual(3, outlines, "two blocks of A and one of B");
            v.Select(B(v));
            Assert.AreEqual(0, v.transform.Cast<Transform>().Count(t => t.name.StartsWith("selection ") && t.gameObject.activeSelf));
        }

        [Test]
        public void SelectAllAndARectangleSelectStacks()
        {
            var v = NewView();
            Assert.IsTrue(v.SelectAll());
            Assert.AreEqual(3, v.SelectedStacks.Count);
            v.Select(null);
            var box = v.Layout.ByBlock[B(v)];
            v.SelectRect(new Rect(box.X, box.Y, 5, 5), false);
            Assert.AreEqual(1, v.SelectedStacks.Count);
            Assert.AreSame(B(v), v.SelectedBlock);
            var c = v.Layout.ByBlock[C(v)];
            v.SelectRect(new Rect(c.X, c.Y, 5, 5), true);
            CollectionAssert.AreEquivalent(new[] { B(v), C(v) }, v.SelectedStacks);
        }

        [Test]
        public void DraggingOneOfTheSelectedStacksMovesThemAllAsOneUndoStep()
        {
            var v = NewView();
            v.Select(A(v)); v.ToggleInSelection(B(v));
            double ax = A(v).X, ay = A(v).Y, bx = B(v).X, by = B(v).Y, cx = C(v).X;
            Assert.IsTrue(v.BeginGroupDrag(A(v), new Vector2(30, 30)));
            Assert.IsTrue(v.IsDragging);
            v.DragTo(new Vector2(70, 50));
            v.EndDrag();
            Assert.IsFalse(v.IsDragging);
            Assert.AreEqual(A(v).X - ax, B(v).X - bx, 0.01);
            Assert.AreEqual(A(v).Y - ay, B(v).Y - by, 0.01);
            Assert.AreNotEqual(ax, A(v).X);
            Assert.AreEqual(cx, C(v).X, "an unselected stack stays");
            Assert.IsTrue(v.IsMultiSelected, "still selected after the move");
            v.Undo();
            Assert.AreEqual(ax, A(v).X, 0.01); Assert.AreEqual(by, B(v).Y, 0.01);
        }

        [Test]
        public void CancellingAGroupDragPutsTheStacksBack()
        {
            var v = NewView();
            v.Select(A(v)); v.ToggleInSelection(B(v));
            double ax = A(v).X, by = B(v).Y;
            v.BeginGroupDrag(A(v), new Vector2(30, 30));
            v.DragTo(new Vector2(130, 130));
            v.CancelDrag();
            Assert.AreEqual(ax, A(v).X, 0.01); Assert.AreEqual(by, B(v).Y, 0.01);
            Assert.IsFalse(v.History.CanUndo, "nothing changed, nothing to undo");
        }

        [Test]
        public void DroppingTheGroupOnTheDeleteAreaDeletesItAll()
        {
            var v = NewView();
            v.Select(A(v)); v.ToggleInSelection(B(v));
            v.BeginGroupDrag(A(v), new Vector2(30, 30));
            v.SetOverDeleteArea(true);
            v.DragTo(new Vector2(40, 40));
            v.EndDrag();
            Assert.AreEqual(1, v.Workspace.TopBlocks.Count);
            Assert.AreEqual(0, Count(v, "a1") + Count(v, "a2") + Count(v, "b"));
            v.Undo();
            Assert.AreEqual(3, v.Workspace.TopBlocks.Count);
        }

        [Test]
        public void DeleteRemovesTheWholeSelectionAsOneUndoStep()
        {
            var v = NewView();
            v.SelectAll();
            Assert.IsTrue(v.DeleteSelected());
            Assert.AreEqual(0, v.Workspace.TopBlocks.Count);
            Assert.IsNull(v.SelectedBlock);
            Assert.IsTrue(v.Undo());
            Assert.AreEqual(3, v.Workspace.TopBlocks.Count);
            Assert.IsFalse(v.IsMultiSelected, "undo makes new blocks: nothing is selected");
        }

        [Test]
        public void CopyPasteAndCutWorkOnTheSelection()
        {
            var v = NewView();
            v.Select(A(v)); v.ToggleInSelection(B(v));
            Assert.IsTrue(v.CopySelected());
            Assert.IsTrue(v.Paste());
            Assert.AreEqual(2, Count(v, "a1")); Assert.AreEqual(2, Count(v, "a2"));
            Assert.AreEqual(2, Count(v, "b"));
            Assert.IsTrue(v.IsMultiSelected, "the pasted stacks are selected");
            Assert.IsTrue(v.CutSelected());
            Assert.AreEqual(1, Count(v, "b"));
            Assert.AreEqual(1, Count(v, "a1"));
        }

        [Test]
        public void DuplicateAndDisableWorkOnTheSelection()
        {
            var v = NewView();
            v.Select(A(v)); v.ToggleInSelection(B(v));
            Assert.IsTrue(v.ToggleDisabledSelected());
            Assert.IsTrue(A(v).Disabled); Assert.IsTrue(A(v).NextBlock.Disabled); Assert.IsTrue(B(v).Disabled);
            Assert.IsFalse(C(v).Disabled);
            Assert.IsTrue(v.ToggleDisabledSelected());
            Assert.IsFalse(A(v).Disabled);
            Assert.IsTrue(v.DuplicateSelected());
            Assert.AreEqual(5, v.Workspace.TopBlocks.Count);
            Assert.AreEqual(2, v.SelectedStacks.Count);
        }

        [Test]
        public void TheMenuOfASelectionOffersOnlyWhatFitsAllOfIt()
        {
            var v = NewView();
            v.Select(A(v)); v.ToggleInSelection(B(v));
            Assert.IsTrue(v.ShowBlockMenu(B(v), Vector2.zero));
            Assert.IsTrue(v.IsMultiSelected, "opening the menu does not drop the selection");
            var menu = Object.FindFirstObjectByType<DropdownMenu>();
            CollectionAssert.AreEqual(new[] { "Duplicate", "Copy", "Cut", "Disable block", "Delete" }, menu.Items.Select(i => i.Label));
        }

        [Test]
        public void TurningItOffMakesTheModifierAndSelectAllNoOps()
        {
            var v = NewView();
            v.Select(A(v));
            v.MultiSelectEnabled = false;
            Assert.IsFalse(v.ToggleInSelection(B(v)));
            Assert.IsFalse(v.SelectAll());
            v.SelectMode = true;
            Assert.IsFalse(v.SelectMode);
        }

        [Test]
        public void TheSelectButtonIsAlwaysThereAndTogglesTheMode()
        {
            var v = NewView();
            var host = new GameObject("Scroll", typeof(RectTransform)).GetComponent<RectTransform>();
            host.SetParent(root.transform, false);
            v.transform.parent.SetParent(host, false);
            v.SetWorkspace(v.Workspace);                                       // the button is made when the workspace is set
            Assert.IsNotNull(v.SelectButton, "shown while the mode is off");
            Assert.IsNull(v.SelectChip, "the chip is only for the mode on");
            Assert.AreEqual("Select", v.SelectButton.GetComponentInChildren<TMPro.TMP_Text>().text);
            v.SelectButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsTrue(v.SelectMode);
            StringAssert.StartsWith("Select: on", v.SelectButton.GetComponentInChildren<TMPro.TMP_Text>().text);
            v.SelectButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsFalse(v.SelectMode);
            v.SelectButtonVisible = false;
            Assert.IsNull(v.SelectButton, "can be hidden");
            v.SelectButtonVisible = true; v.MultiSelectEnabled = false;
            Assert.IsNull(v.SelectButton, "no button without multi-select");
        }

        [Test]
        public void TheSelectModeShowsAChipAndTheBackgroundMenuTogglesIt()
        {
            var v = NewView();
            var host = new GameObject("Scroll", typeof(RectTransform)).GetComponent<RectTransform>();
            host.SetParent(root.transform, false);
            v.transform.parent.SetParent(host, false);                 // Scroll > Viewport > View, like the playground
            v.SelectMode = true;
            Assert.IsNotNull(v.SelectChip);
            v.SelectChip.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsFalse(v.SelectMode);
            Assert.IsNull(v.SelectChip);
            Assert.IsTrue(v.ShowWorkspaceMenu(Vector2.zero));
            var menu = Object.FindFirstObjectByType<DropdownMenu>();
            CollectionAssert.AreEqual(new[] { "Add note", "Select blocks", "Select all" }, menu.Items.Select(i => i.Label));
        }
    }
}
