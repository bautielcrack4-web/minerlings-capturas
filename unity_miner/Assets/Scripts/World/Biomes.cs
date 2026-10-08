using Mineros.Art;
using UnityEngine;

namespace Mineros.World
{
    /// <summary>Paleta y luz de un bioma (valores hex de Art.BIOMES de la version Godot + ajustes de luz 3D).</summary>
    public sealed class BiomePal
    {
        public string Name;
        public Color Ground, Blot, Decor, Decor2, Face, FaceD, Top, Lip, Low, Rock, RockD, RockL, Ore, Prop, Prop2, Void, Mote;
        /// <summary>0 = dia claro; > 0 = bioma oscuro (cueva, volcan): luces puntuales en lamparas.</summary>
        public float Dark;
        public Color Sun, AmbSky, AmbEq, AmbGround;
        public float SunI;
    }

    /// <summary>Paletas de los 4 biomas y colores fijos de Art (port 1:1 de art.gd).</summary>
    public static class Biomes
    {
        public static Color H(string hex)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString("#" + hex, out c)) c = Color.magenta;
            c.a = 1f;
            return c;
        }

        // colores fijos de art.gd
        public static readonly Color Out = H("3b2a1e");
        public static readonly Color Gold = H("ffcc33");
        public static readonly Color GoldD = H("d9961c");
        public static readonly Color Steel = H("b8c6d6");
        public static readonly Color SteelD = H("55627a");
        public static readonly Color WoodL = H("d9a066");
        public static readonly Color WoodM = H("b9824a");
        public static readonly Color WoodD = H("8a5a30");
        public static readonly Color CrystalL = H("d9d0ff");
        public static readonly Color CrystalM = H("a99ae0");
        public static readonly Color CrystalD = H("6a5a9a");
        public static readonly Color CrystalPink = H("f0a0ff");
        public static readonly Color CrystalCyan = H("8fe8ff");
        public static readonly Color GoldRich = H("ffd84a");

        static BiomePal[] all;

        public static int Count { get { return 4; } }

        public static BiomePal Get(int i)
        {
            if (all == null) Build();
            return all[((i % 4) + 4) % 4];
        }

        static void Build()
        {
            all = new BiomePal[4];
            for (int b = 0; b < 4; b++) all[b] = FromPalette(b);
        }

        /// <summary>Lee TODOS los colores de la paleta unica (Palette.Get). Sol y ambiente son solo de iluminacion.</summary>
        static BiomePal FromPalette(int b)
        {
            var p = new BiomePal();
            p.Name = Names[b];
            p.Ground = Palette.Get(b, "ground"); p.Blot = Palette.Get(b, "blot");
            p.Decor = Palette.Get(b, "decor"); p.Decor2 = Palette.Get(b, "decor2");
            p.Face = Palette.Get(b, "face"); p.FaceD = Palette.Get(b, "face_d");
            p.Top = Palette.Get(b, "top"); p.Lip = Palette.Get(b, "lip"); p.Low = Palette.Get(b, "low");
            p.Rock = Palette.Get(b, "rock"); p.RockD = Palette.Get(b, "rock_d"); p.RockL = Palette.Get(b, "rock_l");
            p.Ore = Palette.Get(b, "ore"); p.Prop = Palette.Get(b, "prop"); p.Prop2 = Palette.Get(b, "prop2");
            p.Void = Palette.Get(b, "void"); p.Mote = Palette.Get(b, "mote");
            p.Dark = Palette.Dark(b);
            switch (b)
            {
                case 0: p.Sun = H("fff0d6"); p.SunI = 1.15f; p.AmbSky = H("bcd6ff"); p.AmbEq = H("d6d2c0"); p.AmbGround = H("8a7a5a"); break;
                case 1: p.Sun = H("ffe2b8"); p.SunI = 1.2f; p.AmbSky = H("ffe9c8"); p.AmbEq = H("e6c9a0"); p.AmbGround = H("a07848"); break;
                case 2: p.Sun = H("b9b0ff"); p.SunI = 0.7f; p.AmbSky = H("6a62a8"); p.AmbEq = H("544c84"); p.AmbGround = H("342e52"); break;
                default: p.Sun = H("ffb48a"); p.SunI = 0.85f; p.AmbSky = H("86605e"); p.AmbEq = H("6a4642"); p.AmbGround = H("45282a"); break;
            }
            return p;
        }

        static readonly string[] Names = { "Pradera", "Desierto", "Cueva", "Volcan" };
    }
}
