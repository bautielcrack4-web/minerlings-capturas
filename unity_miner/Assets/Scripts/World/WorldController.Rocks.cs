using System.Collections.Generic;
using Mineros.Art;
using Mineros.Audio;
using Mineros.Core;
using Mineros.Fx;
using UnityEngine;
using UnityEngine.Rendering;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.World
{
    public sealed partial class WorldController
    {
        /// <summary>Roca del mundo (normal 0, rica 1, jefe 2, reloj 3, cofre 4). Los objetos se reciclan con un pool.</summary>
        sealed class Rk
        {
            public GameObject go;
            public Transform tr;      // raiz: solo posicion (Juice.Punch escala esta)
            public Transform vis;     // visual: escala de la roca (aparicion, jefe, desaparicion)
            public MeshFilter bodyMf, crackMf;
            public MeshRenderer bodyR, crackR;
            public Vector2 pos;
            public float r = 0.5f;
            public Vector2 kick;
            public double hp = 1, maxHp = 1, gold = 1;
            public int kind, tier = 1;
            public bool hard;
            public int phase;
            public float gscale = 1f, vanish = -1f, glint, shake, spawn;
            public bool dead;
            public RockSet set;
            public int crackLevel = -1;
            public int shownPhase = -1;
            public Transform extras;
            public Transform lidTr, clockTr;
            public Transform[] crystals;
            public float[] crystalScale;
            public int[] crystalRank;
        }

        readonly List<Rk> rocks = new List<Rk>(64);
        readonly List<Rk> leaving = new List<Rk>(4);
        readonly Stack<Rk> rockPool = new Stack<Rk>();
        Rk boss, chest;
        int targetRocks = 10;
        int combo;
        float comboT;
        int playRocksNeeded = 30;
        float hitFxCd;

        static readonly float[] TierRLo = { 27f, 37f, 54f };
        static readonly float[] TierRHi = { 33f, 45f, 62f };
        static readonly float[] TierHp = { 0.55f, 1.05f, 2.2f };
        static readonly float[] TierGold = { 0.6f, 1.0f, 2.5f };
        static readonly int[] CrystalOrder = { 0, 6, 1, 5, 2, 4, 3 };

        float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        float R01() { return (float)rng.NextDouble(); }

        // ================================================================== pool
        static void SetupRend(MeshRenderer m, bool shadows)
        {
            m.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            m.receiveShadows = true;
            m.lightProbeUsage = LightProbeUsage.Off;
            m.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>Crea un hijo con malla + renderer (materiales compartidos de la pieza).</summary>
        static MeshRenderer MakePart(Transform parent, string name, MeshPart part, bool shadows)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = part.mesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterials = part.mats;
            SetupRend(mr, shadows);
            return mr;
        }

        Rk GetRockObj()
        {
            Rk r;
            if (rockPool.Count > 0) r = rockPool.Pop();
            else
            {
                r = new Rk();
                r.go = new GameObject("Roca");
                r.tr = r.go.transform;
                r.tr.SetParent(rocksRoot, false);
                var v = new GameObject("Visual");
                r.vis = v.transform;
                r.vis.SetParent(r.tr, false);
                var body = new GameObject("Cuerpo");
                body.transform.SetParent(r.vis, false);
                r.bodyMf = body.AddComponent<MeshFilter>();
                r.bodyR = body.AddComponent<MeshRenderer>();
                SetupRend(r.bodyR, true);
                var cr = new GameObject("Grietas");
                cr.transform.SetParent(r.vis, false);
                r.crackMf = cr.AddComponent<MeshFilter>();
                r.crackR = cr.AddComponent<MeshRenderer>();
                SetupRend(r.crackR, false);
            }
            r.kick = Vector2.zero;
            r.phase = 0; r.gscale = 1f; r.vanish = -1f;
            r.glint = r.shake = 0f;
            r.dead = false;
            r.crackLevel = -1;
            r.shownPhase = -1;
            r.hard = false;
            r.kind = 0;
            r.set = null;
            r.tr.localScale = Vector3.one;
            r.vis.localScale = Vector3.one;
            r.crackR.enabled = false;
            r.go.SetActive(true);
            return r;
        }

        void ClearExtras(Rk r)
        {
            if (r.extras != null) { Destroy(r.extras.gameObject); r.extras = null; }
            if (r.lidTr != null) { Destroy(r.lidTr.gameObject); r.lidTr = null; }
            if (r.clockTr != null) { Destroy(r.clockTr.gameObject); r.clockTr = null; }
            r.crystals = null;
        }

        void ReleaseRock(Rk r)
        {
            // limpiar toda referencia viva para que un objeto reciclado no sea confundido con el anterior
            for (int i = 0; i < miners.Count; i++)
                if (miners[i].target == r) miners[i].target = null;
            for (int i = 0; i < meteors.Count; i++)
                if (meteors[i].rock == r) meteors[i].rock = null;
            if (boss == r) boss = null;
            if (chest == r) chest = null;
            ClearExtras(r);
            r.go.SetActive(false);
            r.bodyMf.sharedMesh = null;
            r.crackMf.sharedMesh = null;
            rockPool.Push(r);
        }

        void ReleaseAllRocks()
        {
            for (int i = 0; i < rocks.Count; i++) ReleaseRock(rocks[i]);
            rocks.Clear();
            for (int i = 0; i < leaving.Count; i++) ReleaseRock(leaving[i]);
            leaving.Clear();
            boss = null;
            chest = null;
        }

        void HideRock(Rk r)
        {
            r.go.SetActive(false);
        }

        // ================================================================== creacion
        int RollTier()
        {
            int si = G.StageIdx();
            float big = Mathf.Clamp(0.04f + si * 0.012f, 0.04f, 0.18f);
            float small = Mathf.Clamp(0.38f - si * 0.01f, 0.22f, 0.38f);
            float u = R01();
            if (u < big) return 2;
            if (u < big + small) return 0;
            return 1;
        }

        bool FreeSpot(Vector2 p, float d)
        {
            for (int i = 0; i < rocks.Count; i++)
            {
                var r = rocks[i];
                if (!r.dead && (r.pos - p).magnitude < d + (r.r - 40f * W.PX)) return false;
            }
            for (int i = 0; i < miners.Count; i++)
                if ((miners[i].pos - p).magnitude < 70f * W.PX) return false;
            return true;
        }

        void SpawnIdleRock(bool initial)
        {
            float s0 = W.S(AvgMiner());
            for (int tries = 0; tries < 14; tries++)
            {
                float s = initial ? Rf(s0 - 380f * W.PX, s0 + 760f * W.PX) : Rf(s0 + 150f * W.PX, s0 + 820f * W.PX);
                Vector2 p = W.CorridorPoint(s, Rf(-0.72f, 0.72f));
                if (FreeSpot(p, 128f * W.PX))
                {
                    float richP = (goldK > 0.5f && !initial) ? 0.55f : 0.12f;
                    AddRock(p, R01() < richP, initial);
                    return;
                }
            }
        }

        void SetRockMesh(Rk r)
        {
            int cls = r.kind == 1 ? 1 : (r.hard ? 2 : 0);
            r.set = RockFactory.Get(Mathf.Clamp(r.tier, 0, 2), cls, rng.Next(RockFactory.Variants));
            r.bodyMf.sharedMesh = r.set.body.mesh;
            r.bodyR.sharedMaterials = r.set.body.mats;
            r.crackLevel = -1;
        }

        Rk AddRock(Vector2 p, bool rich, bool instant)
        {
            var rk = GetRockObj();
            rk.pos = p;
            rk.kind = rich ? 1 : 0;
            rk.tier = RollTier();
            rk.hard = !rich && G.World >= 3 && rk.tier >= 1 && R01() < 0.16f;
            rk.r = Rf(TierRLo[rk.tier], TierRHi[rk.tier]) * W.PX * (rich ? 1.12f : 1f);
            double hpBase = G.RockHp();
            rk.maxHp = hpBase * (rich ? 1.5 : 1.0) * TierHp[rk.tier] * (rk.hard ? 1.8 : 1.0);
            rk.gold = G.RockGold() * (rich ? 4.0 : 1.0) * TierGold[rk.tier] * (rk.hard ? 2.2 : 1.0);
            if (playMode)
            {
                rk.gold *= 2.0;
                rk.maxHp *= 0.8;
            }
            rk.hp = rk.maxHp;
            rk.spawn = instant ? 1f : 0f;
            SetRockMesh(rk);
            rocks.Add(rk);
            return rk;
        }

        /// <summary>Roca reloj de Excavar: +3 s al romperla.</summary>
        void MakeClock(Rk rk)
        {
            rk.kind = 3;
            var part = RockFactory.GetClock();
            var mr = MakePart(rk.vis, "Reloj", part, false);
            mr.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            mr.transform.localScale = Vector3.one * 0.95f;
            rk.clockTr = mr.transform;
        }

        void SpawnBoss()
        {
            if (boss != null && !boss.dead) return;
            Vector2 p = W.CorridorPoint(W.S(AvgMiner()) + 330f * W.PX, 0f);
            for (int i = 0; i < rocks.Count; i++)
            {
                var r = rocks[i];
                if (!r.dead && (r.pos - p).magnitude < 200f * W.PX)
                {
                    r.dead = true;
                    HideRock(r);
                    Burst(r.pos, r.r, 6, false, false);
                }
            }
            var b = GetRockObj();
            b.pos = p;
            b.kind = 2;
            b.tier = 3;
            b.r = 92f * W.PX;
            b.maxHp = G.RockHp() * Balance.BossHpMult;
            b.hp = b.maxHp;
            b.gold = G.RockGold() * Balance.BossGoldMult;
            b.spawn = 0f;
            b.set = RockFactory.GetBoss();
            b.bodyMf.sharedMesh = b.set.body.mesh;
            b.bodyR.sharedMaterials = b.set.body.mats;
            BuildBossCrystals(b);
            rocks.Add(b);
            boss = b;
            Shake(16f);
            Vector3 g = W.V3(p);
            FxApi.Play("ring", g + Vector3.up * 0.05f, Biomes.CrystalL, 2.6f);
            FxApi.Play("dust", g + Vector3.up * 0.1f, pal.Decor2, 2.2f);
            FxApi.Play("dust", g + new Vector3(1f, 0.1f, 0.6f), pal.Decor2, 1.6f);
            FxApi.Play("dust", g + new Vector3(-1f, 0.1f, -0.6f), pal.Decor2, 1.6f);
            Juice.Vibrate(30);
            ShowBossLabel();
        }

        void BuildBossCrystals(Rk b)
        {
            var root = new GameObject("Cristales").transform;
            root.SetParent(b.vis, false);
            b.extras = root;
            b.crystals = new Transform[7];
            b.crystalScale = new float[7 * 3];
            b.crystalRank = new int[7];
            var rg = new System.Random(77);
            Vector3 r3 = new Vector3(W.R2.x, 0f, W.R2.y);
            for (int i = 0; i < 7; i++)
            {
                float u = (i - 3f) / 3f;
                float x = u * 0.62f;
                float baseY = 0.78f + 0.2f * (1f - Mathf.Abs(u));
                float h = (1.2f - Mathf.Abs(u) * 0.45f) * (0.85f + 0.25f * (float)rg.NextDouble()) * 0.85f;
                float w = (0.17f + 0.06f * (float)rg.NextDouble()) * 1.2f;
                float tilt = u * 0.38f + ((float)rg.NextDouble() - 0.5f) * 0.24f;
                var mr = MakePart(root, "C" + i, RockFactory.GetBossCrystal(i % 3), true);
                var g = mr.transform;
                g.localPosition = r3 * x + new Vector3(0f, baseY, ((float)rg.NextDouble() - 0.5f) * 0.3f) - new Vector3(0f, 0.08f, 0f);
                Vector3 up = (Vector3.up * Mathf.Cos(tilt) + r3 * Mathf.Sin(tilt)).normalized;
                g.localRotation = Quaternion.FromToRotation(Vector3.up, up);
                b.crystals[i] = g;
                b.crystalScale[i * 3] = w; b.crystalScale[i * 3 + 1] = h; b.crystalScale[i * 3 + 2] = w;
                for (int k = 0; k < 7; k++) if (CrystalOrder[k] == i) b.crystalRank[i] = k;
            }
            b.shownPhase = -1;
        }

        /// <summary>Roca Cofre del evento: en el centro del pasillo, con mas vida que una roca normal.</summary>
        void SpawnChest()
        {
            if (chest != null && !chest.dead) return;
            Vector2 p = W.CorridorPoint(W.S(AvgMiner()) + 300f * W.PX, 0f);
            for (int i = 0; i < rocks.Count; i++)
            {
                var r = rocks[i];
                if (!r.dead && r != boss && (r.pos - p).magnitude < 170f * W.PX + r.r * 0.5f)
                {
                    r.dead = true;
                    HideRock(r);
                    Burst(r.pos, r.r, 3, false, false);
                }
            }
            double dmg = 0;
            for (int i = 0; i < miners.Count; i++) dmg += G.ToolDamage(miners[i].tool);
            if (miners.Count > 0) dmg /= miners.Count;
            var c = GetRockObj();
            c.pos = p;
            c.kind = 4;
            c.tier = 1;
            c.r = 66f * W.PX;
            c.maxHp = System.Math.Max(G.RockHp() * 5.0, dmg * 8.0);
            c.hp = c.maxHp;
            c.gold = 0;
            c.spawn = 1f;
            var body = RockFactory.GetChestBody();
            c.bodyMf.sharedMesh = body.mesh;
            c.bodyR.sharedMaterials = body.mats;
            var lr = MakePart(c.vis, "Tapa", RockFactory.GetChestLid(), true);
            lr.transform.localPosition = new Vector3(0f, 0.86f, 0f);
            c.lidTr = lr.transform;
            rocks.Add(c);
            chest = c;
            Vector3 wp = W.V3(p);
            FxApi.Play("chest_open", wp, Biomes.GoldRich, 0.7f);
            FxApi.Play("ring", wp + Vector3.up * 0.05f, Biomes.GoldRich, 2.2f);
            FxApi.Play("dust", wp + Vector3.up * 0.1f, pal.Decor2, 1.4f);
            Sfx.Play("clink", -4f, 1.4f);
        }

        // ================================================================== bucle de rocas
        void UpdateRocks(float dt)
        {
            float s0 = W.S(AvgMiner());
            hitFxCd = Mathf.Max(0f, hitFxCd - dt);
            for (int i = rocks.Count - 1; i >= 0; i--)
            {
                var r = rocks[i];
                r.shake = Mathf.Max(0f, r.shake - dt * 6f);
                r.spawn = Mathf.Min(1f, r.spawn + dt * (r.kind == 2 ? 1.2f : (r.tier == 0 ? 4.5f : 3f)));
                r.kick = Vector2.Lerp(r.kick, Vector2.zero, Mathf.Min(1f, dt * 16f));
                r.glint = Mathf.Max(0f, r.glint - dt * 2.5f);
                if (r.kind == 2)
                    r.gscale = Mathf.Lerp(r.gscale, 1f + 0.07f * r.phase + 0.1f * r.glint, Mathf.Min(1f, dt * 12f));
                bool drop = r.dead;
                if (!drop && !playMode && W.S(r.pos) < s0 - 950f * W.PX && r != boss) drop = true;
                if (drop)
                {
                    rocks.RemoveAt(i);
                    ReleaseRock(r);
                }
            }
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                var r = leaving[i];
                r.vanish -= dt * 1.8f;
                if (r.vanish <= 0f)
                {
                    leaving.RemoveAt(i);
                    ReleaseRock(r);
                }
            }
        }

        static float EaseOut(float x, float k) { return 1f - Mathf.Pow(1f - Mathf.Clamp01(x), k); }

        void UpdateRockVisual(Rk r)
        {
            if (r.dead && r.vanish < 0f) return;
            float sp = r.spawn < 1f ? Mathf.Max(0.08f, EaseOut(r.spawn, 2.2f)) : 1f;
            Vector3 sc = Vector3.one * sp;
            Vector2 off = r.kick + W.R2 * (Mathf.Sin(time * 70f) * 3f * W.PX * r.shake);
            float yOff = 0f;
            if (r.kind == 2)
            {
                float lost = r.maxHp > 0 ? (float)(1.0 - r.hp / r.maxHp) : 0f;
                off += W.R2 * (Mathf.Sin(time * 40f) * (2f + 2f * r.phase) * W.PX * lost);
                sc *= r.gscale;
            }
            if (r.vanish >= 0f)
            {
                sc.y *= Mathf.Pow(Mathf.Clamp01(r.vanish), 0.6f);
                yOff -= (1f - r.vanish) * r.r * 0.4f;
            }
            if (r.kind == 4) sc *= 1f + 0.025f * Mathf.Sin(time * 5f);
            r.tr.position = W.V3(r.pos + off, yOff);
            r.vis.localScale = sc * r.r;

            // grietas segun el dano
            if (r.set != null && r.kind != 4)
            {
                float frac = r.maxHp > 0 ? Mathf.Clamp01((float)(1.0 - r.hp / r.maxHp)) : 0f;
                int nc = Mathf.Clamp(Mathf.CeilToInt(frac * 4f - 0.3f), 0, 4);
                if (nc != r.crackLevel)
                {
                    r.crackLevel = nc;
                    if (nc > 0)
                    {
                        var cp = r.set.cracks[nc - 1];
                        r.crackMf.sharedMesh = cp.mesh;
                        r.crackR.sharedMaterials = cp.mats;
                    }
                    else r.crackMf.sharedMesh = null;
                    r.crackR.enabled = nc > 0;
                }
            }
            if (r.kind == 2 && r.crystals != null && r.shownPhase != r.phase)
            {
                r.shownPhase = r.phase;
                int show = 5 + r.phase;
                float k = 1f + 0.1f * r.phase;
                for (int i = 0; i < r.crystals.Length; i++)
                {
                    bool vis = r.crystalRank[i] < show;
                    r.crystals[i].gameObject.SetActive(vis);
                    r.crystals[i].localScale = new Vector3(r.crystalScale[i * 3] * k, r.crystalScale[i * 3 + 1] * k, r.crystalScale[i * 3 + 2] * k);
                }
            }
            if (r.clockTr != null)
                r.clockTr.localPosition = new Vector3(0f, 1.85f + 0.07f * Mathf.Sin(time * 3f + r.pos.x), 0f);
        }

        // ================================================================== dano y rotura
        void DmgText(Rk t, double dmg, bool crit, Color col)
        {
            texts.MaxActive = G.Eco ? 14 : 40;
            Vector3 basePos = W.V3(t.pos, t.r * 1.55f + 0.3f);
            if (t.kind == 2)
                basePos = W.V3(t.pos + W.R2 * (Rf(-0.55f, 0.55f) * t.r), t.r * 1.7f);
            if (!crit)
            {
                if (texts.TryMerge(t, dmg)) return;
            }
            Vector3 vel = new Vector3(Rf(-24f, 24f), 140f, 0f) * W.PX;
            texts.Spawn(BigNum.Fmt(dmg), basePos + new Vector3(Rf(-14f, 14f) * W.PX, 0f, 0f), vel, 0.8f, crit ? 40f : 30f,
                crit ? Biomes.H("ffe14d") : col, 0f, t, dmg, true);
            if (crit)
                texts.Spawn("¡CRÍTICO!", basePos + Vector3.up * (42f * W.PX), new Vector3(0f, 100f * W.PX, 0f), 0.7f, 24f,
                    Biomes.H("ff7a3a"), Rf(-8.5f, 8.5f), null, 0, false);
        }

        /// <summary>Color de los pedazos de una roca segun su tipo.</summary>
        Color ChipColor(Rk t)
        {
            if (t.kind == 2) return Biomes.CrystalM;
            if (t.kind == 4) return Biomes.WoodM;
            if (t.hard) return Biomes.Steel;
            return pal.Rock;
        }

        /// <summary>Escala de los efectos segun el tamano de la roca.</summary>
        static float FxScale(Rk t) { return Mathf.Clamp(t.r / 0.55f, 0.7f, 2.4f); }

        /// <summary>Capturas: deja al jefe ("boss") o al cofre ("chest") con la fraccion de vida indicada (0 = romper).</summary>
        public bool DebugSetSpecialHp(string which, double frac)
        {
            Rk t = which == "boss" ? boss : chest;
            if (t == null || t.dead) return false;
            Damage(t, System.Math.Max(t.hp - t.maxHp * frac, 0.0) + (frac <= 0 ? 1.0 : 0.0), false, t.tr.position, Color.white);
            return true;
        }

        void Damage(Rk t, double dmg, bool crit, Vector3 at, Color col)
        {
            if (t.dead) return;
            t.hp -= dmg;
            t.shake = 1f;
            DmgText(t, dmg, crit, col);
            // golpe: chispas + rebote de escala + destello + sacudida leve (con tope de frecuencia para no saturar)
            float fs = FxScale(t);
            if (crit) FxApi.Play("crit", at, Biomes.H("ffe14d"), fs);
            else FxApi.Play("hit_spark", at, default(Color), fs);
            Juice.Punch(t.tr, crit ? 0.2f : 0.1f, crit ? 0.3f : 0.2f);
            Juice.Flash(t.go, crit ? 0.12f : 0.08f);
            if (hitFxCd <= 0f)
            {
                hitFxCd = G.Eco ? 0.12f : 0.05f;
                Juice.Shake((crit ? 0.07f : 0.025f), crit ? 0.14f : 0.07f);
                FxApi.Play("debris", at, ChipColor(t), fs * 0.6f);
            }
            if (t.hp > 0 && t.kind == 2)
            {
                float f = (float)(t.hp / t.maxHp);
                int ph = f > 0.66f ? 0 : (f > 0.33f ? 1 : 2);
                while (t.phase < ph)
                {
                    t.phase++;
                    BossPhase(t);
                }
            }
            if (t.hp <= 0) Break(t);
        }

        /// <summary>La geoda se agrieta al 66% y al 33%: estallido de cristales, destello, camara lenta y crece.</summary>
        void BossPhase(Rk t)
        {
            t.glint = 1f;
            Shake(18f);
            Juice.SlowMo(0.3f, 0.25f);
            Juice.HitStop(0.05f);
            Juice.ZoomPunch(0.05f, 0.25f);
            Juice.Vibrate(40);
            Vector3 c = W.V3(t.pos, t.r * 1.0f);
            FxApi.Play("boss_phase", c, Biomes.CrystalPink, FxScale(t));
            FxApi.Play("rock_break", c, Biomes.CrystalM, FxScale(t) * 0.8f);
            texts.Spawn("¡Se agrieta!", W.V3(t.pos, t.r * 1.9f), new Vector3(0f, 40f * W.PX, 0f), 0.9f, 30f, Biomes.CrystalL, Rf(-4.5f, 4.5f), null, 0, false);
            Sfx.Play("boss_phase", -2f);
            EmitBigMoment("boss_phase");
        }

        void Break(Rk t)
        {
            if (t.kind == 4)
            {
                BreakChest(t);
                return;
            }
            t.dead = true;
            HideRock(t);
            double gold = t.gold * G.GoldMultNow();
            if (playMode) gold *= 1.0 + System.Math.Min(combo, 20) * 0.05;
            Burst(t.pos, t.r, 0, t.hard, t.kind == 1);
            int gsize = t.kind == 0 ? 34 : 44;
            if (goldK > 0.3f) gsize = (int)(gsize * (1f + 0.3f * goldK));
            texts.Spawn(BigNum.Fmt(gold), W.V3(t.pos, t.r * 1.3f + 0.45f), new Vector3(0f, 90f * W.PX, 0f), 1.1f, gsize,
                Biomes.GoldRich, 0f, null, 0, false);
            Vector3 center = W.V3(t.pos, t.r * 0.6f);
            FxApi.Play("coin_burst", center, default(Color), FxScale(t));
            if (t.kind == 1) FxApi.Play("gem_sparkle", center, Biomes.GoldRich, FxScale(t));
            Vector2 sp = ScreenPos(center);
            if (playMode)
            {
                PlayGold += gold;
                PlayProgress = Mathf.Min(1f, PlayProgress + (t.kind == 1 ? 3f : 1f) / playRocksNeeded);
                G.TotalRocks += 1;
                G.AddDaily("rocks", 1);
                if (t.kind == 3)
                {
                    EmitTimeBonus(3f);
                    texts.Spawn("+3s", W.V3(t.pos, t.r * 1.3f + 1.1f), new Vector3(0f, 80f * W.PX, 0f), 1.1f, 40f,
                        Biomes.H("7fe0ff"), 0f, null, 0, false);
                    FxApi.Play("ring", W.V3(t.pos, 0.05f), Biomes.H("7fe0ff"), 1.6f);
                    Sfx.Play("tick", -2f, 1.3f);
                }
                SetPlay(PlayProgress, PlayGold);
            }
            EmitRockBroken(sp, gold, t.kind == 2, t.kind == 1 || (goldK > 0.5f && !playMode));
            if (!playMode) G.OnRockBroken(t.kind == 2);
            if (t.kind == 2)
            {
                BossBreak(t, sp);
            }
            else
            {
                Shake(3f + 1.5f * t.tier);
                Juice.HitStop(0.03f);
                if (t.tier == 2)
                {
                    FxApi.Play("ring", W.V3(t.pos, 0.05f), new Color(1f, 0.95f, 0.8f), 1.5f);
                    Juice.Vibrate(15);
                }
            }
            Sfx.Play("break", -3f);
        }

        void BossBreak(Rk t, Vector2 sp)
        {
            Shake(24f);
            Juice.SlowMo(0.3f, 0.5f);
            Juice.ZoomPunch(0.08f, 0.4f);
            Juice.Vibrate(60);
            boss = null;
            HideBossLabel();
            Vector3 c = W.V3(t.pos, t.r * 0.9f);
            FxApi.Play("boss_break", c, Biomes.CrystalL, FxScale(t));
            FxApi.Play("rock_break", c, Biomes.CrystalM, FxScale(t));
            FxApi.Play("gem_sparkle", c, Biomes.CrystalCyan, FxScale(t));
            int gems = G.BossGems();
            texts.Spawn("+" + gems, W.V3(t.pos, t.r * 2.3f), new Vector3(0f, 70f * W.PX, 0f), 1.4f, 52f, Biomes.H("7fe0ff"), 0f, null, 0, false);
            Sfx.Play("boss_phase", -3f, 0.8f);
            EmitBossBroken(sp, gems);
            EmitBigMoment("boss_break");
        }

        void BreakChest(Rk t)
        {
            t.dead = true;
            HideRock(t);
            if (chest == t) chest = null;
            int gems = G.ChestOpened();
            Vector3 c = W.V3(t.pos, t.r * 0.7f);
            Vector2 sp = ScreenPos(c);
            LaunchLid(W.V3(t.pos, t.r * 0.86f), t.r);
            FxApi.Play("chest_open", c, Biomes.GoldRich, FxScale(t));
            FxApi.Play("coin_burst", c, default(Color), FxScale(t) * 1.3f);
            FxApi.Play("rock_break", c, Biomes.WoodM, FxScale(t) * 0.8f);
            FxApi.Play("gem_sparkle", c, Biomes.CrystalCyan, FxScale(t));
            texts.Spawn("+" + gems, W.V3(t.pos, t.r * 1.9f), new Vector3(0f, 80f * W.PX, 0f), 1.3f, 48f, Biomes.H("7fe0ff"), 0f, null, 0, false);
            Shake(11f);
            Juice.HitStop(0.05f);
            Juice.Vibrate(40);
            Sfx.Play("chest", -1f);
            EmitChestBroken(sp, gems);
            EmitBigMoment("chest");
        }

        /// <summary>Pedazos y polvo al desaparecer una roca (el estallido principal lo hace el kit de efectos).</summary>
        void Burst(Vector2 p, float r, int unused, bool hard, bool rich)
        {
            Vector3 wp = W.V3(p, r * 0.4f);
            float sc = Mathf.Clamp(r / 0.55f, 0.7f, 2.4f);
            FxApi.Play("rock_break", wp, hard ? Biomes.Steel : pal.Rock, sc);
            FxApi.Play("dust", W.V3(p, 0.1f), pal.Decor2, sc);
        }
    }
}
