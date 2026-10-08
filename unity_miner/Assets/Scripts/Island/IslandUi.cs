using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Interfaz de la Isla Minera (uGUI con el Kit del juego): barra de mineros/monedas/gemas, rastreador de metas,
    /// avisos cortos, "+" sobre parcelas libres, hoja de construir/mejorar, tarjeta de minero, turbo, ampliar la isla,
    /// ajustes, bienvenida con lo ganado sin conexion, globo del pedido del barco, marca de la veta gigante y la mano del
    /// tutorial. Las monedas vuelan al contador y los numeros ruedan.
    /// </summary>
    public sealed partial class IslandUi : MonoBehaviour
    {
        IslandGame game;
        Island Isl { get { return game.Isl; } }
        RectTransform canvasRT, worldLayer, hudLayer, flyLayer, toastLayer, sheetLayer;
        Pill coinPill, gemPill, minerPill;
        double shownCoins, shownGems;
        IslandPerfHud perfHud;
        int perfTaps; float perfTapT;

        public bool BlocksWorld { get { return sheet != null || summonActive; } }

        static readonly Color Ink = Icons.H("2b2440");

        // ------------------------------------------------------------ armado
        public void Init(IslandGame g)
        {
            game = g;
            EnsureEventSystem();
            Tw.Init(transform);
            LoadSettings();
            var cgo = new GameObject("IslaUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var cs = cgo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(720f, 1544f);
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight = 0.5f;
            canvasRT = (RectTransform)cgo.transform;
            Kit.CanvasRT = canvasRT;
            // cada capa con su propio lienzo: lo que cambia cada cuadro (marcas del mundo, vuelos, numeros) no obliga a
            // recalcular el resto (docs/PLAN_PULIDO.md 2.8)
            worldLayer = Layer("Mundo", true);
            hudLayer = Layer("Hud", true);
            hudLayer.gameObject.AddComponent<SafeAreaFitter>();
            sheetLayer = Layer("Hojas", true);
            flyLayer = Layer("Vuelo", false);   // por encima de las hojas: lo que vuela siempre se ve
            toastLayer = Layer("Avisos", false);
            toastLayer.gameObject.AddComponent<SafeAreaFitter>();

            // HUD de arriba: solo monedas y gemas (y los mineros cuando importan), chicos y sobre vidrio oscuro
            minerPill = GlassPill(hudLayer, "miner", 104);
            Kit.Place(minerPill.Root, 0f, 0f, 20f, 22f, 104, 42);
            coinPill = GlassPill(hudLayer, "coin", 168);
            Kit.Place(coinPill.Root, 0.5f, 0f, -84f, 22f, 168, 42);
            gemPill = GlassPill(hudLayer, "gem", 104);
            Kit.Place(gemPill.Root, 1f, 0f, -124f, 22f, 104, 42);
            BuildGoal();
            BuildButtons();
            BuildCityHud();
            BuildMenu();
            // tocar las gemas abre la tienda
            gemPill.Root.GetComponent<Image>().raycastTarget = true;
            gemPill.Root.gameObject.AddComponent<Btn>().Clicked += () => { if (Isl.TutDone) OpenShop(); };
            // 5 toques rapidos sobre las monedas: medidor de FPS para probar en el telefono (IslandPerfHud)
            perfHud = IslandPerfHud.Make(toastLayer);
            coinPill.Root.GetComponent<Image>().raycastTarget = true;
            coinPill.Root.gameObject.AddComponent<Btn>().Clicked += () =>
            {
                perfTaps = Time.unscaledTime - perfTapT < 0.45f ? perfTaps + 1 : 1;
                perfTapT = Time.unscaledTime;
                if (perfTaps >= 5) { perfTaps = 0; perfHud.Toggle(); }
            };
            shownCoins = Isl.Coins;
            shownGems = Isl.Gems;
            RefreshHud(true);
            Mineros.UI.Motion.Reduced = PlayerPrefs.GetInt("isla_menosmov", 0) == 1;
            if (game.OfflineGain > 0) Welcome(game.OfflineGain);
            else
            {
                Flash(Color.white, 0.55f);   // la isla aparece desde blanco (arranque y cambio de idioma)
                if (game.DailyPending && Isl.TutDone) Tw.After(this, "diario", 1.2f, OpenDaily);
            }
        }

        RectTransform Layer(string n, bool touch)
        {
            var r = Kit.New(n, canvasRT);
            Kit.Stretch(r);
            r.gameObject.AddComponent<Canvas>();
            if (touch) r.gameObject.AddComponent<GraphicRaycaster>();
            return r;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }

        // ------------------------------------------------------------ HUD
        void RefreshHud(bool force)
        {
            double target = Isl.Coins;
            if (force) { shownCoins = target; shownGems = Isl.Gems; }
            else
            {
                shownCoins += (target - shownCoins) * Mathf.Min(1f, Time.unscaledDeltaTime * 8f);
                shownGems += (Isl.Gems - shownGems) * Mathf.Min(1f, Time.unscaledDeltaTime * 6f);
            }
            if (System.Math.Abs(target - shownCoins) < 0.5) shownCoins = target;
            if (System.Math.Abs(Isl.Gems - shownGems) < 0.05) shownGems = Isl.Gems;
            coinPill.Label.text = BigNum.Fmt(System.Math.Floor(shownCoins));
            gemPill.Label.text = System.Math.Round(shownGems).ToString();
            string mt = Isl.Miners.Count + "/" + Mathf.Max(Isl.MinerCap(), Isl.Miners.Count);
            if (minerPill.Label.text != mt) { minerPill.Label.text = mt; minerSeenT = Time.unscaledTime; }
        }

        float minerSeenT = -99f;

        // ------------------------------------------------------------ meta (tarjeta colapsada: icono, 3/10, +60)
        RectTransform goalBox, goalWide;
        Text goalText, goalReward, goalCount;
        Image goalFill;
        int shownGoal = -1;
        long shownProg = -1;
        float goalOpenUntil;
        bool goalOpen;

        void BuildGoal()
        {
            goalBox = Glass(hudLayer, 170, 44, "Meta");
            Kit.Place(goalBox, 0f, 0f, 20f, 74f, 170, 44);
            var badge = Kit.Icon(goalBox, "mission", 34);
            Kit.PlaceTL((RectTransform)badge.transform, 6, 5, 34, 34);
            goalCount = Kit.LabelAt(goalBox, "", 22, Color.white, 0, true, 44, 0, 70, 44, TextAnchor.MiddleLeft, "Cuenta");
            goalReward = Kit.LabelAt(goalBox, "", 20, Kit.Yellow, 0, true, 98, 0, 64, 44, TextAnchor.MiddleRight, "Premio");
            // lo que se ve solo al abrirla: el texto de la meta y una barra fina
            goalWide = Kit.New("Abierta", goalBox);
            Kit.Stretch(goalWide);
            goalText = Kit.LabelAt(goalWide, "", 21, Color.white, 0, true, 44, 6, 400, 30);
            var bar = Kit.RoundImg(goalWide, 4, new Color(1f, 1f, 1f, 0.22f), "Barra");
            Kit.PlaceTL(bar.rectTransform, 46, 50, 200, 8);
            goalFill = Kit.RoundImg(bar.transform, 4, Kit.Green, "Relleno");
            goalFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            goalFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            goalFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            goalFill.rectTransform.offsetMin = Vector2.zero;
            goalFill.rectTransform.offsetMax = Vector2.zero;
            goalWide.gameObject.SetActive(false);
            goalBox.GetComponent<Image>().raycastTarget = true;
            goalBox.gameObject.AddComponent<Btn>().Clicked += () => { if (goalOpen) goalOpenUntil = 0f; else OpenGoal(5f); };
        }

        /// <summary>La tarjeta se abre sola un rato cuando hay una meta nueva o se completa; tocandola se abre o cierra.</summary>
        void OpenGoal(float secs) { goalOpenUntil = Time.unscaledTime + secs; }

        void RefreshGoal()
        {
            var g = Isl.CurrentGoal();
            long prog = System.Math.Min(Isl.GoalProgress(g), (long)g.Target);
            float frac = Mathf.Clamp01(prog / (float)Mathf.Max(1, g.Target));
            if (shownGoal != Isl.GoalIdx)
            {
                bool first = shownGoal < 0;
                shownGoal = Isl.GoalIdx;
                goalText.text = g.Text;
                goalReward.text = "+" + BigNum.Fmt(g.Coins);
                if (!first) { OpenGoal(4f); Tw.Pop(goalBox, 1.08f); }
            }
            if (prog != shownProg)
            {
                if (shownProg >= 0 && prog > shownProg) Tw.Pop(goalCount.rectTransform, 1.2f);
                shownProg = prog;
                goalCount.text = prog + "/" + g.Target;
            }
            bool open = Time.unscaledTime < goalOpenUntil && sheet == null;
            float wantW = open ? Mathf.Clamp(goalText.preferredWidth + 70f, 300f, Kit.CanvasSize.x - 170f) : 170f;
            float wantH = open ? 70f : 44f;
            Vector2 sz = goalBox.sizeDelta;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);
            Vector2 nsz = new Vector2(Mathf.Lerp(sz.x, wantW, k), Mathf.Lerp(sz.y, wantH, k));
            if ((nsz - sz).sqrMagnitude > 0.01f) Kit.SetSize(goalBox, nsz.x, nsz.y);
            if (open != goalOpen) { goalOpen = open; goalWide.gameObject.SetActive(open); goalCount.gameObject.SetActive(!open); goalReward.gameObject.SetActive(!open); }
            if (open)
            {
                var bar = (RectTransform)goalFill.transform.parent;
                float bw = Mathf.Max(40f, goalBox.sizeDelta.x - 200f);
                bar.sizeDelta = new Vector2(bw, 8f);
                bar.anchoredPosition = new Vector2(46f + bw * 0.5f, -54f);
                goalFill.rectTransform.sizeDelta = new Vector2(bw * frac, 0f);
                string tail = prog + "/" + g.Target + "  +" + BigNum.Fmt(g.Coins);
                var info = goalWide.Find("Info") as RectTransform;
                Text il;
                if (info == null) { il = Kit.Label(goalWide, "", 18, Kit.Yellow, 0, true, TextAnchor.MiddleRight, "Info"); info = il.rectTransform; }
                else il = info.GetComponent<Text>();
                Kit.Place(info, 1f, 0f, -146f, 40f, 136, 30);
                if (il.text != tail) il.text = tail;
            }
        }

        public void GoalDone(IGoal g)
        {
            Toast(Loc.T("Meta cumplida  +") + BigNum.Fmt(g.Coins) + (g.Gems > 0 ? "  +" + g.Gems + Loc.T(" gemas") : ""), Kit.Green, Icons.Get("mission"));
            Vector2 c = LayerPos(goalBox);
            for (int i = 0; i < 5; i++)
            {
                var star = Kit.Img(flyLayer, Icons.Get("star"), Color.white, "Estrella");
                var rt = star.rectTransform;
                rt.sizeDelta = new Vector2(44, 44);
                Vector2 to = c + new Vector2(Random.Range(-40f, 260f), Random.Range(-140f, -60f));
                Tw.To(rt, "star", 0.8f, Ease.Linear, u =>
                {
                    float e = 1f - (1f - u) * (1f - u);
                    rt.anchoredPosition = Vector2.Lerp(c, to, e);
                    rt.localScale = Vector3.one * (u < 0.2f ? u * 5f : 1.2f - u);
                    rt.localRotation = Quaternion.Euler(0, 0, u * 360f);
                }, () => Destroy(star.gameObject));
            }
            Tw.Pop(goalBox, 1.2f);
            FlyCoins(LayerPos(goalBox, 60f), 6);   // la recompensa vuela desde el cartel (18)
            if (g.Gems > 0) FlyGems(LayerPos(goalBox), g.Gems);
        }

        // ------------------------------------------------------------ botones: turbo, ampliar, ajustes
        Btn turboBtn, expandBtn;
        Text turboL, expandL, expandPrice;
        float turboAskUntil;

        void BuildButtons()
        {
            // abajo, chicos: el costo del turbo se ve recien al tocarlo (primer toque pregunta, el segundo compra)
            turboBtn = Kit.Button(hudLayer, "", Kit.Purple, 24, 150, 64, Loc.T("Turbo"));
            Kit.Place((RectTransform)turboBtn.transform, 0f, 1f, 20f, -86f, 150, 64);
            var ti = Kit.Icon(turboBtn.Content, "speed", 34);
            Kit.PlaceTL((RectTransform)ti.transform, 10, 11, 34, 34);
            turboL = turboBtn.Label;
            turboL.alignment = TextAnchor.MiddleCenter;
            Kit.Stretch(turboL.rectTransform, 44, 0, 6, 4);
            turboBtn.Clicked += () =>
            {
                if (Isl.TurboT > 0f) { Toast(Loc.T("Turbo activo: mineros al doble"), Kit.Purple, Icons.Get("speed")); return; }
                if (Time.unscaledTime > turboAskUntil) { turboAskUntil = Time.unscaledTime + 3f; Sfx.Play("pop", -10f, 1.2f); return; }
                turboAskUntil = 0f;
                if (Isl.BuyTurbo())
                {
                    Toast(Loc.T("¡Turbo! Mineros al doble por 2 minutos"), Kit.Purple, Icons.Get("speed"));
                    Sfx.Play("powerup", -2f);
                    Juice.Vibrate(40);
                    game.Save();
                }
                else { Toast(Loc.T("Te faltan gemas: las dan las metas y los barcos"), Kit.Gray, Icons.Get("gem")); Sfx.Play("error", -6f); NoMoney(turboBtn); }
            };

            // Plan Pueblo: un boton "Construir" abajo al centro reemplaza los "+" del mapa (fuera del tutorial). Abre el
            // catalogo y el edificio elegido se arrastra a cualquier lugar libre de la isla.
            buildBtn = Kit.Button(hudLayer, "", Kit.Orange, 24, 180, 76, Loc.T("Construir"));
            Kit.Place((RectTransform)buildBtn.transform, 0.5f, 1f, -90f, -98f, 180, 76);
            var bi = Kit.Icon(buildBtn.Content, "hammer", 38);
            Kit.PlaceTL((RectTransform)bi.transform, 12, 18, 38, 38);
            buildBtn.Label.text = Loc.T("Construir");
            buildBtn.Label.alignment = TextAnchor.MiddleCenter;
            Kit.Stretch(buildBtn.Label.rectTransform, 46, 0, 8, 4);
            buildBtn.Clicked += () =>
            {
                Plot free = null;
                foreach (var p in Isl.Plots) if (p.Ring <= Isl.Expand && Isl.Offered(p)) { free = p; break; }
                if (free == null) { Toast(Loc.T("No queda lugar: ampliá la isla"), Kit.Gray); NoMoney(buildBtn); return; }
                OpenCatalog(free);
            };
            buildBtn.gameObject.SetActive(false);

            expandBtn = Kit.Button(hudLayer, "", Kit.Blue, 22, 190, 64, "Ampliar");
            Kit.Place((RectTransform)expandBtn.transform, 1f, 1f, -210f, -86f, 190, 64);
            expandL = expandBtn.Label;
            expandL.alignment = TextAnchor.MiddleCenter;
            Kit.Stretch(expandL.rectTransform, 0, 4, 0, 24);
            expandPrice = Kit.Label(expandBtn.Content, "", 17, new Color(1f, 1f, 1f, 0.85f), 0, true, TextAnchor.MiddleCenter, "Precio");
            Kit.Stretch(expandPrice.rectTransform, 0, 34, 0, 8);
            expandBtn.Clicked += () =>
            {
                if (Isl.DoExpand()) game.Save();
                else { Toast(Loc.T("Te faltan monedas para ampliar la isla"), Kit.Gray, Icons.Get("coin")); NoMoney(expandBtn); }
            };
        }

        Btn buildBtn;

        void RefreshButtons()
        {
            if (buildBtn != null)
            {
                bool on = Isl.TutDone && sheet == null && !WorldQuiet;
                if (buildBtn.gameObject.activeSelf != on) { buildBtn.gameObject.SetActive(on); if (on) Tw.Pop(buildBtn.transform, 1.15f); }
            }
            // el turbo aparece cuando ya hay gemas o se avanzo un poco (al principio no distrae)
            bool turboVis = Isl.Gems > 0 || Isl.TurboT > 0f || Isl.GoalIdx >= 2;
            if (turboBtn.gameObject.activeSelf != turboVis) { turboBtn.gameObject.SetActive(turboVis); if (turboVis) Tw.Pop(turboBtn.transform, 1.3f); }
            string tt;
            if (Isl.TurboT > 0f)
            {
                int s = Mathf.CeilToInt(Isl.TurboT);
                tt = "x2 " + s / 60 + ":" + (s % 60).ToString("00");
                float k = 1f + Mathf.Sin(Time.time * 8f) * 0.03f;
                if (!turboBtn.IsPressed && Time.unscaledTime > turboBtn.QuietUntil) turboBtn.transform.localScale = Vector3.one * k;
            }
            else tt = Time.unscaledTime < turboAskUntil ? Island.TurboGems + Loc.T(" gemas") : Loc.T("Turbo");
            if (turboL.text != tt) turboL.text = tt;
            turboL.fontSize = Time.unscaledTime < turboAskUntil ? 20 : 22;
            bool canMore = Isl.Expand < Island.ExpandCost.Length;
            bool show = canMore && Isl.TotalEarned >= Isl.ExpandPrice() * 0.35;
            if (expandBtn.gameObject.activeSelf != show) { expandBtn.gameObject.SetActive(show); if (show) Tw.Pop(expandBtn.transform, 1.3f); }
            if (show)
            {
                expandL.text = Loc.T("Expandir isla");
                expandL.fontSize = 22;
                expandPrice.text = BigNum.Fmt(Isl.ExpandPrice());
                Color m = Isl.CanExpand() ? Color.white : new Color(0.8f, 0.8f, 0.85f);
                if (expandBtn.Modulate != m) { expandBtn.Modulate = m; expandBtn.Restyle(); }
            }
        }

        // ------------------------------------------------------------ avisos (toasts)
        struct ToastMsg { public string Text; public Color Col; public Sprite Icon; }
        readonly Queue<ToastMsg> toasts = new Queue<ToastMsg>();
        float toastBusy;
        string lastToast;
        RectTransform curToast;
        float lastToastT;

        float lastTouchT = -99f;

        /// <summary>
        /// Aviso corto (2 s) en una capsula de vidrio arriba. Desde 0.9.2 solo responde a lo que el jugador acaba de tocar
        /// (por ejemplo "te faltan gemas"); lo que pasa solo en la isla ya no avisa con texto: se ve en el mundo.
        /// `force`: avisos que siguen a una accion del jugador aunque lleguen tarde (resultado de un anuncio).
        /// </summary>
        public void Toast(string text, Color col, Sprite icon = null, bool force = false)
        {
            if (!force && Time.unscaledTime - lastTouchT > 0.8f) return;
            if (toasts.Count > 2) return;
            if (text == lastToast && Time.unscaledTime - lastToastT < 4f) return;   // el mismo aviso seguido no se repite
            lastToast = text; lastToastT = Time.unscaledTime;
            toasts.Enqueue(new ToastMsg { Text = text, Col = col, Icon = icon });
        }

        void UpdateToasts()
        {
            toastBusy -= Time.unscaledDeltaTime;
            if (toastBusy > 0f || toasts.Count == 0) return;
            var t = toasts.Dequeue();
            toastBusy = toasts.Count > 0 ? 1.2f : 2f;
            // el aviso anterior se va enseguida: nunca hay dos encimados
            if (curToast != null) { var old = curToast; Tw.Kill(old); var og = Kit.Group(old.gameObject); Tw.Alpha(og, 0f, 0.15f, 0f, () => { if (old != null) Destroy(old.gameObject); }); }
            var box = Glass(toastLayer, 200, 48, "Aviso");
            curToast = box;
            var dot = t.Icon != null ? Kit.Img(box, t.Icon, Color.white, "Icono") : Kit.RoundImg(box, 6, t.Col, "Punto");
            dot.preserveAspect = true;
            if (t.Icon != null) Kit.PlaceTL(dot.rectTransform, 8, 6, 36, 36);
            else Kit.PlaceTL(dot.rectTransform, 18, 18, 12, 12);
            float x0 = t.Icon != null ? 50f : 38f;
            var l = Kit.Label(box, t.Text, 22, Color.white, 0, true, TextAnchor.MiddleLeft);
            float maxW = Kit.CanvasSize.x - 60f;
            float w = Mathf.Clamp(l.preferredWidth + x0 + 20f, 180f, maxW);
            float h = 48f;
            if (l.preferredWidth + x0 + 20f > maxW) { Kit.Wrap(l); l.fontSize = 19; h = 66f; }
            Kit.Stretch(l.rectTransform, x0, 0, 16, 0);
            Kit.Place(box, 0.5f, 0f, -w * 0.5f, 132f, w, h);
            Vector2 at = box.anchoredPosition;
            var grp = Kit.Group(box.gameObject);
            grp.blocksRaycasts = false;
            Tw.MoveFrom(box, at + new Vector2(0, 30f), at, 0.25f, Ease.OutBack);
            grp.alpha = 0f;
            Tw.Alpha(grp, 1f, 0.15f);
            float life = toastBusy + 0.3f;
            Tw.After(box, "fin", life, () =>
            {
                Tw.Alpha(grp, 0f, 0.25f, 0f, () => { if (curToast == box) curToast = null; Destroy(box.gameObject); });
                Tw.Move(box, at + new Vector2(0, 20f), 0.25f, Ease.Linear);
            });
        }

        // ------------------------------------------------------------ marcadores sobre el mundo
        readonly Dictionary<int, Btn> plusMarks = new Dictionary<int, Btn>();
        readonly Dictionary<int, Image> upMarks = new Dictionary<int, Image>();

        void Update()
        {
            if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0) || Input.touchCount > 0) lastTouchT = Time.unscaledTime;
            UpdateIntro();
            UpdateSheetChrome();
            RefreshHud(false);
            RefreshGoal();
            RefreshButtons();
            UpdateCityHud();
            UpdateToasts();
            PickArrows();
            foreach (var p in Isl.Plots)
            {
                bool inIsland = p.Ring <= Isl.Expand;
                Vector2 c = ToCanvas(game.PlotWorld(p) + Vector3.up * (p.Building >= 0 ? game.PlotHeight(p) + 0.9f : 0.9f));
                // "+" chico (vidrio claro) solo en las parcelas donde ya alcanza para construir algo
                Btn plus;
                if (!plusMarks.TryGetValue(p.Id, out plus))
                {
                    plus = Kit.HitArea(worldLayer, 64, 64, "Mas");
                    var disc = Kit.RoundImg(plus.transform, 22, new Color(1f, 1f, 1f, 0.92f), "Disco");
                    Kit.Place(disc.rectTransform, 0.5f, 0.5f, -22f, -22f, 44, 44);
                    var ic = Kit.KIcon(plus.transform, "plus", 24);
                    ic.color = Kit.GreenD;
                    Kit.Place(ic.rectTransform, 0.5f, 0.5f, -12f, -12f, 24, 24);
                    var pp = p;
                    plus.Clicked += () => OpenPlot(pp);
                    plusMarks[p.Id] = plus;
                }
                bool free = inIsland && plusPlots.Contains(p.Id);
                if (plus.gameObject.activeSelf != free) { plus.gameObject.SetActive(free); if (free) Tw.Pop(plus.transform, 1.3f); }
                if (free)
                {
                    float bob = Mathf.Sin(Time.time * 2.2f + p.Id) * 3f;
                    ((RectTransform)plus.transform).anchoredPosition = new Vector2(c.x, -c.y + bob);
                }
                // flecha de mejora: solo un momento cuando recien alcanza, o si el jugador parece perdido
                Image up;
                if (!upMarks.TryGetValue(p.Id, out up))
                {
                    up = Kit.Img(worldLayer, ArrowUp(), Color.white, "Mejora");
                    up.rectTransform.sizeDelta = new Vector2(36, 39);
                    upMarks[p.Id] = up;
                }
                bool canUp = inIsland && p.Building >= 0 && p.BuildT < 0f && p.Work <= 0 && arrowPlots.Contains(p.Id) && ArrowWanted(p.Id) && sheet == null;
                if (up.gameObject.activeSelf != canUp) up.gameObject.SetActive(canUp);
                if (canUp) up.rectTransform.anchoredPosition = new Vector2(c.x, -c.y + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 8f);
            }
            UpdateCityMarks();
            UpdateCitySheet();
            UpdateGiantMark();
            UpdateShipBubble();
            UpdateTutorial();
            UpdateRewards();
            UpdateFrenzyBar();
            RefreshAlbumButton();
            UpdateCritterTag();
            UpdateDailyButton();
            UpdateBoardTag();
            UpdateProgressUi();
            UpdateOnboarding();
            UpdateDetailsUi();
            UpdateShopUi();
            UpdateClean();
            if (sheet != null) RefreshSheet();
        }

        readonly HashSet<int> arrowPlots = new HashSet<int>();
        float arrowT;

        /// <summary>Flecha de mejora solo sobre las 2 mejoras mas baratas que se pueden pagar (con muchos edificios, todas juntas eran ruido).</summary>
        void PickArrows()
        {
            arrowT -= Time.unscaledDeltaTime;
            if (arrowT > 0f) return;
            arrowT = 0.5f;
            arrowPlots.Clear();
            Plot a = null, b = null; double ca = double.MaxValue, cb = double.MaxValue;
            foreach (var p in Isl.Plots)
            {
                if (p.Building < 0 || !Isl.CanUpgrade(p)) continue;
                double c = Isl.UpgradeCost(p);
                if (c < ca) { b = a; cb = ca; a = p; ca = c; }
                else if (c < cb) { b = p; cb = c; }
            }
            if (a != null) arrowPlots.Add(a.Id);
            if (b != null) arrowPlots.Add(b.Id);
            ArrowAnnounce();
        }

        static Sprite arrow;

        /// <summary>Flecha verde de "mejora disponible" (contorno oscuro, dibujada a 4x para que sea nitida).</summary>
        static Sprite ArrowUp()
        {
            if (arrow != null) return arrow;
            var p = new Painter(192, 208, 4f, 3);
            Vector2[] pts = { new Vector2(24, 4), new Vector2(46, 26), new Vector2(34, 26), new Vector2(34, 48), new Vector2(14, 48), new Vector2(14, 26), new Vector2(2, 26) };
            p.Stroke(pts, 6f, Painter.Out, true, true);
            p.Poly(pts, Kit.Green);
            Vector2[] hi = { new Vector2(24, 10), new Vector2(38, 23), new Vector2(29, 23), new Vector2(29, 30), new Vector2(19, 30), new Vector2(19, 23), new Vector2(10, 23) };
            p.Poly(hi, new Color(0.75f, 1f, 0.6f, 0.9f));
            var t = p.ToTexture();
            arrow = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 400f);
            return arrow;
        }

        static Sprite hand;

        /// <summary>Mano que señala (dedo indice hacia arriba), dibujada a 4x con contorno.</summary>
        static Sprite Hand()
        {
            if (hand != null) return hand;
            var p = new Painter(224, 256, 4f, 3);
            Color skin = Color.white, sh = Icons.H("d9dde6");
            // palma y dedos cerrados
            var palm = Painter.RoundRectPts(new Rect(12, 26, 34, 28), 9f);
            p.OPoly(palm, skin, 3f);
            // dedo indice
            var finger = Painter.RoundRectPts(new Rect(14, 3, 11, 30), 5.5f);
            p.OPoly(finger, skin, 3f);
            // pulgar
            var thumb = Painter.RoundRectPts(new Rect(4, 30, 14, 10), 5f);
            p.OPoly(thumb, skin, 3f);
            p.Poly(Painter.RoundRectPts(new Rect(14, 26, 30, 26), 8f), skin);
            p.Line(new Vector2(28, 33), new Vector2(28, 42), 2f, sh);
            p.Line(new Vector2(36, 33), new Vector2(36, 42), 2f, sh);
            p.Poly(Painter.RoundRectPts(new Rect(16, 5, 7, 7), 3f), new Color(1f, 0.85f, 0.85f));
            var t = p.ToTexture();
            hand = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 400f);
            return hand;
        }

        bool AnyBuildable(Plot p)
        {
            foreach (var d in Island.Defs) if (Isl.CanBuild(d.Kind, p)) return true;
            return false;
        }

        // ------------------------------------------------------------ mineros
        public static string MinerName(Miner m) { return Island.Char(m).Name; }

        static string StateText(Miner m)
        {
            switch (m.State)
            {
                case MState.Idle: return Loc.T("Buscando una veta");
                case MState.ToOre: return Loc.T("Va a picar");
                case MState.Mining: return Loc.T("¡Picando!");
                case MState.ToDepot: return Loc.T("Lleva mineral al depósito");
                case MState.ToCanteen: return Loc.T("Va a comer");
                case MState.Eating: return Loc.T("Comiendo");
                case MState.ToShowers: return Loc.T("Va a ducharse");
                case MState.Showering: return Loc.T("En la ducha");
                case MState.Resting: return Loc.T("Durmiendo una siesta");
                case MState.ToBuild: return Loc.T("Corre a ayudar en la obra");
                case MState.Building: return Loc.T("¡Martillando!");
                case MState.ToDig: return Loc.T("Va a la X del tesoro");
                case MState.Digging: return Loc.T("¡Cavando!");
                default: return Loc.T("Llegando a la isla");
            }
        }

        /// <summary>Tarjeta del minero tocado: nombre, que esta haciendo, energia, limpieza y rendimiento.</summary>
        public void ShowMiner(Miner m)
        {
            var c = Island.Char(m);
            Color rc = IslandGame.HelmetOf(m);
            var fr = OpenSheet(470);
            var bust = Kit.Img(fr, Icons.MinerBust(rc), Color.white, "Cara");
            bust.preserveAspect = true;
            Kit.PlaceTL(bust.rectTransform, 30, 24, 120, 130);
            SpecBadge(fr, m.Char, 112, 112, 50, false);
            Kit.LabelAt(fr, c.Name, 42, Kit.Brown, 0, true, 170, 18, 300, 52);
            var chip = Kit.OutBox(fr, 14, 3, 4, rc, Kit.Out, "Rareza");
            Kit.PlaceTL(chip, 470, 26, 180, 40);
            var cl = Kit.Label(chip, m.Golden ? Loc.T("Dorado") : Island.RarityName[c.Rarity], 22, Color.white, 5, true, TextAnchor.MiddleCenter);
            Kit.Stretch(cl.rectTransform, 0, 0, 0, 4);
            var desc = Kit.LabelAt(fr, c.Desc, 22, Kit.Brown, 0, false, 170, 70, 480, 56);
            Kit.Wrap(desc);
            var st = Kit.LabelAt(fr, "", 24, Kit.OrangeD, 0, true, 170, 126, 480, 34);
            var perf = Kit.LabelAt(fr, "", 22, Kit.GreenD, 0, true, 30, 164, 620, 32);
            var en = Bar(fr, Loc.T("Energía"), 30, 206, Kit.Orange);
            var clean = Bar(fr, "Limpieza", 30, 260, Kit.Blue);
            var xp = Bar(fr, Loc.T("Nivel ") + m.Level, 30, 314, Kit.Yellow);
            var lvLabel = fr.GetChild(fr.childCount - 2).GetComponent<Text>();
            var friend = Kit.LabelAt(fr, "", 22, Icons.H("e0577a"), 0, true, 30, 370, 620, 34);
            sheetRefresh.Add(() =>
            {
                st.text = StateText(m);
                float pf = Isl.Perf(m) * Isl.HitMult(m);
                perf.text = Loc.T("Rinde ") + Mathf.RoundToInt(pf * 100f) + "%" + (m.Fresh > 0f ? Loc.T("  ¡Fresco!") : "") + (Isl.TurboT > 0f ? Loc.T("  Turbo") : "");
                en(m.Energy / 100f);
                clean(m.Clean / 100f);
                if (lvLabel != null) lvLabel.text = Loc.T("Nivel ") + m.Level;
                xp(m.Level >= Island.MaxMinerLevel ? 1f : m.Xp / (float)Island.XpFor(m.Level));
                string fn = null;
                foreach (var o in Isl.Miners) if (o.Id == m.Friend) fn = Island.Char(o).Name;
                friend.text = fn != null ? Loc.T("Amigo de ") + fn + Loc.T(" (+10 % juntos)") : Loc.T("Todavía sin amigos: trabajando juntos se hacen amigos");
                friend.color = fn != null ? Icons.H("e0577a") : new Color(0.55f, 0.47f, 0.4f);
            });
        }

        System.Action<float> Bar(Transform parent, string label, float x, float y, Color col)
        {
            Kit.LabelAt(parent, label, 24, Kit.Brown, 0, true, x, y + 4, 150, 34);
            var bar = Kit.OutBox(parent, 12, 3, 0, Icons.H("d8c6a2"), Kit.Out, "Barra");
            Kit.PlaceTL(bar, x + 150, y, 460, 40);
            var fill = Kit.RoundImg(bar, 10, col, "Relleno");
            var rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(3, 3); rt.offsetMax = new Vector2(3, -3);
            return f =>
            {
                f = Mathf.Clamp01(f);
                fill.gameObject.SetActive(f > 0.02f);
                rt.sizeDelta = new Vector2(Mathf.Max(24f, 454f * f), rt.sizeDelta.y);
            };
        }

        // ------------------------------------------------------------ veta gigante y barco
        Ore giant;
        int giantMarkFor = -1;
        RectTransform giantMark;

        public void MarkGiant(Ore o)
        {
            giant = o;
            Sfx.Play("bell", -3f);
        }

        void UpdateGiantMark()
        {
            bool show = giant != null && !giant.Dead && Isl.Giant == giant;
            if (!show)
            {
                if (giantMark != null) { Destroy(giantMark.gameObject); giantMark = null; }
                giant = Isl.Giant;
                return;
            }
            // otra veta (la legendaria del hallazgo puede llegar justo despues de romper una comun): cartel nuevo
            if (giantMark != null && giantMarkFor != giant.Id) { Destroy(giantMark.gameObject); giantMark = null; }
            if (giantMark == null)
            {
                giantMarkFor = giant.Id;
                giantMark = Kit.MakeTag(worldLayer, giant.Boss ? Loc.T("¡GOLEM DE ROCA!") : giant.Legendary ? Loc.T("¡LEGENDARIO! x5") : Loc.T("¡VETA GIGANTE!"),
                    giant.Boss ? new Color(0.55f, 0.5f, 0.5f) : giant.Legendary ? new Color(0.25f, 0.6f, 0.95f) : Kit.Orange, 24);
                Tw.Pop(giantMark, 1.5f);
            }
            Vector2 c = ToCanvas(new Vector3(giant.X, 4.6f, giant.Z));
            Vector2 cs = Kit.CanvasSize;
            // si sale de la pantalla, se pega al borde para que se sepa hacia donde ir
            float hx = cs.x * 0.5f - 140f, hy = cs.y * 0.5f - 230f;
            Vector2 p = new Vector2(Mathf.Clamp(c.x, -hx, hx), Mathf.Clamp(-c.y, -hy + 120f, hy));
            float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.05f;
            giantMark.anchoredPosition = p + new Vector2(0f, Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 8f);
            giantMark.localScale = Vector3.one * pulse;
        }

        RectTransform shipBubble;
        Text shipL;

        void UpdateShipBubble()
        {
            var s = Isl.CurShip;
            bool show = s != null && !s.Done && game.Ambient != null && game.Ambient.BoatDocked;
            if (!show) { if (shipBubble != null && shipBubble.gameObject.activeSelf) shipBubble.gameObject.SetActive(false); return; }
            if (shipBubble == null)
            {
                shipBubble = Glass(worldLayer, 190, 62, "Pedido");
                shipL = Kit.Label(shipBubble, "", 19, Color.white, 0, true, TextAnchor.MiddleCenter);
                Kit.Stretch(shipL.rectTransform, 8, 2, 8, 2);
            }
            if (!shipBubble.gameObject.activeSelf) { shipBubble.gameObject.SetActive(true); Tw.Pop(shipBubble, 1.3f); }
            int left = Mathf.CeilToInt(s.Left);
            shipL.text = Island.Ores[s.Kind].Name + "  " + s.Delivered + "/" + s.Count + "\n" + left / 60 + ":" + (left % 60).ToString("00");
            Vector2 c = ToCanvas(game.Ambient.BoatPos + Vector3.up * 4.4f);
            Vector2 cs = Kit.CanvasSize;
            float hx = cs.x * 0.5f - 135f, hy = cs.y * 0.5f - 240f;
            shipBubble.anchoredPosition = new Vector2(Mathf.Clamp(c.x, -hx, hx), Mathf.Clamp(-c.y, -hy + 130f, hy));
        }

        // ------------------------------------------------------------ tutorial (mano que señala)
        RectTransform handRT;
        Vector3 tutFocusAt = new Vector3(9999f, 0f, 0f);
        float tutFocusT;

        void UpdateTutorial()
        {
            Vector3? target = null;
            string hint = "";
            TutorialTarget(ref target, ref hint);
            if (target == null) { if (handRT != null) handRT.gameObject.SetActive(false); return; }
            if (handRT == null)
            {
                handRT = Kit.New("Tutorial", flyLayer);
                handRT.sizeDelta = new Vector2(10, 10);
                var ring = Kit.Img(handRT, Icons.Ring(0.12f), new Color(1f, 1f, 1f, 0.9f), "Anillo");
                ring.rectTransform.sizeDelta = new Vector2(90, 90);
                var h = Kit.Img(handRT, Hand(), Color.white, "Mano");
                h.rectTransform.sizeDelta = new Vector2(56, 64);
                var l = Kit.Label(handRT, "", 26, Color.white, 6, true, TextAnchor.MiddleCenter, "Pista");
                l.rectTransform.sizeDelta = new Vector2(420, 40);
                l.rectTransform.anchoredPosition = new Vector2(0, -112f);
            }
            if (!handRT.gameObject.activeSelf) handRT.gameObject.SetActive(true);
            Vector2 c = ToCanvas(target.Value);
            // si lo que hay que tocar quedo fuera de la pantalla, la camara va hasta ahi (sin que el jugador lo busque)
            Vector2 csz = Kit.CanvasSize;
            // una sola vez por objetivo (antes cada 2.5 s, peleando con el arrastre del jugador)
            if ((Mathf.Abs(c.x) > csz.x * 0.4f || c.y < -csz.y * 0.36f || c.y > csz.y * 0.32f) && Time.unscaledTime > tutFocusT
                && (target.Value - tutFocusAt).sqrMagnitude > 9f)
            {
                tutFocusT = Time.unscaledTime + 2.5f;
                tutFocusAt = target.Value;
                game.FocusOn(target.Value, true);
            }
            handRT.anchoredPosition = new Vector2(c.x, -c.y);
            // la pista nunca se corta en el borde de la pantalla
            float halfW = Kit.CanvasSize.x * 0.5f, lw = 400f;
            var labT = (RectTransform)handRT.GetChild(2);
            float lx = Mathf.Clamp(c.x, -halfW + lw * 0.5f + 12f, halfW - lw * 0.5f - 12f) - c.x;
            labT.anchoredPosition = new Vector2(lx, -112f);
            float k = Mathf.Repeat(Time.time * 1.2f, 1f);
            var ringT = (RectTransform)handRT.GetChild(0);
            ringT.localScale = Vector3.one * (0.4f + k * 0.9f);
            ringT.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f - k);
            var handT = (RectTransform)handRT.GetChild(1);
            float tap = Mathf.Abs(Mathf.Sin(Time.time * 3.8f));
            handT.anchoredPosition = new Vector2(16f, -34f - tap * 18f);
            var lab = handRT.GetChild(2).GetComponent<Text>();
            if (lab.text != hint) lab.text = hint;
        }

        // ------------------------------------------------------------ conversion mundo -> lienzo
        Vector2 ToCanvas(Vector3 world)
        {
            Vector3 sp = game.Cam.WorldToScreenPoint(world);
            Vector2 cs = Kit.CanvasSize;
            // pixeles de la camara (no Screen: en capturas la camara dibuja a una textura de otro tamaño)
            float pw = Mathf.Max(game.Cam.pixelWidth, 1), ph = Mathf.Max(game.Cam.pixelHeight, 1);
            var r = new Vector2(sp.x / pw * cs.x, (1f - sp.y / ph) * cs.y);
            // las capas estiradas tienen el origen en el centro: anchoredPosition = (x - w/2, -(y - h/2))
            return new Vector2(r.x - cs.x * 0.5f, r.y - cs.y * 0.5f);
        }

        // ------------------------------------------------------------ efectos de UI
        /// <summary>Texto flotante que sube y se desvanece.</summary>
        public void Popup(Vector3 world, string text, Color col) { Popup(world, text, col, 30); }

        public void Popup(Vector3 world, string text, Color col, int size)
        {
            Vector2 c = ToCanvas(world);
            // del mundo: debajo de las hojas (el "Level 4" de la casa quedaba encima de la hoja del barco)
            var t = Kit.Label(worldLayer, text, size, col, 7, true, TextAnchor.MiddleCenter, "Popup");
            t.rectTransform.sizeDelta = new Vector2(360, size + 14);
            var rt = t.rectTransform;
            Vector2 from = new Vector2(c.x, -c.y);
            Tw.Scale(rt, Vector3.one * 0.4f, Vector3.one, 0.25f, Ease.OutBack);
            Tw.To(rt, "pop", 1.3f, Ease.Linear, u =>
            {
                rt.anchoredPosition = from + new Vector2(0, 70f * (1f - (1f - u) * (1f - u)));
                t.color = new Color(col.r, col.g, col.b, Mathf.Clamp01((1f - u) * 3f));
            }, () => Destroy(t.gameObject));
        }

        /// <summary>Monedas que salen del mundo y vuelan en arco al contador.</summary>
        public void CoinsFrom(Vector3 world, double amount)
        {
            Vector2 c = ToCanvas(world);
            Popup(world, "+" + BigNum.Fmt(amount), new Color(1f, 0.87f, 0.3f));
            float size; int n;
            var spr = CoinSprite(amount, out size, out n);
            if (size > 41f) Sfx.Play("coins_pour", -8f);
            FlyCoins(new Vector2(c.x, -c.y), n, spr, size);
            CoinGain(amount);
        }

        void FlyCoins(Vector2 from0, int n) { FlyCoins(from0, n, Icons.Get("coin"), 40f); }

        void FlyCoins(Vector2 from0, int n, Sprite sprite, float size)
        {
            bool heavy = size > 41f;
            // el pill esta anclado arriba-centro dentro de hudLayer; llevarlo a coordenadas del centro de la capa
            Vector2 target = LayerPos(coinPill.Root, -coinPill.Root.rect.width * 0.5f + 22f);
            for (int i = 0; i < n; i++)
            {
                var img = UiPool.Get(flyLayer, sprite, Color.white, "Moneda");
                img.preserveAspect = true;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(size, size);
                // estela: dos copias que siguen a la moneda con retraso mientras vuela al contador
                var ghosts = new Image[2];
                for (int gI = 0; gI < 2; gI++)
                {
                    ghosts[gI] = UiPool.Get(flyLayer, sprite, new Color(1f, 0.85f, 0.35f, 0f), "Estela");
                    ghosts[gI].preserveAspect = true;
                    ghosts[gI].rectTransform.sizeDelta = new Vector2(size, size) * (0.8f - gI * 0.15f);
                    ghosts[gI].transform.SetAsFirstSibling();
                }
                // salta, rebota dos veces en el "piso" y despues vuela al contador
                Vector2 from = from0 + Random.insideUnitCircle * 14f;
                float vx = Random.Range(-170f, 170f), vy = Random.Range(380f, 560f) * (heavy ? 0.75f : 1f);
                float floorY = from.y - Random.Range(20f, 60f);
                const float bounceT = 0.62f, flyT = 0.55f;
                float delay = i * 0.05f;
                float total = delay + bounceT + flyT;
                int idx = i;
                rt.anchoredPosition = from;
                rt.localScale = Vector3.zero;
                Vector2 land = from;
                Tw.To(rt, "fly", total, Ease.Linear, u =>
                {
                    float t = u * total - delay;
                    if (t < 0f) return;
                    if (t < bounceT)
                    {
                        // fisica simple con dos rebotes
                        float g = 2600f, x = from.x + vx * t, y, tt = t;
                        float v0 = vy, y0 = from.y;
                        for (int b = 0; b < 3; b++)
                        {
                            float tHit = (v0 + Mathf.Sqrt(v0 * v0 + 2f * g * Mathf.Max(0f, y0 - floorY))) / g;
                            if (tt < tHit || b == 2) { y = y0 + v0 * tt - 0.5f * g * tt * tt; y = Mathf.Max(y, floorY); rt.anchoredPosition = new Vector2(x, y); break; }
                            tt -= tHit; y0 = floorY; v0 = (g * tHit - v0) * 0.42f;
                        }
                        rt.localScale = Vector3.one * Mathf.Min(1f, t / 0.08f);
                        rt.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 20f) * 60f, 0);
                        land = rt.anchoredPosition;
                        return;
                    }
                    float k = Mathf.Clamp01((t - bounceT) / flyT);
                    float e = k * k * (3f - 2f * k);
                    Vector2 mid = new Vector2(land.x, Mathf.Max(land.y, target.y) + 120f);
                    rt.anchoredPosition = Vector2.Lerp(Vector2.Lerp(land, mid, e), Vector2.Lerp(mid, target, e), e);
                    rt.localScale = Vector3.one * (1f - 0.35f * k);
                    // la moneda gira (se ve el canto) mientras vuela; las bolsas y lingotes solo se balancean
                    rt.localRotation = heavy ? Quaternion.Euler(0, 0, Mathf.Sin(k * 12f) * 10f) : Quaternion.Euler(0, k * 900f, 0);
                    for (int gI = 0; gI < 2; gI++)
                    {
                        var gr = ghosts[gI].rectTransform;
                        gr.anchoredPosition = Vector2.Lerp(gr.anchoredPosition, rt.anchoredPosition, gI == 0 ? 0.45f : 0.25f);
                        gr.localScale = rt.localScale;
                        ghosts[gI].color = new Color(1f, 0.85f, 0.35f, (gI == 0 ? 0.45f : 0.25f) * Mathf.Sin(k * Mathf.PI));
                    }
                }, () =>
                {
                    UiPool.Release(img);
                    foreach (var gh in ghosts) UiPool.Release(gh);
                    Squash(coinPill.Root);   // la capsula "traga" la moneda
                    if (idx % 2 == 0) Mineros.Fx.Haptics.Selection();
                    Sfx.Play("coin", -12f, 1f + idx * 0.07f);
                    CoinGlow();
                });
                foreach (var gh in ghosts) gh.rectTransform.anchoredPosition = from;
            }
        }

        // ------------------------------------------------------------ hojas (panel inferior con fondo oscuro)
        RectTransform sheet;
        bool sheetLocked;
        readonly List<System.Action> sheetRefresh = new List<System.Action>();

        /// <summary>Abre un panel abajo (alto h) que se cierra tocando afuera. Devuelve el marco.</summary>
        /// <param name="frost">vidrio esmerilado (foto fija desenfocada de la isla). Sin el, el mundo sigue vivo detras
        /// (las hojas de edificios: se ve la obra y la evolucion; auditoria final)</param>
        Transform OpenSheet(float h, bool center = false, float w = 680f, float dimA = 0.4f, bool frost = true)
        {
            CloseSheet();
            sheetRefresh.Clear();
            sheet = Kit.New("Hoja", sheetLayer);
            Kit.Stretch(sheet);
            if (frost) AddFrost(sheet);
            var dim = Kit.Tint(sheet, new Color(0, 0, 0, dimA * 0.75f), "Dim");
            Kit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var close = dim.gameObject.AddComponent<Btn>();
            close.Juice = false;
            close.PlaySound = false;
            close.Clicked += () => { if (!sheetLocked) CloseSheet(); };
            // panel con profundidad (cara crema, labio, sombra grande) en vez de la madera de Kenney
            var frame = Kit.Img(sheet, ButtonArt.Box(Kit.Cream, 30f, 8f, 0.3f), Color.white, "Marco", true);
            frame.type = Image.Type.Sliced;
            var fr = frame.rectTransform;
            Cascade(fr);
            float y0 = center ? 0.5f : 0f;
            fr.anchorMin = new Vector2(0.5f, y0); fr.anchorMax = new Vector2(0.5f, y0);
            fr.pivot = new Vector2(0.5f, center ? 0.5f : 0f);
            fr.sizeDelta = new Vector2(w, h);
            if (center)
            {
                fr.anchoredPosition = Vector2.zero;
                Tw.Scale(fr, Vector3.one * 0.6f, Vector3.one, 0.3f, Ease.OutBack);
            }
            else
            {
                fr.anchoredPosition = new Vector2(0, 20);
                Tw.MoveFrom(fr, new Vector2(0, -h), new Vector2(0, 20), 0.3f, Ease.OutBack);
            }
            SheetChrome(fr, center);
            Sfx.Play("open", -6f);
            return frame.transform;
        }

        /// <summary>Fondo de vidrio esmerilado: la isla desenfocada detras de la hoja (se funde al abrir).</summary>
        void AddFrost(RectTransform parent)
        {
            if (game == null || game.Cam == null) return;
            var tex = Frost.Capture(game.Cam);
            if (tex == null) return;
            var raw = Kit.New("Esmerilado", parent).gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            Kit.Stretch(raw.rectTransform);
            raw.color = new Color(1f, 1f, 1f, 0f);
            Tw.To(raw, "funde", 0.18f, Ease.OutQuad, u => raw.color = new Color(0.93f, 0.93f, 0.95f, u));
        }

        /// <summary>El contenido de la hoja aparece en cascada (30 ms por elemento) apenas se termina de armar.</summary>
        void Cascade(RectTransform frame)
        {
            Tw.After(frame, "cascada", 0.001f, () =>
            {
                if (frame == null) return;
                int n = frame.childCount;
                for (int i = 0; i < n; i++)
                {
                    var ch = frame.GetChild(i);
                    var g = ch.GetComponent<CanvasGroup>();
                    if (g == null) g = ch.gameObject.AddComponent<CanvasGroup>();
                    g.alpha = 0f;
                    Tw.Alpha(g, 1f, 0.2f, 0.05f + Mineros.UI.Motion.Delay(i));
                }
            });
        }

        public void OpenPlot(Plot p)
        {
            if (p == null) return;
            if (p.Building == (int)BKind.Barracks && p.Level >= 1) { game.EnterComplex(); return; }   // el Cuartel se abre en el Modo Cuartel
            if (p.Building >= 0) { OpenBuilding(p); return; }
            OpenCatalog(p);
        }

        /// <summary>Catalogo viejo de 0.8 (queda para referencia; la ciudad usa OpenCatalog).</summary>
        void OpenPlotLegacy(Plot p)
        {
            var kinds = new List<BDef>();
            foreach (var d in Island.Defs)
            {
                if (d.Kind == BKind.Depot) continue;
                if (d.Kind != BKind.House && Isl.Find(d.Kind) != null) continue;   // ya construido: no ocupar lugar
                if (d.Kind == BKind.House && Isl.CountOf(BKind.House) >= Island.MaxHouses) continue;
                kinds.Add(d);
            }
            // lo que se puede pagar primero, despues por precio
            kinds.Sort((a, b) =>
            {
                bool ca = Isl.CanBuild(a.Kind, p), cb = Isl.CanBuild(b.Kind, p);
                if (ca != cb) return ca ? -1 : 1;
                return Isl.BuildCost(a.Kind).CompareTo(Isl.BuildCost(b.Kind));
            });
            int rows = Mathf.Min(kinds.Count, 6);
            var fr = OpenSheet(100 + Mathf.Max(rows, 1) * 150);
            Kit.LabelAt(fr, Loc.T("Construir"), 40, Kit.Brown, 0, true, 0, 18, 680, 50, TextAnchor.MiddleCenter);
            if (kinds.Count == 0) Kit.LabelAt(fr, Loc.T("¡Ya construiste todo! Mejorá tus edificios."), 26, Kit.Brown, 0, true, 0, 110, 680, 40, TextAnchor.MiddleCenter);
            for (int i = 0; i < rows; i++) MakeBuildRow(fr, kinds[i], p, 84 + i * 150);
        }

        void MakeBuildRow(Transform parent, BDef d, Plot p, float y)
        {
            var card = Kit.Box9(parent, "card", new Vector4(12, 12, 12, 16), Color.white, "Fila");
            Kit.PlaceTL(card.rectTransform, 22, y, 636, 140);
            var icon = Kit.Img(card.transform, IslandStage.I.BuildingIcon(d.Kind, 1), Color.white, "Modelo");
            icon.preserveAspect = true;
            Kit.PlaceTL(icon.rectTransform, 4, 6, 104, 104);
            Kit.LabelAt(card.transform, d.Name, 30, Kit.Brown, 0, true, 110, 10, 330, 40);
            var desc = Kit.LabelAt(card.transform, d.Desc, 21, Kit.Brown, 0, false, 110, 48, 320, 84);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var b = Kit.Button(card.transform, "", Kit.Green, 28, 190, 84);
            Kit.PlaceTL((RectTransform)b.transform, 436, 26, 190, 84);
            var coin = Kit.Icon(b.Content, "coin", 34);
            Kit.PlaceTL((RectTransform)coin.transform, 12, 18, 34, 34);
            b.Label.alignment = TextAnchor.MiddleRight;
            Kit.Stretch(b.Label.rectTransform, 50, 0, 14, 4);
            var kind = d.Kind;
            b.Clicked += () =>
            {
                if (Isl.Build(kind, p)) { CloseSheet(); game.Save(); Sfx.Play("build", -3f); }
                else NoMoney(b);
            };
            AddShine(b, () => Isl.CanBuild(kind, p));
            sheetRefresh.Add(() =>
            {
                b.Label.text = BigNum.Fmt(Isl.BuildCost(kind));
                Color m = Isl.CanBuild(kind, p) ? Color.white : new Color(0.72f, 0.72f, 0.75f);
                if (b.Modulate != m) { b.Modulate = m; b.Restyle(); }
            });
        }

        void MakeUpgradeView(Transform parent, Plot p)
        {
            var d = Island.Defs[p.Building];
            var icon = Kit.Img(parent, IslandStage.I.BuildingIcon(d.Kind, Island.Tier(p.Level)), Color.white, "Modelo");
            icon.preserveAspect = true;
            Kit.PlaceTL(icon.rectTransform, 24, 18, 140, 140);
            NextStage(parent, p, d);
            Kit.LabelAt(parent, d.Name, 38, Kit.Brown, 0, true, 166, 30, 480, 50);
            var lv = Kit.LabelAt(parent, "", 28, Kit.OrangeD, 0, true, 166, 82, 480, 40);
            var desc = Kit.LabelAt(parent, d.Desc, 24, Kit.Brown, 0, false, 36, 160, 610, 60);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var eff = Kit.LabelAt(parent, "", 28, Kit.GreenD, 0, true, 36, 222, 610, 40);
            var b = Kit.Button(parent, "", Kit.Green, 32, 400, 96);
            Kit.Place((RectTransform)b.transform, 0.5f, 0f, -200, 290, 400, 96);
            var coin = Kit.Icon(b.Content, "coin", 40);
            Kit.PlaceTL((RectTransform)coin.transform, 20, 22, 40, 40);
            b.Clicked += () =>
            {
                if (Isl.Upgrade(p)) { game.Save(); Sfx.Play("build", -5f, 1.1f); CloseSheet(); game.FocusOn(game.PlotWorld(p)); }   // VER la obra
                else if (p.Level < d.MaxLevel && p.BuildT < 0f) NoMoney(b);
            };
            AddShine(b, () => Isl.CanUpgrade(p) && p.BuildT < 0f);
            sheetRefresh.Add(() =>
            {
                bool max = p.Level >= d.MaxLevel;
                lv.text = Loc.T("Nivel ") + p.Level + (max ? Loc.T(" (máximo)") : "");
                eff.text = Effect(d.Kind, p.Level, max);
                b.Label.text = max ? Loc.T("Máximo") : Loc.T("Mejorar  ") + BigNum.Fmt(Isl.UpgradeCost(p));
                b.Interactable = !max && p.BuildT < 0f;
                Color m = Isl.CanUpgrade(p) ? Color.white : new Color(0.72f, 0.72f, 0.75f);
                if (b.Modulate != m) { b.Modulate = m; b.Restyle(); }
            });
        }

        string Effect(BKind k, int lv, bool max)
        {
            string next = max ? "" : "  →  ";
            switch (k)
            {
                case BKind.House: return lv + Loc.T(" minero") + (lv > 1 ? "s" : "") + (max ? "" : next + (lv + 1));
                case BKind.Depot: return Loc.T("Precio x") + (1 + 0.12 * (lv - 1)).ToString("0.00") + (max ? "" : next + "x" + (1 + 0.12 * lv).ToString("0.00"));
                case BKind.Canteen: return Loc.T("Comen ") + (22 + 8 * lv) + "/s" + (max ? "" : next + (30 + 8 * lv) + "/s");
                case BKind.Showers: return Loc.T("¡Fresco! ") + (45 + 15 * lv) + " s" + (max ? "" : next + (60 + 15 * lv) + " s");
                case BKind.Smithy: return Loc.T("Golpe x") + (1 + 0.6 * lv).ToString("0.0") + (max ? "" : next + "x" + (1 + 0.6 * (lv + 1)).ToString("0.0"));
                case BKind.Mine: return BigNum.Fmt(Isl.MineRate() * 60) + Loc.T(" por minuto");
                case BKind.Lighthouse: return Loc.T("Vetas raras x") + (1 + 0.35 * lv).ToString("0.00") + (max ? "" : next + "x" + (1 + 0.35 * (lv + 1)).ToString("0.00"));
                case BKind.Dock: return Loc.T("Barcos cada ") + Mathf.RoundToInt(140f / (1f + 0.2f * (lv - 1))) + " s" + (max ? "" : next + Mathf.RoundToInt(140f / (1f + 0.2f * lv)) + " s");
                case BKind.Barn: return Loc.T("Guarda ") + (50 + 40 * lv) + Loc.T(" materias primas") + (max ? "" : next + (90 + 40 * lv));
                case BKind.Warehouse: return Loc.T("Guarda ") + (15 + 20 * lv) + Loc.T(" productos") + (max ? "" : next + (35 + 20 * lv));
                case BKind.Bank: return Loc.T("Ganancia sin conexión: ") + (2 + lv) + " h" + (max ? "" : next + (3 + lv) + " h");
                case BKind.School: return Loc.T("Experiencia +") + (20 * lv) + " %" + (max ? "" : next + "+" + (20 * (lv + 1)) + " %");
                case BKind.Hospital: return Loc.T("Descanso x2 y comen x1.5 más rápido");
                case BKind.Managers: return Loc.T("Cobran las minas solas cada ") + Mathf.Max(3, 12 - lv) + " s" + (max ? "" : next + Mathf.Max(3, 11 - lv) + " s");
                case BKind.Train: return Loc.T("Tren cada ") + Mathf.RoundToInt(1500f / (1f + 0.08f * (lv - 1)) / 60f) + Loc.T(" min");
                case BKind.Market: return Loc.T("Vende al 80 % del valor");
            }
            return "";
        }

        static string IconFor(BKind k)
        {
            switch (k)
            {
                case BKind.House: return "miner";
                case BKind.Depot: return "coin";
                case BKind.Canteen: return "money";
                case BKind.Showers: return "gem";
                case BKind.Smithy: return "hammer";
                case BKind.Mine: return "pick";
                case BKind.Lighthouse: return "star";
                case BKind.Dock: return "chest";
                default: return "speed";
            }
        }

        void RefreshSheet()
        {
            foreach (var a in sheetRefresh) a();
        }

        public void CloseSheet()
        {
            if (sheet == null) return;
            sheetLocked = false;
            if (IslandStage.I != null && IslandStage.I.Busy) IslandStage.I.EndScene();
            // la hoja se va hacia abajo (EaseInBack 180 ms) y el fondo se aclara; ya no recibe toques
            var old = sheet;
            sheet = null;
            sheetRefresh.Clear();
            var grp = Kit.Group(old.gameObject);
            grp.blocksRaycasts = false;
            RectTransform frame = old.Find("Marco") as RectTransform;
            Vector2 p0 = frame != null ? frame.anchoredPosition : Vector2.zero;
            Tw.To(old, "cierra", 0.18f, Ease.InBack, u =>
            {
                if (frame != null) frame.anchoredPosition = p0 + new Vector2(0f, -700f * u);
                grp.alpha = 1f - u;
            }, () => Destroy(old.gameObject));
            Sfx.Play("close", -12f);
        }

        // ------------------------------------------------------------ bienvenida y ajustes
        /// <summary>
        /// Pantalla de regreso (biblia 3.6): la isla aparece desde blanco, los mineros saludan, el contador de lo ganado
        /// sube con un tic que acelera, el cofre se abre solo y el boton x2 (con gemas) late.
        /// </summary>
        void Welcome(double gain)
        {
            Flash(Color.white, 0.9f);
            Tw.After(this, "saludo", 0.4f, () => { foreach (var m in Isl.Miners) game.Cheer(m, 1f); });   // los mineros saludan
            var fr = OpenSheet(560, true);
            Kit.LabelAt(fr, Loc.T("¡Hola de nuevo!"), 46, Kit.Brown, 0, true, 0, 26, 680, 58, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, Loc.T("Mientras no estabas, tus mineros juntaron"), 26, Kit.Brown, 0, false, 0, 92, 680, 40, TextAnchor.MiddleCenter);
            var chest = Kit.Img(fr, ChestSprite(), Color.white, "Cofre");
            chest.preserveAspect = true;
            Kit.Place(chest.rectTransform, 0.5f, 0f, -80f, 140f, 160, 160);
            var amount = Kit.LabelAt(fr, "0", 64, Kit.OrangeD, 0, true, 0, 300, 680, 76, TextAnchor.MiddleCenter);
            double shown = 0;
            float lastTick = 0f;
            // el contador sube en 2 s; el tic se acelera
            Tw.To(amount, "cuenta", 2f, Ease.OutCubic, u =>
            {
                shown = gain * u;
                amount.text = BigNum.Fmt(System.Math.Floor(shown));
                float every = Mathf.Lerp(0.12f, 0.04f, u);
                if (Time.unscaledTime - lastTick > every && u < 0.98f) { lastTick = Time.unscaledTime; Sfx.Play("tick", -12f, 1f + u * 0.6f); }
            }, () =>
            {
                Tw.Pop(amount.rectTransform, 1.25f);
                Tw.Pop(chest.rectTransform, 1.3f);
                Sfx.Play("chest", -4f);
                FlyCoins(LayerPos(chest.rectTransform), 5);
            });
            Tw.To(chest, "salta", 2f, Ease.Linear, u => chest.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 30f) * 6f * u));
            var b = Kit.Button(fr, Loc.T("¡Cobrar!"), Kit.Green, 34, 300, 96);
            Kit.Place((RectTransform)b.transform, 0.5f, 0f, -320f, 410f, 300, 96);
            primaryBtn = b;
            System.Action done = () =>
            {
                CloseSheet();
                FlyCoins(Vector2.zero, 6);
                Sfx.Play("coins_pour", -3f);
                Juice.Vibrate(40);
                if (game.DailyPending) Tw.After(this, "diario", 1.4f, OpenDaily);
            };
            b.Clicked += done;
            if (Isl.CanAd(AdPlace.OfflineX2, IslandGame.Today, Now))
            {
                // anuncio con premio: el mas valioso del dia, justo al volver (C.2)
                var ad = AdButton(fr, Loc.T("x2 gratis"), 300, 96, 30);
                Kit.Place((RectTransform)ad.transform, 0.5f, 0f, 20f, 410f, 300, 96);
                Tw.To(ad, "late", 99f, Ease.Linear, u => { if (!ad.IsPressed) ad.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 6f) * 0.05f); });
                ad.Clicked += () => WatchAd(AdPlace.OfflineX2, null, gain, got => { Flash(new Color(1f, 0.8f, 0.4f), 0.3f); done(); });
            }
            else if (Isl.Gems >= 3)
            {
                var x2 = Kit.Button(fr, Loc.T("x2 (3 gemas)"), Kit.Purple, 30, 300, 96);
                Kit.Place((RectTransform)x2.transform, 0.5f, 0f, 20f, 410f, 300, 96);
                Tw.To(x2, "late", 99f, Ease.Linear, u => { if (!x2.IsPressed) x2.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 6f) * 0.05f); });
                x2.Clicked += () =>
                {
                    if (Isl.Gems < 3) return;
                    Isl.Gems -= 3;
                    Isl.GiveCoins(gain);
                    Sfx.Play("powerup", -3f);
                    Flash(new Color(0.8f, 0.6f, 1f), 0.3f);
                    done();
                };
            }
        }

        static void LoadSettings()
        {
            Sfx.SoundOn = PlayerPrefs.GetInt("isla_snd", 1) == 1;
            Sfx.MusicOn = PlayerPrefs.GetInt("isla_mus", 1) == 1;
            Juice.VibrationOn = PlayerPrefs.GetInt("isla_vib", 1) == 1;
        }

        public void OpenSettings()
        {
            var fr = OpenSheet(900, true);
            Kit.LabelAt(fr, Loc.T("Ajustes"), 44, Kit.Brown, 0, true, 0, 26, 680, 56, TextAnchor.MiddleCenter);
            // privacidad: consentimiento de anuncios (Europa / EE. UU.), politica y terminos (los piden las tiendas y AdMob)
            var pb = Kit.Button(fr, Loc.T("Privacidad"), Kit.Blue, 24, 190, 72, "Privacidad");
            Kit.PlaceTL((RectTransform)pb.transform, 40, 716, 190, 72);
            pb.Clicked += () =>
            {
                if (Mineros.Monetization.Ads.PrivacyOptionsRequired)
                    Mineros.Monetization.Ads.ShowPrivacyOptions(err => { if (err != null) Toast(Loc.T("No se pudo abrir ahora"), Kit.Gray, null, true); });
                else Application.OpenURL(Mineros.Monetization.Legal.Privacy);
            };
            var pol = Kit.Button(fr, Loc.T("Política"), Kit.Gray, 24, 190, 72, "Politica");
            Kit.PlaceTL((RectTransform)pol.transform, 245, 716, 190, 72);
            pol.Clicked += () => Application.OpenURL(Mineros.Monetization.Legal.Privacy);
            var ter = Kit.Button(fr, Loc.T("Términos"), Kit.Gray, 24, 190, 72, "Terminos");
            Kit.PlaceTL((RectTransform)ter.transform, 450, 716, 190, 72);
            ter.Clicked += () => Application.OpenURL(Mineros.Monetization.Legal.Terms);
            // idioma: English / Español (se rearma la isla en el idioma nuevo)
            Kit.LabelAt(fr, Loc.T("Idioma"), 32, Kit.Brown, 0, true, 60, 528, 300, 50);
            var lb = Kit.Button(fr, Loc.En ? "English" : "Español", Kit.Blue, 28, 200, 80, "Idioma");
            Kit.PlaceTL((RectTransform)lb.transform, 420, 510, 200, 80);
            lb.Clicked += () => { LocSetup.Set(Loc.En ? "es" : "en"); CloseSheet(); Flash(Color.white, 0.35f); game.Rebuild(); };
            Toggle(fr, "Reducir movimiento", 610, () => Mineros.UI.Motion.Reduced, v => { Mineros.UI.Motion.Reduced = v; PlayerPrefs.SetInt("isla_menosmov", v ? 1 : 0); });
            Toggle(fr, "Ahorro de batería", 410, () => PlayerPrefs.GetInt("isla_ahorro", 0) == 1, v => { PlayerPrefs.SetInt("isla_ahorro", v ? 1 : 0); IslandGame.ApplyFrameRate(); });
            Toggle(fr, "Sonido", 110, () => Sfx.SoundOn, v => { Sfx.SoundOn = v; PlayerPrefs.SetInt("isla_snd", v ? 1 : 0); });
            Toggle(fr, "Música", 210, () => Sfx.MusicOn, v => { Sfx.SetMusic(v, 0); PlayerPrefs.SetInt("isla_mus", v ? 1 : 0); });
            Toggle(fr, "Vibración", 310, () => Juice.VibrationOn, v => { Juice.VibrationOn = v; PlayerPrefs.SetInt("isla_vib", v ? 1 : 0); if (v) Juice.Vibrate(30); });
        }

        /// <summary>Interruptor real: la perilla se desliza con rebote y el fondo cambia de color (tick de vibracion).</summary>
        void Toggle(Transform parent, string label, float y, System.Func<bool> get, System.Action<bool> set)
        {
            Kit.LabelAt(parent, Loc.Show(label), 32, Kit.Brown, 0, true, 60, y + 18, 300, 50);
            var hit = Kit.HitArea(parent, 132, 72, "Interruptor");
            Kit.PlaceTL((RectTransform)hit.transform, 470, y + 4, 132, 72);
            var track = Kit.Img(hit.transform, ButtonArt.Box(Color.white, 30f, 0f, 0.15f), Color.white, "Pista");
            track.type = Image.Type.Sliced;
            Kit.Stretch(track.rectTransform);
            var knob = Kit.Img(hit.transform, Icons.Dot(), Color.white, "Perilla");
            var kr = knob.rectTransform;
            kr.sizeDelta = new Vector2(50, 50);
            var ksh = Kit.Img(kr, Icons.Dot(), new Color(0f, 0f, 0f, 0.18f), "Sombra");
            ksh.rectTransform.sizeDelta = new Vector2(50, 50);
            ksh.rectTransform.anchoredPosition = new Vector2(0f, -3f);
            ksh.transform.SetAsFirstSibling();
            System.Action<bool> paint = anim =>
            {
                bool on = get();
                Color tc = on ? Kit.Green : new Color(0.78f, 0.76f, 0.74f);
                Vector2 to = new Vector2(on ? 30f : -30f, 2f);
                if (!anim) { track.color = tc; kr.anchoredPosition = to; return; }
                Color from = track.color; Vector2 p0 = kr.anchoredPosition;
                Tw.To(kr, "perilla", 0.24f, Ease.OutBack, u => { kr.anchoredPosition = Vector2.LerpUnclamped(p0, to, u); track.color = Color.Lerp(from, tc, Mathf.Clamp01(u)); });
                Tw.To(kr.GetChild(0), "aplasta", 0.24f, Ease.Linear, u => kr.localScale = new Vector3(1f + Mathf.Sin(u * Mathf.PI) * 0.18f, 1f - Mathf.Sin(u * Mathf.PI) * 0.1f, 1f));
            };
            paint(false);
            hit.Clicked += () => { set(!get()); paint(true); PlayerPrefs.Save(); Mineros.Fx.Haptics.Selection(); };
        }
    }
}
