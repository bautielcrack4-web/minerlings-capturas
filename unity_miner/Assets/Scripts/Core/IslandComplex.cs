using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Tipos de modulo del Complejo minero (el Cuartel como LEGO).</summary>
    public enum ModKind
    {
        Central = 0, Dorm = 1, Corridor = 2, Unload = 3, Storage = 4, Crusher = 5, Smelter = 6, Polisher = 7,
        Treasury = 8, Vault = 9, Generator = 10, Lab = 11, Drill = 12, Tools = 13, Mess = 14, Classroom = 15,
        Stairs = 16, Observatory = 17, Dovecote = 18, Windmill = 19, Splitter = 20, Secret = 21, Experimental = 22,
        Trophy = 23, Magnet = 24, Clock = 25, PortLab = 26, PortOven = 27, PartyBell = 28,
        Bath = 29,   // Plan Pueblo: las duchas como habitacion del Cuartel (no edificio suelto)
    }

    /// <summary>
    /// Definicion de un modulo. Los conectores se dan en la orientacion 0 como mascara de lados
    /// (bit 0 = N (+Z), 1 = E (+X), 2 = S (-Z), 3 = O (-X)). Puertas en todos los lados salvo que se diga otra cosa.
    /// </summary>
    public sealed class ModDef
    {
        public ModKind Kind;
        public string Name, Desc, Hex;
        public int Level;            // nivel del Cuartel que lo habilita
        public int RailIn, RailOut;  // lados con riel que entra / sale
        public int Cable;            // lados con cable
        public int Doors = 0xF;      // lados con puerta
        public int Floors = 1;       // pisos permitidos (bit 0 planta, 1 primer piso, 2 torre)
        public bool Interior = true; // puede ir en el nucleo 3x3
        public bool Exterior;        // puede ir en el patio (anillo exterior)
        public bool NeedsPower, Buyable = true, Portable;
        public double Cost;
        public int Max = 3;          // cuantos de este tipo
    }

    /// <summary>Modulo colocado en la grilla del Complejo (coordenadas locales del Cuartel).</summary>
    public sealed class Module
    {
        public int Id;
        public ModKind Kind;
        public int Ch = -1;          // especialista (dormitorios)
        public int X, Z, F, Rot;     // celda, piso y giro (0..3, horario visto desde arriba)
        public int Stage = 1;        // etapa fisica 1..3
        public double Expires;       // portatiles: vence en este instante del reloj del Complejo (0 = permanente)
        public double Work, WorkTotal; // obra (construccion de la Sala secreta o subida de etapa)
        public double TunedT;        // segundos de "afinada" despues de arreglarla
        public bool Broken;
        public int Deco;             // adorno (0 = ninguno)
        public int Splits;           // reparto alternado del Patio de vagonetas
        public int Count;            // contador (Bovedas: unidades guardadas; Experimental: vagonetas)
    }

    /// <summary>
    /// Complejo minero (0.11+): el Cuartel deja de tener 8 encastres fijos y pasa a ser una grilla donde el jugador arma
    /// su complejo con modulos que se imantan, giran solos, conectan puertas, rieles y cables, crecen en 3 etapas y
    /// suben de piso. Todas las reglas viven aca (la vista solo pregunta). Celda = 2 m; marco girado SlotYaw.
    /// </summary>
    public sealed partial class Island
    {
        public const float SlotYaw = 35f, Cell = 2.0f, FloorH = 1.55f;
        public static readonly int[] SideDX = { 0, 1, 0, -1 }, SideDZ = { 1, 0, -1, 0 };

        public static readonly ModDef[] ModDefs = BuildModDefs();

        static ModDef M(ModKind k, string name, string desc, string hex, int level, double cost)
        {
            return new ModDef { Kind = k, Name = name, Desc = desc, Hex = hex, Level = level, Cost = cost };
        }

        static ModDef[] BuildModDefs()
        {
            const int N = 1, E = 2, S = 4, W = 8;
            var l = new ModDef[30];
            l[0] = M(ModKind.Central, Loc.T("Sala central"), Loc.T("El corazón del complejo"), "d9533c", 1, 0); l[0].Max = 1; l[0].Buyable = false; l[0].Floors = 1;
            l[1] = M(ModKind.Dorm, Loc.T("Dormitorio"), Loc.T("Trae a su especialista"), "e0884a", 1, 500); l[1].Floors = 1 | 2; l[1].Max = 99;
            l[2] = M(ModKind.Corridor, Loc.T("Pasillo"), Loc.T("Conecta habitaciones"), "c9b48a", 2, 150); l[2].Floors = 1 | 2; l[2].Max = 8; l[2].Cable = 0xF;
            l[3] = M(ModKind.Unload, Loc.T("Descarga"), Loc.T("Los mineros descargan acá"), "8e9aa8", 2, 400); l[3].RailOut = N; l[3].Exterior = true; l[3].Max = 2;
            l[4] = M(ModKind.Storage, Loc.T("Almacén"), Loc.T("Junta y suma valor"), "a8703f", 3, 800); l[4].RailIn = S; l[4].RailOut = N;
            l[5] = M(ModKind.Crusher, Loc.T("Trituradora"), Loc.T("Más piedra, cobre y hierro"), "7d8590", 3, 900); l[5].RailIn = S; l[5].RailOut = N;
            l[6] = M(ModKind.Smelter, Loc.T("Fundición"), Loc.T("Mineral a lingote"), "e05a2e", 4, 1500); l[6].RailIn = S; l[6].RailOut = N;
            l[7] = M(ModKind.Polisher, Loc.T("Pulidora"), Loc.T("Gemas y cristales valen más"), "36c6d9", 6, 6000); l[7].RailIn = S; l[7].RailOut = N; l[7].Cable = E | W; l[7].NeedsPower = true;
            l[8] = M(ModKind.Treasury, Loc.T("Tesorería"), Loc.T("Vende la vagoneta"), "f2c230", 2, 700); l[8].RailIn = S; l[8].Max = 2;
            l[9] = M(ModKind.Vault, Loc.T("Bóveda"), Loc.T("Guarda gemas: da diamantes"), "3fa3ff", 6, 8000); l[9].RailIn = S; l[9].Doors = S; l[9].Max = 1;
            l[10] = M(ModKind.Generator, Loc.T("Generador"), Loc.T("Da energía con carbón"), "3d3d44", 4, 3000); l[10].Cable = N | E | W; l[10].Exterior = true; l[10].Max = 2;
            l[11] = M(ModKind.Lab, Loc.T("Laboratorio"), Loc.T("Cristales +25 % con energía"), "6ff0ff", 5, 5000); l[11].Cable = N | S; l[11].NeedsPower = true; l[11].Floors = 1 | 2; l[11].Max = 1;
            l[12] = M(ModKind.Drill, Loc.T("Taladro"), Loc.T("Saca mineral raro solo"), "b06ef0", 7, 25000); l[12].Cable = S; l[12].RailOut = N; l[12].NeedsPower = true; l[12].Exterior = true; l[12].Max = 1;
            l[13] = M(ModKind.Tools, Loc.T("Herramientas"), Loc.T("Vecinos pican +10 %"), "a8703f", 2, 300); l[13].Floors = 1 | 2; l[13].Max = 2;
            l[14] = M(ModKind.Mess, Loc.T("Comedor"), Loc.T("Comen acá (dos a la vez) y descansan mejor al lado"), "5baa5e", 3, 600); l[14].Floors = 1 | 2; l[14].Max = 2;
            l[15] = M(ModKind.Classroom, Loc.T("Aula"), Loc.T("El Maestro enseña"), "4a82c9", 6, 8000); l[15].Floors = 1 | 2; l[15].Max = 1;
            l[16] = M(ModKind.Stairs, Loc.T("Escalera"), Loc.T("Sube un piso"), "a8703f", 5, 6000); l[16].Floors = 1 | 2; l[16].Max = 2;
            l[17] = M(ModKind.Observatory, Loc.T("Observatorio"), Loc.T("Ve venir los meteoritos"), "4a82c9", 9, 120000); l[17].Floors = 4; l[17].Max = 1;
            l[18] = M(ModKind.Dovecote, Loc.T("Palomar"), Loc.T("Contratos +20 %"), "f6ead2", 8, 40000); l[18].Floors = 2 | 4; l[18].Max = 1;
            l[19] = M(ModKind.Windmill, Loc.T("Molino"), Loc.T("Energía sin carbón"), "f6ead2", 7, 30000); l[19].Floors = 1 | 2 | 4; l[19].Interior = false; l[19].Exterior = true; l[19].Cable = S; l[19].Max = 2;
            l[20] = M(ModKind.Splitter, Loc.T("Desvío"), Loc.T("Divide una línea en dos"), "8e9aa8", 7, 30000); l[20].RailIn = S; l[20].RailOut = E | W; l[20].Exterior = true; l[20].Max = 2;
            l[21] = M(ModKind.Secret, Loc.T("Sala secreta"), Loc.T("¿Qué habrá adentro?"), "6b3fa0", 2, 2000); l[21].Floors = 1 | 2; l[21].Doors = S; l[21].Max = 3; l[21].Buyable = false;
            l[22] = M(ModKind.Experimental, Loc.T("Cámara experimental"), Loc.T("Mineral experimental x4"), "9a5bd9", 5, 50000); l[22].RailIn = S; l[22].RailOut = N; l[22].Cable = E | W; l[22].NeedsPower = true; l[22].Max = 1; l[22].Buyable = false;
            l[23] = M(ModKind.Trophy, Loc.T("Trofeos"), Loc.T("Recuerdos del complejo"), "f2c230", 1, 0); l[23].Floors = 1 | 2 | 4; l[23].Buyable = false; l[23].Max = 9;
            l[24] = M(ModKind.Magnet, Loc.T("Imán de vetas"), Loc.T("Más vetas en la isla"), "e04a6a", 1, 0); l[24].Floors = 1 | 2; l[24].Exterior = true; l[24].Buyable = false; l[24].Max = 2;
            l[25] = M(ModKind.Clock, Loc.T("Reloj"), Loc.T("Contratos +1 min"), "f2c230", 1, 0); l[25].Floors = 1 | 2 | 4; l[25].Buyable = false; l[25].Max = 1;
            l[26] = M(ModKind.PortLab, Loc.T("Laboratorio portátil"), Loc.T("Cristales +25 %"), "6ff0ff", 1, 0); l[26].Floors = 1 | 2; l[26].Exterior = true; l[26].Buyable = false; l[26].Portable = true;
            l[27] = M(ModKind.PortOven, Loc.T("Horno portátil"), Loc.T("Fundiciones +30 %"), "e05a2e", 1, 0); l[27].Floors = 1 | 2; l[27].Exterior = true; l[27].Buyable = false; l[27].Portable = true;
            l[28] = M(ModKind.PartyBell, Loc.T("Campana de fiesta"), Loc.T("Mineros contentos"), "f2c230", 1, 0); l[28].Floors = 1 | 2; l[28].Exterior = true; l[28].Buyable = false; l[28].Portable = true;
            l[29] = M(ModKind.Bath, Loc.T("Duchas"), Loc.T("Salen frescos (+30 %). Uno por vez: los demás hacen cola"), "4ab8e0", 2, 400); l[29].Floors = 1 | 2; l[29].Max = 2;
            return l;
        }

        public static ModDef MDef(ModKind k) { return ModDefs[(int)k]; }

        // ------------------------------------------------------------ estado
        public readonly List<Module> Modules = new List<Module>();
        int nextModule = 1;
        /// <summary>Reloj del Complejo (segundos de juego, incluye el tiempo afuera): vencen los portatiles.</summary>
        public double ComplexClock;
        /// <summary>Tipos de modulo habilitados antes de tiempo (premio de la Sala secreta).</summary>
        public int EarlyUnlocks;

        public event Action<Module> ModulePlaced, ModuleRemoved, ModuleMoved, ModuleChanged;
        /// <summary>Cambio la distribucion (la vista rearma el horneado).</summary>
        public event Action LayoutChanged;

        // ------------------------------------------------------------ geometria
        public static int RotMask(int mask, int rot)
        {
            rot &= 3;
            return ((mask << rot) | (mask >> (4 - rot))) & 0xF;
        }

        /// <summary>Lado del mundo (0..3) que mira el lado local `s` con el giro `rot`.</summary>
        public static int WorldSide(int s, int rot) { return (s + rot) & 3; }

        public static bool IsYard(int x, int z) { return (Math.Abs(x) == 2 && z == 0) || (Math.Abs(z) == 2 && x == 0); }
        public static bool IsCore(int x, int z) { return Math.Abs(x) <= 1 && Math.Abs(z) <= 1; }

        /// <summary>Celda habilitada por el nivel del Cuartel (sin mirar si esta ocupada).</summary>
        public static bool CellUnlocked(int x, int z, int f, int level)
        {
            if (level < 1) return false;
            if (f == 0)
            {
                if (x == 0 && z == 0) return true;
                if (x == 0 && z == -1) return level >= 1;
                if (x == 0 && z == 1) return level >= 2;
                if (z == 0 && Math.Abs(x) == 1) return level >= 3;
                if (Math.Abs(x) == 1 && Math.Abs(z) == 1) return level >= 4;
                if (IsYard(x, z)) return level >= 7;
                return false;
            }
            if (f == 1)
            {
                if (!IsCore(x, z)) return false;
                if (level >= 8) return true;
                if (level >= 6 && z == 1) return true;            // esquinas de atras
                if (level >= 5) return x == 0 || z == 0;          // la cruz
                return false;
            }
            if (f == 2) return level >= 9 && x == 0 && (z == 0 || z == 1);   // torres
            return false;
        }

        /// <summary>Radio de la huella del Cuartel en la isla segun su nivel (crece con las celdas habilitadas).</summary>
        public static float BarracksRadius(int level)
        {
            if (level >= 7) return 4.9f;
            if (level >= 4) return 4.1f;
            return 2.9f;
        }

        /// <summary>Posicion del mundo del centro de una celda (y del piso) relativa al Cuartel.</summary>
        public static void CellOffset(int x, int z, out float wx, out float wz)
        {
            double a = SlotYaw * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            float lx = x * Cell, lz = z * Cell;
            wx = (float)(lx * c + lz * s);
            wz = (float)(-lx * s + lz * c);
        }

        /// <summary>Celda local (redondeada) de un punto del mundo.</summary>
        public static void WorldToCell(float dx, float dz, out float lx, out float lz)
        {
            double a = SlotYaw * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            lx = (float)((dx * c - dz * s) / Cell);
            lz = (float)((dx * s + dz * c) / Cell);
        }

        public Plot BarracksPlot { get { return Find(BKind.Barracks); } }
        public int BarracksLevel { get { var p = BarracksPlot; return p == null ? 0 : p.Level; } }

        public Module ModAt(int x, int z, int f)
        {
            foreach (var m in Modules) if (m.X == x && m.Z == z && m.F == f) return m;
            return null;
        }

        public Module ModById(int id)
        {
            foreach (var m in Modules) if (m.Id == id) return m;
            return null;
        }

        public int CountMod(ModKind k) { int n = 0; foreach (var m in Modules) if (m.Kind == k && m.Expires <= 0) n++; return n; }
        public Module FirstMod(ModKind k) { foreach (var m in Modules) if (m.Kind == k) return m; return null; }

        // ------------------------------------------------------------ dormitorios (compatibilidad con 0.10)
        public bool HasRoom(int ch) { foreach (var m in Modules) if (m.Kind == ModKind.Dorm && m.Ch == ch) return true; return false; }
        public int DormCount() { int n = 0; foreach (var m in Modules) if (m.Kind == ModKind.Dorm) n++; return n; }
        public Module DormOf(int ch) { foreach (var m in Modules) if (m.Kind == ModKind.Dorm && m.Ch == ch) return m; return null; }

        /// <summary>Estado de la habitacion de un especialista: 0 bloqueada (nivel del Cuartel), 1 disponible, 2 sin lugar, 3 ya construida.</summary>
        public int RoomState(int ch)
        {
            if (ch <= 0 || ch >= Roster.Length || Roster[ch].Secret) return 0;
            if (HasRoom(ch)) return 3;
            if (BarracksLevel < Roster[ch].RoomLevel) return 0;
            if (FreeCells(ModKind.Dorm).Count == 0) return 2;
            return 1;
        }

        public double RoomCost(int ch) { return Math.Round(500.0 * Math.Pow(3.1, Math.Max(0, Roster[ch].RoomLevel - 1)) / 10.0) * 10.0; }

        public List<KeyValuePair<Res, int>> RoomMats(int ch)
        {
            int lv = Math.Max(1, Roster[ch].RoomLevel);
            var l = new List<KeyValuePair<Res, int>> { new KeyValuePair<Res, int>(Res.Stone, 8 + lv * 6), new KeyValuePair<Res, int>(Res.Wood, 6 + lv * 5) };
            if (lv >= 4) l.Add(new KeyValuePair<Res, int>(Res.IronBar, lv * 2));
            if (lv >= 7) l.Add(new KeyValuePair<Res, int>(Res.Glass, lv));
            return l;
        }

        public bool CanBuyRoom(int ch) { return RoomState(ch) == 1 && Coins >= RoomCost(ch) && HasMats(RoomMats(ch)); }

        // ------------------------------------------------------------ costos y desbloqueos
        public bool ModUnlocked(ModKind k)
        {
            var d = MDef(k);
            if (!d.Buyable) return false;
            if ((EarlyUnlocks & (1 << (int)k)) != 0) return BarracksLevel >= 1;
            return BarracksLevel >= d.Level;
        }

        /// <summary>Precio en monedas: crece con el nivel que lo habilita y con cuantos ya hay del mismo tipo.</summary>
        public double ModCost(ModKind k)
        {
            var d = MDef(k);
            double c = d.Cost * Math.Pow(2.6, Math.Max(0, d.Level - 2)) * Math.Pow(2.5, CountMod(k)) * IslandValue();
            return Math.Round(c / 10.0) * 10.0;
        }

        public List<KeyValuePair<Res, int>> ModMats(ModKind k)
        {
            int lv = Math.Max(1, MDef(k).Level);
            var l = new List<KeyValuePair<Res, int>> { new KeyValuePair<Res, int>(Res.Stone, 4 + lv * 4), new KeyValuePair<Res, int>(Res.Wood, 4 + lv * 3) };
            if (lv >= 4) l.Add(new KeyValuePair<Res, int>(Res.IronBar, lv));
            if (lv >= 6) l.Add(new KeyValuePair<Res, int>(Res.Glass, lv - 3));
            if (lv >= 8) l.Add(new KeyValuePair<Res, int>(Res.CutGem, 2));
            return l;
        }

        /// <summary>Por que no se puede comprar (0 = se puede): 1 bloqueado, 2 tope de cantidad, 3 sin lugar, 4 monedas, 5 materiales.</summary>
        public int ModBuyBlock(ModKind k)
        {
            var d = MDef(k);
            if (!ModUnlocked(k)) return 1;
            if (CountMod(k) >= d.Max) return 2;
            if (FreeCells(k).Count == 0) return 3;
            if (Coins < ModCost(k)) return 4;
            if (!HasMats(ModMats(k))) return 5;
            return 0;
        }

        // ------------------------------------------------------------ reglas de colocacion
        /// <summary>
        /// Se puede poner un modulo `k` en (x, z, f) con giro `rot`, ignorando al modulo `self` (el que se esta moviendo).
        /// Revisa celda habilitada, ocupada, pisos y patio del tipo, apoyo, no dejar flotando lo de arriba y no cerrar
        /// un circuito de rieles.
        /// </summary>
        public bool CanPlaceMod(ModKind k, int x, int z, int f, int rot, Module self = null)
        {
            var d = MDef(k);
            if (k == ModKind.Central) return false;
            if (!CellUnlocked(x, z, f, BarracksLevel)) return false;
            if ((d.Floors & (1 << f)) == 0) return false;
            bool yard = f == 0 && IsYard(x, z);
            if (yard && !d.Exterior) return false;
            if (!yard && !d.Interior) { if (!(d.Exterior && f > 0)) return false; }
            var occ = ModAt(x, z, f);
            if (occ != null && occ != self) return false;
            if (f > 0)
            {
                var below = ModAt(x, z, f - 1);
                if (below == null || below == self) return false;
            }
            if (self != null && (self.X != x || self.Z != z || self.F != f))
            {
                var above = ModAt(self.X, self.Z, self.F + 1);
                if (above != null) return false;   // lo de arriba quedaria flotando
            }
            if (d.RailIn != 0 || d.RailOut != 0)
            {
                // no cerrar un circuito
                var probe = new Module { Id = -7, Kind = k, X = x, Z = z, F = f, Rot = rot };
                if (CreatesLoop(probe, self)) return false;
            }
            return true;
        }

        /// <summary>Celdas libres validas para un tipo (con algun giro).</summary>
        public List<int[]> FreeCells(ModKind k, Module self = null)
        {
            var l = new List<int[]>();
            int lv = BarracksLevel;
            if (lv < 1) return l;
            for (int f = 0; f <= 2; f++)
                for (int x = -2; x <= 2; x++)
                    for (int z = -2; z <= 2; z++)
                    {
                        if (!CellUnlocked(x, z, f, lv)) continue;
                        for (int r = 0; r < 4; r++)
                            if (CanPlaceMod(k, x, z, f, r, self)) { l.Add(new[] { x, z, f }); break; }
                    }
            return l;
        }

        /// <summary>
        /// Giro automatico: el que mas conectores tipados une (rieles con rieles, cables con cables); a igualdad, el
        /// frente (lado S local) hacia afuera del Cuartel o hacia la camara. -1 si no entra con ningun giro.
        /// </summary>
        public int BestRotation(ModKind k, int x, int z, int f, Module self = null)
        {
            int best = -1, bestScore = int.MinValue;
            for (int r = 0; r < 4; r++)
            {
                if (!CanPlaceMod(k, x, z, f, r, self)) continue;
                int s = LinkScore(k, x, z, f, r, self) * 10 + FacadeScore(x, z, r);
                if (s > bestScore) { bestScore = s; best = r; }
            }
            return best;
        }

        /// <summary>Giros validos (para el boton ⟳: solo los que no rompen lo conectado).</summary>
        public List<int> ValidRotations(ModKind k, int x, int z, int f, Module self = null)
        {
            var l = new List<int>();
            int top = -1;
            var scores = new int[4];
            for (int r = 0; r < 4; r++)
            {
                scores[r] = CanPlaceMod(k, x, z, f, r, self) ? LinkScore(k, x, z, f, r, self) : -1;
                if (scores[r] > top) top = scores[r];
            }
            for (int r = 0; r < 4; r++) if (scores[r] >= 0 && scores[r] == top) l.Add(r);
            return l;
        }

        static int FacadeScore(int x, int z, int r)
        {
            int front = WorldSide(2, r);   // lado S local = fachada
            int score = 0;
            if (front == 2) score += 2;    // hacia la camara
            if ((front == 1 && x > 0) || (front == 3 && x < 0) || (front == 0 && z > 0) || (front == 2 && z < 0)) score += 1;
            return score;
        }

        /// <summary>Conectores tipados que calzan con los vecinos (rieles en el sentido correcto, cables).</summary>
        public int LinkScore(ModKind k, int x, int z, int f, int rot, Module self = null)
        {
            var d = MDef(k);
            int rin = RotMask(d.RailIn, rot), rout = RotMask(d.RailOut, rot), cab = RotMask(d.Cable, rot);
            int n = 0;
            for (int s = 0; s < 4; s++)
            {
                var nb = ModAt(x + SideDX[s], z + SideDZ[s], f);
                if (nb == null || nb == self) continue;
                var nd = MDef(nb.Kind);
                int opp = (s + 2) & 3;
                if ((rout & (1 << s)) != 0 && (RotMask(nd.RailIn, nb.Rot) & (1 << opp)) != 0) n++;
                if ((rin & (1 << s)) != 0 && (RotMask(nd.RailOut, nb.Rot) & (1 << opp)) != 0) n++;
                if ((cab & (1 << s)) != 0 && (CableMaskOf(nb) & (1 << opp)) != 0) n++;
            }
            return n;
        }

        /// <summary>Lados con cable activo (el Pasillo lleva cable desde el nivel 6 del Cuartel).</summary>
        public int CableMaskOf(Module m)
        {
            if (m.Kind == ModKind.Corridor && BarracksLevel < 6) return 0;
            return RotMask(MDef(m.Kind).Cable, m.Rot);
        }

        // ------------------------------------------------------------ acciones
        /// <summary>Compra y coloca un modulo. Devuelve el modulo o null.</summary>
        public Module BuyModule(ModKind k, int x, int z, int f, int rot, int ch = -1)
        {
            if (k == ModKind.Dorm)
            {
                if (!CanBuyRoom(ch)) return null;
            }
            else if (ModBuyBlock(k) != 0) return null;
            if (rot < 0) rot = BestRotation(k, x, z, f);
            if (rot < 0 || !CanPlaceMod(k, x, z, f, rot)) return null;
            if (k == ModKind.Dorm) { Coins -= RoomCost(ch); PayMats(RoomMats(ch)); }
            else { Coins -= ModCost(k); PayMats(ModMats(k)); }
            var m = AddModule(k, x, z, f, rot, ch);
            AddStat("modules", 1);
            if (k == ModKind.Dorm)
            {
                AddStat("rooms", 1);
                AddMiner(ch, rng.NextDouble() < GoldenChance, false);   // su especialista llega enseguida
            }
            return m;
        }

        /// <summary>Compra la habitacion de un especialista en la mejor celda libre (atajo de 0.10 y pruebas).</summary>
        public Module BuyRoom(int ch)
        {
            var cells = FreeCells(ModKind.Dorm);
            if (cells.Count == 0) return null;
            var c = cells[0];
            return BuyModule(ModKind.Dorm, c[0], c[1], c[2], -1, ch);
        }

        Module AddModule(ModKind k, int x, int z, int f, int rot, int ch)
        {
            var m = new Module { Id = nextModule++, Kind = k, X = x, Z = z, F = f, Rot = rot & 3, Ch = ch };
            Modules.Add(m);
            Dirty();
            ModulePlaced?.Invoke(m);
            return m;
        }

        /// <summary>Mover un modulo (gratis): pausa su trabajo 5 s.</summary>
        public bool MoveModule(Module m, int x, int z, int f, int rot)
        {
            if (m == null || m.Kind == ModKind.Central) return false;
            if (rot < 0) rot = BestRotation(m.Kind, x, z, f, m);
            if (rot < 0 || !CanPlaceMod(m.Kind, x, z, f, rot, m)) return false;
            m.X = x; m.Z = z; m.F = f; m.Rot = rot & 3;
            movePause[m.Id] = 5f;
            AddStat("modmoves", 1);
            Dirty();
            ModuleMoved?.Invoke(m);
            return true;
        }

        public bool RotateModule(Module m, int rot)
        {
            if (m == null || m.Kind == ModKind.Central) return false;
            if (!CanPlaceMod(m.Kind, m.X, m.Z, m.F, rot, m)) return false;
            m.Rot = rot & 3;
            Dirty();
            ModuleMoved?.Invoke(m);
            return true;
        }

        /// <summary>Quitar un modulo (portatiles y trofeos; los demas se mueven, no se venden). No deja nada flotando.</summary>
        public bool RemoveModule(Module m)
        {
            if (m == null || m.Kind == ModKind.Central || m.Kind == ModKind.Dorm) return false;
            if (ModAt(m.X, m.Z, m.F + 1) != null) return false;
            Modules.Remove(m);
            Dirty();
            ModuleRemoved?.Invoke(m);
            return true;
        }

        readonly Dictionary<int, float> movePause = new Dictionary<int, float>();
        public bool Paused(Module m) { float t; return movePause.TryGetValue(m.Id, out t) && t > 0f; }

        /// <summary>Al construir el Cuartel aparece la Sala central (y al cargar partidas viejas).</summary>
        void EnsureCentral()
        {
            if (BarracksPlot == null || BarracksPlot.Level < 1) return;
            if (FirstMod(ModKind.Central) != null) return;
            var occ = ModAt(0, 0, 0);
            if (occ != null) Modules.Remove(occ);
            Modules.Insert(0, new Module { Id = nextModule++, Kind = ModKind.Central, X = 0, Z = 0, F = 0, Rot = 0 });
            Dirty();
        }

        // ------------------------------------------------------------ obra de etapa
        public const int MaxStage = 3;

        public double StageCost(Module m)
        {
            if (m.Kind == ModKind.Dorm) return Math.Round(RoomCost(m.Ch) * (m.Stage == 1 ? 2.2 : 4.8));
            var d = MDef(m.Kind);
            double baseC = Math.Max(300, d.Cost) * Math.Pow(2.6, Math.Max(0, d.Level - 2)) * IslandValue();
            return Math.Round(baseC * (m.Stage == 1 ? 2.2 : 4.8) / 10.0) * 10.0;
        }

        public List<KeyValuePair<Res, int>> StageMats(Module m)
        {
            int s = m.Stage;
            var l = new List<KeyValuePair<Res, int>> { new KeyValuePair<Res, int>(Res.Stone, 10 * s), new KeyValuePair<Res, int>(Res.Wood, 8 * s) };
            l.Add(new KeyValuePair<Res, int>(Res.IronBar, 3 * s));
            if (s >= 2) l.Add(new KeyValuePair<Res, int>(Res.Glass, 4));
            return l;
        }

        public bool CanEvolve(Module m)
        {
            if (m == null || m.Stage >= MaxStage || m.Work > 0 || m.Expires > 0) return false;
            if (m.Kind == ModKind.Central || m.Kind == ModKind.Corridor || m.Kind == ModKind.Trophy || m.Kind == ModKind.Secret) return false;
            return Coins >= StageCost(m) && HasMats(StageMats(m));
        }

        public bool Evolve(Module m)
        {
            if (!CanEvolve(m)) return false;
            Coins -= StageCost(m);
            PayMats(StageMats(m));
            m.WorkTotal = m.Work = m.Stage == 1 ? 45 : 150;
            AddStat("evolve", 1);
            ModuleChanged?.Invoke(m);
            return true;
        }

        /// <summary>Etapa de la Sala central: sigue al nivel del Cuartel (madera, piedra, oro).</summary>
        public int CentralStage { get { int l = BarracksLevel; return l >= 7 ? 3 : l >= 4 ? 2 : 1; } }

        /// <summary>Multiplicador de efecto segun la etapa (x1, x1.5, x2 sobre la parte de bono).</summary>
        public static double StageK(Module m) { return m.Stage >= 3 ? 2.0 : m.Stage == 2 ? 1.5 : 1.0; }

        // ------------------------------------------------------------ guardado
        List<object> ModulesObj()
        {
            var l = new List<object>();
            foreach (var m in Modules)
            {
                var d = new Dictionary<string, object> { { "k", (int)m.Kind }, { "x", m.X }, { "z", m.Z }, { "f", m.F }, { "r", m.Rot }, { "s", m.Stage } };
                if (m.Ch >= 0) d["c"] = m.Ch;
                if (m.Expires > 0) d["e"] = m.Expires;
                if (m.Work > 0) { d["w"] = m.Work; d["wt"] = m.WorkTotal; }
                if (m.TunedT > 0) d["t"] = m.TunedT;
                if (m.Deco > 0) d["d"] = m.Deco;
                if (m.Count > 0) d["n"] = m.Count;
                l.Add(d);
            }
            return l;
        }

        /// <summary>Lectura tolerante: lo invalido se descarta (nunca rompe la partida).</summary>
        void LoadModules(List<object> l)
        {
            Modules.Clear();
            nextModule = 1;
            int lv = BarracksLevel;
            // primero la planta (para que los pisos de arriba tengan apoyo al validarse)
            var entries = new List<Dictionary<string, object>>();
            foreach (var o in l) { var d = o as Dictionary<string, object>; if (d != null) entries.Add(d); }
            entries.Sort((a, b) => JsonRead.Int(a, "f", 0).CompareTo(JsonRead.Int(b, "f", 0)));
            foreach (var d in entries)
            {
                int ki = JsonRead.Int(d, "k", -1);
                if (ki < 0 || ki >= ModDefs.Length) continue;
                var k = (ModKind)ki;
                int x = JsonRead.Int(d, "x", 0), z = JsonRead.Int(d, "z", 0), f = JsonRead.Int(d, "f", 0), r = JsonRead.Int(d, "r", 0) & 3;
                int ch = JsonRead.Int(d, "c", -1);
                if (k == ModKind.Central) { if (FirstMod(ModKind.Central) == null && lv >= 1) Modules.Add(new Module { Id = nextModule++, Kind = k }); continue; }
                if (k == ModKind.Dorm && (ch <= 0 || ch >= Roster.Length || HasRoom(ch))) continue;
                if (ModAt(x, z, f) != null || !CellUnlocked(x, z, f, lv)) continue;
                if (f > 0 && ModAt(x, z, f - 1) == null) continue;
                var m = new Module
                {
                    Id = nextModule++, Kind = k, X = x, Z = z, F = f, Rot = r, Ch = ch,
                    Stage = Math.Max(1, Math.Min(MaxStage, JsonRead.Int(d, "s", 1))),
                    Expires = JsonRead.Dbl(d, "e", 0), Work = JsonRead.Dbl(d, "w", 0), WorkTotal = JsonRead.Dbl(d, "wt", 0),
                    TunedT = JsonRead.Dbl(d, "t", 0), Deco = JsonRead.Int(d, "d", 0), Count = JsonRead.Int(d, "n", 0),
                };
                Modules.Add(m);
            }
            EnsureCentral();
            Dirty();
        }

        /// <summary>
        /// Partidas 0.10 (guardado v4): las habitaciones estaban en 8 encastres fijos (0-3 lados N E S O, 4-7 esquinas).
        /// Pasan a las celdas equivalentes; si una no esta habilitada por el nivel, a la celda libre mas cercana. Una
        /// esquina sin acceso recibe de regalo un Pasillo en el lado vacio.
        /// </summary>
        void MigrateRooms(List<object> l)
        {
            Modules.Clear();
            nextModule = 1;
            EnsureCentral();
            int[] sx = { 0, 1, 0, -1, 1, 1, -1, -1 }, sz = { 1, 0, -1, 0, 1, -1, -1, 1 };
            int lv = BarracksLevel;
            foreach (var o in l)
            {
                var e = o as List<object>;
                if (e == null || e.Count < 2) continue;
                int ch = (int)JsonRead.ToDouble(e[0], 0), slot = (int)JsonRead.ToDouble(e[1], 0);
                if (ch <= 0 || ch >= Roster.Length || HasRoom(ch) || slot < 0 || slot >= 8) continue;
                int x = sx[slot], z = sz[slot];
                if (!CellUnlocked(x, z, 0, lv) || ModAt(x, z, 0) != null)
                {
                    int bx = 99, bz = 99, bd = int.MaxValue;
                    for (int cx = -1; cx <= 1; cx++)
                        for (int cz = -1; cz <= 1; cz++)
                        {
                            if (!CellUnlocked(cx, cz, 0, lv) || ModAt(cx, cz, 0) != null || (cx == 0 && cz == 0)) continue;
                            int dd = (cx - x) * (cx - x) + (cz - z) * (cz - z);
                            if (dd < bd) { bd = dd; bx = cx; bz = cz; }
                        }
                    if (bx == 99) continue;
                    x = bx; z = bz;
                }
                Modules.Add(new Module { Id = nextModule++, Kind = ModKind.Dorm, Ch = ch, X = x, Z = z, F = 0, Rot = 0 });
            }
            // esquinas sin acceso: pasillo de regalo
            foreach (var m in Modules.ToArray())
            {
                if (Math.Abs(m.X) != 1 || Math.Abs(m.Z) != 1) continue;
                if (ModAt(m.X, 0, 0) != null || ModAt(0, m.Z, 0) != null) continue;
                if (CellUnlocked(m.X, 0, 0, lv)) Modules.Add(new Module { Id = nextModule++, Kind = ModKind.Corridor, X = m.X, Z = 0 });
                else if (CellUnlocked(0, m.Z, 0, lv)) Modules.Add(new Module { Id = nextModule++, Kind = ModKind.Corridor, X = 0, Z = m.Z });
            }
            foreach (var m in Modules) if (m.Kind == ModKind.Dorm) m.Rot = BestRotation(ModKind.Dorm, m.X, m.Z, 0, m) & 3;
            Dirty();
        }

        // ------------------------------------------------------------ invariantes (pruebas y editor)
        /// <summary>null si el Complejo cumple todas las reglas; si no, la primera que falla.</summary>
        public string ValidateComplex()
        {
            var seen = new HashSet<long>();
            int lv = BarracksLevel, central = 0;
            var ids = new HashSet<int>();
            foreach (var m in Modules)
            {
                if (!ids.Add(m.Id)) return "id repetido " + m.Id;
                long key = ((long)(m.X + 8) << 20) | ((long)(m.Z + 8) << 10) | (long)m.F;
                if (!seen.Add(key)) return "celda doble " + m.X + "," + m.Z + "," + m.F;
                if (m.Kind == ModKind.Central) { central++; if (m.X != 0 || m.Z != 0 || m.F != 0) return "central fuera de lugar"; continue; }
                if (!CellUnlocked(m.X, m.Z, m.F, lv)) return "celda bloqueada " + m.Kind + " " + m.X + "," + m.Z + "," + m.F;
                if ((MDef(m.Kind).Floors & (1 << m.F)) == 0) return "piso no permitido " + m.Kind;
                if (m.F > 0 && ModAt(m.X, m.Z, m.F - 1) == null) return "flotando " + m.Kind;
                if (m.Kind == ModKind.Dorm && (m.Ch <= 0 || m.Ch >= Roster.Length)) return "dormitorio sin minero";
                if (m.Stage < 1 || m.Stage > MaxStage) return "etapa invalida";
                if (m.Rot < 0 || m.Rot > 3) return "giro invalido";
            }
            if (lv >= 1 && central != 1) return "centrales: " + central;
            if (lv < 1 && Modules.Count > 0 && BarracksPlot == null) return "modulos sin Cuartel";
            var s = Summary;
            if (s.Loops.Count > 0) return "rieles en circulo";
            for (int i = 0; i < s.CharBonus.Length; i++) if (s.CharBonus[i] > 0.6001f || float.IsNaN(s.CharBonus[i])) return "bono fuera de tope";
            var fresh = Compute();
            if (fresh.Access.Count != s.Access.Count || fresh.Powered.Count != s.Powered.Count) return "resumen viejo";
            foreach (var c in Carts) if (double.IsNaN(c.Value) || double.IsInfinity(c.Value) || c.Value > c.Base * MaxLineMult * 4.0001) return "vagoneta invalida";
            if (double.IsNaN(Coins) || double.IsInfinity(Coins)) return "monedas invalidas";
            int dorms = 0; foreach (var m in Modules) if (m.Kind == ModKind.Dorm) dorms++;
            var chs = new HashSet<int>(); foreach (var m in Modules) if (m.Kind == ModKind.Dorm && !chs.Add(m.Ch)) return "dormitorio repetido";
            return null;
        }
    }
}
