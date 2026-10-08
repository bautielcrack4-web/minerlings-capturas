using System;
using System.Globalization;

namespace Mineros.Core
{
    /// <summary>Formato de numeros grandes estilo idle (port de Num.fmt / Num.time_hms de num.gd).</summary>
    public static class BigNum
    {
        static readonly string[] Units = { "", "K", "M", "B", "T" };
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>999, 1.23K, 45.6M, 7.89B, 1.00T, 2.50aa, ...</summary>
        public static string Fmt(double v)
        {
            if (double.IsNaN(v)) return "nan";
            if (double.IsInfinity(v)) return v < 0.0 ? "-inf" : "inf";   // en Godot el resultado es indefinido
            if (v < 0.0) return "-" + Fmt(-v);
            if (v < 1000.0)
            {
                // GDScript roundf: mitades alejandose del cero
                if (v < 10.0 && Math.Abs(v - Math.Round(v, MidpointRounding.AwayFromZero)) > 0.05)
                    return v.ToString("F1", Inv);
                return ((long)Math.Floor(v)).ToString(Inv);
            }
            int e = (int)Math.Floor(Math.Log(v) / Math.Log(1000.0));
            double m = v / Math.Pow(1000.0, e);
            if (m >= 999.995)
            {
                m /= 1000.0;
                e += 1;
            }
            string u;
            if (e < Units.Length)
            {
                u = Units[e];
            }
            else
            {
                int k = e - Units.Length;
                u = ((char)(97 + (k / 26) % 26)).ToString() + (char)(97 + k % 26);
            }
            string s;
            if (m < 10.0) s = m.ToString("F2", Inv);
            else if (m < 100.0) s = m.ToString("F1", Inv);
            else s = ((long)Math.Floor(m)).ToString(Inv);
            return s + u;
        }

        /// <summary>"2h 05m", "3m 07s" o "42s".</summary>
        public static string TimeHms(double sec)
        {
            long s = (long)Math.Max(sec, 0.0);
            long h = s / 3600;
            long mi = (s % 3600) / 60;
            long se = s % 60;
            if (h > 0) return h.ToString(Inv) + "h " + mi.ToString("00", Inv) + "m";
            if (mi > 0) return mi.ToString(Inv) + "m " + se.ToString("00", Inv) + "s";
            return se.ToString(Inv) + "s";
        }
    }
}
