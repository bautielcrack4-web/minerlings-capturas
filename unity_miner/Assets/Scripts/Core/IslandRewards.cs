using System;

namespace Mineros.Core
{
    /// <summary>Premio de la ruleta del mercader.</summary>
    public sealed class WheelPrize
    {
        public string Name, Kind;   // Kind: coins, gems, turbo, chest, giant
        public double Amount;       // coins: segundos de ingreso; gems: cantidad; turbo: segundos; chest: tier
        public double Weight;
    }

    /// <summary>Contenido de un cofre abierto.</summary>
    public sealed class ChestLoot
    {
        public int Tier;
        public double Coins;
        public int Gems;
        public float Turbo;
        public int Plans;                 // planos de Sala secreta (0.11)
        public int Portable = -1;         // ModKind de un modulo portatil
        public int FxCard = -1;           // carta de efecto (a veces)
    }

    /// <summary>
    /// Recompensas variables (lo que engancha): el mercader en globo pasa cada pocos minutos y regala un giro de la
    /// ruleta; la ruleta da monedas, gemas, turbo, cofres o una veta de oro gigante que cae del cielo; los cofres
    /// (madera, plata, oro) salen de la ruleta y de las vetas gigantes. Tambien las etapas de evolucion de edificios.
    /// </summary>
    public sealed partial class Island
    {
        public int Spins;
        public readonly int[] Chests = new int[4];   // madera, plata, oro, legendario
        public bool BalloonHere;
        public float BalloonLeft;             // segundos que le quedan al globo en la isla
        float balloonT = 240f;   // el primero a los 4 min de juego (antes 50 s: aparecia "todo el tiempo")
        public int PendingPrize = -1;

        /// <summary>
        /// Medidor del premio mayor: cada giro suma uno y el giro numero `JackpotEvery` cae seguro en un premio grande
        /// (lo que hace que se quiera volver a girar). Se vacia con cualquier premio grande, salga cuando salga.
        /// </summary>
        public int SpinStreak;
        public const int JackpotEvery = 5;

        public event Action BalloonCame;
        public event Action<bool> BalloonGone;   // true = el jugador lo toco
        public event Action<int> ChestGot;       // tier

        public const float BalloonStay = 28f;

        public static readonly WheelPrize[] Wheel =
        {
            new WheelPrize { Name = Loc.T("Monedas"), Kind = "coins", Amount = 45, Weight = 24 },
            new WheelPrize { Name = Loc.T("2 gemas"), Kind = "gems", Amount = 2, Weight = 16 },
            new WheelPrize { Name = Loc.T("Turbo"), Kind = "turbo", Amount = 60, Weight = 14 },
            new WheelPrize { Name = Loc.T("Cofre"), Kind = "chest", Amount = 0, Weight = 14 },
            new WheelPrize { Name = Loc.T("¡Montaña!"), Kind = "coins", Amount = 180, Weight = 10 },
            new WheelPrize { Name = Loc.T("Veta de Oro"), Kind = "giant", Amount = 0, Weight = 10 },
            new WheelPrize { Name = Loc.T("5 gemas"), Kind = "gems", Amount = 5, Weight = 7 },
            new WheelPrize { Name = Loc.T("Cofre de oro"), Kind = "chest", Amount = 2, Weight = 5 },
        };

        /// <summary>Ingreso estimado por segundo (mineros + mina), base de los premios en monedas.</summary>
        public double IncomePerSec()
        {
            double perMiner = Ores[Math.Max(0, BestKind() - 1)].Value * CarryCap() * PriceMult() / 14.0;
            return Math.Max(1.0, perMiner * Miners.Count + MineRate());
        }

        /// <summary>Monedas que vale un premio de "segundos de ingreso" (con piso para el arranque).</summary>
        public double CoinPrize(double seconds) { return Math.Round(Math.Max(20 + seconds, IncomePerSec() * seconds)); }

