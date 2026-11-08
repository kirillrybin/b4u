using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>The list of ready-made themes and the Playground's Theme button.</summary>
    public class BlockThemeCatalogTests
    {
        BlockTheme a, b;

        [SetUp]
        public void SetUp() { BlockThemeCatalog.Clear(); a = BlockTheme.CreateDefault(); b = BlockTheme.CreateDefault(); }

        [TearDown]
        public void TearDown() { BlockThemeCatalog.Clear(); BlockStyle.Theme = null; Object.DestroyImmediate(a); Object.DestroyImmediate(b); }

        [Test]
        public void ClassicIsAlwaysFirstAndRegisteredThemesFollowInOrder()
        {
            CollectionAssert.AreEqual(new[] { "Classic" }, BlockThemeCatalog.Names);
            BlockThemeCatalog.Register("Candy", a);
            BlockThemeCatalog.Register("Dark", b);
            CollectionAssert.AreEqual(new[] { "Classic", "Candy", "Dark" }, BlockThemeCatalog.Names);
            BlockThemeCatalog.Register("Candy", b);                            // same name: replaced, same place
            CollectionAssert.AreEqual(new[] { "Classic", "Candy", "Dark" }, BlockThemeCatalog.Names);
            Assert.AreSame(b, BlockThemeCatalog.Get("Candy"));
            BlockThemeCatalog.Register("Classic", a);                          // cannot be taken
            Assert.IsNull(BlockThemeCatalog.Get("Classic"));
        }

        [Test]
        public void TheBuiltInThemesAreRegisteredAndApply()
        {
            BuiltInThemes.Register();
            CollectionAssert.AreEqual(new[] { "Classic", "Dark", "High Contrast" }, BlockThemeCatalog.Names);
            Assert.IsTrue(BlockThemeCatalog.Select("Dark"));
            Assert.AreEqual("Dark", BlockThemeCatalog.CurrentName);
            Assert.AreNotEqual(BlockTheme.CreateDefault().WorkspaceBackground, BlockStyle.Theme.WorkspaceBackground);
            Assert.IsTrue(BlockThemeCatalog.Select("High Contrast"));
            Assert.AreEqual("High Contrast", BlockThemeCatalog.CurrentName);
            Assert.AreEqual(Color.black, BlockStyle.Theme.WorkspaceBackground);
        }

        [Test]
        public void SelectingAppliesTheThemeAndClassicRestoresTheDefault()
        {
            BlockThemeCatalog.Register("Candy", a);
            Assert.AreEqual("Classic", BlockThemeCatalog.CurrentName);
            int changes = 0; System.Action count = () => changes++;
            BlockStyle.ThemeChanged += count;
            Assert.IsTrue(BlockThemeCatalog.Select("Candy"));
            Assert.AreSame(a, BlockStyle.Theme);
            Assert.AreEqual("Candy", BlockThemeCatalog.CurrentName);
            Assert.IsTrue(BlockThemeCatalog.Select("Classic"));
            Assert.AreEqual("Classic", BlockThemeCatalog.CurrentName);
            Assert.IsFalse(BlockThemeCatalog.Select("Nope"));
            BlockStyle.ThemeChanged -= count;
            Assert.AreEqual(2, changes);
            BlockStyle.Theme = b;
            Assert.AreEqual("Custom", BlockThemeCatalog.CurrentName, "a theme set by hand is not in the list");
        }

        [Test]
        public void AFactoryThemeIsBuiltOnceWhenFirstChosen()
        {
            int built = 0;
            BlockThemeCatalog.Register("Lazy", () => { built++; return a; });
            Assert.AreEqual(0, built);
            BlockThemeCatalog.Select("Lazy"); BlockThemeCatalog.Select("Classic"); BlockThemeCatalog.Select("Lazy");
            Assert.AreEqual(1, built);
            Assert.AreSame(a, BlockStyle.Theme);
        }

        [Test]
        public void TheThemeButtonShowsTheThemeInUseAndItsMenuListsTheCatalog()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var c = Object.FindFirstObjectByType<PlaygroundController>();
            c.ShowByName("text");
            BlockThemeCatalog.Register("Candy", a);
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x => x.name == "Theme");
            Assert.AreEqual("ShowThemeMenu", button.onClick.GetPersistentMethodName(0));
            var label = button.GetComponentInChildren<TMP_Text>();
            c.SetThemeButton((RectTransform)button.transform, label);
            Assert.AreEqual("Theme: Classic", label.text);

            c.ShowThemeMenuAt(new Vector2(100, 600));
            var menu = Object.FindFirstObjectByType<DropdownMenu>();
            Assert.NotNull(menu);
            CollectionAssert.AreEqual(new[] { "Classic", "Candy" }, menu.Items.Select(i => i.Label));
            Assert.IsTrue(menu.Items[0].Selected);
            menu.Pick(1);
            Assert.AreSame(a, BlockStyle.Theme);
            Assert.AreEqual("Theme: Candy", label.text);
        }

        [Test]
        public void TheThemeLabelFollowsTheThemeInAScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var c = Object.FindFirstObjectByType<PlaygroundController>();
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x => x.name == "Theme");
            var label = button.GetComponentInChildren<TMP_Text>();
            BlockThemeCatalog.Register("Candy", a);
            BlockStyle.Theme = a;                                  // set before the controller starts, as a skin does
            // in Play the scene's own fields are used and SetThemeButton is not called: OnEnable does the work (EditMode never calls it)
            typeof(PlaygroundController).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(c, null);
            Assert.AreEqual("Theme: Candy", label.text);
            BlockStyle.Theme = null;
            Assert.AreEqual("Theme: Classic", label.text);
        }
    }
}
