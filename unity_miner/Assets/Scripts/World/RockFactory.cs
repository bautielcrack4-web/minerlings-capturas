using System.Collections.Generic;
using Mineros.Art;
using UnityEngine;

namespace Mineros.World
{
    /// <summary>Malla con una submalla por tono y los materiales compartidos (shader unico) de cada submalla.</summary>
    public sealed class MeshPart
    {
        public Mesh mesh;
        public Material[] mats;
    }

    /// <summary>Malla de una roca y sus mallas de grietas acumulativas (nivel 1..4).</summary>
    public sealed class RockSet
    {
        public MeshPart body;
        public MeshPart[] cracks = new MeshPart[4];
    }

    /// <summary>
    /// Mallas procedurales de rocas (low-poly facetado, 3 tonos de la paleta, identidad por bioma), Geoda, Roca Cofre, reloj y
    /// meteorito. Cristales y gemas incrustados salen de ArtKit.CrystalMesh / ArtKit.GemMesh. Las rocas son de radio unitario:
    /// el mundo las escala. Se comparten entre rocas (biblioteca de variantes).
    /// </summary>
    public static class RockFactory
    {
        public const int Variants = 4;

        static readonly MeshBuilder mb = new MeshBuilder();
        static readonly Dictionary<int, RockSet> sets = new Dictionary<int, RockSet>();
        static readonly List<Mesh> owned = new List<Mesh>();
        static RockSet bossSet;
        static MeshPart chestBody, chestLid, clockPart, meteorPart;
        static readonly MeshPart[] bossCrystal = new MeshPart[3];
        static readonly Dictionary<int, Mesh> kitCrystals = new Dictionary<int, Mesh>();
        static Mesh kitGem;
        static BiomePal pal;
        static int biome = -1;

        /// <summary>Meteorito (independiente del bioma).</summary>
        public static MeshPart Meteor { get { if (meteorPart == null) BuildMeteor(); return meteorPart; } }

        static Color LavaC { get { return pal != null ? pal.Prop2 : Biomes.H("ff8a3a"); } }

        /// <summary>Cambia de bioma y destruye las mallas del anterior (las rocas deben haber sido liberadas antes).</summary>
        public static void SetBiome(int b)
        {
            if (b == biome && pal != null) return;
            DestroyBiomeMeshes();
            biome = b;
            pal = Biomes.Get(b);
        }

        static void DestroyBiomeMeshes()
        {
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null) Object.Destroy(owned[i]);
            owned.Clear();
            sets.Clear();
            bossSet = null;
            chestBody = null; chestLid = null;
            bossCrystal[0] = bossCrystal[1] = bossCrystal[2] = null;
        }

        static Mesh Own(Mesh m) { owned.Add(m); return m; }

        static MeshPart Bake(string name)
        {
            var part = new MeshPart();
            part.mesh = Own(mb.ToMesh(null, name, out part.mats));
            return part;
        }

        // ------------------------------------------------------------------ piezas del kit de arte (normalizadas)
        static Mesh KitCrystal(int seed)
        {
            int k = ((seed % 6) + 6) % 6;
            Mesh m;
            if (kitCrystals.TryGetValue(k, out m) && m != null) return m;
            m = ArtKit.CrystalMesh(k);
            kitCrystals[k] = m;
            return m;
        }

