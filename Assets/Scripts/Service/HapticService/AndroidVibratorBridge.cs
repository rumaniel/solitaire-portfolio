using UnityEngine;

namespace Service.HapticService
{
    public class AndroidVibratorBridge : IVibratorBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private AndroidJavaClass vibrationEffectClass;

        public AndroidVibratorBridge()
        {
            // Hold partial state in locals so a mid-init exception can dispose only what was created
            // without leaving the instance in a half-initialized state. Fields are committed only on
            // full success; on failure the locals are disposed and fields stay at their defaults (null).
            // The SDK version decision is encoded directly in `vibrationEffectClass` (non-null = API 26+),
            // so no SDK-int field is kept around.
            AndroidJavaObject pendingVibrator = null;
            AndroidJavaClass pendingEffectClass = null;
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                pendingVibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                int sdkInt = version.GetStatic<int>("SDK_INT");

                if (sdkInt >= 26)
                    pendingEffectClass = new AndroidJavaClass("android.os.VibrationEffect");

                vibrator = pendingVibrator;
                vibrationEffectClass = pendingEffectClass;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Haptic] AndroidVibratorBridge init failed: {ex.Message}");
                pendingEffectClass?.Dispose();
                pendingVibrator?.Dispose();
            }
        }

        public void Vibrate(int durationMs, int amplitude)
        {
            if (vibrator == null) return;
            try
            {
                if (vibrationEffectClass != null)
                {
                    using var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", (long)durationMs, amplitude);
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", (long)durationMs);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Haptic] vibrate failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            vibrationEffectClass?.Dispose();
            vibrationEffectClass = null;
            vibrator?.Dispose();
            vibrator = null;
        }
#else
        public void Vibrate(int durationMs, int amplitude) { }
        public void Dispose() { }
#endif
    }
}
