using System.Collections.Generic;
using UnityEngine;

namespace Mineros.Fx
{
    /// <summary>
    /// Motor de "game feel": sacudida con ruido Perlin, micro-pausa, camara lenta, golpes de escala con resorte,
    /// destellos, zoom y vibracion. Lo mueve el host (FxHost) con tiempo SIN escala, asi sigue funcionando con
    /// Time.timeScale = 0 (micro-pausa) o reducido (camara lenta).
    /// </summary>
    internal static class JuiceCore
    {
        // ---------------------------------------------------------------- resorte amortiguado (forma cerrada)
        // D(t): 1 en t=0, rebasa al otro lado y se asienta en ~dur. Sin integracion: estable con cualquier dt.
        static float Spring(float t, float dur)
        {
            if (t < 0f || t >= dur) return 0f;
            float u = t / dur;
            float d = Mathf.Exp(-3.9f * u) * (Mathf.Cos(9.49f * u) + 0.411f * Mathf.Sin(9.49f * u));
            float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.85f) / 0.15f));   // cierra suave el ultimo 15%
            return d * w;
        }

        // ---------------------------------------------------------------- sacudida
        struct ShakeSlot { public float Amp, Dur, T0, SeedX, SeedY; public bool On; }
        static readonly ShakeSlot[] shakes = new ShakeSlot[8];
        internal static float MaxShake = 0.6f;
        internal static Vector2 ShakeXY;                // desplazamiento actual en el plano de la camara
        const float ShakeFreq = 26f;

        /// <summary>
        /// Multiplicador global de la sacudida de camara. 0 = sin temblores (decision de diseño: mareaban; el impacto se
        /// lee en la roca: rebote, destello, particulas y numero). Subirlo solo para pruebas.
        /// </summary>
        internal static float ShakeScale = 0f;

        internal static void Shake(float amp, float dur)
        {
            amp *= ShakeScale;
            if (!(amp > 0f) || !(dur > 0f)) return;
            FxHost.Ensure();
            float now = Time.unscaledTime;
            int slot = -1;
            float weakest = float.MaxValue;
            for (int i = 0; i < shakes.Length; i++)
            {
                if (!shakes[i].On) { slot = i; break; }
                float k = Mathf.Clamp01(1f - (now - shakes[i].T0) / shakes[i].Dur);
                float left = shakes[i].Amp * k * k;
                if (left < weakest) { weakest = left; slot = i; }
            }
            shakes[slot] = new ShakeSlot { Amp = amp, Dur = dur, T0 = now, SeedX = Random.value * 100f, SeedY = Random.value * 100f + 50f, On = true };
        }

        static void TickShake(float now)
        {
            float x = 0f, y = 0f;
            for (int i = 0; i < shakes.Length; i++)
            {
                if (!shakes[i].On) continue;
                float el = now - shakes[i].T0;
                float k = 1f - el / shakes[i].Dur;
                if (k <= 0f) { shakes[i].On = false; continue; }
                float a = shakes[i].Amp * k * k;
                float tt = el * ShakeFreq;
                float nx = Mathf.Clamp((Mathf.PerlinNoise(shakes[i].SeedX, tt) * 2f - 1f) * 1.6f, -1f, 1f);
                float ny = Mathf.Clamp((Mathf.PerlinNoise(shakes[i].SeedY, tt) * 2f - 1f) * 1.6f, -1f, 1f);
                x += nx * a;
                y += ny * a;
            }
            float mag = Mathf.Sqrt(x * x + y * y);
            if (mag > 1e-5f)
            {
                // tope suave: varias sacudidas juntas se acumulan pero nunca pasan de MaxShake
                float soft = MaxShake * (float)System.Math.Tanh(mag / MaxShake);
                float f = soft / mag;
                x *= f;
                y *= f;
            }
            ShakeXY = new Vector2(x, y);
        }

        // ---------------------------------------------------------------- zoom
        struct ZoomSlot { public float Amount, Dur, T0; public bool On; }
        static readonly ZoomSlot[] zooms = new ZoomSlot[4];
        internal static float ZoomFactor = 1f;

        internal static void ZoomPunch(float amount, float dur)
        {
            if (Mathf.Approximately(amount, 0f) || !(dur > 0f)) return;
            FxHost.Ensure();
            float now = Time.unscaledTime;
            int slot = 0;
            float oldest = float.MaxValue;
            for (int i = 0; i < zooms.Length; i++)
            {
                if (!zooms[i].On) { slot = i; oldest = -1f; break; }
                if (zooms[i].T0 < oldest) { oldest = zooms[i].T0; slot = i; }
            }
            zooms[slot] = new ZoomSlot { Amount = Mathf.Clamp(amount, -0.4f, 0.4f), Dur = dur, T0 = now, On = true };
        }

        static void TickZoom(float now)
        {
            float s = 0f;
            for (int i = 0; i < zooms.Length; i++)
            {
                if (!zooms[i].On) continue;
                float el = now - zooms[i].T0;
                if (el >= zooms[i].Dur) { zooms[i].On = false; continue; }
                s += zooms[i].Amount * Spring(el, zooms[i].Dur);
            }
            ZoomFactor = Mathf.Clamp(1f - s, 0.6f, 1.4f);   // amount > 0 acerca (tamano ortografico menor) y vuelve con rebote
        }

        // ---------------------------------------------------------------- tiempo: micro-pausa y camara lenta
        static float hitEnd = -1f;
        static float slowEnd = -1f, slowScale = 1f;
        const float SlowReturn = 0.3f;
        static bool owned;
        static float baseScale = 1f, lastSet = 1f;

        internal static void HitStop(float seconds)
        {
            if (!(seconds > 0f)) return;
            if (!owned && Time.timeScale < 0.001f) return;     // el juego esta en pausa: no tocar
            FxHost.Ensure();
            hitEnd = Mathf.Max(hitEnd, Time.unscaledTime + Mathf.Min(seconds, 0.4f));
        }

        internal static void SlowMo(float scale, float seconds)
        {
            if (!(seconds > 0f)) return;
            if (!owned && Time.timeScale < 0.001f) return;
            FxHost.Ensure();
            float now = Time.unscaledTime;
            scale = Mathf.Clamp(scale, 0.05f, 1f);
            slowScale = (now < slowEnd + SlowReturn) ? Mathf.Min(CurrentSlow(now), scale) : scale;
            slowEnd = Mathf.Max(slowEnd, now + Mathf.Min(seconds, 5f));
        }

        static float CurrentSlow(float now)
        {
            if (now < slowEnd) return slowScale;
            if (now < slowEnd + SlowReturn)
            {
                float u = (now - slowEnd) / SlowReturn;
                return Mathf.Lerp(slowScale, 1f, u * u * (3f - 2f * u));
            }
            return 1f;
        }

        static void TickTime(float now)
        {
            bool hit = now < hitEnd;
            float slow = CurrentSlow(now);
            bool active = hit || slow < 0.9999f;
            if (active)
            {
                if (!owned)
                {
                    owned = true;
                    baseScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
                }
                else if (Mathf.Abs(Time.timeScale - lastSet) > 0.001f)
                {
                    baseScale = Time.timeScale;            // alguien mas cambio el tiempo: se respeta
                }
                float want = hit ? 0f : baseScale * slow;
                Time.timeScale = want;
                lastSet = want;
            }
            else if (owned)
            {
                RestoreTime();
            }
        }

        /// <summary>Devuelve Time.timeScale a su valor original (nunca queda trabado).</summary>
        internal static void RestoreTime()
        {
            if (!owned) return;
            owned = false;
            hitEnd = -1f;
            slowEnd = -1f;
            Time.timeScale = baseScale;
            lastSet = baseScale;
        }

        // ---------------------------------------------------------------- camara
        static Camera appliedCam;
        static Vector3 appliedPos, appliedOff;
        static float appliedSize, baseSize, appliedFov, baseFov;
        static bool appliedZoomOrtho, appliedZoom;

        internal static void CameraRevert()
        {
            var cam = appliedCam;
            if (cam == null) { appliedCam = null; appliedZoom = false; return; }
            var tr = cam.transform;
            tr.position = tr.position - appliedOff;      // si el mundo la movio en el medio, igual se resta lo aplicado
            if (appliedZoom)
            {
                if (appliedZoomOrtho)
                {
                    if (Mathf.Abs(cam.orthographicSize - appliedSize) < 1e-5f) cam.orthographicSize = baseSize;
                }
                else if (Mathf.Abs(cam.fieldOfView - appliedFov) < 1e-4f) cam.fieldOfView = baseFov;
            }
            appliedCam = null;
            appliedZoom = false;
            appliedOff = Vector3.zero;
        }

        internal static void CameraApply()
        {
            if (!Juice.ApplyToCamera) return;
            var cam = Juice.Cam;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var tr = cam.transform;
            Vector3 off = tr.right * ShakeXY.x + tr.up * ShakeXY.y;
            bool zoom = Mathf.Abs(ZoomFactor - 1f) > 1e-4f;
            if (off.sqrMagnitude < 1e-12f && !zoom) return;
            appliedCam = cam;
            appliedOff = off;
            tr.position = tr.position + off;
            appliedPos = tr.position;
            appliedZoom = zoom;
            if (zoom)
            {
                appliedZoomOrtho = cam.orthographic;
                if (cam.orthographic)
                {
                    baseSize = cam.orthographicSize;
                    cam.orthographicSize = baseSize * ZoomFactor;
                    appliedSize = cam.orthographicSize;
                }
                else
                {
                    baseFov = cam.fieldOfView;
                    cam.fieldOfView = baseFov * ZoomFactor;
                    appliedFov = cam.fieldOfView;
                }
            }
        }

        // ---------------------------------------------------------------- punch (aplastar y estirar)
        sealed class PunchState
        {
            public Transform T;
            public Vector3 Base, Last;
            public float[] Amp = new float[4], Dur = new float[4], T0 = new float[4];
            public bool[] On = new bool[4];
        }
        static readonly Dictionary<Transform, PunchState> punches = new Dictionary<Transform, PunchState>();
        static readonly List<PunchState> punchList = new List<PunchState>(16);
        static readonly Stack<PunchState> punchPool = new Stack<PunchState>();

        internal static void Punch(Transform t, float amount, float dur)
        {
            if (t == null || !(dur > 0f) || Mathf.Approximately(amount, 0f)) return;
            FxHost.Ensure();
            float now = Time.unscaledTime;
            PunchState st;
            if (!punches.TryGetValue(t, out st))
            {
                st = punchPool.Count > 0 ? punchPool.Pop() : new PunchState();
                st.T = t;
                st.Base = t.localScale;      // la escala original: aqui se vuelve SIEMPRE, sin importar cuantos golpes seguidos
                st.Last = st.Base;
                for (int i = 0; i < 4; i++) st.On[i] = false;
                punches[t] = st;
                punchList.Add(st);
            }
            int slot = 0;
            float oldest = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (!st.On[i]) { slot = i; break; }
                if (st.T0[i] < oldest) { oldest = st.T0[i]; slot = i; }
            }
            st.Amp[slot] = Mathf.Clamp(amount, -0.6f, 0.6f);
            st.Dur[slot] = dur;
            st.T0[slot] = now;
            st.On[slot] = true;
        }

        static void TickPunch(float now)
        {
            for (int i = punchList.Count - 1; i >= 0; i--)
            {
                var st = punchList[i];
                if (st.T == null)                // el objeto se destruyo
                {
                    FinishPunch(i, false);
                    continue;
                }
                // si otro sistema cambio la escala mientras tanto, esa pasa a ser la base
                if ((st.T.localScale - st.Last).sqrMagnitude > 1e-8f) st.Base = st.T.localScale;
                float s = 0f;
                bool any = false;
                for (int k = 0; k < 4; k++)
                {
                    if (!st.On[k]) continue;
                    float el = now - st.T0[k];
                    if (el >= st.Dur[k]) { st.On[k] = false; continue; }
                    any = true;
                    s += -st.Amp[k] * Spring(el, st.Dur[k]);
                }
                if (!any)
                {
                    st.T.localScale = st.Base;   // vuelve exactamente a la escala original
                    FinishPunch(i, true);
                    continue;
                }
                s = Mathf.Clamp(s, -0.65f, 0.65f);
                float fy = 1f + s, fxz = 1f - 0.5f * s;
                var b = st.Base;
                var sc = new Vector3(b.x * fxz, b.y * fy, b.z * fxz);
                st.T.localScale = sc;
                st.Last = sc;
            }
        }

        static void FinishPunch(int listIndex, bool recycle)
        {
            var st = punchList[listIndex];
            int last = punchList.Count - 1;
            punchList[listIndex] = punchList[last];
            punchList.RemoveAt(last);
            if (st.T != null) punches.Remove(st.T);
            else
            {
                // transform destruido: limpiar la entrada huerfana
                Transform dead = null;
                foreach (var kv in punches) if (kv.Value == st) { dead = kv.Key; break; }
                if (!ReferenceEquals(dead, null)) punches.Remove(dead);
            }
            st.T = null;
            punchPool.Push(st);
        }

        // ---------------------------------------------------------------- flash
        sealed class FlashState
        {
            public GameObject Go;
            public float T0, Dur;
            public readonly List<Renderer> R = new List<Renderer>(8);
            public readonly List<Color> Orig = new List<Color>(8);
            public readonly List<bool> Emis = new List<bool>(8);
            public readonly List<bool> HadBlock = new List<bool>(8);
        }
        static readonly Dictionary<int, FlashState> flashes = new Dictionary<int, FlashState>();
        static readonly List<FlashState> flashList = new List<FlashState>(16);
        static readonly Stack<FlashState> flashPool = new Stack<FlashState>();
        static readonly List<Renderer> tmpRenderers = new List<Renderer>(16);
        static MaterialPropertyBlock mpb;
        static int idEmission, idColor;

        static void InitIds()
        {
            if (mpb != null) return;
            mpb = new MaterialPropertyBlock();
            idEmission = Shader.PropertyToID("_EmissionColor");
            idColor = Shader.PropertyToID("_Color");
        }

        internal static void Flash(GameObject go, float dur)
        {
            if (go == null || !(dur > 0f)) return;
            InitIds();
            FxHost.Ensure();
            float now = Time.unscaledTime;
            int id = go.GetInstanceID();
            FlashState st;
            if (flashes.TryGetValue(id, out st))
            {
                st.T0 = now;                 // reinicia el destello sin perder los colores originales
                st.Dur = dur;
                return;
            }
            st = flashPool.Count > 0 ? flashPool.Pop() : new FlashState();
            st.Go = go;
            st.T0 = now;
            st.Dur = dur;
            st.R.Clear(); st.Orig.Clear(); st.Emis.Clear(); st.HadBlock.Clear();
            go.GetComponentsInChildren(false, tmpRenderers);
            for (int i = 0; i < tmpRenderers.Count; i++)
            {
                var r = tmpRenderers[i];
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                var m = r.sharedMaterial;
                if (m == null) continue;
                bool emis = m.HasProperty(idEmission);
                if (!emis && !m.HasProperty(idColor)) continue;
                int pid = emis ? idEmission : idColor;
                bool had = r.HasPropertyBlock();
                Color orig;
                if (had)
                {
                    r.GetPropertyBlock(mpb);
                    orig = mpb.HasProperty(pid) ? mpb.GetColor(pid) : m.GetColor(pid);
                }
                else orig = m.GetColor(pid);
                st.R.Add(r); st.Orig.Add(orig); st.Emis.Add(emis); st.HadBlock.Add(had);
            }
            tmpRenderers.Clear();
            if (st.R.Count == 0) { st.Go = null; flashPool.Push(st); return; }
            flashes[id] = st;
            flashList.Add(st);
        }

        static void TickFlash(float now)
        {
            for (int i = flashList.Count - 1; i >= 0; i--)
            {
                var st = flashList[i];
                float el = now - st.T0;
                bool done = el >= st.Dur || st.Go == null;
                // tope 0.65 como el Godot original (capa blanca al 65%): la roca no pierde su forma ni sombreado
                float k = done ? 0f : 0.65f * Mathf.Pow(1f - el / st.Dur, 1.6f);
                for (int j = 0; j < st.R.Count; j++)
                {
                    var r = st.R[j];
                    if (r == null) continue;
                    int pid = st.Emis[j] ? idEmission : idColor;
                    if (done && !st.HadBlock[j]) { r.SetPropertyBlock(null); continue; }
                    r.GetPropertyBlock(mpb);
                    Color o = st.Orig[j];
                    Color target = st.Emis[j] ? new Color(1.25f, 1.25f, 1.15f, 1f) : Color.white;
                    mpb.SetColor(pid, done ? o : Color.Lerp(o, target, k));
                    r.SetPropertyBlock(mpb);
                }
                if (done)
                {
                    if (st.Go != null) flashes.Remove(st.Go.GetInstanceID());
                    else
                    {
                        int dead = 0; bool found = false;
                        foreach (var kv in flashes) if (kv.Value == st) { dead = kv.Key; found = true; break; }
                        if (found) flashes.Remove(dead);
                    }
                    int last = flashList.Count - 1;
                    flashList[i] = flashList[last];
                    flashList.RemoveAt(last);
                    st.Go = null;
                    flashPool.Push(st);
                }
            }
        }

        // ---------------------------------------------------------------- vibracion

        internal static void Vibrate(int ms)
        {
            // 0.9.4: todas las vibraciones pasan por Haptics (vibracion fina segun el tipo, no por milisegundos)
            Haptics.FromMs(ms);
        }



        // ---------------------------------------------------------------- reloj del host
        internal static void Update()
        {
            float now = Time.unscaledTime;
            TickShake(now);
            TickZoom(now);
            TickTime(now);
        }

        internal static void LateTick()
        {
            float now = Time.unscaledTime;
            TickPunch(now);
            TickFlash(now);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < shakes.Length; i++) shakes[i].On = false;
            for (int i = 0; i < zooms.Length; i++) zooms[i].On = false;
            ShakeXY = Vector2.zero;
            ZoomFactor = 1f;
            hitEnd = slowEnd = -1f;
            slowScale = 1f;
            owned = false;
            baseScale = lastSet = 1f;
            appliedCam = null;
            appliedZoom = false;
            punches.Clear();
            punchList.Clear();
            flashes.Clear();
            flashList.Clear();
            mpb = null;
        }
    }
}
