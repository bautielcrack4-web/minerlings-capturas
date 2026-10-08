using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mineros.UI
{
    /// <summary>
    /// Rasterizador 2D propio (poligonos, trazos, circulos) con antialiasing por supersampling.
    /// Trabaja en "unidades de diseño" (y hacia abajo, como Godot) y produce una Texture2D RGBA.
    /// </summary>
    public sealed class Painter
    {
        public readonly int W, H;          // pixeles finales
        readonly int ss;                   // factor de supersampling
        readonly float scale;              // pixeles hi-res por unidad
        readonly int hw, hh;
        readonly float[] buf;              // RGBA premultiplicado, hi-res
        readonly bool[] mask;              // cobertura temporal
        int mx0, mx1, my0, my1;            // caja sucia de la mascara
        // transformacion afin actual: p' = (a*x + c*y + tx, b*x + d*y + ty)
        float ma = 1, mb = 0, mc = 0, md = 1, mtx = 0, mty = 0;
        readonly Stack<float[]> stack = new Stack<float[]>();
        readonly List<float> tmpX = new List<float>(16);
        Vector2[] tmpPts = new Vector2[64];
        readonly Vector2[] quad = new Vector2[4];
        readonly Vector2[] lineBuf = new Vector2[2];

        public static readonly Color Out = new Color(0.2314f, 0.1647f, 0.1176f, 1f);   // #3b2a1e

        public Painter(int pixelsW, int pixelsH, float pixelsPerUnit, int supersample = 3)
        {
            W = pixelsW; H = pixelsH; ss = supersample;
            scale = pixelsPerUnit * ss;
            hw = W * ss; hh = H * ss;
            buf = new float[hw * hh * 4];
            mask = new bool[hw * hh];
            ResetDirty();
        }

        // ---------------------------------------------------------------- transformacion
        public void Push() { stack.Push(new[] { ma, mb, mc, md, mtx, mty }); }

        public void Pop()
        {
            float[] m = stack.Pop();
            ma = m[0]; mb = m[1]; mc = m[2]; md = m[3]; mtx = m[4]; mty = m[5];
        }

        /// <summary>Compone T(offset) * R(rot) * S(sx, sy) en el espacio actual.</summary>
        public void Transform(Vector2 offset, float rot, float sx, float sy)
        {
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            float la = cs * sx, lb = sn * sx, lc = -sn * sy, ld = cs * sy;
            float na = ma * la + mc * lb;
            float nb = mb * la + md * lb;
            float nc = ma * lc + mc * ld;
            float nd = mb * lc + md * ld;
            float ntx = ma * offset.x + mc * offset.y + mtx;
            float nty = mb * offset.x + md * offset.y + mty;
            ma = na; mb = nb; mc = nc; md = nd; mtx = ntx; mty = nty;
        }

        public void Transform(Vector2 offset, float rot, float s) { Transform(offset, rot, s, s); }

        float StrokeScale() { return Mathf.Sqrt(Mathf.Abs(ma * md - mb * mc)); }

        Vector2 Hi(Vector2 p)
        {
            float x = ma * p.x + mc * p.y + mtx;
            float y = mb * p.x + md * p.y + mty;
            return new Vector2(x * scale, y * scale);
        }

        // ---------------------------------------------------------------- mascara
        void ResetDirty() { mx0 = int.MaxValue; my0 = int.MaxValue; mx1 = -1; my1 = -1; }

        void MaskPoly(Vector2[] hp, int n)
        {
            if (n < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (hp[i].y < minY) minY = hp[i].y;
                if (hp[i].y > maxY) maxY = hp[i].y;
            }
            int y0 = Mathf.Max(0, Mathf.CeilToInt(minY - 0.5f));
            int y1 = Mathf.Min(hh - 1, Mathf.FloorToInt(maxY - 0.5f));
            for (int y = y0; y <= y1; y++)
            {
                float sy = y + 0.5f;
                tmpX.Clear();
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = hp[i];
                    Vector2 b = hp[(i + 1) % n];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                    {
                        float t = (sy - a.y) / (b.y - a.y);
                        tmpX.Add(a.x + t * (b.x - a.x));
                    }
                }
                if (tmpX.Count < 2) continue;
                tmpX.Sort();
                for (int k = 0; k + 1 < tmpX.Count; k += 2)
                {
                    int xa = Mathf.Max(0, Mathf.CeilToInt(tmpX[k] - 0.5f));
                    int xb = Mathf.Min(hw - 1, Mathf.FloorToInt(tmpX[k + 1] - 0.5f));
                    if (xb < xa) continue;
                    int row = y * hw;
                    for (int x = xa; x <= xb; x++) mask[row + x] = true;
                    if (xa < mx0) mx0 = xa;
                    if (xb > mx1) mx1 = xb;
                    if (y < my0) my0 = y;
                    if (y > my1) my1 = y;
                }
            }
        }

        void Commit(Color col)
        {
            if (my1 < 0 || mx1 < 0) { ResetDirty(); return; }
            float a = col.a;
            float pr = col.r * a, pg = col.g * a, pb = col.b * a;
            float inv = 1f - a;
            for (int y = my0; y <= my1; y++)
            {
                int row = y * hw;
                for (int x = mx0; x <= mx1; x++)
                {
                    int m = row + x;
                    if (!mask[m]) continue;
                    mask[m] = false;
                    int i = m * 4;
                    buf[i] = pr + buf[i] * inv;
                    buf[i + 1] = pg + buf[i + 1] * inv;
                    buf[i + 2] = pb + buf[i + 2] * inv;
                    buf[i + 3] = a + buf[i + 3] * inv;
                }
            }
            ResetDirty();
        }

        // ---------------------------------------------------------------- primitivas
        public void Poly(IList<Vector2> pts, Color col)
        {
            int n = pts.Count;
            if (n < 3) return;
            if (tmpPts.Length < n) tmpPts = new Vector2[n * 2];
            for (int i = 0; i < n; i++) tmpPts[i] = Hi(pts[i]);
            MaskPoly(tmpPts, n);
            Commit(col);
        }

        public void Circle(Vector2 c, float r, Color col)
        {
            Poly(CirclePts(c, r), col);
        }

        public void Ellipse(Vector2 c, Vector2 r, Color col, int seg = 24)
        {
            Poly(EllPts(c, r, seg), col);
        }

        /// <summary>Trazo de una polilinea con ancho w (unidades). round: uniones y puntas redondas.</summary>
        public void Stroke(IList<Vector2> pts, float w, Color col, bool closed = false, bool round = true)
        {
            int n = pts.Count;
            if (n < 2) return;
            float half = w * 0.5f;
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 1e-5f) continue;
                Vector2 nrm = new Vector2(-d.y, d.x) / len * half;
                quad[0] = Hi(a + nrm); quad[1] = Hi(b + nrm); quad[2] = Hi(b - nrm); quad[3] = Hi(a - nrm);
                MaskPoly(quad, 4);
            }
            if (round)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector2[] cp = CirclePts(pts[i], half);
                    int m = cp.Length;
                    if (tmpPts.Length < m) tmpPts = new Vector2[m * 2];
                    for (int k = 0; k < m; k++) tmpPts[k] = Hi(cp[k]);
                    MaskPoly(tmpPts, m);
                }
            }
            Commit(col);
        }

        public void Line(Vector2 a, Vector2 b, float w, Color col, bool round = true)
        {
            lineBuf[0] = a; lineBuf[1] = b;
            Stroke(lineBuf, w, col, false, round);
        }

        /// <summary>Arco (radianes, a0 a a1; y hacia abajo como Godot).</summary>
        public void Arc(Vector2 c, float r, float a0, float a1, float w, Color col, int seg = 16)
        {
            Vector2[] p = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, (float)i / seg);
                p[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            Stroke(p, w, col, false, true);
        }

        // ---------------------------------------------------------------- generadores de puntos
        public static Vector2[] EllPts(Vector2 c, Vector2 r, int seg = 24)
        {
            Vector2[] p = new Vector2[seg];
            for (int i = 0; i < seg; i++)
            {
                float t = Mathf.PI * 2f * i / seg;
                p[i] = c + new Vector2(Mathf.Cos(t) * r.x, Mathf.Sin(t) * r.y);
            }
            return p;
        }

        Vector2[] CirclePts(Vector2 c, float r)
        {
            float rp = r * StrokeScale() * scale;
            int seg = Mathf.Clamp(Mathf.CeilToInt(rp * 0.9f), 10, 72);
            return EllPts(c, new Vector2(r, r), seg);
        }

        /// <summary>Rectangulo redondeado (como Art.rr_pts).</summary>
        public static Vector2[] RoundRectPts(Rect r, float rad, int seg = 5)
        {
            rad = Mathf.Clamp(rad, 0.5f, Mathf.Max(0.5f, Mathf.Min(r.width, r.height) * 0.5f - 0.01f));
            List<Vector2> pts = new List<Vector2>();
            Vector2[] cs =
            {
                new Vector2(r.xMax - rad, r.yMin + rad), new Vector2(r.xMax - rad, r.yMax - rad),
                new Vector2(r.xMin + rad, r.yMax - rad), new Vector2(r.xMin + rad, r.yMin + rad),
            };
            float[] a0 = { -Mathf.PI / 2, 0f, Mathf.PI / 2, Mathf.PI };
            for (int k = 0; k < 4; k++)
            {
                for (int i = 0; i <= seg; i++)
                {
                    float a = a0[k] + Mathf.PI / 2 * i / seg;
                    Vector2 q = cs[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                    if (pts.Count == 0 || (pts[pts.Count - 1] - q).sqrMagnitude > 0.0001f) pts.Add(q);
                }
            }
            if (pts.Count > 2 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude <= 0.0001f) pts.RemoveAt(pts.Count - 1);
            return pts.ToArray();
        }

        public static Rect Grow(Rect r, float g) { return new Rect(r.x - g, r.y - g, r.width + 2 * g, r.height + 2 * g); }

        public static Vector2[] Xform(Vector2[] pts, Vector2 s, Vector2 off)
        {
            Vector2[] o = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) o[i] = new Vector2(pts[i].x * s.x + off.x, pts[i].y * s.y + off.y);
            return o;
        }

        // ---------------------------------------------------------------- formas con contorno (Art.*)
        public void OC(Vector2 c, float r, Color col, float ow = 3f)
        {
            Circle(c, r + ow, Out);
            Circle(c, r, col);
        }

        public void ORR(Rect rect, float rad, Color col, float ow = 3f)
        {
            Poly(RoundRectPts(Grow(rect, ow), rad + ow), Out);
            Poly(RoundRectPts(rect, rad), col);
        }

        public void OPoly(Vector2[] pts, Color col, float ow = 3f)
        {
            Poly(pts, col);
            Stroke(pts, ow, Out, true, true);
        }

        public void Star(Vector2 c, float s, Color col)
        {
            Vector2[] pts = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.PI / 4 * i - Mathf.PI / 2;
                float rr = i % 2 == 0 ? s : s * 0.28f;
                pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            }
            Poly(pts, col);
        }

        // ---------------------------------------------------------------- salida
        /// <summary>Reduce el buffer hi-res (promedio premultiplicado) y devuelve la textura (fila 0 abajo).</summary>
        public Texture2D ToTexture()
        {
            Color32[] px = ToPixels();
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Pixeles finales (con el supermuestreo resuelto), para copiarlos a un atlas.</summary>
        public Color32[] ToPixels()
        {
            Color32[] px = new Color32[W * H];
            float norm = 1f / (ss * ss);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        int row = ((y * ss + sy) * hw + x * ss) * 4;
                        for (int sx = 0; sx < ss; sx++)
                        {
                            int i = row + sx * 4;
                            r += buf[i]; g += buf[i + 1]; b += buf[i + 2]; a += buf[i + 3];
                        }
                    }
                    r *= norm; g *= norm; b *= norm; a *= norm;
                    Color32 c;
                    if (a <= 0.0005f) c = new Color32(0, 0, 0, 0);
                    else
                    {
                        float ia = 1f / a;
                        c = new Color32(
                            (byte)Mathf.Clamp(Mathf.RoundToInt(r * ia * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(g * ia * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(b * ia * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                    }
                    px[(H - 1 - y) * W + x] = c;
                }
            }
            return px;
        }
    }
}
