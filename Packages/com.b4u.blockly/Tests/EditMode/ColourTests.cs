using System.Collections.Generic;
using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>The colour field and the colour blocks.</summary>
    public class ColourTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static Workspace Load(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        static string Print(string expression)
        {
            var ws = Load("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\">" + expression + "</value></block>");
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion(100000);
            Assert.IsNull(it.Error);
            return lines.Single();
        }

        static string Num(double n) => "<block type=\"math_number\"><field name=\"NUM\">" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + "</field></block>";

        [Test]
        public void NormalizeAcceptsShortAndLongHexInAnyCaseAndRefusesTheRest()
        {
            Assert.AreEqual("#ff8800", Colour.Normalize("#F80"));
            Assert.AreEqual("#aabbcc", Colour.Normalize("#AABBCC"));
            foreach (var bad in new[] { null, "", "ff0000", "#ff00", "#gg0000", "#ff00000" }) Assert.IsNull(Colour.Normalize(bad), bad);
        }

        [Test]
        public void ThePaletteHasSeventyDistinctColoursInRowsOfSeven()
        {
            Assert.AreEqual(70, Colour.Palette.Length);
            Assert.AreEqual(70, Colour.Palette.Distinct().Count());
            Assert.AreEqual(7, Colour.PaletteColumns);
            Assert.IsTrue(Colour.Palette.All(c => Colour.Normalize(c) == c));
        }

        [Test]
        public void ThePickerBlockGivesItsColourAndTheLayoutDrawsASwatch()
        {
            const string picker = "<block type=\"colour_picker\"><field name=\"COLOUR\">#3366ff</field></block>";
            Assert.AreEqual("#3366ff", Print(picker));
            var ws = Load("<block type=\"colour_picker\" x=\"0\" y=\"0\"><field name=\"COLOUR\">#3366ff</field></block>");
            var field = new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace().ByBlock[ws.TopBlocks[0]].Fields.Single();
            Assert.AreEqual("#3366ff", field.Colour);
            Assert.AreEqual(Metrics.ColourWidth, field.W);
            Assert.IsTrue(field.Editable);
            Assert.IsTrue(string.IsNullOrEmpty(field.Text), "a swatch, no text");
        }

        [Test]
        public void ANewPickerStartsRedAndAFieldAcceptsOnlyColours()
        {
            var ws = Load("<block type=\"colour_picker\" x=\"0\" y=\"0\"></block>");
            var block = ws.TopBlocks[0];
            var layouter = new BlockLayouter(ws, new FixedWidth());
            Assert.AreEqual("#ff0000", block.GetFieldValue("COLOUR"));
            var field = block.GetField("COLOUR");
            Assert.IsTrue(FieldEditing.IsColour(field));
            Assert.IsFalse(FieldEditing.IsEditableText(field));
            Assert.IsTrue(FieldEditing.SetValue(ws, block, field, "#0F0", layouter));
            Assert.AreEqual("#00ff00", block.GetFieldValue("COLOUR"), "a short colour is stored in full");
            Assert.IsFalse(FieldEditing.SetValue(ws, block, field, "green", layouter));
            Assert.AreEqual("#00ff00", block.GetFieldValue("COLOUR"));
        }

        [Test]
        public void RgbTakesPercentagesAndClampsThem()      // 50 % is 127.49999999999999 in floating point, which Blockly rounds down to 7f as well
        {
            Assert.AreEqual("#ff7f00", Print("<block type=\"colour_rgb\"><value name=\"RED\">" + Num(100) + "</value><value name=\"GREEN\">" + Num(50) + "</value><value name=\"BLUE\">" + Num(0) + "</value></block>"));
            Assert.AreEqual("#ff0000", Print("<block type=\"colour_rgb\"><value name=\"RED\">" + Num(500) + "</value><value name=\"GREEN\">" + Num(-5) + "</value><value name=\"BLUE\">" + Num(0) + "</value></block>"));
        }

        [Test]
        public void BlendMixesTwoColours()
        {
            string Blend(string a, string b, double r) => Print("<block type=\"colour_blend\"><value name=\"COLOUR1\"><block type=\"colour_picker\"><field name=\"COLOUR\">" + a + "</field></block></value>" +
                "<value name=\"COLOUR2\"><block type=\"colour_picker\"><field name=\"COLOUR\">" + b + "</field></block></value><value name=\"RATIO\">" + Num(r) + "</value></block>");
            Assert.AreEqual("#800080", Blend("#ff0000", "#0000ff", 0.5));
            Assert.AreEqual("#ff0000", Blend("#ff0000", "#0000ff", 0));
            Assert.AreEqual("#0000ff", Blend("#ff0000", "#0000ff", 7), "the ratio is clamped to 0..1");
        }

        [Test]
        public void ARandomColourIsAColour()
        {
            for (int i = 0; i < 20; i++) Assert.IsNotNull(Colour.Normalize(Print("<block type=\"colour_random\"></block>")));
        }

        [Test]
        public void TheColourRoundTripsThroughJsonAndXml()
        {
            var ws = Load("<block type=\"colour_picker\" x=\"0\" y=\"0\"><field name=\"COLOUR\">#123abc</field></block>");
            var back = TestSupport.NewWorkspace();
            WorkspaceJson.Load(back, WorkspaceJson.Save(ws));
            Assert.AreEqual("#123abc", back.TopBlocks[0].GetFieldValue("COLOUR"));
        }
    }
}
