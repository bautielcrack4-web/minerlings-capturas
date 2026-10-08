using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using Mineros.World;
using UnityEngine;
using UnityEngine.UI;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Interfaz del Modo Cuartel (0.11): bandeja de modulos abajo (se toca una tarjeta y el modulo aparece flotando
    /// para arrastrarlo), pestañas de piso, nivel y espacio del Cuartel arriba, contratos y libro de combinaciones a la
    /// derecha, anillo de acciones alrededor del modulo elegido y globos de eventos. Casi sin texto: iconos y numeros.
    /// </summary>
    public sealed partial class IslandUi
    {
        RectTransform cxHud, cxTray, cxTrayContent, cxTop, cxRight, cxFloors, cxRing, cxPill, cxEventDot;
        Text cxTitle, cxSpace, cxPillText, cxRingCost, cxPlans;
        Image cxPillFill, cxEventIcon;
        Btn cxContractsBtn, cxBookBtn, cxCloseBtn, cxUpBtn;
        Badge cxContractsBadge;
        readonly Btn[] cxFloorBtn = new Btn[3];
        readonly List<Btn> cxRingBtns = new List<Btn>();
        string cxTraySig = "";
        Module cxRingFor;
        float cxEvolveArmT = -9f;
        readonly Dictionary<string, Sprite> cxIcons = new Dictionary<string, Sprite>();
        readonly List<RectTransform> cxWarn = new List<RectTransform>();
        CanvasGroup[] cxHidden;

        public bool ComplexUiOpen { get { return cxHud != null && cxHud.gameObject.activeSelf; } }

        /// <summary>El mundo queda sin indicadores mientras se coloca o en el Modo Cuartel.</summary>
        bool WorldQuiet { get { return game.Placing || game.ComplexMode; } }

        public void ComplexOpened()
        {
            CloseSheet();
            if (cxHud == null) BuildComplexUi();
            cxHud.gameObject.SetActive(true);
            cxTraySig = "";
            RefreshTray();
            Tw.MoveFrom(cxTray, cxTray.anchoredPosition + new Vector2(0f, -260f), cxTray.anchoredPosition, 0.32f, Ease.OutBack);
            HideBottomButtons(true);
            ModSelected(null);
        }

        public void ComplexClosed()
        {
            if (cxHud != null) cxHud.gameObject.SetActive(false);
            HideBottomButtons(false);
            ModSelected(null);
        }

        void HideBottomButtons(bool hide)
        {
            if (cxHidden == null)
            {
                var l = new List<CanvasGroup>();
                foreach (var b in new[] { turboBtn, expandBtn, chestBtn })
                {
                    if (b == null) continue;
                    var cg = b.GetComponent<CanvasGroup>() ?? b.gameObject.AddComponent<CanvasGroup>();
                    l.Add(cg);
                }
                cxHidden = l.ToArray();
            }
            foreach (var cg in cxHidden) { if (cg == null) continue; cg.alpha = hide ? 0f : 1f; cg.blocksRaycasts = !hide; }
        }

        void BuildComplexUi()
        {
            cxHud = Kit.New("ModoCuartel", hudLayer);
            Kit.Stretch(cxHud);
            // arriba: nivel y espacio del Cuartel + mejorar
            cxTop = Glass(cxHud, 420, 64, "Cuartel");
            Kit.Place(cxTop, 0.5f, 0f, -210f, 132f, 420, 64);
            var ic = Kit.Img(cxTop, Icons.Get("hammer"), Color.white, "Icono");
            Kit.PlaceTL(ic.rectTransform, 12, 12, 40, 40);
            cxTitle = Kit.LabelAt(cxTop, "", 24, Color.white, 4, true, 60, 6, 220, 30);
            cxSpace = Kit.LabelAt(cxTop, "", 18, new Color(1f, 1f, 1f, 0.85f), 3, false, 60, 34, 220, 24);
            cxUpBtn = Kit.Button(cxTop, Loc.T("Mejorar"), Kit.Green, 20, 120, 48, "Mejorar");
            Kit.Place((RectTransform)cxUpBtn.transform, 1f, 0f, -128f, 8f, 120, 48);
            cxUpBtn.Clicked += () => { var p = Isl.BarracksPlot; if (p != null) OpenBuilding(p); };
            // derecha: contratos, libro, planos y cerrar
            cxRight = Kit.New("Derecha", cxHud);
            Kit.Place(cxRight, 1f, 0f, -104f, 210f, 90, 400);
            cxCloseBtn = CxRound(cxRight, "close", Kit.Red, 0f);
            cxCloseBtn.Clicked += () => game.ExitComplex();
            cxContractsBtn = CxRound(cxRight, "mission", Kit.Blue, 96f);
            cxContractsBtn.Clicked += OpenContracts;
            cxContractsBadge = Kit.MakeBadge(cxContractsBtn.transform);
            cxBookBtn = CxRound(cxRight, "star", Kit.Purple, 192f);
            var medal = cxBookBtn.transform.Find("Icono");
            if (medal != null) medal.GetComponent<Image>().sprite = TripoIcon("cx_medalla", "star");
            cxBookBtn.Clicked += OpenBook;
            cxPlans = Kit.LabelAt(cxRight, "", 20, Color.white, 4, true, 0, 280, 90, 30, TextAnchor.MiddleCenter);
            // izquierda: pisos
            cxFloors = Kit.New("Pisos", cxHud);
            Kit.Place(cxFloors, 0f, 0f, 16f, 230f, 80, 260);
            for (int i = 0; i < 3; i++)
            {
                int f = i;
                var b = Kit.Button(cxFloors, (i + 1).ToString(), Kit.Blue, 28, 72, 72, "Piso" + (i + 1));
                Kit.PlaceTL((RectTransform)b.transform, 4, 4 + (2 - i) * 84, 72, 72);
                b.Clicked += () => game.SetViewFloor(f);
                cxFloorBtn[i] = b;
            }
            // abajo: bandeja horizontal de modulos
            cxTray = Glass(cxHud, Kit.CanvasSize.x, 230, "Bandeja");
            cxTray.anchorMin = new Vector2(0f, 0f); cxTray.anchorMax = new Vector2(1f, 0f);
            cxTray.pivot = new Vector2(0.5f, 0f);
            cxTray.sizeDelta = new Vector2(0f, 230f);
            cxTray.anchoredPosition = Vector2.zero;
            var sr = Kit.New("Desliza", cxTray);
            Kit.Stretch(sr, 10, 12, 10, 12);
            var catcher = sr.gameObject.AddComponent<Image>(); catcher.color = new Color(0, 0, 0, 0);
            var scroll = sr.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.decelerationRate = 0.12f;
            var vp = Kit.New("Viewport", sr); Kit.Stretch(vp); vp.gameObject.AddComponent<RectMask2D>();
            cxTrayContent = Kit.New("Contenido", vp);
            cxTrayContent.anchorMin = new Vector2(0f, 0f); cxTrayContent.anchorMax = new Vector2(0f, 1f);
            cxTrayContent.pivot = new Vector2(0f, 0.5f);
            cxTrayContent.offsetMin = Vector2.zero; cxTrayContent.offsetMax = Vector2.zero;
            scroll.viewport = vp; scroll.content = cxTrayContent;
            // contrato activo (se ve tambien fuera del modo)
            cxPill = Glass(hudLayer, 300, 54, "Contrato");
            Kit.Place(cxPill, 0.5f, 0f, -150f, 76f, 300, 54);
            var pic = Kit.Img(cxPill, Icons.Get("mission"), Color.white, "Icono");
            Kit.PlaceTL(pic.rectTransform, 8, 7, 40, 40);
            cxPillText = Kit.LabelAt(cxPill, "", 20, Color.white, 4, true, 54, 4, 236, 26);
            var bar = Kit.RoundImg(cxPill, 6, new Color(0f, 0f, 0f, 0.35f), "Barra");
            Kit.PlaceTL(bar.rectTransform, 54, 34, 230, 12);
            cxPillFill = Kit.RoundImg(bar.transform, 6, Kit.Yellow, "Relleno");
            cxPillFill.rectTransform.anchorMin = new Vector2(0f, 0f); cxPillFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            cxPillFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            cxPillFill.rectTransform.offsetMin = Vector2.zero; cxPillFill.rectTransform.offsetMax = Vector2.zero;
            var pillHit = cxPill.gameObject.AddComponent<Btn>();
            pillHit.Clicked += OpenContracts;
            cxPill.gameObject.SetActive(false);
            // globo del evento (en el mundo)
            cxEventDot = Dot(worldLayer, Icons.Get("hammer"), Kit.Yellow, 64, "EventoCuartel");
            cxEventIcon = cxEventDot.Find("Icono") != null ? cxEventDot.Find("Icono").GetComponent<Image>() : cxEventDot.GetComponentInChildren<Image>();
            var eh = cxEventDot.gameObject.AddComponent<Btn>();
            eh.Clicked += TapCxEvent;
            cxEventDot.gameObject.SetActive(false);
            // anillo de acciones
            cxRing = Kit.New("Acciones", worldLayer);
            cxRing.sizeDelta = new Vector2(10, 10);
            AddRingBtn("arrow", Kit.Blue, () => { var m = game.SelectedMod; if (m != null) { game.SelectedMod = null; ModSelected(null); game.BeginModMove(m); } });
            AddRingBtn("rebirth", Kit.Purple, () => RotateSelected());
            AddRingBtn("boost", Kit.Green, () => EvolveSelected());
            AddRingBtn("skin", Kit.Orange, () => OpenDeco(game.SelectedMod));
            AddRingBtn("gear", Kit.Gray, () => OpenModInfo(game.SelectedMod));
            cxRingCost = Kit.Label(cxRing, "", 18, Color.white, 4, true, TextAnchor.MiddleCenter, "Costo");
            cxRingCost.rectTransform.sizeDelta = new Vector2(220, 30);
            cxRing.gameObject.SetActive(false);
            cxHud.gameObject.SetActive(false);
        }

        Btn CxRound(Transform parent, string icon, Color col, float y)
        {
            var b = Kit.HitArea(parent, 84, 84, icon);
            Kit.PlaceTL((RectTransform)b.transform, 3, y, 84, 84);
            var sh = Kit.RoundImg(b.transform, 38, new Color(0f, 0f, 0f, 0.22f), "Sombra");
            Kit.Place(sh.rectTransform, 0.5f, 0.5f, -38f, -34f, 76, 76);
            var d = Kit.RoundImg(b.transform, 38, col, "Disco");
            Kit.Place(d.rectTransform, 0.5f, 0.5f, -38f, -38f, 76, 76);
            var ic = Kit.Img(b.transform, Icons.Get(icon), Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.Place(ic.rectTransform, 0.5f, 0.5f, -22f, -22f, 44, 44);
            return b;
        }

        void AddRingBtn(string icon, Color col, System.Action act)
        {
            var b = RoundBtn(cxRing, col, icon, 0f);
            b.Clicked += () => act();
            cxRingBtns.Add(b);
        }

        // ------------------------------------------------------------ bandeja
        sealed class TrayItem { public ModKind K; public int Ch = -1; public int State; public string Price; public string Badge; }

        List<TrayItem> TrayItems()
        {
            var l = new List<TrayItem>();
            int lv = Isl.BarracksLevel;
            if (Isl.Plans > 0 && Isl.CountMod(ModKind.Secret) == 0)
                l.Add(new TrayItem { K = ModKind.Secret, State = Isl.CanBuySecret() ? 1 : (Isl.FreeCells(ModKind.Secret).Count == 0 ? 2 : 4), Price = BigNum.Fmt(Isl.SecretCost()), Badge = "x" + Isl.Plans });
            for (int i = 0; i < Island.PortableKinds.Length; i++)
                if (Isl.PortInv[i] > 0)
                {
                    var k = Island.PortableKinds[i];
                    l.Add(new TrayItem { K = k, State = Isl.FreeCells(k).Count > 0 ? 1 : 2, Price = Loc.T("20 min"), Badge = "x" + Isl.PortInv[i] });
                }
            if (Isl.CombosFound[0] && Isl.CountMod(ModKind.Experimental) == 0)
                l.Add(new TrayItem { K = ModKind.Experimental, State = Isl.CanBuyExperimental() ? 1 : 4, Price = BigNum.Fmt(Isl.ModCost(ModKind.Experimental)) });
            for (int ch = 1; ch < Island.Roster.Length; ch++)
            {
                if (Island.Roster[ch].Secret) continue;
                int st = Isl.RoomState(ch);
                if (st == 3) continue;
                if (st == 0 && Island.Roster[ch].RoomLevel > lv + 1) continue;
                l.Add(new TrayItem { K = ModKind.Dorm, Ch = ch, State = st == 1 ? (Isl.CanBuyRoom(ch) ? 1 : 4) : st == 0 ? 0 : 2, Price = st == 0 ? Loc.T("Nv ") + Island.Roster[ch].RoomLevel : BigNum.Fmt(Isl.RoomCost(ch)) });
            }
            var defs = new List<ModDef>(Island.ModDefs);
            defs.Sort((a, b) => a.Level.CompareTo(b.Level));
            foreach (var d in defs)
            {
                if (!d.Buyable || d.Kind == ModKind.Dorm) continue;
                if (d.Level > lv + 1 && !Isl.ModUnlocked(d.Kind)) continue;   // adelanto de lo proximo, no todo el catalogo
                int block = Isl.ModBuyBlock(d.Kind);
                if (block == 2) continue;   // ya tiene todos los de ese tipo
                int st = block == 0 ? 1 : block == 1 ? 0 : block == 3 ? 2 : 4;
                l.Add(new TrayItem { K = d.Kind, State = st, Price = st == 0 ? Loc.T("Nv ") + d.Level : BigNum.Fmt(Isl.ModCost(d.Kind)) });
            }
            // primero lo que se puede poner ya
            l.Sort((a, b) => (a.State == 1 ? 0 : a.State == 4 ? 1 : a.State == 2 ? 2 : 3).CompareTo(b.State == 1 ? 0 : b.State == 4 ? 1 : b.State == 2 ? 2 : 3));
            return l;
        }

        void RefreshTray()
        {
            var items = TrayItems();
            var sb = new System.Text.StringBuilder();
            foreach (var it in items) sb.Append((int)it.K).Append(':').Append(it.Ch).Append(':').Append(it.State).Append(':').Append(it.Price).Append(it.Badge).Append('|');
            string sig = sb.ToString();
            if (sig == cxTraySig) return;
            bool first = cxTraySig == "";
            cxTraySig = sig;
            for (int i = cxTrayContent.childCount - 1; i >= 0; i--) Destroy(cxTrayContent.GetChild(i).gameObject);
            float x = 8f;
            int n = 0;
            foreach (var it in items)
            {
                var card = TrayCard(cxTrayContent, it);
                card.anchorMin = card.anchorMax = new Vector2(0f, 0.5f);
                card.pivot = new Vector2(0f, 0.5f);
                card.anchoredPosition = new Vector2(x, 0f);
                if (first) { card.localScale = Vector3.zero; Tw.Scale(card, Vector3.zero, Vector3.one, 0.28f, Ease.OutBack, 0.03f * n); }
                x += 156f; n++;
            }
            cxTrayContent.sizeDelta = new Vector2(x + 8f, 0f);
        }

        RectTransform TrayCard(Transform parent, TrayItem it)
        {
            bool locked = it.State == 0;
            Color edge = it.K == ModKind.Dorm ? IslandArt.SpecColor(it.Ch) : IslandArt.H(Island.MDef(it.K).Hex);
            var card = Kit.OutBox(parent, 16, 4, 6, locked ? Icons.H("d8cdb8") : Color.white, locked ? Kit.Out : edge, "Tarjeta");
            card.sizeDelta = new Vector2(148, 196);
            var icon = Kit.Img(card, CxIcon(it.K, it.Ch), locked ? new Color(0.2f, 0.16f, 0.12f, 0.75f) : Color.white, "Icono");
            icon.preserveAspect = true;
            Kit.PlaceTL(icon.rectTransform, 12, 8, 124, 112);
            var name = Kit.LabelAt(card, it.K == ModKind.Dorm ? Island.Roster[it.Ch].Name : Island.MDef(it.K).Name, 16, Kit.Brown, 0, true, 4, 120, 140, 34, TextAnchor.MiddleCenter);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 11; name.resizeTextMaxSize = 16;
            var price = Kit.LabelAt(card, it.Price, 19, locked ? Kit.OrangeD : it.State == 4 ? Kit.Red : Kit.GreenD, 0, true, 4, 154, 140, 30, TextAnchor.MiddleCenter);
            if (locked) { var lk = Kit.Icon(card, "lock", 34); Kit.Place((RectTransform)lk.transform, 0.5f, 0f, -17f, 46f, 34, 34); }
            if (it.State == 2) { var full = Kit.MakeTag(card, Loc.T("Sin lugar"), Kit.Gray, 14); Kit.PlaceTL(full, 14, 8, 120, 24); }
            if (!string.IsNullOrEmpty(it.Badge)) { var bt = Kit.MakeTag(card, it.Badge, Kit.Purple, 16); Kit.PlaceTL(bt, 92, 6, 50, 26); }
            var b = card.gameObject.AddComponent<Btn>();
            var item = it;
            b.Clicked += () => TrayTap(item, b);
            if (it.State == 1) AddShine(b, () => true);
            return card;
        }

        void TrayTap(TrayItem it, Btn b)
        {
            switch (it.State)
            {
                case 1:
                    Sfx.Play("cx_card", -4f);
                    Mineros.Fx.Haptics.Medium();
                    game.BeginModPlace(it.K, it.Ch);
                    return;
                case 0:
                    NoMoney(b);
                    Toast(Loc.T("Se habilita con el Cuartel nivel ") + (it.K == ModKind.Dorm ? Island.Roster[it.Ch].RoomLevel : Island.MDef(it.K).Level), Kit.Gray, null, true);
                    return;
                case 2:
                    NoMoney(b);
                    Toast(Loc.T("No hay lugar: subí el Cuartel"), Kit.Gray, null, true);
                    return;
                default:
                {
                    NoMoney(b);
                    var mats = it.K == ModKind.Dorm ? Isl.RoomMats(it.Ch) : Isl.ModMats(it.K);
                    if (!Isl.HasMats(mats)) { Toast(Loc.T("Te faltan materiales"), Kit.Gray, null, true); MissingPop((RectTransform)b.transform, mats); }
                    else Toast(Loc.T("Te faltan monedas"), Kit.Gray, null, true);
                    return;
                }
            }
        }

        /// <summary>Icono de un modulo: la pieza de Tripo (o la carcasa) fotografiada; los dormitorios, la cara del minero.</summary>
        Sprite CxIcon(ModKind k, int ch)
        {
            if (k == ModKind.Dorm) return Icons.MinerBust(IslandArt.SpecColor(ch));
            string key = ((int)k).ToString();
            Sprite s;
            if (cxIcons.TryGetValue(key, out s) && s != null) return s;
            Material mat = null;
            string file = IslandArt.PropFile(k);
            var mesh = file != "" ? IslandArt.TripoModel(file, out mat) : null;
            if (mesh != null) s = IslandStage.I.RenderIcon(mesh, new[] { mat }, 192);
            else
            {
                var mb = new MeshBuilder();
                IslandArt.ModuleGhost(mb, k, -1, 1);
                Material[] mats;
                var gm = mb.ToMesh(null, "IconoModulo", out mats);
                mats = VertexColorMerge.Apply(gm, mats);
                s = IslandStage.I.RenderIcon(gm, mats, 192);
            }
            cxIcons[key] = s;
            return s;
        }

        // ------------------------------------------------------------ cada cuadro
        void UpdateComplexUi()
        {
            UpdateContractPill();
            UpdateCxEvent();
            if (!ComplexUiOpen) { HideWarn(); return; }
            int lv = Isl.BarracksLevel;
            if (cxTitle.text != Loc.T("Cuartel ") + lv) cxTitle.text = Loc.T("Cuartel ") + lv;
            int used = 0, total = 0;
            for (int f = 0; f <= 2; f++)
                for (int x = -2; x <= 2; x++)
                    for (int z = -2; z <= 2; z++)
                        if (Island.CellUnlocked(x, z, f, lv) && !(x == 0 && z == 0 && f == 0)) { total++; if (Isl.ModAt(x, z, f) != null) used++; }
            string sp = used + " / " + total + Loc.T(" lugares");
            if (cxSpace.text != sp) cxSpace.text = sp;
            var bp = Isl.BarracksPlot;
            bool canUp = bp != null && Isl.CanUpgrade(bp);
            cxUpBtn.Modulate = canUp ? Color.white : new Color(0.72f, 0.72f, 0.75f);
            int maxF = game.MaxFloorUnlocked();
            cxFloors.gameObject.SetActive(maxF > 0);
            for (int i = 0; i < 3; i++)
            {
                bool on = i <= maxF;
                if (cxFloorBtn[i].gameObject.activeSelf != on) cxFloorBtn[i].gameObject.SetActive(on);
                var want = i == game.ViewFloor ? Kit.Yellow : Kit.Blue;
                cxFloorBtn[i].transform.localScale = Vector3.one * (i == game.ViewFloor ? 1.08f : 0.94f);
                if (cxFloorBtn[i].Label.color != (i == game.ViewFloor ? Kit.Brown : Color.white)) { cxFloorBtn[i].SetSkin(Kit.Skin(want)); cxFloorBtn[i].Label.color = i == game.ViewFloor ? Kit.Brown : Color.white; }
            }
            int offers = Isl.Offers.Count;
            cxContractsBtn.gameObject.SetActive(Isl.ContractsOpen);
            cxContractsBadge.SetCount(offers > 0 && Isl.ActiveContract == null ? offers : 0);
            string pl = Isl.Plans > 0 ? Loc.T("Planos ") + Isl.Plans : "";
            if (cxPlans.text != pl) cxPlans.text = pl;
            if (!game.Placing) RefreshTray();
            cxTray.gameObject.SetActive(!game.Placing);
            UpdateRing();
            UpdateWarn();
            UpdateNewCells();
            // primera vez: la primera tarjeta que se puede poner late (sin texto: se entiende tocando)
            if (Isl.Modules.Count <= 1 && !game.Placing && cxTrayContent.childCount > 0)
            {
                var first = cxTrayContent.GetChild(0);
                first.localScale = Vector3.one * (1f + 0.06f * Mathf.Abs(Mathf.Sin(Time.time * 3.5f)));
            }
        }

        readonly List<Image> cxNewMarks = new List<Image>();

        /// <summary>Al subir el Cuartel: las celdas nuevas laten en dorado unos segundos (la regla nueva se ve).</summary>
        void UpdateNewCells()
        {
            int n = game.NewCellsT > 0f ? game.NewCells.Count : 0;
            while (cxNewMarks.Count < n)
            {
                var im = Kit.Img(worldLayer, Icons.Ring(6f), Kit.Yellow, "CeldaNueva");
                im.rectTransform.SetAsFirstSibling();
                cxNewMarks.Add(im);
            }
            for (int i = 0; i < cxNewMarks.Count; i++)
            {
                var im = cxNewMarks[i];
                bool on = i < n && game.NewCells[i][2] <= game.ViewFloor;
                if (im.gameObject.activeSelf != on) im.gameObject.SetActive(on);
                if (!on) continue;
                var c = game.NewCells[i];
                Vector3 w = game.CellWorld(c[0], c[1], c[2]);
                Vector2 a = ToCanvas(w + new Vector3(0.9f, 0f, 0f)), b = ToCanvas(w - new Vector3(0.9f, 0f, 0f));
                Vector2 cc = ToCanvas(w);
                float size = Mathf.Abs(a.x - b.x) * (1f + 0.12f * Mathf.Sin(Time.time * 7f + i));
                im.rectTransform.anchoredPosition = new Vector2(cc.x, -cc.y);
                im.rectTransform.sizeDelta = new Vector2(size, size * 0.62f);
                im.color = new Color(1f, 0.85f, 0.3f, Mathf.Clamp01(game.NewCellsT));
            }
        }

        void UpdateContractPill()
        {
            if (cxPill == null) { if (Isl.ActiveContract != null && cxHud == null) BuildComplexUi(); return; }
            var c = Isl.ActiveContract;
            bool on = c != null && sheet == null;
            if (cxPill.gameObject.activeSelf != on) { cxPill.gameObject.SetActive(on); if (on) Tw.Pop(cxPill, 1.1f); }
            if (!on) return;
            string t = c.Prog + "/" + c.Target + "  " + Clock(c.Left);
            if (cxPillText.text != t) cxPillText.text = t;
            float f = Mathf.Clamp01((float)c.Prog / Mathf.Max(1, c.Target));
            cxPillFill.rectTransform.sizeDelta = new Vector2(230f * f, cxPillFill.rectTransform.sizeDelta.y);
            cxPillFill.color = c.Left < 30f ? Kit.Red : Kit.Yellow;
        }

        // ------------------------------------------------------------ anillo de acciones
        public void ModSelected(Module m)
        {
            if (cxRing == null) return;
            cxRingFor = m;
            cxEvolveArmT = -9f;
            bool on = m != null;
            cxRing.gameObject.SetActive(on);
            if (on) Tw.Pop(cxRing, 1.2f);
        }

        void UpdateRing()
        {
            var m = game.SelectedMod;
            if (m != cxRingFor) ModSelected(m);
            if (m == null || !cxRing.gameObject.activeSelf) return;
            if (Isl.ModById(m.Id) == null) { game.SelectedMod = null; ModSelected(null); return; }
            Vector2 c = ToCanvas(game.ModWorld(m) + Vector3.up * 0.9f);
            // el anillo no se sale de la pantalla ni queda bajo los botones de los costados
            float hx = Kit.CanvasSize.x * 0.5f - 250f;
            cxRing.anchoredPosition = new Vector2(Mathf.Clamp(c.x, -hx, hx), Mathf.Clamp(-c.y, -Kit.CanvasSize.y * 0.5f + 420f, Kit.CanvasSize.y * 0.5f - 330f));
            bool central = m.Kind == ModKind.Central;
            bool[] show =
            {
                !central,
                !central && Isl.ValidRotations(m.Kind, m.X, m.Z, m.F, m).Count > 1,
                !central && m.Stage < Island.MaxStage && m.Expires <= 0 && m.Kind != ModKind.Corridor && m.Kind != ModKind.Trophy && m.Kind != ModKind.Secret,
                m.Expires <= 0,
                true,
            };
            int n = 0; foreach (var s in show) if (s) n++;
            int k = 0;
            for (int i = 0; i < cxRingBtns.Count; i++)
            {
                var b = cxRingBtns[i];
                if (b.gameObject.activeSelf != show[i]) b.gameObject.SetActive(show[i]);
                if (!show[i]) continue;
                float a = Mathf.PI * (0.5f + (k - (n - 1) * 0.5f) * 0.42f);
                ((RectTransform)b.transform).anchoredPosition = new Vector2(Mathf.Cos(a) * 120f, Mathf.Sin(a) * 120f);
                k++;
            }
            bool armed = Time.unscaledTime - cxEvolveArmT < 3f;
            string cost = armed && show[2] ? Loc.T("Evolucionar: ") + BigNum.Fmt(Isl.StageCost(m)) : (m.Work > 0 ? Loc.T("Mejorando ") + Clock(m.Work) : "");
            if (cxRingCost.text != cost) cxRingCost.text = cost;
            cxRingCost.rectTransform.anchoredPosition = new Vector2(0f, -86f);
        }

        void RotateSelected()
        {
            var m = game.SelectedMod;
            if (m == null) return;
            var rs = Isl.ValidRotations(m.Kind, m.X, m.Z, m.F, m);
            if (rs.Count <= 1) return;
            int i = rs.IndexOf(m.Rot);
            if (Isl.RotateModule(m, rs[(i + 1) % rs.Count])) { Sfx.Play("tick", -6f, 1.2f); Mineros.Fx.Haptics.Selection(); game.Save(); }
        }

        void EvolveSelected()
        {
            var m = game.SelectedMod;
            if (m == null) return;
            var b = cxRingBtns[2];
            if (Time.unscaledTime - cxEvolveArmT > 3f) { cxEvolveArmT = Time.unscaledTime; Tw.Pop(b.transform, 1.15f); Sfx.Play("ui", -6f); return; }   // primer toque: muestra el precio
            if (Isl.Evolve(m))
            {
                cxEvolveArmT = -9f;
                Sfx.Play("build", -4f, 1.1f);
                Sfx.Note(4, -10f);
                Mineros.Fx.Haptics.Medium();
                Vector2 c = ToCanvas(game.ModWorld(m) + Vector3.up * 1.2f);
                FlyTrail(LayerPos(coinPill.Root), new Vector2(c.x, -c.y), Icons.Get("coin"), 5);
                game.Save();
            }
            else
            {
                NoMoney(b);
                if (m.Work > 0) Toast(Loc.T("Ya se está mejorando"), Kit.Gray, null, true);
                else if (!Isl.HasMats(Isl.StageMats(m))) { Toast(Loc.T("Te faltan materiales"), Kit.Gray, null, true); MissingPop((RectTransform)b.transform, Isl.StageMats(m)); }
                else Toast(Loc.T("Te faltan monedas"), Kit.Gray, null, true);
            }
        }

        void OpenDeco(Module m)
        {
            if (m == null) return;
            var fr = OpenSheet(330, true, 620f, 0.3f);
            Kit.LabelAt(fr, Loc.T("Adornos"), 32, Kit.Brown, 0, true, 0, 14, 620, 40, TextAnchor.MiddleCenter);
            string[] icons = { "trophy", "skin", "power", "star", "boss", "mission" };
            for (int i = 1; i <= Island.DecoKinds; i++)
            {
                int d = i;
                var cell = Kit.OutBox(fr, 14, 3, 4, m.Deco == d ? Kit.Yellow : Color.white, Kit.Out, "Adorno");
                Kit.PlaceTL(cell, 20 + (i - 1) * 98, 74, 90, 170);
                var ic = Kit.Img(cell, Icons.Get(icons[i - 1]), Color.white, "Icono");
                ic.preserveAspect = true;
                Kit.PlaceTL(ic.rectTransform, 15, 12, 60, 60);
                Kit.LabelAt(cell, BigNum.Fmt(Isl.DecoCost(d)), 18, Kit.GreenD, 0, true, 0, 84, 90, 28, TextAnchor.MiddleCenter);
                var b = cell.gameObject.AddComponent<Btn>();
                b.Clicked += () =>
                {
                    if (Isl.BuyDeco(m, d)) { Sfx.Play("confirm", -6f); Mineros.Fx.Haptics.Light(); game.Save(); CloseSheet(); }
                    else { NoMoney(b); }
                };
            }
            Kit.LabelAt(fr, Loc.T("Solo decoran: suman belleza a la isla"), 18, new Color(0.5f, 0.42f, 0.35f), 0, false, 0, 262, 620, 28, TextAnchor.MiddleCenter);
        }

        void OpenModInfo(Module m)
        {
            if (m == null) return;
            var d = Island.MDef(m.Kind);
            var fr = OpenSheet(380, true, 600f, 0.3f);
            var ic = Kit.Img(fr, m.Kind == ModKind.Dorm ? Icons.MinerBust(IslandArt.SpecColor(m.Ch)) : CxIcon(m.Kind, -1), Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.PlaceTL(ic.rectTransform, 20, 20, 130, 130);
            Kit.LabelAt(fr, m.Kind == ModKind.Dorm ? Island.Roster[m.Ch].Name : d.Name, 30, Kit.Brown, 0, true, 166, 22, 410, 40);
            Kit.LabelAt(fr, m.Kind == ModKind.Dorm ? Island.Roster[m.Ch].Desc : d.Desc, 20, Kit.Brown, 0, false, 166, 64, 410, 30);
            // etapa (estrellas)
            for (int i = 0; i < Island.MaxStage; i++)
            {
                var st = Kit.Img(fr, Icons.Get("star"), i < m.Stage ? Kit.Yellow : new Color(0.75f, 0.7f, 0.62f), "Etapa");
                Kit.PlaceTL(st.rectTransform, 166 + i * 40, 100, 34, 34);
            }
            var s = Isl.Summary;
            float y = 160f;
            // con quien hace sinergia (iconos), energia y acceso
            int pairs = 0;
            foreach (var p in s.Pairs) if (p[0] == m.Id || p[1] == m.Id) pairs++;
            string info = (pairs > 0 ? Loc.T("Sinergias: ") + pairs + "   " : "") + (s.Unpowered.Contains(m.Id) ? Loc.T("Sin energía") + "   " : s.Powered.Contains(m.Id) ? Loc.T("Con energía") + "   " : "")
                + (!s.Access.Contains(m.Id) && m.Kind != ModKind.Central ? Loc.T("Sin acceso") : "");
            if (info != "") { Kit.LabelAt(fr, info, 20, Kit.OrangeD, 0, true, 20, y, 560, 30); y += 36f; }
            if (m.Kind == ModKind.Dorm || m.Kind == ModKind.Central)
            {
                int ch = m.Kind == ModKind.Dorm ? m.Ch : 0;
                var pref = Island.PreferredOf(ch);
                Kit.LabelAt(fr, Loc.T("Le gusta tener al lado:"), 20, Kit.Brown, 0, true, 20, y, 300, 30);
                var pi = Kit.Img(fr, CxIcon(pref, -1), Color.white, "Prefiere");
                pi.preserveAspect = true;
                Kit.PlaceTL(pi.rectTransform, 320, y - 14, 64, 64);
                Kit.LabelAt(fr, "+" + Mathf.RoundToInt((float)Island.PreferenceBonus(ch) * 100f) + "%", 24, Kit.GreenD, 0, true, 392, y, 120, 30);
                y += 60f;
                Kit.LabelAt(fr, Loc.T("Rinde ") + Mathf.RoundToInt((1f + s.CharBonus[Mathf.Min(ch, s.CharBonus.Length - 1)] + s.GlobalBonus) * 100f) + "%", 24, Kit.GreenD, 0, true, 20, y, 400, 30);
            }
            if (m.Expires > 0) Kit.LabelAt(fr, Loc.T("Se va en ") + Clock((float)(m.Expires - Isl.ComplexClock)), 22, Kit.OrangeD, 0, true, 20, 320, 560, 30);
        }

        // ------------------------------------------------------------ avisos sobre los modulos (solo en el modo)
        void UpdateWarn()
        {
            var s = Isl.Summary;
            int k = 0;
            foreach (var m in Isl.Modules)
            {
                if (m.F > game.ViewFloor) continue;
                string icon = null;
                if (s.Unpowered.Contains(m.Id)) icon = "power";
                else if (!s.Access.Contains(m.Id) && m.Kind != ModKind.Central) icon = "lock";
                else if (m.Work > 0) icon = "clock";
                if (icon == null) continue;
                while (cxWarn.Count <= k) cxWarn.Add(Dot(worldLayer, Icons.Get("power"), Kit.Red, 46, "Aviso"));
                var d = cxWarn[k++];
                if (!d.gameObject.activeSelf) d.gameObject.SetActive(true);
                var img = d.Find("Icono") != null ? d.Find("Icono").GetComponent<Image>() : null;
                if (img != null) img.sprite = Icons.Get(icon);
                Vector2 c = ToCanvas(game.ModWorld(m) + Vector3.up * 2.0f);
                d.anchoredPosition = new Vector2(c.x, -c.y + Mathf.Sin(Time.time * 4f + k) * 4f);
            }
            for (int i = k; i < cxWarn.Count; i++) if (cxWarn[i].gameObject.activeSelf) cxWarn[i].gameObject.SetActive(false);
        }

        void HideWarn() { foreach (var w in cxWarn) if (w.gameObject.activeSelf) w.gameObject.SetActive(false); }

        // ------------------------------------------------------------ eventos del Cuartel
        public void CxEventStarted(CxEvent e)
        {
            if (cxHud == null) BuildComplexUi();
            Tw.Pop(cxEventDot, 1.4f);
            string t;
            switch (e.Kind)
            {
                case CxEventKind.Breakdown: t = Loc.T("¡Se rompió una máquina!"); break;
                case CxEventKind.Merchant: t = Loc.T("¡Llegó un comerciante al Cuartel!"); break;
                case CxEventKind.Party: t = Loc.T("¡Fiesta en el comedor!"); break;
                case CxEventKind.Inspector: t = Loc.T("¡Visita del inspector!"); break;
                default: t = Loc.T("¡Se trabó una vagoneta!"); break;
            }
            Toast(t, Kit.Yellow, Icons.Get(CxEventIcon(e.Kind)), true);
        }

        public void CxEventEnded(CxEvent e) { if (cxEventDot != null) cxEventDot.gameObject.SetActive(false); }

        static string CxEventIcon(CxEventKind k)
        {
            switch (k)
            {
                case CxEventKind.Breakdown: return "hammer";
                case CxEventKind.Merchant: return "coin";
                case CxEventKind.Party: return "star";
                case CxEventKind.Inspector: return "mission";
                default: return "pick";
            }
        }

        void UpdateCxEvent()
        {
            if (cxEventDot == null) return;
            var e = Isl.CurEvent;
            bool on = e != null && sheet == null && !game.Placing;
            if (cxEventDot.gameObject.activeSelf != on) cxEventDot.gameObject.SetActive(on);
            if (!on) return;
            if (cxEventIcon != null) cxEventIcon.sprite = Icons.Get(CxEventIcon(e.Kind));
            Module at = e.Kind == CxEventKind.Merchant ? Isl.ModById(e.ModId) : e.Kind == CxEventKind.Jam || e.Kind == CxEventKind.Breakdown ? Isl.ModById(e.ModId) : Isl.FirstMod(ModKind.Central);
            if (at == null) at = Isl.FirstMod(ModKind.Central);
            if (at == null) return;
            Vector2 c = ToCanvas(game.ModWorld(at) + Vector3.up * 2.6f);
            float bob = Mathf.Sin(Time.time * 3f) * 6f;
            cxEventDot.anchoredPosition = new Vector2(c.x, -c.y + bob);
            cxEventDot.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 6f));
        }

        void TapCxEvent()
        {
            var e = Isl.CurEvent;
            if (e == null) return;
            Mineros.Fx.Haptics.Light();
            switch (e.Kind)
            {
                case CxEventKind.Breakdown:
                    if (e.Fixer >= 0) { Toast(Loc.T("Ya va el especialista"), Kit.Gray, null, true); return; }
                    if (Isl.SendFixer()) { Toast(Loc.T("¡Va el especialista a arreglarla!"), Kit.Green, Icons.Get("hammer"), true); Sfx.Play("confirm", -6f); return; }
                    OpenRushFix();
                    return;
                case CxEventKind.Merchant:
                    Toast(Loc.T("Lo que se vende cerca de él: +50 %"), Kit.Yellow, Icons.Get("coin"), true);
                    if (!game.ComplexMode) game.EnterComplex();
                    return;
                case CxEventKind.Party:
                    if (Isl.RingBell()) { Sfx.Play("bell", -4f, 1.2f); Sfx.PlayLater("jingle_small", 0.2f, -6f); Mineros.Fx.Haptics.Success(); game.Save(); }
                    return;
                case CxEventKind.Inspector:
                {
                    int tier = Isl.ClaimInspector();
                    if (tier >= 0) { Sfx.Play("tadaa", -4f); Toast(Loc.T("¡El inspector dejó cofres!"), Kit.Yellow, Icons.Get("chest"), true); game.Save(); }
                    return;
                }
                default:
                    Isl.TapJam();
                    Juice.Punch(cxEventDot, 0.25f, 0.2f);
                    Sfx.Play("thud", -6f, 1.3f);
                    return;
            }
        }

        void OpenRushFix()
        {
            var fr = OpenSheet(300, true, 560f, 0.3f);
            Kit.LabelAt(fr, Loc.T("No hay quien sepa arreglarla"), 26, Kit.Brown, 0, true, 0, 24, 560, 36, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, Loc.T("Se arregla sola en 3 min, o ya:"), 20, Kit.Brown, 0, false, 0, 70, 560, 30, TextAnchor.MiddleCenter);
            var b = Kit.Button(fr, Island.RushFixGems + Loc.T(" gemas"), Kit.Purple, 26, 260, 84);
            Kit.Place((RectTransform)b.transform, 0.5f, 0f, -130f, 130f, 260, 84);
            b.Clicked += () => { if (Isl.RushFix()) { CloseSheet(); Sfx.Play("tadaa", -4f); game.Save(); } else NoMoney(b); };
        }

        // ------------------------------------------------------------ contratos
        public void OpenContracts()
        {
            if (!Isl.ContractsOpen) { Toast(Loc.T("Los contratos llegan con el Cuartel nivel 6 y una Descarga"), Kit.Gray, null, true); return; }
            Isl.ContractNewDay(IslandGame.Today);
            int rows = (Isl.ActiveContract != null ? 1 : 0) + Isl.Offers.Count;
            var fr = OpenSheet(Mathf.Max(300f, 120f + rows * 190f), true, 640f, 0.38f);
            Kit.LabelAt(fr, Loc.T("Contratos"), 36, Kit.Brown, 0, true, 0, 16, 640, 46, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, Isl.ContractsToday + " / " + Isl.ContractsPerDay + Loc.T(" hoy"), 20, Kit.OrangeD, 0, true, 0, 62, 640, 28, TextAnchor.MiddleCenter);
            float y = 104f;
            if (Isl.ActiveContract != null) { ContractRow(fr, Isl.ActiveContract, -1, y); y += 190f; }
            for (int i = 0; i < Isl.Offers.Count; i++) { ContractRow(fr, Isl.Offers[i], i, y); y += 190f; }
            if (Isl.ActiveContract == null && Isl.Offers.Count == 0)
                Kit.LabelAt(fr, Loc.T("Vuelven pronto"), 26, Kit.Brown, 0, true, 0, y + 40, 640, 40, TextAnchor.MiddleCenter);
        }

        void ContractRow(Transform fr, Contract c, int idx, float y)
        {
            var box = Kit.OutBox(fr, 18, 4, 5, c.Active ? Kit.Cream : Color.white, c.Active ? Kit.Yellow : Kit.Out, "Contrato");
            Kit.PlaceTL(box, 20, y, 600, 176);
            Sprite icon = c.Kind == 0 ? OreIcon(c.What) : c.Kind == 1 ? ResIcons.Get(Res.IronBar) : CxIcon((ModKind)c.What, -1);
            var ic = Kit.Img(box, icon, Color.white, "Que");
            ic.preserveAspect = true;
            Kit.PlaceTL(ic.rectTransform, 14, 14, 96, 96);
            Kit.LabelAt(box, (c.Active ? c.Prog + " / " : "") + c.Target, 34, Kit.Brown, 0, true, 120, 14, 220, 44);
            TimeTag(box, 120, 64, Clock(c.Active ? c.Left : c.Time), 22);
            // premio
            var coin = Kit.Icon(box, "coin", 30); Kit.PlaceTL((RectTransform)coin.transform, 120, 112, 30, 30);
            Kit.LabelAt(box, BigNum.Fmt(c.Coins), 22, Kit.GreenD, 0, true, 154, 112, 120, 30);
            var gem = Kit.Icon(box, "gem", 28); Kit.PlaceTL((RectTransform)gem.transform, 276, 113, 28, 28);
            Kit.LabelAt(box, c.Gems.ToString(), 22, Kit.Purple, 0, true, 306, 112, 50, 30);
            float rx = 356f;
            if (c.Plans > 0) { var p = Kit.Icon(box, "star", 30); Kit.PlaceTL((RectTransform)p.transform, rx, 112, 30, 30); rx += 36f; }
            if (c.Portable >= 0) { var p = Kit.Img(box, CxIcon((ModKind)c.Portable, -1), Color.white, "Portatil"); p.preserveAspect = true; Kit.PlaceTL(p.rectTransform, rx, 106, 42, 42); }
            if (idx >= 0)
            {
                var b = Kit.Button(box, Loc.T("Aceptar"), Kit.Green, 24, 170, 80);
                Kit.PlaceTL((RectTransform)b.transform, 414, 46, 170, 80);
                b.Clicked += () =>
                {
                    if (Isl.AcceptContract(idx)) { Sfx.Play("confirm", -4f); Mineros.Fx.Haptics.Medium(); game.Save(); CloseSheet(); }
                    else { NoMoney(b); Toast(Isl.ActiveContract != null ? Loc.T("Ya hay un contrato en curso") : Loc.T("No quedan contratos hoy"), Kit.Gray, null, true); }
                };
            }
            else
            {
                var bar = Kit.OutBox(box, 8, 3, 0, Icons.H("d8c6a2"), Kit.Out, "Barra");
                Kit.PlaceTL(bar, 414, 70, 170, 22);
                var fill = Kit.RoundImg(bar, 6, Kit.Green, "Relleno");
                fill.rectTransform.anchorMin = new Vector2(0f, 0f); fill.rectTransform.anchorMax = new Vector2(0f, 1f);
                fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                fill.rectTransform.offsetMin = new Vector2(3, 3); fill.rectTransform.offsetMax = new Vector2(3, -3);
                sheetRefresh.Add(() => fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(8f, 164f * Mathf.Clamp01((float)c.Prog / Mathf.Max(1, c.Target))), fill.rectTransform.sizeDelta.y));
            }
        }

        public void ContractEnded(Contract c, bool ok)
        {
            if (ok)
            {
                Toast(Loc.T("¡Contrato cumplido!"), Kit.Yellow, Icons.Get("mission"), true);
                Sfx.Play("goal", -3f); Sfx.Play("coins_pour", -6f);
                Mineros.Fx.Haptics.Success();
                FlyCoins(LayerPos(cxPill != null ? cxPill : coinPill.Root), 8);
            }
            else Toast(Loc.T("El contrato venció. Llegan otros"), new Color(0.85f, 0.88f, 0.92f), null, true);
        }

        // ------------------------------------------------------------ libro de combinaciones
        /// <summary>Pistas del libro como iconos de modulos (sin texto).</summary>
        static readonly int[][] ComboHint =
        {
            new[] { -4, -5, (int)ModKind.Lab }, new[] { -2, (int)ModKind.Smelter, -3 }, new[] { (int)ModKind.Vault, (int)ModKind.Treasury, -6 },
            new[] { (int)ModKind.Classroom, (int)ModKind.Lab }, new[] { (int)ModKind.Mess, -1, -1, -1 }, new[] { (int)ModKind.Observatory, (int)ModKind.Central },
            new[] { (int)ModKind.Unload, (int)ModKind.Crusher, (int)ModKind.Smelter, (int)ModKind.Storage, (int)ModKind.Treasury }, new[] { -1, -1, -1, -1 },
            new[] { (int)ModKind.Tools, -2, (int)ModKind.Central }, new[] { (int)ModKind.Windmill, (int)ModKind.Lab }, new[] { -9, (int)ModKind.Drill }, new[] { -10, (int)ModKind.Observatory },
        };

        Sprite HintIcon(int v)
        {
            if (v >= 0) return CxIcon((ModKind)v, -1);
            int ch = -v;
            if (v == -1) return Icons.MinerBust(new Color(0.85f, 0.8f, 0.7f));
            return Icons.MinerBust(IslandArt.SpecColor(ch));
        }

        public void OpenBook()
        {
            var fr = OpenSheet(1100, true, 660f, 0.4f);
            int found = 0; foreach (var b in Isl.CombosFound) if (b) found++;
            Kit.LabelAt(fr, Loc.T("Libro de combinaciones"), 34, Kit.Brown, 0, true, 0, 16, 660, 46, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, found + " / " + Island.ComboCount, 22, Kit.OrangeD, 0, true, 0, 62, 660, 30, TextAnchor.MiddleCenter);
            for (int i = 0; i < Island.ComboCount; i++)
            {
                bool has = Isl.CombosFound[i];
                bool active = Isl.Summary.Combos[i];
                var cell = Kit.OutBox(fr, 16, 4, 5, has ? Kit.Cream : new Color(0.8f, 0.76f, 0.7f), active ? Kit.Yellow : Kit.Out, "Combinacion");
                Kit.PlaceTL(cell, 22 + (i % 3) * 210, 104 + (i / 3) * 240, 196, 228);
                var hint = ComboHint[i];
                float w = Mathf.Min(56f, 176f / hint.Length);
                for (int k = 0; k < hint.Length; k++)
                {
                    var im = Kit.Img(cell, HintIcon(hint[k]), has ? Color.white : new Color(0.2f, 0.16f, 0.12f, 0.8f), "Pista");
                    im.preserveAspect = true;
                    Kit.PlaceTL(im.rectTransform, 98 - hint.Length * w * 0.5f + k * w, 18, w, w);
                }
                var nm = Kit.LabelAt(cell, has ? Island.ComboNames[i] : "???", 20, Kit.Brown, 0, true, 6, 96, 184, 56, TextAnchor.MiddleCenter);
                Kit.Wrap(nm);
                if (has) { var ok = Kit.Icon(cell, active ? "check" : "star", 36); Kit.Place((RectTransform)ok.transform, 0.5f, 0f, -18f, 162f, 36, 36); }
                cell.localScale = Vector3.zero;
                Tw.Scale(cell, Vector3.zero, Vector3.one, 0.3f, Ease.OutBack, 0.03f * i);
            }
        }

        public void ComboFound(int i)
        {
            if (cxHud == null) BuildComplexUi();
            Sfx.Play("cx_magic", -2f);
            Sfx.PlayLater("fanfare", 0.5f, -6f);
            Mineros.Fx.Haptics.Success();
            Flash(new Color(1f, 0.92f, 0.6f), 0.35f);
            Toast(Loc.T("¡Combinación! ") + Island.ComboNames[i], Kit.Yellow, TripoIcon("cx_medalla", "star"), true);
            foreach (var id in Isl.Summary.ComboMods)
            {
                var m = Isl.ModById(id);
                if (m != null) Mineros.Fx.Fx.Play("unlock_burst", game.ModWorld(m) + Vector3.up * 1.4f, new Color(1f, 0.85f, 0.35f), 1f);
            }
        }

        public void SecretRevealed(Module m, SecretOut o, int detail)
        {
            var fr = OpenSheet(460, true, 560f, 0.45f);
            Sprite icon; string title, sub;
            switch (o)
            {
                case SecretOut.Miner: icon = Icons.MinerBust(IslandArt.SpecColor(detail)); title = Loc.T("¡") + Island.Roster[detail].Name + "!"; sub = Island.Roster[detail].Desc; break;
                case SecretOut.Machine: icon = CxIcon((ModKind)detail, -1); title = Island.MDef((ModKind)detail).Name; sub = Island.MDef((ModKind)detail).Desc; break;
                case SecretOut.NewRoom: icon = CxIcon((ModKind)detail, -1); title = Loc.T("¡Habitación nueva!"); sub = Island.MDef((ModKind)detail).Name + Loc.T(" antes de tiempo"); break;
                case SecretOut.Upgrade: icon = Icons.Get("boost"); title = Loc.T("¡Mejora gratis!"); sub = Loc.T("Una habitación subió de etapa"); break;
                case SecretOut.Expedition: icon = Icons.Get("chest"); title = Loc.T("¡Cofre de oro!"); sub = Loc.T("Lo guardaba el que la construyó"); break;
                default: icon = Icons.Get("gem"); title = "+" + detail + Loc.T(" gemas"); sub = Loc.T("Un tesoro escondido"); break;
            }
            var ic = Kit.Img(fr, icon, Color.white, "Premio");
            ic.preserveAspect = true;
            Kit.Place(ic.rectTransform, 0.5f, 0f, -90f, 24f, 180, 180);
            Tw.Pop(ic.rectTransform, 1.4f);
            Kit.LabelAt(fr, Loc.T("Sala secreta"), 20, new Color(0.5f, 0.35f, 0.75f), 0, true, 0, 210, 560, 30, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, title, 34, Kit.Brown, 0, true, 0, 244, 560, 46, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, sub, 22, Kit.Brown, 0, false, 0, 292, 560, 30, TextAnchor.MiddleCenter);
            var b = Kit.Button(fr, Loc.T("¡Genial!"), Kit.Green, 28, 240, 78);
            Kit.Place((RectTransform)b.transform, 0.5f, 0f, -120f, 350f, 240, 78);
            b.Clicked += CloseSheet;
            game.Save();
        }

        /// <summary>Al caer un modulo: cartel con lo que gano el Complejo (si cambio algo).</summary>
        public void ModPlacedFx(Module m)
        {
            var s = Isl.Summary;
            int pairs = 0;
            foreach (var p in s.Pairs) if (p[0] == m.Id || p[1] == m.Id) pairs++;
            if (pairs > 0) FloatText(game.ModWorld(m) + Vector3.up * 2.2f, "+" + pairs + Loc.T(" sinergia") + (pairs > 1 ? "s" : ""), new Color(0.6f, 1f, 0.5f), 30);
            cxTraySig = "";
        }

        /// <summary>Hoja del Cuartel: que trae el proximo nivel (nueva regla de construccion) y aviso si no tiene lugar.</summary>
        static readonly string[] LevelRule =
        {
            "", Loc.T("Una habitación al frente"), Loc.T("Atrás + Descarga, Pasillo y Tesorería"), Loc.T("Laterales + Almacén, Trituradora y Comedor"),
            Loc.T("Esquinas + Fundición y Generador"), Loc.T("¡Segundo piso! + Laboratorio y Escalera"), Loc.T("Más arriba + Pulidora, Bóveda y Aula; contratos"),
            Loc.T("Patio exterior + Taladro, Molino y Desvío"), Loc.T("Segundo piso completo + Palomar"), Loc.T("¡Torres! + Observatorio"),
        };

        void BarracksSection(Transform body, Plot p)
        {
            int next = p.Level + 1;
            var s = Section(body, Isl.BarracksCramped(p) ? 150 : 96, "ProximoNivel");
            var box = Kit.OutBox(s, 18, 4, 5, Kit.Cream, Kit.Out, "Caja");
            Kit.Stretch(box, 4, 4, 4, 4);
            if (next < LevelRule.Length)
            {
                Kit.LabelAt(box, Loc.T("Nivel ") + next + ":", 22, Kit.OrangeD, 0, true, 16, 10, 140, 30);
                var r = Kit.LabelAt(box, LevelRule[next], 22, Kit.Brown, 0, true, 120, 10, 490, 60);
                Kit.Wrap(r);
            }
            if (Isl.BarracksCramped(p))
            {
                var w = Kit.LabelAt(box, Loc.T("Necesita más lugar alrededor: movelo"), 22, Kit.Red, 0, true, 16, 92, 420, 40);
                w.alignment = TextAnchor.MiddleLeft;
                var mv = Kit.Button(box, Loc.T("Mover"), Kit.Blue, 20, 140, 46, "MoverCuartel");
                Kit.PlaceTL((RectTransform)mv.transform, 460, 90, 140, 46);
                var pp = p;
                mv.Clicked += () => { CloseSheet(); game.ExitComplex(); game.BeginMove(pp); };
            }
        }

        /// <summary>Icono fotografiado de una pieza de Tripo (o el icono dibujado si falta).</summary>
        public Sprite TripoIcon(string file, string fallback)
        {
            Sprite s;
            if (cxIcons.TryGetValue(file, out s) && s != null) return s;
            Material mat;
            var mesh = IslandArt.TripoModel(file, out mat);
            s = mesh != null ? IslandStage.I.RenderIcon(mesh, new[] { mat }, 256) : Icons.Get(fallback);
            cxIcons[file] = s;
            return s;
        }
    }
}
