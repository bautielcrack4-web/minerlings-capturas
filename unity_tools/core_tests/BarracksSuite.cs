using System;
using System.Collections.Generic;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Complejo minero (0.11+): grilla LEGO, conectores, cadenas, energia, vida, secretos, guardado y fuzz.</summary>
    public static class BarracksSuite
    {
        static void Run(Island isl, float seconds) { for (float t = 0; t < seconds; t += 0.05f) isl.Tick(0.05f); }

        static void Fill(Island isl) { for (int k = 0; k < isl.Stock.Length; k++) isl.Stock[k] = 99999; isl.Coins = 1e15; }

        /// <summary>Isla rica con el Cuartel en el nivel pedido (sin pasar por las obras).</summary>
        public static Island Complex(int seed, int level)
        {
            var isl = IslandSuite.Rich(new Island(seed), 1e15, 9);
            isl.Tut = Island.TutStep.Done;
            isl.Expand = 3;
            var free = isl.Plots.Find(p => isl.Offered(p));
            IslandSuite.Now(isl, isl.Build(BKind.Barracks, free));
            var b = isl.Find(BKind.Barracks);
            b.Level = level;
            isl.Tick(0.02f);
            Fill(isl);
            return isl;
        }

        public static void Go()
        {
            Basics();
            Chains();
            Energy();
            Synergies();
            Floors();
            LifeEvents();
            Secrets();
            ContractsTest();
            Circulation();
            SaveAndMigrate();
            Fuzz();
            EconomySim();
        }

        static void Basics()
        {
            var isl = IslandSuite.Rich(new Island(80), 1e9, 6);
            isl.Tut = Island.TutStep.Done;
            T.Check("complejo: sin Cuartel no hay celdas ni habitaciones", isl.FreeCells(ModKind.Dorm).Count == 0 && isl.RoomState(1) == 0);
            var c = Complex(81, 1);
            var central = c.FirstMod(ModKind.Central);
            T.Check("complejo: al construir el Cuartel aparece la Sala central en (0,0)", central != null && central.X == 0 && central.Z == 0 && c.Modules.Count == 1);
            var cells = c.FreeCells(ModKind.Dorm);
            T.Check("complejo: nivel 1 = una celda al frente", cells.Count == 1 && cells[0][0] == 0 && cells[0][1] == -1 && cells[0][2] == 0);
            int cap0 = c.MinerCap(), n0 = c.Miners.Count;
            var r = c.BuyRoom(1);
            T.Check("complejo: el dormitorio de Cobre se compra y trae a su especialista", r != null && c.HasRoom(1) && c.MinerCap() == cap0 + 1 && c.Miners.Count == n0 + 1 && c.Miners[c.Miners.Count - 1].Char == 1);
            T.Check("complejo: sin lugar no entra otro", c.BuyRoom(2) == null && c.RoomState(2) == 0);
            c.BarracksPlot.Level = 2; c.Dirty();
            T.Check("complejo: nivel 2 suma la celda de atras", c.FreeCells(ModKind.Corridor).Count == 1);
            c.BarracksPlot.Level = 3; c.Dirty();
            T.Check("complejo: nivel 3 suma los laterales", c.FreeCells(ModKind.Corridor).Count == 3);
            c.BarracksPlot.Level = 4; c.Dirty();
            T.Check("complejo: nivel 4 = nucleo 3x3", c.FreeCells(ModKind.Corridor).Count == 7);
            c.BarracksPlot.Level = 5; c.Dirty();
            T.Check("complejo: nivel 5 = segundo piso sobre lo que hay (cruz)", c.CanPlaceMod(ModKind.Corridor, 0, -1, 1, 0) && !c.CanPlaceMod(ModKind.Corridor, 1, 1, 1, 0));
            c.BarracksPlot.Level = 7; c.Dirty();
            T.Check("complejo: nivel 7 = patio solo para exteriores", c.CanPlaceMod(ModKind.Windmill, 2, 0, 0, 0) && !c.CanPlaceMod(ModKind.Corridor, 2, 0, 0, 0));
            T.Check("complejo: precios suben por tipo y cantidad", c.ModCost(ModKind.Smelter) > c.ModCost(ModKind.Storage) && c.ModCost(ModKind.Tools) > 0);
            var t1 = c.BuyModule(ModKind.Tools, 1, 0, 0, -1);
            T.Check("complejo: comprar un modulo cobra y lo coloca", t1 != null && c.ModAt(1, 0, 0) == t1);
            double before = c.ModCost(ModKind.Tools);
            T.Check("complejo: el segundo del mismo tipo sale mas caro", before > 0 && c.CountMod(ModKind.Tools) == 1);
            T.Check("complejo: mover es gratis y valida la celda", c.MoveModule(t1, -1, 0, 0, -1) && c.ModAt(-1, 0, 0) == t1 && !c.MoveModule(t1, 0, 0, 0, 0));
            T.Check("complejo: la Sala central no se mueve ni se quita", !c.MoveModule(central, 1, 1, 0, 0) && !c.RemoveModule(central));
            T.Check("complejo: invariantes ok", c.ValidateComplex() == null, c.ValidateComplex());
        }

        static void Chains()
        {
            var c = Complex(82, 4);
            var u = c.BuyModule(ModKind.Unload, 1, -1, 0, -1);
            T.Check("cadena: la Descarga sola mira al frente (fachada a la camara)", u != null && u.Rot == 0);
            int rot = c.BestRotation(ModKind.Smelter, 1, 0, 0);
            T.Check("cadena: el giro automatico conecta el riel con la Descarga", rot == 0, "rot=" + rot);
            var sm = c.BuyModule(ModKind.Smelter, 1, 0, 0, -1);
            var tr = c.BuyModule(ModKind.Treasury, 1, 1, 0, -1);
            T.Check("cadena: Descarga -> Fundicion -> Tesoreria", tr != null && c.RailNext(u).Count == 1 && c.RailNext(sm)[0] == tr);
            double m = c.PathMult(u, Island.OreIron);
            T.Check("cadena: el hierro vale x1.6 x1.25 = x2.0", Math.Abs(m - 2.0) < 1e-6, m.ToString());
            T.Check("cadena: la piedra no se funde (solo x1.25 de venta)", Math.Abs(c.PathMult(u, Island.OreStone) - 1.25) < 1e-6);
            for (int i = 0; i < c.Stock.Length; i++) c.Stock[i] = 0;
            double coins = c.Coins; int bars = c.Stock[(int)Res.IronBar];
            var cart = c.SpawnCart(u, Island.OreIron, 4, 100);
            int arrived = 0; c.CartArrived += (cc, mm) => arrived++;
            Run(c, 2.5f);
            T.Check("cadena: la vagoneta recorre la linea y se vende al final", cart != null && c.Carts.Count == 0 && Math.Abs(c.Coins - coins - 200) < 1.0, (c.Coins - coins).ToString());
            T.Check("cadena: cada modulo hace CLACK (evento por llegada)", arrived == 2);
            T.Check("cadena: la Fundicion manda lingotes a la ciudad", c.Stock[(int)Res.IronBar] > bars);
            // sin final: se cobra al toque sin bono (nunca se pierde)
            var c2 = Complex(83, 4);
            var u2 = c2.BuyModule(ModKind.Unload, 1, -1, 0, -1);
            double k0 = c2.Coins;
            T.Check("cadena: Descarga sin linea cobra directo", c2.SpawnCart(u2, 0, 2, 50) == null && Math.Abs(c2.Coins - k0 - 50) < 1e-6);
            // tope de vagonetas: se paga lo que daria la linea
            for (int i = 0; i < Island.MaxCarts; i++) c.SpawnCart(u, Island.OreIron, 1, 10);
            double k1 = c.Coins;
            T.Check("cadena: con 12 en viaje la siguiente se cobra con su multiplicador", c.SpawnCart(u, Island.OreIron, 1, 10) == null && Math.Abs(c.Coins - k1 - 20) < 1e-6 && c.Carts.Count == Island.MaxCarts);
            // etapas: mas efecto
            sm.Stage = 3; c.Dirty();
            T.Check("cadena: Fundicion etapa 3 = x2.2", Math.Abs(c.PathMult(u, Island.OreIron) - 2.2 * 1.25) < 1e-6);
            T.Check("cadena: tope global x4", c.PathMult(u, Island.OreIron) <= Island.MaxLineMult);
            T.Check("cadena: el offline cuenta la linea", c.ComplexOfflineMult() > 1.0);
            T.Check("cadena: invariantes ok", c.ValidateComplex() == null, c.ValidateComplex());
        }

        static void Energy()
        {
            var c = Complex(84, 5);
            var lab = c.BuyModule(ModKind.Lab, 0, -1, 0, -1);
            T.Check("energia: el Laboratorio sin cable queda sin energia", lab != null && c.Summary.Unpowered.Contains(lab.Id));
            T.Check("energia: el Generador no va en el primer piso", !c.CanPlaceMod(ModKind.Generator, 0, -1, 1, 0));
            var c2 = Complex(85, 5);
            c2.Stock[(int)Res.Coal] = 0;
            var l2 = c2.BuyModule(ModKind.Lab, 1, -1, 0, -1);
            var g2 = c2.BuyModule(ModKind.Generator, 1, 0, 0, -1);
            T.Check("energia: el giro automatico apunta el cable del Generador al Laboratorio", l2 != null && g2 != null && (c2.CableMaskOf(g2) & 4) != 0, g2 == null ? "null" : g2.Rot.ToString());
            T.Check("energia: sin carbon no hay energia", !c2.FuelOk && c2.Summary.Unpowered.Contains(l2.Id));
            c2.Stock[(int)Res.Coal] = 3;
            T.Check("energia: con carbon se prende (el resumen lo ve sin tocar nada)", c2.Summary.Powered.Contains(l2.Id));
            Run(c2, 61f);
            T.Check("energia: el Generador quema 1 carbon por minuto", c2.Stock[(int)Res.Coal] <= 2);
            c2.Stock[(int)Res.Coal] = 0; c2.FuelT = 0; c2.Dirty();
            T.Check("energia: se apaga cuando se acaba", c2.Summary.Unpowered.Contains(l2.Id));
        }

        static void Synergies()
        {
            var c = Complex(86, 4);
            c.BuyModule(ModKind.Dorm, 0, -1, 0, -1, 1);   // cobre en (0,-1)
            var cr = c.BuyModule(ModKind.Crusher, 1, -1, 0, -1);
            T.Check("vecinos: el Cobre al lado de la Trituradora pica +20 %", cr != null && Math.Abs(c.Summary.CharBonus[1] - 0.2f) < 1e-4, c.Summary.CharBonus[1].ToString());
            var cop = c.Miners.Find(m => m.Char == 1);
            float h1 = c.HitMult(cop);
            c.MoveModule(cr, 1, 1, 0, -1);
            T.Check("vecinos: al alejarla el bono se va (estado identico al anterior)", c.Summary.CharBonus[1] == 0f && c.HitMult(cop) < h1);
            c.MoveModule(cr, 1, -1, 0, -1);
            T.Check("vecinos: y vuelve igual", Math.Abs(c.HitMult(cop) - h1) < 1e-5);
            double prev = c.PreviewDelta(ModKind.Tools, -1, -1, -1, 0, c.BestRotation(ModKind.Tools, -1, -1, 0));
            T.Check("vecinos: la vista previa anticipa la mejora", prev > 0, prev.ToString());
            var tools = c.BuyModule(ModKind.Tools, -1, -1, 0, -1);
            T.Check("vecinos: Herramientas suma +10 % al vecino", tools != null && c.Summary.CharBonus[1] > 0.29f);
            T.Check("vecinos: la vista previa no cambio nada", c.ValidateComplex() == null);
            var partners = c.PreviewPartners(ModKind.Crusher, -1, 1, -1, 0, c.BestRotation(ModKind.Crusher, 1, -1, 0, cr), cr);
            T.Check("vecinos: la vista previa marca con quien se conecta", partners.Contains(c.DormOf(1).Id), string.Join(",", partners));
            // tope 60 %
            var c2 = Complex(87, 9);
            c2.BuyRoom(1);
            var d1 = c2.DormOf(1);
            foreach (var k in new[] { ModKind.Tools, ModKind.Tools, ModKind.Crusher })
            {
                var cells = c2.FreeCells(k);
                foreach (var cell in cells)
                    if (Math.Abs(cell[0] - d1.X) + Math.Abs(cell[1] - d1.Z) == 1 && cell[2] == d1.F) { c2.BuyModule(k, cell[0], cell[1], cell[2], -1); break; }
            }
            d1.Stage = 3; c2.Dirty();
            T.Check("vecinos: el bono por especialista no pasa de +60 %", c2.Summary.CharBonus[1] <= 0.6001f);
            // combinacion facil: Cocina minera (Comedor con 3 dormitorios alrededor, uno arriba)
            var c3 = Complex(88, 6);
            int found = -1; c3.ComboDiscovered += i => found = i;
            var mess = c3.BuyModule(ModKind.Mess, 0, -1, 0, -1);
            c3.Modules.Add(new Module { Id = 900, Kind = ModKind.Dorm, Ch = 1, X = -1, Z = -1 });
            c3.Modules.Add(new Module { Id = 901, Kind = ModKind.Dorm, Ch = 2, X = 1, Z = -1 });
            T.Check("combinacion: con 2 dormitorios todavia no", !c3.Summary.Combos[4]);
            c3.Modules.Add(new Module { Id = 902, Kind = ModKind.Dorm, Ch = 3, X = 0, Z = -1, F = 1 });
            c3.Dirty();
            var s3 = c3.Summary;
            T.Check("combinacion: Comedor entre 3 dormitorios = Cocina minera", mess != null && c3.CombosFound[4] && found == 4);
            T.Check("combinacion: suma animo", s3.Mood >= 80, s3.Mood.ToString());
            T.Check("combinacion: invariantes ok", c3.ValidateComplex() == null, c3.ValidateComplex());
        }

        static void Floors()
        {
            var c = Complex(89, 5);
            T.Check("pisos: arriba sin apoyo no se puede", !c.CanPlaceMod(ModKind.Corridor, 0, -1, 1, 0));
            var a = c.BuyModule(ModKind.Corridor, 0, -1, 0, -1);
            var up = c.BuyModule(ModKind.Mess, 0, -1, 1, -1);
            T.Check("pisos: con apoyo si", a != null && up != null);
            T.Check("pisos: no se puede mover lo que sostiene algo", !c.MoveModule(a, 1, 0, 0, -1));
            T.Check("pisos: ni quitarlo", !c.RemoveModule(a));
            T.Check("pisos: lo de arriba sobre la Sala central tiene acceso por su escalera", c.BuyModule(ModKind.Tools, 0, 0, 1, -1) != null && c.Summary.Access.Count == c.Modules.Count);
            T.Check("pisos: el Observatorio solo va en la torre (nivel 9)", !c.CanPlaceMod(ModKind.Observatory, 0, 0, 1, 0));
            T.Check("pisos: la Fundicion no sube", !c.CanPlaceMod(ModKind.Smelter, 0, -1, 1, 0));
            // acceso: una esquina sin vecinos en los lados no tiene acceso (pero funciona)
            var c2 = Complex(90, 4);
            var corner = c2.BuyModule(ModKind.Tools, 1, 1, 0, -1);
            T.Check("acceso: una esquina aislada no tiene acceso", corner != null && !c2.Summary.Access.Contains(corner.Id));
            c2.BuyModule(ModKind.Corridor, 1, 0, 0, -1);
            T.Check("acceso: con un pasillo al lado si", c2.Summary.Access.Contains(corner.Id));
            T.Check("pisos: invariantes ok", c.ValidateComplex() == null && c2.ValidateComplex() == null);
        }

        static void LifeEvents()
        {
            // Observatorio: avisa la lluvia de meteoritos un minuto antes
            var o = Complex(99, 9);
            o.TotalEarned = 5000;
            o.BuyModule(ModKind.Corridor, 0, -1, 0, -1);
            o.BuyModule(ModKind.Tools, 0, 0, 1, -1);
            var obs = o.BuyModule(ModKind.Observatory, 0, 0, 2, -1);
            int warned = 0, meteors = 0;
            o.MeteorSoon += () => warned++;
            o.WeatherChanged += w => { if (w == Weather.Meteors) meteors++; };
            for (float t = 0; t < 7200f; t += 0.1f) o.Tick(0.1f);
            T.Check("observatorio: va en la torre y avisa cada lluvia de meteoritos", obs != null && meteors > 0 && warned >= meteors, warned + "/" + meteors);
            var c = Complex(91, 6);
            c.BuyRoom(1); c.BuyRoom(2);
            var u = c.BuyModule(ModKind.Unload, 1, -1, 0, -1);
            var sm = c.BuyModule(ModKind.Smelter, 1, 0, 0, -1);
            var tr = c.BuyModule(ModKind.Treasury, 1, 1, 0, -1);
            T.Check("eventos: se arma la linea", u != null && sm != null && tr != null);
            Run(c, 2f);
            T.Check("eventos: avería", c.StartCxEvent(CxEventKind.Breakdown) && c.CurEvent != null && c.ModById(c.CurEvent.ModId).Broken);
            var broken = c.ModById(c.CurEvent.ModId);
            bool sent = c.SendFixer();
            T.Check("eventos: va el especialista que sabe", sent || broken.Kind == ModKind.Smelter == false);
            Run(c, 60f);
            T.Check("eventos: la maquina queda arreglada y afinada", !broken.Broken && (broken.TunedT > 0 || !sent) && c.CurEvent == null);
            T.Check("eventos: comerciante", c.StartCxEvent(CxEventKind.Merchant));
            var e = c.CurEvent;
            T.Check("eventos: vende +50 % cerca", c.MerchantMult(c.ModAt(e.X, e.Z, 0)) == 1.5);
            var other = c.Boundary().Find(m => m.Id != e.ModId);
            T.Check("eventos: el comerciante se muda una sola vez", other == null || (c.MoveMerchant(other.X, other.Z) && !c.MoveMerchant(e.X, e.Z)));
            c.CurEvent = null;
            T.Check("eventos: inspector da cofres", c.StartCxEvent(CxEventKind.Inspector) && c.ClaimInspector() >= 0 && c.ChestCount > 0);
            c.SpawnCart(u, Island.OreIron, 2, 100);
            T.Check("eventos: vagoneta trabada", c.StartCxEvent(CxEventKind.Jam) && c.JamCart >= 0);
            double k = c.Coins;
            c.TapJam(); c.TapJam(); c.TapJam();
            Run(c, 3f);
            T.Check("eventos: 3 toques la liberan con el doble", c.JamCart < 0 && c.Coins - k > 300, (c.Coins - k).ToString());
            // portatiles
            c.AddPortable(ModKind.PartyBell);
            var free = c.FreeCells(ModKind.PartyBell)[0];
            var bell = c.PlacePortable(ModKind.PartyBell, free[0], free[1], free[2], -1);
            int mood = c.Summary.Mood;
            T.Check("portatil: se coloca y suma animo", bell != null && bell.Expires > 0 && mood >= 60);
            Run(c, (float)Island.PortableSeconds + 1f);
            T.Check("portatil: a los 20 min se pliega", c.ModById(bell.Id) == null);
            // aula: el Maestro ensenia al vecino
            var c2 = Complex(92, 9);
            var cl = c2.BuyModule(ModKind.Classroom, 0, -1, 0, -1);
            c2.Modules.Add(new Module { Id = 990, Kind = ModKind.Dorm, Ch = 8, X = -1, Z = -1 });
            c2.Modules.Add(new Module { Id = 991, Kind = ModKind.Dorm, Ch = 1, X = 1, Z = -1 });
            c2.Dirty();
            var pupil = c2.Miners[0];
            pupil.Char = 1; pupil.Level = 1; pupil.Xp = 0;
            int lv0 = pupil.Level, xp0 = pupil.Xp;
            Run(c2, 60f);
            T.Check("aula: el alumno gana experiencia", cl != null && (pupil.Level > lv0 || pupil.Xp > xp0 + 20), pupil.Level + "/" + pupil.Xp);
        }

        static void Secrets()
        {
            var c = Complex(93, 3);
            T.Check("secreta: sin plano no se construye", !c.CanBuySecret());
            int got = 0; c.PlansFound += n => got += n;
            c.AddPlans(5);
            T.Check("secreta: los planos tienen tope 3", c.Plans == Island.MaxPlans && got == 3);
            var cell = c.FreeCells(ModKind.Secret)[0];
            var s = c.BuySecret(cell[0], cell[1], cell[2], -1);
            T.Check("secreta: se construye con un plano (2 min de obra)", s != null && c.Plans == 2 && s.Work > 0);
            SecretOut outc = SecretOut.Gems; bool rev = false;
            c.SecretRevealed += (m, o, d) => { rev = true; outc = o; };
            Run(c, 121f);
            T.Check("secreta: se revela", rev && s.Kind != ModKind.Secret);
            // garantia: en 3 salas sale algo nuevo
            int news = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                var k = Complex(200 + seed, 6);
                int n = 0;
                k.SecretRevealed += (m, o, d) => { if (o == SecretOut.Miner || o == SecretOut.Machine || o == SecretOut.NewRoom) n++; };
                for (int i = 0; i < 3; i++)
                {
                    k.AddPlans(1);
                    var cc = k.FreeCells(ModKind.Secret);
                    if (cc.Count == 0) break;
                    if (k.BuySecret(cc[0][0], cc[0][1], cc[0][2], -1) == null) break;
                    Run(k, 121f);
                }
                if (n > 0) news++;
                if (k.ValidateComplex() != null) { T.Check("secreta: invariantes", false, k.ValidateComplex()); break; }
            }
            T.Check("secreta: garantia de algo nuevo cada 3", news == 30, news + "/30");
        }

        static void ContractsTest()
        {
            var c = Complex(94, 6);
            T.Check("contratos: sin linea no hay", c.MakeContract() == null);
            c.BuyRoom(1);
            c.BuyModule(ModKind.Unload, 1, -1, 0, -1);
            c.BuyModule(ModKind.Smelter, 1, 0, 0, -1);
            c.BuyModule(ModKind.Treasury, 1, 1, 0, -1);
            bool okAll = true;
            for (int i = 0; i < 1000; i++)
            {
                var k = c.MakeContract();
                if (k == null || k.Target <= 0 || k.Time < 240 || k.Coins <= 0) { okAll = false; break; }
                if (k.Kind == 2 && c.FirstMod((ModKind)k.What) == null) { okAll = false; break; }
            }
            T.Check("contratos: 1000 generados son validos (lo pedido existe)", okAll);
            // simular: se cumplen jugando
            int done = 0, total = 0; string dbg = "";
            for (int seed = 0; seed < 8; seed++)
            {
                var k = Complex(300 + seed, 6);
                k.TotalEarned = 20000;
                k.BuyRoom(1);
                k.BuyModule(ModKind.Unload, 1, -1, 0, -1);
                k.BuyModule(ModKind.Smelter, 1, 0, 0, -1);
                k.BuyModule(ModKind.Treasury, 1, 1, 0, -1);
                Run(k, 30f);
                k.ContractNewDay(1);
                Run(k, 5f);
                if (!k.AcceptContract(0)) continue;
                total++;
                bool ok = false;
                k.ContractEnded += (ct, win) => ok = win;
                var ac = k.ActiveContract;
                Run(k, ac.Time + 2f);
                if (ok) done++;
                dbg += " [" + ac.Kind + ":" + ac.What + " " + ac.Prog + "/" + ac.Target + " m" + k.Miners.Count + "]";
            }
            T.Check("contratos: jugando se cumplen (>= 6 de 8)", total > 0 && done >= Math.Min(6, total), done + "/" + total + dbg);
        }

        static void Circulation()
        {
            var c = Complex(95, 4);
            c.TotalEarned = 20000;
            c.BuyRoom(1);
            c.BuyModule(ModKind.Unload, 1, -1, 0, -1);
            c.BuyModule(ModKind.Treasury, 1, 0, 0, -1);
            bool inside = false, rested = false;
            for (float t = 0; t < 240f; t += 0.05f)
            {
                c.Tick(0.05f);
                foreach (var m in c.Miners) { if (m.InComplex) inside = true; if (m.State == MState.InDorm) rested = true; }
                if (t > 100f && t < 101f) foreach (var m in c.Miners) m.Energy = Math.Min(m.Energy, 20f);
            }
            T.Check("circulacion: los mineros entran al Complejo y descargan en vagonetas", inside && c.Stat("carts") > 0, "carts=" + c.Stat("carts"));
            T.Check("circulacion: descansan en su dormitorio", rested);
            T.Check("circulacion: nadie queda atrapado adentro mucho tiempo", c.Miners.TrueForAll(m => !(m.State == MState.Leaving && m.T > 30f)));
        }

        static void SaveAndMigrate()
        {
            var c = Complex(96, 6);
            c.BuyRoom(1); c.BuyRoom(2);
            c.BuyModule(ModKind.Unload, 1, -1, 0, -1);
            var sm = c.BuyModule(ModKind.Smelter, 1, 0, 0, -1);
            sm.Stage = 2;
            c.AddPlans(2); c.AddPortable(ModKind.PortLab);
            c.CombosFound[3] = true;
            var json = c.ToJson();
            var d = new Island(1);
            d.LoadJson(json);
            T.Check("guardado: modulos, etapas, planos, portatiles y libro vuelven igual",
                d.Modules.Count == c.Modules.Count && d.FirstMod(ModKind.Smelter).Stage == 2 && d.Plans == 2 && d.PortInv[0] == 1 && d.CombosFound[3] && d.HasRoom(2),
                d.Modules.Count + " vs " + c.Modules.Count);
            T.Check("guardado: los mineros de los dormitorios siguen", d.MinerCap() == c.MinerCap() && d.Miners.Count == c.Miners.Count);
            T.Check("guardado: invariantes ok", d.ValidateComplex() == null, d.ValidateComplex());
            // migracion de 0.10: las 256 combinaciones de encastres
            int bad = 0; string why = "";
            for (int mask = 0; mask < 256; mask++)
            {
                for (int lv = 1; lv <= 9; lv += 2)
                {
                    var k = Complex(97, lv);
                    var save = k.ToJson();
                    var rooms = new List<string>();
                    int ch = 1;
                    for (int s = 0; s < 8; s++) if ((mask & (1 << s)) != 0) rooms.Add("[" + (ch++) + "," + s + "]");
                    int i0 = save.IndexOf("\"cx\":");
                    int depth = 0, i1 = i0 + 5;
                    for (; i1 < save.Length; i1++) { if (save[i1] == '{') depth++; if (save[i1] == '}') { depth--; if (depth == 0) { i1++; break; } } }
                    string old = save.Substring(0, i0) + "\"rooms\":[" + string.Join(",", rooms.ToArray()) + "]" + save.Substring(i1);
                    old = old.Replace("\"v\":5", "\"v\":4");
                    var m = new Island(2);
                    try
                    {
                        m.LoadJson(old);
                        string v = m.ValidateComplex();
                        if (v != null) { bad++; why = v; }
                        foreach (var mod in m.Modules) if (mod.Kind == ModKind.Dorm && !m.Summary.Access.Contains(mod.Id) && Math.Abs(mod.X) == 1 && Math.Abs(mod.Z) == 1) { bad++; why = "esquina sin acceso"; }
                    }
                    catch (Exception ex) { bad++; why = ex.Message; }
                }
            }
            T.Check("migracion: 256 combinaciones x 5 niveles sin errores", bad == 0, bad + " " + why);
        }

        /// <summary>20 000 operaciones al azar: despues de cada una se validan las reglas; guardar y cargar da lo mismo.</summary>
        static void Fuzz()
        {
            var rnd = new Random(1234);
            var c = Complex(98, 9);
            c.TotalEarned = 1e6;
            string fail = null; int ops = 0;
            var kinds = (ModKind[])Enum.GetValues(typeof(ModKind));
            for (int i = 0; i < 20000 && fail == null; i++)
            {
                ops++;
                try
                {
                    int op = rnd.Next(10);
                    if (op <= 3)
                    {
                        var k = kinds[rnd.Next(kinds.Length)];
                        int x = rnd.Next(-2, 3), z = rnd.Next(-2, 3), f = rnd.Next(3), r = rnd.Next(-1, 4);
                        if (k == ModKind.Dorm) c.BuyModule(k, x, z, f, r, 1 + rnd.Next(8));
                        else if (Array.IndexOf(Island.PortableKinds, k) >= 0) { c.AddPortable(k); c.PlacePortable(k, x, z, f, r); }
                        else c.BuyModule(k, x, z, f, r);
                    }
                    else if (op == 4 && c.Modules.Count > 0) { var m = c.Modules[rnd.Next(c.Modules.Count)]; c.MoveModule(m, rnd.Next(-2, 3), rnd.Next(-2, 3), rnd.Next(3), rnd.Next(-1, 4)); }
                    else if (op == 5 && c.Modules.Count > 0) { var m = c.Modules[rnd.Next(c.Modules.Count)]; c.RotateModule(m, rnd.Next(4)); }
                    else if (op == 6 && c.Modules.Count > 0) { var m = c.Modules[rnd.Next(c.Modules.Count)]; c.RemoveModule(m); }
                    else if (op == 7 && c.Modules.Count > 0) { var m = c.Modules[rnd.Next(c.Modules.Count)]; c.Evolve(m); }
                    else if (op == 8) { c.Stock[(int)Res.Coal] = rnd.Next(2) * 50; if (rnd.Next(20) == 0) c.StartCxEvent(); }
                    else { for (int t = 0; t < 10; t++) c.Tick(0.05f); }
                    Fill(c);
                    fail = c.ValidateComplex();
                    if (i % 2000 == 1999)
                    {
                        var j = c.ToJson();
                        var d = new Island(5);
                        d.LoadJson(j);
                        if (d.Modules.Count != c.Modules.Count) fail = "guardado distinto " + d.Modules.Count + " vs " + c.Modules.Count;
                        else if (d.ValidateComplex() != null) fail = "cargado: " + d.ValidateComplex();
                    }
                }
                catch (Exception ex) { fail = ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
            }
            T.Check("fuzz: 20 000 operaciones sin romper ninguna regla", fail == null, "op " + ops + ": " + fail);
            T.Check("fuzz: el complejo crecio (no quedo vacio)", c.Modules.Count >= 10, c.Modules.Count.ToString());
        }

        // ------------------------------------------------------------ simulacion economica con bots
        /// <summary>Isla de mitad de partida con el Cuartel en `level`, monedas y materiales normales (no infinitos).</summary>
        static Island MidGame(int seed, int level)
        {
            var isl = Complex(seed, level);
            isl.TotalEarned = 30000;
            isl.Coins = 40000;
            for (int k = 0; k < isl.Stock.Length; k++) isl.Stock[k] = 60;
            isl.Stock[(int)Res.Coal] = 40;
            return isl;
        }

        /// <summary>Un paso del bot: compra algo cada tanto. mode 0 goloso (vista previa), 1 lindo (orden fijo), 2 al azar.</summary>
        static void BotStep(Island c, int mode, Random rnd)
        {
            var kinds = new List<KeyValuePair<ModKind, int>>();
            for (int ch = 1; ch < Island.Roster.Length; ch++) if (!Island.Roster[ch].Secret && c.CanBuyRoom(ch)) kinds.Add(new KeyValuePair<ModKind, int>(ModKind.Dorm, ch));
            foreach (var d in Island.ModDefs) if (d.Buyable && d.Kind != ModKind.Dorm && c.ModBuyBlock(d.Kind) == 0) kinds.Add(new KeyValuePair<ModKind, int>(d.Kind, -1));
            if (kinds.Count == 0) return;
            if (mode == 2)
            {
                var k = kinds[rnd.Next(kinds.Count)];
                var cells = c.FreeCells(k.Key);
                if (cells.Count == 0) return;
                var cell = cells[rnd.Next(cells.Count)];
                c.BuyModule(k.Key, cell[0], cell[1], cell[2], -1, k.Value);
                return;
            }
            if (mode == 1)
            {
                // lindo: dormitorios y Comedor en orden simetrico, sin mirar numeros
                foreach (var k in kinds)
                {
                    if (k.Key != ModKind.Dorm && k.Key != ModKind.Mess && k.Key != ModKind.Corridor && k.Key != ModKind.Tools) continue;
                    var cells = c.FreeCells(k.Key);
                    if (cells.Count == 0) continue;
                    c.BuyModule(k.Key, cells[0][0], cells[0][1], cells[0][2], -1, k.Value);
                    return;
                }
                return;
            }
            // goloso: lo que mas sube la vista previa
            double best = 0.5; KeyValuePair<ModKind, int> bk = default(KeyValuePair<ModKind, int>); int[] bc = null; int br = -1;
            foreach (var k in kinds)
                foreach (var cell in c.FreeCells(k.Key))
                {
                    int r = c.BestRotation(k.Key, cell[0], cell[1], cell[2]);
                    double d = c.PreviewDelta(k.Key, k.Value, cell[0], cell[1], cell[2], r);
                    if (d > best) { best = d; bk = k; bc = cell; br = r; }
                }
            if (bc != null) c.BuyModule(bk.Key, bc[0], bc[1], bc[2], br, bk.Value);
            else
            {
                // nada sube el numero: un dormitorio (mas mineros) o una Descarga/Tesoreria si faltan
                foreach (var k in kinds)
                    if (k.Key == ModKind.Dorm || k.Key == ModKind.Unload || k.Key == ModKind.Treasury)
                    {
                        var cells = c.FreeCells(k.Key);
                        if (cells.Count == 0) continue;
                        int r = c.BestRotation(k.Key, cells[0][0], cells[0][1], cells[0][2]);
                        c.BuyModule(k.Key, cells[0][0], cells[0][1], cells[0][2], r, k.Value);
                        return;
                    }
            }
        }

        static double PlayBot(int mode, int seed, float minutes)
        {
            var rnd = new Random(seed);
            var c = MidGame(seed, 6);
            double start = c.TotalEarned;
            for (float t = 0; t < minutes * 60f; t += 0.1f)
            {
                c.Tick(0.1f);
                if (((int)(t * 10)) % 300 == 0) { BotStep(c, mode, rnd); for (int k = 0; k < c.Stock.Length; k++) c.Stock[k] = Math.Max(c.Stock[k], 60); }
                if (double.IsNaN(c.Coins) || c.ValidateComplex() != null) return -1;
            }
            return c.TotalEarned - start;
        }

        static void EconomySim()
        {
            double greedy = 0, pretty = 0, random = 0;
            for (int s = 0; s < 3; s++) { greedy += PlayBot(0, 500 + s, 20f); pretty += PlayBot(1, 500 + s, 20f); random += PlayBot(2, 500 + s, 20f); }
            T.Check("economia: los bots juegan 20 min sin romper nada", greedy > 0 && pretty > 0 && random > 0, greedy + " / " + pretty + " / " + random);
            T.Check("economia: el goloso gana mas que el que juega al azar", greedy >= random * 0.95, (greedy / Math.Max(1, random)).ToString("0.00"));
            T.Check("economia: armar lindo no castiga de mas (>= 70 % del goloso)", pretty >= greedy * 0.7, (pretty / Math.Max(1, greedy)).ToString("0.00"));
            // online contra offline con la misma distribucion
            var on = MidGame(600, 6);
            on.BuyRoom(1);
            on.BuyModule(ModKind.Unload, 1, -1, 0, -1);
            on.BuyModule(ModKind.Crusher, 1, 0, 0, -1);
            on.BuyModule(ModKind.Treasury, 1, 1, 0, -1);
            var off = new Island(1);
            off.LoadJson(on.ToJson());
            double e0 = on.TotalEarned;
            for (float t = 0; t < 1200f; t += 0.1f) on.Tick(0.1f);
            double online = on.TotalEarned - e0;
            off.LastSeen = 1000;
            double offline = off.ApplyOffline(1000 + 1200);
            double ratio = offline / Math.Max(1, online);
            T.Check("economia: el offline paga entre 30 y 90 % de jugar (nunca mas que jugar)", ratio > 0.3 && ratio < 0.9, ratio.ToString("0.00") + " on=" + online.ToString("0") + " off=" + offline.ToString("0"));
            T.Check("economia: el Complejo suma al offline", off.ComplexOfflineMult() > 1.0);
        }
    }
}
