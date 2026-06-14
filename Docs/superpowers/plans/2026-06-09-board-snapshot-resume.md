# Board Snapshot / Resume Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Auto-save and resume in-progress Pyramid board games at full parity with the card stack — debounced auto-save, lobby "Continue" badge, restore on re-entry, clear on finish/new-game.

**Architecture:** Mirror the card persistence stack (do not genericize the working code): a `BoardSnapshot`-typed repository + a `BoardSnapshotService` that is a structural twin of `GameSnapshotService`, wired into `BoardPresenter` (load/restore + auto-save + flush + clear) and surfaced by a type-agnostic lobby continue-scan.

**Tech Stack:** Unity 6, C#, R3, UniTask, MemoryPack, VContainer, NUnit (EditMode), `uloop` editor CLI.

**Spec:** `Docs/superpowers/specs/2026-06-09-board-snapshot-resume-design.md`

---

## Background the engineer needs

- **Existing card stack to mirror** (read these first): `Assets/Scripts/Gateway/Snapshot/LocalGameSnapshotRepository.cs`, `Assets/Scripts/Gateway/Snapshot/IGameSnapshotRepository.cs`, `Assets/Scripts/Service/SnapshotService/GameSnapshotService.cs`, `Assets/Scripts/Service/SnapshotService/IGameSnapshotService.cs`. The board files are line-for-line analogues with the types swapped.
- **Already exists, do not recreate:** `Model/Board/BoardSnapshot.cs` (MemoryPackable), `Model/Board/BoardSnapshotConverter.cs` (`ToSnapshot`, `ToBoardState`, `ToHistory`, `ToSessionStats`), `Gateway/Snapshot/SnapshotKey.cs`, and `BoardGameService.Restore(...)`. `BoardSnapshotConverter` is unit-tested by `Assets/Tests/EditMode/BoardSnapshotTests.cs`.
- **`BoardSnapshotConverter.ToSnapshot` signature (note arg order — variant BEFORE seed):**
  `ToSnapshot(GameType gameType, int variant, int seed, BoardState currentState, IReadOnlyCollection<BoardState> undoHistory, SessionStats stats)`.
- **`BoardGameService.Restore` signature:**
  `Restore(BoardLayout layout, IBoardMatchRule rule, int seed, BoardState state, IReadOnlyList<BoardState> undoHistory, int maxRecycles = 0)`.
- **`IBoardGameService`** exposes `Observable<BoardState> OnBoardStateChanged`, `BoardState CurrentState`, `int? CurrentSeed`, `IReadOnlyCollection<BoardState> UndoHistory`.
- **`ISessionStatsService.Restore(IScoreRule scoreRule, SessionStats stats)`** — board passes its `zeroScoreRule` field.
- **`IngameQuery`** (constructed `new IngameQuery(RouteService.CurrentQuery)`) exposes `GameType`, `int? Variant`, `int? Seed`, `bool IsContinue`.
- **`GameType.IsBoardMode()`** extension already used in `LobbyPresenter.OnTileSelected`.
- **`uloop` recompile dance** (CLAUDE.md): after editing `.cs`,
  ```bash
  uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"
  ```
  then poll `uloop compile` until Message is not "Unity is compiling…"/"Domain Reload" (a one-shot `ErrorCount:1` with `Errors:[]` mid-reload is STALE — re-poll).
- **EditMode baseline: 417 passed / 0 failed / 6 skipped.** This feature adds no EditMode tests (the serialization logic is already covered by `BoardSnapshotTests`; the repo/service/presenter/lobby are IO+timing+multi-scene and are **play-verified** in Task 5 — the same way the card repo/service are not unit-tested). Every code task must keep EditMode at 417.
- **Commit trailer (verbatim, generic):** `Co-Authored-By: Claude <noreply@anthropic.com>`
- **Never `git add -A`.** Stage only the files each task lists. Do not push.

---

## File Structure

