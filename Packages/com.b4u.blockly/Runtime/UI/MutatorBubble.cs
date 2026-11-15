using System.Collections.Generic;
using Blockly.Editing;
using Blockly.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>A click on the gear icon of a block opens or closes its mutator bubble. A drag that starts on the icon moves the block (it bubbles up to its <see cref="BlockDragTarget"/>).</summary>
    public sealed class MutatorIconTarget : MonoBehaviour, IPointerClickHandler
    {
        WorkspaceView view;
        Block block;

        public Block Block => block;
        public void Init(WorkspaceView owner, Block b) { view = owner; block = b; }

        public void OnPointerClick(PointerEventData e)
        {
            if (e != null && e.button != PointerEventData.InputButton.Left) return;
            if (view != null) view.ToggleMutatorBubble(block);
        }
    }

    /// <summary>
    /// The bubble that changes the shape of an if, join or list block: add buttons on top, then one row per part (else if, else, item) with move up,
    /// move down and remove. A button changes the block (one undo step) and the view rebuilds, which builds the bubble again for the new shape.
    /// Blockly does this with a small workspace and dragging; buttons do the same without the fiddly dragging on a phone.
    /// </summary>
    public sealed class MutatorBubble : MonoBehaviour
    {
        public const string BubbleName = "mutator bubble";
        public const float PreferredWidth = 210f, Margin = 8f, HeaderHeight = 20f, Border = 4f, RowHeight = 26f, ButtonSize = 22f, ButtonGap = 3f;

        Block block;

        public Block Block => block;
        /// <summary>The buttons of the bubble by name, for tests: "add else if", "add else", "add item", and "remove 1" / "up 1" / "down 1" per part (kind and index, e.g. "remove else", "up item 2").</summary>
        public readonly Dictionary<string, Button> Buttons = new Dictionary<string, Button>();

        /// <summary>Builds the bubble of <paramref name="block"/> beside <paramref name="box"/> (beyond <paramref name="right"/>), starting <paramref name="top"/> units below the block's top.</summary>
        public static MutatorBubble Create(WorkspaceView view, Transform parent, Block block, Layout.BlockBox box, float right, float top, Rect visible, BlockStyle.BlockLook look)
        {
            var kind = MutatorEditing.KindOf(block);
            var parts = MutatorEditing.Parts(block);
            int rows = parts.Count + (kind == MutatorEditing.Kind.If ? 1 : 0);       // an if shows its fixed first branch too
            bool limited = visible.width > 0f && visible.height > 0f;
            float w = limited ? Mathf.Min(PreferredWidth, Mathf.Max(120f, visible.width - 2 * Margin)) : PreferredWidth;
            float h = HeaderHeight + RowHeight + rows * RowHeight + Border * 2 + 4f;

            var go = new GameObject(BubbleName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            var pos = CommentBubble.Place(box, right, w, h, visible, limited);
            pos.y = limited ? Mathf.Clamp(box.Y + top, visible.yMin + Margin, Mathf.Max(visible.yMin + Margin, visible.yMax - Margin - h)) : box.Y + top;
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = new Vector2(w, h);
            go.GetComponent<Image>().color = look.Fill;

            var b = go.AddComponent<MutatorBubble>();
            b.block = block;

            var body = Child("body", rt);
            body.gameObject.AddComponent<Image>().color = look.InputFill;
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(Border, Border); body.offsetMax = new Vector2(-Border, -HeaderHeight);

            // header: the title, and the close button
            string title = kind == MutatorEditing.Kind.If ? Loc.T("if") : Loc.T(block.Type == "text_join" ? "create text with" : "create list with");
            Label(rt, "title", title, 12f, Color.white, TextAlignmentOptions.MidlineLeft, Border + 2, 0, w - HeaderHeight - Border, HeaderHeight);
            var close = Button(rt, "close", w - HeaderHeight, 0, HeaderHeight, HeaderHeight, Color.clear);
            Label((RectTransform)close.transform, "x", "×", 14f, Color.white, TextAlignmentOptions.Center, 0, 0, HeaderHeight, HeaderHeight);
            close.onClick.AddListener(() => view.ToggleMutatorBubble(block));

            // add buttons
            float y = Border;                                  // the body starts below the header already
            float x = Border + 2;
            if (kind == MutatorEditing.Kind.If)
            {
                b.Add(view, body, "add else if", "+ " + Loc.T("else if"), x, y, 92f, look, () => view.MutatorAdd(block, PartKind.ElseIf));
                if (!MutatorEditing.HasElse(block))
                    b.Add(view, body, "add else", "+ " + Loc.T("else"), x + 92f + ButtonGap * 2, y, 70f, look, () => view.MutatorAdd(block, PartKind.Else));
            }
            else
                b.Add(view, body, "add item", "+ " + Loc.T("item"), x, y, 92f, look, () => view.MutatorAdd(block, PartKind.Item));
            y += RowHeight;

            // the parts
            if (kind == MutatorEditing.Kind.If)
            {
                Label(body, "row", Loc.T("if"), 12f, look.InputText, TextAlignmentOptions.MidlineLeft, 6, y, w - 2 * Border - 12, RowHeight);      // the first branch is fixed
                y += RowHeight;
            }
            for (int i = 0; i < parts.Count; i++, y += RowHeight)
            {
                var part = parts[i];
                bool canUp = part.Kind != PartKind.Else && i > 0 && parts[i - 1].Kind == part.Kind;
                bool canDown = part.Kind != PartKind.Else && i + 1 < parts.Count && parts[i + 1].Kind == part.Kind;
                string name = part.Kind == PartKind.ElseIf ? "else if " + part.Index : part.Kind == PartKind.Else ? "else" : "item " + (part.Index + 1);
                string text = part.Kind == PartKind.ElseIf ? Loc.T("else if") : part.Kind == PartKind.Else ? Loc.T("else") : Loc.T("item");
                b.Row(view, body, text, y, w, look, name, canUp, canDown, part);
            }
            return b;
        }

        void Row(WorkspaceView view, RectTransform body, string text, float y, float w, BlockStyle.BlockLook look, string name, bool up, bool down, MutatorPart part)
        {
            Label(body, "row", text, 12f, look.InputText, TextAlignmentOptions.MidlineLeft, 6, y, w - 2 * Border - 12, RowHeight);
            float bx = w - 2 * Border - 3 * (ButtonSize + ButtonGap);
            Arrow(view, body, "up " + name, bx, y + 2, true, up, look, () => view.MutatorMove(block, part, -1));
            Arrow(view, body, "down " + name, bx + ButtonSize + ButtonGap, y + 2, false, down, look, () => view.MutatorMove(block, part, 1));
            Add(view, body, "remove " + name, "−", bx + 2 * (ButtonSize + ButtonGap), y + 2, ButtonSize, look, () => view.MutatorRemove(block, part));
        }

        void Add(WorkspaceView view, RectTransform parent, string name, string text, float x, float y, float width, BlockStyle.BlockLook look, UnityEngine.Events.UnityAction onClick)
        {
            var btn = Button(parent, name, x, y + 2, width, ButtonSize, look.Stroke);
            Label((RectTransform)btn.transform, "text", text, 12f, Color.white, TextAlignmentOptions.Center, 0, 0, width, ButtonSize);
            btn.onClick.AddListener(onClick);
            Buttons[name] = btn;
        }

        /// <summary>A move button: the arrow is drawn as a triangle, so it does not depend on the font having the glyph.</summary>
        void Arrow(WorkspaceView view, RectTransform parent, string name, float x, float y, bool up, bool enabled, BlockStyle.BlockLook look, UnityEngine.Events.UnityAction onClick)
        {
            var btn = Button(parent, name, x, y, ButtonSize, ButtonSize, look.Stroke);
            var tri = Child("arrow", (RectTransform)btn.transform);
            tri.anchorMin = tri.anchorMax = new Vector2(0, 1); tri.pivot = new Vector2(0, 1);
            tri.anchoredPosition = Vector2.zero; tri.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            float c = ButtonSize / 2f, r = 5f;
            var pts = up ? new[] { new Vec2(c - r, c + r * 0.6f), new Vec2(c + r, c + r * 0.6f), new Vec2(c, c - r * 0.8f) }
                         : new[] { new Vec2(c - r, c - r * 0.6f), new Vec2(c + r, c - r * 0.6f), new Vec2(c, c + r * 0.8f) };
            var g = tri.gameObject.AddComponent<PolygonGraphic>();
            g.SetShape(pts, Color.white);
            g.raycastTarget = false;
            btn.interactable = enabled;
            var img = btn.GetComponent<Image>();
            if (!enabled) { var c2 = img.color; c2.a = 0.35f; img.color = c2; g.color = new Color(1, 1, 1, 0.5f); }
            btn.onClick.AddListener(onClick);
            Buttons[name] = btn;
        }

        // ------------------------------------------------------------------ small builders
        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static Button Button(RectTransform parent, string name, float x, float y, float w, float h, Color colour)
        {
            var rt = Child(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour.a == 0f ? new Color(1, 1, 1, 0.001f) : colour;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }

        static void Label(RectTransform parent, string name, string text, float size, Color colour, TextAlignmentOptions align, float x, float y, float w, float h)
        {
            var rt = Child(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = colour; t.alignment = align;
            if (BlockStyle.Theme.Font != null) t.font = BlockStyle.Theme.Font;
            t.fontStyle = BlockStyle.FontStyle;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
        }
    }
}
