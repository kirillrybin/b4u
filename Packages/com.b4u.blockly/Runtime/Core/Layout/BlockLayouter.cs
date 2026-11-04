using System;
using System.Collections.Generic;

namespace Blockly.Layout
{
    /// <summary>A text or editable field placed on a block (absolute workspace coordinates, y down).</summary>
    public sealed class FieldBox
    {
        public Field Field;
        public string Text;
        public float X, Y, W, H;
        public bool Editable;      // draws a field background; labels are plain text
        public bool IsDropdown;
    }

    /// <summary>Hole for an inline value input; the child block is drawn on top of it.</summary>
    public sealed class SlotBox
    {
        public Input Input;
        public Vec2[] Polygon;
    }

    public sealed class BlockBox
    {
        public Block Block;
        public bool IsShadow;
        public float X, Y, W, H;
        public Vec2[] Outline;                     // clockwise in y-down coordinates
        public readonly List<FieldBox> Fields = new List<FieldBox>();
        public readonly List<SlotBox> Slots = new List<SlotBox>();
        /// <summary>
        /// Where each of the block's connections sits (workspace coordinates). A parent's connection and the
        /// connected child's connection are at the same point, which is what snapping measures against.
        /// </summary>
        public readonly Dictionary<Connection, Vec2> Connections = new Dictionary<Connection, Vec2>();
    }

    public sealed class LayoutResult
    {
        /// <summary>Draw order: parents before their children, a block before its next blocks.</summary>
        public readonly List<BlockBox> Boxes = new List<BlockBox>();
        public readonly Dictionary<Block, BlockBox> ByBlock = new Dictionary<Block, BlockBox>();
        public float Left, Top, Right, Bottom;
    }

    /// <summary>
    /// Computes where everything on a block stack goes, following Blockly's row model: inputs are grouped into
    /// rows, value inputs are either external (tab on the right edge) or inline (hole inside the row), and
    /// statement inputs form the C-shaped arms.
    /// </summary>
    public sealed class BlockLayouter
    {
        readonly Workspace workspace;
        readonly ITextMeasurer text;

        public BlockLayouter(Workspace workspace, ITextMeasurer text) { this.workspace = workspace; this.text = text; }

        // ------------------------------------------------------------------ measured tree
        sealed class Item
        {
            public Field Field; public string Text; public bool Editable, Dropdown;
            public Input Slot; public Measured SlotChild;
            public float W, H;
        }

        sealed class Row
        {
            public readonly List<Item> Items = new List<Item>();
            public Input External; public Measured ExternalChild;
            public Input Statement; public List<Measured> Chain;
            public float W, H, Y, InnerH;
        }

        sealed class Measured
        {
            public Block Block; public bool Shadow;
            public readonly List<Row> Rows = new List<Row>();
            public float W, H, StmtX, TopPad, BottomPad;
            /// <summary>Width including values plugged into external inputs, which hang off the right edge.</summary>
            public float TotalW;
        }

        // ------------------------------------------------------------------ public API
        public LayoutResult LayoutStack(Block first, float x, float y, LayoutResult into = null)
        {
            var res = into ?? new LayoutResult { Left = x, Top = y, Right = x, Bottom = y };
            var chain = MeasureChain(first);
            PlaceChain(chain, x, y, res);
            return res;
        }

        /// <summary>
        /// Like Blockly's workspace.cleanUp(): moves the top blocks into one column, keeping their current top-to-bottom
        /// order (which is also the order the interpreter runs them in).
        /// </summary>
        public void CleanUp(float gap = 24f)
        {
            var order = new List<KeyValuePair<int, Block>>();
            for (int i = 0; i < workspace.TopBlocks.Count; i++) order.Add(new KeyValuePair<int, Block>(i, workspace.TopBlocks[i]));
            order.Sort((a, b) => { int c = a.Value.Y.CompareTo(b.Value.Y); return c != 0 ? c : a.Key.CompareTo(b.Key); });

            float y = 0;
            foreach (var kv in order)
            {
                var probe = LayoutStack(kv.Value, 0, 0);
                kv.Value.X = Metrics.TabWidth;      // leave room for an output tab
                kv.Value.Y = y;
                y += probe.Bottom + Metrics.NotchDepth + gap;
            }
        }

        public LayoutResult LayoutWorkspace()
        {
            var res = new LayoutResult { Left = float.MaxValue, Top = float.MaxValue, Right = float.MinValue, Bottom = float.MinValue };
            foreach (var top in workspace.TopBlocks)
                LayoutStack(top, (float)top.X, (float)top.Y, res);
            if (res.Boxes.Count == 0) { res.Left = res.Top = res.Right = res.Bottom = 0; }
            return res;
        }

