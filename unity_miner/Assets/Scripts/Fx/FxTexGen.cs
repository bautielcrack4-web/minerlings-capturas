using System;

namespace Mineros.Fx
{
    /// <summary>Texturas de particulas disponibles (todas generadas por codigo, formas gordas y redondeadas).</summary>
    internal enum FxTex
    {
        SoftCircle, HardCircle, Star4, Cross, Spark, Dust, Ring, Confetti, Coin, Gem, Smoke, Lava, Chunk, Shard
    }

    /// <summary>
    /// Generador PURO (sin UnityEngine) de las texturas de particulas: devuelve RGBA8 recto (sin premultiplicar).
    /// Al ser puro, lo comparten el juego y la herramienta de vista previa (unity_tools/fx_preview).
    /// Convencion: x a la derecha, y hacia arriba, ambos en [-1, 1]. Todo en blanco/grises (se tine con el color de la
    /// particula) salvo la moneda y la chispa de lava, que llevan su color.
    /// </summary>
    internal static class FxTexGen
    {
        /// <summary>Si es true la chispa alargada se dibuja a lo largo del eje X (por defecto a lo largo de Y).</summary>
        internal static bool SparkAlongX;

        internal static int SizeOf(FxTex t)
        {
            switch (t)
            {
                case FxTex.Star4: case FxTex.Cross: case FxTex.Ring: case FxTex.Dust: case FxTex.Smoke: return 128;
                default: return 64;
            }
        }

        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        static float Smooth(float a, float b, float x)
        {
            float t = Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        static float Len(float x, float y) { return (float)Math.Sqrt(x * x + y * y); }
        static float Abs(float v) { return v < 0f ? -v : v; }
        static float Max(float a, float b) { return a > b ? a : b; }
        static float Min(float a, float b) { return a < b ? a : b; }

        // minimo suave (union redondeada de circulos)
        static float SMin(float a, float b, float k)
        {
            float h = Clamp01(0.5f + 0.5f * (b - a) / k);
            return Lerp(b, a, h) - k * h * (1f - h);
        }

        static float SdCircle(float x, float y, float cx, float cy, float r) { return Len(x - cx, y - cy) - r; }

        static float SdRoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Abs(x) - hx + r, qy = Abs(y) - hy + r;
            float ox = Max(qx, 0f), oy = Max(qy, 0f);
            return Len(ox, oy) + Min(Max(qx, qy), 0f) - r;
        }

        // rombo de semiejes (bx, by) con esquinas redondeadas: distancia al borde, con signo, menos 'round'
        static float SdRhombus(float x, float y, float bx, float by, float round)
        {
            float px = Abs(x), py = Abs(y);
            float dirx = -bx, diry = by;                   // borde de (bx,0) a (0,by)
            float ax = px - bx, ay = py;
            float t = Clamp01((ax * dirx + ay * diry) / (dirx * dirx + diry * diry));
            float cx = bx + dirx * t, cy = diry * t;
            float d = Len(px - cx, py - cy);
            float sign = (px / bx + py / by) > 1f ? 1f : -1f;
            return d * sign - round;
        }

        static float Aa(int n) { return 2.2f / n; }

