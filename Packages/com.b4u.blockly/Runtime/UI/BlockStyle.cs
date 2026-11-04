using UnityEngine;

namespace Blockly.UI
{
    /// <summary>Colours derived from a block's base colour, following Blockly's look.</summary>
    public static class BlockStyle
    {
        public const float FontSize = 12f;

        public static Color Parse(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : new Color(0.65f, 0.65f, 0.65f);
        }

        public static Color Fill(Color baseColour, bool shadow) => shadow ? Color.Lerp(baseColour, new Color(0.78f, 0.78f, 0.78f), 0.55f) : baseColour;
        public static Color Stroke(Color baseColour, bool shadow) => Scale(Fill(baseColour, shadow), 0.7f);
        public static Color Hole(Color baseColour, bool shadow) => Scale(Fill(baseColour, shadow), 0.8f);
        public static Color DropdownFill(Color baseColour, bool shadow) => Scale(Fill(baseColour, shadow), 0.85f);
        public static readonly Color InputFill = new Color(1f, 1f, 1f, 1f);
        public static readonly Color InputText = new Color(0.08f, 0.08f, 0.08f, 1f);
        public static readonly Color LabelText = Color.white;

        static Color Scale(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
