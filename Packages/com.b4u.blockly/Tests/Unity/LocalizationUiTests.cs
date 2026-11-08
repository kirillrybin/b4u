using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>Switching the language in the Playground: toolbar, File menu, category names and block labels.</summary>
    public class LocalizationUiTests
    {
        [TearDown] public void TearDown() { Loc.Language = Loc.English; PlayerPrefs.DeleteKey(PlaygroundController.LanguagePrefsKey); }

        static PlaygroundController OpenPlayground()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var c = Object.FindFirstObjectByType<PlaygroundController>();
            // EditMode never calls OnEnable
            typeof(PlaygroundController).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(c, null);
            c.ShowByName("text");
            return c;
        }

        static string LabelOf(string buttonName) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == buttonName).GetComponentInChildren<TMP_Text>().text;

        [Test]
        public void TheToolbarButtonsAreTranslatedAndComeBackInEnglish()
        {
            var c = OpenPlayground();
            Assert.AreEqual("Run", LabelOf("Run"));
            c.SetLanguage("ru", remember: false);
            Assert.AreEqual("Запуск", LabelOf("Run"));
            Assert.AreEqual("Шаг", LabelOf("Step"));
            Assert.AreEqual("Файл", LabelOf("File"));
            Assert.AreEqual("Консоль: вкл", LabelOf("ConsoleToggle"));
            Assert.AreEqual("Тема: Classic", LabelOf("Theme"));
            c.ToggleSlow();
            Assert.AreEqual("Медленно: вкл", LabelOf("Slow"));
            c.SetLanguage("en", remember: false);
            Assert.AreEqual("Run", LabelOf("Run"));
            Assert.AreEqual("Slow: on", LabelOf("Slow"));
        }

        [Test]
        public void TheCategoryNamesTheFlyoutAndTheBlocksAreTranslated()
        {
            var c = OpenPlayground();
            var toolbox = Object.FindObjectsByType<ToolboxView>(FindObjectsSortMode.None).Single();
            toolbox.SelectCategory(toolbox.Definition.Categories.FindIndex(x => x.Name == "Variables"));      // the open flyout shows the button
            c.SetLanguage("ru", remember: false);
            var names = toolbox.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToList();
            CollectionAssert.Contains(names, "Логика");
            CollectionAssert.Contains(names, "Циклы");
            Assert.AreEqual("Создать переменную...", toolbox.VariableButton.GetComponentInChildren<TMP_Text>(true).text);
            var blocks = Object.FindObjectsByType<WorkspaceView>(FindObjectsSortMode.None).First(v => v.Spawner == null)
                .GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToList();
            CollectionAssert.Contains(blocks, "напечатать", "the labels of the blocks on the workspace follow the language");
        }

        [Test]
        public void TheFileMenuOffersTheLanguagesAndPickingOneSwitchesAndRemembersIt()
        {
            var c = OpenPlayground();
            c.ShowFileMenuAt(new Vector2(100, 600));
            var menu = Object.FindFirstObjectByType<DropdownMenu>();
            CollectionAssert.Contains(menu.Items.Select(i => i.Label).ToList(), "Language…");
            c.OnFileCommand("language", new Vector2(100, 600));
            menu = Object.FindFirstObjectByType<DropdownMenu>();
            CollectionAssert.AreEqual(new[] { "English", "Русский" }, menu.Items.Select(i => i.Label).Take(2));
            Assert.IsTrue(menu.Items[0].Selected);
            menu.Pick(1);
            Assert.AreEqual("ru", Loc.Language);
            Assert.AreEqual("ru", PlayerPrefs.GetString(PlaygroundController.LanguagePrefsKey));
        }
    }
}
