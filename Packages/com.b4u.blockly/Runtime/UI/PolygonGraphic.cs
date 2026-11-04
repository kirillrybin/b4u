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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (simplified == null || triangles == null || triangles.Length == 0) return;

            Color32 fill = color;
            foreach (var p in simplified) vh.AddVert(new Vector3(p.X, -p.Y, 0), fill, Vector2.zero);
            for (int i = 0; i < triangles.Length; i += 3) vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);

            if (strokeWidth <= 0f || strokeColor.a <= 0f) return;
            Color32 sc = strokeColor;
            float h = strokeWidth * 0.5f;
            int n = simplified.Count;
            for (int i = 0; i < n; i++)
            {
                var a = simplified[i]; var b = simplified[(i + 1) % n];
                float dx = b.X - a.X, dy = b.Y - a.Y;
                float len = Mathf.Sqrt(dx * dx + dy * dy);
                if (len < 1e-4f) continue;
                float nx = -dy / len * h, ny = dx / len * h;
                int v = vh.currentVertCount;
                vh.AddVert(new Vector3(a.X + nx, -(a.Y + ny), 0), sc, Vector2.zero);
                vh.AddVert(new Vector3(b.X + nx, -(b.Y + ny), 0), sc, Vector2.zero);
                vh.AddVert(new Vector3(b.X - nx, -(b.Y - ny), 0), sc, Vector2.zero);
                vh.AddVert(new Vector3(a.X - nx, -(a.Y - ny), 0), sc, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v, v + 2, v + 3);
                // square at the corner so consecutive segments join without a notch
                int c = vh.currentVertCount;
                vh.AddVert(new Vector3(a.X - h, -(a.Y - h), 0), sc, Vector2.zero);
                vh.AddVert(new Vector3(a.X + h, -(a.Y - h), 0), sc, Vector2.zero);
                vh.AddVert(new Vector3(a.X + h, -(a.Y + h), 0), sc, Vector2.zero);
                vh.AddVert(new Vector3(a.X - h, -(a.Y + h), 0), sc, Vector2.zero);
                vh.AddTriangle(c, c + 1, c + 2); vh.AddTriangle(c, c + 2, c + 3);
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
