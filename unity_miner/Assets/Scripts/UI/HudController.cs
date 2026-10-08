using System;
using System.Collections;
using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Game;
using Mineros.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FxJuice = Mineros.Fx.Juice;

namespace Mineros.UI
{
    /// <summary>Ajusta un RectTransform estirado al area segura de la pantalla (muescas, barras).</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect last;
        Vector2 lastSize;

        void OnEnable() { Apply(); }

        void Update()
        {
            if (Screen.safeArea != last || lastSize.x != Screen.width || lastSize.y != Screen.height) Apply();
        }

        void Apply()
        {
            Rect sa = Screen.safeArea;
            float w = Mathf.Max(Screen.width, 1), h = Mathf.Max(Screen.height, 1);
            last = sa;
            lastSize = new Vector2(Screen.width, Screen.height);
            RectTransform rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / w, sa.yMin / h);
            rt.anchorMax = new Vector2(sa.xMax / w, sa.yMax / h);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Interfaz completa (uGUI armado por codigo con sprites Kenney 9-slice) y orquestacion del flujo de main.gd:
    /// HUD, paneles, desbloqueos, celebraciones, monedas voladoras, Excavar, transiciones y arranque.
    /// Init se llama despues de GameManager.Init y de WorldController.Init.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        static HudController instance;

        GameManager gm;
        GameState G { get { return gm.G; } }
        WorldController world;

        Canvas canvas;
        RectTransform canvasRT, hudLayer, flyLayer, fxLayer, panelsLayer, topLayer;
        HudView hud;
        Flyer flyer;
        Confetti confetti;
        Toaster toaster;
        UiDirector director;
        PlayHud playHud;
        string mode = "idle";
        bool rebirthPending;
        int lastGain;
        readonly Dictionary<string, BasePanel> openPanels = new Dictionary<string, BasePanel>();

        public string Mode { get { return mode; } }

        /// <summary>Muestra un mensaje corto flotante.</summary>
        public static void Toast(string msg)
        {
            if (instance != null && instance.toaster != null) instance.toaster.Show(msg);
        }

        /// <summary>Refresca los valores del HUD (despues de cambios que Core no notifica).</summary>
        public static void RefreshHud()
        {
            if (instance != null && instance.hud != null) instance.hud.Refresh();
        }

        const string VibKey = "mineros_vibration";

        /// <summary>Activa o apaga la vibracion (Ajustes) y lo recuerda entre sesiones.</summary>
        public static void SetVibration(bool on)
        {
            FxJuice.VibrationOn = on;
            try
            {
                PlayerPrefs.SetInt(VibKey, on ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (Exception) { }
        }

        public void Init(GameManager manager)
        {
            gm = manager;
            world = gm.World;
            instance = this;
            Sfx.SoundOn = G.Sound;
            Sfx.MusicOn = G.Music;
            try { FxJuice.VibrationOn = PlayerPrefs.GetInt(VibKey, 1) != 0; }
            catch (Exception) { FxJuice.VibrationOn = true; }
            Mineros.Fx.Fx.Eco = G.Eco;
            gm.Mode = mode;
            EnsureEventSystem();
            Tw.Init(transform);   // antes de construir: los tweens de la construccion no se pierden
            BuildCanvas();
            Wire();
            StartCoroutine(Startup());
        }

        // ================================================================ canvas y capas
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            bool done = false;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Type t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null)
            {
                es.AddComponent(t);
                done = true;
            }
#endif
            if (!done) es.AddComponent<StandaloneInputModule>();
        }

