using System;
using System.Collections.Generic;

namespace Mineros.Art.Gen
{
    // Utileria de mineria: vagoneta, riel, farol, puntal, barril, cajon, mineral, lingote, gema, picos y cartel.
    // Maderas y metales son fijos (misma madera que el mango del minero); el mineral y las rocas toman el bioma.
    public static partial class PropGen
    {
        /// <summary>Altura de la cabeza del riel: apoya la vagoneta (origen en la base de las ruedas) a esta altura.</summary>
        public const float RailTop = 0.14f;

        static int[] WoodT(MeshBuilder b) { return b.Tones(PaletteData.Handle); }
        static int[] SteelT(MeshBuilder b, float spec = 0.22f) { return b.Tones(PaletteData.Steel, spec); }

        // ------------------------------------------------------------ vagoneta
        static void WheelAt(MeshBuilder b, int tire, int hub, float cx, float cy, float cz, float r, float w)
        {
            var xf = Xf.I.T(cx + w * 0.5f, cy, cz).Rz(90);
            Prims.Cyl(b, tire, xf, r, r, w, 9, false, 0.0f);
            Prims.Cyl(b, hub, Xf.I.T(cx + w * 0.5f + 0.014f, cy, cz).Rz(90), r * 0.42f, r * 0.42f, w + 0.028f, 6, false, 0.0f);
        }

        static MeshData MineCart(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("minecart", bm, v);
            var body = b.Tones(PaletteData.Overall, 0.1f);
            var steel = SteelT(b);
            int dark = b.Mat(PaletteData.Steel.Darken(0.58f), 0.2f);
            // chasis y ruedas
            Prims.Slab(b, dark, Xf.I.T(0, 0.17f, 0), 0.22f, 0.07f, 0.4f, 5f, 8);
            foreach (float z in new[] { -0.27f, 0.27f })
            {
                Prims.Cyl(b, dark, Xf.I.T(0.36f, 0.13f, z).Rz(90), 0.024f, 0.024f, 0.72f, 5, false, 0f, false, false);
                WheelAt(b, steel[0], steel[2], 0.33f, 0.13f, z, 0.13f, 0.055f);
                WheelAt(b, steel[0], steel[2], -0.33f - 0.055f, 0.13f, z, 0.13f, 0.055f);
            }
            // acople
            foreach (float s in new[] { -1f, 1f })
            {
                Prims.Cyl(b, dark, Xf.I.T(0, 0.26f, s * 0.46f).Rx(s > 0 ? 90 : -90), 0.028f, 0.028f, 0.13f, 5, false, 0f, false, false);
                Sp(b, dark, 0, 0.26f, s * 0.6f, 0.04f, 1f, 5, 3);
            }
            // caja afinada hacia abajo
            Prims.Loft(b, body[1], Xf.I, new[] { 0.25f, 0.33f, 0.37f }, new[] { 0.38f, 0.46f, 0.49f }, new[] { 0.2f, 0.4f, 0.58f }, 4f, 14, true, false);
            // interior oscuro
            Prims.Loft(b, dark, Xf.I.T(0, 0.5f, 0), new[] { 0.34f, 0.34f }, new[] { 0.46f, 0.46f }, new[] { 0f, 0.02f }, 4f, 14, false, true);
            // cintas laterales (luz y sombra)
            foreach (float s in new[] { -1f, 1f })
                Prims.Slab(b, body[0], Xf.I.T(s * 0.35f, 0.3f, 0).Rz(s * 10f), 0.025f, 0.22f, 0.12f, 4f, 6);
            // borde
            Prims.RingTube(b, steel[2], Xf.I.T(0, 0.585f, 0), 0.37f, 0.49f, 0.036f, 4f, 14, 4);
            // carga
            Rgb oc = v == 1 ? PaletteData.Gem : (v == 2 ? PaletteData.Gold : P(bm, "ore"));
            float spec = v == 0 ? 0.12f : 0.5f;
            float em = bm == 3 && v == 0 ? 0.3f : 0f;
            var ot = b.Tones(oc, spec, em);
            int glint = b.Mat(oc.Lighten(0.6f), 0.7f, oc.Lighten(0.4f).Mul(0.45f));
            for (int i = 0; i < 7; i++)
            {
                float x = rng.Range(-0.24f, 0.24f), z = rng.Range(-0.36f, 0.36f);
                float d = (float)Math.Sqrt((x / 0.26f) * (x / 0.26f) + (z / 0.4f) * (z / 0.4f));
                float r = rng.Range(0.1f, 0.15f);
                float y = 0.6f + (1f - Math.Min(1f, d)) * 0.1f;
                int sl = ot[Math.Min(1, Shade(x, z, y - 0.62f))];
                Bl(b, sl, x, y, z, r, 90 + i + v * 9, 0.85f, 6, 3, 0.34f, true);
            }
            for (int i = 0; i < 3; i++)
                Sp(b, glint, rng.Range(-0.2f, 0.2f), 0.74f + rng.Range(0, 0.04f), rng.Range(-0.3f, 0.3f), 0.035f, 1f, 4, 2, true);
            return b.Build();
        }

