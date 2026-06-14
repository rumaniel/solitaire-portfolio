# TriPeaks (Board Mode) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a fully playable TriPeaks game to the board-mode engine with authentic Microsoft-Solitaire-Collection scoring (streak + peak bonuses), reusing the existing board shell.

**Architecture:** A shared `BoardGameServiceBase` holds the scaffolding common to both board games; `PyramidGameService` (the reshaped current service) and a new `TriPeaksGameService` subclass it. A stateful presenter-side `IBoardScorer` (`PyramidScorer` / `TriPeaksScorer`) turns a `(prev, next, won)` transition into `(points, soundKind)`. A `BoardAnchorSet` lets one `UIBoardController` render either board; both games route to the same `BoardScene`, selected at runtime by `GameType`.

**Tech Stack:** Unity 6, C#, VContainer (DI), R3 (Observables), NUnit (EditMode), `uloop` CLI.

**Spec:** `Docs/superpowers/specs/2026-06-10-board-tripeaks-design.md`

**Phases:** P1 Logic (T1–T7, pure C# + TDD) → P2 Presenter & DI (T8–T9, compile + regression) → P3 Scene/Editor (T10–T13, Unity editor + play-verify).

> **P3 PIVOT (2026-06-10, implementation discovery — supersedes the `BoardAnchorSet` design in spec §7 and tasks T10/T11 below):** the shipped `UIBoardController` is baked **inside** `PyramidBoard.prefab` (on its root, with the 28 anchors as children), so the single-controller / `BoardAnchorSet` plan would require restructuring the just-shipped Pyramid board (moving the controller out to a scene parent). Per the user's "don't disturb Pyramid" constraint we pivoted to **two controllers** (user-confirmed):
> - **T10′** (`6a6c169`): a `BoardViewSet` holder (`Scene/Board/BoardViewSet.cs`) carrying both board views; `BoardScene` serializes `boardController` (Pyramid) + `triPeaksBoardController` and registers `RegisterInstance(new BoardViewSet(...))`; the presenter injects `BoardViewSet`, resolves the active `UIBoardController` per init via `For(gameType)`, `SetActive`s only the matching board, and wires **both** boards' taps to the active service. `UIBoardController.cs` and `PyramidBoard.prefab` are left untouched. No `BoardAnchorSet` is created.
> - **T11′** (`430b798`): `TriPeaksBoard.prefab` is built by **copying** `PyramidBoard.prefab` (preserves all controller/anchor wiring) and repositioning the 28 anchors into the 3-peak + 10-card-base layout (col scheme: apex 3/9/15, row1 2,4/8,10/14,16, row2 1,3,5/7,9,11/13,15,17, base 0,2,…,18 in half-card-width units; PairGuide disabled). The instance is parented under `PlayArea` in `BoardScene` and assigned to `triPeaksBoardController`.
> - **Play-verified (TriPeaks):** board renders as 3 peaks + base; playing a 5 onto the 4 waste-top scored 50 (streak ×50) and incremented moves — MSSC scoring confirmed end-to-end.
> Tasks T10/T11 as written below (BoardAnchorSet, single-controller) are **NOT** the implemented approach; T12/T13 still apply (lobby tile + variant + localization; final play-verify + review).

**Build/test commands (every task that changes `.cs`):**
- Force a real recompile after adding/renaming files:
  `uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"`
- Then poll `uloop compile` until it stops returning "Unity is compiling scripts" / "Domain Reload"; expect `ErrorCount: 0`.
- Run tests: `uloop run-tests --test-mode EditMode`. (If a result looks STALE — `ErrorCount:1` with empty `Errors:[]` mid-reload — re-poll.)
- The EditMode suite is currently fully green; each task must keep it green and add its own passing tests.

---

# PHASE 1 — LOGIC

## Task 1: `BoardState.WithCardPlayedToWaste`

The TriPeaks "play a free card onto the waste" mutation: the cell clears and that same card becomes the new waste-top. Additive — no existing call site changes.

**Files:**
- Modify: `Assets/Scripts/Model/Board/BoardState.cs`
- Test: `Assets/Tests/EditMode/BoardStateTests.cs`

- [ ] **Step 1: Write the failing test** — append inside `BoardStateTests` (before the closing brace):

```csharp
        [Test]
        public void WithCardPlayedToWaste_RemovesCell_AndPushesCardToWasteTop()
        {
            var cellCards = new[]
            {
                C(Rank.Seven, Suit.Spade),  // CellId 0
                C(Rank.Nine, Suit.Heart),   // CellId 1
            };
            var waste = new[] { C(Rank.Eight, Suit.Club) };
            var state = new BoardState(cellCards, stock: null, waste: waste);

            var next = state.WithCardPlayedToWaste(new CellId(0));

            Assert.IsFalse(next.HasCard(new CellId(0)), "played cell cleared");
            Assert.IsTrue(next.HasCard(new CellId(1)), "other cell untouched");
            Assert.AreEqual(Rank.Seven, next.WasteTop.Rank, "played card is the new waste-top");
            Assert.AreEqual(2, next.Waste.Count);
            Assert.IsTrue(state.HasCard(new CellId(0)), "original state unchanged (immutability)");
            Assert.AreEqual(Rank.Eight, state.WasteTop.Rank, "original waste-top unchanged");
        }

        [Test]
        public void WithCardPlayedToWaste_EmptyCell_IsNoOp()
        {
            var state = new BoardState(new[] { C(Rank.Seven, Suit.Spade) });
            var cleared = state.WithCellsRemoved(new[] { new CellId(0) });
            var next = cleared.WithCardPlayedToWaste(new CellId(0)); // already empty
            Assert.AreSame(cleared, next);
        }
```

- [ ] **Step 2: Run to verify it fails**

Run: `uloop run-tests --test-mode EditMode`
Expected: compile error / failure — `WithCardPlayedToWaste` is not defined.

- [ ] **Step 3: Implement** — add to `BoardState.cs` after `WithWasteTopRemoved()` (around line 80):

```csharp
        /// <summary>Plays a free cell's card onto the waste (TriPeaks): the cell clears and that card
        /// becomes the new waste-top. No-op if the cell is already empty.</summary>
        public BoardState WithCardPlayedToWaste(CellId id)
        {
            var card = cells[id.Value];
            if (card == null) return this;
            var copy = (PlayingCard[])cells.Clone();
            copy[id.Value] = null;
            var newWaste = new List<PlayingCard>(Waste) { card };
            return new BoardState(copy, Stock, newWaste.AsReadOnly(), RecycleCount);
        }
```

- [ ] **Step 4: Run to verify it passes**

Run: `uloop run-tests --test-mode EditMode`
Expected: PASS, no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Model/Board/BoardState.cs Assets/Tests/EditMode/BoardStateTests.cs
git commit -m "feat(board): BoardState.WithCardPlayedToWaste for TriPeaks"
```

---

## Task 2: `TriPeaksLayoutFactory`

The 28-cell, three-peak cover graph (rows 3/6/9/10), plus the apex-cell ids.

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/TriPeaksLayoutFactory.cs`
- Test: `Assets/Tests/EditMode/TriPeaksLayoutFactoryTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections;
using System.Linq;
using Model.Board;
using Model.Game;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksLayoutFactoryTests
    {
        [Test]
        public void Creates28Cells_ForTriPeaks()
        {
            var layout = TriPeaksLayoutFactory.Create();
            Assert.AreEqual(GameType.TriPeaks, layout.GameType);
            Assert.AreEqual(28, layout.Count);
        }

        [Test]
        public void ApexCellIds_AreTheThreeRow0Tips()
        {
            CollectionAssert.AreEquivalent(
                new[] { new CellId(0), new CellId(1), new CellId(2) },
                TriPeaksLayoutFactory.ApexCellIds);
        }

        [Test]
        public void Apex0_CoveredByRow1Cells3And4()
        {
            var layout = TriPeaksLayoutFactory.Create();
            var apex = layout.Cell(new CellId(0));
            Assert.AreEqual(2, apex.CoverBlockers.Count);
            Assert.Contains(new CellId(3), (ICollection)apex.CoverBlockers);
            Assert.Contains(new CellId(4), (ICollection)apex.CoverBlockers);
        }

        [Test]
        public void BaseRow_HasTenCellsWithNoCover()
        {
            var layout = TriPeaksLayoutFactory.Create();
            for (int id = 18; id <= 27; id++)
                Assert.AreEqual(0, layout.Cell(new CellId(id)).CoverBlockers.Count, $"cell {id}");
        }

        [Test]
        public void Row2Cell9_CoveredByBaseCells18And19()
        {
            var layout = TriPeaksLayoutFactory.Create();
            var cell = layout.Cell(new CellId(9));
            Assert.Contains(new CellId(18), (ICollection)cell.CoverBlockers);
            Assert.Contains(new CellId(19), (ICollection)cell.CoverBlockers);
        }

        [Test]
        public void Constructs_WithoutThrowing_AllBlockersValidAndDense()
        {
            // BoardLayout's ctor validates dense ids + real blockers; reaching here means the graph is sound.
            Assert.DoesNotThrow(() => TriPeaksLayoutFactory.Create());
            var layout = TriPeaksLayoutFactory.Create();
            Assert.AreEqual(28, layout.Cells.Select(c => c.Id).Distinct().Count());
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `uloop run-tests --test-mode EditMode`
Expected: FAIL — `TriPeaksLayoutFactory` not defined.

- [ ] **Step 3: Implement**

```csharp
using System.Collections.Generic;
using Model.Board;
using Model.Game;

namespace Service.BoardGameService
{
    /// <summary>
    /// Builds the classic three-peak, 28-card TriPeaks topology in code. Logical cover graph only;
    /// render positions are fixed prefab anchors supplied by the View (the three visually separate
    /// peaks are a render concern — the cover graph is a single dense set, with rows 2↔3 forming one
    /// continuous base strip that links the peaks).
    /// Cell ids: row0 (apexes) 0..2, row1 3..8, row2 9..17, row3 (base) 18..27.
    /// </summary>
    public static class TriPeaksLayoutFactory
    {
        public const int CellCount = 28; // 3 + 6 + 9 + 10

        public static readonly IReadOnlyList<CellId> ApexCellIds =
            new[] { new CellId(0), new CellId(1), new CellId(2) };

        public static BoardLayout Create(int variant = 1)
        {
            var cells = new List<BoardCell>(CellCount);

            // Row 0 (apex p, id p): covered by the two row-1 cells of peak p.
            for (int p = 0; p < 3; p++)
                cells.Add(new BoardCell(new CellId(p), new[] { new CellId(3 + (2 * p)), new CellId(4 + (2 * p)) }));

            // Row 1 (ids 3..8): peak p's left cell (3+2p) covered by row-2 (9+3p),(10+3p);
            // its right cell (4+2p) covered by (10+3p),(11+3p).
            for (int p = 0; p < 3; p++)
            {
                cells.Add(new BoardCell(new CellId(3 + (2 * p)),
                    new[] { new CellId(9 + (3 * p)), new CellId(10 + (3 * p)) }));
                cells.Add(new BoardCell(new CellId(4 + (2 * p)),
                    new[] { new CellId(10 + (3 * p)), new CellId(11 + (3 * p)) }));
            }

            // Row 2 (ids 9..17): cell 9+j covered by base cells 18+j and 19+j (continuous strip).
            for (int j = 0; j < 9; j++)
                cells.Add(new BoardCell(new CellId(9 + j), new[] { new CellId(18 + j), new CellId(19 + j) }));

            // Row 3 / base (ids 18..27): fully exposed, no cover.
            for (int b = 18; b < 28; b++)
                cells.Add(new BoardCell(new CellId(b), null));

            return new BoardLayout(GameType.TriPeaks, variant, cells);
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `uloop run-tests --test-mode EditMode`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/TriPeaksLayoutFactory.cs Assets/Tests/EditMode/TriPeaksLayoutFactoryTests.cs
git commit -m "feat(board): TriPeaksLayoutFactory three-peak cover graph"
```

---

## Task 3: `TriPeaksMatchRule`

Reuses `IBoardMatchRule`. A 2-card selection `[wasteTop, tapped]` is a Match iff the ranks differ by exactly 1 or form an {Ace, King} wrap.

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/TriPeaksMatchRule.cs`
- Test: `Assets/Tests/EditMode/TriPeaksMatchRuleTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksMatchRuleTests
    {
        private static readonly TriPeaksMatchRule Rule = new TriPeaksMatchRule();
        private static PlayingCard Card(Rank r) => new PlayingCard(r, Suit.Spade);
        private static MatchVerdict Eval(Rank a, Rank b)
            => Rule.Evaluate(new[] { Card(a), Card(b) });

        [Test] public void OneApart_IsMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.Seven, Rank.Eight));
        [Test] public void OneApart_Descending_IsMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.Eight, Rank.Seven));
        [Test] public void AceOnTwo_IsMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.Two, Rank.Ace));
        [Test] public void KingOnAce_WrapsToMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.Ace, Rank.King));
        [Test] public void AceOnKing_WrapsToMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.King, Rank.Ace));
        [Test] public void SameRank_IsInvalid() => Assert.AreEqual(MatchVerdict.Invalid, Eval(Rank.Five, Rank.Five));
        [Test] public void TwoApart_IsInvalid() => Assert.AreEqual(MatchVerdict.Invalid, Eval(Rank.Five, Rank.Seven));
        [Test] public void KingAndQueen_IsMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.King, Rank.Queen));

        [Test]
        public void SingleCard_IsIncomplete()
        {
            Assert.AreEqual(MatchVerdict.Incomplete, Rule.Evaluate(new[] { Card(Rank.Five) }));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `uloop run-tests --test-mode EditMode`
Expected: FAIL — `TriPeaksMatchRule` not defined.

- [ ] **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;

namespace Service.BoardGameService
{
    /// <summary>TriPeaks: a tapped free card plays onto the waste-top when their ranks differ by one,
    /// with Ace↔King wrap (Ace plays on King and King plays on Ace). The service evaluates the ordered
    /// pair [wasteTop, tapped]; order does not matter to the verdict.</summary>
    public sealed class TriPeaksMatchRule : IBoardMatchRule
    {
        public MatchVerdict Evaluate(IReadOnlyList<PlayingCard> selection)
        {
            if (selection.Count != 2) return MatchVerdict.Incomplete;
            int a = (int)selection[0].Rank;
            int b = (int)selection[1].Rank;
            int diff = Math.Abs(a - b);
            // diff 1 = adjacent ranks; diff 12 = Ace(1)↔King(13) wrap.
            return (diff == 1 || diff == 12) ? MatchVerdict.Match : MatchVerdict.Invalid;
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `uloop run-tests --test-mode EditMode`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/TriPeaksMatchRule.cs Assets/Tests/EditMode/TriPeaksMatchRuleTests.cs
git commit -m "feat(board): TriPeaksMatchRule rank-adjacency with A-K wrap"
```

---

## Task 4: Extract `BoardGameServiceBase`; reshape `BoardGameService` → `PyramidGameService`

Behavior-preserving refactor. Move the identical scaffolding to an abstract base; the Pyramid logic becomes `PyramidGameService : BoardGameServiceBase`. The existing service tests (renamed) prove no behavior changed.

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/BoardGameServiceBase.cs`
- Create: `Assets/Scripts/Service/BoardGameService/PyramidGameService.cs`
- Delete: `Assets/Scripts/Service/BoardGameService/BoardGameService.cs` (and its `.meta`)
- Modify: `Assets/Scripts/Scene/Board/BoardScene.cs` (registration name)
- Rename test: `Assets/Tests/EditMode/BoardGameServiceTests.cs` → `PyramidGameServiceTests.cs`

- [ ] **Step 1: Rename the test file and its references first (so the rename target exists)**

Rename `BoardGameServiceTests.cs` → `PyramidGameServiceTests.cs`. Inside it:
- class `BoardGameServiceTests` → `PyramidGameServiceTests`;
- every `new BoardGameService(` → `new PyramidGameService(`;
- the two helper return types `private BoardGameService NewService()` / `NewInitializedPyramid()` → `PyramidGameService`.

(There are ~20 occurrences — replace `BoardGameService` with `PyramidGameService` throughout the file, including the `BoardGameServiceTests` class name.)

- [ ] **Step 2: Run to verify it fails**

Run: `uloop run-tests --test-mode EditMode`
Expected: compile error — `PyramidGameService` not defined yet.

- [ ] **Step 3a: Create `BoardGameServiceBase.cs`**

```csharp
using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using R3;
using Service.GameService;

namespace Service.BoardGameService
{
    /// <summary>
    /// Shared scaffolding for the cover-based board games (Pyramid, TriPeaks): the state/selection
    /// streams, deal+split, stock draw/recycle, undo stack, snapshot restore, win check, and selection
    /// emission. Subclasses own the game-specific tap/apply/move-detection/hint logic.
    /// </summary>
    public abstract class BoardGameServiceBase : IBoardGameService, IDisposable
    {
        private readonly IShuffleStrategy shuffle;
        private readonly Subject<BoardState> stateSubject = new();
        private readonly Subject<SelectionSnapshot> selectionSubject = new();
        private readonly List<BoardState> undoStack = new();

        protected IBoardMatchRule Rule { get; private set; }
        protected int MaxRecycles { get; private set; }

        public BoardLayout Layout { get; private set; }
        public BoardState CurrentState { get; protected set; }
        public int? CurrentSeed { get; private set; }
        public Observable<BoardState> OnBoardStateChanged => stateSubject;
        public SelectionSnapshot CurrentSelection { get; private set; } = SelectionSnapshot.Empty;
        public Observable<SelectionSnapshot> OnSelectionChanged => selectionSubject;

        protected BoardGameServiceBase(IShuffleStrategy shuffle)
        {
            this.shuffle = shuffle ?? throw new ArgumentNullException(nameof(shuffle));
        }

        public void Initialize(BoardLayout layout, IBoardMatchRule rule, int? seed = null, int maxRecycles = 0)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            MaxRecycles = maxRecycles;

            int actualSeed = seed ?? DeckFactory.CreateRandomSeed();
            CurrentSeed = actualSeed;

            var deck = shuffle.Shuffle(actualSeed);
            if (deck.Count < layout.Count)
                throw new InvalidOperationException(
                    $"Deck ({deck.Count}) smaller than layout cell count ({layout.Count}).");

            var cellCards = new List<PlayingCard>(layout.Count);
            for (int i = 0; i < layout.Count; i++) cellCards.Add(deck[i]);

            var stock = new List<PlayingCard>(deck.Count - layout.Count);
            for (int i = layout.Count; i < deck.Count; i++) stock.Add(deck[i]);

            CurrentState = new BoardState(cellCards, stock, waste: null);
            undoStack.Clear();
            ResetSelectionState();
            OnDealt(); // hook: TriPeaks flips the first stock card to the waste so play has an anchor.
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public void DrawFromStock()
        {
            if (CurrentState.Stock.Count == 0) return;
            PushUndo();
            ResetSelectionState();
            CurrentState = CurrentState.WithStockDrawn();
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public bool CanRecycle(BoardState state)
            => state.Stock.Count == 0 && state.Waste.Count > 0 && state.RecycleCount < MaxRecycles;

        public void RecycleStock()
        {
            if (!CanRecycle(CurrentState)) return;
            PushUndo();
            ResetSelectionState();
            CurrentState = CurrentState.WithStockRecycled();
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public void ClearSelection()
        {
            ResetSelectionState();
            EmitSelection(SelectionSnapshot.Empty);
        }

        public bool IsWon(BoardState state) => !state.AnyOccupied();

        public bool CanUndo => undoStack.Count > 0;

        public void Undo()
        {
            if (undoStack.Count == 0) return;
            int last = undoStack.Count - 1;
            CurrentState = undoStack[last];
            undoStack.RemoveAt(last);
            ResetSelectionState();
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public IReadOnlyList<BoardState> UndoHistory => undoStack;

        public void Restore(BoardLayout layout, IBoardMatchRule rule, int seed,
            BoardState state, IReadOnlyList<BoardState> undoHistory, int maxRecycles = 0)
        {
            // Validate EVERYTHING before mutating any field — Restore must be atomic.
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.CellCount != layout.Count)
                throw new ArgumentException(
                    $"Snapshot state cell count ({state.CellCount}) does not match layout ({layout.Count}).",
                    nameof(state));
            if (undoHistory != null)
            {
                foreach (var past in undoHistory)
                {
                    if (past == null)
                        throw new ArgumentException("Undo history contains a null state.", nameof(undoHistory));
                    if (past.CellCount != layout.Count)
                        throw new ArgumentException(
                            $"Undo history state cell count ({past.CellCount}) does not match layout ({layout.Count}).",
                            nameof(undoHistory));
                }
            }

            Layout = layout;
            Rule = rule;
            MaxRecycles = maxRecycles;
            CurrentSeed = seed;
            undoStack.Clear();
            if (undoHistory != null) undoStack.AddRange(undoHistory);
            ResetSelectionState();

            CurrentState = state;
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public virtual void Dispose()
        {
            stateSubject.Dispose();
            selectionSubject.Dispose();
        }

        // --- protected helpers / hooks for subclasses ---

        protected void PushUndo() => undoStack.Add(CurrentState);

        protected void PublishState(BoardState state)
        {
            CurrentState = state;
            stateSubject.OnNext(CurrentState);
        }

        /// <summary>Emits a selection snapshot, skipping a redundant emission equal to the current one.</summary>
        protected void EmitSelection(SelectionSnapshot next)
        {
            if (next.Equals(CurrentSelection)) return;
            CurrentSelection = next;
            selectionSubject.OnNext(CurrentSelection);
        }

        /// <summary>Clears the subclass's pending-selection representation (Pyramid: its accumulator; TriPeaks: nothing).</summary>
        protected abstract void ResetSelectionState();

        /// <summary>Post-deal hook. Default no-op (Pyramid); TriPeaks flips the first stock card to the waste.</summary>
        protected virtual void OnDealt() { }

        // --- game-specific surface ---

        public abstract void SelectCell(CellId id);
        public abstract void SelectWasteTop();
        public abstract bool HasAnyMove(BoardState state);
        public abstract IReadOnlyList<BoardHint> GetHints(BoardState state);
    }
}
```

- [ ] **Step 3b: Create `PyramidGameService.cs`** (the current Pyramid logic, on the base)

```csharp
using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using Service.GameService;

namespace Service.BoardGameService
{
    /// <summary>Pyramid: tap free cells (and the waste-top) to remove pairs summing to 13, or a King
    /// alone. Recyclable stock. Owns its tap-accumulator; all shared plumbing lives in the base.</summary>
    public sealed class PyramidGameService : BoardGameServiceBase
    {
        private readonly List<SelectedTarget> selection = new(); // accumulator lives in the Service

        private static readonly IReadOnlyList<BoardHint> DrawHints = new[] { BoardHint.Draw };
        private static readonly IReadOnlyList<BoardHint> RecycleHints = new[] { BoardHint.Recycle };

        public PyramidGameService(IShuffleStrategy shuffle) : base(shuffle) { }

        protected override void ResetSelectionState() => selection.Clear();

        public override void SelectCell(CellId id)
        {
            if (!BoardRules.IsFree(Layout, CurrentState, id)) return;
            HandleSelect(SelectedTarget.OfCell(id), CurrentState.CardAt(id));
            EmitSelection(BuildSelectionSnapshot());
        }

        public override void SelectWasteTop()
        {
            var top = CurrentState.WasteTop;
            if (top == null) return;
            HandleSelect(SelectedTarget.Waste(), top);
            EmitSelection(BuildSelectionSnapshot());
        }

        private PlayingCard CardOf(SelectedTarget t)
            => t.IsWaste ? CurrentState.WasteTop : CurrentState.CardAt(t.Cell);

        private void HandleSelect(SelectedTarget target, PlayingCard card)
        {
            if (card == null) return;

            int existing = selection.FindIndex(t => t.Equals(target));
            if (existing >= 0) { selection.RemoveAt(existing); return; } // toggle off

            selection.Add(target);
            if (TryResolve()) return;

            // Invalid: restart the selection with just the latest tap, then re-check (handles K after a non-match).
            selection.Clear();
            selection.Add(target);
            if (!TryResolve()) selection.Clear();
        }

        /// <summary>Evaluates the current selection; applies removal on Match. Returns true unless Invalid.</summary>
        private bool TryResolve()
        {
            var cards = new List<PlayingCard>(selection.Count);
            foreach (var t in selection) cards.Add(CardOf(t));

            switch (Rule.Evaluate(cards))
            {
                case MatchVerdict.Incomplete:
                    return true;
                case MatchVerdict.Match:
                    ApplyRemoval();
                    selection.Clear();
                    return true;
                default:
                    return false; // Invalid
            }
        }

        private void ApplyRemoval()
        {
            PushUndo();

            var cellIds = new List<CellId>(selection.Count);
            bool removeWaste = false;
            foreach (var t in selection)
            {
                if (t.IsWaste) removeWaste = true;
                else cellIds.Add(t.Cell);
            }

            var next = CurrentState.WithCellsRemoved(cellIds);
            if (removeWaste) next = next.WithWasteTopRemoved();
            PublishState(next);
        }

        private SelectionSnapshot BuildSelectionSnapshot()
        {
            var cells = new List<CellId>(selection.Count);
            bool waste = false;
            foreach (var t in selection)
            {
                if (t.IsWaste) waste = true;
                else cells.Add(t.Cell);
            }
            return new SelectionSnapshot(cells, waste);
        }

        public override bool HasAnyMove(BoardState state)
        {
            if (state.Stock.Count > 0) return true;
            if (CanRecycle(state)) return true;
            return EnumerateMatches(state).Count > 0;
        }

        public override IReadOnlyList<BoardHint> GetHints(BoardState state)
        {
            var matches = EnumerateMatches(state);
            if (matches.Count > 0) return matches;
            if (state.Stock.Count > 0) return DrawHints;
            if (CanRecycle(state)) return RecycleHints;
            return Array.Empty<BoardHint>();
        }

        /// <summary>Every removable match among the free cells + waste-top, as highlight targets.</summary>
        private List<BoardHint> EnumerateMatches(BoardState state)
        {
            var cells = new List<CellId>();
            var cards = new List<PlayingCard>();
            foreach (var id in BoardRules.FreeCells(Layout, state))
            {
                cells.Add(id);
                cards.Add(state.CardAt(id));
            }
            bool hasWaste = state.WasteTop != null;
            if (hasWaste) cards.Add(state.WasteTop); // waste is the last candidate, index == cells.Count

            var result = new List<BoardHint>();
            var single = new PlayingCard[1];
            var pair = new PlayingCard[2];
            for (int i = 0; i < cards.Count; i++)
            {
                single[0] = cards[i];
                if (Rule.Evaluate(single) == MatchVerdict.Match)
                {
                    result.Add(BoardHint.OfMatch(TargetsFor(cells, i, -1, hasWaste)));
                    continue;
                }
                for (int j = i + 1; j < cards.Count; j++)
                {
                    pair[0] = cards[i];
                    pair[1] = cards[j];
                    if (Rule.Evaluate(pair) == MatchVerdict.Match)
                        result.Add(BoardHint.OfMatch(TargetsFor(cells, i, j, hasWaste)));
                }
            }
            return result;
        }

        private static SelectionSnapshot TargetsFor(List<CellId> cells, int a, int b, bool hasWaste)
        {
            var picked = new List<CellId>(2);
            bool waste = false;
            AddTarget(cells, a, hasWaste, picked, ref waste);
            if (b >= 0) AddTarget(cells, b, hasWaste, picked, ref waste);
            return new SelectionSnapshot(picked, waste);
        }

        private static void AddTarget(List<CellId> cells, int index, bool hasWaste, List<CellId> picked, ref bool waste)
        {
            if (hasWaste && index == cells.Count) waste = true;
            else picked.Add(cells[index]);
        }

        private readonly struct SelectedTarget : IEquatable<SelectedTarget>
        {
            public bool IsWaste { get; }
            public CellId Cell { get; }
            private SelectedTarget(bool isWaste, CellId cell) { IsWaste = isWaste; Cell = cell; }
            public static SelectedTarget OfCell(CellId id) => new SelectedTarget(false, id);
            public static SelectedTarget Waste() => new SelectedTarget(true, default);
            public bool Equals(SelectedTarget other) => IsWaste == other.IsWaste && Cell.Equals(other.Cell);
            public override bool Equals(object obj) => obj is SelectedTarget t && Equals(t);
            public override int GetHashCode() => System.HashCode.Combine(IsWaste, Cell);
        }
    }
}
```

- [ ] **Step 3c: Delete the old service**

```bash
git rm Assets/Scripts/Service/BoardGameService/BoardGameService.cs
```

(Removes the `.cs`; the `.meta` is removed with it. `BoardGameService` is a plain class — nothing references it by GUID.)

- [ ] **Step 3d: Update the DI registration in `BoardScene.cs`**

Change the line:
```csharp
            builder.Register<BoardGameService>(Lifetime.Scoped).As<IBoardGameService>();
```
to:
```csharp
            builder.Register<PyramidGameService>(Lifetime.Scoped).As<IBoardGameService>();
```
(Task 8 replaces this with the factory; this keeps the project compiling and Pyramid working in the interim.)

- [ ] **Step 4: Run to verify it passes**

Run: force-recompile snippet, poll `uloop compile` (Expected `ErrorCount: 0`), then `uloop run-tests --test-mode EditMode`.
Expected: ALL existing Pyramid service tests (now `PyramidGameServiceTests`) pass — the extraction changed no behavior. No regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/ Assets/Scripts/Scene/Board/BoardScene.cs Assets/Tests/EditMode/
git commit -m "refactor(board): extract BoardGameServiceBase, BoardGameService -> PyramidGameService"
```

---

## Task 5: `TriPeaksGameService`

Single-tap play onto the waste; no recycle; the deal flips the first waste card.

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/TriPeaksGameService.cs`
- Test: `Assets/Tests/EditMode/TriPeaksGameServiceTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using System.Linq;
using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksGameServiceTests
    {
        private sealed class FixedShuffle : IShuffleStrategy
        {
            private readonly List<PlayingCard> deck;
            public FixedShuffle(params PlayingCard[] cards) => deck = new List<PlayingCard>(cards);
            public List<PlayingCard> Shuffle(int seed) => new List<PlayingCard>(deck);
        }

        private static PlayingCard Card(Rank r) => new PlayingCard(r, Suit.Spade);

        // A flat n-cell layout (no covers) so every cell is free — lets us drive plays directly.
        private static BoardLayout FlatLayout(int n)
        {
            var cells = new List<BoardCell>(n);
            for (int i = 0; i < n; i++) cells.Add(new BoardCell(new CellId(i), null));
            return new BoardLayout(GameType.TriPeaks, 1, cells);
        }

        [Test]
        public void Initialize_FlipsFirstStockCardToWaste()
        {
            // 1 cell (Seven) + stock [Eight, Nine]; deal should flip Nine? No — top of stock list is index 0.
            // Deal order: cell gets deck[0]=Seven; stock = [Eight, Nine]; WithStockDrawn pops the LAST (Nine).
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Seven), Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);

            Assert.AreEqual(1, svc.CurrentState.Stock.Count, "one card left in stock after the deal flip");
            Assert.IsNotNull(svc.CurrentState.WasteTop);
            Assert.AreEqual(Rank.Nine, svc.CurrentState.WasteTop.Rank);
        }

        [Test]
        public void SelectCell_PlayableCard_MovesItToWaste_AndClearsCell()
        {
            // cell0 = Eight; waste-top after deal = Nine (8 and 9 are adjacent → playable).
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            Assert.AreEqual(Rank.Nine, svc.CurrentState.WasteTop.Rank);

            svc.SelectCell(new CellId(0)); // 8 plays on 9

            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)), "cell cleared");
            Assert.AreEqual(Rank.Eight, svc.CurrentState.WasteTop.Rank, "played card is new waste-top");
            Assert.IsTrue(svc.IsWon(svc.CurrentState), "all cells cleared");
        }

        [Test]
        public void SelectCell_NonPlayableCard_IsIgnored()
        {
            // cell0 = Five; waste-top = Nine (not adjacent) → ignored.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);

            svc.SelectCell(new CellId(0));
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)), "non-adjacent card stays");
        }

        [Test]
        public void SelectWasteTop_IsNoOp()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            var before = svc.CurrentState;
            svc.SelectWasteTop();
            Assert.AreSame(before, svc.CurrentState);
        }

        [Test]
        public void CanRecycle_IsAlwaysFalse_NoSecondPass()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Nine), Card(Rank.Two)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0, maxRecycles: 0);
            svc.DrawFromStock(); // exhaust the stock onto the waste
            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            Assert.IsFalse(svc.CanRecycle(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_TrueWhenAFreeCellIsPlayable()
        {
            // cell0 = Eight, waste-top = Nine → playable.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            Assert.IsTrue(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_FalseWhenStuck()
        {
            // cell0 = Five, waste-top = Nine, stock empty → no move.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            Assert.IsFalse(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_TrueWhenStockRemains()
        {
            // cell0 = Five (not adjacent to Nine), but stock still has a card → draw is a move.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Two), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            Assert.IsTrue(svc.CurrentState.Stock.Count > 0);
            Assert.IsTrue(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void Undo_RevertsAPlay()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            svc.SelectCell(new CellId(0)); // play 8
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)));

            svc.Undo();
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)), "play reverted");
            Assert.AreEqual(Rank.Nine, svc.CurrentState.WasteTop.Rank, "waste-top restored");
        }

        [Test]
        public void GetHints_PlayableFreeCell_ReturnsMatch()
        {
            // cell0 = Eight (playable on Nine), cell1 = Two (not). Expect one Match hint on cell 0.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Two), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(2), new TriPeaksMatchRule(), seed: 0);
            // After deal: cells [Eight, Two], stock had [Two?, Nine] — re-derive: deck = E,2,9 → cells=E,2; stock=[9]; flip → waste=9, stock empty.
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Match, hints[0].Kind);
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(0)));
        }

        [Test]
        public void GetHints_NoPlayButStock_ReturnsDraw()
        {
            // cells [Five, Two] (neither adjacent to waste Nine), stock still has a card.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Two), Card(Rank.Nine), Card(Rank.King)));
            svc.Initialize(FlatLayout(2), new TriPeaksMatchRule(), seed: 0);
            // deck = 5,2,9,K → cells=5,2; stock=[9,K]; flip pops K → waste=K, stock=[9].
            // 5 vs K: not adjacent; 2 vs K: not adjacent → no play; stock has [9] → Draw.
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Draw, hints[0].Kind);
        }

        [Test]
        public void GetHints_StuckReturnsEmpty_NeverRecycle()
        {
            // cells [Five, Two], waste Nine, stock empty → stuck (no recycle in TriPeaks).
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Two), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(2), new TriPeaksMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(0, hints.Count);
        }

        [Test]
        public void Restore_RoundTripsState()
        {
            var layout = TriPeaksLayoutFactory.Create();
            var rule = new TriPeaksMatchRule();
            var source = new TriPeaksGameService(new FisherYatesShuffleStrategy());
            source.Initialize(layout, rule, seed: 42);
            var snapshotState = source.CurrentState;

            var svc = new TriPeaksGameService(new FisherYatesShuffleStrategy());
            svc.Restore(layout, rule, seed: 42, state: snapshotState, undoHistory: new List<BoardState>());

            Assert.AreEqual(42, svc.CurrentSeed);
            Assert.IsTrue(svc.CurrentState.Equals(snapshotState));
            Assert.IsFalse(svc.CanUndo);
        }
    }
}
```

> Note for the implementer: `WithStockDrawn()` pops the **last** element of the stock list (see `BoardState.WithStockDrawn`). The `FixedShuffle` deck order therefore determines the waste-top after the deal flip — the test comments trace it. Verify each `FixedShuffle` ordering against that pop-from-end behavior before asserting; adjust the expected waste-top rank if your trace differs, but keep the asserted *playability* relationship intact.

- [ ] **Step 2: Run to verify it fails**

Run: `uloop run-tests --test-mode EditMode`
Expected: FAIL — `TriPeaksGameService` not defined.

- [ ] **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using Service.GameService;

namespace Service.BoardGameService
{
    /// <summary>TriPeaks: tap one free tableau card to play it onto the waste-top when their ranks are
    /// adjacent (A↔K wraps). No multi-tap accumulation, no recycle. The deal flips the first stock card
    /// to the waste so play has an anchor.</summary>
    public sealed class TriPeaksGameService : BoardGameServiceBase
    {
        private static readonly IReadOnlyList<BoardHint> DrawHints = new[] { BoardHint.Draw };
        private readonly PlayingCard[] pair = new PlayingCard[2];

        public TriPeaksGameService(IShuffleStrategy shuffle) : base(shuffle) { }

        protected override void ResetSelectionState() { } // no pending-selection accumulator

        protected override void OnDealt()
        {
            // Flip the first stock card to the waste so the player has a card to build on.
            if (CurrentState.Stock.Count > 0)
                CurrentState = CurrentState.WithStockDrawn();
        }

        public override void SelectCell(CellId id)
        {
            if (!BoardRules.IsFree(Layout, CurrentState, id)) return;
            var top = CurrentState.WasteTop;
            if (top == null) return;
            if (!IsPlayable(CurrentState.CardAt(id), top)) return;

            PushUndo();
            PublishState(CurrentState.WithCardPlayedToWaste(id));
            EmitSelection(SelectionSnapshot.Empty);
        }

        public override void SelectWasteTop() { } // the waste-top is the anchor, never a tap target

        public override bool HasAnyMove(BoardState state)
        {
            if (state.Stock.Count > 0) return true;
            var top = state.WasteTop;
            if (top == null) return false;
            foreach (var id in BoardRules.FreeCells(Layout, state))
                if (IsPlayable(state.CardAt(id), top)) return true;
            return false;
        }

        public override IReadOnlyList<BoardHint> GetHints(BoardState state)
        {
            var top = state.WasteTop;
            var result = new List<BoardHint>();
            if (top != null)
            {
                foreach (var id in BoardRules.FreeCells(Layout, state))
                    if (IsPlayable(state.CardAt(id), top))
                        result.Add(BoardHint.OfMatch(new SelectionSnapshot(new[] { id }, false)));
            }
            if (result.Count > 0) return result;
            if (state.Stock.Count > 0) return DrawHints;
            return Array.Empty<BoardHint>(); // no recycle: an empty stock with no play is stuck
        }

        private bool IsPlayable(PlayingCard card, PlayingCard wasteTop)
        {
            pair[0] = wasteTop;
            pair[1] = card;
            return Rule.Evaluate(pair) == MatchVerdict.Match;
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: force-recompile, poll `uloop compile`, then `uloop run-tests --test-mode EditMode`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/TriPeaksGameService.cs Assets/Tests/EditMode/TriPeaksGameServiceTests.cs
git commit -m "feat(board): TriPeaksGameService play-onto-waste, single pass"
```

