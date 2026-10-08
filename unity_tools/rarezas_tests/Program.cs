using System;
using System.Collections.Generic;
using Rarezas.Core;

namespace RarezasTests
{
    /// <summary>Contador de comprobaciones PASS/FAIL (mismo arnes que unity_tools/core_tests).</summary>
    public static class T
    {
        public static int Total, Fails;
        public static bool Quiet = true;

        public static void Check(string name, bool cond, string extra = "")
        {
            Total++;
            if (cond) { if (!Quiet) Console.WriteLine("PASS  " + name + "  " + extra); }
            else { Fails++; Console.WriteLine("FAIL  " + name + "  " + extra); }
        }

        public static void Info(string s) { Console.WriteLine("      " + s); }
    }

    public static partial class Program
    {
        public static int Main(string[] args)
        {
            if (Array.IndexOf(args, "-v") >= 0) T.Quiet = false;
            Catalog();
            Luck();
            Offers();
            Answers();
            Rounds();
            Faces();
            Tells();
            AuctionSuite();
            CarrySuite();
            SpotsSuite();
            ProgressSuite();
            GestureSuite();
            WorkshopSuite();
            LayoutSuite();
            StaffTests.Run();
            MuseumTests.Run();
            GemsTests.Run();
            Bots.Run();
            Economy.Run();
            Console.WriteLine((T.Fails == 0 ? "OK " : "FALLAS ") + (T.Total - T.Fails) + "/" + T.Total + " pruebas");
            return T.Fails == 0 ? 0 : 1;
        }

        public static ItemInst It(string id, Condition c = Condition.Perfect) { return new ItemInst(Items.IndexOf(id), c); }

        // ------------------------------------------------------------ catalogo
        static void Catalog()
        {
            T.Check("catalogo de 40 objetos", Items.All.Length == 40, Items.All.Length + "");
            var ids = new HashSet<string>();
            bool ok = true;
            foreach (var d in Items.All) { if (!ids.Add(d.Id)) ok = false; if (d.Value < 5 || d.Value > 50000) ok = false; }
            T.Check("ids unicos y valores entre 5 y 50.000", ok);
            T.Check("bosque tiene 10", Items.OfZone(Zone.Forest, Weight.Giant).Count == 10);
            T.Check("playa tiene 10", Items.OfZone(Zone.Beach, Weight.Giant).Count == 10);
            T.Check("mazmorra tiene 10", Items.OfZone(Zone.Dungeon, Weight.Giant).Count == 10);
            T.Check("pirata = 5 piezas", Items.CountInSet(Set.Pirate) == 5);
            T.Check("caballero = 4 piezas", Items.CountInSet(Set.Knight) == 4);
            T.Check("estado: sucio -20%, rajado -30%, restaurado +40%",
                Math.Abs(Items.CondMul(Condition.Dirty) - 0.8) < 1e-9 && Math.Abs(Items.CondMul(Condition.Cracked) - 0.7) < 1e-9 && Math.Abs(Items.CondMul(Condition.Restored) - 1.4) < 1e-9);
            T.Check("bonus de lugar por etapa 0/10/25/45/70/100%", Venue.Stages[0].PlaceBonus == 0 && Venue.Stages[1].PlaceBonus == 0.10 && Venue.Stages[5].PlaceBonus == 1.0);
            T.Check("precios de etapa", Venue.Stages[1].Price == 1500 && Venue.Stages[2].Price == 12000 && Venue.Stages[3].Price == 80000 && Venue.Stages[4].Price == 450000 && Venue.Stages[5].Price == 2500000);
            T.Check("estante de tablas acepta chico y mediano, no grande", Venue.Get(DisplayKind.PlankShelf).Accepts(Weight.Medium) && !Venue.Get(DisplayKind.PlankShelf).Accepts(Weight.Large));
            T.Check("vitrina de lujo acepta cualquiera", Venue.Get(DisplayKind.LuxuryCase).Accepts(Weight.Giant) && Venue.Get(DisplayKind.LuxuryCase).Accepts(Weight.Small));
            double v = Venue.PricedValue(It("old_lantern"), DisplayKind.PlankShelf, 0);
            T.Check("valor con bonus de exhibidor +5%", Math.Abs(v - 16 * 1.05) < 1e-6, v + "");
            T.Check("bonus de coleccion 3/4/5 = 15/25/40%", Venue.SetBonus(3) == 0.15 && Venue.SetBonus(4) == 0.25 && Venue.SetBonus(5) == 0.40 && Venue.SetBonus(2) == 0);
            T.Check("mugre sobre 30% baja el bonus (max -25%)", Venue.DirtPenalty(0.2) == 0 && Venue.DirtPenalty(0.5) > 0 && Venue.DirtPenalty(1.0) <= 0.25 + 1e-9);
            T.Check("reloj: 1 dia = 24 min, abre de 9 a 21", DayClock.IsBusinessHours(DayClock.Hour(10 * 60)) && !DayClock.IsBusinessHours(DayClock.Hour(22 * 60)) && Math.Abs(DayClock.Hour(24 * 60) - 0) < 1e-6);
        }

