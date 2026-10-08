using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Mineros.Core
{
    /// <summary>
    /// Mini JSON (escritor + parser) sin dependencias, igual en Unity y en .NET.
    /// Modelo: Dictionary&lt;string,object&gt;, List&lt;object&gt;, string, long, double, bool, null.
    /// Los enteros sin punto ni exponente se leen como long; el resto como double.
    /// </summary>
    public static class Json
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ escritura
        public static string Stringify(object v)
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, v);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            string s = v as string;
            if (s != null) { WriteString(sb, s); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is double) { WriteDouble(sb, (double)v); return; }
            if (v is float) { WriteDouble(sb, (double)(float)v); return; }
            if (v is int || v is long || v is short || v is byte || v is uint || v is ulong)
            {
                sb.Append(Convert.ToString(v, Inv));
                return;
            }
            IDictionary dict = v as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, Convert.ToString(e.Key, Inv));
                    sb.Append(':');
                    Write(sb, e.Value);
                }
                sb.Append('}');
                return;
            }
            IEnumerable list = v as IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (object o in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, o);
                }
                sb.Append(']');
                return;
            }
            throw new ArgumentException("Tipo no serializable: " + v.GetType());
        }

        static void WriteDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d)) d = 0.0;
            else if (double.IsPositiveInfinity(d)) d = double.MaxValue;
            else if (double.IsNegativeInfinity(d)) d = -double.MaxValue;
            string t = d.ToString("R", Inv);
            sb.Append(t);
            // que un lector estricto (Godot) lo reconozca como float
            if (t.IndexOf('.') < 0 && t.IndexOf('E') < 0 && t.IndexOf('e') < 0) sb.Append(".0");
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", Inv));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------ lectura
        /// <summary>Parsea; devuelve false (y result = null) si el texto no es JSON valido.</summary>
        public static bool TryParse(string text, out object result)
        {
            result = null;
            if (text == null) return false;
            Reader r = new Reader(text);
            try
            {
                object v = r.ReadValue();
                r.SkipWs();
                if (!r.AtEnd) return false;
                result = v;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        sealed class Reader
        {
            readonly string s;
            int p;
            int depth;
            public Reader(string text) { s = text; }
            public bool AtEnd { get { return p >= s.Length; } }

            public void SkipWs()
            {
                while (p < s.Length && (s[p] == ' ' || s[p] == '\t' || s[p] == '\n' || s[p] == '\r')) p++;
            }

            static FormatException Err(string m) { return new FormatException(m); }

            public object ReadValue()
            {
                SkipWs();
                if (p >= s.Length) throw Err("fin inesperado");
                char c = s[p];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == 't') { Expect("true"); return true; }
                if (c == 'f') { Expect("false"); return false; }
                if (c == 'n') { Expect("null"); return null; }
                return ReadNumber();
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, p, word, 0, word.Length) != 0) throw Err("literal invalido");
                p += word.Length;
            }

            object ReadObject()
            {
                if (++depth > 64) throw Err("demasiado anidado");
                p++;
                Dictionary<string, object> d = new Dictionary<string, object>();
                SkipWs();
                if (p < s.Length && s[p] == '}') { p++; depth--; return d; }
                while (true)
                {
                    SkipWs();
                    if (p >= s.Length || s[p] != '"') throw Err("clave esperada");
                    string k = ReadString();
                    SkipWs();
                    if (p >= s.Length || s[p] != ':') throw Err("':' esperado");
                    p++;
                    d[k] = ReadValue();
                    SkipWs();
                    if (p >= s.Length) throw Err("fin inesperado");
                    if (s[p] == ',') { p++; continue; }
                    if (s[p] == '}') { p++; break; }
                    throw Err("',' o '}' esperado");
                }
                depth--;
                return d;
            }

            object ReadArray()
            {
                if (++depth > 64) throw Err("demasiado anidado");
                p++;
                List<object> l = new List<object>();
                SkipWs();
                if (p < s.Length && s[p] == ']') { p++; depth--; return l; }
                while (true)
                {
                    l.Add(ReadValue());
                    SkipWs();
                    if (p >= s.Length) throw Err("fin inesperado");
                    if (s[p] == ',') { p++; continue; }
                    if (s[p] == ']') { p++; break; }
                    throw Err("',' o ']' esperado");
                }
                depth--;
                return l;
            }

            string ReadString()
            {
                p++; // comilla inicial
                StringBuilder sb = new StringBuilder();
                while (true)
                {
                    if (p >= s.Length) throw Err("cadena sin cerrar");
                    char c = s[p++];
                    if (c == '"') break;
                    if (c != '\\') { sb.Append(c); continue; }
                    if (p >= s.Length) throw Err("escape incompleto");
                    char e = s[p++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (p + 4 > s.Length) throw Err("\\u incompleto");
                            int code;
                            if (!int.TryParse(s.Substring(p, 4), NumberStyles.AllowHexSpecifier, Inv, out code))
                                throw Err("\\u invalido");
                            sb.Append((char)code);
                            p += 4;
                            break;
                        default: throw Err("escape invalido");
                    }
                }
                return sb.ToString();
            }

            object ReadNumber()
            {
                int start = p;
                bool isFloat = false;
                if (p < s.Length && s[p] == '-') p++;
                int digits = 0;
                while (p < s.Length && s[p] >= '0' && s[p] <= '9') { p++; digits++; }
                if (digits == 0) throw Err("numero invalido");
                if (p < s.Length && s[p] == '.')
                {
                    isFloat = true;
                    p++;
                    int fd = 0;
                    while (p < s.Length && s[p] >= '0' && s[p] <= '9') { p++; fd++; }
                    if (fd == 0) throw Err("decimales esperados");
                }
                if (p < s.Length && (s[p] == 'e' || s[p] == 'E'))
                {
                    isFloat = true;
                    p++;
                    if (p < s.Length && (s[p] == '+' || s[p] == '-')) p++;
                    int ed = 0;
                    while (p < s.Length && s[p] >= '0' && s[p] <= '9') { p++; ed++; }
                    if (ed == 0) throw Err("exponente esperado");
                }
                string tok = s.Substring(start, p - start);
                if (!isFloat)
                {
                    long l;
                    if (long.TryParse(tok, NumberStyles.AllowLeadingSign, Inv, out l)) return l;
                }
                double d;
                if (!double.TryParse(tok, NumberStyles.Float, Inv, out d)) throw Err("numero invalido");
                return d;
            }
        }
    }

    /// <summary>Lectores tolerantes sobre el modelo JSON (valores ausentes o de otro tipo -> por defecto), como d.get(k, def) de Godot.</summary>
    public static class JsonRead
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }

        public static bool Has(Dictionary<string, object> d, string key) { return d != null && d.ContainsKey(key); }

        public static double ToDouble(object v, double def)
        {
            if (v is double) return (double)v;
            if (v is long) return (double)(long)v;
            if (v is int) return (int)v;
            if (v is bool) return (bool)v ? 1.0 : 0.0;
            string s = v as string;
            double r;
            if (s != null && double.TryParse(s, NumberStyles.Float, Inv, out r)) return r;
            return def;
        }

        /// <summary>int() de GDScript: trunca los decimales hacia cero.</summary>
        public static long ToLong(object v, long def)
        {
            if (v is long) return (long)v;
            if (v is int) return (int)v;
            if (v is double || v is bool || v is string)
            {
                double d = ToDouble(v, double.NaN);
                if (double.IsNaN(d)) return def;
                return NumUtil.ToLongClamped(d);
            }
            return def;
        }

        public static int ToInt(object v, int def)
        {
            long l = ToLong(v, def);
            if (l > int.MaxValue) return int.MaxValue;
            if (l < int.MinValue) return int.MinValue;
            return (int)l;
        }

        public static bool ToBool(object v, bool def)
        {
            if (v is bool) return (bool)v;
            if (v is long || v is double || v is int) return ToDouble(v, 0.0) != 0.0;
            return def;
        }

        public static string ToStr(object v, string def)
        {
            if (v == null) return def;
            string s = v as string;
            if (s != null) return s;
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is long || v is int) return Convert.ToString(v, Inv);
            if (v is double) return ((double)v).ToString("R", Inv);
            return def;
        }

        public static double Dbl(Dictionary<string, object> d, string k, double def) { return ToDouble(Get(d, k), def); }
        public static long Lng(Dictionary<string, object> d, string k, long def) { return ToLong(Get(d, k), def); }
        public static int Int(Dictionary<string, object> d, string k, int def) { return ToInt(Get(d, k), def); }
        public static bool Bool(Dictionary<string, object> d, string k, bool def) { return ToBool(Get(d, k), def); }
        public static string Str(Dictionary<string, object> d, string k, string def) { return ToStr(Get(d, k), def); }
        public static Dictionary<string, object> Dict(Dictionary<string, object> d, string k) { return Get(d, k) as Dictionary<string, object>; }
        public static List<object> List(Dictionary<string, object> d, string k) { return Get(d, k) as List<object>; }
    }

    /// <summary>Conversiones numericas comunes.</summary>
    public static class NumUtil
    {
        /// <summary>int() de GDScript con saturacion (en C# el cast de un double enorme es indefinido).</summary>
        public static long ToLongClamped(double d)
        {
            if (double.IsNaN(d)) return 0;
            if (d >= 9.2233720368547758e18) return long.MaxValue;
            if (d <= -9.2233720368547758e18) return long.MinValue;
            return (long)d;
        }
    }
}
