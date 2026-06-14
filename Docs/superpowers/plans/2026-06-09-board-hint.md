# Board Hint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the already-rendered Hint button work in Pyramid board mode — highlight a removable match, fall back to draw/recycle suggestions, cycle through matches, and play hint/no-hint sounds.

**Architecture:** Add a `BoardHint` immutable model and a `GetHints` method on `BoardGameService` that enumerates matches (sharing the enumeration with the existing `HasAnyMove`), then falls back to a single Draw/Recycle suggestion, else empty (stuck). Wire the existing shell Hint button in `BoardPresenter` to cycle these hints, reusing the selection-glow visual. One new controller method glows the stock pile.

**Tech Stack:** Unity 6, C#, R3, NUnit (EditMode), `uloop` editor CLI.

**Spec:** `Docs/superpowers/specs/2026-06-09-board-hint-design.md`

---

## Background the engineer needs

- **Domain:** Pyramid solitaire. A "match" removes either a lone **King** (rank value 13) or a **pair of cards summing 13** (e.g. Nine+Four). Cards are removable only when *free* (not covered). A non-empty **stock** can be drawn one card at a time to the **waste**; the waste-top is also matchable. When the stock empties, the waste can be **recycled** back into the stock up to `maxRecycles` times (Pyramid uses 3).
- **Existing service:** `Service/BoardGameService/BoardGameService.cs` already has `HasAnyMove(BoardState)` (returns bool), `CanRecycle(BoardState)`, an `IBoardMatchRule rule` field (private), `BoardLayout Layout`, and helper `BoardRules.FreeCells(Layout, state)`.
- **Existing UI:** the shared `IngameShellView` renders a Hint button exposed as `Shell.OnHintObservable()`. `BoardPresenter` does not subscribe to it yet (a comment at the shell-buttons section says hint is deferred).
- **Selection glow:** `UIBoardController.SetSelection(SelectionSnapshot)` is the sole driver of the per-card highlight — it iterates every spawned card and sets `SetHighlight(selection.Contains(cell))`, plus the waste-top. `BoardPresenter` drives it from the service's `OnSelectionChanged` stream.
- **Match rule values:** Ace=1 … Nine=9, Ten=10, Jack=11, Queen=12, King=13. Nine+Four=13, Four+Nine=13, Ten+Three=13.
- **`uloop` recompile dance** (CLAUDE.md): after editing `.cs`, force a rebuild then poll:
  ```bash
  uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"
  # then poll `uloop compile` until Message is not "Unity is compiling…"/"Domain Reload";
  # a one-shot ErrorCount:1 with Errors:[] mid-reload is STALE → re-poll.
  ```
- **Run EditMode tests:** `uloop run-tests --test-mode EditMode` → JSON with passed/failed counts. Current baseline: **409 passed / 0 failed / 6 skipped**.
- **Commit trailer (verbatim, generic — NOT a version string):**
  ```
  Co-Authored-By: Claude <noreply@anthropic.com>
  ```
- **Never `git add -A`.** Stage only the exact files each task lists.

---

## File Structure

- **Create** `Assets/Scripts/Model/Model/Board/BoardHint.cs` — *(actual path below)* immutable hint value (`Model.Board`).
  - Exact path: `Assets/Scripts/Model/Board/BoardHint.cs`
- **Modify** `Assets/Scripts/Service/BoardGameService/IBoardGameService.cs` — add `GetHints`.
- **Modify** `Assets/Scripts/Service/BoardGameService/BoardGameService.cs` — add `GetHints` + `EnumerateMatches` helper; refactor `HasAnyMove` to share it.
- **Modify** `Assets/Scripts/Component/Board/UIBoardController.cs` — add `SetStockHighlight(bool)`.
- **Modify** `Assets/Scripts/Scene/Board/BoardPresenter.cs` — `HandleHint`, button wiring, cache invalidation, stock-glow clearing.
- **Modify** `Assets/Tests/EditMode/BoardGameServiceTests.cs` — model equality + `GetHints` tests.

