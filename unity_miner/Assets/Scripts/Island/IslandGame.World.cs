using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.IslandView
{
    /// <summary>
    /// Dia y noche (biblia 3.7): la luz pasa de la mañana calida al atardecer naranja (sombras largas) y a la noche azul;
    /// de noche se encienden los faroles de los caminos de uno en uno desde la plaza, las ventanas brillan, el faro barre
    /// el mar con su haz, aparecen luciernagas y cristales nocturnos que hay que tocar. Al amanecer los mineros salen a
    /// estirarse y canta el gallo.
    /// </summary>
    public sealed partial class IslandGame
    {
        Light sun;
        IslandPost post;

        // colores de la luz por momento del dia (sol, cielo, horizonte, suelo)
        // noche de luna clara (auditoria final: a 0.42 de sol y ambiente 0.12-0.22 la isla no se leia en el telefono);
        // las ventanas encendidas siguen siendo lo que manda
        static readonly Color SunDay = new Color(1f, 0.95f, 0.86f), SunDusk = new Color(1f, 0.62f, 0.4f), SunNight = new Color(0.55f, 0.66f, 1f);
        static readonly Color SkyDay = new Color(0.8f, 0.88f, 0.98f), SkyDusk = new Color(0.95f, 0.72f, 0.62f), SkyNight = new Color(0.34f, 0.42f, 0.7f);
        static readonly Color EqDay = new Color(0.74f, 0.8f, 0.72f), EqDusk = new Color(0.85f, 0.62f, 0.5f), EqNight = new Color(0.27f, 0.33f, 0.56f);
        static readonly Color GrDay = new Color(0.52f, 0.5f, 0.44f), GrDusk = new Color(0.55f, 0.42f, 0.36f), GrNight = new Color(0.2f, 0.22f, 0.36f);
        static readonly Color SeaShallowNight = new Color(0.10f, 0.28f, 0.45f), SeaDeepNight = new Color(0.03f, 0.10f, 0.26f);

        sealed class Lamp { public Vector3 P; public MeshRenderer Glass, Halo, Pool; public float Delay, K; }
        readonly List<Lamp> lamps = new List<Lamp>();
        Transform lampRoot;
        float nightSince = -1f, envT;
        static Mesh lampPole, lampGlass;
        static Material[] lampPoleMats, lampGlassMats;
        ParticleSystem fireflies;
        Transform beam;
        readonly Dictionary<int, MeshRenderer> windowHalos = new Dictionary<int, MeshRenderer>();

        void InitWorld()
        {
            post = Cam.GetComponent<IslandPost>();
            var lightGo = GameObject.Find("Sol");
            if (lightGo != null) sun = lightGo.GetComponent<Light>();
            BuildLamps();
            fireflies = IslandFxKit.System(root, "Luciernagas", IslandFxKit.Additive(), 60);
            var m = fireflies.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            m.startSpeed = 0.15f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
            m.startColor = new Color(0.85f, 1f, 0.45f, 0.9f);
            var sh = fireflies.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = Isl.Radius - 1f;
            sh.rotation = new Vector3(90f, 0f, 0f);
            fireflies.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var noise = fireflies.noise;
            noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.4f; noise.scrollSpeed = 0.2f;
            var col = fireflies.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.3f, 0.5f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var em = fireflies.emission;
            em.rateOverTime = 0f;
            fireflies.Play();
            Isl.Morning += OnMorning;
            Isl.OreFaded += OnOreFaded;
            ApplyLight(true);
        }

        // ------------------------------------------------------------ faroles
        void BuildLamps()
        {
            if (lampRoot != null) Destroy(lampRoot.gameObject);
            lamps.Clear();
            lampRoot = new GameObject("Faroles").transform;
            lampRoot.SetParent(root, false);
            if (lampPole == null) MakeLampMeshes();
            BuildTufts();
            var pts = new List<Vector3>();
            // plaza: cuatro faroles en diagonal
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                pts.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (Island.PlazaR + 0.45f));
            }
            // caminos de lo construido: un farol cada ~3.4 m, alternando de lado
            foreach (var path in Isl.Paths)
            {
                var p = Isl.Plots[path.Plot];
                if (p.Building < 0 || p.Ring > Isl.Expand) continue;
                float acc = 1.6f; int side = path.Plot % 2 == 0 ? 1 : -1;
                for (int i = 1; i < path.X.Length; i++)
                {
                    var a = new Vector3(path.X[i - 1], 0f, path.Z[i - 1]);
                    var b = new Vector3(path.X[i], 0f, path.Z[i]);
                    float seg = Vector3.Distance(a, b);
                    acc += seg;
                    if (acc < 3.4f) continue;
                    acc = 0f;
                    Vector3 dir = (b - a).normalized;
                    Vector3 n = Vector3.Cross(Vector3.up, dir) * side;
                    side = -side;
                    Vector3 at = b + n * (Island.PathHalf + 0.22f);
                    if (at.magnitude < Island.PlazaR + 1.2f) continue;
                    bool near = false;
                    foreach (var q in pts) if ((q - at).sqrMagnitude < 2.2f) { near = true; break; }
                    foreach (var pl in Isl.Plots)
                        if (pl.Building >= 0 && (new Vector3(pl.X, 0, pl.Z) - at).magnitude < Island.Defs[pl.Building].Radius + 0.4f) { near = true; break; }
                    if (!near) pts.Add(at);
                }
            }
            foreach (var at in pts)
            {
                var t = new GameObject("Farol").transform;
                t.SetParent(lampRoot, false);
                t.localPosition = at;
                IslandArt.MakeRenderer(t, "Poste", lampPole, lampPoleMats);
                var glass = IslandArt.MakeRenderer(t, "Vidrio", lampGlass, lampGlassMats, false);
                var halo = IslandFxKit.Halo(t, "Halo", 2.2f);
                halo.transform.localPosition = new Vector3(0f, 1.32f, 0f);
                halo.enabled = false;
                // charco de luz calida en el suelo alrededor del farol
                var pool = IslandFxKit.Halo(t, "Charco", 3.4f);
                pool.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                pool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                pool.enabled = false;
                IslandArt.Blob(t, 0.35f, 0.3f);
                lamps.Add(new Lamp { P = at, Glass = glass, Halo = halo, Pool = pool, Delay = at.magnitude * 0.12f });
            }
        }

        // ------------------------------------------------------------ mechones de pasto en los bordes de los caminos
        Transform tufts;

        /// <summary>Una sola malla con todos los mechones (se mecen en el shader GrassWind con las rafagas).</summary>
        void BuildTufts()
        {
            if (tufts != null) Destroy(tufts.gameObject);
            var mb = new MeshBuilder();
            var rnd = new System.Random(7);
            Color dark = IslandArt.GrassD, light = IslandArt.H("a6d65a"), tip = IslandArt.H("c4e57a");
            foreach (var path in Isl.Paths)
            {
                if (Isl.Plots[path.Plot].Ring > Isl.Expand) continue;
                float acc = 0f;
                for (int i = 1; i < path.X.Length; i++)
                {
                    var a = new Vector3(path.X[i - 1], 0f, path.Z[i - 1]);
                    var b = new Vector3(path.X[i], 0f, path.Z[i]);
                    acc += Vector3.Distance(a, b);
                    if (acc < 0.9f) continue;
                    acc = 0f;
                    Vector3 dir = (b - a).normalized, n = Vector3.Cross(Vector3.up, dir);
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        if (rnd.NextDouble() < 0.35) continue;
                        Vector3 c = b + n * sd * (Island.PathHalf + 0.12f + (float)rnd.NextDouble() * 0.25f) + dir * (float)(rnd.NextDouble() - 0.5);
                        if (c.magnitude < Island.PlazaR + 0.6f) continue;
                        bool inPlot = false;
                        foreach (var pl in Isl.Plots)
                            if (pl.Ring <= Isl.Expand && (new Vector3(pl.X, 0, pl.Z) - c).magnitude < 1.5f) { inPlot = true; break; }
                        if (inPlot) continue;
                        int blades = 4 + rnd.Next(3);
                        float sc = 0.8f + (float)rnd.NextDouble() * 0.5f;
                        for (int k = 0; k < blades; k++)
                        {
                            float ang = (float)(k / (double)blades * Mathf.PI * 2 + rnd.NextDouble());
                            var bdir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                            var root0 = c + bdir * 0.05f;
                            var top = c + bdir * 0.16f * sc + Vector3.up * (0.24f + (float)rnd.NextDouble() * 0.16f) * sc;
                            var side = Vector3.Cross(Vector3.up, bdir) * 0.04f * sc;
                            Color col = Color.Lerp(dark, light, (float)rnd.NextDouble());
                            mb.Tri(root0 - side, top, root0 + side, (bdir + Vector3.up).normalized, col, 0f);
                            mb.Tri(root0 + side, top, root0 - side, (-bdir + Vector3.up).normalized, col, 0f);
                        }
                    }
                }
            }
            Material[] mats;
            var mesh = mb.ToMesh(null, "Mechones", out mats);
            mats = VertexColorMerge.Apply(mesh, mats);
            var sh = Shader.Find("Mineros/GrassWind");
            if (sh != null) for (int i = 0; i < mats.Length; i++) mats[i] = new Material(mats[i]) { shader = sh, name = "PastoViento" };
            var r = IslandArt.MakeRenderer(root, "Mechones", mesh, mats, false);
            r.receiveShadows = true;
            tufts = r.transform;
        }

        static void MakeLampMeshes()
        {
            var mb = new MeshBuilder();
            Color iron = IslandArt.H("3b3446"), wood = IslandArt.WoodD;
            mb.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.26f, 0.12f, 0.26f), IslandArt.StoneD, 0f);
            mb.Box(new Vector3(0f, 0.65f, 0f), new Vector3(0.09f, 1.15f, 0.09f), wood, 0f);
            mb.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.3f, 0.05f, 0.3f), iron, 0f);
            mb.Box(new Vector3(0f, 1.48f, 0f), new Vector3(0.34f, 0.08f, 0.34f), iron, 0f);
            mb.Octa(new Vector3(0f, 1.6f, 0f), new Vector3(0.2f, 0.14f, 0.2f), iron, 0f);
            lampPole = mb.ToMesh(null, "Farol", out lampPoleMats);
            lampPoleMats = VertexColorMerge.Apply(lampPole, lampPoleMats);
            var gb = new MeshBuilder();
            gb.Box(new Vector3(0f, 1.34f, 0f), new Vector3(0.24f, 0.24f, 0.24f), IslandArt.H("fff1c4"), 0.1f);
            lampGlass = gb.ToMesh(null, "FarolVidrio", out lampGlassMats);
            lampGlassMats = VertexColorMerge.Apply(lampGlass, lampGlassMats);
        }

        // ------------------------------------------------------------ luz del dia
        void UpdateWorld(float dt)
        {
            ApplyLight(false);
            float night = Isl.Night;
            if (night > 0.5f && nightSince < 0f) { nightSince = Time.time; OnNightFall(); }
            if (night < 0.5f) nightSince = -1f;
            // faroles: se encienden de a uno desde la plaza hacia afuera
            foreach (var l in lamps)
            {
                bool on = nightSince >= 0f && Time.time - nightSince > l.Delay || night > 0.5f && nightSince < 0f;
                if (night < 0.5f) on = false;
                float k = Mathf.MoveTowards(l.K, on ? 1f : 0f, dt * 3f);
                if (k > 0f && l.K <= 0f && Sound != null && Sound.Zoom01 > 0.5f) Sfx.Play("tick", -24f, 1.6f);
                l.K = k;
                if (l.Halo.enabled != k > 0.01f) { l.Halo.enabled = k > 0.01f; l.Pool.enabled = k > 0.01f; }
                if (k > 0.01f)
                {
                    IslandFxKit.Face(l.Halo.transform, Cam);
                    float flick = 1f + Mathf.Sin(Time.time * 9f + l.Delay * 13f) * 0.04f;
                    IslandFxKit.Tint(l.Halo, new Color(1f, 0.78f, 0.38f, 0.85f * k * flick));
                    IslandFxKit.Tint(l.Pool, new Color(1f, 0.75f, 0.35f, 0.45f * k * flick));
                }
                var mpb = new MaterialPropertyBlock();
                l.Glass.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.35f) * (k * 1.4f));
                l.Glass.SetPropertyBlock(mpb);
            }
            // ventanas encendidas: halo calido delante de cada edificio
            foreach (var v in plots)
            {
                bool want = v.Body != null && night > 0.3f && v.Root.gameObject.activeSelf && v.P.Building != (int)BKind.Dock;
                MeshRenderer h;
                windowHalos.TryGetValue(v.P.Id, out h);
                if (want && h == null)
                {
                    h = IslandFxKit.Halo(v.Root, "Ventanas", 2.6f);
                    windowHalos[v.P.Id] = h;
                }
                if (h == null) continue;
                h.enabled = want;
                if (!want) continue;
                h.transform.position = v.Root.position + Vector3.up * (v.Height * 0.4f) - Cam.transform.forward * 1.2f;
                IslandFxKit.Face(h.transform, Cam);
                h.transform.localScale = Vector3.one * (1.6f + v.Height * 0.5f);
                IslandFxKit.Tint(h, new Color(1f, 0.72f, 0.35f, 0.45f * Mathf.Clamp01((night - 0.3f) / 0.4f)));
            }
            // luciernagas de noche
            var em = fireflies.emission;
            em.rateOverTime = night > 0.6f && Isl.Sky != Weather.Rain && Isl.Sky != Weather.Storm ? 9f : 0f;
            UpdateBeam(night);
            UpdateCrystals();
            if (Sound != null) Sound.Night = night;
        }

        void ApplyLight(bool force)
        {
            float night = Isl.Night, dusk = Isl.Dusk;
            float storm = Isl.Sky == Weather.Storm ? 1f : Isl.Sky == Weather.Rain ? 0.6f : 0f;
            weatherDim = Mathf.MoveTowards(weatherDim, storm, Time.deltaTime * 0.3f);
            if (force) weatherDim = storm;
            Color Mix(Color d, Color k, Color n) { return Color.Lerp(Color.Lerp(d, k, dusk), n, night); }
            if (sun != null)
            {
                sun.color = Color.Lerp(Mix(SunDay, SunDusk, SunNight), new Color(0.75f, 0.8f, 0.9f), weatherDim * 0.6f);
                sun.intensity = Mathf.Lerp(1.08f, 0.58f, night) * (1f - 0.35f * weatherDim) * (1f - 0.12f * dusk);
                sun.shadowStrength = Mathf.Lerp(0.5f, 0.32f, Mathf.Max(night, weatherDim));
                // el sol baja al atardecer (sombras largas); de noche la "luna" viene del otro lado
                float elev = Mathf.Lerp(55f, 24f, dusk);
                float yaw = Mathf.Lerp(150f, 115f, dusk) + night * 60f;
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(elev, 50f, night), yaw, 0f);
            }
            RenderSettings.ambientSkyColor = Color.Lerp(Mix(SkyDay, SkyDusk, SkyNight), SkyNight * 1.6f, weatherDim * 0.25f);
            RenderSettings.ambientEquatorColor = Mix(EqDay, EqDusk, EqNight);
            RenderSettings.ambientGroundColor = Mix(GrDay, GrDusk, GrNight);
            envT -= Time.deltaTime;
            if (force || envT <= 0f) { envT = 0.25f; DynamicGI.UpdateEnvironment(); }
            if (post != null)
            {
                post.Warmth = 0.015f + 0.05f * dusk - 0.04f * night;
                post.Vignette = 0.45f + 0.15f * night;
                post.BloomIntensity = 0.45f + 0.3f * night;
                post.Threshold = Mathf.Lerp(0.93f, 0.8f, night);
                post.Saturation = 1.02f - 0.14f * weatherDim + 0.05f * dusk - 0.12f * night;
                post.Tint = Color.Lerp(Color.Lerp(Color.white, new Color(1f, 0.9f, 0.82f), dusk * 0.6f), new Color(0.5f, 0.58f, 0.92f), night);
            }
            if (Ambient != null && Ambient.SeaMat != null)
            {
                Ambient.SeaMat.SetColor("_Shallow", Color.Lerp(Color.Lerp(IslandArt.SeaShallow, new Color(0.55f, 0.6f, 0.72f), dusk * 0.35f), SeaShallowNight, night));
                Ambient.SeaMat.SetColor("_Deep", Color.Lerp(IslandArt.SeaDeep, SeaDeepNight, night));
            }
            Cam.backgroundColor = Color.Lerp(IslandArt.SeaD, SeaDeepNight, night);
        }

        float weatherDim;

        void OnNightFall()
        {
            if (Isl.Stat("nights") == 0) Ui.Toast(Loc.T("Cae la noche: tocá los cristales que brillan"), new Color(0.45f, 0.4f, 0.85f));
            Isl.AddStat("nights", 1);
        }

        void OnMorning()
        {
            Sfx.Play("rooster", -10f);
            foreach (var mv in miners.Values) mv.Celebrate = 0.6f;   // salen a estirarse
            Ui.Toast(Loc.T("¡Buen día!"), Kit3.Yellow);
        }

        // ------------------------------------------------------------ faro
        void UpdateBeam(float night)
        {
            var lh = Isl.Find(BKind.Lighthouse);
            bool want = lh != null && night > 0.05f && lh.BuildT < 0f;
            if (!want) { if (beam != null) beam.gameObject.SetActive(false); return; }
            if (beam == null)
            {
                beam = new GameObject("HazFaro").transform;
                beam.SetParent(root, false);
                for (int i = 0; i < 2; i++)
                {
                    // dos planos cruzados con degradado: el haz se ve desde cualquier lado
                    var go = new GameObject("Plano");
                    go.transform.SetParent(beam, false);
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                    go.AddComponent<MeshFilter>().sharedMesh = BeamMesh();
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = IslandFxKit.Additive();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            beam.gameObject.SetActive(true);
            var v = plots[lh.Id];
            beam.position = v.Root.position + Vector3.up * (v.Height * 0.86f);
            beam.rotation = Quaternion.Euler(8f, Time.time * 40f, 0f);
            foreach (Transform c in beam) IslandFxKit.Tint(c.GetComponent<MeshRenderer>(), new Color(1f, 0.92f, 0.6f, 0.3f * night));
        }

        static Mesh beamMesh;

        static Mesh BeamMesh()
        {
            if (beamMesh != null) return beamMesh;
            beamMesh = new Mesh { name = "Haz" };
            // abanico largo: brillante en la punta del faro, se apaga a lo lejos (uv.x del punto suave en el centro)
            beamMesh.vertices = new[] { new Vector3(0, -0.1f, 0), new Vector3(0, 0.1f, 0), new Vector3(0, 2.2f, 22f), new Vector3(0, -2.2f, 22f) };
            beamMesh.uv = new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.98f), new Vector2(0.5f, 0.98f) };
            beamMesh.colors = new[] { Color.white, Color.white, new Color(1, 1, 1, 0.15f), new Color(1, 1, 1, 0.15f) };
            beamMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            beamMesh.RecalculateBounds();
            return beamMesh;
        }

        // ------------------------------------------------------------ cristales nocturnos
        void UpdateCrystals()
        {
            foreach (var v in ores.Values)
            {
                if (!v.O.NightCrystal || v.Rend == null) continue;
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 3f + v.O.Id);
                var mpb = new MaterialPropertyBlock();
                v.Rend.GetPropertyBlock(mpb);
                if (v.Flash <= 0f) mpb.SetColor("_EmissionColor", new Color(0.8f, 0.5f, 1f) * (1.3f * pulse));
                v.Rend.SetPropertyBlock(mpb);
                if (v.Aura == null)
                {
                    v.Aura = FxApi.Attach("aura", v.T, new Color(0.7f, 0.45f, 1f), 0.9f);
                    var halo = IslandFxKit.Halo(v.T, "HaloCristal", 2.2f);
                    halo.transform.localPosition = Vector3.up * 0.45f;
                    v.Halo = halo;
                }
                if (v.Halo != null)
                {
                    IslandFxKit.Face(v.Halo.transform, Cam);
                    IslandFxKit.Tint(v.Halo, new Color(0.75f, 0.5f, 1f, 0.55f * pulse));
                }
                if (Random.value < Time.deltaTime * 2.5f)
                    FxApi.Play("glint", v.T.position + Vector3.up * Random.Range(0.3f, 0.9f) + Random.insideUnitSphere * 0.3f, new Color(0.85f, 0.65f, 1f), 0.8f);
            }
        }

        void OnOreFaded(Ore o)
        {
            OreView v;
            if (!ores.TryGetValue(o.Id, out v)) return;
            FxApi.Play("sparkle", v.T.position + Vector3.up * 0.4f, new Color(0.8f, 0.6f, 1f), 1f);
            Destroy(v.T.gameObject);
            ores.Remove(o.Id);
        }
    }
}
