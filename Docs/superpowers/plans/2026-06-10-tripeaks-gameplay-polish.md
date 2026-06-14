# TriPeaks Gameplay Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three TriPeaks-only board polish items — a played card flies to the waste (not off-screen), an invalid tap shakes the card + plays a sound, and the board is re-laid-out so it fits the screen with comfortably-sized cards.

**Architecture:** Service emits a new `OnInvalidTap` Observable (default silent; only TriPeaks emits); presenter routes it to a `UICard.Shake()` + the `card.move_rejected` sound. The renderer (`UIBoardController`) detects a play-to-waste (a despawned cell whose card is the new waste-top) and flies that same `UICard` to the waste anchor, adopting it as the waste card instead of spinning it off-screen. The board layout is re-tuned in the prefab (no code). Pyramid is untouched throughout.

**Tech Stack:** Unity 6, C#, VContainer (DI), R3 (Observables), UniTask (animation), NUnit (EditMode), `uloop` CLI.

**Spec:** `Docs/superpowers/specs/2026-06-10-tripeaks-gameplay-polish-design.md`

**Build/test commands (every task that changes `.cs`):**
- Force a real recompile after editing `.cs`:
  `uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"`
- Poll `uloop compile` (compile-only) until it stops returning "compiling"/"Domain Reload"; expect `ErrorCount: 0`.
- Run tests with a SINGLE uninterrupted call (do NOT interleave `uloop compile` while a test run is pending — that re-dirties the editor and the run never settles): `uloop run-tests --test-mode EditMode` with a long timeout. Expect the suite green.
- Baseline before this plan: EditMode 474 tests / 468 passed / 0 failed / 6 skipped.

---

## Task 1: `OnInvalidTap` service signal (TDD)

A new Observable on the board service that fires when a *free* card is tapped but cannot be played. Default never emits; only `TriPeaksGameService` emits it. This is pure C# and fully unit-testable.

**Files:**
- Modify: `Assets/Scripts/Service/BoardGameService/IBoardGameService.cs`
- Modify: `Assets/Scripts/Service/BoardGameService/BoardGameServiceBase.cs`
- Modify: `Assets/Scripts/Service/BoardGameService/TriPeaksGameService.cs`
- Test: `Assets/Tests/EditMode/TriPeaksGameServiceTests.cs`

- [ ] **Step 1: Write the failing tests** — append inside `TriPeaksGameServiceTests` (before the class closing brace). These reuse the file's existing `FixedShuffle`, `Card`, and `FlatLayout` helpers:

```csharp
        [Test]
        public void SelectCell_FreeButNotPlayable_EmitsOnInvalidTap()
        {
            // cell0 = Five; waste-top after deal = Nine (not adjacent) → invalid tap.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);

            CellId? fired = null;
            using var _ = svc.OnInvalidTap.Subscribe(id => fired = id);

            svc.SelectCell(new CellId(0));

            Assert.IsTrue(fired.HasValue, "invalid tap should emit");
            Assert.AreEqual(new CellId(0), fired.Value);
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)), "card not played");
        }

        [Test]
        public void SelectCell_PlayableCard_DoesNotEmitOnInvalidTap()
        {
            // cell0 = Eight; waste-top = Nine (adjacent) → valid play, no invalid signal.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);

            bool fired = false;
            using var _ = svc.OnInvalidTap.Subscribe(_2 => fired = true);

            svc.SelectCell(new CellId(0));

            Assert.IsFalse(fired, "a playable tap must not emit invalid");
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)), "card was played");
        }

        [Test]
        public void SelectCell_CoveredCell_DoesNotEmitOnInvalidTap()
        {
            // Real layout: base cells are free, row-2+ are covered. Tap a covered apex → silent no-op.
            var svc = new TriPeaksGameService(new FisherYatesShuffleStrategy());
            svc.Initialize(TriPeaksLayoutFactory.Create(), new TriPeaksMatchRule(), seed: 7);

            bool fired = false;
            using var _ = svc.OnInvalidTap.Subscribe(_2 => fired = true);

            svc.SelectCell(new CellId(0)); // apex 0 is covered at deal

            Assert.IsFalse(fired, "a covered (locked) cell must not emit invalid");
        }
```

- [ ] **Step 2: Run to verify it fails**

Run: force-recompile, poll `uloop compile`, then `uloop run-tests --test-mode EditMode`.
Expected: compile error — `OnInvalidTap` not defined.

