using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI.Tests
{
    /// <summary>Zooming the workspace: the point under the pointer stays put, conversions stay consistent, limits hold.</summary>
    public class ZoomTests
    {
        GameObject root;

        [TearDown]
        public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        const string Xml =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<block type=\"text_print\" x=\"60\" y=\"40\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">hello</field></block></value></block></xml>";

        sealed class NoSpawner : IBlockSpawner { public bool TrySpawn(Block flyoutBlock, PointerEventData e, WorkspaceView source) => false; }

        WorkspaceView NewView()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            return view;
        }

        static void AssertNear(Vector2 expected, Vector2 actual, float tolerance, string what)
            => Assert.LessOrEqual(Vector2.Distance(expected, actual), tolerance, what + ": expected " + expected + " but was " + actual);

        [Test]
        public void ZoomStartsAtOneAndIsClamped()
        {
            var view = NewView();
            Assert.AreEqual(1f, view.Zoom);
            Assert.IsTrue(view.SetZoom(100f, Vector2.zero));
            Assert.AreEqual(WorkspaceView.MaxZoom, view.Zoom, 1e-4f);
            Assert.IsFalse(view.SetZoom(100f, Vector2.zero), "already at the limit");
            Assert.IsTrue(view.SetZoom(0.001f, Vector2.zero));
            Assert.AreEqual(WorkspaceView.MinZoom, view.Zoom, 1e-4f);
        }

        [Test]
        public void ThePointUnderThePivotDoesNotMove()
        {
            var view = NewView();
            var point = new Vector2(150, 90);                           // a point in workspace units
            var pivot = view.WorkspaceToScreen(point, null);
            Assert.IsTrue(view.ZoomBy(2f, pivot));
            AssertNear(point, view.ScreenToWorkspace(pivot, null), 0.05f, "after zooming in");
            Assert.IsTrue(view.ZoomBy(0.25f, pivot));
            AssertNear(point, view.ScreenToWorkspace(pivot, null), 0.05f, "after zooming out");
        }

        [Test]
        public void ConversionsFollowTheZoom()
        {
            var view = NewView();
            var a = view.WorkspaceToScreen(new Vector2(10, 10), null);
            var b = view.WorkspaceToScreen(new Vector2(110, 10), null);
            float unit = Vector2.Distance(a, b) / 100f;

            view.SetZoom(2f, view.WorkspaceToScreen(Vector2.zero, null));
            var a2 = view.WorkspaceToScreen(new Vector2(10, 10), null);
            var b2 = view.WorkspaceToScreen(new Vector2(110, 10), null);
            Assert.AreEqual(unit * 2f, Vector2.Distance(a2, b2) / 100f, 1e-3f, "twice as many pixels per workspace unit");

            var s = new Vector2(333, 222);
            AssertNear(s, view.WorkspaceToScreen(view.ScreenToWorkspace(s, null), null), 0.05f, "screen -> workspace -> screen");
        }

        [Test]
        public void ZoomSurvivesARebuildAndDragStillWorks()
        {
            var view = NewView();
            view.SetZoom(1.5f, view.WorkspaceToScreen(Vector2.zero, null));
            view.Rebuild();
            Assert.AreEqual(1.5f, view.Zoom, 1e-4f);

            var print = view.Workspace.TopBlocks[0];
            Assert.IsTrue(view.BeginDrag(print, new Vector2(70, 50)));
            view.DragTo(new Vector2(170, 150));
            view.EndDrag();
            Assert.AreEqual(160, print.X, 1e-3);
            Assert.AreEqual(140, print.Y, 1e-3);
        }

        [Test]
        public void KeyboardShortcutsZoomInOutAndReset()
        {
            var view = NewView();
            Assert.IsTrue(view.HandleShortcut(KeyCode.Equals, true, false));
            Assert.AreEqual(WorkspaceView.ZoomStep, view.Zoom, 1e-4f);
            Assert.IsTrue(view.HandleShortcut(KeyCode.Minus, true, false));
            Assert.AreEqual(1f, view.Zoom, 1e-4f);
            view.HandleShortcut(KeyCode.Equals, true, false); view.HandleShortcut(KeyCode.Equals, true, false);
            Assert.IsTrue(view.HandleShortcut(KeyCode.Alpha0, true, false));
            Assert.AreEqual(1f, view.Zoom, 1e-4f);
            Assert.IsFalse(view.HandleShortcut(KeyCode.Equals, false, false), "the bare key is not a shortcut");
        }

        [Test]
        public void AFlyoutNeverZooms()
        {
            var view = NewView();
            view.Spawner = new NoSpawner();
            Assert.IsFalse(view.SetZoom(2f, Vector2.zero));
            Assert.AreEqual(1f, view.Zoom);
        }
    }
}
