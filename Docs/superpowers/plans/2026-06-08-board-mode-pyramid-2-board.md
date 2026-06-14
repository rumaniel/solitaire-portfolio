# Board Mode — Plan 2-Board: Playable Pyramid (render + scene + entry) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make **Pyramid playable end-to-end** — deal → tap-to-match → win/stuck → undo, with a live score/moves/time HUD — by adding a tap-based board renderer (`UIBoardController`), a `BoardPresenter`/`BoardScene` that reuse the shared `IngameShellView`, and a Lobby tile that routes layered-board games to the new scene.

**Architecture:** Parallel board stack (spec §3). The board scene is a **prefab variant of `IngameShell.prefab`** (extracted here, deferred from Plan 2b Task 5) with `UIBoardController` in the `PlayArea` slot. `BoardPresenter` drives `IBoardGameService` (already built in Plans 1/2a) and reuses the exact same shell UI as the card game. Pure-logic additions (selection feedback, board score entry point, board-mode classifier) are TDD'd first; Unity code is compile-verified; scene/prefab/asset wiring is manual-play-gated.

**Tech Stack:** Unity 6, VContainer (DI, `RegisterComponent`/`RegisterEntryPoint`), R3 (`Subject`/`Observable`/`.AddTo`), UniTask, NUnit (EditMode). Editor automation via `uloop execute-dynamic-code` (mirrors the skin/2b approach).

