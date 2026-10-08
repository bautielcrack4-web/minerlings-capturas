using System;

namespace Mineros.Core
{
    public enum CritterKind { Crab = 0, Gull = 1, Mole = 2, Butterfly = 3 }

    /// <summary>Bicho que aparece unos segundos: si el jugador lo toca a tiempo, suelta su premio.</summary>
    public sealed class Critter
    {
        public CritterKind Kind;
        public float X, Z, Left, Age;
    }

    /// <summary>Botella que trajo la marea (en la orilla) y el tesoro marcado con una X en el mapa.</summary>
    public sealed class Bottle
    {
        public float X, Z;            // donde quedo en la arena
        public bool Opened;
        public float TX, TZ;          // la X del tesoro
        public int Digger = -1;       // minero que va a cavar
        public float DigT;
    }

    /// <summary>
    /// Sorpresas (biblia 3.5): cofres que pueden subir de rareza antes de abrirse (madera → plata → oro → legendario),
    /// botellas con mapas del tesoro (un minero cava en la X y sale un cofre) y bichos que hay que tocar (cangrejo con
    /// gema, gaviota ladrona que suelta monedas, topo con tesoro y mariposa dorada que da turbo).
    /// </summary>
    public sealed partial class Island
    {
        // ------------------------------------------------------------ cofres que suben de rareza
        public static readonly string[] ChestName = { Loc.T("Cofre de madera"), Loc.T("Cofre de plata"), Loc.T("Cofre de oro"), Loc.T("Cofre legendario") };
        static readonly double[] UpgradeChance = { 0.35, 0.25, 0.12 };
        public const int ChestUpgradeTries = 3;
        /// <summary>Cofre que se esta abriendo (-1 = ninguno).</summary>
        public int OpenTier = -1;

        /// <summary>Saca el mejor cofre para abrirlo. Devuelve su rareza o -1 si no hay.</summary>
        public int BeginChest()
        {
            if (OpenTier >= 0) return OpenTier;
            for (int t = Chests.Length - 1; t >= 0; t--)
                if (Chests[t] > 0) { Chests[t]--; OpenTier = t; return t; }
            return -1;
        }

        /// <summary>Un toque antes de abrir: con suerte el cofre sube de rareza. true si subio.</summary>
        public bool TryUpgradeChest()
        {
            if (OpenTier < 0 || OpenTier >= 3) return false;
            if (rng.NextDouble() >= UpgradeChance[OpenTier]) return false;
            OpenTier++;
            AddStat("chest_up", 1);
            return true;
        }

        /// <summary>Abre el cofre en curso y cobra el botin.</summary>
        public ChestLoot FinishChest()
        {
            int t = OpenTier;
            if (t < 0) return null;
            OpenTier = -1;
            AddStat("chests", 1);
            AddXp(25);
            var l = new ChestLoot { Tier = t };
            double secs = t == 0 ? 60 : t == 1 ? 150 : t == 2 ? 400 : 1000;
            l.Coins = Math.Round(CoinPrize(secs) * (0.85 + rng.NextDouble() * 0.3));
            l.Gems = t == 0 ? (rng.NextDouble() < 0.5 ? 1 : 0) : t == 1 ? 2 + rng.Next(2) : t == 2 ? 5 + rng.Next(4) : 12 + rng.Next(5);
            l.Turbo = t == 3 ? 120f : t == 2 ? 60f : (t == 1 && rng.NextDouble() < 0.4 ? 30f : 0f);
            Earn(l.Coins);
            Gems += l.Gems;
            TurboT += l.Turbo;
            // Complejo: planos (epico 15 %, legendario 40 %) y modulos portatiles (desde plata, 25 %)
            if (BarracksLevel >= 2)
            {
                double pc = t == 2 ? 0.15 : t == 3 ? 0.40 : 0.0;
                if (pc > 0 && Plans < MaxPlans && rng.NextDouble() < pc) { l.Plans = 1; AddPlans(1); }
                if (t >= 1 && rng.NextDouble() < 0.25) { l.Portable = (int)PortableKinds[rng.Next(PortableKinds.Length)]; AddPortable((ModKind)l.Portable); }
            }
            return l;
        }

        // ------------------------------------------------------------ botella con mapa
        public Bottle CurBottle;
        float bottleT = 240f;
        public event Action<Bottle> BottleArrived;
        public event Action<Bottle> BottleOpened;
        public event Action<Bottle, int> TreasureDug;    // tier del cofre que salio

        void TickBottle(float dt)
        {
            if (CurBottle == null)
            {
                if (TotalEarned < 200) return;
                bottleT -= dt;
                if (bottleT > 0f) return;
                bottleT = 240f + (float)rng.NextDouble() * 180f;
                double a = rng.NextDouble() * Math.PI * 2;
                float r = Radius + 0.3f;
                CurBottle = new Bottle { X = (float)Math.Cos(a) * r, Z = (float)Math.Sin(a) * r };
                BottleArrived?.Invoke(CurBottle);
                return;
            }
            var b = CurBottle;
            if (!b.Opened) return;
            // el minero elegido va a la X y cava
            Miner m = null;
            foreach (var x in Miners) if (x.Id == b.Digger) m = x;
            if (m == null)
            {
                float best = float.MaxValue;
                foreach (var x in Miners)
                {
                    if (x.CarryKind >= 0 || x.State == MState.Showering || x.State == MState.Eating) continue;
                    float d = Sq(x.X - b.TX, x.Z - b.TZ);
                    if (d < best) { best = d; m = x; }
                }
                if (m == null) return;
                Release(m);
                b.Digger = m.Id;
                GoTo(m, MState.ToDig, b.TX, b.TZ, -1);
            }
        }

