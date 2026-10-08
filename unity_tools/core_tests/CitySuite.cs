using System;
using System.Collections.Generic;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Pruebas de la ciudad (0.9): Ayuntamiento, obras con tiempo, materiales, almacenes, produccion, mercado y tren.</summary>
    public static class CitySuite
    {
        static void Run(Island isl, float seconds) { for (float t = 0; t < seconds; t += 0.1f) isl.Tick(0.1f); }

        public static void Go()
        {
            Layout();
            Works();
            Storage();
            Production();
            Commerce();
            SaveAndMigrate();
            Tutorial();
            LongRun();
        }

        static void Tutorial()
        {
            var isl = new Island(60);
            T.Check("tutorial: las partidas cargadas o de prueba no lo muestran", isl.TutDone);
            isl.StartTutorial();
            int offered = isl.Plots.FindAll(p => isl.Offered(p)).Count;
            T.Check("tutorial: arranca con un minero y sin parcelas libres", isl.Miners.Count == 1 && offered == 0 && !isl.TutDone);
            Ore o = isl.OreList.Find(x => x.Kind == 0);
            for (int i = 0; i < 30 && !o.Dead; i++) { isl.TapOre(o, i); isl.Tick(0.6f); }
            isl.Tick(0.1f);
            T.Check("tutorial: romper una roca avanza y suma piedra", isl.Tut >= Island.TutStep.WatchMiner && isl.Stock[(int)Res.Stone] >= 1);
            int goal0 = isl.GoalIdx;
            for (int i = 0; i < 600 && isl.Tut < Island.TutStep.UpgradeHouse; i++) isl.Tick(0.1f);
            T.Check("tutorial: el minero pica solo y pide mejorar la casa", isl.Tut == Island.TutStep.UpgradeHouse && isl.GoalIdx == goal0);
            isl.Coins += 50; isl.Stock[(int)Res.Stone] += 5;
            isl.Upgrade(isl.Find(BKind.House)); isl.Tick(0.1f);
            T.Check("tutorial: mejorar pasa a terminar la obra", isl.Tut == Island.TutStep.FinishWork);
            T.Check("tutorial: la obra de la casa se termina gratis", isl.SpeedUpGems(isl.Find(BKind.House)) == 0 && isl.SpeedUp(isl.Find(BKind.House)));
            isl.Tick(0.1f);
            offered = isl.Plots.FindAll(p => isl.Offered(p)).Count;
            T.Check("tutorial: recien ahora aparecen las parcelas", isl.Tut == Island.TutStep.BuildSawmill && offered == 2);
            isl.Coins += 100;
            var f = isl.Plots.Find(p => isl.Offered(p));
            T.Check("tutorial: el aserradero cuesta poco y se puede construir", isl.BuildCost(BKind.Sawmill) <= 60 && isl.Build(BKind.Sawmill, f));
            isl.SpeedUp(f);
            for (int i = 0; i < 200 && f.Ready == 0; i++) isl.Tick(0.1f);
            isl.Collect(f); isl.Tick(0.1f);
            T.Check("tutorial: cobrar la madera lo termina y arrancan las metas", isl.TutDone && isl.GoalIdx > goal0);
            var h = new Island(1);
            T.Check("tutorial: el paso se guarda", h.LoadJson(isl.ToJson()) && h.TutDone);
        }

        static void Layout()
        {
            var isl = new Island(1);
            T.Check("ciudad: 39 parcelas alrededor del Ayuntamiento", isl.Plots.Count == 40);
            float min = 99f;
            for (int i = 1; i < isl.Plots.Count; i++)
                for (int j = i + 1; j < isl.Plots.Count; j++)
                {
                    float dx = isl.Plots[i].X - isl.Plots[j].X, dz = isl.Plots[i].Z - isl.Plots[j].Z;
                    min = Math.Min(min, (float)Math.Sqrt(dx * dx + dz * dz));
                }
            T.Check("ciudad: los edificios no quedan amontonados (>= 4.8 m entre parcelas)", min >= 4.8f, min.ToString("0.00"));
            T.Check("ciudad: las 16 parcelas de 0.8 quedan en su lugar", Math.Abs(isl.Plots[13].X) < 1e-3 && Math.Abs(isl.Plots[13].Z - 14.2f) < 1e-3);
            T.Check("ciudad: hay un tipo de edificio por cada entrada de datos", Island.Defs.Length == 33 && Island.Defs[31].Kind == BKind.Lab && Island.Defs[32].Kind == BKind.Barracks);
            bool order = true;
            for (int i = 0; i < Island.Defs.Length; i++) order &= (int)Island.Defs[i].Kind == i;
            T.Check("ciudad: Defs indexado por tipo", order);
            T.Check("ciudad: arranca con Ayuntamiento nivel 1", isl.Th == 1 && isl.Find(BKind.Depot).Level == 1);
            T.Check("ciudad: el Ayuntamiento bloquea lo de mas arriba", isl.Unlocked(BKind.Sawmill) && !isl.Unlocked(BKind.Foundry) && !isl.Unlocked(BKind.Lab));
            int dup = 0; foreach (var r in Island.ResDefs) if (r.Th > Island.MaxTh) dup++;
            T.Check("ciudad: todos los recursos se desbloquean en algun Ayuntamiento", dup == 0);
            bool recipesOk = true;
            foreach (var rc in Island.Recipes)
            {
                foreach (var inp in rc.In) if (Island.RDef(inp).Th > Island.Def(rc.Kind).Th && Island.RDef(inp).Th > Island.RDef(rc.Out).Th) recipesOk = false;
                if (Island.RDef(rc.Out).Th < Island.Def(rc.Kind).Th && rc.Kind != BKind.Smithy) recipesOk = false;
            }
            T.Check("ciudad: ninguna receta pide algo que todavia no existe", recipesOk);
            bool matsOk = true;
            for (int th = 2; th <= Island.MaxTh; th++)
                foreach (var kv in isl.MatsFor(BKind.Depot, th)) if (Island.RDef(kv.Key).Th > th - 1) matsOk = false;
            T.Check("ciudad: el Ayuntamiento solo pide lo que ya se puede fabricar", matsOk);
            bool bandOk = true;
            for (int to = 2; to <= 15; to++)
                foreach (var kv in isl.MatsFor(BKind.House, to)) if (Island.RDef(kv.Key).Th > Math.Max(1, to - 2)) bandOk = false;
            T.Check("ciudad: las mejoras piden materiales del nivel de Ayuntamiento que las habilita", bandOk);
        }

        static void Works()
        {
            var isl = IslandSuite.Rich(new Island(2), 1e6, 3);
            var free = isl.Plots.Find(p => isl.Offered(p));
            bool ok = isl.Build(BKind.Foundry, free);
            T.Check("obras: construir deja la obra en curso (nivel 0)", ok && free.Level == 0 && free.Work > 0 && free.Building == (int)BKind.Foundry);
            T.Check("obras: mientras dura no cuenta como construido", isl.Level(BKind.Foundry) == 0);
            bool done = false; isl.WorkDone += p => done = true;
            Run(isl, (float)isl.WorkSeconds(BKind.Foundry, 1) + 1f);
            T.Check("obras: al terminar pasa a nivel 1 y avisa", done && free.Level == 1 && free.Work <= 0);
            var h = isl.Find(BKind.House);
            isl.Upgrade(h);
            T.Check("obras: mejorar mantiene el nivel hasta que termina", h.Level == 1 && h.Work > 0);
            T.Check("obras: no se puede mejorar dos veces a la vez", !isl.CanUpgrade(h));
            T.Check("obras: los primeros 5 min se terminan gratis", isl.SpeedUpGems(h) == 0 && isl.SpeedUp(h) && h.Level == 2);
            // obra larga: cuesta gemas y libera el constructor
            var b = IslandSuite.Rich(new Island(3), 1e9, 12);
            b.BonusBuilders = 0;
            var th = b.Plots[0];
            th.Level = 11;
            b.Upgrade(th);
            T.Check("obras: el Ayuntamiento alto tarda horas", th.Work >= 3 * 3600);
            int g = b.SpeedUpGems(th);
            b.Gems = 0;
            T.Check("obras: acelerar una obra larga cuesta gemas", g > 10 && !b.SpeedUp(th));
            b.Gems = g;
            T.Check("obras: con gemas termina ya", b.SpeedUp(th) && th.Level == 12 && b.Gems == 0);
            b.CutWork(th, 10);   // sin obra: no hace nada
            // constructores
            var c = IslandSuite.Rich(new Island(4), 1e6, 2);
            c.BonusBuilders = 0;
            T.Check("obras: se empieza con 2 constructores", c.Builders() == 2 && c.FreeBuilders() == 2);
            c.Upgrade(c.Find(BKind.House));
            var f1 = c.Plots.Find(p => c.Offered(p));
            c.Build(BKind.Canteen, f1);
            var f2 = c.Plots.Find(p => c.Offered(p));
            T.Check("obras: sin constructor libre no se puede construir", c.FreeBuilders() == 0 && !c.CanBuild(BKind.Sawmill, f2) && !c.Allowed(BKind.Sawmill, f2));
            c.Plots[0].Level = 5;
            T.Check("obras: el Ayuntamiento 5 suma un constructor", c.Builders() == 3);
            // anuncio: -30 min
            var d = IslandSuite.Rich(new Island(5), 1e9, 12);
            d.Plots[0].Level = 11; d.Upgrade(d.Plots[0]);
            double w0 = d.Plots[0].Work; d.CutWork(d.Plots[0], 1800);
            T.Check("obras: el anuncio recorta 30 min", Math.Abs(w0 - d.Plots[0].Work - 1800) < 1e-6);
            // materiales
            var m = new Island(6);
            m.Coins = 1e6;
            T.Check("obras: mejorar pide materiales (sin piedra no se puede)", !m.CanUpgrade(m.Find(BKind.House)));
            m.Stock[(int)Res.Stone] = 3;
            T.Check("obras: con la piedra justa se puede y la descuenta", m.Upgrade(m.Find(BKind.House)) && m.Stock[(int)Res.Stone] == 0);
            // ampliar pide Ayuntamiento
            var e = new Island(7); e.Coins = 1e9;
            T.Check("obras: ampliar la isla pide Ayuntamiento 3", !e.CanExpand());
            e.Plots[0].Level = 3;
            T.Check("obras: con Ayuntamiento 3 se amplia", e.DoExpand() && e.Expand == 1 && !e.CanExpand());
            // segunda mina con el Ayuntamiento mas alto
            var s2 = IslandSuite.Rich(new Island(8), 1e9, 1);
            T.Check("obras: un solo aserradero al principio", s2.CountCap(BKind.Sawmill) == 1);
            s2.Plots[0].Level = 4;
            T.Check("obras: el segundo aserradero llega con el Ayuntamiento 4", s2.CountCap(BKind.Sawmill) == 2 && s2.CountCap(BKind.Foundry) == 1);
        }

        static void Storage()
        {
            var isl = new Island(10);
            int cap = isl.RawCap();
            T.Check("almacen: sin galpon entran 50 materias primas", cap == 50);
            int got = isl.AddRes(Res.Wood, 80);
            bool full = false; isl.StorageFull += k => full = true;
            T.Check("almacen: lo que no entra se pierde y avisa", got == 50 && isl.AddRes(Res.Stone, 1) == 0 && full);
            T.Check("almacen: productos van por separado", isl.AddRes(Res.IronBar, 5) == 5 && isl.ProdStored() == 5);
            double c0 = isl.Coins;
            T.Check("almacen: sin mercado igual se vende barato para hacer lugar", isl.Sell(Res.Wood, 10) > 0 && isl.Coins > c0 && isl.Stock[(int)Res.Wood] == 40);
            var b = IslandSuite.Rich(new Island(11), 1e6, 2);
            for (int i = 0; i < b.Stock.Length; i++) b.Stock[i] = 0;
            b.Stock[(int)Res.Stone] = 0;
            var free = b.Plots.Find(p => b.Offered(p));
            T.Check("almacen: el galpon no pide materiales (nunca queda trabado)", b.CanBuild(BKind.Barn, free));
            IslandSuite.Now(b, b.Build(BKind.Barn, free));
            T.Check("almacen: el galpon agranda el lugar", b.RawCap() > 50);
            // las vetas suman materia prima
            var v = new Island(12);
            int dep = 0; v.Deposited += (mm, x) => dep++;
            Run(v, 40f);
            T.Check("almacen: lo que traen los mineros suma piedra", dep > 0 && v.Stock[(int)Res.Stone] + v.Stock[(int)Res.Copper] > 0);
        }

        static void Production()
        {
            var isl = IslandSuite.Rich(new Island(20), 1e6, 3);
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = 0;
            var f = isl.Plots.Find(p => isl.Offered(p));
            IslandSuite.Now(isl, isl.Build(BKind.Sawmill, f));
            bool ready = false; isl.ProductReady += p => ready = true;
            Run(isl, 25f);
            T.Check("produccion: el aserradero saca madera solo", ready && f.Ready > 0 && f.ReadyRes == (int)Res.Wood);
            int n = f.Ready;
            T.Check("produccion: cobrar la pasa al almacen", isl.Collect(f) == n && isl.Stock[(int)Res.Wood] == n && f.Ready == 0);
            Run(isl, 1000f);
            T.Check("produccion: la extraccion se frena al llenar su lugar", f.Ready == isl.ExtractBuffer(f));
            // fundicion: receta con ingredientes
            var g = isl.Plots.Find(p => isl.Offered(p));
            isl.Stock[(int)Res.Stone] = 50; isl.Stock[(int)Res.Wood] = 50;
            IslandSuite.Now(isl, isl.Build(BKind.Foundry, g));
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = 0;
            int rec = Island.RecipesOf(BKind.Foundry)[0];
            T.Check("produccion: sin ingredientes no encola", !isl.QueueRecipe(g, rec));
            isl.Stock[(int)Res.IronOre] = 4; isl.Stock[(int)Res.Coal] = 2;
            T.Check("produccion: encola y descuenta ingredientes", isl.QueueRecipe(g, rec) && isl.QueueRecipe(g, rec) && isl.Stock[(int)Res.IronOre] == 0 && g.Queue.Count == 2);
            Run(isl, 45f);
            T.Check("produccion: la receta termina y queda lista", g.Ready == 1 && g.ReadyRes == (int)Res.IronBar && g.Queue.Count == 1);
            Run(isl, 45f);
            T.Check("produccion: las listas del mismo producto se apilan", g.Ready == 2 && g.Queue.Count == 0);
            isl.Collect(g);
            T.Check("produccion: cobrar lingotes", isl.Stock[(int)Res.IronBar] == 2 && isl.Stat("made9") == 2);
            // en obra no produce
            isl.Stock[(int)Res.IronOre] = 2; isl.Stock[(int)Res.Coal] = 1; isl.QueueRecipe(g, rec);
            isl.Stock[(int)Res.Stone] = 20;
            isl.Upgrade(g);
            double t0 = g.ProdT; Run(isl, 3f);
            T.Check("produccion: durante la mejora no produce", g.Work > 0 && g.ProdT == t0);
            isl.FinishAllWork();
            T.Check("produccion: la cola crece con el nivel", isl.QueueSlots(g) >= 3);
            // gerentes
            var mg = IslandSuite.Rich(new Island(21), 1e6, 4);
            var s1 = mg.Plots.Find(p => mg.Offered(p)); IslandSuite.Now(mg, mg.Build(BKind.Sawmill, s1));
            var s2 = mg.Plots.Find(p => mg.Offered(p)); IslandSuite.Now(mg, mg.Build(BKind.Managers, s2));
            for (int i = 0; i < mg.Stock.Length; i++) mg.Stock[i] = 0;
            Run(mg, 40f);
            T.Check("produccion: con gerentes la madera se cobra sola", mg.Stock[(int)Res.Wood] > 0);
        }

        static void Commerce()
        {
            var isl = IslandSuite.Rich(new Island(30), 1e6, 6);
            var f = isl.Plots.Find(p => isl.Offered(p)); IslandSuite.Now(isl, isl.Build(BKind.Market, f));
            Run(isl, 1f);
            T.Check("mercado: el mercader trae 3 ofertas", isl.Merchant.Count == 3);
            double before = isl.Coins;
            T.Check("mercado: vender paga mas que sin mercado", isl.Sell(Res.Wood, 10) > 0 && isl.Coins > before && isl.SellPrice(Res.Wood) >= Island.RDef(Res.Wood).Value * 0.8 - 1);
            for (int i = 0; i < isl.Stock.Length; i++) isl.Stock[i] = 0;
            var o = isl.Merchant[0];
            T.Check("mercado: comprar una oferta", isl.BuyOffer(0) && o.Sold && isl.Stock[(int)o.What] == o.Count && !isl.BuyOffer(0));
            // tren
            var t = IslandSuite.Rich(new Island(31), 1e6, 6);
            var ts = t.Plots.Find(p => t.Offered(p)); IslandSuite.Now(t, t.Build(BKind.Train, ts));
            bool came = false, left = false, full = false;
            t.TrainArrived += () => came = true;
            t.TrainLeft += ok => { left = true; full = ok; };
            t.TrainSoon(); Run(t, 1f);
            T.Check("tren: llega con 3 vagones", came && t.TrainHere && t.Wagons.Count == 3);
            int gems = t.Gems; double c0 = t.Coins;
            for (int i = 0; i < 3; i++) t.LoadWagon(i);
            T.Check("tren: cargar los 3 vagones paga y se va lleno", left && full && t.Coins > c0 && t.Gems >= gems + 2 && !t.TrainHere);
            var t2 = IslandSuite.Rich(new Island(32), 1e6, 6);
            var ts2 = t2.Plots.Find(p => t2.Offered(p)); IslandSuite.Now(t2, t2.Build(BKind.Train, ts2));
            bool l2 = false, f2 = true; t2.TrainLeft += ok => { l2 = true; f2 = ok; };
            t2.TrainSoon(); Run(t2, 1f);
            Run(t2, (float)Island.TrainStay + 2f);
            T.Check("tren: si no se carga, se va igual (sin castigo)", l2 && !f2 && !t2.TrainHere);
        }

        static void SaveAndMigrate()
        {
            var a = IslandSuite.Rich(new Island(40), 1e6, 4);
            var f = a.Plots.Find(p => a.Offered(p)); IslandSuite.Now(a, a.Build(BKind.Foundry, f));
            a.Stock[(int)Res.IronOre] = 10; a.Stock[(int)Res.Coal] = 5;
            a.QueueRecipe(f, Island.RecipesOf(BKind.Foundry)[0]);
            var g = a.Plots.Find(p => a.Offered(p)); a.Build(BKind.Sawmill, g);
            a.BonusBuilders = 3;
            var b = new Island(1);
            T.Check("guardado: la ciudad se guarda y carga", b.LoadJson(a.ToJson()) && b.Th == 4 && b.Stock[(int)Res.IronOre] == 8
                && b.Plots[f.Id].Queue.Count == 1 && b.Plots[g.Id].Work > 0 && b.Plots[g.Id].Level == 0 && b.BonusBuilders == 3);
            // partida de 0.8 (sin "city"): el deposito pasa a Ayuntamiento acotado a 5, nada se pierde
            string old = "{\"v\":2,\"coins\":500,\"plots\":[{\"b\":1,\"l\":9},{\"b\":0,\"l\":6},{\"b\":2,\"l\":3}],\"gems\":4}";
            var c = new Island(2);
            T.Check("migracion: una partida de 0.8 carga", c.LoadJson(old));
            T.Check("migracion: el deposito queda como Ayuntamiento 5", c.Th == 5 && c.Level(BKind.House) == 6 && c.Level(BKind.Canteen) == 3);
            T.Check("migracion: regalo de piedra y madera para arrancar", c.Stock[(int)Res.Stone] >= 30 && c.Stock[(int)Res.Wood] >= 20);
            // sin conexion: obras y produccion siguen
            var d = IslandSuite.Rich(new Island(41), 1e6, 3);
            for (int i = 0; i < d.Stock.Length; i++) d.Stock[i] = 0;
            var s = d.Plots.Find(p => d.Offered(p)); d.Build(BKind.Sawmill, s);
            d.LastSeen = 1000;
            d.ApplyOffline(1000 + 3600);
            T.Check("sin conexion: la obra terminó y el aserradero produjo", s.Level == 1 && s.Ready == d.ExtractBuffer(s));
            T.Check("sin conexion: el banco agranda el tope de horas", d.OfflineCap() == 7200);
        }

        static void LongRun()
        {
            // bot de 2 h: progreso del Ayuntamiento y que no se trabe
            var isl = new Island(78);
            var log = new System.Text.StringBuilder();
            float gap;
            IslandSuite.CityBot(isl, 7200f, null, out gap, 0.2f);
            int built = 0; foreach (var p in isl.Plots) if (p.Building >= 0) built++;
            Console.WriteLine("---- bot 2 h: Ayuntamiento " + isl.Th + ", edificios " + built + ", mineros " + isl.Miners.Count + ", metas " + isl.GoalIdx
                + ", fabricados " + isl.Stat("made") + ", ganado " + BigNum.Fmt(isl.TotalEarned) + ", mayor espera " + gap.ToString("0") + " s");
            T.Check("ciudad: en 2 h se llega al Ayuntamiento 6 o más", isl.Th >= 6, isl.Th.ToString());
            T.Check("ciudad: en 2 h se fabrican productos de verdad", isl.Stat("made") >= 30, isl.Stat("made").ToString());
        }
    }
}
