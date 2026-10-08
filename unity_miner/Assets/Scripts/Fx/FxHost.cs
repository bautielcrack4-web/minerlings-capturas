using UnityEngine;

namespace Mineros.Fx
{
    /// <summary>
    /// Host oculto (DontDestroyOnLoad, creado a demanda) que mueve Fx y Juice. Dos componentes con orden de ejecucion
    /// extremo para no pelear con el seguimiento de camara del mundo:
    ///   FxEarly (-32000): en Update, ANTES de cualquier script del mundo, deshace el desplazamiento de camara del frame anterior,
    ///                     asi el mundo siempre lee/escribe la camara "limpia".
    ///   FxLate  (+32000): en Update corre el reloj (sacudida, zoom, tiempo, reciclaje de efectos) y en LateUpdate, DESPUES del
    ///                     mundo, aplica el desplazamiento y los golpes de escala.
    /// </summary>
    internal static class FxHost
    {
        static GameObject host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { host = null; }

        internal static void Ensure()
        {
            if (host != null || !Application.isPlaying) return;
            host = new GameObject("~MinerosFx");
            host.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(host);
            host.AddComponent<FxEarly>();
            host.AddComponent<FxLate>();
        }
    }

    [DefaultExecutionOrder(-32000)]
    [AddComponentMenu("")]
    internal sealed class FxEarly : MonoBehaviour
    {
        void Update() { JuiceCore.CameraRevert(); }
        void OnDisable() { JuiceCore.CameraRevert(); }
        void OnDestroy() { JuiceCore.CameraRevert(); }
    }

    [DefaultExecutionOrder(32000)]
    [AddComponentMenu("")]
    internal sealed class FxLate : MonoBehaviour
    {
        void Update()
        {
            JuiceCore.Update();
            FxRuntime.Tick();
        }

        void LateUpdate()
        {
            JuiceCore.LateTick();
            JuiceCore.CameraApply();
        }

        void OnDisable() { JuiceCore.RestoreTime(); }
        void OnDestroy() { JuiceCore.RestoreTime(); }
        void OnApplicationQuit()
        {
            FxRuntime.Quitting = true;
            JuiceCore.RestoreTime();
        }
    }
}
