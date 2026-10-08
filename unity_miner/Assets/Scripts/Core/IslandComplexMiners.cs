using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>
    /// Circulacion real (idea 3): los mineros entran al Complejo por una puerta exterior, recorren los modulos con acceso
    /// (puertas y escaleras) y llegan a la Descarga, a su dormitorio o a la maquina que tienen que arreglar; despues
    /// salen por la puerta mas cercana. Si algo cambia en el camino, salen y vuelven a decidir (nunca quedan atrapados).
    /// </summary>
    public sealed partial class Island
    {
        public static bool InComplexState(MState s)
        {
            return s == MState.ToUnload || s == MState.ToDorm || s == MState.InDorm || s == MState.ToFix || s == MState.Fixing || s == MState.Leaving
                || s == MState.ToRoom || s == MState.InRoom;
        }

        public int ObservatoryCrystals() { return (FirstMod(ModKind.Observatory) != null ? 1 : 0) + (Summary.Combos[5] ? 1 : 0); }

        void GoDeposit(Miner m)
        {
            var u = UnloadFor(m);
            if (u != null)
            {
                m.Target = u.Id;
                m.Path = PathInto(m, u);
                m.PathI = 0;
                Set(m, MState.ToUnload);
                return;
            }
            var dep = Find(BKind.Depot);
            GoTo(m, MState.ToDepot, dep.X, dep.Z, dep.Id);
        }

        /// <summary>Cansado: a descansar a su dormitorio (los de Piedra, a la Sala central). false si no tiene.</summary>
        bool GoRest(Miner m)
        {
            if (BarracksLevel < 1) return false;
            var home = m.Char > 0 ? DormOf(m.Char) : FirstMod(ModKind.Central);
            if (home == null || home.Work > 0) return false;
            m.Target = home.Id;
            m.Path = PathInto(m, home);
            m.PathI = 0;
            Set(m, MState.ToDorm);
            return true;
        }

        // ------------------------------------------------------------ habitaciones con cola (Plan Pueblo)
        /// <summary>Cuantos caben a la vez: Duchas 1, Comedor 2.</summary>
        public static int RoomCap(ModKind k) { return k == ModKind.Bath ? 1 : 2; }

        /// <summary>Cuantos estan usando (o entrando a) esa habitacion.</summary>
        public int RoomUsers(Module r)
        {
            int n = 0;
            foreach (var x in Miners) if (x.Target == r.Id && (x.State == MState.ToRoom || x.State == MState.InRoom)) n++;
            return n;
        }

        /// <summary>Lugar `i` de la cola: en fila hacia afuera desde la puerta exterior de la habitacion.</summary>
        void QueueSpot(Module r, int i, out float x, out float z)
        {
            int side = DoorSideOf(r);
            float dx, dz; DoorPoint(r, side, out dx, out dz);
            float ox, oz; CellOffset(SideDX[side], SideDZ[side], out ox, out oz);
            float len = (float)Math.Sqrt(ox * ox + oz * oz); if (len < 1e-3f) len = 1f;
            x = dx + ox / len * (0.9f + 0.6f * i); z = dz + oz / len * (0.9f + 0.6f * i);
        }

        int DoorSideOf(Module r)
        {
            for (int side = 0; side < 4; side++) if (ModAt(r.X + SideDX[side], r.Z + SideDZ[side], r.F) == null) return side;
            return 2;
        }

        /// <summary>Va a comer/ducharse a la habitacion `k` (la menos ocupada); si estan llenas, hace cola afuera.</summary>
        bool GoRoom(Miner m, ModKind k)
        {
            if (BarracksLevel < 1) return false;
            Module best = null; int bestU = int.MaxValue;
            foreach (var r in Modules)
            {
                if (r.Kind != k || r.Work > 0) continue;
                int u = RoomUsers(r);
                if (u < bestU) { bestU = u; best = r; }
            }
            if (best == null) return false;
            m.Target = best.Id;
            m.QueueT = 0f;
            if (bestU < RoomCap(k)) { m.Path = PathInto(m, best); m.PathI = 0; Set(m, MState.ToRoom); }
            else Set(m, MState.Queued);
            return true;
        }

        /// <summary>En la cola: se para en su lugar (orden de llegada) y entra apenas se libera.</summary>
        void TickQueued(Miner m, float dt, float perf)
        {
            var r = ModById(m.Target);
            if (r == null) { Set(m, MState.Idle); return; }
            m.QueueT += dt;
            if (RoomUsers(r) < RoomCap(r.Kind))
            {
                Miner first = null;
                foreach (var x in Miners) if (x.State == MState.Queued && x.Target == r.Id && (first == null || x.QueueT > first.QueueT)) first = x;
                if (first == m) { m.Path = PathInto(m, r); m.PathI = 0; Set(m, MState.ToRoom); return; }
            }
            int pos = 0;
            foreach (var x in Miners) if (x != m && x.State == MState.Queued && x.Target == r.Id && x.QueueT > m.QueueT) pos++;
            float qx, qz; QueueSpot(r, pos, out qx, out qz);
            if (Walk(m, qx, qz, 0.15f, dt, perf * WalkMult(m))) { float cx, cz; Center(r, out cx, out cz); m.Face = (float)Math.Atan2(cx - m.X, cz - m.Z); }
            if (m.QueueT > 90f) Set(m, MState.Idle);   // mucho tiempo en la fila: se va y vuelve a decidir
        }

        void TickComplexMiner(Miner m, float dt, float perf)
        {
            var mod = ModById(m.Target);
            if (mod == null && m.State != MState.Leaving) { LeaveNow(m); return; }
            switch (m.State)
            {
                case MState.ToUnload:
                    if (FollowPath(m, dt, perf * 0.9f * WalkMult(m)))
                    {
                        if (m.CarryKind >= 0) UnloadDeposit(m, mod);
                        StartLeaving(m, mod);
                    }
                    break;
                case MState.ToDorm:
                    if (FollowPath(m, dt, perf * WalkMult(m))) Set(m, MState.InDorm);
                    break;
                case MState.InDorm:
                {
                    bool mess = false;
                    foreach (var n in Neighbors(mod)) if (n.Kind == ModKind.Mess) mess = true;
                    m.Energy = Math.Min(100f, m.Energy + 12f * dt * (mess ? 1.5f : 1f) * (float)StageK(mod));
                    if (m.Energy >= 100f) { MinerMood?.Invoke(m, "fed"); StartLeaving(m, mod); }
                    break;
                }
                case MState.ToRoom:
                    if (FollowPath(m, dt, perf * WalkMult(m))) Set(m, MState.InRoom);
                    break;
                case MState.InRoom:
                    if (mod.Kind == ModKind.Bath)
                    {
                        m.Clean = Math.Min(100f, m.Clean + 45f * dt);
                        if (m.Clean >= 100f) { m.Fresh = (float)(45.0 * StageK(mod)); MinerMood?.Invoke(m, "fresh"); StartLeaving(m, mod); }
                    }
                    else
                    {
                        m.Energy = Math.Min(100f, m.Energy + (float)(30.0 * StageK(mod)) * dt);
                        if (m.Energy >= 100f) { MinerMood?.Invoke(m, "fed"); StartLeaving(m, mod); }
                    }
                    break;
                case MState.ToFix:
                    if (FollowPath(m, dt, perf * WalkMult(m) * 1.2f)) Set(m, MState.Fixing);
                    break;
                case MState.Fixing:
                    m.HitT += dt;
                    if (m.T >= 20f) { StartLeaving(m, mod); FixArrived(m); }
                    break;
                case MState.Leaving:
                    if (FollowPath(m, dt, perf * WalkMult(m))) { m.InComplex = false; m.Y = 0f; m.Path = null; Set(m, MState.Idle); }
                    break;
            }
        }

        void LeaveNow(Miner m)
        {
            m.Path = null; m.InComplex = false; m.Y = 0f;
            Set(m, MState.Idle);
        }

        void StartLeaving(Miner m, Module from)
        {
            m.Path = from != null ? PathOut(from) : null;
            m.PathI = 0;
            m.Target = from != null ? from.Id : -1;
            Set(m, MState.Leaving);
        }

        /// <summary>Sigue los puntos del camino. El primero (afuera) esquivando edificios; adentro, derecho.</summary>
        bool FollowPath(Miner m, float dt, float perf)
        {
            var p = m.Path;
            if (p == null || m.PathI * 3 >= p.Count) return true;
            float x = p[m.PathI * 3], z = p[m.PathI * 3 + 1], f = p[m.PathI * 3 + 2];
            bool arrived;
            if (m.PathI == 0 && !m.InComplex) arrived = Walk(m, x, z, 0.2f, dt, perf);
            else
            {
                m.InComplex = true;
                float dx = x - m.X, dz = z - m.Z;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                float step = WalkSpeed * 0.8f * perf * dt;
                if (d <= Math.Max(0.12f, step)) { m.X = x; m.Z = z; arrived = true; }
                else { m.X += dx / d * step; m.Z += dz / d * step; m.Face = (float)Math.Atan2(dx, dz); m.Moving = true; arrived = false; }
            }
            float wantY = f * FloorH;
            m.Y += Math.Max(-dt * 2.5f, Math.Min(dt * 2.5f, wantY - m.Y));
            if (!arrived) return false;
            m.PathI++;
            return m.PathI * 3 >= p.Count;
        }

        // ------------------------------------------------------------ caminos
        static void Pt(List<float> l, float x, float z, int f) { l.Add(x); l.Add(z); l.Add(f); }

        void Center(Module m, out float x, out float z) { ModWorld(m, out x, out z); }

        /// <summary>Punto afuera de la puerta exterior de un modulo.</summary>
        void DoorPoint(Module m, int side, out float x, out float z)
        {
            float cx, cz; Center(m, out cx, out cz);
            float ox, oz; CellOffset(SideDX[side], SideDZ[side], out ox, out oz);
            x = cx + ox * 0.62f; z = cz + oz * 0.62f;
        }

        /// <summary>Camino desde donde esta el minero hasta adentro de `target`.</summary>
        public List<float> PathInto(Miner who, Module target)
        {
            var l = new List<float>();
            var s = Summary;
            if (!s.Access.Contains(target.Id) || target.F > 0 && !s.Access.Contains(target.Id))
            {
                // sin acceso: entra directo por su lado exterior (nunca bloquea)
                int side = OuterDoor(target);
                if (side < 0) side = 2;
                float dx, dz; DoorPoint(target, side, out dx, out dz);
                Pt(l, dx, dz, 0);
                float tx, tz; Center(target, out tx, out tz);
                Pt(l, tx, tz, target.F);
                return l;
            }
            // entrada: el modulo de borde mas cercano al minero
            Module entry = null; int entrySide = -1; float bd = float.MaxValue;
            foreach (var b in Boundary())
            {
                int side = OuterDoor(b);
                float dx, dz; DoorPoint(b, side, out dx, out dz);
                float d = Sq(dx - who.X, dz - who.Z) + RouteLen(b, target) * Cell * Cell;
                if (d < bd) { bd = d; entry = b; entrySide = side; }
            }
            if (entry == null) { float tx, tz; Center(target, out tx, out tz); Pt(l, tx, tz, target.F); return l; }
            float ex, ez; DoorPoint(entry, entrySide, out ex, out ez);
            Pt(l, ex, ez, 0);
            foreach (var step in Route(entry, target)) { float x, z; Center(step, out x, out z); Pt(l, x, z, step.F); }
            return l;
        }

        /// <summary>Camino desde adentro de `from` hasta afuera, por la puerta exterior mas cercana.</summary>
        public List<float> PathOut(Module from)
        {
            var l = new List<float>();
            Module exit = null; int best = int.MaxValue;
            foreach (var b in Boundary()) { int r = RouteLen(from, b); if (r < best) { best = r; exit = b; } }
            if (exit == null || best == int.MaxValue)
            {
                int side = OuterDoor(from); if (side < 0) side = 2;
                float dx, dz; DoorPoint(from, side, out dx, out dz);
                Pt(l, dx, dz, 0);
                return l;
            }
            var route = Route(from, exit);
            foreach (var step in route) { float x, z; Center(step, out x, out z); Pt(l, x, z, step.F); }
            float ex, ez; DoorPoint(exit, OuterDoor(exit), out ex, out ez);
            Pt(l, ex, ez, 0);
            return l;
        }

        /// <summary>Ruta por puertas y escaleras (incluye el destino, no el origen). Vacia si no hay.</summary>
        public List<Module> Route(Module from, Module to)
        {
            var res = new List<Module>();
            if (from == to) { res.Add(to); return res; }
            var prev = new Dictionary<int, Module>();
            var q = new Queue<Module>();
            q.Enqueue(from); prev[from.Id] = null;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == to) break;
                foreach (var n in DoorNeighbors(c))
                    if (!prev.ContainsKey(n.Id)) { prev[n.Id] = c; q.Enqueue(n); }
            }
            if (!prev.ContainsKey(to.Id)) { res.Add(to); return res; }
            for (var c = to; c != null && c != from; c = prev[c.Id]) res.Add(c);
            res.Reverse();
            return res;
        }

        int RouteLen(Module from, Module to)
        {
            if (from == to) return 0;
            var dist = new Dictionary<int, int> { { from.Id, 0 } };
            var q = new Queue<Module>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var n in DoorNeighbors(c))
                {
                    if (dist.ContainsKey(n.Id)) continue;
                    dist[n.Id] = dist[c.Id] + 1;
                    if (n == to) return dist[n.Id];
                    q.Enqueue(n);
                }
            }
            return int.MaxValue / 4;
        }

        IEnumerable<Module> DoorNeighbors(Module c)
        {
            int dc = RotMask(MDef(c.Kind).Doors, c.Rot);
            for (int side = 0; side < 4; side++)
            {
                if ((dc & (1 << side)) == 0) continue;
                var n = ModAt(c.X + SideDX[side], c.Z + SideDZ[side], c.F);
                if (n == null) continue;
                if ((RotMask(MDef(n.Kind).Doors, n.Rot) & (1 << ((side + 2) & 3))) == 0) continue;
                yield return n;
            }
            bool stairs = c.Kind == ModKind.Central || c.Kind == ModKind.Stairs;
            var up = ModAt(c.X, c.Z, c.F + 1);
            if (up != null && (stairs || up.Kind == ModKind.Stairs)) yield return up;
            if (c.F > 0)
            {
                var dn = ModAt(c.X, c.Z, c.F - 1);
                if (dn != null && (dn.Kind == ModKind.Central || dn.Kind == ModKind.Stairs || c.Kind == ModKind.Stairs)) yield return dn;
            }
        }
    }
}
