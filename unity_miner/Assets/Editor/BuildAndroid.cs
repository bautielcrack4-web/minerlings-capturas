using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Build de Android por linea de comandos (APK de prueba, IL2CPP arm64, firmado con la clave de depuracion).
/// Uso: Unity -batchmode -projectPath unity_miner -executeMethod BuildAndroid.Apk -buildOut Builds/Minerlings.apk -quit
/// La escena se arma por codigo (Bootstrap); aqui solo se asegura una escena vacia para la build.
/// </summary>
public static class BuildAndroid
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    public static void Apk() { Build(false, Arg("-buildOut", "Builds/Minerlings.apk")); }

    /// <summary>
    /// AAB firmado para Google Play (lo usa Codemagic). Clave de subida por variables de entorno: CM_KEYSTORE_PATH,
    /// CM_KEYSTORE_PASSWORD, CM_KEY_ALIAS y CM_KEY_PASSWORD (Codemagic las define con android_signing).
    /// </summary>
    public static void Aab() { Build(true, Arg("-buildOut", "Builds/Minerlings.aab")); }

    static void Build(bool bundle, string outPath)
    {
        EnsureScene();
        ProjectSetup.SetIcon();
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, GameVersion.AndroidId);
        PlayerSettings.bundleVersion = GameVersion.Name;
        PlayerSettings.Android.bundleVersionCode = GameVersion.Code;
        // sin pantalla de inicio de Unity (Unity 6 lo permite): menos espera y 2.7 MB menos
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        // Activity clasica en vez de GameActivity: no arrastra las bibliotecas de androidx (APK ~2 MB mas chico)
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        // APK mas chico: quitar codigo sin usar (nivel medio, seguro con JsonUtility) y compilar IL2CPP por tamaño
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, Environment.GetEnvironmentVariable("STRIP_HIGH") == "1" ? ManagedStrippingLevel.High : ManagedStrippingLevel.Medium);
        PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.Android, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.gpuSkinning = true;   // los mineros fusionados se deforman en la placa de video, no en el procesador
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        EditorUserBuildSettings.buildAppBundle = bundle;
        PlayerSettings.Android.useCustomKeystore = false;
        if (bundle)
        {
            string ks = System.Environment.GetEnvironmentVariable("CM_KEYSTORE_PATH");
            if (!string.IsNullOrEmpty(ks))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = ks;
                PlayerSettings.Android.keystorePass = System.Environment.GetEnvironmentVariable("CM_KEYSTORE_PASSWORD");
                PlayerSettings.Android.keyaliasName = System.Environment.GetEnvironmentVariable("CM_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = System.Environment.GetEnvironmentVariable("CM_KEY_PASSWORD");
            }
            string bn = System.Environment.GetEnvironmentVariable("BUILD_NUMBER");
            int code;
            if (int.TryParse(bn, out code) && code > GameVersion.Code) PlayerSettings.Android.bundleVersionCode = code;
        }
        HighQualityAndroid();
        MonetizationSetup.Apply(true, bundle);   // AdMob (reales en el AAB, de prueba en el APK) y dependencias de Android
        PlayerSettings.Android.minifyRelease = true;   // R8: saca el codigo sin usar de los SDK (APK mas chico)
        // reglas propias de R8 (Assets/Plugins/Android/proguard-user.txt): el puente de AdMob trae codigo NextGen que no usamos
        var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var cp = ps.FindProperty("useCustomProguardFile");
        if (cp != null) { cp.boolValue = true; ps.ApplyModifiedPropertiesWithoutUndo(); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outPath,
            target = BuildTarget.Android,
            // sin LZ4: el APK ya comprime con deflate y sale mas chico que con LZ4HC (31.2 vs 30.2 MB)
            options = BuildOptions.None,
        };
        BuildReport r = BuildPipeline.BuildPlayer(opts);
        var s = r.summary;
        Debug.Log("build " + s.result + " size=" + (s.totalSize / (1024 * 1024)) + "MB errors=" + s.totalErrors + " warnings=" + s.totalWarnings
            + " time=" + s.totalTime);
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    /// <summary>
    /// Android arranca con el nivel de calidad "Medium" (sin antialiasing, sombras de baja resolucion). El MSAA del
    /// framebuffer se decide al crear la superficie, asi que cambiarlo en tiempo de juego no alcanza: se usa "Ultra"
    /// con MSAA 4x, filtrado anisotropico, sombras suaves de alta resolucion y texturas a resolucion completa.
    /// </summary>
    public static void HighQualityAndroid()
    {
        var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
        var so = new SerializedObject(qs);
        var levels = so.FindProperty("m_QualitySettings");
        int ultra = levels.arraySize - 1;
        var q = levels.GetArrayElementAtIndex(ultra);
        q.FindPropertyRelative("antiAliasing").intValue = 4;
        q.FindPropertyRelative("anisotropicTextures").intValue = 2;
        q.FindPropertyRelative("shadows").intValue = 2;
        q.FindPropertyRelative("shadowResolution").intValue = 3;
        q.FindPropertyRelative("shadowCascades").intValue = 1;
        q.FindPropertyRelative("globalTextureMipmapLimit").intValue = 0;
        q.FindPropertyRelative("resolutionScalingFixedDPIFactor").floatValue = 1f;
        var per = so.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; i < per.arraySize; i++)
        {
            var e = per.GetArrayElementAtIndex(i);
            var k = e.FindPropertyRelative("first");
            if (k != null && (k.stringValue == "Android" || k.stringValue == "iPhone")) e.FindPropertyRelative("second").intValue = ultra;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    /// <summary>Escena vacia (sin camara: WorldController crea la suya) registrada en Build Settings.</summary>
    public static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(sc, ScenePath);
            AssetDatabase.Refresh();
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    public static string Arg(string name, string def)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return def;
    }
}
