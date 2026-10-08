using UnityEngine;

namespace Mineros.World
{
    /// <summary>Punto luminoso de escenario (cristales, hongos, fumarolas): el mundo lanza Fx periodicos ahi (crystal_glow, lava_bubble).</summary>
    public struct GlowSpot
    {
        public Vector3 pos;
        public Color col;
        public float size;
        public float phase;
        public int kind; // 0 cristal, 1 hongo, 2 fumarola
    }

    /// <summary>
    /// Tablas de accesorios por bioma (los modelos los genera ArtKit.Prop) y decoracion menuda del suelo,
    /// que se hornea en la malla del tramo con solo 2-3 tonos de la paleta (Decor / Decor2).
    /// </summary>
    public static class PropFactory
    {
        /// <summary>Accesorios de escenografia por bioma (kinds de ArtKit.Prop).</summary>
        public static readonly string[][] Kinds =
        {
            new[] { "tree", "tree", "bush", "flower", "grass" },
            new[] { "cactus", "shrub", "rock_small", "cactus", "pebble" },
            new[] { "crystal_cluster", "mushroom", "crystal_cluster", "rock_small", "mushroom" },
            new[] { "deadtree", "vent", "rock_small", "deadtree", "vent" }
        };

        /// <summary>Utileria de mina que aparece de vez en cuando al borde del pasillo (nunca en el centro).</summary>
        public static readonly string[] MineKinds = { "lantern", "barrel", "crate", "beam", "ore_pile", "minecart", "sign", "pickaxe_stand" };

        /// <summary>true para los accesorios que emiten luz propia (el mundo les agrega destellos periodicos).</summary>
        public static int GlowKind(string kind)
        {
            switch (kind)
            {
                case "crystal_cluster": return 0;
                case "mushroom": return 1;
                case "vent": return 2;
                default: return -1;
            }
        }

        /// <summary>Decoracion menuda: matas, guijarros, florecitas, cristalitos o brasas segun el bioma. Base en pos.</summary>
        public static void AddDecor(MeshBuilder mb, int kind, Vector3 pos, float v, BiomePal pal, int biome, int seed)
        {
            mb.SetTRS(pos, v * 360f, Vector3.one);
            Color tone = pal.Decor, tone2 = pal.Decor2;
            switch (kind)
            {
                case 0:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = -0.6f + i * 0.6f;
                        mb.Cyl(new Vector3((i - 1) * 0.05f, 0f, 0f), new Vector3((i - 1) * 0.05f + Mathf.Sin(a) * 0.1f, 0.2f + v * 0.1f, 0.03f * i), 0.035f, 0f, 3, tone, 0f);
                    }
                    break;
                case 1:
                    mb.Blob(new Vector3(0f, 0.04f, 0f), new Vector3(0.12f + v * 0.1f, 0.07f, 0.1f + v * 0.06f), 0, seed, 0.2f, tone2, tone2, 0f, -0.3f);
                    break;
                case 2:
                    if (biome == 0)
                    {
                        mb.Cyl(Vector3.zero, new Vector3(0f, 0.12f, 0f), 0.012f, 0.01f, 3, pal.Prop, 0f);
                        mb.Octa(new Vector3(0f, 0.14f, 0f), new Vector3(0.05f, 0.035f, 0.05f), v > 0.5f ? pal.Prop2 : tone2, 0.2f);
                    }
                    else if (biome == 1)
                    {
                        mb.Cyl(Vector3.zero, new Vector3(0.1f, 0.18f, 0f), 0.02f, 0.006f, 3, tone, 0f);
                        mb.Cyl(Vector3.zero, new Vector3(-0.08f, 0.16f, 0.05f), 0.02f, 0.006f, 3, tone, 0f);
                    }
                    else if (biome == 2)
                    {
                        mb.Crystal(Vector3.zero, Vector3.up, 0.24f, 0.05f, 5, pal.Prop, 0.6f);
                    }
                    else
                    {
                        mb.Octa(new Vector3(0f, 0.05f, 0f), new Vector3(0.07f, 0.05f, 0.07f), pal.Prop2, 0.9f);
                    }
                    break;
                default:
                    mb.Blob(new Vector3(0f, 0.03f, 0f), new Vector3(0.07f, 0.05f, 0.07f), 0, seed, 0.2f, tone, tone, 0f, -0.3f);
                    break;
            }
            mb.ResetM();
        }
    }
}
