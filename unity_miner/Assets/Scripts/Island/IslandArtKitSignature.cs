using Mineros.Core;
using Mineros.World;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Silueta propia de cada edificio del kit (autocritica de la corrida 11: "se parecen demasiado, solo cambian la puerta
    /// y el remate"). La sala es la misma arquitectura para todos; lo que dice QUE es cada uno, sin texto y a zoom lejano,
    /// es una pieza grande afuera: castillete con rueda en las minas, porton con columnas en el Banco, toldo a rayas en el
    /// Mercado, vias y tanque de agua en el Tren, chimeneas altas en los hornos, campanario en la Escuela, cupula en el
    /// Laboratorio, etc. El frente es -Z; la pila de lo producido ocupa el costado +X del frente, asi que las piezas altas
    /// van al costado -X o atras.
    /// </summary>
    public static partial class IslandArt
    {
        static void KitSignature(MeshBuilder mb, BKind k, int level)
        {
            float roofY = SlabH + WallH;
            switch (k)
            {
                case BKind.CoalMine: case BKind.IronMine: case BKind.CopperMine: case BKind.GoldMine: case BKind.GemMine:
                    Headframe(mb, new Vector3(-1.4f, 0f, 0.35f), 2.5f + 0.05f * level, k == BKind.GoldMine || k == BKind.GemMine ? GoldC : Iron);
                    FrontRails(mb, 1.0f);
                    bool has; Color load = KitPile(k, out has);
                    Cart(mb, new Vector3(-0.05f, 0f, -1.55f), load);
                    break;
                case BKind.SandPit:
                    for (int i = 0; i < 3; i++)
                        mb.Octa(new Vector3(-1.35f + i * 0.12f, 0.25f - i * 0.05f, 0.2f - i * 0.35f), new Vector3(0.9f - i * 0.18f, 0.55f - i * 0.12f, 0.9f - i * 0.18f), SandC, 0f);
                    mb.Cyl(new Vector3(-1.0f, 0.1f, -0.4f), new Vector3(-0.85f, 0.95f, -0.35f), 0.03f, 0.03f, 6, WoodD, 0f);   // pala clavada
                    mb.Box(new Vector3(-1.02f, 0.12f, -0.41f), new Vector3(0.16f, 0.2f, 0.03f), Iron, 0f);
                    break;
                case BKind.CrystalWell:
                    // pozo de piedra con manivela y cristales que brotan
                    Vector3 w = new Vector3(-1.35f, 0f, 0.1f);
                    mb.Cyl(w, w + Vector3.up * 0.45f, 0.48f, 0.48f, 12, Stone, 0f);
                    mb.Disc(w + Vector3.up * 0.46f, 0.36f, 12, H("2a6a8a"), 0.4f);
                    for (int i = 0; i < 2; i++) mb.Box(w + new Vector3(i == 0 ? -0.42f : 0.42f, 0.85f, 0), new Vector3(0.08f, 0.9f, 0.08f), WoodD, 0f);
                    mb.Cyl(w + new Vector3(-0.46f, 1.2f, 0), w + new Vector3(0.46f, 1.2f, 0), 0.05f, 0.05f, 6, Wood, 0f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.26f;
                        mb.Crystal(w + new Vector3(Mathf.Cos(a) * 0.22f, 0.4f, Mathf.Sin(a) * 0.22f), new Vector3(Mathf.Cos(a) * 0.35f, 1f, Mathf.Sin(a) * 0.35f).normalized, 0.6f + 0.12f * (i % 3), 0.12f, 5, OreCol[6], 0.6f);
                    }
                    break;
                case BKind.Foundry: case BKind.GlassKiln: case BKind.GoldRefinery:
                    Color brick = k == BKind.GlassKiln ? H("5c6a7a") : H("8a3a2a");
                    // desde el piso (adentro, en las esquinas de atras): con el techo abierto no quedan flotando
                    Chimney(mb, new Vector3(0.55f, SlabH, 0.55f), roofY - SlabH + 0.95f + 0.03f * level, brick, true);
                    Chimney(mb, new Vector3(-0.5f, SlabH, 0.55f), roofY - SlabH + 1.4f + 0.04f * level, brick, true);
                    // boca del horno brillando junto a la puerta
                    mb.Box(new Vector3(-1.2f, 0.35f, -0.3f), new Vector3(0.6f, 0.7f, 0.6f), brick, 0f);
                    mb.Box(new Vector3(-1.2f, 0.3f, -0.61f), new Vector3(0.3f, 0.26f, 0.02f), Ember, 1f);
                    break;
                case BKind.Sawmill:
                    // sierra circular gigante y troncos apilados
                    Gear(mb, new Vector3(-1.3f, 0.95f, -0.2f), 0.55f, Metal);
                    mb.Box(new Vector3(-1.3f, 0.2f, -0.12f), new Vector3(0.9f, 0.4f, 0.5f), WoodD, 0f);
                    for (int i = 0; i < 5; i++)
                    {
                        float y = 0.13f + (i < 3 ? 0 : 0.22f), x = -1.45f + (i < 3 ? i : i - 2.5f) * 0.26f;
                        mb.Cyl(new Vector3(x, y, 0.35f), new Vector3(x, y, 1.05f), 0.12f, 0.12f, 8, Wood, 0f);
                    }
                    break;
                case BKind.Workshop:
                    Gear(mb, new Vector3(0f, roofY + 0.15f, -ModW * 0.5f - 0.1f), 0.36f, Copper);   // engranaje cartel colgado de la pared del frente
                    mb.Box(new Vector3(-1.25f, 0.25f, -0.35f), new Vector3(0.25f, 0.5f, 0.25f), Iron, 0f);   // yunque
                    mb.Box(new Vector3(-1.25f, 0.55f, -0.35f), new Vector3(0.55f, 0.14f, 0.25f), Iron, 0f);
                    Barrel(mb, new Vector3(-1.3f, 0f, 0.4f));
                    break;
                case BKind.Market:
                    Awning(mb, -0.85f, 1.7f, roofY - 0.1f, -0.93f, H("e0533c"), Wall, 6);
                    for (int i = 0; i < 2; i++) mb.Cyl(new Vector3(i == 0 ? -0.8f : 0.8f, 0f, -1.45f), new Vector3(i == 0 ? -0.8f : 0.8f, roofY - 0.38f, -1.45f), 0.04f, 0.04f, 6, WoodD, 0f);
                    mb.Box(new Vector3(-1.35f, 0.35f, -0.6f), new Vector3(0.6f, 0.7f, 0.5f), Wood, 0f);   // puesto
                    for (int i = 0; i < 4; i++) mb.Octa(new Vector3(-1.5f + (i % 2) * 0.28f, 0.78f, -0.7f + (i / 2) * 0.2f), Vector3.one * 0.17f, i % 2 == 0 ? Copper : GoldC, 0.15f);
                    break;
                case BKind.Bank:
                    Portico(mb, roofY, GoldC);
                    break;
                case BKind.Jeweler:
                    // un diamante gigante girado en un pilar: la marca del joyero
                    mb.Cyl(new Vector3(-1.3f, 0f, -0.3f), new Vector3(-1.3f, 1.4f, -0.3f), 0.1f, 0.1f, 8, GoldC, 0.1f);
                    mb.Octa(new Vector3(-1.3f, 1.85f, -0.3f), new Vector3(0.55f, 0.75f, 0.55f), OreCol[4], 0.6f);
                    break;
                case BKind.Lapidary:
                    mb.Box(new Vector3(-1.3f, 0.4f, -0.2f), new Vector3(0.6f, 0.8f, 0.6f), Stone, 0f);
                    Gear(mb, new Vector3(-1.3f, 1.1f, -0.52f), 0.32f, Metal);   // muela de pulir
                    for (int i = 0; i < 3; i++) mb.Crystal(new Vector3(-1.45f + i * 0.15f, 0f, 0.45f), Vector3.up, 0.7f - i * 0.15f, 0.16f, 6, Ruby, 0.5f);
                    break;
                case BKind.Lab:
                    // observatorio: torre de piedra con cupula y telescopio
                    Vector3 t = new Vector3(-1.35f, 0f, 0.3f);
                    mb.Cyl(t, t + Vector3.up * 2.0f, 0.42f, 0.38f, 10, Stone, 0f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a0 = i / 5f * Mathf.PI * 0.5f, a1 = (i + 1) / 5f * Mathf.PI * 0.5f;
                        mb.Cyl(t + Vector3.up * (2.0f + Mathf.Sin(a0) * 0.45f), t + Vector3.up * (2.0f + Mathf.Sin(a1) * 0.45f), 0.45f * Mathf.Cos(a0) + 0.02f, 0.45f * Mathf.Cos(a1) + 0.02f, 12, Glass, 0.25f);
                    }
                    mb.Cyl(t + new Vector3(0, 2.25f, -0.2f), t + new Vector3(0.1f, 2.6f, -0.65f), 0.08f, 0.06f, 8, Copper, 0f);
                    break;
                case BKind.School: case BKind.Managers:
                    // campanario (Escuela) / torre del reloj (Gerentes)
                    Vector3 b = new Vector3(-1.35f, 0f, 0.35f);
                    mb.Box(b + Vector3.up * 1.0f, new Vector3(0.6f, 2.0f, 0.6f), k == BKind.School ? H("c86a4a") : Stone, 0f);
                    for (int i = 0; i < 4; i++) mb.Box(b + new Vector3(i % 2 == 0 ? -0.25f : 0.25f, 2.25f, i < 2 ? -0.25f : 0.25f), new Vector3(0.1f, 0.5f, 0.1f), WoodD, 0f);
                    if (k == BKind.School) mb.Cyl(b + Vector3.up * 2.35f, b + Vector3.up * 2.1f, 0.06f, 0.2f, 10, GoldC, 0.3f);   // campana
                    else { mb.Cyl(b + new Vector3(0, 1.6f, -0.29f), b + new Vector3(0, 1.6f, -0.32f), 0.22f, 0.22f, 14, Wall, 0.2f); mb.Box(b + new Vector3(0.05f, 1.66f, -0.33f), new Vector3(0.03f, 0.14f, 0.01f), Dark, 0f); }
                    mb.Cyl(b + Vector3.up * 2.5f, b + Vector3.up * 3.15f, 0.48f, 0.02f, 4, H("4a5260"), 0f);   // aguja
                    break;
                case BKind.Hospital:
                    // cartel con la cruz roja en un poste alto
                    mb.Cyl(new Vector3(-1.3f, 0f, -0.4f), new Vector3(-1.3f, 1.6f, -0.4f), 0.05f, 0.05f, 6, WoodD, 0f);
                    mb.Box(new Vector3(-1.3f, 1.85f, -0.4f), new Vector3(0.7f, 0.7f, 0.08f), Wall, 0f);
                    mb.Box(new Vector3(-1.3f, 1.85f, -0.45f), new Vector3(0.5f, 0.15f, 0.03f), H("e0453c"), 0.2f);
                    mb.Box(new Vector3(-1.3f, 1.85f, -0.45f), new Vector3(0.15f, 0.5f, 0.03f), H("e0453c"), 0.2f);
                    break;
                case BKind.Canteen:
                    Awning(mb, -0.85f, 1.7f, roofY - 0.1f, -0.93f, RoofGreenF, Wall, 6);
                    for (int i = 0; i < 2; i++)
                    {
                        float x = -1.4f, z = -0.55f + i * 0.75f;
                        mb.Box(new Vector3(x, 0.4f, z), new Vector3(0.55f, 0.06f, 0.4f), Wood, 0f);
                        mb.Box(new Vector3(x, 0.2f, z), new Vector3(0.08f, 0.4f, 0.08f), WoodD, 0f);
                        mb.Box(new Vector3(x, 0.22f, z - 0.32f), new Vector3(0.5f, 0.05f, 0.14f), WoodD, 0f);
                    }
                    break;
                case BKind.Train:
                    // via que pasa por el frente y tanque de agua sobre patas
                    for (int r = -1; r <= 1; r += 2) mb.Box(new Vector3(0f, 0.05f, -1.45f + r * 0.17f), new Vector3(3.6f, 0.05f, 0.05f), Iron, 0f);
                    for (int i = 0; i < 12; i++) mb.Box(new Vector3(-1.65f + i * 0.3f, 0.02f, -1.45f), new Vector3(0.08f, 0.03f, 0.5f), WoodD, 0f);
                    Vector3 wt = new Vector3(-1.4f, 0f, 0.4f);
                    for (int i = 0; i < 4; i++) mb.Box(wt + new Vector3(i % 2 == 0 ? -0.25f : 0.25f, 0.6f, i < 2 ? -0.25f : 0.25f), new Vector3(0.07f, 1.2f, 0.07f), WoodD, 0f);
                    mb.Cyl(wt + Vector3.up * 1.2f, wt + Vector3.up * 1.85f, 0.42f, 0.42f, 12, Wood, 0f);
                    mb.Cyl(wt + Vector3.up * 1.85f, wt + Vector3.up * 2.15f, 0.46f, 0.05f, 12, RoofRedF, 0f);
                    break;
                case BKind.Airport:
                    // pista con la H y manga de viento
                    mb.Disc(new Vector3(-1.3f, 0.03f, -0.4f), 0.6f, 16, StoneD, 0f);
                    mb.Box(new Vector3(-1.45f, 0.04f, -0.4f), new Vector3(0.07f, 0.01f, 0.5f), Wall, 0f);
                    mb.Box(new Vector3(-1.15f, 0.04f, -0.4f), new Vector3(0.07f, 0.01f, 0.5f), Wall, 0f);
                    mb.Box(new Vector3(-1.3f, 0.04f, -0.4f), new Vector3(0.3f, 0.01f, 0.07f), Wall, 0f);
                    mb.Cyl(new Vector3(-1.35f, 0f, 0.55f), new Vector3(-1.35f, 2.1f, 0.55f), 0.04f, 0.04f, 6, Metal, 0f);
                    mb.Cyl(new Vector3(-1.35f, 2.0f, 0.55f), new Vector3(-0.85f, 1.92f, 0.55f), 0.13f, 0.06f, 8, H("ff8a2e"), 0.2f);
                    break;
                case BKind.Barn:
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(-1.35f + (i % 2) * 0.05f, 0.2f + (i / 2) * 0.38f, -0.35f + (i % 2) * 0.62f - (i / 2) * 0.3f), new Vector3(0.55f, 0.38f, 0.55f), H("e8c25a"), 0f);   // fardos
                    break;
                case BKind.Barracks:
                    // icono del Cuartel: la torre del salon central (faroles y bandera roja) y la boca con vias y vagoneta
                    Vector3 tw = new Vector3(-1.3f, 0f, 0.3f);
                    mb.Box(tw + Vector3.up * 1.25f, new Vector3(0.75f, 2.5f, 0.75f), Stone, 0f);
                    for (int i = 0; i < 2; i++) mb.Box(tw + new Vector3(i == 0 ? -0.18f : 0.18f, 1.9f, -0.38f), new Vector3(0.16f, 0.28f, 0.02f), Ember, 0.9f);
                    mb.Cyl(tw + Vector3.up * 2.5f, tw + Vector3.up * 3.15f, 0.6f, 0.02f, 4, RoofRedF, 0f);
                    mb.Cyl(tw + Vector3.up * 3.05f, tw + Vector3.up * 3.75f, 0.03f, 0.03f, 6, WoodD, 0f);
                    mb.Box(tw + new Vector3(0.22f, 3.6f, 0f), new Vector3(0.42f, 0.24f, 0.02f), H("e0453c"), 0.15f);
                    FrontRails(mb, 1.0f);
                    Cart(mb, new Vector3(-0.05f, 0f, -1.55f), Copper);
                    break;
                case BKind.Warehouse:
                    // grua de carga con el gancho y cajones apilados
                    Vector3 g = new Vector3(-1.35f, 0f, 0.4f);
                    mb.Box(g + Vector3.up * 1.2f, new Vector3(0.14f, 2.4f, 0.14f), WoodD, 0f);
                    mb.Box(g + new Vector3(0.35f, 2.35f, -0.35f), new Vector3(0.1f, 0.1f, 1.1f), WoodD, 0f);
                    mb.Cyl(g + new Vector3(0.35f, 2.3f, -0.85f), g + new Vector3(0.35f, 1.5f, -0.85f), 0.015f, 0.015f, 4, Dark, 0f);
                    Crate(mb, g + new Vector3(0.35f, 1.15f, -0.85f), 0.3f);
                    for (int i = 0; i < 3; i++) Crate(mb, new Vector3(-1.45f + (i % 2) * 0.36f, (i / 2) * 0.34f, -0.5f), 0.34f);
                    break;
            }
        }

        /// <summary>Castillete de mina: torre de vigas en A con la rueda de la jaula arriba.</summary>
        static void Headframe(MeshBuilder mb, Vector3 b, float h, Color wheel)
        {
            float s = 0.42f;
            Vector3 top = b + Vector3.up * h;
            for (int i = 0; i < 4; i++)
            {
                Vector3 foot = b + new Vector3(i % 2 == 0 ? -s : s, 0f, i < 2 ? -s : s);
                mb.Cyl(foot, top + new Vector3((foot.x - b.x) * 0.25f, 0f, (foot.z - b.z) * 0.25f), 0.06f, 0.05f, 6, WoodD, 0f);
            }
            for (int j = 1; j <= 2; j++)
            {
                float y = h * j / 3f, k = 1f - 0.75f * j / 3f;
                mb.Box(b + new Vector3(0, y, -s * k), new Vector3(2f * s * k, 0.06f, 0.06f), Wood, 0f);
                mb.Box(b + new Vector3(0, y, s * k), new Vector3(2f * s * k, 0.06f, 0.06f), Wood, 0f);
            }
            Gear(mb, top + new Vector3(0, 0.15f, -0.12f), 0.36f, wheel);
            mb.Cyl(top + new Vector3(0, 0.15f, -0.2f), b + new Vector3(0, 0.3f, -0.2f), 0.012f, 0.012f, 4, Dark, 0f);   // cable de la jaula
        }

        /// <summary>Vias desde la puerta hacia afuera.</summary>
        static void FrontRails(MeshBuilder mb, float len)
        {
            float z0 = -ModW * 0.5f, zm = z0 - len * 0.5f;
            for (int r = -1; r <= 1; r += 2) mb.Box(new Vector3(r * 0.17f, 0.05f, zm), new Vector3(0.05f, 0.05f, len), Iron, 0f);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.28f));
            for (int i = 0; i < n; i++) mb.Box(new Vector3(0f, 0.02f, z0 - (i + 0.5f) * len / n), new Vector3(0.5f, 0.03f, 0.08f), WoodD, 0f);
        }

        /// <summary>Porton de banco: escalinata, cuatro columnas y fronton dorado delante de la puerta.</summary>
        static void Portico(MeshBuilder mb, float h, Color trim)
        {
            float z = -ModW * 0.5f - 0.38f, w = ModW + 0.1f;
            mb.Box(new Vector3(0, 0.06f, z), new Vector3(w + 0.2f, 0.12f, 0.8f), Wall, 0f);
            for (int i = 0; i < 4; i++)
            {
                float x = -w * 0.5f + 0.15f + i * (w - 0.3f) / 3f;
                mb.Cyl(new Vector3(x, 0.12f, z - 0.15f), new Vector3(x, h, z - 0.15f), 0.08f, 0.07f, 10, Wall, 0f);
            }
            mb.Box(new Vector3(0, h + 0.07f, z + 0.05f), new Vector3(w + 0.1f, 0.14f, 0.85f), trim, 0.15f);
            Vector3 l = new Vector3(-w * 0.5f - 0.05f, h + 0.14f, z - 0.38f), r = new Vector3(w * 0.5f + 0.05f, h + 0.14f, z - 0.38f), p = new Vector3(0, h + 0.62f, z - 0.38f);
            mb.Tri(l, p, r, Vector3.back, Wall, 0f);
            Vector3 l2 = l + Vector3.forward * 0.85f, r2 = r + Vector3.forward * 0.85f, p2 = p + Vector3.forward * 0.85f;
            mb.Quad(l, p, p2, l2, new Vector3(-1, 1, 0), trim, 0.1f);
            mb.Quad(p, r, r2, p2, new Vector3(1, 1, 0), trim, 0.1f);
            mb.Cyl(new Vector3(0, h + 0.33f, z - 0.37f), new Vector3(0, h + 0.33f, z - 0.41f), 0.14f, 0.14f, 12, GoldC, 0.5f);   // moneda en el fronton
        }
    }
}