        // ------------------------------------------------------------ riel (2 unidades a lo largo de Z, enganchable)
        static readonly V2[] RailProfile =
        {
            new V2(-0.055f, 0f), new V2(0.055f, 0f), new V2(0.055f, 0.014f), new V2(0.018f, 0.024f), new V2(0.018f, 0.048f),
            new V2(0.036f, 0.056f), new V2(0.036f, 0.074f), new V2(0.026f, 0.08f), new V2(-0.026f, 0.08f), new V2(-0.036f, 0.074f),
            new V2(-0.036f, 0.056f), new V2(-0.018f, 0.048f), new V2(-0.018f, 0.024f), new V2(-0.055f, 0.014f),
        };

        static MeshData Rail(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("rail", bm, v);
            var wood = WoodT(b);
            var steel = SteelT(b, 0.3f);
            // durmientes a 0.5 de paso, centrados en +-0.25, +-0.75: el patron continua entre tramos
            for (int i = 0; i < 4; i++)
            {
                float z = -0.75f + 0.5f * i;
                int sl = wood[(i + v) % 2 == 0 ? 1 : 0];
                Prims.Slab(b, sl, Xf.I.T(0, 0, z).Ry(rng.Range(-2f, 2f)), 0.46f, 0.06f, 0.075f, 5f, 10);
            }
            // rieles (perfil I extruido): cabeza a RailTop
            foreach (float x in new[] { -0.3f, 0.3f })
                Prims.Extrude(b, steel[x < 0 ? 1 : 2], Xf.I.T(x, 0.06f, 0), new List<V2>(RailProfile), 2f, 0f, false);
            if (v == 1)
            {
                // placas de asiento
                for (int i = 0; i < 4; i++)
                    foreach (float x in new[] { -0.3f, 0.3f })
                        Prims.Box(b, steel[0], Xf.I.T(x, 0.065f, -0.75f + 0.5f * i), 0.075f, 0.007f, 0.055f);
            }
            if (v == 2)
            {
                // balasto
                var rt = b.Tones(P(bm, "rock_d"));
                for (int i = 0; i < 10; i++)
                {
                    float z = rng.Range(-0.95f, 0.95f);
                    float x = (i % 2 == 0 ? -1 : 1) * rng.Range(0.5f, 0.62f);
                    Bl(b, rt[i % 3], x, 0.03f, z, rng.Range(0.05f, 0.08f), 200 + i, 0.6f, 6, 3, 0.3f, true);
                }
            }
            return b.Build();
        }

