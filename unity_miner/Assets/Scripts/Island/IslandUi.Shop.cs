using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Monetization;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Tienda y anuncios con premio en la interfaz (biblia de produccion C y B.10): la tienda se abre tocando las gemas;
    /// los anuncios son siempre botones naranjas con el icono de TV y el premio escrito (nunca aparecen solos); comprar se
    /// siente generoso (lo comprado cae en cascada y vuela a su contador). Restaurar compras siempre visible (iOS).
    /// </summary>
    public sealed partial class IslandUi
    {
        static Sprite tvIcon;

        /// <summary>Icono de TV (anuncio) dibujado a 4x.</summary>
        public static Sprite TvIcon()
        {
            if (tvIcon != null) return tvIcon;
            var p = new Painter(192, 192, 4f, 3);
            p.ORR(new Rect(5, 11, 38, 28), 6f, Icons.H("3b3f47"), 3f);
            p.ORR(new Rect(9, 15, 30, 20), 3f, Icons.H("7fd3f0"), 1.5f);
            p.Poly(new[] { new Vector2(20, 19), new Vector2(29, 25), new Vector2(20, 31) }, Color.white);
            p.Line(new Vector2(17, 4), new Vector2(23, 11), 2.5f, Painter.Out);
            p.Line(new Vector2(31, 4), new Vector2(25, 11), 2.5f, Painter.Out);
            var t = p.ToTexture();
            tvIcon = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
            return tvIcon;
        }

        double Now { get { return game.NowSecondsPublic; } }

        /// <summary>Boton naranja de anuncio: icono de TV a la izquierda y el premio escrito.</summary>
        Btn AdButton(Transform parent, string reward, float w, float h, int fsize = 24)
        {
            var b = Kit.Button(parent, reward, Kit.Orange, fsize, w, h, "Anuncio");
            var ic = Kit.Img(b.Content, TvIcon(), Color.white, "TV");
            ic.preserveAspect = true;
            Kit.PlaceTL(ic.rectTransform, 10, (h - 14) * 0.5f - 22, 44, 44);
            b.Label.alignment = TextAnchor.MiddleCenter;
            Kit.Stretch(b.Label.rectTransform, 54, 0, 10, 4);
            return b;
        }

        /// <summary>Mira un anuncio y entrega el premio del lugar (o al instante si sos Capataz).</summary>
        void WatchAd(AdPlace place, Plot plot, double amount, System.Action<string> onReward)
        {
            int today = IslandGame.Today;
            if (!Isl.CanAd(place, today, Now)) { Toast(Loc.T("Ya viste todos los de hoy. ¡Mañana hay más!"), Kit.Gray); return; }
            System.Action give = () =>
            {
                string got = Isl.GrantAd(place, today, Now, plot, amount);
                if (got == "") return;
                game.Save();
                Sfx.Play("tadaa", -4f);
                Juice.Vibrate(20);
                Toast(got, Kit.Orange, TvIcon(), true);
                onReward?.Invoke(got);
            };
            if (Isl.Capataz) { give(); return; }
            System.Action show = () =>
            {
                Sfx.Duck(30f, 30f);
                Ads.Show(ok =>
                {
                    Sfx.Duck(0f, 0.1f);
                    if (ok) give();
                    else Toast(Loc.T("El anuncio no terminó: no hay premio esta vez"), Kit.Gray, null, true);
                });
            };
            if (Ads.Ready) { show(); return; }
            // no estaba listo: se pide ya y, si llega en 8 s, se muestra solo (antes solo decia "probá en unos segundos")
            if (adWaiting) return;
            Toast(Loc.T("Cargando el anuncio…"), Kit.Gray);
            Ads.Init();
            StartCoroutine(WaitAd(show));
        }

        bool adWaiting;

        System.Collections.IEnumerator WaitAd(System.Action show)
        {
            adWaiting = true;
            float t0 = Time.unscaledTime;
            while (!Ads.Ready && Time.unscaledTime - t0 < 15f) yield return null;
            adWaiting = false;
            if (Ads.Ready) show();
            else Toast(Loc.T("Ahora no hay anuncios disponibles. Probá en un rato") + (string.IsNullOrEmpty(Ads.LastError) ? "" : " (" + Ads.LastError + ")"), Kit.Gray, null, true);
        }

        // ------------------------------------------------------------ cofre por anuncio en el HUD
        Btn adChestBtn;

        void UpdateAdChest()
        {
            bool show = Isl.TutDone && Isl.CanAd(AdPlace.Chest, IslandGame.Today, Now) && sheet == null;
            if (adChestBtn == null)
            {
                // cofre gratis por anuncio: cuadradito naranja chico abajo a la izquierda (sin texto: cofre + TV)
                adChestBtn = Kit.HitArea(hudLayer, 76, 76, "CofreAnuncio");
                Kit.Place((RectTransform)adChestBtn.transform, 0f, 1f, 26f, -268f, 76, 76);
                var box = Kit.RoundImg(adChestBtn.transform, 24, Kit.Orange, "Caja");
                Kit.Stretch(box.rectTransform);
                var ch = Kit.Img(box.transform, ChestSprite(), Color.white, "Cofre");
                ch.preserveAspect = true;
                Kit.PlaceTL(ch.rectTransform, 6, 4, 56, 56);
                var tv = Kit.Img(box.transform, TvIcon(), Color.white, "TV");
                Kit.PlaceTL(tv.rectTransform, 44, 42, 30, 30);
                adChestBtn.Clicked += () => WatchAd(AdPlace.Chest, null, 0, got => Tw.Pop(adChestBtn.transform, 1.3f));
                adChestBtn.gameObject.SetActive(false);
            }
            if (adChestBtn.gameObject.activeSelf != show) { adChestBtn.gameObject.SetActive(show); if (show) Tw.Pop(adChestBtn.transform, 1.3f); }
            if (show && !adChestBtn.IsPressed && Time.unscaledTime > adChestBtn.QuietUntil)
                adChestBtn.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 2f) * 2f);
        }

        // ------------------------------------------------------------ giros guardados
        Btn spinBtn;
        Text spinN;

        /// <summary>Giros de ruleta guardados (se cerro la ruleta sin usarlos): boton violeta con el numero, abre la ruleta.</summary>
        void UpdateSpinButton()
        {
            bool show = Isl.TutDone && Isl.Spins > 0 && sheet == null && !Isl.BalloonHere;
            if (spinBtn == null)
            {
                spinBtn = Kit.HitArea(hudLayer, 76, 76, "Giros");
                Kit.Place((RectTransform)spinBtn.transform, 0f, 1f, 26f, -356f, 76, 76);
                var box = Kit.RoundImg(spinBtn.transform, 24, Kit.Purple, "Caja");
                Kit.Stretch(box.rectTransform);
                var ic = Kit.Icon(box.transform, "star", 46);
                Kit.PlaceTL((RectTransform)ic.transform, 15, 10, 46, 46);
                var badge = Kit.RoundImg(box.transform, 14, Kit.Red, "Numero");
                Kit.PlaceTL(badge.rectTransform, 48, -8, 36, 30);
                spinN = Kit.Label(badge.transform, "1", 20, Color.white, 4, true, TextAnchor.MiddleCenter);
                Kit.Stretch(spinN.rectTransform);
                spinBtn.Clicked += OpenWheel;
                spinBtn.gameObject.SetActive(false);
            }
            if (spinBtn.gameObject.activeSelf != show) { spinBtn.gameObject.SetActive(show); if (show) Tw.Pop(spinBtn.transform, 1.3f); }
            if (show) spinN.text = Isl.Spins.ToString();
        }

        // ------------------------------------------------------------ tienda
        public void OpenShop()
        {
            cityPlot = null;
            var fr = OpenSheet(1180);
            Kit.LabelAt(fr, Loc.T("Tienda"), 44, Kit.Brown, 0, true, 0, 16, 680, 54, TextAnchor.MiddleCenter);
            var sc = Kit.Scroll(fr, 12f, "Lista");
            Kit.PlaceTL((RectTransform)sc.Scroll.transform, 20, 84, 640, 1180 - 104);
            var body = sc.Content;
            if (Isl.CanOffer("starter")) Offer(body, "starter", Loc.T("¡OFERTA DE INICIO!"), Loc.T("x5 VALOR"), Kit.Red, 210);
            if (Isl.CanOffer("piggy")) Offer(body, "piggy", Loc.T("Alcancía: ") + Isl.Piggy + Loc.T(" gemas"), Loc.T("Se llena jugando"), Kit.Purple, 170);
            // packs de gemas en grilla de 3
            string[] packs = { "gems_s", "gems_m", "gems_l", "gems_xl", "gems_xxl", "gems_xxxl" };
            var grid = Section(body, 2 * 250 + 20, "Gemas");
            for (int i = 0; i < packs.Length; i++) GemCell(grid, packs[i], 6 + (i % 3) * 212, (i / 3) * 256);
            if (Isl.CanOffer("builder")) Offer(body, "builder", Loc.T("Constructor extra"), Loc.T("Para siempre"), Kit.Blue, 170);
            if (Isl.CanOffer("pass")) Offer(body, "pass", Loc.T("Pase dorado"), Loc.T("Temporada actual"), Kit.Orange, 170);
            if (Isl.CanOffer("capataz")) Offer(body, "capataz", Loc.T("Capataz (suscripción mensual)"), Loc.T("Se renueva cada mes · cancelás cuando quieras desde la tienda"), Kit.Green, 200);
            else if (Isl.Capataz) { var s = Section(body, 60); Kit.LabelAt(s, Loc.T("¡Sos Capataz! Gracias por apoyar el juego."), 24, Kit.GreenD, 0, true, 0, 12, 640, 36, TextAnchor.MiddleCenter); }
            // restaurar (obligatorio en iOS) y aviso
            var rs = Section(body, 230, "Restaurar");
            var rb = Kit.Button(rs, Loc.T("Restaurar compras"), Kit.Blue, 24, 320, 70);
            Kit.Place((RectTransform)rb.transform, 0.5f, 0f, -160f, 10f, 320, 70);
            rb.Clicked += () => Store.Restore(ok => Toast(ok ? Loc.T("Compras restauradas") : Loc.T("No se pudo restaurar ahora"), ok ? Kit.Green : Kit.Gray, null, true));
            var legal = Kit.LabelAt(rs, Loc.T("Todo lo que se compra también se puede ganar jugando. Los pagos los procesa la tienda de tu teléfono."), 16, Kit.Brown, 0, false, 10, 90, 620, 50);
            Kit.Wrap(legal); legal.alignment = TextAnchor.UpperCenter;
            // enlaces que piden Apple (3.1.1 probabilidades, 3.1.2 suscripciones) y Google Play
            string[] lt = { Loc.T("Probabilidades"), Loc.T("Términos"), Loc.T("Privacidad") };
            string[] lu = { Mineros.Monetization.Legal.Odds, Mineros.Monetization.Legal.Terms, Mineros.Monetization.Legal.Privacy };
            for (int i = 0; i < 3; i++)
            {
                string url = lu[i];
                var b = Kit.Button(rs, lt[i], Kit.Gray, 20, 200, 56, "Legal");
                Kit.PlaceTL((RectTransform)b.transform, 10 + i * 212, 156, 200, 56);
                b.Clicked += () => Application.OpenURL(url);
            }
        }

        void Offer(Transform body, string id, string title, string tag, Color col, float h)
        {
            if (id == "piggy") h += 26f;
            var item = Island.ShopOf(id);
            var s = Section(body, h, "Oferta");
            var box = Kit.OutBox(s, 20, 4, 6, Kit.Cream, col, "Caja");
            Kit.Stretch(box, 2, 2, 2, 2);
            var ic = Kit.Img(box, OfferIcon(id), Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.PlaceTL(ic.rectTransform, 14, (h - 120) * 0.5f, 110, 110);
            Kit.LabelAt(box, title, 26, Kit.Brown, 0, true, 134, 12, 300, 34);
            var d = Kit.LabelAt(box, item.Desc, 18, Kit.Brown, 0, false, 134, 48, 290, h - 70);
            Kit.Wrap(d);
            var t = Kit.MakeTag(box, tag, col, 14);
            Kit.Place(t, 1f, 0f, -t.sizeDelta.x - 16f, 10f, t.sizeDelta.x, t.sizeDelta.y);
            var b = Kit.Button(box, Store.Price(id), Kit.Purple, 24, 190, 80, Loc.T("Comprar"));
            Kit.Place((RectTransform)b.transform, 1f, 1f, -206f, -96f, 190, 80);
            AddShine(b, () => true);
            if (id == "piggy")
            {
                // la alcancia se ve llenarse: barra de monedas y el chanchito que se mece cuando esta lleno
                var bar = Kit.RoundImg(box, 8, Icons.H("eadcc0"), "Llenado");
                Kit.PlaceTL(bar.rectTransform, 134, h - 40f, 270, 16);
                var fill = Kit.RoundImg(bar.transform, 8, Kit.Purple, "Relleno");
                var fr = fill.rectTransform;
                fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f);
                fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
                float f = Mathf.Clamp01(Isl.Piggy / (float)Island.PiggyCap);
                Tw.To(fr, "llena", 0.8f, Ease.OutCubic, u => fr.sizeDelta = new Vector2(270f * f * u, 0f));
                var pr = ic.rectTransform;
                Tw.To(pr, "mece", 99f, Ease.Linear, u => pr.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * (f >= 1f ? 9f : 2f)) * (f >= 1f ? 6f : 2f)));
            }
            b.Clicked += () => Purchase(id, b);
        }

        void GemCell(Transform grid, string id, float x, float y)
        {
            var item = Island.ShopOf(id);
            bool first = !Isl.FirstDone.Contains(id);
            var box = Kit.OutBox(grid, 18, 4, 5, Color.white, Kit.Out, "Pack");
            Kit.PlaceTL(box, x, y, 204, 244);
            // montoncito de gemas que crece con el pack (1 a 6 gemas apiladas), cada una con su brillo
            int tier = System.Array.IndexOf(new[] { "gems_s", "gems_m", "gems_l", "gems_xl", "gems_xxl", "gems_xxxl" }, id);
            Vector2[] pile = { new Vector2(0, 0), new Vector2(-26, -14), new Vector2(26, -14), new Vector2(-14, 14), new Vector2(16, 12), new Vector2(0, 30) };
            int ng = Mathf.Clamp(tier + 1, 1, 6);
            for (int gi = ng - 1; gi >= 0; gi--)
            {
                var ic = Kit.Img(box, Icons.Get("gem"), Color.white, "Gema");
                ic.preserveAspect = true;
                float gs = ng == 1 ? 76f : 58f;
                Vector2 o = ng == 1 ? Vector2.zero : pile[gi];
                Kit.Place(ic.rectTransform, 0.5f, 0f, -gs * 0.5f + o.x, 46f - gs * 0.5f - o.y + 20f, gs, gs);
                ic.rectTransform.localRotation = Quaternion.Euler(0, 0, (gi % 2 == 0 ? -1f : 1f) * gi * 4f);
            }
            Kit.LabelAt(box, (item.Gems * (first ? 2 : 1)).ToString("N0", Loc.Culture), 30, Kit.Brown, 0, true, 0, 122, 204, 36, TextAnchor.MiddleCenter);
            if (first)
            {
                var t = Kit.MakeTag(box, Loc.T("¡x2 la 1.ª vez!"), Kit.Red, 13);
                Kit.Place(t, 0.5f, 0f, -t.sizeDelta.x * 0.5f, -8f, t.sizeDelta.x, t.sizeDelta.y);
            }
            var b = Kit.Button(box, Store.Price(id), Kit.Purple, 22, 180, 66, Loc.T("Comprar"));
            Kit.Place((RectTransform)b.transform, 0.5f, 1f, -90f, -76f, 180, 66);
            b.Clicked += () => Purchase(id, b);
        }

        Sprite OfferIcon(string id)
        {
            switch (id)
            {
                case "starter": return ChestSprite();
                case "piggy": return TripoIcon("cx_chanchito", "piggy");
                case "builder": return Icons.Get("hammer");
                case "pass": return Icons.Get("star");
                case "capataz": return Icons.Get("trophy");
                default: return Icons.Get("gem");
            }
        }

        void Purchase(string id, Btn b)
        {
            Store.Buy(id, (ok, got) =>
            {
                if (!ok) { if (!string.IsNullOrEmpty(got)) { Toast(got, Kit.Gray, null, true); NoMoney(b); } return; }
                PurchaseShow(id, got);
            });
        }

        /// <summary>B.10.2: lo comprado cae en cascada, se apila con rebote y vuela a su contador.</summary>
        void PurchaseShow(string id, string got)
        {
            if (id == "piggy") { BreakPiggy(got); return; }
            CloseSheet();
            game.Save();
            Sfx.Play("coins_pour", -2f);
            Sfx.PlayLater("fanfare", 0.2f, -4f);
            game.DoublePulse();
            Banner(Loc.T("¡Gracias!"), got, Kit.Purple, OfferIcon(id), null);
            var item = Island.ShopOf(id);
            if (item != null && item.Gems > 0) FlyGems(Vector2.zero, Mathf.Min(14, 4 + item.Gems / 200));
        }

        /// <summary>
        /// Romper la alcancia: el chanchito aparece grande en el centro, tiembla tres veces (cada golpe mas fuerte, con
        /// vibracion que sube), se agrieta y explota en pedazos y una lluvia de gemas que vuelan al contador.
        /// </summary>
        void BreakPiggy(string got)
        {
            CloseSheet();
            game.Save();
            var pig = Kit.Img(flyLayer, TripoIcon("cx_chanchito", "piggy"), Color.white, "Chanchito");
            pig.preserveAspect = true;
            var rt = pig.rectTransform;
            rt.sizeDelta = new Vector2(260, 260);
            rt.anchoredPosition = new Vector2(0f, 80f);
            Tw.Scale(rt, Vector3.zero, Vector3.one, 0.35f, Ease.OutBack);
            Sfx.Play("pop", -4f, 0.8f);
            for (int hit = 0; hit < 3; hit++)
            {
                int h = hit;
                Tw.After(this, "golpe" + h, 0.5f + h * 0.38f, () =>
                {
                    Sfx.Play("break", -8f, 1.4f + h * 0.12f);
                    Sfx.Play("thud", -6f, 1.2f + h * 0.1f);
                    if (h == 0) Mineros.Fx.Haptics.Light(); else if (h == 1) Mineros.Fx.Haptics.Medium(); else Mineros.Fx.Haptics.Heavy();
                    Tw.To(rt, "tiembla", 0.3f, Ease.Linear, u =>
                    {
                        float k = (1f - u) * (6f + h * 5f);
                        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 60f) * k);
                        rt.localScale = Vector3.one * (1f + Mathf.Sin(u * Mathf.PI) * 0.08f * (h + 1));
                    });
                    // grieta: una linea blanca que crece con cada golpe
                    var crack = Kit.Img(rt, Kit.White, new Color(1f, 1f, 1f, 0.9f), "Grieta");
                    crack.rectTransform.sizeDelta = new Vector2(6f, 40f + h * 30f);
                    crack.rectTransform.anchoredPosition = new Vector2(-20f + h * 22f, 10f - h * 8f);
                    crack.rectTransform.localRotation = Quaternion.Euler(0, 0, 25f - h * 40f);
                });
            }
            Tw.After(this, "explota", 0.5f + 3 * 0.38f + 0.15f, () =>
            {
                Vector2 at = rt.anchoredPosition;
                Destroy(pig.gameObject);
                Flash(new Color(1f, 0.85f, 1f), 0.25f);
                Sfx.Play("break", -2f, 1.1f);
                Sfx.PlayLater("coins_pour", 0.1f, -3f);
                Sfx.PlayLater("fanfare", 0.25f, -5f);
                Mineros.Fx.Haptics.Heavy();
                Tw.After(this, "exito", 0.18f, () => Mineros.Fx.Haptics.Success());
                // pedazos de ceramica que salen girando
                for (int i = 0; i < 9; i++)
                {
                    var sh = UiPool.Get(flyLayer, Kit.White, Icons.H("f4a6c0"), "Pedazo");
                    var sr = sh.rectTransform;
                    sr.sizeDelta = new Vector2(Random.Range(18f, 34f), Random.Range(14f, 26f));
                    float a = i / 9f * Mathf.PI * 2f + Random.value * 0.4f;
                    Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(380f, 620f);
                    float spin = Random.Range(-720f, 720f);
                    Tw.To(sr, "vuela", 0.9f, Ease.Linear, u =>
                    {
                        sr.anchoredPosition = at + v * u + new Vector2(0f, -900f * u * u);
                        sr.localRotation = Quaternion.Euler(0, 0, spin * u);
                        sh.color = new Color(0.96f, 0.65f, 0.75f, 1f - u);
                    }, () => UiPool.Release(sh));
                }
                FlyGems(at, 8);
                Banner(Loc.T("¡Gracias!"), got, Kit.Purple, TripoIcon("cx_chanchito", "piggy"), null);
            });
        }

        // ------------------------------------------------------------ oferta de inicio (una sola vez, con el Ayuntamiento 3)
        void CheckStarterOffer()
        {
            if (sheet != null || !Isl.TutDone || Isl.Th < 3 || !Isl.CanOffer("starter") || Application.isBatchMode) return;
            if (PlayerPrefs.GetInt("oferta_inicio_vista", 0) == 1) return;
            PlayerPrefs.SetInt("oferta_inicio_vista", 1);
            Tw.After(this, "oferta", 2f, () => { if (sheet == null) OpenShop(); });
        }

        void UpdateShopUi()
        {
            UpdateAdChest();
            UpdateSpinButton();
            UpdateMinerCard();
            CheckStarterOffer();
        }
    }
}
