using System.Collections.Generic;
using System.Linq;
using Blockly.Editing;
using NUnit.Framework;

namespace Blockly.UI.Tests
{
    /// <summary>Custom blocks added through <see cref="StandardBlockSet.AddPackage"/> (the same shape as Samples~/CustomBlocks).</summary>
    public class BlockPackageTests
    {
        sealed class Pkg : IBlockPackage
        {
            public string BlocksJson => @"[
              {""type"":""t_seven"",""message0"":""seven"",""output"":""Number"",""colour"":20},
              {""type"":""t_shout"",""message0"":""shout %1"",""args0"":[{""type"":""input_value"",""name"":""TEXT""}],""previousStatement"":null,""nextStatement"":null,""colour"":20}]";
            public void RegisterHandlers(HandlerRegistry r)
            {
                r.Expression("t_seven", (it, b) => 7.0);
                r.Simple("t_shout", (it, b) => it.Print(JsValue.ToStr(it.EvalInput(b, "TEXT", "")).ToUpperInvariant() + "!"));
            }
            public IEnumerable<ToolboxCategory> ToolboxCategories => new[]
            {
                new ToolboxCategory { Name = "Test pkg", Blocks = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "kind", "block" }, { "type", "t_seven" } } } },
            };
        }

        Pkg pkg;

        [SetUp] public void SetUp() { pkg = new Pkg(); StandardBlockSet.AddPackage(pkg); }
        [TearDown] public void TearDown() => StandardBlockSet.RemovePackage(pkg);

        const string Program = @"{""blocks"":{""languageVersion"":0,""blocks"":[{""type"":""t_shout"",""x"":0,""y"":0,
            ""inputs"":{""TEXT"":{""block"":{""type"":""text_join"",""extraState"":{""itemCount"":2},""inputs"":{
              ""ADD0"":{""block"":{""type"":""text"",""fields"":{""TEXT"":""n=""}}},""ADD1"":{""block"":{""type"":""t_seven""}}}}}}}]}}";

        [Test]
        public void ACustomBlockLoadsRunsAndRoundTrips()
        {
            var ws = StandardBlockSet.Load(Program);
            Assert.AreEqual(4, ws.AllBlocks().Count());
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion(10000);
            Assert.IsNull(it.Error);
            Assert.AreEqual("N=7!", string.Join(",", lines));

            var again = StandardBlockSet.Load(WorkspaceJson.Save(ws));
            Assert.AreEqual(WorkspaceJson.Save(ws), WorkspaceJson.Save(again));
        }

        [Test]
        public void TheToolboxGetsTheCategory()
        {
            var tb = StandardBlockSet.LoadToolbox();
            Assert.AreEqual("Test pkg", tb.Categories.Last().Name);
            Assert.IsTrue(tb.Categories.Count > 1);
        }

        [Test]
        public void ARemovedPackageStopsContributingDefinitionsAndCategories()
        {
            Assert.IsTrue(StandardBlockSet.RemovePackage(pkg));
            Assert.IsFalse(StandardBlockSet.CreateRegistry().TryGet("t_seven", out _));
            Assert.AreNotEqual("Test pkg", StandardBlockSet.LoadToolbox().Categories.Last().Name);
            StandardBlockSet.AddPackage(pkg);
        }

        [Test]
        public void AddingTheSamePackageTwiceDoesNotDuplicateTheCategory()
        {
            StandardBlockSet.AddPackage(pkg);
            Assert.AreEqual(1, StandardBlockSet.LoadToolbox().Categories.Count(c => c.Name == "Test pkg"));
        }
    }
}
