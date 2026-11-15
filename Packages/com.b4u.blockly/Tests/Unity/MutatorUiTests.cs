using System.Linq;
using Blockly.Editing;
using NUnit.Framework;
using UnityEngine;

namespace Blockly.UI.Tests
{
    /// <summary>The gear and the mutator bubble of if, join and list blocks.</summary>
    public class MutatorUiTests
    {
        GameObject root;

        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        const string Xml =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<block type=\"controls_if\" x=\"20\" y=\"20\"><value name=\"IF0\"><block type=\"logic_boolean\"><field name=\"BOOL\">TRUE</field></block></value>" +
            "<statement name=\"DO0\"><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">one</field></block></value></block></statement></block>" +
            "<block type=\"text_print\" x=\"20\" y=\"220\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">two</field></block></value></block></xml>";

        WorkspaceView NewView(Vector2? viewportSize = null)
        {
            root = new GameObject("Canvas", typeof(Canvas));
            Transform parent = root.transform;
            if (viewportSize.HasValue)
            {
                var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
                viewport.SetParent(root.transform, false);
                viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
                viewport.sizeDelta = viewportSize.Value;
                parent = viewport;
            }
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(parent, false);
            var vrt = (RectTransform)go.transform;
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0, 1);       // like the real view: its top-left corner is the workspace origin
            vrt.anchoredPosition = Vector2.zero;
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            return view;
        }

        static Block If(WorkspaceView v) => v.Workspace.TopBlocks.First(b => b.Type == "controls_if");
        static Block Print(WorkspaceView v) => v.Workspace.TopBlocks.First(b => b.Type == "text_print");
        static Transform Gear(WorkspaceView v, Block b) => v.transform.Cast<Transform>().First(t => t.name == b.Type).Find(WorkspaceView.MutatorIconName);
        static void Click(MutatorBubble bubble, string button) => bubble.Buttons[button].onClick.Invoke();

        [Test]
        public void OnlyTheMutableBlocksHaveAGear()
        {
            var v = NewView();
            Assert.IsNotNull(Gear(v, If(v)));
            Assert.IsNull(Gear(v, Print(v)));
            Assert.IsNotNull(Gear(v, If(v)).GetComponent<MutatorIconTarget>());
        }

        [Test]
        public void TheGearOpensAndClosesTheBubbleAndAnIfListsItsParts()
        {
            var v = NewView();
            Assert.IsTrue(v.ToggleMutatorBubble(If(v)));
            var bubble = v.MutatorBubbleOf(If(v));
            Assert.IsNotNull(bubble);
            CollectionAssert.Contains(bubble.Buttons.Keys, "add else if");
            CollectionAssert.Contains(bubble.Buttons.Keys, "add else");
            Assert.IsTrue(v.ToggleMutatorBubble(If(v)));
            Assert.IsNull(v.MutatorBubbleOf(If(v)));
        }

        [Test]
        public void TheAddButtonsChangeTheBlockAndTheBubbleFollowsTheNewShape()
        {
            var v = NewView();
            var b = If(v);
            v.ToggleMutatorBubble(b);
            Click(v.MutatorBubbleOf(b), "add else if");
            Assert.AreEqual(1, MutatorEditing.ElseIfCount(b));
            Assert.IsNotNull(v.MutatorBubbleOf(b), "the bubble stays open");
            CollectionAssert.Contains(v.MutatorBubbleOf(b).Buttons.Keys, "remove else if 1");
            Click(v.MutatorBubbleOf(b), "add else");
            Assert.IsTrue(MutatorEditing.HasElse(b));
            CollectionAssert.DoesNotContain(v.MutatorBubbleOf(b).Buttons.Keys, "add else", "an if has at most one else");
        }

        [Test]
        public void RemoveAndMoveButtonsWorkAndTheEdgesAreDisabled()
        {
            var v = NewView();
            var b = If(v);
            v.ToggleMutatorBubble(b);
            Click(v.MutatorBubbleOf(b), "add else if"); Click(v.MutatorBubbleOf(b), "add else if");
            var bubble = v.MutatorBubbleOf(b);
            Assert.IsFalse(bubble.Buttons["up else if 1"].interactable, "the first else if cannot go up");
            Assert.IsTrue(bubble.Buttons["down else if 1"].interactable);
            Assert.IsFalse(bubble.Buttons["down else if 2"].interactable);
            b.GetInput("DO2").Connection.Connect(WorkspaceEditing.Spawn(v.Workspace, new System.Collections.Generic.Dictionary<string, object> { { "type", "text_print" } }).Previous);
            var marker = b.GetInputTargetBlock("DO2");
            Click(bubble, "up else if 2");
            Assert.AreSame(marker, b.GetInputTargetBlock("DO1"), "the body moved up with its part");
            Click(v.MutatorBubbleOf(b), "remove else if 1");
            Assert.AreEqual(1, MutatorEditing.ElseIfCount(b));
        }

        [Test]
        public void EachButtonIsOneUndoStepAndUndoGivesTheOldShapeBack()
        {
            var v = NewView();
            var b = If(v);
            v.ToggleMutatorBubble(b);
            Click(v.MutatorBubbleOf(b), "add else if");
            Click(v.MutatorBubbleOf(b), "add else");
            v.Undo();
            Assert.IsFalse(MutatorEditing.HasElse(If(v)));
            Assert.AreEqual(1, MutatorEditing.ElseIfCount(If(v)));
            v.Undo();
            Assert.AreEqual(0, MutatorEditing.ElseIfCount(If(v)));
        }

        [Test]
        public void TurningMutatorsOffHidesTheGearAndTheBubbleButKeepsTheShape()
        {
            var v = NewView();
            var b = If(v);
            v.ToggleMutatorBubble(b);
            Click(v.MutatorBubbleOf(b), "add else");
            v.MutatorsEnabled = false;
            Assert.IsNull(Gear(v, If(v)));
            Assert.AreEqual(0, v.MutatorBubbles.Count);
            Assert.IsFalse(v.ToggleMutatorBubble(If(v)));
            Assert.IsTrue(MutatorEditing.HasElse(If(v)), "the shape stays");
            v.MutatorsEnabled = true;
            Assert.IsNotNull(Gear(v, If(v)));
        }

        [Test]
        public void ABubbleStaysInsideTheVisibleArea()
        {
            var v = NewView(new Vector2(260, 200));
            v.ToggleMutatorBubble(If(v));
            var rt = (RectTransform)v.MutatorBubbleOf(If(v)).transform;
            Assert.GreaterOrEqual(rt.anchoredPosition.x, 0f);
            Assert.LessOrEqual(rt.anchoredPosition.x + rt.sizeDelta.x, 260f);
            Assert.GreaterOrEqual(-rt.anchoredPosition.y, 0f);
        }

        [Test]
        public void TheBubbleGoesBelowAnOpenCommentBubble()
        {
            var v = NewView(new Vector2(3000, 3000));                       // room enough that nothing is pulled aside
            v.AddComment(If(v));
            v.ToggleMutatorBubble(If(v));
            var comment = (RectTransform)v.BubbleOf(If(v)).transform;
            var mutator = (RectTransform)v.MutatorBubbleOf(If(v)).transform;
            Assert.Less(comment.anchoredPosition.y, 1000f);
            Assert.Greater(comment.anchoredPosition.y, mutator.anchoredPosition.y, "the mutator bubble is lower on screen (y grows downward, anchored y falls)");
        }
    }
}
