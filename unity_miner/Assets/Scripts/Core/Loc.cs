using System;
using System.Collections.Generic;
using System.Globalization;

namespace Mineros.Core
{
    /// <summary>
    /// Idiomas (1.0): el juego se escribe en español y una tabla da el inglés (idioma principal de la tienda). Sin
    /// tabla cargada todo queda en español (asi corren las pruebas del nucleo). `T` traduce un texto o un pedazo de
    /// texto exacto; `Show` lo usa la interfaz para traducir lo que llega armado de otro lado (nombres de edificios,
    /// recursos, mineros), en las dos direcciones, para que cambiar de idioma funcione aunque algo ya estuviera armado.
    /// </summary>
    public static class Loc
    {
        static readonly Dictionary<string, string> esToEn = new Dictionary<string, string>();
        static readonly Dictionary<string, string> enToEs = new Dictionary<string, string>();
        public static readonly HashSet<string> Missing = new HashSet<string>();
        public static bool Harvest;   // capturas: anota lo que falta traducir
        public static event Action<string> MissingFound;

        /// <summary>"en" o "es".</summary>
        public static string Lang { get; private set; } = "es";
        public static bool En { get { return Lang == "en"; } }

        public static CultureInfo Culture { get { return En ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("es-AR"); } }

        /// <summary>Carga la tabla: una linea por texto, "español TAB inglés" (\n escrito como \\n). # = comentario.</summary>
        public static void Load(string tsv)
        {
            esToEn.Clear(); enToEs.Clear();
            if (string.IsNullOrEmpty(tsv)) return;
            foreach (var raw in tsv.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                string es = Unescape(line.Substring(0, tab)), en = Unescape(line.Substring(tab + 1));
                if (en.Length == 0) continue;
                if (en == "<>") en = "";   // pedazo que en inglés no va (por ejemplo "¡")
                esToEn[es] = en;
                if (!enToEs.ContainsKey(en)) enToEs[en] = es;
            }
        }

        static string Unescape(string s) { return s.Replace("\\n", "\n").Replace("\\t", "\t"); }

        public static void SetLang(string lang) { Lang = lang == "en" ? "en" : "es"; }

        public static int Count { get { return esToEn.Count; } }

        /// <summary>Traduce un texto (o pedazo) escrito en español.</summary>
        public static string T(string es)
        {
            if (!En || string.IsNullOrEmpty(es)) return es;
            string en;
            if (esToEn.TryGetValue(es, out en)) return en;
            if (Harvest && Wants(es) && Missing.Add(es)) MissingFound?.Invoke(es);
            return es;
        }

        /// <summary>Para la interfaz: traduce el texto entero si esta en la tabla (en cualquier direccion); si no, lo deja.</summary>
        public static string Show(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string o;
            if (En) { if (esToEn.TryGetValue(s, out o)) return o; }
            else if (enToEs.TryGetValue(s, out o)) return o;
            if (Harvest && En && LooksSpanish(s) && Missing.Add("[pantalla] " + s)) MissingFound?.Invoke("[pantalla] " + s);
            return s;
        }

        /// <summary>Lo que vale la pena anotar como faltante: tiene letras (no solo numeros y simbolos).</summary>
        static bool Wants(string s)
        {
            int letters = 0;
            foreach (char c in s) if (char.IsLetter(c)) letters++;
            return letters >= 2;
        }

        static readonly string[] esWords = { " de ", " la ", " el ", " los ", " las ", " y ", " con ", " para ", " por ", " más", " una ", " un " };

        /// <summary>Para encontrar textos que llegaron a la pantalla sin traducir (solo en capturas).</summary>
        static bool LooksSpanish(string s)
        {
            foreach (char c in s) if ("áéíóúñ¿¡ÁÉÍÓÚÑ".IndexOf(c) >= 0) return true;
            string l = " " + s.ToLowerInvariant() + " ";
            foreach (var w in esWords) if (l.Contains(w)) return true;
            return false;
        }

        /// <summary>Numero con separador de miles del idioma (1,580 o 1.580).</summary>
        public static string N(long v) { return v.ToString("N0", Culture); }

        /// <summary>Elige texto segun el idioma, para frases que no se pueden armar por pedazos (otro orden de palabras).</summary>
        public static string Pick(string es, string en) { return En ? en : es; }
    }
}
