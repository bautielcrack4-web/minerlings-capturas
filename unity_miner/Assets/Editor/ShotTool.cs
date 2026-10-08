using UnityEditor;

/// <summary>
/// Capturas automaticas en batch (con graficos): entra en Play y Mineros.Game.ShotRunner hace el resto
/// (arma el escenario, guarda los PNG y cierra Unity).
/// Uso: bash unity_tools/unity_shot.sh &lt;escenario&gt; [carpeta]
/// </summary>
public static class ShotTool
{
    public static void Capture()
    {
        // experimentos de rendimiento: PERF_DYN=1 activa el batching dinamico en la plataforma activa
        string dyn = System.Environment.GetEnvironmentVariable("PERF_DYN");
        if (!string.IsNullOrEmpty(dyn))
        {
            var m = typeof(PlayerSettings).GetMethod("SetBatchingForPlatform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (m != null) m.Invoke(null, new object[] { EditorUserBuildSettings.activeBuildTarget, 1, dyn == "1" ? 1 : 0 });
            UnityEngine.Debug.Log("perf: batching dinamico=" + dyn + " metodo=" + (m != null));
        }
        EditorApplication.isPlaying = true;
    }
}
