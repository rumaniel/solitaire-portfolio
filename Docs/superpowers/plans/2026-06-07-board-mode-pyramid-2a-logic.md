# Board Mode — Plan 2a: Scoring + Snapshot (pure logic) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the pure-C#, EditMode-tested pieces that the board (Pyramid) game needs for Standard parity but that don't touch Unity: a board score rule and a board snapshot (save/resume) model + converter.

**Architecture:** Mirrors the existing card stack. `BoardSnapshot`/`BoardStateDto`/`BoardSnapshotConverter` parallel `GameSnapshot`/`TableStateDto`/`GameSnapshotConverter` (the card snapshot repo is `GameSnapshot`-typed, so the board needs its own serialization model — the file gateway/auto-save wiring is deferred to the Unity plan 2c). `IBoardScoreRule`/`PyramidScoreRule` parallel `IScoreRule`. Reuses `CardDto`, `SessionStatsDto`, and `Model.Board` types from plan 1.

**Tech Stack:** C# (Unity 6), NUnit (EditMode), MemoryPack (serialization). No UnityEngine in any new type.

**Branch:** `feature/board-mode-pyramid` (already checked out — plan 1 is committed here).

---

## Notes for the implementer

- **New `.cs` files need a metadata refresh before `uloop` sees them.** After creating files for a task, run:
  `uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"`
  then poll `uloop compile` until it stops returning "Unity is compiling scripts" / "Domain Reload" / "already in progress" and reports a real ErrorCount.
- **Run tests:** `uloop run-tests --test-mode EditMode`. Green baseline at the start of this plan is **381 total, 375 passed, 6 skipped**; this plan only ADDS tests.
- **No asmdef changes.** `Model/Board/*` is under `Model.asmdef` (which already references MemoryPack — `CardDto`/`TableStateDto` live there). `Service/BoardGameService/*` is under `Service.asmdef`. Tests are under `Tests.EditMode.asmdef` (already references Model, Service, MemoryPack, NUnit).
- **Conventions (CLAUDE.md):** no `UnityEngine` in `Model`; DTOs are `[MemoryPackable] public partial class` with `{ get; set; }` (mirror `TableStateDto`/`CardDto`); minimal comments.
- **Git hygiene:** commit after each task with the listed files only (never `git add -A`). Trailer:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

---

## File Structure

**Create (Model — `Model.Board`):**
- `Assets/Scripts/Model/Board/IBoardScoreRule.cs` — board scoring contract.
- `Assets/Scripts/Model/Board/BoardStateDto.cs` — `[MemoryPackable]` serialization shape of `BoardState`.
- `Assets/Scripts/Model/Board/BoardSnapshot.cs` — `[MemoryPackable]` full save record.
- `Assets/Scripts/Model/Board/BoardSnapshotConverter.cs` — `BoardState`/`SessionStats` ⟷ DTO.

**Create (Service — `Service.BoardGameService`):**
- `Assets/Scripts/Service/BoardGameService/PyramidScoreRule.cs` — concrete Pyramid scoring.

**Create (Tests):**
- `Assets/Tests/EditMode/PyramidScoreRuleTests.cs`
- `Assets/Tests/EditMode/BoardSnapshotTests.cs`

---

## Task 1: Board score rule

