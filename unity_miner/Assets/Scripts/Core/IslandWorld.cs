using System;

namespace Mineros.Core
{
    public enum Weather { Clear = 0, Rain = 1, Storm = 2, Meteors = 3 }

    /// <summary>
    /// Mundo vivo (biblia 3.7): ciclo de dia y noche de 10 minutos, cristales nocturnos que solo aparecen de noche y hay
    /// que tocar, y clima (lluvia, tormenta con rayos que convierten vetas en cristal, lluvia de meteoritos y arcoiris
    /// al terminar la lluvia, que da +50 % de ganancia un rato).
    /// </summary>
    public sealed partial class Island
    {
        public const float DayLength = 600f;
        /// <summary>Reloj del dia en segundos (0 = amanecer temprano).</summary>
        public float DayClock = DayLength * 0.06f;
        public Weather Sky = Weather.Clear;
        public float WeatherLeft, RainbowT;
        float weatherT = 200f, strikeT = 6f, meteorT, crystalT = 8f;
        int meteorsLeft;

        public event Action<Weather> WeatherChanged;
        public event Action<Ore> Lightning;       // el rayo cayo sobre esta veta (ahora es cristal)
        public event Action RainbowStarted;
        public event Action<Ore> OreFaded;        // cristal nocturno que se apago al amanecer
        public event Action Morning;              // amanece: los mineros salen a estirarse

        /// <summary>0..1 a lo largo del dia.</summary>
        public float DayPhase { get { return DayClock / DayLength; } }

        /// <summary>
        /// 0 = pleno dia, 1 = plena noche. Dia hasta 0.62, atardecer 0.62-0.72, noche 0.72-0.92, amanecer 0.92-1.
        /// </summary>
        public float Night
        {
            get
            {
                float p = DayPhase;
                if (p < 0.62f) return 0f;
                if (p < 0.72f) return Smooth((p - 0.62f) / 0.1f);
                if (p < 0.92f) return 1f;
                return 1f - Smooth((p - 0.92f) / 0.08f);
            }
        }

        /// <summary>0..1: cuanto de atardecer/amanecer (luz naranja).</summary>
        public float Dusk
        {
            get
            {
                float p = DayPhase;
                float a = 1f - Math.Abs(p - 0.665f) / 0.07f;
                float b = 1f - Math.Abs(p - 0.95f) / 0.05f;
                return Clamp01(Math.Max(a, b));
            }
        }

        static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }

        public bool IsNight { get { return Night > 0.5f; } }

        void TickWorld(float dt)
        {
            float before = DayPhase;
            DayClock += dt;
            if (DayClock >= DayLength) DayClock -= DayLength;
            float after = DayPhase;
            // amanecer: los cristales que nadie toco se apagan y los mineros salen a estirarse
            if (before < 0.93f && after >= 0.93f)
            {
                foreach (var o in OreList)
                    if (o.NightCrystal && !o.Dead) { o.Dead = true; OreFaded?.Invoke(o); }
                Morning?.Invoke();
            }
            TickCrystals(dt);
            TickWeather(dt);
            if (RainbowT > 0f) RainbowT = Math.Max(0f, RainbowT - dt);
        }

        // ------------------------------------------------------------ cristales nocturnos
        public const int MaxCrystals = 3;

        int CrystalCount()
        {
            int n = 0;
            foreach (var o in OreList) if (o.NightCrystal && !o.Dead) n++;
            return n;
        }

        void TickCrystals(float dt)
        {
            if (DayPhase < 0.72f || DayPhase >= 0.92f || TotalEarned < 60) return;   // solo en plena noche
            crystalT -= dt;
            if (crystalT > 0f) return;
            crystalT = 14f + (float)rng.NextDouble() * 10f;
            if (CrystalCount() >= MaxCrystals + ObservatoryCrystals()) return;
            SpawnCrystal();
        }

        public Ore SpawnCrystal()
        {
            return SpawnOre(false, 4, o => { o.NightCrystal = true; o.MaxHp = o.Hp = 3; });
        }

