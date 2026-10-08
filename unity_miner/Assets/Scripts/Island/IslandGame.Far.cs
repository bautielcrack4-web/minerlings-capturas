using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Isla lejana (vista): la islita en el horizonte, la niebla que la tapa hasta descubrirla, la balsa del Muelle
    /// (crece con el nivel: troncos, vela, baranda) y el pedido de la camara para poder ir hasta alla.
    /// </summary>
    public sealed partial class IslandGame
    {
        Transform farRoot, farFog, raftT;
        int raftBuiltSeats = -1;
        float fogGone = -1f;   // -1 niebla puesta; 0..1 se va; >1 ya no esta
        readonly List<Transform> fogClouds = new List<Transform>();

        public Vector3 FarCenter { get { return new Vector3(Island.FarX, 0f, Island.FarZ); } }

        void InitFar()
        {
            farRoot = new GameObject("IslaLejana").transform;
            farRoot.SetParent(root, false);
            farRoot.localPosition = FarCenter;
            // suelo: playa y pasto en escalones suaves, como la isla de casa
            var mb = new MeshBuilder();
            mb.Cyl(new Vector3(0, -0.9f, 0), new Vector3(0, -0.05f, 0), Island.FarR + 1.4f, Island.FarR + 0.9f, 28, IslandArt.SandD, 0f);
            mb.Cyl(new Vector3(0, -0.05f, 0), new Vector3(0, 0f, 0), Island.FarR + 0.9f, Island.FarR + 0.8f, 28, IslandArt.Sand, 0f);
            mb.Cyl(new Vector3(0, -0.02f, 0), new Vector3(0, 0.04f, 0), Island.FarR - 0.4f, Island.FarR - 0.6f, 28, IslandArt.Grass, 0f);
            // espuma alrededor
            const int N = 40;
            for (int i = 0; i < N; i++)
            {
                float a0 = Mathf.PI * 2f * i / N, a1 = Mathf.PI * 2f * (i + 1) / N;
                float r0 = Island.FarR + 1.5f, r1 = r0 + 0.7f;
                mb.Quad(new Vector3(Mathf.Cos(a0) * r0, -0.36f, Mathf.Sin(a0) * r0), new Vector3(Mathf.Cos(a1) * r0, -0.36f, Mathf.Sin(a1) * r0),
                        new Vector3(Mathf.Cos(a1) * r1, -0.36f, Mathf.Sin(a1) * r1), new Vector3(Mathf.Cos(a0) * r1, -0.36f, Mathf.Sin(a0) * r1), Vector3.up, IslandArt.Foam, 0.2f);
            }
            // palmeras y piedras (siempre iguales)
            var rnd = new System.Random(4242);
            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f + (float)rnd.NextDouble() * 0.08f) * Mathf.PI * 2f, d = Island.FarR - 1.2f - (float)rnd.NextDouble() * 1.2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if ((p + farRoot.localPosition - new Vector3(Island.FarBeachX, 0, Island.FarBeachZ)).sqrMagnitude < 9f) continue;   // la playa de la balsa, libre
                Palm(mb, p, 1.6f + (float)rnd.NextDouble() * 0.9f, rnd.Next(1000));
            }
            for (int i = 0; i < 5; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = Island.FarR * (0.85f + (float)rnd.NextDouble() * 0.2f);
                mb.Blob(new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), new Vector3(0.45f, 0.3f, 0.4f), 1, 70 + i, 0.2f, IslandArt.StoneD, IslandArt.Stone, 0f, 0f);
            }
            IslandArt.Bake(mb, farRoot, "Suelo", true);
            // niebla: nubes gordas encima hasta descubrirla
            farFog = new GameObject("Niebla").transform;
            farFog.SetParent(farRoot, false);
            for (int i = 0; i < 9; i++)
            {
                Material[] cm;
                var cmesh = IslandArt.Cloud(900 + i, out cm);
                var c = IslandArt.MakeRenderer(farFog, "Nube", cmesh, cm, false).transform;
                float a = i / 9f * Mathf.PI * 2f, d = i == 0 ? 0f : Island.FarR * (0.35f + (i % 3) * 0.22f);
                c.localPosition = new Vector3(Mathf.Cos(a) * d, 1.2f + (i % 3) * 0.7f, Mathf.Sin(a) * d);
                c.localScale = Vector3.one * (2.6f + (i % 4) * 0.5f);
                c.localRotation = Quaternion.Euler(0f, i * 40f, 0f);
                fogClouds.Add(c);
            }
            fogGone = Isl.FarFound ? 2f : -1f;
            farFog.gameObject.SetActive(!Isl.FarFound);
            Isl.FarDiscovered += OnFarDiscovered;
            Isl.RaftChanged += OnRaftChanged;
            Isl.FarRockBroken += OnFarRockBroken;
        }

        static void Palm(MeshBuilder mb, Vector3 p, float h, int seed)
        {
            Color trunk = IslandArt.H("a8703f"), leaf = IslandArt.H("4fae3e"), leafL = IslandArt.H("7bd04f");
            Vector3 lean = new Vector3(Mathf.Sin(seed) * 0.35f, 0f, Mathf.Cos(seed) * 0.35f);
            mb.Cyl(p, p + Vector3.up * h + lean, 0.12f, 0.08f, 6, trunk, 0f);
            Vector3 top = p + Vector3.up * h + lean;
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + seed;
                Vector3 dir = new Vector3(Mathf.Cos(a), -0.35f, Mathf.Sin(a));
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * 0.18f;
                mb.Tri(top, top + dir * 1.1f + side, top + dir * 1.1f - side, Vector3.up, i % 2 == 0 ? leaf : leafL, 0.05f);
                mb.Tri(top, top + dir * 1.1f - side, top + dir * 1.1f + side, Vector3.down, leaf, 0f);
            }
            mb.Octa(top + Vector3.down * 0.1f, new Vector3(0.14f, 0.14f, 0.14f), IslandArt.H("7a4a2a"), 0f);
        }

        // ------------------------------------------------------------ balsa
        void BuildRaft(int seats)
        {
            if (raftT != null) Destroy(raftT.gameObject);
            raftBuiltSeats = seats;
            if (seats <= 0) { raftT = null; return; }
            // nivel 1: balsa de 3 troncos para uno; cada 2 asientos, mas larga; vela desde 4; baranda desde 7
            int rows = Mathf.Max(1, (seats + 1) / 2);
            float len = 1.1f + rows * 0.62f, wid = seats <= 1 ? 0.75f : 1.15f;
            var mb = new MeshBuilder();
            Color log = IslandArt.H("a8703f"), logD = IslandArt.H("7a4a2a"), rope = IslandArt.H("e8d7a8");
            int logs = seats <= 1 ? 3 : 5;
            for (int i = 0; i < logs; i++)
            {
                float x = (i - (logs - 1) * 0.5f) * (wid / logs);
                mb.Cyl(new Vector3(x, 0f, -len * 0.5f), new Vector3(x, 0f, len * 0.5f), wid / logs * 0.55f, wid / logs * 0.55f, 7, i % 2 == 0 ? log : logD, 0f);
            }
            mb.Box(new Vector3(0, 0.05f, len * 0.36f), new Vector3(wid + 0.1f, 0.06f, 0.08f), rope, 0f);
            mb.Box(new Vector3(0, 0.05f, -len * 0.36f), new Vector3(wid + 0.1f, 0.06f, 0.08f), rope, 0f);
            if (seats >= 4)
            {
                mb.Cyl(new Vector3(0, 0f, len * 0.3f), new Vector3(0, 2.2f, len * 0.3f), 0.05f, 0.04f, 6, logD, 0f);
                mb.Tri(new Vector3(0, 2.1f, len * 0.3f + 0.02f), new Vector3(-0.7f, 0.55f, len * 0.3f + 0.02f), new Vector3(0.7f, 0.55f, len * 0.3f + 0.02f), Vector3.back, IslandArt.H("fffaf0"), 0.1f);
                mb.Tri(new Vector3(0, 2.1f, len * 0.3f - 0.02f), new Vector3(0.7f, 0.55f, len * 0.3f - 0.02f), new Vector3(-0.7f, 0.55f, len * 0.3f - 0.02f), Vector3.forward, IslandArt.H("fffaf0"), 0.1f);
                mb.Box(new Vector3(0, 2.2f, len * 0.3f + 0.12f), new Vector3(0.04f, 0.18f, 0.28f), IslandArt.H("e0594a"), 0.2f);   // banderin
            }
            if (seats >= 7)
                for (int s = -1; s <= 1; s += 2)
                    mb.Box(new Vector3(s * (wid * 0.5f + 0.02f), 0.28f, 0f), new Vector3(0.05f, 0.08f, len * 0.9f), logD, 0f);
            Material[] mats;
            var mesh = mb.ToMesh(null, "Balsa", out mats);
            mats = VertexColorMerge.Apply(mesh, mats);
            raftT = IslandArt.MakeRenderer(root, "Balsa", mesh, mats).transform;
        }

        void UpdateFar(float dt)
        {
            if (farRoot == null) return;
            // niebla: respira; al descubrir, las nubes se abren hacia afuera y se desvanecen
            if (fogGone < 0f)
            {
                for (int i = 0; i < fogClouds.Count; i++)
                    fogClouds[i].localPosition += new Vector3(Mathf.Sin(Time.time * 0.3f + i) * 0.002f, 0f, Mathf.Cos(Time.time * 0.25f + i) * 0.002f);
            }
            else if (fogGone <= 1f)
            {
                fogGone += dt / 2.2f;
                float u = Mathf.Clamp01(fogGone);
                for (int i = 0; i < fogClouds.Count; i++)
                {
                    var c = fogClouds[i];
                    Vector3 o = c.localPosition; o.y = 0f;
                    if (o.sqrMagnitude < 0.01f) o = Vector3.right;
                    c.localPosition += o.normalized * dt * 6f + Vector3.up * dt * 1.5f;
                    c.localScale = Vector3.one * (2.6f + (i % 4) * 0.5f) * (1f - u * u);
                }
                if (fogGone > 1f) farFog.gameObject.SetActive(false);
            }
            // balsa: se arma al nivel del Muelle y sigue a la del juego, meciendose
            int seats = Isl.RaftSeats;
            if (seats != raftBuiltSeats) BuildRaft(seats);
            if (raftT != null)
            {
                raftT.gameObject.SetActive(Isl.FarFound);
                float bob = Mathf.Sin(Time.time * 1.6f) * 0.05f;
                Vector3 to = new Vector3(Isl.RaftX, -0.32f + bob, Isl.RaftZ);
                raftT.localPosition = Vector3.Lerp(raftT.localPosition, to, raftT.localPosition.sqrMagnitude < 1f ? 1f : 1f - Mathf.Exp(-dt * 10f));
                raftT.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f) * 2.5f, Isl.RaftFace * Mathf.Rad2Deg, Mathf.Cos(Time.time * 1.1f) * 3f);
                // estela al navegar
                if (Isl.Raft == RaftState.ToFar || Isl.Raft == RaftState.Returning)
                {
                    wakeT -= dt;
                    if (wakeT <= 0f) { wakeT = 0.25f; FxApi.Play("tinydust", raftT.position + Vector3.up * 0.1f - raftT.forward * 0.8f, new Color(0.9f, 0.97f, 1f), 0.8f); }
                }
            }
        }

        float wakeT;

        void OnFarDiscovered()
        {
            fogGone = 0f;
            Sfx.Play("magic_rise", -4f);
            Sfx.PlayLater("milestone", 0.6f, -4f);
            FxApi.Play("rays", FarCenter + Vector3.up * 1.5f, new Color(1f, 0.92f, 0.6f), 3f);
            FxApi.Play("unlock_burst", FarCenter + Vector3.up * 1f, new Color(1f, 0.92f, 0.6f), 3f);
            Juice.Vibrate(60);
            Ui.Toast(Loc.T("¡Isla descubierta! La balsa sale a las 6:00"), new Color(0.55f, 0.9f, 1f), Mineros.UI.Icons.Get("star"), true);
            Save();
        }

        void OnRaftChanged(RaftState s)
        {
            if (s == RaftState.Boarding)
            {
                Sfx.Play("bell", -6f, 1.1f);
                if (Isl.Stat("raft_trips") < 3) Ui.Toast(Loc.T("¡La balsa zarpa a las 6:00!"), new Color(0.55f, 0.9f, 1f), Mineros.UI.Icons.Get("star"));
            }
            else if (s == RaftState.ToFar || s == RaftState.Returning) Sfx.Play("whoosh", -10f, 0.8f);
            else if (s == RaftState.AtFar) Sfx.Play("thud", -10f, 1.2f);
        }

        void OnFarRockBroken(Ore o, double coins, int gems)
        {
            Vector3 at = new Vector3(o.X, 1.5f, o.Z);
            FxApi.Play("unlock_burst", at, IslandArt.OreCol[o.Kind], 3f);
            FxApi.Play("confetti", at + Vector3.up, default(Color), 2.5f);
            Sfx.Play("milestone", -2f);
            Sfx.PlayLater("coins_pour", 0.3f, -4f);
            Juice.Vibrate(80);
            Ui.Popup(at + Vector3.up * 2f, "+" + BigNum.Fmt(coins), new Color(1f, 0.85f, 0.3f), 44);
            Ui.CoinsFrom(at, coins);
            if (gems > 0) Ui.FlyGemsFrom(Cam.WorldToScreenPoint(at), Mathf.Min(gems, 8));
        }

        /// <summary>Tocar la isla lejana tapada: sin Muelle avisa; con Muelle la descubre.</summary>
        bool TapFarFog(Vector2 screen)
        {
            if (Isl.FarFound || farRoot == null) return false;
            Vector3 g = ScreenToGround(screen);
            Vector2 cs = Cam.WorldToScreenPoint(FarCenter + Vector3.up * 2f);
            float px = Cam.pixelHeight / 1544f;
            bool hit = (new Vector2(g.x - Island.FarX, g.z - Island.FarZ)).magnitude < Island.FarR + 2.5f || Vector2.Distance(screen, cs) < 160f * px;
            if (!hit) return false;
            if (Isl.TryDiscoverFar()) return true;
            // sin Muelle: el globito se sacude y dice que hace falta (icono del Muelle)
            Ui.Toast(Loc.T("Necesitás un Muelle para llegar"), Kit3.Yellow, IslandStage.I.BuildingIcon(BKind.Dock, 1), true);
            Sfx.Play("error", -8f);
            Juice.Punch(farFog, 0.08f, 0.3f);
            return true;
        }

        /// <summary>Escala de dibujo de una veta (gigante, legendaria o roca de la isla lejana).</summary>
        public static float OreScale(Ore o)
        {
            if (o.Far) return 2.3f;
            if (o.Giant) return o.Legendary ? 3.5f : 2.8f;
            if (o.Fancy >= 0) return Island.FxCatalog[o.Fancy].Effect == (int)Island.FxKind.MiniGiant ? 1.8f : 1.25f;   // roca de carta
            return 1f;
        }
    }
}
