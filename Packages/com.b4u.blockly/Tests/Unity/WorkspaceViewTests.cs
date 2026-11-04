using System.IO;
using System.Linq;
using Blockly.Layout;
using NUnit.Framework;
using TMPro;
using UnityEngine;

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
