using System;
using System.Linq;
using System.Threading.Tasks;
using Fortin.Core;

namespace DefensaTests
{
    /// <summary>Balance con bots (seccion 13): metas de victoria en el primer intento.</summary>
    public static class Balance
    {
        public static float WinRate(Func<int, Bot> bot, int ch, int lv, int[] deck, int[] perm, int n, int seed0 = 1000, bool revive = false)
        {
            int wins = 0;
            Parallel.For(0, n, i =>
            {
                var b = new Battle(ch, lv, deck, perm, seed0 + i * 7919);
                if (bot(i).Play(b, revive)) System.Threading.Interlocked.Increment(ref wins);
            });
            return wins / (float)n;
        }

        static int[] Perm(int ch) { int p = Program.PermFor[ch - 1]; return new[] { p, p, p, p, p }; }

        public static float Good(int ch, int lv, int n) { return WinRate(i => new GoodBot(), ch, lv, Program.DeckFor(ch), Perm(ch), n); }
        public static float Novice(int ch, int lv, int n) { return WinRate(i => new NoviceBot(i), ch, lv, Program.DeckFor(ch), Perm(ch), n); }

        /// <summary>Lo que corre en cada build: las metas de la especificacion (con muestras chicas).</summary>
        public static void Gates(Action<bool, string> ok)
        {
            float g11 = Good(1, 1, 80);
            Console.WriteLine("  bot bueno 1-1: " + g11.ToString("P0"));
            ok(g11 >= 0.95f, "bot bueno gana el 1-1 al 95% (" + g11.ToString("P0") + ")");
            for (int ch = 1; ch <= 8; ch++)
            {
                float g = Good(ch, 10, 40);
                float goal = ch >= 7 ? 0.55f : 0.70f;
                Console.WriteLine("  bot bueno " + ch + "-10 (jefe): " + g.ToString("P0") + " meta " + goal.ToString("P0"));
                ok(g >= goal - 0.12f && g <= 0.97f, "bot bueno en el jefe del capitulo " + ch + " ~" + goal.ToString("P0") + " (" + g.ToString("P0") + ")");
            }
            for (int lv = 1; lv <= 10; lv++)
            {
                float nv = WinRate(i => new NoviceBot(i), 1, lv, Program.StartDeck, new[] { 1 + lv / 5, 1 + lv / 5, 1 + lv / 5, 1 + lv / 5, 1 + lv / 5 }, 30, 5000, true);
                Console.WriteLine("  novato 1-" + lv + ": " + nv.ToString("P0"));
                ok(nv >= 0.25f, "el novato puede pasar el 1-" + lv + " (" + nv.ToString("P0") + ")");
            }
        }

        public static void Chapter(int ch, int n)
        {
            for (int lv = 1; lv <= 10; lv++)
                Console.WriteLine(ch + "-" + lv + ": bueno " + Good(ch, lv, n).ToString("P0") + "  novato " + Novice(ch, lv, n).ToString("P0"));
        }

        public static void Table(int n)
        {
            for (int ch = 1; ch <= 8; ch++)
            {
                Console.Write("cap " + ch + ":");
                foreach (int lv in new[] { 1, 5, 9, 10 })
                    Console.Write("  " + ch + "-" + lv + " bueno " + Good(ch, lv, n).ToString("P0") + " novato " + Novice(ch, lv, n / 2).ToString("P0"));
                Console.WriteLine();
            }
        }

        /// <summary>Capitulo 1: vida global y pendiente por nivel para 1-1 ~97 % y 1-10 ~70 % (bot bueno).</summary>
        public static void TuneCh1()
        {
            for (int round = 0; round < 3; round++)
            {
                float lo = 0.2f, hi = 6f;
                for (int it = 0; it < 12; it++)
                {
                    Waves.GlobalHp = (float)Math.Sqrt(lo * hi);
                    if (Good(1, 1, 120) > 0.975f) lo = Waves.GlobalHp; else hi = Waves.GlobalHp;
                }
                Waves.GlobalHp = (float)Math.Sqrt(lo * hi);
                float a = 0f, b = 0.4f;
                for (int it = 0; it < 12; it++)
                {
                    Waves.LevelSlope = (a + b) / 2;
                    if (Good(1, 10, 120) > 0.70f) a = Waves.LevelSlope; else b = Waves.LevelSlope;
                }
                Waves.LevelSlope = (a + b) / 2;
                Console.WriteLine("ronda " + round + ": GlobalHp " + Waves.GlobalHp.ToString("0.000") + " LevelSlope " + Waves.LevelSlope.ToString("0.000")
                    + " -> 1-1 " + Good(1, 1, 200).ToString("P0") + " 1-5 " + Good(1, 5, 200).ToString("P0") + " 1-10 " + Good(1, 10, 200).ToString("P0"));
            }
        }

        /// <summary>Busca el multiplicador de vida por capitulo para que el bot bueno gane ~70 % (55 % en 7 y 8) el nivel 10.</summary>
        public static void Tune(int from = 2, int to = 8)
        {
            for (int ch = from; ch <= to; ch++)
            {
                float goal = ch >= 7 ? 0.55f : 0.70f;
                float lo = 0.5f, hi = 12f;
                for (int it = 0; it < 12; it++)
                {
                    float mid = (float)Math.Sqrt(lo * hi);
                    Waves.ChapterHp[ch - 1] = mid;
                    float g = Good(ch, 10, 80);
                    if (g > goal) lo = mid; else hi = mid;
                }
                Waves.ChapterHp[ch - 1] = (float)Math.Sqrt(lo * hi);
                Console.WriteLine("cap " + ch + ": ChapterHp = " + Waves.ChapterHp[ch - 1].ToString("0.00") + " -> bueno " + Good(ch, 10, 120).ToString("P0"));
            }
            Console.WriteLine("ChapterHp = { " + string.Join(", ", Waves.ChapterHp.Select(x => x.ToString("0.0") + "f")) + " }");
        }
    }

    /// <summary>Suites de Meta y Save (se agregan en F2).</summary>
    public static partial class Suites
    {
        public static void Run(Action<bool, string> ok) { RunMeta(ok); }
        static partial void RunMeta(Action<bool, string> ok);
    }
}
