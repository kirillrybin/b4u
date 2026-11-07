using System.Linq;
using Blockly.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    public class PolygonStrokeTests
    {
        GameObject root;
        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        static Mesh MeshOf(PolygonGraphic g)
        {
            g.Rebuild(CanvasUpdate.PreRender);
            return g.canvasRenderer.GetMesh();
        }

        PolygonGraphic Make(Vec2[] points, float width)
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("poly", typeof(RectTransform), typeof(PolygonGraphic));
            go.transform.SetParent(root.transform, false);
            var g = go.GetComponent<PolygonGraphic>();
            g.SetShape(points, Color.white, Color.black, width);
            return g;
        }

        [Test]
        public void TheStrokeOfARoundedShapeNeverSticksOutMoreThanHalfItsWidth()
        {
            var rect = new[] { new Vec2(0, 0), new Vec2(120, 0), new Vec2(120, 60), new Vec2(0, 60) };
            var rounded = Geometry.RoundCorners(rect, 12).ToArray();
            var mesh = MeshOf(Make(rounded, 4f));
            float half = 2f;
            foreach (var v in mesh.vertices)
            {
                float best = rounded.Min(p => Vector2.Distance(new Vector2(p.X, -p.Y), v));
                Assert.LessOrEqual(best, half * 1.01f, "stroke vertex " + v + " is farther than half the stroke width from the outline");
            }
        }

        [Test]
        public void SharpRightAngleCornersStayMitered()
        {
            var rect = new[] { new Vec2(0, 0), new Vec2(100, 0), new Vec2(100, 40), new Vec2(0, 40) };
            var mesh = MeshOf(Make(rect, 2f));
            Assert.IsTrue(mesh.vertices.Any(v => Mathf.Abs(v.x + 1f) < 1e-3f && Mathf.Abs(v.y - 1f) < 1e-3f), "outer corner of the miter at (-1, +1)");
        }

        [Test]
        public void ASoftEdgeAddsATransparentFringeOfAboutOnePixelOutsideTheStroke()
        {
            var rect = new[] { new Vec2(0, 0), new Vec2(100, 0), new Vec2(100, 40), new Vec2(0, 40) };
            var g = Make(Geometry.RoundCorners(rect, 8).ToArray(), 2f);
            g.EdgeWidth = 1f;
            var mesh = MeshOf(g);
            var colours = mesh.colors32;
            Assert.IsTrue(colours.Any(c => c.a == 0), "outer fringe is transparent");
            Assert.IsTrue(colours.Any(c => c.a == 255), "the core stays opaque");
            float maxOut = mesh.vertices.Max(v => Mathf.Max(Mathf.Max(-v.x, v.x - 100f), Mathf.Max(v.y, -40f - v.y)));
            Assert.LessOrEqual(maxOut, 1f + 0.5f + 0.01f, "no further than half the stroke plus the feather");
            Assert.Greater(maxOut, 1f + 0.4f, "the fringe really extends past the stroke");
        }

        [Test]
        public void WithoutAStrokeTheFillGetsItsOwnSoftEdge()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("poly", typeof(RectTransform), typeof(PolygonGraphic));
            go.transform.SetParent(root.transform, false);
            var g = go.GetComponent<PolygonGraphic>();
            g.SetShape(new[] { new Vec2(0, 0), new Vec2(50, 0), new Vec2(50, 20), new Vec2(0, 20) }, Color.red);
            int hard = MeshOf(g).vertexCount;
            g.EdgeWidth = 1f;
            var soft = MeshOf(g);
            Assert.Greater(soft.vertexCount, hard);
            Assert.AreEqual(4, soft.colors32.Count(c => c.a == 0));
        }

        [Test]
        public void AViewKeepsTheSoftEdgeAboutOnePixelWhateverTheZoomAndRefreshesIt()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load("{\"blocks\":{\"languageVersion\":0,\"blocks\":[{\"type\":\"text_print\",\"x\":0,\"y\":0}]}}"));
            var shape = go.GetComponentsInChildren<PolygonGraphic>().First();
            Assert.AreEqual(1f, shape.EdgeWidth, 1e-3f, "one pixel at zoom 1 and canvas scale 1");

            go.transform.localScale = new Vector3(2f, 2f, 1f);                 // what SetZoom does
            view.RefreshEdges();
            Assert.AreEqual(0.5f, shape.EdgeWidth, 1e-3f, "half a local unit when zoomed in twice");
        }
    }
}
