using System;
using System.Collections.Generic;

namespace Mineros.Art.Gen
{
    /// <summary>Cristales (prisma hexagonal con punta) y gemas talladas. Todo con normales PLANAS por cara para que el
    /// especular las haga brillar facetadas. Solidos convexos: la orientacion de cada cara se resuelve respecto del centro.</summary>
    public static class Gems
    {
        const float Pi = (float)Math.PI;

        /// <summary>Cristal con la base en y = 0 y la punta en y = h. r = radio del prisma. seed da irregularidad
        /// (vertices, hombro, desvio de la punta). 6 caras laterales + 6 de punta + tapa base.</summary>
        public static void Crystal(MeshBuilder b, int slot, Xf xf, int seed, float h, float r, bool cap = true)
        {
            var rng = new Rng(Rng.Hash("crystal", seed, 3));
            var ring0 = new V3[6];
            var ring1 = new V3[6];
            float shoulder = h * rng.Range(0.62f, 0.74f);
            float tw = rng.Range(-9f, 9f) * Pi / 180f;
            float taper = rng.Range(0.86f, 0.97f);
            for (int k = 0; k < 6; k++)
            {
                float a = 2f * Pi * k / 6f + rng.Range(-0.08f, 0.08f);
                float r0 = r * rng.Range(0.9f, 1.1f);
                ring0[k] = new V3(-(float)Math.Sin(a) * r0, 0f, (float)Math.Cos(a) * r0);
                float a1 = a + tw;
                float r1 = r0 * taper;
                ring1[k] = new V3(-(float)Math.Sin(a1) * r1, shoulder, (float)Math.Cos(a1) * r1);
            }
            var apex = new V3(rng.Range(-0.18f, 0.18f) * r, h, rng.Range(-0.18f, 0.18f) * r);
            var center = new V3(0, h * 0.4f, 0);
            b.Push(xf);
            for (int k = 0; k < 6; k++)
            {
                int n = (k + 1) % 6;
                b.FlatQuadOut(slot, ring0[k], ring0[n], ring1[n], ring1[k], center);
                b.FlatTriOut(slot, ring1[k], ring1[n], apex, center);
            }
            if (cap)
            {
                var bc = new V3(0, 0, 0);
                for (int k = 0; k < 6; k++) b.FlatTriOut(slot, ring0[k], ring0[(k + 1) % 6], bc, center);
            }
            b.Pop();
        }

        /// <summary>Gema tallada simplificada (brillante): mesa octogonal, facetas estrella y biseles en la corona, cintura y
        /// pabellon hasta la punta. Centrada en el origen, radio de cintura r. Culet abajo (-Y).</summary>
        public static void Gem(MeshBuilder b, int slot, Xf xf, float r = 0.5f)
        {
            float table = r * 0.56f, crownH = r * 0.36f, girdleT = r * 0.045f, pavH = r * 0.86f;
            float yOff = (pavH - crownH) * 0.5f; // centra la caja envolvente
            var T = new V3[8];
            var G = new V3[16];
            var Gb = new V3[16];
            for (int k = 0; k < 8; k++)
            {
                float a = 2f * Pi * k / 8f;
                T[k] = new V3(-(float)Math.Sin(a) * table, crownH + yOff, (float)Math.Cos(a) * table);
            }
            for (int k = 0; k < 16; k++)
            {
                float a = 2f * Pi * k / 16f;
                float rr = (k % 2 == 0) ? r : r * 0.955f;
                G[k] = new V3(-(float)Math.Sin(a) * rr, yOff, (float)Math.Cos(a) * rr);
                Gb[k] = new V3(G[k].x, yOff - girdleT, G[k].z);
            }
            var culet = new V3(0, -pavH + yOff, 0);
            var c = V3.Zero;
            b.Push(xf);
            // mesa
            for (int k = 1; k < 7; k++) b.FlatTriOut(slot, T[0], T[k], T[k + 1], c);
            for (int i = 0; i < 8; i++)
            {
                int n = (i + 1) % 8;
                // faceta estrella (triangulo entre dos vertices de la mesa y el vertice impar de la cintura)
                b.FlatTriOut(slot, T[i], T[n], G[2 * i + 1], c);
                // bisel (cometa) partido en dos triangulos
                int gm = (2 * i + 15) % 16, g0 = 2 * i, gp = 2 * i + 1;
                b.FlatTriOut(slot, T[i], G[gm], G[g0], c);
                b.FlatTriOut(slot, T[i], G[g0], G[gp], c);
            }
            // cintura y pabellon
            for (int k = 0; k < 16; k++)
            {
                int n = (k + 1) % 16;
                b.FlatQuadOut(slot, G[k], G[n], Gb[n], Gb[k], c);
                b.FlatTriOut(slot, Gb[k], Gb[n], culet, c);
            }
            b.Pop();
        }
    }
}
