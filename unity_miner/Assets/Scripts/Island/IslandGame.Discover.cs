using Mineros.Audio;
using Mineros.Core;
using UnityEngine;
using FxApi = Mineros.Fx.Fx;

namespace Mineros.IslandView
{
    /// <summary>
    /// Expandir con promesa (auditoria final, "¿que habra detras?"): mientras queda tierra por abrir, en la niebla de la
    /// proxima ampliacion brilla algo cada tanto; al expandir, unos segundos despues aparece lo que escondia (un
    /// Yacimiento legendario con vetas ricas alrededor, Island.Discovered).
    /// </summary>
    public sealed partial class IslandGame
    {
        float teaseT = 2f;
        /// <summary>Capturas: fuerza el fin de semana dorado.</summary>
        public static bool DebugWeekend;

        void InitDiscover()
        {
            Isl.Discovered += o =>
            {
                Ui.Toast(Loc.T("¡La tierra nueva escondía un Yacimiento legendario!"), new Color(0.6f, 0.92f, 1f), null, true);
                Sfx.PlayLater("crystal_chime", 0.2f, -5f);
            };
        }

        void UpdateTease(float dt)
        {
            Isl.DecorDeal = (Today * 3 + 1) % Island.DecorDefs.Length;   // adorno del dia (-40 %), rota con el calendario
            var dow = System.DateTime.Now.DayOfWeek;
            Isl.Weekend = DebugWeekend || dow == System.DayOfWeek.Saturday || dow == System.DayOfWeek.Sunday;   // fin de semana dorado
            if (Isl.Expand >= Island.Radii.Length - 1 || !Isl.TutDone) return;
            teaseT -= dt;
            if (teaseT > 0f) return;
            teaseT = Isl.ExpandAllowed() ? 2.2f + Random.value * 1.5f : 5f + Random.value * 3f;   // con la ampliacion a mano, mas seguido
            // un punto en el anillo que todavia esta en la niebla, del lado que mira la camara
            float r0 = Isl.Radius + 0.8f, r1 = Island.Radii[Isl.Expand + 1] - 0.8f;
            float a = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(r0, Mathf.Max(r0, r1));
            Vector3 p = new Vector3(Mathf.Cos(a) * d, 0.5f, Mathf.Sin(a) * d);
            Vector3 vp = Cam.WorldToViewportPoint(p);
            if (vp.x < 0.05f || vp.x > 0.95f || vp.y < 0.1f || vp.y > 0.9f) { teaseT = 0.3f; return; }
            FxApi.Play("glint", p, new Color(0.6f, 0.92f, 1f), 1.6f);
        }
    }
}
