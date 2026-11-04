using System.Linq;
using Blockly.Editing;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>The toolbox and its flyout in the saved Playground scene, driven with real pointer events.</summary>
    public class ToolboxViewTests
    {
        const string ScenePath = "Assets/Playground/Playground.unity";

        PlaygroundController c;
        ToolboxView tb;
        WorkspaceView main;
        EventSystem es;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            c = Object.FindFirstObjectByType<PlaygroundController>();
            Assert.IsTrue(c.ShowByName("text"), "program with print blocks");
            tb = c.Toolbox;
            Assert.NotNull(tb, "scene has a ToolboxView");
            main = c.View;
            es = Object.FindFirstObjectByType<EventSystem>();
        }

        int IndexOf(string category) => tb.Definition.Categories.FindIndex(x => x.Name == category);

        BlockDragTarget FlyoutBlock(string type)
            => tb.FlyoutView.GetComponentsInChildren<BlockDragTarget>().First(t => !t.Block.IsShadow && t.Block.Type == type);

        PointerEventData PressOnFlyoutBlock(BlockDragTarget target, float dx = 6, float dy = 6)
        {
            var box = tb.FlyoutView.Layout.ByBlock[target.Block];
            var screen = tb.FlyoutView.WorkspaceToScreen(new Vector2(box.X + dx, box.Y + dy), null);
            return new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = screen, pressPosition = screen };
        }

        Vector2 ToolboxCenterOnScreen()
        {
            var rt = (RectTransform)tb.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        }

        // ---------------------------------------------------------------- categories and flyout
        [Test]
        public void OneButtonPerCategoryWithItsName()
        {
            tb.SelectCategory(0); tb.CloseFlyout();
            var names = tb.transform.Cast<Transform>().First(t => t.name == "Categories").Cast<Transform>().Select(t => t.name).ToArray();
            Assert.AreEqual(new[] { "Logic", "Loops", "Math", "Text", "Lists", "Variables", "Functions" }, names);
            Assert.IsFalse(tb.IsFlyoutOpen);
        }

        [Test]
        public void SelectingACategoryOpensTheFlyoutWithItsBlocksAndTogglingClosesIt()
        {
            tb.SelectCategory(IndexOf("Loops"));
            Assert.IsTrue(tb.IsFlyoutOpen);
            var cat = tb.Definition.Categories[IndexOf("Loops")];
            Assert.AreEqual(cat.Blocks.Count, tb.FlyoutView.Workspace.TopBlocks.Count);
            Assert.AreEqual(tb.FlyoutView.Workspace.AllBlocks().Count(), tb.FlyoutView.Layout.Boxes.Count(b => !b.IsShadow));

            tb.ToggleCategory(IndexOf("Loops"));
            Assert.IsFalse(tb.IsFlyoutOpen, "clicking the open category again closes it");
            tb.ToggleCategory(IndexOf("Math"));
            Assert.IsTrue(tb.IsFlyoutOpen);
            Assert.AreEqual(IndexOf("Math"), tb.SelectedIndex);
        }

        [Test]
        public void EveryCategoryOpensWithoutErrors()
        {
            for (int i = 0; i < tb.Definition.Categories.Count; i++)
            {
                tb.SelectCategory(i);
                Assert.Greater(tb.FlyoutView.Layout.Boxes.Count, 0, tb.Definition.Categories[i].Name);
            }
        }

        [Test]
        public void ChangingProgramClosesTheFlyout()
        {
            tb.SelectCategory(0);
            c.Next();
            Assert.IsFalse(tb.IsFlyoutOpen);
        }

        [Test]
        public void PressingTheWorkspaceBackgroundClosesTheFlyout()
        {
            tb.SelectCategory(0);
            var dismiss = Object.FindFirstObjectByType<FlyoutDismiss>();
            Assert.NotNull(dismiss);
            dismiss.OnPointerDown(null);
            Assert.IsFalse(tb.IsFlyoutOpen);
        }

        // ---------------------------------------------------------------- dragging out of the flyout
        [Test]
        public void GrabbingAFlyoutBlockCreatesARealBlockInTheWorkspaceAndTakesOverTheDrag()
        {
            tb.SelectCategory(IndexOf("Math"));
            var grab = FlyoutBlock("math_arithmetic");
            var ped = PressOnFlyoutBlock(grab);
            int before = main.Workspace.AllBlocks().Count();
            int flyoutBefore = tb.FlyoutView.Workspace.AllBlocks().Count();

            ExecuteEvents.Execute(grab.gameObject, ped, ExecuteEvents.beginDragHandler);

            Assert.IsTrue(main.IsDragging);
            Assert.AreEqual("math_arithmetic", main.DraggedBlock.Type);
            Assert.AreSame(main.gameObject, ped.pointerDrag, "the drag now belongs to the main workspace");
            Assert.IsFalse(tb.IsFlyoutOpen, "the flyout gets out of the way");
            Assert.AreEqual(before + main.DraggedBlock.Descendants().Count(), main.Workspace.AllBlocks().Count(), "a new block exists (with its shadows excluded from the count)");
            Assert.IsTrue(main.Workspace.TopBlocks.Contains(main.DraggedBlock));
            Assert.AreEqual(flyoutBefore, tb.FlyoutView.Workspace.AllBlocks().Count(), "the flyout keeps its own copy");
            main.CancelDrag();
        }

        [Test]
        public void GrabbingAShadowInsideAFlyoutBlockGrabsTheWholeBlock()
        {
            tb.SelectCategory(IndexOf("Loops"));
            var shadowTarget = tb.FlyoutView.GetComponentsInChildren<BlockDragTarget>().First(t => t.Block.IsShadow);
            var top = shadowTarget.Block.ShadowOf.Owner;
            var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = Vector2.zero, pressPosition = Vector2.zero };
            ExecuteEvents.Execute(shadowTarget.gameObject, ped, ExecuteEvents.beginDragHandler);
            Assert.IsTrue(main.IsDragging);
            Assert.AreEqual(top.Type, main.DraggedBlock.Type);
            main.CancelDrag();
        }

        [Test]
        public void ADraggedBlockSnapsIntoTheProgramWhenDropped()
        {
            tb.SelectCategory(IndexOf("Math"));
            var grab = FlyoutBlock("math_number");
            var ped = PressOnFlyoutBlock(grab);
            var pressInMain = main.ScreenToWorkspace(ped.pressPosition, null);
            ExecuteEvents.Execute(grab.gameObject, ped, ExecuteEvents.beginDragHandler);
            var fresh = main.DraggedBlock;

            var print = main.Workspace.AllBlocks().First(b => b.Type == "text_print");
            var target = main.Layout.ByBlock[print].Connections[print.GetInput("TEXT").Connection];
            var mine = main.Layout.ByBlock[fresh].Connections[fresh.Output];
            ped.position = main.WorkspaceToScreen(pressInMain + new Vector2(target.X - mine.X + 3, target.Y - mine.Y + 3), null);
            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.dragHandler);
            Assert.IsNotNull(main.SnapCandidate, "within snapping distance");

            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.endDragHandler);
            Assert.IsFalse(main.IsDragging);
            Assert.AreSame(fresh, print.GetInputTargetBlock("TEXT"));
            Assert.AreEqual("math_number", print.GetInputTargetBlock("TEXT").Type);
        }

        [Test]
        public void DroppingOnTheToolboxDeletesTheNewBlock()
        {
            tb.SelectCategory(IndexOf("Logic"));
            var grab = FlyoutBlock("controls_if");
            var ped = PressOnFlyoutBlock(grab);
            int before = main.Workspace.AllBlocks().Count();
            bool hovered = false, left = false; int edits = 0;
            main.DeleteHoverChanged += over => { if (over) hovered = true; else left = true; };
            main.WorkspaceEdited += () => edits++;

            ExecuteEvents.Execute(grab.gameObject, ped, ExecuteEvents.beginDragHandler);
            Assert.AreEqual(before + 1, main.Workspace.AllBlocks().Count());

            ped.position = ToolboxCenterOnScreen();
            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.dragHandler);
            Assert.IsTrue(main.OverDeleteArea, "pointer is over the toolbox strip");
            var dim = main.GetComponentsInChildren<CanvasGroup>().FirstOrDefault();
            Assert.NotNull(dim); Assert.Less(dim.alpha, 1f, "dragged blocks look 'about to be thrown away'");

            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.endDragHandler);
            Assert.AreEqual(before, main.Workspace.AllBlocks().Count(), "the block was deleted, not placed");
            Assert.IsTrue(hovered && left, "hover state was announced and cleared");
            Assert.IsFalse(main.OverDeleteArea);
            Assert.AreEqual(1, edits);
        }

        [Test]
        public void DraggingAnExistingBlockOntoTheToolboxDeletesIt()
        {
            var victim = main.Workspace.TopBlocks.Last();
            int size = victim.Descendants().Count();
            int before = main.Workspace.AllBlocks().Count();
            var box = main.Layout.ByBlock[victim];
            var start = main.WorkspaceToScreen(new Vector2(box.X + 5, box.Y + 5), null);
            var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = start, pressPosition = start };
            var go = main.GetComponentsInChildren<BlockDragTarget>().First(t => t.Block == victim).gameObject;

            ExecuteEvents.Execute(go, ped, ExecuteEvents.beginDragHandler);
            ped.position = ToolboxCenterOnScreen();
            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.endDragHandler);

            Assert.AreEqual(before - size, main.Workspace.AllBlocks().Count());
            Assert.IsFalse(main.Workspace.TopBlocks.Contains(victim));
        }

        [Test]
        public void EscapeDuringADragFromTheToolboxLeavesNoBlockBehind()
        {
            tb.SelectCategory(IndexOf("Text"));
            var grab = FlyoutBlock("text_print");
            int before = main.Workspace.AllBlocks().Count();
            ExecuteEvents.Execute(grab.gameObject, PressOnFlyoutBlock(grab), ExecuteEvents.beginDragHandler);
            Assert.AreEqual(before + main.DraggedBlock.Descendants().Count(), main.Workspace.AllBlocks().Count());
            main.CancelDrag();
            Assert.AreEqual(before, main.Workspace.AllBlocks().Count());
        }

        [Test]
        public void ProceduresFromTheToolboxGetUniqueNamesAndAppearAsCalls()
        {
            for (int i = 0; i < 2; i++)
            {
                tb.SelectCategory(IndexOf("Functions"));
                var grab = FlyoutBlock("procedures_defnoreturn");
                var ped = PressOnFlyoutBlock(grab);
                ExecuteEvents.Execute(grab.gameObject, ped, ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.endDragHandler);   // drop in the air
            }
            var names = main.Workspace.AllBlocks().Where(ProcedureInfo.IsDefinition).Select(b => b.GetFieldValue("NAME")).ToArray();
            Assert.AreEqual(new[] { "do something", "do something2" }, names);

            tb.SelectCategory(IndexOf("Functions"));
            var calls = tb.FlyoutView.Workspace.TopBlocks.Where(b => b.Type == "procedures_callnoreturn").Select(ProcedureInfo.Name).ToArray();
            CollectionAssert.AreEquivalent(names, calls, "the flyout now offers a call for each definition");
        }

        [Test]
        public void ToolboxStripIsTheDeleteArea()
        {
            Assert.AreSame(tb.transform, main.DeleteArea);
        }
    }
}
