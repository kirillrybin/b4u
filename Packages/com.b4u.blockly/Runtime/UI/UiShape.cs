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
        static readonly Sprite[] sprites = new Sprite[16];          // one per set of rounded corners

        static Sprite Rounded(UiCorners corners)
        {
            int mask = (int)corners & 15;
            if (sprites[mask] != null) return sprites[mask];
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    // y grows upwards in a texture: the bottom half is y < Corner
                    bool right = x >= Size / 2, top = y >= Size / 2;
                    var here = top ? (right ? UiCorners.TopRight : UiCorners.TopLeft) : (right ? UiCorners.BottomRight : UiCorners.BottomLeft);
                    byte a = 255;
                    if ((corners & here) != 0)
                    {
                        // distance outside the rounded rectangle, measured from the corner centre
                        float cx = Mathf.Clamp(x + 0.5f, Corner, Size - Corner), cy = Mathf.Clamp(y + 0.5f, Corner, Size - Corner);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        a = (byte)(Mathf.Clamp01(Corner - d + 0.5f) * 255f);
                    }
                    px[y * Size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(Corner, Corner, Corner, Corner));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprites[mask] = sprite;
        }

        static bool IsOurs(Sprite s)
        {
            if (s == null) return false;
            foreach (var x in sprites) if (x == s) return true;
            return false;
        }

        /// <summary>Gives the image corners of <paramref name="radius"/> canvas units; 0 or less restores a plain rectangle.</summary>
        public static void Round(Graphic graphic, float radius) => Round(graphic, radius, UiCorners.All);

        /// <summary>Like <see cref="Round(Graphic,float)"/>, but only the given corners are rounded (a panel that joins another keeps square sides there).</summary>
        public static void Round(Graphic graphic, float radius, UiCorners corners)
        {
            if (!(graphic is Image image)) return;
            if (radius <= 0f || corners == UiCorners.None)
            {
                if (IsOurs(image.sprite)) { image.sprite = null; image.type = Image.Type.Simple; image.pixelsPerUnitMultiplier = 1f; }
                return;
            }
            image.sprite = Rounded(corners);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Corner / radius;     // the corner is Corner texels, shown as `radius` units
        }
    }

    /// <summary>The corners of a rectangle.</summary>
    [System.Flags]
    public enum UiCorners
    {
        None = 0, TopLeft = 1, TopRight = 2, BottomRight = 4, BottomLeft = 8,
        Left = TopLeft | BottomLeft, Right = TopRight | BottomRight, Top = TopLeft | TopRight, Bottom = BottomLeft | BottomRight,
        All = 15,
    }
}
