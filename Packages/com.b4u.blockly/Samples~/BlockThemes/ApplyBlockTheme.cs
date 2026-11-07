using Blockly.UI;
using UnityEngine;

/// <summary>
/// Add this component to any object in the Playground scene and press Play.
/// With no theme assigned it builds a light-outline "Candy" look; assign a BlockTheme asset
/// (Assets > Create > Blockly > Block Theme) to use your own. Toggle the component to re-apply after editing the asset.
/// </summary>
public class ApplyBlockTheme : MonoBehaviour
{
    public BlockTheme theme;

    void OnEnable()
    {
        BlockStyle.Theme = theme != null ? theme : Candy();
    }

    void OnDisable()
    {
        BlockStyle.Theme = null;      // back to the classic look
    }

    static BlockTheme Candy()
    {
        var t = BlockTheme.CreateDefault();
        t.name = "Candy";
        t.StrokeScale = 1.35f;         // outline lighter than the fill
        t.StrokeWidth = 2.2f;
        t.FieldStrokeWidth = 0f;
        t.FontSize = 14f;
        t.InputFill = new Color(1f, 1f, 1f, 0.35f);
        t.InputText = Color.white;
        t.DropdownScale = 1.2f;
        t.CornerRadius = 8f;           // rounded blocks with smooth notches and tabs
        t.RoundNotches = true;
        t.RoundTabs = true;
        t.FieldCornerRadius = 4f;
        t.RowMinHeight = 34f;          // roomier blocks
        t.PaddingX = 12f;
        t.ItemGap = 6f;
        t.FieldHeight = 24f;
        t.FieldPaddingX = 8f;
        t.LabelHeight = 20f;
        return t;
    }
}
