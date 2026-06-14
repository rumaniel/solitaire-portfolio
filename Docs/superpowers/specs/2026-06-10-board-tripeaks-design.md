# TriPeaks (Board Mode) — Design

**Date:** 2026-06-10
**Status:** Approved (design); pending implementation plan
**Branch:** `feature/board-mode-tripeaks`

## Goal

Add a fully playable **TriPeaks** game to the existing board-mode engine, with authentic
Microsoft-Solitaire-Collection scoring (streak + peak bonuses). TriPeaks reuses the entire board
shell (HUD, undo, hint, snapshot/resume, lifetime stats, play-with-code, win-copy) that Pyramid
already established; only the genuinely game-specific pieces are new.

## Architecture (approved decisions)

1. **Separate service + shared base.** Pyramid's service is left functionally untouched. A new
   `BoardGameServiceBase` holds the byte-for-byte-identical scaffolding; `PyramidGameService` and
   `TriPeaksGameService` subclass it with their own selection/apply/move-detection/hint logic. The
   presenter resolves the right service per `GameType` via a factory.
2. **Extract `IBoardScorer`.** A stateful, presenter-side scorer turns a `(prev, next, won)` state
   transition into `(points, soundKind)`. `PyramidScorer` is a behavior-preserving extraction of the
   current presenter scoring branch; `TriPeaksScorer` owns the streak + peaks-cleared counters.
   Accumulators live in the scorer (domain-model-first rule), never in the immutable score rule.

Both games continue to route to the **same** `BoardScene` via `GameType.IsBoardMode()`; the presenter
switches behavior by the route's game type, exactly as the Pyramid-only path does today.

---

## 1. Ruleset

- **Board topology:** 28 tableau cards arranged as **three peaks**. Rows: 3 apexes / 6 / 9 / a
  continuous **10-card base** (3 + 6 + 9 + 10 = 28). The cover relationship is the same *kind* as
  Pyramid — a card is free once the two cards overlapping it from the next row down are removed.
  Rows 0–1 are three independent mini-triangles; rows 2↔3 form one continuous 10-wide base strip
  that links the peaks. The three-peak **visual separation is a render concern only** (fixed prefab
  anchors), exactly like Pyramid; the logical topology is a single dense cover graph.
- **Cell id assignment** (row-major, peaks-first):
  - Row 0 (apexes): ids `0,1,2` — one tip per peak.
  - Row 1: ids `3..8` — two per peak.
  - Row 2: ids `9..17` — three per peak.
  - Row 3 (base): ids `18..27` — one continuous row of ten.
  - **Apex set** = `{0,1,2}`.
- **Cover graph:**
  - Apex `p` (id `p`, p∈{0,1,2}) is covered by row-1 cells `3+2p` and `4+2p`.
  - Row-1 cell `3+2p` covered by row-2 cells `9+3p`,`10+3p`; cell `4+2p` covered by `10+3p`,`11+3p`.
  - Row-2 cell `9+j` (j∈0..8) covered by base cells `18+j`,`19+j` (a continuous strip; base ids
    `18..27`). Every blocker references a real cell and ids are the dense set `0..27`, so
    `BoardLayout`'s constructor validation passes.
- **Stock / waste:** the remaining `52 − 28 = 24` cards form the stock; at deal time the **first
  stock card is flipped to the waste** so play has an anchor (23 deals remain). **No recycle** —
  single pass, `maxRecycles = 0`.
- **Move:** tap **one** free tableau card. If its rank is **±1 of the current waste-top, with A↔K
  wrap** (Ace plays on King and King plays on Ace), the card moves onto the waste (becomes the new
  top) and its cell clears. Tapping a non-playable free card does nothing (no state change, no
  selection). Tapping the stock flips the next stock card to the waste.
- **Win:** every tableau cell cleared — `!AnyOccupied()` (stock/waste are irrelevant to the win).
- **Stuck:** stock empty **and** no free card is playable on the waste-top.

## 2. Scoring (Microsoft Solitaire Collection; constants tunable)

- **Card cleared:** `50 × streak`. The streak is 1 for the first card cleared after a deal and
  increments by 1 for each consecutive clear (1st = 50, 2nd = 100, 3rd = 150, …).
- **Stock deal:** **−5** points and the streak resets (next clear scores `50 × 1`).
- **Peak tips cleared:** **+500 / +1000 / +5000** for the 1st / 2nd / 3rd peak tip cleared (by order
  of clearing). The 3rd-peak bonus is the win reward — there is no separate board-clear bonus.
