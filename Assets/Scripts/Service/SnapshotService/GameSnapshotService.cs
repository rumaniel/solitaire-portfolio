using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gateway.Snapshot;
using Model.Game;
using R3;
using Service.GameService;
using Service.StatsService;
using UnityEngine;
using VContainer;

namespace Service.SnapshotService
{
    public class GameSnapshotService : IGameSnapshotService, IDisposable
    {
        [Inject] private IGameSnapshotRepository Repository { get; set; }

        private readonly SemaphoreSlim ioLock = new(1, 1);
        private readonly CancellationTokenSource disposeCts = new();
        private IDisposable autoSaveSubscription;
        private SnapshotKey currentKey;
        private int currentSeed;
        private IGameService gameService;
        private ISessionStatsService statsService;
        private int isLoopRunning;
        private bool pendingSave;

        public UniTask<GameSnapshot> LoadSnapshotAsync(SnapshotKey key)
        {
            return Repository.LoadAsync(key);
        }

        public void StartAutoSave(SnapshotKey key, int seed,
            IGameService gameService, ISessionStatsService statsService)
        {
            StopAutoSave();

            currentKey = key;
            currentSeed = seed;
            this.gameService = gameService;
            this.statsService = statsService;
            Volatile.Write(ref isLoopRunning, 0);
            pendingSave = false;

            autoSaveSubscription = gameService.OnTableStateChanged
                .Debounce(TimeSpan.FromSeconds(1))
                .Subscribe(_ => EnqueueSave());
        }

        public void StopAutoSave()
        {
            autoSaveSubscription?.Dispose();
            autoSaveSubscription = null;
        }

        public async UniTask FlushAsync()
        {
            if (gameService == null || statsService == null) return;
            await RunLockedSave();
        }

        /// <summary>
        /// Synchronously captures the current snapshot and detaches the game/stats sources, then
        /// writes the captured data under the IO lock. Capturing before the first await means a new
        /// owner re-initializing the shared SessionStats can no longer corrupt this save; nulling the
        /// sources immediately also turns any still-queued autosave iteration into a no-op.
        /// </summary>
        public async UniTask FlushAndStopAsync()
        {
            autoSaveSubscription?.Dispose();
            autoSaveSubscription = null;
            if (gameService == null || statsService == null) return;

            var snapshot = GameSnapshotConverter.ToSnapshot(
                currentKey.GameType, currentSeed, currentKey.VariantId,
                gameService.CurrentState, gameService.UndoHistory, statsService.Current);
            var key = currentKey;
            gameService = null;
            statsService = null;

            // No dispose token: the snapshot is already captured, so the write should always
            // drain even when disposal races this fire-and-forget call.
            await ioLock.WaitAsync();
            try
            {
                await Repository.SaveAsync(key, snapshot);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                ioLock.Release();
            }
        }

        public async UniTask ClearSnapshotAsync(SnapshotKey key)
        {
            StopAutoSave();
            // Wait for any in-flight save to complete, then delete
            await ioLock.WaitAsync();
            try
            {
                gameService = null;
                statsService = null;
                await Repository.DeleteAsync(key);
            }
            finally
            {
                ioLock.Release();
            }
        }

        /// <summary>
        /// Non-blocking best-effort final save, then cleanup.
        /// VContainer disposes in reverse registration order — GameSnapshotService is disposed
        /// after IngamePresenter but before GameService/StatsService, so their state is still valid.
        /// </summary>
        public void Dispose()
        {
            StopAutoSave();
            // Signal the save loop to stop; pending WaitAsync(disposeCts.Token) will throw,
            // and the while-loop condition will exit on the next check.
            disposeCts.Cancel();
            // Best-effort final save: only if no in-flight save holds the lock,
            // to avoid concurrent writes to the same snapshot file.
            try
            {
                if (gameService != null && statsService != null && ioLock.Wait(0))
                {
                    try
                    {
                        var snapshot = GameSnapshotConverter.ToSnapshot(
                            currentKey.GameType, currentSeed, currentKey.VariantId,
                            gameService.CurrentState, gameService.UndoHistory,
                            statsService.Current);
                        Repository.SaveAsync(currentKey, snapshot).Forget();
                    }
                    finally
                    {
                        ioLock.Release();
                    }
                }
                // If lock is busy, an in-flight save is already writing the latest state — skip.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            gameService = null;
            statsService = null;
            // ioLock is intentionally not disposed: SemaphoreSlim holds no OS resources,
            // and in-flight saves may still reference it. GC handles cleanup.
            disposeCts.Dispose();
        }

        private void EnqueueSave()
        {
            if (disposeCts.IsCancellationRequested) return;
            if (Interlocked.CompareExchange(ref isLoopRunning, 1, 0) != 0)
            {
                // A save loop is already running; coalesce into next cycle
                pendingSave = true;
                return;
            }
            RunSaveLoop().Forget();
        }

        private async UniTaskVoid RunSaveLoop()
        {
            try
            {
                do
                {
                    pendingSave = false;
                    await RunLockedSave();
                } while (pendingSave && !disposeCts.IsCancellationRequested);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                Volatile.Write(ref isLoopRunning, 0);
            }
        }

        private async UniTask RunLockedSave()
        {
            await ioLock.WaitAsync(disposeCts.Token);
            try
            {
                await SaveCurrentState();
            }
            finally
            {
                ioLock.Release();
            }
        }

        private async UniTask SaveCurrentState()
        {
            if (gameService == null || statsService == null) return;

            try
            {
                var snapshot = GameSnapshotConverter.ToSnapshot(
                    currentKey.GameType,
                    currentSeed,
                    currentKey.VariantId,
                    gameService.CurrentState,
                    gameService.UndoHistory,
                    statsService.Current);

                await Repository.SaveAsync(currentKey, snapshot);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
