using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>
    /// Estado global del juego: port 1:1 de game_state.gd (autoload "G"). C# puro, sin UnityEngine.
    /// Se divide en archivos parciales por tema: Economy, Meta (desbloqueos/metas/logros/esencia),
    /// Events, Tools, Daily (misiones/pase/tienda) y Persistence.
    /// </summary>
    public sealed partial class GameState
    {
        // ---------------------------------------------------------------- senales -> eventos
        public event Action Changed;
        public event Action EquipChanged;
        public event Action<int, int> StageCleared;
        public event Action BossNeeded;
        public event Action<string> Toast;
        public event Action<string> Upgraded;
        public event Action<string> FeatureUnlocked;
        public event Action<string, int> MilestoneReached;
        public event Action<Goal> GoalCompleted;
        public event Action<string, double> EventStarted;
        public event Action<string> EventEnded;
        public event Action StatsChanged;
        public event Action<int> Rebirthed;
        public event Action<int> BossDefeated;

        // ---------------------------------------------------------------- servicios
        readonly IClock clock;
        readonly Random rng;
        readonly ISaveStore store;

        // ---------------------------------------------------------------- estado
        public double Gold { get; set; }
        public int Gems { get; set; }
        public Dictionary<string, int> Lv { get; set; }
        public Dictionary<string, int> LvBest { get; set; }
        public int World { get; set; }
        public int Sub { get; set; }
        public double Progress { get; set; }
        public bool NeedBoss { get; set; }
        /// <summary>Etapa maxima de por vida (indice).</summary>
        public int MaxStage { get; set; }
        /// <summary>Etapa maxima de la partida actual (se reinicia al renacer).</summary>
        public int RunMax { get; set; }
        public Tool[] Inv { get; private set; }
        public Tool[] Equip { get; private set; }
        public Dictionary<string, int> Daily { get; set; }
        public string DailyDate { get; set; }
        public List<string> Claimed { get; set; }
        public int LoginDay { get; set; }
        public string LoginDate { get; set; }
        public int PassXp { get; set; }
        public double BoostUntil { get; set; }
        public double BoostCdUntil { get; set; }
        public bool AutoUp { get; set; }
        public bool Eco { get; set; }
        public bool Sound { get; set; }
        public bool Music { get; set; }
        public int Skin { get; set; }
        public List<int> SkinsOwned { get; set; }
        public int Rebirths { get; set; }
        public bool Turbo { get; set; }
        public double FreeChestAt { get; set; }
        public double PiggyT { get; set; }
        public int Piggy { get; set; }
        public double TotalGold { get; set; }
        public double LastSeen { get; set; }
        public List<string> Collection { get; set; }
        public double PendingOffline { get; set; }
        public double PendingOfflineTime { get; set; }

        // meta-progresion
        public Dictionary<string, long> Stats { get; set; }
        public List<string> FeaturesUnlocked { get; set; }
        public List<string> FeaturesSeen { get; set; }
        public int Essence { get; set; }
        public Dictionary<string, int> EssenceLv { get; set; }
        public List<string> AchClaimed { get; set; }
        public int GoalIdx { get; set; }
        public double GoalBase { get; set; }
        public int GoalGenTarget { get; set; }
        public string GoalGenKind { get; set; }

        double saveT;
        double pollT;
        bool goalNotified;

        /// <summary>Compatibilidad: el mundo hacia `G.total_rocks += 1`; se vuelca al contador "rocks".</summary>
        public long TotalRocks
        {
            get { return Stat("rocks"); }
            set { Stats["rocks"] = Math.Max(StatRaw("rocks"), value); }
        }

        public GameState(IClock clock = null, Random rng = null, ISaveStore store = null)
        {
            this.clock = clock ?? new SystemClock();
            this.rng = rng ?? new Random();
            this.store = store ?? new MemorySaveStore();
            Inv = new Tool[Balance.InvSize];
            Equip = new Tool[Balance.EquipSlots];
            Sound = true;
            Music = true;
            GoalGenKind = "power";
            EvNext = Balance.EventFirst;
            EventsEnabledField = true;
            ResetDefaults();
        }

        /// <summary>Equivale a _ready(): carga la partida guardada y deja todo consistente. Llamar una vez al iniciar.</summary>
        public void Start()
        {
            ResetValues();
            LoadGame();
            CheckDailyReset();
            SyncBest();
            CheckUnlocks();
        }

        // Valores iniciales de todos los campos (los que el _ready de Godot inicializa por declaracion).
        void ResetDefaults()
        {
            Gold = 0.0;
            Gems = 0;
            Lv = NewLevels();
            LvBest = NewLevels();
            World = 1;
            Sub = 1;
            Progress = 0.0;
            NeedBoss = false;
            MaxStage = 0;
            RunMax = 0;
            Daily = new Dictionary<string, int>();
            DailyDate = "";
            Claimed = new List<string>();
            SkinsOwned = new List<int> { 0 };
            Stats = new Dictionary<string, long>();
            FeaturesUnlocked = new List<string>();
            FeaturesSeen = new List<string>();
            EssenceLv = new Dictionary<string, int>();
            AchClaimed = new List<string>();
            LoginDate = "";
            ResetValues();
        }

        static Dictionary<string, int> NewLevels()
        {
            return new Dictionary<string, int> { { "power", 1 }, { "speed", 1 }, { "money", 1 } };
        }

        void ResetValues()
        {
            for (int i = 0; i < Inv.Length; i++) Inv[i] = null;
            Equip[0] = new Tool(0, 0);
            Equip[1] = new Tool(1, 0);
            Equip[2] = null;
            Collection = new List<string> { "0_0", "1_0" };
        }

        public double Now() { return clock.Now(); }
        public string Today() { return clock.Today(); }

        /// <summary>Equivale a _process(dt): llamar cada frame con el delta en segundos.</summary>
        public void Tick(double dt)
        {
            PiggyT += dt;
            if (PiggyT >= 60.0)
            {
                PiggyT -= 60.0;
                if (Piggy < Balance.PiggyCap)
                {
                    Piggy += 1;
                    Changed?.Invoke();
                }
            }
            saveT += dt;
            if (saveT > 5.0)
            {
                saveT = 0.0;
                SaveGame();
            }
            if (AutoUp) AutoBuy();
            TickEvents(dt);
            pollT += dt;
            if (pollT >= 0.25)
            {
                pollT = 0.0;
                SyncBest();
                CheckUnlocks();
                PollGoal();
            }
        }

        /// <summary>Equivale a _notification(CLOSE_REQUEST / APPLICATION_PAUSED): guardar al pausar o cerrar.</summary>
        public void OnAppPausedOrClosing() { SaveGame(); }

        // ---------------------------------------------------------------- estadisticas
        public void AddStat(string name, int n = 1)
        {
            Stats[name] = StatRaw(name) + n;
            StatsChanged?.Invoke();
        }

        long StatRaw(string name)
        {
            long v;
            return Stats.TryGetValue(name, out v) ? v : 0;
        }

        /// <summary>
        /// Contador de por vida. Nombres: rocks, bosses, merges, gacha, upgrades, plays, plays_won, plays_2star,
        /// crits, events, chests, boosts, missions, goals, stages, ach + derivados: gold, max_stage, rebirths, collection.
        /// </summary>
        public long Stat(string name)
        {
            switch (name)
            {
                case "gold": return NumUtil.ToLongClamped(Math.Min(TotalGold, 9.0e18));
                case "max_stage": return MaxStage;
                case "rebirths": return Rebirths;
                case "collection": return Collection.Count;
            }
            return StatRaw(name);
        }

        double StatF(string name)
        {
            if (name == "gold") return TotalGold;
            return (double)Stat(name);
        }

        // ---------------------------------------------------------------- progreso
        public void AddGold(double v)
        {
            Gold += v;
            TotalGold += v;
            Changed?.Invoke();
        }

        public void AddGems(int v)
        {
            Gems += v;
            Changed?.Invoke();
        }

        /// <summary>El mundo la llama al romper una roca (boss = es la Geoda gigante).</summary>
        public void OnRockBroken(bool boss)
        {
            Stats["rocks"] = StatRaw("rocks") + 1;
            AddDaily("rocks", 1);
            if (boss)
            {
                NeedBoss = false;
                Stats["bosses"] = StatRaw("bosses") + 1;
                int bg = BossGems();
                AddGems(bg);
                BossDefeated?.Invoke(bg);
                ClearStage();
                return;
            }
            if (Progress >= 1.0) return;
            Progress = Math.Min(1.0, Progress + 1.0 / RocksNeeded());
            if (Progress >= 1.0)
            {
                if (Sub == Balance.SubsPerWorld)
                {
                    NeedBoss = true;
                    BossNeeded?.Invoke();
                }
                else
                {
                    ClearStage();
                }
            }
            Changed?.Invoke();
        }

        void ClearStage()
        {
            int w = World;
            int s = Sub;
            Sub += 1;
            if (Sub > Balance.SubsPerWorld)
            {
                Sub = 1;
                World += 1;
            }
            Progress = 0.0;
            MaxStage = Math.Max(MaxStage, StageIdx());
            RunMax = Math.Max(RunMax, StageIdx());
            Stats["stages"] = StatRaw("stages") + 1;
            AddDaily("stages", 1);
            StageCleared?.Invoke(w, s);
            CheckUnlocks();
            Changed?.Invoke();
            StatsChanged?.Invoke();
            SaveGame();
        }

        /// <summary>Gemas del jefe: base 5 + 2 por nivel de Gemas de jefe.</summary>
        public int BossGems() { return Balance.BossGemsBase + 2 * EssenceLevel("boss"); }

        /// <summary>El mundo la llama al romper la Roca Cofre. Devuelve las gemas otorgadas.</summary>
        public int ChestOpened()
        {
            int g = (int)Math.Round(Balance.ChestGemsBase * (1.0 + 0.25 * EssenceLevel("chest")), MidpointRounding.AwayFromZero);
            AddGems(g);
            AddStat("chests");
            if (EvKind == "chest") EndEvent();
            return g;
        }

        /// <summary>Al terminar Excavar. stars (1-3) es opcional; si es &gt;= 2 suma "plays_2star".</summary>
        public void OnPlayFinished(bool won, int stars = 0)
        {
            Stats["plays"] = StatRaw("plays") + 1;
            if (won)
            {
                Stats["plays_won"] = StatRaw("plays_won") + 1;
                if (stars >= 2) Stats["plays_2star"] = StatRaw("plays_2star") + 1;
            }
            CheckUnlocks();
            StatsChanged?.Invoke();
        }

        // ---------------------------------------------------------------- reinicio total
        public void ResetAll()
        {
            Gold = 0.0;
            Gems = 0;
            Lv = NewLevels();
            LvBest = NewLevels();
            World = 1;
            Sub = 1;
            Progress = 0.0;
            NeedBoss = false;
            MaxStage = 0;
            RunMax = 0;
            ResetValues();
            Daily = new Dictionary<string, int>();
            Claimed = new List<string>();
            LoginDay = 0;
            LoginDate = "";
            PassXp = 0;
            BoostUntil = 0.0;
            BoostCdUntil = 0.0;
            AutoUp = false;
            Skin = 0;
            SkinsOwned = new List<int> { 0 };
            Rebirths = 0;
            Turbo = false;
            FreeChestAt = 0.0;
            Piggy = 0;
            TotalGold = 0.0;
            Stats = new Dictionary<string, long>();
            FeaturesUnlocked = new List<string>();
            FeaturesSeen = new List<string>();
            Essence = 0;
            EssenceLv = new Dictionary<string, int>();
            AchClaimed = new List<string>();
            GoalIdx = 0;
            GoalBase = 0.0;
            GoalGenTarget = 0;
            GoalGenKind = "power";
            goalNotified = false;
            EvKind = "";
            EvLeft = 0.0;
            EvNext = Balance.EventFirst;
            SaveGame();
            Changed?.Invoke();
            EquipChanged?.Invoke();
            StatsChanged?.Invoke();
            StageCleared?.Invoke(0, 0);
        }

        void EmitToast(string text) { Toast?.Invoke(text); }
    }
}
