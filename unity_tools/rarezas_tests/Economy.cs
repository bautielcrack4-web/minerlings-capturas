using System;
using System.Collections.Generic;
using Rarezas.Core;

namespace RarezasTests
{
    /// <summary>
    /// Simulacion de la primera hora con el bot lector manejando a Pipo (DIRECCION_CREATIVA 9, ritmo objetivo):
    /// ir al bosque, juntar, volver, exhibir, abrir y regatear. Tiempos medidos para el mapa de la fase 1 (bosque a ~20 s,
    /// cargando un mediano se vuelve a 1.9 m/s). Mide ganancia por hora, perfectos por hora y que nunca te quedes trabado.
    /// </summary>
    public static class Economy
    {
        public const double ToForest = 28, BackLight = 28, BackCarry = 46, PerSpot = 12, Place = 4, Negotiate = 16, Browse = 8;
        public const int ForestSpots = 20;
        /// <summary>Una persona no encadena acciones como un bot: mira, duda, se distrae (+20%).</summary>
        public const double Human = 1.2;

        sealed class Cu
        {
            public Kind Kind;
            public double Ready;   // cuando aparece el globito
            public double Expire;
            public int Display;
        }

        public sealed class Report
        {
            public double FirstSale = -1, FirstPerfect = -1, Barn = -1, Cart = -1;
            public int Sales10, Perfects10, Sales60, Perfects60;
            public long Earned60;
            public double LongestGap;
            public int Stage60;
        }

        public static Report Sim(int seed, double minutes = 60)
        {
            var rng = new Random(seed);
            var p = Progress.New(seed);
            Spots.Seed(p.Spots, Zone.Forest, ForestSpots, Weight.Medium, 0, rng);
            Spots.Refill(p.Spots, 0, rng);
            var rep = new Report();
            var custs = new List<Cu>();
            double t = 0, busyUntil = 0, nextCust = 0, lastSale = 0;
            bool atShop = true, cartBought = false;
            ItemInst arms = ItemInst.None;
            p.OpenSign = false;
            double end = minutes * 60;
            while (t < end)
            {
                Spots.Refill(p.Spots, t, rng);
                p.GameSeconds += 1;
                // clientes
                if (p.OpenSign && p.ItemsOnDisplay() > 0 && t >= nextCust && custs.Count < 4)
                {
                    var k = p.NextKind();
                    if (Customers.Get(k).Buys)
                    {
                        int disp = -1;
                        var opts = new List<int>();
                        for (int i = 0; i < p.Displays.Count; i++) if (!p.Displays[i].Free && p.Displays[i].Holder < 0) opts.Add(i);
                        if (opts.Count > 0) disp = opts[rng.Next(opts.Count)];
                        if (disp >= 0 && rng.NextDouble() < p.Interest(k, disp))
                        {
                            p.Displays[disp].Holder = 1;
                            custs.Add(new Cu { Kind = k, Display = disp, Ready = t + Browse, Expire = t + Browse + Customers.Get(k).BubbleSeconds });
                        }
                    }
                    nextCust = t + p.SpawnInterval * (0.75 + 0.5 * rng.NextDouble());
                }
                // el hueco sin ventas solo cuenta en horario de atencion (de noche es para salir a buscar)
                if (!DayClock.IsBusinessHours(p.Hour)) lastSale = Math.Max(lastSale, t);
                for (int i = custs.Count - 1; i >= 0; i--)
                    if (t > custs[i].Expire) { p.Displays[custs[i].Display].Holder = -1; custs.RemoveAt(i); }

                if (t >= busyUntil)
                {
                    if (!atShop)
                    {
                        atShop = true;   // llego de vuelta (el viaje ya esta descontado)
                        if (arms.Valid)
                        {
                            int d = p.FreeDisplay(Weight.Medium);
                            if (d >= 0) { p.Put(d, arms); arms = ItemInst.None; busyUntil = t + Place; }
                        }
                        if (!p.OpenSign) p.OpenSign = true;
                    }
                    else
                    {
                        // 1) atender un globito
                        Cu ready = null;
                        foreach (var c in custs) if (t >= c.Ready) { ready = c; break; }
                        if (ready != null)
                        {
                            var deal = p.OpenDeal(ready.Display, ready.Kind);
                            Bots.Reader(deal, rng);
                            var r = deal.Last;
                            p.Apply(ready.Display, r);
                            custs.Remove(ready);
                            busyUntil = t + (p.Deals <= 1 ? 25 : Negotiate) * Human;
                            if (r.Outcome == Outcome.Sold)
                            {
                                if (rep.FirstSale < 0) rep.FirstSale = t;
                                if (r.Perfect && rep.FirstPerfect < 0) rep.FirstPerfect = t;
                                if (t < 600) { rep.Sales10++; if (r.Perfect) rep.Perfects10++; }
                                rep.Sales60++;
                                if (r.Perfect) rep.Perfects60++;
                                rep.LongestGap = Math.Max(rep.LongestGap, t - lastSale);
                                lastSale = t;
                            }
                        }
                        else
                        {
                            // 2) poner lo de la mochila
                            bool placed = false;
                            for (int i = 0; i < p.Bag.Items.Count && !placed; i++)
                            {
                                int d = p.FreeDisplay(Weight.Small);
                                if (d >= 0) { p.Put(d, p.Bag.Take(i)); placed = true; busyUntil = t + Place; }
                            }
                            if (!placed)
                            {
                                // 3) comprar: galpon, exhibidores, carreta
                                if (p.Stage == 0 && p.Coins >= Venue.Stages[1].Price)
                                {
                                    p.Spend(Venue.Stages[1].Price); p.Stage = 1; rep.Barn = t; busyUntil = t + 30;
                                    Spots.Seed(p.Spots, Zone.Beach, 20, Weight.Medium, t, rng);
                                    continue;
                                }
                                if (p.Stage >= 1 && !cartBought && p.Displays.Count >= 4 && p.Coins >= 900) { p.Spend(900); cartBought = true; rep.Cart = t; }
                                if (p.Displays.Count < p.StageDef.MaxDisplays && (cartBought || p.Displays.Count < 4 || p.Stage == 0))
                                {
                                    var kind = p.Displays.Count % 2 == 0 ? DisplayKind.PlankShelf : DisplayKind.AppleCrate;
                                    if (p.Stage >= 1 && p.Displays.Count % 3 == 2) kind = DisplayKind.Table;
                                    long price = Venue.Get(kind).Price;
                                    if (p.Coins >= price + 40) { p.Spend(price); p.Displays.Add(new Placed { Kind = kind }); busyUntil = t + Place; continue; }
                                }
                                if (p.Stage >= 1 && !cartBought && p.Coins >= 900) { p.Spend(900); cartBought = true; rep.Cart = t; }
                                // 4) salir a buscar si hay lugar libre y nadie esta por ofertar
                                bool freeSpot = p.FreeDisplay(Weight.Small) >= 0;
                                bool anyFound = false;
                                foreach (var s in p.Spots) if (s.Has) anyFound = true;
                                bool soon = false;
                                foreach (var c in custs) if (c.Ready - t < 12) soon = true;
                                if (freeSpot && anyFound && !soon)
                                {
                                    // el primer viaje es el del tutorial: el reloj y el farol estan en el borde del bosque
                                    bool firstTrip = p.Deals == 0 && rep.FirstSale < 0;
                                    double trip = firstTrip ? 18 : ToForest;
                                    bool wantMedium = p.FreeDisplay(Weight.Medium) >= 0;
                                    int picked = 0;
                                    foreach (var s in p.Spots)
                                    {
                                        if (!s.Has) continue;
                                        bool med = s.Item.D.Weight == Weight.Medium;
                                        if (med && (!wantMedium || arms.Valid)) continue;
                                        if (!med && p.Bag.Full) continue;
                                        var it = Spots.Take(s, t, rng);
                                        p.NoteFound(it);
                                        if (med) arms = it; else p.Bag.Add(it);
                                        trip += PerSpot;
                                        picked++;
                                        if (p.Bag.Full && (arms.Valid || !wantMedium)) break;
                                        if (p.Deals == 0 && picked >= 2 && arms.Valid) break;   // primer viaje: reloj y farol
                                    }
                                    trip += firstTrip ? 26 : arms.Valid ? BackCarry : BackLight;
                                    if (firstTrip) trip -= picked * PerSpot * 0.5;
                                    atShop = false;
                                    busyUntil = t + trip * Human;
                                }
                            }
                        }
                    }
                }
                t += 1;
            }
            rep.Earned60 = p.Earned;
            rep.Stage60 = p.Stage;
            return rep;
        }

