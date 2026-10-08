namespace Mineros.Core
{
    /// <summary>Constantes de balance (espejo de game_state.gd y tools/econ_sim.py). No cambiar sin re-simular.</summary>
    public static class Balance
    {
        public const int InvSize = 16;
        public const int EquipSlots = 3;
        public const int SubsPerWorld = 5;
        public const double OfflineCap = 8.0 * 3600.0;
        public const double OfflineEff = 0.5;
        public const double BoostTime = 60.0;
        public const double BoostCd = 240.0;
        public const int PiggyCap = 60;

        // vida / oro de rocas
        public const double Hp0 = 40.37;
        public const double HpG0 = 1.2643;      // crecimiento de vida entre etapas: min(HpGMax, HpG0 * HpQ^etapa)
        public const double HpQ = 1.2326;
        public const double HpGMax = 2.7153;
        public const int HpDecayStart = 16;     // desde esta etapa el crecimiento decae (muro suave)
        public const double HpDecay = 0.97;
        public const double HpGMin = 1.7;
        public const double Gold0 = 3.76;
        public const double GoldGrow = 1.1924;

        // mejoras
        public const double Power0 = 10.0;
        public const double PowerGrow = 1.0568;
        public const int SpeedStep = 3;
        public const int SpeedCap = 200;
        public const double MoneyGrow = 1.0775;
        public const double CostPower0 = 3.65, CostPowerGrow = 1.2820;
        public const double CostSpeed0 = 25.0, CostSpeedGrow = 1.080;
        public const double CostMoney0 = 37.33, CostMoneyGrow = 1.1062;

        // etapas / jefes / rocas ricas
        public const int RocksBase = 40;
        public const int RocksPerSub = 0;
        public const int RocksPerWorld = 7;
        public const double BossHpMult = 14.0;
        public const double BossGoldMult = 12.0;
        public const double RichChance = 0.12;
        public const double RichHpMult = 1.5;
        public const double RichGoldMult = 4.0;
        public const double WalkOverhead = 0.70;   // s por roca entre golpes
        public const double MazoAoe = 1.50;        // rendimiento extra del Mazo
        public const double CritBase = 0.10;
        public const double CritMultBase = 2.5;

        // esencia y gemas
        public const double EssenceK = 0.125;
        public const double EssenceP = 1.6;
        public const int BossGemsBase = 5;
        public const int ChestGemsBase = 20;

        // eventos
        public const double EventFirst = 120.0;
        public const double EventMin = 180.0;
        public const double EventMax = 300.0;
    }
}
