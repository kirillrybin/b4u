using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>The Code panel of the Playground: the program as JavaScript, kept up to date.</summary>
    public class CodeViewTests
    {
        PlaygroundController c;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(CodeView.PrefsKey);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            c = Object.FindFirstObjectByType<PlaygroundController>();
            c.ShowByName("if_else");
        }

        [TearDown] public void TearDown() { BlockStyle.Theme = null; Loc.Language = Loc.English; PlayerPrefs.DeleteKey(CodeView.PrefsKey); }

        [Test]
        public void TheCodeButtonIsInTheToolbarAndShowsAndHidesThePanel()
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x => x.name == "CodeToggle");
            Assert.AreEqual("ToggleCode", button.onClick.GetPersistentMethodName(0));
            Assert.AreEqual("Code", button.GetComponentInChildren<TMP_Text>().text);
            Assert.IsFalse(c.CodeVisible);
            c.ToggleCode();
            Assert.IsTrue(c.CodeVisible);
            c.ToggleCode();
            Assert.IsFalse(c.CodeVisible);
        }

        [Test]
        public void ThePanelShowsTheProgramAsJavaScript()
        {
            c.SetCodeVisible(true);
            var code = c.CodePanel;
            Assert.AreEqual("JavaScript", code.LanguageName);
            StringAssert.StartsWith("var x;", code.Code);
            StringAssert.Contains("console.log('done');", code.ShownText);
            Assert.AreEqual("", code.Error);
        }

        [Test]
        public void ThePythonLanguageCanBePickedAndIsRemembered()
        {
            c.SetCodeVisible(true);
            var code = c.CodePanel;
            Assert.IsTrue(code.PickLanguage("Python"));
            Assert.AreEqual("Python", code.LanguageName);
            StringAssert.StartsWith("x = None", code.Code);
            StringAssert.Contains("print('done')", code.ShownText);
            Assert.AreEqual("Python", code.LanguageButton.GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Python", PlayerPrefs.GetString(CodeView.PrefsKey));
            Assert.IsFalse(code.PickLanguage("Cobol"));

            Object.DestroyImmediate(code.gameObject);                       // a new session builds the panel again
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            c = Object.FindFirstObjectByType<PlaygroundController>();
            c.ShowByName("if_else");
            c.SetCodeVisible(true);
            Assert.AreEqual("Python", c.CodePanel.LanguageName);
        }

        [Test]
        public void ThePanelFollowsEditsAndProgramChanges()
        {
            c.SetCodeVisible(true);
            c.ShowByName("text");
            StringAssert.Contains("toUpperCase()", c.CodePanel.Code);
            var ws = c.View.Workspace;
            var text = ws.AllBlocks().First(b => b.Type == "text");
            text.SetFieldValue("TEXT", "changed");
            c.View.NotifyEdited();
            StringAssert.Contains("'changed'", c.CodePanel.Code);
        }

        [Test]
        public void AHiddenPanelDoesNotExportUntilItIsShown()
        {
            c.SetCodeVisible(true);
            c.SetCodeVisible(false);
            c.ShowByName("loops");
            Assert.IsFalse(c.CodeVisible);
            c.SetCodeVisible(true);
            StringAssert.Contains("for (var count", c.CodePanel.Code);
        }

        [Test]
        public void CopyPutsTheCodeOnTheClipboardAndCloseHidesThePanel()
        {
            c.SetCodeVisible(true);
            GUIUtility.systemCopyBuffer = "";
            c.CodePanel.CopyButton.onClick.Invoke();
            Assert.AreEqual(c.CodePanel.Code, GUIUtility.systemCopyBuffer);
            c.CodePanel.CloseButton.onClick.Invoke();
            Assert.IsFalse(c.CodeVisible);
        }

        [Test]
        public void SaveWritesTheCodeToAFileOfTheLanguageAndTellsWhere()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "blockly-code-" + System.Guid.NewGuid().ToString("N"));
            c.Exchange = new Blockly.Storage.FileExchange(dir);
            try
            {
                c.SetCodeVisible(true);
                c.CodePanel.SaveButton.onClick.Invoke();
                string js = System.IO.Path.Combine(dir, "program.js");
                Assert.IsTrue(System.IO.File.Exists(js));
                Assert.AreEqual(c.CodePanel.Code, System.IO.File.ReadAllText(js));
                StringAssert.Contains("program.js", c.Console.StatusText);
                c.CodePanel.PickLanguage("Python");
                c.CodePanel.SaveButton.onClick.Invoke();
                Assert.AreEqual(c.CodePanel.Code, System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "program.py")));
                CollectionAssert.IsEmpty(c.Exchange.List(), "code files are not offered as programs to import");
            }
            finally { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
        }

        [Test]
        public void ABlockWithoutAGeneratorShowsAMessageInsteadOfCode()
        {
            var reg = new BlockRegistry();
            reg.RegisterJson("[{\"type\":\"my_say\",\"message0\":\"say\",\"previousStatement\":null,\"nextStatement\":null,\"colour\":160}]");
            var ws = new Workspace(reg);
            ws.TopBlocks.Add(ws.CreateBlock("my_say"));
            c.SetCodeVisible(true);
            c.CodePanel.Show(ws);
            StringAssert.Contains("my_say", c.CodePanel.Error);
            Assert.AreEqual("", c.CodePanel.Code);
            Assert.AreEqual(c.CodePanel.Error, c.CodePanel.ShownText);
        }

        [Test]
        public void ThePanelWearsTheThemeAndSpeaksTheLanguage()
        {
            c.SetCodeVisible(true);
            var back = c.CodePanel.GetComponent<Image>();
            Assert.AreEqual(BlockStyle.ConsoleBackground, back.color);
            BuiltInThemes.Register();
            BlockThemeCatalog.Select("High Contrast");
            Assert.AreEqual(BlockStyle.ConsoleBackground, back.color);
            Assert.AreEqual(Color.black, back.color);
            BlockThemeCatalog.Select("Classic");
            Loc.Language = Loc.Pick("ru");
            Assert.AreEqual("Закрыть", c.CodePanel.CloseButton.GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Сохранить", c.CodePanel.SaveButton.GetComponentInChildren<TMP_Text>().text);
        }
    }
}
