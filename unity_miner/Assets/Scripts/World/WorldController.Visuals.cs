using System.Collections.Generic;
using Mineros.Art;
using Mineros.Fx;
using UnityEngine;
using UnityEngine.Rendering;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.World
{
    /// <summary>
    /// Parte visual del mundo que no es ni roca ni minero: cartel y barra del jefe, motas del ambiente (una sola malla),
    /// destellos periodicos de cristales y lava (Fx del kit), tapa voladora del cofre y atajos hacia Juice.
    /// </summary>
    public sealed partial class WorldController
    {
        // ================================================================== atajos de sensacion de juego
        /// <summary>Sacudida de camara con la escala del Godot original (px de pantalla que decaen a 40 px/s).</summary>
        void Shake(float px)
        {
            float amp = px * W.PX * (G != null && G.Eco ? 0.6f : 1f);
            Juice.Shake(amp, Mathf.Clamp(px / 40f, 0.1f, 0.7f));
        }

        // ================================================================== cartel y barra del jefe
        int bossTextIdx = -1;
        Transform bossBar, bossFill;
        Mesh quadMesh;

        Mesh QuadMesh()
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh();
            quadMesh.name = "BarraQuad";
            quadMesh.vertices = new[] { new Vector3(0f, -0.5f, 0f), new Vector3(1f, -0.5f, 0f), new Vector3(1f, 0.5f, 0f), new Vector3(0f, 0.5f, 0f) };
            quadMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quadMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quadMesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        Transform MakeBarPart(Transform parent, string name, Color col, float emis, float z)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(0f, 0f, z);
            g.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = WorldMaterials.Tone(col, emis);
            SetupRend(mr, false);
            mr.receiveShadows = false;
            return g.transform;
        }

        void EnsureBossBar()
        {
            if (bossBar != null) return;
            var rt = new GameObject("BarraJefe").transform;
            rt.SetParent(root, false);
            bossBar = rt;
            const float w = 3.6f, h = 0.25f;
            var frame = MakeBarPart(rt, "Marco", Biomes.WoodD, 0f, 0.02f);
            frame.localPosition = new Vector3(-w * 0.5f - 0.05f, 0f, 0.02f);
            frame.localScale = new Vector3(w + 0.1f, h + 0.1f, 1f);
            var bg = MakeBarPart(rt, "Fondo", Biomes.H("3a2d24"), 0f, 0.01f);
            bg.localPosition = new Vector3(-w * 0.5f, 0f, 0.01f);
            bg.localScale = new Vector3(w, h, 1f);
            bossFill = MakeBarPart(rt, "Relleno", Biomes.H("b06ef0"), 1f, -0.01f);
            bossFill.localPosition = new Vector3(-w * 0.5f, 0f, -0.01f);
            bossFill.localScale = new Vector3(w, h * 0.8f, 1f);
            rt.gameObject.SetActive(false);
        }

        /// <summary>Muestra el cartel "GEODA GIGANTE" y la barra de vida sobre la geoda.</summary>
        void ShowBossLabel()
        {
            EnsureBossBar();
            bossBar.gameObject.SetActive(true);
            if (bossTextIdx >= 0) texts.Kill(bossTextIdx);
            bossTextIdx = -1;
            TrySpawnBossText();
            UpdateBossLabel();
        }

        void TrySpawnBossText()
        {
            if (boss == null) return;
            bossTextIdx = texts.Spawn("GEODA GIGANTE", BossLabelPos(), Vector3.zero, 100000f, 30f, Color.white, 0f, null, 0, false);
        }

        Vector3 BossLabelPos()
        {
            return W.V3(boss.pos, boss.r * 2.9f * boss.gscale) + Cam.transform.up * 0.45f;
        }

        void HideBossLabel()
        {
            if (bossTextIdx >= 0) texts.Kill(bossTextIdx);
            bossTextIdx = -1;
            if (bossBar != null) bossBar.gameObject.SetActive(false);
        }

        void UpdateBossLabel()
        {
            if (boss == null || boss.dead || bossBar == null)
            {
                if (bossBar != null && bossBar.gameObject.activeSelf) bossBar.gameObject.SetActive(false);
                return;
            }
            if (!bossBar.gameObject.activeSelf) bossBar.gameObject.SetActive(true);
            if (bossTextIdx < 0) TrySpawnBossText();
            else texts.SetPos(bossTextIdx, BossLabelPos());
            Transform ct = Cam.transform;
            // la barra va hacia la camara (en ortografica no cambia donde se ve) para no quedar tapada por los cristales
            bossBar.position = W.V3(boss.pos, boss.r * 2.9f * boss.gscale) - ct.forward * 6f;
            bossBar.rotation = ct.rotation;
            float f = boss.maxHp > 0 ? Mathf.Clamp01((float)(boss.hp / boss.maxHp)) : 0f;
            bossFill.gameObject.SetActive(f > 0.02f);
            bossFill.localScale = new Vector3(3.6f * f, 0.2f, 1f);
        }

        // ================================================================== barras de vida de las rocas golpeadas
        sealed class HpBar
        {
            public Transform root, fill;
        }

        readonly List<HpBar> hpBars = new List<HpBar>(16);

        HpBar NewHpBar()
        {
            var b = new HpBar();
            b.root = new GameObject("BarraVida").transform;
            b.root.SetParent(root, false);
            var frame = MakeBarPart(b.root, "Marco", Biomes.WoodD, 0f, 0.02f);
            frame.localPosition = new Vector3(-0.5f - 0.035f, 0f, 0.02f);
            frame.localScale = new Vector3(1.07f, 1f, 1f);
            var bg = MakeBarPart(b.root, "Fondo", Biomes.H("3a2d24"), 0f, 0.01f);
            bg.localPosition = new Vector3(-0.5f, 0f, 0.01f);
            bg.localScale = new Vector3(1f, 0.62f, 1f);
            b.fill = MakeBarPart(b.root, "Relleno", Biomes.H("ff6b4a"), 0.6f, -0.01f);
            b.fill.localPosition = new Vector3(-0.5f, 0f, -0.01f);
            return b;
        }

        /// <summary>Como _draw_hp de Godot: barra roja sobre cada roca danada (no el jefe, que tiene la suya).</summary>
        void UpdateRockBars()
        {
            int n = 0;
            Transform ct = Cam.transform;
            for (int i = 0; i < rocks.Count; i++)
            {
                Rk r = rocks[i];
                if (r.dead || r.kind == 2 || r.hp >= r.maxHp || r.vanish >= 0f) continue;
                if (n >= hpBars.Count) hpBars.Add(NewHpBar());
                HpBar b = hpBars[n++];
                if (!b.root.gameObject.activeSelf) b.root.gameObject.SetActive(true);
                float w = r.r * 1.5f, h = 13f * W.PX;
                b.root.position = W.V3(r.pos, r.r * 1.55f) - ct.forward * 4f;
                b.root.rotation = ct.rotation;
                b.root.localScale = new Vector3(w, h, 1f);
                float f = r.maxHp > 0 ? Mathf.Clamp01((float)(r.hp / r.maxHp)) : 0f;
                b.fill.gameObject.SetActive(f > 0.02f);
                b.fill.localScale = new Vector3(f, 0.62f, 1f);
            }
            for (int i = n; i < hpBars.Count; i++)
                if (hpBars[i].root.gameObject.activeSelf) hpBars[i].root.gameObject.SetActive(false);
        }

        // ================================================================== motas del ambiente (una sola malla)
        sealed class MoteCloud
        {
            public const int Cap = 40;
            public readonly GameObject go;
            public readonly MeshRenderer mr;
            readonly Mesh mesh;
            readonly Vector3[] verts = new Vector3[Cap * 6];
            static readonly Vector3[] Axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            public MoteCloud(Transform parent)
            {
                go = new GameObject("Motas");
                go.transform.SetParent(parent, false);
                mesh = new Mesh();
                mesh.name = "Motas";
                mesh.MarkDynamic();
                var norms = new Vector3[Cap * 6];
                var tris = new int[Cap * 24];
                int[] oct = { 2, 4, 0, 2, 0, 5, 2, 5, 1, 2, 1, 4, 3, 0, 4, 3, 5, 0, 3, 1, 5, 3, 4, 1 };
                for (int i = 0; i < Cap; i++)
                {
                    for (int k = 0; k < 6; k++) norms[i * 6 + k] = Axes[k];
                    for (int k = 0; k < 24; k++) tris[i * 24 + k] = i * 6 + oct[k];
                }
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.triangles = tris;
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(400f, 100f, 400f));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                mr = go.AddComponent<MeshRenderer>();
                SetupRend(mr, false);
                mr.receiveShadows = false;
            }

            public void Set(int i, Vector3 c, float s)
            {
                for (int k = 0; k < 6; k++)
                {
                    Vector3 a = Axes[k];
                    verts[i * 6 + k] = c + new Vector3(a.x * s, a.y * s * 1.35f, a.z * s);
                }
            }

            public void Hide(int from)
            {
                for (int i = from; i < Cap; i++)
                    for (int k = 0; k < 6; k++) verts[i * 6 + k] = Vector3.zero;
            }

            public void Apply() { mesh.vertices = verts; }

            public void Destroy()
            {
                if (mesh != null) Object.Destroy(mesh);
                if (go != null) Object.Destroy(go);
            }
        }

        MoteCloud moteCloud;
        int moteBiome = -1;

        void DrawMotes()
        {
            if (pal == null) return;
            if (moteCloud == null) moteCloud = new MoteCloud(root);
            if (moteBiome != biome)
            {
                moteBiome = biome;
                moteCloud.mr.sharedMaterial = WorldMaterials.Tone(pal.Mote, 1f);
            }
            float baseSize = biome == 2 ? 0.075f : (biome == 1 ? 0.04f : 0.05f);
            int n = Mathf.Min(motes.Count, MoteCloud.Cap);
            for (int i = 0; i < n; i++)
            {
                var q = motes[i];
                float al = Mathf.Clamp01(q.t) * Mathf.Clamp01(q.life - q.t);
                float tw = 0.7f + 0.3f * Mathf.Sin(time * 3f * q.sp + q.ph);
                moteCloud.Set(i, q.pos, baseSize * al * tw);
            }
            moteCloud.Hide(n);
            moteCloud.Apply();
        }

        // ================================================================== destellos de cristales y lava
        float glowT;

        void UpdateGlowFx(float dt)
        {
            glowT -= dt;
            if (glowT > 0f) return;
            glowT = G.Eco ? 0.8f : 0.32f;
            var gl = canyon.Glows;
            if (gl.Count == 0) return;
            for (int tries = 0; tries < 5; tries++)
            {
                GlowSpot g = gl[rng.Next(gl.Count)];
                Vector3 vp = Cam.WorldToViewportPoint(g.pos);
                if (vp.z < 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) continue;
                FxApi.Play(g.kind == 2 ? "lava_bubble" : "crystal_glow", g.pos, g.col, g.size);
                return;
            }
        }

        // ================================================================== tapa voladora del cofre
        sealed class Lid
        {
            public GameObject go;
            public Vector3 pos, vel, spin;
            public float t, floorY, scale;
        }

        readonly List<Lid> lids = new List<Lid>(2);
        readonly Stack<GameObject> lidPool = new Stack<GameObject>();

        void LaunchLid(Vector3 pos, float r)
        {
            // la malla de la tapa depende del bioma: se reasigna siempre (la anterior pudo destruirse al cambiar de mundo)
            var part = RockFactory.GetChestLid();
            GameObject go;
            if (lidPool.Count > 0) go = lidPool.Pop();
            else go = MakePart(root, "TapaVoladora", part, true).gameObject;
            go.GetComponent<MeshFilter>().sharedMesh = part.mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = part.mats;
            go.SetActive(true);
            var l = new Lid();
            l.go = go;
            l.pos = pos;
            l.vel = new Vector3(Rf(-1.25f, 1.25f), 8.9f, Rf(-1.25f, 1.25f));
            l.spin = new Vector3(Rf(-300f, 300f), Rf(-200f, 200f), Rf(-300f, 300f));
            l.floorY = r * 0.3f;
            l.scale = r;
            l.t = 0f;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * r;
            go.transform.rotation = Quaternion.identity;
            lids.Add(l);
        }

        void UpdateLids(float dt)
        {
            for (int i = lids.Count - 1; i >= 0; i--)
            {
                var l = lids[i];
                l.t += dt;
                l.vel.y -= 23.6f * dt;
                l.pos += l.vel * dt;
                if (l.pos.y < l.floorY)
                {
                    l.pos.y = l.floorY;
                    l.vel.y = Mathf.Abs(l.vel.y) * 0.3f;
                    l.vel.x *= 0.6f; l.vel.z *= 0.6f;
                    l.spin *= 0.5f;
                }
                l.go.transform.position = l.pos;
                l.go.transform.Rotate(l.spin * dt, Space.World);
                if (l.t > 1.6f)
                {
                    l.go.SetActive(false);
                    lidPool.Push(l.go);
                    lids.RemoveAt(i);
                }
            }
        }

        void ClearLids()
        {
            for (int i = 0; i < lids.Count; i++)
            {
                lids[i].go.SetActive(false);
                lidPool.Push(lids[i].go);
            }
            lids.Clear();
        }

        // ================================================================== bucle visual
        /// <summary>Todo lo visual que no depende de rocas ni mineros: se llama una vez por cuadro.</summary>
        void UpdateAmbientFx(float dt, float sdt)
        {
            DrawMotes();
            UpdateGlowFx(sdt);
            UpdateLids(sdt);
            UpdateBossLabel();
            UpdateRockBars();
        }

        void DestroyVisuals()
        {
            if (moteCloud != null) moteCloud.Destroy();
            moteCloud = null;
            if (quadMesh != null) Destroy(quadMesh);
            quadMesh = null;
        }
    }
}
