using Mineros.Core;
using Mineros.World;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Edificios de la ciudad (0.9) dibujados por codigo, con la paleta y las reglas de la biblia de produccion (A.1-A.3):
    /// base de piedra, paredes crema, techo del color de la familia (ocre = extraccion, verde = transformacion,
    /// rojo = pueblo, azul = comercio, oro = especiales) y UN elemento caracteristico grande que se lee de lejos. Los
    /// modelos de Tripo, cuando existen en Resources/Island, los reemplazan.
    /// </summary>
    public static partial class IslandArt
    {
        public static readonly Color RoofOchre = H("d9a441"), RoofGreenF = H("5baa5e"), RoofRedF = H("d9533c"), RoofBlueF = H("4a82c9"), RoofGold = H("f2c230");
        static readonly Color Metal = H("8e9aa8"), Iron = H("4b4f57"), Ember = H("ff7a2e"), GemC = H("36c6d9"), Ruby = H("e04a6a"), Violet = H("9a5bd9");
        static readonly Color Coal = H("2f3036"), Copper = H("e0884a"), GoldC = H("f2c230"), SandC = H("ecd28e"), Leaf = H("6dbb4a");

        /// <summary>Color de techo segun la familia del edificio.</summary>
        public static Color FamilyRoof(BKind k)
        {
            switch (Island.Def(k).Family)
            {
                case BFamily.Extraction: return RoofOchre;
                case BFamily.Transform: return RoofGreenF;
                case BFamily.Town: return RoofRedF;
                case BFamily.Commerce: return RoofBlueF;
                default: return RoofGold;
            }
        }

        static void City(MeshBuilder mb, BKind k, int lv)
        {
            Color roof = FamilyRoof(k);
            switch (k)
            {
                case BKind.Barn: Barn(mb, lv, roof); break;
                case BKind.Warehouse: Warehouse(mb, lv, roof); break;
                case BKind.CoalMine: MineMouth(mb, lv, roof, Coal, false); break;
                case BKind.IronMine: MineMouth(mb, lv, roof, OreCol[2], true); break;
                case BKind.CopperMine: MineMouth(mb, lv, roof, Copper, false); break;
                case BKind.GoldMine: MineMouth(mb, lv, roof, GoldC, true); break;
                case BKind.GemMine: MineMouth(mb, lv, roof, Violet, true); break;
                case BKind.Sawmill: Sawmill(mb, lv, roof); break;
                case BKind.SandPit: SandPit(mb, lv, roof); break;
                case BKind.CrystalWell: CrystalWell(mb, lv, roof); break;
                case BKind.Foundry: Foundry(mb, lv, roof); break;
                case BKind.GlassKiln: GlassKiln(mb, lv, roof); break;
                case BKind.Workshop: Workshop(mb, lv, roof); break;
                case BKind.GoldRefinery: Refinery(mb, lv, roof); break;
                case BKind.Lapidary: Lapidary(mb, lv, roof); break;
                case BKind.Jeweler: Jeweler(mb, lv, roof); break;
                case BKind.Lab: Lab(mb, lv, roof); break;
                case BKind.Market: Market(mb, lv, roof); break;
                case BKind.Train: Station(mb, lv, roof); break;
                case BKind.Bank: Bank(mb, lv, roof); break;
                case BKind.School: School(mb, lv, roof); break;
                case BKind.Managers: Office(mb, lv, roof); break;
                case BKind.Hospital: Hospital(mb, lv, roof); break;
                case BKind.Airport: Airport(mb, lv, roof); break;
                case BKind.Barracks: Barracks(mb, lv, roof); break;
                default: Office(mb, lv, roof); break;
            }
            LevelPennants(mb, lv, 1.3f);
        }

        // ------------------------------------------------------------ piezas comunes
        static void Plinth(MeshBuilder mb, float w, float d) { mb.Box(new Vector3(0, 0.08f, 0), new Vector3(w + 0.3f, 0.16f, d + 0.3f), Stone, 0f); }

        static void Body(MeshBuilder mb, float w, float h, float d, Color col, float z = 0f)
        {
            mb.Box(new Vector3(0, 0.16f + h * 0.5f, z), new Vector3(w, h, d), col, 0f);
        }

        static void Door(MeshBuilder mb, float x, float d, float h = 0.84f)
        {
            mb.Box(new Vector3(x, 0.16f + h * 0.5f, -d * 0.5f - 0.02f), new Vector3(0.5f, h, 0.08f), WoodD, 0f);
        }

        /// <summary>Banderines de nivel en un mastil al costado (uno por cada 2 niveles, hasta 6).</summary>
        static void LevelPennants(MeshBuilder mb, int lv, float x)
        {
            int n = Mathf.Clamp((lv + 1) / 2, 1, 6);
            Vector3 b = new Vector3(x, 0f, -0.9f);
            mb.Cyl(b, b + Vector3.up * (1.2f + n * 0.12f), 0.04f, 0.035f, 6, WoodD, 0f);
            for (int i = 0; i < n; i++)
                mb.Box(b + new Vector3(0.12f, 1.12f + i * 0.12f, 0f), new Vector3(0.2f, 0.09f, 0.02f), i % 2 == 0 ? Kit3.Yellow : Kit3.Blue, 0.1f);
        }

        static void Crate(MeshBuilder mb, Vector3 c, float s) { mb.Box(c + Vector3.up * s * 0.5f, Vector3.one * s, Wood, 0f); mb.Box(c + Vector3.up * s * 0.5f, new Vector3(s * 1.02f, s * 0.14f, s * 1.02f), WoodD, 0f); }

        static void Barrel(MeshBuilder mb, Vector3 c) { mb.Cyl(c, c + Vector3.up * 0.55f, 0.22f, 0.22f, 10, WoodD, 0f); mb.Cyl(c + Vector3.up * 0.25f, c + Vector3.up * 0.31f, 0.23f, 0.23f, 10, Iron, 0f); }

        static void Cart(MeshBuilder mb, Vector3 c, Color load)
        {
            mb.Box(c + new Vector3(0, 0.32f, 0), new Vector3(0.62f, 0.32f, 0.45f), Iron, 0f);
            for (int i = 0; i < 4; i++) mb.Cyl(c + new Vector3(i < 2 ? -0.22f : 0.22f, 0.1f, i % 2 == 0 ? -0.24f : 0.24f) + Vector3.forward * 0f,
                c + new Vector3(i < 2 ? -0.22f : 0.22f, 0.1f, i % 2 == 0 ? -0.27f : 0.27f), 0.1f, 0.1f, 8, Dark, 0f);
            for (int i = 0; i < 4; i++) mb.Octa(c + new Vector3(-0.15f + (i % 2) * 0.3f, 0.52f + (i / 2) * 0.08f, -0.08f + (i / 2) * 0.12f), Vector3.one * 0.2f, load, load == GoldC || load == Violet ? 0.4f : 0.05f);
        }

        static void Chimney(MeshBuilder mb, Vector3 b, float h, Color col, bool glow)
        {
            mb.Box(b + Vector3.up * h * 0.5f, new Vector3(0.36f, h, 0.36f), col, 0f);
            mb.Box(b + Vector3.up * (h + 0.04f), new Vector3(0.44f, 0.1f, 0.44f), Color.Lerp(col, Dark, 0.3f), 0f);
            if (glow) mb.Box(b + Vector3.up * (h + 0.1f), new Vector3(0.24f, 0.04f, 0.24f), Ember, 1f);
        }

        static void Gear(MeshBuilder mb, Vector3 c, float r, Color col)
        {
            mb.Cyl(c, c + new Vector3(0, 0, -0.08f), r, r, 14, col, 0f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                mb.Box(c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -0.04f), new Vector3(r * 0.32f, r * 0.32f, 0.08f), col, 0f);
            }
            mb.Cyl(c + new Vector3(0, 0, -0.08f), c + new Vector3(0, 0, -0.12f), r * 0.3f, r * 0.3f, 10, Iron, 0f);
        }

        static void Awning(MeshBuilder mb, float x0, float w, float y, float z, Color a, Color b, int stripes)
        {
            for (int i = 0; i < stripes; i++)
            {
                float xa = x0 + i * w / stripes, xb = xa + w / stripes;
                mb.Quad(new Vector3(xa, y, z), new Vector3(xb, y, z), new Vector3(xb, y - 0.3f, z - 0.55f), new Vector3(xa, y - 0.3f, z - 0.55f), new Vector3(0, 1, -1), i % 2 == 0 ? a : b, 0f);
            }
        }

        // ------------------------------------------------------------ pueblo
        static void Barn(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.6f, d = 2.0f, h = 1.3f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, Wood);
            for (int i = -2; i <= 2; i++) mb.Box(new Vector3(i * 0.6f, 0.16f + h * 0.5f, -d * 0.5f - 0.01f), new Vector3(0.05f, h, 0.03f), WoodD, 0f);
            // porton con cruz
            mb.Box(new Vector3(0, 0.71f, -d * 0.5f - 0.04f), new Vector3(1.1f, 1.1f, 0.06f), Wall, 0f);
            mb.Box(new Vector3(0, 0.71f, -d * 0.5f - 0.08f), new Vector3(0.08f, 1.5f, 0.03f), WoodD, 0f);
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 1.1f, roof);
            mb.Box(new Vector3(0, 0.16f + h + 0.55f, -d * 0.5f - 0.1f), new Vector3(0.45f, 0.35f, 0.05f), Wall, 0f);   // ventanita del altillo
            for (int i = 0; i < Mathf.Min(1 + lv / 3, 4); i++) mb.Blob(new Vector3(-w * 0.5f - 0.35f, 0.2f + i * 0.02f, -0.6f + i * 0.4f), new Vector3(0.3f, 0.25f, 0.3f), 1, 5 + i, 0.08f, H("c9a25a"), H("e8c77a"), 0f, -1f);
        }

        static void Warehouse(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.5f, d = 1.9f, h = 1.25f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, Wall);
            mb.Box(new Vector3(0, 0.16f + h * 0.22f, 0), new Vector3(w + 0.04f, h * 0.44f, d + 0.04f), Stone, 0f);   // zocalo
            mb.Box(new Vector3(0, 0.66f, -d * 0.5f - 0.03f), new Vector3(1.2f, 1.0f, 0.06f), RoofBlue, 0f);       // porton de chapa
            for (int i = 0; i < 5; i++) mb.Box(new Vector3(0, 0.25f + i * 0.2f, -d * 0.5f - 0.07f), new Vector3(1.2f, 0.03f, 0.02f), H("2f6fb8"), 0f);
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.7f, roof);
            int crates = Mathf.Min(2 + lv / 2, 6);
            for (int i = 0; i < crates; i++) Crate(mb, new Vector3(w * 0.5f + 0.35f, (i / 3) * 0.42f, -0.7f + (i % 3) * 0.48f), 0.42f);
        }

        static void School(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.4f, d = 1.8f, h = 1.2f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, Wall);
            Door(mb, 0f, d);
            Window(mb, new Vector3(-0.75f, 0.75f, -d * 0.5f)); Window(mb, new Vector3(0.75f, 0.75f, -d * 0.5f));
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.75f, roof);
            // campanario
            Vector3 t = new Vector3(0, 0.16f + h + 0.5f, 0.1f);
            mb.Box(t + Vector3.up * 0.35f, new Vector3(0.55f, 0.7f, 0.55f), Wall, 0f);
            mb.Cyl(t + Vector3.up * 0.35f, t + Vector3.up * 0.15f, 0.12f, 0.2f, 10, GoldC, 0.3f);
            mb.Cyl(t + Vector3.up * 0.7f, t + Vector3.up * 1.15f, 0.42f, 0.02f, 4, roof, 0f);
            mb.Box(new Vector3(-w * 0.5f - 0.3f, 0.75f, -0.3f), new Vector3(0.06f, 0.6f, 0.8f), H("2f4a3a"), 0f);   // pizarron
        }

        static void Office(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.0f, d = 1.7f, h = 1.0f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, Wall);
            Body(mb, w * 0.9f, h * 0.9f, d * 0.9f, Color.Lerp(Wall, WallD, 0.3f));
            mb.Box(new Vector3(0, 0.16f + h + h * 0.45f, 0), new Vector3(w * 0.9f, h * 0.9f, d * 0.9f), Color.Lerp(Wall, WallD, 0.3f), 0f);
            Door(mb, 0f, d);
            for (int f = 0; f < 2; f++) { Window(mb, new Vector3(-0.55f, 0.75f + f * h, -d * 0.5f + (f == 1 ? 0.09f : 0f))); Window(mb, new Vector3(0.55f, 0.75f + f * h, -d * 0.5f + (f == 1 ? 0.09f : 0f))); }
            Roof(mb, new Vector3(0, 0.16f + h * 1.9f, 0), w * 0.9f, d * 0.9f, 0.7f, roof);
            mb.Box(new Vector3(0, 0.16f + h + 0.05f, -d * 0.5f - 0.06f), new Vector3(1.3f, 0.26f, 0.05f), Kit3.Blue, 0.05f);   // cartel "Gerencia"
            Barrel(mb, new Vector3(-w * 0.5f - 0.3f, 0f, -0.5f));
        }

        static void Hospital(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.4f, d = 1.8f, h = 1.3f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, H("fbfbf7"));
            Door(mb, 0f, d, 0.9f);
            Window(mb, new Vector3(-0.8f, 0.8f, -d * 0.5f)); Window(mb, new Vector3(0.8f, 0.8f, -d * 0.5f));
            mb.Box(new Vector3(0, 0.16f + h + 0.06f, 0), new Vector3(w + 0.2f, 0.12f, d + 0.2f), roof, 0f);
            // cruz grande arriba (se lee de lejos)
            Vector3 c = new Vector3(0, 0.16f + h + 0.55f, 0);
            mb.Box(c, new Vector3(0.75f, 0.24f, 0.18f), Ruby, 0.5f);
            mb.Box(c, new Vector3(0.24f, 0.75f, 0.18f), Ruby, 0.5f);
        }

        static void Bank(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.4f, d = 1.8f, h = 1.2f;
            Plinth(mb, w, d);
            mb.Box(new Vector3(0, 0.24f, -0.05f), new Vector3(w + 0.1f, 0.16f, d + 0.2f), Stone, 0f);   // escalones
            Body(mb, w * 0.9f, h, d * 0.8f, Wall, 0.15f);
            for (int i = 0; i < 4; i++) mb.Cyl(new Vector3(-0.9f + i * 0.6f, 0.32f, -d * 0.5f + 0.05f), new Vector3(-0.9f + i * 0.6f, 0.16f + h, -d * 0.5f + 0.05f), 0.11f, 0.11f, 10, H("fbf6ea"), 0f);
            mb.Box(new Vector3(0, 0.16f + h + 0.06f, 0), new Vector3(w + 0.1f, 0.14f, d + 0.1f), WallD, 0f);
            Roof(mb, new Vector3(0, 0.16f + h + 0.12f, 0), w, d, 0.55f, roof);
            mb.Cyl(new Vector3(0, 0.16f + h + 0.3f, -d * 0.5f - 0.14f), new Vector3(0, 0.16f + h + 0.3f, -d * 0.5f - 0.2f), 0.2f, 0.2f, 16, GoldC, 0.4f);   // moneda
        }

        // ------------------------------------------------------------ extraccion
        /// <summary>Boca de mina: loma de roca con marco de madera, rieles y un carrito con el mineral de su color.</summary>
        static void MineMouth(MeshBuilder mb, int lv, Color roof, Color ore, bool tower)
        {
            mb.Blob(new Vector3(0.2f, 0f, 0.35f), new Vector3(1.5f, 1.15f, 1.15f), 1, 17, 0.12f, StoneD, Stone, 0f, 0f);
            // vetas del mineral asomando en la loma
            for (int i = 0; i < 4; i++)
            {
                float a = -0.6f + i * 0.5f;
                mb.Crystal(new Vector3(Mathf.Sin(a) * 1.15f + 0.2f, 0.55f + (i % 2) * 0.25f, 0.35f + Mathf.Cos(a) * 0.4f - 0.6f), new Vector3(Mathf.Sin(a), 0.6f, -0.6f).normalized, 0.3f, 0.12f, 5, ore, ore == GoldC || ore == Violet ? 0.5f : 0.1f);
            }
            // marco y boca oscura
            mb.Box(new Vector3(-0.15f, 0.55f, -0.55f), new Vector3(0.75f, 0.9f, 0.12f), Dark, 0f);
            mb.Box(new Vector3(-0.55f, 0.55f, -0.62f), new Vector3(0.14f, 1.0f, 0.14f), Wood, 0f);
            mb.Box(new Vector3(0.25f, 0.55f, -0.62f), new Vector3(0.14f, 1.0f, 0.14f), Wood, 0f);
            mb.Box(new Vector3(-0.15f, 1.1f, -0.62f), new Vector3(1.0f, 0.16f, 0.18f), Wood, 0f);
            mb.Box(new Vector3(-0.15f, 1.3f, -0.66f), new Vector3(0.7f, 0.22f, 0.05f), roof, 0f);   // cartel del color de la familia
            // rieles hacia afuera
            for (int s = -1; s <= 1; s += 2) mb.Box(new Vector3(-0.15f + s * 0.2f, 0.03f, -1.15f), new Vector3(0.05f, 0.04f, 1.2f), Iron, 0f);
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.15f, 0.02f, -0.7f - i * 0.3f), new Vector3(0.6f, 0.03f, 0.1f), WoodD, 0f);
            Cart(mb, new Vector3(-0.15f, 0.02f, -1.35f), ore);
            Vector3 lamp = new Vector3(0.45f, 0f, -0.8f);
            mb.Cyl(lamp, lamp + Vector3.up * 0.9f, 0.03f, 0.03f, 6, WoodD, 0f);
            mb.Box(lamp + Vector3.up * 0.95f, Vector3.one * 0.14f, H("ffe28a"), 0.9f);
            if (tower)
            {
                // castillete con rueda (minas profundas)
                Vector3 b = new Vector3(0.9f, 0f, 0.6f);
                mb.Cyl(b + new Vector3(-0.3f, 0, 0), b + new Vector3(0, 1.9f, 0), 0.05f, 0.05f, 6, Wood, 0f);
                mb.Cyl(b + new Vector3(0.3f, 0, 0), b + new Vector3(0, 1.9f, 0), 0.05f, 0.05f, 6, Wood, 0f);
                mb.Cyl(b + new Vector3(0, 1.9f, 0.05f), b + new Vector3(0, 1.9f, -0.05f), 0.3f, 0.3f, 14, Iron, 0f);
                mb.Cyl(b + new Vector3(0, 1.9f, -0.05f), b + new Vector3(0, 1.9f, -0.08f), 0.1f, 0.1f, 8, roof, 0f);
            }
        }

        static void Sawmill(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.4f, d = 1.7f;
            Plinth(mb, w, d);
            // galpon abierto sobre postes
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(i < 2 ? -1.0f : 1.0f, 0.75f, i % 2 == 0 ? -0.7f : 0.7f), new Vector3(0.14f, 1.2f, 0.14f), Wood, 0f);
            Roof(mb, new Vector3(0, 1.35f, 0), 2.1f, 1.5f, 0.6f, roof);
            // mesa y sierra circular grande
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(1.6f, 0.12f, 0.6f), WoodD, 0f);
            mb.Cyl(new Vector3(0.05f, 0.82f, -0.04f), new Vector3(0.05f, 0.82f, 0.04f), 0.42f, 0.42f, 18, Metal, 0.05f);
            mb.Cyl(new Vector3(0.05f, 0.82f, -0.06f), new Vector3(0.05f, 0.82f, 0.06f), 0.1f, 0.1f, 8, Iron, 0f);
            // troncos y tablas
            for (int i = 0; i < 3; i++) mb.Cyl(new Vector3(-w * 0.5f - 0.1f, 0.2f + (i == 2 ? 0.34f : 0f), -0.55f + (i % 2) * 0.4f + (i == 2 ? 0.2f : 0f)),
                new Vector3(-w * 0.5f + 0.9f, 0.2f + (i == 2 ? 0.34f : 0f), -0.55f + (i % 2) * 0.4f + (i == 2 ? 0.2f : 0f)), 0.18f, 0.18f, 10, Wood, 0f);
            int planks = Mathf.Min(2 + lv / 2, 6);
            for (int i = 0; i < planks; i++) mb.Box(new Vector3(w * 0.5f + 0.2f, 0.06f + i * 0.08f, 0f), new Vector3(0.36f, 0.07f, 1.2f), Color.Lerp(Wood, Wall, 0.35f), 0f);
        }

        static void SandPit(MeshBuilder mb, int lv, Color roof)
        {
            mb.Disc(new Vector3(0, 0.03f, 0.2f), 1.45f, 22, SandC, 0f);
            mb.Cyl(new Vector3(0.5f, 0.03f, 0.4f), new Vector3(0.5f, 0.85f, 0.4f), 0.75f, 0.06f, 16, H("f3dc9a"), 0f);   // pila conica
            // grua de madera con balde
            Vector3 b = new Vector3(-0.8f, 0f, -0.2f);
            mb.Box(b + Vector3.up * 0.9f, new Vector3(0.16f, 1.8f, 0.16f), Wood, 0f);
            mb.Box(b + new Vector3(0.6f, 1.75f, 0f), new Vector3(1.3f, 0.12f, 0.12f), Wood, 0f);
            mb.Cyl(b + new Vector3(1.2f, 1.7f, 0f), b + new Vector3(1.2f, 1.1f, 0f), 0.01f, 0.01f, 4, Dark, 0f);
            mb.Cyl(b + new Vector3(1.2f, 1.1f, 0f), b + new Vector3(1.2f, 0.8f, 0f), 0.2f, 0.14f, 10, Iron, 0f);
            // casilla
            mb.Box(new Vector3(-0.9f, 0.35f, 0.9f), new Vector3(0.8f, 0.7f, 0.6f), Wall, 0f);
            Roof(mb, new Vector3(-0.9f, 0.7f, 0.9f), 0.8f, 0.6f, 0.35f, roof);
        }

        static void CrystalWell(MeshBuilder mb, int lv, Color roof)
        {
            mb.Cyl(new Vector3(0, 0f, 0), new Vector3(0, 0.6f, 0), 0.85f, 0.85f, 18, Stone, 0f);
            mb.Cyl(new Vector3(0, 0.6f, 0), new Vector3(0, 0.62f, 0), 0.68f, 0.68f, 18, H("1b3a5c"), 0.2f);
            for (int s = -1; s <= 1; s += 2) mb.Box(new Vector3(s * 0.75f, 1.1f, 0f), new Vector3(0.12f, 1.0f, 0.12f), Wood, 0f);
            Roof(mb, new Vector3(0, 1.6f, 0), 1.7f, 1.1f, 0.5f, roof);
            mb.Cyl(new Vector3(-0.7f, 1.35f, 0), new Vector3(0.7f, 1.35f, 0), 0.06f, 0.06f, 8, WoodD, 0f);
            // cristales flotando sobre el pozo
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f;
                mb.Crystal(new Vector3(Mathf.Cos(a) * 0.3f, 0.9f + i * 0.08f, Mathf.Sin(a) * 0.3f), Vector3.up, 0.38f, 0.13f, 6, GemC, 0.8f);
            }
        }

        // ------------------------------------------------------------ transformacion
        static void Foundry(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.4f, d = 1.8f, h = 1.2f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, Stone);
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.65f, roof);
            Chimney(mb, new Vector3(0.75f, 0.16f + h, 0.3f), 1.6f, H("a5553f"), true);
            // boca del horno encendida
            mb.Box(new Vector3(-0.35f, 0.6f, -d * 0.5f - 0.02f), new Vector3(0.8f, 0.6f, 0.06f), Ember, 1f);
            mb.Box(new Vector3(-0.35f, 0.95f, -d * 0.5f - 0.04f), new Vector3(1.0f, 0.12f, 0.08f), Iron, 0f);
            // crisol y moldes con lingotes
            Vector3 c = new Vector3(0.6f, 0f, -d * 0.5f - 0.6f);
            mb.Cyl(c, c + Vector3.up * 0.45f, 0.28f, 0.32f, 12, Iron, 0f);
            mb.Cyl(c + Vector3.up * 0.45f, c + Vector3.up * 0.47f, 0.24f, 0.24f, 12, Ember, 1f);
            for (int i = 0; i < Mathf.Min(1 + lv / 3, 4); i++) mb.Box(new Vector3(-0.8f + i * 0.32f, 0.08f, -d * 0.5f - 0.6f), new Vector3(0.26f, 0.12f, 0.14f), Metal, 0.1f);
        }

        static void GlassKiln(MeshBuilder mb, int lv, Color roof)
        {
            Plinth(mb, 2.2f, 1.8f);
            mb.Blob(new Vector3(0, 0.16f, 0.1f), new Vector3(1.0f, 1.0f, 0.9f), 1, 23, 0.04f, H("b9653f"), H("d7845a"), 0f, 0f);   // horno cupula de ladrillo
            mb.Box(new Vector3(0, 0.55f, -0.75f), new Vector3(0.6f, 0.45f, 0.1f), Ember, 1f);
            Chimney(mb, new Vector3(0.1f, 0.95f, 0.3f), 0.9f, H("8d4b33"), false);
            // botellas de vidrio en un estante
            mb.Box(new Vector3(1.15f, 0.45f, -0.3f), new Vector3(0.3f, 0.06f, 1.0f), Wood, 0f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 b = new Vector3(1.15f, 0.48f, -0.7f + i * 0.27f);
                mb.Cyl(b, b + Vector3.up * 0.3f, 0.09f, 0.09f, 10, GemC, 0.35f);
                mb.Cyl(b + Vector3.up * 0.3f, b + Vector3.up * 0.42f, 0.04f, 0.04f, 8, GemC, 0.35f);
            }
            mb.Box(new Vector3(-1.0f, 0.45f, 0.5f), new Vector3(0.6f, 0.6f, 0.6f), Wall, 0f);
            Roof(mb, new Vector3(-1.0f, 0.75f, 0.5f), 0.6f, 0.6f, 0.35f, roof);
        }

        static void Workshop(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.4f, d = 1.8f, h = 1.3f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, Wall);
            Door(mb, 0.6f, d, 0.95f);
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.7f, roof);
            // engranajes grandes en la fachada
            Gear(mb, new Vector3(-0.45f, 0.95f, -d * 0.5f - 0.02f), 0.38f, Metal);
            Gear(mb, new Vector3(-0.05f, 0.55f, -d * 0.5f - 0.06f), 0.24f, Copper);
            Chimney(mb, new Vector3(-0.8f, 0.16f + h, 0.4f), 0.8f, Iron, false);
            Crate(mb, new Vector3(w * 0.5f + 0.35f, 0f, -0.5f), 0.4f);
        }

        static void Refinery(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.4f, d = 1.8f, h = 1.2f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, Stone);
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.65f, roof);
            Chimney(mb, new Vector3(0.8f, 0.16f + h, 0.2f), 1.3f, StoneD, true);
            // canaleta de oro brillante que sale del frente
            mb.Box(new Vector3(-0.3f, 0.7f, -d * 0.5f - 0.45f), new Vector3(0.3f, 0.08f, 0.9f), Iron, 0f);
            mb.Box(new Vector3(-0.3f, 0.75f, -d * 0.5f - 0.45f), new Vector3(0.2f, 0.03f, 0.88f), GoldC, 1f);
            int bars = Mathf.Min(2 + lv / 2, 6);
            for (int i = 0; i < bars; i++) mb.Box(new Vector3(0.45f + (i % 3) * 0.3f, 0.08f + (i / 3) * 0.12f, -d * 0.5f - 0.6f), new Vector3(0.26f, 0.12f, 0.14f), GoldC, 0.45f);
        }

        static void Lapidary(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.0f, d = 1.6f, h = 1.1f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, Wall);
            Door(mb, -0.45f, d);
            Window(mb, new Vector3(0.5f, 0.75f, -d * 0.5f));
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.75f, roof);
            // rueda de tallar afuera con una gema grande encima
            Vector3 c = new Vector3(0.95f, 0f, -d * 0.5f - 0.55f);
            mb.Box(c + Vector3.up * 0.3f, new Vector3(0.5f, 0.6f, 0.4f), WoodD, 0f);
            mb.Cyl(c + new Vector3(0, 0.75f, -0.06f), c + new Vector3(0, 0.75f, 0.06f), 0.32f, 0.32f, 16, Stone, 0f);
            mb.Octa(new Vector3(-1.05f, 0.45f, -d * 0.5f - 0.4f), new Vector3(0.36f, 0.48f, 0.36f), GemC, 0.7f);
            mb.Cyl(new Vector3(-1.05f, 0f, -d * 0.5f - 0.4f), new Vector3(-1.05f, 0.2f, -d * 0.5f - 0.4f), 0.2f, 0.16f, 10, Stone, 0f);
        }

        static void Jeweler(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.2f, d = 1.7f, h = 1.3f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, H("fbf3e4"));
            // vidriera grande con un anillo en exhibicion
            mb.Box(new Vector3(-0.35f, 0.8f, -d * 0.5f - 0.02f), new Vector3(1.0f, 0.75f, 0.06f), WoodD, 0f);
            mb.Box(new Vector3(-0.35f, 0.8f, -d * 0.5f - 0.05f), new Vector3(0.86f, 0.62f, 0.04f), Glass, 0.3f);
            mb.Cyl(new Vector3(-0.35f, 0.82f, -d * 0.5f - 0.09f), new Vector3(-0.35f, 0.82f, -d * 0.5f - 0.13f), 0.16f, 0.16f, 16, GoldC, 0.6f);
            mb.Octa(new Vector3(-0.35f, 1.0f, -d * 0.5f - 0.11f), Vector3.one * 0.12f, Ruby, 0.8f);
            Door(mb, 0.65f, d, 0.9f);
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.6f, roof);
            mb.Box(new Vector3(0, 0.16f + h + 0.02f, -d * 0.5f - 0.1f), new Vector3(w + 0.2f, 0.06f, 0.06f), GoldC, 0.4f);   // ribete dorado
        }

        static void Lab(MeshBuilder mb, int lv, Color roof)
        {
            mb.Cyl(new Vector3(0, 0.0f, 0.1f), new Vector3(0, 1.6f, 0.1f), 0.85f, 0.8f, 16, Wall, 0f);   // torre redonda
            mb.Cyl(new Vector3(0, 1.6f, 0.1f), new Vector3(0, 2.4f, 0.1f), 0.95f, 0.05f, 16, roof, 0f);
            mb.Box(new Vector3(0, 0.45f, -0.72f), new Vector3(0.5f, 0.8f, 0.1f), WoodD, 0f);
            // frascos de colores (brillan)
            Color[] fl = { Violet, GemC, H("7ad94a") };
            for (int i = 0; i < 3; i++)
            {
                Vector3 b = new Vector3(-1.1f + i * 0.28f, 0f, -0.6f + i * 0.2f);
                mb.Cyl(b, b + Vector3.up * 0.3f, 0.14f, 0.14f, 10, fl[i], 0.7f);
                mb.Cyl(b + Vector3.up * 0.3f, b + Vector3.up * 0.48f, 0.05f, 0.05f, 8, Glass, 0.2f);
            }
            mb.Box(new Vector3(0.8f, 1.2f, -0.35f), new Vector3(0.3f, 0.3f, 0.05f), GemC, 0.6f);   // ventana encendida
        }

        // ------------------------------------------------------------ comercio
        static void Market(MeshBuilder mb, int lv, Color roof)
        {
            Plinth(mb, 2.6f, 1.8f);
            int stalls = 3;
            for (int i = 0; i < stalls; i++)
            {
                float x = -0.85f + i * 0.85f;
                mb.Box(new Vector3(x, 0.45f, 0.1f), new Vector3(0.7f, 0.5f, 0.7f), Wood, 0f);   // mesa
                for (int s = -1; s <= 1; s += 2) mb.Box(new Vector3(x + s * 0.32f, 0.75f, -0.2f), new Vector3(0.05f, 1.1f, 0.05f), WoodD, 0f);
                Awning(mb, x - 0.4f, 0.8f, 1.35f, 0.4f, i % 2 == 0 ? roof : RoofRedF, Wall, 4);
                Color[] goods = { OreCol[1], Leaf, GoldC };
                for (int g = 0; g < 3; g++) mb.Octa(new Vector3(x - 0.18f + g * 0.18f, 0.78f, 0.0f), Vector3.one * 0.16f, goods[(g + i) % 3], 0.05f);
            }
            Barrel(mb, new Vector3(1.4f, 0f, -0.5f));
            Crate(mb, new Vector3(-1.45f, 0f, -0.5f), 0.4f);
        }

        static void Station(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.6f, d = 1.4f, h = 1.1f;
            Plinth(mb, w, d);
            Body(mb, w, h, d, Wall, 0.3f);
            Door(mb, 0f, d - 0.6f, 0.9f);
            Window(mb, new Vector3(-0.8f, 0.75f, -0.4f)); Window(mb, new Vector3(0.8f, 0.75f, -0.4f));
            Roof(mb, new Vector3(0, 0.16f + h, 0.3f), w, d, 0.6f, roof);
            // anden techado y reloj
            mb.Box(new Vector3(0, 0.2f, -0.75f), new Vector3(w + 0.4f, 0.1f, 0.7f), Stone, 0f);
            mb.Cyl(new Vector3(0, 0.16f + h + 0.35f, -0.42f), new Vector3(0, 0.16f + h + 0.35f, -0.46f), 0.2f, 0.2f, 16, H("fbfbf7"), 0.2f);
            mb.Cyl(new Vector3(0, 0.16f + h + 0.35f, -0.4f), new Vector3(0, 0.16f + h + 0.35f, -0.42f), 0.24f, 0.24f, 16, roof, 0f);
            // vias
            for (int s = -1; s <= 1; s += 2) mb.Box(new Vector3(0, 0.03f, -1.35f + s * 0.2f), new Vector3(3.6f, 0.04f, 0.05f), Iron, 0f);
            for (int i = 0; i < 8; i++) mb.Box(new Vector3(-1.6f + i * 0.46f, 0.02f, -1.35f), new Vector3(0.1f, 0.03f, 0.6f), WoodD, 0f);
        }

        static void Airport(MeshBuilder mb, int lv, Color roof)
        {
            Plinth(mb, 2.2f, 1.8f);
            // torre de amarre con el globo
            mb.Cyl(new Vector3(0.6f, 0f, 0.3f), new Vector3(0.6f, 2.0f, 0.3f), 0.1f, 0.08f, 8, Wood, 0f);
            mb.Blob(new Vector3(0.6f, 2.9f, 0.3f), new Vector3(0.75f, 0.85f, 0.75f), 1, 41, 0.02f, Color.Lerp(roof, Dark, 0.2f), roof, 0f, -1f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                mb.Box(new Vector3(0.6f + Mathf.Cos(a) * 0.6f, 2.95f, 0.3f + Mathf.Sin(a) * 0.6f), new Vector3(0.16f, 1.1f, 0.16f), Kit3.Yellow, 0.05f);
            }
            mb.Box(new Vector3(0.6f, 1.95f, 0.3f), new Vector3(0.45f, 0.3f, 0.45f), Wood, 0f);   // canasta
            // casilla de control
            mb.Box(new Vector3(-0.6f, 0.55f, -0.2f), new Vector3(0.9f, 0.8f, 0.8f), Wall, 0f);
            Roof(mb, new Vector3(-0.6f, 0.95f, -0.2f), 0.9f, 0.8f, 0.45f, roof);
            Window(mb, new Vector3(-0.6f, 0.6f, -0.6f));
        }
    }
}
