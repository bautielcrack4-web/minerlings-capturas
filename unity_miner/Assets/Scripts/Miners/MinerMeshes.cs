using System.Collections.Generic;
using UnityEngine;

namespace Mineros.Miners
{
    /// <summary>
    /// Generador de mallas procedurales (esfera, capsula, cilindro/cono, caja, hemisferio) con normales suaves.
    /// Todas se construyen por revolucion alrededor de Y; el angulo 0 apunta a +Z (el frente del minero).
    /// Las mallas se cachean por parametros y se comparten entre mineros.
    /// Convencion de caras: sentido horario visto desde afuera (Unity).
    /// </summary>
    public static class MinerMeshes
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        struct P
        {
            public float r, y, nr, ny;
            public P(float r, float y, float nr, float ny) { this.r = r; this.y = y; this.nr = nr; this.ny = ny; }
        }

        static Mesh Get(string key, System.Func<Mesh> make)
        {
            Mesh m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = make();
            m.name = key;
            m.hideFlags = HideFlags.DontSave;
            Cache[key] = m;
            return m;
        }

        static string K(string kind, int segs, params float[] a)
        {
            var sb = new System.Text.StringBuilder(kind);
            sb.Append('_').Append(segs);
            for (int i = 0; i < a.Length; i++)
                sb.Append('_').Append(a[i].ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>Esfera de radio r.</summary>
        public static Mesh Sphere(float r, int segs = 24)
        {
            return Get(K("sph", segs, r), () =>
            {
                var prof = new List<P>();
                int rings = Mathf.Max(4, segs / 2);
                for (int j = 0; j <= rings; j++)
                {
                    float a = -Mathf.PI * 0.5f + Mathf.PI * j / rings;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    prof.Add(new P(c * r, s * r, c, s));
                }
                return Revolve(prof, segs);
            });
        }

        /// <summary>Hemisferio superior (sin tapa inferior) de radio r.</summary>
        public static Mesh Dome(float r, int segs = 24)
        {
            return Get(K("dome", segs, r), () =>
            {
                var prof = new List<P>();
                int rings = Mathf.Max(3, segs / 4);
                for (int j = 0; j <= rings; j++)
                {
                    float a = Mathf.PI * 0.5f * j / rings;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    prof.Add(new P(c * r, s * r, c, s));
                }
                return Revolve(prof, segs);
            });
        }

        /// <summary>Capsula de radio r y altura total h (incluye las tapas), centrada en el origen.</summary>
        public static Mesh Capsule(float r, float h, int segs = 24)
        {
            return Get(K("cap", segs, r, h), () =>
            {
                var prof = new List<P>();
                int rings = Mathf.Max(3, segs / 4);
                float half = Mathf.Max(0f, h * 0.5f - r);
                for (int j = 0; j <= rings; j++)
                {
                    float a = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * j / rings;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    prof.Add(new P(c * r, -half + s * r, c, s));
                }
                for (int j = 0; j <= rings; j++)
                {
                    float a = Mathf.PI * 0.5f * j / rings;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    prof.Add(new P(c * r, half + s * r, c, s));
                }
                return Revolve(prof, segs);
            });
        }

        /// <summary>Cilindro/cono truncado con tapas: rt radio superior (puede ser 0), rb inferior, h altura, centrado.</summary>
        public static Mesh Cylinder(float rt, float rb, float h, int segs = 24)
        {
            return Get(K("cyl", segs, rt, rb, h), () =>
            {
                var prof = new List<P>();
                float hh = h * 0.5f;
                float sn = h, sy = rb - rt; // normal lateral en (radio, y)
                float l = Mathf.Sqrt(sn * sn + sy * sy);
                if (l < 1e-6f) l = 1f;
                sn /= l; sy /= l;
                if (rb > 0f)
                {
                    prof.Add(new P(0f, -hh, 0f, -1f));
                    prof.Add(new P(rb, -hh, 0f, -1f));
                }
                prof.Add(new P(rb, -hh, sn, sy));
                prof.Add(new P(rt, hh, sn, sy));
                if (rt > 0f)
                {
                    prof.Add(new P(rt, hh, 0f, 1f));
                    prof.Add(new P(0f, hh, 0f, 1f));
                }
                return Revolve(prof, segs);
            });
        }

        /// <summary>Caja de tamano size centrada en el origen.</summary>
        public static Mesh Box(Vector3 size)
        {
            return Get(K("box", 0, size.x, size.y, size.z), () =>
            {
                var vs = new List<Vector3>();
                var ns = new List<Vector3>();
                var ts = new List<int>();
                Vector3 e = size * 0.5f;
                Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
                for (int ax = 0; ax < 3; ax++)
                {
                    for (int sg = -1; sg <= 1; sg += 2)
                    {
                        Vector3 n = axes[ax] * sg;
                        Vector3 u = axes[(ax + 1) % 3], v = axes[(ax + 2) % 3];
                        Vector3 c = Vector3.Scale(n, e);
                        Vector3 eu = Vector3.Scale(u, e), ev = Vector3.Scale(v, e);
                        Vector3 p0 = c - eu - ev, p1 = c - eu + ev, p2 = c + eu + ev, p3 = c + eu - ev;
                        bool ok = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) > 0f;
                        int b = vs.Count;
                        vs.Add(p0); vs.Add(p1); vs.Add(p2); vs.Add(p3);
                        for (int i = 0; i < 4; i++) ns.Add(n);
                        if (ok) { ts.Add(b); ts.Add(b + 1); ts.Add(b + 2); ts.Add(b); ts.Add(b + 2); ts.Add(b + 3); }
                        else { ts.Add(b); ts.Add(b + 2); ts.Add(b + 1); ts.Add(b); ts.Add(b + 3); ts.Add(b + 2); }
                    }
                }
                var m = new Mesh();
                m.SetVertices(vs);
                m.SetNormals(ns);
                m.SetTriangles(ts, 0);
                m.RecalculateBounds();
                return m;
            });
        }

        static Mesh Revolve(List<P> prof, int segs)
        {
            int cols = segs + 1, rows = prof.Count;
            var vs = new Vector3[cols * rows];
            var ns = new Vector3[cols * rows];
            var uv = new Vector2[cols * rows];
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    float a = Mathf.PI * 2f * i / segs;
                    float sa = Mathf.Sin(a), ca = Mathf.Cos(a);
                    P p = prof[j];
                    int idx = j * cols + i;
                    vs[idx] = new Vector3(p.r * sa, p.y, p.r * ca);
                    Vector3 n = new Vector3(p.nr * sa, p.ny, p.nr * ca);
                    ns[idx] = n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.up;
                    uv[idx] = new Vector2((float)i / segs, rows > 1 ? (float)j / (rows - 1) : 0f);
                }
            }
            var tris = new int[segs * (rows - 1) * 6];
            int t = 0;
            for (int j = 0; j < rows - 1; j++)
            {
                for (int i = 0; i < segs; i++)
                {
                    int a = j * cols + i, b = a + 1, c = a + cols, d = c + 1;
                    tris[t++] = a; tris[t++] = d; tris[t++] = c;
                    tris[t++] = a; tris[t++] = b; tris[t++] = d;
                }
            }
            var m = new Mesh();
            m.vertices = vs;
            m.normals = ns;
            m.uv = uv;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }
    }
}
