using System;
using System.Collections.Generic;
using Mineros.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FxJuice = Mineros.Fx.Juice;

namespace Mineros.UI
{
    /// <summary>Estilo de boton: sprites 9-slice Kenney (normal/presionado) + tinte y margenes de contenido.</summary>
    public sealed class BtnSkin
    {
        public Sprite Normal, Pressed, Disabled;
        public Color Tint = Color.white;
        public Color DisabledTint = new Color(0.8f, 0.8f, 0.82f, 1f);
        public bool UsePressedTint;
        public Color PressedTint = Color.white;
        public Vector4 CmNormal = new Vector4(12, 5, 12, 11);   // izquierda, arriba, derecha, abajo (como Godot)
        public Vector4 CmPressed = new Vector4(12, 9, 12, 7);
        public Color Lip = new Color(0.23f, 0.16f, 0.12f, 1f);   // color del costado: contorno del texto del boton
    }

    /// <summary>Boton con "juice": se achica al presionar, rebota al soltar, suena "ui". Funciona con cualquier Graphic raiz.</summary>
    public sealed class Btn : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Image Bg;
        public RectTransform Content;     // contenedor del texto/icono que se corre al presionar
        public Text Label;
        public BtnSkin Skin;
        public bool Juice = true;
        public bool PlaySound = true;
        public event Action Clicked;
        public event Action Down;
        public event Action Up;
        bool interactable = true;
        bool pressed;
        Color labelColor = Color.white;
        /// <summary>Tinte extra multiplicado (atenuar el boton entero sin desactivarlo).</summary>
        public Color Modulate = Color.white;

        public bool IsPressed { get { return pressed; } }
        /// <summary>Hasta este instante (tiempo sin escala) hay un rebote en curso: no tocar la escala.</summary>
        public float QuietUntil;

        public bool Interactable
        {
            get { return interactable; }
            set
            {
                if (interactable == value) return;
                interactable = value;
                if (!value) pressed = false;
                Restyle();
            }
        }

        public void SetSkin(BtnSkin s)
        {
            Skin = s;
            Restyle();
        }

        public void SetLabelColor(Color c)
        {
            labelColor = c;
            Restyle();
        }