        // ------------------------------------------------------------ globo del mercader
        void TickBalloon(float dt)
        {
            if (BalloonHere)
            {
                BalloonLeft -= dt;
                if (BalloonLeft <= 0f) { BalloonHere = false; balloonT = NextBalloon(); BalloonGone?.Invoke(false); }
                return;
            }
            if (TotalEarned < 25) return;
            balloonT -= dt;
            if (balloonT > 0f) return;
            BalloonHere = true;
            BalloonLeft = BalloonStay;
            BalloonCame?.Invoke();
        }

        float NextBalloon() { return (float)(480 + rng.NextDouble() * 240); }   // cada 8-12 min: escaso, se espera

        /// <summary>El jugador toca el globo: regala un giro y se va.</summary>
        public bool TapBalloon()
        {
            if (!BalloonHere) return false;
            BalloonHere = false;
            balloonT = NextBalloon();
            Spins++;
            AddStat("balloons", 1);
            BalloonGone?.Invoke(true);
            return true;
        }

        /// <summary>Para pruebas y capturas: el globo llega ya.</summary>
        public void BalloonSoon() { if (!BalloonHere) balloonT = 0f; }

        // ------------------------------------------------------------ ruleta
        /// <summary>Elige el premio (sin cobrarlo: la vista gira la ruleta y despues llama a ClaimSpin).</summary>
        public int Spin()
        {
            if (Spins <= 0) return -1;
            if (PendingPrize >= 0) return PendingPrize;
            double sum = 0; foreach (var w in Wheel) sum += w.Weight;
            double r = rng.NextDouble() * sum;
            int idx = Wheel.Length - 1;
            for (int i = 0; i < Wheel.Length; i++) { r -= Wheel[i].Weight; if (r <= 0) { idx = i; break; } }
            if (SpinStreak >= JackpotEvery - 1)
            {
                // giro del premio mayor: uno de los grandes, al azar
                int[] big = Giant != null ? new[] { 6, 7 } : new[] { 5, 6, 7 };
                idx = big[rng.Next(big.Length)];
            }
            // si ya hay una veta gigante, la de oro se cambia por la montaña de monedas
            if (Wheel[idx].Kind == "giant" && Giant != null) idx = 4;
            PendingPrize = idx;
            return idx;
        }

        /// <summary>Cobra el premio elegido. Devuelve las monedas dadas (0 si fue otra cosa).</summary>
        public double ClaimSpin()
        {
            if (PendingPrize < 0 || Spins <= 0) return 0;
            var w = Wheel[PendingPrize];
            PendingPrize = -1;
            Spins--;
            AddStat("spins", 1);
            SpinStreak = IsBigPrize(w) ? 0 : SpinStreak + 1;
            switch (w.Kind)
            {
                case "coins": { double v = CoinPrize(w.Amount); Earn(v); return v; }
                case "gems": Gems += (int)w.Amount; break;
                case "turbo": TurboT += (float)w.Amount; break;
                case "chest": GiveChest((int)w.Amount); break;
                case "giant": SpawnGiant(true); break;
            }
            return 0;
        }

        public static bool IsBigPrize(WheelPrize w) { return w.Kind == "giant" || (w.Kind == "chest" && w.Amount >= 2) || (w.Kind == "gems" && w.Amount >= 5); }

        // ------------------------------------------------------------ cofres
        public void GiveChest(int tier)
        {
            tier = Math.Max(0, Math.Min(3, tier));
            Chests[tier]++;
            ChestGot?.Invoke(tier);
        }

        public int ChestCount { get { int n = 0; foreach (var c in Chests) n += c; return n; } }

        /// <summary>Abre el mejor cofre que haya, sin intentos de mejora. Devuelve null si no hay.</summary>
        public ChestLoot OpenChest()
        {
            if (BeginChest() < 0) return null;
            return FinishChest();
        }

        // ------------------------------------------------------------ evolucion de edificios
        /// <summary>Etapa visual de un edificio segun su nivel (cambia de forma en 3, 6 y 9).</summary>
        public static int Tier(int level) { return level >= 9 ? 4 : level >= 6 ? 3 : level >= 3 ? 2 : 1; }
    }
}