        /// <summary>Para pruebas y capturas: la proxima botella llega ya.</summary>
        public void BottleSoon() { if (CurBottle == null) bottleT = 0f; }

        /// <summary>El jugador toca la botella: se abre el mapa y aparece la X del tesoro.</summary>
        public bool OpenBottle()
        {
            var b = CurBottle;
            if (b == null || b.Opened) return false;
            for (int tries = 0; tries < 60; tries++)
            {
                double a = rng.NextDouble() * Math.PI * 2, d = 2.5 + rng.NextDouble() * (Radius - 4.5);
                float x = (float)(Math.Cos(a) * d), z = (float)(Math.Sin(a) * d);
                if (!FreeSpot(x, z, 1.0f)) continue;
                b.TX = x; b.TZ = z; b.Opened = true;
                AddStat("bottles", 1);
                BottleOpened?.Invoke(b);
                return true;
            }
            b.TX = 0f; b.TZ = -Island.PlazaR - 1.5f; b.Opened = true;
            BottleOpened?.Invoke(b);
            return true;
        }

        public const float DigTime = 3f;

        void TickDig(Miner m, float dt, float perf)
        {
            var b = CurBottle;
            if (b == null || !b.Opened || b.Digger != m.Id) { Set(m, MState.Idle); return; }
            if (m.State == MState.ToDig)
            {
                if (Walk(m, b.TX, b.TZ, 0.7f, dt, perf * WalkMult(m))) { Set(m, MState.Digging); m.HitT = 0f; }
                return;
            }
            m.Face = (float)Math.Atan2(b.TX - m.X, b.TZ - m.Z);
            m.HitT += dt;
            if (m.T < DigTime) return;
            int tier = rng.NextDouble() < 0.15 ? 2 : rng.NextDouble() < 0.5 ? 1 : 0;
            CurBottle = null;
            AddStat("treasures", 1);
            AddXp(40);
            RollPiece(0.4);
            GiveChest(tier);
            TreasureDug?.Invoke(b, tier);
            Set(m, MState.Idle);
            MinerMood?.Invoke(m, "cheer");
        }

        // ------------------------------------------------------------ bichos
        public Critter CurCritter;
        float critterT = 45f;
        public event Action<Critter> CritterCame;
        public event Action<Critter, bool> CritterGone;   // true = lo atrapo el jugador

        public static float CritterStay(CritterKind k) { return k == CritterKind.Gull ? 6f : k == CritterKind.Butterfly ? 9f : 8f; }

        void TickCritter(float dt)
        {
            if (CurCritter != null)
            {
                CurCritter.Age += dt;
                CurCritter.Left -= dt;
                if (CurCritter.Left <= 0f) { var c = CurCritter; CurCritter = null; CritterGone?.Invoke(c, false); }
                return;
            }
            if (TotalEarned < 100) return;
            critterT -= dt;
            if (critterT > 0f) return;
            critterT = 40f + (float)rng.NextDouble() * 35f;
            double r = rng.NextDouble();
            var k = r < 0.35 ? CritterKind.Crab : r < 0.65 ? CritterKind.Gull : r < 0.9 ? CritterKind.Mole : CritterKind.Butterfly;
            SpawnCritter(k);
        }

        /// <summary>Hace aparecer un bicho (tambien para pruebas y capturas).</summary>
        public Critter SpawnCritter(CritterKind k)
        {
            float x, z;
            if (k == CritterKind.Crab)
            {
                double a = rng.NextDouble() * Math.PI * 2;
                x = (float)Math.Cos(a) * (Radius - 0.2f); z = (float)Math.Sin(a) * (Radius - 0.2f);
            }
            else
            {
                x = 0f; z = 0f;
                for (int i = 0; i < 40; i++)
                {
                    double a = rng.NextDouble() * Math.PI * 2, d = 2 + rng.NextDouble() * (Radius - 3.5);
                    x = (float)(Math.Cos(a) * d); z = (float)(Math.Sin(a) * d);
                    if (FreeSpot(x, z, 0.6f)) break;
                }
            }
            CurCritter = new Critter { Kind = k, X = x, Z = z, Left = CritterStay(k) };
            CritterCame?.Invoke(CurCritter);
            return CurCritter;
        }

        /// <summary>El jugador toco el bicho: premio segun el tipo. Devuelve las monedas dadas.</summary>
        public double CatchCritter()
        {
            var c = CurCritter;
            if (c == null) return -1;
            CurCritter = null;
            AddStat("critters", 1);
            AddXp(15);
            double coins = 0;
            switch (c.Kind)
            {
                case CritterKind.Crab: Gems += 1; break;
                case CritterKind.Gull: coins = CoinPrize(40); Earn(coins); break;
                case CritterKind.Mole: coins = CoinPrize(25); Earn(coins); if (rng.NextDouble() < 0.3) Gems += 1; break;
                case CritterKind.Butterfly: TurboT += 60f; break;
            }
            CritterGone?.Invoke(c, true);
            return coins;
        }
    }
}
