using Mineros.Core;
using Mineros.World;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Arte del Cuartel de mineros (0.10): el edificio central (casa de mina con boca, rieles y vagoneta, que se vuelve de
    /// piedra y despues con ribetes dorados al subir de nivel) y las 8 habitaciones especializadas, cada una con la
    /// estetica de su mineral. La puerta de cada habitacion mira al Cuartel y un pasillo techado las une.
    /// </summary>
    public static partial class IslandArt
    {
        static void Barracks(MeshBuilder mb, int lv, Color roof)
        {
            float w = 2.6f, d = 2.3f, h = 1.55f;
            Color wall = lv >= 4 ? Stone : Wood;
            Color trim = lv >= 7 ? GoldC : WoodD;
            Plinth(mb, w, d);
            Body(mb, w, h, d, wall);
            // esquinas reforzadas
            for (int i = 0; i < 4; i++)
                mb.Box(new Vector3((i % 2 == 0 ? -1 : 1) * (w * 0.5f), 0.16f + h * 0.5f, (i < 2 ? -1 : 1) * (d * 0.5f)), new Vector3(0.16f, h + 0.02f, 0.16f), trim, lv >= 7 ? 0.25f : 0f);
            // boca de mina con arco de madera
            mb.Box(new Vector3(0f, 0.16f + 0.42f, -d * 0.5f - 0.03f), new Vector3(0.78f, 0.84f, 0.08f), Dark, 0f);
            mb.Box(new Vector3(0f, 0.16f + 0.88f, -d * 0.5f - 0.06f), new Vector3(1.0f, 0.12f, 0.12f), WoodD, 0f);
            mb.Box(new Vector3(-0.45f, 0.16f + 0.42f, -d * 0.5f - 0.06f), new Vector3(0.1f, 0.86f, 0.12f), WoodD, 0f);
            mb.Box(new Vector3(0.45f, 0.16f + 0.42f, -d * 0.5f - 0.06f), new Vector3(0.1f, 0.86f, 0.12f), WoodD, 0f);
            Window(mb, new Vector3(-0.72f, 0.16f + 0.78f, -d * 0.5f));
            Window(mb, new Vector3(0.72f, 0.16f + 0.78f, -d * 0.5f));
            Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.8f, roof);
            // bandera arriba: el color cambia con el nivel (amarilla, naranja, azul, ... dorada)
            Color[] flags = { Kit3.Yellow, Copper, Kit3.Blue, Metal, Coal, Stone, GemC, Violet, GoldC };
            Vector3 fb = new Vector3(0.75f, 0.16f + h + 0.3f, 0.3f);
            mb.Cyl(fb, fb + Vector3.up * 1.0f, 0.035f, 0.03f, 6, WoodD, 0f);
            mb.Box(fb + new Vector3(0.2f, 0.85f, 0f), new Vector3(0.36f, 0.22f, 0.02f), flags[Mathf.Clamp(lv - 1, 0, flags.Length - 1)], 0.15f);
            // farol en la puerta
            mb.Box(new Vector3(0.62f, 0.16f + 0.95f, -d * 0.5f - 0.1f), new Vector3(0.12f, 0.16f, 0.12f), Ember, 0.9f);
            if (lv >= 9) mb.Crystal(new Vector3(0f, 0.16f + h + 0.8f, 0f), Vector3.up, 0.7f, 0.22f, 6, GemC, 0.6f);
        }

        /// <summary>Rieles y vagoneta frente a la boca de mina (la vista los saca si en el frente hay una habitacion).</summary>
        public static void BarracksRails(MeshBuilder mb, int lv)
        {
            float d = 2.3f;
            for (int i = 0; i < 2; i++) mb.Box(new Vector3(-0.22f + i * 0.44f, 0.18f, -d * 0.5f - 0.6f), new Vector3(0.05f, 0.04f, 1.1f), Iron, 0f);
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(0f, 0.17f, -d * 0.5f - 0.2f - i * 0.28f), new Vector3(0.62f, 0.03f, 0.1f), WoodD, 0f);
            Cart(mb, new Vector3(0f, 0.18f, -d * 0.5f - 0.75f), lv >= 7 ? GoldC : Copper);
        }

        /// <summary>Color del mineral de cada especialista.</summary>
        public static Color SpecColor(int ch) { return H(Island.Roster[Mathf.Clamp(ch, 0, Island.Roster.Length - 1)].Hex); }

        /// <summary>
        /// Habitacion especializada (1.9 x 1.9 m) con la puerta hacia +Z (hacia el Cuartel; la vista la gira). Cada una
        /// cuenta su oficio sin texto: caños de cobre, chapas de hierro, carbon y chimenea, lingotes de oro, cristales, etc.
        /// </summary>
        public static void Room(MeshBuilder mb, int ch)
        {
            float w = 1.8f, d = 1.7f, h = 1.0f;
            Color c = SpecColor(ch);
            Color wall = Wall, roofC = c;
            switch (ch)
            {
                case 2: wall = Metal; break;
                case 3: wall = H("4a4a52"); break;
                case 5: wall = Stone; break;
                case 6: wall = H("eef6ff"); break;
                case 7: wall = H("d9c8ef"); break;
                case 8: wall = H("2e2e36"); roofC = GoldC; break;
            }
            Plinth(mb, w, d);
            Body(mb, w, h, d, wall);
            // puerta hacia el Cuartel (+Z) y una ventana a cada costado
            mb.Box(new Vector3(0f, 0.16f + 0.4f, d * 0.5f + 0.02f), new Vector3(0.48f, 0.8f, 0.08f), WoodD, 0f);
            mb.Box(new Vector3(0f, 0.16f + 0.36f, -d * 0.5f - 0.03f), new Vector3(0.42f, 0.42f, 0.06f), WoodD, 0f);
            mb.Box(new Vector3(0f, 0.16f + 0.36f, -d * 0.5f - 0.05f), new Vector3(0.32f, 0.32f, 0.06f), Glass, 0.25f);
            if (ch == 7)
            {
                // carpa violeta de exploradores
                Vector3 t0 = new Vector3(0, 0.16f + h, 0);
                mb.Cyl(t0, t0 + Vector3.up * 0.9f, w * 0.72f, 0.05f, 8, roofC, 0.05f);
            }
            else Roof(mb, new Vector3(0, 0.16f + h, 0), w, d, 0.6f, roofC);
            float top = 0.16f + h;
            switch (ch)
            {
                case 1:   // cobre: caños y bobinas
                    mb.Cyl(new Vector3(-w * 0.5f - 0.08f, 0.2f, -0.4f), new Vector3(-w * 0.5f - 0.08f, top + 0.3f, -0.4f), 0.07f, 0.07f, 8, c, 0.1f);
                    mb.Cyl(new Vector3(-w * 0.5f - 0.08f, top + 0.3f, -0.4f), new Vector3(-w * 0.5f - 0.08f, top + 0.3f, 0.3f), 0.07f, 0.07f, 8, c, 0.1f);
                    mb.Cyl(new Vector3(w * 0.5f + 0.25f, 0.16f, 0.2f), new Vector3(w * 0.5f + 0.25f, 0.56f, 0.2f), 0.22f, 0.22f, 12, c, 0.1f);
                    mb.Cyl(new Vector3(w * 0.5f + 0.25f, 0.56f, 0.2f), new Vector3(w * 0.5f + 0.25f, 0.6f, 0.2f), 0.24f, 0.24f, 12, WoodD, 0f);
                    break;
                case 2:   // hierro: chapas remachadas y un yunque
                    for (int i = -1; i <= 1; i++) mb.Box(new Vector3(i * 0.55f, 0.16f + h * 0.5f, -d * 0.5f - 0.02f), new Vector3(0.08f, h, 0.04f), Iron, 0f);
                    mb.Box(new Vector3(w * 0.5f + 0.3f, 0.35f, -0.2f), new Vector3(0.22f, 0.3f, 0.2f), Iron, 0f);
                    mb.Box(new Vector3(w * 0.5f + 0.3f, 0.55f, -0.2f), new Vector3(0.45f, 0.12f, 0.22f), Iron, 0f);
                    break;
                case 3:   // carbon: montañas de carbon y chimenea que brilla
                    for (int i = 0; i < 5; i++) mb.Octa(new Vector3(-w * 0.5f - 0.2f + (i % 2) * 0.2f, 0.2f + (i / 2) * 0.12f, -0.4f + i * 0.18f), Vector3.one * 0.28f, Coal, 0f);
                    Chimney(mb, new Vector3(0.45f, top + 0.15f, 0.2f), 0.7f, H("3a3a40"), true);
                    break;
                case 4:   // oro: cajas con lingotes y techo dorado
                    Crate(mb, new Vector3(w * 0.5f + 0.25f, 0.16f, -0.3f), 0.4f);
                    for (int i = 0; i < 3; i++) mb.Box(new Vector3(w * 0.5f + 0.25f, 0.6f + i * 0.07f, -0.3f + (i - 1) * 0.06f), new Vector3(0.3f, 0.07f, 0.12f), GoldC, 0.5f);
                    mb.Box(new Vector3(0f, top + 0.62f, 0f), new Vector3(0.2f, 0.2f, 0.2f), GoldC, 0.6f);
                    break;
                case 5:   // cristal: cristales que crecen de las paredes y del techo
                    mb.Crystal(new Vector3(-w * 0.5f, 0.3f, 0.2f), new Vector3(-0.6f, 1f, 0f).normalized, 0.6f, 0.16f, 6, c, 0.6f);
                    mb.Crystal(new Vector3(w * 0.5f, 0.4f, -0.3f), new Vector3(0.7f, 1f, 0f).normalized, 0.5f, 0.14f, 6, c, 0.6f);
                    mb.Crystal(new Vector3(0.3f, top + 0.4f, -0.2f), Vector3.up, 0.6f, 0.18f, 6, c, 0.7f);
                    break;
                case 6:   // diamante: un diamante enorme arriba y vidrieras
                    mb.Octa(new Vector3(0f, top + 0.95f, 0f), new Vector3(0.5f, 0.6f, 0.5f), c, 0.7f);
                    mb.Crystal(new Vector3(-w * 0.5f, 0.2f, -0.4f), new Vector3(-0.5f, 1f, 0f).normalized, 0.45f, 0.12f, 6, c, 0.6f);
                    break;
                case 7:   // raros: antena parabolica y cajas con muestras
                    mb.Cyl(new Vector3(w * 0.5f + 0.2f, 0.16f, 0.3f), new Vector3(w * 0.5f + 0.2f, 1.1f, 0.3f), 0.04f, 0.04f, 6, Metal, 0f);
                    mb.Cyl(new Vector3(w * 0.5f + 0.2f, 1.1f, 0.3f), new Vector3(w * 0.5f + 0.05f, 1.25f, 0.18f), 0.32f, 0.08f, 12, Metal, 0f);
                    Crate(mb, new Vector3(-w * 0.5f - 0.25f, 0.16f, -0.2f), 0.36f);
                    mb.Octa(new Vector3(-w * 0.5f - 0.25f, 0.62f, -0.2f), Vector3.one * 0.18f, c, 0.6f);
                    break;
                case 8:   // maestro: ribetes dorados, estandartes y una estrella
                    for (int i = 0; i < 4; i++)
                        mb.Box(new Vector3((i % 2 == 0 ? -1 : 1) * (w * 0.5f), 0.16f + h * 0.5f, (i < 2 ? -1 : 1) * (d * 0.5f)), new Vector3(0.14f, h + 0.02f, 0.14f), GoldC, 0.3f);
                    mb.Box(new Vector3(-0.55f, 0.16f + h * 0.55f, -d * 0.5f - 0.04f), new Vector3(0.3f, 0.6f, 0.02f), Kit3.Blue, 0.05f);
                    mb.Box(new Vector3(0.55f, 0.16f + h * 0.55f, -d * 0.5f - 0.04f), new Vector3(0.3f, 0.6f, 0.02f), Kit3.Blue, 0.05f);
                    mb.Octa(new Vector3(0f, top + 0.85f, 0f), new Vector3(0.36f, 0.36f, 0.14f), GoldC, 0.8f);
                    break;
            }
        }

        /// <summary>Pasillo techado entre el Cuartel y una habitacion (largo `len`, a lo largo de +Z).</summary>
        public static void Corridor(MeshBuilder mb, float len, Color roof)
        {
            float w = 0.8f, h = 0.75f;
            mb.Box(new Vector3(0, 0.08f, 0), new Vector3(w + 0.2f, 0.16f, len), Stone, 0f);
            mb.Box(new Vector3(-w * 0.5f, 0.16f + h * 0.5f, 0), new Vector3(0.08f, h, len), Wood, 0f);
            mb.Box(new Vector3(w * 0.5f, 0.16f + h * 0.5f, 0), new Vector3(0.08f, h, len), Wood, 0f);
            mb.Box(new Vector3(0, 0.16f + h + 0.05f, 0), new Vector3(w + 0.3f, 0.1f, len + 0.1f), roof, 0f);
            for (int i = -1; i <= 1; i += 2) mb.Box(new Vector3(0, 0.16f + h * 0.5f, i * len * 0.5f), new Vector3(w + 0.18f, h + 0.1f, 0.1f), WoodD, 0f);
        }
    }
}