- Defaults live in `TriPeaksScoreRule` (ctor params, mirroring `PyramidScoreRule`'s tunable style).

> **Sources:** semicolon.com TriPeaks rules; Microsoft Solitaire TriPeaks expert guide;
> solitairecity Tri-Peaks scoring. (The older arcade 1-2-3 / 15-15-30 scheme was explicitly *not*
> chosen — MSSC is the target.)

## 3. Model changes (additive; zero Pyramid impact)

- `BoardState.WithCardPlayedToWaste(CellId id)` — returns a new state with the cell's card removed
  and that same card appended to the waste (the TriPeaks "play onto waste" mutation). Sits alongside
  the existing `WithCellsRemoved` / `WithWasteTopRemoved` / `WithStockDrawn` / `WithStockRecycled`.
- `TriPeaksLayoutFactory.Create(int variant = 1)` → `BoardLayout` (the §1 cover graph), plus a
  `public static readonly IReadOnlyList<CellId> ApexCellIds` exposing `{0,1,2}`.
- `TriPeaksMatchRule : IBoardMatchRule` — reuses the existing interface with no change.
  `Evaluate([wasteTop, tapped])` returns `Match` iff the two ranks differ by exactly 1 **or** form an
  {Ace, King} pair (wrap); otherwise `Invalid`. (A 1-card selection returns `Incomplete`; the service
  never feeds it one.)

## 4. Service layer

### `BoardGameServiceBase` (abstract)

Holds only what is identical between the two games:

- `IShuffleStrategy`; `Subject<BoardState> stateSubject`; `Subject<SelectionSnapshot> selectionSubject`
  and their observables; `Layout`, `CurrentState`, `CurrentSeed`, `CurrentSelection`.
- Deal + split (deal `layout.Count` cards to cells, the rest to stock).
- `DrawFromStock`, `RecycleStock` / `CanRecycle` (inert when `maxRecycles == 0`).
- Undo stack: `Undo`, `CanUndo`, `UndoHistory`, `PushUndo`.
- `Restore` (validate-everything-then-commit, exactly as today).
- `IsWon` = `!state.AnyOccupied()`.
- `PublishSelection(SelectionSnapshot)` (the idempotent-emission guard), `Dispose`.
- `protected virtual void ResetSelectionState()` — hook called on draw/undo so each game clears its
  own selection representation (Pyramid clears its accumulator list; TriPeaks is a no-op).
- Abstract: `SelectCell`, `SelectWasteTop`, `HasAnyMove`, `GetHints`.
- `protected virtual void OnDealt()` — post-deal hook (TriPeaks flips the first stock card to waste).

### `PyramidGameService : BoardGameServiceBase`

The current `BoardGameService`, reshaped onto the base. Keeps its accumulate-pair / toggle /
sum-to-13-or-King resolve, `SelectWasteTop` as a real selectable target, and its `HasAnyMove` /
`GetHints` (matches + Draw/Recycle fallback) **exactly as shipped**. Owns its
`List<SelectedTarget> selection` accumulator; `ResetSelectionState()` clears it.

### `TriPeaksGameService : BoardGameServiceBase`

- `OnDealt()` — flip the first stock card to the waste (`WithStockDrawn` once) so play can begin; this
  is the deal anchor, **not** a scored draw.
- `SelectCell(id)` — if the cell is free and `TriPeaksMatchRule.Evaluate([WasteTop, card]) == Match`,
  `PushUndo()` then `CurrentState = CurrentState.WithCardPlayedToWaste(id)` and emit. Otherwise
  ignore (no selection state). There is no multi-tap accumulation.
- `SelectWasteTop()` — no-op (the waste-top is the anchor, never a selection target in TriPeaks).
- `HasAnyMove(state)` = `state.Stock.Count > 0` **or** any free cell is playable on `state.WasteTop`.
- `GetHints(state)` — every free cell playable on the waste-top, as `BoardHint.OfMatch` targets
  (single-cell snapshots); else if stock remains, `BoardHint.Draw`; else empty (stuck). Reuses the
  existing `BoardHint` / `BoardHintKind` model unchanged.

### Service selection (DI)

`BoardScene` registers both `PyramidGameService` and `TriPeaksGameService` (Scoped) and a tiny
`IBoardGameServiceFactory` whose `Create(GameType)` returns the matching instance. The presenter
resolves the service for the current route game type at init time (replacing the single injected
`IBoardGameService` property with a per-init-resolved field).

## 5. Scoring layer — `IBoardScorer`

```csharp
public enum BoardScoreEvent { None, Cleared, Draw, Recycle }
public readonly struct BoardScoreOutcome { public int Points; public BoardScoreEvent Event; }

public interface IBoardScorer
{
    void Reset(BoardState initial);                                  // set counters from the start state
    BoardScoreOutcome Evaluate(BoardState prev, BoardState next, bool won);
}
```

- `Event` drives the presenter's move sound (`Cleared` → place/foundation, `Draw` → flip,
  `Recycle` → refresh, `None` → silent). Points feed `SessionStats.RecordScoreDelta`.