- **Create** `Assets/Scripts/Gateway/Snapshot/IBoardSnapshotRepository.cs` — board snapshot persistence contract.
- **Create** `Assets/Scripts/Gateway/Snapshot/LocalBoardSnapshotRepository.cs` — MemoryPack file impl.
- **Create** `Assets/Scripts/Service/SnapshotService/IBoardSnapshotService.cs` — board auto-save/restore contract.
- **Create** `Assets/Scripts/Service/SnapshotService/BoardSnapshotService.cs` — twin of `GameSnapshotService`.
- **Modify** `Assets/Scripts/App/AppLifetimeScope.cs` — register the board repo (singleton).
- **Modify** `Assets/Scripts/Scene/Board/BoardScene.cs` — register the board snapshot service (scoped).
- **Modify** `Assets/Scripts/Scene/Board/BoardPresenter.cs` — load/restore, auto-save, flush, clear, dispose.
- **Modify** `Assets/Scripts/Scene/Lobby/LobbyPresenter.cs` — type-agnostic elapsed scan + board repo.
- **Modify** `Assets/Scripts/Scene/Lobby/LobbyComponent.cs` — `ApplySnapshots` takes an elapsed map.

---

### Task 1: Board snapshot repository

**Files:**
- Create: `Assets/Scripts/Gateway/Snapshot/IBoardSnapshotRepository.cs`
- Create: `Assets/Scripts/Gateway/Snapshot/LocalBoardSnapshotRepository.cs`

Compile-only (mirrors the untested card repo; persistence is play-verified in Task 5).

- [ ] **Step 1: Create the interface**

`Assets/Scripts/Gateway/Snapshot/IBoardSnapshotRepository.cs`:
```csharp
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
```

- [ ] **Step 2: Create the local impl**

`Assets/Scripts/Gateway/Snapshot/LocalBoardSnapshotRepository.cs` (identical to `LocalGameSnapshotRepository` with `BoardSnapshot` in place of `GameSnapshot`; same `snapshot_<key>.bin` path — board and card never collide because `SnapshotKey` embeds `GameType`):
```csharp
using System;
using System.IO;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Model.Board;
using UnityEngine;

namespace Gateway.Snapshot
{
    public class LocalBoardSnapshotRepository : IBoardSnapshotRepository
    {
        private static string GetPath(SnapshotKey key)
            => Path.Combine(Application.persistentDataPath, string.Concat("snapshot_", key.ToString(), ".bin"));

        public async UniTask<BoardSnapshot> LoadAsync(SnapshotKey key)
        {
            var path = GetPath(key);
            if (!File.Exists(path))
                return null;

            try
            {
                byte[] bytes;
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    bytes = File.ReadAllBytes(path);
                else
                    bytes = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(path));

                return MemoryPackSerializer.Deserialize<BoardSnapshot>(bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoardSnapshot] Failed to load snapshot for {key}: {e.Message}");
                return null;
            }
        }

        public async UniTask SaveAsync(SnapshotKey key, BoardSnapshot snapshot)
        {
            var path = GetPath(key);
            var bytes = MemoryPackSerializer.Serialize(snapshot);

            try
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    EnsureDirectory(path);
                    File.WriteAllBytes(path, bytes);
                }
                else
                {
                    await UniTask.RunOnThreadPool(() =>
                    {
                        EnsureDirectory(path);
                        File.WriteAllBytes(path, bytes);
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoardSnapshot] Failed to save snapshot for {key}: {e.Message}");
            }
        }

        public async UniTask DeleteAsync(SnapshotKey key)
        {
            var path = GetPath(key);
            if (!File.Exists(path))
                return;

            try
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    File.Delete(path);
                else
                    await UniTask.RunOnThreadPool(() => File.Delete(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoardSnapshot] Failed to delete snapshot for {key}: {e.Message}");
            }
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
```

- [ ] **Step 3: Compile**

Run the recompile dance, then `uloop compile`. Expected `ErrorCount: 0`, `WarningCount: 0`.

