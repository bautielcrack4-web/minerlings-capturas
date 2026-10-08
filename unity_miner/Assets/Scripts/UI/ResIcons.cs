using System.Collections.Generic;
using Mineros.Core;
using UnityEngine;

namespace Mineros.UI
{
    /// <summary>
    /// Iconos de los recursos de la ciudad, dibujados a 4x con contorno (mismo estilo que Icons): lienzo de 48x48
    /// unidades, centro en (24,24), y hacia abajo. Se reemplazaran por los renders de IconBake cuando existan.
    /// </summary>
    public static class ResIcons
    {
        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
        static Color H(string h) { return Icons.H(h); }
        static Vector2 V(float x, float y) { return new Vector2(x, y); }
        static Rect R(float x, float y, float w, float h) { return new Rect(x, y, w, h); }

        const int Cell = 196, Cols = 6;

        /// <summary>
        /// Todos los iconos van en UNA textura (atlas de 6 columnas, celdas de 196 px con 2 px de aire): la interfaz los
        /// dibuja juntos en una sola llamada en vez de una por icono.
        /// </summary>
        public static Sprite Get(Res r)
        {
            Sprite s;
            if (cache.TryGetValue((int)r, out s)) return s;
            int n = Island.ResCount, rows = (n + Cols - 1) / Cols;
            var atlas = new Texture2D(Cols * Cell, rows * Cell, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "IconosRecursos" };
            var clear = new Color32[atlas.width * atlas.height];
            atlas.SetPixels32(clear);
            var rects = new Rect[n];
            for (int i = 0; i < n; i++)
            {
                var p = new Painter(192, 192, 4f, 3);
                Draw(p, (Res)i, V(24f, 24f));
                int x = (i % Cols) * Cell + 2, y = (i / Cols) * Cell + 2;
                atlas.SetPixels32(x, y, p.W, p.H, p.ToPixels());
                rects[i] = new Rect(x, y, p.W, p.H);
            }
            atlas.Apply(false, true);
            for (int i = 0; i < n; i++)
                cache[i] = Sprite.Create(atlas, rects[i], new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return cache[(int)r];
        }

        public static Sprite Get(int r) { return Get((Res)r); }

        /// <summary>Color caracteristico del recurso (estelas, destellos).</summary>
        public static Color Tint(int r)
        {
            switch ((Res)r)
            {
                case Res.Stone: return H("c9c4bc");
                case Res.Coal: return H("6b6e78");
                case Res.IronOre: case Res.IronBar: case Res.Pick: case Res.Motor: return H("cfd6de");
                case Res.Wood: case Res.Cart: return H("e3b97f");
                case Res.Copper: case Res.Cable: return H("f6a066");
                case Res.Sand: return H("f3dc9a");
                case Res.Nugget: case Res.GoldBar: case Res.Ring: case Res.Crown: case Res.Compass: return H("ffd84a");
                case Res.RawGem: return H("c99af0");
                case Res.Crystal: case Res.Glass: case Res.CutGem: return H("7fe3f0");
                case Res.Lamp: return H("ffe28a");
                case Res.Dynamite: return H("ff7a5a");
                default: return Color.white;
            }
        }

        static void Chunk(Painter p, Vector2 c, float s, Color col, Color hi)
        {
            Vector2[] pts = { V(-14, 4), V(-11, -8), V(-2, -14), V(10, -11), V(15, -1), V(11, 11), V(-3, 13), V(-12, 10) };
            p.OPoly(Painter.Xform(pts, V(s, s), c), col, 2.5f);
            Vector2[] h = { V(-9, -6), V(-2, -11), V(6, -9), V(0, -4) };
            p.Poly(Painter.Xform(h, V(s, s), c), hi);
        }

        static void Bar(Painter p, Vector2 c, Color col, Color top)
        {
            Vector2[] body = { V(-17, 8), V(17, 8), V(12, -6), V(-12, -6) };
            p.OPoly(Painter.Xform(body, V(1, 1), c + V(0, 3)), col, 2.5f);
            Vector2[] t = { V(-12, -6), V(12, -6), V(9, -2), V(-9, -2) };
            p.Poly(Painter.Xform(t, V(1, 1), c + V(0, 3)), top);
        }

        static void Draw(Painter p, Res r, Vector2 c)
        {
            switch (r)
            {
                case Res.Stone: Chunk(p, c, 1.05f, H("a9a39b"), H("d2cec8")); break;
                case Res.Coal:
                    Chunk(p, c, 1.05f, H("34353c"), H("6b6e78"));
                    p.Circle(c + V(5, 2), 1.6f, H("9aa1aa"));
                    break;
                case Res.IronOre:
                    Chunk(p, c, 1.05f, H("8a8f96"), H("c6ccd4"));
                    p.Poly(Painter.Xform(new[] { V(0, -5), V(4, 0), V(0, 5), V(-4, 0) }, V(1, 1), c + V(4, 4)), H("dfe7f0"));
                    break;
                case Res.Wood:
                    p.ORR(R(c.x - 17, c.y - 7, 28, 16), 5f, H("b07a4a"), 2.5f);
                    p.OC(c + V(11, 1), 8.5f, H("e3b97f"), 2.5f);
                    p.Circle(c + V(11, 1), 4f, H("c99a60"));
                    p.Circle(c + V(11, 1), 1.5f, H("a0703f"));
                    break;
                case Res.Copper: Chunk(p, c, 1.05f, H("e0884a"), H("f6c49a")); break;
                case Res.Sand:
                {
                    var pile = Painter.EllPts(c + V(0, 6), V(19, 13), 24);
                    var half = new List<Vector2>();
                    foreach (var q in pile) if (q.y <= c.y + 7) half.Add(q);
                    p.OPoly(half.ToArray(), H("ecd28e"), 2.5f);
                    p.Circle(c + V(-5, -1), 1.4f, H("fff1c8"));
                    p.Circle(c + V(4, 2), 1.2f, H("c9a85e"));
                    break;
                }
                case Res.Nugget:
                    Chunk(p, c, 0.95f, H("f2c230"), H("fff2a8"));
                    p.Star(c + V(9, -9), 5f, Color.white);
                    break;
                case Res.RawGem:
                {
                    Vector2[] pts = { V(-12, 10), V(-14, -2), V(-6, -14), V(6, -12), V(14, -3), V(10, 11) };
                    p.OPoly(Painter.Xform(pts, V(1, 1), c), H("9a5bd9"), 2.5f);
                    p.Poly(Painter.Xform(new[] { V(-6, -14), V(6, -12), V(0, -2), V(-8, -4) }, V(1, 1), c), H("c99af0"));
                    break;
                }
                case Res.Crystal:
                {
                    Vector2[] a = { V(-4, 16), V(-8, -6), V(-3, -18), V(2, -6), V(1, 16) };
                    p.OPoly(Painter.Xform(a, V(1, 1), c), H("36c6d9"), 2.5f);
                    Vector2[] b = { V(3, 16), V(5, -2), V(11, -11), V(14, 0), V(10, 16) };
                    p.OPoly(Painter.Xform(b, V(1, 1), c), H("7fe3f0"), 2.5f);
                    p.Line(c + V(-3, -12), c + V(-4, 6), 1.6f, Color.white);
                    break;
                }
                case Res.IronBar: Bar(p, c, H("8e9aa8"), H("cfd6de")); break;
                case Res.GoldBar: Bar(p, c, H("f2c230"), H("fff2a8")); p.Star(c + V(10, -6), 4.5f, Color.white); break;
                case Res.Pick: Icons.Icon(p, "pick", c, 1f); break;
                case Res.Cart:
                {
                    Vector2[] body = { V(-16, -6), V(16, -6), V(12, 8), V(-12, 8) };
                    p.OPoly(Painter.Xform(body, V(1, 1), c), H("8a5a3a"), 2.5f);
                    p.ORR(R(c.x - 17, c.y - 9, 34, 5), 2f, H("4b4f57"), 2f);
                    p.OC(c + V(-8, 11), 4.5f, H("4b4f57"), 2f);
                    p.OC(c + V(8, 11), 4.5f, H("4b4f57"), 2f);
                    p.Circle(c + V(-4, -11), 4f, H("a9a39b"));
                    p.Circle(c + V(4, -12), 3.5f, H("f2c230"));
                    break;
                }
                case Res.Glass:
                    p.ORR(R(c.x - 9, c.y - 6, 18, 22), 6f, new Color(0.62f, 0.9f, 0.97f), 2.5f);
                    p.ORR(R(c.x - 4, c.y - 16, 8, 11), 2f, new Color(0.62f, 0.9f, 0.97f), 2.5f);
                    p.Line(c + V(-4, -1), c + V(-4, 11), 2f, Color.white);
                    break;
                case Res.Cable:
                    p.Arc(c, 13f, 0f, Mathf.PI * 2f, 8f, Painter.Out, 24);
                    p.Arc(c, 13f, 0f, Mathf.PI * 2f, 5f, H("e0884a"), 24);
                    p.Arc(c, 6f, 0f, Mathf.PI * 2f, 6f, Painter.Out, 16);
                    p.Arc(c, 6f, 0f, Mathf.PI * 2f, 3f, H("f6c49a"), 16);
                    p.Line(c + V(12, 6), c + V(19, 14), 3f, H("e0884a"));
                    break;
                case Res.CutGem: Icons.Icon(p, "gem", c, 1f); break;
                case Res.Ring:
                    p.Arc(c + V(0, 4), 12f, 0f, Mathf.PI * 2f, 7f, Painter.Out, 24);
                    p.Arc(c + V(0, 4), 12f, 0f, Mathf.PI * 2f, 4f, H("f2c230"), 24);
                    p.OPoly(Painter.Xform(new[] { V(0, -18), V(7, -11), V(0, -5), V(-7, -11) }, V(1, 1), c), H("e04a6a"), 2.2f);
                    break;
                case Res.Crown:
                {
                    Vector2[] pts = { V(-17, 10), V(-17, -10), V(-8, -2), V(0, -15), V(8, -2), V(17, -10), V(17, 10) };
                    p.OPoly(Painter.Xform(pts, V(1, 1), c), H("f2c230"), 2.5f);
                    p.OC(c + V(0, 4), 3f, H("e04a6a"), 1.5f);
                    p.OC(c + V(-9, 5), 2.2f, H("36c6d9"), 1.5f);
                    p.OC(c + V(9, 5), 2.2f, H("36c6d9"), 1.5f);
                    break;
                }
                case Res.Lamp:
                    p.ORR(R(c.x - 9, c.y - 8, 18, 20), 4f, H("ffe28a"), 2.5f);
                    p.ORR(R(c.x - 11, c.y - 12, 22, 5), 2f, H("4b4f57"), 2f);
                    p.ORR(R(c.x - 11, c.y + 11, 22, 5), 2f, H("4b4f57"), 2f);
                    p.Arc(c + V(0, -14), 5f, Mathf.PI, Mathf.PI * 2f, 2.5f, Painter.Out, 10);
                    p.Circle(c + V(0, 2), 3.5f, Color.white);
                    break;
                case Res.Motor:
                    p.ORR(R(c.x - 15, c.y - 4, 22, 18), 3f, H("8e9aa8"), 2.5f);
                    for (int i = 0; i < 3; i++) p.Line(c + V(-12 + i * 6, -1), c + V(-12 + i * 6, 11), 1.6f, H("5d6670"));
                    Icons.Icon(p, "gear", c + V(9, -7), 0.55f);
                    break;
                case Res.Dynamite:
                    for (int i = 0; i < 3; i++) p.ORR(R(c.x - 14 + i * 9, c.y - 8, 9, 22), 3f, H("e5484d"), 2.2f);
                    p.ORR(R(c.x - 15, c.y + 1, 30, 4), 1f, H("8a5a3a"), 1.5f);
                    p.Line(c + V(0, -8), c + V(5, -16), 2f, Painter.Out);
                    p.Star(c + V(6, -17), 5f, H("ffd34d"));
                    break;
                case Res.Compass:
                    p.OC(c, 16f, H("f2c230"), 2.5f);
                    p.OC(c, 12f, H("fbf1dc"), 1.5f);
                    p.Poly(new[] { c + V(0, -10), c + V(3, 0), c + V(-3, 0) }, H("e5484d"));
                    p.Poly(new[] { c + V(0, 10), c + V(3, 0), c + V(-3, 0) }, H("4b4f57"));
                    p.Circle(c, 1.8f, Painter.Out);
                    break;
            }
        }
    }
}
