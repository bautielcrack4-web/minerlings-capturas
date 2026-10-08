using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>
    /// Lenguaje de movimiento del juego (docs/PLAN_PULIDO.md 2.1): duraciones y presets de resorte en un solo lugar.
    /// Regla: entra con rebote, sale sin rebote; nada que bloquee al jugador dura mas de 0.25 s.
    /// </summary>
    public static class Motion
    {
        public const float Instant = 0.06f, Quick = 0.12f, Base = 0.22f, Slow = 0.40f, Epic = 1.0f;
        public const float Stagger = 0.03f;
        public const int MaxStagger = 8;

        /// <summary>Retraso de la cascada para el elemento i (los que pasan del tope entran juntos).</summary>
        public static float Delay(int i) { return Mathf.Min(i, MaxStagger) * Stagger; }

        /// <summary>"Reducir movimiento" (Ajustes): sin rebotes ni balanceos, solo fundidos.</summary>
        public static bool Reduced;
    }

    /// <summary>
    /// Resorte amortiguado (masa 1) con integracion semi-implicita en subpasos: si el destino cambia a mitad de camino
    /// el movimiento sigue continuo y conserva la velocidad. Struct: sin memoria nueva por cuadro.
    /// </summary>
    public struct Spring
    {
        public float Value, Velocity, Target, K, C;

        public Spring(float k, float zeta, float start = 0f)
        {
            K = k; C = 2f * zeta * Mathf.Sqrt(k); Value = Target = start; Velocity = 0f;
        }

        public static Spring Snappy(float v = 0f) { return new Spring(600f, 0.75f, v); }
        public static Spring Bouncy(float v = 0f) { return new Spring(300f, 0.45f, v); }
        public static Spring Heavy(float v = 0f) { return new Spring(180f, 0.9f, v); }
        public static Spring Soft(float v = 0f) { return new Spring(120f, 1f, v); }

        public float Step(float dt)
        {
            dt = Mathf.Min(dt, 1f / 30f);
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 240f)));
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                float a = -K * (Value - Target) - C * Velocity;
                Velocity += a * h;
                Value += Velocity * h;
            }
            return Value;
        }

        public bool Resting { get { return Mathf.Abs(Value - Target) < 0.001f && Mathf.Abs(Velocity) < 0.01f; } }
        public void Snap(float v) { Value = Target = v; Velocity = 0f; }
    }

    /// <summary>Resorte de tres ejes (posiciones del mundo).</summary>
    public struct Spring3
    {
        public Vector3 Value, Velocity, Target;
        public float K, C;

        public Spring3(float k, float zeta, Vector3 start)
        {
            K = k; C = 2f * zeta * Mathf.Sqrt(k); Value = Target = start; Velocity = Vector3.zero;
        }

        public Vector3 Step(float dt)
        {
            dt = Mathf.Min(dt, 1f / 30f);
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 240f)));
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = -K * (Value - Target) - C * Velocity;
                Velocity += a * h;
                Value += Velocity * h;
            }
            return Value;
        }

        public void Snap(Vector3 v) { Value = Target = v; Velocity = Vector3.zero; }
    }

    /// <summary>
    /// Pila de imagenes de UI reutilizables (monedas voladoras, estelas, chispas): cero Instantiate/Destroy al jugar,
    /// asi no hay basura de memoria ni tirones en las cadenas de cobro.
    /// </summary>
    public static class UiPool
    {
        static readonly Stack<Image> free = new Stack<Image>();

        public static Image Get(Transform parent, Sprite sprite, Color col, string name)
        {
            Image im = null;
            while (free.Count > 0 && im == null) im = free.Pop();
            if (im == null)
            {
                im = Kit.Img(parent, sprite, col, name);
            }
            else
            {
                im.transform.SetParent(parent, false);
                im.gameObject.SetActive(true);
                im.sprite = sprite;
                im.color = col;
                im.name = name;
            }
            var rt = im.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            im.preserveAspect = false;
            im.type = Image.Type.Simple;
            im.raycastTarget = false;
            return im;
        }

        public static void Release(Image im)
        {
            if (im == null) return;
            Tw.Kill(im.rectTransform);
            Tw.Kill(im);
            im.gameObject.SetActive(false);
            if (free.Count < 120) free.Push(im);
            else Object.Destroy(im.gameObject);
        }
    }
}
