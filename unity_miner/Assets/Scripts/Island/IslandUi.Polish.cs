using Mineros.Audio;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.IslandView
{
    /// <summary>
    /// Pulido de la interfaz (biblia 3.9): brillo que recorre los botones que se pueden pagar, boton sin plata que tiembla
    /// en X con el precio en rojo (en el boton, nunca la camara) y la hoja que se va hacia abajo al cerrarse.
    /// </summary>
    public sealed partial class IslandUi
    {
        static Sprite shineSpr;

        static Sprite ShineSprite()
        {
            if (shineSpr != null) return shineSpr;
            const int W = 64, H = 128;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Brillo" };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(u)); a = a * a * (3f - 2f * a);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 150));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            shineSpr = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return shineSpr;
        }

        /// <summary>Brillo que cruza el boton cada 3 s mientras `when` sea verdadero.</summary>
        public static void AddShine(Btn b, System.Func<bool> when)
        {
            if (b == null || b.GetComponent<ShineSweep>() != null) return;
            // mascara solo para el brillo (antes la del boton entero recortaba los iconos que asoman del borde)
            var clip = Kit.New("BrilloClip", b.transform);
            Kit.Stretch(clip);
            clip.gameObject.AddComponent<RectMask2D>();
            var img = Kit.Img(clip, ShineSprite(), Color.white, "Brillo");
            img.raycastTarget = false;
            var s = b.gameObject.AddComponent<ShineSweep>();
            s.Bar = img.rectTransform;
            s.When = when;
            s.Bar.localRotation = Quaternion.Euler(0, 0, -20f);
            s.Bar.gameObject.SetActive(false);
        }

        /// <summary>Sin plata: el boton tiembla 3 veces en X y el texto parpadea en rojo.</summary>
        public void NoMoney(Btn b)
        {
            if (b == null) return;
            Sfx.Play("error", -6f);
            var rt = (RectTransform)b.transform;
            Vector2 p0 = rt.anchoredPosition;
            var lab = b.Label;
            Color c0 = lab != null ? lab.color : Color.white;
            Tw.To(rt, "sinplata", 0.36f, Ease.Linear, u =>
            {
                rt.anchoredPosition = p0 + new Vector2(Mathf.Sin(u * Mathf.PI * 6f) * 10f * (1f - u), 0f);
                if (lab != null) lab.color = Color.Lerp(new Color(1f, 0.35f, 0.3f), c0, u);
            }, () => { rt.anchoredPosition = p0; if (lab != null) lab.color = c0; });
        }
    }

    /// <summary>Barra de brillo que cruza el boton (se mueve sola cada 3 s).</summary>
    public sealed class ShineSweep : MonoBehaviour
    {
        public RectTransform Bar;
        public System.Func<bool> When;
        float t;

        void Update()
        {
            if (Bar == null) return;
            bool on = When == null || When();
            t += Time.unscaledDeltaTime;
            if (t > 3f) t = 0f;
            bool sweeping = on && t < 0.6f;
            if (Bar.gameObject.activeSelf != sweeping) Bar.gameObject.SetActive(sweeping);
            if (!sweeping) return;
            var rt = (RectTransform)transform;
            float w = rt.rect.width, h = rt.rect.height;
            Bar.sizeDelta = new Vector2(h * 0.6f, h * 2f);
            Bar.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.7f, w * 0.7f, t / 0.6f), 0f);
        }
    }
}
