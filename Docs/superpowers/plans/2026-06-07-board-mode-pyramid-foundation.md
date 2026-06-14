# Board Mode Foundation (Pyramid logic) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the pure-C# layered-board foundation (`Model.Board`) and a board game service, fully EditMode-tested, that makes Pyramid playable through a code API (deal → tap-select → match-remove → win/stuck/undo).

**Architecture:** Parallel board stack — new `Model.Board` types + `Service.BoardGameService`, reusing the existing deck/shuffle (`DeckFactory`/`IShuffleStrategy`). No Unity/scene/UI work in this plan; that is Plan 2. A single generic "free-cell" predicate + a game-specific `IBoardMatchRule` cover Pyramid now and Mahjong/TriPeaks later.

**Tech Stack:** C# (Unity 6), NUnit (EditMode), R3 (`Subject`/`Observable`). No UnityEngine in `Model.Board`.

**Branch:** `feature/board-mode-pyramid` (already checked out).

> **Amendment (2026-06-07, post-implementation):** `BoardCell` was reduced to **cover-only logical topology** (YAGNI). Removed: render `Layer`/`X`/`Y` (position is a View concern — fixed prefab anchors for Pyramid/TriPeaks), AND the Mahjong-only `LeftBlocker`/`RightBlocker` side rule (deferred to the Mahjong slice; `BoardRules.IsFree` is now cover-only, the `IsFree_3D` test was dropped). The `BoardCell` constructor is now `BoardCell(CellId id, IReadOnlyList<CellId> coverBlockers)`. Code blocks below that show `BoardCell(id, layer, x, y, ..., leftBlocker, rightBlocker)` predate these changes — the committed source/tests reflect the simplified signature. See spec §4.1 / §4.2 / §6.

---

## Notes for the implementer

- **New `.cs` files need a metadata refresh before `uloop` sees them.** After creating files for a task, run:
  `uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"`
  then poll `uloop compile` until it stops returning "Unity is compiling scripts"/"Domain Reload" before trusting results.
- **Run tests:** `uloop run-tests --test-mode EditMode`. A green baseline is 347 tests; this plan only ADDS tests.
- **No asmdef changes.** `Model/Board/*` falls under the existing `Model.asmdef`; `Service/BoardGameService/*` under `Service.asmdef`; tests under `Tests.EditMode.asmdef` (already references Model, Service, R3).
- **Conventions (CLAUDE.md):** immutable Model + `IEquatable`/`GetHashCode`; no `UnityEngine` in `Model`; With-pattern mutations; accumulators live in the Service, not the Model; minimal comments (XML summary only where non-obvious).
- **Commit** with the trailer:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

---

## File Structure

**Create (Model — `Model.Board` namespace):**
- `Assets/Scripts/Model/Board/CellId.cs` — stable position id (readonly struct).
- `Assets/Scripts/Model/Board/BoardCell.cs` — immutable cell topology (coords + cover/side blockers).
- `Assets/Scripts/Model/Board/BoardLayout.cs` — immutable layout (cells + id lookup).
- `Assets/Scripts/Model/Board/BoardState.cs` — immutable runtime state (+ With-pattern).
- `Assets/Scripts/Model/Board/BoardRules.cs` — static free-cell predicate.
- `Assets/Scripts/Model/Board/MatchVerdict.cs` — enum.
- `Assets/Scripts/Model/Board/IBoardMatchRule.cs` — game-specific match strategy.

**Create (Service — `Service.BoardGameService` namespace):**
- `Assets/Scripts/Service/BoardGameService/PyramidMatchRule.cs`
- `Assets/Scripts/Service/BoardGameService/PyramidLayoutFactory.cs`
- `Assets/Scripts/Service/BoardGameService/IBoardGameService.cs`
- `Assets/Scripts/Service/BoardGameService/BoardGameService.cs`

**Create (Tests — `Tests.EditMode` namespace):**
- `Assets/Tests/EditMode/BoardStateTests.cs`
- `Assets/Tests/EditMode/BoardRulesTests.cs`
- `Assets/Tests/EditMode/PyramidMatchRuleTests.cs`
- `Assets/Tests/EditMode/PyramidLayoutFactoryTests.cs`
- `Assets/Tests/EditMode/BoardGameServiceTests.cs`

---

## Task 1: `CellId`

