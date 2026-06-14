using Cysharp.Threading.Tasks;
using Model.Board;

namespace Gateway.Snapshot
{
    /// <summary>Persists and retrieves BoardSnapshot instances by SnapshotKey. Board-stack twin of IGameSnapshotRepository.</summary>
    public interface IBoardSnapshotRepository
    {
        UniTask<BoardSnapshot> LoadAsync(SnapshotKey key);
        UniTask SaveAsync(SnapshotKey key, BoardSnapshot snapshot);
        UniTask DeleteAsync(SnapshotKey key);
    }
}
