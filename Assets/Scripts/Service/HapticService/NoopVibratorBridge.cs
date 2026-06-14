namespace Service.HapticService
{
    /// <summary>No-op bridge used on every target where native vibration isn't wired —
    /// the Unity Editor and all non-Android player builds (iOS, WebGL, Standalone).
    /// Logs in Editor / development builds only; production builds stay silent to avoid log spam.</summary>
    public class NoopVibratorBridge : IVibratorBridge
    {
        public void Vibrate(int durationMs, int amplitude)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[Haptic] no-op (unsupported target) duration={durationMs}ms amplitude={amplitude}");
#endif
        }

        public void Dispose() { }
    }
}
