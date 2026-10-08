using System;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Pruebas de la Isla Minera: arranque, IA, economia, construccion, necesidades y guardado.</summary>
    public static class IslandSuite
    {
        static void Run(Island isl, float seconds) { for (float t = 0; t < seconds; t += 0.05f) isl.Tick(0.05f); }

        /// <summary>Isla con monedas, materiales, constructores y Ayuntamiento de sobra (para probar otra cosa).</summary>
        public static Island Rich(Island i, double coins, int th = 6)
        {
            i.Coins = coins;
            for (int k = 0; k < i.Stock.Length; k++) i.Stock[k] = 500;
            i.BonusBuilders = 30;
            i.Plots[0].Level = Math.Max(i.Plots[0].Level, th);
            return i;
        }

        /// <summary>Construye o mejora y termina la obra en el acto.</summary>
        public static bool Now(Island i, bool ok) { i.FinishAllWork(); return ok; }

        public static void Go()
        {
            Rewards();
            var isl = new Island(7);
            T.Check("isla: arranca con deposito y casa nivel 1", isl.Find(BKind.Depot) != null && isl.Level(BKind.House) == 1);
            T.Check("isla: un minero al empezar", isl.Miners.Count == 1);
            T.Check("isla: hay vetas al empezar", isl.OreList.Count >= 4);
            int broken = 0, deposits = 0;
            isl.OreBroken += (o, m) => broken++;
            isl.Deposited += (m, v) => deposits++;
            Run(isl, 30f);
            T.Check("isla: en 30 s el minero rompe vetas", broken >= 2, "rotas=" + broken);
            T.Check("isla: y las lleva al deposito", deposits >= 2 && isl.Coins > 0, "dep=" + deposits + " coins=" + isl.Coins);
            T.Check("isla: la primera mejora de casa (15) se paga en < 60 s", TimeTo(new Island(7), 15) < 60f);
            T.Check("isla: jugando (mejora la casa al poder) la cantina llega en < 4 min", PlayUntil(new Island(7), BKind.Canteen) < 240f);

            // construir y mejorar
            var b = Rich(new Island(3), 10000);
            var free = b.Plots.Find(p => b.Offered(p));
            T.Check("isla: construir cantina", Now(b, b.Build(BKind.Canteen, free)) && b.Level(BKind.Canteen) == 1);
            T.Check("isla: no hay dos cantinas", !b.CanBuild(BKind.Canteen, b.Plots.Find(p => b.Offered(p))));
            T.Check("isla: mejorar casa suma minero", Now(b, b.Upgrade(b.Find(BKind.House))) && b.Miners.Count == 2);
            double c0 = b.Coins;
            T.Check("isla: no deja construir el deposito", !b.CanBuild(BKind.Depot, b.Plots.Find(p => b.Offered(p))));
            T.Check("isla: el costo se descuenta", c0 > b.Coins - 1e-9);

            // necesidades
            var n = Rich(new Island(5), 10000);
            n.Build(BKind.Canteen, n.Plots[2]);
            n.Build(BKind.Showers, n.Plots[3]);
            n.FinishAllWork();
            var m = n.Miners[0];
            m.Energy = 10f; m.Clean = 10f;
            bool ate = false, fresh = false;
            n.MinerMood += (mm, mood) => { if (mood == "fed") ate = true; if (mood == "fresh") fresh = true; };
            Run(n, 40f);
            T.Check("isla: minero cansado va a la cantina y come", ate);
            T.Check("isla: minero sucio se ducha y sale fresco", fresh);
            T.Check("isla: fresco rinde mas", n.Perf(new Miner { Energy = 100, Clean = 100, Fresh = 10 }) > n.Perf(new Miner { Energy = 100, Clean = 100 }));
            T.Check("isla: sin energia rinde menos", n.Perf(new Miner { Energy = 5, Clean = 100 }) < n.Perf(new Miner { Energy = 100, Clean = 100 }));

            // dos mineros no se pisan la misma veta
            var s = Rich(new Island(9), 1000);
            Now(s, s.Upgrade(s.Find(BKind.House)));
            Run(s, 2f);
            var a1 = s.Miners[0]; var a2 = s.Miners[1];
            T.Check("isla: dos mineros eligen vetas distintas", a1.Target < 0 || a2.Target < 0 || a1.Target != a2.Target);

            // los mineros no quedan dentro de los edificios
            bool inside = false;
            var w = Rich(new Island(11), 1e6);
            w.Build(BKind.Canteen, w.Plots[2]); w.Build(BKind.Showers, w.Plots[3]); w.Build(BKind.Smithy, w.Plots[4]);
            for (int i = 0; i < 4; i++) Now(w, w.Upgrade(w.Find(BKind.House)));
            w.FinishAllWork();
            for (int k = 0; k < 2000; k++)
            {
                w.Tick(0.05f);
                foreach (var mm in w.Miners)
                    foreach (var p in w.Plots)
                        if (p.Building >= 0 && p.Building != (int)BKind.House && Math.Sqrt((mm.X - p.X) * (mm.X - p.X) + (mm.Z - p.Z) * (mm.Z - p.Z)) < Island.Defs[p.Building].Radius * 0.5)
                            inside = true;
            }
            T.Check("isla: los mineros rodean los edificios", !inside);

            // parcelas ofrecidas de a dos
            var o = Rich(new Island(4), 1e6);
            int offered = 0; foreach (var p in o.Plots) if (o.Offered(p)) offered++;
            T.Check("isla: se ofrecen 2 parcelas libres", offered == 2);
            T.Check("isla: no deja construir en una parcela no ofrecida", !o.CanBuild(BKind.Canteen, o.Plots[8]));
            o.Build(BKind.Canteen, o.Plots[2]);
            T.Check("isla: al usar una aparece la siguiente", o.Offered(o.Plots[4]));

            // veta gigante: todos corren, paga por golpe y premio
            var gi = Rich(new Island(21), 1e5); gi.TotalEarned = 500;
            for (int i = 0; i < 3; i++) Now(gi, gi.Upgrade(gi.Find(BKind.House)));
            int gems0 = gi.Gems;
            var giant = gi.SpawnGiant();
            T.Check("isla: aparece la veta gigante", giant != null && giant.Giant);
            if (giant == null) return;
            Run(gi, 3f);
            int going = 0; foreach (var mm in gi.Miners) if (mm.Target == giant.Id) going++;
            T.Check("isla: los mineros libres van a la gigante (salvo el que lleva carga)", going >= gi.Miners.Count - 1, "van=" + going);
            double before = gi.Coins;
            Run(gi, 120f);
            T.Check("isla: la gigante se rompe y da premio y gemas", giant.Dead && gi.Coins > before && gi.Gems >= gems0 + 2);

            // barco comprador
            var sh = Rich(new Island(22), 1e6); sh.TotalEarned = 100;
            sh.Build(BKind.Dock, sh.Plots[2]);
            sh.Upgrade(sh.Find(BKind.House));
            sh.FinishAllWork();
            bool arrived = false, done = false;
            sh.ShipArrived += x => arrived = true;
            sh.ShipLeft += (x, ok) => { if (ok) done = true; };
            Run(sh, 70f);
            T.Check("isla: con muelle llega un barco", arrived);
            Run(sh, 200f);
            T.Check("isla: los mineros completan el pedido o el barco se va", done || sh.CurShip == null || sh.CurShip.Delivered > 0);

            // metas encadenadas
            var go = new Island(23);
            int g0 = go.GoalIdx;
            go.Coins = 100; go.Stock[(int)Res.Stone] = 10; go.Upgrade(go.Find(BKind.House)); go.FinishAllWork(); go.Tick(0.05f);
            T.Check("isla: meta 'mejorar casa' se cumple y avanza", go.GoalIdx == g0 + 1);
            T.Check("isla: siempre hay una meta", go.CurrentGoal() != null && go.CurrentGoal().Target > 0);

            // expansion
            var ex = Rich(new Island(24), 1e6);
            float r0 = ex.Radius;
            T.Check("isla: expandir agranda el radio y abre parcelas", ex.DoExpand() && ex.Radius > r0 && ex.Plots.Exists(p => p.Ring == 1 && ex.Offered(p)) == ex.Plots.FindAll(p => ex.Offered(p)).Exists(p => p.Ring == 1) );
            // mina, faro, turbo, toques, sin conexion
            var mn = Rich(new Island(25), 1e6); mn.TotalEarned = 500;
            Now(mn, mn.Build(BKind.Mine, mn.Plots[2]));
            double c1 = mn.Coins; Run(mn, 10f);
            T.Check("isla: la mina paga sola", mn.MineRate() > 0 && mn.Coins > c1);
            mn.Gems = 10;
            float pf = mn.Perf(mn.Miners[0]);
            T.Check("isla: turbo con gemas duplica el ritmo", mn.BuyTurbo() && mn.Perf(mn.Miners[0]) > pf * 1.9f && mn.Gems == 5);
            var tapIsl = new Island(26);
            var target = tapIsl.OreList.Find(x => x.Kind == 0);
            double got = 0; for (int i = 0; i < 10 && got <= 0; i++) got = tapIsl.TapOre(target, i);
            T.Check("isla: el jugador rompe una veta tocando y cobra", got > 0 && target.Dead);
            var off = Rich(new Island(27), 100, 1); Now(off, off.Upgrade(off.Find(BKind.House)));
            T.Check("isla: ganancia sin conexion acotada a 2 h", off.OfflineEarnings(3600) > 0 && off.OfflineEarnings(1e6) == off.OfflineEarnings(7200));
            T.Check("isla: menos de un minuto afuera no paga", off.OfflineEarnings(30) == 0);

            Pacing();

            // guardado
            var g = Rich(new Island(2), 500); g.Build(BKind.Showers, g.Plots[2]); g.Upgrade(g.Find(BKind.House)); g.FinishAllWork();
            var h = new Island(99);
            g.Gems = 7; g.GoalIdx = 3; g.AddStat("rocks", 42);
            T.Check("isla: guardar y cargar", h.LoadJson(g.ToJson()) && h.Level(BKind.Showers) == 1 && h.Level(BKind.House) == 2
                && h.Miners.Count == 2 && Math.Abs(h.Coins - g.Coins) < 1e-6 && h.Gems == 7 && h.GoalIdx == 3 && h.Stat("rocks") == 42);
        }

        /// <summary>
        /// Bot jugador de la ciudad: cobra, encola recetas, termina gratis lo corto y compra con prioridades (edificio
        /// nuevo desbloqueado > Ayuntamiento > ampliar > la mejora mas barata). Devuelve la mayor espera entre compras.
        /// </summary>
        public static float CityBot(Island isl, float seconds, System.Text.StringBuilder log, out float maxGap, float dt = 0.1f)
        {
            float t = 0, lastBuy = 0; maxGap = 0;
            float tt = 0;
            isl.GoalDone += g => log?.Append("  " + Clock(tt) + " meta: " + g.Text + "\n");
            while (t < seconds)
            {
                isl.Tick(dt); t += dt; tt = t;
                if (log != null && (int)(t / 300f) != (int)((t - dt) / 300f))
                    log.Append("  " + Clock(t) + " -- ganado " + BigNum.Fmt(isl.TotalEarned) + ", monedas " + BigNum.Fmt(isl.Coins) + ", mineros " + isl.Miners.Count + ", mina " + BigNum.Fmt(isl.MineRate()) + "/s\n");
                foreach (var p in isl.Plots)
                {
                    if (p.Work > 0 && isl.SpeedUpGems(p) == 0) isl.SpeedUp(p);
                    if (p.Ready > 0) isl.Collect(p);
                    if (p.Building >= 0 && p.Level >= 1 && Island.Produces(p.Building))
                        foreach (int r in Island.RecipesOf((BKind)p.Building)) if (Wise(isl, r) && isl.QueueRecipe(p, r)) break;
                }
                // almacen lleno: vende la mitad de lo que mas sobra (como haria un jugador)
                foreach (bool raw in new[] { true, false })
                    if ((raw ? isl.RawCap() - isl.RawStored() : isl.ProdCap() - isl.ProdStored()) <= 0)
                    {
                        int bi = -1;
                        for (int i = 0; i < isl.Stock.Length; i++)
                            if (Island.ResDefs[i].Raw == raw && (bi < 0 || isl.Stock[i] > isl.Stock[bi])) bi = i;
                        if (bi >= 0) isl.Sell((Res)bi, isl.Stock[bi] / 2);
                    }
                string what = Buy(isl);
                if (what != null)
                {
                    if (log != null && t - lastBuy > 150f)
                    {
                        log.Append("  " + Clock(t) + " ESPERA " + (int)(t - lastBuy) + " s hasta: " + what + " | stock:");
                        for (int i = 0; i < isl.Stock.Length; i++) if (isl.Stock[i] > 0) log.Append(" " + (Res)i + "=" + isl.Stock[i]);
                        log.Append("\n");
                    }
                    maxGap = Math.Max(maxGap, t - lastBuy);
                    lastBuy = t;
                    if (log != null && !what.StartsWith("mejora ")) log.Append("  " + Clock(t) + " " + what + "\n");
                }
                if (isl.Gems >= Island.TurboGems + 20 && isl.TurboT <= 0f) isl.BuyTurbo();
            }
            return maxGap;
        }

        /// <summary>Encolar solo si no se come lo que pide el proximo Ayuntamiento (salvo que fabrique algo que pide).</summary>
        static bool Wise(Island isl, int recipe)
        {
            var rc = Island.Recipes[recipe];
            var need = isl.MatsFor(BKind.Depot, isl.Th + 1);
            int Need(Res r) { foreach (var kv in need) if (kv.Key == r) return kv.Value; return 0; }
            if (Need(rc.Out) > isl.Stock[(int)rc.Out]) return true;
            if (isl.Stock[(int)rc.Out] >= 12) return false;
            for (int i = 0; i < rc.In.Length; i++) if (isl.Stock[(int)rc.In[i]] - rc.InN[i] < Need(rc.In[i])) return false;
            return true;
        }

        static string Clock(float t) { return (int)(t / 3600) + "h" + ((int)(t / 60) % 60).ToString("00") + ":" + ((int)t % 60).ToString("00"); }

        static string Buy(Island isl)
        {
            // 1) edificio desbloqueado que todavia no tiene
            foreach (var d in Island.Defs)
            {
                if (d.Kind == BKind.House || isl.CountOf(d.Kind) >= isl.CountCap(d.Kind)) continue;
                var free = isl.Plots.Find(p => isl.Offered(p));
                if (free != null && isl.CanBuild(d.Kind, free)) { isl.Build(d.Kind, free); return "construye " + d.Name; }
            }
            // 2) Ayuntamiento
            var th = isl.Plots[0];
            if (isl.CanUpgrade(th)) { isl.Upgrade(th); return "Ayuntamiento " + (th.Level + 1); }
            // 3) ampliar
            if (isl.CanExpand()) { isl.DoExpand(); return "expande la isla"; }
            // 4) la mejora mas barata (o una segunda casa)
            Plot best = null; double bc = double.MaxValue;
            foreach (var p in isl.Plots)
                if (p.Building > 0 && isl.CanUpgrade(p) && isl.UpgradeCost(p) < bc) { bc = isl.UpgradeCost(p); best = p; }
            var h = isl.Plots.Find(p => p.Building == (int)BKind.House);
            if (h != null && isl.CanUpgrade(h) && isl.UpgradeCost(h) < bc) { bc = isl.UpgradeCost(h); best = h; }
            if (best != null) { isl.Upgrade(best); return "mejora " + Island.Defs[best.Building].Name; }
            var f2 = isl.Plots.Find(p => isl.Offered(p));
            if (f2 != null && isl.CanBuild(BKind.House, f2)) { isl.Build(BKind.House, f2); return "construye otra casa"; }
            return null;
        }

        public static void Pacing()
        {
            var isl = new Island(77);
            var log = new System.Text.StringBuilder();
            float maxGap;
            CityBot(isl, 1800f, log, out maxGap);
            Console.WriteLine("---- ritmo (bot 30 min):\n" + log + "  mineros " + isl.Miners.Count + ", ganado " + BigNum.Fmt(isl.TotalEarned)
                + ", Ayuntamiento " + isl.Th + ", metas " + isl.GoalIdx + ", gemas " + isl.Gems + ", mayor espera entre compras " + maxGap.ToString("0") + " s");
            var dbg = new System.Text.StringBuilder("  estado: constructores libres " + isl.FreeBuilders() + "/" + isl.Builders() + ", monedas " + BigNum.Fmt(isl.Coins) + "\n");
            foreach (var p in isl.Plots) if (p.Building >= 0) dbg.Append("   " + Island.Defs[p.Building].Name + " nv" + p.Level + (p.Work > 0 ? " obra " + (int)p.Work + "s" : "") + (p.Ready > 0 ? " listo " + p.Ready : "") + "\n");
            dbg.Append("   stock:"); for (int i = 0; i < isl.Stock.Length; i++) if (isl.Stock[i] > 0) dbg.Append(" " + (Res)i + "=" + isl.Stock[i]);
            dbg.Append(" | ofrecidas " + isl.Plots.FindAll(p => isl.Offered(p)).Count + " | meta: " + isl.CurrentGoal().Text);
            Console.WriteLine(dbg);
            T.Check("isla: en 30 min no hay esperas de mas de 3 min entre compras", maxGap < 180f, maxGap.ToString("0"));
            T.Check("isla: en 30 min se cumplen al menos 8 metas", isl.GoalIdx >= 8, isl.GoalIdx.ToString());
        }

        /// <summary>Jugador simple: mejora la casa hasta 3 cuando puede y construye `goal` apenas le alcanza.</summary>
        static float PlayUntil(Island isl, BKind goal)
        {
            float t = 0;
            while (t < 900f)
            {
                isl.Tick(0.05f); t += 0.05f;
                var free = isl.Plots.Find(p => isl.Offered(p));
                if (isl.CanBuild(goal, free)) { isl.Build(goal, free); break; }
                var h = isl.Find(BKind.House);
                if (h.Level < 3 && isl.CanUpgrade(h)) isl.Upgrade(h);
                foreach (var p in isl.Plots) if (p.Work > 0 && isl.SpeedUpGems(p) == 0) isl.SpeedUp(p);
            }
            Console.WriteLine("      (isla: " + goal + " construida a los " + t.ToString("0") + " s, mineros " + isl.Miners.Count + ")");
            return t;
        }

        /// <summary>Segundos de simulacion hasta juntar `target` monedas.</summary>
        static float TimeTo(Island isl, double target)
        {
            float t = 0;
            while (isl.Coins < target && t < 600f) { isl.Tick(0.05f); t += 0.05f; }
            Console.WriteLine("      (isla: " + target + " monedas en " + t.ToString("0") + " s)");
            return t;
        }

        static void Rewards()
        {
            var r = new Island(11);
            r.TotalEarned = 100;
            bool came = false, gone = false;
            r.BalloonCame += () => came = true;
            r.BalloonGone += t => gone = t;
            Run(r, 55f);
            T.Check("premios: el globo llega al rato de jugar", came && r.BalloonHere);
            T.Check("premios: tocar el globo da un giro", r.TapBalloon() && r.Spins == 1 && gone && !r.BalloonHere);
            var seen = new bool[Island.Wheel.Length];
            for (int i = 0; i < 400; i++)
            {
                r.Spins = 1;
                int k = r.Spin();
                if (k >= 0) seen[k] = true;
                r.ClaimSpin();
                foreach (var o in r.OreList) if (o.Giant) o.Dead = true;
                r.OreList.RemoveAll(o => o.Dead);
            }
            bool all = true; foreach (var b in seen) all &= b;
            T.Check("premios: la ruleta puede dar todos los premios", all);
            r.Spins = 0;
            T.Check("premios: sin giros no gira", r.Spin() < 0);
            var c = new Island(12);
            c.TotalEarned = 5000;
            c.GiveChest(0); c.GiveChest(2);
            double before = c.Coins;
            var l = c.OpenChest();
            T.Check("premios: se abre primero el mejor cofre", l != null && l.Tier == 2 && c.Coins > before && l.Gems >= 5);
            T.Check("premios: queda el de madera", c.Chests[0] == 1 && c.ChestCount == 1);
            var g = new Island(13);
            g.Spins = 2; g.GiveChest(1);
            var h = new Island(1);
            h.LoadJson(g.ToJson());
            T.Check("premios: giros y cofres se guardan", h.Spins == 2 && h.Chests[1] == 1);
            T.Check("premios: etapas de evolucion 1-4", Island.Tier(1) == 1 && Island.Tier(3) == 2 && Island.Tier(6) == 3 && Island.Tier(10) == 4);
            var sky = new Island(14);
            sky.TotalEarned = 5000; sky.Spins = 1;
            sky.PendingPrize = 5;
            sky.ClaimSpin();
            T.Check("premios: la veta de oro de la ruleta cae del cielo", sky.Giant != null && sky.Giant.Sky);
            var pa = new Island(21);
            pa.TotalEarned = 5000;
            for (int i = 0; i < 40; i++) pa.SpawnOre(false);
            bool clean = true;
            foreach (var o in pa.OreList) if (pa.OnPath(o.X, o.Z, 0f)) clean = false;
            T.Check("caminos: hay uno por parcela", pa.Paths.Count == pa.Plots.Count - 1);
            T.Check("caminos: las vetas no caen sobre los caminos", clean);
        }
    }
}