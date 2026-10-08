using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Tipos de edificio de la Isla Minera (ver docs/ISLA_MINERA.md).</summary>
    /// <summary>
    /// Tipos de edificio. Los numeros se guardan en la partida: solo se agregan al final. Depot es el Ayuntamiento
    /// (edificio central: recibe lo que traen los mineros y su nivel habilita todo lo demas).
    /// </summary>
    public enum BKind
    {
        House = 0, Depot = 1, Canteen = 2, Showers = 3, Smithy = 4, Mine = 5, Lighthouse = 6, Dock = 7,
        Barn = 8, Warehouse = 9, CoalMine = 10, Sawmill = 11, IronMine = 12, Foundry = 13, CopperMine = 14, SandPit = 15,
        GlassKiln = 16, Market = 17, Train = 18, Workshop = 19, Bank = 20, GoldMine = 21, GoldRefinery = 22, School = 23,
        Managers = 24, Hospital = 25, GemMine = 26, Lapidary = 27, Jeweler = 28, Airport = 29, CrystalWell = 30, Lab = 31,
        Barracks = 32,
    }

    /// <summary>Familia del edificio: define el color del techo (biblia A.2) y donde aparece en el catalogo.</summary>
    public enum BFamily { Extraction = 0, Transform = 1, Town = 2, Commerce = 3, Special = 4 }

    /// <summary>Estados de la IA de un minero.</summary>
    public enum MState { Idle, ToOre, Mining, ToDepot, ToCanteen, Eating, ToShowers, Showering, Resting, Spawning, ToDig, Digging, ToBuild, Building,
        ToUnload, ToDorm, InDorm, ToFix, Fixing, Leaving,
        ToRoom, InRoom, Queued }   // Plan Pueblo: comer/ducharse en habitaciones del Cuartel, con cola

    public sealed class BDef
    {
        public BKind Kind;
        public string Name, Desc;
        public int MaxLevel;
        public double BuildCost, UpCost, UpMult;
        public float Radius;     // huella en metros (para que los mineros la esquiven y para elegir parcela)
        public BFamily Family;
        public int Th = 1;       // nivel de Ayuntamiento que lo desbloquea
    }

    public sealed class OreDef
    {
        public string Name;
        public double Value;     // monedas por unidad
        public double Hp;
        public int Units;        // unidades que suelta al romperse
        public float Size;
    }

    public sealed class Plot
    {
        public int Id;
        public float X, Z;
        public int Building = -1;   // BKind o -1 libre
        public int Level;
        public float BuildT = -1f;  // >=0: animacion de obra (segundos restantes, solo visual)
        public int Ring;            // 0 = isla inicial; 1..3 se habilitan al expandir
        public bool Moved;          // el jugador la movio (se guarda su X, Z)
        // obra con tiempo (constructores): Work > 0 = en obra; si Level == 0 es la construccion inicial
        public double Work, WorkTotal;
        // produccion: cola de recetas (indices de Island.Recipes) o ciclo de extraccion; Ready = listo para cobrar
        public readonly List<int> Queue = new List<int>();
        public double ProdT;
        public int Ready;
        public int ReadyRes = -1;
        public bool Busy { get { return Work > 0; } }
    }

    /// <summary>Barco comprador: pide Count unidades de un mineral antes de Left segundos.</summary>
    public sealed class Ship
    {
        public int Kind, Count, Delivered;
        public float Left, Age;
        public bool Done;
    }

    /// <summary>Meta del rastreador (encadenadas).</summary>
    public sealed class IGoal
    {
        public string Text, Stat;
        public long Target;
        public double Coins;
        public int Gems;
    }

    public sealed class Ore
    {
        public int Id, Kind;
        public float X, Z;
        public double Hp, MaxHp;
        public int ClaimedBy = -1;
        public float Age;           // segundos desde que aparecio (para la animacion de nacimiento)
        public bool Dead;
        public bool Giant;          // veta gigante: la pican todos a la vez, paga por golpe y un premio al romperla
        public bool Sky;            // vino de la ruleta: cae del cielo en vez de salir de la tierra (solo visual)
        public bool Boss;           // jefe semanal (Golem de Roca): una veta gigante con fases
        public bool Legendary;      // yacimiento legendario: raro a proposito, de gema, paga x5 y trae cofre de oro
        public int BossPhase;
        public bool NightCrystal;   // cristal nocturno: solo de noche, lo toca el jugador (los mineros no), se apaga al amanecer
    }

    public sealed class Miner
    {
        public int Id, Home;        // Home = parcela de la casa
        public float X, Z, Face;    // Face = angulo (rad) hacia donde mira
        public MState State = MState.Spawning;
        /// <summary>Segundos extra quieto al nacer (la invocacion con carta dura mas que la chispa).</summary>
        public float Hold;
        public float T;             // tiempo en el estado actual
        public int Target = -1;     // ore id o parcela segun el estado
        public float TX, TZ;        // destino de caminata
        public int CarryKind = -1, CarryUnits;
        public float Energy = 100f, Clean = 100f, Fresh;   // Fresh = segundos de bono Loc.T("¡Fresco!")
        public float HitT;          // reloj del golpe (0..intervalo)
        public bool Moving;
        public int Hits;            // golpes totales (para animar)
        public int Char;            // personaje de la coleccion (Island.Roster)
        public bool Golden;         // version dorada (1 de cada 300)
        public int Level = 1, Xp;   // suben picando: mas rendimiento y equipo visible
        public int Friend;          // id del amigo (0 = ninguno)
        // Complejo (0.11): camino por adentro (x, z, piso por punto), punto actual, altura del piso y si esta adentro
        public List<float> Path;
        public int PathI;
        public float Y;
        public bool InComplex;
        public float QueueT;        // tiempo en la cola de una habitacion (orden de llegada)
    }

    /// <summary>
    /// Simulacion de la Isla Minera en C# puro (sin UnityEngine): edificios en parcelas, vetas que aparecen, mineros con
    /// necesidades e IA, economia y guardado. La vista de Unity solo dibuja lo que hay aca y avisa toques.
    /// Coordenadas: metros en el plano XZ, centro de la isla en (0,0).
    /// </summary>
    public sealed partial class Island
    {
        // ------------------------------------------------------------ contenido
        static BDef D(BKind k, string name, string desc, BFamily fam, int th, int max, double build, double up, double mult, float radius)
        {
            return new BDef { Kind = k, Name = name, Desc = desc, Family = fam, Th = th, MaxLevel = max, BuildCost = build, UpCost = up, UpMult = mult, Radius = radius };
        }

        public static readonly BDef[] Defs =
        {
            D(BKind.House, Loc.T("Casa de Mineros"), Loc.T("Cada nivel suma un minero."), BFamily.Town, 1, 6, 40, 15, 2.1, 1.5f),
            D(BKind.Depot, Loc.T("Ayuntamiento"), Loc.T("El corazón de la isla: recibe lo que traen los mineros y cada nivel desbloquea edificios."), BFamily.Special, 1, 15, 0, 35, 1.95, 1.6f),
            D(BKind.Canteen, Loc.T("Cantina"), Loc.T("Los mineros cansados comen y recuperan energía."), BFamily.Town, 1, 8, 90, 120, 2.0, 1.5f),
            D(BKind.Showers, Loc.T("Duchas"), Loc.T("Limpios rinden más: salen ¡Frescos! (+30% velocidad)."), BFamily.Town, 2, 8, 160, 200, 2.0, 1.3f),
            D(BKind.Smithy, Loc.T("Herrería"), Loc.T("Picos más fuertes para todos. Fabrica picos y carretillas."), BFamily.Transform, 3, 12, 300, 260, 1.9, 1.5f),
            D(BKind.Mine, Loc.T("Mina de piedra"), Loc.T("Saca piedra sola y deja ingreso constante."), BFamily.Extraction, 1, 12, 700, 600, 1.85, 1.6f),
            D(BKind.Lighthouse, Loc.T("Faro"), Loc.T("Atrae vetas raras (oro y gemas) y más vetas gigantes."), BFamily.Commerce, 4, 8, 1500, 1400, 2.1, 1.2f),
            D(BKind.Dock, Loc.T("Muelle"), Loc.T("Llegan barcos que pagan el triple por pedidos."), BFamily.Commerce, 3, 8, 1100, 1000, 2.0, 1.5f),
            D(BKind.Barn, Loc.T("Galpón"), Loc.T("Guarda las materias primas: piedra, madera, carbón, minerales."), BFamily.Town, 2, 15, 250, 220, 1.7, 1.5f),
            D(BKind.Warehouse, Loc.T("Almacén"), Loc.T("Guarda los productos: lingotes, herramientas, vidrio, joyas."), BFamily.Town, 2, 15, 400, 300, 1.7, 1.5f),
            D(BKind.CoalMine, Loc.T("Mina de carbón"), Loc.T("Saca carbón: el combustible de hornos y fundiciones."), BFamily.Extraction, 2, 12, 350, 300, 1.8, 1.5f),
            D(BKind.Sawmill, Loc.T("Aserradero"), Loc.T("Corta madera de los árboles de la isla."), BFamily.Extraction, 1, 12, 40, 110, 1.8, 1.5f),
            D(BKind.IronMine, Loc.T("Mina de hierro"), Loc.T("Saca mineral de hierro."), BFamily.Extraction, 3, 12, 900, 700, 1.8, 1.5f),
            D(BKind.Foundry, Loc.T("Fundición"), Loc.T("Hierro y carbón se vuelven lingotes."), BFamily.Transform, 3, 12, 1200, 900, 1.8, 1.6f),
            D(BKind.CopperMine, Loc.T("Mina de cobre"), Loc.T("Saca cobre para cables y piezas."), BFamily.Extraction, 5, 12, 5000, 3500, 1.8, 1.5f),
            D(BKind.SandPit, Loc.T("Cantera de arena"), Loc.T("Saca arena para el vidrio."), BFamily.Extraction, 6, 12, 9000, 6000, 1.8, 1.5f),
            D(BKind.GlassKiln, Loc.T("Horno de vidrio"), Loc.T("Arena y carbón se vuelven vidrio."), BFamily.Transform, 6, 12, 12000, 8000, 1.8, 1.5f),
            D(BKind.Market, Loc.T("Mercado"), Loc.T("Vendé lo que sobra y comprale al mercader lo que falta."), BFamily.Commerce, 5, 10, 6000, 4000, 1.8, 1.6f),
            D(BKind.Train, Loc.T("Estación de tren"), Loc.T("El tren lleva pedidos al continente: vagones con premio."), BFamily.Commerce, 6, 10, 15000, 9000, 1.8, 1.7f),
            D(BKind.Workshop, Loc.T("Taller mecánico"), Loc.T("Fabrica cables, lámparas y motores."), BFamily.Transform, 7, 12, 30000, 20000, 1.8, 1.6f),
            D(BKind.Bank, "Banco", Loc.T("Más horas de ganancia mientras no jugás."), BFamily.Town, 7, 10, 35000, 22000, 1.8, 1.5f),
            D(BKind.GoldMine, Loc.T("Mina de oro"), Loc.T("Saca pepitas de oro."), BFamily.Extraction, 8, 12, 70000, 45000, 1.8, 1.5f),
            D(BKind.GoldRefinery, Loc.T("Refinería de oro"), Loc.T("Funde pepitas en lingotes de oro."), BFamily.Transform, 8, 12, 90000, 60000, 1.8, 1.6f),
            D(BKind.School, Loc.T("Escuela de mineros"), Loc.T("Los mineros aprenden más rápido (+20% experiencia por nivel)."), BFamily.Town, 5, 10, 7000, 5000, 1.8, 1.5f),
            D(BKind.Managers, Loc.T("Oficina de gerentes"), Loc.T("Un gerente cobra solo lo que sacan las minas."), BFamily.Town, 4, 10, 2500, 1800, 1.8, 1.5f),
            D(BKind.Hospital, Loc.T("Hospital"), Loc.T("Los mineros cansados se recuperan el doble de rápido."), BFamily.Town, 9, 10, 200000, 130000, 1.8, 1.5f),
            D(BKind.GemMine, Loc.T("Mina de gemas"), Loc.T("Saca gemas en bruto."), BFamily.Extraction, 11, 12, 900000, 600000, 1.8, 1.5f),
            D(BKind.Lapidary, Loc.T("Lapidario"), Loc.T("Talla las gemas en bruto."), BFamily.Transform, 11, 12, 1000000, 650000, 1.8, 1.5f),
            D(BKind.Jeweler, Loc.T("Joyería"), Loc.T("Oro y gemas talladas se vuelven anillos y coronas."), BFamily.Transform, 11, 12, 1300000, 800000, 1.8, 1.6f),
            D(BKind.Airport, Loc.T("Aeropuerto de globos"), Loc.T("Pedidos de lujo que pagan en gemas."), BFamily.Commerce, 12, 10, 2500000, 1500000, 1.8, 1.7f),
            D(BKind.CrystalWell, Loc.T("Pozo de cristal"), Loc.T("Saca cristal mágico de las profundidades."), BFamily.Extraction, 14, 12, 9000000, 6000000, 1.8, 1.4f),
            D(BKind.Lab, Loc.T("Laboratorio"), Loc.T("Cristal y motores se vuelven explosivos y brújulas."), BFamily.Transform, 14, 12, 12000000, 8000000, 1.8, 1.6f),
            // 0.10: el Cuartel modular (habitaciones alrededor que traen especialistas)
            D(BKind.Barracks, Loc.T("Cuartel de mineros"), Loc.T("Cada habitación trae un minero especialista."), BFamily.Town, 2, 9, 300, 600, 2.3, 2.9f),
        };

        public static readonly OreDef[] Ores =
        {
            new OreDef { Name = Loc.T("Piedra"), Value = 2, Hp = 3, Units = 1, Size = 0.55f },
            new OreDef { Name = Loc.T("Cobre"), Value = 6, Hp = 6, Units = 1, Size = 0.6f },
            new OreDef { Name = Loc.T("Hierro"), Value = 11, Hp = 15, Units = 1, Size = 0.65f },
            new OreDef { Name = Loc.T("Oro"), Value = 32, Hp = 28, Units = 2, Size = 0.7f },
            new OreDef { Name = Loc.T("Gema"), Value = 120, Hp = 50, Units = 2, Size = 0.6f },
            // 0.10: vetas de los especialistas (nacen cuando su habitacion esta construida)
            new OreDef { Name = Loc.T("Carbón"), Value = 8, Hp = 8, Units = 1, Size = 0.62f },
            new OreDef { Name = Loc.T("Cristal"), Value = 55, Hp = 34, Units = 2, Size = 0.66f },
            new OreDef { Name = Loc.T("Mineral raro"), Value = 210, Hp = 70, Units = 2, Size = 0.64f },
        };

        public const float IslandRadius = 15f;     // borde caminable de la isla inicial (x1.25, Plan Pueblo: aire, menos caos)
        public static readonly float[] Radii = { 15f, 18.75f, 22.5f, 27f };   // x1.25 (Plan Pueblo)
        public static readonly double[] ExpandCost = { 1200, 15000, 150000 };
        /// <summary>
        /// Nivel de Ayuntamiento que pide cada ampliacion. La tercera era a 8: hasta el nivel 5 hay 23 edificios posibles
        /// para 23 parcelas, y el Ayuntamiento 6 pide vidrio (Cantera de arena + Horno, ambos de nivel 6) = sin lugar,
        /// bloqueo sin salida (auditoria final, simulacion del Core).
        /// </summary>
        public static readonly int[] ExpandTh = { 3, 5, 6 };
        /// <summary>Radio caminable actual (crece al expandir).</summary>
        public float Radius { get { return Radii[Math.Min(Expand, Radii.Length - 1)]; } }
        public const float WalkSpeed = 1.9f;       // m/s (era 2.4: pedido del creador, "que caminen tranquilos", sin caos)
        public const float HitInterval = 0.95f;   // era 0.75: "que piquen lentos" (cada golpe se lee)
        const float ReachDist = 0.85f;

        // ------------------------------------------------------------ estado
        public double Coins;
        public double TotalEarned;
        public int Gems;
        public int Expand;                 // 0..3
        public int GoalIdx;
        public float TurboT;               // segundos de turbo x2
        public double LastSeen;            // reloj del jugador (segundos), para ganancias sin conexion
        public Ship CurShip;
        float giantT = 150f, shipT = 60f, mineT;
        public readonly Dictionary<string, long> Stats = new Dictionary<string, long>();
        public readonly long[] Mined = new long[8];
        public readonly List<Plot> Plots = new List<Plot>();
        public readonly List<Ore> OreList = new List<Ore>();
        public readonly List<Miner> Miners = new List<Miner>();
        int nextOre = 1, nextMiner = 1;
        float spawnT;
        readonly Random rng;

        // ------------------------------------------------------------ eventos para la vista
        public event Action<Ore> OreSpawned;
        public event Action<Ore, Miner> OreHit;
        public event Action<Ore, Miner> OreBroken;
        public event Action<Miner, double> Deposited;          // minero, monedas
        public event Action<Miner> MinerSpawned;
        public event Action<Plot> BuildingChanged;
        public event Action<Miner, string> MinerMood;          // "fresh", "fed", "tired", "dirty"
        public event Action<Ore> GiantSpawned;
        public event Action<Ore, double> GiantPaid;            // veta gigante: monedas por golpe o premio final
        public event Action<Ship> ShipArrived;
        public event Action<Ship, bool> ShipLeft;              // true = pedido cumplido
        public event Action<IGoal> GoalDone;
        public event Action<double> MinePaid;                  // ingreso de la mina (cada pocos segundos)
        public event Action<int> Expanded;
        /// <summary>La tierra nueva de una ampliacion revelo su hallazgo (el Yacimiento legendario).</summary>
        public event Action<Ore> Discovered;
        float discoveryT;

        public Island(int seed = 1)
        {
            rng = new Random(seed);
            // pueblo planificado: el deposito en la plaza; 8 parcelas en un anillo parejo (cada 45°, a 6.4 m, ~2.4 m
            // libres entre edificios); la primera ampliacion suma 4 en los huecos de afuera y la segunda 4 alineadas
            // detras de las del medio (sus caminos siguen derechos hacia la plaza)
            Plots.Add(new Plot { Id = 0, X = 0f, Z = 0f, Ring = 0 });
            float[] ang0 = { 135f, 45f, 225f, 315f, 90f, 180f, 0f, 270f };
            foreach (var a in ang0) AddPlotAt(a, RingR[0], 0);
            foreach (var a in new[] { 157.5f, 22.5f, 202.5f, 337.5f }) AddPlotAt(a, RingR[1], 1);
            foreach (var a in new[] { 90f, 180f, 0f, 270f }) AddPlotAt(a, RingR[2], 2);
            // ciudad (0.9): las 16 de arriba quedan en su lugar (indices guardados en la partida); se suman el resto del
            // anillo 1 (cada 45°, sin pisar la Maravilla a 67.5°), el anillo 2 completo (cada 22.5°) y un anillo 3
            // anillo 1 cada 45° desde 22.5° (sin el de 67.5°, donde esta la Maravilla); anillo 2 cada 45° desde 0°
            // (intercalado con el 1, asi no quedan en fila); anillo 3 con 16 parcelas desfasadas 11.25°
            foreach (var a in new[] { 112.5f, 247.5f, 292.5f }) AddPlotAt(a, RingR[1], 1);
            foreach (var a in new[] { 45f, 135f, 225f, 315f }) AddPlotAt(a, RingR[2], 2);
            for (int i = 0; i < 16; i++) AddPlotAt(11.25f + i * 22.5f, RingR[3], 3);
            Plots[0].Building = (int)BKind.Depot; Plots[0].Level = 1;
            Plots[1].Building = (int)BKind.House; Plots[1].Level = 1;
            BuildPaths();
            BuildDecorSlots();
            Coins = 0;
            SyncMiners(true);
            for (int i = 0; i < 5; i++) SpawnOre(true);
        }

        /// <summary>Radio de cada anillo de parcelas.</summary>
        public static readonly float[] RingR = { 8f, 13.25f, 17.75f, 23f };   // x1.25: mas espacio entre edificios

        void AddPlotAt(float deg, float r, int ring)
        {
            double a = deg * Math.PI / 180.0;
            Plots.Add(new Plot { Id = Plots.Count, X = (float)Math.Round(Math.Cos(a) * r, 3), Z = (float)Math.Round(Math.Sin(a) * r, 3), Ring = ring });
        }

        // ------------------------------------------------------------ consultas
        public static BDef Def(BKind k) { return Defs[(int)k]; }

        public Plot Find(BKind k)
        {
            foreach (var p in Plots) if (p.Building == (int)k) return p;
            return null;
        }

        public int Level(BKind k) { var p = Find(k); return p == null ? 0 : p.Level; }

        /// <summary>Tope de casas: 2 casas x nivel 6 = 12 mineros (rendimiento en telefono y pantalla legible).</summary>
        public const int MaxHouses = 2;

        public int CountOf(BKind k)
        {
            int n = 0;
            foreach (var p in Plots) if (p.Building == (int)k) n++;
            return n;
        }

        public int MinerCap()
        {
            int n = 0;
            foreach (var p in Plots) if (p.Building == (int)BKind.House) n += p.Level;
            return n + DormCount();   // cada dormitorio del Complejo aloja a su especialista
        }

        public int CarryCap() { return 1 + Math.Max(0, Math.Min(10, Level(BKind.Depot)) - 1) / 2; }
        public double PriceMult() { return (1.0 + 0.12 * Math.Max(0, Level(BKind.Depot) - 1)) * RainbowMult() * GlobalMult(); }
        public double PickPower() { return 1.0 + 0.6 * Level(BKind.Smithy); }

        public double UpgradeCost(Plot p)
        {
            if (p.Building == (int)BKind.Depot) return ThCost(p.Level + 1);
            var d = Defs[p.Building];
            return Math.Round(d.UpCost * Math.Pow(d.UpMult, p.Level - 1) * IslandValue());
        }

        /// <summary>Se puede mejorar sin mirar monedas ni materiales: no esta en obra, hay constructor y tope.</summary>
        public bool UpgradeAllowed(Plot p)
        {
            return p.Building >= 0 && p.Level >= 1 && p.Work <= 0 && p.Level < LevelCap((BKind)p.Building) && FreeBuilders() > 0;
        }

        public bool CanUpgrade(Plot p)
        {
            return UpgradeAllowed(p) && Coins >= UpgradeCost(p) && HasMats(MatsFor((BKind)p.Building, p.Level + 1)) && !BarracksCramped(p);
        }

        public double BuildCost(BKind k)
        {
            var d = Def(k);
            int have = 0;
            foreach (var p in Plots) if (p.Building == (int)k) have++;
            // la segunda casa es una inversion grande; la segunda mina o aserradero, no tanto
            return Math.Round(d.BuildCost * Math.Pow(k == BKind.House ? 12.0 : 3.0, have) * IslandValue());
        }

        /// <summary>Parcelas libres ofrecidas: solo las 2 primeras (la isla "crece" a medida que se usan).</summary>
        public bool Offered(Plot p)
        {
            if (p.Building >= 0 || TutHidesPlots) return false;
            int n = 0;
            if (p.Ring > Expand) return false;
            foreach (var q in Plots)
            {
                if (q.Building >= 0 || q.Ring > Expand || Covered(q)) continue;
                if (q == p) return n < 2;
                n++;
            }
            return false;
        }

        /// <summary>Se puede construir (sin mirar las monedas): parcela ofrecida, tipo permitido, topes.</summary>
        public bool Allowed(BKind k, Plot p)
        {
            if (p.Building >= 0 || !Offered(p)) return false;
            if (k == BKind.Depot) return false;
            if (CountOf(k) >= CountCap(k)) return false;   // uno por tipo (dos en la extraccion basica, mas adelante)
            if (!Unlocked(k) || FreeBuilders() <= 0) return false;
            return true;
        }

        public bool CanBuild(BKind k, Plot p)
        {
            return Allowed(k, p) && Coins >= BuildCost(k) && HasMats(MatsFor(k, 1));
        }

        /// <summary>Rendimiento de un minero (multiplica la velocidad de golpe y de caminata).</summary>
        public float Perf(Miner m)
        {
            float e = 0.55f + 0.45f * Clamp01(m.Energy / 60f);
            float c = m.Fresh > 0f ? 1.3f : (m.Clean < 30f ? 0.8f : 1f);
            return e * c * (TurboT > 0f ? 2f : 1f) * MoodMult();
        }

        // ------------------------------------------------------------ acciones del jugador
        /// <summary>Empieza la obra (nivel 0 mientras dura; al terminar pasa a nivel 1 y avisa WorkDone).</summary>
        public bool Build(BKind k, Plot p)
        {
            if (!CanBuild(k, p)) return false;
            Coins -= BuildCost(k);
            PayMats(MatsFor(k, 1));
            p.Building = (int)k;
            p.Level = 0;
            p.Queue.Clear(); p.Ready = 0; p.ReadyRes = -1; p.ProdT = 0;
            p.BuildT = 1.6f;
            StartWork(p, k, 1);
            AddStat("built", 1);
            CallBuilders(p);
            BuildingChanged?.Invoke(p);
            return true;
        }

        /// <summary>Empieza la mejora (el nivel sube cuando termina la obra; mientras tanto no produce).</summary>
        public bool Upgrade(Plot p)
        {
            if (!CanUpgrade(p)) return false;
            Coins -= UpgradeCost(p);
            PayMats(MatsFor((BKind)p.Building, p.Level + 1));
            p.BuildT = 1.2f;
            StartWork(p, (BKind)p.Building, p.Level + 1);
            AddStat("upgrades", 1);
            CallBuilders(p);
            BuildingChanged?.Invoke(p);
            return true;
        }

        // ------------------------------------------------------------ simulacion
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            dt = Math.Min(dt, 0.1f);
            foreach (var p in Plots) if (p.BuildT >= 0f) { p.BuildT -= dt; if (p.BuildT < 0f) p.BuildT = -1f; }
            foreach (var o in OreList) o.Age += dt;
            OreList.RemoveAll(o => o.Dead);
            spawnT -= dt;
            if (spawnT <= 0f)
            {
                spawnT = 2.2f;
                if (OreList.Count < MaxOres()) SpawnOre(false);
            }
            foreach (var m in Miners) TickMiner(m, dt);
            SeparateMiners(dt);
            TickFriends(dt);
            if (TurboT > 0f) TurboT = Math.Max(0f, TurboT - dt);
            if (FrenzyT > 0f) FrenzyT = Math.Max(0f, FrenzyT - dt);
            if (frenzyCd > 0f) frenzyCd -= dt;
            TickGiant(dt);
            TickShip(dt);
            TickMine(dt);
            if (TutDone)
            {
                // sorpresas, pedidos y metas recien despues de la primera partida (al principio, nada distrae)
                TickBalloon(dt);
                TickBottle(dt);
                TickCritter(dt);
                TickOrders(dt);
                TickBoss(dt);
            }
            TickWorld(dt);
            TickCity(dt);
            TickComplex(dt);
            TickTutorial();
            TickMissions();
            if (TutDone) CheckGoal();
        }

        int MaxOres() { return 5 + 2 * Miners.Count + 3 * Expand + 3 * CountMod(ModKind.Magnet); }

        /// <summary>Probabilidades de cada mineral: mejoran con lo ganado (la isla "madura").</summary>
        int RollKind()
        {
            double p = TotalEarned;
            double[] w =
            {
                10,
                p > 30 ? 6 : 1,
                p > 300 ? 4 : 0,
                p > 2000 ? 2.2 * RareMult() : 0,
                p > 12000 ? 0.8 * RareMult() : 0,
                HasRoom(3) || HasRoom(8) ? 3.5 : 0,                     // carbon (habitacion de carbon o maestro)
                HasRoom(5) || HasRoom(8) ? 1.2 * RareMult() : 0,        // cristal
                HasRoom(7) || HasRoom(8) ? 0.45 * RareMult() : 0,       // minerales raros
            };
            double sum = 0; foreach (var x in w) sum += x;
            double r = rng.NextDouble() * sum;
            for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r <= 0) return i; }
            return 0;
        }

        double RareMult() { return 1.0 + 0.35 * Level(BKind.Lighthouse); }

        public Ore SpawnOre(bool initial, int forceKind = -1, Action<Ore> setup = null)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                double a = rng.NextDouble() * Math.PI * 2;
                double d = Math.Sqrt(rng.NextDouble()) * (Radius - 1.2);
                float x = (float)(Math.Cos(a) * d), z = (float)(Math.Sin(a) * d);
                if (!FreeSpot(x, z, 1.1f)) continue;
                int k = forceKind >= 0 ? forceKind : RollKind();
                var o = new Ore { Id = nextOre++, Kind = k, X = x, Z = z, MaxHp = Ores[k].Hp, Hp = Ores[k].Hp, Age = initial ? 5f : 0f };
                setup?.Invoke(o);   // antes del aviso: la vista ya lo dibuja como cristal o meteorito
                OreList.Add(o);
                OreSpawned?.Invoke(o);
                return o;
            }
            return null;
        }

        /// <summary>Obstaculos de decoracion (arboles, arbustos): las vetas no nacen detras de un arbol.</summary>
        public readonly List<float[]> Blockers = new List<float[]>();
        public void AddBlocker(float x, float z, float r) { Blockers.Add(new[] { x, z, r }); }

        public bool FreeSpot(float x, float z, float margin)
        {
            foreach (var b in Blockers) if (Sq(x - b[0], z - b[1]) < (b[2] + margin * 0.6f) * (b[2] + margin * 0.6f)) return false;
            if (x * x + z * z > (Radius - 0.8f) * (Radius - 0.8f)) return false;
            if (OnPath(x, z, Math.Min(margin, 1.2f) * 0.55f)) return false;   // los caminos quedan limpios
            if (DecorBlocked(x, z, margin * 0.5f)) return false;
            foreach (var p in Plots)
            {
                if (p.Ring > Expand) continue;
                float r = (p.Building >= 0 ? RadiusOf(p) : (Offered(p) ? 1.2f : 0.7f)) + margin;
                if (Sq(x - p.X, z - p.Z) < r * r) return false;
            }
            foreach (var o in OreList)
            {
                float rr = o.Giant ? 2.6f : 1.3f;
                if (!o.Dead && Sq(x - o.X, z - o.Z) < rr * rr) return false;
            }
            return true;
        }

        // ------------------------------------------------------------ economia comun
        void Earn(double v)
        {
            Coins += v;
            TotalEarned += v;
        }

        public void AddStat(string k, long n)
        {
            long c;
            Stats.TryGetValue(k, out c);
            Stats[k] = c + n;
        }

        public long Stat(string k) { long c; return Stats.TryGetValue(k, out c) ? c : 0; }

        // ------------------------------------------------------------ veta gigante
        public Ore Giant
        {
            get { foreach (var o in OreList) if (o.Giant && !o.Dead) return o; return null; }
        }

        void TickGiant(float dt)
        {
            // descubrimiento de la ampliacion: un Yacimiento legendario y vetas ricas alrededor ("¿que habra detras?")
            if (discoveryT > 0f)
            {
                discoveryT -= dt;
                if (discoveryT <= 0f)
                {
                    if (Giant != null) { discoveryT = 2f; }
                    else
                    {
                        var g = SpawnGiant(false, false, true);
                        for (int i = 0; i < 3; i++) SpawnOre(false, i == 0 ? 4 : 3);
                        AddStat("discoveries", 1);
                        if (g != null) Discovered?.Invoke(g);
                    }
                }
            }
            if (Giant != null || TotalEarned < 120) return;
            giantT -= dt;
            if (giantT > 0f) return;
            giantT = (float)(140 + rng.NextDouble() * 100) / (float)(1.0 + 0.15 * Level(BKind.Lighthouse)) * (Weekend ? 0.75f : 1f);
            SpawnGiant(false, false, TotalEarned > 5000 && rng.NextDouble() < LegendaryChance * (1.0 + 0.1 * Level(BKind.Lighthouse)) * (Weekend ? 3.0 : 1.0));
        }

        /// <summary>Probabilidad de que una veta gigante sea un Yacimiento legendario (~1 de cada 14: raro a proposito).</summary>
        public const double LegendaryChance = 0.07;

        /// <summary>Fin de semana dorado (lo fija la vista con el calendario): legendarios x3 y vetas gigantes mas seguidas.</summary>
        public bool Weekend;

        public Ore SpawnGiant(bool sky = false, bool boss = false, bool legendary = false)
        {
            for (int tries = 0; tries < 120; tries++)
            {
                double a = rng.NextDouble() * Math.PI * 2, d = Math.Sqrt(rng.NextDouble()) * (Radius - 2.5);
                float x = (float)(Math.Cos(a) * d), z = (float)(Math.Sin(a) * d);
                if (!FreeSpot(x, z, tries < 30 ? 1.6f : 1.0f)) continue;
                double hp = Ores[3].Hp * (4 + Miners.Count) * PickPower();   // ~30-60 s con todos picando
                if (boss) hp *= 4 + BossLevel;   // el jefe aguanta mucho mas y sube con cada victoria
                legendary &= !boss;
                if (legendary) hp *= 1.6;
                var o = new Ore { Id = nextOre++, Kind = legendary ? 4 : 3, X = x, Z = z, MaxHp = hp, Hp = hp, Giant = true, Sky = sky, Boss = boss, BossPhase = 3, Legendary = legendary };
                OreList.Add(o);
                // todos los que estan libres o picando sueltan lo que hacen y corren
                foreach (var m in Miners)
                    if (m.State == MState.Idle || m.State == MState.ToOre || m.State == MState.Mining) { Release(m); Set(m, MState.Idle); }
                OreSpawned?.Invoke(o);
                GiantSpawned?.Invoke(o);
                return o;
            }
            return null;
        }

        double GiantValue() { return Ores[BestKind()].Value * PriceMult() * (1 + 0.1 * Miners.Count); }   // escala con el progreso

        void GiantHit(Ore o, Miner m)
        {
            if (o.Boss) BossHit(o);
            m.Energy = Math.Max(0f, m.Energy - 0.4f);
            // era 0.25: con 12 mineros son cientos de golpes y la veta gigante daba el 54 % de todas las monedas
            // (auditoria final, simulacion del Core); el toque del jugador sigue pagando 0.1 (premia al que juega)
            double v = GiantValue() * 0.06;
            Earn(v);
            GiantPaid?.Invoke(o, v);
            if (o.Hp > 0) return;
            o.Dead = true;
            AddStat("giants", 1);
            double bonus = GiantValue() * (o.Legendary ? 125 : 25);
            Earn(bonus);
            Gems += o.Legendary ? 10 : 2;
            GiveChest(o.Legendary ? 2 : 1);
            if (o.Legendary) AddStat("legendary", 1);
            AddXp(50);
            if (BarracksLevel >= 2 && rng.NextDouble() < 0.10) AddPlans(1);   // a veces trae un plano de Sala secreta
            MinerBroke(m, o);
            if (o.Boss) BossReward(o);
            GiantPaid?.Invoke(o, bonus);
            OreBroken?.Invoke(o, m);
            foreach (var mm in Miners) if (mm.Target == o.Id) { mm.Target = -1; Set(mm, MState.Idle); MinerMood?.Invoke(mm, "cheer"); }
        }

        // ------------------------------------------------------------ barco comprador
        int BestKind()
        {
            double p = TotalEarned;
            return p > 12000 ? 4 : p > 2000 ? 3 : p > 300 ? 2 : p > 30 ? 1 : 0;
        }

        void TickShip(float dt)
        {
            int dock = Level(BKind.Dock);
            if (CurShip != null)
            {
                CurShip.Age += dt;
                CurShip.Left -= dt;
                if (!CurShip.Done && CurShip.Delivered >= CurShip.Count)
                {
                    CurShip.Done = true;
                    double reward = Ores[CurShip.Kind].Value * CurShip.Count * 3.0 * PriceMult();
                    Earn(reward);
                    Gems += 1 + CurShip.Kind / 2;
                    AddStat("ships", 1);
                    ShipLeft?.Invoke(CurShip, true);
                    CurShip = null;
                    shipT = ShipEvery();
                }
                else if (CurShip.Left <= 0f)
                {
                    ShipLeft?.Invoke(CurShip, false);
                    CurShip = null;
                    shipT = ShipEvery();
                }
                return;
            }
            if (dock <= 0) return;
            shipT -= dt;
            if (shipT > 0f) return;
            int k = Math.Max(0, Math.Min(3, BestKind()) - rng.Next(2));
            int count = Math.Max(2, (int)Math.Round((9 - k * 1.6) * (0.6 + 0.08 * Math.Min(Miners.Count, 12))));
            CurShip = new Ship { Kind = k, Count = count, Left = 120f + 15f * dock };
            ShipArrived?.Invoke(CurShip);
        }

        /// <summary>Para pruebas y capturas: el proximo barco llega en el siguiente tick (si hay muelle).</summary>
        public void ShipSoon() { if (CurShip == null) shipT = 0f; }

        float ShipEvery() { return (float)(110 + rng.NextDouble() * 60) / (1f + 0.2f * (Level(BKind.Dock) - 1)); }

        // ------------------------------------------------------------ mina (ingreso pasivo)
        public double MineRate()
        {
            int l = Level(BKind.Mine);
            // la mina saca piedra (y algo de hierro): paga como esos minerales, no como las gemas
            return l <= 0 ? 0 : 0.8 * Math.Pow(l, 1.6) * Ores[Math.Max(1, Math.Min(2, BestKind()))].Value * 0.5 * GlobalMult();
        }

        void TickMine(float dt)
        {
            double r = MineRate();
            if (r <= 0) return;
            mineT += dt;
            if (mineT < 4f) return;
            double v = r * mineT;
            mineT = 0f;
            Earn(v);
            MinePaid?.Invoke(v);
        }

        // ------------------------------------------------------------ toques del jugador, turbo, expansion
        /// <summary>El ultimo toque fue critico (1 de cada ~12: golpe x3 y numero dorado).</summary>
        public bool LastCrit { get; private set; }
        /// <summary>El ultimo toque rompio la veta.</summary>
        public bool LastBroke { get; private set; }
        /// <summary>Frenesi: 5 s con pico doble y monedas en cada golpe (se gana con combo x30).</summary>
        public float FrenzyT;
        float frenzyCd;
        public const int FrenzyCombo = 30, CoinCombo = 20;
        public const float FrenzyTime = 5f, CritChance = 1f / 12f;
        public event Action FrenzyStarted;

        /// <summary>
        /// Golpe del jugador sobre una veta. Devuelve las monedas ganadas con este toque (al romperla, y desde combo
        /// x20 o en frenesi tambien una pizca en cada golpe), o -1 si no se pudo tocar.
        /// </summary>
        public double TapOre(Ore o, float combo)
        {
            LastCrit = false; LastBroke = false;
            if (o == null || o.Dead || o.Age < 0.5f) return -1;
            if (combo >= FrenzyCombo && FrenzyT <= 0f && frenzyCd <= 0f)
            {
                FrenzyT = FrenzyTime;
                frenzyCd = 25f;
                AddStat("frenzies", 1);
                FrenzyStarted?.Invoke();
            }
            LastCrit = rng.NextDouble() < CritChance * (SetDone(2) ? 1.5 : 1.0);
            double dmg = Math.Max(1.0, PickPower() * 0.6) * (1 + Math.Min(combo, 20) * 0.05);
            if (LastCrit) { dmg *= 3; AddStat("crits", 1); }
            if (FrenzyT > 0f) dmg *= 2;
            o.Hp -= dmg;
            AddStat("taps", 1);
            OreHit?.Invoke(o, null);
            // pizca de monedas por golpe con combo alto o en frenesi
            double pinch = (combo >= CoinCombo || FrenzyT > 0f) ? Math.Max(1, Math.Round(Ores[o.Kind].Value * PriceMult() * 0.25)) : 0;
            if (o.Giant)
            {
                double v = GiantValue() * (o.Legendary ? 0.2 : 0.1) * (LastCrit ? 3 : 1);
                Earn(v);
                GiantPaid?.Invoke(o, v);
                if (o.Boss) BossHit(o);
                if (o.Hp <= 0) { var any = Miners.Count > 0 ? Miners[0] : null; o.Hp = 0; GiantHit(o, any ?? new Miner()); }
                return 0;
            }
            if (o.Hp > 0) { if (pinch > 0) Earn(pinch); return pinch; }
            o.Dead = true;
            LastBroke = true;
            AddStat("tap_breaks", 1);
            AddRes(OreRes[o.Kind], Ores[o.Kind].Units);   // lo que rompe el jugador tambien suma al galpon
            Mined[o.Kind]++;
            AddStat("rocks", 1);
            AddStat("mined" + o.Kind, 1);
            double val = o.NightCrystal ? CrystalPrize(o) : Ores[o.Kind].Value * Ores[o.Kind].Units * PriceMult() * (FrenzyT > 0f ? 2 : 1) + pinch;
            Earn(val);
            if (o.Kind == 4 && !o.NightCrystal) Gems += 1;   // las gemas dan una gema de verdad
            if (o.NightCrystal) AddXp(10);
            if (o.Kind >= 1) RollPiece(PieceChance);
            OreBroken?.Invoke(o, null);
            foreach (var m in Miners) if (m.Target == o.Id) { m.Target = -1; Set(m, MState.Idle); }
            return val;
        }

        public const int TurboGems = 5;

        public bool BuyTurbo()
        {
            if (Gems < TurboGems) return false;
            Gems -= TurboGems;
            TurboT += 120f;
            return true;
        }

        public double ExpandPrice() { return Expand < ExpandCost.Length ? Math.Round(ExpandCost[Expand] * IslandValue()) : 0; }
        public bool ExpandAllowed() { return Expand < ExpandCost.Length && Th >= ExpandTh[Expand]; }
        public bool CanExpand() { return ExpandAllowed() && Coins >= ExpandPrice(); }

        public bool DoExpand()
        {
            if (!CanExpand()) return false;
            Coins -= ExpandPrice();
            Expand++;
            discoveryT = 3.5f;   // la tierra nueva esconde algo: aparece despues de que se va la niebla
            FixCoveredPlots();
            BuildDecorSlots();
            AddStat("expand", 1);
            Expanded?.Invoke(Expand);
            return true;
        }

        // ------------------------------------------------------------ metas encadenadas
        static IGoal G(string text, string stat, long target, double coins, int gems = 0)
        {
            return new IGoal { Text = text, Stat = stat, Target = target, Coins = coins, Gems = gems };
        }

        // metas de la ciudad (0.9): llevan de la mano por el Ayuntamiento y las primeras cadenas
        static readonly IGoal[] Fixed =
        {
            G(Loc.T("Mejorá la Casa a nivel 2"), "lv_house", 2, 15),
            G(Loc.T("Picá 8 rocas"), "rocks", 8, 25),
            G("Construí la Cantina", "b:Canteen", 1, 40, 2),
            G("Construí el Aserradero", "b:Sawmill", 1, 50),
            G(Loc.T("Cobrá 10 materiales de tus edificios"), "collected", 10, 60),
            G(Loc.T("Tené 3 mineros"), "miners", 3, 60),
            G(Loc.T("Subí el Ayuntamiento a nivel 2"), "th", 2, 100, 3),
            G("Construí el Galpón", "b:Barn", 1, 120),
            G("Construí la Mina de carbón", "b:CoalMine", 1, 150),
            G("Construí las Duchas", "b:Showers", 1, 150, 2),
            G(Loc.T("Subí el Ayuntamiento a nivel 3"), "th", 3, 300, 3),
            G("Construí la Fundición", "b:Foundry", 1, 400),
            G(Loc.T("Fabricá 5 lingotes de hierro"), "made9", 5, 500, 3),
            G(Loc.T("Expandí la isla"), "expand", 1, 500, 5),
            G("Construí la Herrería", "b:Smithy", 1, 600, 3),
            G(Loc.T("Romper una Veta Gigante"), "giants", 1, 300, 3),
            G("Construí el Muelle", "b:Dock", 1, 900),
            G(Loc.T("Subí el Ayuntamiento a nivel 4"), "th", 4, 1500, 5),
            G(Loc.T("Completá un pedido de barco"), "ships", 1, 900, 4),
            G("Construí el Faro", "b:Lighthouse", 1, 1500, 5),
            G(Loc.T("Fabricá 5 picos"), "made10", 5, 1800, 4),
            G(Loc.T("Subí el Ayuntamiento a nivel 5"), "th", 5, 4000, 6),
            G("Construí el Mercado", "b:Market", 1, 4000, 4),
            G(Loc.T("Picá 10 de oro"), "mined3", 10, 2500, 5),
        };

        public IGoal CurrentGoal()
        {
            if (GoalIdx < Fixed.Length) return Fixed[GoalIdx];
            int n = GoalIdx - Fixed.Length;
            // metas generadas: alternan picar rocas y completar barcos, cada vez mas grandes
            // metas generadas: el Ayuntamiento siguiente cada tres; en el medio, picar y fabricar cada vez mas
            if (n % 3 == 2 && Th < MaxTh)
                return new IGoal { Text = Loc.T("Subí el Ayuntamiento a nivel ") + (Th + 1), Stat = "th", Target = Th + 1, Coins = Math.Round(TotalEarned * 0.1 + 1000), Gems = 8 };
            if (n % 3 == 1)
                return new IGoal { Text = Loc.T("Fabricá ") + (10 + n * 3) + Loc.T(" productos"), Stat = "made_g", Target = 10 + n * 3, Coins = Math.Round(TotalEarned * 0.08 + 800), Gems = 4 };
            if (n % 2 == 0)
                return new IGoal { Text = Loc.T("Picá ") + (50 + n * 25) + Loc.T(" rocas"), Stat = "rocks_g", Target = 50 + n * 25, Coins = Math.Round(TotalEarned * 0.08 + 500), Gems = 3 };
            return new IGoal { Text = Loc.T("Romper ") + (1 + n / 2) + Loc.T(" Vetas Gigantes"), Stat = "giants_g", Target = 1 + n / 2, Coins = Math.Round(TotalEarned * 0.1 + 800), Gems = 5 };
        }

        public long GoalProgress(IGoal g)
        {
            if (g.Stat.StartsWith("b:"))
            {
                BKind k;
                if (!Enum.TryParse(g.Stat.Substring(2), out k)) return 0;
                // Plan Pueblo: las Duchas y el Comedor tambien valen como habitacion del Cuartel
                if (k == BKind.Showers && CountMod(ModKind.Bath) > 0) return 1;
                if (k == BKind.Canteen && CountMod(ModKind.Mess) > 0) return 1;
                foreach (var p in Plots) if (p.Building == (int)k && p.Level >= 1) return 1;
                return 0;
            }
            switch (g.Stat)
            {
                case "th": return Th;
                case "made_g": return Stat("made") - Stat("goal_base_made");
                case "lv_house": return Level(BKind.House);
                case "lv_depot": return Level(BKind.Depot);
                case "miners": return Miners.Count;
                case "b_canteen": return Find(BKind.Canteen) != null ? 1 : 0;
                case "b_showers": return Find(BKind.Showers) != null ? 1 : 0;
                case "b_smithy": return Find(BKind.Smithy) != null ? 1 : 0;
                case "b_mine": return Find(BKind.Mine) != null ? 1 : 0;
                case "b_dock": return Find(BKind.Dock) != null ? 1 : 0;
                case "b_lighthouse": return Find(BKind.Lighthouse) != null ? 1 : 0;
                case "rocks_g": return Stat("rocks") - Stat("goal_base_rocks");
                case "giants_g": return Stat("giants") - Stat("goal_base_giants");
                default: return Stat(g.Stat);
            }
        }

        void CheckGoal()
        {
            var g = CurrentGoal();
            if (GoalProgress(g) < g.Target) return;
            Earn(g.Coins * IslandValue());
            Gems += g.Gems;
            FeedPiggy(2 + g.Gems / 2);   // la alcancia junta gemas jugando
            GoalIdx++;
            // las metas generadas cuentan desde ahora
            Stats["goal_base_rocks"] = Stat("rocks");
            Stats["goal_base_giants"] = Stat("giants");
            Stats["goal_base_made"] = Stat("made");
            GoalDone?.Invoke(g);
        }

        // ------------------------------------------------------------ ganancias sin conexion
        /// <summary>Monedas por estar afuera `seconds` (tope 2 h): ritmo estimado de los mineros + mina, al 50 %.</summary>
        public double OfflineEarnings(double seconds)
        {
            seconds = Math.Max(0, Math.Min(seconds, OfflineCap()));
            if (seconds < 60) return 0;
            double perMiner = Ores[Math.Max(0, BestKind() - 1)].Value * CarryCap() * PriceMult() / 14.0 * ComplexOfflineMult();
            return Math.Round((perMiner * Miners.Count + MineRate()) * seconds * 0.5 * (Capataz ? 2 : 1));
        }

        /// <summary>Tope de horas de ganancia sin conexion: 2 h + 1 h por nivel del Banco.</summary>
        public double OfflineCap() { return 7200 + 3600 * Level(BKind.Bank); }

        public double ApplyOffline(double now)
        {
            double v = LastSeen > 0 ? OfflineEarnings(now - LastSeen) : 0;
            // el dia siguio corriendo mientras no estaba
            if (LastSeen > 0 && now > LastSeen) DayClock = (float)((DayClock + (now - LastSeen)) % DayLength);
            if (LastSeen > 0 && now > LastSeen) AdvanceCity(now - LastSeen);   // obras y produccion en tiempo real
            if (LastSeen > 0 && now > LastSeen) ComplexOffline(Math.Min(now - LastSeen, 86400 * 3));
            if (v > 0) Earn(v);
            LastSeen = now;
            return v;
        }

        // ------------------------------------------------------------ IA de los mineros
        void TickMiner(Miner m, float dt)
        {
            m.T += dt;
            if (m.Fresh > 0f) m.Fresh = Math.Max(0f, m.Fresh - dt);
            m.Moving = false;
            float perf = Perf(m);
            switch (m.State)
            {
                case MState.Spawning:
                    if (m.T > 1.2f + m.Hold) { m.Hold = 0f; Set(m, MState.Idle); }
                    break;
                case MState.Idle:
                    Decide(m);
                    break;
                case MState.ToOre:
                {
                    var o = OreById(m.Target);
                    if (o == null || o.Dead) { Release(m); Set(m, MState.Idle); break; }
                    float reach = o.Giant ? 1.7f + (m.Id % 3) * 0.25f : ReachDist + Ores[o.Kind].Size * 0.5f;
                    if (Walk(m, o.X, o.Z, reach, dt, perf * WalkMult(m))) { Set(m, MState.Mining); m.HitT = 0f; }
                    break;
                }
                case MState.Mining:
                {
                    var o = OreById(m.Target);
                    if (o == null || o.Dead) { Release(m); Set(m, MState.Idle); break; }
                    m.Face = (float)Math.Atan2(o.X - m.X, o.Z - m.Z);
                    m.HitT += dt * perf;
                    if (m.HitT >= HitInterval)
                    {
                        m.HitT -= HitInterval;
                        m.Hits++;
                        o.Hp -= PickPower() * HitMult(m) * SpecBonus(m, o, IsNight);   // el especialista pica su mineral mucho mas rapido
                        m.Energy = Math.Max(0f, m.Energy - 1.1f * (Char(m).Trait == Trait.Glutton ? 2f : 1f));
                        m.Clean = Math.Max(0f, m.Clean - 0.9f);
                        OreHit?.Invoke(o, m);
                        if (o.Giant) { GiantHit(o, m); break; }
                        if (o.Hp <= 0)
                        {
                            o.Dead = true;
                            Mined[o.Kind]++;
                            AddStat("rocks", 1);
                            AddStat("mined" + o.Kind, 1);
                            m.CarryKind = o.Kind;
                            m.CarryUnits = Ores[o.Kind].Units * CarryCap();
                            m.Target = -1;
                            MinerBroke(m, o);
                            if (o.Kind >= 1) RollPiece(PieceChance);
                            OreBroken?.Invoke(o, m);
                            GoDeposit(m);
                        }
                    }
                    break;
                }
                case MState.ToDepot:
                {
                    var dep = Plots[m.Target];
                    if (Walk(m, dep.X, dep.Z, Defs[(int)BKind.Depot].Radius + 0.5f, dt, perf * 0.9f * WalkMult(m)))
                    {
                        double v = Ores[m.CarryKind].Value * m.CarryUnits * PriceMult();
                        Earn(v);
                        AddStat("sold", m.CarryUnits);
                        AddRes(OreRes[m.CarryKind], m.CarryUnits);   // ademas de venderlo, suma materia prima a la ciudad
                        FillOrders(m.CarryKind, m.CarryUnits);
                        if (CurShip != null && !CurShip.Done && CurShip.Kind == m.CarryKind)
                            CurShip.Delivered = Math.Min(CurShip.Count, CurShip.Delivered + m.CarryUnits);
                        m.CarryKind = -1; m.CarryUnits = 0;
                        Deposited?.Invoke(m, v);
                        Set(m, MState.Idle);
                    }
                    break;
                }
                case MState.ToCanteen:
                case MState.ToShowers:
                {
                    var p = Plots[m.Target];
                    if (p.Building < 0) { Set(m, MState.Idle); break; }
                    if (Walk(m, p.X, p.Z, Defs[p.Building].Radius + 0.4f, dt, perf * WalkMult(m)))
                        Set(m, m.State == MState.ToCanteen ? MState.Eating : MState.Showering);
                    break;
                }
                case MState.Eating:
                {
                    float rate = (22f + 8f * Level(BKind.Canteen)) * (Level(BKind.Hospital) > 0 ? 1.5f : 1f);
                    m.Energy = Math.Min(100f, m.Energy + rate * dt);
                    if (m.Energy >= 100f) { MinerMood?.Invoke(m, "fed"); Set(m, MState.Idle); }
                    break;
                }
                case MState.Showering:
                {
                    m.Clean = Math.Min(100f, m.Clean + 40f * dt);
                    if (m.Clean >= 100f)
                    {
                        m.Fresh = 45f + 15f * Level(BKind.Showers);
                        MinerMood?.Invoke(m, "fresh");
                        Set(m, MState.Idle);
                    }
                    break;
                }
                case MState.Resting:
                    m.Energy = Math.Min(100f, m.Energy + 7f * dt * (Level(BKind.Hospital) > 0 ? 2f : 1f));   // el hospital duplica el descanso
                    if (m.Energy >= 60f) Set(m, MState.Idle);
                    break;
                case MState.ToDig:
                case MState.Digging:
                    TickDig(m, dt, perf);
                    break;
                case MState.ToUnload:
                case MState.ToDorm:
                case MState.InDorm:
                case MState.ToFix:
                case MState.Fixing:
                case MState.Leaving:
                case MState.ToRoom:
                case MState.InRoom:
                    TickComplexMiner(m, dt, perf);
                    break;
                case MState.Queued:
                    TickQueued(m, dt, perf);
                    break;
                case MState.ToBuild:
                case MState.Building:
                {
                    // ayudan en la obra: corren hasta la parcela y martillan hasta que termina
                    var bp = Plots[m.Target];
                    if (bp.BuildT < 0f) { Set(m, MState.Idle); MinerMood?.Invoke(m, "cheer"); break; }
                    if (m.State == MState.ToBuild)
                    {
                        if (Walk(m, m.TX, m.TZ, 0.25f, dt, perf * WalkMult(m) * 1.3f)) { Set(m, MState.Building); m.HitT = 0f; }
                    }
                    else { m.Face = (float)Math.Atan2(bp.X - m.X, bp.Z - m.Z); m.HitT += dt; }
                    break;
                }
            }
        }

        /// <summary>Elige que hacer: necesidades primero, despues la mejor veta libre.</summary>
        void Decide(Miner m)
        {
            if (m.CarryKind >= 0)
            {
                GoDeposit(m);
                return;
            }
            if (m.Energy < 25f && GoRest(m)) return;
            if (m.Energy < 25f && GoRoom(m, ModKind.Mess)) return;   // el Comedor del Cuartel antes que la Cantina suelta
            if (m.Energy < 25f)
            {
                var c = Find(BKind.Canteen);
                if (c != null) { GoTo(m, MState.ToCanteen, c.X, c.Z, c.Id); return; }   // si esta en obra, llega cuando termina
                MinerMood?.Invoke(m, "tired");
                Set(m, MState.Resting);
                return;
            }
            if (m.Clean < 30f && GoRoom(m, ModKind.Bath)) return;   // las Duchas del Cuartel antes que el edificio suelto
            var sh = Find(BKind.Showers);
            if (m.Clean < 30f && sh != null) { GoTo(m, MState.ToShowers, sh.X, sh.Z, sh.Id); return; }
            // IA de reparto (pedido del creador: "todos van a la misma roca, no se distribuyen, es un caos"):
            // - una roca, un minero (la tomada por otro no se elige mientras haya otra libre);
            // - la veta gigante atrae como mucho a 6 (el resto sigue con lo suyo);
            // - cada minero tiene su zona de la isla (angulo segun su id) y prefiere lo que esta cerca de ella;
            // - el valor pesa menos que la distancia (raiz): nadie cruza la isla entera por una gema.
            Ore best = null; double bestScore = double.MinValue;
            int onGiant = 0;
            var gOre = Giant;
            if (gOre != null) foreach (var o2 in Miners) if (o2 != m && o2.Target == gOre.Id) onGiant++;
            double za = ZoneAngle(m), zr = Math.Max(3.0, Radius * 0.55);
            float zx = (float)(Math.Cos(za) * zr), zz = (float)(Math.Sin(za) * zr);
            for (int pass = 0; pass < 2 && best == null; pass++)
                foreach (var o in OreList)
                {
                    if (o.Dead || o.Age < 0.6f || o.NightCrystal) continue;
                    if (o.Giant) { if (onGiant < 6) { best = o; break; } continue; }
                    if (pass == 0 && o.ClaimedBy >= 0 && o.ClaimedBy != m.Id && ClaimAlive(o)) continue;
                    if (pass == 1 && Heading(o, m) >= 2) continue;   // como mucho 2 por veta: si no hay, se espera en su zona
                    double d = Math.Sqrt(Sq(o.X - m.X, o.Z - m.Z));
                    double dz = Math.Sqrt(Sq(o.X - zx, o.Z - zz));
                    double score = Math.Sqrt(Ores[o.Kind].Value) / (d + 2.0 + 0.6 * dz);
                    if (SpecBonus(m, o, IsNight) > 1.6f) score *= 3.0;   // cada especialista busca primero su mineral
                    if (score > bestScore) { bestScore = score; best = o; }
                }
            if (best == null)
            {
                // nada libre para picar: pasea tranquilo por SU zona (antes: alrededor de la casa, todos juntos)
                if (m.T > 2.5f)
                {
                    double a = rng.NextDouble() * Math.PI * 2;
                    m.TX = zx + (float)Math.Cos(a) * 1.6f; m.TZ = zz + (float)Math.Sin(a) * 1.6f;
                    if (!FreeSpot(m.TX, m.TZ, 0.6f)) { m.TX = m.X; m.TZ = m.Z; }
                    m.T = 0f;
                }
                Walk(m, m.TX, m.TZ, 0.3f, 0.016f, 0.35f);
                return;
            }
            if (!best.Giant) best.ClaimedBy = m.Id;
            m.Target = best.Id;
            Set(m, MState.ToOre);
        }

        /// <summary>Zona de cada minero: angulos repartidos parejo segun su lugar en la lista (no se amontonan).</summary>
        double ZoneAngle(Miner m)
        {
            int i = Miners.IndexOf(m), n = Math.Max(1, Miners.Count);
            return (i + 0.5) / n * Math.PI * 2.0 + 0.4;
        }

        /// <summary>Cuantos mineros (sin contar a `m`) van o pican esta veta.</summary>
        int Heading(Ore o, Miner m)
        {
            int n = 0;
            foreach (var x in Miners) if (x != m && x.Target == o.Id && (x.State == MState.ToOre || x.State == MState.Mining)) n++;
            return n;
        }

        /// <summary>El minero que tomo la veta sigue yendo hacia ella (si no, la toma queda libre).</summary>
        bool ClaimAlive(Ore o)
        {
            foreach (var x in Miners) if (x.Id == o.ClaimedBy) return x.Target == o.Id;
            return false;
        }

        /// <summary>
        /// Nadie se encima: los mineros que caminan o esperan se separan suave (40 cm). Los que pican la misma veta
        /// gigante se acomodan alrededor en vez de apilarse.
        /// </summary>
        void SeparateMiners(float dt)
        {
            const float min = 0.55f;
            for (int i = 0; i < Miners.Count; i++)
            {
                var a = Miners[i];
                if (a.InComplex || a.State == MState.Spawning) continue;
                for (int j = i + 1; j < Miners.Count; j++)
                {
                    var b = Miners[j];
                    if (b.InComplex || b.State == MState.Spawning) continue;
                    float dx = b.X - a.X, dz = b.Z - a.Z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 >= min * min) continue;
                    float d = (float)Math.Sqrt(d2);
                    if (d < 1e-3f) { dx = (a.Id % 2 == 0 ? 1f : -1f) * 0.01f; dz = 0.005f; d = 0.011f; }
                    float push = (min - d) * 0.5f * Math.Min(1f, dt * 8f);
                    float ux = dx / d, uz = dz / d;
                    a.X -= ux * push; a.Z -= uz * push;
                    b.X += ux * push; b.Z += uz * push;
                }
            }
        }

        /// <summary>Los dos mineros libres mas cercanos corren a ayudar en la obra.</summary>
        void CallBuilders(Plot p)
        {
            for (int k = 0; k < 2; k++)
            {
                Miner best = null; float bd = float.MaxValue;
                foreach (var m in Miners)
                {
                    if (m.CarryKind >= 0 || !(m.State == MState.Idle || m.State == MState.ToOre || m.State == MState.Mining)) continue;
                    float d = Sq(m.X - p.X, m.Z - p.Z);
                    if (d < bd) { bd = d; best = m; }
                }
                if (best == null) return;
                Release(best);
                float r = (p.Building >= 0 ? RadiusOf(p) : 1.4f) + 0.45f;
                double a = Math.Atan2(best.Z - p.Z, best.X - p.X) + (k == 0 ? -0.5 : 0.5);
                GoTo(best, MState.ToBuild, p.X + (float)Math.Cos(a) * r, p.Z + (float)Math.Sin(a) * r, p.Id);
            }
        }

        void GoTo(Miner m, MState st, float x, float z, int target)
        {
            m.Target = target; m.TX = x; m.TZ = z;
            Set(m, st);
        }

        void Release(Miner m)
        {
            var o = OreById(m.Target);
            if (o != null && o.ClaimedBy == m.Id) o.ClaimedBy = -1;
            m.Target = -1;
        }

        static void Set(Miner m, MState s) { m.State = s; m.T = 0f; }

        /// <summary>Camina hacia (x,z) esquivando edificios; true al llegar a menos de stop metros.</summary>
        bool Walk(Miner m, float x, float z, float stop, float dt, float perf)
        {
            float dx = x - m.X, dz = z - m.Z;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d <= stop) return true;
            float vx = dx / d, vz = dz / d;
            // esquivar huellas de edificios que no son el destino
            foreach (var p in Plots)
            {
                if (p.Building < 0) continue;
                if (p.Building == (int)BKind.Barracks && InComplexState(m.State)) continue;   // entra al Complejo
                float r = RadiusOf(p) + 0.35f;
                float px = m.X - p.X, pz = m.Z - p.Z;
                float pd = (float)Math.Sqrt(px * px + pz * pz);
                bool isDest = Sq(p.X - x, p.Z - z) < 0.01f;
                if (isDest || pd > r + 1.2f || pd < 1e-3f) continue;
                float push = Clamp01((r + 1.2f - pd) / 1.2f);
                // empuje hacia afuera + giro tangencial para rodear
                float tx = -pz / pd, tz = px / pd;
                if (tx * vx + tz * vz < 0f) { tx = -tx; tz = -tz; }
                vx += (px / pd) * push * 1.2f + tx * push;
                vz += (pz / pd) * push * 1.2f + tz * push;
            }
            float vl = (float)Math.Sqrt(vx * vx + vz * vz);
            if (vl < 1e-4f) return false;
            vx /= vl; vz /= vl;
            float step = Math.Min(d, WalkSpeed * perf * dt);
            m.X += vx * step; m.Z += vz * step;
            // no salir de la isla
            float r2 = m.X * m.X + m.Z * m.Z, lim = Radius - 0.4f;
            if (r2 > lim * lim) { float k = lim / (float)Math.Sqrt(r2); m.X *= k; m.Z *= k; }
            m.Face = (float)Math.Atan2(vx, vz);
            m.Moving = true;
            return false;
        }

        Ore OreById(int id)
        {
            foreach (var o in OreList) if (o.Id == id) return o;
            return null;
        }

        static float Sq(float a, float b) { return a * a + b * b; }
        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        // ------------------------------------------------------------ guardado
        public string ToJson()
        {
            var plots = new List<object>();
            foreach (var p in Plots)
            {
                var pd = new Dictionary<string, object> { { "b", p.Building }, { "l", p.Level } };
                if (p.Moved) { pd["x"] = p.X; pd["z"] = p.Z; }   // solo las que el jugador acomodo
                plots.Add(pd);
            }
            var mined = new List<object>();
            foreach (var x in Mined) mined.Add(x);
            return Json.Stringify(new Dictionary<string, object>
            {
                { "v", 5 }, { "cx", ComplexObj() }, { "coins", Coins }, { "earned", TotalEarned }, { "plots", plots }, { "mined", mined },
                { "gems", Gems }, { "expand", Expand }, { "goal", GoalIdx }, { "seen", LastSeen }, { "stats", StatsObj() },
                { "spins", Spins }, { "chests", ChestsObj() }, { "day", DayClock },
                { "miners", MinersObj() }, { "found", FoundObj() }, { "daily", DailyObj() }, { "prog", ProgressObj() },
                { "city", CityObj() }, { "tut", (int)Tut }, { "shop", ShopObj() },
            });
        }

        List<object> ChestsObj()
        {
            var l = new List<object>();
            for (int i = 0; i < Chests.Length; i++) l.Add(Chests[i] + (i == OpenTier ? 1 : 0));   // el que se estaba abriendo vuelve
            return l;
        }

        List<object> MinersObj()
        {
            var l = new List<object>();
            foreach (var m in Miners)
                l.Add(new Dictionary<string, object> { { "c", m.Char }, { "g", m.Golden ? 1 : 0 }, { "l", m.Level }, { "x", m.Xp }, { "f", m.Friend } });
            return l;
        }

        List<object> FoundObj()
        {
            var l = new List<object>();
            for (int i = 0; i < Found.Length; i++) l.Add((Found[i] ? 1 : 0) + (FoundGolden[i] ? 2 : 0));
            return l;
        }

        Dictionary<string, object> StatsObj()
        {
            var d = new Dictionary<string, object>();
            foreach (var kv in Stats) d[kv.Key] = kv.Value;
            return d;
        }

        public bool LoadJson(string text)
        {
            object parsed;
            Dictionary<string, object> d;
            if (string.IsNullOrEmpty(text) || !Json.TryParse(text, out parsed) || (d = parsed as Dictionary<string, object>) == null) return false;
            Coins = JsonRead.Dbl(d, "coins", 0);
            int saveV = JsonRead.Int(d, "v", 1);
            Gems = JsonRead.Int(d, "gems", 0);
            Expand = Math.Max(0, Math.Min(Radii.Length - 1, JsonRead.Int(d, "expand", 0)));
            GoalIdx = JsonRead.Int(d, "goal", 0);
            LastSeen = JsonRead.Dbl(d, "seen", 0);
            Spins = JsonRead.Int(d, "spins", 0);
            DayClock = (float)JsonRead.Dbl(d, "day", DayLength * 0.06f) % DayLength;
            object dl;
            if (d.TryGetValue("daily", out dl) && dl is Dictionary<string, object> dd) LoadDaily(dd);
            object pg;
            if (d.TryGetValue("prog", out pg) && pg is Dictionary<string, object> prd) LoadProgress(prd);
            BuildDecorSlots();
            object ch;
            if (d.TryGetValue("chests", out ch) && ch is List<object> cl)
                for (int i = 0; i < cl.Count && i < Chests.Length; i++) Chests[i] = (int)JsonRead.ToDouble(cl[i], 0);
            object so;
            if (d.TryGetValue("stats", out so) && so is Dictionary<string, object> sd)
                foreach (var kv in sd) Stats[kv.Key] = (long)JsonRead.ToDouble(kv.Value, 0);
            TotalEarned = JsonRead.Dbl(d, "earned", 0);
            object pl;
            if (d.TryGetValue("plots", out pl) && pl is List<object> list)
            {
                for (int i = 0; i < list.Count && i < Plots.Count; i++)
                {
                    var pd = list[i] as Dictionary<string, object>;
                    if (pd == null) continue;
                    Plots[i].Building = JsonRead.Int(pd, "b", -1);
                    Plots[i].Level = JsonRead.Int(pd, "l", 0);
                    if (pd.ContainsKey("x") && pd.ContainsKey("z"))
                    {
                        Plots[i].X = (float)JsonRead.Dbl(pd, "x", Plots[i].X);
                        Plots[i].Z = (float)JsonRead.Dbl(pd, "z", Plots[i].Z);
                        Plots[i].Moved = true;
                    }
                }
                BuildPaths();
            }
            object mi;
            if (d.TryGetValue("mined", out mi) && mi is List<object> ml)
                for (int i = 0; i < ml.Count && i < Mined.Length; i++) Mined[i] = (long)JsonRead.ToDouble(ml[i], 0);
            Plots[0].Building = (int)BKind.Depot;
            if (Plots[0].Level < 1) Plots[0].Level = 1;
            // Complejo: despues de las parcelas (el nivel del Cuartel define las celdas) y antes de los mineros
            ResetComplex();
            object cxo, ro;
            if (d.TryGetValue("cx", out cxo) && cxo is Dictionary<string, object> cxd) LoadComplex(cxd);
            else if (d.TryGetValue("rooms", out ro) && ro is List<object> rl) MigrateRooms(rl);   // partidas 0.10
            EnsureCentral();
            Tut = (TutStep)Math.Max(0, Math.Min((int)TutStep.Done, JsonRead.Int(d, "tut", (int)TutStep.Done)));
            object sh;
            if (d.TryGetValue("shop", out sh) && sh is Dictionary<string, object> shd) LoadShop(shd);
            object co;
            if (d.TryGetValue("city", out co) && co is Dictionary<string, object> cd) LoadCity(cd);
            else MigrateTo09();
            foreach (var p in Plots) if (p.Building >= 0 && p.Level < 1 && p.Work <= 0) p.Level = 1;   // obra perdida: queda hecha
            Miners.Clear();
            nextMiner = 1;
            loadingSave = true;
            SyncMiners(true);
            loadingSave = false;
            // identidad de cada minero (personaje, dorado, nivel, amistad)
            object mo;
            if (d.TryGetValue("miners", out mo) && mo is List<object> mlist)
                for (int i = 0; i < mlist.Count && i < Miners.Count; i++)
                {
                    var md = mlist[i] as Dictionary<string, object>;
                    if (md == null) continue;
                    var m = Miners[i];
                    // partidas de antes de 0.10 (16 personajes): todos pasan a Mineros de Piedra
                    m.Char = saveV < 4 ? 0 : Math.Max(0, Math.Min(Roster.Length - 1, JsonRead.Int(md, "c", 0)));
                    m.Golden = JsonRead.Int(md, "g", 0) == 1;
                    m.Level = Math.Max(1, JsonRead.Int(md, "l", 1));
                    m.Xp = JsonRead.Int(md, "x", 0);
                    m.Friend = JsonRead.Int(md, "f", 0);
                }
            for (int i = 0; i < Found.Length; i++) { Found[i] = false; FoundGolden[i] = false; }
            object fo;
            if (d.TryGetValue("found", out fo) && fo is List<object> fl)
                for (int i = 0; i < fl.Count && i < Found.Length; i++) { int v = (int)JsonRead.ToDouble(fl[i], 0); Found[i] = (v & 1) != 0; FoundGolden[i] = (v & 2) != 0; }
            foreach (var m in Miners) { Found[m.Char] = true; if (m.Golden) FoundGolden[m.Char] = true; }
            return true;
        }
    }
}
