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
    /// Interfaz de colocar/mover edificios: huella en el suelo (verde si entra, roja si no), botones ✓ y ✗ que siguen
    /// al edificio y una pista corta abajo. Mientras se coloca, el resto de los indicadores del mundo se esconde.
    /// </summary>
    public sealed partial class IslandUi
    {
        RectTransform placeFoot, placeBtns, placeHint, placeGhost;
        Image placeFill, placeRing;
        Btn placeOk, placeNo, placeRot;
        Text placeDelta;
        float placeDeniedT = -9f;

        public bool PlacingUi { get { return game != null && game.Placing; } }

        readonly List<RectTransform> partnerDots = new List<RectTransform>();
        readonly System.Collections.Generic.List<Image> roomMarks = new System.Collections.Generic.List<Image>();

        public void PlaceStarted(bool isNew, bool room = false)
        {
            CloseSheet();
            if (placeFoot == null) BuildPlaceUi();
            placeFoot.gameObject.SetActive(true);
            placeBtns.gameObject.SetActive(true);
            placeHint.gameObject.SetActive(true);
            placeHint.GetComponentInChildren<Text>().text = room ? Loc.T("Arrastralo: se imanta solo") : isNew ? Loc.T("Arrastralo a donde quieras") : Loc.T("Movelo a donde quieras");
            if (placeRot != null) placeRot.gameObject.SetActive(false);
            if (placeDelta != null) placeDelta.text = "";
            Tw.Pop(placeBtns, 1.3f);
        }

        public void PlaceEnded()
        {
            if (placeFoot == null) return;
            placeFoot.gameObject.SetActive(false);
            placeBtns.gameObject.SetActive(false);
            placeHint.gameObject.SetActive(false);
            placeGhost.gameObject.SetActive(false);
            foreach (var m in roomMarks) m.gameObject.SetActive(false);
            foreach (var d in partnerDots) d.gameObject.SetActive(false);
        }

        public void PlaceDenied()
        {
            placeDeniedT = Time.unscaledTime;
            NoMoney(placeOk);
            Juice.Vibrate(12);
        }

        void BuildPlaceUi()
        {
            placeFoot = Kit.New("Huella", worldLayer);
            placeFoot.SetAsFirstSibling();
            placeFill = Kit.Img(placeFoot, Icons.Dot(), new Color(0.3f, 0.9f, 0.4f, 0.35f), "Relleno");   // disco lleno
            Kit.Stretch(placeFill.rectTransform);
            placeRing = Kit.Img(placeFoot, Icons.Ring(6f), Color.white, "Borde");
            Kit.Stretch(placeRing.rectTransform);

            // anillo tenue donde estaba (al mover): se ve de donde salio
            var gh = Kit.Img(worldLayer, Icons.Ring(4f), new Color(1f, 1f, 1f, 0.55f), "LugarOriginal");
            placeGhost = gh.rectTransform;
            placeGhost.SetAsFirstSibling();
            placeGhost.gameObject.SetActive(false);
            placeBtns = Kit.New("Botones", worldLayer);
            placeBtns.sizeDelta = new Vector2(200, 80);
            placeNo = RoundBtn(placeBtns, Kit.Red, "close", -56f);
            placeOk = RoundBtn(placeBtns, Kit.Green, "check", 56f);
            placeOk.Clicked += () => game.ConfirmPlace();
            placeNo.Clicked += () => game.CancelPlace();
            placeRot = RoundBtn(placeBtns, Kit.Purple, "rebirth", 0f);
            ((RectTransform)placeRot.transform).anchoredPosition = new Vector2(0f, 86f);
            placeRot.Clicked += () => game.RotatePlacing();
            placeRot.gameObject.SetActive(false);
            placeDelta = Kit.Label(placeBtns, "", 30, Color.white, 6, true, TextAnchor.MiddleCenter, "Ganancia");
            placeDelta.rectTransform.sizeDelta = new Vector2(260, 44);
            placeDelta.rectTransform.anchoredPosition = new Vector2(0f, -70f);

            placeHint = Glass(hudLayer, 330, 48, "PistaColocar");
            Kit.Place(placeHint, 0.5f, 1f, -165f, -160f, 330, 48);
            var l = Kit.Label(placeHint, "", 21, Color.white, 0, true, TextAnchor.MiddleCenter);
            Kit.Stretch(l.rectTransform);
        }

        Btn RoundBtn(Transform parent, Color col, string icon, float x)
        {
            var b = Kit.HitArea(parent, 76, 76, icon);
            ((RectTransform)b.transform).anchoredPosition = new Vector2(x, 0f);
            var sh = Kit.RoundImg(b.transform, 34, new Color(0f, 0f, 0f, 0.2f), "Sombra");
            Kit.Place(sh.rectTransform, 0.5f, 0.5f, -34f, -30f, 68, 68);
            var d = Kit.RoundImg(b.transform, 34, col, "Disco");
            Kit.Place(d.rectTransform, 0.5f, 0.5f, -34f, -34f, 68, 68);
            var ic = Kit.Img(b.transform, Icons.Get(icon), Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.Place(ic.rectTransform, 0.5f, 0.5f, -19f, -19f, 38, 38);
            return b;
        }

        void UpdatePlaceUi()
        {
            if (placeFoot == null || !game.Placing) return;
            Vector3 c = game.PlacePos;
            float R = game.PlaceRadius + 0.2f;
            Vector2 a = ToCanvas(c + new Vector3(R, 0f, 0f)), b = ToCanvas(c - new Vector3(R, 0f, 0f));
            Vector2 d = ToCanvas(c + new Vector3(0f, 0f, R)), e = ToCanvas(c - new Vector3(0f, 0f, R));
            Vector2 cc = ToCanvas(c);
            float w = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(d.x - e.x));
            float h = Mathf.Max(Mathf.Abs(a.y - b.y), Mathf.Abs(d.y - e.y));
            placeFoot.sizeDelta = new Vector2(w, h) * 1.15f;
            placeFoot.anchoredPosition = Vector2.Lerp(placeFoot.anchoredPosition, new Vector2(cc.x, -cc.y), 1f - Mathf.Exp(-Time.unscaledDeltaTime * 22f));
            bool ok = game.PlaceValid;
            float pulse = 0.3f + 0.08f * Mathf.Sin(Time.time * 5f);
            placeFill.color = ok ? new Color(0.35f, 0.95f, 0.45f, pulse) : new Color(1f, 0.25f, 0.25f, pulse + 0.05f);
            placeRing.color = ok ? new Color(0.45f, 1f, 0.55f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
            // ✓ y ✗ debajo de la huella (si no entra en pantalla, arriba)
            float y = -cc.y - h * 0.5f - 64f;
            if (y < -Kit.CanvasSize.y * 0.5f + 200f) y = -cc.y + h * 0.5f + 150f;
            float hx = Kit.CanvasSize.x * 0.5f - 110f;
            placeBtns.anchoredPosition = new Vector2(Mathf.Clamp(cc.x, -hx, hx), y);
            placeOk.transform.localScale = Vector3.one * (ok ? 1f : 0.85f);
            var okImg = placeOk.transform.Find("Disco").GetComponent<Image>();
            okImg.color = ok ? Kit.Green : new Color(0.6f, 0.62f, 0.6f);
            UpdateSlotMarks();
            var orig = game.PlaceOrigin;
            bool showG = orig.HasValue && (new Vector3(orig.Value.x - c.x, 0f, orig.Value.z - c.z)).sqrMagnitude > 1f;
            if (placeGhost.gameObject.activeSelf != showG) placeGhost.gameObject.SetActive(showG);
            if (showG)
            {
                Vector2 oc = ToCanvas(orig.Value);
                placeGhost.anchoredPosition = new Vector2(oc.x, -oc.y);
                placeGhost.sizeDelta = new Vector2(w, h);
            }
        }

        /// <summary>Celdas libres del Complejo para el modulo que se arrastra: anillos que laten; la elegida, verde.</summary>
        void UpdateSlotMarks()
        {
            bool mod = game.PlacingMod;
            List<int[]> cells = null;
            if (mod)
            {
                cells = Isl.FreeCells((ModKind)game.PlaceModKind, null);
                cells.RemoveAll(c => c[2] != game.ViewFloor);
            }
            int n = cells == null ? 0 : cells.Count;
            while (roomMarks.Count < n)
            {
                var im = Kit.Img(worldLayer, Icons.Ring(5f), Color.white, "Encastre");
                im.rectTransform.SetAsFirstSibling();
                roomMarks.Add(im);
            }
            for (int i = 0; i < roomMarks.Count; i++)
            {
                var im = roomMarks[i];
                bool on = i < n;
                if (im.gameObject.activeSelf != on) im.gameObject.SetActive(on);
                if (!on) continue;
                Vector3 c = game.CellWorld(cells[i][0], cells[i][1], cells[i][2]);
                bool hot = game.PlaceValid && (new Vector2(c.x - game.PlacePos.x, c.z - game.PlacePos.z)).sqrMagnitude < 0.05f;
                Vector2 a = ToCanvas(c + new Vector3(0.9f, 0f, 0f)), b = ToCanvas(c - new Vector3(0.9f, 0f, 0f));
                Vector2 d = ToCanvas(c + new Vector3(0f, 0f, 0.9f)), e = ToCanvas(c - new Vector3(0f, 0f, 0.9f));
                Vector2 cc = ToCanvas(c);
                float w = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(d.x - e.x)), h = Mathf.Max(Mathf.Abs(a.y - b.y), Mathf.Abs(d.y - e.y));
                float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 6f + i);
                im.rectTransform.anchoredPosition = new Vector2(cc.x, -cc.y);
                im.rectTransform.sizeDelta = new Vector2(w, h) * (hot ? 1.1f : pulse);
                im.color = hot ? new Color(0.45f, 1f, 0.55f, 0f) : new Color(1f, 1f, 1f, 0.75f);
            }
            // vecinos con los que se conectaria: un "+" verde que late arriba de cada uno
            var partners = mod && game.PlaceValid ? game.PlacePartners : null;
            int pn = partners == null ? 0 : partners.Count, pk = 0;
            for (int i = 0; i < pn; i++)
            {
                var pm = Isl.ModById(partners[i]);
                if (pm == null || pm.F > game.ViewFloor) continue;
                while (partnerDots.Count <= pk) partnerDots.Add(Dot(worldLayer, Icons.Get("check"), Kit.Green, 44, "Socio"));
                var dt = partnerDots[pk++];
                if (!dt.gameObject.activeSelf) { dt.gameObject.SetActive(true); Tw.Pop(dt, 1.3f); }
                Vector2 pc = ToCanvas(game.ModWorld(pm) + Vector3.up * 1.6f);
                dt.anchoredPosition = new Vector2(pc.x, -pc.y + Mathf.Sin(Time.time * 6f + i) * 5f);
            }
            for (int i = pk; i < partnerDots.Count; i++) if (partnerDots[i].gameObject.activeSelf) partnerDots[i].gameObject.SetActive(false);
            if (placeRot != null)
            {
                bool rot = mod && game.PlaceRotations > 1;
                if (placeRot.gameObject.activeSelf != rot) { placeRot.gameObject.SetActive(rot); if (rot) Tw.Pop(placeRot.transform, 1.2f); }
            }
            if (placeDelta != null)
            {
                double dv = game.PlaceDelta;
                string t = mod && game.PlaceValid && System.Math.Abs(dv) >= 0.5 ? (dv > 0 ? "+" : "") + Mathf.RoundToInt((float)dv) + "%" : "";
                if (placeDelta.text != t) { placeDelta.text = t; placeDelta.color = dv >= 0 ? new Color(0.55f, 1f, 0.5f) : new Color(1f, 0.55f, 0.5f); if (t != "") Tw.Pop(placeDelta.transform, 1.25f); }
            }
        }
    }
}