        // ------------------------------------------------------------ suerte y personalidades
        static void Luck()
        {
            var rng = new Random(7);
            int n = 200000, a = 0, b = 0, c = 0, fan = 0;
            for (int i = 0; i < n; i++)
            {
                double l = Deal.DrawLuck(rng);
                if (l >= 2.0) fan++;
                else if (l < 0.85) c++;
                else if (l > 1.15) b++;
                else a++;
            }
            T.Check("suerte: 70% normal", Math.Abs(a / (double)n - 0.70) < 0.01, (a / (double)n).ToString("0.000"));
            T.Check("suerte: 20% alta", Math.Abs(b / (double)n - 0.20) < 0.01, (b / (double)n).ToString("0.000"));
            T.Check("suerte: 7% baja", Math.Abs(c / (double)n - 0.07) < 0.008, (c / (double)n).ToString("0.000"));
            T.Check("suerte: 3% fans", Math.Abs(fan / (double)n - 0.03) < 0.005, (fan / (double)n).ToString("0.000"));

            var cnt = new int[5];
            int tot = 0;
            for (int i = 0; i < 60000; i++)
            {
                var t = Customers.All[i % 6];
                cnt[(int)Deal.DrawPersona(t, rng)]++;
                tot++;
            }
            T.Info("personalidades (exp/poker/exag/mentira/nerv %): " + string.Join(" / ", Array.ConvertAll(cnt, x => (x * 100.0 / tot).ToString("0"))));
            T.Check("hay de todas las personalidades", Array.TrueForAll(cnt, x => x > tot * 0.05));
            T.Check("la expresiva es la mas comun", cnt[0] > cnt[1] && cnt[0] > cnt[3] && cnt[0] > cnt[4]);

            double lo = 1e9, hi = 0;
            for (int i = 0; i < 400; i++)
            {
                var d = new Deal(It("banjo"), 58, Customers.Get(Kind.Tourist), rng);
                lo = Math.Min(lo, d.Max);
                hi = Math.Max(hi, d.Max);
            }
            T.Check("mismo tipo y objeto: el maximo varia mucho (fans incluidos)", hi / lo > 3.0, lo.ToString("0") + ".." + hi.ToString("0"));
        }

        static void Offers()
        {
            var rng = new Random(3);
            bool okOpen = true, okEasy = true, okTaste = true;
            for (int i = 0; i < 20000; i++)
            {
                var t = Customers.All[i % 6];
                var it = new ItemInst(i % Items.All.Length, Condition.Perfect);
                bool easy = i % 4 == 0;
                var d = new Deal(it, it.D.Value, t, rng, easy);
                double f = d.FirstOffer / d.Max;
                if (d.Max > 60 && (f < 0.535 || f > 0.851)) okOpen = false;
                if (easy && (d.Luck < 1.0 || d.Fan || d.Persona != Persona.Expressive || d.StartPatience != t.Patience + 1 || !d.HasTell)) okEasy = false;
                if (d.Taste < 0.6 || d.Taste > 1.6) okTaste = false;
            }
            T.Check("primera oferta entre 0.55 y 0.85 de M", okOpen);
            T.Check("primeras 10: suerte >= 1, sin fan, expresiva, +1 de paciencia y con tell", okEasy);
            T.Check("gusto entre 0.6 y 1.5", okTaste);
            T.Check("turista: le gusta lo nautico", Customers.Taste(Kind.Tourist, Items.Get("compass")) > Customers.Taste(Kind.Tourist, Items.Get("rusty_axe")));
            T.Check("abuelita: le gusta el bosque", Customers.Taste(Kind.Grandma, Items.Get("pocket_watch")) > Customers.Taste(Kind.Grandma, Items.Get("anchor")));
            T.Check("niño: juguetes", Customers.Taste(Kind.Kid, Items.Get("banjo")) >= 1.4 && Customers.Taste(Kind.Kid, Items.Get("throne")) < 0.8);
            T.Check("coleccionista: solo su coleccion", Customers.Taste(Kind.Collector, Items.Get("spyglass"), Set.Pirate) == 1.5 && Customers.Taste(Kind.Collector, Items.Get("chalice"), Set.Pirate) == 0.6);
            T.Check("rico: epicos y legendarios", Customers.Taste(Kind.Rich, Items.Get("crown")) == 1.5 && Customers.Taste(Kind.Rich, Items.Get("tin_cup")) < 1);
            T.Check("paciencia por tipo (turista 3, abuelita 4, niño 2, rico 2)",
                Customers.Get(Kind.Tourist).Patience == 3 && Customers.Get(Kind.Grandma).Patience == 4 && Customers.Get(Kind.Kid).Patience == 2 && Customers.Get(Kind.Rich).Patience == 2);
            T.Check("globito de 20 a 40 segundos", Array.TrueForAll(Customers.All, t => !t.Buys || (t.BubbleSeconds >= 20 && t.BubbleSeconds <= 40)));
        }

        static Deal Fresh(Random rng, double v = 100)
        {
            return new Deal(It("banjo"), v, Customers.Get(Kind.Tourist), rng);
        }

