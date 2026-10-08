using System.Collections.Generic;
using Mineros.Core;
using UnityEngine;

namespace Mineros.World
{
    /// <summary>
    /// Numeros flotantes (dano, oro, gemas, textos) con TextMesh y la fuente Lilita One, contorno simulado con 4 copias,
    /// siempre mirando a la camara. Pool fijo: no crea ni destruye objetos durante el juego.
    /// </summary>
    public sealed class FloatTexts
    {
        sealed class T
        {
            public GameObject go;
            public TextMesh main;
            public MeshRenderer mainR;
            public readonly TextMesh[] ol = new TextMesh[4];
            public bool active;
            public Vector3 pos, vel;
            public float t, life, sizePx, rotDeg, scale, olOff;
            public Color col;
            public object owner;
            public double acc;
            public bool isDmg;
            public float alphaShown = 1f;
        }

        readonly List<T> pool = new List<T>();
        readonly Transform root;
        Font font;
        Material mat;
        float digitH = 0f;
        public int MaxActive = 40;

        static readonly Color OutCol = Biomes.Out;

        public FloatTexts(Transform parent, int poolSize)
        {
            root = new GameObject("Texts").transform;
            root.SetParent(parent, false);
            font = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) mat = WorldMaterials.TextMaterial(font);
            for (int i = 0; i < poolSize; i++) pool.Add(Make(i));
        }

        public Font TextFont { get { return font; } }
        public Material TextMat { get { return mat; } }

        T Make(int slot)
        {
            var t = new T();
            t.go = new GameObject("T" + slot);
            t.go.transform.SetParent(root, false);
            for (int i = 0; i < 4; i++)
            {
                var g = new GameObject("o" + i);
                g.transform.SetParent(t.go.transform, false);
                t.ol[i] = Setup(g, slot * 2);
            }
            var m = new GameObject("m");
            m.transform.SetParent(t.go.transform, false);
            t.main = Setup(m, slot * 2 + 1);
            t.mainR = m.GetComponent<MeshRenderer>();
            t.go.SetActive(false);
            return t;
        }

        TextMesh Setup(GameObject g, int order)
        {
            var tm = g.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.richText = false;
            tm.text = "";
            var r = g.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = order;
            return tm;
        }

        void Calibrate(T t)
        {
            // altura real del digito "0" a fontSize 64 / characterSize 0.1 (escala 1), para dimensionar en metros
            t.main.text = "0";
            float h = 0f;
            if (t.mainR != null) h = t.mainR.bounds.size.y;
            digitH = (h > 0.01f && h < 50f) ? h : 0.45f;
        }

        T Free()
        {
            int act = 0;
            T free = null;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].active) act++;
                else if (free == null) free = pool[i];
            }
            if (act >= MaxActive) return null;
            return free;
        }

        void SetAll(T t, string s)
        {
            t.main.text = s;
            for (int i = 0; i < 4; i++) t.ol[i].text = s;
        }

        void SetColor(T t, Color c, float a)
        {
            Color m = c; m.a = a;
            t.main.color = m;
            Color o = OutCol; o.a = a;
            for (int i = 0; i < 4; i++) t.ol[i].color = o;
            t.alphaShown = a;
        }

        /// <summary>Muestra un texto. sizePx = tamano en "pixeles de Godot" (30 dano, 40 critico, 34 oro...).</summary>
        public int Spawn(string text, Vector3 pos, Vector3 vel, float life, float sizePx, Color col, float rotDeg, object owner, double acc, bool isDmg)
        {
            var t = Free();
            if (t == null || font == null) return -1;
            t.active = true;
            t.go.SetActive(true);
            t.go.transform.localScale = Vector3.one;
            if (digitH <= 0f) Calibrate(t);
            t.pos = pos; t.vel = vel; t.t = 0f; t.life = life; t.sizePx = sizePx; t.rotDeg = rotDeg;
            t.col = col; t.owner = owner; t.acc = acc; t.isDmg = isDmg;
            // 1.45: el bounds del TextMesh incluye el interlineado; medido contra las capturas de Godot (digito de "52": 22 px)
            t.scale = sizePx * W.PX * 0.72f * 1.45f / digitH;
            t.olOff = Mathf.Max(0.04f, sizePx * W.PX * 0.14f);
            SetAll(t, text);
            SetColor(t, col, 1f);
            // contorno: 4 diagonales en el plano del texto, un poco detras
            float o = t.olOff / t.scale;
            t.ol[0].transform.localPosition = new Vector3(-o, -o, 0.01f);
            t.ol[1].transform.localPosition = new Vector3(o, -o, 0.01f);
            t.ol[2].transform.localPosition = new Vector3(-o, o, 0.01f);
            t.ol[3].transform.localPosition = new Vector3(o, o, 0.01f);
            t.main.transform.localPosition = Vector3.zero;
            t.go.transform.localScale = Vector3.one * (t.scale * 1.7f);
            return pool.IndexOf(t);
        }

        /// <summary>Mueve un texto fijo (vida larga) creado con Spawn.</summary>
        public void SetPos(int idx, Vector3 p)
        {
            if (idx >= 0 && idx < pool.Count && pool[idx].active) pool[idx].pos = p;
        }

        public void Kill(int idx)
        {
            if (idx < 0 || idx >= pool.Count) return;
            pool[idx].active = false;
            pool[idx].go.SetActive(false);
        }

        /// <summary>Si hay un numero de dano reciente sobre la misma roca, le suma el dano en vez de crear otro.</summary>
        public bool TryMerge(object owner, double dmg)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var t = pool[i];
                if (t.active && t.isDmg && t.owner == owner && t.t < 0.35f)
                {
                    t.acc += dmg;
                    SetAll(t, BigNum.Fmt(t.acc));
                    t.t = 0.05f;
                    t.vel = new Vector3(t.vel.x * 0.3f, 110f * W.PX, t.vel.z * 0.3f);
                    return true;
                }
            }
            return false;
        }

        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < pool.Count; i++) if (pool[i].active) n++;
                return n;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].active = false;
                pool[i].go.SetActive(false);
            }
        }

        public void Update(float dt, Camera cam)
        {
            Quaternion cr = cam.transform.rotation;
            // el texto usa la prueba de profundidad normal: se adelanta hacia la camara (en ortografica no cambia donde se ve)
            Vector3 toCam = -cam.transform.forward * 9f;
            float damp = Mathf.Pow(0.9f, dt * 60f);
            for (int i = 0; i < pool.Count; i++)
            {
                var t = pool[i];
                if (!t.active) continue;
                t.t += dt;
                if (t.t >= t.life)
                {
                    t.active = false;
                    t.go.SetActive(false);
                    continue;
                }
                t.pos += t.vel * dt;
                t.vel *= damp;
                float f = t.t / t.life;
                float sc = 1f;
                if (t.t < 0.12f) sc = Mathf.Lerp(1.7f, 1f, 1f - Mathf.Pow(1f - t.t / 0.12f, 2f));
                float a = Mathf.Clamp01((1f - f) * 3f);
                if (Mathf.Abs(a - t.alphaShown) > 0.04f) SetColor(t, t.col, a);
                var tr = t.go.transform;
                tr.position = t.pos + toCam;
                tr.rotation = cr * Quaternion.Euler(0f, 0f, t.rotDeg);
                tr.localScale = Vector3.one * (t.scale * sc);
            }
        }
    }
}
