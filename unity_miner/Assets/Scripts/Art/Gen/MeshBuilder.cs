using System;
using System.Collections.Generic;

namespace Mineros.Art.Gen
{
    /// <summary>Descripcion de un material del kit: color sRGB, brillo especular y emision (sin iluminar).</summary>
    public sealed class MatSpec
    {
        public Rgb color;
        public float spec;
        public Rgb emis;
        public MatSpec(Rgb c, float spec, Rgb emis) { color = c; this.spec = spec; this.emis = emis; }
    }

    /// <summary>Resultado final de un accesorio: una sola malla con una submalla por material (ya combinada).</summary>
    public sealed class MeshData
    {
        public V3[] verts;
        public V3[] normals;
        public int[][] tris;      // una lista de indices por material
        public MatSpec[] mats;
        /// <summary>Punto de luz/brillo sugerido (farol, lava, cristales) en local; lo usan ArtKit y el mundo.</summary>
        public bool hasAnchor;
        public V3 anchor;
        public int TriCount
        {
            get { int n = 0; foreach (var t in tris) n += t.Length / 3; return n; }
        }
    }

    /// <summary>
    /// Acumulador de geometria: todas las primitivas escriben aca (con la transformacion vigente) y el resultado sale como
    /// UNA malla con submallas por material. Es el equivalente directo de combinar piezas con Mesh.CombineMeshes, pero sin
    /// crear mallas intermedias ni depender de UnityEngine.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<V3> verts = new List<V3>();
        readonly List<V3> norms = new List<V3>();
        readonly List<List<int>> tris = new List<List<int>>();
        readonly List<MatSpec> mats = new List<MatSpec>();
        readonly Stack<Xf> stack = new Stack<Xf>();
        Xf cur = Xf.Identity;
        bool flip;

        public Xf Cur { get { return cur; } }
        public bool HasAnchor;
        public V3 Anchor;

