using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>
    /// Carta de minero viva (IslandUi.Cards): el dibujo respira (zoom lento), un barrido de luz la cruza cada tanto,
    /// motas de luz suben por delante (mas cuanto mas rara), el brillo de atras late, y la carta se inclina con inercia
    /// cuando se la arrastra (con paralaje del dibujo y la luz siguiendo la inclinacion). Sin la mano encima se mece apenas.
    /// Usa el tiempo sin escala: sigue viva aunque el juego este en camara lenta.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        public RectTransform Body;          // lo que se inclina (todo menos la sombra)
        public RawImage Art;
        public Image Glow;
        public Color GlowCol = new Color(1f, 0.9f, 0.5f);
        public int Rarity;
        public bool Golden;
        /// <summary>Velocidad del arrastre (unidades del lienzo por segundo): la escribe quien la mueve.</summary>
        public Vector2 Vel;
        public bool Held;
        /// <summary>Destello blanco sobre el dibujo (0..1), se apaga solo.</summary>
        public float Flash;

        Material mat;
        Vector2 tilt;
        float t0;
        readonly List<RectTransform> motes = new List<RectTransform>();
        readonly List<float> moteT = new List<float>();

        static readonly int idShine = Shader.PropertyToID("_Shine"), idTilt = Shader.PropertyToID("_Tilt"), idGlow = Shader.PropertyToID("_Glow"),
            idZoom = Shader.PropertyToID("_Zoom"), idFoil = Shader.PropertyToID("_Foil"), idAspect = Shader.PropertyToID("_Aspect");

        public void Init(RawImage art, RectTransform body, int rarity, bool golden, Color glowCol)
        {
            Art = art; Body = body; Rarity = rarity; Golden = golden; GlowCol = glowCol;
            t0 = Random.value * 10f;
            var sh = Shader.Find("Mineros/CardArt");
            if (sh != null && art != null)
            {
                mat = new Material(sh) { name = "Carta" };
                var r = art.rectTransform.rect;
                mat.SetFloat(idAspect, r.width > 1f ? r.height / r.width : 1.5f);
                mat.SetFloat(idFoil, golden ? 1f : rarity >= 3 ? 0.8f : rarity == 2 ? 0.45f : 0f);
                art.material = mat;
            }
            // motas de luz: 2 en las comunes, 8 en las legendarias
            int n = golden ? 9 : 2 + rarity * 2;
            for (int i = 0; i < n; i++)
            {
                var m = Kit.Img(body, Icons.Glow(), new Color(1f, 1f, 1f, 0f), "Mota").rectTransform;
                m.sizeDelta = Vector2.one * Random.Range(10f, 22f);
                motes.Add(m);
                moteT.Add(Random.value * 3f);
            }
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float t = Time.unscaledTime + t0;
            // inclinacion: con la mano, por la velocidad (frena con inercia); sin la mano, se mece
            Vector2 want = Held
                ? new Vector2(Mathf.Clamp(-Vel.y * 0.02f, -24f, 24f), Mathf.Clamp(Vel.x * 0.02f, -24f, 24f))
                : new Vector2(Mathf.Sin(t * 0.7f) * 3f, Mathf.Sin(t * 0.53f) * 5f);
            tilt = Vector2.Lerp(tilt, want, 1f - Mathf.Exp(-dt * (Held ? 10f : 3f)));
            if (Body != null) Body.localRotation = Quaternion.Euler(tilt.x, tilt.y, -tilt.y * 0.2f);
            if (mat != null)
            {
                float shine = Held ? 0.45f + tilt.y * 0.025f : Mathf.Repeat(t * 0.3f, 1.9f) - 0.4f;   // cruza cada ~6 s
                mat.SetFloat(idShine, shine);
                mat.SetVector(idTilt, new Vector4(tilt.y / 24f, tilt.x / 24f, -tilt.y * 0.0014f, tilt.x * 0.0014f));
                mat.SetFloat(idZoom, 0.92f - 0.012f * (0.5f + 0.5f * Mathf.Sin(t * 1.6f)));   // respira
                mat.SetFloat(idGlow, Flash);
            }
            Flash = Mathf.MoveTowards(Flash, 0f, dt * 1.8f);
            if (Glow != null)
            {
                float a = (Rarity >= 2 || Golden ? 0.55f : 0.25f) + 0.2f * Mathf.Sin(t * 2.2f);
                Glow.color = new Color(GlowCol.r, GlowCol.g, GlowCol.b, a);
            }
            // motas: suben por delante del dibujo y se apagan
            if (Body == null) return;
            var sz = Body.rect.size;
            for (int i = 0; i < motes.Count; i++)
            {
                moteT[i] += dt;
                float life = 2.6f;
                if (moteT[i] > life) moteT[i] -= life;
                float u = moteT[i] / life;
                float seed = i * 12.9898f;
                float x = (Mathf.Repeat(Mathf.Sin(seed) * 43758.5f, 1f) - 0.5f) * sz.x * 0.8f + Mathf.Sin(t * 1.3f + i) * 6f;
                float y = Mathf.Lerp(-sz.y * 0.35f, sz.y * 0.4f, u);
                motes[i].anchoredPosition = new Vector2(x, y);
                float al = Mathf.Sin(u * Mathf.PI) * (Golden ? 0.9f : 0.7f);
                Color c = Golden || Rarity >= 3 ? new Color(1f, 0.92f, 0.6f, al) : Color.Lerp(Color.white, GlowCol, 0.5f) * new Color(1, 1, 1, al);
                motes[i].GetComponent<Image>().color = c;
            }
        }
    }
}
