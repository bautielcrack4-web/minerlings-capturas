using UnityEditor;
using UnityEngine;

/// <summary>Ajustes del proyecto aplicados por codigo (vertical, nombre, empresa). Corre al abrir el editor.</summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    static ProjectSetup()
    {
        PlayerSettings.productName = "Minerlings";
        PlayerSettings.companyName = "Baubagas Games";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        EditorApplication.delayCall += SetIcon;
    }

    /// <summary>Icono de la app (Assets/Icon/app_icon.png, 1024x1024) para todas las plataformas.</summary>
    public static void SetIcon()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon/app_icon.png");
        if (tex == null) { Debug.LogWarning("icono: falta Assets/Icon/app_icon.png"); return; }
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
    }
}
