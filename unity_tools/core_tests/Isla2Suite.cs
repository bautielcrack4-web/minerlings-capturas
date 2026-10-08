using System;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Pruebas de la Isla 0.8 (biblia visual): picar, mineros, sorpresas, rutina diaria y progreso grande.</summary>
    public static class Isla2Suite
    {
        static void Run(Island isl, float seconds) { for (float t = 0; t < seconds; t += 0.05f) isl.Tick(0.05f); }

        public static void Go()
        {
            Tapping();
            World();
            MinerSuite();
            Surprises();
            Daily();
            Progress();
            Builders();
        }

        static void Builders()
        {
            var b = new Island(91);
            b.Coins = 1e6;
            IslandSuite.Now(b, IslandSuite.Rich(b, b.Coins, 1).Upgrade(b.Find(BKind.House))); IslandSuite.Now(b, IslandSuite.Rich(b, b.Coins, 1).Upgrade(b.Find(BKind.House)));
            Run(b, 3f);
            var free = b.Plots.Find(p => b.Offered(p));
            IslandSuite.Rich(b, b.Coins, 1).Build(BKind.Canteen, free);
            int helping = 0;
            foreach (var m in b.Miners) if (m.State == MState.ToBuild || m.State == MState.Building) helping++;
            T.Check("obra: dos mineros corren a ayudar", helping == 2, helping.ToString());
            Run(b, 3f);
            bool back = true;
            foreach (var m in b.Miners) if (m.State == MState.ToBuild || m.State == MState.Building) back = false;
            T.Check("obra: al terminar vuelven a trabajar", back);
        }

        static void Progress()
        {
            var w = new Island(81);
            float wx, wz; w.WonderSpot(out wx, out wz);
            T.Check("maravilla: su lugar esta dentro de la isla y lejos de caminos", wx * wx + wz * wz < w.Radius * w.Radius && !w.OnPath(wx, wz, 1f));
            w.Coins = 1e9;
            int built = 0; w.WonderBuilt += ph => built = ph;
            for (int i = 0; i < 6; i++) w.BuildWonder();
            T.Check("maravilla: 5 fases y multiplica la ganancia x1.5", w.WonderPhase == 5 && built == 5 && Math.Abs(w.WonderMult() - 1.5) < 1e-9 && w.CanSail);
            // decoracion
            var d = new Island(82);
            T.Check("decoracion: hay lugares al costado de los caminos", d.DecorSlots.Count >= 3, d.DecorSlots.Count.ToString());
            d.Coins = 1e6;
            double b0 = d.BeautyMult();
            T.Check("decoracion: poner una fuente suma belleza", d.PlaceDecor(0, 5) && d.Beauty == 12 && d.BeautyMult() > b0);
            T.Check("decoracion: no se ponen dos cosas en el mismo lugar", !d.PlaceDecor(0, 1));
            var s0 = d.DecorSlots[0];
            T.Check("decoracion: las vetas no nacen encima", !d.FreeSpot(s0[0], s0[1], 0.5f));
            // jefe
            var j = new Island(83);
            j.Coins = 1e6; j.TotalEarned = 9000;
            IslandSuite.Now(j, IslandSuite.Rich(j, j.Coins, 1).Upgrade(j.Find(BKind.House))); IslandSuite.Now(j, IslandSuite.Rich(j, j.Coins, 1).Upgrade(j.Find(BKind.House)));
            bool came = false, defeated = false; int phases = 0;
            j.BossCame += o => came = true;
            j.BossPhase += (o, ph) => phases++;
            j.BossDefeated += o => defeated = true;
            j.BossSoon();
            Run(j, 1f);
            var boss = j.Boss;
            T.Check("jefe: aparece el Golem de Roca", came && boss != null && boss.Giant);
            int gems = j.Gems, chests = j.ChestCount;
            for (int i = 0; i < 4000 && !boss.Dead; i++) { j.TapOre(boss, 10); boss.Age = 9f; }
            Run(j, 0.2f);
            T.Check("jefe: pasa por dos fases y cae", phases == 2 && defeated && j.BossLevel == 1);
            T.Check("jefe: da gemas y un cofre de oro", j.Gems >= gems + 6 && j.ChestCount > chests);
            // pase de temporada
            var p = new Island(84);
            p.CheckSeason(200);
            int ups = 0; p.SeasonLevelUp += lv => ups++;
            p.AddXp(350);
            T.Check("pase: la experiencia sube niveles", p.SeasonLevel == 3 && ups == 1);
            double c0 = p.Coins;
            T.Check("pase: se cobra el premio gratis", p.ClaimPass(1, false) && p.Coins > c0 && !p.ClaimPass(1, false));
            T.Check("pase: el dorado necesita comprarse", !p.ClaimPass(1, true));
            p.Gems = 200;
            T.Check("pase: comprado se cobra el dorado", p.BuyPassGold() && p.ClaimPass(1, true) && p.Gems > 200 - Island.PassGoldGems);
            p.CheckSeason(200 + Island.SeasonDays);
            T.Check("pase: la temporada nueva empieza de cero", p.SeasonXp == 0 && !p.PassGold);
            // museo
            var m = new Island(85);
            int found = 0; m.PieceFound += (st, pc) => found++;
            for (int i = 0; i < 400 && found < 12; i++) m.RollPiece(1.0);
            T.Check("museo: se completan los 3 sets y dan su bonus", found == 12 && m.SetDone(0) && m.SetDone(2) && m.GlobalMult() > 1.09);
            // expedicion
            var e = new Island(86);
            e.Coins = 1e9; e.TotalEarned = 2e6; e.Gems = 7;
            IslandSuite.Now(e, IslandSuite.Rich(e, e.Coins, 1).Upgrade(e.Find(BKind.House))); IslandSuite.Now(e, IslandSuite.Rich(e, e.Coins, 1).Upgrade(e.Find(BKind.House)));
            for (int i = 0; i < 5; i++) e.BuildWonder();
            int crew = e.Miners.Count;
            T.Check("expedicion: zarpar da reliquias y empieza una isla nueva", e.Sail() && e.Relics >= 3 && e.IslandNo == 1 && e.WonderPhase == 0 && e.Coins == 0 && e.Level(BKind.House) == 1);
            T.Check("expedicion: la ciudad arranca de cero", e.Th == 1 && e.RawStored() == 0 && e.BusyBuilders() == 0);
            T.Check("expedicion: se conservan gemas y la tripulacion espera en el barco", e.Gems == 7 && e.Miners.Count == 1 && e.Bench.Count == crew - 1);
            T.Check("expedicion: la isla nueva vale mas y las reliquias multiplican", e.IslandValue() > 2.9 && e.RelicMult() > 1.25);
            e.Coins = 1e9;
            e.AutoRecruit = false;
            IslandSuite.Now(e, IslandSuite.Rich(e, e.Coins, 1).Upgrade(e.Find(BKind.House)));
            T.Check("expedicion: la tripulacion baja sola cuando hay lugar", e.Miners.Count == 2 && e.Recruits.Count == 0);
            var sv = new Island(3);
            sv.LoadJson(e.ToJson());
            T.Check("progreso: isla, reliquias y tripulacion se guardan", sv.IslandNo == 1 && sv.Relics == e.Relics && sv.Bench.Count == e.Bench.Count);
        }

        static void Daily()
        {
            var d = new Island(71);
            d.TotalEarned = 3000;
            T.Check("diario: el primer dia hay premio", d.CheckDaily(100) && d.Streak == 1);
            T.Check("diario: se cobra una vez por dia", d.ClaimDaily(100) && !d.ClaimDaily(100) && !d.CheckDaily(100));
            for (int day = 101; day <= 106; day++) { d.CheckDaily(day); d.ClaimDaily(day); }
            int chests = d.ChestCount;
            T.Check("diario: 7 dias seguidos", d.Streak == 7 && d.StreakDay == 7);
            double c; int g, ch;
            d.DailyPrize(7, out c, out g, out ch);
            T.Check("diario: el dia 7 es un cofre de oro", ch == 2 && g >= 5 && chests >= 1);
            d.CheckDaily(109);
            T.Check("diario: saltear un dia corta la racha", d.Streak == 1 && d.BrokenStreak == 7);
            d.Gems = 20;
            T.Check("diario: la racha se puede salvar con gemas", d.RepairStreak(109) && d.Streak == 8 && d.Gems == 20 - Island.RepairGems);
            T.Check("diario: hay 3 misiones distintas por dia", d.Missions.Count == 3 && d.Missions[0].Stat != d.Missions[1].Stat && d.Missions[1].Stat != d.Missions[2].Stat);
            var m = d.Missions[0];
            T.Check("diario: no se cobra una mision sin cumplir", !d.ClaimMission(m));
            d.Stats[m.Stat] = d.Stat(m.Stat) + m.Target;
            int gems = d.Gems;
            T.Check("diario: cumplida se cobra", d.ClaimMission(m) && d.Gems > gems);
            bool all = false;
            d.AllMissionsDone += () => all = true;
            foreach (var x in d.Missions) { d.Stats[x.Stat] = d.Stat(x.Stat) + x.Target; d.ClaimMission(x); }
            T.Check("diario: las tres dan un cofre de plata", all && d.MissionBonus);
            // tablon de pedidos
            var o = new Island(72);
            o.TotalEarned = 2000;
            Run(o, 1f);
            T.Check("tablon: hay 3 pedidos", o.Orders.Count == 3);
            // un pedido de cada mineral comun: alguno se llena seguro con lo que vendan los mineros
            for (int i = 0; i < 3; i++) { o.Orders[i].Kind = i; o.Orders[i].Count = 1; o.Orders[i].Delivered = 0; o.Orders[i].Wait = 0f; }
            Order first = null;
            o.OrderDone += x => { if (first == null) first = x; };
            for (int i = 0; i < 6 && first == null; i++) Run(o, 60f);
            T.Check("tablon: los mineros llenan el pedido al vender", first != null && first.Done);
            double coins = o.Coins;
            int slot = o.Orders.IndexOf(first);
            T.Check("tablon: cobrar el pedido paga y trae otro papel", first != null && o.ClaimOrder(first) && o.Coins > coins && o.Orders[slot] != first && o.Orders[slot].Wait > 0f);
            var sv = new Island(2);
            sv.LoadJson(d.ToJson());
            T.Check("diario: racha, misiones y pedidos se guardan", sv.Streak == d.Streak && sv.Missions.Count == 3 && sv.Missions[0].Claimed && sv.LastDay == d.LastDay);
        }

        static void Surprises()
        {
            // cofres que suben de rareza
            var c = new Island(61);
            c.TotalEarned = 5000;
            int ups = 0, legends = 0;
            for (int i = 0; i < 400; i++)
            {
                c.GiveChest(0);
                int t0 = c.BeginChest();
                for (int q = 0; q < Island.ChestUpgradeTries; q++) if (c.TryUpgradeChest()) ups++;
                if (c.OpenTier == 3) legends++;
                var l = c.FinishChest();
                if (l == null || t0 != 0) { ups = -9999; break; }
            }
            T.Check("sorpresas: los cofres a veces suben de rareza", ups > 80, ups.ToString());
            T.Check("sorpresas: llegar a legendario desde madera es muy raro", legends >= 0 && legends < 12, legends.ToString());
            var lg = new Island(62);
            lg.TotalEarned = 5000;
            lg.GiveChest(3);
            int g0 = lg.Gems;
            lg.BeginChest();
            var loot = lg.FinishChest();
            T.Check("sorpresas: el cofre legendario da 12+ gemas y turbo", loot.Tier == 3 && lg.Gems - g0 >= 12 && loot.Turbo >= 120f);
            var sv = new Island(63);
            sv.GiveChest(1);
            sv.BeginChest();
            var sv2 = new Island(1);
            sv2.LoadJson(sv.ToJson());
            T.Check("sorpresas: un cofre a medio abrir vuelve al guardar", sv2.Chests[1] == 1);
            // botella con mapa: un minero cava y sale un cofre
            var b = new Island(64);
            b.TotalEarned = 5000;
            b.BottleSoon();
            bool came = false, dug = false;
            b.BottleArrived += x => came = true;
            b.TreasureDug += (x, t) => dug = true;
            Run(b, 1f);
            T.Check("sorpresas: llega una botella a la orilla", came && b.CurBottle != null);
            int chests = b.ChestCount;
            T.Check("sorpresas: abrir la botella marca una X en la isla", b.OpenBottle() && b.CurBottle.Opened && b.CurBottle.TX * b.CurBottle.TX + b.CurBottle.TZ * b.CurBottle.TZ < b.Radius * b.Radius);
            Run(b, 25f);
            T.Check("sorpresas: un minero cava en la X y sale un cofre", dug && b.CurBottle == null && b.ChestCount == chests + 1);
            // bichos
            var k = new Island(65);
            k.TotalEarned = 5000;
            k.SpawnCritter(CritterKind.Crab);
            int gems = k.Gems;
            k.CatchCritter();
            T.Check("sorpresas: atrapar al cangrejo da una gema", k.Gems == gems + 1 && k.CurCritter == null);
            k.SpawnCritter(CritterKind.Gull);
            double coins0 = k.Coins;
            T.Check("sorpresas: la gaviota ladrona suelta monedas", k.CatchCritter() > 0 && k.Coins > coins0);
            k.SpawnCritter(CritterKind.Butterfly);
            float tb = k.TurboT;
            k.CatchCritter();
            T.Check("sorpresas: la mariposa dorada da turbo", k.TurboT >= tb + 60f);
            bool gone = false;
            k.CritterGone += (x, caught) => gone = !caught;
            k.SpawnCritter(CritterKind.Mole);
            Run(k, 9f);
            T.Check("sorpresas: si no lo tocás, el bicho se va", gone && k.CurCritter == null);
        }

        static void MinerSuite()
        {
            var a = new Island(51);
            T.Check("mineros: el primero es el Minero de Piedra", a.Miners.Count == 1 && a.Miners[0].Char == 0 && Island.Char(a.Miners[0]).Spec == Island.OreStone && a.Found[0]);
            a.AutoRecruit = false;
            a.Coins = 1e6;
            bool arrived = false;
            a.RecruitsArrived += () => arrived = true;
            IslandSuite.Now(a, IslandSuite.Rich(a, a.Coins, 1).Upgrade(a.Find(BKind.House)));
            T.Check("mineros: al mejorar la casa llega un barco con 3 candidatos", arrived && a.Recruits.Count == 3 && a.Miners.Count == 1);
            T.Check("mineros: sin habitaciones en el Cuartel solo vienen Mineros de Piedra", a.Recruits.TrueForAll(c => c == 0));
            int pick = a.Recruits[1];
            var m = a.ChooseRecruit(1);
            T.Check("mineros: elegir uno lo suma a la isla y al album", m != null && m.Char == pick && a.Miners.Count == 2 && a.Found[pick] && a.Recruits.Count == 0);
            // rarezas: con todas las habitaciones sale de todo y lo legendario es raro
            var r = new Island(52);
            for (int ch = 1; ch < Island.Roster.Length; ch++) r.Modules.Add(new Module { Kind = ModKind.Dorm, Ch = ch, X = 9 + ch, Z = 9 });
            int[] byRar = new int[4]; int golden = 0;
            for (int i = 0; i < 6000; i++)
            {
                r.Recruits.Clear();
                var mi = typeof(Island).GetMethod("RollChar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var args = new object[] { false };
                int c = (int)mi.Invoke(r, args);
                byRar[Island.Roster[c].Rarity]++;
                if ((bool)args[0]) golden++;
            }
            T.Check("mineros: salen las 4 rarezas y la legendaria es rara", byRar[0] > byRar[1] && byRar[1] > byRar[2] && byRar[2] > byRar[3] && byRar[3] > 30 && byRar[3] < 400, string.Join(",", byRar));
            T.Check("mineros: el dorado sale ~1 de cada 300", golden > 4 && golden < 50, golden.ToString());
            // especialidad: pica su mineral mucho mas rapido; el maestro, todo un poco mejor
            var t = new Island(53);
            var basic = new Miner { Char = 0, Level = 1 };
            var stone = new Ore { Kind = Island.OreStone };
            var gold = new Ore { Kind = Island.OreGold };
            T.Check("mineros: el de Piedra pica piedra x2.2", Math.Abs(Island.SpecBonus(basic, stone) - Island.SpecMult) < 1e-4 && Math.Abs(Island.SpecBonus(basic, gold) - 1f) < 1e-4);
            T.Check("mineros: el de Oro pica oro x2.2", Math.Abs(Island.SpecBonus(new Miner { Char = 4 }, gold) - Island.SpecMult) < 1e-4);
            T.Check("mineros: el Maestro pica todo x1.5", Math.Abs(Island.SpecBonus(new Miner { Char = 8 }, stone) - Island.MasterMult) < 1e-4);
            T.Check("mineros: el de Cristal saca los cristales de noche", Island.SpecBonus(new Miner { Char = 5 }, new Ore { Kind = Island.OreGem, NightCrystal = true }) > 2f);
            T.Check("mineros: la etapa visual va de 1 a 3 con el nivel", Island.Tier(new Miner { Level = 1 }) == 1 && Island.Tier(new Miner { Level = 5 }) == 2 && Island.Tier(new Miner { Level = 10 }) == 3);
            T.Check("mineros: el dorado rinde x1.5", Math.Abs(t.HitMult(new Miner { Char = 0, Level = 1, Golden = true }) - 1.5f) < 1e-4);
            // nivel
            var lv = new Island(54);
            lv.TotalEarned = 3000;
            bool up = false;
            lv.MinerLevelUp += x => up = true;
            Run(lv, 120f);
            T.Check("mineros: picando suben de nivel", up && lv.Miners[0].Level >= 2, lv.Miners[0].Level.ToString());
            T.Check("mineros: el nivel suma rendimiento", lv.HitMult(new Miner { Char = 0, Level = 5 }) > lv.HitMult(new Miner { Char = 0, Level = 1 }));
            // amistad: dos mineros picando la gigante juntos
            var f = new Island(55);
            f.Coins = 1e6; f.TotalEarned = 5000;
            IslandSuite.Now(f, IslandSuite.Rich(f, f.Coins, 1).Upgrade(f.Find(BKind.House)));
            bool friends = false;
            f.BecameFriends += (x, y) => friends = true;
            for (int i = 0; i < 12 && !friends; i++) { foreach (var mm in f.Miners) { mm.Energy = 100f; mm.Clean = 100f; } var g = f.SpawnGiant(); if (g != null) { g.Hp = g.MaxHp = 1e9; } Run(f, 30f); foreach (var o in f.OreList) if (o.Giant) o.Dead = true; }
            T.Check("mineros: picando juntos se hacen amigos", friends && f.Miners[0].Friend == f.Miners[1].Id);
            // guardado de identidades
            var s1 = new Island(56);
            s1.Miners[0].Level = 4; s1.Miners[0].Golden = true; s1.Found[7] = true;
            var s2 = new Island(2);
            s2.LoadJson(s1.ToJson());
            T.Check("mineros: personaje, nivel, dorado y album se guardan", s2.Miners[0].Level == 4 && s2.Miners[0].Golden && s2.Found[7] && s2.FoundGolden[0]);
        }

        static void World()
        {
            var w = new Island(41);
            w.TotalEarned = 5000;
            T.Check("mundo: arranca de dia", w.Night == 0f);
            w.DayClock = Island.DayLength * 0.8f;
            T.Check("mundo: a 0.8 del dia es de noche", w.Night == 1f && w.IsNight);
            w.DayClock = Island.DayLength * 0.665f;
            T.Check("mundo: el atardecer es naranja", w.Dusk > 0.9f);
            w.DayClock = Island.DayLength * 0.75f;
            Run(w, 60f);
            int crystals = 0;
            foreach (var o in w.OreList) if (o.NightCrystal && !o.Dead) crystals++;
            T.Check("mundo: de noche aparecen cristales nocturnos", crystals >= 1 && crystals <= Island.MaxCrystals, crystals.ToString());
            Ore cr = w.OreList.Find(o => o.NightCrystal && !o.Dead);
            Run(w, 20f);
            T.Check("mundo: los mineros no pican los cristales nocturnos", w.Miners.TrueForAll(m => m.Target != cr.Id));
            int gems = w.Gems;
            cr.Age = 5f;
            for (int i = 0; i < 10 && !cr.Dead; i++) w.TapOre(cr, 1);
            T.Check("mundo: tocar un cristal nocturno da una gema", cr.Dead && w.Gems == gems + 1);
            bool morning = false, faded = false;
            w.Morning += () => morning = true;
            w.OreFaded += o => faded = true;
            w.SpawnCrystal();
            w.DayClock = Island.DayLength * 0.925f;
            Run(w, 10f);
            T.Check("mundo: al amanecer los cristales se apagan y los mineros despiertan", morning && faded && !w.OreList.Exists(o => o.NightCrystal && !o.Dead), "m=" + morning + " f=" + faded + " c=" + w.OreList.Count + " day=" + w.DayPhase);
            var r = new Island(42);
            r.TotalEarned = 5000;
            for (int i = 0; i < 4; i++) r.SpawnOre(false, 1);
            Run(r, 2f);
            double p0 = r.PriceMult();
            bool rainbow = false; Ore struck = null;
            r.RainbowStarted += () => rainbow = true;
            r.Lightning += o => struck = o;
            r.StartWeather(Weather.Storm);
            Run(r, 15f);
            T.Check("clima: en la tormenta un rayo convierte una veta en cristal", struck != null && struck.Kind == 4);
            Run(r, 60f);
            T.Check("clima: la tormenta termina con arcoiris", r.Sky == Weather.Clear && rainbow && r.RainbowT > 0f);
            T.Check("clima: el arcoiris da +50 %", Math.Abs(r.PriceMult() - p0 * 1.5) < 1e-9);
            var m = new Island(43);
            m.TotalEarned = 5000;
            int before = m.OreList.Count;
            int sky = 0;
            m.OreSpawned += o => { if (o.Sky) sky++; };
            m.StartWeather(Weather.Meteors);
            Run(m, 16f);
            T.Check("clima: la lluvia de meteoritos trae varias vetas del cielo", sky >= 4 && m.Sky == Weather.Clear, sky.ToString());
            var sv = new Island(44);
            sv.DayClock = 123f;
            var ld = new Island(1);
            ld.LoadJson(sv.ToJson());
            T.Check("mundo: la hora del dia se guarda", Math.Abs(ld.DayClock - 123f) < 0.01f);
        }

        static Ore Fresh(Island isl, int kind)
        {
            var o = isl.SpawnOre(false, kind);
            o.Age = 5f;
            o.Hp = o.MaxHp = 1e9;   // que no se rompa
            return o;
        }

        static void Tapping()
        {
            var t = new Island(31);
            t.TotalEarned = 5000;
            var o = Fresh(t, 0);
            int crits = 0;
            for (int i = 0; i < 1200; i++) { t.TapOre(o, 0); if (t.LastCrit) crits++; }
            T.Check("picar: los criticos salen ~1 de cada 12", crits > 60 && crits < 150, crits.ToString());
            var p = new Island(32);
            p.TotalEarned = 5000;
            var o2 = Fresh(p, 1);
            double c0 = p.Coins;
            double got = p.TapOre(o2, 5);
            T.Check("picar: con poco combo un golpe que no rompe no paga", got == 0 && p.Coins == c0);
            got = p.TapOre(o2, Island.CoinCombo);
            T.Check("picar: desde combo x20 cada golpe deja una pizca", got > 0 && p.Coins > c0);
            bool started = false;
            p.FrenzyStarted += () => started = true;
            p.TapOre(o2, Island.FrenzyCombo);
            T.Check("picar: combo x30 arranca el frenesi", started && p.FrenzyT > 0f);
            started = false;
            p.TapOre(o2, Island.FrenzyCombo + 1);
            T.Check("picar: el frenesi no se reinicia en cada toque", !started);
            Run(p, Island.FrenzyTime + 0.2f);
            T.Check("picar: el frenesi dura 5 s", p.FrenzyT <= 0f);
            var g = new Island(33);
            g.TotalEarned = 20000;
            var gem = g.SpawnOre(false, 4);
            gem.Age = 5f;
            int gems0 = g.Gems;
            for (int i = 0; i < 200 && !gem.Dead; i++) g.TapOre(gem, 10);
            T.Check("picar: romper una veta de gema da una gema", gem.Dead && g.Gems == gems0 + 1 && g.LastBroke);
        }
    }
}
