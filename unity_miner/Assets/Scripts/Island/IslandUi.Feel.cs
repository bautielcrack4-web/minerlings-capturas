using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;
using Haptics = Mineros.Fx.Haptics;

namespace Mineros.IslandView
{
    /// <summary>
    /// Detalles de tacto (docs/PLAN_PULIDO.md, fases P4-P7): lo que entra al contador se ve llegar ("+240" debajo y un
    /// brillo que barre la capsula), el ☰ se vuelve una X al abrir el menu, y tocar el suelo, el agua o un arbol siempre
    /// responde (nunca un toque "muerto").
    /// </summary>
    public sealed partial class IslandUi
    {
        // ------------------------------------------------------------ "+240" debajo del contador
        double pendingGain;
        float gainT;
        Text gainLabel;

        /// <summary>Lo que se va a sumar a las monedas: se muestra junto, cuando terminan de llegar.</summary>
        public void CoinGain(double amount)
        {
            if (amount <= 0) return;
            pendingGain += amount;
            gainT = 0.95f;
        }

        void UpdateFeel()
        {
            if (gainT > 0f)
            {
                gainT -= Time.unscaledDeltaTime;
                if (gainT <= 0f && pendingGain > 0)
                {
                    ShowGain(pendingGain);
                    pendingGain = 0;
                }
            }
            UpdateMenuMorph();
            // sin tocar 20 s el HUD se aparta (70 %); vuelve al primer toque
            bool touch = Input.touchCount > 0 || Input.GetMouseButton(0);
            idleHud = touch ? 0f : idleHud + Time.unscaledDeltaTime;
            if (hudGroup == null) hudGroup = Kit.Group(hudLayer.gameObject);
            float want = idleHud > 20f && sheet == null && !game.ComplexMode ? 0.7f : 1f;
            if (Mathf.Abs(hudGroup.alpha - want) > 0.001f) hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, want, Time.unscaledDeltaTime * (want < 1f ? 0.6f : 6f));
        }

        float idleHud;
        CanvasGroup hudGroup;

        void ShowGain(double v)
        {
            if (gainLabel == null)
            {
                gainLabel = Kit.Label(hudLayer, "", 22, Kit.Yellow, 4, true, TextAnchor.MiddleCenter, "Ganancia");
                gainLabel.rectTransform.sizeDelta = new Vector2(220, 32);
            }
            var rt = gainLabel.rectTransform;
            rt.anchorMin = rt.anchorMax = coinPill.Root.anchorMin;
            Vector2 at = coinPill.Root.anchoredPosition + new Vector2(18f, -40f);
            gainLabel.text = "+" + BigNum.Fmt(v);
            gainLabel.gameObject.SetActive(true);
            Tw.To(rt, "gana", 1.1f, Ease.Linear, u =>
            {
                rt.anchoredPosition = at + new Vector2(0f, -14f * u);
                float a = u < 0.15f ? u / 0.15f : u > 0.7f ? (1f - u) / 0.3f : 1f;
                gainLabel.color = new Color(Kit.Yellow.r, Kit.Yellow.g, Kit.Yellow.b, a);
                rt.localScale = Vector3.one * (u < 0.15f ? Tw.Eval(Ease.OutBack, u / 0.15f) : 1f);
            }, () => gainLabel.gameObject.SetActive(false));
            PillShine(coinPill.Root);
        }

