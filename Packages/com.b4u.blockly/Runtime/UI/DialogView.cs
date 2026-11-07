using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// A small modal dialog: a text prompt (with a validation message that keeps it open) or a yes/no confirmation.
    /// Built from code under the root canvas so it works in any scene and above everything else.
    /// </summary>
    public sealed class DialogView : MonoBehaviour
    {
        Func<string, string> validate;
        Action<string> onSubmit;
        Action onCancel;
        bool closed;

        public TMP_InputField Input { get; private set; }
        public TMP_Text Title { get; private set; }
        public TMP_Text Message { get; private set; }
        public TMP_Text Error { get; private set; }
        public Button OkButton { get; private set; }
        public Button CancelButton { get; private set; }
        public bool IsOpen => !closed;
        public bool IsPrompt => Input != null;

        public string ErrorText => Error != null ? Error.text : "";

        // ------------------------------------------------------------------ factories
        /// <param name="validate">Returns an error message to keep the dialog open, or null to accept.</param>
        public static DialogView ShowPrompt(Canvas anyCanvasInHierarchy, string title, string initial, Func<string, string> validate, Action<string> onOk, Action onCancel = null)
        {
            var d = Create(anyCanvasInHierarchy, title, null, true, "OK");
            d.validate = validate; d.onSubmit = onOk; d.onCancel = onCancel;
            d.Input.SetTextWithoutNotify(initial ?? "");
            d.Input.Select();
            d.Input.ActivateInputField();
            return d;
        }

        public static DialogView ShowConfirm(Canvas anyCanvasInHierarchy, string title, string message, Action onYes, Action onCancel = null, string confirmLabel = "Delete")
        {
            var d = Create(anyCanvasInHierarchy, title, message, false, confirmLabel);
            d.onSubmit = _ => onYes?.Invoke(); d.onCancel = onCancel;
            return d;
        }

        // ------------------------------------------------------------------ actions
        /// <summary>The OK button / Enter: validates, then closes and reports the text.</summary>
        public bool Submit(string text)
        {
            if (closed) return false;
            string problem = validate?.Invoke(text);
            if (problem != null)
            {
                Error.text = problem;
                if (Input != null) { Input.Select(); Input.ActivateInputField(); }
                return false;
            }
            Close();
            onSubmit?.Invoke(text);
            return true;
        }

        public void Confirm() => Submit(Input != null ? Input.text : "");

        public void Cancel()
        {
            if (closed) return;
            Close();
            onCancel?.Invoke();
        }

        void Close()
        {
            closed = true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) Cancel();     // not Blockly.Input
        }
#endif

        // ------------------------------------------------------------------ construction
        static DialogView Create(Canvas anyCanvas, string title, string message, bool prompt, string okLabel)
        {
            var root = anyCanvas.rootCanvas;
            var go = new GameObject("Dialog", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.SetActive(false);                                   // finish wiring before the input field enables
            var rt = (RectTransform)go.transform;
            rt.SetParent(root.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 300;

            var dialog = go.AddComponent<DialogView>();

            var dim = Child(rt, "Dim", typeof(Image));
            Stretch(dim);
            var dimImg = dim.GetComponent<Image>(); dimImg.color = new Color(0, 0, 0, 0.5f);     // swallows clicks behind the dialog

            float height = prompt ? 200f : 170f;
            var panel = Child(rt, "Panel", typeof(Image));
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(440, height);
            panel.GetComponent<Image>().color = BlockStyle.DialogPanel;
            UiShape.Round(panel.GetComponent<Image>(), BlockStyle.PanelCornerRadius);

            dialog.Title = Label(panel, "Title", title, 18, TextAlignmentOptions.TopLeft, Color.white, new Vector2(20, -14), new Vector2(-40, 28));
            if (prompt)
            {
                dialog.Input = MakeInput(panel, new Vector2(20, -56), new Vector2(-40, 36));
                dialog.Error = Label(panel, "Error", "", 14, TextAlignmentOptions.TopLeft, new Color(1f, 0.5f, 0.5f), new Vector2(20, -98), new Vector2(-40, 22));
                dialog.Input.onSubmit.AddListener(t => dialog.Submit(t));
            }
            else
            {
                dialog.Message = Label(panel, "Message", message, 16, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.85f, 0.9f), new Vector2(20, -52), new Vector2(-40, 60));
                dialog.Error = Label(panel, "Error", "", 14, TextAlignmentOptions.TopLeft, Color.clear, new Vector2(20, -120), new Vector2(-40, 1));
            }

            dialog.OkButton = MakeButton(panel, okLabel, new Vector2(-130, 16), new Vector2(110, 36), new Color(0.3f, 0.5f, 0.85f));
            dialog.CancelButton = MakeButton(panel, "Cancel", new Vector2(-12, 16), new Vector2(110, 36), new Color(0.38f, 0.38f, 0.45f));
            dialog.OkButton.onClick.AddListener(dialog.Confirm);
            dialog.CancelButton.onClick.AddListener(dialog.Cancel);

            go.SetActive(true);
            return dialog;
        }

        static RectTransform Child(RectTransform parent, string name, params Type[] components)
        {
            var types = new Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            Array.Copy(components, 0, types, 1, components.Length);
            var rt = (RectTransform)new GameObject(name, types).transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        /// <summary>Top-stretch label: offset = (left, top), size = (width delta, height).</summary>
        static TMP_Text Label(RectTransform parent, string name, string text, float size, TextAlignmentOptions align, Color colour, Vector2 offset, Vector2 sizeDelta)
        {
            var rt = Child(parent, name, typeof(TextMeshProUGUI));
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = offset; rt.sizeDelta = sizeDelta;
            var t = rt.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = colour; t.alignment = align; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        static Button MakeButton(RectTransform parent, string text, Vector2 fromBottomRight, Vector2 size, Color colour)
        {
            var rt = Child(parent, text, typeof(Image), typeof(Button));
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = fromBottomRight; rt.sizeDelta = size;
            var img = rt.GetComponent<Image>(); img.color = colour;
            UiShape.Round(img, BlockStyle.ButtonCornerRadius);
            rt.GetComponent<Button>().targetGraphic = img;
            var l = Label(rt, "Text", text, 16, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.zero);
            Stretch(l.rectTransform);
            return rt.GetComponent<Button>();
        }

        static TMP_InputField MakeInput(RectTransform parent, Vector2 offset, Vector2 sizeDelta)
        {
            var go = Child(parent, "Input", typeof(Image));
            go.anchorMin = new Vector2(0, 1); go.anchorMax = new Vector2(1, 1); go.pivot = new Vector2(0, 1);
            go.anchoredPosition = offset; go.sizeDelta = sizeDelta;
            var bg = go.GetComponent<Image>(); bg.color = Color.white;

            var area = Child(go, "TextArea", typeof(RectMask2D));
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(8, 2); area.offsetMax = new Vector2(-8, -2);
            var text = Child(area, "Text", typeof(TextMeshProUGUI));
            Stretch(text);
            var t = text.GetComponent<TextMeshProUGUI>();
            t.fontSize = 17; t.color = new Color(0.08f, 0.08f, 0.08f); t.alignment = TextAlignmentOptions.MidlineLeft; t.richText = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;

            var input = go.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = t; input.targetGraphic = bg;
            input.lineType = TMP_InputField.LineType.SingleLine; input.richText = false; input.onFocusSelectAll = true;
            return input;
        }
    }
}
