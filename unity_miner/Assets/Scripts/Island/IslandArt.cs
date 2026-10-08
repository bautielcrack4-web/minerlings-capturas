using System.Collections.Generic;
using Mineros.Core;
using Mineros.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.IslandView
{
    /// <summary>
    /// Arte de la Isla Minera generado por codigo (low-poly, colores planos): terreno, mar, edificios por nivel, vetas y
    /// bulto de carga. Todo sale de MeshBuilder + VertexColorMerge (pocas llamadas de dibujo).
    /// </summary>
    public static partial class IslandArt
    {
        public static Color H(string h) { return Biomes.H(h); }

        // paleta de la isla
        // colores del bioma (cambian con cada isla nueva: SetBiome)
        public static Color Grass = H("8cc247"), GrassD = H("76ab3a"), Sand = H("f3d99a"), SandD = H("dcbc78");
        public static Color SeaC = H("39b6e0"), SeaD = H("1f8fc4");
        public static Color SeaShallow = new Color(0.30f, 0.82f, 0.90f), SeaDeep = new Color(0.06f, 0.40f, 0.72f);
        public static int PropBiome;
        public static readonly Color Foam = H("e9fbff");

        /// <summary>
        /// Paleta de cada isla: 0 Verde, 1 Dunas (arena dorada), 2 Volcanica (ceniza y arena negra), 3 Cristal (menta y
        /// lila), 4 Nevada. Mismo valor y saturacion que la verde (biblia 1.3) para que todas se sientan del mismo juego.
        /// </summary>
        public static void SetBiome(int b)
        {
            string[][] pal =
            {
                new[] { "8cc247", "76ab3a", "f3d99a", "dcbc78", "39b6e0", "1f8fc4", "4dd1e6", "0f66b8", "e2c48e", "cfa96f", "b58d58" },
                new[] { "d9c27a", "c4a95e", "f6e3b0", "e2c78a", "34c2cf", "1a8fb0", "52d6d6", "0e74a8", "d9a865", "c08a4a", "9e6c36" },
                new[] { "7d7a6a", "6a6758", "4d4542", "3a3331", "3a9cc0", "1b5f8a", "4aa6c4", "0c3866", "9a6a52", "84553f", "6b4232" },
                new[] { "9fe3d6", "86cfc2", "ece6ff", "d2c8f2", "7ab8f0", "4a5fc0", "8fd0ff", "3b40a8", "c9bdf0", "ad9fe2", "8f80c8" },
                new[] { "eef4fa", "d4e2ec", "e3eaf0", "c6d4de", "5cb4d8", "2a6a9a", "7cc8e4", "17508a", "b8c6d6", "9eacbe", "8292a6" },
            };
            var p = pal[((b % pal.Length) + pal.Length) % pal.Length];
            Grass = H(p[0]); GrassD = H(p[1]); Sand = H(p[2]); SandD = H(p[3]); SeaC = H(p[4]); SeaD = H(p[5]);
            SeaShallow = H(p[6]); SeaDeep = H(p[7]);
            PropBiome = b == 1 ? 1 : b == 2 ? 3 : b == 3 ? 2 : 0;
            IslandGround.SetDirt(H(p[8]), H(p[9]), H(p[10]));
        }
        public static readonly Color Wall = H("f6ead2"), WallD = H("dcc8a4"), Wood = H("a8703f"), WoodD = H("7a4a2a");
        public static readonly Color RoofRed = H("e0594a"), RoofBlue = H("4a8fe0"), RoofGreen = H("5cbf63"), RoofDark = H("5b5f6a");
        public static readonly Color Stone = H("9aa1aa"), StoneD = H("6d747e"), Glass = H("bfe8ff"), Dark = H("3b2a1e");
        public static readonly Color[] OreCol = { H("9aa1aa"), H("e0884a"), H("b8c4d0"), H("ffd23a"), H("8fd8ff"), H("3d3d44"), H("6ff0ff"), H("b06ef0") };

        public static MeshRenderer MakeRenderer(Transform parent, string name, Mesh mesh, Material[] mats, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        public static MeshRenderer Bake(MeshBuilder mb, Transform parent, string name, bool shadows = true)
        {
            Material[] mats;
            Mesh m = mb.ToMesh(null, name, out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return MakeRenderer(parent, name, m, mats, shadows);
        }

        // ------------------------------------------------------------ terreno
        /// <summary>Borde irregular de la isla (radio en funcion del angulo).</summary>
        public static float EdgeR(float a, float r)
        {
            return r * (1f + 0.045f * Mathf.Sin(a * 3f + 0.7f) + 0.03f * Mathf.Sin(a * 5f + 2.1f) + 0.02f * Mathf.Sin(a * 9f));
        }

        public static Transform Terrain(Transform parent, float radius, bool paintedTop = true)
        {
            var mb = new MeshBuilder();
            const int N = 72;
            float rGrass = radius + 0.6f, rSand = radius + 2.0f;
            float yG = 0f, yS = -0.28f, yBottom = -1.2f;
            var c = new Vector3(0, yG, 0);
            for (int i = 0; i < N; i++)
            {
                float a0 = Mathf.PI * 2f * i / N, a1 = Mathf.PI * 2f * (i + 1) / N;
                Vector3 g0 = P(a0, EdgeR(a0, rGrass), yG), g1 = P(a1, EdgeR(a1, rGrass), yG);
                Vector3 s0 = P(a0, EdgeR(a0 + 0.4f, rSand), yS), s1 = P(a1, EdgeR(a1 + 0.4f, rSand), yS);
                Vector3 b0 = P(a0, EdgeR(a0 + 0.4f, rSand) + 0.6f, yBottom), b1 = P(a1, EdgeR(a1 + 0.4f, rSand) + 0.6f, yBottom);
                if (!paintedTop) mb.Tri(c, g1, g0, Vector3.up, Grass, 0f);   // con suelo pintado la cara de arriba es otra malla
                // talud corto de pasto a arena
                Vector3 m0 = P(a0, EdgeR(a0, rGrass) + 0.25f, yG - 0.18f), m1 = P(a1, EdgeR(a1, rGrass) + 0.25f, yG - 0.18f);
                mb.Quad(g0, g1, m1, m0, Vector3.up, GrassD, 0f);
                mb.Quad(m0, m1, s1, s0, Vector3.up, Sand, 0f);
                mb.Quad(s0, s1, b1, b0, P(a0, 1f, 0f), SandD, 0f);
            }
            var rnd = new System.Random(5);
            for (int i = 0; i < (paintedTop ? 0 : 26); i++)
            {
                float a = (float)(rnd.NextDouble() * Mathf.PI * 2), d = (float)(Mathf.Sqrt((float)rnd.NextDouble()) * (radius - 1.5f));
                mb.Disc(new Vector3(Mathf.Cos(a) * d, 0.01f, Mathf.Sin(a) * d), 0.6f + (float)rnd.NextDouble() * 1.1f, 9,
                    Color.Lerp(Grass, GrassD, 0.35f + (float)rnd.NextDouble() * 0.3f), 0f);
            }
            var isla = Bake(mb, parent, "Isla", false);
            isla.receiveShadows = true;
            return isla.transform;
        }

        static Vector3 P(float a, float r, float y) { return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }

        /// <summary>Anillo de espuma alrededor de la isla (late con las olas).</summary>
        public static Transform FoamRing(Transform parent, float radius)
        {
            var mb = new MeshBuilder();
            const int N = 72;
            float r0 = radius + 2.0f, r1 = r0 + 0.9f;
            for (int i = 0; i < N; i++)
            {
                float a0 = Mathf.PI * 2f * i / N, a1 = Mathf.PI * 2f * (i + 1) / N;
                mb.Quad(P(a0, EdgeR(a0 + 0.4f, r0), -0.36f), P(a1, EdgeR(a1 + 0.4f, r0), -0.36f),
                        P(a1, EdgeR(a1 + 0.4f, r1), -0.36f), P(a0, EdgeR(a0 + 0.4f, r1), -0.36f), Vector3.up, Foam, 0.2f);
            }
            return Bake(mb, parent, "Espuma", false).transform;
        }

        /// <summary>Mar: grilla con color por vertice (mas claro cerca de la orilla); IslandAmbient mueve las olas.</summary>
        public static Mesh SeaGrid(int n, float size, float shoreR, out Material mat)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    float px = (x / (float)n - 0.5f) * size, pz = (z / (float)n - 0.5f) * size;
                    v.Add(new Vector3(px, -0.45f, pz));
                    float d = Mathf.Sqrt(px * px + pz * pz);
                    float k = Mathf.Clamp01((d - shoreR) / 9f);
                    c.Add(Color.Lerp(H("6fd8f0"), SeaD, k).linear);
                }
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int i = z * (n + 1) + x;
                    t.Add(i); t.Add(i + n + 1); t.Add(i + 1);
                    t.Add(i + 1); t.Add(i + n + 1); t.Add(i + n + 2);
                }
            var m = new Mesh { name = "Mar" };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.MarkDynamic();
            var sh = Shader.Find("Mineros/IslandSea");
            mat = new Material(sh != null ? sh : Shader.Find("Mineros/MinerToonVC")) { name = "Mar" };
            mat.SetFloat("_ShoreR", shoreR - 2f);
            // las olas se mueven en el shader: los limites de la malla crecen para que no se recorte
            m.bounds = new Bounds(Vector3.zero, new Vector3(size, 2f, size));
            return m;
        }

        // ------------------------------------------------------------ sombra de contacto
        static Texture2D blobTex;
        static Material blobMat;
        static Mesh blobMesh;

        /// <summary>Mancha de sombra suave en el suelo (oclusion barata bajo edificios, mineros, vetas y arboles).</summary>
        public static Transform Blob(Transform parent, float radius, float alpha = 0.42f)
        {
            if (blobTex == null)
            {
                const int N = 128;
                blobTex = new Texture2D(N, N, TextureFormat.Alpha8, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Blob" };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        px[y * N + x] = new Color32(0, 0, 0, (byte)(a * 255));
                    }
                blobTex.SetPixels32(px);
                blobTex.Apply(false, true);
                var sh = Shader.Find("Mineros/Blob");
                blobMat = new Material(sh) { name = "Blob", mainTexture = blobTex };
                blobMesh = new Mesh { name = "Blob" };
                blobMesh.vertices = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, -1) };
                blobMesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
                blobMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                blobMesh.RecalculateBounds();
            }
            var go = new GameObject("SombraContacto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            go.transform.localScale = new Vector3(radius, 1f, radius);
            go.AddComponent<MeshFilter>().sharedMesh = blobMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (alpha >= 0.999f) r.sharedMaterial = blobMat;
            else
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", new Color(0.08f, 0.12f, 0.04f, alpha));
                r.sharedMaterial = blobMat;
                r.SetPropertyBlock(mpb);
            }
            return go.transform;
        }

        /// <summary>Decoracion fija: arboles, arbustos, flores y piedritas en el borde (lejos de parcelas).</summary>
        /// <summary>Devuelve los arboles y arbustos (para que se mezan con el viento).</summary>
        public static List<Transform> Decor(Transform parent, Island isl, float radius)
        {
            var sway = new List<Transform>();
            var rnd = new System.Random(42);
            isl.Blockers.Clear();
            // lo que no se mueve (flores, pasto, piedritas) va junto y se combina en pocas mallas (mismo aspecto, menos dibujo)
            var fixedRoot = new GameObject("DecoFija").transform;
            fixedRoot.SetParent(parent, false);
            string[] kinds = { "tree", "tree", "bush", "flower", "flower", "grass", "pebble" };
            int placed = 0;
            for (int t = 0; t < 600 && placed < 30 + (int)(radius * 1.5f); t++)
            {
                float a = (float)(rnd.NextDouble() * Mathf.PI * 2);
                float d = Mathf.Lerp(radius - 1.6f, radius + 0.3f, (float)rnd.NextDouble());
                if (rnd.NextDouble() < 0.25) d = (float)rnd.NextDouble() * radius;
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                string k = kinds[rnd.Next(kinds.Length)];
                bool big = k == "tree";
                if (!isl.FreeSpot(x, z, big ? 1.2f : 0.5f)) continue;
                if (big && d < radius - 2.2f) continue;   // arboles solo en la orilla: el centro queda para jugar
                bool moves = big || k == "bush";
                var go = Mineros.Art.ArtKit.Prop(k, PropBiome, rnd.Next(1000), moves ? parent : fixedRoot);
                if (go == null) continue;
                go.transform.localPosition = new Vector3(x, 0f, z);
                go.transform.localRotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                float s = big ? Mathf.Lerp(0.8f, 1.15f, (float)rnd.NextDouble()) : Mathf.Lerp(0.8f, 1.2f, (float)rnd.NextDouble());
                go.transform.localScale = Vector3.one * s;
                if (big || k == "bush") { Blob(go.transform, big ? 1.5f : 0.8f, big ? 0.38f : 0.3f); isl.AddBlocker(x, z, (big ? 1.3f : 0.7f) * s); sway.Add(go.transform); }
                placed++;
            }
            bool noOpt = Application.isBatchMode && System.Environment.GetEnvironmentVariable("PERF_MODE") == "0";
            if (!noOpt)
            {
                foreach (var r in fixedRoot.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = ShadowCastingMode.Off;   // flores y piedritas: su sombra no se ve y cuesta un dibujo cada una
                StaticBatchingUtility.Combine(fixedRoot.gameObject);
            }
            return sway;
        }

        // ------------------------------------------------------------ parcela vacia
        public static void PlotPad(MeshBuilder mb, Vector3 c)
        {
            mb.Disc(c + Vector3.up * 0.02f, 1.25f, 20, GrassD, 0f);
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.PI * 2f * i / 8f;
                mb.Box(c + new Vector3(Mathf.Cos(a) * 1.2f, 0.08f, Mathf.Sin(a) * 1.2f), new Vector3(0.18f, 0.16f, 0.18f), Wood, 0f);
            }
        }

        // ------------------------------------------------------------ edificios
        // ------------------------------------------------------------ edificios de Tripo (Resources/Island/<nombre>.json + .png)
        [System.Serializable] sealed class MeshJson { public float[] pos; public float[] nrm; public float[] uv; public int[] idx; public float height; public int emis; public AnchorJson[] anchors; }
        [System.Serializable] sealed class AnchorJson { public string n; public float[] p; public float[] d; }

        /// <summary>Ancla de una pieza del kit (vapor, aguja...): nombre, punto y direccion en coordenadas de la malla.</summary>
        public struct KitAnchor { public string N; public Vector3 P, D; }

        static readonly Dictionary<string, KitAnchor[]> tripoAnchors = new Dictionary<string, KitAnchor[]>();

        /// <summary>Anclas de un modelo cargado con TripoModel (vacio si no tiene).</summary>
        public static KitAnchor[] TripoAnchors(string file)
        {
            KitAnchor[] a;
            if (tripoAnchors.TryGetValue(file, out a)) return a;
            Material mm;
            TripoModel(file, out mm);
            return tripoAnchors.TryGetValue(file, out a) ? a : new KitAnchor[0];
        }

        static readonly Dictionary<string, Mesh> tripoMesh = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> tripoMat = new Dictionary<string, Material>();

        public static string FileOf(BKind k)
        {
            switch (k)
            {
                case BKind.House: return "house";
                case BKind.Depot: return "depot";
                case BKind.Canteen: return "canteen";
                case BKind.Showers: return "showers";
                case BKind.Mine: return "mine";
                case BKind.Smithy: return "smithy";
                case BKind.Lighthouse: return "lighthouse";
                case BKind.Dock: return "dock";
                default: return "";
            }
        }

        /// <summary>Etapa de modelo que existe para ese edificio (las etapas 2 y 3 son modelos distintos; la 4 usa el 3).</summary>
        public static int ModelStage(BKind k, int tier)
        {
            for (int t = Mathf.Min(tier, 3); t > 1; t--)
                if (Resources.Load<TextAsset>("Island/" + FileOf(k) + "_" + t) != null) return t;
            return 1;
        }

        /// <summary>Modelo de Tripo del edificio en su etapa (null si no esta en Resources).</summary>
        public static Mesh TripoBuilding(BKind k, out Material mat) { return TripoBuilding(k, 1, out mat); }

        public static Mesh TripoBuilding(BKind k, int tier, out Material mat)
        {
            mat = null;
            if (FileOf(k) == "") return null;
            int st = ModelStage(k, tier);
            return TripoModel(st > 1 ? FileOf(k) + "_" + st : FileOf(k), out mat);
        }

        /// <summary>Carga un modelo de Tripo por nombre de archivo (malla compartida + material con textura).</summary>
        // ------------------------------------------------------------ atlas del kit de habitaciones
        static Texture2D kitAtlas;
        static Material kitMat;
        static readonly Dictionary<string, RectInt> kitSlots = new Dictionary<string, RectInt>();
        static bool kitAtlasTried;

        /// <summary>
        /// Todas las paletas de Resources/RoomKit (32 px de ancho, celdas de 8 px) apiladas en una textura. Si alguna no es
        /// legible o no mide 32 de ancho, esa pieza sigue con su material propio (camino de antes).
        /// </summary>
        static void BuildKitAtlas()
        {
            kitAtlasTried = true;
            var texs = Resources.LoadAll<Texture2D>("RoomKit");
            var ok = new List<Texture2D>();
            int h = 0;
            foreach (var t in texs) if (t != null && t.width == 32) { ok.Add(t); h += (t.height + 7) / 8 * 8; }
            if (ok.Count == 0) { Debug.Log("kitatlas sin paletas (" + texs.Length + ")"); return; }
            int H = 8; while (H < h) H *= 2;
            kitAtlas = new Texture2D(32, H, TextureFormat.RGBA32, false) { name = "KitAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color32[32 * H];
            kitAtlas.SetPixels32(clear);
            int y = 0;
            foreach (var t in ok)
            {
                kitAtlas.SetPixels32(0, y, 32, t.height, Pixels(t));
                kitSlots[t.name] = new RectInt(0, y, 32, t.height);
                y += (t.height + 7) / 8 * 8;
            }
            kitAtlas.Apply(false, true);
            Debug.Log("kitatlas paletas=" + ok.Count + "/" + texs.Length + " alto=" + H);
        }

        /// <summary>Pixeles de una textura aunque no sea legible (copia por GPU, misma medida, sin filtro).</summary>
        static Color32[] Pixels(Texture2D t)
        {
            if (t.isReadable) return t.GetPixels32();
            var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(t, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tmp = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
            tmp.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tmp.GetPixels32();
            Object.Destroy(tmp);
            return px;
        }

        static bool KitAtlasSlot(Texture2D tex, out RectInt slot)
        {
            slot = default(RectInt);
            if (!kitAtlasTried) BuildKitAtlas();
            return kitAtlas != null && tex != null && kitSlots.TryGetValue(tex.name, out slot);
        }

        static Material KitMaterial(Shader sh)
        {
            if (kitMat != null) return kitMat;
            kitMat = new Material(sh) { name = "Edificio_KitAtlas", mainTexture = kitAtlas };
            kitMat.SetFloat("_Floor", 0.55f);
            kitMat.SetFloat("_Rim", 0.15f);
            kitMat.SetFloat("_EmisRows", 0f);
            kitMat.SetFloat("_KitAtlas", 1f);
            return kitMat;
        }

        public static Mesh TripoModel(string file, out Material mat)
        {
            Mesh m;
            if (tripoMesh.TryGetValue(file, out m) && m != null) { mat = tripoMat[file]; return m; }
            mat = null;
            string path = file.IndexOf('/') >= 0 ? file : "Island/" + file;   // RoomKit/oro_pared, etc.
            var ta = Resources.Load<TextAsset>(path);
            var tex = Resources.Load<Texture2D>(path);
            var sh = Shader.Find("Mineros/MinerToonTex");
            if (ta == null || tex == null || sh == null) return null;
            var d = JsonUtility.FromJson<MeshJson>(ta.text);
            int n = d.pos.Length / 3;
            var v = new Vector3[n]; var nr = new Vector3[n]; var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(d.pos[i * 3], d.pos[i * 3 + 1], d.pos[i * 3 + 2]);
                nr[i] = new Vector3(d.nrm[i * 3], d.nrm[i * 3 + 1], d.nrm[i * 3 + 2]);
                uv[i] = new Vector2(d.uv[i * 2], d.uv[i * 2 + 1]);
            }
            m = new Mesh { name = "Tripo_" + file };
            m.indexFormat = n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            // piezas del kit: todas sobre un atlas (un material para todo el Complejo = una llamada por piso en vez de
            // una por archivo); la fila emisiva viaja en el uv2 (pixel local de su paleta, filas emisivas de la pieza)
            RectInt slot = default(RectInt);
            bool atlas = path.StartsWith("RoomKit/") && KitAtlasSlot(tex, out slot);
            if (atlas)
            {
                var uv2 = new Vector2[n];
                float H = kitAtlas.height;
                for (int i = 0; i < n; i++)
                {
                    float lv = uv[i].y * slot.height;
                    uv2[i] = new Vector2(lv, d.emis);
                    uv[i] = new Vector2(uv[i].x, (slot.y + lv) / H);
                }
                m.vertices = v; m.normals = nr; m.uv = uv; m.uv2 = uv2; m.triangles = d.idx;
            }
            else { m.vertices = v; m.normals = nr; m.uv = uv; m.triangles = d.idx; }
            m.RecalculateBounds();
            m.UploadMeshData(path.StartsWith("RoomKit/") ? false : true);   // las piezas del kit se combinan por piso: legibles
            if (atlas) mat = KitMaterial(sh);
            else
            {
                mat = new Material(sh) { name = "Edificio_" + file };
                mat.mainTexture = tex;
                mat.SetFloat("_Floor", 0.55f);
                mat.SetFloat("_Rim", 0.15f);
                mat.SetFloat("_EmisRows", d.emis);   // filas de abajo de la paleta que laten (kit_lib.py, glow)
            }
            var anc = new KitAnchor[d.anchors != null ? d.anchors.Length : 0];
            for (int i = 0; i < anc.Length; i++)
            {
                var aj = d.anchors[i];
                anc[i] = new KitAnchor { N = aj.n, P = new Vector3(aj.p[0], aj.p[1], aj.p[2]), D = new Vector3(aj.d[0], aj.d[1], aj.d[2]) };
            }
            tripoAnchors[file] = anc;
            tripoMesh[file] = m;
            tripoMat[file] = mat;
            return m;
        }

        /// <summary>Malla del edificio (centrado en 0, frente hacia -Z, que es hacia la camara). Crece con el nivel.</summary>
        public static Mesh Building(BKind k, int level, out Material[] mats)
        {
            var mb = new MeshBuilder();
            switch (k)
            {
                case BKind.House: House(mb, level); break;
                case BKind.Depot: Depot(mb, level); break;
                case BKind.Canteen: Canteen(mb, level); break;
                case BKind.Showers: Showers(mb, level); break;
                case BKind.Smithy: Smithy(mb, level); break;
            }
            Mesh m = mb.ToMesh(null, k + "_" + level, out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }

        static void Roof(MeshBuilder mb, Vector3 c, float w, float d, float h, Color col)
        {
            // techo a dos aguas con alero
            float hw = w * 0.5f + 0.12f, hd = d * 0.5f + 0.12f;
            Vector3 a = c + new Vector3(-hw, 0, -hd), b = c + new Vector3(hw, 0, -hd), e = c + new Vector3(hw, 0, hd), f = c + new Vector3(-hw, 0, hd);
            Vector3 r0 = c + new Vector3(-hw, h, 0), r1 = c + new Vector3(hw, h, 0);
            mb.Quad(a, r0, r1, b, new Vector3(0, 1, -1), col, 0f);
            mb.Quad(e, r1, r0, f, new Vector3(0, 1, 1), Color.Lerp(col, Dark, 0.15f), 0f);
            mb.Tri(a, f, r0, Vector3.left, Color.Lerp(col, Dark, 0.25f), 0f);
            mb.Tri(b, r1, e, Vector3.right, Color.Lerp(col, Dark, 0.25f), 0f);
        }

        static void Window(MeshBuilder mb, Vector3 c)
        {
            mb.Box(c, new Vector3(0.42f, 0.42f, 0.06f), WoodD, 0f);
            mb.Box(c + new Vector3(0, 0, -0.02f), new Vector3(0.32f, 0.32f, 0.06f), Glass, 0.25f);
        }

        static void House(MeshBuilder mb, int lv)
        {
            float w = 2.0f + 0.1f * Mathf.Min(lv, 4), d = 1.8f, h = 1.2f;
            int floors = lv >= 4 ? 2 : 1;
            mb.Box(new Vector3(0, 0.08f, 0), new Vector3(w + 0.3f, 0.16f, d + 0.3f), Stone, 0f);
            for (int f = 0; f < floors; f++)
            {
                float y = 0.16f + f * h;
                mb.Box(new Vector3(0, y + h * 0.5f, 0), new Vector3(w, h, d), f == 0 ? Wall : Color.Lerp(Wall, WallD, 0.4f), 0f);
                Window(mb, new Vector3(-w * 0.28f, y + h * 0.58f, -d * 0.5f));
                Window(mb, new Vector3(w * 0.28f, y + h * 0.58f, -d * 0.5f));
            }
            mb.Box(new Vector3(0, 0.16f + 0.42f, -d * 0.5f - 0.02f), new Vector3(0.5f, 0.84f, 0.08f), WoodD, 0f);   // puerta
            Roof(mb, new Vector3(0, 0.16f + floors * h, 0), w, d, 0.85f, RoofRed);
            mb.Box(new Vector3(w * 0.3f, 0.16f + floors * h + 0.6f, 0.3f), new Vector3(0.28f, 0.7f, 0.28f), StoneD, 0f);   // chimenea
            // carteles de nivel: un banderin por nivel en el frente
            for (int i = 0; i < Mathf.Min(lv, 8); i++)
                mb.Box(new Vector3(-w * 0.5f + 0.2f + i * 0.22f, 0.16f + floors * h - 0.08f, -d * 0.5f - 0.13f), new Vector3(0.14f, 0.16f, 0.03f),
                    i % 2 == 0 ? Kit3.Yellow : Kit3.Blue, 0.1f);
        }

        static void Depot(MeshBuilder mb, int lv)
        {
            float w = 2.6f, d = 2.0f, h = 1.3f;
            mb.Box(new Vector3(0, 0.08f, 0), new Vector3(w + 0.4f, 0.16f, d + 0.4f), Stone, 0f);
            mb.Box(new Vector3(0, 0.16f + h * 0.5f, 0), new Vector3(w, h, d), Wood, 0f);
            for (int i = -2; i <= 2; i++) mb.Box(new Vector3(i * 0.55f, 0.16f + h * 0.5f, -d * 0.5f - 0.01f), new Vector3(0.06f, h, 0.04f), WoodD, 0f);
            mb.Box(new Vector3(0, 0.16f + 0.55f, -d * 0.5f - 0.04f), new Vector3(1.0f, 1.1f, 0.06f), WoodD, 0f);   // porton
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.9f, RoofGreen);
            // cajas, barriles y pila de mineral (crece con el nivel)
            mb.Box(new Vector3(w * 0.5f + 0.35f, 0.3f, -0.4f), new Vector3(0.5f, 0.5f, 0.5f), Wood, 0f);
            mb.Cyl(new Vector3(-w * 0.5f - 0.35f, 0f, -0.5f), new Vector3(-w * 0.5f - 0.35f, 0.62f, -0.5f), 0.24f, 0.24f, 10, WoodD, 0f);
            int pile = Mathf.Min(2 + lv, 9);
            for (int i = 0; i < pile; i++)
            {
                float a = i * 2.4f;
                mb.Octa(new Vector3(w * 0.5f + 0.2f + Mathf.Cos(a) * 0.35f, 0.15f + (i / 4) * 0.18f, 0.5f + Mathf.Sin(a) * 0.35f),
                    Vector3.one * 0.28f, OreCol[Mathf.Min(i % 3 + lv / 3, 4)], 0.05f);
            }
        }

        static void Canteen(MeshBuilder mb, int lv)
        {
            float w = 2.2f, d = 1.7f, h = 1.15f;
            mb.Box(new Vector3(0, 0.08f, 0), new Vector3(w + 0.3f, 0.16f, d + 0.3f), Stone, 0f);
            mb.Box(new Vector3(0, 0.16f + h * 0.5f, 0.1f), new Vector3(w, h, d), Wall, 0f);
            Window(mb, new Vector3(-0.5f, 0.75f, -d * 0.5f + 0.1f));
            mb.Box(new Vector3(0.45f, 0.58f, -d * 0.5f + 0.08f), new Vector3(0.5f, 0.84f, 0.08f), WoodD, 0f);
            // toldo a rayas
            for (int i = 0; i < 6; i++)
            {
                float x0 = -w * 0.5f + i * w / 6f;
                Color c = i % 2 == 0 ? RoofRed : Wall;
                mb.Quad(new Vector3(x0, 1.35f, -d * 0.5f + 0.1f), new Vector3(x0 + w / 6f, 1.35f, -d * 0.5f + 0.1f),
                        new Vector3(x0 + w / 6f, 1.05f, -d * 0.5f - 0.55f), new Vector3(x0, 1.05f, -d * 0.5f - 0.55f), new Vector3(0, 1, -1), c, 0f);
            }
            Roof(mb, new Vector3(0, 0.16f + h, 0.1f), w, d, 0.6f, RoofDark);
            // mesas afuera (mas con el nivel)
            int tables = Mathf.Min(1 + lv / 2, 3);
            for (int t = 0; t < tables; t++)
            {
                var c = new Vector3(-0.8f + t * 0.8f, 0f, -d * 0.5f - 1.0f);
                mb.Cyl(c, c + Vector3.up * 0.42f, 0.05f, 0.05f, 6, WoodD, 0f);
                mb.Cyl(c + Vector3.up * 0.42f, c + Vector3.up * 0.47f, 0.32f, 0.32f, 12, Wood, 0f);
                mb.Cyl(c + Vector3.up * 0.47f, c + Vector3.up * 0.52f, 0.08f, 0.06f, 8, RoofRed, 0.1f);
            }
            mb.Box(new Vector3(-w * 0.5f + 0.1f, 1.7f, -d * 0.5f), new Vector3(0.7f, 0.35f, 0.06f), Kit3.Yellow, 0.05f);   // cartel
        }

        static void Showers(MeshBuilder mb, int lv)
        {
            float w = 1.9f, d = 1.5f, h = 1.25f;
            mb.Box(new Vector3(0, 0.08f, 0), new Vector3(w + 0.3f, 0.16f, d + 0.3f), H("d8e8f0"), 0f);
            mb.Box(new Vector3(0, 0.16f + h * 0.5f, 0), new Vector3(w, h, d), H("eaf6fb"), 0f);
            // azulejos celestes al frente
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 2; j++)
                    if ((i + j) % 2 == 0)
                        mb.Box(new Vector3(-0.68f + i * 0.45f, 0.4f + j * 0.45f, -d * 0.5f - 0.01f), new Vector3(0.4f, 0.4f, 0.03f), H("7cc6ea"), 0f);
            mb.Box(new Vector3(0, 0.16f + h + 0.06f, 0), new Vector3(w + 0.2f, 0.12f, d + 0.2f), RoofBlue, 0f);
            // tanque de agua y caño
            mb.Cyl(new Vector3(0.4f, 0.16f + h + 0.12f, 0.1f), new Vector3(0.4f, 0.16f + h + 0.85f, 0.1f), 0.42f, 0.42f, 14, RoofBlue, 0f);
            mb.Cyl(new Vector3(0.4f, 0.16f + h + 0.85f, 0.1f), new Vector3(0.4f, 0.16f + h + 0.95f, 0.1f), 0.42f, 0.2f, 14, H("2f6fb8"), 0f);
            mb.Cyl(new Vector3(-0.55f, 0.16f + h, -d * 0.5f), new Vector3(-0.55f, 0.16f + h + 0.4f, -d * 0.5f - 0.3f), 0.06f, 0.06f, 8, Stone, 0f);
            // cortina por cubiculo
            int cub = Mathf.Min(1 + lv / 2, 3);
            for (int i = 0; i < cub; i++)
                mb.Box(new Vector3(-w * 0.5f - 0.05f, 0.8f, -0.4f + i * 0.45f), new Vector3(0.05f, 0.9f, 0.38f), i % 2 == 0 ? H("f59ab0") : Kit3.Yellow, 0f);
        }

        static void Smithy(MeshBuilder mb, int lv)
        {
            float w = 2.2f, d = 1.8f, h = 1.2f;
            mb.Box(new Vector3(0, 0.08f, 0), new Vector3(w + 0.3f, 0.16f, d + 0.3f), StoneD, 0f);
            mb.Box(new Vector3(0, 0.16f + h * 0.5f, 0), new Vector3(w, h, d), Stone, 0f);
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.7f, RoofDark);
            mb.Box(new Vector3(-0.6f, 0.16f + h + 0.7f, 0.2f), new Vector3(0.45f, 1.4f, 0.45f), StoneD, 0f);   // chimenea
            mb.Box(new Vector3(-0.6f, 0.16f + h + 1.42f, 0.2f), new Vector3(0.3f, 0.06f, 0.3f), H("ff8a3a"), 1f);   // brasa
            mb.Box(new Vector3(0.3f, 0.6f, -d * 0.5f - 0.01f), new Vector3(0.9f, 0.7f, 0.05f), H("ff8a3a"), 0.9f);   // fragua (luz)
            // yunque afuera
            var a = new Vector3(-0.2f, 0f, -d * 0.5f - 0.7f);
            mb.Box(a + new Vector3(0, 0.2f, 0), new Vector3(0.3f, 0.4f, 0.3f), WoodD, 0f);
            mb.Box(a + new Vector3(0, 0.48f, 0), new Vector3(0.55f, 0.16f, 0.25f), H("4b4f58"), 0f);
            for (int i = 0; i < Mathf.Min(lv, 5); i++)
                mb.Box(new Vector3(w * 0.5f + 0.2f, 0.3f + i * 0.1f, -0.6f + i * 0.25f), new Vector3(0.1f, 0.6f, 0.1f), OreCol[Mathf.Min(i, 4)], 0.15f);
        }

        // ------------------------------------------------------------ faro, muelle, barco, nube
        static void Lighthouse(MeshBuilder mb, int lv)
        {
            mb.Cyl(new Vector3(0, 0f, 0), new Vector3(0, 0.3f, 0), 1.1f, 1.05f, 14, Stone, 0f);
            int bands = 5;
            float h = 3.4f + 0.15f * lv;
            for (int i = 0; i < bands; i++)
            {
                float y0 = 0.3f + h * i / bands, y1 = 0.3f + h * (i + 1) / bands;
                float r0 = Mathf.Lerp(0.78f, 0.5f, i / (float)bands), r1 = Mathf.Lerp(0.78f, 0.5f, (i + 1) / (float)bands);
                mb.Cyl(new Vector3(0, y0, 0), new Vector3(0, y1, 0), r0, r1, 16, i % 2 == 0 ? H("f4f1ea") : RoofRed, 0f);
            }
            float top = 0.3f + h;
            mb.Cyl(new Vector3(0, top, 0), new Vector3(0, top + 0.1f, 0), 0.75f, 0.75f, 16, H("4b4f58"), 0f);   // balcon
            mb.Cyl(new Vector3(0, top + 0.1f, 0), new Vector3(0, top + 0.7f, 0), 0.42f, 0.42f, 12, H("fff3b0"), 1.0f);   // luz
            mb.Cyl(new Vector3(0, top + 0.7f, 0), new Vector3(0, top + 1.15f, 0), 0.55f, 0.05f, 12, RoofRed, 0f);   // techo
            mb.Box(new Vector3(0, 0.75f, -0.66f), new Vector3(0.36f, 0.6f, 0.12f), WoodD, 0f);   // puerta
        }

        /// <summary>Muelle: plataforma de tablones hacia el mar (+Z local = hacia afuera de la isla).</summary>
        static void Dock(MeshBuilder mb, int lv)
        {
            float len = 4.2f + 0.3f * lv;
            mb.Box(new Vector3(0, 0.12f, -0.6f), new Vector3(2.2f, 0.24f, 1.6f), Wood, 0f);   // cabecera en tierra
            for (int i = 0; i < (int)(len / 0.5f); i++)
            {
                float z = 0.3f + i * 0.5f;
                mb.Box(new Vector3(0, 0.1f, z), new Vector3(1.6f, 0.12f, 0.44f), i % 2 == 0 ? Wood : Color.Lerp(Wood, WoodD, 0.35f), 0f);
            }
            for (int i = 0; i <= (int)(len / 1.2f); i++)
            {
                float z = 0.3f + i * 1.2f;
                mb.Cyl(new Vector3(-0.8f, -0.8f, z), new Vector3(-0.8f, 0.45f, z), 0.1f, 0.1f, 6, WoodD, 0f);
                mb.Cyl(new Vector3(0.8f, -0.8f, z), new Vector3(0.8f, 0.45f, z), 0.1f, 0.1f, 6, WoodD, 0f);
            }
            mb.Box(new Vector3(0.6f, 0.45f, -0.7f), new Vector3(0.5f, 0.5f, 0.5f), Wood, 0f);   // caja
            mb.Cyl(new Vector3(-0.6f, 0.2f, -0.6f), new Vector3(-0.6f, 0.75f, -0.6f), 0.22f, 0.22f, 10, WoodD, 0f);   // barril
            mb.Box(new Vector3(-0.7f, 1.2f, len - 0.2f), new Vector3(0.12f, 1.6f, 0.12f), WoodD, 0f);   // poste con farol
            mb.Box(new Vector3(-0.7f, 2.05f, len - 0.2f), new Vector3(0.3f, 0.3f, 0.3f), H("ffe28a"), 0.9f);
        }

        public static Mesh ProcBuilding(BKind k, int level, out Material[] mats)
        {
            var mb = new MeshBuilder();
            if (k == BKind.Lighthouse) Lighthouse(mb, level);
            else if (k == BKind.Dock) Dock(mb, level);
            else City(mb, k, level);
            Mesh m = mb.ToMesh(null, k + "_" + level, out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }

        /// <summary>Barco comprador (proa hacia +Z).</summary>
        public static Mesh Boat(out Material[] mats)
        {
            var mb = new MeshBuilder();
            Color hull = H("c9563f"), hullD = H("8f3a2a");
            mb.Box(new Vector3(0, 0.25f, 0), new Vector3(1.8f, 0.6f, 4.0f), hull, 0f);
            mb.Box(new Vector3(0, 0.6f, 0), new Vector3(1.9f, 0.12f, 4.1f), hullD, 0f);
            mb.Octa(new Vector3(0, 0.3f, 2.2f), new Vector3(0.9f, 0.35f, 0.9f), hull, 0f);
            mb.Box(new Vector3(0, 0.7f, 0), new Vector3(1.6f, 0.08f, 3.8f), Wood, 0f);   // cubierta
            mb.Box(new Vector3(0, 1.1f, -1.2f), new Vector3(1.2f, 0.8f, 1.0f), H("f4f1ea"), 0f);   // cabina
            mb.Box(new Vector3(0, 1.6f, -1.2f), new Vector3(1.35f, 0.14f, 1.15f), H("4a8fe0"), 0f);
            mb.Cyl(new Vector3(0, 0.7f, 0.5f), new Vector3(0, 3.6f, 0.5f), 0.07f, 0.06f, 6, WoodD, 0f);   // mastil
            mb.Tri(new Vector3(0, 3.5f, 0.55f), new Vector3(0, 1.0f, 0.55f), new Vector3(0, 1.1f, 2.0f), Vector3.right, H("fffaf0"), 0.1f);
            mb.Tri(new Vector3(0, 3.5f, 0.55f), new Vector3(0, 1.1f, 2.0f), new Vector3(0, 1.0f, 0.55f), Vector3.left, H("fffaf0"), 0.1f);
            mb.Box(new Vector3(0, 3.55f, 0.5f), new Vector3(0.05f, 0.3f, 0.5f), H("ffd84a"), 0.3f);   // banderin
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(0.3f - i * 0.3f, 0.95f, 1.0f), new Vector3(0.28f, 0.28f, 0.28f), Wood, 0f);
            Mesh m = mb.ToMesh(null, "Barco", out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }

        public static Mesh Cloud(int seed, out Material[] mats)
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(seed);
            int n = 3 + rnd.Next(3);
            for (int i = 0; i < n; i++)
            {
                float x = (i - n * 0.5f) * 0.9f + (float)rnd.NextDouble() * 0.3f;
                float r = 0.8f + (float)rnd.NextDouble() * 0.6f;
                mb.Blob(new Vector3(x, 0, (float)rnd.NextDouble() * 0.6f), new Vector3(r, r * 0.7f, r), 1, seed + i, 0.08f, H("eef6fb"), Color.white, 0.25f, -0.3f);
            }
            Mesh m = mb.ToMesh(null, "Nube", out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }

        /// <summary>Andamio de obra (se muestra mientras BuildT > 0).</summary>
        public static Mesh Scaffold(out Material[] mats)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 1.2f, z = (i < 2 ? -1 : 1) * 1.0f;
                mb.Box(new Vector3(x, 1.0f, z), new Vector3(0.1f, 2.0f, 0.1f), Wood, 0f);
            }
            for (int j = 0; j < 3; j++)
            {
                float y = 0.4f + j * 0.65f;
                mb.Box(new Vector3(0, y, -1.0f), new Vector3(2.5f, 0.08f, 0.1f), WoodD, 0f);
                mb.Box(new Vector3(0, y, 1.0f), new Vector3(2.5f, 0.08f, 0.1f), WoodD, 0f);
            }
            Mesh m = mb.ToMesh(null, "Andamio", out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }

        // ------------------------------------------------------------ vetas
        static readonly Dictionary<int, Mesh> oreMeshes = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Material[]> oreMats = new Dictionary<int, Material[]>();

        public static Mesh OreMesh(int kind, int variant, out Material[] mats)
        {
            int key = kind * 10 + (variant % 3);
            Mesh m;
            if (oreMeshes.TryGetValue(key, out m) && m != null) { mats = oreMats[key]; return m; }
            var mb = new MeshBuilder();
            float s = Island.Ores[kind].Size;
            bool glow = kind >= 3 && kind != 5;   // el carbon es opaco: no brilla
            Color rock = kind == 4 || kind == 7 ? H("5b5f6a") : kind == 5 ? H("6b6862") : Stone;
            mb.Blob(new Vector3(0, 0.15f * s, 0), new Vector3(s, s * 0.8f, s * 0.9f), 1, 7 + variant * 13 + kind, 0.22f,
                Color.Lerp(rock, StoneD, 0.4f), rock, 0f, 0f);
            if (kind > 0)
            {
                var rnd = new System.Random(kind * 31 + variant);
                int n = glow ? 5 : kind == 5 ? 6 : 4;
                for (int i = 0; i < n; i++)
                {
                    float a = (float)(rnd.NextDouble() * Mathf.PI * 2);
                    var b = new Vector3(Mathf.Cos(a) * s * 0.55f, s * (0.35f + (float)rnd.NextDouble() * 0.4f), Mathf.Sin(a) * s * 0.55f);
                    var up = (b.normalized + Vector3.up * 0.6f).normalized;
                    if (kind == 5) mb.Octa(b * 0.9f, Vector3.one * s * 0.32f, OreCol[kind], 0f);   // terrones de carbon
                    else mb.Crystal(b, up, s * (glow ? 0.55f : 0.35f), s * 0.16f, 5, OreCol[kind], glow ? 0.55f : 0.2f);
                }
            }
            m = mb.ToMesh(null, "Veta" + key, out mats);
            mats = VertexColorMerge.Apply(m, mats);
            oreMeshes[key] = m;
            oreMats[key] = mats;
            return m;
        }

        /// <summary>Bulto en la espalda (bolsa con un trozo del mineral asomando).</summary>
        public static Mesh Sack(int kind, out Material[] mats)
        {
            var mb = new MeshBuilder();
            mb.Blob(Vector3.zero, new Vector3(0.22f, 0.24f, 0.18f), 1, 3, 0.1f, H("b08a5a"), H("cfa874"), 0f, -1f);
            mb.Octa(new Vector3(0.02f, 0.2f, 0f), Vector3.one * 0.16f, OreCol[kind], kind >= 3 && kind != 5 ? 0.5f : 0.15f);
            Mesh m = mb.ToMesh(null, "Bulto" + kind, out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }
    }

    /// <summary>Colores de acento compartidos con la UI.</summary>
    static class Kit3
    {
        public static readonly Color Yellow = Biomes.H("ffd84a"), Blue = Biomes.H("4aa3f0");
    }
}
