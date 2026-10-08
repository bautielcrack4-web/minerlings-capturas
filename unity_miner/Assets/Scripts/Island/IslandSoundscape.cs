using Mineros.Audio;
using UnityEngine;

namespace Mineros.IslandView
{
    /// <summary>
    /// Paisaje sonoro de la isla, en capas que se mezclan segun la camara (grabaciones CC0, ver docs/SONIDOS.md):
    /// mar abierto de fondo (sube al alejarse), orilla con olas que rompen (sube al acercarse a la costa), olas sueltas
    /// que suenan del lado de la pantalla donde esta la orilla, pajaros de dia, grillos de noche, viento (mas fuerte de
    /// lejos), lluvia y fogata. La musica cambia de dia a noche con un cruce largo.
    /// </summary>
    public sealed class IslandSoundscape : MonoBehaviour
    {
        IslandGame game;
        float waveT = 1.5f;
        bool night;

        /// <summary>0 = dia, 1 = noche (lo maneja el ciclo de dia).</summary>
        public float Night;
        /// <summary>0..1 intensidad de lluvia (clima).</summary>
        public float Rain;
        /// <summary>0..1 cercania a una fogata encendida.</summary>
        public float Fire;

        public void Init(IslandGame g)
        {
            game = g;
            Sfx.PlayTrack("music_day", 3f);
        }

        /// <summary>Cercania de la camara al primer plano: 0 = lo mas lejos, 1 = lo mas cerca.</summary>
        public float Zoom01 { get { return Mathf.InverseLerp(IslandGame.ZoomMax, IslandGame.ZoomMin, game.Cam.orthographicSize); } }

        /// <summary>
        /// 0..1: cuanto de la costa se ve cerca del centro de la pantalla, y el paneo (-1..1) hacia donde queda.
        /// </summary>
        public float Shore(out float pan)
        {
            var cam = game.Cam;
            Vector3 c = game.ScreenToGround(new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f));
            c.y = 0f;
            float coast = game.Isl.Radius + 1.2f;
            float d = c.magnitude;
            Vector3 dir = d > 0.01f ? c / d : Vector3.forward;
            Vector3 shorePt = dir * coast;
            float half = cam.orthographicSize * Mathf.Max(0.4f, cam.aspect);
            float dist = Mathf.Abs(d - coast);
            pan = Mathf.Clamp(Vector3.Dot(shorePt - c, cam.transform.right) / Mathf.Max(half, 0.1f), -1f, 1f) * 0.8f;
            // la camara no puede pasar mucho del borde: a 1-2 m de la costa ya cuenta como "en la orilla"
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, dist - 1.5f) / (4f + half * 0.6f));
        }

        void Update()
        {
            if (game == null) return;
            float dt = Time.deltaTime;
            float z = Zoom01, pan;
            float near = Shore(out pan);
            float day = 1f - Night;
            // mar: fondo siempre; la orilla manda cuando la camara esta cerca de la costa (y mas con zoom)
            Sfx.SetLoop("amb_sea_far", 0.30f + 0.28f * (1f - z), 2f);
            Sfx.SetLoop("amb_sea_near", Mathf.Clamp01(0.06f + near * (0.35f + 0.55f * z)), 1.2f);
            Sfx.SetLoop("amb_wind", 0.10f + 0.26f * (1f - z) + 0.25f * Rain, 2f);
            Sfx.SetLoop("amb_birds", day * (1f - Rain) * (0.22f + 0.38f * z) * (1f - near * 0.4f), 3f);
            Sfx.SetLoop("amb_crickets", Night * (1f - Rain * 0.7f) * (0.25f + 0.35f * z), 3f);
            Sfx.SetLoop("amb_rain", Rain * (0.55f + 0.25f * z), 2.5f);
            Sfx.SetLoop("amb_fire", Fire * (0.2f + 0.6f * z), 1f);

            // olas que rompen, del lado de la orilla
            waveT -= dt;
            if (waveT <= 0f)
            {
                float k = near * (0.35f + 0.65f * z);
                waveT = Mathf.Lerp(6.5f, 2.2f, k) + Random.value * 2f;
                if (k > 0.08f) Sfx.PlayPan("waves", Mathf.Lerp(-26f, -9f, k), 0.92f + Random.value * 0.16f, pan + Random.Range(-0.15f, 0.15f));
            }
            // musica de dia / de noche (con histeresis para no ir y venir)
            if (!night && Night > 0.6f) { night = true; Sfx.PlayTrack("music_night", 5f); }
            else if (night && Night < 0.35f) { night = false; Sfx.PlayTrack("music_day", 5f); }
        }
    }
}
