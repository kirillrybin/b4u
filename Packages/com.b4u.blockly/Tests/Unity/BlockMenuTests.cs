using System.Linq;
using Blockly.Editing;
using NUnit.Framework;
using UnityEngine;

namespace Blockly.UI.Tests
{
    /// <summary>The menu of a block (right click, long press): duplicate, copy, cut, disable / enable, delete.</summary>
    public class BlockMenuTests
    {
        GameObject root;

        [SetUp] public void SetUp() => WorkspaceView.Clipboard = null;

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
        public void TheMenuListsTheActionsAndOffersDisableOnlyForStatements()
        {
            var view = NewView();
            var print = view.Workspace.TopBlocks[0];
            Assert.IsTrue(view.ShowBlockMenu(print, new Vector2(100, 100)));
            Assert.AreSame(print, view.SelectedBlock, "the block is selected");
            var menu = Object.FindFirstObjectByType<DropdownMenu>();
            CollectionAssert.AreEqual(new[] { "Duplicate", "Copy", "Cut", "Disable block", "Delete" }, menu.Items.Select(i => i.Label));

            var text = print.GetInputTargetBlock("TEXT");
            view.ShowBlockMenu(text, new Vector2(100, 100));
            menu = Object.FindFirstObjectByType<DropdownMenu>();
            CollectionAssert.AreEqual(new[] { "Duplicate", "Copy", "Cut", "Delete" }, menu.Items.Select(i => i.Label), "an expression block always runs with its parent");
        }

        [Test]
        public void DuplicateAddsACopyOfTheBlockWithoutTouchingTheClipboardAndUndoRemovesIt()
        {
            var view = NewView();
            var first = view.Workspace.TopBlocks[0];
            view.ShowBlockMenu(first, Vector2.zero);
            Assert.IsTrue(view.ChooseBlockMenuItem(first, WorkspaceView.MenuDuplicate));
            Assert.AreEqual(2, Count(view, "one"));
            Assert.AreEqual(1, Count(view, "two"), "the follower is not duplicated");
            Assert.AreNotSame(first, view.SelectedBlock);
            Assert.IsFalse(WorkspaceView.HasClipboard);
            Assert.IsTrue(view.Undo());
            Assert.AreEqual(1, Count(view, "one"));
        }

        [Test]
        public void DisableAndEnableToggleTheBlockAndItsContentsLookDisabled()
        {
            var view = NewView();
            var first = view.Workspace.TopBlocks[0];
            var second = first.NextBlock;
            var text = first.GetInputTargetBlock("TEXT");
            Assert.IsTrue(view.ChooseBlockMenuItem(first, WorkspaceView.MenuDisable));
            Assert.IsTrue(first.Disabled);
            Assert.IsTrue(BlockStyle.IsDisabled(text), "its inputs look disabled");
            Assert.IsFalse(BlockStyle.IsDisabled(second), "the block after it is not part of it");
            Assert.IsTrue(view.Undo());
            first = view.Workspace.TopBlocks[0];                      // undo brings back new block objects
            Assert.IsFalse(first.Disabled);
            second = first.NextBlock; text = first.GetInputTargetBlock("TEXT");
            Assert.IsTrue(view.ChooseBlockMenuItem(second, WorkspaceView.MenuDisable));
            Assert.IsTrue(view.ChooseBlockMenuItem(second, WorkspaceView.MenuDisable), "the same entry enables it again");
            Assert.IsFalse(second.Disabled);
            Assert.IsFalse(view.ChooseBlockMenuItem(text, WorkspaceView.MenuDisable), "an expression cannot be disabled");
        }

        [Test]
        public void CopyCutAndDeleteWorkLikeTheShortcuts()
        {
            var view = NewView();
            var first = view.Workspace.TopBlocks[0];
            Assert.IsTrue(view.ChooseBlockMenuItem(first, WorkspaceView.MenuCopy));
            Assert.IsTrue(WorkspaceView.HasClipboard);
            Assert.AreEqual(1, Count(view, "one"));
            Assert.IsTrue(view.ChooseBlockMenuItem(first, WorkspaceView.MenuCut));
            Assert.AreEqual(0, Count(view, "one"));
            var second = view.Workspace.AllBlocks().First(b => b.GetFieldValue("TEXT") == "two").Parent;
            Assert.IsTrue(view.ChooseBlockMenuItem(second, WorkspaceView.MenuDelete));
            Assert.AreEqual(0, Count(view, "two"));
        }

        [Test]
        public void AHeldPressOpensTheMenuAndAReleaseOrDragBeforeThatDoesNot()
        {
            var view = NewView();
            var first = view.Workspace.TopBlocks[0];
            view.BeginPress(first, new Vector2(50, 50));
            view.CheckLongPress(Time.unscaledTime + 0.1f);
            Assert.IsNull(Object.FindFirstObjectByType<DropdownMenu>(), "not yet");
            view.EndPress();
            view.CheckLongPress(Time.unscaledTime + 5f);
            Assert.IsNull(Object.FindFirstObjectByType<DropdownMenu>(), "released in time");

            view.BeginPress(first, new Vector2(50, 50));
            view.CheckLongPress(Time.unscaledTime + WorkspaceView.LongPressSeconds + 0.1f);
            Assert.IsNotNull(Object.FindFirstObjectByType<DropdownMenu>());
        }
    }
}