        // ------------------------------------------------------------------ measuring
        List<Measured> MeasureChain(Block first)
        {
            var list = new List<Measured>();
            for (var b = first; b != null; b = b.NextBlock) list.Add(Measure(b, false));
            return list;
        }

        static float ChainHeight(List<Measured> chain)
        {
            float h = 0;
            foreach (var m in chain) h += m.H;
            return h;
        }

        static Block ChildOf(Connection c, out bool shadow)
        {
            shadow = false;
            if (c?.Target != null) return c.Target.Owner;
            if (c?.Shadow != null) { shadow = true; return c.Shadow; }
            return null;
        }

        Measured Measure(Block block, bool shadow)
        {
            var m = new Measured { Block = block, Shadow = shadow };
            bool inline = block.InputsInline;

            Row cur = null;
            void Close() { if (cur != null) { m.Rows.Add(cur); cur = null; } }

            foreach (var input in block.Inputs)
            {
                if (input.Kind == InputKind.Statement)
                {
                    Close();
                    var row = new Row { Statement = input };
                    AddFields(row, input);
                    var child = ChildOf(input.Connection, out _);
                    row.Chain = child != null ? MeasureChain(child) : new List<Measured>();
                    m.Rows.Add(row);
                    continue;
                }

                if (cur == null) cur = new Row();
                AddFields(cur, input);

                if (input.Kind == InputKind.Value)
                {
                    var child = ChildOf(input.Connection, out bool childShadow);
                    var cm = child != null ? Measure(child, childShadow) : null;
                    if (inline)
                    {
                        float w = Metrics.TabWidth + Math.Max(cm?.TotalW ?? 0, Metrics.SlotMinWidth);
                        float h = Math.Max(cm?.H ?? 0, Metrics.SlotMinHeight);
                        cur.Items.Add(new Item { Slot = input, SlotChild = cm, W = w, H = h });
                    }
                    else
                    {
                        cur.External = input;
                        cur.ExternalChild = cm;
                        Close();
                    }
                }
                else if (!inline)
                {
                    Close();            // a dummy input ends its row on a non-inline block
                }
            }
            Close();
            if (m.Rows.Count == 0) m.Rows.Add(new Row());

            // Statement arms line up: the inner edge sits right of the widest statement-row label.
            float stmtLabel = 0;
            foreach (var r in m.Rows)
                if (r.Statement != null) stmtLabel = Math.Max(stmtLabel, ItemsWidth(r));
            m.StmtX = Metrics.PadLeft + stmtLabel + (stmtLabel > 0 ? Metrics.ItemGap : 0);

            float width = Metrics.PadLeft + Metrics.PadRight;
            foreach (var r in m.Rows)
            {
                float itemsH = 0;
                foreach (var it in r.Items) itemsH = Math.Max(itemsH, it.Slot != null ? it.H + 2 * Metrics.SlotPadY : it.H);
                if (r.Statement != null)
                {
                    r.InnerH = Math.Max(Metrics.StatementMinHeight, ChainHeight(r.Chain));
                    r.H = r.InnerH;
                    r.W = m.StmtX + Metrics.StatementMinWidth;
                }
                else
                {
                    float extH = r.ExternalChild != null ? r.ExternalChild.H : 0;
                    r.H = Math.Max(Metrics.RowMinHeight, Math.Max(itemsH, extH));
                    r.W = Metrics.PadLeft + ItemsWidth(r) + Metrics.PadRight + (r.External != null ? Metrics.TabWidth : 0);
                }
                width = Math.Max(width, r.W);
            }
            m.W = width;
            m.TotalW = width;
            foreach (var r in m.Rows)
                if (r.ExternalChild != null) m.TotalW = Math.Max(m.TotalW, m.W + r.ExternalChild.TotalW);

            m.TopPad = m.Rows[0].Statement != null ? Metrics.TopBarHeight : 0;
            m.BottomPad = m.Rows[m.Rows.Count - 1].Statement != null ? Metrics.BottomBarHeight : 0;
            float y = m.TopPad;
            for (int i = 0; i < m.Rows.Count; i++)
            {
                var r = m.Rows[i];
                // Two C-arms in a row (if / else) need a bar between them, otherwise the outline pinches to a point.
                if (i > 0 && r.Statement != null && m.Rows[i - 1].Statement != null) y += Metrics.StatementGap;
                r.Y = y; y += r.H;
            }
            m.H = y + m.BottomPad;
            return m;
        }

