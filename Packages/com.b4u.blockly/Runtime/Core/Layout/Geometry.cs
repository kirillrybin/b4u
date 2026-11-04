using System;
using System.Collections.Generic;

namespace Blockly.Layout
{
    public struct Vec2
    {
        public float X, Y;
        public Vec2(float x, float y) { X = x; Y = y; }
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    public interface ITextMeasurer
    {
        /// <summary>Width of a single line of text at the block text size.</summary>
        float Width(string text);
    }

    public static class Geometry
    {
        /// <summary>Signed area; positive when the polygon is counter-clockwise in a y-up system (clockwise in y-down).</summary>
        public static float SignedArea(IList<Vec2> p)
        {
            float a = 0;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) a += (p[j].X * p[i].Y) - (p[i].X * p[j].Y);
            return a * 0.5f;
        }

        /// <summary>Removes repeated and collinear vertices, which would break ear clipping.</summary>
        public static List<Vec2> Simplify(IList<Vec2> p)
        {
            var r = new List<Vec2>(p.Count);
            foreach (var v in p)
                if (r.Count == 0 || Math.Abs(r[r.Count - 1].X - v.X) > 1e-4f || Math.Abs(r[r.Count - 1].Y - v.Y) > 1e-4f) r.Add(v);
            if (r.Count > 1 && Math.Abs(r[0].X - r[r.Count - 1].X) < 1e-4f && Math.Abs(r[0].Y - r[r.Count - 1].Y) < 1e-4f) r.RemoveAt(r.Count - 1);
            bool changed = true;
            while (changed && r.Count > 3)
            {
                changed = false;
                for (int i = 0; i < r.Count; i++)
                {
                    var a = r[(i + r.Count - 1) % r.Count]; var b = r[i]; var c = r[(i + 1) % r.Count];
                    float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                    if (Math.Abs(cross) < 1e-4f) { r.RemoveAt(i); changed = true; break; }
                }
            }
            return r;
        }

        /// <summary>Ear-clipping triangulation of a simple polygon. Returns indices into <paramref name="poly"/> (as given).</summary>
        public static int[] Triangulate(IList<Vec2> poly)
        {
            int n = poly.Count;
            var tris = new List<int>();
            if (n < 3) return tris.ToArray();
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(i);
            bool ccw = SignedArea(poly) > 0;
            if (!ccw) idx.Reverse();

            int guard = n * n + 10;
            bool inclusive = true;       // vertices lying ON the ear's boundary also reject it (rectilinear shapes hit this a lot)
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    var a = poly[ia]; var b = poly[ib]; var c = poly[ic];
                    if (Cross(a, b, c) <= 1e-6f) continue;                         // reflex or degenerate
                    bool blocked = false;
                    for (int k = 0; k < idx.Count && !blocked; k++)
                    {
                        int ik = idx[k];
                        if (ik == ia || ik == ib || ik == ic) continue;
                        var p = poly[ik];
                        if (SamePoint(p, a) || SamePoint(p, b) || SamePoint(p, c)) continue;
                        if (inclusive ? PointInOrOnTriangle(p, a, b, c) : PointInTriangle(p, a, b, c)) blocked = true;
                    }
                    if (blocked) continue;
                    tris.Add(ia); tris.Add(ib); tris.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    inclusive = true;
                    break;
                }
                if (!clipped)
                {
                    if (inclusive) { inclusive = false; continue; }                  // degenerate input: fall back to the strict test
                    break;                                                          // numerically stuck; stop rather than loop
                }
            }
            if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
            return tris.ToArray();
        }

        static float Cross(Vec2 a, Vec2 b, Vec2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        /// <summary>Strictly inside a counter-clockwise triangle (boundary points do not block an ear).</summary>
        static bool PointInTriangle(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
            => Cross(a, b, p) > 1e-6f && Cross(b, c, p) > 1e-6f && Cross(c, a, p) > 1e-6f;

        static bool PointInOrOnTriangle(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
            => Cross(a, b, p) >= -1e-6f && Cross(b, c, p) >= -1e-6f && Cross(c, a, p) >= -1e-6f;

        static bool SamePoint(Vec2 p, Vec2 q) => Math.Abs(p.X - q.X) < 1e-4f && Math.Abs(p.Y - q.Y) < 1e-4f;

        public static float TrianglesArea(IList<Vec2> poly, int[] tris)
        {
            float a = 0;
            for (int i = 0; i < tris.Length; i += 3)
                a += System.Math.Abs(SignedArea(new[] { poly[tris[i]], poly[tris[i + 1]], poly[tris[i + 2]] }));
            return a;
        }
    }
}