        /// <summary>Premio de un cristal nocturno: una gema y unos segundos de ingreso.</summary>
        double CrystalPrize(Ore o)
        {
            Gems += 1;
            AddStat("crystals", 1);
            return CoinPrize(20);
        }

        // ------------------------------------------------------------ clima
        void TickWeather(float dt)
        {
            if (Sky != Weather.Clear)
            {
                WeatherLeft -= dt;
                if (Sky == Weather.Storm)
                {
                    strikeT -= dt;
                    if (strikeT <= 0f) { strikeT = 7f + (float)rng.NextDouble() * 7f; Strike(); }
                }
                if (Sky == Weather.Meteors && meteorsLeft > 0)
                {
                    meteorT -= dt;
                    if (meteorT <= 0f)
                    {
                        meteorT = 0.9f + (float)rng.NextDouble() * 1.1f;
                        meteorsLeft--;
                        SpawnOre(false, Math.Min(3, Math.Max(1, BestKind())), m => m.Sky = true);
                        if (meteorsLeft <= 0) WeatherLeft = Math.Min(WeatherLeft, 3f);
                    }
                }
                if (WeatherLeft <= 0f) EndWeather();
                return;
            }
            if (TotalEarned < 150) return;
            if (nextSky < 0)
            {
                // el proximo clima se decide de antemano (asi el Observatorio puede avisar los meteoritos)
                double r = rng.NextDouble();
                double meteor = FirstMod(ModKind.Observatory) != null ? 0.80 : 0.86;   // con Observatorio caen mas seguido
                nextSky = r < 0.45 ? (int)Weather.Clear : r < 0.72 ? (int)Weather.Rain : r < meteor ? (int)Weather.Storm : (int)Weather.Meteors;
                meteorWarned = false;
            }
            weatherT -= dt;
            if (!meteorWarned && nextSky == (int)Weather.Meteors && weatherT <= 60f && FirstMod(ModKind.Observatory) != null)
            {
                meteorWarned = true;
                MeteorSoon?.Invoke();
            }
            if (weatherT > 0f) return;
            weatherT = 170f + (float)rng.NextDouble() * 120f;
            var w = (Weather)nextSky;
            nextSky = -1;
            if (w != Weather.Clear) StartWeather(w);
        }

        int nextSky = -1;
        bool meteorWarned;
        /// <summary>El Observatorio vio venir una lluvia de meteoritos (1 minuto antes).</summary>
        public event Action MeteorSoon;

        /// <summary>Arranca un clima (tambien para pruebas y capturas).</summary>
        public void StartWeather(Weather w)
        {
            Sky = w;
            WeatherLeft = w == Weather.Rain ? 75f : w == Weather.Storm ? 55f : 14f;
            strikeT = 4f;
            if (w == Weather.Meteors) { meteorsLeft = 5 + rng.Next(4); meteorT = 1.5f; }
            AddStat("weather", 1);
            WeatherChanged?.Invoke(w);
        }

        void EndWeather()
        {
            var was = Sky;
            Sky = Weather.Clear;
            WeatherLeft = 0f;
            WeatherChanged?.Invoke(Weather.Clear);
            if (was == Weather.Rain || was == Weather.Storm)
            {
                RainbowT = 120f;
                RainbowStarted?.Invoke();
            }
        }

        /// <summary>Rayo: cae sobre una veta comun y la convierte en cristal (vale mucho mas).</summary>
        public Ore Strike()
        {
            Ore best = null;
            int seen = 0;
            foreach (var o in OreList)
            {
                if (o.Dead || o.Giant || o.NightCrystal || o.Kind >= 4 || o.Age < 1f) continue;
                seen++;
                if (rng.Next(seen) == 0) best = o;   // al azar entre las validas
            }
            if (best == null) return null;
            best.Kind = 4;
            best.MaxHp = best.Hp = Ores[4].Hp;
            AddStat("lightning", 1);
            Lightning?.Invoke(best);
            return best;
        }

        /// <summary>Multiplicador del arcoiris (+50 % mientras dura).</summary>
        public double RainbowMult() { return RainbowT > 0f ? 1.5 : 1.0; }
    }
}
