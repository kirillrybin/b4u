using Blockly.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// Add this component to any object in the Playground scene and press Play.
/// With no theme assigned it builds a light-outline "Candy" look; assign a BlockTheme asset
/// (Assets > Create > Blockly > Block Theme) to use your own. Drop a TMP font asset into "font" to change every font of Candy. Toggle the component to re-apply after editing the asset.
/// </summary>
public class ApplyBlockTheme : MonoBehaviour
{
    public BlockTheme theme;

    [Tooltip("Optional TextMeshPro font asset for every text of the built-in Candy theme (blocks, toolbar, toolbox, console, menus, dialogs).")]
    public TMP_FontAsset font;

    void OnEnable()
    {
        BlockStyle.Theme = theme != null ? theme : Candy(font);
    }

    void OnDisable()
    {
        BlockStyle.Theme = null;      // back to the classic look
    }

    static Color Hex(string html) { ColorUtility.TryParseHtmlString(html, out var c); return c; }

    static BlockTheme Candy(TMP_FontAsset font)
    {
        var t = BlockTheme.CreateDefault();
        if (font != null)
        {
            t.Font = font;
            t.ToolbarFont.Font = t.ToolboxFont.Font = t.ConsoleFont.Font = t.MenuFont.Font = t.DialogFont.Font = font;
        }
        t.name = "Candy";
        t.StrokeScale = 1.35f;         // outline lighter than the fill
        t.StrokeWidth = 2.8f;
        t.FieldStrokeWidth = 0f;
        t.FontSize = 15f;
        t.InputFill = new Color(1f, 1f, 1f, 0.35f);
        t.InputText = Color.white;
        t.DropdownScale = 1.2f;
        t.CornerRadius = 9f;           // rounded blocks with smooth notches and tabs
        t.RoundNotches = true;
        t.RoundTabs = true;
        t.FieldCornerRadius = 5f;
        t.RowMinHeight = 42f;          // roomier blocks
        t.PaddingX = 11f;
        t.ItemGap = 8f;
        t.FieldHeight = 24f;
        t.FieldPaddingX = 7f;
        t.LabelHeight = 20f;
        // connection shapes in the proportions of the design: a round notch about 1.4 font sizes wide
        t.StatementMinWidth = 46f; t.SlotMinHeight = 30f; t.SlotPaddingY = 6f;
        t.NotchX = 20f; t.NotchWidth = 22f; t.NotchDepth = 9f;
        t.TabWidth = 9f; t.TabHeight = 18f; t.TabTop = 6f;

        // a dark navy interface
        t.AppBackground = Hex("#1f2342");
        t.ToolbarBackground = Hex("#1f2342");
        t.ToolboxBackground = Hex("#252749");     // the rail and the flyout are one colour, as in the design
        t.FlyoutBackground = Hex("#252749");
        t.WorkspaceBackground = Hex("#2b3060");
        t.ConsoleBackground = Hex("#181b33");
        t.ConsoleText = Hex("#dfe3ff");
        t.ButtonColour = Hex("#3d4377");
        t.CategoryShade = 0.6f;
        // the rail of the design: lilac plates with coloured icons, a gradient plate and a white icon when selected
        t.ToolboxWidth = 156f;
        t.CategoryButtonHeight = 46f; t.CategoryGap = 12f; t.CategoryIconSize = 26f;
        t.CategoryPlate = Hex("#494971");
        t.CategoryPlateSelected = Hex("#494971");     // the gradient picture covers it when the app registers one
        t.CategoryText = Hex("#b3b1d1");
        t.CategoryTextSelected = Color.white;
        t.ToolboxCorners = UiCorners.All;
        t.ToolboxCornersWithFlyout = UiCorners.Left;     // the rail and the flyout join into one panel
        t.FlyoutCorners = UiCorners.Right;
        t.FlyoutTitle = true; t.FlyoutTitleSize = 24f; t.FlyoutTitleHeight = 64f; t.FlyoutTitleColour = Color.white;
        t.FlyoutPadding = 24f;
        // the rail collapses to square icon buttons; a round tab on its edge toggles it
        t.ToolboxCollapsible = true; t.ToolboxCollapsedWidth = 86f;
        t.ToggleSize = 44f; t.ToggleTop = 24f;
        t.ToggleColour = Hex("#252749"); t.ToggleIconColour = Hex("#7370a0");
        t.MenuBackground = Hex("#262b50");
        t.MenuHover = Hex("#3d4377");
        t.MenuSelected = Hex("#4a63c8");
        t.DialogPanel = Hex("#262b50");
        t.ToolbarFont.Style = FontStyles.Bold;   // bolder toolbar, slightly larger toolbox
        t.ToolboxFont.Scale = 1.1f;
        t.SelectionColour = Hex("#7aa2ff");
        t.Rules.Add(new BlockStyleRule { Match = "controls_*", StrokeWidth = 3.5f });     // loops and conditions get a heavier outline
        // the "When flag clicked" block of the IconBlocks sample: its own colour and a cap; it is the only block with one
        t.Rules.Add(new BlockStyleRule
        {
            Match = "event_when_clicked", Hat = true,
            OverrideFill = true, Fill = Hex("#8f8f4d"),
            OverrideStroke = true, Stroke = Hex("#d9d77a"),
        });
        t.HatHeight = 23f;      // about 0.55 of a row (42), like the proportions of the design
        t.HatStyle = Blockly.Layout.HatShape.Dome;      // a dome like a "when clicked" hat: rises at the left, slopes down to the edge
        t.ButtonCornerRadius = 12f;    // rounded buttons and panels
        t.PanelCornerRadius = 14f;
        return t;
    }
}
