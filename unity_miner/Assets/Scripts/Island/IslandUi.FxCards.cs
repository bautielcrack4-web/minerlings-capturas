using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Cartas de efecto en la interfaz: boton con la mano (cuantas hay), la mano (tocar una = jugarla: vuela a la isla),
    /// el album de las 240 (las que faltan, dadas vuelta con "?"), los sobres de la tienda y su apertura (de a una, la
    /// mejor al final, con suspenso en las epicas y legendarias y la melodia propia de cada carta al darse vuelta).
    /// </summary>
    public sealed partial class IslandUi
    {
        Btn cardsBtn;
        Text cardsN;

        void UpdateCardsButton()
        {
            bool show = Isl.TutDone && sheet == null && (Isl.FxHandCount > 0 || Isl.FxSeenCount > 0);
            if (cardsBtn == null)
            {
                cardsBtn = Kit.HitArea(hudLayer, 76, 76, "Cartas");
                Kit.Place((RectTransform)cardsBtn.transform, 0f, 1f, 26f, -444f, 76, 76);
                var box = Kit.RoundImg(cardsBtn.transform, 24, Icons.H("5b4bd6"), "Caja");
                Kit.Stretch(box.rectTransform);
                // abanico de 3 cartitas
                for (int i = 0; i < 3; i++)
                {
                    var c = Kit.Img(box.transform, FxCardArt.Back(), Color.white, "Carta");
                    c.preserveAspect = true;
                    var rt = c.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(34, 48);
                    rt.anchoredPosition = new Vector2((i - 1) * 12f, -2f + (i == 1 ? 4f : 0f));
                    rt.localRotation = Quaternion.Euler(0, 0, (1 - i) * 16f);
                }
                var badge = Kit.RoundImg(box.transform, 14, Kit.Red, "Numero");
                Kit.PlaceTL(badge.rectTransform, 48, -8, 36, 30);
                cardsN = Kit.Label(badge.transform, "", 20, Color.white, 4, true, TextAnchor.MiddleCenter);
                Kit.Stretch(cardsN.rectTransform);
                cardsBtn.Clicked += () => OpenHand(false);
                cardsBtn.gameObject.SetActive(false);
            }
            if (cardsBtn.gameObject.activeSelf != show) { cardsBtn.gameObject.SetActive(show); if (show) Tw.Pop(cardsBtn.transform, 1.3f); }
            if (show)
            {
                int n = Isl.FxHandCount;
                cardsN.transform.parent.gameObject.SetActive(n > 0);
                cardsN.text = n.ToString();
            }
        }

        /// <summary>Carta chica para listas: dibujo + nombre (+ cuantas, si hay mas de una).</summary>
        RectTransform FxCardView(Transform parent, int id, float w, int count, bool known = true)
        {
            float h = w * 170f / 120f;
            var img = Kit.Img(parent, known ? FxCardArt.Get(id) : FxCardArt.Back(), known ? Color.white : new Color(0.55f, 0.55f, 0.6f), "Carta");
            img.preserveAspect = true;
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(w, h);
            if (known)
            {
                var nl = Kit.Label(img.transform, Island.FxCatalog[id].Name, Mathf.RoundToInt(w * 0.11f), Color.white, 3, true, TextAnchor.MiddleCenter, "Nombre");
                var nrt = nl.rectTransform;
                nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(1f, 0f);
                nrt.offsetMin = new Vector2(w * 0.09f, h * 0.05f); nrt.offsetMax = new Vector2(-w * 0.09f, h * 0.05f + h * 0.12f);
                nl.resizeTextForBestFit = true; nl.resizeTextMinSize = 7; nl.resizeTextMaxSize = Mathf.RoundToInt(w * 0.11f);
                if (count > 1)
                {
                    var b = Kit.RoundImg(img.transform, 12, Kit.Red, "Cuantas");
                    var brt = b.rectTransform;
                    brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
                    brt.sizeDelta = new Vector2(40, 28);
                    brt.anchoredPosition = new Vector2(-14f, -12f);
                    var bl = Kit.Label(b.transform, "x" + count, 18, Color.white, 3, true, TextAnchor.MiddleCenter);
                    Kit.Stretch(bl.rectTransform);
                }
            }
            else
            {
                var q = Kit.Label(img.transform, "?", Mathf.RoundToInt(w * 0.4f), Color.white, 4, true, TextAnchor.MiddleCenter);
                Kit.Stretch(q.rectTransform);
            }
            return rt;
        }

        // ------------------------------------------------------------ mano y album
        public void OpenHand(bool album)
        {
            var fr = OpenSheet(760, false, 680f, 0.12f, false);
            // pestañas con icono: mano | album (con el contador)
            var tHand = Kit.Button(fr, "", album ? Kit.Gray : Icons.H("5b4bd6"), 22, 150, 56, "Mano");
            Kit.PlaceTL((RectTransform)tHand.transform, 24, 16, 150, 56);
            var hi = Kit.Img(tHand.Content, FxCardArt.Back(), Color.white, "Icono"); hi.preserveAspect = true;
            Kit.PlaceTL(hi.rectTransform, 12, 6, 30, 42);
            var hl = Kit.Label(tHand.Content, Isl.FxHandCount.ToString(), 26, Color.white, 4, true, TextAnchor.MiddleCenter); Kit.Stretch(hl.rectTransform, 40, 0, 0, 4);
            tHand.Clicked += () => { if (album) OpenHand(false); };
            var tAlb = Kit.Button(fr, "", album ? Icons.H("5b4bd6") : Kit.Gray, 22, 190, 56, "Album");
            Kit.PlaceTL((RectTransform)tAlb.transform, 184, 16, 190, 56);
            var ai = Kit.Icon(tAlb.Content, "trophy", 36); Kit.PlaceTL((RectTransform)ai.transform, 10, 9, 36, 36);
            var al = Kit.Label(tAlb.Content, Isl.FxSeenCount + "/" + Island.FxCount, 24, Color.white, 4, true, TextAnchor.MiddleCenter); Kit.Stretch(al.rectTransform, 44, 0, 0, 4);
            tAlb.Clicked += () => { if (!album) OpenHand(true); };
            var sc = Kit.Scroll(fr, 12f, "Lista");
            Kit.PlaceTL((RectTransform)sc.Scroll.transform, 20, 86, 640, 760 - 106);
            var body = sc.Content;
            if (!album)
            {
                var ids = new List<int>();
                for (int i = 0; i < Island.FxCount; i++) if (Isl.FxOwned[i] > 0) ids.Add(i);
                ids.Sort((a, b) => Island.FxCatalog[b].Rarity.CompareTo(Island.FxCatalog[a].Rarity));
                if (ids.Count == 0)
                {
                    // sin cartas: a la tienda (icono de sobre + flecha)
                    var s = Section(body, 200, "Vacia");
                    var go = Kit.Button(s, "", Kit.Purple, 24, 260, 120, "Sobres");
                    Kit.Place((RectTransform)go.transform, 0.5f, 0f, -130f, 40f, 260, 120);
                    var bi = Kit.Img(go.Content, FxCardArt.Back(), Color.white, "Sobre"); bi.preserveAspect = true;
                    Kit.PlaceTL(bi.rectTransform, 24, 14, 64, 90);
                    var sh = Kit.Icon(go.Content, "shop", 56); Kit.PlaceTL((RectTransform)sh.transform, 150, 30, 56, 56);
                    go.Clicked += () => { CloseSheet(); OpenShop(); };
                    return;
                }
                const int cols = 4; const float cw = 146f;
                int rows = (ids.Count + cols - 1) / cols;
                var grid = Section(body, rows * (cw * 1.42f + 14f) + 10f, "Mano");
                for (int i = 0; i < ids.Count; i++)
                {
                    int id = ids[i];
                    var v = FxCardView(grid, id, cw, Isl.FxOwned[id]);
                    v.anchorMin = v.anchorMax = new Vector2(0f, 1f);
                    v.pivot = new Vector2(0f, 1f);
                    v.anchoredPosition = new Vector2(8f + (i % cols) * (cw + 12f), -6f - (i / cols) * (cw * 1.42f + 14f));
                    var b = v.gameObject.AddComponent<Btn>();
                    b.Clicked += () => { CloseSheet(); PlayCardFly(id); };
                    v.localScale = Vector3.zero;
                    Tw.Scale(v, Vector3.zero, Vector3.one, 0.22f, Ease.OutBack, 0.03f * Mathf.Min(i, 12));
                }
            }
            else
            {
                const int cols = 6; const float cw = 96f;
                int rows = (Island.FxCount + cols - 1) / cols;
                var grid = Section(body, rows * (cw * 1.42f + 8f) + 10f, "Album");
                for (int i = 0; i < Island.FxCount; i++)
                {
                    var v = FxCardView(grid, i, cw, 1, Isl.FxSeen[i]);
                    v.anchorMin = v.anchorMax = new Vector2(0f, 1f);
                    v.pivot = new Vector2(0f, 1f);
                    v.anchoredPosition = new Vector2(6f + (i % cols) * (cw + 8f), -4f - (i / cols) * (cw * 1.42f + 8f));
                    int id = i;
                    if (Isl.FxSeen[i]) v.gameObject.AddComponent<Btn>().Clicked += () => Sfx.PlayExact("cj_" + id, -4f);   // cada una suena
                }
            }
        }

        // ------------------------------------------------------------ jugar una carta
        /// <summary>La carta sale de abajo, gira y vuela al centro de la vista; al llegar pasa el efecto.</summary>
        public void PlayCardFly(int id)
        {
            if (Isl.FxOwned[id] <= 0) return;
            Vector3 g = game.ScreenToGround(new Vector2(game.Cam.pixelWidth * 0.5f, game.Cam.pixelHeight * 0.5f));
            float r = Mathf.Sqrt(g.x * g.x + g.z * g.z), lim = Isl.Radius - 2f;
            if (r > lim) g = g / r * lim;   // si se mira el mar: lo mas cerca en la isla
            var card = FxCardView(flyLayer, id, 150, 1);
            Vector2 from = new Vector2(0f, -Kit.CanvasSize.y * 0.5f + 140f);
            Vector2 c = ToCanvas(g);
            Vector2 to = new Vector2(c.x, -c.y);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = from;
            Sfx.Play("card", -4f, 1.1f);
            Sfx.Play("whoosh", -10f, 1.2f);
            Tw.To(card, "vuela", 0.55f, Ease.Linear, u =>
            {
                float e = 1f - (1f - u) * (1f - u);
                card.anchoredPosition = Vector2.Lerp(from, to, e) + Vector2.up * Mathf.Sin(u * Mathf.PI) * 160f;
                card.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.35f, e);
                card.localRotation = Quaternion.Euler(0, 0, u * 540f);
            }, () =>
            {
                Destroy(card.gameObject);
                if (Isl.PlayFxCard(id, g.x, g.z)) { Sfx.PlayExact("cj_" + id, -2f); Juice.Vibrate(30); game.Save(); }
            });
        }

        // ------------------------------------------------------------ sobres
        void PackSection(Transform body)
        {
            var s = Section(body, 290, "Sobres");
            Kit.LabelAt(s, Loc.T("Sobres de cartas"), 30, Kit.Brown, 0, true, 0, 4, 640, 40, TextAnchor.MiddleCenter);
            for (int k = 0; k < 2; k++)
            {
                bool big = k == 1;
                var box = Kit.OutBox(s, 18, 4, 5, big ? Kit.Cream : Color.white, big ? Icons.H("a36be8") : Kit.Out, "Sobre");
                Kit.PlaceTL(box, 10 + k * 320, 50, 300, 230);
                int n = big ? 5 : 3;
                for (int i = 0; i < n; i++)
                {
                    var ci = Kit.Img(box, FxCardArt.Back(), Color.white, "Carta"); ci.preserveAspect = true;
                    var rt = ci.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(62, 88);
                    rt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * 28f, -70f);
                    rt.localRotation = Quaternion.Euler(0, 0, ((n - 1) * 0.5f - i) * 9f);
                }
                Kit.LabelAt(box, "x" + n, 26, Kit.Brown, 0, true, 0, 118, 300, 34, TextAnchor.MiddleCenter);
                var b = Kit.Button(box, "", Kit.Purple, 26, 200, 62, "Abrir");
                Kit.Place((RectTransform)b.transform, 0.5f, 1f, -100f, -72f, 200, 62);
                var gi = Kit.Icon(b.Content, "gem", 34); Kit.PlaceTL((RectTransform)gi.transform, 26, 12, 34, 34);
                var gl = Kit.Label(b.Content, (big ? Island.BigPackGems : Island.PackGems).ToString(), 28, Color.white, 4, true, TextAnchor.MiddleCenter); Kit.Stretch(gl.rectTransform, 50, 0, 10, 4);
                b.Clicked += () =>
                {
                    var got = Isl.OpenPack(big);
                    if (got == null) { NoMoney(b); Toast(Loc.T("Te faltan gemas: las dan las metas y los barcos"), Kit.Gray, Icons.Get("gem")); return; }
                    game.Save();
                    CloseSheet();
                    Tw.After(this, "sobre", 0.25f, () => OpenPackUi(got));
                };
            }
        }

        /// <summary>Apertura: las cartas caen dadas vuelta; tocarlas (o esperar) las da vuelta de a una, la mejor al final.</summary>
        public void OpenPackUi(List<int> ids)
        {
            var fr = OpenSheet(900, true);
            sheetLocked = true;
            Sfx.Play("chest", -4f);
            int n = ids.Count;
            var cards = new List<RectTransform>();
            int shown = 0;
            System.Action next = null;
            var done = Kit.Button(fr, "", Kit.Green, 30, 220, 84, "Listo");
            Kit.Place((RectTransform)done.transform, 0.5f, 1f, -110f, -110f, 220, 84);
            var di = Kit.Icon(done.Content, "check", 50); Kit.Place((RectTransform)di.transform, 0.5f, 0.5f, -25f, -29f, 50, 50);
            done.gameObject.SetActive(false);
            done.Clicked += () => { sheetLocked = false; CloseSheet(); };
            primaryBtn = done;
            float cw = n > 3 ? 160f : 190f;
            for (int i = 0; i < n; i++)
            {
                int id = ids[i];
                var holder = Kit.New("Carta", fr);
                holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
                holder.sizeDelta = new Vector2(cw, cw * 1.42f);
                float x = n == 5 ? (i < 3 ? (i - 1) * (cw + 20f) : (i - 3.5f) * (cw + 20f)) : (i - (n - 1) * 0.5f) * (cw + 20f);
                float y = n == 5 ? (i < 3 ? 150f : -120f) : 40f;
                Vector2 pos = new Vector2(x, y);
                var back = Kit.Img(holder, FxCardArt.Back(), Color.white, "Dorso"); back.preserveAspect = true; Kit.Stretch(back.rectTransform);
                var front = FxCardView(holder, id, cw, 1);
                front.anchorMin = front.anchorMax = new Vector2(0.5f, 0.5f);
                front.anchoredPosition = Vector2.zero;
                front.gameObject.SetActive(false);
                if (Isl.FxOwned[id] == 1 && Isl.Stat("fxnew_" + id) == 0)
                {
                    Isl.AddStat("fxnew_" + id, 1);
                    var nb = Kit.MakeTag(front, "NEW", Kit.Red, 18);
                    nb.anchorMin = nb.anchorMax = new Vector2(0.5f, 1f);
                    nb.anchoredPosition = new Vector2(0f, 4f);
                }
                holder.anchoredPosition = pos + new Vector2(0f, -900f);
                Tw.MoveFrom(holder, pos + new Vector2(0f, -900f), pos, 0.4f, Ease.OutBack, 0.1f * i);
                cards.Add(holder);
                var tap = back.gameObject.AddComponent<Btn>();
                tap.Juice = false;
                int rar = Island.FxCatalog[id].Rarity;
                bool open = false;
                System.Action flip = () =>
                {
                    if (open) return;
                    open = true;
                    float wait = rar >= 2 ? 0.55f : 0f;
                    if (rar >= 2)
                    {
                        // suspenso: tiembla y brilla del color de la rareza
                        Color rc = FxCardArt.RarityCol[rar];
                        var glow = Kit.Glow(holder, new Color(rc.r, rc.g, rc.b, 0f), "Brillo");
                        glow.rectTransform.sizeDelta = new Vector2(cw * 2.2f, cw * 2.6f);
                        glow.transform.SetAsFirstSibling();
                        Sfx.Play("magic_rise", -8f, 1.2f + rar * 0.1f);
                        Tw.To(holder, "susp", 0.55f, Ease.Linear, u => { holder.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 60f) * 5f * u); glow.color = new Color(rc.r, rc.g, rc.b, u * 0.85f); }, () => holder.localRotation = Quaternion.identity);
                    }
                    Tw.After(holder, "voltea", wait, () => Flip(holder, back.rectTransform, front, () =>
                    {
                        Sfx.PlayExact("cj_" + id, -2f);   // la melodia de ESTA carta
                        if (rar >= 2) { Flash(FxCardArt.RarityCol[rar], 0.25f); Mineros.Fx.Haptics.Heavy(); }
                        else Mineros.Fx.Haptics.Light();
                        Tw.Pop(holder, 1.12f);
                        shown++;
                        if (shown >= n) { done.gameObject.SetActive(true); Tw.Pop(done.transform, 1.2f); }
                        else Tw.After(fr, "siguiente", 0.35f, () => next());
                    }));
                };
                tap.Clicked += flip;
                if (i == 0) next = flip;
            }
            // se dan vuelta solas en orden si no se tocan (la mejor al final)
            int k2 = 0;
            next = () => { while (k2 < cards.Count) { var t = cards[k2++].Find("Dorso"); if (t != null && t.gameObject.activeSelf) { t.GetComponent<Btn>().Click(); return; } } };
            Tw.After(fr, "primera", 0.9f, () => next());
        }

        /// <summary>Carta nueva fuera de un sobre (regalo, cofre): aviso con su dibujo.</summary>
        public void FxCardGot(int id)
        {
            if (sheet != null) return;   // en sobres y cofres ya se ve
            Toast(Island.FxCatalog[id].Name, FxCardArt.RarityCol[Island.FxCatalog[id].Rarity], FxCardArt.Get(id), true);
            Sfx.PlayExact("cj_" + id, -6f);
            if (cardsBtn != null) Tw.Pop(cardsBtn.transform, 1.3f);
        }
    }
}
