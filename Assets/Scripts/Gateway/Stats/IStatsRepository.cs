using Cysharp.Threading.Tasks;
using Model.Game;
using Model.Stats;

namespace Gateway.Stats
{
    public interface IStatsRepository
    {
        UniTask<LifetimeStats> LoadAsync(GameType gameType);
        UniTask SaveAsync(GameType gameType, LifetimeStats stats);
    }
}
