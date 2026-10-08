using System;
using System.Collections;
using UnityEngine;

namespace Mineros.Audio
{
    /// <summary>
    /// Sintesis de los efectos y la musica (port de miner_idle/scripts/sfx.gd). Todo mono a 22050 Hz.
    /// </summary>
    internal static class SfxSynth
    {
        public const int Rate = 22050;
        const float Tau = Mathf.PI * 2f;

        static readonly System.Random Rng = new System.Random(20240607);

        /// <summary>Ruido blanco en [-1, 1] (randf()*2-1 de Godot).</summary>
        static float N() { return (float)(Rng.NextDouble() * 2.0 - 1.0); }

        static float Exp(float x) { return Mathf.Exp(x); }
        static float Sin(float x) { return Mathf.Sin(x); }

        static float Fmod(float a, float b) { return a - b * Mathf.Floor(a / b); }

        /// <summary>Onda cuadrada de frecuencia f.</summary>
        static float Sq(float f, float t) { return Fmod(t * f, 1f) < 0.5f ? 1f : -1f; }

        /// <summary>Campana suave: fundamental + parcial inarmonico, con ataque corto. t0 = inicio de la nota.</summary>
        static float Bell(float t, float t0, float f, float amp, float decay)
        {
            float u = t - t0;
            if (u < 0f) return 0f;
            float a = Mathf.Min(1f, u * 400f) * Exp(-u * decay);
            return (Sin(Tau * f * u)
                    + 0.35f * Sin(Tau * f * 2.76f * u) * Exp(-u * decay * 0.6f)
                    + 0.15f * Sin(Tau * f * 5.4f * u) * Exp(-u * decay * 1.5f)) * a * amp;
        }

        /// <summary>Nota tipo cuerno/trompeta: armonicos con un leve vibrato.</summary>
        static float Horn(float t, float t0, float dur, float f, float amp)
        {
            float u = t - t0;
            if (u < 0f || u > dur) return 0f;
            float env = Mathf.Min(1f, u * 60f) * Mathf.Min(1f, (dur - u) * 25f);
            float ph = Tau * f * u + Sin(u * 38f) * 0.05f;
            return (Sin(ph) + 0.5f * Sin(2f * ph) + 0.28f * Sin(3f * ph) + 0.12f * Sin(4f * ph)) * env * amp * 0.55f;
        }

        /// <summary>Barrido lineal de frecuencia f0 -> f1 durante T segundos.</summary>
        static float Chirp(float t, float f0, float f1, float T)
        {
            float u = Mathf.Min(t, T);
            return Sin(Tau * (f0 * u + (f1 - f0) * u * u / (2f * T)));
        }

        static AudioClip Synth(string name, float dur, Func<float, float> fn)
        {
            int n = (int)(dur * Rate);
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float fade = Mathf.Min(1f, (float)(n - i) / 200f);
                buf[i] = Mathf.Clamp(fn(t) * fade, -1f, 1f);
            }
            return MakeClip(name, buf, false);
        }

        static AudioClip MakeClip(string name, float[] buf, bool stream)
        {
            var clip = AudioClip.Create(name, buf.Length, 1, Rate, stream);
            clip.SetData(buf, 0);
            return clip;
        }