        /// <summary>
        /// Incrusta un cristal de ArtKit.CrystalMesh con la base en baseC, apuntando a up, de alto h y radio w
        /// (la malla del kit se normaliza por sus limites). Si el kit no devuelve malla, usa un prisma propio.
        /// </summary>
        static void AddKitCrystal(MeshBuilder b, int seed, Vector3 baseC, Vector3 up, float h, float w, Color col, float emis)
        {
            Mesh src = KitCrystal(seed);
            if (src != null && src.vertexCount > 0 && src.isReadable)
            {
                Bounds bb = src.bounds;
                float hy = Mathf.Max(0.001f, bb.size.y);
                float rx = Mathf.Max(0.001f, Mathf.Max(bb.extents.x, bb.extents.z));
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, up.normalized);
                Matrix4x4 m = Matrix4x4.TRS(baseC, rot, new Vector3(w / rx, h / hy, w / rx)) * Matrix4x4.Translate(new Vector3(-bb.center.x, -bb.min.y, -bb.center.z));
                if (b.AddMesh(src, m, col, emis)) return;
            }
            b.Crystal(baseC, up, h, w, 5, col, emis);
        }

        /// <summary>Incrusta una gema tallada (ArtKit.GemMesh) centrada en pos con el tamano (diametro) dado.</summary>
        static void AddKitGem(MeshBuilder b, Vector3 pos, float size, float rotY, Color col, float emis)
        {
            if (kitGem == null) kitGem = ArtKit.GemMesh();
            Mesh src = kitGem;
            if (src != null && src.vertexCount > 0 && src.isReadable)
            {
                Bounds bb = src.bounds;
                float k = size / Mathf.Max(0.001f, Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z)));
                Matrix4x4 m = Matrix4x4.TRS(pos, Quaternion.Euler(0f, rotY, 0f), Vector3.one * k) * Matrix4x4.Translate(-bb.center);
                if (b.AddMesh(src, m, col, emis)) return;
            }
            b.Octa(pos, new Vector3(size * 0.45f, size * 0.55f, size * 0.45f), col, emis);
        }

        // ------------------------------------------------------------------ rocas normales
        /// <summary>tier 0..2, cls 0 normal / 1 rica / 2 dura.</summary>
        public static RockSet Get(int tier, int cls, int variant)
        {
            int key = (tier * 3 + cls) * Variants + variant;
            RockSet s;
            if (sets.TryGetValue(key, out s)) return s;
            s = BuildRock(tier, cls, key * 7919 + biome * 131 + 5);
            sets[key] = s;
            return s;
        }

        static RockSet BuildRock(int tier, int cls, int seed)
        {
            var rng = new System.Random(seed);
            mb.Clear();
            bool hard = cls == 2, rich = cls == 1;
            Color cD = pal.RockD, cM = pal.Rock, cL = pal.RockL;
            if (hard)
            {
                cM = Color.Lerp(pal.Rock, Biomes.SteelD, 0.5f) * 0.92f;
                cD = Color.Lerp(pal.RockD, Biomes.H("2e3748"), 0.5f);
                cL = Color.Lerp(pal.RockL, Biomes.Steel, 0.4f);
            }
            var cen = new List<Vector3>();
            var nrm = new List<Vector3>();
            AddBlob(Vector3.zero, 1f, rng, seed, cD, cM, cL, biome, hard, rich, cen, nrm);
            if (tier == 2)
            {
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 off = new Vector3(W.R2.x, 0f, W.R2.y) * (side * 0.74f);
                AddBlob(off, 0.55f + 0.1f * (float)rng.NextDouble(), rng, seed + 17, cD, cM, cL, biome, hard, rich, cen, nrm);
            }
            // vetas de mineral (octaedros)
            int ns = rich ? rng.Next(5, 9) : rng.Next(1, 4);
            if (tier == 0 && !rich) ns = 1;
            Color oreCol = rich ? Biomes.Gold : (hard ? Biomes.Steel : pal.Ore);
            float oreEm = rich ? 0.75f : (hard ? 0.12f : (biome == 2 ? 0.7f : 0.4f));
            for (int i = 0; i < ns && cen.Count > 0; i++)
            {
                int tries = 0, fi;
                do { fi = rng.Next(cen.Count); tries++; } while (nrm[fi].y < 0.05f && tries < 12);
                float sz = (0.11f + 0.08f * (float)rng.NextDouble());
                Vector3 pos = cen[fi] + nrm[fi] * (sz * 0.35f);
                mb.Octa(pos, new Vector3(sz * 0.8f, sz, sz * 0.8f), oreCol, oreEm);
            }
            if (rich)
            {
                // pepitas de oro talladas incrustadas en la cara que mira a la camara
                for (int i = 0; i < 2; i++)
                {
                    Vector3 pos = new Vector3(W.R2.x * (i == 0 ? -0.42f : 0.38f), 0.62f + 0.18f * i, W.R2.y * (i == 0 ? -0.42f : 0.38f)) - new Vector3(W.F2.x, 0f, W.F2.y) * 0.78f;
                    AddKitGem(mb, pos, 0.3f, 25f + i * 50f, Biomes.GoldRich, 0.6f);
                }
            }
            if (biome == 2) AddCaveCrystals(rng);
            var set = new RockSet();
            set.body = Bake("Rock");
            Color crackCol = Color.Lerp(pal.FaceD, Color.black, 0.45f);
            bool glow = biome == 3;
            BuildCracks(set.cracks, seed + 3, crackCol, glow, LavaC, cen.Count);
            return set;
        }

        static void AddCaveCrystals(System.Random rng)
        {
            for (int i = 0; i < 3; i++)
            {
                float bx = (i - 1) * 0.34f;
                float h = i == 1 ? 0.78f : 0.52f;
                float a = (i - 1) * 0.38f;
                Vector3 b = new Vector3(W.R2.x * bx, 1.0f, W.R2.y * bx) + new Vector3(0.05f * (float)rng.NextDouble(), 0f, 0.05f * (float)rng.NextDouble());
                Vector3 up = Vector3.up * Mathf.Cos(a) + new Vector3(W.R2.x, 0f, W.R2.y) * Mathf.Sin(a);
                AddKitCrystal(mb, i + 1, b - up * 0.1f, up, h, 0.13f, pal.Ore, 0.6f);
            }
        }

        /// <summary>Roca low-poly (icosfera de 80 caras deformada, base plana) coloreada por 3 tonos y por bioma.</summary>
        static void AddBlob(Vector3 off, float rad, System.Random rng, int seed, Color cD, Color cM, Color cL,
            int flavor, bool hard, bool rich, List<Vector3> cen, List<Vector3> nrm)
        {
            Vector3[] sv = Ico.V1;
            int[] sf = Ico.F1;
            var p = new Vector3[sv.Length];
            float sx = 0.94f + 0.12f * (float)rng.NextDouble();
            float sz = 0.94f + 0.12f * (float)rng.NextDouble();
            for (int i = 0; i < sv.Length; i++)
            {
                float rr = 0.84f + 0.23f * (float)rng.NextDouble();
                Vector3 d = sv[i];
                Vector3 q = new Vector3(d.x * rr * sx, d.y * rr * 0.85f + 0.45f, d.z * rr * sz);
                if (q.y < 0f) q.y = 0f;
                p[i] = off + q * rad;
            }
            Vector3 center = off + new Vector3(0f, 0.45f * rad, 0f);
            for (int f = 0; f < sf.Length; f += 3)
            {
                Vector3 a = p[sf[f]], b = p[sf[f + 1]], c = p[sf[f + 2]];
                Vector3 ct = (a + b + c) / 3f;
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, ct - center) < 0f) n = -n;
                Vector3 lc = (ct - off) / rad;
                float ny = n.y;
                float h = Nz.Hash01(seed, f, 9);
                Color col = ny > 0.58f ? cL : (ny > 0.12f ? cM : cD);
                float emis = 0f;
                switch (flavor)
                {
                    case 0:
                        if (ny > 0.62f) col = pal.Prop; // musgo: el verde de la paleta de la pradera
                        break;
                    case 1:
                    {
                        int band = Mathf.FloorToInt(lc.y * 5f);
                        if ((band & 1) == 1 && ny <= 0.58f) col = cD;
                        break;
                    }
                    case 3:
                    {
                        float v = Mathf.Sin(lc.x * 6f + lc.z * 5f + lc.y * 2.5f + seed * 0.37f);
                        if (Mathf.Abs(v) < 0.16f && lc.y > 0.12f) { col = LavaC; emis = 0.95f; }
                        break;
                    }
                }
                if (hard)
                {
                    float v = Mathf.Sin(lc.x * 4.5f - lc.y * 5.5f + lc.z * 3f + seed * 0.21f);
                    if (Mathf.Abs(v) < 0.10f && lc.y > 0.1f) { col = Biomes.Steel; emis = 0.12f; }
                }
                else if (rich)
                {
                    float v = Mathf.Sin(lc.x * 5f + lc.y * 4f - lc.z * 5f + seed * 0.13f);
                    if (Mathf.Abs(v) < 0.09f && lc.y > 0.1f) { col = Biomes.Gold; emis = 0.55f; }
                }
                mb.Tri(a, b, c, n, col, emis);
                cen.Add(ct);
                nrm.Add(n);
            }
        }

        // ------------------------------------------------------------------ grietas
        static void BuildCracks(MeshPart[] outMeshes, int seed, Color crackCol, bool glow, Color glowCol, int nt)
        {
            // triangulos del cuerpo (los primeros nt de mb: las rocas, no los cristales incrustados)
            var tri = new Vector3[nt * 3];
            for (int i = 0; i < nt * 3; i++) tri[i] = mb.V[mb.T[i]];
            var crk = new MeshBuilder();
            var rng = new System.Random(seed);
            Vector3 O = new Vector3(0f, 0.45f, 0f);
            float camAz = Mathf.Atan2(-W.F2.y, -W.F2.x);
            for (int i = 0; i < 4; i++)
            {
                float az = camAz + (i - 1.5f) * 0.62f + ((float)rng.NextDouble() - 0.5f) * 0.3f;
                float el = 0.18f + (float)rng.NextDouble() * 0.35f;
                Vector3 prev = Vector3.zero;
                bool has = false;
                for (int j = 0; j < 5; j++)
                {
                    Vector3 dir = new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az));
                    float t = SurfaceT(O, dir, tri);
                    Vector3 pt = O + dir * (t + 0.012f);
                    if (has)
                    {
                        Vector3 along = (pt - prev).normalized;
                        Vector3 side = Vector3.Cross(dir, along).normalized;
                        float w = 0.05f * (1f - j * 0.14f);
                        crk.Quad(prev - side * w, prev + side * w, pt + side * (w * 0.8f), pt - side * (w * 0.8f), dir, crackCol, 0f);
                        if (glow)
                        {
                            float w2 = w * 0.45f;
                            Vector3 lift = dir * 0.008f;
                            crk.Quad(prev - side * w2 + lift, prev + side * w2 + lift, pt + side * w2 + lift, pt - side * w2 + lift, dir, glowCol, 0.95f);
                        }
                    }
                    prev = pt;
                    has = true;
                    el += 0.2f + 0.1f * (float)rng.NextDouble();
                    az += ((float)rng.NextDouble() - 0.5f) * 0.45f;
                }
                var part = new MeshPart();
                part.mesh = Own(crk.ToMesh(null, "Cracks" + (i + 1), out part.mats));
                outMeshes[i] = part;
            }
        }

        /// <summary>Distancia mayor a la que el rayo (desde dentro) cruza la malla; 1 si no cruza nada.</summary>
        static float SurfaceT(Vector3 o, Vector3 d, Vector3[] tri)
        {
            float best = -1f;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = tri[i], e1 = tri[i + 1] - a, e2 = tri[i + 2] - a;
                Vector3 pv = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, pv);
                if (Mathf.Abs(det) < 1e-8f) continue;
                float inv = 1f / det;
                Vector3 tv = o - a;
                float u = Vector3.Dot(tv, pv) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 qv = Vector3.Cross(tv, e1);
                float v = Vector3.Dot(d, qv) * inv;
                if (v < 0f || u + v > 1f) continue;
                float t = Vector3.Dot(e2, qv) * inv;
                if (t > best) best = t;
            }
            return best > 0f ? best : 1f;
        }

        // ------------------------------------------------------------------ Geoda gigante
        public static RockSet GetBoss()
        {
            if (bossSet != null) return bossSet;
            var rng = new System.Random(4242);
            mb.Clear();
            var cen = new List<Vector3>();
            var nrm = new List<Vector3>();
            AddBlob(Vector3.zero, 1f, rng, 4242, Biomes.H("5c4f7a"), Biomes.H("8a7aa8"), Biomes.H("b9aee0"), -1, false, false, cen, nrm);
            var set = new RockSet();
            set.body = Bake("Geoda");
            BuildCracks(set.cracks, 99, Biomes.CrystalD * 0.55f, true, Biomes.CrystalPink, cen.Count);
            bossSet = set;
            return set;
        }

        /// <summary>Un cristal de la geoda (base en el origen, apunta a +Y, alto 1, radio 1: se escala al colocar). c = 0..2.</summary>
        public static MeshPart GetBossCrystal(int c)
        {
            c = Mathf.Clamp(c, 0, 2);
            if (bossCrystal[c] != null) return bossCrystal[c];
            Color[] acc = { Biomes.CrystalM, Biomes.CrystalPink, Biomes.CrystalCyan };
            mb.Clear();
            AddKitCrystal(mb, 10 + c, Vector3.zero, Vector3.up, 1f, 1f, Color.Lerp(acc[c], Biomes.CrystalL, 0.25f), 0.38f);
            bossCrystal[c] = Bake("GeodaCristal");
            return bossCrystal[c];
        }

        // ------------------------------------------------------------------ Roca Cofre
        public static MeshPart GetChestBody()
        {
            if (chestBody != null) return chestBody;
            mb.Clear();
            mb.M = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, W.CamYaw, 0f), Vector3.one);
            Color cD = pal.RockD, cM = pal.Rock;
            mb.Blob(new Vector3(0f, 0.1f, 0f), new Vector3(1.05f, 0.34f, 0.9f), 1, 31, 0.1f, cD, cM, 0f, -0.35f);
            mb.Box(new Vector3(-0.52f, 0.46f, 0f), new Vector3(0.51f, 0.66f, 0.95f), Biomes.WoodL, 0f);
            mb.Box(new Vector3(0f, 0.46f, 0f), new Vector3(0.52f, 0.66f, 0.95f), Biomes.WoodM, 0f);
            mb.Box(new Vector3(0.52f, 0.46f, 0f), new Vector3(0.51f, 0.66f, 0.95f), Biomes.WoodD, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Box(new Vector3(s * 0.62f, 0.47f, 0f), new Vector3(0.12f, 0.70f, 1.0f), Biomes.Gold, 0.22f);
                mb.Octa(new Vector3(s * 0.62f, 0.7f, -0.52f), new Vector3(0.05f, 0.05f, 0.04f), Biomes.GoldD, 0.1f);
                mb.Octa(new Vector3(s * 0.62f, 0.24f, -0.52f), new Vector3(0.05f, 0.05f, 0.04f), Biomes.GoldD, 0.1f);
            }
            mb.Box(new Vector3(0f, 0.82f, 0f), new Vector3(1.64f, 0.07f, 1.04f), Biomes.Gold, 0.2f);
            mb.Box(new Vector3(0f, 0.70f, -0.5f), new Vector3(0.30f, 0.36f, 0.1f), Biomes.Gold, 0.25f);
            mb.Box(new Vector3(0f, 0.68f, -0.56f), new Vector3(0.07f, 0.16f, 0.03f), Biomes.WoodD, 0f);
            mb.Blob(new Vector3(-0.68f, 0.1f, -0.52f), new Vector3(0.32f, 0.2f, 0.26f), 0, 71, 0.15f, cD, cM, 0f, -0.2f);
            mb.Blob(new Vector3(0.68f, 0.1f, -0.52f), new Vector3(0.32f, 0.2f, 0.26f), 0, 72, 0.15f, cD, cM, 0f, -0.2f);
            chestBody = Bake("CofreCuerpo");
            return chestBody;
        }

        /// <summary>Tapa del cofre; el origen es la base de la tapa (se coloca a y = 0.85 del cofre).</summary>
        public static MeshPart GetChestLid()
        {
            if (chestLid != null) return chestLid;
            mb.Clear();
            mb.M = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, W.CamYaw, 0f), Vector3.one);
            mb.Blob(new Vector3(0f, 0f, 0f), new Vector3(0.84f, 0.34f, 0.52f), 1, 0, 0f, Biomes.WoodD, Biomes.WoodL, 0f, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Blob(new Vector3(s * 0.62f, 0.0f, 0f), new Vector3(0.07f, 0.36f, 0.55f), 0, 0, 0f, Biomes.GoldD, Biomes.Gold, 0.25f, 0f);
            }
            chestLid = Bake("CofreTapa");
            return chestLid;
        }

        // ------------------------------------------------------------------ reloj (rocas reloj de Excavar)
        public static MeshPart GetClock()
        {
            if (clockPart != null) return clockPart;
            mb.Clear();
            mb.M = Matrix4x4.TRS(Vector3.zero, W.CamRot, Vector3.one);
            Color rim = Biomes.H("7fe0ff");
            mb.Cyl(new Vector3(0f, 0f, 0.02f), new Vector3(0f, 0f, -0.12f), 0.5f, 0.5f, 14, rim, 0.45f);
            mb.Cyl(new Vector3(0f, 0f, -0.05f), new Vector3(0f, 0f, -0.15f), 0.4f, 0.4f, 14, Biomes.H("f4f4ff"), 0.25f);
            mb.Box(new Vector3(0f, 0.12f, -0.17f), new Vector3(0.05f, 0.24f, 0.03f), Biomes.SteelD, 0f);
            mb.Box(new Vector3(0.09f, 0f, -0.17f), new Vector3(0.18f, 0.05f, 0.03f), Biomes.SteelD, 0f);
            // el reloj no depende del bioma: se conserva al cambiar de mundo (se crea una sola vez)
            var part = new MeshPart();
            part.mesh = mb.ToMesh(null, "Reloj", out part.mats);
            clockPart = part;
            return clockPart;
        }

        // ------------------------------------------------------------------ meteorito
        static void BuildMeteor()
        {
            mb.Clear();
            Color rockC = Biomes.H("5a3a30");
            mb.Blob(Vector3.zero, new Vector3(1f, 1f, 1f), 1, 3, 0.2f, rockC, rockC, 0f, -9f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 d = new Vector3(Nz.Hash01(i, 1, 2) - 0.5f, Nz.Hash01(i, 2, 2) - 0.5f, Nz.Hash01(i, 3, 2) - 0.5f).normalized;
                mb.Octa(d * 0.92f, new Vector3(0.22f, 0.22f, 0.22f), Biomes.H("ff9a3a"), 0.95f);
            }
            var part = new MeshPart();
            part.mesh = mb.ToMesh(null, "Meteorito", out part.mats);
            meteorPart = part;
        }
    }
}