        // ------------------------------------------------------------ farol
        static void LampHang(MeshBuilder b, int[] steel, int glass, float x, float yTop, float z, bool sit)
        {
            // yTop = altura del punto de cuelgue (o de apoyo si sit)
            float y0 = sit ? yTop : yTop - 0.24f; // base del farol
            Prims.Cyl(b, steel[0], Xf.I.T(x, y0, z), 0.07f, 0.085f, 0.05f, 8, false, 0.012f);
            Prims.Sphere(b, glass, Xf.I.T(x, y0 + 0.14f, z).S(1f, 1.3f, 1f), 0.1f, 10, 6);
            Prims.Cyl(b, steel[0], Xf.I.T(x, y0 + 0.255f, z), 0.12f, 0.0f, 0.08f, 8);
            Sp(b, steel[1], x, y0 + 0.345f, z, 0.026f, 1f, 6, 3);
            if (!sit) Prims.Stick(b, steel[0], Xf.I, Pt(x, y0 + 0.31f, z), Pt(x, yTop + 0.04f, z), 0.01f, 0.01f, 4, false, false);
        }

        static MeshData Lantern(int bm, int v)
        {
            var b = new MeshBuilder();
            var wood = WoodT(b);
            var steel = SteelT(b);
            int glass = b.Mat(new Rgb(1f, 0.86f, 0.52f), 0.35f, new Rgb(1f, 0.68f, 0.26f).Mul(1.0f));
            if (v == 1)
            {
                // farol de poste bajo: la lampara apoya arriba
                Prims.Cyl(b, wood[0], Xf.I, 0.14f, 0.12f, 0.08f, 8, false, 0.025f);
                Prims.Cyl(b, wood[1], Xf.I, 0.07f, 0.055f, 1.0f, 8, false, 0.008f);
                Prims.Cyl(b, steel[0], Xf.I.T(0, 0.98f, 0), 0.1f, 0.085f, 0.04f, 8, false, 0.01f);
                LampHang(b, steel, glass, 0, 1.02f, 0, true);
                b.HasAnchor = true; b.Anchor = Pt(0, 1.16f, 0);
            }
            else if (v == 2)
            {
                // travesano con dos faroles
                Prims.Cyl(b, wood[0], Xf.I, 0.15f, 0.13f, 0.09f, 8, false, 0.025f);
                Prims.Cyl(b, wood[1], Xf.I, 0.07f, 0.055f, 1.55f, 8, false, 0.008f);
                Prims.Cyl(b, wood[2], Xf.I.T(0.36f, 1.5f, 0).Rz(90), 0.035f, 0.032f, 0.72f, 6, false, 0.008f);
                LampHang(b, steel, glass, -0.34f, 1.5f, 0, false);
                LampHang(b, steel, glass, 0.34f, 1.5f, 0, false);
                b.HasAnchor = true; b.Anchor = Pt(0, 1.4f, 0);
            }
            else
            {
                Prims.Cyl(b, wood[0], Xf.I, 0.15f, 0.13f, 0.09f, 8, false, 0.025f);
                Prims.Cyl(b, wood[1], Xf.I, 0.07f, 0.055f, 1.5f, 8, false, 0.008f);
                Prims.Stick(b, wood[2], Xf.I, Pt(0, 1.43f, 0), Pt(0.34f, 1.5f, 0), 0.035f, 0.03f, 6);
                Prims.Stick(b, wood[0], Xf.I, Pt(0, 1.15f, 0), Pt(0.26f, 1.46f, 0), 0.022f, 0.02f, 5);
                LampHang(b, steel, glass, 0.34f, 1.5f, 0, false);
                b.HasAnchor = true; b.Anchor = Pt(0.34f, 1.4f, 0);
            }
            return b.Build();
        }

        // ------------------------------------------------------------ puntal (marco de tunel, en el plano XY)
        // viga horizontal a lo largo de X (centrada en cx): Slab girado
        static void HBeam(MeshBuilder b, int slot, float cx, float y, float z, float len, float hh, float hd)
        {
            Prims.Slab(b, slot, Xf.I.T(cx - len * 0.5f, y, z).Rz(-90), hh, len, hd, 5f, 10);
        }

