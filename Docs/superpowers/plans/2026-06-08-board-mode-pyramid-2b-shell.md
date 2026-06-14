# Board Mode — Plan 2b: Shared Ingame Shell extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Extract the game-agnostic in-game UI (HUD + panels + toast + input blocker + confetti, and their observables) out of the card-specific `IngameComponent` into a reusable `IngameShellView`, and lift that UI into an `IngameShell` base prefab with an empty `PlayArea` slot — so the Pyramid/board scene can reuse the exact same UI via a prefab variant (no double-built UI).

**Architecture:** "Shared shell component" (the option chosen in the spec). `IngameShellView` (new MonoBehaviour) owns the game-agnostic UI and exposes the same `Show*/Hide*/On*` API the presenter already uses. `IngamePresenter` gets `[Inject] IngameShellView Shell` and routes shell concerns to it; `IngameComponent` keeps only the card play-area (cards controller, foundation cascade, drag/tap card events). The mature card game must keep working at every step — **the existing EditMode suite (card tests) staying green + a manual card-game play check are the verification gates.**

**Tech Stack:** Unity 6, VContainer (DI), R3, NUnit (regression gate). Editor automation via `uloop execute-dynamic-code` for the prefab/scene work (mirrors the skin-feature approach).

**Branch:** `feature/board-mode-pyramid`.

> ⚠️ **Highest-risk slice.** It refactors the working `IngamePresenter` (1222 lines) + `IngameComponent` (380 lines) + the `Ingame.unity` scene. Do NOT batch — one task, compile + run card tests + (where noted) manual play, then commit. If a step can't be verified green, STOP and report.

---

## Notes for the implementer

- **Compile/test loop (uloop):** after C# edits, force a refresh + recompile and poll:
  `uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"`
  then poll `uloop compile` until a real `ErrorCount`. If it hangs on "Domain Reload"/"already in progress" for a long time, run `uloop fix` (clears stale `*.lock` files) and retry.
