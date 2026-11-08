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

        static readonly Sprite[] chevrons = new Sprite[2];

        /// <summary>A white chevron (">" when <paramref name="right"/>, "&lt;" otherwise) with round ends; tint it with the Image colour.</summary>
        public static Sprite Chevron(bool right)
        {
            int k = right ? 1 : 0;
            if (chevrons[k] != null) return chevrons[k];
            const int S = 64; const float half = 3f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var tip = new Vector2(right ? 42f : 22f, 32f);
            var a = new Vector2(right ? 24f : 40f, 14f); var b = new Vector2(right ? 24f : 40f, 50f);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(DistanceToSegment(p, a, tip), DistanceToSegment(p, tip, b));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(half - d + 0.5f) * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return chevrons[k] = sprite;
        }

        /// <summary>A white trash can (a body with a tilted lid and a knob); tint it with the image colour.</summary>
        public static Sprite Trash()
        {
            if (trash != null) return trash;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var lidA = new Vector2(10f, 41f); var lidB = new Vector2(54f, 49f); var knob = new Vector2(31f, 50f);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // body: a rounded box, x 15..49, y 5..39
                    var c = new Vector2(32f, 22f); var q = new Vector2(Mathf.Abs(p.x - c.x) - (17f - 6f), Mathf.Abs(p.y - c.y) - (17f - 6f));
                    float body = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 6f;
                    float lid = DistanceToSegment(p, lidA, lidB) - 3.2f;
                    float knobD = (p - knob).magnitude - 3.6f;
                    float d = Mathf.Min(body, Mathf.Min(lid, knobD));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            trash = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            trash.hideFlags = HideFlags.HideAndDontSave;
            return trash;
        }

        static Sprite trash;

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
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