        /// <summary>Genera la textura (SizeOf(t) x SizeOf(t), RGBA8, fila 0 = abajo como Unity).</summary>
        internal static byte[] Make(FxTex tex)
        {
            int n = SizeOf(tex);
            var px = new byte[n * n * 4];
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    // 2x2 supermuestreo
                    float r = 0, g = 0, b = 0, a = 0, ur = 0, ug = 0, ub = 0;
                    for (int sj = 0; sj < 2; sj++)
                    {
                        for (int si = 0; si < 2; si++)
                        {
                            float x = ((i + 0.25f + 0.5f * si) / n) * 2f - 1f;
                            float y = ((j + 0.25f + 0.5f * sj) / n) * 2f - 1f;
                            float pr, pg, pb, pa;
                            Pixel(tex, n, x, y, out pr, out pg, out pb, out pa);
                            // promedio ponderado por alpha para no oscurecer bordes
                            r += pr * pa; g += pg * pa; b += pb * pa; a += pa;
                            ur += pr; ug += pg; ub += pb;
                        }
                    }
                    bool solid = a > 1e-5f;   // sin alpha: se conserva el color vecino (evita bordes oscuros en los mipmaps)
                    float aw = solid ? a : 4f;
                    int o = (j * n + i) * 4;
                    px[o] = (byte)(Clamp01((solid ? r : ur) / aw) * 255f + 0.5f);
                    px[o + 1] = (byte)(Clamp01((solid ? g : ug) / aw) * 255f + 0.5f);
                    px[o + 2] = (byte)(Clamp01((solid ? b : ub) / aw) * 255f + 0.5f);
                    px[o + 3] = (byte)(Clamp01(a * 0.25f) * 255f + 0.5f);
                }
            }
            return px;
        }

        static readonly float[] GemShade = { 0.62f, 0.55f, 0.70f, 0.82f, 1.0f, 0.93f, 0.80f, 0.70f };

        static void Pixel(FxTex tex, int n, float x, float y, out float r, out float g, out float b, out float a)
        {
            r = g = b = 1f; a = 0f;
            float aa = Aa(n);
            switch (tex)
            {
                case FxTex.SoftCircle:
                {
                    float d = Len(x, y);
                    float f = Clamp01(1f - d);
                    a = Clamp01((float)Math.Pow(f, 1.7) * 1.05f) * Smooth(1f, 0.85f, d);
                    break;
                }
                case FxTex.HardCircle:
                {
                    float R = 0.84f, d = Len(x, y);
                    a = 1f - Smooth(R - aa, R + aa, d);
                    float light = Clamp01(0.5f + 0.5f * (-x * 0.55f + y * 0.8f) / R);
                    float s = 0.80f + 0.20f * light;
                    s *= 1f - 0.22f * Smooth(R - 0.22f, R - 0.04f, d);   // borde mas oscuro
                    r = g = b = s;
                    break;
                }
                case FxTex.Star4:
                {
                    float ax = Abs(x) / 0.96f, ay = Abs(y) / 0.96f;
                    float f = (float)(Math.Pow(ax, 0.72) + Math.Pow(ay, 0.72));
                    float shape = 1f - Smooth(0.86f, 1.0f, f);
                    float d = Len(x, y);
                    float core = (float)Math.Pow(Clamp01(1f - d * 1.55f), 1.6) * 0.95f;
                    a = Max(shape, core);
                    break;
                }
                case FxTex.Cross:
                {
                    float sy = 0.065f;
                    float bh = (float)Math.Pow(Clamp01(1f - Abs(x) / 0.98f), 2.2) * (float)Math.Exp(-(y * y) / (2f * sy * sy));
                    float bv = (float)Math.Pow(Clamp01(1f - Abs(y) / 0.98f), 2.2) * (float)Math.Exp(-(x * x) / (2f * sy * sy));
                    float d2 = x * x + y * y;
                    float core = (float)Math.Exp(-d2 / (2f * 0.15f * 0.15f));
                    a = Clamp01(Max(bh, bv) + core * 0.95f);
                    break;
                }
                case FxTex.Spark:
                {
                    float u = SparkAlongX ? x : y;      // eje largo
                    float v = SparkAlongX ? y : x;      // eje corto
                    float au = Abs(u);
                    float w = 0.27f * (float)Math.Pow(Clamp01(1f - (float)Math.Pow(au, 1.5)), 0.85);
                    float shape = 1f - Smooth(w - aa * 1.2f, w + aa * 1.2f, Abs(v));
                    a = shape * (0.6f + 0.4f * (1f - au));
                    float core = (float)Math.Exp(-(v * v) / (2f * 0.05f * 0.05f)) * Clamp01(1f - au);
                    a = Clamp01(Max(a, core));
                    break;
                }
                case FxTex.Dust:
                    Cloud(x, y, 0.10f, 0.9f, 0.86f, true, out r, out a);
                    g = b = r;
                    break;
                case FxTex.Smoke:
                    Cloud(x, y, 0.30f, 0.82f, 0.94f, false, out r, out a);
                    g = b = r;
                    break;
                case FxTex.Ring:
                {
                    float d = Len(x, y), R = 0.80f;
                    float ring = 1f - Smooth(0.035f, 0.095f, Abs(d - R));
                    float glow = 0.2f * (float)Math.Exp(-((d - R) * (d - R)) / (2f * 0.06f * 0.06f));
                    a = Clamp01(ring + glow) * Smooth(1f, 0.93f, d);
                    break;
                }
                case FxTex.Confetti:
                {
                    float sd = SdRoundBox(x, y, 0.46f, 0.80f, 0.2f);
                    a = 1f - Smooth(-aa, aa, sd);
                    float s = x < 0.03f ? 1f : 0.82f;
                    s = Lerp(s, s * 1.08f, Smooth(0.35f, 0.75f, y));
                    r = g = b = Min(1f, s);
                    break;
                }
                case FxTex.Coin:
                {
                    float R = 0.86f, d = Len(x, y);
                    a = 1f - Smooth(R - aa, R + aa, d);
                    // oro #FFCC33 (cara) y #D9961C (borde)
                    float rim = Smooth(0.60f, 0.64f, d);
                    float cr = Lerp(1f, 0.85f, rim), cg = Lerp(0.80f, 0.59f, rim), cb = Lerp(0.20f, 0.11f, rim);
                    float line = (1f - Smooth(0.0f, 0.045f, Abs(d - 0.52f))) * 0.8f;   // linea grabada
                    cr = Lerp(cr, 0.85f, line); cg = Lerp(cg, 0.59f, line); cb = Lerp(cb, 0.11f, line);
                    float em = (1f - Smooth(-aa, aa, SdRhombus(x, y, 0.30f, 0.36f, 0.05f))) * 0.85f;   // emblema
                    cr = Lerp(cr, 1f, em); cg = Lerp(cg, 0.92f, em); cb = Lerp(cb, 0.55f, em);
                    float gl = Clamp01(1f - Len(x + 0.38f, y - 0.42f) / 0.34f);   // brillo superior izquierdo
                    gl = gl * gl * 0.8f;
                    cr = Lerp(cr, 1f, gl); cg = Lerp(cg, 1f, gl); cb = Lerp(cb, 0.9f, gl);
                    r = cr; g = cg; b = cb;
                    break;
                }
                case FxTex.Gem:
                {
                    float sd = SdRhombus(x, y, 0.70f, 0.90f, 0.10f);
                    a = 1f - Smooth(-aa, aa, sd);
                    float inner = SdRhombus(x, y + 0.02f, 0.34f, 0.44f, 0.03f);
                    float ang = (float)Math.Atan2(y, x);               // -pi..pi
                    int sector = (int)Math.Floor((ang + Math.PI) / (Math.PI / 4.0));
                    if (sector > 7) sector = 7;
                    if (sector < 0) sector = 0;
                    float s = GemShade[sector];
                    if (inner < 0f) s = Lerp(s, 1f, 0.55f);
                    s *= 1f - 0.15f * Smooth(-0.07f, 0.0f, sd);
                    float hl = Clamp01(1f - Len(x + 0.22f, y - 0.30f) / 0.18f);
                    s = Lerp(s, 1f, hl * hl);
                    r = g = b = Min(1f, s);
                    break;
                }
                case FxTex.Lava:
                {
                    float d = Len(x, y);
                    a = 1f - Smooth(0.55f, 0.92f, d);
                    float t = Smooth(0.0f, 0.75f, d);
                    r = 1f;                                   // nucleo amarillo palido -> naranja -> rojo
                    g = Lerp(0.96f, 0.32f, t);
                    b = Lerp(0.72f, 0.08f, t);
                    break;
                }
                case FxTex.Chunk:
                {
                    // fragmento de roca: caja redonda rotada con dos planos de luz
                    const float c = 0.9659f, s = 0.2588f;           // 15 grados
                    float rx = x * c - y * s, ry = x * s + y * c;
                    float sd = SdRoundBox(rx, ry, 0.64f, 0.58f, 0.30f);
                    a = 1f - Smooth(-aa, aa, sd);
                    float plane = Clamp01(0.5f + (-x * 0.6f + y * 0.8f) * 1.6f);
                    float sh = Lerp(0.62f, 1f, Smooth(0.15f, 0.85f, plane));
                    sh *= 1f - 0.12f * Smooth(-0.12f, 0f, sd);
                    r = g = b = sh;
                    break;
                }
                case FxTex.Shard:
                {
                    float sd = SdRhombus(x, y, 0.36f, 0.90f, 0.08f);
                    a = 1f - Smooth(-aa, aa, sd);
                    float sh = x < -0.02f ? 1f : 0.70f;
                    sh = Lerp(sh, Min(1f, sh * 1.12f), Smooth(0.2f, 0.8f, y));
                    float hl = Clamp01(1f - Len(x + 0.08f, y - 0.34f) / 0.2f);
                    sh = Lerp(sh, 1f, hl * hl * 0.8f);
                    r = g = b = Min(1f, sh);
                    break;
                }
            }
        }

        // nube redondeada como union suave de circulos; k = redondez de la union, hard = borde definido
        static void Cloud(float x, float y, float k, float scale, float bottomShade, bool hard, out float shade, out float alpha)
        {
            float d = SdCircle(x, y, 0.0f, 0.02f, 0.46f * scale);
            d = SMin(d, SdCircle(x, y, -0.40f * scale, -0.06f * scale, 0.33f * scale), k);
            d = SMin(d, SdCircle(x, y, 0.40f * scale, -0.08f * scale, 0.34f * scale), k);
            d = SMin(d, SdCircle(x, y, -0.17f * scale, 0.32f * scale, 0.29f * scale), k);
            d = SMin(d, SdCircle(x, y, 0.22f * scale, 0.30f * scale, 0.27f * scale), k);
            d = SMin(d, SdCircle(x, y, 0.02f * scale, -0.32f * scale, 0.28f * scale), k);
            float soft = hard ? 0.06f : 0.28f;
            alpha = 1f - Smooth(-soft, soft * 0.6f, d);
            float light = Smooth(-0.7f, 0.7f, y * 0.85f - x * 0.25f);
            shade = Lerp(bottomShade, 1f, light);
            shade *= 1f - 0.05f * Smooth(-0.22f, 0.0f, d);
        }
    }
}
