# Board Snapshot / Resume — Design Spec

**Date:** 2026-06-09
**Status:** Approved (design)
**Scope:** Auto-save and resume in-progress board (Pyramid) games, at full parity with the card-game persistence (continue badge in the lobby + restore on re-entry).

---

## Problem

Card games auto-save and resume: leave mid-game, the lobby tile shows a "Continue" badge, tapping it restores the exact position. Board games (Pyramid) do not — leaving loses the game.

The board half of the persistence stack already exists and is unit-tested: `BoardSnapshot` (MemoryPackable), `BoardSnapshotConverter` (`BoardSnapshotTests`), and `BoardGameService.Restore(...)`. Missing: a repository, a save/load service, presenter wiring, DI registration, and the lobby integration that surfaces board saves.

## Goals

- A board game in progress auto-saves (debounced) and on pause / app-background / scene-exit.
- The Pyramid lobby tile shows a Continue badge (with elapsed time) when a save exists; tapping it resumes.
- Re-entry restores cells, stock, waste, recycle count, score, move count, elapsed time, and undo history.
- The save is cleared when the game finishes (won **or** stuck) and when a new game starts.
- A corrupt or missing save degrades to a fresh deal — never throws into play.

## Non-Goals (YAGNI)

- No generic `SnapshotService<T>` refactor of the working card stack — mirror it (see Approach). The card path is untouched.
- No board daily/challenge snapshot (Pyramid has no daily yet; `SnapshotKey.Mode` stays null for board).
- No cloud/remote sync — local file only, like cards.
- No "resume vs new game" prompt — tapping a tile with a save resumes (in-game New Game starts fresh), exactly like cards.

---

## Approach

**Mirror the card stack; do not genericize.** `GameSnapshotService` is ~200 lines of careful concurrency (1 s debounce, `SemaphoreSlim` IO-lock, coalescing save loop, dispose-time best-effort save). A parallel `BoardSnapshotService` keeps the battle-tested card path untouched (low risk) and matches the genuinely-parallel `BoardSnapshot`/`GameSnapshot` types. A shared generic base is deferred until a third snapshot type exists. The duplication is an accepted, bounded trade.

---

## Design

### 1. Gateway — `Assets/Scripts/Gateway/Snapshot/`

**`IBoardSnapshotRepository`** (parallels `IGameSnapshotRepository`):
```csharp
UniTask<BoardSnapshot> LoadAsync(SnapshotKey key);
UniTask SaveAsync(SnapshotKey key, BoardSnapshot snapshot);
UniTask DeleteAsync(SnapshotKey key);
```

**`LocalBoardSnapshotRepository`** — MemoryPack file at `Application.persistentDataPath/snapshot_<key>.bin`, same scheme/threadpool/WebGL handling as `LocalGameSnapshotRepository`. No filename collision with card saves: `SnapshotKey` embeds `GameType`, so `snapshot_Pyramid_Draw1.bin` ≠ `snapshot_Klondike_Draw1.bin`. Reuses the existing `SnapshotKey` struct unchanged.

### 2. Service — `Assets/Scripts/Service/SnapshotService/`

**`IBoardSnapshotService`** (parallels `IGameSnapshotService`):
```csharp
UniTask<BoardSnapshot> LoadSnapshotAsync(SnapshotKey key);
void StartAutoSave(SnapshotKey key, int seed, IBoardGameService game, ISessionStatsService stats);
void StopAutoSave();
UniTask FlushAsync();
UniTask FlushAndStopAsync();
UniTask ClearSnapshotAsync(SnapshotKey key);
```

**`BoardSnapshotService`** — structural twin of `GameSnapshotService`:
- Debounced 1 s auto-save subscribed to `IBoardGameService.OnBoardStateChanged`.
- `SemaphoreSlim` IO-lock + coalescing `RunSaveLoop` (identical concurrency shape).
- Dispose-time best-effort save (so quitting mid-game persists), guarded by `ioLock.Wait(0)`.
- Serializes via `BoardSnapshotConverter.ToSnapshot(key.GameType, key.VariantId, seed, game.CurrentState, game.UndoHistory, stats.Current)`.

### 3. BoardPresenter wiring (mirrors `IngamePresenter`)

Inject `IBoardSnapshotService`. Build `currentSnapshotKey = new SnapshotKey(query.GameType, variant)` (no Mode).

- **Load on launch** (in the game-init path, replacing the unconditional `Initialize`):
  - `canLoad = query.IsContinue && query.Seed == null;`
  - if `canLoad`, `snapshot = await SnapshotService.LoadSnapshotAsync(key)`.
  - if `snapshot != null`:
    - build `layout`/`matchRule`/`maxRecycles` for the game type (same as a fresh deal);
    - `BoardGameService.Restore(layout, matchRule, snapshot.Seed, BoardSnapshotConverter.ToBoardState(snapshot.CurrentState), BoardSnapshotConverter.ToHistory(snapshot.UndoHistory), maxRecycles)`;
    - `SessionStats.Restore(zeroScoreRule, BoardSnapshotConverter.ToSessionStats(snapshot.Stats))`.
  - else: `BoardGameService.Initialize(layout, matchRule, query.Seed, maxRecycles)` (fresh) and `ClearSnapshotAsync(key)`.
  - then `StartAutoSave(key, BoardGameService.CurrentSeed.Value, BoardGameService, SessionStats)`.
