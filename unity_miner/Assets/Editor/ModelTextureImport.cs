using UnityEditor;

/// <summary>
/// Texturas de los modelos 3D (Assets/Resources/Island): en Android van en ASTC 8x8 con mipmaps (se ven igual en
/// pantalla y pesan la mitad que ETC2). Las de interfaz siguen sin compresion (UiTextureImport).
/// </summary>
public sealed class ModelTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (path.Contains("/Resources/RoomKit/"))
        {
            // piezas del kit de habitaciones modeladas a mano: textura de paleta (un color por celda de 8 px). Sin mipmaps,
            // sin filtro y sin compresion: si no, de lejos los colores vecinos se mezclan.
            var tk = (TextureImporter)assetImporter;
            tk.mipmapEnabled = false;
            tk.filterMode = UnityEngine.FilterMode.Point;
            tk.textureCompression = TextureImporterCompression.Uncompressed;
            tk.isReadable = true;   // se apilan en un atlas al cargar (IslandArt.BuildKitAtlas): son de 32 px, no pesan
            return;
        }
        if (!path.Contains("/Resources/Island/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.mipmapEnabled = true;
        var s = ti.GetPlatformTextureSettings("Android");
        s.overridden = true;
        s.format = TextureImporterFormat.ASTC_8x8;
        s.maxTextureSize = 1024;
        ti.SetPlatformTextureSettings(s);
    }
}