        public static void Run()
        {
            int n = 40;
            double first = 0, perf = 0, sales10 = 0, perf10 = 0, sales60 = 0, perf60 = 0, earned = 0, gap = 0, barn = 0, cart = 0;
            int barnN = 0, cartN = 0, perfN = 0;
            for (int s = 0; s < n; s++)
            {
                var r = Sim(1000 + s);
                first += r.FirstSale;
                if (r.FirstPerfect >= 0) { perf += r.FirstPerfect; perfN++; }
                sales10 += r.Sales10; perf10 += r.Perfects10; sales60 += r.Sales60; perf60 += r.Perfects60; earned += r.Earned60;
                gap = Math.Max(gap, r.LongestGap);
                if (r.Barn >= 0) { barn += r.Barn; barnN++; }
                if (r.Cart >= 0) { cart += r.Cart; cartN++; }
            }
            T.Info("economia (promedio de " + n + " partidas, bot lector):");
            T.Info("  primera venta " + (first / n).ToString("0") + " s; primer perfecto " + (perfN > 0 ? (perf / perfN / 60).ToString("0.0") + " min" : "-"));
            T.Info("  a los 10 min: " + (sales10 / n).ToString("0.0") + " ventas, " + (perf10 / n).ToString("0.0") + " perfectos");
            T.Info("  en 1 hora: " + (sales60 / n).ToString("0") + " ventas, " + (perf60 / n).ToString("0") + " perfectos, " + (earned / n).ToString("0") + " monedas ganadas");
            T.Info("  galpon en " + (barnN > 0 ? (barn / barnN / 60).ToString("0") + " min (" + barnN + "/" + n + ")" : "-") + "; carreta en " + (cartN > 0 ? (cart / cartN / 60).ToString("0") + " min (" + cartN + "/" + n + ")" : "-"));
            T.Info("  hueco mas largo sin vender: " + (gap / 60).ToString("0.0") + " min");
            T.Check("ritmo: primera venta antes del minuto 2", first / n < 120, (first / n).ToString("0"));
            T.Check("ritmo: 6-8 ventas a los 10 minutos", sales10 / n >= 5.5 && sales10 / n <= 9, (sales10 / n).ToString("0.0"));
            T.Check("ritmo: primer perfecto en los primeros 10 minutos", perfN >= n * 0.8 && perf / perfN < 600);
            T.Check("ritmo: galpon y carreta en la primera hora (casi siempre)", barnN >= n * 0.75 && cartN >= n * 0.6, barnN + "/" + cartN);
            T.Check("nunca te quedas trabado (de dia, sin ventas mas de 6 min)", gap < 360, (gap / 60).ToString("0.0"));
        }
    }
}
