using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    public class UiShapeTests
    {
        GameObject go;
        [TearDown] public void TearDown() { if (go != null) Object.DestroyImmediate(go); }

        Image NewImage() { go = new GameObject("img", typeof(RectTransform), typeof(Image)); return go.GetComponent<Image>(); }

        [Test]
        public void ARadiusGivesASlicedSpriteScaledToTheRadius()
        {
            var img = NewImage();
            UiShape.Round(img, 16);
            Assert.AreEqual(Image.Type.Sliced, img.type);
            Assert.NotNull(img.sprite);
            Assert.AreEqual(2f, img.pixelsPerUnitMultiplier, 1e-4f, "32 texel corner shown as 16 units");
            UiShape.Round(img, 8);
            Assert.AreEqual(4f, img.pixelsPerUnitMultiplier, 1e-4f);
        }

        [Test]
        public void TheCornerOfTheSpriteIsTransparentAndTheMiddleIsSolid()
        {
            var tex = NewImageSprite().texture;
            Assert.AreEqual(0f, tex.GetPixel(0, 0).a, 0.01f, "outer corner");
            Assert.AreEqual(1f, tex.GetPixel(32, 32).a, 0.01f, "middle");
            Assert.AreEqual(1f, tex.GetPixel(32, 0).a, 0.01f, "straight edge");
        }

        Sprite NewImageSprite() { var i = NewImage(); UiShape.Round(i, 10); return i.sprite; }

        [Test]
        public void ZeroRestoresAPlainRectangleButLeavesOtherSpritesAlone()
        {
            var img = NewImage();
            UiShape.Round(img, 12); UiShape.Round(img, 0);
            Assert.IsNull(img.sprite); Assert.AreEqual(Image.Type.Simple, img.type);

            var own = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
            img.sprite = own;
            UiShape.Round(img, 0);
            Assert.AreSame(own, img.sprite, "a sprite the app set is not removed");
            Object.DestroyImmediate(own.texture); Object.DestroyImmediate(own);
        }
    }
}
