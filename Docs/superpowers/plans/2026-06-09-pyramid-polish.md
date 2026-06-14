# Pyramid Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an always-on sum-13 reference table (top-right) and a spin-and-fly-down removal animation for matched Pyramid cards, with no audio change and zero impact on the card games.

**Architecture:** A new `BoardRemovalAnimator` MonoBehaviour animates the **real** matched card (reparent to an overlay → spin + fall off-screen via a `UniTask` lerp → destroy), hooked into `UIBoardController.DespawnCell`. The reference table is static prefab UI (no script). Both live on `PyramidBoard.prefab`; `DespawnAll` (new-game teardown) stays instant.

**Tech Stack:** Unity 6, R3, UniTask (`UniTaskVoid` + manual `Time.deltaTime` lerp, mirroring `CardMoveAnimator`), TextMeshPro, VContainer. Editor automation via `uloop execute-dynamic-code`.

**Branch:** `feature/board-mode-pyramid` (continue).

**Spec:** `Docs/superpowers/specs/2026-06-09-pyramid-polish-design.md`.

---

## Notes for the implementer

- **Compile loop (uloop):** after C# edits, force a refresh + recompile, then poll:
  `uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"`
  then poll `uloop compile` until a real `ErrorCount` (it can report a STALE `ErrorCount` mid-reload — ignore `ErrorCount:N` when `Errors:[]` and re-poll). If it hangs, `uloop fix` then retry.
- **No EditMode tests:** these are a MonoBehaviour animation + static UI — not EditMode-testable. Verification is compile-clean + a Pyramid play-gate (Task 3). EditMode count must stay ≥ baseline and 0-failed (nothing here touches logic).
- **execute-dynamic-code sandbox gotchas:** `using System;` is injected → use `UnityEngine.Object` (not `Object`); the project has a `Component` namespace → use `UnityEngine.Component`. No `System.IO` / `AssetDatabase.CreateAsset` / `Date.now`.
- **Git:** commit per task, only the listed files, trailer `Co-Authored-By: Claude <noreply@anthropic.com>`. Never `git add -A`.
- **Confirmed APIs (do not re-derive):** `UICard`: public `RectTransform rectTransform`, `Disable()`, serialized `CanvasGroup` (reachable via `GetComponent<CanvasGroup>()`). `UIBoardController` (in `Assets/Scripts/Component/Board/UIBoardController.cs`) has `private void DespawnCell(int value)` and `Dictionary<int,UICard> cardByCell` / `Dictionary<UICard,CellId> cellByCard`; `DespawnAll()` destroys directly (NOT via `DespawnCell`). `PyramidBoard.prefab` root has a `UIBoardController`; cell anchors are children with `anchorMin=anchorMax=(0.5,0.5)`; existing geometry: apex `Cell_0` at `(0,225)`, base `Cell_21..27` at `y=-225`.

---

## File Structure

- **Create** `Assets/Scripts/Component/Board/BoardRemovalAnimator.cs` (+ `.meta`) — namespace `Component.Board`. One responsibility: animate a single removed card off-screen and destroy it.
- **Modify** `Assets/Scripts/Component/Board/UIBoardController.cs` — add a `removalAnimator` serialized field; route `DespawnCell` through it.
- **Modify** `Assets/Prefabs/Board/PyramidBoard.prefab` — root → full-stretch; add `OverlayRoot` + `BoardRemovalAnimator`; wire `UIBoardController.removalAnimator`; add static `PairGuide` legend (top-right).

No assembly, model, service, or audio changes.

---

## Task 1: `BoardRemovalAnimator` component

**Files:**
- Create: `Assets/Scripts/Component/Board/BoardRemovalAnimator.cs`

- [ ] **Step 1: Create the file with this exact content**

