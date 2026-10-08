using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.IslandView
{
    /// <summary>
    /// Escenario 3D aparte (lejos de la isla, con su propia camara a una textura): renderiza los iconos de la interfaz
    /// desde los modelos reales y muestra la ruleta del mercader y los cofres con profundidad, luz y sombras de verdad.
    /// La interfaz lo muestra con un RawImage.
    /// </summary>
    public sealed class IslandStage : MonoBehaviour
    {
        static readonly Vector3 Origin = new Vector3(1200f, 0f, 1200f);   // lejos y al costado: la isla no le hace sombra
        public static IslandStage I { get; private set; }
        Camera cam;
        Transform root;
        public RenderTexture Tex { get; private set; }
        public bool Busy { get { return mode != Mode.None; } }

        enum Mode { None, Wheel, Chest }
        Mode mode;

        public static IslandStage Create(Transform parent)
        {
            var go = new GameObject("Escenario3D");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<IslandStage>();
            I.Setup();
            return I;
        }

        void Setup()
        {
            root = new GameObject("Raiz").transform;
            root.SetParent(transform, false);
            root.position = Origin;
            var cg = new GameObject("CamaraEscenario");
            cg.transform.SetParent(transform, false);
            cam = cg.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 60f;
        }

        // ------------------------------------------------------------ iconos desde los modelos
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        /// <summary>Icono del edificio en esa etapa, renderizado desde el modelo 3D (a 3x de su tamaño en pantalla).</summary>
        public Sprite BuildingIcon(BKind k, int tier)
        {
            bool kit = IslandArt.TripoBuilding(k, tier, out _) == null && (IslandArt.HasKitBuilding(k) || k == BKind.Barracks);
            int st = kit ? Mathf.Clamp(tier, 1, 4) : IslandArt.ModelStage(k, tier);   // el de kit cambia en cada etapa (salas nuevas)
            string key = k + "_" + st;
            Sprite sp;
            if (icons.TryGetValue(key, out sp)) return sp;
            Material tm;
            var mesh = IslandArt.TripoBuilding(k, tier, out tm);
            Material[] mats;
            if ((mesh == null && IslandArt.HasKitBuilding(k)) || k == BKind.Barracks)
            {
                // Plan Pueblo: el icono sale del mismo edificio de kit que se ve en la isla
                var holder = new GameObject("Icono").transform;
                holder.SetParent(root, false);
                var inner = new GameObject("Kit").transform;
                inner.SetParent(holder, false);
                inner.localRotation = Quaternion.Euler(0f, 35f, 0f);
                IslandArt.KitBuilding(k, st >= 4 ? 9 : st == 3 ? 6 : st == 2 ? 3 : 1, inner);
                sp = RenderIconHolder(holder, 512);
                icons[key] = sp;
                return sp;
            }
            if (mesh == null) { mesh = IslandArt.ProcBuilding(k, 1, out mats); }
            else mats = new[] { tm };
            sp = RenderIcon(mesh, mats, 512);
            icons[key] = sp;
            return sp;
        }

        public Sprite RenderIcon(Mesh mesh, Material[] mats, int px)
        {
            var holder = new GameObject("Icono").transform;
            holder.SetParent(root, false);
            var r = IslandArt.MakeRenderer(holder, "M", mesh, mats);
            r.transform.localRotation = Quaternion.Euler(0f, IslandGame.BuildingYaw, 0f);
            return RenderIconHolder(holder, px);
        }

        /// <summary>Icono de todo lo que cuelga de `holder` (encuadra la union de sus renderers). Destruye el holder.</summary>
        public Sprite RenderIconHolder(Transform holder, int px)
        {
            Bounds b = new Bounds(holder.position, Vector3.zero);
            bool any = false;
            foreach (var rr in holder.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = rr.bounds; any = true; } else b.Encapsulate(rr.bounds); }
            cam.orthographic = true;
            cam.transform.rotation = Quaternion.Euler(30f, 35f, 0f);
            cam.transform.position = b.center - cam.transform.forward * 20f;
            // ajustar al contorno proyectado
            float ext = 0f;
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 l = cam.transform.InverseTransformPoint(p);
                ext = Mathf.Max(ext, Mathf.Abs(l.x), Mathf.Abs(l.y));
            }
            cam.orthographicSize = ext * 1.04f;
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(px, px, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = true });
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.ReadPixels(new Rect(0, 0, px, px), 0, 0);
            // recorte al contorno real (la caja de limites deja mucho margen): cuadrado centrado con un poco de aire
            var pix = tex.GetPixels32();
            int x0 = px, y0 = px, x1 = -1, y1 = -1;
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                    if (pix[y * px + x].a > 8) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            DestroyImmediate(holder.gameObject);
            if (x1 < 0) { tex.Apply(false, true); return Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 400f); }
            int side = Mathf.Min(px, Mathf.Max(x1 - x0, y1 - y0) + 8);
            int rx = Mathf.Clamp((x0 + x1) / 2 - side / 2, 0, px - side), ry = Mathf.Clamp((y0 + y1) / 2 - side / 2, 0, px - side);
            var crop = new Texture2D(side, side, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            crop.SetPixels(tex.GetPixels(rx, ry, side, side));
            crop.Apply(false, true);
            Destroy(tex);
            return Sprite.Create(crop, new Rect(0, 0, side, side), new Vector2(0.5f, 0.5f), 400f);
        }

        // ------------------------------------------------------------ escena viva (ruleta / cofre)
        Transform scene;

        void BeginScene(Mode m, int px)
        {
            EndScene();
            mode = m;
            if (Tex == null)
            {
                Tex = new RenderTexture(new RenderTextureDescriptor(px, px, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = true });
                Tex.Create();
            }
            scene = new GameObject("Escena").transform;
            scene.SetParent(root, false);
            cam.targetTexture = Tex;
            cam.enabled = true;
            cam.orthographic = false;
            cam.fieldOfView = 30f;
        }

        public void EndScene()
        {
            mode = Mode.None;
            if (scene != null) Destroy(scene.gameObject);
            scene = null;
            cam.enabled = false;
            wheel = null; chestLid = null; chestBody = null; rays = null;
        }

        // ------------------------------------------------------------ ruleta
        Transform wheel, pointer;
        readonly List<Renderer> bulbs = new List<Renderer>();
        float wheelAngle, spinFrom, spinTo, spinT = -1f, spinDur = 4.6f, lastTick;
        public System.Action SpinDone;
        public bool Spinning { get { return spinT >= 0f; } }
        public static readonly Color[] SegCol =
        {
            Biomes.H("ff5a5f"), Biomes.H("ffd23a"), Biomes.H("4aa3f0"), Biomes.H("5cc84a"),
            Biomes.H("a36be8"), Biomes.H("ff9a3c"), Biomes.H("39c6d6"), Biomes.H("ff6fb5"),
        };

        public void ShowWheel()
        {
            BeginScene(Mode.Wheel, 1024);
            bulbs.Clear();
            prizes.Clear();
            showT = -1f;
            int n = Island.Wheel.Length;
            float R = 2.2f, depth = 0.35f;
            wheel = new GameObject("Ruleta").transform;
            wheel.SetParent(scene, false);
            // gajos con volumen (cara frontal + borde) y un objeto de premio sobre cada uno
            for (int i = 0; i < n; i++)
            {
                var mb = new MeshBuilder();
                float a0 = (i - 0.5f) / n * Mathf.PI * 2f, a1 = (i + 0.5f) / n * Mathf.PI * 2f;
                const int seg = 10;
                Color c = SegCol[i % SegCol.Length], cd = Color.Lerp(c, Color.black, 0.25f);
                for (int s = 0; s < seg; s++)
                {
                    float u0 = Mathf.Lerp(a0, a1, s / (float)seg), u1 = Mathf.Lerp(a0, a1, (s + 1) / (float)seg);
                    Vector3 p0 = new Vector3(Mathf.Sin(u0) * R, Mathf.Cos(u0) * R, 0), p1 = new Vector3(Mathf.Sin(u1) * R, Mathf.Cos(u1) * R, 0);
                    mb.Tri(Vector3.zero, p0, p1, Vector3.back, c, 0.12f);
                    mb.Quad(p0, p1, p1 + Vector3.forward * depth, p0 + Vector3.forward * depth, (p0 + p1).normalized, cd, 0f);
                }
                Material[] mats;
                var mesh = mb.ToMesh(null, "Gajo" + i, out mats);
                mats = VertexColorMerge.Apply(mesh, mats);
                var gr = IslandArt.MakeRenderer(wheel, "Gajo" + i, mesh, mats);
                var prize = PrizeObject(Island.Wheel[i]);
                prize.SetParent(wheel, false);
                prizes.Add(prize);
                float am = i / (float)n * Mathf.PI * 2f;
                prize.localPosition = new Vector3(Mathf.Sin(am), Mathf.Cos(am), 0f) * R * 0.66f + Vector3.back * 0.25f;
                prize.localRotation = Quaternion.Euler(0f, 0f, -am * Mathf.Rad2Deg);
            }
            // aro dorado con focos
            {
                var mb = new MeshBuilder();
                const int N = 48;
                Color gold = Biomes.H("ffcc33"), goldD = Biomes.H("c98a12");
                for (int s = 0; s < N; s++)
                {
                    float u0 = s / (float)N * Mathf.PI * 2f, u1 = (s + 1) / (float)N * Mathf.PI * 2f;
                    Vector3 d0 = new Vector3(Mathf.Sin(u0), Mathf.Cos(u0), 0), d1 = new Vector3(Mathf.Sin(u1), Mathf.Cos(u1), 0);
                    float r0 = R, r1 = R + 0.32f;
                    mb.Quad(d0 * r0 + Vector3.back * 0.12f, d0 * r1 + Vector3.back * 0.12f, d1 * r1 + Vector3.back * 0.12f, d1 * r0 + Vector3.back * 0.12f, Vector3.back, gold, 0.15f);
                    mb.Quad(d0 * r1 + Vector3.back * 0.12f, d0 * r1 + Vector3.forward * 0.45f, d1 * r1 + Vector3.forward * 0.45f, d1 * r1 + Vector3.back * 0.12f, (d0 + d1).normalized, goldD, 0f);
                }
                mb.Cyl(Vector3.back * 0.1f, Vector3.back * 0.42f, 0.42f, 0.36f, 20, gold, 0.25f);
                mb.Octa(Vector3.back * 0.5f, new Vector3(0.22f, 0.22f, 0.12f), Biomes.H("fff3b0"), 0.6f);
                Material[] mats;
                var mesh = mb.ToMesh(null, "Aro", out mats);
                mats = VertexColorMerge.Apply(mesh, mats);
                IslandArt.MakeRenderer(wheel, "Aro", mesh, mats);
                Mesh bulb = BulbMesh(out mats);
                for (int i = 0; i < 16; i++)
                {
                    float u = i / 16f * Mathf.PI * 2f;
                    var br = IslandArt.MakeRenderer(wheel, "Foco", bulb, mats, false);
                    br.transform.localPosition = new Vector3(Mathf.Sin(u), Mathf.Cos(u), 0f) * (R + 0.16f) + Vector3.back * 0.2f;
                    bulbs.Add(br);
                }
            }
            // flecha que marca el premio (arriba), con base
            {
                var mb = new MeshBuilder();
                Color red = Biomes.H("e5484d");
                mb.Tri(new Vector3(-0.32f, 0.55f, 0), new Vector3(0.32f, 0.55f, 0), new Vector3(0, -0.2f, 0), Vector3.back, red, 0.15f);
                mb.Tri(new Vector3(-0.32f, 0.55f, 0.18f), new Vector3(0, -0.2f, 0.18f), new Vector3(0.32f, 0.55f, 0.18f), Vector3.forward, red, 0f);
                mb.Quad(new Vector3(-0.32f, 0.55f, 0), new Vector3(0, -0.2f, 0), new Vector3(0, -0.2f, 0.18f), new Vector3(-0.32f, 0.55f, 0.18f), Vector3.left, Biomes.H("a82a2f"), 0f);
                mb.Quad(new Vector3(0, -0.2f, 0), new Vector3(0.32f, 0.55f, 0), new Vector3(0.32f, 0.55f, 0.18f), new Vector3(0, -0.2f, 0.18f), Vector3.right, Biomes.H("a82a2f"), 0f);
                mb.Octa(new Vector3(0, 0.6f, 0.05f), new Vector3(0.2f, 0.2f, 0.2f), Biomes.H("ffd84a"), 0.3f);
                Material[] mats;
                var mesh = mb.ToMesh(null, "Flecha", out mats);
                mats = VertexColorMerge.Apply(mesh, mats);
                pointer = IslandArt.MakeRenderer(scene, "Flecha", mesh, mats).transform;
                pointer.localPosition = new Vector3(0f, R + 0.25f, -0.35f);
            }
            // pie de madera
            {
                var mb = new MeshBuilder();
                mb.Box(new Vector3(0, -R - 0.9f, 0.4f), new Vector3(1.6f, 0.35f, 1.2f), Biomes.H("a8703f"), 0f);
                mb.Box(new Vector3(0, -R * 0.5f - 0.6f, 0.45f), new Vector3(0.35f, R + 0.3f, 0.3f), Biomes.H("7a4a2a"), 0f);
                Material[] mats;
                var mesh = mb.ToMesh(null, "Pie", out mats);
                mats = VertexColorMerge.Apply(mesh, mats);
                IslandArt.MakeRenderer(scene, "Pie", mesh, mats);
            }
            wheelAngle = 0f;
            wheel.localRotation = Quaternion.identity;
            cam.transform.position = Origin + new Vector3(0f, -0.9f, -14.5f);
            cam.transform.rotation = Quaternion.Euler(-4f, 0f, 0f);
            rays = MakeRays(scene, new Vector3(0, 0, 1.5f), 7f, new Color(1f, 0.9f, 0.5f, 0.55f));
        }

        static Mesh bulbMesh; static Material[] bulbMats;

        static Mesh BulbMesh(out Material[] mats)
        {
            if (bulbMesh == null)
            {
                var mb = new MeshBuilder();
                mb.Octa(Vector3.zero, new Vector3(0.11f, 0.11f, 0.11f), Color.white, 0.2f);
                bulbMesh = mb.ToMesh(null, "Foco", out bulbMats);
                bulbMats = VertexColorMerge.Apply(bulbMesh, bulbMats);
            }
            mats = bulbMats;
            return bulbMesh;
        }

        /// <summary>Objeto 3D del premio que va sobre el gajo.</summary>
        static Transform PrizeObject(WheelPrize w)
        {
            var mb = new MeshBuilder();
            Color gold = Biomes.H("ffcc33"), goldD = Biomes.H("d9961c");
            switch (w.Kind)
            {
                case "coins":
                    int stacks = w.Amount > 100 ? 3 : 1;
                    for (int s = 0; s < stacks; s++)
                    {
                        float x = (s - (stacks - 1) * 0.5f) * 0.32f;
                        int h = 3 + s % 2 * 2;
                        for (int k = 0; k < h; k++)
                            mb.Cyl(new Vector3(x, -0.2f + k * 0.09f, 0), new Vector3(x, -0.13f + k * 0.09f, 0), 0.17f, 0.17f, 14, k % 2 == 0 ? gold : goldD, 0.2f);
                    }
                    break;
                case "gems":
                    int g = w.Amount > 3 ? 3 : 1;
                    for (int s = 0; s < g; s++)
                        mb.Crystal(new Vector3((s - (g - 1) * 0.5f) * 0.22f, -0.25f, 0), Vector3.up, 0.42f + (s == 1 ? 0.12f : 0f), 0.13f, 6, Biomes.H("4fc3f7"), 0.4f);
                    break;
                case "turbo":
                    mb.Tri(new Vector3(0.05f, 0.35f, 0), new Vector3(-0.2f, -0.02f, 0), new Vector3(0.02f, -0.02f, 0), Vector3.back, Biomes.H("ffe14a"), 0.5f);
                    mb.Tri(new Vector3(-0.04f, 0.04f, 0), new Vector3(0.2f, 0.04f, 0), new Vector3(-0.06f, -0.38f, 0), Vector3.back, Biomes.H("ffe14a"), 0.5f);
                    mb.Octa(new Vector3(0, 0, 0.08f), new Vector3(0.28f, 0.28f, 0.06f), Biomes.H("a36be8"), 0.2f);
                    break;
                case "chest":
                    Color wood = w.Amount >= 2 ? gold : Biomes.H("a8703f"), band = w.Amount >= 2 ? Biomes.H("fff3b0") : gold;
                    mb.Box(new Vector3(0, -0.12f, 0), new Vector3(0.5f, 0.3f, 0.32f), wood, w.Amount >= 2 ? 0.25f : 0f);
                    mb.Box(new Vector3(0, 0.08f, 0), new Vector3(0.52f, 0.14f, 0.34f), Color.Lerp(wood, Color.white, 0.15f), 0f);
                    mb.Box(new Vector3(0, -0.05f, -0.17f), new Vector3(0.1f, 0.12f, 0.03f), band, 0.4f);
                    mb.Box(new Vector3(-0.2f, -0.05f, 0), new Vector3(0.05f, 0.46f, 0.36f), band, 0.2f);
                    mb.Box(new Vector3(0.2f, -0.05f, 0), new Vector3(0.05f, 0.46f, 0.36f), band, 0.2f);
                    break;
                case "giant":
                    mb.Blob(new Vector3(0, -0.05f, 0), new Vector3(0.32f, 0.28f, 0.25f), 1, 7, 0.1f, Biomes.H("8d8f98"), Biomes.H("b8bcc6"), 0f, -1f);
                    for (int s = 0; s < 4; s++)
                    {
                        float a = s * 1.6f;
                        mb.Crystal(new Vector3(Mathf.Cos(a) * 0.14f, -0.02f, Mathf.Sin(a) * 0.1f - 0.12f), new Vector3(Mathf.Cos(a) * 0.5f, 1f, -0.4f).normalized, 0.28f, 0.09f, 5, gold, 0.5f);
                    }
                    break;
            }
            Material[] mats;
            var mesh = mb.ToMesh(null, "Premio", out mats);
            mats = VertexColorMerge.Apply(mesh, mats);
            var go = new GameObject("Premio");
            var r = IslandArt.MakeRenderer(go.transform, "M", mesh, mats);
            r.transform.localScale = Vector3.one * 1.25f;
            return go.transform;
        }

        readonly List<Transform> prizes = new List<Transform>();
        /// <summary>Para capturas: siempre hace el "casi".</summary>
        public static bool ForceNear;
        int spinIdx;
        float nearT = -1f, nearFrom, nearTo, showT = -1f;
        Transform showObj;

        /// <summary>Premios "grandes" de la ruleta (la flecha a veces se frena en el borde de uno y cae al lado).</summary>
        static bool Big(int i) { var w = Island.Wheel[i]; return w.Kind == "giant" || (w.Kind == "chest" && w.Amount >= 2) || (w.Kind == "gems" && w.Amount >= 5); }

        /// <summary>Gira hasta dejar el gajo `idx` bajo la flecha (con varias vueltas y frenado largo).</summary>
        public void Spin(int idx)
        {
            int n = Island.Wheel.Length;
            float seg = 360f / n;
            float target = idx * seg;                   // el gajo i esta a i/n de vuelta (sentido horario)
            float jitter = Random.Range(-0.32f, 0.32f) * seg;
            spinIdx = idx;
            nearT = -1f;
            // "casi": si al lado hay un premio grande, a veces frena sobre el borde de ese premio, duda y cae al de al lado
            int left = (idx + n - 1) % n, right = (idx + 1) % n;
            int side = Big(right) ? 1 : Big(left) ? -1 : 0;
            if (!Big(idx) && side != 0 && (ForceNear || Random.value < 0.4f))
            {
                // con la rueda girando en sentido horario, angulo mayor = el gajo siguiente bajo la flecha
                jitter = side * 0.56f * seg;
                nearFrom = target + jitter;
                nearTo = target + side * 0.38f * seg;
                nearT = 0f;
            }
            spinDur = 4.6f;
            hurried = false;
            spinFrom = wheelAngle;
            float baseTo = Mathf.Ceil((spinFrom + 360f * 5f) / 360f) * 360f;
            spinTo = baseTo + target + jitter;
            nearFrom += baseTo; nearTo += baseTo;
            spinT = 0f;
            lastTick = spinFrom;
            Sfx.Play("whoosh", -6f);
            Sfx.Play("drumroll", -10f);
        }

        // ------------------------------------------------------------ cofre
        Transform chestBody, chestLid, rays;
        float chestT;
        bool chestPopped;
        int chestTier;
        public System.Action ChestOpened;

        /// <summary>Colores del cofre por rareza: madera, plata, oro, legendario (violeta con oro).</summary>
        static void ChestColors(int tier, out Color wood, out Color band, out float emis)
        {
            switch (tier)
            {
                case 3: wood = Biomes.H("8a4fe8"); band = Biomes.H("ffd23a"); emis = 0.35f; break;
                case 2: wood = Biomes.H("ffcc33"); band = Biomes.H("fff3b0"); emis = 0.2f; break;
                case 1: wood = Biomes.H("b8c4d0"); band = Biomes.H("ffd84a"); emis = 0.05f; break;
                default: wood = Biomes.H("a8703f"); band = Biomes.H("ffcc33"); emis = 0f; break;
            }
        }

        public void ShowChest(int tier)
        {
            BeginScene(Mode.Chest, 1024);
            chestTier = tier;
            BuildChest(tier);
            cam.transform.position = Origin + new Vector3(0f, 3.2f, -8.5f);
            cam.transform.rotation = Quaternion.Euler(17f, 0f, 0f);
            chestT = 0f;
            chestPopped = false;
            upT = -1f;
            jam = tier == 3;
            MakeChestRays(tier);
        }

        void MakeChestRays(int tier)
        {
            if (rays != null) Destroy(rays.gameObject);
            Color rc = tier == 3 ? new Color(0.8f, 0.6f, 1f, 0.75f) : tier == 2 ? new Color(1f, 0.85f, 0.4f, 0.7f) : new Color(1f, 0.95f, 0.75f, 0.55f);
            rays = MakeRays(scene, new Vector3(0, 1.4f, 1.2f), 7.5f, rc);
            rays.gameObject.SetActive(false);
        }

        void BuildChest(int tier)
        {
            Color wood, band; float emis;
            ChestColors(tier, out wood, out band, out emis);
            Color woodD = Color.Lerp(wood, Color.black, 0.25f);
            Vector3 keepPos = chestBody != null ? chestBody.localPosition : Vector3.zero;
            Quaternion keepRot = chestBody != null ? chestBody.localRotation : Quaternion.Euler(0f, -18f, 0f);
            if (chestBody != null) Destroy(chestBody.gameObject);
            chestBody = new GameObject("Cofre").transform;
            chestBody.SetParent(scene, false);
            chestBody.localPosition = keepPos;
            chestBody.localRotation = keepRot;
            {
                var mb = new MeshBuilder();
                mb.Box(new Vector3(0, 0.55f, 0), new Vector3(2.2f, 1.1f, 1.4f), wood, emis);
                mb.Box(new Vector3(0, 0.55f, 0), new Vector3(2.05f, 0.95f, 1.45f), woodD, 0f);
                for (int s = -1; s <= 1; s += 2) mb.Box(new Vector3(s * 0.85f, 0.55f, 0), new Vector3(0.2f, 1.14f, 1.5f), band, 0.25f);
                mb.Box(new Vector3(0, 0.9f, -0.74f), new Vector3(0.42f, 0.5f, 0.1f), band, 0.45f);
                mb.Box(new Vector3(0, 0.82f, -0.8f), new Vector3(0.1f, 0.18f, 0.05f), Biomes.H("3b2a1e"), 0f);
                if (tier == 3) mb.Octa(new Vector3(0, 0.55f, -0.76f), new Vector3(0.3f, 0.3f, 0.12f), Biomes.H("ff5ad9"), 0.8f);   // gema en el frente
                mb.Box(new Vector3(0, 1.0f, 0), new Vector3(1.9f, 0.08f, 1.2f), tier == 3 ? Biomes.H("e9c8ff") : Biomes.H("ffe680"), 0.9f);   // brillo de adentro
                Material[] mats;
                var mesh = mb.ToMesh(null, "CofreBase", out mats);
                mats = VertexColorMerge.Apply(mesh, mats);
                IslandArt.MakeRenderer(chestBody, "Base", mesh, mats);
            }
            {
                var mb = new MeshBuilder();
                // tapa redondeada (media caña), con bisagra en el borde trasero (z = 0.7)
                const int N = 8;
                for (int s = 0; s < N; s++)
                {
                    float u0 = s / (float)N * Mathf.PI, u1 = (s + 1) / (float)N * Mathf.PI;
                    Vector3 p0 = new Vector3(0, Mathf.Sin(u0) * 0.5f, -Mathf.Cos(u0) * 0.7f - 0.7f);
                    Vector3 p1 = new Vector3(0, Mathf.Sin(u1) * 0.5f, -Mathf.Cos(u1) * 0.7f - 0.7f);
                    Vector3 n0 = new Vector3(0, Mathf.Sin(u0 + 0.2f), -Mathf.Cos(u0 + 0.2f));
                    mb.Quad(p0 + Vector3.left * 1.1f, p0 + Vector3.right * 1.1f, p1 + Vector3.right * 1.1f, p1 + Vector3.left * 1.1f, n0, wood, emis);
                    mb.Tri(new Vector3(-1.1f, 0, -0.7f), p0 + Vector3.left * 1.1f, p1 + Vector3.left * 1.1f, Vector3.left, woodD, 0f);
                    mb.Tri(new Vector3(1.1f, 0, -0.7f), p1 + Vector3.right * 1.1f, p0 + Vector3.right * 1.1f, Vector3.right, woodD, 0f);
                    for (int b = -1; b <= 1; b += 2)
                        mb.Quad(p0 + new Vector3(b * 0.85f - 0.1f, 0.02f * n0.y, 0) + n0 * 0.03f, p0 + new Vector3(b * 0.85f + 0.1f, 0, 0) + n0 * 0.03f,
                                p1 + new Vector3(b * 0.85f + 0.1f, 0, 0) + n0 * 0.03f, p1 + new Vector3(b * 0.85f - 0.1f, 0, 0) + n0 * 0.03f, n0, band, 0.25f);
                }
                Material[] mats;
                var mesh = mb.ToMesh(null, "CofreTapa", out mats);
                mats = VertexColorMerge.Apply(mesh, mats);
                chestLid = new GameObject("Bisagra").transform;
                chestLid.SetParent(chestBody, false);
                chestLid.localPosition = new Vector3(0, 1.1f, 0.7f);
                IslandArt.MakeRenderer(chestLid, "Tapa", mesh, mats);
            }
        }

        float upT = -1f;
        int upTier;
        bool upBuilt, jam;

        /// <summary>El cofre sube de rareza: se encoge, destella del color nuevo y crece con el material nuevo.</summary>
        public void UpgradeChest(int tier)
        {
            if (mode != Mode.Chest) return;
            upTier = tier;
            upT = 0f;
            upBuilt = false;
        }

        /// <summary>Tiembla (no subio de rareza).</summary>
        public void ShakeChest() { shakeT = 0.45f; }
        float shakeT;

        /// <summary>Abre la tapa (el UI llama cuando termina el suspenso).</summary>
        public void OpenChest()
        {
            if (mode != Mode.Chest) return;
            chestT = 10f;   // fase de apertura
        }

        // ------------------------------------------------------------ rayos de luz
        static Texture2D raysTex;
        static Material raysMat;

        public static Transform MakeRays(Transform parent, Vector3 pos, float size, Color col)
        {
            if (raysTex == null)
            {
                const int N = 256;
                raysTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Rayos" };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                        float ray = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 6f)), 6f) * 0.8f + Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 6f + 0.5f)), 12f) * 0.4f;
                        float fall = Mathf.Clamp01(1f - d);
                        float core = Mathf.Clamp01(1f - d * 2.2f);
                        float v = Mathf.Clamp01(ray * fall * fall + core * core * 0.8f);
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(v * 255));
                    }
                raysTex.SetPixels32(px);
                raysTex.Apply(false, true);
                raysMat = new Material(Shader.Find("Mineros/FxParticleAdd")) { mainTexture = raysTex, name = "Rayos" };
            }
            var go = new GameObject("Rayos");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * size;
            var m = new Mesh();
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.colors = new[] { col, col, col, col };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = raysMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        // ------------------------------------------------------------ bucle
        void Update()
        {
            if (mode == Mode.None) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f), t = Time.unscaledTime;   // con tope, como los tweens de la UI
            if (rays != null)
            {
                rays.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up) * Quaternion.Euler(0f, 0f, t * 20f);
            }
            if (mode == Mode.Wheel && wheel != null) UpdateWheel(dt, t);
            if (mode == Mode.Chest && chestBody != null) UpdateChest(dt, t);
        }

        bool hurried;

        /// <summary>
        /// El jugador toco mientras gira: frena YA, de verdad (antes no pasaba nada). Mismo premio y mismo lugar de
        /// llegada, pero las vueltas que faltaban se cortan: menos de una vuelta y ~0.8 s de frenado fuerte.
        /// </summary>
        public void Hurry()
        {
            if (spinT < 0f || hurried) return;
            float left = spinTo - wheelAngle;
            if (left < 200f) return;   // ya esta frenando: dejarlo terminar
            hurried = true;
            float to = spinTo - Mathf.Floor((left - 120f) / 360f) * 360f;   // mismo angulo final, entre 120 y 480 grados por delante
            spinFrom = wheelAngle;
            nearFrom -= spinTo - to; nearTo -= spinTo - to;
            spinTo = to;
            spinT = 0f;
            spinDur = 0.85f;
            Sfx.Play("down", -8f, 1.3f);
            Mineros.Fx.Haptics.Light();
        }

        void UpdateWheel(float dt, float t)
        {
            float speed = 0f;
            if (spinT >= 0f)
            {
                spinT += dt;
                float u = Mathf.Clamp01(spinT / spinDur);
                float e = 1f - Mathf.Pow(1f - u, 4f);
                float prev = wheelAngle;
                wheelAngle = Mathf.Lerp(spinFrom, spinTo, e);
                speed = (wheelAngle - prev) / Mathf.Max(dt, 1e-4f);
                // tic cada vez que un separador pasa por la flecha; la flecha salta
                float segDeg = 360f / Island.Wheel.Length;
                if (Mathf.Floor((wheelAngle + segDeg * 0.5f) / segDeg) != Mathf.Floor((lastTick + segDeg * 0.5f) / segDeg))
                {
                    Sfx.Play("wheel", -6f, 0.9f + Mathf.Clamp01(speed / 900f) * 0.4f); Mineros.Fx.Haptics.Selection();
                    pointerKick = 1f;
                }
                lastTick = wheelAngle;
                if (u >= 1f)
                {
                    spinT = -1f;
                    if (nearT < 0f) Land();
                }
            }
            else if (nearT >= 0f)
            {
                // duda 300 ms sobre el borde del premio grande y despues cae al gajo de al lado
                nearT += dt;
                if (nearT > 0.3f)
                {
                    float k = Mathf.Clamp01((nearT - 0.3f) / 0.4f);
                    float prev = wheelAngle;
                    wheelAngle = Mathf.Lerp(nearFrom, nearTo, k * k * (3f - 2f * k));
                    float segDeg = 360f / Island.Wheel.Length;
                    if (Mathf.Floor((wheelAngle + segDeg * 0.5f) / segDeg) != Mathf.Floor((prev + segDeg * 0.5f) / segDeg)) { Sfx.Play("wheel", -4f, 0.8f); pointerKick = 1f; Mineros.Fx.Haptics.Light(); }
                    if (k >= 1f) { nearT = -1f; Sfx.Play("down", -10f); Land(); }
                }
                else wheelAngle = nearFrom + Mathf.Sin(nearT * 40f) * 0.6f;
            }
            else if (showT < 0f) wheelAngle += dt * 8f;   // gira despacito esperando
            UpdateShowcase(dt, t);
            wheel.localRotation = Quaternion.Euler(0f, 0f, wheelAngle);
            pointerKick = Mathf.MoveTowards(pointerKick, 0f, dt * 7f);
            if (pointer != null) pointer.localRotation = Quaternion.Euler(0f, 0f, -pointerKick * 22f);
            // focos: corren alrededor (mas rapido mientras gira)
            for (int i = 0; i < bulbs.Count; i++)
            {
                float k = Mathf.Repeat(t * (spinT >= 0f ? 9f : 2.5f) - i * 0.25f, 1f);
                bool on = k < 0.5f;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_EmissionColor", on ? new Color(1f, 0.85f, 0.4f) * 1.4f : new Color(0.15f, 0.1f, 0.05f));
                bulbs[i].SetPropertyBlock(mpb);
            }
        }

        float pointerKick;

        /// <summary>Cae el premio: festejo y el objeto sale del gajo, flota al centro y gira; despues se cobra.</summary>
        void Land()
        {
            Sfx.Play("goal", -3f);
            FxApi.Play("confetti", wheel.position + Vector3.back * 1.5f + Vector3.up * 1.5f, default(Color), 2f);
            if (spinIdx >= 0 && spinIdx < prizes.Count)
            {
                var src = prizes[spinIdx];
                showObj = Instantiate(src.gameObject, scene).transform;
                showObj.position = src.position;
                showObj.rotation = src.rotation;
                showFrom = src.position;
                showT = 0f;
                Sfx.Play("whoosh", -8f, 1.2f);
            }
            else SpinDone?.Invoke();
        }

        Vector3 showFrom;

        void UpdateShowcase(float dt, float t)
        {
            if (showT < 0f || showObj == null) return;
            showT += dt;
            float k = Mathf.Clamp01(showT / 0.5f);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            Vector3 to = wheel.position + Vector3.back * 1.6f;
            showObj.position = Vector3.Lerp(showFrom, to, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
            showObj.localScale = Vector3.one * Mathf.Lerp(1f, 2.6f, e) * (1f + Mathf.Sin(showT * 6f) * 0.03f);
            showObj.rotation = Quaternion.Euler(0f, showT * 220f, 0f);
            if (showT >= 0.5f && showT - dt < 0.5f)
            {
                FxApi.Play("sparkle", to, new Color(1f, 0.9f, 0.5f), 1.4f);
                SpinDone?.Invoke();   // se cobra cuando el premio llega al centro
            }
            if (showT > 1.8f)
            {
                float f = Mathf.Clamp01((showT - 1.8f) / 0.3f);
                showObj.localScale = Vector3.one * 2.6f * (1f - f);
                if (f >= 1f) { Destroy(showObj.gameObject); showObj = null; showT = -1f; }
            }
        }

        void UpdateChest(float dt, float t)
        {
            chestT += dt;
            float scale = 1f;
            if (upT >= 0f)
            {
                upT += dt;
                if (upT < 0.16f) scale = Mathf.Lerp(1f, 0.55f, upT / 0.16f);
                else
                {
                    if (!upBuilt)
                    {
                        upBuilt = true;
                        chestTier = upTier;
                        jam = upTier == 3;
                        BuildChest(upTier);
                        MakeChestRays(upTier);
                        Color fc = upTier == 3 ? new Color(0.8f, 0.55f, 1f) : upTier == 2 ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.9f, 1f);
                        FxApi.Play("unlock_burst", chestBody.position + Vector3.up * 0.8f, fc, 1.6f);
                        FxApi.Play("ring", chestBody.position + Vector3.up * 0.1f, fc, 3f);
                    }
                    float k = Mathf.Clamp01((upT - 0.16f) / 0.34f);
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float ob = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
                    scale = Mathf.LerpUnclamped(0.55f, 1f, ob);
                    if (k >= 1f) upT = -1f;
                }
            }
            if (chestBody != null) chestBody.localScale = Vector3.one * scale;
            if (shakeT > 0f) shakeT -= dt;
            if (chestT < 10f)
            {
                // cae con rebote y despues tiembla cada vez mas (suspenso)
                float fall = Mathf.Clamp01(chestT / 0.55f);
                float y = fall < 1f ? Mathf.Lerp(5f, 0f, fall * fall) : Mathf.Abs(Mathf.Sin((chestT - 0.55f) * 9f)) * 0.35f * Mathf.Exp(-(chestT - 0.55f) * 5f);
                float shake = chestT > 1.1f ? Mathf.Sin(t * 50f) * 0.04f * Mathf.Min(1f, (chestT - 1.1f) * 0.8f) : 0f;
                if (shakeT > 0f) shake += Mathf.Sin(t * 70f) * 0.12f * shakeT;
                // legendario: luz que se escapa por las rendijas
                if (chestTier == 3 && rays != null && chestT > 0.6f) { rays.gameObject.SetActive(true); rays.localScale = Vector3.one * (3f + Mathf.Sin(t * 6f) * 0.4f); }
                chestBody.localPosition = new Vector3(shake, y, 0f);
                chestBody.localRotation = Quaternion.Euler(0f, -18f, shake * 120f);
                if (fall >= 1f && chestT - dt < 0.55f) { Sfx.Play("thud", -4f, 0.9f); FxApi.Play("dust", chestBody.position, default(Color), 2.2f); }
                float lid = chestT > 1.1f ? Mathf.Abs(Mathf.Sin(t * 18f)) * 4f * Mathf.Min(1f, (chestT - 1.1f)) : 0f;
                chestLid.localRotation = Quaternion.Euler(lid, 0f, 0f);
                return;
            }
            float o = Mathf.Clamp01((chestT - 10f) / 0.35f);
            if (!chestPopped)
            {
                chestPopped = true;
                Sfx.Play("chest", -2f);
                Sfx.Play("gleam", -6f);
                Sfx.Duck(6f, 2f);
                FxApi.Play("chest_open", chestBody.position + Vector3.up * 1.2f, default(Color), 0.9f);
                FxApi.Play("coin_burst", chestBody.position + Vector3.up * 1.4f, default(Color), 1.2f);
                rays.gameObject.SetActive(true);
                ChestOpened?.Invoke();
            }
            float ang = Mathf.Lerp(0f, 115f, 1f - Mathf.Pow(1f - o, 3f)) + Mathf.Sin(Mathf.Min(chestT - 10f, 1f) * 18f) * 6f * (1f - Mathf.Clamp01(chestT - 10.35f));
            if (jam)
            {
                // la tapa se traba una vez: sube un poco, se cierra de golpe y despues se abre del todo
                float jt = chestT - 10f;
                if (jt < 0.25f) ang = Mathf.Sin(jt / 0.25f * Mathf.PI) * 22f;
                else if (jt < 0.55f) { ang = 0f; if (jt - dt < 0.25f) { Sfx.Play("thud", -6f, 1.3f); FxApi.Play("sparkle", chestBody.position + Vector3.up * 1.2f, new Color(0.85f, 0.6f, 1f), 1.2f); } }
                else { float k2 = Mathf.Clamp01((jt - 0.55f) / 0.35f); ang = Mathf.Lerp(0f, 115f, 1f - Mathf.Pow(1f - k2, 3f)); }
            }
            chestLid.localRotation = Quaternion.Euler(ang, 0f, 0f);
            chestBody.localPosition = Vector3.zero;
            chestBody.localRotation = Quaternion.Euler(0f, -18f + Mathf.Sin(t * 0.8f) * 6f, 0f);
            rays.localScale = Vector3.one * (7.5f + Mathf.Sin(t * 2f) * 0.4f);
        }
    }
}
