using System;
using Rarezas.Core;

namespace RarezasTests
{
    public static class GemsTests
    {
        public static void Run()
        {
            Console.WriteLine("== Gemas ==");
            var p = Progress.New(8);
            T.Check("sin logros no hay gemas", p.CheckGoals().Count == 0 && p.Gems == 0);
            p.Deals = 1; p.Earned = 20; p.Perfects = 1;
            var g = p.CheckGoals();
            T.Check("primera venta y primer perfecto dan gemas", g.Count == 2 && p.Gems == 10, p.Gems + "");
            T.Check("no se cobra dos veces", p.CheckGoals().Count == 0 && p.Gems == 10);
            foreach (var d in Items.All) if (d.Set == Set.Pirate) p.Found[d.Index] = true;
            p.CheckGoals();
            T.Check("coleccion completa da gemas", p.Achieved.Contains("set_pirate"));
            p.Gems = 100;
            int cap = p.Bag.Capacity;
            T.Check("lugar extra en la mochila", p.BuyBagSlot() && p.Bag.Capacity == cap + 1 && p.Gems == 80);
            p.Bag.Bonus = Backpack.MaxBonus;
            T.Check("maximo 3 lugares extra", !p.BuyBagSlot());
            p.Stage = 0; p.Grant(1500); p.StartBuild(0);
            long gm = p.Gems;
            T.Check("terminar la obra con gemas", p.FinishWithGems(10) && p.Gems == gm - Gems.FinishCost(20));
            T.Check("anuncio VIP una vez por hora", p.VipAdReady(4000) && (p.VipAdAt = 4000) > 0 && !p.VipAdReady(5000) && p.VipAdReady(7700));
            var back = Progress.FromSave(p.ToSave(0), 8);
            T.Check("gemas y logros se guardan", back.Achieved.Count == p.Achieved.Count && back.Bag.Bonus == Backpack.MaxBonus);
        }
    }
}
