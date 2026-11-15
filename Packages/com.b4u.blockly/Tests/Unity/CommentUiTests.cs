using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Blockly.UI.Tests
{
    /// <summary>Comments on blocks in the view: the menu entry, the icon, the bubble, undo, the switch that turns the feature off.</summary>
    public class CommentUiTests
    {
        GameObject root;

        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        const string Xml =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<block type=\"text_print\" x=\"20\" y=\"20\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">one</field></block></value></block></xml>";

        WorkspaceView NewView()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            return view;
        }

        static Block Print(WorkspaceView v) => v.Workspace.TopBlocks[0];
        static Transform Icon(WorkspaceView v, Block b) => v.transform.Cast<Transform>().First(t => t.name == b.Type).Find(WorkspaceView.CommentIconName);

        [Test]
        public void TheMenuOffersAddCommentThenRemoveComment()
        {
            var v = NewView();
            v.ShowBlockMenu(Print(v), new Vector2(100, 100));
            CollectionAssert.Contains(Object.FindFirstObjectByType<DropdownMenu>().Items.Select(i => i.Label), "Add comment");
            Assert.IsTrue(v.ChooseBlockMenuItem(Print(v), WorkspaceView.MenuAddComment));
            v.ShowBlockMenu(Print(v), new Vector2(100, 100));
            var labels = Object.FindFirstObjectByType<DropdownMenu>().Items.Select(i => i.Label).ToList();
            CollectionAssert.Contains(labels, "Remove comment");
            CollectionAssert.DoesNotContain(labels, "Add comment");
        }

        [Test]
        public void AddingACommentShowsTheIconAndAnOpenBubbleAndRemovingItTakesThemAway()
        {
            var v = NewView();
            Assert.IsNull(Icon(v, Print(v)), "no icon without a comment");
            Assert.IsTrue(v.AddComment(Print(v)));
            Assert.AreEqual("", Print(v).Comment);
            Assert.IsNotNull(Icon(v, Print(v)));
            Assert.IsNotNull(v.BubbleOf(Print(v)));
            Assert.IsTrue(v.RemoveComment(Print(v)));
            Assert.IsNull(Print(v).Comment);
            Assert.IsNull(Icon(v, Print(v)));
            Assert.AreEqual(0, v.CommentBubbles.Count);
        }

        [Test]
        public void TheIconOpensAndClosesTheBubble()
        {
            var v = NewView();
            v.AddComment(Print(v));
            Assert.IsTrue(v.ToggleCommentBubble(Print(v)));
            Assert.IsFalse(Print(v).CommentPinned);
            Assert.IsNull(v.BubbleOf(Print(v)));
            Assert.IsNotNull(Icon(v, Print(v)), "the icon stays");
            Assert.IsTrue(v.ToggleCommentBubble(Print(v)));
            Assert.IsNotNull(v.BubbleOf(Print(v)));
            Assert.IsNotNull(Icon(v, Print(v)).GetComponent<CommentIconTarget>());
        }

        [Test]
        public void TypingGoesIntoTheBlockAndTheWholeEditIsOneUndoStep()
        {
            var v = NewView();
            v.AddComment(Print(v));
            var bubble = v.BubbleOf(Print(v));
            bubble.Input.onSelect.Invoke("");                                  // the field got focus
            bubble.Input.text = "a";
            bubble.Input.text = "a note";
            Assert.AreEqual("a note", Print(v).Comment);
            bubble.Flush();                                                    // focus left
            Assert.IsTrue(v.CanUndo);
            v.Undo();
            Assert.AreEqual("", Print(v).Comment, "undo takes the whole typing back at once");
            v.Undo();
            Assert.IsNull(Print(v).Comment, "and the step before it was adding the comment");
        }

        [Test]
        public void TurningCommentsOffHidesEverythingButKeepsTheText()
        {
            var v = NewView();
            v.AddComment(Print(v));
            Print(v).Comment = "kept";
            v.CommentsEnabled = false;
            Assert.IsNull(Icon(v, Print(v)));
            Assert.AreEqual(0, v.CommentBubbles.Count);
            Assert.IsFalse(v.AddComment(Print(v)));
            v.ShowBlockMenu(Print(v), new Vector2(100, 100));
            CollectionAssert.DoesNotContain(Object.FindFirstObjectByType<DropdownMenu>().Items.Select(i => i.Label), "Remove comment");
            Assert.AreEqual("kept", Print(v).Comment);
            v.CommentsEnabled = true;
            Assert.IsNotNull(Icon(v, Print(v)));
            Assert.AreEqual("kept", Print(v).Comment);
        }

        [Test]
        public void TheBubbleStaysInsideTheVisibleArea()
        {
            var box = new Layout.BlockBox { X = 380, Y = 10, W = 60, H = 24 };
            var visible = new Rect(0, 0, 400, 300);
            var p = CommentBubble.Place(box, 440, 200, 96, visible, true);
            Assert.GreaterOrEqual(p.x, visible.xMin);
            Assert.LessOrEqual(p.x + 200, visible.xMax);
            Assert.GreaterOrEqual(p.y, visible.yMin);
            Assert.LessOrEqual(p.y + 96, visible.yMax);
            var below = CommentBubble.Place(new Layout.BlockBox { X = 10, Y = 290, W = 60, H = 24 }, 70, 200, 96, visible, true);
            Assert.LessOrEqual(below.y + 96, visible.yMax, "a block at the bottom edge still gets a bubble that fits");
        }

        [Test]
        public void TheBubbleIsBesideTheBlockAndWhatIsPluggedIntoItNotOverIt()
        {
            var v = NewView();
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();      // a big visible area: nothing is pulled aside
            viewport.SetParent(root.transform, false);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
            viewport.sizeDelta = new Vector2(3000, 3000);
            v.transform.SetParent(viewport, false);
            v.AddComment(Print(v));
            var bubble = (RectTransform)v.BubbleOf(Print(v)).transform;
            var textBox = v.Layout.ByBlock[Print(v).GetInputTargetBlock("TEXT")];
            Assert.GreaterOrEqual(bubble.anchoredPosition.x, textBox.X + textBox.W, "right of the block that is plugged in");
        }

        [Test]
        public void ADraggedBlocksBubbleIsLeftOutUntilItIsDropped()
        {
            var v = NewView();
            v.AddComment(Print(v));
            Assert.IsTrue(v.BeginDrag(Print(v), new Vector2(30, 30)));
            Assert.AreEqual(0, v.CommentBubbles.Count);
        }
    }
}
