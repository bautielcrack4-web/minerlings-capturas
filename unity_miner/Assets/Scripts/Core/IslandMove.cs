using System;

namespace Mineros.Core
{
    /// <summary>
    /// Colocar y mover edificios donde quiera el jugador (como en Clash of Clans). Las parcelas siguen existiendo (son
    /// los "cupos" de construccion de cada anillo), pero su lugar ya no es fijo: un edificio se puede soltar en cualquier
    /// punto libre de la isla habilitada. Los caminos se vuelven a trazar solos hacia la nueva ubicacion.
    /// </summary>
    public sealed partial class Island
    {
        public const float PlaceGrid = 0.5f, EmptyR = 1.6f, PlaceGap = 0.5f;
        public event Action<Plot> PlotMoved;

        /// <summary>Huella (radio) de lo que va en la parcela: el edificio, o el lote vacio.</summary>
        public static float Footprint(int kind) { return kind >= 0 ? Math.Max(1.4f, Defs[kind].Radius) : EmptyR; }

        /// <summary>Huella de una parcela concreta: el Cuartel crece con su nivel (celdas del Complejo).</summary>
        public float FootprintOf(Plot p, int kind)
        {
            if (kind == (int)BKind.Barracks && p != null && p.Building == (int)BKind.Barracks) return BarracksRadius(Math.Max(1, p.Level));
            return Footprint(kind);
        }

        /// <summary>Radio para esquivar y separar edificios (el Cuartel segun su nivel).</summary>
        public float RadiusOf(Plot p)
        {
            if (p.Building == (int)BKind.Barracks) return BarracksRadius(Math.Max(1, p.Level));
            return Defs[p.Building].Radius;
        }

        /// <summary>
        /// El Cuartel no puede subir al nivel que ensancha su huella si no hay lugar alrededor (hay que moverlo).
        /// </summary>
        public bool BarracksCramped(Plot p)
        {
            if (p == null || p.Building != (int)BKind.Barracks) return false;
            float now = BarracksRadius(Math.Max(1, p.Level)), next = BarracksRadius(p.Level + 1);
            if (next <= now + 0.01f) return false;
            return !FitsRadius(p, next, p.X, p.Z);
        }

        bool FitsRadius(Plot p, float r, float x, float z)
        {
            float d = Len(x, z);
            if (d < PlazaR + r + 0.4f || d > Radius - r - 0.7f) return false;
            foreach (var q in Plots)
            {
                if (q == p || q.Ring > Expand || q.Building < 0) continue;
                float rq = FootprintOf(q, q.Building);
                if (Sq(x - q.X, z - q.Z) < (r + rq + PlaceGap) * (r + rq + PlaceGap)) return false;
            }
            foreach (var b in Blockers)
            {
                float rb = b[2] * 0.75f + r;
                if (Sq(x - b[0], z - b[1]) < rb * rb) return false;
            }
            return !DecorBlocked(x, z, r);
        }

        /// <summary>Se puede mover: no el Ayuntamiento (es la plaza) ni el muelle (va en la orilla), ni durante el tutorial.</summary>
        public bool Movable(Plot p)
        {
            if (p == null || p.Id == 0 || !TutDone) return false;
            if (p.Building == (int)BKind.Dock) return false;
            return p.Ring <= Expand;
        }

        public static float Snap(float v) { return (float)Math.Round(v / PlaceGrid) * PlaceGrid; }

        /// <summary>
        /// Lugar valido para la parcela `p` con el contenido `kind`: dentro de la isla habilitada (sin pisar la playa),
        /// fuera de la plaza, sin tocar otros edificios, arboles, el tablon, la Maravilla, decoraciones ni la veta gigante.
        /// </summary>
        public bool CanPlace(Plot p, int kind, float x, float z)
        {
            float r = FootprintOf(p, kind);
            float d = Len(x, z);
            if (d < PlazaR + r + 0.4f) return false;
            if (d > Radius - r - 0.7f) return false;
            foreach (var q in Plots)
            {
                if (q == p || q.Ring > Expand || q.Building < 0) continue;
                float rq = FootprintOf(q, q.Building);
                if (Sq(x - q.X, z - q.Z) < (r + rq + PlaceGap) * (r + rq + PlaceGap)) return false;
            }
            foreach (var b in Blockers)
            {
                float rb = b[2] * 0.75f + r;
                if (Sq(x - b[0], z - b[1]) < rb * rb) return false;
            }
            if (DecorBlocked(x, z, r)) return false;
            foreach (var o in OreList)
                if (!o.Dead && o.Giant && Sq(x - o.X, z - o.Z) < (r + 2.4f) * (r + 2.4f)) return false;
            return true;
        }