### `PyramidScorer(IBoardScoreRule rule)`

Behavior-preserving extraction of today's `OnBoardStateChanged` scoring branch:

- total-card delta (`occupied + stock + waste`) `removed = prevTotal − newTotal`;
- `removed > 0` → `Cleared`, `rule.ScoreForRemoval(removed)` (+ `BoardClearedBonus` when `won`);
- `removed == 0` and recycle-count changed → `Recycle`, `rule.ScoreForRecycle`;
- `removed == 0` otherwise → `Draw`, `rule.ScoreForStockDraw`.
- Stateless: computes `removed` and the recycle-change directly from the supplied `(prev, next)`
  each call, so `Reset` is a no-op (no internal counters to seed). This keeps the prior-state source
  of truth in the presenter, shared by both scorers.

### `TriPeaksScorer(ITriPeaksScoreRule rule, ISet<CellId> apexCells)`

- Holds `int streak` and `int peaksCleared`.
- `Reset(initial)` — `streak = 0`; `peaksCleared` = count of apex cells **already empty** in `initial`
  (so a resumed mid-game scores subsequent peaks at the correct ordinal).
- `Evaluate(prev, next, won)`:
  - **Play** (exactly one cell present in `prev` is absent in `next`, and waste grew by 1):
    `streak++`; `points = rule.PointsForStreak(streak)`; if the removed cell ∈ `apexCells`,
    `peaksCleared++` and `points += rule.PeakBonus(peaksCleared)`. `Event = Cleared`.
  - **Deal** (stock shrank by 1, waste grew by 1, occupancy unchanged): `streak = 0`;
    `points = rule.StockDrawPenalty` (−5). `Event = Draw`.
  - else `(0, None)`.
  - The removed cell is found by scanning the 28 cells for the one occupied in `prev` and empty in
    `next` (a TriPeaks play removes exactly one).

### `ITriPeaksScoreRule`

Its own small interface — the shape differs from `IBoardScoreRule`, so it stays separate rather than
widening the Pyramid contract:

```csharp
public interface ITriPeaksScoreRule
{
    int PointsForStreak(int streak);   // default: 50 * streak
    int PeakBonus(int peakOrdinal);    // default: 500 / 1000 / 5000 for ordinal 1 / 2 / 3
    int StockDrawPenalty { get; }      // default: -5
}
```

`TriPeaksScoreRule` implements it with the §2 defaults via ctor params.

## 6. Presenter (`BoardPresenter`)

- The init `switch` gains a `TriPeaks` case: `layout = TriPeaksLayoutFactory.Create(variant)`,
  `matchRule = new TriPeaksMatchRule()`, `scorer = new TriPeaksScorer(new TriPeaksScoreRule(),
  apexSet)`, `maxRecycles = 0`. The `Pyramid` case builds `new PyramidScorer(new PyramidScoreRule())`
  and keeps `maxRecycles = 3`.
- `BoardGameService` is resolved from `IBoardGameServiceFactory` for the current game type and stored
  in a field; all existing `BoardGameService.*` call sites are unchanged (field vs property only).
- `OnBoardStateChanged`'s scoring branch becomes:
  `var outcome = scorer.Evaluate(prev, next, won); if (...guards...) { RecordScoreDelta(outcome.Points);
  PlaySoundFor(outcome.Event); }`. The `prev` state is captured before applying `next` (the presenter
  already holds the prior state via its `prev*` fields; it will hold the prior `BoardState` instead).
  Win / stuck / undo / hint flow is unchanged.
- Stock-tap handler unchanged — for TriPeaks the empty-stock `RecycleStock` is inert (`CanRecycle`
  false), so tapping an exhausted stock does nothing, as required for a single pass.