---

### Task 1: `BoardHint` model

**Files:**
- Create: `Assets/Scripts/Model/Board/BoardHint.cs`
- Test: `Assets/Tests/EditMode/BoardGameServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add this test method inside `Tests.EditMode.BoardGameServiceTests` (the file already has `using Model.Board;`, `using Model.Card;`, `using NUnit.Framework;`, `using System.Linq;`):

```csharp
        [Test]
        public void BoardHint_Match_ValueEquality()
        {
            var a = BoardHint.OfMatch(new SelectionSnapshot(new[] { new CellId(0) }, false));
            var b = BoardHint.OfMatch(new SelectionSnapshot(new[] { new CellId(0) }, false));
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, BoardHint.Draw);
        }
```

- [ ] **Step 2: Verify it fails (red)**

Run:
```bash
uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"
# poll until stable:
uloop compile
```
Expected: compile error — `BoardHint` does not exist (`CS0103`/`CS0246`). This is the red state.

- [ ] **Step 3: Create the model**

`Assets/Scripts/Model/Board/BoardHint.cs`:

```csharp
using System;

namespace Model.Board
{
    /// <summary>What the Hint button should surface: a removable match (with the cells/waste to glow),
    /// or a suggestion to draw or recycle the stock.</summary>
    public enum BoardHintKind { Match, Draw, Recycle }

    /// <summary>Immutable hint. For <see cref="BoardHintKind.Match"/>, <see cref="Targets"/> holds the
    /// cells (one King, or a pair summing 13) plus the waste flag to highlight. For Draw/Recycle the
    /// stock pile is the affordance, so <see cref="Targets"/> is <see cref="SelectionSnapshot.Empty"/>.</summary>
    public sealed class BoardHint : IEquatable<BoardHint>
    {
        public BoardHintKind Kind { get; }
        public SelectionSnapshot Targets { get; }

        public BoardHint(BoardHintKind kind, SelectionSnapshot targets)
        {
            Kind = kind;
            Targets = targets ?? throw new ArgumentNullException(nameof(targets));
        }

        public static BoardHint OfMatch(SelectionSnapshot targets) => new BoardHint(BoardHintKind.Match, targets);
        public static readonly BoardHint Draw = new BoardHint(BoardHintKind.Draw, SelectionSnapshot.Empty);
        public static readonly BoardHint Recycle = new BoardHint(BoardHintKind.Recycle, SelectionSnapshot.Empty);

        public bool Equals(BoardHint other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Kind == other.Kind && Targets.Equals(other.Targets);
        }

        public override bool Equals(object obj) => obj is BoardHint o && Equals(o);
        public override int GetHashCode() => HashCode.Combine((int)Kind, Targets);
    }
}
```

- [ ] **Step 4: Verify it passes (green)**

Run the recompile dance, then:
```bash
uloop run-tests --test-mode EditMode
```
Expected: passed count = **410** (baseline 409 + this test), 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Model/Board/BoardHint.cs Assets/Scripts/Model/Board/BoardHint.cs.meta Assets/Tests/EditMode/BoardGameServiceTests.cs
git commit -m "feat(board): BoardHint model

Co-Authored-By: Claude <noreply@anthropic.com>"
```
(The `.meta` is created by Unity on import — include it if present.)

---

### Task 2: `GetHints` on the service (+ `HasAnyMove` refactor)

