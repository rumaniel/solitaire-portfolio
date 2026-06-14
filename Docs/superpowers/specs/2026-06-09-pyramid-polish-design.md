# Pyramid Polish — Sum-13 Reference Table + Match Removal Animation (Design)

**Status:** Approved (2026-06-09)
**Slice:** Partial Plan 2c (the deferred "removal animation" polish) + one new affordance (the sum-13 reference table). Builds on the playable Pyramid slice (`feature/board-mode-pyramid`).

## Goal

Make a Pyramid match feel deliberate and readable:
1. **Reference table** — an always-on top-right chart of the sum-13 pairings, so the player can see at a glance which ranks match (like Microsoft Pyramid Solitaire).
2. **Removal animation** — matched cards spin and fly straight down off the bottom of the screen (akin to Klondike's win-clear cascade) instead of vanishing instantly.

**No audio change.** A match keeps the existing `card.place` sound; the emphasis comes from the animation. (Audio remains on/off only — no new clips, no settings.)

**Pyramid-only.** Both features live on the Pyramid board prefab and never touch the shared shell or the card games.

## Non-goals (still deferred)

Snapshot auto-save/resume, lifetime-stats on win, board **Hint** (move suggestion + the Hint button stays hidden), daily/achievements, score-tuning asset, win/stuck visual flourishes beyond the removal animation. A dedicated match sound clip is explicitly out (decision: keep `card.place`).

---

## Feature A — Sum-13 reference table (`PairGuide`)

A **static** UI panel. No script, no runtime logic — pure Inspector/prefab content (CLAUDE.md "Inspector-binding-first").

**Content (7 rows, top → bottom):**

```
 K
 Q  A
 J  2
10  3
 9  4
 8  5
 7  6
```

Each row lists ranks whose pip values sum to 13 (King = 13, removed alone). Rendered as TMP text in a small bordered legend.

**Placement:** anchored to the **top-right** of the play area, always visible during play.

**Prefab change required:** the `PyramidBoard.prefab` root `RectTransform` is currently centered with zero size (`anchorMin = anchorMax = (0.5, 0.5)`, `sizeDelta = 0`). To let a child anchor to the top-right *corner of the play area*, change the root to **full-stretch** (`anchorMin = (0,0)`, `anchorMax = (1,1)`, `offsets = 0`), keeping **pivot = (0.5, 0.5)**.

- Invariant preserved: with a full-stretch rect and center pivot, a child anchored at `(0.5, 0.5)` with the same `anchoredPosition` resolves to the same world point as before. The 28 cell anchors (and stock/waste anchors) keep their exact `anchoredPosition` values → **the pyramid does not move**. This is verified by construction, but re-checked visually in the play-gate.

**Structure:** `PairGuide` is a child of the `PyramidBoard` root, `RectTransform` anchored top-right (`anchorMin = anchorMax = (1,1)`, `pivot = (1,1)`, `anchoredPosition = (-margin, -margin)`), holding a vertical list of 7 rows. It travels with the board prefab (the board owns its own rule legend).

---

## Feature B — Match removal animation (`BoardRemovalAnimator`)

Matched cards **spin and fly straight down off-screen**, then despawn.

### New component: `BoardRemovalAnimator` (`Assets/Scripts/Component/Board/BoardRemovalAnimator.cs`, namespace `Component.Board`)

MonoBehaviour, Inspector-tunable (CLAUDE.md Inspector-first). Mirrors `CardMoveAnimator`'s manual `UniTask` lerp style (no DOTween in the project):

```
[SerializeField] private RectTransform overlayRoot;        // flying cards reparent here (renders above cells)
[SerializeField] private float duration = 0.6f;
[SerializeField] private float spinDegrees = 540f;         // total Z rotation over the flight
[SerializeField] private float extraFallPadding = 200f;    // ensures it clears the bottom edge
[SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
[SerializeField] private bool fadeOut = true;              // fade alpha over the last portion
```

```
public void AnimateRemoval(UICard card);
```

Behaviour of `AnimateRemoval`:
1. Reparent the **real** `card` to `overlayRoot` with `worldPositionStays = true`, `SetAsLastSibling()` (renders above remaining cells).
2. Disable interaction (`card.Disable()`) so a flying card can't be tapped.
3. Run a `UniTaskVoid` lerp (same pattern as `CardMoveAnimator.AnimateMoveInternalAsync`): over `duration`, translate `anchoredPosition.y` from the start value down to `-(screenHeightInLocalUnits + cardHeight + extraFallPadding)` (off the bottom), and rotate local Z from `0` to `spinDegrees * direction`; if `fadeOut`, lerp `CanvasGroup.alpha` to 0 over the final ~30%.
4. `finally`: `Destroy(card.gameObject)`.
5. Cancellation: link to the animator's destroy token (`GetCancellationTokenOnDestroy()`) so a scene unload cancels in-flight flights and the `finally` still destroys the card. Track active flights so `OnDestroy` cleans up.

The animator owns no game state — it is a fire-and-forget visual.

### Hook: `UIBoardController`

- Add `[SerializeField] private BoardRemovalAnimator removalAnimator;`.
- `DespawnCell(int value)` (a single removal mid-play = a match): remove the card from `cardByCell`/`cellByCard` and unsubscribe **immediately** (controller state stays consistent), then — instead of `Destroy(view.gameObject)` — call `removalAnimator.AnimateRemoval(view)` if the animator is wired (fail-fast if unexpectedly null is acceptable; it is a required serialized field). The animator takes ownership of destroying the card.
- `DespawnAll()` (new-game teardown) is **unchanged** — instant `Destroy`, no animation. Bulk reset must be immediate.

Rationale for animating the real card (not a ghost like the win cascade): the card is being removed anyway, so reparent-tween-destroy is simpler than spawning a ghost, and there is no "real card to keep in place."

### Prefab wiring (`PyramidBoard.prefab`)

- Add an `overlayRoot` `RectTransform` (full-stretch child of the board root, **last sibling** so it renders on top).
- Add the `BoardRemovalAnimator` component (on the board root or a dedicated child); serialize `overlayRoot`.
- Serialize `removalAnimator` into `UIBoardController`.

---

## Edge cases (accepted for v1, cosmetic only)

- **Undo within the ~0.6s flight:** the restored card spawns at its anchor while the previous instance is still flying off. Brief visual overlap. Acceptable; can be tightened later by tracking/cancelling per-cell flights.
- **Win timing:** the final clear's cards may still be flying when the win panel appears (the presenter shows it after `PlayWinEffectAsync`, which is a no-op in the board variant). Acceptable.

Neither affects game state — removals are driven by `BoardState`, which is already updated before the visual plays.

---

## Files

**Create:**
- `Assets/Scripts/Component/Board/BoardRemovalAnimator.cs` (+ `.meta`)

**Modify (code):**
- `Assets/Scripts/Component/Board/UIBoardController.cs` — add `removalAnimator` field; route `DespawnCell` through it.

**Modify (prefab/scene, manual gate):**
- `Assets/Prefabs/Board/PyramidBoard.prefab` — root → full-stretch; add `PairGuide` (static legend, top-right); add `overlayRoot` + `BoardRemovalAnimator`; wire `UIBoardController.removalAnimator`.

No assembly changes (`Component.Board` already in `Component.asmdef`). No model/service changes. No audio changes.

---

## Testing / verification

- **Compile** 0 errors / 0 warnings after the C# changes.
- **EditMode** unchanged (no logic touched) — count stays ≥ baseline, 0 failed. No new unit tests (animation + static UI are not EditMode-testable; verified by play).
- **Pyramid play-gate (manual/automated):**
  - Reference table visible top-right, all 7 rows correct.
  - Pyramid geometry unchanged after the root full-stretch change (cells in the same place).
  - A match → both cards **spin and fly down off the bottom**, then disappear; HUD/score still update.
  - New game / restart → instant clear (no fly-out on teardown).
- **Regression:** Klondike (and Easthaven) play unchanged — they never reference `UIBoardController`/`BoardRemovalAnimator`.

---

## What this enables

The `overlayRoot` + `BoardRemovalAnimator` are reusable for TriPeaks/Mahjong removals later. The `PairGuide` pattern (a static rule legend in the board prefab) generalizes to other board rules (e.g., a TriPeaks rank-ladder legend) by swapping the static content.
