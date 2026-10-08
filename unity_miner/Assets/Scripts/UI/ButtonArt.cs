using System.Collections.Generic;
using UnityEngine;

namespace Mineros.UI
{
    /// <summary>
    /// Botones con profundidad dibujados por codigo (docs/PLAN_PULIDO.md 2.4), a 4x y con bordes analiticos:
    /// sombra suave, labio inferior (el costado del boton, mas oscuro), cara con degradado, brillo "caramelo" arriba y un
    /// bisel claro. Presionado: el labio se achica y la cara baja (se hunde, no se encoge). Un solo sprite 9-slice por
    /// color y estado, guardado en cache.
    /// </summary>
    public static class ButtonArt
    {
        const int S = 4;                       // pixeles por unidad de canvas
        const float W = 64f, H = 74f;          // tamaño del sprite en unidades
        const float Y0 = 5f;                   // base (lugar para la sombra)
        const float FaceH = 58f;               // alto de la cara
        public const float LipN = 7f, LipP = 2f;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Margenes de contenido (izq, arriba, der, abajo) normal y presionado, para BtnSkin.</summary>
        public static readonly Vector4 CmNormal = new Vector4(10, H - (Y0 + LipN + FaceH) + 3, 10, Y0 + LipN + 3);
        public static readonly Vector4 CmPressed = new Vector4(10, H - (Y0 + LipP + FaceH) + 3, 10, Y0 + LipP + 3);

        public static Color Lip(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            return Color.HSVToRGB(Mathf.Repeat(h + 0.012f, 1f), Mathf.Min(1f, s * 1.08f + 0.05f), v * 0.66f);
        }

        /// <summary>state: 0 normal, 1 presionado, 2 deshabilitado. glass: sin labio, cara translucida oscura.</summary>
        public static Sprite Get(Color face, bool square, int state)
        {
            string key = ColorUtility.ToHtmlStringRGBA(face) + (square ? "S" : "B") + state;
            Sprite sp;
            if (cache.TryGetValue(key, out sp)) return sp;
            if (state == 2)
            {
                float g = face.grayscale * 0.55f + 0.38f;
                face = new Color(g, g, g * 1.02f, 1f);
            }
            float lip = state == 1 ? LipP : state == 2 ? 3f : LipN;
            float rad = square ? 15f : 18f;
            int pw = (int)(W * S), ph = (int)(H * S);
            var px = new Color32[pw * ph];
            Color lipC = Lip(face);
            Color top = Color.Lerp(face, Color.white, 0.14f), bot = Color.Lerp(face, Color.black, 0.06f);
            Color rim = Color.Lerp(face, Color.white, 0.45f);
            float faceX0 = 3f, faceX1 = W - 3f;
            float faceY0 = Y0 + lip, faceY1 = Y0 + lip + FaceH;
            for (int y = 0; y < ph; y++)
            {
                float uy = (y + 0.5f) / S;
                for (int x = 0; x < pw; x++)
                {
                    float ux = (x + 0.5f) / S;
                    // sombra: rectangulo redondeado bajo el boton, muy suave
                    float dSh = RR(ux, uy, faceX0 + 1f, Y0 - 3.5f, faceX1 - 1f, Y0 + FaceH * 0.5f, rad);
                    float aSh = (state == 1 ? 0.12f : 0.2f) * Mathf.Clamp01(1f - (dSh + 1f) / 6f);
                    Color c = new Color(0f, 0f, 0f, aSh);
                    // labio: del piso a la cara
                    float dLip = RR(ux, uy, faceX0, Y0, faceX1, Y0 + FaceH, rad);
                    float aLip = Mathf.Clamp01(0.5f - dLip * S / 1.2f);
                    if (aLip > 0f) c = Over(c, new Color(lipC.r, lipC.g, lipC.b, aLip));
                    // cara
                    float dFace = RR(ux, uy, faceX0, faceY0, faceX1, faceY1, rad);
                    float aFace = Mathf.Clamp01(0.5f - dFace * S / 1.2f);
                    if (aFace > 0f)
                    {
                        float t = Mathf.InverseLerp(faceY0, faceY1, uy);
                        Color fc = Color.Lerp(bot, top, t);
                        // brillo caramelo: franja del tercio de arriba con borde suave abajo
                        float hl = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(faceY1 - FaceH * 0.42f, faceY1 - FaceH * 0.30f, uy));
                        float inner = RR(ux, uy, faceX0 + 4f, faceY0 + 4f, faceX1 - 4f, faceY1 - 3.5f, rad - 4f);
                        hl *= Mathf.Clamp01(0.5f - inner * S / 3f);
                        fc = Color.Lerp(fc, Color.white, hl * (state == 2 ? 0.1f : 0.24f));
                        // bisel: borde interior claro arriba, que se apaga hacia abajo
                        float edge = Mathf.Clamp01(1f - Mathf.Abs(dFace + 1.6f) / 1.1f) * Mathf.Lerp(0.15f, 0.85f, t);
                        fc = Color.Lerp(fc, rim, edge * (state == 2 ? 0.2f : 0.55f));
                        c = Over(c, new Color(fc.r, fc.g, fc.b, aFace));
                    }
                    px[y * pw + x] = c;
                }
            }
            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Boton" + key };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            // 9-slice: (izquierda, abajo, derecha, arriba) en pixeles; el centro estirable cae en la parte recta de todo
            var border = new Vector4(22f, 30f, 22f, 30f) * S;
            sp = Sprite.Create(tex, new Rect(0, 0, pw, ph), new Vector2(0.5f, 0.5f), 100f * S, 0, SpriteMeshType.FullRect, border);
            cache[key] = sp;
            return sp;
        }

