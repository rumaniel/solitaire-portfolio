# Per-Table Card Architecture — Klondike/Spider own their tables

> Mirrors the board-game pattern (PyramidBoard/TriPeaksBoard each own a UIBoardController)
> for the card games. Replaces the single shared UICardsController + layout-prefab swap +
> coverScale workaround with one self-contained table per card game.

**Goal:** Each card game (Klondike/Easthaven share one; Spider its own; FreeCell later its own)
renders from a self-contained table prefab that owns its UICardsController + CardMoveAnimator +
Cover, selected per-game like BoardViewSet selects a board.

**Why:** The shared controller lives outside the per-game (scaled) layout, which forced the
`coverScale` field to rescale the shared cover. Per-table tables put the cover inside each table
at the correct scale — no workaround — and unify card/board structure.

**Sequencing:** Do this AFTER merging PR #109 (Spider, verified working). It is a structural
refactor with prefab surgery; isolating it in its own PR keeps risk and review scoped. It also
becomes the substrate FreeCell builds on.

---

## Current state (from investigation)

- `PlayArea` children: `Table` (scene GO), `PyramidBoard`, `TriPeaksBoard` (prefab instances).
- `Table` components: UICardsController, CardMoveAnimator, Cover (child), CanvasGroup,
  WinCascadeAnimator, WinEffectView. Children: KlondikeTable + SpiderTable prefab instances
  (layout-only: root + Foundation/Tableau anchors + placeholders), Cover, Top Anchor.
- `IngameComponent` (on IngameShell): `[SerializeField] UICardsController cardsController`,
  `[SerializeField] WinCascadeAnimator winCascadeAnimator`. ~20 methods delegate to cardsController.
- `WinCascadeAnimator`: own `[SerializeField] UICardsController cardsController` (GetFoundationCards).
- `IngamePresenter`: talks ONLY to `IngameComponent` (no direct controller ref).
- `UICardsController`: layouts (TableLayoutSet list) + coverScale + ActivateLayout swap; cardPrefab,
  coverRootTransform, moveAnimator, placeholders serialized.
- `BoardViewSet`: `For(GameType)`, `All`; built `new BoardViewSet(pyramid, triPeaks)` in
  IngameScene from two serialized UIBoardController fields, RegisterInstance.

---

## Target architecture

- KlondikeTable.prefab / SpiderTable.prefab each contain: their layout (existing) PLUS their own
  `UICardsController` + `CardMoveAnimator` + `Cover` child. The controller's placeholders/cover/
  animator/cardPrefab are wired within the prefab. Cover scale is baked per prefab (Klondike 1,
  Spider 0.65) — no runtime coverScale.
- `IngameComponent` holds the per-game controllers + an `active` pointer; `ActivateLayout(gameType)`
  selects active, toggles the table GOs (SetActive), retargets winCascade + card service. All other
  delegations use `active`. Presenter unchanged.
- `UICardsController` loses `layouts`, `coverScale`, the ActivateLayout swap (each controller is
  single-layout now).
- `WinCascadeAnimator` gains `SetController(UICardsController)`, called by ActivateLayout.

---

## Task 1 — CardViewSet (mirror BoardViewSet) [code]
Create `Assets/Scripts/Scene/Ingame/CardViewSet.cs` (namespace Scene.Ingame), plain sealed class:
```csharp
public sealed class CardViewSet
{
    private readonly UICardsController klondike; // serves Klondike + Easthaven
    private readonly UICardsController spider;
    public CardViewSet(UICardsController klondike, UICardsController spider)
    { this.klondike = klondike; this.spider = spider; }

    /// <summary>Controller for the game type (Spider → spider; everything else → klondike).</summary>
    public UICardsController For(GameType gameType) =>
        gameType == GameType.Spider ? spider : klondike;

    public System.Collections.Generic.IEnumerable<UICardsController> All
    { get { if (klondike != null) yield return klondike; if (spider != null) yield return spider; } }
}
```
(FreeCell later: add a field + extend For; keep the board-set shape.)

## Task 2 — UICardsController: drop layout-swap [code]
Remove `TableLayoutSet`, `layouts`, `ActivateLayout`, and the coverScale logic. Each controller now
uses its single serialized `placeholders` + `coverRootTransform` directly. Keep `SetCardService`,
spawn/despawn/find/anim/hint API unchanged. `Start()` subscribes its own `placeholders` (already
does). Verify no remaining references to ActivateLayout/coverScale except the ones removed in Task 3.

