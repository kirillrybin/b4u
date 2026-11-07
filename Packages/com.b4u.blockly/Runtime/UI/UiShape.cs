using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// Rounded corners for plain UGUI panels and buttons without any art assets: one generated, nine-sliced
    /// rounded-rectangle sprite is shared, and each Image scales its corner to the requested radius.
    /// </summary>
    public static class UiShape
    {
        const int Size = 64, Corner = 32;
        static Sprite sprite;

        static Sprite Rounded()
        {
            if (sprite != null) return sprite;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    // distance outside the rounded rectangle, measured from the nearest corner centre
                    float cx = Mathf.Clamp(x + 0.5f, Corner, Size - Corner), cy = Mathf.Clamp(y + 0.5f, Corner, Size - Corner);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    byte a = (byte)(Mathf.Clamp01(Corner - d + 0.5f) * 255f);
                    px[y * Size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(Corner, Corner, Corner, Corner));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>Gives the image corners of <paramref name="radius"/> canvas units; 0 or less restores a plain rectangle.</summary>
        public static void Round(Graphic graphic, float radius)
        {
            if (!(graphic is Image image)) return;
            if (radius <= 0f)
            {
                if (image.sprite == sprite && sprite != null) { image.sprite = null; image.type = Image.Type.Simple; image.pixelsPerUnitMultiplier = 1f; }
                return;
            }
            image.sprite = Rounded();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Corner / radius;     // the corner is Corner texels, shown as `radius` units
        }
    }
}
