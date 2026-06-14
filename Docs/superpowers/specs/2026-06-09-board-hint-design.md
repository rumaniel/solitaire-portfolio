# Board Hint — Design Spec

**Date:** 2026-06-09
**Status:** Approved (design)
**Scope:** Wire a working Hint affordance for Pyramid board mode (and future board games), surfacing a valid move to the player.

---

## Problem

Board mode reuses the shared `IngameShellView`, which already renders a **Hint button** (`OnHintObservable()`, `[Button("Hint")]`). `BoardPresenter` never wired it — line 242 marks it "deferred to 2c". So tapping Hint in Pyramid does nothing. The board service can answer *whether* a move exists (`HasAnyMove`, a bool) but cannot say *which* move, so there is nothing to show.

This spec adds the "which move" computation and wires the existing button: highlight a removable match, fall back to draw/recycle suggestions, and play the hint/no-hint sounds — mirroring the card game's hint UX but far lighter.

## Goals

- Tapping Hint highlights a genuinely useful next move.
- Cycle through all available matches on repeated taps (wrap-around).
- When no match exists, suggest drawing the stock; if the stock is empty, suggest recycling; if truly stuck, play the no-hint sound.
- Reuse the existing selection-glow visual (no new card art).
- Record hint usage in session stats (parity with the card game).

## Non-Goals (YAGNI)

