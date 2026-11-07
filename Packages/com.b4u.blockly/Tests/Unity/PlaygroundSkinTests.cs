using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>The interface colours of a BlockTheme reach the saved Playground scene.</summary>
    public class PlaygroundSkinTests
    {
        [SetUp] public void SetUp() => EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", OpenSceneMode.Single);
        [TearDown] public void TearDown() => BlockStyle.Theme = null;

        [Test]
        public void TheDefaultThemeLeavesTheSceneColoursAsBuilt()
        {
            var skin = Object.FindFirstObjectByType<PlaygroundSkin>();
            Assert.NotNull(skin, "the scene has a PlaygroundSkin");
            var before = skin.GetComponentsInChildren<Image>(true).Select(i => i.color).ToArray();
            skin.Apply();
            CollectionAssert.AreEqual(before, skin.GetComponentsInChildren<Image>(true).Select(i => i.color).ToArray());
        }

        [Test]
        public void ChangingTheThemeRepaintsPanelsButtonsAndTheToolbox()
        {
            var skin = Object.FindFirstObjectByType<PlaygroundSkin>();
            skin.Apply();                                                  // subscribes, as OnEnable would at runtime
            var toolbox = Object.FindFirstObjectByType<ToolboxView>();
            toolbox.SetDefinition(StandardBlockSet.LoadToolbox());         // initialises and subscribes, as Awake would at runtime
            var t = BlockTheme.CreateDefault();
            t.WorkspaceBackground = Color.red; t.ConsoleBackground = Color.green; t.ButtonColour = Color.blue;
            t.ButtonText = Color.yellow; t.ToolboxBackground = Color.magenta; t.CategoryText = Color.cyan;
            BlockStyle.Theme = t;

            Assert.AreEqual(Color.red, GameObject.Find("Scroll").GetComponent<Image>().color);
            Assert.AreEqual(Color.green, GameObject.Find("Console").GetComponent<Image>().color);
            Assert.AreEqual(Color.blue, GameObject.Find("Run").GetComponent<Image>().color);
            Assert.AreEqual(Color.yellow, GameObject.Find("Run").GetComponentInChildren<TMP_Text>().color);
            Assert.AreEqual(Color.magenta, toolbox.GetComponent<Image>().color);
            Assert.AreEqual(Color.cyan, toolbox.GetComponentsInChildren<TMP_Text>(true).First().color);
        }
    }
}
