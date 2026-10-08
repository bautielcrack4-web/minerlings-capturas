using System;
using System.Collections.Generic;
using Mineros.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>Mensajes cortos flotantes (toast.gd).</summary>
    public sealed class Toaster : MonoBehaviour
    {
        readonly List<RectTransform> active = new List<RectTransform>();

        public static Toaster Create(Transform parent)
        {
            RectTransform rt = Kit.New("Toaster", parent);
            Kit.Stretch(rt);
            return rt.gameObject.AddComponent<Toaster>();
        }

        public void Show(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] == null) { active.RemoveAt(i); continue; }
                active[i].anchoredPosition += new Vector2(0, 60f);
            }
            Vector2 cs = Kit.CanvasSize;
            Image bg = Kit.RoundImg(transform, 20, new Color(0.12f, 0.09f, 0.07f, 0.85f), "Toast");
            Text l = Kit.Label(bg.transform, text, 26, Color.white, 0, false, TextAnchor.UpperLeft);
            float tw = l.preferredWidth;
            float th = l.preferredHeight;
            float w = tw + 44f, h = th + 18f;
            RectTransform rt = bg.rectTransform;
            Kit.PlaceTL(rt, (cs.x - w) * 0.5f, cs.y * 0.36f, w, h);
            Kit.PlaceTL(l.rectTransform, 22, 8, tw + 2f, th);
            CanvasGroup g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            g.blocksRaycasts = false;
            g.interactable = false;
            active.Add(rt);
            Tw.Alpha(g, 1f, 0.15f, 0f, () =>
                Tw.Alpha(g, 0f, 0.3f, 1.4f, () =>
                {
                    active.Remove(rt);
                    if (rt != null) Destroy(rt.gameObject);
                }));
        }
    }

    /// <summary>Pantalla de carga "Minero en camino..." con patron de picos (transition.gd). Bloquea toques mientras dura.</summary>
    public sealed class Transition : MonoBehaviour
    {
        const float Dur = 1.3f;
        static Sprite pickSil;

        Action covered;
        string text;
        float t;
        bool fired;
        CanvasGroup group;
        readonly List<RectTransform> picks = new List<RectTransform>();
        int cols, rows;
        RectTransform miner;
        Text label;
        int lastDots = -1;
        Vector2 center;

        static Sprite PickSilhouette()
        {
            if (pickSil != null) return pickSil;
            Painter p = new Painter(128, 128, 1.6f, 3);
            p.Push();
            p.Transform(new Vector2(40f, 58f), 0f, 1f);
            p.Line(new Vector2(0, 8), new Vector2(0, -44), 7f, Color.white, false);
            p.Poly(new[]
            {
                new Vector2(-30, -30), new Vector2(-17, -44), new Vector2(0, -50), new Vector2(17, -44),
                new Vector2(30, -30), new Vector2(15, -39), new Vector2(0, -42), new Vector2(-15, -39),
            }, Color.white);
            p.Pop();
            Texture2D tex = p.ToTexture();
            pickSil = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return pickSil;
        }

        public static void Run(Transform parent, string text, Action covered, Color helmet, int biome = -1)
        {
            RectTransform rt = Kit.New("Transition", parent);
            Kit.Stretch(rt);
            Transition tr = rt.gameObject.AddComponent<Transition>();
            tr.text = text;
            tr.covered = covered;
            tr.Build(helmet, biome);
        }

        void Build(Color helmet, int biome)
        {
            group = Kit.Group(gameObject);
            group.alpha = 0f;
            // fondo arena, apenas teñido con el suelo del bioma (misma paleta que el mundo 3D)
            Color sand = Icons.H("e3cf9e");
            Color pick = Icons.H("d4bd88");
            if (biome >= 0)
            {
                Color ground = Icons.BiomeCol(biome, "ground", sand);
                sand = Color.Lerp(sand, ground, 0.22f);
                pick = Color.Lerp(pick, Icons.BiomeCol(biome, "ground", pick), 0.12f) * new Color(0.95f, 0.93f, 0.9f, 1f);
                pick.a = 1f;
            }
            Image bg = Kit.Tint(transform, sand, "Bg");
            bg.raycastTarget = true;
            Kit.Stretch(bg.rectTransform);
            Vector2 cs = Kit.CanvasSize;
            center = cs * 0.5f;
            cols = Mathf.CeilToInt(cs.x / 120f) + 3;
            rows = Mathf.CeilToInt(cs.y / 120f) + 3;
            Sprite sil = PickSilhouette();
            Color pc = pick;
            for (int i = 0; i < cols * rows; i++)
            {
                Image im = Kit.Img(transform, sil, pc, "Pick");
                RectTransform r = im.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.sizeDelta = new Vector2(80, 80);
                r.localScale = new Vector3(0.45f, 0.45f, 1f);
                r.localRotation = Quaternion.Euler(0, 0, -0.6f * Mathf.Rad2Deg);
                picks.Add(r);
            }
            Image m = Kit.Img(transform, Icons.MinerBust(helmet), Color.white, "Miner");
            m.preserveAspect = true;
            miner = m.rectTransform;
            miner.anchorMin = miner.anchorMax = new Vector2(0, 1);
            miner.pivot = new Vector2(55f / Icons.MinerCanvasW, 1f - 108f / Icons.MinerCanvasH);
            miner.sizeDelta = new Vector2(Icons.MinerCanvasW * 1.3f, Icons.MinerCanvasH * 1.3f);
            label = Kit.Label(transform, text, 26, Kit.Brown, 0, false, TextAnchor.MiddleCenter, "Text");
            Kit.PlaceTL(label.rectTransform, center.x - 300f, center.y - 100f, 600, 40);
            Tick(0f);
        }

        void Update() { Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f)); }

        void Tick(float dt)
        {
            t += dt;
            group.alpha = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((Dur - t) * 6f);
            if (t > Dur * 0.4f && !fired)
            {
                fired = true;
                if (covered != null) covered();
            }
            if (t > Dur)
            {
                Destroy(gameObject);
                return;
            }
            float off = Mathf.Repeat(t * 30f, 120f);
            int k = 0;
            for (int y = -1; y < rows - 1; y++)
            {
                for (int x = -1; x < cols - 1; x++)
                {
                    if (k >= picks.Count) break;
                    float px = x * 120f + ((y & 1) != 0 ? 60f : 0f);
                    float py = y * 120f + off;
                    picks[k].anchoredPosition = new Vector2(px, -py);
                    k++;
                }
            }
            float bob = Mathf.Abs(Mathf.Sin(t * 12f)) * 7f;
            miner.anchoredPosition = new Vector2(center.x, -(center.y + 70f - bob));
            miner.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 12f) * 3f);
            int dots = (int)(t * 3f) % 4;
            if (dots != lastDots)
            {
                lastDots = dots;
                label.text = text + new string('.', dots);
            }
        }
    }
}
