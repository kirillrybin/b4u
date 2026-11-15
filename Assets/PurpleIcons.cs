using System.Collections.Generic;
using Blockly.UI;
using UnityEngine;

/// <summary>
/// The pictures of the Purple theme: an icon and a gradient plate for each toolbox category, and the can of the delete panel.
/// The sprites are in Resources/Icons, found by their path; you can also register sprites with BlockImages.Register("name", sprite)
/// or set BlockImages.Resolver. They only show while the Purple theme is selected.
/// </summary>
public static class PurpleIcons
{
    // the icons, plates and the trash belong to the Purple look: the other themes of the Theme list show the toolbox without them
    static readonly Dictionary<string, Sprite> skinImages = new Dictionary<string, Sprite>();
    static bool IsPurpleTheme => BlockStyle.Theme != null && BlockStyle.Theme.name == "Purple";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        // the pictures are the icons of the theme, in Resources/Icons (found by their path, no registration needed)
        RegisterCategoryLooks();
        var trash = Resources.Load<Sprite>("Icons/trash");           // the can of the delete panel (white, tinted by DeleteIconColour)
        if (trash != null) skinImages["toolbox/delete"] = trash;
        var previous = BlockImages.Resolver;
        BlockImages.Resolver = key => (IsPurpleTheme && skinImages.TryGetValue(key, out var sprite) ? sprite : null) ?? previous?.Invoke(key);
    }

    // The toolbox of the design: a lilac plate with a coloured icon, and a gradient plate with a white icon while selected.
    // Categories are matched by name; our categories borrow the look of the six of the design.
    static void RegisterCategoryLooks()
    {
        var looks = new Dictionary<string, (string icon, Color from, Color to, Vector2 dir)>
        {
            { "actions",    ("actions",    Hex("#3355f5"), Hex("#78c8ff"), new Vector2(1, -0.3f)) },
            { "logic",      ("logic",      Hex("#8f5a22"), Hex("#ffbd6e"), new Vector2(1, 0.4f)) },
            { "operations", ("operations", Hex("#25a5f5"), Hex("#15e6e6"), new Vector2(1, 0.3f)) },
            { "lists",      ("lists",      Hex("#8f45ff"), Hex("#d3bfff"), new Vector2(0, 1)) },
            { "colour",     ("colour",     Hex("#8f6a00"), Hex("#ffd84d"), new Vector2(1, 0.6f)) },
            { "variables",  ("variables",  Hex("#b04440"), Hex("#ff5a4e"), new Vector2(1, 1)) },
            { "text",       ("text",       Hex("#10966e"), Hex("#82f0be"), new Vector2(1, 1)) },
            { "functions",  ("functions",  Hex("#be28be"), Hex("#ff8cd2"), new Vector2(1, 1)) },
            { "search",     ("search",     Hex("#5c6475"), Hex("#b9c0d0"), new Vector2(1, 0.5f)) },
        };
        var names = new Dictionary<string, string>
        {
            { "Loops", "actions" }, { "Logic", "logic" }, { "Math", "operations" }, { "Lists", "lists" }, { "Colour", "colour" },
            { "Variables", "variables" }, { "Text", "text" }, { "Functions", "functions" }, { "Search", "search" },
        };
        foreach (var kv in names)
        {
            var look = looks[kv.Value];
            var icon = Resources.Load<Sprite>("Icons/category-" + look.icon);
            var white = Resources.Load<Sprite>("Icons/category-" + look.icon + "-selected");
            if (icon == null) continue;
            skinImages["category/" + kv.Key] = icon;
            if (white != null) skinImages["category/" + kv.Key + "@selected"] = white;
            skinImages["plate/" + kv.Key] = Plate(look.from, look.to, look.dir);
        }
    }

    static Color Hex(string html) { ColorUtility.TryParseHtmlString(html, out var c); return c; }

    /// <summary>A rounded, nine-sliced plate with a linear gradient: the corner is 28 texels, shown as 12 canvas units.</summary>
    static Sprite Plate(Color from, Color to, Vector2 dir)
    {
        const int S = 64, R = 28;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[S * S];
        dir = dir.normalized;
        float min = float.MaxValue, max = float.MinValue;
        foreach (var c in new[] { new Vector2(0, 0), new Vector2(S, 0), new Vector2(0, S), new Vector2(S, S) })
        { float d = Vector2.Dot(c, dir); min = Mathf.Min(min, d); max = Mathf.Max(max, d); }
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, R, S - R), cy = Mathf.Clamp(y + 0.5f, R, S - R);
                float dist = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = Mathf.Clamp01(R - dist + 0.5f);
                float t = Mathf.InverseLerp(min, max, Vector2.Dot(new Vector2(x + 0.5f, y + 0.5f), dir));
                var col = Color.Lerp(from, to, t); col.a = a;
                px[y * S + x] = col;
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 233f, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
    }
}
