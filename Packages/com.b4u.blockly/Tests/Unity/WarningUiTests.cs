using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI.Tests
{
    /// <summary>The warning icon and its bubble.</summary>
    public class WarningUiTests
    {
        GameObject root;

        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        const string Head = "<xml xmlns=\"https://developers.google.com/blockly/xml\"><variables><variable id=\"v1\">x</variable></variables>";
        const string Empty = Head + "<block type=\"variables_set\" x=\"20\" y=\"20\"><field name=\"VAR\" id=\"v1\">x</field></block></xml>";
        const string Full = Head + "<block type=\"variables_set\" x=\"20\" y=\"20\"><field name=\"VAR\" id=\"v1\">x</field><value name=\"VALUE\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value></block></xml>";

        sealed class NoSpawn : IBlockSpawner { public bool TrySpawn(Block b, PointerEventData e, WorkspaceView v) => false; }

        WorkspaceView NewView(string xml, bool flyout = false)
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
            if (flyout) view.Spawner = new NoSpawn();
            view.SetWorkspace(StandardBlockSet.Load(xml));
            return view;
        }

        static Block Set(WorkspaceView v) => v.Workspace.TopBlocks.First(b => b.Type == "variables_set");
        static Transform Icon(WorkspaceView v, Block b) => v.transform.Cast<Transform>().First(t => t.name == b.Type).Find(WorkspaceView.WarningIconName);

        [Test]
        public void ABlockWithAWarningHasTheIconAndAFilledOneDoesNot()
        {
            var v = NewView(Empty);
            Assert.IsNotNull(Icon(v, Set(v)));
            StringAssert.Contains("empty", v.WarningOf(Set(v)));
            Assert.IsNotNull(Icon(v, Set(v)).GetComponent<WarningIconTarget>());
            var ok = NewView(Full);
            Assert.IsNull(Icon(ok, Set(ok)));
            Assert.IsNull(ok.WarningOf(Set(ok)));
        }

        [Test]
        public void FillingTheInputRemovesTheIconAfterARebuild()
        {
            var v = NewView(Empty);
            v.Workspace.TopBlocks.Clear();
            v.Workspace.TopBlocks.AddRange(StandardBlockSet.Load(Full).TopBlocks);
            v.Rebuild();
            Assert.IsNull(Icon(v, Set(v)));
        }

        [Test]
        public void TheIconOpensAndClosesTheBubbleWithTheText()
        {
            var v = NewView(Empty);
            Assert.IsTrue(v.ToggleWarningBubble(Set(v)));
            var bubble = v.WarningBubbleOf(Set(v));
            Assert.IsNotNull(bubble);
            Assert.AreEqual(v.WarningOf(Set(v)), bubble.Text);
            Assert.IsTrue(v.ToggleWarningBubble(Set(v)));
            Assert.IsNull(v.WarningBubbleOf(Set(v)));
        }

        [Test]
        public void TurningWarningsOffRemovesTheIconAndTheBubble()
        {
            var v = NewView(Empty);
            v.ToggleWarningBubble(Set(v));
            v.WarningsEnabled = false;
            Assert.IsNull(Icon(v, Set(v)));
            Assert.IsNull(v.WarningBubbleOf(Set(v)));
            Assert.IsNull(v.WarningOf(Set(v)));
            v.WarningsEnabled = true;
            Assert.IsNotNull(Icon(v, Set(v)));
        }

        [Test]
        public void AFlyoutShowsNoWarnings()
        {
            var v = NewView(Empty, flyout: true);
            Assert.IsNull(Icon(v, Set(v)));
        }

        [Test]
        public void TheBubbleStaysInsideTheVisibleArea()
        {
            var v = NewView(Empty);
            v.ToggleWarningBubble(Set(v));
            var rt = (RectTransform)v.WarningBubbleOf(Set(v)).transform;
            Assert.GreaterOrEqual(rt.anchoredPosition.x, 0f);
            Assert.GreaterOrEqual(-rt.anchoredPosition.y, 0f);
        }
    }
}
