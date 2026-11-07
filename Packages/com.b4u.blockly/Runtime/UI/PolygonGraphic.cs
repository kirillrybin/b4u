using System.Collections.Generic;
using Blockly.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// UGUI graphic that fills an arbitrary (concave) polygon given in workspace coordinates (y down, relative to
    /// this object's top-left pivot) and optionally outlines it. Used for block bodies, input holes and field backgrounds.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PolygonGraphic : MaskableGraphic
    {
        Vec2[] polygon;
        Color strokeColor;
        float strokeWidth;
        List<Vec2> simplified;
        int[] triangles;

        public Vec2[] Polygon => polygon;

        public void SetShape(Vec2[] points, Color fill, Color? stroke = null, float width = 1.2f)
        {
            polygon = points;
            color = fill;
            strokeColor = stroke ?? Color.clear;
            strokeWidth = stroke.HasValue ? width : 0f;
            simplified = Geometry.Simplify(points);
            triangles = Geometry.Triangulate(simplified);
            SetVerticesDirty();
        }

        /// <summary>
        /// Width of the soft edge in local units (about one screen pixel; 0 = hard edges). The owner keeps it matched to the
        /// canvas scale and zoom with <see cref="WorkspaceView"/>.
        /// </summary>
        public float EdgeWidth
        {
            get => edgeWidth;
            set { if (Mathf.Abs(value - edgeWidth) > 1e-4f) { edgeWidth = value; SetVerticesDirty(); } }
        }
        float edgeWidth;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (simplified == null || triangles == null || triangles.Length == 0) return;

            int n = simplified.Count;
            float f = Mathf.Clamp(edgeWidth, 0f, 6f);
            float h = strokeWidth * 0.5f;
            bool stroked = strokeWidth > 0f && strokeColor.a > 0f;

            Color32 fill = color;
            foreach (var p in simplified) vh.AddVert(new Vector3(p.X, -p.Y, 0), fill, Vector2.zero);
            for (int i = 0; i < triangles.Length; i += 3) vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);

            if (n < 3 || (f <= 0f && !stroked)) return;

            // outward miter offsets, one per vertex, for a distance of 1
            var miter = new Vector2[n];
            float sign = Geometry.SignedArea(simplified) >= 0 ? 1f : -1f;
            var normals = new Vector2[n];                                        // outward normal of the edge i -> i + 1
            for (int i = 0; i < n; i++)
            {
                var a = simplified[i]; var b = simplified[(i + 1) % n];
                float dx = b.X - a.X, dy = b.Y - a.Y, len = Mathf.Sqrt(dx * dx + dy * dy);
                normals[i] = len < 1e-5f ? Vector2.zero : new Vector2(dy, -dx) * (sign / len);
            }
            for (int i = 0; i < n; i++)
            {
                var np = normals[(i + n - 1) % n]; var nn = normals[i];
                float k = 1f + Vector2.Dot(np, nn);
                miter[i] = (np + nn) / Mathf.Max(k, 0.5f);                       // capped so sharp spikes do not shoot out
            }

            // a soft edge on a fill that has no stroke covering it
            if (f > 0f && !(stroked && h >= f))
                AddRing(vh, simplified, miter, n, new[] { 0f, f }, new[] { fill, new Color32(fill.r, fill.g, fill.b, 0) });

            if (!stroked) return;
            Color32 sc = strokeColor;
            if (f <= 0f)
            {
                AddRing(vh, simplified, miter, n, new[] { -h, h }, new[] { sc, sc });      // a hard-edged band
                return;
            }
            // a band of the stroke's width with both edges feathered across about one pixel, centred on the edges
            float core = Mathf.Max(h - f * 0.5f, 0f);
            byte alpha = (byte)(sc.a * Mathf.Min(1f, 2f * h / f));
            var solid = new Color32(sc.r, sc.g, sc.b, alpha);
            var clear = new Color32(sc.r, sc.g, sc.b, 0);
            AddRing(vh, simplified, miter, n, new[] { -core - f, -core, core, core + f }, new[] { clear, solid, solid, clear });
        }

        /// <summary>
        /// Strips of quads along the outline: one vertex per offset at each outline vertex (positive offsets point outwards),
        /// each with its own colour, joined to the next offset.
        /// </summary>
        static void AddRing(VertexHelper vh, List<Vec2> p, Vector2[] miter, int n, float[] offsets, Color32[] colours)
        {
            int k = offsets.Length, baseIndex = vh.currentVertCount;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < k; j++)
                    vh.AddVert(new Vector3(p[i].X + miter[i].x * offsets[j], -(p[i].Y + miter[i].y * offsets[j]), 0), colours[j], Vector2.zero);
            for (int i = 0; i < n; i++)
            {
                int a = baseIndex + i * k, b = baseIndex + ((i + 1) % n) * k;
                for (int j = 0; j < k - 1; j++)
                {
                    vh.AddTriangle(a + j, a + j + 1, b + j + 1);
                    vh.AddTriangle(a + j, b + j + 1, b + j);
                }
            }
        }

        /// <summary>Clicks only land on the actual block shape, not on its bounding rectangle.</summary>
        public override bool Raycast(Vector2 sp, Camera eventCamera)
        {
            if (polygon == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out var local)) return false;
            return Contains(local.x, -local.y);
        }

        bool Contains(float x, float y)
        {
            bool inside = false;
            var p = simplified;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
                if ((p[i].Y > y) != (p[j].Y > y) && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X)
                    inside = !inside;
            return inside;
        }
    }
}
