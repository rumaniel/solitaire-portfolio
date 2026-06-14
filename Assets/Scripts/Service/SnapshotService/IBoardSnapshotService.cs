using Cysharp.Threading.Tasks;
using Gateway.Snapshot;
using Model.Board;
using Service.BoardGameService;
using Service.StatsService;

namespace Service.SnapshotService
{
    /// <summary>Auto-save and restore of in-progress board games, keyed by SnapshotKey. Scoped to the Ingame scene.
    /// Board-stack twin of IGameSnapshotService.</summary>
    public interface IBoardSnapshotService
    {
        UniTask<BoardSnapshot> LoadSnapshotAsync(SnapshotKey key);

        void StartAutoSave(SnapshotKey key, int seed,
            IBoardGameService gameService, ISessionStatsService statsService);

        void StopAutoSave();

        UniTask FlushAsync();

        /// <summary>
        /// Captures the current snapshot synchronously (before the first await), detaches the
        /// game/stats sources so any queued autosave becomes a no-op, then persists the captured
        /// data under the IO lock. Use on cross-owner release so the new owner's Initialize cannot
        /// corrupt this save.
        /// </summary>
        UniTask FlushAndStopAsync();

        UniTask ClearSnapshotAsync(SnapshotKey key);
    }
}
