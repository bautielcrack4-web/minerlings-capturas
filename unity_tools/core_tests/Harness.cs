using System;
using System.Collections.Generic;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Reloj controlado por las pruebas.</summary>
    public sealed class FakeClock : IClock
    {
        public double Time = 1.8e9;
        public string Date = "2026-01-15";
        public double Now() { return Time; }
        public string Today() { return Date; }
    }

    /// <summary>Contador de comprobaciones PASS/FAIL.</summary>
    public static class T
    {
        public static int Total, Fails;
        public static bool Quiet;

        public static void Check(string name, bool cond, string extra = "")
        {
            Total++;
            if (cond)
            {
                if (!Quiet) Console.WriteLine("PASS  " + name);
            }
            else
            {
                Fails++;
                Console.WriteLine("FAIL  " + name + "  " + extra);
            }
        }

        public static bool Approx(double a, double b, double eps) { return Math.Abs(a - b) < eps; }

        public static bool Eq(List<string> a, params string[] b)
        {
            if (a.Count != b.Length) return false;
            for (int i = 0; i < b.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public static string Str(IEnumerable<string> l) { return "[" + string.Join(",", l) + "]"; }
    }

    /// <summary>Contexto de juego equivalente al autoload G de Godot (uno compartido por las pruebas).</summary>
    public sealed class Ctx
    {
        public FakeClock Clock = new FakeClock();
        public MemorySaveStore Store = new MemorySaveStore();
        public GameState G;

        public Ctx(int seed = 12345)
        {
            G = new GameState(Clock, new Random(seed), Store);
            G.Start();
        }

        public void Fresh()
        {
            G.EventsEnabled = false;
            G.ResetAll();
            G.EventsEnabled = true;
            G.EvNext = 99999.0;
        }

        public void ClearStage()
        {
            int guard = 0;
            int before = G.StageIdx();
            while (G.StageIdx() == before && guard < 200)
            {
                G.OnRockBroken(G.NeedBoss);
                guard++;
            }
        }

        public void BuyTo(string kind, int level)
        {
            G.Gold = 1.0e30;
            while (G.Lv[kind] < level) G.Buy(kind);
        }
    }
}
