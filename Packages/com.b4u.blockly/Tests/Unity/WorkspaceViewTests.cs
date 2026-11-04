using System.IO;
using System.Linq;
using Blockly.Layout;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI.Tests
{
    /// <summary>Unity-only tests for the UGUI layer (the pure layout is covered by the shared EditMode tests).</summary>
    public class WorkspaceViewTests
    {
        GameObject root;

        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        static string Fixture(string name) => File.ReadAllText(Path.GetFullPath("Packages/com.b4u.blockly/Tests/EditMode/Fixtures/programs/" + name + ".json"));

        WorkspaceView NewView()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            return go.GetComponent<WorkspaceView>();
        }

        public static string[] Names => new[] { "loops", "lists", "procedures", "text_more", "if_else" };

        [TestCaseSource(nameof(Names))]
        public void BuildsOneObjectPerBlockWithAShapeAndAllFieldTexts(string name)
        {
            var view = NewView();
            view.SetWorkspace(StandardBlockSet.Load(Fixture(name)));

            Assert.Greater(view.Layout.Boxes.Count, 0);
            var shapes = view.GetComponentsInChildren<PolygonGraphic>(true);
            Assert.GreaterOrEqual(shapes.Length, view.Layout.Boxes.Count, "every block has a body polygon");

            int expectedTexts = view.Layout.Boxes.Sum(b => b.Fields.Count(f => !string.IsNullOrEmpty(f.Text)));
            int texts = view.GetComponentsInChildren<TMP_Text>(true).Count(t => t.gameObject.name == "text");
            Assert.AreEqual(expectedTexts, texts, "one TMP text per non-empty field");
        }

        [Test]
        public void RebuildReplacesTheOldObjectsInsteadOfAccumulating()
        {
            var view = NewView();
            var ws = StandardBlockSet.Load(Fixture("loops"));
            view.SetWorkspace(ws);
            int first = view.GetComponentsInChildren<PolygonGraphic>(true).Length;
            view.Rebuild(); view.Rebuild();
            Assert.AreEqual(first, view.GetComponentsInChildren<PolygonGraphic>(true).Length);
        }

        [Test]
        public void TextMeasurerGrowsWithTextAndAgreesWithTmp()
        {
            var view = NewView();
            view.SetWorkspace(StandardBlockSet.Load(Fixture("loops")));
            var template = view.GetComponentsInChildren<TMP_Text>(true).First(t => t.gameObject.name == "TextMeasure");
            var m = new TmpTextMeasurer(template);
            Assert.AreEqual(0f, m.Width(""));
            Assert.Greater(m.Width("hello"), m.Width("hi"));
            Assert.Greater(m.Width("hi"), 0f);
        }

        [Test]
        public void ClicksLandOnTheBlockShapeNotItsBoundingBox()
        {
            var view = NewView();
            view.SetWorkspace(StandardBlockSet.Load(Fixture("loops")));
            // the C-shaped repeat block: its empty inner area is inside the bounding box but not part of the shape
            var repeat = view.GetComponentsInChildren<PolygonGraphic>(true).First(g => g.gameObject.name == "controls_repeat_ext");
            var box = view.Layout.ByBlock.Values.First(b => b.Block.Type == "controls_repeat_ext");
            var local = new Vector2(box.X + 4, box.Y + 4);                                   // inside the top-left corner of the body
            Assert.IsTrue(Contains(repeat, local.x - box.X, local.y - box.Y));
            float openingY = box.H - Metrics.BottomBarHeight - 6;                        // just above the bottom bar
            Assert.IsFalse(Contains(repeat, box.W - 4, openingY), "inside the C's opening");
            Assert.IsTrue(Contains(repeat, box.W - 4, box.H - 4), "the bottom bar itself is part of the shape");
        }

        [Test]
        public void HighlightOutlinesTheBlockOnTopAndCanBeCleared()
        {
            var view = NewView();
            var ws = StandardBlockSet.Load(Fixture("loops"));
            view.SetWorkspace(ws);
            var target = ws.TopBlocks[0];

            view.SetHighlight(target);
            Assert.AreSame(target, view.HighlightedBlock);
            var h = view.transform.Cast<Transform>().Single(t => t.name == "highlight");
            Assert.IsTrue(h.gameObject.activeSelf);
            Assert.AreEqual(view.transform.childCount - 1, h.GetSiblingIndex(), "drawn above every block, children included");
            var box = view.Layout.ByBlock[target];
            Assert.AreEqual(box.Outline.Length, h.GetComponent<PolygonGraphic>().Polygon.Length, "uses the block's own outline");
            Assert.AreEqual(box.X, ((RectTransform)h).anchoredPosition.x, 1e-3f);
            Assert.AreEqual(-box.Y, ((RectTransform)h).anchoredPosition.y, 1e-3f);
            Assert.IsFalse(h.GetComponent<PolygonGraphic>().raycastTarget, "must not steal clicks");

            var other = ws.TopBlocks[0].NextBlock;
            view.SetHighlight(other);
            Assert.AreEqual(1, view.transform.Cast<Transform>().Count(t => t.name == "highlight"), "one highlight object, reused");
            Assert.AreEqual(view.Layout.ByBlock[other].X, ((RectTransform)h).anchoredPosition.x, 1e-3f);

            view.SetHighlight(null);
            Assert.IsNull(view.HighlightedBlock);
            Assert.IsFalse(h.gameObject.activeSelf);
        }

        [Test]
        public void RebuildDropsTheHighlight()
        {
            var view = NewView();
            var ws = StandardBlockSet.Load(Fixture("loops"));
            view.SetWorkspace(ws);
            view.SetHighlight(ws.TopBlocks[0]);
            view.Rebuild();
            Assert.IsNull(view.HighlightedBlock);
            Assert.AreEqual(0, view.transform.Cast<Transform>().Count(t => t.name == "highlight"));
        }

        [Test]
        public void HighlightingAnUnknownBlockClearsInsteadOfThrowing()
        {
            var view = NewView();
            view.SetWorkspace(StandardBlockSet.Load(Fixture("loops")));
            var stranger = StandardBlockSet.Load(Fixture("logic")).TopBlocks[0];
            Assert.DoesNotThrow(() => view.SetHighlight(stranger));
            Assert.IsNull(view.HighlightedBlock);
        }

        // ------------------------------------------------------------------ dragging
        const string TwoBlocks =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<block type=\"text_print\" x=\"20\" y=\"20\"></block>" +
            "<block type=\"text\" x=\"400\" y=\"200\"><field name=\"TEXT\">hi</field></block></xml>";

        (WorkspaceView view, Workspace ws, Block print, Block text) DragSetup()
        {
            var view = NewView();
            var ws = StandardBlockSet.Load(TwoBlocks);
            view.SetWorkspace(ws);
            return (view, ws, ws.TopBlocks[0], ws.TopBlocks[1]);
        }

        /// <summary>Workspace point to drag to so that the text block's output lands on print's TEXT input.</summary>
        static Vector2 PointOntoTextInput(WorkspaceView view, Block print, Block text, Vector2 grab)
        {
            var target = view.Layout.ByBlock[print].Connections[print.GetInput("TEXT").Connection];
            var mine = view.Layout.ByBlock[text].Connections[text.Output];
            return grab + new Vector2(target.X - mine.X, target.Y - mine.Y);
        }

        [Test]
        public void DragMovesTheObjectsShowsTheSnapMarkerAndDropConnects()
        {
            var (view, ws, print, text) = DragSetup();
            Block started = null; int edits = 0;
            view.BlockDragStarted += b => started = b;
            view.WorkspaceEdited += () => edits++;

            var box = view.Layout.ByBlock[text];
            var grab = new Vector2(box.X + 5, box.Y + 5);
            Assert.IsTrue(view.BeginDrag(text, grab));
            Assert.IsTrue(view.IsDragging);
            Assert.AreSame(text, started);
            Assert.IsNull(view.SnapCandidate);

            var textObject = view.GetComponentsInChildren<BlockDragTarget>().First(t => t.Block == text).transform;
            Assert.AreEqual(view.transform.childCount - 1, textObject.GetSiblingIndex(), "dragged block is drawn on top");

            view.DragTo(grab + new Vector2(-100, 40));                          // far from anything
            Assert.AreEqual(box.X - 100, ((RectTransform)textObject).anchoredPosition.x, 1e-3f);
            Assert.AreEqual(-(box.Y + 40), ((RectTransform)textObject).anchoredPosition.y, 1e-3f);
            Assert.IsNull(view.SnapCandidate);

            view.DragTo(PointOntoTextInput(view, print, text, grab));
            Assert.IsNotNull(view.SnapCandidate, "close to print's input");
            var marker = view.transform.Cast<Transform>().Single(t => t.name == "snap");
            Assert.IsTrue(marker.gameObject.activeSelf);
            var tp = view.SnapCandidate.Value.TargetPoint;
            Assert.AreEqual(tp.X, ((RectTransform)marker).anchoredPosition.x, 1e-3f);
            Assert.AreEqual(-tp.Y, ((RectTransform)marker).anchoredPosition.y, 1e-3f);

            view.EndDrag();
            Assert.IsFalse(view.IsDragging);
            Assert.AreSame(text, print.GetInputTargetBlock("TEXT"));
            Assert.AreEqual(1, ws.TopBlocks.Count);
            Assert.AreEqual(1, edits);
            Assert.AreEqual(0, view.transform.Cast<Transform>().Count(t => t.name == "snap"), "marker is gone after the rebuild");
            Assert.AreEqual(ws.AllBlocks().Count(), view.Layout.Boxes.Count(b => !b.IsShadow));
        }

        [Test]
        public void CancelPutsEverythingBack()
        {
            var (view, ws, print, text) = DragSetup();
            string before = WorkspaceJson.Save(ws);
            var box = view.Layout.ByBlock[text];
            view.BeginDrag(text, new Vector2(box.X + 5, box.Y + 5));
            view.DragTo(new Vector2(0, 0));
            view.CancelDrag();
            Assert.IsFalse(view.IsDragging);
            Assert.AreEqual(before, WorkspaceJson.Save(ws));
        }

        [Test]
        public void DraggingAChildOutOfItsParentShrinksTheParentAndRebuildsOnce()
        {
            var view = NewView();
            var ws = StandardBlockSet.Load("<xml xmlns=\"https://developers.google.com/blockly/xml\"><block type=\"text_print\" x=\"10\" y=\"10\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">a long long text</field></block></value></block></xml>");
            view.SetWorkspace(ws);
            var print = ws.TopBlocks[0]; var child = print.GetInputTargetBlock("TEXT");
            float widthWith = view.Layout.ByBlock[print].W;
            var cb = view.Layout.ByBlock[child];
            view.BeginDrag(child, new Vector2(cb.X + 3, cb.Y + 3));
            Assert.IsNull(child.Parent);
            Assert.AreEqual(2, ws.TopBlocks.Count);
            view.CancelDrag();
            Assert.AreSame(print, child.Parent, "cancel reattaches it");
            Assert.AreEqual(widthWith, view.Layout.ByBlock[print].W, 1e-3f);
        }

        [Test]
        public void PointerEventsDriveTheWholeDragThroughTheView()
        {
            var (view, ws, print, text) = DragSetup();
            var es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            try
            {
                var box = view.Layout.ByBlock[text];
                var grab = new Vector2(box.X + 5, box.Y + 5);
                var start = view.WorkspaceToScreen(grab, null);
                var end = view.WorkspaceToScreen(PointOntoTextInput(view, print, text, grab), null);

                var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = start, pressPosition = start };
                var textObject = view.GetComponentsInChildren<BlockDragTarget>().First(t => t.Block == text).gameObject;

                ExecuteEvents.Execute(textObject, ped, ExecuteEvents.beginDragHandler);
                Assert.IsTrue(view.IsDragging, "the block's object started the drag");
                Assert.AreSame(view.gameObject, ped.pointerDrag, "later events are redirected to the view (the block's own object gets rebuilt)");

                ped.position = end;
                ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.dragHandler);
                Assert.IsNotNull(view.SnapCandidate);

                ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.endDragHandler);
                Assert.IsFalse(view.IsDragging);
                Assert.AreSame(text, print.GetInputTargetBlock("TEXT"));
            }
            finally { Object.DestroyImmediate(es.gameObject); }
        }

        [Test]
        public void OnlyTheLeftMouseButtonDrags()
        {
            var (view, ws, print, text) = DragSetup();
            var es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            try
            {
                var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Right, position = Vector2.zero, pressPosition = Vector2.zero };
                var go = view.GetComponentsInChildren<BlockDragTarget>().First(t => t.Block == text).gameObject;
                ExecuteEvents.Execute(go, ped, ExecuteEvents.beginDragHandler);
                Assert.IsFalse(view.IsDragging);
            }
            finally { Object.DestroyImmediate(es.gameObject); }
        }

        [Test]
        public void ShadowBlocksDragTheirParent()
        {
            var view = NewView();
            var ws = StandardBlockSet.Load("<xml xmlns=\"https://developers.google.com/blockly/xml\"><block type=\"text_print\" x=\"10\" y=\"10\"><value name=\"TEXT\"><shadow type=\"text\"><field name=\"TEXT\">s</field></shadow></value></block></xml>");
            view.SetWorkspace(ws);
            var es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            try
            {
                var print = ws.TopBlocks[0];
                var shadowObject = view.GetComponentsInChildren<BlockDragTarget>().First(t => t.Block.IsShadow).gameObject;
                var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = Vector2.zero, pressPosition = Vector2.zero };
                ExecuteEvents.Execute(shadowObject, ped, ExecuteEvents.beginDragHandler);
                Assert.IsTrue(view.IsDragging);
                Assert.AreSame(print, view.DraggedBlock);
                view.CancelDrag();
            }
            finally { Object.DestroyImmediate(es.gameObject); }
        }

        [Test]
        public void ScreenAndWorkspaceCoordinatesRoundTrip()
        {
            var view = NewView();
            view.SetWorkspace(StandardBlockSet.Load(Fixture("loops")));
            foreach (var p in new[] { new Vector2(0, 0), new Vector2(123.5f, 40f), new Vector2(300, 700) })
            {
                var back = view.ScreenToWorkspace(view.WorkspaceToScreen(p, null), null);
                Assert.AreEqual(p.x, back.x, 1e-2f); Assert.AreEqual(p.y, back.y, 1e-2f);
            }
        }

        static bool Contains(PolygonGraphic g, float x, float y)
        {
            bool inside = false;
            var p = g.Polygon;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].Y > y) != (p[j].Y > y) && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X) inside = !inside;
            return inside;
        }
    }
}
