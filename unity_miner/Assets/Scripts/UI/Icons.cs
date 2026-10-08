using System.Collections.Generic;
using UnityEngine;

namespace Mineros.UI
{
    /// <summary>
    /// Iconos y sprites dibujados por codigo (port de Art.icon / Art.miner / Art.tool_shape de Godot) con el
    /// rasterizador propio. Todo se genera una sola vez y queda cacheado por nombre.
    /// </summary>
    public static class Icons
    {
        /// <summary>Lado del lienzo de un icono, en unidades de diseño (el icono ocupa ~40 y centra en 24).</summary>
        public const float Canvas = 48f;
        /// <summary>Un control de tamaño s muestra el lienzo ampliado por este factor (48/40).</summary>
        public const float Over = Canvas / 40f;

        public static readonly Color Out = Painter.Out;
        public static readonly Color Skin = H("f2c49b");
        public static readonly Color SkinD = H("d9a27a");
        public static readonly Color Beard = H("8a5a3a");
        public static readonly Color Overall = H("3f6fb5");
        public static readonly Color Shirt = H("e0703a");
        public static readonly Color Boot = H("4a3426");
        public static readonly Color Handle = H("9b6a3c");
        public static readonly Color Gold = H("ffcc33");
        public static readonly Color GoldD = H("d9961c");
        public static readonly Color Gem = H("4fc3f7");
        public static readonly Color GemD = H("1f7fc4");
        public static readonly Color[] RankCols =
        {
            H("a9b3bd"), H("6fd36a"), H("4aa3f0"), H("b06ef0"), H("ffcf3a"), H("ff5a4e"),
        };

        /// <summary>Color de rango (0 D .. 5 SS) de la paleta unica del juego (Palette.Rank). Si el kit de arte todavia no esta
        /// (devuelve gris/magenta de reemplazo) usa los valores locales de arriba, que son los mismos del ART_BIBLE.</summary>
        public static Color RankCol(int r)
        {
            r = Mathf.Clamp(r, 0, RankCols.Length - 1);
            Color c = Mineros.Art.Palette.Rank(r);
            if (IsPlaceholder(c)) return RankCols[r];
            return c;
        }

        /// <summary>Color con nombre de la paleta del bioma; `fallback` si el kit de arte todavia devuelve el reemplazo.</summary>
        public static Color BiomeCol(int biome, string key, Color fallback)
        {
            Color c = Mineros.Art.Palette.Get(Mathf.Clamp(biome, 0, 3), key);
            return IsPlaceholder(c) ? fallback : c;
        }

        static bool IsPlaceholder(Color c)
        {
            return (Mathf.Approximately(c.r, 1f) && Mathf.Approximately(c.g, 0f) && Mathf.Approximately(c.b, 1f))
                || (Mathf.Approximately(c.r, 0.5f) && Mathf.Approximately(c.g, 0.5f) && Mathf.Approximately(c.b, 0.5f));
        }

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Color H(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.magenta;
        }

        static Color Darkened(Color c, float a) { return new Color(c.r * (1f - a), c.g * (1f - a), c.b * (1f - a), c.a); }
        static Color Lightened(Color c, float a) { return new Color(c.r + (1f - c.r) * a, c.g + (1f - c.g) * a, c.b + (1f - c.b) * a, c.a); }
        static Vector2 V(float x, float y) { return new Vector2(x, y); }
        static Rect R(Vector2 p, Vector2 s) { return new Rect(p.x, p.y, s.x, s.y); }

        static Sprite MakeSprite(Texture2D t)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        // ---------------------------------------------------------------- API
        /// <summary>Icono de interfaz por nombre (coin, gem, power, speed, money, gear, trophy, shop, rebirth, skin, tool, pick,
        /// hammer, piggy, auto, play, boost, mission, lock, chest, clock, close, star, boss, miner, arrow).</summary>
        public static Sprite Get(string kind)
        {
            Sprite s;
            string key = "i:" + kind;
            if (cache.TryGetValue(key, out s)) return s;
            Painter p = new Painter(192, 192, 4f, 3);
            Icon(p, kind, V(24f, 24f), 1f);
            s = MakeSprite(p.ToTexture());
            cache[key] = s;
            return s;
        }