- [ ] **Step 3a: Declare it on the interface** — in `IBoardGameService.cs`, add after the `OnSelectionChanged` / `CurrentSelection` lines (the file already has `using Model.Board;` and `using R3;`):

```csharp
        /// <summary>Fires when a free card is tapped but cannot be played (e.g. TriPeaks rank mismatch),
        /// so the View can give invalid-move feedback. Default implementations never emit.</summary>
        Observable<CellId> OnInvalidTap { get; }
```

- [ ] **Step 3b: Implement on the base** — in `BoardGameServiceBase.cs`:

Add the subject next to the existing `selectionSubject` field (line ~19):
```csharp
        private readonly Subject<CellId> invalidTapSubject = new();
```
Add the observable next to `OnSelectionChanged` (line ~30):
```csharp
        public Observable<CellId> OnInvalidTap => invalidTapSubject;
```
Add a protected emit helper (place it near the other protected helpers, e.g. just after `EmitSelection`):
```csharp
        /// <summary>Signals that a free cell was tapped but the move was rejected (View shows shake/sound).</summary>
        protected void EmitInvalidTap(CellId id) => invalidTapSubject.OnNext(id);
```
In `Dispose()`, add alongside `selectionSubject.Dispose();`:
```csharp
            invalidTapSubject.Dispose();
```

- [ ] **Step 3c: Emit it from TriPeaks** — in `TriPeaksGameService.cs`, change `SelectCell` so the rejected-but-free branch emits. Replace:

```csharp
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
```
with:
```csharp
        public override void SelectCell(CellId id)
        {
            if (!BoardRules.IsFree(Layout, CurrentState, id)) return; // covered cell: silent no-op
            var top = CurrentState.WasteTop;
            if (top == null) return;
            if (!IsPlayable(CurrentState.CardAt(id), top))
            {
                EmitInvalidTap(id); // free but rank-mismatched → invalid-move feedback
                return;
            }

            PushUndo();
            PublishState(CurrentState.WithCardPlayedToWaste(id));
            EmitSelection(SelectionSnapshot.Empty);
        }
```

- [ ] **Step 4: Run to verify it passes**

Run: force-recompile, poll `uloop compile` (`ErrorCount: 0`), then `uloop run-tests --test-mode EditMode`.
Expected: all green — the 3 new tests pass; no regressions (suite count rises by 3).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Service/BoardGameService/IBoardGameService.cs Assets/Scripts/Service/BoardGameService/BoardGameServiceBase.cs Assets/Scripts/Service/BoardGameService/TriPeaksGameService.cs Assets/Tests/EditMode/TriPeaksGameServiceTests.cs
git commit -m "feat(board): OnInvalidTap signal; TriPeaks emits on a free non-playable tap"
```

---

## Task 2: `UICard.Shake()` + invalid-tap feedback wiring

The visual half of invalid feedback: a card shake and the rejected-move sound. No unit test (it is a visual UniTask animation + a UI wiring); verified at compile time here and by play-verify in Task 5.

**Files:**
- Modify: `Assets/Scripts/Component/Card/UICard.cs`
- Modify: `Assets/Scripts/Component/Board/UIBoardController.cs`
- Modify: `Assets/Scripts/Scene/Board/BoardPresenter.cs`

- [ ] **Step 1: Add `Shake()` to `UICard`**

Add `using Cysharp.Threading.Tasks;` to the top of `UICard.cs` (next to the existing `using UnityEngine;`). Add this field next to the other private fields (e.g. after `private bool dragAccepted = true;`):
```csharp
        private bool shaking; // guards against overlapping shake animations
```
Add these methods (place them after `SetHighlight`, before the `#region Event Handlers`):
```csharp
        /// <summary>Brief horizontal shake for invalid-move feedback. Restores the original position;
        /// changes no card state. Ignores the call if a shake is already running.</summary>
        public void Shake(float amplitude = 16f, float duration = 0.3f)
        {
            if (shaking || rectTransform == null) return;
            ShakeAsync(amplitude, duration).Forget();
        }

        private async UniTaskVoid ShakeAsync(float amplitude, float duration)
        {
            shaking = true;
            Vector2 origin = rectTransform.anchoredPosition;
            try
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (rectTransform == null) return;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    // ~3 damped oscillations, amplitude decaying to 0
                    float offset = Mathf.Sin(t * Mathf.PI * 6f) * amplitude * (1f - t);
                    rectTransform.anchoredPosition = origin + new Vector2(offset, 0f);
                    await UniTask.Yield();
                }
            }
            finally
            {
                if (rectTransform != null) rectTransform.anchoredPosition = origin;
                shaking = false;
            }
        }
```