        void AddFields(Row row, Input input)
        {
            foreach (var f in input.Fields)
            {
                var it = new Item { Field = f };
                bool label = f.IsLabel || f.Spec.FieldType == "field_label_serializable";
                it.Text = DisplayText(f);
                it.Editable = !label;
                it.Dropdown = f.Spec.FieldType == "field_dropdown" || f.Spec.FieldType == "field_variable";
                if (f.Spec.FieldType == "field_checkbox")
                {
                    it.W = Metrics.CheckboxWidth; it.H = Metrics.FieldHeight;
                }
                else if (label)
                {
                    it.W = text.Width(it.Text); it.H = Metrics.LabelHeight;
                }
                else
                {
                    it.W = text.Width(it.Text) + (it.Dropdown ? 14f : 0f) + 2 * Metrics.FieldPadX; it.H = Metrics.FieldHeight;
                }
                if (label && it.Text.Length == 0) continue;
                row.Items.Add(it);
            }
        }

        /// <summary>Blockly prefixes some labels with bidi control marks (U+200F); they have no glyph, so drop them.</summary>
        static string StripBidi(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                if (!(c == '\u200E' || c == '\u200F' || (c >= '\u202A' && c <= '\u202E') || (c >= '\u2066' && c <= '\u2069'))) sb.Append(c);
            return sb.ToString();
        }

        string DisplayText(Field f) => StripBidi(RawDisplayText(f));

        string RawDisplayText(Field f)
        {
            switch (f.Spec.FieldType)
            {
                case "field_variable": return workspace.GetVariableById(f.Value)?.Name ?? f.Value ?? "";
                case "field_checkbox": return f.Value == "TRUE" ? "✓" : "";
                case "field_dropdown":
                    if (f.Spec.Options != null)
                        foreach (var o in f.Spec.Options) if (o.Value == f.Value) return o.Label;
                    return f.Value ?? "";
                default: return f.Value ?? "";
            }
        }

        static float ItemsWidth(Row r)
        {
            float w = 0;
            for (int i = 0; i < r.Items.Count; i++) w += r.Items[i].W + (i > 0 ? Metrics.ItemGap : 0);
            return w;
        }

        // ------------------------------------------------------------------ placing
        void PlaceChain(List<Measured> chain, float x, float y, LayoutResult res)
        {
            foreach (var m in chain) { Place(m, x, y, res); y += m.H; }
        }

        void Place(Measured m, float x, float y, LayoutResult res)
        {
            var box = new BlockBox { Block = m.Block, IsShadow = m.Shadow, X = x, Y = y, W = m.W, H = m.H };
            res.Boxes.Add(box);
            res.ByBlock[m.Block] = box;
            res.Left = Math.Min(res.Left, x - (m.Block.Output != null ? Metrics.TabWidth : 0));
            res.Top = Math.Min(res.Top, y);
            res.Right = Math.Max(res.Right, x + m.W);
            res.Bottom = Math.Max(res.Bottom, y + m.H);

            var b = m.Block;
            if (b.Output != null) box.Connections[b.Output] = new Vec2(x, y + Metrics.TabTop);
            if (b.Previous != null) box.Connections[b.Previous] = new Vec2(x, y);
            if (b.Next != null) box.Connections[b.Next] = new Vec2(x, y + m.H);

            var children = new List<Action>();   // children are placed after the parent so they draw on top
            foreach (var r in m.Rows)
            {
                float rowY = y + r.Y;
                float cx = x + Metrics.PadLeft;
                if (r.External?.Connection != null) box.Connections[r.External.Connection] = new Vec2(x + m.W, rowY + Metrics.TabTop);
                if (r.Statement?.Connection != null) box.Connections[r.Statement.Connection] = new Vec2(x + m.StmtX, rowY);
                foreach (var it in r.Items)
                {
                    if (it.Slot != null)
                    {
                        float sy = rowY + (r.H - it.H) / 2f;
                        if (it.Slot.Connection != null) box.Connections[it.Slot.Connection] = new Vec2(cx + Metrics.TabWidth, sy + Metrics.TabTop);
                        box.Slots.Add(new SlotBox { Input = it.Slot, Polygon = SlotPolygon(cx, sy, it.W, it.H) });
                        if (it.SlotChild != null)
                        {
                            var child = it.SlotChild; float childX = cx + Metrics.TabWidth, childY = sy;
                            children.Add(() => Place(child, childX, childY, res));
                        }
                    }
                    else
                    {
                        float fy = rowY + (r.Statement != null ? (Metrics.RowMinHeight - it.H) / 2f : (r.H - it.H) / 2f);
                        box.Fields.Add(new FieldBox
                        {
                            Field = it.Field, Text = it.Text, X = cx, Y = fy, W = it.W, H = it.H,
                            Editable = it.Editable, IsDropdown = it.Dropdown,
                        });
                    }
                    cx += it.W + Metrics.ItemGap;
                }
                if (r.ExternalChild != null)
                {
                    var child = r.ExternalChild; float childX = x + m.W, childY = rowY;
                    children.Add(() => Place(child, childX, childY, res));
                }
                if (r.Statement != null && r.Chain.Count > 0)
                {
                    var chain = r.Chain; float chainX = x + m.StmtX, chainY = rowY;
                    children.Add(() => PlaceChain(chain, chainX, chainY, res));
                }
            }
            box.Outline = Outline(m, x, y);
            foreach (var c in children) c();
        }

