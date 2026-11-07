using System.Linq;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class RowFlowTests
    {
        [Test]
        public void EverythingFitsInOneRow()
        {
            var slots = RowFlow.Arrange(new float[] { 50, 50, 50 }, 400, 10, 8, out int rows);
            Assert.AreEqual(1, rows);
            CollectionAssert.AreEqual(new float[] { 8, 68, 128 }, slots.Select(s => s.X).ToArray());
            Assert.IsTrue(slots.All(s => s.Row == 0));
        }

        [Test]
        public void ARowIsFilledAndThenTheNextItemStartsANewOne()
        {
            var slots = RowFlow.Arrange(new float[] { 100, 100, 100, 100 }, 230, 10, 0, out int rows);
            Assert.AreEqual(2, rows);
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, slots.Select(s => s.Row).ToArray());
            CollectionAssert.AreEqual(new float[] { 0, 110, 0, 110 }, slots.Select(s => s.X).ToArray());
        }

        [Test]
        public void AnItemThatJustFitsStaysAndOneTooWideGetsItsOwnRow()
        {
            var exact = RowFlow.Arrange(new float[] { 100, 100 }, 210, 10, 0, out int rows);
            Assert.AreEqual(1, rows, "100 + 10 + 100 = 210 fits");
            var wide = RowFlow.Arrange(new float[] { 50, 500, 50 }, 200, 10, 0, out rows);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, wide.Select(s => s.Row).ToArray());
        }

        [Test]
        public void NoItemEverLeavesTheAvailableWidthUnlessItIsWiderThanItAlone()
        {
            var widths = new float[] { 56, 56, 80, 80, 80, 120, 84, 84, 74 };
            foreach (float available in new float[] { 300, 400, 480, 600, 720, 1000, 1300 })
            {
                var slots = RowFlow.Arrange(widths, available, 8, 8, out int rows);
                for (int i = 0; i < widths.Length; i++)
                    Assert.LessOrEqual(slots[i].X + widths[i], 8 + available + 0.001f, "item " + i + " at width " + available);
                for (int i = 1; i < widths.Length; i++)
                    Assert.IsTrue(slots[i].Row > slots[i - 1].Row || slots[i].X > slots[i - 1].X, "order is kept");
                Assert.AreEqual(slots.Max(s => s.Row) + 1, rows);
            }
        }

        [Test]
        public void NothingToPlaceMakesNoRows()
        {
            RowFlow.Arrange(new float[0], 100, 8, 0, out int rows);
            Assert.AreEqual(0, rows);
            Assert.AreEqual(5f, RowFlow.EndOfLastRow(new float[0], new RowSlot[0], 8, 5));
        }
    }
}
