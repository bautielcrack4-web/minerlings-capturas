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
    /// Interfaz de la rutina diaria (biblia 3.6): boton "Diario" con aviso, hoja con la racha de 7 dias (el de hoy se
    /// ilumina, la moneda cae adentro con rebote y el fueguito crece), misiones del dia con barra y cobro, el tablon de
    /// pedidos (papeles con sello LISTO al cobrar) y la pantalla de regreso con el contador que sube.
    /// </summary>
    public sealed partial class IslandUi
    {
        Btn dailyBtn;
        Badge dailyBadge;
        static Sprite flame;

        static Sprite Flame()
        {
            if (flame != null) return flame;
            var p = new Painter(160, 200, 4f, 3);
            var outer = new List<Vector2>();
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f * Mathf.PI * 2f;
                float r = 15f * (1f - 0.35f * Mathf.Sin(t));
                float x = Mathf.Sin(t) * r * 0.85f, y = Mathf.Cos(t) * r;
                if (y < 0) { x *= 1f + y / 40f; y *= 1.6f; }   // punta arriba
                outer.Add(new Vector2(20f + x, 30f - y));
            }
            p.OPoly(outer.ToArray(), new Color(1f, 0.45f, 0.15f), 3f);
            p.Ellipse(new Vector2(20f, 34f), new Vector2(7f, 9f), new Color(1f, 0.85f, 0.3f));
            p.Ellipse(new Vector2(20f, 37f), new Vector2(3.5f, 4.5f), new Color(1f, 0.98f, 0.8f));
            flame = MakeSpr(p);
            return flame;
        }

        void BuildDailyButton()
        {
            dailyBtn = Kit.SqButton(hudLayer, Kit.Orange, 60, Loc.T("Diario"));
            Kit.Place((RectTransform)dailyBtn.transform, 1f, 0f, -84f, 242f, 60, 60);
            var ic = Kit.Img(dailyBtn.transform, Flame(), Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.PlaceTL(ic.rectTransform, 12, 6, 36, 46);
            dailyBadge = Kit.MakeBadge(dailyBtn.transform);
            Kit.Place(dailyBadge.Root, 1f, 0f, -18f, -8f, 28, 28);
            dailyBadge.AlwaysNumber = true;
            dailyBtn.Clicked += OpenDaily;
            dailyBtn.gameObject.SetActive(false);   // vive en el ☰ (Misiones); el contador va al punto del ☰
        }

        void UpdateDailyButton()
        {
            if (dailyBtn == null) BuildDailyButton();
            int n = (game.DailyPending ? 1 : 0);
            foreach (var m in Isl.Missions) if (!m.Claimed && Isl.MissionProgress(m) >= m.Target) n++;
            dailyBadge.SetCount(n);
            if (n > 0 && !dailyBtn.IsPressed && Time.unscaledTime > dailyBtn.QuietUntil)
            {
                float w = Mathf.Repeat(Time.time, 2.4f);
                dailyBtn.transform.localRotation = Quaternion.Euler(0, 0, w < 0.5f ? Mathf.Sin(w * 40f) * 7f * (1f - w * 2f) : 0f);
            }
        }

        // ------------------------------------------------------------ hoja del diario
        public void OpenDaily()
        {
            int today = IslandGame.Today;
            var fr = OpenSheet(1060, true);
            Kit.LabelAt(fr, Loc.T("Diario"), 44, Kit.Brown, 0, true, 0, 18, 680, 56, TextAnchor.MiddleCenter);
            // racha
            var fl = Kit.Img(fr, Flame(), Color.white, "Fuego");
            fl.preserveAspect = true;
            Kit.PlaceTL(fl.rectTransform, 210, 78, 64, 80);
            var sl = Kit.LabelAt(fr, Isl.Streak + (Isl.Streak == 1 ? Loc.T(" día") : Loc.T(" días")) + Loc.T(" seguidos"), 34, Kit.OrangeD, 0, true, 280, 96, 340, 50);
            Tw.To(fl, "fuego", 99f, Ease.Linear, u => fl.rectTransform.localScale = new Vector3(1f + Mathf.Sin(Time.time * 7f) * 0.04f, 1f + Mathf.Sin(Time.time * 9f) * 0.07f, 1f));
            int day = Isl.StreakDay;
            bool canClaim = game.DailyPending && Isl.ClaimedDay != today;
            var tiles = new List<RectTransform>();
            for (int d = 1; d <= 7; d++)
            {
                double coins; int gems, chest;
                Isl.DailyPrize(d, out coins, out gems, out chest);
                bool past = d < day || (d == day && !canClaim);
                bool now = d == day && canClaim;
                Color bg = now ? Kit.Yellow : past ? Icons.H("cde6b0") : Color.white;
                var tile = Kit.OutBox(fr, 14, 4, 5, bg, now ? Kit.OrangeD : Kit.Out, "Dia");
                float w = d == 7 ? 168f : 80f;
                float x = d < 7 ? 22 + (d - 1) * 86 : 22 + 6 * 86;
                Kit.PlaceTL(tile, x, 176, d == 7 ? 126 : 80, 132);
                tiles.Add(tile);
                Kit.LabelAt(tile, Loc.T("Día ") + d, 18, Kit.Brown, 0, true, 0, 4, d == 7 ? 126 : 80, 26, TextAnchor.MiddleCenter);
                string icon = chest >= 0 ? "chest" : gems >= 2 && d >= 4 ? "gem" : "coin";
                var ic = Kit.Icon(tile, icon, d == 7 ? 58 : 44);
                Kit.Place((RectTransform)ic.transform, 0.5f, 0f, d == 7 ? -29f : -22f, 34f, d == 7 ? 58 : 44, d == 7 ? 58 : 44);
                string amt = chest >= 0 ? (chest == 2 ? Loc.T("Cofre oro") : Loc.T("Cofre")) : icon == "gem" ? "+" + gems : BigNum.Fmt(coins);
                Kit.LabelAt(tile, amt, 16, Kit.Brown, 0, true, 0, 96, d == 7 ? 126 : 80, 26, TextAnchor.MiddleCenter);
                if (past)
                {
                    var ok = Kit.Icon(tile, "check", 34);
                    Kit.Place((RectTransform)ok.transform, 1f, 0f, -36f, 2f, 34, 34);
                }
                if (now) Tw.To(tile, "late", 99f, Ease.Linear, u => tile.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 5f) * 0.05f));
            }
            var claim = Kit.Button(fr, canClaim ? Loc.T("¡Cobrar día ") + day + "!" : Loc.T("Volvé mañana"), canClaim ? Kit.Green : Kit.Gray, 30, 360, 84);
            Kit.Place((RectTransform)claim.transform, 0.5f, 0f, -180f, 322f, 360, 84);
            claim.Interactable = canClaim;
            if (canClaim) primaryBtn = claim;
            claim.Clicked += () =>
            {
                if (!Isl.ClaimDaily(today)) return;
                var t = tiles[day - 1];
                Tw.Kill(t, "late");
                // la moneda del dia cae dentro con rebote; el fueguito crece y suma
                var coin = Kit.Img(flyLayer, Icons.Get(day == 7 ? "chest" : "coin"), Color.white, "Moneda");
                var cr = coin.rectTransform;
                cr.sizeDelta = new Vector2(64, 64);
                Vector2 to = LayerPos(t);
                Tw.To(cr, "cae", 0.7f, Ease.OutBounce, u => cr.anchoredPosition = Vector2.Lerp(to + new Vector2(0f, 300f), to, u), () =>
                {
                    Destroy(coin.gameObject);
                    Tw.Pop(t, 1.3f);
                    Sfx.Play(day == 7 ? "chest" : "coins_pour", -4f);
                    double c; int g, ch;
                    Isl.DailyPrize(day, out c, out g, out ch);
                    if (c > 0) FlyCoins(to, 6);
                    if (g > 0) FlyGems(to, g);
                    Tw.Pop(fl.rectTransform, 1.6f);
                    Tw.Pop(sl.rectTransform, 1.3f);
                    Juice.Vibrate(40);
                });
                claim.Label.text = Loc.T("¡Hasta mañana!");
                claim.Interactable = false;
                game.Save();
            };
            if (Isl.CanRepair(today))
            {
                var rep = Kit.Button(fr, Loc.T("Salvar racha de ") + Isl.BrokenStreak + Loc.T(" días (") + Island.RepairGems + Loc.T(" gemas)"), Kit.Purple, 22, 560, 60);
                Kit.Place((RectTransform)rep.transform, 0.5f, 0f, -280f, 412f, 560, 60);
                rep.Clicked += () =>
                {
                    if (!Isl.RepairStreak(today)) return;
                    Sfx.Play("powerup", -3f);
                    Flash(new Color(1f, 0.6f, 0.3f), 0.3f);
                    CloseSheet(); OpenDaily();
                };
            }
            // misiones del dia
            Kit.LabelAt(fr, Loc.T("Misiones de hoy"), 34, Kit.Brown, 0, true, 0, 486, 680, 46, TextAnchor.MiddleCenter);
            for (int i = 0; i < Isl.Missions.Count; i++) MissionRow(fr, Isl.Missions[i], 540 + i * 136);
            var bonus = Kit.LabelAt(fr, Isl.MissionBonus ? Loc.T("¡Cofre de plata cobrado!") : Loc.T("Completá las tres: cofre de plata"), 24, Kit.OrangeD, 0, true, 0, 950, 680, 40, TextAnchor.MiddleCenter);
            sheetRefresh.Add(() => bonus.text = Isl.MissionBonus ? Loc.T("¡Cofre de plata cobrado!") : Loc.T("Completá las tres: cofre de plata"));
        }

        void MissionRow(Transform parent, DayMission m, float y)
        {
            var card = Kit.Box9(parent, "card", new Vector4(12, 12, 12, 16), Color.white, "Mision");
            Kit.PlaceTL(card.rectTransform, 22, y, 636, 124);
            Kit.LabelAt(card.transform, m.Text, 26, Kit.Brown, 0, true, 20, 12, 400, 36);
            var bar = Kit.OutBox(card.transform, 10, 3, 0, Icons.H("d8c6a2"), Kit.Out, "Barra");
            Kit.PlaceTL(bar, 20, 62, 360, 30);
            var fill = Kit.RoundImg(bar, 8, Kit.Green, "Relleno");
            var fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = new Vector2(3, 3); fr.offsetMax = new Vector2(3, -3);
            var cnt = Kit.Label(bar, "", 18, Color.white, 4, true, TextAnchor.MiddleCenter);
            Kit.Stretch(cnt.rectTransform);
            var b = Kit.Button(card.transform, "", Kit.Green, 24, 210, 74);
            Kit.PlaceTL((RectTransform)b.transform, 410, 24, 210, 74);
            b.Clicked += () =>
            {
                if (!Isl.ClaimMission(m)) { Sfx.Play("error", -8f); return; }
                Sfx.Play("goal", -4f);
                // sello de cumplida con aplastamiento
                var st = Kit.Label(card.transform, Loc.T("¡LISTO!"), 44, Kit.Green, 7, true, TextAnchor.MiddleCenter, "Sello");
                st.rectTransform.sizeDelta = new Vector2(260, 70);
                st.rectTransform.anchoredPosition = new Vector2(80f, 0f);
                st.rectTransform.localRotation = Quaternion.Euler(0, 0, -12f);
                Tw.Scale(st.rectTransform, Vector3.one * 2.2f, Vector3.one, 0.25f, Ease.OutBack);
                FlyCoins(LayerPos(card.rectTransform), 5);
                FlyGems(LayerPos(card.rectTransform), m.Gems);
                Juice.Vibrate(30);
                game.Save();
            };
            sheetRefresh.Add(() =>
            {
                long p = Isl.MissionProgress(m);
                float f = Mathf.Clamp01(p / (float)Mathf.Max(1, m.Target));
                fill.gameObject.SetActive(f > 0.02f);
                fr.sizeDelta = new Vector2(Mathf.Max(20f, 354f * f), fr.sizeDelta.y);
                cnt.text = p + " / " + m.Target;
                bool ready = !m.Claimed && p >= m.Target;
                b.Label.text = m.Claimed ? Loc.T("Hecha") : ready ? Loc.T("Cobrar") : "+" + m.Gems + Loc.T(" gemas");
                b.Interactable = ready;
            });
        }

        // ------------------------------------------------------------ tablon de pedidos
        static readonly Dictionary<int, Sprite> oreIcons = new Dictionary<int, Sprite>();

        Sprite OreIcon(int kind)
        {
            Sprite s;
            if (oreIcons.TryGetValue(kind, out s)) return s;
            Material[] mats;
            var mesh = IslandArt.OreMesh(kind, 1, out mats);
            s = IslandStage.I.RenderIcon(mesh, mats, 192);
            oreIcons[kind] = s;
            return s;
        }

        /// <summary>
        /// Tablon de pedidos (0.9.2): ~18 % mas chico y con el fondo apenas oscurecido para seguir sintiendo la isla.
        /// Jerarquia de cada papel: que piden (icono + nombre) > cuanto falta (8/10) > premio > tiempo. Al completarse
        /// solo aparece un "LISTO" chico con tilde en la esquina y el boton de cobrar.
        /// </summary>
        public void OpenBoard()
        {
            var fr = OpenSheet(440, true, 560f, 0.38f);
            Kit.LabelAt(fr, Loc.T("Pedidos"), 34, Kit.Brown, 0, true, 0, 16, 560, 44, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, Loc.T("Llevá mineral al depósito: pagan 4 veces más"), 17, new Color(0.55f, 0.47f, 0.4f), 0, false, 0, 58, 560, 26, TextAnchor.MiddleCenter);
            for (int i = 0; i < 3 && i < Isl.Orders.Count; i++) OrderPaper(fr, i);
        }

        void OrderPaper(Transform parent, int i)
        {
            const float W = 168f, H = 320f;
            var paper = Kit.RoundImg(parent, 16, Icons.H("fff6e2"), "Papel").rectTransform;
            Kit.PlaceTL(paper, 18 + i * (W + 10f), 98, W, H);
            var icon = Kit.Img(paper, OreIcon(0), Color.white, "Mineral");
            icon.preserveAspect = true;
            Kit.PlaceTL(icon.rectTransform, (W - 92f) * 0.5f, 14, 92, 92);
            var what = Kit.LabelAt(paper, "", 24, Kit.Brown, 0, true, 0, 108, W, 32, TextAnchor.MiddleCenter);
            var bar = Kit.RoundImg(paper, 6, Icons.H("eadcc0"), "Barra").rectTransform;
            Kit.PlaceTL(bar, 18, 150, W - 36f, 12);
            var fill = Kit.RoundImg(bar, 6, Kit.Green, "Relleno");
            var fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
            var cnt = Kit.LabelAt(paper, "", 22, Kit.Brown, 0, true, 0, 166, W, 30, TextAnchor.MiddleCenter);
            var coinI = Kit.Img(paper, Icons.Get("coin"), Color.white, "Moneda");
            coinI.preserveAspect = true;
            var reward = Kit.LabelAt(paper, "", 22, Kit.OrangeD, 0, true, 0, 202, W, 30, TextAnchor.MiddleCenter);
            var timer = Kit.LabelAt(paper, "", 16, new Color(0.6f, 0.52f, 0.45f), 0, false, 0, 234, W, 24, TextAnchor.MiddleCenter);
            // "LISTO" chico con tilde en la esquina (antes: sello rojo gigante)
            var ok = Kit.RoundImg(paper, 12, Kit.Green, "Listo").rectTransform;
            Kit.Place(ok, 1f, 0f, -92f, 8f, 84, 26);
            var okI = Kit.Img(ok, Icons.Get("check"), Color.white, "Tilde");
            okI.preserveAspect = true;
            Kit.PlaceTL(okI.rectTransform, 6, 3, 20, 20);
            Kit.LabelAt(ok, Loc.T("LISTO"), 15, Color.white, 0, true, 26, 0, 54, 26, TextAnchor.MiddleCenter);
            ok.gameObject.SetActive(false);
            var b = Kit.Button(paper, Loc.T("Cobrar"), Kit.Green, 22, 136, 52);
            Kit.Place((RectTransform)b.transform, 0.5f, 1f, -68f, -62f, 136, 52);
            int shownKind = -1;
            Order shownOrder = null;
            bool wasDone = false;
            b.Clicked += () =>
            {
                var o = i < Isl.Orders.Count ? Isl.Orders[i] : null;
                if (o == null) return;
                int gems = o.Gems;
                if (!Isl.ClaimOrder(o)) { Sfx.Play("error", -8f); return; }
                // monedas al contador y el papel se va volando
                Tw.Pop(ok, 1.4f);
                Sfx.Play("thud", -8f, 1.4f);
                Juice.Vibrate(25);
                Sfx.PlayLater("coins_pour", 0.2f, -4f);
                Tw.After(paper, "vuela", 0.45f, () =>
                {
                    FlyCoins(LayerPos(paper), 6);
                    if (gems > 0) FlyGems(LayerPos(paper), gems);
                    Vector2 a0 = paper.anchoredPosition;
                    Tw.To(paper, "fuera", 0.3f, Ease.InBack, u => paper.anchoredPosition = a0 + new Vector2(0f, 500f * u), () => { paper.anchoredPosition = a0; shownOrder = null; });
                });
                game.Save();
            };
            sheetRefresh.Add(() =>
            {
                var o = i < Isl.Orders.Count ? Isl.Orders[i] : null;
                if (o == null) return;
                bool waiting = o.Wait > 0f;
                if (o != shownOrder && !waiting)
                {
                    // papel nuevo: entra desde arriba y se asienta
                    shownOrder = o;
                    Vector2 a0 = paper.anchoredPosition;
                    Tw.To(paper, "entra", 0.3f, Ease.OutBack, u => paper.anchoredPosition = a0 + new Vector2(0f, 400f * (1f - u)));
                }
                if (o.Kind != shownKind) { shownKind = o.Kind; icon.sprite = OreIcon(o.Kind); }
                icon.gameObject.SetActive(!waiting);
                bar.gameObject.SetActive(!waiting);
                cnt.gameObject.SetActive(!waiting);
                reward.gameObject.SetActive(!waiting);
                coinI.gameObject.SetActive(!waiting);
                what.text = waiting ? Loc.T("Nuevo pedido") : Island.Ores[o.Kind].Name;
                what.color = waiting ? new Color(0.6f, 0.52f, 0.45f) : Kit.Brown;
                float f = Mathf.Clamp01(o.Delivered / (float)Mathf.Max(1, o.Count));
                fill.gameObject.SetActive(f > 0.02f);
                fr.sizeDelta = new Vector2((W - 36f) * f, 0f);
                cnt.text = o.Delivered + "/" + o.Count;
                string rw = BigNum.Fmt(o.Coins) + (o.Gems > 0 ? "  +" + o.Gems + Loc.T(" gema") : "");
                if (reward.text != rw)
                {
                    reward.text = rw;
                    float rwW = reward.preferredWidth;
                    Kit.PlaceTL(coinI.rectTransform, (W - rwW) * 0.5f - 30f, 205, 24, 24);
                }
                int s = Mathf.CeilToInt(waiting ? o.Wait : o.Left);
                timer.text = waiting ? Loc.T("en ") + s + " s" : o.Done ? "" : s / 60 + ":" + (s % 60).ToString("00");
                bool done = o.Done && !waiting;
                if (done != wasDone) { wasDone = done; ok.gameObject.SetActive(done); if (done) Tw.Pop(ok, 1.3f); }
                b.gameObject.SetActive(done);
            });
        }

        // ------------------------------------------------------------ marca sobre el tablon
        RectTransform boardTag;

        void UpdateBoardTag()
        {
            // un indicador chico (punto amarillo con tilde) en vez del cartel "¡Pedido listo!"
            bool show = game.BoardHasReady && sheet == null;
            if (boardTag == null)
            {
                if (!show) return;
                boardTag = Dot(worldLayer, Icons.Get("check"), Kit.Yellow, 40, "PedidoListo");
            }
            if (boardTag.gameObject.activeSelf != show) { boardTag.gameObject.SetActive(show); if (show) Tw.Pop(boardTag, 1.4f); }
            if (!show) return;
            Vector2 c = ToCanvas(game.BoardWorld + Vector3.up * 2.1f);
            boardTag.anchoredPosition = new Vector2(c.x, -c.y + Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 5f);
        }
    }
}
