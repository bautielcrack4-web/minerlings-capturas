using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Fx;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.World
{
    public sealed partial class WorldController
    {
        sealed class Mt
        {
            public Rk rock;
            public Vector3 imp, start, gp;
            public float rr, t;
            public GameObject go, trail;
        }

        struct Mote
        {
            public Vector3 pos;
            public float ph, sp, t, life;
        }

        const float MeteorFall = 0.6f;
        readonly List<Mt> meteors = new List<Mt>(4);
        readonly List<Mote> motes = new List<Mote>(40);
        float meteorNext, fxAcc;
        GameObject goldFx;

        static Vector3 MeteorDir()
        {
            Vector3 r3 = new Vector3(W.R2.x, 0f, W.R2.y);
            return (r3 * 0.45f + Vector3.down).normalized;
        }

        // ================================================================== eventos de Core
        void OnEventStarted(string kind, double duration)
        {
            if (playMode) return;
            evNow = kind;
            switch (kind)
            {
                case "gold_rush":
                    EnrichRocks();
                    StopGoldFx();
                    goldFx = AttachFx("gold_rush", Cam.transform, default(Color), 1f);
                    break;
                case "frenzy":
                    for (int i = 0; i < miners.Count; i++)
                    {
                        var m = miners[i];
                        FxApi.Play("ring", W.V3(m.pos, 0.05f), Biomes.H("ff9a3a"), 1.4f);
                        FxApi.Play("levelup_aura", W.V3(m.pos, 0.05f), Biomes.H("ff9a3a"), 0.8f);
                    }
                    break;
                case "meteor":
                    meteorNext = 0.8f;
                    break;
                case "chest":
                    SpawnChest();
                    break;
            }
        }

        void OnEventEnded(string kind)
        {
            if (evNow == kind) evNow = "";
            if (kind == "gold_rush") StopGoldFx();
            if (kind == "chest" && chest != null && !chest.dead)
            {
                // sin romper: se esfuma entre humo
                var c = chest;
                chest = null;
                rocks.Remove(c);
                c.dead = true;
                c.vanish = 1f;
                leaving.Add(c);
                FxApi.Play("smoke", W.V3(c.pos, c.r * 0.5f), new Color(0.54f, 0.5f, 0.48f), 1.6f);
            }
        }

        /// <summary>Efecto continuo pegado a un transform: garantiza el padre y lo devuelve (nunca null).</summary>
        static GameObject AttachFx(string kind, Transform target, Color tint, float scale)
        {
            GameObject go = FxApi.Attach(kind, target, tint, scale);
            if (go != null && go.transform.parent != target) go.transform.SetParent(target, false);
            return go;
        }

        void StopGoldFx()
        {
            if (goldFx != null) Destroy(goldFx);
            goldFx = null;
        }

        void StopMinerFx()
        {
            for (int i = 0; i < miners.Count; i++)
            {
                if (miners[i].frenzyFx != null) Destroy(miners[i].frenzyFx);
                miners[i].frenzyFx = null;
            }
        }

        /// <summary>Convierte rocas intactas y visibles en rocas ricas (inicio de Fiebre de Oro).</summary>
        void EnrichRocks()
        {
            for (int i = 0; i < rocks.Count; i++)
            {
                var r = rocks[i];
                if (r.dead || r.kind != 0 || r.hp < r.maxHp || r.spawn < 1f) continue;
                Vector3 vp = Cam.WorldToViewportPoint(W.V3(r.pos, r.r * 0.5f));
                if (vp.x < -0.03f || vp.x > 1.03f || vp.y > 0.903f || vp.y < 0.14f) continue;
                if (R01() < 0.6f)
                {
                    r.kind = 1;
                    r.gold *= 4.0;
                    r.maxHp *= 1.5;
                    r.hp = r.maxHp;
                    SetRockMesh(r);
                    Juice.Punch(r.tr, 0.15f, 0.25f);
                    Juice.Flash(r.go, 0.15f);
                    FxApi.Play("glint", W.V3(r.pos, r.r * 1.4f), new Color(1f, 1f, 0.8f), 1.2f);
                    FxApi.Play("gem_sparkle", W.V3(r.pos, r.r * 0.8f), Biomes.GoldRich, 0.8f);
                }
            }
        }

        // ================================================================== bucle de eventos
        static float MoveToward(float v, float target, float maxDelta)
        {
            if (Mathf.Abs(target - v) <= maxDelta) return target;
            return v + Mathf.Sign(target - v) * maxDelta;
        }

        void UpdateEvents(float dt)
        {
            float gt = evNow == "gold_rush" ? 1f : 0f;
            goldK = MoveToward(goldK, gt, dt * (gt > 0.5f ? 2.5f : 2f));
            float ft = evNow == "frenzy" ? 1f : 0f;
            frenzyK = MoveToward(frenzyK, ft, dt * (ft > 0.5f ? 3f : 2f));
            // estelas del Frenesi pegadas a cada minero mientras dura
            for (int i = 0; i < miners.Count; i++)
            {
                var m = miners[i];
                if (m.model == null) continue;
                if (frenzyK > 0.2f && m.frenzyFx == null)
                    m.frenzyFx = AttachFx("frenzy_trail", m.model.transform, default(Color), 1f);
                else if (frenzyK < 0.05f && m.frenzyFx != null)
                {
                    Destroy(m.frenzyFx);
                    m.frenzyFx = null;
                }
            }
            fxAcc += dt;
            if (goldK > 0.3f && fxAcc > (G.Eco ? 0.5f : 0.22f))
            {
                fxAcc = 0f;
                Vector3 p = GroundFromViewport(R01(), Rf(0.15f, 0.85f), Rf(0.3f, 2.5f));
                FxApi.Play("glint", p, new Color(1f, 0.9f, 0.5f), Rf(0.8f, 1.3f));
            }
            else if (evNow == "chest" && chest != null && !chest.dead && fxAcc > 0.25f)
            {
                fxAcc = 0f;
                float a = R01() * Mathf.PI * 2f;
                Vector3 p = W.V3(chest.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (chest.r * 1.1f), chest.r * Rf(0.3f, 1.4f));
                FxApi.Play("glint", p, new Color(1f, 0.95f, 0.65f), 1f);
            }
            if (evNow == "meteor")
            {
                meteorNext -= dt;
                if (meteorNext <= 0f)
                {
                    meteorNext = Rf(0.8f, 1.2f);
                    SpawnMeteor();
                }
            }
            UpdateMeteors(dt);
        }

        void SpawnMeteor()
        {
            if (meteors.Count >= 3) return;
            Rk pick = null;
            int seen = 0;
            for (int i = 0; i < rocks.Count; i++)
            {
                var r = rocks[i];
                if (r.dead || r.kind == 4 || r.spawn < 0.8f) continue;
                Vector3 vp = Cam.WorldToViewportPoint(W.V3(r.pos, r.r * 0.5f));
                if (vp.x < 0.097f || vp.x > 0.944f || vp.y > 0.786f || vp.y < 0.26f) continue;
                bool taken = false;
                for (int k = 0; k < meteors.Count; k++) if (meteors[k].rock == r) taken = true;
                if (taken) continue;
                seen++;
                if (rng.Next(seen) == 0) pick = r; // eleccion uniforme (reservoir)
            }
            if (pick == null) return;
            var m = new Mt();
            m.rock = pick;
            m.imp = W.V3(pick.pos, pick.r * 0.5f);
            m.start = m.imp - MeteorDir() * 24f;
            m.gp = W.V3(pick.pos);
            m.rr = pick.r;
            m.t = 0f;
            m.go = GetMeteorObj();
            m.go.transform.position = m.start;
            m.go.SetActive(true);
            m.trail = AttachFx("meteor_trail", m.go.transform, default(Color), 1f);
            meteors.Add(m);
            Sfx.Play("meteor", -8f);
        }

        Vector3 MeteorPos(Mt m)
        {
            float u = Mathf.Clamp01(m.t / MeteorFall);
            return Vector3.Lerp(m.start, m.imp, Mathf.Pow(u, 1.45f));
        }

        readonly Stack<GameObject> meteorPool = new Stack<GameObject>();

        GameObject GetMeteorObj()
        {
            if (meteorPool.Count > 0) return meteorPool.Pop();
            var part = RockFactory.Meteor;
            var mr = MakePart(root, "Meteorito", part, false);
            mr.transform.localScale = Vector3.one * 0.55f;
            return mr.gameObject;
        }

        void ReleaseMeteor(Mt m)
        {
            if (m.trail != null)
            {
                // la estela se queda un instante donde cayo y se destruye sola
                m.trail.transform.SetParent(null, true);
                Destroy(m.trail, 0.7f);
                m.trail = null;
            }
            if (m.go != null)
            {
                m.go.SetActive(false);
                meteorPool.Push(m.go);
                m.go = null;
            }
        }

        void ClearMeteors()
        {
            for (int i = 0; i < meteors.Count; i++) ReleaseMeteor(meteors[i]);
            meteors.Clear();
        }

        void UpdateMeteors(float dt)
        {
            for (int i = meteors.Count - 1; i >= 0; i--)
            {
                var m = meteors[i];
                m.t += dt;
                if (m.t >= MeteorFall)
                {
                    meteors.RemoveAt(i);
                    ReleaseMeteor(m);
                    MeteorImpact(m);
                    continue;
                }
                if (m.go != null)
                {
                    m.go.transform.position = MeteorPos(m);
                    m.go.transform.Rotate(180f * dt, 90f * dt, 0f, Space.Self);
                }
            }
        }

        void MeteorImpact(Mt m)
        {
            Vector3 imp = m.imp, gp = m.gp;
            var rk = m.rock;
            FxApi.Play("meteor_impact", gp, Biomes.H("ffae4a"), 1f);
            FxApi.Play("dust", gp + Vector3.up * 0.1f, pal.Decor2, 1.4f);
            if (rk != null && !rk.dead) Damage(rk, rk.maxHp * 0.35, false, imp, Biomes.H("ffae4a"));
            Shake(9f);
            Juice.HitStop(0.04f);
            Juice.Vibrate(25);
            EmitBigMoment("meteor_hit");
        }

        // ================================================================== ambiente
        void UpdateMotes(float dt)
        {
            if (pal == null) return;
            int want = G.Eco ? 10 : 34;
            while (motes.Count < want)
            {
                var m = new Mote();
                m.pos = GroundFromViewport(R01(), R01(), Rf(0.3f, 3.0f));
                m.ph = R01() * Mathf.PI * 2f;
                m.sp = Rf(0.6f, 1.4f);
                m.t = 0f;
                m.life = Rf(4f, 9f);
                motes.Add(m);
            }
            Vector3 r3 = new Vector3(W.R2.x, 0f, W.R2.y);
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var q = motes[i];
                q.t += dt;
                float sp = q.sp;
                Vector3 vel;
                switch (biome)
                {
                    case 0: vel = r3 * (Mathf.Sin(time * sp + q.ph) * 22f * W.PX) + Vector3.up * (10f * sp * W.PX); break;
                    case 1: vel = r3 * (260f * sp * W.PX) + Vector3.up * (30f * Mathf.Sin(time + q.ph) * W.PX * 0.3f); break;
                    case 2: vel = r3 * (Mathf.Sin(time * 0.7f + q.ph) * 12f * W.PX) + Vector3.up * (14f * sp * W.PX); break;
                    default: vel = r3 * (Mathf.Sin(time * 2f + q.ph) * 18f * W.PX) + Vector3.up * (60f * sp * W.PX); break;
                }
                q.pos += vel * dt;
                Vector3 vp = Cam.WorldToViewportPoint(q.pos);
                if (q.t > q.life || vp.x < -0.08f || vp.x > 1.08f || vp.y < -0.04f || vp.y > 1.04f)
                {
                    motes.RemoveAt(i);
                    continue;
                }
                motes[i] = q;
            }
        }
    }
}
