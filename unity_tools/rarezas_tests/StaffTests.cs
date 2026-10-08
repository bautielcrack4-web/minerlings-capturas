using System;
using Rarezas.Core;

namespace RarezasTests
{
    public static class StaffTests
    {
        public static void Run()
        {
            Console.WriteLine("== Personal ==");
            var p = Progress.New(5);
            T.Check("en el rancho no se contrata", !p.CanHire(StaffRole.Receptionist));
            p.Stage = 1;
            p.Grant(5000);
            T.Check("galpon: recepcionista y limpieza", p.Hire(StaffRole.Receptionist) != null && p.Hire(StaffRole.Cleaner) != null);
            T.Check("una sola Martina", p.Hire(StaffRole.Receptionist) == null);
            T.Check("guardia recien en etapa 4 de la tabla", !p.CanHire(StaffRole.Guard));
            p.Stage = 2;
            p.Grant(20000);
            for (int i = 0; i < 4; i++) p.Hire(StaffRole.Seeker);
            T.Check("hasta 3 buscadores", p.CountStaff(StaffRole.Seeker) == 3);
            var s = p.Staff(StaffRole.Seeker);
            T.Check("buscador sale a zona abierta", p.SendSeeker(s, Zone.Forest, 0) && s.BackAt > 0);
            T.Check("expedicion 15-60 min", Staff.TripSeconds(Zone.Forest, 1) == 900 && Staff.TripSeconds(Zone.Ruins, 1) == 3600);
            T.Check("no vuelve antes", p.CollectSeekers(100).Count == 0);
            var box = p.CollectSeekers(2000);
            T.Check("vuelve con caja sorpresa de 1-3", box.Count >= 1 && box.Count <= 3, box.Count + "");
            T.Check("limpieza: 8 s por mancha y mas rapido con nivel", Staff.CleanEvery(1) == 8f && Staff.CleanEvery(3) < 8f);
            var h = p.Staff(StaffRole.Cleaner);
            long before = p.Coins;
            T.Check("subir de nivel cuesta", p.LevelUp(h) && h.Level == 2 && p.Coins < before);
            h.Mood = 0.5f;
            T.Check("regalito sube el animo", p.Gift(h) && h.Mood > 0.5f);

            Console.WriteLine("== Mientras no estas ==");
            double rateAvg = 0;
            int n = 30;
            for (int k = 0; k < n; k++)
            {
                var q = Progress.New(100 + k);
                q.Stage = 1;
                q.OpenSign = true;
                q.Displays.Clear();
                for (int i = 0; i < 4; i++) q.Displays.Add(new Placed { Kind = DisplayKind.PlankShelf, X = i * 2, Y = 3, Item = new ItemInst(i, Condition.Perfect) });
                double v = 0;
                for (int i = 0; i < 4; i++) v += q.PricedValue(i);
                var none = q.Away(3600, 0);
                T.Check("sin personal no vende", k > 0 || none.Sold == 0);
                q.Grant(800);
                q.Hire(StaffRole.Receptionist);
                q.AddStain();
                var r = q.Away(4 * 3600, 0);
                rateAvg += r.Coins / v;
                if (k == 0)
                {
                    T.Check("recepcionista vende algo en 4 h", r.Sold > 0, r.Sold + "");
                    T.Check("la mejor venta queda registrada", r.Best > 0 && r.BestItem >= 0);
                }
            }
            rateAvg /= n;
            T.Check("vende mas o menos lo exhibido a ~V (con enojados)", rateAvg > 0.6 && rateAvg < 1.1, rateAvg.ToString("0.00"));
            T.Check("tasa 50-70%", Staff.AwayRate(1) == 0.5 && Math.Abs(Staff.AwayRate(5) - 0.7) < 1e-9);
            var lim = Progress.New(9);
            T.Check("maximo 8 h", lim.Away(30 * 3600, 0).Seconds == 8 * 3600);
            var sv = Progress.New(3);
            sv.Stage = 1; sv.Grant(900); sv.Hire(StaffRole.Cleaner);
            var back = Progress.FromSave(sv.ToSave(0), 3);
            T.Check("el personal se guarda", back.HasStaff(StaffRole.Cleaner));
        }
    }
}
