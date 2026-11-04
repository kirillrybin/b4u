using System;
using System.Collections.Generic;
using Blockly.Editing;
using Blockly.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    public sealed class WorkspaceView : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        Workspace workspace;
        TmpTextMeasurer measurer;
        TMP_Text measureTemplate;
        readonly Dictionary<Block, RectTransform> blockObjects = new Dictionary<Block, RectTransform>();

        RectTransform highlightObject;
        PolygonGraphic highlightShape;

        // drag & drop
        BlockDragger dragger;
        Vector2 grabOffset;
        RectTransform snapMarker;
        PolygonGraphic snapShape;
        readonly List<RectTransform> draggedObjects = new List<RectTransform>();

        /// <summary>A drag started (the program should stop running; the model is about to change).</summary>
        public event Action<Block> BlockDragStarted;
        /// <summary>The user changed the workspace (a block was dropped, moved or reconnected).</summary>
        public event Action WorkspaceEdited;

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

        public bool IsDragging => dragger != null && dragger.IsDragging;
        public Block DraggedBlock => dragger?.Dragged;
        public SnapCandidate? SnapCandidate => dragger?.Candidate;
        public static readonly Color SnapColour = new Color(1f, 0.85f, 0.1f, 0.95f);

        // ------------------------------------------------------------------ coordinates
        /// <summary>Screen point to workspace units (x right, y down from the workspace's top-left corner).</summary>
        public Vector2 ScreenToWorkspace(Vector2 screen, Camera cam)
        {
            var rt = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, cam, out var local);
            var r = rt.rect;
            return new Vector2(local.x - r.xMin, r.yMax - local.y);
        }

        public Vector2 WorkspaceToScreen(Vector2 workspacePoint, Camera cam)
        {
            var rt = (RectTransform)transform;
            var r = rt.rect;
            var world = rt.TransformPoint(new Vector3(r.xMin + workspacePoint.x, r.yMax - workspacePoint.y, 0));
            return RectTransformUtility.WorldToScreenPoint(cam, world);
        }

        // ------------------------------------------------------------------ dragging blocks
        /// <summary>Starts dragging <paramref name="block"/> (and the blocks after it) grabbed at <paramref name="workspacePoint"/>.</summary>
        public bool BeginDrag(Block block, Vector2 workspacePoint)
        {
            if (workspace == null || IsDragging || block == null) return false;
            EnsureMeasurer();
            var d = new BlockDragger(workspace, new BlockLayouter(workspace, measurer));
            if (!d.Begin(block)) return false;
            dragger = d;
            BlockDragStarted?.Invoke(block);

            Rebuild();                                   // the old parent shrank now that the block left it
            grabOffset = workspacePoint - new Vector2((float)block.X, (float)block.Y);
            ReorderDraggedToFront();
            UpdateSnapMarker();
            return true;
        }

        public void DragTo(Vector2 workspacePoint)
        {
            if (!IsDragging) return;
            var target = workspacePoint - grabOffset;
            dragger.MoveTo(target.x, target.y);
            ApplyDraggedLayout();
            UpdateSnapMarker();
        }

        public void EndDrag()
        {
            if (!IsDragging) return;
            dragger.End();
            FinishDrag();
        }

        public void CancelDrag()
        {
            if (!IsDragging) return;
            dragger.Cancel();
            FinishDrag();
        }

        void FinishDrag()
        {
            dragger = null;
            draggedObjects.Clear();
            Rebuild();
            WorkspaceEdited?.Invoke();
        }

        void ReorderDraggedToFront()
        {
            draggedObjects.Clear();
            if (Layout == null) return;
            foreach (var box in Layout.Boxes)
                if (dragger.IsBeingDragged(box.Block) && blockObjects.TryGetValue(box.Block, out var rt))
                {
                    rt.SetAsLastSibling();               // layout order keeps parents beneath their children
                    draggedObjects.Add(rt);
                }
        }

        /// <summary>Moves the dragged blocks' objects to where the core says they are (no rebuild while dragging).</summary>
        void ApplyDraggedLayout()
        {
            var layout = dragger.DraggedLayout;
            if (layout == null) return;
            foreach (var box in layout.Boxes)
                if (blockObjects.TryGetValue(box.Block, out var rt))
                    rt.anchoredPosition = new Vector2(box.X, -box.Y);
        }

        void UpdateSnapMarker()
        {
            var cand = dragger?.Candidate;
            if (!cand.HasValue)
            {
                if (snapMarker != null) snapMarker.gameObject.SetActive(false);
                return;
            }
            if (snapMarker == null)
            {
                snapMarker = NewRect("snap", transform, 0, 0, 1, 1);
                snapShape = snapMarker.gameObject.AddComponent<PolygonGraphic>();
                snapShape.raycastTarget = false;
                var circle = new Vec2[16];
                for (int i = 0; i < circle.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / circle.Length;
                    circle[i] = new Vec2(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 7f);
                }
                snapShape.SetShape(circle, SnapColour, new Color(0.45f, 0.35f, 0f, 1f), 1.5f);
            }
            snapMarker.gameObject.SetActive(true);
            snapMarker.anchoredPosition = new Vector2(cand.Value.TargetPoint.X, -cand.Value.TargetPoint.Y);
            snapMarker.SetAsLastSibling();
        }

        // Pointer events are redirected here by BlockDragTarget (the dragged block's own object is rebuilt mid-drag).
        public void OnDrag(PointerEventData e)
        {
            if (IsDragging) DragTo(ScreenToWorkspace(e.position, e.pressEventCamera));
        }

        public void OnEndDrag(PointerEventData e) => EndDrag();

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (IsDragging && UnityEngine.Input.GetKeyDown(KeyCode.Escape)) CancelDrag();   // not Blockly.Input
        }
#endif

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
            snapMarker = null; snapShape = null;
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

            var dragTarget = root.gameObject.AddComponent<BlockDragTarget>();
            dragTarget.Init(this, box.Block);
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
