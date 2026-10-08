using System.Collections;
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
    /// Progreso grande en el mundo (biblia 3.8): la Maravilla (ruinas con enredaderas desde el dia 1; cada fase baja una
    /// pieza que se asienta con polvo; la estatua se ve construida de abajo hacia arriba y al final se baña en oro con
    /// fuegos artificiales y una onda de flores), la decoracion en lugares fijos, el Golem de Roca (sombra que crece,
    /// cae, se enoja en cada fase y se desarma en monedas) y el viaje a la isla nueva.
    /// </summary>
    public sealed partial class IslandGame
    {
        Transform wonder, wonderRuins, wonderScaffold;
        MeshRenderer wonderStatue;
        Vector3 wonderPos;
        float wonderShine;
        static Mesh ruinsMesh, wonderScaffoldMesh;
        static Material[] ruinsMats, wonderScaffoldMats;
        Material statueMat;
        float statueH = 3f;

        void InitProgress()
        {
            float x, z;
            Isl.WonderSpot(out x, out z);
            wonderPos = new Vector3(x, 0f, z);
            Isl.AddBlocker(x, z, 1.9f);
            BuildWonderView();
            BuildDecorViews();
            Isl.WonderBuilt += ph => StartCoroutine(WonderShow(ph));
            Isl.DecorPlaced += OnDecorPlaced;
            Isl.BossCame += OnBossCame;
            Isl.BossPhase += OnBossPhase;
            Isl.BossDefeated += OnBossDefeated;
            Isl.SeasonLevelUp += lv => { Ui.Toast(Loc.T("¡Nivel ") + lv + Loc.T(" del pase de temporada!"), Kit3.Yellow); Sfx.Play("powerup", -6f, 1.2f); Ui.PassPop(); };
            Isl.PieceFound += (st, pc) =>
            {
                Ui.Toast(Loc.T("¡Pieza del museo: ") + Island.PieceName[st, pc] + " (" + Island.SetName[st] + ")!", new Color(0.55f, 0.45f, 0.8f));
                Sfx.Play("gleam", -4f);
                Sfx.PlayLater("jingle_small", 0.3f, -6f);
                if (Isl.SetDone(st)) Ui.Banner(Loc.T("¡Colección completa!"), Island.SetName[st] + ": " + Island.SetBonus[st], new Color(0.55f, 0.45f, 0.8f), null, null);
            };
            Isl.CheckSeason(Today);
        }

        // ------------------------------------------------------------ maravilla
        void BuildWonderView()
        {
            if (wonder != null) Destroy(wonder.gameObject);
            wonder = new GameObject("Maravilla").transform;
            wonder.SetParent(root, false);
            wonder.localPosition = wonderPos;
            if (ruinsMesh == null) MakeRuinsMeshes();
            wonderRuins = IslandArt.MakeRenderer(wonder, "Ruinas", ruinsMesh, ruinsMats).transform;
            wonderScaffold = IslandArt.MakeRenderer(wonder, "Andamio", wonderScaffoldMesh, wonderScaffoldMats).transform;
            Material tm;
            var mesh = IslandArt.TripoModel("wonder", out tm);
            if (mesh != null)
            {
                statueMat = new Material(tm) { name = "Estatua" };
                wonderStatue = IslandArt.MakeRenderer(wonder, "Estatua", mesh, new[] { statueMat });
                wonderStatue.transform.localRotation = Quaternion.Euler(0f, BuildingYaw, 0f);
                statueH = mesh.bounds.max.y;
            }
            IslandArt.Blob(wonder, 2.4f, 0.4f);
            ApplyWonderPhase(Isl.WonderPhase, 1f);
        }

        /// <summary>Estado visual de la fase (k = 0..1 dentro de la animacion de subida del corte).</summary>
        void ApplyWonderPhase(int ph, float k)
        {
            wonderRuins.gameObject.SetActive(ph == 0);
            wonderScaffold.gameObject.SetActive(ph >= 1 && ph < Island.WonderPhases);
            if (wonderStatue == null) return;
            wonderStatue.gameObject.SetActive(ph >= 1);
            // de abajo hacia arriba: pedestal, piernas, cuerpo, cabeza y pico; piedra gris hasta el baño de oro
            float[] h = { 0f, 0.36f, 0.56f, 0.8f, 1.02f, 1.02f };
            float prev = ph > 0 ? h[ph - 1] : 0f;
            float clip = Mathf.Lerp(prev, h[ph], k) * statueH;
            statueMat.SetFloat("_ClipY", ph >= Island.WonderPhases ? 1000f : wonder.position.y + clip);
            bool gold = ph >= Island.WonderPhases;
            statueMat.SetColor("_Color", gold ? Color.white : new Color(0.62f, 0.6f, 0.58f));
            statueMat.SetColor("_EmissionColor", gold ? new Color(0.18f, 0.13f, 0.02f) : Color.black);
            wonderScaffold.localScale = new Vector3(1f, Mathf.Max(0.3f, h[Mathf.Min(ph, 4)] + 0.1f), 1f);
        }

        void MakeRuinsMeshes()
        {
            var mb = new MeshBuilder();
            Color st = IslandArt.Stone, sd = IslandArt.StoneD, vine = IslandArt.H("4f9a3a"), vineL = IslandArt.H("6fbf4a");
            // base rota y columnas caidas, con enredaderas
            mb.Cyl(new Vector3(0, 0, 0), new Vector3(0, 0.3f, 0), 1.6f, 1.5f, 14, sd, 0f);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.3f;
                var c = new Vector3(Mathf.Cos(a) * 1.1f, 0.3f, Mathf.Sin(a) * 1.1f);
                float hh = i % 2 == 0 ? 1.4f : 0.6f;
                mb.Cyl(c, c + Vector3.up * hh, 0.22f, 0.2f, 8, st, 0f);
                mb.Blob(c + Vector3.up * hh * 0.5f + Vector3.right * 0.15f, new Vector3(0.18f, hh * 0.45f, 0.18f), 0, 3 + i, 0.2f, vine, vineL, 0f, -1f);
            }
            mb.Box(new Vector3(-0.4f, 0.45f, 0.5f), new Vector3(1.1f, 0.3f, 0.35f), st, 0f);   // columna caida
            mb.Blob(new Vector3(0.3f, 0.5f, -0.3f), new Vector3(0.6f, 0.25f, 0.5f), 1, 9, 0.3f, vine, vineL, 0f, -1f);
            ruinsMesh = mb.ToMesh(null, "Ruinas", out ruinsMats);
            ruinsMats = VertexColorMerge.Apply(ruinsMesh, ruinsMats);
            var sb = new MeshBuilder();
            Color w = IslandArt.Wood, wd = IslandArt.WoodD;
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f + 0.78f;
                var c = new Vector3(Mathf.Cos(a) * 1.35f, 0f, Mathf.Sin(a) * 1.35f);
                sb.Box(c + Vector3.up * 1.6f, new Vector3(0.1f, 3.2f, 0.1f), wd, 0f);
            }
            for (int y = 1; y <= 3; y++)
                for (int i = 0; i < 4; i++)
                {
                    float a0 = i / 4f * Mathf.PI * 2f + 0.78f, a1 = (i + 1) / 4f * Mathf.PI * 2f + 0.78f;
                    var p0 = new Vector3(Mathf.Cos(a0) * 1.35f, y, Mathf.Sin(a0) * 1.35f);
                    var p1 = new Vector3(Mathf.Cos(a1) * 1.35f, y, Mathf.Sin(a1) * 1.35f);
                    var mid = (p0 + p1) * 0.5f;
                    float len = (p1 - p0).magnitude;
                    sb.Box(mid, new Vector3(len, 0.08f, 0.1f), w, 0f);
                }
            wonderScaffoldMesh = sb.ToMesh(null, "AndamioMaravilla", out wonderScaffoldMats);
            wonderScaffoldMats = VertexColorMerge.Apply(wonderScaffoldMesh, wonderScaffoldMats);
        }

        IEnumerator WonderShow(int ph)
        {
            FocusOn(wonderPos);
            Vector3 top = wonderPos + Vector3.up * (statueH + 4f);
            if (ph == 1)
            {
                // se sacan las enredaderas: hojas y polvo
                FxApi.Play("dust", wonderPos, new Color(0.6f, 0.75f, 0.45f), 3f);
                FxApi.Play("confetti", wonderPos + Vector3.up * 1.5f, new Color(0.45f, 0.75f, 0.3f), 1.6f);
                Sfx.Play("whoosh", -4f, 0.8f);
            }
            // una pieza baja del cielo (como de una grua) y se asienta
            var piece = IslandArt.MakeRenderer(root, "Pieza", ruinsMesh, ruinsMats).transform;
            piece.localScale = Vector3.one * 0.35f;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                float k = t / 0.8f;
                piece.position = Vector3.Lerp(top, wonderPos + Vector3.up * statueH * 0.4f, k * k);
                piece.rotation = Quaternion.Euler(0f, t * 200f, 0f);
                yield return null;
            }
            Destroy(piece.gameObject);
            Sfx.Play("thud", -2f, 0.8f);
            Sfx.Play("build", -4f);
            FxApi.Play("dust", wonderPos, new Color(0.85f, 0.78f, 0.62f), 3.4f);
            Juice.Vibrate(40);
            // la obra sube hasta la altura de la fase
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                ApplyWonderPhase(ph, Mathf.SmoothStep(0f, 1f, t / 1.2f));
                if (Random.value < Time.deltaTime * 12f) FxApi.Play("hit_spark", wonderPos + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.4f, statueH), Random.Range(-1.2f, 1.2f)), default(Color), 0.8f);
                yield return null;
            }
            ApplyWonderPhase(ph, 1f);
            if (ph < Island.WonderPhases)
            {
                Sfx.Play("milestone", -3f);
                Ui.Banner(Loc.T("Maravilla ") + ph + "/" + Island.WonderPhases, Island.WonderPhaseName[ph - 1] + Loc.T(" listo. +8 % a todo"), Kit3.Yellow, Ui.WonderIcon(), null);
                Cheer(wonderPos, 9f);
                yield break;
            }
            // final epico: destello, rayos, fuegos artificiales, onda de flores y todos festejan
            Sfx.Duck(10f, 6f);
            Ui.Flash(new Color(1f, 0.95f, 0.75f), 0.6f);
            Sfx.Play("fanfare", -1f);
            Juice.ZoomPunch(0.04f, 0.35f);
            var rays = IslandStage.MakeRays(wonder, Vector3.up * statueH * 0.6f, statueH * 3f, new Color(1f, 0.88f, 0.45f, 0.8f));
            for (int i = 0; i < 5; i++)
            {
                Sfx.PlayLater("firework", 0.3f + i * 0.45f, -6f, 0.9f + i * 0.05f);
                FxApi.Play("confetti", wonderPos + Vector3.up * (statueH + 2f) + Random.insideUnitSphere * 2f, default(Color), 2.6f);
            }
            Sfx.PlayLater("crackle", 1.2f, -8f);
            Sfx.PlayLater("cheer", 0.6f, -4f);
            foreach (var mv in miners.Values) mv.Celebrate = 1f;
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * Mathf.PI * 2f, d = 2.5f + (i % 3) * 1.4f;
                Vector3 p = wonderPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
                if (Isl.FreeSpot(p.x, p.z, 0.3f)) FlowerAt(p, 0.2f + d * 0.25f, 200f);
            }
            Ui.Banner(Loc.T("¡MARAVILLA TERMINADA!"), Loc.T("La Gran Estatua de Oro: +50 % a todo. ¡Ya podés zarpar a una isla nueva!"), Kit3.Yellow, Ui.WonderIcon(), null);
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                rays.rotation = Quaternion.LookRotation(Cam.transform.forward, Cam.transform.up) * Quaternion.Euler(0f, 0f, t * 30f);
                rays.localScale = Vector3.one * statueH * 3f * (1f - Mathf.Clamp01((t - 3f)));
                yield return null;
            }
            Destroy(rays.gameObject);
        }

        void UpdateWonder(float dt)
        {
            if (wonder == null || Isl.WonderPhase < Island.WonderPhases) return;
            // brillo que recorre la estatua cada pocos segundos
            wonderShine -= dt;
            if (wonderShine <= 0f)
            {
                wonderShine = 5f;
                FxApi.Play("sparkle", wonderPos + Vector3.up * statueH * 0.7f, new Color(1f, 0.9f, 0.5f), 1.4f);
            }
        }

        public Vector3 WonderWorld { get { return wonderPos; } }

        /// <summary>Las ruinas de la Maravilla aparecen con el Ayuntamiento 5 (al principio la isla esta despejada).</summary>
        public bool WonderShown { get { return Isl.WonderPhase > 0 || Isl.Th >= 5; } }
        bool wonderWasShown = true;

        void UpdateWonderVisible()
        {
            if (wonder == null) return;
            bool show = WonderShown;
            if (show == wonderWasShown && wonder.gameObject.activeSelf == show) return;
            bool appear = show && !wonderWasShown && Time.timeSinceLevelLoad > 1f;
            wonderWasShown = show;
            wonder.gameObject.SetActive(show);
            if (appear)
            {
                StartCoroutine(FogRevealAt(wonderPos));
                Ui.Toast(Loc.T("¡Aparecieron unas ruinas misteriosas!"), Kit3.Yellow);
            }
        }
        public float WonderHeight { get { return statueH; } }

        bool TapWonder(Vector2 screen)
        {
            if (wonder == null) return false;
            Vector2 sp = Cam.WorldToScreenPoint(wonderPos + Vector3.up * 1.2f);
            if (Vector2.Distance(screen, sp) > 130f * (Cam.pixelHeight / 1544f)) return false;
            Juice.Punch(wonder, 0.06f, 0.25f);
            Sfx.Play("ui", -6f);
            Ui.OpenWonder();
            return true;
        }

        // ------------------------------------------------------------ decoracion
        readonly Dictionary<int, Transform> decorViews = new Dictionary<int, Transform>();
        static readonly Mesh[] decorMesh = new Mesh[7];
        static readonly Material[][] decorMats = new Material[7][];

        void BuildDecorViews()
        {
            foreach (var t in decorViews.Values) if (t != null) Destroy(t.gameObject);
            decorViews.Clear();
            foreach (var kv in Isl.Decor) MakeDecor(kv.Key, kv.Value, false);
        }

        void OnDecorPlaced(int slot)
        {
            int item = Isl.Decor[slot];
            var t = MakeDecor(slot, item, true);
            if (t == null) return;
            Sfx.Play("pop", -4f);
            Sfx.Play("gleam", -10f, 1.2f);
            FxApi.Play("sparkle", t.position + Vector3.up * 0.5f, new Color(1f, 0.95f, 0.7f), 1f);
            FxApi.Play("dust", t.position, new Color(0.85f, 0.8f, 0.65f), 1f);
            Ui.Popup(t.position + Vector3.up * 1.6f, Loc.T("Belleza +") + Island.DecorDefs[item].Beauty, new Color(1f, 0.6f, 0.85f), 28);
        }

        Transform MakeDecor(int slot, int item, bool animate)
        {
            if (slot >= Isl.DecorSlots.Count) return null;
            var s = Isl.DecorSlots[slot];
            Transform t;
            if (item == 6 && wonderStatue != null)
            {
                // estatuita: la misma estatua de la maravilla, chica y de piedra
                var holder = new GameObject("Estatuita").transform;
                holder.SetParent(root, false);
                var m = new Material(statueMat) { name = "Estatuita" };
                m.SetFloat("_ClipY", 1000f);
                m.SetColor("_Color", new Color(0.7f, 0.68f, 0.64f));
                var r = IslandArt.MakeRenderer(holder, "M", wonderStatue.GetComponent<MeshFilter>().sharedMesh, new[] { m });
                r.transform.localScale = Vector3.one * 0.33f;
                t = holder;
            }
            else
            {
                if (decorMesh[item] == null) MakeDecorMesh(item);
                t = IslandArt.MakeRenderer(root, "Deco" + item, decorMesh[item], decorMats[item]).transform;
            }
            t.position = new Vector3(s[0], 0f, s[1]);
            t.rotation = Quaternion.Euler(0f, s[2] * Mathf.Rad2Deg, 0f);
            IslandArt.Blob(t, 0.7f, 0.3f);
            decorViews[slot] = t;
            if (animate) StartCoroutine(PopIn(t));
            if (item == 5) FxApi.Attach("aura", t, new Color(0.6f, 0.85f, 1f), 0.7f);
            return t;
        }

        IEnumerator PopIn(Transform t)
        {
            for (float u = 0f; u < 0.45f; u += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = u / 0.45f;
                float s = OutBack(Mathf.Min(1f, k * 1.2f));
                float sq = 1f + Mathf.Sin(k * Mathf.PI * 2f) * 0.15f * (1f - k);
                t.localScale = new Vector3(s / Mathf.Sqrt(sq), s * sq, s / Mathf.Sqrt(sq));
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        static void MakeDecorMesh(int item)
        {
            var mb = new MeshBuilder();
            Color wood = IslandArt.Wood, woodD = IslandArt.WoodD, stone = IslandArt.Stone, stoneD = IslandArt.StoneD;
            switch (item)
            {
                case 0:   // cantero con flores
                    mb.Box(new Vector3(0, 0.1f, 0), new Vector3(1.1f, 0.2f, 0.55f), woodD, 0f);
                    mb.Box(new Vector3(0, 0.17f, 0), new Vector3(0.98f, 0.1f, 0.44f), IslandArt.H("6b4a2e"), 0f);
                    for (int i = 0; i < 6; i++)
                    {
                        var p = new Vector3(-0.38f + i * 0.15f, 0.3f + (i % 2) * 0.06f, (i % 2 - 0.5f) * 0.18f);
                        Color[] fc = { IslandArt.H("ff6fa8"), IslandArt.H("ffd23a"), IslandArt.H("ffffff"), IslandArt.H("b06ef0") };
                        mb.Box(p - Vector3.up * 0.09f, new Vector3(0.03f, 0.16f, 0.03f), IslandArt.GrassD, 0f);
                        mb.Octa(p, Vector3.one * 0.12f, fc[i % 4], 0.15f);
                    }
                    break;
                case 1:   // cerca
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.6f + i * 0.4f, 0.32f, 0), new Vector3(0.09f, 0.64f, 0.09f), woodD, 0f);
                    mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.32f, 0.07f, 0.05f), wood, 0f);
                    mb.Box(new Vector3(0, 0.22f, 0), new Vector3(1.32f, 0.07f, 0.05f), wood, 0f);
                    break;
                case 2:   // banco
                    mb.Box(new Vector3(0, 0.3f, 0), new Vector3(1.1f, 0.07f, 0.38f), wood, 0f);
                    mb.Box(new Vector3(0, 0.55f, 0.17f), new Vector3(1.1f, 0.25f, 0.06f), wood, 0f);
                    for (int s = -1; s <= 1; s += 2) mb.Box(new Vector3(s * 0.45f, 0.15f, 0), new Vector3(0.08f, 0.3f, 0.34f), IslandArt.H("3b3446"), 0f);
                    break;
                case 3:   // farol doble
                    mb.Box(new Vector3(0, 0.75f, 0), new Vector3(0.1f, 1.5f, 0.1f), IslandArt.H("3b3446"), 0f);
                    mb.Box(new Vector3(0, 1.45f, 0), new Vector3(0.9f, 0.06f, 0.08f), IslandArt.H("3b3446"), 0f);
                    for (int s = -1; s <= 1; s += 2) mb.Box(new Vector3(s * 0.4f, 1.3f, 0), new Vector3(0.18f, 0.22f, 0.18f), IslandArt.H("fff1c4"), 0.6f);
                    break;
                case 4:   // arbusto podado en maceta
                    mb.Cyl(Vector3.zero, new Vector3(0, 0.32f, 0), 0.28f, 0.34f, 10, IslandArt.H("c9734a"), 0f);
                    mb.Blob(new Vector3(0, 0.78f, 0), new Vector3(0.42f, 0.48f, 0.42f), 1, 7, 0.05f, IslandArt.GrassD, IslandArt.Grass, 0f, -1f);
                    break;
                default:  // fuente
                    mb.Cyl(Vector3.zero, new Vector3(0, 0.35f, 0), 0.85f, 0.9f, 16, stone, 0f);
                    mb.Cyl(new Vector3(0, 0.3f, 0), new Vector3(0, 0.36f, 0), 0.72f, 0.72f, 16, IslandArt.H("7fd6f0"), 0.25f);
                    mb.Cyl(new Vector3(0, 0.35f, 0), new Vector3(0, 0.95f, 0), 0.13f, 0.11f, 10, stoneD, 0f);
                    mb.Cyl(new Vector3(0, 0.95f, 0), new Vector3(0, 1.05f, 0), 0.38f, 0.35f, 12, stone, 0f);
                    break;
            }
            Material[] mats;
            decorMesh[item] = mb.ToMesh(null, "Deco" + item, out mats);
            decorMats[item] = VertexColorMerge.Apply(decorMesh[item], mats);
        }

        public bool DecorMode;

        /// <summary>Toque sobre un lugar de decoracion (solo en modo decorar).</summary>
        bool TapDecorSlot(Vector2 screen)
        {
            if (!DecorMode) return false;
            float px = Cam.pixelHeight / 1544f;
            int best = -1; float bd = 80f * px;
            for (int i = 0; i < Isl.DecorSlots.Count; i++)
            {
                if (Isl.Decor.ContainsKey(i)) continue;
                var s = Isl.DecorSlots[i];
                Vector2 sp = Cam.WorldToScreenPoint(new Vector3(s[0], 0.2f, s[1]));
                float d = Vector2.Distance(screen, sp);
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) return false;
            Ui.OpenDecorPicker(best);
            return true;
        }

        public Vector3 SlotWorld(int i) { var s = Isl.DecorSlots[i]; return new Vector3(s[0], 0f, s[1]); }

        // ------------------------------------------------------------ golem de roca
        static Mesh golemMesh;
        static Material[] golemMats;

        Transform GolemVis(Transform parent)
        {
            if (golemMesh == null)
            {
                var mb = new MeshBuilder();
                Color r = IslandArt.H("8d8f98"), rl = IslandArt.H("b8bcc6"), moss = IslandArt.H("6fae4a"), eye = IslandArt.H("ffb21e");
                mb.Blob(new Vector3(0, 0.55f, 0), new Vector3(0.62f, 0.55f, 0.5f), 1, 11, 0.12f, r, rl, 0f, -1f);           // cuerpo
                mb.Blob(new Vector3(0, 1.25f, 0.05f), new Vector3(0.38f, 0.32f, 0.34f), 1, 13, 0.1f, r, rl, 0f, -1f);       // cabeza
                for (int s = -1; s <= 1; s += 2)
                {
                    mb.Blob(new Vector3(s * 0.72f, 0.7f, 0f), new Vector3(0.22f, 0.4f, 0.22f), 1, 17 + s, 0.12f, r, rl, 0f, -1f);   // brazos
                    mb.Blob(new Vector3(s * 0.78f, 0.28f, 0.04f), new Vector3(0.24f, 0.2f, 0.24f), 1, 19 + s, 0.1f, r, rl, 0f, -1f); // puños
                    mb.Octa(new Vector3(s * 0.13f, 1.3f, 0.36f), new Vector3(0.09f, 0.07f, 0.04f), eye, 0.9f);                     // ojos que brillan
                    mb.Blob(new Vector3(s * 0.25f, 0.1f, 0f), new Vector3(0.2f, 0.12f, 0.22f), 0, 23 + s, 0.05f, r, rl, 0f, -1f);  // pies
                }
                mb.Blob(new Vector3(0.1f, 1.52f, 0f), new Vector3(0.25f, 0.08f, 0.22f), 0, 29, 0.2f, moss, IslandArt.H("8cc247"), 0f, -1f);   // musgo
                for (int i = 0; i < 4; i++) mb.Crystal(new Vector3(-0.3f + i * 0.2f, 0.9f, -0.35f), new Vector3(0, 0.6f, -1f).normalized, 0.3f, 0.08f, 5, IslandArt.OreCol[3], 0.5f);
                golemMesh = mb.ToMesh(null, "Golem", out golemMats);
                golemMats = VertexColorMerge.Apply(golemMesh, golemMats);
            }
            var rr = IslandArt.MakeRenderer(parent, "Vis", golemMesh, golemMats);
            return rr.transform;
        }

        Transform bossShadow;

        void OnBossCame(Ore o)
        {
            OreView v;
            if (ores.TryGetValue(o.Id, out v))
            {
                // el golem reemplaza a la veta: aparece con su sombra creciendo y cae
                if (v.Vis != null) Destroy(v.Vis.gameObject);
                v.Vis = GolemVis(v.T);
                v.Rend = v.Vis.GetComponent<MeshRenderer>();
                v.T.localScale = Vector3.one * 1.9f;
                StartCoroutine(BossDrop(v));
            }
            Ui.Toast(Loc.T("¡EL GOLEM DE ROCA! Todos a pelear"), new Color(0.55f, 0.5f, 0.5f));
            Sfx.Duck(8f, 3f);
            Sfx.Play("rumble", 0f, 0.7f);
            Sfx.PlayLater("thunder", 0.3f, -6f, 0.7f);
            FocusOn(new Vector3(o.X, 0f, o.Z), true);
            Ui.ShowBossBar(o);
        }

        IEnumerator BossDrop(OreView v)
        {
            var t = v.Vis;
            if (bossShadow == null) bossShadow = IslandArt.Blob(root, 1f, 0.6f);
            Vector3 at = new Vector3(v.O.X, 0f, v.O.Z);
            bossShadow.position = at + Vector3.up * 0.04f;
            for (float u = 0f; u < 1.2f; u += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = u / 1.2f;
                bossShadow.localScale = new Vector3(0.3f + k * 2.6f, 1f, 0.3f + k * 2.6f);
                t.localPosition = Vector3.up * Mathf.Lerp(12f, 0f, k * k);
                yield return null;
            }
            if (t != null) t.localPosition = Vector3.zero;
            bossShadow.localScale = Vector3.zero;
            FxApi.Play("meteor_impact", at, new Color(0.8f, 0.75f, 0.65f), 2.6f);
            FxApi.Play("dust", at, new Color(0.8f, 0.75f, 0.65f), 4f);
            FxApi.Play("ring", at + Vector3.up * 0.05f, new Color(1f, 0.9f, 0.6f), 5f);
            Sfx.Play("thud", 0f, 0.5f);
            Sfx.Play("break", -2f, 0.6f);
            Juice.Vibrate(120);
            foreach (var mv in miners.Values) mv.Celebrate = 0f;
        }

        void OnBossPhase(Ore o, int ph)
        {
            OreView v;
            if (ores.TryGetValue(o.Id, out v)) StartCoroutine(BossAngry(v));
            Ui.Toast(ph == 2 ? Loc.T("¡El golem se enoja!") : Loc.T("¡Furia del golem! Falta poco"), new Color(0.85f, 0.35f, 0.3f));
            Sfx.Play("rumble", -2f, 0.9f);
            Sfx.Play("thunder", -10f, 1.2f);
        }

        IEnumerator BossAngry(OreView v)
        {
            // se agacha (anticipacion) y brilla rojo
            for (float u = 0f; u < 0.9f; u += Time.deltaTime)
            {
                if (v.Vis == null) yield break;
                float k = u / 0.9f;
                float crouch = Mathf.Sin(Mathf.Min(1f, k * 1.5f) * Mathf.PI);
                v.Vis.localScale = new Vector3(1f + crouch * 0.12f, 1f - crouch * 0.18f, 1f + crouch * 0.12f);
                var mpb = new MaterialPropertyBlock();
                v.Rend.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.1f) * Mathf.Sin(k * Mathf.PI) * 0.8f);
                v.Rend.SetPropertyBlock(mpb);
                yield return null;
            }
            if (v.Vis != null) v.Vis.localScale = Vector3.one;
            FxApi.Play("ring", v.T.position + Vector3.up * 0.05f, new Color(1f, 0.4f, 0.3f), 4f);
        }

        void OnBossDefeated(Ore o)
        {
            Vector3 at = new Vector3(o.X, 0.5f, o.Z);
            Debris(at + Vector3.up, 0, 16, 2.6f);
            FxApi.Play("coin_burst", at + Vector3.up, default(Color), 3f);
            Juice.ZoomPunch(0.04f, 0.3f);   // el unico zoom permitido: jefe derrotado
            Sfx.Duck(10f, 4f);
            Sfx.Play("fanfare", -2f);
            Sfx.PlayLater("cheer", 0.4f, -4f);
            Ui.Banner(Loc.T("¡GOLEM DERROTADO!"), Loc.T("Nivel ") + Isl.BossLevel + Loc.T(": gemas, monedas y un cofre de oro"), Kit3.Yellow, null, null);
        }

        // ------------------------------------------------------------ viaje a una isla nueva
        /// <summary>Zarpa: guarda, cierra esta isla y arma la nueva desde el guardado (con su paleta).</summary>
        public void SailAway()
        {
            if (!Isl.Sail()) return;
            Save();
            StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            yield return null;
            // la isla vieja se destruye; un objeto aparte arma la nueva en el cuadro siguiente
            var host = new GameObject("Viaje").AddComponent<IslandRestarter>();
            host.File = saveFileName;
            I = null;
            Destroy(gameObject);
        }
    }
}

namespace Mineros.IslandView
{
    /// <summary>Arma la isla nueva despues de que la vieja se destruyo (el viaje entre islas).</summary>
    public sealed class IslandRestarter : MonoBehaviour
    {
        public string File;
        System.Collections.IEnumerator Start()
        {
            yield return null;
            IslandGame.Build(File);
            Destroy(gameObject);
        }
    }
}
