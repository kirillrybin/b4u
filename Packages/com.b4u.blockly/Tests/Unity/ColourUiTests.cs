using System.Linq;
using Blockly.Editing;
using NUnit.Framework;
using UnityEngine;

namespace Blockly.UI.Tests
{
    /// <summary>The colour field on screen: the swatch, the palette under it, and the toolbox category.</summary>
    public class ColourUiTests
    {
        GameObject root;

        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        WorkspaceView NewView()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load("<xml xmlns=\"https://developers.google.com/blockly/xml\"><block type=\"colour_picker\" x=\"10\" y=\"10\"><field name=\"COLOUR\">#3366ff</field></block></xml>"));
            return view;
        }

        [Test]
        public void ThePaletteOpensWithTheCurrentColourMarkedAndChoosingOneSetsTheFieldInOneUndoStep()
        {
            var view = NewView();
            var block = view.Workspace.TopBlocks[0];
            var field = block.GetField("COLOUR");
            Assert.IsTrue(view.ShowColourPicker(block, field));
            var menu = Object.FindFirstObjectByType<DropdownMenu>();
            Assert.AreEqual(70, menu.Items.Count);
            Assert.AreEqual("#3366ff", menu.Items.Single(i => i.Selected).Value);
            int red = menu.Items.ToList().FindIndex(i => i.Value == "#ff0000");
            menu.Pick(red);
            Assert.AreEqual("#ff0000", block.GetFieldValue("COLOUR"));
            Assert.IsTrue(view.Undo());
            Assert.AreEqual("#3366ff", view.Workspace.TopBlocks[0].GetFieldValue("COLOUR"));
        }

        [Test]
        public void ClickingTheSwatchOpensThePaletteAndAColourFieldIsNotTypedInto()
        {
            var view = NewView();
            var target = view.GetComponentsInChildren<FieldClickTarget>(true).Single();
            target.OnPointerClick(null);
            Assert.IsNotNull(Object.FindFirstObjectByType<DropdownMenu>());
            Assert.IsFalse(view.IsEditingField);
        }

        [Test]
        public void TheToolboxHasAColourCategoryWithItsFourBlocks()
        {
            var tb = StandardBlockSet.LoadToolbox();
            var cat = tb.Categories.Single(c => c.Name == "Colour");
            Assert.AreEqual(4, cat.Blocks.Count);
            CollectionAssert.Contains(tb.Categories.Select(c => c.Name).ToList(), "Variables");
        }
    }
}