        void BuildCanvas()
        {
            GameObject cgo = new GameObject("MinerosUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(720f, 1544f);
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight = 0.5f;
            cs.referencePixelsPerUnit = 100f;
            canvasRT = (RectTransform)cgo.transform;
            Kit.CanvasRT = canvasRT;

            // vineta suave detras del HUD
            RectTransform back = Kit.New("Back", canvasRT);
            Kit.Stretch(back);
            Image vig = Kit.Img(back, Icons.Vignette(), Color.white, "Vignette");
            Kit.Stretch(vig.rectTransform);

            hudLayer = Kit.New("HudLayer", canvasRT);
            Kit.Stretch(hudLayer);
            hudLayer.gameObject.AddComponent<SafeAreaFitter>();
            flyLayer = Kit.New("FlyLayer", canvasRT);
            Kit.Stretch(flyLayer);
            fxLayer = Kit.New("FxLayer", canvasRT);
            Kit.Stretch(fxLayer);
            panelsLayer = Kit.New("PanelsLayer", canvasRT);
            Kit.Stretch(panelsLayer);
            topLayer = Kit.New("TopLayer", canvasRT);
            Kit.Stretch(topLayer);

            hud = new GameObject("Hud", typeof(RectTransform)).AddComponent<HudView>();
            hud.transform.SetParent(hudLayer, false);
            Kit.Stretch((RectTransform)hud.transform);
            hud.Init(gm, (RectTransform)hud.transform, fxLayer);

            GameObject fgo = new GameObject("Flyer", typeof(RectTransform));
            fgo.transform.SetParent(flyLayer, false);
            Kit.Stretch((RectTransform)fgo.transform);
            flyer = fgo.AddComponent<Flyer>();
            flyer.Init(gm);

            GameObject cfo = new GameObject("Confetti", typeof(RectTransform));
            cfo.transform.SetParent(fxLayer, false);
            Kit.Stretch((RectTransform)cfo.transform);
            confetti = cfo.AddComponent<Confetti>();
            confetti.Init(gm);

            toaster = Toaster.Create(topLayer);
            director = UiDirector.Create(transform, gm, hud, panelsLayer, fxLayer, () => mode);
        }

        void Wire()
        {
            hud.OpenPanel += name => OpenPanel(name);
            hud.PlayPressed += StartPlay;
            hud.GoalClaimed += OnGoalClaimed;
            G.StageCleared += OnStageCleared;
            G.BossNeeded += OnBossNeeded;
            G.Rebirthed += OnRebirthed;
            G.Toast += OnCoreToast;
            G.Changed += OnGameChanged;
            flyer.Arrived += OnCoinArrived;
            flyer.GemArrived += OnGemArrived;
            world.RockBroken += OnRockBroken;
            world.ChestBroken += OnWorldGems;
            world.BossBroken += OnWorldGems;
            world.BigMoment += OnBigMoment;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (gm == null || gm.G == null) return;
            G.StageCleared -= OnStageCleared;
            G.BossNeeded -= OnBossNeeded;
            G.Rebirthed -= OnRebirthed;
            G.Toast -= OnCoreToast;
            G.Changed -= OnGameChanged;
            if (world != null)
            {
                world.RockBroken -= OnRockBroken;
                world.ChestBroken -= OnWorldGems;
                world.BossBroken -= OnWorldGems;
                world.BigMoment -= OnBigMoment;
            }
        }

        // ================================================================ arranque
        IEnumerator Startup()
        {
            yield return null;
            Sfx.PlayMusic(G.Biome());
            if (G.PendingOffline > 1.0)
            {
                BasePanel w = OpenPanel("welcome");
                if (w != null)
                {
                    bool closed = false;
                    w.Closed += () => closed = true;
                    while (!closed) yield return null;
                    yield return new WaitForSecondsRealtime(0.3f);
                }
            }
            if (G.LoginAvailable() && G.IsUnlocked("missions"))
            {
                BasePanel d = OpenPanel("daily");
                if (d != null)
                {
                    bool closed = false;
                    d.Closed += () => closed = true;
                    while (!closed) yield return null;
                }
            }
            director.StartDirector();
        }

        // ================================================================ paneles
        internal BasePanel OpenPanel(string name)
        {
            BasePanel existing;
            if (openPanels.TryGetValue(name, out existing) && existing != null && !existing.Closing) return existing;
            BasePanel p = null;
            switch (name)
            {
                case "shop": p = BasePanel.Open<ShopPanel>(panelsLayer, gm); break;
                case "missions": p = BasePanel.Open<MissionsPanel>(panelsLayer, gm); break;
                case "tools": p = BasePanel.Open<ToolsPanel>(panelsLayer, gm); break;
                case "daily": p = BasePanel.Open<DailyPanel>(panelsLayer, gm); break;
                case "skin": p = BasePanel.Open<SkinPanel>(panelsLayer, gm); break;
                case "rebirth": p = BasePanel.Open<EssencePanel>(panelsLayer, gm); break;
                case "settings": p = BasePanel.Open<SettingsPanel>(panelsLayer, gm); break;
                case "piggy": p = BasePanel.Open<PiggyPanel>(panelsLayer, gm); break;
                case "stats": p = BasePanel.Open<AchievementsPanel>(panelsLayer, gm, a => a.StartTab = 1); break;
                case "achievements": p = BasePanel.Open<AchievementsPanel>(panelsLayer, gm); break;
                case "welcome": p = BasePanel.Open<WelcomePanel>(panelsLayer, gm); break;
            }
            if (p != null) openPanels[name] = p;
            return p;
        }

        // ================================================================ bucle
        /// <summary>Boton atras de Android (Escape): cierra el panel de arriba, o sale de Excavar si no hay resultado.</summary>
        void HandleBack()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            for (int i = panelsLayer.childCount - 1; i >= 0; i--)
            {
                BasePanel p = panelsLayer.GetChild(i).GetComponent<BasePanel>();
                if (p != null && !p.Closing)
                {
                    p.Close();
                    return;
                }
            }
#endif
        }