- Snapshot/resume, hints, lifetime stats, play-with-code, win-copy are inherited unchanged.
  **Streak resets to 0 on resume** — it is ephemeral (resets on every deal) and not derivable from
  the board state; the accumulated score itself is persisted via `SessionStats`. This is a minor,
  documented imperfection, not a correctness bug.

## 7. View / Scene / Prefab

- **`BoardAnchorSet` component** — extract the three serialized anchor fields (`cellAnchors[]`,
  `stockAnchor`, `wasteAnchor`) out of `UIBoardController` into a small component. The controller
  renders against the **active** set and gains `UseAnchorSet(BoardAnchorSet set)`. Pyramid keeps its
  current anchors as one `BoardAnchorSet`. This is the only `UIBoardController` change and is
  mechanical (Pyramid keeps rendering against its set).
- **`TriPeaksBoard` prefab** — a sibling of `PyramidBoard.prefab` providing the TriPeaks visuals: 28
  card anchors positioned as three peaks + a 10-card base (its own `BoardAnchorSet`), plus the shared
  stock/waste anchors. Built the Inspector-first / prefab-variant way.
- **`BoardScene`** holds both board subtrees (Pyramid, TriPeaks). At init the presenter activates the
  subtree matching the game type and calls `boardController.UseAnchorSet(setForGameType)`. The
  inactive board subtree is disabled so only the active peak art shows.

## 8. Data / Lobby / Localization

- A `TriPeaks` `GameVariant` asset (`GameType = TriPeaks`, `variantId = 1`, display name, preview
  icon), mirroring `Assets/ScriptableObjects/GameVariants/Pyramid.asset`.
- A Lobby tile wired to that variant (routes to `BoardScene` via `IsBoardMode()`), plus a localized
  display-name entry in the UI localization table, mirroring the Pyramid tile.

## 9. Testing (EditMode, NUnit, TDD)

- `TriPeaksLayoutFactoryTests` — 28 cells, dense `0..27` ids, apex set `{0,1,2}`, the §1 cover graph,
  and `BoardLayout` construction succeeds (no duplicate/dangling blockers).
- `TriPeaksMatchRuleTests` — ±1 matches, A↔K wrap matches, non-adjacent invalid, same-rank invalid.
- `BoardStateTests` — `WithCardPlayedToWaste` removes the cell and pushes its card to the waste-top;
  immutability preserved.
- `TriPeaksGameServiceTests` — deal flips the first waste card; a playable tap moves the card to
  waste and frees its covered neighbors; a non-playable tap is ignored; stock draw; no recycle;
  win on full clear; stuck when stock empty and nothing playable; undo reverts a play and a draw;
  restore round-trips.
- `TriPeaksScorerTests` — streak escalation (50,100,150…), reset to `50×1` after a deal, the −5 deal
  penalty, peak ordinals (500/1000/5000) by clear order, and `Reset` deriving `peaksCleared` from an
  already-cleared-apex start state.
- `PyramidScorerTests` — parity with the previously inline behavior (removal, draw, recycle, clear
  bonus), guarding the extraction.

## 10. Execution phasing

One spec; the implementation plan splits into phases mirroring the original Pyramid build:

- **P1 — Logic:** `BoardState.WithCardPlayedToWaste`, `TriPeaksLayoutFactory`, `TriPeaksMatchRule`,
  `BoardGameServiceBase` + `PyramidGameService` reshaping + `TriPeaksGameService`, `IBoardScorer` +
  `PyramidScorer` + `TriPeaksScorer` + `TriPeaksScoreRule`, and all EditMode tests. No scene work;
  fully unit-tested.
- **P2 — Presenter & DI:** `IBoardGameServiceFactory`, presenter switch/scorer/service wiring, the
  scoring-branch swap to `IBoardScorer`. Compiles and passes the existing regression suite.
- **P3 — Scene / Editor:** `BoardAnchorSet` extraction, `TriPeaksBoard` prefab, `BoardScene`
  two-subtree wiring, `TriPeaks` `GameVariant` + Lobby tile + localization, then in-editor
  play-verification (deal, play streak, peak bonus, win, stuck, undo, resume, hint, play-with-code).

## Non-goals (deferred, consistent with Pyramid's rollout)

- Daily challenge and achievements for TriPeaks (separate brainstorm, as agreed for Pyramid).
- Persisting the in-flight streak across an app-kill resume (reset-to-0 is accepted).
- Any change to Pyramid's gameplay, scoring values, or assets.
