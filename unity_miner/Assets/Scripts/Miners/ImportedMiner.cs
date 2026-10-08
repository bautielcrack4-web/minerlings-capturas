using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Mineros.Miners
{
    /// <summary>
    /// Minero modelado en Blender (unity_tools/blender_miner): lee Resources/Miner/miner.json y arma una malla por pieza
    /// (submallas por canal: color de vertice, piel, pelo, casco, lente). Las mallas se comparten entre todos los mineros.
    /// </summary>
    public static class ImportedMiner
    {
        [Serializable] public sealed class Sub { public string channel; public float[] pos; public float[] nrm; public float[] col; public int[] idx; }
        [Serializable] public sealed class Group { public string name; public string parent; public float[] pivot; public Sub[] subs; }
        [Serializable] public sealed class Root { public Group[] groups; public float handLen; public float height; }

        static Root data;
        static bool tried;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static Material vcMat;

        public static Root Data
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    var ta = Resources.Load<TextAsset>("Miner/miner");
                    if (ta != null) data = JsonUtility.FromJson<Root>(ta.text);
                }
                return data;
            }
        }

        public static Mesh MeshOf(Group g)
        {
            Mesh m;
            if (meshes.TryGetValue(g.name, out m) && m != null) return m;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var c = new List<Color>();
            m = new Mesh { name = "Minero_" + g.name };
            var tris = new List<int[]>();
            foreach (var s in g.subs)
            {
                int b = v.Count;
                for (int i = 0; i + 2 < s.pos.Length; i += 3)
                {
                    v.Add(new Vector3(s.pos[i], s.pos[i + 1], s.pos[i + 2]));
                    n.Add(new Vector3(s.nrm[i], s.nrm[i + 1], s.nrm[i + 2]));
                    c.Add(new Color(s.col[i], s.col[i + 1], s.col[i + 2], 1f));
                }
                var t = new int[s.idx.Length];
                for (int i = 0; i < t.Length; i++) t[i] = s.idx[i] + b;
                tris.Add(t);
            }
            m.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetColors(c);
            m.subMeshCount = tris.Count;
            for (int i = 0; i < tris.Count; i++) m.SetTriangles(tris[i], i, false);
            m.RecalculateBounds();
            m.UploadMeshData(false);   // legible: el minero se fusiona en una malla con esqueleto (MinerModel.MergeSkinned)
            meshes[g.name] = m;
            return m;
        }

        /// <summary>
        /// La misma pieza con la piel en otro tono (cara y manos): los mineros eran todos iguales porque la piel viene
        /// pintada en los vertices y los tonos por variante nunca se aplicaban. Una copia por pieza y tono.
        /// </summary>
        public static Mesh MeshOf(Group g, int skin)
        {
            var src = MeshOf(g);
            if (skin < 0) return src;
            string key = g.name + "#" + skin;
            Mesh m;
            if (meshes.TryGetValue(key, out m) && m != null) return m;
            var cols = src.colors;
            bool any = false;
            for (int i = 0; i < cols.Length; i++)
                if (MinerFace.IsSkin(cols[i])) { cols[i] = MinerFace.Tint(cols[i], skin); any = true; }
            if (!any) { meshes[key] = src; return src; }
            m = Object.Instantiate(src);
            m.name = src.name + "_piel" + skin;
            m.colors = cols;
            m.UploadMeshData(false);
            meshes[key] = m;
            return m;
        }

        /// <summary>Material compartido de color por vertice (MinerToonVC), con los parametros del toon del minero.</summary>
        public static Material VertexColorMaterial()
        {
            if (vcMat != null) return vcMat;
            var sh = Shader.Find("Mineros/MinerToonVC");
            if (sh == null) sh = Shader.Find("Standard");
            vcMat = new Material(sh) { name = "Miner_vc", hideFlags = HideFlags.DontSave };
            vcMat.color = Color.white;
            if (vcMat.HasProperty("_Rim")) vcMat.SetFloat("_Rim", 0.22f);
            if (vcMat.HasProperty("_Floor")) vcMat.SetFloat("_Floor", 0.42f);
            return vcMat;
        }
    }
}
