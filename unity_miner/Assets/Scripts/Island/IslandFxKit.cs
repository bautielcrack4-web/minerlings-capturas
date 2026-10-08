using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.IslandView
{
    /// <summary>
    /// Piezas chicas compartidas por el mundo vivo: textura de punto suave, materiales aditivo y transparente de las
    /// particulas propias, cartel (quad) que mira a la camara y sistemas de particulas simples (luciernagas, lluvia,
    /// hojas). Todo se crea una vez y se reutiliza.
    /// </summary>
    public static class IslandFxKit
    {
        static Texture2D dot, white;
        static Material add, alpha;
        static Mesh quad;

        /// <summary>Punto suave (borde analitico, 128 px) para halos, luciernagas y gotas.</summary>
        public static Texture2D Dot()
        {
            if (dot != null) return dot;
            const int N = 128;
            dot = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PuntoSuave" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            dot.SetPixels32(px);
            dot.Apply(false, true);
            return dot;
        }

        public static Texture2D White()
        {
            if (white != null) return white;
            white = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "Blanco" };
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            white.SetPixels32(px);
            white.Apply(false, true);
            return white;
        }

        public static Material Additive()
        {
            if (add != null) return add;
            add = new Material(Shader.Find("Mineros/FxParticleAdd")) { name = "AditivoIsla", mainTexture = Dot() };
            return add;
        }

        public static Material Alpha()
        {
            if (alpha != null) return alpha;
            alpha = new Material(Shader.Find("Mineros/FxParticleAlpha")) { name = "TransparenteIsla", mainTexture = Dot() };
            return alpha;
        }

        public static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "Quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
            return quad;
        }

        /// <summary>Halo aditivo (cartel que mira a la camara: girarlo cada cuadro con Face).</summary>
        public static MeshRenderer Halo(Transform parent, string name, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Additive();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static MaterialPropertyBlock mpb;

        /// <summary>Color (con alfa) de un halo o cartel por MaterialPropertyBlock.</summary>
        public static void Tint(Renderer r, Color c)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            mpb.SetColor("_TintColor", c);
            r.SetPropertyBlock(mpb);
        }

        public static void Face(Transform t, Camera cam) { t.rotation = cam.transform.rotation; }

        /// <summary>Sistema de particulas simple en espacio de mundo (las piezas comunes ya configuradas).</summary>
        public static ParticleSystem System(Transform parent, string name, Material mat, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.playOnAwake = false;
            main.loop = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }
    }
}
