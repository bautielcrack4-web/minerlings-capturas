using UnityEditor;
using UnityEngine;

/// <summary>
/// Sonidos reales (Assets/Resources/Audio, fuentes CC0: ver docs/SONIDOS.md). Efectos cortos: mono, Vorbis,
/// descomprimidos al cargar (sin costo de CPU al sonar). Ambientes y musica (amb_*, music_*): Vorbis comprimido en
/// memoria (musica en streaming) a calidad media, para que el APK no pase de 30 MB.
/// </summary>
public sealed class AudioImport : AssetPostprocessor
{
    // subir el numero obliga a Unity a reimportar los sonidos con los ajustes nuevos
    public override uint GetVersion() { return 2; }

    void OnPreprocessAudio()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Resources/Audio/")) return;
        var ai = (AudioImporter)assetImporter;
        string n = System.IO.Path.GetFileNameWithoutExtension(p);
        bool music = n.StartsWith("music_"), amb = n.StartsWith("amb_");
        bool stereo = music || n == "amb_sea_near" || n.StartsWith("wave_");
        ai.forceToMono = !stereo;
        ai.loadInBackground = music || amb;
        var s = ai.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        if (music) { s.loadType = AudioClipLoadType.Streaming; s.quality = 0.22f; }
        else if (amb) { s.loadType = AudioClipLoadType.CompressedInMemory; s.quality = 0.32f; }
        else { s.loadType = AudioClipLoadType.DecompressOnLoad; s.quality = 0.5f; }
        s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        s.sampleRateOverride = 32000u;
        s.preloadAudioData = !music;
        ai.defaultSampleSettings = s;
    }
}
