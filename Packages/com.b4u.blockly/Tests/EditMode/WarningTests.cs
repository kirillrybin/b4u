using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>Warnings on blocks: what is reported, what is left alone, and the icon in the layout.</summary>
    public class WarningTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        static Workspace Xml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return ws;
        }

        const string Var = "<variables><variable id=\"v1\">x</variable></variables>";
        const string SetEmpty = "<block type=\"variables_set\" x=\"0\" y=\"0\"><field name=\"VAR\" id=\"v1\">x</field></block>";
        const string SetFull = "<block type=\"variables_set\" x=\"0\" y=\"0\"><field name=\"VAR\" id=\"v1\">x</field><value name=\"VALUE\"><block type=\"math_number\"><field name=\"NUM\">1</field></block></value></block>";

        // ---------------------------------------------------------------- the rules
        [Test]
        public void AnEmptyRequiredInputIsReportedAndFillingItClearsTheWarning()
        {
            var empty = Xml(Var + SetEmpty);
            Assert.IsTrue(BlockWarnings.Check(empty).ContainsKey(empty.TopBlocks[0]));
            var full = Xml(Var + SetFull);
            Assert.AreEqual(0, BlockWarnings.Check(full).Count);
        }

        [Test]
        public void AShadowBlockInAnInputCountsAsFilled()
        {
            var ws = Xml(Var + "<block type=\"variables_set\" x=\"0\" y=\"0\"><field name=\"VAR\" id=\"v1\">x</field><value name=\"VALUE\"><shadow type=\"math_number\"><field name=\"NUM\">0</field></shadow></value></block>");
            Assert.AreEqual(0, BlockWarnings.Check(ws).Count);
        }

        [Test]
        public void ADisabledBlockAndItsChildrenAreNotChecked()
        {
            var ws = Xml(Var + SetEmpty);
            ws.TopBlocks[0].Disabled = true;
            Assert.AreEqual(0, BlockWarnings.Check(ws).Count);
        }

        [Test]
        public void AValueBlockPluggedIntoNothingIsReported()
        {
            var ws = Xml("<block type=\"math_number\" x=\"0\" y=\"0\"><field name=\"NUM\">1</field></block>");
            StringAssert.Contains("connected", BlockWarnings.Check(ws)[ws.TopBlocks[0]]);
        }

        [Test]
        public void ACallOfAMissingProcedureIsReportedAndADefinitionFixesIt()
        {
            const string call = "<block type=\"procedures_callnoreturn\" x=\"0\" y=\"0\"><mutation name=\"go\"></mutation></block>";
            var ws = Xml(call);
            StringAssert.Contains("go", BlockWarnings.Check(ws)[ws.TopBlocks[0]]);
            var ok = Xml(call + "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"80\"><field name=\"NAME\">go</field></block>");
            Assert.IsFalse(BlockWarnings.Check(ok).ContainsKey(ok.TopBlocks.First(b => b.Type == "procedures_callnoreturn")));
        }

        [Test]
        public void AVariableThatDoesNotExistIsReported()
        {
            var ws = Xml("<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"variables_get\"><field name=\"VAR\" id=\"nope\">ghost</field></block></value></block>");
            ws.Variables.Clear();                         // loading a file creates the variables it uses: take it away again
            var get = ws.TopBlocks[0].GetInputTargetBlock("TEXT");
            Assert.IsTrue(BlockWarnings.Check(ws).ContainsKey(get));
        }

        [Test]
        public void TheListOfRequiredInputsCanBeExtended()
        {
            var ws = Xml("<block type=\"text_print\" x=\"0\" y=\"0\"></block>");
            Assert.AreEqual(0, BlockWarnings.Check(ws).Count);
            BlockWarnings.RequiredInputs["text_print"] = new[] { "TEXT" };
            try { Assert.AreEqual(1, BlockWarnings.Check(ws).Count); }
            finally { BlockWarnings.RequiredInputs.Remove("text_print"); }
        }

        // ---------------------------------------------------------------- the layout
        static BlockBox Box(Workspace ws, bool show, bool mutators = false)
        {
            var lay = new BlockLayouter(ws, new FixedWidth()) { ShowWarnings = show, ShowMutators = mutators, Warnings = BlockWarnings.Check(ws).Keys.ToList() };
            return lay.LayoutWorkspace().ByBlock[ws.TopBlocks[0]];
        }

        [Test]
        public void TheIconIsOnlyThereWhenTheFlagIsOnAndTheBlockHasAWarning()
        {
            Assert.IsNull(Box(Xml(Var + SetEmpty), false).WarningIcon);
            Assert.IsNotNull(Box(Xml(Var + SetEmpty), true).WarningIcon);
            Assert.IsNull(Box(Xml(Var + SetFull), true).WarningIcon);
        }

        [Test]
        public void TheIconMakesTheBlockWiderByItsSizeAndAGap()
        {
            float without = Box(Xml(Var + SetEmpty), false).W, with = Box(Xml(Var + SetEmpty), true).W;
            Assert.AreEqual(BlockLayouter.CommentIconSize + BlockGeometry.Current.ItemGap, with - without, 0.01f);
        }

        [Test]
        public void TheWarningComesBeforeTheGearAndTheComment()
        {
            var ws = Xml("<block type=\"controls_if\" x=\"0\" y=\"0\"></block>");
            ws.TopBlocks[0].Comment = "note";
            var lay = new BlockLayouter(ws, new FixedWidth()) { ShowWarnings = true, ShowMutators = true, Warnings = new[] { ws.TopBlocks[0] } };
            var box = lay.LayoutWorkspace().ByBlock[ws.TopBlocks[0]];
            Assert.Less(box.WarningIcon.X, box.MutatorIcon.X);
            Assert.Less(box.MutatorIcon.X, box.CommentIcon.X);
        }
    }
}
