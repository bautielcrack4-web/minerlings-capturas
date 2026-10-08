using Mineros.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>
    /// Texto de la interfaz que se muestra en el idioma del jugador: si lo que llega esta entero en la tabla (un
    /// nombre de edificio, recurso o minero armado antes) lo traduce. Si se le vuelve a dar el mismo texto no rehace
    /// la malla (el codigo suele comparar y asignar cada cuadro).
    /// </summary>
    public sealed class LocText : Text
    {
        string raw, shown;

        public override string text
        {
            get { return base.text; }
            set
            {
                if (value == raw && base.text == shown) return;
                raw = value;
                shown = Loc.Show(value);
                base.text = shown;
            }
        }
    }

    /// <summary>
    /// Carga la tabla de idiomas antes que nada (los nombres fijos del nucleo se arman al primer uso) y elige el idioma:
    /// el que eligio el jugador en Ajustes o, si no eligio, el del telefono (español si el telefono esta en español; si
    /// no, inglés). En capturas: LOC_LANG=en|es y se anotan los textos sin traducir en LOC_MISSING.
    /// </summary>
    public static class LocSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init() { Apply(); }

        public static void Apply()
        {
            var ta = Resources.Load<TextAsset>("Loc/en");
            Loc.Load(ta != null ? ta.text : null);
            string lang = null;
            if (Application.isBatchMode) lang = System.Environment.GetEnvironmentVariable("LOC_LANG");
            if (string.IsNullOrEmpty(lang))
            {
                string pref = "";
                try { pref = PlayerPrefs.GetString("lang", ""); } catch (System.Exception) { }
                lang = pref != "" ? pref : Application.systemLanguage == SystemLanguage.Spanish ? "es" : "en";
            }
            Loc.SetLang(lang);
            string miss = Application.isBatchMode ? System.Environment.GetEnvironmentVariable("LOC_MISSING") : null;
            if (!string.IsNullOrEmpty(miss))
            {
                Loc.Harvest = true;
                Loc.MissingFound += s => System.IO.File.AppendAllText(miss, s.Replace("\n", "\\n") + "\n");
            }
        }

        /// <summary>Cambia el idioma desde Ajustes (queda guardado).</summary>
        public static void Set(string lang)
        {
            PlayerPrefs.SetString("lang", lang);
            PlayerPrefs.Save();
            Loc.SetLang(lang);
        }
    }
}
