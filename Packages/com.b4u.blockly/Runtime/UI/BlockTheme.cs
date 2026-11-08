using System;
using Blockly.Layout;
using TMPro;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>A font for one part of the interface: its own font asset, a size multiplier and a style.</summary>
    [Serializable]
    public class ThemeFont
    {
        [Tooltip("Leave empty for the TextMeshPro default font.")]
        public TMP_FontAsset Font;
        [Tooltip("Multiplies the size the package would use (1 = unchanged).")]
        public float Scale = 1f;
        public FontStyles Style = FontStyles.Normal;

        /// <summary>Applies font, style and <paramref name="baseSize"/> times <see cref="Scale"/> to a text.</summary>
        public void Apply(TMP_Text text, float baseSize)
        {
            if (text == null) return;
            if (Font != null) text.font = Font;
            text.fontStyle = Style;
            text.fontSize = baseSize * (Scale > 0f ? Scale : 1f);
        }
    }

    /// <summary>
    /// Overrides for some blocks. <see cref="Match"/> is a block type ("text_print"), a prefix ending in * ("text_*") or * for all;
    /// every rule that matches applies, in list order, so later rules win. Only the ticked parts change.
    /// </summary>
    [Serializable]
    public class BlockStyleRule
    {
        [Tooltip("A block type, a prefix ending in * (math_*), or * for every block.")]
        public string Match = "*";
        public bool OverrideFill; public Color Fill = Color.gray;
        public bool OverrideStroke; public Color Stroke = Color.white;
        [Tooltip("Outline width of these blocks (0 = the theme's).")]
        public float StrokeWidth;
        public bool OverrideLabelText; public Color LabelText = Color.white;
        public bool OverrideInputFill; public Color InputFill = Color.white;
        public bool OverrideInputText; public Color InputText = Color.black;
        [Tooltip("Draws the theme's hat (a cap above the block) on matching blocks that start a stack.")]
        public bool Hat;

        public bool Matches(string blockType)
        {
            if (string.IsNullOrEmpty(Match) || blockType == null) return false;
            if (Match == "*") return true;
            if (Match[Match.Length - 1] == '*') return blockType.StartsWith(Match.Substring(0, Match.Length - 1), StringComparison.Ordinal);
            return blockType == Match;
        }
    }

    /// <summary>
    /// The look of blocks: how the outline, slots and fields are coloured and which font is used.
    /// Block fill colours still come from the block definitions. Assign an instance to <see cref="BlockStyle.Theme"/>.
    /// The defaults reproduce Blockly's classic look.
    /// </summary>
    [CreateAssetMenu(menuName = "Blockly/Block Theme", fileName = "BlockTheme")]
    public class BlockTheme : ScriptableObject
    {
        [Header("Outline")]
        [Tooltip("Outline colour = block colour * this. Above 1 gives a lighter outline.")]
        public float StrokeScale = 0.7f;
        public float StrokeWidth = 1.2f;

        [Header("Derived colours")]
        public float HoleScale = 0.8f;
        public float DropdownScale = 0.85f;
        [Tooltip("How far shadow blocks are pulled towards grey (0..1).")]
        [Range(0f, 1f)] public float ShadowBlend = 0.55f;

        [Header("Shape")]
        [Tooltip("Radius of the right-angle corners of blocks (0 = sharp, as in Blockly).")]
        public float CornerRadius = 0f;
        [Tooltip("Smooth bump for the previous/next notches instead of a trapezoid.")]
        public bool RoundNotches = false;
        [Tooltip("Smooth bump for the output tabs and value holes instead of a puzzle knob.")]
        public bool RoundTabs = false;
        [Tooltip("Radius of the corners of field plates (0 = square).")]
        public float FieldCornerRadius = 0f;

        [Header("Connection shapes (TabTop + TabHeight must fit in SlotMinHeight, NotchX + NotchWidth in StatementMinWidth; larger values are clamped)")]
        public float TabWidth = 8f;
        public float TabHeight = 15f;
        public float TabTop = 5f;
        public float NotchX = 15f;
        public float NotchWidth = 15f;
        public float NotchDepth = 4f;

        [Header("Spacing (workspace units; the defaults are Blockly's)")]
        public float RowMinHeight = 24f;
        [Tooltip("Space between a block's edge and its first / last item.")]
        public float PaddingX = 8f;
        public float ItemGap = 4f;
        public float FieldHeight = 20f;
        public float FieldPaddingX = 5f;
        public float LabelHeight = 16f;
        [Tooltip("Space above and below an inline value hole.")]
        public float SlotPaddingY = 4f;
        public float BottomBarHeight = 12f;
        public float StatementGap = 8f;
        [Tooltip("Smallest width of a statement (C-shaped) input's opening; the notch has to fit in it.")]
        public float StatementMinWidth = 30f;
        public float StatementMinHeight = 24f;
        [Tooltip("Smallest size of an inline value hole; the output tab has to fit in its height.")]
        public float SlotMinWidth = 24f;
        public float SlotMinHeight = 24f;

        [Header("Quality")]
        [Tooltip("Soft edges on block shapes, in screen pixels (0 = hard edges). About 1 removes the stair-steps without MSAA.")]
        public float EdgeSmoothing = 1f;

        [Header("Hats (for rules with Hat ticked)")]
        public float HatHeight = 14f;
        [Tooltip("Width of the cap (0 = 60% of the block).")]
        public float HatWidth = 0f;
        [Tooltip("Bump: a symmetric hump. Dome: the left edge rises, turns round and slopes down to the top edge.")]
        public HatShape HatStyle = HatShape.Bump;
        [Tooltip("Dome only: where the peak is, as a share of the cap's width.")]
        [Range(0.1f, 0.9f)] public float HatPeak = 0.45f;
        [Tooltip("Dome only: radius of the concave curve where the dome meets the top edge (0 = a quarter of the height).")]
        public float HatFillet = 0f;

        [Header("Block states")]
        [Tooltip("How far disabled blocks are pulled towards grey (0 = look like any block).")]
        [Range(0f, 1f)] public float DisabledBlend = 0.55f;
        public Color SelectionColour = new Color(0.15f, 0.45f, 0.95f, 1f);
        public float SelectionWidth = 3f;
        [Tooltip("The running block.")]
        public Color HighlightColour = new Color(1f, 0.85f, 0.1f, 1f);
        public float HighlightWidth = 3.5f;
        [Tooltip("The connection a dragged block would snap to.")]
        public Color SnapColour = new Color(1f, 0.85f, 0.1f, 0.95f);
        public Color SnapOutline = new Color(0.45f, 0.35f, 0f, 1f);

        [Header("Per block")]
        public System.Collections.Generic.List<BlockStyleRule> Rules = new System.Collections.Generic.List<BlockStyleRule>();

        [Header("Fields")]
        public Color InputFill = Color.white;
        public Color InputText = new Color(0.08f, 0.08f, 0.08f, 1f);
        public Color LabelText = Color.white;
        public Color DropdownArrow = new Color(1f, 1f, 1f, 0.9f);
        public float FieldStrokeWidth = 1f;

        [Header("Interface (Playground, toolbox, menus, dialogs)")]
        public Color AppBackground = new Color(0.17f, 0.17f, 0.2f);
        public Color WorkspaceBackground = new Color(0.97f, 0.97f, 0.97f);
        public Color ToolbarBackground = new Color(0.17f, 0.17f, 0.2f);
        public Color ToolboxBackground = new Color(0.22f, 0.22f, 0.26f);
        [Tooltip("Toolbox colour while a block is dragged over it (the trash).")]
        public Color ToolboxDeleteHover = new Color(0.6f, 0.15f, 0.15f);
        public Color FlyoutBackground = new Color(0.9f, 0.9f, 0.93f);
        public Color ConsoleBackground = new Color(0.1f, 0.1f, 0.12f);
        public Color ConsoleText = new Color(0.9f, 0.9f, 0.9f);
        public Color ButtonColour = new Color(0.33f, 0.36f, 0.48f);
        public Color ButtonText = Color.white;
        public Color CategoryText = Color.white;
        [Tooltip("Category buttons are their colour times this until selected.")]
        public float CategoryShade = 0.75f;
        public Color VariableButton = new Color(0.55f, 0.3f, 0.5f);
        public Color MenuBackground = new Color(0.17f, 0.17f, 0.2f, 0.98f);
        public Color MenuHover = new Color(0.32f, 0.36f, 0.5f, 1f);
        public Color MenuSelected = new Color(0.25f, 0.45f, 0.75f, 1f);
        [Tooltip("Corner radius of toolbar, category, dialog and variable buttons (0 = square).")]
        public float ButtonCornerRadius = 0f;
        [Tooltip("Corner radius of the toolbox, flyout, console, menus and dialogs (0 = square).")]
        public float PanelCornerRadius = 0f;
        public Color DialogPanel = new Color(0.2f, 0.2f, 0.24f, 1f);

        [Header("Toolbox and flyout")]
        [Tooltip("Which corners of the category strip are rounded (PanelCornerRadius). Square the right ones when the flyout joins it.")]
        public UiCorners ToolboxCorners = UiCorners.All;
        [Tooltip("Which corners of the category strip are rounded while a flyout is open next to it (usually the right ones are square).")]
        public UiCorners ToolboxCornersWithFlyout = UiCorners.All;
        [Tooltip("Which corners of the flyout are rounded. Square the left ones when it joins the strip.")]
        public UiCorners FlyoutCorners = UiCorners.All;
        [Tooltip("Width of the category strip in canvas units.")]
        public float ToolboxWidth = 130f;
        [Tooltip("Let the category strip collapse to icons only, with a round tab to toggle it.")]
        public bool ToolboxCollapsible = false;
        [Tooltip("Width of the collapsed strip (category buttons become square icon buttons).")]
        public float ToolboxCollapsedWidth = 84f;
        [Tooltip("Diameter of the round tab that collapses and expands the strip, and its distance from the top.")]
        public float ToggleSize = 44f;
        public float ToggleTop = 24f;
        [Tooltip("Colour of the tab. Transparent uses ToolboxBackground.")]
        public Color ToggleColour = new Color(0f, 0f, 0f, 0f);
        public Color ToggleIconColour = new Color(0.7f, 0.7f, 0.85f, 1f);
        public float CategoryButtonHeight = 40f;
        [Tooltip("Space between category buttons.")]
        public float CategoryGap = 2f;
        public float CategoryIconSize = 24f;
        [Tooltip("Colour of a category button. Transparent (alpha 0) tints it with the colour of its category.")]
        public Color CategoryPlate = new Color(0f, 0f, 0f, 0f);
        [Tooltip("Colour of the selected category button. Transparent uses the colour of its category.")]
        public Color CategoryPlateSelected = new Color(0f, 0f, 0f, 0f);
        [Tooltip("Text colour of the selected category button.")]
        public Color CategoryTextSelected = Color.white;
        [Tooltip("Show the name of the open category above its blocks in the flyout.")]
        public bool FlyoutTitle = false;
        public Color FlyoutTitleColour = Color.white;
        public float FlyoutTitleSize = 22f;
        public float FlyoutTitleHeight = 56f;
        [Tooltip("Space between the edge of the flyout and its blocks.")]
        public float FlyoutPadding = 12f;

        [Header("Text")]
        public float FontSize = 12f;
        [Tooltip("Leave empty for the TextMeshPro default font.")]
        public TMP_FontAsset Font;
        public FontStyles FontStyle = FontStyles.Normal;

        [Header("Fonts of the interface")]
        public ThemeFont ToolbarFont = new ThemeFont();
        public ThemeFont ToolboxFont = new ThemeFont();
        public ThemeFont ConsoleFont = new ThemeFont();
        public ThemeFont MenuFont = new ThemeFont();
        public ThemeFont DialogFont = new ThemeFont();

        /// <summary>The classic Blockly look.</summary>
        public static BlockTheme CreateDefault()
        {
            var t = CreateInstance<BlockTheme>();
            t.name = "Classic";
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
