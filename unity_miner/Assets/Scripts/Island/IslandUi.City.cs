using System.Collections.Generic;
using System.Text;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Monetization;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Interfaz de la ciudad (biblia de produccion A.8 y B.1-B.4): constructores y almacenes en el HUD, reloj de obra y
    /// burbuja de "listo" sobre cada edificio (tocar o deslizar el dedo para cobrar en cadena), catalogo de construccion
    /// por Ayuntamiento y hoja de cada edificio (obra, produccion con cola, mercado, tren, almacen, Ayuntamiento).
    /// </summary>
    public sealed partial class IslandUi
    {
        // ------------------------------------------------------------ formato
        public static string Clock(double s)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt((float)s));
            if (t >= 3600) return (t / 3600) + ":" + ((t / 60) % 60).ToString("00") + ":" + (t % 60).ToString("00");
            return (t / 60) + ":" + (t % 60).ToString("00");
        }

        public static string Dur(double s)
        {
            int t = Mathf.Max(0, Mathf.RoundToInt((float)s));
            if (t < 60) return t + " s";
            if (t < 3600) return (t / 60) + Loc.T(" min") + (t % 60 >= 10 ? " " + (t % 60) + " s" : "");
            int h = t / 3600, m = (t / 60) % 60;
            return h + " h" + (m > 0 ? " " + m + Loc.T(" min") : "");
        }

        static readonly Color MissingCol = Icons.H("e5484d");

        // ------------------------------------------------------------ HUD: constructores y almacenes
        RectTransform builderChip, rawChip, prodChip;
        Text builderL, rawL, prodL;
        Image rawFill, prodFill;

        void BuildCityHud()
        {
            builderChip = Chip("Constructores", Icons.Get("hammer"), 196, out builderL, out _);
            rawChip = Chip("Galpon", ResIcons.Get(Res.Stone), 250, out rawL, out rawFill);
            prodChip = Chip("Almacen", ResIcons.Get(Res.IronBar), 304, out prodL, out prodFill);
            AddTap(builderChip, () => Toast(Isl.FreeBuilders() > 0 ? Loc.T("Tenés ") + Isl.FreeBuilders() + Loc.T(" constructor(es) libre(s): ¡a construir!") : Loc.T("Todos los constructores están trabajando"), Kit.Blue));
            AddTap(rawChip, () => OpenStorage(true));
            AddTap(prodChip, () => OpenStorage(false));
            Isl.Collected += OnCollected;
            Isl.StorageSold += OnStorageSold;
            Isl.StorageFull += kind =>
            {
                if (Time.unscaledTime < storageToastT) return;
                storageToastT = Time.unscaledTime + 8f;
                Toast(kind == "raw" ? Loc.T("Galpón lleno: mejoralo o vendé") : Loc.T("Almacén lleno: mejoralo o vendé"), Kit.Orange);
                FlashChip(kind == "raw");
                Tw.Pop(kind == "raw" ? rawChip : prodChip, 1.25f);
            };
        }

        float storageToastT, soldToastT;
        int soldN; double soldCoins;

        /// <summary>El Galpon lleno vende solo la pila mas grande: se ve y se oye (monedas al contador), nunca frena.</summary>
        void OnStorageSold(Res r, int n, double coins)
        {
            soldN += n; soldCoins += coins;
            if (Time.unscaledTime < soldToastT) return;
            soldToastT = Time.unscaledTime + 6f;
            Toast(Loc.T("Galpón lleno: se vendió ") + soldN + " " + Island.RDef(r).Name + "  +" + BigNum.Fmt(System.Math.Round(soldCoins)), Kit.Yellow);
            Tw.Pop(Island.RDef(r).Raw ? rawChip : prodChip, 1.15f);
            soldN = 0; soldCoins = 0;
        }

        RectTransform Chip(string name, Sprite icon, float y, out Text label, out Image fill)
        {
            // capsula de vidrio como el HUD de arriba; aparece solo cuando importa (IslandUi.Clean.LayoutLeft)
            var box = Glass(hudLayer, 132, 42, name);
            Kit.Place(box, 0f, 0f, 20f, y, 132, 42);
            var ic = Kit.Img(box, icon, Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.PlaceTL(ic.rectTransform, 4, 3, 36, 36);
            label = Kit.LabelAt(box, "", 20, Color.white, 0, true, 42, 2, 84, 28, TextAnchor.MiddleLeft);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 11; label.resizeTextMaxSize = 20;   // "1717/50" no se sale del vidrio
            var bar = Kit.RoundImg(box, 2, new Color(1f, 1f, 1f, 0.22f), "Barra");
            Kit.PlaceTL(bar.rectTransform, 44, 30, 76, 5);
            fill = Kit.RoundImg(bar.transform, 2, Kit.Green, "Relleno");
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            box.gameObject.SetActive(false);
            return box;
        }

        void AddTap(RectTransform rt, System.Action a)
        {
            var img = rt.GetComponent<Image>();
            if (img == null) img = rt.GetChild(0).GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Btn>();
            b.Clicked += a;
        }

        void UpdateCityHud()
        {
            if (builderChip == null) return;
            string bt = Isl.FreeBuilders() + "/" + Isl.Builders();
            if (builderL.text != bt) builderL.text = bt;
            SetCap(rawL, rawFill, Isl.RawStored(), Isl.RawCap());
            SetCap(prodL, prodFill, Isl.ProdStored(), Isl.ProdCap());
        }

        void SetCap(Text l, Image fill, int have, int cap)
        {
            string s = have + "/" + cap;
            if (l.text != s) l.text = s;
            float f = Mathf.Clamp01(have / (float)Mathf.Max(1, cap));
            fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(4f, 76f * f), 0f);
            fill.color = f >= 1f ? Kit.Red : f >= 0.9f ? Kit.Orange : Kit.Green;
        }

        // ------------------------------------------------------------ marcas sobre los edificios
        readonly Dictionary<int, Btn> timerMarks = new Dictionary<int, Btn>();
        readonly Dictionary<int, Btn> bubbleMarks = new Dictionary<int, Btn>();
        bool swiping;
        int swipeChain;

        void UpdateCityMarks()
        {
            bool held = Input.GetMouseButton(0) || Input.touchCount > 0;
            if (!held) { swiping = false; swipeChain = 0; }
            Vector2 pointer = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            PickBubbles();
            foreach (var p in Isl.Plots)
            {
                bool inIsland = p.Ring <= Isl.Expand;
                Vector3 top = game.PlotWorld(p) + Vector3.up * (p.Building >= 0 && p.Level >= 1 ? game.PlotHeight(p) + 1.5f : 2.6f);
                Vector2 c = ToCanvas(top);
                // reloj de obra
                Btn tm;
                if (!timerMarks.TryGetValue(p.Id, out tm)) { tm = MakeTimerMark(p); timerMarks[p.Id] = tm; }
                bool working = inIsland && p.Work > 0;
                if (tm.gameObject.activeSelf != working) { tm.gameObject.SetActive(working); if (working) Tw.Pop(tm.transform, 1.2f); }
                if (working)
                {
                    var rt = (RectTransform)tm.transform;
                    rt.anchoredPosition = new Vector2(c.x, -c.y);
                    // "¡Terminar!" verde que late solo en el tutorial (ensena); despues, reloj neutro con la cuenta
                    // regresiva (tocarlo igual termina gratis): antes era una orden en cada obra (pedido del creador)
                    bool free = Isl.SpeedUpGems(p) == 0 && !Isl.TutDone;
                    var lab = tm.Label;
                    string txt = free ? Loc.T("¡Terminar!") : Clock(p.Work);
                    if (lab.text != txt) lab.text = txt;
                    var fill = rt.Find("Caja/Barra/Relleno") as RectTransform;
                    float f = p.WorkTotal > 0 ? Mathf.Clamp01(1f - (float)(p.Work / p.WorkTotal)) : 0f;
                    if (fill != null) fill.sizeDelta = new Vector2(Mathf.Max(3f, 70f * f), 0f);
                    if (fill != null) fill.parent.gameObject.SetActive(!free);
                    Color want = free ? Kit.Green : GlassCol;
                    var box = rt.Find("Caja").GetComponent<Image>();
                    if (box.color != want) box.color = want;
                    if (free && !tm.IsPressed && Time.unscaledTime > tm.QuietUntil) tm.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 7f) * 0.05f);
                }
                // burbuja de producto listo
                Btn bb;
                if (!bubbleMarks.TryGetValue(p.Id, out bb)) { bb = MakeBubble(p); bubbleMarks[p.Id] = bb; }
                bool ready = inIsland && p.Ready > 0 && p.ReadyRes >= 0 && p.Work <= 0 && sheet == null && bubblePlots.Contains(p.Id);
                if (bb.gameObject.activeSelf != ready) { bb.gameObject.SetActive(ready); if (ready) Tw.Pop(bb.transform, 1.35f); }
                if (ready)
                {
                    var rt = (RectTransform)bb.transform;
                    float bob = Mathf.Sin(Time.time * 3.2f + p.Id) * 3f;
                    rt.anchoredPosition = new Vector2(c.x, -c.y + bob);
                    var ic = rt.Find("Icono").GetComponent<Image>();
                    var spr = ResIcons.Get(p.ReadyRes);
                    if (ic.sprite != spr) ic.sprite = spr;
                    bool blocked = Isl.Room((Res)p.ReadyRes) <= 0;
                    ic.color = blocked ? new Color(0.6f, 0.6f, 0.62f) : Color.white;
                    bool full = Island.Extractor(p.Building) != null && p.Ready >= Isl.ExtractBuffer(p);
                    var innerT = rt.Find("Burbuja/Inner");
                    if (innerT != null)
                    {
                        var inner = innerT.GetComponent<Image>();
                        Color want = full ? Color.Lerp(Kit.Yellow, Color.white, 0.35f + 0.25f * Mathf.Sin(Time.time * 6f)) : Color.white;
                        inner.color = want;
                    }
                    if (full) rt.localScale = Vector3.one * (1.04f + Mathf.Sin(Time.time * 5f) * 0.04f);
                    else if (rt.localScale.x > 1.02f && !bb.IsPressed) rt.localScale = Vector3.one;
                    // deslizar el dedo por encima cobra en cadena
                    if (swiping && held && RectTransformUtility.RectangleContainsScreenPoint(rt, pointer, null)) CollectFrom(p, bb, true);
                }
            }
        }

        Btn MakeTimerMark(Plot p)
        {
            // reloj chico de vidrio: tiempo + barra fina; verde con "¡Terminar!" cuando es gratis
            var b = Kit.HitArea(worldLayer, 112, 52, "Reloj");
            var rt = (RectTransform)b.transform;
            var box = Glass(rt, 104, 38, "Caja");
            Kit.Place(box, 0.5f, 0.5f, -52f, -19f, 104, 38);
            var ic = Kit.Img(box, Icons.Get("clock"), Color.white, "Icono");
            Kit.PlaceTL(ic.rectTransform, 3, 4, 30, 30);
            var l = Kit.Label(box, "", 19, Color.white, 0, true, TextAnchor.MiddleCenter);
            Kit.Stretch(l.rectTransform, 30, 0, 4, 6);
            b.Label = l;
            var bar = Kit.RoundImg(box, 2, new Color(1f, 1f, 1f, 0.25f), "Barra");
            Kit.Place(bar.rectTransform, 0.5f, 1f, -35f, -9f, 70, 4);
            var fill = Kit.RoundImg(bar.transform, 2, Kit.Yellow, "Relleno");
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            var pp = p;
            b.Clicked += () =>
            {
                if (pp.Work <= 0) return;
                if (Isl.SpeedUpGems(pp) == 0)
                {
                    if (Isl.SpeedUp(pp)) { Sfx.Play("tadaa", -6f); Juice.Vibrate(25); game.Save(); }
                }
                else OpenPlot(pp);
            };
            b.gameObject.SetActive(false);
            return b;
        }

        Btn MakeBubble(Plot p)
        {
            // burbuja chica (46 px, antes 84): circulo blanco sin contorno + icono; la zona de toque es mas grande
            var b = Kit.HitArea(worldLayer, 64, 64, "Listo");
            var rt = (RectTransform)b.transform;
            var shadow = Kit.RoundImg(rt, 23, new Color(0f, 0f, 0f, 0.16f), "Sombra");
            Kit.Place(shadow.rectTransform, 0.5f, 0.5f, -23f, -20f, 46, 46);
            var circle = Kit.New("Burbuja", rt);
            Kit.Place(circle, 0.5f, 0.5f, -23f, -23f, 46, 46);
            var inner = Kit.RoundImg(circle, 23, Color.white, "Inner");
            Kit.Stretch(inner.rectTransform);
            var ic = Kit.Img(rt, ResIcons.Get(Res.Stone), Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.Place(ic.rectTransform, 0.5f, 0.5f, -17f, -17f, 34, 34);
            b.Juice = false;
            b.PlaySound = false;
            var pp = p;
            b.Down += () => { swiping = true; swipeChain = 0; CollectFrom(pp, b, false); };
            b.gameObject.SetActive(false);
            return b;
        }

        /// <summary>La burbuja se rompe en 4 pedacitos que salen despedidos (el recurso ya vuela al almacen).</summary>
        void BubbleBurst(RectTransform bubble, Color col)
        {
            Vector2 at = LayerPos(bubble);
            for (int i = 0; i < 4; i++)
            {
                var d = UiPool.Get(flyLayer, Icons.Dot(), Color.white, "Pedacito");
                var rt = d.rectTransform;
                rt.sizeDelta = new Vector2(12, 12);
                float a = (i * 90f + 45f + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(50f, 80f);
                Tw.To(rt, "burbuja", 0.28f, Ease.OutCubic, u =>
                {
                    rt.anchoredPosition = at + v * u;
                    rt.localScale = Vector3.one * (1f - u * 0.7f);
                    d.color = new Color(1f, 1f, 1f, 1f - u);
                }, () => UiPool.Release(d));
            }
        }

        /// <summary>Toque sobre un edificio con algo listo: cobra (como tocar su burbuja). Devuelve false si no habia nada.</summary>
        public bool TapCollect(Plot p)
        {
            Btn bb;
            if (p == null || p.Ready <= 0 || p.ReadyRes < 0 || p.Work > 0 || !bubbleMarks.TryGetValue(p.Id, out bb) || bb == null || !bb.gameObject.activeSelf) return false;
            swipeChain = 0;
            CollectFrom(p, bb, false);
            return true;
        }

        void CollectFrom(Plot p, Btn b, bool chained)
        {
            if (p.Ready <= 0) return;
            int got = Isl.Collect(p);
            if (got <= 0)
            {
                if (!chained) { NoMoney(b); Toast(Island.RDef((Res)p.ReadyRes).Raw ? Loc.T("¡Galpón lleno!") : Loc.T("¡Almacén lleno!"), Kit.Orange); }
                return;
            }
            swipeChain++;
            Mineros.Fx.Haptics.Soft(Mathf.Min(1f, 0.35f + swipeChain * 0.08f));   // cada eslabon de la cadena un poco mas fuerte
            BubbleBurst((RectTransform)b.transform, ResIcons.Tint(p.ReadyRes));
            if (chained && swipeChain >= 5) Popup(game.PlotWorld(p) + Vector3.up * (game.PlotHeight(p) + 2.6f), Loc.T("¡Cadena x") + swipeChain + "!", Kit.Yellow, 30 + Mathf.Min(swipeChain, 12));
            game.Save();
        }

        /// <summary>Lo cobrado vuela en arco hasta su almacen (B.3: 1-5 copias, cada una con un tic que sube de tono).</summary>
        void OnCollected(Plot p, int res, int n)
        {
            Vector2 c = ToCanvas(game.PlotWorld(p) + Vector3.up * (game.PlotHeight(p) + 1.5f));
            Vector2 from = new Vector2(c.x, -c.y + 10f);
            bool isRaw = Island.RDef((Res)res).Raw;
            FlashChip(isRaw);
            var chip = isRaw ? rawChip : prodChip;
            if (chip != null && !chip.gameObject.activeSelf) { chip.gameObject.SetActive(true); LayoutLeft(); }
            if (chip == null || !chip.gameObject.activeInHierarchy) chip = coinPill.Root;
            Vector2 to = LayerPos(chip, -50f);
            int copies = Mathf.Clamp(n, 1, 5);
            var spr = ResIcons.Get(res);
            int variant = (p.Id + swipeChain) % 3;
            CheckNewResource(res);
            Color trail = ResIcons.Tint(res);
            int chainNow = swipeChain;
            for (int i = 0; i < copies; i++)
            {
                var ghost = UiPool.Get(flyLayer, Icons.Glow(), new Color(trail.r, trail.g, trail.b, 0f), "Estela");
                ghost.rectTransform.sizeDelta = new Vector2(46, 46);
                var img = UiPool.Get(flyLayer, spr, Color.white, "Recurso");
                img.preserveAspect = true;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(54, 54);
                ghost.rectTransform.anchoredPosition = from;
                rt.anchoredPosition = from;
                rt.localScale = Vector3.zero;
                float delay = i * 0.05f;
                int idx = i;
                Vector2 side = new Vector2(Random.Range(-60f, 60f), 0f);
                Tw.To(rt, "res", 0.45f + delay, Ease.Linear, u =>
                {
                    float t = Mathf.Clamp01((u * (0.45f + delay) - delay) / 0.45f);
                    if (t <= 0f) return;
                    float e = Tw.Eval(Ease.OutBack, t);
                    Vector2 mid = variant == 0 ? (from + to) * 0.5f + new Vector2(0f, 220f) + side
                        : variant == 1 ? from + new Vector2(side.x * 2f, 140f) : (from + to) * 0.5f + new Vector2(side.x, -60f);
                    Vector2 a = Vector2.Lerp(from, mid, t), b2 = Vector2.Lerp(mid, to, t);
                    rt.anchoredPosition = Vector2.Lerp(a, b2, Mathf.Clamp01(e));
                    rt.localScale = Vector3.one * (t < 0.15f ? t / 0.15f : 1f - 0.3f * t);
                    rt.localRotation = Quaternion.Euler(0, 0, variant == 1 ? t * 360f : Mathf.Sin(t * 10f) * 12f);
                    var gr = ghost.rectTransform;
                    gr.anchoredPosition = Vector2.Lerp(gr.anchoredPosition, rt.anchoredPosition, 0.35f);
                    ghost.color = new Color(trail.r, trail.g, trail.b, 0.6f * Mathf.Sin(t * Mathf.PI));
                }, () =>
                {
                    UiPool.Release(img);
                    UiPool.Release(ghost);
                    Squash(chip);
                    Sfx.Play("clink", -10f, PentaPitch(chainNow + idx));
                });
            }
            Popup(game.PlotWorld(p) + Vector3.up * (game.PlotHeight(p) + 2f), "+" + n + " " + Island.RDef((Res)res).Name, Kit.Cream, 26);
        }

        // ------------------------------------------------------------ piezas de las hojas
        /// <summary>Fila de materiales: icono + cantidad (roja si falta). Devuelve el ancho usado.</summary>
        float MatsRow(Transform parent, float x, float y, List<KeyValuePair<Res, int>> mats, float size = 40f)
        {
            float cx = x;
            foreach (var kv in mats)
            {
                var r = kv.Key; int need = kv.Value;
                var ic = Kit.Img(parent, ResIcons.Get(r), Color.white, "Mat");
                ic.preserveAspect = true;
                Kit.PlaceTL(ic.rectTransform, cx, y, size, size);
                var l = Kit.LabelAt(parent, "", (int)(size * 0.55f), Kit.Brown, 0, true, cx + size, y + size * 0.15f, 90, size * 0.7f, TextAnchor.MiddleLeft);
                sheetRefresh.Add(() =>
                {
                    int have = Isl.Stock[(int)r];
                    string s = have + "/" + need;
                    if (l.text != s) l.text = s;
                    l.color = have >= need ? Kit.GreenD : MissingCol;
                });
                cx += size + 10f + 16f * (need.ToString().Length + 2);
            }
            return cx - x;
        }

        /// <summary>Reloj + duracion (la fuente no trae el simbolo de reloj).</summary>
        void TimeTag(Transform parent, float x, float y, string text, int size)
        {
            var ic = Kit.Img(parent, Icons.Get("clock"), Color.white, "Reloj");
            Kit.PlaceTL(ic.rectTransform, x, y, size + 6, size + 6);
            var l = Kit.LabelAt(parent, text, size, Kit.Brown, 0, false, x + size + 10, y + 2, 240, size + 4);
            l.alignment = TextAnchor.MiddleLeft;
        }

        float sectionSum;

        RectTransform Section(Transform scrollContent, float h, string name = "Seccion")
        {
            sectionSum += h + 12f;
            var r = Kit.New(name, scrollContent);
            Kit.Item(r, -1f, h);
            return r;
        }

        // ------------------------------------------------------------ hoja de un edificio
        Plot cityPlot;
        string citySig;
        Transform cityFrame;
        const float CitySheetH = 1060f;

        /// <summary>Abre la hoja del edificio (o el catalogo si la parcela esta libre).</summary>
        public void OpenBuilding(Plot p)
        {
            // sin foto borrosa: el edificio queda vivo y a la vista arriba de la hoja (la camara lo sube)
            cityFrame = OpenSheet(CitySheetH, false, 680f, 0.12f, false);
            game.FocusOn(game.PlotWorld(p), false, 0.84f);
            cityPlot = p;
            citySig = CitySig(p);
            FillBuilding();
        }

        string CitySig(Plot p)
        {
            var sb = new StringBuilder();
            sb.Append(p.Building).Append('|').Append(p.Level).Append('|').Append(p.Work > 0).Append('|').Append(p.Queue.Count).Append('|')
              .Append(p.Ready > 0).Append(p.ReadyRes).Append('|').Append(Isl.Th);
            if (p.Building == (int)BKind.Train) { sb.Append(Isl.TrainHere); foreach (var w in Isl.Wagons) sb.Append(w.Done ? 1 : 0); }
            if (p.Building == (int)BKind.Market) { foreach (var o in Isl.Merchant) sb.Append(o.Sold ? 1 : 0).Append((int)o.What); for (int i = 0; i < Isl.Stock.Length; i++) sb.Append(Isl.Stock[i] > 0 ? 1 : 0); }
            if (p.Building == (int)BKind.Barracks) sb.Append('m').Append(Isl.Modules.Count);
            if (p.Building == (int)BKind.Barn || p.Building == (int)BKind.Warehouse) for (int i = 0; i < Isl.Stock.Length; i++) sb.Append(Isl.Stock[i] > 0 ? 1 : 0);
            return sb.ToString();
        }

        void UpdateCitySheet()
        {
            if (sheet == null || cityFrame == null || cityPlot == null) { cityPlot = null; return; }
            string sig = CitySig(cityPlot);
            if (sig == citySig) return;
            citySig = sig;
            for (int i = cityFrame.childCount - 1; i >= 0; i--) Destroy(cityFrame.GetChild(i).gameObject);
            sheetRefresh.Clear();
            FillBuilding();
        }

        void FillBuilding()
        {
            var p = cityPlot;
            var fr = cityFrame;
            var k = (BKind)p.Building;
            var d = Island.Def(k);
            // cabecera
            var icon = Kit.Img(fr, IslandStage.I.BuildingIcon(k, Island.Tier(Mathf.Max(1, p.Level))), Color.white, "Modelo");
            icon.preserveAspect = true;
            Kit.PlaceTL(icon.rectTransform, 24, 16, 140, 140);
            var nameL = Kit.LabelAt(fr, d.Name, 38, Kit.Brown, 0, true, 170, 24, 400, 48);
            int cap = Isl.LevelCap(k);
            // nivel sin palabras: estrella + "2/3" (o martillo mientras se construye)
            var lvIc = Kit.Icon(fr, p.Level == 0 ? "hammer" : "star", 34);
            Kit.PlaceTL((RectTransform)lvIc.transform, 170, 74, 34, 34);
            string lvTxt = p.Level == 0 ? "…" : p.Level + " / " + (p.Level >= d.MaxLevel ? d.MaxLevel : cap);
            Kit.LabelAt(fr, lvTxt, 28, Kit.OrangeD, 0, true, 210, 72, 200, 38);
            // la descripcion queda detras de la (i): los jugadores no quieren leer, pero esta si la piden
            var desc = Kit.LabelAt(fr, d.Desc, 22, Kit.Brown, 0, false, 170, 112, 480, 56);
            Kit.Wrap(desc);
            desc.gameObject.SetActive(false);
            var info = Kit.RoundImg(fr, 18, new Color(0.45f, 0.36f, 0.28f, 0.9f), "Info");
            info.raycastTarget = true;
            Kit.PlaceTL(info.rectTransform, 170 + Mathf.Min(nameL.preferredWidth, 400f) + 12f, 32, 36, 36);
            var il = Kit.Label(info.transform, "i", 24, Color.white, 0, true, TextAnchor.MiddleCenter);
            Kit.Stretch(il.rectTransform);
            info.gameObject.AddComponent<Btn>().Clicked += () => { desc.gameObject.SetActive(!desc.gameObject.activeSelf); if (desc.gameObject.activeSelf) Tw.Pop(desc.rectTransform, 1.05f); };
            var famTag = Kit.MakeTag(fr, FamilyName(d.Family), IslandArt.FamilyRoof(k), 16);
            Kit.PlaceTL(famTag, 26, 150, 136, 28);
            if (Isl.Movable(p) && p.Level >= 1)
            {
                // mover el edificio (tambien se puede manteniendolo apretado en la isla)
                var mv = Kit.Button(fr, "", Kit.Blue, 20, 64, 56, Loc.T("Mover"));
                Kit.Place((RectTransform)mv.transform, 1f, 0f, -84f, 18f, 64, 56);
                var mvi = Kit.Icon(mv.Content, "expand", 40);   // flechas en cruz = mover
                Kit.PlaceTL((RectTransform)mvi.transform, 12, 6, 40, 40);
                mvi.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                var pp = p;
                mv.Clicked += () => { CloseSheet(); game.BeginMove(pp); };
            }

            // cuerpo con desplazamiento
            var sc = Kit.Scroll(fr, 12f, "Cuerpo");
            Kit.PlaceTL((RectTransform)sc.Scroll.transform, 20, 190, 640, CitySheetH - 190 - 210);
            var body = sc.Content;
            sectionSum = 0f;
            if (p.Work > 0) WorkSection(body, p);
            if (p.Level >= 1)
            {
                if (Island.Extractor(p.Building) != null) ExtractSection(body, p);
                if (Island.Produces(p.Building)) RecipeSections(body, p);
                if (k == BKind.Depot) ThSection(body);
                if (k == BKind.Market) MarketSection(body);
                if (k == BKind.Train) TrainSection(body);
                if (k == BKind.Barn) StorageSection(body, true);
                if (k == BKind.Warehouse) StorageSection(body, false);
                if (k == BKind.Barracks) BarracksSection(body, p);
                string eff = Effect(k, p.Level, p.Level >= d.MaxLevel);
                if (eff != "")
                {
                    var s = Section(body, 50);
                    var el = Kit.LabelAt(s, eff, 26, Kit.GreenD, 0, true, 10, 6, 620, 38);
                    el.alignment = TextAnchor.MiddleLeft;
                }
            }
            // barra de abajo: mejorar
            UpgradeBar(fr, p);
            // la hoja mide lo que mide su contenido (entre 560 y CitySheetH)
            float h = Mathf.Clamp(190f + sectionSum + 226f, 520f, CitySheetH);
            var frt = (RectTransform)fr;
            frt.sizeDelta = new Vector2(frt.sizeDelta.x, h);
            Kit.PlaceTL((RectTransform)sc.Scroll.transform, 20, 190, 640, h - 190 - 216);
        }

        static string FamilyName(BFamily f)
        {
            switch (f)
            {
                case BFamily.Extraction: return Loc.T("Extracción");
                case BFamily.Transform: return Loc.T("Transformación");
                case BFamily.Town: return Loc.T("Pueblo");
                case BFamily.Commerce: return Loc.T("Comercio");
                default: return Loc.T("Especial");
            }
        }

        void WorkSection(Transform body, Plot p)
        {
            var s = Section(body, 206, "Obra");
            var box = Kit.OutBox(s, 18, 4, 5, Kit.Cream, Kit.Out, "Caja");
            Kit.Stretch(box, 4, 4, 4, 4);
            var ic = Kit.Img(box, Icons.Get("hammer"), Color.white, "Icono");
            Kit.PlaceTL(ic.rectTransform, 14, 14, 56, 56);
            var tStar = Kit.Icon(box, p.Level == 0 ? "hammer" : "star", 30);
            Kit.PlaceTL((RectTransform)tStar.transform, 80, 14, 30, 30);
            var title = Kit.LabelAt(box, p.Level == 0 ? "…" : (p.Level + 1).ToString(), 26, Kit.Brown, 0, true, 116, 12, 260, 34);
            var left = Kit.LabelAt(box, "", 30, Kit.OrangeD, 0, true, 80, 44, 300, 38);
            var bar = Kit.OutBox(box, 10, 3, 0, Icons.H("d8c6a2"), Kit.Out, "Barra");
            Kit.PlaceTL(bar, 16, 92, 600, 26);
            var fill = Kit.RoundImg(bar, 8, Kit.Orange, "Relleno");
            fill.rectTransform.anchorMin = new Vector2(0f, 0f); fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = new Vector2(3, 3); fill.rectTransform.offsetMax = new Vector2(3, -3);
            var b = Kit.Button(box, "", Kit.Green, 30, 220, 76);
            Kit.PlaceTL((RectTransform)b.transform, 390, 8, 220, 76);
            var finI = Kit.Icon(b.Content, "speed", 44);
            Kit.PlaceTL((RectTransform)finI.transform, 16, 12, 44, 44);
            b.Label.alignment = TextAnchor.MiddleCenter;
            Kit.Stretch(b.Label.rectTransform, 60, 0, 10, 4);
            b.Clicked += () =>
            {
                if (Isl.SpeedUp(p))
                {
                    Sfx.Play("tadaa", -4f); Mineros.Fx.Haptics.Success(); game.Save();
                    Vector2 c = ToCanvas(game.PlotWorld(p) + Vector3.up * 1.5f);
                    FlyTrail(LayerPos(gemPill.Root), new Vector2(c.x, -c.y), Icons.Get("gem"), 4);   // las gemas van a la obra
                    CloseSheet();   // se ve terminar la obra (y la evolucion), no detras de la hoja
                    game.FocusOn(game.PlotWorld(p));
                }
                else { NoMoney(b); Toast(Loc.T("Te faltan gemas (metas, barcos, vetas gigantes, tren)"), Kit.Gray); }
            };
            int skinFree = -1;
            sheetRefresh.Add(() =>
            {
                left.text = Clock(p.Work);
                float f = p.WorkTotal > 0 ? Mathf.Clamp01(1f - (float)(p.Work / p.WorkTotal)) : 1f;
                fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(20f, 594f * f), fill.rectTransform.sizeDelta.y);
                int g = Isl.SpeedUpGems(p);
                // terminar ya: rayo + "GRATIS" o rayo + gema + numero
                string t = g == 0 ? "FREE" : g.ToString();
                if (b.Label.text != t) b.Label.text = t;
                if (finI != null) finI.SetKind(g == 0 ? "speed" : "gem");
                int isFree = g == 0 ? 1 : 0;
                if (isFree != skinFree) { skinFree = isFree; b.SetSkin(Kit.Skin(g == 0 ? Kit.Green : Kit.Purple)); }
            });
            AddShine(b, () => Isl.SpeedUpGems(p) == 0);
            var hint = Kit.LabelAt(box, Loc.T("Las obras de 5 minutos o menos se terminan gratis."), 18, Kit.Brown, 0, false, 16, 134, 360, 52);
            Kit.Wrap(hint);
            hint.alignment = TextAnchor.MiddleLeft;
            hint.gameObject.SetActive(Isl.Stat("speedups") < 2);   // se explica las primeras veces, despues se sabe
            if (p.Work > Island.FreeFinish && Isl.CanAd(AdPlace.WorkCut, IslandGame.Today, Now))
            {
                var ad = AdButton(box, Loc.T("-30 min"), 220, 56, 22);
                Kit.PlaceTL((RectTransform)ad.transform, 390, 128, 220, 56);
                ad.Clicked += () => WatchAd(AdPlace.WorkCut, p, 0, null);
            }
        }

        void ExtractSection(Transform body, Plot p)
        {
            var ex = Island.Extractor(p.Building);
            var s = Section(body, 150, "Extraccion");
            var box = Kit.OutBox(s, 18, 4, 5, Color.white, Kit.Out, "Caja");
            Kit.Stretch(box, 4, 4, 4, 4);
            var ic = Kit.Img(box, ResIcons.Get(ex.Out), Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.PlaceTL(ic.rectTransform, 12, 12, 76, 76);
            Kit.LabelAt(box, Island.RDef(ex.Out).Name, 28, Kit.Brown, 0, true, 100, 10, 300, 34);
            var info = Kit.LabelAt(box, "", 20, Kit.Brown, 0, false, 100, 44, 300, 28);
            var ready = Kit.LabelAt(box, "", 24, Kit.OrangeD, 0, true, 100, 72, 300, 30);
            var bar = Kit.OutBox(box, 8, 3, 0, Icons.H("d8c6a2"), Kit.Out, "Barra");
            Kit.PlaceTL(bar, 14, 106, 380, 20);
            var fill = Kit.RoundImg(bar, 6, Kit.Green, "Relleno");
            fill.rectTransform.anchorMin = new Vector2(0f, 0f); fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = new Vector2(3, 3); fill.rectTransform.offsetMax = new Vector2(3, -3);
            var b = Kit.Button(box, Loc.T("Cobrar"), Kit.Green, 28, 200, 84);
            Kit.PlaceTL((RectTransform)b.transform, 410, 26, 200, 84);
            b.Clicked += () =>
            {
                if (p.Ready <= 0) { NoMoney(b); return; }
                if (Isl.Collect(p) <= 0) { NoMoney(b); Toast(Loc.T("¡Galpón lleno! Mejoralo o vendé lo que sobra"), Kit.Orange); }
                else game.Save();
            };
            AddShine(b, () => p.Ready > 0);
            sheetRefresh.Add(() =>
            {
                info.text = Isl.ExtractUnits(p) + Loc.T(" cada ") + Dur(ex.Cycle / Isl.ProdSpeed(p)) + (Isl.Level(BKind.Managers) > 0 ? Loc.T("  ·  gerente") : "");
                ready.text = Loc.T("Listo: ") + p.Ready + " / " + Isl.ExtractBuffer(p);
                float f = p.Ready >= Isl.ExtractBuffer(p) ? 1f : Isl.ProdProgress(p);
                fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(14f, 374f * f), fill.rectTransform.sizeDelta.y);
                fill.color = p.Ready >= Isl.ExtractBuffer(p) ? Kit.Orange : Kit.Green;
            });
        }

        void RecipeSections(Transform body, Plot p)
        {
            var rec = Island.RecipesOf((BKind)p.Building);
            // cola: casilleros con lo que se esta haciendo y lo listo
            var qs = Section(body, 136, "Cola");
            var qbox = Kit.OutBox(qs, 18, 4, 5, Kit.Cream, Kit.Out, "Caja");
            Kit.Stretch(qbox, 4, 4, 4, 4);
            Kit.LabelAt(qbox, Loc.T("Producción"), 22, Kit.Brown, 0, true, 14, 6, 300, 28);
            int slots = Isl.QueueSlots(p);
            for (int i = 0; i < 6; i++)
            {
                var cell = Kit.OutBox(qbox, 12, 3, 4, i < slots ? Color.white : Icons.H("d8cdb8"), Kit.Out, "Casillero");
                Kit.PlaceTL(cell, 14 + i * 76, 38, 68, 76);
                if (i >= slots) { var lk = Kit.Icon(cell, "lock", 30); Kit.Place((RectTransform)lk.transform, 0.5f, 0.5f, -15f, -15f, 30, 30); continue; }
                if (i < p.Queue.Count)
                {
                    var ic = Kit.Img(cell, ResIcons.Get(Island.Recipes[p.Queue[i]].Out), Color.white, "Icono");
                    ic.preserveAspect = true;
                    Kit.PlaceTL(ic.rectTransform, 8, 6, 52, 52);
                    if (i == 0)
                    {
                        var bar = Kit.RoundImg(cell, 4, Kit.Green, "Progreso");
                        bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(0f, 0f);
                        bar.rectTransform.pivot = new Vector2(0f, 0f);
                        bar.rectTransform.anchoredPosition = new Vector2(6, 8);
                        sheetRefresh.Add(() => bar.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, 56f * Isl.ProdProgress(p)), 8));
                    }
                }
            }
            // listo para cobrar
            var rb = Kit.Button(qbox, "", Kit.Green, 24, 150, 76);
            Kit.PlaceTL((RectTransform)rb.transform, 470, 38, 150, 76);
            rb.Clicked += () =>
            {
                if (p.Ready <= 0) { NoMoney(rb); return; }
                if (Isl.Collect(p) <= 0) { NoMoney(rb); Toast(Loc.T("¡Almacén lleno! Mejoralo o vendé lo que sobra"), Kit.Orange); }
                else game.Save();
            };
            AddShine(rb, () => p.Ready > 0);
            sheetRefresh.Add(() =>
            {
                string t = p.Ready > 0 ? "+" + p.Ready : (p.Queue.Count > 0 ? Clock(Island.Recipes[p.Queue[0]].Time / Isl.ProdSpeed(p) * (1f - Isl.ProdProgress(p))) : "");
                if (rb.Label.text != t) rb.Label.text = t;
                rb.Interactable = p.Ready > 0;
            });
            // recetas: primero lo que hace falta para el proximo Ayuntamiento (con "!" y cuantas faltan)
            rec = new List<int>(rec);
            rec.Sort((a, c) => (Isl.NeedFor(Island.Recipes[c].Out) > 0 ? 1 : 0).CompareTo(Isl.NeedFor(Island.Recipes[a].Out) > 0 ? 1 : 0));
            foreach (int ri in rec)
            {
                var rc = Island.Recipes[ri];
                bool known = Island.RDef(rc.Out).Th <= Isl.Th;
                int need = known ? Isl.NeedFor(rc.Out) : 0;
                var s = Section(body, 128, "Receta");
                var box = Kit.OutBox(s, 18, 4, 5, known ? Color.white : Icons.H("e2d8c4"), Kit.Out, "Caja");
                Kit.Stretch(box, 4, 4, 4, 4);
                var ic = Kit.Img(box, ResIcons.Get(rc.Out), known ? Color.white : new Color(0.3f, 0.25f, 0.2f, 0.7f), "Producto");
                ic.preserveAspect = true;
                Kit.PlaceTL(ic.rectTransform, 12, 14, 84, 84);
                Kit.LabelAt(box, Island.RDef(rc.Out).Name, 26, Kit.Brown, 0, true, 104, 8, 300, 32);
                if (need > 0)
                {
                    // hace falta: "!" rojo + el Ayuntamiento chiquito + cuantas faltan
                    var nb = Kit.RoundImg(box, 16, Kit.Red, "HaceFalta");
                    Kit.PlaceTL(nb.rectTransform, 2, 2, 34, 34);
                    var nl = Kit.Label(nb.transform, "!", 24, Color.white, 4, true, TextAnchor.MiddleCenter);
                    Kit.Stretch(nl.rectTransform);
                    Tw.To(nb.rectTransform, "late", 999f, Ease.Linear, u => nb.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 5f) * 0.08f));
                    var th = Kit.Img(box, IslandStage.I.BuildingIcon(BKind.Depot, Island.Tier(Mathf.Max(1, Isl.Th))), Color.white, "Ayuntamiento");
                    th.preserveAspect = true;
                    Kit.PlaceTL(th.rectTransform, 330, 2, 40, 40);
                    Kit.LabelAt(box, "x" + need, 22, Kit.Red, 0, true, 372, 6, 60, 34);
                }
                var mats = new List<KeyValuePair<Res, int>>();
                for (int i = 0; i < rc.In.Length; i++) mats.Add(new KeyValuePair<Res, int>(rc.In[i], rc.InN[i]));
                MatsRow(box, 104, 44, mats, 36f);
                TimeTag(box, 104, 82, Dur(rc.Time / Isl.ProdSpeed(p)), 20);
                if (!known)
                {
                    Kit.LabelAt(box, Loc.T("Ayuntamiento ") + Island.RDef(rc.Out).Th, 22, Kit.OrangeD, 0, true, 420, 40, 190, 40, TextAnchor.MiddleCenter);
                    continue;
                }
                // fabricar: icono de "play" (sin palabra)
                var b = Kit.Button(box, "", Kit.Green, 28, 130, 80);
                Kit.PlaceTL((RectTransform)b.transform, 480, 20, 130, 80);
                var pi = Kit.Icon(b.Content, "play", 46);
                Kit.Place((RectTransform)pi.transform, 0.5f, 0.5f, -23f, -27f, 46, 46);
                int rid = ri;
                b.Clicked += () =>
                {
                    if (Isl.QueueRecipe(p, rid))
                    {
                        Sfx.Play("confirm", -6f); Tw.Pop(b.transform, 1.15f); game.Save();
                        // el producto sale del boton y cae en su casillero de la cola
                        RectTransform slot = null; int k = 0, want = p.Queue.Count - 1;
                        if (cityFrame != null)
                            foreach (var t in cityFrame.GetComponentsInChildren<RectTransform>())
                                if (t.name == "Casillero") { if (k == want) { slot = t; break; } k++; }
                        if (slot != null) FlyTrail(LayerPos((RectTransform)b.transform), LayerPos(slot), ResIcons.Get(Island.Recipes[rid].Out), 1);
                        Mineros.Fx.Haptics.Light();
                    }
                    else
                    {
                        NoMoney(b);
                        Toast(p.Queue.Count >= Isl.QueueSlots(p) ? Loc.T("La cola está llena: mejorá el edificio para más lugares") : Loc.T("Te faltan ingredientes"), Kit.Gray);
                        if (p.Queue.Count < Isl.QueueSlots(p)) MissingPop((RectTransform)b.transform, mats);
                    }
                };
                AddShine(b, () => Isl.CanQueue(p, rid));
                sheetRefresh.Add(() =>
                {
                    Color m = Isl.CanQueue(p, rid) ? Color.white : new Color(0.72f, 0.72f, 0.75f);
                    if (b.Modulate != m) { b.Modulate = m; b.Restyle(); }
                });
            }
        }

        void ThSection(Transform body)
        {
            int next = Isl.Th + 1;
            var list = new List<BDef>();
            foreach (var d in Island.Defs) if (d.Th == next) list.Add(d);
            var extras = new List<string>();
            if (next == 5) extras.Add(Loc.T("+1 constructor"));
            for (int i = 0; i < Island.ExpandTh.Length; i++) if (Island.ExpandTh[i] == next) extras.Add(Loc.T("Ampliar la isla"));
            foreach (var d in Island.Defs) if (Island.Doubles(d.Kind) && d.Th + 3 == next) extras.Add(Loc.T("2.º ") + d.Name);
            foreach (var r in Island.ResDefs) if (r.Th == next) extras.Add(r.Name);
            if (next > Island.MaxTh) { var e = Section(body, 60); Kit.LabelAt(e, Loc.T("¡Ayuntamiento al máximo!"), 28, Kit.GreenD, 0, true, 0, 10, 620, 40, TextAnchor.MiddleCenter); return; }
            float rows = Mathf.Ceil(list.Count / 4f);
            var s = Section(body, 60 + rows * 150 + (extras.Count > 0 ? 70 : 0), "Desbloquea");
            var box = Kit.OutBox(s, 18, 4, 5, Kit.Cream, Kit.Out, "Caja");
            Kit.Stretch(box, 4, 4, 4, 4);
            Kit.LabelAt(box, Loc.T("El nivel ") + next + Loc.T(" desbloquea:"), 26, Kit.Brown, 0, true, 16, 8, 600, 34);
            for (int i = 0; i < list.Count; i++)
            {
                var cell = Kit.OutBox(box, 14, 3, 4, Color.white, IslandArt.FamilyRoof(list[i].Kind), "Edificio");
                Kit.PlaceTL(cell, 14 + (i % 4) * 152, 50 + (i / 4) * 150, 144, 140);
                var ic = Kit.Img(cell, IslandStage.I.BuildingIcon(list[i].Kind, 1), Color.white, "Icono");
                ic.preserveAspect = true;
                Kit.PlaceTL(ic.rectTransform, 22, 6, 100, 96);
                var nl = Kit.LabelAt(cell, list[i].Name, 18, Kit.Brown, 0, true, 4, 102, 136, 32, TextAnchor.MiddleCenter);
                nl.resizeTextForBestFit = true; nl.resizeTextMinSize = 12; nl.resizeTextMaxSize = 18;
            }
            if (extras.Count > 0)
            {
                var el = Kit.LabelAt(box, Loc.T("Y además: ") + string.Join(" · ", extras.ToArray()), 20, Kit.OrangeD, 0, true, 16, 56 + rows * 150, 600, 56);
                Kit.Wrap(el);
            }
        }

        void MarketSection(Transform body)
        {
            // mercader
            var s = Section(body, 230, "Mercader");
            var box = Kit.OutBox(s, 18, 4, 5, Kit.Cream, Kit.Out, "Caja");
            Kit.Stretch(box, 4, 4, 4, 4);
            Kit.LabelAt(box, Loc.T("El mercader vende"), 24, Kit.Brown, 0, true, 16, 8, 360, 32);
            var tl = Kit.LabelAt(box, "", 20, Kit.OrangeD, 0, true, 250, 10, 170, 30, TextAnchor.MiddleRight);
            sheetRefresh.Add(() => tl.text = Loc.T("en ") + Clock(Isl.MerchantT));
            if (Isl.CanAd(AdPlace.Merchant, IslandGame.Today, Now))
            {
                var ad = AdButton(box, Loc.T("Renovar"), 186, 40, 18);
                Kit.PlaceTL((RectTransform)ad.transform, 430, 4, 186, 40);
                ad.Clicked += () => WatchAd(AdPlace.Merchant, null, 0, null);
            }
            for (int i = 0; i < Isl.Merchant.Count && i < 3; i++)
            {
                var o = Isl.Merchant[i];
                var cell = Kit.OutBox(box, 14, 3, 4, o.Sold ? Icons.H("cde6b0") : Color.white, Kit.Out, "Oferta");
                Kit.PlaceTL(cell, 14 + i * 204, 46, 196, 168);
                var ic = Kit.Img(cell, ResIcons.Get(o.What), Color.white, "Icono");
                ic.preserveAspect = true;
                Kit.PlaceTL(ic.rectTransform, 16, 8, 64, 64);
                Kit.LabelAt(cell, "x" + o.Count, 28, Kit.Brown, 0, true, 86, 20, 100, 40);
                if (o.Sold) { Kit.LabelAt(cell, Loc.T("¡Comprado!"), 22, Kit.GreenD, 0, true, 0, 96, 196, 40, TextAnchor.MiddleCenter); continue; }
                var b = Kit.Button(cell, BigNum.Fmt(o.Price), Kit.Green, 22, 172, 66);
                Kit.PlaceTL((RectTransform)b.transform, 12, 86, 172, 66);
                int idx = i;
                b.Clicked += () => { if (Isl.BuyOffer(idx)) { Sfx.Play("coins_pour", -6f); game.Save(); } else NoMoney(b); };
            }
            // vender lo que sobra
            var have = new List<int>();
            for (int i = 0; i < Isl.Stock.Length; i++) if (Isl.Stock[i] > 0) have.Add(i);
            var s2 = Section(body, 52 + Mathf.Max(1, have.Count) * 76, "Vender");
            var b2 = Kit.OutBox(s2, 18, 4, 5, Color.white, Kit.Out, "Caja");
            Kit.Stretch(b2, 4, 4, 4, 4);
            Kit.LabelAt(b2, Loc.T("Vender (80 % del valor)"), 24, Kit.Brown, 0, true, 16, 8, 600, 32);
            if (have.Count == 0) Kit.LabelAt(b2, Loc.T("No tenés nada para vender."), 22, Kit.Brown, 0, false, 16, 50, 600, 30);
            for (int j = 0; j < have.Count; j++) SellRow(b2, have[j], 46 + j * 76);
        }

        void SellRow(Transform box, int r, float y)
        {
            var ic = Kit.Img(box, ResIcons.Get(r), Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.PlaceTL(ic.rectTransform, 14, y, 60, 60);
            var nl = Kit.LabelAt(box, "", 22, Kit.Brown, 0, true, 82, y + 4, 230, 30);
            var pl = Kit.LabelAt(box, "", 18, Kit.OrangeD, 0, true, 82, y + 32, 230, 26);
            var b1 = Kit.Button(box, "x1", Kit.Blue, 22, 90, 60);
            Kit.PlaceTL((RectTransform)b1.transform, 330, y, 90, 60);
            var b10 = Kit.Button(box, "x10", Kit.Blue, 22, 90, 60);
            Kit.PlaceTL((RectTransform)b10.transform, 426, y, 90, 60);
            var ball = Kit.Button(box, Loc.T("Todo"), Kit.Orange, 22, 90, 60);
            Kit.PlaceTL((RectTransform)ball.transform, 522, y, 90, 60);
            System.Action<int> sell = n =>
            {
                double v = Isl.Sell((Res)r, n);
                if (v > 0) { Sfx.Play("coin", -6f); CoinsFrom(game.PlotWorld(cityPlot ?? Isl.Plots[0]) + Vector3.up * 2f, v); game.Save(); }
            };
            b1.Clicked += () => sell(1);
            b10.Clicked += () => sell(10);
            ball.Clicked += () => sell(Isl.Stock[r]);
            sheetRefresh.Add(() =>
            {
                nl.text = Island.ResDefs[r].Name + "  x" + Isl.Stock[r];
                pl.text = BigNum.Fmt(Isl.SellPrice((Res)r)) + Loc.T(" c/u");
            });
        }

        void StorageSection(Transform body, bool raw)
        {
            var have = new List<int>();
            for (int i = 0; i < Isl.Stock.Length; i++) if (Island.ResDefs[i].Raw == raw && Island.ResDefs[i].Th <= Isl.Th) have.Add(i);
            int rows = Mathf.CeilToInt(have.Count / 4f);
            var s = Section(body, 60 + rows * 136, "Guardado");
            var box = Kit.OutBox(s, 18, 4, 5, Kit.Cream, Kit.Out, "Caja");
            Kit.Stretch(box, 4, 4, 4, 4);
            var cap = Kit.LabelAt(box, "", 24, Kit.Brown, 0, true, 16, 8, 600, 32);
            sheetRefresh.Add(() => cap.text = (raw ? Loc.T("Materias primas: ") + Isl.RawStored() + " / " + Isl.RawCap() : Loc.T("Productos: ") + Isl.ProdStored() + " / " + Isl.ProdCap()));
            for (int j = 0; j < have.Count; j++)
            {
                int r = have[j];
                var cell = Kit.OutBox(box, 12, 3, 4, Color.white, Kit.Out, "Celda");
                Kit.PlaceTL(cell, 14 + (j % 4) * 152, 48 + (j / 4) * 136, 144, 128);
                var ic = Kit.Img(cell, ResIcons.Get(r), Color.white, "Icono");
                ic.preserveAspect = true;
                Kit.PlaceTL(ic.rectTransform, 40, 6, 64, 64);
                var l = Kit.LabelAt(cell, "", 22, Kit.Brown, 0, true, 0, 70, 144, 28, TextAnchor.MiddleCenter);
                var nm = Kit.LabelAt(cell, Island.ResDefs[r].Name, 15, Kit.Brown, 0, false, 2, 98, 140, 24, TextAnchor.MiddleCenter);
                nm.resizeTextForBestFit = true; nm.resizeTextMinSize = 10; nm.resizeTextMaxSize = 15;
                sheetRefresh.Add(() => { string t = Isl.Stock[r].ToString(); if (l.text != t) l.text = t; });
            }
            var hint = Section(body, 44);
            Kit.LabelAt(hint, Isl.Level(BKind.Market) > 0 ? Loc.T("Vendé lo que sobra en el Mercado (80 %).") : Loc.T("Sin Mercado, tocá un recurso en el Mercado futuro… o vendelo acá al 40 %:"), 18, Kit.Brown, 0, false, 10, 6, 620, 32);
            if (Isl.Level(BKind.Market) <= 0)
            {
                var stocked = new List<int>();
                foreach (int r in have) if (Isl.Stock[r] > 0) stocked.Add(r);
                var s2 = Section(body, 20 + Mathf.Max(1, stocked.Count) * 76, "Vender");
                var b2 = Kit.OutBox(s2, 18, 4, 5, Color.white, Kit.Out, "Caja");
                Kit.Stretch(b2, 4, 4, 4, 4);
                for (int j = 0; j < stocked.Count; j++) SellRow(b2, stocked[j], 10 + j * 76);
            }
        }

        void TrainSection(Transform body)
        {
            var s = Section(body, Isl.TrainHere ? 290 : 100, "Tren");
            var box = Kit.OutBox(s, 18, 4, 5, Kit.Cream, Kit.Out, "Caja");
            Kit.Stretch(box, 4, 4, 4, 4);
            var tl = Kit.LabelAt(box, "", 26, Kit.Brown, 0, true, 16, 10, 600, 34);
            sheetRefresh.Add(() => tl.text = Isl.TrainHere ? Loc.T("El tren se va en ") + Clock(Isl.TrainT) : Loc.T("El tren llega en ") + Clock(Isl.TrainT));
            if (!Isl.TrainHere)
            {
                Kit.LabelAt(box, Loc.T("Cargá los 3 vagones y llevás monedas, gemas y un cofre."), 20, Kit.Brown, 0, false, 16, 50, 600, 30);
                return;
            }
            for (int i = 0; i < Isl.Wagons.Count; i++)
            {
                var w = Isl.Wagons[i];
                var cell = Kit.OutBox(box, 14, 3, 4, w.Done ? Icons.H("cde6b0") : Color.white, Kit.Out, "Vagon");
                Kit.PlaceTL(cell, 14 + i * 204, 52, 196, 222);
                var ic = Kit.Img(cell, ResIcons.Get(w.Want), Color.white, "Icono");
                ic.preserveAspect = true;
                Kit.PlaceTL(ic.rectTransform, 58, 8, 80, 80);
                var hl = Kit.LabelAt(cell, "", 22, Kit.Brown, 0, true, 0, 88, 196, 30, TextAnchor.MiddleCenter);
                Kit.LabelAt(cell, "+" + BigNum.Fmt(w.Coins), 20, Kit.OrangeD, 0, true, 0, 116, 196, 28, TextAnchor.MiddleCenter);
                if (w.Done) { Kit.LabelAt(cell, Loc.T("¡Cargado!"), 22, Kit.GreenD, 0, true, 0, 150, 196, 40, TextAnchor.MiddleCenter); continue; }
                var b = Kit.Button(cell, Loc.T("Cargar"), Kit.Green, 24, 172, 62);
                Kit.PlaceTL((RectTransform)b.transform, 12, 148, 172, 62);
                int idx = i;
                b.Clicked += () => { if (Isl.LoadWagon(idx)) { Sfx.Play("thud", -6f, 1.2f); Sfx.Play("coins_pour", -8f); game.Save(); } else { NoMoney(b); Toast(Loc.T("Te faltan ") + Island.RDef(w.Want).Name.ToLower(), Kit.Gray); } };
                AddShine(b, () => Isl.Stock[(int)w.Want] >= w.Count);
                var ww = w;
                sheetRefresh.Add(() => { hl.text = Mathf.Min(Isl.Stock[(int)ww.Want], ww.Count) + " / " + ww.Count; hl.color = Isl.Stock[(int)ww.Want] >= ww.Count ? Kit.GreenD : MissingCol; });
            }
        }

        void UpgradeBar(Transform fr, Plot p)
        {
            var k = (BKind)p.Building;
            var d = Island.Def(k);
            var bar = Kit.OutBox(fr, 18, 4, 5, Kit.Cream, Kit.Out, "Mejorar");
            Kit.Place(bar, 0f, 1f, 20f, -206f, 640, 180);
            if (p.Level == 0)
            {
                var hi = Kit.Icon(bar, "hammer", 70);
                Kit.Place((RectTransform)hi.transform, 0.5f, 0f, -35f, 50f, 70, 70);
                return;
            }
            if (p.Level >= d.MaxLevel)
            {
                // nivel maximo: sello + MAX
                var mi = Kit.Icon(bar, "stamp", 84);
                Kit.Place((RectTransform)mi.transform, 0.5f, 0f, -110f, 46f, 84, 84);
                Kit.LabelAt(bar, "MAX", 44, Kit.GreenD, 0, true, 330, 60, 200, 56, TextAnchor.MiddleLeft);
                return;
            }
            if (p.Level >= Isl.LevelCap(k))
            {
                // tope: candado + el Ayuntamiento (se entiende sin leer: "subí el Ayuntamiento")
                var li = Kit.Icon(bar, "lock", 64);
                Kit.Place((RectTransform)li.transform, 0.5f, 0f, -150f, 56f, 64, 64);
                var ai = Kit.Icon(bar, "arrow", 44);
                Kit.Place((RectTransform)ai.transform, 0.5f, 0f, -60f, 66f, 44, 44);
                var th = Kit.Img(bar, IslandStage.I.BuildingIcon(BKind.Depot, Island.Tier(Mathf.Max(1, Isl.Th))), Color.white, "Ayuntamiento");
                th.preserveAspect = true;
                Kit.Place(th.rectTransform, 0.5f, 0f, 0f, 24f, 130, 130);
                return;
            }
            // mejorar: flecha + estrella + el nivel que viene
            var upI = Kit.Icon(bar, "arrow", 32);
            Kit.PlaceTL((RectTransform)upI.transform, 16, 8, 32, 32);
            var upS = Kit.Icon(bar, "star", 30);
            Kit.PlaceTL((RectTransform)upS.transform, 50, 9, 30, 30);
            Kit.LabelAt(bar, (p.Level + 1).ToString(), 28, Kit.Brown, 0, true, 84, 6, 100, 36);
            TimeTag(bar, 16, 44, Dur(Isl.WorkSeconds(k, p.Level + 1)), 20);
            MatsRow(bar, 16, 84, Isl.MatsFor(k, p.Level + 1), 44f);
            var b = Kit.Button(bar, "", Kit.Green, 28, 230, 100);
            Kit.PlaceTL((RectTransform)b.transform, 396, 40, 230, 100);
            var coin = Kit.Icon(b.Content, "coin", 36);
            Kit.PlaceTL((RectTransform)coin.transform, 14, 26, 36, 36);
            b.Label.alignment = TextAnchor.MiddleRight;
            Kit.Stretch(b.Label.rectTransform, 50, 0, 14, 4);
            b.Clicked += () =>
            {
                if (Isl.Upgrade(p))
                {
                    game.Save(); Tw.Pop(b.transform, 1.15f); Sfx.Play("build", -5f, 1.1f);
                    // el pago sale del boton y vuela a la obra: las monedas tambien se ven irse
                    Vector2 from = LayerPos((RectTransform)b.transform);
                    Vector2 c = ToCanvas(game.PlotWorld(p) + Vector3.up * 1.5f);
                    FlyTrail(from, new Vector2(c.x, -c.y), Icons.Get("coin"), 5);
                    Mineros.Fx.Haptics.Medium();
                    // VER -> TOCAR -> VERLA SUCEDER: la hoja se va y la camara muestra la obra (antes pasaba detras del vidrio)
                    CloseSheet();
                    game.FocusOn(game.PlotWorld(p));
                }
                else
                {
                    NoMoney(b);
                    if (p.Work > 0) Toast(Loc.T("Ya se está mejorando"), Kit.Gray);
                    else if (Isl.FreeBuilders() <= 0) Toast(Loc.T("No hay constructores libres"), Kit.Gray);
                    else if (!Isl.HasMats(Isl.MatsFor(k, p.Level + 1))) { Toast(Loc.T("Te faltan materiales"), Kit.Gray); MissingPop((RectTransform)b.transform, Isl.MatsFor(k, p.Level + 1)); }
                    else Toast(Loc.T("Te faltan monedas"), Kit.Gray);
                }
            };
            AddShine(b, () => Isl.CanUpgrade(p));
            sheetRefresh.Add(() =>
            {
                b.Label.text = BigNum.Fmt(Isl.UpgradeCost(p));
                Color m = Isl.CanUpgrade(p) ? Color.white : new Color(0.72f, 0.72f, 0.75f);
                if (b.Modulate != m) { b.Modulate = m; b.Restyle(); }
            });
        }

        // ------------------------------------------------------------ catalogo de construccion
        public void OpenCatalog(Plot p) { OpenCatalogDrawer(p); }   // el cajon nuevo (IslandUi.Catalog.cs)

        /// <summary>Lista vertical vieja (queda de referencia).</summary>
        void OpenCatalogList(Plot p)
        {
            cityPlot = null;
            var avail = new List<BDef>();
            var locked = new List<BDef>();
            foreach (var d in Island.Defs)
            {
                if (d.Kind == BKind.Depot) continue;
                if (!Isl.Unlocked(d.Kind)) { locked.Add(d); continue; }
                if (Isl.CountOf(d.Kind) >= Isl.CountCap(d.Kind)) continue;
                avail.Add(d);
            }
            avail.Sort((a, b) =>
            {
                bool ca = Isl.CanBuild(a.Kind, p), cb = Isl.CanBuild(b.Kind, p);
                if (ca != cb) return ca ? -1 : 1;
                return Isl.BuildCost(a.Kind).CompareTo(Isl.BuildCost(b.Kind));
            });
            locked.Sort((a, b) => a.Th.CompareTo(b.Th));
            var fr = OpenSheet(1180);
            Kit.LabelAt(fr, Loc.T("Construir"), 40, Kit.Brown, 0, true, 0, 16, 680, 50, TextAnchor.MiddleCenter);
            var bl = Kit.LabelAt(fr, "", 22, Kit.OrangeD, 0, true, 0, 64, 680, 30, TextAnchor.MiddleCenter);
            sheetRefresh.Add(() => bl.text = Loc.T("Constructores libres: ") + Isl.FreeBuilders() + " / " + Isl.Builders() + Loc.T("   ·   Ayuntamiento ") + Isl.Th);
            var sc = Kit.Scroll(fr, 10f, "Lista");
            Kit.PlaceTL((RectTransform)sc.Scroll.transform, 20, 104, 640, 1180 - 124);
            if (avail.Count == 0)
            {
                var s = Section(sc.Content, 70);
                Kit.LabelAt(s, Loc.T("No hay nada nuevo para construir: subí el Ayuntamiento."), 22, Kit.Brown, 0, true, 0, 16, 640, 36, TextAnchor.MiddleCenter);
            }
            foreach (var d in avail) CatalogRow(sc.Content, d, p, false);
            int shownLocked = 0;
            foreach (var d in locked) { if (shownLocked++ >= 8) break; CatalogRow(sc.Content, d, p, true); }
        }

        void CatalogRow(Transform content, BDef d, Plot p, bool locked)
        {
            var row = Section(content, 176, "Fila");
            var card = Kit.Box9(row, "card", new Vector4(12, 12, 12, 16), locked ? new Color(0.88f, 0.86f, 0.82f) : Color.white, "Tarjeta");
            Kit.Stretch(card.rectTransform);
            // tarjeta coleccionable: "escenario" del color de la familia con un resplandor, el edificio en 3D encima
            Color fam = IslandArt.FamilyRoof(d.Kind);
            var stage = Kit.Img(card.transform, ButtonArt.Box(Color.Lerp(fam, Color.white, locked ? 0.75f : 0.55f), 14f, 3f, 0f), Color.white, "Escenario");
            stage.type = Image.Type.Sliced;
            Kit.PlaceTL(stage.rectTransform, 4, 4, 128, 128);
            var halo = Kit.Img(stage.transform, Icons.Glow(), new Color(1f, 1f, 1f, locked ? 0.15f : 0.55f), "Halo");
            Kit.PlaceTL(halo.rectTransform, 4, 4, 120, 120);
            var icon = Kit.Img(card.transform, IslandStage.I.BuildingIcon(d.Kind, 1), locked ? new Color(0.25f, 0.2f, 0.16f, 0.75f) : Color.white, "Modelo");
            icon.preserveAspect = true;
            Kit.PlaceTL(icon.rectTransform, 6, 4, 124, 124);
            // entra en cascada: la fila sube y aparece (sin tocar posiciones: escala y alfa)
            var rowRt = (RectTransform)row;
            int order = content.childCount;
            var cg = row.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            Tw.Alpha(cg, 1f, 0.22f, 0.08f + Mineros.UI.Motion.Delay(order));
            card.rectTransform.localScale = Vector3.one * 0.94f;
            Tw.Scale(card.rectTransform, Vector3.one * 0.94f, Vector3.one, 0.3f, Ease.OutBack, 0.08f + Mineros.UI.Motion.Delay(order));
            if (!locked)
            {
                // tocar la tarjeta la inclina hacia el dedo y vuelve con rebote (se siente fisica)
                var press = card.gameObject.AddComponent<Btn>();
                card.raycastTarget = true;
                press.Juice = false;
                press.PlaySound = false;
                var crt = card.rectTransform;
                press.Down += () =>
                {
                    float side = Input.mousePosition.x < Screen.width * 0.5f ? 1f : -1f;
                    Tw.To(crt, "inclina", 0.1f, Ease.OutQuad, u => { crt.localRotation = Quaternion.Euler(0, 0, side * 2.2f * u); crt.localScale = Vector3.one * (1f - 0.03f * u); });
                    Mineros.Fx.Haptics.Selection();
                };
                press.Up += () => Tw.To(crt, "inclina", 0.3f, Ease.OutBack, u => { crt.localRotation = Quaternion.Euler(0, 0, crt.localEulerAngles.z > 180f ? (crt.localEulerAngles.z - 360f) * (1f - u) : crt.localEulerAngles.z * (1f - u)); crt.localScale = Vector3.one * (0.97f + 0.03f * u); });
                // el edificio "respira" apenas: la tarjeta se siente viva
                var irt = icon.rectTransform;
                float ph = order * 0.7f;
                Tw.To(irt, "respira", 999f, Ease.Linear, u => irt.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 2.2f + ph) * 0.025f));
            }
            var tag = Kit.MakeTag(card.transform, FamilyName(d.Family), IslandArt.FamilyRoof(d.Kind), 14);
            Kit.PlaceTL(tag, 10, 132, 116, 26);
            Kit.LabelAt(card.transform, d.Name, 28, Kit.Brown, 0, true, 134, 8, 300, 36);
            var desc = Kit.LabelAt(card.transform, d.Desc, 18, Kit.Brown, 0, false, 134, 44, 300, 52);
            Kit.Wrap(desc);
            if (locked)
            {
                var lk = Kit.Icon(card.transform, "lock", 44);
                Kit.PlaceTL((RectTransform)lk.transform, 480, 30, 44, 44);
                Kit.LabelAt(card.transform, Loc.T("Ayuntamiento ") + d.Th, 22, Kit.OrangeD, 0, true, 430, 80, 190, 40, TextAnchor.MiddleCenter);
                // tocar una tarjeta bloqueada: el candado tiembla y dice que nivel del Ayuntamiento hace falta
                var tap = card.gameObject.AddComponent<Btn>();
                card.raycastTarget = true;
                tap.PlaySound = false;
                var lrt = (RectTransform)lk.transform;
                tap.Clicked += () =>
                {
                    Tw.To(lrt, "candado", 0.35f, Ease.Linear, u => lrt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 40f) * 18f * (1f - u)), () => lrt.localRotation = Quaternion.identity);
                    Mineros.Fx.Haptics.Warning();
                    Sfx.Play("error", -10f, 1.2f);
                };
                return;
            }
            MatsRow(card.transform, 134, 102, Isl.MatsFor(d.Kind, 1), 34f);
            TimeTag(card.transform, 134, 136, Dur(Isl.WorkSeconds(d.Kind, 1)), 18);
            var b = Kit.Button(card.transform, "", Kit.Green, 26, 180, 86);
            Kit.PlaceTL((RectTransform)b.transform, 440, 36, 180, 86);
            var coin = Kit.Icon(b.Content, "coin", 32);
            Kit.PlaceTL((RectTransform)coin.transform, 12, 22, 32, 32);
            b.Label.alignment = TextAnchor.MiddleRight;
            Kit.Stretch(b.Label.rectTransform, 46, 0, 12, 4);
            var kind = d.Kind;
            b.Clicked += () =>
            {
                Vector2 from = LayerPos(icon.rectTransform);
                if (Isl.CanBuild(kind, p) && Isl.TutDone)
                {
                    // como en Clash of Clans: la tarjeta se da vuelta, se vuelve una chispa que cae en la isla y ahi el
                    // edificio aparece flotando para colocarlo donde quiera el jugador
                    var cardRt = card.rectTransform;
                    Tw.To(cardRt, "vuelta", 0.14f, Ease.InQuad, u => cardRt.localScale = new Vector3(1f - u, 1f + u * 0.06f, 1f));
                    Sfx.Play("card", -6f, 1.1f);
                    Tw.After(this, "compra", 0.14f, () =>
                    {
                        CloseSheet();
                        Mineros.Fx.Haptics.Light();
                        FlySpark(from, game.PlotWorld(p) + Vector3.up * 0.5f, Kit.Yellow, () =>
                        {
                            Mineros.Fx.Haptics.Medium();
                            Sfx.Play("thud", -8f, 1.3f);
                            game.BeginPlace(p, kind, () => { if (Isl.Build(kind, p)) { game.Save(); Sfx.Play("build", -3f); } });
                        });
                    });
                }
                else if (Isl.Build(kind, p)) { CloseSheet(); game.Save(); Sfx.Play("build", -3f); FlyIcon(icon.sprite, from, game.PlotWorld(p) + Vector3.up * 1f, 130f); }
                else
                {
                    NoMoney(b);
                    if (Isl.FreeBuilders() <= 0) Toast(Loc.T("No hay constructores libres: esperá o terminá una obra"), Kit.Gray);
                    else if (!Isl.HasMats(Isl.MatsFor(kind, 1))) { Toast(Loc.T("Te faltan materiales"), Kit.Gray); MissingPop((RectTransform)b.transform, Isl.MatsFor(kind, 1)); }
                    else Toast(Loc.T("Te faltan monedas"), Kit.Gray);
                }
            };
            AddShine(b, () => Isl.CanBuild(kind, p));
            sheetRefresh.Add(() =>
            {
                b.Label.text = BigNum.Fmt(Isl.BuildCost(kind));
                Color m = Isl.CanBuild(kind, p) ? Color.white : new Color(0.72f, 0.72f, 0.75f);
                if (b.Modulate != m) { b.Modulate = m; b.Restyle(); }
            });
        }

        // ------------------------------------------------------------ almacenes (desde el HUD)
        void OpenStorage(bool raw)
        {
            var target = Isl.Find(raw ? BKind.Barn : BKind.Warehouse);
            if (target != null && target.Level >= 1) { OpenBuilding(target); return; }
            cityPlot = null;
            var fr = OpenSheet(900);
            Kit.LabelAt(fr, raw ? Loc.T("Materias primas") : Loc.T("Productos"), 40, Kit.Brown, 0, true, 0, 16, 680, 50, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, raw ? Loc.T("Construí el Galpón para guardar más.") : Loc.T("Construí el Almacén para guardar más."), 22, Kit.OrangeD, 0, true, 0, 66, 680, 30, TextAnchor.MiddleCenter);
            var sc = Kit.Scroll(fr, 10f, "Lista");
            Kit.PlaceTL((RectTransform)sc.Scroll.transform, 20, 104, 640, 900 - 124);
            StorageSection(sc.Content, raw);
        }
    }
}
