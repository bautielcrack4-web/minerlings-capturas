using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.Miners
{
    /// <summary>
    /// Minero con esqueleto (TRELLIS.2 + UniRig, convertido por unity_tools/blender_miner/rig_export.py): lee
    /// Resources/MinerRig/&lt;nombre&gt;.json + .png. Alto 1, suela en 0, mirando a +Z. La malla y el material se comparten.
    /// roles = [cadera, pecho, cabeza, brazoL, antebrazoL, manoL, brazoR, antebrazoR, manoR, piernaL, rodillaL, piernaR, rodillaR].
    /// </summary>
    public static class RiggedMiner
    {
        [Serializable]
        public sealed class Data
        {
            public float[] pos, nrm, uv, wt, head;
            public int[] idx, bw, parent, roles;
            public string[] bones;
            public bool tex;
            [NonSerialized] public Mesh Mesh;
            [NonSerialized] public Material Mat;

            public Vector3 Head(int bone) { return new Vector3(head[bone * 3], head[bone * 3 + 1], head[bone * 3 + 2]); }
            public int Role(int r) { return roles[r]; }
        }

        public const int Hips = 0, Chest = 1, HeadR = 2, UpL = 3, LoL = 4, HandL = 5, UpR = 6, LoR = 7, HandR = 8, LegL = 9, ShinL = 10, LegR = 11, ShinR = 12;

        static readonly Dictionary<string, Data> cache = new Dictionary<string, Data>();

        /// <summary>Nombre del modelo de un especialista y su etapa (mr_oro_3); null si no es un especialista con modelo.</summary>
        public static string NameFor(int ch, int tier)
        {
            if (ch < 0 || ch >= Minerals.Length) return null;
            return "mr_" + Minerals[ch] + "_" + Mathf.Clamp(tier, 1, 3);
        }
        static readonly string[] Minerals = { "piedra", "cobre", "hierro", "carbon", "oro", "cristal", "diamante", "raros", "maestro" };

        /// <summary>Datos listos (malla con pesos + material) o null si ese modelo no esta en Resources.</summary>
        public static Data Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Data d;
            if (cache.TryGetValue(name, out d)) return d;
            cache[name] = null;
            var ta = Resources.Load<TextAsset>("MinerRig/" + name);
            if (ta == null) return null;
            try { d = JsonUtility.FromJson<Data>(ta.text); }
            catch (Exception e) { Debug.LogError("Minero con esqueleto " + name + ": " + e.Message); return null; }
            if (d == null || d.roles == null || d.roles.Length < 13 || d.pos == null) { Debug.LogError("Minero con esqueleto " + name + ": datos incompletos"); return null; }
            d.Mesh = BuildMesh(d, name);
            d.Mat = BuildMat(d, name);
            cache[name] = d;
            return d;
        }

        static Mesh BuildMesh(Data d, string name)
        {
            int n = d.pos.Length / 3;
            var v = new Vector3[n]; var nr = new Vector3[n]; var uv = new Vector2[n];
            var w = new BoneWeight[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(d.pos[i * 3], d.pos[i * 3 + 1], d.pos[i * 3 + 2]);
                nr[i] = new Vector3(d.nrm[i * 3], d.nrm[i * 3 + 1], d.nrm[i * 3 + 2]);
                uv[i] = new Vector2(d.uv[i * 2], d.uv[i * 2 + 1]);
                int k = i * 4;
                w[i] = new BoneWeight
                {
                    boneIndex0 = d.bw[k], weight0 = d.wt[k], boneIndex1 = d.bw[k + 1], weight1 = d.wt[k + 1],
                    boneIndex2 = d.bw[k + 2], weight2 = d.wt[k + 2], boneIndex3 = d.bw[k + 3], weight3 = d.wt[k + 3],
                };
            }
            // los huesos nacen sin giro en la cabeza de cada hueso: su pose de enlace es solo la traslacion inversa
            var bind = new Matrix4x4[d.bones.Length];
            for (int b = 0; b < bind.Length; b++) bind[b] = Matrix4x4.Translate(-d.Head(b));
            var m = new Mesh { name = "MineroRig_" + name };
            m.indexFormat = n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.vertices = v; m.normals = nr; m.uv = uv; m.triangles = d.idx;
            m.boneWeights = w;
            m.bindposes = bind;
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }

        static Material BuildMat(Data d, string name)
        {
            var sh = Shader.Find("Mineros/MinerToonTex");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh) { name = "MineroRig_" + name, hideFlags = HideFlags.DontSave };
            if (d.tex)
            {
                var tex = Resources.Load<Texture2D>("MinerRig/" + name);
                if (tex != null) mat.mainTexture = tex;
            }
            // mismo toon que el minero modelado (borde iluminado suave, sombras no muy oscuras)
            if (mat.HasProperty("_Floor")) mat.SetFloat("_Floor", 0.5f);
            if (mat.HasProperty("_Rim")) mat.SetFloat("_Rim", 0.22f);
            return mat;
        }
    }
}
