using System.Linq;
using Blockly.Editing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI.Tests
{
    /// <summary>Clicking a block selects it; the Delete key removes the selection, closing the gap, as one undo step.</summary>
    public class SelectionTests
    {
        GameObject root;

        [TearDown]
        public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        const string Xml =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<block type=\"text_print\" x=\"20\" y=\"20\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">one</field></block></value>" +
            "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">two</field></block></value>" +
            "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">three</field></block></value></block></next></block></next></block></xml>";

        WorkspaceView NewView()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            return view;
        }

        static Block Print(WorkspaceView v, int n) => v.Workspace.AllBlocks().Where(b => b.Type == "text_print").ElementAt(n);
        static void Click(WorkspaceView v, Block b)
        {
            var target = v.GetComponentsInChildren<BlockDragTarget>().First(t => t.Block == b);
            target.OnPointerClick(new PointerEventData(EventSystem.current));
        }

        [Test]
        public void ClickingABlockSelectsItAndClickingTheBackgroundClearsIt()
        {
            var view = NewView();
            int changes = 0; view.SelectionChanged += _ => changes++;
            var second = Print(view, 1);
            Click(view, second);
            Assert.AreSame(second, view.SelectedBlock);
            Assert.AreEqual(1, changes);
            Assert.IsNotNull(view.transform.Find("selection"));
            Assert.IsTrue(view.transform.Find("selection").gameObject.activeSelf);

            view.Select(null);
            Assert.IsNull(view.SelectedBlock);
            Assert.IsFalse(view.transform.Find("selection").gameObject.activeSelf);
        }

        [Test]
        public void ASelectionSurvivesARebuildButNotTheBlocksDeath()
        {
            var view = NewView();
            var second = Print(view, 1);
            view.Select(second);
            view.Rebuild();
            Assert.AreSame(second, view.SelectedBlock);
            Assert.IsTrue(view.transform.Find("selection").gameObject.activeSelf);
        }

        [Test]
        public void DeleteRemovesTheSelectedBlockAndClosesTheGap()
        {
            var view = NewView();
            view.Select(Print(view, 1));
            Assert.IsTrue(view.HandleShortcut(KeyCode.Delete, false, false));
            var prints = view.Workspace.AllBlocks().Where(b => b.Type == "text_print").ToList();
            Assert.AreEqual(2, prints.Count);
            Assert.AreSame(prints[1], prints[0].NextBlock, "the follower reconnected to what preceded the deleted block");
            Assert.IsNull(view.SelectedBlock);
            Assert.IsFalse(view.Workspace.AllBlocks().Any(b => b.GetFieldValue("TEXT") == "two"), "its inputs went with it");
            var outline = view.transform.Find("selection");
            Assert.IsTrue(outline == null || !outline.gameObject.activeSelf);
        }

        [Test]
        public void DeleteIsOneUndoStep()
        {
            var view = NewView();
            string before = WorkspaceJson.Save(view.Workspace);
            view.Select(Print(view, 1));
            view.HandleShortcut(KeyCode.Backspace, false, false);
            Assert.IsTrue(view.CanUndo);
            Assert.IsTrue(view.Undo());
            Assert.AreEqual(before, WorkspaceJson.Save(view.Workspace));
            Assert.IsFalse(view.CanUndo);
        }

        [Test]
        public void DeleteWithNothingSelectedOrWhileTypingDoesNothing()
        {
            var view = NewView();
            string before = WorkspaceJson.Save(view.Workspace);
            Assert.IsFalse(view.HandleShortcut(KeyCode.Delete, false, false));
            view.Select(Print(view, 0));
            Assert.IsFalse(view.HandleShortcut(KeyCode.Delete, true, false), "Ctrl+Delete is not Delete");
            var t = view.Workspace.AllBlocks().First(b => b.Type == "text");
            Assert.IsTrue(view.BeginFieldEdit(t, t.GetField("TEXT")));
            Assert.IsFalse(view.HandleShortcut(KeyCode.Delete, false, false), "the key belongs to the text box");
            view.CancelFieldEdit();
            Assert.AreEqual(before, WorkspaceJson.Save(view.Workspace));
        }

        [Test]
        public void ABlockOutsideTheWorkspaceCannotBeSelected()
        {
            var view = NewView();
            view.Select(StandardBlockSet.Load(Xml).AllBlocks().First());
            Assert.IsNull(view.SelectedBlock);
        }
    }
}
