using Mineros.Core;
using Mineros.World;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Suelo pintado de la isla (una textura de 2048 px sobre la cara de arriba): pasto con manchas suaves, plaza de
    /// tierra alrededor del deposito, caminitos curvos de borde difuminado hacia cada parcela y, en las parcelas
    /// construidas, un lote de tierra con borde de piedritas. Los caminos a parcelas sin construir se ven apenas
    /// pisados; al construir se repinta solo esa zona y el camino "aparece" en unos pocos pasos.
    /// </summary>
    public sealed class IslandGround
    {
        const int N = 2048;
        const float Half = 21f;                   // metros: cubre la isla mas grande (18 m + playa)
        const float Px = N / (Half * 2f);         // pixeles por metro
        readonly Texture2D tex;
        static Color32[] baseCol;   // el pasto de base no depende del estado: se calcula una sola vez
        readonly Color32[] outCol;
        readonly byte[] dirt, stone;
        readonly Island isl;
        public Transform T { get; private set; }

        static Color DirtA = Biomes.H("e2c48e"), DirtB = Biomes.H("cfa96f"), DirtEdge = Biomes.H("b58d58");
        /// <summary>Tierra de los caminos (para montículos, manchas y trozos que combinen con el suelo).</summary>
        public static Color Dirt { get { return DirtB; } }
        public static Color DirtD { get { return DirtEdge; } }

        /// <summary>Tierra de la isla nueva; el pasto de base se vuelve a pintar con los colores del bioma.</summary>
        public static void SetDirt(Color a, Color b, Color edge)
        {
            DirtA = a; DirtB = b; DirtEdge = edge;
            baseCol = null;
        }
        static readonly Color StoneA = Biomes.H("c9c3b8"), StoneB = Biomes.H("a29b90");

        public IslandGround(Transform parent, Island isl, float radius)
        {
            this.isl = isl;
            tex = new Texture2D(N, N, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4, name = "SueloIsla" };
            outCol = new Color32[N * N];
            dirt = new byte[N * N];
            stone = new byte[N * N];
            if (baseCol == null) { baseCol = new Color32[N * N]; PaintBase(); }
            Repaint();
            T = MakeTop(parent, radius);
        }

        // ------------------------------------------------------------ malla de la cara de arriba (UV = xz)
        Transform MakeTop(Transform parent, float radius)
        {
            const int S = 96;
            float r = radius + 0.6f;
            var v = new Vector3[S + 1]; var n = new Vector3[S + 1]; var uv = new Vector2[S + 1];
            var tri = new int[S * 3];
            v[0] = Vector3.zero; n[0] = Vector3.up; uv[0] = UV(0, 0);
            for (int i = 0; i < S; i++)
            {
                float a = Mathf.PI * 2f * i / S;
                float rr = IslandArt.EdgeR(a, r);
                float x = Mathf.Cos(a) * rr, z = Mathf.Sin(a) * rr;
                v[i + 1] = new Vector3(x, 0f, z); n[i + 1] = Vector3.up; uv[i + 1] = UV(x, z);
                tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % S; tri[i * 3 + 2] = 1 + i;
            }
            var m = new Mesh { name = "Suelo" };
            m.vertices = v; m.normals = n; m.uv = uv; m.triangles = tri;
            m.RecalculateBounds();
            var mat = new Material(Shader.Find("Mineros/MinerToonTex")) { name = "Suelo", mainTexture = tex };
            mat.SetFloat("_Rim", 0f);
            mat.SetFloat("_Floor", 0.62f);
            var rend = IslandArt.MakeRenderer(parent, "Suelo", m, new[] { mat }, false);
            rend.receiveShadows = true;
            rend.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            return rend.transform;
        }

        static Vector2 UV(float x, float z) { return new Vector2((x + Half) / (Half * 2f), (z + Half) / (Half * 2f)); }

        // ------------------------------------------------------------ pasto de base (una vez)
        static float Hash(int x, int y) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h & 0xffff) / 65535f; } }

        static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static void PaintBase()
        {
            Color g0 = IslandArt.Grass, g1 = IslandArt.GrassD, g2 = Color.Lerp(IslandArt.Grass, Biomes.H("b5d65a"), 0.6f);
            for (int y = 0; y < N; y++)
            {
                float wz = y / Px - Half;
                for (int x = 0; x < N; x++)
                {
                    float wx = x / Px - Half;
                    // manchas grandes y suaves + variacion fina
                    float big = Noise(wx * 0.32f + 11f, wz * 0.32f + 7f);
                    float mid = Noise(wx * 1.1f, wz * 1.1f);
                    Color c = Color.Lerp(g0, g1, Mathf.SmoothStep(0.45f, 0.8f, big) * 0.55f);
                    c = Color.Lerp(c, g2, Mathf.SmoothStep(0.55f, 0.85f, mid) * 0.35f);
                    float fine = Hash(x, y) * 0.04f - 0.02f;
                    baseCol[y * N + x] = new Color(c.r + fine, c.g + fine, c.b + fine * 0.5f);
                }
            }
        }

        // ------------------------------------------------------------ caminos y lotes
        /// <summary>Repinta todo (al expandir o al cargar).</summary>
        public void Repaint(int skipPlot = -1)
        {
            System.Array.Clear(dirt, 0, dirt.Length);
            System.Array.Clear(stone, 0, stone.Length);
            // sin aros: la plaza es tierra con borde suave y solo hay caminos hacia lo construido (0.9.2: el mundo respira)
            Disc(0f, 0f, Island.PlazaR, 1f, 0.45f);
            foreach (var l in isl.Paths)
            {
                var p = isl.Plots[l.Plot];
                if (p.Ring > isl.Expand || p.Building < 0 || p.Id == skipPlot) continue;
                StampPath(l, 1f, 1f);
            }
            foreach (var p in isl.Plots)
                if (p.Id != 0 && p.Building >= 0 && p.Ring <= isl.Expand && p.Id != skipPlot) Lot(p);
            Compose(0, 0, N, N);
            tex.SetPixels32(outCol);
            tex.Apply(false);
        }

        /// <summary>Camino de una parcela recien construida: aparece de a poco (f = 0..1 del largo).</summary>
        public void RevealPath(int plotId, float f)
        {
            PathLine line = null;
            foreach (var l in isl.Paths) if (l.Plot == plotId) { line = l; break; }
            if (line == null) return;
            StampPath(line, 1f, f);
            if (f >= 1f) Lot(isl.Plots[plotId]);
            int x0 = N, y0 = N, x1 = 0, y1 = 0;
            for (int i = 0; i < line.X.Length; i++) Grow(line.X[i], line.Z[i], 1.2f, ref x0, ref y0, ref x1, ref y1);
            var pl = isl.Plots[plotId];
            Grow(pl.X, pl.Z, 2.8f, ref x0, ref y0, ref x1, ref y1);
            Compose(x0, y0, x1, y1);
            tex.SetPixels32(outCol);
            tex.Apply(false);
        }

        static void Grow(float wx, float wz, float r, ref int x0, ref int y0, ref int x1, ref int y1)
        {
            x0 = Mathf.Max(0, Mathf.Min(x0, (int)((wx - r + Half) * Px)));
            y0 = Mathf.Max(0, Mathf.Min(y0, (int)((wz - r + Half) * Px)));
            x1 = Mathf.Min(N, Mathf.Max(x1, (int)((wx + r + Half) * Px) + 1));
            y1 = Mathf.Min(N, Mathf.Max(y1, (int)((wz + r + Half) * Px) + 1));
        }

        void StampPath(PathLine l, float strength, float upTo)
        {
            float total = 0f;
            for (int i = 0; i < l.X.Length - 1; i++) total += Vector2.Distance(new Vector2(l.X[i], l.Z[i]), new Vector2(l.X[i + 1], l.Z[i + 1]));
            float limit = total * Mathf.Clamp01(upTo), run = 0f;
            for (int i = 0; i < l.X.Length - 1; i++)
            {
                Vector2 a = new Vector2(l.X[i], l.Z[i]), b = new Vector2(l.X[i + 1], l.Z[i + 1]);
                float seg = Vector2.Distance(a, b);
                for (float s = 0f; s < seg; s += 0.12f)
                {
                    if (run + s > limit) return;
                    Vector2 p = Vector2.Lerp(a, b, s / seg);
                    // ancho que respira un poco a lo largo del camino
                    float w = Island.PathHalf * (0.92f + 0.12f * Mathf.Sin((run + s) * 1.7f + l.Plot));
                    Disc(p.x, p.y, w, strength, 0.3f);
                }
                run += seg;
            }
        }

        /// <summary>Lote de la parcela construida: tierra pareja con borde de piedritas.</summary>
        void Lot(Plot p)
        {
            Disc(p.X, p.Z, 1.55f, 0.55f, 0.45f);
        }

        void Disc(float wx, float wz, float r, float strength, float soft)
        {
            int cx = (int)((wx + Half) * Px), cy = (int)((wz + Half) * Px);
            int rp = Mathf.CeilToInt((r + soft) * Px);
            byte sv = (byte)(strength * 255);
            for (int y = Mathf.Max(0, cy - rp); y < Mathf.Min(N, cy + rp); y++)
                for (int x = Mathf.Max(0, cx - rp); x < Mathf.Min(N, cx + rp); x++)
                {
                    float dx = (x - cx) / Px, dy = (y - cy) / Px;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > r + soft) continue;
                    float k = 1f - Mathf.SmoothStep(r - soft, r + soft, d);
                    byte v = (byte)(k * sv);
                    int i = y * N + x;
                    if (v > dirt[i]) dirt[i] = v;
                }
        }

        void Ring(float wx, float wz, float r, float w)
        {
            int cx = (int)((wx + Half) * Px), cy = (int)((wz + Half) * Px);
            int rp = Mathf.CeilToInt((r + w) * Px);
            for (int y = Mathf.Max(0, cy - rp); y < Mathf.Min(N, cy + rp); y++)
                for (int x = Mathf.Max(0, cx - rp); x < Mathf.Min(N, cx + rp); x++)
                {
                    float dx = (x - cx) / Px, dy = (y - cy) / Px;
                    float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r);
                    if (d > w * 0.5f + 0.03f) continue;
                    float k = 1f - Mathf.SmoothStep(w * 0.5f - 0.03f, w * 0.5f + 0.03f, d);
                    byte v = (byte)(k * 255);
                    int i = y * N + x;
                    if (v > stone[i]) stone[i] = v;
                }
        }

        void Compose(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y < y1; y++)
            {
                float wz = y / Px - Half;
                for (int x = x0; x < x1; x++)
                {
                    int i = y * N + x;
                    Color32 g = baseCol[i];
                    float m = dirt[i] / 255f, st = stone[i] / 255f;
                    if (m <= 0.001f && st <= 0.001f) { outCol[i] = g; continue; }
                    float wx = x / Px - Half;
                    // tierra: clara al centro, tono que varia, piedritas sueltas; borde un poco mas oscuro
                    float n = Noise(wx * 2.2f + 3f, wz * 2.2f);
                    Color d = Color.Lerp(DirtA, DirtB, n * 0.8f);
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(m - 0.45f) * 3.2f);
                    d = Color.Lerp(d, DirtEdge, edge * 0.45f);
                    if (Hash(x / 6, y / 6) > 0.965f && m > 0.6f) d = Color.Lerp(d, StoneB, 0.6f);
                    Color c = Color.Lerp(g, d, Mathf.SmoothStep(0f, 1f, m));
                    if (st > 0f)
                    {
                        // borde de piedritas: celdas redondeadas claras con junta oscura
                        float cell = Noise(wx * 7f, wz * 7f);
                        Color s = Color.Lerp(StoneA, StoneB, cell);
                        float joint = Mathf.SmoothStep(0.35f, 0.5f, Mathf.Abs(Noise(wx * 9f + 5f, wz * 9f) - 0.5f) * 2f);
                        s = Color.Lerp(s, StoneB * 0.8f, joint * 0.5f);
                        c = Color.Lerp(c, s, st);
                    }
                    outCol[i] = c;
                }
            }
        }
    }
}
