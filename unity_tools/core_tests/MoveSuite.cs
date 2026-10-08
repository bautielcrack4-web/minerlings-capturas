using System;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Colocar y mover edificios donde quiera el jugador (estilo Clash of Clans).</summary>
    public static class MoveSuite
    {
        public static void Go()
        {
            var isl = new Island(90);
            isl.Tut = Island.TutStep.Done;
            var house = isl.Plots[1];
            T.Check("mover: el Ayuntamiento no se mueve", !isl.Movable(isl.Plots[0]) && !isl.MovePlot(isl.Plots[0], 3f, 3f));
            T.Check("mover: la casa si", isl.Movable(house));
            T.Check("mover: no dentro de la plaza", !isl.CanPlace(house, house.Building, 1f, 1f));
            T.Check("mover: no fuera de la isla", !isl.CanPlace(house, house.Building, isl.Radius, 0f));
            // un lugar libre en el anillo de adentro, lejos de la casa
            float bx = 0f, bz = 0f; bool found = false;
            for (int i = 0; i < 360 && !found; i += 5)
                for (float rr = 5f; rr < isl.Radius - 2f && !found; rr += 0.5f)
                {
                    float x = Island.Snap((float)Math.Cos(i * Math.PI / 180) * rr), z = Island.Snap((float)Math.Sin(i * Math.PI / 180) * rr);
                    if ((x - house.X) * (x - house.X) + (z - house.Z) * (z - house.Z) < 25f) continue;
                    if (isl.CanPlace(house, house.Building, x, z)) { bx = x; bz = z; found = true; }
                }
            T.Check("mover: hay lugar libre en la isla", found);
            float ox = house.X, oz = house.Z;
            bool moved = isl.MovePlot(house, bx, bz);
            T.Check("mover: la casa queda donde la soltaron (en la grilla)", moved && house.X == bx && house.Z == bz && house.Moved);
            bool pathOk = false;
            foreach (var l in isl.Paths) if (l.Plot == house.Id) { int n = l.X.Length - 1; pathOk = (l.X[n] - bx) * (l.X[n] - bx) + (l.Z[n] - bz) * (l.Z[n] - bz) < 4f; }
            T.Check("mover: el camino se traza hasta el lugar nuevo", pathOk);
            bool overlap = false;
            foreach (var q in isl.Plots)
                if (q != house && q.Building < 0 && q.Ring <= isl.Expand && isl.Offered(q) && isl.Covered(q)) overlap = true;
            T.Check("mover: ningun lote ofrecido queda debajo de un edificio", !overlap);
            // otro edificio no puede pisar a la casa
            var p2 = isl.Plots[2];
            p2.Building = (int)BKind.Sawmill; p2.Level = 1;
            T.Check("mover: no se puede encimar sobre otro edificio", !isl.CanPlace(p2, p2.Building, bx + 0.5f, bz));
            // guardado
            var h = new Island(1);
            T.Check("mover: el lugar elegido se guarda", h.LoadJson(isl.ToJson()) && h.Plots[1].X == bx && h.Plots[1].Z == bz && h.Plots[1].Moved);
            T.Check("mover: las parcelas que no se movieron quedan igual al cargar", h.Plots[5].X == isl.Plots[5].X && h.Plots[5].Z == isl.Plots[5].Z);
            T.Check("mover: volver al lugar original", isl.MovePlot(house, ox, oz) || isl.CanPlace(house, house.Building, ox, oz) == false);
        }
    }
}