**Files:**
- Create: `Assets/Scripts/Model/Board/IBoardScoreRule.cs`, `Assets/Scripts/Service/BoardGameService/PyramidScoreRule.cs`
- Test: `Assets/Tests/EditMode/PyramidScoreRuleTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/PyramidScoreRuleTests.cs`:
```csharp
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PyramidScoreRuleTests
    {
        [Test]
        public void ScoreForRemoval_IsPointsPerClearedCard()
        {
            var rule = new PyramidScoreRule(perCard: 5);
            Assert.AreEqual(10, rule.ScoreForRemoval(2)); // a sum-13 pair
            Assert.AreEqual(5, rule.ScoreForRemoval(1));  // a King alone
        }

        [Test]
        public void BoardClearedBonus_IsConfigured()
        {
            var rule = new PyramidScoreRule(perCard: 5, boardClearedBonus: 100);
            Assert.AreEqual(100, rule.BoardClearedBonus);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `uloop run-tests --test-mode EditMode` (after the metadata refresh).
Expected: compile error / FAIL — `PyramidScoreRule` does not exist.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Model/Board/IBoardScoreRule.cs`:
```csharp
namespace Model.Board
{
    /// <summary>
    /// Scoring for a cover-match board game. Plain values; the presenter calls these to feed SessionStats
    /// (mirrors how IScoreRule drives card scoring, but board events differ — no pile types).
    /// </summary>
    public interface IBoardScoreRule
    {
        /// <summary>Points awarded for clearing <paramref name="cardCount"/> cards in one match (pair = 2, King = 1).</summary>
        int ScoreForRemoval(int cardCount);

        /// <summary>Bonus added when the board is fully cleared.</summary>
        int BoardClearedBonus { get; }
    }
}
```

`Assets/Scripts/Service/BoardGameService/PyramidScoreRule.cs`:
```csharp
using Model.Board;

namespace Service.BoardGameService
{
    /// <summary>Pyramid scoring: fixed points per cleared card, plus a one-off board-cleared bonus.</summary>
    public sealed class PyramidScoreRule : IBoardScoreRule
    {
        private readonly int perCard;

        public PyramidScoreRule(int perCard = 5, int boardClearedBonus = 100)
        {
            this.perCard = perCard;
            BoardClearedBonus = boardClearedBonus;
        }

        public int ScoreForRemoval(int cardCount) => cardCount * perCard;
        public int BoardClearedBonus { get; }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `uloop run-tests --test-mode EditMode`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Model/Board/IBoardScoreRule.cs Assets/Scripts/Model/Board/IBoardScoreRule.cs.meta \
        Assets/Scripts/Service/BoardGameService/PyramidScoreRule.cs Assets/Scripts/Service/BoardGameService/PyramidScoreRule.cs.meta \
        Assets/Tests/EditMode/PyramidScoreRuleTests.cs Assets/Tests/EditMode/PyramidScoreRuleTests.cs.meta
git commit -m "feat(board): add IBoardScoreRule + PyramidScoreRule"
```

---

## Task 2: Snapshot DTOs

**Files:**
- Create: `Assets/Scripts/Model/Board/BoardStateDto.cs`, `Assets/Scripts/Model/Board/BoardSnapshot.cs`
- (No standalone test — exercised by the converter round-trip in Task 3.)

- [ ] **Step 1: Write the DTOs**

`Assets/Scripts/Model/Board/BoardStateDto.cs`:
```csharp
using MemoryPack;
using Model.Card;

namespace Model.Board
{
    /// <summary>
    /// Serialization shape of <see cref="BoardState"/>. <see cref="Cells"/> is indexed by CellId.Value;
    /// a null entry means that cell has been removed. Stock/Waste are ordered bottom→top.
    /// </summary>
    [MemoryPackable]
    public partial class BoardStateDto
    {
        public CardDto[] Cells { get; set; }
        public CardDto[] Stock { get; set; }
        public CardDto[] Waste { get; set; }
    }
}
```

`Assets/Scripts/Model/Board/BoardSnapshot.cs`:
```csharp
using MemoryPack;
using Model.Game;
using Model.Stats;

namespace Model.Board
{
    /// <summary>Full board-game save record. Parallels <see cref="GameSnapshot"/> for the card stack.</summary>
    [MemoryPackable]
    public partial class BoardSnapshot
    {
        public GameType GameType { get; set; }
        public int Variant { get; set; }
        public int Seed { get; set; }
        public BoardStateDto CurrentState { get; set; }
        public BoardStateDto[] UndoHistory { get; set; }
        public SessionStatsDto Stats { get; set; }
        public long SavedAtUtcTicks { get; set; }
    }
}
```

- [ ] **Step 2: Compile**