```csharp
using System.Collections.Generic;
using System.Threading;
using Component.Card;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Component.Board
{
    /// <summary>
    /// Spins a matched board card and flies it straight down off the bottom edge, then destroys it.
    /// Animates the real removed card (no ghost) — fire-and-forget; board state is already updated
    /// before this plays. Mirrors CardMoveAnimator's manual UniTask-lerp style.
    /// </summary>
    public class BoardRemovalAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform overlayRoot; // flying cards reparent here (renders above cells)

        [Header("Flight")]
        [SerializeField, Min(0.05f)] private float duration = 0.6f;
        [SerializeField] private float spinDegrees = 540f;               // total |Z| spin; direction randomized per card
        [SerializeField, Min(0f)] private float horizontalDrift = 120f;  // random horizontal drift while falling
        [SerializeField, Min(0f)] private float extraFallPadding = 200f; // ensures the card clears the bottom edge
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Fade")]
        [SerializeField] private bool fadeOut = true;
        [SerializeField, Range(0f, 1f)] private float fadeStartT = 0.7f;  // begin fading after this fraction of the flight

        private CancellationTokenSource masterCts;
        private readonly List<GameObject> active = new();

        private void Awake() => masterCts = new CancellationTokenSource();

        private void OnDestroy()
        {
            masterCts?.Cancel();
            masterCts?.Dispose();
            foreach (var go in active)
                if (go != null) Destroy(go);
            active.Clear();
        }

        /// <summary>Reparents the card to the overlay, spins + drops it off-screen, then destroys it.</summary>
        public void AnimateRemoval(UICard card)
        {
            if (card == null) return;
            if (overlayRoot == null) { Destroy(card.gameObject); return; }
            AnimateAsync(card, masterCts.Token).Forget();
        }

        private async UniTaskVoid AnimateAsync(UICard card, CancellationToken ct)
        {
            var rt = card.rectTransform;
            rt.SetParent(overlayRoot, true); // keep world position
            rt.SetAsLastSibling();           // render above remaining cells
            card.Disable();                  // a flying card cannot be tapped
            active.Add(card.gameObject);

            var cg = card.GetComponentInChildren<CanvasGroup>(); // UICard's CanvasGroup may sit on a child

            Vector2 startPos = rt.anchoredPosition;
            float cardHeight = rt.rect.height;
            float bottomY = -(overlayRoot.rect.height * 0.5f) - cardHeight - extraFallPadding;
            Vector2 endPos = new Vector2(startPos.x + Random.Range(-horizontalDrift, horizontalDrift), bottomY);
            float startZ = rt.localEulerAngles.z;
            float spin = spinDegrees * (Random.value < 0.5f ? 1f : -1f);

            try
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    if (rt == null) break;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float curved = ease != null ? ease.Evaluate(t) : t;
                    rt.anchoredPosition = Vector2.Lerp(startPos, endPos, curved);
                    rt.localRotation = Quaternion.Euler(0f, 0f, startZ + spin * t);
                    if (fadeOut && cg != null && t > fadeStartT)
                        cg.alpha = Mathf.InverseLerp(1f, fadeStartT, t); // 1 -> 0 across [fadeStartT, 1]
                    await UniTask.Yield(ct);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                active.Remove(card.gameObject);
                if (card != null) Destroy(card.gameObject);
            }
        }
    }
}
```

- [ ] **Step 2: Refresh + `uloop compile` → ErrorCount 0.** `Component.Board` already resolves `Component.Card.UICard`, UniTask, and `UnityEngine.*` (all referenced by `Component.asmdef`).

- [ ] **Step 3: Commit.**
```bash
git add Assets/Scripts/Component/Board/BoardRemovalAnimator.cs Assets/Scripts/Component/Board/BoardRemovalAnimator.cs.meta
git commit -m "feat(board): add BoardRemovalAnimator (spin + fly-down off-screen)"
```

---

## Task 2: Route `UIBoardController.DespawnCell` through the animator

**Files:**
- Modify: `Assets/Scripts/Component/Board/UIBoardController.cs`

- [ ] **Step 1: Add the serialized field.** In the field-declaration block (next to `cardPrefab`), add:

```csharp
        [SerializeField] private BoardRemovalAnimator removalAnimator;
```

- [ ] **Step 2: Route the single-cell despawn through the animator.** Replace the existing `DespawnCell` method:

