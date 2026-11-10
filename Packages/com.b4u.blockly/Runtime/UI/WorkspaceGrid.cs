using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// The dotted grid behind the blocks of a workspace. It fills the viewport of the scroll area and draws only the points that are
    /// in view, so it needs no texture and follows panning and zoom exactly (the points are at multiples of the spacing in workspace units).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkspaceGrid : MaskableGraphic
    {
        const float MinScreenSpacing = 4f;       // a finer grid is a haze: not drawn
        const int MaxPoints = 30000;

        float spacing, dot, zoom = 1f;
        Vector2 origin;

        /// <summary>Creates the grid as the first (back-most) child of <paramref name="viewport"/>.</summary>
        public static WorkspaceGrid Create(RectTransform viewport)
        {
            var go = new GameObject("Grid", typeof(RectTransform), typeof(CanvasRenderer), typeof(WorkspaceGrid));
            var rt = (RectTransform)go.transform;
            rt.SetParent(viewport, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var g = go.GetComponent<WorkspaceGrid>();
            g.raycastTarget = false;
            return g;
        }

        /// <param name="contentPosition">Where the workspace's top-left corner is, from the top-left corner of the viewport.</param>
        public void Apply(float spacingUnits, Color colour, float dotUnits, Vector2 contentPosition, float zoomFactor)
        {
            if (Mathf.Approximately(spacing, spacingUnits) && Mathf.Approximately(dot, dotUnits) && color == colour
                && origin == contentPosition && Mathf.Approximately(zoom, zoomFactor)) return;
            spacing = spacingUnits; dot = dotUnits; origin = contentPosition; zoom = zoomFactor;
            color = colour;
            SetVerticesDirty();
        }

        /// <summary>The centres of the points in view, in the local space of the grid (the viewport).</summary>
        public List<Vector2> Points()
        {
            var points = new List<Vector2>();
            float step = spacing * zoom;
            if (spacing <= 0f || step < MinScreenSpacing) return points;
            var r = rectTransform.rect;
            float x0 = FirstAtOrAfter(origin.x, step);                 // distance from the left edge of the viewport
            float y0 = FirstAtOrAfter(-origin.y, step);                // distance from its top edge
            int columns = Mathf.CeilToInt((r.width - x0) / step) + 1, rows = Mathf.CeilToInt((r.height - y0) / step) + 1;
            if (columns <= 0 || rows <= 0 || (long)columns * rows > MaxPoints) return points;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < columns; i++)
                {
                    float x = x0 + i * step, y = y0 + j * step;
                    if (x > r.width || y > r.height) continue;
                    points.Add(new Vector2(r.xMin + x, r.yMax - y));
                }
            return points;
        }

        /// <summary>The smallest origin + k * step (k any whole number) that is not before 0.</summary>
        static float FirstAtOrAfter(float origin, float step)
        {
            float m = origin % step;
            return m < 0f ? m + step : m;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float half = Mathf.Max(0.5f, dot * zoom) / 2f;
            foreach (var p in Points())
            {
                int v = vh.currentVertCount;
                vh.AddVert(new Vector3(p.x - half, p.y - half), color, Vector2.zero);
                vh.AddVert(new Vector3(p.x - half, p.y + half), color, Vector2.zero);
                vh.AddVert(new Vector3(p.x + half, p.y + half), color, Vector2.zero);
                vh.AddVert(new Vector3(p.x + half, p.y - half), color, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v + 2, v + 3, v);
            }
        }
    }
}