        // ------------------------------------------------------------ respuestas
        static void Answers()
        {
            var rng = new Random(11);
            bool okSell = true, okPerf = true;
            for (int i = 0; i < 3000; i++)
            {
                var d = Fresh(rng);
                int p = Math.Max(d.Offer + 1, (int)Math.Floor(d.Max * (0.8 + 0.2 * rng.NextDouble())));
                if (p > d.Max) continue;
                var r = d.Ask(p);
                if (r.Outcome != Outcome.Sold || r.Price != p) okSell = false;
                if (r.Perfect != (p >= 0.97 * d.Max)) okPerf = false;
            }
            T.Check("P <= M: acepta a tu precio", okSell);
            T.Check("PERFECTO a partir de 0.97 M", okPerf);

            int n = 0, cnt = 0, last = 0, lucky = 0;
            for (int i = 0; i < 30000; i++)
            {
                var d = Fresh(rng, 400);
                int p = (int)Math.Ceiling(d.Max * (1.01 + 0.10 * rng.NextDouble()));
                if (p <= d.Max || p > d.Max * 1.12) continue;
                var r = d.Ask(p);
                n++;
                if (r.Outcome == Outcome.Counter) cnt++;
                else if (r.Outcome == Outcome.LastPrice) last++;
                else if (r.Outcome == Outcome.Sold && r.Lucky) lucky++;
            }
            T.Check("hasta 1.12 M: 60% contraoferta", Math.Abs(cnt / (double)n - 0.60) < 0.02, (cnt / (double)n).ToString("0.000"));
            T.Check("hasta 1.12 M: 25% ultimo precio", Math.Abs(last / (double)n - 0.25) < 0.02, (last / (double)n).ToString("0.000"));
            T.Check("hasta 1.12 M: 15% acepta igual", Math.Abs(lucky / (double)n - 0.15) < 0.02, (lucky / (double)n).ToString("0.000"));

            n = 0; int grumpy = 0, off = 0;
            for (int i = 0; i < 20000; i++)
            {
                var d = new Deal(It("banjo"), 400, Customers.Get(Kind.Grandma), rng);
                int p = (int)Math.Ceiling(d.Max * (1.13 + 0.2 * rng.NextDouble()));
                if (p <= d.Max * 1.12 || p > d.Max * 1.35) continue;
                var r = d.Ask(p);
                n++;
                if (r.Outcome == Outcome.Counter && r.Grumpy) grumpy++;
                if (r.Outcome == Outcome.Offended) off++;
            }
            T.Check("hasta 1.35 M: 50% contraoferta con mala cara", Math.Abs(grumpy / (double)n - 0.5) < 0.03, (grumpy / (double)n).ToString("0.000"));
            T.Check("hasta 1.35 M: 50% se ofende", Math.Abs(off / (double)n - 0.5) < 0.03, (off / (double)n).ToString("0.000"));

            bool okOff = true;
            for (int i = 0; i < 500; i++)
            {
                var d = Fresh(rng);
                var r = d.Ask((int)Math.Ceiling(d.Max * 1.36) + 1);
                if (r.Outcome != Outcome.Offended || r.Fame != -1 || !d.Closed) okOff = false;
            }
            T.Check("mas de 1.35 M: se ofende y se va (-1 fama)", okOff);

            int ret = 0, tot = 0;
            bool okRet = true;
            for (int i = 0; i < 20000; i++)
            {
                var d = Fresh(rng);
                var r = d.Ask((int)Math.Ceiling(d.Max * 1.5));
                tot++;
                if (r.ReturnIn > 0)
                {
                    ret++;
                    if (r.ReturnIn < 60 || r.ReturnIn > 180) okRet = false;
                    var back = d.Repent();
                    if (back.Closed || back.Offer != (int)Math.Floor(d.Max * 0.9) || !back.Repentant) okRet = false;
                    if (back.Accept().Price != (int)Math.Floor(d.Max * 0.9)) okRet = false;
                }
            }
            T.Check("20% vuelve arrepentido", Math.Abs(ret / (double)tot - 0.2) < 0.015, (ret / (double)tot).ToString("0.000"));
            T.Check("el arrepentido vuelve en 1-3 min y ofrece 0.9 M", okRet);

            var dr = Fresh(rng);
            var rr = dr.Reject();
            T.Check("rechazar: sin fama perdida", rr.Outcome == Outcome.Rejected && rr.Fame == 0 && dr.Closed);

            int tips = 0, friends = 0, sales = 0;
            bool okTip = true;
            for (int i = 0; i < 40000; i++)
            {
                var d = Fresh(rng, 1000);
                var r = d.Accept();
                sales++;
                if (r.Tip > 0) { tips++; if (r.Tip < r.Price * 0.1 - 1 || r.Tip > r.Price * 0.3 + 1) okTip = false; }
                if (r.Friend) friends++;
            }
            T.Check("propina 5%", Math.Abs(tips / (double)sales - 0.05) < 0.006, (tips / (double)sales).ToString("0.000"));
            T.Check("propina de 10 a 30%", okTip);
            T.Check("trae un amigo 4%", Math.Abs(friends / (double)sales - 0.04) < 0.006, (friends / (double)sales).ToString("0.000"));
        }

