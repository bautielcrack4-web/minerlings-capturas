using System;
using System.Linq;
using Fortin.Core;

namespace DefensaTests
{
    public static class Diag
    {
        public static void Run(string botName, int ch, int lv, int seed)
        {
            var b = new Battle(ch, lv, Program.DeckFor(ch), Enumerable.Repeat(Program.PermFor[ch - 1], 5).ToArray(), seed);
            Bot bot = botName == "novato" ? (Bot)new NoviceBot(seed) : new GoodBot();
            bot.B = b;
            int lastWave = 0; float think = 0;
            while (b.Time < 1500 && b.State != BState.Won && b.State != BState.Lost)
            {
                think -= Battle.Dt;
                if (think <= 0 || b.State == BState.Cards) { bot.Decide(); think = 0.5f; }
                b.Step();
                foreach (var e in b.Events)
                    if (e.Type == Ev.Leak) Console.WriteLine("   t=" + b.Time.ToString("0") + " se escapa " + Defs.Enemies[e.C].Id + " hp " + "vidas " + b.Lives);
                b.Events.Clear();
                if (b.WaveIndex != lastWave)
                {
                    lastWave = b.WaveIndex;
                    Console.WriteLine("ola " + b.WaveIndex + " t=" + b.Time.ToString("0") + " chispas " + b.Sparks + " costo " + b.SummonCost + " vidas " + b.Lives + " xp " + b.XpLevel
                        + " rangos " + string.Join(",", b.Rank) + " | " + string.Join(" ", b.Cells.Select(u => u.Empty ? "." : Defs.Heroes[u.Hero].Id.Substring(0, 3) + u.Level)));
                }
            }
            Console.WriteLine("FIN " + b.State + " ola " + b.WaveIndex + " vidas " + b.Lives + " t=" + b.Time.ToString("0") + " | " + string.Join(" ", b.Cells.Select(u => u.Empty ? "." : Defs.Heroes[u.Hero].Id.Substring(0, 3) + u.Level)));
        }
    }
}
