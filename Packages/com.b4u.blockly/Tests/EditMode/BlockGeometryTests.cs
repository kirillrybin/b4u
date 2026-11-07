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

        static BlockGeometry Roomy() => new BlockGeometry
        {
            RowMinHeight = 40, PadLeft = 14, PadRight = 14, ItemGap = 8, FieldHeight = 28, FieldPadX = 9, LabelHeight = 22,
            SlotPadY = 6, BottomBarHeight = 18, StatementGap = 12, StatementMinHeight = 36,
        };

        [Test]
        public void MoreSpacingMakesBlocksBiggerAndKeepsEverythingInsideThem()
        {
            var classic = Layout("text");
            BlockGeometry.Current = Roomy();
            var roomy = Layout("text");
            Assert.AreEqual(classic.Boxes.Count, roomy.Boxes.Count);
            for (int i = 0; i < classic.Boxes.Count; i++)
            {
                Assert.Greater(roomy.Boxes[i].H, classic.Boxes[i].H - 0.01f, "taller rows");
                Assert.GreaterOrEqual(roomy.Boxes[i].H, 40f, "a row is at least RowMinHeight");
            }
            foreach (var box in roomy.Boxes)
                foreach (var f in box.Fields)
                {
                    Assert.GreaterOrEqual(f.X, box.X + 14 - 0.01f, box.Block.Type + " left padding");
                    Assert.LessOrEqual(f.Y + f.H, box.Y + box.H + 0.01f, box.Block.Type);
                    Assert.GreaterOrEqual(f.Y, box.Y - 0.01f, box.Block.Type);
                }
        }

        [TestCaseSource(nameof(Programs))]
        public void RoomyOutlinesStillTriangulateAndStayConnected(string name)
        {
            BlockGeometry.Current = Roomy();
            var layout = Layout(name);
            foreach (var box in layout.Boxes)
            {
                var tris = Geometry.Triangulate(box.Outline);
                Assert.AreEqual(Math.Abs(Geometry.SignedArea(box.Outline)), Geometry.TrianglesArea(box.Outline, tris), 0.5f, box.Block.Type);
                if (box.Block.Parent != null && !box.IsShadow)
                    Assert.IsTrue(layout.ByBlock.ContainsKey(box.Block.Parent));
                // a connected child's connection sits exactly on its parent's
                foreach (var kv in box.Connections)
                    if (kv.Key.Target != null && layout.ByBlock.TryGetValue(kv.Key.Target.Owner, out var other) && other.Connections.TryGetValue(kv.Key.Target, out var at))
                    {
                        Assert.AreEqual(kv.Value.X, at.X, 0.01f, name);
                        Assert.AreEqual(kv.Value.Y, at.Y, 0.01f, name);
                    }
            }
        }

        static BlockGeometry BigConnections(bool round = false) => new BlockGeometry
        {
            TabWidth = 12, TabHeight = 18, TabTop = 3, NotchX = 10, NotchWidth = 20, NotchDepth = 6, RoundNotches = round, RoundTabs = round,
        };

        [TestCaseSource(nameof(Programs))]
        public void BiggerNotchesAndTabsStillTriangulateAndStayConnected(string name)
        {
            foreach (var round in new[] { false, true })
            {
                BlockGeometry.Current = BigConnections(round);
                var layout = Layout(name);
                foreach (var box in layout.Boxes)
                {
                    var tris = Geometry.Triangulate(box.Outline);
                    Assert.AreEqual(Math.Abs(Geometry.SignedArea(box.Outline)), Geometry.TrianglesArea(box.Outline, tris), 0.5f, name + " " + box.Block.Type);
                    foreach (var kv in box.Connections)
                        if (kv.Key.Target != null && layout.ByBlock.TryGetValue(kv.Key.Target.Owner, out var other) && other.Connections.TryGetValue(kv.Key.Target, out var at))
                        {
                            Assert.AreEqual(kv.Value.X, at.X, 0.01f, name);
                            Assert.AreEqual(kv.Value.Y, at.Y, 0.01f, name);
                        }
                }
            }
        }

        [Test]
        public void ATallerWiderTabWidensBlocksThatHaveInlineInputsOrAnOutput()
        {
            var classic = Layout("arithmetic");
            BlockGeometry.Current = BigConnections();
            var big = Layout("arithmetic");
            Assert.Greater(big.Boxes.Sum(b => b.W), classic.Boxes.Sum(b => b.W), "a wider tab takes room in the rows");
        }

        [Test]
        public void TheOutputConnectionSitsAtTheTabTop()
        {
            BlockGeometry.Current = BigConnections();
            var layout = Layout("arithmetic");
            var box = layout.Boxes.First(b => b.Block.Output != null);
            Assert.AreEqual(3f, box.Connections[box.Block.Output].Y - box.Y, 1e-3f, "TabTop");
        }

        [Test]
        public void AHatRisesAboveTheBlockOnlyForStackStartersThatAreAccepted()
        {
            var classic = Layout("text");
            BlockGeometry.Current = new BlockGeometry { HatHeight = 16, IsHat = b => true };
            var hat = Layout("text");
            foreach (var box in hat.Boxes)
            {
                float top = box.Outline.Min(v => v.Y);
                if (box.Block.Previous == null && box.Block.Output == null)
                    Assert.AreEqual(box.Y - 16f, top, 0.2f, box.Block.Type + " has a cap of the given height");
                else
                    Assert.AreEqual(box.Y, top, 0.01f, box.Block.Type + " has none");
            }
            float expectedTop = hat.Boxes.Min(b => b.Y - (b.Block.Previous == null && b.Block.Output == null ? 16f : 0f));
            Assert.AreEqual(expectedTop, hat.Top, 0.01f, "the layout reserves the cap above the topmost block");
            Assert.Less(hat.Top, classic.Top + 0.01f);
            BlockGeometry.Current = new BlockGeometry { HatHeight = 16, IsHat = b => false };
            Assert.AreEqual(classic.Top, Layout("text").Top, 1e-4f, "nothing accepted, nothing drawn");
        }

        [Test]
        public void TheCapStartsAndEndsLevelWithTheEdgeInsteadOfAFastClimb()
        {
            BlockGeometry.Current = new BlockGeometry { HatHeight = 20, HatWidth = 80, IsHat = b => true };
            var box = Layout("procedures").Boxes.First(b => b.Block.Previous == null && b.Block.Output == null);
            var outline = box.Outline.ToList();
            int start = outline.FindIndex(v => Math.Abs(v.X - box.X) < 1e-3f && Math.Abs(v.Y - box.Y) < 1e-3f);
            var a = outline[(start + 1) % outline.Count]; var peak = outline.OrderBy(v => v.Y).First();
            float slopeAtStart = (box.Y - a.Y) / (a.X - box.X);
            Assert.Less(slopeAtStart, 0.35f, "the first step off the flat edge is gentle (a cubic with a steep tangent was about 1.2)");
            Assert.AreEqual(box.Y - 20f, peak.Y, 0.2f);
            Assert.AreEqual(box.X + 40f, peak.X, 5f, "the peak is mid-cap");
        }

        [Test]
        public void ADomeRisesAtTheLeftEdgePeaksAndComesBackToTheTopEdgeWithAFillet()
        {
            BlockGeometry.Current = new BlockGeometry { HatHeight = 30, HatStyle = HatShape.Dome, IsHat = b => true };
            var box = Layout("procedures").Boxes.First(b => b.Block.Previous == null && b.Block.Output == null);
            var o = box.Outline;
            Assert.AreEqual(box.Y - 30f, o.Min(v => v.Y), 0.01f, "as tall as asked");
            Assert.GreaterOrEqual(o.Min(v => v.X), box.X - 0.01f, "the dome does not poke out to the left");
            Assert.IsTrue(o.Any(v => v.X < box.X + 1f && v.Y < box.Y - 3f), "the left edge leaves the block's top-left corner going straight up");
            float width = box.W * 0.62f;
            var peak = o.OrderBy(v => v.Y).First();
            Assert.AreEqual(box.X + width * 0.45f, peak.X, width * 0.05f, "the peak is at HatPeak of the cap");
            float domeEnd = o.Where(v => v.Y < box.Y - 0.01f).Max(v => v.X);
            Assert.AreEqual(box.X + width, domeEnd, 3f, "the cap ends at 62% of the block (the last fillet point sits on the edge itself)");
            // the fillet: the outline reaches the top edge going horizontally, so the last steps are nearly level
            var onEdge = o.ToList();
            int i = onEdge.FindIndex(v => Math.Abs(v.X - domeEnd) < 1e-3f && v.Y < box.Y - 0.01f);
            var prev = onEdge[i - 1];
            Assert.Greater(Math.Abs(onEdge[i].X - prev.X), 0f);
            Assert.Less(onEdge[i].Y, box.Y, "the fillet's last step is just above the edge");
        }

        [Test]
        public void CleaningUpLeavesRoomAboveEveryHatSoNothingIsClipped()
        {
            BlockGeometry.Current = new BlockGeometry { HatHeight = 30, HatStyle = HatShape.Dome, IsHat = b => true };
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs", "procedures.json")));
            var layouter = new BlockLayouter(ws, new FixedWidth());
            layouter.CleanUp(18f);
            var layout = layouter.LayoutWorkspace();
            Assert.GreaterOrEqual(layout.Top, -0.001f, "the topmost cap starts inside the workspace");
            foreach (var box in layout.Boxes) Assert.GreaterOrEqual(box.Outline.Min(v => v.Y), -0.01f, box.Block.Type);
            // and stacks do not run into each other's caps
            var tops = ws.TopBlocks.OrderBy(b => b.Y).ToList();
            for (int i = 1; i < tops.Count; i++)
            {
                var above = layout.ByBlock[tops[i - 1]].Outline.Max(v => v.Y);
                var capOfThis = layout.ByBlock[tops[i]].Outline.Min(v => v.Y);
                Assert.Less(above, capOfThis + 0.01f, "stack " + i + " starts below the previous one");
            }
        }

        [TestCaseSource(nameof(Programs))]
        public void DomesAndRoundedCornersStillTriangulate(string name)
        {
            foreach (var radius in new[] { 0f, 8f })
            {
                BlockGeometry.Current = new BlockGeometry { HatHeight = 26, HatStyle = HatShape.Dome, IsHat = b => true, CornerRadius = radius, RoundNotches = radius > 0 };
                foreach (var box in Layout(name).Boxes)
                {
                    var tris = Geometry.Triangulate(box.Outline);
                    Assert.AreEqual(Math.Abs(Geometry.SignedArea(box.Outline)), Geometry.TrianglesArea(box.Outline, tris), 0.5f, name + " " + box.Block.Type);
                }
            }
        }

        [TestCaseSource(nameof(Programs))]
        public void HatsAndRoundedCornersStillTriangulate(string name)
        {
            foreach (var radius in new[] { 0f, 8f })
            {
                BlockGeometry.Current = new BlockGeometry { HatHeight = 18, HatWidth = 70, IsHat = b => true, CornerRadius = radius, RoundNotches = radius > 0 };
                foreach (var box in Layout(name).Boxes)
                {
                    var tris = Geometry.Triangulate(box.Outline);
                    Assert.AreEqual(Math.Abs(Geometry.SignedArea(box.Outline)), Geometry.TrianglesArea(box.Outline, tris), 0.5f, name + " " + box.Block.Type);
                }
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
