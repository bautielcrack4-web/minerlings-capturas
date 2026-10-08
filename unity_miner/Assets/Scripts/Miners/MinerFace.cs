using System.Collections.Generic;
using Mineros.Core;
using UnityEngine;

namespace Mineros.Miners
{
    /// <summary>
    /// Cara de cada minero en 3D, la misma persona que su foto del carnet (Island.Looks): tono de piel, barba, bigote,
    /// chivita, patillas y anteojos. Medidas tomadas del modelo de Blender (espacio local de la cabeza, frente = +Z):
    /// menton y ≈ -0.02, nariz y ≈ 0.05 (z 0.139, lo mas saliente), ojos y ≈ 0.10, ala del casco y ≈ 0.13. La cara
    /// mide ~0.15 de ancho. Mallas chicas con color por vertice, cacheadas por cara.
    /// </summary>
    public static class MinerFace
    {
        static readonly Dictionary<int, Mesh> cache = new Dictionary<int, Mesh>();

        /// <summary>Tonos de piel (tono &lt; 0.065: el teñido del casco toma los naranjas saturados de la cabeza). Mismo espacio de color que los vertices del modelo, que trae ~0.70/0.41/0.25).</summary>
        public static readonly Color[] SkinTones =
        {
            new Color(0.84f, 0.60f, 0.47f), new Color(0.80f, 0.54f, 0.40f), new Color(0.74f, 0.46f, 0.31f), new Color(0.68f, 0.41f, 0.26f),
            new Color(0.62f, 0.39f, 0.25f), new Color(0.53f, 0.32f, 0.20f), new Color(0.43f, 0.25f, 0.15f), new Color(0.31f, 0.18f, 0.11f),
            new Color(0.23f, 0.13f, 0.08f),
        };
        static readonly Color RefSkin = new Color(0.70f, 0.41f, 0.25f);

        /// <summary>¿Este color de vertice es piel? (calido, rojo &gt; verde &gt; azul con azul presente: el casco naranja no tiene azul).</summary>
        public static bool IsSkin(Color c)
        {
            if (c.r < 0.45f || c.r > 0.9f) return false;
            float g = c.g / c.r, b = c.b / c.r;
            return g > 0.45f && g < 0.68f && b > 0.26f && b < 0.45f;
        }

        /// <summary>Recolorea la piel conservando el sombreado pintado (razon por canal).</summary>
        public static Color Tint(Color c, int skin)
        {
            var t = SkinTones[Mathf.Clamp(skin, 0, SkinTones.Length - 1)];
            return new Color(Mathf.Clamp01(c.r * t.r / RefSkin.r), Mathf.Clamp01(c.g * t.g / RefSkin.g), Mathf.Clamp01(c.b * t.b / RefSkin.b), c.a);
        }

