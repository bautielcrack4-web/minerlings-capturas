using System.Collections.Generic;
using Mineros.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.World
{
    /// <summary>
    /// Cañon diagonal en 3D generado por tramos (chunks de 10 m) que se crean delante de la camara y se reciclan detras:
    /// pasillo con manchas de tono, meseta alta con pared de franjas y labio redondeado con festones, desnivel con pared que
    /// cae, accesorios por bioma (ArtKit.Prop) y decoracion menuda. El terreno es una malla facetada por tramo con una submalla
    /// por tono (materiales del shader unico); los accesorios del kit se funden en una segunda malla por material.
    /// Tambien arma la meseta circular del modo Excavar.
    /// </summary>
    public sealed class CanyonBuilder
    {
        public const float L = 20f;
        const float CellProp = 120f * W.PX;
        const float CellDecor = 96f * W.PX;

        sealed class Chunk
        {
            public int idx;
            public GameObject go;
            public MeshFilter mf;
            public MeshRenderer mr;
            public Mesh mesh;
            public Transform props;
            public MeshFilter propMf;
            public MeshRenderer propMr;
            public Mesh propMesh;
            public readonly List<GlowSpot> glows = new List<GlowSpot>();
        }

        readonly Transform root;
        readonly MeshBuilder mb = new MeshBuilder();
        readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
        readonly Stack<Chunk> pool = new Stack<Chunk>();
        readonly List<int> tmpKeys = new List<int>();
        BiomePal pal;
        int biome = -1;
        GameObject arenaGo;
        MeshFilter arenaMf, arenaPropMf;
        MeshRenderer arenaMr, arenaPropMr;
        Transform arenaProps;
        Mesh arenaMesh, arenaPropMesh;
        readonly List<GlowSpot> arenaGlows = new List<GlowSpot>();
        bool arenaMode;
        bool dirtyGlows = true;

        /// <summary>Puntos luminosos de los tramos visibles (o de la arena).</summary>
        public readonly List<GlowSpot> Glows = new List<GlowSpot>(64);
        /// <summary>true = menos accesorios y decoracion (modo Eco).</summary>
        public bool Eco;

        public CanyonBuilder(Transform parent)
        {
            root = new GameObject("Canyon").transform;
            root.SetParent(parent, false);
        }

        public void SetBiome(int b)
        {
            pal = Biomes.Get(b);
            biome = b;
            ClearChunks();
            ClearArena();
        }

        public void ClearChunks()
        {
            foreach (var kv in chunks) Recycle(kv.Value);
            chunks.Clear();
            Glows.Clear();
            dirtyGlows = true;
        }

        public void ClearArena()
        {
            arenaMode = false;
            if (arenaGo != null) arenaGo.SetActive(false);
            arenaGlows.Clear();
        }

        static void SetupRenderer(MeshRenderer mr)
        {
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void Recycle(Chunk c)
        {
            ClearPropChildren(c.props);
            c.glows.Clear();
            c.go.SetActive(false);
            pool.Push(c);
        }

        static void ClearPropChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var ch = t.GetChild(i);
                ch.SetParent(null, false);
                Object.Destroy(ch.gameObject);
            }
        }

        Chunk NewChunk()
        {
            if (pool.Count > 0) return pool.Pop();
            var c = new Chunk();
            c.go = new GameObject("Chunk");
            c.go.transform.SetParent(root, false);
            c.mf = c.go.AddComponent<MeshFilter>();
            c.mr = c.go.AddComponent<MeshRenderer>();
            SetupRenderer(c.mr);
            c.props = new GameObject("Accesorios").transform;
            c.props.SetParent(c.go.transform, false);
            c.propMf = c.props.gameObject.AddComponent<MeshFilter>();
            c.propMr = c.props.gameObject.AddComponent<MeshRenderer>();
            SetupRenderer(c.propMr);
            return c;
        }

        /// <summary>Crea los tramos que faltan entre sMin y sMax (maximo `budget` por llamada) y recicla los lejanos.</summary>
        public void EnsureRange(float sMin, float sMax, int budget)
        {
            if (pal == null || arenaMode) return;
            int i0 = Mathf.FloorToInt(sMin / L), i1 = Mathf.FloorToInt(sMax / L);
            for (int i = i0; i <= i1 && budget > 0; i++)
            {
                if (chunks.ContainsKey(i)) continue;
                var c = NewChunk();
                c.idx = i;
                BuildChunk(c);
                chunks[i] = c;
                budget--;
                dirtyGlows = true;
            }
            tmpKeys.Clear();
            foreach (var kv in chunks)
                if (kv.Key < i0 - 1 || kv.Key > i1 + 1) tmpKeys.Add(kv.Key);
            for (int k = 0; k < tmpKeys.Count; k++)
            {
                Recycle(chunks[tmpKeys[k]]);
                chunks.Remove(tmpKeys[k]);
                dirtyGlows = true;
            }
            if (dirtyGlows)
            {
                dirtyGlows = false;
                Glows.Clear();
                foreach (var kv in chunks) Glows.AddRange(kv.Value.glows);
            }
        }

        /// <summary>Cantidad de tramos que faltan para cubrir el rango (para decidir si generar de golpe).</summary>
        public int Missing(float sMin, float sMax)
        {
            int i0 = Mathf.FloorToInt(sMin / L), i1 = Mathf.FloorToInt(sMax / L), n = 0;
            for (int i = i0; i <= i1; i++) if (!chunks.ContainsKey(i)) n++;
            return n;
        }

        // ================================================================== helpers de color (tonos discretos de la paleta)
        static Color Mul(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }
        static Vector3 P(float s, float n, float y) { return new Vector3(n, y, s); }

        static float PlatY(float s, float d)
        {
            return W.HPlat + 0.18f * (Nz.Value(s * 0.15f, d * 0.15f, 5) - 0.5f) * Mathf.SmoothStep(0f, 1f, (d - 0.6f) / 2.5f);
        }

        static float LowY(float s, float e)
        {
            return -W.HDrop + 0.14f * (Nz.Value(s * 0.17f, e * 0.17f, 6) - 0.5f) * Mathf.SmoothStep(0f, 1f, (e - 0.3f) / 2f);
        }

        /// <summary>Suelo del pasillo: 3 tonos (suelo, mancha, suelo sombreado junto al borde de la meseta).</summary>
        Color CorrCol(Vector3 c)
        {
            float ao = Mathf.Clamp01(1f - (c.x - W.EdgeL(c.z)) / 1.0f);
            if (ao > 0.62f) return Mul(pal.Ground, 0.9f);
            float nz = Nz.Value(c.z * 0.30f, c.x * 0.30f, 11);
            return nz > 0.52f ? pal.Blot : pal.Ground;
        }

        /// <summary>Tapa de la meseta: 2 tonos.</summary>
        Color TopCol(Vector3 c)
        {
            float nz = Nz.Value(c.z * 0.22f, c.x * 0.22f, 12);
            return nz > 0.55f ? Mul(pal.Top, 0.9f) : pal.Top;
        }

        /// <summary>Suelo bajo del desnivel: 3 tonos.</summary>
        Color LowCol(Vector3 c, float e)
        {
            if (e < 0.7f) return Mul(pal.Low, 0.84f);
            float nz = Nz.Value(c.z * 0.25f, c.x * 0.25f, 13);
            return nz > 0.55f ? Mul(pal.Low, 0.92f) : pal.Low;
        }

        /// <summary>Pared con franjas: 4 tonos entre Face y FaceD (mas oscuro abajo, franjas alternadas).</summary>
        Color WallCol(int k, int rows)
        {
            float tt = 1f - (k + 0.5f) / rows;
            float dark = 0.5f * tt * tt;
            float mix = Mathf.Clamp01(0.12f + dark + ((k & 1) == 1 ? 0.12f : 0f));
            float q = Mathf.Round(mix * 3f) / 3f;
            return Color.Lerp(pal.Face, pal.FaceD, q);
        }

        void QuadUp(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color c0, Color c1)
        {
            mb.Tri(a, b, c, Vector3.up, c0, 0f);
            mb.Tri(a, c, d, Vector3.up, c1, 0f);
        }

        // ================================================================== tramo
        void BuildChunk(Chunk ch)
        {
            mb.Clear();
            ch.glows.Clear();
            float s0 = ch.idx * L;
            ClearPropChildren(ch.props);
            BuildCorridor(s0);
            BuildPlateau(s0);
            BuildDrop(s0);
            BuildDecor(s0);
            Material[] mats;
            ch.mesh = mb.ToMesh(ch.mesh, "Chunk" + ch.idx, out mats);
            ch.mf.sharedMesh = ch.mesh;
            ch.mr.sharedMaterials = VertexColorMerge.Apply(ch.mesh, mats);
            BuildProps(s0, ch.glows, ch.props);
            ch.propMesh = FusePropsInto(ch.props, ch.propMesh, ch.propMr, ch.propMf);
            ch.go.SetActive(true);
        }

        void BuildCorridor(float s0)
        {
            const int cols = 9;
            int rows = Mathf.RoundToInt(L);
            for (int i = 0; i < rows; i++)
            {
                float sa = s0 + i, sb = sa + 1f;
                float nlA = W.EdgeL(sa) - 0.12f, nrA = W.EdgeR(sa) - 0.30f;
                float nlB = W.EdgeL(sb) - 0.12f, nrB = W.EdgeR(sb) - 0.30f;
                for (int k = 0; k < cols; k++)
                {
                    float t0 = k / (float)cols, t1 = (k + 1) / (float)cols;
                    Vector3 a = P(sa, Mathf.Lerp(nlA, nrA, t0), 0f);
                    Vector3 b = P(sa, Mathf.Lerp(nlA, nrA, t1), 0f);
                    Vector3 c = P(sb, Mathf.Lerp(nlB, nrB, t1), 0f);
                    Vector3 d = P(sb, Mathf.Lerp(nlB, nrB, t0), 0f);
                    Color c0 = CorrCol((a + b + c) / 3f);
                    Color c1 = CorrCol((a + c + d) / 3f);
                    QuadUp(a, b, c, d, c0, c1);
                }
            }
        }

        static readonly float[] ProfD = { 0.30f, 0.08f, -0.06f, -0.08f, 0.0f };
        static readonly float[] ProfY = { 0f, -0.03f, -0.14f, -0.30f, -0.44f };
        static readonly float[] DepthP = { 0.30f, 0.9f, 1.8f, 3.2f, 5.0f, 7.5f, 10.5f, 14f, 18.5f, 24f };
        static readonly float[] DepthL = { 0f, 0.35f, 1.0f, 2.0f, 3.5f, 5.5f, 8f, 11f, 15f, 19f, 24f };

        void LipColors(out Color c0, out Color c1, out Color c2, out Color c3)
        {
            c0 = Color.Lerp(pal.Lip, Color.white, 0.18f);
            c1 = pal.Lip;
            c2 = Mul(pal.Lip, 0.88f);
            c3 = Mul(pal.Lip, 0.72f);
        }

        void BuildPlateau(float s0)
        {
            int rows = Mathf.RoundToInt(L);
            Color l0, l1, l2, l3;
            LipColors(out l0, out l1, out l2, out l3);
            Color[] lc = { l0, l1, l2, l3 };
            Vector3[] lh = { new Vector3(0.1f, 1f, 0f), new Vector3(0.8f, 0.6f, 0f), new Vector3(1f, 0.1f, 0f), new Vector3(0.9f, -0.4f, 0f) };
            float H = W.HPlat;
            float Hw = H + ProfY[4];
            for (int i = 0; i < rows; i++)
            {
                float sa = s0 + i, sb = sa + 1f;
                // tapa de la meseta (de afuera hacia el labio)
                for (int k = 0; k < DepthP.Length - 1; k++)
                {
                    float d0 = DepthP[k], d1 = DepthP[k + 1];
                    Vector3 a = P(sa, W.EdgeL(sa) - d0, PlatY(sa, d0));
                    Vector3 b = P(sa, W.EdgeL(sa) - d1, PlatY(sa, d1));
                    Vector3 c = P(sb, W.EdgeL(sb) - d1, PlatY(sb, d1));
                    Vector3 d = P(sb, W.EdgeL(sb) - d0, PlatY(sb, d0));
                    Color c0 = TopCol((a + b + c) / 3f), c1 = TopCol((a + c + d) / 3f);
                    if (k == 0)
                    {
                        c0 = lc[1];
                        c1 = lc[1];
                    }
                    QuadUp(a, b, c, d, c0, c1);
                }
                // labio redondeado
                float wa = 0.05f * (Nz.Value(sa * 0.7f, 0f, 23) - 0.5f), wb = 0.05f * (Nz.Value(sb * 0.7f, 0f, 23) - 0.5f);
                for (int m = 0; m < 4; m++)
                {
                    Vector3 a = P(sa, W.EdgeL(sa) - (ProfD[m] + wa), H + ProfY[m]);
                    Vector3 b = P(sa, W.EdgeL(sa) - (ProfD[m + 1] + wa), H + ProfY[m + 1]);
                    Vector3 c = P(sb, W.EdgeL(sb) - (ProfD[m + 1] + wb), H + ProfY[m + 1]);
                    Vector3 d = P(sb, W.EdgeL(sb) - (ProfD[m] + wb), H + ProfY[m]);
                    mb.Quad(a, b, c, d, lh[m], lc[m], 0f);
                }
            }
            // pared con franjas (paso 0.5 m, 5 filas)
            const int R = 5;
            int cols = Mathf.RoundToInt(L * 2f);
            for (int j = 0; j < cols; j++)
            {
                float sa = s0 + j * 0.5f, sb = sa + 0.5f;
                for (int k = 0; k < R; k++)
                {
                    float y0 = Hw * k / R, y1 = Hw * (k + 1) / R;
                    Vector3 a = P(sa, WallN(sa, y0, y0 / Hw, -1f), y0);
                    Vector3 b = P(sa, WallN(sa, y1, y1 / Hw, -1f), y1);
                    Vector3 c = P(sb, WallN(sb, y1, y1 / Hw, -1f), y1);
                    Vector3 d = P(sb, WallN(sb, y0, y0 / Hw, -1f), y0);
                    Color col = WallCol(k, R);
                    mb.Quad(a, b, c, d, Vector3.right, col, 0f);
                }
            }
            // festones del labio
            int nsc = Eco ? 10 : 20;
            float stepSc = L / nsc;
            for (int j = 0; j < nsc; j++)
            {
                float s = s0 + (j + 0.5f) * stepSc + (Nz.Hash01(j, Mathf.FloorToInt(s0), 41) - 0.5f) * 0.3f;
                Vector3 c = P(s, W.EdgeL(s) - 0.26f, H - 0.03f);
                mb.Blob(c, new Vector3(0.28f, 0.17f, 0.26f), 0, Mathf.FloorToInt(s * 10f), 0.1f, lc[0], lc[0], 0f, -0.2f);
            }
        }

        /// <summary>Posicion lateral (n) de la pared a la altura y, con ondulacion; side = -1 meseta (izquierda), +1 desnivel.</summary>
        float WallN(float s, float yAbs, float t, float side)
        {
            float wob = (Nz.Value(s * 0.9f, yAbs * 1.2f, 21) - 0.5f) * 0.18f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
            return (side < 0f ? W.EdgeL(s) : W.EdgeR(s)) + wob;
        }

        void BuildDrop(float s0)
        {
            int rows = Mathf.RoundToInt(L);
            Color l0, l1, l2, l3;
            LipColors(out l0, out l1, out l2, out l3);
            Color[] lc = { l0, l1, l2, l3 };
            Vector3[] lh = { new Vector3(0.1f, 1f, 0f), new Vector3(0.8f, 0.6f, 0f), new Vector3(1f, 0.1f, 0f), new Vector3(0.9f, -0.4f, 0f) };
            float[] E = { -0.30f, -0.08f, 0.06f, 0.08f, 0.0f };
            float Hd = W.HDrop;
            float Hw = Hd + ProfY[4]; // ProfY[4] es negativo
            for (int i = 0; i < rows; i++)
            {
                float sa = s0 + i, sb = sa + 1f;
                float wa = 0.05f * (Nz.Value(sa * 0.7f, 3f, 24) - 0.5f), wb = 0.05f * (Nz.Value(sb * 0.7f, 3f, 24) - 0.5f);
                for (int m = 0; m < 4; m++)
                {
                    Vector3 a = P(sa, W.EdgeR(sa) + E[m] + wa, ProfY[m]);
                    Vector3 b = P(sa, W.EdgeR(sa) + E[m + 1] + wa, ProfY[m + 1]);
                    Vector3 c = P(sb, W.EdgeR(sb) + E[m + 1] + wb, ProfY[m + 1]);
                    Vector3 d = P(sb, W.EdgeR(sb) + E[m] + wb, ProfY[m]);
                    mb.Quad(a, b, c, d, lh[m], lc[m], 0f);
                }
                // suelo bajo
                for (int k = 0; k < DepthL.Length - 1; k++)
                {
                    float e0 = DepthL[k], e1 = DepthL[k + 1];
                    Vector3 a = P(sa, W.EdgeR(sa) + e0, LowY(sa, e0));
                    Vector3 b = P(sa, W.EdgeR(sa) + e1, LowY(sa, e1));
                    Vector3 c = P(sb, W.EdgeR(sb) + e1, LowY(sb, e1));
                    Vector3 d = P(sb, W.EdgeR(sb) + e0, LowY(sb, e0));
                    QuadUp(a, b, c, d, LowCol((a + b + c) / 3f, e0), LowCol((a + c + d) / 3f, e0));
                }
            }
            // pared que cae (de ProfY[4] hasta -HDrop)
            const int R = 5;
            int cols = Mathf.RoundToInt(L * 2f);
            float top = ProfY[4];
            for (int j = 0; j < cols; j++)
            {
                float sa = s0 + j * 0.5f, sb = sa + 0.5f;
                for (int k = 0; k < R; k++)
                {
                    // k = 0 arriba (claro) ... R-1 abajo (oscuro)
                    float y0 = top - (Hw * k / R);
                    float y1 = top - (Hw * (k + 1) / R);
                    float t0 = (float)k / R, t1 = (float)(k + 1) / R;
                    Vector3 a = P(sa, WallN(sa, -y0, t0, 1f), y0);
                    Vector3 b = P(sa, WallN(sa, -y1, t1, 1f), y1);
                    Vector3 c = P(sb, WallN(sb, -y1, t1, 1f), y1);
                    Vector3 d = P(sb, WallN(sb, -y0, t0, 1f), y0);
                    // WallCol espera k=0 abajo: se invierte el indice
                    Color col = WallCol(R - 1 - k, R);
                    mb.Quad(a, b, c, d, Vector3.right, col, 0f);
                }
            }
        }

        // ================================================================== accesorios y decoracion
        System.Random CellRng(int ci, int salt)
        {
            unchecked { return new System.Random(ci * 7919 + biome * 104729 + salt * 15485863 + 13); }
        }

        /// <summary>Crea un accesorio del kit (ArtKit.Prop) bajo parent; registra su punto luminoso si emite luz.</summary>
        GameObject AddProp(Transform parent, string kind, Vector3 pos, float scale, float rotY, int seed, List<GlowSpot> glows, float v)
        {
            GameObject go = ArtKit.Prop(kind, biome, seed, parent);
            if (go == null) return null;
            Transform t = go.transform;
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, rotY, 0f);
            t.localScale = Vector3.one * scale;
            int gk = PropFactory.GlowKind(kind);
            if (gk >= 0 && glows != null)
            {
                Color gc = gk == 2 ? new Color(1f, 0.42f, 0.1f) : (v < 0.6f ? pal.Prop : pal.Prop2);
                glows.Add(new GlowSpot { pos = pos + Vector3.up * (0.45f * scale), col = gc, size = (gk == 2 ? 2f : 1.3f) * scale, phase = v * 9f, kind = gk });
            }
            return go;
        }

        void BuildProps(float s0, List<GlowSpot> glows, Transform parent)
        {
            float s1 = s0 + L;
            string[] kinds = PropFactory.Kinds[((biome % 4) + 4) % 4];
            int c0 = Mathf.FloorToInt(s0 / CellProp), c1 = Mathf.FloorToInt(s1 / CellProp);
            for (int ci = c0; ci <= c1; ci++)
            {
                var r = CellRng(ci, 1);
                // pocos accesorios por celda: la malla del tramo los funde en pocas llamadas de dibujo
                int count = Eco ? 1 : 2;
                for (int k = 0; k < count; k++)
                {
                    float s = (ci + (float)r.NextDouble()) * CellProp;
                    float depth = Mathf.Lerp(0.9f, 7.8f, (float)r.NextDouble());
                    string kind = kinds[r.Next(kinds.Length)];
                    float sc = Mathf.Lerp(0.85f, 1.25f, (float)r.NextDouble()) * (depth < 1.94f ? 1.15f : 1f);
                    float v = (float)r.NextDouble();
                    float rot = (float)r.NextDouble() * 360f;
                    if (s < s0 || s >= s1) continue;
                    Vector3 p = P(s, W.EdgeL(s) - depth, PlatY(s, depth));
                    AddProp(parent, kind, p, sc, rot, ci * 31 + k, glows, v);
                }
                var r2 = CellRng(ci, 2);
                if (r2.NextDouble() < (Eco ? 0.3 : 0.55))
                {
                    float s = (ci + (float)r2.NextDouble()) * CellProp;
                    float e = Mathf.Lerp(1.0f, 5.8f, (float)r2.NextDouble());
                    string kind = kinds[(r2.Next(2)) + 1];
                    float sc = Mathf.Lerp(0.7f, 0.95f, (float)r2.NextDouble());
                    float v = (float)r2.NextDouble();
                    float rot = (float)r2.NextDouble() * 360f;
                    if (s < s0 || s >= s1) continue;
                    Vector3 p = P(s, W.EdgeR(s) + e, LowY(s, e));
                    AddProp(parent, kind, p, sc, rot, ci * 37 + 5, glows, v);
                }
            }
            BuildMineProps(s0, s1, parent);
        }

        const float CellMine = 15f;

        /// <summary>Utileria de mina al borde del pasillo, de vez en cuando (rieles, vagoneta, farol, vigas, barriles, cajas...).</summary>
        void BuildMineProps(float s0, float s1, Transform parent)
        {
            int c0 = Mathf.FloorToInt(s0 / CellMine), c1 = Mathf.FloorToInt(s1 / CellMine);
            for (int ci = c0; ci <= c1; ci++)
            {
                var r = CellRng(ci, 9);
                if (r.NextDouble() > (Eco ? 0.45 : 0.7)) continue;
                float s = (ci + (float)r.NextDouble()) * CellMine;
                if (s < s0 || s >= s1) continue;
                float side = r.NextDouble() < 0.5 ? -1f : 1f;
                float t = side * Mathf.Lerp(0.9f, 0.97f, (float)r.NextDouble());
                Vector2 cp = W.CorridorPoint(s, t);
                float yaw = CorridorYaw(s);
                int pick = r.Next(PropFactory.MineKinds.Length);
                string kind = PropFactory.MineKinds[pick];
                float sc = Mathf.Lerp(0.95f, 1.1f, (float)r.NextDouble());
                if (kind == "minecart")
                {
                    // vagoneta sobre un tramo de riel, alineada con el pasillo
                    for (int k = -1; k <= 1; k++)
                    {
                        float sk = s + k * 1.6f;
                        Vector2 rp = W.CorridorPoint(sk, t);
                        AddProp(parent, "rail", new Vector3(rp.x, 0.01f, sk), 1f, CorridorYaw(sk), ci * 13 + k + 100, null, 0f);
                    }
                    AddProp(parent, "minecart", new Vector3(cp.x, 0.03f, s), sc, yaw, ci * 13 + 7, null, 0f);
                }
                else if (kind == "beam" || kind == "sign" || kind == "lantern" || kind == "pickaxe_stand")
                {
                    AddProp(parent, kind, new Vector3(cp.x, 0f, s), sc, kind == "beam" ? yaw : yaw + (side < 0f ? 90f : -90f), ci * 13 + pick, null, 0f);
                }
                else
                {
                    AddProp(parent, kind, new Vector3(cp.x, 0f, s), sc, (float)r.NextDouble() * 360f, ci * 13 + pick, null, 0f);
                    if (kind == "crate" && r.NextDouble() < 0.5)
                        AddProp(parent, "barrel", new Vector3(cp.x - side * 0.5f, 0f, s + 0.5f), sc * 0.95f, (float)r.NextDouble() * 360f, ci * 13 + 5, null, 0f);
                }
            }
        }

        /// <summary>Giro (grados, eje Y) que alinea un objeto con la direccion del pasillo en s.</summary>
        static float CorridorYaw(float s)
        {
            const float ds = 0.5f;
            return Mathf.Atan2(W.Cen(s + ds) - W.Cen(s - ds), 2f * ds) * Mathf.Rad2Deg;
        }

        void BuildDecor(float s0)
        {
            float s1 = s0 + L;
            int c0 = Mathf.FloorToInt(s0 / CellDecor), c1 = Mathf.FloorToInt(s1 / CellDecor);
            int tries = Eco ? 1 : 2;
            for (int ci = c0; ci <= c1; ci++)
            {
                var r = CellRng(ci, 3);
                for (int k = 0; k < tries; k++)
                {
                    float s = (ci + (float)r.NextDouble()) * CellDecor;
                    float t = Mathf.Lerp(-0.93f, 0.93f, (float)r.NextDouble());
                    int kind = r.Next(4);
                    float v = (float)r.NextDouble();
                    if (s < s0 || s >= s1) continue;
                    Vector2 cp = W.CorridorPoint(s, t);
                    PropFactory.AddDecor(mb, kind, new Vector3(cp.x, 0f, s), v, pal, biome, ci * 5 + k);
                }
                var rp = CellRng(ci, 4);
                for (int k = 0; k < tries; k++)
                {
                    float s = (ci + (float)rp.NextDouble()) * CellDecor;
                    float d = Mathf.Lerp(0.8f, 12f, (float)rp.NextDouble());
                    int kind = rp.Next(4);
                    float v = (float)rp.NextDouble();
                    if (s >= s0 && s < s1)
                        PropFactory.AddDecor(mb, kind, P(s, W.EdgeL(s) - d, PlatY(s, d)), v, pal, biome, ci * 7 + k);
                    float s2 = (ci + (float)rp.NextDouble()) * CellDecor;
                    float e = Mathf.Lerp(0.8f, 10f, (float)rp.NextDouble());
                    int kind2 = rp.Next(4);
                    float v2 = (float)rp.NextDouble();
                    if (s2 >= s0 && s2 < s1)
                        PropFactory.AddDecor(mb, kind2, P(s2, W.EdgeR(s2) + e, LowY(s2, e)), v2, pal, biome, ci * 11 + k);
                }
            }
        }

        // ================================================================== fusion de accesorios del kit
        static readonly Dictionary<Material, List<CombineInstance>> fuseMap = new Dictionary<Material, List<CombineInstance>>();
        static readonly List<Material> fuseMats = new List<Material>();
        static readonly List<Mesh> fuseTemps = new List<Mesh>();
        static readonly List<MeshFilter> fuseMfs = new List<MeshFilter>();
        static readonly List<GameObject> fuseKill = new List<GameObject>();

        /// <summary>
        /// Funde todos los accesorios simples (solo MeshRenderer) bajo root en UNA malla con una submalla por material
        /// (pocas llamadas de dibujo por tramo) y destruye los originales. Los accesorios con luces, particulas u otros
        /// renderers se dejan intactos. Devuelve la malla reutilizada.
        /// </summary>
        static Mesh FusePropsInto(Transform root, Mesh dst, MeshRenderer mr, MeshFilter mf)
        {
            fuseMap.Clear();
            fuseMats.Clear();
            fuseTemps.Clear();
            fuseKill.Clear();
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform prop = root.GetChild(i);
                if (!Fusable(prop)) continue;
                fuseMfs.Clear();
                prop.GetComponentsInChildren(false, fuseMfs);
                bool any = false;
                for (int k = 0; k < fuseMfs.Count; k++)
                {
                    var f = fuseMfs[k];
                    var r = f.GetComponent<MeshRenderer>();
                    Mesh m = f.sharedMesh;
                    if (r == null || m == null || !m.isReadable || m.vertexCount == 0) continue;
                    var sm = r.sharedMaterials;
                    if (sm == null || sm.Length == 0) continue;
                    Matrix4x4 tr = toRoot * f.transform.localToWorldMatrix;
                    for (int s = 0; s < m.subMeshCount; s++)
                    {
                        Material mat = sm[Mathf.Min(s, sm.Length - 1)];
                        if (mat == null) continue;
                        List<CombineInstance> list;
                        if (!fuseMap.TryGetValue(mat, out list))
                        {
                            list = new List<CombineInstance>(8);
                            fuseMap[mat] = list;
                            fuseMats.Add(mat);
                        }
                        var ci = new CombineInstance();
                        ci.mesh = m;
                        ci.subMeshIndex = s;
                        ci.transform = tr;
                        list.Add(ci);
                        any = true;
                    }
                }
                if (any) fuseKill.Add(prop.gameObject);
            }
            if (fuseMats.Count == 0)
            {
                mf.sharedMesh = null;
                mr.enabled = false;
                return dst;
            }
            var parts = new CombineInstance[fuseMats.Count];
            for (int i = 0; i < fuseMats.Count; i++)
            {
                var tmp = new Mesh();
                tmp.indexFormat = IndexFormat.UInt32;
                tmp.CombineMeshes(fuseMap[fuseMats[i]].ToArray(), true, true);
                fuseTemps.Add(tmp);
                parts[i] = new CombineInstance { mesh = tmp, subMeshIndex = 0, transform = Matrix4x4.identity };
            }
            if (dst == null) dst = new Mesh();
            dst.Clear();
            dst.name = "Accesorios";
            dst.indexFormat = IndexFormat.UInt32;
            dst.CombineMeshes(parts, false, false);
            dst.RecalculateBounds();
            for (int i = 0; i < fuseTemps.Count; i++) Object.Destroy(fuseTemps[i]);
            fuseTemps.Clear();
            mf.sharedMesh = dst;
            mr.sharedMaterials = VertexColorMerge.Apply(dst, fuseMats.ToArray());
            mr.enabled = true;
            for (int i = 0; i < fuseKill.Count; i++)
            {
                fuseKill[i].transform.SetParent(null, false);
                Object.Destroy(fuseKill[i]);
            }
            fuseKill.Clear();
            fuseMap.Clear();
            fuseMats.Clear();
            return dst;
        }

        static bool Fusable(Transform prop)
        {
            if (prop.GetComponentInChildren<ParticleSystem>(true) != null) return false;
            if (prop.GetComponentInChildren<Light>(true) != null) return false;
            if (prop.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return false;
            return true;
        }

        // ================================================================== meseta circular (Excavar)
        public void BuildArena()
        {
            ClearChunks();
            arenaMode = true;
            arenaGlows.Clear();
            Glows.Clear();
            mb.Clear();
            float R = W.ArenaR;
            const int seg = 48;
            float[] rr = { 0f, 1.5f, 3.2f, 5f, 7f, 9f, 11f, 0f };
            int rings = 7;
            rr[rings] = R - 0.3f;
            // suelo
            for (int k = 0; k < rings; k++)
            {
                float r0 = rr[k], r1 = rr[k + 1];
                for (int i = 0; i < seg; i++)
                {
                    float a0 = Mathf.PI * 2f * i / seg, a1 = Mathf.PI * 2f * (i + 1) / seg;
                    Vector3 p00 = new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0);
                    Vector3 p01 = new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);
                    Vector3 p10 = new Vector3(Mathf.Cos(a0) * r1, 0f, Mathf.Sin(a0) * r1);
                    Vector3 p11 = new Vector3(Mathf.Cos(a1) * r1, 0f, Mathf.Sin(a1) * r1);
                    mb.Tri(p00, p10, p11, Vector3.up, ArenaCol((p00 + p10 + p11) / 3f), 0f);
                    mb.Tri(p00, p11, p01, Vector3.up, ArenaCol((p00 + p11 + p01) / 3f), 0f);
                }
            }
            // labio redondeado y pared
            Color l0, l1, l2, l3;
            LipColors(out l0, out l1, out l2, out l3);
            Color[] lc = { l0, l1, l2, l3 };
            float[] pd = { -0.30f, -0.08f, 0.06f, 0.08f, 0.0f };
            float[] py = { 0f, -0.03f, -0.14f, -0.30f, -0.44f };
            float wallBottom = -2.6f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2f * i / seg, a1 = Mathf.PI * 2f * (i + 1) / seg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                for (int m = 0; m < 4; m++)
                {
                    float ra = R + pd[m], rb = R + pd[m + 1];
                    Vector3 a = d0 * ra + Vector3.up * py[m], b = d0 * rb + Vector3.up * py[m + 1];
                    Vector3 c = d1 * rb + Vector3.up * py[m + 1], d = d1 * ra + Vector3.up * py[m];
                    Vector3 hint = (d0 + d1) * 0.5f + Vector3.up * (m == 0 ? 2f : 0.2f);
                    mb.Quad(a, b, c, d, hint, lc[m], 0f);
                }
                const int WR = 4;
                for (int k = 0; k < WR; k++)
                {
                    float y0 = py[4] + (wallBottom - py[4]) * k / WR, y1 = py[4] + (wallBottom - py[4]) * (k + 1) / WR;
                    Color col = WallCol(WR - 1 - k, WR);
                    Vector3 a = d0 * R + Vector3.up * y0, b = d0 * R + Vector3.up * y1;
                    Vector3 c = d1 * R + Vector3.up * y1, d = d1 * R + Vector3.up * y0;
                    mb.Quad(a, b, c, d, (d0 + d1) * 0.5f, col, 0f);
                }
                // festones del labio
                if ((i & 1) == 0)
                {
                    Vector3 c = (d0 + d1) * 0.5f * (R - 0.26f) + Vector3.up * -0.03f;
                    mb.Blob(c, new Vector3(0.3f, 0.17f, 0.3f), 0, i, 0.1f, lc[0], lc[0], 0f, -0.2f);
                }
            }
            // decoracion menuda
            var rd = CellRng(98, 98);
            int nDec = Eco ? 120 : 260;
            for (int i = 0; i < nDec; i++)
            {
                float a2 = (float)rd.NextDouble() * Mathf.PI * 2f;
                float rad = Mathf.Sqrt((float)rd.NextDouble()) * (R - 0.6f);
                PropFactory.AddDecor(mb, rd.Next(4), new Vector3(Mathf.Cos(a2) * rad, 0f, Mathf.Sin(a2) * rad), (float)rd.NextDouble(), pal, biome, i);
            }
            if (arenaGo == null)
            {
                arenaGo = new GameObject("Arena");
                arenaGo.transform.SetParent(root, false);
                arenaMf = arenaGo.AddComponent<MeshFilter>();
                arenaMr = arenaGo.AddComponent<MeshRenderer>();
                SetupRenderer(arenaMr);
                arenaProps = new GameObject("AccesoriosArena").transform;
                arenaProps.SetParent(arenaGo.transform, false);
                arenaPropMf = arenaProps.gameObject.AddComponent<MeshFilter>();
                arenaPropMr = arenaProps.gameObject.AddComponent<MeshRenderer>();
                SetupRenderer(arenaPropMr);
            }
            ClearPropChildren(arenaProps);
            Material[] amats;
            arenaMesh = mb.ToMesh(arenaMesh, "Arena", out amats);
            arenaMf.sharedMesh = arenaMesh;
            arenaMr.sharedMaterials = VertexColorMerge.Apply(arenaMesh, amats);
            // accesorios en el borde (ArtKit.Prop), fundidos en una malla
            var rg = CellRng(99, 99);
            string[] kinds = PropFactory.Kinds[((biome % 4) + 4) % 4];
            int nProps = Eco ? 16 : 26;
            for (int i = 0; i < nProps; i++)
            {
                float a3 = Mathf.PI * 2f * i / nProps + (float)(rg.NextDouble() - 0.5) * 0.1f;
                float rad = R - Mathf.Lerp(0.5f, 1.4f, (float)rg.NextDouble());
                string kind = kinds[rg.Next(kinds.Length)];
                float sc = Mathf.Lerp(0.8f, 1.1f, (float)rg.NextDouble());
                float v = (float)rg.NextDouble();
                float rot = (float)rg.NextDouble() * 360f;
                AddProp(arenaProps, kind, new Vector3(Mathf.Cos(a3) * rad, 0f, Mathf.Sin(a3) * rad), sc, rot, i, arenaGlows, v);
            }
            arenaPropMesh = FusePropsInto(arenaProps, arenaPropMesh, arenaPropMr, arenaPropMf);
            arenaGo.SetActive(true);
            Glows.AddRange(arenaGlows);
        }

        Color ArenaCol(Vector3 c)
        {
            float nz = Nz.Value(c.x * 0.30f, c.z * 0.30f, 14);
            return nz > 0.52f ? pal.Blot : pal.Ground;
        }

        public void DestroyAll()
        {
            ClearChunks();
            while (pool.Count > 0)
            {
                var c = pool.Pop();
                if (c.mesh != null) Object.Destroy(c.mesh);
                if (c.propMesh != null) Object.Destroy(c.propMesh);
                Object.Destroy(c.go);
            }
            if (arenaMesh != null) Object.Destroy(arenaMesh);
            if (arenaPropMesh != null) Object.Destroy(arenaPropMesh);
            if (arenaGo != null) Object.Destroy(arenaGo);
            if (root != null) Object.Destroy(root.gameObject);
        }
    }
}
