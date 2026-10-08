using System;
using System.Globalization;

namespace Mineros.Art.Gen
{
    // Matematica minima SIN UnityEngine: el generador de mallas del kit de arte corre tambien fuera de Unity
    // (unity_tools/artkit_preview) para poder revisar las piezas sin licencia. Convencion de Unity: mano izquierda,
    // caras en sentido horario vistas desde afuera, es decir normal = cross(b - a, c - a) apunta hacia afuera.

    public struct V2
    {
        public float x, y;
        public V2(float x, float y) { this.x = x; this.y = y; }
        public static V2 operator +(V2 a, V2 b) { return new V2(a.x + b.x, a.y + b.y); }
        public static V2 operator -(V2 a, V2 b) { return new V2(a.x - b.x, a.y - b.y); }
        public static V2 operator *(V2 a, float s) { return new V2(a.x * s, a.y * s); }
    }

    public struct V3
    {
        public float x, y, z;
        public V3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static readonly V3 Zero = new V3(0, 0, 0);
        public static readonly V3 Up = new V3(0, 1, 0);
        public static V3 operator +(V3 a, V3 b) { return new V3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static V3 operator -(V3 a) { return new V3(-a.x, -a.y, -a.z); }
        public static V3 operator *(V3 a, float s) { return new V3(a.x * s, a.y * s, a.z * s); }
        public static V3 operator *(float s, V3 a) { return new V3(a.x * s, a.y * s, a.z * s); }
        public static float Dot(V3 a, V3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static V3 Cross(V3 a, V3 b) { return new V3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public float Length { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public V3 Normalized
        {
            get
            {
                float l = Length;
                return l > 1e-12f ? new V3(x / l, y / l, z / l) : new V3(0, 1, 0);
            }
        }
        public static V3 Lerp(V3 a, V3 b, float t) { return a + (b - a) * t; }
        public override string ToString() { return x.ToString("0.###", CultureInfo.InvariantCulture) + "," + y.ToString("0.###", CultureInfo.InvariantCulture) + "," + z.ToString("0.###", CultureInfo.InvariantCulture); }
    }

    /// <summary>Transformacion afin (3x3 + traslacion). Encadenable de afuera hacia adentro:
    /// Xf.T(p).Ry(30).S(2) = primero escala, luego rota, luego traslada.</summary>
    public struct Xf
    {
        public float m00, m01, m02, m10, m11, m12, m20, m21, m22;
        public V3 t;

        /// <summary>Identidad: punto de partida de las cadenas (Xf.I.T(0,1,0).Ry(30).S(2)).</summary>
        public static Xf I { get { return Identity; } }

        public static Xf Identity
        {
            get { var x = new Xf(); x.m00 = 1; x.m11 = 1; x.m22 = 1; return x; }
        }

        public static Xf Tr(float x, float y, float z) { var r = Identity; r.t = new V3(x, y, z); return r; }
        public static Xf Tr(V3 p) { var r = Identity; r.t = p; return r; }
        public static Xf Sc(float x, float y, float z) { var r = Identity; r.m00 = x; r.m11 = y; r.m22 = z; return r; }
        public static Xf Sc(float s) { return Sc(s, s, s); }

        public static Xf RotX(float deg)
        {
            float a = deg * (float)Math.PI / 180f, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            var r = Identity; r.m11 = c; r.m12 = -s; r.m21 = s; r.m22 = c; return r;
        }
        public static Xf RotY(float deg)
        {
            float a = deg * (float)Math.PI / 180f, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            var r = Identity; r.m00 = c; r.m02 = s; r.m20 = -s; r.m22 = c; return r;
        }
        public static Xf RotZ(float deg)
        {
            float a = deg * (float)Math.PI / 180f, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            var r = Identity; r.m00 = c; r.m01 = -s; r.m10 = s; r.m11 = c; return r;
        }

        // Encadenado (post-multiplicacion: this * o, se aplica primero o).
        public Xf Then(Xf o) { return Mul(this, o); }
        public Xf T(float x, float y, float z) { return Mul(this, Tr(x, y, z)); }
        public Xf T(V3 p) { return Mul(this, Tr(p)); }
        public Xf S(float x, float y, float z) { return Mul(this, Sc(x, y, z)); }
        public Xf S(float s) { return Mul(this, Sc(s, s, s)); }
        public Xf Rx(float d) { return Mul(this, RotX(d)); }
        public Xf Ry(float d) { return Mul(this, RotY(d)); }
        public Xf Rz(float d) { return Mul(this, RotZ(d)); }

        public static Xf Mul(Xf a, Xf b)
        {
            var r = new Xf();
            r.m00 = a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20;
            r.m01 = a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21;
            r.m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22;
            r.m10 = a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20;
            r.m11 = a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21;
            r.m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22;
            r.m20 = a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20;
            r.m21 = a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21;
            r.m22 = a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22;
            r.t = a.Dir(b.t) + a.t;
            return r;
        }

        public V3 Dir(V3 v)
        {
            return new V3(m00 * v.x + m01 * v.y + m02 * v.z, m10 * v.x + m11 * v.y + m12 * v.z, m20 * v.x + m21 * v.y + m22 * v.z);
        }
        public V3 Point(V3 v) { return Dir(v) + t; }

        public float Det
        {
            get { return m00 * (m11 * m22 - m12 * m21) - m01 * (m10 * m22 - m12 * m20) + m02 * (m10 * m21 - m11 * m20); }
        }

        /// <summary>Normal transformada (matriz de cofactores = inversa traspuesta; respeta espejos).</summary>
        public V3 Normal(V3 n)
        {
            float c00 = m11 * m22 - m12 * m21, c01 = m12 * m20 - m10 * m22, c02 = m10 * m21 - m11 * m20;
            float c10 = m02 * m21 - m01 * m22, c11 = m00 * m22 - m02 * m20, c12 = m01 * m20 - m00 * m21;
            float c20 = m01 * m12 - m02 * m11, c21 = m02 * m10 - m00 * m12, c22 = m00 * m11 - m01 * m10;
            var r = new V3(c00 * n.x + c01 * n.y + c02 * n.z, c10 * n.x + c11 * n.y + c12 * n.z, c20 * n.x + c21 * n.y + c22 * n.z);
            if (Det < 0) r = -r;
            return r.Normalized;
        }
    }

    /// <summary>Color sRGB (lo que se escribe en hex). Unity lo recibe como Color y lo convierte a lineal al pasarlo al
    /// material (Material.SetColor en espacio Linear), asi que NO se convierte a mano.</summary>
    public struct Rgb
    {
        public float r, g, b;
        public Rgb(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }

        public static Rgb Hex(string h)
        {
            if (h.StartsWith("#")) h = h.Substring(1);
            int v = int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Rgb(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
        }

        // Misma semantica que Color.darkened / lightened de Godot (usada por la fuente art.gd).
        public Rgb Darken(float a) { return new Rgb(r * (1 - a), g * (1 - a), b * (1 - a)); }
        public Rgb Lighten(float a) { return new Rgb(r + (1 - r) * a, g + (1 - g) * a, b + (1 - b) * a); }
        public Rgb Mul(float k) { return new Rgb(r * k, g * k, b * k); }
        public static Rgb Lerp(Rgb a, Rgb b, float t) { return new Rgb(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t); }
        public static readonly Rgb Black = new Rgb(0, 0, 0);
        public static readonly Rgb White = new Rgb(1, 1, 1);
    }

    /// <summary>Generador pseudoaleatorio determinista (xorshift32) para que cada variante sea reproducible.</summary>
    public sealed class Rng
    {
        uint s;
        public Rng(uint seed) { s = seed == 0 ? 0x9E3779B9u : seed; Next(); Next(); }
        public float Next()
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / (float)0x1000000;
        }
        public float Range(float a, float b) { return a + (b - a) * Next(); }
        public int Int(int n) { return (int)(Next() * n) % n; }
        public bool Chance(float p) { return Next() < p; }
        public float Sign() { return Next() < 0.5f ? -1f : 1f; }

        public static uint Hash(string str, int a, int b)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < str.Length; i++) { h ^= str[i]; h *= 16777619u; }
                h ^= (uint)a * 0x9E3779B1u; h *= 16777619u;
                h ^= (uint)b * 0x85EBCA6Bu; h *= 16777619u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;
                return h;
            }
        }
        public static uint Hash(int a) { return Hash("s", a, 7); }
    }

    /// <summary>Ruido de valor 3D suave, determinista (para blobs organicos y rocas).</summary>
    public static class Noise
    {
        static float H(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177 + seed * 362437);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 32767.5f - 1f; // -1..1
            }
        }
        static float Sm(float t) { return t * t * (3 - 2 * t); }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        public static float Value(float x, float y, float z, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y), zi = (int)Math.Floor(z);
            float fx = Sm(x - xi), fy = Sm(y - yi), fz = Sm(z - zi);
            float a = Lerp(H(xi, yi, zi, seed), H(xi + 1, yi, zi, seed), fx);
            float b = Lerp(H(xi, yi + 1, zi, seed), H(xi + 1, yi + 1, zi, seed), fx);
            float c = Lerp(H(xi, yi, zi + 1, seed), H(xi + 1, yi, zi + 1, seed), fx);
            float d = Lerp(H(xi, yi + 1, zi + 1, seed), H(xi + 1, yi + 1, zi + 1, seed), fx);
            return Lerp(Lerp(a, b, fy), Lerp(c, d, fy), fz);
        }
    }
}