- [ ] **Step 4: Commit**
```bash
git add Assets/Scripts/Gateway/Snapshot/IBoardSnapshotRepository.cs Assets/Scripts/Gateway/Snapshot/IBoardSnapshotRepository.cs.meta Assets/Scripts/Gateway/Snapshot/LocalBoardSnapshotRepository.cs Assets/Scripts/Gateway/Snapshot/LocalBoardSnapshotRepository.cs.meta
git commit -m "feat(board): BoardSnapshot repository (MemoryPack file)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Board snapshot service

**Files:**
- Create: `Assets/Scripts/Service/SnapshotService/IBoardSnapshotService.cs`
- Create: `Assets/Scripts/Service/SnapshotService/BoardSnapshotService.cs`

Compile-only (orchestration + IO + timing; play-verified in Task 5).

- [ ] **Step 1: Create the interface**

`Assets/Scripts/Service/SnapshotService/IBoardSnapshotService.cs`:
```csharp
using Cysharp.Threading.Tasks;
using Gateway.Snapshot;
using Model.Board;
using Service.BoardGameService;
using Service.StatsService;

namespace Service.SnapshotService
{
    /// <summary>Auto-save and restore of in-progress board games, keyed by SnapshotKey. Scoped to BoardScene.
    /// Board-stack twin of IGameSnapshotService.</summary>
    public interface IBoardSnapshotService
    {
        UniTask<BoardSnapshot> LoadSnapshotAsync(SnapshotKey key);

        void StartAutoSave(SnapshotKey key, int seed,
            IBoardGameService gameService, ISessionStatsService statsService);

        void StopAutoSave();

        UniTask FlushAsync();

        UniTask FlushAndStopAsync();

