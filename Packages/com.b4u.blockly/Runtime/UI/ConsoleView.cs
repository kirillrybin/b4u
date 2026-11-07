using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// Scrolling text console for program output plus a one-line status (ready / finished / error).
    /// Output is shown literally (rich text off), so printed "&lt;b&gt;" is not interpreted as markup.
    /// </summary>
    public sealed class ConsoleView : MonoBehaviour
    {
        [SerializeField] TMP_Text output;
        [SerializeField] TMP_Text status;
        [SerializeField] ScrollRect scroll;
        [SerializeField] int maxLines = 500;

        readonly List<string> lines = new List<string>();
        readonly StringBuilder sb = new StringBuilder();

        public IReadOnlyList<string> Lines => lines;
        public string StatusText => status != null ? status.text : "";

        public static readonly Color OkColour = new Color(0.55f, 0.85f, 0.55f);
        public static readonly Color ErrorColour = new Color(1f, 0.45f, 0.45f);
        public static readonly Color NeutralColour = new Color(0.75f, 0.75f, 0.8f);

        public void Configure(TMP_Text outputText, TMP_Text statusText, ScrollRect scrollRect)
        {
            output = outputText; status = statusText; scroll = scrollRect;
            if (output != null) output.richText = false;
            ApplyTheme();
        }

        (float output, float status)? fontBase;

        void OnEnable() { ApplyTheme(); }
        void OnDisable() { BlockStyle.ThemeChanged -= ApplyTheme; }
        void OnDestroy() { BlockStyle.ThemeChanged -= ApplyTheme; }

        /// <summary>Applies the theme's console font, now and after every theme change (EditMode never calls OnEnable, so it subscribes here).</summary>
        public void ApplyTheme()
        {
            if (this == null) { BlockStyle.ThemeChanged -= ApplyTheme; return; }
            BlockStyle.ThemeChanged -= ApplyTheme; BlockStyle.ThemeChanged += ApplyTheme;
            if (!fontBase.HasValue) fontBase = (output != null ? output.fontSize : 14f, status != null ? status.fontSize : 14f);
            BlockStyle.ConsoleFont.Apply(output, fontBase.Value.output);
            BlockStyle.ConsoleFont.Apply(status, fontBase.Value.status);
        }

        public void Append(string line)
        {
            lines.Add(line ?? "");
            if (lines.Count > maxLines) lines.RemoveRange(0, lines.Count - maxLines);
            Refresh();
        }

        public void Clear()
        {
            lines.Clear();
            Refresh();
        }

        public void SetStatus(string message, Color colour)
        {
            if (status == null) return;
            status.text = message;
            status.color = colour;
        }

        void Refresh()
        {
            if (output == null) return;
            sb.Clear();
            for (int i = 0; i < lines.Count; i++) { if (i > 0) sb.Append('\n'); sb.Append(lines[i]); }
            output.richText = false;
            output.text = sb.ToString();
            if (scroll != null && isActiveAndEnabled)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(output.rectTransform);
                scroll.verticalNormalizedPosition = 0f;     // follow the newest line
            }
        }
    }
}
