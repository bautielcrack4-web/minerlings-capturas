using System.Collections.Generic;
using Mineros.Core;
using Mineros.World;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Edificios de la ciudad armados con el kit de habitaciones (Plan Pueblo, pedido del creador: "los logos de los
    /// edificios ni se entienden", "que cada edificio al hacer zoom veas el interior"). Los ~24 edificios sin modelo eran
    /// cajas con techo de color; ahora son una sala de la misma arquitectura que el Cuartel (piedra, madera, pizarra; el
    /// color del recurso solo de acento): pared con puerta al frente, ventanas a los costados, techo comun con el remate de
    /// su tema, su maquina adentro y, afuera, la pila de lo que produce. El techo se disuelve al acercar (MinerCut) y se ve
    /// la maquina. Los iconos del catalogo salen del mismo modelo.
    /// </summary>
    public static partial class IslandArt
    {
        /// <summary>Tema del kit de cada edificio de la ciudad (null = sigue con su modelo propio o el de codigo).</summary>
        public static string KitTheme(BKind k)
        {
            switch (k)
            {
                case BKind.Barn: case BKind.Warehouse: case BKind.SandPit: case BKind.Sawmill: case BKind.Canteen: return "piedra";
                case BKind.Foundry: case BKind.CopperMine: return "cobre";
                case BKind.IronMine: case BKind.Workshop: case BKind.Train: return "hierro";
                case BKind.CoalMine: case BKind.GlassKiln: return "carbon";
                case BKind.Market: case BKind.Bank: case BKind.GoldMine: case BKind.GoldRefinery: case BKind.Jeweler: return "oro";
                case BKind.GemMine: case BKind.CrystalWell: case BKind.Lab: return "cristal";
                case BKind.Hospital: case BKind.Lapidary: return "diamante";
                case BKind.School: case BKind.Managers: return "maestro";
                case BKind.Airport: return "raros";
                default: return null;
            }
        }

        static string KitPiece(string theme, string piece)
        {
            string f = "RoomKit/" + theme + "_" + piece;
            Material mat;
            return TripoModel(f, out mat) != null ? f : null;
        }

        /// <summary>Pieza (maquina) de adentro de cada edificio.</summary>
        static string KitProp(BKind k)
        {
            switch (k)
            {
                case BKind.Foundry: case BKind.GlassKiln: case BKind.GoldRefinery: return "cx_horno";
                case BKind.Workshop: case BKind.Sawmill: return "cx_banco";
                case BKind.Lab: case BKind.Lapidary: case BKind.Jeweler: return "cx_laboratorio";
                case BKind.Bank: case BKind.Market: return "cx_tesoro";
                case BKind.School: case BKind.Managers: return "cx_aula";
                case BKind.Train: return "cx_vagoneta";
                case BKind.Airport: return "cx_palomar";
                case BKind.Canteen: case BKind.Hospital: case BKind.Barracks: return "cx_comedor";
                case BKind.Barn: case BKind.Warehouse: return "";
                default: return "cx_trituradora";   // minas y canteras
            }
        }

        /// <summary>Recurso que se apila afuera (identidad sin texto), o -1.</summary>
        static Color KitPile(BKind k, out bool has)
        {
            has = true;
            switch (k)
            {
                case BKind.CoalMine: return OreCol[5];
                case BKind.IronMine: return OreCol[2];
                case BKind.CopperMine: return OreCol[1];
                case BKind.GoldMine: case BKind.GoldRefinery: return OreCol[3];
                case BKind.GemMine: case BKind.Lapidary: case BKind.Jeweler: return OreCol[4];
                case BKind.CrystalWell: case BKind.Lab: return OreCol[6];
                case BKind.SandPit: return H("e8cf8f");
                case BKind.Sawmill: return Wood;
                default: has = false; return Color.white;
            }
        }

        /// <summary>Anexo atras (+Z): sala angosta pegada a la pared del fondo, mas baja, con su techo y otra maquina.</summary>
        const float AnnexX = 0.75f;

        static void KitAnnex(MeshBuilder code, List<RoomKit.Slot> walls, List<RoomKit.Slot> roofs, string th)
        {
            const float ad = 1.2f, ah = WallH * 0.82f;
            Vector3 c = new Vector3(AnnexX, 0f, ModW * 0.5f + ad * 0.5f);   // corrido a la derecha: asoma de frente (forma de L)
            code.Box(c + new Vector3(0, SlabH * 0.5f, 0), new Vector3(ModW + 0.16f, SlabH, ad + 0.1f), Stone, 0f);
            code.Box(c + new Vector3(0, SlabH + 0.005f, 0), new Vector3(ModW - 0.2f, 0.01f, ad - 0.15f), Color.Lerp(Wood, Wall, 0.25f), 0f);
            string win = KitPiece(th, "ventana") ?? KitPiece(th, "pared"), wall = KitPiece(th, "pared");
            // costados (E y O) y fondo (N); el lado de la sala principal queda con su propia pared
            RoomKit.Fit(walls, win, c + new Vector3(ModW * 0.5f - 0.06f, SlabH, 0f), Vector3.right, new Vector3(ad - 0.1f, ah, 0f));
            RoomKit.Fit(walls, win, c + new Vector3(-ModW * 0.5f + 0.06f, SlabH, 0f), Vector3.left, new Vector3(ad - 0.1f, ah, 0f));
            RoomKit.Fit(walls, wall, c + new Vector3(0f, SlabH, ad * 0.5f - 0.06f), Vector3.forward, new Vector3(ModW - 0.1f, ah, 0f));
            for (int i = 0; i < 2; i++)
                code.Box(c + new Vector3((i == 0 ? -1 : 1) * (ModW * 0.5f - 0.06f), SlabH + ah * 0.5f, ad * 0.5f - 0.06f), new Vector3(0.14f, ah, 0.14f), WoodD, 0f);
            // techo comun aplastado a lo hondo del anexo
            string roof = RoomKit.Common("techo_0");
            if (roof != null)
                roofs.Add(new RoomKit.Slot { File = roof, M = Matrix4x4.TRS(c + Vector3.up * (SlabH + ah), Quaternion.Euler(0f, 180f, 0f), new Vector3(1f, 0.7f, ad / ModW)) });
            code.Box(c + new Vector3(0.6f, (SlabH + ah + 0.6f) * 0.5f, 0.3f), new Vector3(0.3f, SlabH + ah + 0.6f, 0.3f), StoneD, 0f);   // chimenea chica del anexo (desde el piso)
        }

        public static bool HasKitBuilding(BKind k)
        {
            string th = KitTheme(k);
            return th != null && KitPiece(th, "pared") != null && RoomKit.Common("techo_0") != null;
        }

        /// <summary>
        /// Arma el edificio dentro de `parent` (frente hacia -Z local: el que lo use lo gira 35° hacia la camara).
        /// Devuelve la altura y deja los techos en un hijo "Techos" con materiales de corte (se disuelven al acercar).
        /// </summary>
        public static float KitBuilding(BKind k, int level, Transform parent)
        {
            string th = KitTheme(k) ?? "piedra";   // el Cuartel (solo su icono) usa la piedra del complejo
            var walls = new List<RoomKit.Slot>();
            var roofs = new List<RoomKit.Slot>();
            var code = new MeshBuilder();
            Vector3 c = Vector3.zero;
            float h = WallH;
            // piso de piedra
            code.Box(c + new Vector3(0, SlabH * 0.5f, 0), new Vector3(ModW + 0.16f, SlabH, ModW + 0.16f), Stone, 0f);
            code.Box(c + new Vector3(0, SlabH + 0.005f, 0), new Vector3(ModW - 0.2f, 0.01f, ModW - 0.2f), Color.Lerp(Wood, Wall, 0.25f), 0f);
            // paredes: puerta al frente (-Z), ventanas a los costados, pared atras
            for (int side = 0; side < 4; side++)
            {
                string piece = side == 2 ? "marco" : side == 0 ? "pared" : "ventana";
                string f = KitPiece(th, piece) ?? KitPiece(th, "pared");
                if (f == null) continue;
                RoomKit.Fit(walls, f, SideCenter(c, side, SlabH) - SideDir[side] * 0.06f, SideDir[side], new Vector3(ModW - 0.1f, h, 0f));
            }
            // hoja de la puerta, cerrada en el hueco
            string leaf = KitPiece(th, "hoja");
            if (leaf != null)
                RoomKit.Fit(walls, leaf, SideCenter(c, 2, SlabH) + SideDir[2] * 0.0f, SideDir[2], new Vector3(DoorOpen - 0.06f, DoorHgt, 0.05f));
            // columnas de las esquinas (marco madera, doradas en la etapa alta)
            Color trim = Island.Tier(level) >= 4 ? GoldC : WoodD;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0 ? -1 : 1) * (ModW * 0.5f - 0.06f), sz = (i < 2 ? -1 : 1) * (ModW * 0.5f - 0.06f);
                code.Box(c + new Vector3(sx, SlabH + h * 0.5f, sz), new Vector3(0.14f, h, 0.14f), trim, 0f);
            }
            // Plan Pueblo etapa 7, "mejorar = agregar adentro": cada evolucion le suma una sala al edificio, armada con
            // las mismas piezas. Etapa 2: un anexo atras con otra maquina; etapa 3: un piso arriba; etapa 4: ribetes y
            // remate dorados. El piso de arriba va con los techos (se disuelve al acercar y deja ver la planta baja).
            int tier = Island.Tier(level);
            var up = new MeshBuilder();
            if (tier >= 2) KitAnnex(code, walls, roofs, th);
            float topY = SlabH + h;
            if (tier >= 3)
            {
                up.Box(c + new Vector3(0, topY + 0.07f, 0), new Vector3(ModW + 0.04f, 0.14f, ModW + 0.04f), Wood, 0f);
                for (int side = 0; side < 4; side++)
                {
                    string f = KitPiece(th, side == 3 ? "pared" : "ventana") ?? KitPiece(th, "pared");
                    if (f != null) RoomKit.Fit(roofs, f, SideCenter(c, side, topY + 0.14f) - SideDir[side] * 0.06f, SideDir[side], new Vector3(ModW - 0.1f, h * 0.9f, 0f));
                }
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i % 2 == 0 ? -1 : 1) * (ModW * 0.5f - 0.06f), sz = (i < 2 ? -1 : 1) * (ModW * 0.5f - 0.06f);
                    up.Box(c + new Vector3(sx, topY + 0.14f + h * 0.45f, sz), new Vector3(0.14f, h * 0.9f, 0.14f), trim, trim == GoldC ? 0.2f : 0f);
                }
                // balcon al frente con baranda
                up.Box(c + new Vector3(0, topY + 0.1f, -ModW * 0.5f - 0.25f), new Vector3(ModW * 0.7f, 0.08f, 0.5f), WoodD, 0f);
                up.Box(c + new Vector3(0, topY + 0.38f, -ModW * 0.5f - 0.48f), new Vector3(ModW * 0.7f, 0.05f, 0.05f), trim, 0f);
                for (int i = 0; i < 5; i++) up.Box(c + new Vector3(-ModW * 0.33f + i * ModW * 0.165f, topY + 0.25f, -ModW * 0.5f - 0.48f), new Vector3(0.04f, 0.28f, 0.04f), trim, 0f);
                topY += 0.14f + h * 0.9f;
            }
            // techo comun + remate del tema (dorado en la ultima etapa)
            Vector3 top = c + Vector3.up * topY;
            RoomKit.Place(roofs, RoomKit.Common("techo_0"), top);
            RoomKit.Place(roofs, KitPiece(th, "remate"), top);
            if (tier >= 4)
            {
                up.Cyl(top + Vector3.up * 0.9f, top + Vector3.up * 1.6f, 0.03f, 0.03f, 6, GoldC, 0.3f);
                up.Box(top + new Vector3(0.22f, 1.48f, 0f), new Vector3(0.42f, 0.24f, 0.02f), GoldC, 0.35f);
            }
            // afuera: la pila de lo que produce, a un costado del frente
            bool pile;
            Color pc = KitPile(k, out pile);
            if (pile)
                for (int i = 0; i < 6; i++)
                    code.Octa(c + new Vector3(ModW * 0.5f + 0.35f + (i % 3) * 0.16f, 0.08f + (i / 3) * 0.12f, -0.55f + (i % 2) * 0.14f), Vector3.one * 0.2f, pc, k == BKind.CrystalWell || k == BKind.GemMine ? 0.4f : 0.05f);
            if (k == BKind.Barn)
                for (int i = 0; i < 3; i++) Crate(code, c + new Vector3(ModW * 0.5f + 0.35f, 0f, -0.6f + i * 0.38f), 0.3f);
            KitSignature(code, k, level);   // la pieza grande que dice que es (castillete, porton, toldo, vias...)

            var wr = Bake(code, parent, "Edificio", true);
            RoomKit.Bake(walls, wr.transform, "Paredes", true);
            // la maquina adentro (se ve con el techo abierto)
            string prop = KitProp(k);
            if (prop != "")
            {
                Material pm;
                var mesh = TripoModel(prop, out pm);
                if (mesh != null)
                {
                    var r = MakeRenderer(parent, "Maquina", mesh, new[] { pm }, false);
                    var b = mesh.bounds;
                    float foot = Mathf.Max(b.size.x, b.size.z, 0.01f);
                    float s = 1.1f / foot;
                    r.transform.localScale = Vector3.one * s;
                    r.transform.localPosition = new Vector3(-b.center.x * s, SlabH - b.min.y * s, -b.center.z * s);
                    if (tier >= 2)
                    {
                        // la segunda maquina, en el anexo
                        var r2 = MakeRenderer(parent, "Maquina2", mesh, new[] { pm }, false);
                        float s2 = 0.85f / foot;
                        r2.transform.localScale = Vector3.one * s2;
                        r2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                        r2.transform.localPosition = new Vector3(AnnexX + 0.4f - b.center.z * s2,   // en la parte que asoma, no detras de la sala
                             SlabH - b.min.y * s2, ModW * 0.5f + 0.6f + b.center.x * s2);
                    }
                }
            }
            var roofT = new GameObject("Techos").transform;
            roofT.SetParent(parent, false);
            RoomKit.Bake(roofs, roofT, "Techo", true);
            Bake(up, roofT, "Piso", true);
            return topY + 0.95f;
        }
    }
}
