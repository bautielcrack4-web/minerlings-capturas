using UnityEditor;
using UnityEngine;

/// <summary>
/// Texturas de interfaz (Assets/Resources/UI): sin compresion, sin mipmaps, filtro bilineal y borde fijo.
/// La compresion de Android (ETC2/ASTC) y los mipmaps son los que "muerden" los bordes redondeados y el alfa.
/// </summary>
public sealed class UiTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/UI/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.sRGBTexture = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 2048;
        foreach (var plat in new[] { "Android", "iPhone", "Standalone" })
        {
            var s = ti.GetPlatformTextureSettings(plat);
            s.overridden = true;
            s.format = TextureImporterFormat.RGBA32;
            s.textureCompression = TextureImporterCompression.Uncompressed;
            s.maxTextureSize = 2048;
            ti.SetPlatformTextureSettings(s);
        }
    }
}
