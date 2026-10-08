using System.IO;
using UnityEngine;

/// <summary>Depuracion: guarda en PNG los sprites generados por codigo (anillos, redondeados, iconos) para revisarlos.</summary>
public static class SpriteDump
{
    public static void Run()
    {
        string dir = System.Environment.GetEnvironmentVariable("DUMP_DIR") ?? "/tmp/spritedump";
        Directory.CreateDirectory(dir);
        Save(Mineros.UI.Icons.Ring(3f), dir, "ring3");
        Save(Mineros.UI.Icons.Ring(8f), dir, "ring8");
        Save(Mineros.UI.Kit.Round(12), dir, "round12");
        foreach (var k in new[] { "gold", "gem", "power", "speed", "money", "rebirth", "tools", "shop" })
        {
            try { Save(Mineros.UI.Icons.Get(k), dir, "icon_" + k); } catch (System.Exception e) { Debug.Log("icon " + k + " " + e.Message); }
        }
        UnityEditor.EditorApplication.Exit(0);
    }

    static void Save(Sprite s, string dir, string name)
    {
        if (s == null) return;
        var t = s.texture;
        var rt = RenderTexture.GetTemporary(t.width, t.height, 0);
        Graphics.Blit(t, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var o = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
        o.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0); o.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), o.EncodeToPNG());
        Debug.Log("dump " + name + " " + t.width + "x" + t.height + " filter=" + t.filterMode + " mip=" + t.mipmapCount + " wrap=" + t.wrapMode);
    }
}
