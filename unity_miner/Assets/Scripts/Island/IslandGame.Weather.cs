using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Clima (biblia 3.7): lluvia (gotas que siguen a la camara, salpicaduras, charcos en los caminos y flores que crecen
    /// al terminar), tormenta (cielo oscuro y rayos que convierten una veta en cristal: destello, sin temblor de pantalla,
    /// trueno con retraso), lluvia de meteoritos (vetas que caen del cielo) y arcoiris al final con +50 % un rato.
    /// </summary>
    public sealed partial class IslandGame
    {
        ParticleSystem rain;
        float splashT, puddleT;
        sealed class Puddle { public Transform T; public MeshRenderer R; public float K, Size; }
        readonly List<Puddle> puddles = new List<Puddle>();
        Transform rainbow;
        float rainbowK;

        void InitWeather()
        {
            rain = IslandFxKit.System(transform, "Lluvia", IslandFxKit.Alpha(), 700);
            var m = rain.main;
            m.startLifetime = 0.9f;
            m.startSpeed = 0f;
            m.startSize3D = true;
            m.startSizeX = 0.05f; m.startSizeY = 0.7f; m.startSizeZ = 0.05f;
            m.startColor = new Color(0.82f, 0.9f, 1f, 0.55f);
            var vel = rain.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-2.5f); vel.y = new ParticleSystem.MinMaxCurve(-24f); vel.z = new ParticleSystem.MinMaxCurve(-1f);
            var sh = rain.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(34f, 0.1f, 34f);
            var r = rain.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2.5f;
            r.velocityScale = 0.03f;
            var em = rain.emission;
            em.rateOverTime = 0f;
            rain.Play();
            Isl.WeatherChanged += OnWeather;
            Isl.Lightning += OnLightning;
            Isl.RainbowStarted += OnRainbow;
        }

        Vector3 ViewCenter() { var c = ScreenToGround(new Vector2(Cam.pixelWidth * 0.5f, Cam.pixelHeight * 0.5f)); c.y = 0f; return c; }

        void UpdateWeather(float dt)
        {
            var w = Isl.Sky;
            bool wet = w == Weather.Rain || w == Weather.Storm;
            float rate = w == Weather.Storm ? 520f : w == Weather.Rain ? 330f : 0f;
            var em = rain.emission;
            em.rateOverTime = rate;
            rain.transform.position = ViewCenter() + Vector3.up * 13f;
            if (Sound != null) Sound.Rain = Mathf.MoveTowards(Sound.Rain, wet ? (w == Weather.Storm ? 1f : 0.75f) : 0f, dt * 0.4f);
            // salpicaduras: anillitos en el suelo visible
            if (wet)
            {
                splashT -= dt;
                if (splashT <= 0f)
                {
                    splashT = 0.08f;
                    Vector3 p = ViewCenter() + new Vector3(Random.Range(-9f, 9f), 0.05f, Random.Range(-9f, 9f));
                    if (p.magnitude < Isl.Radius) FxApi.Play("ring", p, new Color(0.85f, 0.92f, 1f), 0.35f);
                }
                // charcos que crecen en los caminos
                puddleT -= dt;
                if (puddleT <= 0f && puddles.Count < 9)
                {
                    puddleT = 2.5f;
                    var path = Isl.Paths[Random.Range(0, Isl.Paths.Count)];
                    if (Isl.Plots[path.Plot].Ring <= Isl.Expand)
                    {
                        int i = Random.Range(2, path.X.Length - 2);
                        var t = IslandArt.Blob(root, 1f, 0.4f);
                        t.position = new Vector3(path.X[i], 0.035f, path.Z[i]);
                        puddles.Add(new Puddle { T = t, R = t.GetComponent<MeshRenderer>(), Size = Random.Range(0.55f, 0.9f) });
                    }
                }
            }
            for (int i = puddles.Count - 1; i >= 0; i--)
            {
                var p = puddles[i];
                p.K = Mathf.MoveTowards(p.K, wet ? 1f : 0f, dt * (wet ? 0.12f : 0.05f));
                p.T.localScale = new Vector3(p.Size * (0.4f + 0.6f * p.K) * 1.3f, 1f, p.Size * (0.4f + 0.6f * p.K));
                var mpb = new MaterialPropertyBlock();
                p.R.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", new Color(0.32f, 0.45f, 0.62f, 0.45f * p.K));
                p.R.SetPropertyBlock(mpb);
                if (!wet && p.K <= 0f) { Destroy(p.T.gameObject); puddles.RemoveAt(i); }
            }
            UpdateRainbow(dt);
        }

        void OnWeather(Weather w)
        {
            switch (w)
            {
                case Weather.Rain: Ui.Toast(Loc.T("Empieza a llover…"), new Color(0.45f, 0.6f, 0.85f)); break;
                case Weather.Storm: Ui.Toast(Loc.T("¡Tormenta! Los rayos convierten vetas en cristal"), new Color(0.35f, 0.35f, 0.6f)); Sfx.PlayLater("thunder", 0.4f, -10f, 0.8f); break;
                case Weather.Meteors: Ui.Toast(Loc.T("¡Lluvia de meteoritos! Mirá el cielo"), Kit3.Yellow); Sfx.Play("event_start", -4f); break;
                case Weather.Clear:
                    // despues de la lluvia brotan flores en el pasto
                    for (int i = 0; i < 7; i++)
                    {
                        float a = Random.value * Mathf.PI * 2f, d = Random.Range(2f, Isl.Radius - 1.5f);
                        float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                        if (Isl.FreeSpot(x, z, 0.4f)) FlowerAt(new Vector3(x, 0f, z), i * 0.35f);
                    }
                    break;
            }
        }

        /// <summary>Rayo: linea quebrada del cielo a la veta, destello de pantalla corto, la veta pasa a cristal.</summary>
        void OnLightning(Ore o)
        {
            OreView v;
            if (!ores.TryGetValue(o.Id, out v)) return;
            Vector3 to = v.T.position + Vector3.up * 0.4f;
            StartCoroutine(Bolt(to));
            Ui.Flash(new Color(0.9f, 0.95f, 1f), 0.18f);
            Sfx.Play("thunder", -4f, Random.Range(0.9f, 1.1f));
            Juice.Vibrate(40);
            FxApi.Play("meteor_impact", to, new Color(0.7f, 0.85f, 1f), 1.2f);
            FxApi.Play("ring", new Vector3(o.X, 0.05f, o.Z), new Color(0.75f, 0.85f, 1f), 2.4f);
            // la veta se reconstruye como cristal (con un destello)
            RebuildOre(v);
            Ui.Popup(to + Vector3.up * 1.2f, Loc.T("¡Cristal!"), new Color(0.85f, 0.65f, 1f), 30);
        }

        void RebuildOre(OreView v)
        {
            if (v.Vis != null) Destroy(v.Vis.gameObject);
            Material[] mats;
            var mesh = IslandArt.OreMesh(v.O.Kind, v.O.Id, out mats);
            var r = IslandArt.MakeRenderer(v.T, "Vis", mesh, mats);
            r.transform.localRotation = Quaternion.Euler(0, (v.O.Id * 73) % 360, 0);
            v.Vis = r.transform;
            v.Rend = r;
            v.Squash = 1f;
            v.Flash = 0.25f;
            FxApi.Play("gem_sparkle", v.T.position + Vector3.up * 0.5f, IslandArt.OreCol[v.O.Kind], 1.4f);
        }

        System.Collections.IEnumerator Bolt(Vector3 to)
        {
            var go = new GameObject("Rayo");
            go.transform.SetParent(root, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = IslandFxKit.Additive();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mesh = new Mesh { name = "Rayo" };
            mf.sharedMesh = mesh;
            Vector3 from = to + new Vector3(Random.Range(-3f, 3f), 22f, Random.Range(-3f, 3f));
            for (float t = 0f; t < 0.32f; t += Time.deltaTime)
            {
                // se redibuja quebrado cada pocos cuadros (titila)
                BoltMesh(mesh, from, to, 0.35f * (1f - t / 0.32f) + 0.08f);
                IslandFxKit.Tint(mr, new Color(0.85f, 0.92f, 1f, (Mathf.Repeat(t * 30f, 1f) > 0.3f ? 1f : 0.4f) * (1f - t / 0.32f)));
                yield return null;
            }
            Destroy(mesh);
            Destroy(go);
        }

        void BoltMesh(Mesh m, Vector3 a, Vector3 b, float width)
        {
            const int N = 12;
            var v = new Vector3[(N + 1) * 2]; var uv = new Vector2[v.Length]; var c = new Color[v.Length];
            var tri = new int[N * 6];
            Vector3 side = Vector3.Cross(Cam.transform.forward, (b - a).normalized).normalized;
            for (int i = 0; i <= N; i++)
            {
                float k = i / (float)N;
                Vector3 p = Vector3.Lerp(a, b, k);
                if (i > 0 && i < N) p += new Vector3(Random.Range(-0.9f, 0.9f), 0f, Random.Range(-0.9f, 0.9f)) * (1f - Mathf.Abs(k - 0.5f));
                v[i * 2] = p - side * width; v[i * 2 + 1] = p + side * width;
                uv[i * 2] = new Vector2(0f, 0.5f); uv[i * 2 + 1] = new Vector2(1f, 0.5f);
                c[i * 2] = c[i * 2 + 1] = Color.white;
                if (i < N)
                {
                    int o = i * 6, q = i * 2;
                    tri[o] = q; tri[o + 1] = q + 1; tri[o + 2] = q + 2; tri[o + 3] = q + 1; tri[o + 4] = q + 3; tri[o + 5] = q + 2;
                }
            }
            m.Clear();
            m.vertices = v; m.uv = uv; m.colors = c; m.triangles = tri;
            m.RecalculateBounds();
        }

        // ------------------------------------------------------------ arcoiris
        void OnRainbow()
        {
            Ui.Toast(Loc.T("¡Arcoíris! Todo vale +50 % por 2 minutos"), new Color(0.95f, 0.55f, 0.75f));
            Sfx.Play("chimes", -6f);
            Sfx.PlayLater("goal", 0.6f, -6f);
        }

        void UpdateRainbow(float dt)
        {
            bool want = Isl.RainbowT > 0f;
            rainbowK = Mathf.MoveTowards(rainbowK, want ? 1f : 0f, dt * 0.35f);
            if (rainbowK <= 0f) { if (rainbow != null) rainbow.gameObject.SetActive(false); return; }
            if (rainbow == null) rainbow = MakeRainbow();
            rainbow.gameObject.SetActive(true);
            // de pie detras de la isla (del lado opuesto a la camara), mirando a la camara
            Vector3 fwd = Cam.transform.forward; fwd.y = 0f; fwd.Normalize();
            rainbow.position = fwd * 5f + Vector3.down * 2f;   // sobre la mitad de atras de la isla: se ve entero en pantalla
            rainbow.rotation = Quaternion.LookRotation(fwd);
            IslandFxKit.Tint(rainbow.GetComponent<MeshRenderer>(), new Color(1f, 1f, 1f, 0.85f * rainbowK));
        }

        Transform MakeRainbow()
        {
            var go = new GameObject("Arcoiris");
            go.transform.SetParent(root, false);
            var mesh = new Mesh { name = "Arcoiris" };
            Color[] bands = { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.6f, 0.25f), new Color(1f, 0.92f, 0.3f), new Color(0.4f, 0.9f, 0.4f), new Color(0.35f, 0.65f, 1f), new Color(0.65f, 0.45f, 1f) };
            const int Seg = 48;
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var t = new List<int>();
            float r0 = 10f, bw = 0.62f;
            for (int b = 0; b <= bands.Length; b++)
            {
                float r = r0 - b * bw;
                Color col = b < bands.Length ? bands[b] : bands[bands.Length - 1];
                if (b == 0 || b == bands.Length) col.a = 0f;   // bordes suaves
                for (int s = 0; s <= Seg; s++)
                {
                    float a = Mathf.PI * s / Seg;
                    v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
                    c.Add(col);
                    uv.Add(new Vector2(0.5f, 0.5f));
                }
            }
            for (int b = 0; b < bands.Length; b++)
                for (int s = 0; s < Seg; s++)
                {
                    int i = b * (Seg + 1) + s, j = i + Seg + 1;
                    t.Add(i); t.Add(j); t.Add(i + 1); t.Add(i + 1); t.Add(j); t.Add(j + 1);
                }
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(IslandFxKit.Alpha()) { mainTexture = IslandFxKit.White(), name = "Arcoiris" };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
