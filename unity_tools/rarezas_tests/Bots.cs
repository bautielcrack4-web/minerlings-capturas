using System;
using Rarezas.Core;

namespace RarezasTests
{
    /// <summary>
    /// Bots de regateo (DIRECCION_CREATIVA 9): "timido" (acepta siempre), "codicioso" (siempre +30%) y "lector" (usa la cara
    /// y el tell, solo con lo que ve un jugador: la cara mientras arrastra y su oferta). El lector tiene que ganar entre 25 y
    /// 40% mas que el timido y el codicioso no le debe ganar al lector.
    /// </summary>
    public static class Bots
    {
        public struct Result
        {
            public double Revenue, Deals, Sales, Perfects, Offended, Lucky, Fans;
            public double PerDeal { get { return Revenue / Math.Max(1, Deals); } }
        }

        public static int Timid(Deal d) { var r = d.Accept(); return r.Outcome == Outcome.Sold ? r.Price + r.Tip : 0; }

        public static int Greedy(Deal d)
        {
            for (int guard = 0; guard < 10; guard++)
            {
                if (d.Final) { var a = d.Accept(); return a.Price + a.Tip; }
                var r = d.Ask((int)Math.Ceiling(d.Offer * 1.3));
                if (r.Outcome == Outcome.Sold) return r.Price + r.Tip;
                if (r.Outcome == Outcome.Offended || r.Outcome == Outcome.Walked) return 0;
            }
            return 0;
        }

        /// <summary>
        /// El lector arrastra la perilla de su oferta hacia arriba mirando la cara. Si ve el brillo de ojos, frena (con un
        /// poquito de demora humana) y pide apenas mas. Si no, propone donde la cara pasa a "molesto"; con cara de poker
        /// pide poco. A una contraoferta le pide la mitad del camino una vez y despues acepta.
        /// </summary>
        public static int Reader(Deal d, Random rng)
        {
            int offer = d.Offer;
            int step = Math.Max(1, offer / 100);
            int tellAt = -1, angryAt = -1;
            float lo = 9, hi = -9;
            for (int p = offer + 1; p <= offer * 2.2; p += step)
            {
                // temblor lento de la vista: la lectura no es exacta
                var f = d.Face(p);
                float lv = f.Level + (float)((rng.NextDouble() - 0.5) * 0.25);
                lo = Math.Min(lo, lv); hi = Math.Max(hi, lv);
                if (tellAt < 0 && d.TryTell(p)) tellAt = p;
                if (angryAt < 0 && lv >= 1.45) angryAt = p;
            }
            int ask;
            if (tellAt > 0) ask = (int)Math.Round(tellAt * (1.0 + 0.04 * rng.NextDouble()) * 1.02);
            else if (hi - lo < 0.6) ask = (int)Math.Round(offer * 1.2);          // cara de poker: pide poco
            else if (angryAt > 0) ask = angryAt;
            else ask = (int)Math.Round(offer * 1.25);
            ask = Math.Max(ask, offer + 1);
            int lastAsk = ask;
            for (int round = 0; round < 6; round++)
            {
                var r = d.Ask(lastAsk);
                if (r.Outcome == Outcome.Sold) return r.Price + r.Tip;
                if (r.Outcome == Outcome.Offended || r.Outcome == Outcome.Walked) return 0;
                if (r.Outcome == Outcome.LastPrice) { var a = d.Accept(); return a.Price + a.Tip; }
                // contraoferta: una vez pide la mitad del camino, despues acepta
                if (round == 0 && !r.Grumpy) { lastAsk = Math.Max(d.Offer + 1, (d.Offer + lastAsk) / 2); continue; }
                var acc = d.Accept();
                return acc.Price + acc.Tip;
            }
            return 0;
        }

        public static Result Play(string who, int n, int seed)
        {
            var rng = new Random(seed);
            var res = new Result();
            for (int i = 0; i < n; i++)
            {
                var t = Customers.All[rng.Next(6)];
                var it = new ItemInst(rng.Next(Items.All.Length), Condition.Perfect);
                var d = new Deal(it, 100, t, rng);
                int got = who == "timido" ? Timid(d) : who == "codicioso" ? Greedy(d) : Reader(d, rng);
                res.Deals++;
                res.Revenue += got;
                if (d.Fan) res.Fans++;
                var last = d.Last;
                if (last.Outcome == Outcome.Sold) { res.Sales++; if (last.Perfect) res.Perfects++; if (last.Lucky) res.Lucky++; }
                if (last.Outcome == Outcome.Offended) res.Offended++;
            }
            return res;
        }

        public static void Run()
        {
            int n = 60000;
            var timid = Play("timido", n, 101);
            var greedy = Play("codicioso", n, 101);
            var reader = Play("lector", n, 101);
            Func<Result, string> s = r => (r.PerDeal).ToString("0.0") + " por trato, " + (r.Sales / r.Deals * 100).ToString("0") + "% ventas, "
                + (r.Perfects / r.Deals * 100).ToString("0.0") + "% perfectos, " + (r.Offended / r.Deals * 100).ToString("0.0") + "% ofendidos";
            T.Info("timido:    " + s(timid));
            T.Info("codicioso: " + s(greedy));
            T.Info("lector:    " + s(reader));
            double k = reader.PerDeal / timid.PerDeal;
            T.Check("bots: el lector gana 25-40% mas que el timido", k >= 1.25 && k <= 1.40, k.ToString("0.000"));
            T.Check("bots: el codicioso no le gana al lector", greedy.PerDeal <= reader.PerDeal, greedy.PerDeal.ToString("0.0"));
            T.Check("bots: el codicioso igual gana algo mas que el timido (pedir mas paga)", greedy.PerDeal > timid.PerDeal);
            T.Check("bots: el lector logra perfectos seguido (>= 15%)", reader.Perfects / reader.Deals >= 0.15, (reader.Perfects / reader.Deals).ToString("0.00"));
            T.Check("bots: el codicioso hace enojar mas que el lector", greedy.Offended > reader.Offended);
            T.Check("bots: el timido nunca hace enojar", timid.Offended == 0);
        }
    }
}
