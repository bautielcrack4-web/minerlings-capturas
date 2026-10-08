using UnityEngine;
using UnityEngine.Rendering;

namespace Mineros.Fx
{
    /// <summary>
    /// Traduce los emisores de FxSpecs (datos puros) a ParticleSystem (Shuriken) configurados por codigo.
    /// Build arma la instancia una sola vez; Apply (por cada Play) solo escribe valores de struct: sin basura.
    /// </summary>
    internal static class FxBuilder
    {
        const float Deg = Mathf.Deg2Rad;

        static Color Rgba(uint c)
        {
            return new Color(((c >> 24) & 255) / 255f, ((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f);
        }

        static Color Shade(Color c, float s)
        {
            if (s > 0f) return new Color(c.r + (1f - c.r) * s, c.g + (1f - c.g) * s, c.b + (1f - c.b) * s, 1f);
            float k = 1f + s;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        // ---------------------------------------------------------------- curvas
        // curva lineal exacta a partir de pares (t, v)
        static AnimationCurve LinearCurve(float[] kv)
        {
            int n = kv.Length / 2;
            var keys = new Keyframe[n];
            for (int i = 0; i < n; i++)
            {
                float t = kv[2 * i], v = kv[2 * i + 1];
                float inT = 0f, outT = 0f;
                if (i > 0) inT = (v - kv[2 * i - 1]) / Mathf.Max(1e-5f, t - kv[2 * i - 2]);
                if (i < n - 1) outT = (kv[2 * i + 3] - v) / Mathf.Max(1e-5f, kv[2 * i + 2] - t);
                keys[i] = new Keyframe(t, v, inT, outT);
            }
            return new AnimationCurve(keys);
        }

        static float Eval(float[] kv, float t)
        {
            if (kv == null || kv.Length < 2) return 1f;
            int n = kv.Length / 2;
            if (t <= kv[0]) return kv[1];
            for (int i = 0; i < n - 1; i++)
            {
                float t0 = kv[2 * i], v0 = kv[2 * i + 1], t1 = kv[2 * i + 2], v1 = kv[2 * i + 3];
                if (t <= t1) return v0 + (v1 - v0) * (t - t0) / Mathf.Max(1e-5f, t1 - t0);
            }
            return kv[kv.Length - 1];
        }

        // curva de giro en 3D (moneda/confeti): |cos| con minimo, multiplicada por la curva de tamano
        static AnimationCurve FlipCurve(float[] sizeCurve, float cycles)
        {
            const int N = 48;
            var keys = new Keyframe[N + 1];
            var v = new float[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                v[i] = Mathf.Max(0.14f, Mathf.Abs(Mathf.Cos(Mathf.PI * 2f * cycles * t))) * Eval(sizeCurve, t);
            }
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                float inT = i > 0 ? (v[i] - v[i - 1]) * N : 0f;
                float outT = i < N ? (v[i + 1] - v[i]) * N : 0f;
                keys[i] = new Keyframe(t, v[i], inT, outT);
            }
            return new AnimationCurve(keys);
        }

        static Gradient MakeGradient(float[] kv)
        {
            int n = kv.Length / 5;
            var ck = new GradientColorKey[n];
            var ak = new GradientAlphaKey[n];
            for (int i = 0; i < n; i++)
            {
                float t = kv[5 * i];
                ck[i] = new GradientColorKey(new Color(kv[5 * i + 1], kv[5 * i + 2], kv[5 * i + 3], 1f), t);
                ak[i] = new GradientAlphaKey(kv[5 * i + 4], t);
            }
            var g = new Gradient();
            g.SetKeys(ck, ak);
            return g;
        }

        static ParticleSystem.MinMaxGradient MakePalette(uint[] pal)
        {
            int n = pal.Length;
            var ck = new GradientColorKey[n];
            var ak = new GradientAlphaKey[1];
            for (int i = 0; i < n; i++)
            {
                Color c = Rgba(pal[i]);
                c.a = 1f;
                ck[i] = new GradientColorKey(c, (i + 1f) / n);
            }
            ak[0] = new GradientAlphaKey(1f, 0f);
            var g = new Gradient();
            g.mode = GradientMode.Fixed;
            g.SetKeys(ck, ak);
            var mm = new ParticleSystem.MinMaxGradient(g);
            mm.mode = ParticleSystemGradientMode.RandomColor;
            return mm;
        }

        // ---------------------------------------------------------------- armado
        internal static FxInstance Build(FxEffect spec, Transform parent)
        {
            int n = spec.Emitters.Length;
            var root = new GameObject("fx_" + spec.Kind);
            root.SetActive(false);                 // nada arranca hasta el primer Play
            root.transform.SetParent(parent, false);

            var inst = new FxInstance
            {
                Spec = spec, Root = root, RootTr = root.transform, Ps = new ParticleSystem[n],
                Rend = new ParticleSystemRenderer[n], Playing = new bool[n], PaletteGrad = new ParticleSystem.MinMaxGradient[n]
            };

            // quien dispara a cada sub-emisor (para colgarlo de su padre)
            var par = new int[n];
            for (int i = 0; i < n; i++) par[i] = -1;
            for (int j = 0; j < n; j++)
            {
                var e = spec.Emitters[j];
                if (e.SubCollision != null) foreach (int s in e.SubCollision) if (par[s] < 0) par[s] = j;
                if (e.SubDeath != null) foreach (int s in e.SubDeath) if (par[s] < 0) par[s] = j;
            }

            var gos = new GameObject[n];
            for (int i = 0; i < n; i++)
            {
                gos[i] = new GameObject(spec.Emitters[i].Name + i);
                gos[i].transform.SetParent(root.transform, false);
            }
            for (int i = 0; i < n; i++)
            {
                if (par[i] >= 0) gos[i].transform.SetParent(gos[par[i]].transform, false);
                var ps = gos[i].AddComponent<ParticleSystem>();
                inst.Ps[i] = ps;
                inst.Rend[i] = gos[i].GetComponent<ParticleSystemRenderer>();
                ConfigureStatic(spec, spec.Emitters[i], i, ps, inst.Rend[i], inst);
            }
            for (int j = 0; j < n; j++)
            {
                var e = spec.Emitters[j];
                if (e.SubCollision == null && e.SubDeath == null) continue;
                var sub = inst.Ps[j].subEmitters;
                sub.enabled = true;
                if (e.SubCollision != null)
                    foreach (int s in e.SubCollision)
                        sub.AddSubEmitter(inst.Ps[s], ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, e.SubProb);
                if (e.SubDeath != null)
                    foreach (int s in e.SubDeath)
                        sub.AddSubEmitter(inst.Ps[s], ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing, 1f);
            }
            return inst;
        }

        static void ConfigureStatic(FxEffect spec, FxEmitter e, int idx, ParticleSystem ps, ParticleSystemRenderer r, FxInstance inst)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.useUnscaledTime = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.gravityModifier = 0f;
            main.startDelay = new ParticleSystem.MinMaxCurve(e.Delay);
            main.startLifetime = new ParticleSystem.MinMaxCurve(e.LifeMin, e.LifeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(e.RotMin * Deg, e.RotMax * Deg);
            main.maxParticles = MaxParticles(e);

            // ---- emision
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            int nb = e.Bursts == null ? 0 : e.Bursts.Length / 2;
            if (nb > 0)
            {
                var bursts = new ParticleSystem.Burst[nb];
                for (int b = 0; b < nb; b++) bursts[b] = new ParticleSystem.Burst(e.Bursts[2 * b], (short)Mathf.Max(1, Mathf.RoundToInt(e.Bursts[2 * b + 1])));
                em.SetBursts(bursts);
            }
            else em.burstCount = 0;

            // ---- forma (el eje +Z del emisor apunta hacia arriba: rotacion -90 en X)
            var sh = ps.shape;
            sh.enabled = true;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            sh.radiusThickness = 1f;
            sh.randomDirectionAmount = 0f;
            sh.sphericalDirectionAmount = 0f;
            switch (e.Shape)
            {
                case FxShape.Point:
                    sh.shapeType = ParticleSystemShapeType.Sphere;
                    sh.radius = 0.0001f;
                    break;
                case FxShape.Sphere:
                    sh.shapeType = ParticleSystemShapeType.Sphere;
                    sh.radius = Mathf.Max(0.0001f, e.Radius);
                    break;
                case FxShape.Hemisphere:
                    sh.shapeType = ParticleSystemShapeType.Hemisphere;
                    sh.radius = Mathf.Max(0.0001f, e.Radius);
                    break;
                case FxShape.Cone:
                    sh.shapeType = ParticleSystemShapeType.Cone;
                    sh.angle = e.Angle;
                    sh.radius = Mathf.Max(0.0001f, e.Radius);
                    sh.length = 1f;
                    break;
                case FxShape.Disc:
                    sh.shapeType = ParticleSystemShapeType.Circle;
                    sh.arc = 360f;
                    sh.radius = Mathf.Max(0.0001f, e.Radius);
                    break;
            }

            // ---- tamano
            if (e.SizeCurve != null || e.Flip > 0f)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                if (e.Flip > 0f)
                {
                    sol.separateAxes = true;
                    sol.x = new ParticleSystem.MinMaxCurve(1f, FlipCurve(e.SizeCurve, e.Flip), FlipCurve(e.SizeCurve, e.Flip * 1.4f));
                    var yz = e.SizeCurve != null ? LinearCurve(e.SizeCurve) : AnimationCurve.Linear(0f, 1f, 1f, 1f);
                    sol.y = new ParticleSystem.MinMaxCurve(1f, yz);
                    sol.z = new ParticleSystem.MinMaxCurve(1f, yz);
                }
                else
                {
                    sol.separateAxes = false;
                    sol.size = new ParticleSystem.MinMaxCurve(1f, LinearCurve(e.SizeCurve));
                }
            }

            // ---- color sobre la vida
            if (e.ColorCurve != null)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(MakeGradient(e.ColorCurve));
            }

            // ---- giro
            if (e.SpinMin != 0f || e.SpinMax != 0f)
            {
                var rol = ps.rotationOverLifetime;
                rol.enabled = true;
                rol.separateAxes = false;
                rol.z = new ParticleSystem.MinMaxCurve(e.SpinMin * Deg, e.SpinMax * Deg);
            }

            // ---- gravedad (fuerza en espacio mundo; el valor se escala en Apply)
            if (e.Gravity != 0f)
            {
                var fol = ps.forceOverLifetime;
                fol.enabled = true;
                fol.space = ParticleSystemSimulationSpace.World;
                fol.x = new ParticleSystem.MinMaxCurve(0f);
                fol.y = new ParticleSystem.MinMaxCurve(-e.Gravity);
                fol.z = new ParticleSystem.MinMaxCurve(0f);
            }

            // ---- arrastre
            if (e.Drag > 0f)
            {
                var lv = ps.limitVelocityOverLifetime;
                lv.enabled = true;
                lv.separateAxes = false;
                lv.space = ParticleSystemSimulationSpace.World;
                lv.limit = new ParticleSystem.MinMaxCurve(1000f);
                lv.dampen = 0f;
                lv.drag = new ParticleSystem.MinMaxCurve(e.Drag);
                lv.multiplyDragByParticleSize = false;
                lv.multiplyDragByParticleVelocity = false;
            }

            // ---- turbulencia
            if (e.Noise > 0f)
            {
                var nz = ps.noise;
                nz.enabled = true;
                nz.frequency = 0.4f;
                nz.scrollSpeed = 0.5f;
                nz.damping = true;
                nz.quality = ParticleSystemNoiseQuality.Low;
                nz.strength = new ParticleSystem.MinMaxCurve(e.Noise);
            }

            // ---- colision con el plano del suelo (y = GroundY): rebote simple
            if (e.Bounce > 0f)
            {
                var c = ps.collision;
                c.enabled = true;
                c.type = ParticleSystemCollisionType.Planes;
                c.mode = ParticleSystemCollisionMode.Collision3D;
                c.SetPlane(0, FxRuntime.GroundPlane);
                c.dampen = new ParticleSystem.MinMaxCurve(e.Dampen);
                c.bounce = new ParticleSystem.MinMaxCurve(e.Bounce);
                c.lifetimeLoss = 0f;
                c.minKillSpeed = 0f;
                c.maxKillSpeed = 1000f;
                c.radiusScale = 0.5f;
                c.quality = ParticleSystemCollisionQuality.High;
                c.sendCollisionMessages = false;
                c.enableDynamicColliders = false;
            }

            // ---- sub-emisores: no emiten solos, solo cuando los dispara otro emisor
            if (e.Sub)
            {
                em.rateOverTime = 0f;
            }

            // ---- paleta de colores al azar
            if (e.Palette != null) inst.PaletteGrad[idx] = MakePalette(e.Palette);

            // ---- render
            r.sharedMaterial = FxAssets.Mat(e.Tex, e.Additive, e.Overlay);
            r.sortingOrder = idx;
            r.sortMode = ParticleSystemSortMode.None;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            r.alignment = ParticleSystemRenderSpace.View;
            switch (e.Render)
            {
                case FxRender.Stretch:
                    r.renderMode = ParticleSystemRenderMode.Stretch;
                    r.lengthScale = e.StretchLen;
                    r.velocityScale = e.StretchSpeed;
                    r.cameraVelocityScale = 0f;
                    break;
                case FxRender.Flat:
                    r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                    break;
                default:
                    r.renderMode = ParticleSystemRenderMode.Billboard;
                    break;
            }
        }

