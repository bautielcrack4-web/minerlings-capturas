using System.Collections.Generic;
using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;
using Juice = Mineros.Fx.Juice;

namespace Mineros.IslandView
{
    /// <summary>
    /// Detalles del mundo (docs/DETALLES.md): luces de los edificios que se prenden de a una al anochecer (14), el
    /// Ayuntamiento que sube y todos festejan con doble pulso (11), el minero que contesta al tocarlo (13), el cartel
    /// de obra que destella en el ultimo 10 % (10) y el vocabulario de vibraciones (19).
    /// </summary>
    public sealed partial class IslandGame
    {
        // ------------------------------------------------------------ vibracion con vocabulario (19)
        /// <summary>Doble pulso suave (12 + 12 ms) para las cosas grandes.</summary>
        public void DoublePulse()
        {
            Juice.Vibrate(12);
            Mineros.UI.Tw.After(this, "vib2", 0.14f, () => Juice.Vibrate(12));
        }

        // ------------------------------------------------------------ luces de noche (14)
        readonly Dictionary<int, MeshRenderer> doorLights = new Dictionary<int, MeshRenderer>();

        void UpdateNightLights()
        {
            float night = Isl.Night;
            foreach (var v in plots)
            {
                var p = v.P;
                bool has = p.Building >= 0 && p.Level >= 1 && v.Root.gameObject.activeSelf && p.Building != (int)BKind.Dock;
                MeshRenderer r;
                doorLights.TryGetValue(p.Id, out r);
                if (!has) { if (r != null) r.gameObject.SetActive(false); continue; }
                // cada edificio se prende un poco despues que el anterior (de a una, como un pueblo de verdad)
                float delay = (p.Id * 0.137f) % 0.35f;
                float k = Mathf.Clamp01((night - 0.25f - delay) / 0.2f);
                if (k <= 0.001f) { if (r != null && r.gameObject.activeSelf) r.gameObject.SetActive(false); continue; }
                if (r == null)
                {
                    r = IslandFxKit.Halo(v.Root, "LuzPuerta", 2.6f);
                    Vector3 f = -Cam.transform.forward; f.y = 0f; f.Normalize();
                    r.transform.localPosition = f * (Island.Defs[p.Building].Radius + 0.15f) + Vector3.up * 0.04f;
                    r.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    doorLights[p.Id] = r;
                }
                if (!r.gameObject.activeSelf)
                {
                    r.gameObject.SetActive(true);
                    if (OnScreen(r.transform.position) && Random.value < 0.3f) Sfx.Play("tick", -26f, 1.6f);   // el "clic" de la luz
                }
                float flicker = 1f + Mathf.Sin(Time.time * 9f + p.Id) * 0.04f;
                IslandFxKit.Tint(r, new Color(1f, 0.72f, 0.35f, 0.55f * k * flicker));
            }
        }

        // ------------------------------------------------------------ Ayuntamiento nuevo: todos festejan (11)
        void TownHallCheer()
        {
            foreach (var mv in miners.Values) mv.Celebrate = 1f;
            foreach (var b in builders) b.Celebrate = 1f;
            DoublePulse();
            Sfx.PlayLater("cheer", 0.3f, -6f);
        }

        // ------------------------------------------------------------ el minero contesta (13)
        static readonly string[] HappyLines = { "¡A picar!", "¡Qué lindo día!", "¡Vamos que se puede!", "¡Esta isla es mía!", "¡Una más!" };

        public void MinerSays(Miner m)
        {
            MinerView mv;
            if (!miners.TryGetValue(m.Id, out mv)) return;
            string line;
            if (m.Energy < 25f) line = Loc.T("Tengo hambre…");
            else if (m.Clean < 30f) line = Loc.T("Necesito una ducha…");
            else if (m.Fresh > 0f) line = Loc.T("¡Me siento fresco!");
            else if (IsNight && m.State == MState.Resting) line = Loc.T("Zzz…");
            else if (m.CarryKind >= 0) line = Loc.T("¡Llevo ") + Island.Ores[m.CarryKind].Name.ToLower() + "!";
            else line = HappyLines[(m.Id + (int)Time.time) % HappyLines.Length];
            mv.Celebrate = 0.6f;
            Ui.Popup(mv.Model.transform.position + Vector3.up * MH(2.5f), line, Color.white, 26);
        }

        bool IsNight { get { return Isl.Night > 0.5f; } }

