using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>
    /// Contorno redondo para Text: copias del texto en un circulo (16 direcciones, dos anillos si es grueso) en vez de las
    /// 4 diagonales del Outline de Unity, que dejan esquinas cuadradas y bordes "recortados".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoftOutline : BaseMeshEffect
    {
        public Color Color = Color.black;
        public float Radius = 2f;

        static readonly List<UIVertex> src = new List<UIVertex>(256);
        static readonly List<UIVertex> dst = new List<UIVertex>(4096);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Radius <= 0.01f) return;
            src.Clear();
            vh.GetUIVertexStream(src);
            dst.Clear();
            int rings = Radius > 3f ? 2 : 1;
            for (int ring = rings; ring >= 1; ring--)
            {
                float r = Radius * ring / rings;
                int n = ring == rings ? 16 : 10;
                for (int i = 0; i < n; i++)
                {
                    float a = (i + 0.5f * ring) * Mathf.PI * 2f / n;
                    Vector3 off = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r - Radius * 0.12f, 0f);
                    for (int j = 0; j < src.Count; j++)
                    {
                        UIVertex v = src[j];
                        v.position += off;
                        Color32 c = Color;
                        c.a = (byte)(c.a * v.color.a / 255);
                        v.color = c;
                        dst.Add(v);
                    }
                }
            }
            dst.AddRange(src);
            vh.Clear();
            vh.AddUIVertexTriangleStream(dst);
        }
    }
}