        /// <summary>Herramienta ya rotada 0.55 rad como en las celdas del Equipo. Origen (mango) en (24,72) de un lienzo de 84x84.</summary>
        public static Sprite Tool(int k, int r)
        {
            Sprite s;
            string key = "t:" + k + ":" + r;
            if (cache.TryGetValue(key, out s)) return s;
            Painter p = new Painter(210, 210, 2.5f, 3);
            p.Push();
            p.Transform(V(24f, 72f), 0.55f, 1f);
            ToolShape(p, k, r);
            p.Pop();
            s = MakeSprite(p.ToTexture());
            cache[key] = s;
            return s;
        }

        public const float ToolCanvas = 84f;
        public static readonly Vector2 ToolOrigin = new Vector2(24f, 72f);

        /// <summary>Minero de pie con casco del color pedido (para las vistas previas de cascos). Lienzo 110x120.</summary>
        public static Sprite MinerBust(Color helmet)
        {
            Sprite s;
            string key = "m:" + ColorUtility.ToHtmlStringRGB(helmet);
            if (cache.TryGetValue(key, out s)) return s;
            Painter p = new Painter(220, 240, 2f, 3);
            Miner(p, V(55f, 108f), 1f, 1f, 0f, false, -0.4f, helmet, 0, 0, false);
            s = MakeSprite(p.ToTexture());
            cache[key] = s;
            return s;
        }

        public const float MinerCanvasW = 110f, MinerCanvasH = 120f;

