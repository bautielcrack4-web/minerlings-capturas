using System.Collections.Generic;
using UnityEngine;

namespace Mineros.Fx
{
    /// <summary>
    /// Instancia de un efecto: un GameObject raiz con un ParticleSystem por emisor (hijos). Se arma UNA vez por kind
    /// (FxBuilder) y se reutiliza desde el pool: Play/Attach nunca instancian ni destruyen, y no generan basura por frame.
    /// </summary>
    internal sealed class FxInstance
    {
        public FxEffect Spec;
        public GameObject Root;
        public Transform RootTr;
        public ParticleSystem[] Ps;
        public ParticleSystemRenderer[] Rend;
        public bool[] Playing;              // emisores lanzados en este uso (los saltados por Eco no)
        public ParticleSystem.MinMaxGradient[] PaletteGrad;
        public bool InUse, Attached, Fading;
        public float StartTime;
        public FxAttachment Handle;
    }

    /// <summary>Pegamento de Fx.Attach: sigue al transform y, al destruirse, apaga el efecto suavemente.</summary>
    [AddComponentMenu("")]
    internal sealed class FxAttachment : MonoBehaviour
    {
        internal FxInstance Inst;

        void LateUpdate()
        {
            if (Inst != null && Inst.RootTr != null) Inst.RootTr.position = transform.position;
        }

        void OnDisable()
        {
            if (Inst != null && !FxRuntime.Quitting) FxRuntime.SetEmitting(Inst, false);
        }

        void OnEnable()
        {
            if (Inst != null && Inst.Attached && !Inst.Fading && !FxRuntime.Quitting) FxRuntime.SetEmitting(Inst, true);
        }

        void OnDestroy()
        {
            if (Inst != null && !FxRuntime.Quitting) FxRuntime.Detach(Inst);
            Inst = null;
        }
    }

    /// <summary>Pool, Play/Attach y recoleccion de efectos terminados.</summary>
    internal static class FxRuntime
    {
        internal static bool Quitting;
        internal static float GroundY;

        static readonly Dictionary<string, List<FxInstance>> pools = new Dictionary<string, List<FxInstance>>();
        static readonly List<FxInstance> active = new List<FxInstance>(32);
        static Transform poolRoot;
        static Transform groundPlane;
        static float bigUntil = -10f, lastBigAt = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pools.Clear();
            active.Clear();
            poolRoot = null;
            groundPlane = null;
            Quitting = false;
            bigUntil = lastBigAt = -10f;
            GroundY = 0f;
        }

        internal static Transform PoolRoot
        {
            get
            {
                if (poolRoot == null)
                {
                    var go = new GameObject("~FxPool");
                    go.hideFlags = HideFlags.HideInHierarchy;
                    Object.DontDestroyOnLoad(go);
                    poolRoot = go.transform;
                    var g = new GameObject("ground");
                    g.transform.SetParent(poolRoot, false);
                    groundPlane = g.transform;       // plano de colision (normal = arriba) en y = GroundY
                    // el host del runner (reaper, camara, tiempo) se crea junto con el pool
                    FxHost.Ensure();
                }
                return poolRoot;
            }
        }

        internal static Transform GroundPlane
        {
            get
            {
                var _ = PoolRoot;
                groundPlane.position = new Vector3(0f, GroundY, 0f);
                return groundPlane;
            }
        }

        // ------------------------------------------------------------------ API
        internal static void Play(string kind, Vector3 pos, Color tint, float scale)
        {
            if (!Application.isPlaying) return;
            var spec = FxLib.Get(kind);
            if (spec == null) return;
            float inten;
            if (!Focus(spec, out inten)) return;
            var inst = Acquire(spec);
            if (inst == null) return;
            Launch(inst, pos, tint, scale, inten, false);
        }

        internal static GameObject Attach(string kind, Transform target, Color tint, float scale)
        {
            var handle = new GameObject(kind);
            if (target != null) handle.transform.SetParent(target, false);
            if (!Application.isPlaying) return handle;
            var spec = FxLib.Get(kind);
            if (spec == null) return handle;
            var inst = Acquire(spec);
            if (inst == null) return handle;
            var att = handle.AddComponent<FxAttachment>();
            att.Inst = inst;
            inst.Handle = att;
            Launch(inst, handle.transform.position, tint, scale, 1f, true);
            return handle;
        }

        internal static void Prewarm(string kind, int count)
        {
            if (!Application.isPlaying) return;
            var spec = FxLib.Get(kind);
            if (spec == null) return;
            List<FxInstance> list = PoolOf(spec);
            while (list.Count < count && list.Count < spec.MaxInstances) list.Add(FxBuilder.Build(spec, PoolRoot));
        }

