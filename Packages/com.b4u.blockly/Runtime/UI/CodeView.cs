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
            var go = new GameObject("Code", typeof(RectTransform), typeof(Image));
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
            panel = GetComponent<Image>();
            panel.color = BlockStyle.ConsoleBackground;

            LanguageButton = MakeButton(rt, "Language", 0, 150, out languageLabel);
            CopyButton = MakeButton(rt, "Copy", 0, 90, out copyLabel);
            SaveButton = MakeButton(rt, "Save", 0, 90, out saveLabel);
            CloseButton = MakeButton(rt, "Close", 0, 90, out closeLabel);
            Place(LanguageButton, Margin, 150); Place(CopyButton, Margin + 158, 90); Place(SaveButton, Margin + 256, 90);
            var close = (RectTransform)CloseButton.transform;                                    // the close button sits at the right end
            close.anchorMin = close.anchorMax = new Vector2(1, 1); close.pivot = new Vector2(1, 1);
            close.anchoredPosition = new Vector2(-Margin, -(HeaderHeight - ButtonHeight) / 2f); close.sizeDelta = new Vector2(90, ButtonHeight);
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
            if (languageLabel != null) languageLabel.text = Language.Name;
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

        void OnEnable() { Subscribe(); }
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
            foreach (var t in new[] { languageLabel, copyLabel, saveLabel, closeLabel }) { t.color = BlockStyle.ButtonText; BlockStyle.ToolbarFont.Apply(t, 15f); }
        }

        void Localize()
        {
            if (this == null) { Loc.Changed -= Localize; return; }
            copyLabel.text = Loc.T("Copy");
            saveLabel.text = Loc.T("Save");
            closeLabel.text = Loc.T("Close");
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
