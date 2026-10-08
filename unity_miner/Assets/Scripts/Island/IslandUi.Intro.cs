using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.IslandView
{
    /// <summary>
    /// Presentacion de la primera partida: antes del primer "tocá la roca" entra Old Gus (el ultimo minero de la isla) desde
    /// el costado y cuenta en 2 frases cortas por que estamos aca. Letra por letra con un blip, un toque completa la frase y
    /// el siguiente pasa a la proxima; "Saltar" arriba. Se ve una sola vez (PlayerPrefs) y la mano del tutorial espera.
    /// </summary>
    public sealed partial class IslandUi
    {
        // auditoria final (FTUE): eran 4 frases (~15-20 s leyendo antes de tocar nada). Accion primero: 2 frases y a picar
        static readonly string[] IntroLines =
        {
            "¡Mirá quién llegó! Soy el Viejo Gus. Esta isla esconde oro, cristales... ¡hasta diamantes!",
            "Vos tenés el pico. ¡Empezá tocando esa roca!",
        };
        const string IntroPref = "intro_gus_v1";

        // pose de Gus por frase: saluda, pico al hombro, pico al hombro, señala la roca
        static readonly string[] IntroPoses = { "gus_a", "gus_c" };
        static readonly System.Collections.Generic.Dictionary<string, Sprite> gusSprites = new System.Collections.Generic.Dictionary<string, Sprite>();
        Image introPic;

        static Sprite GusPose(int line)
        {
            string n = IntroPoses[Mathf.Clamp(line, 0, IntroPoses.Length - 1)];
            Sprite sp;
            if (gusSprites.TryGetValue(n, out sp)) return sp;
            var tex = Resources.Load<Texture2D>("UI/" + n);
            sp = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f) : null;
            gusSprites[n] = sp;
            return sp;
        }

        RectTransform introLayer, introGus, introBubble;
        Text introText, introHint;
        int introLine = -1;
        string introFull = "";
        float introShown, introNextBlip, introOpenT;
        bool introClosing;

        /// <summary>True mientras la presentacion esta en pantalla (la mano del tutorial y los avisos esperan).</summary>
        public bool IntroOpen { get { return introLayer != null && !introClosing; } }

        void UpdateIntro()
        {
            if (introLayer == null)
            {
                if (introLine >= IntroLines.Length) return;   // ya se mostro en esta sesion
                if (Isl.TutDone || Isl.Tut != Island.TutStep.TapRock || Isl.Stat("tap_breaks") > 0) return;
                if (PlayerPrefs.GetInt(IntroPref, 0) == 1 || onboardT < 0.9f) return;
                OpenIntro();
                return;
            }
            if (introClosing) return;
            introOpenT += Time.unscaledDeltaTime;
            // letra por letra (~38 por segundo) con un blip cada 2-3 letras que no sean espacios
            if (introShown < introFull.Length)
            {
                int before = (int)introShown;
                introShown = Mathf.Min(introFull.Length, introShown + Time.unscaledDeltaTime * 38f);
                int now = (int)introShown;
                if (now != before)
                {
                    introText.text = introFull.Substring(0, now);
                    if (Time.unscaledTime >= introNextBlip && !char.IsWhiteSpace(introFull[now - 1]))
                    {
                        Sfx.Play(now % 2 == 0 ? "blip" : "tick", -17f, 0.95f + (now % 5) * 0.05f);
                        introNextBlip = Time.unscaledTime + 0.075f;
                    }
                }
                if (introHint != null) introHint.enabled = false;
            }
            else if (introHint != null && !introHint.enabled)
            {
                introHint.enabled = true;
                Tw.Pop(introHint.rectTransform, 1.2f);
            }
            if (introHint != null && introHint.enabled)
                introHint.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f);
            // Gus "habla": un leve vaiven mientras escribe
            if (introPic != null)
            {
                float talk = introShown < introFull.Length ? Mathf.Sin(Time.unscaledTime * 14f) * 0.014f : 0f;
                introPic.rectTransform.localScale = new Vector3(1f - talk, 1f + talk, 1f);
            }
        }

        void OpenIntro()
        {
            introLayer = Layer("Presentacion", true);
            introClosing = false; introOpenT = 0f;
            Vector2 cs = Kit.CanvasSize;

            // velo: oscurece un poco la isla y se come los toques (todo el panel es un boton para avanzar)
            var veil = Kit.HitArea(introLayer, cs.x, cs.y, "Velo");
            Kit.Stretch((RectTransform)veil.transform);
            var veilImg = veil.GetComponent<Image>();
            veilImg.color = new Color(0.08f, 0.05f, 0.03f, 0f);
            Tw.To(veilImg, "velo", 0.35f, Ease.OutQuad, u => veilImg.color = new Color(0.08f, 0.05f, 0.03f, 0.42f * u));
            veil.Clicked += IntroTap;

            // Old Gus abajo a la izquierda, entra desde afuera con rebote
            float bw = Mathf.Min(cs.x - 32f, 680f), bh = 250f, bBottom = 46f;
            float gh = Mathf.Min(560f, cs.y * 0.36f), gw = gh;
            introGus = Kit.New("Gus", introLayer);
            // Gus de pie DETRAS del globo (estilo novela visual): la cintura queda tapada por el globo, nunca cortada por el borde
            Kit.Place(introGus, 0f, 1f, 4f, -(bBottom + bh * 0.55f) - gh, gw, gh);
            introPic = Kit.Img(introGus, GusPose(0), Color.white, "Retrato");
            introPic.preserveAspect = true;
            Kit.Stretch(introPic.rectTransform);
            Vector2 gTo = introGus.anchoredPosition, gFrom = gTo + new Vector2(-gw * 0.9f, 0f);
            introGus.anchoredPosition = gFrom;
            Tw.To(introGus, "entra", 0.55f, Ease.OutBack, u => introGus.anchoredPosition = Vector2.LerpUnclamped(gFrom, gTo, u));
            Sfx.Play("whoosh", -6f, 1.05f);

            // globo de dialogo crema arriba de Gus, con el nombre en una etiqueta naranja
            introBubble = Kit.OutBox(introLayer, 30, 6f, 8f, Kit.Cream, Kit.CreamD, "Globo");
            Kit.Place(introBubble, 0.5f, 1f, -bw * 0.5f, -bBottom - bh, bw, bh);   // globo abajo, sobre la cintura de Gus
            var tag = Kit.OutBox(introBubble, 18, 4f, 5f, Kit.Orange, Kit.OrangeD, "Nombre");
            Kit.Place(tag, 1f, 0f, -212f, -26f, 190f, 52f);   // nombre arriba a la derecha (a la izquierda asoma Gus)
            var tagTxt = Kit.Label(tag, Loc.T("Viejo Gus"), 28, Color.white, 6, true, TextAnchor.MiddleCenter, "NombreTxt");
            Kit.Stretch(tagTxt.rectTransform, 0, 0, 0, 5);
            introText = Kit.LabelAt(introBubble, "", 29, Kit.Brown, 0, false, 30f, 40f, bw - 60f, bh - 84f, TextAnchor.UpperLeft, "Texto");
            Kit.Wrap(introText);
            introText.lineSpacing = 1.05f;
            introHint = Kit.Label(introBubble, Loc.T("Tocá para seguir"), 22, Kit.OrangeD, 0, true, TextAnchor.MiddleRight, "Seguir");
            Kit.Place(introHint.rectTransform, 1f, 1f, -260f, -52f, 236f, 34f);
            introHint.enabled = false;
            introBubble.localScale = Vector3.zero;
            Tw.Scale(introBubble, Vector3.zero, Vector3.one, 0.38f, Ease.OutBack, 0.32f, () => Sfx.Play("pop", -6f, 1.1f));

            // saltar (arriba a la derecha, discreto)
            var skip = Kit.OutBox(introLayer, 20, 4f, 5f, new Color(1f, 1f, 1f, 0.92f), Kit.CreamD, "Saltar");
            Kit.Place(skip, 1f, 0f, -150f, 120f, 128f, 54f);
            var skipTxt = Kit.Label(skip, Loc.T("Saltar"), 24, Kit.Brown, 0, true, TextAnchor.MiddleCenter, "SaltarTxt");
            Kit.Stretch(skipTxt.rectTransform, 0, 0, 0, 5);
            var skipHit = Kit.HitArea(skip, 128f, 54f, "SaltarHit");
            Kit.Stretch((RectTransform)skipHit.transform);
            skipHit.Clicked += () => CloseIntro(true);

            introLine = -1;
            NextIntroLine();
        }

        void IntroTap()
        {
            if (introLayer == null || introClosing || introOpenT < 0.45f) return;
            if (introShown < introFull.Length)
            {
                // primer toque: completa la frase
                introShown = introFull.Length;
                introText.text = introFull;
                Sfx.Play("ui", -12f, 1.2f);
                return;
            }
            NextIntroLine();
        }

        void NextIntroLine()
        {
            introLine++;
            if (introLine >= IntroLines.Length) { CloseIntro(false); return; }
            if (introLine > 0)
            {
                Sfx.Play("paper", -10f, 1.1f);
                Tw.Pop(introBubble, 1.05f);
                var pose = GusPose(introLine);
                if (introPic != null && pose != introPic.sprite) { introPic.sprite = pose; Tw.Pop(introGus, 1.08f); }
            }
            introFull = Loc.T(IntroLines[introLine]);
            introShown = 0f;
            introText.text = "";
        }

        void CloseIntro(bool skipped)
        {
            if (introLayer == null || introClosing) return;
            introClosing = true;
            introLine = IntroLines.Length;
            PlayerPrefs.SetInt(IntroPref, 1);
            PlayerPrefs.Save();
            Isl.AddStat(skipped ? "intro_skip" : "intro_done", 1);
            Sfx.Play("whoosh", -7f, 0.9f);
            if (!skipped) Sfx.Play("jingle_small", -6f);
            var layer = introLayer;
            Vector2 gFrom = introGus.anchoredPosition, gTo = gFrom + new Vector2(-introGus.sizeDelta.x, 0f);
            Tw.To(introGus, "sale", 0.35f, Ease.InBack, u => introGus.anchoredPosition = Vector2.LerpUnclamped(gFrom, gTo, u));
            Tw.Scale(introBubble, Vector3.one, Vector3.zero, 0.25f, Ease.InBack, 0f, () =>
            {
                Destroy(layer.gameObject);
                if (introLayer == layer) { introLayer = null; introGus = introBubble = null; introText = introHint = null; introPic = null; }
            });
        }

        /// <summary>Capturas: avanza la presentacion como si el jugador tocara.</summary>
        public void DebugIntroTap() { if (introOpenT < 0.45f) introOpenT = 0.45f; IntroTap(); }
    }
}