```csharp
        private void DespawnCell(int value)
        {
            if (!cardByCell.TryGetValue(value, out var view)) return;
            cardByCell.Remove(value);
            if (view == null) return;
            cellByCard.Remove(view);
            view.OnPointerClickEvent.RemoveListener(OnCellCardClicked);
            Destroy(view.gameObject);
        }
```

with:

```csharp
        private void DespawnCell(int value)
        {
            if (!cardByCell.TryGetValue(value, out var view)) return;
            cardByCell.Remove(value);
            if (view == null) return;
            cellByCard.Remove(view);
            view.OnPointerClickEvent.RemoveListener(OnCellCardClicked);
            // A single mid-play removal is a match — spin the card out instead of vanishing.
            // The animator takes ownership of destroying the card. (DespawnAll stays instant.)
            removalAnimator.AnimateRemoval(view);
        }
```

(`removalAnimator` is a required wired field — a null here is a prefab-wiring bug and should fail fast, per the project's no-silent-failover rule. `DespawnAll` is left untouched: it destroys directly for an immediate new-game teardown.)

- [ ] **Step 3: Refresh + `uloop compile` → ErrorCount 0.**

- [ ] **Step 4: Commit.**
```bash
git add Assets/Scripts/Component/Board/UIBoardController.cs
git commit -m "feat(board): play removal animation on match (DespawnCell)"
```

---

## Task 3: Wire the prefab — root full-stretch, overlay, animator, PairGuide (manual gate)

> ⚠️ Editor surgery on `PyramidBoard.prefab`. Use `uloop execute-dynamic-code` to load the prefab contents (`PrefabUtility.LoadPrefabContents` / `SavePrefabAsset`), edit, save. After: clean `uloop compile` + a Pyramid play-gate.

**Files:**
- Modify: `Assets/Prefabs/Board/PyramidBoard.prefab`

- [ ] **Step 1: Root → full-stretch; add OverlayRoot + BoardRemovalAnimator; wire it; verify cells unchanged.** Run this `execute-dynamic-code` snippet (it edits prefab contents and saves):

```csharp
using UnityEditor;
using UnityEngine;
using Component.Board;
var path = "Assets/Prefabs/Board/PyramidBoard.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
var sb = new System.Text.StringBuilder();

// 1. root RectTransform -> full-stretch, pivot centered (cells keep their anchoredPosition)
var rrt = (RectTransform)root.transform;
rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.pivot = new Vector2(0.5f, 0.5f);
rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

// 2. OverlayRoot: full-stretch, LAST sibling (renders above cells)
var overlay = new GameObject("OverlayRoot", typeof(RectTransform));
var ort = (RectTransform)overlay.transform;
ort.SetParent(rrt, false);
ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.pivot = new Vector2(0.5f, 0.5f);
ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
ort.SetAsLastSibling();

// 3. BoardRemovalAnimator on the root; wire overlayRoot
var anim = root.GetComponent<BoardRemovalAnimator>();
if (anim == null) anim = root.AddComponent<BoardRemovalAnimator>();
var animSo = new SerializedObject(anim);
animSo.FindProperty("overlayRoot").objectReferenceValue = ort;
animSo.ApplyModifiedProperties();

// 4. wire UIBoardController.removalAnimator
var ctrl = root.GetComponent<UIBoardController>();
var ctrlSo = new SerializedObject(ctrl);
ctrlSo.FindProperty("removalAnimator").objectReferenceValue = anim;
ctrlSo.ApplyModifiedProperties();

// verify a couple of cell anchors are still where they were
var cellArr = new SerializedObject(ctrl).FindProperty("cellAnchors");
var apex = (RectTransform)cellArr.GetArrayElementAtIndex(0).objectReferenceValue;
var baseR = (RectTransform)cellArr.GetArrayElementAtIndex(27).objectReferenceValue;
sb.AppendLine("apex=" + apex.anchoredPosition + " base27=" + baseR.anchoredPosition);

PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);
AssetDatabase.SaveAssets();
sb.AppendLine("done: overlay+animator wired");
return sb.ToString();
```
Expected: `apex=(0.00, 225.00) base27=(270.00, -225.00)` (unchanged) and `done`.

- [ ] **Step 2: Build the static PairGuide legend (top-right).** Run this snippet (builds a 7-row sum-13 chart anchored top-right; TMP text, no script):

```csharp
using UnityEditor;
using UnityEngine;
using TMPro;
var path = "Assets/Prefabs/Board/PyramidBoard.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
var rrt = (RectTransform)root.transform;

// panel anchored top-right
var panel = new GameObject("PairGuide", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
var prt = (RectTransform)panel.transform;
prt.SetParent(rrt, false);
prt.anchorMin = Vector2.one; prt.anchorMax = Vector2.one; prt.pivot = Vector2.one;
prt.anchoredPosition = new Vector2(-24f, -24f);
panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.35f);
var vlg = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
vlg.childAlignment = TextAnchor.UpperCenter; vlg.spacing = 2f;
vlg.padding = new RectOffset(14, 14, 10, 10);
vlg.childControlWidth = true; vlg.childControlHeight = false;
vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
panel.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
panel.GetComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

string[] rows = { "K", "Q  A", "J  2", "10  3", "9  4", "8  5", "7  6" };
foreach (var r in rows)
{
    var go = new GameObject("Row_" + r.Replace(" ", ""), typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
    go.transform.SetParent(prt, false);
    go.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 30f;
    var tmp = go.GetComponent<TextMeshProUGUI>();
    tmp.text = r;
    tmp.fontSize = 26f;
    tmp.alignment = TextAlignmentOptions.Center;
    tmp.color = Color.white;
    tmp.raycastTarget = false;
}

PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);
AssetDatabase.SaveAssets();
return "PairGuide built with " + rows.Length + " rows";
```
Expected: `PairGuide built with 7 rows`.

- [ ] **Step 3: Refresh + `uloop compile` → ErrorCount 0, WarningCount 0.**

- [ ] **Step 4: Pyramid play-gate.** From `App`: `uloop control-play-mode --action Play`; route into BoardScene/Pyramid (Lobby → Pyramid tile, or directly). `uloop screenshot`. Confirm:
  - The sum-13 legend is visible **top-right**, all 7 rows correct.
  - The pyramid is in the **same position** as before (cells did not shift).
  - Tap a sum-13 free pair → both cards **spin and fly down off the bottom**, then disappear; score/moves still update.
  - New game / restart → cards clear **instantly** (no fly-out on teardown).
  - `uloop control-play-mode --action Stop`.

- [ ] **Step 5: Regression.** Route into `Ingame`/Klondike → deal + a drag-move still work (the card game never touches `UIBoardController`/`BoardRemovalAnimator`). `uloop run-tests --test-mode EditMode` → 0 failed, count ≥ baseline.

- [ ] **Step 6: Commit.**
```bash
git add Assets/Prefabs/Board/PyramidBoard.prefab
git commit -m "feat(board): pyramid sum-13 legend + removal-animation wiring"
```

---

## Self-Review (author checklist)

- **Spec coverage:** Reference table = Task 3 Step 2 (static `PairGuide`, top-right, 7 rows). Removal animation = Task 1 (`BoardRemovalAnimator`) + Task 2 (`DespawnCell` hook) + Task 3 Step 1 (overlay + wiring). Root full-stretch (table-anchor enabler, cells preserved) = Task 3 Step 1. No-audio-change and `DespawnAll`-stays-instant honored. No spec requirement left unimplemented.
- **Type consistency:** `BoardRemovalAnimator.AnimateRemoval(UICard)` (Task 1) is called by `UIBoardController.DespawnCell` (Task 2) and wired in Task 3 Step 1 (`removalAnimator` field name matches; `overlayRoot` serialized name matches). `UICard.rectTransform` / `Disable()` / `CanvasGroup` verified against source.
- **Placeholder scan:** all code is complete; editor steps are procedural at the established 2b/2-Board precedent with exact `SerializedObject`/`PrefabUtility` mechanics and expected outputs. No TBD/TODO.
- **Risk control:** Tasks 1-2 end at compile-0; Task 3 is wire → play → commit with a Klondike regression re-check (card games are isolated from the board renderer).
```
