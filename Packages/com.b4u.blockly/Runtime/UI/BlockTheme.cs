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

        [Header("Fields")]
        public Color InputFill = Color.white;
        public Color InputText = new Color(0.08f, 0.08f, 0.08f, 1f);
        public Color LabelText = Color.white;
        public Color DropdownArrow = new Color(1f, 1f, 1f, 0.9f);
        public float FieldStrokeWidth = 1f;

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
