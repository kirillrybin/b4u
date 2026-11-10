using TMPro;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>Colours derived from a block's base colour, following Blockly's look.</summary>
    public static class BlockStyle
    {
        static BlockTheme theme;
        static bool classic = true;

        /// <summary>True while no theme is set (the classic look).</summary>
        public static bool IsClassic => classic;

        /// <summary>Counts theme changes, so a view that was inactive (and heard no <see cref="ThemeChanged"/>) can tell its measurements are stale.</summary>
        public static int Version { get; private set; }

        /// <summary>Raised after <see cref="Theme"/> is replaced; views rebuild themselves.</summary>
        public static event System.Action ThemeChanged;

        /// <summary>The active look. Setting null restores the classic one.</summary>
        public static BlockTheme Theme
        {
            get { if (theme == null) theme = BlockTheme.CreateDefault(); return theme; }
            set
            {
                theme = value;
                Version++;
                classic = value == null;
                var t = Theme;
                Blockly.Layout.BlockGeometry.Current = new Blockly.Layout.BlockGeometry
                {
                    CornerRadius = t.CornerRadius, RoundNotches = t.RoundNotches, RoundTabs = t.RoundTabs,
                    RowMinHeight = t.RowMinHeight, PadLeft = t.PaddingX, PadRight = t.PaddingX, ItemGap = t.ItemGap,
                    FieldHeight = t.FieldHeight, FieldPadX = t.FieldPaddingX, LabelHeight = t.LabelHeight,
                    SlotPadY = t.SlotPaddingY, BottomBarHeight = t.BottomBarHeight, StatementGap = t.StatementGap,
                    StatementMinWidth = Mathf.Max(8f, t.StatementMinWidth), StatementMinHeight = Mathf.Max(8f, t.StatementMinHeight),
                    SlotMinWidth = Mathf.Max(8f, t.SlotMinWidth), SlotMinHeight = Mathf.Max(8f, t.SlotMinHeight),
                    TabWidth = Mathf.Max(1f, t.TabWidth), TabHeight = Mathf.Clamp(t.TabHeight, 4f, Mathf.Max(8f, t.SlotMinHeight)),
                    TabTop = Mathf.Clamp(t.TabTop, 0f, Mathf.Max(8f, t.SlotMinHeight) - Mathf.Clamp(t.TabHeight, 4f, Mathf.Max(8f, t.SlotMinHeight))),
                    NotchWidth = Mathf.Clamp(t.NotchWidth, 4f, Mathf.Max(8f, t.StatementMinWidth)), NotchDepth = Mathf.Max(1f, t.NotchDepth),
                    HatHeight = Mathf.Max(0f, t.HatHeight), HatWidth = Mathf.Max(0f, t.HatWidth), HatStyle = t.HatStyle, HatPeak = t.HatPeak, HatFillet = Mathf.Max(0f, t.HatFillet), IsHat = b => HasHat(t, b.Type),
                    NotchX = Mathf.Clamp(t.NotchX, 0f, Mathf.Max(8f, t.StatementMinWidth) - Mathf.Clamp(t.NotchWidth, 4f, Mathf.Max(8f, t.StatementMinWidth))),
                };
                ThemeChanged?.Invoke();
            }
        }

        /// <summary>True for a disabled block and for the blocks inside it (its value inputs and statement bodies; not the blocks that follow it).</summary>
        public static bool IsDisabled(Block block)
        {
            while (block != null)
            {
                if (block.Disabled) return true;
                var up = block.Output?.Target;
                if (up == null) { up = block.Previous?.Target; if (up != null && up == up.Owner.Next) up = null; }     // a follower is not inside the block before it
                block = up?.Owner;
            }
            return false;
        }

        public static float FontSize => Theme.FontSize;
        public static Color AppBackground => Theme.AppBackground;
        public static Color WorkspaceBackground => Theme.WorkspaceBackground;
        public static float GridSpacing => Mathf.Max(0f, Theme.GridSpacing);
        public static Color GridColour => Theme.GridColour;
        public static float GridDot => Mathf.Max(0.5f, Theme.GridDot);
        public static bool GridSnap => Theme.GridSnap;
        public static Color ToolbarBackground => Theme.ToolbarBackground;
        public static Color ToolboxBackground => Theme.ToolboxBackground;
        public static Color ToolboxDeleteHover => Theme.ToolboxDeleteHover;
        public static bool ToolboxDeletePanel => Theme.ToolboxDeletePanel;
        public static Color DeleteBorderColour => Theme.DeleteBorderColour;
        public static float DeleteBorderWidth => Mathf.Clamp(Theme.DeleteBorderWidth, 0f, 20f);
        public static float DeleteDashLength => Mathf.Max(1f, Theme.DeleteDashLength);
        public static float DeleteDashGap => Mathf.Max(0f, Theme.DeleteDashGap);
        public static Color DeleteIconColour => Theme.DeleteIconColour;
        public static float DeleteIconSize => Mathf.Clamp(Theme.DeleteIconSize, 8f, 200f);
        public static string DeleteLabel => Theme.DeleteLabel ?? "";
        public static Color DeleteLabelColour => Theme.DeleteLabelColour;
        public static float DeleteLabelSize => Mathf.Clamp(Theme.DeleteLabelSize, 6f, 100f);
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
        public static UiCorners ToolboxCorners => Theme.ToolboxCorners;
        public static UiCorners ToolboxCornersWithFlyout => Theme.ToolboxCornersWithFlyout;
        public static UiCorners FlyoutCorners => Theme.FlyoutCorners;
        public static float ToolboxWidth => Mathf.Clamp(Theme.ToolboxWidth, 48f, 600f);
        public static bool ToolboxCollapsible => Theme.ToolboxCollapsible;
        public static float ToolboxCollapsedWidth => Mathf.Clamp(Theme.ToolboxCollapsedWidth, 40f, 600f);
        public static float ToggleSize => Mathf.Clamp(Theme.ToggleSize, 16f, 120f);
        public static float ToggleTop => Mathf.Max(0f, Theme.ToggleTop);
        public static Color ToggleColour => Theme.ToggleColour.a > 0f ? Theme.ToggleColour : Theme.ToolboxBackground;
        public static Color ToggleIconColour => Theme.ToggleIconColour;
        public static float CategoryButtonHeight => Mathf.Clamp(Theme.CategoryButtonHeight, 16f, 200f);
        public static float CategoryGap => Mathf.Clamp(Theme.CategoryGap, 0f, 100f);
        public static float CategoryIconSize => Mathf.Clamp(Theme.CategoryIconSize, 8f, 128f);
        public static Color CategoryPlate => Theme.CategoryPlate;
        public static Color CategoryPlateSelected => Theme.CategoryPlateSelected;
        public static Color CategoryTextSelected => Theme.CategoryTextSelected;
        public static bool FlyoutTitle => Theme.FlyoutTitle;
        public static Color FlyoutTitleColour => Theme.FlyoutTitleColour;
        public static float FlyoutTitleSize => Theme.FlyoutTitleSize;
        public static float FlyoutTitleHeight => Theme.FlyoutTitleHeight;
        public static float FlyoutPadding => Mathf.Max(0f, Theme.FlyoutPadding);
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
            if (t.DisabledBlend > 0f && IsDisabled(block)) baseColour = Color.Lerp(baseColour, new Color(0.6f, 0.6f, 0.6f), t.DisabledBlend);
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
