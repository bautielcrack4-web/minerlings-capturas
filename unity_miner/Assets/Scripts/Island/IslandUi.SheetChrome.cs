using Mineros.Audio;
using Mineros.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mineros.IslandView
{
    /// <summary>
    /// Toda hoja se cierra como en un juego profesional (pedido del creador: "para salir tenes que tocar encima"):
    /// boton ✕ arriba a la derecha, manija arriba y deslizar hacia abajo, y el boton "atras" de Android. Tocar afuera
    /// sigue funcionando. Las hojas trabadas (elegir carta del barco) no muestran la ✕.
    /// </summary>
    public sealed partial class IslandUi
    {
        GameObject sheetCloseBtn;

        void SheetChrome(RectTransform fr, bool center)
        {
            // ✕
            var b = Kit.RoundImg(fr, 32, new Color(0.36f, 0.27f, 0.2f, 0.92f), "Cerrar");
            b.raycastTarget = true;
            var rt = b.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(64, 64);
            rt.anchoredPosition = new Vector2(-34f, 44f);   // por encima del borde: no pisa botones de la hoja ("Mover")
            var ic = Kit.Icon(rt, "close", 34);
            var irt = (RectTransform)ic.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            var btn = b.gameObject.AddComponent<Btn>();
            btn.Clicked += () => { if (!sheetLocked) CloseSheet(); };
            sheetCloseBtn = b.gameObject;
            if (center) return;
            // manija y deslizar hacia abajo
            var handle = Kit.RoundImg(fr, 5, new Color(0.45f, 0.36f, 0.28f, 0.35f), "Manija");
            var hrt = handle.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(96, 10);
            hrt.anchoredPosition = new Vector2(0f, -12f);
            var sw = fr.gameObject.AddComponent<SheetSwipe>();
            sw.Ui = this;
        }

        void UpdateSheetChrome()
        {
            if (sheetCloseBtn != null) { bool on = !sheetLocked; if (sheetCloseBtn.activeSelf != on) sheetCloseBtn.SetActive(on); }
            // "atras" de Android = Escape
            if (sheet != null && !sheetLocked && Input.GetKeyDown(KeyCode.Escape)) CloseSheet();
        }

        internal bool SheetLockedNow { get { return sheetLocked; } }
        internal void CloseFromSwipe() { if (!sheetLocked) CloseSheet(); }
    }

    /// <summary>Arrastrar la hoja hacia abajo la cierra (mas de 140 px o un tiron rapido); si no, vuelve con rebote.</summary>
    public sealed class SheetSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public IslandUi Ui;
        Vector2 basePos;
        float pulled, lastDy, lastT;

        public void OnBeginDrag(PointerEventData e)
        {
            basePos = ((RectTransform)transform).anchoredPosition;
            pulled = 0f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Ui == null || Ui.SheetLockedNow) return;
            var canvas = GetComponentInParent<Canvas>();
            float k = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            lastDy = e.delta.y / Mathf.Max(k, 0.01f);
            lastT = Time.unscaledTime;
            pulled = Mathf.Min(0f, pulled + lastDy);
            ((RectTransform)transform).anchoredPosition = basePos + new Vector2(0f, pulled);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (Ui == null) return;
            var rt = (RectTransform)transform;
            bool flick = lastDy < -18f && Time.unscaledTime - lastT < 0.1f;
            if (!Ui.SheetLockedNow && (pulled < -140f || flick)) { Ui.CloseFromSwipe(); return; }
            Vector2 from = rt.anchoredPosition;
            Tw.To(rt, "vuelve", 0.25f, Ease.OutBack, u => rt.anchoredPosition = Vector2.Lerp(from, basePos, u));
        }
    }
}
