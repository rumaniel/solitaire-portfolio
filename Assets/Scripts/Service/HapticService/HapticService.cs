using R3;
using UnityEngine;

namespace Service.HapticService
{
    public class HapticService : IHapticService
    {
        private const string EnabledKey = "haptic.enabled";

        private readonly IVibratorBridge bridge;
        private readonly Subject<bool> onEnabledChanged = new();
        private bool enabled;

        public HapticService(IVibratorBridge bridge)
        {
            this.bridge = bridge;
            enabled = PlayerPrefs.GetInt(EnabledKey, 1) == 1;
        }

        public bool IsEnabled => enabled;
        public Observable<bool> OnEnabledChanged => onEnabledChanged;

        public void SetEnabled(bool value)
        {
            if (enabled == value) return;
            enabled = value;
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            onEnabledChanged.OnNext(value);
        }

        public void Trigger(HapticTier tier)
        {
            if (!enabled) return;
            var (duration, amplitude) = GetParams(tier);
            bridge.Vibrate(duration, amplitude);
        }

        private static (int duration, int amplitude) GetParams(HapticTier tier) => tier switch
        {
            HapticTier.Selection => (5, 50),
            HapticTier.Light => (10, 80),
            HapticTier.Medium => (20, 160),
            HapticTier.Heavy => (40, 255),
            _ => (10, 80),
        };
    }
}
