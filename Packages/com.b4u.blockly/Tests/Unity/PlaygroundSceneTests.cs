using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>Opens the saved Playground scene and drives it the way its buttons and frames do.</summary>
    public class PlaygroundSceneTests
    {
        const string ScenePath = "Assets/Playground/Playground.unity";

        PlaygroundController Open()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var c = Object.FindFirstObjectByType<PlaygroundController>();
            Assert.NotNull(c, "scene has a PlaygroundController");
            return c;
        }

        static void ShowByName(PlaygroundController c, string name) => Assert.IsTrue(c.ShowByName(name), "program not found: " + name);

        static void RunToEnd(PlaygroundController c)
        {
            for (int i = 0; i < 100000 && c.Runner.State == RunnerState.Running; i++) c.Tick(0.016f);
        }

        [Test]
        public void EveryProgramLoadsAndDrawsBlocks()
        {
            var c = Open();
            Assert.Greater(c.Count, 10);
            for (int i = 0; i < c.Count; i++)
            {
                c.Show(i);
                Assert.Greater(c.View.Layout.Boxes.Count, 0, "program #" + i);
                Assert.Greater(c.View.GetComponentsInChildren<PolygonGraphic>(true).Length, 0, "program #" + i);
                var rt = (RectTransform)c.View.transform;
                Assert.Greater(rt.sizeDelta.x, 40f); Assert.Greater(rt.sizeDelta.y, 40f);
            }
        }

        [Test]
        public void NextAndPreviousWrapAround()
        {
            var c = Open();
            c.Show(0);
            c.Previous();
            Assert.AreEqual(c.Count - 1, c.Index);
            c.Next();
            Assert.AreEqual(0, c.Index);
        }

        [Test]
        public void ToolbarButtonsAreWiredToTheController()
        {
            // Edit Mode does not run RuntimeOnly persistent listeners, so check what the scene actually stores:
            // each button must call the matching method on the PlaygroundController.
            var c = Open();
            var expected = new[] { ("Next", "Next"), ("Prev", "Previous"), ("Run", "Run"), ("Step", "Step"), ("Stop", "Stop"), ("Slow", "ToggleSlow") };
            foreach (var (button, method) in expected)
            {
                var b = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x => x.name == button);
                Assert.AreEqual(1, b.onClick.GetPersistentEventCount(), button);
                Assert.AreSame(c, b.onClick.GetPersistentTarget(0), button);
                Assert.AreEqual(method, b.onClick.GetPersistentMethodName(0), button);
            }
        }

        [Test]
        public void SceneHasAnEventSystemAndAScrollableViewport()
        {
            Open();
            Assert.NotNull(Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>());
            var scroll = Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None).Single(s => s.content.GetComponent<WorkspaceView>() != null);
            Assert.NotNull(scroll.viewport.GetComponent<RectMask2D>());
        }

        // ---- running ----
        [Test]
        public void RunPrintsToTheConsoleExactlyWhatBlocklyPrints()
        {
            var c = Open();
            ShowByName(c, "loops");
            c.Run();
            RunToEnd(c);
            string expected = System.IO.File.ReadAllText("Packages/com.b4u.blockly/Tests/EditMode/Fixtures/programs/loops.expected.txt").TrimEnd('\n');
            Assert.AreEqual(expected, string.Join("\n", c.Console.Lines));
            Assert.AreEqual("Finished", c.Console.StatusText);
            Assert.IsNull(c.View.HighlightedBlock, "highlight is gone once the program ends");
        }

        [Test]
        public void StepOutlinesTheBlockAboutToRunAndPrintsOneLineAtATime()
        {
            var c = Open();
            ShowByName(c, "variables");
            c.Step();
            Assert.NotNull(c.View.HighlightedBlock);
            Assert.AreSame(c.Runner.CurrentBlock, c.View.HighlightedBlock);
            Assert.AreEqual("Paused", c.Console.StatusText);
            int steps = 0;
            var seenLines = new System.Collections.Generic.List<int>();
            while (c.Runner.State == RunnerState.Paused && steps++ < 200)
            {
                seenLines.Add(c.Console.Lines.Count);
                Assert.AreSame(c.Runner.CurrentBlock, c.View.HighlightedBlock);
                c.Step();
            }
            Assert.AreEqual(RunnerState.Finished, c.Runner.State);
            Assert.AreEqual(6, c.Console.Lines.Count);
            Assert.IsTrue(seenLines.Zip(seenLines.Skip(1), (a, b) => b >= a).All(x => x), "output only grows");
            Assert.Greater(new System.Collections.Generic.HashSet<int>(seenLines).Count, 3, "lines appear gradually, not all at once");
        }

        [Test]
        public void StopClearsHighlightAndReportsStopped()
        {
            var c = Open();
            ShowByName(c, "loops");
            c.Step(); c.Step();
            Assert.NotNull(c.View.HighlightedBlock);
            c.Stop();
            Assert.IsNull(c.View.HighlightedBlock);
            Assert.AreEqual("Stopped", c.Console.StatusText);
            Assert.AreEqual(RunnerState.Idle, c.Runner.State);
        }

        [Test]
        public void SlowToggleChangesTheRunnerAndTheButtonLabel()
        {
            var c = Open();
            c.Show(0);
            Assert.IsFalse(c.Runner.Slow);
            c.ToggleSlow();
            Assert.IsTrue(c.Slow); Assert.IsTrue(c.Runner.Slow);
            var label = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Slow").GetComponentInChildren<TMP_Text>();
            Assert.AreEqual("Slow: on", label.text);
            c.Show(1);
            Assert.IsTrue(c.Runner.Slow, "the setting survives switching programs");
            c.ToggleSlow();
            Assert.AreEqual("Slow: off", label.text);
        }

        [Test]
        public void SwitchingProgramsStopsTheRunningOneAndClearsTheConsole()
        {
            var c = Open();
            ShowByName(c, "loops");
            c.Run();
            c.Tick(0.016f);
            c.Next();
            Assert.AreEqual(0, c.Console.Lines.Count);
            Assert.AreEqual("Ready", c.Console.StatusText);
            Assert.IsNull(c.View.HighlightedBlock);
        }

        [Test]
        public void ConsoleShowsPrintedMarkupLiterally()
        {
            var c = Open();
            c.Console.Append("<b>bold</b> <color=red>x");
            var text = c.Console.GetComponentsInChildren<TMP_Text>(true).First(t => t.gameObject.name == "Text");
            Assert.IsFalse(text.richText);
            StringAssert.Contains("<b>bold</b>", text.text);
        }
    }
}
