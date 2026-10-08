using System;
using System.Collections.Generic;

namespace Mineros.Art.Gen
{
    /// <summary>
    /// Generador de accesorios (sin UnityEngine). Cada kind se arma con varias piezas (primitivas propias) y sale como UNA
    /// malla con submallas por material. Convenciones de escala: el minero mide ~1.2; vagoneta a la cintura (~0.8), arboles
    /// 2.5-3. Origen en la base, +Y arriba. Las variantes son deterministas (kind, bioma, variante).
    /// Luz del juego: calida desde arriba-izquierda de la camara; los 3 tonos (sombra, base, luz) de follaje y rocas se
    /// reparten con esa misma direccion (ver Shade()).
    /// </summary>
    public static partial class PropGen
    {
        public static readonly string[] Kinds =
        {
            "tree", "bush", "flower", "grass", "cactus", "shrub", "crystal_cluster", "mushroom", "deadtree", "vent",
            "pebble", "rock_small", "minecart", "rail", "lantern", "beam", "barrel", "crate", "ore_pile", "ingot",
            "gem", "pickaxe_stand", "sign",
        };

        static readonly Dictionary<string, int> VarCount = new Dictionary<string, int>
        {
            { "tree", 4 }, { "bush", 4 }, { "flower", 3 }, { "grass", 4 }, { "cactus", 4 }, { "shrub", 3 },
            { "crystal_cluster", 4 }, { "mushroom", 4 }, { "deadtree", 3 }, { "vent", 3 }, { "pebble", 3 },
            { "rock_small", 4 }, { "minecart", 3 }, { "rail", 3 }, { "lantern", 3 }, { "beam", 3 }, { "barrel", 4 },
            { "crate", 4 }, { "ore_pile", 4 }, { "ingot", 3 }, { "gem", 4 }, { "pickaxe_stand", 3 }, { "sign", 3 },
        };

        // Piezas que NO se giran ni escalan al azar (alineacion con rieles / tuneles / flechas).
        static readonly HashSet<string> Fixed = new HashSet<string> { "rail", "beam", "minecart", "sign", "lantern", "pickaxe_stand" };

        public static bool IsKind(string kind) { return kind != null && VarCount.ContainsKey(kind); }
        public static int Variants(string kind) { int n; return kind != null && VarCount.TryGetValue(kind, out n) ? n : 0; }
        /// <summary>true si el kind admite rotacion Y y escala aleatorias por seed.</summary>
        public static bool Randomizes(string kind) { return !Fixed.Contains(kind); }
        /// <summary>El riel no se escala nunca (tramo de 2 unidades enganchable).</summary>
        public static bool Scales(string kind) { return kind != "rail"; }

        public static MeshData Build(string kind, int biome, int variant)
        {
            int bm = PaletteData.Clamp(biome);
            int n = Variants(kind);
            if (n == 0) return null;
            variant = ((variant % n) + n) % n;
            switch (kind)
            {
                case "tree": return Tree(bm, variant);
                case "bush": return Bush(bm, variant);
                case "flower": return Flower(bm, variant);
                case "grass": return Grass(bm, variant);
                case "cactus": return Cactus(bm, variant);
                case "shrub": return Shrub(bm, variant);
                case "crystal_cluster": return CrystalCluster(bm, variant);
                case "mushroom": return Mushroom(bm, variant);
                case "deadtree": return DeadTree(bm, variant);
                case "vent": return Vent(bm, variant);
                case "pebble": return Pebble(bm, variant);
                case "rock_small": return RockSmall(bm, variant);
                case "minecart": return MineCart(bm, variant);
                case "rail": return Rail(bm, variant);
                case "lantern": return Lantern(bm, variant);
                case "beam": return Beam(bm, variant);
                case "barrel": return Barrel(bm, variant);
                case "crate": return Crate(bm, variant);
                case "ore_pile": return OrePile(bm, variant);
                case "ingot": return Ingot(bm, variant);
                case "gem": return GemProp(bm, variant);
                case "pickaxe_stand": return PickaxeStand(bm, variant);
                case "sign": return Sign(bm, variant);
            }
            return null;
        }

        /// <summary>Malla suelta de cristal (base en y = 0, ~1 de alto) para rocas de cueva.</summary>
        public static MeshData CrystalShape(int seed)
        {
            var b = new MeshBuilder();
            int s = b.Mat(Rgb.White, 0.5f);
            var rng = new Rng(Rng.Hash("cmesh", seed, 1));
            Gems.Crystal(b, s, Xf.I, seed, rng.Range(0.95f, 1.15f), rng.Range(0.24f, 0.3f));
            return b.Build();
        }

        /// <summary>Malla suelta de gema tallada centrada en el origen (radio de cintura 0.5).</summary>
        public static MeshData GemShape()
        {
            var b = new MeshBuilder();
            int s = b.Mat(Rgb.White, 0.9f);
            Gems.Gem(b, s, Xf.I, 0.5f);
            return b.Build();
        }

        // ------------------------------------------------------------ helpers
        static Rgb P(int bm, string key) { return PaletteData.Get(bm, key); }
        static Rng R(string kind, int bm, int v) { return new Rng(Rng.Hash(kind, bm, v)); }

        static void Bl(MeshBuilder b, int slot, float x, float y, float z, float r, int seed, float sy = 1f,
            int seg = 10, int rings = 6, float amp = 0.12f, bool flat = false)
        {
            Prims.Blob(b, slot, Xf.I.T(x, y, z).S(1f, sy, 1f), seed, r, seg, rings, amp, flat);
        }

        static void Sp(MeshBuilder b, int slot, float x, float y, float z, float r, float sy = 1f, int seg = 8, int rings = 4, bool flat = false)
        {
            Prims.Sphere(b, slot, Xf.I.T(x, y, z).S(1f, sy, 1f), r, seg, rings, flat);
        }

        /// <summary>Tono segun la luz del juego (calida desde arriba-izquierda de la camara iso, cuyo "adelante" es (-1,*,+1)):
        /// 0 sombra / 1 base / 2 luz para un punto con direccion horizontal (dx,dz) y altura relativa h (-1 abajo .. 1 arriba).</summary>
        static int Shade(float dx, float dz, float h)
        {
            float lit = -(dx + dz) * 0.7071f + h * 0.9f; // + = hacia la luz
            return lit > 0.75f ? 2 : (lit > -0.15f ? 1 : 0);
        }

        static V3 Pt(float x, float y, float z) { return new V3(x, y, z); }

        static float Rad(float deg) { return deg * (float)Math.PI / 180f; }
    }
}