**Files:**
- Modify: `Assets/Scripts/Service/BoardGameService/IBoardGameService.cs`
- Modify: `Assets/Scripts/Service/BoardGameService/BoardGameService.cs`
- Test: `Assets/Tests/EditMode/BoardGameServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add these seven test methods inside `Tests.EditMode.BoardGameServiceTests`:

```csharp
        [Test]
        public void GetHints_LoneKing_ReturnsMatchForThatCell()
        {
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.King)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Match, hints[0].Kind);
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(0)));
        }

        [Test]
        public void GetHints_FreePairSumming13_ReturnsMatchWithBothCells()
        {
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Match, hints[0].Kind);
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(0)));
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(1)));
        }

        [Test]
        public void GetHints_WasteTopPlusCellSumming13_ReturnsMatchWithCellAndWaste()
        {
            // cell0 = Four, stock = [Nine]; draw → waste-top = Nine; 4 + 9 = 13.
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Four), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0);
            svc.DrawFromStock();
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Match, hints[0].Kind);
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(0)));
            Assert.IsTrue(hints[0].Targets.WasteSelected);
        }

        [Test]
        public void GetHints_NoMatchButStock_ReturnsDraw()
        {
            // cells [2,3] (2+3=5, no King), stock = [7] → suggest a draw.
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three), Card(Rank.Seven)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Draw, hints[0].Kind);
        }

        [Test]
        public void GetHints_NoMatchEmptyStockRecyclable_ReturnsRecycle()
        {
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three), Card(Rank.Seven)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0, maxRecycles: 1);
            svc.DrawFromStock(); // stock now empty, waste = [7]; no pair sums to 13
            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Recycle, hints[0].Kind);
        }

        [Test]
        public void GetHints_FullyStuck_ReturnsEmpty()
        {
            // cells [2,3], no stock, no recycle → nothing to do.
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(0, hints.Count);
        }

        [Test]
        public void GetHints_TwoIndependentMatches_ReturnsBoth()
        {
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.King), Card(Rank.King)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(2, hints.Count);
            Assert.IsTrue(hints.All(h => h.Kind == BoardHintKind.Match));
        }
```

- [ ] **Step 2: Verify they fail (red)**

Run the recompile dance + `uloop compile`. Expected: compile error — `BoardGameService` has no `GetHints` (`CS1061`). Red.

- [ ] **Step 3: Add `GetHints` to the interface**

In `IBoardGameService.cs`, add after the `HasAnyMove` declaration (the file already has `using System.Collections.Generic;` and `using Model.Board;`):

```csharp
        /// <summary>Available next moves for the Hint button, best-first: every removable match, else a
        /// single Draw or Recycle suggestion, else empty (stuck).</summary>
        IReadOnlyList<BoardHint> GetHints(BoardState state);
```

- [ ] **Step 4: Implement in the service + refactor `HasAnyMove`**

In `BoardGameService.cs`, **replace** the entire existing `HasAnyMove` method:

```csharp
        public bool HasAnyMove(BoardState state)
        {
            if (state.Stock.Count > 0) return true;
            if (CanRecycle(state)) return true;

            var candidates = new List<PlayingCard>();
            foreach (var id in BoardRules.FreeCells(Layout, state))
                candidates.Add(state.CardAt(id));
            if (state.WasteTop != null) candidates.Add(state.WasteTop);

            // Match rules in this mode are single- or pair-based, so enumerating singles + pairs is exhaustive;
            // validity is delegated to the rule. A future rule needing 3+ cards would require widening this.
            var single = new PlayingCard[1];
            var pair = new PlayingCard[2];
            for (int i = 0; i < candidates.Count; i++)
            {
                single[0] = candidates[i];
                if (rule.Evaluate(single) == MatchVerdict.Match) return true;
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    pair[0] = candidates[i];
                    pair[1] = candidates[j];
                    if (rule.Evaluate(pair) == MatchVerdict.Match) return true;
                }
            }
            return false;
        }
