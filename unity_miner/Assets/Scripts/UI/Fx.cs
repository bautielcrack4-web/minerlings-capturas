using System;
using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>Pool simple de imagenes (anclas arriba-izquierda, pivote al centro) para particulas de pantalla.</summary>
    public sealed class ImgPool
    {
        readonly Transform parent;
        readonly Sprite sprite;
        readonly float size;
        readonly Stack<Image> free = new Stack<Image>();

        public ImgPool(Transform parent, Sprite sprite, float size)
        {
            this.parent = parent;
            this.sprite = sprite;
            this.size = size;
        }

        public Image Get()
        {
            Image im;
            if (free.Count > 0)
            {
                im = free.Pop();
                im.gameObject.SetActive(true);
                return im;
            }
            RectTransform rt = Kit.New("p", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite;
            im.raycastTarget = false;
            return im;
        }

        public void Release(Image im)
        {
            im.gameObject.SetActive(false);
            free.Push(im);
        }

        public static void Put(Image im, Vector2 p) { im.rectTransform.anchoredPosition = new Vector2(p.x, -p.y); }
    }

    /// <summary>Monedas y gemas que saltan, rebotan y vuelan a su contador (port de flyer.gd). Coordenadas de canvas (y hacia abajo).</summary>
    public sealed class Flyer : MonoBehaviour
    {
        /// <summary>Una moneda llego: el valor se suma al oro (idle) o es 0 (Excavar).</summary>
        public event Action<double> Arrived;
        /// <summary>Una gema llego (solo visual: Core ya las otorgo).</summary>
        public event Action<int> GemArrived;

        public Vector2 Target = new Vector2(600, 100);
        public Vector2 GemTarget = new Vector2(600, 150);

        sealed class Coin
        {
            public Vector2 P, V, From;
            public float T, Spin, Dur, Floor, Big;
            public int Phase;
            public double Val;
            public Image Img, Glow;
        }

        sealed class GemP
        {
            public Vector2 P, V, From;
            public float T, Spin, Dur, Floor;
            public int Phase;
            public Image Img, Glow;
        }

        sealed class Glint
        {
            public Vector2 P;
            public float T, Life;
            public Image Img;
        }

        readonly List<Coin> coins = new List<Coin>();
        readonly List<GemP> gems = new List<GemP>();
        readonly List<Glint> glints = new List<Glint>();
        ImgPool coinPool, gemPool, glowPool, glintPool;
        GameManager gm;

        public int InFlightCoins { get { return coins.Count; } }

        public void Init(GameManager manager)
        {
            gm = manager;
            RectTransform glowLayer = Kit.New("Glows", transform);
            Kit.Stretch(glowLayer);
            RectTransform main = Kit.New("Items", transform);
            Kit.Stretch(main);
            glowPool = new ImgPool(glowLayer, Icons.Glow(), 64f);
            coinPool = new ImgPool(main, Icons.CoinSprite(), 40f);
            gemPool = new ImgPool(main, Icons.GemSprite(), 40f);
            glintPool = new ImgPool(main, Icons.Spark(), 32f);
        }

        bool Eco { get { return gm != null && gm.G.Eco; } }

        public void Burst(Vector2 from, double value, int n)
        {
            if (Eco) n = Mathf.Max(2, n / 2);
            bool rush = gm != null && gm.G.ActiveEvent() == "gold_rush";
            if (rush) n = Mathf.Min(n * 2, 16);
            if (coins.Count > 90) n = Mathf.Min(n, 3);
            if (n < 1) n = 1;
            for (int i = 0; i < n; i++)
            {
                float a = UnityEngine.Random.Range(-Mathf.PI * 0.85f, -Mathf.PI * 0.15f);
                Coin c = new Coin();
                c.P = from;
                c.V = new Vector2(Mathf.Cos(a) * 0.7f, Mathf.Sin(a)) * UnityEngine.Random.Range(380f, 620f);
                c.T = -UnityEngine.Random.Range(0f, 0.15f);
                c.Phase = 0;
                c.Val = value / n;
                c.Spin = UnityEngine.Random.value * Mathf.PI * 2f;
                c.Dur = UnityEngine.Random.Range(0.45f, 0.65f);
                c.Floor = from.y + UnityEngine.Random.Range(30f, 70f);
                c.Big = rush ? 1.35f : 1f;
                c.Img = coinPool.Get();
                c.Img.enabled = false;
                if (rush)
                {
                    c.Glow = glowPool.Get();
                    c.Glow.color = new Color(1f, 0.85f, 0.35f, 0.3f);
                    c.Glow.rectTransform.sizeDelta = new Vector2(52f, 52f);
                    c.Glow.enabled = false;
                }
                coins.Add(c);
            }
        }

        /// <summary>Gemas que saltan, rebotan y vuelan a GemTarget.</summary>
        public void BurstGems(Vector2 from, int n)
        {
            n = Mathf.Min(n, 24);
            if (Eco) n = Mathf.Max(1, n / 2);
            for (int i = 0; i < n; i++)
            {
                float a = UnityEngine.Random.Range(-Mathf.PI * 0.9f, -Mathf.PI * 0.1f);
                GemP g = new GemP();
                g.P = from;
                g.V = new Vector2(Mathf.Cos(a) * 0.9f, Mathf.Sin(a)) * UnityEngine.Random.Range(420f, 760f);
                g.T = -UnityEngine.Random.Range(0f, 0.2f);
                g.Phase = 0;
                g.Spin = UnityEngine.Random.value * Mathf.PI * 2f;
                g.Dur = UnityEngine.Random.Range(0.5f, 0.7f);
                g.Floor = from.y + UnityEngine.Random.Range(40f, 90f);
                g.Img = gemPool.Get();
                g.Img.enabled = false;
                g.Glow = glowPool.Get();
                g.Glow.color = new Color(0.5f, 0.85f, 1f, 0.35f);
                g.Glow.rectTransform.sizeDelta = new Vector2(52f, 52f);
                g.Glow.enabled = false;
                gems.Add(g);
            }
        }

        static Vector2 Bez(Vector2 from, Vector2 ctrl, Vector2 to, float e)
        {
            return Vector2.Lerp(Vector2.Lerp(from, ctrl, e), Vector2.Lerp(ctrl, to, e), e);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                Coin c = coins[i];
                c.T += dt;
                c.Spin += dt * 12f;
                bool arrived = false;
                if (c.Phase == 0)
                {
                    c.V.x *= Mathf.Pow(0.08f, dt);
                    c.V.y += 1500f * dt;
                    c.P += c.V * dt;
                    if (c.P.y > c.Floor && c.V.y > 0f)
                    {
                        c.P.y = c.Floor;
                        c.V.y *= -0.38f;
                    }
                    if (c.T > 0.55f)
                    {
                        c.Phase = 1;
                        c.T = 0f;
                        c.From = c.P;
                    }
                }
                else
                {
                    float u = Mathf.Clamp01(c.T / c.Dur);
                    float e = u * u;
                    Vector2 ctrl = Vector2.Lerp(c.From, Target, 0.5f) + new Vector2(-120f, 60f);
                    c.P = Bez(c.From, ctrl, Target, e);
                    if (u >= 1f) arrived = true;
                }
                if (arrived)
                {
                    coinPool.Release(c.Img);
                    if (c.Glow != null) glowPool.Release(c.Glow);
                    coins.RemoveAt(i);
                    if (Arrived != null) Arrived(c.Val);
                    continue;
                }
                float s = 14f * c.Big;
                if (c.Phase == 1) s = Mathf.Lerp(14f * c.Big, 10f, Mathf.Clamp01(c.T / c.Dur));
                float k = s / 14f;
                float sx = Mathf.Max(Mathf.Abs(Mathf.Cos(c.Spin)), 0.25f);
                c.Img.enabled = true;
                ImgPool.Put(c.Img, c.P);
                c.Img.rectTransform.localScale = new Vector3(k * sx, k, 1f);
                if (c.Glow != null)
                {
                    c.Glow.enabled = true;
                    ImgPool.Put(c.Glow, c.P);
                }
            }
            for (int i = gems.Count - 1; i >= 0; i--)
            {
                GemP g = gems[i];
                g.T += dt;
                g.Spin += dt * 9f;
                bool arrived = false;
                bool visible = true;
                if (g.Phase == 0)
                {
                    if (g.T < 0f) visible = false;
                    else
                    {
                        g.V.x *= Mathf.Pow(0.1f, dt);
                        g.V.y += 1700f * dt;
                        g.P += g.V * dt;
                        if (g.P.y > g.Floor && g.V.y > 0f)
                        {
                            g.P.y = g.Floor;
                            g.V.y *= -0.45f;
                        }
                        if (g.T > 0.6f)
                        {
                            g.Phase = 1;
                            g.T = 0f;
                            g.From = g.P;
                        }
                    }
                }
                else
                {
                    float u = Mathf.Clamp01(g.T / g.Dur);
                    float e = u * u * (3f - 2f * u) * 0.4f + u * u * 0.6f;
                    Vector2 ctrl = Vector2.Lerp(g.From, GemTarget, 0.5f) + new Vector2(120f, 80f);
                    g.P = Bez(g.From, ctrl, GemTarget, e);
                    if (u > 0.15f && glints.Count < 40 && UnityEngine.Random.value < 0.5f)
                    {
                        Glint q = new Glint();
                        q.P = g.P + new Vector2(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(-6f, 6f));
                        q.Life = 0.3f;
                        q.Img = glintPool.Get();
                        glints.Add(q);
                    }
                    if (u >= 1f) arrived = true;
                }
                if (arrived)
                {
                    gemPool.Release(g.Img);
                    glowPool.Release(g.Glow);
                    gems.RemoveAt(i);
                    if (GemArrived != null) GemArrived(1);
                    continue;
                }
                g.Img.enabled = visible;
                g.Glow.enabled = visible;
                if (visible)
                {
                    float s = 15f;
                    if (g.Phase == 1) s = Mathf.Lerp(15f, 11f, Mathf.Clamp01(g.T / g.Dur));
                    float k = s / 15f * (0.85f + 0.15f * Mathf.Cos(g.Spin));
                    ImgPool.Put(g.Img, g.P);
                    g.Img.rectTransform.localScale = new Vector3(k, s / 15f, 1f);
                    ImgPool.Put(g.Glow, g.P);
                }
            }
            for (int i = glints.Count - 1; i >= 0; i--)
            {
                Glint q = glints[i];
                q.T += dt;
                if (q.T >= q.Life)
                {
                    glintPool.Release(q.Img);
                    glints.RemoveAt(i);
                    continue;
                }
                float f = q.T / q.Life;
                ImgPool.Put(q.Img, q.P);
                float sc = 7f / 14f * (1f - f);
                q.Img.rectTransform.localScale = new Vector3(sc, sc, 1f);
                q.Img.color = new Color(0.8f, 0.95f, 1f, 0.9f);
            }
        }
    }

    /// <summary>Destello + chispas de un solo uso (se destruye solo). Estrellas con contorno.</summary>
    public sealed class BurstFx : MonoBehaviour
    {
        Image glow;
        Image[] sparks;
        float[] ang, dist, size;
        Color col;
        float radius, life = 0.6f, t;
        Vector2 center;

        /// <summary>Crea el efecto centrado en `center` (canvas, y hacia abajo) dentro de `parent`.</summary>
        public static BurstFx Spawn(Transform parent, Vector2 center, Color color, float rad = 90f, int n = 8)
        {
            GameManager gm = GameManager.I;
            if (gm != null && gm.G.Eco) n = Mathf.Max(3, n / 2);
            RectTransform rt = Kit.New("Burst", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.sizeDelta = Vector2.zero;
            BurstFx f = rt.gameObject.AddComponent<BurstFx>();
            f.col = color;
            f.radius = rad;
            f.center = center;
            f.glow = Kit.Glow(rt, color, "G");
            f.glow.rectTransform.anchorMin = f.glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            f.sparks = new Image[n];
            f.ang = new float[n];
            f.dist = new float[n];
            f.size = new float[n];
            for (int i = 0; i < n; i++)
            {
                f.ang[i] = Mathf.PI * 2f * i / n + UnityEngine.Random.Range(-0.2f, 0.2f);
                f.dist[i] = rad * UnityEngine.Random.Range(0.7f, 1.3f);
                f.size[i] = UnityEngine.Random.Range(7f, 12f);
                Image s = Kit.Img(rt, Icons.Spark(), Color.white, "S", false);
                s.rectTransform.anchorMin = s.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                s.rectTransform.sizeDelta = new Vector2(32f, 32f);
                f.sparks[i] = s;
            }
            rt.anchoredPosition = new Vector2(center.x, -center.y);
            f.Tick(0f);
            return f;
        }

        void Update() { Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f)); }

        void Tick(float dt)
        {
            t += dt;
            if (t >= life) { Destroy(gameObject); return; }
            float u = Mathf.Clamp01(t / life);
            float a = (1f - u) * (1f - u);
            float gr = radius * (0.5f + 0.9f * u);
            glow.rectTransform.sizeDelta = new Vector2(gr * 2f, gr * 2f);
            glow.color = new Color(col.r, col.g, col.b, 0.9f * a);
            float e = 1f - Mathf.Pow(1f - u, 3f);
            for (int i = 0; i < sparks.Length; i++)
            {
                Vector2 p = new Vector2(Mathf.Cos(ang[i]), -Mathf.Sin(ang[i])) * dist[i] * e;   // y arriba en Unity
                sparks[i].rectTransform.anchoredPosition = p;
                float sz = (size[i] * (1f - u * 0.7f) + 2f) / 14f;
                sparks[i].rectTransform.localScale = new Vector3(sz, sz, 1f);
                sparks[i].color = new Color(1f, 1f, 1f, a);
            }
        }
    }

    /// <summary>Quads rectos o inclinados para franjas animadas (cinta de etapa, destellos).</summary>
    public sealed class StripesGraphic : MaskableGraphic
    {
        public struct Quad
        {
            public float X, Skew, W;   // x del borde izquierdo (arriba), desplazamiento horizontal abajo, ancho
            public Color C;
        }

        public readonly List<Quad> Quads = new List<Quad>();

        public void Mark() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            int idx = 0;
            for (int i = 0; i < Quads.Count; i++)
            {
                Quad q = Quads[i];
                Color32 c = q.C;
                float x0 = r.xMin + q.X, x1 = x0 + q.W;
                vh.AddVert(new Vector3(x0, r.yMax), c, Vector2.zero);
                vh.AddVert(new Vector3(x1, r.yMax), c, Vector2.zero);
                vh.AddVert(new Vector3(x1 + q.Skew, r.yMin), c, Vector2.zero);
                vh.AddVert(new Vector3(x0 + q.Skew, r.yMin), c, Vector2.zero);
                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx, idx + 2, idx + 3);
                idx += 4;
            }
        }
    }

    /// <summary>Confeti y cinta de "Etapa superada" (port de confetti.gd).</summary>
    public sealed class Confetti : MonoBehaviour
    {
        sealed class Part
        {
            public Vector2 P, V, S;
            public float Rot, Vr, T, Life;
            public Color Col;
            public Image Img;
        }

        static readonly Color[] Cols =
        {
            Icons.H("ff5a4e"), Icons.H("ffd84a"), Icons.H("5cc84a"), Icons.H("4aa3f0"), Icons.H("b06ef0"), Icons.H("ff9a3a"),
        };

        readonly List<Part> parts = new List<Part>();
        ImgPool pool;
        RectTransform bannerRoot;
        Image bannerOut, bannerFill;
        StripesGraphic stripes;
        Text bannerText;
        float bannerT = -1f;
        GameManager gm;

        public void Init(GameManager manager)
        {
            gm = manager;
            RectTransform bandClip = Kit.New("Banner", transform);
            bandClip.anchorMin = new Vector2(0, 1);
            bandClip.anchorMax = new Vector2(1, 1);
            bandClip.pivot = new Vector2(0.5f, 0.5f);
            bandClip.sizeDelta = new Vector2(0, 100);
            bannerRoot = bandClip;
            bannerOut = Kit.Tint(bandClip, new Color(Kit.Out.r, Kit.Out.g, Kit.Out.b, 0.9f), "Out");
            bannerFill = Kit.Tint(bandClip, new Color(1f, 0.86f, 0.45f, 1f), "Fill");
            RectTransform clip = Kit.New("Clip", bandClip);
            Kit.Stretch(clip, 0, 4, 0, 4);
            clip.gameObject.AddComponent<RectMask2D>();
            GameObject sg = new GameObject("Stripes", typeof(RectTransform), typeof(CanvasRenderer));
            sg.transform.SetParent(clip, false);
            stripes = sg.AddComponent<StripesGraphic>();
            stripes.raycastTarget = false;
            Kit.Stretch(stripes.rectTransform);
            bannerText = Kit.Label(bandClip, "", 46, Color.white, 12, true, TextAnchor.MiddleCenter, "Text");
            Kit.Stretch(bannerText.rectTransform);
            bandClip.gameObject.SetActive(false);
            pool = new ImgPool(transform, Kit.White, 1f);
        }

        public bool Active { get { return bannerT >= 0f; } }

        public void Fire(string text)
        {
            bannerText.text = text;
            bannerT = 0f;
            bannerRoot.gameObject.SetActive(true);
            float w = Kit.CanvasSize.x;
            int n = (gm != null && gm.G.Eco) ? 60 : 140;
            for (int i = 0; i < n; i++)
            {
                bool left = i % 2 == 0;
                Part q = new Part();
                q.P = new Vector2(left ? -10f : w + 10f, UnityEngine.Random.Range(250f, 700f));
                float a = UnityEngine.Random.Range(-1.25f, -0.35f);
                q.V = new Vector2(Mathf.Cos(a) * (left ? 1f : -1f), Mathf.Sin(a)) * UnityEngine.Random.Range(500f, 1200f);
                q.Rot = UnityEngine.Random.value * Mathf.PI * 2f;
                q.Vr = UnityEngine.Random.Range(-12f, 12f);
                q.Col = Cols[i % Cols.Length];
                q.S = new Vector2(UnityEngine.Random.Range(8f, 14f), UnityEngine.Random.Range(16f, 26f));
                q.Life = UnityEngine.Random.Range(2f, 3.2f);
                q.Img = pool.Get();
                q.Img.color = q.Col;
                parts.Add(q);
            }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                Part q = parts[i];
                q.T += dt;
                if (q.T > q.Life)
                {
                    pool.Release(q.Img);
                    parts.RemoveAt(i);
                    continue;
                }
                q.V *= Mathf.Pow(0.25f, dt);
                q.V.y += 520f * dt;
                q.P += q.V * dt;
                q.Rot += q.Vr * dt;
                float al = Mathf.Clamp01((q.Life - q.T) * 2f);
                float sx = Mathf.Max(q.S.x * Mathf.Abs(Mathf.Cos(q.Rot * 0.7f)), 2f);
                RectTransform rt = q.Img.rectTransform;
                ImgPool.Put(q.Img, q.P);
                rt.sizeDelta = new Vector2(sx, q.S.y);
                rt.localRotation = Quaternion.Euler(0, 0, -q.Rot * Mathf.Rad2Deg);
                q.Img.color = new Color(q.Col.r, q.Col.g, q.Col.b, al);
            }
            if (bannerT >= 0f)
            {
                bannerT += dt;
                if (bannerT > 2.2f)
                {
                    bannerT = -1f;
                    bannerRoot.gameObject.SetActive(false);
                }
                else DrawBanner();
            }
        }

        void DrawBanner()
        {
            Vector2 cs = Kit.CanvasSize;
            float a = Mathf.Clamp01(bannerT * 6f) * Mathf.Clamp01((2.2f - bannerT) * 4f);
            float yc = cs.y * 0.24f;
            float h = 84f * a;
            bannerRoot.anchoredPosition = new Vector2(0, -yc);
            bannerRoot.sizeDelta = new Vector2(0, h + 8f);
            Kit.Stretch(bannerOut.rectTransform);
            Kit.Stretch(bannerFill.rectTransform, 0, 4, 0, 4);
            bannerOut.color = new Color(Kit.Out.r, Kit.Out.g, Kit.Out.b, a * 0.9f);
            bannerFill.color = new Color(1f, 0.86f, 0.45f, a);
            stripes.Quads.Clear();
            float skew = -h * 0.4f;
            for (int i = -2; i < (int)(cs.x / 40f) + 2; i++)
            {
                float x = i * 40f + Mathf.Repeat(bannerT * 60f, 40f);
                stripes.Quads.Add(new StripesGraphic.Quad { X = x, Skew = skew, W = 18f, C = new Color(1f, 0.95f, 0.75f, a * 0.6f) });
            }
            float sx = Mathf.Repeat(bannerT * 900f, cs.x + 400f) - 200f;
            stripes.Quads.Add(new StripesGraphic.Quad { X = sx, Skew = -h * 0.5f, W = 60f, C = new Color(1f, 1f, 1f, 0.55f * a) });
            stripes.Mark();
            float sc = 1f + 0.4f * Mathf.Max(0f, 1f - bannerT * 5f);
            bannerText.rectTransform.localScale = new Vector3(sc, sc, 1f);
            bannerText.color = new Color(1f, 1f, 1f, a);
            Outline[] os = bannerText.GetComponents<Outline>();
            for (int i = 0; i < os.Length; i++) os[i].effectColor = new Color(Kit.Out.r, Kit.Out.g, Kit.Out.b, a);
        }
    }
}
