using UnityEngine;

namespace Mineros.Fx
{
    /// <summary>
    /// Efectos estilizados propios (nivel "Cartoon FX"), con el estilo unico de ART_BIBLE.md.
    /// CONTRATO (no cambiar firmas). Lo implementa el modulo Fx; el mundo y la UI lo usan.
    /// kinds: hit_spark, crit, dust, debris, rock_break, coin_burst, gem_sparkle, glint, ring, explosion,
    /// confetti, levelup_aura, milestone, meteor_trail, meteor_impact, chest_open, boss_phase, boss_break,
    /// gold_rush, frenzy_trail, smoke, lava_bubble, crystal_glow, unlock_burst.
    /// Tipos desconocidos no hacen nada.
    ///
    /// Notas de uso (unidades: el minero mide ~1.2; escala 1 = roca mediana de ~0.9):
    ///  - pos es el centro del efecto en el mundo. Los anillos, el polvo y las auras nacen a ras del suelo (y = GroundY).
    ///  - tint (opcional) tine el efecto con el color de la roca/bioma: sirve en rock_break, debris, dust, smoke, ring,
    ///    explosion y los destellos. default(Color) = colores propios de cada efecto.
    ///  - Todo sale de un pool por kind: Play/Attach no instancian ni destruyen nada por llamada.
    ///  - "Un foco grande por vez": tras un efecto grande (boss_break, boss_phase, milestone, chest_open, levelup_aura,
    ///    unlock_burst, confetti) los efectos menores se atenuan o se descartan ~0.6 s.
    /// </summary>
    public static class Fx
    {
        /// <summary>Lanza un efecto de una sola vez en pos (mundo). tint: color principal (default = el del efecto).</summary>
        public static void Play(string kind, Vector3 pos, Color tint = default(Color), float scale = 1f)
        {
            FxRuntime.Play(kind, pos, tint, scale);
        }

        /// <summary>Efecto continuo pegado a un transform (estelas, auras). Devuelve el GameObject para destruirlo.</summary>
        public static GameObject Attach(string kind, Transform target, Color tint = default(Color), float scale = 1f)
        {
            return FxRuntime.Attach(kind, target, tint, scale);
        }

        /// <summary>Si es true (modo Eco) los efectos usan menos particulas.</summary>
        public static bool Eco;

        /// <summary>Altura del plano del suelo contra el que rebotan fragmentos, monedas y gemas (por defecto 0).</summary>
        public static float GroundY
        {
            get { return FxRuntime.GroundY; }
            set { FxRuntime.GroundY = value; }
        }

        /// <summary>Arma por adelantado las instancias de un kind (para evitar el primer tiron en una pantalla critica).</summary>
        public static void Prewarm(string kind, int count = 1)
        {
            FxRuntime.Prewarm(kind, count);
        }

        /// <summary>Invierte el eje largo de la textura de chispa (ver docs): usar solo si con licencia las chispas salen de costado.</summary>
        public static bool SparkAlongX
        {
            get { return FxTexGen.SparkAlongX; }
            set { FxTexGen.SparkAlongX = value; }
        }
    }

    /// <summary>
    /// "Sensacion de juego" propia (nivel "Feel"): sacudida, micro-pausa, golpes de escala, destellos, zoom, vibracion.
    /// CONTRATO (no cambiar firmas). Lo implementa el modulo Fx.
    ///
    /// INTEGRACION CON LA CAMARA DEL MUNDO: no hay que hacer nada. Si Juice.Cam esta registrada (o Camera.main existe) el
    /// host oculto desplaza la camara DESPUES del LateUpdate del mundo y deshace el desplazamiento ANTES del Update del
    /// frame siguiente, asi el seguimiento de camara del mundo siempre ve la posicion y el orthographicSize "limpios" y
    /// nunca se acumula. Para sumarlo a mano en cambio (por ejemplo si el mundo usa su propio rig), poner
    /// Juice.ApplyToCamera = false y leer Juice.ShakeOffset (mundo, en el plano de la camara) y Juice.ZoomFactor.
    /// </summary>
    public static class Juice
    {
        /// <summary>Camara que sacude/hace zoom (la registra el mundo al crearla).</summary>
        public static Camera Cam;

        /// <summary>
        /// Sacudida con ruido Perlin y decaimiento cuadratico. amplitude en unidades de mundo (desplazamiento maximo):
        /// 0.04 golpe suave, 0.1 roca rota, 0.25 jefe. Las sacudidas se acumulan con tope suave (MaxShake). Tiempo sin escala.
        /// </summary>
        public static void Shake(float amplitude, float duration) { JuiceCore.Shake(amplitude, duration); }

        /// <summary>Congela el tiempo del juego unos milisegundos (Time.timeScale) y lo restaura solo.</summary>
        public static void HitStop(float seconds) { JuiceCore.HitStop(seconds); }

        /// <summary>Camara lenta temporal.</summary>
        public static void SlowMo(float scale, float seconds) { JuiceCore.SlowMo(scale, seconds); }

        /// <summary>Rebote de escala (aplastar y estirar) sobre un transform.</summary>
        public static void Punch(Transform t, float amount = 0.2f, float duration = 0.25f) { JuiceCore.Punch(t, amount, duration); }

        /// <summary>Destello blanco sobre los renderers de un objeto.</summary>
        public static void Flash(GameObject go, float duration = 0.1f) { JuiceCore.Flash(go, duration); }

        /// <summary>Zoom breve de la camara ortografica.</summary>
        public static void ZoomPunch(float amount = 0.06f, float duration = 0.2f) { JuiceCore.ZoomPunch(amount, duration); }

        /// <summary>Vibracion del telefono (respeta un ajuste global).</summary>
        public static void Vibrate(int milliseconds = 20) { JuiceCore.Vibrate(milliseconds); }

        public static bool VibrationOn = true;

        /// <summary>Si es true (por defecto) el host aplica solo la sacudida y el zoom a Juice.Cam.</summary>
        public static bool ApplyToCamera = true;

        /// <summary>Desplazamiento de camara actual por sacudida (mundo). Para integracion manual.</summary>
        public static Vector3 ShakeOffset
        {
            get
            {
                var c = Cam != null ? Cam : Camera.main;
                if (c == null) return Vector3.zero;
                var t = c.transform;
                return t.right * JuiceCore.ShakeXY.x + t.up * JuiceCore.ShakeXY.y;
            }
        }

        /// <summary>Factor actual del zoom (multiplica el orthographicSize; 1 = sin zoom). Para integracion manual.</summary>
        public static float ZoomFactor { get { return JuiceCore.ZoomFactor; } }

        /// <summary>Tope de la sacudida acumulada, en unidades de mundo.</summary>
        public static float MaxShake
        {
            get { return JuiceCore.MaxShake; }
            set { JuiceCore.MaxShake = Mathf.Max(0.01f, value); }
        }
    }
}
