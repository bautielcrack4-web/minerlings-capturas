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
    /// Mano de caricatura (Icons.Hand, contorno OUT) que hace "tap" con un anillo (tutorial_hand.gd).
    /// Uso: PointAt(() => rect_en_canvas, "¡Tocá para mejorar!"); Stop().
    /// </summary>
    public sealed class TutorialHand : MonoBehaviour
    {
        static Sprite ringSprite, ringOutSprite;

        Func<Rect> getter;
        string text = "";
        Image hand, ring, ringOut, dot;
        Text label;
        float alpha, t, pop;
        bool want;

        public bool IsActive { get { return want; } }

        public static TutorialHand Create(Transform parent)
        {
            RectTransform rt = Kit.New("TutorialHand", parent);
            Kit.Stretch(rt);
            TutorialHand h = rt.gameObject.AddComponent<TutorialHand>();
            h.Build();
            return h;
        }

        static Sprite MakeRing(float width)
        {
            Texture2D tex = Icons.RingTex(width);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 400f);
        }

        void Build()
        {
            if (ringSprite == null) { ringSprite = MakeRing(8f); ringOutSprite = MakeRing(14f); }
            ringOut = Kit.Img(transform, ringOutSprite, Kit.Out, "RingOut");
            ring = Kit.Img(transform, ringSprite, Color.white, "Ring");
            dot = Kit.Img(transform, Icons.Dot(), Color.white, "Dot");
            hand = Kit.Img(transform, Icons.Hand(), Color.white, "Hand");
            foreach (Image im in new[] { ringOut, ring, dot, hand })
            {
                RectTransform r = im.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0.5f, 0.5f);
            }
            RectTransform hr = hand.rectTransform;
            hr.pivot = new Vector2(Icons.HandTip.x / Icons.HandW, 1f - Icons.HandTip.y / Icons.HandH);
            hr.sizeDelta = new Vector2(Icons.HandW, Icons.HandH);
            label = Kit.Label(transform, "", 30, Color.white, 9, true, TextAnchor.MiddleCenter, "Hint");
            RectTransform lr = label.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0, 1);
            lr.sizeDelta = new Vector2(520, 44);
            SetVisible(false);
        }

        public void PointAt(Func<Rect> g, string txt = "")
        {
            getter = g;
            text = txt;
            label.text = txt;
            if (!want)
            {
                pop = 0f;
                t = 0f;
            }
            want = true;
        }

        public void Stop() { want = false; }

        void SetVisible(bool v)
        {
            if (hand.gameObject.activeSelf == v) return;
            hand.gameObject.SetActive(v);
            ring.gameObject.SetActive(v);
            ringOut.gameObject.SetActive(v);
            dot.gameObject.SetActive(v);
            label.gameObject.SetActive(v && text != "");
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, dt * (want ? 5f : 8f));
            bool vis = alpha > 0.01f;
            SetVisible(vis);
            if (!vis) return;
            t += dt;
            pop = Mathf.Min(pop + dt * 4f, 1f);
            Draw();
        }

        struct Spot
        {
            public Vector2 Tip, Dir;
            public float Rot;
        }

        /// <summary>Orientacion segun la zona: tarjetas de abajo = mano desde el costado; columnas = mano desde el costado.</summary>
        Spot GetSpot()
        {
            Vector2 vw = Kit.CanvasSize;
            Spot s = new Spot();
            if (getter == null)
            {
                s.Tip = new Vector2(-999, -999);
                s.Dir = new Vector2(0, -1);
                return s;
            }
            Rect r = getter();
            Vector2 c = r.center;
            if (c.y > vw.y - 300f)
            {
                // tarjetas de abajo: señala el boton de precio desde el costado para no tapar el titulo
                float py = r.yMax - 30f;
                if (c.x > vw.x * 0.66f)
                {
                    s.Tip = new Vector2(r.xMin + 40f, py);
                    s.Rot = Mathf.PI * 0.5f + 0.2f;
                    s.Dir = new Vector2(1, 0);
                }
                else
                {
                    s.Tip = new Vector2(r.xMax - 40f, py);
                    s.Rot = -Mathf.PI * 0.5f - 0.2f;
                    s.Dir = new Vector2(-1, 0);
                }
                return s;
            }
            if (c.x < 150f)
            {
                s.Tip = new Vector2(r.xMax - 24f, c.y);
                s.Rot = -Mathf.PI * 0.5f - 0.2f;
                s.Dir = new Vector2(-1, 0);
                return s;
            }
            if (c.x > vw.x - 150f)
            {
                s.Tip = new Vector2(r.xMin + 24f, c.y);
                s.Rot = Mathf.PI * 0.5f + 0.2f;
                s.Dir = new Vector2(1, 0);
                return s;
            }
            s.Tip = c + new Vector2(8, 12);
            s.Rot = -0.28f;
            s.Dir = new Vector2(0, -1);
            return s;
        }

        static void Put(RectTransform rt, Vector2 p) { rt.anchoredPosition = new Vector2(p.x, -p.y); }

        void Draw()
        {
            Spot sp = GetSpot();
            Vector2 vw = Kit.CanvasSize;
            float cyc = Mathf.Repeat(t, 1.3f);
            // 0..0.3 avanza, 0.3..0.5 sostiene, 0.5..0.8 vuelve; el resto espera
            float press = 0f;
            if (cyc < 0.3f) press = cyc / 0.3f;
            else if (cyc < 0.5f) press = 1f;
            else if (cyc < 0.8f) press = 1f - (cyc - 0.5f) / 0.3f;
            press = press * press * (3f - 2f * press);
            float easePop = 1f + 0.9f * (1f - pop) * (1f - pop);
            float sc = 0.95f * easePop;
            Vector2 bob = sp.Dir * (14f * press - 12f);
            // anillo del toque
            bool ringOn = cyc >= 0.3f && cyc < 1.1f;
            ring.gameObject.SetActive(ringOn);
            ringOut.gameObject.SetActive(ringOn);
            if (ringOn)
            {
                float u = (cyc - 0.3f) / 0.8f;
                float r = Mathf.Lerp(10f, 50f, 1f - Mathf.Pow(1f - u, 2f));
                float a = (1f - u) * alpha;
                float sz = (r / 56f) * 128f;
                float so = ((r + 3f) / 56f) * 128f;
                ring.rectTransform.sizeDelta = new Vector2(sz, sz);
                ringOut.rectTransform.sizeDelta = new Vector2(so, so);
                Put(ring.rectTransform, sp.Tip);
                Put(ringOut.rectTransform, sp.Tip);
                ring.color = new Color(1, 1, 1, a);
                ringOut.color = new Color(Kit.Out.r, Kit.Out.g, Kit.Out.b, a * 0.8f);
            }
            float dr = 7f * (1f - 0.5f * press) * 2f;
            dot.rectTransform.sizeDelta = new Vector2(dr, dr);
            Put(dot.rectTransform, sp.Tip);
            dot.color = new Color(1, 1, 1, 0.55f * alpha * press);
            RectTransform hr = hand.rectTransform;
            Put(hr, sp.Tip + bob);
            hr.localScale = new Vector3(sc, sc, 1f);
            hr.localRotation = Quaternion.Euler(0, 0, -sp.Rot * Mathf.Rad2Deg);
            hand.color = new Color(1, 1, 1, alpha);
            if (text != "")
            {
                float py = sp.Tip.y - 120f;
                if (sp.Dir.y > 0.5f) py = sp.Tip.y - 205f;
                else if (sp.Dir.y < -0.5f) py = sp.Tip.y + 150f;
                if (sp.Tip.y > vw.y - 320f) py = sp.Tip.y - 205f;
                RectTransform lr = label.rectTransform;
                lr.anchoredPosition = new Vector2(Mathf.Clamp(sp.Tip.x - 260f, 6f, vw.x - 526f) + 260f, -(py + 22f));
                Color c = label.color;
                c.a = alpha;
                label.color = c;
                Outline[] os = label.GetComponents<Outline>();
                for (int i = 0; i < os.Length; i++) os[i].effectColor = new Color(Kit.Out.r, Kit.Out.g, Kit.Out.b, alpha);
            }
        }
    }

    /// <summary>Cola de celebraciones (un solo foco por vez) y el cartel central de hitos (celebration.gd).</summary>
    public sealed class Celebration : MonoBehaviour
    {
        /// <summary>Devuelve false mientras la pantalla no esta libre (panel abierto, modo Excavar...).</summary>
        public Func<bool> Gate;
        public bool Busy { get; private set; }
        readonly Queue<Func<IEnumerator>> q = new Queue<Func<IEnumerator>>();
        RectTransform fxParent;

        public static Celebration Create(Transform parent)
        {
            RectTransform rt = Kit.New("Celebration", parent);
            Kit.Stretch(rt);
            Celebration c = rt.gameObject.AddComponent<Celebration>();
            c.fxParent = rt;
            return c;
        }

        public void Enqueue(Func<IEnumerator> job)
        {
            q.Enqueue(job);
            if (!Busy) StartCoroutine(Pump());
        }

        public int Pending { get { return q.Count + (Busy ? 1 : 0); } }

        IEnumerator Pump()
        {
            Busy = true;
            while (q.Count > 0)
            {
                while (Gate != null && !Gate()) yield return null;
                Func<IEnumerator> j = q.Dequeue();
                yield return StartCoroutine(j());
                yield return new WaitForSecondsRealtime(0.12f);
            }
            Busy = false;
        }

        /// <summary>Cartel central con cinta de color, icono y estrellas. Dura 0.25 + hold + 0.12 s.</summary>
        public IEnumerator Cartel(string title, string sub, string iconKind, Color col, float hold = 0.95f)
        {
            Vector2 vw = Kit.CanvasSize;
            RectTransform root = Kit.New("Cartel", transform);
            Kit.PlaceTL(root, (vw.x - 600f) * 0.5f, vw.y * 0.40f - 80f, 600, 160);
            CanvasGroup g = root.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.interactable = false;
            string pre = Kit.KCol(col);
            Image plate = Kit.Box9(root, "btn_" + pre, new Vector4(12, 12, 12, 16), pre == "grey" ? col : Color.white, "Plate");
            Kit.PlaceTL(plate.rectTransform, 0, 34, 600, 126);
            IconView ic = Kit.Icon(root, iconKind, 84);
            Kit.PlaceTL((RectTransform)ic.transform, 16, 56, 84, 84);
            Kit.LabelAt(root, title, 46, Color.white, 11, true, 104, 44, 480, 58, TextAnchor.MiddleCenter);
            if (sub != "") Kit.LabelAt(root, sub, 32, Icons.H("ffe27a"), 9, true, 104, 100, 480, 44, TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                float sz = i == 1 ? 50 : 40;
                IconView st = Kit.Icon(root, "star", sz);
                Kit.PlaceTL((RectTransform)st.transform, 250 + i * 52 - (i == 1 ? 4 : 0), i == 1 ? 0 : 8, sz, sz);
                st.transform.localRotation = Quaternion.Euler(0, 0, -(i - 1) * 0.28f * Mathf.Rad2Deg);
                Tw.Reveal(st.transform, 0.12f + i * 0.1f);
            }
            root.localScale = new Vector3(0.6f, 0.6f, 1f);
            g.alpha = 0f;
            Tw.Scale(root, Vector3.one * 0.6f, Vector3.one, 0.25f, Ease.OutBack);
            Tw.Alpha(g, 1f, 0.12f);
            BurstFx.Spawn(fxParent, new Vector2(vw.x * 0.5f, vw.y * 0.40f + 10f), Color.Lerp(col, Color.white, 0.5f), 170f, 12);
            yield return new WaitForSecondsRealtime(0.25f + hold);
            Tw.Scale(root, Vector3.one * 0.92f, 0.12f, Ease.Linear);
            Tw.Alpha(g, 0f, 0.12f);
            yield return new WaitForSecondsRealtime(0.13f);
            if (root != null) Destroy(root.gameObject);
        }
    }

    /// <summary>
    /// Director de la primera experiencia: cola de focos (desbloqueos, hitos, etapas), mano tutorial y pistas (director.gd).
    /// Un solo foco por vez. La mano se guarda en G.FeaturesSeen con ids "tut_power" y "tut_goal" (no repite nunca).
    /// </summary>
    public sealed class UiDirector : MonoBehaviour
    {
        static string Hint(string f)
        {
            switch (f)
            {
                case "speed": return "¡Más velocidad!";
                case "money": return "¡Más oro!";
                case "boost": return "¡Oro x3 gratis!";
                case "tools": return "¡Nuevo equipo!";
                case "missions": return "¡Misiones!";
                case "play": return "¡A excavar!";
                case "shop": return "¡La tienda!";
                case "skin": return "¡Cascos nuevos!";
                case "piggy": return "¡Tu alcancía!";
                case "auto": return "¡Auto-mejora!";
                case "rebirth": return "¡Renacer!";
                case "achievements": return "¡Tus logros!";
            }
            return "";
        }

        GameManager gm;
        HudView hud;
        RectTransform panels;
        Func<string> modeGetter;
        public TutorialHand Hand { get; private set; }
        public Celebration Celeb { get; private set; }
        string owner = "";      // quien maneja la mano: "unlock" | "hint" | ""
        bool started;

        GameState G { get { return gm.G; } }

        public static UiDirector Create(Transform parent, GameManager manager, HudView h, RectTransform panelsLayer, RectTransform fxLayer,
            Func<string> mode)
        {
            RectTransform rt = Kit.New("Director", parent);
            UiDirector d = rt.gameObject.AddComponent<UiDirector>();
            d.Setup(manager, h, panelsLayer, fxLayer, mode);
            return d;
        }

        void Setup(GameManager manager, HudView h, RectTransform panelsLayer, RectTransform fxLayer, Func<string> mode)
        {
            gm = manager;
            hud = h;
            panels = panelsLayer;
            modeGetter = mode;
            Celeb = Celebration.Create(fxLayer);
            Celeb.Gate = ScreenFree;
            Hand = TutorialHand.Create(fxLayer);
            G.FeatureUnlocked += OnUnlocked;
            G.MilestoneReached += OnMilestone;
            // partidas viejas: no repetir el tutorial de lo que ya hicieron
            if (G.LvBest["power"] > 1 || G.Stat("rocks") > 30) G.MarkFeatureSeen("tut_power");
            if (G.Stat("goals") > 0) G.MarkFeatureSeen("tut_goal");
        }

        void OnDestroy()
        {
            if (gm == null || gm.G == null) return;
            G.FeatureUnlocked -= OnUnlocked;
            G.MilestoneReached -= OnMilestone;
        }

        void OnUnlocked(string f)
        {
            string ff = f;
            Celeb.Enqueue(() => UnlockJob(ff));
        }

        void OnMilestone(string k, int lvl)
        {
            string kk = k;
            int ll = lvl;
            Celeb.Enqueue(() => MilestoneJob(kk, ll));
        }

        /// <summary>Llamar cuando ya pasaron los carteles de arranque (bienvenida, premio diario).</summary>
        public void StartDirector()
        {
            started = true;
            List<string> list = G.NewlyUnlocked();
            for (int i = 0; i < list.Count; i++)
            {
                string f = list[i];
                Celeb.Enqueue(() => UnlockJob(f));
            }
        }

        bool ScreenFree()
        {
            return started && panels.childCount == 0 && modeGetter() == "idle";
        }

        void Update()
        {
            if (!started) return;
            bool free = ScreenFree();
            if (owner == "unlock") return;
            if (!free || Celeb.Busy)
            {
                if (owner == "hint")
                {
                    Hand.Stop();
                    owner = "";
                }
                return;
            }
            Hints();
        }

        void Hints()
        {
            // 1) primera compra de Fuerza
            if (!G.FeaturesSeen.Contains("tut_power"))
            {
                if (G.Lv["power"] > 1)
                {
                    G.MarkFeatureSeen("tut_power");
                    StopHint();
                }
                else if (G.CanBuy("power"))
                    PointHint(() => hud.CardRect("power"), "¡Tocá para mejorar!");
                return;
            }
            // 2) primera meta lista
            if (!G.FeaturesSeen.Contains("tut_goal"))
            {
                if (hud.Tracker.IsReady) PointHint(() => hud.TrackerRewardRect(), "¡Tocá para reclamar!");
                else if (owner == "hint") StopHint();
                if (G.Stat("goals") > 0)
                {
                    G.MarkFeatureSeen("tut_goal");
                    StopHint();
                }
                return;
            }
            StopHint();
        }

        void PointHint(Func<Rect> getter, string text)
        {
            if (owner == "hint") return;
            owner = "hint";
            Hand.PointAt(getter, text);
        }

        void StopHint()
        {
            if (owner != "hint") return;
            Hand.Stop();
            owner = "";
        }

        // ---------------------------------------------------------------- focos de la cola
        IEnumerator UnlockJob(string f)
        {
            StopHint();
            yield return StartCoroutine(hud.Reveal(f));
            owner = "unlock";
            string ff = f;
            Hand.PointAt(() => hud.TargetRect(ff), Hint(f));
            float t = 0f;
            while (hud.HasNewTag(f) && t < 4f && panels.childCount == 0 && modeGetter() == "idle")
            {
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            Hand.Stop();
            owner = "";
        }

        IEnumerator MilestoneJob(string kind, int level)
        {
            StopHint();
            string title, sub, icon;
            Color col;
            switch (kind)
            {
                case "speed": title = "Velocidad"; sub = "x1.25 velocidad"; icon = "speed"; col = Kit.Orange; break;
                case "money": title = "Oro"; sub = "x2 oro"; icon = "money"; col = Kit.Green; break;
                default: title = "Fuerza"; sub = "x2 daño"; icon = "power"; col = Kit.Red; break;
            }
            hud.BounceCard(kind);
            Sfx.Play("milestone", -2f);
            yield return StartCoroutine(Celeb.Cartel("¡" + title + " nivel " + level + "!", sub, icon, col));
        }
    }
}