        static MeshData Beam(int bm, int v)
        {
            var b = new MeshBuilder();
            var wood = WoodT(b);
            var steel = SteelT(b);
            float H = 1.7f, X = 0.9f;
            foreach (float x in new[] { -X, X })
            {
                Prims.Slab(b, wood[x < 0 ? 2 : 1], Xf.I.T(x, 0, 0), 0.1f, H, 0.115f, 5f, 10);
                Prims.Slab(b, wood[0], Xf.I.T(x, 0, 0), 0.17f, 0.07f, 0.18f, 5f, 10);
            }
            // dintel
            HBeam(b, wood[1], 0, H, 0, 2.4f, 0.1f, 0.13f);
            if (v >= 1)
            {
                foreach (float x in new[] { -X, X })
                {
                    float s = x < 0 ? 1f : -1f;
                    Prims.Slab(b, wood[0], Xf.I.T(x + s * 0.3f, H - 0.55f, 0.0f).Rz(-s * 45f), 0.07f, 0.8f, 0.08f, 5f, 8);
                }
            }
            if (v == 2)
            {
                // tablon superior y calzas
                HBeam(b, wood[2], 0, H + 0.2f, 0, 1.9f, 0.06f, 0.15f);
                foreach (float x in new[] { -0.5f, 0.5f })
                    Prims.Box(b, wood[0], Xf.I.T(x, H + 0.17f, 0.0f), 0.09f, 0.08f, 0.1f);
            }
            // pernos
            foreach (float x in new[] { -X, X })
                Sp(b, steel[1], x, H + 0.12f, 0.135f, 0.036f, 0.8f, 5, 3);
            return b.Build();
        }

        // ------------------------------------------------------------ barril
        static MeshData Barrel(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("barrel", bm, v);
            var wood = WoodT(b);
            var steel = SteelT(b, 0.18f);
            int hoop = b.Mat(PaletteData.Steel.Darken(0.5f), 0.25f);
            float h = 0.7f, re = 0.22f, rm = 0.3f;
            Func<float, float> rad = y => re + (rm - re) * (1f - (float)Math.Pow(2f * y / h - 1f, 2));
            var prof = new List<P2> { new P2(0, 0), new P2(re * 0.98f, 0, true) };
            int n = 7;
            for (int i = 1; i <= n; i++) { float y = h * i / n; prof.Add(new P2(rad(y), y)); }
            if (v == 3) b.Push(Xf.I.T(0, 0.3f, 0).Rz(90).T(0, -0.35f, 0));
            Prims.Lathe(b, wood[1], Xf.I, prof, 12, true);
            // tapa
            if (v == 1)
            {
                var oc = b.Tones(P(bm, "ore"), 0.12f, bm == 3 ? 0.3f : 0f);
                Prims.Lathe(b, steel[0], Xf.I, new List<P2> { new P2(re * 1.0f, h), new P2(0, h - 0.04f) }, 12, true);
                for (int i = 0; i < 6; i++)
                {
                    float a = rng.Range(0, 6.28f), d = rng.Range(0, 0.14f);
                    Bl(b, oc[i % 3], -(float)Math.Sin(a) * d, h + 0.02f - d * 0.15f, (float)Math.Cos(a) * d, rng.Range(0.09f, 0.13f), 300 + i, 0.8f, 6, 3, 0.3f, true);
                }
            }
            else
            {
                Prims.Lathe(b, wood[2], Xf.I, new List<P2> { new P2(re * 1.0f, h + 0.002f), new P2(0, h + 0.002f) }, 12, true);
                
            }
            foreach (float y in new[] { 0.1f, 0.35f, 0.6f })
            {
                var band = new List<P2>
                {
                    new P2(rad(y - 0.028f) + 0.004f, y - 0.028f), new P2(rad(y - 0.018f) + 0.016f, y - 0.018f),
                    new P2(rad(y + 0.018f) + 0.016f, y + 0.018f), new P2(rad(y + 0.028f) + 0.004f, y + 0.028f),
                };
                Prims.Lathe(b, hoop, Xf.I, band, 12, true);
            }
            if (v == 2)
            {
                Prims.Cyl(b, steel[1], Xf.I.T(0, 0.22f, rad(0.22f) - 0.02f).Rx(90), 0.026f, 0.026f, 0.12f, 6);
                Sp(b, steel[2], 0, 0.22f, rad(0.22f) + 0.1f, 0.034f, 1f, 6, 3);
            }
            if (v == 3) b.Pop();
            return b.Build();
        }

