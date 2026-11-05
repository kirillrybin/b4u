using System.IO;
using System.Linq;
using Blockly.Storage;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blockly.UI.Tests
{
    /// <summary>Saving, opening, deleting, copying and pasting programs through the Playground's File menu commands.</summary>
    public class PlaygroundFileTests
    {
        const string ScenePath = "Assets/Playground/Playground.unity";
        string clipboard;

        [SetUp]
        public void SetUp() => clipboard = GUIUtility.systemCopyBuffer;

        [TearDown]
        public void TearDown()
        {
            GUIUtility.systemCopyBuffer = clipboard;                       // the tests must not leave their text in the developer's clipboard
            foreach (var g in Object.FindObjectsByType<DialogView>(FindObjectsSortMode.None)) Object.DestroyImmediate(g.gameObject);
            foreach (var g in Object.FindObjectsByType<DropdownMenu>(FindObjectsSortMode.None)) Object.DestroyImmediate(g.gameObject);
        }

        PlaygroundController Open(IProgramStore store = null)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var c = Object.FindFirstObjectByType<PlaygroundController>();
            Assert.NotNull(c);
            c.Store = store ?? new MemoryProgramStore();                   // never the real disk
            return c;
        }

        static void RunToEnd(PlaygroundController c)
        {
            c.Run();
            for (int i = 0; i < 100000 && c.Runner.State == RunnerState.Running; i++) c.Tick(0.016f);
        }

        static string[] Output(PlaygroundController c) => c.Console.Lines.ToArray();

        static void EditSomething(PlaygroundController c)
        {
            var top = c.View.Workspace.TopBlocks[0];
            c.View.Select(top);
            Assert.IsTrue(c.View.DeleteSelected());
        }

        [Test]
        public void ASavedProgramOpensAgainAndRunsTheSame()
        {
            var c = Open();
            Assert.IsTrue(c.ShowByName("arithmetic"));
            RunToEnd(c);
            var expected = Output(c);
            Assert.Greater(expected.Length, 3);

            Assert.IsTrue(c.SaveTo("my arithmetic"));
            Assert.AreEqual("my arithmetic", c.SavedName);
            Assert.IsFalse(c.IsDirty);
            CollectionAssert.AreEqual(new[] { "my arithmetic" }, c.Store.List().ToArray());

            c.Show(3);
            Assert.IsNull(c.SavedName);
            Assert.IsTrue(c.OpenSaved("my arithmetic"));
            Assert.AreEqual("my arithmetic", c.SavedName);
            Assert.IsFalse(c.IsDirty);
            StringAssert.StartsWith("my arithmetic", c.Title);
            RunToEnd(c);
            CollectionAssert.AreEqual(expected, Output(c));
        }

        [Test]
        public void EditingMarksTheProgramUnsavedAndSavingClearsIt()
        {
            var c = Open();
            c.ShowByName("arithmetic");
            Assert.IsFalse(c.IsDirty);
            StringAssert.DoesNotContain("*", c.Title);
            EditSomething(c);
            Assert.IsTrue(c.IsDirty);
            StringAssert.EndsWith(" *", c.Title);
            Assert.IsTrue(c.SaveTo("x"));
            Assert.IsFalse(c.IsDirty);
            StringAssert.DoesNotContain("*", c.Title);
        }

        [Test]
        public void LeavingAChangedProgramAsksFirst()
        {
            var c = Open();
            c.Show(0);
            EditSomething(c);
            c.Next();
            Assert.IsNotNull(c.ActiveDialog, "asked before discarding the changes");
            Assert.AreEqual(0, c.Index);
            c.ActiveDialog.Cancel();
            Assert.AreEqual(0, c.Index);
            Assert.IsTrue(c.IsDirty, "cancelling keeps the changes");

            c.Next();
            c.ActiveDialog.Confirm();
            Assert.AreEqual(1, c.Index);
            Assert.IsFalse(c.IsDirty);
        }

        [Test]
        public void AnUnchangedProgramIsLeftWithoutAsking()
        {
            var c = Open();
            c.Show(0);
            c.Next();
            Assert.IsNull(c.ActiveDialog);
            Assert.AreEqual(1, c.Index);
        }

        [Test]
        public void SaveAsAsksForANameAndAsksBeforeReplacingAnotherProgram()
        {
            var c = Open();
            c.ShowByName("arithmetic");
            c.SaveAs();
            Assert.IsTrue(c.ActiveDialog.IsPrompt);
            Assert.IsFalse(c.ActiveDialog.Submit("   "), "a name is required");
            Assert.IsTrue(c.ActiveDialog.Submit("a/b"));
            Assert.AreEqual("a_b", c.SavedName, "made safe for a file name");

            c.Store.Save("other", "{}");
            c.SaveAs();
            Assert.IsTrue(c.ActiveDialog.Submit("other"));
            Assert.IsNotNull(c.ActiveDialog);
            Assert.IsFalse(c.ActiveDialog.IsPrompt, "asks whether to replace");
            Assert.AreEqual("{}", c.Store.Load("other"));
            c.ActiveDialog.Cancel();
            Assert.AreEqual("{}", c.Store.Load("other"), "cancelled: not replaced");
            Assert.AreEqual("a_b", c.SavedName);

            c.SaveAs();
            c.ActiveDialog.Submit("other");
            c.ActiveDialog.Confirm();
            Assert.AreNotEqual("{}", c.Store.Load("other"));
            Assert.AreEqual("other", c.SavedName);

            c.SaveAs();
            Assert.IsTrue(c.ActiveDialog.Submit("other"), "the program's own name needs no confirmation");
            Assert.IsNull(c.ActiveDialog);
        }

        [Test]
        public void SaveWithoutANameAsksAndWithOneJustSaves()
        {
            var c = Open();
            c.ShowByName("arithmetic");
            c.Save();
            Assert.IsNotNull(c.ActiveDialog);
            c.ActiveDialog.Submit("first");
            EditSomething(c);
            c.Save();
            Assert.IsNull(c.ActiveDialog);
            Assert.IsFalse(c.IsDirty);
        }

        [Test]
        public void DeletingAsksAndForgetsTheName()
        {
            var c = Open();
            c.ShowByName("arithmetic");
            c.SaveTo("gone");
            c.DeleteSaved("gone");
            Assert.IsNotNull(c.ActiveDialog);
            c.ActiveDialog.Cancel();
            Assert.IsTrue(c.Store.Exists("gone"));
            c.DeleteSaved("gone");
            c.ActiveDialog.Confirm();
            Assert.IsFalse(c.Store.Exists("gone"));
            Assert.IsNull(c.SavedName, "the program on screen is no longer a saved one");
            Assert.IsTrue(c.IsDirty);
        }

        [Test]
        public void ABrokenTextIsRefusedAndTheProgramStays()
        {
            var c = Open();
            c.ShowByName("arithmetic");
            var before = c.View.Workspace;
            Assert.IsFalse(c.LoadText("not a program", "x", null, false));
            Assert.IsFalse(c.LoadText("{\"blocks\":{\"blocks\":[{\"type\":\"no_such_block\"}]}}", "x", null, false));
            Assert.IsFalse(c.LoadText("   ", "x", null, false));
            Assert.AreSame(before, c.View.Workspace);
            StringAssert.StartsWith("Cannot load", c.Console.StatusText);
            Assert.IsFalse(c.OpenSaved("missing"));
        }

        [Test]
        public void PastingBlocklyJsonOrXmlLoadsItAsAnUnsavedProgram()
        {
            var c = Open();
            c.ShowByName("arithmetic");
            GUIUtility.systemCopyBuffer = "<xml xmlns=\"https://developers.google.com/blockly/xml\"><block type=\"text_print\" x=\"10\" y=\"10\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">from xml</field></block></value></block></xml>";
            c.PasteProgram();
            Assert.IsTrue(c.IsDirty);
            Assert.IsNull(c.SavedName);
            StringAssert.StartsWith("pasted program", c.Title);
            RunToEnd(c);
            CollectionAssert.AreEqual(new[] { "from xml" }, Output(c));
        }

        [Test]
        public void CopyAsJsonPutsTheProgramOnTheClipboardAndItLoadsBack()
        {
            var c = Open();
            c.ShowByName("arithmetic");
            RunToEnd(c);
            var expected = Output(c);
            c.CopyJson();
            StringAssert.StartsWith("{", GUIUtility.systemCopyBuffer);
            c.Show(2);
            c.PasteProgram();
            RunToEnd(c);
            CollectionAssert.AreEqual(expected, Output(c));
        }

        [Test]
        public void PastingAnEmptyClipboardChangesNothing()
        {
            var c = Open();
            c.ShowByName("arithmetic");
            var before = c.View.Workspace;
            GUIUtility.systemCopyBuffer = "";
            c.PasteProgram();
            Assert.AreSame(before, c.View.Workspace);
            StringAssert.Contains("empty", c.Console.StatusText);
        }

        [Test]
        public void NewStartsAnEmptyProgram()
        {
            var c = Open();
            c.ShowByName("arithmetic");
            c.SaveTo("keep");
            c.NewProgram();
            Assert.AreEqual(0, c.View.Workspace.TopBlocks.Count);
            Assert.IsNull(c.SavedName);
            Assert.IsFalse(c.IsDirty);
            RunToEnd(c);
            Assert.AreEqual(0, Output(c).Length);
        }

        [Test]
        public void TheFileButtonIsInTheSceneAndOpensTheMenu()
        {
            var c = Open();
            var b = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(x => x.name == "File");
            Assert.AreEqual(1, b.onClick.GetPersistentEventCount());
            Assert.AreSame(c, b.onClick.GetPersistentTarget(0));
            Assert.AreEqual("ShowFileMenu", b.onClick.GetPersistentMethodName(0));
            c.ShowFileMenu();
            Assert.IsNotNull(c.ActiveMenu, "the button's menu opens under it");
        }

        [Test]
        public void TheFileMenuListsTheCommandsAndTheSavedPrograms()
        {
            var c = Open();
            c.Store.Save("alpha", "{}");
            c.ShowFileMenuAt(new Vector2(100, 100));
            Assert.IsNotNull(c.ActiveMenu);
            c.OnFileCommand("open", new Vector2(100, 100));
            Assert.IsNotNull(c.ActiveMenu, "the list of saved programs");
        }

        [Test]
        public void AFailingDiskIsReportedNotThrown()
        {
            var c = Open(new ThrowingStore());
            c.ShowByName("arithmetic");
            Assert.IsFalse(c.SaveTo("x"));
            StringAssert.StartsWith("Cannot save", c.Console.StatusText);
            Assert.IsNull(c.SavedName);
            Assert.IsFalse(c.OpenSaved("x"));
            StringAssert.StartsWith("Cannot open", c.Console.StatusText);
        }

        sealed class ThrowingStore : IProgramStore
        {
            public System.Collections.Generic.IReadOnlyList<string> List() => throw new IOException("disk gone");
            public bool Exists(string name) => false;
            public string Load(string name) => throw new IOException("disk gone");
            public string Save(string name, string text) => throw new IOException("disk gone");
            public bool Delete(string name) => throw new IOException("disk gone");
        }
    }
}
