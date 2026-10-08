using System;
using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Mineros.UI
{
    // ==================================================================== Renacer con Esencia
    public sealed class EssencePanel : BasePanel
    {
        sealed class NodeUi
        {
            public EssenceNode N;
            public RectTransform Panel;
            public Text Lv, CostLbl, MaxLbl;
            public Btn B;
            public IconView CostIcon;
            public int LastLv = -1, LastState = -1;
        }

        Text gainL, balL, needL, essenceWord;
        Btn goBtn;
        readonly List<NodeUi> nodes = new List<NodeUi>();
        bool confirm;
        string lastGain = "", lastNeed = "";
        int lastBal = -1;

        protected override void Configure()
        {
            TitleText = "Renacer";
            FrameSize = new Vector2(660, 1180);
            RibbonCol = Kit.Purple;
        }

        protected override void Build()
        {
            // cabecera: que gano y cuanto tengo
            Image head = Kit.Box9(Body, "card", new Vector4(12, 12, 12, 16), Color.white, "Head");
            Kit.Item(head.rectTransform, -1, 196);
            IconView big = Kit.Icon(head.transform, "rebirth", 84);
            Kit.PlaceTL((RectTransform)big.transform, 14, 14, 84, 84);
            Kit.LabelAt(head.transform, "Ganás al renacer", 20, Kit.Brown, 0, false, 110, 10, 260, 26);
            gainL = Kit.LabelAt(head.transform, "+0", 56, Color.white, 12, true, 110, 32, 120, 66, TextAnchor.MiddleLeft);
            essenceWord = Kit.LabelAt(head.transform, "Esencia", 26, Icons.H("7a45c8"), 0, true, 222, 52, 140, 36);
            Kit.LabelAt(head.transform, "Tenés", 20, Kit.Brown, 0, false, 440, 10, 160, 26, TextAnchor.UpperRight);
            balL = Kit.LabelAt(head.transform, "0", 44, Color.white, 10, true, 400, 34, 200, 56, TextAnchor.MiddleRight);
            needL = Kit.LabelAt(head.transform, "", 21, Kit.Brown, 0, false, 14, 104, 588, 30, TextAnchor.UpperCenter);
            Text ex = Kit.LabelAt(head.transform, "Empezás de cero, pero más fuerte.\nLa Esencia compra mejoras eternas.", 20, Kit.Brown, 0, false,
                14, 128, 588, 60, TextAnchor.UpperCenter);
            ex.horizontalOverflow = HorizontalWrapMode.Wrap;

            goBtn = BodyButton("Renacer", Kit.Purple, 34, 84);
            goBtn.Clicked += OnGo;
            BodyText("Mejoras permanentes", 28, Color.white, TextAnchor.MiddleCenter, 40, 8, true);

            Kit.ScrollParts sc = Kit.Scroll(Body, 10, "Nodes", new Vector2(303, 158), 2);
            Kit.Item(sc.Scroll, -1, 0, -1, 1);
            for (int i = 0; i < Content.EssenceNodes.Length; i++) nodes.Add(NodeCard(sc.Content, Content.EssenceNodes[i]));
            Refresh();
        }

        NodeUi NodeCard(Transform parent, EssenceNode n)
        {
            NodeUi u = new NodeUi();
            u.N = n;
            Image p = Kit.Box9(parent, "card", new Vector4(12, 12, 12, 16), Color.white, "Node_" + n.Id);
            u.Panel = p.rectTransform;
            IconView ic = Kit.Icon(p.transform, n.Icon, 50);
            Kit.PlaceTL((RectTransform)ic.transform, 8, 8, 50, 50);
            Text t = Kit.LabelAt(p.transform, n.Title, 22, Color.white, 6, true, 64, 4, 232, 30);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            u.Lv = Kit.LabelAt(p.transform, "", 19, Icons.H("7a45c8"), 0, true, 64, 32, 232, 26);
            Text d = Kit.LabelAt(p.transform, n.Desc, 18, Kit.Brown, 0, false, 12, 62, 280, 40);
            d.horizontalOverflow = HorizontalWrapMode.Wrap;
            u.B = Kit.Button(p.transform, "", Kit.Purple, 24, 279, 46);
            Kit.PlaceTL(u.B.GetComponent<RectTransform>(), 12, 104, 279, 46);
            u.CostIcon = Kit.Icon(u.B.transform, "rebirth", 32);
            Kit.PlaceTL((RectTransform)u.CostIcon.transform, 92, 5, 32, 32);
            u.CostLbl = Kit.LabelAt(u.B.transform, "", 24, Color.white, 6, true, 130, 0, 130, 40, TextAnchor.MiddleLeft);
            u.MaxLbl = Kit.LabelAt(u.B.transform, "MÁX", 24, Color.white, 6, true, 0, 0, 279, 40, TextAnchor.MiddleCenter);
            u.MaxLbl.gameObject.SetActive(false);
            string nid = n.Id;
            RectTransform pr = p.rectTransform;
            u.B.Clicked += () =>
            {
                if (G.BuyEssence(nid))
                {
                    Sfx.Play("upgrade", -4f, 1.2f);
                    Kit.Buzz(20);
                    Tw.Pop(pr);
                }
            };
            return u;
        }

        public override void Refresh()
        {
            if (goBtn == null) return;
            bool can = G.CanRebirth();
            string gain = "+" + (can ? G.EssenceGainPreview() : 0);
            if (gain != lastGain)
            {
                lastGain = gain;
                gainL.text = gain;
                Kit.SetPos(essenceWord.rectTransform, 110f + gainL.preferredWidth + 10f, 52);
            }
            if (G.Essence != lastBal)
            {
                lastBal = G.Essence;
                balL.text = G.Essence.ToString();
            }
            string need = can ? "¡Ya podés renacer!" : "Se abre en la Etapa 3-1 (ahora " + G.StageLabel(G.RunMax) + ")";
            if (need != lastNeed)
            {
                lastNeed = need;
                needL.text = need;
            }
            goBtn.Interactable = can;
            if (!confirm) PanelUtil.SetText(goBtn, "Renacer");
            for (int i = 0; i < nodes.Count; i++)
            {
                NodeUi u = nodes[i];
                int lvl = G.EssenceLevel(u.N.Id);
                if (lvl != u.LastLv)
                {
                    u.LastLv = lvl;
                    u.Lv.text = "Nv " + lvl + " / " + u.N.Max;
                }
                bool maxed = G.EssenceMaxed(u.N.Id);
                int cost = maxed ? 0 : G.EssenceCost(u.N.Id);
                bool afford = !maxed && G.Essence >= cost;
                int state = (maxed ? 1 : 0) | (afford ? 2 : 0) | (cost << 2);
                if (state == u.LastState) continue;
                u.LastState = state;
                u.MaxLbl.gameObject.SetActive(maxed);
                u.CostLbl.gameObject.SetActive(!maxed);
                u.CostIcon.gameObject.SetActive(!maxed);
                u.B.Interactable = afford;
                if (!maxed)
                {
                    u.CostLbl.text = cost.ToString();
                    u.CostIcon.Img.color = afford ? Color.white : new Color(1, 1, 1, 0.5f);
                }
            }
        }

        void OnGo()
        {
            if (!G.CanRebirth()) return;
            if (!confirm)
            {
                confirm = true;
                goBtn.Label.text = "¿Seguro? Tocá de nuevo";
                goBtn.SetSkin(Kit.Skin(Kit.Green));
                Tw.After(this, "confirm", 3.5f, CancelConfirm);
                return;
            }
            Tw.Kill(this, "confirm");
            G.DoRebirth();
            Sfx.Play("rebirth");
            Kit.Buzz(60);
            Close();
        }

        void CancelConfirm()
        {
            confirm = false;
            goBtn.SetSkin(Kit.Skin(Kit.Purple));
            Refresh();
        }
    }

    // ==================================================================== Logros y datos
    public sealed class AchievementsPanel : BasePanel
    {
        sealed class ARow
        {
            public Achievement A;
            public RectTransform Panel;
            public Capsule Cap;
            public Btn B;
            public int State = -1;
            public float Frac = -1f;
            public int Idx;
        }

        public int StartTab;
        int tab;
        readonly Btn[] tabBtns = new Btn[2];
        Badge tabBadge;
        RectTransform listRoot, statsBox;
        readonly List<ARow> rows = new List<ARow>();
        readonly Dictionary<string, Text> statLbls = new Dictionary<string, Text>();
        readonly List<ARow> sorted = new List<ARow>();

        protected override void Configure()
        {
            TitleText = "Logros";
            FrameSize = new Vector2(660, 1120);
            RibbonCol = Icons.H("d9961c");
        }

        protected override void Build()
        {
            tab = StartTab;
            RectTransform hb = Kit.New("Tabs", Body);
            Kit.Item(hb, -1, 62);
            Kit.HBox(hb, 12, TextAnchor.MiddleCenter);
            string[] names = { "Logros", "Datos" };
            for (int i = 0; i < 2; i++)
            {
                Btn b = Kit.Button(hb, names[i], Kit.Blue, 26, 100, 62);
                Kit.Item(b, -1, 62, 1);
                int ii = i;
                b.Clicked += () => SetTab(ii);
                tabBtns[i] = b;
            }
            tabBadge = Kit.MakeBadge(tabBtns[0].transform);
            Kit.PlaceTL(tabBadge.Root, 236, -8, 26, 26);

            Kit.ScrollParts sc = Kit.Scroll(Body, 10);
            Kit.Item(sc.Scroll, -1, 0, -1, 1);
            listRoot = sc.Scroll.GetComponent<RectTransform>();
            for (int i = 0; i < Content.Achievements.Length; i++) rows.Add(MakeRow(sc.Content, Content.Achievements[i]));
            sorted.AddRange(rows);

            statsBox = Kit.New("Stats", Body);
            Kit.Item(statsBox, -1, 0, -1, 1);
            Kit.VBox(statsBox, 14);
            BuildStats();
            SetTab(tab);
            Refresh();
        }

        void SetTab(int i)
        {
            tab = i;
            listRoot.gameObject.SetActive(i == 0);
            statsBox.gameObject.SetActive(i == 1);
            for (int k = 0; k < 2; k++) tabBtns[k].SetSkin(Kit.Skin(k == i ? Kit.Blue : Kit.Gray));
            if (i == 1) RefreshStats();
        }

        ARow MakeRow(Transform parent, Achievement a)
        {
            ARow r = new ARow();
            r.A = a;
            r.Idx = rows.Count;
            const float h = 104f;
            Image p = Kit.Box9(parent, "card", new Vector4(12, 12, 12, 16), Color.white, "Ach_" + a.Id);
            Kit.Item(p.rectTransform, -1, h);
            r.Panel = p.rectTransform;
            IconView ic = Kit.Icon(p.transform, a.Icon, 62);
            Kit.PlaceTL((RectTransform)ic.transform, 12, 20, 62, 62);
            Text t = Kit.LabelAt(p.transform, a.Title, 26, Color.white, 7, true, 88, 6, 340, 34);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            Text d = Kit.LabelAt(p.transform, a.Desc, 20, Kit.Brown, 0, false, 88, 40, 340, 26);
            d.horizontalOverflow = HorizontalWrapMode.Wrap;
            d.verticalOverflow = VerticalWrapMode.Truncate;
            r.Cap = Kit.MakeCapsule(p.transform, 300, 20, "green_border", Color.white, new Color(0.55f, 0.5f, 0.6f, 1f), 15);
            Kit.PlaceTL(r.Cap.Root, 88, 72, 300, 20);
            IconView gi = Kit.Icon(p.transform, "gem", 38);
            Kit.PlaceTL((RectTransform)gi.transform, 470, 6, 38, 38);
            Kit.LabelAt(p.transform, "+" + a.Gems, 28, Color.white, 7, true, 506, 8, 90, 36);
            r.B = Kit.Button(p.transform, "Reclamar", Kit.Green, 22, 140, 50);
            Kit.PlaceTL(r.B.GetComponent<RectTransform>(), 456, 46, 140, 50);
            string aid = a.Id;
            RectTransform pr = p.rectTransform;
            r.B.Clicked += () =>
            {
                int n = G.ClaimAchievement(aid);
                if (n > 0)
                {
                    Sfx.Play("goal");
                    Kit.Buzz(25);
                    ToastMsg("+" + n + " gemas");
                    Tw.Pop(pr);
                }
            };
            return r;
        }

        static readonly string[,] StatDefs =
        {
            { "rocks", "Rocas picadas" }, { "gold", "Oro total" }, { "max_stage", "Etapa máxima" }, { "bosses", "Jefes vencidos" },
            { "rebirths", "Renacimientos" }, { "events", "Eventos vividos" }, { "plays_won", "Excavaciones ganadas" },
            { "collection", "Herramientas vistas" }, { "dps", "Daño por segundo" }, { "ips", "Oro por segundo" },
        };

        void BuildStats()
        {
            for (int i = 0; i < StatDefs.GetLength(0); i++)
            {
                RectTransform h = Kit.New("Stat", statsBox);
                Kit.Item(h, -1, 40);
                Kit.LabelAt(h, StatDefs[i, 1], 26, Kit.Brown, 0, false, 0, 0, 380, 40, TextAnchor.MiddleLeft);
                Text v = Kit.LabelAt(h, "0", 28, Color.white, 7, true, 380, 0, 236, 40, TextAnchor.MiddleRight);
                statLbls[StatDefs[i, 0]] = v;
            }
        }

        string StatText(string k)
        {
            switch (k)
            {
                case "max_stage": return G.StageLabel(G.MaxStage);
                case "dps": return BigNum.Fmt(G.MinersDps());
                case "ips": return BigNum.Fmt(G.IncomePerSec());
                case "collection": return G.Collection.Count + " / " + (Content.Ranks.Length * 2);
                case "gold": return BigNum.Fmt((double)G.Stat("gold"));
            }
            return BigNum.Fmt((double)G.Stat(k));
        }

        void RefreshStats()
        {
            foreach (KeyValuePair<string, Text> kv in statLbls) PanelUtil.SetText(kv.Value, StatText(kv.Key));
        }

        public override void Refresh()
        {
            if (rows.Count == 0) return;
            tabBadge.SetCount(G.AchievementsReadyCount());
            bool resort = false;
            for (int i = 0; i < rows.Count; i++)
            {
                ARow r = rows[i];
                Achievement a = r.A;
                bool claimed = G.AchClaimed.Contains(a.Id);
                bool ready = G.AchievementReady(a);
                int st = claimed ? 2 : (ready ? 0 : 1);
                long prog = G.AchievementProgress(a);
                float frac = (float)prog / Mathf.Max((float)a.Goal, 1f);
                if (st != r.State || Mathf.Abs(frac - r.Frac) > 0.0001f)
                {
                    if (st != r.State || st == 1) resort = true;
                    r.State = st;
                    r.Frac = frac;
                    string txt = claimed ? "Hecho" : (st == 0 ? "¡Listo!" : BigNum.Fmt((double)prog) + " / " + BigNum.Fmt((double)a.Goal));
                    r.Cap.Set(claimed ? 1f : frac, txt);
                    r.B.gameObject.SetActive(st == 0);
                    Color mod = claimed ? new Color(0.8f, 0.8f, 0.8f, 1f) : Color.white;
                    r.Panel.GetComponent<Image>().color = mod;
                    if (st == 0) r.Cap.SetFillKind("white_border", new Color(1f, 0.86f, 0.3f, 1f));
                    else r.Cap.SetFillKind("green_border", Color.white);
                }
            }
            if (resort)
            {
                // orden: listos, en curso (mas avanzados primero), reclamados
                sorted.Sort((x, y) =>
                {
                    if (x.State != y.State) return x.State.CompareTo(y.State);
                    if (x.State == 1 && x.Frac != y.Frac) return y.Frac.CompareTo(x.Frac);
                    return x.Idx.CompareTo(y.Idx);
                });
                for (int i = 0; i < sorted.Count; i++)
                    if (sorted[i].Panel.GetSiblingIndex() != i) sorted[i].Panel.SetSiblingIndex(i);
            }
            if (tab == 1) RefreshStats();
        }
    }
}
