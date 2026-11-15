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
        var t = theme != null ? theme : Candy(font);
        BlockThemeCatalog.Register(theme != null ? theme.name : "Purple", t);
        BlockStyle.Theme = t;
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
        t.name = "Purple";
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
        // while a block is dragged the rail becomes the delete panel: dashed red border, trash can, label
        t.ToolboxDeletePanel = true; t.ToolboxDeleteHover = Hex("#2f2848");
        t.DeleteBorderColour = Hex("#e04a4a"); t.DeleteBorderWidth = 2f; t.DeleteDashLength = 8f; t.DeleteDashGap = 8f;
        t.DeleteIconColour = Hex("#dc544b"); t.DeleteIconSize = 40f;
        t.DeleteLabel = "Delete"; t.DeleteLabelColour = Color.white; t.DeleteLabelSize = 18f;
        t.MenuBackground = Hex("#262b50");
        t.MenuHover = Hex("#3d4377");
        t.MenuSelected = Hex("#4a63c8");
        t.DialogPanel = Hex("#262b50");
        t.ToolbarFont.Style = FontStyles.Bold;   // bolder toolbar, slightly larger toolbox
        t.ToolboxFont.Scale = 1.1f;
        t.SelectionColour = Hex("#7aa2ff");
        t.Rules.Add(new BlockStyleRule { Match = "controls_*", StrokeWidth = 3.5f });     // loops and conditions get a heavier outline
        t.ButtonCornerRadius = 12f;    // rounded buttons and panels
        t.PanelCornerRadius = 14f;
        return t;
    }
}