        void Update()
        {
            if (gm == null || flyer == null) return;
            HandleBack();
            if (mode == "idle")
            {
                flyer.Target = hud.GoldTarget();
                flyer.GemTarget = hud.GemTarget();
            }
            else if (playHud != null)
            {
                flyer.Target = Kit.ToCanvas(playHud.GoldPill.Root).position + new Vector2(10, 22);
            }
        }

        // ================================================================ monedas y gemas
        void OnRockBroken(Vector2 screenPos, double gold, bool boss, bool rich)
        {
            Vector2 p = Kit.ScreenToCanvas(screenPos);
            if (mode == "idle") flyer.Burst(p, gold, boss ? 12 : (rich ? 7 : 4));
            else flyer.Burst(p, 0.0, rich ? 5 : 3);
        }

        /// <summary>Cada moneda que llega suma su parte del oro (idle). En Excavar valen 0 (el oro lo suma PlayHud al final).</summary>
        void OnCoinArrived(double v)
        {
            if (v > 0.0) G.AddGold(v);
            Sfx.Play("coin", -12f);
            if (mode == "idle") hud.BumpGold();
            else if (playHud != null) Tw.Pop(playHud.GoldPill.Root);
        }

        void OnGemArrived(int n)
        {
            if (hud != null) hud.GemsArrived(n);
        }

        /// <summary>Momentos grandes del mundo: vibracion leve (solo en idle; en Excavar no se interrumpe el pulso).</summary>
        void OnBigMoment(string kind)
        {
            if (mode != "idle") return;
            if (kind == "boss_break" || kind == "chest" || kind == "milestone") Kit.Buzz(35);
        }

        /// <summary>Roca Cofre / Geoda rota: las gemas ya las otorgo Core; solo vuelan (la cifra espera a que lleguen).</summary>
        void OnWorldGems(Vector2 screenPos, int gems)
        {
            if (gems <= 0 || mode != "idle") return;
            hud.GemsInFlight(gems);
            flyer.BurstGems(Kit.ScreenToCanvas(screenPos), gems);
        }

        void OnGoalClaimed(int gems, Vector2 from)
        {
            flyer.BurstGems(from, gems);
        }

        // ================================================================ eventos del juego
        void OnGameChanged() { Mineros.Fx.Fx.Eco = G.Eco; }

