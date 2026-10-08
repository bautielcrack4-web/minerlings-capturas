using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Formulas de economia: costos, valores de mejoras, vida/oro de rocas, hitos de nivel e ingreso esperado.</summary>
    public sealed partial class GameState
    {
        public int StageIdx() { return (World - 1) * Balance.SubsPerWorld + (Sub - 1); }

        public int Biome() { return (World - 1) % Content.BiomeNames.Length; }

        public string StageName() { return "Etapa " + World + "-" + Sub; }

        public string StageLabel(int idx)
        {
            return (idx / Balance.SubsPerWorld + 1) + "-" + (idx % Balance.SubsPerWorld + 1);
        }

        public int EssenceLevel(string id)
        {
            int v;
            return EssenceLv.TryGetValue(id, out v) ? v : 0;
        }

        /// <summary>Aporte informativo de la Esencia (media geometrica de dano y oro). No multiplica nada por si solo.</summary>
        public double RebirthMult()
        {
            return Math.Sqrt((1.0 + 0.25 * EssenceLevel("power")) * (1.0 + 0.25 * EssenceLevel("gold")));
        }

        public double PowerValue()
        {
            return Balance.Power0 * Math.Pow(Balance.PowerGrow, Lv["power"] - 1) * MilestoneMult("power")
                * (1.0 + 0.25 * EssenceLevel("power")) * (Turbo ? 2.0 : 1.0);
        }

        public int SpeedValue() { return 60 + Balance.SpeedStep * (Lv["speed"] - 1); }

        /// <summary>Velocidad efectiva relativa (1.0 = nivel 1): nivel, hitos, Esencia y evento Frenesi.</summary>
        public double SpeedMultTotal()
        {
            return (SpeedValue() / 60.0) * MilestoneMult("speed") * (1.0 + 0.05 * EssenceLevel("speed")) * EventSpeedMult();
        }

        public double MoneyMult()
        {
            return Math.Pow(Balance.MoneyGrow, Lv["money"] - 1) * MilestoneMult("money")
                * (1.0 + 0.25 * EssenceLevel("gold")) * (Turbo ? 2.0 : 1.0) * (Skin == 5 ? 1.1 : 1.0);
        }

        /// <summary>Costo de la proxima mejora de `kind` ("power", "speed" o "money").</summary>
        public double Cost(string kind)
        {
            int l = Lv[kind] - 1;
            switch (kind)
            {
                case "power": return Balance.CostPower0 * Math.Pow(Balance.CostPowerGrow, l);
                case "speed": return Balance.CostSpeed0 * Math.Pow(Balance.CostSpeedGrow, l);
                default: return Balance.CostMoney0 * Math.Pow(Balance.CostMoneyGrow, l);
            }
        }

        public bool CanBuy(string kind)
        {
            if (kind == "speed" && Lv["speed"] >= Balance.SpeedCap) return false;
            return Gold >= Cost(kind);
        }

        public bool Buy(string kind)
        {
            if (!CanBuy(kind)) return false;
            Gold -= Cost(kind);
            Lv[kind] += 1;
            LvBest[kind] = Math.Max(LvBestOf(kind), Lv[kind]);
            Stats["upgrades"] = StatRaw("upgrades") + 1;
            AddDaily("upgrades", 1);
            Upgraded?.Invoke(kind);
            if (IsMilestoneLevel(Lv[kind])) MilestoneReached?.Invoke(kind, Lv[kind]);
            CheckUnlocks();
            Changed?.Invoke();
            StatsChanged?.Invoke();
            return true;
        }

        int LvBestOf(string kind)
        {
            int v;
            return LvBest.TryGetValue(kind, out v) ? v : 1;
        }

        void AutoBuy()
        {
            string best = "";
            double bestC = double.PositiveInfinity;
            for (int i = 0; i < Content.UpgradeKinds.Length; i++)
            {
                string k = Content.UpgradeKinds[i];
                if (k == "speed" && Lv["speed"] >= Balance.SpeedCap) continue;
                double c = Cost(k);
                if (c < bestC)
                {
                    bestC = c;
                    best = k;
                }
            }
            if (best != "" && Gold >= bestC) Buy(best);
        }

        readonly List<double> hpTable = new List<double> { Balance.Hp0 };

        /// <summary>Vida base de una roca en la etapa `s` (indice; -1 = la actual). Crece rapido al principio y luego se estabiliza.</summary>
        public double RockHp(int s = -1)
        {
            if (s < 0) s = StageIdx();
            while (hpTable.Count <= s)
            {
                int i = hpTable.Count - 1;
                double gr = Math.Min(Balance.HpGMax, Balance.HpG0 * Math.Pow(Balance.HpQ, i));
                if (i >= Balance.HpDecayStart)
                    gr = Math.Max(Balance.HpGMin, gr * Math.Pow(Balance.HpDecay, i - Balance.HpDecayStart + 1));
                hpTable.Add(hpTable[i] * gr);
            }
            return hpTable[s];
        }

        public double RockBaseGold(int s = -1)
        {
            if (s < 0) s = StageIdx();
            return Balance.Gold0 * Math.Pow(Balance.GoldGrow, s);
        }

        public double RockGold() { return RockBaseGold() * MoneyMult(); }

        public bool BoostActive() { return Now() < BoostUntil; }

        /// <summary>Multiplicador de oro del momento: x3 Oro (boost) por evento Fiebre de Oro.</summary>
        public double GoldMultNow() { return (BoostActive() ? 3.0 : 1.0) * EventGoldMult(); }

        // ---------------------------------------------------------------- herramientas (formulas)
        public double ToolPower(Tool t)
        {
            double b = t.K == 0 ? 100.0 : 80.0;
            return b * Math.Pow(2.2, t.R);
        }

        public double ToolInterval(Tool t) { return ToolBaseInterval(t) / SpeedMultTotal(); }

        public double ToolBaseInterval(Tool t) { return t.K == 0 ? 0.6 : 1.0; }

        public double ToolDamage(Tool t) { return PowerValue() * ToolPower(t) / 100.0; }

        public double CritChance() { return Balance.CritBase + 0.015 * EssenceLevel("crit"); }

        /// <summary>Multiplicador de dano critico (base 2.5, +25% por nivel de Golpe brutal).</summary>
        public double CritMult() { return Balance.CritMultBase * (1.0 + 0.25 * EssenceLevel("brutal")); }

        public int RocksNeeded()
        {
            return Balance.RocksBase + Sub * Balance.RocksPerSub + Balance.RocksPerWorld * (World - 1);
        }

        public double MinersDps()
        {
            double d = 0.0;
            for (int i = 0; i < Equip.Length; i++)
            {
                Tool t = Equip[i];
                if (t != null) d += ToolDamage(t) / ToolInterval(t);
            }
            return d;
        }

        /// <summary>Oro/s esperado (misma formula que el simulador): rocas/s por minero * oro medio por roca.</summary>
        public double IncomePerSec()
        {
            double avgCrit = 1.0 + CritChance() * (CritMult() - 1.0);
            double rps = 0.0;
            for (int i = 0; i < Equip.Length; i++)
            {
                Tool t = Equip[i];
                if (t == null) continue;
                double dmg = ToolDamage(t) * avgCrit;
                double iv = ToolInterval(t);
                double tt = 0.0;
                tt += (1.0 - Balance.RichChance) * (Math.Ceiling(RockHp() / dmg) * iv + Balance.WalkOverhead);
                tt += Balance.RichChance * (Math.Ceiling(RockHp() * Balance.RichHpMult / dmg) * iv + Balance.WalkOverhead);
                rps += (t.K == 1 ? Balance.MazoAoe : 1.0) / tt;
            }
            double goldPerRock = RockGold() * ((1.0 - Balance.RichChance) + Balance.RichChance * Balance.RichGoldMult);
            return rps * goldPerRock;
        }

        // ---------------------------------------------------------------- hitos de nivel
        /// <summary>Hitos: 25, 50, 75, 100, 150, 200, 250, 300 y luego cada 50.</summary>
        public int MilestoneCount(int level)
        {
            if (level < 25) return 0;
            if (level < 100) return level / 25;
            return 4 + (level - 100) / 50;
        }

        public int MilestoneLevel(int n)
        {
            if (n <= 0) return 0;
            if (n <= 4) return 25 * n;
            return 100 + 50 * (n - 4);
        }

        public bool IsMilestoneLevel(int level)
        {
            return level >= 25 && MilestoneCount(level) > MilestoneCount(level - 1);
        }

        /// <summary>"power" y "money" x2 por hito, "speed" x1.25 por hito (acumulativo).</summary>
        public double MilestoneMult(string kind)
        {
            int n = MilestoneCount(Lv[kind]);
            return kind == "speed" ? Math.Pow(1.25, n) : Math.Pow(2.0, n);
        }

        /// <summary>Proximo nivel hito de `kind`; 0 si ya no hay (Velocidad tiene tope 200).</summary>
        public int NextMilestone(string kind)
        {
            int nxt = MilestoneLevel(MilestoneCount(Lv[kind]) + 1);
            if (kind == "speed" && nxt > Balance.SpeedCap) return 0;
            return nxt;
        }
    }
}
