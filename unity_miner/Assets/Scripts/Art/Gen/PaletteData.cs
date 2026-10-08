using System.Collections.Generic;

namespace Mineros.Art.Gen
{
    /// <summary>
    /// Paleta por bioma: port exacto de Art.BIOMES de miner_idle/scripts/art.gd (4 biomas: 0 Pradera, 1 Desierto,
    /// 2 Cueva, 3 Volcan), RANK_COLS y los colores fijos del minero. Sin UnityEngine.
    /// Los hex son sRGB; Unity convierte a lineal al asignarlos al material (el proyecto usa espacio Linear), asi que se
    /// usan tal cual (ver Palette en ArtKit.cs).
    /// </summary>
    public static class PaletteData
    {
        public static readonly string[] Keys =
        {
            "ground", "blot", "decor", "decor2", "face", "face_d", "top", "lip", "low",
            "rock", "rock_d", "rock_l", "ore", "prop", "prop2", "void", "mote",
        };

        static readonly string[][] Hex =
        {
            //          ground    blot      decor     decor2    face      face_d    top       lip       low
            //          rock      rock_d    rock_l    ore       prop      prop2     void      mote
            new[] { "a9c97a", "9dbf6c", "86a85c", "c3da97", "7a5f45", "5c4632", "8fb866", "a8cf78", "6f8a4e",
                    "9aa0a6", "6f757c", "c9cdd2", "e08a4a", "5fa04a", "f0a443", "2b2a20", "fff6a0" },
            new[] { "ecd09a", "e2c487", "c9a46a", "f6e0b4", "b07a4c", "8a5a36", "e0b97e", "f0cf98", "c09a64",
                    "d0905a", "9e6740", "efbe8a", "ffd34d", "6fae5a", "e86a5a", "3a2a1c", "fff0d0" },
            new[] { "6d6385", "645a7c", "857a9e", "5a5170", "3a3150", "2a233c", "51476c", "665a86", "3e3656",
                    "7a74a0", "4f496c", "a7a2c9", "6ef0ff", "6ef0ff", "ff7ad9", "17131f", "8ff4ff" },
            new[] { "7d5d52", "735448", "94695c", "65473f", "4a2d28", "33201c", "62423a", "7a5248", "4a332d",
                    "615659", "40383b", "8a7f83", "ff6a2a", "2e2321", "ff8a3a", "1c1110", "ffae4a" },
        };

        static readonly float[] DarkV = { 0f, 0f, 0.38f, 0.22f };

        static readonly string[] RankHex = { "a9b3bd", "6fd36a", "4aa3f0", "b06ef0", "ffcf3a", "ff5a4e" };

        static readonly Dictionary<string, Rgb>[] Tables = Build();

        static Dictionary<string, Rgb>[] Build()
        {
            var t = new Dictionary<string, Rgb>[Hex.Length];
            for (int b = 0; b < Hex.Length; b++)
            {
                t[b] = new Dictionary<string, Rgb>();
                for (int k = 0; k < Keys.Length; k++) t[b][Keys[k]] = Rgb.Hex(Hex[b][k]);
            }
            return t;
        }

        public static int Clamp(int biome) { return biome < 0 ? 0 : (biome >= Hex.Length ? Hex.Length - 1 : biome); }

        public static bool TryGet(int biome, string key, out Rgb c)
        {
            return Tables[Clamp(biome)].TryGetValue(key, out c);
        }

        public static Rgb Get(int biome, string key)
        {
            Rgb c;
            return TryGet(biome, key, out c) ? c : new Rgb(1, 0, 1);
        }

        public static float Dark(int biome) { return DarkV[Clamp(biome)]; }

        public static Rgb Rank(int r)
        {
            r = r < 0 ? 0 : (r >= RankHex.Length ? RankHex.Length - 1 : r);
            return Rgb.Hex(RankHex[r]);
        }

        // ---- colores fijos del minero y utileria (art.gd)
        public static readonly Rgb Overall = Rgb.Hex("3f6fb5");
        public static readonly Rgb Shirt = Rgb.Hex("e0703a");
        public static readonly Rgb Boot = Rgb.Hex("4a3426");
        public static readonly Rgb Handle = Rgb.Hex("9b6a3c");
        public static readonly Rgb Gold = Rgb.Hex("ffcc33");
        public static readonly Rgb GoldD = Rgb.Hex("d9961c");
        public static readonly Rgb Gem = Rgb.Hex("4fc3f7");
        public static readonly Rgb GemD = Rgb.Hex("1f7fc4");
        public static readonly Rgb Trunk = Rgb.Hex("8a5a3a");
        public static readonly Rgb TrunkD = Rgb.Hex("6b4329");
        public static readonly Rgb Stem = Rgb.Hex("5f8f3e");
        public static readonly Rgb Ivory = Rgb.Hex("fff4d8");
        public static readonly Rgb MushStem = Rgb.Hex("e8e0f0");
        public static readonly Rgb Twig = Rgb.Hex("a07a4a");
        public static readonly Rgb Yellow = Rgb.Hex("ffd34d");
        public static readonly Rgb Steel = Rgb.Hex("a9b3bd");
    }
}