        // ---------------------------------------------------------------- melodia propia de cada carta de efecto
        static readonly float[] Penta = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f };

        /// <summary>
        /// Jingle unico de la carta `id` (240 cartas = 240 melodias): 3 a 5 notas de una pentatonica (siempre suena bien),
        /// ritmo y timbre segun el efecto (campana, pluck, chip o cuerno) y un brillo final mas largo en las raras.
        /// </summary>
        static AudioClip CardJingle(string name, int id)
        {
            var r = new System.Random(id * 7919 + 13);
            int effect = id / 20, variant = id % 20;
            int rarity = variant >= 16 ? 3 : variant >= 12 ? 2 : variant >= 6 ? 1 : 0;
            int notes = 3 + Math.Min(2, rarity);
            float step = 0.075f + (effect % 3) * 0.02f;
            int timbre = effect % 4;
            var f = new float[notes]; var t0 = new float[notes];
            int idx = r.Next(0, 5);
            for (int i = 0; i < notes; i++)
            {
                idx = Mathf.Clamp(idx + r.Next(-2, 4), 0, Penta.Length - 1);
                if (i == notes - 1) idx = Mathf.Max(idx, 5);   // termina arriba: suena a premio
                f[i] = Penta[idx] * (effect >= 6 ? 0.5f : 1f);
                t0[i] = i * step * (r.NextDouble() < 0.25 ? 0.5f : 1f) + (i > 0 ? 0f : 0f);
            }
            float dur = t0[notes - 1] + 0.45f + rarity * 0.12f;
            return Synth(name, dur, t =>
            {
                float v = 0f;
                for (int i = 0; i < notes; i++)
                {
                    float u = t - t0[i];
                    if (u < 0f) continue;
                    bool last = i == notes - 1;
                    float dec = last ? 6f - rarity : 14f;
                    switch (timbre)
                    {
                        case 0: v += Bell(t, t0[i], f[i], 0.32f, dec); break;
                        case 1: v += Sin(Tau * f[i] * u) * Exp(-u * dec * 1.4f) * 0.4f * Mathf.Min(1f, u * 600f); break;   // pluck
                        case 2: v += Sq(f[i], u) * Exp(-u * dec * 1.2f) * 0.12f; break;                                        // chip
                        default: v += Horn(t, t0[i], last ? 0.35f : step * 0.9f, f[i] * 0.5f, 0.42f); break;
                    }
                }
                // destello final de las raras
                if (rarity >= 2) v += Sin(Tau * 2637f * t) * Exp(-Mathf.Abs(t - t0[notes - 1] - 0.05f) * 30f) * 0.08f;
                return v;
            });
        }

        static readonly float[] UpgradeNotes = { 523f, 659f, 784f, 1046f };
        static readonly float[] ClearNotes = { 523f, 659f, 784f, 1046f, 1046f, 1318f };
        static readonly float[] MilestoneNotes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        static readonly float[] ChestTw = { 2093f, 2637f, 3136f, 2349f, 3520f, 2794f, 3951f };
        static readonly float[] BossAmp = { 1f, 0.45f, 0.2f };
        static readonly float[] RebirthBells = { 1046.5f, 1318.5f, 1568f, 2093f, 2637f };

        /// <summary>Crea el efecto por nombre; devuelve null si el nombre no existe.</summary>
        public static AudioClip MakeSfx(string name)
        {
            if (name.StartsWith("cj_")) { int id; if (int.TryParse(name.Substring(3), out id)) return CardJingle(name, id); }
            switch (name)
            {
                case "pick":
                    return Synth(name, 0.09f, t => N() * Exp(-t * 60f) * 0.5f + Sin(Tau * 180f * t) * Exp(-t * 40f) * 0.6f);
                case "clink":
                    return Synth(name, 0.12f, t => Sin(Tau * 1650f * t) * Exp(-t * 35f) * 0.35f
                        + Sin(Tau * 2470f * t) * Exp(-t * 45f) * 0.25f + N() * Exp(-t * 120f) * 0.3f);
                case "break":
                    return Synth(name, 0.35f, t => N() * Exp(-t * 11f) * 0.6f
                        + Sin(Tau * (90f - t * 80f) * t) * Exp(-t * 9f) * 0.5f);
                case "coin":
                    return Synth(name, 0.16f, t => Sq(t < 0.05f ? 1320f : 1980f, t) * Exp(-t * 18f) * 0.18f);
                case "ui":
                    return Synth(name, 0.06f, t => Sin(Tau * (700f + t * 3000f) * t) * Exp(-t * 50f) * 0.4f);
                case "upgrade":
                    return Synth(name, 0.22f, t => Sq(UpgradeNotes[Mathf.Min((int)(t / 0.055f), 3)], t)
                        * Exp(-Fmod(t, 0.055f) * 20f) * 0.16f);
                case "clear":
                    return Synth(name, 0.9f, t => Sq(ClearNotes[Mathf.Min((int)(t / 0.13f), 5)], t)
                        * Exp(-Fmod(t, 0.13f) * 6f) * 0.17f);
                case "gacha":
                    return Synth(name, 0.5f, t => Sin(Tau * (400f + t * 1600f) * t) * 0.25f * Exp(-t * 3f));
                case "crit":
                    return Synth(name, 0.25f, t => Sin(Tau * (220f - t * 300f) * t) * Exp(-t * 14f) * 0.7f
                        + Sin(Tau * 2900f * t) * Exp(-t * 30f) * 0.2f);
                case "tick":
                    return Synth(name, 0.05f, t => Sin(Tau * 1800f * t) * Exp(-t * 80f) * 0.5f);
                case "go":
                    return Synth(name, 0.4f, t => Sq(t < 0.12f ? 880f : 1320f, t) * Exp(-Fmod(t, 0.2f) * 8f) * 0.2f);
                case "combo":
                    return Synth(name, 0.12f, t => Sin(Tau * (900f + t * 4000f) * t) * Exp(-t * 20f) * 0.3f);
                case "milestone":
                    // hito de nivel: fanfarria corta ascendente y brillante
                    return Synth(name, 0.95f, t =>
                    {
                        float v = 0f;
                        for (int i = 0; i < 4; i++) v += Horn(t, i * 0.085f, 0.16f, MilestoneNotes[i], 0.5f);
                        v += Horn(t, 0.34f, 0.5f, 1046.5f, 0.45f) + Horn(t, 0.34f, 0.5f, 1318.5f, 0.3f) + Horn(t, 0.34f, 0.5f, 1568f, 0.25f);
                        v += Bell(t, 0.34f, 2093f, 0.12f, 5f);
                        return v * 0.42f;
                    });
                case "unlock":
                    // desbloqueo: campanita magica de 3 notas
                    return Synth(name, 0.8f, t =>
                        (Bell(t, 0f, 1318.5f, 0.3f, 7f) + Bell(t, 0.1f, 1568f, 0.3f, 7f) + Bell(t, 0.2f, 2093f, 0.34f, 5.5f)) * 0.8f);
                case "goal":
                    // meta cumplida: ding de recompensa
                    return Synth(name, 0.6f, t => (Bell(t, 0f, 987.8f, 0.3f, 9f) + Bell(t, 0.09f, 1318.5f, 0.38f, 6.5f)) * 0.85f);
                case "event_start":
                    // aviso de evento: arpegio de cuerno, alegre
                    return Synth(name, 0.85f, t =>
                    {
                        float v = Horn(t, 0f, 0.14f, 523.25f, 0.5f) + Horn(t, 0.12f, 0.14f, 659.25f, 0.5f) + Horn(t, 0.24f, 0.14f, 783.99f, 0.5f);
                        v += Horn(t, 0.36f, 0.42f, 1046.5f, 0.55f) + Horn(t, 0.36f, 0.42f, 783.99f, 0.3f);
                        return v * 0.75f;
                    });
                case "meteor":
                    // meteoro: silbido que cae + impacto grave
                    return Synth(name, 1.15f, t =>
                    {
                        float v = 0f;
                        if (t < 0.58f)
                        {
                            float k = t / 0.58f;
                            v += Chirp(t, 2400f, 520f, 0.58f) * (0.1f + 0.22f * k * k);
                            v += N() * 0.05f * k;
                        }
                        else
                        {
                            float u = t - 0.58f;
                            v += Sin(Tau * (120f - Mathf.Min(u, 0.4f) * 160f) * u) * Exp(-u * 9f) * 0.7f;
                            v += N() * Exp(-u * 16f) * 0.5f;
                        }
                        return v * 0.65f;
                    });
                case "chest":
                    // cofre: crujido de tapa, apertura y tintineo de gemas
                    return Synth(name, 1f, t =>
                    {
                        float v = 0f;
                        if (t < 0.22f)
                        {
                            v += Sin(Tau * (260f + 380f * t + Sin(t * 70f) * 20f) * t) * 0.18f * Mathf.Min(1f, t * 30f) * Mathf.Min(1f, (0.22f - t) * 40f);
                            v += N() * 0.05f * Mathf.Min(1f, (0.22f - t) * 40f);
                        }
                        else
                        {
                            float u = t - 0.22f;
                            v += Sin(Tau * 170f * u) * Exp(-u * 22f) * 0.5f;
                        }
                        for (int i = 0; i < 7; i++) v += Bell(t, 0.24f + i * 0.065f, ChestTw[i], 0.2f, 12f);
                        return v * 0.8f;
                    });
                case "gem":
                    // gema: tintin cristalino corto
                    return Synth(name, 0.22f, t => (Bell(t, 0f, 2637f, 0.3f, 20f) + Bell(t, 0.04f, 3520f, 0.22f, 24f)) * 0.75f);
                case "boss_phase":
                    // fase del jefe: golpe grave con eco
                    return Synth(name, 0.95f, t =>
                    {
                        float v = 0f;
                        for (int i = 0; i < 3; i++)
                        {
                            float u = t - i * 0.2f;
                            if (u >= 0f)
                                v += (Sin(Tau * (95f - Mathf.Min(u, 0.3f) * 120f) * u) * Exp(-u * 7f) * 0.8f + N() * Exp(-u * 30f) * 0.35f) * BossAmp[i];
                        }
                        return v * 0.68f;
                    });
                case "rebirth":
                    // renacer (no existe en Godot): barrido ascendente brillante, campanas en cascada y acorde de cuernos
                    return Synth(name, 1.6f, t =>
                    {
                        float v = 0f;
                        if (t < 0.7f)
                        {
                            float k = t / 0.7f;
                            v += Chirp(t, 180f, 1100f, 0.7f) * 0.2f * k * k * Mathf.Min(1f, (0.7f - t) * 20f);
                            v += N() * 0.03f * k;
                        }
                        for (int i = 0; i < RebirthBells.Length; i++) v += Bell(t, 0.55f + i * 0.07f, RebirthBells[i], 0.26f, 4.5f);
                        v += Horn(t, 0.7f, 0.7f, 523.25f, 0.4f) + Horn(t, 0.7f, 0.7f, 659.25f, 0.3f) + Horn(t, 0.7f, 0.7f, 783.99f, 0.3f);
                        return v * 0.6f;
                    });
                case "pop":
                    // veta que nace: burbuja que sube
                    return Synth(name, 0.14f, t => Sin(Tau * (260f + t * 2600f) * t) * Exp(-t * 26f) * 0.45f);
                case "horn":
                    // bocina de barco: dos notas graves con batido
                    return Synth(name, 1.5f, t =>
                    {
                        float env = Mathf.Min(1f, t * 8f) * Mathf.Min(1f, (1.5f - t) * 3f) * (t < 0.62f || t > 0.75f ? 1f : 0.15f);
                        float f = t < 0.7f ? 146.8f : 110f;
                        return (Sin(Tau * f * t) + 0.6f * Sin(Tau * f * 2.003f * t) + 0.35f * Sin(Tau * f * 3f * t) + 0.2f * Sq(f, t)) * env * 0.32f;
                    });
                case "bell":
                    // campana de veta gigante: tres golpes grandes
                    return Synth(name, 1.8f, t => (Bell(t, 0f, 784f, 0.4f, 2.5f) + Bell(t, 0.35f, 784f, 0.35f, 2.5f) + Bell(t, 0.7f, 1046.5f, 0.45f, 2f)) * 0.7f);
                case "build":
                    // martillazos de obra
                    return Synth(name, 0.5f, t =>
                    {
                        float v = 0f;
                        for (int i = 0; i < 3; i++)
                        {
                            float u = t - i * 0.15f;
                            if (u >= 0f) v += (Sin(Tau * 330f * u) * Exp(-u * 30f) * 0.5f + N() * Exp(-u * 70f) * 0.4f);
                        }
                        return v * 0.7f;
                    });
                case "gull":
                    // gaviota: chirrido que baja, dos veces
                    return Synth(name, 0.7f, t =>
                    {
                        float u = t < 0.3f ? t : t - 0.33f;
                        if (u < 0f || u > 0.28f) return 0f;
                        float env = Mathf.Sin(u / 0.28f * Mathf.PI);
                        return Sin(Tau * (1700f - u * 2400f) * u + Sin(u * 160f) * 0.6f) * env * 0.16f;
                    });
                case "waves":
                {
                    // olas: ruido filtrado que crece y baja (5 s, se repite de fondo)
                    float lp = 0f;
                    return Synth(name, 5f, t =>
                    {
                        lp += (N() - lp) * 0.06f;
                        float swell = 0.35f + 0.65f * Mathf.Pow(Mathf.Sin(t / 5f * Mathf.PI), 2f);
                        return lp * swell * 0.9f;
                    });
                }
                case "steam_hiss":
                {
                    // siseo de vapor: ruido con paso alto, ataque rapido y cola de 0.6 s (kit de cobre)
                    float lp = 0f;
                    return Synth(name, 0.65f, t =>
                    {
                        float n0 = N();
                        lp += (n0 - lp) * 0.35f;
                        float env = Mathf.Min(1f, t * 40f) * Mathf.Exp(-t * 3.2f);
                        return (n0 - lp) * env * 0.35f;
                    });
                }
                case "gauge_tick":
                    // tic metalico de la aguja del manometro
                    return Synth(name, 0.05f, t => (Sin(Tau * 3200f * t) * 0.4f + N() * 0.3f) * Exp(-t * 140f) * 0.5f);
                case "splash":
                    return Synth(name, 0.4f, t => N() * Exp(-t * 9f) * 0.3f * Mathf.Min(1f, t * 60f) + Sin(Tau * (500f - t * 600f) * t) * Exp(-t * 18f) * 0.2f);
                // --- PLAN_HABITACIONES §6 (sintetizados: sin licencias de terceros) ---
                case "wood_place":
                {
                    // tabla que se apoya: golpe sordo de madera (cuerpo grave + resonancia hueca + chasquido)
                    float lp = 0f;
                    return Synth(name, 0.22f, t =>
                    {
                        lp += (N() - lp) * 0.25f;
                        return (Sin(Tau * (140f - t * 120f) * t) * Exp(-t * 26f) * 0.75f
                              + Sin(Tau * 420f * t) * Exp(-t * 45f) * 0.22f
                              + lp * Exp(-t * 70f) * 0.5f);
                    });
                }
                case "scaffold_hammer":
                    // martillazo en el andamio: clavo (metal agudo) sobre madera
                    return Synth(name, 0.18f, t => Sin(Tau * 2650f * t) * Exp(-t * 55f) * 0.25f + Sin(Tau * 3900f * t) * Exp(-t * 70f) * 0.12f
                        + Sin(Tau * (180f - t * 200f) * t) * Exp(-t * 30f) * 0.5f + N() * Exp(-t * 160f) * 0.35f);
                case "metal_clang":
                    return Synth(name, 0.6f, t => (Sin(Tau * 520f * t) + 0.6f * Sin(Tau * 1371f * t) + 0.35f * Sin(Tau * 2410f * t)) * Exp(-t * 7f) * 0.22f
                        + N() * Exp(-t * 90f) * 0.3f);
                case "crystal_chime":
                    return Synth(name, 1.1f, t => Bell(t, 0f, 1568f, 0.28f, 5f) + Bell(t, 0.07f, 2093f, 0.2f, 6f) + Bell(t, 0.15f, 2637f, 0.14f, 7f));
                case "diamond_ting":
                    return Synth(name, 0.5f, t => Bell(t, 0f, 3136f, 0.22f, 11f) + Sin(Tau * 6272f * t) * Exp(-t * 30f) * 0.06f);
                case "coins_clink":
                    return Synth(name, 0.45f, t =>
                    {
                        float a = 0f;
                        for (int k = 0; k < 5; k++) { float t0 = k * 0.07f + (k % 2) * 0.012f; float u = t - t0; if (u > 0f) a += (Sin(Tau * (1700f + k * 230f) * u) * 0.5f + Sin(Tau * (2600f + k * 310f) * u) * 0.3f) * Exp(-u * 40f); }
                        return a * 0.3f;
                    });
                case "fanfare_short":
                    return Synth(name, 1.1f, t => Horn(t, 0f, 0.16f, 523.25f, 0.5f) + Horn(t, 0.16f, 0.16f, 659.25f, 0.5f)
                        + Horn(t, 0.32f, 0.7f, 783.99f, 0.55f) + Horn(t, 0.32f, 0.7f, 1046.5f, 0.3f));
            }
            return null;
        }

        // ------------------------------------------------------------------ musica
        static readonly float[] Roots = { 261.63f, 220f, 196f, 174.61f };
        static readonly float[] Bpms = { 104f, 96f, 84f, 112f };
        static readonly int[][] Scales =
        {
            new[] { 0, 2, 4, 7, 9 }, new[] { 0, 3, 5, 7, 10 }, new[] { 0, 2, 3, 7, 9 }, new[] { 0, 1, 5, 7, 8 },
        };
        static readonly int[][] Prog =
        {
            new[] { 0, 5, 3, 4 }, new[] { 0, 3, 5, 4 }, new[] { 0, 5, 0, 3 }, new[] { 0, 1, 0, 5 },
        };
        static readonly int[] ArpPat = { 0, 2, 4, 2, 1, 3, 4, 2 };

        /// <summary>
        /// Bucle de 8 compases (bajo triangular, arpegio pluck pentatonico y colchon). Se genera repartido entre frames:
        /// ceder cada pocos pasos evita trabar el arranque. Al terminar entrega el clip.
        /// </summary>
        public static IEnumerator MakeMusic(int biome, Action<AudioClip> done)
        {
            float root = Roots[biome];
            float beat = 60f / Bpms[biome];
            float step = beat / 2f;
            const int nSteps = 64;
            int total = (int)(step * nSteps * Rate);
            var buf = new float[total];
            var rng = new System.Random(1234 + biome);
            int[] sc = Scales[biome];
            for (int st = 0; st < nSteps; st++)
            {
                int bar = st / 8;
                int chord = Prog[biome][bar % 4];
                int t0 = (int)(st * step * Rate);
                if (st % 2 == 0)
                {
                    float f = root / 2f * Mathf.Pow(2f, (sc[chord % sc.Length] + (chord >= sc.Length ? 12 : 0)) / 12f);
                    Note(buf, t0, beat * 0.9f, f, 0.22f, 0);
                }
                if (rng.NextDouble() < 0.82)
                {
                    int deg = (chord + ArpPat[st % 8]) % sc.Length;
                    float oct = rng.NextDouble() < 0.8 ? 1f : 2f;
                    float f = root * oct * Mathf.Pow(2f, sc[deg] / 12f);
                    Note(buf, t0, step * 1.6f, f, 0.11f, 1);
                }
                if (st % 8 == 0)
                {
                    float f = root * Mathf.Pow(2f, sc[chord % sc.Length] / 12f);
                    Note(buf, t0, step * 8f, f, 0.05f, 2);
                    Note(buf, t0, step * 8f, f * 1.5f, 0.035f, 2);
                }
                if (st % 6 == 5) yield return null;
            }
            for (int i = 0; i < total; i++) buf[i] = Mathf.Clamp(buf[i], -1f, 1f);
            var clip = AudioClip.Create("music_" + biome, total, 1, Rate, false);
            clip.SetData(buf, 0);
            done(clip);
        }

        // kind: 0 triangular (bajo), 1 pluck, 2 pad
        static void Note(float[] buf, int start, float dur, float f, float amp, int kind)
        {
            int n = (int)(dur * Rate);
            int total = buf.Length;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float v = 0f;
                if (kind == 0)
                {
                    float ph = Fmod(t * f, 1f);
                    v = (4f * Mathf.Abs(ph - 0.5f) - 1f) * Mathf.Min(1f, t * 80f) * Exp(-t * 2.5f);
                }
                else if (kind == 1)
                {
                    v = (Sin(Tau * f * t) + 0.35f * Sin(Tau * f * 2f * t)) * Exp(-t * 9f) * Mathf.Min(1f, t * 300f);
                }
                else
                {
                    float env = Mathf.Min(1f, t * 3f) * Mathf.Min(1f, (dur - t) * 3f);
                    v = (Sin(Tau * f * t) + Sin(Tau * f * 1.004f * t)) * 0.5f * env;
                }
                buf[(start + i) % total] += v * amp;
            }
        }
    }
}
