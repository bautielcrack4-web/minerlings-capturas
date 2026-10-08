using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.IslandView
{
    /// <summary>
    /// Interfaz de las sorpresas: el mapa del tesoro que se desenrolla (isla vista igual que en pantalla, linea punteada
    /// que se dibuja desde la botella hasta la X) y la etiqueta "¡Tocalo!" sobre los bichos.
    /// </summary>
    public sealed partial class IslandUi
    {
        // ------------------------------------------------------------ mapa del tesoro
        public void ShowTreasureMap(Bottle b)
        {
            var fr = OpenSheet(860, true);
            sheetLocked = true;
            Kit.LabelAt(fr, Loc.T("¡Un mapa del tesoro!"), 42, Kit.Brown, 0, true, 0, 22, 680, 56, TextAnchor.MiddleCenter);
            const float S = 560f;
            var holder = Kit.New("Mapa", fr);
            Kit.Place(holder, 0.5f, 0f, -S * 0.5f, 90f, S, S);
            var map = Kit.Img(holder, MapSprite(), Color.white, "Papel");
            Kit.Stretch(map.rectTransform);
            Sfx.Play("paper", -4f);
            // se desenrolla: crece de una tira fina a todo el papel
            holder.localScale = new Vector3(1f, 0.05f, 1f);
            Tw.To(holder, "desenrolla", 0.55f, Ease.OutBack, u => holder.localScale = new Vector3(1f, Mathf.Lerp(0.05f, 1f, u), 1f));
            // puntos de la ruta (orientados como la camara)
            Vector3 f = game.Cam.transform.forward; f.y = 0f; f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            float scale = (S * 0.4f) / (Isl.Radius + 1.5f);
            System.Func<float, float, Vector2> toMap = (x, z) => new Vector2(Vector3.Dot(new Vector3(x, 0, z), r), Vector3.Dot(new Vector3(x, 0, z), f)) * scale;
            Vector2 a = toMap(b.X, b.Z), c = toMap(b.TX, b.TZ);
            Vector2 mid = (a + c) * 0.5f + new Vector2(-(c - a).y, (c - a).x) * 0.25f;
            int n = 14;
            for (int i = 0; i <= n; i++)
            {
                float k = i / (float)n;
                Vector2 p = Vector2.Lerp(Vector2.Lerp(a, mid, k), Vector2.Lerp(mid, c, k), k);
                var dot = Kit.RoundImg(holder, 5, new Color(0.55f, 0.24f, 0.14f), "Punto");
                dot.rectTransform.sizeDelta = new Vector2(11, 11);
                dot.rectTransform.anchoredPosition = p;
                dot.transform.localScale = Vector3.zero;
                Tw.Scale(dot.transform, Vector3.zero, Vector3.one, 0.2f, Ease.OutBack, 0.6f + i * 0.08f);
            }
            var bottle = Kit.Label(holder, "o", 30, new Color(0.3f, 0.6f, 0.45f), 4, true, TextAnchor.MiddleCenter, "Botella");
            bottle.rectTransform.sizeDelta = new Vector2(40, 40);
            bottle.rectTransform.anchoredPosition = a;
            var x = Kit.Label(holder, "X", 64, new Color(0.8f, 0.15f, 0.12f), 6, true, TextAnchor.MiddleCenter, "X");
            x.rectTransform.sizeDelta = new Vector2(80, 80);
            x.rectTransform.anchoredPosition = c;
            x.transform.localScale = Vector3.zero;
            float xd = 0.6f + (n + 1) * 0.08f;
            Tw.Scale(x.transform, Vector3.zero, Vector3.one, 0.4f, Ease.OutBack, xd, () => Sfx.Play("jingle_small", -6f));
            Kit.LabelAt(fr, Loc.T("Un minero va a cavar en la X"), 28, Kit.Brown, 0, true, 0, 664, 680, 40, TextAnchor.MiddleCenter);
            var go = Kit.Button(fr, Loc.T("¡Vamos!"), Kit.Green, 36, 340, 100);
            Kit.Place((RectTransform)go.transform, 0.5f, 0f, -170f, 730f, 340, 100);
            primaryBtn = go;
            go.Clicked += () => { sheetLocked = false; CloseSheet(); };
        }

        static Sprite mapSpr;

        /// <summary>Papel del mapa: pergamino con borde gastado y la isla (arena y pasto) en el centro.</summary>
        static Sprite MapSprite()
        {
            if (mapSpr != null) return mapSpr;
            var p = new Painter(560, 560, 4f, 2);
            Color paper = Icons.H("f3e2b8"), paperD = Icons.H("d9bd85"), sea = Icons.H("b9d8d0"), sand = Icons.H("ead39a"), grass = Icons.H("a9c97a");
            var edge = new List<Vector2>();
            var rnd = new System.Random(5);
            for (int i = 0; i < 40; i++)
            {
                float t = i / 40f;
                float j = (float)rnd.NextDouble() * 3f;
                if (t < 0.25f) edge.Add(new Vector2(4 + t * 4 * 132, 4 + j));
                else if (t < 0.5f) edge.Add(new Vector2(136 - j, 4 + (t - 0.25f) * 4 * 132));
                else if (t < 0.75f) edge.Add(new Vector2(136 - (t - 0.5f) * 4 * 132, 136 - j));
                else edge.Add(new Vector2(4 + j, 136 - (t - 0.75f) * 4 * 132));
            }
            p.OPoly(edge.ToArray(), paper, 3f);
            p.Circle(new Vector2(70, 70), 60f, sea);
            p.Circle(new Vector2(70, 70), 53f, sand);
            p.Circle(new Vector2(70, 70), 47f, grass);
            p.Circle(new Vector2(70, 70), 6f, Icons.H("caa772"));
            // rosa de los vientos chiquita
            p.Star(new Vector2(122, 18), 9f, paperD);
            var t2 = p.ToTexture();
            mapSpr = Sprite.Create(t2, new Rect(0, 0, t2.width, t2.height), new Vector2(0.5f, 0.5f), 400f);
            return mapSpr;
        }

        // ------------------------------------------------------------ bichos
        RectTransform critterTag;
        Text critterTagL;

        public void MarkCritter(Critter c)
        {
            string[] hint = { "¡Un cangrejo con una gema! Tocalo", "¡Una gaviota ladrona! Tocala", "¡Un topo con un tesoro! Tocalo cuando asome", "¡Mariposa dorada! Tocala: turbo" };
            if (Isl.Stat("critters") < 6) Toast(hint[(int)c.Kind], Kit.Purple);
            if (critterTag != null) Tw.Pop(critterTag, 1.3f);
        }

        void UpdateCritterTag()
        {
            var c = Isl.CurCritter;
            bool show = c != null && game.CritterVisible && sheet == null;
            if (critterTag == null)
            {
                if (!show) return;
                critterTag = Kit.MakeTag(worldLayer, Loc.T("¡Tocalo!"), Kit.Purple, 18);
                critterTagL = critterTag.GetComponentInChildren<Text>();
            }
            if (critterTag.gameObject.activeSelf != show) critterTag.gameObject.SetActive(show);
            if (!show) return;
            Vector2 p = ToCanvas(game.CritterPos + Vector3.up * 1.1f);
            critterTag.anchoredPosition = new Vector2(p.x, -p.y + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 8f);
            float left = Mathf.Clamp01(c.Left / 3f);
            critterTag.localScale = Vector3.one * (1f + (1f - left) * Mathf.Abs(Mathf.Sin(Time.time * 10f)) * 0.12f);   // se apura al final
        }
    }
}
