using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Decoracion que se pone en los lugares fijos al costado de los caminos.</summary>
    public sealed class DecorDef { public string Name; public double Cost; public int Beauty; }

    /// <summary>
    /// Progreso grande (biblia 3.8 y decoracion): la Maravilla (Gran Estatua de Oro en 5 fases, visible como ruinas
    /// desde el primer dia), decoracion con puntaje de belleza, jefe semanal (Golem de Roca), pase de temporada,
    /// museo de piezas (sets con bonus permanente) y expediciones a islas nuevas con reliquias que multiplican todo.
    /// </summary>
    public sealed partial class Island
    {
        // ------------------------------------------------------------ multiplicador general
        /// <summary>Maravilla x belleza x reliquias x museo x isla (se aplica al precio de todo lo que se vende).</summary>
        public double GlobalMult() { return WonderMult() * BeautyMult() * RelicMult() * (SetDone(0) ? 1.1 : 1.0) * IslandValue(); }

        // ------------------------------------------------------------ maravilla
        public int WonderPhase;           // 0 = ruinas .. 5 = terminada
        public const int WonderPhases = 5;
        static readonly double[] WonderCostBase = { 2500, 9000, 30000, 90000, 260000 };
        public static readonly string[] WonderPhaseName = { Loc.T("Limpiar las ruinas"), Loc.T("Pedestal de piedra"), Loc.T("Piernas de la estatua"), Loc.T("Cuerpo y pico"), Loc.T("¡Baño de oro!") };
        public event Action<int> WonderBuilt;

        public double WonderCost() { return WonderPhase >= WonderPhases ? 0 : Math.Round(WonderCostBase[WonderPhase] * IslandValue()); }
        public bool CanBuildWonder() { return WonderPhase < WonderPhases && Coins >= WonderCost(); }
        public double WonderMult() { return 1.0 + 0.08 * WonderPhase + (WonderPhase >= WonderPhases ? 0.1 : 0.0); }

        public bool BuildWonder()
        {
            if (!CanBuildWonder()) return false;
            Coins -= WonderCost();
            WonderPhase++;
            AddStat("wonder", 1);
            AddXp(80);
            WonderBuilt?.Invoke(WonderPhase);
            return true;
        }

        /// <summary>
        /// Lugar de la Maravilla: el fondo del pueblo, centrado arriba en la pantalla (la camara mira a 55°), en el hueco
        /// entre dos parcelas del medio y lejos de las de afuera: un monumento que se ve siempre detras del pueblo.
        /// </summary>
        public void WonderSpot(out float x, out float z)
        {
            const double a = 67.5 * Math.PI / 180.0;
            const float r = 12.5f;
            x = (float)(Math.Cos(a) * r); z = (float)(Math.Sin(a) * r);
        }

        // ------------------------------------------------------------ decoracion y belleza
        public static readonly DecorDef[] DecorDefs =
        {
            new DecorDef { Name = Loc.T("Cantero de flores"), Cost = 150, Beauty = 2 },
            new DecorDef { Name = Loc.T("Cerca de madera"), Cost = 220, Beauty = 2 },
            new DecorDef { Name = Loc.T("Banco"), Cost = 350, Beauty = 3 },
            new DecorDef { Name = Loc.T("Farol doble"), Cost = 600, Beauty = 4 },
            new DecorDef { Name = Loc.T("Arbusto podado"), Cost = 900, Beauty = 5 },
            new DecorDef { Name = Loc.T("Fuente"), Cost = 3500, Beauty = 12 },
            new DecorDef { Name = Loc.T("Estatuita de minero"), Cost = 8000, Beauty = 18 },
        };
        public readonly List<float[]> DecorSlots = new List<float[]>();   // x, z, giro (radianes)
        public readonly Dictionary<int, int> Decor = new Dictionary<int, int>();   // lugar → decoracion
        public event Action<int> DecorPlaced;

        /// <summary>Recalcula los lugares de decoracion (al costado de los caminos de lo que ya esta abierto).</summary>
        public void BuildDecorSlots()
        {
            // lugares que bordean la plaza y los caminos (ni encima ni lejos), fuera de las parcelas y separados entre si
            DecorSlots.Clear();
            float lim = Radius - 1.5f;
            for (int ring = 0; ring < 12; ring++)
            {
                float r = PlazaR + 1.05f + ring * 0.75f;
                if (r > lim) break;
                int n = Math.Max(8, (int)(r * 2.6f));
                for (int i = 0; i < n; i++)
                {
                    double a = (i + (ring % 2) * 0.5) / n * Math.PI * 2;
                    float x = (float)Math.Cos(a) * r, z = (float)Math.Sin(a) * r;
                    if (OnPath(x, z, 0.5f) || !OnPath(x, z, 1.45f)) continue;
                    bool ok = true;
                    foreach (var p in Plots) if (Sq(x - p.X, z - p.Z) < 2.1f * 2.1f) { ok = false; break; }
                    if (!ok) continue;
                    foreach (var q in DecorSlots) if (Sq(x - q[0], z - q[1]) < 1.9f * 1.9f) { ok = false; break; }
                    if (!ok) continue;
                    DecorSlots.Add(new[] { x, z, (float)Math.Atan2(-x, -z) });   // mirando a la plaza
                }
            }
        }

        public int Beauty
        {
            get { int b = ComplexBeauty(); foreach (var kv in Decor) if (kv.Key < DecorSlots.Count) b += DecorDefs[kv.Value].Beauty; return b; }
        }

        /// <summary>La belleza pone contentos a los mineros: +0.5 % por punto, hasta +25 %.</summary>
        public double BeautyMult() { return 1.0 + Math.Min(0.25, Beauty * 0.005); }

        /// <summary>Adorno del dia (-40 %): lo fija la vista con el dia del calendario; rota cada 24 h (motivo para volver).</summary>
        public int DecorDeal = -1;
        public const double DecorDealMult = 0.6;

        public double DecorCost(int item) { return Math.Round(DecorDefs[item].Cost * IslandValue() * (item == DecorDeal ? DecorDealMult : 1.0)); }

        public bool PlaceDecor(int slot, int item)
        {
            if (slot < 0 || slot >= DecorSlots.Count || item < 0 || item >= DecorDefs.Length) return false;
            if (Decor.ContainsKey(slot) || Coins < DecorCost(item)) return false;
            Coins -= DecorCost(item);
            Decor[slot] = item;
            AddStat("decor", 1);
            AddXp(10);
            DecorPlaced?.Invoke(slot);
            return true;
        }

        public bool DecorBlocked(float x, float z, float margin)
        {
            foreach (var kv in Decor)
            {
                if (kv.Key >= DecorSlots.Count) continue;
                var s = DecorSlots[kv.Key];
                if (Sq(x - s[0], z - s[1]) < (0.8f + margin) * (0.8f + margin)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------ jefe semanal (Golem de Roca)
        public int BossLevel;
        float bossT = 600f;
        public event Action<Ore> BossCame;
        public event Action<Ore, int> BossPhase;       // 2 = a 2/3 de vida, 1 = a 1/3
        public event Action<Ore> BossDefeated;

        public Ore Boss { get { foreach (var o in OreList) if (o.Boss && !o.Dead) return o; return null; } }

        void TickBoss(float dt)
        {
            if (Boss != null || TotalEarned < 3000 || Giant != null) return;
            bossT -= dt;
            if (bossT > 0f) return;
            bossT = 1500f;
            SpawnBoss();
        }

        public void BossSoon() { bossT = 0f; }

        public Ore SpawnBoss()
        {
            var o = SpawnGiant(false, true);
            if (o == null) return null;
            BossCame?.Invoke(o);
            return o;
        }

        void BossHit(Ore o)
        {
            int ph = o.Hp <= 0 ? 0 : o.Hp < o.MaxHp / 3 ? 1 : o.Hp < o.MaxHp * 2 / 3 ? 2 : 3;
            if (ph < o.BossPhase && ph > 0) { o.BossPhase = ph; BossPhase?.Invoke(o, ph); }
        }

        void BossReward(Ore o)
        {
            BossLevel++;
            AddStat("bosses", 1);
            Gems += 5 + BossLevel + (SetDone(2) ? 1 : 0);
            Earn(CoinPrize(600));
            GiveChest(BossLevel % 4 == 0 ? 3 : 2);
            AddXp(150);
            BossDefeated?.Invoke(o);
        }

        // ------------------------------------------------------------ pase de temporada
        public int SeasonXp, SeasonStart = -1;
        public bool PassGold;
        public long PassFree, PassGoldClaimed;   // bits: niveles cobrados
        public const int SeasonLevels = 30, XpPerLevel = 100, SeasonDays = 28, PassGoldGems = 120;
        public event Action<int> SeasonLevelUp;
        public static readonly string[] SeasonThemes = { Loc.T("Fiebre del oro"), Loc.T("Tesoros piratas"), Loc.T("Cristales de luna"), Loc.T("Verano tropical") };

        public int SeasonLevel { get { return Math.Min(SeasonLevels, SeasonXp / XpPerLevel); } }
        public int SeasonNo(int today) { return SeasonStart < 0 ? 0 : Math.Max(0, (today - SeasonStart) / SeasonDays); }
        public int SeasonDaysLeft(int today) { return SeasonStart < 0 ? SeasonDays : SeasonDays - ((today - SeasonStart) % SeasonDays); }

        /// <summary>Empieza o renueva la temporada (cada 28 dias): el progreso vuelve a cero.</summary>
        public void CheckSeason(int today)
        {
            if (SeasonStart < 0) { SeasonStart = today; return; }
            if (today - SeasonStart >= SeasonDays)
            {
                SeasonStart += ((today - SeasonStart) / SeasonDays) * SeasonDays;
                SeasonXp = 0; PassFree = 0; PassGoldClaimed = 0; PassGold = false;
            }
        }

        public void AddXp(int xp)
        {
            int before = SeasonLevel;
            SeasonXp = Math.Min(SeasonLevels * XpPerLevel, SeasonXp + xp);
            if (SeasonLevel > before) SeasonLevelUp?.Invoke(SeasonLevel);
        }

        /// <summary>Premio del nivel `lv` (1..30) del pase: tipo ("coins","gems","chest") y cantidad o rareza.</summary>
        public void PassPrize(int lv, bool gold, out string kind, out double amount)
        {
            if (!gold)
            {
                if (lv % 10 == 5) { kind = "chest"; amount = lv >= 25 ? 1 : 0; return; }
                if (lv % 3 == 0) { kind = "gems"; amount = 2; return; }
                kind = "coins"; amount = CoinPrize(60 + lv * 10); return;
            }
            if (lv == 30) { kind = "chest"; amount = 3; return; }
            if (lv % 10 == 0) { kind = "chest"; amount = 2; return; }
            if (lv % 5 == 0) { kind = "chest"; amount = 1; return; }
            kind = "gems"; amount = 3 + lv / 10;
        }

        public bool PassClaimed(int lv, bool gold) { return ((gold ? PassGoldClaimed : PassFree) & (1L << lv)) != 0; }

        public bool ClaimPass(int lv, bool gold)
        {
            if (lv < 1 || lv > SeasonLevel || PassClaimed(lv, gold) || (gold && !PassGold)) return false;
            string kind; double amount;
            PassPrize(lv, gold, out kind, out amount);
            if (kind == "coins") Earn(amount);
            else if (kind == "gems") Gems += (int)amount;
            else GiveChest((int)amount);
            if (gold) PassGoldClaimed |= 1L << lv; else PassFree |= 1L << lv;
            return true;
        }

        public bool BuyPassGold()
        {
            if (PassGold || Gems < PassGoldGems) return false;
            Gems -= PassGoldGems;
            PassGold = true;
            return true;
        }

        // ------------------------------------------------------------ museo
        public static readonly string[] SetName = { Loc.T("Dinosaurio"), Loc.T("Barco hundido"), Loc.T("Corona perdida") };
        public static readonly string[] SetBonus = { "+10 % a todo lo que se vende", Loc.T("Los pedidos pagan +25 %"), "+1 gema por jefe y más críticos" };
        public static readonly string[,] PieceName =
        {
            { Loc.T("Cráneo"), Loc.T("Costillas"), Loc.T("Pata"), Loc.T("Cola") },
            { Loc.T("Timón"), Loc.T("Ancla"), Loc.T("Cofre roto"), Loc.T("Mascarón") },
            { Loc.T("Aro de oro"), Loc.T("Rubí"), Loc.T("Esmeralda"), Loc.T("Zafiro") },
        };
        public int Pieces;   // 12 bits (set * 4 + pieza)
        public event Action<int, int> PieceFound;
        public const double PieceChance = 1.0 / 90.0;

        public bool HasPiece(int set, int piece) { return (Pieces & (1 << (set * 4 + piece))) != 0; }
        public bool SetDone(int set) { int m = 0xF << (set * 4); return (Pieces & m) == m; }

        /// <summary>Tira por una pieza del museo (al romper vetas y al cavar tesoros). Devuelve true si salio una nueva.</summary>
        public bool RollPiece(double chance)
        {
            if (rng.NextDouble() >= chance) return false;
            var missing = new List<int>();
            for (int i = 0; i < 12; i++) if ((Pieces & (1 << i)) == 0) missing.Add(i);
            if (missing.Count == 0) return false;
            int b = missing[rng.Next(missing.Count)];
            Pieces |= 1 << b;
            AddStat("pieces", 1);
            AddXp(30);
            PieceFound?.Invoke(b / 4, b % 4);
            return true;
        }

        // ------------------------------------------------------------ expediciones (islas nuevas y reliquias)
        public int IslandNo;     // 0 verde, 1 nevada, 2 volcanica, 3 selva, 4 cristal (despues se repite mas cara)
        public int Relics;
        public readonly List<Miner> Bench = new List<Miner>();   // tripulacion que viaja y vuelve a bajar
        public static readonly string[] IslandNames = { Loc.T("Isla Verde"), Loc.T("Isla Dunas"), Loc.T("Isla Volcánica"), Loc.T("Isla Cristal"), Loc.T("Isla Nevada") };
        public event Action<int> Sailed;

        public int Biome { get { return IslandNo % IslandNames.Length; } }
        public double RelicMult() { return 1.0 + 0.1 * Relics; }
        /// <summary>Cada isla nueva vale mas (vetas, premios y costos escalan juntos).</summary>
        public double IslandValue() { return Math.Pow(3.0, IslandNo); }
        public bool CanSail { get { return WonderPhase >= WonderPhases; } }
        public int RelicsFor() { return Math.Max(3, (int)Math.Floor(Math.Sqrt(TotalEarned / (20000.0 * IslandValue())))) + IslandNo * 2; }

        /// <summary>
        /// Zarpar: se gana reliquias y se empieza una isla nueva. Se conservan gemas, album, museo, pase, racha y la
        /// tripulacion (vuelve a bajar a medida que hay lugar en las casas, sin esperar el barco).
        /// </summary>
        public bool Sail()
        {
            if (!CanSail) return false;
            Relics += RelicsFor();
            IslandNo++;
            AddStat("islands", 1);
            AddXp(200);
            Coins = 0; TotalEarned = 0; Expand = 0; WonderPhase = 0; GoalIdx = 0; TurboT = 0; FrenzyT = 0;
            CurShip = null; CurBottle = null; CurCritter = null; Recruits.Clear(); RecruitGolden.Clear();
            Decor.Clear(); Orders.Clear(); OreList.Clear(); ResetComplex();
            Raft = RaftState.Docked; Crew.Clear(); FarFocus = -1;   // la isla lejana queda descubierta; sus rocas vuelven a salir
            for (int i = 0; i < Plots.Count; i++)
            {
                var pl = Plots[i];
                pl.Building = -1; pl.Level = 0; pl.BuildT = -1f;
                pl.Work = 0; pl.WorkTotal = 0; pl.Queue.Clear(); pl.Ready = 0; pl.ReadyRes = -1; pl.ProdT = 0;
            }
            // la ciudad arranca de cero (los constructores comprados se conservan)
            for (int i = 0; i < Stock.Length; i++) Stock[i] = 0;
            TrainHere = false; Wagons.Clear(); TrainT = 120; Merchant.Clear();
            Plots[0].Building = (int)BKind.Depot; Plots[0].Level = 1;
            Plots[1].Building = (int)BKind.House; Plots[1].Level = 1;
            for (int i = 1; i < Miners.Count; i++) Bench.Add(Miners[i]);
            var first = Miners.Count > 0 ? Miners[0] : null;
            Miners.Clear();
            if (first != null)
            {
                first.State = MState.Idle; first.Target = -1; first.CarryKind = -1; first.CarryUnits = 0; first.Energy = 100f; first.Clean = 100f;
                first.X = Plots[1].X; first.Z = Plots[1].Z - 1.4f;
                Miners.Add(first);
            }
            Stats["goal_base_rocks"] = Stat("rocks");
            Stats["goal_base_giants"] = Stat("giants");
            BuildDecorSlots();
            for (int i = 0; i < 5; i++) SpawnOre(true);
            Sailed?.Invoke(IslandNo);
            return true;
        }

        /// <summary>Si hay alguien de la tripulacion esperando en el barco, baja a ocupar el lugar libre.</summary>
        bool FromBench()
        {
            if (Bench.Count == 0 || Miners.Count >= MinerCap()) return false;
            var m = Bench[0];
            Bench.RemoveAt(0);
            var home = Find(BKind.House) ?? Plots[1];
            m.Id = nextMiner++;   // id nuevo: los guardados se renumeran al cargar
            m.Friend = 0;
            m.Home = home.Id; m.X = home.X; m.Z = home.Z - 1.4f;
            m.State = MState.Spawning; m.T = 0f; m.Target = -1; m.CarryKind = -1; m.Energy = 100f; m.Clean = 100f;
            Miners.Add(m);
            MinerSpawned?.Invoke(m);
            return true;
        }

        // ------------------------------------------------------------ guardado
        Dictionary<string, object> ProgressObj()
        {
            var deco = new List<object>();
            foreach (var kv in Decor) deco.Add(new List<object> { kv.Key, kv.Value });
            var bench = new List<object>();
            foreach (var m in Bench) bench.Add(new Dictionary<string, object> { { "c", m.Char }, { "g", m.Golden ? 1 : 0 }, { "l", m.Level }, { "x", m.Xp } });
            return new Dictionary<string, object>
            {
                { "wonder", WonderPhase }, { "deco", deco }, { "boss", BossLevel }, { "bossT", bossT },
                { "sxp", SeasonXp }, { "sstart", SeasonStart }, { "pgold", PassGold ? 1 : 0 }, { "pfree", PassFree }, { "pgc", PassGoldClaimed },
                { "pieces", Pieces }, { "isle", IslandNo }, { "relics", Relics }, { "bench", bench },
            };
        }

        void LoadProgress(Dictionary<string, object> d)
        {
            WonderPhase = Math.Max(0, Math.Min(WonderPhases, JsonRead.Int(d, "wonder", 0)));
            BossLevel = JsonRead.Int(d, "boss", 0);
            bossT = (float)JsonRead.Dbl(d, "bossT", 600);
            SeasonXp = JsonRead.Int(d, "sxp", 0);
            SeasonStart = JsonRead.Int(d, "sstart", -1);
            PassGold = JsonRead.Int(d, "pgold", 0) == 1;
            PassFree = (long)JsonRead.Dbl(d, "pfree", 0);
            PassGoldClaimed = (long)JsonRead.Dbl(d, "pgc", 0);
            Pieces = JsonRead.Int(d, "pieces", 0);
            IslandNo = JsonRead.Int(d, "isle", 0);
            Relics = JsonRead.Int(d, "relics", 0);
            Decor.Clear();
            object dl;
            if (d.TryGetValue("deco", out dl) && dl is List<object> list)
                foreach (var x in list)
                    if (x is List<object> pair && pair.Count == 2) Decor[(int)JsonRead.ToDouble(pair[0], 0)] = (int)JsonRead.ToDouble(pair[1], 0);
            Bench.Clear();
            object bl;
            if (d.TryGetValue("bench", out bl) && bl is List<object> bs)
                foreach (var x in bs)
                {
                    var md = x as Dictionary<string, object>;
                    if (md == null) continue;
                    Bench.Add(new Miner { Id = nextMiner++, Char = JsonRead.Int(md, "c", 0) < Roster.Length ? JsonRead.Int(md, "c", 0) : 0, Golden = JsonRead.Int(md, "g", 0) == 1, Level = JsonRead.Int(md, "l", 1), Xp = JsonRead.Int(md, "x", 0) });
                }
        }
    }
}
