using System.Collections;
using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Miners;
using Mineros.UI;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// La ciudad en el mundo (biblia de produccion B.1 y B.3): obras con cimientos, cartel, andamio y lona que se cubre
    /// por etapas; constructores visibles con casco amarillo que caminan hasta la obra y martillan (los libres esperan en
    /// la plaza); revelacion del edificio terminado (las tablas salen volando, la lona se va al cielo) y vida de los
    /// edificios de produccion.
    /// </summary>
    public sealed partial class IslandGame
    {
        static readonly Color BuilderHelmet = new Color(1f, 0.82f, 0.18f);
        static readonly Color TarpCol = new Color(0.95f, 0.93f, 0.86f);

        sealed class SiteView
        {
            public Transform Root, Sign, Tarp;
            public readonly List<Transform> Stones = new List<Transform>();
            public int Stage = -1;
        }

        sealed class BuilderView
        {
            public MinerModel Model;
            public Vector3 Pos;
            public int Plot = -1;
            public readonly List<Vector3> Path = new List<Vector3>();
            public float Phase, HitT, BlinkT, Celebrate;
            public Vector3 Dir = Vector3.back;
            public int Index;
            public bool Inside = true;   // esperan dentro del Ayuntamiento: no ensucian la isla ni parecen mineros
            public float Show;           // 0 = adentro (invisible), 1 = afuera
        }

        readonly Dictionary<int, SiteView> sites = new Dictionary<int, SiteView>();
        readonly List<BuilderView> builders = new List<BuilderView>();

        void InitCity()
        {
            Isl.WorkDone += p =>
            {
                Sfx.Play("bell", -8f, 1.3f);   // "ding" de obra terminada (10)
                foreach (var b in builders) if (b.Plot == p.Id) b.Celebrate = 1f;
                if (p.Building == (int)BKind.Depot) TownHallCheer();   // (11)
            };
            Isl.ProductReady += p => { };
        }

        // ------------------------------------------------------------ obra: cimientos, cartel, lona
        SiteView Site(PlotView v)
        {
            SiteView s;
            if (sites.TryGetValue(v.P.Id, out s) && s.Root != null) return s;
            s = new SiteView();
            s.Root = new GameObject("Obra").transform;
            s.Root.SetParent(v.Root, false);
            var mb = new MeshBuilder();
            mb.Disc(new Vector3(0, 0.025f, 0), 1.45f, 22, new Color(0.72f, 0.58f, 0.4f), 0f);   // tierra removida
            IslandArt.Bake(mb, s.Root, "Tierra", false);
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 2f * i / 10f;
                var smb = new MeshBuilder();
                smb.Box(Vector3.zero, new Vector3(0.34f, 0.22f, 0.3f), IslandArt.Stone, 0f);
                var st = IslandArt.Bake(smb, s.Root, "Piedra", true).transform;
                st.localPosition = new Vector3(Mathf.Cos(a) * 1.3f, 0.11f, Mathf.Sin(a) * 1.3f);
                st.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                s.Stones.Add(st);
            }
            // cartel de obra (del color de la familia)
            var sg = new MeshBuilder();
            sg.Box(new Vector3(0, 0.55f, 0), new Vector3(0.08f, 1.1f, 0.08f), IslandArt.WoodD, 0f);
            sg.Box(new Vector3(0, 1.05f, -0.03f), new Vector3(0.8f, 0.45f, 0.05f), IslandArt.Wall, 0f);
            sg.Box(new Vector3(0, 1.05f, -0.06f), new Vector3(0.62f, 0.1f, 0.02f), IslandArt.FamilyRoof((BKind)Mathf.Max(0, v.P.Building)), 0.1f);
            for (int i = 0; i < 4; i++) sg.Box(new Vector3(-0.27f + i * 0.18f, 1.24f, -0.06f), new Vector3(0.12f, 0.05f, 0.02f), i % 2 == 0 ? Kit3.Yellow : IslandArt.Dark, 0f);
            s.Sign = IslandArt.Bake(sg, s.Root, "Cartel", true).transform;
            Vector3 toCenter = -new Vector3(v.P.X, 0f, v.P.Z).normalized;
            if (toCenter.sqrMagnitude < 0.01f) toCenter = Vector3.back;
            s.Sign.localPosition = toCenter * 1.55f + Vector3.Cross(Vector3.up, toCenter) * 0.9f;
            s.Sign.localRotation = Quaternion.LookRotation(-toCenter);
            // lona: un volumen claro que va tapando el andamio (se ve la forma del edificio debajo)
            var tm = new MeshBuilder();
            tm.Box(new Vector3(0, 0.5f, 0), new Vector3(2.3f, 1f, 1.9f), TarpCol, 0f);
            tm.Box(new Vector3(0, 1.0f, 0), new Vector3(2.4f, 0.06f, 2.0f), new Color(0.3f, 0.55f, 0.85f), 0f);   // franja azul
            s.Tarp = IslandArt.Bake(tm, s.Root, "Lona", true).transform;
            s.Tarp.localScale = new Vector3(1f, 0.001f, 1f);
            s.Tarp.gameObject.SetActive(false);
            sites[v.P.Id] = s;
            return s;
        }

        void DropSite(PlotView v)
        {
            SiteView s;
            if (!sites.TryGetValue(v.P.Id, out s)) return;
            if (s.Root != null) Destroy(s.Root.gameObject);
            sites.Remove(v.P.Id);
        }

        /// <summary>B.1.1: la parcela se hunde, cae el cartel y las piedras de los cimientos saltan de a una.</summary>
        IEnumerator PlaceShow(PlotView v)
        {
            var s = Site(v);
            Vector3 c = v.Root.position;
            FocusOn(c);
            Sfx.Play("thud", -2f, 0.85f);
            FxApi.Play("dust", c + Vector3.up * 0.2f, new Color(0.85f, 0.75f, 0.6f), 2.4f);
            Juice.Vibrate(20);
            foreach (var st in s.Stones) st.gameObject.SetActive(false);
            // cartel: cae desde 3 m torcido y se endereza (OutElastic 400 ms)
            Vector3 sp = s.Sign.localPosition;
            Quaternion sr = s.Sign.localRotation;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float u = t / 0.5f;
                float fall = Mathf.Min(1f, u * 2.4f);
                s.Sign.localPosition = sp + Vector3.up * (1f - fall * fall) * 3f;
                float wob = fall >= 1f ? Mathf.Sin((u - 0.42f) * 30f) * 14f * (1f - u) : 18f;
                s.Sign.localRotation = sr * Quaternion.Euler(0, 0, wob);
                yield return null;
            }
            s.Sign.localPosition = sp; s.Sign.localRotation = sr;
            Sfx.Play("build", -6f, 0.9f);
            // cimientos: cada 25 ms una piedra salta desde el suelo
            for (int i = 0; i < s.Stones.Count; i++)
            {
                var st = s.Stones[i];
                st.gameObject.SetActive(true);
                StartCoroutine(StoneHop(st));
                Sfx.Play("pop", -14f, 0.9f + i * 0.04f);
                yield return new WaitForSeconds(0.025f);
            }
            // andamio que se arma de abajo hacia arriba
            v.Scaffold.gameObject.SetActive(true);
            for (float t = 0f; t < 0.36f; t += Time.deltaTime)
            {
                float u = t / 0.36f;
                v.Scaffold.localScale = new Vector3(1f, Mathf.Max(0.01f, OutBack(u)), 1f);
                yield return null;
            }
            v.Scaffold.localScale = Vector3.one;
        }

        IEnumerator StoneHop(Transform st)
        {
            Vector3 p = st.localPosition;
            for (float t = 0f; t < 0.28f; t += Time.deltaTime)
            {
                if (st == null) yield break;
                float u = t / 0.28f;
                st.localPosition = p + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.35f - (1f - u) * 0.2f);
                st.localScale = Vector3.one * OutBack(u);
                yield return null;
            }
            if (st != null) { st.localPosition = p; st.localScale = Vector3.one; }
        }

        /// <summary>
        /// B.1.5: revelacion. Las tablas del andamio salen volando en abanico, la lona sube girando, el edificio aparece
        /// estirado y se asienta; anillo de luz, estrellitas, confeti y cartel.
        /// </summary>
        IEnumerator RevealShow(PlotView v)
        {
            v.Driven = true;
            Vector3 c = v.Root.position;
            SiteView s;
            sites.TryGetValue(v.P.Id, out s);
            FocusOn(c);
            Sfx.Play("whoosh", -6f, 0.9f);
            // tablas en abanico
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                StartCoroutine(FlyingPlank(c + Vector3.up * (0.6f + (i % 3) * 0.6f), new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))));
            }
            v.Scaffold.gameObject.SetActive(false);
            // lona que se va al cielo girando
            if (s != null && s.Tarp != null && s.Tarp.gameObject.activeSelf) StartCoroutine(TarpAway(s.Tarp));
            yield return new WaitForSeconds(0.15f);
            if (s != null && s.Root != null)
            {
                foreach (var st in s.Stones) if (st != null) st.gameObject.SetActive(false);
                if (s.Sign != null) s.Sign.gameObject.SetActive(false);
            }
            RefreshPlot(v);
            if (v.Body != null)
            {
                v.Body.localScale = new Vector3(0.85f, 1.3f, 0.85f);
                FxApi.Play("dust", c + Vector3.up * 0.3f, new Color(0.95f, 0.9f, 0.8f), 2.8f);
                yield return Pop(v, 0.55f, 0f);
            }
            DropSite(v);
            Sfx.Play("thud", -5f, 1.1f);
            yield return GrowPath(v.P.Id);
            BuildLamps();
            PathFlowers(v.P.Id);
            v.SmokeT = 0.2f;
            Color fam = IslandArt.FamilyRoof((BKind)v.P.Building);
            FxApi.Play("ring", c + Vector3.up * 0.1f, new Color(1f, 0.95f, 0.6f), 3.2f);
            FxApi.Play("levelup_aura", c, fam, 1.8f);
            FxApi.Play("confetti", c + Vector3.up * (v.Height + 1.2f), default(Color), 1.6f);
            Sfx.Play("milestone", -3f);
            Sfx.PlayLater("cheer", 0.25f, -8f);
            Mineros.Fx.Haptics.Success();
            Cheer(c, 7f);
            foreach (var b in builders) if (b.Plot == v.P.Id || (b.Pos - c).magnitude < 4f) b.Celebrate = 1f;
            var d = Island.Def((BKind)v.P.Building);
            Ui.Banner(Loc.T("¡Obra terminada!"), d.Name + ": " + d.Desc, Kit3.Yellow, IslandStage.I.BuildingIcon((BKind)v.P.Building, 1), null);
            v.Driven = false;
        }

        IEnumerator FlyingPlank(Vector3 from, Vector3 dir)
        {
            var mb = new MeshBuilder();
            mb.Box(Vector3.zero, new Vector3(1.0f, 0.08f, 0.16f), IslandArt.Wood, 0f);
            var r = IslandArt.Bake(mb, root, "Tabla", false).transform;
            r.position = from;
            Vector3 vel = dir * Random.Range(3.5f, 5f) + Vector3.up * Random.Range(4f, 6f);
            Vector3 spin = new Vector3(Random.Range(-600f, 600f), Random.Range(-300f, 300f), Random.Range(-600f, 600f));
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                vel += Vector3.down * 14f * Time.deltaTime;
                r.position += vel * Time.deltaTime;
                r.Rotate(spin * Time.deltaTime);
                if (t > 0.7f) r.localScale = Vector3.one * Mathf.Max(0.01f, 1f - (t - 0.7f) / 0.2f);
                yield return null;
            }
            FxApi.Play("tinydust", r.position, default(Color), 0.8f);
            Destroy(r.gameObject);
        }

        IEnumerator TarpAway(Transform tarp)
        {
            tarp.SetParent(root, true);
            Vector3 p = tarp.position;
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                if (tarp == null) yield break;
                float u = t / 1.1f;
                tarp.position = p + Vector3.up * (u * u * 9f) + new Vector3(u * 1.5f, 0f, 0f);
                tarp.rotation = Quaternion.Euler(0f, u * 360f, Mathf.Sin(u * 9f) * 20f);
                tarp.localScale = new Vector3(1f + u * 0.4f, Mathf.Max(0.02f, 1f - u), 1f + u * 0.4f);
                yield return null;
            }
            if (tarp != null) Destroy(tarp.gameObject);
        }

        /// <summary>Por cuadro: etapas de la lona (al 33 % y al 66 %, con destello) y andamio durante las mejoras.</summary>
        void UpdateSites()
        {
            foreach (var v in plots)
            {
                var p = v.P;
                bool firstBuild = p.Building >= 0 && p.Level == 0 && p.Work > 0;
                if (!firstBuild)
                {
                    if (sites.ContainsKey(p.Id) && !v.Driven && p.Level >= 1) DropSite(v);
                    continue;
                }
                var s = Site(v);
                float prog = p.WorkTotal > 0 ? 1f - (float)(p.Work / p.WorkTotal) : 0f;
                int stage = prog >= 0.66f ? 2 : prog >= 0.33f ? 1 : 0;
                if (stage != s.Stage)
                {
                    bool grew = s.Stage >= 0 && stage > s.Stage;
                    s.Stage = stage;
                    s.Tarp.gameObject.SetActive(stage > 0);
                    float h = stage == 0 ? 0.01f : stage == 1 ? 1.1f : 2.0f;
                    if (grew) StartCoroutine(TarpGrow(s.Tarp, h));
                    else s.Tarp.localScale = new Vector3(1f, h, 1f);
                    if (grew) { FxApi.Play("sparkle", v.Root.position + Vector3.up * h, new Color(1f, 0.95f, 0.7f), 1.2f); Sfx.Play("tick", -10f, 1.2f); }
                }
            }
        }

        IEnumerator TarpGrow(Transform t, float h)
        {
            float h0 = t.localScale.y;
            for (float k = 0f; k < 0.35f; k += Time.deltaTime)
            {
                if (t == null) yield break;
                t.localScale = new Vector3(1f, Mathf.LerpUnclamped(h0, h, OutBack(k / 0.35f)), 1f);
                yield return null;
            }
            if (t != null) t.localScale = new Vector3(1f, h, 1f);
        }

        // ------------------------------------------------------------ constructores
        Vector3 PlazaPoint(float angle, int idx)
        {
            float r = Island.PlazaR + 0.4f;
            return new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        }

        /// <summary>Puerta del Ayuntamiento (del lado de la camara): ahi salen y entran los constructores.</summary>
        Vector3 IdleSpot(int i)
        {
            Vector3 f = Cam != null ? -Cam.transform.forward : Vector3.back;
            f.y = 0f; f.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, f) * ((i % 3) - 1) * 0.35f;
            return f * (Island.Def(BKind.Depot).Radius + 0.45f) + side;
        }

        /// <summary>Camino por la plaza (arco) y despues derecho por el camino de la parcela.</summary>
        void RouteTo(BuilderView b, Vector3 to)
        {
            b.Path.Clear();
            float a0 = Mathf.Atan2(b.Pos.z, b.Pos.x), a1 = Mathf.Atan2(to.z, to.x);
            float far0 = new Vector2(b.Pos.x, b.Pos.z).magnitude, far1 = new Vector2(to.x, to.z).magnitude;
            if (far0 > Island.PlazaR + 1.2f) b.Path.Add(PlazaPoint(a0, 0));
            float da = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            int steps = Mathf.CeilToInt(Mathf.Abs(da) / 0.6f);
            for (int i = 1; i <= steps; i++) b.Path.Add(PlazaPoint(a0 + da * i / steps, 0));
            if (far1 > Island.PlazaR + 1.2f) b.Path.Add(to);
            else b.Path.Add(to);
        }

        void UpdateBuilders(float dt)
        {
            int want = Isl.Builders();
            while (builders.Count < want)
            {
                var b = new BuilderView { Index = builders.Count };
                b.Model = MinerModel.Create(root, 900 + b.Index);
                b.Model.transform.localScale = Vector3.one * MinerScale;
                b.Model.SetLook(BuilderHelmet, new Tool(1, 0));
                b.Pos = IdleSpot(b.Index);
                b.Model.transform.localPosition = b.Pos;
                IslandArt.Blob(b.Model.transform, 0.45f, 0.35f);
                b.BlinkT = Random.value * 3f;
                b.Model.gameObject.SetActive(false);
                builders.Add(b);
            }
            // asignar obras a constructores libres (el mas cercano)
            foreach (var v in plots)
            {
                var p = v.P;
                if (p.Work <= 0) continue;
                bool taken = false;
                foreach (var b in builders) if (b.Plot == p.Id) { taken = true; break; }
                if (taken) continue;
                BuilderView best = null; float bd = float.MaxValue;
                foreach (var b in builders)
                {
                    if (b.Plot >= 0) continue;
                    float d = (b.Pos - v.Root.localPosition).sqrMagnitude;
                    if (d < bd) { bd = d; best = b; }
                }
                if (best == null) break;
                best.Plot = p.Id;
                if (best.Inside)
                {
                    // sale por la puerta con un saltito y una nubecita
                    best.Inside = false;
                    best.Pos = IdleSpot(best.Index);
                    best.Model.transform.localPosition = best.Pos;
                    best.Model.gameObject.SetActive(true);
                    FxApi.Play("tinydust", best.Model.transform.position, new Color(0.9f, 0.85f, 0.75f), 0.8f);
                }
                Vector3 toC = -new Vector3(p.X, 0, p.Z).normalized;
                if (toC.sqrMagnitude < 0.01f) toC = Vector3.back;
                float r = (p.Building >= 0 ? Island.Defs[p.Building].Radius : 1.4f) + 0.35f;
                Vector3 side = Vector3.Cross(Vector3.up, toC) * ((best.Index % 2 == 0) ? -0.5f : 0.5f);
                RouteTo(best, new Vector3(p.X, 0, p.Z) + (toC + side).normalized * r);
            }
            foreach (var b in builders)
            {
                if (b.Plot >= 0 && Isl.Plots[b.Plot].Work <= 0)
                {
                    b.Plot = -1;
                    RouteTo(b, IdleSpot(b.Index));
                }
                if (b.Inside) continue;
                bool moving = false;
                if (b.Path.Count > 0)
                {
                    Vector3 to = b.Path[0];
                    Vector3 d = to - b.Pos; d.y = 0f;
                    float step = 2.6f * dt;
                    if (d.magnitude <= step) { b.Pos = to; b.Path.RemoveAt(0); }
                    else { b.Pos += d.normalized * step; b.Dir = d.normalized; moving = true; }
                }
                // de vuelta en la puerta y sin obra: entra (se achica en el umbral)
                bool home = b.Plot < 0 && b.Path.Count == 0;
                b.Show = Mathf.MoveTowards(b.Show, home ? 0f : 1f, dt * (home ? 4f : 5f));
                if (home && b.Show <= 0f) { b.Inside = true; b.Model.gameObject.SetActive(false); continue; }
                b.Model.transform.localScale = Vector3.one * MinerScale * Mineros.UI.Tw.Eval(Mineros.UI.Ease.OutBack, b.Show);
                float swing = -1f;
                if (!moving && b.Plot >= 0)
                {
                    var p = Isl.Plots[b.Plot];
                    b.Dir = (new Vector3(p.X, 0, p.Z) - b.Pos).normalized;
                    float prevHit = b.HitT;
                    b.HitT += dt;
                    swing = Mathf.Repeat(b.HitT / 0.6f + b.Index * 0.3f, 1f);
                    float prevSwing = Mathf.Repeat(prevHit / 0.6f + b.Index * 0.3f, 1f);
                    if (prevSwing < 0.62f && swing >= 0.62f)
                    {
                        Vector3 at = b.Model.transform.position + b.Dir * 0.5f + Vector3.up * 0.5f;
                        if (OnScreen(at)) FxApi.Play("hit_spark", at, new Color(1f, 0.85f, 0.5f), 0.45f);
                        float near = Sound != null ? Sound.Zoom01 : 0.5f;
                        if (OnScreen(at) && near > 0.5f && Random.value < 0.5f) Sfx.Play("build", -24f + 10f * near, 0.95f + Random.value * 0.2f);
                    }
                }
                else if (!moving) b.Dir = Vector3.Lerp(b.Dir, -b.Pos.normalized, dt * 6f);   // en la puerta se dan vuelta para entrar
                if (moving) b.Phase += dt * 11f * (1.3f / MinerScale) * 0.8f;
                b.BlinkT -= dt;
                if (b.BlinkT < 0f) b.BlinkT = 2.5f + Random.value * 3f;
                b.Celebrate = Mathf.MoveTowards(b.Celebrate, 0f, dt * 0.8f);
                var t = b.Model.transform;
                t.localPosition = Vector3.Lerp(t.localPosition, b.Pos, 1f - Mathf.Exp(-dt * 20f));
                b.Model.SetCelebrate(b.Celebrate);
                b.Model.SetPose(b.Dir, b.Phase, moving, swing, b.BlinkT < 0.12f, dt);
            }
        }

        // ------------------------------------------------------------ vida de los edificios de la ciudad (B.3)
        /// <summary>Esta trabajando: extraccion con lugar libre, o cola con algo, y sin obra.</summary>
        bool Producing(Plot p)
        {
            if (p.Building < 0 || p.Level < 1 || p.Work > 0) return false;
            if (Island.Extractor(p.Building) != null) return p.Ready < Isl.ExtractBuffer(p);
            if (Island.Produces(p.Building)) return p.Queue.Count > 0;
            return true;
        }

        static readonly Color SmokeL = new Color(0.93f, 0.93f, 0.96f), SmokeD = new Color(0.45f, 0.43f, 0.42f);

        /// <summary>Efecto caracteristico de cada edificio mientras produce (parado no hace nada: se ve quien esta ocioso).</summary>
        void CityLife(PlotView v, Vector3 top, Vector3 front, bool onScreen, float near)
        {
            var p = v.P;
            if (!Producing(p) || !onScreen) { v.SmokeT = 1.2f; return; }
            var k = (BKind)p.Building;
            Vector3 c = v.Root.position;
            switch (k)
            {
                case BKind.CoalMine: case BKind.IronMine: case BKind.CopperMine: case BKind.GoldMine: case BKind.GemMine:
                {
                    v.SmokeT = 2.2f + Random.value;
                    Color ore = k == BKind.CoalMine ? new Color(0.3f, 0.3f, 0.34f) : k == BKind.IronMine ? IslandArt.OreCol[2]
                        : k == BKind.CopperMine ? IslandArt.OreCol[1] : k == BKind.GoldMine ? IslandArt.OreCol[3] : IslandArt.OreCol[4];
                    FxApi.Play("tinydust", front + Vector3.up * 0.3f, new Color(0.75f, 0.66f, 0.55f), 1.3f);
                    if (k == BKind.GoldMine || k == BKind.GemMine) FxApi.Play("sparkle", c + Vector3.up * 1.2f, ore, 0.9f);
                    if (near > 0.6f) Sfx.Play("pick", -30f + 8f * near, 0.75f + Random.value * 0.1f);
                    break;
                }
                case BKind.Sawmill:
                    v.SmokeT = 0.5f;
                    FxApi.Play("tinydust", c + Vector3.up * 0.9f, new Color(0.95f, 0.85f, 0.65f), 0.7f);
                    if (near > 0.65f && Random.value < 0.25f) Sfx.Play("wheel", -30f + 8f * near, 1.6f);   // la sierra (20)
                    break;
                case BKind.SandPit:
                    v.SmokeT = 1.3f;
                    FxApi.Play("tinydust", c + new Vector3(0.4f, 0.8f, 0.3f), new Color(0.95f, 0.86f, 0.6f), 1.1f);
                    break;
                case BKind.CrystalWell:
                    v.SmokeT = 0.9f;
                    FxApi.Play("sparkle", c + Vector3.up * (1.1f + Random.value * 0.4f), new Color(0.5f, 0.9f, 1f), 1f);
                    break;
                case BKind.Foundry: case BKind.GoldRefinery:
                    v.SmokeT = 1.4f;
                    if (near > 0.65f) Sfx.Play("crackle", -30f + 8f * near, 0.9f);   // brasas (20)
                    FxApi.Play("smoke", top + new Vector3(0.5f, 0.6f, 0.2f), SmokeD, 0.9f);
                    FxApi.Play("hit_spark", front + Vector3.up * 0.6f, new Color(1f, 0.6f, 0.25f), 0.6f);
                    break;
                case BKind.GlassKiln:
                    v.SmokeT = 1.6f;
                    FxApi.Play("smoke", top + Vector3.up * 0.4f, SmokeL, 0.8f);
                    FxApi.Play("sparkle", front + Vector3.up * 0.5f, new Color(1f, 0.6f, 0.3f), 0.7f);
                    break;
                case BKind.Workshop:
                    v.SmokeT = 1.8f;
                    FxApi.Play("smoke", top + new Vector3(-0.5f, 0.4f, 0.3f), SmokeD, 0.7f);
                    FxApi.Play("hit_spark", front + Vector3.up * 0.7f, new Color(0.85f, 0.9f, 1f), 0.6f);
                    if (near > 0.6f) Sfx.Play("anvil", -30f + 8f * near, 1.3f + Random.value * 0.2f);
                    break;
                case BKind.Lapidary: case BKind.Jeweler:
                    v.SmokeT = 1.1f;
                    FxApi.Play("sparkle", front + Vector3.up * 0.8f, k == BKind.Jeweler ? new Color(1f, 0.85f, 0.35f) : new Color(0.5f, 0.9f, 1f), 0.8f);
                    break;
                case BKind.Lab:
                    v.SmokeT = 1.5f;
                    Color[] puffs = { new Color(0.7f, 0.5f, 0.95f), new Color(0.5f, 0.9f, 0.6f), new Color(0.5f, 0.85f, 1f) };
                    if (near > 0.65f) Sfx.Play("pop", -30f + 8f * near, 0.6f + Random.value * 0.3f);   // burbujas (20)
                    FxApi.Play("smoke", top + Vector3.up * 0.3f, puffs[Random.Range(0, 3)], 0.8f);
                    break;
                case BKind.Bank:
                    v.SmokeT = 3f;
                    FxApi.Play("sparkle", top, new Color(1f, 0.85f, 0.35f), 0.7f);
                    break;
                case BKind.Hospital: case BKind.School: case BKind.Managers: case BKind.Barn: case BKind.Warehouse:
                    v.SmokeT = 2.4f;
                    FxApi.Play("smoke", top + new Vector3(0.2f, 0.2f, 0.2f), SmokeL, 0.5f);
                    break;
                default:
                    v.SmokeT = 4f;
                    break;
            }
        }

        /// <summary>Piezas que se mueven de verdad: la sierra del aserradero y los engranajes del taller.</summary>
        void CityParts(PlotView v)
        {
            var k = (BKind)v.P.Building;
            if (v.Model == null || (k != BKind.Sawmill && k != BKind.Workshop)) return;
            if (IslandArt.FileOf(k) != "") return;   // un modelo de Tripo trae sus propias piezas
            var mb = new MeshBuilder();
            Vector3 c;
            if (k == BKind.Sawmill)
            {
                c = new Vector3(0.05f, 0.82f, 0f);
                mb.Cyl(new Vector3(0, 0, -0.045f), new Vector3(0, 0, 0.045f), 0.44f, 0.44f, 18, new Color(0.62f, 0.68f, 0.75f), 0.05f);
                for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; mb.Box(new Vector3(Mathf.Cos(a) * 0.46f, Mathf.Sin(a) * 0.46f, 0f), new Vector3(0.07f, 0.07f, 0.05f), new Color(0.75f, 0.8f, 0.86f), 0f); }
                mb.Cyl(new Vector3(0, 0, -0.07f), new Vector3(0, 0, 0.07f), 0.1f, 0.1f, 8, IslandArt.Dark, 0f);
            }
            else
            {
                c = new Vector3(-0.45f, 0.95f, -0.93f);
                mb.Cyl(Vector3.zero, new Vector3(0, 0, -0.09f), 0.4f, 0.4f, 14, new Color(0.62f, 0.68f, 0.75f), 0f);
                for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; mb.Box(new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(a) * 0.42f, -0.045f), new Vector3(0.13f, 0.13f, 0.09f), new Color(0.62f, 0.68f, 0.75f), 0f); }
                mb.Cyl(new Vector3(0, 0, -0.09f), new Vector3(0, 0, -0.13f), 0.12f, 0.12f, 10, IslandArt.Dark, 0f);
            }
            var r = IslandArt.Bake(mb, v.Model.transform, "Pieza", true);
            r.transform.localPosition = c;
            var spin = r.gameObject.AddComponent<PartSpin>();
            spin.Game = this; spin.P = v.P; spin.Speed = k == BKind.Sawmill ? 540f : 90f;
        }

        public bool IsProducing(Plot p) { return Producing(p); }

        // ------------------------------------------------------------ tren (B.4)
        Transform train;
        readonly List<Transform> trainCars = new List<Transform>();
        float trainX = 14f, trainV;
        bool trainLeaving;
        float trainSmokeT;

        static Mesh Loco(out Material[] mats)
        {
            var mb = new MeshBuilder();
            Color red = IslandArt.RoofRedF, dark = IslandArt.H("3b3f47"), gold = IslandArt.RoofGold;
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.5f, 0.9f), dark, 0f);                  // chasis
            mb.Cyl(new Vector3(-0.75f, 0.85f, 0), new Vector3(0.45f, 0.85f, 0), 0.36f, 0.36f, 16, red, 0f);   // caldera
            mb.Box(new Vector3(0.6f, 1.05f, 0), new Vector3(0.7f, 0.9f, 0.95f), red, 0f);                // cabina
            mb.Box(new Vector3(0.6f, 1.55f, 0), new Vector3(0.85f, 0.1f, 1.05f), dark, 0f);              // techo
            mb.Box(new Vector3(0.6f, 1.15f, -0.48f), new Vector3(0.35f, 0.3f, 0.03f), IslandArt.Glass, 0.3f);
            mb.Cyl(new Vector3(-0.55f, 1.15f, 0), new Vector3(-0.55f, 1.65f, 0), 0.12f, 0.18f, 10, dark, 0f);   // chimenea
            mb.Box(new Vector3(-0.98f, 0.85f, 0), new Vector3(0.06f, 0.3f, 0.3f), gold, 0.6f);           // farol delantero
            for (int i = 0; i < 3; i++)
                for (int s = -1; s <= 1; s += 2)
                    mb.Cyl(new Vector3(-0.55f + i * 0.5f, 0.22f, s * 0.47f), new Vector3(-0.55f + i * 0.5f, 0.22f, s * 0.52f), 0.2f, 0.2f, 12, gold, 0.1f);
            var m = mb.ToMesh(null, "Locomotora", out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }

        static Mesh Car(Color load, out Material[] mats)
        {
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0, 0.38f, 0), new Vector3(1.3f, 0.12f, 0.85f), IslandArt.H("3b3f47"), 0f);
            mb.Box(new Vector3(0, 0.7f, 0), new Vector3(1.25f, 0.55f, 0.8f), IslandArt.Wood, 0f);
            mb.Box(new Vector3(0, 0.7f, -0.41f), new Vector3(1.27f, 0.08f, 0.02f), IslandArt.WoodD, 0f);
            for (int i = 0; i < 4; i++) mb.Octa(new Vector3(-0.4f + (i % 2) * 0.4f + (i / 2) * 0.2f, 1.02f + (i / 2) * 0.08f, -0.1f + (i % 2) * 0.2f), Vector3.one * 0.26f, load, 0.05f);
            for (int i = 0; i < 2; i++)
                for (int s = -1; s <= 1; s += 2)
                    mb.Cyl(new Vector3(-0.4f + i * 0.8f, 0.2f, s * 0.44f), new Vector3(-0.4f + i * 0.8f, 0.2f, s * 0.49f), 0.18f, 0.18f, 12, IslandArt.RoofGold, 0.1f);
            var m = mb.ToMesh(null, "Vagon", out mats);
            mats = VertexColorMerge.Apply(m, mats);
            return m;
        }

        void BuildTrain(PlotView st)
        {
            if (train != null) Destroy(train.gameObject);
            trainCars.Clear();
            train = new GameObject("Tren").transform;
            train.SetParent(st.Model != null ? st.Model.transform : st.Root, false);
            train.localPosition = new Vector3(trainX, 0.04f, -1.35f);
            Material[] mats;
            var loco = IslandArt.MakeRenderer(train, "Locomotora", Loco(out mats), mats).transform;
            trainCars.Add(loco);
            for (int i = 0; i < Isl.Wagons.Count; i++)
            {
                var w = Isl.Wagons[i];
                var car = IslandArt.MakeRenderer(train, "Vagon", Car(ResIcons.Tint((int)w.Want), out mats), mats).transform;
                car.localPosition = new Vector3(1.55f + i * 1.45f, 0f, 0f);
                trainCars.Add(car);
            }
        }

        void UpdateTrain(float dt)
        {
            var stP = Isl.Find(BKind.Train);
            if (stP == null || stP.Level < 1) { if (train != null) { Destroy(train.gameObject); train = null; } return; }
            var st = plots[stP.Id];
            if (Isl.TrainHere && train == null)
            {
                trainX = 14f; trainV = 9f; trainLeaving = false;   // entra con la locomotora adelante (hacia -X)
                BuildTrain(st);
                if (OnScreen(st.Root.position)) { Sfx.Play("horn", -6f, 1.25f); }
            }
            if (!Isl.TrainHere && train != null && !trainLeaving) { trainLeaving = true; trainV = 0.5f; Sfx.Play("horn", -6f, 1.35f); foreach (var mv in miners.Values) if ((mv.Model.transform.position - st.Root.position).sqrMagnitude < 64f) mv.Celebrate = 0.8f; }
            if (train == null) return;
            if (!trainLeaving)
            {
                // llega frenando: la velocidad baja con la distancia y se detiene con un rebote de cada vagon
                float target = -0.6f;
                float d = Mathf.Abs(trainX - target);
                float prevV = trainV;
                trainV = Mathf.Max(0f, Mathf.Min(trainV, Mathf.Sqrt(Mathf.Max(0f, d) * 7f)));
                trainX = Mathf.MoveTowards(trainX, target, trainV * dt);
                if (prevV > 0.3f && trainV <= 0.3f)
                {
                    FxApi.Play("hit_spark", train.position + Vector3.up * 0.2f, new Color(1f, 0.8f, 0.4f), 1f);
                    for (int i = 0; i < trainCars.Count; i++) StartCoroutine(CarBump(trainCars[i], i * 0.08f));
                    Sfx.Play("thud", -10f, 1.3f);
                }
            }
            else
            {
                trainV = Mathf.Min(trainV + dt * 3f, 12f);
                trainX -= trainV * dt;   // sale hacia el otro lado
                if (trainX < -18f) { Destroy(train.gameObject); train = null; return; }
            }
            train.localPosition = new Vector3(trainX, 0.04f, -1.35f);
            // humo en bolitas mientras se mueve
            trainSmokeT -= dt;
            if (trainV > 0.5f && trainSmokeT <= 0f) { trainSmokeT = 0.22f; FxApi.Play("smoke", trainCars[0].position + Vector3.up * 1.8f, new Color(0.9f, 0.9f, 0.92f), 0.45f); }
            // vagon cargado: hunde un poquito
            for (int i = 1; i < trainCars.Count && i - 1 < Isl.Wagons.Count; i++)
            {
                float y = Isl.Wagons[i - 1].Done ? -0.04f : 0f;
                var p = trainCars[i].localPosition;
                trainCars[i].localPosition = new Vector3(p.x, Mathf.Lerp(p.y, y, dt * 6f), p.z);
            }
        }

        IEnumerator CarBump(Transform car, float delay)
        {
            yield return new WaitForSeconds(delay);
            Vector3 p = car.localPosition;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                if (car == null) yield break;
                car.localPosition = p + new Vector3(Mathf.Sin(t / 0.3f * Mathf.PI) * 0.12f, 0f, 0f);
                yield return null;
            }
            if (car != null) car.localPosition = p;
        }

        // ------------------------------------------------------------ almacen lleno (B.3)
        float fullT;

        void UpdateFullStorage(float dt)
        {
            fullT -= dt;
            if (fullT > 0f) return;
            fullT = 2f;
            bool rawFull = Isl.RawStored() >= Isl.RawCap(), prodFull = Isl.ProdStored() >= Isl.ProdCap();
            foreach (var v in plots)
            {
                if (v.Body == null || v.Driven) continue;
                bool full = (v.P.Building == (int)BKind.Barn && rawFull) || (v.P.Building == (int)BKind.Warehouse && prodFull);
                if (!full) continue;
                v.Punch = 1f;   // se infla y se desinfla (el modelo, nunca la camara)
                if (OnScreen(v.Root.position)) FxApi.Play("tinydust", v.Root.position + Vector3.up * v.Height, new Color(0.9f, 0.8f, 0.6f), 0.8f);
            }
        }

        void UpdateCity(float dt)
        {
            UpdateTrain(dt);
            UpdateFullStorage(dt);
            UpdateWonderVisible();
            UpdateDetails();
            UpdateSites();
            UpdateBuilders(dt);
        }
    }

    /// <summary>Gira una pieza (sierra, engranaje) solo mientras el edificio produce; frena suave al parar.</summary>
    public sealed class PartSpin : MonoBehaviour
    {
        public IslandGame Game;
        public Plot P;
        public float Speed;
        float w;

        void Update()
        {
            if (Game == null || P == null) return;
            float target = Game.IsProducing(P) ? Speed : 0f;
            w = Mathf.MoveTowards(w, target, Speed * 2f * Time.deltaTime);
            transform.Rotate(0f, 0f, w * Time.deltaTime, Space.Self);
        }
    }
}
