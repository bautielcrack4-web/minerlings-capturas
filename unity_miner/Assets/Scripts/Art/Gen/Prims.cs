using System;
using System.Collections.Generic;

namespace Mineros.Art.Gen
{
    /// <summary>Punto de un perfil de revolucion (radio, altura). hard = el perfil se corta aca (arista dura).</summary>
    public struct P2
    {
        public float r, y;
        public bool hard;
        public P2(float r, float y, bool hard = false) { this.r = r; this.y = y; this.hard = hard; }
    }

    /// <summary>
    /// Primitivas propias del kit. Todas escriben en un MeshBuilder con la transformacion xf (local de la pieza).
    /// Convenciones: revolucion alrededor de Y con el angulo 0 hacia +Z; "base" = y 0 cuando se aclara; segmentos bajos
    /// a proposito (juguete de vinilo, rendimiento movil). flat = facetado (normal por cara).
    /// </summary>
    public static class Prims
    {
        const float Pi = (float)Math.PI;

        // ------------------------------------------------------------ revolucion
        /// <summary>Lathe: revoluciona un perfil (r,y). Los puntos con hard=true parten el perfil en tiras con aristas duras
        /// (tapas planas). El perfil debe recorrerse de modo que la normal (dPerfil x dAngulo) salga hacia afuera: hacia
        /// arriba por el costado, hacia afuera por la tapa de abajo, hacia adentro por la de arriba.</summary>
        public static void Lathe(MeshBuilder b, int slot, Xf xf, IList<P2> prof, int seg, bool flat = false, float phaseDeg = 0f)
        {
            b.Push(xf);
            int start = 0;
            for (int k = 1; k < prof.Count; k++)
            {
                bool last = k == prof.Count - 1;
                if (prof[k].hard || last)
                {
                    LatheStrip(b, slot, prof, start, k, seg, flat, phaseDeg);
                    start = k;
                }
            }
            b.Pop();
        }

        static void LatheStrip(MeshBuilder b, int slot, IList<P2> prof, int s, int e, int seg, bool flat, float phaseDeg)
        {
            int rows = e - s + 1;
            var p = new V3[rows, seg];
            for (int i = 0; i < rows; i++)
            {
                var q = prof[s + i];
                for (int j = 0; j < seg; j++)
                {
                    float th = phaseDeg * Pi / 180f + 2f * Pi * j / seg;
                    p[i, j] = new V3(-q.r * (float)Math.Sin(th), q.y, q.r * (float)Math.Cos(th));
                }
            }
            b.Grid(slot, p, true, flat);
        }

        static List<P2> ArcProfile(float r, float a0Deg, float a1Deg, int rings)
        {
            var l = new List<P2>();
            for (int k = 0; k <= rings; k++)
            {
                float a = (a0Deg + (a1Deg - a0Deg) * k / rings) * Pi / 180f;
                l.Add(new P2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r));
            }
            return l;
        }

        /// <summary>Esfera (elipsoide con la escala de xf) de radio r centrada en el origen.</summary>
        public static void Sphere(MeshBuilder b, int slot, Xf xf, float r = 1f, int seg = 10, int rings = 6, bool flat = false)
        {
            Lathe(b, slot, xf, ArcProfile(r, -90, 90, rings), seg, flat);
        }

        /// <summary>Hemisferio superior con base plana en y = 0.</summary>
        public static void Dome(MeshBuilder b, int slot, Xf xf, float r = 1f, int seg = 10, int rings = 3, bool flat = false, bool bottomCap = true)
        {
            var prof = new List<P2>();
            if (bottomCap)
            {
                prof.Add(new P2(0, 0));
                prof.Add(new P2(r, 0, true));
            }
            else prof.Add(new P2(r, 0, false));
            var arc = ArcProfile(r, 0, 90, rings);
            for (int i = 1; i < arc.Count; i++) prof.Add(arc[i]);
            Lathe(b, slot, xf, prof, seg, flat);
        }

