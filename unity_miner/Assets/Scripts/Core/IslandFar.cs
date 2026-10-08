using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    public enum RaftState { Docked, Boarding, ToFar, AtFar, ToBeach, Returning }

    /// <summary>
    /// Isla lejana (pedido del dueño, 8-oct): una isla en el mar, tapada por niebla, a la que se llega con la balsa del
    /// Muelle. Ahi hay rocas exclusivas enormes (oro, gema, roca antigua) con barra de vida: hacen falta cientos de
    /// toques y varias jornadas de la cuadrilla para romperlas, y pagan mucho.
    ///
    /// Jornada (reloj del dia, ver Hour):
    /// - 6:00: la balsa llama a la cuadrilla. Asientos = nivel del Muelle (nivel 1: balsa de 1; nivel 8: 8 mineros).
    ///   Suben los mas descansados que esten libres; se espera hasta 40 s a que lleguen.
    /// - Cruza (25 s), desembarcan y pican. La cuadrilla sigue la roca FIJADA por el jugador (la ultima que toco en la
    ///   isla lejana); si no hay, la que venia rompiendo cada uno; si no, la mas avanzada (la terminan entre todos).
    /// - 19:00: vuelven a la playa, suben y cruzan de vuelta. A la noche descansan en casa como siempre.
    /// Alla no gastan energia (llevan vianda) y pegan a 1/7: son rocas de varios dias. Las rocas y su vida se guardan.
    /// </summary>
    public sealed partial class Island
    {
        // ---------------------------------------------------------------- lugar
        /// <summary>Hacia "arriba" de la pantalla (la camara mira con giro de 35°): la isla lejana asoma en el horizonte.</summary>
        static readonly float FarDirX = (float)Math.Sin(35.0 * Math.PI / 180.0), FarDirZ = (float)Math.Cos(35.0 * Math.PI / 180.0);
        public const float FarDist = 42f, FarR = 7f;
        public static float FarX { get { return FarDirX * FarDist; } }
        public static float FarZ { get { return FarDirZ * FarDist; } }
        /// <summary>Playa de la isla lejana donde amarra la balsa.</summary>
        public static float FarBeachX { get { return FarX - FarDirX * (FarR - 0.6f); } }
        public static float FarBeachZ { get { return FarZ - FarDirZ * (FarR - 0.6f); } }

        public bool FarFound;
        public int FarFocus = -1;   // id de la roca fijada por el jugador
        public RaftState Raft = RaftState.Docked;
        public float RaftT;         // 0..1 del cruce (o reloj de espera al embarcar)
        public float RaftX, RaftZ, RaftFace;
        public readonly List<int> Crew = new List<int>();
        bool farLeftToday;          // la balsa ya salio esta manana (se rearma al mediodia)

        public event Action FarDiscovered;
        public event Action<RaftState> RaftChanged;
        public event Action<Ore, double, int> FarRockBroken;   // roca, monedas, gemas

        public const float RaftTrip = 25f, BoardWait = 40f;
        public const float FarCrewHit = 0.14f;   // la cuadrilla pega a 1/7 en estas rocas (son de varios dias)

        public int RaftSeats { get { return Math.Max(0, Math.Min(8, Level(BKind.Dock))); } }
        public bool HasDock { get { var d = Find(BKind.Dock); return d != null && d.Level >= 1; } }

        /// <summary>Punto de embarque en casa: la orilla al lado del Muelle (o la orilla mas cercana a la isla lejana).</summary>
        public void DockPoint(out float x, out float z)
        {
            var d = Find(BKind.Dock);
            float bx = d != null ? d.X : FarDirX * (Radius - 1f), bz = d != null ? d.Z : FarDirZ * (Radius - 1f);
            float l = (float)Math.Sqrt(bx * bx + bz * bz);
            if (l < 1e-3f) { bx = FarDirX; bz = FarDirZ; l = 1f; }
            float r = Radius - 0.7f;
            x = bx / l * r; z = bz / l * r;
        }

        /// <summary>Donde espera la balsa en casa: en el agua, frente a la orilla del Muelle.</summary>
        public void RaftHome(out float x, out float z)
        {
            float sx, sz; DockPoint(out sx, out sz);
            float l = (float)Math.Sqrt(sx * sx + sz * sz);
            x = sx / l * (Radius + 1.1f); z = sz / l * (Radius + 1.1f);
        }

        /// <summary>Hora del dia (0..24) con la que se lee el reloj: 6:00 = amanecer (fase 0.93), 19:00 = atardecer (0.665).</summary>
        public float Hour
        {
            get
            {
                float p = DayPhase;
                // de dia: 0.93 -> 1 -> 0.665 son 13 horas (6 a 19); de noche: 0.665 -> 0.93 son 11 horas (19 a 6)
                float dayLen = 1f - 0.93f + 0.665f;
                if (p >= 0.93f) return 6f + (p - 0.93f) / dayLen * 13f;
                if (p < 0.665f) return 6f + (p + 1f - 0.93f) / dayLen * 13f;
                float h = 19f + (p - 0.665f) / (0.93f - 0.665f) * 11f;
                return h >= 24f ? h - 24f : h;
            }
        }

        public static bool IsFarOre(Ore o) { return o != null && o.Far; }
        public bool OnFarIsland(float x, float z) { return Sq(x - FarX, z - FarZ) < (FarR + 1f) * (FarR + 1f); }

        public int FarRockCount { get { int n = 0; foreach (var o in OreList) if (o.Far && !o.Dead) n++; return n; } }

        /// <summary>El jugador toca la niebla: sin Muelle no se puede; con Muelle se descubre.</summary>
        public bool TryDiscoverFar()
        {
            if (FarFound) return true;
            if (!HasDock) return false;
            FarFound = true;
            AddStat("far_found", 1);
            FillFarRocks();
            RaftX = 0f; RaftZ = 0f;
            FarDiscovered?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- rocas exclusivas
        static readonly int[] FarKinds = { 3, 4, 2 };            // oro, gema, roca antigua (hierro)
        static readonly double[] FarTaps = { 800, 1000, 500 };   // toques del jugador que aguanta cada una (sin combo)

        void FillFarRocks()
        {
            int have = FarRockCount;
            for (int i = have; i < 3; i++) SpawnFarRock(i);
        }

        Ore SpawnFarRock(int slot)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                double a = (slot / 3.0 + rng.NextDouble() * 0.2) * Math.PI * 2, d = 2.2 + rng.NextDouble() * (FarR - 4.2);
                float x = FarX + (float)(Math.Cos(a) * d), z = FarZ + (float)(Math.Sin(a) * d);
                bool ok = Sq(x - FarBeachX, z - FarBeachZ) > 2.4f * 2.4f;
                foreach (var o in OreList) if (o.Far && !o.Dead && Sq(o.X - x, o.Z - z) < 3.2f * 3.2f) ok = false;
                if (!ok) continue;
                int pick = rng.Next(FarKinds.Length);
                double hp = FarTaps[pick] * Math.Max(1.0, PickPower() * 0.6);
                var r = new Ore { Id = nextOre++, Kind = FarKinds[pick], X = x, Z = z, MaxHp = hp, Hp = hp, Far = true, Age = 5f };
                OreList.Add(r);
                OreSpawned?.Invoke(r);
                return r;
            }
            return null;
        }

        /// <summary>Premio de una roca exclusiva: mucho oro (la de gema, gemas), cofre y experiencia.</summary>
        void FarBroke(Ore o, Miner m)
        {
            if (o.Dead) return;
            o.Dead = true;
            o.Hp = 0;
            AddStat("far_rocks", 1);
            double big = o.MaxHp / Math.Max(1.0, PickPower() * 0.6);   // 500..1000
            double coins = Math.Max(GiantValue() * big * 0.12, IncomePerSec() * 60.0 * 25.0 * big / 1000.0);   // ~25 min de ingreso (la de 1000)
            int gems = o.Kind == 4 ? 12 : o.Kind == 3 ? 4 : 2;
            Earn(coins);
            Gems += gems;
            GiveChest(o.Kind == 4 ? 2 : 1);
            AddRes(OreRes[o.Kind], Ores[o.Kind].Units * 25);
            AddXp(80);
            Mined[o.Kind]++;
            AddStat("rocks", 1);
            if (FarFocus == o.Id) FarFocus = -1;
            FarRockBroken?.Invoke(o, coins, gems);
            OreBroken?.Invoke(o, m);
            foreach (var c in Miners) if (c.Target == o.Id) c.Target = -1;
        }

        /// <summary>Toque del jugador sobre una roca exclusiva: la fija como objetivo de la cuadrilla y la golpea.</summary>
        double TapFar(Ore o, double dmg)
        {
            FarFocus = o.Id;
            o.Hp -= dmg;
            double v = GiantValue() * 0.03 * (LastCrit ? 3 : 1);
            Earn(v);
            if (o.Hp <= 0) { LastBroke = true; FarBroke(o, null); }
            return v;
        }

        // ---------------------------------------------------------------- balsa y cuadrilla
        void TickFar(float dt)
        {
            if (!FarFound) return;
            float hour = Hour;
            if (hour >= 12f && Raft == RaftState.Docked) farLeftToday = false;
            switch (Raft)
            {
                case RaftState.Docked:
                {
                    RaftHome(out RaftX, out RaftZ);
                    RaftFace = (float)Math.Atan2(FarX - RaftX, FarZ - RaftZ);
                    // sale a las 6 (una vez por dia), si hay asientos
                    if (hour >= 6f && hour < 6.6f && !farLeftToday && RaftSeats > 0)
                    {
                        farLeftToday = true;
                        FillFarRocks();
                        CallCrew();
                        if (Crew.Count > 0) SetRaft(RaftState.Boarding);
                    }
                    break;
                }
                case RaftState.Boarding:
                {
                    RaftT += dt;
                    bool all = true;
                    foreach (var m in CrewMiners()) if (m.State != MState.OnRaft) all = false;
                    if (all || RaftT > BoardWait)
                    {
                        // los que no llegaron se quedan en casa
                        foreach (var m in CrewMiners()) if (m.State != MState.OnRaft) { m.Target = -1; Set(m, MState.Idle); }
                        Crew.RemoveAll(id => { var mm = MinerById(id); return mm == null || mm.State != MState.OnRaft; });
                        if (Crew.Count == 0) { SetRaft(RaftState.Docked); break; }
                        SetRaft(RaftState.ToFar);
                    }
                    break;
                }
                case RaftState.ToFar:
                case RaftState.Returning:
                {
                    RaftT = Math.Min(1f, RaftT + dt / RaftTrip);
                    float hx, hz; RaftHome(out hx, out hz);
                    float u = RaftT * RaftT * (3f - 2f * RaftT);
                    bool going = Raft == RaftState.ToFar;
                    float ax = going ? hx : FarBeachX, az = going ? hz : FarBeachZ, bx = going ? FarBeachX : hx, bz = going ? FarBeachZ : hz;
                    // arco suave (no una linea recta): se ve navegar
                    float px = -(bz - az), pz = bx - ax, pl = (float)Math.Sqrt(px * px + pz * pz);
                    float bow = (float)Math.Sin(u * Math.PI) * 3.5f / Math.Max(pl, 1e-3f);
                    RaftX = ax + (bx - ax) * u + px * bow;
                    RaftZ = az + (bz - az) * u + pz * bow;
                    RaftFace = (float)Math.Atan2(bx - ax, bz - az);
                    if (RaftT >= 1f)
                    {
                        if (going)
                        {
                            SetRaft(RaftState.AtFar);
                            foreach (var m in CrewMiners()) { Set(m, MState.FarWork); m.X = FarBeachX + (m.Id % 3 - 1) * 0.6f; m.Z = FarBeachZ + (m.Id % 2) * 0.5f; }
                        }
                        else
                        {
                            SetRaft(RaftState.Docked);
                            float sx, sz; DockPoint(out sx, out sz);
                            foreach (var m in CrewMiners()) { m.Target = -1; Set(m, MState.Idle); m.X = sx * 0.97f + (m.Id % 3 - 1) * 0.4f; m.Z = sz * 0.97f; }
                            AddStat("raft_trips", 1);
                            Crew.Clear();
                        }
                    }
                    break;
                }
                case RaftState.AtFar:
                    RaftX = FarBeachX; RaftZ = FarBeachZ;
                    if (hour >= 19f || hour < 6f || FarRockCount == 0)
                    {
                        SetRaft(RaftState.ToBeach);
                        foreach (var m in CrewMiners()) { m.Target = -1; Set(m, MState.ToRaft); }
                    }
                    break;
                case RaftState.ToBeach:
                {
                    RaftT += dt;
                    bool all = true;
                    foreach (var m in CrewMiners()) if (m.State != MState.OnRaft) all = false;
                    if (all || RaftT > BoardWait)
                    {
                        // nadie queda varado: el que no llego sube igual (se lo ve saltar a la balsa)
                        foreach (var m in CrewMiners()) Set(m, MState.OnRaft);
                        SetRaft(RaftState.Returning);
                    }
                    break;
                }
            }
            // sentados en la balsa: van con ella
            if (Raft != RaftState.AtFar)
            {
                int i = 0;
                foreach (var m in CrewMiners())
                {
                    if (m.State != MState.OnRaft) continue;
                    float sx, sz; RaftSeat(i++, out sx, out sz);
                    m.X = sx; m.Z = sz; m.Face = RaftFace; m.Moving = false; m.Y = -0.2f;   // sentado en la balsa, sobre el agua
                }
            }
        }

        void SetRaft(RaftState s)
        {
            Raft = s; RaftT = 0f;
            if (s == RaftState.AtFar || s == RaftState.Docked) foreach (var m in CrewMiners()) m.Y = 0f;   // en tierra
            RaftChanged?.Invoke(s);
        }

        /// <summary>Asiento `i` de la balsa (de a dos, en fila hacia atras).</summary>
        public void RaftSeat(int i, out float x, out float z)
        {
            float fx = (float)Math.Sin(RaftFace), fz = (float)Math.Cos(RaftFace);
            float rx = fz, rz = -fx;
            float along = 0.5f - (i / 2) * 0.62f, side = (i % 2 == 0 ? -0.32f : 0.32f);
            if (RaftSeats <= 1) side = 0f;
            x = RaftX + fx * along + rx * side;
            z = RaftZ + fz * along + rz * side;
        }

        IEnumerable<Miner> CrewMiners()
        {
            foreach (var id in Crew) { var m = MinerById(id); if (m != null) yield return m; }
        }

        Miner MinerById(int id) { foreach (var m in Miners) if (m.Id == id) return m; return null; }

        public bool InCrew(Miner m) { return Crew.Contains(m.Id); }

        /// <summary>Los mas descansados que esten libres (no en el Cuartel, no en obra, no cargando) suben a la balsa.</summary>
        void CallCrew()
        {
            Crew.Clear();
            var free = new List<Miner>();
            foreach (var m in Miners)
            {
                if (m.InComplex || m.CarryKind >= 0) continue;
                if (m.State != MState.Idle && m.State != MState.ToOre && m.State != MState.Mining && m.State != MState.Resting) continue;
                free.Add(m);
            }
            free.Sort((a, b) => b.Energy.CompareTo(a.Energy));
            for (int i = 0; i < free.Count && Crew.Count < RaftSeats; i++)
            {
                var m = free[i];
                Release(m);
                Crew.Add(m.Id);
                Set(m, MState.ToRaft);
            }
        }

        /// <summary>Estados de la expedicion (los llama TickMiner).</summary>
        void TickFarMiner(Miner m, float dt, float perf)
        {
            switch (m.State)
            {
                case MState.ToRaft:
                {
                    bool atFar = Raft == RaftState.ToBeach;
                    float tx = FarBeachX, tz = FarBeachZ;
                    if (!atFar) DockPoint(out tx, out tz);   // en casa se sube desde la orilla
                    bool arrived = atFar ? FarWalk(m, tx, tz, 0.6f, dt, perf * 1.2f) : Walk(m, tx, tz, 0.9f, dt, perf * WalkMult(m) * 1.3f);
                    if (arrived) Set(m, MState.OnRaft);
                    break;
                }
                case MState.OnRaft:
                    break;   // la balsa lo lleva (TickFar)
                case MState.FarWork:
                {
                    var o = FarTarget(m);
                    if (o == null) { m.Moving = false; break; }
                    float reach = 1.5f + (m.Id % 3) * 0.3f;
                    if (!FarWalk(m, o.X, o.Z, reach, dt, perf)) break;
                    m.Face = (float)Math.Atan2(o.X - m.X, o.Z - m.Z);
                    m.HitT += dt * perf;
                    if (m.HitT >= HitInterval)
                    {
                        m.HitT -= HitInterval;
                        m.Hits++;
                        o.Hp -= PickPower() * HitMult(m) * FarCrewHit;
                        OreHit?.Invoke(o, m);
                        if (o.Hp <= 0) { FarBroke(o, m); MinerBroke(m, o); MinerMood?.Invoke(m, "cheer"); }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// A que roca va: la fijada por el jugador; si no, la que venia rompiendo; si no, la mas avanzada (la terminan
        /// entre todos en vez de repartirse).
        /// </summary>
        Ore FarTarget(Miner m)
        {
            Ore f = FarFocus >= 0 ? OreById(FarFocus) : null;
            if (f != null && !f.Dead) { m.Target = f.Id; return f; }
            Ore cur = m.Target >= 0 ? OreById(m.Target) : null;
            if (cur != null && cur.Far && !cur.Dead) return cur;
            Ore best = null; double bestF = 2.0;
            foreach (var o in OreList)
            {
                if (!o.Far || o.Dead) continue;
                double fr = o.Hp / Math.Max(1.0, o.MaxHp);
                if (fr < bestF) { bestF = fr; best = o; }
            }
            if (best != null) m.Target = best.Id;
            return best;
        }

        /// <summary>Caminar en la isla lejana (sin edificios que esquivar; no salir de su costa).</summary>
        bool FarWalk(Miner m, float x, float z, float stop, float dt, float perf)
        {
            float dx = x - m.X, dz = z - m.Z;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d <= stop) { m.Moving = false; return true; }
            float step = Math.Min(d, WalkSpeed * perf * dt);
            m.X += dx / d * step; m.Z += dz / d * step;
            float ox = m.X - FarX, oz = m.Z - FarZ, r = (float)Math.Sqrt(ox * ox + oz * oz), lim = FarR - 0.5f;
            if (r > lim) { m.X = FarX + ox / r * lim; m.Z = FarZ + oz / r * lim; }
            m.Face = (float)Math.Atan2(dx, dz);
            m.Moving = true;
            return false;
        }

        public static bool FarState(MState s) { return s == MState.ToRaft || s == MState.OnRaft || s == MState.FarWork; }

        // ---------------------------------------------------------------- guardado
        object FarObj()
        {
            var rocks = new List<object>();
            foreach (var o in OreList)
                if (o.Far && !o.Dead) rocks.Add(new Dictionary<string, object> { { "k", o.Kind }, { "x", o.X }, { "z", o.Z }, { "hp", o.Hp }, { "max", o.MaxHp } });
            return new Dictionary<string, object> { { "found", FarFound ? 1 : 0 }, { "rocks", rocks } };
        }

        void ReadFar(Dictionary<string, object> d)
        {
            object fo;
            if (!d.TryGetValue("far", out fo) || !(fo is Dictionary<string, object> f)) return;
            FarFound = JsonRead.Int(f, "found", 0) == 1;
            object ro;
            if (f.TryGetValue("rocks", out ro) && ro is List<object> rl)
                foreach (var x in rl)
                {
                    var r = x as Dictionary<string, object>;
                    if (r == null) continue;
                    var o = new Ore
                    {
                        Id = nextOre++, Kind = JsonRead.Int(r, "k", 3), X = (float)JsonRead.Dbl(r, "x", FarX), Z = (float)JsonRead.Dbl(r, "z", FarZ),
                        Hp = JsonRead.Dbl(r, "hp", 100), MaxHp = JsonRead.Dbl(r, "max", 100), Far = true, Age = 5f,
                    };
                    OreList.Add(o);
                }
            // la balsa siempre arranca amarrada (la cuadrilla, en casa)
            Raft = RaftState.Docked;
            Crew.Clear();
        }
    }
}