        UniTask ClearSnapshotAsync(SnapshotKey key);
    }
}
```

- [ ] **Step 2: Create the service**

`Assets/Scripts/Service/SnapshotService/BoardSnapshotService.cs` — structural twin of `GameSnapshotService`. Differences from the card version: `IBoardSnapshotRepository`/`IBoardGameService`/`BoardSnapshot`, `OnBoardStateChanged`, and `BoardSnapshotConverter.ToSnapshot(gameType, **variant, seed**, ...)` (note the variant-before-seed arg order):
```csharp
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

        public async UniTask FlushAndStopAsync()
        {
            autoSaveSubscription?.Dispose();
            autoSaveSubscription = null;
            if (gameService != null && statsService != null)
                await RunLockedSave();
            gameService = null;
            statsService = null;
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
```

- [ ] **Step 3: Compile** — recompile dance, `uloop compile` → `ErrorCount: 0`, `WarningCount: 0`.

- [ ] **Step 4: Commit**
```bash
git add Assets/Scripts/Service/SnapshotService/IBoardSnapshotService.cs Assets/Scripts/Service/SnapshotService/IBoardSnapshotService.cs.meta Assets/Scripts/Service/SnapshotService/BoardSnapshotService.cs Assets/Scripts/Service/SnapshotService/BoardSnapshotService.cs.meta
git commit -m "feat(board): BoardSnapshotService (debounced auto-save twin of GameSnapshotService)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: DI registration

**Files:**
- Modify: `Assets/Scripts/App/AppLifetimeScope.cs`
- Modify: `Assets/Scripts/Scene/Board/BoardScene.cs`

- [ ] **Step 1: Register the board repo (App singleton)**

In `AppLifetimeScope.cs`, find:
```csharp
            // Snapshot
            builder.Register<LocalGameSnapshotRepository>(Lifetime.Singleton).As<IGameSnapshotRepository>();
```
Replace with:
```csharp
            // Snapshot
            builder.Register<LocalGameSnapshotRepository>(Lifetime.Singleton).As<IGameSnapshotRepository>();
            builder.Register<LocalBoardSnapshotRepository>(Lifetime.Singleton).As<IBoardSnapshotRepository>();
```
(`using Gateway.Snapshot;` is already present.)

- [ ] **Step 2: Register the board snapshot service (BoardScene scoped)**

In `BoardScene.cs`, add `using Service.SnapshotService;` to the using block (alongside `using Service.StatsService;`). Then find:
```csharp
            builder.Register<SessionStatsService>(Lifetime.Scoped).As<ISessionStatsService>();
```
Add immediately after it:
```csharp
            builder.Register<BoardSnapshotService>(Lifetime.Scoped).As<IBoardSnapshotService>();
```

- [ ] **Step 3: Compile + EditMode**

Recompile dance, `uloop compile` → 0/0. Then `uloop run-tests --test-mode EditMode` → **417 passed / 0 failed / 6 skipped**.

- [ ] **Step 4: Commit**
```bash
git add Assets/Scripts/App/AppLifetimeScope.cs Assets/Scripts/Scene/Board/BoardScene.cs
git commit -m "feat(board): register board snapshot repo + service in DI

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: BoardPresenter — load/restore, auto-save, flush, clear

**Files:**
- Modify: `Assets/Scripts/Scene/Board/BoardPresenter.cs`

Ten labeled edits in one file. Apply each exactly.

- [ ] **Edit A — usings.** Find:
```csharp
using Data.Audio;
using Model.Board;
```
Replace with:
```csharp
using Data.Audio;
using Gateway.Snapshot;
using Model.Board;
using Service.SnapshotService;
```

- [ ] **Edit B — inject the service.** Find:
```csharp
        [Inject] private ILifetimeStatsService LifetimeStats { get; set; }
```
Add immediately after:
```csharp
        [Inject] private IBoardSnapshotService SnapshotService { get; set; }
```

- [ ] **Edit C — snapshot key field.** Find:
```csharp
        private int prevTotal;
        private int prevRecycleCount;
```
Replace with:
```csharp
        private int prevTotal;
        private int prevRecycleCount;
        private SnapshotKey currentSnapshotKey;
```

- [ ] **Edit D — make init async.** Find:
```csharp
        private void InitializeGame()
        {
            lifeCts?.Cancel();
```
Replace with:
```csharp
        private void InitializeGame() => InitializeGameAsync().Forget();

        private async UniTaskVoid InitializeGameAsync()
        {
            lifeCts?.Cancel();
```

- [ ] **Edit E — record the key.** Find:
```csharp
            currentGameType = query.GameType;
            int variant = query.Variant ?? 1;
```
Replace with:
```csharp
            currentGameType = query.GameType;
            int variant = query.Variant ?? 1;
            currentSnapshotKey = new SnapshotKey(currentGameType, variant);
```

- [ ] **Edit F — load/restore or fresh-deal.** Find:
```csharp
            BoardGameService.Initialize(layout, matchRule, query.Seed, maxRecycles);
            SessionStats.Initialize(zeroScoreRule);
```
Replace with:
```csharp
            // Resume only an explicit continue with no forced seed; otherwise deal fresh and drop any
            // stale save (covers New Game / Restart, which re-enter here without the continue flag).
            bool canLoad = query.IsContinue && query.Seed == null;
            BoardSnapshot snapshot = canLoad ? await SnapshotService.LoadSnapshotAsync(currentSnapshotKey) : null;
            if (snapshot != null)
            {
                BoardGameService.Restore(layout, matchRule, snapshot.Seed,
                    BoardSnapshotConverter.ToBoardState(snapshot.CurrentState),
                    BoardSnapshotConverter.ToHistory(snapshot.UndoHistory), maxRecycles);
                SessionStats.Restore(zeroScoreRule, BoardSnapshotConverter.ToSessionStats(snapshot.Stats));
            }
            else
            {
                await SnapshotService.ClearSnapshotAsync(currentSnapshotKey);
                BoardGameService.Initialize(layout, matchRule, query.Seed, maxRecycles);
                SessionStats.Initialize(zeroScoreRule);
            }
```

- [ ] **Edit G — start auto-save after the subscriptions.** Find (the tail of `InitializeGameAsync`, the `OnSelectionChanged` subscription's end followed by the method close):
```csharp
                    if (sel.Cells.Count > 0 || sel.WasteSelected)
                        AudioService.Play(AudioCatalog.Card.Place);
                })
                .AddTo(gameSubscriptions);
        }
```
Replace with:
```csharp
                    if (sel.Cells.Count > 0 || sel.WasteSelected)
                        AudioService.Play(AudioCatalog.Card.Place);
                })
                .AddTo(gameSubscriptions);

            SnapshotService.StartAutoSave(currentSnapshotKey, BoardGameService.CurrentSeed.Value,
                BoardGameService, SessionStats);
        }
