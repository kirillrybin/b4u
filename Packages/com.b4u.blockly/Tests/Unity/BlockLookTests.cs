using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Blockly.UI.Tests
{
    public class BlockLookTests
    {
        GameObject root;
        [TearDown] public void TearDown() { BlockStyle.Theme = null; if (root != null) Object.DestroyImmediate(root); }

        const string Blocks = @"[{""type"":""a_one"",""message0"":""one"",""previousStatement"":null,""nextStatement"":null,""colour"":120},
            {""type"":""a_two"",""message0"":""two"",""previousStatement"":null,""nextStatement"":null,""colour"":120},
            {""type"":""b_three"",""message0"":""three"",""previousStatement"":null,""nextStatement"":null,""colour"":120}]";

        static Block NewBlock(string type, bool disabled = false)
        {
            var reg = new BlockRegistry();
            reg.RegisterJson(Blocks);
            var ws = new Workspace(reg);
            var b = ws.CreateBlock(type);
            b.Disabled = disabled;
            return b;
        }

        [Test]
        public void WithoutRulesTheLookIsTheThemesAndTheDefinitionsColour()
        {
            var look = BlockStyle.Look(NewBlock("a_one"), false);
            Assert.AreEqual(BlockStyle.Parse(Colour.FromHue(120)), look.Fill);
            Assert.AreEqual(1.2f, look.StrokeWidth, 1e-4f);
            Assert.AreEqual(BlockStyle.InputFill, look.InputFill);
        }

        [Test]
        public void RulesMatchByTypeByPrefixAndForAllInOrder()
        {
            var t = BlockTheme.CreateDefault();
            t.Rules.Add(new BlockStyleRule { Match = "a_*", OverrideFill = true, Fill = Color.red });
            t.Rules.Add(new BlockStyleRule { Match = "a_two", OverrideFill = true, Fill = Color.blue, OverrideStroke = true, Stroke = Color.yellow, StrokeWidth = 3 });
            t.Rules.Add(new BlockStyleRule { Match = "*", OverrideLabelText = true, LabelText = Color.cyan });
            BlockStyle.Theme = t;

            var one = BlockStyle.Look(NewBlock("a_one"), false);
            var two = BlockStyle.Look(NewBlock("a_two"), false);
            var three = BlockStyle.Look(NewBlock("b_three"), false);
            Assert.AreEqual(Color.red, one.Fill, "prefix rule");
            Assert.AreEqual(Color.blue, two.Fill, "a later rule wins");
            Assert.AreEqual(Color.yellow, two.Stroke);
            Assert.AreEqual(3f, two.StrokeWidth);
            Assert.AreEqual(1.2f, one.StrokeWidth, 1e-4f, "unticked parts stay the theme's");
            Assert.AreEqual(BlockStyle.Parse(Colour.FromHue(120)), three.Fill, "no rule matches b_three's fill");
            Assert.AreEqual(Color.cyan, three.LabelText, "the * rule");
            Assert.AreEqual(Color.cyan, one.LabelText);
        }

        [Test]
        public void ADisabledBlockIsPulledTowardsGreyAndShadowsAreStillLighter()
        {
            var normal = BlockStyle.Look(NewBlock("a_one"), false);
            var disabled = BlockStyle.Look(NewBlock("a_one", true), false);
            Assert.AreNotEqual(normal.Fill, disabled.Fill);
            float sat(Color c) => Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);
            Assert.Less(sat(disabled.Fill), sat(normal.Fill), "less saturated");
            var t = BlockTheme.CreateDefault(); t.DisabledBlend = 0;
            BlockStyle.Theme = t;
            Assert.AreEqual(normal.Fill, BlockStyle.Look(NewBlock("a_one", true), false).Fill, "a zero blend turns it off");
        }

        [Test]
        public void TheViewDrawsABlockWithItsRule()
        {
            var t = BlockTheme.CreateDefault();
            t.Rules.Add(new BlockStyleRule { Match = "text_print", OverrideFill = true, Fill = new Color(1, 0, 1) });
            BlockStyle.Theme = t;
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load("{\"blocks\":{\"languageVersion\":0,\"blocks\":[{\"type\":\"text_print\",\"x\":10,\"y\":10}]}}"));
            var body = go.GetComponentsInChildren<PolygonGraphic>().First();
            Assert.AreEqual(new Color(1, 0, 1), body.color);
        }

        [Test]
        public void SelectionAndHighlightColoursComeFromTheTheme()
        {
            var t = BlockTheme.CreateDefault();
            t.SelectionColour = Color.green; t.HighlightColour = Color.magenta; t.SnapColour = Color.cyan;
            BlockStyle.Theme = t;
            Assert.AreEqual(Color.green, WorkspaceView.SelectionColour);
            Assert.AreEqual(Color.magenta, WorkspaceView.HighlightColour);
            Assert.AreEqual(Color.cyan, WorkspaceView.SnapColour);
        }

        [Test]
        public void ConnectionShapeSettingsReachTheGeometryAndAreClamped()
        {
            var t = BlockTheme.CreateDefault();
            t.TabWidth = 10; t.TabHeight = 100; t.TabTop = 100; t.NotchWidth = 100; t.NotchX = 100;
            BlockStyle.Theme = t;
            var g = Blockly.Layout.BlockGeometry.Current;
            Assert.AreEqual(10f, g.TabWidth);
            Assert.LessOrEqual(g.TabTop + g.TabHeight, 24f, "the tab fits the smallest row");
            Assert.LessOrEqual(g.NotchX + g.NotchWidth, 30f, "the notch fits the statement width");
        }
    }
}
