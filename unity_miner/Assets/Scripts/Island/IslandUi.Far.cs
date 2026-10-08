using System.Collections.Generic;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.IslandView
{
    /// <summary>
    /// Interfaz de la isla lejana y del reloj (pedido del dueño, 8-oct):
    /// - globito sobre la niebla: candado + Muelle si falta el Muelle; "?" que late si ya se puede descubrir;
    /// - barra de vida sobre cada roca exclusiva (se ve desde lejos) y una bandera roja en la que sigue la cuadrilla;
    /// - reloj chico arriba (sol o luna + hora), para saber cuando sale la balsa (6:00) y cuando vuelve (19:00).
    /// </summary>
    public sealed partial class IslandUi
    {
        RectTransform fogBubble;
        IconView fogIcon;
        Image fogDock;
        Text fogQ;
        readonly Dictionary<int, RectTransform> farBars = new Dictionary<int, RectTransform>();
        readonly Dictionary<int, RectTransform> farFills = new Dictionary<int, RectTransform>();
        RectTransform farFlag;

        void UpdateFarUi()
        {
            // --- globito de la niebla
            bool showFog = !Isl.FarFound && Isl.TutDone && sheet == null;
            if (fogBubble == null)
            {
                fogBubble = Dot(worldLayer, null, new Color(0.25f, 0.6f, 0.95f), 84, "Niebla");
                fogIcon = Kit.Icon(fogBubble, "lock", 40);
                Kit.Place((RectTransform)fogIcon.transform, 0.5f, 0.5f, -40f, -24f, 40, 40);
                fogDock = Kit.Img(fogBubble, null, Color.white, "Muelle");
                fogDock.preserveAspect = true;
                Kit.Place(fogDock.rectTransform, 0.5f, 0.5f, -6f, -40f, 50, 50);
                fogQ = Kit.Label(fogBubble, "?", 54, Color.white, 6, true, TextAnchor.MiddleCenter);
                Kit.Stretch(fogQ.rectTransform, 0, 0, 0, 6);
                fogBubble.gameObject.SetActive(false);
            }
            if (fogBubble.gameObject.activeSelf != showFog) fogBubble.gameObject.SetActive(showFog);
            if (showFog)
            {
                bool can = Isl.HasDock;
                fogQ.gameObject.SetActive(can);
                fogIcon.gameObject.SetActive(!can);
                fogDock.gameObject.SetActive(!can);
                if (!can && fogDock.sprite == null) fogDock.sprite = IslandStage.I.BuildingIcon(BKind.Dock, 1);
                Vector2 c = ToCanvas(game.FarCenter + Vector3.up * 4.5f);
                fogBubble.anchoredPosition = new Vector2(c.x, -c.y + Mathf.Abs(Mathf.Sin(Time.time * 2.6f)) * (can ? 14f : 5f));
                fogBubble.localScale = Vector3.one * (can ? 1f + Mathf.Sin(Time.time * 5f) * 0.06f : 1f);
            }

            // --- barras de vida de las rocas exclusivas (y la bandera de la roca que sigue la cuadrilla)
            Ore focus = null;
            foreach (var o in Isl.OreList)
            {
                if (!o.Far) continue;
                RectTransform bar;
                if (!farBars.TryGetValue(o.Id, out bar))
                {
                    bar = Glass(worldLayer, 150, 24, "Vida");
                    var fillBg = Kit.RoundImg(bar, 9, new Color(0f, 0f, 0f, 0.35f), "Fondo");
                    Kit.Stretch(fillBg.rectTransform, 5, 5, 5, 5);
                    var fill = Kit.RoundImg(bar, 8, IslandArt.OreCol[o.Kind], "Relleno").rectTransform;
                    fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, 0.5f);
                    fill.offsetMin = new Vector2(5, 5); fill.offsetMax = new Vector2(5, -5);
                    farBars[o.Id] = bar; farFills[o.Id] = fill;
                }
                bool vis = !o.Dead && Isl.FarFound;
                if (bar.gameObject.activeSelf != vis) bar.gameObject.SetActive(vis);
                if (!vis) continue;
                float sz = Island.Ores[o.Kind].Size * IslandGame.OreScale(o);
                Vector2 c = ToCanvas(new Vector3(o.X, sz * 1.25f + 0.6f, o.Z));
                bar.anchoredPosition = new Vector2(c.x, -c.y);
                float f = Mathf.Clamp01((float)(o.Hp / Mathf.Max(1f, (float)o.MaxHp)));
                farFills[o.Id].sizeDelta = new Vector2(Mathf.Max(8f, 140f * f), farFills[o.Id].sizeDelta.y);
                if (o.Id == Isl.FarFocus) focus = o;
            }
            // limpiar las de rocas que ya no estan
            if (farBars.Count > 0 && Time.frameCount % 30 == 0)
            {
                var gone = new List<int>();
                foreach (var kv in farBars) if (!Isl.OreList.Exists(x => x.Id == kv.Key)) gone.Add(kv.Key);
                foreach (var id in gone) { Destroy(farBars[id].gameObject); farBars.Remove(id); farFills.Remove(id); }
            }
            if (farFlag == null)
            {
                farFlag = (RectTransform)Kit.Icon(worldLayer, "flag", 52, "Fijada").transform;
                farFlag.gameObject.SetActive(false);
            }
            if (farFlag.gameObject.activeSelf != (focus != null)) farFlag.gameObject.SetActive(focus != null);
            if (focus != null)
            {
                float sz = Island.Ores[focus.Kind].Size * IslandGame.OreScale(focus);
                Vector2 c = ToCanvas(new Vector3(focus.X, sz * 1.25f + 0.6f, focus.Z));
                farFlag.anchoredPosition = new Vector2(c.x - 98f, -c.y + 6f + Mathf.Sin(Time.time * 4f) * 3f);
            }
            UpdateClock();
        }

        // ------------------------------------------------------------ reloj
        RectTransform clockPill;
        IconView clockIcon;
        Text clockL;

        void UpdateClock()
        {
            bool show = Isl.TutDone;
            if (clockPill == null)
            {
                clockPill = Glass(hudLayer, 132, 36, "Reloj");
                Kit.Place(clockPill, 0.5f, 0f, -66f, 70f, 132, 36);
                clockIcon = Kit.Icon(clockPill, "sun", 30);
                Kit.PlaceTL((RectTransform)clockIcon.transform, 6, 3, 30, 30);
                clockL = Kit.LabelAt(clockPill, "", 21, Color.white, 0, true, 36, 0, 90, 36, TextAnchor.MiddleCenter);
                clockPill.gameObject.SetActive(false);
            }
            if (clockPill.gameObject.activeSelf != show) clockPill.gameObject.SetActive(show);
            if (!show) return;
            float h = Isl.Hour;
            int hh = (int)h, mm = (int)((h - hh) * 60f) / 10 * 10;   // de a 10 minutos: no titila
            int h12 = hh % 12 == 0 ? 12 : hh % 12;
            string t = h12 + ":" + mm.ToString("00") + (hh < 12 ? " AM" : " PM");
            if (clockL.text != t) clockL.text = t;
            bool day = h >= 6f && h < 19f;
            clockIcon.SetKind(day ? "sun" : "moon");
        }
    }
}