        /// <summary>Capsula de radio r y altura total h (incluye tapas), con la base en y = 0.</summary>
        public static void Capsule(MeshBuilder b, int slot, Xf xf, float r, float h, int seg = 10, int capRings = 3, bool flat = false)
        {
            h = Math.Max(h, 2 * r);
            var prof = new List<P2>();
            prof.Add(new P2(0, 0));
            var bot = ArcProfile(r, -90, 0, capRings);
            for (int i = 1; i < bot.Count; i++) prof.Add(new P2(bot[i].r, bot[i].y + r));
            var top = ArcProfile(r, 0, 90, capRings);
            for (int i = 0; i < top.Count; i++) prof.Add(new P2(top[i].r, top[i].y + h - r));
            Lathe(b, slot, xf, prof, seg, flat);
        }

        /// <summary>Cilindro / cono truncado de base y = 0 a y = h. bevel > 0 redondea (chaflan) los bordes de las tapas.
        /// r1 = 0 da un cono con punta.</summary>
        public static void Cyl(MeshBuilder b, int slot, Xf xf, float r0, float r1, float h, int seg = 10, bool flat = false,
            float bevel = 0f, bool capBottom = true, bool capTop = true)
        {
            var prof = new List<P2>();
            bool tip = r1 <= 1e-5f;
            float bv0 = Math.Min(bevel, r0 * 0.9f), bv1 = tip ? 0 : Math.Min(bevel, r1 * 0.9f);
            if (capBottom) prof.Add(new P2(0, 0));
            if (bv0 > 0)
            {
                prof.Add(new P2(r0 - bv0, 0, capBottom));
                prof.Add(new P2(r0, bv0));
            }
            else prof.Add(new P2(r0, 0, capBottom));
            if (tip) prof.Add(new P2(0, h));
            else
            {
                if (bv1 > 0)
                {
                    prof.Add(new P2(r1, h - bv1));
                    prof.Add(new P2(r1 - bv1, h, capTop));
                }
                else prof.Add(new P2(r1, h, capTop));
                if (capTop) prof.Add(new P2(0, h));
            }
            // si no hay tapa de abajo, el primer punto no puede ser "duro"
            Lathe(b, slot, xf, prof, seg, flat);
        }

        public static void Cone(MeshBuilder b, int slot, Xf xf, float r, float h, int seg = 8, bool flat = false)
        {
            Cyl(b, slot, xf, r, 0f, h, seg, flat);
        }

        // ------------------------------------------------------------ blob organico
        /// <summary>Esfera con ruido suave (copas, rocas, montones). amp ~0.1-0.3. Con flat=true y pocos segmentos da rocas
        /// facetadas.</summary>
        public static void Blob(MeshBuilder b, int slot, Xf xf, int seed, float r = 1f, int seg = 10, int rings = 6,
            float amp = 0.15f, bool flat = false, float freq = 1.4f)
        {
            int R = rings + 1;
            var p = new V3[R, seg];
            for (int i = 0; i < R; i++)
            {
                float a = (-90f + 180f * i / rings) * Pi / 180f;
                float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                for (int j = 0; j < seg; j++)
                {
                    float th = 2f * Pi * j / seg;
                    var d = new V3(-ca * (float)Math.Sin(th), sa, ca * (float)Math.Cos(th));
                    float n = Noise.Value(d.x * freq + 7.3f, d.y * freq + 3.1f, d.z * freq + 5.7f, seed)
                        + 0.5f * Noise.Value(d.x * freq * 2.1f + 1.7f, d.y * freq * 2.1f, d.z * freq * 2.1f + 9.2f, seed + 17);
                    p[i, j] = d * (r * (1f + amp * n * 0.75f));
                }
            }
            b.Push(xf);
            b.Grid(slot, p, true, flat);
            b.Pop();
        }

        // ------------------------------------------------------------ caja redondeada
        static float[] BoxSamples(float h, float r, int bs)
        {
            float inner = Math.Max(h - r, 0.0005f);
            var l = new List<float>();
            for (int k = bs; k >= 1; k--) l.Add(-(inner + (h - inner) * (float)Math.Tan(Pi / 4f * k / bs)));
            l.Add(-inner);
            l.Add(inner);
            for (int k = 1; k <= bs; k++) l.Add(inner + (h - inner) * (float)Math.Tan(Pi / 4f * k / bs));
            return l.ToArray();
        }