- [ ] **Step 2: Add `ShakeCell` to `UIBoardController`**

In `UIBoardController.cs`, add this public method (e.g. just after `SetStockHighlight`):
```csharp
        /// <summary>Shakes the card at a cell as invalid-move feedback (no state change).</summary>
        public void ShakeCell(CellId id)
        {
            if (cardByCell.TryGetValue(id.Value, out var card) && card != null) card.Shake();
        }
```

- [ ] **Step 3: Wire it in `BoardPresenter`**

In `BoardPresenter.cs`, in the per-game subscription block (right after the `BoardGameService.OnSelectionChanged ... .AddTo(gameSubscriptions);` subscription that ends around line 207), add:
```csharp
            BoardGameService.OnInvalidTap
                .Subscribe(id =>
                {
                    BoardController.ShakeCell(id);
                    AudioService.Play(AudioCatalog.Card.MoveRejected);
                })
                .AddTo(gameSubscriptions);
```
(`AudioCatalog` is already used in this file, e.g. `AudioCatalog.Card.Place`; `MoveRejected` is the existing `"card.move_rejected"` constant.)

- [ ] **Step 4: Verify compile**

Run: force-recompile, poll `uloop compile`. Expected `ErrorCount: 0`. Then `uloop run-tests --test-mode EditMode` — expect the same green suite as Task 1 (no new tests, no regressions). Live behavior is verified in Task 5.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Component/Card/UICard.cs Assets/Scripts/Component/Board/UIBoardController.cs Assets/Scripts/Scene/Board/BoardPresenter.cs
git commit -m "feat(board): invalid tap shakes the card and plays the rejected-move sound"
```

---

## Task 3: Fly-to-waste play animation

When a TriPeaks play clears a cell whose card becomes the new waste-top, slide that same `UICard` to the waste anchor and adopt it as the waste card — instead of the Pyramid spin-off-bottom. Visual; verified at compile time + Task 5 play-verify.

**Files:**
- Modify: `Assets/Scripts/Component/Board/BoardRemovalAnimator.cs`
- Modify: `Assets/Scripts/Component/Board/UIBoardController.cs`

- [ ] **Step 1: Add `FlyToWaste` to `BoardRemovalAnimator`**

In `BoardRemovalAnimator.cs`, add `using System;` at the top (for `Action`). Add a serialized flight duration next to the `[Header("Flight")]` fields:
```csharp
        [SerializeField, Min(0.05f)] private float playToWasteDuration = 0.24f;
