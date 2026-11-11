using System;
using System.Collections.Generic;
using Blockly.Editing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// A panel over the right half of the workspace that shows the program as code (JavaScript, and other <see cref="CodeLanguage"/>s)
    /// and keeps it up to date. It has a button for the language, one that copies the code and one that closes the panel.
    /// Built from code, colours and fonts come from the theme.
    /// </summary>
    public sealed class CodeView : MonoBehaviour
    {
        const float HeaderHeight = 40f, Margin = 8f, ButtonHeight = 28f;

        TMP_Text body;
        TMP_Text languageLabel, copyLabel, saveLabel, closeLabel;
        Image panel;
        Image[] buttonImages;
        ScrollRect scroll;
        CodeLanguage language;
        string code = "", error = "";
        float bodySize = 14f;

        public Button LanguageButton { get; private set; }
        public Button CopyButton { get; private set; }
        public Button SaveButton { get; private set; }
        public Button CloseButton { get; private set; }

        /// <summary>The code of the last refresh; empty when the program could not be exported.</summary>
        public string Code => code;
        /// <summary>Why the program could not be exported, or empty.</summary>
        public string Error => error;
        /// <summary>What the panel shows: the code, or the reason there is none.</summary>
        public string ShownText => body != null ? body.text : "";
        public string LanguageName => Language.Name;

        /// <summary>Raised by the Save button with the language and the code to put in a file.</summary>
        public event Action<CodeLanguage, string> SaveRequested;

        /// <summary>Raised when the panel is hidden by its close button.</summary>
        public event Action Closed;

        public CodeLanguage Language
        {
            get => language ?? CodeLanguage.JavaScript;
            set { language = value; Refresh(); }
        }

        public bool Visible
        {
            get => gameObject.activeSelf;
            set { if (gameObject.activeSelf != value) { gameObject.SetActive(value); if (value) Refresh(); } }
        }

        Workspace workspace;

        /// <summary>Creates the panel, hidden, filling the right half of <paramref name="parent"/>.</summary>
        public static CodeView Create(RectTransform parent)
        {
            var go = new GameObject("Code", typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
            go.SetActive(false);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(0, Margin); rt.offsetMax = new Vector2(-Margin, -Margin);
            var v = go.AddComponent<CodeView>();
            v.Build(rt);
            return v;
        }

        void Build(RectTransform rt)
        {
            var parent = (RectTransform)rt.parent;
            panel = GetComponent<Image>();
            panel.color = BlockStyle.ConsoleBackground;

            LanguageButton = MakeButton(rt, "Language", 0, 130, out languageLabel);
            CopyButton = MakeButton(rt, "Copy", 0, 76, out copyLabel);
            SaveButton = MakeButton(rt, "Save", 0, 76, out saveLabel);
            CloseButton = MakeButton(rt, "Close", 0, 76, out closeLabel);
            buttonImages = new[] { LanguageButton.GetComponent<Image>(), CopyButton.GetComponent<Image>(), SaveButton.GetComponent<Image>(), CloseButton.GetComponent<Image>() };

            var viewport = New("Viewport", rt, typeof(RectMask2D));
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(Margin, Margin); viewport.offsetMax = new Vector2(-Margin, -HeaderHeight);
            var text = New("Text", viewport, typeof(TextMeshProUGUI));
            text.anchorMin = new Vector2(0, 1); text.anchorMax = new Vector2(1, 1); text.pivot = new Vector2(0, 1);
            text.anchoredPosition = Vector2.zero; text.sizeDelta = new Vector2(0, 20);
            body = text.GetComponent<TextMeshProUGUI>();
            body.richText = false; body.raycastTarget = false; body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.fontSize = bodySize;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = text;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 20f;

            LanguageButton.onClick.AddListener(ChooseLanguage);
            CopyButton.onClick.AddListener(Copy);
            SaveButton.onClick.AddListener(Save);
            CloseButton.onClick.AddListener(Close);
            Subscribe();
            ApplyTheme();
            Localize();
            RestoreLanguage();
            languageLabel.text = Language.Name;
            laidOutFor = -1f;
            Layout(parent.rect.width > 0f ? parent.rect.width : 2 * NarrowWidth);
        }

        // ------------------------------------------------------------ layout
        /// <summary>A workspace area narrower than this (a phone held upright) gives the whole width to the panel.</summary>
        public const float NarrowWidth = 640f;
        const float ButtonGap = 6f;
        const float MinButtonWidth = 48f, ButtonPaddingX = 8f;
        float fontScale = 1f;

        /// <summary>
        /// The width the panel has: half of a wide workspace area; on a narrow one all of the width inside <paramref name="screenWidth"/>
        /// (the whole screen, so it covers the toolbox too: beside the toolbox a phone has room for very little).
        /// </summary>
        public static float PanelWidth(float parentWidth, float screenWidth) => parentWidth < NarrowWidth ? screenWidth - 2 * Margin : parentWidth / 2f - Margin;

        void OnRectTransformDimensionsChange()
        {
            if (LanguageButton != null && transform.parent is RectTransform parent) Layout(parent.rect.width);
        }

        /// <summary>
        /// Fits the panel to a workspace area <paramref name="parentWidth"/> wide: the right half of a wide area, all of a narrow one; the
        /// four buttons of the header share the width (shrinking, labels with them, when it is short) and none covers another.
        /// </summary>
        public void Layout(float parentWidth)
        {
            if (parentWidth <= 0f || LanguageButton == null || inLayout) return;       // changing the panel's own size raises OnRectTransformDimensionsChange again
            if (Mathf.Approximately(parentWidth, laidOutFor)) return;
            inLayout = true;
            try { LayOutFor(parentWidth); }
            finally { inLayout = false; }
        }

        bool inLayout;
        float laidOutFor = -1f, panelWidth;
        /// <summary>The width the panel was laid out for.</summary>
        public float LaidOutWidth => panelWidth;

        void LayOutFor(float parentWidth)
        {
            laidOutFor = parentWidth;
            var rt = (RectTransform)transform;
            var parent = (RectTransform)rt.parent;
            bool narrow = parentWidth < NarrowWidth;
            float screenWidth = parentWidth, left = Margin;
            if (narrow && parent.parent is RectTransform screen && screen.rect.width > 0f)
            {
                screenWidth = screen.rect.width;                                              // the panel starts at the screen's left edge, not the workspace's
                left = parent.InverseTransformPoint(screen.TransformPoint(new Vector3(screen.rect.xMin, 0f, 0f))).x - parent.rect.xMin + Margin;
            }
            rt.anchorMin = new Vector2(narrow ? 0f : 0.5f, 0f); rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(narrow ? left : 0f, Margin); rt.offsetMax = new Vector2(-Margin, -Margin);
            panelWidth = PanelWidth(parentWidth, screenWidth);

            // each button is as wide as its label needs; when they do not all fit, the labels (and so the buttons) get smaller together
            fontScale = 1f;
            ApplyFonts();
            var buttons = new[] { LanguageButton, CopyButton, SaveButton, CloseButton };
            var labels = new[] { languageLabel, copyLabel, saveLabel, closeLabel };
            var text = new float[buttons.Length];
            float total = 0f;
            for (int i = 0; i < buttons.Length; i++)
            {
                float measured = Measure(labels[i]);
                text[i] = Mathf.Max(MinButtonWidth - 2 * ButtonPaddingX, measured);          // a little more than measured: bold and kerning
                total += text[i];
            }
            float padding = 2 * ButtonPaddingX * buttons.Length;                           // the room around a label does not shrink with the font
            float available = panelWidth - 2 * Margin - (buttons.Length - 1) * ButtonGap - padding;
            float scale = total > 0f ? Mathf.Clamp(available / total, 0.2f, 1f) : 1f;
            measuredWidths = new float[buttons.Length];
            for (int i = 0; i < buttons.Length; i++) measuredWidths[i] = Measure(labels[i]);
            recheckFrames = 4;
            float x = Margin;
            for (int i = 0; i < buttons.Length; i++)
            {
                float w = text[i] * scale + 2 * ButtonPaddingX;
                Place(buttons[i], x, w);
                x += w + ButtonGap;
            }
            fontScale = Mathf.Max(0.5f, scale);
            ApplyFonts();
        }

        // A font that makes its glyphs on demand (a dynamic atlas) measures a label wider once the glyphs are drawn: the widths are looked at
        // again for a few frames after every layout, and the header is fitted again when one has changed.
        float[] measuredWidths;
        int recheckFrames;

        void LateUpdate()
        {
            if (recheckFrames <= 0) return;
            recheckFrames--;
            Recheck();
        }

        /// <summary>Measures the four labels again; when one is wider or narrower than at the last layout, fits the header again. True if it did.</summary>
        public bool Recheck()
        {
            if (measuredWidths == null || languageLabel == null) return false;
            var labels = new[] { languageLabel, copyLabel, saveLabel, closeLabel };
            for (int i = 0; i < labels.Length; i++)
                if (Mathf.Abs(Measure(labels[i]) - measuredWidths[i]) > 1f) { Refit(); return true; }
            return false;
        }

        float Measure(TMP_Text label) => Mathf.Ceil(label.GetPreferredValues(label.text).x * 1.08f);

        /// <summary>Fits the header again after a label changed (another language, another theme font).</summary>
        void Refit()
        {
            laidOutFor = -1f;
            if (LanguageButton != null && transform.parent is RectTransform parent && parent.rect.width > 0f) Layout(parent.rect.width);
        }

        void ApplyFonts()
        {
            foreach (var t in new[] { languageLabel, copyLabel, saveLabel, closeLabel }) { t.color = BlockStyle.ButtonText; BlockStyle.ToolbarFont.Apply(t, 15f * fontScale); }
        }

        // ------------------------------------------------------------ content
        /// <summary>Exports <paramref name="ws"/> again and shows it. Called after every edit.</summary>
        public void Show(Workspace ws) { workspace = ws; Refresh(); }

        public void Refresh()
        {
            if (body == null || !gameObject.activeSelf) return;      // a hidden panel exports when it is shown
            code = ""; error = "";
            if (workspace != null)
            {
                try { code = Language.Generate(workspace); }
                catch (NotSupportedException e) { error = e.Message; }
            }
            body.color = error.Length > 0 ? ConsoleView.ErrorColour : BlockStyle.ConsoleText;
            body.text = error.Length > 0 ? error : code;
            if (languageLabel != null && languageLabel.text != Language.Name) { languageLabel.text = Language.Name; Refit(); }
            if (isActiveAndEnabled)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(body.rectTransform);
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>Puts the code on the clipboard.</summary>
        public void Copy()
        {
            if (code.Length == 0) return;
            PlatformClipboard.Write(code);
        }

        /// <summary>Asks for the code to be saved as a file (the Playground does it; see <see cref="SaveRequested"/>).</summary>
        public void Save()
        {
            if (code.Length == 0) return;
            SaveRequested?.Invoke(Language, code);
        }

        public void Close() { Visible = false; Closed?.Invoke(); }

        DropdownMenu menu;

        void ChooseLanguage()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            if (menu != null && menu.IsOpen) menu.Close();
            var corners = new Vector3[4];
            ((RectTransform)LanguageButton.transform).GetWorldCorners(corners);
            var cam = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            var point = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            var items = new List<DropdownItem>();
            foreach (var l in CodeLanguage.All) items.Add(new DropdownItem { Label = l.Name, Value = l.Name, Selected = l == Language });
            menu = DropdownMenu.Open(canvas, point, 150f, items, item => PickLanguage(item.Value));
        }

        /// <summary>The PlayerPrefs key that keeps the language the user chose last.</summary>
        public const string PrefsKey = "Blockly.CodeLanguage";

        /// <summary>Switches to the language called <paramref name="name"/> and remembers the choice for the next session.</summary>
        public bool PickLanguage(string name)
        {
            foreach (var l in CodeLanguage.All)
                if (l.Name == name)
                {
                    Language = l;
                    try { PlayerPrefs.SetString(PrefsKey, name); PlayerPrefs.Save(); }
                    catch (Exception e) when (e is InvalidOperationException || e is UnauthorizedAccessException) { }   // a convenience, never a failure
                    return true;
                }
            return false;
        }

        void RestoreLanguage()
        {
            string saved = PlayerPrefs.GetString(PrefsKey, null);
            foreach (var l in CodeLanguage.All) if (l.Name == saved) language = l;
        }

        // ------------------------------------------------------------ look
        void Subscribe()
        {
            BlockStyle.ThemeChanged -= ApplyTheme; BlockStyle.ThemeChanged += ApplyTheme;
            Loc.Changed -= Localize; Loc.Changed += Localize;
        }

        void OnEnable()
        {
            Subscribe();
            ApplySorting();
        }

        /// <summary>Draws the panel above the toolbox and flyout (on a phone it covers them) and below menus and dialogs. Done once it is nested in the scene's canvas.</summary>
        void ApplySorting()
        {
            var canvas = GetComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = SortingOrder;
        }

        const int SortingOrder = 100;
        void OnDisable() { BlockStyle.ThemeChanged -= ApplyTheme; Loc.Changed -= Localize; }
        void OnDestroy() { BlockStyle.ThemeChanged -= ApplyTheme; Loc.Changed -= Localize; }

        public void ApplyTheme()
        {
            if (this == null) { BlockStyle.ThemeChanged -= ApplyTheme; return; }
            panel.color = BlockStyle.ConsoleBackground;
            UiShape.Round(panel, BlockStyle.PanelCornerRadius);
            BlockStyle.ConsoleFont.Apply(body, bodySize);
            body.color = error.Length > 0 ? ConsoleView.ErrorColour : BlockStyle.ConsoleText;
            foreach (var img in buttonImages) { img.color = BlockStyle.ButtonColour; UiShape.Round(img, BlockStyle.ButtonCornerRadius); }
            ApplyFonts();
            Refit();                                                   // another font, other label widths
        }

        void Localize()
        {
            if (this == null) { Loc.Changed -= Localize; return; }
            copyLabel.text = Loc.T("Copy");
            saveLabel.text = Loc.T("Save");
            closeLabel.text = Loc.T("Close");
            Refit();
            if (error.Length > 0) Refresh();                    // the message is in the language too
        }

        // ------------------------------------------------------------ construction
        static RectTransform New(string name, Transform parent, params Type[] components)
        {
            var types = new Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            Array.Copy(components, 0, types, 1, components.Length);
            var rt = (RectTransform)new GameObject(name, types).transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static Button MakeButton(RectTransform parent, string name, float x, float width, out TMP_Text label)
        {
            var rt = New(name, parent, typeof(Image), typeof(Button));
            var img = rt.GetComponent<Image>();
            rt.GetComponent<Button>().targetGraphic = img;
            var l = New("Text", rt, typeof(TextMeshProUGUI));
            l.anchorMin = Vector2.zero; l.anchorMax = Vector2.one; l.offsetMin = l.offsetMax = Vector2.zero;
            label = l.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 15f;
            return rt.GetComponent<Button>();
        }

        static void Place(Button b, float x, float width)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -(HeaderHeight - ButtonHeight) / 2f); rt.sizeDelta = new Vector2(width, ButtonHeight);
        }
    }
}
