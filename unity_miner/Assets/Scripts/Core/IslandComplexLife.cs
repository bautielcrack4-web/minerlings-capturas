using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    public enum SecretOut { Miner = 0, Machine = 1, NewRoom = 2, Upgrade = 3, Expedition = 4, Gems = 5 }

    /// <summary>
    /// Vida del Complejo: eventos internos, contratos con reloj, planos y Sala secreta, modulos portatiles, adornos y
    /// fiesta. Todo con semilla del jugador (se repite igual en las pruebas) y sin castigos fuertes.
    /// </summary>
    public sealed partial class Island
    {
        // ------------------------------------------------------------ eventos internos
        public CxEvent CurEvent;
        float cxEventT = 600f;
        public int JamCart = -1;
        public event Action<CxEvent> CxEventStarted, CxEventEnded;

        void TickCxEvent(float dt)
        {
            if (CurEvent == null)
            {
                if (BarracksLevel < 4) return;
                cxEventT -= dt;
                if (cxEventT > 0f) return;
                cxEventT = 480f + (float)rng.NextDouble() * 360f;
                StartCxEvent();
                return;
            }
            var e = CurEvent;
            e.T -= dt;
            if (e.Kind == CxEventKind.Breakdown && e.Fixer >= 0) return;   // ya va alguien: espera a que llegue
            if (e.T <= 0f) EndCxEvent(false);
        }

        /// <summary>Elige un evento posible para lo que hay construido (null si ninguno aplica).</summary>
        public bool StartCxEvent(CxEventKind force = CxEventKind.None)
        {
            if (CurEvent != null) return false;
            var opts = new List<CxEventKind>();
            var machines = Machines();
            if (machines.Count > 0) opts.Add(CxEventKind.Breakdown);
            if (FirstMod(ModKind.Treasury) != null && Boundary().Count > 0) opts.Add(CxEventKind.Merchant);
            if (FirstMod(ModKind.Mess) != null && Summary.Mood >= 55) opts.Add(CxEventKind.Party);
            if (Modules.Count >= 5) opts.Add(CxEventKind.Inspector);
            if (Carts.Count > 0) opts.Add(CxEventKind.Jam);
            if (force != CxEventKind.None) { if (!opts.Contains(force)) return false; opts.Clear(); opts.Add(force); }
            if (opts.Count == 0) return false;
            var k = opts[rng.Next(opts.Count)];
            var ev = new CxEvent { Kind = k };
            switch (k)
            {
                case CxEventKind.Breakdown:
                {
                    var m = machines[rng.Next(machines.Count)];
                    m.Broken = true; ev.ModId = m.Id; ev.T = 180f;
                    Dirty();
                    break;
                }
                case CxEventKind.Merchant:
                {
                    var b = Boundary();
                    var m = b[rng.Next(b.Count)];
                    ev.X = m.X; ev.Z = m.Z; ev.ModId = m.Id; ev.T = 180f;
                    break;
                }
                case CxEventKind.Party: ev.T = 90f; break;
                case CxEventKind.Inspector: ev.T = 90f; break;
                case CxEventKind.Jam:
                {
                    var c = Carts[rng.Next(Carts.Count)];
                    JamCart = c.Id; ev.ModId = c.From; ev.T = 60f;
                    break;
                }
            }
            CurEvent = ev;
            AddStat("cxevents", 1);
            CxEventStarted?.Invoke(ev);
            return true;
        }

        void EndCxEvent(bool solved)
        {
            var e = CurEvent;
            if (e == null) return;
            CurEvent = null;
            if (e.Kind == CxEventKind.Breakdown)
            {
                var m = ModById(e.ModId);
                if (m != null) { m.Broken = false; if (solved) m.TunedT = 600; }
                foreach (var mm in Miners) if (mm.State == MState.ToFix || mm.State == MState.Fixing) { mm.Path = null; Set(mm, MState.Idle); }
                Dirty();
            }
            if (e.Kind == CxEventKind.Jam && JamCart >= 0)
            {
                foreach (var c in Carts) if (c.Id == JamCart && solved) c.Value *= 2;
                JamCart = -1;
            }
            if (solved) AddStat("cxsolved", 1);
            CxEventEnded?.Invoke(e);
        }

        List<Module> Machines()
        {
            var l = new List<Module>();
            foreach (var m in Modules)
                if ((m.Kind == ModKind.Crusher || m.Kind == ModKind.Smelter || m.Kind == ModKind.Generator || m.Kind == ModKind.Polisher || m.Kind == ModKind.Drill) && !m.Broken)
                    l.Add(m);
            return l;
        }

        /// <summary>Modulos de planta con una puerta hacia afuera (sin vecino de ese lado).</summary>
        public List<Module> Boundary()
        {
            var l = new List<Module>();
            var s = Summary;
            foreach (var m in Modules)
            {
                if (m.F != 0 || !s.Access.Contains(m.Id)) continue;
                if (OuterDoor(m) >= 0) l.Add(m);
            }
            return l;
        }

        /// <summary>Lado con puerta hacia afuera del modulo (preferencia: hacia la camara). -1 si ninguno.</summary>
        public int OuterDoor(Module m)
        {
            int doors = RotMask(MDef(m.Kind).Doors, m.Rot);
            int[] order = { 2, 1, 3, 0 };
            foreach (int s in order)
            {
                if ((doors & (1 << s)) == 0) continue;
                if (ModAt(m.X + SideDX[s], m.Z + SideDZ[s], m.F) != null) continue;
                return s;
            }
            return -1;
        }

        /// <summary>Especialista que sabe arreglar cada maquina (el Maestro arregla todo).</summary>
        public static int FixerChar(ModKind k)
        {
            switch (k)
            {
                case ModKind.Crusher: return 1;
                case ModKind.Smelter: return 2;
                case ModKind.Generator: return 3;
                case ModKind.Polisher: return 5;
                case ModKind.Drill: return 7;
                default: return 8;
            }
        }

        /// <summary>Mandar al minero adecuado a arreglar la maquina rota. false: no hay quien sepa (esperar o gemas).</summary>
        public bool SendFixer()
        {
            var e = CurEvent;
            if (e == null || e.Kind != CxEventKind.Breakdown || e.Fixer >= 0) return false;
            var mod = ModById(e.ModId);
            if (mod == null) { EndCxEvent(false); return false; }
            int want = FixerChar(mod.Kind);
            Miner best = null;
            foreach (var m in Miners)
            {
                if (m.Char != want && m.Char != 8) continue;
                if (m.State == MState.Spawning) continue;
                if (best == null || (m.Char == want && best.Char != want)) best = m;
            }
            if (best == null) return false;
            Release(best);
            if (best.CarryKind >= 0) { Earn(Ores[best.CarryKind].Value * best.CarryUnits * PriceMult()); best.CarryKind = -1; best.CarryUnits = 0; }
            e.Fixer = best.Id;
            best.Target = mod.Id;
            best.Path = PathInto(best, mod);
            best.PathI = 0;
            Set(best, MState.ToFix);
            return true;
        }

        public const int RushFixGems = 5;
        public bool RushFix()
        {
            var e = CurEvent;
            if (e == null || e.Kind != CxEventKind.Breakdown || Gems < RushFixGems) return false;
            Gems -= RushFixGems;
            EndCxEvent(true);
            return true;
        }

        /// <summary>El comerciante se puede arrastrar una vez a otra puerta exterior.</summary>
        public bool MoveMerchant(int x, int z)
        {
            var e = CurEvent;
            if (e == null || e.Kind != CxEventKind.Merchant || e.Moved) return false;
            var m = ModAt(x, z, 0);
            if (m == null || !Boundary().Contains(m)) return false;
            e.X = x; e.Z = z; e.ModId = m.Id; e.Moved = true;
            return true;
        }

        /// <summary>Los modulos a 2 celdas o menos del comerciante venden +50 %.</summary>
        public double MerchantMult(Module m)
        {
            var e = CurEvent;
            if (e == null || e.Kind != CxEventKind.Merchant || m.F != 0) return 1.0;
            return Math.Abs(m.X - e.X) + Math.Abs(m.Z - e.Z) <= 2 ? 1.5 : 1.0;
        }

        public bool RingBell()
        {
            var e = CurEvent;
            if (e == null || e.Kind != CxEventKind.Party) return false;
            foreach (var m in Miners) { m.Energy = 100f; MinerMood?.Invoke(m, "cheer"); }
            PartyT = 600f;
            EndCxEvent(true);
            Dirty();
            return true;
        }

        /// <summary>El inspector cuenta las sinergias: 1 a 3 cofres. Devuelve el tier del mejor.</summary>
        public int ClaimInspector()
        {
            var e = CurEvent;
            if (e == null || e.Kind != CxEventKind.Inspector) return -1;
            int pairs = Summary.Pairs.Count, n = 0;
            for (int i = 0; i < ComboCount; i++) if (Summary.Combos[i]) pairs += 2;
            int tier = pairs >= 8 ? 2 : pairs >= 4 ? 1 : 0;
            n = pairs >= 8 ? 3 : pairs >= 4 ? 2 : 1;
            for (int i = 0; i < n; i++) GiveChest(tier);
            EndCxEvent(true);
            return tier;
        }

        /// <summary>Tocar la vagoneta trabada (3 toques: sale con el doble).</summary>
        public bool TapJam()
        {
            var e = CurEvent;
            if (e == null || e.Kind != CxEventKind.Jam) return false;
            e.Taps++;
            if (e.Taps >= 3) EndCxEvent(true);
            return true;
        }

        void FixArrived(Miner m)
        {
            var e = CurEvent;
            if (e != null && e.Kind == CxEventKind.Breakdown && e.Fixer == m.Id) EndCxEvent(true);
        }

        // ------------------------------------------------------------ contratos
        public Contract ActiveContract;
        public readonly List<Contract> Offers = new List<Contract>();
        public int ContractsToday, ContractDay = -1, ContractExtra;
        float offerT;
        public event Action<Contract, bool> ContractEnded;   // true = cumplido

        public bool ContractsOpen { get { return BarracksLevel >= 6 && FirstMod(ModKind.Unload) != null; } }
        public int ContractsPerDay { get { return 3 + (FirstMod(ModKind.Dovecote) != null ? 1 : 0) + ContractExtra; } }

        /// <summary>Cambio de dia (lo llama la vista con el dia del calendario).</summary>
        public void ContractNewDay(int today)
        {
            if (today == ContractDay) return;
            ContractDay = today; ContractsToday = 0; ContractExtra = 0;
        }

        void TickContracts(float dt)
        {
            if (!ContractsOpen) return;
            var a = ActiveContract;
            if (a != null)
            {
                a.Left -= dt;
                if (a.Prog >= a.Target) FinishContract(true);
                else if (a.Left <= 0f) FinishContract(false);
            }
            if (offerT > 0f) offerT -= dt;
            if (Offers.Count < 2 && offerT <= 0f && ContractsToday + Offers.Count + (ActiveContract != null ? 1 : 0) < ContractsPerDay + 2)
            {
                var c = MakeContract();
                if (c != null) Offers.Add(c);
                offerT = Offers.Count < 2 ? 2f : 0f;
            }
        }

        /// <summary>Contrato que se puede cumplir con lo que hay (minerales que salen y modulos en las lineas).</summary>
        public Contract MakeContract()
        {
            var lines = new List<Module>();
            foreach (var u in Modules) if (u.Kind == ModKind.Unload && TracePath(u, false).Count > 0) lines.Add(u);
            if (lines.Count == 0) return null;
            float time = 240f + 30f * rng.Next(5);
            if (FirstMod(ModKind.Clock) != null) time += 60f;
            // viajes a la Descarga por segundo (medido jugando: ~0.012 por minero) con margen para que se cumpla
            double tripsPerSec = Math.Max(1, Miners.Count) * 0.012 * 0.6;
            var c = new Contract { Time = time, Left = time };
            int pick = rng.Next(3);
            var procs = new List<ModKind>();
            foreach (var u in lines) foreach (var p in TracePath(u, false)) if (!procs.Contains(p.Kind)) procs.Add(p.Kind);
            if (pick == 1 && procs.Contains(ModKind.Smelter))
            {
                c.Kind = 1; c.What = -1;
                c.Target = Math.Max(2, (int)Math.Round(tripsPerSec * time * 0.35 * Math.Max(1, CarryCap() / 2)));
            }
            else if (pick == 2 && procs.Count > 0)
            {
                c.Kind = 2; c.What = (int)procs[rng.Next(procs.Count)];
                c.Target = Math.Max(3, (int)Math.Round(tripsPerSec * time));
                c.Plans = rng.NextDouble() < 0.3 ? 1 : 0;
            }
            else
            {
                // un mineral que de verdad esta saliendo (segun lo picado), con su parte de los viajes
                long tot = 0; for (int k = 0; k <= OreGold; k++) tot += Mined[k];
                int what = OreStone; double share = 0.3;
                if (tot > 0)
                {
                    double r = rng.NextDouble() * tot;
                    for (int k = 0; k <= OreGold; k++) { r -= Mined[k]; if (r <= 0) { what = k; break; } }
                    share = (double)Mined[what] / tot;
                }
                c.Kind = 0; c.What = what;
                c.Target = Math.Max(3, (int)Math.Round(tripsPerSec * time * share * CarryCap() * Ores[what].Units));
            }
            double mult = FirstMod(ModKind.Dovecote) != null ? 1.2 : 1.0;
            c.Coins = Math.Round(CoinPrize(time * 1.6) * mult);
            c.Gems = (int)Math.Round((2 + rng.Next(3)) * mult);
            if (rng.NextDouble() < 0.3) c.Portable = (int)ModKind.PortLab + rng.Next(3);
            return c;
        }

        public bool AcceptContract(int i)
        {
            if (ActiveContract != null || i < 0 || i >= Offers.Count || ContractsToday >= ContractsPerDay) return false;
            ActiveContract = Offers[i];
            Offers.RemoveAt(i);
            ActiveContract.Active = true;
            ActiveContract.Left = ActiveContract.Time;
            ContractsToday++;
            return true;
        }

        void ContractCount(int kind, int what, int n)
        {
            var a = ActiveContract;
            if (a == null || a.Kind != kind) return;
            if (kind != 1 && a.What != what) return;
            a.Prog = Math.Min(a.Target, a.Prog + n);
        }

        void FinishContract(bool ok)
        {
            var a = ActiveContract;
            ActiveContract = null;
            if (ok)
            {
                Earn(a.Coins);
                Gems += a.Gems;
                if (a.Plans > 0) AddPlans(a.Plans);
                if (a.Portable >= 0) AddPortable((ModKind)a.Portable);
                AddStat("contracts", 1);
            }
            else offerT = 600f;   // sin castigo: la proxima oferta tarda un poco
            ContractEnded?.Invoke(a, ok);
        }

        // ------------------------------------------------------------ planos y Sala secreta
        public int Plans, SecretPity;
        public const int MaxPlans = 3;
        public event Action<int> PlansFound;
        public event Action<Module, SecretOut, int> SecretRevealed;   // modulo, premio, detalle (minero, tipo, gemas)

        public void AddPlans(int n)
        {
            int before = Plans;
            Plans = Math.Min(MaxPlans, Plans + n);
            if (Plans > before) PlansFound?.Invoke(Plans - before);
        }

        public double SecretCost() { return Math.Round(2000 * Math.Pow(2.0, Stat("secrets")) * IslandValue() / 10.0) * 10.0; }

        public bool CanBuySecret() { return BarracksLevel >= 2 && Plans > 0 && Coins >= SecretCost() && CountMod(ModKind.Secret) < 1 && FreeCells(ModKind.Secret).Count > 0; }

        public Module BuySecret(int x, int z, int f, int rot)
        {
            if (!CanBuySecret()) return null;
            if (rot < 0) rot = BestRotation(ModKind.Secret, x, z, f);
            if (rot < 0 || !CanPlaceMod(ModKind.Secret, x, z, f, rot)) return null;
            Coins -= SecretCost();
            Plans--;
            AddStat("secrets", 1);
            var m = AddModule(ModKind.Secret, x, z, f, rot, -1);
            m.Work = m.WorkTotal = 120;
            return m;
        }

        /// <summary>La Sala secreta se abre: premio al azar con garantia (a la 3ra sin algo nuevo, sale algo nuevo).</summary>
        void RevealSecret(Module m)
        {
            bool force = SecretPity >= 2;
            var opts = new List<KeyValuePair<SecretOut, double>>();
            bool minerLeft = !Found[9] || !Found[10];
            bool machineLeft = CountMod(ModKind.Magnet) < 2 || CountMod(ModKind.Clock) < 1;
            ModKind early = NextLocked();
            if (minerLeft) opts.Add(new KeyValuePair<SecretOut, double>(SecretOut.Miner, force ? 20 : 5));
            if (machineLeft) opts.Add(new KeyValuePair<SecretOut, double>(SecretOut.Machine, 20));
            if (early != ModKind.Central) opts.Add(new KeyValuePair<SecretOut, double>(SecretOut.NewRoom, 15));
            if (!force)
            {
                opts.Add(new KeyValuePair<SecretOut, double>(SecretOut.Upgrade, 25));
                opts.Add(new KeyValuePair<SecretOut, double>(SecretOut.Expedition, 15));
                opts.Add(new KeyValuePair<SecretOut, double>(SecretOut.Gems, 20));
            }
            if (opts.Count == 0) opts.Add(new KeyValuePair<SecretOut, double>(SecretOut.Gems, 1));
            double sum = 0; foreach (var o in opts) sum += o.Value;
            double r = rng.NextDouble() * sum;
            var pick = opts[0].Key;
            foreach (var o in opts) { r -= o.Value; if (r <= 0) { pick = o.Key; break; } }
            int detail = 0;
            switch (pick)
            {
                case SecretOut.Miner:
                {
                    int ch = !Found[9] ? 9 : 10;
                    m.Kind = ModKind.Dorm; m.Ch = ch; detail = ch;
                    AddMiner(ch, false, false);
                    break;
                }
                case SecretOut.Machine:
                    m.Kind = CountMod(ModKind.Magnet) < 2 ? ModKind.Magnet : ModKind.Clock;
                    detail = (int)m.Kind;
                    if ((MDef(m.Kind).Floors & (1 << m.F)) == 0) m.Kind = ModKind.Trophy;
                    break;
                case SecretOut.NewRoom:
                    EarlyUnlocks |= 1 << (int)early; detail = (int)early; m.Kind = ModKind.Trophy;
                    break;
                case SecretOut.Upgrade:
                {
                    var c = new List<Module>();
                    foreach (var o in Modules) if (o != m && o.Stage < MaxStage && o.Kind != ModKind.Central && o.Kind != ModKind.Corridor && o.Kind != ModKind.Trophy && o.Expires <= 0) c.Add(o);
                    if (c.Count > 0) { var t = c[rng.Next(c.Count)]; t.Stage++; detail = t.Id; ModuleChanged?.Invoke(t); }
                    else { Gems += 15; detail = 15; pick = SecretOut.Gems; }
                    m.Kind = ModKind.Trophy;
                    break;
                }
                case SecretOut.Expedition:
                    GiveChest(2); detail = 2; m.Kind = ModKind.Trophy;
                    break;
                default:
                    Gems += 15; detail = 15; m.Kind = ModKind.Trophy;
                    break;
            }
            bool isNew = pick == SecretOut.Miner || pick == SecretOut.Machine || pick == SecretOut.NewRoom;
            SecretPity = isNew ? 0 : SecretPity + 1;
            m.Rot = Math.Max(0, BestRotation(m.Kind, m.X, m.Z, m.F, m));
            Dirty();
            ModuleChanged?.Invoke(m);
            SecretRevealed?.Invoke(m, pick, detail);
        }

        /// <summary>Proximo tipo de modulo comprable que todavia esta bloqueado (Central = ninguno).</summary>
        ModKind NextLocked()
        {
            ModKind best = ModKind.Central; int bl = int.MaxValue;
            foreach (var d in ModDefs)
            {
                if (!d.Buyable || d.Kind == ModKind.Dorm || ModUnlocked(d.Kind)) continue;
                if (d.Level < bl) { bl = d.Level; best = d.Kind; }
            }
            return best;
        }

        /// <summary>La Camara experimental se habilita al descubrir su combinacion.</summary>
        public bool CanBuyExperimental()
        {
            return CombosFound[0] && CountMod(ModKind.Experimental) == 0 && Coins >= ModCost(ModKind.Experimental) && HasMats(ModMats(ModKind.Experimental)) && FreeCells(ModKind.Experimental).Count > 0;
        }

        public Module BuyExperimental(int x, int z, int f, int rot)
        {
            if (!CanBuyExperimental()) return null;
            if (rot < 0) rot = BestRotation(ModKind.Experimental, x, z, f);
            if (rot < 0 || !CanPlaceMod(ModKind.Experimental, x, z, f, rot)) return null;
            Coins -= ModCost(ModKind.Experimental);
            PayMats(ModMats(ModKind.Experimental));
            return AddModule(ModKind.Experimental, x, z, f, rot, -1);
        }

        // ------------------------------------------------------------ portatiles
        public static readonly ModKind[] PortableKinds = { ModKind.PortLab, ModKind.PortOven, ModKind.PartyBell };
        public readonly int[] PortInv = new int[3];
        public const double PortableSeconds = 1200;
        public event Action<ModKind> PortableGot;

        static int PortIdx(ModKind k) { return Array.IndexOf(PortableKinds, k); }

        public void AddPortable(ModKind k)
        {
            int i = PortIdx(k);
            if (i < 0) return;
            PortInv[i] = Math.Min(9, PortInv[i] + 1);
            PortableGot?.Invoke(k);
        }

        public Module PlacePortable(ModKind k, int x, int z, int f, int rot)
        {
            int i = PortIdx(k);
            if (i < 0 || PortInv[i] <= 0 || BarracksLevel < 1) return null;
            if (rot < 0) rot = BestRotation(k, x, z, f);
            if (rot < 0 || !CanPlaceMod(k, x, z, f, rot)) return null;
            PortInv[i]--;
            var m = AddModule(k, x, z, f, rot, -1);
            m.Expires = ComplexClock + PortableSeconds;
            AddStat("portables", 1);
            return m;
        }

        // ------------------------------------------------------------ adornos
        public const int DecoKinds = 6;
        public double DecoCost(int deco) { return Math.Round(150 * (1 + deco) * Math.Max(1, BarracksLevel) * IslandValue() / 10.0) * 10.0; }

        public bool BuyDeco(Module m, int deco)
        {
            if (m == null || deco < 1 || deco > DecoKinds || m.Deco == deco || Coins < DecoCost(deco)) return false;
            Coins -= DecoCost(deco);
            m.Deco = deco;
            AddStat("deco", 1);
            Dirty();
            ModuleChanged?.Invoke(m);
            return true;
        }

        /// <summary>Belleza que aporta el Complejo a la isla (los adornos no cambian la produccion).</summary>
        public int ComplexBeauty() { int b = 0; foreach (var m in Modules) if (m.Deco > 0) b += 2 + m.Deco; return b; }

        // ------------------------------------------------------------ guardado del Complejo
        Dictionary<string, object> ComplexObj()
        {
            double pending = 0; foreach (var c in Carts) pending += c.Value;
            var d = new Dictionary<string, object>
            {
                { "mods", ModulesObj() }, { "clock", ComplexClock }, { "early", EarlyUnlocks }, { "plans", Plans }, { "pity", SecretPity },
                { "fuel", FuelT }, { "pend", pending }, { "cday", ContractDay }, { "ctoday", ContractsToday }, { "cextra", ContractExtra },
            };
            var combos = new List<object>(); foreach (var b in CombosFound) combos.Add(b ? 1 : 0);
            d["combos"] = combos;
            var port = new List<object>(); foreach (var p in PortInv) port.Add(p);
            d["port"] = port;
            if (ActiveContract != null) d["contract"] = ContractObj(ActiveContract);
            return d;
        }

        static Dictionary<string, object> ContractObj(Contract c)
        {
            return new Dictionary<string, object>
            {
                { "k", c.Kind }, { "w", c.What }, { "t", c.Target }, { "p", c.Prog }, { "tm", c.Time }, { "l", c.Left },
                { "c", c.Coins }, { "g", c.Gems }, { "pl", c.Plans }, { "po", c.Portable },
            };
        }

        void LoadComplex(Dictionary<string, object> d)
        {
            object mo;
            if (d.TryGetValue("mods", out mo) && mo is List<object> ml) LoadModules(ml);
            ComplexClock = JsonRead.Dbl(d, "clock", 0);
            EarlyUnlocks = JsonRead.Int(d, "early", 0);
            Plans = Math.Max(0, Math.Min(MaxPlans, JsonRead.Int(d, "plans", 0)));
            SecretPity = JsonRead.Int(d, "pity", 0);
            FuelT = JsonRead.Dbl(d, "fuel", 0);
            ContractDay = JsonRead.Int(d, "cday", -1);
            ContractsToday = JsonRead.Int(d, "ctoday", 0);
            ContractExtra = JsonRead.Int(d, "cextra", 0);
            double pend = JsonRead.Dbl(d, "pend", 0);
            if (pend > 0 && !double.IsNaN(pend) && !double.IsInfinity(pend)) Earn(pend);   // vagonetas que iban en viaje
            object co;
            if (d.TryGetValue("combos", out co) && co is List<object> cl)
                for (int i = 0; i < cl.Count && i < ComboCount; i++) CombosFound[i] = JsonRead.ToDouble(cl[i], 0) > 0;
            object po;
            if (d.TryGetValue("port", out po) && po is List<object> pl)
                for (int i = 0; i < pl.Count && i < PortInv.Length; i++) PortInv[i] = Math.Max(0, (int)JsonRead.ToDouble(pl[i], 0));
            object ac;
            if (d.TryGetValue("contract", out ac) && ac is Dictionary<string, object> cd)
            {
                ActiveContract = new Contract
                {
                    Kind = JsonRead.Int(cd, "k", 0), What = JsonRead.Int(cd, "w", 0), Target = Math.Max(1, JsonRead.Int(cd, "t", 1)),
                    Prog = JsonRead.Int(cd, "p", 0), Time = (float)JsonRead.Dbl(cd, "tm", 240), Left = (float)JsonRead.Dbl(cd, "l", 0),
                    Coins = JsonRead.Dbl(cd, "c", 0), Gems = JsonRead.Int(cd, "g", 0), Plans = JsonRead.Int(cd, "pl", 0),
                    Portable = JsonRead.Int(cd, "po", -1), Active = true,
                };
            }
        }

        /// <summary>Al zarpar a otra isla el Complejo queda atras (los descubrimientos del libro se conservan).</summary>
        void ResetComplex()
        {
            Modules.Clear(); Carts.Clear(); Offers.Clear();
            ActiveContract = null; CurEvent = null; JamCart = -1;
            FuelT = 0; EarlyUnlocks = 0; PartyT = 0;
            Dirty();
        }
    }
}