---

## Task 6: `ITriPeaksScoreRule` + `TriPeaksScoreRule`

The MSSC scoring constants (tunable).

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/ITriPeaksScoreRule.cs`
- Create: `Assets/Scripts/Service/BoardGameService/TriPeaksScoreRule.cs`
- Test: `Assets/Tests/EditMode/TriPeaksScoreRuleTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksScoreRuleTests
    {
        [Test]
        public void PointsForStreak_Is50TimesStreak_ByDefault()
        {
            var rule = new TriPeaksScoreRule();
            Assert.AreEqual(50, rule.PointsForStreak(1));
            Assert.AreEqual(100, rule.PointsForStreak(2));
            Assert.AreEqual(500, rule.PointsForStreak(10));
            Assert.AreEqual(0, rule.PointsForStreak(0));
        }

        [Test]
        public void PeakBonus_Is500_1000_5000_ByOrdinal()
        {
            var rule = new TriPeaksScoreRule();
            Assert.AreEqual(500, rule.PeakBonus(1));
            Assert.AreEqual(1000, rule.PeakBonus(2));
            Assert.AreEqual(5000, rule.PeakBonus(3));
            Assert.AreEqual(0, rule.PeakBonus(4)); // out of range
            Assert.AreEqual(0, rule.PeakBonus(0));
        }

        [Test]
        public void StockDrawPenalty_IsMinusFive_ByDefault()
        {
            Assert.AreEqual(-5, new TriPeaksScoreRule().StockDrawPenalty);
        }

        [Test]
        public void CustomConstants_AreHonored()
        {
            var rule = new TriPeaksScoreRule(pointsPerStreakStep: 10, stockDrawPenalty: -1,
                firstPeakBonus: 1, secondPeakBonus: 2, thirdPeakBonus: 3);
            Assert.AreEqual(30, rule.PointsForStreak(3));
            Assert.AreEqual(-1, rule.StockDrawPenalty);
            Assert.AreEqual(2, rule.PeakBonus(2));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails** — `uloop run-tests --test-mode EditMode`. Expected FAIL (types undefined).

- [ ] **Step 3a: Implement `ITriPeaksScoreRule.cs`**

```csharp
namespace Service.BoardGameService
{
    /// <summary>TriPeaks scoring (MSSC). A run is the consecutive cards cleared with no stock draw
    /// between them; the streak counter that feeds <see cref="PointsForStreak"/> lives in the scorer,
    /// not here. These are plain, stateless values (mirrors IBoardScoreRule's role for Pyramid).</summary>
    public interface ITriPeaksScoreRule
    {
        /// <summary>Points for clearing the card at this 1-based position in the current run (default 50×streak).</summary>
        int PointsForStreak(int streak);

        /// <summary>Bonus for clearing the Nth peak tip (1-based ordinal); default 500 / 1000 / 5000.</summary>
        int PeakBonus(int peakOrdinal);

        /// <summary>Penalty per stock draw (default -5); the draw also resets the run.</summary>
        int StockDrawPenalty { get; }
    }
}
```

- [ ] **Step 3b: Implement `TriPeaksScoreRule.cs`**

```csharp
namespace Service.BoardGameService
{
    /// <summary>Microsoft-Solitaire-Collection TriPeaks scoring with tunable constants.</summary>
    public sealed class TriPeaksScoreRule : ITriPeaksScoreRule
    {
        private readonly int pointsPerStreakStep;
        private readonly int[] peakBonuses;

        public TriPeaksScoreRule(int pointsPerStreakStep = 50, int stockDrawPenalty = -5,
            int firstPeakBonus = 500, int secondPeakBonus = 1000, int thirdPeakBonus = 5000)
        {
            this.pointsPerStreakStep = pointsPerStreakStep;
            StockDrawPenalty = stockDrawPenalty;
            peakBonuses = new[] { firstPeakBonus, secondPeakBonus, thirdPeakBonus };
        }

        public int PointsForStreak(int streak) => streak <= 0 ? 0 : pointsPerStreakStep * streak;

        public int PeakBonus(int peakOrdinal)
            => (peakOrdinal >= 1 && peakOrdinal <= peakBonuses.Length) ? peakBonuses[peakOrdinal - 1] : 0;

        public int StockDrawPenalty { get; }
    }
}
```

- [ ] **Step 4: Run to verify it passes** — `uloop run-tests --test-mode EditMode`. Expected PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/ITriPeaksScoreRule.cs Assets/Scripts/Service/BoardGameService/TriPeaksScoreRule.cs Assets/Tests/EditMode/TriPeaksScoreRuleTests.cs
git commit -m "feat(board): TriPeaksScoreRule MSSC scoring constants"
```

---

## Task 7: `IBoardScorer` + `PyramidScorer` + `TriPeaksScorer`

The stateful scoring interface and both implementations.

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/IBoardScorer.cs` (interface + `BoardScoreOutcome` + `BoardScoreEvent`)
- Create: `Assets/Scripts/Service/BoardGameService/PyramidScorer.cs`
- Create: `Assets/Scripts/Service/BoardGameService/TriPeaksScorer.cs`
- Test: `Assets/Tests/EditMode/PyramidScorerTests.cs`
- Test: `Assets/Tests/EditMode/TriPeaksScorerTests.cs`

- [ ] **Step 1: Write the failing tests**

`PyramidScorerTests.cs`:

```csharp
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PyramidScorerTests
    {
        private static PlayingCard C(Rank r) => new PlayingCard(r, Suit.Spade);

        // 2-cell board with a stock and waste we control directly.
        private static BoardState State(PlayingCard c0, PlayingCard c1,
            IReadOnlyList<PlayingCard> stock, IReadOnlyList<PlayingCard> waste, int recycle = 0)
            => new BoardState(new[] { c0, c1 }, stock, waste, recycle);

        private static PyramidScorer NewScorer() => new PyramidScorer(new PyramidScoreRule());

        [Test]
        public void Removal_ScoresPerCard_AndEventIsCleared()
        {
            var scorer = NewScorer();
            var prev = State(C(Rank.Nine), C(Rank.Four), stock: null, waste: null);
            var next = prev.WithCellsRemoved(new[] { new CellId(0), new CellId(1) }); // 2 cards gone
            scorer.Reset(prev);

            var outcome = scorer.Evaluate(prev, next, won: false);
            Assert.AreEqual(BoardScoreEvent.Cleared, outcome.Event);
            Assert.AreEqual(10, outcome.Points); // perCard 5 × 2
        }

        [Test]
        public void Removal_OnWin_AddsBoardClearedBonus()
        {
            var scorer = NewScorer();
            var prev = State(C(Rank.King), C(Rank.King), stock: null, waste: null);
            var afterFirst = prev.WithCellsRemoved(new[] { new CellId(0) });
            var next = afterFirst.WithCellsRemoved(new[] { new CellId(1) }); // board now empty
            scorer.Reset(afterFirst);

            var outcome = scorer.Evaluate(afterFirst, next, won: true);
            Assert.AreEqual(BoardScoreEvent.Cleared, outcome.Event);
            Assert.AreEqual(5 + 100, outcome.Points); // 1 card × 5 + boardClearedBonus 100
        }

        [Test]
        public void StockDraw_ScoresDrawPenalty_AndEventIsDraw()
        {
            var scorer = NewScorer();
            var prev = State(C(Rank.Nine), C(Rank.Four), stock: new[] { C(Rank.Seven) }, waste: null);
            var next = prev.WithStockDrawn();
            scorer.Reset(prev);

            var outcome = scorer.Evaluate(prev, next, won: false);
            Assert.AreEqual(BoardScoreEvent.Draw, outcome.Event);
            Assert.AreEqual(-2, outcome.Points);
        }

        [Test]
        public void Recycle_ScoresRecyclePenalty_AndEventIsRecycle()
        {
            var scorer = NewScorer();
            var prev = State(C(Rank.Nine), C(Rank.Four), stock: null, waste: new[] { C(Rank.Seven) });
            var next = prev.WithStockRecycled(); // waste→stock, RecycleCount++
            scorer.Reset(prev);

            var outcome = scorer.Evaluate(prev, next, won: false);
            Assert.AreEqual(BoardScoreEvent.Recycle, outcome.Event);
            Assert.AreEqual(-10, outcome.Points);
        }
    }
}
```

`TriPeaksScorerTests.cs`:

```csharp
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksScorerTests
    {
        private static PlayingCard C(Rank r) => new PlayingCard(r, Suit.Spade);

        // 3-cell board; treat CellId 0 as an apex for peak-bonus tests.
        private static BoardState State(PlayingCard[] cells,
            IReadOnlyList<PlayingCard> stock, IReadOnlyList<PlayingCard> waste)
            => new BoardState(cells, stock, waste);

        private static TriPeaksScorer NewScorer(params int[] apex)
        {
            var set = new List<CellId>();
            foreach (var a in apex) set.Add(new CellId(a));
            return new TriPeaksScorer(new TriPeaksScoreRule(), set);
        }

        [Test]
        public void Play_FirstCard_Scores50_StreakOne()
        {
            var scorer = NewScorer(); // no apex
            var prev = State(new[] { C(Rank.Eight), C(Rank.Two), C(Rank.Three) },
                stock: null, waste: new[] { C(Rank.Nine) });
            var next = prev.WithCardPlayedToWaste(new CellId(0)); // 8 onto 9
            scorer.Reset(prev);

            var outcome = scorer.Evaluate(prev, next, won: false);
            Assert.AreEqual(BoardScoreEvent.Cleared, outcome.Event);
            Assert.AreEqual(50, outcome.Points);
        }

        [Test]
        public void Play_ConsecutiveCards_EscalateStreak()
        {
            var scorer = NewScorer();
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Seven), C(Rank.Three) },
                stock: null, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);

            var s1 = s0.WithCardPlayedToWaste(new CellId(0)); // 8 → streak 1 = 50
            Assert.AreEqual(50, scorer.Evaluate(s0, s1, false).Points);

            var s2 = s1.WithCardPlayedToWaste(new CellId(1)); // 7 → streak 2 = 100
            Assert.AreEqual(100, scorer.Evaluate(s1, s2, false).Points);
        }

        [Test]
        public void StockDraw_ResetsStreak_AndScoresMinusFive()
        {
            var scorer = NewScorer();
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Seven), C(Rank.Three) },
                stock: new[] { C(Rank.King) }, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);

            var s1 = s0.WithCardPlayedToWaste(new CellId(0)); // streak → 1
            scorer.Evaluate(s0, s1, false);

            var s2 = s1.WithStockDrawn(); // draw → reset, -5
            var drawOutcome = scorer.Evaluate(s1, s2, false);
            Assert.AreEqual(BoardScoreEvent.Draw, drawOutcome.Event);
            Assert.AreEqual(-5, drawOutcome.Points);

            var s3 = s2.WithCardPlayedToWaste(new CellId(1)); // next play → streak back to 1 = 50
            Assert.AreEqual(50, scorer.Evaluate(s2, s3, false).Points);
        }

        [Test]
        public void Play_ApexCell_AddsPeakBonus_ByOrdinal()
        {
            var scorer = NewScorer(0, 1); // cells 0 and 1 are apexes
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Eight), C(Rank.Three) },
                stock: null, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);

            var s1 = s0.WithCardPlayedToWaste(new CellId(0)); // apex #1 → 50 + 500
            var o1 = scorer.Evaluate(s0, s1, false);
            Assert.AreEqual(50 + 500, o1.Points);

            var s2 = s1.WithCardPlayedToWaste(new CellId(1)); // streak 2 (100) + apex #2 (1000)
            var o2 = scorer.Evaluate(s1, s2, false);
            Assert.AreEqual(100 + 1000, o2.Points);
        }

        [Test]
        public void Reset_DerivesPeaksClearedFromAlreadyEmptyApexes()
        {
            var scorer = NewScorer(0, 1, 2);
            // Start state with apex 0 already cleared → next apex play should be the 2nd peak bonus.
            var start = State(new[] { (PlayingCard)null, C(Rank.Eight), C(Rank.Five) },
                stock: null, waste: new[] { C(Rank.Nine) });
            scorer.Reset(start); // peaksCleared seeded to 1

            var next = start.WithCardPlayedToWaste(new CellId(1)); // apex #2 → 50 + 1000
            var outcome = scorer.Evaluate(start, next, false);
            Assert.AreEqual(50 + 1000, outcome.Points);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails** — `uloop run-tests --test-mode EditMode`. Expected FAIL (types undefined).

- [ ] **Step 3a: Implement `IBoardScorer.cs`**

```csharp
using Model.Board;

namespace Service.BoardGameService
{
    /// <summary>What kind of move a board state-transition was — drives the move sound.</summary>
    public enum BoardScoreEvent { None, Cleared, Draw, Recycle }

    /// <summary>Points to apply plus the move kind for a single state transition.</summary>
    public readonly struct BoardScoreOutcome
    {
        public int Points { get; }
        public BoardScoreEvent Event { get; }
        public BoardScoreOutcome(int points, BoardScoreEvent ev) { Points = points; Event = ev; }
    }

    /// <summary>Stateful, game-specific scorer. Turns a (prev → next) board transition into points and a
    /// move kind. The presenter calls <see cref="Reset"/> at deal/restore, then <see cref="Evaluate"/> on
    /// each state change (skipping undo). Accumulators (e.g. TriPeaks streak) live here, not in the
    /// immutable score rule.</summary>
    public interface IBoardScorer
    {
        void Reset(BoardState initial);
        BoardScoreOutcome Evaluate(BoardState prev, BoardState next, bool won);
    }
}
```

- [ ] **Step 3b: Implement `PyramidScorer.cs`**

```csharp
using Model.Board;

namespace Service.BoardGameService
{
    /// <summary>Pyramid scoring driven by the total-card delta: a positive delta is a removal (per-card
    /// points, plus the clear bonus on a win); otherwise a recycle or a stock draw. Stateless — reads
    /// everything from the supplied (prev, next), so the presenter owns the prior-state source of truth.</summary>
    public sealed class PyramidScorer : IBoardScorer
    {
        private readonly IBoardScoreRule rule;
        public PyramidScorer(IBoardScoreRule rule) { this.rule = rule; }

        public void Reset(BoardState initial) { } // stateless

        public BoardScoreOutcome Evaluate(BoardState prev, BoardState next, bool won)
        {
            int removed = TotalCards(prev) - TotalCards(next);
            if (removed > 0)
            {
                int pts = rule.ScoreForRemoval(removed);
                if (won) pts += rule.BoardClearedBonus;
                return new BoardScoreOutcome(pts, BoardScoreEvent.Cleared);
            }
            if (next.RecycleCount != prev.RecycleCount)
                return new BoardScoreOutcome(rule.ScoreForRecycle, BoardScoreEvent.Recycle);
            return new BoardScoreOutcome(rule.ScoreForStockDraw, BoardScoreEvent.Draw);
        }

        private static int TotalCards(BoardState s)
        {
            int occ = 0;
            foreach (var _ in s.OccupiedCells()) occ++;
            return occ + s.Stock.Count + s.Waste.Count;
        }
    }
}
```

- [ ] **Step 3c: Implement `TriPeaksScorer.cs`**

```csharp
using System.Collections.Generic;
using Model.Board;

namespace Service.BoardGameService
{
    /// <summary>TriPeaks scoring. A play (one cell cleared, waste +1) scores 50×streak with a peak bonus
    /// when an apex clears; a stock draw (stock −1, waste +1, occupancy unchanged) resets the streak and
    /// scores the draw penalty. Holds the streak + peaks-cleared counters; <see cref="Reset"/> derives
    /// peaks-cleared from any apexes already empty in the start state (correct on a resumed game).</summary>
    public sealed class TriPeaksScorer : IBoardScorer
    {
        private readonly ITriPeaksScoreRule rule;
        private readonly HashSet<CellId> apex;
        private int streak;
        private int peaksCleared;

        public TriPeaksScorer(ITriPeaksScoreRule rule, IEnumerable<CellId> apexCells)
        {
            this.rule = rule;
            apex = new HashSet<CellId>(apexCells);
        }

        public void Reset(BoardState initial)
        {
            streak = 0;
            peaksCleared = 0;
            foreach (var id in apex)
                if (id.Value < initial.CellCount && !initial.HasCard(id)) peaksCleared++;
        }

        public BoardScoreOutcome Evaluate(BoardState prev, BoardState next, bool won)
        {
            int prevOcc = OccupiedCount(prev);
            int nextOcc = OccupiedCount(next);

            // A play: exactly one cell cleared and the waste grew by one.
            if (nextOcc == prevOcc - 1 && next.Waste.Count == prev.Waste.Count + 1)
            {
                streak++;
                int pts = rule.PointsForStreak(streak);
                var removed = FindRemovedCell(prev, next);
                if (removed.HasValue && apex.Contains(removed.Value))
                {
                    peaksCleared++;
                    pts += rule.PeakBonus(peaksCleared);
                }
                return new BoardScoreOutcome(pts, BoardScoreEvent.Cleared);
            }

            // A deal: stock shrank by one, waste grew by one, occupancy unchanged.
            if (nextOcc == prevOcc && next.Stock.Count == prev.Stock.Count - 1
                && next.Waste.Count == prev.Waste.Count + 1)
            {
                streak = 0;
                return new BoardScoreOutcome(rule.StockDrawPenalty, BoardScoreEvent.Draw);
            }

            return new BoardScoreOutcome(0, BoardScoreEvent.None);
        }

        private static int OccupiedCount(BoardState s)
        {
            int n = 0;
            foreach (var _ in s.OccupiedCells()) n++;
            return n;
        }

        private static CellId? FindRemovedCell(BoardState prev, BoardState next)
        {
            for (int i = 0; i < prev.CellCount; i++)
            {
                var id = new CellId(i);
                if (prev.HasCard(id) && !next.HasCard(id)) return id;
            }
            return null;
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes** — force-recompile, poll `uloop compile`, then `uloop run-tests --test-mode EditMode`. Expected PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/IBoardScorer.cs Assets/Scripts/Service/BoardGameService/PyramidScorer.cs Assets/Scripts/Service/BoardGameService/TriPeaksScorer.cs Assets/Tests/EditMode/PyramidScorerTests.cs Assets/Tests/EditMode/TriPeaksScorerTests.cs
git commit -m "feat(board): IBoardScorer with PyramidScorer and TriPeaksScorer"
```

---

# PHASE 2 — PRESENTER & DI

## Task 8: `IBoardGameServiceFactory` + DI registration

Register both services and a factory that picks one by `GameType`.

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/IBoardGameServiceFactory.cs`
- Create: `Assets/Scripts/Service/BoardGameService/BoardGameServiceFactory.cs`
- Modify: `Assets/Scripts/Scene/Board/BoardScene.cs`

- [ ] **Step 1: Implement the factory interface + impl**

`IBoardGameServiceFactory.cs`:

```csharp
using Model.Game;

namespace Service.BoardGameService
{
    /// <summary>Resolves the board game service for a given <see cref="GameType"/> (Pyramid / TriPeaks).
    /// Both services are registered per-scene; the presenter creates the one matching the route.</summary>
    public interface IBoardGameServiceFactory
    {
        IBoardGameService Create(GameType gameType);
    }
}
```

`BoardGameServiceFactory.cs`:

```csharp
using Model.Game;

namespace Service.BoardGameService
{
    public sealed class BoardGameServiceFactory : IBoardGameServiceFactory
    {
        private readonly PyramidGameService pyramid;
        private readonly TriPeaksGameService triPeaks;

        public BoardGameServiceFactory(PyramidGameService pyramid, TriPeaksGameService triPeaks)
        {
            this.pyramid = pyramid;
            this.triPeaks = triPeaks;
        }

        public IBoardGameService Create(GameType gameType)
            => gameType == GameType.TriPeaks ? triPeaks : pyramid;
    }
}
```

- [ ] **Step 2: Update `BoardScene.Configure`**

Replace:
```csharp
            builder.Register<FisherYatesShuffleStrategy>(Lifetime.Scoped).As<IShuffleStrategy>();
            builder.Register<PyramidGameService>(Lifetime.Scoped).As<IBoardGameService>();
            builder.Register<SessionStatsService>(Lifetime.Scoped).As<ISessionStatsService>();
```
with:
```csharp
            builder.Register<FisherYatesShuffleStrategy>(Lifetime.Scoped).As<IShuffleStrategy>();
            builder.Register<PyramidGameService>(Lifetime.Scoped);
            builder.Register<TriPeaksGameService>(Lifetime.Scoped);
            builder.Register<BoardGameServiceFactory>(Lifetime.Scoped).As<IBoardGameServiceFactory>();
            builder.Register<SessionStatsService>(Lifetime.Scoped).As<ISessionStatsService>();
```
(Each concrete service is registered as itself so the factory can ctor-inject both; neither is registered `As<IBoardGameService>` anymore — the presenter resolves via the factory.)

- [ ] **Step 3: Compile** — force-recompile, poll `uloop compile`. Expected `ErrorCount: 0`. (Presenter still injects `IBoardGameService` directly — that resolution now fails at runtime, fixed in Task 9; the project still *compiles*.)

> Because the presenter's `[Inject] IBoardGameService` no longer resolves, do NOT enter Play mode between Task 8 and Task 9. EditMode tests are unaffected.

- [ ] **Step 4: Run tests** — `uloop run-tests --test-mode EditMode`. Expected: PASS (no scene resolution in EditMode).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/IBoardGameServiceFactory.cs Assets/Scripts/Service/BoardGameService/BoardGameServiceFactory.cs Assets/Scripts/Scene/Board/BoardScene.cs
git commit -m "feat(board): IBoardGameServiceFactory + register both board services"
```

---

## Task 9: `BoardPresenter` — TriPeaks case, service factory, `IBoardScorer` scoring

**Files:**
- Modify: `Assets/Scripts/Scene/Board/BoardPresenter.cs`

- [ ] **Step 1: Swap the service injection for the factory**

Change line 39 from:
```csharp
        [Inject] private IBoardGameService BoardGameService { get; set; }
```
to (drop `[Inject]`, add the factory):
```csharp
        private IBoardGameService BoardGameService { get; set; } // resolved per-init from the factory
        [Inject] private IBoardGameServiceFactory BoardGameServiceFactory { get; set; }
```

- [ ] **Step 2: Replace the scorer/total fields**

Change:
```csharp
        private GameType currentGameType;
        private IBoardScoreRule scoreRule;
        private int prevTotal;
        private int prevRecycleCount;
```
to:
```csharp
        private GameType currentGameType;
        private IBoardScorer scorer;
        private BoardState prevState;
```

- [ ] **Step 3: Resolve the service + scorer in the init switch**

In `InitializeGameAsync`, replace the switch block (currently lines ~113–125):
```csharp
            BoardLayout layout;
            IBoardMatchRule matchRule;
            int maxRecycles;
            switch (currentGameType)
            {
                case GameType.Pyramid:
                default:
                    layout = PyramidLayoutFactory.Create(variant);
                    matchRule = new PyramidMatchRule();
                    scoreRule = new PyramidScoreRule();
                    maxRecycles = 3; // MS-style: recycle the waste back into the stock up to 3 times
                    break;
            }
```
with:
```csharp
            BoardGameService = BoardGameServiceFactory.Create(currentGameType);

            BoardLayout layout;
            IBoardMatchRule matchRule;
            int maxRecycles;
            switch (currentGameType)
            {
                case GameType.TriPeaks:
                    layout = TriPeaksLayoutFactory.Create(variant);
                    matchRule = new TriPeaksMatchRule();
                    scorer = new TriPeaksScorer(new TriPeaksScoreRule(), TriPeaksLayoutFactory.ApexCellIds);
                    maxRecycles = 0; // single pass, no recycle
                    break;
                case GameType.Pyramid:
                default:
                    layout = PyramidLayoutFactory.Create(variant);
                    matchRule = new PyramidMatchRule();
                    scorer = new PyramidScorer(new PyramidScoreRule());
                    maxRecycles = 3; // MS-style: recycle the waste back into the stock up to 3 times
                    break;
            }
```

- [ ] **Step 4: Seed `prevState` + the scorer after the initial render**

In `InitializeGameAsync`, replace (currently lines ~173–176):
```csharp
            prevTotal = TotalCards(state);
            prevRecycleCount = state.RecycleCount;
            currentHints = null;
            hintIndex = 0;
```
with:
```csharp
            prevState = state;
            scorer.Reset(state);
            currentHints = null;
            hintIndex = 0;
```

- [ ] **Step 5: Rewrite the scoring branch in `OnBoardStateChanged`**

Replace the body from `int newTotal = TotalCards(next);` through the end of the scoring `if` (currently lines ~229–254):
```csharp
            int newTotal = TotalCards(next);
            int removed = prevTotal - newTotal;
            prevTotal = newTotal;
            bool recycled = next.RecycleCount != prevRecycleCount;
            prevRecycleCount = next.RecycleCount;

            bool won = BoardGameService.IsWon(next);

            if (!SessionStats.Current.IsFinished && !undoInProgress)
            {
                if (removed > 0)
                {
                    int pts = scoreRule.ScoreForRemoval(removed);
                    if (won) pts += scoreRule.BoardClearedBonus;
                    SessionStats.RecordScoreDelta(pts);
                    AudioService.Play(AudioCatalog.Card.FoundationPlace);
                }
                else if (removed == 0)
                {
                    // No cards left the board: a stock draw or a waste→stock recycle. Each costs points
                    // (an efficiency penalty — recycle costs more), floored at 0 by RecordScoreDelta;
                    // an undo refunds it via the single-level lastScoreDelta. Excluded during undo.
                    SessionStats.RecordScoreDelta(recycled ? scoreRule.ScoreForRecycle : scoreRule.ScoreForStockDraw);
                    AudioService.Play(recycled ? AudioCatalog.Card.Refresh : AudioCatalog.Card.Flip);
                }
            }
```
with:
```csharp
            bool won = BoardGameService.IsWon(next);

            if (!SessionStats.Current.IsFinished && !undoInProgress)
            {
                // The scorer interprets the transition in game-specific terms (Pyramid: cards removed;
                // TriPeaks: a play-to-waste streak or a draw). Penalties are floored at 0 by
                // RecordScoreDelta; an undo refunds via the single-level lastScoreDelta and is excluded
                // here by the undoInProgress guard.
                var outcome = scorer.Evaluate(prevState, next, won);
                if (outcome.Points != 0) SessionStats.RecordScoreDelta(outcome.Points);
                PlayMoveSound(outcome.Event);
            }
            prevState = next; // advance the diff base even when scoring is skipped (finished / undo)
```

- [ ] **Step 6: Add the sound helper; remove the dead `TotalCards`**

Add this method (e.g. just after `OnBoardStateChanged`):
```csharp
        private void PlayMoveSound(BoardScoreEvent ev)
        {
            switch (ev)
            {
                case BoardScoreEvent.Cleared: AudioService.Play(AudioCatalog.Card.FoundationPlace); break;
                case BoardScoreEvent.Recycle: AudioService.Play(AudioCatalog.Card.Refresh); break;
                case BoardScoreEvent.Draw: AudioService.Play(AudioCatalog.Card.Flip); break;
            }
        }
```
Delete the now-unused `private static int TotalCards(BoardState s) { ... }` helper (currently lines ~316–321).

- [ ] **Step 7: Compile + regression**

Run: force-recompile, poll `uloop compile` (Expected `ErrorCount: 0`), then `uloop run-tests --test-mode EditMode`.
Expected: PASS, no regressions.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Scene/Board/BoardPresenter.cs
git commit -m "feat(board): presenter TriPeaks case + IBoardScorer scoring path"
```

---

# PHASE 3 — SCENE / EDITOR (Unity editor work + play-verification)

> These tasks edit Unity assets (prefabs, scene, ScriptableObjects, localization) — they are NOT pure-C# TDD. Do them in the editor; the verification is in-editor play, not NUnit. After each, run `uloop compile` to confirm no errors, and re-run the EditMode suite once at the end to confirm no C# regressions. Mirror the existing `PyramidBoard.prefab` / `BoardScene.unity` setup wherever possible.

## Task 10: `BoardAnchorSet` + `UIBoardController` anchor-set refactor + presenter call; re-wire Pyramid

**Files:**
- Create: `Assets/Scripts/Component/Board/BoardAnchorSet.cs`
- Modify: `Assets/Scripts/Component/Board/UIBoardController.cs`
- Modify: `Assets/Scripts/Scene/Board/BoardPresenter.cs` (one `UseAnchorSet` call)
- Editor: re-wire the existing Pyramid anchors into a `BoardAnchorSet` in the scene/prefab.

- [ ] **Step 1: Create `BoardAnchorSet.cs`**

```csharp
using System.Collections.Generic;
using Model.Game;
using UnityEngine;

namespace Component.Board
{
    /// <summary>The fixed render anchors for one board layout (a Pyramid board or a TriPeaks board).
    /// Element index = CellId.Value. The scene holds one per board game; <see cref="UIBoardController"/>
    /// renders against the active set chosen by game type.</summary>
    public sealed class BoardAnchorSet : MonoBehaviour
    {
        [SerializeField] private GameType gameType = GameType.Pyramid;
        [SerializeField] private RectTransform[] cellAnchors;
        [SerializeField] private RectTransform stockAnchor;
        [SerializeField] private RectTransform wasteAnchor;

        public GameType GameType => gameType;
        public RectTransform StockAnchor => stockAnchor;
        public RectTransform WasteAnchor => wasteAnchor;

        public RectTransform CellAnchor(int index)
            => (cellAnchors != null && index >= 0 && index < cellAnchors.Length) ? cellAnchors[index] : null;
    }
}
```

- [ ] **Step 2: Refactor `UIBoardController.cs` to render against an active `BoardAnchorSet`**

Replace the anchor fields (lines ~18–21):
```csharp
        [Header("Anchors (element index = CellId.Value; order apex→base for correct overlap)")]
        [SerializeField] private RectTransform[] cellAnchors;
        [SerializeField] private RectTransform stockAnchor;
        [SerializeField] private RectTransform wasteAnchor;
```
with:
```csharp
        [Header("Anchor sets (one per board game; selected by game type at init)")]
        [SerializeField] private BoardAnchorSet[] anchorSets;
        private BoardAnchorSet active;
```

Add an `Awake` default + the selector (place near the top of the methods):
```csharp
        private void Awake()
        {
            // Default to the first set so the board renders even before the presenter selects one.
            if (anchorSets != null && anchorSets.Length > 0) active = anchorSets[0];
        }

        /// <summary>Activates the anchor set (and its board subtree) matching the game type; hides the others.</summary>
        public void UseAnchorSet(GameType gameType)
        {
            BoardAnchorSet match = null;
            if (anchorSets != null)
            {
                foreach (var set in anchorSets)
                {
                    if (set == null) continue;
                    bool isMatch = set.GameType == gameType;
                    set.gameObject.SetActive(isMatch);
                    if (isMatch) match = set;
                }
            }
            if (match != null) active = match;
        }
```

Update the three anchor reads:
- `AnchorFor` (line ~209):
```csharp
        private RectTransform AnchorFor(CellId id) => active != null ? active.CellAnchor(id.Value) : null;
```
- `RenderStock` — change the stock-anchor guard + instantiate target. Replace:
```csharp
            if (showStock && stockCard == null && stockAnchor != null)
            {
                stockCard = Instantiate(cardPrefab, stockAnchor);
```
with:
```csharp
            if (showStock && stockCard == null && active != null && active.StockAnchor != null)
            {
                stockCard = Instantiate(cardPrefab, active.StockAnchor);
```
- `RenderWaste` — replace:
```csharp
            if (wasteCard == null && wasteAnchor != null)
            {
                wasteCard = Instantiate(cardPrefab, wasteAnchor);
```
with:
```csharp
            if (wasteCard == null && active != null && active.WasteAnchor != null)
            {
                wasteCard = Instantiate(cardPrefab, active.WasteAnchor);
```

Add `using Model.Game;` to the file's usings (for `GameType`).

- [ ] **Step 3: Presenter — select the anchor set at init**

In `BoardPresenter.InitializeGameAsync`, immediately before `BoardController.DespawnAll();` (line ~168) add:
```csharp
            BoardController.UseAnchorSet(currentGameType);
```

- [ ] **Step 4: Editor — re-wire the Pyramid board**

In the open `BoardScene` (and/or `PyramidBoard.prefab`): the existing 28 Pyramid card anchors + stock + waste are currently referenced by `UIBoardController`'s removed fields. Re-home them:
1. Add a `BoardAnchorSet` component to the Pyramid board root object (the parent of the 28 anchors), set its `GameType = Pyramid`.
2. Drag the same 28 `RectTransform` anchors into its `cellAnchors` (preserve order = CellId 0..27, apex→base), and the stock/waste anchors into `stockAnchor` / `wasteAnchor`.
3. On `UIBoardController`, set `anchorSets` size to 1 (for now) and assign this Pyramid `BoardAnchorSet`.
4. Save the prefab/scene.

- [ ] **Step 5: Compile + verify Pyramid still renders**

Run `uloop compile` (Expected `ErrorCount: 0`), then play-verify Pyramid: route to Pyramid, confirm the 28 cards, stock, and waste render and a sum-13 match still works (boot via the editor's normal flow). Run `uloop run-tests --test-mode EditMode` — expect green.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Component/Board/BoardAnchorSet.cs Assets/Scripts/Component/Board/UIBoardController.cs Assets/Scripts/Scene/Board/BoardPresenter.cs Assets/Prefabs/Board/ Assets/Scenes/BoardScene.unity
git commit -m "feat(board): BoardAnchorSet — controller renders the active board layout"
```

---

## Task 11: `TriPeaksBoard` prefab + `BoardScene` two-subtree wiring

**Files:**
- Create: `Assets/Prefabs/Board/TriPeaksBoard.prefab`
- Modify: `Assets/Scenes/BoardScene.unity`

- [ ] **Step 1: Build the TriPeaks board prefab**

Mirror `PyramidBoard.prefab`. Create a board subtree with **28 card anchors** positioned as three peaks plus a 10-card base, matching the §1 cell-id layout:
- Row 0 (ids 0,1,2): the three peak tips, spread across the top (one per peak cluster).
- Row 1 (ids 3..8): two cards under each peak tip, offset half a card-width and down one row.
- Row 2 (ids 9..17): three cards under each peak (continuing the triangle).
- Row 3 / base (ids 18..27): a single continuous row of ten, overlapping the row-2 cards from below.
Visual overlap should read apex-on-top → base-in-front (the base row is fully tappable first). Use the same card size/spacing as Pyramid; the three peaks are visually separated horizontally but the base row is continuous.

Add a `BoardAnchorSet` to the prefab root: `GameType = TriPeaks`; assign the 28 anchors **in CellId order 0..27**, plus the shared stock and waste anchors (place stock/waste below the base row, same relative spot as Pyramid's).

- [ ] **Step 2: Place the prefab in `BoardScene` and wire the controller**

1. Instantiate `TriPeaksBoard` as a sibling of the Pyramid board subtree under the same parent the Pyramid board lives in.
2. On `UIBoardController`, grow `anchorSets` to size 2 and assign element [1] = the TriPeaks `BoardAnchorSet` (element [0] stays the Pyramid one).
3. Leave the TriPeaks subtree active in the editor; `UIBoardController.UseAnchorSet` toggles visibility at runtime (the presenter deactivates the non-matching subtree on init).

- [ ] **Step 3: Compile + play-verify TriPeaks renders**

`uloop compile` → `ErrorCount: 0`. Temporarily route to TriPeaks (e.g. a debug nav, since the lobby tile lands in Task 12) and confirm: 28 cards render in the three-peak shape, the stock shows a face-down pile, the waste shows one face-up card after the deal, and tapping an adjacent base card plays it onto the waste. Confirm routing back to Pyramid still shows the Pyramid board (anchor-set toggle works).

- [ ] **Step 4: Commit**

```bash
git add Assets/Prefabs/Board/TriPeaksBoard.prefab Assets/Scenes/BoardScene.unity
git commit -m "feat(board): TriPeaksBoard prefab + scene two-board wiring"
```

---

## Task 12: `TriPeaks` GameVariant asset + Lobby tile + localization

**Files:**
- Create: `Assets/ScriptableObjects/GameVariants/TriPeaks.asset`
- Modify: the Lobby scene/prefab (add a TriPeaks tile)
- Modify: `Assets/Localization/Tables/UI_en.asset` (+ other locale tables) — a TriPeaks display-name entry

- [ ] **Step 1: Create the GameVariant asset**

Duplicate `Assets/ScriptableObjects/GameVariants/Pyramid.asset` → `TriPeaks.asset`. Set: `gameType = TriPeaks`, `variantId = 1`, `displayName = "TriPeaks"`, a `previewIcon` (reuse a placeholder or the Pyramid icon until art exists). It needs no `dealRule` (board games build their layout in code).

- [ ] **Step 2: Add the Lobby tile**

Mirror the Pyramid tile in the Lobby grid: add a tile bound to the `TriPeaks` GameVariant (the lobby already routes board-mode variants to `BoardScene` via `GameType.IsBoardMode()` — no routing code change). Wire its label to the localized key from Step 3 and its icon to the variant's preview.

- [ ] **Step 3: Add the localized display name**

Add a `TriPeaks` entry to the UI localization table(s) (`UI_en.asset` and any other locale tables that carry the game names — mirror the key/naming the Pyramid tile uses). If the lobby tile reads its label from a localization key, use the same key on the new tile.

- [ ] **Step 4: Verify**

`uloop compile` → `ErrorCount: 0`. Open the Lobby; confirm the TriPeaks tile appears with the correct (localized) name and tapping it routes into `BoardScene` and deals a TriPeaks game.

- [ ] **Step 5: Commit**

```bash
git add Assets/ScriptableObjects/GameVariants/TriPeaks.asset Assets/Scenes/ Assets/Prefabs/ Assets/Localization/
git commit -m "feat(board): TriPeaks GameVariant, lobby tile, localized name"
```

---

## Task 13: Final play-verification + whole-feature review

**Files:** none (verification only).

- [ ] **Step 1: Full play-verification of TriPeaks** — from the Lobby tile:
  - Deal: 28 cards in three peaks, base row tappable, one card flipped to the waste, stock face-down.
  - Play a run: tap adjacent free cards; the HUD score increases by 50, 100, 150… (streak escalation); a stock draw resets the next clear to 50 and the score dips by 5.
  - Peak bonus: clearing each peak tip jumps the score by 500 / 1000 / 5000 in order.
  - Win: clear all 28 → win panel with the game code (copy works); the snapshot is cleared (no resume offered after a win).
  - Stuck: exhaust the stock with no playable card → stuck panel; Undo there reverts the last move.
  - Undo: a normal undo reverts a play and refunds its score.
  - Hint: the Hint button glows a playable free card, else the stock (draw), else buzzes when stuck; cycles through multiple plays.
  - Resume: pause / background mid-game, relaunch, choose continue → the board, stock, waste, score, and moves restore (streak restarts at 0 by design).
  - Play-with-code (pause panel) + win-panel code copy work for TriPeaks.
- [ ] **Step 2: Regression** — route to Pyramid and confirm an unchanged experience (deal, sum-13 match, King-alone, draw, recycle ×3, win, stuck, undo, hint, resume).
- [ ] **Step 3: Final EditMode suite** — `uloop run-tests --test-mode EditMode`. Expect all green (the prior baseline plus the new TriPeaks/scorer/layout/match/state tests).
- [ ] **Step 4: Whole-feature code review** — dispatch a final reviewer over the full branch diff against `Docs/superpowers/specs/2026-06-10-board-tripeaks-design.md` and `CLAUDE.md` conventions. Address any ≥80-confidence findings.
- [ ] **Step 5:** Use **superpowers:finishing-a-development-branch** to wrap up (PR / merge per the user's direction).

---

## Notes / invariants for the implementer

- **Never** register `PyramidGameService` or `TriPeaksGameService` `As<IBoardGameService>` — the presenter resolves through `IBoardGameServiceFactory`. Both are registered as their concrete types so the factory can inject them.
- `BoardState.WithStockDrawn()` pops the **last** stock element; `FixedShuffle` test decks must be ordered with that in mind (the Task 5 tests trace it in comments).
- The presenter advances `prevState = next` on **every** `OnBoardStateChanged`, including when scoring is skipped (finished / undo), so the next diff has the correct base.
- TriPeaks `maxRecycles` is always 0; `CanRecycle` is therefore always false and the stock-tap handler's empty-stock `RecycleStock()` is an inert no-op — do not special-case it.
- Streak is intentionally **not** persisted across resume (it resets to 0); the accumulated score is persisted via `SessionStats`. This is the documented design choice, not a bug to "fix".
- Do not touch Pyramid gameplay, scoring values, or its assets beyond the mechanical base-class extraction (Task 4) and the anchor-set re-homing (Task 10).
