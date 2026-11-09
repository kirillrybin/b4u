namespace Blockly.Layout
{
    /// <summary>Geometry constants (workspace units). Close to Blockly's Geras renderer, not pixel-identical.</summary>
    public static class Metrics
    {
        public const float RowMinHeight = 24f;
        public const float PadLeft = 8f, PadRight = 8f, ItemGap = 4f;
        public const float FieldPadX = 5f, FieldHeight = 20f, LabelHeight = 16f, CheckboxWidth = 16f, ColourWidth = 30f;

        // Output tab (puzzle knob) and the matching indent on the parent
        public const float TabWidth = 8f, TabHeight = 15f, TabTop = 5f;

        // Previous/next notch on stacked statement blocks
        public const float NotchX = 15f, NotchWidth = 15f, NotchDepth = 4f;

        // Inline value inputs
        public const float SlotMinWidth = 24f, SlotMinHeight = 24f, SlotPadY = 4f;

        // Statement ("C") inputs
        public const float StatementMinHeight = 24f, StatementMinWidth = 30f, BottomBarHeight = 12f, TopBarHeight = 8f, StatementGap = 8f;
    }
}
