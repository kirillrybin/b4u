using System.Collections.Generic;
using Blockly;
using Blockly.Editing;
using Blockly.UI;
using UnityEngine;

/// <summary>
/// Blocks with pictures: a "field_image" inside a block and an icon on the toolbox category.
/// The pictures are sprites in Resources/FigmaSkin, found by their path; you can also register sprites with
/// BlockImages.Register("name", sprite) or set BlockImages.Resolver.
/// </summary>
public sealed class FigmaIconBlocks : IBlockPackage
{
    public string BlocksJson => @"[
      {""type"":""icon_forward"",""message0"":""Move forward %1"",
       ""args0"":[{""type"":""field_image"",""src"":""FigmaSkin/move-forward"",""width"":28,""height"":14}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215},
      {""type"":""icon_back"",""message0"":""Move backward %1"",
       ""args0"":[{""type"":""field_image"",""src"":""FigmaSkin/move-back"",""width"":28,""height"":14}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215},
      {""type"":""icon_turn_right"",""message0"":""Turn right %1"",
       ""args0"":[{""type"":""field_image"",""src"":""FigmaSkin/turn-right"",""width"":22,""height"":22}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215},
      {""type"":""icon_turn_left"",""message0"":""Turn left %1"",
       ""args0"":[{""type"":""field_image"",""src"":""FigmaSkin/turn-left"",""width"":22,""height"":22}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215},
      {""type"":""event_when_clicked"",""message0"":""When %1 clicked"",
       ""args0"":[{""type"":""field_image"",""src"":""FigmaSkin/flag"",""width"":22,""height"":22}],
       ""nextStatement"":null,""colour"":60}
    ]";

    public void RegisterHandlers(HandlerRegistry r)
    {
        r.Simple("icon_forward", (it, b) => it.Print("forward"));
        r.Simple("icon_back", (it, b) => it.Print("backward"));
        r.Simple("icon_turn_right", (it, b) => it.Print("turn right"));
        r.Simple("icon_turn_left", (it, b) => it.Print("turn left"));
        r.Simple("event_when_clicked", (it, b) => it.Print("clicked"));
    }

    public IEnumerable<ToolboxCategory> ToolboxCategories => new[]
    {
        new ToolboxCategory
        {
            Name = "Moves", ColourHex = Colour.FromHue(215),
            Blocks = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "kind", "block" }, { "type", "icon_forward" } },
                new Dictionary<string, object> { { "kind", "block" }, { "type", "icon_back" } },
                new Dictionary<string, object> { { "kind", "block" }, { "type", "icon_turn_right" } },
                new Dictionary<string, object> { { "kind", "block" }, { "type", "icon_turn_left" } },
            },
        },
        new ToolboxCategory
        {
            Name = "Events", ColourHex = Colour.FromHue(60),
            Blocks = new List<Dictionary<string, object>>
            {
                // no previous connection: it starts a stack, so a theme rule with Hat can put a cap on it (see FigmaSkin)
                new Dictionary<string, object> { { "kind", "block" }, { "type", "event_when_clicked" } },
            },
        },
    };

    // the icons, plates and the trash belong to the Figma look: the other themes of the Theme list show the toolbox without them
    static readonly Dictionary<string, Sprite> skinImages = new Dictionary<string, Sprite>();
    static bool IsFigmaTheme => BlockStyle.Theme != null && BlockStyle.Theme.name == "Figma";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        // the pictures are the icons of the design, in Resources/FigmaSkin (found by their path, no registration needed)
        StandardBlockSet.AddPackage(new FigmaIconBlocks());
        RegisterCategoryLooks();
        var trash = Resources.Load<Sprite>("FigmaSkin/trash");           // the can of the delete panel (white, tinted by DeleteIconColour)
        if (trash != null) skinImages["toolbox/delete"] = trash;
        var previous = BlockImages.Resolver;
        BlockImages.Resolver = key => (IsFigmaTheme && skinImages.TryGetValue(key, out var sprite) ? sprite : null) ?? previous?.Invoke(key);
    }

    // The toolbox of the design: a lilac plate with a coloured icon, and a gradient plate with a white icon while selected.
    // Categories are matched by name; our categories borrow the look of the six of the design.
    static void RegisterCategoryLooks()
    {
        var looks = new Dictionary<string, (string icon, Color from, Color to, Vector2 dir)>
        {
            { "events",     ("events",     Hex("#7c7a40"), Hex("#ece98c"), new Vector2(1, 0.3f)) },
            { "actions",    ("actions",    Hex("#3355f5"), Hex("#78c8ff"), new Vector2(1, -0.3f)) },
            { "control",    ("control",    Hex("#8f5a22"), Hex("#ffbd6e"), new Vector2(1, 0.4f)) },
            { "operations", ("operations", Hex("#25a5f5"), Hex("#15e6e6"), new Vector2(1, 0.3f)) },
            { "sensors",    ("sensors",    Hex("#8f45ff"), Hex("#d3bfff"), new Vector2(0, 1)) },
            { "variables",  ("variables",  Hex("#b04440"), Hex("#ff5a4e"), new Vector2(1, 1)) },
            { "text",       ("text",       Hex("#10966e"), Hex("#82f0be"), new Vector2(1, 1)) },
            { "functions",  ("functions",  Hex("#be28be"), Hex("#ff8cd2"), new Vector2(1, 1)) },
            { "search",     ("search",     Hex("#5c6475"), Hex("#b9c0d0"), new Vector2(1, 0.5f)) },
        };
        var names = new Dictionary<string, string>
        {
            { "Events", "events" }, { "Moves", "actions" }, { "Loops", "actions" }, { "Logic", "control" },
            { "Math", "operations" }, { "Lists", "sensors" }, { "Colour", "functions" }, { "Variables", "variables" }, { "Text", "text" }, { "Functions", "functions" }, { "Search", "search" },
        };
        foreach (var kv in names)
        {
            var look = looks[kv.Value];
            var icon = Resources.Load<Sprite>("FigmaSkin/category-" + look.icon);
            var white = Resources.Load<Sprite>("FigmaSkin/category-" + look.icon + "-selected");
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
