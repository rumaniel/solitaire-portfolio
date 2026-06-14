# TriPeaks Gameplay Polish — Design

**Date:** 2026-06-10
**Status:** Approved (design)
**Scope:** Three TriPeaks-only board-mode polish items. No Pyramid behavior change. No responsive/multi-device layout.

## Goal

Make a TriPeaks play feel correct and tactile, give invalid taps real feedback, and fix the off-screen/too-small board so the game is comfortably playable on the target portrait device.

---

## Item 1 — Play animates the card to the waste (not off-screen)

### Problem
A TriPeaks play clears the tapped cell and makes that same card the new waste-top. The renderer currently treats the cleared cell as a generic Pyramid removal: `UIBoardController.DespawnCell` → `BoardRemovalAnimator.AnimateRemoval`, which "spins the card and flies it straight down off the bottom edge, then destroys it." So the played card exits downward and a fresh waste card pops in instantly — wrong and jarring.

### Design
Add a **fly-to-waste** flight, used only when a cell's card becomes the new waste-top.

- `UIBoardController.RenderBoard` captures `state.WasteTop` (and the prior waste count) **before** reconciling cells.
- During cell reconciliation, when a cell is being despawned *and* `animateRemovals` is true *and* the despawned card's identity equals the captured new waste-top *and* the waste grew by one (a play, not a Pyramid match), route that `UICard` to a fly-to-waste tween instead of `AnimateRemoval`:
  - reparent to the overlay root (renders above cells), slide from the cell position to `wasteAnchor`'s position, easing out (~0.22–0.28s), lerping scale to the waste card's scale.
  - on arrival, hand the **same** `UICard` to the controller as the new `wasteCard` (re-parent under `wasteAnchor`, re-wire the waste tap listener); do **not** spin it out or destroy it.
- `RenderWaste` must **not** also spawn a duplicate waste card for this transition. Coordinate via a one-shot "waste adopted this frame" flag set by the flight path, so `RenderWaste` skips the instant spawn when the flight is delivering the card.
- The flight is fire-and-forget (UniTask), matching `BoardRemovalAnimator`/`CardMoveAnimator` style, and is cancel-safe on scene teardown.

### Components
- New animator method (in `BoardRemovalAnimator`, or a small sibling `BoardPlayAnimator`): `FlyToWaste(UICard card, RectTransform wasteAnchor, float targetScale, Action onArrived)`.
- `UIBoardController` owns the detection + adoption; the presenter is unchanged.

### Non-goals
Pyramid removals keep the existing spin-off-bottom animation unchanged.

---

## Item 2 — Invalid tap shakes the card and plays a sound

### Problem
Tapping a **free** card whose rank is not adjacent to the waste-top is silently ignored (`TriPeaksGameService.SelectCell` does `if (!IsPlayable) return;`). No feedback.

### Design
- **`UICard.Shake()`** — a short horizontal shake (UniTask lerp, ~0.3s, a few damped oscillations), restoring the original `anchoredPosition`; no state/selection change; ignores re-entrancy (a shake already running is not restarted or is restarted cleanly).
- **`BoardGameServiceBase`** exposes `Observable<CellId> OnInvalidTap` (a `Subject<CellId>` that, by default, never emits). Disposed with the other subjects.
- **`TriPeaksGameService.SelectCell`**: in the existing "free but not playable" branch, emit `OnInvalidTap.OnNext(id)` instead of a bare `return`. (Covered cells still no-op silently — only a *free* but non-adjacent tap is "invalid".)
- **`BoardPresenter`** subscribes to `OnInvalidTap` → `BoardController.ShakeCell(id)` + `AudioService.Play(AudioCatalog.Card.MoveRejected)`.
- **`UIBoardController.ShakeCell(CellId)`** — looks up the spawned `UICard` for that cell and calls `Shake()`.

### Non-goals
Pyramid is unchanged. Its multi-tap pair-accumulation model has no single "invalid tap" (the first tap is never invalid; a non-matching second tap restarts the selection). Pyramid inherits the never-emitting default.

---

## Item 3 — Re-tune the board layout (fixed anchors, no scale-down)

### Problem
The 10-card base row is the width constraint. The board was globally scaled down to fit, but a uniform scale shrinks the inter-card gaps too, wasting width and making cards needlessly small.

### Design (editor-only, no code)
Re-tune `Assets/Prefabs/Board/TriPeaksBoard.prefab`:
- Remove the global scale-down (board root + any per-card scale hacks back to 1, except deliberate stock/waste scaling).
- Size the cards so the **10-card base fills the play-area width** with small symmetric side margins (card width ≈ playWidth / ~10.x; height keeps the 98:133 aspect).
- **Overlap the peak rows vertically** (vertical row step < card height) so the three peaks + base + waste/stock all fit under the HUD without scaling.
- Keep the apex→base anchor ordering (so later rows render over earlier ones, matching the cover graph).
- Iteratively verify with live `uloop` screenshots at the real device resolution until the whole board fits and cards read clearly.

### Non-goals
No runtime/responsive layout component; this tunes one portrait target. (Responsive layout is a possible future item.)

---

## Architecture & data flow summary

- **Service layer:** `BoardGameServiceBase.OnInvalidTap` (new Observable, default silent); `TriPeaksGameService` emits it. Immutable model unchanged; the subject is service-private state exposed as a read-only `Observable`.
- **Presenter:** subscribes to `OnInvalidTap`; routes to controller shake + `AudioService`. Otherwise unchanged.
- **Component layer:** `UICard.Shake()`; `UIBoardController` gains play-to-waste detection/adoption + `ShakeCell`; animator gains `FlyToWaste`.
- **Editor/asset:** `TriPeaksBoard.prefab` anchor + size re-tune.

## Testing

- **Unit (EditMode):** `TriPeaksGameService` emits `OnInvalidTap` for a free non-adjacent tap; does **not** emit for a playable tap, a covered cell, or when there is no waste-top. The full existing suite stays green.
- **Play-verify (screenshots):** the played card slides to the waste; an invalid tap shakes + thuds; the board fits the screen with comfortably-sized cards; Pyramid still plays unchanged (regression).

## Out of scope (whole feature)

- Any Pyramid behavior, animation, or layout change.
- Responsive/multi-device board layout.
- New audio assets (reuses `card.move_rejected`).
