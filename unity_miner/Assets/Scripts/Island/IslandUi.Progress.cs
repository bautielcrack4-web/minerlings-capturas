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
    /// Interfaz del progreso grande (biblia 3.8): hoja de la Maravilla, modo decorar (lugares marcados, selector con
    /// precio y belleza), barra de vida del Golem con segmentos, pase de temporada (gratis y dorado), museo de piezas y
    /// la expedicion a una isla nueva (mapa del archipielago con el barco que navega hasta la isla siguiente).
    /// </summary>
    public sealed partial class IslandUi
    {
        Btn passBtn, museumBtn, decorBtn, sailBtn;
        Text passLv;
        Image passRing;
        RectTransform wonderTag, beautyChip;
        Text wonderTagL, beautyL;
        readonly List<RectTransform> slotMarks = new List<RectTransform>();
        static Sprite wonderIcon;

        public Sprite WonderIcon()
        {
            if (wonderIcon != null) return wonderIcon;
            Material tm;
            var mesh = IslandArt.TripoModel("wonder", out tm);
            wonderIcon = mesh != null ? IslandStage.I.RenderIcon(mesh, new[] { tm }, 256) : Icons.Get("trophy");
            return wonderIcon;
        }

        void BuildProgressButtons()
        {
            passBtn = Kit.SqButton(hudLayer, Kit.Yellow, 60, Loc.T("Pase"));
            Kit.Place((RectTransform)passBtn.transform, 1f, 0f, -84f, 312f, 60, 60);
            var pi = Kit.Img(passBtn.transform, Icons.Get("star"), Color.white, "Icono");
            Kit.PlaceTL(pi.rectTransform, 10, 8, 40, 40);
            passLv = Kit.Label(passBtn.transform, "", 20, Color.white, 5, true, TextAnchor.MiddleCenter, Loc.T("Nivel"));
            Kit.Place(passLv.rectTransform, 0.5f, 0f, -30f, 40f, 60, 26);
            passBtn.Clicked += OpenPass;
            museumBtn = Kit.SqButton(hudLayer, Kit.Purple, 60, Loc.T("Museo"));
            Kit.Place((RectTransform)museumBtn.transform, 1f, 0f, -84f, 382f, 60, 60);
            var mi = Kit.KIcon(museumBtn.transform, "trophy", 36);
            Kit.PlaceTL(mi.rectTransform, 12, 9, 36, 36);
            museumBtn.Clicked += OpenMuseum;
            decorBtn = Kit.SqButton(hudLayer, Icons.H("e86fb0"), 60, Loc.T("Decorar"));
            Kit.Place((RectTransform)decorBtn.transform, 1f, 0f, -84f, 452f, 60, 60);
            var di = Kit.Img(decorBtn.transform, Heart(), Color.white, "Icono");
            di.preserveAspect = true;
            Kit.PlaceTL(di.rectTransform, 12, 12, 36, 34);
            decorBtn.Clicked += ToggleDecor;
            sailBtn = Kit.Button(hudLayer, Loc.T("¡Zarpar a otra isla!"), Kit.Blue, 26, 340, 84, Loc.T("Zarpar"));
            Kit.Place((RectTransform)sailBtn.transform, 0.5f, 1f, -170f, -210f, 340, 84);
            sailBtn.Clicked += OpenSail;
            sailBtn.gameObject.SetActive(false);
            // viven en el ☰ (Nivel, Logros, Decorar)
            passBtn.gameObject.SetActive(false);
            museumBtn.gameObject.SetActive(false);
            decorBtn.gameObject.SetActive(false);
        }

        void UpdateProgressUi()
        {
            if (passBtn == null) BuildProgressButtons();
            passLv.text = Loc.T("Nv ") + Isl.SeasonLevel;
            bool sail = Isl.CanSail;
            if (sailBtn.gameObject.activeSelf != sail) { sailBtn.gameObject.SetActive(sail); if (sail) Tw.Pop(sailBtn.transform, 1.3f); }
            if (sail && !sailBtn.IsPressed) sailBtn.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 4f) * 0.04f);
            UpdateWonderTag();
            UpdateDecorMarks();
            UpdateBossBar();
        }

        public void PassPop() { if (menuBtn != null && menuBtn.gameObject.activeInHierarchy) { Tw.Pop(menuBtn.transform, 1.4f); Sparkle(LayerPos((RectTransform)menuBtn.transform), new Color(1f, 0.9f, 0.5f)); } }

        // ------------------------------------------------------------ maravilla
        void UpdateWonderTag()
        {
            // sin cartel permanente: un punto con estrella aparece solo cuando se puede construir la fase siguiente
            bool show = sheet == null && !game.DecorMode && game.WonderShown && Isl.CanBuildWonder();
            if (wonderTag == null)
            {
                if (!show) return;
                wonderTag = Dot(worldLayer, Icons.Get("star"), Kit.Yellow, 40, "Maravilla");
            }
            if (wonderTag.gameObject.activeSelf != show) { wonderTag.gameObject.SetActive(show); if (show) Tw.Pop(wonderTag, 1.4f); }
            if (!show) return;
            int ph = Isl.WonderPhase;
            Vector2 c = ToCanvas(game.WonderWorld + Vector3.up * (ph == 0 ? 2.2f : game.WonderHeight + 0.8f));
            wonderTag.anchoredPosition = new Vector2(c.x, -c.y + Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 5f);
        }

        public void OpenWonder()
        {
            var fr = OpenSheet(600, true);
            int ph = Isl.WonderPhase;
            Kit.LabelAt(fr, ph == 0 ? Loc.T("Ruinas misteriosas") : Loc.T("Gran Estatua de Oro"), 40, Kit.Brown, 0, true, 0, 20, 680, 54, TextAnchor.MiddleCenter);
            var ic = Kit.Img(fr, WonderIcon(), ph >= 1 ? Color.white : new Color(0.16f, 0.1f, 0.06f, 0.85f), "Estatua");
            ic.preserveAspect = true;
            Kit.Place(ic.rectTransform, 0.5f, 0f, -90f, 80f, 180, 200);
            // fases como puntos
            for (int i = 0; i < Island.WonderPhases; i++)
            {
                var dot = Kit.RoundImg(fr, 14, i < ph ? Kit.Yellow : Icons.H("d8c6a2"), "Fase");
                Kit.Place(dot.rectTransform, 0.5f, 0f, -110f + i * 46f, 292f, 34, 34);
            }
            bool done = ph >= Island.WonderPhases;
            string desc = done ? Loc.T("¡Terminada! +50 % a todo lo que se vende. Ya podés zarpar a una isla nueva.")
                : (ph == 0 ? Loc.T("Algo enorme está tapado por las enredaderas… ") : "") + Loc.T("Fase ") + (ph + 1) + ": " + Island.WonderPhaseName[ph] + Loc.T(". Cada fase: +8 % a todo.");
            var dl = Kit.LabelAt(fr, desc, 24, Kit.Brown, 0, false, 30, 340, 620, 90, TextAnchor.UpperCenter);
            Kit.Wrap(dl);
            var b = Kit.Button(fr, "", Kit.Green, 30, 400, 96);
            Kit.Place((RectTransform)b.transform, 0.5f, 0f, -200f, 470f, 400, 96);
            if (!done) primaryBtn = b;
            AddShine(b, () => done || Isl.CanBuildWonder());
            b.Clicked += () =>
            {
                if (done) { CloseSheet(); if (Isl.CanSail) OpenSail(); return; }
                if (Isl.BuildWonder()) { CloseSheet(); game.Save(); Sfx.Play("build", -3f); }
                else NoMoney(b);
            };
            sheetRefresh.Add(() =>
            {
                b.Label.text = done ? Loc.T("¡Zarpar!") : Loc.T("Construir  ") + BigNum.Fmt(Isl.WonderCost());
                Color m = done || Isl.CanBuildWonder() ? Color.white : new Color(0.72f, 0.72f, 0.75f);
                if (b.Modulate != m) { b.Modulate = m; b.Restyle(); }
            });
        }

        // ------------------------------------------------------------ decorar
        void ToggleDecor()
        {
            game.DecorMode = !game.DecorMode;
            Sfx.Play(game.DecorMode ? "open" : "close", -6f);
            if (game.DecorMode) Toast(Loc.T("Tocá un lugar marcado para decorar"), Icons.H("e86fb0"));
        }

        void UpdateDecorMarks()
        {
            bool on = game.DecorMode && sheet == null;
            int need = 0;
            if (on)
                for (int i = 0; i < Isl.DecorSlots.Count; i++)
                {
                    if (Isl.Decor.ContainsKey(i)) continue;
                    RectTransform m;
                    if (need < slotMarks.Count) m = slotMarks[need];
                    else
                    {
                        m = Kit.New("Lugar", worldLayer);
                        m.sizeDelta = new Vector2(56, 56);
                        var ring = Kit.Img(m, Icons.Ring(0.14f), new Color(1f, 0.6f, 0.85f), "Anillo");
                        Kit.Stretch(ring.rectTransform);
                        var plus = Kit.Label(m, "+", 34, Color.white, 5, true, TextAnchor.MiddleCenter);
                        Kit.Stretch(plus.rectTransform);
                        slotMarks.Add(m);
                    }
                    m.gameObject.SetActive(true);
                    Vector2 c = ToCanvas(game.SlotWorld(i) + Vector3.up * 0.3f);
                    m.anchoredPosition = new Vector2(c.x, -c.y);
                    m.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 4f + i) * 0.08f);
                    need++;
                }
            for (int i = need; i < slotMarks.Count; i++) if (slotMarks[i].gameObject.activeSelf) slotMarks[i].gameObject.SetActive(false);
            // puntaje de belleza
            bool chip = game.DecorMode;
            if (beautyChip == null)
            {
                if (!chip) return;
                beautyChip = Kit.MakeTag(hudLayer, "", Icons.H("e86fb0"), 24);
                beautyL = beautyChip.GetComponentInChildren<Text>();
            }
            if (beautyChip.gameObject.activeSelf != chip) beautyChip.gameObject.SetActive(chip);
            if (!chip) return;
            beautyChip.anchorMin = beautyChip.anchorMax = new Vector2(0.5f, 1f);
            beautyChip.anchoredPosition = new Vector2(0f, -250f);
            if (beautyL != null) beautyL.text = Loc.T("Belleza ") + Isl.Beauty + "  (+" + Mathf.RoundToInt((float)(Isl.BeautyMult() - 1.0) * 100f) + " %)";
        }

        static readonly Dictionary<int, Sprite> decorIcons = new Dictionary<int, Sprite>();

        public void OpenDecorPicker(int slot)
        {
            int n = Island.DecorDefs.Length;
            var fr = OpenSheet(150 + n * 112, false);
            Kit.LabelAt(fr, Loc.T("Decorar"), 40, Kit.Brown, 0, true, 0, 16, 680, 52, TextAnchor.MiddleCenter);
            for (int i = 0; i < n; i++)
            {
                var d = Island.DecorDefs[i];
                int item = i;
                var card = Kit.Box9(fr, "card", new Vector4(12, 12, 12, 16), Color.white, "Deco");
                Kit.PlaceTL(card.rectTransform, 22, 76 + i * 112, 636, 104);
                Kit.LabelAt(card.transform, d.Name, 28, Kit.Brown, 0, true, 24, 12, 360, 40);
                Kit.LabelAt(card.transform, Loc.T("Belleza +") + d.Beauty, 22, Icons.H("d0559a"), 0, true, 24, 54, 300, 34);
                if (item == Isl.DecorDeal)
                {
                    // oferta del dia: cinta roja que late (rota cada 24 h)
                    var tag = Kit.RoundImg(card.transform, 10, Icons.H("e5484d"), "Oferta");
                    Kit.PlaceTL(tag.rectTransform, 250, 14, 150, 36);
                    Kit.LabelAt(tag.transform, Loc.T("Hoy -40 %"), 22, Color.white, 2, true, 0, 0, 150, 36, TextAnchor.MiddleCenter);
                    var tr = tag.rectTransform;
                    Tw.To(tr, "late", 600f, Ease.Linear, u => tr.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(u * 600f * 4f)));
                }
                var b = Kit.Button(card.transform, "", Kit.Green, 26, 200, 76);
                Kit.PlaceTL((RectTransform)b.transform, 420, 14, 200, 76);
                b.Clicked += () =>
                {
                    if (Isl.PlaceDecor(slot, item)) { CloseSheet(); game.Save(); }
                    else NoMoney(b);
                };
                AddShine(b, () => Isl.Coins >= Isl.DecorCost(item));
                sheetRefresh.Add(() =>
                {
                    b.Label.text = BigNum.Fmt(Isl.DecorCost(item));
                    Color m = Isl.Coins >= Isl.DecorCost(item) ? Color.white : new Color(0.72f, 0.72f, 0.75f);
                    if (b.Modulate != m) { b.Modulate = m; b.Restyle(); }
                });
            }
        }

        // ------------------------------------------------------------ barra del golem
        RectTransform bossBar;
        Image bossFill;
        Text bossName;
        Ore bossOre;

        public void ShowBossBar(Ore o) { bossOre = o; }

        void UpdateBossBar()
        {
            bool show = bossOre != null && !bossOre.Dead && Isl.Boss == bossOre;
            if (bossBar == null)
            {
                if (!show) return;
                bossBar = Kit.OutBox(hudLayer, 14, 4, 0, Icons.H("3b2a2a"), Kit.Out, "Jefe");
                Kit.Place(bossBar, 0.5f, 0f, -260f, 196f, 520, 44);
                bossFill = Kit.RoundImg(bossBar, 10, Icons.H("e5484d"), "Vida");
                var r = bossFill.rectTransform;
                r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
                r.offsetMin = new Vector2(4, 4); r.offsetMax = new Vector2(4, -4);
                for (int i = 1; i < 3; i++)
                {
                    var seg = Kit.Tint(bossBar, new Color(0.15f, 0.1f, 0.1f, 0.9f), "Segmento");
                    Kit.Place(seg.rectTransform, 0f, 0f, 4f + 512f * i / 3f, 4f, 3, 36);
                }
                bossName = Kit.Label(bossBar, "", 22, Color.white, 5, true, TextAnchor.MiddleCenter);
                Kit.Stretch(bossName.rectTransform);
            }
            if (bossBar.gameObject.activeSelf != show) { bossBar.gameObject.SetActive(show); if (show) Tw.Pop(bossBar, 1.3f); }
            if (!show) { if (bossOre != null && bossOre.Dead) bossOre = null; return; }
            float f = Mathf.Clamp01((float)(bossOre.Hp / bossOre.MaxHp));
            bossFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(20f, 512f * f), bossFill.rectTransform.sizeDelta.y);
            bossFill.color = bossOre.BossPhase == 1 ? Color.Lerp(Icons.H("e5484d"), Color.white, Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.3f) : Icons.H("e5484d");
            bossName.text = Loc.T("Golem de Roca  nv ") + (Isl.BossLevel + 1);
        }

        // ------------------------------------------------------------ pase de temporada
        int passPage;

        public void OpenPass()
        {
            int today = IslandGame.Today;
            Isl.CheckSeason(today);
            var fr = OpenSheet(1100, true);
            int season = Isl.SeasonNo(today);
            Kit.LabelAt(fr, Loc.T("Pase: ") + Island.SeasonThemes[season % Island.SeasonThemes.Length], 38, Kit.Brown, 0, true, 0, 16, 680, 50, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, Loc.T("Termina en ") + Isl.SeasonDaysLeft(today) + Loc.T(" días"), 22, Kit.OrangeD, 0, true, 0, 64, 680, 30, TextAnchor.MiddleCenter);
            // barra de experiencia con chispa en la punta
            var bar = Kit.OutBox(fr, 12, 3, 0, Icons.H("d8c6a2"), Kit.Out, "Barra");
            Kit.PlaceTL(bar, 60, 104, 560, 36);
            var fill = Kit.RoundImg(bar, 10, Kit.Yellow, "Relleno");
            var r = fill.rectTransform;
            r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
            r.offsetMin = new Vector2(3, 3); r.offsetMax = new Vector2(3, -3);
            var lv = Kit.Label(bar, "", 20, Color.white, 4, true, TextAnchor.MiddleCenter);
            Kit.Stretch(lv.rectTransform);
            var spark = Kit.Img(bar, Icons.Get("star"), Color.white, "Chispa");
            spark.rectTransform.sizeDelta = new Vector2(30, 30);
            sheetRefresh.Add(() =>
            {
                bool max = Isl.SeasonLevel >= Island.SeasonLevels;
                float f = max ? 1f : (Isl.SeasonXp % Island.XpPerLevel) / (float)Island.XpPerLevel;
                r.sizeDelta = new Vector2(Mathf.Max(20f, 554f * f), r.sizeDelta.y);
                lv.text = Loc.T("Nivel ") + Isl.SeasonLevel + (max ? Loc.T(" (máximo)") : "  ·  " + (Isl.SeasonXp % Island.XpPerLevel) + "/" + Island.XpPerLevel);
                spark.rectTransform.anchoredPosition = new Vector2(-280f + 554f * f + 3f, 0f);
                spark.rectTransform.localRotation = Quaternion.Euler(0, 0, Time.time * 90f);
                spark.rectTransform.localScale = Vector3.one * (0.8f + Mathf.Sin(Time.time * 8f) * 0.2f);
            });
            Kit.LabelAt(fr, Loc.T("Gratis"), 24, Kit.GreenD, 0, true, 20, 154, 120, 30);
            Kit.LabelAt(fr, Loc.T("Dorado"), 24, Kit.OrangeD, 0, true, 20, 380, 120, 30);
            var page = Kit.New("Pagina", fr);
            Kit.PlaceTL(page, 0, 150, 680, 520);
            System.Action build = null;
            build = () =>
            {
                for (int c = page.childCount - 1; c >= 0; c--) Destroy(page.GetChild(c).gameObject);
                for (int k = 0; k < 5; k++)
                {
                    int lvN = passPage * 5 + k + 1;
                    float x = 130 + k * 108;
                    Kit.LabelAt(page, lvN.ToString(), 26, Kit.Brown, 0, true, x, 186, 96, 36, TextAnchor.MiddleCenter);
                    PassCell(page, lvN, false, x, 36);
                    PassCell(page, lvN, true, x, 236);
                }
            };
            build();
            var prev = Kit.Button(fr, "<", Kit.Blue, 34, 90, 70);
            Kit.Place((RectTransform)prev.transform, 0.5f, 0f, -320f, 690f, 90, 70);
            var next = Kit.Button(fr, ">", Kit.Blue, 34, 90, 70);
            Kit.Place((RectTransform)next.transform, 0.5f, 0f, 230f, 690f, 90, 70);
            var pl = Kit.LabelAt(fr, "", 24, Kit.Brown, 0, true, 0, 708, 680, 34, TextAnchor.MiddleCenter);
            sheetRefresh.Add(() => pl.text = Loc.T("Niveles ") + (passPage * 5 + 1) + Loc.T(" a ") + (passPage * 5 + 5));
            prev.Clicked += () => { passPage = (passPage + 5) % 6; build(); Sfx.Play("paper", -12f); };
            next.Clicked += () => { passPage = (passPage + 1) % 6; build(); Sfx.Play("paper", -12f); };
            var gold = Kit.Button(fr, "", Kit.Orange, 26, 520, 90);
            Kit.Place((RectTransform)gold.transform, 0.5f, 0f, -260f, 790f, 520, 90);
            gold.Clicked += () =>
            {
                if (Isl.BuyPassGold()) { Sfx.Play("fanfare", -4f); Flash(new Color(1f, 0.85f, 0.4f), 0.35f); build(); game.Save(); }
                else Sfx.Play("error", -6f);
            };
            sheetRefresh.Add(() =>
            {
                gold.gameObject.SetActive(!Isl.PassGold);
                gold.Label.text = Loc.T("Pase dorado: ") + Island.PassGoldGems + Loc.T(" gemas");
                gold.Interactable = Isl.Gems >= Island.PassGoldGems;
            });
            var how = Kit.LabelAt(fr, Loc.T("Ganás experiencia con misiones, pedidos, cofres, vetas gigantes, jefes y bichos"), 20, Kit.Brown, 0, false, 30, 900, 620, 60, TextAnchor.MiddleCenter);
            Kit.Wrap(how);
            // si hay premios para cobrar, abrir en esa pagina
        }

        void PassCell(Transform page, int lv, bool gold, float x, float y)
        {
            string kind; double amount;
            Isl.PassPrize(lv, gold, out kind, out amount);
            bool claimed = Isl.PassClaimed(lv, gold);
            bool reached = lv <= Isl.SeasonLevel;
            bool locked = gold && !Isl.PassGold;
            var cell = Kit.OutBox(page, 14, 4, 5, claimed ? Icons.H("cde6b0") : gold ? Icons.H("ffe9a8") : Color.white, reached && !claimed && !locked ? Kit.OrangeD : Kit.Out, "Premio");
            Kit.PlaceTL(cell, x, y, 96, 140);
            string icon = kind == "coins" ? "coin" : kind == "gems" ? "gem" : "chest";
            var ic = Kit.Icon(cell, icon, 50);
            Kit.Place((RectTransform)ic.transform, 0.5f, 0f, -25f, 18f, 50, 50);
            string amt = kind == "coins" ? BigNum.Fmt(amount) : kind == "gems" ? "+" + amount : Island.ChestName[(int)amount].Replace(Loc.T("Cofre "), "");
            var al = Kit.LabelAt(cell, amt, 18, Kit.Brown, 0, true, 0, 76, 96, 26, TextAnchor.MiddleCenter);
            al.resizeTextForBestFit = true; al.resizeTextMinSize = 12; al.resizeTextMaxSize = 18;
            if (claimed) { var ok = Kit.Icon(cell, "check", 34); Kit.Place((RectTransform)ok.transform, 0.5f, 0f, -17f, 100f, 34, 34); }
            else if (locked) { var lk = Kit.Icon(cell, "lock", 32); Kit.Place((RectTransform)lk.transform, 0.5f, 0f, -16f, 100f, 32, 32); }
            else if (reached)
            {
                var b = cell.gameObject.AddComponent<Btn>();
                var cl = Kit.LabelAt(cell, Loc.T("¡Cobrar!"), 18, Kit.GreenD, 0, true, 0, 104, 96, 28, TextAnchor.MiddleCenter);
                Tw.To(cell, "late", 99f, Ease.Linear, u => cell.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 5f + lv) * 0.04f));
                b.Clicked += () =>
                {
                    if (!Isl.ClaimPass(lv, gold)) return;
                    Sfx.Play(kind == "chest" ? "chest" : kind == "gems" ? "gem" : "coins_pour", -4f);
                    Vector2 p = LayerPos(cell);
                    if (kind == "coins") FlyCoins(p, 6);
                    else if (kind == "gems") FlyGems(p, (int)amount);
                    else Toast(Loc.T("¡") + Island.ChestName[(int)amount] + "!", Kit.Orange);
                    Tw.Kill(cell, "late");
                    cell.localScale = Vector3.one;
                    Tw.Pop(cell, 1.25f);
                    cl.text = Loc.T("Listo");
                    b.enabled = false;
                    game.Save();
                };
            }
        }

        // ------------------------------------------------------------ museo
        public void OpenMuseum()
        {
            var fr = OpenSheet(900, true);
            Kit.LabelAt(fr, Loc.T("Museo de la isla"), 42, Kit.Brown, 0, true, 0, 18, 680, 54, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, Loc.T("Las piezas salen al romper vetas y al cavar tesoros"), 22, Kit.Brown, 0, false, 0, 70, 680, 30, TextAnchor.MiddleCenter);
            Color[] setCol = { Icons.H("c9a26b"), Icons.H("4a8fb0"), Icons.H("e0b23a") };
            for (int s = 0; s < 3; s++)
            {
                bool done = Isl.SetDone(s);
                var row = Kit.OutBox(fr, 18, 4, 6, done ? Icons.H("fff3c4") : Kit.Cream, done ? Kit.OrangeD : Kit.Out, "Set");
                Kit.PlaceTL(row, 22, 116 + s * 252, 636, 238);
                Kit.LabelAt(row, Island.SetName[s] + (done ? Loc.T("  ¡completo!") : ""), 30, Kit.Brown, 0, true, 20, 10, 600, 40);
                for (int p = 0; p < 4; p++)
                {
                    bool has = Isl.HasPiece(s, p);
                    var cell = Kit.OutBox(row, 14, 3, 4, has ? setCol[s] : Icons.H("d8d0c0"), Kit.Out, "Pieza");
                    Kit.PlaceTL(cell, 20 + p * 152, 58, 140, 120);
                    var q = Kit.Label(cell, has ? Island.PieceName[s, p] : "?", has ? 22 : 54, has ? Color.white : Icons.H("a99f8c"), has ? 5 : 0, true, TextAnchor.MiddleCenter);
                    Kit.Stretch(q.rectTransform, 6, 6, 6, 6);
                    Kit.Wrap(q);
                    cell.localScale = Vector3.zero;
                    Tw.Scale(cell, Vector3.zero, Vector3.one, 0.3f, Ease.OutBack, 0.05f * (s * 4 + p));
                }
                Kit.LabelAt(row, Loc.T("Bonus: ") + Island.SetBonus[s], 22, done ? Kit.GreenD : Kit.OrangeD, 0, true, 20, 190, 600, 34);
            }
        }

        // ------------------------------------------------------------ expedicion
        public void OpenSail()
        {
            var fr = OpenSheet(820, true);
            int next = (Isl.IslandNo + 1) % Island.IslandNames.Length;
            Kit.LabelAt(fr, Loc.T("¡Zarpar a ") + Island.IslandNames[next] + "!", 40, Kit.Brown, 0, true, 0, 18, 680, 54, TextAnchor.MiddleCenter);
            var map = Kit.Img(fr, ArchipelagoSprite(), Color.white, "Mapa");
            Kit.Place(map.rectTransform, 0.5f, 0f, -270f, 80f, 540, 300);
            int relics = Isl.RelicsFor();
            var gain = Kit.LabelAt(fr, "+" + relics + Loc.T(" reliquias  (cada una: +10 % a todo, para siempre)"), 26, Kit.OrangeD, 0, true, 20, 396, 640, 40, TextAnchor.MiddleCenter);
            Kit.Wrap(gain);
            var keep = Kit.LabelAt(fr, Loc.T("Te llevás: gemas, mineros, álbum, museo, pase y racha.\nEmpezás de cero: edificios, monedas y la Maravilla.\nLa isla nueva vale el triple."), 22, Kit.Brown, 0, false, 30, 450, 620, 120, TextAnchor.MiddleCenter);
            Kit.Wrap(keep);
            var b = Kit.Button(fr, Loc.T("¡Zarpar!"), Kit.Blue, 36, 360, 100);
            Kit.Place((RectTransform)b.transform, 0.5f, 0f, -180f, 600f, 360, 100);
            primaryBtn = b;
            b.Clicked += () =>
            {
                sheetLocked = true;
                b.Interactable = false;
                Voyage(map.rectTransform, next);
            };
        }

        static readonly Vector2[] IslePos = { new Vector2(-170, -40), new Vector2(-60, 70), new Vector2(60, -60), new Vector2(150, 50), new Vector2(200, -80) };

        /// <summary>El barco navega por el mapa hasta la isla siguiente; despues la isla nueva se arma desde cero.</summary>
        void Voyage(RectTransform map, int next)
        {
            int from = Isl.IslandNo % Island.IslandNames.Length;
            Sfx.Play("horn", -3f);
            Sfx.Duck(8f, 5f);
            var boatImg = Kit.Img(map, BoatSprite(), Color.white, "Barco");
            boatImg.preserveAspect = true;
            boatImg.rectTransform.sizeDelta = new Vector2(56, 56);
            Vector2 a = IslePos[from], c = IslePos[next];
            Vector2 mid = (a + c) * 0.5f + new Vector2(0f, 60f);
            int n = 12;
            for (int i = 0; i <= n; i++)
            {
                float k = i / (float)n;
                var dot = Kit.RoundImg(map, 5, new Color(0.55f, 0.24f, 0.14f), "Ruta");
                dot.rectTransform.sizeDelta = new Vector2(10, 10);
                dot.rectTransform.anchoredPosition = Vector2.Lerp(Vector2.Lerp(a, mid, k), Vector2.Lerp(mid, c, k), k);
                dot.transform.localScale = Vector3.zero;
                Tw.Scale(dot.transform, Vector3.zero, Vector3.one, 0.15f, Ease.OutBack, i * 0.06f);
            }
            boatImg.transform.SetAsLastSibling();
            Tw.To(boatImg, "navega", 3f, Ease.InOutSine, u =>
            {
                boatImg.rectTransform.anchoredPosition = Vector2.Lerp(Vector2.Lerp(a, mid, u), Vector2.Lerp(mid, c, u), u) + new Vector2(0f, Mathf.Sin(u * 30f) * 3f);
                boatImg.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 20f) * 8f);
            }, () =>
            {
                Sfx.Play("fanfare", -2f);
                Flash(Color.white, 1.2f);
                sheetLocked = false;
                game.SailAway();
            }, 0.8f);
        }

        static Sprite archSpr, boatSpr;

        /// <summary>Barquito con vela (a 4x, con contorno).</summary>
        static Sprite BoatSprite()
        {
            if (boatSpr != null) return boatSpr;
            var p = new Painter(200, 200, 4f, 3);
            p.Line(new Vector2(25, 8), new Vector2(25, 36), 2.5f, Painter.Out);
            p.OPoly(new[] { new Vector2(26, 9), new Vector2(42, 32), new Vector2(26, 32) }, Color.white, 2.5f);
            p.OPoly(new[] { new Vector2(24, 13), new Vector2(24, 32), new Vector2(12, 32) }, Icons.H("ffd84a"), 2.5f);
            p.OPoly(new[] { new Vector2(6, 35), new Vector2(44, 35), new Vector2(38, 44), new Vector2(12, 44) }, Icons.H("a8703f"), 2.5f);
            boatSpr = MakeSpr(p);
            return boatSpr;
        }

        static Sprite ArchipelagoSprite()
        {
            if (archSpr != null) return archSpr;
            var p = new Painter(1080, 600, 4f, 2);
            p.ORR(new Rect(2, 2, 266, 146), 12f, Icons.H("b9d8e8"), 3f);
            string[][] cols = { new[] { "f3d99a", "8cc247" }, new[] { "f6e3b0", "d9c27a" }, new[] { "4d4542", "7d7a6a" }, new[] { "ece6ff", "9fe3d6" }, new[] { "e3eaf0", "eef4fa" } };
            for (int i = 0; i < IslePos.Length; i++)
            {
                Vector2 c = new Vector2(135f + IslePos[i].x / 4f * 1f, 75f - IslePos[i].y / 4f);
                p.OC(c, 16f, Icons.H(cols[i][0]), 2.5f);
                p.Circle(c, 11f, Icons.H(cols[i][1]));
            }
            var t = p.ToTexture();
            archSpr = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 400f);
            return archSpr;
        }
    }
}
