using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blockly.UI.Tests
{
    /// <summary>How the Playground lays itself out on a desktop window, a portrait phone and a landscape phone.</summary>
    public class PlaygroundLayoutTests
    {
        PlaygroundLayout Open()
        {
            EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", OpenSceneMode.Single);
            var layout = Object.FindFirstObjectByType<PlaygroundLayout>();
            Assert.NotNull(layout, "the scene has a PlaygroundLayout");
            return layout;
        }

        static RectTransform Child(string path) => (RectTransform)GameObject.Find("Canvas").transform.Find(path);

        static Rect Box(RectTransform rt) => new Rect(rt.anchoredPosition.x, -rt.anchoredPosition.y, rt.sizeDelta.x, rt.sizeDelta.y);   // top-left pivot, y grows downwards

        static RectTransform[] Buttons() => new[] { "Prev", "Next", "Run", "Step", "Stop", "Slow", "Undo", "Redo", "File" }.Select(n => Child("SafeArea/Toolbar/" + n)).ToArray();

        [Test]
        public void ADesktopWindowKeepsTheDesignedSingleRow()
        {
            var layout = Open();
            layout.Apply(1280, 720);
            Assert.AreEqual(1, layout.ToolbarRows);
            Assert.AreEqual(48f, layout.ToolbarHeight);
            Assert.AreEqual(150f, Child("SafeArea/Toolbar/Run").anchoredPosition.x);
            Assert.AreEqual(736f, Child("SafeArea/Toolbar/File").anchoredPosition.x);
            Assert.AreEqual(190f, layout.EffectiveConsoleHeight);
            Assert.AreEqual(-48f, Child("SafeArea/Scroll").offsetMax.y);
        }

        [TestCase(360f, 780f)]
        [TestCase(412f, 892f)]
        [TestCase(768f, 1024f)]
        public void APortraitScreenWrapsTheToolbarAndEveryButtonStaysOnScreenWithoutOverlap(float w, float h)
        {
            var layout = Open();
            layout.Apply(w, h);
            Assert.Greater(layout.ToolbarRows, 1, "nine buttons and a title do not fit one row of " + w);
            var boxes = Buttons().Select(Box).ToArray();
            foreach (var b in boxes)
            {
                Assert.GreaterOrEqual(b.xMin, 0f);
                Assert.LessOrEqual(b.xMax, w + 0.01f, "inside the width " + w);
                Assert.LessOrEqual(b.yMax, layout.ToolbarHeight, "inside the toolbar");
            }
            for (int i = 0; i < boxes.Length; i++)
                for (int j = i + 1; j < boxes.Length; j++)
                    Assert.IsFalse(boxes[i].Overlaps(boxes[j]), Buttons()[i].name + " overlaps " + Buttons()[j].name);
            Assert.AreEqual(-layout.ToolbarHeight, Child("SafeArea/Scroll").offsetMax.y, 0.01f, "the workspace starts below the toolbar");
            Assert.AreEqual(layout.ToolbarHeight, Child("SafeArea/Toolbar").sizeDelta.y, 0.01f);
        }

        [Test]
        public void TheTitleHasARowOrRoomOfItsOwnAndNeverCoversAButton()
        {
            var layout = Open();
            layout.Apply(360, 780);
            var title = Box(Child("SafeArea/Toolbar/Title"));
            Assert.LessOrEqual(title.xMax, 360.01f);
            foreach (var b in Buttons().Select(Box)) Assert.IsFalse(title.Overlaps(b));
        }

        [Test]
        public void TheFlyoutNeverReachesPastTheScreenAndTheWorkspaceKeepsRoom()
        {
            var layout = Open();
            layout.Apply(360, 780);
            var flyout = Child("SafeArea/Flyout");
            Assert.LessOrEqual(flyout.offsetMax.x, 360f, "the flyout fits in the width");
            Assert.GreaterOrEqual(flyout.offsetMax.x - flyout.offsetMin.x, 120f);

            layout.Apply(740, 360);                                       // a landscape phone: short, so the console gives way
            Assert.Less(layout.EffectiveConsoleHeight, 190f);
            float workspaceHeight = 360 - layout.ToolbarHeight - layout.EffectiveConsoleHeight;
            Assert.GreaterOrEqual(workspaceHeight, 120f);
            Assert.AreEqual(layout.EffectiveConsoleHeight, Child("SafeArea/Console").sizeDelta.y, 0.01f);
        }

        [Test]
        public void GoingBackToAWideWindowRestoresTheDesign()
        {
            var layout = Open();
            layout.Apply(360, 780);
            layout.Apply(1280, 720);
            Assert.AreEqual(1, layout.ToolbarRows);
            Assert.AreEqual(150f, Child("SafeArea/Toolbar/Run").anchoredPosition.x);
            Assert.AreEqual(822f, Child("SafeArea/Toolbar/Title").anchoredPosition.x);
        }

        [Test]
        public void TheSafeAreaMovesTheContentAwayFromTheNotch()
        {
            var layout = Open();
            layout.ApplySafeArea(new Rect(0, 100, 1000, 1800), new Vector2Int(1000, 2000));   // 100 px home bar below, 100 px notch above
            var safe = Child("SafeArea");
            Assert.AreEqual(new Vector2(0f, 0.05f), safe.anchorMin);
            Assert.AreEqual(new Vector2(1f, 0.95f), safe.anchorMax);
        }
    }
}
