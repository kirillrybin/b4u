using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>A click on the comment icon of a block opens or closes its bubble. A drag that starts on the icon moves the block (it bubbles up to its <see cref="BlockDragTarget"/>).</summary>
    public sealed class CommentIconTarget : MonoBehaviour, IPointerClickHandler
    {
        WorkspaceView view;
        Block block;

        public Block Block => block;
        public void Init(WorkspaceView owner, Block b) { view = owner; block = b; }

        public void OnPointerClick(PointerEventData e)
        {
            if (e != null && e.button != PointerEventData.InputButton.Left) return;
            if (view != null) view.ToggleCommentBubble(block);
        }
    }

    /// <summary>
    /// The text bubble of a block's comment: a frame in the block's colour with a multi-line text field and a close button. It is a child of the
    /// workspace view, so it moves and zooms with the blocks; it sits beside its block and is kept inside the visible area. It is not draggable
    /// and has a fixed size. The text goes into the block as it is typed; the whole edit is one undo step, recorded when the field loses focus.
    /// </summary>
    public sealed class CommentBubble : MonoBehaviour
    {
        public const string BubbleName = "comment bubble";
        public const float PreferredWidth = 200f, Height = 96f, Margin = 8f, HeaderHeight = 20f, Border = 4f;

        WorkspaceView view;
        Block block;
        TMP_InputField input;
        string before;          // the undo snapshot from when the field got focus; null when no edit is open
        Rect visible; bool limited;
        Vector2 homePosition, homeSize;       // where the bubble sits when no keyboard is in the way
        bool docked;                          // moved up so that the on-screen keyboard does not cover it

        public Block Block => block;
        public TMP_InputField Input => input;
        public bool IsEditing => before != null;

        /// <summary>Builds the bubble of <paramref name="block"/> beside <paramref name="box"/> (beyond <paramref name="right"/>, the right edge of the block with what is plugged into it), inside <paramref name="visible"/> (workspace units, y down; empty means no limit).</summary>
        public static CommentBubble Create(WorkspaceView view, Transform parent, Block block, Layout.BlockBox box, float right, Rect visible, BlockStyle.BlockLook look)
        {
            var go = new GameObject(BubbleName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);

            bool limited = visible.width > 0f && visible.height > 0f;
            float w = limited ? Mathf.Min(PreferredWidth, Mathf.Max(80f, visible.width - 2 * Margin)) : PreferredWidth;
            float h = limited ? Mathf.Min(Height, Mathf.Max(60f, visible.height - 2 * Margin)) : Height;
            var pos = Place(box, right, w, h, visible, limited);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = new Vector2(w, h);

            var frame = go.GetComponent<Image>();
            frame.color = look.Fill;                    // the border is the block's own colour

            var inner = Child("body", rt);
            inner.gameObject.AddComponent<Image>().color = look.InputFill;
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(Border, Border); inner.offsetMax = new Vector2(-Border, -HeaderHeight);

            var area = Child("text area", inner);
            area.gameObject.AddComponent<RectMask2D>();
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(4, 2); area.offsetMax = new Vector2(-4, -2);
            var text = Child("text", area).gameObject.AddComponent<TextMeshProUGUI>();
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            text.fontSize = BlockStyle.FontSize;
            if (BlockStyle.Theme.Font != null) text.font = BlockStyle.Theme.Font;
            text.fontStyle = BlockStyle.FontStyle;
            text.color = look.InputText;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = false;
            text.raycastTarget = false;

            var b = go.AddComponent<CommentBubble>();
            b.view = view; b.block = block;
            b.visible = visible; b.limited = limited; b.homePosition = pos; b.homeSize = new Vector2(w, h);

            go.SetActive(false);                                          // finish wiring before TMP_InputField enables
            var field = inner.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.targetGraphic = inner.GetComponent<Image>();
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            field.richText = false;
            field.SetTextWithoutNotify(block.Comment ?? "");
            field.onSelect.AddListener(_ => b.before = view.SnapshotForCommentEdit());
            field.onValueChanged.AddListener(t => { block.Comment = t; });
            field.onEndEdit.AddListener(_ => b.Flush());
            b.input = field;

            // close button in the header strip
            var close = Child("close", rt);
            close.anchorMin = close.anchorMax = new Vector2(1, 1); close.pivot = new Vector2(1, 1);
            close.anchoredPosition = new Vector2(-2, -2); close.sizeDelta = new Vector2(HeaderHeight - 4, HeaderHeight - 4);
            var cimg = close.gameObject.AddComponent<Image>();
            cimg.color = new Color(1, 1, 1, 0.001f);
            var btn = close.gameObject.AddComponent<Button>();
            btn.targetGraphic = cimg;
            btn.onClick.AddListener(() => view.ToggleCommentBubble(block));
            var x = Child("x", close).gameObject.AddComponent<TextMeshProUGUI>();
            var xrt = (RectTransform)x.transform;
            xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one; xrt.offsetMin = xrt.offsetMax = Vector2.zero;
            x.text = "×"; x.fontSize = 14; x.alignment = TextAlignmentOptions.Center; x.color = Color.white; x.raycastTarget = false;
            if (BlockStyle.Theme.Font != null) x.font = BlockStyle.Theme.Font;

            go.SetActive(true);
            return b;
        }

        /// <summary>Where the top-left corner of a bubble of size w×h goes (workspace units): right of the block (beyond <paramref name="right"/>), or left of it when that does not fit; then pulled inside <paramref name="visible"/>.</summary>
        public static Vector2 Place(Layout.BlockBox box, float right, float w, float h, Rect visible, bool limited)
        {
            float x = Mathf.Max(right, box.X + box.W) + Margin, y = box.Y;
            if (!limited) return new Vector2(x, y);
            if (x + w > visible.xMax - Margin) x = box.X - Margin - w;                   // not enough room on the right: try the left
            x = Mathf.Clamp(x, visible.xMin + Margin, Mathf.Max(visible.xMin + Margin, visible.xMax - Margin - w));
            y = Mathf.Clamp(y, visible.yMin + Margin, Mathf.Max(visible.yMin + Margin, visible.yMax - Margin - h));
            return new Vector2(x, y);
        }

        /// <summary>
        /// While the user types on a touch screen the keyboard covers the lower part of the screen: the bubble moves to the top of the visible area
        /// and shrinks to fit above the keyboard, and goes back when typing ends.
        /// </summary>
        void Update()
        {
            // Unity does not always report the keyboard (area stays empty with the native Android one): assume it takes the lower 56% of the screen then (what the Galaxy A13's does in landscape)
            bool typing = before != null && Application.isMobilePlatform && input != null && input.isFocused;
            var rt = (RectTransform)transform;
            if (!typing)
            {
                if (docked) { docked = false; rt.anchoredPosition = new Vector2(homePosition.x, -homePosition.y); rt.sizeDelta = homeSize; }
                return;
            }
            float keyboard = TouchScreenKeyboard.visible && TouchScreenKeyboard.area.height > 0f ? TouchScreenKeyboard.area.height : Screen.height * 0.56f;
            float keyboardTop = view.WorkspaceYAtScreenY(keyboard) - Margin;     // the keyboard is docked at the bottom edge
            var area = view.VisibleArea();                                        // the view may have been scrolled since the bubble was built
            float top = area.height > 0f ? area.yMin + Margin : homePosition.y;
            float height = Mathf.Clamp(keyboardTop - top, 56f, homeSize.y);
            rt.anchoredPosition = new Vector2(homePosition.x, -top);
            rt.sizeDelta = new Vector2(homeSize.x, height);
            docked = true;
        }

        /// <summary>Puts the cursor in the text field.</summary>
        public void Focus()
        {
            if (input == null) return;
            input.Select();
            input.ActivateInputField();
        }

        /// <summary>Ends an open edit: records it as one undo step and tells the view the program changed. Safe to call when nothing is open.</summary>
        public void Flush()
        {
            if (before == null || view == null) return;
            string snapshot = before;
            before = null;
            view.FinishCommentEdit(snapshot);
        }

        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }
    }
}
