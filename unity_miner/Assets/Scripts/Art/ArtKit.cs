using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Mineros.Art.Gen;

namespace Mineros.Art
{
    /// <summary>
    /// Kit de arte propio: TODA la utileria, vegetacion y piezas de mineria del juego, generadas por codigo con el
    /// estilo unico (ver unity_miner/ART_BIBLE.md): shader Mineros/MinerToon, paleta por bioma, formas redondeadas.
    /// CONTRATO (no cambiar firmas). Lo implementa el modulo Art; el mundo y la UI lo usan.
    ///
    /// Arquitectura: la geometria se genera en Assets/Scripts/Art/Gen (sin UnityEngine, tambien corre en
    /// unity_tools/artkit_preview). Cada accesorio sale como UNA malla con una submalla por material (equivale a combinar
    /// las piezas con Mesh.CombineMeshes). Las mallas y materiales se cachean por (kind, bioma, variante) y se comparten
    /// entre todas las instancias; no hay trabajo por frame.
    /// </summary>
    public static class ArtKit
    {
        sealed class Built
        {
            public Mesh mesh;
            public Material[] mats;
            public bool hasAnchor;
            public Vector3 anchor;
        }

        static readonly Dictionary<string, Built> PropCache = new Dictionary<string, Built>();
        static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();
        static readonly Dictionary<int, Mesh> CrystalCache = new Dictionary<int, Mesh>();
        static Mesh gemCache;
        static Shader shaderCache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // con "Enter Play Mode sin recargar dominio" las estaticas sobreviven: se vacian al entrar a Play
            PropCache.Clear();
            MatCache.Clear();
            CrystalCache.Clear();
            gemCache = null;
            shaderCache = null;
        }

        /// <summary>
        /// Crea un accesorio como hijo de parent, con la base en el origen local. biome 0..3, seed para variacion.
        /// kinds: tree, bush, flower, grass, cactus, shrub, crystal_cluster, mushroom, deadtree, vent, pebble,
        /// rock_small, minecart, rail, lantern, beam, barrel, crate, ore_pile, ingot, gem, pickaxe_stand, sign.
        /// Tipos desconocidos devuelven un GameObject vacio (nunca null).
        ///
        /// El seed elige la variante (3-4 por tipo), la escala (+-12%) y la rotacion Y; rail, beam, minecart, sign, lantern
        /// y pickaxe_stand NO rotan al azar (se alinean con el mundo) y rail nunca se escala (tramo de 2 unidades a lo largo
        /// de +Z, centrado, que se engancha con el siguiente; la cabeza del riel queda a y = 0.14 y la vagoneta tiene su
        /// origen en la base de las ruedas). beam es un marco de tunel en el plano XY (abierto en Z, luz libre de 1.6 de ancho por 1.7 de alto).
        /// sign apunta a +X. lantern, crystal_cluster, mushroom y vent llevan un hijo vacio "Glow" donde conviene poner una
        /// luz puntual / particulas.
        /// </summary>
        public static GameObject Prop(string kind, int biome, int seed, Transform parent)
        {
            var go = new GameObject(string.IsNullOrEmpty(kind) ? "prop" : kind);
            go.transform.SetParent(parent, false);
            if (!PropGen.IsKind(kind)) return go;

            biome = PaletteData.Clamp(biome);
            int n = PropGen.Variants(kind);
            var rng = new Rng(Rng.Hash(kind, biome * 131 + 7, seed));
            int variant = rng.Int(n);
            float scale = PropGen.Scales(kind) ? 1f + rng.Range(-0.12f, 0.12f) : 1f;
            float rot = PropGen.Randomizes(kind) ? rng.Range(0f, 360f) : 0f;

            var b = GetBuilt(kind, biome, variant);
            go.transform.localRotation = Quaternion.Euler(0f, rot, 0f);
            go.transform.localScale = new Vector3(scale, scale, scale);
            go.AddComponent<MeshFilter>().sharedMesh = b.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = b.mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            if (b.hasAnchor)
            {
                var g = new GameObject("Glow");
                g.transform.SetParent(go.transform, false);
                g.transform.localPosition = b.anchor;
            }
            return go;
        }

        /// <summary>Material compartido con el shader unico (cacheado por color/brillo/emision). No modificarlo: es compartido.
        /// Los colores se escriben en sRGB (como en la paleta hex); Unity los convierte a lineal al asignarlos al material.</summary>
        public static Material Mat(Color color, float spec = 0f, Color emission = default(Color))
        {
            string key = Key(color, spec, emission);
            Material m;
            if (MatCache.TryGetValue(key, out m) && m != null) return m;

            if (shaderCache == null) shaderCache = Shader.Find("Mineros/MinerToon");
            bool toon = shaderCache != null;
            var sh = toon ? shaderCache : Shader.Find("Standard"); // respaldo si el shader unico no esta en la build
            m = new Material(sh);
            m.name = "Art_" + key;
            m.hideFlags = HideFlags.DontSave;
            color.a = 1f;
            m.color = color;
            if (m.HasProperty("_Spec")) m.SetFloat("_Spec", spec);
            if (m.HasProperty("_Gloss")) m.SetFloat("_Gloss", spec > 0.8f ? 70f : 40f);
            bool glow = emission.r + emission.g + emission.b > 0.001f;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", glow ? emission : Color.black);
            if (!toon)
            {
                // Standard: brillo y emision con sus propios nombres
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f + spec * 0.5f);
                if (glow) m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            MatCache[key] = m;
            return m;
        }

