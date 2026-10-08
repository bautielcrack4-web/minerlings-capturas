using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Detalles de la interfaz (docs/DETALLES.md): el edificio elegido vuela a su parcela (2), recurso nuevo (3), cobro
    /// en cadena musical (5), numeros redondos que se festejan (7), estela del color del recurso y almacen que "traga"
    /// (8), lo que falta salta en rojo (15), "¡ahora podes!" (16) y la recompensa de la meta que vuela (18).
    /// </summary>
    public sealed partial class IslandUi
    {
        // ------------------------------------------------------------ (2) el edificio elegido vuela a la parcela
        public void FlyIcon(Sprite sprite, Vector2 from, Vector3 toWorld, float size, System.Action onLand = null)
        {
            Vector2 c = ToCanvas(toWorld);
            Vector2 to = new Vector2(c.x, -c.y);
            var img = Kit.Img(flyLayer, sprite, Color.white, "Vuela");
            img.preserveAspect = true;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = from;
            Vector2 mid = new Vector2((from.x + to.x) * 0.5f, Mathf.Max(from.y, to.y) + 200f);
            Sfx.Play("whoosh", -10f, 1.2f);
            Tw.To(rt, "vuela", 0.5f, Ease.Linear, u =>
            {
                float e = u * u * (3f - 2f * u);
                rt.anchoredPosition = Vector2.Lerp(Vector2.Lerp(from, mid, e), Vector2.Lerp(mid, to, e), e);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, e);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * Mathf.PI) * 14f);
            }, () => { Destroy(img.gameObject); onLand?.Invoke(); });
        }

        // ------------------------------------------------------------ (3) recurso nuevo
        void CheckNewResource(int res)
        {
            string k = "seen_res" + res;
            if (Isl.Stat(k) > 0) return;
            Isl.AddStat(k, 1);
            // capsula chica con el icono (antes un cartel grande que tapaba la isla justo cuando el recurso aparece en ella)
            Toast(Loc.T("¡Nuevo recurso!") + " " + Island.ResDefs[res].Name, ResIcons.Tint(res), ResIcons.Get(res), true);
            Sfx.Play("gleam", -6f);
        }

        // ------------------------------------------------------------ (5) escala pentatonica: nunca desafina
        static readonly int[] Penta = { 0, 2, 4, 7, 9 };

        public static float PentaPitch(int n)
        {
            n = Mathf.Clamp(n, 0, 12);
            int st = Penta[n % 5] + 12 * (n / 5);
            return Mathf.Pow(2f, st / 12f) * 0.9f;
        }

        // ------------------------------------------------------------ (7) numeros redondos
        double roundShown = -1;

        void CheckRoundNumber()
        {
            double c = Isl.Coins;
            double m = c >= 1000 ? System.Math.Pow(10, System.Math.Floor(System.Math.Log10(c))) : 0;
            if (roundShown < 0) { roundShown = m; return; }
            if (m <= roundShown) { if (m < roundShown) roundShown = m; return; }
            roundShown = m;
            var at = LayerPos(coinPill.Root);
            Sparkle(at, new Color(1f, 0.9f, 0.4f));
            Sparkle(at + new Vector2(60f, 0f), new Color(1f, 0.95f, 0.6f));
            Sfx.Play("chimes", -6f);
            Squash(coinPill.Root);
            Toast(Loc.T("¡") + BigNum.Fmt(m) + Loc.T(" monedas!"), Kit.Orange);
        }

        // ------------------------------------------------------------ (8) el indicador "traga"
        void Squash(RectTransform rt)
        {
            if (rt == null) return;
            Tw.To(rt, "trago", 0.28f, Ease.Linear, u =>
            {
                float k = Mathf.Sin(u * Mathf.PI) * (1f - u * 0.4f);
                rt.localScale = new Vector3(1f + 0.14f * k, 1f - 0.12f * k, 1f);
            }, () => rt.localScale = Vector3.one);
        }

        // ------------------------------------------------------------ (15) lo que falta salta en rojo
        void MissingPop(RectTransform near, List<KeyValuePair<Res, int>> mats)
        {
            if (near == null || mats == null) return;
            Vector2 at = LayerPos(near);
            int i = 0;
            foreach (var kv in mats)
            {
                if (Isl.Stock[(int)kv.Key] >= kv.Value) continue;
                var img = Kit.Img(flyLayer, ResIcons.Get(kv.Key), Color.white, "Falta");
                img.preserveAspect = true;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(64, 64);
                var x = Kit.Img(rt, Icons.Get("close"), Color.white, "X");
                x.rectTransform.sizeDelta = new Vector2(30, 30);
                x.rectTransform.anchoredPosition = new Vector2(22f, -20f);
                Vector2 from = at + new Vector2((i - 0.5f) * 70f, 20f);
                rt.anchoredPosition = from;
                int idx = i;
                Tw.To(rt, "falta", 1.1f, Ease.Linear, u =>
                {
                    float t = Mathf.Clamp01(u * 1.1f - idx * 0.08f);
                    rt.anchoredPosition = from + new Vector2(0f, Mathf.Sin(Mathf.Min(1f, t * 1.6f) * Mathf.PI) * 70f + t * 40f);
                    rt.localScale = Vector3.one * (t < 0.15f ? t / 0.15f : 1f);
                    img.color = new Color(1f, 0.55f, 0.55f, u > 0.75f ? (1f - u) / 0.25f : 1f);
                }, () => Destroy(img.gameObject));
                i++;
            }
        }

        // ------------------------------------------------------------ (16) "¡ahora podes!"
        readonly HashSet<int> arrowSeen = new HashSet<int>();
        bool arrowsReady;

        void ArrowAnnounce()
        {
            if (!arrowsReady) { foreach (var id in arrowPlots) arrowSeen.Add(id); arrowsReady = true; return; }
            foreach (var id in arrowPlots)
            {
                if (arrowSeen.Contains(id)) continue;
                arrowSeen.Add(id);
                Image up;
                arrowFlash[id] = Time.unscaledTime + 3f;   // la flecha se ve 3 s cuando recien alcanza
                if (upMarks.TryGetValue(id, out up) && up != null) { Tw.Pop(up.transform, 1.6f); }
                Sfx.Play("chimes", -12f, 1.25f);
            }
            // si deja de alcanzar, la proxima vez vuelve a anunciarse
            arrowSeen.RemoveWhere(id => !arrowPlots.Contains(id));
        }

        void UpdateDetailsUi()
        {
            CheckRoundNumber();
        }
    }
}
