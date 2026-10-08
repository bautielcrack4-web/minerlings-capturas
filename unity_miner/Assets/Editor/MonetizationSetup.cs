using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deja listos AdMob y las compras antes de cada build (lo llaman BuildAndroid y BuildIOS):
/// - Identificadores de AdMob: los REALES de la cuenta de Baubagas Games (app "Minerlings", bloque "rewarded_main") en
///   las builds de tienda (AAB, iOS). El APK de prueba usa los de PRUEBA publicos de Google ("Test Ad", no pagan), asi
///   el dueño puede tocar anuncios en su telefono sin arriesgar la cuenta. Se puede forzar con ADMOB_TEST=1 / 0 y pisar
///   cada id con las variables ADMOB_ANDROID_APP_ID, ADMOB_IOS_APP_ID, ADMOB_REWARDED_ANDROID y ADMOB_REWARDED_IOS.
/// - Plantillas de Gradle propias, para que el resolvedor de Google agregue las dependencias de Android al build.
/// - Resolucion de dependencias de Android forzada (tambien en modo batch).
/// </summary>
public static class MonetizationSetup
{
    // https://developers.google.com/admob/android/test-ads  y  /ios/test-ads (publicos, sin cuenta)
    public const string TestAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
    public const string TestIosAppId = "ca-app-pub-3940256099942544~1458002511";

    public const string TestRewardedAndroid = "ca-app-pub-3940256099942544/5224354917";
    public const string TestRewardedIos = "ca-app-pub-3940256099942544/1712485313";

    // cuenta de AdMob de Baubagas Games (pub-7920322916204338): app "Minerlings", bloque con premio "rewarded_main"
    public const string AndroidAppId = "ca-app-pub-7920322916204338~1890578349";
    public const string IosAppId = "ca-app-pub-7920322916204338~7932233351";
    public const string RewardedAndroid = "ca-app-pub-7920322916204338/5342738191";
    public const string RewardedIos = "ca-app-pub-7920322916204338/2692524123";

    static bool test;

    /// <summary>`store` = build para la tienda (ids reales); false = APK de prueba (ids de prueba de Google).</summary>
    public static void Apply(bool android, bool store)
    {
        string t = Environment.GetEnvironmentVariable("ADMOB_TEST");
        test = t == "1" || (t != "0" && !store);
        Debug.Log("monetizacion: anuncios " + (test ? "de PRUEBA" : "REALES"));
        ConfigureAdMob();
        WriteRuntimeConfig();
        if (android)
        {
            EnsureGradleTemplates();
            ResolveAndroid();
        }
    }

    static string Env(string k, string def)
    {
        string v = Environment.GetEnvironmentVariable(k);
        return string.IsNullOrEmpty(v) ? def : v;
    }

    static void ConfigureAdMob()
    {
        var t = FindType("GoogleMobileAds.Editor.GoogleMobileAdsSettings");
        if (t == null) { Debug.LogWarning("monetizacion: no esta el paquete de AdMob"); return; }
        var load = t.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var inst = load != null ? load.Invoke(null, null) as ScriptableObject : null;
        if (inst == null) { Debug.LogWarning("monetizacion: no se pudo crear la configuracion de AdMob"); return; }
        var so = new SerializedObject(inst);
        so.FindProperty("adMobAndroidAppId").stringValue = Env("ADMOB_ANDROID_APP_ID", test ? TestAndroidAppId : AndroidAppId);
        so.FindProperty("adMobIOSAppId").stringValue = Env("ADMOB_IOS_APP_ID", test ? TestIosAppId : IosAppId);
        var ut = so.FindProperty("userTrackingUsageDescription");
        if (ut != null) ut.stringValue = "We use this to show you more relevant reward ads. The game works the same if you say no.";
        var lang = so.FindProperty("userLanguage");
        if (lang != null) lang.stringValue = "es";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(inst);
        AssetDatabase.SaveAssets();
        Debug.Log("monetizacion: AdMob android=" + so.FindProperty("adMobAndroidAppId").stringValue + " ios=" + so.FindProperty("adMobIOSAppId").stringValue);
    }

    /// <summary>Ids de los anuncios con premio para el juego (Resources/monetizacion.json).</summary>
    static void WriteRuntimeConfig()
    {
        Directory.CreateDirectory("Assets/Resources");
        string json = "{\"rewardedAndroid\":\"" + Env("ADMOB_REWARDED_ANDROID", test ? TestRewardedAndroid : RewardedAndroid) + "\",\"rewardedIos\":\"" + Env("ADMOB_REWARDED_IOS", test ? TestRewardedIos : RewardedIos) + "\"}";
        File.WriteAllText("Assets/Resources/monetizacion.json", json);
        AssetDatabase.ImportAsset("Assets/Resources/monetizacion.json");
    }

    /// <summary>Copia las plantillas de Gradle de Unity a Assets/Plugins/Android (asi el resolvedor puede inyectar dependencias).</summary>
    static void EnsureGradleTemplates()
    {
        string src = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/Tools/GradleTemplates");
        string dst = "Assets/Plugins/Android";
        Directory.CreateDirectory(dst);
        foreach (var f in new[] { "mainTemplate.gradle", "settingsTemplate.gradle", "gradleTemplate.properties" })
        {
            string to = Path.Combine(dst, f);
            if (File.Exists(to)) continue;
            string from = Path.Combine(src, f);
            if (File.Exists(from)) File.Copy(from, to);
            else Debug.LogWarning("monetizacion: falta la plantilla " + from);
        }
        // AndroidX (lo piden los anuncios)
        string props = Path.Combine(dst, "gradleTemplate.properties");
        if (File.Exists(props))
        {
            string p = File.ReadAllText(props);
            if (!p.Contains("android.useAndroidX")) p += "\nandroid.useAndroidX=true\nandroid.enableJetifier=true\n";
            File.WriteAllText(props, p);
        }
        AssetDatabase.Refresh();
    }

    static void ResolveAndroid()
    {
        var t = FindType("GooglePlayServices.PlayServicesResolver");
        if (t == null) { Debug.LogWarning("monetizacion: no esta el resolvedor de dependencias de Google"); return; }
        var m = t.GetMethod("ResolveSync", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(bool) }, null);
        if (m == null) { Debug.LogWarning("monetizacion: ResolveSync no existe en esta version del resolvedor"); return; }
        object ok = m.Invoke(null, new object[] { true });
        Debug.Log("monetizacion: dependencias de Android resueltas=" + ok);
    }

    static Type FindType(string fullName)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType(fullName, false);
            if (t != null) return t;
        }
        return null;
    }
}