        // ------------------------------------------------------------ cajon
        static void CrateAt(MeshBuilder b, int[] wood, float s, float ox, float oy, float oz, float rotY, bool rich)
        {
            b.Push(Xf.I.T(ox, oy, oz).Ry(rotY));
            float h = s * 0.5f;
            Prims.RoundBox(b, wood[1], Xf.I.T(0, h, 0), h - 0.012f, h - 0.012f, h - 0.012f, 0.045f * s / 0.62f, 1);
            float e = 0.045f * s / 0.62f;
            if (rich)
            {
                foreach (float x in new[] { -1f, 1f })
                    foreach (float z in new[] { -1f, 1f })
                        Prims.Slab(b, wood[0], Xf.I.T(x * (h - e * 0.65f), 0, z * (h - e * 0.65f)), e * 0.9f, s, e * 0.9f, 4f, 8);
            }
            else
            {
                foreach (float x in new[] { -1f, 1f })
                    foreach (float z in new[] { -1f, 1f })
                        Prims.Box(b, wood[0], Xf.I.T(x * (h - e * 0.5f), h, z * (h - e * 0.5f)), e, h, e);
            }
            // marcos arriba y abajo (4 caras)
            foreach (float y in new[] { 0.09f * s / 0.62f, s - 0.09f * s / 0.62f })
            {
                Prims.Box(b, wood[2], Xf.I.T(0, y, h), h - e, 0.03f * s / 0.62f, 0.012f);
                Prims.Box(b, wood[2], Xf.I.T(0, y, -h), h - e, 0.03f * s / 0.62f, 0.012f);
                Prims.Box(b, wood[2], Xf.I.T(h, y, 0), 0.012f, 0.03f * s / 0.62f, h - e);
                Prims.Box(b, wood[2], Xf.I.T(-h, y, 0), 0.012f, 0.03f * s / 0.62f, h - e);
            }
            // tabla en diagonal en las caras frontal y trasera
            float len = (s - 0.2f * s / 0.62f) * 1.3f;
            Prims.Box(b, wood[2], Xf.I.T(0, h, h).Rz(38), 0.032f * s / 0.62f, len * 0.5f, 0.012f);
            Prims.Box(b, wood[2], Xf.I.T(0, h, -h).Rz(-38), 0.032f * s / 0.62f, len * 0.5f, 0.012f);
            b.Pop();
        }

        static MeshData Crate(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("crate", bm, v);
            var wood = WoodT(b);
            switch (v)
            {
                case 0: CrateAt(b, wood, 0.62f, 0, 0, 0, 0, true); break;
                case 1:
                {
                    CrateAt(b, wood, 0.62f, 0, 0, 0, 0, true);
                    var oc = b.Tones(P(bm, "ore"), 0.12f, bm == 3 ? 0.3f : 0f);
                    for (int i = 0; i < 5; i++)
                    {
                        float x = rng.Range(-0.18f, 0.18f), z = rng.Range(-0.18f, 0.18f);
                        Bl(b, oc[Shade(x, z, 0.3f)], x, 0.64f + (0.1f - Math.Abs(x) * 0.3f), z, rng.Range(0.1f, 0.14f), 400 + i, 0.85f, 7, 4, 0.3f, true);
                    }
                    // tapa entreabierta apoyada atras
                    Prims.RoundBox(b, wood[2], Xf.I.T(0, 0.62f, -0.31f).Rx(-115f).T(0, 0.02f, 0.31f), 0.28f, 0.025f, 0.31f, 0.02f, 1);
                    break;
                }
                case 2:
                    CrateAt(b, wood, 0.62f, 0, 0, 0, 0, true);
                    CrateAt(b, wood, 0.46f, 0.02f, 0.62f, 0, 24, false);
                    break;
                default:
                    CrateAt(b, wood, 0.46f, -0.28f, 0, 0.04f, 8, true);
                    CrateAt(b, wood, 0.4f, 0.26f, 0, -0.05f, -16, false);
                    break;
            }
            return b.Build();
        }