- **Regression gate:** `uloop run-tests --test-mode EditMode`. Baseline at the start of this plan is **398 total / 392 passed / 6 skipped**. This plan changes NO test logic — the count must stay exactly the same and stay green. (Board logic tests + the existing card tests are all in this suite.)
- **Manual gate (Tasks 5–6):** after the scene/prefab wiring, run the card game (`uloop control-play-mode play`, then `uloop screenshot` / `uloop get-hierarchy`) and confirm Klondike still deals, drags, wins, pauses, shows panels. The presenter refactor can compile-pass yet mis-wire a panel — only play confirms it.
- **DI:** `IngameShellView` is a scene component → `builder.RegisterComponent(shellView)` in `IngameScene.Configure` (same pattern as `RegisterComponent(component)`).
- **Git:** commit per task, only the listed files, trailer `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

---

## Member split (source of truth for the extraction)

Move to **`IngameShellView`** (game-agnostic). Fields:
`hudView, winPanelView, dailyWinPanelView, statsPanelView, codeInputView, stuckPanelView, pausePanelView, settingPanelView, toastView, winEffectView, inputBlocker, OnWin (UnityEvent)`, and the localized strings `toastCopied/toastCodeCopied/toastAchievementUnlocked/errorCodeInvalid/challengeWonTemplate/challengeLoseTemplate/dailyShareTemplate`.

Move these **methods/observables** (verbatim bodies) to `IngameShellView`:
- HUD: `UpdateHudScore/UpdateHudMoves/UpdateHudTime/ResetHud`
- Win: `ShowWinPanel/HideWinPanel/OnShareObservable/OnCopyCodeObservable/OnWinLobbyObservable/TriggerWin`
- Daily win: `ShowDailyWinPanel/HideDailyWinPanel/DailyShareText/OnDailyCopyObservable/OnDailyTwitterObservable/OnDailyWinLobbyObservable`
- Pause: `ShowPausePanel/HidePausePanel/OnPauseToGameObservable/OnPauseNewGameObservable/OnPauseRestartObservable/OnPauseLobbyObservable/OnPausePlayWithCodeObservable`
- Setting: `ShowSettingPanel/HideSettingPanel`
- Stats: `ShowStatsPanel/HideStatsPanel`
- Code input: `ShowCodeInput/HideCodeInput/ShowCodeInputError/OnPlayWithCodeObservable`
- Stuck: `ShowStuckPanel/HideStuckPanel/OnStuckNewGameObservable/OnStuckRestartObservable/OnStuckUndoObservable`
- Toast: `ShowToast`
- Input/clipboard: `SetInputBlocker/CopyToClipboard`
- Confetti: `PlayWinEffectAsync` (NEW — the `winEffectView.PlayAsync` half of the old `PlayWinCelebrationAsync`)
- Bottom-bar buttons: the subjects + observables `OnRefreshEvents/OnUndo/OnNewGame/OnPause/OnStats/OnHint/OnApplicationPause` and the `[Button]` trigger methods (`Undo/NewGame/Pause/Stats/Hint/RefreshEvents`) and `OnApplicationPause`.
- The localized-string accessor properties (`ToastCopied` … `DailyShareTemplate`).

**Stays in `IngameComponent`** (card play-area): `cardsController, winCascadeAnimator`, and `SpawnCard/DespawnAllCards/DespawnPile/ApplySpriteSet/MoveAnimator/GetCardWorldPosition/FindCard/BindCard/UnbindCard/SetStockRestoreVisible/RevertCardDrop/MoveCardToPile/ShowHintHighlight/ClearHintHighlight` and the card observables `OnCardDragStarted/OnCardDropped/OnCardDragCanceled/OnCardClicked/OnPlaceHolderClicked`. Replace the old `PlayWinCelebrationAsync` with `PlayWinCascadeAsync` (the `winCascadeAnimator.PlayAsync` half).

---

## File Structure

- **Create:** `Assets/Scripts/Scene/Ingame/IngameShellView.cs` (the extracted MonoBehaviour).
- **Modify:** `Assets/Scripts/Scene/Ingame/IngameComponent.cs` (remove shell members; keep card-only; split win-celebration).
- **Modify:** `Assets/Scripts/Scene/Ingame/IngamePresenter.cs` (`[Inject] IngameShellView Shell`; route shell calls; combine win-celebration).
- **Modify:** `Assets/Scripts/Scene/Ingame/IngameScene.cs` (register `IngameShellView`).
- **Editor/asset:** extract `Assets/Prefabs/InGame/IngameShell.prefab` (base, with `PlayArea` slot) from `Ingame.unity`; the card scene uses an instance with the cards under `PlayArea`.

---

## Task 1: Create `IngameShellView` (no behavior change yet)

**Files:** Create `Assets/Scripts/Scene/Ingame/IngameShellView.cs`.

- [ ] **Step 1:** Create `IngameShellView : MonoBehaviour` in namespace `Scene.Ingame`. Move the shell **fields** (listed above) and the **verbatim bodies** of the shell methods/observables/properties from `IngameComponent` into it. Add the new `PlayWinEffectAsync(CancellationToken ct = default)`:
```csharp
public UniTask PlayWinEffectAsync(CancellationToken ct = default)
    => winEffectView != null ? winEffectView.PlayAsync(ct) : UniTask.CompletedTask;
