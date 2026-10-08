using UnityEngine;
using UnityEngine.UI;
using Mineros.UI;

namespace Mineros.IslandView
{
    /// <summary>
    /// Medidor de rendimiento para probar en el telefono (auditoria final, Fase E): 5 toques rapidos sobre las monedas lo
    /// prende o apaga. Muestra FPS promedio, el "1 % bajo" y el peor cuadro de los ultimos 2 s (lo que se siente como tiron).
    /// </summary>
    public sealed class IslandPerfHud : MonoBehaviour
    {
        Text label;
        readonly float[] dts = new float[240];
        int n, head;
        float showT;

        public static IslandPerfHud Make(RectTransform layer)
        {
            var go = new GameObject("Rendimiento", typeof(RectTransform));
            go.transform.SetParent(layer, false);
            var h = go.AddComponent<IslandPerfHud>();
            h.label = Kit.LabelAt((RectTransform)go.transform, "", 22, Color.white, 4, true, 0, 0, 340, 90, TextAnchor.UpperLeft);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -200f);
            rt.sizeDelta = new Vector2(520, 130);
            go.SetActive(PlayerPrefs.GetInt("isla_perf", 0) == 1);
            return h;
        }

        public void Toggle()
        {
            bool on = !gameObject.activeSelf;
            gameObject.SetActive(on);
            PlayerPrefs.SetInt("isla_perf", on ? 1 : 0);
        }

        void Update()
        {
            dts[head] = Time.unscaledDeltaTime; head = (head + 1) % dts.Length; if (n < dts.Length) n++;
            showT -= Time.unscaledDeltaTime;
            if (showT > 0f) return;
            showT = 0.5f;
            float sum = 0f, worst = 0f, t = 0f; int c = 0;
            var tmp = new System.Collections.Generic.List<float>(n);
            for (int i = 0; i < n && t < 2f; i++)
            {
                float d = dts[(head - 1 - i + dts.Length) % dts.Length];
                sum += d; t += d; c++; tmp.Add(d); if (d > worst) worst = d;
            }
            if (c == 0) return;
            tmp.Sort();
            float low = tmp[Mathf.Clamp(Mathf.CeilToInt(c * 0.99f) - 1, 0, c - 1)];
            label.text = (c / Mathf.Max(sum, 1e-4f)).ToString("0") + " FPS\n1% bajo " + (1f / Mathf.Max(low, 1e-4f)).ToString("0")
                + " · peor " + (worst * 1000f).ToString("0") + " ms\nanuncio: " + Mineros.Monetization.Ads.Status;
            label.color = c / sum >= 55f ? new Color(0.6f, 1f, 0.6f) : c / sum >= 40f ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.5f, 0.45f);
        }
    }
}
