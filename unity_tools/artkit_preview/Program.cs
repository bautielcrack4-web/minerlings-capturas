using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Mineros.Art.Gen;

// Uso: dotnet run -- <carpeta_salida>
// Genera <kind>_<bioma>_<variante>.obj/.mtl para todos los accesorios y un resumen de triangulos.
// El .obj sale en mano derecha (x invertida y caras invertidas respecto de Unity) para abrirlo en cualquier visor.
static class Program
{
    static readonly CultureInfo C = CultureInfo.InvariantCulture;

    static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "/tmp/artkit_preview/obj";
        Directory.CreateDirectory(dir);
        var summary = new StringBuilder();
        int worst = 0; string worstName = "";
        long total = 0; int count = 0;
        var kindMax = new Dictionary<string, int>();
        foreach (var kind in PropGen.Kinds)
        {
            int nv = PropGen.Variants(kind);
            for (int bm = 0; bm < 4; bm++)
                for (int v = 0; v < nv; v++)
                {
                    var d = PropGen.Build(kind, bm, v);
                    if (d == null) { Console.Error.WriteLine("FALTA " + kind); return 1; }
                    string name = kind + "_" + bm + "_" + v;
                    Export(Path.Combine(dir, name), d);
                    int tris = d.TriCount;
                    total += tris; count++;
                    if (tris > worst) { worst = tris; worstName = name; }
                    if (!kindMax.ContainsKey(kind) || kindMax[kind] < tris) kindMax[kind] = tris;
                    string bad = Check(d);
                    summary.AppendLine(name + " tris=" + tris + " verts=" + d.verts.Length + " mats=" + d.mats.Length + (bad != null ? " PROBLEMA: " + bad : ""));
                }
        }
        var g = PropGen.GemShape(); Export(Path.Combine(dir, "GemMesh"), g);
        summary.AppendLine("GemMesh tris=" + g.TriCount + " " + (Check(g) ?? ""));
        for (int s = 0; s < 8; s++)
        {
            var c = PropGen.CrystalShape(s); Export(Path.Combine(dir, "CrystalMesh_" + s), c);
            summary.AppendLine("CrystalMesh_" + s + " tris=" + c.TriCount + " " + (Check(c) ?? ""));
        }
        File.WriteAllText(Path.Combine(dir, "summary.txt"), summary.ToString());
        Console.WriteLine(summary.ToString());
        Console.WriteLine("props=" + count + " mediaTris=" + (total / count) + " maxTris=" + worst + " (" + worstName + ")");
        foreach (var kv in kindMax) Console.WriteLine("  max " + kv.Key + " = " + kv.Value);
        return 0;
    }

    static string Check(MeshData d)
    {
        // indices validos, normales unitarias y sin NaN, normal de vertice alineada con la cara
        int bad = 0, nan = 0, dis = 0; string firstBad = "";
        foreach (var v in d.verts) if (float.IsNaN(v.x + v.y + v.z)) nan++;
        foreach (var n in d.normals) { float l = n.Length; if (float.IsNaN(l) || Math.Abs(l - 1f) > 0.01f) nan++; }
        foreach (var t in d.tris)
        {
            for (int i = 0; i < t.Length; i += 3)
            {
                if (t[i] >= d.verts.Length || t[i + 1] >= d.verts.Length || t[i + 2] >= d.verts.Length) { bad++; continue; }
                var a = d.verts[t[i]]; var b = d.verts[t[i + 1]]; var c = d.verts[t[i + 2]];
                var fn = V3.Cross(b - a, c - a);
                var vn = d.normals[t[i]] + d.normals[t[i + 1]] + d.normals[t[i + 2]];
                if (V3.Dot(fn.Normalized, vn.Normalized) < -0.2f) { dis++; if (dis == 1) firstBad = "tri@" + a + " " + b + " " + c + " n=" + d.normals[t[i]] + "|" + d.normals[t[i+1]] + "|" + d.normals[t[i+2]] + " fn=" + fn.Normalized; }
            }
        }
        if (bad + nan + dis == 0) return null;
        return "indices=" + bad + " nan=" + nan + " normal_contraria=" + dis + " " + firstBad;
    }

    static void Export(string path, MeshData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("mtllib " + Path.GetFileName(path) + ".mtl");
        foreach (var v in d.verts) sb.AppendLine("v " + (-v.x).ToString("0.####", C) + " " + v.y.ToString("0.####", C) + " " + v.z.ToString("0.####", C));
        foreach (var n in d.normals) sb.AppendLine("vn " + (-n.x).ToString("0.####", C) + " " + n.y.ToString("0.####", C) + " " + n.z.ToString("0.####", C));
        for (int m = 0; m < d.tris.Length; m++)
        {
            sb.AppendLine("usemtl m" + m);
            var t = d.tris[m];
            for (int i = 0; i < t.Length; i += 3)
            {
                // invertir el orden (a, c, b) por el espejo en X
                int a = t[i] + 1, b = t[i + 1] + 1, c = t[i + 2] + 1;
                sb.AppendLine("f " + a + "//" + a + " " + c + "//" + c + " " + b + "//" + b);
            }
        }
        File.WriteAllText(path + ".obj", sb.ToString());
        var mt = new StringBuilder();
        if (d.hasAnchor) mt.AppendLine("# anchor " + d.anchor);
        for (int m = 0; m < d.mats.Length; m++)
        {
            var s = d.mats[m];
            mt.AppendLine("newmtl m" + m);
            mt.AppendLine("Kd " + s.color.r.ToString("0.####", C) + " " + s.color.g.ToString("0.####", C) + " " + s.color.b.ToString("0.####", C));
            mt.AppendLine("Ks " + s.spec.ToString("0.####", C) + " " + s.spec.ToString("0.####", C) + " " + s.spec.ToString("0.####", C));
            mt.AppendLine("Ke " + s.emis.r.ToString("0.####", C) + " " + s.emis.g.ToString("0.####", C) + " " + s.emis.b.ToString("0.####", C));
        }
        File.WriteAllText(path + ".mtl", mt.ToString());
    }
}