        static void Rounds()
        {
            var rng = new Random(5);
            bool okMid = true, okLast = true, okFinal = true;
            for (int i = 0; i < 4000; i++)
            {
                var d = Fresh(rng, 300);
                int before = d.Offer, pat = d.Patience;
                var r = d.Ask((int)Math.Ceiling(d.Max * 1.05));
                if (r.Outcome == Outcome.Counter)
                {
                    int want = Math.Min(d.MaxPrice, Math.Max(before + 1, (int)Math.Ceiling((before + d.Max) * 0.5)));
                    if (r.Offer != want || d.Patience != pat - 1) okMid = false;
                }
                if (r.Outcome == Outcome.LastPrice)
                {
                    if (r.Offer != Math.Max(before, (int)Math.Floor(d.Max * 0.98)) || !d.Final) okLast = false;
                    var again = d.Ask(d.Offer + 50);
                    if (again.Outcome != Outcome.LastPrice || d.Closed) okFinal = false;
                    var w = d.Reject();
                    if (w.Outcome != Outcome.Walked || w.Fame != 0) okFinal = false;
                }
            }
            T.Check("contraoferta: mitad de camino hacia M y una ronda menos", okMid);
            T.Check("ultimo precio: 0.98 M", okLast);
            T.Check("ultimo precio: no se puede contraofertar y rechazar lo despide sin enojo", okFinal);

            bool okOut = true;
            for (int i = 0; i < 2000; i++)
            {
                var d = new Deal(It("banjo"), 300, Customers.Get(Kind.Kid), rng);
                int guard = 0;
                while (!d.Closed && !d.Final && guard++ < 20) d.Ask((int)Math.Ceiling(d.Max * 1.05));
                if (guard >= 20) okOut = false;
                if (!d.Closed && !d.Final) okOut = false;
            }
            T.Check("la negociacion siempre termina (sin rondas infinitas)", okOut);

            Deal dl = null;
            for (int g = 0; g < 200; g++)
            {
                var d = Fresh(rng, 500);
                d.Ask((int)Math.Ceiling(d.Max * 1.05));
                if (d.Final && !d.Closed) { dl = d; break; }
            }
            T.Check("aparece el ultimo precio", dl != null);
            if (dl != null)
            {
                var a = dl.Accept();
                T.Check("aceptar el ultimo precio (0.98 M) cuenta como PERFECTO", a.Perfect && a.Outcome == Outcome.Sold);
            }
        }

        // ------------------------------------------------------------ caras
        public static Deal WithPersona(Persona p, Random rng, double v = 200)
        {
            for (int i = 0; i < 4000; i++)
            {
                var d = new Deal(It("banjo"), v, Customers.Get(Kind.Tourist), rng);
                if (d.Persona == p) return d;
            }
            throw new Exception("no salio la personalidad " + p);
        }

        static void Faces()
        {
            var rng = new Random(21);
            bool mono = true;
            for (int k = 0; k < 200; k++)
            {
                var d = WithPersona(Persona.Expressive, rng);
                float prev = -1;
                for (double r = 0.5; r < 1.5; r += 0.02)
                {
                    float lv = d.Face((int)Math.Round(d.Max * r)).Level;
                    if (lv < prev - 1e-4) mono = false;
                    prev = lv;
                }
            }
            T.Check("expresiva: la cara empeora a medida que pedis mas", mono);

            var e = WithPersona(Persona.Expressive, rng);
            T.Check("expresiva: contenta lejos de su maximo", e.Face((int)(e.Max * 0.6)).Level < 0.3);
            T.Check("expresiva: furiosa muy por encima", e.Face((int)(e.Max * 1.5)).Level > 2.8);

            var pk = WithPersona(Persona.Poker, rng);
            float pLo = pk.Face((int)(pk.Max * 0.6)).Level, pHi = pk.Face((int)(pk.Max * 1.4)).Level;
            T.Check("cara de poker: casi no se mueve", Math.Abs(pHi - pLo) < 0.35, pLo + ".." + pHi);
            T.Check("cara de poker: solo la ceja al final", pk.Face((int)(pk.Max * 0.8)).Brow < 0.05 && pk.Face((int)(pk.Max * 1.2)).Brow > 0.8);

            var ex = WithPersona(Persona.Exaggerated, rng);
            T.Check("exagerada: pone caras grandes antes de tiempo", ex.Face((int)(ex.Max * 0.9)).Level > e.Face((int)(e.Max * 0.9)).Level);
            T.Check("exagerada: aguanta una ronda mas", ex.StartPatience == Customers.Get(Kind.Tourist).Patience + 1);

            var li = WithPersona(Persona.Liar, rng);
            var near = li.Face((int)(li.Max * 0.95));
            T.Check("mentirosa: cara de enojo cerca de su maximo", near.Level > 2.2, near.Level + "");
            T.Check("mentirosa: pero sin humito (todavia le alcanza)", near.Steam < 0.01);
            T.Check("mentirosa: igual acepta", li.Ask((int)(li.Max * 0.95)).Outcome == Outcome.Sold);

            var nv = WithPersona(Persona.Nervous, rng);
            T.Check("nerviosa: suda desde temprano", nv.Face((int)(nv.Max * 0.7)).Sweat > 0.4);
            T.Check("expresiva: no suda lejos de su maximo", e.Face(e.Offer + 1).Sweat < 0.05);
            T.Check("tension: sudor sobre 1.4 veces su oferta", e.Face((int)Math.Ceiling(e.Offer * 1.6)).Sweat > 0.95);
            T.Check("humito solo si de verdad te pasaste", e.Face((int)(e.Max * 1.05)).Steam < 0.01 && e.Face((int)(e.Max * 1.3)).Steam > 0.9);

            float lvA = 9, lvB = -9;
            for (int i = 0; i < 200; i++)
            {
                var a = WithPersona(Persona.Expressive, rng);
                float lv = a.Face((int)(a.Max * 0.95)).Level;
                lvA = Math.Min(lvA, lv); lvB = Math.Max(lvB, lv);
            }
            T.Check("la cara tiene desvio propio (+-12%)", lvB - lvA > 0.5, (lvB - lvA).ToString("0.00"));
            T.Check("pensar: de 0.6 a 1.5 s, mas cerca de M mas largo", Math.Abs(e.ThinkTime((int)e.Max) - 1.5f) < 0.05f && Math.Abs(e.ThinkTime((int)(e.Max * 2)) - 0.6f) < 0.01f);

            var w = WithPersona(Persona.Expressive, rng);
            int p0 = (int)(w.Max * 0.8);
            T.Check("hacerlo esperar lo pone impaciente", w.Face(p0, 1.0).Level > w.Face(p0, 0).Level + 0.2);
        }