        // ------------------------------------------------------------ monton de mineral
        static MeshData OrePile(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("ore_pile", bm, v);
            Rgb oc = P(bm, "ore");
            var ot = b.Tones(oc, 0.2f, bm == 3 ? 0.32f : 0.06f);
            var rt = b.Tones(P(bm, "rock"));
            int glint = b.Mat(oc.Lighten(0.6f), 0.8f, oc.Lighten(0.4f).Mul(0.5f));
            Bl(b, rt[0], 0, 0.05f, 0, 0.34f, 500 + v, 0.35f, 8, 3, 0.2f, true);
            int n = new[] { 9, 13, 6, 10 }[v];
            float spread = v == 1 ? 0.42f : 0.34f;
            for (int i = 0; i < n; i++)
            {
                float a = rng.Range(0, 6.2832f);
                float d = (float)Math.Sqrt(rng.Next()) * spread;
                float k = 1f - d / (spread + 0.1f);
                float r = rng.Range(0.09f, 0.16f) * (0.6f + k * 0.7f) * (v == 2 ? 1.4f : 1f);
                float dx = -(float)Math.Sin(a), dz = (float)Math.Cos(a);
                float y = 0.1f + k * 0.2f + r * 0.35f;
                Bl(b, ot[Shade(dx, dz, k - 0.3f)], dx * d, y, dz * d, r, 520 + i * 3 + v, 0.78f, 6, 3, 0.36f, true);
            }
            for (int i = 0; i < 3 + v; i++)
            {
                float a = rng.Range(0, 6.2832f), d = rng.Range(0.05f, 0.34f);
                float k = 1f - d / 0.5f;
                Sp(b, glint, -(float)Math.Sin(a) * d, 0.17f + k * 0.14f, (float)Math.Cos(a) * d, rng.Range(0.03f, 0.045f), 1f, 5, 2, true);
            }
            if (v == 3)
            {
                var rk = b.Tones(P(bm, "rock"));
                Bl(b, rk[1], -0.4f, 0.12f, 0.25f, 0.17f, 560, 0.7f, 7, 3, 0.35f, true);
                Bl(b, rk[2], 0.42f, 0.1f, -0.2f, 0.13f, 561, 0.7f, 7, 3, 0.35f, true);
            }
            return b.Build();
        }

        // ------------------------------------------------------------ lingote
        static void IngotAt(MeshBuilder b, int[] g, float x, float y, float z, float rotY)
        {
            b.Push(Xf.I.T(x, y, z).Ry(rotY));
            var prof = new List<V2> { new V2(-0.26f, 0), new V2(0.26f, 0), new V2(0.205f, 0.16f), new V2(-0.205f, 0.16f) };
            Prims.Extrude(b, g[1], Xf.I, prof, 0.25f, 0.03f, false);
            // placa superior mas clara, hundida
            var top = new List<V2> { new V2(-0.17f, 0), new V2(0.17f, 0), new V2(0.15f, 0.03f), new V2(-0.15f, 0.03f) };
            Prims.Extrude(b, g[2], Xf.I.T(0, 0.158f, 0), top, 0.15f, 0.0f, false);
            b.Pop();
        }

        static MeshData Ingot(int bm, int v)
        {
            var b = new MeshBuilder();
            var g = new[]
            {
                b.Mat(PaletteData.GoldD, 0.6f), b.Mat(PaletteData.Gold, 0.6f), b.Mat(PaletteData.Gold.Lighten(0.45f), 0.7f),
            };
            b.Push(Xf.I.S(1.35f));
            if (v == 0) IngotAt(b, g, 0, 0, 0, 0);
            else if (v == 1)
            {
                IngotAt(b, g, 0, 0, -0.14f, 0);
                IngotAt(b, g, 0, 0, 0.14f, 0);
                IngotAt(b, g, 0, 0.16f, 0, 90);
            }
            else
            {
                for (int i = 0; i < 3; i++) IngotAt(b, g, 0, 0, -0.28f + 0.28f * i, 0);
                IngotAt(b, g, 0, 0.16f, -0.14f, 0);
                IngotAt(b, g, 0, 0.16f, 0.14f, 0);
                IngotAt(b, g, 0, 0.32f, 0, 0);
            }
            b.Pop();
            return b.Build();
        }

