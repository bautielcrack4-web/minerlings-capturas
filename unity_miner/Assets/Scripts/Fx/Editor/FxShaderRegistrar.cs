using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mineros.Fx.EditorTools
{
    /// <summary>
    /// Shader.Find solo encuentra en una build los shaders referenciados por algun material de escena o listados en
    /// "Always Included Shaders". Los efectos y el toon se crean por codigo (sin materiales guardados), asi que se
    /// registran aqui: al abrir el editor y antes de cada build.
    /// </summary>
    [InitializeOnLoad]
    public sealed class FxShaderRegistrar : IPreprocessBuildWithReport
    {
        static readonly string[] Names =
        {
            "Mineros/FxParticleAdd", "Mineros/FxParticleAlpha",
            "Mineros/MinerToon", "Mineros/MinerToonVC", "Mineros/MinerToonTex", "Mineros/MinerFace", "Mineros/MinerVest",
            "Mineros/CardArt",
        };

        static FxShaderRegistrar()
        {
            EditorApplication.delayCall += Register;
        }

        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report) { Register(); }

        [MenuItem("Mineros/Registrar shaders en builds")]
        public static void Register()
        {
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            bool changed = false;
            foreach (string n in Names)
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                bool has = false;
                for (int i = 0; i < arr.arraySize; i++)
                {
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
                }
                if (has) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                changed = true;
            }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }
    }
}
