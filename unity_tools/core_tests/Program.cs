using System;
using Mineros.Core;

namespace CoreTests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--dump")
            {
                Dump.Run();
                return 0;
            }
            T.Quiet = args.Length > 0 && args[0] == "--quiet";

            Ctx c = new Ctx();
            MetaSuite.Run(c);
            int portTotal = T.Total;
            int portFails = T.Fails;
            Console.WriteLine("---- port de test_meta.gd: " + portTotal + " pruebas, " + portFails + " fallos");

            ExtraSuite.Run();
            IslandSuite.Go();
            Isla2Suite.Go();
            CitySuite.Go();
            ShopSuite.Go();
            MoveSuite.Go();
            BarracksSuite.Go();
            Console.WriteLine("---- " + T.Total + " pruebas, " + T.Fails + " fallos (" + (T.Total - T.Fails) + " PASS)");
            return T.Fails > 0 ? 1 : 0;
        }
    }
}
