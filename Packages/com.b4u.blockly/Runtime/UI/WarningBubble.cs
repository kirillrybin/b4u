using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>A click on the warning icon of a block opens or closes its bubble. A drag that starts on the icon moves the block (it bubbles up to its <see cref="BlockDragTarget"/>).</summary>
    public sealed class WarningIconTarget : MonoBehaviour, IPointerClickHandler
    {
        WorkspaceView view;
        Block block;

        public Block Block => block;
        public void Init(WorkspaceView owner, Block b) { view = owner; block = b; }

        public void OnPointerClick(PointerEventData e)
        {
            if (e != null && e.button != PointerEventData.InputButton.Left) return;
            if (view != null) view.ToggleWarningBubble(block);
        }
    }

    /// <summary>The bubble with the text of a block's warning: a frame in the block's colour, the text (read only) and a close button. Placed like the comment bubble.</summary>
    public sealed class WarningBubble : MonoBehaviour
    {
        public const string BubbleName = "warning bubble";
        public const float PreferredWidth = 200f, Margin = 8f, HeaderHeight = 20f, Border = 4f, LineHeight = 16f;

        Block block;
        TMP_Text text;

        public Block Block => block;
        public string Text => text != null ? text.text : "";

        /// <summary>Builds the bubble of <paramref name="block"/> beside <paramref name="box"/> (beyond <paramref name="right"/>), starting <paramref name="top"/> units below the block's top.</summary>
        public static WarningBubble Create(WorkspaceView view, Transform parent, Block block, string message, Layout.BlockBox box, float right, float top, Rect visible, BlockStyle.BlockLook look)
        {
            bool limited = visible.width > 0f && visible.height > 0f;
            float w = limited ? Mathf.Min(PreferredWidth, Mathf.Max(100f, visible.width - 2 * Margin)) : PreferredWidth;
            int lines = 0;
            foreach (var l in message.Split('\n')) lines += Mathf.Max(1, Mathf.CeilToInt(l.Length * 6.5f / (w - 2 * Border - 8f)));
            float h = HeaderHeight + Border + lines * LineHeight + 6f;

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

            var b = go.AddComponent<WarningBubble>();
            b.block = block;

            var body = Child("body", rt);
            body.gameObject.AddComponent<Image>().color = look.InputFill;
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(Border, Border); body.offsetMax = new Vector2(-Border, -HeaderHeight);

            var t = Child("text", body).gameObject.AddComponent<TextMeshProUGUI>();
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(4, 2); trt.offsetMax = new Vector2(-4, -2);
            t.text = message;
            t.fontSize = BlockStyle.FontSize;
            if (BlockStyle.Theme.Font != null) t.font = BlockStyle.Theme.Font;
            t.fontStyle = BlockStyle.FontStyle;
            t.color = look.InputText;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = false;
            t.raycastTarget = false;
            b.text = t;

            var close = Child("close", rt);
            close.anchorMin = close.anchorMax = new Vector2(1, 1); close.pivot = new Vector2(1, 1);
            close.anchoredPosition = new Vector2(-2, -2); close.sizeDelta = new Vector2(HeaderHeight - 4, HeaderHeight - 4);
            var cimg = close.gameObject.AddComponent<Image>();
            cimg.color = new Color(1, 1, 1, 0.001f);
            var btn = close.gameObject.AddComponent<Button>();
            btn.targetGraphic = cimg;
            btn.onClick.AddListener(() => view.ToggleWarningBubble(block));
            var x = Child("x", close).gameObject.AddComponent<TextMeshProUGUI>();
            var xrt = (RectTransform)x.transform;
            xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one; xrt.offsetMin = xrt.offsetMax = Vector2.zero;
            x.text = "×"; x.fontSize = 14; x.alignment = TextAlignmentOptions.Center; x.color = Color.white; x.raycastTarget = false;
            if (BlockStyle.Theme.Font != null) x.font = BlockStyle.Theme.Font;
            return b;
        }

        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
