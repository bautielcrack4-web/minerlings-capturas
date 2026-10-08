using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

/// <summary>
/// Exporta el proyecto de Xcode (lo compila y firma Codemagic en una Mac; ver codemagic.yaml en la raiz del repo).
/// Uso: Unity -batchmode -projectPath unity_miner -executeMethod BuildIOS.Xcode -buildOut ios -quit
/// Variables opcionales: BUNDLE_ID (identificador de la app) y BUILD_NUMBER (numero de build de TestFlight).
/// </summary>
public static class BuildIOS
{
    public static void Xcode()
    {
        string outPath = BuildAndroid.Arg("-buildOut", "ios");
        BuildAndroid.EnsureScene();
        ProjectSetup.SetIcon();
        var t = UnityEditor.Build.NamedBuildTarget.iOS;
        string id = System.Environment.GetEnvironmentVariable("BUNDLE_ID");
        PlayerSettings.SetApplicationIdentifier(t, string.IsNullOrEmpty(id) ? GameVersion.IosId : id);
        PlayerSettings.bundleVersion = GameVersion.Name;
        string bn = System.Environment.GetEnvironmentVariable("BUILD_NUMBER");
        PlayerSettings.iOS.buildNumber = string.IsNullOrEmpty(bn) ? GameVersion.Code.ToString() : bn;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(t, 1);   // arm64
        PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Medium);
        PlayerSettings.SetIl2CppCodeGeneration(t, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.iOS.targetOSVersionString = "13.0";
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        PlayerSettings.iOS.appleEnableAutomaticSigning = false;   // Codemagic pone los perfiles (xcode-project use-profiles)
        PlayerSettings.iOS.requiresFullScreen = true;
        PlayerSettings.iOS.hideHomeButton = false;
        PlayerSettings.statusBarHidden = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, true);   // Metal
        PlayerSettings.gpuSkinning = true;
        BuildAndroid.HighQualityAndroid();   // tambien deja iPhone en "Ultra" con MSAA 4x
        MonetizationSetup.Apply(false, true);
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = outPath,
            target = BuildTarget.iOS,
            options = BuildOptions.None,
        };
        BuildReport r = BuildPipeline.BuildPlayer(opts);
        var s = r.summary;
        Debug.Log("build ios " + s.result + " errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " out=" + Path.GetFullPath(outPath));
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

#if UNITY_IOS
    /// <summary>Info.plist: sin cifrado propio (evita la pregunta de exportacion en cada subida) y textos de permisos.</summary>
    [PostProcessBuild(100)]
    public static void Plist(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        // inglés como idioma principal; español con su propio nombre y textos (es.lproj)
        root.SetString("CFBundleDevelopmentRegion", "en");
        var langs = root.CreateArray("CFBundleLocalizations");
        langs.AddString("en");
        langs.AddString("es");
        root.SetString("CFBundleDisplayName", "Minerlings");
        // la pantalla de seguimiento (ATT) solo se muestra si hay anuncios; el texto tiene que existir igual
        root.SetString("NSUserTrackingUsageDescription",
            "We use this to show you more relevant reward ads. The game works the same if you say no.");
        WriteStrings(path, "en", "Minerlings", "We use this to show you more relevant reward ads. The game works the same if you say no.");
        WriteStrings(path, "es", "Minerlings", "Usamos este permiso para mostrarte anuncios con premio más relevantes. El juego funciona igual si no lo aceptás.");
        plist.WriteToFile(plistPath);
    }

    /// <summary>Carpeta xx.lproj con InfoPlist.strings (nombre y permiso traducidos), agregada al proyecto de Xcode.</summary>
    static void WriteStrings(string path, string lang, string name, string att)
    {
        string dir = Path.Combine(path, lang + ".lproj");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "InfoPlist.strings"),
            "\"CFBundleDisplayName\" = \"" + name + "\";\n\"NSUserTrackingUsageDescription\" = \"" + att + "\";\n");
        string projPath = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(path);
        var proj = new UnityEditor.iOS.Xcode.PBXProject();
        proj.ReadFromFile(projPath);
        string target = proj.GetUnityMainTargetGuid();
        string guid = proj.AddFolderReference(lang + ".lproj", lang + ".lproj");
        proj.AddFileToBuild(target, guid);
        proj.WriteToFile(projPath);
    }
#endif
}
