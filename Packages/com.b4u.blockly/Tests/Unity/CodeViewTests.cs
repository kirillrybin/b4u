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

        static Rect Box(Button b) { var rt = (RectTransform)b.transform; return new Rect(rt.anchoredPosition.x, 0, rt.sizeDelta.x, 1); }

        [TestCase(380f)]
        [TestCase(500f)]
        [TestCase(639f)]
        [TestCase(1150f)]
        public void TheHeaderButtonsNeverCoverEachOtherOrLeaveThePanel(float areaWidth)
        {
            c.SetCodeVisible(true);
            var code = c.CodePanel;
            code.Layout(areaWidth);
            var boxes = new[] { code.LanguageButton, code.CopyButton, code.SaveButton, code.CloseButton }.Select(Box).ToArray();
            for (int i = 0; i < boxes.Length; i++)
            {
                Assert.GreaterOrEqual(boxes[i].xMin, 0f, "button " + i);
                Assert.LessOrEqual(boxes[i].xMax, code.LaidOutWidth + 0.01f, "button " + i + " inside the panel of an area " + areaWidth);
                if (i > 0) Assert.Less(boxes[i - 1].xMax, boxes[i].xMin + 0.01f, "button " + i + " does not cover the one before");
            }
        }

        [Test]
        public void ANarrowAreaGivesTheWholeWidthToThePanelAndAWideOneHalf()
        {
            c.SetCodeVisible(true);
            var rt = (RectTransform)c.CodePanel.transform;
            c.CodePanel.Layout(400f);
            Assert.AreEqual(0f, rt.anchorMin.x);
            Assert.Less(rt.offsetMin.x, 0f, "it starts left of the workspace, over the toolbox");
            c.CodePanel.Layout(1150f);
            Assert.AreEqual(0.5f, rt.anchorMin.x);
        }

        [Test]
        public void LayingOutAgainForTheSameWidthDoesNothingAndAChangeOfSizeDoesNotLoop()
        {
            c.SetCodeVisible(true);
            var code = c.CodePanel;
            code.Layout(384.3f);                                           // a fractional width, as on a phone
            var rt = (RectTransform)code.transform;
            var before = new[] { rt.anchoredPosition, rt.sizeDelta, ((RectTransform)code.CopyButton.transform).anchoredPosition };
            code.Layout(384.3f);
            Assert.AreEqual(before[2], ((RectTransform)code.CopyButton.transform).anchoredPosition);
            rt.sizeDelta += new Vector2(0.1f, 0f);                         // raises OnRectTransformDimensionsChange: must come back
            Assert.IsTrue(code.Visible);
        }

        [Test]
        public void ALabelThatTurnsOutWiderMakesTheHeaderFitAgain()
        {
            c.SetCodeVisible(true);
            var code = c.CodePanel;
            code.Layout(400f);
            Assert.IsFalse(code.Recheck(), "nothing changed");
            var label = code.LanguageButton.GetComponentInChildren<TMP_Text>();
            float before = ((RectTransform)code.LanguageButton.transform).sizeDelta.x;
            label.text = "A language with a very long name";            // as if its glyphs came out wider once drawn
            Assert.IsTrue(code.Recheck());
            Assert.Greater(((RectTransform)code.LanguageButton.transform).sizeDelta.x, before);
            var boxes = new[] { code.LanguageButton, code.CopyButton, code.SaveButton, code.CloseButton }.Select(Box).ToArray();
            for (int i = 1; i < boxes.Length; i++) Assert.Less(boxes[i - 1].xMax, boxes[i].xMin + 0.01f);
        }

        [Test]
        public void ThePanelIsDrawnAboveTheToolboxAndBelowMenus()
        {
            c.SetCodeVisible(true);
            var canvas = c.CodePanel.GetComponent<Canvas>();
            // the setting only holds when it is made while the panel is active: OnEnable does it (EditMode never calls it)
            typeof(CodeView).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(c.CodePanel, null);
            Assert.IsTrue(canvas.overrideSorting);
            Assert.Greater(canvas.sortingOrder, 0);
            Assert.Less(canvas.sortingOrder, 200, "a dropdown menu is above it");
            Assert.IsFalse(canvas.isRootCanvas, "nested in the scene's canvas");
        }
    }
}
