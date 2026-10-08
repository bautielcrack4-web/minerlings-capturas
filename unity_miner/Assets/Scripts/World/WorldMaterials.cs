using System.Collections.Generic;
using Mineros.Art;
using UnityEngine;

namespace Mineros.World
{
    /// <summary>
    /// Materiales del mundo: TODOS salen del shader unico Mineros/MinerToon a traves de ArtKit.Mat (una sola fuente de estilo).
    /// Las mallas del mundo reparten sus triangulos en submallas por "tono" (color cuantizado + nivel de emision), asi el
    /// mundo no necesita colores de vertice ni shaders propios y los materiales se comparten entre todas las mallas.
    /// </summary>
    public static class WorldMaterials
    {
        static readonly Dictionary<int, Material> tones = new Dictionary<int, Material>();
        static Material textMat;
        static Font textFont;

        // ---- claves de tono: 7 bits por canal (paso ~4/255) + nivel de emision (0 nada, 1 suave, 2 fuerte)
        public static int Key(Color c, float emis)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 63.75f), 0, 64);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 63.75f), 0, 64);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 63.75f), 0, 64);
            int lvl = emis <= 0.02f ? 0 : (emis < 0.5f ? 1 : 2);
            return r | (g << 7) | (b << 14) | (lvl << 21);
        }

        /// <summary>Material compartido para una clave de tono (se crea una sola vez).</summary>
        public static Material FromKey(int key)
        {
            Material m;
            if (tones.TryGetValue(key, out m) && m != null) return m;
            float r = (key & 127) / 63.75f, g = ((key >> 7) & 127) / 63.75f, b = ((key >> 14) & 127) / 63.75f;
            int lvl = (key >> 21) & 3;
            Color col = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
            Color em = lvl == 0 ? Color.black : (lvl == 1 ? col * 0.38f : col * 0.9f);
            em.a = 1f;
            float spec = lvl > 0 ? 0.35f : 0f;
            m = Make(col, spec, em);
            tones[key] = m;
            return m;
        }

        public static Material Tone(Color c, float emis) { return FromKey(Key(c, emis)); }

        /// <summary>Material del estilo unico (ArtKit.Mat) con respaldo si el kit aun no responde.</summary>
        public static Material Make(Color col, float spec, Color emission)
        {
            Material m = ArtKit.Mat(col, spec, emission);
            if (m != null) return m;
            Shader sh = Shader.Find("Mineros/MinerToon");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            m.color = col;
            if (m.HasProperty("_Rim")) m.SetFloat("_Rim", 0.22f);
            if (m.HasProperty("_Floor")) m.SetFloat("_Floor", 0.42f);
            if (m.HasProperty("_Spec")) m.SetFloat("_Spec", spec);
            if (m.HasProperty("_Gloss")) m.SetFloat("_Gloss", 40f);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            return m;
        }

        public static void Build()
        {
            // los materiales se crean bajo demanda; aqui solo se descartan los destruidos (recarga de dominio)
            if (textMat == null) textFont = null;
        }

        /// <summary>Material del texto flotante para la fuente dada: siempre encima, con la textura de la fuente.</summary>
        public static Material TextMaterial(Font font)
        {
            if (textMat != null && textFont == font) return textMat;
            textFont = font;
            textMat = new Material(font.material);
            textMat.name = "TextoFlotante";
            textMat.renderQueue = 3200;
            Font.textureRebuilt -= OnFontRebuilt;
            Font.textureRebuilt += OnFontRebuilt;
            return textMat;
        }

        static void OnFontRebuilt(Font f)
        {
            if (f == textFont && textMat != null && f.material != null) textMat.mainTexture = f.material.mainTexture;
        }
    }
}
