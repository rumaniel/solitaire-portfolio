using Cysharp.Threading.Tasks;
using Gateway.Snapshot;
using Model.Game;
using Service.GameService;
using Service.StatsService;

namespace Service.SnapshotService
{
    /// <summary>Manages auto-save and restore of in-progress games, keyed by SnapshotKey. Scoped to Ingame scene.</summary>
    public interface IGameSnapshotService
    {
        /// <summary>Loads the snapshot for the given key, or returns <c>null</c> if none exists.</summary>
        UniTask<GameSnapshot> LoadSnapshotAsync(SnapshotKey key);

        /// <summary>
        /// Begins debounced auto-save for the active game. The (GameType, VariantId)
        /// pair embedded in <paramref name="key"/> determines the snapshot file path.
        /// </summary>
        void StartAutoSave(SnapshotKey key, int seed,
            IGameService gameService, ISessionStatsService statsService);

        void StopAutoSave();

        UniTask FlushAsync();

        /// <summary>
        /// Captures the current snapshot synchronously (before the first await), detaches the
        /// game/stats sources so any queued autosave becomes a no-op, then persists the captured
        /// data under the IO lock. Use on cross-owner release so the new owner's Initialize cannot
        /// corrupt this save.
        /// </summary>
        UniTask FlushAndStopAsync();

        /// <summary>Removes the snapshot file for the given key.</summary>
        UniTask ClearSnapshotAsync(SnapshotKey key);
    }
}