**Branch:** `feature/board-mode-pyramid` (continue; PR #103 draft).

**Scope this slice (Standard parity minus persistence):** render, tap-match, win, stuck, undo, score/moves/time HUD, pause/new-game/restart/lobby/stats, skinning. **Deferred to Plan 2c:** snapshot save/resume + auto-save, lifetime-stats recording on win, **Hint** (board hint service), removal fade polish, daily/achievements, score-tuning ScriptableObject. The board variant **hides the Hint / Play-with-code / Daily** shell buttons until 2c (no dead buttons).

---

## Notes for the implementer

- **Compile/test loop (uloop):** after C# edits, force a refresh + recompile, then poll:
  `uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"`
  then poll `uloop compile` until a real `ErrorCount`. If it hangs on "Domain Reload"/"already in progress", run `uloop fix` (clears stale `*.lock`) and retry.
- **execute-dynamic-code sandbox gotchas:** `using System;` is injected, so `Object` is ambiguous → use `UnityEngine.Object`; the project has a `Component` namespace → use `UnityEngine.Component`. For inactive objects use `FindObjectOfType<T>(true)` / `Resources.FindObjectsOfTypeAll<T>()` filtered by `.scene` + `hideFlags == HideFlags.None`.
- **Regression gate:** `uloop run-tests --test-mode EditMode`. Baseline at the start of this plan is **398 total / 392 passed / 0 failed / 6 skipped**. Phase A adds new tests (count goes up); the count must never go *down* and must stay 0-failed.
- **Manual gate (Phase C):** scene/prefab wiring can compile-pass yet mis-wire a panel/anchor — only play confirms it. After Phase C, play Pyramid AND re-verify Klondike/Easthaven in the (refactored) Ingame scene.
- **Git:** commit per task, only the listed files, trailer `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`. Never `git add -A` (avoid `ProjectSettings.asset` drift) — except `EditorBuildSettings.asset` in Task 11, which is the intended change.
- **Confirmed APIs (do not re-derive):** `IBoardGameService.Initialize(BoardLayout, IBoardMatchRule, int? seed)`, `SelectCell/SelectWasteTop/DrawFromStock/ClearSelection`, `IsWon/HasAnyMove(BoardState)`, `CanUndo/Undo`, `Restore(...)`. `BoardState`: `CellCount`, `HasCard(CellId)`, `CardAt(CellId)`, `WasteTop`, `Stock`, `Waste`, `OccupiedCells()`. `BoardRules.FreeCells(layout,state)` / `IsFree`. `PyramidLayoutFactory.Create(variant)` (28 cells, apex=CellId 0, row-major). `PyramidMatchRule`, `PyramidScoreRule(perCard=5, boardClearedBonus=100)` with `ScoreForRemoval(int)` + `BoardClearedBonus`. `UICard`: `SetCard`, `SetSpriteSet`, `OpenImmediate`, `Close`, `SetVisible(bool)`, `SetHighlight(bool)`, `Enable`/`Disable`, `rectTransform`, `OnPointerClickEvent` (`UnityEvent<UICard>`). `ISessionStatsService`: `Initialize(IScoreRule)`, `RecordMove(ScoredMoveInfo)`, `Tick`, `Pause/Resume`, `MarkWon`, `Current`, `OnStatsChanged`. `IRouteService.NavigateAsync(path, query)` / `GoBackAsync()` / `CurrentQuery` / `OnSamePathNavigated`. `GameCode.Encode(GameType, seed)`. `IShuffleStrategy` impl = `FisherYatesShuffleStrategy` (public, parameterless).

---

## File Structure

**Phase A — pure logic (Model + Service, EditMode-testable):**
- Create `Assets/Scripts/Model/Board/SelectionSnapshot.cs` — immutable pending-selection view.
- Modify `Assets/Scripts/Service/BoardGameService/IBoardGameService.cs` + `BoardGameService.cs` — expose `OnSelectionChanged`/`CurrentSelection`.
- Create `Assets/Scripts/Service/StatsService/ZeroScoreRule.cs` — null-object `IScoreRule` for board sessions.
- Modify `Assets/Scripts/Service/StatsService/ISessionStatsService.cs` + `SessionStatsService.cs` — add `RecordScoreDelta(int)`.
- Create `Assets/Scripts/Model/Game/GameTypeExtensions.cs` — `IsBoardMode()`.
- Tests: `Assets/Tests/EditMode/SelectionSnapshotTests.cs`, additions to `BoardGameServiceTests.cs` + `SessionStatsServiceTests.cs`, `Assets/Tests/EditMode/GameTypeExtensionsTests.cs`.

**Phase B — Unity code (compile-verified):**
- Create `Assets/Scripts/Component/Board/UIBoardController.cs` (+ `.meta`) — namespace `Component.Board`.
- Create `Assets/Scripts/Scene/Board/BoardPresenter.cs` — namespace `Scene.Board`.
- Create `Assets/Scripts/Scene/Board/BoardScene.cs` — namespace `Scene.Board`.
- Modify `Assets/Scripts/Scene/Lobby/LobbyPresenter.cs` — route board games to `"BoardScene"`.

**Phase C — scene/prefab/assets (manual gate):**
- Create `Assets/Prefabs/InGame/IngameShell.prefab` (base shell + empty `PlayArea`), refactor `Ingame.unity` to instance it.
- Create `Assets/Prefabs/Board/PyramidBoard.prefab` (28 cell anchors + stock/waste + `UIBoardController`).
- Create `Assets/Scenes/BoardScene.unity` (prefab **variant** of `IngameShell.prefab`, `PlayArea` ← `PyramidBoard`).
- Create `Assets/ScriptableObjects/GameVariants/Pyramid.asset` (`GameVariant`).
- Modify Lobby scene (add Pyramid `GameTileView`) + `EditorBuildSettings.asset` (register `BoardScene`).

No new assembly: `Component.Board` → `Component.asmdef`, `Scene.Board` → `Scene.asmdef`, board model → `Model.asmdef` (all existing).

---

## Phase A — Pure logic (TDD)

### Task 1: `SelectionSnapshot` + expose pending selection from the board service

Tap-to-select needs visual feedback for the *first* tap, but `BoardState` carries no selection (it's a private accumulator in the service, per spec §5.1). Expose a read-only, immutable snapshot + an observable so the renderer can highlight the pending pick.

**Files:**
- Create: `Assets/Scripts/Model/Board/SelectionSnapshot.cs`
- Modify: `Assets/Scripts/Service/BoardGameService/IBoardGameService.cs`, `Assets/Scripts/Service/BoardGameService/BoardGameService.cs`
- Test: `Assets/Tests/EditMode/SelectionSnapshotTests.cs`, `Assets/Tests/EditMode/BoardGameServiceTests.cs`

- [ ] **Step 1: Write `SelectionSnapshotTests`**

```csharp
using Model.Board;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class SelectionSnapshotTests
    {
        [Test]
        public void Empty_HasNoCellsAndNoWaste()
        {
            Assert.AreEqual(0, SelectionSnapshot.Empty.Cells.Count);
            Assert.IsFalse(SelectionSnapshot.Empty.WasteSelected);
        }

        [Test]
        public void Contains_FindsSelectedCell()
        {
            var s = new SelectionSnapshot(new[] { new CellId(3), new CellId(7) }, wasteSelected: false);
            Assert.IsTrue(s.Contains(new CellId(3)));
            Assert.IsTrue(s.Contains(new CellId(7)));
            Assert.IsFalse(s.Contains(new CellId(4)));
        }

        [Test]
        public void Equals_IsValueBased()
        {
            var a = new SelectionSnapshot(new[] { new CellId(1) }, true);
            var b = new SelectionSnapshot(new[] { new CellId(1) }, true);
            var c = new SelectionSnapshot(new[] { new CellId(1) }, false);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void Ctor_DefensivelyCopies()
        {
            var src = new[] { new CellId(2) };
            var s = new SelectionSnapshot(src, false);
            src[0] = new CellId(9);
            Assert.IsTrue(s.Contains(new CellId(2)));
            Assert.IsFalse(s.Contains(new CellId(9)));
        }
    }
}
```

- [ ] **Step 2: Run → FAIL** (`SelectionSnapshot` undefined). `uloop run-tests --test-mode EditMode`.

- [ ] **Step 3: Create `SelectionSnapshot.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Model.Board
{
    /// <summary>Immutable view of the board's pending tap-selection: chosen cells plus whether the waste-top is chosen.</summary>
    public sealed class SelectionSnapshot : IEquatable<SelectionSnapshot>
    {
        public static readonly SelectionSnapshot Empty = new SelectionSnapshot(Array.Empty<CellId>(), false);

        private readonly CellId[] cells;
        public IReadOnlyList<CellId> Cells => cells;
        public bool WasteSelected { get; }

        public SelectionSnapshot(IReadOnlyList<CellId> cells, bool wasteSelected)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            this.cells = new CellId[cells.Count];
            for (int i = 0; i < cells.Count; i++) this.cells[i] = cells[i];
            WasteSelected = wasteSelected;
        }

        public bool Contains(CellId id)
        {
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].Equals(id)) return true;
            return false;
        }

        public bool Equals(SelectionSnapshot other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (WasteSelected != other.WasteSelected) return false;
            if (cells.Length != other.cells.Length) return false;
            for (int i = 0; i < cells.Length; i++)
                if (!cells[i].Equals(other.cells[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is SelectionSnapshot o && Equals(o);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var c in cells) hash.Add(c);
            hash.Add(WasteSelected);
            return hash.ToHashCode();
        }
    }
}
```

- [ ] **Step 4: Add `OnSelectionChanged`/`CurrentSelection` to `IBoardGameService`** (after `OnBoardStateChanged`):

```csharp
        /// <summary>Pending tap-selection (cells + waste-top) for the View to highlight. Emits Empty after a match/clear.</summary>
        Observable<SelectionSnapshot> OnSelectionChanged { get; }
        SelectionSnapshot CurrentSelection { get; }
```

(`IBoardGameService.cs` already has `using Model.Board;` and `using R3;`.)

- [ ] **Step 5: Implement in `BoardGameService`.** Add field + property + a publish helper, and publish after every selection mutation. Concretely:

Add near the other fields:
```csharp
        private readonly Subject<SelectionSnapshot> selectionSubject = new();
        public SelectionSnapshot CurrentSelection { get; private set; } = SelectionSnapshot.Empty;
        public Observable<SelectionSnapshot> OnSelectionChanged => selectionSubject;
```

Add the helper:
```csharp
        private void PublishSelection()
        {
            var cells = new List<CellId>(selection.Count);
            bool waste = false;
            foreach (var t in selection)
            {
                if (t.IsWaste) waste = true;
                else cells.Add(t.Cell);
            }
            CurrentSelection = new SelectionSnapshot(cells, waste);
            selectionSubject.OnNext(CurrentSelection);
        }
```

Call `PublishSelection();` as the **last** line of: `Initialize` (after `stateSubject.OnNext`), `SelectCell` (after `HandleSelect`), `SelectWasteTop` (after `HandleSelect`), `DrawFromStock` (after `stateSubject.OnNext`), `ClearSelection`, `Undo` (after `stateSubject.OnNext`), and `Restore` (after `stateSubject.OnNext`). Board state is always published **before** the selection, so a subscriber that re-renders on state-change then re-highlights on selection-change sees a consistent order. Update `SelectCell`/`SelectWasteTop` like:
```csharp
        public void SelectCell(CellId id)
        {
            if (!BoardRules.IsFree(Layout, CurrentState, id)) return;
            HandleSelect(SelectedTarget.OfCell(id), CurrentState.CardAt(id));
            PublishSelection();
        }
```
(The early `return` on a non-free cell intentionally skips the publish — nothing changed.) Dispose it in `Dispose`:
```csharp
        public void Dispose()
        {
            stateSubject.Dispose();
            selectionSubject.Dispose();
        }
```

- [ ] **Step 6: Add service-level selection tests to `BoardGameServiceTests`** (append). Reuse the fixture's existing deterministic helpers `FixedShuffle`, `FlatLayout(int n)`, and `Card(Rank r)` (the suite uses factory helpers, **not** a `[SetUp]`/`service` field — match that style). The fixture already has `using R3;` and `using Model.Board;`:

```csharp
        [Test]
        public void SelectCell_FreeCard_PublishesPendingSelection()
        {
            // cells: [9, 4] (FlatLayout = no covers → both free)
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);

            SelectionSnapshot last = null;
            using var _ = svc.OnSelectionChanged.Subscribe(s => last = s);

            svc.SelectCell(new CellId(0)); // 9 → incomplete, stays selected

            Assert.IsNotNull(last);
            Assert.IsTrue(last.Contains(new CellId(0)));
            Assert.IsFalse(last.WasteSelected);
        }

        [Test]
        public void Match_ClearsSelection()
        {
            // cells: [9, 4] → 9+4 = 13 removes both; selection must publish Empty
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);

            SelectionSnapshot last = null;
            using var _ = svc.OnSelectionChanged.Subscribe(s => last = s);

            svc.SelectCell(new CellId(0)); // 9 → incomplete
            svc.SelectCell(new CellId(1)); // 4 → match → selection cleared

            Assert.AreEqual(SelectionSnapshot.Empty, last);
        }
```

- [ ] **Step 7: Refresh + `uloop compile` → ErrorCount 0. Run EditMode tests → all new tests pass, 0 failed, count ≥ baseline.**

- [ ] **Step 8: Commit.**
```bash
git add Assets/Scripts/Model/Board/SelectionSnapshot.cs Assets/Scripts/Model/Board/SelectionSnapshot.cs.meta Assets/Scripts/Service/BoardGameService/IBoardGameService.cs Assets/Scripts/Service/BoardGameService/BoardGameService.cs Assets/Tests/EditMode/SelectionSnapshotTests.cs Assets/Tests/EditMode/SelectionSnapshotTests.cs.meta Assets/Tests/EditMode/BoardGameServiceTests.cs
git commit -m "feat(board): expose pending SelectionSnapshot from board service for tap feedback"
```

---

### Task 2: Board score entry point on `SessionStatsService` + `ZeroScoreRule`

`SessionStatsService.RecordMove` computes score from `PileType` source/target — meaningless for a board game (always `PileType.None` → 0). Per spec §5.2, add an explicit-delta entry point and bypass the pile inference. `Initialize` requires an `IScoreRule`; board sessions never hit the pile path, so pass a zero null-object (avoids a latent null deref while honoring the interface).

**Files:**
- Create: `Assets/Scripts/Service/StatsService/ZeroScoreRule.cs`
- Modify: `Assets/Scripts/Service/StatsService/ISessionStatsService.cs`, `Assets/Scripts/Service/StatsService/SessionStatsService.cs`
- Test: `Assets/Tests/EditMode/SessionStatsServiceTests.cs`

- [ ] **Step 1: Write the failing test** (append to `SessionStatsServiceTests`):

```csharp
        [Test]
        public void RecordScoreDelta_AddsScoreAndCountsMove()
        {
            var stats = new SessionStatsService();
            stats.Initialize(new ZeroScoreRule());

            stats.RecordScoreDelta(10);
            stats.RecordScoreDelta(5);

            Assert.AreEqual(15, stats.Current.Score);
            Assert.AreEqual(2, stats.Current.MoveCount);
        }

        [Test]
        public void RecordScoreDelta_ThenUndo_SubtractsLastDeltaAndCountsMove()
        {
            var stats = new SessionStatsService();
            stats.Initialize(new ZeroScoreRule());

            stats.RecordScoreDelta(10);
            stats.RecordScoreDelta(7);
            stats.RecordMove(new ScoredMoveInfo(MoveType.Undo));

            Assert.AreEqual(10, stats.Current.Score);   // last delta (7) removed
            Assert.AreEqual(3, stats.Current.MoveCount); // 2 deltas + 1 undo
            Assert.IsTrue(stats.Current.UndoUsed);
        }

        [Test]
        public void RecordScoreDelta_NeverNegative()
        {
            var stats = new SessionStatsService();
            stats.Initialize(new ZeroScoreRule());
            stats.RecordScoreDelta(3);
            stats.RecordScoreDelta(-100);
            Assert.AreEqual(0, stats.Current.Score);
        }
```

(`SessionStatsServiceTests` already `using Model.Stats;` / `using Service.StatsService;`. Add `using Service.StatsService;` if missing for `ZeroScoreRule`.)

- [ ] **Step 2: Run → FAIL** (`RecordScoreDelta`/`ZeroScoreRule` undefined).

- [ ] **Step 3: Create `ZeroScoreRule.cs`**

```csharp
using Model.Stats;

namespace Service.StatsService
{
    /// <summary>
    /// Null-object <see cref="IScoreRule"/> for board games (Pyramid/TriPeaks), which score via
    /// <see cref="ISessionStatsService.RecordScoreDelta"/> and never use the pile-type scoring path.
    /// </summary>
    public sealed class ZeroScoreRule : IScoreRule
    {
        public int WasteToTableau => 0;
        public int WasteToFoundation => 0;
        public int TableauToFoundation => 0;
        public int FoundationToTableau => 0;
        public int TableauReveal => 0;
        public int StockRecycle => 0;
    }
}
```

- [ ] **Step 4: Add to `ISessionStatsService`** (after `RecordMove`):

```csharp
        /// <summary>Adds an explicit score delta and counts it as one move. For board games that compute their own
        /// per-match score (bypasses the pile-type inference in <see cref="RecordMove"/>). A later
        /// <see cref="RecordMove"/> with <see cref="MoveType.Undo"/> subtracts the most recent delta.</summary>
        void RecordScoreDelta(int delta);
```

- [ ] **Step 5: Implement in `SessionStatsService`** (mirrors the non-undo branch of `RecordMove`, but with an explicit delta and no `scoreRule` use):

```csharp
        public void RecordScoreDelta(int delta)
        {
            if (frozen) return;

            Current.MoveCount++;
            lastScoreDelta = delta;
            Current.Score = Math.Max(0, Current.Score + delta);
            statsSubject.OnNext(Current.Snapshot());
        }
```

- [ ] **Step 6: Refresh + `uloop compile` → 0 errors. Run tests → new ones pass, 0 failed.**

- [ ] **Step 7: Commit.**
```bash
git add Assets/Scripts/Service/StatsService/ZeroScoreRule.cs Assets/Scripts/Service/StatsService/ZeroScoreRule.cs.meta Assets/Scripts/Service/StatsService/ISessionStatsService.cs Assets/Scripts/Service/StatsService/SessionStatsService.cs Assets/Tests/EditMode/SessionStatsServiceTests.cs
git commit -m "feat(stats): add RecordScoreDelta board entry point + ZeroScoreRule null-object"
```

---

### Task 3: `GameType.IsBoardMode()` classifier

The Lobby must route Pyramid/TriPeaks to `BoardScene` and Klondike/Easthaven/Spider to `Ingame`. Centralize the classification.

**Files:**
- Create: `Assets/Scripts/Model/Game/GameTypeExtensions.cs`
- Test: `Assets/Tests/EditMode/GameTypeExtensionsTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using Model.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class GameTypeExtensionsTests
    {
        [Test]
        public void IsBoardMode_TrueForLayeredBoardGames()
        {
            Assert.IsTrue(GameType.Pyramid.IsBoardMode());
            Assert.IsTrue(GameType.TriPeaks.IsBoardMode());
        }

        [Test]
        public void IsBoardMode_FalseForCardGames()
        {
            Assert.IsFalse(GameType.Klondike.IsBoardMode());
            Assert.IsFalse(GameType.Easthaven.IsBoardMode());
            Assert.IsFalse(GameType.Spider.IsBoardMode());
            Assert.IsFalse(GameType.None.IsBoardMode());
        }
    }
}
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Create `GameTypeExtensions.cs`**

```csharp
namespace Model.Game
{
    public static class GameTypeExtensions
    {
        /// <summary>True for layered-board games (Pyramid, TriPeaks) that run in BoardScene rather than the card Ingame scene.</summary>
        public static bool IsBoardMode(this GameType gameType)
            => gameType == GameType.Pyramid || gameType == GameType.TriPeaks;
    }
}
```

- [ ] **Step 4: Refresh + `uloop compile` → 0. Tests pass.**

- [ ] **Step 5: Commit.**
```bash
git add Assets/Scripts/Model/Game/GameTypeExtensions.cs Assets/Scripts/Model/Game/GameTypeExtensions.cs.meta Assets/Tests/EditMode/GameTypeExtensionsTests.cs Assets/Tests/EditMode/GameTypeExtensionsTests.cs.meta
git commit -m "feat(model): add GameType.IsBoardMode() classifier"
```

---

## Phase B — Unity code (compile-verified; no EditMode tests for MonoBehaviours/presenter)

### Task 4: `UIBoardController` — fixed-anchor tap renderer

Reuses the existing `UICard` prefab (skin/tap/highlight already work). Places one card per `CellId` at `cellAnchors[CellId.Value]`, renders stock/waste, emits taps, reflects free (interactable) + selected (highlight). No drag.

**Files:** Create `Assets/Scripts/Component/Board/UIBoardController.cs`.

- [ ] **Step 1: Create the file**

```csharp
using System.Collections.Generic;
using Component.Card;
using Data.Card;
using Model.Board;
using Model.Card;
using R3;
using UnityEngine;

namespace Component.Board
{
    /// <summary>
    /// Tap-based, fixed-anchor renderer for cover-based board games (Pyramid now). One <see cref="UICard"/>
    /// per board <see cref="CellId"/> at <c>cellAnchors[CellId.Value]</c>; plus a stock (face-down) and a
    /// waste (face-up top). Emits taps as cell / stock / waste events; the presenter owns all game logic.
    /// </summary>
    public class UIBoardController : MonoBehaviour
    {
        [Header("Anchors (element index = CellId.Value; order apex→base for correct overlap)")]
        [SerializeField] private RectTransform[] cellAnchors;
        [SerializeField] private RectTransform stockAnchor;
        [SerializeField] private RectTransform wasteAnchor;

        [Header("Prefab")]
        [SerializeField] private UICard cardPrefab;

        private CardSpriteSet currentSpriteSet;

        private readonly Dictionary<int, UICard> cardByCell = new();   // CellId.Value -> view
        private readonly Dictionary<UICard, CellId> cellByCard = new();
        private UICard stockCard;
        private UICard wasteCard;

        private readonly Subject<CellId> cellTapped = new();
        private readonly Subject<Unit> stockTapped = new();
        private readonly Subject<Unit> wasteTapped = new();

        public Observable<CellId> OnCellTapped => cellTapped;
        public Observable<Unit> OnStockTapped => stockTapped;
        public Observable<Unit> OnWasteTapped => wasteTapped;

        public void ApplySpriteSet(CardSpriteSet spriteSet)
        {
            currentSpriteSet = spriteSet;
            if (spriteSet == null) return;
            foreach (var c in cardByCell.Values)
                if (c != null) c.SetSpriteSet(spriteSet);
            if (stockCard != null) stockCard.SetSpriteSet(spriteSet);
            if (wasteCard != null) wasteCard.SetSpriteSet(spriteSet);
        }

        /// <summary>Reconciles spawned views with the state: spawn newly-present cells, despawn removed ones, refresh stock/waste.</summary>
        public void RenderBoard(BoardState state)
        {
            for (int value = 0; value < state.CellCount; value++)
            {
                var id = new CellId(value);
                bool has = state.HasCard(id);
                bool spawned = cardByCell.ContainsKey(value);
                if (has && !spawned) SpawnCell(id, state.CardAt(id));
                else if (!has && spawned) DespawnCell(value);
            }
            RenderStock(state);
            RenderWaste(state);
        }

        /// <summary>Sets which cells are interactable (free). Locked cells cannot be tapped.</summary>
        public void SetFreeCells(ICollection<CellId> freeCells)
        {
            foreach (var kv in cardByCell)
            {
                if (kv.Value == null) continue;
                if (freeCells.Contains(new CellId(kv.Key))) kv.Value.Enable();
                else kv.Value.Disable();
            }
        }

        /// <summary>Highlights the pending tap-selection. Sole driver of the per-card highlight visual.</summary>
        public void SetSelection(SelectionSnapshot selection)
        {
            foreach (var kv in cardByCell)
            {
                if (kv.Value == null) continue;
                kv.Value.SetHighlight(selection != null && selection.Contains(new CellId(kv.Key)));
            }
            if (wasteCard != null)
                wasteCard.SetHighlight(selection != null && selection.WasteSelected);
        }

        public void DespawnAll()
        {
            foreach (var kv in cardByCell)
            {
                if (kv.Value == null) continue;
                kv.Value.OnPointerClickEvent.RemoveListener(OnCellCardClicked);
                Destroy(kv.Value.gameObject);
            }
            cardByCell.Clear();
            cellByCard.Clear();
            if (stockCard != null) { stockCard.OnPointerClickEvent.RemoveListener(OnStockClicked); Destroy(stockCard.gameObject); stockCard = null; }
            if (wasteCard != null) { wasteCard.OnPointerClickEvent.RemoveListener(OnWasteClicked); Destroy(wasteCard.gameObject); wasteCard = null; }
        }

        private void SpawnCell(CellId id, PlayingCard card)
        {
            var anchor = AnchorFor(id);
            var view = Instantiate(cardPrefab, anchor != null ? anchor : transform);
            view.rectTransform.anchoredPosition = Vector2.zero;
            if (currentSpriteSet != null) view.SetSpriteSet(currentSpriteSet);
            view.SetCard(card);
            view.OpenImmediate();                 // board cards are dealt face-up
            view.OnPointerClickEvent.AddListener(OnCellCardClicked);
            cardByCell[id.Value] = view;
            cellByCard[view] = id;
        }

        private void DespawnCell(int value)
        {
            if (!cardByCell.TryGetValue(value, out var view)) return;
            cardByCell.Remove(value);
            if (view == null) return;
            cellByCard.Remove(view);
            view.OnPointerClickEvent.RemoveListener(OnCellCardClicked);
            Destroy(view.gameObject);
        }

        private void OnCellCardClicked(UICard card)
        {
            if (card != null && cellByCard.TryGetValue(card, out var id)) cellTapped.OnNext(id);
        }

        private void RenderStock(BoardState state)
        {
            bool hasStock = state.Stock.Count > 0;
            if (hasStock && stockCard == null && stockAnchor != null)
            {
                stockCard = Instantiate(cardPrefab, stockAnchor);
                stockCard.rectTransform.anchoredPosition = Vector2.zero;
                if (currentSpriteSet != null) stockCard.SetSpriteSet(currentSpriteSet);
                stockCard.SetCard(new PlayingCard(Rank.Ace, Suit.Spade)); // face hidden — any card; only the back shows
                stockCard.Close();
                stockCard.OnPointerClickEvent.AddListener(OnStockClicked);
            }
            if (stockCard != null) stockCard.SetVisible(hasStock);
        }

        private void OnStockClicked(UICard _) => stockTapped.OnNext(Unit.Default);

        private void RenderWaste(BoardState state)
        {
            var top = state.WasteTop;
            if (top == null)
            {
                if (wasteCard != null) wasteCard.SetVisible(false);
                return;
            }
            if (wasteCard == null && wasteAnchor != null)
            {
                wasteCard = Instantiate(cardPrefab, wasteAnchor);
                wasteCard.rectTransform.anchoredPosition = Vector2.zero;
                if (currentSpriteSet != null) wasteCard.SetSpriteSet(currentSpriteSet);
                wasteCard.OnPointerClickEvent.AddListener(OnWasteClicked);
            }
            if (wasteCard != null)
            {
                wasteCard.SetCard(top);
                wasteCard.OpenImmediate();
                wasteCard.SetVisible(true);
            }
        }

        private void OnWasteClicked(UICard _) => wasteTapped.OnNext(Unit.Default);

        private RectTransform AnchorFor(CellId id)
            => (cellAnchors != null && id.Value >= 0 && id.Value < cellAnchors.Length) ? cellAnchors[id.Value] : null;

        private void OnDestroy()
        {
            cellTapped.Dispose();
            stockTapped.Dispose();
            wasteTapped.Dispose();
        }
    }
}
```

- [ ] **Step 2: Refresh + `uloop compile` → ErrorCount 0** (verify `Component.Board` resolves `Component.Card.UICard`, `Data.Card.CardSpriteSet`, `Model.Board.*`, `Model.Card.*` — all already referenced by `Component.asmdef`).

- [ ] **Step 3: Commit.**
```bash
git add Assets/Scripts/Component/Board/UIBoardController.cs Assets/Scripts/Component/Board/UIBoardController.cs.meta
git commit -m "feat(board): add UIBoardController fixed-anchor tap renderer"
```

---

### Task 5: `BoardPresenter`

Drives `IBoardGameService` + the shared `IngameShellView` + `UIBoardController`. Scoring is **state-diff driven** (the service decides matches asynchronously via its selection accumulator): on each board change, `removed = prevTotalCards − newTotalCards`; `removed > 0` → a match (`ScoreForRemoval` + clear bonus on win), `removed == 0` → a stock draw (counts as a move, 0 score), `removed < 0` → an undo/restore (its own handler already recorded it — skip). Subscribing to `OnBoardStateChanged` happens **after** `Initialize` (a plain `Subject` doesn't replay), so the initial deal is rendered manually and isn't mistaken for a move.

**Files:** Create `Assets/Scripts/Scene/Board/BoardPresenter.cs`.

- [ ] **Step 1: Create the file**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Component.Board;
using Cysharp.Threading.Tasks;
using Data.Audio;
using Model.Board;
using Model.Game;
using Model.Stats;
using R3;
using Scene.Ingame;
using Service.AudioService;
using Service.BoardGameService;
using Service.GameService;
using Service.RouteService;
using Service.SkinService;
using Service.StatsService;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.Board
{
    /// <summary>
    /// Coordinates the layered-board game (Pyramid): deal → tap-match → win/stuck → undo, with a live HUD.
    /// Reuses the shared <see cref="IngameShellView"/> and drives <see cref="IBoardGameService"/> via tap input.
    /// </summary>
    public sealed class BoardPresenter : IStartable, ITickable, IDisposable
    {
        private const string BoardSceneName = "BoardScene";

        [Inject] private IngameShellView Shell { get; set; }
        [Inject] private UIBoardController BoardController { get; set; }
        [Inject] private IBoardGameService BoardGameService { get; set; }
        [Inject] private ISessionStatsService SessionStats { get; set; }
        [Inject] private IRouteService RouteService { get; set; }
        [Inject] private IAudioService AudioService { get; set; }
        [Inject] private ISkinService SkinService { get; set; }
        [Inject] private ILifetimeStatsService LifetimeStats { get; set; }

        private readonly CompositeDisposable disposable = new();
        private CompositeDisposable gameSubscriptions = new();
        private readonly ZeroScoreRule zeroScoreRule = new();

        private GameType currentGameType;
        private IBoardScoreRule scoreRule;
        private int prevTotal;
        private CancellationTokenSource lifeCts;

        public void Start()
        {
            lifeCts = new CancellationTokenSource();
            WireShellButtons();
            WireBoardInput();

            SkinService.CurrentSpriteSet
                .Where(set => set != null)
                .Subscribe(set => BoardController.ApplySpriteSet(set))
                .AddTo(disposable);

            SessionStats.OnStatsChanged
                .Subscribe(s =>
                {
                    Shell.UpdateHudScore(s.Score);
                    Shell.UpdateHudMoves(s.MoveCount);
                    Shell.UpdateHudTime(s.ElapsedSeconds);
                })
                .AddTo(disposable);

            RouteService.OnSamePathNavigated
                .Subscribe(_ => InitializeGame())
                .AddTo(disposable);

            InitializeGame();
        }

        public void Tick() => SessionStats.Tick(Time.unscaledDeltaTime);

        // --- Setup ---

        private void InitializeGame()
        {
            var query = new IngameQuery(RouteService.CurrentQuery);
            currentGameType = query.GameType;
            int variant = query.Variant ?? 1;

            BoardLayout layout;
            IBoardMatchRule matchRule;
            switch (currentGameType)
            {
                case GameType.Pyramid:
                default:
                    layout = PyramidLayoutFactory.Create(variant);
                    matchRule = new PyramidMatchRule();
                    scoreRule = new PyramidScoreRule();
                    break;
            }

            gameSubscriptions.Dispose();
            gameSubscriptions = new CompositeDisposable();

            BoardGameService.Initialize(layout, matchRule, query.Seed);
            SessionStats.Initialize(zeroScoreRule);

            Shell.HideWinPanel();
            Shell.HideStuckPanel();
            Shell.HidePausePanel();
            Shell.HideStatsPanel();
            Shell.ResetHud();
            Shell.SetInputBlocker(false);

            BoardController.DespawnAll();
            var state = BoardGameService.CurrentState;
            BoardController.RenderBoard(state);
            RefreshHighlights(state);
            BoardController.SetSelection(SelectionSnapshot.Empty);
            prevTotal = TotalCards(state);

            BoardGameService.OnBoardStateChanged
                .Subscribe(OnBoardStateChanged)
                .AddTo(gameSubscriptions);
            BoardGameService.OnSelectionChanged
                .Subscribe(sel => BoardController.SetSelection(sel))
                .AddTo(gameSubscriptions);
        }

        // --- Input ---

        private void WireBoardInput()
        {
            BoardController.OnCellTapped.Subscribe(id => BoardGameService.SelectCell(id)).AddTo(disposable);
            BoardController.OnWasteTapped.Subscribe(_ => BoardGameService.SelectWasteTop()).AddTo(disposable);
            BoardController.OnStockTapped.Subscribe(_ => BoardGameService.DrawFromStock()).AddTo(disposable);
        }

        // --- State / scoring / win / stuck ---

        private void OnBoardStateChanged(BoardState next)
        {
            BoardController.RenderBoard(next);
            RefreshHighlights(next);

            int newTotal = TotalCards(next);
            int removed = prevTotal - newTotal;
            prevTotal = newTotal;

            bool won = BoardGameService.IsWon(next);

            if (!SessionStats.Current.IsFinished)
            {
                if (removed > 0)
                {
                    int pts = scoreRule.ScoreForRemoval(removed);
                    if (won) pts += scoreRule.BoardClearedBonus;
                    SessionStats.RecordScoreDelta(pts);
                    AudioService.Play(AudioCatalog.Card.Place);
                }
                else if (removed == 0)
                {
                    // Stock draw: a move with no score. (removed < 0 = undo/restore, recorded by its own handler.)
                    SessionStats.RecordScoreDelta(0);
                }
            }

            if (won && !SessionStats.Current.IsFinished)
            {
                HandleWin();
                return;
            }

            if (!SessionStats.Current.IsFinished && !BoardGameService.HasAnyMove(next))
            {
                AudioService.Play(AudioCatalog.Game.Stuck);
                Shell.ShowStuckPanel(BoardGameService.CanUndo);
            }
        }

        private void HandleWin()
        {
            SessionStats.MarkWon();
            Shell.SetInputBlocker(true);
            AudioService.Play(AudioCatalog.Game.Win);
            PlayWinCelebrationAsync().Forget();
        }

        private async UniTaskVoid PlayWinCelebrationAsync()
        {
            var ct = lifeCts.Token;
            try { await Shell.PlayWinEffectAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { Debug.LogException(e); }
            if (ct.IsCancellationRequested) return;

            var c = SessionStats.Current;
            var code = GameCode.Encode(currentGameType, BoardGameService.CurrentSeed ?? 0);
            Shell.ShowWinPanel(c.Score, c.MoveCount, c.ElapsedSeconds, code);
            Shell.TriggerWin();
        }

        private void RefreshHighlights(BoardState state)
        {
            var free = new HashSet<CellId>();
            foreach (var id in BoardRules.FreeCells(BoardGameService.Layout, state)) free.Add(id);
            BoardController.SetFreeCells(free);
        }

        private static int TotalCards(BoardState s)
        {
            int occ = 0;
            foreach (var _ in s.OccupiedCells()) occ++;
            return occ + s.Stock.Count + s.Waste.Count;
        }

        // --- Shell buttons (mirror IngamePresenter; persistence/daily/hint deferred to 2c) ---

        private void WireShellButtons()
        {
            Shell.OnUndoObservable().Subscribe(_ =>
            {
                if (!BoardGameService.CanUndo) return;
                AudioService.Play(AudioCatalog.Game.Undo);
                BoardGameService.Undo();
                SessionStats.RecordMove(new ScoredMoveInfo(MoveType.Undo));
            }).AddTo(disposable);

            Shell.OnNewGameObservable().Subscribe(_ =>
            {
                AudioService.Play(AudioCatalog.Game.New);
                StartNewGameAsync().Forget();
            }).AddTo(disposable);

            Shell.OnPauseObservable().Subscribe(_ =>
            {
                AudioService.Play(AudioCatalog.UI.Open);
                SessionStats.Pause();
                AudioService.Pause();
                Shell.ShowPausePanel();
            }).AddTo(disposable);

            Shell.OnStatsObservable().Subscribe(_ =>
                Shell.ShowStatsPanel(LifetimeStats.GetStats(currentGameType))).AddTo(disposable);

            Shell.OnApplicationPauseObservable().Subscribe(paused =>
            {
                if (paused) { SessionStats.Pause(); AudioService.Pause(); }
                else { SessionStats.Resume(); AudioService.UnPause(); }
            }).AddTo(disposable);

            // Pause panel
            Shell.OnPauseToGameObservable().Subscribe(_ =>
            {
                AudioService.Play(AudioCatalog.UI.Close);
                Shell.HidePausePanel();
                SessionStats.Resume();
                AudioService.UnPause();
            }).AddTo(disposable);

            Shell.OnPauseNewGameObservable().Subscribe(_ =>
            {
                Shell.HidePausePanel();
                SessionStats.Resume();
                AudioService.UnPause();
                AudioService.Play(AudioCatalog.Game.New);
                StartNewGameAsync().Forget();
            }).AddTo(disposable);

            Shell.OnPauseRestartObservable().Subscribe(_ =>
            {
                Shell.HidePausePanel();
                SessionStats.Resume();
                AudioService.UnPause();
                AudioService.Play(AudioCatalog.Game.New);
                StartRestartAsync().Forget();
            }).AddTo(disposable);

            Shell.OnPauseLobbyObservable().Subscribe(_ =>
            {
                Shell.HidePausePanel();
                SessionStats.Resume();
                AudioService.UnPause();
                AudioService.Play(AudioCatalog.UI.Click);
                RouteService.GoBackAsync().Forget();
            }).AddTo(disposable);

            // Stuck panel
            Shell.OnStuckNewGameObservable().Subscribe(_ =>
            {
                Shell.HideStuckPanel();
                AudioService.Play(AudioCatalog.Game.New);
                StartNewGameAsync().Forget();
            }).AddTo(disposable);

            Shell.OnStuckRestartObservable().Subscribe(_ =>
            {
                Shell.HideStuckPanel();
                AudioService.Play(AudioCatalog.Game.New);
                StartRestartAsync().Forget();
            }).AddTo(disposable);

            Shell.OnStuckUndoObservable().Subscribe(_ =>
            {
                if (!BoardGameService.CanUndo) return;
                Shell.HideStuckPanel();
                AudioService.Play(AudioCatalog.Game.Undo);
                BoardGameService.Undo();
                SessionStats.RecordMove(new ScoredMoveInfo(MoveType.Undo));
            }).AddTo(disposable);

            // Win panel
            Shell.OnWinLobbyObservable().Subscribe(_ =>
            {
                AudioService.Play(AudioCatalog.UI.Click);
                RouteService.GoBackAsync().Forget();
            }).AddTo(disposable);
        }

        private async UniTaskVoid StartNewGameAsync()
        {
            try
            {
                var query = new IngameQuery(RouteService.CurrentQuery);
                var variantStr = (query.Variant ?? 1).ToString(CultureInfo.InvariantCulture);
                var q = new Dictionary<string, string>
                {
                    { GameRouteParams.GameType, query.GameType.ToString() },
                    { GameRouteParams.Variant, variantStr },
                };
                await RouteService.NavigateAsync(BoardSceneName, q);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private async UniTaskVoid StartRestartAsync()
        {
            try
            {
                var seed = BoardGameService.CurrentSeed;
                if (!seed.HasValue) { StartNewGameAsync().Forget(); return; }

                var query = new IngameQuery(RouteService.CurrentQuery);
                var variantStr = (query.Variant ?? 1).ToString(CultureInfo.InvariantCulture);
                var q = new Dictionary<string, string>
                {
                    { GameRouteParams.GameType, query.GameType.ToString() },
                    { GameRouteParams.Variant, variantStr },
                    { GameRouteParams.Seed, seed.Value.ToString(CultureInfo.InvariantCulture) },
                };
                await RouteService.NavigateAsync(BoardSceneName, q);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        public void Dispose()
        {
            lifeCts?.Cancel();
            lifeCts?.Dispose();
            gameSubscriptions.Dispose();
            disposable.Dispose();
        }
    }
}
```

- [ ] **Step 2: Refresh + `uloop compile` → ErrorCount 0.** (`Scene.asmdef` already references `Component`, `Service`, `Model`, `Data`, `Audio`, plus VContainer/R3/UniTask. `IngameShellView`/`IngameQuery`/`GameRouteParams` live in the same `Scene` assembly.)

- [ ] **Step 3: Commit.**
```bash
git add Assets/Scripts/Scene/Board/BoardPresenter.cs Assets/Scripts/Scene/Board/BoardPresenter.cs.meta
git commit -m "feat(board): add BoardPresenter (tap-match, win/stuck/undo, HUD, reuses shell)"
```

---

### Task 6: `BoardScene` (DI root)

**Files:** Create `Assets/Scripts/Scene/Board/BoardScene.cs`.

- [ ] **Step 1: Create the file**

```csharp
using Audio;
using Core;
using Data.Audio;
using Component.Board;
using Scene.Ingame;
using Service.BoardGameService;
using Service.GameService;
using Service.StatsService;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.Board
{
    /// <summary>DI root for the layered-board game scene (Pyramid). Reuses the shared <see cref="IngameShellView"/>.</summary>
    public class BoardScene : SceneBase
    {
        [SerializeField] private UIBoardController boardController;
        [SerializeField] private IngameShellView shellView;

        [Header("Audio")]
        [SerializeField] private AudioDatabaseAsset sceneAudioDatabase;

        private AudioSystem audioSystem;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(boardController);
            builder.RegisterComponent(shellView);

            if (sceneAudioDatabase != null)
            {
                builder.RegisterBuildCallback(container =>
                {
                    audioSystem = container.Resolve<AudioSystem>();
                    audioSystem.AddDatabase(sceneAudioDatabase);
                });
            }

            builder.Register<FisherYatesShuffleStrategy>(Lifetime.Scoped).As<IShuffleStrategy>();
            builder.Register<BoardGameService>(Lifetime.Scoped).As<IBoardGameService>();
            builder.Register<SessionStatsService>(Lifetime.Scoped).As<ISessionStatsService>();

            builder.RegisterEntryPoint<BoardPresenter>().As<BoardPresenter>();
        }

        protected override void OnDestroy()
        {
            if (audioSystem != null && sceneAudioDatabase != null)
                audioSystem.RemoveDatabase(sceneAudioDatabase);
            base.OnDestroy();
        }
    }
}
```

> App-level singletons (`IRouteService`, `ISkinService`, `IAudioService`, `ILifetimeStatsService`, `AudioSystem`) resolve from the parent App scope — same as `IngameScene`. Only scene-scoped services are registered here.

- [ ] **Step 2: Refresh + `uloop compile` → ErrorCount 0.**

- [ ] **Step 3: Commit.**
```bash
git add Assets/Scripts/Scene/Board/BoardScene.cs Assets/Scripts/Scene/Board/BoardScene.cs.meta
git commit -m "feat(board): add BoardScene DI root"
```

---

### Task 7: Route board games to `BoardScene` from the Lobby

**Files:** Modify `Assets/Scripts/Scene/Lobby/LobbyPresenter.cs`.

- [ ] **Step 1:** Add `using Model.Game;` if not present (it already uses `GameType` via `Model.Game`). Replace the three `RouteService.NavigateAsync("Ingame", query)` call sites that depend on the selected game type:
  - In `OnTileSelected` (line ~191):
    ```csharp
    var scene = selection.GameType.IsBoardMode() ? "BoardScene" : "Ingame";
    RouteService.NavigateAsync(scene, query).Forget();
    ```
  - In `HandleValidCode` (line ~321):
    ```csharp
    var scene = gameType.IsBoardMode() ? "BoardScene" : "Ingame";
    RouteService.NavigateAsync(scene, query).Forget();
    ```
  - Leave `OnDailyTileSelected` (line ~270) as `"Ingame"` — Daily is Klondike (a card game).

- [ ] **Step 2: Refresh + `uloop compile` → 0. Run EditMode tests → 0 failed, count ≥ baseline.**

- [ ] **Step 3: Commit.**
```bash
git add Assets/Scripts/Scene/Lobby/LobbyPresenter.cs
git commit -m "feat(lobby): route layered-board games to BoardScene"
```

---

## Phase C — Scene / prefab / assets (manual play gate)

> ⚠️ **Highest-risk phase** (editor surgery, like Plan 2b Task 5). Do NOT batch — wire, compile, **play**, commit. Use `uloop execute-dynamic-code` with `SerializedObject`/`PrefabUtility` for deterministic wiring (mirrors the skin + 2b automation). After each task: `uloop control-play-mode stop` and a clean `uloop compile`.

### Task 8: Extract `IngameShell.prefab` (base shell + empty `PlayArea`) — the deferred 2b Task 5 step

Lifts the in-scene shell UI into a reusable base prefab so BoardScene can be a variant. The card game must keep working.

**Files:** `Assets/Scenes/Ingame.unity`; new `Assets/Prefabs/InGame/IngameShell.prefab`.

- [ ] **Step 1:** Dump the Ingame canvas tree (`uloop get-hierarchy`, or `execute-dynamic-code` listing the Canvas root and the objects referenced by `IngameShellView` + `IngameComponent`). Identify: the Canvas root, the shell panels (HUD/Win/DailyWin/Stuck/Pause/Setting/Stats/Toast/CodeInput/winEffect/inputBlocker), the `IngameShellView` host, and the `UICardsController` (+ its `UIPlaceHolder` anchors).
- [ ] **Step 2:** Under the shell Canvas, add an empty `RectTransform` child named **`PlayArea`** (stretch-to-fill). Re-parent `UICardsController` (and the card placeholder anchors) under `PlayArea`. Verify `IngameComponent.cardsController` + `UICardsController.placeholders` references survive the reparent (they reference by object, not path — they should).
- [ ] **Step 3:** Select the shell Canvas root (now containing shell panels + `IngameShellView` + empty-structure `PlayArea` with the cards as instance children) and save it as **`Assets/Prefabs/InGame/IngameShell.prefab`** via `PrefabUtility.SaveAsPrefabAssetAndConnect(canvasRoot, path, InteractionMode.UserAction)`. The base prefab should own the shell + the `PlayArea` container; the **cards stay as scene-side children of the instance's `PlayArea`** (added-component/added-GameObject overrides) so the base prefab itself has an empty PlayArea for variants.
  - If isolating the cards as overrides is fiddly in automation, an acceptable equivalent: make the base prefab with shell + empty `PlayArea` only (temporarily move cards out), then in the Ingame scene instance, parent the card objects back under the instance's `PlayArea`. The end state must be: **base prefab = shell + empty PlayArea; Ingame scene = prefab instance + cards under PlayArea.**
- [ ] **Step 4: Manual gate (card game):** `uloop control-play-mode play`; `uloop screenshot`; confirm Klondike deals, drag-moves, wins (panel + confetti + cascade), pause/settings/stuck panels all work. `uloop control-play-mode stop`. `uloop run-tests --test-mode EditMode` → 0 failed.
- [ ] **Step 5: Commit.**
```bash
git add Assets/Scenes/Ingame.unity Assets/Prefabs/InGame/IngameShell.prefab Assets/Prefabs/InGame/IngameShell.prefab.meta
git commit -m "feat(ingame): extract IngameShell base prefab with empty PlayArea slot"
```

### Task 9: Build the Pyramid board prefab + `BoardScene.unity` variant

**Files:** new `Assets/Prefabs/Board/PyramidBoard.prefab`; new `Assets/Scenes/BoardScene.unity`.

- [ ] **Step 1:** Create `PyramidBoard.prefab`: a `RectTransform` root holding a `UIBoardController`, with **28 cell anchors** laid out as a 7-row triangle (apex centered top; row `r` has `r+1` anchors, evenly spaced, overlapping the row above by ~40%). Assign them to `UIBoardController.cellAnchors` so **element index = CellId.Value** (apex = index 0, then row-major left→right). **Order the anchor GameObjects apex→base in the hierarchy** (base last) so lower rows render on top (overlap correctness). Add a `stockAnchor` and `wasteAnchor` (e.g. bottom-left / bottom-right). Assign `cardPrefab` = the existing `UICard` prefab used by `UICardsController`.
  - Anchor positions can be generated in `execute-dynamic-code`: for row `r` (0..6), x = `(k - r/2) * dx`, y = `-r * dy`, with `dx`≈card width, `dy`≈card height*0.6; parent each under the controller root and collect into `cellAnchors[rowStart+k]`.
- [ ] **Step 2:** Create `Assets/Scenes/BoardScene.unity`. Add a **prefab variant instance of `IngameShell.prefab`** (`PrefabUtility.InstantiatePrefab` + the scene gets the variant). Place a `PyramidBoard.prefab` instance under the variant's `PlayArea`. Add an empty `BoardScene` GameObject (the `BoardScene : SceneBase` LifetimeScope) — set Auto Run / parent like `IngameScene`. Add the EventSystem/Canvas essentials the variant inherits from the base.
- [ ] **Step 3:** Wire DI SerializeFields via `execute-dynamic-code` + `SerializedObject`:
  - `BoardScene.boardController` → the `PyramidBoard` instance's `UIBoardController`.
  - `BoardScene.shellView` → the variant's `IngameShellView`.
  - `BoardScene.sceneAudioDatabase` → the same `AudioDatabaseAsset` the Ingame scene uses.
  - In the variant, **hide the Hint / Play-with-code / Daily shell buttons** (deferred features) via prefab override (`SetActive(false)`), so no dead buttons appear.
- [ ] **Step 4:** Register the scene: add `Assets/Scenes/BoardScene.unity` to `ProjectSettings/EditorBuildSettings.asset` (`EditorBuildSettings.scenes` += new `EditorBuildSettingsScene(path, true)` via `execute-dynamic-code`).
- [ ] **Step 5: Commit.**
```bash
git add Assets/Prefabs/Board/PyramidBoard.prefab Assets/Prefabs/Board/PyramidBoard.prefab.meta Assets/Scenes/BoardScene.unity Assets/Scenes/BoardScene.unity.meta ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(board): add PyramidBoard prefab + BoardScene variant; register in build settings"
```

### Task 10: Pyramid `GameVariant` asset + Lobby tile

**Files:** new `Assets/ScriptableObjects/GameVariants/Pyramid.asset`; Lobby scene (`Assets/Scenes/Lobby.unity` + a `GameTileView`).

- [ ] **Step 1:** Create `Assets/ScriptableObjects/GameVariants/Pyramid.asset` (`Solitaire/Game/Game Variant`): `gameType = 4` (Pyramid), `variantId = 1`, `displayName = "Pyramid"`, `dealRule = null` (board games don't use it), `previewIcon` = a placeholder sprite (reuse an existing one for now).
- [ ] **Step 2:** In the Lobby scene, add a Pyramid `GameTileView` (duplicate an existing tile prefab/object; per CLAUDE.md prefer a variant of the existing tile). Set its `variant` = `Pyramid.asset`. Add it to `LobbyComponent.tiles[]`.
- [ ] **Step 3: Manual gate:** play Lobby → tap Pyramid → confirm it routes to `BoardScene` (not Ingame). `uloop control-play-mode stop`.
- [ ] **Step 4: Commit.**
```bash
git add Assets/ScriptableObjects/GameVariants/Pyramid.asset Assets/ScriptableObjects/GameVariants/Pyramid.asset.meta Assets/Scenes/Lobby.unity
git commit -m "feat(lobby): add Pyramid game variant + lobby tile"
```

### Task 11: Final manual gate + regression

- [ ] **Step 1:** `uloop fix` if needed, then clean refresh + `uloop compile` → ErrorCount 0, WarningCount 0.
- [ ] **Step 2:** `uloop run-tests --test-mode EditMode` → 0 failed, count ≥ baseline.
- [ ] **Step 3: Play Pyramid end-to-end:** deal renders the 28-card triangle + stock; tapping a free card highlights it; a sum-13 pair (or a free King) clears with score+move+time updating; stock draw flips to waste; waste-top is tappable; **win** (clear the pyramid — use a known-winnable seed or just verify the win path by clearing) shows confetti + win panel; **stuck** (exhaust stock with no pair) shows the stuck panel; **undo** restores the last move; pause / new-game / restart / lobby / stats all work.
- [ ] **Step 4: Regression:** play Klondike AND Easthaven in the (now prefab-instanced) Ingame scene — confirm no shell/HUD/celebration regressions.
- [ ] **Step 5:** Push. Update PR #103 description with the Pyramid slice.
```bash
git push
```

---

## What this enables / defers

- **Enables:** Pyramid is playable end-to-end (in-session), and the BoardScene variant + `UIBoardController` are the reusable substrate for TriPeaks (sequence input) and Mahjong (z-layer + side rule) later.
- **Defers to Plan 2c:** `BoardSnapshot` gateway + auto-save + resume (so a game survives app relaunch), lifetime-stats recording on win, **Hint** (board hint service + flash), removal fade/shrink animation, daily/achievements for board, and a score-tuning ScriptableObject. The board variant hides the not-yet-wired shell buttons until then.

---

## Self-Review (author checklist)

- **Spec coverage (§4–8):** render = Task 4 (`UIBoardController`); tap-match/win/stuck/undo/score-HUD = Task 5 (`BoardPresenter`) + Tasks 1–2 (selection feedback + board score entry point); scene/DI = Task 6; shared shell + prefab variant (§7) = Tasks 8–9; Lobby/Route/BuildSettings (§8) = Tasks 3, 7, 9, 10. Snapshot/Hint (§5.3, §2) explicitly deferred to 2c with the rationale stated, and dead buttons hidden — no silent gap.
- **Placeholder scan:** all C# tasks carry full source; editor tasks (8–10) are procedural at the 2b precedent level (Unity prefab/scene work isn't deterministic source) with the exact wiring targets, anchor-generation formula, and `SerializedObject`/`EditorBuildSettings` mechanics named.
- **Type consistency:** `SelectionSnapshot` (Task 1) is consumed by `UIBoardController.SetSelection` (Task 4) + `BoardPresenter` (Task 5). `RecordScoreDelta`/`ZeroScoreRule` (Task 2) used by `BoardPresenter`/`BoardScene`. `IsBoardMode` (Task 3) used by `LobbyPresenter` (Task 7). `IBoardScoreRule.ScoreForRemoval/BoardClearedBonus`, `PyramidScoreRule`, `IBoardGameService.*`, `IngameShellView.*` (Plan 2b), `GameCode.Encode`, `FisherYatesShuffleStrategy` — all verified against current source.
- **Risk control:** Phase A is pure-logic TDD (count goes up, never down); Phase B ends at compile-0; Phase C is per-task wire→play→commit with the card-game regression re-checked after the prefab extraction (the single largest risk).
- **Scoring correctness:** state-diff (`prevTotal − newTotal`) distinguishes match (>0), draw (==0), undo/restore (<0, skipped — recorded by its own handler), and folds the board-clear bonus into the winning removal so it's one move; subscription starts after `Initialize` (plain `Subject` doesn't replay) so the initial deal isn't a move. The single-level `lastScoreDelta` undo-restore limitation matches the existing card game (acceptable parity).
```