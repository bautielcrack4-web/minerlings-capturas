using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.IslandView
{
    /// <summary>
    /// Respuesta de la interfaz al picar (biblia 3.1-3.2): anillo en el dedo, numeros que salen del toque (criticos
    /// dorados), carteles de combo que escalan de color, frenesi con barra de tiempo arcoiris, monedas que giran con
    /// estela (bolsas y lingotes si la cantidad es grande) y gemas que vuelan al contador con destellos.
    /// </summary>
    public sealed partial class IslandUi
    {
        /// <summary>Posicion de pantalla (pixeles de la camara) → posicion en una capa estirada (origen al centro).</summary>
        Vector2 ScreenToLayer(Vector2 screen)
        {
            Vector2 cs = Kit.CanvasSize;
            float pw = Mathf.Max(game.Cam.pixelWidth, 1), ph = Mathf.Max(game.Cam.pixelHeight, 1);
            return new Vector2(screen.x / pw * cs.x - cs.x * 0.5f, screen.y / ph * cs.y - cs.y * 0.5f);
        }

        /// <summary>Centro de un elemento del HUD en coordenadas de la capa de vuelo.</summary>
        Vector2 LayerPos(RectTransform rt, float dx = 0f)
        {
            Vector3 w = rt.TransformPoint(new Vector3(rt.rect.center.x + dx, rt.rect.center.y, 0f));
            return flyLayer.InverseTransformPoint(w);
        }

        // ------------------------------------------------------------ chispa que trae algo a la isla (detalle 1)
        /// <summary>
        /// Chispa con estela: sale de `from` (o del cielo, arriba del destino), sube un poco, cruza en arco y cae sobre el
        /// punto del mundo; al llegar llama a onLand. Destellos chiquitos van quedando detras.
        /// </summary>
        public void FlySpark(Vector2? from, Vector3 toWorld, Color col, System.Action onLand)
        {
            Vector2 c = ToCanvas(toWorld);
            Vector2 to = new Vector2(c.x, -c.y);
            Vector2 a = from ?? new Vector2(to.x + Random.Range(-80f, 80f), Kit.CanvasSize.y * 0.5f + 60f);
            var spark = Kit.Img(flyLayer, Icons.Glow(), new Color(col.r, col.g, col.b, 1f), "Chispa");
            var rt = spark.rectTransform;
            rt.sizeDelta = new Vector2(70, 70);
            var core = Kit.Img(rt, Icons.Get("star"), Color.white, "Nucleo");
            core.rectTransform.sizeDelta = new Vector2(34, 34);
            rt.anchoredPosition = a;
            Vector2 mid = new Vector2((a.x + to.x) * 0.5f, Mathf.Max(a.y, to.y) + 260f);
            float dur = from.HasValue ? 0.75f : 0.6f;
            float trailT = 0f;
            Sfx.Play("whoosh", -12f, 1.4f);
            Tw.To(rt, "chispa", dur, Ease.Linear, u =>
            {
                float e = u * u * (3f - 2f * u);
                rt.anchoredPosition = Vector2.Lerp(Vector2.Lerp(a, mid, e), Vector2.Lerp(mid, to, e), e);
                rt.localScale = Vector3.one * (0.8f + Mathf.Sin(u * Mathf.PI) * 0.5f);
                core.rectTransform.localRotation = Quaternion.Euler(0, 0, u * 540f);
                trailT += Time.unscaledDeltaTime;
                if (trailT > 0.035f)
                {
                    trailT = 0f;
                    var d = UiPool.Get(flyLayer, Icons.Get("star"), new Color(col.r, col.g, col.b, 0.9f), "Estela");
                    var drt = d.rectTransform;
                    drt.sizeDelta = new Vector2(18, 18);
                    drt.anchoredPosition = rt.anchoredPosition + Random.insideUnitCircle * 8f;
                    drt.SetSiblingIndex(rt.GetSiblingIndex());
                    Tw.To(drt, "estela", 0.4f, Ease.Linear, k => { drt.localScale = Vector3.one * (1f - k); d.color = new Color(col.r, col.g, col.b, 0.9f * (1f - k)); }, () => UiPool.Release(d));
                }
            }, () =>
            {
                Destroy(spark.gameObject);
                onLand?.Invoke();
            });
        }

        // ------------------------------------------------------------ anillo en el dedo
        public void TapRing(Vector2 screen, Color col, int combo)
        {
            Vector2 p = ScreenToLayer(screen);
            var ring = UiPool.Get(flyLayer, Icons.Ring(5f), col, "Anillo");
            var rt = ring.rectTransform;
            float size = 80f + Mathf.Min(combo, 30) * 2.5f;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = p;
            Tw.To(rt, "ring", 0.32f, Ease.OutCubic, u =>
            {
                rt.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.25f, u);
                ring.color = new Color(col.r, col.g, col.b, (1f - u) * 0.9f);
            }, () => UiPool.Release(ring));
            if (combo < 10) return;
            // con combo alto, chispas que salen del dedo
            int n = combo >= 20 ? 6 : 4;
            for (int i = 0; i < n; i++)
            {
                var s = UiPool.Get(flyLayer, Icons.Get("star"), col, "Chispa");
                var sr = s.rectTransform;
                sr.sizeDelta = new Vector2(22, 22);
                float a = (i + Random.value * 0.5f) / n * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Tw.To(sr, "chispa", 0.4f, Ease.OutCubic, u =>
                {
                    sr.anchoredPosition = p + dir * Mathf.Lerp(20f, 90f, u);
                    sr.localScale = Vector3.one * (1f - u);
                    sr.localRotation = Quaternion.Euler(0, 0, u * 180f);
                }, () => UiPool.Release(s));
            }
        }

        // ------------------------------------------------------------ numero que sale del toque
        public void PopupAt(Vector2 screen, string text, Color col, int size, bool crit)
        {
            Vector2 from = ScreenToLayer(screen) + new Vector2(Random.Range(-12f, 12f), 30f);
            var t = Kit.Label(flyLayer, text, size, col, crit ? 8 : 7, true, TextAnchor.MiddleCenter, "Numero");
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(360, size + 16);
            rt.anchoredPosition = from;
            Tw.To(rt, "num", crit ? 1.0f : 0.75f, Ease.Linear, u =>
            {
                float e = Tw.Eval(Ease.OutBack, Mathf.Min(1f, u * 2.2f));
                rt.anchoredPosition = from + new Vector2(0f, (crit ? 80f : 60f) * e);
                rt.localScale = Vector3.one * Mathf.Lerp(crit ? 1.7f : 1.3f, 1f, Mathf.Min(1f, u * 3f));
                t.color = new Color(col.r, col.g, col.b, u < 0.7f ? 1f : (1f - u) / 0.3f);
            }, () => Destroy(t.gameObject));
            if (crit && text != Loc.T("¡CRÍTICO!"))
            {
                var c = Kit.Label(flyLayer, Loc.T("¡CRÍTICO!"), 24, new Color(1f, 0.95f, 0.7f), 6, true, TextAnchor.MiddleCenter, "Critico");
                var cr = c.rectTransform;
                cr.sizeDelta = new Vector2(240, 34);
                Tw.To(cr, "crit", 1.0f, Ease.Linear, u =>
                {
                    cr.anchoredPosition = from + new Vector2(0f, 46f + 80f * Tw.Eval(Ease.OutBack, Mathf.Min(1f, u * 2.2f)));
                    c.color = new Color(1f, 0.95f, 0.7f, u < 0.7f ? 1f : (1f - u) / 0.3f);
                }, () => Destroy(c.gameObject));
            }
        }

        // ------------------------------------------------------------ carteles de combo
        RectTransform comboRT;

        public void ComboBanner(int c, Color col)
        {
            if (comboRT != null) { Tw.Kill(comboRT); Destroy(comboRT.gameObject); }
            var t = Kit.Label(flyLayer, Loc.T("COMBO x") + c, c >= 20 ? 58 : 48, col, 8, true, TextAnchor.MiddleCenter, "Combo");
            comboRT = t.rectTransform;
            var rt = comboRT;
            rt.sizeDelta = new Vector2(600, 80);
            rt.anchoredPosition = new Vector2(0f, Kit.CanvasSize.y * 0.5f - 300f);   // arriba, sin tapar la accion
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-6f, 6f));
            bool rainbow = c >= Island.FrenzyCombo;
            Tw.To(rt, "combo", 1.1f, Ease.Linear, u =>
            {
                rt.localScale = Vector3.one * Tw.Eval(Ease.OutElastic, Mathf.Min(1f, u * 2f)) * (u > 0.8f ? 1f - (u - 0.8f) * 2f : 1f);
                Color k = rainbow ? Color.HSVToRGB(Mathf.Repeat(u * 2f, 1f), 0.6f, 1f) : col;
                t.color = new Color(k.r, k.g, k.b, u > 0.8f ? (1f - u) / 0.2f : 1f);
            }, () => { if (rt != null) Destroy(rt.gameObject); });
        }

        // ------------------------------------------------------------ frenesi
        RectTransform frenzyBar;
        Image frenzyFill;

        public void Frenzy()
        {
            if (comboRT != null) { Tw.Kill(comboRT); Destroy(comboRT.gameObject); comboRT = null; }
            Flash(new Color(1f, 0.9f, 0.5f), 0.3f);
            var t = Kit.Label(flyLayer, Loc.T("¡FRENESÍ!"), 76, Color.white, 9, true, TextAnchor.MiddleCenter, "Frenesi");
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(680, 110);
            rt.anchoredPosition = new Vector2(0f, Kit.CanvasSize.y * 0.5f - 310f);
            var sub = Kit.Label(rt, Loc.T("¡Pico doble y monedas en cada golpe!"), 28, Color.white, 6, true, TextAnchor.MiddleCenter, "Sub");
            sub.rectTransform.sizeDelta = new Vector2(680, 40);
            sub.rectTransform.anchoredPosition = new Vector2(0f, -70f);
            Tw.To(rt, "fr", 1.8f, Ease.Linear, u =>
            {
                rt.localScale = Vector3.one * Tw.Eval(Ease.OutElastic, Mathf.Min(1f, u * 2.5f)) * (u > 0.85f ? 1f - (u - 0.85f) * 4f : 1f);
                t.color = Color.HSVToRGB(Mathf.Repeat(u * 3f, 1f), 0.55f, 1f);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 20f) * 3f * (1f - u));
            }, () => Destroy(rt.gameObject));
        }

        RectTransform rainbowChip;
        Text rainbowL;

        /// <summary>Etiqueta del arcoiris (+50 %) con el tiempo que queda.</summary>
        void UpdateRainbowChip()
        {
            bool on = Isl.RainbowT > 0f;
            if (rainbowChip == null)
            {
                if (!on) return;
                rainbowChip = Kit.MakeTag(hudLayer, Loc.T("Arcoíris +50 %"), new Color(0.9f, 0.45f, 0.7f), 22);
                rainbowL = rainbowChip.GetComponentInChildren<Text>();
            }
            if (rainbowChip.gameObject.activeSelf != on) { rainbowChip.gameObject.SetActive(on); if (on) Tw.Pop(rainbowChip, 1.3f); }
            if (!on) return;
            int s = Mathf.CeilToInt(Isl.RainbowT);
            if (rainbowL != null) rainbowL.text = Loc.T("Arcoíris +50 %  ") + s / 60 + ":" + (s % 60).ToString("00");
            rainbowChip.anchorMin = rainbowChip.anchorMax = new Vector2(0.5f, 1f);
            rainbowChip.anchoredPosition = new Vector2(0f, -300f);
            Color c = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.15f, 1f), 0.45f, 0.95f);
            var img = rainbowChip.GetComponent<Image>();
            if (img != null) img.color = c;
        }

        void UpdateFrenzyBar()
        {
            UpdateRainbowChip();
            bool on = Isl.FrenzyT > 0f;
            if (frenzyBar == null)
            {
                if (!on) return;
                frenzyBar = Kit.OutBox(hudLayer, 12, 3, 0, Icons.H("3b2a52"), Kit.Out, "Frenesi");
                Kit.Place(frenzyBar, 0.5f, 0f, -200f, 190f, 400, 30);
                frenzyFill = Kit.RoundImg(frenzyBar, 10, Color.white, "Relleno");
                var r = frenzyFill.rectTransform;
                r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
                r.offsetMin = new Vector2(3, 3); r.offsetMax = new Vector2(3, -3);
                var l = Kit.Label(frenzyBar, Loc.T("FRENESÍ"), 20, Color.white, 5, true, TextAnchor.MiddleCenter);
                Kit.Stretch(l.rectTransform);
            }
            if (frenzyBar.gameObject.activeSelf != on) { frenzyBar.gameObject.SetActive(on); if (on) Tw.Pop(frenzyBar, 1.3f); }
            if (!on) return;
            float f = Isl.FrenzyT / Island.FrenzyTime;
            frenzyFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(20f, 394f * f), frenzyFill.rectTransform.sizeDelta.y);
            frenzyFill.color = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.7f, 1f), 0.55f, 1f);
        }

        // ------------------------------------------------------------ monedas grandes: bolsas y lingotes
        static Sprite ingot;

        Sprite Ingot()
        {
            if (ingot != null) return ingot;
            var mb = new Mineros.World.MeshBuilder();
            Color g = IslandArt.H("ffd23a"), d = IslandArt.H("e09a1c");
            // lingote trapezoidal con brillo arriba
            Vector3[] lo = { new Vector3(-0.5f, 0f, -0.25f), new Vector3(0.5f, 0f, -0.25f), new Vector3(0.5f, 0f, 0.25f), new Vector3(-0.5f, 0f, 0.25f) };
            Vector3[] hi = { new Vector3(-0.38f, 0.28f, -0.15f), new Vector3(0.38f, 0.28f, -0.15f), new Vector3(0.38f, 0.28f, 0.15f), new Vector3(-0.38f, 0.28f, 0.15f) };
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector3 n = Vector3.Cross(hi[i] - lo[i], lo[j] - lo[i]).normalized;
                mb.Tri(lo[i], hi[i], hi[j], -n, i % 2 == 0 ? d : g, 0.25f);
                mb.Tri(lo[i], hi[j], lo[j], -n, i % 2 == 0 ? d : g, 0.25f);
            }
            mb.Tri(hi[0], hi[3], hi[2], Vector3.up, IslandArt.H("ffe680"), 0.45f);
            mb.Tri(hi[0], hi[2], hi[1], Vector3.up, IslandArt.H("ffe680"), 0.45f);
            Material[] mats;
            var m = mb.ToMesh(null, "Lingote", out mats);
            mats = Mineros.World.VertexColorMerge.Apply(m, mats);
            ingot = IslandStage.I.RenderIcon(m, mats, 192);
            return ingot;
        }

        /// <summary>Que mostrar volando segun la cantidad: monedas, bolsas (x10 del ritmo) o lingotes (x60).</summary>
        Sprite CoinSprite(double amount, out float size, out int n)
        {
            double unit = Mathf.Max(1f, (float)Isl.IncomePerSec());
            if (amount >= unit * 60) { size = 58f; n = 5; return Ingot(); }
            if (amount >= unit * 10) { size = 54f; n = 4; return Icons.Get("money"); }
            size = 40f;
            n = Mathf.Clamp((int)System.Math.Ceiling(System.Math.Log(amount + 1, 2)), 1, 6);
            return Icons.Get("coin");
        }

        public void FlyCoinsFrom(Vector2 screen, int n) { FlyCoins(ScreenToLayer(screen), n); }
        public void FlyGemsFrom(Vector2 screen, int n) { FlyGems(ScreenToLayer(screen), n); }

        // ------------------------------------------------------------ gemas al contador
        public void FlyGems(Vector2 from, int n)
        {
            Vector2 target = LayerPos(gemPill.Root, -gemPill.Root.rect.width * 0.5f + 23f);
            n = Mathf.Clamp(n, 1, 8);
            for (int i = 0; i < n; i++)
            {
                var g = UiPool.Get(flyLayer, Icons.Get("gem"), Color.white, "Gema");
                var rt = g.rectTransform;
                rt.sizeDelta = new Vector2(44, 44);
                Vector2 p0 = from + Random.insideUnitCircle * 30f;
                Vector2 mid = Vector2.Lerp(p0, target, 0.4f) + new Vector2(Random.Range(-160f, 160f), 160f);
                float delay = i * 0.08f, dur = 0.9f;
                float nextStar = 0f;
                rt.localScale = Vector3.zero;
                Tw.To(rt, "gema", delay + dur, Ease.Linear, u =>
                {
                    float t = u * (delay + dur) - delay;
                    if (t < 0f) return;
                    float k = Mathf.Clamp01(t / dur);
                    float e = k * k * (3f - 2f * k);
                    rt.anchoredPosition = Vector2.Lerp(Vector2.Lerp(p0, mid, e), Vector2.Lerp(mid, target, e), e);
                    rt.localScale = Vector3.one * (k < 0.15f ? Tw.Eval(Ease.OutBack, k / 0.15f) : 1f - 0.3f * k);
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 9f) * 12f);
                    // destello en estrella cada 0.4 s
                    if (t >= nextStar) { nextStar = t + 0.4f; Sparkle(rt.anchoredPosition, new Color(0.85f, 0.6f, 1f)); }
                }, () =>
                {
                    UiPool.Release(g);
                    Squash(gemPill.Root);   // la capsula "traga" cada gema
                    Mineros.Fx.Haptics.Selection();
                    Sfx.Play("gem", -8f, 1.1f);
                });
            }
        }

        void Sparkle(Vector2 at, Color col)
        {
            var s = Kit.Img(flyLayer, Icons.Get("star"), col, "Destello");
            var sr = s.rectTransform;
            sr.sizeDelta = new Vector2(36, 36);
            sr.anchoredPosition = at;
            Tw.To(sr, "dest", 0.3f, Ease.Linear, u =>
            {
                sr.localScale = Vector3.one * Mathf.Sin(u * Mathf.PI) * 1.2f;
                sr.localRotation = Quaternion.Euler(0, 0, u * 90f);
            }, () => Destroy(s.gameObject));
        }
    }
}
