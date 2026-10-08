using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Recursos de la ciudad. Los numeros se guardan en la partida: solo se agregan al final.</summary>
    public enum Res
    {
        Stone = 0, Coal = 1, IronOre = 2, Wood = 3, Copper = 4, Sand = 5, Nugget = 6, RawGem = 7, Crystal = 8,
        IronBar = 9, Pick = 10, Cart = 11, Glass = 12, Cable = 13, GoldBar = 14, CutGem = 15, Ring = 16, Crown = 17,
        Lamp = 18, Motor = 19, Dynamite = 20, Compass = 21,
    }

    public sealed class ResDef
    {
        public Res Id;
        public string Name;
        public double Value;     // monedas por unidad al vender
        public int Th;           // nivel de Ayuntamiento desde el que existe
        public bool Raw { get { return (int)Id <= (int)Res.Crystal; } }
    }

    /// <summary>Receta de un edificio de transformacion.</summary>
    public sealed class Recipe
    {
        public BKind Kind;
        public Res Out;
        public int OutN = 1;
        public Res[] In;
        public int[] InN;
        public double Time;      // segundos
    }

    /// <summary>Edificio de extraccion: saca una materia prima sola cada Cycle segundos.</summary>
    public sealed class ExtractDef
    {
        public BKind Kind;
        public Res Out;
        public double Cycle;
    }

    /// <summary>Vagon del tren: pide Count de un recurso; Done cuando se cargo.</summary>
    public sealed class Wagon
    {
        public Res Want;
        public int Count;
        public bool Done;
        public double Coins;
    }

    /// <summary>Oferta del mercader: vende Count de un recurso por Price monedas.</summary>
    public sealed class MerchantOffer
    {
        public Res What;
        public int Count;
        public double Price;
        public bool Sold;
    }

    /// <summary>
    /// La ciudad (0.9, docs/PLAN_1_0.md seccion 1): recursos con tope de almacen, Ayuntamiento que habilita edificios,
    /// cadenas de produccion, constructores con obras que llevan tiempo, mercado y tren.
    /// </summary>
    public sealed partial class Island
    {
        // ------------------------------------------------------------ contenido
        static ResDef R(Res id, string name, double value, int th) { return new ResDef { Id = id, Name = name, Value = value, Th = th }; }

        public static readonly ResDef[] ResDefs =
        {
            R(Res.Stone, Loc.T("Piedra"), 2, 1), R(Res.Coal, Loc.T("Carbón"), 4, 2), R(Res.IronOre, Loc.T("Mineral de hierro"), 8, 3), R(Res.Wood, Loc.T("Madera"), 3, 1),
            R(Res.Copper, Loc.T("Cobre"), 6, 5), R(Res.Sand, Loc.T("Arena"), 4, 6), R(Res.Nugget, Loc.T("Pepita de oro"), 30, 8), R(Res.RawGem, Loc.T("Gema en bruto"), 100, 11),
            R(Res.Crystal, Loc.T("Cristal"), 160, 14),
            R(Res.IronBar, Loc.T("Lingote de hierro"), 35, 3), R(Res.Pick, Loc.T("Pico"), 90, 3), R(Res.Cart, Loc.T("Carretilla"), 160, 3), R(Res.Glass, Loc.T("Vidrio"), 30, 6),
            R(Res.Cable, Loc.T("Cable"), 45, 7), R(Res.GoldBar, Loc.T("Lingote de oro"), 160, 8), R(Res.CutGem, Loc.T("Gema tallada"), 300, 11), R(Res.Ring, Loc.T("Anillo"), 700, 11),
            R(Res.Crown, Loc.T("Corona"), 2200, 11), R(Res.Lamp, Loc.T("Lámpara"), 260, 7), R(Res.Motor, Loc.T("Motor"), 600, 7), R(Res.Dynamite, Loc.T("Explosivo"), 1200, 14),
            R(Res.Compass, Loc.T("Brújula"), 900, 14),
        };

        public static int ResCount { get { return ResDefs.Length; } }
        public static ResDef RDef(Res r) { return ResDefs[(int)r]; }

        static Recipe Rc(BKind k, Res o, double t, params object[] ins)
        {
            int n = ins.Length / 2;
            var rc = new Recipe { Kind = k, Out = o, Time = t, In = new Res[n], InN = new int[n] };
            for (int i = 0; i < n; i++) { rc.In[i] = (Res)ins[i * 2]; rc.InN[i] = (int)ins[i * 2 + 1]; }
            return rc;
        }

        public static readonly Recipe[] Recipes =
        {
            Rc(BKind.Foundry, Res.IronBar, 40, Res.IronOre, 2, Res.Coal, 1),
            Rc(BKind.Smithy, Res.Pick, 60, Res.IronBar, 1, Res.Wood, 2),
            Rc(BKind.Smithy, Res.Cart, 90, Res.IronBar, 2, Res.Wood, 3),
            Rc(BKind.GlassKiln, Res.Glass, 50, Res.Sand, 2, Res.Coal, 1),
            Rc(BKind.Workshop, Res.Cable, 60, Res.Copper, 2, Res.Coal, 1),
            Rc(BKind.Workshop, Res.Lamp, 120, Res.IronBar, 1, Res.Cable, 1, Res.Glass, 1),
            Rc(BKind.Workshop, Res.Motor, 240, Res.IronBar, 2, Res.Cable, 2),
            Rc(BKind.GoldRefinery, Res.GoldBar, 120, Res.Nugget, 3, Res.Coal, 1),
            Rc(BKind.Lapidary, Res.CutGem, 150, Res.RawGem, 1),
            Rc(BKind.Jeweler, Res.Ring, 240, Res.GoldBar, 1, Res.CutGem, 1),
            Rc(BKind.Jeweler, Res.Crown, 600, Res.GoldBar, 3, Res.CutGem, 2),
            Rc(BKind.Lab, Res.Dynamite, 300, Res.Crystal, 2, Res.Motor, 1),
            Rc(BKind.Lab, Res.Compass, 300, Res.Crystal, 1, Res.Glass, 2),
        };

        public static readonly ExtractDef[] Extractors =
        {
            new ExtractDef { Kind = BKind.Mine, Out = Res.Stone, Cycle = 8 },
            new ExtractDef { Kind = BKind.Sawmill, Out = Res.Wood, Cycle = 10 },
            new ExtractDef { Kind = BKind.CoalMine, Out = Res.Coal, Cycle = 12 },
            new ExtractDef { Kind = BKind.IronMine, Out = Res.IronOre, Cycle = 15 },
            new ExtractDef { Kind = BKind.CopperMine, Out = Res.Copper, Cycle = 18 },
            new ExtractDef { Kind = BKind.SandPit, Out = Res.Sand, Cycle = 15 },
            new ExtractDef { Kind = BKind.GoldMine, Out = Res.Nugget, Cycle = 30 },
            new ExtractDef { Kind = BKind.GemMine, Out = Res.RawGem, Cycle = 60 },
            new ExtractDef { Kind = BKind.CrystalWell, Out = Res.Crystal, Cycle = 90 },
        };

        public static ExtractDef Extractor(int kind)
        {
            foreach (var e in Extractors) if ((int)e.Kind == kind) return e;
            return null;
        }

        public static bool Produces(int kind)
        {
            foreach (var r in Recipes) if ((int)r.Kind == kind) return true;
            return false;
        }

        public static List<int> RecipesOf(BKind k)
        {
            var l = new List<int>();
            for (int i = 0; i < Recipes.Length; i++) if (Recipes[i].Kind == k) l.Add(i);
            return l;
        }

        /// <summary>Mineral de las vetas → materia prima que suma al deposito.</summary>
        public static readonly Res[] OreRes = { Res.Stone, Res.Copper, Res.IronOre, Res.Nugget, Res.RawGem, Res.Coal, Res.Crystal, Res.RawGem };

        // ------------------------------------------------------------ tiempos, costos y topes
        public const int MaxTh = 15;
        /// <summary>Las obras que terminan en 5 minutos o menos se terminan gratis con un toque.</summary>
        public const double FreeFinish = 300;
        /// <summary>Duracion de una obra hasta el nivel L (indice = nivel destino).</summary>
        static readonly double[] WorkTime = { 0, 5, 10, 30, 60, 120, 240, 480, 900, 1800, 3600, 5400, 7200, 10800, 14400, 21600 };
        static readonly double[] ThTime = { 0, 0, 20, 60, 180, 420, 900, 1800, 3600, 5400, 7200, 10800, 14400, 21600, 28800, 43200 };
        static readonly double[] ThCoins = { 0, 0, 150, 600, 2000, 6000, 15000, 35000, 80000, 180000, 400000, 900000, 2e6, 4.5e6, 1e7, 2.2e7 };
        /// <summary>Materiales para subir el Ayuntamiento al nivel indicado (pares recurso, cantidad).</summary>
        static readonly object[][] ThMats =
        {
            new object[0], new object[0],
            new object[] { Res.Stone, 10 },
            new object[] { Res.Stone, 25, Res.Wood, 15 },
            new object[] { Res.Wood, 30, Res.Coal, 15, Res.IronBar, 6 },
            new object[] { Res.IronBar, 15, Res.Pick, 5 },
            new object[] { Res.Pick, 10, Res.Cart, 3, Res.Copper, 30 },
            new object[] { Res.Glass, 15, Res.Cart, 6 },
            new object[] { Res.Cable, 15, Res.Glass, 20, Res.Lamp, 3 },
            new object[] { Res.Lamp, 8, Res.GoldBar, 5 },
            new object[] { Res.Motor, 5, Res.GoldBar, 12 },
            new object[] { Res.Motor, 10, Res.Lamp, 15 },
            new object[] { Res.CutGem, 10, Res.Ring, 3 },
            new object[] { Res.Ring, 8, Res.Motor, 15 },
            new object[] { Res.Crown, 3, Res.Ring, 12 },
            new object[] { Res.Compass, 5, Res.Dynamite, 10, Res.Crown, 6 },
        };

        /// <summary>Nivel del Ayuntamiento (el edificio central).</summary>
        public int Th { get { return Math.Max(1, Plots[0].Level); } }

        /// <summary>Nivel maximo al que puede llegar un edificio con el Ayuntamiento actual.</summary>
        public int LevelCap(BKind k)
        {
            var d = Def(k);
            if (k == BKind.Depot) return MaxTh;
            return Math.Min(d.MaxLevel, Th + 2);
        }

        public bool Unlocked(BKind k) { return Th >= Def(k).Th; }

        /// <summary>Cuantos edificios de este tipo se pueden tener.</summary>
        public static int MaxCount(BKind k) { return k == BKind.House ? MaxHouses : (Doubles(k) ? 2 : 1); }

        /// <summary>Extraccion basica que se puede duplicar (la segunda, con el Ayuntamiento 3 niveles mas arriba).</summary>
        public static bool Doubles(BKind k) { return k == BKind.Sawmill || k == BKind.Mine || k == BKind.CoalMine || k == BKind.IronMine; }

        /// <summary>Cuantos de este tipo permite el Ayuntamiento actual.</summary>
        public int CountCap(BKind k)
        {
            if (!Unlocked(k)) return 0;
            if (Doubles(k)) return Th >= Def(k).Th + 3 ? 2 : 1;
            return MaxCount(k);
        }

        /// <summary>Duracion (s) de construir (nivel 1) o mejorar hasta el nivel `to`.</summary>
        public double WorkSeconds(BKind k, int to)
        {
            if (k == BKind.Depot) return ThTime[Math.Min(to, MaxTh)];
            double t = WorkTime[Math.Min(to, WorkTime.Length - 1)];
            // los edificios de mas arriba tardan un poco mas para el mismo nivel
            return Math.Round(t * (1.0 + 0.06 * Math.Max(0, Def(k).Th - 1)));
        }

        /// <summary>Materiales para construir/mejorar hasta el nivel `to` (vacio si no pide).</summary>
        public List<KeyValuePair<Res, int>> MatsFor(BKind k, int to)
        {
            var l = new List<KeyValuePair<Res, int>>();
            if (k == BKind.Depot)
            {
                var m = ThMats[Math.Min(to, MaxTh)];
                for (int i = 0; i + 1 < m.Length; i += 2) l.Add(new KeyValuePair<Res, int>((Res)m[i], (int)m[i + 1]));
                return l;
            }
            if (to <= 1)
            {
                // construir: un poco de piedra/madera segun lo alto que este en el arbol
                int th = Def(k).Th;
                if (k == BKind.Barn || k == BKind.Warehouse) return l;   // los almacenes nunca piden lo que no entra
                if (th >= 2) l.Add(new KeyValuePair<Res, int>(Res.Stone, 4 + th * 2));
                if (th >= 3) l.Add(new KeyValuePair<Res, int>(Res.Wood, 4 + th * 2));
                return l;
            }
            // mejorar: bandas de materiales (cada banda ya esta desbloqueada al nivel de Ayuntamiento que la pide)
            if (to <= 3) l.Add(new KeyValuePair<Res, int>(Res.Stone, 3 + 2 * (to - 2)));
            else if (to <= 6) { l.Add(new KeyValuePair<Res, int>(Res.Stone, 6 + 3 * (to - 4))); l.Add(new KeyValuePair<Res, int>(Res.Wood, 5 + 3 * (to - 4))); }
            else if (to <= 8) { l.Add(new KeyValuePair<Res, int>(Res.IronBar, 4 + 2 * (to - 7))); l.Add(new KeyValuePair<Res, int>(Res.Wood, 15 + 5 * (to - 7))); }
            else if (to <= 10) { l.Add(new KeyValuePair<Res, int>(Res.Glass, 6 + 3 * (to - 9))); l.Add(new KeyValuePair<Res, int>(Res.Pick, 3 + (to - 9))); }
            else if (to <= 12) { l.Add(new KeyValuePair<Res, int>(Res.Cable, 6 + 3 * (to - 11))); l.Add(new KeyValuePair<Res, int>(Res.Cart, 2 + (to - 11))); }
            else { l.Add(new KeyValuePair<Res, int>(Res.Lamp, 3 + (to - 13))); l.Add(new KeyValuePair<Res, int>(Res.GoldBar, 2 + (to - 13))); }
            return l;
        }

        public bool HasMats(List<KeyValuePair<Res, int>> mats)
        {
            foreach (var kv in mats) if (Stock[(int)kv.Key] < kv.Value) return false;
            return true;
        }

        void PayMats(List<KeyValuePair<Res, int>> mats)
        {
            foreach (var kv in mats) Stock[(int)kv.Key] -= kv.Value;
        }

        /// <summary>Monedas de subir el Ayuntamiento al nivel `to`.</summary>
        public double ThCost(int to) { return to > MaxTh ? 0 : Math.Round(ThCoins[to] * IslandValue()); }

        // ------------------------------------------------------------ estado
        public readonly int[] Stock = new int[22];
        public int BonusBuilders;          // constructores comprados u obtenidos en ofertas
        public event Action<Plot> WorkDone;
        public event Action<Plot, int, int> Collected;   // parcela, recurso, cantidad
        public event Action<Plot> ProductReady;
        public event Action<string> StorageFull;          // "raw" o "prod"

        public int Builders() { return 2 + (Th >= 5 ? 1 : 0) + BonusBuilders + (Capataz ? 1 : 0); }

        public int BusyBuilders()
        {
            int n = 0;
            foreach (var p in Plots) if (p.Work > 0) n++;
            return n;
        }

        public int FreeBuilders() { return Builders() - BusyBuilders(); }

        // topes de almacen: materias primas en el Galpon, productos en el Almacen
        public int RawCap() { int l = Level(BKind.Barn); return 50 + 40 * l; }
        public int ProdCap() { int l = Level(BKind.Warehouse); return 15 + 20 * l; }

        public int RawStored()
        {
            int n = 0;
            for (int i = 0; i <= (int)Res.Crystal; i++) n += Stock[i];
            return n;
        }

        public int ProdStored()
        {
            int n = 0;
            for (int i = (int)Res.Crystal + 1; i < Stock.Length; i++) n += Stock[i];
            return n;
        }

        /// <summary>Lugar libre para el recurso (en su almacen).</summary>
        public int Room(Res r) { return RDef(r).Raw ? Math.Max(0, RawCap() - RawStored()) : Math.Max(0, ProdCap() - ProdStored()); }

        /// <summary>Suma al almacen hasta donde entre; devuelve cuanto entro. Lleno, hace lugar vendiendo la pila mas grande.</summary>
        public int AddRes(Res r, int n)
        {
            int k = Math.Min(n, Room(r));
            if (k < n) k += MakeRoom(r, n - k);
            if (k > 0) Stock[(int)r] += k;
            if (k < n) StorageFull?.Invoke(RDef(r).Raw ? "raw" : "prod");
            return k;
        }

        /// <summary>Pila que se vendio sola para hacer lugar (recurso, unidades, monedas).</summary>
        public event Action<Res, int, double> StorageSold;

        /// <summary>
        /// El almacen lleno nunca frena el progreso (auditoria final: a los 9 min el Galpon se llenaba de cobre y hierro
        /// que todavia no sirven y la piedra de las mejoras dejaba de entrar). Si no entra `r`, la pila mas grande del
        /// mismo almacen (si es mas grande que la de `r`) se vende sola al precio de venta para hacerle lugar.
        /// </summary>
        int MakeRoom(Res r, int need)
        {
            bool raw = RDef(r).Raw;
            int lo = raw ? 0 : (int)Res.Crystal + 1, hi = raw ? (int)Res.Crystal : Stock.Length - 1;
            int made = 0;
            for (int guard = 0; guard < 8 && made < need; guard++)
            {
                int big = -1;
                for (int i = lo; i <= hi; i++)
                    if (i != (int)r && Stock[i] > Stock[(int)r] + made + 1 && (big < 0 || Stock[i] > Stock[big])) big = i;
                if (big < 0) break;
                int sell = Math.Min(need - made, Math.Max(1, (Stock[big] - Stock[(int)r] - made) / 2));
                Stock[big] -= sell;
                double v = SellPrice((Res)big) * sell;
                Earn(v);
                AddStat("autosold", sell);
                StorageSold?.Invoke((Res)big, sell, v);
                made += sell;
            }
            return made;
        }

        // ------------------------------------------------------------ obras
        /// <summary>Gemas para terminar ya una obra (gratis si faltan 5 min o menos).</summary>
        public int SpeedUpGems(Plot p)
        {
            if (p.Work <= FreeFinish) return 0;
            double min = p.Work / 60.0;
            return Math.Max(1, (int)Math.Ceiling(Math.Sqrt(min) * 2.2));
        }

        /// <summary>Terminar la obra ya (gratis en los ultimos 5 minutos, si no con gemas).</summary>
        public bool SpeedUp(Plot p)
        {
            if (p.Work <= 0) return false;
            int g = SpeedUpGems(p);
            if (Gems < g) return false;
            Gems -= g;
            if (g > 0) AddStat("speedups", 1);
            p.Work = 0.0001;
            TickWork(p, 1);
            return true;
        }

        /// <summary>Recorta `seconds` de la obra (anuncio con premio: 30 min).</summary>
        public void CutWork(Plot p, double seconds)
        {
            if (p.Work <= 0) return;
            p.Work = Math.Max(0.0001, p.Work - seconds);
        }

        void StartWork(Plot p, BKind k, int to)
        {
            double t = WorkSeconds(k, to);
            p.Work = Math.Max(0.0001, t);
            p.WorkTotal = p.Work;
        }

        void TickWork(Plot p, double dt)
        {
            if (p.Work <= 0) return;
            p.Work -= dt;
            if (p.Work > 0) return;
            p.Work = 0; p.WorkTotal = 0;
            p.Level++;
            p.BuildT = 1.2f;
            if (p.Building == (int)BKind.Depot) AddStat("th", 1);
            AddStat("works", 1);
            SyncMiners(false);
            BuildingChanged?.Invoke(p);
            WorkDone?.Invoke(p);
        }

        /// <summary>Para pruebas, capturas y migracion: termina todas las obras ya.</summary>
        public void FinishAllWork()
        {
            foreach (var p in Plots) if (p.Work > 0) { p.Work = 0.0001; TickWork(p, 1); }
        }

        // ------------------------------------------------------------ produccion
        public int QueueSlots(Plot p) { return Math.Min(6, 2 + p.Level / 2); }
        public int ExtractUnits(Plot p) { return 1 + p.Level / 3; }
        public int ExtractBuffer(Plot p) { return 6 + 3 * p.Level; }

        /// <summary>Velocidad de produccion del edificio (los gerentes y la maravilla la suben).</summary>
        public double ProdSpeed(Plot p) { return (1.0 + 0.04 * (p.Level - 1)) * (TurboT > 0f ? 2.0 : 1.0); }

        public bool CanQueue(Plot p, int recipe)
        {
            if (p.Building < 0 || p.Level < 1 || recipe < 0 || recipe >= Recipes.Length) return false;
            var rc = Recipes[recipe];
            if ((int)rc.Kind != p.Building || !Unlocked(rc.Kind) || RDef(rc.Out).Th > Th) return false;
            if (p.Queue.Count >= QueueSlots(p)) return false;
            for (int i = 0; i < rc.In.Length; i++) if (Stock[(int)rc.In[i]] < rc.InN[i]) return false;
            return true;
        }

        /// <summary>Encola una receta: los ingredientes se descuentan al encolar.</summary>
        public bool QueueRecipe(Plot p, int recipe)
        {
            if (!CanQueue(p, recipe)) return false;
            var rc = Recipes[recipe];
            for (int i = 0; i < rc.In.Length; i++) Stock[(int)rc.In[i]] -= rc.InN[i];
            p.Queue.Add(recipe);
            return true;
        }

        void TickProduction(Plot p, double dt)
        {
            if (p.Building < 0 || p.Level < 1 || p.Work > 0) return;   // en obra no produce
            var ex = Extractor(p.Building);
            if (ex != null)
            {
                p.ReadyRes = (int)ex.Out;
                int cap = ExtractBuffer(p);
                if (p.Ready >= cap) { p.ProdT = 0; return; }
                p.ProdT += dt * ProdSpeed(p);
                while (p.ProdT >= ex.Cycle && p.Ready < cap)
                {
                    p.ProdT -= ex.Cycle;
                    bool was = p.Ready > 0;
                    p.Ready = Math.Min(cap, p.Ready + ExtractUnits(p));
                    if (!was) ProductReady?.Invoke(p);
                }
                return;
            }
            if (p.Queue.Count == 0) return;
            var rc = Recipes[p.Queue[0]];
            // lo terminado espera en el edificio; solo se apila el mismo producto
            if (p.Ready > 0 && p.ReadyRes != (int)rc.Out) return;
            p.ProdT += dt * ProdSpeed(p);
            if (p.ProdT < rc.Time) return;
            p.ProdT = 0;
            p.Queue.RemoveAt(0);
            p.ReadyRes = (int)rc.Out;
            p.Ready += rc.OutN;
            AddStat("made", rc.OutN);
            AddStat("made" + (int)rc.Out, rc.OutN);
            ProductReady?.Invoke(p);
        }

        /// <summary>Progreso 0..1 de lo que se esta haciendo en el edificio.</summary>
        public float ProdProgress(Plot p)
        {
            var ex = Extractor(p.Building);
            if (ex != null) return (float)Math.Min(1.0, p.ProdT / ex.Cycle);
            if (p.Queue.Count == 0) return 0f;
            return (float)Math.Min(1.0, p.ProdT / Recipes[p.Queue[0]].Time);
        }

        /// <summary>Cobra lo listo del edificio al almacen; devuelve cuanto entro (0 si el almacen esta lleno).</summary>
        public int Collect(Plot p)
        {
            if (p.Ready <= 0 || p.ReadyRes < 0) return 0;
            var r = (Res)p.ReadyRes;
            int got = AddRes(r, p.Ready);
            if (got <= 0) return 0;
            p.Ready -= got;
            AddStat("collected", got);
            if (Extractor(p.Building) == null && p.Ready == 0 && p.Queue.Count == 0) p.ReadyRes = -1;
            Collected?.Invoke(p, (int)r, got);
            return got;
        }

        /// <summary>Gerentes: con la oficina construida, las minas se cobran solas cada pocos segundos.</summary>
        float managerT;

        void TickManagers(float dt)
        {
            int l = Level(BKind.Managers);
            if (l <= 0) return;
            managerT += dt;
            if (managerT < Math.Max(3f, 12f - l)) return;
            managerT = 0f;
            foreach (var p in Plots)
                if (p.Ready > 0 && Extractor(p.Building) != null && p.Work <= 0) Collect(p);
        }

        // ------------------------------------------------------------ mercado
        /// <summary>Precio de venta: 80 % en el Mercado; sin Mercado, el almacen igual compra al 40 % (para hacer lugar).</summary>
        public double SellPrice(Res r) { return Math.Max(1, Math.Round(RDef(r).Value * (Level(BKind.Market) > 0 ? 0.8 : 0.4) * PriceMult())); }

        public double Sell(Res r, int n)
        {
            n = Math.Min(n, Stock[(int)r]);
            if (n <= 0) return 0;
            Stock[(int)r] -= n;
            double v = SellPrice(r) * n;
            Earn(v);
            AddStat("market_sold", n);
            return v;
        }

        public readonly List<MerchantOffer> Merchant = new List<MerchantOffer>();
        public double MerchantT;   // segundos hasta que renueva
        public const double MerchantEvery = 3600;

        public void RefreshMerchant()
        {
            Merchant.Clear();
            var pool = new List<Res>();
            foreach (var d in ResDefs) if (d.Th <= Th) pool.Add(d.Id);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int k = rng.Next(pool.Count);
                var r = pool[k]; pool.RemoveAt(k);
                int n = RDef(r).Raw ? 10 + rng.Next(11) : 2 + rng.Next(4);
                Merchant.Add(new MerchantOffer { What = r, Count = n, Price = Math.Round(RDef(r).Value * n * 1.4 * IslandValue()) });
            }
            MerchantT = MerchantEvery;
        }

        public bool BuyOffer(int i)
        {
            if (i < 0 || i >= Merchant.Count || Level(BKind.Market) <= 0) return false;
            var o = Merchant[i];
            if (o.Sold || Coins < o.Price || Room(o.What) < o.Count) return false;
            Coins -= o.Price;
            Stock[(int)o.What] += o.Count;
            o.Sold = true;
            return true;
        }

        // ------------------------------------------------------------ tren
        public readonly List<Wagon> Wagons = new List<Wagon>();
        public double TrainT = 120;        // segundos hasta que llega (o hasta que se va, si esta)
        public bool TrainHere;
        public const double TrainStay = 1200, TrainEvery = 1500;
        public event Action TrainArrived;
        public event Action<bool> TrainLeft;   // true = se fue con todos los vagones cargados
        public event Action<Wagon> WagonLoaded;

        void TickTrain(float dt)
        {
            if (Level(BKind.Train) <= 0) return;
            TrainT -= dt;
            if (TrainT > 0) return;
            if (TrainHere) { TrainGo(false); return; }
            TrainHere = true;
            TrainT = TrainStay;
            Wagons.Clear();
            var pool = new List<Res>();
            foreach (var d in ResDefs) if (d.Th <= Th && d.Id != Res.Stone) pool.Add(d.Id);
            for (int i = 0; i < 3; i++)
            {
                var r = pool[rng.Next(pool.Count)];
                int n = RDef(r).Raw ? 8 + rng.Next(8) : 2 + rng.Next(3);
                Wagons.Add(new Wagon { Want = r, Count = n, Coins = Math.Round(RDef(r).Value * n * 2.5 * PriceMult()) });
            }
            TrainArrived?.Invoke();
        }

        public bool LoadWagon(int i)
        {
            if (!TrainHere || i < 0 || i >= Wagons.Count) return false;
            var w = Wagons[i];
            if (w.Done || Stock[(int)w.Want] < w.Count) return false;
            Stock[(int)w.Want] -= w.Count;
            w.Done = true;
            Earn(w.Coins);
            AddStat("wagons", 1);
            WagonLoaded?.Invoke(w);
            bool all = true;
            foreach (var x in Wagons) if (!x.Done) all = false;
            if (all) TrainGo(true);
            return true;
        }

        void TrainGo(bool full)
        {
            if (full) { Gems += 2; GiveChest(1); AddStat("trains", 1); FeedPiggy(3); }
            TrainHere = false;
            Wagons.Clear();
            TrainT = TrainEvery / (1.0 + 0.08 * (Level(BKind.Train) - 1));
            TrainLeft?.Invoke(full);
        }

        /// <summary>Para pruebas y capturas: el tren llega en el proximo tick.</summary>
        public void TrainSoon() { if (!TrainHere) TrainT = 0; }

        // ------------------------------------------------------------ avance de la ciudad
        void TickCity(float dt)
        {
            foreach (var p in Plots) { TickWork(p, dt); TickProduction(p, dt); }
            TickAutoMake(dt);
            TickManagers(dt);
            TickTrain(dt);
            if (Level(BKind.Market) > 0)
            {
                MerchantT -= dt;
                if (MerchantT <= 0 || Merchant.Count == 0) RefreshMerchant();
            }
        }

        /// <summary>Lo que paso mientras no estaba: las obras y la produccion siguen en tiempo real.</summary>
        void AdvanceCity(double seconds)
        {
            if (seconds <= 0) return;
            seconds = Math.Min(seconds, 7 * 24 * 3600);
            // por tramos (auditoria final): en cada tramo los talleres guardan lo fabricado en el Almacen y vuelven a
            // encolar lo que falta. Antes solo se hacia lo que ya estaba en la cola al irse (<= 6 piezas) y lo hecho
            // quedaba "listo para cobrar": la cadena cable -> motor se cortaba y un jugador casual (5 min cada 3 h)
            // pasaba 4,6 dias en el Ayuntamiento 10.
            double chunk = Math.Max(600, seconds / 200);
            for (double left = seconds; left > 1e-6; left -= chunk)
            {
                double ch = Math.Min(chunk, left);
                AutoMake();
                foreach (var p in Plots)
                {
                    double s = ch;
                    if (p.Work > 0) { double w = Math.Min(s, p.Work); TickWork(p, w); s -= w; }
                    // produccion: a pasos de 10 s (barato y suficiente para colas y buffers)
                    int steps = (int)Math.Min(400, Math.Ceiling(s / 10.0));
                    double step = steps > 0 ? s / steps : 0;
                    for (int i = 0; i < steps; i++) TickProduction(p, step);
                    if (p.Building >= 0 && Extractor(p.Building) == null && p.Ready > 0) Collect(p);
                }
            }
            for (int i = 0; i < 6; i++) TickManagers(60f);
        }

        // ------------------------------------------------------------ guardado
        Dictionary<string, object> CityObj()
        {
            var stock = new List<object>();
            foreach (var x in Stock) stock.Add(x);
            var plots = new List<object>();
            foreach (var p in Plots)
            {
                var q = new List<object>();
                foreach (var r in p.Queue) q.Add(r);
                plots.Add(new Dictionary<string, object> { { "w", p.Work }, { "wt", p.WorkTotal }, { "q", q }, { "t", p.ProdT }, { "r", p.Ready }, { "rr", p.ReadyRes } });
            }
            return new Dictionary<string, object>
            {
                { "stock", stock }, { "plots", plots }, { "bb", BonusBuilders }, { "train", TrainT }, { "here", TrainHere ? 1 : 0 },
            };
        }

        void LoadCity(Dictionary<string, object> d)
        {
            object so;
            if (d.TryGetValue("stock", out so) && so is List<object> sl)
                for (int i = 0; i < sl.Count && i < Stock.Length; i++) Stock[i] = (int)JsonRead.ToDouble(sl[i], 0);
            BonusBuilders = JsonRead.Int(d, "bb", 0);
            TrainT = JsonRead.Dbl(d, "train", 120);
            TrainHere = false;   // el tren que estaba se fue mientras no estabas
            object po;
            if (d.TryGetValue("plots", out po) && po is List<object> pl)
                for (int i = 0; i < pl.Count && i < Plots.Count; i++)
                {
                    var pd = pl[i] as Dictionary<string, object>;
                    if (pd == null) continue;
                    var p = Plots[i];
                    p.Work = JsonRead.Dbl(pd, "w", 0);
                    p.WorkTotal = JsonRead.Dbl(pd, "wt", 0);
                    p.ProdT = JsonRead.Dbl(pd, "t", 0);
                    p.Ready = JsonRead.Int(pd, "r", 0);
                    p.ReadyRes = JsonRead.Int(pd, "rr", -1);
                    p.Queue.Clear();
                    object qo;
                    if (pd.TryGetValue("q", out qo) && qo is List<object> ql)
                        foreach (var x in ql) { int r = (int)JsonRead.ToDouble(x, -1); if (r >= 0 && r < Recipes.Length) p.Queue.Add(r); }
                }
        }

        /// <summary>
        /// Partidas de 0.8 (sin ciudad): el Deposito pasa a ser el Ayuntamiento. Su nivel no traia desbloqueos, asi que
        /// se acota a 5 (todo lo que ya estaba construido sigue funcionando) y se regala un poco de piedra y madera.
        /// </summary>
        void MigrateTo09()
        {
            var th = Plots[0];
            th.Level = Math.Max(1, Math.Min(th.Level, 5));
            Stock[(int)Res.Stone] += 30;
            Stock[(int)Res.Wood] += 20;
        }
    }
}