        /// <summary>Resplandor radial blanco con alpha (1 al centro, 0.45 a 0.35, 0 en el borde).</summary>
        public static Sprite Glow()
        {
            Sprite s;
            if (cache.TryGetValue("glow", out s)) return s;
            const int N = 128;
            Color32[] px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (d >= 1f) a = 0f;
                    else if (d < 0.35f) a = Mathf.Lerp(1f, 0.45f, d / 0.35f);
                    else a = Mathf.Lerp(0.45f, 0f, (d - 0.35f) / 0.65f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.Apply(false, true);
            s = MakeSprite(t);
            cache["glow"] = s;
            return s;
        }

        /// <summary>Vineta suave: transparente al centro y oscura en las esquinas.</summary>
        public static Sprite Vignette()
        {
            Sprite s;
            if (cache.TryGetValue("vig", out s)) return s;
            const int N = 64;
            Color32[] px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                    a = a * a * 0.38f;
                    px[y * N + x] = new Color32(20, 12, 6, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.Apply(false, true);
            s = MakeSprite(t);
            cache["vig"] = s;
            return s;
        }

        /// <summary>Estrella de destello con contorno (lienzo 32).</summary>
        public static Sprite Spark()
        {
            Sprite s;
            if (cache.TryGetValue("spark", out s)) return s;
            Painter p = new Painter(128, 128, 4f, 3);
            p.Star(V(16f, 16f), 14f, Out);
            p.Star(V(16f, 16f), 11.5f, new Color(1f, 0.95f, 0.7f, 1f));
            s = MakeSprite(p.ToTexture());
            cache["spark"] = s;
            return s;
        }

        /// <summary>Monedas (r=14 -> lienzo 40) y gemas (r=15 -> lienzo 40).</summary>
        public static Sprite CoinSprite()
        {
            Sprite s;
            if (cache.TryGetValue("coin", out s)) return s;
            Painter p = new Painter(160, 160, 4f, 3);
            Coin(p, V(20f, 20f), 14f, 1f);
            s = MakeSprite(p.ToTexture());
            cache["coin"] = s;
            return s;
        }

        public static Sprite GemSprite()
        {
            Sprite s;
            if (cache.TryGetValue("gemf", out s)) return s;
            Painter p = new Painter(160, 160, 4f, 3);
            GemShape(p, V(20f, 20f), 15f);
            s = MakeSprite(p.ToTexture());
            cache["gemf"] = s;
            return s;
        }

        /// <summary>Anillo blanco (radio 56 en un lienzo de 128) con el grosor pedido.</summary>
        public static Sprite Ring(float width)
        {
            Sprite s;
            string key = "ring" + width;
            if (cache.TryGetValue(key, out s)) return s;
            s = MakeSprite(RingTex(width));
            cache[key] = s;
            return s;
        }

        /// <summary>Anillo analitico (radio 56 y grosor en unidades de un lienzo de 128) a 512 px: borde suave y parejo.</summary>
        public static Texture2D RingTex(float width)
        {
            const int N = 512;
            const float k = N / 128f;
            float R = 56f * k, hw = width * 0.5f * k;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - R) - hw;
                    float a = Mathf.Clamp01(0.5f - d / 1.2f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        public static Sprite Dot()
        {
            Sprite s;
            if (cache.TryGetValue("dot", out s)) return s;
            Painter p = new Painter(64, 64, 4f, 3);
            p.Circle(V(8f, 8f), 8f, Color.white);
            s = MakeSprite(p.ToTexture());
            cache["dot"] = s;
            return s;
        }

        /// <summary>Punto rojo de aviso (lienzo 26 con contorno y brillo).</summary>
        public static Sprite BadgeDot()
        {
            Sprite s;
            if (cache.TryGetValue("badge", out s)) return s;
            Painter p = new Painter(104, 104, 4f, 3);
            p.Circle(V(13f, 13f), 13f, Out);
            p.Circle(V(13f, 13f), 10.5f, H("e5484d"));
            p.Circle(V(10f, 9.5f), 3f, new Color(1f, 1f, 1f, 0.45f));
            s = MakeSprite(p.ToTexture());
            cache["badge"] = s;
            return s;
        }

        /// <summary>Mano de caricatura con la punta del dedo en (40, 5) de un lienzo de 85x145.</summary>
        public static Sprite Hand()
        {
            Sprite s;
            if (cache.TryGetValue("hand", out s)) return s;
            Painter p = new Painter(170, 290, 2f, 3);
            p.Push();
            p.Transform(V(40f, 5f), 0f, 1f);
            HandShape(p);
            p.Pop();
            s = MakeSprite(p.ToTexture());
            cache["hand"] = s;
            return s;
        }

        public const float HandW = 85f, HandH = 145f;
        public static readonly Vector2 HandTip = new Vector2(40f, 5f);

        // ---------------------------------------------------------------- formas
        public static void Coin(Painter p, Vector2 c, float r, float spin)
        {
            float sx = Mathf.Max(Mathf.Abs(spin), 0.25f);
            p.Poly(Painter.EllPts(c, V(r * sx + 2.5f, r + 2.5f), 20), Out);
            p.Ellipse(c, V(r * sx, r), GoldD, 20);
            p.Ellipse(c + V(0, -r * 0.1f), V(r * sx * 0.9f, r * 0.88f), Gold, 20);
            p.Ellipse(c + V(0, -r * 0.08f), V(r * sx * 0.55f, r * 0.55f), Color.Lerp(GoldD, Gold, 0.5f), 16);
            p.Circle(c + V(-r * 0.35f * sx, -r * 0.4f), r * 0.18f, new Color(1, 1, 1, 0.8f));
        }

        public static void GemShape(Painter p, Vector2 c, float r)
        {
            Vector2[] pts =
            {
                c + V(-r * 0.6f, -r * 0.75f), c + V(r * 0.6f, -r * 0.75f), c + V(r, -r * 0.2f), c + V(0, r), c + V(-r, -r * 0.2f),
            };
            p.OPoly(pts, Gem, 2.5f);
            p.Poly(new[] { c + V(-r * 0.6f, -r * 0.75f), c + V(r * 0.6f, -r * 0.75f), c + V(r * 0.35f, -r * 0.2f), c + V(-r * 0.35f, -r * 0.2f) }, H("a5e4ff"));
            p.Poly(new[] { c + V(r * 0.35f, -r * 0.2f), c + V(r, -r * 0.2f), c + V(0, r) }, GemD);
            p.Line(c + V(-r, -r * 0.2f), c + V(r, -r * 0.2f), 1.5f, Out, false);
        }

        public static void ToolShape(Painter p, int k, int rank)
        {
            Color col = RankCol(rank);
            p.ORR(new Rect(-3.5f, -44f, 7f, 52f), 3f, Handle, 2.5f);
            if (k == 0)
            {
                Vector2[] head =
                {
                    V(-30, -30), V(-17, -44), V(0, -50), V(17, -44), V(30, -30), V(15, -39), V(0, -42), V(-15, -39),
                };
                p.OPoly(head, col, 3f);
                p.Stroke(new[] { V(-17, -42), V(0, -47), V(17, -42) }, 2.5f, Lightened(col, 0.45f), false, true);
            }
            else
            {
                p.ORR(new Rect(-19f, -62f, 38f, 22f), 5f, col, 3f);
                p.Poly(Painter.RoundRectPts(new Rect(-5f, -62f, 10f, 22f), 0.6f, 1), Darkened(col, 0.25f));
                p.Line(V(-15, -58), V(15, -58), 2.5f, Lightened(col, 0.45f), false);
            }
        }

        /// <summary>Minero (port de Art.miner). p = pies; s = escala; facing = +1/-1.</summary>
        public static void Miner(Painter pt, Vector2 pos, float s, float facing, float walk, bool moving, float ang,
            Color helmet, int toolK, int toolR, bool blink)
        {
            pt.Push();
            pt.Transform(pos, 0f, 1f);
            pt.Ellipse(V(0, s), V(27f * s, 9f * s), new Color(0, 0, 0, 0.2f), 20);
            float l1 = 0f, bob;
            if (moving)
            {
                l1 = Mathf.Sin(walk * 14f) * 5f;
                bob = -Mathf.Abs(Mathf.Sin(walk * 14f)) * 3f;
            }
            else bob = Mathf.Sin(walk * 3f) * 0.8f;
            pt.Transform(V(0, 0), 0f, facing * s, s);
            pt.ORR(new Rect(-14, -17 + Mathf.Min(l1, 0f), 11, 14), 3, Overall);
            pt.ORR(new Rect(3, -17 + Mathf.Min(-l1, 0f), 11, 14), 3, Overall);
            pt.ORR(new Rect(-16, -7 + Mathf.Min(l1, 0f), 14, 7), 3, Boot);
            pt.ORR(new Rect(2, -7 + Mathf.Min(-l1, 0f), 14, 7), 3, Boot);
            pt.Push();
            pt.Transform(V(0, bob), 0f, 1f);
            // brazo trasero
            pt.OC(V(-19, -31), 5.5f, Skin);
            // torso
            pt.ORR(new Rect(-20, -50, 40, 32), 11, Shirt);
            pt.ORR(new Rect(-19, -33, 38, 18), 7, Overall);
            pt.Poly(Painter.RoundRectPts(new Rect(-11, -44, 22, 14), 3), Overall);
            pt.Line(V(-11, -44), V(-15, -49), 4f, Overall, false);
            pt.Line(V(11, -44), V(15, -49), 4f, Overall, false);
            pt.Circle(V(-6, -40), 2.2f, Gold);
            pt.Circle(V(6, -40), 2.2f, Gold);
            // cabeza
            pt.OC(V(0, -66), 20f, Skin);
            pt.Circle(V(-12, -60), 4f, new Color(1f, 0.55f, 0.5f, 0.35f));
            pt.Circle(V(13, -60), 4f, new Color(1f, 0.55f, 0.5f, 0.35f));
            Vector2[] beard =
            {
                V(-16, -61), V(-7, -57), V(0, -58), V(7, -57), V(16, -61), V(14, -51), V(7, -45), V(0, -47), V(-7, -45), V(-14, -51),
            };
            pt.OPoly(beard, Beard, 2.5f);
            pt.Circle(V(5, -62), 4.5f, SkinD);
            if (blink)
            {
                pt.Line(V(-6, -68), V(0, -68), 2.2f, Out, false);
                pt.Line(V(7, -68), V(13, -68), 2.2f, Out, false);
            }
            else
            {
                pt.Ellipse(V(-3, -68), V(2.4f, 3.4f), Out, 10);
                pt.Ellipse(V(10, -68), V(2.4f, 3.4f), Out, 10);
                pt.Circle(V(-2.3f, -69.3f), 0.9f, Color.white);
                pt.Circle(V(10.7f, -69.3f), 0.9f, Color.white);
            }
            // casco
            Vector2[] dome = new Vector2[13];
            for (int i = 0; i < 13; i++)
            {
                float a = Mathf.PI + Mathf.PI * i / 12f;
                dome[i] = V(0, -75) + V(Mathf.Cos(a) * 22f, Mathf.Sin(a) * 17f);
            }
            pt.OPoly(dome, helmet, 3f);
            pt.Line(V(0, -92), V(0, -76), 4f, Darkened(helmet, 0.2f), false);
            pt.ORR(new Rect(-25, -78, 50, 6), 3, Darkened(helmet, 0.12f), 2.5f);
            pt.OC(V(14, -84), 6f, H("8c8f94"), 2.5f);
            pt.Circle(V(15, -84), 3.8f, H("fff6b0"));
            // herramienta
            pt.Push();
            pt.Transform(V(12, -34), ang, 1f);
            ToolShape(pt, toolK, toolR);
            pt.OC(V(0, 0), 6f, Skin, 2.5f);
            pt.Pop();
            pt.Pop();
            pt.Pop();
        }

        static void HandShape(Painter p)
        {
            Color skin = Skin, skinD = SkinD, outc = Out;
            // manga
            RR(p, new Rect(-30, 104, 62, 30), 8, new Color(0.98f, 0.95f, 0.88f, 1f), outc);
            RR(p, new Rect(-30, 104, 62, 10), 5, H("4aa3f0"), outc);
            // pulgar (elipse girada -0.7 rad alrededor de su centro)
            Vector2 pivot = V(-28, 76);
            Vector2[] el = Painter.EllPts(pivot, V(14, 10), 14);
            Vector2[] q = new Vector2[el.Length];
            Vector2[] inner = new Vector2[el.Length];
            float cs = Mathf.Cos(-0.7f), sn = Mathf.Sin(-0.7f);
            for (int i = 0; i < el.Length; i++)
            {
                Vector2 d = el[i] - pivot;
                q[i] = pivot + V(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
                inner[i] = pivot + (q[i] - pivot) * 0.86f;
            }
            p.Poly(q, outc);
            p.Poly(inner, skin);
            // palma
            RR(p, new Rect(-30, 44, 62, 66), 22, skin, outc);
            // dedos doblados
            Vector2[] ks = { V(20, 54), V(25, 70), V(27, 86) };
            for (int i = 0; i < ks.Length; i++)
            {
                p.Circle(ks[i], 14f, outc);
                p.Circle(ks[i], 11f, skin);
            }
            // indice
            RR(p, new Rect(-15, 0, 24, 66), 12, skin, outc);
            p.Poly(Painter.RoundRectPts(new Rect(-11, 50, 16, 14), 0.6f, 1), skin);
            p.Line(V(-7, 10), V(-7, 30), 4f, new Color(1, 1, 1, 0.5f), false);
            p.Line(V(-5, 44), V(3, 44), 2.5f, skinD, false);
        }

        static void RR(Painter p, Rect r, float rad, Color col, Color outc)
        {
            p.Poly(Painter.RoundRectPts(Painter.Grow(r, 3f), rad + 3f), outc);
            p.Poly(Painter.RoundRectPts(r, rad), col);
        }

        // ---------------------------------------------------------------- iconos (Art.icon)
        public static void Icon(Painter p, string kind, Vector2 c, float k)
        {
            switch (kind)
            {
                case "coin":
                    Coin(p, c, 15f * k, 1f);
                    break;
                case "gem":
                    GemShape(p, c, 16f * k);
                    break;
                case "power":
                {
                    Vector2[] pts = new Vector2[14];
                    for (int i = 0; i < 14; i++)
                    {
                        float a = Mathf.PI * 2f * i / 14f - Mathf.PI / 2f;
                        float rr = (i % 2 == 0 ? 18f : 10f) * k;
                        pts[i] = c + V(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    }
                    p.OPoly(pts, H("ff5a3c"), 2.5f);
                    p.Circle(c, 6f * k, H("ffd34d"));
                    break;
                }
                case "speed":
                {
                    Vector2[] pts = { V(4, -19), V(-11, 3), V(-1, 3), V(-5, 19), V(11, -4), V(1, -4) };
                    p.OPoly(Painter.Xform(pts, V(k, k), c), H("ffd34d"), 2.5f);
                    break;
                }
                case "money":
                    for (int i = 0; i < 3; i++)
                        p.ORR(R(c + V(-13f, 6f - i * 7f) * k, V(26f, 7f) * k), 3f * k, i % 2 == 0 ? Gold : GoldD, 2f);
                    Coin(p, c + V(9, -10) * k, 8f * k, 1f);
                    break;
                case "gear":
                {
                    Vector2[] pts = new Vector2[32];
                    for (int i = 0; i < 32; i++)
                    {
                        float a = Mathf.PI * 2f * i / 32f;
                        float rr = ((i / 2) % 2 == 0 ? 17f : 13f) * k;
                        pts[i] = c + V(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    }
                    p.OPoly(pts, H("b8c0c8"), 2.5f);
                    p.OC(c, 5f * k, H("6f7880"), 2f);
                    break;
                }
                case "trophy":
                    p.ORR(R(c + V(-12, -17) * k, V(24, 18) * k), 7f * k, Gold, 2.5f);
                    p.Arc(c + V(-13, -9) * k, 6f * k, Mathf.PI * 0.5f, Mathf.PI * 1.5f, 2.5f, Out, 10);
                    p.Arc(c + V(13, -9) * k, 6f * k, -Mathf.PI * 0.5f, Mathf.PI * 0.5f, 2.5f, Out, 10);
                    p.ORR(R(c + V(-3, 0) * k, V(6, 9) * k), 1f, GoldD, 2f);
                    p.ORR(R(c + V(-10, 9) * k, V(20, 7) * k), 2f * k, GoldD, 2.5f);
                    break;
                case "shop":
                    p.ORR(R(c + V(-16, -4) * k, V(32, 20) * k), 3f * k, H("f2e6d0"), 2.5f);
                    for (int i = 0; i < 4; i++)
                    {
                        Color col = i % 2 == 0 ? H("e5484d") : Color.white;
                        p.ORR(R(c + V(-18 + i * 9, -17) * k, V(9, 13) * k), 3f * k, col, 2f);
                    }
                    p.ORR(R(c + V(-5, 3) * k, V(10, 13) * k), 2f * k, H("8a5a3a"), 2f);
                    break;
                case "rebirth":
                    p.Arc(c, 13f * k, 0.4f, Mathf.PI * 2f - 0.6f, 9f * k, Out, 24);
                    p.Arc(c, 13f * k, 0.4f, Mathf.PI * 2f - 0.6f, 5f * k, H("b06ef0"), 24);
                    p.Star(c, 8f * k, H("ffd34d"));
                    break;
                case "skin":
                {
                    Vector2[] dome = new Vector2[13];
                    for (int i = 0; i < 13; i++)
                    {
                        float a = Mathf.PI + Mathf.PI * i / 12f;
                        dome[i] = c + V(Mathf.Cos(a) * 17f, Mathf.Sin(a) * 14f + 5f) * k;
                    }
                    p.OPoly(dome, H("f5c531"), 2.5f);
                    p.ORR(R(c + V(-20, 3) * k, V(40, 6) * k), 2f * k, H("d9a520"), 2f);
                    p.OC(c + V(0, -3) * k, 4.5f * k, H("fff6b0"), 2f);
                    break;
                }
                case "tool":
                case "pick":
                    p.Push();
                    p.Transform(c + V(-2, 12) * k, 0.6f, k * 0.62f);
                    ToolShape(p, 0, 0);
                    p.Pop();
                    break;
                case "hammer":
                    p.Push();
                    p.Transform(c + V(-2, 14) * k, 0.6f, k * 0.55f);
                    ToolShape(p, 1, 0);
                    p.Pop();
                    break;
                case "piggy":
                    p.Poly(Painter.EllPts(c + V(0, 2) * k, V(19, 15) * k, 24), Out);
                    p.Poly(Painter.EllPts(c + V(0, 2) * k, V(16.5f, 12.5f) * k, 24), H("f7a8c0"));
                    p.OC(c + V(13, 2) * k, 5f * k, H("f28aa8"), 2f);
                    p.Circle(c + V(4, -3) * k, 2f * k, Out);
                    p.ORR(R(c + V(-6, -12) * k, V(10, 3) * k), 1f, Out, 0.5f);
                    GemShape(p, c + V(-2, -15) * k, 6f * k);
                    break;
                case "auto":
                {
                    Icon(p, "gear", c + V(-3, 2) * k, k * 0.8f);
                    Vector2[] a = { V(4, -20), V(18, -8), V(8, -8), V(8, 4), V(0, 4), V(0, -8), V(-10, -8) };
                    p.OPoly(Painter.Xform(a, V(k, k), c + V(4, 0) * k), H("5fdc5a"), 2f);
                    break;
                }
                case "play":
                    Icon(p, "pick", c, k);
                    break;
                case "boost":
                    Coin(p, c + V(-5, 2) * k, 12f * k, 1f);
                    break;
                case "mission":
                    p.ORR(R(c + V(-14, -17) * k, V(28, 34) * k), 4f * k, H("f2e6d0"), 2.5f);
                    for (int i = 0; i < 3; i++)
                        p.Line(c + V(-8, -8 + i * 8) * k, c + V(8, -8 + i * 8) * k, 2.5f * k, H("b59a7a"), false);
                    p.Star(c + V(10, 12) * k, 10f * k, H("ffd34d"));
                    break;
                case "lock":
                    p.Arc(c + V(0, -4) * k, 7f * k, Mathf.PI, Mathf.PI * 2f, 5f * k, Out, 12);
                    p.ORR(R(c + V(-10, -4) * k, V(20, 16) * k), 3f * k, GoldD, 2f);
                    break;
                case "chest":
                    p.ORR(R(c + V(-18, -6) * k, V(36, 22) * k), 3f * k, H("a0663a"), 2.5f);
                    p.ORR(R(c + V(-18, -16) * k, V(36, 12) * k), 5f * k, H("c07c46"), 2.5f);
                    p.ORR(R(c + V(-4, -8) * k, V(8, 9) * k), 2f * k, Gold, 2f);
                    break;
                case "clock":
                    p.OC(c, 14f * k, Color.white, 3f);
                    p.Line(c, c + V(0, -9) * k, 2.5f, Out, false);
                    p.Line(c, c + V(7, 0) * k, 2.5f, Out, false);
                    p.Arc(c, 12f * k, -Mathf.PI / 2f, 0.6f, 4f * k, H("e5484d"), 12);
                    break;
                case "close":
                    p.OC(c, 17f * k, H("e5484d"), 3f);
                    p.Line(c + V(-7, -7) * k, c + V(7, 7) * k, 4.5f * k, Color.white, true);
                    p.Line(c + V(7, -7) * k, c + V(-7, 7) * k, 4.5f * k, Color.white, true);
                    break;
                case "star":
                {
                    Vector2[] pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.PI * 2f * i / 10f - Mathf.PI / 2f;
                        pts[i] = c + V(Mathf.Cos(a), Mathf.Sin(a)) * ((i % 2 == 0 ? 16f : 7f) * k);
                    }
                    p.OPoly(pts, H("ffd34d"), 2.5f);
                    break;
                }
                case "boss":
                    p.OC(c, 13f * k, H("e5e0d8"), 2.5f);
                    p.Circle(c + V(-5, -2) * k, 3.5f * k, Out);
                    p.Circle(c + V(5, -2) * k, 3.5f * k, Out);
                    p.ORR(R(c + V(-6, 6) * k, V(12, 7) * k), 2f, H("e5e0d8"), 2f);
                    break;
                case "miner":
                    Miner(p, c + V(0, 18f) * k, 0.40f * k, 1f, 0f, false, -0.4f, H("f5c531"), 0, 0, false);
                    break;
                case "arrow":
                {
                    Vector2[] a = { V(0, -18), V(15, -2), V(6, -2), V(6, 16), V(-6, 16), V(-6, -2), V(-15, -2) };
                    p.OPoly(Painter.Xform(a, V(k, k), c), H("7ee84a"), 2.5f);
                    break;
                }
                case "check":
                    p.Line(c + V(-10, 1) * k, c + V(-3, 8) * k, 6f * k, Out, true);
                    p.Line(c + V(-3, 8) * k, c + V(11, -8) * k, 6f * k, Out, true);
                    p.Line(c + V(-10, 1) * k, c + V(-3, 8) * k, 3.4f * k, H("5cc84a"), true);
                    p.Line(c + V(-3, 8) * k, c + V(11, -8) * k, 3.4f * k, H("5cc84a"), true);
                    break;
                case "expand":   // ampliar: cuatro flechas hacia las esquinas
                    for (int q = 0; q < 4; q++)
                    {
                        float sx = q % 2 == 0 ? -1f : 1f, sy = q < 2 ? -1f : 1f;
                        Vector2 tip = c + V(sx * 17f, sy * 17f) * k;
                        Vector2[] tri = { tip, tip + V(-sx * 11f, 0f) * k, tip + V(0f, -sy * 11f) * k };
                        p.OPoly(tri, Color.white, 2.5f);
                        p.Line(tip + V(-sx * 4f, -sy * 4f) * k, c + V(sx * 4f, sy * 4f) * k, 5.5f * k, Out, true);
                        p.Line(tip + V(-sx * 4f, -sy * 4f) * k, c + V(sx * 4f, sy * 4f) * k, 3f * k, Color.white, true);
                    }
                    break;
                case "stamp":   // sello de hecho: disco verde con borde dentado y tilde blanco
                {
                    Vector2[] st = new Vector2[32];
                    for (int i = 0; i < 32; i++)
                    {
                        float a = Mathf.PI * 2f * i / 32f;
                        float rr = (i % 2 == 0 ? 19f : 17f) * k;
                        st[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    }
                    p.OPoly(st, H("4cc25a"), 2.5f);
                    p.Ellipse(c, V(13.5f, 13.5f) * k, H("3aa84a"), 24);
                    p.Line(c + V(-7, 0) * k, c + V(-2, 6) * k, 4.2f * k, Color.white, true);
                    p.Line(c + V(-2, 6) * k, c + V(8, -6) * k, 4.2f * k, Color.white, true);
                    break;
                }
                case "drop":   // gota (limpieza): punta arriba, panza abajo
                {
                    Vector2 o = c + V(0, 5) * k;
                    float r = 11f * k;
                    Vector2[] d = new Vector2[19];
                    d[0] = c + V(0, -16) * k;
                    for (int i = 0; i < 18; i++)
                    {
                        float a = Mathf.Lerp(-55f, 235f, i / 17f) * Mathf.Deg2Rad;
                        d[i + 1] = o + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    p.OPoly(d, H("4aa3f0"), 2.5f);
                    p.Circle(c + V(-4, 7) * k, 2.6f * k, new Color(1, 1, 1, 0.8f));
                    break;
                }
                default:
                    p.Circle(c, 10f * k, Color.magenta);
                    break;
            }
        }
    }
}