        /// <summary>Caja con aristas redondeadas (bevel), centrada en el origen. hx/hy/hz = semiextensiones, rad = radio del
        /// bevel, bs = pasos del redondeo (1 = chaflan suave, 2 = redondeo).</summary>
        public static void RoundBox(MeshBuilder b, int slot, Xf xf, float hx, float hy, float hz, float rad, int bs = 1, bool flat = false)
        {
            rad = Math.Min(rad, Math.Min(hx, Math.Min(hy, hz)) * 0.98f);
            float[] h = { hx, hy, hz };
            var samp = new[] { BoxSamples(hx, rad, bs), BoxSamples(hy, rad, bs), BoxSamples(hz, rad, bs) };
            float[] inner = { Math.Max(hx - rad, 0.0005f), Math.Max(hy - rad, 0.0005f), Math.Max(hz - rad, 0.0005f) };
            b.Push(xf);
            for (int ax = 0; ax < 3; ax++)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
                    // sgn>0: dv=a1,du=a2 ; sgn<0: dv=a2,du=a1 (normal hacia afuera)
                    int av = sgn > 0 ? a1 : a2, au = sgn > 0 ? a2 : a1;
                    var sv = samp[av]; var su = samp[au];
                    var p = new V3[sv.Length, su.Length];
                    var n = new V3[sv.Length, su.Length];
                    for (int i = 0; i < sv.Length; i++)
                        for (int j = 0; j < su.Length; j++)
                        {
                            float[] P = new float[3];
                            P[ax] = sgn * h[ax]; P[av] = sv[i]; P[au] = su[j];
                            float[] q = new float[3];
                            for (int k = 0; k < 3; k++) q[k] = Math.Max(-inner[k], Math.Min(inner[k], P[k]));
                            var d = new V3(P[0] - q[0], P[1] - q[1], P[2] - q[2]).Normalized;
                            p[i, j] = new V3(q[0], q[1], q[2]) + d * rad;
                            n[i, j] = d;
                        }
                    b.Grid(slot, p, false, flat, flat ? null : n);
                }
            b.Pop();
        }

        /// <summary>Caja redondeada con la base en y = 0 (ancho w, alto hgt, fondo d).</summary>
        public static void Block(MeshBuilder b, int slot, Xf xf, float w, float hgt, float d, float rad, int bs = 1, bool flat = false)
        {
            RoundBox(b, slot, xf.T(0, hgt * 0.5f, 0), w * 0.5f, hgt * 0.5f, d * 0.5f, rad, bs, flat);
        }

        // ------------------------------------------------------------ loft de superelipses (vagonetas, cajones redondeados)
        /// <summary>Punto de una superelipse de semiejes (hw, hd): n=2 elipse, n=4 cuadrado redondeado. th como en Lathe.</summary>
        static V3 Super(float hw, float hd, float n, float th, float y)
        {
            float s = -(float)Math.Sin(th), c = (float)Math.Cos(th);
            float e = 2f / n;
            float x = hw * Math.Sign(s) * (float)Math.Pow(Math.Abs(s), e);
            float z = hd * Math.Sign(c) * (float)Math.Pow(Math.Abs(c), e);
            return new V3(x, y, z);
        }

        /// <summary>Solido por niveles: en cada nivel k una superelipse de ancho 2*ws[k], fondo 2*ds[k] a altura ys[k]
        /// (de abajo hacia arriba). Tapas planas opcionales. n = redondez (4 = cuadrado redondeado).</summary>
        public static void Loft(MeshBuilder b, int slot, Xf xf, float[] ws, float[] ds, float[] ys, float n = 4f, int seg = 16,
            bool capBottom = true, bool capTop = true, bool flat = false)
        {
            int R = ys.Length;
            var p = new V3[R, seg];
            for (int i = 0; i < R; i++)
                for (int j = 0; j < seg; j++)
                    p[i, j] = Super(ws[i], ds[i], n, 2f * Pi * j / seg, ys[i]);
            b.Push(xf);
            b.Grid(slot, p, true, flat);
            if (capBottom) LoftCap(b, slot, ws[0], ds[0], ys[0], n, seg, false);
            if (capTop) LoftCap(b, slot, ws[R - 1], ds[R - 1], ys[R - 1], n, seg, true);
            b.Pop();
        }

        static void LoftCap(MeshBuilder b, int slot, float hw, float hd, float y, float n, int seg, bool top)
        {
            var c = new V3(0, y, 0);
            for (int j = 0; j < seg; j++)
            {
                var a = Super(hw, hd, n, 2f * Pi * j / seg, y);
                var d = Super(hw, hd, n, 2f * Pi * (j + 1) / seg, y);
                if (top) b.FlatTri(slot, a, c, d); else b.FlatTri(slot, a, d, c);
            }
        }

        /// <summary>Aro (tubo) siguiendo una superelipse horizontal: bordes de vagoneta, marcos redondos.</summary>
        public static void RingTube(MeshBuilder b, int slot, Xf xf, float hw, float hd, float tubeR, float n = 4f, int segMajor = 16,
            int segMinor = 5, bool flat = false)
        {
            var p = new V3[segMajor, segMinor];
            for (int i = 0; i < segMajor; i++)
            {
                float th = 2f * Pi * i / segMajor;
                var c = Super(hw, hd, n, th, 0f);
                var o = new V3(c.x, 0, c.z).Normalized; // direccion hacia afuera (aprox. radial)
                var up = V3.Up;
                for (int j = 0; j < segMinor; j++)
                {
                    float ph = 2f * Pi * (segMinor - j) / segMinor;
                    p[i, j] = c + (o * (float)Math.Cos(ph) + up * (float)Math.Sin(ph)) * tubeR;
                }
            }
            b.Push(xf);
            b.Grid(slot, p, true, flat);
            b.Pop();
        }

        /// <summary>Bloque suave barato: planta de superelipse (n=4..8 = cuadrado redondeado) extruida de y = 0 a h, tapas planas.
        /// Para tablones, durmientes y detalles donde RoundBox sale caro.</summary>
        public static void Slab(MeshBuilder b, int slot, Xf xf, float hw, float h, float hd, float n = 5f, int seg = 12)
        {
            Loft(b, slot, xf, new[] { hw, hw }, new[] { hd, hd }, new[] { 0f, h }, n, seg, true, true, false);
        }

        /// <summary>Caja plana (12 triangulos) centrada en el origen, para detalles chicos.</summary>
        public static void Box(MeshBuilder b, int slot, Xf xf, float hx, float hy, float hz)
        {
            b.Push(xf);
            var c = V3.Zero;
            for (int ax = 0; ax < 3; ax++)
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    float[] h = { hx, hy, hz };
                    int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
                    V3[] q = new V3[4];
                    float[,] uv = { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };
                    for (int k = 0; k < 4; k++)
                    {
                        float[] P = new float[3];
                        P[ax] = sg * h[ax]; P[a1] = uv[k, 0] * h[a1]; P[a2] = uv[k, 1] * h[a2];
                        q[k] = new V3(P[0], P[1], P[2]);
                    }
                    b.FlatQuadOut(slot, q[0], q[1], q[2], q[3], c);
                }
            b.Pop();
        }

        // ------------------------------------------------------------ toroide
        /// <summary>Toroide en el plano XZ (eje Y), radio mayor R y menor r.</summary>
        public static void Torus(MeshBuilder b, int slot, Xf xf, float R, float r, int segMajor = 14, int segMinor = 6, bool flat = false)
        {
            var p = new V3[segMajor, segMinor];
            for (int i = 0; i < segMajor; i++)
            {
                float th = 2f * Pi * i / segMajor;
                float sx = -(float)Math.Sin(th), cz = (float)Math.Cos(th);
                for (int j = 0; j < segMinor; j++)
                {
                    float ph = 2f * Pi * j / segMinor;
                    float rr = R + r * (float)Math.Cos(ph);
                    p[i, j] = new V3(sx * rr, r * (float)Math.Sin(ph), cz * rr);
                }
            }
            b.Push(xf);
            // normal = dFila x dColumna: fila = angulo mayor, columna = angulo menor
            // (se invierte el orden de las columnas para que apunte hacia afuera)
            var q = new V3[segMajor, segMinor];
            for (int i = 0; i < segMajor; i++) for (int j = 0; j < segMinor; j++) q[i, j] = p[i, (segMinor - j) % segMinor];
            b.Grid(slot, q, true, flat);
            b.Pop();
        }

        // ------------------------------------------------------------ tubo / rama
        /// <summary>Tubo a lo largo de una curva con radio variable (ramas, raices, tallos, brazos). Tapas redondeadas
        /// opcionales. Los puntos van en local de xf.</summary>
        public static void Tube(MeshBuilder b, int slot, Xf xf, IList<V3> path, IList<float> radii, int seg = 6,
            bool capStart = true, bool capEnd = true, bool flat = false)
        {
            int n = path.Count;
            var T = new V3[n];
            for (int k = 0; k < n; k++)
            {
                V3 d = k == 0 ? path[1] - path[0] : (k == n - 1 ? path[n - 1] - path[n - 2] : path[k + 1] - path[k - 1]);
                T[k] = d.Normalized;
            }
            var N = new V3[n];
            V3 ax = Math.Abs(T[0].y) < 0.9f ? V3.Up : new V3(1, 0, 0);
            N[0] = (ax - T[0] * V3.Dot(ax, T[0])).Normalized;
            for (int k = 1; k < n; k++)
            {
                var pn = N[k - 1];
                N[k] = (pn - T[k] * V3.Dot(pn, T[k])).Normalized;
            }

            var rows = new List<V3[]>();
            Func<V3, V3, V3, float, V3[]> ring = (c, nn, bb, rad) =>
            {
                var ps = new V3[seg];
                for (int j = 0; j < seg; j++)
                {
                    float ph = 2f * Pi * j / seg;
                    ps[j] = c + (nn * (float)Math.Cos(ph) + bb * (float)Math.Sin(ph)) * rad;
                }
                return ps;
            };
            Func<int, V3> Bv = k => V3.Cross(N[k], T[k]);
            if (capStart)
            {
                float r0 = radii[0];
                rows.Add(ring(path[0] - T[0] * r0, N[0], Bv(0), 0f));
                rows.Add(ring(path[0] - T[0] * (r0 * 0.7071f), N[0], Bv(0), r0 * 0.7071f));
            }
            for (int k = 0; k < n; k++) rows.Add(ring(path[k], N[k], Bv(k), radii[k]));
            if (capEnd)
            {
                float r1 = radii[n - 1];
                rows.Add(ring(path[n - 1] + T[n - 1] * (r1 * 0.7071f), N[n - 1], Bv(n - 1), r1 * 0.7071f));
                rows.Add(ring(path[n - 1] + T[n - 1] * r1, N[n - 1], Bv(n - 1), 0f));
            }
            var p = new V3[rows.Count, seg];
            for (int i = 0; i < rows.Count; i++) for (int j = 0; j < seg; j++) p[i, j] = rows[i][j];
            b.Push(xf);
            b.Grid(slot, p, true, flat);
            b.Pop();
        }

        /// <summary>Tubo recto de a a b con radio r0 -> r1.</summary>
        public static void Stick(MeshBuilder b, int slot, Xf xf, V3 a, V3 e, float r0, float r1, int seg = 6, bool capStart = true, bool capEnd = true, bool flat = false)
        {
            Tube(b, slot, xf, new[] { a, V3.Lerp(a, e, 0.5f), e }, new[] { r0, (r0 + r1) * 0.5f, r1 }, seg, capStart, capEnd, flat);
        }

        // ------------------------------------------------------------ extrusion de perfil
        /// <summary>Extruye un perfil 2D (plano XY) a lo largo de Z, centrado (de -depth/2 a +depth/2). bevel > 0 achaflana
        /// las tapas hacia adentro (perfiles convexos o apenas concavos). smoothSides = normales suaves en el contorno.</summary>
        public static void Extrude(MeshBuilder b, int slot, Xf xf, IList<V2> poly, float depth, float bevel = 0f, bool smoothSides = false)
        {
            var pl = new List<V2>(poly);
            if (Area(pl) < 0) pl.Reverse();
            int n = pl.Count;
            float hz = depth * 0.5f;
            bevel = Math.Min(bevel, hz * 0.95f);
            var ins = new List<V2>(pl);
            if (bevel > 0)
            {
                for (int k = 0; k < n; k++)
                {
                    var a = pl[(k + n - 1) % n]; var c = pl[k]; var d = pl[(k + 1) % n];
                    var e1 = Norm2(c - a); var e2 = Norm2(d - c);
                    var n1 = new V2(e1.y, -e1.x); var n2 = new V2(e2.y, -e2.x);
                    float dt = n1.x * n2.x + n1.y * n2.y;
                    var m = (n1 + n2) * (1f / Math.Max(0.4f, 1f + dt));
                    ins[k] = c - m * bevel;
                }
            }
            b.Push(xf);
            // costados
            float[] zs = bevel > 0 ? new[] { -hz, -hz + bevel, hz - bevel, hz } : new[] { -hz, hz };
            var p = new V3[n + 1, zs.Length];
            for (int k = 0; k <= n; k++)
            {
                var o = pl[k % n]; var q = ins[k % n];
                for (int j = 0; j < zs.Length; j++)
                {
                    var v = (bevel > 0 && (j == 0 || j == zs.Length - 1)) ? q : o;
                    p[k, j] = new V3(v.x, v.y, zs[j]);
                }
            }
            b.Grid(slot, p, false, !smoothSides);
            // tapas
            var tri = Triangulate(ins);
            for (int t = 0; t < tri.Count; t += 3)
            {
                var a = ins[tri[t]]; var c = ins[tri[t + 1]]; var d = ins[tri[t + 2]];
                b.FlatTri(slot, new V3(a.x, a.y, hz), new V3(c.x, c.y, hz), new V3(d.x, d.y, hz));
                b.FlatTri(slot, new V3(a.x, a.y, -hz), new V3(d.x, d.y, -hz), new V3(c.x, c.y, -hz));
            }
            b.Pop();
        }

        static V2 Norm2(V2 v)
        {
            float l = (float)Math.Sqrt(v.x * v.x + v.y * v.y);
            return l > 1e-9f ? new V2(v.x / l, v.y / l) : new V2(1, 0);
        }

        static float Area(List<V2> p)
        {
            float a = 0;
            for (int i = 0; i < p.Count; i++)
            {
                var c = p[i]; var d = p[(i + 1) % p.Count];
                a += c.x * d.y - d.x * c.y;
            }
            return a * 0.5f;
        }

        /// <summary>Triangulacion por orejas de un poligono simple antihorario. Devuelve indices.</summary>
        public static List<int> Triangulate(IList<V2> poly)
        {
            var idx = new List<int>();
            for (int i = 0; i < poly.Count; i++) idx.Add(i);
            var res = new List<int>();
            int guard = 0;
            while (idx.Count > 3 && guard++ < 1000)
            {
                bool cut = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int ia = idx[(k + idx.Count - 1) % idx.Count], ib = idx[k], ic = idx[(k + 1) % idx.Count];
                    var a = poly[ia]; var bb = poly[ib]; var c = poly[ic];
                    float cr = (bb.x - a.x) * (c.y - a.y) - (bb.y - a.y) * (c.x - a.x);
                    if (cr <= 1e-9f) continue;
                    bool inside = false;
                    for (int m = 0; m < idx.Count && !inside; m++)
                    {
                        int im = idx[m];
                        if (im == ia || im == ib || im == ic) continue;
                        if (InTri(poly[im], a, bb, c)) inside = true;
                    }
                    if (inside) continue;
                    res.Add(ia); res.Add(ib); res.Add(ic);
                    idx.RemoveAt(k);
                    cut = true;
                    break;
                }
                if (!cut) break;
            }
            if (idx.Count == 3) { res.Add(idx[0]); res.Add(idx[1]); res.Add(idx[2]); }
            return res;
        }

        static bool InTri(V2 p, V2 a, V2 b, V2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < -1e-9f || d2 < -1e-9f || d3 < -1e-9f;
            bool pos = d1 > 1e-9f || d2 > 1e-9f || d3 > 1e-9f;
            return !(neg && pos);
        }
    }
}
