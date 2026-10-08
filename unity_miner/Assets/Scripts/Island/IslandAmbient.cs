using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.World;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.IslandView
{
    /// <summary>
    /// Vida alrededor de la isla: mar con olas por vertices, espuma que late en la orilla, nubes que pasan sobre el mar,
    /// sombras de nubes que cruzan la isla, mariposas, gaviotas, el barco comprador y el sonido de fondo (olas, aves).
    /// </summary>
    public sealed class IslandAmbient : MonoBehaviour
    {
        IslandGame game;
        Transform root, foam;
        Mesh sea;
        Material seaMat;
        float seaT, radius;

        sealed class Drifter { public Transform T; public Vector3 Dir; public float Speed, Phase, Lane, Vis = 1f, Scale = 1f; public bool Shadow; public Renderer R; }
        readonly List<Drifter> clouds = new List<Drifter>();
        readonly List<Drifter> flyers = new List<Drifter>();   // mariposas
        readonly List<Drifter> gulls = new List<Drifter>();
        float gullT = 6f;

        // barco
        Transform boat;
        Vector3 boatFrom, boatTo;
        float boatT = -1f, boatDur = 6f;
        bool boatLeaving;
        public Material SeaMat { get { return seaMat; } }
        public bool BoatDocked { get { return boat != null && boat.gameObject.activeSelf && boatT >= boatDur && !boatLeaving; } }
        public Vector3 BoatPos { get { return boat != null ? boat.position : Vector3.zero; } }
        public bool BoatVisible { get { return boat != null && boat.gameObject.activeSelf; } }

        public void Init(IslandGame g, Transform parent)
        {
            game = g;
            root = new GameObject("Ambiente").transform;
            root.SetParent(parent, false);
            Material sm;
            sea = IslandArt.SeaGrid(80, 130f, g.Isl.Radius + 2f, out sm);
            seaMat = sm;
            var sr = IslandArt.MakeRenderer(root, "Mar", sea, new[] { sm }, false);
            sr.receiveShadows = true;
            for (int i = 0; i < 6; i++) clouds.Add(MakeCloud(i, false));
            for (int i = 0; i < 3; i++) clouds.Add(MakeCloud(10 + i, true));
            for (int i = 0; i < 5; i++) flyers.Add(MakeButterfly(i));
            for (int i = 0; i < 3; i++) gulls.Add(MakeGull(i));
            Material[] bm;
            var bmesh = IslandArt.Boat(out bm);
            boat = IslandArt.MakeRenderer(root, "Barco", bmesh, bm).transform;
            boat.gameObject.SetActive(false);
            Rebuild(g.Isl.Radius);
            if (g.Isl.CurShip != null && !g.Isl.CurShip.Done) ShipCome(g.Isl.CurShip, true);
            if (g.Isl.BalloonHere) { BalloonCome(); balT = balDur; }
        }

        public void Rebuild(float r)
        {
            radius = r;
            // la espuma la dibuja el shader del mar; el anillo de malla queda solo si el shader no esta
            if (foam != null) Destroy(foam.gameObject);
            if (seaMat == null || seaMat.shader.name != "Mineros/IslandSea") foam = IslandArt.FoamRing(root, r);
            // el shader del mar sigue la costa (espuma y color) con el radio actual
            if (seaMat != null) seaMat.SetFloat("_ShoreR", r);
        }

        // ------------------------------------------------------------ armado de cosas que se mueven
        Drifter MakeCloud(int seed, bool shadowOnly)
        {
            Material[] mats;
            var mesh = IslandArt.Cloud(seed + 3, out mats);
            var r = IslandArt.MakeRenderer(root, shadowOnly ? "SombraNube" : "Nube", mesh, mats);
            r.receiveShadows = false;
            // la sombra la dan solo las nubes-sombra (nunca se apagan): las visibles, que se esconden al acercar, no proyectan
            // (antes su sombra desaparecia de golpe con el zoom; auditoria final)
            r.shadowCastingMode = shadowOnly ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.Off;
            var d = new Drifter
            {
                T = r.transform, Shadow = shadowOnly, R = r,
                Dir = new Vector3(1f, 0f, 0.35f).normalized,
                Speed = 0.5f + (seed % 3) * 0.18f,
                Phase = seed * 37f % 100f,
                Lane = shadowOnly ? (seed - 11) * 7f : (seed % 2 == 0 ? 1f : -1f) * (0.5f + seed * 1.7f),   // visibles: separacion extra fuera de la isla (ver Update)
            };
            d.Scale = shadowOnly ? 2.2f : 1.3f + (seed % 3) * 0.3f;
            r.transform.localScale = Vector3.one * d.Scale;
            return d;
        }

        static Mesh butterflyMesh, gullMesh;

        /// <summary>Malla de la gaviota (la reusa la gaviota ladrona).</summary>
        public Mesh GullMesh(out Material[] mats) { mats = gullMats; return gullMesh; }
        static Material[] butterflyMats, gullMats;

        Drifter MakeButterfly(int i)
        {
            if (butterflyMesh == null)
            {
                var mb = new MeshBuilder();
                Color c = i % 2 == 0 ? Biomes.H("ffd84a") : Biomes.H("ff9ad5");
                Wing(mb, 1f, c); Wing(mb, -1f, c);
                butterflyMesh = mb.ToMesh(null, "Mariposa", out butterflyMats);
                butterflyMats = VertexColorMerge.Apply(butterflyMesh, butterflyMats);
            }
            var r = IslandArt.MakeRenderer(root, "Mariposa", butterflyMesh, butterflyMats, false);
            return new Drifter { T = r.transform, Phase = i * 1.7f, Speed = 0.9f + i * 0.1f, Lane = 2f + i * 1.6f };
        }

        /// <summary>Ala doble cara (dos triangulos en direcciones opuestas) del lado `side`.</summary>
        static void Wing(MeshBuilder mb, float side, Color c)
        {
            Vector3 a = Vector3.zero, b = new Vector3(side * 0.22f, 0f, 0.12f), d = new Vector3(side * 0.18f, 0f, -0.14f);
            mb.Tri(a, b, d, Vector3.up, c, 0.25f);
            mb.Tri(a, d, b, Vector3.down, c, 0.25f);
        }

        Drifter MakeGull(int i)
        {
            if (gullMesh == null)
            {
                var mb = new MeshBuilder();
                Color w = Color.white, g = Biomes.H("c9d2dc");
                mb.Tri(Vector3.zero, new Vector3(0.7f, 0.18f, -0.15f), new Vector3(0.1f, 0f, -0.3f), Vector3.up, w, 0.2f);
                mb.Tri(Vector3.zero, new Vector3(0.1f, 0f, -0.3f), new Vector3(0.7f, 0.18f, -0.15f), Vector3.down, g, 0f);
                mb.Tri(Vector3.zero, new Vector3(-0.1f, 0f, -0.3f), new Vector3(-0.7f, 0.18f, -0.15f), Vector3.up, w, 0.2f);
                mb.Tri(Vector3.zero, new Vector3(-0.7f, 0.18f, -0.15f), new Vector3(-0.1f, 0f, -0.3f), Vector3.down, g, 0f);
                mb.Octa(new Vector3(0, 0, -0.1f), new Vector3(0.12f, 0.1f, 0.42f), w, 0.2f);
                gullMesh = mb.ToMesh(null, "Gaviota", out gullMats);
                gullMats = VertexColorMerge.Apply(gullMesh, gullMats);
            }
            var r = IslandArt.MakeRenderer(root, "Gaviota", gullMesh, gullMats);
            return new Drifter { T = r.transform, Phase = i * 2.1f, Speed = 0.22f + i * 0.04f, Lane = 0f };
        }

        // ------------------------------------------------------------ viento: arboles que se mecen y hojas que caen
        readonly Dictionary<Transform, Quaternion> baseRot = new Dictionary<Transform, Quaternion>();
        List<Transform> swayList;
        ParticleSystem leaves;
        float leafT = 2f, gust;

        readonly Dictionary<Transform, Vector2> shakes = new Dictionary<Transform, Vector2>();   // (grados, inicio)

        /// <summary>Sacude los arboles y arbustos cerca de `pos` (onda que se propaga). Devuelve si toco alguno.</summary>
        public bool ShakeNear(Vector3 pos, float radius, float degrees)
        {
            var list = game.Swayers;
            if (list == null) return false;
            bool any = false;
            float now = Time.time;
            foreach (var tr in list)
            {
                if (tr == null) continue;
                float d = Vector2.Distance(new Vector2(tr.position.x, tr.position.z), new Vector2(pos.x, pos.z));
                if (d > radius) continue;
                shakes[tr] = new Vector2(degrees * (1f - d / radius * 0.6f), now + d * 0.05f);
                any = true;
            }
            return any;
        }

        void UpdateSway(float dt, float t)
        {
            var list = game.Swayers;
            if (list == null) return;
            if (list != swayList) { baseRot.Clear(); swayList = list; }   // la isla se rearmo (expansion)
            // rafagas: el viento sube y baja (mas fuerte con tormenta)
            float storm = game.Isl.Sky == Weather.Storm ? 1f : game.Isl.Sky == Weather.Rain ? 0.5f : 0f;
            float target = 0.35f + 0.65f * Mathf.PerlinNoise(t * 0.12f, 0.3f) + storm * 1.2f;
            gust = Mathf.Lerp(gust, target, 1f - Mathf.Exp(-dt * 1.5f));
            Shader.SetGlobalFloat("_IslandWind", gust);   // el pasto de los caminos se mece en el shader
            foreach (var tr in list)
            {
                if (tr == null) continue;
                Quaternion b;
                if (!baseRot.TryGetValue(tr, out b)) { b = tr.localRotation; baseRot[tr] = b; }
                Vector3 p = tr.localPosition;
                float ph = p.x * 0.35f + p.z * 0.27f;
                float ax = Mathf.Sin(t * 1.4f + ph) * 2.2f * gust, az = Mathf.Cos(t * 1.1f + ph * 1.3f) * 1.6f * gust;
                Vector2 pk;
                if (shakes.TryGetValue(tr, out pk))
                {
                    // sacudon amortiguado (tocarlo o un edificio que cae cerca): la onda llega con retraso por distancia
                    float e = t - pk.y;
                    if (e > 1.4f) shakes.Remove(tr);
                    else if (e > 0f) { float w = pk.x * Mathf.Exp(-e * 3.2f) * Mathf.Sin(e * 22f); ax += w; az += w * 0.6f; }
                }
                tr.localRotation = Quaternion.Euler(ax, 0f, az) * b;
            }
            // de vez en cuando cae una hoja de algun arbol
            leafT -= dt * (0.6f + gust);
            if (leafT <= 0f && list.Count > 0)
            {
                leafT = 1.6f + Random.value * 2.5f;
                if (leaves == null) MakeLeaves();
                var tr = list[Random.Range(0, list.Count)];
                if (tr != null)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = tr.position + Vector3.up * Random.Range(1.6f, 2.6f) + Random.insideUnitSphere * 0.5f,
                        velocity = new Vector3(0.6f * gust, -0.35f, 0.3f),
                        startLifetime = 3.5f,
                        startSize = Random.Range(0.14f, 0.2f),
                        startColor = Random.value < 0.3f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.45f, 0.75f, 0.25f),
                        rotation = Random.value * 360f,
                    };
                    leaves.Emit(ep, 1);
                }
            }
        }

        void MakeLeaves()
        {
            var mat = new Material(IslandFxKit.Alpha()) { name = "Hojas", mainTexture = LeafTex() };
            leaves = IslandFxKit.System(root, "Hojas", mat, 30);
            var m = leaves.main;
            m.gravityModifier = 0.05f;
            var n = leaves.noise;
            n.enabled = true; n.strength = 0.8f; n.frequency = 0.6f;
            var rot = leaves.rotationOverLifetime;
            rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var col = leaves.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var em = leaves.emission; em.rateOverTime = 0f;
            leaves.Play();
        }

        static Texture2D leafTex;

        /// <summary>Hojita (punta a punta, borde suave) para las particulas.</summary>
        static Texture2D LeafTex()
        {
            if (leafTex != null) return leafTex;
            const int N = 64;
            leafTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Hoja" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float w = 0.55f * (1f - v * v);                     // ancho de la hoja segun la altura
                    float d = Mathf.Abs(u) - w;
                    float a = Mathf.Clamp01(0.5f - d * N * 0.5f);
                    float vein = Mathf.Abs(u) < 0.04f ? 0.8f : 1f;
                    px[y * N + x] = new Color32((byte)(255 * vein), (byte)(255 * vein), (byte)(255 * vein), (byte)(a * 255));
                }
            leafTex.SetPixels32(px);
            leafTex.Apply(false, true);
            return leafTex;
        }

        // ------------------------------------------------------------ vida en el mar: peces y delfin
        sealed class Jumper { public Transform T; public Vector3 A, B; public float Age = -1f, Dur, H; public bool Splashed; }
        Jumper fish, dolphin;
        float fishT = 5f, dolphinT = 70f;
        static Mesh fishMesh, dolphinMesh;
        static Material[] fishMats, dolphinMats;

        Jumper MakeJumper(bool big)
        {
            if (fishMesh == null)
            {
                var mb = new MeshBuilder();
                mb.Blob(Vector3.zero, new Vector3(0.12f, 0.1f, 0.26f), 1, 4, 0.05f, Biomes.H("ff8a3d"), Biomes.H("ffc46b"), 0.05f, -1f);
                mb.Tri(new Vector3(0, 0, -0.22f), new Vector3(0, 0.12f, -0.38f), new Vector3(0, -0.12f, -0.38f), Vector3.right, Biomes.H("ff7a2e"), 0f);
                mb.Tri(new Vector3(0, 0, -0.22f), new Vector3(0, -0.12f, -0.38f), new Vector3(0, 0.12f, -0.38f), Vector3.left, Biomes.H("ff7a2e"), 0f);
                fishMesh = mb.ToMesh(null, "Pez", out fishMats);
                fishMats = VertexColorMerge.Apply(fishMesh, fishMats);
                var db = new MeshBuilder();
                db.Blob(Vector3.zero, new Vector3(0.3f, 0.28f, 0.9f), 1, 6, 0.04f, Biomes.H("6f8fa8"), Biomes.H("b9cfdc"), 0f, -1f);
                db.Tri(new Vector3(0, 0.2f, 0f), new Vector3(0, 0.55f, -0.25f), new Vector3(0, 0.2f, -0.4f), Vector3.right, Biomes.H("5d7c94"), 0f);
                db.Tri(new Vector3(0, 0.2f, 0f), new Vector3(0, 0.2f, -0.4f), new Vector3(0, 0.55f, -0.25f), Vector3.left, Biomes.H("5d7c94"), 0f);
                db.Tri(new Vector3(0, 0, -0.8f), new Vector3(0.35f, 0, -1.05f), new Vector3(-0.35f, 0, -1.05f), Vector3.up, Biomes.H("5d7c94"), 0f);
                db.Tri(new Vector3(0, 0, -0.8f), new Vector3(-0.35f, 0, -1.05f), new Vector3(0.35f, 0, -1.05f), Vector3.down, Biomes.H("5d7c94"), 0f);
                dolphinMesh = db.ToMesh(null, "Delfin", out dolphinMats);
                dolphinMats = VertexColorMerge.Apply(dolphinMesh, dolphinMats);
            }
            var r = IslandArt.MakeRenderer(root, big ? "Delfin" : "Pez", big ? dolphinMesh : fishMesh, big ? dolphinMats : fishMats, false);
            r.gameObject.SetActive(false);
            return new Jumper { T = r.transform };
        }

        void UpdateSeaLife(float dt)
        {
            if (fish == null) { fish = MakeJumper(false); dolphin = MakeJumper(true); }
            fishT -= dt; dolphinT -= dt;
            if (fishT <= 0f && fish.Age < 0f) { fishT = 4f + Random.value * 6f; Launch(fish, 1.6f, 0.9f, 0.75f); }
            if (dolphinT <= 0f && dolphin.Age < 0f) { dolphinT = 70f + Random.value * 70f; Launch(dolphin, 4.5f, 1.9f, 1.35f); }
            Jump(fish, dt, false);
            Jump(dolphin, dt, true);
        }

        /// <summary>Salto en arco desde el agua cerca de la orilla visible.</summary>
        void Launch(Jumper j, float len, float h, float dur)
        {
            var cam = game.Cam;
            Vector3 c = game.ScreenToGround(new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f)); c.y = 0f;
            Vector3 dir = c.sqrMagnitude > 1f ? c.normalized : Random.insideUnitSphere;
            dir.y = 0f; dir.Normalize();
            float a = Mathf.Atan2(dir.z, dir.x) + Random.Range(-0.7f, 0.7f);
            float r = radius + Random.Range(2.5f, 6f);
            Vector3 p = new Vector3(Mathf.Cos(a) * r, -0.45f, Mathf.Sin(a) * r);
            Vector3 tang = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * (Random.value < 0.5f ? 1f : -1f);
            j.A = p; j.B = p + tang * len; j.H = h; j.Dur = dur; j.Age = 0f; j.Splashed = false;
            j.T.gameObject.SetActive(true);
            Splash(p, len > 3f);
        }

        void Jump(Jumper j, float dt, bool big)
        {
            if (j.Age < 0f) return;
            j.Age += dt;
            float k = j.Age / j.Dur;
            if (k >= 1f)
            {
                j.T.gameObject.SetActive(false);
                j.Age = -1f;
                return;
            }
            Vector3 p = Vector3.Lerp(j.A, j.B, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * j.H);
            Vector3 vel = (j.B - j.A) / j.Dur + Vector3.up * (Mathf.Cos(k * Mathf.PI) * Mathf.PI * j.H / j.Dur);
            j.T.position = p;
            if (vel.sqrMagnitude > 0.01f) j.T.rotation = Quaternion.LookRotation(vel.normalized);
            if (!j.Splashed && k > 0.88f) { j.Splashed = true; Splash(j.B, big); if (big) Sfx.PlayPan("dolphin", -14f, 1f, 0f); }
        }

        void Splash(Vector3 p, bool big)
        {
            FxApi.Play("ring", new Vector3(p.x, -0.4f, p.z), new Color(0.9f, 0.98f, 1f), big ? 1.6f : 0.8f);
            FxApi.Play("bubble", p + Vector3.up * 0.2f, new Color(0.9f, 0.98f, 1f), big ? 1.4f : 0.7f);
            var cam = game.Cam;
            Vector3 vp = cam.WorldToViewportPoint(p);
            if (vp.x < -0.1f || vp.x > 1.1f || vp.y < -0.1f || vp.y > 1.1f) return;
            float z = game.Sound != null ? game.Sound.Zoom01 : 0.5f;
            Sfx.PlayPan("splash", (big ? -12f : -18f) + 6f * z, big ? 0.85f : 1.1f, Mathf.Clamp(vp.x * 2f - 1f, -1f, 1f) * 0.8f);
        }

        // ------------------------------------------------------------ bandada que cruza
        readonly List<Transform> flock = new List<Transform>();
        Vector3 flockFrom, flockTo;
        float flockT = 25f, flockAge = -1f;

        void UpdateFlock(float dt)
        {
            if (flock.Count == 0)
                for (int i = 0; i < 5; i++)
                {
                    var r = IslandArt.MakeRenderer(root, "Pajaro", gullMesh, gullMats, false);
                    r.gameObject.SetActive(false);
                    flock.Add(r.transform);
                }
            if (flockAge < 0f)
            {
                flockT -= dt;
                if (flockT > 0f || game.Isl.IsNight) return;
                flockT = 25f + Random.value * 25f;
                float a = Random.value * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                flockFrom = -d * (radius + 18f) + Vector3.up * 9.5f;
                flockTo = d * (radius + 18f) + Vector3.up * 9.5f;
                flockAge = 0f;
                foreach (var b in flock) b.gameObject.SetActive(true);
                Sfx.Play("wings", -22f);
            }
            flockAge += dt;
            float k = flockAge / 11f;
            Vector3 dir = (flockTo - flockFrom).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < flock.Count; i++)
            {
                // formacion en V
                int row = (i + 1) / 2, s = i % 2 == 0 ? 1 : -1;
                Vector3 off = -dir * row * 0.9f + side * s * row * 0.8f;
                var b = flock[i];
                b.position = Vector3.Lerp(flockFrom, flockTo, k) + off + Vector3.up * Mathf.Sin(Time.time * 2f + i) * 0.15f;
                b.rotation = Quaternion.LookRotation(dir);
                float flap = Mathf.Sin(Time.time * 9f + i * 1.3f);
                b.localScale = new Vector3(0.45f, 0.45f * (1f + flap * 0.8f), 0.45f);
            }
            if (k >= 1f) { flockAge = -1f; foreach (var b in flock) b.gameObject.SetActive(false); }
        }

        // ------------------------------------------------------------ globo del mercader
        Transform balloon;
        Vector3 balFrom, balTo;
        float balT = -1f, balDur = 5f;
        bool balLeaving, balTapped;
        public bool BalloonVisible { get { return balloon != null && balloon.gameObject.activeSelf; } }
        public Vector3 BalloonPos { get { return balloon != null ? balloon.position : Vector3.zero; } }
        static readonly Vector3 Hover = new Vector3(1.8f, 5.2f, -2.4f);

        Transform MakeBalloon()
        {
            Material tm;
            var mesh = IslandArt.TripoModel("balloon", out tm);
            var holder = new GameObject("Globo").transform;
            holder.SetParent(root, false);
            if (mesh != null)
            {
                var r = IslandArt.MakeRenderer(holder, "Modelo", mesh, new[] { tm });
                r.transform.localScale = Vector3.one * 1.5f;
                r.transform.localRotation = Quaternion.Euler(0f, IslandGame.BuildingYaw, 0f);
            }
            else
            {
                var mb = new MeshBuilder();
                mb.Blob(new Vector3(0, 3f, 0), new Vector3(1.6f, 1.9f, 1.6f), 2, 3, 0.02f, Biomes.H("ff5a5f"), Biomes.H("ffd23a"), 0.1f, -1f);
                mb.Box(new Vector3(0, 0.4f, 0), new Vector3(1f, 0.7f, 1f), Biomes.H("a8703f"), 0f);
                Material[] mats;
                var m = mb.ToMesh(null, "Globo", out mats);
                mats = VertexColorMerge.Apply(m, mats);
                IslandArt.MakeRenderer(holder, "Modelo", m, mats);
            }
            holder.gameObject.SetActive(false);
            return holder;
        }

        public void BalloonCome()
        {
            if (balloon == null) balloon = MakeBalloon();
            balFrom = new Vector3(-26f, 11f, 22f);
            balTo = Hover;
            balT = 0f; balDur = 5.5f;
            balLeaving = false;
            balloon.gameObject.SetActive(true);
            balloon.position = balFrom;
        }

        public void BalloonGo(bool tapped)
        {
            if (!BalloonVisible) return;
            balFrom = balloon.position;
            balTo = tapped ? balloon.position + new Vector3(4f, 22f, 6f) : new Vector3(28f, 12f, -24f);
            balT = 0f; balDur = tapped ? 3f : 7f;
            balLeaving = true;
            balTapped = tapped;
            if (!tapped) DropFeather(balloon.position + Vector3.up * 1.5f);   // pista de que se perdio algo
            if (tapped)
            {
                FxApi.Play("confetti", balloon.position + Vector3.up * 2f, default(Color), 2f);
                FxApi.Play("coin_burst", balloon.position + Vector3.up * 1f, default(Color), 1.6f);
            }
        }

        // pluma que cae despacio meciendose cuando el globo se va sin que lo toquen
        Transform feather;
        float featherT = -1f;
        Vector3 featherFrom;

        void DropFeather(Vector3 from)
        {
            if (feather == null)
            {
                var mb = new MeshBuilder();
                Color w = Color.white, g = Biomes.H("d9e4ee");
                mb.Tri(new Vector3(0, 0, -0.35f), new Vector3(0.09f, 0, 0.05f), new Vector3(0, 0, 0.35f), Vector3.up, w, 0.1f);
                mb.Tri(new Vector3(0, 0, -0.35f), new Vector3(0, 0, 0.35f), new Vector3(-0.09f, 0, 0.05f), Vector3.up, g, 0.1f);
                mb.Tri(new Vector3(0, 0, -0.35f), new Vector3(0, 0, 0.35f), new Vector3(0.09f, 0, 0.05f), Vector3.down, g, 0f);
                mb.Tri(new Vector3(0, 0, -0.35f), new Vector3(-0.09f, 0, 0.05f), new Vector3(0, 0, 0.35f), Vector3.down, g, 0f);
                Material[] mats;
                var m = mb.ToMesh(null, "Pluma", out mats);
                mats = VertexColorMerge.Apply(m, mats);
                feather = IslandArt.MakeRenderer(root, "Pluma", m, mats, false).transform;
            }
            feather.gameObject.SetActive(true);
            featherFrom = from;
            featherT = 0f;
        }

        void UpdateFeather(float dt)
        {
            if (featherT < 0f || feather == null) return;
            featherT += dt;
            float y = featherFrom.y - featherT * 0.9f;
            if (y <= 0.05f) { y = 0.05f; if (featherT > 12f) { feather.gameObject.SetActive(false); featherT = -1f; return; } }
            float sw = y > 0.06f ? Mathf.Sin(featherT * 2.2f) : 0f;
            feather.position = new Vector3(featherFrom.x + sw * 0.8f, y, featherFrom.z + featherT * 0.15f);
            feather.rotation = Quaternion.Euler(0f, featherT * 20f, sw * 35f);
        }

        void UpdateBalloon(float dt, float t)
        {
            UpdateFeather(dt);
            if (!BalloonVisible) return;
            balT += dt;
            float k = Mathf.Clamp01(balT / balDur);
            float e = balLeaving ? k * k : 1f - Mathf.Pow(1f - k, 3f);
            Vector3 p = Vector3.Lerp(balFrom, balTo, e);
            p += new Vector3(Mathf.Sin(t * 0.7f) * 0.25f, Mathf.Sin(t * 1.3f) * 0.22f, Mathf.Cos(t * 0.6f) * 0.2f);
            balloon.position = p;
            balloon.rotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 3f, t * 6f, Mathf.Cos(t * 0.9f) * 3f);
            if (balLeaving && k >= 1f) balloon.gameObject.SetActive(false);
            else if (!balLeaving && Random.value < dt * 2f) FxApi.Play("glint", p + Vector3.up * Random.Range(0.5f, 3.5f) + Random.insideUnitSphere * 1.2f, new Color(1f, 0.9f, 0.5f), 0.8f);
        }

        // ------------------------------------------------------------ barco de reclutas
        Transform rboat;
        Vector3 rFrom, rTo, rDir;
        float rT = -1f, rDur = 4.5f;
        bool rLeaving;
        System.Action rArrived;

        /// <summary>Punto de la playa donde bajan los mineros nuevos (del lado de la camara).</summary>
        public Vector3 RecruitShore { get { return rDir * (radius - 0.6f); } }

        public void RecruitCome(System.Action arrived)
        {
            if (rboat == null)
            {
                Material[] bm;
                var bmesh = IslandArt.Boat(out bm);
                rboat = IslandArt.MakeRenderer(root, "BarcoReclutas", bmesh, bm).transform;
            }
            Vector3 f = game.Cam.transform.forward; f.y = 0f; f.Normalize();
            // playa de abajo de la pantalla, un poco corrida para no tapar el muelle
            rDir = Quaternion.Euler(0f, 25f, 0f) * -f;
            rTo = rDir * (radius + 2.6f) + Vector3.up * -0.45f;
            Vector3 side = Vector3.Cross(Vector3.up, rDir);
            rFrom = rTo + rDir * 22f + side * 12f;
            rT = 0f; rDur = 4.5f; rLeaving = false;
            rArrived = arrived;
            rboat.gameObject.SetActive(true);
            rboat.position = rFrom;
            Sfx.Play("horn", -8f, 1.15f);
        }

        public void RecruitGo()
        {
            if (rboat == null || !rboat.gameObject.activeSelf) return;
            Vector3 side = Vector3.Cross(Vector3.up, rDir);
            rFrom = rboat.position;
            rTo = rFrom + rDir * 22f - side * 12f;
            rT = 0f; rDur = 5f; rLeaving = true;
            Sfx.Play("horn", -12f, 1.2f);
        }

        void UpdateRecruitBoat(float dt)
        {
            if (rboat == null || !rboat.gameObject.activeSelf || rT < 0f) return;
            rT += dt;
            float k = Mathf.Clamp01(rT / rDur);
            float e = rLeaving ? k * k : 1f - (1f - k) * (1f - k);
            Vector3 p = Vector3.Lerp(rFrom, rTo, e);
            Vector3 dir = rTo - rFrom; dir.y = 0f;
            rboat.position = new Vector3(p.x, -0.45f + Mathf.Sin(Time.time * 1.7f) * 0.08f, p.z);
            Vector3 face = k < 1f || rLeaving ? dir.normalized : -rDir;
            if (face.sqrMagnitude > 0.01f)
                rboat.rotation = Quaternion.Slerp(rboat.rotation, Quaternion.LookRotation(face) * Quaternion.Euler(Mathf.Sin(Time.time * 1.3f) * 3f, 0f, Mathf.Sin(Time.time * 1.7f) * 4f), 1f - Mathf.Exp(-dt * 3f));
            if (k < 1f && Random.value < dt * 10f) FxApi.Play("bubble", rboat.position - dir.normalized * 2f + Vector3.up * 0.3f, new Color(0.9f, 0.98f, 1f), 0.9f);
            if (!rLeaving && k >= 1f && rArrived != null)
            {
                // atraca con un golpe: espuma y aviso
                FxApi.Play("ring", new Vector3(rboat.position.x, -0.4f, rboat.position.z), new Color(0.9f, 0.98f, 1f), 2f);
                Sfx.Play("thud", -12f, 1.2f);
                var a = rArrived; rArrived = null;
                a();
            }
            if (rLeaving && k >= 1f) { rboat.gameObject.SetActive(false); rT = -1f; }
        }

        /// <summary>Posicion del barco de reclutas (para anclar la interfaz).</summary>
        public Vector3 RecruitBoatPos { get { return rboat != null ? rboat.position : Vector3.zero; } }

        // ------------------------------------------------------------ barco
        Vector3 DockPoint(out Vector3 outward)
        {
            var dock = game.Isl.Find(BKind.Dock);
            Vector3 p = dock != null ? new Vector3(dock.X, 0f, dock.Z) : new Vector3(radius, 0f, -radius * 0.3f);
            outward = new Vector3(p.x, 0f, p.z).normalized;
            if (outward.sqrMagnitude < 0.01f) outward = Vector3.right;
            return outward * (radius + 4.2f) + Vector3.up * -0.45f;
        }

        public void ShipCome(Ship s) { ShipCome(s, false); }

        void ShipCome(Ship s, bool instant)
        {
            Vector3 outward;
            boatTo = DockPoint(out outward);
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            boatFrom = boatTo + outward * 26f + side * 14f;
            boatLeaving = false;
            boatDur = 6f;
            boatT = instant ? boatDur : 0f;
            boat.gameObject.SetActive(true);
            boat.position = instant ? boatTo : boatFrom;
            if (!instant) Sfx.Play("horn", -4f);
        }

        public void ShipGo()
        {
            if (boat == null || !boat.gameObject.activeSelf) return;
            Vector3 outward;
            DockPoint(out outward);
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            boatFrom = boat.position;
            boatTo = boat.position + outward * 26f - side * 14f;
            boatLeaving = true;
            boatT = 0f;
            Sfx.Play("horn", -8f, 1.05f);
        }

        void UpdateBoat(float dt)
        {
            if (boat == null || !boat.gameObject.activeSelf) return;
            boatT += dt;
            float k = Mathf.Clamp01(boatT / boatDur);
            // llega frenando, se va acelerando
            float e = boatLeaving ? k * k : 1f - (1f - k) * (1f - k);
            Vector3 p = Vector3.Lerp(boatFrom, boatTo, e);
            Vector3 dir = boatTo - boatFrom; dir.y = 0f;
            float bob = Mathf.Sin(Time.time * 1.6f) * 0.08f;
            boat.position = new Vector3(p.x, -0.45f + bob, p.z);
            if (dir.sqrMagnitude > 0.01f)
            {
                var face = Quaternion.LookRotation(k < 1f || boatLeaving ? dir.normalized : -DockOutward());
                boat.rotation = Quaternion.Slerp(boat.rotation, face * Quaternion.Euler(Mathf.Sin(Time.time * 1.2f) * 3f, 0f, Mathf.Sin(Time.time * 1.6f) * 4f), 1f - Mathf.Exp(-dt * 3f));
            }
            // estela de espuma mientras navega
            if (k < 1f && Random.value < dt * 10f) FxApi.Play("bubble", boat.position - dir.normalized * 2f + Vector3.up * 0.3f, new Color(0.9f, 0.98f, 1f), 0.9f);
            if (boatLeaving && k >= 1f) boat.gameObject.SetActive(false);
        }

        Vector3 DockOutward() { Vector3 o; DockPoint(out o); return o; }

        // ------------------------------------------------------------ bucle
        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;
            if (foam != null)
            {
                float s = 1f + Mathf.Sin(t * 1.3f) * 0.012f;
                foam.localScale = new Vector3(s, 1f, s);
            }
            float R = radius + 30f;
            // de cerca (o en el Modo Cuartel) las nubes taparian lo que se mira: se esconden (queda su sombra)
            bool near = game != null && (game.ComplexMode || game.Cam.orthographicSize < 10.5f);
            foreach (var c in clouds)
            {
                if (!c.Shadow && c.R != null)
                {
                    // se desvanecen achicandose (0.4 s) en vez de apagarse de golpe
                    c.Vis = Mathf.MoveTowards(c.Vis, near ? 0f : 1f, dt / 0.4f);
                    float e = c.Vis * c.Vis * (3f - 2f * c.Vis);
                    c.T.localScale = Vector3.one * c.Scale * Mathf.Max(0.001f, e);
                    bool on = c.Vis > 0.001f;
                    if (c.R.enabled != on) c.R.enabled = on;
                }
                c.Phase += dt * c.Speed;
                float u = Mathf.Repeat(c.Phase, 100f) / 100f;   // 0..1 a lo largo del carril
                Vector3 side = Vector3.Cross(Vector3.up, c.Dir);
                // las visibles van siempre por el mar (radio actual + margen: a 6.5 de altura la camara inclinada las corre ~6
                // hacia la isla); solo las sombras cruzan por encima
                float lane = c.Shadow ? c.Lane : Mathf.Sign(c.Lane) * (radius + 13f + Mathf.Abs(c.Lane));
                Vector3 p = c.Dir * Mathf.Lerp(-R, R, u) + side * lane;
                c.T.localPosition = new Vector3(p.x, c.Shadow ? 14f : 6.5f, p.z);
            }
            UpdateButterflies(t);
            UpdateGulls(dt, t);
            UpdateBoat(dt);
            UpdateBalloon(dt, t);
            UpdateSway(dt, t);
            UpdateRecruitBoat(dt);
            UpdateSeaLife(dt);
            UpdateFlock(dt);
        }

        void UpdateButterflies(float t)
        {
            for (int i = 0; i < flyers.Count; i++)
            {
                var f = flyers[i];
                float a = t * 0.25f * f.Speed + f.Phase;
                float r = Mathf.Min(f.Lane, radius - 2f);
                Vector3 p = new Vector3(Mathf.Cos(a) * r + Mathf.Sin(a * 2.3f) * 1.4f, 1.2f + Mathf.Sin(t * 2.2f + i) * 0.4f, Mathf.Sin(a * 1.3f) * r);
                Vector3 prev = f.T.localPosition;
                f.T.localPosition = p;
                Vector3 d = p - prev; d.y = 0f;
                float flap = Mathf.Abs(Mathf.Sin(t * 18f + i));
                f.T.localScale = new Vector3(0.25f + flap * 0.9f, 1f, 1f) * 1.4f;
                if (d.sqrMagnitude > 1e-6f) f.T.localRotation = Quaternion.LookRotation(d.normalized);
            }
        }

        void UpdateGulls(float dt, float t)
        {
            for (int i = 0; i < gulls.Count; i++)
            {
                var g = gulls[i];
                float a = t * g.Speed + g.Phase;
                float r = radius + 6f + i * 2.5f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 7.5f + i * 0.6f + Mathf.Sin(t * 0.7f + i) * 0.4f, Mathf.Sin(a) * r);
                g.T.localPosition = p;
                Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                float flap = Mathf.Sin(t * 7f + i * 2f);
                g.T.localRotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, -12f);
                g.T.localScale = new Vector3(0.75f, 0.75f * (1f + flap * 0.8f), 0.75f);
            }
            gullT -= dt;
            if (gullT <= 0f)
            {
                // la gaviota grita desde su lado de la pantalla; mas fuerte si la camara esta cerca
                gullT = 7f + Random.value * 10f;
                var g = gulls[Random.Range(0, gulls.Count)];
                Vector3 sp = game.Cam.WorldToViewportPoint(g.T.position);
                float z = game.Sound != null ? game.Sound.Zoom01 : 0.5f;
                Sfx.PlayPan("gull", -20f + 8f * z, 0.92f + Random.value * 0.16f, Mathf.Clamp(sp.x * 2f - 1f, -1f, 1f) * 0.8f);
            }
        }
    }
}
