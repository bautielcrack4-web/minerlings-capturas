using System;
using Mineros.Core;
using Mineros.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>Capta toques sobre una imagen (cerrar al tocar el fondo oscuro).</summary>
    public sealed class ClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;
        public void OnPointerClick(PointerEventData e) { if (Clicked != null) Clicked(); }
    }

    /// <summary>
    /// Ventana modal con fondo oscurecido (bloquea toques al mundo), marco panel_brown, cinta de titulo y boton de cerrar.
    /// Se abre con BasePanel.Open&lt;T&gt;. Las subclases arman su contenido en Build() dentro de Body (VerticalLayoutGroup).
    /// </summary>
    public abstract class BasePanel : MonoBehaviour
    {
        public event Action Closed;

        protected GameManager gm;
        protected GameState G { get { return gm.G; } }
        public RectTransform Frame { get; private set; }
        public RectTransform Body { get; private set; }
        public Vector2 FrameSize = new Vector2(660, 900);
        public string TitleText = "";
        public Color RibbonCol = Kit.Blue;
        public bool Closing { get; private set; }
        float fit = 1f;
        CanvasGroup group;

        /// <summary>Fija titulo/tamaño/color de cinta (se llama antes de construir).</summary>
        protected virtual void Configure() { }
        protected abstract void Build();
        public virtual void Refresh() { }
        protected virtual void OnTick(float dt) { }

        /// <summary>Crea y abre el panel como hijo de `parent` (estirado a pantalla completa).</summary>
        public static T Open<T>(Transform parent, GameManager manager, Action<T> pre = null) where T : BasePanel
        {
            RectTransform rt = Kit.New(typeof(T).Name, parent);
            Kit.Stretch(rt);
            T p = rt.gameObject.AddComponent<T>();
            p.gm = manager;
            if (pre != null) pre(p);
            p.Setup();
            return p;
        }

        void Setup()
        {
            Configure();
            group = Kit.Group(gameObject);
            RectTransform rt = (RectTransform)transform;
            Image dim = Kit.Tint(rt, new Color(0, 0, 0, 0.55f), "Dim");
            dim.raycastTarget = true;
            Kit.Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<ClickCatcher>().Clicked = Close;

            Vector2 cs = Kit.CanvasSize;
            fit = Mathf.Min(1f, (cs.y - 70f) / (FrameSize.y + 40f), (cs.x - 8f) / FrameSize.x);
            Image frame = Kit.Box9(rt, "panel_brown", new Vector4(22, 22, 22, 22), Color.white, "Frame", true);
            Frame = frame.rectTransform;
            Kit.Place(Frame, 0.5f, 0.5f, -FrameSize.x * 0.5f, -FrameSize.y * 0.5f + 20f, FrameSize.x, FrameSize.y);
            bool titled = TitleText != "";
            Body = Kit.New("Body", Frame);
            Kit.PlaceTL(Body, 22, titled ? 64 : 22, FrameSize.x - 44, FrameSize.y - (titled ? 86 : 44));
            Kit.VBox(Body, 12);
            if (titled)
            {
                Image rib = Kit.Box9(Frame, "btn_" + Kit.KCol(RibbonCol), new Vector4(12, 12, 12, 16),
                    Kit.KCol(RibbonCol) == "grey" ? RibbonCol : Color.white, "Ribbon");
                float rw = Mathf.Min(FrameSize.x - 80f, 440f);
                Kit.PlaceTL(rib.rectTransform, (FrameSize.x - rw) * 0.5f, -34, rw, 66);
                Text t = Kit.Label(rib.transform, TitleText, 34, Color.white, 9, true, TextAnchor.MiddleCenter);
                Kit.Stretch(t.rectTransform, 0, 0, 0, 6);
            }
            Btn x = Kit.SqButton(Frame, Kit.Red, 60, "Close");
            Kit.PlaceTL(x.GetComponent<RectTransform>(), FrameSize.x - 46, -26, 60, 60);
            Image xi = Kit.KIcon(x.transform, "cross", 34);
            Kit.PlaceTL(xi.rectTransform, 13, 10, 34, 34);
            x.Clicked += Close;
            Build();
            Frame.localScale = new Vector3(0.7f * fit, 0.7f * fit, 1f);
            group.alpha = 0f;
            Tw.To(Frame, "open", 0.28f, Ease.OutBack, u =>
            {
                float s = Mathf.LerpUnclamped(0.7f, 1f, u) * fit;
                Frame.localScale = new Vector3(s, s, 1f);
            });
            Tw.Alpha(group, 1f, 0.15f);
            gm.G.Changed += OnChanged;
        }

        void OnDestroy()
        {
            if (gm != null && gm.G != null) gm.G.Changed -= OnChanged;
        }

        void OnChanged()
        {
            if (!Closing) Refresh();
        }

        void Update()
        {
            if (!Closing) OnTick(Time.unscaledDeltaTime);
        }

        public virtual void Close()
        {
            if (Closing) return;
            Closing = true;
            if (Closed != null) Closed();
            group.interactable = false;
            group.blocksRaycasts = false;
            Tw.To(Frame, "open", 0.12f, Ease.Linear, u =>
            {
                float s = Mathf.Lerp(1f, 0.85f, u) * fit;
                Frame.localScale = new Vector3(s, s, 1f);
            });
            Tw.Alpha(group, 0f, 0.12f, 0f, () => { if (this != null) Destroy(gameObject); });
        }

        // ---------------------------------------------------------------- ayudas de contenido
        public sealed class RowParts
        {
            public RectTransform Panel;
            public Btn Button;
            public Text Title, Desc;
            public IconView Icon;
        }

        /// <summary>Fila tipo tarjeta: icono, titulo, descripcion y un boton a la derecha.</summary>
        public RowParts Row(Transform parent, string iconKind, string title, string desc, string btnText, Color bg,
            Color btnCol, float h = 112f)
        {
            RowParts r = new RowParts();
            Image p = Kit.Box9(parent, "card", new Vector4(12, 12, 12, 16), bg, "Row");
            p.rectTransform.sizeDelta = new Vector2(100, h);
            Kit.Item(p.rectTransform, -1, h);
            r.Panel = p.rectTransform;
            r.Icon = Kit.Icon(p.transform, iconKind, 66);
            Kit.PlaceTL((RectTransform)r.Icon.transform, 12, (h - 66) * 0.5f, 66, 66);
            r.Title = Kit.LabelAt(p.transform, title, 26, Color.white, 7, true, 90, 10, 330, 34);
            r.Desc = Kit.LabelAt(p.transform, desc, 20, Kit.Brown, 0, false, 90, 44, 330, h - 50);
            r.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            float bodyW = FrameSize.x - 44f;
            r.Button = Kit.Button(p.transform, btnText, btnCol, 22, 150, 64);
            Kit.PlaceTL(r.Button.GetComponent<RectTransform>(), bodyW - 166f, (h - 64f) * 0.5f, 150, 64);
            return r;
        }

        /// <summary>Texto simple que ocupa el ancho del cuerpo (con ajuste de linea).</summary>
        public Text BodyText(string text, int size, Color col, TextAnchor anchor = TextAnchor.UpperCenter, float minH = -1f,
            int outline = 0, bool title = false)
        {
            Text t = Kit.Label(Body, text, size, col, outline, title, anchor);
            Kit.Wrap(t);
            if (minH >= 0) Kit.Item(t, -1, minH);
            return t;
        }

        /// <summary>Boton grande del cuerpo (alto fijo).</summary>
        public Btn BodyButton(string text, Color col, int fsize, float h)
        {
            Btn b = Kit.Button(Body, text, col, fsize, 100, h);
            Kit.Item(b, -1, h);
            return b;
        }

        /// <summary>Fila centrada con un icono y un texto (monedas, gemas...). El texto debe ser fijo.</summary>
        public RectTransform IconTextRow(Transform parent, string icon, float iconSize, string text, int fsize, Color col, int outline, float h)
        {
            RectTransform row = Kit.New("IconText", parent);
            Kit.Item(row, -1, h);
            float bodyW = FrameSize.x - 44f;
            Text l = Kit.Label(row, text, fsize, col, outline, true, TextAnchor.MiddleLeft);
            float tw = l.preferredWidth;
            float total = iconSize + 8f + tw;
            float x0 = (bodyW - total) * 0.5f;
            IconView ic = Kit.Icon(row, icon, iconSize);
            Kit.PlaceTL((RectTransform)ic.transform, x0, (h - iconSize) * 0.5f, iconSize, iconSize);
            Kit.PlaceTL(l.rectTransform, x0 + iconSize + 8f, 0, tw + 4f, h);
            return row;
        }

        public RectTransform Spacer(float flexH = 1f)
        {
            RectTransform sp = Kit.New("Spacer", Body);
            Kit.Item(sp, -1, 0, -1, flexH);
            return sp;
        }

        public static void ToastMsg(string s) { HudController.Toast(s); }
    }
}
