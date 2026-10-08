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
    /// Carnet del minero (pedido del dueño despues de probarlo en el telefono): la tarjeta anterior era una hoja con
    /// fondo desenfocado que tapaba la isla y se abria sin querer. Ahora es un carnet chico abajo, SIN desenfoque ni
    /// oscurecer: el mundo sigue vivo y se puede seguir tocando. Foto propia (48 caras), nombre y ciudad de EE. UU.,
    /// clase, nivel con estrellas, energia/limpieza como barras con icono y un boton para entrenarlo (sube un nivel).
    /// Se cierra con la ✕, tocando el suelo o abriendo cualquier hoja; tocar otro minero lo reemplaza.
    /// </summary>
    public sealed partial class IslandUi
    {
        const float IdW = 680f, IdH = 286f;
        RectTransform minerCard;
        Miner cardMiner;
        System.Action cardRefresh;
        static readonly Dictionary<int, Sprite> faceCache = new Dictionary<int, Sprite>();

        /// <summary>Foto del minero (Resources/Faces/fNN), o el busto dibujado si falta.</summary>
        public static Sprite FaceSprite(Miner m) { return FaceSpriteId(m.Id, IslandGame.HelmetOf(m)); }

        public static Sprite FaceSpriteId(int minerId, Color helmet)
        {
            int i = Island.FaceOfId(minerId);
            Sprite s;
            if (faceCache.TryGetValue(i, out s) && s != null) return s;
            var tex = Resources.Load<Texture2D>("Faces/f" + i.ToString("00"));
            s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : Icons.MinerBust(helmet);
            faceCache[i] = s;
            return s;
        }

        public bool MinerCardOpen { get { return minerCard != null; } }

        public void CloseMinerCard()
        {
            if (minerCard == null) return;
            var old = minerCard;
            minerCard = null;
            cardMiner = null;
            cardRefresh = null;
            Tw.MoveFrom(old, old.anchoredPosition, old.anchoredPosition + new Vector2(0f, -IdH - 60f), 0.18f, Ease.InBack, 0f, () => { if (old != null) Destroy(old.gameObject); });
        }

        void ShowMinerCard(Miner m)
        {
            bool swap = minerCard != null;
            if (minerCard != null) { Destroy(minerCard.gameObject); minerCard = null; }
            cardMiner = m;
            var c = Island.Char(m);
            Color rc = m.Golden ? new Color(1f, 0.8f, 0.25f) : IslandGame.HelmetOf(m);

            var frame = Kit.Img(hudLayer, ButtonArt.Box(Kit.Cream, 30f, 8f, 0.3f), Color.white, "Carnet", true);
            frame.type = Image.Type.Sliced;
            var fr = frame.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0f);
            fr.pivot = new Vector2(0.5f, 0f);
            fr.sizeDelta = new Vector2(IdW, IdH);
            fr.anchoredPosition = new Vector2(0f, 150f);
            minerCard = fr;
            if (swap) Tw.Pop(fr, 1.04f);
            else Tw.MoveFrom(fr, new Vector2(0f, -IdH), new Vector2(0f, 150f), 0.26f, Ease.OutBack);

            // foto: marco del color de su rango, esquinas redondeadas
            var ring = Kit.RoundImg(fr, 26, rc, "MarcoFoto");
            Kit.PlaceTL(ring.rectTransform, 20, 20, 200, 200);
            var mask = Kit.RoundImg(ring.transform, 22, Color.white, "Mascara");
            Kit.Stretch(mask.rectTransform, 6, 6, 6, 6);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var photo = Kit.Img(mask.transform, FaceSprite(m), Color.white, "Foto");
            Kit.Stretch(photo.rectTransform);
            SpecBadge(fr, m.Char, 168, 166, 62, false);
            // rango: cinta bajo la foto
            var chip = Kit.OutBox(fr, 12, 3, 4, rc, Kit.Out, "Rango");
            Kit.PlaceTL(chip, 36, 226, 168, 40);
            var cl = Kit.Label(chip, m.Golden ? Loc.T("Dorado") : Island.RarityName[c.Rarity], 22, Color.white, 5, true, TextAnchor.MiddleCenter);
            Kit.Stretch(cl.rectTransform, 0, 0, 0, 4);

            // nombre real, ciudad y clase
            var name = Kit.LabelAt(fr, Island.FullName(m), 38, Kit.Brown, 0, true, 240, 16, 380, 48);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 26; name.resizeTextMaxSize = 38;
            Kit.LabelAt(fr, Island.Hometown(m) + "  ·  " + c.Name, 21, new Color(0.55f, 0.45f, 0.36f), 0, false, 242, 62, 400, 30);
            var st = Kit.LabelAt(fr, "", 21, Kit.OrangeD, 0, true, 242, 92, 400, 30);

            // nivel: estrella + numero, y la barra de experiencia
            var lvBox = Kit.OutBox(fr, 14, 3, 4, Kit.Yellow, Kit.Out, "Nivel");
            Kit.PlaceTL(lvBox, 242, 130, 112, 50);
            var lvIc = Kit.Icon(lvBox, "star", 34);
            Kit.PlaceTL((RectTransform)lvIc.transform, 6, 5, 34, 34);
            var lv = Kit.Label(lvBox, "", 28, Color.white, 5, true, TextAnchor.MiddleCenter);
            Kit.Stretch(lv.rectTransform, 38, 0, 4, 5);
            var xp = MiniBar(fr, 364, 146, 120, Kit.Yellow);

            // energia y limpieza: barras con icono (sin palabras)
            var enIc = Kit.Icon(fr, "power", 32);
            Kit.PlaceTL((RectTransform)enIc.transform, 242, 192, 32, 32);
            var en = MiniBar(fr, 280, 198, 140, Kit.Orange);
            var drop = Kit.Icon(fr, "drop", 30);
            Kit.PlaceTL((RectTransform)drop.transform, 434, 193, 30, 30);
            var clean = MiniBar(fr, 468, 198, 140, Kit.Blue);

            // amigo: su carita y el bono
            var friendFace = Kit.Img(fr, null, Color.white, "Amigo");
            Kit.PlaceTL(friendFace.rectTransform, 242, 234, 40, 40);
            var friend = Kit.LabelAt(fr, "", 20, Icons.H("e0577a"), 0, true, 290, 240, 200, 30);

            // entrenar: flecha arriba + precio (sin texto)
            var tb = Kit.Button(fr, "", Kit.Green, 28, 150, 92, "Entrenar");
            Kit.PlaceTL((RectTransform)tb.transform, 506, 134, 150, 92);
            var up = Kit.Icon(tb.transform, "arrow", 34);
            Kit.PlaceTL((RectTransform)up.transform, 58, 6, 34, 34);
            var coin = Kit.Icon(tb.transform, "coin", 28);
            Kit.PlaceTL((RectTransform)coin.transform, 14, 46, 28, 28);
            var price = Kit.Label(tb.transform, "", 26, Color.white, 5, true, TextAnchor.MiddleCenter);
            Kit.PlaceTL(price.rectTransform, 40, 40, 104, 40);
            tb.Clicked += () =>
            {
                if (!Isl.TrainMiner(m)) { NoMoney(tb); return; }
                Sfx.Play("powerup", -4f, 0.9f + m.Level * 0.04f);
                Juice.Vibrate(30);
                Tw.Pop(lvBox, 1.35f);
                Flash(new Color(1f, 0.92f, 0.55f), 0.18f);
                game.MinerSays(m);
                game.Save();
            };

            // ✕
            var x = Kit.RoundImg(fr, 26, new Color(0.36f, 0.27f, 0.2f, 0.92f), "Cerrar");
            x.raycastTarget = true;
            var xrt = x.rectTransform;
            xrt.anchorMin = xrt.anchorMax = new Vector2(1f, 1f);
            xrt.sizeDelta = new Vector2(52, 52);
            xrt.anchoredPosition = new Vector2(-30f, -30f);
            var xi = Kit.Icon(xrt, "close", 28);
            var xirt = (RectTransform)xi.transform;
            xirt.anchorMin = xirt.anchorMax = new Vector2(0.5f, 0.5f);
            xirt.anchoredPosition = Vector2.zero;
            x.gameObject.AddComponent<Btn>().Clicked += CloseMinerCard;

            cardRefresh = () =>
            {
                st.text = StateText(m);
                lv.text = m.Level.ToString();
                xp(m.Level >= Island.MaxMinerLevel ? 1f : m.Xp / (float)Island.XpFor(m.Level));
                en(m.Energy / 100f);
                clean(m.Clean / 100f);
                bool max = m.Level >= Island.MaxMinerLevel;
                price.text = max ? "MAX" : BigNum.Fmt(Isl.TrainCost(m));
                coin.gameObject.SetActive(!max);
                up.gameObject.SetActive(!max);
                tb.Interactable = !max && Isl.CanTrain(m);
                Miner fm = null;
                foreach (var o in Isl.Miners) if (o.Id == m.Friend) fm = o;
                friendFace.gameObject.SetActive(fm != null);
                if (fm != null) { friendFace.sprite = FaceSprite(fm); friend.text = Island.FirstName(fm) + "  +10%"; }
                else friend.text = "";
            };
            cardRefresh();
        }

        System.Action<float> MiniBar(Transform parent, float x, float y, float w, Color col)
        {
            var bar = Kit.OutBox(parent, 9, 2, 0, Icons.H("d8c6a2"), Kit.Out, "Barra");
            Kit.PlaceTL(bar, x, y, w, 22);
            var fill = Kit.RoundImg(bar, 7, col, "Relleno");
            var rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(2, 2); rt.offsetMax = new Vector2(2, -2);
            return f =>
            {
                f = Mathf.Clamp01(f);
                fill.gameObject.SetActive(f > 0.03f);
                rt.sizeDelta = new Vector2(Mathf.Max(14f, (w - 4f) * f), rt.sizeDelta.y);
            };
        }

        void UpdateMinerCard()
        {
            if (minerCard == null) return;
            if (sheet != null || cardMiner == null || !Isl.Miners.Contains(cardMiner)) { CloseMinerCard(); return; }
            if (Time.frameCount % 6 == 0) cardRefresh?.Invoke();
        }
    }
}
