using System;
using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using UnityEngine.UI;
using FxJuice = Mineros.Fx.Juice;

namespace Mineros.UI
{
    /// <summary>Ayudas comunes de los paneles sencillos.</summary>
    static class PanelUtil
    {
        public static void SetText(Btn b, string s) { if (b.Label.text != s) b.Label.text = s; }

        public static void SetText(Text t, string s) { if (t.text != s) t.text = s; }

        public static RectTransform Header(Transform parent, string text, Color fill)
        {
            RectTransform box = Kit.OutBox(parent, 12, 3, 0, fill, Kit.Out, "Header");
            Kit.Item(box, -1, 44);
            Text l = Kit.Label(box, text, 24, Color.white, 6, true, TextAnchor.MiddleCenter);
            Kit.Stretch(l.rectTransform);
            return box;
        }

        /// <summary>Multiplica el color de todos los graficos del arbol (atenuar un elemento completo).</summary>
        public static void Modulate(Transform root, Color mod)
        {
            Graphic[] gs = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++)
            {
                if (gs[i] is Text) continue;
                gs[i].color = gs[i].color * mod;
            }
        }
    }

    // ==================================================================== Tienda
    public sealed class ShopPanel : BasePanel
    {
        RowParts chest, turbo;
        readonly List<RowParts> goldRows = new List<RowParts>();
        readonly List<double> goldSecs = new List<double>();
        float t;

        protected override void Configure()
        {
            TitleText = "Tienda";
            FrameSize = new Vector2(660, 1040);
            RibbonCol = Kit.Red;
        }

        protected override void Build()
        {
            Kit.ScrollParts sc = Kit.Scroll(Body, 12);
            Kit.Item(sc.Scroll, -1, 0, -1, 1);
            Transform v = sc.Content;
            PanelUtil.Header(v, "Gratis", Icons.H("a8402f"));
            chest = Row(v, "chest", "Cofre diario", "+30 gemas cada 4 horas.", "Abrir", Icons.H("fff2c2"), Kit.Green);
            chest.Button.Clicked += () =>
            {
                if (G.FreeChestReady())
                {
                    G.ClaimFreeChest();
                    Sfx.Play("gacha");
                    Kit.Buzz(30);
                    ToastMsg("+30 gemas");
                }
            };
            PanelUtil.Header(v, "Paquetes", Icons.H("a8402f"));
            turbo = Row(v, "power", "Paquete Turbo", "x2 permanente a Fuerza y Oro.", "800", Icons.H("dff0ff"), Kit.Blue);
            turbo.Button.Clicked += () =>
            {
                if (!G.Turbo && G.SpendGems(800))
                {
                    G.Turbo = true;
                    HudController.RefreshHud();
                    Sfx.Play("upgrade");
                    Kit.Buzz(30);
                }
            };
            RowParts tools = Row(v, "hammer", "Cofre de herramientas", "Una herramienta al azar (D 70% - C 25% - B 5%).", "100",
                Icons.H("efe2ff"), Kit.Purple);
            tools.Button.Clicked += () =>
            {
                if (G.Gacha() != null)
                {
                    Sfx.Play("gacha");
                    Kit.Buzz(30);
                    ToastMsg("¡Herramienta nueva en el Equipo!");
                }
            };
            PanelUtil.Header(v, "Oro por gemas", Icons.H("a8402f"));
            int[] gems = { 50, 200, 600 };
            double[] secs = { 120.0, 600.0, 2400.0 };
            string[] names = { "Saco de oro", "Carretilla de oro", "Vagón de oro" };
            for (int i = 0; i < 3; i++)
            {
                int gcost = gems[i];
                double sec = secs[i];
                RowParts r = Row(v, "money", names[i], "", gcost.ToString(), Icons.H("fff6dc"), Kit.Blue);
                r.Button.Clicked += () =>
                {
                    double amount = Math.Max(G.IncomePerSec(), 1.0) * sec;
                    if (G.SpendGems(gcost))
                    {
                        G.AddGold(amount);
                        Sfx.Play("coin");
                        Kit.Buzz(20);
                        ToastMsg("+" + BigNum.Fmt(amount) + " oro");
                    }
                };
                goldRows.Add(r);
                goldSecs.Add(sec);
            }
            Refresh();
        }

        public override void Refresh()
        {
            if (chest == null) return;
            bool ready = G.FreeChestReady();
            chest.Button.Interactable = ready;
            PanelUtil.SetText(chest.Button, ready ? "Abrir" : BigNum.TimeHms(G.FreeChestAt - G.Now()));
            turbo.Button.Interactable = !G.Turbo;
            PanelUtil.SetText(turbo.Button, G.Turbo ? "Activo" : "800");
            for (int i = 0; i < goldRows.Count; i++)
            {
                double s = goldSecs[i];
                PanelUtil.SetText(goldRows[i].Desc, "+" + BigNum.Fmt(Math.Max(G.IncomePerSec(), 1.0) * s) + " oro (" + BigNum.TimeHms(s) + " de producción)");
            }
        }

        protected override void OnTick(float dt)
        {
            t += dt;
            if (t > 0.25f)
            {
                t = 0f;
                Refresh();
            }
        }
    }

    // ==================================================================== Pase y misiones
    public sealed class MissionsPanel : BasePanel
    {
        sealed class MRow
        {
            public Mission M;
            public RowParts R;
            public Image Fill;
            public int LastP = -1, LastState = -1;
        }

        Text lvlLbl, passLbl, timerLbl;
        Image passFill;
        RectTransform passBar;
        readonly List<MRow> rows = new List<MRow>();
        Image[] tileBox = new Image[5];
        Image[] tileInner = new Image[5];
        IconView[] tileIcon = new IconView[5];
        Text[] tileReward = new Text[5];
        Text[] tileNum = new Text[5];
        int lastSig = int.MinValue;
        int lastSec = -1;
        RectTransform listContent;

        protected override void Configure()
        {
            TitleText = "Pase de Minero";
            FrameSize = new Vector2(660, 1100);
            RibbonCol = Kit.Purple;
        }

        protected override void Build()
        {
            RectTransform top = Kit.New("Top", Body);
            Kit.Item(top, -1, 60);
            lvlLbl = Kit.LabelAt(top, "Nv.1", 28, Color.white, 7, true, 0, 10, 80, 40);
            Image pb = Kit.Box9(top, "progress_transparent", new Vector4(8, 8, 8, 8), new Color(0.5f, 0.42f, 0.62f, 1f), "PassBar");
            passBar = pb.rectTransform;
            Kit.PlaceTL(passBar, 80, 16, 500, 28);
            passFill = Kit.Box9(passBar, "progress_white_border", new Vector4(8, 8, 8, 8), new Color(0.85f, 0.65f, 1f, 1f), "Fill");
            Kit.PlaceTL(passFill.rectTransform, 0, 0, 18, 28);
            passLbl = Kit.Label(passBar, "0/100", 20, Color.white, 6, true, TextAnchor.MiddleCenter);
            Kit.Stretch(passLbl.rectTransform);

            RectTransform rewards = Kit.New("Rewards", Body);
            Kit.Item(rewards, -1, 120);
            Kit.HBox(rewards, 10, TextAnchor.MiddleLeft);
            for (int i = 0; i < 5; i++)
            {
                RectTransform tile = Kit.OutBox(rewards, 16, 3, 3, Icons.H("6a4f99"), Kit.Out, "Tile");
                Kit.Item(tile, 114, 114);
                tileInner[i] = tile.Find("Inner").GetComponent<Image>();
                tileIcon[i] = Kit.Icon(tile, "gem", 54);
                Kit.PlaceTL((RectTransform)tileIcon[i].transform, 30, 14, 54, 54);
                tileReward[i] = Kit.LabelAt(tile, "0", 22, Color.white, 6, true, 0, 70, 114, 30, TextAnchor.UpperCenter);
                tileNum[i] = Kit.LabelAt(tile, "0", 18, Kit.Yellow, 5, true, 8, 2, 60, 24);
            }

            RectTransform hdr = Kit.New("Header", Body);
            Kit.Item(hdr, -1, 40);
            Kit.LabelAt(hdr, "Misiones diarias", 28, Color.white, 7, true, 0, 0, 616, 40, TextAnchor.UpperCenter);
            timerLbl = Kit.LabelAt(hdr, "", 20, Kit.Brown, 0, false, 470, 8, 146, 30, TextAnchor.UpperRight);

            Kit.ScrollParts sc = Kit.Scroll(Body, 10);
            Kit.Item(sc.Scroll, -1, 0, -1, 1);
            listContent = sc.Content;
            for (int i = 0; i < Content.Missions.Length; i++)
            {
                Mission m = Content.Missions[i];
                MRow mr = new MRow();
                mr.M = m;
                mr.R = Row(listContent, "mission", m.Title + "  +" + m.Gems, m.Desc, "Reclamar", Color.white, Kit.Purple, 104);
                mr.R.Desc.horizontalOverflow = HorizontalWrapMode.Overflow;
                RectTransform dr = mr.R.Desc.rectTransform;
                dr.sizeDelta = new Vector2(330, 30);
                Kit.SetPos(dr, 90, 44);
                IconView gi = Kit.Icon(mr.R.Panel, "gem", 26);
                Kit.PlaceTL((RectTransform)gi.transform, 90 + mr.R.Title.preferredWidth + 8f, 12, 26, 26);
                Image bar = Kit.Box9(mr.R.Panel, "progress_transparent", new Vector4(8, 8, 8, 8), new Color(0.75f, 0.7f, 0.8f, 1f), "Bar");
                Kit.PlaceTL(bar.rectTransform, 90, 78, 240, 16);
                mr.Fill = Kit.Box9(bar.transform, "progress_white_border", new Vector4(8, 8, 8, 8), new Color(0.85f, 0.65f, 1f, 1f), "Fill");
                Kit.PlaceTL(mr.Fill.rectTransform, 0, 0, 16, 16);
                Mission mm = m;
                mr.R.Button.Clicked += () =>
                {
                    G.ClaimMission(mm);
                    Sfx.Play("gacha");
                    Kit.Buzz(25);
                };
                rows.Add(mr);
            }
            Refresh();
        }

        protected override void OnTick(float dt)
        {
            int sec = DateTime.Now.Hour * 3600 + DateTime.Now.Minute * 60 + DateTime.Now.Second;
            if (sec == lastSec) return;
            lastSec = sec;
            int left = 86400 - sec;
            timerLbl.text = (left / 3600).ToString("00") + ":" + ((left % 3600) / 60).ToString("00") + ":" + (left % 60).ToString("00");
        }

        public override void Refresh()
        {
            if (rows.Count == 0) return;
            int sg = G.PassXp * 31 + G.Claimed.Count;
            for (int i = 0; i < rows.Count; i++) sg = sg * 31 + G.MissionProgress(rows[i].M);
            if (sg == lastSig) return;
            lastSig = sg;
            int lv = G.PassLevel();
            lvlLbl.text = "Nv." + lv;
            int xp = G.PassXp % 100;
            Kit.SetSize(passFill.rectTransform, Mathf.Max(18f, 500f * xp / 100f), 28);
            Kit.SetPos(passFill.rectTransform, 0, 0);
            passLbl.text = xp + "/100";
            for (int i = 0; i < 5; i++)
            {
                int l = lv + i;
                tileInner[i].color = i == 0 ? Icons.H("c58bff") : Icons.H("6a4f99");
                tileIcon[i].SetKind(l % 3 != 0 ? "gem" : "chest");
                tileReward[i].text = G.PassReward(l + 1).ToString();
                tileNum[i].text = (l + 1).ToString();
            }
            for (int i = 0; i < rows.Count; i++)
            {
                MRow r = rows[i];
                bool done = G.Claimed.Contains(r.M.Id);
                bool ready = G.MissionReady(r.M);
                int p = G.MissionProgress(r.M);
                int state = ready ? 0 : (done ? 2 : 1);
                if (p != r.LastP || state != r.LastState)
                {
                    r.LastP = p;
                    r.LastState = state;
                    PanelUtil.SetText(r.R.Button, done ? "Hecho" : "Reclamar");
                    r.R.Button.Interactable = ready;
                    PanelUtil.SetText(r.R.Desc, r.M.Desc + "  (" + p + "/" + r.M.Goal + ")");
                    r.Fill.gameObject.SetActive(p > 0);
                    Kit.SetSize(r.Fill.rectTransform, Mathf.Max(16f, 240f * p / r.M.Goal), 16);
                    Kit.SetPos(r.Fill.rectTransform, 0, 0);
                }
            }
            // orden: listas, en curso, hechas (estable por indice)
            for (int s = 2; s >= 0; s--)
                for (int i = rows.Count - 1; i >= 0; i--)
                    if (rows[i].LastState == s) rows[i].R.Panel.SetSiblingIndex(0);
        }
    }

    // ==================================================================== Premio diario
    public sealed class DailyPanel : BasePanel
    {
        protected override void Configure()
        {
            TitleText = "Premio diario";
            FrameSize = new Vector2(640, 860);
            RibbonCol = Kit.Orange;
        }

        protected override void Build()
        {
            RectTransform grid = Kit.New("Grid", Body);
            Kit.Item(grid, -1, 394);
            GridLayoutGroup gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(186, 190);
            gl.spacing = new Vector2(14, 14);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.UpperCenter;
            int todayI = G.LoginDay % 7;
            for (int i = 0; i < 6; i++) Day(grid, i, todayI, new Vector2(186, 190));
            RectTransform wrap = Kit.New("Wide", Body);
            Kit.Item(wrap, -1, 170);
            RectTransform wide = Day(wrap, 6, todayI, new Vector2(586, 170));
            Kit.PlaceTL(wide, (FrameSize.x - 44f - 586f) * 0.5f, 0, 586, 170);
            bool avail = G.LoginAvailable();
            Btn b = BodyButton(avail ? "Reclamar" : "Vuelve mañana", Kit.Green, 30, 84);
            b.Interactable = avail;
            b.Clicked += () =>
            {
                int g = G.ClaimLogin();
                if (g > 0)
                {
                    Sfx.Play("gacha");
                    Kit.Buzz(30);
                    ToastMsg("+" + g + " gemas");
                }
                Close();
            };
        }

        RectTransform Day(Transform parent, int i, int todayI, Vector2 sz)
        {
            bool isToday = i == todayI && G.LoginAvailable();
            bool done = i < todayI || (i == todayI && !G.LoginAvailable());
            Color headCol = isToday ? Icons.H("ffc93a") : Kit.Purple;
            RectTransform p = Kit.OutBox(parent, 16, isToday ? 4 : 3, 3, isToday ? Icons.H("fff2b0") : Icons.H("fff7e6"),
                isToday ? Color.white : Kit.Out, "Day" + i);
            p.sizeDelta = sz;
            Image head = Kit.RoundImg(p, 12, headCol, "Head");
            Kit.PlaceTL(head.rectTransform, 4, 4, sz.x - 8, 40);
            Text t = Kit.Label(head.transform, "Día " + (i + 1), 24, Color.white, 6, true, TextAnchor.MiddleCenter);
            Kit.Stretch(t.rectTransform);
            IconView ic = Kit.Icon(p, "gem", 76);
            Kit.PlaceTL((RectTransform)ic.transform, (sz.x - 76) * 0.5f, 54, 76, 76);
            Kit.LabelAt(p, Content.DailyGems[i].ToString(), 28, Color.white, 7, true, 0, sz.y - 50, sz.x, 40, TextAnchor.UpperCenter);
            if (done)
            {
                PanelUtil.Modulate(p, new Color(0.7f, 0.7f, 0.7f, 1f));
                IconView ok = Kit.Icon(p, "check", 64);
                Kit.PlaceTL((RectTransform)ok.transform, sz.x - 74, 38, 64, 64);
            }
            return p;
        }
    }

    // ==================================================================== Bienvenida sin conexion
    public sealed class WelcomePanel : BasePanel
    {
        protected override void Configure()
        {
            TitleText = "¡Bienvenido de nuevo!";
            FrameSize = new Vector2(640, 500);
            RibbonCol = Kit.Blue;
        }

        protected override void Build()
        {
            BodyText("Estuviste fuera " + BigNum.TimeHms(G.PendingOfflineTime), 26, Color.white, TextAnchor.UpperCenter, 36, 7, true);
            IconView m = Kit.Icon(Body, "miner", 150);
            Kit.Item(m, -1, 150);
            IconTextRow(Body, "coin", 56, BigNum.Fmt(G.PendingOffline), 46, Kit.Yellow, 10, 64);
            BodyText("Mineros al " + Mathf.RoundToInt((float)(G.OfflineEff() * 100.0)) + "% (tope " + Mathf.RoundToInt((float)(G.OfflineCap() / 3600.0)) + " h)",
                22, Kit.Brown, TextAnchor.UpperCenter, 30);
            Btn b = BodyButton("Recoger", Kit.Green, 32, 84);
            b.Clicked += Close;
        }

        public override void Close()
        {
            if (Closing) return;
            if (G.PendingOffline > 0.0)
            {
                G.CollectOffline();
                Sfx.Play("coin");
                Kit.Buzz(25);
            }
            base.Close();
        }
    }

    // ==================================================================== Alcancia
    public sealed class PiggyPanel : BasePanel
    {
        Text lbl;
        Btn b;

        protected override void Configure()
        {
            TitleText = "Alcancía";
            FrameSize = new Vector2(600, 600);
            RibbonCol = Icons.H("f28aa8");
        }

        protected override void Build()
        {
            IconView ic = Kit.Icon(Body, "piggy", 150);
            Kit.Item(ic, -1, 150);
            BodyText("La alcancía junta 1 gema por minuto (máx. " + Balance.PiggyCap + "), incluso sin conexión.", 22, Kit.Brown, TextAnchor.UpperCenter, 60);
            lbl = BodyText("", 44, Color.white, TextAnchor.MiddleCenter, 60, 9, true);
            b = BodyButton("Romper", Kit.Green, 30, 84);
            b.Clicked += () =>
            {
                int n = G.CollectPiggy();
                if (n > 0)
                {
                    Sfx.Play("gacha");
                    Kit.Buzz(30);
                    ToastMsg("+" + n + " gemas");
                }
                Close();
            };
            Refresh();
        }

        public override void Refresh()
        {
            if (lbl == null) return;
            PanelUtil.SetText(lbl, G.Piggy + " / " + Balance.PiggyCap);
            b.Interactable = G.Piggy > 0;
        }
    }

    // ==================================================================== Ajustes
    public sealed class SettingsPanel : BasePanel
    {
        Btn snd, mus, eco, vib, resetB;
        bool confirm;
        float confirmT;

        protected override void Configure()
        {
            TitleText = "Ajustes";
            FrameSize = new Vector2(600, 720);
            RibbonCol = Kit.GrayD;
        }

        protected override void Build()
        {
            snd = BodyButton("", Kit.Blue, 28, 80);
            snd.Clicked += () =>
            {
                G.Sound = !G.Sound;
                Sfx.SoundOn = G.Sound;
                Refresh();
            };
            mus = BodyButton("", Kit.Blue, 28, 80);
            mus.Clicked += () =>
            {
                bool on = !G.Music;
                G.Music = on;
                Sfx.SetMusic(on, G.Biome());
                Refresh();
            };
            eco = BodyButton("", Kit.Blue, 28, 80);
            eco.Clicked += () =>
            {
                G.Eco = !G.Eco;
                Mineros.Fx.Fx.Eco = G.Eco;
                Refresh();
                HudController.RefreshHud();
            };
            vib = BodyButton("", Kit.Blue, 28, 80);
            vib.Clicked += () =>
            {
                HudController.SetVibration(!FxJuice.VibrationOn);
                if (FxJuice.VibrationOn) Kit.Buzz(30);
                Refresh();
            };
            resetB = BodyButton("Borrar progreso", Kit.Red, 28, 80);
            resetB.Clicked += () =>
            {
                if (confirm)
                {
                    G.ResetAll();
                    Close();
                }
                else
                {
                    confirm = true;
                    confirmT = 4f;
                    resetB.Label.text = "¿Seguro? Tocá otra vez";
                }
            };
            BodyText("Mineros Idle v1.0", 20, Kit.Brown, TextAnchor.UpperCenter, 30);
            Refresh();
        }

        protected override void OnTick(float dt)
        {
            if (confirm)
            {
                confirmT -= dt;
                if (confirmT <= 0f)
                {
                    confirm = false;
                    resetB.Label.text = "Borrar progreso";
                }
            }
        }

        public override void Refresh()
        {
            if (snd == null || vib == null) return;
            snd.Label.text = "Sonido: " + (G.Sound ? "SÍ" : "NO");
            mus.Label.text = "Música: " + (G.Music ? "SÍ" : "NO");
            eco.Label.text = "Modo Eco: " + (G.Eco ? "SÍ" : "NO");
            vib.Label.text = "Vibración: " + (FxJuice.VibrationOn ? "SÍ" : "NO");
        }
    }

    // ==================================================================== Cascos
    public sealed class SkinPanel : BasePanel
    {
        sealed class Tile
        {
            public RectTransform Root;
            public Image Outer;
            public RectTransform Preview;
            public Btn B;
            public Text Name;
            public int LastState = -1;
        }

        readonly List<Tile> tiles = new List<Tile>();
        float tm;

        protected override void Configure()
        {
            TitleText = "Cascos";
            FrameSize = new Vector2(640, 900);
            RibbonCol = Kit.Orange;
        }

        protected override void Build()
        {
            RectTransform grid = Kit.New("Grid", Body);
            Kit.Item(grid, -1, 2 * 290 + 14);
            GridLayoutGroup gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(186, 290);
            gl.spacing = new Vector2(14, 14);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.UpperCenter;
            for (int i = 0; i < Content.Skins.Length; i++)
            {
                Core.Skin s = Content.Skins[i];
                Tile t = new Tile();
                RectTransform root = Kit.OutBox(grid, 16, 3, 3, Icons.H("fff7e6"), Kit.Out, "Skin" + i);
                t.Root = root;
                t.Outer = root.Find("Outer").GetComponent<Image>();
                Color col = Icons.H(s.ColorHex);
                Image prev = Kit.Img(root, Icons.MinerBust(col), Color.white, "Preview");
                prev.preserveAspect = true;
                t.Preview = prev.rectTransform;
                Kit.PlaceTL(t.Preview, 11.75f, 27.8f, Icons.MinerCanvasW * 1.15f, Icons.MinerCanvasH * 1.15f);
                t.Name = Kit.LabelAt(root, s.Name, 24, Color.white, 6, true, 0, 168, 186, 30, TextAnchor.UpperCenter);
                t.B = Kit.Button(root, "", Kit.Green, 22, 160, 60);
                Kit.PlaceTL(t.B.GetComponent<RectTransform>(), 13, 210, 160, 60);
                int idx = i;
                t.B.Clicked += () =>
                {
                    int before = G.Skin;
                    G.BuySkin(idx);
                    if (G.Skin != before) Kit.Buzz(20);
                };
                tiles.Add(t);
            }
            BodyText("El casco Oro puro da +10% de oro.", 22, Kit.Brown, TextAnchor.UpperCenter, 30);
            Refresh();
        }

        protected override void OnTick(float dt)
        {
            tm += dt;
            for (int i = 0; i < tiles.Count; i++)
            {
                float bob = Mathf.Sin(tm * 3f + i) * 0.8f;
                Kit.SetPos(tiles[i].Preview, 11.75f, 27.8f + bob);
            }
        }

        public override void Refresh()
        {
            if (tiles.Count == 0) return;
            for (int i = 0; i < tiles.Count; i++)
            {
                Tile t = tiles[i];
                bool owned = G.SkinsOwned.Contains(i);
                bool current = G.Skin == i;
                int state = (current ? 1 : 0) | (owned ? 2 : 0) | (G.Gems >= Content.Skins[i].Cost ? 4 : 0);
                if (state == t.LastState) continue;
                t.LastState = state;
                t.Outer.color = current ? Kit.Green : Kit.Out;
                string txt = current ? "Usando" : (owned ? "Usar" : Content.Skins[i].Cost.ToString());
                t.B.SetSkin(Kit.Skin(owned ? Kit.Green : Kit.Blue));
                PanelUtil.SetText(t.B, txt);
                t.B.Interactable = !current;
            }
        }
    }
}