        static void Tells()
        {
            var rng = new Random(31);
            var d = WithPersona(Persona.Expressive, rng);
            T.Check("tell: no fuera de la franja", !d.TryTell((int)(d.Max * 0.85)));
            T.Check("tell: si en la franja 92-100%", d.TryTell((int)Math.Ceiling(d.Max * 0.95)));
            T.Check("tell: una sola vez por negociacion", !d.TryTell((int)Math.Ceiling(d.Max * 0.96)));
            int with = 0, n = 0;
            for (int i = 0; i < 3000; i++)
            {
                var p = WithPersona(Persona.Poker, rng);
                n++;
                if (p.HasTell) with++;
            }
            T.Check("cara de poker: tell el 40% de las veces", Math.Abs(with / (double)n - 0.4) < 0.04, (with / (double)n).ToString("0.00"));
        }

        // ------------------------------------------------------------ subasta
        static void AuctionSuite()
        {
            var rng = new Random(41);
            bool okMax = true, okTime = true, okStep = true;
            for (int i = 0; i < 2000; i++)
            {
                var a = new Deal(It("treasure_chest"), 320, Customers.Get(Kind.Tourist), rng);
                var b = new Deal(It("treasure_chest"), 320, Customers.Get(Kind.Grandma), rng);
                var au = new Auction(new[] { a, b }, rng);
                int bids = 0;
                float t = 0;
                while (!au.Done && t < 20)
                {
                    var ev = au.Tick(0.1f);
                    t += 0.1f;
                    foreach (var e in ev)
                    {
                        if (e.Dropped) continue;
                        bids++;
                        if (e.Amount > au.Bidders[e.Bidder].MaxPrice) okMax = false;
                    }
                }
                if (t > 8.05f || !au.TimedOut) okTime = false;
                if (bids > 7) okStep = false;
                var r = au.Result();
                if (r.Outcome == Outcome.Sold && r.Price > au.Winner.MaxPrice) okMax = false;
            }
            T.Check("subasta: nadie paga mas que su maximo", okMax);
            T.Check("subasta: dura como maximo 8 s", okTime);
            T.Check("subasta: una puja cada 1.2 s", okStep);

            var x = new Deal(It("treasure_chest"), 320, Customers.Get(Kind.Tourist), rng);
            var y = new Deal(It("treasure_chest"), 320, Customers.Get(Kind.Tourist), rng);
            var z = new Deal(It("treasure_chest"), 320, Customers.Get(Kind.Kid), rng);
            var au3 = new Auction(new[] { x, y, z }, rng);
            au3.Tick(2.5f);
            au3.Hammer();
            var r3 = au3.Result();
            T.Check("subasta de 3 y martillo: vende al que va ganando", au3.Done && r3.Outcome == Outcome.Sold && r3.Price == au3.Amount);
            int noSale = 0;
            for (int i = 0; i < 2000; i++)
            {
                var a = new Deal(It("treasure_chest"), 320, Customers.Get(Kind.Tourist), rng);
                var b = new Deal(It("treasure_chest"), 320, Customers.Get(Kind.Tourist), rng);
                var au = new Auction(new[] { a, b }, rng);
                au.Tick(9f);
                if (au.NoSale) noSale++;
            }
            T.Check("si esperas de mas, a veces se van los dos", noSale > 700 && noSale < 1300, noSale + "");
        }