        void OnCoreToast(string msg) { if (toaster != null) toaster.Show(msg); }

        void OnBossNeeded() { Sfx.Play("clear", -6f); }

        void OnRebirthed(int gain)
        {
            rebirthPending = true;
            lastGain = gain;
        }

        static string BiomeDisplay(int b)
        {
            string n = Content.BiomeNames[b];
            return n == "Volcan" ? "Volcán" : n;
        }

        void OnStageCleared(int w, int s)
        {
            if (w == 0)
            {
                // reinicio (borrar progreso) o renacer
                if (rebirthPending)
                {
                    rebirthPending = false;
                    RunTransition("Renaciendo", () =>
                    {
                        world.SetupIdle();
                        Sfx.PlayMusic(G.Biome());
                        hud.Refresh();
                    });
                    director.Celeb.Enqueue(RebirthJob);
                }
                else
                {
                    world.SetupIdle();
                    hud.Refresh();
                }
                return;
            }
            Sfx.Play("clear");
            bool worldDone = s == Balance.SubsPerWorld;
            int ww = w;
            director.Celeb.Enqueue(() => StageJob(ww, worldDone));
        }

        IEnumerator StageJob(int w, bool worldDone)
        {
            confetti.Fire(worldDone ? "¡Mundo " + w + " completado!" : "¡Etapa superada!");
            Kit.Buzz(worldDone ? 60 : 40);
            yield return new WaitForSecondsRealtime(worldDone ? 1.9f : 1.7f);
            if (worldDone)
            {
                RunTransition("Bajando a " + BiomeDisplay(G.Biome()), () =>
                {
                    world.SetupIdle();
                    Sfx.PlayMusic(G.Biome());
                    hud.Refresh();
                });
                yield return new WaitForSecondsRealtime(1.4f);
            }
        }

        IEnumerator RebirthJob()
        {
            yield return new WaitForSecondsRealtime(1.3f);
            Sfx.Play("milestone", -2f);
            Kit.Buzz(60);
            yield return StartCoroutine(director.Celeb.Cartel("¡Renaciste!", "+" + lastGain + " Esencia", "rebirth", Kit.Purple));
        }

        // ================================================================ transiciones y Excavar
        void RunTransition(string text, Action mid)
        {
            Color helmet = Color.yellow;
            int sk = Mathf.Clamp(G.Skin, 0, Content.Skins.Length - 1);
            helmet = Icons.H(Content.Skins[sk].ColorHex);
            Transition.Run(topLayer, text, mid, helmet, G.Biome());
        }

        internal void StartPlay()
        {
            if (mode != "idle") return;
            RunTransition("Minero en camino", () =>
            {
                mode = "play";
                gm.Mode = mode;
                G.EventsEnabled = false;   // sin eventos mientras se excava
                hudLayer.gameObject.SetActive(false);
                for (int i = panelsLayer.childCount - 1; i >= 0; i--) Destroy(panelsLayer.GetChild(i).gameObject);
                openPanels.Clear();
                world.SetupPlay();
                Sfx.PlayMusic(G.Biome());
                playHud = PlayHud.Create(topLayer, hudLayer, gm);
                playHud.Finished += OnPlayFinished;
                playHud.transform.SetAsFirstSibling();
            });
        }

        void OnPlayFinished(bool won, double gold, int gems, int stars)
        {
            G.AddGold(gold);
            if (gems > 0) G.AddGems(gems);
            G.AddDaily("plays", 1);
            G.OnPlayFinished(won, stars);
            RunTransition("Volviendo a la mina", () =>
            {
                mode = "idle";
                gm.Mode = mode;
                G.EventsEnabled = true;
                if (playHud != null)
                {
                    Destroy(playHud.gameObject);
                    playHud = null;
                }
                world.Paused = false;
                world.Joy = Vector2.zero;
                world.SetupIdle();
                Sfx.PlayMusic(G.Biome());
                hudLayer.gameObject.SetActive(true);
                hud.Refresh();
            });
        }
    }
}
