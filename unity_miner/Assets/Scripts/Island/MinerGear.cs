using Mineros.Core;
using Mineros.Miners;
using Mineros.World;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Equipo visible de cada especialista (0.10): la silueta cuenta que pica. Etapa 1: casco del color de su mineral.
    /// Etapa 2: mochila con trozos de su mineral. Etapa 3: el equipo completo del oficio (cristales que crecen del casco y
    /// la mochila, caños de cobre, hombreras de hierro, mascara de carbon, mazo de piedra, capa y corona del Maestro).
    /// Las piezas cuelgan del modelo (gira con el minero); se arman una vez por minero y etapa.
    /// </summary>
    public static class MinerGear
    {
        /// <summary>Alto del minero en el espacio local de su raiz (MinerModel), para ubicar las piezas.</summary>
        public const float Height = 1.7f;

        public static void Apply(MinerModel mm, int ch, int tier)
        {
            if (mm == null || mm.Rig == null) return;
            var holder = mm.Torso != null ? mm.Torso : mm.Rig;
            for (int i = holder.childCount - 1; i >= 0; i--)
                if (holder.GetChild(i).name == "Equipo") Object.Destroy(holder.GetChild(i).gameObject);
            float height = Height;
            if (tier <= 1 && ch != 8) return;
            var mb = new MeshBuilder();
            Color c = IslandArt.SpecColor(ch);
            Color dark = new Color(0.23f, 0.17f, 0.12f);
            float H = height;
            Vector3 back = new Vector3(0f, H * 0.55f, -H * 0.17f);   // espalda (el minero mira a +Z)
            Vector3 top = new Vector3(0f, H * 0.98f, -H * 0.02f);    // arriba del casco
            bool tripo = IslandArt.TripoModel("cx_mochila", out _) != null;
            if (tier >= 2)
            {
                // mochila con trozos de su mineral (la de Tripo si esta; si no, una por codigo)
                if (!tripo)
                {
                    mb.Box(back, new Vector3(H * 0.3f, H * 0.32f, H * 0.14f), new Color(0.55f, 0.38f, 0.22f), 0f);
                    mb.Box(back + new Vector3(0f, H * 0.17f, 0f), new Vector3(H * 0.31f, H * 0.04f, H * 0.15f), dark, 0f);
                }
                for (int i = 0; i < 3; i++)
                    mb.Octa(back + new Vector3((i - 1) * H * 0.08f, H * 0.2f + (i % 2) * H * 0.03f, 0f), Vector3.one * H * 0.09f, c, Glowy(ch) ? 0.5f : 0.1f);
            }
            if (tier >= 3 || ch == 8)
            {
                switch (ch)
                {
                    case 1:   // cobre: caños que salen de la mochila
                        mb.Cyl(back + new Vector3(-H * 0.12f, H * 0.12f, 0f), back + new Vector3(-H * 0.12f, H * 0.45f, H * 0.06f), H * 0.025f, H * 0.025f, 6, c, 0.1f);
                        mb.Cyl(back + new Vector3(H * 0.12f, H * 0.12f, 0f), back + new Vector3(H * 0.12f, H * 0.4f, H * 0.08f), H * 0.025f, H * 0.025f, 6, c, 0.1f);
                        break;
                    case 2:   // hierro: hombreras pesadas
                        if (tripo) break;
                        mb.Box(new Vector3(-H * 0.2f, H * 0.66f, 0f), new Vector3(H * 0.14f, H * 0.07f, H * 0.18f), new Color(0.42f, 0.45f, 0.5f), 0f);
                        mb.Box(new Vector3(H * 0.2f, H * 0.66f, 0f), new Vector3(H * 0.14f, H * 0.07f, H * 0.18f), new Color(0.42f, 0.45f, 0.5f), 0f);
                        break;
                    case 3:   // carbon: mascara de polvo y lampara potente
                        mb.Box(new Vector3(0f, H * 0.8f, H * 0.12f), new Vector3(H * 0.16f, H * 0.08f, H * 0.04f), dark, 0f);
                        mb.Box(top + new Vector3(0f, -H * 0.06f, H * 0.12f), new Vector3(H * 0.08f, H * 0.06f, H * 0.04f), new Color(1f, 0.85f, 0.4f), 1f);
                        break;
                    case 4:   // oro: hombreras doradas
                        mb.Box(new Vector3(-H * 0.2f, H * 0.66f, 0f), new Vector3(H * 0.12f, H * 0.05f, H * 0.16f), c, 0.4f);
                        mb.Box(new Vector3(H * 0.2f, H * 0.66f, 0f), new Vector3(H * 0.12f, H * 0.05f, H * 0.16f), c, 0.4f);
                        break;
                    case 5: case 6: case 7:   // cristal, diamante, raros: cristales que crecen del casco y de la mochila
                        mb.Crystal(top, new Vector3(-0.3f, 1f, -0.2f).normalized, H * 0.22f, H * 0.06f, 6, c, 0.6f);
                        mb.Crystal(top + new Vector3(H * 0.06f, 0f, -H * 0.04f), new Vector3(0.4f, 1f, -0.1f).normalized, H * 0.17f, H * 0.05f, 6, c, 0.6f);
                        mb.Crystal(back + new Vector3(-H * 0.08f, H * 0.18f, 0f), new Vector3(-0.2f, 1f, -0.3f).normalized, H * 0.25f, H * 0.06f, 6, c, 0.6f);
                        mb.Crystal(back + new Vector3(H * 0.09f, H * 0.18f, 0f), new Vector3(0.3f, 1f, -0.2f).normalized, H * 0.2f, H * 0.05f, 6, c, 0.6f);
                        break;
                    case 8:   // maestro: corona y capa
                        for (int i = 0; i < 5 && !tripo; i++)
                        {
                            float a = i / 5f * Mathf.PI * 2f;
                            mb.Octa(top + new Vector3(Mathf.Cos(a) * H * 0.08f, H * 0.03f, Mathf.Sin(a) * H * 0.08f), new Vector3(H * 0.05f, H * 0.09f, H * 0.05f), new Color(1f, 0.82f, 0.25f), 0.7f);
                        }
                        if (tier >= 2)
                            mb.Quad(new Vector3(-H * 0.18f, H * 0.7f, -H * 0.12f), new Vector3(H * 0.18f, H * 0.7f, -H * 0.12f),
                                new Vector3(H * 0.24f, H * 0.12f, -H * 0.24f), new Vector3(-H * 0.24f, H * 0.12f, -H * 0.24f), Vector3.back, new Color(0.75f, 0.12f, 0.15f), 0f);
                        break;
                }
            }
            // se arma en el espacio de la raiz, mirando hacia donde mira el minero, y se cuelga del torso (sigue el balanceo)
            var r = IslandArt.Bake(mb, mm.transform, "Equipo", true);
            r.transform.localPosition = Vector3.zero;
            if (tripo)
            {
                // piezas de Tripo (mochila, hombreras, corona), en el espacio de la raiz antes de colgarlas del torso
                if (tier >= 2) Piece(r.transform, "cx_mochila", back + new Vector3(0f, 0f, -H * 0.02f), H * 0.34f, 0f, true);
                if ((tier >= 3 || ch == 8) && (ch == 2 || ch == 4))
                {
                    Piece(r.transform, "cx_hombreras", new Vector3(-H * 0.21f, H * 0.66f, 0f), H * 0.2f, 90f, true);
                    Piece(r.transform, "cx_hombreras", new Vector3(H * 0.21f, H * 0.66f, 0f), H * 0.2f, -90f, true);
                }
                if (ch == 8) Piece(r.transform, "cx_corona", top + new Vector3(0f, -H * 0.03f, 0f), H * 0.26f, 0f, false);
            }
            r.transform.localRotation = mm.Rig.localRotation;
            r.transform.SetParent(holder, true);
        }

        static bool Glowy(int ch) { return ch >= 4; }

        /// <summary>Pieza de Tripo de ancho `width`: centrada en `pos` (o apoyada, si no `center`), girada `yaw`.</summary>
        static void Piece(Transform parent, string file, Vector3 pos, float width, float yaw, bool center)
        {
            Material mat;
            var mesh = IslandArt.TripoModel(file, out mat);
            if (mesh == null) return;
            var go = IslandArt.MakeRenderer(parent, file, mesh, new[] { mat }, false);
            var b = mesh.bounds;
            float s = width / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            var q = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localRotation = q;
            go.transform.localScale = Vector3.one * s;
            Vector3 off = q * new Vector3(b.center.x, center ? b.center.y : b.min.y, b.center.z) * s;
            go.transform.localPosition = pos - off;
        }
    }
}
