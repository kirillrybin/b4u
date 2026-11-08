using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>A dashed rectangular outline along the inside edge of its rect (used by the delete panel).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DashedBorder : MaskableGraphic
    {
        float thickness = 2f, dash = 8f, gap = 6f;

        public override bool raycastTarget { get => false; set { } }

        public void Set(Color colour, float width, float dashLength, float gapLength)
        {
            color = colour; thickness = Mathf.Max(0f, width); dash = Mathf.Max(1f, dashLength); gap = Mathf.Max(0f, gapLength);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (thickness <= 0f || r.width <= 0f || r.height <= 0f) return;
            float t = Mathf.Min(thickness, Mathf.Min(r.width, r.height) * 0.5f);
            Side(vh, new Vector2(r.xMin, r.yMax - t), new Vector2(r.xMax, r.yMax - t), t, true);     // top
            Side(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), t, true);              // bottom
            Side(vh, new Vector2(r.xMin, r.yMin + t), new Vector2(r.xMin, r.yMax - t), t, false);     // left
            Side(vh, new Vector2(r.xMax - t, r.yMin + t), new Vector2(r.xMax - t, r.yMax - t), t, false);
        }

        // dashes along a side, the pattern centred so both ends start and finish with a dash
        void Side(VertexHelper vh, Vector2 from, Vector2 to, float t, bool horizontal)
        {
            float length = horizontal ? to.x - from.x : to.y - from.y;
            if (length <= 0f) return;
            int count = Mathf.Max(1, Mathf.RoundToInt((length + gap) / (dash + gap)));
            float d = count == 1 ? length : (length - gap * (count - 1)) / count;
            if (d <= 0f) { d = length; count = 1; }
            for (int i = 0; i < count; i++)
            {
                float a = i * (d + gap), b = a + d;
                var min = horizontal ? new Vector2(from.x + a, from.y) : new Vector2(from.x, from.y + a);
                var max = horizontal ? new Vector2(from.x + b, from.y + t) : new Vector2(from.x + t, from.y + b);
                int v = vh.currentVertCount;
                vh.AddVert(new Vector3(min.x, min.y), color, Vector2.zero);
                vh.AddVert(new Vector3(min.x, max.y), color, Vector2.zero);
                vh.AddVert(new Vector3(max.x, max.y), color, Vector2.zero);
                vh.AddVert(new Vector3(max.x, min.y), color, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v + 2, v + 3, v);
            }
        }
    }
}