```
Add this method (after `AnimateRemoval`):
```csharp
        /// <summary>Slides a played card from its cell to the waste anchor (world-space lerp), scaling to the
        /// waste card's scale, then invokes <paramref name="onArrived"/> so the controller can adopt it as the
        /// waste card. Unlike AnimateRemoval, the card is NOT destroyed.</summary>
        public void FlyToWaste(UICard card, RectTransform wasteAnchor, float targetScale, Action onArrived)
        {
            if (card == null) { onArrived?.Invoke(); return; }
            if (overlayRoot == null || wasteAnchor == null) { onArrived?.Invoke(); return; }
            FlyToWasteAsync(card, wasteAnchor, targetScale, onArrived, masterCts.Token).Forget();
        }

        private async UniTaskVoid FlyToWasteAsync(UICard card, RectTransform wasteAnchor, float targetScale,
            Action onArrived, CancellationToken ct)
        {
            var rt = card.rectTransform;
            rt.SetParent(overlayRoot, true); // keep world position
            rt.SetAsLastSibling();           // render above remaining cells while it flies
            card.Disable();                  // not tappable mid-flight
            active.Add(card.gameObject);

            Vector3 startPos = rt.position;
            Vector3 startScale = rt.localScale;
            Vector3 endScale = new Vector3(targetScale, targetScale, 1f);
            try
            {
                float elapsed = 0f;
                while (elapsed < playToWasteDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    if (rt == null) break;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / playToWasteDuration);
                    float curved = ease != null ? ease.Evaluate(t) : t;
                    rt.position = Vector3.Lerp(startPos, wasteAnchor.position, curved);
                    rt.localScale = Vector3.Lerp(startScale, endScale, curved);
                    await UniTask.Yield(ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                active.Remove(card.gameObject);
                if (card != null) onArrived?.Invoke(); // controller re-parents + adopts as waste card
            }
        }
```

- [ ] **Step 2: Detect + route the play in `UIBoardController.RenderBoard`**

Replace the existing `RenderBoard` body with the version below (adds play-to-waste detection; everything else unchanged):
```csharp
        public void RenderBoard(BoardState state, bool animateRemovals = true, bool canRecycle = false)
        {
            var newWasteTop = state.WasteTop;
            bool wasteGrew = state.Waste.Count > lastWasteCount;
            bool adoptedPlay = false;

            for (int value = 0; value < state.CellCount; value++)
            {
                var id = new CellId(value);
                bool has = state.HasCard(id);
                bool spawned = cardByCell.ContainsKey(value);
                if (has && !spawned) SpawnCell(id, state.CardAt(id));
                else if (!has && spawned)
                {
                    // TriPeaks play: this cell's card is the new waste-top → fly it to the waste and adopt it.
                    if (animateRemovals && wasteGrew && !adoptedPlay && IsPlayToWaste(value, newWasteTop))
                    {
                        adoptedPlay = true;
                        PlayCellToWaste(value);
                    }
                    else DespawnCell(value, animateRemovals);
                }
            }
            RenderStock(state, canRecycle);
            RenderWaste(state, animateRemovals, suppressSpawn: adoptedPlay);
        }

        private bool IsPlayToWaste(int value, PlayingCard newWasteTop)
        {
            if (newWasteTop == null) return false;
            return cardByCell.TryGetValue(value, out var card) && card != null
                   && card.GetCard() != null && card.GetCard().Equals(newWasteTop);
        }

        /// <summary>Flies the played cell card to the waste anchor and adopts it as the new waste card.</summary>
        private void PlayCellToWaste(int value)
        {
            if (!cardByCell.TryGetValue(value, out var card) || card == null) return;
            cardByCell.Remove(value);
            cellByCard.Remove(card);
            card.OnPointerClickEvent.RemoveListener(OnCellCardClicked);

            // The previous waste-top is now covered (renderer only shows the top) → destroy it.
            if (wasteCard != null)
            {
                wasteCard.OnPointerClickEvent.RemoveListener(OnWasteClicked);
                Destroy(wasteCard.gameObject);
                wasteCard = null;
            }

            float wasteScale = wasteAnchor != null ? wasteAnchor.localScale.x : 1f;
            removalAnimator.FlyToWaste(card, wasteAnchor, wasteScale, () => AdoptAsWaste(card));
        }

        private void AdoptAsWaste(UICard card)
        {
            if (card == null) return;
            if (wasteAnchor != null)
            {
                card.rectTransform.SetParent(wasteAnchor, false);
                card.rectTransform.anchoredPosition = Vector2.zero;
                card.rectTransform.localScale = Vector3.one; // anchor itself carries any scale
            }
            card.Enable();
            card.OnPointerClickEvent.AddListener(OnWasteClicked);
            wasteCard = card;
        }
```

- [ ] **Step 3: Add the `suppressSpawn` parameter to `RenderWaste`**

Replace the `RenderWaste` signature + the trailing spawn/update block so a play-adopted waste is not duplicated. Change the signature line:
```csharp
        private void RenderWaste(BoardState state, bool animate)
```
to:
```csharp
        private void RenderWaste(BoardState state, bool animate, bool suppressSpawn = false)
```
Then, immediately after the `lastWasteCount = count;` line (just before `var top = state.WasteTop;`), add:
```csharp
            if (suppressSpawn) return; // a play-to-waste flight is delivering+adopting the new waste card
```
Everything below (the `var top = ...` spawn/update) stays unchanged. (The shrink/removal branch above the insert still runs for matches/undo, which is correct — a play grows the waste, so it never triggers there.)

- [ ] **Step 4: Verify compile**

Run: force-recompile, poll `uloop compile`. Expected `ErrorCount: 0`. Then `uloop run-tests --test-mode EditMode` — same green suite (no new tests). Live behavior verified in Task 5.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Component/Board/BoardRemovalAnimator.cs Assets/Scripts/Component/Board/UIBoardController.cs
git commit -m "feat(board): TriPeaks play flies the card to the waste and adopts it (no off-screen spin)"
```

---

## Task 4: Re-tune `TriPeaksBoard.prefab` layout (editor, no code)

Make the board fill the screen width with comfortably-sized cards and no global scale-down. This is iterative editor work verified by screenshots — not NUnit.

**Files:**
- Modify: `Assets/Prefabs/Board/TriPeaksBoard.prefab` (the 28 cell anchors + card size; remove the global scale-down)

- [ ] **Step 1: Inspect the current layout live**

Boot the game to a TriPeaks deal (App → Play → `RouteService.NavigateAsync("BoardScene", {gameType:"TriPeaks", variant:"1", seed:"42"})`), hide login canvases, `uloop screenshot`, and Read it. Note: which cards clip off-screen, the current card size (`98x133`), and the current board/anchor scale. Record the PlayArea width in the scene.

- [ ] **Step 2: Re-tune the anchors + card size**

Open `TriPeaksBoard.prefab` (or edit the instance in `BoardScene.unity` then apply) and:
- Reset the global board-root scale and any per-card scale hacks to 1 (keep deliberate stock/waste anchor scaling if present).
- Set the card size so a 10-card base row + small symmetric side margins fills the play-area width (card width ≈ playWidth / ~10.x; keep the 98:133 aspect for height).
- Reposition the 28 anchors: base row evenly spans the width; the three peaks sit above with rows overlapping vertically (row vertical step < card height) so all rows + waste + stock fit under the HUD.
- Preserve apex→base ordering in `cellAnchors` so later rows render over earlier ones.

- [ ] **Step 3: Screenshot-verify the fit**

Re-deal TriPeaks, `uloop screenshot`, Read it. Confirm: no card clips the screen edges, the three-peak shape reads clearly, cards are comfortably sized (not tiny), and the waste + stock are visible and tappable. Iterate Step 2 until it looks right at the real device resolution.

- [ ] **Step 4: Commit**

```bash
git add Assets/Prefabs/Board/TriPeaksBoard.prefab Assets/Scenes/BoardScene.unity
git commit -m "polish(board): re-tune TriPeaks layout to fill width, no global scale-down"
```

---

## Task 5: Full play-verification + Pyramid regression + review

**Files:** none (verification only).

- [ ] **Step 1: Live-verify the three items (TriPeaks).** Boot to a TriPeaks deal from the lobby tile (or direct nav). Confirm with screenshots:
  - **Play → waste:** tapping a free card adjacent to the waste-top slides that card onto the waste (it does NOT fly off the bottom); the waste shows the played card; score +50.
  - **Invalid tap:** tapping a free card that is NOT adjacent shakes that card and plays the rejected-move sound; no state change.
  - **Layout:** the whole board fits on screen with comfortably-sized cards.
- [ ] **Step 2: Regression — Pyramid unchanged.** Nav to Pyramid; confirm matched cards still spin off the bottom (its removal animation is untouched), deal/match/draw/recycle/undo still work, and the Pyramid board still renders correctly.
- [ ] **Step 3: Final EditMode suite.** A single uninterrupted `uloop run-tests --test-mode EditMode`. Expect all green (baseline 474/468 plus the 3 new `OnInvalidTap` tests → 477/471, 0 failed).
- [ ] **Step 4: Whole-feature review.** Dispatch a reviewer over the branch diff for these commits against the spec + `CLAUDE.md`. Address any ≥80-confidence findings.
- [ ] **Step 5:** Use **superpowers:finishing-a-development-branch** to wrap up (push / PR per the user's direction).

---

## Notes / invariants for the implementer

- **Pyramid is never changed.** `OnInvalidTap` has a never-emitting default; only `TriPeaksGameService` emits. `BoardRemovalAnimator.AnimateRemoval` (spin off-bottom) is still used for Pyramid removals and TriPeaks's own non-play despawns; only the play-to-waste path uses `FlyToWaste`.
- **One play per frame:** `adoptedPlay` guards against adopting more than one card into the waste in a single `RenderBoard` (a TriPeaks play removes exactly one cell).
- **Undo/reverse stays instant:** `RenderBoard` is called with `animateRemovals=false` on undo/restore, so `IsPlayToWaste` is bypassed (the `animateRemovals` guard) and the waste re-renders instantly — no spurious flight when reverting.
- **Waste scale:** the flight lerps `localScale` to the waste anchor's `localScale.x`; `AdoptAsWaste` reparents under `wasteAnchor` with `localScale = 1` so the final on-screen size matches the flight's end. If the waste anchor is not scaled, both are 1 and there is no scale change.
- **Durations are tunable**, not fixed contracts: shake `0.3s`/amp `16`, flight `0.24s`. Adjust during Task 5 if they feel off.