```
Keep the same `using`s the moved code needs (`R3`, `Cysharp.Threading.Tasks`, `UnityEngine`, `UnityEngine.Events`, `UnityEngine.Localization`, `Component.*`, `Model.Stats`, `NaughtyAttributes`). Dispose the moved subjects in `OnDestroy`.
- [ ] **Step 2:** Refresh + `uloop compile`. `IngameComponent` still has its copies, so expect **no errors** (the new file is additive). Run `uloop run-tests --test-mode EditMode` → still 398/392/6.
- [ ] **Step 3:** Commit.
```bash
git add Assets/Scripts/Scene/Ingame/IngameShellView.cs Assets/Scripts/Scene/Ingame/IngameShellView.cs.meta
git commit -m "feat(ingame): add IngameShellView (extracted game-agnostic UI, not yet wired)"
```

---

## Task 2: Strip the moved members from `IngameComponent`

**Files:** Modify `Assets/Scripts/Scene/Ingame/IngameComponent.cs`.

- [ ] **Step 1:** Delete from `IngameComponent` every field/method/observable now living in `IngameShellView` (the "Move to IngameShellView" list). Keep the card play-area members. Replace `PlayWinCelebrationAsync` with:
```csharp
public UniTask PlayWinCascadeAsync(CancellationToken ct = default)
    => winCascadeAnimator != null ? winCascadeAnimator.PlayAsync(ct) : UniTask.CompletedTask;
