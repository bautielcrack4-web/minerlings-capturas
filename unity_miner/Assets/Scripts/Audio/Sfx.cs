using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mineros.Audio
{
    /// <summary>
    /// Audio del juego. Efectos: primero se buscan grabaciones reales en Resources/Audio (bancos "nombre_0..n", se elige
    /// una al azar sin repetir la anterior); si no hay, se sintetizan por codigo (SfxSynth). Ambientes en capas
    /// (SetLoop: mar, pajaros, viento, lluvia, grillos) con fundidos, musica grabada con cruce (PlayTrack) y "agachado"
    /// de musica y ambiente en los momentos grandes (Duck).
    /// CONTRATO (no cambiar firmas): Play, PlayMusic, SetMusic, SoundOn, MusicOn. Nombres desconocidos se ignoran.
    /// </summary>
    public static class Sfx
    {
        public static bool SoundOn = true;
        public static bool MusicOn = true;

        const int PoolSize = 24;
        const float MusicBaseVol = 0.2f;   // musica sintetizada (-14 dB como en Godot)
        const float TrackVol = 0.42f;      // musica grabada
        const float MinGap = 0.035f;       // anti-repeticion por nombre

        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> Unknown = new HashSet<string>();
        static readonly Dictionary<string, float> Last = new Dictionary<string, float>();
        static readonly Dictionary<string, int> LastVariant = new Dictionary<string, int>();
        static readonly AudioClip[] MusicCache = new AudioClip[4];
        static Dictionary<string, AudioClip[]> banks;

        /// <summary>Nombres del juego que suenan con un banco grabado de otro nombre.</summary>
        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "waves", "wave" }, { "goal", "jingle_win" }, { "milestone", "jingle_build" }, { "upgrade", "up" },
            { "event_start", "jingle_event" }, { "unlock", "gleam" }, { "rebirth", "fanfare" },
        };

        static SfxHost host;
        static AudioSource[] pool;
        static AudioSource music, music2;
        static int next;
        static int musicBiome = -1;   // bioma sonando o cargando
        static int musicToken;        // invalida fundidos y generaciones viejas
        static string track;          // musica grabada sonando

        sealed class LoopVoice { public AudioSource Src; public float Target, Base; }
        static readonly Dictionary<string, LoopVoice> loops = new Dictionary<string, LoopVoice>();
        static float duck = 1f, duckTarget = 1f, duckUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // con "Enter Play Mode" sin recarga de dominio los estaticos sobrevivirian a objetos destruidos
            Clips.Clear(); Unknown.Clear(); Last.Clear(); LastVariant.Clear(); loops.Clear();
            for (int i = 0; i < MusicCache.Length; i++) MusicCache[i] = null;
            host = null; pool = null; music = null; music2 = null; next = 0; musicBiome = -1; musicToken++;
            banks = null; track = null; duck = duckTarget = 1f;
        }

        static void Ensure()
        {
            if (host != null && pool != null && music != null) return;
            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            host = go.AddComponent<SfxHost>();
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                pool[i] = s;
            }
            music = NewMusic(go);
            music2 = NewMusic(go);
            next = 0;
        }

        static AudioSource NewMusic(GameObject go)
        {
            var m = go.AddComponent<AudioSource>();
            m.playOnAwake = false;
            m.spatialBlend = 0f;
            m.loop = true;
            m.volume = 0f;
            m.priority = 0;
            return m;
        }

        // ------------------------------------------------------------ bancos grabados
        static void LoadBanks()
        {
            if (banks != null) return;
            banks = new Dictionary<string, AudioClip[]>();
            var tmp = new Dictionary<string, List<AudioClip>>();
            foreach (var c in Resources.LoadAll<AudioClip>("Audio"))
            {
                if (c.name.StartsWith("music_") || c.name.StartsWith("amb_")) continue;
                string n = c.name;
                int us = n.LastIndexOf('_');
                if (us > 0 && us < n.Length - 1 && char.IsDigit(n[us + 1]) && IsNum(n, us + 1)) n = n.Substring(0, us);
                List<AudioClip> l;
                if (!tmp.TryGetValue(n, out l)) tmp[n] = l = new List<AudioClip>();
                l.Add(c);
            }
            foreach (var kv in tmp)
            {
                kv.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                banks[kv.Key] = kv.Value.ToArray();
            }
        }

        static bool IsNum(string s, int from)
        {
            for (int i = from; i < s.Length; i++) if (!char.IsDigit(s[i])) return false;
            return true;
        }

        /// <summary>Diagnostico: cantidad de bancos grabados y volumen actual de una capa de ambiente.</summary>
        public static int BankCount { get { LoadBanks(); return banks.Count; } }
        public static float LoopLevel(string name) { LoopVoice lv; return loops.TryGetValue(name, out lv) && lv.Src != null ? lv.Src.volume : 0f; }

        /// <summary>true si hay grabacion para ese nombre (o su alias).</summary>
        public static bool HasRecorded(string name)
        {
            LoadBanks();
            string a;
            return banks.ContainsKey(Alias.TryGetValue(name, out a) ? a : name);
        }

        static AudioClip GetClip(string name)
        {
            LoadBanks();
            string bank;
            if (!Alias.TryGetValue(name, out bank)) bank = name;
            AudioClip[] set;
            if (banks.TryGetValue(bank, out set) && set.Length > 0)
            {
                int prev;
                if (!LastVariant.TryGetValue(bank, out prev)) prev = -1;
                int i = set.Length == 1 ? 0 : Random.Range(0, set.Length);
                if (i == prev && set.Length > 1) i = (i + 1 + Random.Range(0, set.Length - 1)) % set.Length;
                LastVariant[bank] = i;
                return set[i];
            }
            AudioClip c;
            if (Clips.TryGetValue(name, out c) && c != null) return c;
            if (Unknown.Contains(name)) return null;
            c = SfxSynth.MakeSfx(name);
            if (c == null) { Unknown.Add(name); return null; }
            Clips[name] = c;
            return c;
        }

        public static void Play(string name, float volDb = 0f, float pitch = 1f) { PlayPan(name, volDb, pitch, 0f); }

        /// <summary>Como Play, con paneo estereo (-1 izquierda .. 1 derecha): olas y gaviotas suenan de su lado.</summary>
        public static void PlayPan(string name, float volDb, float pitch, float pan) { PlayCore(name, volDb, pitch, pan, false); }

        /// <summary>
        /// Afinado: sin variacion de tono ni de volumen (notas de una escala, que tienen que sonar justas) y sin el
        /// tiempo minimo entre repeticiones.
        /// </summary>
        public static void PlayExact(string name, float volDb, float pitch = 1f) { PlayCore(name, volDb, pitch, 0f, true); }

        /// <summary>Nota `step` de la escala de construccion (pentatonica: cualquier secuencia suena bien).</summary>
        public static void Note(int step, float volDb) { PlayExact("cx_n" + Mathf.Clamp(step, 0, 7), volDb); }

        static void PlayCore(string name, float volDb, float pitch, float pan, bool exact)
        {
            if (!SoundOn || string.IsNullOrEmpty(name)) return;
            AudioClip clip = GetClip(name);
            if (clip == null) return;
            Ensure();
            float now = Time.realtimeSinceStartup;
            float prev;
            if (!exact && Last.TryGetValue(name, out prev) && now - prev < MinGap) return;
            Last[name] = now;

            // preferir una voz libre; si no, rotar
            AudioSource src = null;
            for (int i = 0; i < PoolSize; i++)
            {
                var c = pool[(next + i) % PoolSize];
                if (!c.isPlaying) { src = c; next = (next + i + 1) % PoolSize; break; }
            }
            if (src == null) { src = pool[next]; next = (next + 1) % PoolSize; }
            src.clip = clip;
            // variacion de volumen (+-2 dB) y tono (+-5 %) para que nada suene repetido
            src.volume = Mathf.Clamp01(Mathf.Pow(10f, (volDb + (exact ? 0f : Random.Range(-1.5f, 1.5f))) / 20f));
            src.pitch = Mathf.Max(0.05f, pitch * (exact ? 1f : Random.Range(0.95f, 1.05f)));
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.Play();
        }

        /// <summary>Suena despues de `delay` segundos (capas de un mismo momento).</summary>
        public static void PlayLater(string name, float delay, float volDb = 0f, float pitch = 1f)
        {
            Ensure();
            host.StartCoroutine(Later(name, delay, volDb, pitch));
        }

        static IEnumerator Later(string name, float delay, float volDb, float pitch)
        {
            yield return new WaitForSecondsRealtime(delay);
            Play(name, volDb, pitch);
        }

        // ------------------------------------------------------------ ambientes en capas
        /// <summary>
        /// Pone una capa de ambiente (Resources/Audio/amb_*.ogg) al volumen v (0..1) con fundido de `fade` segundos.
        /// v = 0 la apaga al terminar el fundido. Se puede llamar cada cuadro: solo cambia el objetivo.
        /// </summary>
        public static void SetLoop(string name, float v, float fade = 1.5f)
        {
            Ensure();
            LoopVoice lv;
            if (!loops.TryGetValue(name, out lv))
            {
                if (v <= 0.001f) return;
                var clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip == null) return;
                var s = host.gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.loop = true; s.spatialBlend = 0f; s.clip = clip; s.volume = 0f; s.priority = 10;
                // cada capa arranca en un punto distinto para que no se note el principio del loop
                s.time = Random.value * clip.length * 0.9f;
                s.Play();
                lv = new LoopVoice { Src = s };
                loops[name] = lv;
            }
            lv.Target = Mathf.Clamp01(v);
            lv.Base = fade;
        }

        /// <summary>Baja la musica y los ambientes `db` decibeles durante `seconds` (momentos grandes).</summary>
        public static void Duck(float db, float seconds)
        {
            duckTarget = Mathf.Min(duckTarget, Mathf.Pow(10f, -Mathf.Abs(db) / 20f));
            duckUntil = Mathf.Max(duckUntil, Time.realtimeSinceStartup + seconds);
        }

        internal static void Tick(float dt)
        {
            if (Time.realtimeSinceStartup > duckUntil) duckTarget = 1f;
            duck = Mathf.MoveTowards(duck, duckTarget, dt * (duckTarget < duck ? 6f : 0.8f));
            List<string> dead = null;
            foreach (var kv in loops)
            {
                var lv = kv.Value;
                if (lv.Src == null) continue;
                float target = SoundOn ? lv.Target * Mathf.Lerp(1f, duck, 0.6f) : 0f;
                lv.Src.volume = Mathf.MoveTowards(lv.Src.volume, target, dt / Mathf.Max(0.05f, lv.Base));
                if (lv.Target <= 0f && lv.Src.volume <= 0.0005f) { (dead ?? (dead = new List<string>())).Add(kv.Key); }
            }
            if (dead != null)
                foreach (var k in dead) { Object.Destroy(loops[k].Src); loops.Remove(k); }
            if (track != null && music != null && MusicOn && !fading) music.volume = TrackVol * duck;
        }

        // ------------------------------------------------------------ musica
        /// <summary>Musica del bioma (0..3, se aplica modulo 4) con fundido corto desde el tema anterior.</summary>
        public static void PlayMusic(int biome)
        {
            Ensure();
            if (!MusicOn) { StopMusic(); return; }
            biome = ((biome % 4) + 4) % 4;
            if (biome == musicBiome && track == null) return; // ya suena o se esta generando
            musicBiome = biome;
            track = null;
            int token = ++musicToken;
            AudioClip c = MusicCache[biome];
            if (c != null) host.StartCoroutine(SwitchTo(c, token, MusicBaseVol, 0.5f));
            else host.StartCoroutine(SfxSynth.MakeMusic(biome, clip =>
            {
                MusicCache[biome] = clip;
                if (token == musicToken && host != null) host.StartCoroutine(SwitchTo(clip, token, MusicBaseVol, 0.5f));
            }));
        }

        /// <summary>Musica grabada (Resources/Audio/music_*.ogg) con cruce de `fade` segundos desde la anterior.</summary>
        public static void PlayTrack(string name, float fade = 2.5f)
        {
            Ensure();
            if (!MusicOn) { track = name; StopMusic(); track = name; return; }
            if (track == name && music.isPlaying) return;
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null) return;
            track = name;
            musicBiome = -1;
            int token = ++musicToken;
            host.StartCoroutine(Cross(clip, token, fade));
        }

        public static string Track { get { return track; } }

        static bool fading;

        static IEnumerator Cross(AudioClip clip, int token, float fade)
        {
            // la musica que sonaba pasa a la segunda voz y se apaga mientras la nueva sube
            var old = music; music = music2; music2 = old;
            music.Stop();
            music.clip = clip;
            music.loop = true;
            music.volume = 0f;
            music.Play();
            fading = true;
            float v0 = music2.volume;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                if (token != musicToken) { fading = false; yield break; }
                float k = t / fade;
                music.volume = TrackVol * duck * k;
                music2.volume = v0 * (1f - k);
                yield return null;
            }
            music2.Stop();
            music2.volume = 0f;
            music.volume = TrackVol * duck;
            fading = false;
        }

        public static void SetMusic(bool on, int biome)
        {
            MusicOn = on;
            if (!on) { if (music != null || host != null) { string t = track; StopMusic(); track = t; } return; }
            if (track != null) { string t = track; track = null; PlayTrack(t, 1f); }
            else PlayMusic(biome);
        }

        static void StopMusic()
        {
            musicToken++;
            musicBiome = -1;
            fading = false;
            if (music != null) { music.Stop(); music.volume = 0f; }
            if (music2 != null) { music2.Stop(); music2.volume = 0f; }
        }

        static IEnumerator SwitchTo(AudioClip clip, int token, float vol, float fade)
        {
            // fundido de salida del tema actual
            if (music.isPlaying)
            {
                float v0 = music.volume;
                for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
                {
                    if (token != musicToken) yield break;
                    music.volume = Mathf.Lerp(v0, 0f, t / 0.25f);
                    yield return null;
                }
            }
            if (token != musicToken) yield break;
            music.Stop();
            if (music2 != null) music2.Stop();
            music.clip = clip;
            music.loop = true;
            music.volume = 0f;
            music.Play();
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                if (token != musicToken) yield break;
                music.volume = Mathf.Lerp(0f, vol, t / fade);
                yield return null;
            }
            music.volume = vol;
        }
    }

    /// <summary>Anfitrion persistente de las corrutinas del audio (no usar directamente).</summary>
    internal sealed class SfxHost : MonoBehaviour
    {
        void Update() { Sfx.Tick(Time.unscaledDeltaTime); }
    }
}
