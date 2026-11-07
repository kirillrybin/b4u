using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Blockly.UI.Tests
{
    public class BlockThemeTests
    {
        GameObject root;

        [TearDown] public void TearDown() { BlockStyle.Theme = null; if (root != null) Object.DestroyImmediate(root); }

        [Test]
        public void DefaultThemeKeepsTheClassicColours()
        {
            var c = new Color(0.5f, 0.4f, 0.2f);
            Assert.AreEqual(c.r * 0.7f, BlockStyle.Stroke(c, false).r, 1e-5f);
            Assert.AreEqual(c.r * 0.8f, BlockStyle.Hole(c, false).r, 1e-5f);
            Assert.AreEqual(12f, BlockStyle.FontSize);
        }

        [Test]
        public void ThemeValuesReachTheStyle()
        {
            var t = BlockTheme.CreateDefault();
            t.StrokeScale = 1.5f; t.FontSize = 18f; t.InputFill = Color.red;
            BlockStyle.Theme = t;
            Assert.AreEqual(0.6f, BlockStyle.Stroke(new Color(0.4f, 0.4f, 0.4f), false).r, 1e-5f);
            Assert.AreEqual(18f, BlockStyle.FontSize);
            Assert.AreEqual(Color.red, BlockStyle.InputFill);
            Assert.AreEqual(1f, BlockStyle.Stroke(new Color(0.9f, 0.9f, 0.9f), false).r, "colours are clamped");
        }

        [Test]
        public void ShapeSettingsReachTheLayoutAndTheClassicShapeComesBack()
        {
            var t = BlockTheme.CreateDefault();
            t.CornerRadius = 8; t.RoundNotches = true; t.RoundTabs = true;
            BlockStyle.Theme = t;
            Assert.AreEqual(8f, Blockly.Layout.BlockGeometry.Current.CornerRadius);
            Assert.IsTrue(Blockly.Layout.BlockGeometry.Current.RoundNotches);
            BlockStyle.Theme = null;
            Assert.AreEqual(0f, Blockly.Layout.BlockGeometry.Current.CornerRadius);
            Assert.IsFalse(Blockly.Layout.BlockGeometry.Current.RoundTabs);
        }

        [Test]
        public void ChangingTheThemeRebuildsTheViewWithTheNewFont()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load("{\"blocks\":{\"languageVersion\":0,\"blocks\":[{\"type\":\"text_print\",\"x\":10,\"y\":10,\"inputs\":{\"TEXT\":{\"block\":{\"type\":\"text\",\"fields\":{\"TEXT\":\"hi\"}}}}}]}}"));
            float before = view.Layout.Boxes[0].W;
            Assert.AreEqual(12f, go.GetComponentsInChildren<TextMeshProUGUI>().First(t => t.text == "hi").fontSize);

            var t2 = BlockTheme.CreateDefault();
            t2.FontSize = 24f;
            BlockStyle.Theme = t2;

            Assert.AreEqual(24f, go.GetComponentsInChildren<TextMeshProUGUI>().First(t => t.text == "hi").fontSize);
            Assert.Greater(view.Layout.Boxes[0].W, before, "bigger text makes bigger blocks");
        }
    }
}
