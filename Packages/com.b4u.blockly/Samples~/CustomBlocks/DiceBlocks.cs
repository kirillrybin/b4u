using System.Collections.Generic;
using Blockly;
using Blockly.Editing;
using Blockly.UI;
using UnityEngine;

/// <summary>
/// Example of adding your own blocks. Put this file anywhere in your project; the blocks appear in the Playground
/// (a "Dice" category in the toolbox, a working interpreter, saving and loading) with no change to the package.
/// </summary>
public sealed class DiceBlocks : IBlockPackage
{
    // 1. How the blocks look: Blockly's JSON block format.
    public string BlocksJson => @"[
      {""type"":""dice_roll"",""message0"":""roll a d%1"",
       ""args0"":[{""type"":""field_number"",""name"":""SIDES"",""value"":6,""min"":2,""max"":1000,""precision"":1}],
       ""output"":""Number"",""colour"":20,""tooltip"":""A random whole number from 1 to the number of sides.""},
      {""type"":""dice_shout"",""message0"":""shout %1"",
       ""args0"":[{""type"":""input_value"",""name"":""TEXT""}],
       ""previousStatement"":null,""nextStatement"":null,""colour"":20}
    ]";

    // 2. What the blocks do: an expression returns a value, a statement runs.
    public void RegisterHandlers(HandlerRegistry r)
    {
        r.Expression("dice_roll", (it, block) =>
        {
            int sides = (int)JsValue.ToNumber(block.GetFieldValue("SIDES"));
            return (double)it.Random.Next(1, System.Math.Max(sides, 1) + 1);
        });
        r.Simple("dice_shout", (it, block) => it.Print(JsValue.ToStr(it.EvalInput(block, "TEXT", "")).ToUpperInvariant() + "!"));
    }

    // 3. Where to pick them from: an extra toolbox category (optional).
    public IEnumerable<ToolboxCategory> ToolboxCategories => new[]
    {
        new ToolboxCategory
        {
            Name = "Dice", ColourHex = Colour.FromHue(20),
            Blocks = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "kind", "block" }, { "type", "dice_roll" } },
                new Dictionary<string, object> { { "kind", "block" }, { "type", "dice_shout" } },
            },
        },
    };

    // 4. Plug it in before the first workspace is created.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() => StandardBlockSet.AddPackage(new DiceBlocks());
}
