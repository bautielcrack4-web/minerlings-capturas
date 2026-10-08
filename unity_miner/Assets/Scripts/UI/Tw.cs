using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mineros.UI
{
    public enum Ease { Linear, OutBack, OutCubic, OutQuad, InQuad, InOutSine, OutBounce, InOutCubic, OutElastic, InBack }

    /// <summary>
    /// Mini sistema de tweens propio (sin paquetes). Tiempo sin escala (unscaled). Cada tween tiene un dueño y una clave:
    /// crear otro con el mismo dueño+clave reemplaza al anterior (como matar el Tween en Godot).
    /// </summary>
    public static class Tw
    {
        public sealed class Entry
        {
            public object Owner;
            public UnityEngine.Object Uo;
            public string Key;
            public float Delay, Dur, T;
            public Ease Ease;
            public Action<float> Apply;
            public Action Done;
            public bool Dead;
            public bool Started;
            public Action OnStart;
        }

        static readonly List<Entry> active = new List<Entry>();
        static readonly List<Entry> pending = new List<Entry>();
        static TwRunner runner;

        sealed class TwRunner : MonoBehaviour
        {
            void Update() { Tw.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f)); }
        }

        /// <summary>Crea el ejecutor (una vez) como hijo de `parent`.</summary>
        public static void Init(Transform parent)
        {
            active.Clear();
            pending.Clear();
            if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
            GameObject go = new GameObject("Tweens");
            go.transform.SetParent(parent, false);
            runner = go.AddComponent<TwRunner>();
        }

        public static float Eval(Ease e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.OutElastic:
                {
                    // elastico suave: dos oscilaciones visibles (biblia 2.2)
                    if (t <= 0f || t >= 1f) return t;
                    return Mathf.Pow(2f, -9f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3.2f)) + 1f;
                }
                case Ease.InBack: { const float c1 = 1.70158f, c3 = c1 + 1f; return c3 * t * t * t - c1 * t * t; }
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InQuad: return t * t;
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case Ease.OutBounce:
                {
                    const float n1 = 7.5625f, d1 = 2.75f;
                    if (t < 1f / d1) return n1 * t * t;
                    if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
                    if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
                    t -= 2.625f / d1;
                    return n1 * t * t + 0.984375f;
                }
            }
            return t;
        }

        /// <summary>Tween generico: apply(u) con u en 0..1 ya con la curva aplicada.</summary>
        public static Entry To(object owner, string key, float dur, Ease ease, Action<float> apply, Action done = null,
            float delay = 0f, Action onStart = null)
        {
            Kill(owner, key);
            Entry e = new Entry
            {
                Owner = owner, Uo = owner as UnityEngine.Object, Key = key, Delay = delay, Dur = Mathf.Max(dur, 0.0001f),
                Ease = ease, Apply = apply, Done = done, OnStart = onStart,
            };
            pending.Add(e);
            return e;
        }

        /// <summary>Pausa y luego una accion (equivale a tween_interval + tween_callback).</summary>
        public static Entry After(object owner, string key, float delay, Action done)
        {
            return To(owner, key, 0.0001f, Ease.Linear, null, done, delay);
        }

        public static void Kill(object owner, string key = null)
        {
            if (owner == null) return;
            for (int i = 0; i < active.Count; i++)
            {
                Entry e = active[i];
                if (e.Dead || !ReferenceEquals(e.Owner, owner)) continue;
                if (key == null || e.Key == key) e.Dead = true;
            }
            for (int i = 0; i < pending.Count; i++)
            {
                Entry e = pending[i];
                if (e.Dead || !ReferenceEquals(e.Owner, owner)) continue;
                if (key == null || e.Key == key) e.Dead = true;
            }
        }

        public static void Tick(float dt)
        {
            if (pending.Count > 0)
            {
                active.AddRange(pending);
                pending.Clear();
            }
            for (int i = 0; i < active.Count; i++)
            {
                Entry e = active[i];
                if (e.Dead) continue;
                if (e.Uo != null || (e.Owner is UnityEngine.Object))
                {
                    if (e.Uo == null) { e.Dead = true; continue; }   // el dueño fue destruido
                }
                float step = dt;
                if (e.Delay > 0f)
                {
                    e.Delay -= dt;
                    if (e.Delay > 0f) continue;
                    step = -e.Delay;
                    e.Delay = 0f;
                }
                if (!e.Started)
                {
                    e.Started = true;
                    if (e.OnStart != null) e.OnStart();
                }
                e.T += step;
                float u = Mathf.Clamp01(e.T / e.Dur);
                if (e.Apply != null) e.Apply(Eval(e.Ease, u));
                if (u >= 1f)
                {
                    e.Dead = true;
                    if (e.Done != null) e.Done();
                }
            }
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].Dead) active.RemoveAt(i);
        }

        // ---------------------------------------------------------------- atajos
        public static Entry Scale(Transform t, Vector3 to, float dur, Ease ease, float delay = 0f, Action done = null)
        {
            Vector3 from = Vector3.one;
            return To(t, "scale", dur, ease, u => { if (t != null) t.localScale = Vector3.LerpUnclamped(from, to, u); }, done, delay,
                () => { if (t != null) from = t.localScale; });
        }

        public static Entry Scale(Transform t, Vector3 from, Vector3 to, float dur, Ease ease, float delay = 0f, Action done = null)
        {
            t.localScale = from;
            return To(t, "scale", dur, ease, u => { if (t != null) t.localScale = Vector3.LerpUnclamped(from, to, u); }, done, delay);
        }

        public static Entry Alpha(CanvasGroup g, float to, float dur, float delay = 0f, Action done = null)
        {
            float from = 1f;
            return To(g, "alpha", dur, Ease.Linear, u => { if (g != null) g.alpha = Mathf.Lerp(from, to, u); }, done, delay,
                () => { if (g != null) from = g.alpha; });
        }

        public static Entry Move(RectTransform rt, Vector2 to, float dur, Ease ease, float delay = 0f, Action done = null)
        {
            Vector2 from = Vector2.zero;
            return To(rt, "move", dur, ease, u => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, u); }, done, delay,
                () => { if (rt != null) from = rt.anchoredPosition; });
        }

        public static Entry MoveFrom(RectTransform rt, Vector2 from, Vector2 to, float dur, Ease ease, float delay = 0f, Action done = null)
        {
            rt.anchoredPosition = from;
            return To(rt, "move", dur, ease, u => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, u); }, done, delay);
        }

        /// <summary>Rebote de aparicion: escala 0 a 1 con TRANS_BACK (0.25 s).</summary>
        public static Entry Reveal(Transform t, float delay = 0f)
        {
            t.localScale = Vector3.zero;
            return Scale(t, Vector3.zero, Vector3.one, 0.25f, Ease.OutBack, delay);
        }

        /// <summary>Golpecito: salta a 1.15 y vuelve a 1 con rebote (como UIK.pop).</summary>
        public static Entry Pop(Transform t, float from = 1.15f)
        {
            return Scale(t, Vector3.one * from, Vector3.one, 0.25f, Ease.OutBack);
        }
    }
}
