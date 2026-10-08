using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mineros.Fx;

/// <summary>Uso: dotnet run --project FxExport -- /ruta/salida   (escribe tex_*.rgba y effects.json)</summary>
static class Program
{
    static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "/tmp/fx_preview/data";
        Directory.CreateDirectory(dir);
        foreach (FxTex t in Enum.GetValues(typeof(FxTex)))
        {
            byte[] px = FxTexGen.Make(t);
            File.WriteAllBytes(Path.Combine(dir, "tex_" + t + "_" + FxTexGen.SizeOf(t) + ".rgba"), px);
        }
        var opt = new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };
        opt.Converters.Add(new JsonStringEnumConverter());
        File.WriteAllText(Path.Combine(dir, "effects.json"), JsonSerializer.Serialize(FxLib.All, opt));
        Console.WriteLine("ok: " + FxLib.All.Count + " efectos, " + Enum.GetValues(typeof(FxTex)).Length + " texturas -> " + dir);
        return 0;
    }
}
