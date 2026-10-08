using System;
using System.Collections.Generic;
using System.Text;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Vuelca una tabla de valores (mismas claves que el script GDScript de comparacion) para diffear contra Godot.</summary>
    public static class Dump
    {
        static GameState g;

        static string Hex(double v)
        {
            byte[] b = BitConverter.GetBytes(v);
            if (!BitConverter.IsLittleEndian) Array.Reverse(b);
            StringBuilder sb = new StringBuilder();
            foreach (byte x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        static void Emit(string key, object v)
        {
            if (v is int || v is long) Console.WriteLine(key + "\ti:" + v);
            else if (v is bool) Console.WriteLine(key + "\ti:" + ((bool)v ? 1 : 0));
            else if (v is double) Console.WriteLine(key + "\td:" + Hex((double)v));
            else Console.WriteLine(key + "\ts:" + v);
        }

        static void SetStage(int s)
        {
            g.World = s / 5 + 1;
            g.Sub = s % 5 + 1;
        }

        static void ResetLv()
        {
            g.Lv = new Dictionary<string, int> { { "power", 1 }, { "speed", 1 }, { "money", 1 } };
            g.EssenceLv = new Dictionary<string, int>();
            g.Turbo = false;
            g.Skin = 0;
            g.EvKind = "";
            g.World = 1;
            g.Sub = 1;
        }

        static Tool Tl(int k, int r) { return new Tool(k, r); }

        public static void Run()
        {
            Ctx c = new Ctx();
            g = c.G;
            g.EventsEnabled = false;
            int[] levels = { 1, 10, 25, 50, 99, 100, 101, 150, 200, 300 };
            foreach (int L in levels)
            {
                foreach (string kind in new[] { "power", "speed", "money" })
                {
                    if (kind == "speed" && L > 200) continue;
                    ResetLv();
                    g.Lv[kind] = L;
                    Emit("cost." + kind + "." + L, g.Cost(kind));
                    Emit("milestone_mult." + kind + "." + L, g.MilestoneMult(kind));
                    Emit("next_milestone." + kind + "." + L, g.NextMilestone(kind));
                    Emit("milestone_count." + L, g.MilestoneCount(L));
                    Emit("is_milestone_level." + L, g.IsMilestoneLevel(L));
                }
                ResetLv();
                g.Lv["power"] = L;
                Emit("power_value." + L, g.PowerValue());
                ResetLv();
                g.Lv["speed"] = Math.Min(L, 200);
                Emit("speed_value." + L, g.SpeedValue());
                Emit("speed_mult_total." + L, g.SpeedMultTotal());
                Emit("tool_interval.pico." + L, g.ToolInterval(Tl(0, 0)));
                Emit("tool_interval.mazo3." + L, g.ToolInterval(Tl(1, 3)));
                ResetLv();
                g.Lv["money"] = L;
                Emit("money_mult." + L, g.MoneyMult());
            }
            // mezcla: esencia + turbo + casco
            ResetLv();
            g.Lv = new Dictionary<string, int> { { "power", 50 }, { "speed", 60 }, { "money", 75 } };
            g.EssenceLv = new Dictionary<string, int> { { "power", 3 }, { "gold", 2 }, { "speed", 4 }, { "crit", 2 }, { "brutal", 3 }, { "night", 2 }, { "boss", 4 }, { "chest", 3 }, { "luck", 1 } };
            g.Turbo = true;
            g.Skin = 5;
            Emit("mix.power_value", g.PowerValue());
            Emit("mix.money_mult", g.MoneyMult());
            Emit("mix.speed_mult_total", g.SpeedMultTotal());
            Emit("mix.crit_chance", g.CritChance());
            Emit("mix.crit_mult", g.CritMult());
            Emit("mix.rebirth_mult", g.RebirthMult());
            Emit("mix.offline_cap", g.OfflineCap());
            Emit("mix.offline_eff", g.OfflineEff());
            Emit("mix.boss_gems", g.BossGems());
            Emit("mix.miners_dps", g.MinersDps());
            Emit("mix.income", g.IncomePerSec());
            foreach (int s in new[] { 0, 1, 2, 5, 10, 15, 16, 17, 20, 30, 40, 100, 300 })
            {
                Emit("rock_hp." + s, g.RockHp(s));
                Emit("rock_base_gold." + s, g.RockBaseGold(s));
            }
            ResetLv();
            g.Lv["money"] = 25;
            foreach (int s in new[] { 0, 5, 10, 20, 45 })
            {
                SetStage(s);
                Emit("rock_gold." + s, g.RockGold());
                Emit("rocks_needed." + s, g.RocksNeeded());
                Emit("biome." + s, g.Biome());
                Emit("stage_name." + s, g.StageName());
                Emit("stage_label." + s, g.StageLabel(s));
            }
            foreach (int s in new[] { 0, 5, 10, 20, 45 })
            {
                foreach (int L in new[] { 1, 25, 100 })
                {
                    ResetLv();
                    g.Lv = new Dictionary<string, int> { { "power", L }, { "speed", Math.Min(L, 200) }, { "money", L } };
                    SetStage(s);
                    Emit("income." + s + "." + L, g.IncomePerSec());
                    Emit("dps." + s + "." + L, g.MinersDps());
                }
            }
            ResetLv();
            g.Equip[2] = Tl(1, 3);
            g.Lv = new Dictionary<string, int> { { "power", 40 }, { "speed", 30 }, { "money", 20 } };
            SetStage(12);
            Emit("income.3tools", g.IncomePerSec());
            g.Equip[2] = null;
            // esencia
            ResetLv();
            foreach (int rm in new[] { 0, 1, 2, 5, 10, 15, 20, 25, 45, 100, 200, 500 })
            {
                g.RunMax = rm;
                Emit("essence_gain." + rm, g.EssenceGainPreview());
            }
            foreach (EssenceNode n in Content.EssenceNodes)
            {
                for (int l = 0; l <= n.Max; l++)
                {
                    g.EssenceLv = new Dictionary<string, int> { { n.Id, l } };
                    Emit("essence_cost." + n.Id + "." + l, g.EssenceCost(n.Id));
                }
            }
            ResetLv();
            foreach (int l in new[] { 0, 1, 5, 10, 25 })
            {
                g.EssenceLv = new Dictionary<string, int> { { "chest", l } };
                g.Gems = 0;
                g.EvKind = "";
                Emit("chest_gems." + l, g.ChestOpened());
            }
            ResetLv();
            g.Gems = 0;
            for (int k = 0; k < 2; k++)
            {
                for (int r = 0; r < 6; r++)
                {
                    Emit("tool_power." + k + "." + r, g.ToolPower(Tl(k, r)));
                    Emit("tool_damage." + k + "." + r, g.ToolDamage(Tl(k, r)));
                }
            }
            double[] fv = { 0.0, 0.04, 0.5, 1.0, 1.04, 1.06, 9.97, 9.5, 10.0, 42.0, 999.0, 999.9, 1000.0, 1234.0, 9995.0, 12345.0, 99999.0, 100000.0, 123456.0, 999994.0, 999995.0, 1.0e6, 2.5e6, 4.56e7, 7.89e9, 1.0e12, 1.5e15, 2.5e18, 1.0e33, 1.0e100, -1234.0, -0.5, 123456789.0, 1.0e9, 5.0e5, 0.05, 0.051, 0.95, 9.05, 9.95, 3.14159, 2.71828e3, 987654321.0, 1.0e15, 1.0e21, 3.0e30, 1.0e60, 9.99e299, 0.001, 8.5, 1999.0, 1999.99 };
            for (int i = 0; i < fv.Length; i++)
            {
                Emit("fmt_in." + i, fv[i]);
                Emit("fmt." + i, BigNum.Fmt(fv[i]));
            }
            foreach (double t in new[] { 0.0, 5.0, 59.0, 60.0, 61.5, 3599.0, 3600.0, 7384.0, 86399.0, 90000.0, -5.0 })
                Emit("hms." + Hex(t), BigNum.TimeHms(t));
        }
    }
}
