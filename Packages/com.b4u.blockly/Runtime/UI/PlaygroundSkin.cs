using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// Paints the Playground's panels, toolbar buttons and console with the interface colours of the active
    /// <see cref="BlockTheme"/>, and repaints them whenever <see cref="BlockStyle.Theme"/> changes.
    /// (The toolbox, menus and dialogs read the theme themselves.)
    /// </summary>
    public sealed class PlaygroundSkin : MonoBehaviour
    {
        [SerializeField] Image appBackground, workspace, toolbar, console, flyout;
        [SerializeField] Image[] buttons;
        [SerializeField] TMP_Text[] toolbarTexts;
        [SerializeField] TMP_Text consoleText;

        readonly Dictionary<TMP_Text, float> baseSizes = new Dictionary<TMP_Text, float>();

        public void Configure(Image app, Image workspaceBackground, Image toolbarBackground, Image consoleBackground, Image flyoutBackground,
                              IEnumerable<Image> buttonImages, IEnumerable<TMP_Text> texts, TMP_Text consoleOutput)
        {
            appBackground = app; workspace = workspaceBackground; toolbar = toolbarBackground; console = consoleBackground; flyout = flyoutBackground;
            buttons = new List<Image>(buttonImages).ToArray();
            toolbarTexts = new List<TMP_Text>(texts).ToArray();
            consoleText = consoleOutput;
        }

        void OnEnable() { Subscribe(); Apply(); }
        void OnDisable() { BlockStyle.ThemeChanged -= OnThemeChanged; }
        void OnDestroy() { BlockStyle.ThemeChanged -= OnThemeChanged; }

        // EditMode never calls OnEnable, so tests (and Apply) can subscribe explicitly
        void Subscribe() { BlockStyle.ThemeChanged -= OnThemeChanged; BlockStyle.ThemeChanged += OnThemeChanged; }

        void OnThemeChanged()
        {
            if (this == null) { BlockStyle.ThemeChanged -= OnThemeChanged; return; }
            Apply();
        }

        /// <summary>Paints everything with the current theme.</summary>
        public void Apply()
        {
            Subscribe();
            Paint(appBackground, BlockStyle.AppBackground);
            Paint(workspace, BlockStyle.WorkspaceBackground);
            Paint(toolbar, BlockStyle.ToolbarBackground);
            Paint(console, BlockStyle.ConsoleBackground);
            Paint(flyout, BlockStyle.FlyoutBackground);
            UiShape.Round(console, BlockStyle.PanelCornerRadius);
            UiShape.Round(flyout, BlockStyle.PanelCornerRadius);
            if (buttons != null) foreach (var b in buttons) { Paint(b, BlockStyle.ButtonColour); UiShape.Round(b, BlockStyle.ButtonCornerRadius); }
            if (toolbarTexts != null)
                foreach (var t in toolbarTexts)
                {
                    if (t == null) continue;
                    t.color = BlockStyle.ButtonText;
                    if (!baseSizes.TryGetValue(t, out var size)) baseSizes[t] = size = t.fontSize;     // the size the scene was built with
                    BlockStyle.ToolbarFont.Apply(t, size);
                }
            if (consoleText != null) consoleText.color = BlockStyle.ConsoleText;
        }

        static void Paint(Image image, Color colour) { if (image != null) image.color = colour; }
    }
}
