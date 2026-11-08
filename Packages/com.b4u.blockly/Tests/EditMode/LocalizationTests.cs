using System.Collections.Generic;
using System.Linq;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>The English text is the key; Russian ships with the package and other languages can be added.</summary>
    public class LocalizationTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        [TearDown] public void TearDown() => Loc.Language = Loc.English;

        static IEnumerable<string> Texts(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            var layout = new BlockLayouter(ws, new FixedWidth()).LayoutWorkspace();
            return layout.Boxes.SelectMany(b => b.Fields.Select(f => f.Text)).ToList();
        }

        [Test]
        public void EnglishIsTheDefaultAndAMissingTranslationShowsTheEnglishText()
        {
            Assert.AreEqual(Loc.English, Loc.Language);
            Assert.AreEqual("Run", Loc.T("Run"));
            Loc.Language = "ru";
            Assert.AreEqual("Запуск", Loc.T("Run"));
            Assert.AreEqual("A text nobody translated", Loc.T("A text nobody translated"));
            Assert.AreEqual("Сохранено «x»", Loc.T("Saved \"{0}\"", "x"));
        }

        [Test]
        public void ChangingTheLanguageRaisesChangedOnceAndAnUnknownCodeIsIgnored()
        {
            int changes = 0; System.Action count = () => changes++;
            Loc.Changed += count;
            Loc.Language = "ru"; Loc.Language = "ru"; Loc.Language = "xx"; Loc.Language = null;
            Loc.Changed -= count;
            Assert.AreEqual(1, changes);
            Assert.AreEqual("ru", Loc.Language);
            CollectionAssert.AreEqual(new[] { "en", "ru" }, Loc.Languages.Take(2));
            Assert.AreEqual("Русский", Loc.NameOf("ru"));
        }

        [Test]
        public void AnotherLanguageCanBeAddedWithATableOfTexts()
        {
            Loc.Register("de", "Deutsch", new Dictionary<string, string> { { "Run", "Ausführen" } });
            Loc.Language = "de";
            Assert.AreEqual("Ausführen", Loc.T("Run"));
            Assert.AreEqual("Step", Loc.T("Step"), "not translated: English");
            Assert.AreEqual("de", Loc.Pick("fr", "de"));
            Assert.AreEqual("en", Loc.Pick("fr"));
        }

        [Test]
        public void BlockLabelsAndDropdownTextsFollowTheLanguageButUserTextDoesNot()
        {
            const string body = "<block type=\"text_print\" x=\"0\" y=\"0\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">print</field></block></value></block>" +
                                "<block type=\"logic_boolean\" x=\"0\" y=\"60\"><field name=\"BOOL\">TRUE</field></block>";
            var english = Texts(body).ToList();
            CollectionAssert.Contains(english, "print");
            CollectionAssert.Contains(english, "true");
            Loc.Language = "ru";
            var russian = Texts(body).ToList();
            CollectionAssert.Contains(russian, "напечатать", "the label of the block");
            CollectionAssert.Contains(russian, "истина", "the shown text of a dropdown");
            Assert.AreEqual(1, russian.Count(t => t == "print"), "what the user typed into the text block stays");
        }

        [Test]
        public void ProcedureNamesAndParametersAreShownAsTheyAre()
        {
            const string body = "<block type=\"procedures_defnoreturn\" x=\"0\" y=\"0\"><mutation><arg name=\"to\"></arg></mutation><field name=\"NAME\">print</field></block>";
            Loc.Language = "ru";
            var texts = Texts(body).ToList();
            CollectionAssert.Contains(texts, "print", "the procedure is called print, not translated");
            Assert.IsTrue(texts.Any(t => t.StartsWith("с:") && t.EndsWith("to")), "'with:' is translated, the parameter name is not: " + string.Join("|", texts));
        }

        [Test]
        public void EveryRussianTextIsUsedAsAKeyThatEnglishCodeCanProduce()
        {
            Loc.Language = "ru";
            foreach (var key in new[] { "Ready", "Finished", "Cancel", "OK", "Logic", "Loops", "Math", "Text", "Lists", "Variables", "Functions" })
                Assert.AreNotEqual(key, Loc.T(key), key + " has no Russian text");
        }
    }
}