        // ------------------------------------------------------------ obra casi lista (10)
        readonly HashSet<int> almostDone = new HashSet<int>();

        void UpdateAlmostDone()
        {
            foreach (var v in plots)
            {
                var p = v.P;
                if (p.Work <= 0 || p.WorkTotal <= 0) { almostDone.Remove(p.Id); continue; }
                bool last = p.Work / p.WorkTotal < 0.1 && p.WorkTotal > 8;
                if (!last || almostDone.Contains(p.Id)) continue;
                almostDone.Add(p.Id);
                Vector3 at = v.Root.position + Vector3.up * 1.2f;
                if (OnScreen(at)) { FxApi.Play("sparkle", at, new Color(1f, 0.95f, 0.6f), 1f); Sfx.Play("tick", -12f, 1.4f); }
            }
        }

        // ------------------------------------------------------------ rendimiento sin perder calidad
        bool optimizeDirty = true;
        float optimizeT;

        /// <summary>
        /// Instancias en todos los materiales (las copias de una misma malla salen en una sola llamada) y sin sombra
        /// proyectada en lo diminuto (flores, brotes, piedritas, banderines): no se nota y ahorra un dibujo por objeto.
        /// </summary>
        void OptimizeRenderers()
        {
            string mode = Application.isBatchMode ? System.Environment.GetEnvironmentVariable("PERF_MODE") : null;
            if (mode == "0") return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                string n = r.name;
                // instancias solo donde se repite la misma malla (sombras de contacto, faroles); el resto lo agrupa
                // mejor el batching dinamico de Unity (mallas chicas distintas con el mismo material)
                bool repeated = n == "SombraContacto" || n == "Poste" || n == "Vidrio" || n == "LuzPuerta" || mode == "2";
                foreach (var m in r.sharedMaterials) if (m != null && m.enableInstancing != repeated) m.enableInstancing = repeated;
                if (n.StartsWith("flower") || n.StartsWith("pebble") || n.StartsWith("grass") || n == "Brote" || n == "Nivel" || n == "Vidrio" || (n.StartsWith("Sombra") && n != "SombraNube"))   // SombraNube SOLO proyecta sombra: si se apaga eso se ve como nube sobre la isla
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public void MarkOptimize() { optimizeDirty = true; }

        // ------------------------------------------------------------ tienda y anuncios
        void InitMonetization()
        {
            Mineros.Monetization.Store.Grant = id => { string got = Isl.Grant(id); Save(); return got; };
            Mineros.Monetization.Store.Restored = id => { Isl.Restore(id); Save(); };
            Mineros.Monetization.Store.SubscriptionLapsed = id => { if (Isl.Capataz) { Isl.Capataz = false; Isl.Owned.Remove(id); Save(); } };
            Mineros.Monetization.Store.Init();
            // reseña: cuenta dias de juego y se pide solo tras un festejo (misiones del dia, barco cumplido)
            Mineros.Monetization.Reviews.CountDay();
            Isl.AllMissionsDone += Mineros.Monetization.Reviews.HappyMoment;
            Isl.ShipLeft += (s, ok) => { if (ok) Mineros.Monetization.Reviews.HappyMoment(); };
            // los anuncios (y el formulario de privacidad) recien despues de la primera partida: al principio nada molesta
            if (Isl.TutDone) { StartAdsLater(); IslandNotify.AskOnce(); }
            else Isl.TutAdvanced += step => { if (step == Island.TutStep.Done) { StartAdsLater(); IslandNotify.AskOnce(); } };
        }

        void StartAdsLater()
        {
            Mineros.UI.Tw.After(this, "anuncios", 3f, () =>
            {
#if UNITY_IOS && !UNITY_EDITOR
                // iOS: el permiso de seguimiento (ATT) se pide despues de jugar un rato, nunca al abrir
                if (Unity.Advertisement.IosSupport.ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == Unity.Advertisement.IosSupport.ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
                    Unity.Advertisement.IosSupport.ATTrackingStatusBinding.RequestAuthorizationTracking();
#endif
                Mineros.Monetization.Ads.Init();
            });
        }

        void UpdateDetails()
        {
            optimizeT -= Time.unscaledDeltaTime;
            if (optimizeDirty && optimizeT <= 0f) { optimizeDirty = false; optimizeT = 1f; OptimizeRenderers(); }
            UpdateNightLights();
            UpdateAlmostDone();
        }
    }
}
