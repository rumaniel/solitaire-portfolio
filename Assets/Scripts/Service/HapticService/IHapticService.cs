using R3;

namespace Service.HapticService
{
    public interface IHapticService
    {
        bool IsEnabled { get; }
        Observable<bool> OnEnabledChanged { get; }

        void Trigger(HapticTier tier);
        void SetEnabled(bool enabled);
    }
}
