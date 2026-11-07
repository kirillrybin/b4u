using TMPro;
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
                    TabWidth = Mathf.Max(1f, t.TabWidth), TabHeight = Mathf.Clamp(t.TabHeight, 4f, 24f),
                    TabTop = Mathf.Clamp(t.TabTop, 0f, 24f - Mathf.Clamp(t.TabHeight, 4f, 24f)),
                    NotchWidth = Mathf.Clamp(t.NotchWidth, 4f, 30f), NotchDepth = Mathf.Max(1f, t.NotchDepth),
                    HatHeight = Mathf.Max(0f, t.HatHeight), HatWidth = Mathf.Max(0f, t.HatWidth), HatStyle = t.HatStyle, HatPeak = t.HatPeak, HatFillet = Mathf.Max(0f, t.HatFillet), IsHat = b => HasHat(t, b.Type),
                    NotchX = Mathf.Clamp(t.NotchX, 0f, 30f - Mathf.Clamp(t.NotchWidth, 4f, 30f)),
                };
                ThemeChanged?.Invoke();
            }
        }

        public static float FontSize => Theme.FontSize;
        public static Color AppBackground => Theme.AppBackground;
        public static Color WorkspaceBackground => Theme.WorkspaceBackground;
        public static Color ToolbarBackground => Theme.ToolbarBackground;
        public static Color ToolboxBackground => Theme.ToolboxBackground;
        public static Color ToolboxDeleteHover => Theme.ToolboxDeleteHover;
        public static Color FlyoutBackground => Theme.FlyoutBackground;
        public static Color ConsoleBackground => Theme.ConsoleBackground;
        public static Color ConsoleText => Theme.ConsoleText;
        public static Color ButtonColour => Theme.ButtonColour;
        public static Color ButtonText => Theme.ButtonText;
        public static Color CategoryText => Theme.CategoryText;
        public static float CategoryShade => Theme.CategoryShade;
        public static Color VariableButton => Theme.VariableButton;
        public static Color MenuBackground => Theme.MenuBackground;
        public static Color MenuHover => Theme.MenuHover;
        public static Color MenuSelected => Theme.MenuSelected;
        public static float ButtonCornerRadius => Theme.ButtonCornerRadius;
        public static float PanelCornerRadius => Theme.PanelCornerRadius;
        public static Color DialogPanel => Theme.DialogPanel;
        public static FontStyles FontStyle => Theme.FontStyle;
        public static ThemeFont ToolbarFont => Theme.ToolbarFont ?? (Theme.ToolbarFont = new ThemeFont());
        public static ThemeFont ToolboxFont => Theme.ToolboxFont ?? (Theme.ToolboxFont = new ThemeFont());
        public static ThemeFont ConsoleFont => Theme.ConsoleFont ?? (Theme.ConsoleFont = new ThemeFont());
        public static ThemeFont MenuFont => Theme.MenuFont ?? (Theme.MenuFont = new ThemeFont());
        public static ThemeFont DialogFont => Theme.DialogFont ?? (Theme.DialogFont = new ThemeFont());
        public static Color SelectionColour => Theme.SelectionColour;
        static bool HasHat(BlockTheme t, string type)
        {
            if (t.Rules != null)
                foreach (var r in t.Rules) if (r != null && r.Hat && r.Matches(type)) return true;
            return false;
        }

        public static float SelectionWidth => Theme.SelectionWidth;
        public static Color HighlightColour => Theme.HighlightColour;
        public static float HighlightWidth => Theme.HighlightWidth;
        public static Color SnapColour => Theme.SnapColour;
        public static Color SnapOutline => Theme.SnapOutline;
        public static float EdgeSmoothing => Theme.EdgeSmoothing;
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

        /// <summary>Everything the view needs to draw one block: the theme with the matching rules applied.</summary>
        public sealed class BlockLook
        {
            public Color Fill, Stroke, Hole, DropdownFill, InputFill, InputText, LabelText;
            public float StrokeWidth;
        }

        /// <summary>The look of <paramref name="block"/>: its definition's colour, the theme, the block's state and the theme's rules.</summary>
        public static BlockLook Look(Blockly.Block block, bool shadow)
        {
            var t = Theme;
            var baseColour = Parse(block.Definition.ColourHex);
            BlockStyleRule fillRule = null, strokeRule = null, labelRule = null, inFillRule = null, inTextRule = null;
            float width = t.StrokeWidth;
            if (t.Rules != null)
                foreach (var r in t.Rules)
                {
                    if (r == null || !r.Matches(block.Type)) continue;
                    if (r.OverrideFill) fillRule = r;
                    if (r.OverrideStroke) strokeRule = r;
                    if (r.StrokeWidth > 0f) width = r.StrokeWidth;
                    if (r.OverrideLabelText) labelRule = r;
                    if (r.OverrideInputFill) inFillRule = r;
                    if (r.OverrideInputText) inTextRule = r;
                }
            if (fillRule != null) baseColour = fillRule.Fill;
            if (block.Disabled && t.DisabledBlend > 0f) baseColour = Color.Lerp(baseColour, new Color(0.6f, 0.6f, 0.6f), t.DisabledBlend);
            var fill = Fill(baseColour, shadow);
            return new BlockLook
            {
                Fill = fill,
                Stroke = strokeRule != null ? strokeRule.Stroke : Scale(fill, t.StrokeScale),
                Hole = Scale(fill, t.HoleScale),
                DropdownFill = Scale(fill, t.DropdownScale),
                InputFill = inFillRule != null ? inFillRule.InputFill : t.InputFill,
                InputText = inTextRule != null ? inTextRule.InputText : t.InputText,
                LabelText = labelRule != null ? labelRule.LabelText : t.LabelText,
                StrokeWidth = width,
            };
        }

        static Color Scale(Color c, float k) => new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), c.a);
    }
}