Run the metadata refresh, then poll `uloop compile`. Expected: ErrorCount 0 (MemoryPack source-generates the partial halves).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Model/Board/BoardStateDto.cs Assets/Scripts/Model/Board/BoardStateDto.cs.meta \
        Assets/Scripts/Model/Board/BoardSnapshot.cs Assets/Scripts/Model/Board/BoardSnapshot.cs.meta
git commit -m "feat(board): add BoardStateDto + BoardSnapshot (MemoryPackable)"
```

---

## Task 3: Snapshot converter + round-trip tests

**Files:**
- Create: `Assets/Scripts/Model/Board/BoardSnapshotConverter.cs`
- Test: `Assets/Tests/EditMode/BoardSnapshotTests.cs`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/BoardSnapshotTests.cs`:
```csharp
using System.Collections.Generic;
using MemoryPack;
using Model.Board;
using Model.Card;
using Model.Game;
using Model.Stats;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class BoardSnapshotTests
    {
        private static PlayingCard C(Rank r, Suit s) => new PlayingCard(r, s);

        // A 3-cell board: cell 0 = 9♠ removed, cell 1 = 4♥, cell 2 = K♣; stock [2♦]; waste [8♠].
        private static BoardState SampleState()
        {
            var cells = new PlayingCard[]
            {
                C(Rank.Nine, Suit.Spade),
                C(Rank.Four, Suit.Heart),
                C(Rank.King, Suit.Club),
            };
            var state = new BoardState(cells,
                stock: new[] { C(Rank.Two, Suit.Diamond) },
                waste: new[] { C(Rank.Eight, Suit.Spade) });
            return state.WithCellsRemoved(new[] { new CellId(0) }); // remove cell 0
        }

        private static void AssertStateEqual(BoardState expected, BoardState actual)
        {
            Assert.AreEqual(expected.CellCount, actual.CellCount);
            for (int i = 0; i < expected.CellCount; i++)
            {
                var id = new CellId(i);
                Assert.AreEqual(expected.HasCard(id), actual.HasCard(id), $"cell {i} presence");
                if (expected.HasCard(id))
                    Assert.IsTrue(expected.CardAt(id).Equals(actual.CardAt(id)), $"cell {i} card");
            }
            CollectionAssert.AreEqual(expected.Stock, actual.Stock);
            CollectionAssert.AreEqual(expected.Waste, actual.Waste);
        }

        [Test]
        public void Converter_RoundTrip_PreservesBoardState_IncludingRemovedCells()
        {
            var original = SampleState();
            var snapshot = BoardSnapshotConverter.ToSnapshot(
                GameType.Pyramid, 1, 42, original, new List<BoardState>(), new SessionStats());
            var restored = BoardSnapshotConverter.ToBoardState(snapshot.CurrentState);

            AssertStateEqual(original, restored);
            Assert.IsFalse(restored.HasCard(new CellId(0)), "removed cell stays removed");
        }

        [Test]
        public void Converter_RoundTrip_PreservesUndoHistory()
        {
            var s0 = SampleState();
            var s1 = s0.WithCellsRemoved(new[] { new CellId(1) });
            var history = new List<BoardState> { s0, s1 };

            var snapshot = BoardSnapshotConverter.ToSnapshot(
                GameType.Pyramid, 1, 42, s1, history, new SessionStats());
            var restored = BoardSnapshotConverter.ToHistory(snapshot.UndoHistory);

            Assert.AreEqual(2, restored.Count);
            AssertStateEqual(s0, restored[0]);
            AssertStateEqual(s1, restored[1]);
        }

        [Test]
        public void Converter_RoundTrip_PreservesStatsAndMetadata()
        {
            var stats = new SessionStats { Score = 150, MoveCount = 42, ElapsedSeconds = 123.5f, HintCount = 3 };
            var snapshot = BoardSnapshotConverter.ToSnapshot(
                GameType.Pyramid, 2, 99, SampleState(), new List<BoardState>(), stats);

            Assert.AreEqual(GameType.Pyramid, snapshot.GameType);
            Assert.AreEqual(2, snapshot.Variant);
            Assert.AreEqual(99, snapshot.Seed);
            Assert.Greater(snapshot.SavedAtUtcTicks, 0);

            var restoredStats = BoardSnapshotConverter.ToSessionStats(snapshot.Stats);
            Assert.AreEqual(150, restoredStats.Score);
            Assert.AreEqual(42, restoredStats.MoveCount);
            Assert.AreEqual(123.5f, restoredStats.ElapsedSeconds, 0.01f);
            Assert.AreEqual(3, restoredStats.HintCount);
        }

        [Test]
        public void Snapshot_MemoryPack_SerializeDeserialize_RoundTrips()
        {
            var snapshot = BoardSnapshotConverter.ToSnapshot(
                GameType.Pyramid, 1, 42, SampleState(), new List<BoardState>(),
                new SessionStats { Score = 10 });

            var bytes = MemoryPackSerializer.Serialize(snapshot);
            var back = MemoryPackSerializer.Deserialize<BoardSnapshot>(bytes);

            Assert.IsNotNull(back);
            Assert.AreEqual(snapshot.Seed, back.Seed);
            Assert.AreEqual(snapshot.Stats.Score, back.Stats.Score);
            AssertStateEqual(
                BoardSnapshotConverter.ToBoardState(snapshot.CurrentState),
                BoardSnapshotConverter.ToBoardState(back.CurrentState));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `uloop run-tests --test-mode EditMode`
Expected: FAIL — `BoardSnapshotConverter` does not exist.

- [ ] **Step 3: Write the converter**

`Assets/Scripts/Model/Board/BoardSnapshotConverter.cs`:
```csharp
using System;
using System.Collections.Generic;
using Model.Card;
using Model.Game;
using Model.Stats;

