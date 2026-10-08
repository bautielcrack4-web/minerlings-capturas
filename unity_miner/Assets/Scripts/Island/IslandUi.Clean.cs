using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.IslandView
{
    /// <summary>
    /// Interfaz limpia (0.9.2, pedido del dueño): "no mostrar información hasta que el jugador la necesite".
    /// - Arriba solo monedas y gemas sobre vidrio oscuro; los mineros aparecen un rato cuando cambian.
    /// - Un único botón ☰ reemplaza la barra derecha: abre un panel con Mineros, Misiones, Nivel, Logros, Decorar,
    ///   Tienda y Ajustes (cada fila aparece recién cuando se desbloquea; lo nuevo lleva un punto).
    /// - Columna izquierda que se acomoda sola: mineros, meta colapsada y los almacenes solo cuando importan
    ///   (recién cobrado o casi lleno).
    /// - En el mundo: como mucho 3 indicadores a la vez (burbujas de "listo" primero, después el "+" de construir);
    ///   el resto espera. Sin círculos permanentes: el anillo aparece solo bajo el edificio seleccionado.
    /// </summary>
    public sealed partial class IslandUi
    {
        public static readonly Color GlassCol = new Color(0.07f, 0.06f, 0.12f, 0.56f);

        /// <summary>Capsula de vidrio oscuro sin borde (estilo "interfaz premium").</summary>
        RectTransform Glass(Transform parent, float w, float h, string name)
        {
            var img = Kit.RoundImg(parent, Mathf.Min(22, (int)(h * 0.5f)), GlassCol, name);
            img.rectTransform.sizeDelta = new Vector2(w, h);
            return img.rectTransform;
        }

        Pill GlassPill(Transform parent, string kind, float w)
        {
            var p = new Pill();
            p.Root = Glass(parent, w, 42, "Pill");
            p.Icon = Kit.Icon(p.Root, kind, 40);
            Kit.PlaceTL((RectTransform)p.Icon.transform, -6, 1, 40, 40);
            p.Label = Kit.LabelAt(p.Root, "", 24, Color.white, 0, true, 36, 0, w - 48, 42, TextAnchor.MiddleRight);
            return p;
        }

        /// <summary>Indicador redondo chico sobre el mundo (un edificio = un indicador).</summary>
        RectTransform Dot(Transform parent, Sprite icon, Color col, float size, string name)
        {
            var b = Kit.New(name, parent);
            b.sizeDelta = new Vector2(size, size);
            var d = Kit.RoundImg(b, (int)(size * 0.5f), col, "Disco");
            Kit.Stretch(d.rectTransform);
            if (icon != null)
            {
                var ic = Kit.Img(b, icon, Color.white, "Icono");
                ic.preserveAspect = true;
                float s = size * 0.62f;
                Kit.Place(ic.rectTransform, 0.5f, 0.5f, -s * 0.5f, -s * 0.5f, s, s);
            }
            return b;
        }

        void UpdateClean()
        {
            UpdateMenu();
            PickPlus();
            LayoutLeft();
            UpdateSelection();
            UpdateDecorDone();
            UpdatePlaceUi();
            UpdateComplexUi();
            UpdateFeel();
            UpdateHandCard();
            UpdateWeekend();
        }

        // ------------------------------------------------------------ fin de semana dorado (meta de vuelta D7)
        RectTransform weekendTag;
        bool weekendToasted;

        void UpdateWeekend()
        {
            bool on = Isl.Weekend && Isl.TutDone;
            if (on && weekendTag == null)
            {
                weekendTag = Glass(hudLayer, 300, 36, "FinDeSemana");
                Kit.Place(weekendTag, 0.5f, 0f, -150f, 70f, 300, 36);
                Kit.LabelAt(weekendTag, Loc.T("★ Fin de semana dorado: legendarios x3"), 19, Kit.Yellow, 0, true, 0, 0, 300, 36, TextAnchor.MiddleCenter);
                var rt = weekendTag;
                Tw.To(rt, "late", 3600f, Ease.Linear, u => rt.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(u * 3600f * 2.5f)));
            }
            if (weekendTag != null && weekendTag.gameObject.activeSelf != on) weekendTag.gameObject.SetActive(on);
            if (on && !weekendToasted && sheet == null && onboardT > 3f)
            {
                weekendToasted = true;
                string key = "finde_" + IslandGame.Today;
                if (PlayerPrefs.GetInt(key, 0) == 0)
                {
                    PlayerPrefs.SetInt(key, 1);
                    Toast(Loc.T("¡Fin de semana dorado! Más vetas gigantes y legendarios x3"), Kit.Yellow, null, true);
                }
            }
        }

        // ------------------------------------------------------------ menu ☰
        Btn menuBtn;
        Badge menuBadge;
        readonly Dictionary<string, bool> unlocked = new Dictionary<string, bool>();
        readonly HashSet<string> menuNew = new HashSet<string>();

        sealed class MenuRow
        {
            public string Key, Name;
            public System.Func<Sprite> Icon;
            public System.Func<bool> Open;
            public System.Action Act;
            public System.Func<string> Right;
        }

        List<MenuRow> menuRows;

        void BuildMenu()
        {
            var rt = Glass(hudLayer, 56, 56, "Menu");
            Kit.Place(rt, 1f, 0f, -76f, 74f, 56, 56);
            rt.GetComponent<Image>().raycastTarget = true;
            menuBtn = rt.gameObject.AddComponent<Btn>();
            for (int i = 0; i < 3; i++)
            {
                var bar = Kit.RoundImg(rt, 2, Color.white, "Raya");
                Kit.Place(bar.rectTransform, 0.5f, 0.5f, -13f, -11f + i * 9f, 26, 4);
            }
            menuBadge = Kit.MakeBadge(rt);
            Kit.Place(menuBadge.Root, 1f, 0f, -18f, -6f, 24, 24);
            menuBtn.Clicked += OpenMenu;
            menuRows = new List<MenuRow>
            {
                new MenuRow { Key = "album", Name = Loc.T("Mineros"), Icon = () => Icons.MinerBust(IslandGame.RarityCol[2]), Open = () => Isl.Miners.Count >= 2 && Isl.TutDone, Act = OpenAlbum,
                    Right = () => Isl.FoundCount + "/" + Island.Roster.Length },
                new MenuRow { Key = "diario", Name = Loc.T("Misiones"), Icon = Flame, Open = () => Isl.TutDone && Isl.GoalIdx >= 3, Act = OpenDaily,
                    Right = () => dailyBadge != null && dailyBadge.Count > 0 ? dailyBadge.Count.ToString() : "" },
                new MenuRow { Key = "pase", Name = Loc.T("Nivel"), Icon = () => Icons.Get("star"), Open = () => Isl.Th >= 3, Act = OpenPass, Right = () => Loc.T("Nv ") + Isl.SeasonLevel },
                new MenuRow { Key = "museo", Name = Loc.T("Logros"), Icon = () => Icons.Get("trophy"), Open = () => Isl.Pieces != 0, Act = OpenMuseum },
                new MenuRow { Key = "decorar", Name = Loc.T("Decorar"), Icon = Heart, Open = () => Isl.Th >= 4 && (Isl.TotalEarned > 400 || Isl.Decor.Count > 0), Act = ToggleDecor,
                    Right = () => Isl.Beauty > 0 ? "+" + Mathf.RoundToInt((float)(Isl.BeautyMult() - 1.0) * 100f) + " %" : "" },
                new MenuRow { Key = "tienda", Name = Loc.T("Tienda"), Icon = () => Icons.Get("gem"), Open = () => Isl.TutDone, Act = OpenShop },
                new MenuRow { Key = "ajustes", Name = Loc.T("Ajustes"), Icon = GearIcon, Open = () => true, Act = OpenSettings },
            };
        }

        static Sprite gearIcon;

        static Sprite GearIcon()
        {
            if (gearIcon != null) return gearIcon;
            var t = Resources.Load<Texture2D>("UI/icons/gear");
            gearIcon = t != null ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 400f) : Icons.Get("star");
            return gearIcon;
        }

        void UpdateMenu()
        {
            if (menuBtn == null) return;
            // lo que se desbloquea por primera vez enciende un punto en el ☰ (y la fila dice "Nuevo")
            foreach (var r in menuRows)
            {
                bool now = r.Open();
                bool was;
                unlocked.TryGetValue(r.Key, out was);
                if (now && !was && onboardT > 1.5f && r.Key != "ajustes" && r.Key != "tienda" && PlayerPrefs.GetInt("nuevo_" + r.Key, 0) == 0)
                {
                    PlayerPrefs.SetInt("nuevo_" + r.Key, 1);
                    menuNew.Add(r.Key);
                    Tw.Pop(menuBtn.transform, 1.35f);
                    Sparkle(LayerPos((RectTransform)menuBtn.transform), new Color(1f, 0.95f, 0.6f));
                    Sfx.Play("pop", -8f, 1.2f);
                }
                unlocked[r.Key] = now;
            }
            int n = (dailyBadge != null ? dailyBadge.Count : 0) + menuNew.Count;
            menuBadge.SetCount(n);
            menuBtn.gameObject.SetActive(Isl.TutDone || Isl.Tut >= Island.TutStep.CollectWood);
            // un meneo suave cada 4 s si hay algo para cobrar adentro (no un temblor constante)
            if (n > 0 && !menuBtn.IsPressed && Time.unscaledTime > menuBtn.QuietUntil)
            {
                float w = Mathf.Repeat(Time.time, 4f);
                menuBtn.transform.localRotation = Quaternion.Euler(0, 0, w < 0.45f ? Mathf.Sin(w * 40f) * 6f * (1f - w / 0.45f) : 0f);
            }
            else menuBtn.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Panel del ☰: tarjeta clara que cae desde el boton, una fila por cosa desbloqueada.</summary>
        public void OpenMenu()
        {
            CloseSheet();
            sheetRefresh.Clear();
            sheet = Kit.New("Hoja", sheetLayer);
            Kit.Stretch(sheet);
            menuOpenSheet = sheet;
            var dim = Kit.Tint(sheet, new Color(0, 0, 0, 0.22f), "Dim");
            Kit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var close = dim.gameObject.AddComponent<Btn>();
            close.Juice = false;
            close.Clicked += () => { if (!sheetLocked) CloseSheet(); };
            var rows = new List<MenuRow>();
            foreach (var r in menuRows) if (r.Open()) rows.Add(r);
            const float rowH = 74f, w = 330f;
            float h = rows.Count * rowH + 30f;
            var panel = Kit.Img(sheet, ButtonArt.Box(Kit.Cream, 26f, 6f, 0.3f), Color.white, "Marco");
            panel.type = Image.Type.Sliced;
            var pr = panel.rectTransform;
            panel.raycastTarget = true;
            // arriba a la derecha, debajo del ☰ (la hoja no tiene el ajuste de zona segura: se suma el margen de arriba)
            float top = 140f + SafeTop();
            Kit.Place(pr, 1f, 0f, -w - 16f, top, w, h);

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                // cada fila es un boton con profundidad (cara clara, labio, se hunde al tocar)
                var hit = Kit.Button(pr, "", new Color(1f, 0.985f, 0.95f), 20, w - 28f, rowH - 6f, r.Key);
                Kit.PlaceTL((RectTransform)hit.transform, 14, 14 + i * rowH, w - 28f, rowH - 6f);
                var cont = hit.Content;
                var ic = Kit.Img(cont, r.Icon(), Color.white, "Icono");
                ic.preserveAspect = true;
                Kit.Place(ic.rectTransform, 0f, 0.5f, 6f, -22f, 44, 44);
                if (r.Key == "ajustes") ic.color = Kit.Brown;
                var nl = Kit.Label(cont, r.Name, 25, Kit.Brown, 0, true, TextAnchor.MiddleLeft, "Nombre");
                Kit.Stretch(nl.rectTransform, 60, 0, 90, 0);
                string right = r.Right != null ? r.Right() : "";
                bool isNew = menuNew.Contains(r.Key);
                if (isNew) right = Loc.T("Nuevo");
                if (right != "")
                {
                    bool hot = isNew || r.Key == "diario";
                    var tag = Kit.Label(cont, right, 20, hot ? Kit.Red : new Color(0.55f, 0.47f, 0.4f), 0, true, TextAnchor.MiddleRight, "Dato");
                    Kit.Stretch(tag.rectTransform, 150, 0, 10, 0);
                    if (hot) Tw.Pop(tag.rectTransform, 1.3f);
                }
                // cascada: cada fila entra 30 ms despues de la anterior, con un gesto del icono
                var hrt = (RectTransform)hit.transform;
                hrt.localScale = Vector3.zero;
                Tw.Scale(hrt, Vector3.zero, Vector3.one, 0.24f, Ease.OutBack, 0.06f + Mineros.UI.Motion.Delay(i));
                var act = r.Act;
                hit.Clicked += () => { CloseSheet(); act(); };
            }
            menuNew.Clear();
            pr.localScale = new Vector3(0.6f, 0.6f, 1f);
            pr.pivot = new Vector2(1f, 1f);
            pr.anchoredPosition += new Vector2(w * 0.5f, h * 0.5f);
            Tw.Scale(pr, new Vector3(0.6f, 0.6f, 1f), Vector3.one, 0.22f, Ease.OutBack);
            Sfx.Play("open", -8f);
        }

        float SafeTop()
        {
            var sa = Screen.safeArea;
            float px = Screen.height - sa.yMax;
            return px / Mathf.Max(1f, Screen.height) * Kit.CanvasSize.y;
        }

        // ------------------------------------------------------------ indicadores: tope de 3 a la vez
        readonly HashSet<int> bubblePlots = new HashSet<int>();
        readonly HashSet<int> plusPlots = new HashSet<int>();
        readonly List<Plot> readyTmp = new List<Plot>();
        float pickT;
        const int MaxMarks = 3;

        /// <summary>Elige que burbujas de "listo" se ven (las mas llenas primero, max 3) y en cuantas parcelas va el "+".</summary>
        void PickBubbles()
        {
            pickT -= Time.unscaledDeltaTime;
            if (pickT > 0f) return;
            pickT = 0.3f;
            readyTmp.Clear();
            foreach (var p in Isl.Plots)
                if (p.Ring <= Isl.Expand && p.Ready > 0 && p.ReadyRes >= 0 && p.Work <= 0) readyTmp.Add(p);
            readyTmp.Sort((a, b) =>
            {
                bool fa = Island.Extractor(a.Building) != null && a.Ready >= Isl.ExtractBuffer(a);
                bool fb = Island.Extractor(b.Building) != null && b.Ready >= Isl.ExtractBuffer(b);
                if (fa != fb) return fa ? -1 : 1;
                bool sa = bubblePlots.Contains(a.Id), sb = bubblePlots.Contains(b.Id);   // las que ya se ven no saltan
                if (sa != sb) return sa ? -1 : 1;
                if (a.Ready != b.Ready) return b.Ready.CompareTo(a.Ready);
                return a.Id.CompareTo(b.Id);
            });
            bubblePlots.Clear();
            if (WorldQuiet) return;   // mientras se coloca un edificio (o en el Modo Cuartel), el mundo queda sin indicadores
            for (int i = 0; i < readyTmp.Count && i < MaxMarks; i++) bubblePlots.Add(readyTmp[i].Id);
        }

        void PickPlus()
        {
            plusPlots.Clear();
            if (game.DecorMode || WorldQuiet) return;
            if (Isl.TutDone) return;   // fuera del tutorial se construye con el boton "Construir" (sin "+" en el mapa)
            int room = Mathf.Min(2, MaxMarks - bubblePlots.Count);
            if (Isl.Tut == Island.TutStep.BuildSawmill) room = 1;
            if (room <= 0) return;
            foreach (var p in Isl.Plots)
            {
                if (plusPlots.Count >= room) break;
                if (p.Ring > Isl.Expand || !Isl.Offered(p)) continue;
                if (!AnyBuildable(p)) continue;
                plusPlots.Add(p.Id);
            }
        }

        /// <summary>El suelo de la parcela libre (estacas) se ve solo donde hay "+".</summary>
        public bool ShowsPlus(int plotId) { return plusPlots.Contains(plotId); }

        // ------------------------------------------------------------ flechas de mejora: solo cuando hacen falta
        readonly Dictionary<int, float> arrowFlash = new Dictionary<int, float>();

        /// <summary>Recien alcanza para mejorar (3 s) o el jugador lleva un rato sin hacer nada (la mas barata).</summary>
        bool ArrowWanted(int id)
        {
            float t;
            if (arrowFlash.TryGetValue(id, out t) && Time.unscaledTime < t) return true;
            if (!Isl.TutDone || idleHintT < 12f || WorldQuiet) return false;
            foreach (var a in arrowPlots) return a == id;
            return false;
        }

        // ------------------------------------------------------------ columna izquierda que se acomoda sola
        float rawFlashT = -99f, prodFlashT = -99f, builderFlashT = -99f;
        int shownFree = -1;

        void FlashChip(bool raw) { if (raw) rawFlashT = Time.unscaledTime; else prodFlashT = Time.unscaledTime; }

        void LayoutLeft()
        {
            float now = Time.unscaledTime;
            bool done = Isl.TutDone;
            int free = Isl.FreeBuilders();
            if (free != shownFree) { if (shownFree >= 0) builderFlashT = now; shownFree = free; }
            bool minersOk = Isl.Miners.Count >= 2 || done;
            SetVis(minerPill.Root, minersOk && now - minerSeenT < 4f);
            if (builderChip != null)
            {
                bool tutB = Isl.Tut == Island.TutStep.FinishWork;
                SetVis(builderChip, (done || tutB) && (now - builderFlashT < 4f || tutB));
                float rf = Isl.RawStored() / (float)Mathf.Max(1, Isl.RawCap());
                float pf = Isl.ProdStored() / (float)Mathf.Max(1, Isl.ProdCap());
                SetVis(rawChip, Isl.RawStored() > 0 && (now - rawFlashT < 4f || rf >= 0.9f));
                SetVis(prodChip, Isl.ProdStored() > 0 && (now - prodFlashT < 4f || pf >= 0.9f));
            }
            float y = 22f;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f);
            RectTransform[] stack = { minerPill.Root, goalBox, builderChip, rawChip, prodChip };
            foreach (var rt in stack)
            {
                if (rt == null || !rt.gameObject.activeSelf) continue;
                Vector2 p = Kit.GetPos(rt);
                float ny = Mathf.Abs(p.y - y) < 0.5f ? y : Mathf.Lerp(p.y, y, k);
                if (p.x != 20f || ny != p.y) Kit.SetPos(rt, 20f, ny);
                y += rt.sizeDelta.y + 8f;
            }
        }

        static void SetVis(RectTransform rt, bool on)
        {
            if (rt == null || rt.gameObject.activeSelf == on) return;
            rt.gameObject.SetActive(on);
            if (on) Tw.Pop(rt, 1.12f);
        }

        // ------------------------------------------------------------ anillo de seleccion (solo con un edificio abierto)
        RectTransform selRing;

        void UpdateSelection()
        {
            Plot p = sheet != null ? cityPlot : null;
            if (p == null) { if (selRing != null && selRing.gameObject.activeSelf) selRing.gameObject.SetActive(false); return; }
            if (selRing == null)
            {
                var img = Kit.Img(worldLayer, Icons.Ring(5f), new Color(1f, 0.93f, 0.6f, 0.95f), "Seleccion");
                selRing = img.rectTransform;
                selRing.SetAsFirstSibling();
            }
            if (!selRing.gameObject.activeSelf) { selRing.gameObject.SetActive(true); Tw.Pop(selRing, 1.25f); }
            Vector3 c = game.PlotWorld(p);
            const float R = 2.1f;
            Vector2 a = ToCanvas(c + new Vector3(R, 0f, 0f)), b = ToCanvas(c - new Vector3(R, 0f, 0f));
            Vector2 d = ToCanvas(c + new Vector3(0f, 0f, R)), e = ToCanvas(c - new Vector3(0f, 0f, R));
            Vector2 cc = ToCanvas(c);
            float w = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(d.x - e.x));
            float h = Mathf.Max(Mathf.Abs(a.y - b.y), Mathf.Abs(d.y - e.y));
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.03f;
            selRing.sizeDelta = new Vector2(w, h) * pulse;
            selRing.anchoredPosition = new Vector2(cc.x, -cc.y);
        }

        // ------------------------------------------------------------ salir del modo decorar
        Btn decorDone;

        void UpdateDecorDone()
        {
            bool on = game.DecorMode && sheet == null;
            if (decorDone == null)
            {
                if (!on) return;
                decorDone = Kit.Button(hudLayer, Loc.T("Listo"), Kit.Green, 26, 180, 64, "DecorarListo");
                Kit.Place((RectTransform)decorDone.transform, 0.5f, 1f, -90f, -86f, 180, 64);
                decorDone.Clicked += () => { if (game.DecorMode) ToggleDecor(); };
            }
            if (decorDone.gameObject.activeSelf != on) { decorDone.gameObject.SetActive(on); if (on) Tw.Pop(decorDone.transform, 1.3f); }
        }
    }
}
