using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Fx;
using Mineros.Miners;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.World
{
    public sealed partial class WorldController
    {
        /// <summary>Minero logico (IA, golpe, estado); su cuerpo visual es un MinerModel.</summary>
        sealed class Mn
        {
            public Vector2 pos, face = new Vector2(0f, 1f), vel;
            public Tool tool;
            public Rk target;
            public float side = 1f, cd, swing = -1f, swingDur = 0.3f;
            public bool hitDone;
            public float walk;
            public bool moving, player;
            public float retarget;
            public int idx;
            public float dust, jump;
            public float cele, celeTot = 1f;
            public float stride;
            public MinerModel model;
            public GameObject frenzyFx;
        }

        readonly List<Mn> miners = new List<Mn>(3);
        int cachedSkin = -1;
        Color helmetColor = Color.yellow;

        Vector2 AvgMiner()
        {
            if (miners.Count == 0) return Vector2.zero;
            Vector2 a = Vector2.zero;
            for (int i = 0; i < miners.Count; i++) a += miners[i].pos;
            return a / miners.Count;
        }

        Color HelmetColor()
        {
            int sk = Mathf.Clamp(G.Skin, 0, Content.Skins.Length - 1);
            if (sk != cachedSkin)
            {
                cachedSkin = sk;
                helmetColor = Biomes.H(Content.Skins[sk].ColorHex);
            }
            return helmetColor;
        }

        // ================================================================== alta y baja de mineros
        void RebuildMiners()
        {
            var old = new List<Mn>(miners);
            miners.Clear();
            int i = 0;
            for (int e = 0; e < G.Equip.Length; e++)
            {
                Tool t = G.Equip[e];
                if (t == null) continue;
                var m = new Mn();
                m.tool = t;
                m.idx = i;
                m.side = (i % 2 == 0) ? 1f : -1f;
                if (i < old.Count)
                {
                    m.pos = old[i].pos;
                    m.face = old[i].face;
                    m.model = old[i].model;
                    m.frenzyFx = old[i].frenzyFx;
                }
                else if (old.Count > 0)
                {
                    m.pos = old[0].pos + W.R2 * (Rf(-60f, 60f) * W.PX) - W.F2 * (40f * W.PX);
                }
                else
                {
                    m.pos = camFocus;
                }
                m.player = playMode && i == 0;
                m.walk = R01() * 10f;
                if (m.model == null) m.model = MinerModel.Create(minersRoot, i);
                miners.Add(m);
                i++;
            }
            for (int k = miners.Count; k < old.Count; k++)
            {
                if (old[k].frenzyFx != null) Destroy(old[k].frenzyFx);
                if (old[k].model != null) Destroy(old[k].model.gameObject);
            }
        }

        // ================================================================== IA
        Rk PickTarget(Mn m)
        {
            if (boss != null && !boss.dead) return boss;
            if (chest != null && !chest.dead) return chest;
            Rk best = null;
            float bestS = float.MaxValue;
            Vector2 anchor = m.pos;
            if (playMode && miners.Count > 0) anchor = miners[0].pos;
            for (int i = 0; i < rocks.Count; i++)
            {
                var r = rocks[i];
                if (r.dead || r.spawn < 0.5f) continue;
                int n = 0;
                for (int k = 0; k < miners.Count; k++)
                    if (miners[k] != m && miners[k].target == r) n++;
                float d = (r.pos - m.pos).magnitude;
                if (playMode) d += (r.pos - anchor).magnitude * 0.8f;
                else
                {
                    Vector3 vp = Cam.WorldToViewportPoint(W.V3(r.pos, r.r * 0.5f));
                    if (vp.y < 0.14f || vp.y > 0.903f || vp.x < -0.055f || vp.x > 1.055f) d += 900f * W.PX;
                }
                float sc = d + n * 260f * W.PX;
                if (sc < bestS)
                {
                    bestS = sc;
                    best = r;
                }
            }
            return best;
        }

        void UpdateMiner(Mn m, float dt)
        {
            m.cd -= dt;
            m.walk += dt;
            m.cele = Mathf.Max(0f, m.cele - dt);
            if (m.jump > 0f) m.jump = Mathf.Max(0f, m.jump - dt * 1.4f);
            if (m.swing >= 0f)
            {
                m.swing += dt / m.swingDur;
                if (m.swing >= 0.62f && !m.hitDone)
                {
                    m.hitDone = true;
                    DoHit(m);
                }
                if (m.swing >= 1f) m.swing = -1f;
            }
            if (m.moving)
            {
                m.dust -= dt;
                if (m.dust <= 0f)
                {
                    m.dust = (G.Eco ? 0.4f : 0.24f) - 0.09f * frenzyK;
                    FxApi.Play("dust", W.V3(m.pos - m.face * 0.14f, 0.08f), pal.Decor2, 0.45f);
                }
            }
            else
            {
                m.vel = Vector2.zero;
            }
            if (m.cele > 0f && m.swing < 0f)
            {
                m.moving = false;
                m.vel = Vector2.zero;
                return;
            }
            if (m.player)
            {
                UpdatePlayer(m, dt);
                return;
            }
            m.retarget -= dt;
            if (m.target == null || m.target.dead || (m.retarget <= 0f && m.swing < 0f))
            {
                var nt = PickTarget(m);
                if (nt != m.target)
                {
                    m.target = nt;
                    if (nt != null)
                    {
                        // lado (izquierda/derecha en pantalla) respecto a la roca para no superponerse
                        float sd = Mathf.Sign(Vector2.Dot(m.pos - nt.pos, W.R2));
                        m.side = sd == 0f ? 1f : sd;
                        for (int i = 0; i < miners.Count; i++)
                        {
                            var o = miners[i];
                            if (o != m && o.target == nt && o.side == m.side)
                            {
                                m.side = -m.side;
                                break;
                            }
                        }
                    }
                }
                m.retarget = 1.5f;
            }
            var t = m.target;
            if (t == null)
            {
                m.moving = false;
                return;
            }
            Vector2 goal = t.pos + W.R2 * (m.side * (t.r * 0.85f + 34f * W.PX)) - W.F2 * (8f * W.PX);
            if (t.kind == 2)
                goal = t.pos + W.R2 * (m.side * (t.r * 0.9f + 36f * W.PX)) - W.F2 * ((18f + m.idx * 10f) * W.PX);
            Vector2 d = goal - m.pos;
            float dl = d.magnitude;
            if (dl > 10f * W.PX && m.swing < 0f)
            {
                float spd = 230f * W.PX * (1f + 0.7f * frenzyK);
                float step = Mathf.Min(spd * dt, dl);
                Vector2 dn = d / dl;
                m.pos += dn * step;
                m.vel = dn * spd;
                m.stride += step * 4.07f;
                m.moving = true;
                m.face = dn;
            }
            else
            {
                m.moving = false;
                Vector2 toRock = t.pos - m.pos;
                if (toRock.sqrMagnitude > 1e-6f) m.face = toRock.normalized;
                if (m.cd <= 0f && m.swing < 0f) StartSwing(m);
            }
        }

        void UpdatePlayer(Mn m, float dt)
        {
            Vector2 jv = W.R2 * Joy.x + W.F2 * Joy.y;
            if (Joy.magnitude > 0.12f)
            {
                m.pos += jv * (300f * W.PX * dt);
                m.vel = jv * (300f * W.PX);
                m.stride += 300f * W.PX * dt * 4.07f;
                float lim = W.ArenaR - 50f * W.PX;
                if (m.pos.magnitude > lim) m.pos = m.pos.normalized * lim;
                m.moving = true;
                if (jv.sqrMagnitude > 1e-6f) m.face = jv.normalized;
            }
            else
            {
                m.moving = false;
            }
            Rk best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < rocks.Count; i++)
            {
                var r = rocks[i];
                if (r.dead) continue;
                float d = (r.pos - m.pos).magnitude - r.r;
                if (d < 62f * W.PX && d < bd)
                {
                    bd = d;
                    best = r;
                }
            }
            m.target = best;
            if (best != null)
            {
                Vector2 toRock = best.pos - m.pos;
                if (!m.moving && toRock.sqrMagnitude > 1e-6f) m.face = toRock.normalized;
                if (m.cd <= 0f && m.swing < 0f)
                {
                    if (toRock.sqrMagnitude > 1e-6f) m.face = toRock.normalized;
                    StartSwing(m);
                }
            }
        }

        void StartSwing(Mn m)
        {
            m.swing = 0f;
            m.hitDone = false;
            float iv = (float)G.ToolInterval(m.tool);
            m.cd = iv;
            m.swingDur = Mathf.Min(0.42f, iv * 0.85f);
        }

        void DoHit(Mn m)
        {
            var t = m.target;
            if (t == null || t.dead) return;
            double dmg = G.ToolDamage(m.tool);
            bool crit = R01() < G.CritChance();
            if (crit)
            {
                dmg *= G.CritMult();
                G.AddStat("crits");
            }
            if (playMode && m.player)
            {
                combo++;
                comboT = 1.4f;
                EmitCombo(combo);
            }
            Vector2 dir = m.pos - t.pos;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : W.R2;
            Vector3 at = W.V3(t.pos + dir * (t.r * 0.75f), t.r * 0.6f);
            // la roca recula un poco al recibir el golpe
            t.kick = -dir * ((crit ? 7f : 4f) * Mathf.Clamp(t.r / (42f * W.PX), 0.6f, 1.2f) * W.PX);
            Damage(t, dmg, crit, at, Color.white);
            if (m.tool.K == 1)
            {
                for (int i = 0; i < rocks.Count; i++)
                {
                    var r = rocks[i];
                    if (r != t && !r.dead && (r.pos - t.pos).magnitude < 150f * W.PX + r.r)
                        Damage(r, dmg * 0.6, false, W.V3(r.pos, r.r * 0.6f), Color.white);
                }
                FxApi.Play("ring", W.V3(t.pos, 0.05f), new Color(1f, 1f, 1f, 0.8f), 1.1f);
                Shake(3f);
            }
            if (crit)
            {
                Juice.HitStop(0.045f);   // sin zoom: los criticos son frecuentes y el zoom pulsaba la pantalla
                Juice.Vibrate(20);
                Shake(5f);
                Sfx.Play("crit", -6f);
            }
            Sfx.Play("pick", -4f, 1f + (crit ? 0.15f : 0f));
            Sfx.Play("clink", -10f);
        }

        // ================================================================== visuales
        void UpdateMinerVisuals(float dt)
        {
            Color helmet = HelmetColor();
            for (int i = 0; i < miners.Count; i++)
            {
                var m = miners[i];
                if (m.model == null) continue;
                m.model.transform.position = W.V3(m.pos, 0f);
                Vector3 dir = new Vector3(m.face.x, 0f, m.face.y);
                bool blink = Mathf.Repeat(time + m.idx * 1.7f, 3.3f) < 0.12f;
                m.model.SetLook(helmet, m.tool);
                m.model.SetPose(dir, m.stride, m.moving, m.swing, blink, dt);
                m.model.SetCelebrate(m.cele > 0f ? Mathf.Clamp01(m.cele / m.celeTot) : 0f);
            }
        }

        void Celebrate()
        {
            for (int i = 0; i < miners.Count; i++)
            {
                var m = miners[i];
                m.jump = 1f + m.idx * 0.12f;
                m.cele = 0.9f;
                m.celeTot = 0.9f;
            }
        }

        void OnUpgraded(string kind)
        {
            Color col = KindColor(kind);
            for (int i = 0; i < miners.Count; i++)
            {
                var m = miners[i];
                FxApi.Play("levelup_aura", W.V3(m.pos, 0.05f), col, 0.7f);
            }
        }

        static Color KindColor(string kind)
        {
            switch (kind)
            {
                case "power": return Biomes.H("ff6a4a");
                case "speed": return Biomes.H("ffd84a");
                default: return Biomes.H("ffcc33");
            }
        }

        /// <summary>Hito de nivel: aura, anillo dorado y celebracion del color de la estadistica.</summary>
        void OnMilestone(string kind, int level)
        {
            Color col = KindColor(kind);
            for (int i = 0; i < miners.Count; i++)
            {
                var m = miners[i];
                m.jump = 1f + m.idx * 0.12f;
                m.cele = 1.1f;
                m.celeTot = 1.1f;
                Vector3 g = W.V3(m.pos, 0.05f);
                FxApi.Play("levelup_aura", g, col, 1f);
            }
            if (miners.Count > 0) FxApi.Play("milestone", W.V3(AvgMiner(), 0.05f), col, 1f);
            Juice.Vibrate(30);
            EmitBigMoment("milestone");
        }

        // ================================================================== luces puntuales (cueva / volcan)
        void UpdateLamps()
        {
            bool dark = pal != null && pal.Dark > 0f;
            if (!dark)
            {
                for (int i = 0; i < lamps.Length; i++) if (lamps[i].enabled) lamps[i].enabled = false;
                return;
            }
            int li = 0;
            for (int i = 0; i < miners.Count && li < 2; i++)
            {
                if (miners[i].model == null) continue;
                var l = lamps[li++];
                l.enabled = true;
                l.transform.position = miners[i].model.LampWorldPos + Vector3.up * 0.2f;
                l.color = new Color(1f, 0.93f, 0.72f);
                l.range = 5.5f;
                l.intensity = 1.5f;
            }
            while (li < 2) lamps[li++].enabled = false;
            // luz de escenario: jefe, cofre o la roca brillante mas cercana
            var f = lamps[2];
            Rk target = null;
            if (boss != null && !boss.dead) target = boss;
            else if (chest != null && !chest.dead) target = chest;
            Color fc = Biomes.H("c58bff");
            float fi = 2.0f, fr = 7f;
            if (target != null && target.kind == 4) { fc = Biomes.GoldRich; fi = 1.6f; fr = 6f; }
            if (target == null)
            {
                float bestD = 1e9f;
                Vector2 a = AvgMiner();
                for (int i = 0; i < rocks.Count; i++)
                {
                    var r = rocks[i];
                    if (r.dead) continue;
                    float d = (r.pos - a).sqrMagnitude;
                    if (d < bestD) { bestD = d; target = r; }
                }
                fc = pal.Ore;
                fi = 1.1f;
                fr = 4.5f;
            }
            if (target != null)
            {
                f.enabled = true;
                f.transform.position = W.V3(target.pos, target.r * 1.4f + 0.4f);
                f.color = fc;
                f.intensity = fi;
                f.range = fr;
            }
            else f.enabled = false;
        }
    }
}
