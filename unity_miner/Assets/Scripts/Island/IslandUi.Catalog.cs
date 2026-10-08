using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.IslandView
{
    /// <summary>
    /// Catalogo de construccion "tycoon premium" (pedido del creador: "el menu de construccion se puede hacer mucho mas
    /// profesional"): un cajon bajo (la isla y la parcela siguen a la vista arriba, sin vidrio borroso), pestañas por
    /// familia y tarjetas grandes que se deslizan de costado: el edificio en su escenario, nombre, una linea de para que
    /// sirve, materiales, tiempo y el precio en el boton. Lo bloqueado va al final, en silueta con candado y el nivel del
    /// Ayuntamiento que lo abre. Comprar: la tarjeta se da vuelta, una chispa cae en la isla y el edificio queda para
    /// ubicarlo (igual que antes).
    /// </summary>
    public sealed partial class IslandUi
    {
        const float CatH = 660f, CardW = 236f, CardH = 452f;
        int catFamily = -1;   // -1 = todas

        void OpenCatalogDrawer(Plot p)
        {
            cityPlot = null;
            var fr = OpenSheet(CatH, false, 720f, 0.1f, false);
            // (la camara ya no se mueve al abrir el catalogo: el lugar se elige despues, al colocar)
            Kit.LabelAt(fr, Loc.T("Construir"), 38, Kit.Brown, 0, true, 28, 20, 300, 48, TextAnchor.MiddleLeft);
            // constructores libres y nivel del Ayuntamiento, con iconos (sin palabras)
            var hb = Kit.Icon(fr, "hammer", 34);
            Kit.PlaceTL((RectTransform)hb.transform, 400, 26, 34, 34);
            var bl = Kit.LabelAt(fr, "", 26, Kit.OrangeD, 0, true, 438, 24, 90, 40, TextAnchor.MiddleLeft);
            var thI = Kit.Img(fr, IslandStage.I.BuildingIcon(BKind.Depot, Island.Tier(Mathf.Max(1, Isl.Th))), Color.white, "Ayuntamiento");
            thI.preserveAspect = true;
            Kit.PlaceTL(thI.rectTransform, 530, 16, 54, 54);
            var tl = Kit.LabelAt(fr, "", 26, Kit.OrangeD, 0, true, 588, 24, 80, 40, TextAnchor.MiddleLeft);
            sheetRefresh.Add(() => { bl.text = Isl.FreeBuilders() + "/" + Isl.Builders(); tl.text = Isl.Th.ToString(); });

            // pestañas: Todo + cada familia que tenga algo
            var fams = new List<BFamily>();
            foreach (var d in Island.Defs)
            {
                if (d.Kind == BKind.Depot || fams.Contains(d.Family)) continue;
                fams.Add(d.Family);
            }
            var tabs = Kit.New("Pestanas", fr);
            Kit.PlaceTL(tabs, 20, 78, 680, 56);
            var tabBtns = new List<KeyValuePair<int, Btn>>();
            RectTransform strip = null;
            System.Action rebuild = null;
            float tx = 0f;
            System.Action<int, string> addTab = (fam, label) =>
            {
                float w = Mathf.Clamp(label.Length * 13f + 34f, 92f, 170f);
                var t = Kit.Button(tabs, label, fam == catFamily ? Kit.Orange : new Color(0.9f, 0.84f, 0.74f), 20, w, 50);
                Kit.PlaceTL((RectTransform)t.transform, tx, 0, w, 50);
                tx += w + 8f;
                int f = fam;
                t.Clicked += () => { catFamily = f; Sfx.Play("ui", -8f, 1.1f); Mineros.Fx.Haptics.Selection(); RestyleTabs(tabBtns); rebuild(); };
                tabBtns.Add(new KeyValuePair<int, Btn>(fam, t));
            };
            addTab(-1, Loc.T("Todo"));
            foreach (var f in fams) addTab((int)f, FamilyName(f));

            // tira horizontal de tarjetas
            var area = Kit.New("Tarjetas", fr);
            Kit.PlaceTL(area, 0, 146, 720, CardH + 40);
            var catcher = area.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);
            var sr = area.gameObject.AddComponent<ScrollRect>();
            var vp = Kit.New("Ventana", area);
            Kit.Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            strip = Kit.New("Tira", vp);
            strip.anchorMin = new Vector2(0f, 0f); strip.anchorMax = new Vector2(0f, 1f);
            strip.pivot = new Vector2(0f, 0.5f);
            strip.anchoredPosition = Vector2.zero;
            sr.viewport = vp; sr.content = strip;
            sr.horizontal = true; sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.inertia = true; sr.decelerationRate = 0.12f;

            rebuild = () =>
            {
                for (int i = strip.childCount - 1; i >= 0; i--) Destroy(strip.GetChild(i).gameObject);
                var avail = new List<BDef>();
                var locked = new List<BDef>();
                foreach (var d in Island.Defs)
                {
                    if (d.Kind == BKind.Depot) continue;
                    // con Cuartel, las duchas son una habitacion (se arrastran adentro), no un edificio suelto
                    if (d.Kind == BKind.Showers && Isl.BarracksLevel >= 1) continue;
                    if (catFamily >= 0 && (int)d.Family != catFamily) continue;
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
                float x = 24f;
                int n = 0;
                foreach (var d in avail) { CatalogCard(strip, d, p, false, x, n++); x += CardW + 14f; }
                int shown = 0;
                foreach (var d in locked) { if (shown++ >= 6) break; CatalogCard(strip, d, p, true, x, n++); x += CardW + 14f; }
                if (n == 0)
                {
                    var l = Kit.LabelAt(strip, Loc.T("No hay nada nuevo acá: subí el Ayuntamiento."), 24, Kit.Brown, 0, true, 24, CardH * 0.4f, 640, 40);
                    l.alignment = TextAnchor.MiddleLeft;
                    x = 700f;
                }
                strip.sizeDelta = new Vector2(x + 10f, 0f);
                strip.anchoredPosition = Vector2.zero;
            };
            RestyleTabs(tabBtns);
            rebuild();
            debugTab = i => { if (i >= 0 && i < tabBtns.Count) { catFamily = tabBtns[i].Key; RestyleTabs(tabBtns); rebuild(); } };
        }

        System.Action<int> debugTab;
        /// <summary>Para capturas: elige la pestaña `i` del catalogo abierto.</summary>
        public void DebugCatalogTab(int i) { debugTab?.Invoke(i); }

        void RestyleTabs(List<KeyValuePair<int, Btn>> tabs)
        {
            foreach (var kv in tabs)
            {
                Color c = kv.Key == catFamily ? Kit.Orange : new Color(0.9f, 0.84f, 0.74f);
                if (kv.Value.Modulate != c) { kv.Value.Modulate = c; kv.Value.Restyle(); }
                kv.Value.Label.color = kv.Key == catFamily ? Color.white : Kit.Brown;
            }
        }

        void CatalogCard(RectTransform strip, BDef d, Plot p, bool locked, float x, int order)
        {
            var card = Kit.Box9(strip, "card", new Vector4(12, 12, 12, 16), locked ? new Color(0.86f, 0.84f, 0.8f) : Color.white, "Tarjeta");
            var crt = card.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.sizeDelta = new Vector2(CardW, CardH);
            crt.anchoredPosition = new Vector2(x, 0f);
            // entra en cascada de costado
            var cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            Tw.Alpha(cg, 1f, 0.2f, 0.04f + 0.04f * Mathf.Min(order, 8));
            Tw.Scale(crt, Vector3.one * 0.92f, Vector3.one, 0.28f, Ease.OutBack, 0.04f + 0.04f * Mathf.Min(order, 8));

            // escenario del color de la familia con el edificio
            Color fam = IslandArt.FamilyRoof(d.Kind);
            var stage = Kit.Img(card.transform, ButtonArt.Box(Color.Lerp(fam, Color.white, locked ? 0.78f : 0.55f), 16f, 3f, 0f), Color.white, "Escenario");
            stage.type = Image.Type.Sliced;
            Kit.PlaceTL(stage.rectTransform, 10, 10, CardW - 20, 196);
            var halo = Kit.Img(stage.transform, Icons.Glow(), new Color(1f, 1f, 1f, locked ? 0.12f : 0.55f), "Halo");
            Kit.PlaceTL(halo.rectTransform, 10, 0, CardW - 40, 196);
            var icon = Kit.Img(card.transform, IslandStage.I.BuildingIcon(d.Kind, 1), locked ? new Color(0.22f, 0.18f, 0.15f, 0.7f) : Color.white, "Modelo");
            icon.preserveAspect = true;
            Kit.PlaceTL(icon.rectTransform, 14, 6, CardW - 28, 200);
            if (!locked)
            {
                var irt = icon.rectTransform;
                float ph = order * 0.7f;
                Tw.To(irt, "respira", 999f, Ease.Linear, u => irt.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 2.2f + ph) * 0.025f));
            }
            var tag = Kit.MakeTag(card.transform, FamilyName(d.Family), fam, 14);
            Kit.PlaceTL(tag, 16, 16, 110, 24);
            var name = Kit.LabelAt(card.transform, d.Name, 24, Kit.Brown, 0, true, 8, 212, CardW - 16, 34, TextAnchor.MiddleCenter);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 16; name.resizeTextMaxSize = 24;
            var desc = Kit.LabelAt(card.transform, d.Desc, 16, new Color(0.42f, 0.32f, 0.24f), 0, false, 12, 248, CardW - 24, 58, TextAnchor.UpperCenter);
            Kit.Wrap(desc);
            // la descripcion aparece solo si se toca el dibujo (lo que se lee, a pedido)
            desc.gameObject.SetActive(false);
            icon.raycastTarget = true;
            var it = icon.gameObject.AddComponent<Btn>();
            it.PlaySound = false;
            it.Clicked += () => desc.gameObject.SetActive(!desc.gameObject.activeSelf);

            if (locked)
            {
                var lk = Kit.Icon(card.transform, "lock", 56);
                Kit.PlaceTL((RectTransform)lk.transform, CardW * 0.5f - 28f, 76, 56, 56);
                // se abre con el Ayuntamiento N: su dibujo + el numero
                var lth = Kit.Img(card.transform, IslandStage.I.BuildingIcon(BKind.Depot, Island.Tier(Mathf.Max(1, d.Th))), Color.white, "Ayuntamiento");
                lth.preserveAspect = true;
                Kit.PlaceTL(lth.rectTransform, CardW * 0.5f - 66f, CardH - 100, 64, 64);
                Kit.LabelAt(card.transform, d.Th.ToString(), 34, Kit.OrangeD, 0, true, CardW * 0.5f + 2f, CardH - 92, 70, 48, TextAnchor.MiddleLeft);
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
            MatsRow(card.transform, 16, 310, Isl.MatsFor(d.Kind, 1), 30f);
            TimeTag(card.transform, 16, 344, Dur(Isl.WorkSeconds(d.Kind, 1)), 18);
            var b = Kit.Button(card.transform, "", Kit.Green, 26, CardW - 36, 72);
            Kit.PlaceTL((RectTransform)b.transform, 18, CardH - 86, CardW - 36, 72);
            var coin = Kit.Icon(b.Content, "coin", 32);
            Kit.PlaceTL((RectTransform)coin.transform, 14, 18, 32, 32);
            b.Label.alignment = TextAnchor.MiddleRight;
            Kit.Stretch(b.Label.rectTransform, 50, 0, 16, 4);
            var kind = d.Kind;
            b.Clicked += () =>
            {
                Vector2 from = LayerPos(icon.rectTransform);
                if (Isl.CanBuild(kind, p) && Isl.TutDone)
                {
                    Tw.To(crt, "vuelta", 0.14f, Ease.InQuad, u => crt.localScale = new Vector3(1f - u, 1f + u * 0.06f, 1f));
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
                if (b == null) return;
                b.Label.text = BigNum.Fmt(Isl.BuildCost(kind));
                Color m = Isl.CanBuild(kind, p) ? Color.white : new Color(0.72f, 0.72f, 0.75f);
                if (b.Modulate != m) { b.Modulate = m; b.Restyle(); }
            });
        }
    }
}
