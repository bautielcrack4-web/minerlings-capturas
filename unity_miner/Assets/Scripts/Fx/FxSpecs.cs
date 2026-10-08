using System.Collections.Generic;

namespace Mineros.Fx
{
    internal enum FxShape { Point, Sphere, Hemisphere, Cone, Disc }
    internal enum FxRender { Billboard, Stretch, Flat }

    /// <summary>
    /// Un emisor de particulas descrito como DATOS PUROS (sin UnityEngine). Los construye FxBuilder (Shuriken) y los
    /// simula la vista previa en Python (unity_tools/fx_preview), asi lo que se ve en la hoja de contacto es lo que se
    /// arma en el juego. Unidades: 1 = el minero mide ~1.2. Colores 0xRRGGBBAA. Curvas planas: (t, v, t, v, ...).
    /// Gradientes planos: (t, r, g, b, a, ...) y se MULTIPLICAN por el color de arranque de la particula.
    /// </summary>
    internal sealed class FxEmitter
    {
        public string Name = "e";
        public FxTex Tex = FxTex.SoftCircle;
        public bool Additive;
        public FxRender Render = FxRender.Billboard;
        public bool Overlay;               // se dibuja encima de la geometria (destellos, estrellas)
        public bool Ground;                // nace a ras del suelo (anillos, polvo)
        public float Y;                    // altura de nacimiento (sobre pos, o sobre el suelo si Ground)
        public float Delay;                // retraso del emisor (s)
        public float Duration;             // emision continua (Rate) durante este tiempo; 0 = solo rafagas
        public float[] Bursts;             // pares (t, cantidad)
        public float Rate;                 // particulas por segundo
        public bool Sub;                   // sub-emisor: lo dispara otro emisor, no arranca solo
        public float LifeMin = 0.5f, LifeMax = 0.5f;
        public float SpeedMin, SpeedMax;
        public FxShape Shape = FxShape.Point;
        public float Radius;               // radio del emisor
        public float Angle = 25f;          // semiangulo del cono (grados)
        public float SizeMin = 0.2f, SizeMax = 0.2f;
        public float[] SizeCurve;          // multiplicador de tamano sobre la vida
        public float StretchLen = 1.2f, StretchSpeed = 0.035f;   // solo Render.Stretch
        public float Gravity;              // u/s^2 hacia abajo (negativo = sube)
        public float Drag;                 // arrastre lineal (1/s)
        public float RotMin, RotMax;       // rotacion inicial (grados)
        public float SpinMin, SpinMax;     // giro (grados/s)
        public uint ColA = 0xFFFFFFFF, ColB = 0xFFFFFFFF;       // se elige al azar entre A y B
        public uint[] Palette;             // si no es null: color al azar de la lista (ignora A/B)
        public float TintK;                // cuanto manda el tinte del parametro (0 nada, 1 todo)
        public float ShadeA, ShadeB;       // con tinte: aclara (+) u oscurece (-) el color A y B
        public float[] ColorCurve;         // gradiente sobre la vida (multiplica)
        public float Flip;                 // giro en 3D (moneda/confeti): ciclos por vida; 0 = sin giro
        public float Bounce;               // 0 = sin colision con el suelo
        public float Dampen = 0.3f;
        public float Noise;                // turbulencia suave
        public int[] SubCollision;         // sub-emisores al chocar con el suelo
        public float SubProb = 1f;
        public int[] SubDeath;             // sub-emisores al morir
        public bool Eco0;                  // no se emite en modo Eco
    }

    internal sealed class FxEffect
    {
        public string Kind;
        public int Priority;               // 0 menor, 1 medio, 2 grande ("foco grande")
        public bool Loop;                  // continuo al hacer Attach
        public float PlayDuration = 1f;    // si es continuo y se lanza con Play: cuanto emite
        public int MaxInstances = 6;
        public float Zoom = 1f;            // solo vista previa
        public FxEmitter[] Emitters;
    }

    internal static class FxLib
    {
        // ---- colores de la biblia de arte ----
        const uint GOLD = 0xFFCC33FF, GOLD_L = 0xFFD84AFF, WARM = 0xFFF3C4FF, ORANGE = 0xFFAE4AFF, EMBER = 0xFF6A2AFF;
        const uint GEM = 0x4FC3F7FF, GEM_L = 0x8FE0FFFF, CRY_L = 0x6EF0FFFF, CRY_PINK = 0xFF7AD9FF, ICE = 0xE8FFFFFF;
        const uint ROCK = 0x9AA0A6FF, ROCK_D = 0x6F757CFF, DUST = 0xDCCFA8FF, DUST_D = 0xC4B48AFF;
        const uint WOOD_M = 0x9A6A3CFF, WOOD_D = 0x5E3B22FF;

        // ---- curvas de tamano ----
        static readonly float[] POP = { 0f, 0.15f, 0.12f, 1.25f, 0.28f, 0.92f, 0.42f, 1.04f, 1f, 0f };
        static readonly float[] POP_FAST = { 0f, 0.2f, 0.16f, 1.2f, 0.4f, 0.8f, 1f, 0f };
        static readonly float[] GROW = { 0f, 0.55f, 0.3f, 1f, 1f, 1.5f };
        static readonly float[] GROW_BIG = { 0f, 0.5f, 0.25f, 1f, 1f, 1.35f };
        static readonly float[] SHRINK_END = { 0f, 1f, 0.7f, 1f, 1f, 0f };
        static readonly float[] SHRINK = { 0f, 1f, 1f, 0.15f };
        static readonly float[] EXPAND = { 0f, 0.12f, 0.18f, 0.62f, 0.5f, 0.9f, 1f, 1f };
        static readonly float[] COIN_SIZE = { 0f, 0.3f, 0.08f, 1.15f, 0.16f, 1f, 0.82f, 1f, 1f, 0f };
        static readonly float[] GLINT = { 0f, 0f, 0.22f, 1.1f, 0.5f, 0.85f, 1f, 0f };
        static readonly float[] BUBBLE = { 0f, 0.2f, 0.3f, 0.9f, 0.85f, 1.1f, 1f, 1.45f };
        static readonly float[] BREATH = { 0f, 0.8f, 0.5f, 1.12f, 1f, 0.8f };
        static readonly float[] PAPER = { 0f, 0.6f, 0.08f, 1f, 0.88f, 1f, 1f, 0.3f };