        // ------------------------------------------------------------ gema (sobre una roca chica)
        static MeshData GemProp(int bm, int v)
        {
            var b = new MeshBuilder();
            var rng = R("gem", bm, v);
            var rt = b.Tones(P(bm, "rock"));
            Rgb[] cols = { PaletteData.Gem, PaletteData.Rank(1), PaletteData.Rank(3), PaletteData.Gem };
            int gs = b.Mat(cols[v], 0.95f, cols[v].Mul(0.14f));
            int gs2 = b.Mat(v == 3 ? PaletteData.GemD : cols[v].Lighten(0.2f), 0.95f, cols[v].Mul(0.14f));
            b.Push(Xf.I.S(1.3f));
            Bl(b, rt[0], 0, 0.07f, 0, 0.3f, 600 + v, 0.5f, 8, 4, 0.3f, true);
            Bl(b, rt[2], -0.1f, 0.12f, -0.08f, 0.14f, 610 + v, 0.6f, 6, 3, 0.3f, true);
            if (v == 3)
            {
                float s = 0.17f / 0.5f;
                Gems.Gem(b, gs, Xf.I.T(0, 0.25f, 0).Rz(14).Ry(20).S(s * 1.1f), 0.5f);
                Gems.Gem(b, gs2, Xf.I.T(0.2f, 0.17f, 0.08f).Rz(-25).Ry(-30).S(s * 0.75f), 0.5f);
                Gems.Gem(b, gs, Xf.I.T(-0.17f, 0.16f, 0.12f).Rx(20).Rz(30).S(s * 0.7f), 0.5f);
            }
            else
            {
                Gems.Gem(b, gs, Xf.I.T(0, 0.25f, 0).Rz(16).Ry(rng.Range(0, 90)).S(0.2f / 0.5f * 1.1f), 0.5f);
            }
            b.Pop();
            return b.Build();
        }

        // ------------------------------------------------------------ soporte de picos
        static void PickAt(MeshBuilder b, int[] wood, int[] steel, int head, Xf xf, float len)
        {
            b.Push(xf);
            Prims.Stick(b, wood[1], Xf.I, Pt(0, 0, 0), Pt(0, len, 0), 0.032f, 0.027f, 5, false, true);
            var path = new List<V3>
            {
                Pt(-0.23f, len - 0.11f, 0), Pt(-0.115f, len - 0.02f, 0), Pt(0, len + 0.02f, 0), Pt(0.115f, len - 0.02f, 0), Pt(0.23f, len - 0.11f, 0),
            };
            Prims.Tube(b, head, Xf.I, path, new List<float> { 0.014f, 0.048f, 0.062f, 0.048f, 0.014f }, 5, true, true);
            Prims.Cyl(b, steel[0], Xf.I.T(0, len - 0.04f, 0), 0.05f, 0.05f, 0.07f, 6, false, 0f, false, false);
            b.Pop();
        }