        // ------------------------------------------------------------------ shapes
        // Puzzle tab as (dx, dy) pairs going down; negative dx points to the left.
        static readonly float[,] Tab = { { 0, 0 }, { -5, 1.5f }, { -8, 4.5f }, { -8, 10.5f }, { -5, 13.5f }, { 0, 15 } };

        static Vec2[] SlotPolygon(float x, float y, float w, float h)
        {
            // Hole (including the tab-shaped bump on its left) that an inline child block plugs into.
            float l = x + Metrics.TabWidth, r = x + w, t = y, b = y + h, ty = y + Metrics.TabTop;
            var p = new List<Vec2> { new Vec2(l, t), new Vec2(r, t), new Vec2(r, b), new Vec2(l, b), new Vec2(l, ty + Metrics.TabHeight) };
            for (int i = 4; i >= 0; i--) p.Add(new Vec2(x + Metrics.TabWidth + Tab[i, 0], ty + Tab[i, 1]));
            return p.ToArray();
        }

        Vec2[] Outline(Measured m, float x, float y)
        {
            var p = new List<Vec2>();
            float w = m.W, h = m.H;
            Block b = m.Block;

            p.Add(new Vec2(x, y));
            if (b.Previous != null)
            {
                p.Add(new Vec2(x + Metrics.NotchX, y));
                p.Add(new Vec2(x + Metrics.NotchX + 6, y + Metrics.NotchDepth));
                p.Add(new Vec2(x + Metrics.NotchX + 9, y + Metrics.NotchDepth));
                p.Add(new Vec2(x + Metrics.NotchX + Metrics.NotchWidth, y));
            }
            p.Add(new Vec2(x + w, y));

            foreach (var r in m.Rows)
            {
                float rt = y + r.Y;
                if (r.External != null)
                {
                    p.Add(new Vec2(x + w, rt + Metrics.TabTop));
                    for (int i = 0; i < Tab.GetLength(0); i++) p.Add(new Vec2(x + w + Tab[i, 0], rt + Metrics.TabTop + Tab[i, 1]));
                }
                else if (r.Statement != null)
                {
                    float sx = x + m.StmtX;
                    p.Add(new Vec2(x + w, rt));
                    p.Add(new Vec2(sx + Metrics.NotchX + Metrics.NotchWidth, rt));
                    p.Add(new Vec2(sx + Metrics.NotchX + 9, rt + Metrics.NotchDepth));
                    p.Add(new Vec2(sx + Metrics.NotchX + 6, rt + Metrics.NotchDepth));
                    p.Add(new Vec2(sx + Metrics.NotchX, rt));
                    p.Add(new Vec2(sx, rt));
                    p.Add(new Vec2(sx, rt + r.InnerH));
                    p.Add(new Vec2(x + w, rt + r.InnerH));
                }
            }

            p.Add(new Vec2(x + w, y + h));
            if (b.Next != null)
            {
                p.Add(new Vec2(x + Metrics.NotchX + Metrics.NotchWidth, y + h));
                p.Add(new Vec2(x + Metrics.NotchX + 9, y + h + Metrics.NotchDepth));
                p.Add(new Vec2(x + Metrics.NotchX + 6, y + h + Metrics.NotchDepth));
                p.Add(new Vec2(x + Metrics.NotchX, y + h));
            }
            p.Add(new Vec2(x, y + h));

            if (b.Output != null)
            {
                p.Add(new Vec2(x, y + Metrics.TabTop + Metrics.TabHeight));
                for (int i = Tab.GetLength(0) - 1; i >= 0; i--) p.Add(new Vec2(x + Tab[i, 0], y + Metrics.TabTop + Tab[i, 1]));
            }
            return Geometry.Simplify(p).ToArray();
        }
    }
}