```

with this refactored version + the new `GetHints` and `EnumerateMatches` helper (place all three together):

```csharp
        public bool HasAnyMove(BoardState state)
        {
            if (state.Stock.Count > 0) return true;
            if (CanRecycle(state)) return true;
            return EnumerateMatches(state).Count > 0;
        }

        public IReadOnlyList<BoardHint> GetHints(BoardState state)
        {
            var matches = EnumerateMatches(state);
            if (matches.Count > 0) return matches;
            if (state.Stock.Count > 0) return new[] { BoardHint.Draw };
            if (CanRecycle(state)) return new[] { BoardHint.Recycle };
            return Array.Empty<BoardHint>();
        }

        /// <summary>Every removable match among the free cells + waste-top, as highlight targets.
        /// Single- or pair-based only (same assumption as <see cref="HasAnyMove"/>); a 3+-card rule
        /// would widen both. Shared by <see cref="HasAnyMove"/> and <see cref="GetHints"/>.</summary>
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
            if (hasWaste) cards.Add(state.WasteTop);   // waste is the last candidate, index == cells.Count

            var result = new List<BoardHint>();
            var single = new PlayingCard[1];
            var pair = new PlayingCard[2];
            for (int i = 0; i < cards.Count; i++)
            {
                single[0] = cards[i];
                if (rule.Evaluate(single) == MatchVerdict.Match)
                {
                    result.Add(BoardHint.OfMatch(TargetsFor(cells, i, -1, hasWaste)));
                    continue; // a card that matches alone (King) needn't also be offered paired
                }
                for (int j = i + 1; j < cards.Count; j++)
                {
                    pair[0] = cards[i];
                    pair[1] = cards[j];
                    if (rule.Evaluate(pair) == MatchVerdict.Match)
                        result.Add(BoardHint.OfMatch(TargetsFor(cells, i, j, hasWaste)));
                }
            }
            return result;
        }

        /// <summary>Builds the highlight snapshot for candidate indices a (and optional b, -1 = none).
        /// An index == cells.Count refers to the waste-top; lower indices are cells[index].</summary>
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
```

(`BoardGameService.cs` already has `using System;`, `using System.Collections.Generic;`, `using Model.Board;`, `using Model.Card;`.)

- [ ] **Step 5: Verify green**

Run the recompile dance, then:
```bash
uloop run-tests --test-mode EditMode
```
Expected: **417 passed** (410 after Task 1 + 7 new), 0 failed. The pre-existing `HasAnyMove_*` tests must still pass (refactor parity).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/IBoardGameService.cs Assets/Scripts/Service/BoardGameService/BoardGameService.cs Assets/Tests/EditMode/BoardGameServiceTests.cs
git commit -m "feat(board): GetHints enumerates matches, falls back to draw/recycle

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: `SetStockHighlight` on the controller

**Files:**
- Modify: `Assets/Scripts/Component/Board/UIBoardController.cs`

No unit test — this is a thin View method (covered by play-mode verification in Task 4). Compile-only gate.

- [ ] **Step 1: Add the method**

In `UIBoardController.cs`, add this method immediately after `SetSelection` (which ends around line 93):

```csharp
        /// <summary>Glow the stock pile as a Draw/Recycle hint affordance (reuses the selection glow).</summary>
        public void SetStockHighlight(bool on)
        {
            if (stockCard != null) stockCard.SetHighlight(on);
        }
```

- [ ] **Step 2: Verify it compiles**

Run the recompile dance, then:
```bash
uloop compile
```
Expected: `ErrorCount: 0`, `WarningCount: 0` (re-poll if a stale `ErrorCount:1` with `Errors:[]` shows mid-reload).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Component/Board/UIBoardController.cs
git commit -m "feat(board): SetStockHighlight for stock-pile hint glow

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: Wire the Hint button in the presenter

**Files:**
- Modify: `Assets/Scripts/Scene/Board/BoardPresenter.cs`

- [ ] **Step 1: Add hint cache fields**

In `BoardPresenter.cs`, find the field block (around line 46-50, ending with `private CancellationTokenSource lifeCts;`). Add after `private int prevRecycleCount;`:

```csharp
        private IReadOnlyList<BoardHint> currentHints;
        private int hintIndex;
```

(`using System.Collections.Generic;` and `using Model.Board;` are already present.)

- [ ] **Step 2: Reset the cache on a new game**

In `InitializeGame`, find:

```csharp
            prevTotal = TotalCards(state);
            prevRecycleCount = state.RecycleCount;
