using System;

namespace Service.HapticService
{
    public interface IVibratorBridge : IDisposable
    {
        void Vibrate(int durationMs, int amplitude);
    }
}