        // ---- gradientes de color/alpha (t, r, g, b, a) ----
        static readonly float[] FADE_OUT = { 0f, 1, 1, 1, 1, 0.55f, 1, 1, 1, 1, 1f, 1, 1, 1, 0 };
        static readonly float[] FADE_SOON = { 0f, 1, 1, 1, 1, 0.25f, 1, 1, 1, 0.8f, 1f, 1, 1, 1, 0 };
        static readonly float[] FADE_LATE = { 0f, 1, 1, 1, 1, 0.8f, 1, 1, 1, 1, 1f, 1, 1, 1, 0 };
        static readonly float[] FADE_IO = { 0f, 1, 1, 1, 0, 0.18f, 1, 1, 1, 1, 0.7f, 1, 1, 1, 1, 1f, 1, 1, 1, 0 };
        static readonly float[] DUST_FADE = { 0f, 1, 1, 1, 0, 0.1f, 1, 1, 1, 0.85f, 1f, 1, 1, 1, 0 };
        static readonly float[] SMOKE_FADE = { 0f, 1, 1, 1, 0, 0.15f, 1, 1, 1, 0.55f, 1f, 1, 1, 1, 0 };
        static readonly float[] FIRE = { 0f, 1, 1, 1, 0, 0.06f, 1, 1, 0.8f, 1, 0.4f, 1, 0.55f, 0.35f, 1, 0.75f, 0.45f, 0.28f, 0.24f, 0.8f, 1f, 0.3f, 0.2f, 0.2f, 0 };
        static readonly float[] FLAME = { 0f, 1, 1, 1, 0, 0.12f, 1, 1, 0.8f, 0.9f, 0.55f, 1, 0.5f, 0.35f, 0.8f, 1f, 0.5f, 0.2f, 0.2f, 0 };

        static readonly uint[] PAPER_COLS = { 0xFF5A4EFF, 0xFFCC33FF, 0x5CC84AFF, 0x4FC3F7FF, 0xB06EF0FF, 0xFF9A3AFF };
        static readonly uint[] CRYSTAL_COLS = { CRY_L, CRY_PINK, ICE, CRY_L, CRY_PINK };
        static readonly uint[] STAR_COLS = { GOLD, WARM, CRY_L, GOLD };

        static readonly Dictionary<string, FxEffect> map = new Dictionary<string, FxEffect>();
        static readonly List<FxEffect> all = new List<FxEffect>();
        internal static IList<FxEffect> All { get { Ensure(); return all; } }

        static void Ensure() { if (all.Count == 0) Build(); }
        internal static FxEffect Get(string kind)
        {
            Ensure();
            FxEffect e;
            return kind != null && map.TryGetValue(kind, out e) ? e : null;
        }

        static void Add(string kind, int prio, float zoom, params FxEmitter[] em)
        {
            var e = new FxEffect { Kind = kind, Priority = prio, Zoom = zoom, Emitters = em };
            e.MaxInstances = prio == 0 ? 10 : (prio == 1 ? 6 : 3);
            map[kind] = e;
            all.Add(e);
        }
        static void AddLoop(string kind, float playDur, float zoom, params FxEmitter[] em)
        {
            Add(kind, 0, zoom, em);
            var e = map[kind];
            e.Loop = true;
            e.PlayDuration = playDur;
            e.MaxInstances = 6;
        }

