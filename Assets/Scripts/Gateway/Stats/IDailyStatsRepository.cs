using Cysharp.Threading.Tasks;
using Model.Stats;

namespace Gateway.Stats
{
    public interface IDailyStatsRepository
    {
        UniTask<DailyStats> LoadAsync();
        UniTask SaveAsync(DailyStats stats);
    }
}
