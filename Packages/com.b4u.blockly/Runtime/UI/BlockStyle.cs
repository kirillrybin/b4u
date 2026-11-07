using UnityEngine;

namespace Blockly.UI
{
    /// <summary>Colours derived from a block's base colour, following Blockly's look.</summary>
    public static class BlockStyle
    {
        static BlockTheme theme;

        /// <summary>Raised after <see cref="Theme"/> is replaced; views rebuild themselves.</summary>
        public static event System.Action ThemeChanged;

        /// <summary>The active look. Setting null restores the classic one.</summary>
        public static BlockTheme Theme
        {
            get { if (theme == null) theme = BlockTheme.CreateDefault(); return theme; }
            set
            {
                theme = value;
                var t = Theme;
                Blockly.Layout.BlockGeometry.Current = new Blockly.Layout.BlockGeometry
                {
                    CornerRadius = t.CornerRadius, RoundNotches = t.RoundNotches, RoundTabs = t.RoundTabs,
                    RowMinHeight = t.RowMinHeight, PadLeft = t.PaddingX, PadRight = t.PaddingX, ItemGap = t.ItemGap,
                    FieldHeight = t.FieldHeight, FieldPadX = t.FieldPaddingX, LabelHeight = t.LabelHeight,
                    SlotPadY = t.SlotPaddingY, BottomBarHeight = t.BottomBarHeight, StatementGap = t.StatementGap,
                };
                ThemeChanged?.Invoke();
            }
        }

        public static float FontSize => Theme.FontSize;
        public static float FieldCornerRadius => Theme.FieldCornerRadius;
        public static float StrokeWidth => Theme.StrokeWidth;
        public static float FieldStrokeWidth => Theme.FieldStrokeWidth;
        public static Color DropdownArrow => Theme.DropdownArrow;

        public static Color Parse(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : new Color(0.65f, 0.65f, 0.65f);
        }

        public static Color Fill(Color baseColour, bool shadow) => shadow ? Color.Lerp(baseColour, new Color(0.78f, 0.78f, 0.78f), Theme.ShadowBlend) : baseColour;
        public static Color Stroke(Color baseColour, bool shadow) => Scale(Fill(baseColour, shadow), Theme.StrokeScale);
        public static Color Hole(Color baseColour, bool shadow) => Scale(Fill(baseColour, shadow), Theme.HoleScale);
        public static Color DropdownFill(Color baseColour, bool shadow) => Scale(Fill(baseColour, shadow), Theme.DropdownScale);
        public static Color InputFill => Theme.InputFill;
        public static Color InputText => Theme.InputText;
        public static Color LabelText => Theme.LabelText;

        static Color Scale(Color c, float k) => new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), c.a);
    }
}