        // ---- piezas reutilizables ----
        static FxEmitter Flash(float size, float life, uint col, float delay = 0f, float y = 0f)
        {
            return new FxEmitter
            {
                Name = "flash", Tex = FxTex.SoftCircle, Additive = true, Overlay = true, Bursts = new float[] { 0f, 1f },
                Delay = delay, Y = y, LifeMin = life, LifeMax = life, SizeMin = size, SizeMax = size, ColA = col, ColB = col,
                SizeCurve = new float[] { 0f, 0.5f, 0.3f, 1f, 1f, 0.8f }, ColorCurve = FADE_SOON, TintK = 0.3f
            };
        }
        static FxEmitter Ring(float size, float life, uint col, float delay = 0f, float tintK = 0.7f, bool add = true)
        {
            return new FxEmitter
            {
                Name = "ring", Tex = FxTex.Ring, Additive = add, Render = FxRender.Flat, Ground = true, Y = 0.03f,
                Bursts = new float[] { 0f, 1f }, Delay = delay, LifeMin = life, LifeMax = life, SizeMin = size, SizeMax = size,
                SizeCurve = EXPAND, ColA = col, ColB = col, TintK = tintK, ColorCurve = FADE_SOON
            };
        }
        static FxEmitter Rays(float size, float life, uint col, float rot, float delay = 0f, float y = 0f)
        {
            return new FxEmitter
            {
                Name = "rays", Tex = FxTex.Cross, Additive = true, Overlay = true, Bursts = new float[] { 0f, 1f }, Delay = delay, Y = y,
                LifeMin = life, LifeMax = life, SizeMin = size, SizeMax = size, SizeCurve = GLINT, ColA = col, ColB = col,
                RotMin = rot, RotMax = rot, TintK = 0.25f, ColorCurve = FADE_SOON
            };
        }
        static FxEmitter Sparkle(float rate, float dur, float radius, float y, float sMin, float sMax, uint a, uint b, float rise = 0.8f)
        {
            return new FxEmitter
            {
                Name = "sparkle", Tex = FxTex.Cross, Additive = true, Overlay = true, Rate = rate, Duration = dur, Y = y,
                Shape = FxShape.Sphere, Radius = radius, LifeMin = 0.3f, LifeMax = 0.45f, SizeMin = sMin, SizeMax = sMax,
                SizeCurve = GLINT, Gravity = -rise, ColA = a, ColB = b, RotMin = 0, RotMax = 45, ColorCurve = FADE_OUT,
                TintK = 0.3f, Eco0 = true
            };
        }
        static FxEmitter Sparks(float n, float speedMin, float speedMax, float lifeMin, float lifeMax, uint a, uint b,
            FxShape shape = FxShape.Sphere, float radius = 0.1f, float angle = 70f, float grav = 10f, float y = 0f, float delay = 0f)
        {
            return new FxEmitter
            {
                Name = "sparks", Tex = FxTex.Spark, Additive = true, Render = FxRender.Stretch, Overlay = true,
                Bursts = new float[] { 0f, n }, Delay = delay, Y = y, Shape = shape, Radius = radius, Angle = angle,
                SpeedMin = speedMin, SpeedMax = speedMax, LifeMin = lifeMin, LifeMax = lifeMax, SizeMin = 0.09f, SizeMax = 0.15f, StretchLen = 1.2f, StretchSpeed = 0.035f,
                Gravity = grav, Drag = 1.6f, ColA = a, ColB = b, TintK = 0.2f, SizeCurve = SHRINK_END, ColorCurve = FADE_OUT
            };
        }
        static FxEmitter Chunks(float n, float speedMin, float speedMax, float sMin, float sMax, uint a, uint b, float tintK,
            float angle = 62f, float radius = 0.2f, float y = 0f, int[] sub = null, float delay = 0f)
        {
            return new FxEmitter
            {
                Name = "chunks", Tex = FxTex.Chunk, Bursts = new float[] { 0f, n }, Delay = delay, Y = y,
                Shape = FxShape.Cone, Radius = radius, Angle = angle, SpeedMin = speedMin, SpeedMax = speedMax,
                LifeMin = 0.9f, LifeMax = 1.3f, SizeMin = sMin, SizeMax = sMax, SizeCurve = SHRINK_END, Gravity = 17f, Drag = 0.2f,
                Bounce = 0.45f, Dampen = 0.3f, RotMin = 0, RotMax = 360, SpinMin = -520, SpinMax = 520,
                ColA = a, ColB = b, TintK = tintK, ShadeA = 0.08f, ShadeB = -0.3f, ColorCurve = FADE_LATE,
                SubCollision = sub, SubProb = 0.3f
            };
        }
        static FxEmitter TinyDust(float size)
        {
            return new FxEmitter
            {
                Name = "tinydust", Tex = FxTex.Dust, Sub = true, Bursts = new float[] { 0f, 1f }, LifeMin = 0.3f, LifeMax = 0.4f,
                SpeedMin = 0.2f, SpeedMax = 0.5f, Shape = FxShape.Sphere, Radius = 0.03f, SizeMin = size, SizeMax = size * 1.4f,
                SizeCurve = GROW, Gravity = -0.5f, ColA = DUST, ColB = DUST_D, TintK = 0.6f, ShadeA = 0.25f, ShadeB = 0f, ColorCurve = DUST_FADE,
                RotMin = 0, RotMax = 360, Eco0 = true
            };
        }
        static FxEmitter DustPuff(float n, float radius, float sMin, float sMax, float speedMin, float speedMax, float delay = 0f, float life = 0.65f, uint ca = DUST, uint cb = DUST_D)
        {
            return new FxEmitter
            {
                Name = "dust", Tex = FxTex.Dust, Bursts = new float[] { delay, n }, Ground = true, Y = 0.1f,
                Shape = FxShape.Disc, Radius = radius, SpeedMin = speedMin, SpeedMax = speedMax, LifeMin = life * 0.75f, LifeMax = life * 1.1f,
                SizeMin = sMin, SizeMax = sMax, SizeCurve = GROW, Gravity = -0.5f, Drag = 3f, RotMin = 0, RotMax = 360, SpinMin = -40, SpinMax = 40,
                ColA = ca, ColB = cb, TintK = 0.75f, ShadeA = 0.3f, ShadeB = 0.05f, ColorCurve = DUST_FADE
            };
        }
        static FxEmitter Coins(float n, float speedMin, float speedMax, float angle, float y = 0f, float delay = 0f, int[] sub = null, float sMin = 0.26f, float sMax = 0.36f)
        {
            return new FxEmitter
            {
                Name = "coins", Tex = FxTex.Coin, Bursts = new float[] { 0f, n }, Delay = delay, Y = y,
                Shape = FxShape.Cone, Radius = 0.12f, Angle = angle, SpeedMin = speedMin, SpeedMax = speedMax, LifeMin = 1.0f, LifeMax = 1.35f,
                SizeMin = sMin, SizeMax = sMax, SizeCurve = COIN_SIZE, Gravity = 20f, Bounce = 0.55f, Dampen = 0.25f, Flip = 3f,
                ColA = 0xFFFFFFFF, ColB = 0xFFEFC8FF, ColorCurve = FADE_LATE, SubCollision = sub, SubProb = 0.6f
            };
        }
        static FxEmitter GlintSub()
        {
            return new FxEmitter
            {
                Name = "glintsub", Tex = FxTex.Cross, Additive = true, Overlay = true, Sub = true, Bursts = new float[] { 0f, 1f },
                LifeMin = 0.22f, LifeMax = 0.22f, SizeMin = 0.32f, SizeMax = 0.42f, SizeCurve = GLINT, ColA = WARM, ColB = WARM,
                RotMin = 0, RotMax = 45, ColorCurve = FADE_OUT, Eco0 = true
            };
        }
        static FxEmitter Gems(float n, float speedMin, float speedMax, float angle, uint a, uint b, float y = 0f, float delay = 0f, float tintK = 0.9f, float sMin = 0.24f, float sMax = 0.34f, int[] sub = null)
        {
            return new FxEmitter
            {
                Name = "gems", Tex = FxTex.Gem, Bursts = new float[] { 0f, n }, Delay = delay, Y = y, Shape = FxShape.Cone, Radius = 0.1f, Angle = angle,
                SpeedMin = speedMin, SpeedMax = speedMax, LifeMin = 0.95f, LifeMax = 1.25f, SizeMin = sMin, SizeMax = sMax, SizeCurve = COIN_SIZE,
                Gravity = 16f, Bounce = 0.5f, Dampen = 0.3f, RotMin = -20, RotMax = 20, SpinMin = -120, SpinMax = 120, ColA = a, ColB = b,
                TintK = tintK, ColorCurve = FADE_LATE, SubCollision = sub, SubProb = 0.5f
            };
        }
        static FxEmitter Shards(float n, float speedMin, float speedMax, float sMin, float sMax, float delay = 0f, float y = 0f, float radius = 0.3f)
        {
            return new FxEmitter
            {
                Name = "shards", Tex = FxTex.Shard, Bursts = new float[] { 0f, n }, Delay = delay, Y = y, Shape = FxShape.Sphere, Radius = radius,
                SpeedMin = speedMin, SpeedMax = speedMax, LifeMin = 0.8f, LifeMax = 1.2f, SizeMin = sMin, SizeMax = sMax, SizeCurve = SHRINK_END,
                Gravity = 12f, Drag = 0.4f, Bounce = 0.32f, Dampen = 0.35f, RotMin = 0, RotMax = 360, SpinMin = -320, SpinMax = 320,
                Palette = CRYSTAL_COLS, TintK = 0f, ColorCurve = FADE_LATE
            };
        }
        static FxEmitter Smoke(float n, float size, float life, float delay, float rise, float alpha = 1f, uint col = 0x8A7E79FF)
        {
            return new FxEmitter
            {
                Name = "smoke", Tex = FxTex.Dust, Bursts = new float[] { delay, n }, Y = 0.15f, Shape = FxShape.Sphere, Radius = 0.25f,
                SpeedMin = 0.3f, SpeedMax = 0.9f, LifeMin = life * 0.8f, LifeMax = life * 1.1f, SizeMin = size * 0.8f, SizeMax = size,
                SizeCurve = GROW_BIG, Gravity = -rise, Drag = 1.2f, RotMin = 0, RotMax = 360, SpinMin = -25, SpinMax = 25, ColA = col, ColB = col,
                ColorCurve = alpha >= 0.99f ? SMOKE_FADE : new float[] { 0f, 1, 1, 1, 0, 0.15f, 1, 1, 1, 0.55f * alpha, 1f, 1, 1, 1, 0 },
                TintK = 0.3f, ShadeA = -0.1f, ShadeB = -0.2f, Eco0 = true
            };
        }
        static FxEmitter Fire(float n, float radius, float sMin, float sMax, float speedMin, float speedMax, float lifeMin, float lifeMax,
            FxShape shape = FxShape.Sphere, float y = 0f, float delay = 0f)
        {
            return new FxEmitter
            {
                Name = "fire", Tex = FxTex.Dust, Bursts = new float[] { 0f, n }, Delay = delay, Y = y, Shape = shape, Radius = radius, Angle = 60f,
                SpeedMin = speedMin, SpeedMax = speedMax, LifeMin = lifeMin, LifeMax = lifeMax, SizeMin = sMin, SizeMax = sMax, SizeCurve = GROW_BIG,
                Gravity = -1.5f, Drag = 2.5f, RotMin = 0, RotMax = 360, SpinMin = -90, SpinMax = 90, ColA = 0xFFE27AFF, ColB = 0xFFB347FF,
                TintK = 0.35f, ColorCurve = FIRE
            };
        }
        static FxEmitter Stars(float n, float speedMin, float speedMax, float sMin, float sMax, uint[] pal, float y = 0f, float delay = 0f, float drag = 2.8f, float grav = 3f)
        {
            return new FxEmitter
            {
                Name = "stars", Tex = FxTex.Star4, Additive = true, Overlay = true, Bursts = new float[] { 0f, n }, Delay = delay, Y = y,
                Shape = FxShape.Sphere, Radius = 0.1f, SpeedMin = speedMin, SpeedMax = speedMax, LifeMin = 0.65f, LifeMax = 1.0f,
                SizeMin = sMin, SizeMax = sMax, SizeCurve = POP, Gravity = grav, Drag = drag, RotMin = -25, RotMax = 25, SpinMin = -90, SpinMax = 90,
                Palette = pal, TintK = 0.15f, ColorCurve = FADE_OUT
            };
        }
        static FxEmitter Paper(float n, float speedMin, float speedMax, float angle, float sMin, float sMax, float lifeMin, float lifeMax, float y = 0f, float[] bursts = null)
        {
            return new FxEmitter
            {
                Name = "paper", Tex = FxTex.Confetti, Bursts = bursts ?? new float[] { 0f, n }, Y = y, Shape = FxShape.Cone, Radius = 0.3f, Angle = angle,
                SpeedMin = speedMin, SpeedMax = speedMax, LifeMin = lifeMin, LifeMax = lifeMax, SizeMin = sMin, SizeMax = sMax, SizeCurve = PAPER,
                Gravity = 7f, Drag = 1.6f, RotMin = 0, RotMax = 360, SpinMin = -360, SpinMax = 360, Flip = 1.6f, Noise = 0.6f,
                Palette = PAPER_COLS, ColorCurve = FADE_LATE
            };
        }

