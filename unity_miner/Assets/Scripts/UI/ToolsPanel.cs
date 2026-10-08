using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>
    /// Celda de herramienta: ranura de equipo (grande) o de inventario. Se arrastra (IBeginDragHandler/IDragHandler/IEndDragHandler)
    /// y recibe soltados (IDropHandler) para equipar, intercambiar y fusionar. En modo `Preview` solo se muestra (coleccion, ghost).
    /// </summary>
    public sealed class ToolCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public ToolsPanel Owner;
        public SlotRef Src;
        public bool Big;
        public bool Preview;
        public int PrevK, PrevR;
        public bool Interactive = true;

        Image bg;
        Text rank, nameL, powerT, speedT;
        Image toolImg;
        RectTransform cross, statRow1, statRow2;
        IconView powerIc, speedIc;
        Btn xBtn;
        float w, h;
        bool dragging;
        int lastK = -2, lastR = -2;
        string lastPower = "", lastSpeed = "";

        public static ToolCell Create(Transform parent, float width, float height, bool big, bool preview, bool interactive = true)
        {
            RectTransform rt = Kit.New("ToolCell", parent);
            rt.sizeDelta = new Vector2(width, height);
            ToolCell c = rt.gameObject.AddComponent<ToolCell>();
            c.w = width;
            c.h = height;
            c.Big = big;
            c.Preview = preview;
            c.Interactive = interactive;
            c.Build();
            return c;
        }

        void Build()
        {
            bg = Kit.Box9(transform, "panel_brown", new Vector4(16, 16, 16, 16), Color.white, "Bg", Interactive);
            Kit.Stretch(bg.rectTransform, 2, 2, 2, 2);
            // cruz de hueco vacio
            cross = Kit.New("Cross", transform);
            Kit.PlaceTL(cross, w * 0.5f - 22.5f, h * 0.5f - 3.5f, 45, 7);
            for (int i = 0; i < 2; i++)
            {
                Image bar = Kit.Tint(cross, new Color(1, 1, 1, 0.5f), "Bar" + i);
                Kit.Stretch(bar.rectTransform);
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45f : -45f);
            }
            cross.gameObject.SetActive(false);
            rank = Kit.LabelAt(transform, "", 26, Color.white, 7, true, 12, 4, 80, 34);
            toolImg = Kit.Img(transform, null, Color.white, "Tool", false);
            toolImg.preserveAspect = true;
            toolImg.enabled = false;
            float fs = Big ? 24 : 20;
            nameL = Kit.LabelAt(transform, "", (int)fs, Color.white, 6, true, 0, h * (Big ? 0.66f : 0.86f) - fs, w, fs * 1.4f, TextAnchor.UpperCenter);
            if (Big)
            {
                float y = h * 0.72f;
                statRow1 = StatRow(y, "power", out powerIc, out powerT);
                statRow2 = StatRow(y + 32f, "speed", out speedIc, out speedT);
                xBtn = Kit.HitArea(transform, 42, 42, "Unequip");
                Kit.PlaceTL(xBtn.GetComponent<RectTransform>(), w - 38, -6, 42, 42);
                IconView xi = Kit.Icon(xBtn.transform, "close", 42);
                Kit.PlaceTL((RectTransform)xi.transform, 0, 0, 42, 42);
                xBtn.Clicked += () => { if (Owner != null) Owner.UnequipSlot(Src.I); };
                xBtn.gameObject.SetActive(false);
            }
            RefreshCell(true);
        }

        RectTransform StatRow(float y, string icon, out IconView ic, out Text t)
        {
            RectTransform row = Kit.New("Stat", transform);
            Kit.PlaceTL(row, 0, y, w, 26);
            ic = Kit.Icon(row, icon, 26);
            Kit.PlaceTL((RectTransform)ic.transform, 14, 0, 26, 26);
            RectTransform chip = Kit.OutBox(row, 8, 2, 0, Icons.H("6a5848"), Kit.Out, "Chip");
            Kit.PlaceTL(chip, 44 - 2, -2, w - 60 + 4, 30);
            t = Kit.Label(chip, "", 20, Color.white, 0, true, TextAnchor.MiddleCenter);
            Kit.Stretch(t.rectTransform);
            return row;
        }

        public Tool Current()
        {
            if (Preview) return new Tool(PrevK, PrevR);
            if (Owner == null || Owner.GameState == null) return null;
            return Src.C == SlotKind.Inv ? Owner.GameState.Inv[Src.I] : Owner.GameState.Equip[Src.I];
        }

        public void RefreshCell(bool force = false)
        {
            Tool tl = Current();
            int k = tl == null ? -1 : tl.K;
            int r = tl == null ? -1 : tl.R;
            bool changed = force || k != lastK || r != lastR;
            if (tl != null && Big && Owner != null && Owner.GameState != null)
            {
                string p = BigNum.Fmt(Owner.GameState.ToolPower(tl)) + "%";
                string s = Owner.GameState.ToolBaseInterval(tl).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "s";
                if (p != lastPower) { lastPower = p; powerT.text = p; }
                if (s != lastSpeed) { lastSpeed = s; speedT.text = s; }
            }
            if (!changed) return;
            lastK = k;
            lastR = r;
            bool empty = tl == null;
            string tex = empty ? "panel_brown_dark" : "panel_brown";
            bg.sprite = Kit.Tex9(tex, new Vector4(16, 16, 16, 16));
            bg.color = (empty && Big) ? new Color(0.85f, 0.8f, 0.75f, 1f) : Color.white;
            cross.gameObject.SetActive(empty && Big);
            rank.gameObject.SetActive(!empty);
            toolImg.gameObject.SetActive(!empty);
            nameL.gameObject.SetActive(!empty);
            if (Big)
            {
                statRow1.gameObject.SetActive(!empty);
                statRow2.gameObject.SetActive(!empty);
                xBtn.gameObject.SetActive(!empty && !Preview);
            }
            if (empty) return;
            rank.text = Content.Ranks[tl.R];
            rank.color = Icons.RankCol(tl.R);
            toolImg.sprite = Icons.Tool(tl.K, tl.R);
            toolImg.enabled = true;
            float sc = Big ? 1.05f : 0.8f;
            Vector2 ic = new Vector2(w * 0.5f, h * (Big ? 0.38f : 0.47f));
            Vector2 origin = ic + new Vector2(-4f, 26f) * sc;
            RectTransform tr = toolImg.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0, 1);
            tr.pivot = new Vector2(Icons.ToolOrigin.x / Icons.ToolCanvas, 1f - Icons.ToolOrigin.y / Icons.ToolCanvas);
            tr.sizeDelta = new Vector2(Icons.ToolCanvas * sc, Icons.ToolCanvas * sc);
            tr.anchoredPosition = new Vector2(origin.x, -origin.y);
            nameL.text = Content.ToolNames[tl.K];
        }

        // ---------------------------------------------------------------- arrastrar y soltar
        public void OnBeginDrag(PointerEventData e)
        {
            dragging = false;
            if (Preview || !Interactive || Owner == null) return;
            Tool tl = Current();
            if (tl == null) return;
            dragging = true;
            Owner.BeginGhost(tl);
            Owner.MoveGhost(e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            if (dragging) Owner.MoveGhost(e.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (dragging) Owner.EndGhost();
            dragging = false;
        }

        public void OnDrop(PointerEventData e)
        {
            if (Preview || !Interactive || Owner == null || e.pointerDrag == null) return;
            ToolCell from = e.pointerDrag.GetComponent<ToolCell>();
            if (from == null || !from.dragging || from == this) return;
            Owner.OnDrop(from.Src, Src);
        }

        void OnDisable()
        {
            if (dragging && Owner != null) Owner.EndGhost();
            dragging = false;
        }
    }

    // ==================================================================== Equipo
    public sealed class ToolsPanel : BasePanel
    {
        readonly List<ToolCell> equipCells = new List<ToolCell>();
        readonly List<ToolCell> invCells = new List<ToolCell>();
        Btn gachaBtn;
        Text gemLbl;
        RectTransform ghost;
        ToolCell ghostCell;

        public GameState GameState { get { return gm != null ? gm.G : null; } }

        protected override void Configure()
        {
            TitleText = "Equipo";
            FrameSize = new Vector2(680, 1180);
            RibbonCol = Kit.Orange;
        }

        protected override void Build()
        {
            RectTransform slots = Kit.New("Slots", Body);
            Kit.Item(slots, -1, 230);
            Kit.HBox(slots, 14, TextAnchor.MiddleCenter);
            for (int i = 0; i < Balance.EquipSlots; i++)
            {
                ToolCell c = ToolCell.Create(slots, 196, 230, true, false);
                c.Owner = this;
                c.Src = new SlotRef(SlotKind.Equip, i);
                Kit.Item(c, 196, 230);
                c.RefreshCell(true);
                equipCells.Add(c);
            }
            BodyText("Arrastrá una herramienta para equipar. Cada ranura es un minero.", 19, Kit.Brown, TextAnchor.UpperCenter, 26);
            Image gp = Kit.Box9(Body, "panel_brown", new Vector4(22, 22, 22, 22), new Color(0.92f, 0.88f, 0.8f, 1f), "GridPanel");
            Kit.Item(gp.rectTransform, -1, 560);
            RectTransform grid = Kit.New("Grid", gp.transform);
            Kit.PlaceTL(grid, 16, 16, 600, 536);
            GridLayoutGroup gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(141, 125);
            gl.spacing = new Vector2(12, 12);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 4;
            for (int i = 0; i < Balance.InvSize; i++)
            {
                ToolCell c = ToolCell.Create(grid, 141, 125, false, false);
                c.Owner = this;
                c.Src = new SlotRef(SlotKind.Inv, i);
                c.RefreshCell(true);
                invCells.Add(c);
            }
            BodyText("Arrastrá dos herramientas iguales para fusionarlas.", 19, Kit.Brown, TextAnchor.UpperCenter, 26);

            RectTransform bottom = Kit.New("Bottom", Body);
            Kit.Item(bottom, -1, 90);
            Kit.HBox(bottom, 18, TextAnchor.MiddleCenter);
            Btn info = Kit.Button(bottom, "Info", Kit.Gray, 22, 110, 70);
            Kit.Item(info, 110, 70);
            info.Clicked += ShowInfo;
            gachaBtn = Kit.Button(bottom, "", Kit.Orange, 28, 250, 84);
            Kit.Item(gachaBtn, 250, 84);
            Kit.LabelAt(gachaBtn.transform, "Cofre", 28, Color.white, 6, true, 22, 14, 100, 46, TextAnchor.MiddleLeft);
            IconView gi = Kit.Icon(gachaBtn.transform, "gem", 34);
            Kit.PlaceTL((RectTransform)gi.transform, 118, 20, 34, 34);
            Kit.LabelAt(gachaBtn.transform, G.GachaCost().ToString(), 28, Color.white, 6, true, 156, 14, 80, 46, TextAnchor.MiddleLeft);
            gachaBtn.Clicked += DoGacha;
            RectTransform col = Kit.New("Gems", bottom);
            Kit.Item(col, 160, 90);
            Kit.VBox(col, 4, TextAnchor.MiddleCenter);
            gemLbl = Kit.Label(col, "Gemas: 0", 26, Color.white, 7, true, TextAnchor.MiddleCenter);
            Kit.Item(gemLbl, -1, 32);
            Btn coll = Kit.Button(col, "Colección", Kit.Blue, 20, 150, 54);
            Kit.Item(coll, -1, 54);
            coll.Clicked += ShowCollection;
            Refresh();
        }

        public override void Refresh()
        {
            if (gemLbl == null) return;
            for (int i = 0; i < equipCells.Count; i++) equipCells[i].RefreshCell();
            for (int i = 0; i < invCells.Count; i++) invCells[i].RefreshCell();
            PanelUtil.SetText(gemLbl, "Gemas: " + G.Gems);
            gachaBtn.Interactable = G.Gems >= G.GachaCost();
        }

        void DoGacha()
        {
            Tool t = G.Gacha();
            if (t != null)
            {
                Sfx.Play("gacha");
                Kit.Buzz(30);
                ToastMsg("¡" + Content.ToolNames[t.K] + " rango " + Content.Ranks[t.R] + "!");
            }
        }

        public void UnequipSlot(int i) { G.Unequip(i); }

        void ShowInfo()
        {
            BasePanel.Open<InfoPanel>(transform.parent, gm);
        }

        void ShowCollection()
        {
            BasePanel.Open<CollectionPanel>(transform.parent, gm);
        }

        public void OnDrop(SlotRef src, SlotRef dst)
        {
            MoveResult r = G.MoveTool(src, dst);
            if (r == MoveResult.Merge)
            {
                Sfx.Play("upgrade");
                Kit.Buzz(30);
                ToastMsg("¡Fusión!");
            }
        }

        // ---------------------------------------------------------------- vista del arrastre
        public void BeginGhost(Tool t)
        {
            EndGhost();
            RectTransform rt = Kit.New("Ghost", transform);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(130, 120);
            ghost = rt;
            ghostCell = ToolCell.Create(rt, 130, 120, false, true, false);
            ghostCell.PrevK = t.K;
            ghostCell.PrevR = t.R;
            ghostCell.RefreshCell(true);
            Kit.Stretch((RectTransform)ghostCell.transform);
            CanvasGroup g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0.85f;
            g.blocksRaycasts = false;
            g.interactable = false;
            rt.SetAsLastSibling();
        }

        public void MoveGhost(Vector2 screenPos)
        {
            if (ghost == null) return;
            Vector2 local;
            Canvas cv = GetComponentInParent<Canvas>();
            Camera cam = cv != null && cv.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cv.rootCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPos, cam, out local))
                ghost.anchoredPosition = local;
        }

        public void EndGhost()
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            ghostCell = null;
        }
    }

    // ==================================================================== Info
    public sealed class InfoPanel : BasePanel
    {
        protected override void Configure()
        {
            TitleText = "Info";
            FrameSize = new Vector2(600, 620);
        }

        protected override void Build()
        {
            BodyText("Pico: golpe rápido a una roca (100% de Fuerza, cada 0.6 s).\n\nMazo: golpe lento (80%, cada 1 s) que también daña 60% a las rocas cercanas.\n\nFusionar dos iguales sube el rango: D > C > B > A > S > SS. Cada rango multiplica el daño x2.2.\n\nCada herramienta equipada es un minero más trabajando.",
                22, Kit.Brown, TextAnchor.UpperLeft);
        }
    }

    // ==================================================================== Coleccion
    public sealed class CollectionPanel : BasePanel
    {
        protected override void Configure()
        {
            TitleText = "Colección";
            FrameSize = new Vector2(640, 760);
        }

        protected override void Build()
        {
            for (int k = 0; k < 2; k++)
            {
                BodyText(Content.ToolNames[k], 28, Color.white, TextAnchor.MiddleLeft, 40, 7, true);
                RectTransform row = Kit.New("Row" + k, Body);
                Kit.Item(row, -1, 110);
                Kit.HBox(row, 8, TextAnchor.MiddleCenter);
                for (int r = 0; r < Content.Ranks.Length; r++)
                {
                    ToolCell c = ToolCell.Create(row, 92, 110, false, true, false);
                    c.PrevK = k;
                    c.PrevR = r;
                    c.RefreshCell(true);
                    Kit.Item(c, 92, 110);
                    bool known = G.Collection.Contains(k + "_" + r);
                    if (!known)
                    {
                        Image shade = Kit.RoundImg(c.transform, 16, new Color(0, 0, 0, 0.7f), "Locked");
                        Kit.Stretch(shade.rectTransform, 2, 2, 2, 2);
                    }
                }
            }
        }
    }
}
