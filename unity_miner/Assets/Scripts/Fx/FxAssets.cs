using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.Fx
{
    /// <summary>
    /// Texturas de particulas (generadas por FxTexGen y cacheadas) y materiales compartidos (uno por textura/mezcla).
    /// Shaders propios Mineros/FxParticleAdd y Mineros/FxParticleAlpha; si Shader.Find devuelve null cae a los de Unity.
    /// </summary>
    internal static class FxAssets
    {
        static readonly Dictionary<FxTex, Texture2D> textures = new Dictionary<FxTex, Texture2D>();
        static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        static Shader addShader, alphaShader;
        static bool addLegacy, alphaLegacy;
        static bool resolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            textures.Clear();
            materials.Clear();
            addShader = alphaShader = null;
            resolved = false;
        }

        internal static Texture2D Tex(FxTex id)
        {
            Texture2D t;
            if (textures.TryGetValue(id, out t) && t != null) return t;
            int n = FxTexGen.SizeOf(id);
            byte[] px = FxTexGen.Make(id);
            t = new Texture2D(n, n, TextureFormat.RGBA32, true, false);
            t.name = "FxTex_" + id;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.HideAndDontSave;
            t.SetPixelData(px, 0);
            t.Apply(true, true);
            textures[id] = t;
            return t;
        }

        static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            addShader = Usable(Shader.Find("Mineros/FxParticleAdd"));
            if (addShader == null)
            {
                addLegacy = true;
                addShader = Usable(Shader.Find("Legacy Shaders/Particles/Additive"));
                if (addShader == null) addShader = Usable(Shader.Find("Particles/Standard Unlit"));
                if (addShader == null) addShader = Usable(Shader.Find("Sprites/Default"));
            }
            alphaShader = Usable(Shader.Find("Mineros/FxParticleAlpha"));
            if (alphaShader == null)
            {
                alphaLegacy = true;
                alphaShader = Usable(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
                if (alphaShader == null) alphaShader = Usable(Shader.Find("Particles/Standard Unlit"));
                if (alphaShader == null) alphaShader = Usable(Shader.Find("Sprites/Default"));
            }
        }

        static Shader Usable(Shader s) { return (s != null && s.isSupported) ? s : null; }

        /// <summary>Material compartido para (textura, aditivo o alfa, encima de la geometria o con profundidad).</summary>
        internal static Material Mat(FxTex id, bool additive, bool overlay)
        {
            int key = ((int)id << 2) | (additive ? 1 : 0) | (overlay ? 2 : 0);
            Material m;
            if (materials.TryGetValue(key, out m) && m != null) return m;
            Resolve();
            Shader sh = additive ? addShader : alphaShader;
            if (sh == null) sh = Shader.Find("Sprites/Default");
            m = new Material(sh);
            m.name = "FxMat_" + id + (additive ? "_add" : "_alpha") + (overlay ? "_top" : "");
            m.hideFlags = HideFlags.HideAndDontSave;
            m.mainTexture = Tex(id);
            bool legacy = additive ? addLegacy : alphaLegacy;
            if (legacy)
            {
                // los shaders de Unity multiplican por _TintColor * 2: 0.5 deja el color de la particula tal cual
                if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                if (m.HasProperty("_Color") && !m.HasProperty("_TintColor")) m.SetColor("_Color", Color.white);
            }
            else
            {
                m.SetFloat("_ZTest", overlay ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
            }
            m.renderQueue = overlay ? 3100 : 3000;
            materials[key] = m;
            return m;
        }
    }
}
