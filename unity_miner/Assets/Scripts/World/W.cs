using UnityEngine;

namespace Mineros.World
{
    /// <summary>
    /// Constantes y geometria del mundo. Unidades: metros (1 m = 72 px de la version Godot).
    /// El eje del cañon es +Z (s), la normal es +X (n). La meseta alta queda en -X, el desnivel en +X.
    /// Las posiciones logicas son Vector2 (x, z).
    /// </summary>
    public static class W
    {
        public const float PX = 1f / 72f;
        public const float HPlat = 150f * PX;
        public const float HDrop = 120f * PX;
        public const float ArenaR = 980f * PX;

        public const float CamYaw = -30f;
        public const float CamPitch = 47f;

        /// <summary>Derecha de la camara en el plano XZ (x, z).</summary>
        public static readonly Vector2 R2 = new Vector2(Mathf.Cos(CamYaw * Mathf.Deg2Rad), -Mathf.Sin(CamYaw * Mathf.Deg2Rad));
        /// <summary>Adelante (hacia arriba en pantalla) de la camara en el plano XZ.</summary>
        public static readonly Vector2 F2 = new Vector2(Mathf.Sin(CamYaw * Mathf.Deg2Rad), Mathf.Cos(CamYaw * Mathf.Deg2Rad));

        public static float Cen(float s)
        {
            float sp = s / PX;
            return PX * (70f * Mathf.Sin(sp * 0.0021f) + 30f * Mathf.Sin(sp * 0.0057f + 1.1f));
        }

        public static float Half(float s)
        {
            float sp = s / PX;
            return PX * (262f + 36f * Mathf.Sin(sp * 0.0031f + 0.7f));
        }

        public static float EdgeL(float s) { return Cen(s) - Half(s); }
        public static float EdgeR(float s) { return Cen(s) + Half(s); }

        /// <summary>t en [-1, 1] a lo ancho del pasillo.</summary>
        public static Vector2 CorridorPoint(float s, float t) { return new Vector2(Cen(s) + t * Half(s), s); }

        public static float S(Vector2 p) { return p.y; }
        public static float NOff(Vector2 p) { return p.x - Cen(p.y); }
        public static bool InCorridor(Vector2 p, float margin) { return Mathf.Abs(NOff(p)) < Half(p.y) - margin; }

        public static Vector3 V3(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }
        public static Vector3 V3(Vector2 p) { return new Vector3(p.x, 0f, p.y); }
        public static Vector2 V2(Vector3 p) { return new Vector2(p.x, p.z); }

        public static Quaternion CamRot { get { return Quaternion.Euler(CamPitch, CamYaw, 0f); } }
    }

    /// <summary>Ruido de valor determinista (sin estado) para manchas de tono, ondulacion y variaciones.</summary>
    public static class Nz
    {
        static uint H(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        public static float Hash01(int x, int y, int z)
        {
            return (H(x, y, z) & 0xFFFFFF) / 16777216f;
        }

        public static float Hash01(int x, int y) { return Hash01(x, y, 0); }

        public static float Value(float x, float y, int z)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash01(ix, iy, z), b = Hash01(ix + 1, iy, z), c = Hash01(ix, iy + 1, z), d = Hash01(ix + 1, iy + 1, z);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }
}
