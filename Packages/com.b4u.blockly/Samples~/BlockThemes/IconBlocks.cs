using System.Collections.Generic;
using Blockly;
using Blockly.Editing;
using Blockly.UI;
using UnityEngine;

/// <summary>
/// Blocks with pictures: a "field_image" inside a block and an icon on the toolbox category.
/// The pictures are drawn in code here so the sample needs no assets; in your app, register your own sprites with
/// BlockImages.Register("name", sprite), set BlockImages.Resolver, or put Sprites in a Resources folder and use their path.
/// </summary>
public sealed class IconBlocks : IBlockPackage
{
    public string BlocksJson => @"[
      {""type"":""icon_forward"",""message0"":""Move forward %1"",
       ""args0"":[{""type"":""field_image"",""src"":""sample/arrow-right"",""width"":28,""height"":20}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215},
      {""type"":""icon_back"",""message0"":""Move backward %1"",
       ""args0"":[{""type"":""field_image"",""src"":""sample/arrow-left"",""width"":28,""height"":20}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215}
    ]";

    public void RegisterHandlers(HandlerRegistry r)
    {
        r.Simple("icon_forward", (it, b) => it.Print("forward"));
        r.Simple("icon_back", (it, b) => it.Print("backward"));
    }

    public IEnumerable<ToolboxCategory> ToolboxCategories => new[]
    {
        new ToolboxCategory
        {
            Name = "Moves", ColourHex = Colour.FromHue(215), Icon = "sample/arrow-right",
            Blocks = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "kind", "block" }, { "type", "icon_forward" } },
                new Dictionary<string, object> { { "kind", "block" }, { "type", "icon_back" } },
            },
        },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        BlockImages.Register("sample/arrow-right", Arrow(false));
        BlockImages.Register("sample/arrow-left", Arrow(true));
        StandardBlockSet.AddPackage(new IconBlocks());
    }

    static Sprite Arrow(bool flip)
    {
        const int W = 56, H = 40;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x0 = 0; x0 < W; x0++)
            {
                int x = flip ? W - 1 - x0 : x0;
                bool shaft = x >= 4 && x <= 40 && Mathf.Abs(y - H / 2) <= 2;

                bool tip = x >= 34 && x <= 52 && Mathf.Abs(y - H / 2) <= (52 - x) * 0.7f && Mathf.Abs(y - H / 2) >= (52 - x) * 0.7f - 3.5f;
                px[y * W + x0] = shaft || tip ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
    }
}
