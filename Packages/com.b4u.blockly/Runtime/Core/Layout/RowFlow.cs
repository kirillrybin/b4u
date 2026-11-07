using System;
using System.Collections.Generic;

namespace Blockly.Layout
{
    /// <summary>Where an item of a <see cref="RowFlow"/> goes: its row (from 0) and the x of its left edge.</summary>
    public struct RowSlot
    {
        public int Row;
        public float X;
    }

    /// <summary>Places items left to right and starts a new row when the next one would not fit (a toolbar on a narrow screen).</summary>
    public static class RowFlow
    {
        /// <summary>
        /// The slot of every item, in the order given. Items keep their order; a row is as full as it can be without exceeding
        /// <paramref name="available"/> (an item wider than that gets a row of its own).
        /// </summary>
        public static RowSlot[] Arrange(IList<float> widths, float available, float gap, float left, out int rows)
        {
            if (widths == null) throw new ArgumentNullException(nameof(widths));
            var slots = new RowSlot[widths.Count];
            int row = 0;
            float x = left;
            for (int i = 0; i < widths.Count; i++)
            {
                bool first = x == left;
                if (!first && x + widths[i] > left + available) { row++; x = left; }
                slots[i] = new RowSlot { Row = row, X = x };
                x += widths[i] + gap;
            }
            rows = widths.Count == 0 ? 0 : row + 1;
            return slots;
        }

        /// <summary>The x where the next item would start on the last row (<paramref name="left"/> if nothing was placed).</summary>
        public static float EndOfLastRow(IList<float> widths, RowSlot[] slots, float gap, float left)
        {
            if (widths.Count == 0) return left;
            var last = slots[widths.Count - 1];
            return last.X + widths[widths.Count - 1] + gap;
        }
    }
}
