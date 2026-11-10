using System.Collections.Generic;
using System.Linq;
using Blockly.Editing;
using NUnit.Framework;

namespace Blockly.Tests
{
    /// <summary>Finding the blocks of the toolbox by the words on them.</summary>
    public class ToolboxSearchTests
    {
        static readonly ToolboxDefinition Toolbox = TestSupport.NewToolbox();

        [TearDown] public void TearDown() => Loc.Language = Loc.English;

        static string[] Types(IEnumerable<Dictionary<string, object>> specs) => specs.Select(s => (string)s["type"]).ToArray();

        static string[] Find(string query, Workspace ws = null) => Types(ToolboxSearch.Find(Toolbox, ws ?? TestSupport.NewWorkspace(), query));

        [Test]
        public void AWordOnTheBlockFindsIt()
        {
            CollectionAssert.Contains(Find("print"), "text_print");
            CollectionAssert.Contains(Find("PRINT"), "text_print", "case does not matter");
            CollectionAssert.Contains(Find("  print  "), "text_print", "spaces around do not matter");
            CollectionAssert.Contains(Find("repeat"), "controls_repeat_ext");
            CollectionAssert.Contains(Find("repeat"), "controls_whileUntil");
        }

        [Test]
        public void EveryWordMustBeThere()
        {
            var both = Find("repeat while");
            CollectionAssert.Contains(both, "controls_whileUntil");
            CollectionAssert.DoesNotContain(both, "controls_repeat_ext");
            CollectionAssert.IsEmpty(Find("print zzzz"));
        }

        [Test]
        public void ABlockIsFoundByTheChoicesOfItsMenusAfterTheOnesWithTheWordOnThem()
        {
            var r = Find("square root");
            CollectionAssert.Contains(r, "math_single", "the menu of the math block lists it, though the block shows another operation");
            var byMenu = Find("until");
            CollectionAssert.Contains(byMenu, "controls_whileUntil");
            var withWord = Find("and");
            Assert.AreEqual(withWord.Distinct().Count(), withWord.Length, "a block is listed once");
        }

        [Test]
        public void TheCategoryNameAndTheTypeFindBlocksToo()
        {
            var colour = Find("colour");
            CollectionAssert.IsSupersetOf(colour, new[] { "colour_picker", "colour_random", "colour_rgb", "colour_blend" });
            CollectionAssert.Contains(Find("whileUntil"), "controls_whileUntil");
        }

        [Test]
        public void NothingOrNothingKnownFindsNothing()
        {
            CollectionAssert.IsEmpty(Find(""));
            CollectionAssert.IsEmpty(Find("   "));
            CollectionAssert.IsEmpty(Find(null));
            CollectionAssert.IsEmpty(Find("qwertyuiop"));
        }

        [Test]
        public void VariablesAndFunctionsOfTheProgramAreSearchedAsTheyAre()
        {
            var ws = TestSupport.NewWorkspace();
            ws.GetOrCreateVariable("score");
            var found = ToolboxSearch.Find(Toolbox, ws, "score");
            Assert.IsTrue(found.Any(s => (string)s["type"] == "variables_get"), "the get block of the variable");
            CollectionAssert.IsEmpty(Find("score"), "an empty program has no such variable");
        }

        [Test]
        public void TheLanguageInUseIsSearchedAndEnglishStillWorks()
        {
            Loc.Language = Loc.Pick("ru");
            if (Loc.Language != "ru") Assert.Ignore("Russian is not registered");
            CollectionAssert.Contains(Find(Loc.T("print")), "text_print");
            CollectionAssert.Contains(Find("print"), "text_print");
        }

        [Test]
        public void ThereAreAtMostSoManyResults()
        {
            Assert.LessOrEqual(ToolboxSearch.Find(Toolbox, TestSupport.NewWorkspace(), "a", 5).Count, 5);
            Assert.LessOrEqual(Find("a").Length, ToolboxSearch.MaxResults);
        }

        [Test]
        public void TheResultsAreTheSpecsOfTheToolboxSoTheyCanBeSpawned()
        {
            var spec = ToolboxSearch.Find(Toolbox, TestSupport.NewWorkspace(), "print").First(s => (string)s["type"] == "text_print");
            Assert.IsTrue(Toolbox.Categories.SelectMany(c => c.Blocks).Any(b => ReferenceEquals(b, spec)));
            var ws = TestSupport.NewWorkspace();
            Assert.DoesNotThrow(() => WorkspaceEditing.Spawn(ws, spec));
        }
    }
}
