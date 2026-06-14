using Cysharp.Threading.Tasks;
using Gateway.Stats;
using Model.Stats;

namespace Tests.EditMode
{
    internal class MockDailyStatsRepository : IDailyStatsRepository
    {
        private DailyStats stored;

        public int SaveCount { get; private set; }

        public UniTask<DailyStats> LoadAsync() => UniTask.FromResult(stored);

        public UniTask SaveAsync(DailyStats stats)
        {
            stored = stats;
            SaveCount++;
            return UniTask.CompletedTask;
        }

        public DailyStats Snapshot() => stored;
    }
}
