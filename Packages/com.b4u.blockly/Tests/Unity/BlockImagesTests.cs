using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    public class BlockImagesTests
    {
        GameObject root;
        Texture2D tex;
        Sprite sprite;

        [SetUp]
        public void SetUp()
        {
            tex = new Texture2D(4, 4);
            sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            BlockImages.Clear();
            if (root != null) Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite); Object.DestroyImmediate(tex);
        }

        const string Blocks = @"[{""type"":""t_move"",""message0"":""Move %1"",""args0"":[{""type"":""field_image"",""src"":""arrow"",""width"":40,""height"":30}],
            ""previousStatement"":null,""nextStatement"":null,""colour"":210}]";

        WorkspaceView Show()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            var reg = new BlockRegistry();
            reg.RegisterJson(Blocks);
            var ws = new Workspace(reg);
            WorkspaceJson.Load(ws, "{\"blocks\":{\"languageVersion\":0,\"blocks\":[{\"type\":\"t_move\",\"x\":10,\"y\":10}]}}");
            view.SetWorkspace(ws);
            return view;
        }

        [Test]
        public void ARegisteredSpriteIsDrawnInTheBlock()
        {
            BlockImages.Register("arrow", sprite);
            var view = Show();
            var pic = view.GetComponentsInChildren<Image>().Single(i => i.sprite == sprite);
            Assert.AreEqual(new Vector2(40, 30), pic.rectTransform.sizeDelta);
            Assert.IsFalse(pic.raycastTarget, "clicks go to the block");
        }

        [Test]
        public void TheResolverIsUsedWhenNothingIsRegistered()
        {
            BlockImages.Resolver = src => src == "arrow" ? sprite : null;
            Assert.AreSame(sprite, BlockImages.Get("arrow"));
            Assert.IsNull(BlockImages.Get("missing"));
        }

        [Test]
        public void AMissingPictureStillLeavesTheSpaceAndDrawsNothing()
        {
            var view = Show();
            Assert.AreEqual(0, view.GetComponentsInChildren<Image>().Count(i => i.sprite != null));
            Assert.AreEqual(30f, view.Layout.Boxes[0].Fields.Single(f => f.ImageSrc != null).H);
        }
    }
}
