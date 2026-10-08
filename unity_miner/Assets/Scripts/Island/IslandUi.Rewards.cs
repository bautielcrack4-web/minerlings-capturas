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
    /// Interfaz de las recompensas y los momentos grandes: cartel de construccion/evolucion (con iconos antes → despues),
    /// destello de pantalla, ruleta 3D del mercader, cofres con apertura de suspenso, boton de cofres, marca del globo y
    /// vista previa de la proxima etapa del edificio (silueta).
    /// </summary>
    public sealed partial class IslandUi
    {
        // ------------------------------------------------------------ cartel grande
        RectTransform banner;

        public void Banner(string title, string sub, Color col, Sprite a, Sprite b)
        {
            // un cartel a la vez: el nuevo reemplaza al anterior
            if (banner != null) { Tw.Kill(banner); Destroy(banner.gameObject); }
            var box = Kit.OutBox(toastLayer, 28, 5, 7, Kit.Cream, Kit.Out, "Cartel");
            banner = box;
            float h = a != null ? 300f : 150f;
            Kit.Place(box, 0.5f, 0f, -320f, 250f, 640, h);
            var head = Kit.OutBox(box, 22, 4, 5, col, Kit.Out, "Titulo");
            Kit.Place(head, 0.5f, 0f, -250f, -26f, 500, 70);
            var tl = Kit.Label(head, title, 36, Color.white, 7, true, TextAnchor.MiddleCenter);
            Kit.Stretch(tl.rectTransform, 0, 0, 0, 6);
            float y = 56f;
            if (a != null)
            {
                if (b != null)
                {
                    var ia = Kit.Img(box, a, Color.white, "Antes"); ia.preserveAspect = true;
                    Kit.Place(ia.rectTransform, 0.5f, 0f, -250f, y, 170, 170);
                    var arrowL = Kit.Label(box, "→", 64, Kit.OrangeD, 0, true, TextAnchor.MiddleCenter);
                    Kit.Place(arrowL.rectTransform, 0.5f, 0f, -50f, y + 40f, 100, 90);
                    var ib = Kit.Img(box, b, Color.white, "Despues"); ib.preserveAspect = true;
                    Kit.Place(ib.rectTransform, 0.5f, 0f, 60f, y - 10f, 200, 200);
                    Tw.Scale(ib.rectTransform, Vector3.one * 0.3f, Vector3.one, 0.5f, Ease.OutBack, 0.25f);
                }
                else
                {
                    var ia = Kit.Img(box, a, Color.white, "Icono"); ia.preserveAspect = true;
                    Kit.Place(ia.rectTransform, 0.5f, 0f, -95f, y - 6f, 190, 190);
                    Tw.Scale(ia.rectTransform, Vector3.one * 0.3f, Vector3.one, 0.5f, Ease.OutBack, 0.15f);
                }
                y += 180f;
            }
            var sl = Kit.LabelAt(box, sub, 26, Kit.Brown, 0, true, 30, y, 580, 60, TextAnchor.MiddleCenter);
            Kit.Wrap(sl);
            var grp = Kit.Group(box.gameObject);
            Tw.Scale(box, Vector3.one * 0.5f, Vector3.one, 0.35f, Ease.OutBack);
            Tw.After(box, "fin", 2.6f, () =>
            {
                Tw.Alpha(grp, 0f, 0.3f, 0f, () => Destroy(box.gameObject));
                Tw.Scale(box, Vector3.one * 0.9f, 0.3f, Ease.Linear);
            });
        }

        // ------------------------------------------------------------ destello de pantalla
        public void Flash(Color c, float dur)
        {
            var img = Kit.Tint(flyLayer, c, "Destello");
            Kit.Stretch(img.rectTransform);
            // tope 0.8: se siente el destello sin tapar la isla del todo
            Tw.To(img, "flash", dur, Ease.Linear, u => img.color = new Color(c.r, c.g, c.b, 0.8f * (1f - u) * (1f - u)), () => Destroy(img.gameObject));
        }

        Image coinGlow;

        void CoinGlow()
        {
            if (coinGlow == null)
            {
                coinGlow = Kit.Glow(coinPill.Root, new Color(1f, 0.85f, 0.3f, 0f), "Brillo");
                coinGlow.rectTransform.sizeDelta = new Vector2(150, 150);
                coinGlow.rectTransform.anchoredPosition = new Vector2(-95f, 0f);
                coinGlow.transform.SetAsFirstSibling();
            }
            Tw.To(coinGlow, "glow", 0.35f, Ease.Linear, u => coinGlow.color = new Color(1f, 0.85f, 0.3f, Mathf.Sin(u * Mathf.PI) * 0.8f));
        }

        // ------------------------------------------------------------ boton de cofres y marca del globo
        Btn chestBtn;
        Badge chestBadge;
        RectTransform balloonMark;
        static Sprite chestSprite;

        Sprite ChestSprite()
        {
            if (chestSprite != null) return chestSprite;
            Material tm;
            var mesh = IslandArt.TripoModel("chest", out tm);
            chestSprite = mesh != null ? IslandStage.I.RenderIcon(mesh, new[] { tm }, 256) : Icons.Get("chest");
            return chestSprite;
        }

        public void ChestGot(int tier)
        {
            Toast(Loc.T("¡") + Island.ChestName[tier] + "!", tier >= 2 ? Kit.Orange : Kit.Blue, tier >= 2 ? TripoIcon("cx_cofreepico", "chest") : null);
            Sfx.Play("gem", -4f);
            if (chestBtn != null) Tw.Pop(chestBtn.transform, 1.35f);
        }

        void UpdateRewards()
        {
            // cofres
            if (chestBtn == null)
            {
                // inventario de cofres: la unica excepcion visible del HUD, y solo cuando hay algo adentro
                chestBtn = Kit.HitArea(hudLayer, 84, 84, "Cofres");
                Kit.Place((RectTransform)chestBtn.transform, 0f, 1f, 22f, -176f, 84, 84);
                var bg = Kit.RoundImg(chestBtn.transform, 26, GlassCol, "Fondo");
                Kit.Stretch(bg.rectTransform);
                var ic = Kit.Img(chestBtn.transform, ChestSprite(), Color.white, "Cofre");
                ic.preserveAspect = true;
                Kit.Stretch(ic.rectTransform, 8, 8, 8, 8);
                chestBadge = Kit.MakeBadge(chestBtn.transform);
                Kit.Place(chestBadge.Root, 1f, 0f, -22f, -6f, 26, 26);
                chestBadge.AlwaysNumber = true;
                chestBtn.Clicked += OpenChestUi;
            }
            bool hasChest = Isl.ChestCount > 0;
            if (chestBtn.gameObject.activeSelf != hasChest) chestBtn.gameObject.SetActive(hasChest);
            if (hasChest)
            {
                chestBadge.SetCount(Isl.ChestCount);
                if (!chestBtn.IsPressed && Time.unscaledTime > chestBtn.QuietUntil)
                {
                    float w = Mathf.Repeat(Time.time, 4f);
                    chestBtn.transform.localRotation = Quaternion.Euler(0, 0, w < 0.5f ? Mathf.Sin(w * 40f) * 6f * (1f - w * 2f) : 0f);
                }
            }
            // globo: "¡Tocame!" encima
            bool showB = Isl.BalloonHere && game.Ambient != null && game.Ambient.BalloonVisible && sheet == null;
            if (balloonMark == null)
            {
                balloonMark = Dot(worldLayer, Icons.Get("star"), Kit.Purple, 66, "RuletaGratis");   // globito con icono, sin texto
                balloonMark.gameObject.SetActive(false);
            }
            if (balloonMark.gameObject.activeSelf != showB) { balloonMark.gameObject.SetActive(showB); if (showB) Tw.Pop(balloonMark, 1.4f); }
            if (showB)
            {
                Vector2 c = ToCanvas(game.Ambient.BalloonPos + Vector3.up * 5.2f);
                balloonMark.anchoredPosition = new Vector2(c.x, -c.y + Mathf.Abs(Mathf.Sin(Time.time * 3.2f)) * 10f);

            }
        }

        // ------------------------------------------------------------ ruleta
        public void OpenWheel()
        {
            var st = IslandStage.I;
            var fr = OpenSheet(1000, true);
            sheetLocked = false;   // se puede cerrar (✕) cuando no gira: los giros quedan guardados para despues
            Kit.LabelAt(fr, Loc.T("Ruleta del Mercader"), 42, Kit.Brown, 0, true, 0, 22, 680, 56, TextAnchor.MiddleCenter);
            // medidor del premio mayor: 5 casilleros, el quinto giro es grande seguro
            var meter = Kit.New("Medidor", fr);
            Kit.Place(meter, 0.5f, 0f, -200f, 84f, 400, 40);
            var pips = new Image[Island.JackpotEvery];
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = Kit.RoundImg(meter, 9, new Color(0.85f, 0.78f, 0.66f), "Casilla");
                Kit.PlaceTL(pips[i].rectTransform, 8 + i * 62, 6, 52, 26);
            }
            var jack = Kit.LabelAt(fr, "", 22, Kit.Purple, 0, true, 0, 126, 680, 30, TextAnchor.MiddleCenter);
            System.Action paintMeter = () =>
            {
                int on = Mathf.Clamp(Isl.SpinStreak, 0, Island.JackpotEvery);
                for (int i = 0; i < pips.Length; i++)
                    pips[i].color = i < on ? Kit.Purple : i == pips.Length - 1 ? new Color(1f, 0.83f, 0.35f) : new Color(0.85f, 0.78f, 0.66f);
                int left = Island.JackpotEvery - on;
                jack.text = left <= 1 ? Loc.T("¡El próximo giro es PREMIO MAYOR!") : Loc.T("Premio mayor seguro en ") + left + Loc.T(" giros");
            };
            paintMeter();
            st.ShowWheel();
            var raw = Kit.New("Ruleta", fr).gameObject.AddComponent<RawImage>();
            raw.texture = st.Tex;
            raw.raycastTarget = true;
            Kit.Place(raw.rectTransform, 0.5f, 0f, -280f, 160f, 560, 560);
            // tocar la ruleta mientras gira la frena (de verdad); quieta, tocarla es girar
            var tapWheel = raw.gameObject.AddComponent<Btn>();
            tapWheel.Juice = false;
            tapWheel.PlaySound = false;
            var prize = Kit.LabelAt(fr, "", 40, Kit.OrangeD, 0, true, 0, 724, 680, 60, TextAnchor.MiddleCenter);
            var b = Kit.Button(fr, "", Kit.Green, 38, 400, 110);
            Kit.Place((RectTransform)b.transform, 0.5f, 0f, -200f, 800f, 400, 110);
            primaryBtn = b;
            int state = 0;   // 0 listo, 1 girando, 2 cobrado
            System.Action paint = () =>
            {
                if (state == 0) b.Label.text = Isl.Spins > 0 ? Loc.T("¡GIRAR!") + (Isl.Spins > 1 ? "  x" + Isl.Spins : "") : Loc.T("Sin giros");
                else if (state == 1) b.Label.text = Loc.T("¡FRENAR!");
                else b.Label.text = Isl.Spins > 0 ? Loc.T("Girar otra vez") : Loc.T("¡Genial!");
                b.Interactable = state == 1 || state == 2 || Isl.Spins > 0;
            };
            paint();
            st.SpinDone = () =>
            {
                int idx = Isl.PendingPrize;
                var w = idx >= 0 ? Island.Wheel[idx] : null;
                double coins = Isl.ClaimSpin();
                state = 2;
                sheetLocked = false;
                if (w != null)
                {
                    string txt = w.Kind == "coins" ? "+" + BigNum.Fmt(coins) + Loc.T(" monedas") : w.Kind == "giant" ? Loc.T("¡Veta de Oro gigante!") : w.Kind == "chest" ? (w.Amount >= 2 ? Loc.T("¡Cofre de oro!") : Loc.T("¡Cofre!")) : w.Kind == "turbo" ? Loc.T("¡Turbo 1 minuto!") : "+" + w.Amount + Loc.T(" gemas");
                    prize.text = txt;
                    Tw.Scale(prize.rectTransform, Vector3.one * 0.3f, Vector3.one, 0.45f, Ease.OutBack);
                    if (coins > 0) FlyCoins(new Vector2(0f, 120f), 6);
                    Juice.Vibrate(50);
                    if (w.Kind == "giant") Toast(Loc.T("¡Mirá el cielo!"), Kit.Orange);
                    if (Island.IsBigPrize(w)) { Flash(new Color(1f, 0.9f, 0.5f), 0.3f); Mineros.Fx.Haptics.Heavy(); Sfx.Play("milestone", -4f); }
                }
                paintMeter();
                game.Save();
                paint();
            };
            System.Action go = () =>
            {
                if (state == 1) { st.Hurry(); return; }
                if (state == 2 && Isl.Spins <= 0) { CloseSheet(); return; }
                if (state == 2) { state = 0; prize.text = ""; }
                int idx = Isl.Spin();
                if (idx < 0) return;
                state = 1;
                sheetLocked = true;
                st.Spin(idx);
                paint();
            };
            b.Clicked += go;
            tapWheel.Clicked += () => { if (state == 1 || Isl.Spins > 0) go(); };
            sheetRefresh.Add(paint);
        }

        // ------------------------------------------------------------ cofres
        /// <summary>Para capturas: aprieta el boton principal de la hoja abierta (girar / abrir cofre).</summary>
        public void DebugPress() { if (primaryBtn != null && primaryBtn.isActiveAndEnabled) primaryBtn.Click(); }
        Btn primaryBtn;

        static readonly Color[] ChestCol = { Icons.H("a8703f"), Icons.H("8f9fb0"), Icons.H("f0a81c"), Icons.H("8a4fe8") };

        /// <summary>
        /// Cofre: cae y tiembla; cada toque puede hacerlo subir de rareza (se encoge, destella y crece con el material
        /// nuevo); si no sube, tiembla y se abre. El botin sale en cartas de a una, la mejor al final.
        /// </summary>
        public void OpenChestUi()
        {
            if (Isl.ChestCount <= 0 && Isl.OpenTier < 0) return;
            int tier = Isl.BeginChest();
            if (tier < 0) return;
            var st = IslandStage.I;
            var fr = OpenSheet(1000, true);
            sheetLocked = true;
            var title = Kit.LabelAt(fr, Island.ChestName[tier], 42, ChestCol[tier], 6, true, 0, 22, 680, 56, TextAnchor.MiddleCenter);
            st.ShowChest(tier);
            var raw = Kit.New("Cofre", fr).gameObject.AddComponent<RawImage>();
            raw.texture = st.Tex;
            Kit.Place(raw.rectTransform, 0.5f, 0f, -320f, 80f, 640, 640);
            int tries = Island.ChestUpgradeTries;
            var hint = Kit.LabelAt(fr, Loc.T("¡Tocá para mejorarlo!"), 32, Kit.OrangeD, 0, true, 0, 712, 680, 46, TextAnchor.MiddleCenter);
            var dots = Kit.LabelAt(fr, "", 30, Kit.Brown, 0, true, 0, 756, 680, 40, TextAnchor.MiddleCenter);
            System.Action paintDots = () => { dots.text = tries > 0 ? Loc.T("Intentos: ") + tries : ""; };
            paintDots();
            var lootBox = Kit.New("Botin", fr);
            Kit.Place(lootBox, 0.5f, 0f, -330f, 700f, 660, 190);
            var b = Kit.Button(fr, Loc.T("¡Genial!"), Kit.Green, 38, 360, 100);
            Kit.Place((RectTransform)b.transform, 0.5f, 0f, -180f, 890f, 360, 100);
            b.gameObject.SetActive(false);
            b.Clicked += CloseSheet;
            var tap = raw.gameObject.AddComponent<Btn>();
            tap.Juice = false;
            primaryBtn = tap;
            bool opened = false, busy = false;
            System.Action open = () =>
            {
                if (opened) return;
                opened = true;
                hint.gameObject.SetActive(false);
                dots.gameObject.SetActive(false);
                st.OpenChest();
            };
            tap.Clicked += () =>
            {
                if (opened || busy) return;
                Tw.Kill(raw, "auto");
                tries--;
                paintDots();
                if (Isl.TryUpgradeChest())
                {
                    int t = Isl.OpenTier;
                    busy = true;
                    st.UpgradeChest(t);
                    Sfx.Play("gleam", -4f, 0.9f + t * 0.12f);
                    Sfx.Play("up", -6f, 0.9f + t * 0.1f);
                    Flash(ChestCol[t], 0.25f);
                    Juice.Vibrate(40);
                    title.text = Island.ChestName[t];
                    title.color = ChestCol[t];
                    Tw.Pop(title.rectTransform, 1.3f);
                    hint.text = t >= 3 ? Loc.T("¡LEGENDARIO!") : Loc.T("¡Subió! ¿Otra vez?");
                    Tw.After(raw, "listo", 0.55f, () =>
                    {
                        busy = false;
                        if (tries <= 0 || t >= 3) open();
                        else Tw.After(raw, "auto", 6f, () => open());
                    });
                }
                else
                {
                    st.ShakeChest();
                    Sfx.Play("creak", -6f);
                    Tw.After(raw, "abre", 0.45f, () => open());
                }
            };
            Tw.After(raw, "auto", 6f, () => open());   // si no lo toca, se abre solo
            st.ChestOpened = () =>
            {
                var loot = Isl.FinishChest();
                if (loot == null) { CloseSheet(); return; }
                Flash(new Color(1f, 0.95f, 0.8f), 0.35f);
                Mineros.Fx.Haptics.Heavy();
                // cartas del botin: de menor a mayor, la ultima es la mejor
                var items = new List<KeyValuePair<string, string>>();
                items.Add(new KeyValuePair<string, string>("coin", "+" + BigNum.Fmt(loot.Coins)));
                if (loot.Turbo > 0) items.Add(new KeyValuePair<string, string>("speed", Loc.T("Turbo ") + Mathf.RoundToInt(loot.Turbo) + " s"));
                if (loot.Gems > 0) items.Add(new KeyValuePair<string, string>("gem", "+" + loot.Gems + Loc.T(" gemas")));
                int n = items.Count;
                for (int i = 0; i < n; i++)
                {
                    int idx = i;
                    bool best = i == n - 1;
                    var card = Kit.OutBox(lootBox, 18, 4, 6, best ? Kit.Cream : Color.white, best ? ChestCol[loot.Tier] : Kit.Out, "Carta");
                    float w = 200f;
                    Kit.Place(card, 0.5f, 0f, (i - (n - 1) * 0.5f) * 214f - w * 0.5f, 0f, w, 180);
                    var ic = Kit.Icon(card, items[i].Key, 72);
                    Kit.Place((RectTransform)ic.transform, 0.5f, 0f, -36f, 18f, 72, 72);
                    var l = Kit.LabelAt(card, items[i].Value, 28, Kit.Brown, 0, true, 0, 108, w, 50, TextAnchor.MiddleCenter);
                    l.resizeTextForBestFit = true; l.resizeTextMinSize = 18; l.resizeTextMaxSize = 28;
                    if (best)
                    {
                        var glow = Kit.Glow(card, new Color(1f, 0.85f, 0.4f, 0.6f), "Brillo");
                        glow.rectTransform.sizeDelta = new Vector2(320, 300);
                        glow.transform.SetAsFirstSibling();
                    }
                    card.localScale = Vector3.zero;
                    float d = 0.3f + i * 0.4f;
                    string snd = items[i].Key == "gem" ? "gem" : items[i].Key == "coin" ? "coins_pour" : "powerup";
                    Tw.Scale(card, Vector3.zero, Vector3.one * (best ? 1.08f : 1f), 0.4f, Ease.OutBack, d, () =>
                    {
                        Sfx.Play(snd, -5f);
                        Sfx.Play("card", -10f);
                        if (items[idx].Key == "gem") FlyGems(LayerPos(card), loot.Gems);
                        if (items[idx].Key == "coin") FlyCoins(LayerPos(card), 6);
                    });
                }
                Tw.After(b, "btn", 0.5f + n * 0.4f, () =>
                {
                    sheetLocked = false;
                    b.gameObject.SetActive(true);
                    Tw.Pop(b.transform, 1.3f);
                    if (Isl.ChestCount > 0) { b.Label.text = Loc.T("Abrir otro"); }
                });
                game.Save();
            };
            b.Clicked += () => { if (Isl.ChestCount > 0) Tw.After(this, "otro", 0.05f, OpenChestUi); };
        }

        // ------------------------------------------------------------ proxima etapa del edificio
        void NextStage(Transform parent, Plot p, BDef d)
        {
            int nextLevel = -1;
            for (int l = p.Level + 1; l <= d.MaxLevel; l++)
                if (IslandArt.ModelStage(d.Kind, Island.Tier(l)) != IslandArt.ModelStage(d.Kind, Island.Tier(p.Level))) { nextLevel = l; break; }
            if (nextLevel < 0) return;
            var sil = Kit.Img(parent, IslandStage.I.BuildingIcon(d.Kind, Island.Tier(nextLevel)), new Color(0.16f, 0.1f, 0.06f, 0.85f), "Silueta");
            sil.preserveAspect = true;
            Kit.PlaceTL(sil.rectTransform, 520, 20, 130, 130);
            var q = Kit.Label(sil.transform, "?", 60, Color.white, 6, true, TextAnchor.MiddleCenter);
            Kit.Stretch(q.rectTransform);
            var l2 = Kit.LabelAt(parent, Loc.T("Evoluciona en nivel ") + nextLevel, 20, Kit.OrangeD, 0, true, 470, 146, 200, 28, TextAnchor.MiddleCenter);
            l2.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }
}
