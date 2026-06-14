using Cysharp.Threading.Tasks;

namespace Service.AchievementService
{
    /// <summary>No-op fallback for Unity Editor, WebGL, and platforms without a backing achievement service.</summary>
    public sealed class NoopPlatformAchievementService : IPlatformAchievementService
    {
        public bool IsAvailable => false;
        public UniTask InitializeAsync() => UniTask.CompletedTask;
        public UniTask<bool> EnsureSignedInAsync() => UniTask.FromResult(false);
        public UniTask UnlockAsync(string platformId) => UniTask.CompletedTask;
        public UniTask SetProgressAsync(string platformId, int currentSteps, int totalSteps) => UniTask.CompletedTask;
        public UniTask<bool> ShowAchievementsUiAsync() => UniTask.FromResult(false);
    }
}
