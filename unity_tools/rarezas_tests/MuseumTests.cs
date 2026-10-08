using System;
using Rarezas.Core;

namespace RarezasTests
{
    public static class MuseumTests
    {
        public static void Run()
        {
            Console.WriteLine("== Museo, eventos y grua ==");
            var p = Progress.New(11);
            T.Check("sin museo no hay entrada", p.Ticket == 0);
            p.Stage = 4;
            p.Deals = 20;
            p.Displays.Clear();
            p.Displays.Add(new Placed { Kind = DisplayKind.GlassCase, Item = new ItemInst(Items.IndexOf("crown"), Condition.Perfect) });
            p.Displays.Add(new Placed { Kind = DisplayKind.GlassCase, Item = new ItemInst(Items.IndexOf("chalice"), Condition.Perfect) });
            int t0 = p.Ticket;
            T.Check("museo cobra entrada", t0 > 0);
            T.Check("exhibir pasa a la sala", p.ToggleExhibit(0) && p.Displays[0].Exhibit);
            T.Check("lo exhibido sube la entrada", p.Ticket > t0, p.Ticket + " vs " + t0);
            T.Check("lo exhibido no se vende", p.Interest(Kind.Tourist, 0) == 0);
            double si = p.SpawnInterval;
            p.ToggleExhibit(1);
            T.Check("la sala atrae gente", p.SpawnInterval < si);
            long c = p.Coins;
            int tk = p.ChargeTicket();
            T.Check("la boleteria cobra", p.Coins == c + tk && p.Tickets == tk);
            var g = Progress.New(2);
            g.Stage = 1;
            T.Check("en la galeria no hay sala", !g.ToggleExhibit(0));

            int[] counts = new int[6];
            for (int d = 0; d < 200; d++) { Set s; counts[(int)Events.Of(d, 3, out s)]++; if (Events.Of(d, 3, out s) == DayEvent.CollectorVisit && s == Set.None) counts[0] = -999; }
            T.Check("eventos variados y deterministas", counts[1] > 20 && counts[2] > 15 && counts[4] > 5 && counts[0] > 20, string.Join(",", counts));
            Set s0;
            T.Check("el primer dia no hay evento", Events.Of(0, 3, out s0) == DayEvent.None);
            var q = Progress.New(4);
            q.Stage = 3;
            q.Deals = 20;
            int day = 0; Set ss = Set.None;
            for (int d = 1; d < 50; d++) if (Events.Of(d, 3, out ss) == DayEvent.CollectorVisit) { day = d; break; }
            q.GameSeconds = day * DayClock.RealSecondsPerDay + 10 * 60;
            T.Check("dia del coleccionista: viene mas", q.KindWeightToday(Kind.Collector) > q.KindWeight(Kind.Collector));

            var r = new Rhythm();
            bool re, ok;
            int guard = 0;
            while (!r.Done && guard++ < 5000)
            {
                double next = r.BeatAt(r.Beat);
                double dt = 1.0 / 60;
                if (Math.Abs(r.T - next) < dt * 0.6) r.Tap(out re, out ok);
                r.Tick(dt, out re, out ok);
            }
            T.Check("ritmo a tiempo: 3 rondas sin repetir", r.Done && r.Retries == 0, r.Retries + "");
            var bad = new Rhythm();
            for (int i = 0; i < 600; i++) bad.Tick(1.0 / 60, out re, out ok);
            T.Check("sin tocar se repite la ronda", !bad.Done && bad.Retries > 0);
            var h = Progress.New(3);
            h.Stage = 4; h.Grant(300000);
            T.Check("grua en el museo por 300.000", h.BuyCrane() && h.HasCrane);
            var back = Progress.FromSave(h.ToSave(0), 3);
            T.Check("grua y entradas se guardan", back.HasCrane);
        }
    }
}
