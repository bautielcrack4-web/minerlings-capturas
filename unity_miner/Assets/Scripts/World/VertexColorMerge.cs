using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.World
{
    /// <summary>
    /// Junta las submallas de una malla que usan MinerToon con los mismos parametros (solo cambia el color y no emiten)
    /// en una sola submalla con el color en los vertices y el shader MinerToonVC. El cañon pasa de ~50 llamadas de
    /// dibujo por pasada (una por color) a unas pocas. Las submallas con emision u otro shader quedan como estaban.
    /// </summary>
    public static class VertexColorMerge
    {
        static Shader toon, toonVC;
        static readonly Dictionary<string, Material> vcMats = new Dictionary<string, Material>();

        // buffers reutilizados
        static readonly List<Vector3> sv = new List<Vector3>(), sn = new List<Vector3>();
        static readonly List<Vector3> dv = new List<Vector3>(), dn = new List<Vector3>();
        static readonly List<Color> dc = new List<Color>();
        static readonly List<int> idx = new List<int>();
        static readonly Dictionary<long, int> remap = new Dictionary<long, int>();

        /// <summary>Reescribe la malla en el lugar y devuelve los materiales nuevos (o los mismos si no hay nada que juntar).</summary>
        public static Material[] Apply(Mesh mesh, Material[] mats)
        {
            if (mesh == null || mats == null || mats.Length < 2 || mesh.subMeshCount != mats.Length) return mats;
            if (toon == null) toon = Shader.Find("Mineros/MinerToon");
            if (toonVC == null) toonVC = Shader.Find("Mineros/MinerToonVC");
            if (toon == null || toonVC == null) return mats;

            // grupos: clave de parametros -> submallas; las que no se pueden juntar van solas
            var groups = new List<List<int>>();
            var keys = new List<string>();
            var byKey = new Dictionary<string, int>();
            int mergeable = 0;
            for (int s = 0; s < mats.Length; s++)
            {
                string k = KeyOf(mats[s]);
                if (k == null) { groups.Add(new List<int> { s }); keys.Add(null); continue; }
                mergeable++;
                int g;
                if (!byKey.TryGetValue(k, out g)) { g = groups.Count; byKey[k] = g; groups.Add(new List<int>()); keys.Add(k); }
                groups[g].Add(s);
            }
            if (mergeable < 2 || groups.Count == mats.Length) return mats;

            mesh.GetVertices(sv);
            mesh.GetNormals(sn);
            bool hasN = sn.Count == sv.Count;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            dv.Clear(); dn.Clear(); dc.Clear();
            var tris = new List<int[]>(groups.Count);
            var outMats = new Material[groups.Count];
            for (int g = 0; g < groups.Count; g++)
            {
                remap.Clear();
                var all = new List<int>();
                foreach (int s in groups[g])
                {
                    Color c = keys[g] != null ? mats[s].color : Color.white;
                    if (keys[g] != null && linear) c = c.linear;
                    mesh.GetTriangles(idx, s);
                    for (int i = 0; i < idx.Count; i++)
                    {
                        long rk = ((long)s << 32) | (uint)idx[i];
                        int ni;
                        if (!remap.TryGetValue(rk, out ni))
                        {
                            ni = dv.Count;
                            remap[rk] = ni;
                            dv.Add(sv[idx[i]]);
                            dn.Add(hasN ? sn[idx[i]] : Vector3.up);
                            dc.Add(c);
                        }
                        all.Add(ni);
                    }
                }
                tris.Add(all.ToArray());
                outMats[g] = keys[g] != null ? VcMat(mats[groups[g][0]], keys[g]) : mats[groups[g][0]];
            }

            string name = mesh.name;
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = dv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(dv);
            mesh.SetNormals(dn);
            mesh.SetColors(dc);
            mesh.subMeshCount = tris.Count;
            for (int g = 0; g < tris.Count; g++) mesh.SetTriangles(tris[g], g, false);
            mesh.RecalculateBounds();
            return outMats;
        }

        /// <summary>Clave de los parametros que no son el color; null si la submalla no se puede juntar.</summary>
        static string KeyOf(Material m)
        {
            if (m == null || m.shader != toon) return null;
            Color e = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            if (e.r > 0.001f || e.g > 0.001f || e.b > 0.001f) return null;
            return F(m, "_Rim") + "|" + F(m, "_Floor") + "|" + F(m, "_Spec") + "|" + F(m, "_Gloss");
        }

        static string F(Material m, string p) { return m.HasProperty(p) ? m.GetFloat(p).ToString("0.###") : "-"; }

        static Material VcMat(Material src, string key)
        {
            Material m;
            if (vcMats.TryGetValue(key, out m) && m != null) return m;
            m = new Material(toonVC);
            m.name = "ToonVC_" + key;
            m.color = Color.white;
            foreach (var p in new[] { "_Rim", "_Floor", "_Spec", "_Gloss" })
                if (src.HasProperty(p)) m.SetFloat(p, src.GetFloat(p));
            m.SetColor("_EmissionColor", Color.black);
            vcMats[key] = m;
            return m;
        }
    }
}
