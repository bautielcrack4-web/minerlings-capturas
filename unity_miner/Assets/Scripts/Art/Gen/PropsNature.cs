using System;
using System.Collections.Generic;

namespace Mineros.Art.Gen
{
    // Vegetacion y rocas decorativas. Siempre 3 tonos por familia (sombra, base, luz) y formas gordas y redondeadas.
    public static partial class PropGen
    {
        // ------------------------------------------------------------ copa de blobs (arboles, arbustos)
        /// <summary>Copa de blobs: nucleo, n blobs laterales, uno superior y un brillo chico hacia la luz. t = {sombra, base, luz}.</summary>
        static void Crown(MeshBuilder b, int[] t, float cy, float R, float flat, int n, Rng rng, int seedBase, int seg = 10, int rings = 6)
        {
            float amp = 0.1f;
            Bl(b, t[0], 0.04f * R, cy - R * 0.4f * flat, 0.02f * R, R * 0.86f, seedBase + 2, flat * 0.8f, 9, 5, amp);
            Bl(b, t[1], 0, cy, 0, R, seedBase, flat, seg, rings, amp);
            float a0 = rng.Range(0f, 6.28f);
            for (int k = 0; k < n; k++)
            {
                float a = a0 + 6.2832f * k / n + rng.Range(-0.25f, 0.25f);
                float dx = -(float)Math.Sin(a), dz = (float)Math.Cos(a);
                float rr = R * rng.Range(0.58f, 0.7f);
                float d = R * rng.Range(0.66f, 0.8f);
                float y = cy - R * rng.Range(0.12f, 0.3f) * flat;
                int tone = Shade(dx, dz, 0f);
                if (tone == 2) tone = 1;
                Bl(b, t[tone], dx * d, y, dz * d, rr, seedBase + 11 * (k + 1), flat * 1.05f, 9, 5, amp);
            }
            Bl(b, t[1], rng.Range(-0.1f, 0.1f) * R, cy + R * 0.62f * flat, rng.Range(-0.1f, 0.1f) * R, R * 0.6f, seedBase + 5, flat, seg, rings, amp);
            Bl(b, t[2], -R * 0.3f, cy + R * (0.5f + 0.3f * flat), -R * 0.3f, R * 0.32f, seedBase + 9, flat * 0.8f, 8, 5, 0.08f);
        }

