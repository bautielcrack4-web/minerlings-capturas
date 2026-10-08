using UnityEngine;

namespace Mineros.Fx
{
    /// <summary>
    /// Vibraciones con significado (docs/PLAN_PULIDO.md 2.3): en vez de "vibrá 30 ms" se pide "un tick", "un golpe
    /// suave", "éxito". iPhone: Taptic Engine (Plugins/iOS/MinerosHaptics.mm). Android 10+: efectos del sistema
    /// (EFFECT_TICK / CLICK / HEAVY_CLICK / DOUBLE_CLICK); Android viejo: pulsos cortos con amplitud.
    /// Respeta el ajuste del jugador (apagada / suave / normal) y un tope para no saturar.
    /// </summary>
    public static class Haptics
    {
        public enum Kind { Selection = 0, Light = 1, Medium = 2, Heavy = 3, Success = 4, Warning = 5, Error = 6, Soft = 7 }

        /// <summary>0 apagada, 1 suave, 2 normal.</summary>
        public static int Level = 2;

        static float lastT = -10f;
        static Kind lastKind;

        public static void Selection() { Play(Kind.Selection, 1f); }
        public static void Light() { Play(Kind.Light, 1f); }
        public static void Medium() { Play(Kind.Medium, 1f); }
        public static void Heavy() { Play(Kind.Heavy, 1f); }
        public static void Success() { Play(Kind.Success, 1f); }
        public static void Warning() { Play(Kind.Warning, 1f); }
        public static void Error() { Play(Kind.Error, 1f); }
        public static void Soft(float intensity) { Play(Kind.Soft, intensity); }

        /// <summary>Traduce las llamadas viejas en milisegundos a un tipo (asi todo el juego usa el sistema nuevo).</summary>
        public static void FromMs(int ms)
        {
            if (ms <= 10) Selection();
            else if (ms <= 22) Light();
            else if (ms <= 45) Medium();
            else Heavy();
        }

        public static void Play(Kind k, float intensity)
        {
            if (Level <= 0 || !Juice.VibrationOn) return;
            float now = Time.realtimeSinceStartup;
            // tope: 40 ms entre vibraciones (los ticks pueden ir mas seguidos si son del mismo tipo)
            float gap = k == Kind.Selection && lastKind == Kind.Selection ? 0.035f : 0.04f;
            if (now - lastT < gap && (int)k <= (int)lastKind) return;
            lastT = now; lastKind = k;
            if (Level == 1) intensity *= 0.55f;
            if (!Application.isMobilePlatform) return;
#if UNITY_IOS && !UNITY_EDITOR
            MinerosHaptic((int)k, intensity);
#elif UNITY_ANDROID && !UNITY_EDITOR
            Android(k, intensity);
#endif
        }

        /// <summary>Al empezar un arrastre (iPhone): la vibracion siguiente sale sin demora.</summary>
        public static void Prepare()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Level > 0 && Juice.VibrationOn) MinerosHapticPrepare();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void MinerosHaptic(int kind, float intensity);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void MinerosHapticPrepare();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static AndroidJavaClass effect;
        static bool tried;
        static int sdk;
        static bool hasAmp;

        static void Android(Kind k, float intensity)
        {
            try
            {
                if (!tried)
                {
                    tried = true;
                    using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var v = new AndroidJavaClass("android.os.Build$VERSION")) sdk = v.GetStatic<int>("SDK_INT");
                    if (sdk >= 26) { effect = new AndroidJavaClass("android.os.VibrationEffect"); hasAmp = vibrator.Call<bool>("hasAmplitudeControl"); }
                }
                if (vibrator == null) return;
                // Android 10+: efectos predefinidos (se sienten como los del sistema)
                if (sdk >= 29 && k != Kind.Soft)
                {
                    int id = k == Kind.Selection ? 2 /*TICK*/ : k == Kind.Light ? 0 /*CLICK*/ : k == Kind.Success ? 1 /*DOUBLE_CLICK*/ : 5 /*HEAVY_CLICK*/;
                    if (k == Kind.Warning || k == Kind.Error) { Wave(new long[] { 0, 18, 60, 18, 60, 18 }, new[] { 0, 160, 0, 160, 0, 200 }); return; }
                    using (var e = effect.CallStatic<AndroidJavaObject>("createPredefined", id)) vibrator.Call("vibrate", e);
                    return;
                }
                int ms = k == Kind.Selection ? 8 : k == Kind.Light ? 12 : k == Kind.Medium ? 20 : k == Kind.Soft ? 14 : 34;
                int amp = Mathf.Clamp(Mathf.RoundToInt((k == Kind.Selection ? 50 : k == Kind.Light ? 90 : k == Kind.Medium ? 160 : 255) * intensity), 1, 255);
                if (k == Kind.Success) { Wave(new long[] { 0, 14, 70, 22 }, new[] { 0, 140, 0, 220 }); return; }
                if (k == Kind.Warning || k == Kind.Error) { Wave(new long[] { 0, 18, 60, 18, 60, 18 }, new[] { 0, 160, 0, 160, 0, 200 }); return; }
                if (sdk >= 26)
                {
                    using (var e = effect.CallStatic<AndroidJavaObject>("createOneShot", (long)ms, hasAmp ? amp : -1)) vibrator.Call("vibrate", e);
                }
                else vibrator.Call("vibrate", (long)ms);
            }
            catch (System.Exception) { vibrator = null; }
        }

        static void Wave(long[] times, int[] amps)
        {
            if (sdk >= 26)
            {
                using (var e = hasAmp ? effect.CallStatic<AndroidJavaObject>("createWaveform", times, amps, -1) : effect.CallStatic<AndroidJavaObject>("createWaveform", times, -1))
                    vibrator.Call("vibrate", e);
            }
            else vibrator.Call("vibrate", times, -1);
        }
#endif
    }
}