        // ------------------------------------------------------------ materiales
        /// <summary>Registra (o reutiliza) un material y devuelve su indice de submalla.</summary>
        public int Mat(Rgb c, float spec = 0f, Rgb emis = default(Rgb))
        {
            for (int i = 0; i < mats.Count; i++)
            {
                var m = mats[i];
                if (Near(m.color, c) && Math.Abs(m.spec - spec) < 1e-4f && Near(m.emis, emis)) return i;
            }
            mats.Add(new MatSpec(c, spec, emis));
            tris.Add(new List<int>());
            return mats.Count - 1;
        }
        static bool Near(Rgb a, Rgb b) { return Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) < 1e-4f; }

        /// <summary>Los 3 tonos del estilo (sombra, base, luz) a partir de un color base. emisK > 0 agrega emision suave
        /// del propio color (cristales, hongos).</summary>
        public int[] Tones(Rgb c, float spec = 0f, float emisK = 0f)
        {
            return new[]
            {
                Mat(c.Darken(0.22f), spec, c.Darken(0.22f).Mul(emisK)),
                Mat(c, spec, c.Mul(emisK)),
                Mat(c.Lighten(0.2f), spec, c.Lighten(0.2f).Mul(emisK)),
            };
        }

        // ------------------------------------------------------------ transformaciones
        public void Push(Xf x)
        {
            stack.Push(cur);
            cur = Xf.Mul(cur, x);
            flip = cur.Det < 0;
        }
        public void Pop()
        {
            cur = stack.Pop();
            flip = cur.Det < 0;
        }

        // ------------------------------------------------------------ bajo nivel
        public int Vert(V3 p, V3 n)
        {
            verts.Add(cur.Point(p));
            norms.Add(cur.Normal(n));
            return verts.Count - 1;
        }

        public void Tri(int slot, int a, int b, int c)
        {
            var l = tris[slot];
            if (flip) { l.Add(a); l.Add(c); l.Add(b); }
            else { l.Add(a); l.Add(b); l.Add(c); }
        }

        /// <summary>Triangulo con normal plana (cara facetada). Posiciones locales. Devuelve false si es degenerado.</summary>
        public bool FlatTri(int slot, V3 a, V3 b, V3 c)
        {
            var n = V3.Cross(b - a, c - a);
            if (V3.Dot(n, n) < 1e-14f) return false;
            n = n.Normalized;
            int ia = Vert(a, n), ib = Vert(b, n), ic = Vert(c, n);
            Tri(slot, ia, ib, ic);
            return true;
        }

        /// <summary>Triangulo plano orientado hacia afuera respecto de un centro (para solidos convexos).</summary>
        public void FlatTriOut(int slot, V3 a, V3 b, V3 c, V3 center)
        {
            var n = V3.Cross(b - a, c - a);
            var mid = (a + b + c) * (1f / 3f);
            if (V3.Dot(n, mid - center) < 0) { var t = b; b = c; c = t; }
            FlatTri(slot, a, b, c);
        }

        public void FlatQuadOut(int slot, V3 a, V3 b, V3 c, V3 d, V3 center)
        {
            FlatTriOut(slot, a, b, c, center);
            FlatTriOut(slot, a, c, d, center);
        }

        /// <summary>
        /// Malla de rejilla p[fila, columna]. Orientacion: la normal sale de (dFila x dColumna). wrapU cierra las columnas.
        /// flat: una normal por triangulo. Si no, normales suaves soldadas por posicion (polos y costuras incluidos),
        /// o las dadas en nrm.
        /// </summary>
        public void Grid(int slot, V3[,] p, bool wrapU, bool flat, V3[,] nrm = null)
        {
            int R = p.GetLength(0), C = p.GetLength(1);
            int cmax = wrapU ? C : C - 1;
            if (flat)
            {
                for (int i = 0; i < R - 1; i++)
                    for (int j = 0; j < cmax; j++)
                    {
                        int j1 = (j + 1) % C;
                        FlatTri(slot, p[i, j], p[i + 1, j], p[i + 1, j1]);
                        FlatTri(slot, p[i, j], p[i + 1, j1], p[i, j1]);
                    }
                return;
            }

            // normales suaves soldadas
            var ids = new int[R, C];
            var keyToId = new Dictionary<(int, int, int), int>();
            var acc = new List<V3>();
            for (int i = 0; i < R; i++)
                for (int j = 0; j < C; j++)
                {
                    var q = p[i, j];
                    var key = ((int)Math.Round(q.x * 2000.0), (int)Math.Round(q.y * 2000.0), (int)Math.Round(q.z * 2000.0));
                    int id;
                    if (!keyToId.TryGetValue(key, out id)) { id = acc.Count; acc.Add(V3.Zero); keyToId[key] = id; }
                    ids[i, j] = id;
                }
            for (int i = 0; i < R - 1; i++)
                for (int j = 0; j < cmax; j++)
                {
                    int j1 = (j + 1) % C;
                    AccTri(acc, ids, p, i, j, i + 1, j, i + 1, j1);
                    AccTri(acc, ids, p, i, j, i + 1, j1, i, j1);
                }
            var emitted = new int[R, C];
            for (int i = 0; i < R; i++) for (int j = 0; j < C; j++) emitted[i, j] = -1;
            for (int i = 0; i < R - 1; i++)
                for (int j = 0; j < cmax; j++)
                {
                    int j1 = (j + 1) % C;
                    EmitSmooth(slot, p, nrm, acc, ids, emitted, i, j, i + 1, j, i + 1, j1);
                    EmitSmooth(slot, p, nrm, acc, ids, emitted, i, j, i + 1, j1, i, j1);
                }
        }

        static void AccTri(List<V3> acc, int[,] ids, V3[,] p, int i0, int j0, int i1, int j1, int i2, int j2)
        {
            var a = p[i0, j0]; var b = p[i1, j1]; var c = p[i2, j2];
            var n = V3.Cross(b - a, c - a);
            int x = ids[i0, j0], y = ids[i1, j1], z = ids[i2, j2];
            acc[x] = acc[x] + n; acc[y] = acc[y] + n; acc[z] = acc[z] + n;
        }

        void EmitSmooth(int slot, V3[,] p, V3[,] nrm, List<V3> acc, int[,] ids, int[,] emitted,
            int i0, int j0, int i1, int j1, int i2, int j2)
        {
            var n = V3.Cross(p[i1, j1] - p[i0, j0], p[i2, j2] - p[i0, j0]);
            if (V3.Dot(n, n) < 1e-14f) return;
            int a = GetV(p, nrm, acc, ids, emitted, i0, j0);
            int b = GetV(p, nrm, acc, ids, emitted, i1, j1);
            int c = GetV(p, nrm, acc, ids, emitted, i2, j2);
            Tri(slot, a, b, c);
        }

        int GetV(V3[,] p, V3[,] nrm, List<V3> acc, int[,] ids, int[,] emitted, int i, int j)
        {
            if (emitted[i, j] >= 0) return emitted[i, j];
            V3 n = nrm != null ? nrm[i, j] : acc[ids[i, j]].Normalized;
            int v = Vert(p[i, j], n);
            emitted[i, j] = v;
            return v;
        }

        // ------------------------------------------------------------ salida
        public MeshData Build()
        {
            var d = new MeshData();
            d.hasAnchor = HasAnchor;
            d.anchor = Anchor;
            d.verts = verts.ToArray();
            d.normals = norms.ToArray();
            var ml = new List<MatSpec>();
            var tl = new List<int[]>();
            for (int i = 0; i < tris.Count; i++)
            {
                if (tris[i].Count == 0) continue; // submallas vacias fuera
                ml.Add(mats[i]);
                tl.Add(tris[i].ToArray());
            }
            d.mats = ml.ToArray();
            d.tris = tl.ToArray();
            return d;
        }
    }
}