        /// <summary>Malla de los rasgos (barba, bigote, anteojos) para esa cara, o null si no tiene ninguno.</summary>
        public static Mesh MeshFor(Island.FaceLook look, int face)
        {
            Mesh m;
            if (cache.TryGetValue(face, out m)) return m;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            Color hair; if (!ColorUtility.TryParseHtmlString("#" + look.Hair, out hair)) hair = new Color(0.35f, 0.23f, 0.13f);
            hair = hair.linear;   // los vertices del modelo vienen en lineal
            Color hairD = hair * 0.8f; hairD.a = 1f;
            const float zF = 0.118f;   // superficie de la cara a la altura de la boca
            switch (look.Woman ? 0 : look.Facial)
            {
                case 1:   // bigote
                    Ell(v, n, c, t, new Vector3(-0.026f, 0.03f, zF + 0.012f), new Vector3(0.03f, 0.012f, 0.014f), hair, 0f, -12f);
                    Ell(v, n, c, t, new Vector3(0.026f, 0.03f, zF + 0.012f), new Vector3(0.03f, 0.012f, 0.014f), hair, 0f, 12f);
                    break;
                case 2:   // barba de dias: una capa finita sobre la mandibula
                    Ell(v, n, c, t, new Vector3(0f, 0.008f, 0.085f), new Vector3(0.072f, 0.04f, 0.045f), Color.Lerp(hair, RefSkin.linear, 0.55f), 0f, 0f);
                    break;
                case 3:   // chivita
                    Ell(v, n, c, t, new Vector3(0f, -0.006f, zF - 0.004f), new Vector3(0.022f, 0.03f, 0.018f), hair, 0f, 0f);
                    Ell(v, n, c, t, new Vector3(0f, 0.03f, zF + 0.01f), new Vector3(0.03f, 0.009f, 0.012f), hairD, 0f, 0f);
                    break;
                case 4:   // barba corta prolija
                    Ell(v, n, c, t, new Vector3(0f, 0.004f, 0.09f), new Vector3(0.076f, 0.046f, 0.05f), hair, 0f, 0f);
                    Ell(v, n, c, t, new Vector3(0f, 0.03f, zF + 0.01f), new Vector3(0.034f, 0.01f, 0.013f), hairD, 0f, 0f);
                    break;
                case 5:   // barba tupida
                case 6:   // barba larga (trenzada): sigue hacia abajo
                    Ell(v, n, c, t, new Vector3(0f, -0.004f, 0.095f), new Vector3(0.088f, 0.06f, 0.06f), hair, 0f, 0f);
                    Ell(v, n, c, t, new Vector3(0f, 0.032f, zF + 0.012f), new Vector3(0.04f, 0.012f, 0.015f), hairD, 0f, 0f);
                    if (look.Facial == 6)
                    {
                        Ell(v, n, c, t, new Vector3(0f, -0.06f, 0.11f), new Vector3(0.04f, 0.045f, 0.035f), hair, 0f, 0f);
                        Ell(v, n, c, t, new Vector3(0f, -0.11f, 0.115f), new Vector3(0.026f, 0.035f, 0.025f), hairD, 0f, 0f);
                    }
                    break;
                case 7:   // patillas gordas
                    for (int s = -1; s <= 1; s += 2)
                        Ell(v, n, c, t, new Vector3(s * 0.068f, 0.035f, 0.07f), new Vector3(0.022f, 0.05f, 0.04f), hair, 0f, 0f);
                    Ell(v, n, c, t, new Vector3(0f, 0.03f, zF + 0.01f), new Vector3(0.036f, 0.01f, 0.013f), hairD, 0f, 0f);
                    break;
            }
            if (look.Glasses)
            {
                Color frame = new Color(0.08f, 0.07f, 0.07f);
                for (int s = -1; s <= 1; s += 2) Ring(v, n, c, t, new Vector3(s * 0.037f, 0.095f, 0.142f), 0.026f, 0.0055f, frame);
                Ell(v, n, c, t, new Vector3(0f, 0.097f, 0.146f), new Vector3(0.012f, 0.004f, 0.004f), frame, 0f, 0f);
            }
            if (v.Count == 0) { cache[face] = null; return null; }
            m = new Mesh { name = "Cara" + face };
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.UploadMeshData(false);   // legible: se fusiona con el minero
            cache[face] = m;
            return m;
        }

        /// <summary>Elipsoide (giro en Z en grados, para el bigote).</summary>
        static void Ell(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t, Vector3 at, Vector3 r, Color col, float unused, float rollDeg)
        {
            const int lat = 6, lon = 10;
            var q = Quaternion.Euler(0f, 0f, rollDeg);
            int b = v.Count;
            for (int i = 0; i <= lat; i++)
            {
                float a = Mathf.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float o = Mathf.PI * 2f * j / lon;
                    var d = new Vector3(Mathf.Sin(a) * Mathf.Cos(o), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(o));
                    v.Add(at + q * Vector3.Scale(d, r));
                    n.Add(q * new Vector3(d.x / r.x, d.y / r.y, d.z / r.z).normalized);
                    // un poco mas oscuro abajo: se lee el volumen
                    c.Add(Color.Lerp(col * 0.78f, col, (d.y + 1f) * 0.5f));
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int p0 = b + i * (lon + 1) + j, p1 = p0 + lon + 1;
                    t.Add(p0); t.Add(p0 + 1); t.Add(p1);
                    t.Add(p0 + 1); t.Add(p1 + 1); t.Add(p1);
                }
        }

        /// <summary>Aro (marco de un lente), mirando hacia +Z.</summary>
        static void Ring(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t, Vector3 at, float R, float r, Color col)
        {
            const int seg = 14, tube = 5;
            int b = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j <= tube; j++)
                {
                    float o = Mathf.PI * 2f * j / tube;
                    var nn = dir * Mathf.Cos(o) + Vector3.forward * Mathf.Sin(o);
                    v.Add(at + dir * R + nn * r);
                    n.Add(nn);
                    c.Add(col);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < tube; j++)
                {
                    int p0 = b + i * (tube + 1) + j, p1 = p0 + tube + 1;
                    t.Add(p0); t.Add(p1); t.Add(p0 + 1);
                    t.Add(p0 + 1); t.Add(p1); t.Add(p1 + 1);
                }
        }
    }
}