- **Flush:** `OnPauseObservable` and `OnApplicationPauseObservable` (when backgrounded) → `FlushAsync().Forget()`.
- **Clear on finish:** `HandleWin()` → `ClearSnapshotAsync(key).Forget()`; the stuck path (where `Game.Stuck` plays / `ShowStuckPanel`) → `ClearSnapshotAsync(key).Forget()`.
- **Clear on new game:** `StartNewGameAsync` clears the current key before navigating (the fresh deal then auto-saves anew).
- **Dispose:** `BoardSnapshotService.Dispose` does the final best-effort save; the presenter calls `StopAutoSave` on its own disposal. VContainer disposes the scoped service after the presenter but before the game/stats services, so their state is still valid at save time (same ordering the card stack relies on).

The initial fresh deal also writes a snapshot on its first debounced change, so a game becomes resumable as soon as the first move lands.

### 4. Lobby parity — make the continue scan snapshot-type-agnostic

The lobby currently scans only `GameSnapshot` via `IGameSnapshotRepository`, so board tiles never badge or route `Continue`. The only datum the badge needs is **elapsed seconds**, so generalize:

- `LobbyPresenter`: replace `Dictionary<SnapshotKey, GameSnapshot> snapshots` with `Dictionary<SnapshotKey, float> snapshotElapsed` (presence = a save exists; value = elapsed seconds). Inject `IBoardSnapshotRepository` alongside the existing `IGameSnapshotRepository`.
- `LoadOneAsync(key)`: branch on `key.GameType.IsBoardMode()` → load `BoardSnapshot` via the board repo, else `GameSnapshot` via the card repo; return `(key, elapsed)` or none. Both repos already swallow IO errors and return null.
- `LobbyComponent.ApplySnapshots(IReadOnlyDictionary<SnapshotKey, float>)`: badge a tile when its key is present (`ShowContinueBadge(elapsed)`), else `HideContinueBadge()`. The Component no longer references any concrete snapshot type.
- `OnTileSelected`: `hasMatchingSnapshot = snapshotElapsed.ContainsKey(key)` — the existing `Continue=true` routing then works for board tiles unchanged.
- The separate daily-tile path (`dailySnapshot`, card/Klondike-only) is left untouched.

### 5. DI

- `App/AppLifetimeScope`: `builder.Register<LocalBoardSnapshotRepository>(Lifetime.Singleton).As<IBoardSnapshotRepository>();` (next to the card repo).
- `Scene/Board/BoardScene`: `builder.Register<BoardSnapshotService>(Lifetime.Scoped).As<IBoardSnapshotService>();` (next to the other board-scene services).

### 6. Error handling

All file IO is try/caught and logged inside the repo and service (mirror of the card stack); failures return null. A null/corrupt snapshot falls through to a fresh `Initialize`. `BoardGameService.Restore` already validates atomically — it throws on a layout/cell-count mismatch *before* mutating any field, so a structurally-incompatible save leaves the service untouched; the presenter treats a thrown restore the same as "no snapshot" and deals fresh. No defensive fallback layering beyond these existing, bounded try/catch sites (per the root-cause principle).

---

## Data Flow

```
Play a move → OnBoardStateChanged → (1s debounce) → BoardSnapshotService saves BoardSnapshot to disk
Pause / background / scene-exit → FlushAsync (immediate save)
Quit mid-game → BoardSnapshotService.Dispose → best-effort final save

Lobby open → scan tile keys → board keys via IBoardSnapshotRepository → elapsed map → Continue badge
Tap Pyramid tile (save exists) → query[Continue]=true → BoardScene
BoardScene init (IsContinue && Seed==null) → LoadSnapshotAsync → Restore(state, history, seed, recycleCount) + SessionStats.Restore

Win / Stuck → ClearSnapshotAsync (badge gone next lobby visit)
New Game → ClearSnapshotAsync → fresh deal → re-saves on first move
```

## Edge Cases

- **No save / corrupt save:** load returns null → fresh deal; the bad file is overwritten on the next save or cleared.
- **`query.Seed` present (Play-with-code / explicit seed):** `canLoad` is false → always a fresh deterministic deal, never a resume (mirrors cards).
- **Recycle count:** carried in `BoardState`/`BoardStateDto`/`BoardSnapshot` already (round-trip tested), so recycle passes survive a resume.
- **Win/stuck arrives before a debounce fires:** the clear runs regardless of any pending save; `ClearSnapshotAsync` stops auto-save and deletes under the IO-lock, so a late save cannot resurrect a finished game.
- **Filename coexistence:** board and card saves share the `snapshot_` prefix but differ by `GameType` in the key, so they never overwrite each other.

## Testing

- `BoardSnapshotConverter` round-trip — already covered by `BoardSnapshotTests`; extend only if a field (e.g. `RecycleCount`) is found uncovered.
- **New:** `LocalBoardSnapshotRepository` save → load → delete round-trip (EditMode, real `Application.persistentDataPath`, a test-only key; assert equality of a representative `BoardSnapshot` and that delete removes the file).
- **Play-verified** (service + presenter + lobby IO/timing): deal Pyramid → make moves + a recycle → return to lobby (Pyramid tile shows Continue badge + elapsed) → re-enter → board matches (cells/stock/waste/recycle/score/moves/undo). Then win (or get stuck) → return to lobby → no badge; re-enter → fresh deal.

## Effort

4 new files (2 gateway, 2 service), BoardPresenter wiring (~40 lines), lobby refactor (3 methods + 1 injected field), 2 DI lines, 1 repo round-trip test. One cohesive feature, sequential tasks.
