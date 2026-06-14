using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Service.AchievementService;

namespace Tests.EditMode
{
    /// <summary>In-memory <see cref="IPlatformAchievementService"/> recording every push for assertions.</summary>
    internal sealed class MockPlatformAchievementService : IPlatformAchievementService
    {
        public bool IsAvailable { get; set; } = true;
        public int InitializeCalls { get; private set; }
        public int EnsureSignedInCalls { get; private set; }
        public bool EnsureSignedInResult { get; set; } = true;
        public List<string> Unlocked { get; } = new();
        public List<(string Id, int Current, int Total)> Progress { get; } = new();
        public int ShowUiCalls { get; private set; }

        public UniTask InitializeAsync() { InitializeCalls++; return UniTask.CompletedTask; }
        public UniTask<bool> EnsureSignedInAsync()
        {
            EnsureSignedInCalls++;
            return UniTask.FromResult(EnsureSignedInResult);
        }
        public UniTask UnlockAsync(string platformId) { Unlocked.Add(platformId); return UniTask.CompletedTask; }
        public UniTask SetProgressAsync(string platformId, int currentSteps, int totalSteps)
        {
            Progress.Add((platformId, currentSteps, totalSteps));
            return UniTask.CompletedTask;
        }
        public bool ShowAchievementsUiResult { get; set; } = true;
        public UniTask<bool> ShowAchievementsUiAsync()
        {
            ShowUiCalls++;
            return UniTask.FromResult(ShowAchievementsUiResult);
        }
    }
}
