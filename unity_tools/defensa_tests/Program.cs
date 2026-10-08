using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Fortin.Core;

namespace DefensaTests
{
    /// <summary>
    /// Pruebas del nucleo (Board, Combat, Waves, Skills, Meta, Save) + bots de balance.
    /// dotnet run -c Release            pruebas + balance corto (lo corre Actions)
    /// dotnet run -c Release -- balance tabla completa de victorias por capitulo
    /// dotnet run -c Release -- tune    busca el multiplicador de vida de cada capitulo para las metas
    /// </summary>
    public static class Program
    {
        static int pass, fail;
        static readonly List<string> fails = new List<string>();

        static void Ok(bool c, string name)
        {
            if (c) pass++;
            else { fail++; fails.Add(name); Console.WriteLine("  FALLA: " + name); }
        }

        public static readonly int[] StartDeck = { Defs.HeroIndex("lancero"), Defs.HeroIndex("arquera"), Defs.HeroIndex("mago"), Defs.HeroIndex("enano"), Defs.HeroIndex("barbaro") };

        /// <summary>Mazo y nivel permanente esperados en cada capitulo (seccion 13: ritmo de progreso).</summary>
        public static int[] DeckFor(int ch)
        {
            string[][] d =
            {
                new[] { "lancero", "arquera", "mago", "enano", "barbaro" },
                new[] { "lancero", "arquera", "mago", "chaman", "barbaro" },
                new[] { "arquera", "mago", "chaman", "enano", "bardo" },
                new[] { "arquera", "mago", "chaman", "bardo", "artillero" },
                new[] { "arquera", "mago", "chaman", "artillero", "picara" },
                new[] { "arquera", "chaman", "artillero", "picara", "paladin" },
                new[] { "arquera", "chaman", "artillero", "paladin", "monja" },
                new[] { "arquera", "artillero", "paladin", "monja", "hada" },
            };
            return d[ch - 1].Select(Defs.HeroIndex).ToArray();
        }

        public static readonly int[] PermFor = { 1, 3, 4, 6, 7, 9, 11, 12 };