        // ------------------------------------------------------------ carga
        static void CarrySuite()
        {
            T.Check("caminar 3.2 m/s y correr 5.5", Math.Abs(Move.Speed(0.7f, false) - 3.2f) < 0.01 && Move.Speed(0.8f, false) == 5.5f);
            T.Check("cargando 1.9 m/s y sin correr", Math.Abs(Move.Speed(1f, true) - 1.9f) < 0.01 && !Move.Running(1f, true));
            T.Check("zona muerta 8%", Move.Speed(0.05f, false) == 0);
            T.Check("empujando el carrito 2.4", Math.Abs(Move.Speed(1f, false, true) - 2.4f) < 0.01);

            var w = new Wobble();
            WobbleEvent ev = WobbleEvent.None;
            float t = 0;
            while (ev == WobbleEvent.None && t < 3) { ev = w.Step(0.05f, 1f, 1f, false); t += 0.05f; }
            T.Check("forzar el paso cargando hace tambalear", ev == WobbleEvent.Start && t > 0.5f && t < 0.75f, t + "");
            ev = WobbleEvent.None; t = 0;
            while (ev == WobbleEvent.None && t < 3) { ev = w.Step(0.05f, 1f, 1f, false); t += 0.05f; }
            T.Check("si seguis forzando se cae", ev == WobbleEvent.Fall && t <= 0.85f, t + "");
            w.Reset();
            ev = w.Step(0.05f, 0.5f, 0.8f, true);
            T.Check("chocar a mas de 70% hace tambalear", ev == WobbleEvent.Start);
            ev = WobbleEvent.None; t = 0;
            while (ev == WobbleEvent.None && t < 3) { ev = w.Step(0.05f, 0.4f, 0.5f, false); t += 0.05f; }
            T.Check("si aflojas se estabiliza", ev == WobbleEvent.Recover);
            w.Reset();
            T.Check("chocar despacio no pasa nada", w.Step(0.05f, 0.3f, 0.3f, true) == WobbleEvent.None);
            var it = Wobble.Crack(It("old_lantern"));
            T.Check("al caerse queda rajado (-30%)", it.Cond == Condition.Cracked && Math.Abs(Items.BaseValue(it) - 16 * 0.7) < 1e-9);

            var bag = new Backpack();
            int added = 0;
            for (int i = 0; i < 10; i++) if (bag.Add(It("pocket_watch"))) added++;
            T.Check("mochila: 4 lugares al principio", added == 4 && bag.Full);
            T.Check("mochila: no entran medianos", !new Backpack().Add(It("old_lantern")));
            bag.Level = 3;
            T.Check("mochila: mejora hasta 12", bag.Capacity == 12);
        }

        static void SpotsSuite()
        {
            var rng = new Random(51);
            var spots = new List<Spot>();
            for (int i = 0; i < 16; i++) spots.Add(new Spot { Id = i, Zone = Zone.Forest, MaxWeight = Weight.Medium });
            int n = Spots.Refill(spots, 0, rng);
            T.Check("puntos de hallazgo: se llenan al empezar", n == 16);
            bool okW = true;
            foreach (var s in spots) if (s.Item.D.Weight > Weight.Medium || s.Item.D.Zone != Zone.Forest) okW = false;
            T.Check("bosque a mano: solo chicos y medianos del bosque", okW);
            var got = Spots.Take(spots[0], 100, rng);
            T.Check("juntar deja el lugar vacio", got.Valid && !spots[0].Has);
            T.Check("vuelve en 10-30 min", spots[0].RespawnAt >= 700 && spots[0].RespawnAt <= 1900);
            T.Check("no vuelve antes", Spots.Refill(spots, 500, rng) == 0);
            T.Check("vuelve despues", Spots.Refill(spots, 2000, rng) == 1);
        }

        static void GestureSuite()
        {
            // frotar: ida y vuelta corta destapa en 1-3 s; arrastrar en una sola direccion no sirve
            var g = new Gesture(SpotKind.Dig);
            float x = 0.5f, t = 0f;
            int dir = 1;
            while (!g.Done && t < 10f)
            {
                float nx = x + dir * 0.05f;   // 0.05 de pantalla por cuadro a 30 cps = frotar rapido
                g.Drag(x, 0.5f, nx, 0.5f);
                x = nx;
                if (x > 0.65f || x < 0.35f) dir = -dir;
                t += 1f / 30f;
            }
            T.Check("desenterrar: frotando sale en 1-3 s", g.Done && t >= 1f && t <= 3f, t.ToString("0.0"));
            var g2 = new Gesture(SpotKind.Dig);
            for (int i = 0; i < 200; i++) g2.Drag(0.1f + i * 0.004f, 0.5f, 0.1f + (i + 1) * 0.004f, 0.5f);
            T.Check("desenterrar: arrastrar de un lado al otro no alcanza", !g2.Done, g2.Progress.ToString("0.00"));

            var s = new Gesture(SpotKind.Tree);
            float sx = 0.5f;
            for (int k = 0; k < 4 && !s.Done; k++)
            {
                float to = k % 2 == 0 ? 0.8f : 0.2f;
                for (int i = 0; i < 6; i++) { float nx = sx + (to - sx) / (6 - i); s.Drag(sx, 0.5f, nx, 0.5f); sx = nx; }
            }
            T.Check("sacudir: 3 idas y vueltas tiran algo", s.Done && s.Hits == 3, s.Hits + "");
            var s2 = new Gesture(SpotKind.Tree);
            for (int k = 0; k < 10; k++) { s2.Drag(0.5f, 0.5f, 0.53f, 0.5f); s2.Drag(0.53f, 0.5f, 0.5f, 0.5f); }
            T.Check("sacudir: temblequear chiquito no cuenta", !s2.Done);

            var c = new Gesture(SpotKind.Chest);
            float a = 0f;
            int steps = 0;
            while (!c.Done && steps < 400)
            {
                float na = a + 0.2f;
                c.Drag(0.5f + 0.2f * (float)Math.Cos(a), 0.5f + 0.2f * (float)Math.Sin(a), 0.5f + 0.2f * (float)Math.Cos(na), 0.5f + 0.2f * (float)Math.Sin(na));
                a = na;
                steps++;
            }
            T.Check("forzar el cofre: dos vueltas con el dedo", c.Done && Math.Abs(a - 4 * Math.PI) < 0.5, a.ToString("0.0"));

            var h = new Gesture(SpotKind.Ground);
            h.Press();
            float ht = 0f;
            while (h.Bar < 0.4f && ht < 2f) { h.Tick(0.02f); ht += 0.02f; }
            h.Release();
            T.Check("levantar pesado: soltar fuera de la zona verde resbala", h.Slipped && !h.Done);
            h.Press();
            ht = 0f;
            while (!(h.Bar >= Gesture.GreenLo + 0.02f && h.Bar <= Gesture.GreenHi - 0.02f) && ht < 3f) { h.Tick(0.01f); ht += 0.01f; }
            h.Release();
            T.Check("levantar pesado: soltar en la zona verde lo levanta", h.Done);

            var w = new Gesture(SpotKind.Water);
            w.Tap();
            T.Check("pescar: sin tirar el iman los toques no cuentan", w.Progress == 0);
            w.Drag(0.5f, 0.3f, 0.5f, 0.6f);
            for (int i = 0; i < Gesture.ReelTaps; i++) w.Tap();
            T.Check("pescar: tirar y recoger con toques rapidos", w.Cast && w.Done);
            T.Check("los pesados piden la barrita", Gesture.NeedsHeavyLift(Items.Get("carved_log")) && Gesture.NeedsHeavyLift(Items.Get("anchor")) && !Gesture.NeedsHeavyLift(Items.Get("old_lantern")));
        }

