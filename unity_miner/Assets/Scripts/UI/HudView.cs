using System;
using System.Collections;
using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>
    /// HUD principal del modo idle (hud.gd). Los botones aparecen de a uno (desbloqueo progresivo):
    /// G.IsUnlocked(f) manda; mientras una funcion espera su revelacion (`held`) sigue oculta.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public event Action<string> OpenPanel;
        public event Action PlayPressed;
        /// <summary>El jugador toco el elemento recien desbloqueado (se apaga su "¡NUEVO!").</summary>
        public event Action<string> FeatureTouched;
        /// <summary>Se reclamo una meta: `gems` vuelan desde `from` (canvas) a la pildora de gemas.</summary>
        public event Action<int, Vector2> GoalClaimed;

        sealed class SideBtn
        {
            public Btn B;
            public RectTransform Rt;
            public IconView Icon;
            public Text Label;
            public float X;
            public Image Bg;
        }

        sealed class Card
        {
            public string Kind;
            public Btn B;
            public RectTransform Rt;
            public Text Lvl, Val, Cost;
            public Image CostBg;
            public Capsule Ms;
            public IconView Arrow;
            public int LastLv = -1;
            public double LastVal = -1;
            public int LastNext = -2;
            public int LastOk = -1;
            public int LastMsLv = -1;
        }

        static readonly string[] CardOrder = { "power", "speed", "money" };

        static readonly string[] FeatureOrder =
            { "speed", "money", "boost", "tools", "play", "shop", "skin", "rebirth", "piggy", "auto", "missions", "achievements" };

        GameManager gm;
        GameState G { get { return gm.G; } }
        RectTransform root;
        RectTransform fxParent;

        // barra superior
        Image progFill;
        RectTransform progBar;
        Text progLabel, stageLabel;
        int lastPct = -1;
        RectTransform trackRt;
        Image trackGold;
        Image[] nodeFill = new Image[5];
        IconView trackMiner;
        float trackW = 260f;
        int lastTrackSub = -1;
        float lastTrackProg = -1f;

        // fila superior y derecha
        Btn gearBtn, dailyBtn, trophyBtn;
        Badge dailyBadge, trophyBadge, piggyBadge, missionBadge;
        Pill goldPill, gemPill;
        Text bossBanner, boostLabel, piggyLabel;
        Image boostGlow;
        SideBtn mission, boost, shop, rebirth, skin, tool, piggy, auto, play;
        readonly Dictionary<string, Card> cards = new Dictionary<string, Card>();
        public GoalTracker Tracker { get; private set; }
        public EventRibbon Ribbon { get; private set; }

        readonly Dictionary<string, RectTransform[]> feat = new Dictionary<string, RectTransform[]>();
        readonly Dictionary<string, RectTransform> primary = new Dictionary<string, RectTransform>();
        readonly HashSet<string> held = new HashSet<string>();
        readonly Dictionary<string, RectTransform> tags = new Dictionary<string, RectTransform>();
        readonly HashSet<RectTransform> placed = new HashSet<RectTransform>();
        int sig = -1;
        string holdKind = "";
        float holdT, holdRep;
        float refreshT;
        double goldDisp;
        double goldShown = -1;
        float tm;
        int flyPending;
        float flyTimeout;
        bool gemShown;
        int lastGemText = -1;
        int lastPiggy = -1;
        bool lastAuto;
        bool bossBlinkOn;

        public RectTransform Root { get { return root; } }

        // ================================================================ construccion
        public void Init(GameManager manager, RectTransform hudRoot, RectTransform fx)
        {
            gm = manager;
            root = hudRoot;
            fxParent = fx;
            BuildTop();
            BuildSide();
            BuildCards();
            Tracker = GoalTracker.Create(root, gm);
            Kit.PlaceTL((RectTransform)Tracker.transform, 10, 172, GoalTracker.W, GoalTracker.H);
            Tracker.WillClaim += GemsInFlight;
            Tracker.Claimed += (g, p) => { if (GoalClaimed != null) GoalClaimed(g, p); };
            Ribbon = EventRibbon.Create(root, gm);
            G.Changed += Refresh;
            G.StatsChanged += Refresh;
            G.FeatureUnlocked += OnFeatureUnlocked;
            foreach (string f in G.NewlyUnlocked()) held.Add(f);
            BuildFeatMap();
            goldDisp = G.Gold;
            Refresh();
            ApplyVisibility(false);
        }

        void OnDestroy()
        {
            if (gm == null) return;
            G.Changed -= Refresh;
            G.StatsChanged -= Refresh;
            G.FeatureUnlocked -= OnFeatureUnlocked;
        }

        void OnFeatureUnlocked(string f)
        {
            held.Add(f);
            ApplyVisibility(true);
        }

        void BuildTop()
        {
            // banda superior (se extiende por arriba para cubrir muescas)
            Image band = Kit.Tint(root, Icons.H("4a3826"), "Band");
            RectTransform br = band.rectTransform;
            br.anchorMin = new Vector2(0, 1);
            br.anchorMax = new Vector2(1, 1);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.offsetMin = new Vector2(0, -58);
            br.offsetMax = new Vector2(0, 400);
            // riel de la barra de progreso
            Image barBg = Kit.Box9(root, "progress_transparent", new Vector4(8, 8, 8, 8), new Color(0.55f, 0.45f, 0.35f, 1f), "ProgBar");
            progBar = barBg.rectTransform;
            progBar.anchorMin = new Vector2(0, 1);
            progBar.anchorMax = new Vector2(1, 1);
            progBar.pivot = new Vector2(0.5f, 0.5f);
            progBar.offsetMin = new Vector2(6, -70);
            progBar.offsetMax = new Vector2(-6, -52);
            progFill = Kit.Box9(progBar, "progress_white_border", new Vector4(8, 8, 8, 8), new Color(1f, 0.92f, 0.6f, 1f), "Fill");
            Kit.PlaceTL(progFill.rectTransform, 0, 0, 18, 18);
            progFill.gameObject.SetActive(false);
            progLabel = Kit.Label(root, "0%", 20, Color.white, 6, true, TextAnchor.MiddleCenter, "ProgLabel");
            Kit.Place(progLabel.rectTransform, 0.5f, 0f, -60, 22, 120, 28);

            // fila superior: ajustes, premio diario, logros
            gearBtn = Kit.SqButton(root, Kit.Blue, 50, "Gear");
            Kit.PlaceTL(gearBtn.GetComponent<RectTransform>(), 10, 78, 50, 50);
            Image gi = Kit.KIcon(gearBtn.transform, "gear", 30);
            Kit.PlaceTL(gi.rectTransform, 10, 7, 30, 30);
            gearBtn.Clicked += () => Open("settings");

            dailyBtn = Kit.SqButton(root, Kit.Blue, 50, "Daily");
            Kit.PlaceTL(dailyBtn.GetComponent<RectTransform>(), 64, 78, 50, 50);
            IconView dg = Kit.Icon(dailyBtn.transform, "gem", 34);
            Kit.PlaceTL((RectTransform)dg.transform, 8, 5, 34, 34);
            dailyBadge = Kit.MakeBadge(dailyBtn.transform);
            Kit.PlaceTL(dailyBadge.Root, 32, -14, 26, 26);
            dailyBtn.Clicked += () => Open("daily");

            trophyBtn = Kit.SqButton(root, Kit.Orange, 50, "Trophy");
            Kit.PlaceTL(trophyBtn.GetComponent<RectTransform>(), 118, 78, 50, 50);
            Image ti = Kit.KIcon(trophyBtn.transform, "trophy", 30);
            Kit.PlaceTL(ti.rectTransform, 10, 7, 30, 30);
            trophyBadge = Kit.MakeBadge(trophyBtn.transform);
            Kit.PlaceTL(trophyBadge.Root, 32, -14, 26, 26);
            trophyBtn.Clicked += () => Open("achievements");

            // pista de 5 sub-etapas con jefe
            trackRt = Kit.New("Track", root);
            Kit.PlaceTL(trackRt, 184, 84, 300, 40);
            trackW = 260f;
            Image lineOut = Kit.Tint(trackRt, Kit.Out, "LineOut");
            Image lineCream = Kit.Tint(trackRt, Kit.CreamD, "LineCream");
            trackGold = Kit.Tint(trackRt, Kit.Yellow, "LineGold");
            Kit.PlaceTL(lineOut.rectTransform, 0, 20 - 4.5f, trackW, 9);
            Kit.PlaceTL(lineCream.rectTransform, 0, 20 - 2.5f, trackW, 5);
            Kit.PlaceTL(trackGold.rectTransform, 0, 20 - 2.5f, 0, 5);
            for (int i = 0; i < 5; i++)
            {
                float cx = trackW * i / 5f + 6f;
                Image o = Kit.Img(trackRt, Icons.Dot(), Kit.Out, "NodeOut");
                Kit.PlaceTL(o.rectTransform, cx - 10, 10, 20, 20);
                nodeFill[i] = Kit.Img(trackRt, Icons.Dot(), Kit.CreamD, "Node");
                Kit.PlaceTL(nodeFill[i].rectTransform, cx - 7.5f, 12.5f, 15, 15);
            }
            trackMiner = Kit.Icon(trackRt, "miner", 26);
            Kit.PlaceTL((RectTransform)trackMiner.transform, 0, 0, 26, 26);
            IconView bossIcon = Kit.Icon(trackRt, "boss", 34);
            Kit.PlaceTL((RectTransform)bossIcon.transform, trackW + 18 - 17, 20 - 17, 34, 34);

            stageLabel = Kit.LabelAt(root, "Etapa 1-1", 30, Color.white, 8, true, 170, 126, 300, 40, TextAnchor.MiddleCenter);

            // pildoras de oro y gemas
            goldPill = Kit.MakePill(root, "coin", 196);
            Kit.Place(goldPill.Root, 1f, 0f, -210, 82, 196, 46);
            gemPill = Kit.MakePill(root, "gem", 150);
            Kit.Place(gemPill.Root, 1f, 0f, -164, 136, 150, 46);

            mission = MakeSide("mission", "Misiones", null, 86, "Missions");
            Kit.Place(mission.Rt, 1f, 0f, -92, 192, 86, 82);
            mission.X = -92;
            missionBadge = Kit.MakeBadge(mission.Rt);
            missionBadge.AlwaysNumber = true;
            Kit.PlaceTL(missionBadge.Root, 49, -5, 26, 26);
            mission.B.Clicked += () => Open("missions");

            bossBanner = Kit.Label(root, "¡Apareció una GEODA GIGANTE!", 30, Kit.Yellow, 9, true, TextAnchor.MiddleCenter, "BossBanner");
            Kit.Place(bossBanner.rectTransform, 0.5f, 0f, -330, 336, 660, 40);
            bossBanner.gameObject.SetActive(false);
        }

        SideBtn MakeSide(string kind, string text, Color? bg, float w, string name)
        {
            SideBtn s = new SideBtn();
            Btn b;
            if (bg.HasValue)
            {
                b = Kit.SqButton(root, bg.Value, 82, name);
                b.GetComponent<RectTransform>().sizeDelta = new Vector2(w, 82);
                s.Bg = b.Bg;
            }
            else b = Kit.HitArea(root, w, 82, name);
            s.B = b;
            s.Rt = b.GetComponent<RectTransform>();
            s.Rt.sizeDelta = new Vector2(w, 82);
            s.Icon = Kit.Icon(b.transform, kind, 50);
            Kit.PlaceTL((RectTransform)s.Icon.transform, (w - 50) * 0.5f, 2, 50, 50);
            s.Label = Kit.LabelAt(b.transform, text, 19, Color.white, 6, true, -10, 52, w + 20, 26, TextAnchor.UpperCenter);
            return s;
        }

        void BuildSide()
        {
            // brillo del x3 Oro: hermano previo para quedar detras del boton
            boostGlow = Kit.Glow(root, Icons.H("ffb030"), "BoostGlow");
            boostGlow.gameObject.SetActive(false);
            boost = MakeSide("boost", "x3 Oro", Kit.Orange, 86, "Boost");
            shop = MakeSide("shop", "Tienda", Kit.Blue, 86, "Shop");
            rebirth = MakeSide("rebirth", "Renacer", Kit.Purple, 86, "Rebirth");
            skin = MakeSide("skin", "Cascos", Kit.Green, 86, "Skin");
            tool = MakeSide("tool", "Equipo", Kit.Red, 86, "Tool");
            SideBtn[] left = { boost, shop, rebirth, skin, tool };
            for (int i = 0; i < left.Length; i++)
            {
                left[i].X = 8;
                Kit.Place(left[i].Rt, 0f, 1f, 8, -300, 86, 82);
            }
            boost.B.Clicked += () => SidePressed("boost");
            shop.B.Clicked += () => Open("shop");
            rebirth.B.Clicked += () => Open("rebirth");
            skin.B.Clicked += () => Open("skin");
            tool.B.Clicked += () => Open("tools");
            boostLabel = Kit.LabelAt(boost.B.transform, "", 20, Kit.Yellow, 7, true, 92, 22, 150, 34, TextAnchor.MiddleLeft);

            piggy = MakeSide("piggy", "Alcancía", null, 86, "Piggy");
            auto = MakeSide("auto", "Auto", null, 86, "Auto");
            play = MakeSide("pick", "Excavar", Kit.Orange, 100, "Play");
            piggy.X = -94;
            auto.X = -94;
            play.X = -110;
            Kit.Place(piggy.Rt, 1f, 1f, -94, -300, 86, 82);
            Kit.Place(auto.Rt, 1f, 1f, -94, -300, 86, 82);
            Kit.Place(play.Rt, 1f, 1f, -110, -300, 100, 82);
            piggyLabel = Kit.LabelAt(piggy.B.transform, "0", 18, Color.white, 6, true, 54, 0, 34, 24, TextAnchor.UpperCenter);
            piggyBadge = Kit.MakeBadge(piggy.B.transform);
            Kit.PlaceTL(piggyBadge.Root, 64, -6, 26, 26);
            piggy.B.Clicked += () => Open("piggy");
            auto.B.Clicked += () =>
            {
                G.AutoUp = !G.AutoUp;
                ToastMsg("Auto-mejora " + (G.AutoUp ? "activada" : "desactivada"));
                Refresh();
            };
            play.B.Clicked += () => { if (PlayPressed != null) PlayPressed(); };
        }

        void ToastMsg(string s)
        {
            // el Toast del juego pasa por el evento de Core; se emite a traves de un metodo publico del Hud controller
            HudController.Toast(s);
        }

        void SidePressed(string k)
        {
            if (k != "boost") return;
            if (G.BoostActive()) ToastMsg("x3 Oro activo");
            else if (G.ActivateBoost())
            {
                ToastMsg("¡x3 Oro durante 60 s!");
                Sfx.Play("upgrade");
                Kit.Buzz(25);
            }
            else ToastMsg("Disponible en " + BigNum.TimeHms(G.BoostCdUntil - G.Now()));
        }

        void Open(string name) { if (OpenPanel != null) OpenPanel(name); }

        // ---------------------------------------------------------------- tarjetas
        void BuildCards()
        {
            string[,] defs = { { "power", "Fuerza", "power" }, { "speed", "Velocidad", "speed" }, { "money", "Oro", "money" } };
            for (int i = 0; i < 3; i++) MakeCard(defs[i, 0], defs[i, 1], defs[i, 2]);
        }

        void MakeCard(string kind, string title, string ic)
        {
            Card c = new Card();
            c.Kind = kind;
            Btn b = Kit.HitArea(root, 224, 172, "Card_" + kind);
            c.B = b;
            c.Rt = b.GetComponent<RectTransform>();
            Kit.Place(c.Rt, 0.5f, 1f, -112, -200, 224, 172);
            Image frame = Kit.Box9(b.transform, "btn_green", new Vector4(14, 14, 14, 18), Color.white, "Frame");
            Kit.PlaceTL(frame.rectTransform, 0, 30, 224, 140);
            Image inner = Kit.RoundImg(frame.transform, 12, new Color(0.12f, 0.2f, 0.1f, 0.45f), "Inner");
            Kit.PlaceTL(inner.rectTransform, 8, 8, 208, 66);
            Kit.LabelAt(b.transform, title, 22, Color.white, 6, true, 0, 0, 224, 30, TextAnchor.UpperCenter, "Title");
            c.Lvl = Kit.LabelAt(b.transform, "Lv.1", 18, Color.white, 6, true, 140, 16, 76, 24, TextAnchor.UpperRight, "Lvl");
            IconView icon = Kit.Icon(b.transform, ic, 46);
            Kit.PlaceTL((RectTransform)icon.transform, -6, 18, 46, 46);
            c.Val = Kit.LabelAt(b.transform, "0", 36, Color.white, 8, true, 8, 38, 208, 46, TextAnchor.MiddleCenter, "Val");
            c.Ms = Kit.MakeCapsule(b.transform, 192, 20, "green_border", new Color(1f, 0.9f, 0.4f, 1f), new Color(0.3f, 0.4f, 0.28f, 1f), 15);
            Kit.PlaceTL(c.Ms.Root, 16, 82, 192, 20);
            c.CostBg = Kit.Box9(b.transform, "btn_yellow", new Vector4(12, 12, 12, 16), Color.white, "CostBg");
            Kit.PlaceTL(c.CostBg.rectTransform, 8, 116, 208, 46);
            IconView coin = Kit.Icon(b.transform, "coin", 40);
            Kit.PlaceTL((RectTransform)coin.transform, 10, 119, 40, 40);
            c.Cost = Kit.LabelAt(b.transform, "0", 26, Color.white, 7, true, 50, 118, 156, 40, TextAnchor.MiddleCenter, "Cost");
            c.Arrow = Kit.Icon(b.transform, "arrow", 40);
            Kit.PlaceTL((RectTransform)c.Arrow.transform, -10, -26, 40, 40);
            c.Arrow.gameObject.SetActive(false);
            string k = kind;
            b.Down += () =>
            {
                holdKind = k;
                holdT = 0f;
                holdRep = 0f;
                TryBuy(k);
            };
            b.Up += () => { holdKind = ""; };
            b.PlaySound = false;
            cards[kind] = c;
        }

        void TryBuy(string kind)
        {
            if (!G.Buy(kind)) return;
            Sfx.Play("upgrade", -6f);
            Kit.Buzz(10);
            Card c = cards[kind];
            Tw.Scale(c.Rt, Vector3.one * 0.94f, Vector3.one, 0.2f, Ease.OutBack);
            Tw.Pop(c.Val.transform);
        }

        /// <summary>Rebote + brillo de la tarjeta cuando se alcanza un hito de nivel.</summary>
        public void BounceCard(string kind)
        {
            Card c;
            if (!cards.TryGetValue(kind, out c)) return;
            Tw.Scale(c.Rt, Vector3.one, Vector3.one * 1.14f, 0.12f, Ease.OutBack, 0f,
                () => Tw.Scale(c.Rt, Vector3.one * 1.14f, Vector3.one, 0.3f, Ease.OutBounce));
            Kit.Buzz(40);
            Vector2 ctr = Kit.ToCanvas(c.Rt).center + new Vector2(0, 10);
            BurstFx.Spawn(fxParent, ctr, Icons.H("fff0a0"), 110f, 10);
        }

        // ---------------------------------------------------------------- desbloqueo progresivo
        void BuildFeatMap()
        {
            feat["speed"] = new[] { cards["speed"].Rt };
            feat["money"] = new[] { cards["money"].Rt };
            feat["boost"] = new[] { boost.Rt };
            feat["tools"] = new[] { tool.Rt };
            feat["play"] = new[] { play.Rt };
            feat["shop"] = new[] { shop.Rt };
            feat["skin"] = new[] { skin.Rt };
            feat["rebirth"] = new[] { rebirth.Rt };
            feat["piggy"] = new[] { piggy.Rt };
            feat["auto"] = new[] { auto.Rt };
            feat["missions"] = new[] { mission.Rt, dailyBtn.GetComponent<RectTransform>() };
            feat["achievements"] = new[] { trophyBtn.GetComponent<RectTransform>() };
            primary["speed"] = cards["speed"].Rt;
            primary["money"] = cards["money"].Rt;
            primary["boost"] = boost.Rt;
            primary["tools"] = tool.Rt;
            primary["play"] = play.Rt;
            primary["shop"] = shop.Rt;
            primary["skin"] = skin.Rt;
            primary["rebirth"] = rebirth.Rt;
            primary["piggy"] = piggy.Rt;
            primary["auto"] = auto.Rt;
            primary["missions"] = mission.Rt;
            primary["achievements"] = trophyBtn.GetComponent<RectTransform>();
            foreach (KeyValuePair<string, RectTransform> kv in primary)
            {
                string f = kv.Key;
                Btn b = kv.Value.GetComponent<Btn>();
                if (b != null) b.Down += () => Touch(f);
            }
        }

        bool Shown(string f) { return G.IsUnlocked(f) && !held.Contains(f); }

        void ApplyVisibility(bool animate = true)
        {
            int s = 0;
            for (int i = 0; i < FeatureOrder.Length; i++)
            {
                string f = FeatureOrder[i];
                bool on = Shown(f);
                if (on) s |= 1 << i;
                RectTransform[] nodes = feat[f];
                for (int n = 0; n < nodes.Length; n++)
                    if (nodes[n].gameObject.activeSelf != on) nodes[n].gameObject.SetActive(on);
            }
            bool showGems = G.Gems > 0 || flyPending > 0 || G.Stat("goals") > 0;
            if (showGems) s |= 1 << 20;
            if (gemPill.Root.gameObject.activeSelf != showGems) gemPill.Root.gameObject.SetActive(showGems);
            if (showGems && !gemShown)
            {
                gemShown = true;
                if (animate) Tw.Reveal(gemPill.Root);
            }
            if (s != sig)
            {
                sig = s;
                Layout(animate);
            }
        }

        void MoveTo(RectTransform c, float x, float y, bool animate)
        {
            Vector2 s = c.sizeDelta;
            Vector2 target = new Vector2(x + s.x * 0.5f, -(y + s.y * 0.5f));
            if (!animate || !placed.Contains(c))
            {
                Tw.Kill(c, "move");
                c.anchoredPosition = target;
                placed.Add(c);
                return;
            }
            if ((c.anchoredPosition - target).sqrMagnitude < 0.01f) return;
            Tw.Move(c, target, 0.22f, Ease.OutCubic);
        }

        void Stack(SideBtn[] items, float baseY, float step, bool animate)
        {
            int k = 0;
            for (int i = items.Length - 1; i >= 0; i--)
            {
                SideBtn b = items[i];
                if (!b.Rt.gameObject.activeSelf) continue;
                MoveTo(b.Rt, b.X, -300f + baseY - step * k, animate);
                k++;
            }
        }

        void Layout(bool animate)
        {
            Stack(new[] { boost, shop, rebirth, skin, tool }, -80f, 90f, animate);
            Stack(new[] { piggy, auto, play }, -82f, 90f, animate);
            // tarjetas centradas segun cuantas hay
            string[] order = { "power", "speed", "money" };
            int n = 0;
            for (int i = 0; i < 3; i++) if (cards[order[i]].Rt.gameObject.activeSelf) n++;
            float x0 = -(n * 233f - 9f) * 0.5f;
            int idx = 0;
            for (int i = 0; i < 3; i++)
            {
                Card c = cards[order[i]];
                if (!c.Rt.gameObject.activeSelf) continue;
                MoveTo(c.Rt, x0 + idx * 233f, -200f, animate);
                idx++;
            }
            // fila superior: engranaje, premio diario, trofeo
            float x = 10f;
            RectTransform[] top = { gearBtn.GetComponent<RectTransform>(), dailyBtn.GetComponent<RectTransform>(), trophyBtn.GetComponent<RectTransform>() };
            for (int i = 0; i < top.Length; i++)
            {
                if (!top[i].gameObject.activeSelf) continue;
                MoveTo(top[i], x, 78f, animate);
                x += 54f;
            }
        }

        void Touch(string f)
        {
            RectTransform tg;
            if (!tags.TryGetValue(f, out tg)) return;
            tags.Remove(f);
            if (tg != null)
                Tw.Scale(tg, Vector3.zero, 0.12f, Ease.Linear, 0f, () => { if (tg != null) Destroy(tg.gameObject); });
            G.MarkFeatureSeen(f);
            if (FeatureTouched != null) FeatureTouched(f);
        }

        public bool HasNewTag(string f) { return tags.ContainsKey(f); }

        public Rect TargetRect(string f)
        {
            RectTransform p;
            if (primary.TryGetValue(f, out p) && p != null) return Kit.ToCanvas(p);
            return new Rect(0, 0, 0, 0);
        }

        /// <summary>Zona de la recompensa de la franja de metas (ahi apunta la mano).</summary>
        public Rect TrackerRewardRect()
        {
            Rect r = Kit.ToCanvas((RectTransform)Tracker.transform);
            return new Rect(r.xMax - 110f, r.y + 14f, 90f, r.height);
        }

        public Rect CardRect(string kind) { return Kit.ToCanvas(cards[kind].Rt); }

        public bool HoldPending { get { return held.Count > 0; } }

        /// <summary>Revela la funcion con escala 0 a 1, destello y etiqueta "¡NUEVO!" pegada hasta que se toca.</summary>
        public IEnumerator Reveal(string f)
        {
            RectTransform p;
            if (!primary.TryGetValue(f, out p))
            {
                held.Remove(f);
                yield break;
            }
            held.Remove(f);
            RectTransform[] nodes = feat[f];
            for (int i = 0; i < nodes.Length; i++) placed.Remove(nodes[i]);
            sig = -1;
            ApplyVisibility(true);
            for (int i = 0; i < nodes.Length; i++) Tw.Reveal(nodes[i]);
            pulseHold = Time.unscaledTime + 0.45f;
            BurstFx.Spawn(fxParent, Kit.ToCanvas(p).center, Icons.H("fff0a0"), 120f, 10);
            Sfx.Play("unlock", -4f, 1.25f);
            Kit.Buzz(20);
            if (!tags.ContainsKey(f) && !G.FeaturesSeen.Contains(f))
            {
                RectTransform tg = Kit.MakeTag(p, "¡NUEVO!", Kit.Red, 16);
                float tw = tg.sizeDelta.x;
                Vector2 ps = p.sizeDelta;
                if (f == "speed" || f == "money") Kit.PlaceTL(tg, ps.x - tw - 2, -36, tw, tg.sizeDelta.y);
                else Kit.PlaceTL(tg, ps.x - tw * 0.75f, -12, tw, tg.sizeDelta.y);
                tags[f] = tg;
                Tw.Reveal(tg, 0.2f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ================================================================ bucle
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            tm += dt;
            goldDisp += (G.Gold - goldDisp) * (1.0 - Math.Exp(-dt * 9.0));
            if (Math.Abs(goldDisp - G.Gold) <= Math.Max(1.0, G.Gold * 0.0005)) goldDisp = G.Gold;
            if (goldDisp != goldShown)
            {
                goldShown = goldDisp;
                goldPill.Label.text = BigNum.Fmt(goldDisp);
            }
            // un solo foco: la flecha rebota solo sobre la primera mejora que se puede comprar
            bool arrowTaken = false;
            for (int i = 0; i < CardOrder.Length; i++)
            {
                Card c = cards[CardOrder[i]];
                bool on = !arrowTaken && c.Rt.gameObject.activeSelf && G.CanBuy(CardOrder[i]);
                if (c.Arrow.gameObject.activeSelf != on) c.Arrow.gameObject.SetActive(on);
                if (on)
                {
                    arrowTaken = true;
                    Kit.SetPos((RectTransform)c.Arrow.transform, -10, -22 - Mathf.Abs(Mathf.Sin(tm * 5f)) * 8f);
                }
            }
            if (flyPending > 0)
            {
                flyTimeout -= dt;
                if (flyTimeout <= 0f)
                {
                    flyPending = 0;
                    Refresh();
                }
            }
            UpdateTrack(false);
            if (holdKind != "")
            {
                holdT += dt;
                if (holdT > 0.4f)
                {
                    holdRep += dt;
                    float iv = holdT < 1.5f ? 0.12f : 0.05f;
                    int guard = 0;
                    while (holdRep >= iv && guard++ < 8)
                    {
                        holdRep -= iv;
                        TryBuy(holdKind);
                    }
                }
            }
            refreshT += dt;
            if (refreshT > 0.25f)
            {
                refreshT = 0f;
                RefreshTimers();
            }
            BoostFx();
            PlayBreath();
        }

        void UpdateTrack(bool force)
        {
            int sub = G.Sub;
            float prog = (float)G.Progress;
            if (force || sub != lastTrackSub || Mathf.Abs(prog - lastTrackProg) > 0.0005f)
            {
                lastTrackSub = sub;
                lastTrackProg = prog;
                float done = (sub - 1) / 5f + prog / 5f;
                Kit.SetSize(trackGold.rectTransform, trackW * done, 5);
                Kit.SetPos(trackGold.rectTransform, 0, 20 - 2.5f);
                for (int i = 0; i < 5; i++) nodeFill[i].color = i < sub ? Kit.Yellow : Kit.CreamD;
            }
            int cur = Mathf.Clamp(sub - 1, 0, 4);
            float px = trackW * cur / 5f + 6f;
            float py = 20f - 6f - Mathf.Abs(Mathf.Sin(tm * 5f)) * 3f;
            Kit.SetPos((RectTransform)trackMiner.transform, px - 13f, py - 13f);
        }

        /// <summary>x3 Oro: listo = late y brilla con "¡GRATIS!"; activo = cuenta regresiva; recarga = apagado.</summary>
        void BoostFx()
        {
            if (!boost.Rt.gameObject.activeSelf)
            {
                if (boostGlow.gameObject.activeSelf) boostGlow.gameObject.SetActive(false);
                return;
            }
            bool ready = !G.BoostActive() && G.Now() >= G.BoostCdUntil;
            bool calm = Tracker != null && Tracker.IsReady;   // un solo foco: si la meta esta lista, x3 no late
            float strength;
            if (ready && !calm)
            {
                strength = 1f;
                if (CanPulse(boost.B)) boost.Rt.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(tm * 6f));
            }
            else
            {
                strength = ready ? 0.5f : 0f;
                if (CanPulse(boost.B)) boost.Rt.localScale = Vector3.one;
            }
            bool glowOn = strength > 0.01f;
            if (boostGlow.gameObject.activeSelf != glowOn) boostGlow.gameObject.SetActive(glowOn);
            if (glowOn)
            {
                float k = 0.75f + 0.25f * Mathf.Sin(tm * 4f);
                float grow = 4f * Mathf.Sin(tm * 4f);
                RectTransform g = boostGlow.rectTransform;
                g.anchorMin = g.anchorMax = boost.Rt.anchorMin;
                g.pivot = new Vector2(0.5f, 0.5f);
                g.sizeDelta = new Vector2(86 + 52 + grow * 2f, 82 + 52 + grow * 2f);
                g.anchoredPosition = boost.Rt.anchoredPosition;
                boostGlow.color = new Color(1f, 0.69f, 0.19f, 0.7f * strength * k);
            }
            Color dim = (ready || G.BoostActive()) ? Color.white : new Color(0.62f, 0.6f, 0.6f, 1f);
            if (boost.B.Modulate != dim)
            {
                boost.B.Modulate = dim;
                boost.B.Restyle();
                boost.Icon.Img.color = dim;
            }
        }

        float pulseHold;

        /// <summary>No pisar la escala mientras hay un rebote de pulsacion o una revelacion en curso.</summary>
        bool CanPulse(Btn b) { return !b.IsPressed && Time.unscaledTime >= b.QuietUntil && Time.unscaledTime >= pulseHold; }

        void PlayBreath()
        {
            if (!play.Rt.gameObject.activeSelf || !CanPulse(play.B)) return;
            bool calm = Tracker != null && Tracker.IsReady;
            float s = calm ? 1f : 1f + 0.03f * Mathf.Sin(tm * 2.4f);
            play.Rt.localScale = new Vector3(s, s, 1f);
        }

        void RefreshTimers()
        {
            if (G.BoostActive())
            {
                boostLabel.text = "x3  " + BigNum.TimeHms(G.BoostUntil - G.Now());
                boostLabel.color = Kit.Yellow;
                boostLabel.fontSize = 24;
            }
            else if (G.Now() < G.BoostCdUntil)
            {
                boostLabel.text = BigNum.TimeHms(G.BoostCdUntil - G.Now());
                boostLabel.color = Icons.H("d8d0c0");
                boostLabel.fontSize = 20;
            }
            else
            {
                boostLabel.text = "¡GRATIS!";
                boostLabel.color = Kit.LimeText;
                boostLabel.fontSize = 28;
            }
            bool on = G.NeedBoss && Mathf.Repeat(Time.unscaledTime, 1f) < 0.75f;
            if (on != bossBlinkOn || bossBanner.gameObject.activeSelf != on)
            {
                bossBlinkOn = on;
                bossBanner.gameObject.SetActive(on);
            }
        }

        // ================================================================ refresco de valores (sin recrear nodos)
        public void Refresh()
        {
            if (gm == null || root == null) return;
            float w = Mathf.Max(progBar.rect.width, 20f);
            bool showFill = G.Progress > 0.01;
            if (progFill.gameObject.activeSelf != showFill) progFill.gameObject.SetActive(showFill);
            Kit.SetSize(progFill.rectTransform, Mathf.Max(18f, w * (float)G.Progress), 18);
            Kit.SetPos(progFill.rectTransform, 0, 0);
            int pct = (int)(G.Progress * 100.0);
            if (pct != lastPct)
            {
                lastPct = pct;
                progLabel.text = pct + "%";
            }
            string sn = G.StageName();
            if (stageLabel.text != sn) stageLabel.text = sn;
            int gemTxt = Mathf.Max(G.Gems - flyPending, 0);
            if (gemTxt != lastGemText)
            {
                lastGemText = gemTxt;
                gemPill.Label.text = gemTxt.ToString();
            }
            UpdateTrack(true);
            // puntos rojos: solo si hay algo para reclamar YA
            missionBadge.SetCount(G.MissionsReadyCount());
            trophyBadge.SetCount(G.AchievementsReadyCount());
            dailyBadge.SetCount(G.LoginAvailable() ? 1 : 0);
            if (G.Piggy != lastPiggy)
            {
                lastPiggy = G.Piggy;
                piggyLabel.text = G.Piggy.ToString();
                piggyLabel.gameObject.SetActive(G.Piggy > 0);
            }
            piggyBadge.SetCount(G.Piggy >= Balance.PiggyCap ? 1 : 0);
            if (G.AutoUp != lastAuto)
            {
                lastAuto = G.AutoUp;
                auto.Label.text = G.AutoUp ? "Auto ON" : "Auto";
                auto.Label.color = G.AutoUp ? Kit.LimeText : Color.white;
            }
            Color rd = G.CanRebirth() ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f);
            if (rebirth.B.Modulate != rd)
            {
                rebirth.B.Modulate = rd;
                rebirth.B.Restyle();
                rebirth.Icon.Img.color = rd;
            }
            SetCard("power", G.PowerValue());
            SetCard("speed", G.SpeedValue());
            SetCard("money", G.RockGold());
            ApplyVisibility();
        }

        void SetCard(string kind, double v)
        {
            Card c = cards[kind];
            int lv = G.Lv[kind];
            if (lv != c.LastLv)
            {
                c.LastLv = lv;
                c.Lvl.text = "Lv." + lv;
                bool maxSpeed = kind == "speed" && lv >= Balance.SpeedCap;
                c.Cost.text = maxSpeed ? "MAX" : BigNum.Fmt(G.Cost(kind));
            }
            if (v != c.LastVal)
            {
                c.LastVal = v;
                c.Val.text = kind == "speed" ? ((int)v).ToString() : BigNum.Fmt(v);
            }
            int ok = G.CanBuy(kind) ? 1 : 0;
            if (ok != c.LastOk)
            {
                c.LastOk = ok;
                c.Cost.color = ok == 1 ? Color.white : Icons.H("ff8a7a");
                if (ok == 1)
                {
                    c.CostBg.sprite = Kit.Tex9("btn_yellow", new Vector4(12, 12, 12, 16));
                    c.CostBg.color = Color.white;
                }
                else
                {
                    c.CostBg.sprite = Kit.Tex9("btn_disabled", new Vector4(12, 12, 12, 16));
                    c.CostBg.color = new Color(0.62f, 0.6f, 0.62f, 1f);
                }
            }
            // proximo hito: "Nv 18 » 25: x2" con barrita
            int nxt = G.NextMilestone(kind);
            bool msOn = nxt > 0;
            if (c.Ms.Root.gameObject.activeSelf != msOn) c.Ms.Root.gameObject.SetActive(msOn);
            RectTransform vr = c.Val.rectTransform;
            if (msOn)
            {
                int prev = G.MilestoneLevel(G.MilestoneCount(lv));
                string ms = kind == "speed" ? "x1.25" : "x2";
                c.Ms.Set((float)(lv - prev) / Mathf.Max(nxt - prev, 1), "Nv " + lv + " » " + nxt + ": " + ms);
                vr.sizeDelta = new Vector2(208, 46);
                Kit.SetPos(vr, 8, 38);
            }
            else
            {
                vr.sizeDelta = new Vector2(208, 56);
                Kit.SetPos(vr, 8, 46);
            }
        }

        // ================================================================ gemas y monedas (contadores)
        /// <summary>Una gema (o varias) va camino a la pildora: la cifra mostrada espera a que lleguen.</summary>
        public void GemsInFlight(int n)
        {
            flyPending += n;
            flyTimeout = 3.5f;
            Refresh();
        }

        public void GemsArrived(int n)
        {
            flyPending = Mathf.Max(flyPending - n, 0);
            int t = Mathf.Max(G.Gems - flyPending, 0);
            lastGemText = t;
            gemPill.Label.text = t.ToString();
            Tw.Pop(gemPill.Root);
            Sfx.Play("gem", -10f, 1.4f);
        }

        public Vector2 GoldTarget() { return Kit.ToCanvas(goldPill.Root).position + new Vector2(10, 22); }
        public Vector2 GemTarget() { return Kit.ToCanvas(gemPill.Root).position + new Vector2(10, 22); }
        public void BumpGold() { Tw.Pop(goldPill.Root); }
    }
}
