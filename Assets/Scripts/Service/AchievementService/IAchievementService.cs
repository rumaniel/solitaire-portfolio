using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Model.Achievement;
using Model.Game;
using Model.Stats;
using R3;

namespace Service.AchievementService
{
    public interface IAchievementService
    {
        UniTask InitializeAsync();

        IReadOnlyList<(IAchievementDefinition Definition, AchievementStatus Status)> GetAll();
        IAchievementDefinition GetDefinition(string id);
        AchievementStatus GetStatus(string id);

        Observable<AchievementUnlockedEvent> OnAchievementUnlocked { get; }
        Observable<Unit> OnProgressChanged { get; }

        /// <summary>Explicitly evaluate game-end-scoped rules (e.g. PerfectRun) right after stats are recorded.</summary>
        void EvaluateOnGameEnd(GameType gameType, LifetimeStats lifetime, SessionStats sessionSnapshot);
    }
}