        static void WorkshopSuite()
        {
            T.Check("taller: sucio se frota, rajado se pega, perfecto se pinta",
                Workshop.Next(Condition.Dirty) == RestoreGame.Scrub && Workshop.Next(Condition.Cracked) == RestoreGame.Glue && Workshop.Next(Condition.Perfect) == RestoreGame.Paint && Workshop.Next(Condition.Restored) == RestoreGame.None);
            T.Check("taller: restaurar deja +40%", Workshop.After(Condition.Perfect) == Condition.Restored && Workshop.After(Condition.Cracked) == Condition.Perfect);
            var it = It("rusty_axe", Condition.Cracked);
            double v0 = Items.BaseValue(it);
            it.Cond = Workshop.After(it.Cond);
            it.Cond = Workshop.After(it.Cond);
            T.Check("del rajado al restaurado el valor se duplica", Items.BaseValue(it) / v0 > 1.9, (Items.BaseValue(it) / v0).ToString("0.00"));

            var load = new List<ItemInst>();
            T.Check("carreta: un grande", Vehicles.Fits(false, load, It("anchor")));
            load.Add(It("anchor"));
            T.Check("carreta: nada mas con un grande", !Vehicles.Fits(false, load, It("anchor")) && !Vehicles.Fits(false, load, It("banjo")));
            T.Check("carreta: no lleva enormes", !Vehicles.Fits(false, new List<ItemInst>(), It("figurehead")));
            var two = new List<ItemInst> { It("banjo") };
            T.Check("carreta: dos medianos", Vehicles.Fits(false, two, It("banjo")));
            T.Check("camioneta: un enorme", Vehicles.Fits(true, new List<ItemInst>(), It("figurehead")));
            T.Check("gigante: ni la camioneta (evento con grua)", !Vehicles.Fits(true, new List<ItemInst>(), It("rowboat")) && Vehicles.Needs(Items.Get("rowboat")) == "crane");

            T.Check("marea: cada 3 minutos", Math.Abs(Tide.Level(0) - 0) < 1e-6 && Math.Abs(Tide.Level(90) - 1) < 1e-6 && Math.Abs(Tide.Level(180) - 0) < 1e-6);
            T.Check("marea: cada bajada es nueva", Tide.Ebb(10) != Tide.Ebb(200));

            var p = Progress.New(3);
            T.Check("obra: sin plata no arranca", !p.StartBuild(0));
            p.Grant(3000);
            T.Check("obra: paga y arranca", p.StartBuild(100) && p.Coins == 3050 - 1500 && p.Work.Active);
            T.Check("obra: no termina antes de tiempo", !p.FinishBuild(110) && p.Stage == 0);
            T.Check("obra: galpon en 30 s", p.FinishBuild(131) && p.Stage == 1 && !p.Work.Active);
            T.Check("el galpon abre la playa", p.ZoneOpen(Zone.Beach) && !p.ZoneOpen(Zone.Dungeon));
            T.Check("carreta: 900 con el galpon", p.BuyCart() && p.HasCart && p.Coins == 3050 - 1500 - 900);
            p.Grant(20000);
            p.StartBuild(0);
            T.Check("obra instantanea con anuncio solo si faltan menos de 30 min", p.Work.CanSkipWithAd(1) && p.FinishBuild(1, true) && p.Stage == 2);
            var s = p.ToSave(5);
            var q = Progress.FromSave(s, 9);
            T.Check("guardar: etapa, carreta y obra", q.Stage == 2 && q.HasCart && !q.Work.Active);
        }