        // ------------------------------------------------------------ arbol
        static MeshData Tree(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("tree", bm, v);
            Rgb leaf, trunk;
            float glow = 0f;
            switch (bm)
            {
                case 1: leaf = P(1, "prop"); trunk = P(1, "face"); break;
                case 2: leaf = P(2, "prop2"); trunk = P(2, "rock"); glow = 0.2f; break;
                case 3: leaf = P(3, "prop").Lighten(0.14f); trunk = P(3, "prop"); break;
                default: leaf = v == 3 ? P(0, "prop2") : P(0, "prop"); trunk = PaletteData.Trunk; break;
            }
            var tl = b.Tones(leaf, 0f, glow);
            var tt = b.Tones(trunk);
            int seed = v * 31 + bm * 7 + 3;

            // tronco con evasado en la base
            float th = bm == 1 ? 1.5f : (v == 1 ? 1.45f : (v == 2 ? 1.0f : 1.35f));
            float sway = bm == 1 ? 0.28f : (bm == 2 ? 0.2f : 0.05f);
            var path = new List<V3> { Pt(0, -0.05f, 0), Pt(sway * 0.3f, th * 0.3f, 0), Pt(sway * 0.8f, th * 0.7f, 0.04f), Pt(sway, th, 0.05f) };
            Prims.Tube(b, tt[1], Xf.I, path, new List<float> { 0.34f, 0.23f, 0.19f, 0.17f }, 7, false, true);
            // raices
            int nr = 3 + (v & 1);
            float ra = rng.Range(0f, 6f);
            for (int k = 0; k < nr; k++)
            {
                float a = ra + 6.2832f * k / nr + rng.Range(-0.3f, 0.3f);
                float dx = -(float)Math.Sin(a), dz = (float)Math.Cos(a);
                var rp = new List<V3> { Pt(dx * 0.1f, 0.34f, dz * 0.1f), Pt(dx * 0.3f, 0.11f, dz * 0.3f), Pt(dx * 0.5f, 0.04f, dz * 0.5f) };
                Prims.Tube(b, tt[1], Xf.I, rp, new List<float> { 0.13f, 0.09f, 0.05f }, 5, false, true);
            }
            // copa
            b.Push(Xf.I.T(sway, 0, 0.05f));
            if (bm == 1)
            {
                Crown(b, tl, th + 0.4f, 1.0f, 0.55f, 4, rng, seed, 11, 6);
            }
            else if (bm == 0 && v == 1)
            {
                // alto y estrecho: tres bolas apiladas
                Bl(b, tl[1], 0, th + 0.451f, 0, 0.7f, seed, 1f, 10, 6, 0.1f);
                Bl(b, tl[0], 0.25f, th + 0.287f, 0.3f, 0.5f, seed + 3, 1f, 10, 6, 0.1f);
                Bl(b, tl[1], -0.05f, th + 0.984f, 0, 0.55f, seed + 6, 1f, 10, 6, 0.1f);
                Bl(b, tl[0], 0.3f, th + 0.943f, 0.25f, 0.38f, seed + 9, 1f, 10, 6, 0.1f);
                Bl(b, tl[1], 0, th + 1.410f, 0, 0.36f, seed + 12, 1f, 9, 5, 0.1f);
                Bl(b, tl[2], -0.2f, th + 1.189f, -0.25f, 0.3f, seed + 15, 1f, 9, 5, 0.08f);
                Bl(b, tl[2], -0.3f, th + 0.697f, -0.35f, 0.3f, seed + 18, 1f, 9, 5, 0.08f);
            }
            else if (bm == 3)
            {
                Crown(b, tl, th + 0.45f, 0.75f, 0.9f, 4, rng, seed, 9, 5);
                var em = b.Mat(P(3, "prop2"), 0.3f, P(3, "prop2").Mul(0.9f));
                for (int k = 0; k < 4; k++)
                {
                    float a = rng.Range(0, 6.28f);
                    Sp(b, em, -(float)Math.Sin(a) * 0.7f, th + 0.3f + rng.Range(0, 0.7f), (float)Math.Cos(a) * 0.7f, 0.1f, 1f, 6, 3);
                }
            }
            else
            {
                float R = v == 2 ? 1.0f : 0.88f;
                Crown(b, tl, th + R * 0.62f, R, v == 2 ? 0.82f : 0.95f, v == 2 ? 6 : 5, rng, seed);
                if (bm == 2)
                {
                    var em = b.Mat(P(2, "prop"), 0.4f, P(2, "prop").Mul(0.8f));
                    for (int k = 0; k < 5; k++)
                    {
                        float a = rng.Range(0, 6.28f);
                        float d = R * rng.Range(0.8f, 1.0f);
                        Sp(b, em, -(float)Math.Sin(a) * d, th + R * rng.Range(0.3f, 0.9f), (float)Math.Cos(a) * d, 0.07f, 1f, 6, 3);
                    }
                }
            }
            b.Pop();
            return b.Build();
        }

        // ------------------------------------------------------------ arbusto
        static MeshData Bush(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("bush", bm, v);
            Rgb baseC = bm == 2 ? P(2, "decor") : P(bm, "prop");
            if (bm == 3) baseC = baseC.Lighten(0.12f);
            var t = b.Tones(baseC);
            int seed = v * 13 + bm;
            float k = v == 3 ? 1.3f : 1f;
            // blobs bajos y anchos; sombra al lado opuesto de la luz
            Bl(b, t[0], 0.02f * k, 0.2f * k, 0.02f * k, 0.5f * k, seed + 7, 0.62f, 10, 5, 0.1f);
            Bl(b, t[1], 0, 0.34f * k, 0, 0.4f * k, seed, 0.9f, 10, 6, 0.12f);
            Bl(b, t[0], 0.4f * k, 0.24f * k, -0.04f * k, 0.3f * k, seed + 1, 0.9f, 9, 5, 0.12f);
            Bl(b, t[1], -0.4f * k, 0.25f * k, 0.12f * k, 0.32f * k, seed + 2, 0.9f, 9, 5, 0.12f);
            Bl(b, t[0], 0.06f * k, 0.22f * k, 0.4f * k, 0.28f * k, seed + 3, 0.9f, 9, 5, 0.12f);
            Bl(b, t[2], -0.15f * k, 0.55f * k, -0.12f * k, 0.2f * k, seed + 4, 0.9f, 8, 5, 0.1f);
            if (v == 1 || v == 2)
            {
                Rgb fc = P(bm, v == 1 ? "prop2" : "ore");
                int fs = b.Mat(fc, 0.15f, bm == 2 ? fc.Mul(0.7f) : Rgb.Black);
                int n = 7;
                for (int i = 0; i < n; i++)
                {
                    float a = i * 6.2832f / n + rng.Range(-0.4f, 0.4f);
                    float el = rng.Range(0.2f, 1.15f); // elevacion sobre el blob central
                    float rr = 0.4f * k * 1.0f;
                    float x = -(float)Math.Sin(a) * (float)Math.Cos(el) * rr, z = (float)Math.Cos(a) * (float)Math.Cos(el) * rr;
                    float y = 0.34f * k + (float)Math.Sin(el) * rr * 0.9f;
                    Sp(b, fs, x * 1.05f, y + 0.015f, z * 1.05f, 0.075f, 1f, 6, 3);
                }
            }
            return b.Build();
        }