        /// <summary>
        /// Mueve la parcela (con o sin edificio) a (x, z) ya ajustado a la grilla. Los lotes vacios que queden debajo se
        /// corren al lugar que se libero (o al hueco libre mas cercano), y los caminos se trazan de nuevo.
        /// </summary>
        public bool MovePlot(Plot p, float x, float z)
        {
            if (p == null || p.Id == 0) return false;
            if (p.Building >= 0 && !Movable(p)) return false;
            x = Snap(x); z = Snap(z);
            if (!CanPlace(p, p.Building, x, z)) return false;
            float ox = p.X, oz = p.Z;
            p.X = x; p.Z = z;
            float r = FootprintOf(p, p.Building);
            foreach (var q in Plots)
            {
                if (q == p || q.Building >= 0) continue;
                if (Sq(q.X - x, q.Z - z) >= (r + EmptyR + PlaceGap) * (r + EmptyR + PlaceGap)) continue;
                if (q.Ring <= Expand && CanPlace(q, -1, ox, oz) && Sq(ox - x, oz - z) >= (r + EmptyR + PlaceGap) * (r + EmptyR + PlaceGap)) { q.X = ox; q.Z = oz; continue; }
                RelocateEmpty(q);
            }
            // las vetas comunes que quedaron debajo se rompen solas (sin premio): el lugar queda limpio
            foreach (var o in OreList)
                if (!o.Dead && !o.Giant && Sq(o.X - x, o.Z - z) < (r + 0.6f) * (r + 0.6f)) { o.Dead = true; o.Hp = 0; }
            p.Moved = true;
            BuildPaths();
            AddStat("moves", 1);
            PlotMoved?.Invoke(p);
            return true;
        }

        /// <summary>Busca un hueco libre para un lote vacio: espiral alrededor de su anillo, el mas cercano a donde estaba.</summary>
        void RelocateEmpty(Plot q)
        {
            float baseR = RingR[Math.Min(q.Ring, RingR.Length - 1)];
            float a0 = (float)Math.Atan2(q.Z, q.X);
            for (int step = 0; step < 60; step++)
            {
                float da = (step / 2 + 1) * 0.12f * (step % 2 == 0 ? 1f : -1f);
                for (int k = 0; k < 3; k++)
                {
                    float rr = baseR + (k == 0 ? 0f : k == 1 ? 1.2f : -1.2f);
                    float x = Snap((float)Math.Cos(a0 + da) * rr), z = Snap((float)Math.Sin(a0 + da) * rr);
                    if (q.Ring <= Expand && !CanPlace(q, -1, x, z)) continue;
                    if (q.Ring > Expand && !FarFromBuildings(x, z)) continue;
                    q.X = x; q.Z = z;
                    q.Moved = true;
                    return;
                }
            }
        }

        bool FarFromBuildings(float x, float z)
        {
            foreach (var b in Plots)
            {
                if (b.Building < 0) continue;
                float rb = FootprintOf(b, b.Building) + EmptyR + PlaceGap;
                if (Sq(x - b.X, z - b.Z) < rb * rb) return false;
            }
            return true;
        }

        /// <summary>Un lote vacio con un edificio encima no se ofrece (pasa si el anillo todavia no estaba habilitado).</summary>
        public bool Covered(Plot q)
        {
            if (q.Building >= 0) return false;
            foreach (var b in Plots)
            {
                if (b == q || b.Building < 0) continue;
                float rb = FootprintOf(b, b.Building) + EmptyR;
                if (Sq(q.X - b.X, q.Z - b.Z) < rb * rb) return true;
            }
            return false;
        }

        /// <summary>Al ampliar: los lotes nuevos que quedaron bajo un edificio movido se corren a un hueco.</summary>
        void FixCoveredPlots()
        {
            bool any = false;
            foreach (var q in Plots) if (q.Ring <= Expand && Covered(q)) { RelocateEmpty(q); any = true; }
            if (any) BuildPaths();
        }
    }
}
