using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Camino de tierra predeterminado que une una parcela con la red (curva suave, muestreada).</summary>
    public sealed class PathLine
    {
        public int Plot, From;      // parcela destino y parcela de donde sale (0 = plaza del deposito)
        public float[] X, Z;        // puntos de la curva
    }

    /// <summary>
    /// Trazado prolijo de la isla: una plaza alrededor del deposito y caminos curvos fijos hacia cada parcela (cada
    /// una se engancha a la mas cercana que este mas adentro). Las vetas y la decoracion no se ponen sobre los caminos.
    /// </summary>
    public sealed partial class Island
    {
        public const float PlazaR = 3.2f, PathHalf = 0.62f;
        public readonly List<PathLine> Paths = new List<PathLine>();

        void BuildPaths()
        {
            Paths.Clear();
            foreach (var p in Plots)
            {
                if (p.Id == 0) continue;
                float dp = Len(p.X, p.Z);
                Plot best = Plots[0]; float bestCost = dp;
                foreach (var q in Plots)
                {
                    if (q == p || q.Id == 0 || q.Ring > p.Ring) continue;
                    float dq = Len(q.X, q.Z);
                    if (dq > dp - 1.5f) continue;
                    float cost = Len(p.X - q.X, p.Z - q.Z) + 0.35f * dq;
                    if (cost < bestCost) { bestCost = cost; best = q; }
                }
                float dx = p.X - best.X, dz = p.Z - best.Z, len = Len(dx, dz);
                float ux = dx / len, uz = dz / len;
                float startOff = best.Id == 0 ? PlazaR - 0.3f : 1.6f;
                float ax = best.X + ux * startOff, az = best.Z + uz * startOff;
                float bx = p.X - ux * 1.5f, bz = p.Z - uz * 1.5f;
                // curva suave: punto de control corrido hacia un costado
                float side = (p.Id % 2 == 0 ? 1f : -1f) * 0.16f * len;
                float cx = (ax + bx) * 0.5f - uz * side, cz = (az + bz) * 0.5f + ux * side;
                const int N = 18;
                var line = new PathLine { Plot = p.Id, From = best.Id, X = new float[N], Z = new float[N] };
                for (int i = 0; i < N; i++)
                {
                    float t = i / (float)(N - 1), m = 1f - t;
                    line.X[i] = m * m * ax + 2f * m * t * cx + t * t * bx;
                    line.Z[i] = m * m * az + 2f * m * t * cz + t * t * bz;
                }
                Paths.Add(line);
            }
        }

        static float Len(float x, float z) { return (float)Math.Sqrt(x * x + z * z); }

        /// <summary>True si (x, z) cae sobre la plaza o un camino habilitado (con margen extra).</summary>
        public bool OnPath(float x, float z, float margin)
        {
            float pr = PlazaR + margin;
            if (x * x + z * z < pr * pr) return true;
            float r = PathHalf + margin, r2 = r * r;
            foreach (var l in Paths)
            {
                if (Plots[l.Plot].Ring > Expand) continue;
                for (int i = 0; i < l.X.Length - 1; i++)
                    if (SegDist2(x, z, l.X[i], l.Z[i], l.X[i + 1], l.Z[i + 1]) < r2) return true;
            }
            return false;
        }

        static float SegDist2(float px, float pz, float ax, float az, float bx, float bz)
        {
            float vx = bx - ax, vz = bz - az, wx = px - ax, wz = pz - az;
            float c = vx * vx + vz * vz;
            float t = c > 1e-6f ? Math.Max(0f, Math.Min(1f, (wx * vx + wz * vz) / c)) : 0f;
            float dx = ax + vx * t - px, dz = az + vz * t - pz;
            return dx * dx + dz * dz;
        }
    }
}