```

- [ ] **Edit H — clear on win.** Find:
```csharp
        private void HandleWin()
        {
            SessionStats.MarkWon();
```
Replace with:
```csharp
        private void HandleWin()
        {
            SnapshotService.ClearSnapshotAsync(currentSnapshotKey).Forget(); // a finished game must not resume
            SessionStats.MarkWon();
```

- [ ] **Edit I — clear on stuck.** Find:
```csharp
            if (!SessionStats.Current.IsFinished && !BoardGameService.HasAnyMove(next))
            {
                AudioService.Play(AudioCatalog.Game.Stuck);
                Shell.ShowStuckPanel(BoardGameService.CanUndo);
            }
```
Replace with:
```csharp
            if (!SessionStats.Current.IsFinished && !BoardGameService.HasAnyMove(next))
            {
                SnapshotService.ClearSnapshotAsync(currentSnapshotKey).Forget(); // stuck = game over; don't resume
                AudioService.Play(AudioCatalog.Game.Stuck);
                Shell.ShowStuckPanel(BoardGameService.CanUndo);
            }
```

- [ ] **Edit J — flush on pause + app-background; stop auto-save on dispose.**

(J1) Find:
```csharp
            Shell.OnPauseObservable().Subscribe(_ =>
            {
                AudioService.Play(AudioCatalog.UI.Open);
                SessionStats.Pause();
                AudioService.Pause();
                Shell.ShowPausePanel();
            }).AddTo(disposable);
```
Replace with:
```csharp
            Shell.OnPauseObservable().Subscribe(_ =>
            {
                AudioService.Play(AudioCatalog.UI.Open);
                SessionStats.Pause();
                AudioService.Pause();
                SnapshotService.FlushAsync().Forget();
                Shell.ShowPausePanel();
            }).AddTo(disposable);
```

(J2) Find:
```csharp
            Shell.OnApplicationPauseObservable().Subscribe(paused =>
            {
                if (paused) { SessionStats.Pause(); AudioService.Pause(); }
                else { SessionStats.Resume(); AudioService.UnPause(); }
            }).AddTo(disposable);
```
Replace with:
```csharp
            Shell.OnApplicationPauseObservable().Subscribe(paused =>
            {
                if (paused) { SessionStats.Pause(); AudioService.Pause(); SnapshotService.FlushAsync().Forget(); }
                else { SessionStats.Resume(); AudioService.UnPause(); }
            }).AddTo(disposable);
```

(J3) Find:
```csharp
        public void Dispose()
        {
            lifeCts?.Cancel();
            lifeCts?.Dispose();
```
Replace with:
```csharp
        public void Dispose()
        {
            SnapshotService.StopAutoSave();
            lifeCts?.Cancel();
            lifeCts?.Dispose();
```

- [ ] **Step K — compile + EditMode.** Recompile dance, `uloop compile` → 0/0. `uloop run-tests --test-mode EditMode` → **417 passed**. If it won't compile or a test fails, STOP and report BLOCKED with the exact error.

- [ ] **Step L — commit**
```bash
git add Assets/Scripts/Scene/Board/BoardPresenter.cs
git commit -m "feat(board): wire snapshot load/restore + auto-save + clear into presenter

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: Lobby parity — type-agnostic continue scan

**Files:**
- Modify: `Assets/Scripts/Scene/Lobby/LobbyPresenter.cs`
- Modify: `Assets/Scripts/Scene/Lobby/LobbyComponent.cs`

- [ ] **Edit A — LobbyPresenter: inject the board repo.** Find:
```csharp
        [Inject] private IGameSnapshotRepository SnapshotRepository { get; set; }
```
Add immediately after:
```csharp
        [Inject] private IBoardSnapshotRepository BoardSnapshotRepository { get; set; }
```

- [ ] **Edit B — LobbyPresenter: replace the snapshot dict with an elapsed map.** Find:
```csharp
        private readonly Dictionary<SnapshotKey, GameSnapshot> snapshots = new();
```
Replace with:
```csharp
        // Presence = a resumable save exists for that key; value = its elapsed seconds (for the badge).
        // Type-agnostic so board and card saves share one path.
        private readonly Dictionary<SnapshotKey, float> snapshotElapsed = new();
```

- [ ] **Edit C — LobbyPresenter: populate the elapsed map.** Find:
```csharp
        private async UniTask LoadSnapshotsAsync()
        {
            snapshots.Clear();

            var keys = new List<SnapshotKey>();
            foreach (var key in Component.GetActiveSnapshotKeys())
                keys.Add(key);

            if (keys.Count == 0)
            {
                Component.ApplySnapshots(snapshots);
                return;
            }

            var tasks = new UniTask<(SnapshotKey, GameSnapshot)>[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                tasks[i] = LoadOneAsync(keys[i]);

            var results = await UniTask.WhenAll(tasks);
            foreach (var (key, snapshot) in results)
            {
                if (snapshot != null)
                    snapshots[key] = snapshot;
            }

            Component.ApplySnapshots(snapshots);
        }
```
Replace with:
```csharp
        private async UniTask LoadSnapshotsAsync()
        {
            snapshotElapsed.Clear();

            var keys = new List<SnapshotKey>();
            foreach (var key in Component.GetActiveSnapshotKeys())
                keys.Add(key);

            if (keys.Count == 0)
            {
                Component.ApplySnapshots(snapshotElapsed);
                return;
            }

            var tasks = new UniTask<(SnapshotKey, float?)>[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                tasks[i] = LoadOneAsync(keys[i]);

            var results = await UniTask.WhenAll(tasks);
            foreach (var (key, elapsed) in results)
            {
                if (elapsed.HasValue)
                    snapshotElapsed[key] = elapsed.Value;
            }

            Component.ApplySnapshots(snapshotElapsed);
        }
```

- [ ] **Edit D — LobbyPresenter: branch `LoadOneAsync` by stack.** Find:
```csharp
        private async UniTask<(SnapshotKey key, GameSnapshot snapshot)> LoadOneAsync(SnapshotKey key)
        {
            try
            {
                var snapshot = await SnapshotRepository.LoadAsync(key);
                return (key, snapshot);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Lobby] Failed to load snapshot for {key}: {ex.Message}");
                return (key, null);
            }
        }
```
Replace with:
```csharp
        private async UniTask<(SnapshotKey key, float? elapsed)> LoadOneAsync(SnapshotKey key)
        {
            try
            {
                if (key.GameType.IsBoardMode())
                {
                    var board = await BoardSnapshotRepository.LoadAsync(key);
                    return (key, board?.Stats?.ElapsedSeconds);
                }

                var snapshot = await SnapshotRepository.LoadAsync(key);
                return (key, snapshot?.Stats?.ElapsedSeconds);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Lobby] Failed to load snapshot for {key}: {ex.Message}");
                return (key, null);
            }
        }
```

- [ ] **Edit E — LobbyPresenter: existence check in tile routing.** Find:
```csharp
            var key = new SnapshotKey(selection.GameType, selection.Variant);
            var hasMatchingSnapshot = snapshots.TryGetValue(key, out var snapshot) && snapshot != null;
```
Replace with:
```csharp
            var key = new SnapshotKey(selection.GameType, selection.Variant);
            var hasMatchingSnapshot = snapshotElapsed.ContainsKey(key);
```

- [ ] **Edit F — LobbyComponent: `ApplySnapshots` takes an elapsed map.** Find:
```csharp
        public void ApplySnapshots(IReadOnlyDictionary<SnapshotKey, GameSnapshot> snapshots)
        {
            if (tiles == null)
                return;

            foreach (var tile in tiles)
            {
                if (tile == null || tile.IsComingSoon)
                    continue;

                if (!IsPlayable(tile))
                {
                    tile.HideContinueBadge();
                    continue;
                }

                var key = new SnapshotKey(tile.GameType, tile.VariantId);
                if (snapshots != null
                    && snapshots.TryGetValue(key, out var snapshot)
                    && snapshot != null)
                {
                    tile.ShowContinueBadge(snapshot.Stats?.ElapsedSeconds ?? 0f);
                }
                else
                {
                    tile.HideContinueBadge();
                }
            }
        }
```
Replace with:
```csharp
        public void ApplySnapshots(IReadOnlyDictionary<SnapshotKey, float> elapsedByKey)
        {
            if (tiles == null)
                return;

            foreach (var tile in tiles)
            {
                if (tile == null || tile.IsComingSoon)
                    continue;

                if (!IsPlayable(tile))
                {
                    tile.HideContinueBadge();
                    continue;
                }

                var key = new SnapshotKey(tile.GameType, tile.VariantId);
                if (elapsedByKey != null && elapsedByKey.TryGetValue(key, out var elapsed))
                    tile.ShowContinueBadge(elapsed);
                else
                    tile.HideContinueBadge();
            }
        }
```

- [ ] **Step G — compile + EditMode.** Recompile dance, `uloop compile` → 0/0 (if an unused-using warning appears for `Model.Game` in `LobbyComponent`, leave it — `GameType` from that namespace is still used; do NOT remove the using). `uloop run-tests --test-mode EditMode` → **417 passed**. If it won't compile, STOP and report BLOCKED.

- [ ] **Step H — play-mode verification (whole flow).** (The coordinator runs this; a subagent may stop after Step G and report DONE for review.) Deal Pyramid fresh → make 2-3 moves + a recycle → leave to lobby → confirm the Pyramid tile shows a Continue badge AND `BoardSnapshotRepository.LoadAsync(key)` returns non-null with the expected elapsed/score → tap the tile → board restores (cell count, stock count, waste, recycle count, score, move count match the pre-exit state) → win or get stuck → return to lobby → no badge AND the snapshot file is gone.

- [ ] **Step I — commit**
```bash
git add Assets/Scripts/Scene/Lobby/LobbyPresenter.cs Assets/Scripts/Scene/Lobby/LobbyComponent.cs
git commit -m "feat(lobby): board-aware continue scan (elapsed map) + Pyramid resume badge

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage:**
- Repo (`IBoardSnapshotRepository`/`LocalBoardSnapshotRepository`) → Task 1. ✓
- Service (`IBoardSnapshotService`/`BoardSnapshotService`, debounce/IO-lock/dispose-save) → Task 2. ✓
- DI (App singleton + BoardScene scoped) → Task 3. ✓
- Presenter load-on-`IsContinue`+`Seed==null`, restore, auto-save, flush on pause/app-pause, clear on win/stuck/new-game(fresh branch), dispose stop → Task 4 (Edits F/H/I/G/J). ✓
- Lobby parity (elapsed map, board repo branch, badge, Continue routing) → Task 5. ✓
- Clear on win AND stuck AND new-game → Edits H, I, and F's `else` branch (New Game / Restart re-enter without continue → fresh branch clears). ✓
- Corrupt/missing save → fresh deal (Edit F: `snapshot == null` → else branch); Restore throws atomically on mismatch — note: a thrown Restore would propagate out of `InitializeGameAsync` (a `UniTaskVoid`), logged by UniTask's unhandled-exception handler; the board would be left un-rendered. Mitigation is acceptable for v1 because `Restore` only throws on a layout/cell-count mismatch, which cannot occur for a same-version Pyramid save (the only board game). If multi-version saves arrive later, wrap the load/restore in try/catch → fresh deal. Documented here intentionally; not a placeholder.
- Testing: converter already covered by `BoardSnapshotTests`; repo/service/presenter/lobby play-verified (Task 5 Step H). ✓

**Placeholder scan:** none — full code for new files, exact find/replace for edits, exact commands.

**Type consistency:** `SnapshotKey`, `BoardSnapshot`, `IBoardSnapshotRepository.{Load,Save,Delete}Async`, `IBoardSnapshotService.{LoadSnapshotAsync,StartAutoSave,StopAutoSave,FlushAsync,FlushAndStopAsync,ClearSnapshotAsync}`, `BoardSnapshotConverter.ToSnapshot(gameType, variant, seed, …)` / `ToBoardState` / `ToHistory` / `ToSessionStats`, `BoardGameService.Restore(layout, rule, seed, state, history, maxRecycles)`, `SessionStats.Restore(IScoreRule, SessionStats)`, `currentSnapshotKey`, `snapshotElapsed`, `ApplySnapshots(IReadOnlyDictionary<SnapshotKey, float>)` — all consistent across tasks and matched to the verified existing signatures.