        /// <summary>
        /// Caja con profundidad para paneles y tarjetas: cara de color con degradado muy suave, labio inferior, sombra
        /// grande y un bisel claro arriba. 9-slice de radio `rad` (en unidades).
        /// </summary>
        public static Sprite Box(Color face, float rad, float lip, float shadow)
        {
            string key = "box" + ColorUtility.ToHtmlStringRGBA(face) + rad + "_" + lip + "_" + shadow;
            Sprite sp;
            if (cache.TryGetValue(key, out sp)) return sp;
            float pad = 10f;   // lugar para la sombra
            float w = rad * 2f + pad * 2f + 8f, h = rad * 2f + pad * 2f + lip + 8f;
            int pw = (int)(w * S), ph = (int)(h * S);
            var px = new Color32[pw * ph];
            Color lipC = Lip(face);
            Color top = Color.Lerp(face, Color.white, 0.06f), bot = Color.Lerp(face, Color.black, 0.03f);
            Color rim = Color.Lerp(face, Color.white, 0.6f);
            float x0 = pad, x1 = w - pad, y0 = pad, y1 = h - pad;
            for (int y = 0; y < ph; y++)
            {
                float uy = (y + 0.5f) / S;
                for (int x = 0; x < pw; x++)
                {
                    float ux = (x + 0.5f) / S;
                    float dSh = RR(ux, uy, x0 + 2f, y0 - 5f, x1 - 2f, y1 - 6f, rad);
                    float aSh = shadow * Mathf.Clamp01(1f - (dSh + 2f) / 10f);
                    Color c = new Color(0f, 0f, 0f, aSh);
                    if (lip > 0f)
                    {
                        float dLip = RR(ux, uy, x0, y0, x1, y1 - lip, rad);
                        float aLip = Mathf.Clamp01(0.5f - dLip * S / 1.2f);
                        if (aLip > 0f) c = Over(c, new Color(lipC.r, lipC.g, lipC.b, aLip));
                    }
                    float dF = RR(ux, uy, x0, y0 + lip, x1, y1, rad);
                    float aF = Mathf.Clamp01(0.5f - dF * S / 1.2f);
                    if (aF > 0f)
                    {
                        float t = Mathf.InverseLerp(y0 + lip, y1, uy);
                        Color fc = Color.Lerp(bot, top, t);
                        float edge = Mathf.Clamp01(1f - Mathf.Abs(dF + 1.5f) / 1.1f) * Mathf.Lerp(0.1f, 0.9f, t);
                        fc = Color.Lerp(fc, rim, edge * 0.5f);
                        c = Over(c, new Color(fc.r, fc.g, fc.b, aF * face.a));
                    }
                    px[y * pw + x] = c;
                }
            }
            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Caja" + key };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = rad + pad + 2f;
            var border = new Vector4(b, b + lip, b, b) * S;
            sp = Sprite.Create(tex, new Rect(0, 0, pw, ph), new Vector2(0.5f, 0.5f), 100f * S, 0, SpriteMeshType.FullRect, border);
            cache[key] = sp;
            return sp;
        }

        /// <summary>Distancia con signo a un rectangulo redondeado (negativa adentro), en unidades.</summary>
        static float RR(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
            float qx = Mathf.Abs(x - cx) - hx, qy = Mathf.Abs(y - cy) - hy;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 1e-4f) return new Color(0, 0, 0, 0);
            return new Color((src.r * src.a + dst.r * dst.a * (1f - src.a)) / a, (src.g * src.a + dst.g * dst.a * (1f - src.a)) / a,
                (src.b * src.a + dst.b * dst.a * (1f - src.a)) / a, a);
        }
    }
}
