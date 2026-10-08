using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.World
{
    /// <summary>Icosferas unitarias (detalle 0 = 20 caras, detalle 1 = 80 caras).</summary>
    public static class Ico
    {
        public static readonly Vector3[] V0;
        public static readonly int[] F0;
        public static readonly Vector3[] V1;
        public static readonly int[] F1;

        static Ico()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            V0 = new Vector3[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < V0.Length; i++) V0[i] = V0[i].normalized;
            F0 = new int[]
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            // subdivision 1
            var verts = new List<Vector3>(V0);
            var faces = new List<int>();
            var cache = new Dictionary<long, int>();
            for (int f = 0; f < F0.Length; f += 3)
            {
                int a = F0[f], b = F0[f + 1], c = F0[f + 2];
                int ab = Mid(verts, cache, a, b), bc = Mid(verts, cache, b, c), ca = Mid(verts, cache, c, a);
                faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            V1 = verts.ToArray();
            F1 = faces.ToArray();
        }

        static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int idx;
            if (cache.TryGetValue(key, out idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            idx = verts.Count - 1;
            cache[key] = idx;
            return idx;
        }
    }

    /// <summary>
    /// Constructor de mallas con sombreado facetado (cada triangulo con sus propios vertices y normal).
    /// El color NO va en los vertices: cada triangulo cae en una submalla segun su "tono" (color cuantizado + nivel de emision)
    /// y cada submalla usa un material compartido del shader unico (WorldMaterials). Pocos tonos por malla = pocas llamadas de dibujo.
    /// Los triangulos se orientan segun una pista de direccion "hacia afuera" (hint), asi no importa el orden de los vertices.
    /// </summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>(4096);
        public readonly List<Vector3> N = new List<Vector3>(4096);
        public readonly List<int> T = new List<int>(8192);
        /// <summary>Matriz aplicada a todo lo que se agrega (identidad por defecto).</summary>
        public Matrix4x4 M = Matrix4x4.identity;

        readonly Dictionary<int, int> bucketOf = new Dictionary<int, int>();
        readonly List<int> bucketKey = new List<int>(16);
        readonly List<List<int>> buckets = new List<List<int>>(16);
        int used;

        List<int> Bucket(int key)
        {
            int bi;
            if (bucketOf.TryGetValue(key, out bi)) return buckets[bi];
            if (used == buckets.Count) buckets.Add(new List<int>(512));
            if (used == bucketKey.Count) bucketKey.Add(key); else bucketKey[used] = key;
            buckets[used].Clear();
            bucketOf[key] = used;
            return buckets[used++];
        }

        public int ToneCount { get { return used; } }

        public int TriCount { get { return T.Count / 3; } }

        public void Clear()
        {
            V.Clear(); N.Clear(); T.Clear();
            bucketOf.Clear();
            used = 0;
            M = Matrix4x4.identity;
        }

        public void ResetM() { M = Matrix4x4.identity; }

        public void SetTRS(Vector3 pos, float rotY, Vector3 scale)
        {
            M = Matrix4x4.TRS(pos, Quaternion.Euler(0f, rotY, 0f), scale);
        }

        public void SetTRS(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            M = Matrix4x4.TRS(pos, rot, scale);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 hint, Color col, float emis)
        {
            a = M.MultiplyPoint3x4(a);
            b = M.MultiplyPoint3x4(b);
            c = M.MultiplyPoint3x4(c);
            hint = M.MultiplyVector(hint);
            Vector3 n = Vector3.Cross(b - a, c - a);
            float len = n.magnitude;
            if (len < 1e-9f) return;
            n /= len;
            if (Vector3.Dot(n, hint) < 0f)
            {
                Vector3 t = b; b = c; c = t;
                n = -n;
            }
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            N.Add(n); N.Add(n); N.Add(n);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
            var bl = Bucket(WorldMaterials.Key(col, emis));
            bl.Add(i); bl.Add(i + 1); bl.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hint, Color col, float emis)
        {
            Tri(a, b, c, hint, col, emis);
            Tri(a, c, d, hint, col, emis);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hint, Color col0, Color col1, float emis)
        {
            Tri(a, b, c, hint, col0, emis);
            Tri(a, c, d, hint, col1, emis);
        }

        /// <summary>Caja alineada a los ejes locales.</summary>
        public void Box(Vector3 c, Vector3 size, Color col, float emis)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z);
            Vector3 p010 = c + new Vector3(-h.x, h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z);
            Vector3 p011 = c + new Vector3(-h.x, h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z);
            Quad(p000, p010, p110, p100, Vector3.back, col, emis);
            Quad(p001, p101, p111, p011, Vector3.forward, col, emis);
            Quad(p000, p001, p011, p010, Vector3.left, col, emis);
            Quad(p100, p110, p111, p101, Vector3.right, col, emis);
            Quad(p010, p011, p111, p110, Vector3.up, col, emis);
            Quad(p000, p100, p101, p001, Vector3.down, col, emis);
        }

        /// <summary>Octaedro (vetas y chispas de mineral).</summary>
        public void Octa(Vector3 c, Vector3 size, Color col, float emis)
        {
            Vector3 px = c + new Vector3(size.x, 0, 0), nx = c - new Vector3(size.x, 0, 0);
            Vector3 py = c + new Vector3(0, size.y, 0), ny = c - new Vector3(0, size.y, 0);
            Vector3 pz = c + new Vector3(0, 0, size.z), nz = c - new Vector3(0, 0, size.z);
            Vector3[] ring = { px, pz, nx, nz };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 4];
                Vector3 mid = (a + b) * 0.5f - c;
                Tri(py, a, b, mid + Vector3.up * 0.5f, col, emis);
                Tri(ny, a, b, mid - Vector3.up * 0.5f, col, emis);
            }
        }

        /// <summary>Esfera baja de poligonos deformada (copas de arboles, arbustos, guijarros, hongos).</summary>
        public void Blob(Vector3 center, Vector3 radii, int detail, int seed, float jitter, Color low, Color high, float emis, float clipY)
        {
            Vector3[] sv = detail > 0 ? Ico.V1 : Ico.V0;
            int[] sf = detail > 0 ? Ico.F1 : Ico.F0;
            Vector3[] p = scratch(sv.Length);
            for (int i = 0; i < sv.Length; i++)
            {
                Vector3 d = sv[i];
                float r = 1f + jitter * (Nz.Hash01(seed, i, 77) * 2f - 1f);
                Vector3 q = d * r;
                if (q.y < clipY) q.y = clipY;
                p[i] = center + new Vector3(q.x * radii.x, q.y * radii.y, q.z * radii.z);
            }
            for (int f = 0; f < sf.Length; f += 3)
            {
                Vector3 a = p[sf[f]], b = p[sf[f + 1]], c = p[sf[f + 2]];
                Vector3 cen = (a + b + c) / 3f;
                Vector3 outDir = cen - center;
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, outDir) < 0f) n = -n;
                // 3 tonos: sombra, base y luz segun hacia donde mira la cara (sin jitter continuo: pocos materiales)
                float t = Mathf.InverseLerp(-0.7f, 0.9f, n.y);
                Color col = t < 0.4f ? low : (t < 0.72f ? Color.Lerp(low, high, 0.5f) : high);
                Tri(a, b, c, outDir, col, emis);
            }
        }

        static Vector3[] scratchArr = new Vector3[64];
        static Vector3[] scratch(int n)
        {
            if (scratchArr.Length < n) scratchArr = new Vector3[n];
            return scratchArr;
        }

        /// <summary>Cristal: prisma de N lados con punta. up = direccion del eje, h = alto, w = radio de la base.</summary>
        public void Crystal(Vector3 baseC, Vector3 up, float h, float w, int sides, Color col, float emis)
        {
            up = up.normalized;
            Vector3 ax = Mathf.Abs(up.y) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 r1 = Vector3.Cross(up, ax).normalized;
            Vector3 r2 = Vector3.Cross(up, r1).normalized;
            Vector3 tip = baseC + up * h;
            Vector3 midC = baseC + up * (h * 0.72f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
                Vector3 d0 = r1 * Mathf.Cos(a0) + r2 * Mathf.Sin(a0);
                Vector3 d1 = r1 * Mathf.Cos(a1) + r2 * Mathf.Sin(a1);
                Vector3 b0 = baseC + d0 * w, b1 = baseC + d1 * w;
                Vector3 m0 = midC + d0 * (w * 0.92f), m1 = midC + d1 * (w * 0.92f);
                Vector3 outD = (d0 + d1) * 0.5f;
                Quad(b0, b1, m1, m0, outD, col, emis);
                Tri(m0, m1, tip, outD + up * 0.6f, Color.Lerp(col, Color.white, 0.22f), emis);
            }
        }

        /// <summary>Cilindro/cono truncado de A a B con radios r0 y r1.</summary>
        public void Cyl(Vector3 a, Vector3 b, float r0, float r1, int sides, Color col, float emis)
        {
            Vector3 up = (b - a);
            float len = up.magnitude;
            if (len < 1e-5f) return;
            up /= len;
            Vector3 ax = Mathf.Abs(up.y) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 u1 = Vector3.Cross(up, ax).normalized;
            Vector3 u2 = Vector3.Cross(up, u1).normalized;
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
                Vector3 d0 = u1 * Mathf.Cos(a0) + u2 * Mathf.Sin(a0);
                Vector3 d1 = u1 * Mathf.Cos(a1) + u2 * Mathf.Sin(a1);
                Vector3 outD = (d0 + d1) * 0.5f;
                Quad(a + d0 * r0, a + d1 * r0, b + d1 * r1, b + d0 * r1, outD, col, emis);
                if (r1 > 0.001f) Tri(b, b + d0 * r1, b + d1 * r1, up, col, emis);
            }
        }

        /// <summary>Disco plano horizontal.</summary>
        public void Disc(Vector3 c, float r, int sides, Color col, float emis)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
                Tri(c, c + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), c + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r), Vector3.up, col, emis);
            }
        }

        /// <summary>
        /// Agrega una malla externa (p. ej. ArtKit.CrystalMesh / GemMesh) transformada por m con un tono dado.
        /// Conserva sus normales; si no tiene, las calcula por triangulo. Una malla vacia no hace nada.
        /// </summary>
        public bool AddMesh(Mesh src, Matrix4x4 m, Color col, float emis)
        {
            if (src == null || src.vertexCount == 0 || !src.isReadable) return false;
            Vector3[] sv = src.vertices;
            Vector3[] sn = src.normals;
            int[] st = src.triangles;
            if (st.Length < 3) return false;
            Matrix4x4 full = M * m;
            Matrix4x4 nm = full.inverse.transpose;
            int baseI = V.Count;
            bool hasN = sn != null && sn.Length == sv.Length;
            for (int i = 0; i < sv.Length; i++)
            {
                V.Add(full.MultiplyPoint3x4(sv[i]));
                Vector3 n = hasN ? nm.MultiplyVector(sn[i]).normalized : Vector3.up;
                N.Add(n);
            }
            var bl = Bucket(WorldMaterials.Key(col, emis));
            for (int i = 0; i < st.Length; i++)
            {
                T.Add(baseI + st[i]);
                bl.Add(baseI + st[i]);
            }
            if (!hasN)
            {
                for (int i = 0; i < st.Length; i += 3)
                {
                    Vector3 a = V[baseI + st[i]], b = V[baseI + st[i + 1]], c = V[baseI + st[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                    N[baseI + st[i]] = n; N[baseI + st[i + 1]] = n; N[baseI + st[i + 2]] = n;
                }
            }
            return true;
        }

        /// <summary>Vuelca el contenido en una malla (la limpia antes) con una submalla por tono. mats recibe sus materiales compartidos.</summary>
        public Mesh ToMesh(Mesh mesh, string name, out Material[] mats)
        {
            if (mesh == null) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            int n = Mathf.Max(1, used);
            mesh.subMeshCount = n;
            mats = new Material[n];
            if (used == 0)
            {
                mats[0] = WorldMaterials.FromKey(WorldMaterials.Key(Color.white, 0f));
            }
            for (int i = 0; i < used; i++)
            {
                mesh.SetTriangles(buckets[i], i, false);
                mats[i] = WorldMaterials.FromKey(bucketKey[i]);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Material de cada submalla (en orden) del ultimo contenido, sin volcar la malla.</summary>
        public Material[] CurrentMats()
        {
            var mats = new Material[Mathf.Max(1, used)];
            for (int i = 0; i < used; i++) mats[i] = WorldMaterials.FromKey(bucketKey[i]);
            if (used == 0) mats[0] = WorldMaterials.FromKey(WorldMaterials.Key(Color.white, 0f));
            return mats;
        }
    }
}
