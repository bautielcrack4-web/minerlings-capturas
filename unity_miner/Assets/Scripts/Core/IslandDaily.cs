using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Mision del dia: un contador (estadistica) que tiene que subir `Target` desde el comienzo del dia.</summary>
    public sealed class DayMission
    {
        public string Text, Stat;
        public long Target, Base;
        public double Coins;
        public int Gems;
        public bool Claimed, Ready;
    }

    /// <summary>Pedido del tablon: llevar `Count` de un mineral al deposito antes de que se venza.</summary>
    public sealed class Order
    {
        public int Kind, Count, Delivered, Gems;
        public double Coins;
        public float Left;          // segundos hasta que se va (0 = sin apuro)
        public float Wait;          // > 0: papel nuevo en camino
        public bool Claimed;
        public bool Done { get { return Delivered >= Count; } }
    }

    /// <summary>
    /// Rutina diaria (biblia 3.6): racha de 7 dias con premio que sube (el septimo, cofre de oro; se puede salvar una
    /// racha cortada con gemas una vez por semana), tres misiones por dia con un cofre de plata si se completan todas y
    /// el tablon de pedidos (3 papeles: llevar mineral al deposito para cobrar mucho mas que vendiendolo).
    /// </summary>
    public sealed partial class Island
    {
        /// <summary>Monedas regaladas (x2 de la bienvenida, premios de la interfaz).</summary>
        public void GiveCoins(double v) { if (v > 0) Earn(v); }

        // ------------------------------------------------------------ racha diaria
        public int Streak;            // dias seguidos (1..)
        public int LastDay = -1;      // ultimo dia que entro (dias desde 2024-01-01)
        public int ClaimedDay = -1;   // ultimo dia que cobro el premio
        public int RepairDay = -1000; // ultimo dia que salvo la racha con gemas
        public int BrokenStreak;      // racha que se corto (para ofrecer salvarla)
        public const int RepairGems = 8;

        /// <summary>Llamar al abrir el juego con el dia de hoy. Devuelve true si hay premio diario para cobrar.</summary>
        public bool CheckDaily(int today)
        {
            if (LastDay < 0) { Streak = 1; LastDay = today; NewDayMissions(today); return true; }
            if (today == LastDay) return ClaimedDay != today;
            int gap = today - LastDay;
            BrokenStreak = 0;
            if (gap == 1) Streak++;
            else if (gap > 1) { BrokenStreak = Streak; Streak = 1; }
            LastDay = today;
            NewDayMissions(today);
            return ClaimedDay != today;
        }

        /// <summary>Premio del dia `d` de la racha (1..7, se repite): monedas y gemas que suben; el 7 es cofre de oro.</summary>
        public void DailyPrize(int d, out double coins, out int gems, out int chest)
        {
            d = ((d - 1) % 7) + 1;
            coins = d == 7 ? 0 : CoinPrize(30 + d * 25);
            gems = d == 7 ? 5 : (d >= 4 ? 2 : 1);
            chest = d == 7 ? 2 : (d == 4 ? 0 : -1);
        }

        public int StreakDay { get { return ((Math.Max(1, Streak) - 1) % 7) + 1; } }

        public bool ClaimDaily(int today)
        {
            if (ClaimedDay == today || LastDay != today) return false;
            double coins; int gems, chest;
            DailyPrize(Streak, out coins, out gems, out chest);
            Earn(coins);
            Gems += gems;
            if (chest >= 0) GiveChest(chest);
            ClaimedDay = today;
            AddStat("daily", 1);
            AddXp(40);
            return true;
        }

        public bool CanRepair(int today) { return BrokenStreak > 1 && today - RepairDay >= 7 && Gems >= RepairGems; }

        /// <summary>Salva la racha cortada pagando gemas (una vez por semana).</summary>
        public bool RepairStreak(int today)
        {
            if (!CanRepair(today)) return false;
            Gems -= RepairGems;
            Streak = BrokenStreak + 1;
            BrokenStreak = 0;
            RepairDay = today;
            return true;
        }

        // ------------------------------------------------------------ misiones del dia
        public readonly List<DayMission> Missions = new List<DayMission>();
        public int MissionDay = -1;
        public bool MissionBonus;     // ya cobro el cofre por completar las tres
        public event Action<DayMission> MissionReady;
        public event Action AllMissionsDone;

        static readonly string[] MissionStats = { "rocks", "taps", "chests", "giants", "critters", "orders", "upgrades", "crits" };

        void NewDayMissions(int today)
        {
            if (MissionDay == today && Missions.Count == 3) return;
            MissionDay = today;
            MissionBonus = false;
            Missions.Clear();
            var r = new Random(today * 7919 + 17);
            var pool = new List<int> { 0, 1, 2, 4, 5, 6, 7 };
            if (TotalEarned > 1500) pool.Add(3);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int k = pool[r.Next(pool.Count)];
                pool.Remove(k);
                Missions.Add(MakeMission(MissionStats[k], r));
            }
        }

        DayMission MakeMission(string stat, Random r)
        {
            int scale = 1 + Math.Min(4, Miners.Count / 3);
            var m = new DayMission { Stat = stat, Base = Stat(stat), Gems = 2 + r.Next(2), Coins = Math.Round(CoinPrize(90)) };
            switch (stat)
            {
                case "rocks": m.Target = 25 * scale; m.Text = "Picá " + m.Target + " rocas"; break;
                case "taps": m.Target = 60 + 30 * scale; m.Text = "Tocá vetas " + m.Target + " veces"; break;
                case "chests": m.Target = 2; m.Text = "Abrí 2 cofres"; break;
                case "giants": m.Target = 1; m.Text = "Rompé una veta gigante"; break;
                case "critters": m.Target = 2; m.Text = "Atrapá 2 bichos"; break;
                case "orders": m.Target = 2; m.Text = "Completá 2 pedidos del tablón"; break;
                case "upgrades": m.Target = 3; m.Text = "Mejorá edificios 3 veces"; break;
                default: m.Target = 5; m.Text = Loc.T("Hacé 5 golpes críticos"); break;
            }
            return m;
        }

        public long MissionProgress(DayMission m) { return Math.Min(m.Target, Stat(m.Stat) - m.Base); }

        void TickMissions()
        {
            foreach (var m in Missions)
                if (!m.Claimed && MissionProgress(m) >= m.Target && !m.Ready) { m.Ready = true; MissionReady?.Invoke(m); }
        }

        public bool ClaimMission(DayMission m)
        {
            if (m == null || m.Claimed || MissionProgress(m) < m.Target) return false;
            m.Claimed = true;
            Earn(m.Coins);
            Gems += m.Gems;
            AddStat("missions", 1);
            AddXp(60);
            bool all = true;
            foreach (var x in Missions) all &= x.Claimed;
            if (all && !MissionBonus) { MissionBonus = true; GiveChest(1); AllMissionsDone?.Invoke(); }
            return true;
        }

        // ------------------------------------------------------------ tablon de pedidos
        public readonly List<Order> Orders = new List<Order>();
        public event Action<Order> OrderDone;
        public bool BoardOpen { get { return TotalEarned >= 150; } }

        void TickOrders(float dt)
        {
            if (!BoardOpen) return;
            while (Orders.Count < 3) Orders.Add(MakeOrder(Orders.Count == 0 ? 0f : 0.01f));
            for (int i = 0; i < Orders.Count; i++)
            {
                var o = Orders[i];
                if (o.Wait > 0f) { o.Wait -= dt; continue; }
                if (o.Done || o.Left <= 0f) continue;
                o.Left -= dt;
                if (o.Left <= 0f) { Orders[i] = MakeOrder(20f); }   // se vencio: llega otro papel
            }
        }

        Order MakeOrder(float wait)
        {
            int best = Math.Max(0, Math.Min(3, BestKind()));
            int k = Math.Max(0, best - rng.Next(Math.Min(best + 1, 3)));
            int count = Math.Max(3, (int)Math.Round((10 - k * 2) * (0.7 + 0.1 * Math.Min(Miners.Count, 10))));
            double value = Ores[k].Value * count * PriceMult();
            return new Order
            {
                Kind = k, Count = count, Coins = Math.Round(value * 4), Gems = rng.NextDouble() < 0.4 ? 1 : 0,
                Left = 900f + (float)rng.NextDouble() * 600f, Wait = wait,
            };
        }

        /// <summary>Lo que se deposita llena primero el pedido mas viejo de ese mineral.</summary>
        void FillOrders(int kind, int units)
        {
            foreach (var o in Orders)
            {
                if (units <= 0) break;
                if (o.Wait > 0f || o.Done || o.Kind != kind) continue;
                int put = Math.Min(units, o.Count - o.Delivered);
                o.Delivered += put;
                units -= put;
                if (o.Done) OrderDone?.Invoke(o);
            }
        }

        // ------------------------------------------------------------ guardado
        Dictionary<string, object> DailyObj()
        {
            var ms = new List<object>();
            foreach (var m in Missions)
                ms.Add(new Dictionary<string, object> { { "t", m.Text }, { "s", m.Stat }, { "g", m.Target }, { "b", m.Base }, { "c", m.Coins }, { "e", m.Gems }, { "k", m.Claimed ? 1 : 0 } });
            var os = new List<object>();
            foreach (var o in Orders)
                os.Add(new Dictionary<string, object> { { "k", o.Kind }, { "n", o.Count }, { "d", o.Delivered }, { "c", o.Coins }, { "e", o.Gems }, { "l", o.Left }, { "w", o.Wait } });
            return new Dictionary<string, object>
            {
                { "streak", Streak }, { "last", LastDay }, { "claimed", ClaimedDay }, { "repair", RepairDay }, { "broken", BrokenStreak },
                { "mday", MissionDay }, { "mbonus", MissionBonus ? 1 : 0 }, { "missions", ms }, { "orders", os },
            };
        }

        void LoadDaily(Dictionary<string, object> d)
        {
            Streak = JsonRead.Int(d, "streak", 0);
            LastDay = JsonRead.Int(d, "last", -1);
            ClaimedDay = JsonRead.Int(d, "claimed", -1);
            RepairDay = JsonRead.Int(d, "repair", -1000);
            BrokenStreak = JsonRead.Int(d, "broken", 0);
            MissionDay = JsonRead.Int(d, "mday", -1);
            MissionBonus = JsonRead.Int(d, "mbonus", 0) == 1;
            Missions.Clear();
            object mo;
            if (d.TryGetValue("missions", out mo) && mo is List<object> ml)
                foreach (var x in ml)
                {
                    var md = x as Dictionary<string, object>;
                    if (md == null) continue;
                    object t, s;
                    md.TryGetValue("t", out t); md.TryGetValue("s", out s);
                    Missions.Add(new DayMission
                    {
                        Text = t as string ?? "", Stat = s as string ?? "rocks", Target = (long)JsonRead.Dbl(md, "g", 1), Base = (long)JsonRead.Dbl(md, "b", 0),
                        Coins = JsonRead.Dbl(md, "c", 0), Gems = JsonRead.Int(md, "e", 0), Claimed = JsonRead.Int(md, "k", 0) == 1,
                    });
                }
            Orders.Clear();
            object oo;
            if (d.TryGetValue("orders", out oo) && oo is List<object> ol)
                foreach (var x in ol)
                {
                    var od = x as Dictionary<string, object>;
                    if (od == null) continue;
                    Orders.Add(new Order
                    {
                        Kind = JsonRead.Int(od, "k", 0), Count = JsonRead.Int(od, "n", 3), Delivered = JsonRead.Int(od, "d", 0), Coins = JsonRead.Dbl(od, "c", 10),
                        Gems = JsonRead.Int(od, "e", 0), Left = (float)JsonRead.Dbl(od, "l", 600), Wait = (float)JsonRead.Dbl(od, "w", 0),
                    });
                }
        }

        /// <summary>Cobra un pedido listo; en su lugar llega otro papel al rato.</summary>
        public bool ClaimOrder(Order o)
        {
            int i = Orders.IndexOf(o);
            if (i < 0 || !o.Done || o.Wait > 0f) return false;
            Earn(o.Coins * (SetDone(1) ? 1.25 : 1.0));
            Gems += o.Gems;
            AddStat("orders", 1);
            AddXp(40);
            Orders[i] = MakeOrder(25f);
            return true;
        }
    }
}
