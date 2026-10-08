using System;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>
    /// Franja fija con la meta actual: icono, texto corto, barra y recompensa en gemas.
    /// Al cumplirse late y dice "¡Reclamar!"; al tocarla reclama y entra la siguiente meta deslizando (goal_tracker.gd).
    /// </summary>
    public sealed class GoalTracker : MonoBehaviour
    {
        public const float W = 540f, H = 66f;

        /// <summary>Se va a reclamar: `gems` van a volar hasta la pildora de gemas.</summary>
        public event Action<int> WillClaim;
        public event Action<int, Vector2> Claimed;

        GameManager gm;
        Btn btn;
        RectTransform content;
        IconView goalIcon, gemIcon;
        Text textL, gemL;
        Capsule cap;
        Image glow;
        Badge badge;
        string id = "";
        long progress = -1, target = -1;
        bool done, wasDone;
        float t;

        public bool IsReady { get { return done; } }

        public static GoalTracker Create(Transform parent, GameManager manager)
        {
            RectTransform root = Kit.New("GoalTracker", parent);
            root.sizeDelta = new Vector2(W, H);
            GoalTracker g = root.gameObject.AddComponent<GoalTracker>();
            g.Build(manager);
            return g;
        }

        void Build(GameManager manager)
        {
            gm = manager;
            // resplandor detras (hermano anterior al boton)
            glow = Kit.Glow(transform, Kit.Yellow, "Glow");
            Kit.PlaceTL(glow.rectTransform, -30, -26, W + 60, H + 52);
            glow.gameObject.SetActive(false);

            btn = Kit.HitArea(transform, W, H, "Button");
            Kit.PlaceTL(btn.GetComponent<RectTransform>(), 0, 0, W, H);
            Image bg = btn.GetComponent<Image>();
            bg.color = Color.white;
            bg.type = Image.Type.Sliced;
            BtnSkin sk = new BtnSkin();
            sk.Normal = sk.Pressed = Kit.Tex9("card", new Vector4(12, 12, 12, 16));
            sk.Disabled = sk.Normal;
            sk.DisabledTint = Color.white;
            sk.UsePressedTint = true;
            sk.PressedTint = new Color(0.92f, 0.92f, 0.95f, 1f);
            btn.Bg = bg;
            btn.SetSkin(sk);

            RectTransform clip = Kit.New("Clip", btn.transform);
            Kit.Stretch(clip);
            clip.gameObject.AddComponent<RectMask2D>();
            content = Kit.New("Content", clip);
            content.anchorMin = content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(W, H);
            content.anchoredPosition = new Vector2(W * 0.5f, -H * 0.5f);

            goalIcon = Kit.Icon(content, "star", 46);
            Kit.PlaceTL((RectTransform)goalIcon.transform, 10, 9, 46, 46);
            textL = Kit.LabelAt(content, "", 22, Kit.Brown, 0, false, 64, 4, 350, 30);
            textL.horizontalOverflow = HorizontalWrapMode.Wrap;
            textL.verticalOverflow = VerticalWrapMode.Truncate;
            cap = Kit.MakeCapsule(content, 352, 20, "green_border");
            Kit.PlaceTL(cap.Root, 64, 35, 352, 20);
            gemIcon = Kit.Icon(content, "gem", 44);
            Kit.PlaceTL((RectTransform)gemIcon.transform, 428, 8, 44, 44);
            gemL = Kit.LabelAt(content, "+5", 30, Color.white, 8, true, 466, 10, 66, 44, TextAnchor.MiddleLeft);

            badge = Kit.MakeBadge(transform);
            Kit.PlaceTL(badge.Root, -4, -14, 26, 26);

            btn.Clicked += OnPressed;
            gm.G.Changed += Refresh;
            gm.G.StatsChanged += Refresh;
            gm.G.GoalCompleted += OnGoalCompleted;
            Refresh();
        }

        void OnDestroy()
        {
            if (gm == null) return;
            gm.G.Changed -= Refresh;
            gm.G.StatsChanged -= Refresh;
            gm.G.GoalCompleted -= OnGoalCompleted;
        }

        void OnGoalCompleted(Goal g) { Refresh(); }

        public void Refresh()
        {
            if (gm == null || !isActiveAndEnabled && content == null) return;
            Goal g = gm.G.CurrentGoal();
            if (g.Id != id)
            {
                bool slide = id != "" && wasDone;
                id = g.Id;
                progress = -1;
                textL.text = g.Text;
                goalIcon.SetKind(g.Icon);
                gemL.text = "+" + g.Gems;
                if (slide)
                {
                    content.anchoredPosition = new Vector2(W * 1.5f, -H * 0.5f);
                    Tw.Move(content, new Vector2(W * 0.5f, -H * 0.5f), 0.3f, Ease.OutBack);
                }
            }
            if (g.Progress != progress || g.Target != target || g.Done != done)
            {
                progress = g.Progress;
                target = g.Target;
                done = g.Done;
                float frac = (float)progress / Mathf.Max((float)target, 1f);
                if (done)
                {
                    cap.SetFillKind("white_border", new Color(1f, 0.86f, 0.3f, 1f));
                    cap.Set(1f, "¡Reclamar!");
                    cap.Label.fontSize = 20;
                }
                else
                {
                    cap.SetFillKind("green_border", Color.white);
                    cap.Set(frac, BigNum.Fmt(progress) + " / " + BigNum.Fmt(target));
                    cap.Label.fontSize = 16;
                }
                if (done && !wasDone)
                {
                    Sfx.Play("goal", -4f, 1.2f);
                    Tw.Pop(btn.transform, 1.08f);
                }
                wasDone = done;
                badge.SetCount(done ? 1 : 0);
            }
        }

        void OnPressed()
        {
            if (!done) return;
            Rect r = Kit.ToCanvas((RectTransform)transform);
            Vector2 from = r.center + new Vector2(150f, 0f);
            int gems = gm.G.CurrentGoal().Gems;
            if (WillClaim != null) WillClaim(gems);
            int n = gm.G.ClaimGoal();
            if (n > 0)
            {
                Sfx.Play("goal", -2f);
                Kit.Buzz(30);
                if (Claimed != null) Claimed(n, from);
            }
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            bool on = done;
            if (glow.gameObject.activeSelf != on) glow.gameObject.SetActive(on);
            if (on)
            {
                float k = 0.75f + 0.25f * Mathf.Sin(t * 7f);
                float grow = 4f * Mathf.Sin(t * 7f);
                Kit.PlaceTL(glow.rectTransform, -30 - grow, -26 - grow, W + 60 + grow * 2f, H + 52 + grow * 2f);
                glow.color = new Color(Kit.Yellow.r, Kit.Yellow.g, Kit.Yellow.b, 0.7f * k);
                if (!btn.IsPressed && Time.unscaledTime >= btn.QuietUntil) btn.transform.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(t * 7f));
                float ip = 1f + 0.12f * Mathf.Abs(Mathf.Sin(t * 6f));
                goalIcon.transform.localScale = new Vector3(ip, ip, 1f);
            }
            else if (goalIcon.transform.localScale.x != 1f)
            {
                goalIcon.transform.localScale = Vector3.one;
            }
        }
    }

    /// <summary>Cinta superior del evento activo: icono, nombre y barra de tiempo que se vacia (event_ribbon.gd).</summary>
    public sealed class EventRibbon : MonoBehaviour
    {
        const float W = 540f, H = 84f, ShownY = 246f, HiddenY = -120f;

        sealed class Info
        {
            public string Name, Sub, Icon;
            public Color Col;
            public Info(string n, string s, string i, string c) { Name = n; Sub = s; Icon = i; Col = Icons.H(c); }
        }

        GameManager gm;
        RectTransform rt;
        Image plate;
        IconView icon;
        Text nameL, subL;
        Capsule cap;
        string kind = "";
        float total = 1f;
        float curY = HiddenY;

        static Info Of(string k)
        {
            switch (k)
            {
                case "gold_rush": return new Info("¡Fiebre de Oro!", "x3 oro", "coin", "f59a32");
                case "frenzy": return new Info("¡Frenesí!", "x2 velocidad", "speed", "4aa3f0");
                case "meteor": return new Info("¡Lluvia de Meteoritos!", "", "power", "e5484d");
                case "chest": return new Info("¡Roca Cofre!", "¡Rompela!", "chest", "5cc84a");
            }
            return null;
        }

        public static EventRibbon Create(Transform parent, GameManager manager)
        {
            RectTransform root = Kit.New("EventRibbon", parent);
            EventRibbon e = root.gameObject.AddComponent<EventRibbon>();
            e.Build(manager);
            return e;
        }

        void Build(GameManager manager)
        {
            gm = manager;
            rt = (RectTransform)transform;
            Kit.PlaceTL(rt, 10, HiddenY, W, H);
            plate = Kit.Box9(rt, "btn_blue", new Vector4(12, 12, 12, 16), Color.white, "Plate");
            Kit.Stretch(plate.rectTransform);
            icon = Kit.Icon(rt, "coin", 56);
            Kit.PlaceTL((RectTransform)icon.transform, 12, 8, 56, 56);
            nameL = Kit.LabelAt(rt, "", 30, Color.white, 8, true, 76, 4, 300, 38);
            subL = Kit.LabelAt(rt, "", 24, Icons.H("ffe27a"), 7, true, 346, 6, 180, 36, TextAnchor.UpperRight);
            cap = Kit.MakeCapsule(rt, 448, 18, "white_border", new Color(1f, 0.95f, 0.7f, 1f), new Color(0.2f, 0.15f, 0.1f, 0.9f), 13);
            Kit.PlaceTL(cap.Root, 76, 48, 448, 18);
            gameObject.SetActive(false);
            gm.G.EventStarted += OnStarted;
            gm.G.EventEnded += OnEnded;
            if (gm.G.ActiveEvent() != "") OnStarted(gm.G.ActiveEvent(), Math.Max(gm.G.EventTimeLeft(), 1.0));
        }

        void OnDestroy()
        {
            if (gm == null) return;
            gm.G.EventStarted -= OnStarted;
            gm.G.EventEnded -= OnEnded;
        }

        void SetY(float y) { curY = y; Kit.SetPos(rt, 10, y); }

        void OnStarted(string k, double dur)
        {
            Info inf = Of(k);
            if (inf == null) return;
            kind = k;
            total = Mathf.Max((float)dur, 0.1f);
            string pre = Kit.KCol(inf.Col);
            plate.sprite = Kit.Tex9("btn_" + pre, new Vector4(12, 12, 12, 16));
            plate.color = pre == "grey" ? inf.Col : Color.white;
            icon.SetKind(inf.Icon);
            nameL.text = inf.Name;
            subL.text = inf.Sub;
            gameObject.SetActive(true);
            float from = curY;
            Tw.To(this, "y", 0.25f, Ease.OutBack, u => SetY(Mathf.LerpUnclamped(from, ShownY, u)));
            Sfx.Play("event_start", -4f, 1.1f);
        }

        void OnEnded(string k)
        {
            kind = "";
            float from = curY;
            Tw.To(this, "y", 0.12f, Ease.Linear, u => SetY(Mathf.Lerp(from, HiddenY, u)), () => gameObject.SetActive(false));
        }

        void Update()
        {
            if (kind == "") return;
            float left = (float)gm.G.EventTimeLeft();
            int secs = Mathf.CeilToInt(left);
            cap.Set(left / total, secs + "s");
            if (left < 4f)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 15f);
                cap.Fill.color = Color.Lerp(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.55f, 0.45f), k);
            }
            else cap.Fill.color = new Color(1f, 0.95f, 0.7f, 1f);
            icon.transform.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(Time.unscaledTime * 8f) * 0.12f * Mathf.Rad2Deg);
        }
    }
}
