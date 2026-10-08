using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Mineros.Monetization
{
    /// <summary>
    /// Anuncios con premio (AdMob). Solo con premio y elegidos por el jugador; nunca intersticiales ni banners.
    /// Orden de arranque: consentimiento (UMP, obligatorio en Europa) → AdMob → precarga del anuncio. En el editor y en
    /// las capturas no hay SDK: el "anuncio" se simula y entrega el premio.
    /// Los ids salen de Resources/monetizacion.json (los escribe MonetizationSetup en cada build): los de prueba de
    /// Google si Codemagic no trae los propios.
    /// </summary>
    public static class Ads
    {
        [Serializable] sealed class Config { public string rewardedAndroid, rewardedIos; }

        static RewardedAd loaded;
        static bool started, loading, initDone;
        static string unitId;
        public static bool Simulated { get { return Application.isEditor; } }

        /// <summary>Hay un anuncio listo para mostrar (o estamos en el editor).</summary>
        public static bool Ready { get { return Simulated || (loaded != null && loaded.CanShowAd()); } }

        /// <summary>Ultimo error de carga (para el cartel y para depurar en el telefono).</summary>
        public static string LastError = "";
        static int fails;

        /// <summary>
        /// Reintentos (auditoria final: si la primera carga fallaba -sin red, sin anuncios en ese momento, consentimiento
        /// sin resolver- nunca se volvia a intentar y el boton decia "cargando" para siempre). 10 s, 30 s, 1 min, 2 min...
        /// </summary>
        sealed class Runner : MonoBehaviour
        {
            public static float RetryAt = -1f;
            void Update()
            {
                if (RetryAt > 0f && Time.unscaledTime >= RetryAt) { RetryAt = -1f; if (initDone) Load(); else if (started) RetryConsent(); }
            }
        }

        static void EnsureRunner()
        {
            if (UnityEngine.Object.FindFirstObjectByType<Runner>() != null) return;
            var go = new GameObject("Anuncios");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        static void ScheduleRetry()
        {
            fails++;
            float[] waits = { 10f, 30f, 60f, 120f, 300f };
            Runner.RetryAt = Time.unscaledTime + waits[Mathf.Min(fails - 1, waits.Length - 1)];
        }

        static void RetryConsent()
        {
            ConsentInformation.Update(new ConsentRequestParameters(), err =>
            {
                if (err != null) { LastError = "consentimiento: " + err.Message; ScheduleRetry(); return; }
                if (ConsentInformation.CanRequestAds()) StartAds();
            });
        }

        public static void Init()
        {
            if (started)
            {
                // ya arranco: si no hay anuncio y no esta cargando, otra carga ya (el jugador lo esta pidiendo)
                if (!Simulated && initDone) Load();
                return;
            }
            started = true;
            if (!Simulated) EnsureRunner();
            if (Simulated) return;
            var ta = Resources.Load<TextAsset>("monetizacion");
            var cfg = ta != null ? JsonUtility.FromJson<Config>(ta.text) : null;
#if UNITY_IOS
            unitId = cfg != null && !string.IsNullOrEmpty(cfg.rewardedIos) ? cfg.rewardedIos : "ca-app-pub-3940256099942544/1712485313";
#else
            unitId = cfg != null && !string.IsNullOrEmpty(cfg.rewardedAndroid) ? cfg.rewardedAndroid : "ca-app-pub-3940256099942544/5224354917";
#endif
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            // consentimiento primero (si el usuario esta en una region que lo pide, aparece el formulario de Google)
            var req = new ConsentRequestParameters();
            ConsentInformation.Update(req, err =>
            {
                if (err != null) { Debug.LogWarning("anuncios: consentimiento " + err.Message); LastError = "consentimiento: " + err.Message; if (!ConsentInformation.CanRequestAds()) ScheduleRetry(); }
                ConsentForm.LoadAndShowConsentFormIfRequired(formErr =>
                {
                    if (formErr != null) Debug.LogWarning("anuncios: formulario " + formErr.Message);
                    if (ConsentInformation.CanRequestAds()) StartAds();
                });
            });
            // si ya habia consentimiento de antes, no esperar al formulario
            if (ConsentInformation.CanRequestAds()) StartAds();
        }

        static void StartAds()
        {
            if (initDone) return;
            initDone = true;
            MobileAds.Initialize(status => Load());
        }

        static void Load()
        {
            if (Simulated || loading || (loaded != null && loaded.CanShowAd())) return;
            loading = true;
            RewardedAd.Load(unitId, new AdRequest(), (ad, error) =>
            {
                loading = false;
                if (error != null || ad == null)
                {
                    LastError = error != null ? error.GetMessage() : "sin anuncio";
                    Debug.LogWarning("anuncios: no cargo " + LastError);
                    ScheduleRetry();
                    return;
                }
                loaded = ad;
                fails = 0;
                LastError = "";
            });
        }

        /// <summary>El usuario esta en una region donde hay que ofrecerle cambiar su consentimiento (Europa, estados de EE. UU.).</summary>
        public static bool PrivacyOptionsRequired
        {
            get { return !Simulated && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
        }

        /// <summary>Formulario de Google para revisar o retirar el consentimiento (Ajustes › Privacidad). `done(error)`.</summary>
        public static void ShowPrivacyOptions(Action<string> done)
        {
            if (Simulated) { done?.Invoke(null); return; }
            ConsentForm.ShowPrivacyOptionsForm(err =>
            {
                done?.Invoke(err != null ? err.Message : null);
                if (ConsentInformation.CanRequestAds()) StartAds();
            });
        }

        /// <summary>Muestra un anuncio con premio; `done(true)` solo si se vio completo.</summary>
        public static void Show(Action<bool> done)
        {
            if (Simulated) { done?.Invoke(true); return; }
            if (loaded == null || !loaded.CanShowAd()) { Load(); done?.Invoke(false); return; }
            var ad = loaded;
            loaded = null;
            bool earned = false;
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); done?.Invoke(earned); Load(); };
            ad.OnAdFullScreenContentFailed += e => { ad.Destroy(); done?.Invoke(false); Load(); };
            ad.Show(r => earned = true);
        }
    }
}