**Files:**
- Create: `Assets/Scripts/Model/Board/CellId.cs`
- Test: `Assets/Tests/EditMode/BoardStateTests.cs` (shared file; start it here)

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/BoardStateTests.cs`:
```csharp
using Model.Board;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class BoardStateTests
    {
        [Test]
        public void CellId_EqualityAndHash_ByValue()
        {
            Assert.AreEqual(new CellId(5), new CellId(5));
            Assert.AreNotEqual(new CellId(5), new CellId(6));
            Assert.AreEqual(new CellId(5).GetHashCode(), new CellId(5).GetHashCode());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `uloop run-tests --test-mode EditMode` (after the metadata refresh above).
Expected: compile error / FAIL — `CellId` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Model/Board/CellId.cs`:
```csharp
using System;

namespace Model.Board
{
    /// <summary>Stable identifier of a position within a <see cref="BoardLayout"/>. Value is a dense 0-based index.</summary>
    public readonly struct CellId : IEquatable<CellId>
    {
        public int Value { get; }
        public CellId(int value) { Value = value; }

        public bool Equals(CellId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CellId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Cell({Value})";
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `uloop run-tests --test-mode EditMode`
Expected: PASS (`CellId_EqualityAndHash_ByValue`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Model/Board/CellId.cs Assets/Scripts/Model/Board/CellId.cs.meta \
        Assets/Tests/EditMode/BoardStateTests.cs Assets/Tests/EditMode/BoardStateTests.cs.meta
git commit -m "feat(board): add CellId value type"
```

---

## Task 2: `BoardCell`

**Files:**
- Create: `Assets/Scripts/Model/Board/BoardCell.cs`
- Test: `Assets/Tests/EditMode/BoardStateTests.cs`

- [ ] **Step 1: Write the failing test** (append method to `BoardStateTests`)

```csharp
        [Test]
        public void BoardCell_StoresTopology_AndEqualsById()
        {
            var cell = new BoardCell(new CellId(3), layer: 0, x: 1.5f, y: -2f,
                coverBlockers: new[] { new CellId(7), new CellId(8) },
                leftBlocker: new CellId(2), rightBlocker: new CellId(4));

            Assert.AreEqual(0, cell.Layer);
            Assert.AreEqual(2, cell.CoverBlockers.Count);
            Assert.AreEqual(new CellId(2), cell.LeftBlocker);
            Assert.AreEqual(new CellId(4), cell.RightBlocker);

            var same = new BoardCell(new CellId(3), 9, 0f, 0f, null);
            Assert.AreEqual(cell, same); // identity by Id
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `uloop run-tests --test-mode EditMode`
Expected: FAIL — `BoardCell` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Model/Board/BoardCell.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace Model.Board
{
    /// <summary>
    /// Immutable static topology of one board position: render coordinate plus the blocker graph
    /// that drives the free-cell predicate. No UnityEngine types (Model layer).
    /// </summary>
    public sealed class BoardCell : IEquatable<BoardCell>
    {
        public CellId Id { get; }
        public int Layer { get; }
        public float X { get; }
        public float Y { get; }

        /// <summary>Cells that must ALL be removed before this cell is free (top/front cover).</summary>
        public IReadOnlyList<CellId> CoverBlockers { get; }

        /// <summary>Same-layer left/right neighbor for the Mahjong side rule. Null = no side constraint.</summary>
        public CellId? LeftBlocker { get; }
        public CellId? RightBlocker { get; }

        public BoardCell(CellId id, int layer, float x, float y,
            IReadOnlyList<CellId> coverBlockers,
            CellId? leftBlocker = null, CellId? rightBlocker = null)
        {
            Id = id;
            Layer = layer;
            X = x;
            Y = y;
            CoverBlockers = coverBlockers ?? Array.Empty<CellId>();
            LeftBlocker = leftBlocker;
            RightBlocker = rightBlocker;
        }

        public bool Equals(BoardCell other) => other != null && Id.Equals(other.Id);
        public override bool Equals(object obj) => obj is BoardCell other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `uloop run-tests --test-mode EditMode`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Model/Board/BoardCell.cs Assets/Scripts/Model/Board/BoardCell.cs.meta \
        Assets/Tests/EditMode/BoardStateTests.cs
git commit -m "feat(board): add BoardCell topology type"
```

---

## Task 3: `BoardLayout`

**Files:**
- Create: `Assets/Scripts/Model/Board/BoardLayout.cs`
- Test: `Assets/Tests/EditMode/BoardStateTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void BoardLayout_LooksUpCellsById()
        {
            var cells = new[]
            {
                new BoardCell(new CellId(0), 0, 0f, 0f, null),
                new BoardCell(new CellId(1), 0, 1f, 0f, null),
            };
            var layout = new BoardLayout(Model.Game.GameType.Pyramid, 1, cells);

            Assert.AreEqual(2, layout.Count);
            Assert.AreEqual(1f, layout.Cell(new CellId(1)).X);
            Assert.IsTrue(layout.TryGetCell(new CellId(0), out _));
            Assert.IsFalse(layout.TryGetCell(new CellId(9), out _));
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `uloop run-tests --test-mode EditMode`
Expected: FAIL — `BoardLayout` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Model/Board/BoardLayout.cs`:
```csharp
using System.Collections.Generic;
using Model.Game;

namespace Model.Board
{
    /// <summary>
    /// Immutable board topology for a (GameType, Variant). Built by code (Pyramid) or loaded from an
    /// asset (Mahjong, later). Configuration object — equality is not required (analogous to IDealRule).
    /// </summary>
    public sealed class BoardLayout
    {
        public GameType GameType { get; }
        public int Variant { get; }
        public IReadOnlyList<BoardCell> Cells { get; }

        private readonly Dictionary<CellId, BoardCell> byId;

        public BoardLayout(GameType gameType, int variant, IReadOnlyList<BoardCell> cells)
        {
            GameType = gameType;
            Variant = variant;
            Cells = cells;
            byId = new Dictionary<CellId, BoardCell>(cells.Count);
            foreach (var cell in cells)
                byId[cell.Id] = cell;
        }

        public int Count => Cells.Count;
        public BoardCell Cell(CellId id) => byId[id];
        public bool TryGetCell(CellId id, out BoardCell cell) => byId.TryGetValue(id, out cell);
    }
}
```

- [ ] **Step 4: Run test to verify it passes** — `uloop run-tests --test-mode EditMode` → PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Model/Board/BoardLayout.cs Assets/Scripts/Model/Board/BoardLayout.cs.meta \
        Assets/Tests/EditMode/BoardStateTests.cs
git commit -m "feat(board): add BoardLayout with id lookup"
```

---

## Task 4: `BoardState` (immutable + With-pattern)

**Files:**
- Create: `Assets/Scripts/Model/Board/BoardState.cs`
- Test: `Assets/Tests/EditMode/BoardStateTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
        private static PlayingCard C(Model.Card.Rank r, Model.Card.Suit s) => new PlayingCard(r, s);

        [Test]
        public void BoardState_RemovesCellsAndDrawsStock_Immutably()
        {
            var cellCards = new[]
            {
                C(Model.Card.Rank.Ace, Model.Card.Suit.Spade),   // CellId 0
                C(Model.Card.Rank.King, Model.Card.Suit.Heart),  // CellId 1
            };
            var stock = new[] { C(Model.Card.Rank.Five, Model.Card.Suit.Club) };
            var state = new BoardState(cellCards, stock, waste: null);

            Assert.IsTrue(state.HasCard(new CellId(0)));
            Assert.AreEqual(Model.Card.Rank.King, state.CardAt(new CellId(1)).Rank);

            var afterRemove = state.WithCellsRemoved(new[] { new CellId(0) });
            Assert.IsFalse(afterRemove.HasCard(new CellId(0)));
            Assert.IsTrue(state.HasCard(new CellId(0)), "original unchanged");

            var afterDraw = state.WithStockDrawn();
            Assert.AreEqual(0, afterDraw.Stock.Count);
            Assert.AreEqual(Model.Card.Rank.Five, afterDraw.WasteTop.Rank);

            var afterWaste = afterDraw.WithWasteTopRemoved();
            Assert.IsNull(afterWaste.WasteTop);
        }
```
(Add `using Model.Card;` once at the top of the test file so `PlayingCard` resolves; keep the fully-qualified `Model.Card.Rank` references shown here or simplify to `Rank`/`Suit` after adding the using.)

- [ ] **Step 2: Run test to verify it fails** — FAIL: `BoardState` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Model/Board/BoardState.cs`:
```csharp
using System;
using System.Collections.Generic;
using Model.Card;

namespace Model.Board
{
    /// <summary>
    /// Immutable runtime board state. Each cell holds a card or is empty (removed). Optional stock/waste
    /// for games that use them (Pyramid); Mahjong leaves them empty. Mutations return new instances.
    /// </summary>
    public sealed class BoardState : IEquatable<BoardState>
    {
        private readonly PlayingCard[] cells; // index = CellId.Value; null = removed

        public IReadOnlyList<PlayingCard> Stock { get; }
        public IReadOnlyList<PlayingCard> Waste { get; }

        public BoardState(IReadOnlyList<PlayingCard> cellCards,
            IReadOnlyList<PlayingCard> stock = null, IReadOnlyList<PlayingCard> waste = null)
        {
            cells = new PlayingCard[cellCards.Count];
            for (int i = 0; i < cellCards.Count; i++) cells[i] = cellCards[i];
            Stock = stock ?? Array.Empty<PlayingCard>();
            Waste = waste ?? Array.Empty<PlayingCard>();
        }

        private BoardState(PlayingCard[] ownedCells, IReadOnlyList<PlayingCard> stock, IReadOnlyList<PlayingCard> waste)
        {
            cells = ownedCells;
            Stock = stock;
            Waste = waste;
        }

        public int CellCount => cells.Length;
        public bool HasCard(CellId id) => cells[id.Value] != null;
        public PlayingCard CardAt(CellId id) => cells[id.Value];
        public PlayingCard WasteTop => Waste.Count > 0 ? Waste[Waste.Count - 1] : null;

        public IEnumerable<CellId> OccupiedCells()
        {
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != null) yield return new CellId(i);
        }

        public bool AnyOccupied()
        {
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != null) return true;
            return false;
        }

        public BoardState WithCellsRemoved(IEnumerable<CellId> ids)
        {
            var copy = (PlayingCard[])cells.Clone();
            foreach (var id in ids) copy[id.Value] = null;
            return new BoardState(copy, Stock, Waste);
        }

        public BoardState WithWasteTopRemoved()
        {
            if (Waste.Count == 0) return this;
            var newWaste = new List<PlayingCard>(Waste);
            newWaste.RemoveAt(newWaste.Count - 1);
            return new BoardState((PlayingCard[])cells.Clone(), Stock, newWaste);
        }

        public BoardState WithStockDrawn()
        {
            if (Stock.Count == 0) return this;
            var newStock = new List<PlayingCard>(Stock);
            var top = newStock[newStock.Count - 1];
            newStock.RemoveAt(newStock.Count - 1);
            var newWaste = new List<PlayingCard>(Waste) { top };
            return new BoardState((PlayingCard[])cells.Clone(), newStock, newWaste);
        }

        public bool Equals(BoardState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (cells.Length != other.cells.Length) return false;
            for (int i = 0; i < cells.Length; i++)
            {
                var a = cells[i];
                var b = other.cells[i];
                if (a is null != (b is null)) return false;
                if (a != null && !a.Equals(b)) return false;
            }
            return ListEquals(Stock, other.Stock) && ListEquals(Waste, other.Waste);
        }

        private static bool ListEquals(IReadOnlyList<PlayingCard> a, IReadOnlyList<PlayingCard> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!a[i].Equals(b[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is BoardState other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var c in cells) hash.Add(c);
            hash.Add(Stock.Count);
            hash.Add(Waste.Count);
            return hash.ToHashCode();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes** — PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Model/Board/BoardState.cs Assets/Scripts/Model/Board/BoardState.cs.meta \
        Assets/Tests/EditMode/BoardStateTests.cs
git commit -m "feat(board): add immutable BoardState with With-pattern"
```

---

## Task 5: `BoardRules` (free-cell predicate)

**Files:**
- Create: `Assets/Scripts/Model/Board/BoardRules.cs`
- Test: `Assets/Tests/EditMode/BoardRulesTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/BoardRulesTests.cs`:
```csharp
using Model.Board;
using Model.Card;
using Model.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class BoardRulesTests
    {
        private static PlayingCard Any() => new PlayingCard(Rank.Ace, Suit.Spade);

        [Test]
        public void IsFree_2D_CellIsLockedWhileAnyCoverRemains()
        {
            // cell 0 covered by cells 1 and 2 (pyramid-style).
            var cells = new[]
            {
                new BoardCell(new CellId(0), 0, 0f, 0f, new[] { new CellId(1), new CellId(2) }),
                new BoardCell(new CellId(1), 0, -1f, -1f, null),
                new BoardCell(new CellId(2), 0,  1f, -1f, null),
            };
            var layout = new BoardLayout(GameType.Pyramid, 1, cells);
            var full = new BoardState(new[] { Any(), Any(), Any() });

            Assert.IsFalse(BoardRules.IsFree(layout, full, new CellId(0)), "covered");
            Assert.IsTrue(BoardRules.IsFree(layout, full, new CellId(1)), "bottom is free");

            var oneGone = full.WithCellsRemoved(new[] { new CellId(1) });
            Assert.IsFalse(BoardRules.IsFree(layout, oneGone, new CellId(0)), "still one cover");

            var bothGone = full.WithCellsRemoved(new[] { new CellId(1), new CellId(2) });
            Assert.IsTrue(BoardRules.IsFree(layout, bothGone, new CellId(0)), "uncovered");
        }

        [Test]
        public void IsFree_3D_NeedsTopClearAndOneSideOpen()
        {
            // cell 1 has left=0, right=2 on same layer; no cover.
            var cells = new[]
            {
                new BoardCell(new CellId(0), 0, -1f, 0f, null),
                new BoardCell(new CellId(1), 0,  0f, 0f, null, leftBlocker: new CellId(0), rightBlocker: new CellId(2)),
                new BoardCell(new CellId(2), 0,  1f, 0f, null),
            };
            var layout = new BoardLayout(GameType.None, 1, cells);
            var full = new BoardState(new[] { Any(), Any(), Any() });

            Assert.IsFalse(BoardRules.IsFree(layout, full, new CellId(1)), "both sides blocked");

            var leftGone = full.WithCellsRemoved(new[] { new CellId(0) });
            Assert.IsTrue(BoardRules.IsFree(layout, leftGone, new CellId(1)), "one side open");
        }

        [Test]
        public void IsFree_RemovedCell_IsNeverFree()
        {
            var cells = new[] { new BoardCell(new CellId(0), 0, 0f, 0f, null) };
            var layout = new BoardLayout(GameType.None, 1, cells);
            var empty = new BoardState(new[] { Any() }).WithCellsRemoved(new[] { new CellId(0) });
            Assert.IsFalse(BoardRules.IsFree(layout, empty, new CellId(0)));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails** — FAIL: `BoardRules` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Model/Board/BoardRules.cs`:
```csharp
using System.Collections.Generic;

namespace Model.Board
{
    /// <summary>Pure, game-agnostic board predicates. The single free-cell rule serves Pyramid, Mahjong, and TriPeaks.</summary>
    public static class BoardRules
    {
        /// <summary>
        /// A cell is free iff it still holds a card, every cover-blocker is removed, and — when side
        /// blockers exist (Mahjong) — at least one of the left/right neighbors is empty.
        /// </summary>
        public static bool IsFree(BoardLayout layout, BoardState state, CellId id)
        {
            if (!state.HasCard(id)) return false;

            var cell = layout.Cell(id);
            foreach (var blocker in cell.CoverBlockers)
                if (state.HasCard(blocker)) return false;

            bool hasSide = cell.LeftBlocker.HasValue || cell.RightBlocker.HasValue;
            if (!hasSide) return true;

            bool leftOpen = !cell.LeftBlocker.HasValue || !state.HasCard(cell.LeftBlocker.Value);
            bool rightOpen = !cell.RightBlocker.HasValue || !state.HasCard(cell.RightBlocker.Value);
            return leftOpen || rightOpen;
        }

        public static IEnumerable<CellId> FreeCells(BoardLayout layout, BoardState state)
        {
            foreach (var cell in layout.Cells)
                if (IsFree(layout, state, cell.Id))
                    yield return cell.Id;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes** — PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Model/Board/BoardRules.cs Assets/Scripts/Model/Board/BoardRules.cs.meta \
        Assets/Tests/EditMode/BoardRulesTests.cs Assets/Tests/EditMode/BoardRulesTests.cs.meta
git commit -m "feat(board): add unified free-cell predicate (BoardRules)"
```

---

## Task 6: `IBoardMatchRule` + `MatchVerdict`

**Files:**
- Create: `Assets/Scripts/Model/Board/MatchVerdict.cs`, `Assets/Scripts/Model/Board/IBoardMatchRule.cs`
- (No standalone test — exercised via Task 7.)

- [ ] **Step 1: Write the interfaces**

`Assets/Scripts/Model/Board/MatchVerdict.cs`:
```csharp
namespace Model.Board
{
    /// <summary>Result of evaluating the player's current ordered selection against a game's match rule.</summary>
    public enum MatchVerdict
    {
        /// <summary>Valid prefix; awaiting more taps.</summary>
        Incomplete,
        /// <summary>The selection is a complete, removable set.</summary>
        Match,
        /// <summary>The selection cannot form a match; the service resets it.</summary>
        Invalid
    }
}
```

`Assets/Scripts/Model/Board/IBoardMatchRule.cs`:
```csharp
using System.Collections.Generic;
using Model.Card;

namespace Model.Board
{
    /// <summary>
    /// Game-specific match strategy (injected, mirrors the IDealRule pattern). Given the cards the player
    /// has selected (in tap order), returns whether they form a match, need more, or are invalid.
    /// Not pair-locked: a size-1 selection may already be a Match (e.g. Pyramid King); a future TriPeaks
    /// rule evaluates a single free card against the waste-top.
    /// </summary>
    public interface IBoardMatchRule
    {
        MatchVerdict Evaluate(IReadOnlyList<PlayingCard> selection);
    }
}
```

- [ ] **Step 2: Compile**

Run the metadata refresh, then `uloop compile`. Expected: ErrorCount 0.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Model/Board/MatchVerdict.cs Assets/Scripts/Model/Board/MatchVerdict.cs.meta \
        Assets/Scripts/Model/Board/IBoardMatchRule.cs Assets/Scripts/Model/Board/IBoardMatchRule.cs.meta
git commit -m "feat(board): add IBoardMatchRule + MatchVerdict"
```

---

## Task 7: `PyramidMatchRule`

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/PyramidMatchRule.cs`
- Test: `Assets/Tests/EditMode/PyramidMatchRuleTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/PyramidMatchRuleTests.cs`:
```csharp
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PyramidMatchRuleTests
    {
        private readonly PyramidMatchRule rule = new PyramidMatchRule();
        private static PlayingCard C(Rank r) => new PlayingCard(r, Suit.Spade);

        [Test]
        public void King_AloneIsMatch()
            => Assert.AreEqual(MatchVerdict.Match, rule.Evaluate(new List<PlayingCard> { C(Rank.King) }));

        [Test]
        public void NonKing_AloneIsIncomplete()
            => Assert.AreEqual(MatchVerdict.Incomplete, rule.Evaluate(new List<PlayingCard> { C(Rank.Five) }));

        [Test]
        public void PairSummingTo13_IsMatch()
            => Assert.AreEqual(MatchVerdict.Match, rule.Evaluate(new List<PlayingCard> { C(Rank.Nine), C(Rank.Four) }));

        [Test]
        public void PairNotSummingTo13_IsInvalid()
            => Assert.AreEqual(MatchVerdict.Invalid, rule.Evaluate(new List<PlayingCard> { C(Rank.Nine), C(Rank.Five) }));

        [Test]
        public void AceQueen_IsMatch() // 1 + 12 = 13
            => Assert.AreEqual(MatchVerdict.Match, rule.Evaluate(new List<PlayingCard> { C(Rank.Ace), C(Rank.Queen) }));
    }
}
```

- [ ] **Step 2: Run test to verify it fails** — FAIL: `PyramidMatchRule` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Service/BoardGameService/PyramidMatchRule.cs`:
```csharp
using System.Collections.Generic;
using Model.Board;
using Model.Card;

namespace Service.BoardGameService
{
    /// <summary>Pyramid: remove two cards whose ranks sum to 13 (Ace=1..King=13), or a King alone.</summary>
    public sealed class PyramidMatchRule : IBoardMatchRule
    {
        public MatchVerdict Evaluate(IReadOnlyList<PlayingCard> selection)
        {
            if (selection.Count == 1)
                return selection[0].Rank == Rank.King ? MatchVerdict.Match : MatchVerdict.Incomplete;

            if (selection.Count == 2)
            {
                int sum = (int)selection[0].Rank + (int)selection[1].Rank;
                return sum == 13 ? MatchVerdict.Match : MatchVerdict.Invalid;
            }

            return MatchVerdict.Invalid;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes** — PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/PyramidMatchRule.cs Assets/Scripts/Service/BoardGameService/PyramidMatchRule.cs.meta \
        Assets/Tests/EditMode/PyramidMatchRuleTests.cs Assets/Tests/EditMode/PyramidMatchRuleTests.cs.meta
git commit -m "feat(board): add PyramidMatchRule (sum-13 / King-alone)"
```

---

## Task 8: `PyramidLayoutFactory`

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/PyramidLayoutFactory.cs`
- Test: `Assets/Tests/EditMode/PyramidLayoutFactoryTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/PyramidLayoutFactoryTests.cs`:
```csharp
using Model.Board;
using Model.Game;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PyramidLayoutFactoryTests
    {
        [Test]
        public void Creates28CellsOverSevenRows_ForPyramid()
        {
            var layout = PyramidLayoutFactory.Create();
            Assert.AreEqual(GameType.Pyramid, layout.GameType);
            Assert.AreEqual(28, layout.Count);
        }

        [Test]
        public void Apex_IsCoveredByTopOfRowTwo()
        {
            var layout = PyramidLayoutFactory.Create();
            var apex = layout.Cell(new CellId(0));
            Assert.AreEqual(2, apex.CoverBlockers.Count);
            Assert.Contains(new CellId(1), (System.Collections.ICollection)apex.CoverBlockers);
            Assert.Contains(new CellId(2), (System.Collections.ICollection)apex.CoverBlockers);
        }

        [Test]
        public void BottomRowCells_HaveNoCover()
        {
            var layout = PyramidLayoutFactory.Create();
            // bottom row (row 6) = indices 21..27
            for (int i = 21; i <= 27; i++)
                Assert.AreEqual(0, layout.Cell(new CellId(i)).CoverBlockers.Count, $"cell {i}");
        }

        [Test]
        public void NoSideBlockers_InPyramid()
        {
            var layout = PyramidLayoutFactory.Create();
            foreach (var cell in layout.Cells)
            {
                Assert.IsFalse(cell.LeftBlocker.HasValue);
                Assert.IsFalse(cell.RightBlocker.HasValue);
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails** — FAIL: `PyramidLayoutFactory` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Service/BoardGameService/PyramidLayoutFactory.cs`:
```csharp
using System.Collections.Generic;
using Model.Board;
using Model.Game;

namespace Service.BoardGameService
{
    /// <summary>
    /// Generates the classic 28-card, 7-row Pyramid layout in code. Cell ids are assigned row-by-row
    /// (apex = 0). Cell (r,k) is covered by the two cards diagonally below it: (r+1,k) and (r+1,k+1).
    /// Render coords are in unit cells (apex at y=0, rows descend); the Component scales to screen.
    /// </summary>
    public static class PyramidLayoutFactory
    {
        public const int Rows = 7;
        public const int CellCount = 28; // 1+2+...+7

        public static BoardLayout Create(int variant = 1)
        {
            var cells = new List<BoardCell>(CellCount);

            for (int r = 0; r < Rows; r++)
            {
                int rowStart = r * (r + 1) / 2;
                for (int k = 0; k <= r; k++)
                {
                    int index = rowStart + k;
                    float x = k - (r * 0.5f); // centered around 0
                    float y = -r;

                    var covers = new List<CellId>(2);
                    if (r < Rows - 1)
                    {
                        int belowStart = (r + 1) * (r + 2) / 2;
                        covers.Add(new CellId(belowStart + k));
                        covers.Add(new CellId(belowStart + k + 1));
                    }

                    cells.Add(new BoardCell(new CellId(index), layer: 0, x, y, covers));
                }
            }

            return new BoardLayout(GameType.Pyramid, variant, cells);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes** — PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/PyramidLayoutFactory.cs Assets/Scripts/Service/BoardGameService/PyramidLayoutFactory.cs.meta \
        Assets/Tests/EditMode/PyramidLayoutFactoryTests.cs Assets/Tests/EditMode/PyramidLayoutFactoryTests.cs.meta
git commit -m "feat(board): add PyramidLayoutFactory (28-cell code-gen layout)"
```

---

## Task 9: `IBoardGameService` + deal

**Files:**
- Create: `Assets/Scripts/Service/BoardGameService/IBoardGameService.cs`, `Assets/Scripts/Service/BoardGameService/BoardGameService.cs`
- Test: `Assets/Tests/EditMode/BoardGameServiceTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/BoardGameServiceTests.cs`:
```csharp
using System.Linq;
using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class BoardGameServiceTests
    {
        private BoardGameService NewService()
            => new BoardGameService(new FisherYatesShuffleStrategy());

        private BoardGameService NewInitializedPyramid(int seed = 12345)
        {
            var svc = NewService();
            svc.Initialize(PyramidLayoutFactory.Create(), new PyramidMatchRule(), seed);
            return svc;
        }

        [Test]
        public void Initialize_Deals28ToCells_24ToStock_Deterministically()
        {
            var a = NewInitializedPyramid(seed: 99);
            var b = NewInitializedPyramid(seed: 99);

            Assert.AreEqual(28, a.CurrentState.OccupiedCells().Count());
            Assert.AreEqual(24, a.CurrentState.Stock.Count);
            Assert.AreEqual(0, a.CurrentState.Waste.Count);
            Assert.AreEqual(99, a.CurrentSeed);
            // same seed → identical deal
            Assert.IsTrue(a.CurrentState.Equals(b.CurrentState));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails** — FAIL: `BoardGameService` does not exist.

- [ ] **Step 3: Write the interface + minimal service (Initialize only)**

`Assets/Scripts/Service/BoardGameService/IBoardGameService.cs`:
```csharp
using System.Collections.Generic;
using Model.Board;
using R3;

namespace Service.BoardGameService
{
    /// <summary>
    /// Drives a layered-board solitaire (Pyramid now; Mahjong/TriPeaks later). Mirrors IGameService:
    /// Initialize, an Observable state stream, undo history, and restore for snapshots.
    /// </summary>
    public interface IBoardGameService
    {
        Observable<BoardState> OnBoardStateChanged { get; }
        BoardState CurrentState { get; }
        BoardLayout Layout { get; }
        int? CurrentSeed { get; }

        void Initialize(BoardLayout layout, IBoardMatchRule rule, int? seed = null);

        /// <summary>Tap a board cell. Ignored unless the cell is free.</summary>
        void SelectCell(CellId id);
        /// <summary>Tap the waste-top card (stock/waste games). Ignored when the waste is empty.</summary>
        void SelectWasteTop();
        /// <summary>Flip the top stock card to the waste. Ignored when the stock is empty.</summary>
        void DrawFromStock();
        void ClearSelection();

        bool IsWon(BoardState state);
        bool HasAnyMove(BoardState state);

        bool CanUndo { get; }
        void Undo();
        IReadOnlyCollection<BoardState> UndoHistory { get; }
        void Restore(BoardLayout layout, IBoardMatchRule rule, int seed,
            BoardState state, IReadOnlyList<BoardState> undoHistory);
    }
}
```

`Assets/Scripts/Service/BoardGameService/BoardGameService.cs`:
```csharp
using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using R3;
using Service.GameService;

namespace Service.BoardGameService
{
    public sealed class BoardGameService : IBoardGameService, IDisposable
    {
        private readonly IShuffleStrategy shuffle;
        private readonly Subject<BoardState> stateSubject = new();
        private readonly List<BoardState> undoStack = new();
        private readonly List<SelectedTarget> selection = new(); // accumulator lives in the Service

        private IBoardMatchRule rule;

        public BoardLayout Layout { get; private set; }
        public BoardState CurrentState { get; private set; }
        public int? CurrentSeed { get; private set; }
        public Observable<BoardState> OnBoardStateChanged => stateSubject;

        public BoardGameService(IShuffleStrategy shuffle)
        {
            this.shuffle = shuffle ?? throw new ArgumentNullException(nameof(shuffle));
        }

        public void Initialize(BoardLayout layout, IBoardMatchRule rule, int? seed = null)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.rule = rule ?? throw new ArgumentNullException(nameof(rule));

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
            selection.Clear();
            stateSubject.OnNext(CurrentState);
        }

        // --- selection / mutation: implemented in Task 10 ---
        public void SelectCell(CellId id) => throw new NotImplementedException();
        public void SelectWasteTop() => throw new NotImplementedException();
        public void DrawFromStock() => throw new NotImplementedException();
        public void ClearSelection() => selection.Clear();

        // --- win / stuck: implemented in Task 11 ---
        public bool IsWon(BoardState state) => throw new NotImplementedException();
        public bool HasAnyMove(BoardState state) => throw new NotImplementedException();

        // --- undo / restore: implemented in Task 12 ---
        public bool CanUndo => undoStack.Count > 0;
        public void Undo() => throw new NotImplementedException();
        public IReadOnlyCollection<BoardState> UndoHistory => undoStack;
        public void Restore(BoardLayout layout, IBoardMatchRule rule, int seed,
            BoardState state, IReadOnlyList<BoardState> undoHistory) => throw new NotImplementedException();

        public void Dispose() => stateSubject.Dispose();

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

> The `NotImplementedException` stubs keep the class compiling while later tasks fill them in via TDD. Each later task replaces a stub and adds its test.

- [ ] **Step 4: Run test to verify it passes** — `Initialize_Deals28ToCells_24ToStock_Deterministically` PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/IBoardGameService.cs Assets/Scripts/Service/BoardGameService/IBoardGameService.cs.meta \
        Assets/Scripts/Service/BoardGameService/BoardGameService.cs Assets/Scripts/Service/BoardGameService/BoardGameService.cs.meta \
        Assets/Tests/EditMode/BoardGameServiceTests.cs Assets/Tests/EditMode/BoardGameServiceTests.cs.meta
git commit -m "feat(board): add IBoardGameService + deterministic deal"
```

---

## Task 10: Selection → match removal

**Files:**
- Modify: `Assets/Scripts/Service/BoardGameService/BoardGameService.cs`
- Test: `Assets/Tests/EditMode/BoardGameServiceTests.cs`

- [ ] **Step 1: Write the failing tests** (append to `BoardGameServiceTests`)

```csharp
        // Build a tiny controllable game: a 3-cell single-layer layout, no covers, with chosen cards.
        private static BoardLayout FlatLayout(int n)
        {
            var cells = new System.Collections.Generic.List<BoardCell>(n);
            for (int i = 0; i < n; i++)
                cells.Add(new BoardCell(new CellId(i), 0, i, 0f, null));
            return new BoardLayout(Model.Game.GameType.None, 1, cells);
        }

        private sealed class FixedShuffle : IShuffleStrategy
        {
            private readonly System.Collections.Generic.List<PlayingCard> deck;
            public FixedShuffle(params PlayingCard[] cards) => deck = new System.Collections.Generic.List<PlayingCard>(cards);
            public System.Collections.Generic.List<PlayingCard> Shuffle(int seed)
                => new System.Collections.Generic.List<PlayingCard>(deck);
        }

        private static PlayingCard Card(Rank r) => new PlayingCard(r, Suit.Spade);

        [Test]
        public void SelectingPairSummingTo13_RemovesBothCells()
        {
            // cells: [9, 4, 2]  → 9+4 = 13
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four), Card(Rank.Two)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            svc.SelectCell(new CellId(0)); // 9 → incomplete
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)));
            svc.SelectCell(new CellId(1)); // 4 → match

            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)));
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(1)));
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(2)));
        }

        [Test]
        public void SelectingKing_RemovesItAlone()
        {
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.King), Card(Rank.Two), Card(Rank.Three)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            svc.SelectCell(new CellId(0)); // King → match immediately
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)));
        }

        [Test]
        public void InvalidPair_ThenKing_RemovesKingViaReset()
        {
            // [5, K, 3]: tap 5 (incomplete) → tap K (invalid pair) resets to {K} which matches alone
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.King), Card(Rank.Three)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            svc.SelectCell(new CellId(0)); // 5
            svc.SelectCell(new CellId(1)); // K → 5+13 invalid → reset to {K} → match
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)), "5 stays");
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(1)), "K removed");
        }

        [Test]
        public void SelectingNonFreeCell_IsIgnored()
        {
            // cell 0 covered by cell 1; tapping 0 does nothing.
            var cells = new System.Collections.Generic.List<BoardCell>
            {
                new BoardCell(new CellId(0), 0, 0f, 0f, new[] { new CellId(1) }),
                new BoardCell(new CellId(1), 0, 0f, -1f, null),
            };
            var layout = new BoardLayout(Model.Game.GameType.None, 1, cells);
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.King), Card(Rank.Two)));
            svc.Initialize(layout, new PyramidMatchRule(), seed: 0);

            svc.SelectCell(new CellId(0)); // not free → ignored, no match
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)));
        }

        [Test]
        public void PublishesNewState_OnMatch()
        {
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four), Card(Rank.Two)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            int publishes = 0;
            using var _ = svc.OnBoardStateChanged.Subscribe(_ => publishes++);
            svc.SelectCell(new CellId(0));
            svc.SelectCell(new CellId(1)); // match → 1 publish
            Assert.AreEqual(1, publishes);
        }
```

- [ ] **Step 2: Run tests to verify they fail** — FAIL: `SelectCell` throws `NotImplementedException`.

- [ ] **Step 3: Implement selection/mutation** (replace the three stub bodies `SelectCell`/`SelectWasteTop`/`DrawFromStock` and add the private helpers)

```csharp
        public void SelectCell(CellId id)
        {
            if (!BoardRules.IsFree(Layout, CurrentState, id)) return;
            HandleSelect(SelectedTarget.OfCell(id), CurrentState.CardAt(id));
        }

        public void SelectWasteTop()
        {
            var top = CurrentState.WasteTop;
            if (top == null) return;
            HandleSelect(SelectedTarget.Waste(), top);
        }

        public void DrawFromStock()
        {
            if (CurrentState.Stock.Count == 0) return;
            PushUndo();
            selection.Clear();
            CurrentState = CurrentState.WithStockDrawn();
            stateSubject.OnNext(CurrentState);
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
            TryResolve();
        }

        /// <summary>Evaluates the current selection; applies removal on Match. Returns true unless Invalid.</summary>
        private bool TryResolve()
        {
            var cards = new List<PlayingCard>(selection.Count);
            foreach (var t in selection) cards.Add(CardOf(t));

            switch (rule.Evaluate(cards))
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

            CurrentState = next;
            stateSubject.OnNext(CurrentState);
        }

        private void PushUndo() => undoStack.Add(CurrentState);
```

- [ ] **Step 4: Run tests to verify they pass** — all Task-10 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/BoardGameService.cs Assets/Tests/EditMode/BoardGameServiceTests.cs
git commit -m "feat(board): tap-select match removal (pairs, King, reset, ignore non-free)"
```

---

## Task 11: Win + stuck detection

**Files:**
- Modify: `Assets/Scripts/Service/BoardGameService/BoardGameService.cs`
- Test: `Assets/Tests/EditMode/BoardGameServiceTests.cs`

- [ ] **Step 1: Write the failing tests** (append)

```csharp
        [Test]
        public void IsWon_TrueWhenAllCellsCleared()
        {
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.King), Card(Rank.King)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            Assert.IsFalse(svc.IsWon(svc.CurrentState));

            svc.SelectCell(new CellId(0)); // King removed alone
            svc.SelectCell(new CellId(1)); // King removed alone
            Assert.IsTrue(svc.IsWon(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_TrueWhenStockNotEmpty()
        {
            // 2 cells [2,3] (no pair sums to 13), but stock has a card → draw is a move.
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three), Card(Rank.Seven)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            Assert.AreEqual(1, svc.CurrentState.Stock.Count);
            Assert.IsTrue(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_FalseWhenNoPairAndNoStock()
        {
            // cells [2,3], no stock → 2+3=5, no King → stuck.
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            Assert.IsFalse(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_TrueWhenFreePairSumsTo13()
        {
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            Assert.IsTrue(svc.HasAnyMove(svc.CurrentState));
        }
```

- [ ] **Step 2: Run tests to verify they fail** — FAIL: `IsWon`/`HasAnyMove` throw `NotImplementedException`.

- [ ] **Step 3: Implement** (replace the `IsWon`/`HasAnyMove` stubs)

```csharp
        public bool IsWon(BoardState state) => !state.AnyOccupied();

        public bool HasAnyMove(BoardState state)
        {
            if (state.Stock.Count > 0) return true;

            var candidates = new List<PlayingCard>();
            foreach (var id in BoardRules.FreeCells(Layout, state))
                candidates.Add(state.CardAt(id));
            if (state.WasteTop != null) candidates.Add(state.WasteTop);

            // delegate validity to the match rule (game-agnostic)
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

- [ ] **Step 4: Run tests to verify they pass** — all PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/BoardGameService.cs Assets/Tests/EditMode/BoardGameServiceTests.cs
git commit -m "feat(board): win + stuck detection via match rule"
```

---

## Task 12: Undo + Restore

**Files:**
- Modify: `Assets/Scripts/Service/BoardGameService/BoardGameService.cs`
- Test: `Assets/Tests/EditMode/BoardGameServiceTests.cs`

- [ ] **Step 1: Write the failing tests** (append)

```csharp
        [Test]
        public void Undo_RestoresPreviousState()
        {
            var svc = new BoardGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four), Card(Rank.Two)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            Assert.IsFalse(svc.CanUndo);
            svc.SelectCell(new CellId(0));
            svc.SelectCell(new CellId(1)); // remove 9 & 4
            Assert.IsTrue(svc.CanUndo);

            svc.Undo();
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)));
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(1)));
            Assert.IsFalse(svc.CanUndo);
        }

        [Test]
        public void Restore_RebuildsStateSeedAndHistory()
        {
            var svc = new BoardGameService(new FisherYatesShuffleStrategy());
            var layout = PyramidLayoutFactory.Create();
            var rule = new PyramidMatchRule();

            var source = new BoardGameService(new FisherYatesShuffleStrategy());
            source.Initialize(layout, rule, seed: 42);
            var snapshotState = source.CurrentState;

            svc.Restore(layout, rule, seed: 42, state: snapshotState,
                undoHistory: new System.Collections.Generic.List<BoardState>());

            Assert.AreEqual(42, svc.CurrentSeed);
            Assert.IsTrue(svc.CurrentState.Equals(snapshotState));
            Assert.IsFalse(svc.CanUndo);
        }
```

- [ ] **Step 2: Run tests to verify they fail** — FAIL: `Undo`/`Restore` throw `NotImplementedException`.

- [ ] **Step 3: Implement** (replace the `Undo`/`Restore` stubs)

```csharp
        public void Undo()
        {
            if (undoStack.Count == 0) return;
            int last = undoStack.Count - 1;
            CurrentState = undoStack[last];
            undoStack.RemoveAt(last);
            selection.Clear();
            stateSubject.OnNext(CurrentState);
        }

        public void Restore(BoardLayout layout, IBoardMatchRule rule, int seed,
            BoardState state, IReadOnlyList<BoardState> undoHistory)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.rule = rule ?? throw new ArgumentNullException(nameof(rule));
            CurrentSeed = seed;

            undoStack.Clear();
            if (undoHistory != null) undoStack.AddRange(undoHistory);
            selection.Clear();

            CurrentState = state;
            stateSubject.OnNext(CurrentState);
        }
```

- [ ] **Step 4: Run tests to verify they pass** — all PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/BoardGameService.cs Assets/Tests/EditMode/BoardGameServiceTests.cs
git commit -m "feat(board): undo + restore (snapshot-ready)"
```

---

## Task 13: Full-suite verification

- [ ] **Step 1: Force a clean rebuild**

Run:
`uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"`
then poll `uloop compile` until ErrorCount 0 and no "compiling"/"Domain Reload".

- [ ] **Step 2: Run the whole EditMode suite**

Run: `uloop run-tests --test-mode EditMode`
Expected: previous baseline (347) + new board tests, all green, 0 failures.

- [ ] **Step 3: Final commit (if any stray meta/asset)**

```bash
git status --short   # expect only intended files
git add -A -- Assets/Scripts/Model/Board Assets/Scripts/Service/BoardGameService Assets/Tests/EditMode
git commit -m "test(board): confirm full EditMode suite green for Pyramid logic" --allow-empty
```

---

## What this plan deliberately defers to Plan 2 (Unity integration)

- `UIBoardController` (positional render, z-order, tap input, free highlight, removal animation), reusing `UICard` + skin.
- `IngameShellView` extraction + `IngameShell.prefab` base + **Board scene prefab variant** (UI reuse, no double build).
- `BoardScene` / `BoardComponent` / `BoardPresenter`; Lobby tile + Route + Build Settings registration.
- Stats wiring (board scoring entry point on `SessionStatsService`) + HUD.
- `BoardSnapshot` DTO + converter + board auto-save through the existing `IGameSnapshotRepository`.

---

## Self-Review (author checklist — completed)

- **Spec coverage:** §4 model → Tasks 1–6; §4.2 match rule → Tasks 6–7; §8 Pyramid layout/rule/deal/stock/win/stuck → Tasks 7–11; Undo/Restore (Standard parity, snapshot-ready) → Task 12. Rendering/UI/Scene/Stats/Snapshot are explicitly Plan 2 (listed above) — not gaps.
- **Placeholder scan:** none — every code step shows complete, compiling code. `NotImplementedException` stubs in Task 9 are intentional and replaced in Tasks 10–12 (each replacement shown in full).
- **Type consistency:** `CellId`, `BoardCell`, `BoardLayout(GameType, int, IReadOnlyList<BoardCell>)`, `BoardState(IReadOnlyList<PlayingCard>, stock, waste)` + `WithCellsRemoved`/`WithWasteTopRemoved`/`WithStockDrawn`/`HasCard`/`CardAt`/`WasteTop`/`OccupiedCells`/`AnyOccupied`, `IBoardMatchRule.Evaluate`→`MatchVerdict`, `IBoardGameService` members, and `BoardGameService(IShuffleStrategy)` are referenced identically across tasks and tests. `SelectedTarget` is private to the service.
