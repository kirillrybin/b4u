using TMPro;
using UnityEngine;

namespace Blockly.UI
{
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

        [Header("Quality")]
        [Tooltip("Soft edges on block shapes, in screen pixels (0 = hard edges). About 1 removes the stair-steps without MSAA.")]
        public float EdgeSmoothing = 1f;

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

        [Header("Text")]
        public float FontSize = 12f;
        [Tooltip("Leave empty for the TextMeshPro default font.")]
        public TMP_FontAsset Font;

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
