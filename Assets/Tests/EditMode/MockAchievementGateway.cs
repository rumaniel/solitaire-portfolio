using Cysharp.Threading.Tasks;
using Gateway.Achievement;
using MemoryPack;
using Model.Achievement;

namespace Tests.EditMode
{
    /// <summary>In-memory achievement store. Round-trips through MemoryPack to mimic the real gateway.</summary>
    internal class MockAchievementGateway : IAchievementGateway
    {
        private byte[] stored;
        public int LoadCalls { get; private set; }
        public int SaveCalls { get; private set; }

        public UniTask<AchievementStore> LoadAsync()
        {
            LoadCalls++;
            if (stored == null) return UniTask.FromResult<AchievementStore>(null);
            return UniTask.FromResult(MemoryPackSerializer.Deserialize<AchievementStore>(stored));
        }

        public UniTask SaveAsync(AchievementStore store)
        {
            SaveCalls++;
            stored = MemoryPackSerializer.Serialize(store);
            return UniTask.CompletedTask;
        }
    }
}
