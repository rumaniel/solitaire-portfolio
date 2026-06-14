using Cysharp.Threading.Tasks;
using Model.Game;

namespace Gateway.Snapshot
{
    /// <summary>Persists and retrieves GameSnapshot instances by SnapshotKey.</summary>
    public interface IGameSnapshotRepository
    {
        UniTask<GameSnapshot> LoadAsync(SnapshotKey key);
        UniTask SaveAsync(SnapshotKey key, GameSnapshot snapshot);
        UniTask DeleteAsync(SnapshotKey key);
    }
}