namespace Model.Board
{
    /// <summary>Converts board runtime state ⟷ serializable DTOs. Parallels GameSnapshotConverter.</summary>
    public static class BoardSnapshotConverter
    {
        public static BoardSnapshot ToSnapshot(
            GameType gameType, int variant, int seed,
            BoardState currentState, IReadOnlyCollection<BoardState> undoHistory, SessionStats stats)
        {
            var history = new BoardStateDto[undoHistory.Count];
            int i = 0;
            foreach (var state in undoHistory)
                history[i++] = ToBoardStateDto(state);

            return new BoardSnapshot
            {
                GameType = gameType,
                Variant = variant,
                Seed = seed,
                CurrentState = ToBoardStateDto(currentState),
                UndoHistory = history,
                Stats = ToSessionStatsDto(stats),
                SavedAtUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        public static BoardState ToBoardState(BoardStateDto dto)
        {
            int count = dto.Cells?.Length ?? 0;
            var cells = new PlayingCard[count];
            for (int i = 0; i < count; i++)
            {
                var c = dto.Cells[i];
                cells[i] = c == null ? null : new PlayingCard(c.Rank, c.Suit);
            }
            return new BoardState(cells, ToCards(dto.Stock), ToCards(dto.Waste));
        }

        public static IReadOnlyList<BoardState> ToHistory(BoardStateDto[] dtos)
        {
            if (dtos == null) return Array.Empty<BoardState>();
            var list = new List<BoardState>(dtos.Length);
            foreach (var dto in dtos)
                list.Add(ToBoardState(dto));
            return list;
        }

        public static SessionStats ToSessionStats(SessionStatsDto dto)
        {
            return new SessionStats
            {
                Score = dto.Score,
                MoveCount = dto.MoveCount,
                ElapsedSeconds = dto.ElapsedSeconds,
                UndoUsed = dto.UndoUsed,
                HintUsed = dto.HintUsed,
                HintCount = dto.HintCount
            };
        }

        private static BoardStateDto ToBoardStateDto(BoardState state)
        {
            var cells = new CardDto[state.CellCount];
            for (int i = 0; i < state.CellCount; i++)
            {
                var id = new CellId(i);
                cells[i] = state.HasCard(id) ? ToDto(state.CardAt(id)) : null;
            }
            return new BoardStateDto
            {
                Cells = cells,
                Stock = ToDtos(state.Stock),
                Waste = ToDtos(state.Waste)
            };
        }

        private static CardDto ToDto(PlayingCard card) => new CardDto { Rank = card.Rank, Suit = card.Suit };

        private static CardDto[] ToDtos(IReadOnlyList<PlayingCard> cards)
        {
            var arr = new CardDto[cards.Count];
            for (int i = 0; i < cards.Count; i++)
                arr[i] = ToDto(cards[i]);
            return arr;
        }

        private static List<PlayingCard> ToCards(CardDto[] dtos)
        {
            var list = new List<PlayingCard>(dtos?.Length ?? 0);
            if (dtos != null)
                foreach (var c in dtos)
                    list.Add(new PlayingCard(c.Rank, c.Suit));
            return list;
        }

        private static SessionStatsDto ToSessionStatsDto(SessionStats stats)
        {
            return new SessionStatsDto
            {
                Score = stats.Score,
                MoveCount = stats.MoveCount,
                ElapsedSeconds = stats.ElapsedSeconds,
                UndoUsed = stats.UndoUsed,
                HintUsed = stats.HintUsed,
                HintCount = stats.HintCount
            };
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `uloop run-tests --test-mode EditMode`
Expected: PASS (4 tests). Full suite green (381 + 6 new = 387).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Model/Board/BoardSnapshotConverter.cs Assets/Scripts/Model/Board/BoardSnapshotConverter.cs.meta \
        Assets/Tests/EditMode/BoardSnapshotTests.cs Assets/Tests/EditMode/BoardSnapshotTests.cs.meta
git commit -m "feat(board): add BoardSnapshotConverter + round-trip tests"
```

---

## Task 4: Full-suite verification

- [ ] **Step 1:** Force a clean rebuild (refresh + `CompilationPipeline.RequestScriptCompilation()`), poll `uloop compile` to ErrorCount 0.
- [ ] **Step 2:** `uloop run-tests --test-mode EditMode` → all green, 0 failures (expect 387 total, 381 passed, 6 skipped).
- [ ] **Step 3:** `git status --short` should show only the unrelated `ProjectSettings.asset` (if present); nothing else uncommitted.

---

## What this plan defers to plan 2c (Unity integration)

- Board snapshot **file gateway + auto-save service** (the existing `IGameSnapshotRepository` is `GameSnapshot`-typed; the board needs a parallel `IBoardSnapshotRepository` / `LocalBoardSnapshotRepository` or a generalized byte-store — decided in 2c with the gateway).
- Wiring `IBoardScoreRule` into the score HUD (presenter computes deltas, feeds `SessionStats`).
- A ScriptableObject wrapper for the score rule, if Inspector-tuning is wanted.

---

## Self-Review (author checklist — completed)

- **Spec coverage (Standard parity, logic slice):** scoring → Task 1; snapshot model + converter (board state incl. removed cells, undo history, stats, metadata, MemoryPack validity) → Tasks 2–3. Repository/auto-save/HUD wiring are explicitly deferred to 2c (listed) — not gaps.
- **Placeholder scan:** none — every code step is complete. Task 2 has no standalone test by design (DTOs are exercised by Task 3's converter + MemoryPack round-trip).
- **Type consistency:** `BoardStateDto { Cells, Stock, Waste }`, `BoardSnapshot { GameType, Variant, Seed, CurrentState, UndoHistory, Stats, SavedAtUtcTicks }`, `BoardSnapshotConverter.{ToSnapshot, ToBoardState, ToHistory, ToSessionStats}`, `IBoardScoreRule.{ScoreForRemoval, BoardClearedBonus}`, `PyramidScoreRule(perCard, boardClearedBonus)` are referenced identically across tasks/tests. Reuses plan-1 `BoardState` API (`CellCount`/`HasCard`/`CardAt`/`Stock`/`Waste`/`WithCellsRemoved`) and existing `CardDto`/`SessionStatsDto`/`SessionStats` verbatim.
