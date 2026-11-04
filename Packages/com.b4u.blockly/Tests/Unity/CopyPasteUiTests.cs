using System.Linq;
using Blockly.Editing;
using NUnit.Framework;
using UnityEngine;

namespace Blockly.UI.Tests
{
    /// <summary>Ctrl+C / Ctrl+X / Ctrl+V on the selected block, as the user presses them.</summary>
    public class CopyPasteUiTests
    {
        GameObject root;

        [SetUp]
        public void SetUp() => WorkspaceView.Clipboard = null;

        [TearDown]
        public void TearDown()
        {
            WorkspaceView.Clipboard = null;
            if (root != null) Object.DestroyImmediate(root);
        }

        const string Xml =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<block type=\"text_print\" x=\"20\" y=\"20\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">one</field></block></value>" +
            "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">two</field></block></value></block></next></block></xml>";

        WorkspaceView NewView()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            return view;
        }

        static int Count(WorkspaceView v, string text) => v.Workspace.AllBlocks().Count(b => b.GetFieldValue("TEXT") == text);

        [Test]
        public void CopyAndPasteDuplicatesTheSelectedBlockAndSelectsTheCopy()
        {
            var view = NewView();
            var first = view.Workspace.TopBlocks[0];
            view.Select(first);
            Assert.IsTrue(view.HandleShortcut(KeyCode.C, true, false));
            Assert.IsTrue(WorkspaceView.HasClipboard);
            Assert.AreEqual(1, Count(view, "one"), "copying changes nothing");
            Assert.IsFalse(view.CanUndo, "copying is not an undo step");

            Assert.IsTrue(view.HandleShortcut(KeyCode.V, true, false));
            Assert.AreEqual(2, Count(view, "one"));
            Assert.AreEqual(1, Count(view, "two"), "the follower was not copied");
            Assert.AreEqual(2, view.Workspace.TopBlocks.Count);
            Assert.AreNotSame(first, view.SelectedBlock);
            Assert.AreSame(view.Workspace.TopBlocks[1], view.SelectedBlock);
            Assert.IsNotNull(view.transform.Find("selection"));
        }

        [Test]
        public void PasteIsOneUndoStep()
        {
            var view = NewView();
            string before = WorkspaceJson.Save(view.Workspace);
            view.Select(view.Workspace.TopBlocks[0]);
            view.HandleShortcut(KeyCode.C, true, false);
            view.HandleShortcut(KeyCode.V, true, false);
            Assert.IsTrue(view.Undo());
            Assert.AreEqual(before, WorkspaceJson.Save(view.Workspace));
            Assert.IsFalse(view.CanUndo);
            Assert.IsTrue(view.Redo());
            Assert.AreEqual(2, Count(view, "one"));
        }

        [Test]
        public void CutRemovesTheBlockClosesTheGapAndPasteBringsItBack()
        {
            var view = NewView();
            view.Select(view.Workspace.TopBlocks[0]);
            Assert.IsTrue(view.HandleShortcut(KeyCode.X, true, false));
            Assert.AreEqual(0, Count(view, "one"));
            Assert.AreEqual(1, Count(view, "two"));
            Assert.IsNull(view.SelectedBlock);

            Assert.IsTrue(view.HandleShortcut(KeyCode.V, true, false));
            Assert.AreEqual(1, Count(view, "one"));
            Assert.IsTrue(view.Undo());                      // undo the paste
            Assert.AreEqual(0, Count(view, "one"));
            Assert.IsTrue(view.Undo());                      // undo the cut
            Assert.AreEqual(1, Count(view, "one"));
        }

        [Test]
        public void ThePasteWorksInAnotherWorkspace()
        {
            var view = NewView();
            view.Select(view.Workspace.TopBlocks[0]);
            view.CopySelected();
            view.SetWorkspace(StandardBlockSet.Load("<xml xmlns=\"https://developers.google.com/blockly/xml\"></xml>"));
            Assert.IsTrue(view.Paste());
            Assert.AreEqual(1, Count(view, "one"));
        }

        [Test]
        public void NothingHappensWithoutASelectionOrClipboard()
        {
            var view = NewView();
            Assert.IsFalse(view.HandleShortcut(KeyCode.C, true, false));
            Assert.IsFalse(view.HandleShortcut(KeyCode.X, true, false));
            Assert.IsFalse(view.HandleShortcut(KeyCode.V, true, false));
            Assert.IsFalse(view.HandleShortcut(KeyCode.V, false, false), "V alone is just a letter");
            Assert.IsFalse(view.CanUndo);
        }

        [Test]
        public void ShortcutsAreIgnoredWhileTyping()
        {
            var view = NewView();
            view.Select(view.Workspace.TopBlocks[0]);
            var t = view.Workspace.AllBlocks().First(b => b.Type == "text");
            Assert.IsTrue(view.BeginFieldEdit(t, t.GetField("TEXT")));
            Assert.IsFalse(view.HandleShortcut(KeyCode.C, true, false));
            Assert.IsFalse(WorkspaceView.HasClipboard);
            view.CancelFieldEdit();
        }
    }
}
