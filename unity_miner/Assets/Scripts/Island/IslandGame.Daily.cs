using Mineros.Audio;
using Mineros.Core;
using Mineros.UI;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Rutina diaria en el mundo (biblia 3.6): el dia de hoy para la racha y las misiones (se revisa tambien si el juego
    /// queda abierto pasada la medianoche) y el tablon de pedidos junto a la plaza, con un papel por pedido que brilla
    /// cuando esta listo para cobrar.
    /// </summary>
    public sealed partial class IslandGame
    {
        public static int Today { get { return (System.DateTime.Now.Date - new System.DateTime(2024, 1, 1)).Days; } }
        public bool DailyPending { get; private set; }
        float dayCheckT = 30f;
        Transform board;
        readonly MeshRenderer[] papers = new MeshRenderer[3];
        Vector3 boardPos;

        void InitDaily()
        {
            DailyPending = Isl.CheckDaily(Today);
            Isl.OrderDone += o =>
            {
                Ui.Toast(Loc.T("Pedido listo"), Kit3.Yellow, Mineros.UI.Icons.Get("chest"));
                Sfx.Play("bell", -8f, 1.2f);
                if (board != null) Juice.Punch(board, 0.15f, 0.3f);
            };
            Isl.MissionReady += m => { Ui.Toast(Loc.T("¡Misión cumplida! ") + m.Text, Kit.Green); Sfx.Play("jingle_small", -6f); };
            Isl.AllMissionsDone += () => Ui.Toast(Loc.T("¡Las tres misiones! Cofre de plata"), Kit3.Yellow);
        }

        void UpdateDaily(float dt)
        {
            dayCheckT -= dt;
            if (dayCheckT <= 0f)
            {
                dayCheckT = 30f;
                if (Isl.LastDay != Today && Isl.CheckDaily(Today)) { DailyPending = true; Ui.Toast(Loc.T("¡Nuevo día! Premio diario listo"), Kit3.Yellow); }
            }
            if (Isl.ClaimedDay == Today) DailyPending = false;
            UpdateBoard(dt);
        }

        // ------------------------------------------------------------ tablon de pedidos
        /// <summary>Lugar del tablon: al borde de la plaza, en el hueco mas grande entre caminos.</summary>
        Vector3 BoardSpot()
        {
            float best = 0f, bestGap = -1f;
            var angs = new System.Collections.Generic.List<float>();
            foreach (var p in Isl.Paths) if (p.From == 0 && p.X.Length > 2) angs.Add(Mathf.Atan2(p.Z[2], p.X[2]));
            angs.Sort();
            for (int i = 0; i < angs.Count; i++)
            {
                float a = angs[i], b = i + 1 < angs.Count ? angs[i + 1] : angs[0] + Mathf.PI * 2f;
                if (b - a > bestGap) { bestGap = b - a; best = (a + b) * 0.5f; }
            }
            if (angs.Count == 0) best = -1.2f;
            return new Vector3(Mathf.Cos(best), 0f, Mathf.Sin(best)) * (Island.PlazaR + 0.55f);
        }

        void MakeBoard()
        {
            boardPos = BoardSpot();
            board = new GameObject("Tablon").transform;
            board.SetParent(root, false);
            board.localPosition = boardPos;
            // de frente a la camara (mismo giro que los edificios)
            board.localRotation = Quaternion.Euler(0f, 35f, 0f);
            var mb = new MeshBuilder();
            Color wood = IslandArt.Wood, woodD = IslandArt.WoodD;
            for (int s = -1; s <= 1; s += 2) mb.Box(new Vector3(s * 0.62f, 0.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), woodD, 0f);
            mb.Box(new Vector3(0f, 0.98f, 0f), new Vector3(1.45f, 0.75f, 0.08f), wood, 0f);
            mb.Box(new Vector3(0f, 1.42f, 0f), new Vector3(1.6f, 0.12f, 0.18f), woodD, 0f);      // techito
            Material[] mats;
            var mesh = mb.ToMesh(null, "Tablon", out mats);
            mats = VertexColorMerge.Apply(mesh, mats);
            IslandArt.MakeRenderer(board, "Madera", mesh, mats);
            IslandArt.Blob(board, 0.9f, 0.35f);
            var pb = new MeshBuilder();
            pb.Box(Vector3.zero, new Vector3(0.36f, 0.48f, 0.02f), IslandArt.H("fff1cf"), 0.05f);
            pb.Box(new Vector3(0f, 0.2f, -0.012f), new Vector3(0.06f, 0.06f, 0.01f), IslandArt.H("e5484d"), 0.2f);   // chinche
            Material[] pm;
            var pmesh = pb.ToMesh(null, "Papel", out pm);
            pm = VertexColorMerge.Apply(pmesh, pm);
            for (int i = 0; i < 3; i++)
            {
                papers[i] = IslandArt.MakeRenderer(board, "Papel" + i, pmesh, pm, false);
                papers[i].transform.localPosition = new Vector3((i - 1) * 0.44f, 0.98f, -0.06f);
                papers[i].transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 4f);
            }
            FxApi.Play("dust", board.position, new Color(0.85f, 0.75f, 0.6f), 1.4f);
            Isl.AddBlocker(boardPos.x, boardPos.z, 1.1f);   // las vetas no nacen pegadas al tablon
        }

        void UpdateBoard(float dt)
        {
            if (!Isl.BoardOpen) return;
            if (board == null) MakeBoard();
            for (int i = 0; i < 3 && i < Isl.Orders.Count; i++)
            {
                var o = Isl.Orders[i];
                var p = papers[i];
                bool here = o.Wait <= 0f;
                if (p.gameObject.activeSelf != here) { p.gameObject.SetActive(here); if (here) Juice.Punch(p.transform, 0.4f, 0.3f); }
                if (!here) continue;
                // listo: brilla y se mece; si no, apenas se mueve con el viento
                float k = o.Done ? 0.5f + 0.5f * Mathf.Sin(Time.time * 6f + i) : 0f;
                var mpb = new MaterialPropertyBlock();
                p.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.3f) * k);
                p.SetPropertyBlock(mpb);
                p.transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2f + i) * 4f, 0f, (i - 1) * 4f + (o.Done ? Mathf.Sin(Time.time * 8f + i) * 6f : 0f));
            }
        }

        public bool BoardHasReady { get { foreach (var o in Isl.Orders) if (o.Done && o.Wait <= 0f) return true; return false; } }
        public Vector3 BoardWorld { get { return board != null ? board.position : Vector3.zero; } }

        bool TapBoard(Vector2 screen)
        {
            if (board == null) return false;
            Vector2 sp = Cam.WorldToScreenPoint(board.position + Vector3.up * 1f);
            if (Vector2.Distance(screen, sp) > 110f * (Cam.pixelHeight / 1544f)) return false;
            Juice.Punch(board, 0.12f, 0.25f);
            Sfx.Play("paper", -8f);
            Ui.OpenBoard();
            return true;
        }
    }
}
