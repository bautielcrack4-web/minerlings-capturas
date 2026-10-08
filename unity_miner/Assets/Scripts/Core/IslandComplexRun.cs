using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Vagoneta que recorre una linea de rieles del Complejo.</summary>
    public sealed class Cart
    {
        public int Id, Kind, Units;
        public double Value, Base;    // valor actual y valor base (para el tope x4)
        public int[] Path;            // ids de modulos (sin la Descarga)
        public int Step;              // indice del modulo al que va
        public float T;               // 0..1 dentro del tramo actual
        public int From;              // id del modulo de donde salio el tramo actual
        public bool Experimental;
    }

    public enum CxEventKind { None = 0, Breakdown = 1, Merchant = 2, Party = 3, Inspector = 4, Jam = 5 }

    /// <summary>Evento dentro del Complejo (uno a la vez).</summary>
    public sealed class CxEvent
    {
        public CxEventKind Kind;
        public int ModId = -1;        // maquina rota, vagoneta trabada (modulo donde esta)
        public int X, Z;              // celda del comerciante
        public float T;               // segundos que le quedan
        public int Fixer = -1;        // minero que va a arreglar
        public bool Moved;            // el comerciante ya se movio una vez
        public int Taps;              // toques a la vagoneta trabada
    }

    /// <summary>Contrato con reloj: cuenta lo que pasa por el Complejo.</summary>
    public sealed class Contract
    {
        public int Kind;              // 0 unidades de un mineral, 1 lingotes, 2 vagonetas por un tipo de modulo
        public int What;              // mineral o tipo de modulo
        public int Target, Prog;
        public float Time, Left;
        public double Coins;
        public int Gems, Plans;
        public int Portable = -1;     // ModKind portatil de premio
        public bool Active;
    }

    public sealed partial class Island
    {
        public const float CartStep = 0.55f;       // segundos por celda
        public const int MaxCarts = 12;
        public readonly List<Cart> Carts = new List<Cart>();
        int nextCart = 1;
        public double FuelT;                       // segundos de carbon ya cargado en los generadores
        double schoolAcc;

        public event Action<Cart> CartSpawned;
        public event Action<Cart, Module> CartArrived;     // llego a un modulo (CLACK); la vista anima la maquina
        public event Action<Cart, Module, double> CartSold; // vendida al final de la linea
        public event Action<Module> ModuleExpired, ModuleReady;
        public event Action<int> GemsFromVault;

        public bool FuelOk { get { return FuelT > 0 || Stock[(int)Res.Coal] > 0; } }

        // ------------------------------------------------------------ deposito en la Descarga
        /// <summary>La Descarga a la que va este minero (o null: va al Deposito de siempre).</summary>
        public Module UnloadFor(Miner m)
        {
            var b = BarracksPlot;
            if (b == null || b.Level < 1) return null;
            Module best = null; float bd = float.MaxValue;
            foreach (var u in Modules)
            {
                if (u.Kind != ModKind.Unload || u.Work > 0) continue;
                float ux, uz; ModWorld(u, out ux, out uz);
                float d = Sq(ux - m.X, uz - m.Z);
                if (d < bd) { bd = d; best = u; }
            }
            if (best == null) return null;
            var dep = Find(BKind.Depot);
            float dd = Sq(dep.X - m.X, dep.Z - m.Z);
            bool own = m.Char > 0 && HasRoom(m.Char);   // los que viven en el Complejo descargan ahi
            return own || bd <= dd * 1.6f ? best : null;
        }

        /// <summary>Centro del modulo en el mundo.</summary>
        public void ModWorld(Module m, out float x, out float z)
        {
            var b = BarracksPlot;
            float ox, oz; CellOffset(m.X, m.Z, out ox, out oz);
            x = (b != null ? b.X : 0f) + ox; z = (b != null ? b.Z : 0f) + oz;
        }

        /// <summary>El minero llego a la Descarga: el mineral suma a la ciudad como siempre y sale en vagoneta.</summary>
        void UnloadDeposit(Miner m, Module u)
        {
            double v = Ores[m.CarryKind].Value * m.CarryUnits * PriceMult();
            AddStat("sold", m.CarryUnits);
            AddRes(OreRes[m.CarryKind], m.CarryUnits);
            FillOrders(m.CarryKind, m.CarryUnits);
            if (CurShip != null && !CurShip.Done && CurShip.Kind == m.CarryKind)
                CurShip.Delivered = Math.Min(CurShip.Count, CurShip.Delivered + m.CarryUnits);
            ContractCount(0, m.CarryKind, m.CarryUnits);
            SpawnCart(u, m.CarryKind, m.CarryUnits, v);
            m.CarryKind = -1; m.CarryUnits = 0;
            Deposited?.Invoke(m, 0);
        }

        /// <summary>Sale una vagoneta de `start`. Sin linea (o con demasiadas en viaje) se cobra al toque, sin perder nada.</summary>
        public Cart SpawnCart(Module start, int kind, int units, double value)
        {
            var path = TracePath(start, true);
            if (path.Count == 0 || Summary.Loops.Contains(start.Id))
            {
                Earn(value);
                return null;
            }
            if (Carts.Count >= MaxCarts)
            {
                // se paga lo que habria ganado recorriendo la linea (nada se acumula ni se pierde)
                double v = value;
                foreach (var p in path) v *= StepMult(p, kind, Summary, false);
                Earn(Math.Min(value * MaxLineMult, v));
                return null;
            }
            var ids = new int[path.Count];
            for (int i = 0; i < path.Count; i++) ids[i] = path[i].Id;
            var c = new Cart { Id = nextCart++, Kind = kind, Units = units, Value = value, Base = value, Path = ids, From = start.Id };
            Carts.Add(c);
            AddStat("carts", 1);
            CartSpawned?.Invoke(c);
            return c;
        }

        /// <summary>
        /// Multiplicador de un modulo sobre una vagoneta de mineral `kind`. `apply`: ademas hace sus efectos (lingotes a la
        /// ciudad, gemas de la Boveda, mineral experimental).
        /// </summary>
        double StepMult(Module p, int kind, ComplexSummary s, bool apply, Cart cart = null)
        {
            double k = StageK(p), v = 1.0;
            bool power = !MDef(p.Kind).NeedsPower || s.Powered.Contains(p.Id);
            switch (p.Kind)
            {
                case ModKind.Storage: v = 1.0 + 0.10 * k; break;
                case ModKind.Crusher:
                    if (kind == OreStone || kind == OreCopper || kind == OreIron || kind == OreCoal) v = 1.0 + 0.33 * k;
                    break;
                case ModKind.Smelter:
                    if (kind == OreCopper || kind == OreIron || kind == OreGold)
                    {
                        v = 1.0 + 0.6 * k;
                        if (s.Combos[1] && s.ComboMods.Contains(p.Id)) v *= 1.25;
                        if (FirstMod(ModKind.PortOven) != null) v *= 1.3;
                        if (apply && cart != null)
                        {
                            int bars = Math.Max(1, cart.Units / 2);
                            if (kind == OreIron) AddRes(Res.IronBar, bars);
                            if (kind == OreGold) AddRes(Res.GoldBar, bars);
                            ContractCount(1, -1, bars);
                        }
                    }
                    break;
                case ModKind.Polisher:
                    if (power && (kind == OreGem || kind == OreCrystal || kind == OreRare)) v = 1.0 + 0.5 * k;
                    break;
                case ModKind.Treasury:
                    v = 1.0 + 0.25 * k;
                    if (s.Combos[6] && s.ComboMods.Contains(p.Id)) v *= 1.15;
                    break;
                case ModKind.Vault:
                    if (kind == OreGem || kind == OreCrystal || kind == OreRare)
                    {
                        v = 1.1;
                        if (apply && cart != null)
                        {
                            p.Count += cart.Units * (s.Combos[2] ? 2 : 1);
                            int g = p.Count / 10;
                            if (g > 0) { p.Count -= g * 10; Gems += g; AddStat("vaultgems", g); GemsFromVault?.Invoke(g); }
                        }
                    }
                    break;
                case ModKind.Experimental:
                    if (power && apply && cart != null)
                    {
                        p.Count++;
                        if (p.Count % 5 == 0) { cart.Experimental = true; v = 4.0; Gems += 1; AddStat("experimental", 1); }
                    }
                    break;
            }
            if (v > 1.0)
            {
                double syn;
                if (s.CartBonus.TryGetValue((long)p.Id * 16 + kind, out syn)) v *= syn;
                if (p.Broken) v = 1.0 + (v - 1.0) * 0.5;
                if (p.TunedT > 0) v *= 1.3;
            }
            if (Paused(p)) v = 1.0;
            return v;
        }

        void TickCarts(float dt)
        {
            if (Carts.Count == 0) return;
            var s = Summary;
            for (int i = Carts.Count - 1; i >= 0; i--)
            {
                var c = Carts[i];
                if (JamCart == c.Id) continue;    // trabada: espera los toques
                c.T += dt / CartStep;
                if (c.T < 1f) continue;
                c.T = 0f;
                var mod = c.Step < c.Path.Length ? ModById(c.Path[c.Step]) : null;
                if (mod == null) { Earn(c.Value); Carts.RemoveAt(i); continue; }   // la linea cambio: se cobra lo que lleva
                c.Value *= StepMult(mod, c.Kind, s, true, c);
                c.Value = Math.Min(c.Value, c.Base * MaxLineMult * (c.Experimental ? 4 : 1));
                ContractCount(2, (int)mod.Kind, 1);
                CartArrived?.Invoke(c, mod);
                c.From = mod.Id;
                c.Step++;
                if (c.Step >= c.Path.Length)
                {
                    double v = c.Value * MerchantMult(mod);
                    Earn(v);
                    AddStat("cartsold", 1);
                    CartSold?.Invoke(c, mod, v);
                    Carts.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------ tiempo
        void TickComplex(float dt)
        {
            if (BarracksLevel < 1) { if (Modules.Count > 0 && BarracksPlot == null) { Modules.Clear(); Carts.Clear(); Dirty(); } return; }
            EnsureCentral();
            ComplexClock += dt;
            // pausas por mover
            if (movePause.Count > 0)
            {
                var keys = new List<int>(movePause.Keys);
                foreach (var k in keys) { movePause[k] -= dt; if (movePause[k] <= 0f) { movePause.Remove(k); Dirty(); } }
            }
            TickModules(dt);
            TickFuel(dt);
            TickCarts(dt);
            TickSchool(dt);
            if (PartyT > 0f) { PartyT -= dt; if (PartyT <= 0f) { PartyT = 0f; Dirty(); } }
            if (TutDone) { TickCxEvent(dt); TickContracts(dt); TickDrill(dt); }
        }

        void TickModules(float dt)
        {
            for (int i = Modules.Count - 1; i >= 0; i--)
            {
                var m = Modules[i];
                if (m.TunedT > 0) m.TunedT = Math.Max(0, m.TunedT - dt);
                if (m.Expires > 0 && ComplexClock >= m.Expires && ModAt(m.X, m.Z, m.F + 1) == null)
                {
                    Modules.RemoveAt(i);
                    Dirty();
                    ModuleExpired?.Invoke(m);
                    continue;
                }
                if (m.Work > 0)
                {
                    m.Work -= dt;
                    if (m.Work <= 0) { m.Work = 0; WorkDoneMod(m); }
                }
            }
        }

        void WorkDoneMod(Module m)
        {
            if (m.Kind == ModKind.Secret) { RevealSecret(m); return; }
            if (m.Stage < MaxStage) m.Stage++;
            Dirty();
            ModuleChanged?.Invoke(m);
            ModuleReady?.Invoke(m);
        }

        /// <summary>Cada generador quema 1 carbon por minuto de la ciudad; sin carbon se apagan.</summary>
        void TickFuel(float dt)
        {
            int gens = 0;
            foreach (var m in Modules) if (m.Kind == ModKind.Generator && !m.Broken) gens++;
            if (gens == 0) return;
            bool was = FuelOk;
            FuelT -= dt * gens;
            if (FuelT <= 0)
            {
                if (Stock[(int)Res.Coal] > 0) { Stock[(int)Res.Coal]--; FuelT += 60; }
                else FuelT = 0;
            }
            if (was != FuelOk) Dirty();
        }

        /// <summary>Taladro con energia: una vagoneta de mineral raro cada 3 min (x2 con el Topo al lado).</summary>
        float drillT;
        void TickDrill(float dt)
        {
            var d = FirstMod(ModKind.Drill);
            if (d == null || !Summary.Powered.Contains(d.Id) || d.Broken) return;
            drillT += dt * (Summary.Combos[10] ? 2f : 1f) * (float)StageK(d);
            if (drillT < 180f) return;
            drillT = 0f;
            int units = 2;
            double v = Ores[OreRare].Value * units * PriceMult();
            Mined[OreRare]++;
            ContractCount(0, OreRare, units);
            SpawnCart(d, OreRare, units, v);
        }

        /// <summary>Aula: el Maestro vecino ensenia a otro dormitorio vecino (+40 XP/min; x2 con la Biblioteca).</summary>
        void TickSchool(float dt)
        {
            var a = FirstMod(ModKind.Classroom);
            if (a == null) return;
            schoolAcc += dt;
            if (schoolAcc < 15) return;
            double secs = schoolAcc; schoolAcc = 0;
            TeachFor(a, secs);
        }

        void TeachFor(Module a, double secs)
        {
            bool master = false;
            var pupils = new List<int>();
            foreach (var n in Neighbors(a))
            {
                if (n.Kind != ModKind.Dorm) continue;
                if (n.Ch == 8) master = true; else pupils.Add(n.Ch);
            }
            if (!master || pupils.Count == 0) return;
            double xp = 40.0 / 60.0 * secs * StageK(a) * (Summary.Combos[3] ? 2 : 1);
            foreach (var ch in pupils)
                foreach (var m in Miners)
                {
                    if (m.Char != ch || m.Level >= MaxMinerLevel) continue;
                    m.Xp += (int)Math.Round(xp);
                    while (m.Level < MaxMinerLevel && m.Xp >= XpFor(m.Level)) { m.Xp -= XpFor(m.Level); m.Level++; AddStat("levelups", 1); MinerLevelUp?.Invoke(m); }
                }
        }

        // ------------------------------------------------------------ sin conexion
        /// <summary>Lo que el Complejo agrega a la ganancia sin conexion (sin simular vagonetas: valor estable).</summary>
        public double ComplexOfflineMult()
        {
            if (BarracksLevel < 1 || FirstMod(ModKind.Unload) == null) return 1.0;
            var s = Summary;
            double best = 1.0;
            foreach (var u in Modules)
            {
                if (u.Kind != ModKind.Unload) continue;
                double sum = 0; int n = 0;
                for (int k = 0; k < 5; k++) { sum += PathMult(u, k, s); n++; }
                best = Math.Max(best, sum / n);
            }
            double share = 0.6;   // parte de los viajes que van a la Descarga
            return 1.0 + share * (best - 1.0);
        }

        void ComplexOffline(double seconds)
        {
            if (BarracksLevel < 1 || seconds <= 0) return;
            ComplexClock += seconds;
            foreach (var m in Modules) if (m.Work > 0) m.Work = Math.Max(0.001, m.Work - seconds);   // termina en el proximo cuadro
            int gens = 0; foreach (var m in Modules) if (m.Kind == ModKind.Generator) gens++;
            if (gens > 0)
            {
                double need = seconds * gens - FuelT;
                int coal = (int)Math.Ceiling(Math.Max(0, need) / 60.0);
                int use = Math.Min(coal, Stock[(int)Res.Coal]);
                Stock[(int)Res.Coal] -= use;
                FuelT = Math.Max(0, FuelT + use * 60 - seconds * gens);
            }
            var a = FirstMod(ModKind.Classroom);
            if (a != null) TeachFor(a, Math.Min(seconds, 7200));
            Dirty();
        }
    }
}
