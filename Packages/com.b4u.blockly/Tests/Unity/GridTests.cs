using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Blockly.UI.Tests
{
    /// <summary>The dotted grid behind the workspace and the blocks that land on it.</summary>
    public class GridTests
    {
        PlaygroundController c;
        WorkspaceView view;

        [SetUp]
        public void SetUp()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            c = Object.FindFirstObjectByType<PlaygroundController>();
            c.ShowByName("text");
            view = c.View;
            BuiltInThemes.Register();
        }

        [TearDown] public void TearDown() { BlockStyle.Theme = null; BlockThemeCatalog.Clear(); }

        static float Mod(float v, float step) { float m = v % step; return Mathf.Min(Mathf.Abs(m), Mathf.Abs(m - Mathf.Sign(m) * step)); }

        [Test]
        public void TheClassicLookHasNoGridAndTheDarkThemeHasOne()
        {
            view.UpdateGrid();
            Assert.IsTrue(view.Grid == null || !view.Grid.gameObject.activeSelf);
            BlockThemeCatalog.Select("Dark");
            view.UpdateGrid();
            Assert.NotNull(view.Grid);
            Assert.IsTrue(view.Grid.gameObject.activeSelf);
            Assert.AreEqual(view.transform.parent, view.Grid.transform.parent);
            Assert.AreEqual(0, view.Grid.transform.GetSiblingIndex(), "behind the blocks");
            Assert.AreEqual(24f, BlockStyle.GridSpacing);
            BlockThemeCatalog.Select("Classic");
            view.UpdateGrid();
            Assert.IsFalse(view.Grid.gameObject.activeSelf);
        }

        [Test]
        public void ThePointsAreAtMultiplesOfTheSpacingInWorkspaceUnitsWhateverThePanAndZoom()
        {
            BlockThemeCatalog.Select("Dark");
            var viewport = (RectTransform)view.transform.parent;
            Canvas.ForceUpdateCanvases();
            foreach (var (pan, zoom) in new[] { (new Vector2(20, -20), 1f), (new Vector2(-137.5f, 53f), 1f), (new Vector2(-60, -90), 0.5f), (new Vector2(35, -10), 2f) })
            {
                var content = (RectTransform)view.transform;
                content.anchoredPosition = pan; content.localScale = new Vector3(zoom, zoom, 1);
                view.UpdateGrid();
                var points = view.Grid.Points();
                Assert.Greater(points.Count, 20, "zoom " + zoom);
                var r = ((RectTransform)view.Grid.transform).rect;
                foreach (var p in points)
                {
                    float wx = (p.x - r.xMin - pan.x) / zoom, wy = (r.yMax - p.y + pan.y) / zoom;
                    Assert.Less(Mod(wx, 24f), 0.01f, "x at zoom " + zoom + " pan " + pan);
                    Assert.Less(Mod(wy, 24f), 0.01f, "y at zoom " + zoom + " pan " + pan);
                    Assert.GreaterOrEqual(p.x, r.xMin - 0.01f); Assert.LessOrEqual(p.x, r.xMax + 0.01f);
                    Assert.GreaterOrEqual(p.y, r.yMin - 0.01f); Assert.LessOrEqual(p.y, r.yMax + 0.01f);
                }
            }
        }

        [Test]
        public void AGridTooFineToSeeIsNotDrawn()
        {
            var t = BuiltInThemes.Dark();
            t.GridSpacing = 6f;
            BlockStyle.Theme = t;
            var content = (RectTransform)view.transform;
            content.localScale = new Vector3(0.5f, 0.5f, 1);
            view.UpdateGrid();
            Assert.AreEqual(0, view.Grid.Points().Count);
            Object.DestroyImmediate(t);
        }

        [Test]
        public void TheFlyoutHasNoGrid()
        {
            BlockThemeCatalog.Select("Dark");
            c.Toolbox.SelectCategory(0);
            c.Toolbox.FlyoutView.UpdateGrid();
            Assert.IsNull(c.Toolbox.FlyoutView.Grid);
        }

        [Test]
        public void ADroppedBlockLandsOnTheGridOnlyWhenTheThemeHasOne()
        {
            var block = view.Workspace.TopBlocks.First(b => b.Previous != null && b.Output == null);
            Vector2 at = new Vector2((float)block.X + 5, (float)block.Y + 5);
            BlockThemeCatalog.Select("Classic");
            Assert.IsTrue(view.BeginDrag(block, new Vector2((float)block.X, (float)block.Y)));
            view.DragTo(new Vector2(at.x + 31.3f, at.y + 17.7f));
            view.EndDrag();
            Assert.AreNotEqual(0f, Mod((float)block.X, 24f) + Mod((float)block.Y, 24f), "no grid, so no steps");

            BlockThemeCatalog.Select("Dark");
            Assert.IsTrue(view.BeginDrag(block, new Vector2((float)block.X, (float)block.Y)));
            view.DragTo(new Vector2((float)block.X + 41.3f, (float)block.Y + 29.7f));
            view.EndDrag();
            Assert.Less(Mod((float)block.X, 24f), 0.001f);
            Assert.Less(Mod((float)block.Y, 24f), 0.001f);
        }

        [Test]
        public void ATurnedOffSnapKeepsTheGridButNotTheSteps()
        {
            var t = BuiltInThemes.Dark();
            t.GridSnap = false;
            BlockStyle.Theme = t;
            var block = view.Workspace.TopBlocks.First(b => b.Previous != null && b.Output == null);
            Assert.IsTrue(view.BeginDrag(block, new Vector2((float)block.X, (float)block.Y)));
            view.DragTo(new Vector2((float)block.X + 11.3f, (float)block.Y + 9.7f));
            view.EndDrag();
            Assert.Greater(Mod((float)block.X, 24f) + Mod((float)block.Y, 24f), 0.001f);
            view.UpdateGrid();
            Assert.IsTrue(view.Grid.gameObject.activeSelf);
            Object.DestroyImmediate(t);
        }
    }
}
