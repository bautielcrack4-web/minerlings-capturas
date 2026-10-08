using System;
using UnityEngine;

namespace Mineros.Monetization
{
    /// <summary>
    /// Pedido de reseña nativo (Google Play In-App Review / SKStoreReviewController), solo despues de un momento de
    /// festejo y nunca al abrir: tras 3 dias distintos de juego, como mucho una vez cada 30 dias. La tienda decide si
    /// muestra el cartel (puede no mostrarlo); no se ofrece nada a cambio (lo prohiben ambas tiendas).
    /// </summary>
    public static class Reviews
    {
        const string KDays = "rev_days", KLastDay = "rev_lastday", KAsked = "rev_asked";

        /// <summary>Contar el dia de juego (llamar al arrancar).</summary>
        public static void CountDay()
        {
            int today = (int)(DateTime.UtcNow - new DateTime(2026, 1, 1)).TotalDays;
            if (PlayerPrefs.GetInt(KLastDay, -1) == today) return;
            PlayerPrefs.SetInt(KLastDay, today);
            PlayerPrefs.SetInt(KDays, PlayerPrefs.GetInt(KDays, 0) + 1);
            PlayerPrefs.Save();
        }

        /// <summary>Momento feliz: pedir la reseña si corresponde.</summary>
        public static void HappyMoment()
        {
            int today = (int)(DateTime.UtcNow - new DateTime(2026, 1, 1)).TotalDays;
            if (PlayerPrefs.GetInt(KDays, 0) < 3) return;
            int last = PlayerPrefs.GetInt(KAsked, -999);
            if (today - last < 30) return;
            PlayerPrefs.SetInt(KAsked, today);
            PlayerPrefs.Save();
            Ask();
        }

        static void Ask()
        {
#if UNITY_EDITOR
            Debug.Log("reseña: se pediria ahora");
#elif UNITY_IOS
            UnityEngine.iOS.Device.RequestStoreReview();
#elif UNITY_ANDROID
            try
            {
                var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                var factory = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory");
                var manager = factory.CallStatic<AndroidJavaObject>("create", activity);
                var request = manager.Call<AndroidJavaObject>("requestReviewFlow");
                request.Call<AndroidJavaObject>("addOnCompleteListener", new Done(task =>
                {
                    if (!task.Call<bool>("isSuccessful")) return;
                    var info = task.Call<AndroidJavaObject>("getResult");
                    manager.Call<AndroidJavaObject>("launchReviewFlow", activity, info);
                }));
            }
            catch (Exception e) { Debug.LogWarning("reseña: " + e.Message); }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        sealed class Done : AndroidJavaProxy
        {
            readonly Action<AndroidJavaObject> cb;
            public Done(Action<AndroidJavaObject> cb) : base("com.google.android.gms.tasks.OnCompleteListener") { this.cb = cb; }
            void onComplete(AndroidJavaObject task) { cb(task); }
        }
#endif
    }
}
