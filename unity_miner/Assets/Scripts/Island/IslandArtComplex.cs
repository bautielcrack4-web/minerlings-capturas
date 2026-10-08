using System.Collections.Generic;
using Mineros.Core;
using Mineros.World;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Arte del Complejo minero con autounion: cada modulo se arma por codigo y mira a sus vecinos. Entre dos modulos
    /// con puerta se abre un arco con viga y farol; si no, pared. Por un lado con riel sale la via por una boca; por uno
    /// con cable pasa un cano. Sin vecino: pared exterior con ventana o puerta. Sin modulo arriba: techo del color del
    /// modulo; con modulo arriba: losa. Asi el complejo SIEMPRE calza, se arme como se arme. Las maquinas de adentro
    /// son piezas de Tripo (o una version por codigo si falta el archivo).
    /// </summary>
    public static partial class IslandArt
    {
        public const float ModW = 1.8f, WallH = 1.25f, SlabH = 0.16f;

        /// <summary>Altura (local) del piso de un modulo.</summary>
        public static float FloorY(int f) { return f * Island.FloorH; }

        public static Vector3 CellPos(int x, int z, int f) { return new Vector3(x * Island.Cell, FloorY(f), z * Island.Cell); }

        static readonly Vector3[] SideDir = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        /// <summary>Color de techo de cada modulo (dormitorios: el de su mineral).</summary>
        public static Color ModRoof(Module m)
        {
            if (m.Kind == ModKind.Dorm) return SpecColor(m.Ch);
            if (m.Kind == ModKind.Central) return RoofRedF;
            return H(Island.MDef(m.Kind).Hex);
        }

        static Color ModWall(Module m, int centralStage)
        {
            int st = m.Kind == ModKind.Central ? centralStage : m.Stage;
            if (m.Kind == ModKind.Corridor) return Color.Lerp(Wood, Wall, 0.35f);
            if (m.Kind == ModKind.Generator || m.Kind == ModKind.Drill) return Metal;
            return st >= 2 ? Color.Lerp(Stone, Wall, 0.35f) : Wall;
        }

        static bool Exterior(Module m) { return m.F == 0 && Island.IsYard(m.X, m.Z) || m.Kind == ModKind.Windmill; }

        /// <summary>
        /// Arma las carcasas de un piso: `walls` (pisos, paredes, arcos, uniones, rieles, canos) y `roofs` (techos y
        /// losas). Coordenadas locales del Complejo (sin el giro del Cuartel). Con `slabs`, las losas (bajo otro modulo)
        /// van aparte: esas no se disuelven al acercar la camara (arriba hay otra habitacion).
        /// </summary>
        /// <param name="skip">no dibuja ese modulo (se arma con piezas que caen, IslandGame.Build)</param>
        /// <param name="only">dibuja solo ese modulo (las piezas que caen)</param>
        public static void ComplexFloor(Island isl, int floor, MeshBuilder walls, MeshBuilder roofs, List<RoomKit.Slot> kitWalls = null, List<RoomKit.Slot> kitRoofs = null, MeshBuilder slabs = null, Module skip = null, Module only = null)
        {
            var s = isl.Summary;
            int cst = isl.CentralStage;
            foreach (var m in isl.Modules)
            {
                if (m.F != floor || m == skip || (only != null && m != only)) continue;
                Vector3 c = CellPos(m.X, m.Z, m.F);
                var d = Island.MDef(m.Kind);
                float h = WallH * (m.Kind == ModKind.Corridor ? 0.85f : 1f);
                bool ext = Exterior(m);
                Color wall = ModWall(m, cst);
                Color trim = (m.Kind == ModKind.Central ? cst : m.Stage) >= 3 ? GoldC : WoodD;
                // piso: zocalo de piedra en planta, tablas arriba
                if (m.F == 0) walls.Box(c + new Vector3(0, SlabH * 0.5f, 0), new Vector3(ModW + 0.16f, SlabH, ModW + 0.16f), Stone, 0f);
                else walls.Box(c + new Vector3(0, SlabH * 0.5f, 0), new Vector3(ModW, SlabH, ModW), Wood, 0f);
                walls.Box(c + new Vector3(0, SlabH + 0.005f, 0), new Vector3(ModW - 0.2f, 0.01f, ModW - 0.2f), Color.Lerp(Wood, Wall, 0.25f), 0f);
                int doors = Island.RotMask(d.Doors, m.Rot), rin = Island.RotMask(d.RailIn, m.Rot), rout = Island.RotMask(d.RailOut, m.Rot), cab = isl.CableMaskOf(m);
                int outer = DoorSide(isl, m);
                for (int side = 0; side < 4; side++)
                {
                    var nb = isl.ModAt(m.X + Island.SideDX[side], m.Z + Island.SideDZ[side], m.F);
                    bool open = nb != null && (doors & (1 << side)) != 0 && (Island.RotMask(Island.MDef(nb.Kind).Doors, nb.Rot) & (1 << ((side + 2) & 3))) != 0;
                    bool rail = ((rin | rout) & (1 << side)) != 0;
                    bool cable = (cab & (1 << side)) != 0;
                    if (ext) { Railing(walls, c, side, open || rail, h); }
                    else if (open) Arch(walls, c, side, h, wall, trim);
                    else if (rail) RailPortal(walls, c, side, h, wall, trim);
                    else if (side == outer)
                    {
                        // con el kit: la pared con el hueco del tema (la hoja la pone IslandGame.Doors); si no, por codigo
                        if (!KitWall(kitWalls, m, "marco", c, side, h)) DoorWall(walls, c, side, h, wall, trim);
                    }
                    else if (!KitWall(kitWalls, m, nb == null ? "ventana" : "pared", c, side, h)) SolidWall(walls, c, side, h, wall, trim, nb == null);
                    if (rail) RailTrack(walls, c, side, nb != null);
                    if (cable) CablePipe(walls, c, side, h, nb != null && (isl.CableMaskOf(nb) & (1 << ((side + 2) & 3))) != 0, s.Net.ContainsKey(m.Id));
                    // union con el vecino de N y E (una sola vez por par): piso, paredes del hueco y viga
                    if (nb != null && (side == 0 || side == 1) && !ext && !Exterior(nb)) Joint(walls, roofs, c, side, Mathf.Min(h, WallH * (nb.Kind == ModKind.Corridor ? 0.85f : 1f)), open || rail, wall, trim, m.F);
                }
                if (rin != 0 && rout != 0) InnerRails(walls, c, rin, rout);
                else if (rout != 0) InnerRails(walls, c, 0, rout);
                else if (rin != 0) InnerRails(walls, c, rin, 0);
                // columnas en las esquinas (marco del modulo)
                if (!ext)
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = (i % 2 == 0 ? -1 : 1) * (ModW * 0.5f - 0.06f), sz = (i < 2 ? -1 : 1) * (ModW * 0.5f - 0.06f);
                        walls.Box(c + new Vector3(sx, SlabH + h * 0.5f, sz), new Vector3(0.14f, h, 0.14f), trim, trim == GoldC ? 0.2f : 0f);
                    }
                // techo o losa
                var above = isl.ModAt(m.X, m.Z, m.F + 1);
                float top = SlabH + h;
                if (above != null) (slabs ?? roofs).Box(c + new Vector3(0, top + 0.07f, 0), new Vector3(ModW + 0.04f, 0.14f, ModW + 0.04f), StoneD, 0f);
                else if (ext) Canopy(roofs, c, m, h);
                else if (!CommonRoof(isl, kitRoofs, roofs, m, c, top))
                    ModuleRoof(roofs, c, m, top, cst);
            }
        }

        /// <summary>Lleva el techo comun (pizarra) y se une con sus vecinas: habitaciones bajo techo, sin la Sala central.</summary>
        static bool SharesRoof(Island isl, Module m)
        {
            if (m == null || Exterior(m) || m.Kind == ModKind.Central || m.Kind == ModKind.Corridor) return false;
            return isl.ModAt(m.X, m.Z, m.F + 1) == null;
        }

        /// <summary>
        /// Techo comun por variantes (kit_comun.py): hacia cada vecina que tambien lo lleva el techo no baja y sigue a la
        /// altura de la cumbrera, asi las habitaciones juntas se leen como un solo techo (PLAN_HABITACIONES §8). Encima, el
        /// remate del tema (cupula de oro, chimenea de cobre...) o, si el tema no tiene, una tapa con el color del modulo.
        /// La Sala central queda aparte con su techo alto: es la silueta que se reconoce de lejos.
        /// </summary>
        static bool CommonRoof(Island isl, List<RoomKit.Slot> kitRoofs, MeshBuilder roofs, Module m, Vector3 c, float top)
        {
            if (kitRoofs == null || !SharesRoof(isl, m)) return false;
            int mask = 0;
            for (int side = 0; side < 4; side++)
                if (SharesRoof(isl, isl.ModAt(m.X + Island.SideDX[side], m.Z + Island.SideDZ[side], m.F))) mask |= 1 << side;
            // limahoyas (kit_lib.roof_variant): esquina interior de una L, los dos lados techados y la diagonal no.
            // Antes la meseta seguia plana hasta la esquina y quedaba un escalon contra los faldones de las vecinas.
            int full = mask;
            for (int k = 0; k < 4; k++)
            {
                int a = k, b = (k + 1) & 3;   // NE, ES, SO, ON (lados del juego: N=+Z, E=+X, S=-Z, O=-X)
                if ((mask & (1 << a)) == 0 || (mask & (1 << b)) == 0) continue;
                int dx = Island.SideDX[a] + Island.SideDX[b], dz = Island.SideDZ[a] + Island.SideDZ[b];
                if (!SharesRoof(isl, isl.ModAt(m.X + dx, m.Z + dz, m.F))) full |= 16 << k;
            }
            Vector3 p = c + Vector3.up * top;
            if (!RoomKit.Place(kitRoofs, RoomKit.Common("techo_" + full) ?? RoomKit.Common("techo_" + mask), p, m)) return false;
            if (!RoomKit.Place(kitRoofs, RoomKit.Piece(m, "remate"), p, m))
                roofs.Box(p + new Vector3(0f, 0.67f, 0f), new Vector3(0.26f, 0.08f, 0.26f), (m.Stage >= 3 ? GoldC : Color.Lerp(ModRoof(m), Dark, 0.1f)), 0.3f);
            return true;
        }

        /// <summary>Pared del kit en ese lado (calza en el mismo lugar que la de codigo). false si el tema no tiene esa pieza.</summary>
        static bool KitWall(List<RoomKit.Slot> kit, Module m, string piece, Vector3 c, int side, float h)
        {
            if (kit == null) return false;
            return RoomKit.Fit(kit, RoomKit.Piece(m, piece), SideCenter(c, side, SlabH) - SideDir[side] * 0.06f, SideDir[side], new Vector3(ModW - 0.1f, h, 0f), m);
        }

        static Vector3 SideCenter(Vector3 c, int side, float y) { return c + SideDir[side] * (ModW * 0.5f) + Vector3.up * y; }
        static Vector3 Along(int side) { return side % 2 == 0 ? Vector3.right : Vector3.forward; }

        /// <summary>Caja orientada al lado: largo a lo largo de la pared, grosor hacia afuera.</summary>
        static void WallBox(MeshBuilder mb, Vector3 center, int side, float len, float hgt, float thick, Color col, float em = 0f)
        {
            var size = side % 2 == 0 ? new Vector3(len, hgt, thick) : new Vector3(thick, hgt, len);
            mb.Box(center, size, col, em);
        }

        static void SolidWall(MeshBuilder mb, Vector3 c, int side, float h, Color wall, Color trim, bool outside)
        {
            WallBox(mb, SideCenter(c, side, SlabH + h * 0.5f) - SideDir[side] * 0.06f, side, ModW - 0.1f, h, 0.12f, wall);
            if (outside)
            {
                // ventana en el medio y una viga arriba
                Vector3 wc = SideCenter(c, side, SlabH + h * 0.62f) + SideDir[side] * 0.01f;
                WallBox(mb, wc, side, 0.46f, 0.42f, 0.06f, WoodD);
                WallBox(mb, wc + SideDir[side] * 0.02f, side, 0.34f, 0.32f, 0.06f, Glass, 0.25f);
                WallBox(mb, SideCenter(c, side, SlabH + h - 0.06f) + SideDir[side] * 0.01f, side, ModW - 0.1f, 0.1f, 0.05f, trim);
            }
        }

        static void Arch(MeshBuilder mb, Vector3 c, int side, float h, Color wall, Color trim)
        {
            float open = 0.9f, pil = (ModW - 0.1f - open) * 0.5f;
            Vector3 a = Along(side);
            Vector3 b = SideCenter(c, side, 0f) - SideDir[side] * 0.06f;
            WallBox(mb, b + a * (open * 0.5f + pil * 0.5f) + Vector3.up * (SlabH + h * 0.5f), side, pil, h, 0.12f, wall);
            WallBox(mb, b - a * (open * 0.5f + pil * 0.5f) + Vector3.up * (SlabH + h * 0.5f), side, pil, h, 0.12f, wall);
            float lintel = h - 0.95f;
            WallBox(mb, b + Vector3.up * (SlabH + 0.95f + lintel * 0.5f), side, open, lintel, 0.12f, wall);
            WallBox(mb, b + Vector3.up * (SlabH + 0.95f + 0.04f), side, open + 0.12f, 0.08f, 0.16f, trim);   // viga
        }

        /// <summary>Lado del modulo con puerta a la calle (marco, escalon, farol y hoja que se abre). -1 si no tiene.</summary>
        public static int DoorSide(Island isl, Module m)
        {
            if (m.F != 0 || Exterior(m) || !isl.Summary.Access.Contains(m.Id)) return -1;
            int side = isl.OuterDoor(m);
            if (side < 0) return -1;
            var d = Island.MDef(m.Kind);
            if (((Island.RotMask(d.RailIn, m.Rot) | Island.RotMask(d.RailOut, m.Rot)) & (1 << side)) != 0) return -1;   // ahi va la boca del riel
            return side;
        }

        /// <summary>Hueco de la puerta exterior (coordenadas del piso): bisagra, hacia donde se extiende la hoja y hacia adentro.</summary>
        public static void DoorFrame(Module m, int side, out Vector3 hinge, out Vector3 along, out Vector3 inward)
        {
            Vector3 c = CellPos(m.X, m.Z, m.F);
            along = -Along(side);
            inward = -SideDir[side];
            hinge = SideCenter(c, side, SlabH) - SideDir[side] * 0.06f - along * (DoorOpen * 0.5f - 0.02f);
        }

        public const float DoorOpen = 0.9f, DoorHgt = 0.93f;

        /// <summary>Hoja de puerta por codigo (tablas, dos herrajes y tirador), con la bisagra en el origen y extendida hacia +X.</summary>
        public static void DoorLeaf(MeshBuilder mb)
        {
            float w = DoorOpen - 0.06f, h = DoorHgt;
            for (int i = 0; i < 4; i++)
            {
                float pw = w / 4f;
                Color col = Color.Lerp(Wood, WoodD, (i % 2) * 0.35f);
                mb.Box(new Vector3(pw * (i + 0.5f), h * 0.5f, 0f), new Vector3(pw - 0.012f, h, 0.05f), col, 0f);
            }
            for (int r = 0; r < 2; r++)
                mb.Box(new Vector3(w * 0.5f, h * (r == 0 ? 0.22f : 0.78f), 0f), new Vector3(w * 0.96f, 0.06f, 0.07f), Iron, 0f);
            mb.Box(new Vector3(w * 0.85f, h * 0.5f, 0f), new Vector3(0.05f, 0.05f, 0.11f), GoldC, 0.2f);
        }

        static void DoorWall(MeshBuilder mb, Vector3 c, int side, float h, Color wall, Color trim)
        {
            Arch(mb, c, side, h, wall, trim);
            // puerta exterior: marco y escalon
            Vector3 b = SideCenter(c, side, 0f);
            WallBox(mb, b + SideDir[side] * 0.08f + Vector3.up * 0.04f, side, 0.8f, 0.08f, 0.22f, Stone);
            WallBox(mb, b + SideDir[side] * 0.02f + Vector3.up * (SlabH + 1.0f), side, 1.0f, 0.1f, 0.1f, WoodD);
            mb.Box(b + SideDir[side] * 0.14f + Along(side) * 0.55f + Vector3.up * (SlabH + 0.9f), new Vector3(0.12f, 0.16f, 0.12f), Ember, 0.9f);   // farol
        }

        static void RailPortal(MeshBuilder mb, Vector3 c, int side, float h, Color wall, Color trim)
        {
            float open = 0.7f, pil = (ModW - 0.1f - open) * 0.5f;
            Vector3 a = Along(side);
            Vector3 b = SideCenter(c, side, 0f) - SideDir[side] * 0.06f;
            WallBox(mb, b + a * (open * 0.5f + pil * 0.5f) + Vector3.up * (SlabH + h * 0.5f), side, pil, h, 0.12f, wall);
            WallBox(mb, b - a * (open * 0.5f + pil * 0.5f) + Vector3.up * (SlabH + h * 0.5f), side, pil, h, 0.12f, wall);
            float lintel = h - 0.7f;
            WallBox(mb, b + Vector3.up * (SlabH + 0.7f + lintel * 0.5f), side, open, lintel, 0.12f, wall);
            WallBox(mb, b + Vector3.up * (SlabH + 0.72f), side, open + 0.1f, 0.07f, 0.16f, Iron);
        }

        static void RailTrack(MeshBuilder mb, Vector3 c, int side, bool across)
        {
            // de la boca hasta el borde del hueco (y cruzandolo si hay vecino)
            float from = 0.25f, to = ModW * 0.5f + (across ? Island.Cell - ModW : 0.05f) * 0.5f + 0.02f;
            Vector3 dir = SideDir[side], a = Along(side);
            float len = to - from;
            Vector3 mid = c + dir * (from + len * 0.5f) + Vector3.up * (SlabH + 0.03f);
            for (int r = -1; r <= 1; r += 2)
                mb.Box(mid + a * (0.17f * r), side % 2 == 0 ? new Vector3(0.05f, 0.04f, len) : new Vector3(len, 0.04f, 0.05f), Iron, 0f);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.28f));
            for (int i = 0; i < n; i++)
            {
                Vector3 p = c + dir * (from + (i + 0.5f) * len / n) + Vector3.up * (SlabH + 0.012f);
                mb.Box(p, side % 2 == 0 ? new Vector3(0.5f, 0.025f, 0.08f) : new Vector3(0.08f, 0.025f, 0.5f), WoodD, 0f);
            }
        }

        static void InnerRails(MeshBuilder mb, Vector3 c, int rin, int rout)
        {
            // tramo central que une la entrada con la salida (o termina en un tope)
            mb.Box(c + Vector3.up * (SlabH + 0.03f), new Vector3(0.5f, 0.04f, 0.5f), Color.Lerp(Iron, WoodD, 0.4f), 0f);
            if (rout == 0) mb.Box(c + Vector3.up * (SlabH + 0.12f), new Vector3(0.5f, 0.2f, 0.12f), H("e0533c"), 0.1f);   // tope de fin de linea
        }

        static void CablePipe(MeshBuilder mb, Vector3 c, int side, float h, bool linked, bool netted)
        {
            Vector3 dir = SideDir[side];
            float y = SlabH + h - 0.18f;
            float to = ModW * 0.5f + (linked ? (Island.Cell - ModW) * 0.5f + 0.02f : 0.08f);
            Vector3 a = c + dir * 0.35f + Vector3.up * y, b = c + dir * to + Vector3.up * y;
            mb.Cyl(a, b, 0.045f, 0.045f, 6, H("3a3a40"), 0f);
            mb.Box(b - dir * 0.15f, new Vector3(0.1f, 0.1f, 0.1f), netted ? H("ffd84a") : H("8a8a8a"), netted ? 0.6f : 0f);
        }

        static void Railing(MeshBuilder mb, Vector3 c, int side, bool open, float h)
        {
            if (open) return;
            Vector3 b = SideCenter(c, side, 0f) - SideDir[side] * 0.05f;
            WallBox(mb, b + Vector3.up * (SlabH + 0.42f), side, ModW - 0.1f, 0.06f, 0.06f, WoodD);
            for (int i = -2; i <= 2; i++) mb.Box(b + Along(side) * (i * 0.4f) + Vector3.up * (SlabH + 0.22f), new Vector3(0.06f, 0.42f, 0.06f), WoodD, 0f);
        }

        static void Canopy(MeshBuilder mb, Vector3 c, Module m, float h)
        {
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0 ? -1 : 1) * (ModW * 0.5f - 0.1f), sz = (i < 2 ? -1 : 1) * (ModW * 0.5f - 0.1f);
                mb.Box(c + new Vector3(sx, SlabH + h * 0.5f, sz), new Vector3(0.1f, h, 0.1f), WoodD, 0f);
            }
            if (m.Kind == ModKind.Windmill) return;
            Roof(mb, c + Vector3.up * (SlabH + h), ModW - 0.1f, ModW - 0.1f, 0.35f, Color.Lerp(RoofBase, ModRoof(m), 0.2f));
        }

        static void Joint(MeshBuilder walls, MeshBuilder roofs, Vector3 c, int side, float h, bool open, Color wall, Color trim, int f)
        {
            float gap = Island.Cell - ModW;
            Vector3 dir = SideDir[side], a = Along(side);
            Vector3 mid = c + dir * (ModW * 0.5f + gap * 0.5f);
            // piso del hueco
            walls.Box(mid + Vector3.up * (SlabH * 0.5f), side % 2 == 0 ? new Vector3(ModW - 0.1f, SlabH, gap + 0.02f) : new Vector3(gap + 0.02f, SlabH, ModW - 0.1f), f == 0 ? Stone : Wood, 0f);
            if (open)
            {
                // costados del pasaje
                float w = 0.98f;
                for (int r = -1; r <= 1; r += 2)
                    walls.Box(mid + a * (w * 0.5f * r) + Vector3.up * (SlabH + h * 0.5f), side % 2 == 0 ? new Vector3(0.08f, h, gap + 0.04f) : new Vector3(gap + 0.04f, h, 0.08f), wall, 0f);
                walls.Box(mid + Vector3.up * (SlabH + 0.95f + (h - 0.95f) * 0.5f), side % 2 == 0 ? new Vector3(w, h - 0.95f, gap + 0.04f) : new Vector3(gap + 0.04f, h - 0.95f, w), wall, 0f);
            }
            else
                walls.Box(mid + Vector3.up * (SlabH + h * 0.5f), side % 2 == 0 ? new Vector3(ModW - 0.1f, h, gap + 0.04f) : new Vector3(gap + 0.04f, h, ModW - 0.1f), wall, 0f);
            // viga que cubre la union (el techo queda continuo)
            roofs.Box(mid + Vector3.up * (SlabH + h + 0.05f), side % 2 == 0 ? new Vector3(ModW + 0.1f, 0.12f, gap + 0.3f) : new Vector3(gap + 0.3f, 0.12f, ModW + 0.1f), trim, trim == GoldC ? 0.2f : 0f);
        }

        /// <summary>
        /// Techo comun del Complejo: pizarra gris-azul para todas las habitaciones (una sola arquitectura). El color del
        /// modulo queda de acento en la cumbrera, no en todo el faldon: antes cada techo de otro color fragmentaba el
        /// Cuartel en cubos pegados (critica del 7-oct).
        /// </summary>
        public static readonly Color RoofBase = new Color(0.36f, 0.4f, 0.46f);

        static void ModuleRoof(MeshBuilder mb, Vector3 c, Module m, float top, int cst)
        {
            Color accent = ModRoof(m);
            Color col = RoofBase;
            int st = m.Kind == ModKind.Central ? cst : m.Stage;
            if (m.Kind == ModKind.Corridor)
            {
                mb.Box(c + Vector3.up * (top + 0.06f), new Vector3(ModW + 0.08f, 0.12f, ModW + 0.08f), Color.Lerp(col, Wood, 0.5f), 0f);
                return;
            }
            float rise = m.Kind == ModKind.Central ? 1.25f : 0.62f + 0.1f * (st - 1);
            float ridge = m.Kind == ModKind.Central ? 0.35f : 0.12f;
            HipRoof(mb, c + Vector3.up * top, ModW, ModW, rise, col, ridge);
            // cumbrera con el color del modulo (dorada desde la etapa 3)
            mb.Box(c + Vector3.up * (top + rise + 0.03f), new Vector3(ridge * 2f + 0.3f, 0.08f, 0.11f), st >= 3 ? GoldC : Color.Lerp(accent, Dark, 0.15f), 0.35f);
            if (m.Kind == ModKind.Central)
            {
                // silueta propia (auditoria final, §8 "el Cuartel se reconoce en 1 s a zoom 15"): torre con farol
                // encendido sobre la cumbrera y bandera grande en la punta
                Vector3 tb = c + Vector3.up * (top + rise - 0.2f);
                Color stone = Color.Lerp(Stone, Dark, 0.15f);
                mb.Box(tb + Vector3.up * 0.3f, new Vector3(0.56f, 0.6f, 0.56f), stone, 0f);
                for (int sd = 0; sd < 4; sd++)
                {
                    Vector3 n = new Vector3(Island.SideDX[sd], 0f, Island.SideDZ[sd]);
                    mb.Box(tb + Vector3.up * 0.34f + n * 0.285f, new Vector3(n.x != 0f ? 0.02f : 0.2f, 0.26f, n.z != 0f ? 0.02f : 0.2f), H("ffc451"), 0.9f);
                }
                mb.Box(tb + Vector3.up * 0.62f, new Vector3(0.66f, 0.06f, 0.66f), WoodD, 0f);
                HipRoof(mb, tb + Vector3.up * 0.65f, 0.7f, 0.7f, 0.55f, col, 0.02f);
                Vector3 fb = tb + Vector3.up * 1.18f;
                mb.Cyl(fb, fb + Vector3.up * 0.95f, 0.03f, 0.025f, 6, WoodD, 0f);
                mb.Box(fb + new Vector3(0.3f, 0.78f, 0f), new Vector3(0.58f, 0.34f, 0.02f), cst >= 3 ? GoldC : H("e5484d"), 0.15f);
                if (cst >= 3) mb.Crystal(fb + Vector3.up * 0.98f, Vector3.up, 0.35f, 0.12f, 6, GemC, 0.6f);
            }
            if (m.Kind == ModKind.Smelter) Chimney(mb, c + new Vector3(0.45f, top + 0.15f, 0.3f), 0.6f + 0.2f * st, H("8a3a2a"), true);
            if (m.Kind == ModKind.Generator) Chimney(mb, c + new Vector3(-0.45f, top + 0.1f, 0.3f), 0.7f, H("3a3a40"), false);
        }

        /// <summary>
        /// Carcasa suelta de un modulo (giro 0, sin vecinos) para el fantasma que se arrastra: paredes con ventana, bocas
        /// de riel y canos donde tiene conectores, y su techo. El fantasma entero gira con el giro elegido.
        /// </summary>
        public static void ModuleGhost(MeshBuilder mb, ModKind k, int ch, int stage)
        {
            var d = Island.MDef(k);
            var m = new Module { Kind = k, Ch = ch, Stage = stage };
            Vector3 c = Vector3.zero;
            float h = WallH * (k == ModKind.Corridor ? 0.85f : 1f);
            Color wall = ModWall(m, 1), trim = stage >= 3 ? GoldC : WoodD;
            bool ext = k == ModKind.Windmill;
            mb.Box(c + new Vector3(0, SlabH * 0.5f, 0), new Vector3(ModW + 0.16f, SlabH, ModW + 0.16f), Stone, 0f);
            for (int side = 0; side < 4; side++)
            {
                bool rail = ((d.RailIn | d.RailOut) & (1 << side)) != 0;
                if (ext) Railing(mb, c, side, rail, h);
                else if (rail) RailPortal(mb, c, side, h, wall, trim);
                else if (side == 2 && (d.Doors & 4) != 0) DoorWall(mb, c, side, h, wall, trim);
                else SolidWall(mb, c, side, h, wall, trim, true);
                if (rail) RailTrack(mb, c, side, false);
                if ((d.Cable & (1 << side)) != 0) CablePipe(mb, c, side, h, false, false);
            }
            if (!ext)
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i % 2 == 0 ? -1 : 1) * (ModW * 0.5f - 0.06f), sz = (i < 2 ? -1 : 1) * (ModW * 0.5f - 0.06f);
                    mb.Box(c + new Vector3(sx, SlabH + h * 0.5f, sz), new Vector3(0.14f, h, 0.14f), trim, 0f);
                }
            if (ext) Canopy(mb, c, m, h);
            else ModuleRoof(mb, c, m, SlabH + h, 1);
        }

        /// <summary>
        /// Techo a cuatro aguas con alero (cada cara con su sombreado: se lee bien desde la camara en diagonal) y una
        /// franja de borde mas oscura. `ridge` = medio largo de la cumbrera.
        /// </summary>
        static void HipRoof(MeshBuilder mb, Vector3 c, float w, float d, float rise, Color col, float ridge)
        {
            float hw = w * 0.5f + 0.12f, hd = d * 0.5f + 0.12f;
            Vector3 a = c + new Vector3(-hw, 0, -hd), b = c + new Vector3(hw, 0, -hd), e = c + new Vector3(hw, 0, hd), f = c + new Vector3(-hw, 0, hd);
            Vector3 p0 = c + new Vector3(-ridge, rise, 0), p1 = c + new Vector3(ridge, rise, 0);
            mb.Quad(a, p0, p1, b, new Vector3(0, 1, -1), col, 0f);
            mb.Quad(e, p1, p0, f, new Vector3(0, 1, 1), Color.Lerp(col, Dark, 0.18f), 0f);
            mb.Tri(a, f, p0, new Vector3(-1, 1, 0), Color.Lerp(col, Dark, 0.1f), 0f);
            mb.Tri(b, p1, e, new Vector3(1, 1, 0), Color.Lerp(col, Dark, 0.28f), 0f);
            // borde del alero (da grosor al techo)
            mb.Box(c + new Vector3(0, -0.04f, 0), new Vector3(hw * 2f, 0.08f, hd * 2f), Color.Lerp(col, Dark, 0.4f), 0f);
        }

        // ------------------------------------------------------------ adentro: piezas y detalles
        /// <summary>Archivo de Tripo de la pieza de cada modulo ("" si no tiene).</summary>
        public static string PropFile(ModKind k)
        {
            switch (k)
            {
                case ModKind.Smelter: case ModKind.PortOven: return "cx_horno";
                case ModKind.Crusher: return "cx_trituradora";
                case ModKind.Generator: return "cx_generador";
                case ModKind.Lab: case ModKind.PortLab: case ModKind.Polisher: return "cx_laboratorio";
                case ModKind.Vault: return "cx_bovedafuerte";
                case ModKind.Treasury: return "cx_tesoro";
                case ModKind.Drill: return "cx_taladro";
                case ModKind.Tools: return "cx_banco";
                case ModKind.Mess: return "cx_comedor";
                case ModKind.Classroom: return "cx_aula";
                case ModKind.Observatory: return "cx_telescopio";
                case ModKind.Secret: return "cx_puertasecreta";
                case ModKind.Experimental: return "cx_experimento";
                case ModKind.Windmill: return "cx_molino";
                case ModKind.Dovecote: return "cx_palomar";
                case ModKind.PartyBell: return "cx_comedor";
                default: return "";
            }
        }

        /// <summary>Tamano (m) de la pieza dentro del modulo (crece con la etapa).</summary>
        public static float PropSize(Module m)
        {
            float s = m.Kind == ModKind.Windmill ? 1.9f : m.Kind == ModKind.Observatory || m.Kind == ModKind.Drill ? 1.45f : 1.15f;
            return s * (m.Stage >= 3 ? 1.12f : m.Stage == 2 ? 1.05f : 1f);
        }

        /// <summary>Detalles por codigo de cada modulo (camas, cajas, el mineral del dormitorio, adornos y etapa).</summary>
        public static void ModuleDetails(MeshBuilder mb, Module m, bool hasProp)
        {
            float y = SlabH;
            switch (m.Kind)
            {
                case ModKind.Central:
                    // escalera interna y mesa con mapa
                    for (int i = 0; i < 5; i++) mb.Box(new Vector3(-0.55f, y + 0.12f + i * 0.24f, -0.5f + i * 0.22f), new Vector3(0.5f, 0.08f, 0.2f), WoodD, 0f);
                    mb.Box(new Vector3(0.35f, y + 0.35f, -0.1f), new Vector3(0.6f, 0.06f, 0.45f), Wood, 0f);
                    mb.Box(new Vector3(0.35f, y + 0.39f, -0.1f), new Vector3(0.5f, 0.01f, 0.36f), H("f2e3c0"), 0f);
                    break;
                case ModKind.Dorm:
                {
                    Color c = SpecColor(m.Ch);
                    int beds = m.Stage >= 2 ? 2 : 1;
                    for (int i = 0; i < beds; i++)
                    {
                        float x = -0.45f + i * 0.9f;
                        mb.Box(new Vector3(x, y + 0.15f, 0.25f), new Vector3(0.5f, 0.2f, 0.9f), WoodD, 0f);
                        mb.Box(new Vector3(x, y + 0.28f, 0.28f), new Vector3(0.44f, 0.08f, 0.82f), Color.Lerp(c, Color.white, 0.55f), 0f);
                        mb.Box(new Vector3(x, y + 0.34f, 0.6f), new Vector3(0.36f, 0.08f, 0.18f), Color.white, 0f);
                    }
                    Crate(mb, new Vector3(0.45f, y, -0.45f), 0.32f);
                    // pila de su mineral (identidad sin texto)
                    int n = 2 + m.Stage * 2;
                    for (int i = 0; i < n; i++)
                        mb.Octa(new Vector3(-0.5f + (i % 3) * 0.14f, y + 0.08f + (i / 3) * 0.1f, -0.5f + (i % 2) * 0.1f), Vector3.one * 0.16f, c, m.Ch >= 4 && m.Ch != 3 ? 0.45f : 0.05f);
                    if (m.Stage >= 3) mb.Box(new Vector3(0f, y + 0.9f, -0.85f), new Vector3(0.5f, 0.3f, 0.03f), c, 0.3f);   // estandarte
                    break;
                }
                case ModKind.Bath:
                {
                    // Duchas: una ducha por etapa (caño, flor y plato con agua) y un banco con toalla
                    Color water = H("6fd0ff"), tile = H("e8eef2"), pipe = H("8a8f96");
                    mb.Box(new Vector3(0f, y + 0.01f, 0f), new Vector3(ModW - 0.3f, 0.02f, ModW - 0.3f), tile, 0f);
                    int n = Mathf.Clamp(m.Stage, 1, 3);
                    for (int i = 0; i < n; i++)
                    {
                        float x = (i - (n - 1) * 0.5f) * 0.55f;
                        mb.Box(new Vector3(x, y + 0.05f, 0.45f), new Vector3(0.44f, 0.08f, 0.44f), Color.white, 0f);
                        mb.Box(new Vector3(x, y + 0.095f, 0.45f), new Vector3(0.36f, 0.01f, 0.36f), water, 0.25f);
                        mb.Box(new Vector3(x, y + 0.65f, 0.7f), new Vector3(0.05f, 1.2f, 0.05f), pipe, 0f);
                        mb.Box(new Vector3(x, y + 1.22f, 0.58f), new Vector3(0.05f, 0.05f, 0.26f), pipe, 0f);
                        mb.Cyl(new Vector3(x, y + 1.2f, 0.46f), new Vector3(x, y + 1.14f, 0.46f), 0.09f, 0.07f, 8, pipe, 0f);
                    }
                    mb.Box(new Vector3(0f, y + 0.2f, -0.5f), new Vector3(0.9f, 0.08f, 0.26f), Wood, 0f);
                    mb.Box(new Vector3(0.22f, y + 0.26f, -0.5f), new Vector3(0.3f, 0.05f, 0.22f), H("e5484d"), 0f);   // toalla
                    break;
                }
                case ModKind.Corridor:
                    mb.Box(new Vector3(0, y + 0.006f, 0), new Vector3(0.7f, 0.012f, 1.6f), H("b5463c"), 0f);   // alfombra
                    break;
                case ModKind.Unload:
                    Crate(mb, new Vector3(-0.55f, y, -0.45f), 0.34f);
                    Crate(mb, new Vector3(-0.55f, y + 0.34f, -0.45f), 0.26f);
                    mb.Box(new Vector3(0.5f, y + 0.25f, -0.45f), new Vector3(0.5f, 0.5f, 0.08f), WoodD, 0f);   // tolva
                    break;
                case ModKind.Storage:
                    for (int i = 0; i < 4; i++) Crate(mb, new Vector3(-0.55f + (i % 2) * 1.1f, y, -0.5f + (i / 2) * 1.0f), 0.36f);
                    mb.Box(new Vector3(-0.55f, y + 0.5f, -0.5f), new Vector3(0.3f, 0.3f, 0.3f), Wood, 0f);
                    break;
                case ModKind.Splitter:
                    mb.Box(new Vector3(0, y + 0.2f, 0), new Vector3(0.3f, 0.4f, 0.3f), Iron, 0f);
                    mb.Box(new Vector3(0, y + 0.45f, 0), new Vector3(0.6f, 0.06f, 0.1f), H("e0533c"), 0.1f);   // palanca de desvio
                    break;
                case ModKind.Stairs:
                    for (int i = 0; i < 6; i++) mb.Box(new Vector3(0, y + 0.12f + i * 0.24f, -0.6f + i * 0.24f), new Vector3(0.9f, 0.08f, 0.24f), WoodD, 0f);
                    break;
                case ModKind.Trophy:
                    mb.Box(new Vector3(0, y + 0.3f, 0), new Vector3(0.5f, 0.6f, 0.5f), Stone, 0f);
                    mb.Octa(new Vector3(0, y + 0.8f, 0), new Vector3(0.3f, 0.4f, 0.3f), GoldC, 0.6f);
                    break;
                case ModKind.Magnet:
                    mb.Cyl(new Vector3(-0.25f, y, 0), new Vector3(-0.25f, y + 0.8f, 0), 0.12f, 0.12f, 10, H("e04a6a"), 0.1f);
                    mb.Cyl(new Vector3(0.25f, y, 0), new Vector3(0.25f, y + 0.8f, 0), 0.12f, 0.12f, 10, H("e04a6a"), 0.1f);
                    mb.Box(new Vector3(0, y + 0.85f, 0), new Vector3(0.62f, 0.14f, 0.26f), Metal, 0f);
                    break;
                case ModKind.Clock:
                    mb.Cyl(new Vector3(0, y + 0.2f, 0), new Vector3(0, y + 1.0f, 0), 0.22f, 0.18f, 10, Wood, 0f);
                    mb.Cyl(new Vector3(0, y + 0.85f, -0.2f), new Vector3(0, y + 0.85f, -0.24f), 0.2f, 0.2f, 14, H("fff6dc"), 0.2f);
                    break;
            }
            if (!hasProp)
            {
                // version por codigo si falta la pieza de Tripo
                switch (m.Kind)
                {
                    case ModKind.Smelter: case ModKind.PortOven:
                        mb.Box(new Vector3(0, y + 0.45f, 0.1f), new Vector3(0.8f, 0.9f, 0.7f), H("b5643c"), 0f);
                        mb.Box(new Vector3(0, y + 0.35f, -0.26f), new Vector3(0.36f, 0.36f, 0.06f), Ember, 0.9f);
                        break;
                    case ModKind.Crusher:
                        mb.Box(new Vector3(0, y + 0.4f, 0), new Vector3(0.8f, 0.8f, 0.6f), Iron, 0f);
                        mb.Box(new Vector3(0, y + 0.85f, 0), new Vector3(0.7f, 0.15f, 0.5f), WoodD, 0f);
                        break;
                    case ModKind.Treasury:
                        mb.Box(new Vector3(0, y + 0.4f, 0.2f), new Vector3(1.0f, 0.8f, 0.4f), Wood, 0f);
                        for (int i = 0; i < 4; i++) mb.Cyl(new Vector3(-0.3f + i * 0.2f, y + 0.8f, 0.2f), new Vector3(-0.3f + i * 0.2f, y + 0.95f, 0.2f), 0.07f, 0.07f, 8, GoldC, 0.4f);
                        break;
                    default:
                        if (Island.MDef(m.Kind).Hex != null && m.Kind != ModKind.Dorm && m.Kind != ModKind.Central && m.Kind != ModKind.Corridor && m.Kind != ModKind.Stairs && m.Kind != ModKind.Trophy)
                            mb.Box(new Vector3(0, y + 0.35f, 0), new Vector3(0.7f, 0.7f, 0.7f), H(Island.MDef(m.Kind).Hex), 0.1f);
                        break;
                }
            }
            if (m.Deco > 0) Deco(mb, m.Deco);
        }

        /// <summary>Adornos (no cambian la produccion): banderines, macetas, faroles, estatua, estandarte, cartel.</summary>
        static void Deco(MeshBuilder mb, int deco)
        {
            float top = SlabH + WallH;
            switch (deco)
            {
                case 1:
                    for (int i = 0; i < 5; i++) mb.Octa(new Vector3(-0.7f + i * 0.35f, top + 0.05f, -ModW * 0.5f - 0.05f), new Vector3(0.16f, 0.2f, 0.04f), i % 2 == 0 ? H("ffd84a") : H("4aa3f0"), 0.2f);
                    break;
                case 2:
                    for (int i = -1; i <= 1; i += 2)
                    {
                        mb.Box(new Vector3(i * 0.6f, SlabH + 0.12f, -ModW * 0.5f - 0.18f), new Vector3(0.24f, 0.24f, 0.24f), H("b5643c"), 0f);
                        mb.Octa(new Vector3(i * 0.6f, SlabH + 0.36f, -ModW * 0.5f - 0.18f), new Vector3(0.3f, 0.26f, 0.3f), Leaf, 0f);
                    }
                    break;
                case 3:
                    for (int i = -1; i <= 1; i += 2) mb.Box(new Vector3(i * 0.75f, SlabH + 0.95f, -ModW * 0.5f - 0.12f), new Vector3(0.12f, 0.18f, 0.12f), Ember, 1f);
                    break;
                case 4:
                    mb.Box(new Vector3(0.65f, SlabH + 0.2f, -ModW * 0.5f - 0.3f), new Vector3(0.3f, 0.4f, 0.3f), Stone, 0f);
                    mb.Octa(new Vector3(0.65f, SlabH + 0.6f, -ModW * 0.5f - 0.3f), new Vector3(0.22f, 0.4f, 0.22f), GoldC, 0.5f);
                    break;
                case 5:
                    mb.Box(new Vector3(0f, SlabH + 0.7f, -ModW * 0.5f - 0.04f), new Vector3(0.45f, 0.75f, 0.02f), H("b5463c"), 0.05f);
                    mb.Octa(new Vector3(0f, SlabH + 0.8f, -ModW * 0.5f - 0.06f), new Vector3(0.16f, 0.16f, 0.03f), GoldC, 0.5f);
                    break;
                default:
                    mb.Box(new Vector3(0f, top + 0.25f, -ModW * 0.5f + 0.1f), new Vector3(0.9f, 0.3f, 0.06f), Wood, 0f);
                    mb.Box(new Vector3(0f, top + 0.25f, -ModW * 0.5f + 0.06f), new Vector3(0.8f, 0.2f, 0.02f), H("f6ead2"), 0f);
                    break;
            }
        }

        /// <summary>Vagoneta por codigo (si falta la de Tripo): caja de madera con su mineral arriba.</summary>
        public static Mesh CartMesh(int kind, out Material[] mats)
        {
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0, 0.2f, 0), new Vector3(0.42f, 0.24f, 0.56f), Wood, 0f);
            mb.Box(new Vector3(0, 0.32f, 0), new Vector3(0.44f, 0.04f, 0.58f), Iron, 0f);
            for (int i = 0; i < 4; i++) mb.Cyl(new Vector3(-0.23f, 0.08f, (i < 2 ? -0.18f : 0.18f)), new Vector3(0.23f, 0.08f, (i < 2 ? -0.18f : 0.18f)), 0.07f, 0.07f, 8, Dark, 0f);
            for (int i = 0; i < 3; i++) mb.Octa(new Vector3(-0.1f + i * 0.1f, 0.38f, (i - 1) * 0.12f), Vector3.one * 0.18f, OreCol[Mathf.Clamp(kind, 0, OreCol.Length - 1)], kind >= 3 && kind != 5 ? 0.5f : 0.05f);
            Mesh m = mb.ToMesh(null, "Vagoneta" + kind, out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }

        /// <summary>Lingote o mineral (lo que lleva la vagoneta arriba despues de la Fundicion).</summary>
        public static Mesh LoadMesh(int kind, bool bar, out Material[] mats)
        {
            var mb = new MeshBuilder();
            Color c = OreCol[Mathf.Clamp(kind, 0, OreCol.Length - 1)];
            if (bar) for (int i = 0; i < 3; i++) mb.Box(new Vector3(-0.1f + i * 0.1f, 0.06f + (i % 2) * 0.05f, 0f), new Vector3(0.09f, 0.06f, 0.24f), kind == Island.OreGold ? GoldC : Color.Lerp(c, Color.white, 0.25f), 0.35f);
            else for (int i = 0; i < 3; i++) mb.Octa(new Vector3(-0.1f + i * 0.1f, 0.06f, (i - 1) * 0.1f), Vector3.one * 0.18f, c, kind >= 3 && kind != 5 ? 0.5f : 0.05f);
            Mesh m = mb.ToMesh(null, "Carga" + kind + (bar ? "b" : ""), out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }
    }
}