        // ------------------------------------------------------------ flor
        static MeshData Flower(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("flower", bm, v);
            var stem = b.Tones(PaletteData.Stem);
            Rgb pc = v == 0 ? P(bm, "prop2") : (v == 1 ? PaletteData.Ivory : P(bm, "ore"));
            var pt = b.Tones(pc, 0f, bm == 2 ? 0.45f : 0f);
            int center = b.Mat(PaletteData.Yellow, 0f, bm == 2 ? PaletteData.Yellow.Mul(0.3f) : Rgb.Black);
            float h = 0.4f + 0.06f * v;
            float lean = 0.05f * (v - 1);
            Prims.Tube(b, stem[1], Xf.I, new List<V3> { Pt(0, -0.02f, 0), Pt(lean * 0.5f, h * 0.5f, 0), Pt(lean, h, 0) },
                new List<float> { 0.032f, 0.026f, 0.022f }, 5, false, true);
            // hojas
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1f : -1f;
                Bl(b, stem[i == 0 ? 2 : 1], s * 0.09f, h * 0.32f + i * 0.05f, 0.0f, 0.085f, 4 + i, 0.4f, 6, 3, 0.05f);
            }
            // petalos
            int np = 5;
            b.Push(Xf.I.T(lean, h, 0).Rx(-8));
            for (int i = 0; i < np; i++)
            {
                float a = 6.2832f * i / np;
                float x = -(float)Math.Sin(a) * 0.1f, z = (float)Math.Cos(a) * 0.1f;
                Prims.Sphere(b, pt[i % 2 == 0 ? 1 : 2], Xf.I.T(x, 0, z).Ry(a * 57.3f).S(0.85f, 0.55f, 1f), 0.09f, 7, 4);
            }
            Sp(b, center, 0, 0.02f, 0, 0.065f, 0.8f, 7, 4);
            b.Pop();
            return b.Build();
        }

        // ------------------------------------------------------------ matas de pasto
        static MeshData Grass(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("grass", bm, v);
            Rgb c = P(bm, "decor");
            var t = new[] { b.Mat(c.Darken(0.2f)), b.Mat(c), b.Mat(P(bm, "decor2")) };
            int n = 8 + v * 2;
            float hs = v == 2 ? 1.35f : 1f;
            for (int i = 0; i < n; i++)
            {
                float a = 6.2832f * i / n + rng.Range(-0.3f, 0.3f);
                float d = rng.Range(0.02f, 0.16f) * (v == 3 ? 1.4f : 1f);
                float x = -(float)Math.Sin(a) * d, z = (float)Math.Cos(a) * d;
                float h = rng.Range(0.34f, 0.56f) * hs * (i % 3 == 0 ? 1.25f : 1f);
                float bend = rng.Range(0.1f, 0.24f);
                float dx = -(float)Math.Sin(a), dz = (float)Math.Cos(a);
                var path = new List<V3>
                {
                    Pt(x, -0.02f, z), Pt(x + dx * bend * 0.3f, h * 0.55f, z + dz * bend * 0.3f), Pt(x + dx * bend, h, z + dz * bend),
                };
                int slot = t[(i + v) % 3];
                Prims.Tube(b, slot, Xf.I, path, new List<float> { 0.065f, 0.05f, 0.012f }, 3, false, false);
            }
            return b.Build();
        }