```

Replace with:

```csharp
            prevTotal = TotalCards(state);
            prevRecycleCount = state.RecycleCount;
            currentHints = null;
            hintIndex = 0;
```

- [ ] **Step 3: Clear the stock glow on any real selection change**

In `InitializeGame`, find the `OnSelectionChanged` subscription body:

```csharp
                .Subscribe(sel =>
                {
                    BoardController.SetSelection(sel);
                    // A non-empty emission means a free card was just picked → select feedback.
                    // Match / clear / undo all clear the selection first and emit Empty, so this never
                    // fires for them — hence no undoInProgress guard is needed here (unlike the scoring
                    // branch, where an undo of a stock draw would otherwise be miscounted).
                    if (sel.Cells.Count > 0 || sel.WasteSelected)
                        AudioService.Play(AudioCatalog.Card.Place);
                })
```

Replace with (adds the stock-glow clear so a cell tap drops a lingering Draw-hint glow):

```csharp
                .Subscribe(sel =>
                {
                    BoardController.SetSelection(sel);
                    BoardController.SetStockHighlight(false); // a real selection supersedes any draw/recycle hint glow
                    // A non-empty emission means a free card was just picked → select feedback.
                    // Match / clear / undo all clear the selection first and emit Empty, so this never
                    // fires for them — hence no undoInProgress guard is needed here (unlike the scoring
                    // branch, where an undo of a stock draw would otherwise be miscounted).
                    if (sel.Cells.Count > 0 || sel.WasteSelected)
                        AudioService.Play(AudioCatalog.Card.Place);
                })
```

- [ ] **Step 4: Invalidate the hint cache + clear glow on every state change**

In `OnBoardStateChanged`, find:

```csharp
            BoardController.RenderBoard(next, !undoInProgress, BoardGameService.CanRecycle(next));
            RefreshHighlights(next);
```

Replace with:

```csharp
            BoardController.RenderBoard(next, !undoInProgress, BoardGameService.CanRecycle(next));
            RefreshHighlights(next);

            // Any move invalidates a shown hint. The service clears its selection on every state change,
            // so re-asserting Empty here drops a stale match-hint glow (the OnSelectionChanged stream may
            // skip its own Empty emission when the selection was already empty, e.g. a stock draw).
            currentHints = null;
            hintIndex = 0;
            BoardController.SetSelection(SelectionSnapshot.Empty);
            BoardController.SetStockHighlight(false);
```

- [ ] **Step 5: Wire the button + add `HandleHint`**

In `WireShellButtons`, find the section comment:

```csharp
        // --- Shell buttons (mirror IngamePresenter; persistence/daily/hint deferred to 2c) ---
```

Replace it with:

```csharp
        // --- Shell buttons (mirror IngamePresenter; persistence/daily deferred to 2c) ---
```

Then find the undo subscription:

```csharp
            Shell.OnUndoObservable().Subscribe(_ =>
            {
                if (!BoardGameService.CanUndo) return;
                PerformUndo();
            }).AddTo(disposable);
```

Add immediately after it:

```csharp
            Shell.OnHintObservable().Subscribe(_ => HandleHint()).AddTo(disposable);
```

Then add the `HandleHint` method. Put it right after `WireShellButtons` ends (before `WireBoardInput`, or anywhere in the class body):

```csharp
        /// <summary>Cycle the available board hints: glow a removable match, or the stock pile for a
        /// draw/recycle suggestion; buzz when stuck. Highlight only — the player still taps to act.</summary>
        private void HandleHint()
        {
            if (currentHints == null || hintIndex >= currentHints.Count)
            {
                currentHints = BoardGameService.GetHints(BoardGameService.CurrentState);
                hintIndex = 0;
            }

            if (currentHints.Count == 0)
            {
                AudioService.Play(AudioCatalog.Game.NoHint);
                return;
            }

            var hint = currentHints[hintIndex];
            hintIndex++;
            SessionStats.RecordHintUsed();
            AudioService.Play(AudioCatalog.Game.Hint);

            if (hint.Kind == BoardHintKind.Match)
            {
                BoardController.SetSelection(hint.Targets);
                BoardController.SetStockHighlight(false);
            }
            else // Draw or Recycle → the stock pile is the affordance
            {
                BoardController.SetSelection(SelectionSnapshot.Empty);
                BoardController.SetStockHighlight(true);
            }
        }
