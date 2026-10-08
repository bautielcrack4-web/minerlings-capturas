using System;
using Mineros.Core;

namespace CoreTests
{
    /// <summary>Pruebas de la tienda y los anuncios con premio (reglas del nucleo; la vista habla con IAP y AdMob).</summary>
    public static class ShopSuite
    {
        public static void Go()
        {
            var isl = new Island(70);
            int g0 = isl.Gems;
            T.Check("tienda: el primer pack de gemas vale doble", isl.Grant("gems_m") == "+1000 gemas" && isl.Gems == g0 + 1000);
            T.Check("tienda: el segundo, normal", isl.Grant("gems_m") == "+500 gemas" && isl.Gems == g0 + 1500);
            int b0 = isl.Builders();
            isl.Grant("starter");
            T.Check("tienda: la oferta de inicio da gemas, constructor y turbo (nada al azar)", isl.Builders() == b0 + 1 && isl.TurboT >= 1800f && isl.Owned.Contains("starter"));
            T.Check("tienda: la oferta de inicio es una sola vez", !isl.CanOffer("starter") && isl.Grant("starter") == "");
            T.Check("tienda: la alcancia vacia no se ofrece", !isl.CanOffer("piggy"));
            isl.FeedPiggy(1000);
            int gp = isl.Gems;
            T.Check("tienda: la alcancia tiene tope y se rompe pagando", isl.Piggy == Island.PiggyCap && isl.CanOffer("piggy") && isl.Grant("piggy") != "" && isl.Gems == gp + Island.PiggyCap && isl.Piggy == 0);
            isl.Grant("pass");
            T.Check("tienda: el pase dorado se activa y no se vuelve a ofrecer", isl.PassGold && !isl.CanOffer("pass"));
            int b1 = isl.Builders();
            isl.Grant("capataz");
            T.Check("tienda: Capataz suma un constructor y duplica lo ganado sin conexion", isl.Builders() == b1 + 1 && isl.Capataz);
            // restaurar en un telefono nuevo: los no consumibles vuelven, los consumibles no
            var r = new Island(71);
            r.Restore("builder"); r.Restore("gems_m");
            T.Check("tienda: restaurar devuelve solo lo permanente", r.Owned.Contains("builder") && r.Gems == 0 && r.BonusBuilders == 1);
            r.Restore("builder");
            T.Check("tienda: restaurar dos veces no duplica", r.BonusBuilders == 1);
            // guardado
            var h = new Island(1);
            T.Check("tienda: compras, alcancia y Capataz se guardan", h.LoadJson(isl.ToJson()) && h.Owned.Contains("starter") && h.FirstDone.Contains("gems_m") && h.Capataz && h.PassGold);

            // anuncios
            var a = IslandSuite.Rich(new Island(72), 1e9, 8);
            int day = 100;
            double now = 1000;
            T.Check("anuncios: el cofre esta disponible", a.CanAd(AdPlace.Chest, day, now));
            int c0 = a.ChestCount;
            T.Check("anuncios: el cofre se entrega y vuelve en 4 h", a.GrantAd(AdPlace.Chest, day, now) != "" && a.ChestCount == c0 + 1 && !a.CanAd(AdPlace.Chest, day, now + 3600) && a.CanAd(AdPlace.Chest, day, now + 4 * 3600 + 1));
            var f = a.Plots.Find(p => a.Offered(p));
            a.Plots[0].Level = 12; a.Upgrade(a.Plots[0]);
            double w0 = a.Plots[0].Work;
            T.Check("anuncios: -30 min de obra", a.GrantAd(AdPlace.WorkCut, day, now, a.Plots[0]) != "" && Math.Abs(w0 - a.Plots[0].Work - 1800) < 1e-6);
            double coins = a.Coins;
            T.Check("anuncios: x2 de lo ganado sin conexion", a.GrantAd(AdPlace.OfflineX2, day, now, null, 500) != "" && Math.Abs(a.Coins - coins - 500) < 1e-6);
            int done = 0;
            for (int i = 0; i < 20; i++) if (a.GrantAd(AdPlace.WorkCut, day, now, a.Plots[0]) != "") done++;
            T.Check("anuncios: tope por lugar (6 de obra por dia)", a.AdToday[(int)AdPlace.WorkCut] == Island.AdCap[(int)AdPlace.WorkCut]);
            T.Check("anuncios: la TV dorada da un cofre de oro al quinto del dia", a.GoldenTvClaimed);
            int total = a.AdTotalToday;
            for (int i = 0; i < 20; i++) { a.GrantAd(AdPlace.Merchant, day, now); a.GrantAd(AdPlace.Spin, day, now); a.GrantAd(AdPlace.OfflineX2, day, now, null, 1); }
            T.Check("anuncios: nunca mas de 12 por dia", a.AdTotalToday <= Island.AdsPerDay);
            T.Check("anuncios: al dia siguiente se renuevan", a.CanAd(AdPlace.WorkCut, day + 1, now) && a.AdTotalToday == 0 && !a.GoldenTvClaimed);
        }
    }
}
