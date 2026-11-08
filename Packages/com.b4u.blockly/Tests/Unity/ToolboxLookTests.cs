using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>Category buttons, the flyout title and the corners of the toolbox and flyout follow the theme.</summary>
    public class ToolboxLookTests
    {
        ToolboxView toolbox;
        BlockTheme theme;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<PlaygroundController>();
            Assert.IsTrue(controller.ShowByName("text"), "a program, so the main workspace exists");
            toolbox = controller.Toolbox;
            toolbox.SetDefinition(StandardBlockSet.LoadToolbox());
            theme = BlockTheme.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            BlockStyle.Theme = null;
            BlockImages.Clear();
            Object.DestroyImmediate(theme);
        }

        Image[] Plates() => toolbox.GetComponentsInChildren<Button>(true).Select(b => b.targetGraphic as Image).ToArray();

        [Test]
        public void ACategoryPlateColourReplacesTheTintedOne()
        {
            theme.CategoryPlate = new Color(0.4f, 0.4f, 0.6f, 1f);
            theme.CategoryPlateSelected = new Color(0.9f, 0.5f, 0.1f, 1f);
            theme.CategoryText = new Color(0.7f, 0.7f, 0.8f, 1f);
            theme.CategoryTextSelected = Color.yellow;
            BlockStyle.Theme = theme;
            toolbox.SelectCategory(1);
            var plates = Plates();
            for (int i = 0; i < plates.Length; i++)
                Assert.AreEqual(i == 1 ? theme.CategoryPlateSelected : theme.CategoryPlate, plates[i].color, "plate " + i);
            var texts = toolbox.GetComponentsInChildren<Button>(true).Select(b => b.GetComponentInChildren<TMP_Text>()).ToArray();
            Assert.AreEqual(Color.yellow, texts[1].color);
            Assert.AreEqual(theme.CategoryText, texts[0].color);
        }

        [Test]
        public void ATransparentPlateKeepsTheCategoryColour()
        {
            BlockStyle.Theme = theme;
            toolbox.SelectCategory(0);
            var cat = toolbox.Definition.Categories[0];
            Assert.AreEqual(BlockStyle.Parse(cat.ColourHex), Plates()[0].color);
        }

        [Test]
        public void ASelectedPlatePictureAndIconAreUsedWhileSelected()
        {
            var def = new Blockly.Editing.ToolboxDefinition();
            def.Categories.Add(new Blockly.Editing.ToolboxCategory { Name = "Events", ColourHex = "#a0a040", Icon = "icons/events" });
            var plain = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.zero);
            var white = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.zero);
            var plate = Sprite.Create(new Texture2D(8, 8), new Rect(0, 0, 8, 8), Vector2.zero, 100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
            BlockImages.Register("icons/events", plain);
            BlockImages.Register("icons/events@selected", white);
            BlockImages.Register("plate/Events", plate);
            toolbox.SetDefinition(def);
            var img = Plates()[0];
            var icon = img.transform.Find("Icon").GetComponent<Image>();
            Assert.AreSame(plain, icon.sprite);
            toolbox.SelectCategory(0);
            Assert.AreSame(plate, img.sprite);
            Assert.AreSame(white, icon.sprite);
            toolbox.CloseFlyout();
            Assert.AreNotSame(plate, img.sprite);
            Assert.AreSame(plain, icon.sprite);
            foreach (var s in new[] { plain, white, plate }) { Object.DestroyImmediate(s.texture); Object.DestroyImmediate(s); }
        }

        [Test]
        public void TheFlyoutShowsTheCategoryNameWhenTheThemeAsks()
        {
            theme.FlyoutTitle = true; theme.FlyoutTitleHeight = 60f; theme.FlyoutTitleSize = 30f; theme.FlyoutTitleColour = Color.cyan;
            BlockStyle.Theme = theme;
            toolbox.SelectCategory(2);
            var panel = GameObject.Find("Flyout").transform;
            var title = panel.Find("Title").GetComponent<TMP_Text>();
            Assert.IsTrue(title.gameObject.activeSelf);
            Assert.AreEqual(toolbox.Definition.Categories[2].Name, title.text);
            Assert.AreEqual(Color.cyan, title.color);
            Assert.AreEqual(30f, title.fontSize, 1e-3f);
            var viewport = panel.GetComponent<ScrollRect>().viewport;
            Assert.AreEqual(-60f, viewport.offsetMax.y, 1e-3f, "the blocks start below the title");

            BlockStyle.Theme = null;
            toolbox.SelectCategory(2);
            Assert.IsFalse(panel.Find("Title").gameObject.activeSelf);
            Assert.AreEqual(0f, viewport.offsetMax.y, 1e-3f);
        }

        [Test]
        public void TheFlyoutPaddingMovesTheBlocks()
        {
            theme.FlyoutPadding = 30f;
            BlockStyle.Theme = theme;
            toolbox.SelectCategory(0);
            var rt = (RectTransform)toolbox.FlyoutView.transform;
            Assert.AreEqual(new Vector2(30f, -30f), rt.anchoredPosition);
        }

        [Test]
        public void ToolboxAndFlyoutRoundOnlyTheCornersTheThemeNames()
        {
            theme.PanelCornerRadius = 14f;
            theme.ToolboxCorners = UiCorners.Left; theme.ToolboxCornersWithFlyout = UiCorners.Left; theme.FlyoutCorners = UiCorners.Right;
            BlockStyle.Theme = theme;
            toolbox.SelectCategory(0);
            var strip = toolbox.GetComponent<Image>();
            var flyout = GameObject.Find("Flyout").GetComponent<Image>();
            var probe = new GameObject("probe", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            UiShape.Round(probe, 14f, UiCorners.Left);
            Assert.AreSame(probe.sprite, strip.sprite);
            UiShape.Round(probe, 14f, UiCorners.Right);
            Assert.AreSame(probe.sprite, flyout.sprite);
            Object.DestroyImmediate(probe.gameObject);
        }

        [Test]
        public void ButtonSizeGapAndIconSizeFollowTheTheme()
        {
            theme.CategoryButtonHeight = 64f; theme.CategoryGap = 20f; theme.CategoryIconSize = 40f;
            BlockStyle.Theme = theme;
            var rts = toolbox.GetComponentsInChildren<Button>(true).Select(b => (RectTransform)b.transform).ToArray();
            Assert.AreEqual(64f, rts[0].sizeDelta.y);
            Assert.AreEqual(-84f, rts[1].anchoredPosition.y, 1e-3f, "64 + 20 below the first");
            var icons = toolbox.GetComponentsInChildren<Image>(true).Where(i => i.name == "Icon").ToArray();
            foreach (var i in icons) Assert.AreEqual(40f, ((RectTransform)i.transform).sizeDelta.x);
            BlockStyle.Theme = null;
            Assert.AreEqual(40f, rts[0].sizeDelta.y);
            Assert.AreEqual(-42f, rts[1].anchoredPosition.y, 1e-3f);
        }

        [Test]
        public void ACategoryWithoutAnIconLooksForCategorySlashItsName()
        {
            var def = new Blockly.Editing.ToolboxDefinition();
            def.Categories.Add(new Blockly.Editing.ToolboxCategory { Name = "Control", ColourHex = "#d08030" });
            var s = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.zero);
            BlockImages.Register("category/Control", s);
            toolbox.SetDefinition(def);
            var icon = toolbox.GetComponentsInChildren<Image>(true).First(i => i.name == "Icon");
            Assert.AreSame(s, icon.sprite);
            Object.DestroyImmediate(s.texture); Object.DestroyImmediate(s);
        }

        [Test]
        public void TheToolboxWidthComesFromTheTheme()
        {
            var layout = Object.FindFirstObjectByType<PlaygroundLayout>();
            toolbox.SelectCategory(0);                                    // the flyout is only found while it is open
            theme.ToolboxWidth = 260f;
            BlockStyle.Theme = theme;
            layout.Apply(1280, 720);
            Assert.AreEqual(260f, ((RectTransform)toolbox.transform).offsetMax.x);
            Assert.AreEqual(260f, GameObject.Find("Scroll").GetComponent<RectTransform>().offsetMin.x);
            Assert.AreEqual(260f, GameObject.Find("Flyout").GetComponent<RectTransform>().offsetMin.x);
            BlockStyle.Theme = null;
            layout.Apply(1280, 720);
            Assert.AreEqual(130f, ((RectTransform)toolbox.transform).offsetMax.x);
        }

        [Test]
        public void TheStripChangesCornersWhileTheFlyoutIsOpen()
        {
            theme.PanelCornerRadius = 14f;
            theme.ToolboxCorners = UiCorners.All; theme.ToolboxCornersWithFlyout = UiCorners.Left;
            BlockStyle.Theme = theme;
            var strip = toolbox.GetComponent<Image>();
            var probe = new GameObject("probe", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            toolbox.CloseFlyout();
            UiShape.Round(probe, 14f, UiCorners.All);
            Assert.AreSame(probe.sprite, strip.sprite, "closed: every corner round");
            toolbox.SelectCategory(0);
            UiShape.Round(probe, 14f, UiCorners.Left);
            Assert.AreSame(probe.sprite, strip.sprite, "open: the right side joins the flyout");
            toolbox.CloseFlyout();
            UiShape.Round(probe, 14f, UiCorners.All);
            Assert.AreSame(probe.sprite, strip.sprite);
            Object.DestroyImmediate(probe.gameObject);
        }
    }
}
