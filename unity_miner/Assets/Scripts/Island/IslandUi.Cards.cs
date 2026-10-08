using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.IslandView
{
    /// <summary>
    /// Cartas de mineros como objetos (docs/PLAN_CARTAS.md): cada carta es un coleccionable con grosor, sombra, marco del
    /// material de su rareza (madera, metal pulido, cristal, oro con energia), un dibujo propio en escena (Resources/Cards)
    /// que respira y una luz que la cruza (CardView + shader CardArt). Elegir un minero es una invocacion: la hoja se va,
    /// la isla vuelve a foco, la carta queda en la mano y se la arrastra al mundo; al soltarla cae y el minero sale de
    /// ella (IslandGame.CardSummon). Regla: VER, TOCAR, VERLO SUCEDER.
    /// </summary>
    public sealed partial class IslandUi
    {
        static readonly string[] CardKeys = { "piedra", "cobre", "hierro", "carbon", "oro", "cristal", "diamante", "raros", "maestro", "topo", "fantasma" };

        /// <summary>Dibujo de la carta (null si todavia no esta en Resources/Cards).</summary>
        public static Texture2D CardTex(int ch, bool golden)
        {
            if (ch < 0 || ch >= CardKeys.Length) return null;
            string n = golden && ch < 9 ? "carta_" + (11 + ch).ToString("00") + "_" + CardKeys[ch] + "_oro" : "carta_" + ch.ToString("00") + "_" + CardKeys[ch];
            return Resources.Load<Texture2D>("Cards/" + n);
        }

        /// <summary>Material del marco segun la rareza: la rareza se siente antes de leerla.</summary>
        public static void CardMaterial(int rarity, bool golden, out Color face, out Color edge, out Color glow)
        {
            if (golden) { face = Icons.H("ffcf3a"); edge = Icons.H("a8740c"); glow = Icons.H("fff0a0"); return; }
            switch (rarity)
            {
                case 1: face = Icons.H("c3cfdb"); edge = Icons.H("5f6e7e"); glow = Icons.H("cfeaff"); break;     // metal pulido
                case 2: face = Icons.H("9a86f2"); edge = Icons.H("4a3a9a"); glow = Icons.H("c3b4ff"); break;     // cristal
                case 3: face = Icons.H("f2b632"); edge = Icons.H("8a5a06"); glow = Icons.H("ffd76a"); break;     // oro con energia
                default: face = Icons.H("9a6a3e"); edge = Icons.H("5a3a20"); glow = Icons.H("fff2d6"); break;   // madera
            }
        }

        /// <summary>Carta de minero completa (sombra, brillo, canto, marco, dibujo vivo, placa con el nombre).</summary>
        public RectTransform MinerCard(Transform parent, int ch, bool golden, float w, float h, out CardView cv)
        {
            var c = Island.Roster[ch];
            Color face, edge, glowC;
            CardMaterial(c.Rarity, golden, out face, out edge, out glowC);
            var root = Kit.New("CartaMinero", parent);
            root.sizeDelta = new Vector2(w, h);
            // sombra en el piso y brillo de la rareza (no se inclinan con la carta)
            var shadow = Kit.Glow(root, new Color(0f, 0f, 0f, 0.55f), "Sombra");
            shadow.rectTransform.sizeDelta = new Vector2(w * 1.3f, h * 1.12f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -26f);
            var glow = Kit.Glow(root, new Color(glowC.r, glowC.g, glowC.b, 0.4f), "Brillo");
            glow.rectTransform.sizeDelta = new Vector2(w * 1.9f, h * 1.55f);
            var body = Kit.New("Cuerpo", root);
            Kit.Stretch(body);
            // canto: tres capas cada vez mas oscuras hacia abajo (grosor de la carta)
            for (int k = 3; k >= 1; k--)
            {
                var e = Kit.Img(body, ButtonArt.Box(Color.Lerp(edge, Color.black, 0.12f * k), 24f, 0f, 0f), Color.white, "Canto");
                e.type = Image.Type.Sliced;
                Kit.Stretch(e.rectTransform);
                e.rectTransform.anchoredPosition = new Vector2(0f, -3.5f * k);
            }
            var frame = Kit.Img(body, ButtonArt.Box(face, 24f, 5f, 0f), Color.white, "Marco");
            frame.type = Image.Type.Sliced;
            Kit.Stretch(frame.rectTransform);
            // filete claro adentro del marco (bisel)
            var bevel = Kit.Img(body, ButtonArt.Box(Color.Lerp(face, Color.white, 0.45f), 18f, 0f, 0f), Color.white, "Bisel");
            bevel.type = Image.Type.Sliced;
            Kit.Stretch(bevel.rectTransform, 8, 8, 8, 8);
            // el dibujo
            var art = Kit.New("Dibujo", body).gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            var tex = CardTex(ch, golden);
            if (tex != null) art.texture = tex;
            else { art.texture = Icons.MinerBust(golden ? IslandGame.GoldCol : IslandArt.SpecColor(ch)).texture; art.color = new Color(1f, 1f, 1f, 1f); }
            Kit.Stretch(art.rectTransform, 11, 11, 11, 11);
            // placa del nombre abajo, sobre el dibujo
            var plate = Kit.Img(body, ButtonArt.Box(new Color(0.1f, 0.07f, 0.06f, 0.82f), 14f, 0f, 0f), Color.white, "Placa");
            plate.type = Image.Type.Sliced;
            Kit.Place(plate.rectTransform, 0.5f, 1f, -w * 0.5f + 16f, -64f, w - 32f, 52f);
            var nm = Kit.Label(plate.transform, c.Name, 22, Color.white, 4, true, TextAnchor.MiddleCenter);
            Kit.Stretch(nm.rectTransform, 6, 2, 6, 14);
            nm.resizeTextForBestFit = true; nm.resizeTextMinSize = 12; nm.resizeTextMaxSize = 22;
            var rl = Kit.Label(plate.transform, golden ? Loc.T("¡DORADO!") : Island.RarityName[c.Rarity], 14, glowC, 3, true, TextAnchor.MiddleCenter);
            Kit.Stretch(rl.rectTransform, 6, 32, 6, 0);
            // gema de rareza arriba a la izquierda (tantas como la rareza) y el escudo del mineral a la derecha
            for (int i = 0; i <= c.Rarity; i++)
            {
                var gem = Kit.Img(body, Kit.White, Color.Lerp(face, Color.white, 0.2f), "Gema");
                Kit.Place(gem.rectTransform, 0f, 0f, 14f + i * 17f, 14f, 13f, 13f);
                gem.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            }
            SpecBadge(body, ch, w - 54f, 10f, 42f, false);
            cv = root.gameObject.AddComponent<CardView>();
            cv.Glow = glow;
            Canvas.ForceUpdateCanvases();
            cv.Init(art, body, c.Rarity, golden, glowC);
            return root;
        }

        /// <summary>Dorso de la carta: mismo cuerpo que el frente, panel oscuro con el sello (pico cruzado) y "?" que late.</summary>
        public RectTransform CardBack(Transform parent, int rarity, bool golden, float w, float h)
        {
            Color face, edge, glowC;
            CardMaterial(rarity, golden, out face, out edge, out glowC);
            var root = Kit.New("Dorso", parent);
            root.sizeDelta = new Vector2(w, h);
            var shadow = Kit.Glow(root, new Color(0f, 0f, 0f, 0.5f), "Sombra");
            shadow.rectTransform.sizeDelta = new Vector2(w * 1.3f, h * 1.12f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -26f);
            if (rarity >= 2 || golden)
            {
                var glow = Kit.Glow(root, new Color(glowC.r, glowC.g, glowC.b, 0.5f), "Brillo");
                glow.rectTransform.sizeDelta = new Vector2(w * 1.8f, h * 1.5f);
            }
            for (int k = 3; k >= 1; k--)
            {
                var e = Kit.Img(root, ButtonArt.Box(Color.Lerp(edge, Color.black, 0.12f * k), 24f, 0f, 0f), Color.white, "Canto");
                e.type = Image.Type.Sliced;
                Kit.Stretch(e.rectTransform);
                e.rectTransform.anchoredPosition = new Vector2(0f, -3.5f * k);
            }
            var frame = Kit.Img(root, ButtonArt.Box(face, 24f, 5f, 0f), Color.white, "Marco", true);
            frame.type = Image.Type.Sliced;
            Kit.Stretch(frame.rectTransform);
            var panel = Kit.Img(root, ButtonArt.Box(Color.Lerp(edge, Color.black, 0.35f), 18f, 0f, 0f), Color.white, "Panel");
            panel.type = Image.Type.Sliced;
            Kit.Stretch(panel.rectTransform, 12, 12, 12, 12);
            // rombo grande y el signo
            var seal = Kit.Img(root, Kit.White, Color.Lerp(face, Color.white, 0.15f), "Sello");
            seal.rectTransform.sizeDelta = new Vector2(w * 0.5f, w * 0.5f);
            seal.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            seal.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            var inner = Kit.Img(root, Kit.White, Color.Lerp(edge, Color.black, 0.2f), "SelloAdentro");
            inner.rectTransform.sizeDelta = new Vector2(w * 0.4f, w * 0.4f);
            inner.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            inner.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            var q = Kit.Label(root, "?", 84, Color.white, 6, true, TextAnchor.MiddleCenter);
            q.rectTransform.sizeDelta = new Vector2(w, 120f);
            q.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            var tag = Kit.Label(root, golden ? Loc.T("¡DORADO!") : Island.RarityName[rarity], 24, Color.white, 6, true, TextAnchor.MiddleCenter);
            tag.rectTransform.sizeDelta = new Vector2(w, 40f);
            tag.rectTransform.anchoredPosition = new Vector2(0f, -h * 0.5f + 40f);
            Tw.To(q.rectTransform, "late", 60f, Ease.Linear, u => q.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(u * 60f * 3f)));
            return root;
        }

        // ------------------------------------------------------------ invocacion: de la hoja a la mano y de la mano al mundo
        bool summonActive;
        SummonDrag summon;
        RectTransform summonSheet;   // la hoja del barco (se esta desvaneciendo): esa no tapa la carta

        /// <summary>La carta en la mano no flota encima de otra hoja (auditoria: tapaba la ficha de la casa); vuelve al cerrarla.</summary>
        void UpdateHandCard()
        {
            if (summon == null || summon.Card == null) return;
            var cg = summon.Card.GetComponent<CanvasGroup>();
            if (cg == null) cg = summon.Card.gameObject.AddComponent<CanvasGroup>();
            float want = sheet != null && sheet != summonSheet ? 0f : 1f;
            cg.alpha = Mathf.MoveTowards(cg.alpha, want, Time.unscaledDeltaTime * 6f);
            cg.blocksRaycasts = want > 0.5f;
            if (summon.Hint != null && want < 0.5f) summon.Hint.color = new Color(1f, 1f, 1f, cg.alpha);
        }

        /// <summary>Eligio la carta `idx`: las otras se van, la isla vuelve a foco y la carta queda en la mano para arrastrarla.</summary>
        void BeginSummon(int idx, RectTransform card, CardView cv, System.Collections.Generic.List<RectTransform> cards, int ch, bool golden)
        {
            summonActive = true;
            summonSheet = sheet;
            for (int j = 0; j < cards.Count; j++)
            {
                if (j == idx) continue;
                var cj = cards[j];
                Vector2 p0 = cj.anchoredPosition;
                float side = j < idx ? -1f : 1f;
                Tw.To(cj, "se_va", 0.4f, Ease.InBack, u =>
                {
                    cj.localScale = Vector3.one * (1f - u * 0.6f);
                    cj.anchoredPosition = p0 + new Vector2(side * u * 160f, u * 60f);
                    cj.localRotation = Quaternion.Euler(0, 0, -side * u * 25f);
                }, () => cj.gameObject.SetActive(false));
            }
            Sfx.Play("whoosh", -8f, 1.2f);
            if (cv != null) cv.Flash = 0.8f;
            // la carta sale de la hoja (queda en la capa de vuelo) y la hoja se desvanece: el fondo deja de estar borroso
            card.SetParent(flyLayer, true);
            Vector3 lp = flyLayer.InverseTransformPoint(card.position);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(lp.x, lp.y);
            if (sheet != null)
            {
                var g = sheet.gameObject.AddComponent<CanvasGroup>();
                var sh = sheet;
                Tw.To(sh, "foco", 0.45f, Ease.OutQuad, u => g.alpha = 1f - u, () => { sheetLocked = false; if (sheet == sh) CloseSheet(); });
            }
            // a la mano: abajo al centro, mas chica, flotando
            Vector2 rest = new Vector2(0f, -Kit.CanvasSize.y * 0.5f + 290f);
            Tw.To(card, "a_la_mano", 0.5f, Ease.OutBack, u =>
            {
                card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, rest, u);
                card.localScale = Vector3.one * Mathf.Lerp(1f, 0.82f, u);
            });
            var hint = Kit.LabelAt(flyLayer, Loc.T("Arrastrá al minero a tu isla"), 30, Color.white, 7, true, -340, 0, 680, 44, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hint.rectTransform.anchoredPosition = rest + new Vector2(0f, 200f);
            hint.color = new Color(1f, 1f, 1f, 0f);
            Tw.To(hint.rectTransform, "pista", 0.4f, Ease.OutQuad, u => hint.color = new Color(1f, 1f, 1f, u), null);
            summon = card.gameObject.AddComponent<SummonDrag>();
            summon.Init(this, game, card, cv, idx, ch, golden, rest, hint);
        }

        /// <summary>La carta se solto sobre la isla: cae hacia el suelo (se achica y se acuesta) y el mundo sigue la escena.</summary>
        internal void DropCard(SummonDrag s, Vector3 ground)
        {
            var card = s.Card;
            Vector2 to = CamPxToLayer(game.Cam.WorldToScreenPoint(ground));
            Vector2 from = card.anchoredPosition;
            if (s.Hint != null) Destroy(s.Hint.gameObject);
            Sfx.Play("whoosh", -10f, 0.9f);
            Tw.To(card, "cae", 0.3f, Ease.InQuad, u =>
            {
                card.anchoredPosition = Vector2.Lerp(from, to, u);
                card.localScale = Vector3.one * Mathf.Lerp(0.82f, 0.3f, u);
                card.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 64f, u), 0f, 0f);
            }, () =>
            {
                Color face, edge, glowC;
                CardMaterial(Island.Roster[s.Ch].Rarity, s.Golden, out face, out edge, out glowC);
                game.CardDrop = new IslandGame.CardDropInfo { At = ground, Tex = CardTex(s.Ch, s.Golden), Frame = face, Glow = glowC, Ch = s.Ch, Golden = s.Golden };
                var m = game.Recruit(s.Idx, ground);
                if (m == null) game.CardDrop = null;
                Destroy(card.gameObject);
                summon = null;
                summonActive = false;
                if (Isl.Recruits.Count > 0) Tw.After(this, "otro", 3.5f, ShowRecruits);
            });
        }

        /// <summary>Pixel de la camara del juego -> punto de la capa de vuelo (relativo al centro). Normalizado: sirve aunque la
        /// camara renderice a otra resolucion que la pantalla (las capturas van a 2x).</summary>
        public Vector2 CamPxToLayer(Vector2 px)
        {
            var cs = Kit.CanvasSize;
            float pw = Mathf.Max(game.Cam.pixelWidth, 1), ph = Mathf.Max(game.Cam.pixelHeight, 1);
            return new Vector2((px.x / pw - 0.5f) * cs.x, (px.y / ph - 0.5f) * cs.y);
        }

        /// <summary>Para capturas: suelta la carta en la mano sobre ese punto del mundo (como si la hubieran arrastrado).</summary>
        public bool DebugDrop(Vector3 world)
        {
            if (summon == null) return false;
            DropCard(summon, world);
            return true;
        }

        /// <summary>Para capturas: mueve la carta en la mano como si el dedo la llevara hasta ese punto del mundo.</summary>
        public bool DebugHold(Vector3 world)
        {
            if (summon == null) return false;
            summon.DebugHold(world);
            return true;
        }
    }

    /// <summary>
    /// La carta en la mano: sigue al dedo con inercia (se inclina con la velocidad), muestra donde va a caer (sombra y
    /// anillos de energia en el suelo) y al soltarla sobre la isla la deja caer; afuera de la isla vuelve a la mano.
    /// </summary>
    public sealed class SummonDrag : MonoBehaviour
    {
        public RectTransform Card;
        public Text Hint;
        public int Idx, Ch;
        public bool Golden;
        IslandUi ui;
        IslandGame game;
        CardView cv;
        Vector2 rest, offset, lastPos;
        bool grab, valid, dropped, debugHeld;
        Vector3 ground;
        float ringT, bobT;
        Transform marker;

        public void Init(IslandUi u, IslandGame g, RectTransform card, CardView view, int idx, int ch, bool golden, Vector2 restPos, Text hint)
        {
            ui = u; game = g; Card = card; cv = view; Idx = idx; Ch = ch; Golden = golden; rest = restPos; Hint = hint;
        }

        RectTransform Layer { get { return (RectTransform)Card.parent; } }

        void OnDestroy() { if (marker != null) Destroy(marker.gameObject); }

        public void DebugHold(Vector3 world)
        {
            debugHeld = true;
            Vector2 sp = game.Cam.WorldToScreenPoint(world);
            Card.anchoredPosition = ui.CamPxToLayer(sp) + new Vector2(0f, 90f);
            Card.localScale = Vector3.one * 0.62f;
            UpdateGround(sp);
            if (cv != null) { cv.Held = true; cv.Vel = new Vector2(600f, -200f); }
        }

        void Update()
        {
            if (dropped || Card == null || debugHeld) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Vector2 sp; bool down, held, up;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                sp = t.position; down = t.phase == TouchPhase.Began; up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled; held = !up;
            }
            else
            {
                sp = Input.mousePosition; down = Input.GetMouseButtonDown(0); held = Input.GetMouseButton(0); up = Input.GetMouseButtonUp(0);
            }
            Vector2 cp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Layer, sp, null, out cp);
            if (!grab && down && RectTransformUtility.RectangleContainsScreenPoint(Card, sp, null))
            {
                grab = true;
                offset = new Vector2(0f, 90f);   // la carta queda arriba del dedo: se ve donde cae
                Sfx.Play("card", -6f, 1.2f);
                Mineros.Fx.Haptics.Light();
                if (cv != null) cv.Held = true;
                if (Hint != null) Hint.text = Loc.T("Soltala donde quieras");
            }
            if (grab && held)
            {
                Vector2 target = cp + offset;
                Vector2 pos = Vector2.Lerp(Card.anchoredPosition, target, 1f - Mathf.Exp(-dt * 16f));
                if (cv != null) cv.Vel = Vector2.Lerp(cv.Vel, (pos - lastPos) / Mathf.Max(dt, 1e-3f), 0.35f);
                Card.anchoredPosition = pos;
                Card.localScale = Vector3.one * Mathf.Lerp(Card.localScale.x, 0.62f, 1f - Mathf.Exp(-dt * 10f));
                UpdateGround(sp);
            }
            else if (!grab)
            {
                // en la mano: flota y llama
                bobT += dt;
                Card.anchoredPosition = Vector2.Lerp(Card.anchoredPosition, rest + new Vector2(0f, Mathf.Sin(bobT * 2.2f) * 10f), 1f - Mathf.Exp(-dt * 6f));
                Card.localScale = Vector3.one * Mathf.Lerp(Card.localScale.x, 0.82f, 1f - Mathf.Exp(-dt * 6f));
            }
            lastPos = Card.anchoredPosition;
            if (grab && up)
            {
                grab = false;
                if (cv != null) { cv.Held = false; cv.Vel = Vector2.zero; }
                if (valid) { dropped = true; ui.DropCard(this, ground); }
                else
                {
                    Sfx.Play("cx_deny", -8f);
                    if (marker != null) marker.gameObject.SetActive(false);
                    if (Hint != null) Hint.text = Loc.T("Arrastrá al minero a tu isla");
                }
            }
        }

        /// <summary>Donde caeria: sombra en el suelo y anillos de energia (solo sobre la isla).</summary>
        void UpdateGround(Vector2 sp)
        {
            ground = game.ScreenToGround(sp);
            valid = game.CanSummonAt(ground);
            if (marker == null)
            {
                marker = new GameObject("SombraCarta").transform;
                IslandArt.Blob(marker, 0.9f, 0.6f);
            }
            marker.gameObject.SetActive(valid);
            marker.position = ground + Vector3.up * 0.02f;
            ringT -= Time.unscaledDeltaTime;
            if (valid && ringT <= 0f)
            {
                ringT = 0.35f;
                Color face, edge, glow;
                IslandUi.CardMaterial(Island.Roster[Ch].Rarity, Golden, out face, out edge, out glow);
                FxApi.Play("ring", ground + Vector3.up * 0.05f, glow, 0.7f);
            }
        }
    }
}