        public void Restyle()
        {
            if (Bg != null && Skin != null)
            {
                if (!interactable)
                {
                    Bg.sprite = Skin.Disabled != null ? Skin.Disabled : Skin.Normal;
                    Bg.color = Skin.Disabled != null ? Skin.DisabledTint : Skin.Tint * new Color(0.6f, 0.6f, 0.62f, 1f);
                }
                else
                {
                    Bg.sprite = pressed ? Skin.Pressed : Skin.Normal;
                    Bg.color = (pressed && Skin.UsePressedTint ? Skin.PressedTint : Skin.Tint) * Modulate;
                }
            }
            if (Content != null && Skin != null)
            {
                Vector4 cm = (pressed && interactable) ? Skin.CmPressed : Skin.CmNormal;
                Content.offsetMin = new Vector2(cm.x, cm.w);
                Content.offsetMax = new Vector2(-cm.z, -cm.y);
            }
            if (Label != null)
            {
                Label.color = interactable ? labelColor : new Color(labelColor.r, labelColor.g, labelColor.b, labelColor.a * 0.8f);
                // contorno del texto del color del costado del boton (mas elegante que negro)
                if (Skin != null && labelColor == Color.white)
                {
                    var so = Label.GetComponent<SoftOutline>();
                    if (so != null && so.Color != Skin.Lip) { so.Color = Skin.Lip; Label.SetVerticesDirty(); }
                }
            }
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!interactable)
            {
                // "no": tiembla dos veces, vibracion de aviso y un "bonk" bajito
                if (Juice && Bg != null)
                {
                    var rt = (RectTransform)transform;
                    Vector2 p0 = rt.anchoredPosition;
                    Tw.To(rt, "no", 0.22f, Ease.Linear, u => rt.anchoredPosition = p0 + new Vector2(Mathf.Sin(u * Mathf.PI * 4f) * 4f * (1f - u), 0f), () => rt.anchoredPosition = p0);
                    Mineros.Fx.Haptics.Warning();
                    Sfx.Play("error", -12f, 1.1f);
                }
                return;
            }
            pressed = true;
            Restyle();
            // se hunde (la cara baja sobre su labio) en el mismo cuadro del toque: vibracion suave y clic
            if (Juice)
            {
                Tw.Scale(transform, Vector3.one * 0.97f, 0.05f, Ease.OutQuad);
                Mineros.Fx.Haptics.Light();
            }
            if (PlaySound) { Sfx.Play("ui", -2f, UnityEngine.Random.Range(0.96f, 1.05f)); soundOnDown = true; }
            if (Down != null) Down();
        }

        bool soundOnDown;

        public void OnPointerUp(PointerEventData e)
        {
            bool was = pressed;
            pressed = false;
            if (interactable) Restyle();
            if (was && Juice)
            {
                Tw.Scale(transform, Vector3.one, 0.22f, Ease.OutBack);
                QuietUntil = Time.unscaledTime + 0.24f;
            }
            if (was && Up != null) Up();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable) return;
            if (PlaySound && !soundOnDown) Sfx.Play("ui");
            soundOnDown = false;
            if (Clicked != null) Clicked();
        }

        public void Click() { if (interactable && Clicked != null) Clicked(); }

        void Start() { EnsureHit(); }

        /// <summary>
        /// Sin una Graphic con raycastTarget el dedo atraviesa el boton (las cajas de Kit.OutBox no reciben toques):
        /// si no hay ninguna, se activa la del fondo (la propia o el primer hijo con Graphic).
        /// </summary>
        public void EnsureHit()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true))
                if (g.raycastTarget) return;
            var own = GetComponent<Graphic>();
            if (own != null) { own.raycastTarget = true; return; }
            foreach (var g in GetComponentsInChildren<Graphic>(true))
                if (g.gameObject != gameObject) { g.raycastTarget = true; return; }
        }

        void OnDisable()
        {
            if (pressed)
            {
                pressed = false;
                transform.localScale = Vector3.one;
                if (Up != null) Up();
            }
        }
    }

    /// <summary>Icono dibujado por codigo dentro de un contenedor de tamaño s (el lienzo se amplia 48/40).</summary>
    public sealed class IconView : MonoBehaviour
    {
        public Image Img;
        string kind;
        public string Kind { get { return kind; } }

        public void SetKind(string k)
        {
            if (k == kind) return;
            kind = k;
            Img.sprite = Icons.Get(k);
            Img.enabled = true;
        }
    }

    /// <summary>Barra tipo capsula con relleno y texto (progress_*).</summary>
    public sealed class Capsule
    {
        public RectTransform Root;
        public Image Bg, Fill;
        public Text Label;
        public float W, H;
        string kind = "";

        public void SetFillKind(string k, Color mod)
        {
            if (k != kind)
            {
                kind = k;
                Fill.sprite = Kit.Tex9("progress_" + k, new Vector4(8, 8, 8, 8));
            }
            Fill.color = mod;
        }

        public void Set(float frac, string text)
        {
            frac = Mathf.Clamp01(frac);
            Fill.gameObject.SetActive(frac > 0.001f);
            Fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(H, W * frac), H);
            Kit.SetPos(Fill.rectTransform, 0, 0);
            if (Label.text != text) Label.text = text;
        }
    }

    /// <summary>Pildora de oro/gemas: campo con icono y numero a la derecha.</summary>
    public sealed class Pill
    {
        public RectTransform Root;
        public Text Label;
        public IconView Icon;
    }

    /// <summary>Punto rojo con contador ("hay algo para reclamar YA"). Oculto con 0.</summary>
    public sealed class Badge
    {
        public RectTransform Root;
        public Text Label;
        public bool AlwaysNumber;
        int count;

        public int Count { get { return count; } }

        public void SetCount(int n)
        {
            if (n == count) return;
            count = n;
            Root.gameObject.SetActive(n > 0);
            Label.text = (n > 1 || (AlwaysNumber && n > 0)) ? Mathf.Min(n, 99).ToString() : "";
        }
    }

    public static class Kit
    {
        // ---------------------------------------------------------------- paleta (ui_kit.gd)
        public static readonly Color Out = Icons.H("3b2a1e");
        public static readonly Color Cream = Icons.H("fbf1dc");
        public static readonly Color CreamD = Icons.H("e9d6b0");
        public static readonly Color Green = Icons.H("5cc84a");
        public static readonly Color GreenD = Icons.H("3a8f2e");
        public static readonly Color Orange = Icons.H("f59a32");
        public static readonly Color OrangeD = Icons.H("c26a12");
        public static readonly Color Red = Icons.H("e5484d");
        public static readonly Color RedD = Icons.H("a82a2f");
        public static readonly Color Blue = Icons.H("4aa3f0");
        public static readonly Color BlueD = Icons.H("2a6fb0");
        public static readonly Color Purple = Icons.H("a36be8");
        public static readonly Color PurpleD = Icons.H("6f3fb3");
        public static readonly Color Gray = Icons.H("9a9590");
        public static readonly Color GrayD = Icons.H("6a655f");
        public static readonly Color Brown = Icons.H("5a4030");
        public static readonly Color Yellow = Icons.H("ffd84a");
        public static readonly Color LimeText = Icons.H("8fe35a");

        // ---------------------------------------------------------------- canvas
        public static RectTransform CanvasRT;

        public static Vector2 CanvasSize
        {
            get
            {
                if (CanvasRT == null) return new Vector2(720f, 1544f);
                Rect r = CanvasRT.rect;
                return new Vector2(r.width, r.height);
            }
        }

        static readonly Vector3[] corners = new Vector3[4];

        /// <summary>Rectangulo del elemento en unidades de canvas con origen arriba-izquierda (y hacia abajo).</summary>
        public static Rect ToCanvas(RectTransform target)
        {
            if (target == null || CanvasRT == null) return new Rect(0, 0, 0, 0);
            target.GetWorldCorners(corners);
            Rect cr = CanvasRT.rect;
            Vector3 bl = CanvasRT.InverseTransformPoint(corners[0]);
            Vector3 tr = CanvasRT.InverseTransformPoint(corners[2]);
            float x0 = bl.x - cr.xMin, x1 = tr.x - cr.xMin;
            float y0 = cr.yMax - tr.y, y1 = cr.yMax - bl.y;
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>Pixeles de pantalla (origen abajo-izquierda) a unidades de canvas (origen arriba-izquierda).</summary>
        public static Vector2 ScreenToCanvas(Vector2 px)
        {
            Vector2 cs = CanvasSize;
            float sw = Mathf.Max(Screen.width, 1), sh = Mathf.Max(Screen.height, 1);
            return new Vector2(px.x / sw * cs.x, (1f - px.y / sh) * cs.y);
        }

        // ---------------------------------------------------------------- fuentes
        static Font titleFont, bodyFont;

        public static Font TitleFont
        {
            get
            {
                if (titleFont == null) titleFont = LoadFont("Fonts/LilitaOne-Regular");
                return titleFont;
            }
        }

        public static Font BodyFont
        {
            get
            {
                if (bodyFont == null) bodyFont = LoadFont("Fonts/Fredoka-SemiBold");
                return bodyFont;
            }
        }

        static Font LoadFont(string path)
        {
            Font f = Resources.Load<Font>(path);
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        // ---------------------------------------------------------------- sprites 9-slice
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static Sprite whiteSprite;

        public static Sprite White
        {
            get
            {
                if (whiteSprite == null)
                    whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                return whiteSprite;
            }
        }

        /// <summary>Sprite de Resources/UI/{name}. m = margenes de Godot (izq, arriba, der, abajo); (0,0,0,0) = sin cortes.</summary>
        public static Sprite Tex9(string name, Vector4 m)
        {
            string key = name + "|" + m.x + "," + m.y + "," + m.z + "," + m.w;
            Sprite s;
            if (sprites.TryGetValue(key, out s)) return s;
            Texture2D t = Resources.Load<Texture2D>("UI/" + name);
            if (t == null)
            {
                Debug.LogWarning("Falta la textura UI/" + name);
                s = White;
            }
            else
            {
                // Las texturas de UI estan guardadas a UiTexScale x (bordes nitidos en pantallas grandes): se compensa con el
                // ppu y los margenes, asi se ven del mismo tamaño. Unity: border = (izquierda, abajo, derecha, arriba)
                float k = UiTexScale(t);
                s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f * k, 0, SpriteMeshType.FullRect,
                    new Vector4(m.x, m.w, m.z, m.y) * k);
            }
            sprites[key] = s;
            return s;
        }

        /// <summary>Factor de escala guardado de una textura de UI (las de Kenney eran de 64 px de alto; se guardan a 4x).</summary>
        static float UiTexScale(Texture2D t) { return t.height >= 128 || t.width >= 256 ? 4f : 1f; }

        const float RoundScale = 6f;   // pixeles de textura por unidad de canvas (antes 2: bordes borrosos)

        /// <summary>Rectangulo redondeado blanco 9-slice (usar con pixelsPerUnitMultiplier = RoundScale).</summary>
        public static Sprite Round(int radius)
        {
            string key = "round" + radius;
            Sprite s;
            if (sprites.TryGetValue(key, out s)) return s;
            int px = (int)(RoundScale * (2 * radius + 2));
            Painter p = new Painter(px, px, RoundScale, 4);
            p.Poly(Painter.RoundRectPts(new Rect(0, 0, 2 * radius + 2, 2 * radius + 2), radius, 24), Color.white);
            Texture2D t = p.ToTexture();
            float b = RoundScale * radius;
            s = Sprite.Create(t, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprites[key] = s;
            return s;
        }

        static readonly Dictionary<string, BtnSkin> skins = new Dictionary<string, BtnSkin>();

        /// <summary>Paquete mas cercano al color pedido ("grey" se tiñe).</summary>
        public static string KCol(Color bg)
        {
            string best = "grey";
            float bd = 0.06f;
            Color[] opts = { Green, Blue, Red, Orange };
            string[] names = { "green", "blue", "red", "yellow" };
            for (int i = 0; i < opts.Length; i++)
            {
                float d = Mathf.Abs(opts[i].r - bg.r) + Mathf.Abs(opts[i].g - bg.g) + Mathf.Abs(opts[i].b - bg.b);
                if (d < bd) { bd = d; best = names[i]; }
            }
            return best;
        }

        static float Saturation(Color c)
        {
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return mx <= 0f ? 0f : (mx - mn) / mx;
        }

        /// <summary>Estilo de boton para un color (kbox de Godot). square = boton cuadrado (sq_*).</summary>
        public static BtnSkin Skin(Color bg, bool square = false)
        {
            // 0.9.4: botones con profundidad dibujados por codigo (ButtonArt), del color pedido; antes: texturas de Kenney
            string key = ColorUtility.ToHtmlStringRGBA(bg) + (square ? "S" : "B");
            BtnSkin s;
            if (skins.TryGetValue(key, out s)) return s;
            s = new BtnSkin();
            s.Normal = ButtonArt.Get(bg, square, 0);
            s.Pressed = ButtonArt.Get(bg, square, 1);
            s.Disabled = ButtonArt.Get(bg, square, 2);
            s.Tint = Color.white;
            s.DisabledTint = Color.white;
            s.CmNormal = ButtonArt.CmNormal;
            s.CmPressed = ButtonArt.CmPressed;
            s.Lip = ButtonArt.Lip(bg);
            skins[key] = s;
            return s;
        }

        // ---------------------------------------------------------------- jerarquia y anclas
        public static RectTransform New(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        /// <summary>Anclas en un punto (ax, ay con y hacia abajo) y posicion de la esquina sup-izq relativa a ese punto (como Godot).</summary>
        public static RectTransform Place(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, 1f - ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
            return rt;
        }

        public static RectTransform PlaceTL(RectTransform rt, float x, float y, float w, float h)
        {
            return Place(rt, 0f, 0f, x, y, w, h);
        }

        /// <summary>Mueve conservando tamaño: x, y de la esquina sup-izq relativa al ancla.</summary>
        public static void SetPos(RectTransform rt, float x, float y)
        {
            Vector2 s = rt.sizeDelta;
            rt.anchoredPosition = new Vector2(x + s.x * 0.5f, -(y + s.y * 0.5f));
        }

        public static Vector2 GetPos(RectTransform rt)
        {
            Vector2 s = rt.sizeDelta;
            return new Vector2(rt.anchoredPosition.x - s.x * 0.5f, -rt.anchoredPosition.y - s.y * 0.5f);
        }

        public static void SetSize(RectTransform rt, float w, float h)
        {
            Vector2 p = GetPos(rt);
            rt.sizeDelta = new Vector2(w, h);
            SetPos(rt, p.x, p.y);
        }

        public static RectTransform Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        // ---------------------------------------------------------------- elementos basicos
        public static Image Img(Transform parent, Sprite sprite, Color col, string name = "Img", bool raycast = false)
        {
            RectTransform rt = New(name, parent);
            Image im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite;
            im.color = col;
            im.raycastTarget = raycast;
            return im;
        }

        /// <summary>Imagen 9-slice de una textura Kenney.</summary>
        public static Image Box9(Transform parent, string tex, Vector4 margins, Color tint, string name = "Box", bool raycast = false)
        {
            // 0.9.4: las tarjetas de Kenney pasan a tarjetas con profundidad dibujadas por codigo
            if (tex == "card")
            {
                Image c = Img(parent, ButtonArt.Box(Color.white, 16f, 5f, 0.18f), tint, name, raycast);
                c.type = Image.Type.Sliced;
                return c;
            }
            Image im = Img(parent, Tex9(tex, margins), tint, name, raycast);
            im.type = Image.Type.Sliced;
            return im;
        }

        public static Image RoundImg(Transform parent, int radius, Color col, string name = "Round")
        {
            Image im = Img(parent, Round(radius), col, name, false);
            im.type = Image.Type.Sliced;
            im.pixelsPerUnitMultiplier = RoundScale;
            return im;
        }

        /// <summary>Caja redondeada con contorno (StyleBoxFlat con borde): dos capas, `bottom` = borde inferior extra.</summary>
        public static RectTransform OutBox(Transform parent, int radius, float border, float bottom, Color fill, Color outline, string name = "OutBox")
        {
            // 0.9.4: sin contorno negro: el borde es el "costado" del mismo color, mas oscuro (estilo con profundidad)
            if (outline == Out && fill.a > 0.9f) outline = Color.Lerp(ButtonArt.Lip(fill), fill, 0.35f);
            RectTransform root = New(name, parent);
            Image o = RoundImg(root, radius, outline, "Outer");
            Stretch(o.rectTransform);
            Image i = RoundImg(root, Mathf.Max(radius - (int)border, 2), fill, "Inner");
            Stretch(i.rectTransform, border, border, border, border + bottom);
            return root;
        }

        public static Image Tint(Transform parent, Color col, string name = "Rect")
        {
            Image im = Img(parent, White, col, name, false);
            return im;
        }

        public static Text Label(Transform parent, string text, int size = 24, Color? color = null, int outline = 6, bool title = true,
            TextAnchor anchor = TextAnchor.UpperLeft, string name = "Label")
        {
            RectTransform rt = New(name, parent);
            Text t = rt.gameObject.AddComponent<LocText>();   // se muestra en el idioma del jugador
            t.font = title ? TitleFont : BodyFont;
            t.fontSize = size;
            t.color = color ?? Color.white;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.text = text;
            if (outline > 0) SetOutline(t, outline);
            return t;
        }

        public static void SetOutline(Text t, int outline)
        {
            float d = Mathf.Min(outline * 0.4f, 8f);
            foreach (var o in t.GetComponents<Outline>()) UnityEngine.Object.Destroy(o);
            var so = t.GetComponent<SoftOutline>();
            if (so == null) so = t.gameObject.AddComponent<SoftOutline>();
            so.Color = Out;
            so.Radius = d;
        }

        public static void Wrap(Text t)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        public static Text LabelAt(Transform parent, string text, int size, Color color, int outline, bool title,
            float x, float y, float w, float h, TextAnchor anchor = TextAnchor.UpperLeft, string name = "Label")
        {
            Text t = Label(parent, text, size, color, outline, title, anchor, name);
            PlaceTL(t.rectTransform, x, y, w, h);
            return t;
        }

        public static IconView Icon(Transform parent, string kind, float size, string name = "Icon")
        {
            RectTransform root = New(name, parent);
            root.sizeDelta = new Vector2(size, size);
            Image im = Img(root, null, Color.white, "Img", false);
            float pad = size * (Icons.Over - 1f) * 0.5f;
            Stretch(im.rectTransform, -pad, -pad, -pad, -pad);
            im.preserveAspect = true;
            IconView iv = root.gameObject.AddComponent<IconView>();
            iv.Img = im;
            iv.SetKind(kind);
            return iv;
        }

        /// <summary>Icono del paquete Game Icons (blanco, CC0).</summary>
        public static Image KIcon(Transform parent, string name, float size)
        {
            Texture2D t = Resources.Load<Texture2D>("UI/icons/" + name);
            Sprite sp;
            string key = "kicon:" + name;
            if (!sprites.TryGetValue(key, out sp))
            {
                sp = t == null ? White : Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 400f);
                sprites[key] = sp;
            }
            Image im = Img(parent, sp, Color.white, "KIcon", false);
            im.preserveAspect = true;
            im.rectTransform.sizeDelta = new Vector2(size, size);
            return im;
        }

        // ---------------------------------------------------------------- botones
        /// <summary>Boton con texto (UIK.btn): w,h en unidades; x,y se fijan despues con Place.</summary>
        public static Btn Button(Transform parent, string text, Color bg, int fsize, float w, float h, string name = "Button")
        {
            RectTransform rt = New(name, parent);
            rt.sizeDelta = new Vector2(w, h);
            Image im = rt.gameObject.AddComponent<Image>();
            im.type = Image.Type.Sliced;
            im.raycastTarget = true;
            Btn b = rt.gameObject.AddComponent<Btn>();
            b.Bg = im;
            b.Skin = Skin(bg);
            RectTransform content = Stretch(New("Content", rt));
            b.Content = content;
            Text t = Label(content, text, fsize, Color.white, 6, true, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            b.Label = t;
            b.Restyle();
            return b;
        }

        /// <summary>Boton cuadrado de color (sq_button) para iconos.</summary>
        public static Btn SqButton(Transform parent, Color bg, float s, string name = "SqButton")
        {
            RectTransform rt = New(name, parent);
            rt.sizeDelta = new Vector2(s, s);
            Image im = rt.gameObject.AddComponent<Image>();
            im.type = Image.Type.Sliced;
            im.raycastTarget = true;
            Btn b = rt.gameObject.AddComponent<Btn>();
            b.Bg = im;
            b.Skin = Skin(bg, true);
            b.Restyle();
            return b;
        }

        /// <summary>Area tocable invisible con juice (para tarjetas y botones de imagen propia).</summary>
        public static Btn HitArea(Transform parent, float w, float h, string name = "Hit")
        {
            RectTransform rt = New(name, parent);
            rt.sizeDelta = new Vector2(w, h);
            Image im = rt.gameObject.AddComponent<Image>();
            im.color = new Color(1, 1, 1, 0);
            im.raycastTarget = true;
            Btn b = rt.gameObject.AddComponent<Btn>();
            b.Bg = null;
            return b;
        }

        // ---------------------------------------------------------------- componentes compuestos
        public static Capsule MakeCapsule(Transform parent, float w, float h, string fillKind = "green_border", Color? fillMod = null,
            Color? bgMod = null, int fsize = 16)
        {
            Capsule c = new Capsule();
            c.W = w; c.H = h;
            Image bg = Box9(parent, "progress_transparent", new Vector4(8, 8, 8, 8), bgMod ?? new Color(0.55f, 0.5f, 0.6f, 1f), "Capsule");
            bg.rectTransform.sizeDelta = new Vector2(w, h);
            c.Root = bg.rectTransform;
            c.Bg = bg;
            Image fill = Box9(bg.transform, "progress_" + fillKind, new Vector4(8, 8, 8, 8), fillMod ?? Color.white, "Fill");
            fill.rectTransform.sizeDelta = new Vector2(h, h);
            PlaceTL(fill.rectTransform, 0, 0, h, h);
            c.Fill = fill;
            c.SetFillKind(fillKind, fillMod ?? Color.white);
            Text l = Label(bg.transform, "", fsize, Color.white, 5, true, TextAnchor.MiddleCenter);
            Stretch(l.rectTransform);
            c.Label = l;
            return c;
        }

        public static Pill MakePill(Transform parent, string kind, float w)
        {
            Pill p = new Pill();
            Image f = Box9(parent, "field", new Vector4(10, 10, 10, 10), Color.white, "Pill");
            f.rectTransform.sizeDelta = new Vector2(w, 46);
            p.Root = f.rectTransform;
            p.Icon = Icon(f.transform, kind, 48);
            PlaceTL((RectTransform)p.Icon.transform, -14, -2, 48, 48);
            Text l = LabelAt(f.transform, "0", 26, Brown, 0, true, 34, 2, w - 44, 40, TextAnchor.MiddleRight);
            p.Label = l;
            return p;
        }

        public static Badge MakeBadge(Transform parent)
        {
            Badge b = new Badge();
            Image dot = Img(parent, Icons.BadgeDot(), Color.white, "Badge", false);
            dot.rectTransform.sizeDelta = new Vector2(26, 26);
            b.Root = dot.rectTransform;
            b.Label = Label(dot.transform, "", 16, Color.white, 0, true, TextAnchor.MiddleCenter);
            Stretch(b.Label.rectTransform);
            b.Root.gameObject.SetActive(false);
            return b;
        }

        /// <summary>Cinta chica ("¡NUEVO!") con la textura de boton.</summary>
        public static RectTransform MakeTag(Transform parent, string text, Color bg, int fsize = 16)
        {
            Image im = Box9(parent, "btn_" + KCol(bg), new Vector4(12, 12, 12, 16), KCol(bg) == "grey" ? bg : Color.white, "Tag");
            Text l = Label(im.transform, text, fsize, Color.white, 5, true, TextAnchor.MiddleCenter);
            float w = l.preferredWidth + 26f;
            im.rectTransform.sizeDelta = new Vector2(w, fsize + 16f);
            Stretch(l.rectTransform, 0, 0, 0, 5);
            return im.rectTransform;
        }

        /// <summary>Resplandor suave (imagen con el sprite radial).</summary>
        public static Image Glow(Transform parent, Color col, string name = "Glow")
        {
            Image g = Img(parent, Icons.Glow(), col, name, false);
            return g;
        }

        // ---------------------------------------------------------------- scroll y listas
        public sealed class ScrollParts
        {
            public ScrollRect Scroll;
            public RectTransform Content;
        }

        /// <summary>ScrollRect vertical con RectMask2D; el contenido crece con un VerticalLayoutGroup.</summary>
        public static ScrollParts Scroll(Transform parent, float spacing = 10f, string name = "Scroll", Vector2? gridCell = null, int gridCols = 2)
        {
            RectTransform rt = New(name, parent);
            Image catcher = rt.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);
            catcher.raycastTarget = true;
            ScrollRect sr = rt.gameObject.AddComponent<ScrollRect>();
            RectTransform vp = New("Viewport", rt);
            Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            RectTransform content = New("Content", vp);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(0, 0);
            content.offsetMax = new Vector2(0, 0);
            if (gridCell.HasValue)
            {
                GridLayoutGroup g = content.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = gridCell.Value;
                g.spacing = new Vector2(spacing, spacing);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                g.constraintCount = gridCols;
                g.childAlignment = TextAnchor.UpperCenter;
            }
            else
            {
                VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = spacing;
                v.childControlWidth = true;
                v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
            }
            ContentSizeFitter f = content.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vp;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30f;
            sr.inertia = true;
            ScrollParts p = new ScrollParts();
            p.Scroll = sr;
            p.Content = content;
            return p;
        }

        public static VerticalLayoutGroup VBox(Transform t, float spacing, TextAnchor anchor = TextAnchor.UpperCenter)
        {
            VerticalLayoutGroup v = t.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = anchor;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HBox(Transform t, float spacing, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            HorizontalLayoutGroup h = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = anchor;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static LayoutElement Item(Component c, float prefW = -1f, float prefH = -1f, float flexW = -1f, float flexH = -1f)
        {
            LayoutElement le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (prefW >= 0) { le.preferredWidth = prefW; le.minWidth = prefW; }
            if (prefH >= 0) { le.preferredHeight = prefH; le.minHeight = prefH; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        // ---------------------------------------------------------------- varios
        static float lastBuzz = -10f;

        /// <summary>Vibracion leve del telefono (compras, reclamos, hitos). Respeta el ajuste y no se pisa a si misma.</summary>
        public static void Buzz(int ms = 15)
        {
            if (!FxJuice.VibrationOn) return;
            float now = Time.unscaledTime;
            if (now - lastBuzz < 0.06f) return;
            lastBuzz = now;
            FxJuice.Vibrate(ms);
        }

        public static void Snd(string name, float vol = 0f, float pitch = 1f) { Sfx.Play(name, vol, pitch); }

        public static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        /// <summary>CanvasGroup (lo crea si falta).</summary>
        public static CanvasGroup Group(GameObject go)
        {
            CanvasGroup g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            return g;
        }

        public static string FmtInt(long v) { return v.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    }
}