        static MeshData PickaxeStand(int bm, int v)
        {
            var b = new MeshBuilder();
            var wood = WoodT(b);
            var steel = SteelT(b);
            int[][] ranks = { new[] { 0, 1, 2 }, new[] { 2, 3, 4 }, new[] { 4, 5, 1 } };
            foreach (float x in new[] { -0.46f, 0.46f })
            {
                Prims.Slab(b, wood[0], Xf.I.T(x, 0, 0.04f), 0.07f, 0.055f, 0.3f, 5f, 10);
                Prims.Cyl(b, wood[x < 0 ? 2 : 1], Xf.I.T(x, 0.03f, 0.04f), 0.048f, 0.042f, 0.6f, 7, false, 0f);
            }
            Prims.Cyl(b, wood[1], Xf.I.T(0.52f, 0.5f, 0.04f).Rz(90), 0.04f, 0.04f, 1.04f, 7, false, 0f);
            float[] lens = { 0.9f, 1.02f, 0.95f };
            for (int i = 0; i < 3; i++)
            {
                int head = b.Mat(PaletteData.Rank(ranks[v][i]), 0.4f);
                float x = -0.3f + 0.3f * i;
                PickAt(b, wood, steel, head, Xf.I.T(x, 0.02f, 0.22f).Rx(-20f), lens[i]);
            }
            return b.Build();
        }

        // ------------------------------------------------------------ cartel con flecha (la flecha apunta a +X)
        static MeshData Sign(int bm, int v)
        {
            var b = new MeshBuilder();
            var wood = WoodT(b);
            var steel = SteelT(b);
            int cream = b.Mat(PaletteData.Ivory);
            float ph = v == 2 ? 1.1f : (v == 1 ? 0.95f : 0.9f);
            Prims.Cyl(b, wood[0], Xf.I, 0.075f, 0.06f, 0.05f, 8, false, 0.015f);
            Prims.Cyl(b, wood[1], Xf.I, 0.052f, 0.045f, ph, 8, false, 0.012f);
            var arrow = new List<V2> { new V2(-0.4f, -0.14f), new V2(0.27f, -0.14f), new V2(0.53f, 0f), new V2(0.27f, 0.14f), new V2(-0.4f, 0.14f) };
            if (v == 0)
            {
                Prims.Extrude(b, wood[0], Xf.I.T(0.04f, ph - 0.12f, 0f), Scale2(arrow, 1.12f), 0.1f, 0.012f, true);
                Prims.Extrude(b, wood[2], Xf.I.T(0.04f, ph - 0.12f, 0f), arrow, 0.125f, 0.02f, true);
                foreach (float y in new[] { -0.07f, 0.07f })
                    foreach (float z in new[] { -0.07f, 0.07f })
                        Sp(b, steel[1], -0.3f, ph - 0.12f + y, z, 0.02f, 0.7f, 5, 3);
            }
            else if (v == 1)
            {
                Prims.RoundBox(b, wood[0], Xf.I.T(0, ph - 0.1f, 0.085f), 0.4f, 0.2f, 0.03f, 0.02f, 1);
                var ar = new List<V2> { new V2(-0.28f, -0.045f), new V2(0.06f, -0.045f), new V2(0.06f, -0.12f), new V2(0.3f, 0f), new V2(0.06f, 0.12f), new V2(0.06f, 0.045f), new V2(-0.28f, 0.045f) };
                Prims.Extrude(b, cream, Xf.I.T(0, ph - 0.1f, 0.115f), ar, 0.016f, 0.0f, false);
                Prims.Extrude(b, cream, Xf.I.T(0, ph - 0.1f, 0.055f), ar, 0.016f, 0.0f, false);
            }
            else
            {
                Prims.Extrude(b, wood[2], Xf.I.T(0.04f, ph - 0.12f, 0.06f).Rz(4), arrow, 0.06f, 0.015f, true);
                var left = new List<V2>();
                foreach (var p in arrow) left.Add(new V2(-p.x, p.y));
                Prims.Extrude(b, wood[1], Xf.I.T(-0.04f, ph - 0.42f, 0.06f).Rz(-3), left, 0.06f, 0.015f, true);
                Sp(b, steel[1], -0.3f, ph - 0.12f, 0.1f, 0.018f, 0.7f, 5, 3);
                Sp(b, steel[1], 0.3f, ph - 0.42f, 0.1f, 0.018f, 0.7f, 5, 3);
            }
            return b.Build();
        }

        static List<V2> Scale2(List<V2> p, float s)
        {
            var r = new List<V2>();
            foreach (var q in p) r.Add(new V2(q.x * s, q.y * s));
            return r;
        }
    }
}