        // ------------------------------------------------------------------ un foco grande por vez
        /// <summary>
        /// Regla "un foco grande por vez": durante ~0.6 s tras un efecto grande (prio 2) los menores se atenuan o se
        /// descartan, y un segundo efecto grande pegado al primero sale mas tenue. Devuelve false si se descarta.
        /// </summary>
        static bool Focus(FxEffect spec, out float inten)
        {
            inten = 1f;
            float now = Time.unscaledTime;
            if (spec.Priority >= 2)
            {
                if (now - lastBigAt < 1.2f) inten = 0.6f;
                lastBigAt = now;
                bigUntil = now + 0.6f;
                // los menores lanzados en este mismo instante se apagan
                for (int i = 0; i < active.Count; i++)
                {
                    var a = active[i];
                    if (a.Spec.Priority == 0 && !a.Attached && now - a.StartTime < 0.12f) ReleaseNow(a);
                }
                for (int i = active.Count - 1; i >= 0; i--) if (!active[i].InUse) active.RemoveAt(i);
            }
            else if (now < bigUntil)
            {
                if (spec.Priority == 0)
                {
                    if (Random.value < 0.5f) return false;
                    inten = 0.35f;
                }
                else inten = 0.65f;
            }
            return true;
        }

        // ------------------------------------------------------------------ pool
        static List<FxInstance> PoolOf(FxEffect spec)
        {
            List<FxInstance> list;
            if (!pools.TryGetValue(spec.Kind, out list))
            {
                list = new List<FxInstance>(spec.MaxInstances);
                pools[spec.Kind] = list;
            }
            return list;
        }

        static FxInstance Acquire(FxEffect spec)
        {
            List<FxInstance> list = PoolOf(spec);
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].InUse && list[i].Root != null) return list[i];
            }
            if (list.Count < spec.MaxInstances)
            {
                var inst = FxBuilder.Build(spec, PoolRoot);
                list.Add(inst);
                return inst;
            }
            // pool lleno: se recicla el mas viejo que no este pegado a un objeto
            FxInstance oldest = null;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c.Attached || c.Root == null) continue;
                if (oldest == null || c.StartTime < oldest.StartTime) oldest = c;
            }
            if (oldest != null) ReleaseNow(oldest);
            return oldest;
        }

        static void Launch(FxInstance inst, Vector3 pos, Color tint, float scale, float inten, bool attached)
        {
            inst.InUse = true;
            inst.Attached = attached;
            inst.Fading = false;
            inst.StartTime = Time.unscaledTime;
            inst.RootTr.position = pos;
            inst.Root.SetActive(true);
            FxBuilder.Apply(inst, tint, Mathf.Max(0.05f, scale), inten, attached, pos.y);
            if (!active.Contains(inst)) active.Add(inst);
        }

        internal static void SetEmitting(FxInstance inst, bool on)
        {
            if (inst.Root == null) return;
            for (int i = 0; i < inst.Ps.Length; i++)
            {
                if (inst.Spec.Emitters[i].Sub || !inst.Playing[i]) continue;
                if (on) { if (!inst.Ps[i].isPlaying) inst.Ps[i].Play(false); }
                else inst.Ps[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>El objeto al que estaba pegado se destruyo: deja de emitir y se libera cuando mueren las particulas.</summary>
        internal static void Detach(FxInstance inst)
        {
            if (inst.Root == null) return;
            inst.Handle = null;
            inst.Attached = false;
            inst.Fading = true;
            SetEmitting(inst, false);
        }

        static void ReleaseNow(FxInstance inst)
        {
            if (inst.Root == null) return;
            if (inst.Handle != null) { inst.Handle.Inst = null; inst.Handle = null; }
            for (int i = 0; i < inst.Ps.Length; i++)
            {
                inst.Ps[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            inst.Root.SetActive(false);
            inst.InUse = false;
            inst.Attached = false;
            inst.Fading = false;
        }

        /// <summary>Lo llama el host cada frame: libera los efectos de una sola vez cuyas particulas ya murieron.</summary>
        internal static void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var inst = active[i];
                if (!inst.InUse || inst.Root == null)
                {
                    active.RemoveAt(i);
                    continue;
                }
                if (inst.Attached) continue;
                if (now - inst.StartTime < 0.05f) continue;
                bool alive = false;
                for (int k = 0; k < inst.Ps.Length; k++)
                {
                    if (inst.Ps[k].IsAlive(false)) { alive = true; break; }
                }
                if (!alive)
                {
                    ReleaseNow(inst);
                    active.RemoveAt(i);
                }
            }
        }
    }
}
