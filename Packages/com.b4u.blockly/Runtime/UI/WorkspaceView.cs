using System.Collections.Generic;
using Blockly.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// Draws a <see cref="Workspace"/> with UGUI. Every block becomes a small hierarchy
    /// (body polygon, input holes, field backgrounds and TMP texts); children are siblings drawn after their
    /// parent, so inline and statement children appear on top of the parent's holes.
    /// Place it under a Canvas; its RectTransform's top-left is the workspace origin.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorkspaceView : MonoBehaviour
    {
        Workspace workspace;
        TmpTextMeasurer measurer;
        TMP_Text measureTemplate;
        readonly Dictionary<Block, RectTransform> blockObjects = new Dictionary<Block, RectTransform>();

        RectTransform highlightObject;
        PolygonGraphic highlightShape;

        public Workspace Workspace => workspace;
        public LayoutResult Layout { get; private set; }
        /// <summary>The block currently outlined as "about to execute", or null.</summary>
        public Block HighlightedBlock { get; private set; }

        public void SetWorkspace(Workspace ws)
        {
            workspace = ws;
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            if (workspace == null) return;
            EnsureMeasurer();
            Layout = new BlockLayouter(workspace, measurer).LayoutWorkspace();
            foreach (var box in Layout.Boxes) CreateBlock(box);

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(Mathf.Max(0, Layout.Right), Mathf.Max(0, Layout.Bottom));
        }

        public static readonly Color HighlightColour = new Color(1f, 0.85f, 0.1f, 1f);

        /// <summary>Outlines a block (drawn above everything, including its children). Pass null to clear.</summary>
        public void SetHighlight(Block block)
        {
            HighlightedBlock = null;
            BlockBox box = null;
            if (block != null && Layout != null && Layout.ByBlock.TryGetValue(block, out box)) HighlightedBlock = block;

            if (HighlightedBlock == null)
            {
                if (highlightObject != null) highlightObject.gameObject.SetActive(false);
                return;
            }
            if (highlightObject == null)
            {
                highlightObject = NewRect("highlight", transform, box.X, box.Y, box.W, box.H);
                highlightShape = highlightObject.gameObject.AddComponent<PolygonGraphic>();
                highlightShape.raycastTarget = false;
            }
            highlightObject.gameObject.SetActive(true);
            highlightObject.anchoredPosition = new Vector2(box.X, -box.Y);
            highlightObject.sizeDelta = new Vector2(box.W, box.H);
            highlightShape.SetShape(Translate(box.Outline, box.X, box.Y), Color.clear, HighlightColour, 3.5f);
            highlightObject.SetAsLastSibling();
        }

        /// <summary>Arranges the top blocks in one column (Blockly's "Clean up Blocks") and redraws.</summary>
        public void CleanUp()
        {
            if (workspace == null) return;
            EnsureMeasurer();
            new BlockLayouter(workspace, measurer).CleanUp();
            Rebuild();
        }

        void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.gameObject == (measureTemplate != null ? measureTemplate.gameObject : null)) continue;
                DestroyImmediateSafe(child.gameObject);
            }
            blockObjects.Clear();
            highlightObject = null; highlightShape = null; HighlightedBlock = null;
        }

        static void DestroyImmediateSafe(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        void EnsureMeasurer()
        {
            if (measurer != null && measureTemplate != null) return;
            var go = new GameObject("TextMeasure", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            Configure(t);
            t.raycastTarget = false;
            t.alpha = 0f;                       // exists only to ask TMP for preferred sizes
            measureTemplate = t;
            measurer = new TmpTextMeasurer(t);
        }

        static void Configure(TMP_Text t)
        {
            t.fontSize = BlockStyle.FontSize;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
        }

        RectTransform NewRect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Vec2[] Translate(Vec2[] pts, float dx, float dy)
        {
            var r = new Vec2[pts.Length];
            for (int i = 0; i < pts.Length; i++) r[i] = new Vec2(pts[i].X - dx, pts[i].Y - dy);
            return r;
        }

        void CreateBlock(BlockBox box)
        {
            var baseColour = BlockStyle.Parse(box.Block.Definition.ColourHex);
            var root = NewRect(box.Block.Type, transform, box.X, box.Y, box.W, box.H);
            blockObjects[box.Block] = root;

            var body = root.gameObject.AddComponent<PolygonGraphic>();
            body.SetShape(Translate(box.Outline, box.X, box.Y), BlockStyle.Fill(baseColour, box.IsShadow), BlockStyle.Stroke(baseColour, box.IsShadow));

            foreach (var slot in box.Slots)
            {
                var s = NewRect("slot " + slot.Input.Name, root, 0, 0, box.W, box.H);
                s.gameObject.AddComponent<PolygonGraphic>().SetShape(Translate(slot.Polygon, box.X, box.Y), BlockStyle.Hole(baseColour, box.IsShadow), BlockStyle.Stroke(baseColour, box.IsShadow), 1f);
                s.GetComponent<PolygonGraphic>().raycastTarget = false;
            }

            foreach (var f in box.Fields) CreateField(root, box, f, baseColour);
        }

        void CreateField(RectTransform parent, BlockBox box, FieldBox f, Color baseColour)
        {
            float lx = f.X - box.X, ly = f.Y - box.Y;
            var rt = NewRect(f.Field.Name ?? "label", parent, lx, ly, f.W, f.H);

            if (f.Editable)
            {
                bool dropdown = f.IsDropdown;
                var rect = new[] { new Vec2(0, 0), new Vec2(f.W, 0), new Vec2(f.W, f.H), new Vec2(0, f.H) };
                var bg = rt.gameObject.AddComponent<PolygonGraphic>();
                bg.SetShape(rect, dropdown ? BlockStyle.DropdownFill(baseColour, box.IsShadow) : BlockStyle.InputFill,
                            BlockStyle.Stroke(baseColour, box.IsShadow), 1f);
                bg.raycastTarget = false;
            }

            if (f.IsDropdown)
            {
                var arrow = NewRect("arrow", rt, 0, 0, f.W, f.H);
                var ag = arrow.gameObject.AddComponent<PolygonGraphic>();
                float cx = f.W - 9f, cy = f.H * 0.5f;
                ag.SetShape(new[] { new Vec2(cx - 3.5f, cy - 2f), new Vec2(cx + 3.5f, cy - 2f), new Vec2(cx, cy + 2.5f) }, new Color(1f, 1f, 1f, 0.9f));
                ag.raycastTarget = false;
            }

            if (string.IsNullOrEmpty(f.Text)) return;
            var textRt = NewRect("text", rt, 0, 0, f.W, f.H);
            var t = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            Configure(t);
            t.text = f.Text;
            t.color = f.Editable && !f.IsDropdown ? BlockStyle.InputText : BlockStyle.LabelText;
            t.alignment = f.Editable ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            if (f.IsDropdown) t.rectTransform.sizeDelta = new Vector2(f.W - 12f, f.H);
        }
    }
}