        // ------------------------------------------------------------ cactus
        static void RibbedCapsule(MeshBuilder b, int slot, Xf xf, float r, float h, int seg, float rib, bool flat = false)
        {
            // perfil capsula con costillas verticales (valles en las columnas impares)
            var prof = new List<P2> { new P2(0, 0) };
            int cr = 4;
            for (int i = 1; i <= cr; i++)
            {
                float a = (-90f + 90f * i / cr) * (float)Math.PI / 180f;
                prof.Add(new P2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r + r));
            }
            for (int i = 1; i <= cr; i++)
            {
                float a = 90f * i / cr * (float)Math.PI / 180f;
                prof.Add(new P2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r + h - r));
            }
            int R = prof.Count;
            var p = new V3[R, seg];
            for (int i = 0; i < R; i++)
                for (int j = 0; j < seg; j++)
                {
                    float th = 2f * (float)Math.PI * j / seg;
                    float m = (j % 2 == 0) ? 1f : 1f - rib;
                    float rr = prof[i].r * m;
                    p[i, j] = new V3(-rr * (float)Math.Sin(th), prof[i].y, rr * (float)Math.Cos(th));
                }
            b.Push(xf);
            b.Grid(slot, p, true, flat);
            b.Pop();
        }

        static MeshData Cactus(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("cactus", bm, v);
            var t = b.Tones(P(bm, "prop"));
            Rgb fc = P(bm, "prop2");
            int fl = b.Mat(fc, 0.1f, bm == 2 ? fc.Mul(0.5f) : Rgb.Black);
            int fc2 = b.Mat(PaletteData.Yellow);
            float r = 0.3f;
            if (v == 3)
            {
                // cactus barril redondo con costillas y flor
                RibbedCapsule(b, t[1], Xf.I, 0.36f, 0.68f, 12, 0.2f);
                for (int i = 0; i < 5; i++)
                {
                    float a = 6.2832f * i / 5;
                    Prims.Sphere(b, fl, Xf.I.T(-(float)Math.Sin(a) * 0.075f, 0.69f, (float)Math.Cos(a) * 0.075f).Ry(a * 57.3f).S(0.9f, 0.55f, 1f), 0.075f, 6, 3);
                }
                Sp(b, fc2, 0, 0.71f, 0, 0.042f, 1f, 6, 3);
                return b.Build();
            }
            float H = v == 2 ? 1.65f : 1.35f;
            RibbedCapsule(b, t[1], Xf.I, r, H, 12, 0.2f);
            // brazos
            Action<float, float, float, float> arm = (side, y0, up, reach) =>
            {
                var path = new List<V3>
                {
                    Pt(side * (r - 0.06f), y0, 0), Pt(side * (r + reach * 0.6f), y0 + 0.02f, 0), Pt(side * (r + reach), y0 + 0.14f, 0),
                    Pt(side * (r + reach + 0.02f), y0 + up * 0.55f, 0), Pt(side * (r + reach + 0.02f), y0 + up, 0),
                };
                Prims.Tube(b, t[side > 0 ? 0 : 1], Xf.I, path, new List<float> { 0.16f, 0.15f, 0.15f, 0.15f, 0.14f }, 8, false, true);
            };
            arm(-1f, H * 0.38f, v == 2 ? 0.7f : 0.5f, 0.3f);
            if (v != 0) arm(1f, H * (v == 2 ? 0.5f : 0.3f), v == 2 ? 0.5f : 0.4f, 0.28f);
            if (v >= 1)
            {
                float fy = H + 0.01f;
                for (int i = 0; i < 5; i++)
                {
                    float a = 6.2832f * i / 5;
                    Prims.Sphere(b, fl, Xf.I.T(-(float)Math.Sin(a) * 0.07f, fy, (float)Math.Cos(a) * 0.07f).Ry(a * 57.3f).S(0.9f, 0.55f, 1f), 0.07f, 6, 3);
                }
                Sp(b, fc2, 0, fy + 0.02f, 0, 0.04f, 1f, 6, 3);
            }
            return b.Build();
        }

        // ------------------------------------------------------------ ramitas secas
        static MeshData Shrub(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("shrub", bm, v);
            Rgb c = bm == 0 ? PaletteData.Twig : (bm == 1 ? P(1, "face").Lighten(0.1f) : (bm == 2 ? P(2, "decor") : P(3, "decor")));
            var t = b.Tones(c);
            int n = 5 + v;
            for (int i = 0; i < n; i++)
            {
                float a = 6.2832f * i / n + rng.Range(-0.3f, 0.3f);
                float dx = -(float)Math.Sin(a), dz = (float)Math.Cos(a);
                float h = rng.Range(0.38f, 0.62f);
                float lean = rng.Range(0.1f, 0.3f);
                var p0 = Pt(dx * 0.03f, -0.02f, dz * 0.03f);
                var p1 = Pt(dx * lean * 0.5f, h * 0.55f, dz * lean * 0.5f);
                var p2 = Pt(dx * lean, h, dz * lean);
                int sl = t[Shade(dx, dz, 0f)];
                Prims.Tube(b, sl, Xf.I, new List<V3> { p0, p1, p2 }, new List<float> { 0.05f, 0.036f, 0.018f }, 4, false, true);
                // horquilla
                float fa = a + rng.Range(0.5f, 1.0f) * (i % 2 == 0 ? 1 : -1);
                var fd = Pt(-(float)Math.Sin(fa), 0, (float)Math.Cos(fa));
                var pf = p1 + Pt(fd.x * 0.16f, 0.18f, fd.z * 0.16f);
                Prims.Tube(b, sl, Xf.I, new List<V3> { p1, V3.Lerp(p1, pf, 0.5f), pf }, new List<float> { 0.03f, 0.024f, 0.012f }, 4, false, true);
            }
            return b.Build();
        }

        // ------------------------------------------------------------ racimo de cristales
        static MeshData CrystalCluster(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("crystal_cluster", bm, v);
            Rgb c = (bm == 2 && v == 2) ? P(2, "prop2") : P(bm, bm == 0 ? "prop" : "prop");
            if (bm == 0) c = v == 2 ? P(0, "prop2") : P(0, "prop");
            var t = b.Tones(c, 0.55f, 0.32f);
            // base rocosa
            var rt = b.Tones(P(bm, "rock"));
            Bl(b, rt[0], 0, 0.1f, 0, 0.5f, 5 + v, 0.4f, 8, 4, 0.25f, true);
            int n = new[] { 4, 5, 3, 7 }[v];
            for (int i = 0; i < n; i++)
            {
                float a, d, h, tilt;
                if (i == 0) { a = 0; d = 0; h = rng.Range(1.0f, 1.25f); tilt = rng.Range(0, 8); }
                else
                {
                    a = 6.2832f * (i - 1) / (n - 1) + rng.Range(-0.3f, 0.3f);
                    d = rng.Range(0.18f, 0.34f);
                    h = rng.Range(0.5f, 0.9f);
                    tilt = rng.Range(14f, 28f);
                }
                float dx = -(float)Math.Sin(a), dz = (float)Math.Cos(a);
                // inclinar hacia afuera: eje de giro perpendicular a la direccion
                var xf = Xf.I.T(dx * d, 0.02f, dz * d).Ry(-a * 57.2958f).Rx(tilt);
                int sl = i == 0 ? t[2] : t[Shade(dx, dz, 0.2f)];
                Gems.Crystal(b, sl, xf, v * 17 + i + bm * 5, h, rng.Range(0.12f, 0.17f) * (i == 0 ? 1.25f : 1f));
            }
            b.HasAnchor = true; b.Anchor = Pt(0, 0.55f, 0);
            return b.Build();
        }

        // ------------------------------------------------------------ hongo
        static void Mush(MeshBuilder b, int[] cap, int stem, int gills, int spot, float x, float z, float s, float lean, int seed, int nSpots)
        {
            b.Push(Xf.I.T(x, 0, z).Rx(lean).S(s));
            var prof = new List<P2>
            {
                new P2(0.095f, 0), new P2(0.062f, 0.05f), new P2(0.052f, 0.14f), new P2(0.058f, 0.22f), new P2(0.0f, 0.23f),
            };
            // tallo con base evasada
            Prims.Lathe(b, stem, Xf.I, prof, 7);
            var capProf = new List<P2>
            {
                new P2(0, 0.205f), new P2(0.17f, 0.205f, true), new P2(0.22f, 0.245f), new P2(0.205f, 0.32f),
                new P2(0.14f, 0.38f), new P2(0, 0.41f),
            };
            // bajo el sombrero (laminas): primera tira es la cara de abajo
            Prims.Lathe(b, cap[1], Xf.I, capProf, 10);
            // lunares sobre la superficie del sombrero (puntos del perfil exterior)
            var rng = new Rng((uint)(seed * 7 + 5));
            for (int i = 0; i < nSpots; i++)
            {
                float a = 6.2832f * i / nSpots + rng.Range(-0.5f, 0.5f);
                float u = rng.Range(0.1f, 1.8f); // tramos 2->3 y 3->4 (la parte alta del sombrero)
                int sgi = 1 + (int)u;
                float f = u - (int)u;
                var p0 = capProf[sgi + 1]; var p1 = capProf[sgi + 2];
                float rr = p0.r + (p1.r - p0.r) * f, yy = p0.y + (p1.y - p0.y) * f;
                float dr = p1.r - p0.r, dy = p1.y - p0.y;
                float l = (float)Math.Sqrt(dr * dr + dy * dy);
                float nr = dy / l, ny = -dr / l;
                float sr = rng.Range(0.035f, 0.05f);
                float cr = rr + nr * 0.004f, cy = yy + ny * 0.004f;
                Prims.Sphere(b, spot, Xf.I.T(-(float)Math.Sin(a) * cr, cy, (float)Math.Cos(a) * cr), sr, 6, 3);
            }
            b.Pop();
        }

        static MeshData Mushroom(int bm, int v)
        {
            var b = new MeshBuilder();
            float glow = PaletteData.Dark(bm) > 0.2f ? 0.28f : 0f;
            var cap = b.Tones(P(bm, "prop2"), 0.12f, glow);
            int stem = b.Mat(PaletteData.MushStem);
            int gills = b.Mat(PaletteData.MushStem.Darken(0.12f));
            int spot = b.Mat(PaletteData.Ivory, 0f, glow > 0 ? PaletteData.Ivory.Mul(0.25f) : Rgb.Black);
            switch (v)
            {
                case 0: Mush(b, cap, stem, gills, spot, 0, 0, 1.2f, 0, 1, 5); break;
                case 1: Mush(b, cap, stem, gills, spot, 0, 0, 1.6f, 0, 2, 7); break;
                case 2:
                    Mush(b, cap, stem, gills, spot, 0, 0, 1.4f, -4, 3, 6);
                    Mush(b, cap, stem, gills, spot, 0.3f, 0.12f, 0.85f, 12, 4, 4);
                    Mush(b, cap, stem, gills, spot, -0.22f, 0.22f, 0.65f, -14, 5, 3);
                    break;
                default:
                    Mush(b, cap, stem, gills, spot, 0, 0, 1.0f, 8, 6, 5);
                    Mush(b, cap, stem, gills, spot, -0.26f, -0.1f, 1.25f, -6, 7, 6);
                    break;
            }
            b.HasAnchor = true; b.Anchor = Pt(0, 0.4f, 0);
            return b.Build();
        }

        // ------------------------------------------------------------ arbol muerto
        static void Branch(MeshBuilder b, int[] t, Rng rng, V3 start, V3 dir, float len, float rad, int depth, int bm)
        {
            var pts = new List<V3>();
            var rads = new List<float>();
            V3 p = start;
            V3 d = dir.Normalized;
            int seg = 3;
            for (int i = 0; i <= seg; i++)
            {
                pts.Add(p);
                rads.Add(rad * (1f - 0.7f * i / seg));
                d = (d + Pt(rng.Range(-0.35f, 0.35f), rng.Range(-0.1f, 0.18f), rng.Range(-0.35f, 0.35f))).Normalized;
                p = p + d * (len / seg);
            }
            int sl = t[Shade(dir.x, dir.z, dir.y * 0.6f)];
            Prims.Tube(b, sl, Xf.I, pts, rads, 5, false, true);
            if (depth > 0)
            {
                int forks = 2;
                for (int f = 0; f < forks; f++)
                {
                    int at = 1 + f;
                    var nd = (dir + Pt(rng.Range(-0.9f, 0.9f), rng.Range(0.1f, 0.5f), rng.Range(-0.9f, 0.9f))).Normalized;
                    Branch(b, t, rng, pts[Math.Min(at, pts.Count - 1)], nd, len * 0.62f, rads[Math.Min(at, rads.Count - 1)] * 0.95f, depth - 1, bm);
                }
            }
        }

        static MeshData DeadTree(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("deadtree", bm, v);
            Rgb c = bm == 3 ? P(3, "prop").Lighten(0.06f) : (bm == 0 ? PaletteData.TrunkD.Lighten(0.12f) : (bm == 1 ? P(1, "decor").Darken(0.15f) : P(2, "decor2").Lighten(0.12f)));
            var t = b.Tones(c);
            // tronco retorcido
            float H = 1.7f + 0.25f * v;
            var path = new List<V3>();
            var rads = new List<float>();
            int n = 6;
            float wob = 0.14f;
            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n;
                path.Add(Pt((float)Math.Sin(u * 5f + v) * wob * u, u * H, (float)Math.Cos(u * 4f + v * 2) * wob * u));
                rads.Add(0.26f * (1f - 0.8f * u) + 0.03f);
            }
            rads[0] = 0.3f;
            Prims.Tube(b, t[1], Xf.I, path, rads, 7, false, true);
            // raices retorcidas
            for (int k = 0; k < 3; k++)
            {
                float a = 6.2832f * k / 3 + rng.Range(-0.3f, 0.3f) + v;
                float dx = -(float)Math.Sin(a), dz = (float)Math.Cos(a);
                Prims.Tube(b, t[Shade(dx, dz, -0.5f) == 0 ? 0 : 1], Xf.I,
                    new List<V3> { Pt(dx * 0.08f, 0.3f, dz * 0.08f), Pt(dx * 0.3f, 0.12f, dz * 0.3f), Pt(dx * 0.55f, 0.02f, dz * 0.55f) },
                    new List<float> { 0.12f, 0.08f, 0.04f }, 5, false, true);
            }
            // ramas
            int nb = 3 + v;
            for (int k = 0; k < nb; k++)
            {
                float u = 0.4f + 0.5f * k / Math.Max(1, nb - 1) + rng.Range(-0.05f, 0.05f);
                int pi = (int)Math.Round(u * n);
                float a = k * 2.4f + rng.Range(0, 1f) + v;
                var dir = Pt(-(float)Math.Sin(a), rng.Range(0.5f, 1.1f), (float)Math.Cos(a));
                Branch(b, t, rng, path[pi], dir, rng.Range(0.7f, 1.0f), 0.15f * (1.1f - u), 1, bm);
            }
            // punta superior
            if (bm == 3)
            {
                var em = b.Mat(P(3, "prop2"), 0.3f, P(3, "prop2").Mul(0.9f));
                for (int k = 0; k < 3; k++)
                {
                    var pp = path[2 + k];
                    Sp(b, em, pp.x + 0.1f, pp.y, pp.z - 0.1f, 0.06f, 1f, 6, 3);
                }
            }
            return b.Build();
        }

        // ------------------------------------------------------------ respiradero de lava
        static MeshData Vent(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("vent", bm, v);
            var t = b.Tones(P(bm, "face"));
            int lip = b.Mat(P(bm, "lip"));
            Rgb lc = P(3, "prop2");
            int lava = b.Mat(lc, 0.5f, new Rgb(1f, 0.46f, 0.12f).Mul(0.9f));
            int hot = b.Mat(PaletteData.Gold, 0.5f, new Rgb(1f, 0.7f, 0.2f).Mul(0.9f));
            Action<float, float, float, float> crater = (x, z, s, h) =>
            {
                var prof = new List<P2>
                {
                    new P2(0.68f, 0), new P2(0.6f, 0.09f), new P2(0.46f, 0.2f), new P2(0.36f, 0.26f * h), new P2(0.3f, 0.25f * h),
                    new P2(0.25f, 0.2f * h),
                };
                prof[3] = new P2(prof[3].r, 0.26f * h);
                b.Push(Xf.I.T(x, 0, z).S(s, 1f, s));
                Prims.Lathe(b, t[1], Xf.I, prof, 10);
                // labio claro a la luz
                Prims.Torus(b, lip, Xf.I.T(0, 0.27f * h, 0), 0.34f, 0.045f, 10, 4);
                Prims.Lathe(b, lava, Xf.I.T(0, 0.01f, 0), new List<P2> { new P2(0.27f, 0.2f * h), new P2(0.1f, 0.215f * h), new P2(0, 0.22f * h) }, 10);
                Sp(b, hot, 0.05f, 0.21f * h, 0.04f, 0.05f, 0.8f, 5, 2);
                Sp(b, hot, -0.08f, 0.21f * h, -0.05f, 0.035f, 0.8f, 5, 2);
                b.Pop();
            };
            if (v == 0) crater(0, 0, 1f, 1f);
            else if (v == 1) { crater(-0.25f, 0.1f, 0.75f, 0.9f); crater(0.38f, -0.2f, 0.5f, 0.8f); }
            else
            {
                // chimenea alta
                var prof = new List<P2>
                {
                    new P2(0.62f, 0), new P2(0.5f, 0.12f), new P2(0.34f, 0.4f), new P2(0.27f, 0.62f), new P2(0.3f, 0.7f), new P2(0.25f, 0.66f),
                };
                Prims.Lathe(b, t[1], Xf.I, prof, 10);
                Prims.Torus(b, lip, Xf.I.T(0, 0.7f, 0), 0.28f, 0.045f, 10, 5);
                Prims.Lathe(b, lava, Xf.I, new List<P2> { new P2(0.25f, 0.655f), new P2(0, 0.68f) }, 10);
                Sp(b, hot, 0.0f, 0.68f, 0.0f, 0.06f, 0.8f, 6, 3);
            }
            // piedras alrededor
            for (int i = 0; i < 3; i++)
            {
                float a = rng.Range(0, 6.28f);
                Bl(b, t[i == 0 ? 2 : 0], -(float)Math.Sin(a) * 0.72f, 0.07f, (float)Math.Cos(a) * 0.72f, rng.Range(0.1f, 0.15f), i + v, 0.65f, 6, 2, 0.3f, true);
            }
            b.HasAnchor = true; b.Anchor = Pt(0, v == 2 ? 0.8f : 0.4f, 0);
            return b.Build();
        }

        // ------------------------------------------------------------ piedritas y roca chica
        static MeshData Pebble(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("pebble", bm, v);
            var t = b.Tones(P(bm, "rock"));
            int n = 2 + v;
            for (int i = 0; i < n; i++)
            {
                float a = 6.2832f * i / n + rng.Range(-0.5f, 0.5f);
                float d = i == 0 ? 0f : rng.Range(0.14f, 0.26f);
                float r = rng.Range(0.07f, 0.12f) * (i == 0 ? 1.3f : 1f);
                Bl(b, t[i % 3], -(float)Math.Sin(a) * d, r * 0.45f, (float)Math.Cos(a) * d, r, 20 + i + v * 3, 0.6f, 7, 3, 0.35f, true);
            }
            return b.Build();
        }

        static MeshData RockSmall(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("rock_small", bm, v);
            var t = b.Tones(P(bm, "rock"));
            float s = v == 1 ? 0.42f : (v == 2 ? 0.3f : 0.36f);
            // cuerpo oscuro abajo, base al medio, brillo arriba a la luz
            Bl(b, t[0], 0, s * 0.38f, 0, s * 1.05f, 40 + v, 0.62f, 9, 4, 0.34f, true);
            Bl(b, t[1], 0.02f, s * 0.62f, 0.01f, s * 0.82f, 50 + v, 0.66f, 8, 4, 0.34f, true);
            Bl(b, t[2], -s * 0.28f, s * 0.82f, -s * 0.22f, s * 0.38f, 60 + v, 0.6f, 6, 3, 0.3f, true);
            if (v == 2 || v == 3)
            {
                Bl(b, t[1], s * 1.15f, s * 0.2f, s * 0.35f, s * 0.42f, 70 + v, 0.6f, 7, 3, 0.35f, true);
            }
            if (v == 3)
            {
                int moss = b.Mat(P(bm, "decor"));
                Bl(b, moss, -s * 0.05f, s * 0.95f, s * 0.05f, s * 0.5f, 80, 0.3f, 8, 3, 0.25f, false);
            }
            return b.Build();
        }
    }
}
