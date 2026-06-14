using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gateway.Snapshot;
using Model.Board;
using R3;
using Service.BoardGameService;
using Service.StatsService;
using UnityEngine;
using VContainer;

namespace Service.SnapshotService
{
    public class BoardSnapshotService : IBoardSnapshotService, IDisposable
    {
        [Inject] private IBoardSnapshotRepository Repository { get; set; }

        private readonly SemaphoreSlim ioLock = new(1, 1);
        private readonly CancellationTokenSource disposeCts = new();
        private IDisposable autoSaveSubscription;
        private SnapshotKey currentKey;
        private int currentSeed;
        private IBoardGameService gameService;
        private ISessionStatsService statsService;
        private int isLoopRunning;
        private bool pendingSave;

        public UniTask<BoardSnapshot> LoadSnapshotAsync(SnapshotKey key)
        {
            return Repository.LoadAsync(key);
        }

        public void StartAutoSave(SnapshotKey key, int seed,
            IBoardGameService gameService, ISessionStatsService statsService)
        {
            StopAutoSave();

            currentKey = key;
            currentSeed = seed;
            this.gameService = gameService;
            this.statsService = statsService;
            Volatile.Write(ref isLoopRunning, 0);
            pendingSave = false;

            autoSaveSubscription = gameService.OnBoardStateChanged
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

            var snapshot = BoardSnapshotConverter.ToSnapshot(
                currentKey.GameType, currentKey.VariantId, currentSeed,
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
        /// Non-blocking best-effort final save, then cleanup. VContainer disposes in reverse registration
        /// order — BoardSnapshotService is disposed after BoardPresenter but before the game/stats services,
        /// so their state is still valid here.
        /// </summary>
        public void Dispose()
        {
            StopAutoSave();
            disposeCts.Cancel();
            try
            {
                if (gameService != null && statsService != null && ioLock.Wait(0))
                {
                    try
                    {
                        var snapshot = BoardSnapshotConverter.ToSnapshot(
                            currentKey.GameType, currentKey.VariantId, currentSeed,
                            gameService.CurrentState, gameService.UndoHistory,
                            statsService.Current);
                        Repository.SaveAsync(currentKey, snapshot).Forget();
                    }
                    finally
                    {
                        ioLock.Release();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            gameService = null;
            statsService = null;
            disposeCts.Dispose();
        }

        private void EnqueueSave()
        {
            if (disposeCts.IsCancellationRequested) return;
            if (Interlocked.CompareExchange(ref isLoopRunning, 1, 0) != 0)
            {
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
                var snapshot = BoardSnapshotConverter.ToSnapshot(
                    currentKey.GameType,
                    currentKey.VariantId,
                    currentSeed,
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
