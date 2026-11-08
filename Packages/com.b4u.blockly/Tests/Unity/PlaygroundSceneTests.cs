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
        public void SceneHasNoMissingScripts()
        {
            Open();
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(all.Length, 20);
            foreach (var t in all)
                Assert.AreEqual(0, UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject),
                    "missing script on '" + t.name + "' (a MonoBehaviour defined in a file with a different name?)");
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
        public void TheConsoleButtonHidesAndShowsTheConsoleAndTheWorkspaceTakesItsRoom()
        {
            var c = Open();
            c.Show(0);
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "ConsoleToggle");
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
            Assert.AreSame(c, button.onClick.GetPersistentTarget(0));
            Assert.AreEqual("ToggleConsole", button.onClick.GetPersistentMethodName(0));
            var label = button.GetComponentInChildren<TMP_Text>();
            var layout = Object.FindFirstObjectByType<PlaygroundLayout>();
            var scroll = (RectTransform)GameObject.Find("Scroll").transform;
            layout.Apply(1280, 720);
            Assert.IsTrue(c.ConsoleVisible);
            Assert.AreEqual("Console: on", label.text);
            Assert.AreEqual(190f, scroll.offsetMin.y);

            c.Run(); RunToEnd(c);
            int lines = c.Console.Lines.Count;
            c.ToggleConsole();
            Assert.IsFalse(c.ConsoleVisible);
            Assert.IsFalse(c.Console.gameObject.activeSelf);
            Assert.AreEqual("Console: off", label.text);
            layout.Apply(1280, 720);
            Assert.AreEqual(0f, layout.EffectiveConsoleHeight);
            Assert.AreEqual(0f, scroll.offsetMin.y, "the workspace reaches the bottom");

            c.Console.Append("while hidden");
            c.ToggleConsole();
            Assert.IsTrue(c.ConsoleVisible);
            Assert.AreEqual(lines + 1, c.Console.Lines.Count, "nothing was lost while it was hidden");
            layout.Apply(1280, 720);
            Assert.AreEqual(190f, scroll.offsetMin.y);
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
        public void StartingADragStopsTheRunningProgramAndEditingMakesTheNextRunUseTheChange()
        {
            var c = Open();
            ShowByName(c, "variables");
            c.Step();
            Assert.AreEqual(RunnerState.Paused, c.Runner.State);

            var first = c.View.Workspace.TopBlocks[0];
            var box = c.View.Layout.ByBlock[first];
            Assert.IsTrue(c.View.BeginDrag(first, new Vector2(box.X + 4, box.Y + 4)));
            Assert.AreEqual(RunnerState.Idle, c.Runner.State, "a running/paused program stops when the user starts editing it");
            Assert.AreEqual("Stopped", c.Console.StatusText);
            c.View.CancelDrag();

            // the program is still runnable and unchanged
            c.Run();
            RunToEnd(c);
            Assert.AreEqual(6, c.Console.Lines.Count);
        }

        [Test]
        public void EditingFieldsThenRunningPrintsTheNewValues()
        {
            var c = Open();
            ShowByName(c, "loops");
            var ws = c.View.Workspace;
            var text = ws.AllBlocks().First(b => b.Type == "text" && b.GetFieldValue("TEXT") == "rep");
            var times = ws.AllBlocks().First(b => b.Type == "controls_repeat_ext").GetInputTargetBlock("TIMES");   // the number block feeding the loop

            c.Step();                                                              // paused somewhere in the program
            Assert.AreEqual(RunnerState.Paused, c.Runner.State);

            Assert.IsTrue(c.View.BeginFieldEdit(text, text.GetField("TEXT")));
            Assert.AreEqual(RunnerState.Idle, c.Runner.State, "starting an edit stops the program");
            Assert.AreEqual("Stopped", c.Console.StatusText);
            Assert.IsTrue(c.View.SubmitFieldEdit("ping"));
            Assert.IsTrue(c.View.BeginFieldEdit(times, times.GetField("NUM")));
            Assert.IsTrue(c.View.SubmitFieldEdit("2"));

            c.Run();
            RunToEnd(c);
            var original = System.IO.File.ReadAllText("Packages/com.b4u.blockly/Tests/EditMode/Fixtures/programs/loops.expected.txt").TrimEnd('\n').Split('\n');
            // the first block printed "rep" three times; now it prints "ping" twice, everything after is unchanged
            var expected = new[] { "ping", "ping" }.Concat(original.Skip(3)).ToArray();
            CollectionAssert.AreEqual(expected, c.Console.Lines.ToArray());
            Assert.AreEqual("Finished", c.Console.StatusText);
        }

        [Test]
        public void ChoosingAnOperatorInTheMenuThenRunningPrintsTheNewResult()
        {
            var c = Open();
            ShowByName(c, "logic");
            var compare = c.View.Workspace.AllBlocks().First(b => b.Type == "logic_compare");      // 1 = 1
            Assert.IsTrue(c.View.ShowDropdown(compare, compare.GetField("OP")));
            int neq = c.View.ActiveMenu.Items.ToList().FindIndex(i => i.Value == "NEQ");
            c.View.ActiveMenu.Pick(neq);

            c.Run();
            RunToEnd(c);
            var original = System.IO.File.ReadAllText("Packages/com.b4u.blockly/Tests/EditMode/Fixtures/programs/logic.expected.txt").TrimEnd('\n').Split('\n');
            Assert.AreEqual("true", original[0], "the original first line is 1 = 1");
            var expected = new[] { "false" }.Concat(original.Skip(1)).ToArray();
            CollectionAssert.AreEqual(expected, c.Console.Lines.ToArray(), "only the first result changed");
        }

        [Test]
        public void RenamingAVariableThroughTheMenuLeavesTheProgramWorking()
        {
            var c = Open();
            ShowByName(c, "variables");
            var set = c.View.Workspace.AllBlocks().First(b => b.Type == "variables_set");
            c.View.ShowDropdown(set, set.GetField("VAR"));
            int rename = c.View.ActiveMenu.Items.ToList().FindIndex(i => i.Action == Blockly.Editing.DropdownAction.RenameVariable);
            c.View.ActiveMenu.Pick(rename);
            Assert.IsTrue(c.View.ActiveDialog.Submit("renamed_variable"));

            c.Run();
            RunToEnd(c);
            var expected = System.IO.File.ReadAllText("Packages/com.b4u.blockly/Tests/EditMode/Fixtures/programs/variables.expected.txt").TrimEnd('\n').Split('\n');
            CollectionAssert.AreEqual(expected, c.Console.Lines.ToArray());
            Assert.NotNull(c.View.Workspace.GetVariableByName("renamed_variable"));
        }

        [Test]
        public void UndoAndRedoButtonsAreWiredAndFollowTheHistory()
        {
            var c = Open();
            foreach (var (button, method) in new[] { ("Undo", "Undo"), ("Redo", "Redo") })
            {
                var b = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x => x.name == button);
                Assert.AreEqual(1, b.onClick.GetPersistentEventCount(), button);
                Assert.AreSame(c, b.onClick.GetPersistentTarget(0), button);
                Assert.AreEqual(method, b.onClick.GetPersistentMethodName(0), button);
            }

            ShowByName(c, "loops");
            Assert.IsFalse(c.UndoButton.interactable, "nothing to undo in a freshly loaded program");
            Assert.IsFalse(c.RedoButton.interactable);

            var text = c.View.Workspace.AllBlocks().First(b => b.Type == "text" && b.GetFieldValue("TEXT") == "rep");
            c.View.BeginFieldEdit(text, text.GetField("TEXT")); c.View.SubmitFieldEdit("ping");
            Assert.IsTrue(c.UndoButton.interactable); Assert.IsFalse(c.RedoButton.interactable);

            c.Undo();
            Assert.IsFalse(c.UndoButton.interactable); Assert.IsTrue(c.RedoButton.interactable);
            c.Redo();
            Assert.IsTrue(c.UndoButton.interactable); Assert.IsFalse(c.RedoButton.interactable);

            c.Next();
            Assert.IsNotNull(c.ActiveDialog, "the program was changed: leaving it asks first");
            c.ActiveDialog.Confirm();
            Assert.IsFalse(c.UndoButton.interactable, "another program starts with a clean history");
        }

        [Test]
        public void EditRunUndoRunPrintsTheOriginalOutputAgain()
        {
            var c = Open();
            ShowByName(c, "loops");
            var expected = System.IO.File.ReadAllText("Packages/com.b4u.blockly/Tests/EditMode/Fixtures/programs/loops.expected.txt").TrimEnd('\n').Split('\n');

            var text = c.View.Workspace.AllBlocks().First(b => b.Type == "text" && b.GetFieldValue("TEXT") == "rep");
            c.View.BeginFieldEdit(text, text.GetField("TEXT")); c.View.SubmitFieldEdit("ping");
            c.Run(); RunToEnd(c);
            Assert.AreEqual("ping", c.Console.Lines[0], "the edit took effect");

            c.Undo();
            c.Run(); RunToEnd(c);
            CollectionAssert.AreEqual(expected, c.Console.Lines.ToArray(), "undone: exactly what real Blockly prints for the original program");
            Assert.AreEqual("Finished", c.Console.StatusText);
        }

        [Test]
        public void UndoStopsARunningProgram()
        {
            var c = Open();
            ShowByName(c, "loops");
            var text = c.View.Workspace.AllBlocks().First(b => b.Type == "text" && b.GetFieldValue("TEXT") == "rep");
            c.View.BeginFieldEdit(text, text.GetField("TEXT")); c.View.SubmitFieldEdit("ping");
            c.Step();
            Assert.AreEqual(RunnerState.Paused, c.Runner.State);
            c.Undo();
            Assert.AreEqual(RunnerState.Idle, c.Runner.State);
            Assert.AreEqual("Stopped", c.Console.StatusText);
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
