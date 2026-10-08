using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    public enum Trait { None, Strong, Fast, Lucky, Glutton, Sleepy, Boss }

    /// <summary>
    /// Minero especialista (0.10): cada uno pica mejor UN mineral (Spec = tipo de veta; -1 = cualquiera, el Maestro).
    /// Rareza (0 comun .. 3 legendario) para las cartas del barco; Color = el de su mineral (casco, equipo).
    /// </summary>
    public sealed class MinerChar
    {
        public string Name, Desc;
        public int Rarity;
        public Trait Trait;
        public float Power;
        public int Spec;          // tipo de veta que pica mejor (-1 = todas)
        public int RoomLevel;     // nivel del Cuartel que habilita su habitacion (0 = no necesita)
        public string Hex;        // color del mineral
        public bool Secret;       // solo sale de la Sala secreta (no viene en el barco)
    }

    /// <summary>
    /// Mineros especialistas (0.10, idea del dueño): 9 identidades que se entienden en un segundo: "tiene diamantes
    /// encima, pica diamantes". Cada uno pica su mineral mucho mas rapido (y lo busca primero); el Maestro pica todo
    /// bastante mejor. El de Piedra viene de entrada; los demas necesitan su habitacion en el Cuartel. Al subir de nivel
    /// el minero cambia de aspecto en 3 etapas (siempre el mismo mineral).
    /// </summary>
    public sealed partial class Island
    {
        public const int OreStone = 0, OreCopper = 1, OreIron = 2, OreGold = 3, OreGem = 4, OreCoal = 5, OreCrystal = 6, OreRare = 7;
        public const float SpecMult = 2.2f, MasterMult = 1.5f;

        public static readonly MinerChar[] Roster =
        {
            new MinerChar { Name = Loc.T("Minero de Piedra"), Desc = Loc.T("Pica piedra"), Rarity = 0, Spec = OreStone, RoomLevel = 0, Hex = "9a9184" },
            new MinerChar { Name = Loc.T("Minero de Cobre"), Desc = Loc.T("Pica cobre"), Rarity = 0, Spec = OreCopper, RoomLevel = 1, Hex = "d9773a" },
            new MinerChar { Name = Loc.T("Minero de Hierro"), Desc = Loc.T("Pica hierro"), Rarity = 1, Spec = OreIron, RoomLevel = 2, Hex = "7d8590" },
            new MinerChar { Name = Loc.T("Minero de Carbón"), Desc = Loc.T("Pica carbón"), Rarity = 1, Spec = OreCoal, RoomLevel = 3, Hex = "3a3a40" },
            new MinerChar { Name = Loc.T("Minero de Oro"), Desc = Loc.T("Pica oro"), Rarity = 2, Spec = OreGold, RoomLevel = 4, Hex = "f2b632" },
            new MinerChar { Name = Loc.T("Minero de Cristal"), Desc = Loc.T("Pica cristales"), Rarity = 2, Spec = OreCrystal, RoomLevel = 5, Hex = "5ab8f0" },
            new MinerChar { Name = Loc.T("Minero de Diamante"), Desc = Loc.T("Pica diamantes"), Rarity = 3, Spec = OreGem, RoomLevel = 6, Hex = "3fa3ff" },
            new MinerChar { Name = Loc.T("Minero de Minerales Raros"), Desc = Loc.T("Pica minerales raros"), Rarity = 3, Spec = OreRare, RoomLevel = 7, Hex = "a35be8" },
            new MinerChar { Name = Loc.T("Minero Maestro"), Desc = Loc.T("Pica cualquier mineral"), Rarity = 3, Spec = -1, RoomLevel = 8, Hex = "e8b62c" },
            // 0.11: secretos (solo de la Sala secreta)
            new MinerChar { Name = Loc.T("Minero Topo"), Desc = Loc.T("Pica todo de abajo"), Rarity = 3, Spec = SpecMole, RoomLevel = 99, Hex = "8a6a4a", Secret = true },
            new MinerChar { Name = Loc.T("Minero Fantasma"), Desc = Loc.T("De noche pica el doble"), Rarity = 3, Spec = SpecGhost, RoomLevel = 99, Hex = "cfe8ff", Secret = true },
        };
        public const int SpecMole = -2, SpecGhost = -3;

        /// <summary>Etapa visual del minero segun su nivel: 1 (1-3), 2 (4-7), 3 (8-10).</summary>
        public static int Tier(Miner m) { return m.Level >= 8 ? 3 : m.Level >= 4 ? 2 : 1; }

        /// <summary>Multiplicador de golpe del minero sobre esta veta (su especialidad pica mucho mas rapido).</summary>
        public static float SpecBonus(Miner m, Ore o, bool night = false)
        {
            var c = Char(m);
            if (c.Spec == SpecMole) return 1.4f;
            if (c.Spec == SpecGhost) return night ? SpecMult : 1f;
            if (c.Spec < 0) return MasterMult;
            if (o.Kind == c.Spec) return SpecMult;
            if (c.Spec == OreCrystal && o.NightCrystal) return SpecMult;   // el de cristal tambien saca los cristales de noche
            return 1f;
        }

        /// <summary>Especialistas disponibles: el de Piedra siempre; los demas con su habitacion construida.</summary>
        public bool SpecUnlocked(int charId)
        {
            if (charId <= 0) return true;
            return HasRoom(charId);
        }

        public static readonly string[] RarityName = { Loc.T("Común"), Loc.T("Raro"), Loc.T("Épico"), Loc.T("Legendario") };
        static readonly double[] RarityWeight = { 60, 28, 10, 2 };
        public const double GoldenChance = 1.0 / 300.0;
        public const int MaxMinerLevel = 10;

        /// <summary>true: los mineros nuevos se eligen solos (simulacion y pruebas). La vista lo apaga y muestra cartas.</summary>
        public bool AutoRecruit = true;
        /// <summary>Candidatos que trajo el barco (indice del personaje y si es dorado). Vacio = no hay eleccion pendiente.</summary>
        public readonly List<int> Recruits = new List<int>();
        public readonly List<bool> RecruitGolden = new List<bool>();
        /// <summary>Cada candidato es una persona (su id define cara y nombre) y algunos vienen bloqueados (vista previa).</summary>
        public readonly List<int> RecruitIds = new List<int>();
        public readonly List<bool> RecruitLocked = new List<bool>();
        /// <summary>Album: personajes descubiertos (y su version dorada).</summary>
        public readonly bool[] Found = new bool[Roster.Length], FoundGolden = new bool[Roster.Length];
        readonly Dictionary<long, float> pairTime = new Dictionary<long, float>();
        float friendT;
        bool loadingSave;   // al cargar se rearman los mineros guardados (la tripulacion queda en el barco)

        public event Action RecruitsArrived;
        public event Action<Miner> MinerLevelUp;
        public event Action<Miner, Miner> BecameFriends;
        public event Action<Miner> LuckyGem;           // un minero suertudo encontro una gema

        public static MinerChar Char(Miner m) { return Roster[Math.Max(0, Math.Min(Roster.Length - 1, m.Char))]; }
        public int FoundCount { get { int n = 0; foreach (var f in Found) if (f) n++; return n; } }

        /// <summary>Elige un personaje al azar segun la rareza (los no descubiertos pesan un poco mas).</summary>
        int RollChar(out bool golden)
        {
            golden = rng.NextDouble() < GoldenChance;
            double sum = 0; foreach (var w in RarityWeight) sum += w;
            double r = rng.NextDouble() * sum;
            int rar = 0;
            for (int i = 0; i < RarityWeight.Length; i++) { r -= RarityWeight[i]; if (r <= 0) { rar = i; break; } }
            // solo especialistas con habitacion; se busca la rareza tirada o la mas cercana que haya
            var pool = new List<int>();
            for (int d = 0; d < 4 && pool.Count == 0; d++)
                for (int i = 0; i < Roster.Length; i++)
                    if (!Roster[i].Secret && SpecUnlocked(i) && (Roster[i].Rarity == rar - d || Roster[i].Rarity == rar + d)) { pool.Add(i); if (!Found[i]) pool.Add(i); }
            if (pool.Count == 0) pool.Add(0);
            return pool[rng.Next(pool.Count)];
        }

        void OfferRecruits()
        {
            Recruits.Clear(); RecruitGolden.Clear(); RecruitIds.Clear(); RecruitLocked.Clear();
            // antes: con solo el de Piedra habilitado salian 3 cartas identicas. Ahora cada candidato es una persona
            // distinta (cara y nombre propios) y, si todavia hay una sola clase, la tercera carta muestra BLOQUEADO al
            // proximo especialista con la habitacion que hace falta: se entiende que construir para conseguirlo.
            int unlocked = 0;
            for (int i = 0; i < Roster.Length; i++) if (!Roster[i].Secret && SpecUnlocked(i)) unlocked++;
            int preview = -1;
            if (unlocked < 2)
            {
                int best = int.MaxValue;
                for (int i = 0; i < Roster.Length; i++)
                    if (!Roster[i].Secret && !SpecUnlocked(i) && Roster[i].RoomLevel < best) { best = Roster[i].RoomLevel; preview = i; }
            }
            int n = preview >= 0 ? 2 : 3;
            for (int i = 0; i < n; i++)
            {
                bool g;
                int c = RollChar(out g);
                int tries = 0;
                while (Recruits.Contains(c) && tries++ < 10) c = RollChar(out g);
                Recruits.Add(c); RecruitGolden.Add(g); RecruitLocked.Add(false);
                RecruitIds.Add(nextMiner + i);
            }
            if (preview >= 0) { Recruits.Add(preview); RecruitGolden.Add(false); RecruitLocked.Add(true); RecruitIds.Add(nextMiner + n); }
            RecruitsArrived?.Invoke();
        }

        /// <summary>El jugador elige al candidato `i`: baja del barco y se suma a la isla.</summary>
        public Miner ChooseRecruit(int i)
        {
            if (i < 0 || i >= Recruits.Count || Miners.Count >= MinerCap()) return null;
            if (i < RecruitLocked.Count && RecruitLocked[i]) return null;   // vista previa: no se puede elegir
            int c = Recruits[i]; bool g = RecruitGolden[i];
            int id = i < RecruitIds.Count ? RecruitIds[i] : -1;
            Recruits.Clear(); RecruitGolden.Clear(); RecruitIds.Clear(); RecruitLocked.Clear();
            var m = AddMiner(c, g, false, id);
            if (Miners.Count < MinerCap()) OfferRecruits();
            return m;
        }

        Miner AddMiner(int charId, bool golden, bool initial, int id = -1)
        {
            if (id < nextMiner) id = nextMiner;   // (ids reservados para los candidatos; nunca uno ya usado)
            nextMiner = id + 1;
            Plot home = null; int seen = 0;
            foreach (var p in Plots)
                if (p.Building == (int)BKind.House) { seen += p.Level; if (seen > Miners.Count) { home = p; break; } }
            if (home == null) home = Find(BKind.House) ?? Plots[0];
            var m = new Miner { Id = id, Home = home.Id, X = home.X, Z = home.Z - 1.4f, Face = 0f, Char = charId, Golden = golden, Level = 1 };
            m.State = initial ? MState.Idle : MState.Spawning;
            Miners.Add(m);
            Found[charId] = true;
            if (golden) { FoundGolden[charId] = true; AddStat("golden", 1); }
            MinerSpawned?.Invoke(m);
            return m;
        }

        /// <summary>Si hay lugar en las casas y no hay candidatos esperando, llega el barco (al cargar la partida).</summary>
        public void CheckRecruits() { SyncMiners(false); }

        void SyncMiners(bool initial)
        {
            int cap = MinerCap();
            if (Miners.Count == 0 && cap > 0) { AddMiner(0, false, initial); }   // Tito, el primero
            if (!loadingSave) while (FromBench()) { }   // la tripulacion del barco baja primero
            if (AutoRecruit || initial)
            {
                while (Miners.Count < cap) { bool g; int c = RollChar(out g); AddMiner(c, g, initial); }
                return;
            }
            // con un solo especialista disponible no hay nada que elegir: el barco igual trae 3 cartas del mismo tipo
            if (Miners.Count < cap && Recruits.Count == 0) OfferRecruits();
        }

        // ------------------------------------------------------------ rasgos y nivel
        /// <summary>Multiplicador de golpe del minero (rasgo, nivel, dorado y capataces cerca).</summary>
        public float HitMult(Miner m)
        {
            var c = Char(m);
            float k = 1f + 0.04f * (m.Level - 1);
            if (c.Trait == Trait.Strong || c.Trait == Trait.Glutton) k *= c.Power;
            if (m.Golden) k *= 1.5f;
            return k * BossAura(m) * FriendBonus(m) * ComplexHit(m);
        }

        /// <summary>Multiplicador de caminata del minero.</summary>
        public float WalkMult(Miner m)
        {
            var c = Char(m);
            if (c.Trait == Trait.Fast) return c.Power;
            if (c.Trait == Trait.Sleepy) return 0.85f;
            return 1f;
        }

        float BossAura(Miner m)
        {
            foreach (var b in Miners)
                if (b != m && Char(b).Trait == Trait.Boss && Sq(b.X - m.X, b.Z - m.Z) < 16f) return 1f + Char(b).Power;
            return 1f;
        }

        float FriendBonus(Miner m)
        {
            if (m.Friend <= 0) return 1f;
            foreach (var f in Miners) if (f.Id == m.Friend && Sq(f.X - m.X, f.Z - m.Z) < 25f) return 1.1f;
            return 1f;
        }

        public static int XpFor(int level) { return 4 + 3 * level; }

        /// <summary>Al romper una veta: experiencia, y los rasgos de suerte y dormilon.</summary>
        void MinerBroke(Miner m, Ore o)
        {
            if (m == null || m.Id == 0) return;
            var c = Char(m);
            if (c.Trait == Trait.Lucky && rng.NextDouble() < c.Power) { Gems += 1; AddStat("lucky", 1); LuckyGem?.Invoke(m); }
            if (c.Trait == Trait.Sleepy && IsNight && rng.NextDouble() < c.Power) { Gems += 1; AddStat("lucky", 1); LuckyGem?.Invoke(m); }
            if (m.Level >= MaxMinerLevel) return;
            // la escuela suma 20 % de experiencia por nivel (redondeo con azar para no perder las fracciones)
            double xp = (o.Kind + 1) * (1.0 + 0.2 * Level(BKind.School));
            int whole = (int)xp;
            if (rng.NextDouble() < xp - whole) whole++;
            m.Xp += whole;
            while (m.Level < MaxMinerLevel && m.Xp >= XpFor(m.Level))
            {
                m.Xp -= XpFor(m.Level);
                m.Level++;
                AddStat("levelups", 1);
                MinerLevelUp?.Invoke(m);
            }
        }

        // ------------------------------------------------------------ amistades
        static long PairKey(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }

        void TickFriends(float dt)
        {
            friendT += dt;
            if (friendT < 0.5f) return;
            float step = friendT; friendT = 0f;
            for (int i = 0; i < Miners.Count; i++)
                for (int j = i + 1; j < Miners.Count; j++)
                {
                    var a = Miners[i]; var b = Miners[j];
                    if (a.Friend > 0 || b.Friend > 0) continue;
                    bool together = Sq(a.X - b.X, a.Z - b.Z) < 6.25f && (a.State == MState.Mining || a.State == MState.Eating) && a.State == b.State;
                    if (!together) continue;
                    long k = PairKey(a.Id, b.Id);
                    float t; pairTime.TryGetValue(k, out t);
                    t += step;
                    pairTime[k] = t;
                    if (t >= FriendTime)
                    {
                        a.Friend = b.Id; b.Friend = a.Id;
                        AddStat("friends", 1);
                        BecameFriends?.Invoke(a, b);
                    }
                }
        }

        public const float FriendTime = 60f;
    }
}