## Task 3 — IngameComponent: multi-table facade [code]
Replace `[SerializeField] UICardsController cardsController` with:
```csharp
[SerializeField] private CardViewSet... // NOT serializable; instead two controller fields:
[SerializeField] private UICardsController klondikeTable;
[SerializeField] private UICardsController spiderTable;
```
Add `private UICardsController active;` and `private ICardService pendingCardService;`.
Rework methods:
- `ActivateLayout(GameType gameType)`:
  ```csharp
  active = gameType == GameType.Spider ? spiderTable : klondikeTable;
  if (klondikeTable != null) klondikeTable.gameObject.SetActive(active == klondikeTable);
  if (spiderTable != null) spiderTable.gameObject.SetActive(active == spiderTable);
  if (pendingCardService != null) active.SetCardService(pendingCardService);
  winCascadeAnimator.SetController(active);
  ```
- `SetCardService(s)`: `pendingCardService = s; if (active != null) active.SetCardService(s);`
- `ApplySpriteSet(set)`: apply to BOTH controllers (skin replays on subscribe; both stay skinned).
- Every other delegate (`SpawnCard`, `DespawnAllCards`, `DespawnPile`, `FindCard`,
  `GetCardWorldPosition`, `MoveAnimator`, `ClearHintHighlight`, `ShowHintHighlight`,
  `SetStockRestoreVisible`, the five `OnCard*AsObservable`, `BindCard`, `UnbindCard`): route to
  `active`. Guard `active != null` where a call can precede the first ActivateLayout (event
  forwarders are subscribed in presenter Start BEFORE ActivateLayout — see risk R1).

## Task 4 — WinCascadeAnimator: settable controller [code]
Add `public void SetController(UICardsController c) => cardsController = c;` (keep the serialized
field as a fallback/default). Called from IngameComponent.ActivateLayout.

## Task 5 — Prefab surgery (editor scripts via uloop) [scene/prefab — HIGH RISK]
For EACH of KlondikeTable.prefab and SpiderTable.prefab (open prefab via PrefabUtility.LoadPrefabContents,
edit, SaveAsPrefabAsset):
1. Add a `Cover` child RectTransform (full-stretch). For SpiderTable set its localScale 0.65; Klondike 1.
2. AddComponent<CardMoveAnimator>; copy serialized params from the current shared Table animator
   (ghostPrefab, moveDuration, moveEase, shake*, preview*); set ghostRoot = this prefab's Cover.
3. AddComponent<UICardsController>; wire cardPrefab (same UICard prefab ref), coverRootTransform =
   this Cover, moveAnimator = this CardMoveAnimator, placeholders = this prefab's placeholder list.
4. Verify each controller's placeholders resolve (Klondike 13, Spider 19).
Then in the scene: wire IngameComponent.klondikeTable / spiderTable to the two prefab instances'
new UICardsController components; remove the now-dead shared UICardsController + CardMoveAnimator +
Cover from `Table` (leave WinCascadeAnimator/WinEffectView/CanvasGroup on Table — shell-level);
point WinCascadeAnimator's default controller at klondikeTable (or leave, ActivateLayout sets it).
Update IngameScene if any serialized card refs changed.

## Task 6 — DI / scene wiring check [code+scene]
IngameComponent is registered via `RegisterComponent(component)` already; its new serialized
controller fields are scene-wired, no new DI. Confirm IngamePresenter unchanged compiles. If a
CardViewSet DI registration is preferred for symmetry with BoardViewSet, optional — not required
since IngameComponent owns the selection.

## Task 7 — Verify
- `uloop compile` 0 errors; full EditMode suite (currently 558) 0 failures.
- Editor smoke: Klondike/Easthaven deal+play (their table active, Spider table inactive), Spider
  deal+play (own table, cards + flying cards at 0.62/0.65, no coverScale), cross-game switch
  (Klondike→Spider→Klondike) toggles tables, win cascade reads the active table's foundations,
  drag/fly scale correct per game, no double card systems active at once.

---

## Risks / mitigations
| Risk | Mitigation |
|------|-----------|
| R1: event forwarders (OnCard*) subscribed in presenter Start BEFORE first ActivateLayout → `active` null | ActivateLayout is called early in InitializeGameAsync (Start kicks it). But presenter Start subscribes forwarders during RefreshEvents before EvaluateOwnership→ActivateLayout. Either (a) default `active` to klondikeTable in Awake, or (b) make forwarders return the active subject lazily. Pick (a): set active in Awake. |
| R2: prefab surgery wiring errors (wrong ghostPrefab/cardPrefab/placeholder refs) | Verify each prefab's controller refs via a post-edit probe (cardPrefab non-null, placeholders count, coverRoot set, animator.ghostRoot set). |
| R3: two controllers both active → double input/spawn | ActivateLayout SetActive-toggles; only active table GO enabled. Confirm inactive table spawns nothing. |
| R4: WinCascade uses stale controller | SetController on every ActivateLayout. |
| R5: duplicated card-system wiring drifts between prefabs | Both controllers are the same script; only placeholder list + cover scale differ. Document in each prefab. |

## Out of scope
- FreeCell (separate; will add a third table prefab + CardViewSet field).
- Removing `Table` GO entirely (keep as container for the win animators + table prefab instances).
