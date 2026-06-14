# Assembly Layering Refactor (Plan B) — Restore Clean Dependency Direction

> **For agentic workers:** Use superpowers:executing-plans (or subagent-driven-development) to run this. Steps use checkbox (`- [ ]`) tracking. This is **independent of the skin-swap feature** — run it on its own branch, ideally after `feature/skin-swap` merges (or rebase skin on top).

**Goal:** Eliminate the two dependency inversions that make the assembly graph contradict CLAUDE.md and force awkward workarounds (the `Skin` assembly): `Data → Service` and `Core → Service`/`Core → Data`. Restore the documented direction where `Model`/`Core` are true base layers.

**Why:** During the skin feature, adding `Service → Data` / `Gateway → Data` created a build-breaking assembly **cycle** (because `Data → Service` already existed). The inversions are caused by exactly three couplings; removing them lets `Service`/`Gateway` reference `Data` normally and makes the layering match the docs.

**Tech Stack:** Unity asmdef, VContainer, R3, UniTask, `uloop` CLI.

---

## Current vs Target dependency graph (project layers)

**Current (inverted):**
```
{Model, Shared} → Gateway → Service → Data → Core → Component → Scene/App
                                         ▲                 (+ Skin above Data)
   Data → Service  (DealRuleAsset : IDealRule)
   Core → Service  (AudioSystem uses IAudioService)
   Core → Data     (AudioSystem/AudioSourcePlayer use Data.Audio)
```

**Target (documented direction):**
```
Model → (none)
Core → Model            (ComponentBase, SceneBase only)
Gateway → Model
Data → Model            (no longer → Service)
Service → Model, Core, Gateway
Component → Core, Data, Service, Model, Shared
Audio (new) → Service, Data, Model, R3, Unity   (AudioSystem/AudioSourcePlayer)
Scene/App → upper
```
After this, `Service → Data` and `Gateway → Data` become **acyclic**, so the skin code can optionally fold back into `Service`/`Gateway` (Phase 3).

---

## Root-cause couplings (verified)

1. **`Data → Service`** — single source: `Assets/Scripts/Data/Game/DealRuleAsset.cs` implements `Service.GameService.IDealRule`. `IDealRule` (`Assets/Scripts/Service/GameService/IDealRule.cs`) is a **pure** interface (only `int`/`bool`/`int[]` properties — no Service/Unity deps). `IScoreRule` is already correctly in `Model.Stats` (not a problem).
2. **`Core → Service`** — `Assets/Scripts/Core/Audio/AudioSystem.cs` consumes `Service.AudioService.IAudioService`.
3. **`Core → Data`** — `AudioSystem.cs` + `Assets/Scripts/Core/Audio/AudioSourcePlayer.cs` use `Data.Audio` (`AudioDatabaseAsset`, `AudioType`).

`Core` contains only 4 files: `ComponentBase.cs`, `SceneBase.cs`, `Audio/AudioSystem.cs`, `Audio/AudioSourcePlayer.cs`. `ComponentBase`/`SceneBase` do **not** reference Service/Data (only the audio files do).

`IDealRule` is referenced by 18 files (mostly Service.GameService/CardService/HintService + Scene + the Data asset).
`AudioSystem` is referenced by `App/AppLifetimeScope.cs`, `Scene/Ingame/IngameScene.cs` (and itself).
`IAudioService` is referenced by App, Component (AudioPlayer, SettingPanelView), Core (AudioSystem), Scene (Ingame, Lobby), Service.

---

## Phase 1 — Move `IDealRule` to Model (breaks `Data → Service`)

**Files:**
- Move/rewrite: `Assets/Scripts/Service/GameService/IDealRule.cs` → `Assets/Scripts/Model/Game/IDealRule.cs` (namespace `Service.GameService` → `Model.Game`)
- Modify usings (add `using Model.Game;` / drop reliance on `Service.GameService` for the interface) in the 18 referencing files
- Modify: `Assets/Scripts/Data/Data.asmdef` — remove the `Service` reference (`GUID:c2884e981ff3f174e8d34a4031fd112d`)

- [ ] **Step 1:** Move `IDealRule.cs` to `Assets/Scripts/Model/Game/` and change its namespace to `Model.Game`. (Use `git mv` + edit the `namespace` line; move the `.cs.meta` too.)
- [ ] **Step 2:** In every file that used `IDealRule` via `Service.GameService`, ensure `Model.Game` is imported. Files in `Service.GameService` namespace previously needed no using — add `using Model.Game;` there. List: `DealRuleAsset.cs`, `IngamePresenter.cs`, `IngameScene.cs`, `ICardService.cs`, `SolitaireCardService.cs`, `SolitaireCardServiceBase.cs`, `DealBuilder.cs`, `DealRuleFactory.cs`, `IDealRuleFactory.cs`, `IGameService.cs`, `IReversePlayStrategy.cs`, `KlondikeSolver.cs`, `ReversePlayShuffleStrategy.cs`, `SolitaireGameService.cs`, `HintService.cs`, `IHintService.cs`, `MoveEnumerator.cs`.
- [ ] **Step 3:** Remove `"GUID:c2884e981ff3f174e8d34a4031fd112d"` (Service) from `Data.asmdef` references.
- [ ] **Step 4:** Force recompile (`AssetDatabase.Refresh(ForceUpdate)` + `RequestScriptCompilation` via `uloop execute-dynamic-code`, then poll `uloop compile`). Expect `ErrorCount: 0` and **no cyclic-dependency error**.
- [ ] **Step 5:** `uloop run-tests --test-mode EditMode` → all green (notably `DealBuilderTests`, `Easthaven*`, `SolitaireGameServiceTests`).
- [ ] **Step 6:** Commit: `refactor(asmdef): move IDealRule to Model; drop Data→Service`.

