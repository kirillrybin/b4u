using UnityEngine;

namespace Blockly.UI
{
    /// <summary>
    /// Ready-made themes that come with the package: "Dark" (a dark workspace with a grid) and "High Contrast" (heavy white outlines on black).
    /// They are in <see cref="BlockThemeCatalog"/> from the start of the app; <see cref="Dark"/> and <see cref="HighContrast"/> build
    /// a fresh copy to start your own theme from.
    /// </summary>
    public static class BuiltInThemes
    {
        public const string DarkName = "Dark", HighContrastName = "High Contrast";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAll() => Register();

        /// <summary>Adds Dark and High Contrast to the catalog (done at start-up; call again after <see cref="BlockThemeCatalog.Clear"/>).</summary>
        public static void Register()
        {
            BlockThemeCatalog.Register(DarkName, Dark);
            BlockThemeCatalog.Register(HighContrastName, HighContrast);
        }

        static Color Hex(string html) { ColorUtility.TryParseHtmlString(html, out var c); return c; }

        /// <summary>Block colours as in the classic look, on a dark workspace with softer outlines.</summary>
        public static BlockTheme Dark()
        {
            var t = BlockTheme.CreateDefault();
            t.name = DarkName;
            t.AppBackground = Hex("#16181d");
            t.ToolbarBackground = Hex("#16181d");
            t.ToolboxBackground = Hex("#1d2027");
            t.FlyoutBackground = Hex("#22262e");
            t.WorkspaceBackground = Hex("#2a2e37");
            t.GridSpacing = 24f;                       // a dotted grid; blocks dropped on their own land on it
            t.GridColour = new Color(1f, 1f, 1f, 0.14f);
            t.ConsoleBackground = Hex("#101216");
            t.ConsoleText = Hex("#d6d9e0");
            t.ButtonColour = Hex("#3a3f4c");
            t.MenuBackground = Hex("#1d2027");
            t.MenuHover = Hex("#343947");
            t.MenuSelected = Hex("#3f5fa8");
            t.DialogPanel = Hex("#1d2027");
            t.SelectionColour = Hex("#7aa2ff");
            t.StrokeScale = 0.55f;
            return t;
        }

        /// <summary>Black surroundings, heavy white outlines and white fields with black text: easy to tell apart at a glance.</summary>
        public static BlockTheme HighContrast()
        {
            var t = BlockTheme.CreateDefault();
            t.name = HighContrastName;
            t.AppBackground = Color.black;
            t.ToolbarBackground = Color.black;
            t.ToolboxBackground = Hex("#0a0a0a");
            t.FlyoutBackground = Color.black;
            t.WorkspaceBackground = Color.black;
            t.ConsoleBackground = Color.black;
            t.ConsoleText = Hex("#ffff66");
            t.ButtonColour = Hex("#1a1a1a");
            t.ButtonText = Color.white;
            t.MenuBackground = Color.black;
            t.MenuHover = Hex("#333333");
            t.MenuSelected = Hex("#0050c8");
            t.DialogPanel = Hex("#0a0a0a");
            t.CategoryShade = 0.5f;
            t.StrokeScale = 2.6f;            // outline lighter than the fill, so it reads as a white frame
            t.StrokeWidth = 3f;
            t.FieldStrokeWidth = 2f;
            t.InputFill = Color.white;
            t.InputText = Color.black;
            t.SelectionColour = Hex("#ffff00");
            t.SelectionWidth = 4f;
            t.HighlightColour = Hex("#00ffff");
            t.FontStyle = TMPro.FontStyles.Bold;
            return t;
        }
    }
}
