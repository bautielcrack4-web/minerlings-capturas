using System.Collections;
using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Game;
using Mineros.Miners;
using Mineros.World;
using UnityEngine;
using UnityEngine.Rendering;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Escena de la Isla Minera: corre la simulacion (Core.Island) y la dibuja. Partes: este archivo (armado, mundo,
    /// eventos), IslandGame.Input.cs (camara y toques) e IslandAmbient (mar, nubes, pajaros). La UI vive en IslandUi.
    /// </summary>
    public sealed partial class IslandGame : MonoBehaviour
    {
        public static IslandGame I { get; private set; }
        public Island Isl { get; private set; }
        public Camera Cam { get; private set; }
        public IslandUi Ui { get; private set; }
        public IslandAmbient Ambient { get; private set; }
        public IslandGround Ground { get; private set; }
        public IslandSoundscape Sound { get; private set; }
        /// <summary>Arboles y arbustos que se mecen con el viento (los mueve IslandAmbient).</summary>
        public List<Transform> Swayers { get; private set; }
        /// <summary>Monedas ganadas mientras no se jugaba (la UI muestra la bienvenida).</summary>
        public double OfflineGain { get; private set; }

        FileSaveStore store;
        string saveFileName;
        float saveT;
        Transform root, terrainRoot, camRig;

        // ------------------------------------------------------------ vistas
        sealed class PlotView
        {
            public Plot P;
            public Transform Root, Body, Scaffold, Pad, Blob;
            public int ShownKind = -2, ShownLevel = -1, ShownTier = -1;
            public float Punch, SmokeT, Height = 2.5f, Glow;
            public bool Driven;          // una animacion (show) maneja la escala del edificio
            public Renderer Model;
        }

        sealed class OreView
        {
            public Ore O;
            public Transform T, Vis;
            public float Shake, GlintT, Squash, Flash;
            public bool Landed, Whistled;
            public GameObject Aura;
            public MeshRenderer Rend;
            public bool Glowing;
            public MeshRenderer Halo;        // halo del cristal nocturno
            public Transform Mound;          // tierra que se abulta antes de que asome la veta
        }

        sealed class MinerView
        {
            public Miner M;
            public MinerModel Model;
            public Transform Sack;
            public int SackKind = -1;
            public float Phase, BlinkT, Celebrate, DustT, Lean, FxT, TrailT, StepT;
            public bool Arriving;        // llega como chispa: invisible hasta que la chispa toca el suelo
            public int StepSide = 1;
            public MeshRenderer Lamp;
            public int Gear = -1;        // especialista * 10 + etapa ya armada
        }

        readonly List<PlotView> plots = new List<PlotView>();
        /// <summary>Escala del minero en el mapa: un tercio de la altura de una casa (antes 1.3, parecian edificios).</summary>
        public const float MinerScale = 0.78f;
        /// <summary>Altura sobre el minero escalada desde los valores pensados para la escala vieja (1.3).</summary>
        public static float MH(float y) { return y * MinerScale / 1.3f; }
        /// <summary>Giro de los modelos de Tripo para que el frente mire a la camara (yaw de camara 35).</summary>
        public static float BuildingYaw = 215f;
        readonly Dictionary<int, OreView> ores = new Dictionary<int, OreView>();
        readonly Dictionary<int, MinerView> miners = new Dictionary<int, MinerView>();
        Mesh scaffoldMesh; Material[] scaffoldMats;

        // ------------------------------------------------------------ arranque
        /// <summary>Cuadros por segundo: la frecuencia real de la pantalla (hasta 120) salvo en ahorro de bateria (60).</summary>
        public static void ApplyFrameRate()
        {
            bool saver = PlayerPrefs.GetInt("isla_ahorro", 0) == 1;
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            if (hz < 50) hz = 60;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = saver ? 60 : Mathf.Clamp(hz, 60, 120);
        }

        public static IslandGame Build(string saveFile = "island.json", System.Action<Island> configure = null)
        {
            ApplyFrameRate();
            var go = new GameObject("IslaMinera");
            DontDestroyOnLoad(go);
            var g = go.AddComponent<IslandGame>();
            g.Init(saveFile, configure);
            return g;
        }

        /// <summary>Rearma todo (guardando antes): se usa al cambiar el idioma.</summary>
        public void Rebuild()
        {
            Save();
            StartCoroutine(Restart());
        }

        static double NowSeconds() { return (System.DateTime.UtcNow - new System.DateTime(2024, 1, 1)).TotalSeconds; }
        public double NowSecondsPublic { get { return NowSeconds(); } }

        void Init(string saveFile, System.Action<Island> configure)
        {
            I = this;
            // los toques se leen directo: sin mouse simulado no hay un "clic" duplicado al levantar el dedo
            if (!Application.isEditor) Input.simulateMouseWithTouches = false;
            store = new FileSaveStore(saveFile);
            saveFileName = saveFile;
            MinerModel.MergeIntoSkin = System.Environment.GetEnvironmentVariable("PERF_NOMERGE") != "1";   // mineros en una malla (rendimiento)
            if (Application.isBatchMode) Random.InitState(1234);
            Isl = new Island(Application.isBatchMode ? 1234 : System.Environment.TickCount & 0xffff);   // capturas repetibles
            string saved = store.Load();
            if (!string.IsNullOrEmpty(saved)) Isl.LoadJson(saved);
            else Isl.StartTutorial();   // partida nueva: isla casi vacia y primeros pasos guiados
            configure?.Invoke(Isl);
            Isl.AutoRecruit = false;   // desde ahora los mineros nuevos llegan en barco y el jugador elige
            OfflineGain = Isl.ApplyOffline(NowSeconds());
            IslandArt.SetBiome(Isl.Biome);   // cada isla tiene su paleta (antes de armar el terreno)
            root = new GameObject("Mundo").transform;
            root.SetParent(transform, false);
            SetupCamera();
            if (!Isl.TutDone) { Cam.orthographicSize = 12f; zoomTarget = 12f; }   // isla nueva y vacia: la camara mas cerca
            SetupLight();
            BuildTerrain();
            scaffoldMesh = IslandArt.Scaffold(out scaffoldMats);
            Ambient = gameObject.AddComponent<IslandAmbient>();
            Ambient.Init(this, root);
            IslandStage.Create(transform);
            Ui = gameObject.AddComponent<IslandUi>();   // antes que los mineros: AddMiner usa la UI
            Ui.Init(this);
            foreach (var p in Isl.Plots) plots.Add(MakePlot(p));
            foreach (var o in Isl.OreList) AddOre(o);
            foreach (var m in Isl.Miners) AddMiner(m);
            Isl.OreSpawned += o => { AddOre(o); if (!o.Giant) Sfx.Play("pop", -14f, 0.8f + Random.value * 0.3f); };
            Isl.OreHit += OnHit;
            Isl.OreBroken += OnBreak;
            Isl.Deposited += OnDeposit;
            Isl.MinerSpawned += m => { AddMiner(m); };
            Isl.BuildingChanged += OnBuilding;
            Isl.PlotMoved += OnPlotMoved;
            Isl.MinerMood += OnMood;
            Isl.GiantSpawned += OnGiant;
            Isl.GiantPaid += OnGiantPaid;
            Isl.MinePaid += OnMinePaid;
            Isl.ShipArrived += s => { Ambient.ShipCome(s); Ui.Toast(Loc.T("¡Llegó un barco! Pide ") + s.Count + Loc.T(" de ") + Island.Ores[s.Kind].Name, Kit3.Blue); Sfx.Play("event_start", -4f, 0.9f); };
            Isl.ShipLeft += (s, ok) =>
            {
                Ambient.ShipGo();
                if (ok) { Ui.Toast(Loc.T("¡Pedido cumplido! +") + BigNum.Fmt(Island.Ores[s.Kind].Value * s.Count * 3.0 * Isl.PriceMult()) + Loc.T(" y gemas"), Kit3.Yellow); Sfx.Play("goal", -3f); Sfx.Play("coins_pour", -6f); }
                else Ui.Toast(Loc.T("El barco se fue. Vuelve pronto"), new Color(0.8f, 0.85f, 0.9f));
            };
            Isl.GoalDone += g => { Ui.GoalDone(g); Sfx.Play("goal", -3f); Mineros.Fx.Haptics.Success(); };
            Isl.Expanded += lv => OnExpanded();
            InitDiscover();
            Isl.BalloonCame += () => { Ambient.BalloonCome(); Ui.Toast(Loc.T("¡Llegó el mercader! Tocá el globo: ruleta gratis"), Kit3.Yellow); Sfx.Play("event_start", -4f, 1.2f); };
            Isl.BalloonGone += tapped => Ambient.BalloonGo(tapped);
            Isl.ChestGot += t => Ui.ChestGot(t);
            Isl.FrenzyStarted += OnFrenzy;
            Isl.RecruitsArrived += OnRecruits;
            Isl.MinerLevelUp += OnMinerLevel;
            Isl.BecameFriends += OnFriends;
            Isl.LuckyGem += OnLucky;
            InitSurprises();
            InitCity();
            InitMonetization();
            InitDaily();
            InitProgress();
            InitComplex();
            Sound = gameObject.AddComponent<IslandSoundscape>();
            Sound.Init(this);
            InitWorld();
            InitWeather();
            Isl.CheckRecruits();
        }

        void BuildTerrain()
        {
            if (terrainRoot != null) Destroy(terrainRoot.gameObject);
            terrainRoot = new GameObject("Terreno").transform;
            terrainRoot.SetParent(root, false);
            float r = Isl.Radius;
            IslandArt.Terrain(terrainRoot, r);
            Ground = new IslandGround(terrainRoot, Isl, r);
            Swayers = IslandArt.Decor(terrainRoot, Isl, r);
            if (Ambient != null) Ambient.Rebuild(r);
        }

        void OnExpanded()
        {
            foreach (var v in plots) if (v.P.Ring == Isl.Expand) StartCoroutine(FogReveal(v));
            MarkOptimize();
            BuildTerrain();
            BuildLamps();
            if (board != null) Isl.AddBlocker(boardPos.x, boardPos.z, 1.1f);
            foreach (var v in plots) v.Root.localScale = Vector3.one;
            Ui.Toast(Loc.T("¡La isla creció! Nuevas parcelas y vetas"), Kit3.Yellow);
            FxApi.Play("confetti", camRig.position + Vector3.up * 6f, default(Color), 3f);
            Sfx.Play("rebirth", -2f);
            Sfx.PlayLater("firework", 0.3f, -6f);
            Sfx.PlayLater("firework", 0.9f, -9f, 1.1f);
            Sfx.Duck(8f, 3f);
            Mineros.Fx.Haptics.Heavy();
            Mineros.Fx.Haptics.Success();
        }

        void SetupCamera()
        {
            camRig = new GameObject("CamRig").transform;
            camRig.SetParent(transform, false);
            var cgo = new GameObject("Main Camera");
            cgo.tag = "MainCamera";
            cgo.transform.SetParent(camRig, false);
            Cam = cgo.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = 15.2f;   // al entrar se ve el pueblo entero, con la isla como protagonista
            Cam.nearClipPlane = 1f;
            Cam.farClipPlane = 140f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = IslandArt.SeaD;
            Cam.allowMSAA = true;
            Cam.allowHDR = false;
            cgo.transform.localPosition = new Vector3(0, 0, -60f);
            camRig.rotation = Quaternion.Euler(48f, 35f, 0f);
            camRig.position = new Vector3(0f, 0f, -0.5f);
            cgo.AddComponent<AudioListener>();
            cgo.AddComponent<IslandPost>();
            Juice.Cam = Cam;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowCascades = 0;
            QualitySettings.shadowDistance = 90f;
        }

        void SetupLight()
        {
            var sg = new GameObject("Sol");
            sg.transform.SetParent(transform, false);
            sg.transform.rotation = Quaternion.Euler(55f, 150f, 0f);
            var sun = sg.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.08f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.5f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.8f, 0.88f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.74f, 0.8f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.52f, 0.5f, 0.44f);
            RenderSettings.fog = false;
        }

        // ------------------------------------------------------------ parcelas y edificios
        PlotView MakePlot(Plot p)
        {
            var v = new PlotView { P = p };
            v.Root = new GameObject("Parcela" + p.Id).transform;
            v.Root.SetParent(root, false);
            v.Root.localPosition = new Vector3(p.X, 0f, p.Z);
            var mb = new MeshBuilder();
            IslandArt.PlotPad(mb, Vector3.zero);
            v.Pad = IslandArt.Bake(mb, v.Root, "Base", false).transform;
            v.Scaffold = IslandArt.MakeRenderer(v.Root, "Andamio", scaffoldMesh, scaffoldMats).transform;
            v.Blob = IslandArt.Blob(v.Root, 2.3f, 0.4f);
            v.Scaffold.gameObject.SetActive(false);
            v.SmokeT = Random.value * 3f;
            RefreshPlot(v);
            return v;
        }

        void RefreshPlot(PlotView v)
        {
            var p = v.P;
            int tier = p.Building >= 0 && p.Level >= 1 ? Island.Tier(p.Level) : 0;
            if (v.ShownKind == p.Building && v.ShownLevel == p.Level && v.ShownTier == tier) return;
            if (v.Body != null) Destroy(v.Body.gameObject);
            v.Body = null;
            v.Model = null;
            if (p.Building >= 0 && p.Level >= 1)   // en la obra inicial (nivel 0) se ven los cimientos, no el edificio
            {
                var k = (BKind)p.Building;
                Material tm;
                var tmesh = IslandArt.TripoBuilding(k, tier, out tm);
                v.Body = new GameObject("Edificio").transform;
                v.Body.SetParent(v.Root, false);
                if (tmesh != null)
                {
                    // modelo de Tripo: el frente queda hacia +Z; se gira para mirar a la camara y crece un poco por nivel
                    var r = IslandArt.MakeRenderer(v.Body, "Modelo", tmesh, new[] { tm });
                    var inner = r.transform;
                    float yaw = BuildingYaw;
                    if (k == BKind.Dock) yaw = Mathf.Atan2(p.X, p.Z) * Mathf.Rad2Deg + DockYaw;   // el muelle mira al mar
                    inner.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    inner.localScale = Vector3.one * (0.9f + 0.035f * Mathf.Min(p.Level, 8));
                    v.Height = tmesh.bounds.max.y * inner.localScale.y;
                    v.Model = r;
                }
                else if (IslandArt.HasKitBuilding(k))
                {
                    // Plan Pueblo: edificio armado con el kit de habitaciones (misma arquitectura que el Cuartel, maquina
                    // adentro, techo que se abre al acercar)
                    var inner = new GameObject("Kit").transform;
                    inner.SetParent(v.Body, false);
                    inner.localRotation = Quaternion.Euler(0f, 35f, 0f);
                    inner.localScale = Vector3.one * (1.2f + 0.015f * Mathf.Min(p.Level, 8));   // crece sobre todo por salas (etapas)
                    float hh = IslandArt.KitBuilding(k, p.Level, inner);
                    v.Height = hh * inner.localScale.y;
                    var roofT = inner.Find("Techos");
                    if (roofT != null)
                        foreach (var rr in roofT.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            var ms = rr.sharedMaterials;
                            for (int i = 0; i < ms.Length; i++) ms[i] = CutMat(ms[i]);
                            rr.sharedMaterials = ms;
                        }
                    v.Model = inner.GetComponentInChildren<MeshRenderer>();
                }
                else
                {
                    Material[] mats;
                    var mesh = IslandArt.ProcBuilding(k, p.Level, out mats);
                    var r = IslandArt.MakeRenderer(v.Body, "Modelo", mesh, mats);
                    var inner = r.transform;
                    if (k == BKind.Dock) inner.localRotation = Quaternion.Euler(0f, Mathf.Atan2(p.X, p.Z) * Mathf.Rad2Deg, 0f);
                    else inner.localRotation = Quaternion.Euler(0f, 35f, 0f);
                    v.Height = mesh.bounds.max.y;
                    v.Model = r;
                }
                CityParts(v);
                // banderines de nivel en el techo (un punto por nivel); el Cuartel tiene su bandera en la Sala central
                if (k != BKind.Barracks) LevelFlags(v);
                Garden(v);
                if (tier >= 4) FxApi.Attach("aura", v.Body, new Color(1f, 0.85f, 0.35f), 1.6f);   // etapa maxima: aura dorada
            }
            v.ShownKind = p.Building;
            v.ShownLevel = p.Level;
            v.ShownTier = tier;
            SetGlow(v, v.Glow);
            if (p.Building == (int)BKind.Barracks) { if (v.Model != null) v.Model.enabled = false; cxDirty = true; }   // el Complejo dibuja la Sala central
        }

        /// <summary>
        /// Jardincito del lote: arbustos y flores en la parte de atras del lote (del lado contrario al camino), para que
        /// cada edificio quede prolijo y enmarcado. Siempre igual para la misma parcela.
        /// </summary>
        void Garden(PlotView v)
        {
            if (v.P.Building == (int)BKind.Dock || v.P.Building == (int)BKind.Depot || v.P.Building == (int)BKind.Barracks) return;
            var g = new GameObject("Jardin").transform;
            g.SetParent(v.Body, false);
            Vector3 back = new Vector3(v.P.X, 0f, v.P.Z).normalized;   // hacia afuera (el camino viene de adentro)
            if (back.sqrMagnitude < 0.01f) back = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, back);
            var rnd = new System.Random(v.P.Id * 31 + 7);
            string[] kinds = { "bush", "flower", "flower", "bush", "flower" };
            float[] at = { -1.15f, -0.6f, 0.05f, 0.65f, 1.2f };
            for (int i = 0; i < kinds.Length; i++)
            {
                var go = Mineros.Art.ArtKit.Prop(kinds[i], IslandArt.PropBiome, rnd.Next(1000), g);
                if (go == null) continue;
                Vector3 p = back * (1.75f + (float)rnd.NextDouble() * 0.15f) + side * at[i];
                go.transform.position = v.Root.position + p;
                float s = kinds[i] == "bush" ? 0.7f : 0.9f;
                go.transform.localScale = Vector3.one * s;
                if (kinds[i] == "bush") IslandArt.Blob(go.transform, 0.7f, 0.3f);
            }
        }

        /// <summary>Giro extra del modelo de Tripo del muelle para que el tablon apunte al mar.</summary>
        public static float DockYaw = 0f;

        /// <summary>Brillo del edificio (emision) para la evolucion.</summary>
        void SetGlow(PlotView v, float g)
        {
            v.Glow = g;
            if (v.Model == null) return;
            var mpb = new MaterialPropertyBlock();
            v.Model.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", new Color(1f, 0.92f, 0.65f) * g);
            v.Model.SetPropertyBlock(mpb);
        }

        void LevelFlags(PlotView v)
        {
            var mb = new MeshBuilder();
            int n = Mathf.Min(v.P.Level, 10);
            for (int i = 0; i < n; i++)
            {
                float a = (i - (n - 1) * 0.5f) * 0.32f;
                mb.Octa(new Vector3(a, 0, 0), new Vector3(0.22f, 0.22f, 0.08f), i % 2 == 0 ? Kit3.Yellow : Kit3.Blue, 0.35f);
            }
            var r = IslandArt.Bake(mb, v.Body, "Nivel", false).transform;
            r.localPosition = new Vector3(0, v.Height + 0.35f, 0);
            r.rotation = Quaternion.Euler(0, 35f, 0);
        }

        public float PlotHeight(Plot p) { return plots[p.Id].Height; }

        void OnBuilding(Plot p)
        {
            MarkOptimize();
            var v = plots[p.Id];
            if (p.Building < 0) { RefreshPlot(v); return; }
            if (p.Level == 0)
            {
                // empieza la obra: cimientos, cartel y andamio (B.1.1)
                v.ShownKind = p.Building; v.ShownLevel = 0; v.ShownTier = 0;
                StartCoroutine(PlaceShow(v));
                return;
            }
            if (v.ShownLevel <= 0) { StartCoroutine(RevealShow(v)); return; }   // termino la obra inicial (B.1.5)
            if (p.Level == v.ShownLevel)
            {
                // empieza una mejora: el andamio sube alrededor del edificio
                if (p.Work > 0) StartCoroutine(ScaffoldUp(v));
                return;
            }
            bool evolve = Island.Tier(p.Level) != v.ShownTier;
            if (evolve) StartCoroutine(EvolveShow(v));
            else StartCoroutine(UpgradeShow(v));
        }

        IEnumerator ScaffoldUp(PlotView v)
        {
            v.Scaffold.gameObject.SetActive(true);
            Sfx.Play("build", -6f, 0.9f);
            FxApi.Play("dust", v.Root.position + Vector3.up * 0.2f, new Color(0.85f, 0.75f, 0.6f), 1.4f);
            for (float t = 0f; t < 0.36f; t += Time.deltaTime)
            {
                v.Scaffold.localScale = new Vector3(1.15f, Mathf.Max(0.01f, OutBack(t / 0.36f)) * Mathf.Max(1f, v.Height / 2.2f), 1.15f);
                yield return null;
            }
        }

        // ------------------------------------------------------------ vetas
        void AddOre(Ore o)
        {
            if (ores.ContainsKey(o.Id)) return;
            var t = new GameObject("Veta" + o.Id).transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(o.X, 0f, o.Z);
            Material[] mats;
            var mesh = IslandArt.OreMesh(o.Kind, o.Id, out mats);
            var r = IslandArt.MakeRenderer(t, "Vis", mesh, mats);
            r.transform.localRotation = Quaternion.Euler(0, (o.Id * 73) % 360, 0);
            IslandArt.Blob(t, Island.Ores[o.Kind].Size * 1.5f, 0.32f);
            var v = new OreView { O = o, T = t, Vis = r.transform, GlintT = Random.value * 3f, Rend = r };
            ores[o.Id] = v;
            if (o.Age < 0.1f && !o.Sky && !o.Giant) v.Mound = MakeMound(t);
            if (o.Giant)
            {
                t.localScale = Vector3.one * (o.Legendary ? 3.5f : 2.8f);
                v.Aura = FxApi.Attach("aura", t, o.Legendary ? new Color(0.55f, 0.9f, 1f) : new Color(1f, 0.85f, 0.3f), o.Legendary ? 3.2f : 2.2f);
            }
            if (o.Age < 0.1f && !o.Sky) FxApi.Play("dust", t.position, new Color(0.8f, 0.7f, 0.5f), o.Giant ? 3f : 0.8f);
        }

        void OnHit(Ore o, Miner m)
        {
            OreView v;
            if (!ores.TryGetValue(o.Id, out v)) return;
            v.Shake = 1f;
            float sz = Island.Ores[o.Kind].Size * (o.Giant ? (o.Legendary ? 3.5f : 2.8f) : 1f);
            Vector3 at = v.T.position + Vector3.up * sz * 0.6f;
            FxApi.Play("hit_spark", at, default(Color), o.Giant ? 1.4f : 0.9f);
            if (Random.value < 0.5f) FxApi.Play("chips", at, IslandArt.OreCol[o.Kind], 0.7f);
            Juice.Punch(v.T, o.Giant ? 0.04f : 0.12f, 0.18f);   // el rebote va en la raiz; la escala de vida en Vis
            if (m != null) Sfx.Play("pick", -12f, 0.9f + o.Kind * 0.06f + Random.value * 0.08f);
        }

        void OnBreak(Ore o, Miner m)
        {
            OreView v;
            if (!ores.TryGetValue(o.Id, out v)) return;
            Vector3 c = v.T.position + Vector3.up * 0.3f;
            if (o.Giant)
            {
                FxApi.Play("boss_break", c + Vector3.up, new Color(1f, 0.85f, 0.3f), 2.4f);
                Debris(c + Vector3.up, 3, 10, 2.2f);
                DirtMark(new Vector3(o.X, 0f, o.Z), 2.2f);
                FxApi.Play("coin_burst", c + Vector3.up, default(Color), 2.5f);
                FxApi.Play("confetti", c + Vector3.up * 3f, default(Color), 2.5f);
                Sfx.Play("coins_pour", -3f);
                Sfx.Play("break", -2f, 0.7f);
                Sfx.PlayLater("goal", 0.3f, -3f);
                Sfx.PlayLater("cheer", 0.4f, -7f);
                Juice.SlowMo(0.35f, 0.4f);
                Juice.Vibrate(80);
                if (o.Legendary)
                {
                    // el gran premio: destello, segundo estallido de gemas y fanfarria
                    Ui.Flash(new Color(1f, 0.97f, 0.85f), 0.35f);
                    FxApi.Play("unlock_burst", c + Vector3.up * 1.5f, new Color(0.55f, 0.9f, 1f), 3f);
                    FxApi.Play("gem_sparkle", c + Vector3.up * 2f, IslandArt.OreCol[4], 3f);
                    Sfx.PlayLater("fanfare_short", 0.15f, -3f);
                    Sfx.PlayLater("crystal_chime", 0.45f, -6f);
                    Ui.Toast(Loc.T("¡Yacimiento legendario! +10 gemas y cofre de oro"), new Color(0.6f, 0.92f, 1f), null, true);
                }
                else if (!o.Boss) Ui.Toast(Loc.T("¡Veta Gigante rota! +2 gemas"), Kit3.Yellow);
                foreach (var mv in miners.Values) mv.Celebrate = 1f;
            }
            else
            {
                FxApi.Play("rock_break", c, IslandArt.OreCol[o.Kind], 1.1f);
                Debris(c, o.Kind, 4);
                DirtMark(new Vector3(o.X, 0f, o.Z), Island.Ores[o.Kind].Size);
                if (o.Kind >= 3) FxApi.Play("gem_sparkle", c, IslandArt.OreCol[o.Kind], 1.2f);
                Sfx.Play("break", -6f, 1f + o.Kind * 0.05f);
                MinerView mv;
                if (m != null && miners.TryGetValue(m.Id, out mv)) mv.Celebrate = o.Kind >= 3 ? 1f : 0.35f;
            }
            Destroy(v.T.gameObject);
            ores.Remove(o.Id);
        }

        void OnGiant(Ore o)
        {
            if (o.Boss) return;   // el golem tiene su propia llegada
            if (o.Legendary)
            {
                // raro a proposito: se anuncia distinto (rayos de luz, campanas y su cartel)
                Ui.Toast(Loc.T("¡YACIMIENTO LEGENDARIO! Pagá x5"), new Color(0.6f, 0.92f, 1f), null, true);
                FxApi.Play("rays", new Vector3(o.X, 1.2f, o.Z), new Color(0.6f, 0.92f, 1f), 3f);
                FxApi.Play("unlock_burst", new Vector3(o.X, 1.0f, o.Z), new Color(0.6f, 0.92f, 1f), 2.5f);
                Sfx.Play("milestone", -3f);
                Sfx.PlayLater("crystal_chime", 0.25f, -5f);
                Juice.Vibrate(90);
                Ui.MarkGiant(o);
                FocusOn(new Vector3(o.X, 0f, o.Z), true);
                return;
            }
            Ui.Toast(Loc.T("¡VETA GIGANTE! Todos a picar"), Kit3.Yellow);
            Sfx.Play("event_start", -3f);
            Sfx.Play("bell", -8f);
            Juice.Vibrate(50);
            Ui.MarkGiant(o);
            FocusOn(new Vector3(o.X, 0f, o.Z), true);   // momento raro y grande: la camara va hacia la veta
        }

        void OnGiantPaid(Ore o, double v)
        {
            OreView ov;
            Vector3 at = ores.TryGetValue(o.Id, out ov) ? ov.T.position + Vector3.up * 3.2f : new Vector3(o.X, 3f, o.Z);
            if (o.Dead) Ui.CoinsFrom(at, v);
            else Ui.Popup(at + Random.insideUnitSphere * 0.6f, "+" + BigNum.Fmt(v), new Color(1f, 0.87f, 0.3f), 24);
        }

        void OnMinePaid(double v)
        {
            var mine = Isl.Find(BKind.Mine);
            if (mine == null) return;
            Vector3 at = new Vector3(mine.X, plots[mine.Id].Height + 0.6f, mine.Z);
            Ui.CoinsFrom(at, v);
            FxApi.Play("sparkle", at, new Color(0.7f, 0.9f, 1f), 0.8f);
        }

        void OnDeposit(Miner m, double coins)
        {
            var dep = Isl.Find(BKind.Depot);
            Vector3 at = new Vector3(dep.X, plots[dep.Id].Height + 0.3f, dep.Z);
            Ui.CoinsFrom(at, coins);
            Sfx.Play("coin", -8f, 1f + Random.value * 0.15f);
            Juice.Punch(plots[dep.Id].Root, 0.05f, 0.18f);
        }

        void OnMood(Miner m, string mood)
        {
            Vector3 at = new Vector3(m.X, MH(2.4f), m.Z);
            switch (mood)
            {
                case "fresh":
                    Ui.Popup(at, Loc.T("¡Fresco!"), new Color(0.55f, 0.9f, 1f));
                    FxApi.Play("bubble", at, new Color(0.7f, 0.95f, 1f), 1.2f);
                    Sfx.Play("unlock", -10f, 1.3f);
                    break;
                case "fed": Ui.Popup(at, "¡Lleno!", new Color(1f, 0.8f, 0.4f)); Sfx.Play("blip", -12f); break;
                case "tired": Ui.Popup(at, "Zzz", new Color(0.8f, 0.85f, 1f)); break;
                case "cheer": MinerView mv; if (miners.TryGetValue(m.Id, out mv)) mv.Celebrate = 1f; break;
            }
        }

        // ------------------------------------------------------------ mineros
        void AddMiner(Miner m)
        {
            if (miners.ContainsKey(m.Id)) return;
            var model = MinerModel.Create(root, m.Id, RigFor(m));
            model.transform.localScale = Vector3.one * MinerScale;
            model.transform.localPosition = new Vector3(m.X, m.Y, m.Z);
            IslandArt.Blob(model.transform, 0.45f, 0.35f);
            var view = new MinerView { M = m, Model = model, BlinkT = Random.value * 3f };
            miners[m.Id] = view;
            RefreshMinerLook(view);
            if (m.State == MState.Spawning)
            {
                // detalle 1: llega como una chispa (desde la carta elegida o desde el cielo) y brota al tocar el suelo
                view.Arriving = true;
                model.transform.localScale = Vector3.one * 0.0001f;
                if (CardDrop != null)
                {
                    // invocado con carta: sale de la carta que cayo (IslandGame.Summon)
                    var info = CardDrop;
                    CardDrop = null;
                    SparkFromCanvas = null;
                    StartCoroutine(CardSummon(view, info));
                    return;
                }
                Vector2? from = SparkFromCanvas;
                SparkFromCanvas = null;
                Ui.FlySpark(from, model.transform.position + Vector3.up * 0.2f, m.Golden ? GoldCol : new Color(1f, 0.88f, 0.4f), () => StartCoroutine(MinerSprout(view)));
            }
        }

        /// <summary>Punto de la capa de vuelo desde donde sale la chispa del proximo minero (la carta elegida).</summary>
        public Vector2? SparkFromCanvas;

        /// <summary>La chispa toco el suelo: vibracion muy suave, anillo de luz y el minero brota estirandose.</summary>
        System.Collections.IEnumerator MinerSprout(MinerView v)
        {
            var m = v.M;
            var t = v.Model.transform;
            Vector3 at = t.position;
            Juice.Vibrate(12);
            FxApi.Play("ring", at + Vector3.up * 0.05f, m.Golden ? GoldCol : new Color(1f, 0.95f, 0.65f), 1.6f);
            FxApi.Play("unlock_burst", at + Vector3.up * 0.6f, m.Golden ? GoldCol : default(Color), m.Golden ? 1.8f : 1.1f);
            Sfx.Play("gem", -6f, 1.25f);
            Sfx.PlayLater("jingle_small", 0.15f, -6f);
            for (float k = 0f; k < 0.5f; k += Time.deltaTime)
            {
                float u = k / 0.5f;
                float s = Mineros.UI.Tw.Eval(Mineros.UI.Ease.OutBack, u);
                float sq = 1f + Mathf.Sin(u * Mathf.PI * 2f) * 0.25f * (1f - u);
                t.localScale = new Vector3(s / Mathf.Sqrt(sq), s * sq, s / Mathf.Sqrt(sq)) * MinerScale;
                yield return null;
            }
            t.localScale = Vector3.one * MinerScale;
            v.Arriving = false;
            v.Celebrate = 1f;
            Ui.Popup(at + Vector3.up * MH(2.4f), Loc.T("¡") + IslandUi.MinerName(m) + Loc.T(" llegó!"), m.Golden ? GoldCol : new Color(1f, 0.9f, 0.4f), 24);
        }

        public Vector3 MinerWorld(Miner m)
        {
            MinerView mv;
            return miners.TryGetValue(m.Id, out mv) ? mv.Model.transform.position : new Vector3(m.X, 0, m.Z);
        }

        void UpdateMiners(float dt)
        {
            UpdateSteps(dt);
            foreach (var mv in miners.Values)
            {
                var m = mv.M;
                var t = mv.Model.transform;
                Vector3 target = new Vector3(m.X, m.Y, m.Z);
                t.localPosition = Vector3.Lerp(t.localPosition, target, 1f - Mathf.Exp(-dt * 20f));
                float perf = Isl.Perf(m);
                // el paso va con el tamaño: un minero mas chico da mas pasos por metro
                if (m.Moving) mv.Phase += dt * 11f * perf * Isl.WalkMult(m) * (1.3f / MinerScale) * 0.8f;
                float swing = m.State == MState.Mining ? Mathf.Repeat(m.HitT / Island.HitInterval + 0.38f, 1f)
                    : m.State == MState.Digging ? Mathf.Repeat(m.HitT / 0.6f, 1f)
                    : m.State == MState.Building ? Mathf.Repeat(m.HitT / 0.45f + m.Id * 0.3f, 1f) : -1f;   // cavar y martillar
                mv.BlinkT -= dt;
                bool blink = mv.BlinkT < 0.12f || m.State == MState.Resting || m.State == MState.InDorm;
                if (mv.BlinkT < 0f) mv.BlinkT = 2.5f + Random.value * 3f;
                mv.Celebrate = Mathf.MoveTowards(mv.Celebrate, 0f, dt * 0.8f);
                mv.Model.SetCelebrate(m.State == MState.Spawning ? 1f : mv.Celebrate);
                var dir = new Vector3(Mathf.Sin(m.Face), 0f, Mathf.Cos(m.Face));
                mv.Model.SetPose(dir, mv.Phase, m.Moving, swing, blink, dt);
                // dentro de las duchas no se ve; comiendo se queda afuera de la cantina
                bool hidden = m.State == MState.Showering || (ComplexMode && m.Y > IslandArt.FloorY(ViewFloor) + 0.5f);   // en el Modo Cuartel, los de pisos de arriba no se ven
                if (hidden && t.localScale.x > MinerScale * 0.4f) { t.localScale = Vector3.one * 0.0001f; }
                else if (!hidden && !mv.Arriving && t.localScale.x < MinerScale * 0.4f) { t.localScale = Vector3.one * MinerScale; Juice.Punch(t, 0.25f, 0.3f); }
                // polvo al correr rapido (turbo o fresco)
                if (m.Moving && perf > 1.2f)
                {
                    mv.DustT -= dt;
                    if (mv.DustT <= 0f) { mv.DustT = 0.18f; FxApi.Play("tinydust", t.position + Vector3.up * 0.05f, default(Color), 0.6f); }
                }
                // bulto en la espalda
                if (m.CarryKind != mv.SackKind)
                {
                    if (mv.Sack != null) Destroy(mv.Sack.gameObject);
                    mv.Sack = null;
                    if (m.CarryKind >= 0)
                    {
                        Material[] mats;
                        var mesh = IslandArt.Sack(m.CarryKind, out mats);
                        mv.Sack = IslandArt.MakeRenderer(t, "Bulto", mesh, mats).transform;
                        Juice.Punch(mv.Sack, 0.4f, 0.3f);
                    }
                    mv.SackKind = m.CarryKind;
                }
                if (mv.Sack != null)
                {
                    mv.Sack.position = t.position + Vector3.up * MH(1.35f) - dir * MH(0.36f) + Vector3.up * Mathf.Abs(Mathf.Sin(mv.Phase)) * MH(0.05f);
                    mv.Sack.rotation = Quaternion.LookRotation(dir);
                }
                RefreshMinerLook(mv);
                MinerExtras(mv, dt);
                Ui.Status(m, t.position);
            }
        }

        // ------------------------------------------------------------ bucle
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            Isl.Tick(dt);
            UpdateOres(dt);
            UpdatePlots(dt);
            UpdateCity(dt);
            UpdateMiners(dt);
            UpdateWorld(dt);
            UpdateTease(dt);
            UpdateWeather(dt);
            UpdateSurprises(dt);
            UpdateDaily(dt);
            UpdateWonder(dt);
            UpdateComplex(dt);
            UpdateInput(dt);
            saveT += dt;
            if (saveT > 5f) { saveT = 0f; Save(); }
        }

        void UpdateOres(float dt)
        {
            bool frenzy = Isl.FrenzyT > 0f;
            foreach (var v in ores.Values)
            {
                var o = v.O;
                float size = Island.Ores[o.Kind].Size * (o.Giant ? 2.8f : 1f);
                float y, sq;
                if (o.Giant || o.Sky || v.Mound == null && o.Age >= 0.85f)
                {
                    // gigante: sube desde la tierra con rebote (squash & stretch)
                    float a = Mathf.Clamp01(o.Age / (o.Giant ? 1.0f : 0.55f));
                    y = Mathf.Lerp(-size, 0f, 1f - (1f - a) * (1f - a));
                    sq = a < 1f ? 1f + Mathf.Sin(a * Mathf.PI) * 0.3f : 1f;
                }
                else Emerge(v, size, out y, out sq);
                if (o.Sky) SkyFall(v, size, ref y, ref sq);
                v.Shake = Mathf.MoveTowards(v.Shake, 0f, dt * 6f);
                float hp = (float)(o.Hp / o.MaxHp);
                // con poca vida tiembla sola (falta poco para romperla)
                float tremble = hp < 0.3f && !o.Giant ? 0.35f : 0f;
                float sh = Mathf.Max(v.Shake, tremble);
                v.T.localPosition = new Vector3(o.X + Mathf.Sin(Time.time * 60f) * 0.03f * sh, y, o.Z + Mathf.Cos(Time.time * 53f) * 0.015f * sh);
                // aplastado del toque: Y 0.88 / XZ 1.08 y vuelve con rebote en 180 ms
                if (v.Squash > 0f)
                {
                    v.Squash = Mathf.Max(0f, v.Squash - dt / 0.18f);
                    float k = OutBack(1f - v.Squash);
                    float sy = Mathf.LerpUnclamped(0.88f, 1f, k);
                    sq *= sy;
                }
                if (v.Vis != null)
                    v.Vis.localScale = new Vector3(1f / Mathf.Sqrt(sq), sq, 1f / Mathf.Sqrt(sq)) * (0.75f + 0.25f * hp);
                // destello blanco del golpe y brillo dorado en frenesi
                float fl = v.Flash > 0f ? 1f : 0f;
                v.Flash = Mathf.Max(0f, v.Flash - dt);
                float fr = frenzy ? 0.35f + 0.25f * Mathf.Sin(Time.time * 10f + o.Id) : 0f;
                if (v.Rend != null && (fl > 0f || fr > 0f || v.Glowing))
                {
                    var mpb = new MaterialPropertyBlock();
                    v.Rend.GetPropertyBlock(mpb);
                    mpb.SetColor("_EmissionColor", fl > 0f ? new Color(0.42f, 0.42f, 0.4f) : new Color(1f, 0.75f, 0.25f) * fr);
                    v.Rend.SetPropertyBlock(mpb);
                    v.Glowing = fl > 0f || fr > 0f;
                }
                // brillo periodico en las vetas valiosas
                if (o.Kind >= 2 || o.Giant || frenzy)
                {
                    v.GlintT -= dt;
                    if (v.GlintT <= 0f)
                    {
                        v.GlintT = o.Giant ? 0.5f : frenzy ? 0.35f : 1.6f + Random.value * 2f;
                        FxApi.Play("glint", v.T.position + Vector3.up * size * (0.6f + Random.value * 0.4f) + Random.insideUnitSphere * size * 0.3f,
                            frenzy ? new Color(1f, 0.85f, 0.3f) : IslandArt.OreCol[o.Kind], o.Giant ? 1.5f : 0.7f);
                    }
                }
            }
            UpdateDebris(dt);
        }

        void UpdatePlots(float dt)
        {
            foreach (var v in plots)
            {
                bool visible = v.P.Ring <= Isl.Expand;
                if (v.Root.gameObject.activeSelf != visible) v.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                bool pad = v.P.Building < 0 && Isl.Offered(v.P) && Ui.ShowsPlus(v.P.Id);   // estacas solo donde hay "+"
                if (v.Pad.gameObject.activeSelf != pad) v.Pad.gameObject.SetActive(pad);
                bool building = v.P.BuildT >= 0f || v.P.Work > 0;
                bool hasB = v.P.Building >= 0;
                if (v.Blob.gameObject.activeSelf != hasB) v.Blob.gameObject.SetActive(hasB);
                if (v.Scaffold.gameObject.activeSelf != building) v.Scaffold.gameObject.SetActive(building);
                if (v.Body != null && !v.Driven)
                {
                    v.Punch = Mathf.MoveTowards(v.Punch, 0f, dt * 2.2f);
                    float s = 1f + Mathf.Sin(v.Punch * Mathf.PI * 3f) * 0.08f * v.Punch;
                    v.Body.localScale = new Vector3(s, s, s);
                }
                if (v.Body != null && v.P.BuildT < 0f)
                {
                    Chimney(v, dt);
                }
            }
        }

        /// <summary>
        /// Vida de los edificios (biblia 3.3): humo de chimenea en bolitas, vapor de la olla en la cantina, chispas en la
        /// herreria (con "clank" bajito si la camara esta cerca), burbujas en las duchas, polvo y golpes en la mina.
        /// </summary>
        void Chimney(PlotView v, float dt)
        {
            var k = (BKind)v.P.Building;
            v.SmokeT -= dt;
            if (v.SmokeT > 0f) return;
            Vector3 top = v.Root.position + Vector3.up * (v.Height * 0.95f);
            Vector3 front = v.Root.position - new Vector3(Cam.transform.forward.x, 0f, Cam.transform.forward.z).normalized * 1.3f;
            float near = Sound != null ? Sound.Zoom01 : 0.5f;
            bool onScreen = OnScreen(v.Root.position);
            switch (k)
            {
                case BKind.House:
                    v.SmokeT = 1.3f;
                    FxApi.Play("smoke", top + new Vector3(0.3f, 0, 0.3f), new Color(0.92f, 0.92f, 0.95f), 0.8f);
                    break;
                case BKind.Canteen:
                    v.SmokeT = 0.9f;
                    FxApi.Play("smoke", top + new Vector3(0.3f, 0, 0.3f), new Color(0.97f, 0.97f, 1f), 0.7f);
                    break;
                case BKind.Smithy:
                    v.SmokeT = 2f + Random.value * 1.2f;
                    FxApi.Play("smoke", top + new Vector3(0.3f, 0, 0.3f), new Color(0.5f, 0.48f, 0.46f), 0.8f);
                    FxApi.Play("hit_spark", front + Vector3.up * 0.9f, new Color(1f, 0.7f, 0.3f), 0.8f);
                    if (onScreen && near > 0.55f) Sfx.Play("anvil", -26f + 8f * near, 1.1f + Random.value * 0.15f);
                    break;
                case BKind.Showers:
                    bool busy = false;
                    foreach (var m in Isl.Miners) if (m.State == MState.Showering) { busy = true; break; }
                    v.SmokeT = busy ? 0.35f : 1.6f;
                    FxApi.Play("bubble", top + Random.insideUnitSphere * 0.4f, new Color(0.8f, 0.95f, 1f), busy ? 0.9f : 0.5f);
                    break;
                case BKind.Mine:
                    v.SmokeT = 1.8f + Random.value;
                    FxApi.Play("tinydust", front + Vector3.up * 0.2f, new Color(0.7f, 0.6f, 0.5f), 1.2f);
                    if (onScreen && near > 0.6f) Sfx.Play("pick", -30f + 8f * near, 0.8f);
                    break;
                default:
                    CityLife(v, top, front, onScreen, near);
                    break;
            }
        }

        bool OnScreen(Vector3 world)
        {
            Vector3 vp = Cam.WorldToViewportPoint(world);
            return vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        static float Ease(float t) { return 1f - Mathf.Pow(1f - t, 3f); }

        public Vector3 PlotWorld(Plot p) { return plots[p.Id].Root.position; }

        // ------------------------------------------------------------ guardado
        public void Save()
        {
            Isl.LastSeen = NowSeconds();
            store.Save(Isl.ToJson());
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) { Save(); IslandNotify.Schedule(Isl); }
            else IslandNotify.Cancel();   // volvio: los avisos programados ya no hacen falta
        }
        void OnApplicationQuit() { Save(); IslandNotify.Schedule(Isl); }
    }
}
