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
    /// <summary>Dropdown menus, variable menus and the dialogs behind them.</summary>
    public class DropdownAndVariableTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            foreach (var g in Object.FindObjectsByType<DropdownMenu>(FindObjectsSortMode.None)) Object.DestroyImmediate(g.gameObject);
            foreach (var g in Object.FindObjectsByType<DialogView>(FindObjectsSortMode.None)) Object.DestroyImmediate(g.gameObject);
        }

        // a = 6 + 3 ; print a ;   plus a free "get b" block
        const string Program =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<variables><variable id=\"va\">a</variable><variable id=\"vb\">b</variable></variables>" +
            "<block type=\"variables_set\" x=\"20\" y=\"20\"><field name=\"VAR\" id=\"va\">a</field><value name=\"VALUE\"><block type=\"math_arithmetic\"><field name=\"OP\">ADD</field>" +
            "<value name=\"A\"><block type=\"math_number\"><field name=\"NUM\">6</field></block></value><value name=\"B\"><block type=\"math_number\"><field name=\"NUM\">3</field></block></value></block></value>" +
            "<next><block type=\"text_print\"><value name=\"TEXT\"><block type=\"variables_get\"><field name=\"VAR\" id=\"va\">a</field></block></value></block></next></block>" +
            "<block type=\"variables_get\" x=\"20\" y=\"220\"><field name=\"VAR\" id=\"vb\">b</field></block></xml>";

        WorkspaceView NewView(string xml = Program)
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load(xml));
            return view;
        }

        static Block Of(WorkspaceView v, string type) => v.Workspace.AllBlocks().First(b => b.Type == type);
        static string Output(Workspace ws)
        {
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion(50000);
            Assert.IsNull(it.Error);
            return string.Join(",", lines);
        }
        static bool ShowsText(WorkspaceView v, string text) => v.GetComponentsInChildren<TMP_Text>().Any(t => t.text == text);
        static FieldClickTarget Target(WorkspaceView v, Block b, string field)
            => v.GetComponentsInChildren<FieldClickTarget>().First(t => t.Block == b && t.Field.Name == field);

        static void Click(GameObject go)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            try { ExecuteEvents.Execute(go, new PointerEventData(es) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler); }
            finally { Object.DestroyImmediate(es.gameObject); }
        }

        // ---------------------------------------------------------------- operator menu
        [Test]
        public void ClickingADropdownOpensAMenuListingEveryChoice()
        {
            var view = NewView();
            var arith = Of(view, "math_arithmetic");
            Block started = null; view.FieldEditStarted += b => started = b;

            Click(Target(view, arith, "OP").gameObject);

            var menu = view.ActiveMenu;
            Assert.NotNull(menu);
            Assert.AreSame(arith, started, "a running program stops when a menu opens");
            Assert.AreEqual(new[] { "+", "-", "×", "÷", "^" }, menu.Items.Select(i => i.Label).ToArray());
            Assert.AreEqual(new[] { true, false, false, false, false }, menu.Items.Select(i => i.Selected).ToArray());
            Assert.AreEqual(5, menu.ItemButtons.Count);
            Assert.AreSame(root.transform, menu.transform.parent, "lives under the root canvas, outside the workspace's mask");
            Assert.IsTrue(menu.GetComponent<Canvas>().overrideSorting);
            Assert.Greater(menu.GetComponent<Canvas>().sortingOrder, 100);
            Assert.GreaterOrEqual(menu.Panel.sizeDelta.x, view.Layout.ByBlock[arith].Fields.First(f => f.Field.Name == "OP").W, "at least as wide as the field");
        }

        [Test]
        public void TheMenuOpensBelowTheFieldItBelongsToAndStaysOnScreen()
        {
            var view = NewView();
            var arith = Of(view, "math_arithmetic");
            var box = view.Layout.ByBlock[arith].Fields.First(f => f.Field.Name == "OP");
            view.ShowDropdown(arith, arith.GetField("OP"));
            var panel = view.ActiveMenu.Panel;
            Assert.AreEqual(0f, panel.pivot.x); Assert.AreEqual(1f, panel.pivot.y, "anchored by its top-left corner");

            // positions in the root canvas's own coordinates
            var canvasRt = (RectTransform)root.transform;
            var bounds = canvasRt.rect;
            var vr = ((RectTransform)view.transform).rect;
            var field = canvasRt.InverseTransformPoint(view.transform.TransformPoint(new Vector3(vr.xMin + box.X, vr.yMax - (box.Y + box.H), 0)));
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            float left = canvasRt.InverseTransformPoint(corners[0]).x, right = canvasRt.InverseTransformPoint(corners[2]).x;
            float top = canvasRt.InverseTransformPoint(corners[1]).y, bottom = canvasRt.InverseTransformPoint(corners[0]).y;
            float width = right - left, height = top - bottom;

            bool usableCanvas = bounds.width >= 1f && bounds.height >= 1f;
            bool roomRight = field.x + width <= bounds.xMax, roomBelow = field.y - height >= bounds.yMin;
            if (!usableCanvas || (roomRight && roomBelow))
            {
                Assert.AreEqual(field.x, left, 0.5f, "left edge lines up with the field");
                Assert.AreEqual(field.y, top, 0.5f, "the menu's top edge touches the field's bottom edge");
            }
            else
            {
                // not enough room (the test runner's 'screen' can be tiny): it must have been moved to stay inside the canvas
                if (!roomRight && width <= bounds.width) { Assert.GreaterOrEqual(left, bounds.xMin - 0.5f); Assert.LessOrEqual(right, bounds.xMax + 0.5f); }
                if (!roomBelow && height <= bounds.height) Assert.LessOrEqual(top, bounds.yMax + 0.5f);
            }
        }

        [Test]
        public void PickingAnItemChangesTheBlockRedrawsAndClosesTheMenu()
        {
            var view = NewView();
            var arith = Of(view, "math_arithmetic");
            int edits = 0; view.WorkspaceEdited += () => edits++;
            Assert.AreEqual("9", Output(view.Workspace));

            view.ShowDropdown(arith, arith.GetField("OP"));
            view.ActiveMenu.Pick(2);                                   // ×

            Assert.AreEqual("MULTIPLY", arith.GetFieldValue("OP"));
            Assert.IsNull(view.ActiveMenu, "closed");
            Assert.AreEqual(1, edits);
            Assert.IsTrue(ShowsText(view, "×"), "the block now shows the new operator");
            Assert.AreEqual("18", Output(view.Workspace));
        }

        [Test]
        public void ClickingOutsideTheMenuClosesItWithoutChanging()
        {
            var view = NewView();
            var arith = Of(view, "math_arithmetic");
            int edits = 0; view.WorkspaceEdited += () => edits++;
            view.ShowDropdown(arith, arith.GetField("OP"));
            var menu = view.ActiveMenu;
            menu.transform.Find("Blocker").GetComponent<Button>().onClick.Invoke();
            Assert.IsNull(view.ActiveMenu);
            Assert.IsFalse(menu.IsOpen);
            Assert.AreEqual("ADD", arith.GetFieldValue("OP"));
            Assert.AreEqual(0, edits);
        }

        [Test]
        public void PickingTheValueAlreadyChosenIsNotAnEdit()
        {
            var view = NewView();
            var arith = Of(view, "math_arithmetic");
            int edits = 0; view.WorkspaceEdited += () => edits++;
            view.ShowDropdown(arith, arith.GetField("OP"));
            view.ActiveMenu.Pick(0);
            Assert.AreEqual(0, edits);
        }

        [Test]
        public void StartingADragClosesTheMenu()
        {
            var view = NewView();
            var arith = Of(view, "math_arithmetic");
            view.ShowDropdown(arith, arith.GetField("OP"));
            var print = Of(view, "text_print");
            var box = view.Layout.ByBlock[print];
            view.BeginDrag(print, new Vector2(box.X + 4, box.Y + 4));
            Assert.IsNull(view.ActiveMenu);
            view.CancelDrag();
        }

        [Test]
        public void ReplacingTheWorkspaceClosesTheMenu()
        {
            var view = NewView();
            var arith = Of(view, "math_arithmetic");
            view.ShowDropdown(arith, arith.GetField("OP"));
            view.SetWorkspace(StandardBlockSet.Load(Program));
            Assert.IsNull(view.ActiveMenu);
        }

        [Test]
        public void ChangingAnIndexModeThroughTheMenuReshapesTheBlockOnScreen()
        {
            var view = NewView("<xml xmlns=\"https://developers.google.com/blockly/xml\"><block type=\"lists_getIndex\" x=\"20\" y=\"20\"><mutation statement=\"false\" at=\"true\"></mutation>" +
                               "<field name=\"MODE\">GET</field><field name=\"WHERE\">FROM_START</field></block></xml>");
            var get = Of(view, "lists_getIndex");
            var conn = view.Layout.ByBlock[get].Connections;
            Assert.IsTrue(conn.ContainsKey(get.GetInput("AT").Connection));
            int slotsBefore = view.Layout.ByBlock[get].Slots.Count;

            view.ApplyFieldValue(get, get.GetField("WHERE"), "FIRST");
            Assert.IsNull(get.GetInput("AT"));
            Assert.Less(view.Layout.ByBlock[get].Slots.Count, slotsBefore, "one slot fewer is drawn");
            Assert.IsTrue(ShowsText(view, "first"));
        }

        // ---------------------------------------------------------------- variable menu
        [Test]
        public void TheVariableMenuOffersEveryVariableRenameAndDelete()
        {
            var view = NewView();
            var set = Of(view, "variables_set");
            view.ShowDropdown(set, set.GetField("VAR"));
            Assert.AreEqual(new[] { "a", "b", "Rename variable...", "Delete the \"a\" variable" }, view.ActiveMenu.Items.Select(i => i.Label).ToArray());
            view.ActiveMenu.Pick(1);                                    // b
            Assert.AreEqual("vb", set.GetFieldValue("VAR"));
            Assert.AreEqual("undefined", Output(view.Workspace), "the set now assigns b, so 'print a' reads a variable nobody set (JS: undefined)");
        }

        [Test]
        public void RenamingAVariableThroughTheDialogUpdatesEverythingOnScreen()
        {
            var view = NewView();
            var set = Of(view, "variables_set");
            int edits = 0; view.WorkspaceEdited += () => edits++;
            view.ShowDropdown(set, set.GetField("VAR"));
            view.ActiveMenu.Pick(2);                                    // Rename variable...

            var d = view.ActiveDialog;
            Assert.NotNull(d);
            Assert.IsTrue(d.IsPrompt);
            StringAssert.Contains("a", d.Title.text);
            Assert.AreEqual("a", d.Input.text, "pre-filled with the current name");

            Assert.IsFalse(d.Submit("B"), "b exists already (names are compared ignoring case)");
            StringAssert.Contains("already exists", d.ErrorText);
            Assert.IsTrue(d.IsOpen, "the dialog stays open so the user can fix it");
            Assert.IsFalse(d.Submit("   "));
            StringAssert.Contains("empty", d.ErrorText);

            Assert.IsTrue(d.Submit("total"));
            Assert.IsNull(view.ActiveDialog);
            Assert.AreEqual("total", view.Workspace.GetVariableById("va").Name);
            Assert.IsTrue(ShowsText(view, "total"));
            Assert.IsFalse(ShowsText(view, "a"), "no label still shows the old name");
            Assert.AreEqual(1, edits);
            Assert.AreEqual("9", Output(view.Workspace), "the program works exactly as before");
        }

        [Test]
        public void CancellingTheRenameKeepsTheName()
        {
            var view = NewView();
            view.RequestRenameVariable("va");
            view.ActiveDialog.Cancel();
            Assert.IsNull(view.ActiveDialog);
            Assert.AreEqual("a", view.Workspace.GetVariableById("va").Name);
        }

        [Test]
        public void DeletingAVariableUsedOnceHappensImmediately()
        {
            var view = NewView();
            Block started = null; view.FieldEditStarted += b => started = b ?? Of(view, "text_print");
            view.RequestDeleteVariable("vb");                           // one use: the free "get b" block
            Assert.IsNull(view.ActiveDialog, "no confirmation needed for a single use");
            Assert.IsNull(view.Workspace.GetVariableById("vb"));
            Assert.AreEqual(0, view.Workspace.AllBlocks().Count(b => b.Type == "variables_get" && b.GetFieldValue("VAR") == "vb"));
            Assert.NotNull(started, "a running program is stopped");
        }

        [Test]
        public void DeletingAVariableUsedSeveralTimesAsksFirst()
        {
            var view = NewView();
            int before = view.Workspace.AllBlocks().Count();
            view.RequestDeleteVariable("va");                           // the set and the get
            var d = view.ActiveDialog;
            Assert.NotNull(d);
            Assert.IsFalse(d.IsPrompt, "a confirmation, not a text prompt");
            StringAssert.Contains("2 uses", d.Message.text);

            d.Cancel();
            Assert.NotNull(view.Workspace.GetVariableById("va"));
            Assert.AreEqual(before, view.Workspace.AllBlocks().Count(), "cancelled: nothing deleted");

            view.RequestDeleteVariable("va");
            view.ActiveDialog.Confirm();
            Assert.IsNull(view.Workspace.GetVariableById("va"));
            Assert.AreEqual(0, view.Workspace.AllBlocks().Count(b => b.Type == "variables_set"));
            Assert.AreEqual(1, view.Workspace.AllBlocks().Count(b => b.Type == "text_print"), "the print stays, now with an empty input");
            Assert.IsFalse(ShowsText(view, "9"));
        }

        [Test]
        public void CreatingAVariableChecksTheNameAndReportsTheNewVariable()
        {
            var view = NewView();
            int edits = 0; view.WorkspaceEdited += () => edits++;
            Variable made = null;
            view.RequestCreateVariable(v => made = v);
            var d = view.ActiveDialog;
            StringAssert.Contains("New variable", d.Title.text);

            Assert.IsFalse(d.Submit(""));
            StringAssert.Contains("empty", d.ErrorText);
            Assert.IsFalse(d.Submit("A"));
            StringAssert.Contains("already exists", d.ErrorText);
            Assert.AreEqual(2, view.Workspace.Variables.Count);

            Assert.IsTrue(d.Submit("  high   score "));
            Assert.AreEqual(3, view.Workspace.Variables.Count);
            Assert.AreEqual("high score", made.Name, "whitespace is tidied");
            Assert.AreEqual(1, edits);
        }

        [Test]
        public void TheDialogHasWorkingButtons()
        {
            var view = NewView();
            view.RequestCreateVariable();
            var d = view.ActiveDialog;
            d.Input.SetTextWithoutNotify("clicked");
            d.OkButton.onClick.Invoke();
            Assert.IsNull(view.ActiveDialog);
            Assert.NotNull(view.Workspace.GetVariableByName("clicked"));

            view.RequestCreateVariable();
            view.ActiveDialog.CancelButton.onClick.Invoke();
            Assert.IsNull(view.ActiveDialog);
        }

        [Test]
        public void VariableFieldsInAFlyoutAreNotClickable()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.Spawner = new Stub();
            view.SetWorkspace(StandardBlockSet.Load(Program));
            Assert.AreEqual(0, view.GetComponentsInChildren<FieldClickTarget>().Length);
            var set = Of(view, "variables_set");
            Assert.IsFalse(view.ShowDropdown(set, set.GetField("VAR")));
        }

        sealed class Stub : IBlockSpawner { public bool TrySpawn(Block b, PointerEventData e, WorkspaceView v) => false; }
    }
}
