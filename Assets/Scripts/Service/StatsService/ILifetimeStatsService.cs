using Cysharp.Threading.Tasks;
using Model.Game;
using Model.Stats;
using R3;

namespace Service.StatsService
{
    public interface ILifetimeStatsService
    {
        UniTask InitializeAsync();
        LifetimeStats GetStats(GameType gameType);
        Observable<(GameType gameType, LifetimeStats stats)> OnStatsChanged { get; }
        UniTask RecordGameResultAsync(GameType gameType, SessionStats sessionStats);
    }
}
