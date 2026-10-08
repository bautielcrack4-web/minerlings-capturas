using Mineros.Core;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

namespace Mineros.IslandView
{
    /// <summary>
    /// Avisos locales honestos (auditoria final, retencion D1): al salir del juego se programan como mucho dos, con la
    /// hora real en que pasan las cosas: la obra mas cercana que termina y el cofre de lo ganado sin conexion lleno
    /// (despues de ese tope los mineros dejan de juntar). Nada de "te extrañamos". Al volver se borran.
    /// El permiso (Android 13+) se pide una sola vez, recien al terminar el tutorial.
    /// </summary>
    public static class IslandNotify
    {
        const string Channel = "minerlings_isla";
        const string AskedKey = "notif_pedido_v1", OffKey = "isla_avisos_off";

        public static bool Enabled { get { return PlayerPrefs.GetInt(OffKey, 0) == 0; } set { PlayerPrefs.SetInt(OffKey, value ? 0 : 1); } }

        /// <summary>Al terminar el tutorial (el jugador ya sabe para que sirve el aviso).</summary>
        public static void AskOnce()
        {
            if (PlayerPrefs.GetInt(AskedKey, 0) == 1) return;
            PlayerPrefs.SetInt(AskedKey, 1);
#if UNITY_ANDROID && !UNITY_EDITOR
            try { new PermissionRequest(); } catch (System.Exception e) { Debug.LogWarning("avisos: " + e.Message); }
#endif
        }

        public static void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { AndroidNotificationCenter.CancelAllScheduledNotifications(); AndroidNotificationCenter.CancelAllDisplayedNotifications(); }
            catch (System.Exception e) { Debug.LogWarning("avisos: " + e.Message); }
#endif
        }

        /// <summary>Lo que se avisaria ahora (tambien para las capturas): segundos hasta el aviso y el texto.</summary>
        public static void Plan(Island isl, out double workS, out string workText, out double chestS, out string chestText)
        {
            workS = -1; workText = null;
            foreach (var p in isl.Plots)
            {
                if (p.Building < 0 || p.Work <= 0) continue;
                if (p.Work < 300) continue;   // las de 5 min o menos se terminan gratis: no hace falta avisar
                if (workS < 0 || p.Work < workS) { workS = p.Work; workText = Island.Def((BKind)p.Building).Name + Loc.T(" está terminado. ¡Vení a verlo!"); }
            }
            chestS = isl.TutDone && isl.Miners.Count > 0 ? isl.OfflineCap() : -1;
            chestText = Loc.T("El cofre de tus mineros se llenó: después de esto no juntan más.");
        }

        /// <summary>Al pausar/salir: programa los avisos (como mucho dos).</summary>
        public static void Schedule(Island isl)
        {
            Cancel();
            if (!Enabled || isl == null || !isl.TutDone) return;
            double workS, chestS; string workText, chestText;
            Plan(isl, out workS, out workText, out chestS, out chestText);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
                {
                    Id = Channel, Name = "Minerlings", Importance = Importance.Default,
                    Description = Loc.T("Obras terminadas y cofre lleno"),
                });
                if (workS > 0) Send(Loc.T("¡Obra terminada!"), workText, workS);
                if (chestS > 0) Send(Loc.T("Cofre lleno"), chestText, chestS);
                double wk = WeekendIn();
                if (wk > 0) Send(Loc.T("Fin de semana dorado"), Loc.T("Hoy y mañana: más vetas gigantes y legendarios x3."), wk);
            }
            catch (System.Exception e) { Debug.LogWarning("avisos: " + e.Message); }
#endif
        }

        /// <summary>Segundos hasta el proximo sabado a las 10 (si falta menos de una semana y hoy no es fin de semana).</summary>
        public static double WeekendIn()
        {
            var now = System.DateTime.Now;
            if (now.DayOfWeek == System.DayOfWeek.Saturday || now.DayOfWeek == System.DayOfWeek.Sunday) return -1;
            int days = ((int)System.DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
            var at = now.Date.AddDays(days).AddHours(10);
            return (at - now).TotalSeconds;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void Send(string title, string text, double seconds)
        {
            var n = new AndroidNotification { Title = title, Text = text, FireTime = System.DateTime.Now.AddSeconds(seconds) };
            AndroidNotificationCenter.SendNotification(n, Channel);
        }
#endif
    }
}
