using System;
using System.Collections.Generic;

namespace Blockly.Layout
{
    /// <summary>
    /// How block outlines are drawn. The sizes (padding, row heights, notch and tab extents) stay the same, so layout
    /// and snapping do not change; only the line the outline follows does. <see cref="Classic"/> is Blockly's look.
    /// </summary>
    public sealed class BlockGeometry
    {
        /// <summary>Radius for the right-angle corners of block outlines (0 = sharp, as in Blockly).</summary>
        public float CornerRadius;
        /// <summary>Previous/next notches as a smooth bump instead of a trapezoid.</summary>
        public bool RoundNotches;
        /// <summary>Output tabs and the matching holes as a smooth bump instead of a puzzle knob.</summary>
        public bool RoundTabs;

        // Spacing and sizes. The defaults are Blockly's (Metrics); notch and tab extents stay fixed.
        public float RowMinHeight = Metrics.RowMinHeight;
        public float PadLeft = Metrics.PadLeft, PadRight = Metrics.PadRight, ItemGap = Metrics.ItemGap;
        public float FieldPadX = Metrics.FieldPadX, FieldHeight = Metrics.FieldHeight, LabelHeight = Metrics.LabelHeight;
        public float SlotMinWidth = Metrics.SlotMinWidth, SlotMinHeight = Metrics.SlotMinHeight, SlotPadY = Metrics.SlotPadY;
        public float StatementMinHeight = Metrics.StatementMinHeight, StatementMinWidth = Metrics.StatementMinWidth;
        public float TopBarHeight = Metrics.TopBarHeight, BottomBarHeight = Metrics.BottomBarHeight, StatementGap = Metrics.StatementGap;

        // Connection shapes: the output tab (and the hole it plugs into) and the notch of stacked blocks.
        // Keep TabTop + TabHeight within the smallest row (24) and NotchX + NotchWidth within the statement width (30).
        public float TabWidth = Metrics.TabWidth, TabHeight = Metrics.TabHeight, TabTop = Metrics.TabTop;
        public float NotchX = Metrics.NotchX, NotchWidth = Metrics.NotchWidth, NotchDepth = Metrics.NotchDepth;

        public static readonly BlockGeometry Classic = new BlockGeometry();

        static BlockGeometry current = Classic;

        /// <summary>The geometry used by every <see cref="BlockLayouter"/>. Setting null restores <see cref="Classic"/>.</summary>
        public static BlockGeometry Current { get => current; set => current = value ?? Classic; }

        static readonly float[,] Puzzle = { { 0, 0 }, { -5, 1.5f }, { -8, 4.5f }, { -8, 10.5f }, { -5, 13.5f }, { 0, 15 } };
        const int Steps = 8;

        /// <summary>The tab as (dx, dy) pairs going down from the tab's top; negative dx points left.</summary>
        internal float[,] Tab()
        {
            if (!RoundTabs)
            {
                var puzzle = new float[Puzzle.GetLength(0), 2];
                for (int i = 0; i < puzzle.GetLength(0); i++)
                {
                    puzzle[i, 0] = Puzzle[i, 0] * TabWidth / Metrics.TabWidth;
                    puzzle[i, 1] = Puzzle[i, 1] * TabHeight / Metrics.TabHeight;
                }
                return puzzle;
            }
            var t = new float[Steps + 1, 2];
            float ry = TabHeight / 2f;
            for (int i = 0; i <= Steps; i++)
            {
                double a = Math.PI * i / Steps;                       // 0..pi down the tab
                t[i, 0] = -(float)Math.Sin(a) * TabWidth;
                t[i, 1] = ry - (float)Math.Cos(a) * ry;
            }
            t[0, 0] = 0; t[Steps, 0] = 0;
            return t;
        }

        /// <summary>Appends a notch starting at <paramref name="left"/> on the edge at <paramref name="y"/>, nose pointing down.</summary>
        internal void AddNotch(List<Vec2> p, float left, float y, bool leftToRight)
        {
            var pts = new List<Vec2>();
            if (!RoundNotches)
            {
                pts.Add(new Vec2(left, y));
                float k = NotchWidth / Metrics.NotchWidth;                    // the trapezoid scales with the notch
                pts.Add(new Vec2(left + 6 * k, y + NotchDepth));
                pts.Add(new Vec2(left + 9 * k, y + NotchDepth));
                pts.Add(new Vec2(left + NotchWidth, y));
            }
            else
            {
                float rx = NotchWidth / 2f;
                for (int i = 0; i <= Steps; i++)
                {
                    double a = Math.PI * i / Steps;
                    pts.Add(new Vec2(left + rx - (float)Math.Cos(a) * rx, y + (float)Math.Sin(a) * NotchDepth));
                }
                pts[0] = new Vec2(left, y); pts[Steps] = new Vec2(left + NotchWidth, y);
            }
            if (!leftToRight) pts.Reverse();
            p.AddRange(pts);
        }
    }
}