- No separate `IBoardHintService` — fold into `BoardGameService` (board hint logic is trivial vs. card hints).
- No move-preview animation (the card game's `AnimateHintPreview` ghost-card is out of scope).
- No auto-complete / auto-move. Hint **only highlights**; the player still taps to act.
- No new prefab/scene edits — the Hint button already renders in the board shell.

---

## Design

### 1. Model — `Assets/Scripts/Model/Board/BoardHint.cs`

Immutable, `IEquatable<BoardHint>` (per project convention: readonly props, value equality, `GetHashCode`).

```csharp
public enum BoardHintKind { Match, Draw, Recycle }

public sealed class BoardHint : IEquatable<BoardHint>
{
    public BoardHintKind Kind { get; }
    public SelectionSnapshot Targets { get; }   // cells/waste to glow for Match; Empty for Draw/Recycle
    // ctor, Equals, GetHashCode, ==/!=
    // convenience: BoardHint.OfMatch(SelectionSnapshot), BoardHint.Draw, BoardHint.Recycle
}
```

- **Match** → `Targets` holds the cell(s) (one King, or a pair summing 13) plus the waste flag when the waste-top participates.
- **Draw / Recycle** → `Targets = SelectionSnapshot.Empty`; the stock pile is the affordance.
- **Stuck** → represented by an **empty hint list**, not an enum value.

`SelectionSnapshot` (existing, `Model.Board`) is reused unchanged as the Match target carrier — it already models "cells + waste-top", exactly a match's shape.

### 2. Service — `IBoardGameService.GetHints`

```csharp
/// <summary>Available next moves for the Hint button, best-first: all removable matches,
/// else a single Draw or Recycle suggestion, else empty (stuck).</summary>
IReadOnlyList<BoardHint> GetHints(BoardState state);
```

Implementation in `BoardGameService`:

1. Enumerate valid matches among **free cells + waste-top** (lone King; any pair summing 13), deduped (a symmetric pair appears once) → one `BoardHint` of `Kind.Match` each, `Targets` = the participating cells (+ waste flag).
2. If matches found → return them.
3. Else if `state.Stock.Count > 0` → `[BoardHint.Draw]`.
4. Else if `CanRecycle(state)` → `[BoardHint.Recycle]`.
5. Else → `Array.Empty<BoardHint>()`.

**Refactor (DRY):** the free-cell + waste candidate enumeration and pair/single rule evaluation currently inside `HasAnyMove` is extracted into a private helper (e.g. `EnumerateMatches(state)`) that yields the matched target sets. `HasAnyMove` becomes "stock/recycle available OR `EnumerateMatches` is non-empty". `GetHints` consumes the same helper. No behavior change to `HasAnyMove`.

Match enumeration stays single/pair only (same assumption `HasAnyMove` already documents); a future 3+-card rule would widen both together.

### 3. Presenter — `BoardPresenter.HandleHint`

Wire the existing button in `WireShellButtons` (replacing the "hint deferred" note):

```csharp
Shell.OnHintObservable().Subscribe(_ => HandleHint()).AddTo(disposable);
```

`HandleHint` mirrors `IngamePresenter.HandleHint`, simplified:

- Maintain `private IReadOnlyList<BoardHint> currentHints; private int hintIndex;`
- If `currentHints == null || hintIndex >= currentHints.Count` → `currentHints = BoardGameService.GetHints(CurrentState); hintIndex = 0;` (recompute + wrap).
- If `currentHints.Count == 0` → `AudioService.Play(AudioCatalog.Game.NoHint)`; return.
- `var hint = currentHints[hintIndex++];`
- `SessionStats.RecordHintUsed();`
- `AudioService.Play(AudioCatalog.Game.Hint);`
- Visual:
  - `Match` → `BoardController.SetSelection(hint.Targets); BoardController.SetStockHighlight(false);`
  - `Draw` / `Recycle` → `BoardController.SetSelection(SelectionSnapshot.Empty); BoardController.SetStockHighlight(true);`

**Hint cache invalidation & no lingering glow** — in `OnBoardStateChanged` (after the existing render):

```csharp
currentHints = null; hintIndex = 0;                                  // stale after any move
BoardController.SetSelection(BoardGameService.CurrentSelection);      // clears stale match glow
BoardController.SetStockHighlight(false);                             // clears stale stock glow
```

A real selection change already routes through the existing `OnSelectionChanged → SetSelection(sel)` subscription; that subscription also calls `BoardController.SetStockHighlight(false)` so tapping a cell after a Draw hint drops the stock glow.

### 4. Controller — `UIBoardController.SetStockHighlight`

```csharp
/// <summary>Glow the stock pile as a Draw/Recycle hint affordance (selection-glow reused).</summary>
public void SetStockHighlight(bool on)
{
    if (stockCard != null) stockCard.SetHighlight(on);
}
```

No other controller changes; match glow reuses the existing `SetSelection` path (sole driver of per-card highlight).

---

## Data Flow

```
Shell Hint button → OnHintObservable → HandleHint
   → GetHints(CurrentState)           [service: enumerate matches → fallback]
   → cache + advance index (cycle)
   → RecordHintUsed + Game.Hint  (or Game.NoHint if empty)
   → SetSelection(targets) / SetStockHighlight(true)   [reuse selection glow]
Any move → OnBoardStateChanged → invalidate cache + re-assert real selection + clear stock glow
```

## Edge Cases

- **Empty board / won:** `GetHints` returns `[]` → no-hint sound. (Hint after win is harmless; session is finished.)
- **Pending selection when Hint tapped:** the recompute reflects the current board; showing the hint glow via `SetSelection(targets)` overwrites the pending-selection glow. The service's actual selection is untouched, so the next real tap proceeds normally and re-drives the glow.
- **Single match, repeated taps:** list has one entry; index wraps → re-shows the same match each tap. Correct.
- **Draw vs Recycle precedence:** stock-not-empty always suggests Draw; Recycle only when stock empty and `CanRecycle`. Matches the player's only legal stock action in each state.

## Testing — EditMode (`BoardGameServiceTests`)

`GetHints` returns:
- lone King among free cells → one `Match` with that cell.
- sum-13 pair among free cells → one `Match` with both cells.
- waste-top + free cell summing 13 → one `Match` with the cell + waste flag.
- no match, stock non-empty → `[Draw]`.
- no match, stock empty, recyclable → `[Recycle]`.
- no match, stock empty, not recyclable → `[]` (stuck).
- two independent matches present → count == 2 (cycle source).
- `HasAnyMove` parity unchanged after the `EnumerateMatches` refactor (existing tests stay green).

Presenter/controller wiring is verified in play mode (tap Hint → glow + `game.hint`; stuck → `game.no_hint`).

## Effort

~1 model file, 1 service method + small `HasAnyMove` refactor, ~25 lines presenter wiring, 1 controller method, ~7 EditMode tests. No asset/prefab/scene edits.
