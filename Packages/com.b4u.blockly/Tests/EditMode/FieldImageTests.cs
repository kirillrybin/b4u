using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>field_image: a picture of a fixed size in a block's row, never serialized.</summary>
    public class FieldImageTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }

        const string Blocks = @"[{""type"":""t_move"",""message0"":""Move %1"",""args0"":[{""type"":""field_image"",""src"":""arrow.png"",""width"":40,""height"":30,""alt"":""->""}],
            ""previousStatement"":null,""nextStatement"":null,""colour"":210}]";

        static Workspace Load()
        {
            var reg = new BlockRegistry();
            reg.RegisterJson(Blocks);
            var ws = new Workspace(reg);
            WorkspaceJson.Load(ws, "{\"blocks\":{\"languageVersion\":0,\"blocks\":[{\"type\":\"t_move\",\"x\":0,\"y\":0}]}}");
            return ws;
        }

        [Test]
        public void TheImageGetsItsOwnBoxWithTheDeclaredSize()
        {
            var ws = Load();
            var box = new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace().Boxes.Single();
            var image = box.Fields.Single(f => f.ImageSrc != null);
            Assert.AreEqual("arrow.png", image.ImageSrc);
            Assert.AreEqual(40f, image.W); Assert.AreEqual(30f, image.H);
            Assert.GreaterOrEqual(box.H, 30f, "a tall picture makes the row taller");
            var label = box.Fields.Single(f => f.ImageSrc == null);
            Assert.Less(label.X + label.W, image.X + 0.01f, "text comes before the picture");
        }

        [Test]
        public void TheImageIsNotSavedAsAField()
        {
            var json = WorkspaceJson.Save(Load());
            StringAssert.DoesNotContain("arrow", json);
            StringAssert.DoesNotContain("\"fields\"", json);
        }

        [Test]
        public void ToolboxCategoriesCarryAnIcon()
        {
            var def = ToolboxDefinition.FromJson("{\"kind\":\"categoryToolbox\",\"contents\":[{\"kind\":\"category\",\"name\":\"Events\",\"icon\":\"events.png\",\"contents\":[]}]}");
            Assert.AreEqual("events.png", def.Categories[0].Icon);
        }
    }
}
