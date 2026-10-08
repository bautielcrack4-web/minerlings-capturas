using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Posprocesado de la camara de la isla: bloom suave (oro, gemas, espuma, chispas), desenfoque de maqueta en los
    /// bordes de arriba y abajo, color "caramelo" y viñeta. Bloom y desenfoque se calculan a media y cuarta resolucion
    /// para que el costo en el telefono sea bajo; la interfaz (Overlay) se dibuja despues y queda nitida.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class IslandPost : MonoBehaviour
    {
        public float Threshold = 0.93f, BloomIntensity = 0.45f;
        public float Saturation = 1.02f, Warmth = 0.015f, Contrast = 1.05f, Vignette = 0.45f, Tilt = 0.8f;
        /// <summary>Tinte general (blanco = sin cambio; azul oscuro de noche).</summary>
        public Color Tint = Color.white;
        Material mat;
        static readonly int IdBloom = Shader.PropertyToID("_Bloom"), IdBlur = Shader.PropertyToID("_Blur");
        const int Levels = 4;
        readonly RenderTexture[] chain = new RenderTexture[Levels];

        void OnEnable()
        {
            var sh = Shader.Find("Hidden/Mineros/IslandPost");
            if (sh == null || !sh.isSupported) { enabled = false; return; }
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable() { if (mat != null) DestroyImmediate(mat); }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null) { Graphics.Blit(src, dst); return; }
            mat.SetFloat("_Threshold", Threshold);
            mat.SetFloat("_BloomInt", BloomIntensity);
            mat.SetFloat("_Sat", Saturation);
            mat.SetFloat("_Warm", Warmth);
            mat.SetFloat("_Contrast", Contrast);
            mat.SetFloat("_Vignette", Vignette);
            mat.SetFloat("_Tilt", Tilt);
            mat.SetColor("_Tint", Tint);
            int w = src.width / 2, h = src.height / 2;
            var fmt = RenderTextureFormat.Default;
            // bloom: umbral a media resolucion y cadena hacia abajo, despues se suma subiendo
            for (int i = 0; i < Levels; i++)
            {
                chain[i] = RenderTexture.GetTemporary(Mathf.Max(1, w >> i), Mathf.Max(1, h >> i), 0, fmt);
                chain[i].filterMode = FilterMode.Bilinear;
            }
            Graphics.Blit(src, chain[0], mat, 0);
            for (int i = 1; i < Levels; i++) Graphics.Blit(chain[i - 1], chain[i], mat, 1);
            for (int i = Levels - 1; i > 0; i--) Graphics.Blit(chain[i], chain[i - 1], mat, 2);
            // desenfoque de maqueta: copia de la escena a un cuarto, suavizada con un octavo
            var b0 = RenderTexture.GetTemporary(Mathf.Max(1, w / 2), Mathf.Max(1, h / 2), 0, fmt);
            var b1 = RenderTexture.GetTemporary(Mathf.Max(1, w / 4), Mathf.Max(1, h / 4), 0, fmt);
            b0.filterMode = b1.filterMode = FilterMode.Bilinear;
            var half = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, half, mat, 1);
            Graphics.Blit(half, b0, mat, 1);
            Graphics.Blit(b0, b1, mat, 1);
            Graphics.Blit(b1, b0, mat, 2);
            mat.SetTexture(IdBloom, chain[0]);
            mat.SetTexture(IdBlur, b0);
            Graphics.Blit(src, dst, mat, 3);
            for (int i = 0; i < Levels; i++) RenderTexture.ReleaseTemporary(chain[i]);
            RenderTexture.ReleaseTemporary(half);
            RenderTexture.ReleaseTemporary(b0);
            RenderTexture.ReleaseTemporary(b1);
        }
    }
}