        /// <summary>Malla de gema tallada (para gemas que vuelan, premios, iconos 3D). Centrada en el origen, radio de
        /// cintura 0.5 (~1 de ancho), punta hacia -Y, normales planas por cara. Una sola submalla: usar Mat(color, spec ~0.9).</summary>
        public static Mesh GemMesh()
        {
            if (gemCache == null)
            {
                gemCache = ToMesh(PropGen.GemShape(), "Art_Gem");
            }
            return gemCache;
        }

        /// <summary>Malla de cristal (prisma con punta) para racimos y rocas de cueva. Base en y = 0, punta a ~1 de altura,
        /// radio ~0.27, normales planas. Hay 8 formas distintas (seed modulo 8). Una sola submalla.</summary>
        public static Mesh CrystalMesh(int seed)
        {
            int k = ((seed % 8) + 8) % 8;
            Mesh m;
            if (CrystalCache.TryGetValue(k, out m) && m != null) return m;
            m = ToMesh(PropGen.CrystalShape(k), "Art_Crystal" + k);
            CrystalCache[k] = m;
            return m;
        }

        // ------------------------------------------------------------ internos
        static Built GetBuilt(string kind, int biome, int variant)
        {
            string key = kind + "|" + biome + "|" + variant;
            Built b;
            if (PropCache.TryGetValue(key, out b) && b.mesh != null) return b;
            var data = PropGen.Build(kind, biome, variant);
            b = new Built();
            b.mesh = ToMesh(data, "Art_" + key);
            b.mats = new Material[data.mats.Length];
            for (int i = 0; i < b.mats.Length; i++)
            {
                var s = data.mats[i];
                b.mats[i] = Mat(new Color(s.color.r, s.color.g, s.color.b, 1f), s.spec, new Color(s.emis.r, s.emis.g, s.emis.b, 1f));
            }
            b.hasAnchor = data.hasAnchor;
            b.anchor = new Vector3(data.anchor.x, data.anchor.y, data.anchor.z);
            PropCache[key] = b;
            return b;
        }

        static Mesh ToMesh(MeshData d, string name)
        {
            var mesh = new Mesh();
            mesh.name = name;
            mesh.hideFlags = HideFlags.DontSave;
            int n = d.verts.Length;
            if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var vs = new Vector3[n];
            var ns = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                vs[i] = new Vector3(d.verts[i].x, d.verts[i].y, d.verts[i].z);
                ns[i] = new Vector3(d.normals[i].x, d.normals[i].y, d.normals[i].z);
            }
            mesh.vertices = vs;
            mesh.normals = ns;
            mesh.subMeshCount = d.tris.Length;
            for (int i = 0; i < d.tris.Length; i++) mesh.SetTriangles(d.tris[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        static string Key(Color c, float spec, Color e)
        {
            var ci = CultureInfo.InvariantCulture;
            return Mathf.RoundToInt(c.r * 1000f).ToString(ci) + "_" + Mathf.RoundToInt(c.g * 1000f).ToString(ci) + "_" +
                   Mathf.RoundToInt(c.b * 1000f).ToString(ci) + "_s" + Mathf.RoundToInt(spec * 100f).ToString(ci) + "_e" +
                   Mathf.RoundToInt(e.r * 1000f).ToString(ci) + "_" + Mathf.RoundToInt(e.g * 1000f).ToString(ci) + "_" +
                   Mathf.RoundToInt(e.b * 1000f).ToString(ci);
        }
    }

    /// <summary>
    /// Paleta por bioma (port exacto de Art.BIOMES de miner_idle/scripts/art.gd): 0 Pradera, 1 Desierto, 2 Cueva, 3 Volcan.
    /// Los hex de la fuente son sRGB y aca se usan tal cual en Color: Unity trabaja los Color de C# como sRGB y los convierte
    /// a lineal al asignarlos a una propiedad de color del material (el proyecto usa espacio Linear), asi que NO se
    /// convierte a mano. Lo implementa el modulo Art.
    /// </summary>
    public static class Palette
    {
        /// <summary>Color con nombre de la paleta del bioma ("ground","blot","decor","decor2","face","face_d","top",
        /// "lip","low","rock","rock_d","rock_l","ore","prop","prop2","void","mote"). Desconocido: magenta.</summary>
        public static Color Get(int biome, string key)
        {
            Rgb c;
            if (key == null || !PaletteData.TryGet(biome, key, out c)) return Color.magenta;
            return new Color(c.r, c.g, c.b, 1f);
        }

        /// <summary>Oscuridad ambiente del bioma (0 claro, cueva 0.38, volcan 0.22).</summary>
        public static float Dark(int biome) { return PaletteData.Dark(biome); }

        /// <summary>Color de rango de herramienta (0 D .. 5 SS).</summary>
        public static Color Rank(int r)
        {
            var c = PaletteData.Rank(r);
            return new Color(c.r, c.g, c.b, 1f);
        }
    }
}
