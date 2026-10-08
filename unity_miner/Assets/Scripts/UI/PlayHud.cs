using System;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Game;
using Mineros.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mineros.UI
{
    /// <summary>Superficie tactil del joystick virtual flotante (cualquier dedo, desde y &gt; 160).</summary>
    public sealed class JoyPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Func<bool> Enabled;
        public Action<Vector2> Down;   // posicion en canvas (y hacia abajo)
        public Action<Vector2> Move;
        public Action Up;
        int pointerId = int.MinValue;

        public bool Active { get { return pointerId != int.MinValue; } }

        static Vector2 ToCanvasPos(PointerEventData e)
        {
            RectTransform c = Kit.CanvasRT;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(c, e.position, null, out local);
            Rect r = c.rect;
            return new Vector2(local.x - r.xMin, r.yMax - local.y);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (pointerId != int.MinValue || (Enabled != null && !Enabled())) return;
            Vector2 p = ToCanvasPos(e);
            if (p.y <= 160f) return;
            pointerId = e.pointerId;
            if (Down != null) Down(p);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            if (Move != null) Move(ToCanvasPos(e));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            pointerId = int.MinValue;
            if (Up != null) Up();
        }

        void OnDisable()
        {
            if (pointerId != int.MinValue)
            {
                pointerId = int.MinValue;
                if (Up != null) Up();
            }
        }
    }

    /// <summary>Resultado de Excavar: estrellas, oro y gemas (panel con titulo VICTORIA / Se acabo el tiempo).</summary>
    public sealed class ResultPanel : BasePanel
    {
        public bool Won;
        public double Gold;
        public int Gems;
        public int Stars;

        protected override void Configure()
        {
            TitleText = Won ? "¡VICTORIA!" : "¡Se acabó el tiempo!";
            FrameSize = new Vector2(620, 640);
            RibbonCol = Won ? Kit.Blue : Kit.Red;
        }

        protected override void Build()
        {
            if (Won)
            {
                RectTransform hs = Kit.New("Stars", Body);
                Kit.Item(hs, -1, 110);
                Kit.HBox(hs, 6, TextAnchor.MiddleCenter);
                for (int i = 0; i < 3; i++)
                {
                    IconView st = Kit.Icon(hs, "star", 96);
                    Kit.Item(st, 96 * Icons.Over, 96 * Icons.Over);
                    bool earned = i < Stars;
                    st.Img.color = earned ? Color.white : new Color(0.3f, 0.27f, 0.25f, 1f);
                    Tw.Reveal(st.transform, 0.25f + i * 0.22f);
                    int ii = i;
                    if (earned)
                        Tw.After(st, "snd", 0.25f + i * 0.22f, () => Sfx.Play("upgrade", -6f, 1f + ii * 0.12f));
                }
            }
            IconTextRow(Body, "coin", 64, BigNum.Fmt(Gold), 52, Kit.Yellow, 10, 76);
            if (Won)
            {
                BodyText("Bono de victoria x1.5", 24, Kit.Brown, TextAnchor.UpperCenter, 32);
                IconTextRow(Body, "gem", 56, "+" + Gems, 44, Color.white, 9, 64);
            }
            Spacer();
            Btn b = BodyButton("Continuar", Kit.Green, 32, 84);
            b.Clicked += Close;
        }
    }

    /// <summary>HUD del modo Excavar: tiempo, progreso, joystick, salir y resultado (play_hud.gd).</summary>
    public sealed class PlayHud : MonoBehaviour
    {
        public const float Duration = 60f;

        /// <summary>won, oro, gemas, estrellas (1-3 si gano, 0 si perdio o salio).</summary>
        public event Action<bool, double, int, int> Finished;

        GameManager gm;
        WorldController world;
        RectTransform safe;
        float timeLeft = Duration;
        bool running;
        bool resultShown;
        float countdown = 3.4f;
        int lastCount = int.MinValue, lastTick = -1;
        float tm;
        bool wasUrgent;

        Image progFill, timerFill;
        RectTransform progBar, timerBar;
        Text progLbl, timerLbl, countLbl, comboLbl;
        public Pill GoldPill { get; private set; }
        int lastPct = -1;
        // joystick
        Image joyRing, joyKnob;
        Vector2 joyCenter, joyPos;
        JoyPad pad;
        bool joyOn;
        double lastShownPlayGold = -1;

        public static PlayHud Create(Transform parent, RectTransform safeArea, GameManager manager)
        {
            RectTransform rt = Kit.New("PlayHud", parent);
            Kit.Stretch(rt);
            PlayHud p = rt.gameObject.AddComponent<PlayHud>();
            p.Build(safeArea, manager);
            return p;
        }

        void Build(RectTransform safeArea, GameManager manager)
        {
            gm = manager;
            world = gm.World;
            // zona tactil del joystick (debajo de los botones)
            Image padImg = Kit.Tint(transform, new Color(0, 0, 0, 0), "JoyPad");
            padImg.raycastTarget = true;
            Kit.Stretch(padImg.rectTransform);
            pad = padImg.gameObject.AddComponent<JoyPad>();
            pad.Enabled = () => running;
            pad.Down = OnJoyDown;
            pad.Move = OnJoyMove;
            pad.Up = OnJoyUp;
            joyRing = Kit.Img(transform, Icons.Ring(3f), new Color(1, 1, 1, 0.85f), "JoyRing");
            joyKnob = Kit.Img(transform, Icons.Dot(), new Color(1, 1, 1, 0.92f), "JoyKnob");
            foreach (Image im in new[] { joyRing, joyKnob })
            {
                im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0, 1);
                im.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            joyRing.rectTransform.sizeDelta = new Vector2(90f / 56f * 128f, 90f / 56f * 128f);
            joyKnob.rectTransform.sizeDelta = new Vector2(68, 68);
            joyRing.gameObject.SetActive(false);
            joyKnob.gameObject.SetActive(false);

            RectTransform s = safe = safeArea != null ? Kit.New("Safe", transform) : (RectTransform)transform;
            if (safeArea != null)
            {
                s.anchorMin = safeArea.anchorMin;
                s.anchorMax = safeArea.anchorMax;
                s.offsetMin = Vector2.zero;
                s.offsetMax = Vector2.zero;
            }
            Image band = Kit.Tint(s, Icons.H("4a3826"), "Band");
            RectTransform br = band.rectTransform;
            br.anchorMin = new Vector2(0, 1);
            br.anchorMax = new Vector2(1, 1);
            br.offsetMin = new Vector2(0, -58);
            br.offsetMax = new Vector2(0, 400);
            Image barBg = Kit.Box9(s, "progress_transparent", new Vector4(8, 8, 8, 8), new Color(0.55f, 0.45f, 0.35f, 1f), "ProgBar");
            progBar = barBg.rectTransform;
            progBar.anchorMin = new Vector2(0, 1);
            progBar.anchorMax = new Vector2(1, 1);
            progBar.offsetMin = new Vector2(6, -70);
            progBar.offsetMax = new Vector2(-6, -52);
            progFill = Kit.Box9(progBar, "progress_white_border", new Vector4(8, 8, 8, 8), new Color(1f, 0.92f, 0.6f, 1f), "Fill");
            Kit.PlaceTL(progFill.rectTransform, 0, 0, 18, 18);
            progFill.gameObject.SetActive(false);
            progLbl = Kit.Label(s, "0%", 20, Color.white, 6, true, TextAnchor.MiddleCenter, "ProgLabel");
            Kit.Place(progLbl.rectTransform, 0.5f, 0f, -60, 22, 120, 28);

            // barra de tiempo, reloj y oro (fila a 84 px)
            Image tb = Kit.Box9(s, "progress_transparent", new Vector4(8, 8, 8, 8), new Color(0.45f, 0.5f, 0.55f, 1f), "TimerBar");
            timerBar = tb.rectTransform;
            Kit.Place(timerBar, 0.5f, 0f, -250, 90, 340, 30);
            timerFill = Kit.Box9(timerBar, "progress_blue_border", new Vector4(8, 8, 8, 8), Color.white, "Fill");
            Kit.PlaceTL(timerFill.rectTransform, 0, 0, 340, 30);
            timerLbl = Kit.Label(timerBar, "60s", 22, Color.white, 6, true, TextAnchor.MiddleCenter);
            Kit.Stretch(timerLbl.rectTransform);
            IconView clock = Kit.Icon(s, "clock", 48);
            Kit.Place((RectTransform)clock.transform, 0.5f, 0f, -280, 81, 48, 48);
            GoldPill = Kit.MakePill(s, "coin", 180);
            Kit.Place(GoldPill.Root, 0.5f, 0f, 140, 84, 180, 46);

            Btn ex = Kit.Button(s, "Salir", Kit.Red, 24, 110, 58, "Exit");
            Kit.Place(ex.GetComponent<RectTransform>(), 0f, 1f, 12, -280, 110, 58);
            ex.Clicked += () => End(false, true);

            Text hint = Kit.Label(s, "Arrastrá para mover a tu minero", 24, Color.white, 7, true, TextAnchor.MiddleCenter, "Hint");
            Kit.Place(hint.rectTransform, 0.5f, 1f, -250, -380, 500, 40);
            CanvasGroup hg = Kit.Group(hint.gameObject);
            Tw.Alpha(hg, 0f, 0.6f, 6f);

            countLbl = Kit.Label(s, "3", 150, Color.white, 22, true, TextAnchor.MiddleCenter, "Count");
            Kit.Place(countLbl.rectTransform, 0.5f, 0.5f, -300, -160, 600, 200);
            comboLbl = Kit.Label(s, "", 34, Kit.Yellow, 9, true, TextAnchor.MiddleCenter, "Combo");
            Kit.Place(comboLbl.rectTransform, 0.5f, 0f, -200, 132, 400, 50);

            running = false;
            world.Paused = true;
            world.TimeBonus += OnTimeBonus;
            world.ComboChanged += OnCombo;
        }

        void OnDestroy()
        {
            if (world == null) return;
            world.TimeBonus -= OnTimeBonus;
            world.ComboChanged -= OnCombo;
            world.Joy = Vector2.zero;
        }

        void OnTimeBonus(float sec)
        {
            timeLeft = Mathf.Min(timeLeft + sec, Duration + 30f);
            Tw.Pop(timerLbl.transform);
        }

        // ---------------------------------------------------------------- joystick
        void OnJoyDown(Vector2 p)
        {
            joyCenter = p;
            joyPos = p;
            joyOn = true;
            joyRing.gameObject.SetActive(true);
            joyKnob.gameObject.SetActive(true);
            DrawJoy();
        }

        void OnJoyMove(Vector2 p)
        {
            joyPos = p;
            Vector2 d = joyPos - joyCenter;
            if (d.magnitude > 90f)
            {
                joyCenter = joyPos - d.normalized * 90f;
                d = joyPos - joyCenter;
            }
            // pantalla (y abajo) -> mundo (y arriba)
            world.Joy = new Vector2(d.x / 90f, -d.y / 90f);
            DrawJoy();
        }

        void OnJoyUp()
        {
            joyOn = false;
            world.Joy = Vector2.zero;
            joyRing.gameObject.SetActive(false);
            joyKnob.gameObject.SetActive(false);
        }

        void DrawJoy()
        {
            Vector2 knob = joyCenter + Vector2.ClampMagnitude(joyPos - joyCenter, 90f);
            joyRing.rectTransform.anchoredPosition = new Vector2(joyCenter.x, -joyCenter.y);
            joyKnob.rectTransform.anchoredPosition = new Vector2(knob.x, -knob.y);
        }

        void OnCombo(int n)
        {
            if (n < 3)
            {
                comboLbl.text = "";
                return;
            }
            float mult = 1f + Mathf.Min(n, 20) * 0.05f;
            comboLbl.text = "Combo " + n + "   x" + mult.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            comboLbl.color = n < 20 ? Kit.Yellow : Icons.H("ff7a3a");
            Tw.Pop(comboLbl.transform);
            if (n % 5 == 0)
            {
                Sfx.Play("combo", -4f, 1f + n * 0.02f);
                Kit.Buzz(15);
            }
        }

        // ---------------------------------------------------------------- bucle
        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            tm += dt;
            if (countdown > 0f && !resultShown)
            {
                countdown -= dt;
                int c = Mathf.CeilToInt(countdown - 0.4f);
                if (c != lastCount)
                {
                    lastCount = c;
                    countLbl.text = c > 0 ? c.ToString() : "¡A PICAR!";
                    countLbl.fontSize = c > 0 ? 150 : 96;
                    countLbl.color = Color.white;
                    Tw.Scale(countLbl.transform, Vector3.one * 1.6f, Vector3.one, 0.3f, Ease.OutBack);
                    Sfx.Play(c <= 0 ? "go" : "tick", -2f, 1f);
                }
                if (countdown <= 0f)
                {
                    running = true;
                    world.Paused = false;
                    Tw.To(countLbl, "fade", 0.3f, Ease.Linear, u =>
                    {
                        Color col = countLbl.color;
                        col.a = 1f - u;
                        countLbl.color = col;
                    });
                }
                return;
            }
            if (!running) return;
            timeLeft -= dt;
            bool urgent = timeLeft <= 10f;
            if (urgent != wasUrgent)
            {
                wasUrgent = urgent;
                timerFill.sprite = Kit.Tex9(urgent ? "progress_red_border" : "progress_blue_border", new Vector4(8, 8, 8, 8));
            }
            if (urgent)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(tm * 10f);
                timerFill.color = Color.Lerp(Color.white, new Color(1f, 0.72f, 0.72f, 1f), k);
                int sec = Mathf.CeilToInt(timeLeft);
                if (sec != lastTick)
                {
                    lastTick = sec;
                    Sfx.Play("tick", -4f, 1.4f);
                    Tw.Pop(timerLbl.transform);
                }
            }
            Kit.SetSize(timerFill.rectTransform, Mathf.Clamp(340f * timeLeft / Duration, 18f, 340f), 30);
            Kit.SetPos(timerFill.rectTransform, 0, 0);
            int secs = Mathf.CeilToInt(Mathf.Max(timeLeft, 0f));
            string tl = secs + "s";
            if (timerLbl.text != tl) timerLbl.text = tl;
            float prog = world.PlayProgress;
            bool showFill = prog > 0.01f;
            if (progFill.gameObject.activeSelf != showFill) progFill.gameObject.SetActive(showFill);
            Kit.SetSize(progFill.rectTransform, Mathf.Max(18f, Mathf.Max(progBar.rect.width, 20f) * prog), 18);
            Kit.SetPos(progFill.rectTransform, 0, 0);
            int pct = (int)(prog * 100f);
            if (pct != lastPct)
            {
                lastPct = pct;
                progLbl.text = pct + "%";
            }
            if (world.PlayGold != lastShownPlayGold)
            {
                lastShownPlayGold = world.PlayGold;
                GoldPill.Label.text = BigNum.Fmt(world.PlayGold);
            }
            if (prog >= 1f) End(true, false);
            else if (timeLeft <= 0f) End(false, false);
        }

        // ---------------------------------------------------------------- final
        void End(bool won, bool quit)
        {
            if (!running) return;
            running = false;
            world.Joy = Vector2.zero;
            if (joyOn) OnJoyUp();
            double gold = world.PlayGold * (won ? 1.5 : 1.0);
            int gems = won ? 15 : 0;
            if (quit)
            {
                if (Finished != null) Finished(false, world.PlayGold, 0, 0);
                return;
            }
            world.Paused = true;
            if (won)
            {
                Sfx.Play("clear");
                Kit.Buzz(50);
            }
            ShowResult(won, gold, gems, StarsFor(won));
        }

        int StarsFor(bool won)
        {
            if (!won) return 0;
            return timeLeft >= 25f ? 3 : (timeLeft >= 10f ? 2 : 1);
        }

        void ShowResult(bool won, double gold, int gems, int stars)
        {
            resultShown = true;
            ResultPanel p = BasePanel.Open<ResultPanel>(transform, gm, r =>
            {
                r.Won = won;
                r.Gold = gold;
                r.Gems = gems;
                r.Stars = stars;
            });
            p.Closed += () => { if (Finished != null) Finished(won, gold, gems, stars); };
        }
    }
}
