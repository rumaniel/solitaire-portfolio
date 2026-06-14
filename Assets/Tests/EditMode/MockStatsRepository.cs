using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Gateway.Stats;
using Model.Game;
using Model.Stats;

namespace Tests.EditMode
{
    internal class MockStatsRepository : IStatsRepository
    {
        private readonly Dictionary<GameType, LifetimeStats> store = new();

        public UniTask<LifetimeStats> LoadAsync(GameType gameType)
        {
            store.TryGetValue(gameType, out var stats);
            return UniTask.FromResult(stats);
        }

        public UniTask SaveAsync(GameType gameType, LifetimeStats stats)
        {
            store[gameType] = stats;
            return UniTask.CompletedTask;
        }
    }
}