        static int MaxParticles(FxEmitter e)
        {
            if (e.Sub) return 48;
            float total = 4f;
            if (e.Bursts != null) for (int i = 1; i < e.Bursts.Length; i += 2) total += e.Bursts[i];
            if (e.Rate > 0f) total += Mathf.Ceil(e.Rate * (e.LifeMax + 0.1f));
            return Mathf.Clamp((int)total, 8, 500);
        }

        // ---------------------------------------------------------------- por cada Play / Attach
        static int CountFor(float n, float mult)
        {
            float v = n * mult;
            int k = (int)v;
            if (Random.value < v - k) k++;
            return k;
        }

        internal static void Apply(FxInstance inst, Color tint, float scale, float inten, bool attached, float posY)
        {
            var spec = inst.Spec;
            bool eco = Fx.Eco;
            bool hasTint = tint.a > 0f;
            float mult = inten * (eco ? 0.5f : 1f);
            FxRuntime.GroundPlane.position = new Vector3(0f, FxRuntime.GroundY, 0f);

            for (int i = 0; i < spec.Emitters.Length; i++)
            {
                var e = spec.Emitters[i];
                var ps = inst.Ps[i];
                bool skip = eco && e.Eco0;
                inst.Playing[i] = !skip && !e.Sub;
                ps.Clear(false);

                var main = ps.main;
                main.startSize = new ParticleSystem.MinMaxCurve(e.SizeMin * scale, e.SizeMax * scale);
                main.startSpeed = new ParticleSystem.MinMaxCurve(e.SpeedMin * scale, e.SpeedMax * scale);
                main.startColor = ColorFor(inst, i, e, tint, hasTint);
                if (!e.Sub)
                {
                    main.loop = attached && spec.Loop;
                    main.duration = DurationOf(spec, e);
                }

                var em = ps.emission;
                float emult = (skip && e.Sub) ? 0f : mult;      // sub-emisores de adorno: nada en modo Eco
                em.rateOverTime = e.Rate > 0f ? e.Rate * emult : 0f;
                if (e.Bursts != null)
                {
                    int nb = e.Bursts.Length / 2;
                    for (int b = 0; b < nb; b++)
                    {
                        int c = CountFor(e.Bursts[2 * b + 1], emult);
                        em.SetBurst(b, new ParticleSystem.Burst(e.Bursts[2 * b], (short)c));
                    }
                }

                var sh = ps.shape;
                switch (e.Shape)
                {
                    case FxShape.Point: break;
                    default: sh.radius = Mathf.Max(0.0001f, e.Radius * scale); break;
                }
                float y = e.Ground ? (FxRuntime.GroundY + e.Y * scale) - posY : e.Y * scale;
                sh.position = new Vector3(0f, y, 0f);

                if (e.Gravity != 0f)
                {
                    var fol = ps.forceOverLifetime;
                    fol.y = new ParticleSystem.MinMaxCurve(-e.Gravity * scale);
                }
                if (e.Noise > 0f)
                {
                    var nz = ps.noise;
                    nz.strength = new ParticleSystem.MinMaxCurve(e.Noise * scale);
                }
                if (inst.Playing[i]) ps.Play(false);
            }
        }

        static float DurationOf(FxEffect spec, FxEmitter e)
        {
            float last = 0f;
            if (e.Bursts != null) for (int i = 0; i < e.Bursts.Length; i += 2) last = Mathf.Max(last, e.Bursts[i]);
            float d = 0.1f;
            if (e.Rate > 0f) d = e.Duration > 0f ? e.Duration : (spec.Loop ? spec.PlayDuration : 0.1f);
            return Mathf.Max(d, last + 0.02f, 0.1f);
        }

        static ParticleSystem.MinMaxGradient ColorFor(FxInstance inst, int i, FxEmitter e, Color tint, bool hasTint)
        {
            if (e.Palette != null) return inst.PaletteGrad[i];
            Color a = Rgba(e.ColA), b = Rgba(e.ColB);
            if (hasTint && e.TintK > 0f)
            {
                Color ta = Shade(tint, e.ShadeA), tb = Shade(tint, e.ShadeB);
                ta.a = a.a; tb.a = b.a;
                a = Color.Lerp(a, ta, e.TintK);
                b = Color.Lerp(b, tb, e.TintK);
            }
            return new ParticleSystem.MinMaxGradient(a, b);
        }
    }
}