        static void LayoutSuite()
        {
            var p = Progress.New(4);
            var st = p.StageDef;
            T.Check("grilla: no se puede pisar otro exhibidor", !Layout.CanPlace(st, p.Displays, p.Decors, 2, 1, 0, 3, 0));
            T.Check("grilla: no se tapa la puerta", !Layout.CanPlace(st, p.Displays, p.Decors, 1, 1, 5, 0, 0));
            T.Check("grilla: no se sale del local", !Layout.CanPlace(st, p.Displays, p.Decors, 2, 2, 5, 3, 0));
            T.Check("grilla: un lugar libre", Layout.CanPlace(st, p.Displays, p.Decors, 2, 1, 0, 1, 0));
            int x = 1, y = 3;
            T.Check("iman: busca el lugar libre mas cercano", Layout.Snap(st, p.Displays, p.Decors, 2, 1, 0, ref x, ref y) && Layout.CanPlace(st, p.Displays, p.Decors, 2, 1, x, y, 0));
            p.Grant(1000);
            int i = p.BuyDisplay(DisplayKind.PlankShelf, 0, 0);
            T.Check("comprar exhibidor: lo pone y cobra", i == 2 && p.Coins == 1050 - 60);
            T.Check("el rancho llega a 3 exhibidores", p.BuyDisplay(DisplayKind.AppleCrate, 0, 0) == -1);
            T.Check("la mesa con mantel recien en el galpon", Venue.Get(DisplayKind.Table).FromStage == 1);
            T.Check("girar: un estante de 2x1 parado ocupa 1x2", p.MoveDisplay(i, 0, 0, 1) && p.Displays[i].Rot == 1);
            T.Check("decoracion: la planta sube el encanto", p.BuyDecor(DecorKind.Plant, 3, 1) && p.Charm == 2);
            T.Check("decoracion: el gato recien en la tienda", !p.BuyDecor(DecorKind.Cat, 0, 0));
            for (int k = 0; k < 4; k++) p.AddStain();
            T.Check("mugre: 4 manchas en el rancho = 2/3 sucio", Math.Abs(p.Dirt - 4f / 6f) < 1e-4);
            p.CleanStain();
            T.Check("limpiar baja la mugre", p.Stains == 3);
            var s = p.ToSave(1);
            var q = Progress.FromSave(s, 2);
            T.Check("guardar decoracion y mugre", q.Decors.Count == 1 && q.Stains == 3 && q.Displays.Count == 3);

            var rng = new Random(77);
            var kidD = new Deal(It("banjo"), 100, Customers.Get(Kind.Kid), rng);
            T.Check("niño: barato vuelve con los padres", Specials.KidBringsParents(kidD, kidD.Accept()));
            var rd = new Deal(It("crown"), 9500, Customers.Get(Kind.Rich), rng);
            var rr = rd.Ask(rd.MaxPrice);
            T.Check("rico: enamorado deja propina grande", rr.Perfect && Specials.RichLoveTip(rr) >= rr.Price / 2);
            T.Check("influencer: solo si esta limpio y lindo", Specials.InfluencerWave(0.1f, 10, 1) && !Specials.InfluencerWave(0.5f, 10, 1) && !Specials.InfluencerWave(0.1f, 2, 1));
            var disp = new List<Placed> { new Placed { Kind = DisplayKind.AppleCrate, Item = It("pocket_watch") }, new Placed { Kind = DisplayKind.AppleCrate, Item = It("tin_cup") }, new Placed { Kind = DisplayKind.PlankShelf, Item = It("banjo") } };
            T.Check("ladronzuelo: va por lo chico y caro", Specials.ThiefTarget(disp) == 0);
        }

        static void ProgressSuite()
        {
            var p = Progress.New(1);
            T.Check("arranca con 50 monedas y el rancho", p.Coins == 50 && p.Stage == 0 && p.Displays.Count == 2);
            T.Check("primer cliente: la abuelita", p.NextKind() == Kind.Grandma);
            int shelf = p.FreeDisplay(Weight.Medium);
            T.Check("el estante acepta el farol", shelf >= 0 && p.Put(shelf, It("old_lantern")));
            T.Check("el cajon no acepta medianos", !p.Put(1, It("old_lantern")));
            var d = p.OpenDeal(shelf, Kind.Grandma);
            T.Check("negociacion facil al principio", d.Easy);
            var r = d.Ask(d.MaxPrice);
            p.Apply(shelf, r);
            T.Check("vender: monedas, valor conocido y estante libre", p.Coins == 50 + r.Price + r.Tip && p.Known[Items.IndexOf("old_lantern")] && p.Displays[shelf].Free);
            T.Check("vender perfecto suma racha", p.PerfectStreak == (r.Perfect ? 1 : 0));
            var s = p.ToSave(123);
            var q = Progress.FromSave(s, 2);
            T.Check("guardar y cargar", q.Coins == p.Coins && q.Deals == p.Deals && q.Known[Items.IndexOf("old_lantern")] && q.Displays.Count == 2);
            p.AddFame(-100);
            T.Check("la fama no baja de 0", p.Fame == 0);
            p.AddFame(1600);
            T.Check("5 estrellas", p.Stars == 5f);
            var p2 = Progress.New(2);
            p2.Deals = 50;
            p2.Put(0, It("old_lantern"));
            double kid = p2.Interest(Kind.Kid, 0), granny = p2.Interest(Kind.Grandma, 0);
            T.Check("a la abuelita le interesa mas el farol que al niño", granny > kid, granny.ToString("0.00") + " vs " + kid.ToString("0.00"));
            p2.Dirt = 0.9f;
            T.Check("el local sucio interesa menos", p2.Interest(Kind.Grandma, 0) < granny);
            T.Check("con el local sucio no entran ricos", p2.KindWeight(Kind.Rich) == 0);
        }
    }
}