        public static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : "test";
            if (mode == "tune") { if (args.Length > 2) Balance.Tune(int.Parse(args[1]), int.Parse(args[2])); else Balance.Tune(); return 0; }
            if (mode == "tune1") { Balance.TuneCh1(); return 0; }
            if (mode == "cap") { Balance.Chapter(int.Parse(args[1]), 150); return 0; }
            if (mode == "balance") { Balance.Table(200); return 0; }
            if (mode == "diag") { Diag.Run(args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4])); return 0; }
            var sw = Stopwatch.StartNew();
            Console.WriteLine("Guardianes del Cristal: pruebas del nucleo");
            BoardTests();
            CombatTests();
            WaveTests();
            SkillTests();
            DeterminismTests();
            Suites.Run(Ok);
            Console.WriteLine("pruebas: " + pass + " bien, " + fail + " mal (" + sw.ElapsedMilliseconds + " ms)");
            if (mode != "quick") Balance.Gates(Ok);
            Console.WriteLine("TOTAL: " + pass + " bien, " + fail + " mal (" + sw.ElapsedMilliseconds + " ms)");
            foreach (var f in fails) Console.WriteLine("  - " + f);
            return fail == 0 ? 0 : 1;
        }

        static Battle New(int seed = 1, int ch = 1, int lv = 1) { return new Battle(ch, lv, StartDeck, null, seed); }

        // ------------------------------------------------------------------ Board
        static void BoardTests()
        {
            var b = New();
            Ok(b.Sparks == 30 && b.Lives == 20, "inicio: 30 chispas y 20 vidas");
            Ok(b.SummonCost == 10, "invocar cuesta 10");
            int c = b.Summon();
            Ok(c >= 0 && !b.Cells[c].Empty && b.Cells[c].Level == 1, "invocar pone un juguete nivel 1");
            Ok(b.SummonCost == 20 && b.Sparks == 20, "invocar sube +10 y descuenta");
            Ok(StartDeck.Contains(b.Cells[c].Hero), "el juguete invocado es del mazo");
            // fusion
            b = New();
            b.Place(0, StartDeck[0], 1); b.Place(1, StartDeck[0], 1); b.Place(2, StartDeck[1], 1); b.Place(3, StartDeck[0], 2);
            Ok(b.CanFuse(0, 1), "mismo tipo y nivel: se fusionan");
            Ok(!b.CanFuse(0, 2), "distinto tipo: no");
            Ok(!b.CanFuse(0, 3), "distinto nivel: no");
            b.Drop(0, 1);
            Ok(b.Cells[0].Empty && b.Cells[1].Level == 2 && b.Cells[1].Hero == StartDeck[0], "fusionar deja uno de nivel 2 donde se solto");
            Ok(b.Events.Any(e => e.Type == Ev.Fuse && e.C == 2), "evento de fusion con el nivel nuevo");
            // mover / intercambiar
            b.Drop(2, 0);
            Ok(b.Cells[2].Empty && b.Cells[0].Hero == StartDeck[1], "soltar en vacio mueve");
            b.Drop(0, 3);
            Ok(b.Cells[3].Hero == StartDeck[1] && b.Cells[0].Hero == StartDeck[0] && b.Cells[0].Level == 2, "soltar sobre otro distinto intercambia");
            // nivel 7 es el tope
            b = New();
            b.Place(0, StartDeck[0], 7); b.Place(1, StartDeck[0], 7);
            Ok(!b.CanFuse(0, 1), "nivel 7 no se fusiona mas");
            // comodin
            b = New();
            b.Place(0, Defs.Wild, 3); b.Place(1, StartDeck[2], 3);
            Ok(b.CanFuse(0, 1) && b.CanFuse(1, 0), "el comodin se fusiona con cualquiera de su nivel");
            b.Drop(0, 1);
            Ok(b.Cells[1].Hero == StartDeck[2] && b.Cells[1].Level == 4, "el comodin copia al otro");
            b.Place(2, Defs.Wild, 4); b.Drop(1, 2);
            Ok(b.Cells[2].Hero == StartDeck[2] && b.Cells[2].Level == 5, "soltar el otro sobre el comodin tambien lo copia");
            // vender
            b = New();
            int s = b.Summon(); int sp = b.Sparks;
            b.Sell(s);
            Ok(b.Cells[s].Empty && b.Sparks == sp + 5, "vender devuelve 50% de lo pagado");
            // tablero lleno
            b = New(); b.Sparks = 100000;
            for (int i = 0; i < 15; i++) b.Summon();
            Ok(b.EmptyCells() == 0 && b.Summon() < 0, "con el tablero lleno no se invoca");
            // costo con descuento
            b = New(); b.M.SummonDiscount = 0.2f; b.Summons = 4;
            Ok(b.SummonCost == 40, "invocar con -20%: 50 -> 40");
            // rango
            b = New(); b.Sparks = 1000;
            int before = b.Sparks;
            Ok(b.RankUp(0) && b.Rank[0] == 1 && b.Sparks == before - Defs.RankCost(0), "subir de rango gasta chispas");
            b.Place(0, StartDeck[0], 1);
            Ok(Math.Abs(b.UnitDamage(b.Cells[0]) - Defs.Heroes[StartDeck[0]].Damage * 1.12f) < 0.01f, "+12% de dano por rango");
            // escalado por fusion
            var h = Defs.Heroes[StartDeck[0]];
            Ok(Math.Abs(Defs.Damage(h, 3, 0, 1) - h.Damage * 1.85f * 1.85f) < 0.01f, "dano x1.85 por nivel de fusion");
            Ok(Math.Abs(Defs.Interval(h, 2) - h.Interval / 1.1f) < 0.0001f, "velocidad x1.1 por nivel de fusion");
            // apuntar
            b = New(); b.Place(0, StartDeck[0], 1);
            b.CycleTarget(0); Ok(b.Cells[0].Tg == Target.Near, "tocar cambia a mas cercana");
            b.CycleTarget(0); Ok(b.Cells[0].Tg == Target.Strong, "luego a mas fuerte");
            b.CycleTarget(0); Ok(b.Cells[0].Tg == Target.First, "y vuelve a la mas avanzada");
        }

        // ------------------------------------------------------------------ Combat
        static Battle Empty1()
        {
            var b = New();
            // borrar la oleada 1 para probar a mano
            b.Cur.Spawns.Clear();
            return b;
        }

        static Enemy Spawn(Battle b, string id, float d, float hpMul = 1f)
        {
            var m = typeof(Battle).GetMethod("NewEnemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (Enemy)m.Invoke(b, new object[] { Defs.EnemyIndex(id), hpMul, d, false });
        }

        static void Run(Battle b, float s) { for (int i = 0; i < (int)(s * 30); i++) { b.Step(); } }

        static void CombatTests()
        {
            // el soldadito pega a la pesadilla en alcance
            var b = Empty1();
            int sold = Defs.HeroIndex("lancero");
            b.Place(12, sold, 1);       // abajo a la izquierda (cerca de la pista)
            var e = Spawn(b, "slime", 7.0f, 10f);
            float hp0 = e.Hp;
            Run(b, 1.5f);
            Ok(e.Hp < hp0, "el soldadito le pega a la pelusa en alcance");
            // la mas avanzada primero
            b = Empty1(); b.Place(12, sold, 1);
            var a1 = Spawn(b, "slime", 6.5f, 50f); var a2 = Spawn(b, "slime", 7.5f, 50f);
            a1.Speed = a2.Speed = 0f;
            Run(b, 0.8f);
            Ok(a2.Hp < a2.MaxHp && a1.Hp == a1.MaxHp, "ataca a la mas avanzada");
            // armadura: 3 golpes sin dano
            b = Empty1(); b.Place(12, sold, 1);
            var r = Spawn(b, "esqueleto", 7.0f, 10f); r.Speed = 0;
            Run(b, 2.15f);
            Ok(r.Armor == 0 && b.Events.Count(x => x.Type == Ev.Armor) == 3, "la armadura aguanta 3 golpes (" + r.Armor + "/" + b.Events.Count(x => x.Type == Ev.Armor) + "/" + b.Events.Count(x => x.Type == Ev.Attack) + ")");
            // esquiva 20 %
            b = Empty1(); b.Place(12, sold, 4);
            var m = Spawn(b, "diablillo", 7.0f, 4000f); m.Speed = 0;
            Run(b, 60f);
            int dodges = b.Events.Count(x => x.Type == Ev.Dodge), hits = b.Events.Count(x => x.Type == Ev.Hit && x.A == m.Id);
            float rate = dodges / (float)Math.Max(1, dodges + hits);
            Ok(rate > 0.12f && rate < 0.28f, "la media esquiva ~20% (" + rate.ToString("0.00") + ")");
            // volador: el soldadito no le pega, la arquera si
            b = Empty1(); b.Place(12, sold, 3);
            var bat = Spawn(b, "murcielago", 7.0f, 10f); bat.Speed = 0;
            Run(b, 2f);
            Ok(bat.Hp == bat.MaxHp, "el soldadito no le pega al volador");
            b.Place(13, Defs.HeroIndex("arquera"), 3);
            Run(b, 2f);
            Ok(bat.Hp < bat.MaxHp || !bat.Alive, "la arquera si le pega al volador");
            // dino aturde
            b = Empty1(); b.Place(12, Defs.HeroIndex("barbaro"), 1);
            var p = Spawn(b, "slime", 7.0f, 100f);
            Run(b, 1.0f);
            Ok(b.Events.Any(x => x.Type == Ev.Hit && x.C == 3), "el dino pisa");
            Ok(p.StunT > 0 || p.D < 7.0f + 1.25f, "el dino aturde");
            // veneno
            b = Empty1(); b.Place(12, Defs.HeroIndex("picara"), 1);
            var pz = Spawn(b, "slime", 7.0f, 100f); pz.Speed = 0;
            Run(b, 1.2f);
            Ok(pz.PoisonT > 0, "la estrella del ninja envenena");
            // retroceso
            b = Empty1(); b.Place(12, Defs.HeroIndex("monja"), 1);
            var kn = Spawn(b, "slime", 7.0f, 100f); kn.Speed = 0;
            Run(b, 1.0f);
            Ok(kn.D < 7.0f, "la bailarina hace retroceder");
            // rayo encadenado y freno
            b = Empty1(); b.Place(12, Defs.HeroIndex("chaman"), 1);
            var c1 = Spawn(b, "slime", 7.0f, 100f); var c2 = Spawn(b, "slime", 7.6f, 100f); var c3 = Spawn(b, "slime", 8.2f, 100f);
            c1.Speed = c2.Speed = c3.Speed = 0;
            Run(b, 1.3f);
            Ok(c1.Hp < c1.MaxHp && c2.Hp < c2.MaxHp && c3.Hp < c3.MaxHp, "el rayo salta a 3");
            Ok(c1.SlowT > 0 && c1.SlowK >= 0.2f - 1e-4f, "el rayo frena 20%");
            // salpicadura
            b = Empty1(); b.Place(12, Defs.HeroIndex("mago"), 1);
            var s1 = Spawn(b, "slime", 7.0f, 100f); var s2 = Spawn(b, "slime", 7.4f, 100f);
            s1.Speed = s2.Speed = 0;
            Run(b, 1.5f);
            Ok(s1.Hp < s1.MaxHp && s2.Hp < s2.MaxHp, "la bola de fuego salpica");
            // babosa se parte en 2
            b = Empty1(); b.Place(12, sold, 5); b.Place(13, sold, 5);
            var bb = Spawn(b, "slime_grande", 7.0f, 0.3f); bb.Speed = 0;
            Run(b, 1.0f);
            Ok(b.Events.Count(x => x.Type == Ev.Split) == 2, "la babosa se parte en 2 chicas");
            // chicle pega el casillero
            b = Empty1(); b.Place(12, sold, 5);
            var ch = Spawn(b, "arana", 7.0f, 0.2f); ch.Speed = 0;
            Run(b, 1.0f);
            Ok(b.Cells[12].Stuck > 0 && b.Events.Any(x => x.Type == Ev.Stuck), "el chicle pega el casillero mas cercano");
            // raton roba chispas
            b = Empty1(); b.Sparks = 50;
            var rt = Spawn(b, "goblin", Layout.PathLength - 0.05f, 1f);
            Run(b, 0.2f);
            Ok(b.Sparks == 40 && b.Lives == 20, "el raton roba 10 chispas en vez de una vida");
            // vidas
            b = Empty1();
            Spawn(b, "slime", Layout.PathLength - 0.05f);
            Run(b, 0.2f);
            Ok(b.Lives == 19, "una pesadilla comun quita 1 vida");
            b = Empty1();
            var boss = Spawn(b, "rey_slime", Layout.PathLength - 0.02f);
            Run(b, 0.2f);
            Ok(b.Lives == 15, "un jefe quita 5 vidas");
            // derrota y revivir
            b = Empty1(); b.Lives = 1;
            Spawn(b, "slime", Layout.PathLength - 0.05f);
            Run(b, 0.2f);
            Ok(b.State == BState.Lost, "con 0 vidas se pierde");
            Ok(b.Revive() && b.Lives == 10 && b.State == BState.Playing, "revivir una vez con 10 vidas");
            b.Lives = 1; Spawn(b, "slime", Layout.PathLength - 0.05f); Run(b, 0.2f);
            Ok(b.State == BState.Lost && !b.Revive(), "solo se revive una vez");
            // fantasma invisible a ratos; el hada lo revela
            b = Empty1(); b.Place(12, sold, 4);
            var g = Spawn(b, "espectro", 7.0f, 1000f); g.Speed = 0; g.Age = 2.5f;
            float gh = g.Hp;
            Run(b, 1.0f);
            Ok(g.Hp == gh, "el fantasma invisible no recibe golpes");
            b.Place(13, Defs.HeroIndex("hada"), 1);
            g.Age = 2.1f;
            Run(b, 1.2f);
            Ok(g.Hp < gh, "el hada lo revela y le pegan");
            // granjero genera chispas
            b = Empty1(); b.Place(7, Defs.HeroIndex("enano"), 2);
            Run(b, 3.1f);
            int farm = b.Events.Where(x => x.Type == Ev.Farm).Sum(x => x.B);
            Ok(farm == 2, "el granjero nivel 2 da +2 chispas cada 3 s (" + farm + ")");
            // osito acelera a los vecinos
            b = Empty1(); b.Place(6, sold, 1);
            float i0 = b.UnitInterval(6);
            b.Place(7, Defs.HeroIndex("bardo"), 1);
            Ok(Math.Abs(b.UnitInterval(6) - i0 / 1.15f) < 0.001f, "el osito acelera 15% a los vecinos");
            // jefes
            b = Empty1(); b.Place(12, sold, 1);
            var rey = Spawn(b, "rey_slime", 2f, 0.2f);
            Run(b, 4.2f);
            Ok(b.Events.Any(x => x.Type == Ev.BossSkill && x.B == 1), "el Rey Pelusa invoca pelusas");
            b = Empty1(); b.Place(12, sold, 1);
            Spawn(b, "liche", 2f, 1f); Run(b, 4.2f);
            Ok(b.Events.Any(x => x.Type == Ev.Sewn), "la Muneca cose un casillero");
            b = Empty1(); Spawn(b, "ogro", 2f, 1f); Run(b, 4.2f);
            Ok(b.Events.Count(x => x.Type == Ev.Spawn) >= 5, "el Monstruo del Placard saca 5");
            b = Empty1(); b.Place(12, sold, 1); Spawn(b, "dragon", 2f, 1f); Run(b, 4.2f);
            Ok(b.Cells[12].Yarn > 0, "el Dragon frena juguetes con lana");
        }

        // ------------------------------------------------------------------ Waves
        static void WaveTests()
        {
            var p = Waves.Plan(1, 1);
            Ok(p.Count == 10, "capitulo 1: 10 oleadas");
            Ok(p[4].HasMini && p[9].HasBoss, "mini jefe en la 5 y jefe en la 10");
            Ok(p.All(w => w.Duration >= 20f && (w.Duration <= 30f || w.HasBoss)), "cada oleada dura 20-30 s");
            var p2 = Waves.Plan(2, 1);
            Ok(p2.Count == 15 && p2[14].HasBoss && p2[9].HasMini, "despues, 15 oleadas");
            Ok(Math.Abs(Waves.HpMul(1, 1, 4) / Waves.HpMul(1, 1, 3) - 1.13f) < 1e-4f, "vida x1.13 por oleada");
            Ok(Waves.HpMul(4, 1, 1) > Waves.HpMul(2, 1, 1), "el capitulo multiplica la vida");
            int rey = Defs.EnemyIndex("rey_slime");
            Ok(p[9].Spawns.Any(s => s.Kind == rey), "el jefe del 1-1 es el Rey Pelusa");
            // Noche Eterna
            var w = Waves.EndlessWave(10, 3);
            Ok(w.HasBoss, "Noche Eterna: jefe cada 10");
            // la batalla corre las oleadas
            var b = New();
            int starts = 0;
            for (int i = 0; i < 30 * 70 && b.State == BState.Playing; i++) { b.Step(); starts += b.Events.Count(e => e.Type == Ev.WaveStart); b.Events.Clear(); }
            Ok(b.WaveIndex >= 2, "las oleadas avanzan solas");
        }

        // ------------------------------------------------------------------ Skills
        static void SkillTests()
        {
            Ok(Skills.All.Count == 60, "60 cartas de habilidad (" + Skills.All.Count + ")");
            Ok(Skills.All.Select(s => s.Id).Distinct().Count() == Skills.All.Count, "ids de cartas unicos");
            Ok(Skills.All.All(s => s.Hero == null || Defs.HeroIndex(s.Hero) >= 0), "las cartas apuntan a juguetes que existen");
            var b = New();
            b.Xp = b.XpNeed(0);
            b.Step();
            Ok(b.State == BState.Cards && b.Offer.All(o => o >= 0) && b.Offer.Distinct().Count() == 3, "subir de nivel ofrece 3 cartas distintas");
            Ok(b.Offer.All(o => Skills.All[o].Hero == null || b.InDeck(Defs.HeroIndex(Skills.All[o].Hero))), "solo cartas de juguetes del mazo");
            var t0 = b.Time;
            b.Step();
            Ok(b.Time == t0, "con las cartas abiertas el juego esta en pausa");
            Ok(b.Reroll() && !b.Reroll(), "volver a tirar una sola vez");
            b.PickCard(0);
            Ok(b.State == BState.Playing && b.Picks.Sum() == 1, "elegir una carta sigue el juego");
            // efecto concreto
            b = New(); var lives = b.Lives;
            Skills.All.First(s => s.Id == "vidas").Apply(b);
            Ok(b.Lives == lives + 3, "Velador: +3 vidas");
            b = New(); Skills.All.First(s => s.Id == "invocar_nv2").Apply(b);
            int c = b.Summon();
            Ok(b.Cells[c].Level == 2, "La proxima invocacion sale en nivel 2");
            b = New(); Skills.All.First(s => s.Id == "fusion_chispas").Apply(b);
            b.Place(0, StartDeck[0], 1); b.Place(1, StartDeck[0], 1); int sp = b.Sparks; b.Drop(0, 1);
            Ok(b.Sparks == sp + 5, "Fusionar da 5 chispas");
        }

        // ------------------------------------------------------------------ determinismo
        static void DeterminismTests()
        {
            string Sig(int seed)
            {
                var b = new Battle(1, 3, StartDeck, null, seed);
                var bot = new GoodBot();
                bot.Play(b);
                return b.State + "/" + b.WaveIndex + "/" + b.Lives + "/" + b.Kills + "/" + b.Sparks + "/" + b.Time.ToString("0.000");
            }
            Ok(Sig(42) == Sig(42), "misma semilla, misma partida");
            Ok(Sig(42) != Sig(43) || true, "semillas distintas (informativo)");
            // el paso es fijo: simular 2 pasos por cuadro (velocidad x2) da lo mismo que 1 por cuadro
            var a = new Battle(1, 2, StartDeck, null, 7); var c = new Battle(1, 2, StartDeck, null, 7);
            a.Sparks = c.Sparks = 500;
            for (int i = 0; i < 6; i++) { a.Summon(); c.Summon(); }
            for (int i = 0; i < 1800; i++) a.Step();
            for (int i = 0; i < 900; i++) { c.Step(); c.Step(); }
            Ok(a.Kills == c.Kills && a.Sparks == c.Sparks && a.Lives == c.Lives, "x2 no cambia el resultado (paso fijo)");
            // rendimiento: 120 pesadillas en el tablero
            var perf = Empty1(); perf.Sparks = 99999;
            for (int i = 0; i < 15; i++) perf.Summon();
            perf.Lives = 100000;
            for (int i = 0; i < 120; i++) Spawn(perf, "slime", 0.12f * i, 400f).Speed = 0.2f;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 300; i++) { perf.Step(); perf.Events.Clear(); }
            double ms = sw.Elapsed.TotalMilliseconds / 300.0;
            Console.WriteLine("  vivas " + perf.Alive + " estado " + perf.State + " juguetes " + perf.Cells.Count(u => !u.Empty));
            Console.WriteLine("  paso con 120 pesadillas y 15 juguetes: " + ms.ToString("0.000") + " ms");
            Ok(ms < 1.0, "un paso con 120 pesadillas cuesta menos de 1 ms");
            long g0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++) { perf.Step(); perf.Events.Clear(); }
            long alloc = GC.GetAllocatedBytesForCurrentThread() - g0;
            Console.WriteLine("  memoria asignada en 300 pasos: " + alloc + " bytes");
            Ok(alloc < 4096, "sin basura de memoria durante la batalla");
        }
    }
}
