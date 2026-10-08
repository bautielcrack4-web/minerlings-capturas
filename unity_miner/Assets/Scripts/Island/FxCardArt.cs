using System.Collections.Generic;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Dibujo de cada carta de efecto, generado (240 cartas, sin imagenes externas): marco del color de la rareza con
    /// estrellas, fondo del color de la variante con un halo, y el simbolo del efecto (roca con pepitas, gema, piñata,
    /// meteoritos, monedas, fuegos, flores, arcoiris, rayo, trebol, veta grande, signo de pregunta). Cacheado por carta.
    /// </summary>
    public static class FxCardArt
    {
        const float W = 120f, H = 170f;
        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
        static Sprite back;
        public static readonly Color[] RarityCol = { Icons.H("b7bec6"), Icons.H("4aa3f0"), Icons.H("a36be8"), Icons.H("ffcc33") };
        static readonly Color Out = Painter.Out;

        public static Color VariantColor(int variant) { return Icons.H(Island.FxVariantHex[Mathf.Clamp(variant, 0, Island.FxVariantHex.Length - 1)]); }

        public static Sprite Get(int id)
        {
            Sprite s;
            if (cache.TryGetValue(id, out s) && s != null) return s;
            var c = Island.FxCatalog[id];
            var p = new Painter(240, 340, 2f, 3);
            Color rc = RarityCol[c.Rarity], vc = VariantColor(c.Variant);
            p.ORR(new Rect(3, 3, W - 6, H - 6), 14f, rc, 3f);
            Color bg = Color.Lerp(vc, Color.black, 0.35f);
            p.ORR(new Rect(10, 22, W - 20, H - 52), 10f, bg, 2f);
            p.Ellipse(new Vector2(W * 0.5f, 74f), new Vector2(46f, 46f), Color.Lerp(vc, Color.white, 0.25f) * new Color(1, 1, 1, 0.55f), 28);
            p.Ellipse(new Vector2(W * 0.5f, 74f), new Vector2(30f, 30f), Color.Lerp(vc, Color.white, 0.55f) * new Color(1, 1, 1, 0.45f), 24);
            for (int i = 0; i <= c.Rarity; i++) p.Star(new Vector2(W * 0.5f + (i - c.Rarity * 0.5f) * 15f, 13f), 6f, Icons.H("fff3b0"));
            Glyph(p, (Island.FxKind)c.Effect, new Vector2(W * 0.5f, 76f), vc, c.Variant);
            // franja de abajo (el nombre lo escribe la interfaz encima)
            p.ORR(new Rect(10, H - 28, W - 20, 20), 6f, Color.Lerp(rc, Color.black, 0.25f), 2f);
            s = Sprite.Create(p.ToTexture(), new Rect(0, 0, 240, 340), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            cache[id] = s;
            return s;
        }

        /// <summary>Dorso comun (sobres y cartas sin descubrir del album).</summary>
        public static Sprite Back()
        {
            if (back != null) return back;
            var p = new Painter(240, 340, 2f, 3);
            p.ORR(new Rect(3, 3, W - 6, H - 6), 14f, Icons.H("5b4bd6"), 3f);
            p.ORR(new Rect(12, 12, W - 24, H - 24), 10f, Icons.H("3e30a8"), 2f);
            for (int i = 0; i < 5; i++) p.Star(new Vector2(W * 0.5f + Mathf.Cos(i * 1.256f) * 26f, H * 0.5f + Mathf.Sin(i * 1.256f) * 26f), 7f, Icons.H("ffd84a"));
            p.OC(new Vector2(W * 0.5f, H * 0.5f), 14f, Icons.H("ffd84a"), 3f);
            back = Sprite.Create(p.ToTexture(), new Rect(0, 0, 240, 340), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return back;
        }

        static Vector2[] RockPts(Vector2 c, float r, int seed)
        {
            var rnd = new System.Random(seed);
            var pts = new Vector2[9];
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                float rr = r * (0.82f + (float)rnd.NextDouble() * 0.3f);
                pts[i] = c + new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.82f);
            }
            return pts;
        }

        static void Rock(Painter p, Vector2 c, float r, Color tint, int seed)
        {
            Color stone = Color.Lerp(Icons.H("9aa1aa"), tint, 0.35f);
            p.OPoly(RockPts(c, r, seed), stone, 3f);
            p.Poly(RockPts(c + new Vector2(-r * 0.2f, -r * 0.25f), r * 0.45f, seed + 1), Color.Lerp(stone, Color.white, 0.25f));
        }

        static void Glyph(Painter p, Island.FxKind k, Vector2 c, Color vc, int seed)
        {
            Color gold = Icons.H("ffd23a");
            switch (k)
            {
                case Island.FxKind.GoldRock:
                    Rock(p, c, 30f, vc, seed);
                    p.OC(c + new Vector2(-9, 4), 6f, gold, 2f); p.OC(c + new Vector2(8, -6), 5f, gold, 2f); p.OC(c + new Vector2(12, 10), 4f, gold, 2f);
                    break;
                case Island.FxKind.GemRock:
                    Rock(p, c + new Vector2(0, 8), 28f, vc, seed);
                    Icons.GemShape(p, c + new Vector2(0, -6), 17f);
                    break;
                case Island.FxKind.Pinata:
                    p.Star(c, 34f, vc);
                    p.Star(c, 20f, Color.Lerp(vc, Color.white, 0.5f));
                    p.Line(c + new Vector2(0, -30), c + new Vector2(0, -44), 3f, Out, true);
                    break;
                case Island.FxKind.Meteors:
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 m = c + new Vector2(-18 + i * 16, -10 + i * 14);
                        p.Line(m, m + new Vector2(-16, -16), 6f, Color.Lerp(vc, Color.white, 0.4f) * new Color(1, 1, 1, 0.7f), true);
                        p.OC(m, 7f, Icons.H("ff7a2a"), 2.5f);
                    }
                    break;
                case Island.FxKind.Fountain:
                    for (int i = 0; i < 5; i++) Icons.Coin(p, c + new Vector2(Mathf.Cos(i * 0.785f + 0.6f) * 22f, -Mathf.Sin(i * 0.785f + 0.6f) * 20f + 8f), 9f, 1f);
                    Icons.Coin(p, c + new Vector2(0, 16), 12f, 1f);
                    break;
                case Island.FxKind.Fireworks:
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        p.Line(c + d * 10f, c + d * 32f, 4f, i % 2 == 0 ? vc : Icons.H("fff3b0"), true);
                    }
                    p.OC(c, 8f, Icons.H("fff3b0"), 2f);
                    break;
                case Island.FxKind.Flowers:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        p.OC(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 17f, 11f, Color.Lerp(vc, Color.white, 0.3f), 2.5f);
                    }
                    p.OC(c, 10f, gold, 2.5f);
                    break;
                case Island.FxKind.RainbowRock:
                    {
                        Color[] rb = { Icons.H("ff5a5f"), Icons.H("ffd23a"), Icons.H("5cc84a"), Icons.H("4aa3f0") };
                        for (int i = 0; i < rb.Length; i++) p.Arc(c + new Vector2(0, 14), 40f - i * 6f, Mathf.PI, Mathf.PI * 2f, 6f, rb[i]);
                        Rock(p, c + new Vector2(0, 14), 20f, vc, seed);
                        break;
                    }
                case Island.FxKind.Frenzy:
                    p.OPoly(new[] { c + new Vector2(6, -36), c + new Vector2(-16, 4), c + new Vector2(0, 4), c + new Vector2(-6, 36), c + new Vector2(18, -6), c + new Vector2(2, -6) }, Color.Lerp(vc, gold, 0.6f), 3f);
                    break;
                case Island.FxKind.Clover:
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i / 4f * Mathf.PI * 2f + 0.785f;
                        p.OC(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 13f + new Vector2(0, -4), 13f, Color.Lerp(Icons.H("4fbf5a"), vc, 0.35f), 2.5f);
                    }
                    p.Line(c + new Vector2(0, 8), c + new Vector2(8, 34), 4f, Icons.H("3f8f3a"), true);
                    break;
                case Island.FxKind.MiniGiant:
                    Rock(p, c + new Vector2(0, 6), 38f, vc, seed);
                    p.OC(c + new Vector2(-10, -4), 7f, gold, 2f); p.OC(c + new Vector2(12, 4), 6f, gold, 2f); p.OC(c + new Vector2(2, -16), 5f, gold, 2f);
                    break;
                default:   // misterio: roca y signo de pregunta
                    Rock(p, c + new Vector2(0, 12), 26f, vc, seed);
                    p.Arc(c + new Vector2(0, -16), 11f, Mathf.PI * 1.0f, Mathf.PI * 2.35f, 6f, Color.white);
                    p.Line(c + new Vector2(3, -6), c + new Vector2(0, 2), 6f, Color.white, true);
                    p.OC(c + new Vector2(0, 12), 3.5f, Color.white, 2f);
                    break;
            }
        }
    }
}
