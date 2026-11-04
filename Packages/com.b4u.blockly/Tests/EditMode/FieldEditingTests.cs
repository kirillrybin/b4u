using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class FieldEditingTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static Workspace FromXml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        static string Run(Workspace ws)
        {
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion(100000);
            Assert.IsNull(it.Error);
            return string.Join(",", lines);
        }

        // ---------------------------------------------------------------- number validation, checked against real Blockly
        public sealed class Case { public double? Min, Max; public double Precision; public string Input; public bool Valid; public string Value; }

        static List<Case> Cases()
        {
            var json = (List<object>)Json.Json.Parse(File.ReadAllText(Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "field-number.cases.json")));
            return json.Select(o =>
            {
                var d = (Dictionary<string, object>)o;
                return new Case
                {
                    Min = d["min"] as double?, Max = d["max"] as double?, Precision = (double)d["precision"],
                    Input = (string)d["input"], Valid = (bool)d["valid"], Value = d["value"] as string,
                };
            }).ToList();
        }

        public static IEnumerable<TestCaseData> NumberCases()
        {
            int i = 0;
            foreach (var c in Cases())
                yield return new TestCaseData(c).SetName($"Number_{i++:000}_min{c.Min}_max{c.Max}_p{c.Precision}_in[{c.Input}]");
        }

        static Field NumberField(double? min, double? max, double precision)
        {
            var spec = new ArgSpec { Kind = ArgKind.Field, FieldType = "field_number", Name = "NUM", Text = "0", Min = min, Max = max, Precision = precision > 0 ? precision : (double?)null };
            return new Field { Name = "NUM", Spec = spec, Value = "0" };
        }

        [TestCaseSource(nameof(NumberCases))]
        public void TypedNumbersBehaveLikeBlocklysFieldNumber(Case c)
        {
            bool ok = FieldEditing.TryNormalize(NumberField(c.Min, c.Max, c.Precision), c.Input, out var value);
            Assert.AreEqual(c.Valid, ok, "validity of '" + c.Input + "'");
            if (c.Valid) Assert.AreEqual(c.Value, value, "value of '" + c.Input + "'");
        }

        [Test]
        public void TheReferenceTableCoversTheInterestingInputs()
        {
            var cases = Cases();
            Assert.GreaterOrEqual(cases.Count, 150);
            Assert.IsTrue(cases.Any(c => !c.Valid), "includes rejected input");
            Assert.IsTrue(cases.Any(c => c.Valid && c.Input == "1,000"), "includes thousands separators");
            Assert.IsTrue(cases.Select(c => c.Precision).Distinct().Count() >= 4, "several precisions");
        }

        // ---------------------------------------------------------------- JS number parsing
        [TestCase("42", 42.0)] [TestCase("  7  ", 7.0)] [TestCase("", 0.0)] [TestCase("0x10", 16.0)] [TestCase("0b101", 5.0)] [TestCase("0o17", 15.0)]
        [TestCase("-1.5e2", -150.0)] [TestCase(".5", 0.5)] [TestCase("5.", 5.0)] [TestCase("+3", 3.0)] [TestCase("1e400", double.PositiveInfinity)]
        [TestCase("-1e400", double.NegativeInfinity)] [TestCase("1e-400", 0.0)] [TestCase("Infinity", double.PositiveInfinity)]
        public void ParseNumberAcceptsWhatJavaScriptAccepts(string text, double expected)
            => Assert.AreEqual(expected, JsValue.ParseNumber(text));

        [TestCase("abc")] [TestCase("12abc")] [TestCase("1 2")] [TestCase("-")] [TestCase("1e")] [TestCase("0x")] [TestCase("0xZZ")] [TestCase(".")] [TestCase("1..2")] [TestCase("infinity")]
        public void ParseNumberRejectsJunk(string text) => Assert.IsTrue(double.IsNaN(JsValue.ParseNumber(text)), text);

        // ---------------------------------------------------------------- applying edits to the model
        [Test]
        public void TextEditChangesTheValueTheProgramUses()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">abc</field></block></value></block>");
            var text = ws.TopBlocks[0].GetInputTargetBlock("TEXT");
            Assert.AreEqual("abc", Run(ws));
            Assert.IsTrue(FieldEditing.SetValue(ws, text, text.GetField("TEXT"), "hello there"));
            Assert.AreEqual("hello there", Run(ws));
            Assert.IsFalse(FieldEditing.SetValue(ws, text, text.GetField("TEXT"), "hello there"), "same value: nothing changed");
            Assert.IsTrue(FieldEditing.SetValue(ws, text, text.GetField("TEXT"), ""), "empty text is allowed");
            Assert.AreEqual("", Run(ws));
        }

        [Test]
        public void NumberEditChangesTheResultAndRejectsGarbage()
        {
            var ws = FromXml("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"math_arithmetic\"><field name=\"OP\">ADD</field>" +
                             "<value name=\"A\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value>" +
                             "<value name=\"B\"><block type=\"math_number\"><field name=\"NUM\">2</field></block></value></block></value></block>");
            var a = ws.TopBlocks[0].GetInputTargetBlock("TEXT").GetInputTargetBlock("A");
            Assert.AreEqual("3", Run(ws));
            Assert.IsTrue(FieldEditing.SetValue(ws, a, a.GetField("NUM"), "40"));
            Assert.AreEqual("42", Run(ws));
            Assert.IsFalse(FieldEditing.SetValue(ws, a, a.GetField("NUM"), "forty"), "rejected");
            Assert.AreEqual("40", a.GetFieldValue("NUM"), "value is unchanged after a rejected edit");
            Assert.IsTrue(FieldEditing.SetValue(ws, a, a.GetField("NUM"), "1,000"));
            Assert.AreEqual("1002", Run(ws));
        }

        [Test]
        public void ShadowFieldsAreEditableAndStayShadows()
        {
            var ws = FromXml("<block type=\"controls_repeat_ext\" x=\"0\" y=\"0\"><value name=\"TIMES\"><shadow type=\"math_number\"><field name=\"NUM\">3</field></shadow></value>" +
                             "<statement name=\"DO\"><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">x</field></block></value></block></statement></block>");
            var shadow = ws.TopBlocks[0].GetInput("TIMES").Connection.Shadow;
            Assert.AreEqual("x,x,x", Run(ws));
            Assert.IsTrue(FieldEditing.SetValue(ws, shadow, shadow.GetField("NUM"), "5"));
            Assert.IsTrue(shadow.IsShadow);
            Assert.AreEqual("x,x,x,x,x", Run(ws));
        }

        [Test]
        public void EditedValuesSurviveSaveAndLoad()
        {
            var ws = FromXml("<block type=\"text\" x=\"0\" y=\"0\"><field name=\"TEXT\">a</field></block><block type=\"math_number\" x=\"0\" y=\"50\"><field name=\"NUM\">1</field></block>");
            FieldEditing.SetValue(ws, ws.TopBlocks[0], ws.TopBlocks[0].GetField("TEXT"), "quote \" and <tag> & unicode ✓");
            FieldEditing.SetValue(ws, ws.TopBlocks[1], ws.TopBlocks[1].GetField("NUM"), "2.50");
            var again = TestSupport.NewWorkspace();
            WorkspaceJson.Load(again, WorkspaceJson.Save(ws));
            Assert.AreEqual("quote \" and <tag> & unicode ✓", again.TopBlocks[0].GetFieldValue("TEXT"));
            Assert.AreEqual("2.5", again.TopBlocks[1].GetFieldValue("NUM"), "2.50 is stored as the number 2.5");
        }

        [Test]
        public void LongerTextMakesTheBlockWider()
        {
            var ws = FromXml("<block type=\"text\" x=\"0\" y=\"0\"><field name=\"TEXT\">ab</field></block>");
            var layouter = new BlockLayouter(ws, new FixedWidth());
            float before = layouter.LayoutWorkspace().ByBlock[ws.TopBlocks[0]].W;
            FieldEditing.SetValue(ws, ws.TopBlocks[0], ws.TopBlocks[0].GetField("TEXT"), "abcdefghij");
            float after = layouter.LayoutWorkspace().ByBlock[ws.TopBlocks[0]].W;
            Assert.AreEqual(before + 8 * 6, after, 1e-3);
        }

        [Test]
        public void OnlyTextAndNumberFieldsAreEditableAsText()
        {
            var ws = TestSupport.NewWorkspace();
            Assert.IsTrue(FieldEditing.IsEditableText(ws.CreateBlock("text").GetField("TEXT")));
            Assert.IsTrue(FieldEditing.IsEditableText(ws.CreateBlock("math_number").GetField("NUM")));
            Assert.IsFalse(FieldEditing.IsEditableText(ws.CreateBlock("logic_boolean").GetField("BOOL")), "dropdown");
            Assert.IsFalse(FieldEditing.IsEditableText(ws.CreateBlock("variables_get").GetField("VAR")), "variable picker");
        }

        // ---------------------------------------------------------------- procedure names
        const string Procs =
            "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"0\"><field name=\"NAME\">foo</field><statement name=\"STACK\"><block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">in foo</field></block></value></block></statement></block>" +
            "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"200\"><field name=\"NAME\">bar</field></block>" +
            "<block type=\"procedures_callnoreturn\" x=\"0\" y=\"400\"><mutation name=\"foo\"></mutation></block>";

        static Block Def(Workspace ws, string name) => ws.TopBlocks.First(b => ProcedureInfo.IsDefinition(b) && b.GetFieldValue("NAME") == name);

        [Test]
        public void RenamingAProcedureUpdatesItsCallersAndTheProgramStillRuns()
        {
            var ws = FromXml(Procs);
            var foo = Def(ws, "foo");
            Assert.IsTrue(FieldEditing.SetValue(ws, foo, foo.GetField("NAME"), "greet"));
            var call = ws.TopBlocks.First(b => b.Type == "procedures_callnoreturn");
            Assert.AreEqual("greet", ProcedureInfo.Name(call));
            Assert.AreEqual("greet", call.GetInput("TOPROW").Fields.Single(f => f.IsLabel).Value, "the label on the call block follows");
            Assert.AreEqual("in foo", Run(ws), "the call still reaches the (renamed) procedure");
        }

        [TestCase("bar", "bar2")] [TestCase("bar2", "bar2")] [TestCase("  spaced   out  ", "spaced out")] [TestCase("", "unnamed")] [TestCase("   ", "unnamed")]
        public void ProcedureNamesAreTidiedAndKeptUnique(string typed, string expected)
        {
            var ws = FromXml(Procs);
            var foo = Def(ws, "foo");
            FieldEditing.SetValue(ws, foo, foo.GetField("NAME"), typed);
            Assert.AreEqual(expected, foo.GetFieldValue("NAME"));
        }

        [Test]
        public void ANameClashBumpsTheTrailingNumber()
        {
            var ws = FromXml(Procs + "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"600\"><field name=\"NAME\">bar2</field></block>");
            var foo = Def(ws, "foo");
            FieldEditing.SetValue(ws, foo, foo.GetField("NAME"), "bar");
            Assert.AreEqual("bar3", foo.GetFieldValue("NAME"), "bar and bar2 are taken");
        }

        [Test]
        public void RenamingToTheSameNameIsNotAChange()
        {
            var ws = FromXml(Procs);
            var foo = Def(ws, "foo");
            Assert.IsFalse(FieldEditing.SetValue(ws, foo, foo.GetField("NAME"), "foo"));
            Assert.IsFalse(FieldEditing.SetValue(ws, foo, foo.GetField("NAME"), " foo "), "just whitespace around the same name");
        }
    }
}