```

- [ ] **Step 6: Verify it compiles**

Run the recompile dance, then:
```bash
uloop compile
```
Expected: `ErrorCount: 0`, `WarningCount: 0`.

- [ ] **Step 7: Run the full EditMode suite (no regressions)**

```bash
uloop run-tests --test-mode EditMode
```
Expected: **417 passed**, 0 failed (no new tests this task; confirms nothing broke).

- [ ] **Step 8: Play-mode verification**

Boot to a Pyramid game (clean Stop → Play → wait for `AppLifetimeScope` → navigate Pyramid). Then:
- Tap **Hint** with a match on the board → a removable match glows (selection-style) and `game.hint` plays. Tap again → next match glows (cycles, wraps).
- Exhaust matches (board state where only a draw helps) → Hint glows the **stock** pile and `game.hint` plays.
- Empty the stock with a recycle available, no match → Hint glows the stock (recycle suggestion).
- Reach a fully stuck state (no match, no stock, no recycle) → Hint plays `game.no_hint`, no glow.
- After tapping a real move, a previously shown hint glow clears.

Capture via `uloop` dynamic code: subscribe to `IAudioService.OnPlay`, invoke `Shell` hint (or call the service `GetHints` directly and assert), and inspect card `SetHighlight` state / `OverlayRoot`. Confirm `game.hint` vs `game.no_hint` and that exactly the expected cards glow.

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/Scene/Board/BoardPresenter.cs
git commit -m "feat(board): wire Hint button — cycle matches, draw/recycle fallback, no-hint buzz

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage:**
- "highlight a removable match" → Task 2 `EnumerateMatches` + Task 4 `SetSelection(hint.Targets)`. ✓
- "cycle through all matches, wrap-around" → Task 4 `hintIndex` cache + recompute on exhaust. ✓
- "no match → suggest draw; stock empty → recycle; stuck → no-hint sound" → Task 2 `GetHints` fallback chain + Task 4 branches + `Game.NoHint`. ✓
- "reuse selection glow" → Task 4 uses `SetSelection`; Task 3 `SetStockHighlight` reuses `SetHighlight`. ✓
- "record hint usage" → Task 4 `SessionStats.RecordHintUsed()`. ✓
- "no separate service" / "no preview animation" / "no auto-move" / "no prefab edits" → honored (GetHints folded into service; HandleHint only highlights). ✓
- "BoardHint model immutable + IEquatable" → Task 1. ✓
- "SetStockHighlight controller method" → Task 3. ✓
- "no lingering glow" → Task 4 Steps 3-4 (clear on selection change AND on state change). ✓
- Testing matrix (King / pair / waste+cell / draw / recycle / stuck / multiple / HasAnyMove parity) → Task 2 Step 1 (7 tests) + existing parity tests. ✓

**Placeholder scan:** none — every step has full code or exact commands.

**Type consistency:** `BoardHint` (`Kind`, `Targets`, `OfMatch`, `Draw`, `Recycle`), `BoardHintKind` (`Match`/`Draw`/`Recycle`), `GetHints(BoardState) : IReadOnlyList<BoardHint>`, `SetStockHighlight(bool)`, `currentHints`/`hintIndex` — used identically across Tasks 1-4. `SelectionSnapshot(IReadOnlyList<CellId>, bool)` ctor + `.Contains`/`.WasteSelected`/`.Empty` match the existing model. `AudioCatalog.Game.Hint`/`NoHint` and `AudioCatalog.Card.Place` are the verified constants.
