using System;
using System.IO;
using System.Linq;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>Rounded outlines: a different line, the same layout.</summary>
    public class BlockGeometryTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }

        [TearDown] public void TearDown() => BlockGeometry.Current = null;

        static LayoutResult Layout(string program)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs", program + ".json")));
            return new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace();
        }

        static System.Collections.Generic.IEnumerable<string> Programs()
            => Directory.GetFiles(Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs"), "*.expected.txt")
                .Select(f => Path.GetFileName(f).Replace(".expected.txt", "")).OrderBy(x => x).Take(12);

        [TestCaseSource(nameof(Programs))]
        public void SizesAndConnectionsDoNotChangeWithTheShape(string name)
        {
            var classic = Layout(name);
            BlockGeometry.Current = new BlockGeometry { CornerRadius = 6, RoundNotches = true, RoundTabs = true };
            var round = Layout(name);
            Assert.AreEqual(classic.Boxes.Count, round.Boxes.Count);
            for (int i = 0; i < classic.Boxes.Count; i++)
            {
                Assert.AreEqual(classic.Boxes[i].W, round.Boxes[i].W, 1e-4f, name);
                Assert.AreEqual(classic.Boxes[i].H, round.Boxes[i].H, 1e-4f, name);
                CollectionAssert.AreEqual(classic.Boxes[i].Connections.Values.Select(v => (v.X, v.Y)), round.Boxes[i].Connections.Values.Select(v => (v.X, v.Y)));
            }
        }

        [TestCaseSource(nameof(Programs))]
        public void RoundedOutlinesStillTriangulateWithoutLosingArea(string name)
        {
            BlockGeometry.Current = new BlockGeometry { CornerRadius = 6, RoundNotches = true, RoundTabs = true };
            foreach (var box in Layout(name).Boxes)
            {
                foreach (var v in box.Outline) Assert.IsFalse(float.IsNaN(v.X) || float.IsNaN(v.Y), box.Block.Type);
                var tris = Geometry.Triangulate(box.Outline);
                Assert.AreEqual(Math.Abs(Geometry.SignedArea(box.Outline)), Geometry.TrianglesArea(box.Outline, tris), 0.5f, box.Block.Type);
            }
        }

        [Test]
        public void ARoundedRectangleLosesTheCornersButKeepsTheEdges()
        {
            var rect = new[] { new Vec2(0, 0), new Vec2(100, 0), new Vec2(100, 40), new Vec2(0, 40) };
            var r = Geometry.RoundCorners(rect, 8);
            Assert.Greater(r.Count, 4);
            Assert.Less(Math.Abs(Geometry.SignedArea(r)), 100 * 40f);
            float removed = 100 * 40f - Math.Abs(Geometry.SignedArea(r));
            Assert.That(removed, Is.InRange(50f, 65f), "four corners remove about 4 * (1 - pi/4) * r^2 = 55");
            Assert.IsTrue(r.Any(v => Math.Abs(v.X - 8) < 1e-3f && Math.Abs(v.Y) < 1e-3f), "straight edge starts after the radius");
            Assert.IsFalse(r.Any(v => v.X < 0.01f && v.Y < 0.01f), "the sharp corner is gone");
        }

        [Test]
        public void ARadiusLargerThanHalfAnEdgeIsClamped()
        {
            var rect = new[] { new Vec2(0, 0), new Vec2(100, 0), new Vec2(100, 10), new Vec2(0, 10) };
            foreach (var v in Geometry.RoundCorners(rect, 50)) { Assert.GreaterOrEqual(v.Y, -1e-3f); Assert.LessOrEqual(v.Y, 10.001f); }
        }

        [Test]
        public void TheClassicGeometryIsUnchanged()
        {
            var a = Layout("text");
            BlockGeometry.Current = new BlockGeometry();
            var b = Layout("text");
            for (int i = 0; i < a.Boxes.Count; i++)
                CollectionAssert.AreEqual(a.Boxes[i].Outline.Select(v => (v.X, v.Y)), b.Boxes[i].Outline.Select(v => (v.X, v.Y)));
        }
    }
}
