using System;
using System.Collections.Generic;

namespace Blockly.Layout
{
    /// <summary>The outline of a block's cap.</summary>
    public enum HatShape
    {
        /// <summary>A symmetric bump in the top edge, level with the edge at both ends.</summary>
        Bump,
        /// <summary>A dome: the left edge rises past the top, turns with a large radius, runs flat and slopes down to the edge.</summary>
        Dome,
    }

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

        // Hats: a cap above blocks that start a stack (no previous or output connection) and that IsHat accepts.
        public float HatHeight, HatWidth;                     // HatWidth 0 = 60% of the block (Dome: 62%)
        public HatShape HatStyle;
        /// <summary>Dome only: where the peak is, as a share of the cap's width (0.1..0.9).</summary>
        public float HatPeak = 0.45f;
        /// <summary>Dome only: radius of the concave curve where the dome meets the block's top edge (0 = a quarter of the height).</summary>
        public float HatFillet;
        public Func<Block, bool> IsHat;

        /// <summary>The height of the cap drawn above <paramref name="b"/> (0 for none).</summary>
        public float HatFor(Block b)
            => HatHeight > 0f && IsHat != null && b.Previous == null && b.Output == null && IsHat(b) ? HatHeight : 0f;

        /// <summary>Appends the cap along a block's top edge, from the top-left corner (already added) to where the flat edge resumes.</summary>
        internal void AddHat(List<Vec2> p, float x, float y, float blockWidth)
        {
            if (HatStyle == HatShape.Dome) { AddDome(p, x, y, blockWidth); return; }
            float width = HatWidth > 0f ? Math.Min(HatWidth, blockWidth) : blockWidth * 0.6f;
            float start = CornerRadius > 0f ? Math.Min(2f * CornerRadius, width * 0.3f) : 0f;   // a flat run keeps the rounded corner round
            float len = width - start;
            if (start > 0f) p.Add(new Vec2(x + start, y));
            float x0 = x + start;
            const int n = 20;
            for (int i = 1; i <= n; i++)
            {
                // a raised cosine: level at both ends, so the cap melts into the flat edge instead of ending in a cliff
                float t = (float)i / n;
                float rise = (1f - (float)Math.Cos(2.0 * Math.PI * t)) * 0.5f;
                p.Add(new Vec2(x0 + t * len, y - HatHeight * rise));
            }
        }

        void AddDome(List<Vec2> p, float x, float y, float blockWidth)
        {
            float width = HatWidth > 0f ? Math.Min(HatWidth, blockWidth) : blockWidth * 0.62f;
            float f = Math.Min(HatFillet > 0f ? HatFillet : HatHeight * 0.25f, Math.Min(HatHeight * 0.6f, width * 0.3f));
            float rx1 = Math.Max(width * Math.Min(Math.Max(HatPeak, 0.1f), 0.9f), 1f);
            float rx2 = Math.Max(width - rx1 - f, 1f);
            float top = y - HatHeight, ry2 = HatHeight - f;
            const int steps = 14;
            // up the left edge and over: a quarter ellipse that leaves the block's top-left corner vertically and levels out at the peak
            for (int i = 1; i <= steps; i++)
            {
                double a = Math.PI * 0.5 * i / steps;
                p.Add(new Vec2(x + rx1 - (float)Math.Cos(a) * rx1, y - (float)Math.Sin(a) * HatHeight));
            }
            // down the other side: a quarter ellipse to just above the edge ...
            for (int i = 1; i <= steps; i++)
            {
                double a = Math.PI * 0.5 * i / steps;
                p.Add(new Vec2(x + rx1 + (float)Math.Sin(a) * rx2, top + (1f - (float)Math.Cos(a)) * ry2));
            }
            // ... and a concave fillet into the flat edge
            float fx = x + rx1 + rx2;
            for (int i = 1; i <= 6; i++)
            {
                double a = Math.PI * 0.5 * i / 6;
                p.Add(new Vec2(fx + f - (float)Math.Cos(a) * f, y - f + (float)Math.Sin(a) * f - 0f));
            }
        }

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