```
- [ ] **Step 2:** Refresh + `uloop compile`. Expect **errors in `IngamePresenter`** (it still calls the removed `Component.<shellMethod>`). That is the to-do list for Task 3 — do not fix in `IngameComponent`.
- [ ] **Step 3:** Commit (compile is red here on purpose — note it; the next task makes it green). If your workflow forbids a red commit, do Tasks 2+3 as one commit.
```bash
git add Assets/Scripts/Scene/Ingame/IngameComponent.cs
git commit -m "refactor(ingame): IngameComponent keeps card play-area only (shell moved out)"
```

---

## Task 3: Route `IngamePresenter` shell calls through `Shell`

**Files:** Modify `Assets/Scripts/Scene/Ingame/IngamePresenter.cs`.

- [ ] **Step 1:** Add the injected shell next to the existing `[Inject] private IngameComponent Component`:
```csharp
[Inject] private IngameShellView Shell { get; set; }
```
- [ ] **Step 2:** Update every shell call site: change `Component.<shellMember>` → `Shell.<shellMember>` for the members moved in Task 1 (HUD/panels/toast/code/stuck/pause/setting/stats/buttons/localized strings/`SetInputBlocker`/`CopyToClipboard`/`TriggerWin`/`OnWin*`). Leave card calls (`Component.SpawnCard`, `Component.MoveAnimator`, `Component.OnCard*`, `Component.ShowHintHighlight`, etc.) untouched. The compiler errors from Task 2 enumerate exactly which lines.
- [ ] **Step 3:** Fix the win celebration (it spanned both): replace `Component.PlayWinCelebrationAsync(ct)` with a combined await:
```csharp
await UniTask.WhenAll(Component.PlayWinCascadeAsync(ct), Shell.PlayWinEffectAsync(ct));
```
(in `PlayWinCelebrationAsync`). Subscriptions previously `.AddTo(Component)` for shell events should now `.AddTo(Shell)` where the event source is the shell (or keep `.AddTo(Component)` if Component still lives the whole scene lifetime — either is fine; prefer the owning view).
- [ ] **Step 4:** Refresh + `uloop compile` → **ErrorCount 0**. `uloop run-tests --test-mode EditMode` → 398/392/6 (unchanged).
- [ ] **Step 5:** Commit.
```bash
git add Assets/Scripts/Scene/Ingame/IngamePresenter.cs
git commit -m "refactor(ingame): presenter routes shell concerns to IngameShellView"
```

---

## Task 4: Register the shell in DI

**Files:** Modify `Assets/Scripts/Scene/Ingame/IngameScene.cs`.

- [ ] **Step 1:** Add a `[SerializeField] private IngameShellView shellView;` and register it in `Configure`:
```csharp
builder.RegisterComponent(shellView);
```
(right after `builder.RegisterComponent(component);`).
- [ ] **Step 2:** Refresh + `uloop compile` → ErrorCount 0. Run tests → unchanged.
- [ ] **Step 3:** Commit.
```bash
git add Assets/Scripts/Scene/Ingame/IngameScene.cs
git commit -m "feat(ingame): register IngameShellView in scene DI"
```

---

## Task 5: Editor — attach shell + extract base prefab (manual gate)

**Files:** `Ingame.unity` scene; new `Assets/Prefabs/InGame/IngameShell.prefab`.

- [ ] **Step 1:** Discover the scene hierarchy. Run `uloop get-hierarchy` (or `execute-dynamic-code` dumping the Ingame canvas tree) to find where HUD/panels/cards live.
- [ ] **Step 2:** Add an `IngameShellView` component to the UI canvas root (or a `Shell` GameObject) and wire its SerializeFields to the existing scene panel objects (HUD, Win/DailyWin/Stuck/Pause/Setting/Stats, Toast, CodeInput, winEffect, inputBlocker, localized strings). Wire `IngameScene.shellView` to it, and `IngameComponent` to the cards/cascade only. Use `execute-dynamic-code` with `SerializedObject`/`PrefabUtility` (mirrors the skin SettingPanel automation).
- [ ] **Step 3:** Introduce a `PlayArea` container child and move the `UICardsController` (and its placeholders) under it. Extract the UI canvas (shell + PlayArea) into `Assets/Prefabs/InGame/IngameShell.prefab`; the Ingame scene keeps an instance.
- [ ] **Step 4:** Verify card game manually: `uloop control-play-mode play`; `uloop screenshot`; confirm deal, drag-move, a win (panel + confetti + cascade), pause panel, settings, stuck. `uloop control-play-mode stop`. Run EditMode tests → 398/392/6.
- [ ] **Step 5:** Commit scene + prefab (+ metas).
```bash
git add Assets/Scenes/Ingame.unity Assets/Prefabs/InGame/IngameShell.prefab Assets/Prefabs/InGame/IngameShell.prefab.meta
git commit -m "feat(ingame): extract IngameShell base prefab with PlayArea slot; wire shell"
```

---

## Task 6: Final regression gate

- [ ] **Step 1:** `uloop fix` if needed, then a clean refresh + `uloop compile` → ErrorCount 0, WarningCount 0.
- [ ] **Step 2:** `uloop run-tests --test-mode EditMode` → 398 total, 0 failed.
- [ ] **Step 3:** Manual: play Klondike AND Easthaven end-to-end (both use the same Ingame scene/shell). Confirm no panel/HUD/celebration regressions.
- [ ] **Step 4:** Push; the board scene (Plan 2-Board) will be a prefab variant of `IngameShell.prefab`.

---

## What this enables / defers

- **Enables:** Plan 2-Board makes `BoardScene` a prefab **variant** of `IngameShell.prefab` (PlayArea ← `UIBoardController`), with `BoardComponent` + `BoardPresenter` reusing `IngameShellView` verbatim — zero re-built UI.
- **Defers:** all board rendering/scene/Lobby/Route/snapshot-gateway/scoring-HUD wiring → Plan 2-Board + 2c.

---

## Self-Review (author checklist — completed)

- **Spec coverage (§7 shared shell + prefab variant):** shell extraction → Tasks 1–4; base prefab + PlayArea slot → Task 5; card game intact → Tasks 2/3/5/6 gates. Board-side reuse is explicitly Plan 2-Board.
- **Placeholder scan:** the member lists ARE the content; the per-method bodies are "move verbatim" (mechanical, compile-verified) rather than re-pasted — intentional for a 30-member move, with the non-mechanical bits (win-celebration split, `PlayWinEffectAsync`/`PlayWinCascadeAsync`, DI registration, PlayArea slot) spelled out in full.
- **Type consistency:** `IngameShellView` is the single new type; `Shell` is its injected handle in the presenter; `PlayWinEffectAsync` (shell) + `PlayWinCascadeAsync` (component) replace `PlayWinCelebrationAsync`, combined via `UniTask.WhenAll` in the presenter. Card members referenced unchanged.
- **Risk control:** every task ends at a green compile + unchanged test count; Tasks 5–6 add the mandatory manual play gate (a compile-clean presenter can still mis-wire a panel).
