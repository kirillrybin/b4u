using System.Collections.Generic;
using Blockly;
using Blockly.Editing;
using Blockly.UI;
using UnityEngine;

/// <summary>
/// Blocks with pictures: a "field_image" inside a block and an icon on the toolbox category.
/// The pictures are sprites in Resources/Icons, found by their path; you can also register sprites with
/// BlockImages.Register("name", sprite) or set BlockImages.Resolver.
/// </summary>
public sealed class IconBlocks : IBlockPackage
{
    public string BlocksJson => @"[
      {""type"":""icon_forward"",""message0"":""Move forward %1"",
       ""args0"":[{""type"":""field_image"",""src"":""Icons/move-forward"",""width"":28,""height"":14}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215},
      {""type"":""icon_back"",""message0"":""Move backward %1"",
       ""args0"":[{""type"":""field_image"",""src"":""Icons/move-back"",""width"":28,""height"":14}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215},
      {""type"":""icon_turn_right"",""message0"":""Turn right %1"",
       ""args0"":[{""type"":""field_image"",""src"":""Icons/turn-right"",""width"":22,""height"":22}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215},
      {""type"":""icon_turn_left"",""message0"":""Turn left %1"",
       ""args0"":[{""type"":""field_image"",""src"":""Icons/turn-left"",""width"":22,""height"":22}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":215},
      {""type"":""event_when_clicked"",""message0"":""When %1 clicked"",
       ""args0"":[{""type"":""field_image"",""src"":""Icons/flag"",""width"":22,""height"":22}],
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
            Name = "Moves", ColourHex = Colour.FromHue(215), Icon = "Icons/move-forward",
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
            Name = "Events", ColourHex = Colour.FromHue(60), Icon = "Icons/flag",
            Blocks = new List<Dictionary<string, object>>
            {
                // no previous connection: it starts a stack, so a theme rule with Hat can put a cap on it (see ApplyBlockTheme)
                new Dictionary<string, object> { { "kind", "block" }, { "type", "event_when_clicked" } },
            },
        },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        // the pictures are the icons of the design, in Resources/Icons (found by their path, no registration needed)
        StandardBlockSet.AddPackage(new IconBlocks());
    }
}