        /// <summary>Un brillo diagonal barre la capsula de izquierda a derecha (recortado por una mascara).</summary>
        void PillShine(RectTransform pill)
        {
            if (pill.GetComponent<RectMask2D>() == null) pill.gameObject.AddComponent<RectMask2D>();
            var shine = pill.Find("Barrido") as RectTransform;
            Image im;
            if (shine == null)
            {
                im = Kit.Img(pill, Icons.Glow(), new Color(1f, 1f, 1f, 0.55f), "Barrido");
                shine = im.rectTransform;
                shine.anchorMin = shine.anchorMax = new Vector2(0f, 0.5f);
                shine.sizeDelta = new Vector2(46f, 110f);
                shine.localRotation = Quaternion.Euler(0, 0, -20f);
            }
            else im = shine.GetComponent<Image>();
            float w = pill.rect.width;
            Tw.To(shine, "barre", 0.45f, Ease.InOutSine, u =>
            {
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-40f, w + 40f, u), 0f);
                im.color = new Color(1f, 1f, 1f, 0.55f * Mathf.Sin(u * Mathf.PI));
            });
        }

        /// <summary>Iconos que viajan de un punto a otro en arco, escalonados (pagos que se van, premios que llegan).</summary>
        void FlyTrail(Vector2 from, Vector2 to, Sprite sprite, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var im = UiPool.Get(flyLayer, sprite, Color.white, "Pago");
                im.preserveAspect = true;
                var rt = im.rectTransform;
                rt.sizeDelta = new Vector2(38, 38);
                Vector2 a = from + Random.insideUnitCircle * 16f;
                Vector2 mid = (a + to) * 0.5f + new Vector2(Random.Range(-80f, 80f), 160f);
                float delay = i * 0.06f, dur = 0.55f;
                rt.localScale = Vector3.zero;
                bool last = i == n - 1;
                Tw.To(rt, "pago", delay + dur, Ease.Linear, u =>
                {
                    float t = Mathf.Clamp01((u * (delay + dur) - delay) / dur);
                    if (t <= 0f) return;
                    float e = t * t * (3f - 2f * t);
                    rt.anchoredPosition = Vector2.Lerp(Vector2.Lerp(a, mid, e), Vector2.Lerp(mid, to, e), e);
                    rt.localScale = Vector3.one * (t < 0.15f ? t / 0.15f : 1f - 0.4f * t);
                    rt.localRotation = Quaternion.Euler(0, 0, t * 360f);
                }, () =>
                {
                    UiPool.Release(im);
                    Sfx.Play("clink", -14f, 1.1f + Random.value * 0.2f);
                    if (last) Sparkle(to, new Color(1f, 0.95f, 0.6f));
                });
            }
        }

        // ------------------------------------------------------------ el ☰ se vuelve X con el menu abierto
        bool menuMorph;
        float morphK;

        void UpdateMenuMorph()
        {
            if (menuBtn == null) return;
            bool open = sheet != null && sheet.Find("Marco") != null && menuOpenSheet == sheet;
            float target = open ? 1f : 0f;
            if (Mathf.Abs(morphK - target) < 0.001f && menuMorph == open) return;
            menuMorph = open;
            morphK = Mathf.MoveTowards(morphK, target, Time.unscaledDeltaTime * 7f);
            float e = morphK * morphK * (3f - 2f * morphK);
            var t = menuBtn.transform;
            int bar = 0;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i) as RectTransform;
                if (c == null || c.name != "Raya") continue;
                // raya de arriba y de abajo giran al centro; la del medio se desvanece
                float baseY = -(bar - 1) * 9f;
                if (bar == 1) { c.localScale = new Vector3(1f - e, 1f, 1f); }
                else
                {
                    float sgn = bar == 0 ? 1f : -1f;
                    c.anchoredPosition = new Vector2(c.anchoredPosition.x, Mathf.Lerp(baseY, 0f, e));
                    c.localRotation = Quaternion.Euler(0, 0, -45f * sgn * e);
                }
                bar++;
            }
        }

        RectTransform menuOpenSheet;

        // ------------------------------------------------------------ toques en la isla que no daban nada
        /// <summary>Tocar el suelo deja un anillo de polvo; el agua, una onda; un arbol, lo sacude.</summary>
        public void GroundTap(Vector2 screen, Vector3 ground, bool water)
        {
            Vector2 p = ScreenToLayer(screen);
            var ring = UiPool.Get(flyLayer, Icons.Ring(4f), water ? new Color(0.85f, 0.95f, 1f, 0.9f) : new Color(1f, 0.95f, 0.85f, 0.8f), "Onda");
            var rt = ring.rectTransform;
            rt.anchoredPosition = p;
            float size = water ? 120f : 70f;
            Tw.To(rt, "onda", water ? 0.6f : 0.35f, Ease.OutCubic, u =>
            {
                rt.sizeDelta = new Vector2(size, size * 0.55f) * Mathf.Lerp(0.3f, 1.2f, u);
                ring.color = new Color(ring.color.r, ring.color.g, ring.color.b, (1f - u) * 0.85f);
            }, () => UiPool.Release(ring));
            bool tree = !water && game.Ambient != null && game.Ambient.ShakeNear(ground, 1.6f, 7f);
            if (water) { Sfx.Play("splash", -16f, Random.Range(1.1f, 1.3f)); Haptics.Selection(); }
            else if (tree) { Sfx.Play("wings", -14f, Random.Range(0.95f, 1.1f)); Haptics.Light(); }
            else Haptics.Selection();
        }
    }
}
