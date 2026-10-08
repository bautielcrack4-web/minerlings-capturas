using System;
using System.Collections.Generic;
using Mineros.Core;
using Mineros.Fx;
using Mineros.Game;
using Mineros.Miners;
using UnityEngine;
using UnityEngine.Rendering;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.World
{
    /// <summary>
    /// Mundo 3D: camara isometrica, luz, cañon diagonal, rocas, mineros, eventos y efectos.
    /// CONTRATO con la interfaz (no cambiar firmas): eventos y miembros publicos de abajo.
    /// Esta clase es parcial: WorldController.cs (contrato, camara, luces, bucle), .Rocks.cs, .Miners.cs, .Events.cs, .Visuals.cs.
    /// Los efectos y la sensacion de juego los pone el kit Fx/Juice; las piezas de arte, ArtKit/Palette.
    /// </summary>
    public sealed partial class WorldController : MonoBehaviour
    {
        // ---- contrato con la UI ----
        /// <summary>Roca rota: posicion en pantalla (pixeles, origen abajo-izquierda como Camera.WorldToScreenPoint), oro, jefe, rica.</summary>
        public event Action<Vector2, double, bool, bool> RockBroken;
        /// <summary>Roca Cofre rota: posicion en pantalla y gemas otorgadas (ya sumadas por Core).</summary>
        public event Action<Vector2, int> ChestBroken;
        /// <summary>Geoda rota: posicion en pantalla y gemas (ya otorgadas por Core).</summary>
        public event Action<Vector2, int> BossBroken;
        /// <summary>"boss_phase", "boss_break", "chest", "meteor_hit", "milestone".</summary>
        public event Action<string> BigMoment;
        /// <summary>Excavar: segundos extra por roca reloj.</summary>
        public event Action<float> TimeBonus;
        /// <summary>Excavar: combo actual (0 = se corto).</summary>
        public event Action<int> ComboChanged;

        /// <summary>Camara principal del mundo.</summary>
        public Camera Cam { get; private set; }
        /// <summary>Joystick del minijuego (-1..1).</summary>
        public Vector2 Joy { get; set; }
        public bool Paused { get; set; }
        public float PlayProgress { get; private set; }
        public double PlayGold { get; private set; }

        // ---- ayudas para que las subclases/partes disparen eventos ----
        internal void EmitRockBroken(Vector2 p, double gold, bool boss, bool rich) { RockBroken?.Invoke(p, gold, boss, rich); }
        internal void EmitChestBroken(Vector2 p, int gems) { ChestBroken?.Invoke(p, gems); }
        internal void EmitBossBroken(Vector2 p, int gems) { BossBroken?.Invoke(p, gems); }
        internal void EmitBigMoment(string k) { BigMoment?.Invoke(k); }
        internal void EmitTimeBonus(float s) { TimeBonus?.Invoke(s); }
        internal void EmitCombo(int n) { ComboChanged?.Invoke(n); }
        internal void SetPlay(float progress, double gold) { PlayProgress = progress; PlayGold = gold; }

        // ================================================================== estado interno
        const float CamDist = 40f;
        const float HalfWidth = 5.0f;

        GameManager gm;
        GameState G;
        bool playMode;
        int biome = -1;
        BiomePal pal;
        System.Random rng = new System.Random();

        Transform root, rocksRoot, minersRoot, camRig;
        Light sun;
        readonly Light[] lamps = new Light[3];
        CanyonBuilder canyon;
        FloatTexts texts;

        float time;
        float goldK, frenzyK;
        string evNow = "";
        float biomeCheckT;
        bool lastEco;
        Vector2 camFocus;
        float lastAspect = -1f;
        float lightKey = -1f;
        bool lightDirty = true;
        bool inited;

        // ================================================================== inicio
        public void Init(GameManager manager)
        {
            gm = manager;
            G = gm.G;
            WorldMaterials.Build();
            root = new GameObject("WorldRoot").transform;
            root.SetParent(transform, false);
            SetupCamera();
            rocksRoot = new GameObject("Rocks").transform;
            rocksRoot.SetParent(root, false);
            minersRoot = new GameObject("Miners").transform;
            minersRoot.SetParent(root, false);
            texts = new FloatTexts(root, 36);
            canyon = new CanyonBuilder(root);
            SetupLights();
            G.EquipChanged += OnEquipChanged;
            G.StageCleared += OnStageCleared;
            G.MilestoneReached += OnMilestone;
            G.EventStarted += OnEventStarted;
            G.EventEnded += OnEventEnded;
            G.Upgraded += OnUpgraded;
            FxApi.Eco = G.Eco;
            lastEco = G.Eco;
            inited = true;
            SetupIdle();
        }

        void OnDestroy()
        {
            if (G != null)
            {
                G.EquipChanged -= OnEquipChanged;
                G.StageCleared -= OnStageCleared;
                G.MilestoneReached -= OnMilestone;
                G.EventStarted -= OnEventStarted;
                G.EventEnded -= OnEventEnded;
                G.Upgraded -= OnUpgraded;
            }
            if (Juice.Cam == Cam) Juice.Cam = null;
            StopGoldFx();
            DestroyVisuals();
        }

        void SetupCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera");
                Cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            // La camara cuelga de un soporte que se mueve con el seguimiento; Juice (sacudida/zoom) mueve solo la camara.
            camRig = new GameObject("CamRig").transform;
            camRig.SetParent(root, false);
            Cam.transform.SetParent(camRig, false);
            Cam.transform.localPosition = new Vector3(0f, 0f, -CamDist);
            Cam.transform.localRotation = Quaternion.identity;
            Cam.orthographic = true;
            Cam.nearClipPlane = 1f;
            Cam.farClipPlane = 85f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.allowHDR = false;
            Cam.allowMSAA = true;
            Cam.useOcclusionCulling = false;
            camRig.rotation = W.CamRot;
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null) Cam.gameObject.AddComponent<AudioListener>();
            UpdateOrtho();
            Juice.Cam = Cam;
            QualitySettings.pixelLightCount = 4;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowCascades = 0;
            QualitySettings.shadowDistance = 58f;   // camara a 40 + medio alto visible: mas texeles por metro (sombras nitidas)
            RenderSettings.fog = false;
        }

        /// <summary>Fija el tamano ortografico segun la relacion de aspecto (solo cuando cambia: no pisa el ZoomPunch de Juice).</summary>
        void UpdateOrtho()
        {
            float asp = Mathf.Max(0.2f, Cam.aspect);
            if (Mathf.Approximately(asp, lastAspect)) return;
            lastAspect = asp;
            Cam.orthographicSize = Mathf.Max(HalfWidth / asp, 9f);
        }

        void SetupLights()
        {
            var sg = new GameObject("Sol");
            sg.transform.SetParent(root, false);
            sg.transform.rotation = Quaternion.Euler(52f, 78f, 0f); // calida, desde arriba-izquierda de la pantalla
            sun = sg.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sun.shadowNearPlane = 0.2f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            for (int i = 0; i < lamps.Length; i++)
            {
                var lg = new GameObject("Lampara" + i);
                lg.transform.SetParent(root, false);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 5.5f;
                l.intensity = 0f;
                l.color = new Color(1f, 0.93f, 0.7f);
                l.shadows = LightShadows.None;
                l.enabled = false;
                lamps[i] = l;
            }
        }

        static readonly Color GoldSun = new Color(1f, 0.82f, 0.45f);
        static readonly Color FrenzySun = new Color(1f, 0.72f, 0.45f);

        /// <summary>Color e intensidad de la luz segun bioma y eventos (Fiebre de Oro dora la luz; Frenesi la calienta).</summary>
        void ApplyLighting()
        {
            if (pal == null) return;
            float key = goldK * 7f + frenzyK * 13f;
            if (!lightDirty && Mathf.Approximately(key, lightKey)) return;
            lightDirty = false;
            lightKey = key;
            Color sc = Color.Lerp(pal.Sun, GoldSun, goldK * 0.55f);
            sc = Color.Lerp(sc, FrenzySun, frenzyK * 0.25f);
            sun.color = sc;
            sun.intensity = pal.SunI * (1f + 0.12f * goldK);
            Color tint = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.62f), goldK * 0.5f);
            tint = Color.Lerp(tint, new Color(1f, 0.85f, 0.7f), frenzyK * 0.2f);
            RenderSettings.ambientSkyColor = pal.AmbSky * tint;
            RenderSettings.ambientEquatorColor = pal.AmbEq * tint;
            RenderSettings.ambientGroundColor = pal.AmbGround * tint;
            sun.shadowStrength = pal.Dark > 0f ? 0.55f : 0.7f;
        }

        // ================================================================== biomas
        /// <summary>Cambia la paleta: libera rocas, recrea mallas del bioma, fondo y luz. No mueve mineros.</summary>
        void ApplyBiome(int b)
        {
            b = ((b % 4) + 4) % 4;
            ReleaseAllRocks();
            biome = b;
            pal = Biomes.Get(b);
            RockFactory.SetBiome(b);
            canyon.Eco = G.Eco;
            canyon.SetBiome(b);
            Cam.backgroundColor = pal.Void;
            motes.Clear();
            lightDirty = true;
            ApplyLighting();
            for (int i = 0; i < lamps.Length; i++) lamps[i].enabled = false;
        }

        // ================================================================== configuraciones
        /// <summary>Arma el cañon del bioma actual con mineros y rocas.</summary>
        public void SetupIdle()
        {
            if (!inited) return;
            playMode = false;
            ClearWorldState();
            ApplyBiome(G.Biome());
            RebuildMiners();
            for (int i = 0; i < miners.Count; i++)
            {
                var m = miners[i];
                m.pos = W.CorridorPoint(-60f * W.PX * m.idx, (m.idx - 1) * 0.35f);
                m.face = W.F2;
                m.target = null;
            }
            SnapCamera();
            canyon.EnsureRange(camFocus.y - 26f, camFocus.y + 34f, 99);
            for (int i = 0; i < 34; i++) SpawnIdleRock(true);
            evNow = "";
            string ev = G.ActiveEvent();
            if (ev != "") OnEventStarted(ev, G.EventTimeLeft());
        }

        /// <summary>Arma la meseta circular del minijuego Excavar.</summary>
        public void SetupPlay()
        {
            PlayProgress = 0;
            PlayGold = 0;
            if (!inited) return;
            playMode = true;
            ClearWorldState();
            combo = 0;
            ApplyBiome(G.Biome() + 1);
            RebuildMiners();
            for (int i = 0; i < miners.Count; i++)
            {
                var m = miners[i];
                m.pos = new Vector2((m.idx - 1) * 90f, -m.idx * 40f) * W.PX;
                m.face = W.F2;
                m.target = null;
                m.player = i == 0;
            }
            canyon.BuildArena();
            SnapCamera();
            int tries = 0;
            float arena = W.ArenaR;
            while (rocks.Count < 46 && tries < 600)
            {
                tries++;
                float a = R01() * Mathf.PI * 2f;
                float d = Mathf.Sqrt(R01()) * (arena - 110f * W.PX);
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (p.magnitude < 170f * W.PX || !FreeSpot(p, 135f * W.PX)) continue;
                float roll = R01();
                var rk = AddRock(p, roll < 0.22f, true);
                if (roll > 0.93f) MakeClock(rk);
            }
        }

        void ClearWorldState()
        {
            ReleaseAllRocks();
            texts.Clear();
            ClearMeteors();
            ClearLids();
            StopGoldFx();
            StopMinerFx();
            HideBossLabel();
            goldK = frenzyK = 0f;
            evNow = "";
            lightDirty = true;
        }

        void SnapCamera()
        {
            camFocus = CamTarget();
            PlaceCamera();
        }

        // ================================================================== camara
        Vector2 CamTarget()
        {
            if (miners.Count == 0) return camFocus;
            if (playMode) return miners[0].pos + W.F2 * 1.1f;
            return AvgMiner() + W.F2 * 3.4f;
        }

        void UpdateCamera(float dt)
        {
            Vector2 tgt = CamTarget();
            camFocus = Vector2.Lerp(camFocus, tgt, Mathf.Min(1f, dt * (playMode ? 6f : 1.6f)));
        }

        void PlaceCamera()
        {
            camRig.rotation = W.CamRot;
            camRig.position = new Vector3(camFocus.x, 0.4f, camFocus.y);
        }

        /// <summary>Punto del suelo (altura y) bajo un punto de la pantalla (coordenadas de viewport 0..1).</summary>
        Vector3 GroundFromViewport(float vx, float vy, float height)
        {
            Ray ray = Cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
            float t = ray.direction.y != 0f ? (height - ray.origin.y) / ray.direction.y : 0f;
            return ray.origin + ray.direction * t;
        }

        Vector2 ScreenPos(Vector3 world)
        {
            Vector3 s = Cam.WorldToScreenPoint(world);
            return new Vector2(s.x, s.y);
        }

        // ================================================================== bucle
        void Update()
        {
            if (!inited || G == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // el tiempo de juego respeta HitStop / SlowMo de Juice (Time.timeScale)
            float sdt = Mathf.Min(Time.deltaTime, 0.05f);

            biomeCheckT -= dt;
            if (biomeCheckT <= 0f)
            {
                biomeCheckT = 0.5f;
                if (!playMode && G.Biome() != biome) OnBiomeChanged();
                if (G.Eco != lastEco) ApplyEco();
            }

            if (!Paused && sdt > 0f)
            {
                time += sdt;
                for (int i = 0; i < miners.Count; i++) UpdateMiner(miners[i], sdt);
                UpdateRocks(sdt);
                if (!playMode) UpdateEvents(sdt);
                if (comboT > 0f)
                {
                    comboT -= sdt;
                    if (comboT <= 0f && combo > 0)
                    {
                        combo = 0;
                        EmitCombo(0);
                    }
                }
                if (!playMode)
                {
                    if (G.NeedBoss && (boss == null || boss.dead)) SpawnBoss();
                    int alive = 0;
                    for (int i = 0; i < rocks.Count; i++)
                        if (!rocks[i].dead && rocks[i].kind != 4) alive++;
                    if (alive < targetRocks + ((boss != null && !boss.dead) ? 1 : 0)) SpawnIdleRock(false);
                }
                UpdateMotes(sdt);
            }
            if (!Paused) UpdateCamera(dt);

            UpdateOrtho();
            PlaceCamera();

            if (!playMode) canyon.EnsureRange(camFocus.y - 26f, camFocus.y + 34f, 1);

            UpdateMinerVisuals(sdt > 0f ? sdt : dt * 0.01f);
            for (int i = 0; i < rocks.Count; i++) UpdateRockVisual(rocks[i]);
            for (int i = leaving.Count - 1; i >= 0; i--) UpdateRockVisual(leaving[i]);

            ApplyLighting();
            UpdateLamps();
            texts.MaxActive = G.Eco ? 14 : 40;
            texts.Update(sdt, Cam);
            UpdateAmbientFx(dt, sdt);
        }

        void OnBiomeChanged()
        {
            // cambio de mundo: nueva paleta; los mineros siguen donde estan
            ApplyBiome(G.Biome());
            canyon.EnsureRange(camFocus.y - 26f, camFocus.y + 34f, 99);
            for (int i = 0; i < 34; i++) SpawnIdleRock(true);
            string ev = G.ActiveEvent();
            evNow = ev;
            if (ev == "chest") SpawnChest();
        }

        void ApplyEco()
        {
            lastEco = G.Eco;
            FxApi.Eco = G.Eco;
            QualitySettings.shadowResolution = G.Eco ? ShadowResolution.Medium : ShadowResolution.VeryHigh;
            canyon.Eco = G.Eco;
        }

        void OnEquipChanged()
        {
            RebuildMiners();
        }

        void OnStageCleared(int w, int s)
        {
            Celebrate();
        }
    }
}
