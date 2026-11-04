using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>Opens the saved Playground scene and drives it the way its buttons do.</summary>
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

        [Test]
        public void EveryProgramLoadsAndDrawsBlocks()
        {
            var c = Open();
            var view = Object.FindFirstObjectByType<WorkspaceView>();
            Assert.Greater(c.Count, 10);
            for (int i = 0; i < c.Count; i++)
            {
                c.Show(i);
                Assert.Greater(view.Layout.Boxes.Count, 0, "program #" + i);
                Assert.Greater(view.GetComponentsInChildren<PolygonGraphic>(true).Length, 0, "program #" + i);
                var rt = (RectTransform)view.transform;
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
            foreach (var (button, method) in new[] { ("Next", "Next"), ("Prev", "Previous") })
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
            var scroll = Object.FindFirstObjectByType<ScrollRect>();
            Assert.NotNull(scroll);
            Assert.NotNull(scroll.content.GetComponent<WorkspaceView>());
            Assert.NotNull(scroll.viewport.GetComponent<RectMask2D>());
        }
    }
}