        static void Build()
        {
            // ---------------------------------------------------------------- golpes menores
            Add("hit_spark", 0, 2.1f,
                new FxEmitter
                {
                    Name = "star", Tex = FxTex.Star4, Additive = true, Overlay = true, Bursts = new float[] { 0f, 1f }, LifeMin = 0.16f, LifeMax = 0.16f,
                    SizeMin = 0.62f, SizeMax = 0.62f, SizeCurve = POP_FAST, RotMin = -12, RotMax = 12, ColA = WARM, ColB = WARM, TintK = 0.35f,
                    ColorCurve = FADE_SOON
                },
                Flash(0.7f, 0.11f, 0xFFE9A880),
                Sparks(6, 4f, 8f, 0.16f, 0.3f, 0xFFE6A0FF, 0xFFB347FF, FxShape.Hemisphere, 0.08f, 70f, 12f));

            Add("crit", 1, 1.7f,
                new FxEmitter
                {
                    Name = "star", Tex = FxTex.Star4, Additive = true, Overlay = true, Bursts = new float[] { 0f, 1f }, LifeMin = 0.34f, LifeMax = 0.34f,
                    SizeMin = 1.3f, SizeMax = 1.3f, SizeCurve = POP, RotMin = -8, RotMax = 8, SpinMin = -50, SpinMax = 50, ColA = GOLD_L, ColB = GOLD_L,
                    TintK = 0.25f, ColorCurve = FADE_LATE
                },
                Flash(1.2f, 0.12f, 0xFFF0B080),
                Rays(1.9f, 0.28f, WARM, 45f),
                new FxEmitter
                {
                    Name = "ring", Tex = FxTex.Ring, Additive = true, Overlay = true, Bursts = new float[] { 0f, 1f }, LifeMin = 0.32f, LifeMax = 0.32f,
                    SizeMin = 1.5f, SizeMax = 1.5f, SizeCurve = EXPAND, ColA = 0xFFF2C0CC, ColB = 0xFFF2C0CC, TintK = 0.3f, ColorCurve = FADE_SOON
                },
                Sparks(9, 4.5f, 8.5f, 0.25f, 0.45f, GOLD_L, WARM, FxShape.Sphere, 0.1f, 70f, 9f));

            Add("dust", 0, 2.4f, DustPuff(6, 0.3f, 0.4f, 0.6f, 1.1f, 2.0f));

            Add("debris", 0, 1.9f,
                new FxEmitter
                {
                    Name = "chips", Tex = FxTex.Chunk, Bursts = new float[] { 0f, 5f }, Shape = FxShape.Cone, Radius = 0.1f, Angle = 55f,
                    SpeedMin = 3.2f, SpeedMax = 6f, LifeMin = 0.75f, LifeMax = 1.05f, SizeMin = 0.11f, SizeMax = 0.19f, SizeCurve = SHRINK_END,
                    Gravity = 16f, Drag = 0.3f, Bounce = 0.42f, Dampen = 0.35f, RotMin = 0, RotMax = 360, SpinMin = -420, SpinMax = 420,
                    ColA = ROCK, ColB = ROCK_D, TintK = 1f, ShadeA = 0.1f, ShadeB = -0.28f, ColorCurve = FADE_LATE, SubCollision = new[] { 1 }, SubProb = 0.35f
                },
                TinyDust(0.2f));

            // ---------------------------------------------------------------- roca rota: fragmentos + polvo + destello
            Add("rock_break", 1, 1.8f,
                Chunks(11, 3.6f, 8f, 0.17f, 0.32f, ROCK, ROCK_D, 1f, 62f, 0.25f, 0.3f, new[] { 1 }),
                TinyDust(0.26f),
                DustPuff(7, 0.35f, 0.5f, 0.75f, 1.0f, 2.2f, 0f, 0.65f),
                Flash(1.3f, 0.11f, 0xFFF1C87A, 0f, 0.3f),
                new FxEmitter
                {
                    Name = "star", Tex = FxTex.Star4, Additive = true, Overlay = true, Bursts = new float[] { 0f, 1f }, Y = 0.3f, LifeMin = 0.2f, LifeMax = 0.2f,
                    SizeMin = 1.15f, SizeMax = 1.15f, SizeCurve = POP_FAST, RotMin = -10, RotMax = 10, ColA = WARM, ColB = WARM, TintK = 0.2f,
                    ColorCurve = FADE_SOON
                },
                new FxEmitter
                {
                    Name = "pebbles", Tex = FxTex.HardCircle, Bursts = new float[] { 0f, 8f }, Y = 0.3f, Shape = FxShape.Sphere, Radius = 0.15f,
                    SpeedMin = 4f, SpeedMax = 9f, LifeMin = 0.6f, LifeMax = 0.9f, SizeMin = 0.05f, SizeMax = 0.09f, SizeCurve = SHRINK_END, Gravity = 16f,
                    Bounce = 0.3f, Dampen = 0.4f, ColA = ROCK_D, ColB = ROCK, TintK = 1f, ShadeA = -0.25f, ShadeB = -0.05f, ColorCurve = FADE_LATE, Eco0 = true
                });

            // ---------------------------------------------------------------- tesoros
            Add("coin_burst", 1, 1.6f,
                Coins(8, 4.5f, 7.5f, 52f, 0.35f, 0f, new[] { 1 }),
                GlintSub(),
                Flash(1.2f, 0.18f, 0xFFCC3370, 0f, 0.35f),
                new FxEmitter
                {
                    Name = "twinkles", Tex = FxTex.Cross, Additive = true, Overlay = true, Bursts = new float[] { 0f, 3f, 0.1f, 2f }, Y = 0.35f,
                    Shape = FxShape.Sphere, Radius = 0.35f, LifeMin = 0.25f, LifeMax = 0.35f, SizeMin = 0.25f, SizeMax = 0.4f, SizeCurve = GLINT,
                    RotMin = 0, RotMax = 45, ColA = WARM, ColB = WARM, ColorCurve = FADE_OUT, Eco0 = true
                });

            Add("gem_sparkle", 1, 1.7f,
                Gems(5, 4f, 7f, 50f, GEM, GEM_L, 0.35f, 0f, 0.9f, 0.24f, 0.34f, new[] { 1 }),
                GlintSub(),
                Flash(1.2f, 0.22f, 0x4FC3F770, 0f, 0.35f),
                Sparkle(16f, 0.6f, 0.45f, 0.35f, 0.22f, 0.45f, 0xE2F6FFFF, 0xA9E4FFFF));

            Add("glint", 0, 3.0f,
                new FxEmitter
                {
                    Name = "glintA", Tex = FxTex.Cross, Additive = true, Overlay = true, Bursts = new float[] { 0f, 1f }, LifeMin = 0.4f, LifeMax = 0.4f,
                    SizeMin = 0.55f, SizeMax = 0.55f, SizeCurve = GLINT, SpinMin = 30, SpinMax = 30, ColA = 0xFFF6C8FF, ColB = 0xFFF6C8FF, TintK = 0.4f,
                    ColorCurve = FADE_OUT
                },
                new FxEmitter
                {
                    Name = "glintB", Tex = FxTex.Cross, Additive = true, Overlay = true, Bursts = new float[] { 0f, 1f }, LifeMin = 0.4f, LifeMax = 0.4f,
                    SizeMin = 0.38f, SizeMax = 0.38f, SizeCurve = GLINT, RotMin = 45, RotMax = 45, SpinMin = 30, SpinMax = 30, ColA = 0xFFF6C8FF,
                    ColB = 0xFFF6C8FF, TintK = 0.4f, ColorCurve = FADE_OUT
                });

            Add("ring", 0, 1.5f,
                Ring(1.8f, 0.45f, 0xFFF1D0FF, 0f, 0.8f),
                Ring(1.1f, 0.4f, 0xFFF1D0B3, 0.07f, 0.8f));

            // ---------------------------------------------------------------- explosiones
            Add("explosion", 1, 1.2f,
                Flash(2.4f, 0.14f, 0xFFF0B0AA, 0f, 0.3f),
                Fire(9, 0.2f, 0.7f, 1.1f, 1.8f, 4.2f, 0.35f, 0.55f, FxShape.Sphere, 0.35f),
                Sparks(16, 6f, 13f, 0.35f, 0.7f, 0xFFE27AFF, 0xFF8A2AFF, FxShape.Sphere, 0.15f, 70f, 10f, 0.35f),
                Smoke(5, 1.3f, 1.0f, 0.05f, 1f, 1f),
                Ring(3.0f, 0.45f, 0xFFC070FF, 0f, 0.5f),
                Chunks(6, 4f, 9f, 0.12f, 0.2f, 0x5A3A30FF, 0x3A2822FF, 0f, 70f, 0.2f, 0.3f));

            Add("confetti", 2, 0.7f,
                Paper(60, 8f, 14f, 50f, 0.2f, 0.3f, 1.8f, 2.6f, 0.2f, new float[] { 0f, 44f, 0.15f, 28f }),
                new FxEmitter
                {
                    Name = "twinkle", Tex = FxTex.Cross, Additive = true, Overlay = true, Rate = 30f, Duration = 0.5f, Y = 1.4f, Shape = FxShape.Sphere,
                    Radius = 1.2f, LifeMin = 0.25f, LifeMax = 0.25f, SizeMin = 0.15f, SizeMax = 0.3f, SizeCurve = GLINT, ColA = WARM, ColB = WARM,
                    RotMin = 0, RotMax = 45, ColorCurve = FADE_OUT, Eco0 = true
                });

            // ---------------------------------------------------------------- celebraciones
            Add("levelup_aura", 2, 1.5f,
                Ring(1.6f, 0.6f, GOLD_L, 0f, 0.5f),
                Ring(1.1f, 0.5f, WARM, 0.18f, 0.4f),
                Flash(1.8f, 0.35f, 0xFFD84A55, 0f, 0.3f),
                new FxEmitter
                {
                    Name = "motes", Tex = FxTex.SoftCircle, Additive = true, Rate = 26f, Duration = 0.9f, Ground = true, Y = 0.1f, Shape = FxShape.Cone,
                    Radius = 0.5f, Angle = 0f, SpeedMin = 1.8f, SpeedMax = 3.4f, LifeMin = 0.6f, LifeMax = 0.95f, SizeMin = 0.1f, SizeMax = 0.2f,
                    SizeCurve = SHRINK_END, ColA = GOLD_L, ColB = WARM, TintK = 0.4f, ColorCurve = FADE_OUT
                },
                new FxEmitter
                {
                    Name = "stars", Tex = FxTex.Star4, Additive = true, Overlay = true, Rate = 8f, Duration = 0.8f, Ground = true, Y = 0.1f, Shape = FxShape.Cone,
                    Radius = 0.5f, Angle = 0f, SpeedMin = 1.2f, SpeedMax = 2.4f, LifeMin = 0.5f, LifeMax = 0.8f, SizeMin = 0.2f, SizeMax = 0.34f,
                    SizeCurve = POP, SpinMin = -90, SpinMax = 90, ColA = GOLD, ColB = WARM, TintK = 0.3f, ColorCurve = FADE_OUT, Eco0 = true
                });

            Add("milestone", 2, 0.9f,
                Ring(3.0f, 0.8f, GOLD_L, 0f, 0.3f),
                Ring(2.0f, 0.7f, WARM, 0.1f, 0.2f),
                Flash(2.6f, 0.25f, 0xFFCC3380, 0f, 0.6f),
                Rays(2.4f, 0.4f, WARM, 45f, 0f, 0.6f),
                Stars(14, 4.5f, 8f, 0.3f, 0.5f, STAR_COLS, 0.6f),
                Sparks(20, 5f, 11f, 0.4f, 0.8f, GOLD, WARM, FxShape.Sphere, 0.1f, 70f, 8f, 0.6f),
                Coins(8, 5f, 9f, 60f, 0.6f, 0.05f, new[] { 7 }, 0.24f, 0.32f),
                GlintSub());

            // ---------------------------------------------------------------- meteoritos
            AddLoop("meteor_trail", 1.0f, 1.3f,
                new FxEmitter
                {
                    Name = "core", Tex = FxTex.SoftCircle, Additive = true, Overlay = true, Rate = 40f, LifeMin = 0.16f, LifeMax = 0.2f, SizeMin = 1.1f,
                    SizeMax = 1.3f, SizeCurve = SHRINK, ColA = 0xFFE9A8B3, ColB = 0xFFE9A8B3, TintK = 0.3f, ColorCurve = FADE_OUT
                },
                new FxEmitter
                {
                    Name = "fire", Tex = FxTex.Dust, Rate = 50f, Shape = FxShape.Sphere, Radius = 0.12f, SpeedMin = 0.3f, SpeedMax = 0.9f, LifeMin = 0.4f,
                    LifeMax = 0.6f, SizeMin = 0.6f, SizeMax = 0.85f, SizeCurve = SHRINK, Gravity = -1.5f, Drag = 2f, RotMin = 0, RotMax = 360,
                    SpinMin = -80, SpinMax = 80, ColA = 0xFFD060FF, ColB = 0xFF8A2AFF, TintK = 0.3f, ColorCurve = FIRE
                },
                new FxEmitter
                {
                    Name = "sparks", Tex = FxTex.Spark, Additive = true, Render = FxRender.Stretch, Rate = 24f, Shape = FxShape.Sphere, Radius = 0.15f,
                    SpeedMin = 1.2f, SpeedMax = 3f, LifeMin = 0.3f, LifeMax = 0.5f, SizeMin = 0.1f, SizeMax = 0.16f, SizeCurve = SHRINK_END, Gravity = 6f,
                    Drag = 1.2f, ColA = 0xFFE27AFF, ColB = 0xFFAE4AFF, TintK = 0.2f, ColorCurve = FADE_OUT
                },
                new FxEmitter
                {
                    Name = "smoke", Tex = FxTex.Smoke, Rate = 16f, Shape = FxShape.Sphere, Radius = 0.15f, SpeedMin = 0.2f, SpeedMax = 0.6f, LifeMin = 0.9f,
                    LifeMax = 1.3f, SizeMin = 0.7f, SizeMax = 1.0f, SizeCurve = GROW_BIG, Gravity = -0.6f, Drag = 1f, RotMin = 0, RotMax = 360, SpinMin = -25, SpinMax = 25,
                    ColA = 0x6B5A55FF, ColB = 0x6B5A55FF, ColorCurve = new float[] { 0f, 1, 1, 1, 0, 0.15f, 1, 1, 1, 0.45f, 1f, 1, 1, 1, 0 }, Eco0 = true
                });

            Add("meteor_impact", 1, 1.1f,
                Flash(2.8f, 0.15f, 0xFFE0A0C8, 0f, 0.3f),
                Fire(12, 0.3f, 0.9f, 1.4f, 2.2f, 5.5f, 0.4f, 0.65f, FxShape.Hemisphere, 0.2f),
                Sparks(20, 6f, 14f, 0.3f, 0.65f, 0xFFE27AFF, 0xFF8A2AFF, FxShape.Hemisphere, 0.1f, 70f, 11f, 0.2f),
                Ring(3.4f, 0.5f, ORANGE, 0f, 0.4f),
                Ring(2.1f, 0.45f, 0xFFE27AFF, 0.06f, 0.3f),
                DustPuff(10, 0.5f, 0.6f, 0.9f, 2.5f, 4f, 0.04f, 0.7f),
                Chunks(7, 4f, 10f, 0.12f, 0.22f, 0x5A3A30FF, 0x3A2822FF, 0f, 68f, 0.2f, 0.2f),
                Smoke(4, 1.4f, 1.2f, 0.1f, 1f, 0.9f));

            // ---------------------------------------------------------------- cofre y jefe
            Add("chest_open", 2, 1.0f,
                Flash(2.4f, 0.28f, 0xFFD84A88, 0f, 0.5f),
                Rays(3.0f, 0.4f, WARM, 0f, 0f, 0.5f),
                Rays(2.5f, 0.4f, GOLD_L, 45f, 0.04f, 0.5f),
                Chunks(9, 3.5f, 8f, 0.14f, 0.24f, WOOD_M, WOOD_D, 0f, 70f, 0.25f, 0.4f),
                Coins(10, 6f, 10f, 45f, 0.5f, 0f, new[] { 8 }),
                Gems(6, 5.5f, 9f, 45f, GEM, CRY_L, 0.5f, 0f, 0.8f, 0.25f, 0.34f, new[] { 8 }),
                Ring(2.8f, 0.6f, GOLD_L, 0f, 0.3f),
                Sparks(14, 5f, 9f, 0.35f, 0.65f, GOLD_L, WARM, FxShape.Sphere, 0.1f, 70f, 9f, 0.5f),
                GlintSub(),
                Sparkle(20f, 0.7f, 0.7f, 0.5f, 0.25f, 0.45f, GOLD_L, WARM));

            Add("boss_phase", 2, 0.85f,
                Flash(3.2f, 0.3f, 0xFF7AD980, 0f, 0.6f),
                Rays(3.2f, 0.35f, ICE, 0f, 0f, 0.6f),
                Ring(4.2f, 0.7f, CRY_L, 0f, 0.3f, false),
                Ring(2.8f, 0.6f, CRY_PINK, 0.1f, 0.3f, false),
                Shards(14, 4f, 8f, 0.26f, 0.46f, 0f, 0.6f),
                Sparks(12, 5f, 10f, 0.3f, 0.6f, CRY_L, CRY_PINK, FxShape.Sphere, 0.2f, 70f, 9f, 0.6f));

            Add("boss_break", 2, 0.6f,
                Flash(4.5f, 0.32f, 0x6EF0FF80, 0f, 0.7f),
                Flash(2.4f, 0.16f, 0xFFFFFFAA, 0f, 0.7f),
                Rays(4.8f, 0.45f, ICE, 0f, 0f, 0.7f),
                Rays(4.0f, 0.4f, WARM, 45f, 0.04f, 0.7f),
                Ring(5.2f, 0.9f, CRY_L, 0f, 0.3f, false),
                Ring(3.6f, 0.8f, CRY_PINK, 0.12f, 0.3f, false),
                Shards(30, 5f, 11f, 0.3f, 0.6f, 0f, 0.7f, 0.5f),
                Gems(12, 6f, 12f, 70f, GEM, CRY_L, 0.7f, 0.05f, 0.8f, 0.3f, 0.42f, new[] { 9 }),
                Coins(10, 6f, 11f, 70f, 0.7f, 0.08f, new[] { 9 }, 0.28f, 0.38f),
                GlintSub(),
                DustPuff(14, 0.8f, 0.8f, 1.2f, 2f, 4f, 0f, 0.8f, 0xCFC8E8FF, 0xB4ABD6FF),
                Sparks(26, 6f, 13f, 0.4f, 0.8f, CRY_L, WARM, FxShape.Sphere, 0.3f, 70f, 9f, 0.7f),
                Sparkle(28f, 1.0f, 1.4f, 0.7f, 0.25f, 0.5f, ICE, WARM));

            // ---------------------------------------------------------------- eventos continuos
            AddLoop("gold_rush", 1.2f, 2.0f,
                new FxEmitter
                {
                    Name = "motes", Tex = FxTex.SoftCircle, Additive = true, Rate = 24f, Ground = true, Y = 0.1f, Shape = FxShape.Cone, Radius = 0.55f,
                    Angle = 0f, SpeedMin = 0.8f, SpeedMax = 1.8f, LifeMin = 0.7f, LifeMax = 1.1f, SizeMin = 0.08f, SizeMax = 0.17f, SizeCurve = SHRINK_END,
                    ColA = GOLD, ColB = WARM, TintK = 0.3f, ColorCurve = FADE_IO
                },
                Sparkle(6f, 0f, 0.6f, 0.5f, 0.2f, 0.38f, WARM, GOLD_L, 0.3f),
                new FxEmitter
                {
                    Name = "coins", Tex = FxTex.Coin, Rate = 3f, Ground = true, Y = 0.3f, Shape = FxShape.Cone, Radius = 0.3f, Angle = 10f, SpeedMin = 1.2f,
                    SpeedMax = 2f, LifeMin = 0.8f, LifeMax = 1.0f, SizeMin = 0.12f, SizeMax = 0.18f, Gravity = -0.3f, Flip = 2f, ColorCurve = FADE_IO, Eco0 = true
                },
                new FxEmitter
                {
                    Name = "aura", Tex = FxTex.SoftCircle, Additive = true, Rate = 3f, Ground = true, Y = 0.1f, LifeMin = 1f, LifeMax = 1f, SizeMin = 1.8f,
                    SizeMax = 1.8f, ColA = 0xFFCC3330, ColB = 0xFFCC3330, TintK = 0.3f, ColorCurve = FADE_IO
                });

            AddLoop("frenzy_trail", 1.0f, 1.5f,
                new FxEmitter
                {
                    Name = "flames", Tex = FxTex.Dust, Rate = 34f, Shape = FxShape.Sphere, Radius = 0.15f, SpeedMin = 0.2f, SpeedMax = 0.6f, LifeMin = 0.4f,
                    LifeMax = 0.6f, SizeMin = 0.5f, SizeMax = 0.7f, SizeCurve = SHRINK, Gravity = -2f, Drag = 1.5f, RotMin = 0, RotMax = 360, SpinMin = -90, SpinMax = 90,
                    ColA = 0xFF9A3AFF, ColB = 0xFF4E3AFF, TintK = 0.3f, ColorCurve = FLAME
                },
                new FxEmitter
                {
                    Name = "streaks", Tex = FxTex.Spark, Additive = true, Render = FxRender.Stretch, Rate = 22f, Shape = FxShape.Cone, Radius = 0.2f, Angle = 12f,
                    SpeedMin = 1.5f, SpeedMax = 3f, LifeMin = 0.3f, LifeMax = 0.5f, SizeMin = 0.12f, SizeMax = 0.18f, SizeCurve = SHRINK_END, ColA = 0xFFD060FF,
                    ColB = 0xFF6A2AFF, TintK = 0.2f, ColorCurve = FADE_OUT
                },
                new FxEmitter
                {
                    Name = "heat", Tex = FxTex.SoftCircle, Additive = true, Rate = 6f, LifeMin = 0.5f, LifeMax = 0.5f, SizeMin = 1.2f, SizeMax = 1.2f,
                    ColA = 0xFF7A3A40, ColB = 0xFF7A3A40, TintK = 0.3f, ColorCurve = FADE_IO
                });

            Add("smoke", 0, 2.4f, Smoke(3, 0.7f, 1.1f, 0f, 0.9f, 1f, 0x908884FF), SmokeLate());
            // chorro de vapor de los canos del kit de cobre (RoomLife): bocanadas blancas chicas que suben y se abren
            Add("steam", 0, 2.4f,
                new FxEmitter
                {
                    Name = "steam", Tex = FxTex.Dust, Rate = 14f, Duration = 0.6f, Shape = FxShape.Sphere, Radius = 0.03f,
                    SpeedMin = 0.25f, SpeedMax = 0.5f, LifeMin = 0.7f, LifeMax = 1.0f, SizeMin = 0.12f, SizeMax = 0.2f, SizeCurve = GROW_BIG,
                    Gravity = -1.4f, Drag = 1.6f, RotMin = 0, RotMax = 360, SpinMin = -40, SpinMax = 40, ColA = 0xF4F2EEFF, ColB = 0xE6E8EAFF,
                    ColorCurve = new float[] { 0f, 1, 1, 1, 0, 0.12f, 1, 1, 1, 0.7f, 1f, 1, 1, 1, 0 }, TintK = 0.1f, Eco0 = true
                });

            AddLoop("lava_bubble", 1.2f, 2.4f,
                new FxEmitter
                {
                    Name = "bubble", Tex = FxTex.Lava, Rate = 4f, Ground = true, Y = 0.1f, Shape = FxShape.Disc, Radius = 0.35f, SpeedMin = 0f, SpeedMax = 0.05f,
                    LifeMin = 0.7f, LifeMax = 1.0f, SizeMin = 0.24f, SizeMax = 0.4f, SizeCurve = BUBBLE, Gravity = -0.8f, ColA = 0xFFFFFFFF, ColB = 0xFFE8C8FF,
                    TintK = 0.4f, ColorCurve = FADE_IO, SubDeath = new[] { 2, 3 }
                },
                new FxEmitter
                {
                    Name = "glow", Tex = FxTex.SoftCircle, Additive = true, Rate = 2.5f, Ground = true, Y = 0.15f, Shape = FxShape.Disc, Radius = 0.3f,
                    LifeMin = 1f, LifeMax = 1f, SizeMin = 1.0f, SizeMax = 1.0f, ColA = 0xFF6A2A4D, ColB = 0xFF6A2A4D, TintK = 0.4f, ColorCurve = FADE_IO
                },
                new FxEmitter
                {
                    Name = "pop", Tex = FxTex.Ring, Additive = true, Sub = true, Bursts = new float[] { 0f, 1f }, LifeMin = 0.22f, LifeMax = 0.22f, SizeMin = 0.75f,
                    SizeMax = 0.75f, SizeCurve = EXPAND, ColA = ORANGE, ColB = ORANGE, TintK = 0.3f, ColorCurve = FADE_SOON
                },
                new FxEmitter
                {
                    Name = "popsparks", Tex = FxTex.Spark, Additive = true, Render = FxRender.Stretch, Sub = true, Bursts = new float[] { 0f, 3f },
                    Shape = FxShape.Cone, Radius = 0.03f, Angle = 70f, SpeedMin = 1.5f, SpeedMax = 3f, LifeMin = 0.3f, LifeMax = 0.5f, SizeMin = 0.07f,
                    SizeMax = 0.1f, Gravity = 8f, ColA = 0xFFE27AFF, ColB = 0xFF8A2AFF, TintK = 0.2f, SizeCurve = SHRINK_END, ColorCurve = FADE_OUT
                });

            AddLoop("crystal_glow", 1.5f, 2.2f,
                new FxEmitter
                {
                    Name = "glow", Tex = FxTex.SoftCircle, Additive = true, Rate = 1f, Y = 0.3f, LifeMin = 1.8f, LifeMax = 1.8f, SizeMin = 1.8f, SizeMax = 1.8f,
                    SizeCurve = BREATH, ColA = 0x6EF0FFE6, ColB = 0x6EF0FFE6, TintK = 0.9f, ColorCurve = new float[] { 0f, 1, 1, 1, 0, 0.5f, 1, 1, 1, 0.8f, 1f, 1, 1, 1, 0 }
                },
                new FxEmitter
                {
                    Name = "twinkles", Tex = FxTex.Cross, Additive = true, Overlay = true, Rate = 2.2f, Y = 0.4f, Shape = FxShape.Sphere, Radius = 0.5f,
                    LifeMin = 0.5f, LifeMax = 0.8f, SizeMin = 0.16f, SizeMax = 0.3f, SizeCurve = GLINT, Gravity = -0.5f, ColA = ICE, ColB = ICE, RotMin = 0, RotMax = 45,
                    TintK = 0.7f, ColorCurve = FADE_OUT
                },
                new FxEmitter
                {
                    Name = "motes", Tex = FxTex.SoftCircle, Additive = true, Rate = 4f, Y = 0.3f, Shape = FxShape.Sphere, Radius = 0.6f, LifeMin = 1.2f, LifeMax = 2f,
                    SizeMin = 0.05f, SizeMax = 0.09f, Gravity = -0.3f, ColA = 0x6EF0FF99, ColB = 0x6EF0FF99, TintK = 0.9f, ColorCurve = FADE_IO, Eco0 = true
                });

            Add("unlock_burst", 2, 1.1f,
                Flash(2.2f, 0.22f, 0xFFD84A80, 0f, 0.5f),
                Rays(2.2f, 0.35f, WARM, 45f, 0f, 0.5f),
                Ring(2.4f, 0.6f, GOLD_L, 0f, 0.3f),
                Ring(1.4f, 0.5f, WARM, 0.1f, 0.3f),
                Stars(9, 3.5f, 6.5f, 0.3f, 0.55f, STAR_COLS, 0.5f, 0f, 3f, 2f),
                Paper(26, 5f, 9f, 55f, 0.16f, 0.24f, 1.3f, 2f, 0.5f),
                Sparks(16, 4f, 9f, 0.35f, 0.7f, GOLD, WARM, FxShape.Sphere, 0.1f, 70f, 8f, 0.5f));
        }

        // una segunda tanda de humo retrasada (humo continuo corto)
        static FxEmitter SmokeLate() { return Smoke(2, 0.6f, 1.0f, 0.12f, 0.9f, 1f, 0x777270FF); }
    }
}
