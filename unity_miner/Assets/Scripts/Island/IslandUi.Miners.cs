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
    /// Interfaz de los mineros (biblia 3.4): burbujas de necesidad que laten (comida, ducha), textos que flotan (z, ♥),
    /// cartas de los candidatos que llegan en barco (entran escalonadas, se dan vuelta al tocarlas, las raras brillan
    /// antes, el dorado tiene su escena), album de la coleccion con siluetas y la tarjeta del minero con su rasgo,
    /// nivel y amigo.
    /// </summary>
    public sealed partial class IslandUi
    {
        // ------------------------------------------------------------ dibujos chicos (a 4x, borde con contorno)
        static Sprite heart, drum, drop, bubble;

        public static Sprite Heart()
        {
            if (heart != null) return heart;
            var p = new Painter(160, 144, 4f, 3);
            var pts = new List<Vector2>();
            for (int i = 0; i < 48; i++)
            {
                float t = i / 48f * Mathf.PI * 2f;
                float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                pts.Add(new Vector2(20f + x * 1.05f, 17f - y * 1.05f));
            }
            p.OPoly(pts.ToArray(), new Color(1f, 0.38f, 0.5f), 3f);
            p.Ellipse(new Vector2(12f, 10f), new Vector2(3.5f, 2.5f), new Color(1f, 0.8f, 0.85f, 0.9f));
            heart = MakeSpr(p);
            return heart;
        }

        static Sprite Drumstick()
        {
            if (drum != null) return drum;
            var p = new Painter(160, 160, 4f, 3);
            p.Line(new Vector2(14f, 26f), new Vector2(28f, 12f), 5f, Painter.Out);
            p.OC(new Vector2(30f, 9f), 3.2f, Color.white, 2.5f);
            p.OC(new Vector2(33f, 13f), 3.2f, Color.white, 2.5f);
            p.Line(new Vector2(16f, 24f), new Vector2(29f, 11f), 3f, new Color(0.98f, 0.95f, 0.9f));
            p.OPoly(Painter.EllPts(new Vector2(14f, 26f), new Vector2(11f, 9f)), new Color(0.82f, 0.48f, 0.2f), 3f);
            p.Ellipse(new Vector2(11f, 23f), new Vector2(4f, 2.5f), new Color(1f, 0.75f, 0.45f, 0.9f));
            drum = MakeSpr(p);
            return drum;
        }

        static Sprite Drop()
        {
            if (drop != null) return drop;
            var p = new Painter(160, 160, 4f, 3);
            var pts = new List<Vector2>();
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f * Mathf.PI * 2f;
                float r = 11f;
                float x = Mathf.Sin(t) * r * (0.55f + 0.45f * Mathf.Sin(t * 0.5f));
                float y = -Mathf.Cos(t) * r;
                pts.Add(new Vector2(20f + x, 22f + y + (y < 0 ? y * 0.5f : 0f)));
            }
            p.OPoly(pts.ToArray(), new Color(0.35f, 0.7f, 1f), 3f);
            p.Ellipse(new Vector2(16f, 24f), new Vector2(2.5f, 4f), new Color(0.85f, 0.95f, 1f, 0.9f));
            drop = MakeSpr(p);
            return drop;
        }

        static Sprite Bubble()
        {
            if (bubble != null) return bubble;
            var p = new Painter(200, 200, 4f, 3);
            p.OC(new Vector2(25f, 22f), 19f, Color.white, 3f);
            p.OC(new Vector2(14f, 44f), 4f, Color.white, 2.5f);
            p.OC(new Vector2(9f, 48.5f), 2f, Color.white, 2f);
            bubble = MakeSpr(p);
            return bubble;
        }

        static Sprite MakeSpr(Painter p)
        {
            var t = p.ToTexture();
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 400f);
        }

        // ------------------------------------------------------------ textos flotantes
        public void FloatText(Vector3 world, string text, Color col, int size)
        {
            if (text == "♥") { FloatHeart(world, size); return; }
            Vector2 c = ToCanvas(world);
            Vector2 from = new Vector2(c.x + Random.Range(-10f, 10f), -c.y);
            // del mundo: van debajo de las hojas (antes ensuciaban la eleccion de cartas con "+41")
            var t = Kit.Label(worldLayer, text, size, col, 5, true, TextAnchor.MiddleCenter, "Flota");
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(80, size + 10);
            float ph = Random.value * 6f;
            Tw.To(rt, "flota", 1.4f, Ease.Linear, u =>
            {
                rt.anchoredPosition = from + new Vector2(Mathf.Sin(u * 5f + ph) * 10f, 70f * u);
                rt.localScale = Vector3.one * (0.6f + u * 0.7f);
                t.color = new Color(col.r, col.g, col.b, Mathf.Sin(u * Mathf.PI));
            }, () => Destroy(t.gameObject));
        }

        void FloatHeart(Vector3 world, int size)
        {
            Vector2 c = ToCanvas(world);
            Vector2 from = new Vector2(c.x, -c.y);
            var img = Kit.Img(worldLayer, Heart(), Color.white, "Corazon");
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(size, size * 0.9f);
            Tw.To(rt, "cor", 1.3f, Ease.Linear, u =>
            {
                rt.anchoredPosition = from + new Vector2(Mathf.Sin(u * 6f) * 8f, 60f * u);
                float beat = 1f + Mathf.Max(0f, Mathf.Sin(u * 18f)) * 0.15f;
                rt.localScale = Vector3.one * Tw.Eval(Ease.OutBack, Mathf.Min(1f, u * 4f)) * beat;
                img.color = new Color(1f, 1f, 1f, u > 0.7f ? (1f - u) / 0.3f : 1f);
            }, () => Destroy(img.gameObject));
        }

        public void FlyGemsFromWorld(Vector3 world, int n)
        {
            Vector2 c = ToCanvas(world);
            FlyGems(new Vector2(c.x, -c.y), n);
        }

        // ------------------------------------------------------------ burbujas de necesidad
        readonly Dictionary<int, RectTransform> needMarks = new Dictionary<int, RectTransform>();

        /// <summary>Burbuja con icono sobre el minero (comida / ducha) que late cada 2 s; "¡Fresco!" brilla aparte.</summary>
        public void Status(Miner m, Vector3 world)
        {
            RectTransform b;
            if (!needMarks.TryGetValue(m.Id, out b))
            {
                b = Kit.New("Necesidad", worldLayer);
                b.sizeDelta = new Vector2(64, 64);
                var bg = Kit.Img(b, Bubble(), Color.white, "Burbuja");
                Kit.Stretch(bg.rectTransform);
                var ic = Kit.Img(b, Drumstick(), Color.white, "Icono");
                ic.rectTransform.sizeDelta = new Vector2(38, 38);
                ic.rectTransform.anchoredPosition = new Vector2(1f, 5f);
                needMarks[m.Id] = b;
            }
            Sprite want = null;
            bool hungry = m.Energy < 25f && m.State != MState.Eating && m.State != MState.Resting && NeedShown(m, BKind.Canteen, true);
            bool dirty = m.Clean < 30f && m.State != MState.Showering && NeedShown(m, BKind.Showers, false);
            if (hungry) want = Drumstick();
            else if (dirty) want = Drop();
            bool vis = want != null && m.State != MState.Showering;
            if (b.gameObject.activeSelf != vis) { b.gameObject.SetActive(vis); if (vis) Tw.Pop(b, 1.4f); }
            if (!vis) return;
            var icon = b.GetChild(1).GetComponent<Image>();
            if (icon.sprite != want) icon.sprite = want;
            Vector2 c = ToCanvas(world + Vector3.up * IslandGame.MH(2.25f));
            b.anchoredPosition = new Vector2(c.x + 22f, -c.y);
            float k = Mathf.Repeat(Time.time + m.Id * 0.37f, 2f);
            b.localScale = Vector3.one * (k < 0.3f ? 1f + Mathf.Sin(k / 0.3f * Mathf.PI) * 0.18f : 1f);
        }

        /// <summary>
        /// Una necesidad se muestra solo si el jugador la puede resolver (auditoria final: gotas sobre todos los mineros
        /// desde el minuto 1 pidiendo Duchas que no existian = ruido). Con el edificio hecho, en cada uno; si se puede
        /// construir y no esta, en un solo minero (el llamado); bloqueado, en ninguno.
        /// </summary>
        bool NeedShown(Miner m, BKind fix, bool food)
        {
            if (Isl.Find(fix) != null) return true;
            if (!Isl.Unlocked(fix)) return false;
            foreach (var o in Isl.Miners)
            {
                bool need = food ? o.Energy < 25f : o.Clean < 30f;
                if (need) return o == m;
            }
            return false;
        }

        // ------------------------------------------------------------ candidatos del barco
        public void ShowRecruits()
        {
            if (Isl.Recruits.Count == 0) return;
            if (sheet != null) { Tw.After(this, "reclutas", 1.5f, ShowRecruits); return; }   // despues de lo que este abierto
            var fr = OpenSheet(640, true);
            sheetLocked = true;
            Kit.LabelAt(fr, Loc.T("¡Llegó el barco!"), 44, Kit.Brown, 0, true, 0, 22, 680, 56, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, Loc.T("Tocá las cartas y elegí quién se queda"), 26, Kit.Brown, 0, false, 0, 82, 680, 36, TextAnchor.MiddleCenter);
            Sfx.Play("card", -6f);
            int n = Isl.Recruits.Count;
            int flipped = 0;
            var cards = new List<RectTransform>();
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                int ch = Isl.Recruits[i];
                bool golden = Isl.RecruitGolden[i];
                var c = Island.Roster[ch];
                var card = Kit.New("Carta", fr);
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
                card.sizeDelta = new Vector2(204, 330);
                Vector2 pos = new Vector2((i - (n - 1) * 0.5f) * 214f, -320f);
                cards.Add(card);
                Color rc = golden ? IslandGame.GoldCol : IslandGame.RarityCol[c.Rarity];
                // dorso: el mismo objeto (grosor, sombra, material de la rareza) con el sello de la mina
                var back = CardBack(card, c.Rarity, golden, 204, 306);
                back.anchorMin = back.anchorMax = new Vector2(0.5f, 0.5f);
                back.anchoredPosition = new Vector2(0f, 12f);
                // frente: la carta coleccionable (dibujo en escena, marco del material de la rareza, viva)
                CardView cv;
                var front = MinerCard(card, ch, golden, 204, 306, out cv);
                front.name = "Frente";
                front.anchorMin = front.anchorMax = new Vector2(0.5f, 0.5f);
                front.anchoredPosition = new Vector2(0f, 12f);
                bool locked = i < Isl.RecruitLocked.Count && Isl.RecruitLocked[i];
                int rid = i < Isl.RecruitIds.Count ? Isl.RecruitIds[i] : -1;
                if (locked)
                {
                    // vista previa: el proximo especialista, oscuro, con candado y la habitacion del Cuartel que hace falta
                    var shade = Kit.RoundImg(front, 18, new Color(0.08f, 0.06f, 0.1f, 0.62f), "Bloqueada");
                    Kit.Stretch(shade.rectTransform);
                    var lk = Kit.Icon(shade.transform, "lock", 72);
                    Kit.Place((RectTransform)lk.transform, 0.5f, 0.5f, -36f, -70f, 72, 72);
                    var bi = Kit.Img(shade.transform, IslandStage.I.BuildingIcon(BKind.Barracks, 1), Color.white, "Cuartel");
                    bi.preserveAspect = true;
                    Kit.Place(bi.rectTransform, 0.5f, 0.5f, -64f, 20f, 76, 76);
                    Kit.LabelAt(shade.transform, Loc.T("Nv ") + c.RoomLevel, 30, Kit.Yellow, 5, true, 112, 168, 90, 44, TextAnchor.MiddleLeft);
                }
                else if (rid >= 0)
                {
                    // cada candidato es una persona: su cara y su nombre sobre la carta
                    var ring = Kit.RoundImg(front, 34, rc, "MarcoCara");
                    Kit.PlaceTL(ring.rectTransform, 6, 6, 68, 68);
                    var msk = Kit.RoundImg(ring.transform, 30, Color.white, "Mascara");
                    Kit.Stretch(msk.rectTransform, 4, 4, 4, 4);
                    msk.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                    var ph = Kit.Img(msk.transform, FaceSpriteId(rid, rc), Color.white, "Cara");
                    Kit.Stretch(ph.rectTransform);
                    var nb = Kit.RoundImg(front, 12, new Color(0.1f, 0.08f, 0.12f, 0.78f), "Nombre");
                    Kit.PlaceTL(nb.rectTransform, 78, 26, 118, 32);
                    var nl = Kit.Label(nb.transform, Island.FirstNameId(rid), 21, Color.white, 0, true, TextAnchor.MiddleCenter);
                    Kit.Stretch(nl.rectTransform, 4, 0, 4, 0);
                }
                var pickBtn = Kit.Button(card, "", Kit.Green, 26, 120, 58);
                Kit.Place((RectTransform)pickBtn.transform, 0.5f, 1f, -60f, -6f, 120, 58);
                var pkI = Kit.Icon(pickBtn.Content, "check", 42);   // elegir = tilde
                Kit.Place((RectTransform)pkI.transform, 0.5f, 0.5f, -21f, -24f, 42, 42);
                pickBtn.gameObject.SetActive(false);
                front.gameObject.SetActive(false);
                var showPick = pickBtn.gameObject;
                // carta en grande: tocar la carta dada vuelta la trae al centro (el arte manda), otro toque la devuelve
                var look = Kit.HitArea(front, 204, 306, "Mirar");
                Kit.Stretch((RectTransform)look.transform);
                bool big = false;
                look.Clicked += () =>
                {
                    if (summonActive) return;
                    big = !big;
                    Sfx.Play("card", -8f, big ? 1.25f : 0.95f);
                    if (big) card.SetAsLastSibling();
                    Vector2 a = card.anchoredPosition, b = big ? new Vector2(0f, -330f) : pos;
                    float s0 = card.localScale.x, s1 = big ? 1.75f : 1f;
                    Tw.To(card, "grande", 0.32f, Ease.OutBack, u =>
                    {
                        card.anchoredPosition = Vector2.LerpUnclamped(a, b, u);
                        card.localScale = Vector3.one * Mathf.LerpUnclamped(s0, s1, u);
                    });
                    foreach (var other in cards)
                    {
                        if (other == card) continue;
                        var g = other.GetComponent<CanvasGroup>();
                        if (g == null) g = other.gameObject.AddComponent<CanvasGroup>();
                        Tw.Alpha(g, big ? 0.25f : 1f, 0.25f, 0f);
                    }
                    if (big && cv != null) cv.Flash = 0.5f;
                };
                // entra desde abajo, escalonada
                card.anchoredPosition = pos + new Vector2(0f, -700f);
                Tw.MoveFrom(card, pos + new Vector2(0f, -700f), pos, 0.45f, Ease.OutBack, 0.12f * i);
                bool open = false;
                var tap = back.gameObject.AddComponent<Btn>();
                tap.Juice = false;
                if (i == 0) primaryBtn = tap;
                tap.Clicked += () =>
                {
                    if (open) return;
                    open = true;
                    float wait = 0f;
                    if (golden) { wait = 1.3f; GoldenSuspense(card, back); }
                    else if (c.Rarity >= 2) { wait = 0.6f; RareSuspense(card, back, rc); }
                    Tw.After(card, "voltea", wait, () => Flip(card, back, front, () =>
                    {
                        flipped++;
                        showPick.SetActive(!locked);
                        Tw.Pop((RectTransform)showPick.transform, 1.15f);
                        if (cv != null) cv.Flash = 1f;
                        if (golden) GoldenReveal(card, c.Name, fr);
                        else if (c.Rarity >= 2) { Sfx.Play("gleam", -6f); Flash(new Color(rc.r, rc.g, rc.b), 0.25f); }
                        else Sfx.Play("card", -6f, 1.1f);
                    }));
                };
                pickBtn.Clicked += () =>
                {
                    // invocacion: la carta va a la mano y se la arrastra a la isla (IslandUi.Cards)
                    if (summonActive) return;
                    showPick.SetActive(false);
                    foreach (var other in cards) { var b2 = other.Find("Button"); if (b2 != null) b2.gameObject.SetActive(false); }
                    Sfx.Play("jingle_small", -4f);
                    Juice.Vibrate(30);
                    BeginSummon(idx, cards[idx], cv, cards, ch, golden);
                };
            }
        }

        void Flip(RectTransform card, RectTransform back, RectTransform front, System.Action done)
        {
            Sfx.Play("card", -8f);
            Tw.To(card, "flip", 0.32f, Ease.Linear, u =>
            {
                float sx = Mathf.Abs(Mathf.Cos(u * Mathf.PI));
                if (u >= 0.5f && back.gameObject.activeSelf) { back.gameObject.SetActive(false); front.gameObject.SetActive(true); }
                card.localScale = new Vector3(Mathf.Max(0.02f, sx), 1f + Mathf.Sin(u * Mathf.PI) * 0.08f, 1f);
            }, () => { card.localScale = Vector3.one; done(); });
        }

        void RareSuspense(RectTransform card, RectTransform back, Color rc)
        {
            Sfx.Play("magic_rise", -10f, 1.3f);
            var glow = Kit.Glow(card, new Color(rc.r, rc.g, rc.b, 0f), "Brillo");
            glow.rectTransform.sizeDelta = new Vector2(420, 520);
            glow.transform.SetAsFirstSibling();
            Tw.To(card, "susp", 0.6f, Ease.Linear, u =>
            {
                card.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 60f) * 4f * u);
                glow.color = new Color(rc.r, rc.g, rc.b, u * 0.8f);
            }, () => card.localRotation = Quaternion.identity);
        }

        void GoldenSuspense(RectTransform card, RectTransform back)
        {
            Sfx.Duck(10f, 4f);
            Sfx.Play("drumroll", -6f);
            var img = back.GetComponent<Image>();
            Tw.To(card, "oro", 1.3f, Ease.Linear, u =>
            {
                card.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 70f) * 6f * u);
                card.localScale = Vector3.one * (1f + u * 0.08f);
                if (img != null) img.color = Color.Lerp(Color.white, new Color(1f, 0.95f, 0.6f), u);
            }, () => { card.localRotation = Quaternion.identity; Flash(new Color(1f, 0.92f, 0.5f), 0.4f); });
        }

        void GoldenReveal(RectTransform card, string name, Transform sheetFrame)
        {
            Sfx.Play("fanfare", -2f);
            Sfx.PlayLater("crackle", 0.3f, -8f);
            Juice.Vibrate(90);
            // rayos que giran detras de la carta
            var rays = Kit.Img(card, Icons.Glow(), new Color(1f, 0.85f, 0.35f, 0.9f), "Rayos");
            rays.rectTransform.sizeDelta = new Vector2(520, 520);
            rays.transform.SetAsFirstSibling();
            Tw.To(rays, "rayos", 3f, Ease.Linear, u =>
            {
                rays.rectTransform.localRotation = Quaternion.Euler(0, 0, u * 120f);
                rays.color = new Color(1f, 0.85f, 0.35f, 0.9f * (1f - u * 0.6f));
            });
            // el cartel va dentro de la hoja (el de avisos queda debajo de las hojas)
            var t = Kit.LabelAt(sheetFrame, Loc.T("¡MINERO DORADO!"), 50, IslandGame.GoldCol, 8, true, 0, 520, 680, 64, TextAnchor.MiddleCenter);
            var sub = Kit.LabelAt(sheetFrame, name + Loc.T(" rinde x1.5 y deja estela de oro"), 24, Kit.Brown, 0, true, 0, 580, 680, 34, TextAnchor.MiddleCenter);
            Tw.To(t.rectTransform, "dor", 1.2f, Ease.Linear, u =>
            {
                t.rectTransform.localScale = Vector3.one * Tw.Eval(Ease.OutElastic, Mathf.Min(1f, u * 1.6f));
                t.color = Color.Lerp(IslandGame.GoldCol, Color.white, Mathf.Abs(Mathf.Sin(u * 12f)) * 0.35f);
            });
            sub.rectTransform.localScale = Vector3.zero;
            Tw.Scale(sub.rectTransform, Vector3.zero, Vector3.one, 0.35f, Ease.OutBack, 0.4f);
        }

        // ------------------------------------------------------------ album
        Btn albumBtn;

        void BuildAlbumButton()
        {
            albumBtn = Kit.SqButton(hudLayer, Kit.Blue, 60, "Album");
            Kit.Place((RectTransform)albumBtn.transform, 1f, 0f, -84f, 172f, 60, 60);
            var ic = Kit.Img(albumBtn.transform, Icons.MinerBust(IslandGame.RarityCol[2]), Color.white, "Icono");
            ic.preserveAspect = true;
            Kit.PlaceTL(ic.rectTransform, 8, 4, 44, 48);
            albumBtn.Clicked += OpenAlbum;
            albumBtn.gameObject.SetActive(false);   // vive en el ☰ (Mineros)
        }

        public void OpenAlbum()
        {
            var fr = OpenSheet(840, true);
            Kit.LabelAt(fr, Loc.T("Álbum de mineros"), 42, Kit.Brown, 0, true, 0, 20, 680, 54, TextAnchor.MiddleCenter);
            Kit.LabelAt(fr, Isl.FoundCount + " / " + Island.Roster.Length + Loc.T(" descubiertos"), 26, Kit.OrangeD, 0, true, 0, 74, 680, 34, TextAnchor.MiddleCenter);
            for (int i = 0; i < Island.Roster.Length; i++)
            {
                var c = Island.Roster[i];
                int col = i % 4, row = i / 4;
                bool found = Isl.Found[i], gold = Isl.FoundGolden[i];
                Color rc = IslandGame.RarityCol[c.Rarity];
                var cell = Kit.OutBox(fr, 16, 4, 5, found ? Kit.Cream : new Color(0.78f, 0.74f, 0.68f), found ? rc : Kit.Out, "Celda");
                Kit.PlaceTL(cell, 26 + col * 160, 122 + row * 232, 148, 220);
                var bust = Kit.Img(cell, Icons.MinerBust(gold ? IslandGame.GoldCol : IslandArt.SpecColor(i)), found ? Color.white : new Color(0.18f, 0.12f, 0.08f, 0.85f), "Cara");
                bust.preserveAspect = true;
                Kit.PlaceTL(bust.rectTransform, 19, 12, 110, 120);
                SpecBadge(cell, i, 96, 92, 44, !found);
                var nm = Kit.LabelAt(cell, found ? c.Name : "???", 22, Kit.Brown, 0, true, 4, 134, 140, 32, TextAnchor.MiddleCenter);
                nm.resizeTextForBestFit = true; nm.resizeTextMinSize = 12; nm.resizeTextMaxSize = 22;
                var rl = Kit.LabelAt(cell, Island.RarityName[c.Rarity], 18, found ? rc : Kit.Brown, 0, true, 0, 164, 148, 26, TextAnchor.MiddleCenter);
                if (!found) rl.color = new Color(0.4f, 0.34f, 0.28f);
                if (gold)
                {
                    var star = Kit.Img(cell, Icons.Get("star"), IslandGame.GoldCol, "Dorado");
                    Kit.PlaceTL(star.rectTransform, 108, 6, 34, 34);
                }
                cell.localScale = Vector3.zero;
                Tw.Scale(cell, Vector3.zero, Vector3.one, 0.3f, Ease.OutBack, 0.02f * i);
            }
        }

        /// <summary>Para capturas: toca "Elegir" en la carta `i` (ya dada vuelta), con un toque real.</summary>
        public void DebugPick(int i)
        {
            if (sheet == null) return;
            int n = 0;
            foreach (var b in sheet.GetComponentsInChildren<Btn>(true))
                if (b.Label != null && b.Label.text == Loc.T("Elegir")) { if (n == i) { RealTap((RectTransform)b.transform); return; } n++; }
        }

        /// <summary>Para capturas: toca la carta `i` ya dada vuelta (la trae en grande o la devuelve).</summary>
        public bool DebugBig(int i)
        {
            if (sheet == null) return false;
            int n = 0;
            foreach (var b in sheet.GetComponentsInChildren<Btn>(true)) if (b.name == "Mirar") { if (n == i) return RealTap(b.transform as RectTransform); n++; }
            return false;
        }

        /// <summary>Para capturas: toca la carta `i` de los candidatos (la da vuelta).</summary>
        public bool DebugFlip(int i)
        {
            if (sheet == null) return false;
            var cards = sheet.GetComponentsInChildren<Btn>(true);
            int n = 0;
            foreach (var b in cards) if (b.name == "Dorso") { if (n == i) return RealTap(b.transform as RectTransform); n++; }
            return false;
        }

        /// <summary>Toque de verdad: raycast del EventSystem en el centro del rect (falla si nada recibe el dedo).</summary>
        public static bool RealTap(RectTransform rt)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var cv = rt.GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            var pe = new UnityEngine.EventSystems.PointerEventData(es) { position = p, pressPosition = p };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(pe, hits);
            if (hits.Count == 0) { Debug.Log("toquereal nada en " + rt.name); return false; }
            var h = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
            Debug.Log("toquereal " + rt.name + " -> " + (h != null ? h.name : "nadie") + " (pega en " + hits[0].gameObject.name + ")");
            if (h == null) return false;
            pe.pointerPress = h;
            UnityEngine.EventSystems.ExecuteEvents.Execute(h, pe, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            return h == rt.gameObject;
        }

        void RefreshAlbumButton()
        {
            if (albumBtn == null) BuildAlbumButton();
        }

        /// <summary>Insignia redonda con el mineral que pica el especialista (estrella para el Maestro).</summary>
        void SpecBadge(Transform parent, int ch, float x, float y, float size, bool dim)
        {
            var disc = Kit.RoundImg(parent, Mathf.RoundToInt(size * 0.5f), dim ? new Color(0.6f, 0.56f, 0.5f) : Color.white, "Especialidad");
            Kit.PlaceTL(disc.rectTransform, x, y, size, size);
            var ring = Kit.Img(disc.transform, Icons.Ring(6f), dim ? new Color(0.45f, 0.4f, 0.35f) : IslandArt.SpecColor(ch), "Borde");
            Kit.Stretch(ring.rectTransform);
            int spec = Island.Roster[ch].Spec;
            var ic = Kit.Img(disc.transform, spec >= 0 ? OreIcon(spec) : Icons.Get("star"), dim ? new Color(0.25f, 0.2f, 0.15f, 0.7f) : (spec >= 0 ? Color.white : IslandGame.GoldCol), "Mineral");
            ic.preserveAspect = true;
            Kit.Stretch(ic.rectTransform, size * 0.12f, size * 0.12f, size * 0.12f, size * 0.12f);
        }
    }
}
