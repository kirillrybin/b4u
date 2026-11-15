using Blockly.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// A note on the workspace (Blockly's workspace comment): a yellow sheet with a header strip (drag it to move the note, a triangle to
    /// collapse it, a cross to delete it) and a text field. It lies under the blocks. The text goes into the model as it is typed; the whole
    /// edit is one undo step, recorded when the field loses focus. Not part of the program.
    /// </summary>
    public sealed class WorkspaceNote : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const string NoteName = "note";
        public const float HeaderHeight = 20f, Border = 4f;
        public static readonly Color Paper = new Color(1f, 0.93f, 0.55f), PaperDark = new Color(0.93f, 0.8f, 0.25f), Ink = new Color(0.2f, 0.18f, 0.05f);

        WorkspaceView view;
        WorkspaceComment note;
        TMP_InputField input;
        string before;                       // undo snapshot while the text is being edited
        string dragBefore;                   // undo snapshot while the note is being dragged
        Vector2 grab;

        public WorkspaceComment Comment => note;
        public TMP_InputField Input => input;
        public Button CollapseButton { get; private set; }
        public Button DeleteButton { get; private set; }
        public bool IsEditing => before != null;

        public static WorkspaceNote Create(WorkspaceView view, Transform parent, WorkspaceComment note, bool interactive)
        {
            float h = note.Collapsed ? HeaderHeight : (float)System.Math.Max(note.H, 2 * HeaderHeight);
            var go = new GameObject(NoteName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2((float)note.X, -(float)note.Y);
            rt.sizeDelta = new Vector2((float)note.W, h);
            var frame = go.GetComponent<Image>();
            frame.color = PaperDark;
            frame.raycastTarget = interactive;

            var n = go.AddComponent<WorkspaceNote>();
            n.view = view; n.note = note;

            // header: the title (the first line of the text when collapsed) and the buttons
            var title = Child("title", rt).gameObject.AddComponent<TextMeshProUGUI>();
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            trt.offsetMin = new Vector2(HeaderHeight + 2, -HeaderHeight); trt.offsetMax = new Vector2(-HeaderHeight - 2, 0);
            string firstLine = (note.Text ?? "").Split('\n')[0];
            title.text = note.Collapsed ? firstLine : "";
            title.fontSize = 11; title.color = Ink; title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Ellipsis;
            title.raycastTarget = false; title.richText = false;
            if (BlockStyle.Theme.Font != null) title.font = BlockStyle.Theme.Font;

            if (interactive)
            {
                n.CollapseButton = HeaderButton(rt, "collapse", 2, () => view.ToggleNoteCollapsed(note));
                var tri = Child("triangle", n.CollapseButton.transform).gameObject.AddComponent<PolygonGraphic>();
                var trect = (RectTransform)tri.transform;
                trect.anchorMin = trect.anchorMax = new Vector2(0.5f, 0.5f); trect.pivot = new Vector2(0, 1);      // like every shape: the corner is the origin, y grows down
                trect.sizeDelta = new Vector2(10, 10); trect.anchoredPosition = new Vector2(-5, 5);                // centred in the button
                tri.SetShape(note.Collapsed
                    ? new[] { new Vec2(2, 0), new Vec2(9, 5), new Vec2(2, 10) }              // points right: expand
                    : new[] { new Vec2(0, 2), new Vec2(10, 2), new Vec2(5, 9) }, Ink);       // points down: collapse
                tri.raycastTarget = false;
                n.DeleteButton = HeaderButton(rt, "delete", -2, () => view.RemoveNote(note), right: true);
                var x = Child("x", n.DeleteButton.transform).gameObject.AddComponent<TextMeshProUGUI>();
                var xrt = (RectTransform)x.transform;
                xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one; xrt.offsetMin = xrt.offsetMax = Vector2.zero;
                x.text = "×"; x.fontSize = 14; x.alignment = TextAlignmentOptions.Center; x.color = Ink; x.raycastTarget = false;
                if (BlockStyle.Theme.Font != null) x.font = BlockStyle.Theme.Font;
            }

            if (!note.Collapsed)
            {
                var body = Child("body", rt);
                body.gameObject.AddComponent<Image>().color = Paper;
                body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
                body.offsetMin = new Vector2(Border, Border); body.offsetMax = new Vector2(-Border, -HeaderHeight);
                var area = Child("text area", body);
                area.gameObject.AddComponent<RectMask2D>();
                area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
                area.offsetMin = new Vector2(4, 2); area.offsetMax = new Vector2(-4, -2);
                var text = Child("text", area).gameObject.AddComponent<TextMeshProUGUI>();
                var tr = (RectTransform)text.transform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                text.fontSize = BlockStyle.FontSize;
                if (BlockStyle.Theme.Font != null) text.font = BlockStyle.Theme.Font;
                text.color = Ink;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.richText = false;
                text.raycastTarget = false;

                go.SetActive(false);                                         // finish wiring before TMP_InputField enables
                var field = body.gameObject.AddComponent<TMP_InputField>();
                field.textViewport = area;
                field.textComponent = text;
                field.targetGraphic = body.GetComponent<Image>();
                field.lineType = TMP_InputField.LineType.MultiLineNewline;
                field.richText = false;
                field.interactable = interactive;
                field.SetTextWithoutNotify(note.Text ?? "");
                field.onSelect.AddListener(_ => n.before = view.SnapshotForCommentEdit());
                field.onValueChanged.AddListener(t => { note.Text = t; });
                field.onEndEdit.AddListener(_ => n.Flush());
                n.input = field;
                go.SetActive(true);
            }
            return n;
        }

        static Button HeaderButton(RectTransform parent, string name, float x, UnityEngine.Events.UnityAction onClick, bool right = false)
        {
            var rt = Child(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1 : 0, 1); rt.pivot = new Vector2(right ? 1 : 0, 1);
            rt.anchoredPosition = new Vector2(x, -2); rt.sizeDelta = new Vector2(HeaderHeight - 4, HeaderHeight - 4);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.001f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            return b;
        }

        bool docked;                         // moved up so that the on-screen keyboard does not cover it

        /// <summary>
        /// While the user types on a touch screen the keyboard covers the lower part of the screen: the note moves to the top of the visible area and
        /// shrinks to fit above the keyboard, and goes back when typing ends (the model keeps its own place).
        /// </summary>
        void Update()
        {
            if (view == null || note == null) return;
            bool typing = before != null && Application.isMobilePlatform && input != null && input.isFocused;
            var rt = (RectTransform)transform;
            float homeH = note.Collapsed ? HeaderHeight : (float)System.Math.Max(note.H, 2 * HeaderHeight);
            if (!typing)
            {
                if (docked) { docked = false; rt.anchoredPosition = new Vector2((float)note.X, -(float)note.Y); rt.sizeDelta = new Vector2((float)note.W, homeH); }
                return;
            }
            // Unity does not always report the native Android keyboard: assume it takes the lower 56% of the screen then
            float keyboard = TouchScreenKeyboard.visible && TouchScreenKeyboard.area.height > 0f ? TouchScreenKeyboard.area.height : Screen.height * 0.56f;
            float margin = 8f;
            float keyboardTop = view.WorkspaceYAtScreenY(keyboard) - margin;
            var area = view.VisibleArea();
            float top = area.height > 0f ? area.yMin + margin : (float)note.Y;
            float left = area.width > 0f ? Mathf.Clamp((float)note.X, area.xMin + margin, Mathf.Max(area.xMin + margin, area.xMax - margin - (float)note.W)) : (float)note.X;
            float height = Mathf.Clamp(keyboardTop - top, 56f, homeH);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2((float)note.W, height);
            docked = true;
        }

        /// <summary>Puts the cursor in the text field (a new note is ready to type into).</summary>
        public void Focus() { if (input != null) { input.Select(); input.ActivateInputField(); } }

        /// <summary>Ends an open text edit: one undo step, and the program counts as changed. Safe to call when nothing is open.</summary>
        public void Flush()
        {
            if (before == null || view == null) return;
            string snapshot = before;
            before = null;
            view.FinishCommentEdit(snapshot);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (view == null || note == null) return;
            dragBefore = view.SnapshotForCommentEdit();
            var p = view.ScreenToWorkspace(e.position, e.pressEventCamera);
            grab = new Vector2(p.x - (float)note.X, p.y - (float)note.Y);
        }

        public void OnDrag(PointerEventData e)
        {
            if (dragBefore == null) return;
            var p = view.ScreenToWorkspace(e.position, e.pressEventCamera);
            note.X = p.x - grab.x; note.Y = p.y - grab.y;
            ((RectTransform)transform).anchoredPosition = new Vector2((float)note.X, -(float)note.Y);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (dragBefore == null) return;
            string snapshot = dragBefore;
            dragBefore = null;
            view.FinishNoteMove(snapshot);
        }

        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
