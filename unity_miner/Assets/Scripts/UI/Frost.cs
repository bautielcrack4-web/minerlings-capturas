using UnityEngine;

namespace Mineros.UI
{
    /// <summary>
    /// Vidrio esmerilado detras de las hojas (docs/PLAN_PULIDO.md 2.5): una sola captura del mundo a 1/8 de resolucion,
    /// desenfocada en la CPU (dos pasadas separables) y mostrada estirada (el filtro bilineal termina de suavizar).
    /// Es estatica: cuesta lo mismo que una imagen mientras la hoja esta abierta.
    /// </summary>
    public static class Frost
    {
        static RenderTexture rt;
        static Texture2D tex;
        static Color32[] a, b;

        public static Texture2D Capture(Camera cam)
        {
            if (cam == null) return null;
            int w = Mathf.Max(32, cam.pixelWidth / 8), h = Mathf.Max(32, cam.pixelHeight / 8);
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "Esmerilado" };
                tex = new Texture2D(w, h, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Esmerilado" };
                a = new Color32[w * h]; b = new Color32[w * h];
            }
            var prevT = cam.targetTexture;
            var prevA = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevT;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = prevA;
            var src = tex.GetPixels32();
            System.Array.Copy(src, a, a.Length);
            Blur(a, b, w, h, true, 3);
            Blur(b, a, w, h, false, 3);
            Blur(a, b, w, h, true, 2);
            Blur(b, a, w, h, false, 2);
            tex.SetPixels32(a);
            tex.Apply(false);
            return tex;
        }

        static void Blur(Color32[] src, Color32[] dst, int w, int h, bool horiz, int r)
        {
            int n = 2 * r + 1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sr = 0, sg = 0, sb = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int xx = horiz ? Mathf.Clamp(x + k, 0, w - 1) : x;
                        int yy = horiz ? y : Mathf.Clamp(y + k, 0, h - 1);
                        var c = src[yy * w + xx];
                        sr += c.r; sg += c.g; sb += c.b;
                    }
                    dst[y * w + x] = new Color32((byte)(sr / n), (byte)(sg / n), (byte)(sb / n), 255);
                }
        }
    }
}