**Risk:** Low. Pure interface move; mechanical using changes. Verify no `.asmdef` other than Data still needs adjusting.

---

## Phase 2 — Relocate audio out of `Core` (breaks `Core → Service` and `Core → Data`)

Decision: move `AudioSystem` + `AudioSourcePlayer` into a **new `Audio` assembly** (recommended) so `Core` becomes a pure base layer. (Alternative: move them into `Component`, which already references Service+Data+R3 — fewer assemblies but muddies "Component = UI". Recommended: dedicated `Audio`.)

**Files:**
- Create: `Assets/Scripts/Audio/Audio.asmdef` (references: Model, Data, Service, R3.Unity, UniTask, Unity.TextMeshPro? no — only what audio needs: Model `5f4983…`, Data `26845efb…`, Service `c2884e98…`, R3.Unity `77221876…`, UniTask `f51ebe6a…`; plus engine modules auto)
- Move: `Core/Audio/AudioSystem.cs`, `Core/Audio/AudioSourcePlayer.cs` → `Assets/Scripts/Audio/` (keep or rename namespace `Core.Audio` → `Audio`)
- Modify: `Core.asmdef` — remove `Service` + `Data` references; add `Model` if `ComponentBase`/`SceneBase` need it (verify by compile)
- Modify usings where `AudioSystem` is referenced: `App/AppLifetimeScope.cs`, `Scene/Ingame/IngameScene.cs` (change `using Core.Audio;` → `using Audio;` if namespace renamed)
- Modify: `App.asmdef`, `Scene.asmdef` — add the new `Audio` assembly GUID (read from `Audio.asmdef.meta` after import)

- [ ] **Step 1:** Create `Assets/Scripts/Audio/` and move the two audio `.cs` (+ `.meta`) there via `git mv`. Update their namespace if renaming (`Core.Audio` → `Audio`).
- [ ] **Step 2:** Create `Audio.asmdef` with references Model, Data, Service, R3.Unity, UniTask. Refresh to generate its `.meta`; read the new GUID.
- [ ] **Step 3:** Remove Service (`c2884e98…`) and Data (`26845efb…`) from `Core.asmdef`. Add Model (`5f4983…`) only if compile shows `ComponentBase`/`SceneBase` need it.
- [ ] **Step 4:** Add the `Audio` GUID to `App.asmdef` and `Scene.asmdef`; fix `using` in `AppLifetimeScope.cs` and `IngameScene.cs`.
- [ ] **Step 5:** Force recompile + poll. Expect `ErrorCount: 0`, no cycle. Confirm `Core` now references only base packages (no Service/Data).
- [ ] **Step 6:** `uloop run-tests --test-mode EditMode` → green (`AudioServicePersistenceTests`, `HapticServiceTests`, etc.).
- [ ] **Step 7:** Play-mode smoke (optional): audio still plays in-game.
- [ ] **Step 8:** Commit: `refactor(asmdef): move audio out of Core into Audio assembly; Core is now base-only`.

**Risk:** Medium. `AudioSystem` is a DontDestroy singleton wired in the App prefab via `[SerializeField]`; moving its assembly does **not** break the serialized reference (GUID/fileID unchanged by an assembly move), but verify the prefab/scene reference resolves after recompile. Namespace rename is optional — skip it to reduce churn if preferred.

---

## Phase 3 (optional) — Dissolve the `Skin` assembly back into Service/Gateway

Only after Phase 1 (so `Data → Service` is gone and `Service`/`Gateway` may reference `Data`).

- [ ] Move `Assets/Scripts/Skin/ISkinAssetGateway.cs`, `AddressableSkinAssetGateway.cs` → `Assets/Scripts/Gateway/Skin/` (namespace `Skin` → `Gateway.Skin`); add Data + Addressables refs to `Gateway.asmdef`.
- [ ] Move `ISkinService.cs`, `SkinService.cs`, `ISkinPreferenceStore.cs`, `PlayerPrefsSkinPreferenceStore.cs` → `Assets/Scripts/Service/SkinService/` (namespace `Skin` → `Service.SkinService`); add Data + Addressables refs to `Service.asmdef`.
- [ ] Delete `Skin.asmdef`; remove its GUID from `App.asmdef`, `Scene.asmdef`, `Tests.EditMode.asmdef`; update `using Skin;` → `using Gateway.Skin;`/`using Service.SkinService;` in consumers and tests.
- [ ] Force recompile + run-tests green.
- [ ] Commit.

**Note:** Phase 3 is cosmetic alignment with the original skin plan. If the team prefers feature-cohesive assemblies, **keeping the `Skin` assembly is also fine** — it is not a cycle risk. Decide based on team convention.

---

## Verification (whole refactor)

- `uloop compile` → `ErrorCount: 0`, **no "cyclic dependencies detected"**.
- `uloop run-tests --test-mode EditMode` → all green.
- Re-resolve the graph (script in `Docs/.../`-style: map `*.asmdef.meta` GUID→name) and confirm: `Data → Model` (no Service), `Core → Model` (no Service/Data).
- Update CLAUDE.md's assembly section again to the post-refactor (now truly "clean") graph.

## Sequencing note

Phase 1 and Phase 2 are independent and each self-contained/committable. Phase 1 is the high-value, low-risk win (it is what caused the skin cycle). Phase 2 is optional polish (Core purity). Phase 3 is optional cosmetic consolidation.
