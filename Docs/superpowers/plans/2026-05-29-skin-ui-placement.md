# 스킨 UI 배치 (Settings 패널 통합) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 스킨 선택 UI를 공유 `SettingPanelView`(로비·인게임 양쪽에서 같은 뷰)로 옮긴다. `SkinSelectView`/`SkinTileView`를 Component 어셈블리로 이동하고, 잔존하던 Lobby/Ingame 컴포넌트·프레젠터의 스킨 선택 배선을 제거한다. 인게임 live re-skin은 보존.

**Architecture:** 3-step refactor. ① 뷰 두 개를 `Scene.Lobby.View` → `Component.Skin`(파일·네임스페이스 이동, GUID 보존). ② `SettingPanelView`에 `[SerializeField] SkinSelectView` + `[Inject] ISkinService` + Awake 구독 + Show 빌드. ③ Lobby/Ingame Component·Presenter에서 redundant 스킨 선택 배선 삭제. 매 단계 후 컴파일 클린 + 347 EditMode 테스트 green.

**Tech Stack:** Unity 6, C#, VContainer(DI), R3, UniTask, NUnit(EditMode), `uloop` CLI. Component asmdef는 이미 Model·Data·Service·Core·Shared·R3·UniTask 참조 → asmdef 변경 불필요.

**기준 정보 (조사 완료):**
- `SettingPanelView` 위치: `Assets/Scripts/Component/Settings/SettingPanelView.cs`, ns `Component.Settings`, `Component` 어셈블리. ComponentBase 상속, self-contained(서비스 직접 주입), `CompositeDisposable disposable` 보유.
- 현재 `SkinSelectView`/`SkinTileView` 위치: `Assets/Scripts/Scene/Lobby/View/`, ns `Scene.Lobby.View`, `Scene` 어셈블리.
- 사용자가 이미 만든 `Assets/Prefabs/Skin/SkinTile.prefab`은 스크립트 GUID로 참조 — 파일 이동·네임스페이스 변경 모두 영향 없음.
- `ISkinService`는 App 스코프 싱글톤(이미 등록). `SkinService.AvailableSkins`/`CurrentSkinId`/`SelectSkinAsync` 시그니처는 변경 없음.
- 컴파일 후엔 항상 `uloop execute-dynamic-code --code "using UnityEditor; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); return \"ok\";"`로 임포트 강제(metas 생성) 후 `uloop compile` 폴링.

---

## File Structure

**이동 (네임스페이스 변경):**
- `Assets/Scripts/Scene/Lobby/View/SkinTileView.cs` → `Assets/Scripts/Component/Skin/SkinTileView.cs` (ns `Scene.Lobby.View` → `Component.Skin`)
- `Assets/Scripts/Scene/Lobby/View/SkinSelectView.cs` → `Assets/Scripts/Component/Skin/SkinSelectView.cs` (ns 동일 변경)
- 두 파일의 `.meta`도 함께 이동(`git mv`) — GUID 보존이 prefab 호환성에 중요.

**수정:**
- `Assets/Scripts/Component/Settings/SettingPanelView.cs` — 스킨 SerializeField + Inject + Awake 구독 + Show 빌드 추가.
- `Assets/Scripts/Scene/Lobby/LobbyComponent.cs` — 스킨 관련 field/methods/usings 제거. (이동 단계에선 `using Scene.Lobby.View;`를 유지하고 `using Component.Skin;`을 추가하는 중간 상태를 가짐.)
- `Assets/Scripts/Scene/Lobby/LobbyPresenter.cs` — Start의 스킨 build/route 블록 제거, ISkinService 사용처 없으면 [Inject]와 using 제거.
- `Assets/Scripts/Scene/Ingame/IngameComponent.cs` — 스킨 관련 field/methods/usings 제거. (live re-skin 메서드 `ApplySpriteSet`은 유지.)
- `Assets/Scripts/Scene/Ingame/IngamePresenter.cs` — Start의 스킨 SELECTION build/route 블록만 제거. **`CurrentSpriteSet → Component.ApplySpriteSet` 구독은 유지.** `[Inject] ISkinService`도 유지(구독이 필요).

**테스트:** 새 단위 테스트는 없음(UI 배선 변경, 도메인 로직 변화 없음). 기존 347 EditMode 테스트가 변하지 않고 통과해야 함.

---

## Task 1: 뷰 두 개를 Component/Skin 으로 이동 (네임스페이스 변경)

**Files:**
- Move: `Assets/Scripts/Scene/Lobby/View/SkinTileView.cs` → `Assets/Scripts/Component/Skin/SkinTileView.cs`
- Move: `Assets/Scripts/Scene/Lobby/View/SkinSelectView.cs` → `Assets/Scripts/Component/Skin/SkinSelectView.cs`
- Modify: 두 파일의 `namespace` 줄
- Modify: `Assets/Scripts/Scene/Lobby/LobbyComponent.cs` (using 추가)
- Modify: `Assets/Scripts/Scene/Ingame/IngameComponent.cs` (using 교체)

- [ ] **Step 1: 대상 디렉터리 생성 + git mv (4개 파일)**

Run:
```bash
mkdir -p Assets/Scripts/Component/Skin
git mv Assets/Scripts/Scene/Lobby/View/SkinTileView.cs       Assets/Scripts/Component/Skin/SkinTileView.cs
git mv Assets/Scripts/Scene/Lobby/View/SkinTileView.cs.meta  Assets/Scripts/Component/Skin/SkinTileView.cs.meta
git mv Assets/Scripts/Scene/Lobby/View/SkinSelectView.cs     Assets/Scripts/Component/Skin/SkinSelectView.cs
git mv Assets/Scripts/Scene/Lobby/View/SkinSelectView.cs.meta Assets/Scripts/Component/Skin/SkinSelectView.cs.meta
```

Expected: 모두 `R`(rename) 스테이징됨. `git status --short`에 `R Assets/Scripts/Scene/Lobby/View/SkinTileView.cs -> Assets/Scripts/Component/Skin/SkinTileView.cs` 등 4줄.

- [ ] **Step 2: SkinTileView 네임스페이스 변경**

Edit `Assets/Scripts/Component/Skin/SkinTileView.cs` — `namespace Scene.Lobby.View` 한 줄을 `namespace Component.Skin`으로 교체:

```csharp
using Data.Skin;
using Model.Skin;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Component.Skin
{
    /// <summary>
    /// One selectable skin tile. Base prefab "SkinTile" → variants placed in the grid
    /// (Prefab Variant 우선 원칙, GameTileView와 동일 결).
    /// </summary>
    public class SkinTileView : MonoBehaviour
    {
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectedIndicator;

        private readonly Subject<SkinId> onClickedSubject = new Subject<SkinId>();
        public Observable<SkinId> OnClickedObservable() => onClickedSubject;

        private SkinId skinId;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => onClickedSubject.OnNext(skinId));
        }

        public void Bind(SkinInfo info)
        {
            skinId = info.Id;
            if (thumbnailImage != null) thumbnailImage.sprite = info.Thumbnail;
        }

        public void SetSelected(bool selected)
        {
            if (selectedIndicator != null) selectedIndicator.SetActive(selected);
        }

        private void OnDestroy() => onClickedSubject.Dispose();
    }
}
```

- [ ] **Step 3: SkinSelectView 네임스페이스 변경**

Edit `Assets/Scripts/Component/Skin/SkinSelectView.cs` — 같은 한 줄 교체:

```csharp
using System.Collections.Generic;
using Data.Skin;
using Model.Skin;
using R3;
using UnityEngine;

namespace Component.Skin
{
    /// <summary>
    /// Builds a grid of SkinTileView from the available skins and surfaces selection clicks.
    /// Highlights the current skin. Token-light: thumbnails come from the catalog (already bundled).
    /// </summary>
    public class SkinSelectView : MonoBehaviour
    {
        [SerializeField] private Transform tileParent;
        [SerializeField] private SkinTileView tilePrefab;

        private readonly List<SkinTileView> tiles = new List<SkinTileView>();
        private readonly Subject<SkinId> onSkinSelectedSubject = new Subject<SkinId>();
        private readonly Dictionary<SkinTileView, SkinId> tileIds = new Dictionary<SkinTileView, SkinId>();

        public Observable<SkinId> OnSkinSelectedObservable() => onSkinSelectedSubject;

        public void Build(IReadOnlyList<SkinInfo> skins)
        {
            Clear();
            if (tilePrefab == null || tileParent == null) return;

            foreach (var info in skins)
            {
                var tile = Instantiate(tilePrefab, tileParent);
                tile.Bind(info);
                tileIds[tile] = info.Id;
                tile.OnClickedObservable()
                    .Subscribe(id => onSkinSelectedSubject.OnNext(id))
                    .AddTo(tile);
                tiles.Add(tile);
            }
        }

        public void SetSelected(SkinId currentId)
        {
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                tile.SetSelected(tileIds.TryGetValue(tile, out var id) && id.Equals(currentId));
            }
        }

        private void Clear()
        {
            foreach (var tile in tiles)
                if (tile != null) Destroy(tile.gameObject);
            tiles.Clear();
            tileIds.Clear();
        }

        private void OnDestroy()
        {
            onSkinSelectedSubject.Dispose();
        }
    }
}
```

- [ ] **Step 4: LobbyComponent에 `using Component.Skin;` 추가**

기존 `using Scene.Lobby.View;`(GameTileView/DailyTileView 등에 필요)는 유지하면서, SkinSelectView를 새 위치에서 해석하도록 `using Component.Skin;`를 추가.

Edit `Assets/Scripts/Scene/Lobby/LobbyComponent.cs` — 11번 줄 근처의 using 블록을 다음으로 교체:

old:
```csharp
using R3;
using Scene.Ingame.View;
using Scene.Lobby.View;
using UnityEngine;
```

new:
```csharp
using Component.Skin;
using R3;
using Scene.Ingame.View;
using Scene.Lobby.View;
using UnityEngine;
```

- [ ] **Step 5: IngameComponent의 using `Scene.Lobby.View` → `Component.Skin`**

Edit `Assets/Scripts/Scene/Ingame/IngameComponent.cs`:

old:
```csharp
using R3;
using Scene.Ingame.View;
using Scene.Lobby.View;
using Data.Skin;
using Model.Skin;
using System.Collections.Generic;
using UnityEngine;
```

new:
```csharp
using R3;
using Scene.Ingame.View;
using Component.Skin;
using Data.Skin;
using Model.Skin;
using System.Collections.Generic;
using UnityEngine;
```

- [ ] **Step 6: 강제 임포트 + 컴파일**

Run:
```bash
uloop execute-dynamic-code --code "using UnityEditor; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); return \"ok\";"
```

Then poll compile:
```bash
for i in $(seq 1 60); do
  out=$(uloop compile 2>&1)
  if echo "$out" | grep -qiE "Domain Reload|already in progress|compiling scripts|server is not"; then sleep 4; continue; fi
  echo "$out" | grep -E '"Success"|"ErrorCount"'
  echo "$out" | grep -oE 'Assets[\\/][^"]*\.cs\([0-9]+,[0-9]+\): error CS[0-9]+: [^"]*' | head -20
  break
done
```

Expected: `"Success": true`, `"ErrorCount": 0`, no error lines.

- [ ] **Step 7: 전체 EditMode 테스트 (회귀 확인)**

Run:
```bash
for i in $(seq 1 30); do
  out=$(uloop run-tests --test-mode EditMode 2>&1)
  if echo "$out" | grep -qiE "Domain Reload|already in progress|compiling scripts|server is not|main thread|Internal error"; then sleep 4; continue; fi
  echo "$out"; break
done
```

Expected: `"TestCount": 347, "PassedCount": 341, "FailedCount": 0, "SkippedCount": 6`.

- [ ] **Step 8: 커밋**

Run:
```bash
git add Assets/Scripts/Component/Skin/SkinTileView.cs       Assets/Scripts/Component/Skin/SkinTileView.cs.meta \
        Assets/Scripts/Component/Skin/SkinSelectView.cs     Assets/Scripts/Component/Skin/SkinSelectView.cs.meta \
        Assets/Scripts/Scene/Lobby/LobbyComponent.cs        Assets/Scripts/Scene/Ingame/IngameComponent.cs
git commit -m "$(cat <<'EOF'
refactor(skin): move SkinSelectView/SkinTileView to Component/Skin

Settings panel (in Component assembly) needs the views accessible. Moving them to
Component.Skin namespace keeps the script GUIDs intact, so the existing SkinTile
prefab the user authored continues to bind to SkinTileView. Lobby/Ingame
components are still consumers in this step — they pick the views up from the
new namespace via 'using Component.Skin'.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: SettingPanelView에 스킨 통합

**Files:**
- Modify: `Assets/Scripts/Component/Settings/SettingPanelView.cs`

- [ ] **Step 1: using 4종 추가**

Edit `Assets/Scripts/Component/Settings/SettingPanelView.cs`:

old:
```csharp
using System;
using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using Model.App;
using R3;
using Service.AudioService;
using Service.HapticService;
using Service.LayoutService;
using Service.LocalizationService;
using Service.UserService;
```

new:
```csharp
using System;
using System.Collections.Generic;
using Component.Skin;
using Core;
using Cysharp.Threading.Tasks;
using Model.App;
using R3;
using Service.AudioService;
using Service.HapticService;
using Service.LayoutService;
using Service.LocalizationService;
using Service.SkinService;
using Service.UserService;
```

- [ ] **Step 2: SerializeField + [Inject] 추가**

Edit `Assets/Scripts/Component/Settings/SettingPanelView.cs` — `[SerializeField] private TMP_Dropdown languageDropdown;` 다음 줄에 새 SerializeField를 추가하고, `[Inject] private ILocalizationService LocalizationService { get; set; }` 다음 줄에 새 Inject를 추가.

old:
```csharp
        [SerializeField] private LicensesPanelView licensesPanelView;
        [SerializeField] private TMP_Dropdown languageDropdown;

        [Inject] private IAudioService AudioService { get; set; }
        [Inject] private IHapticService HapticService { get; set; }
        [Inject] private ILayoutService LayoutService { get; set; }
        [Inject] private IUserService UserService { get; set; }
        [Inject] private IAppConfig AppConfig { get; set; }
        [Inject] private ILocalizationService LocalizationService { get; set; }
```

new:
```csharp
        [SerializeField] private LicensesPanelView licensesPanelView;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private SkinSelectView skinSelectView;

        [Inject] private IAudioService AudioService { get; set; }
        [Inject] private IHapticService HapticService { get; set; }
        [Inject] private ILayoutService LayoutService { get; set; }
        [Inject] private IUserService UserService { get; set; }
        [Inject] private IAppConfig AppConfig { get; set; }
        [Inject] private ILocalizationService LocalizationService { get; set; }
        [Inject] private ISkinService SkinService { get; set; }
```

- [ ] **Step 3: Awake에서 스킨 구독 추가**

Edit `Assets/Scripts/Component/Settings/SettingPanelView.cs` — Awake() 내부, UserService 구독 직전(`if (UserService != null)`) 위치에 스킨 구독 블록을 추가.

old:
```csharp
            // Re-sync the dropdown when locale changes from elsewhere.
            if (LocalizationService != null && languageDropdown != null)
            {
                LocalizationService.OnLocaleChanged
                    .Subscribe(_ => SyncDropdownSelection())
                    .AddTo(disposable);
            }

            if (UserService != null)
```

new:
```csharp
            // Re-sync the dropdown when locale changes from elsewhere.
            if (LocalizationService != null && languageDropdown != null)
            {
                LocalizationService.OnLocaleChanged
                    .Subscribe(_ => SyncDropdownSelection())
                    .AddTo(disposable);
            }

            // Skin selection — route taps to the service, mirror current selection in the grid.
            if (skinSelectView != null)
            {
                skinSelectView.OnSkinSelectedObservable()
                    .Subscribe(id => SkinService?.SelectSkinAsync(id).Forget())
                    .AddTo(disposable);
            }
            if (SkinService != null && skinSelectView != null)
            {
                SkinService.CurrentSkinId
                    .Subscribe(id => skinSelectView.SetSelected(id))
                    .AddTo(disposable);
            }

            if (UserService != null)
```

- [ ] **Step 4: Show에서 스킨 그리드 빌드 추가**

Edit `Assets/Scripts/Component/Settings/SettingPanelView.cs` — Show() 안, `PopulateLanguageDropdown()` 호출 바로 다음에 스킨 빌드 호출을 추가.

old:
```csharp
            PopulateLanguageDropdown();

            licensesPanelView?.Hide();
            panel.SetActive(true);
```

new:
```csharp
            PopulateLanguageDropdown();

            if (skinSelectView != null && SkinService != null)
            {
                skinSelectView.Build(SkinService.AvailableSkins);
                skinSelectView.SetSelected(SkinService.CurrentSkinId.CurrentValue);
            }

            licensesPanelView?.Hide();
            panel.SetActive(true);
```

- [ ] **Step 5: 강제 임포트 + 컴파일**

Run:
```bash
uloop execute-dynamic-code --code "using UnityEditor; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); return \"ok\";"
for i in $(seq 1 60); do
  out=$(uloop compile 2>&1)
  if echo "$out" | grep -qiE "Domain Reload|already in progress|compiling scripts|server is not"; then sleep 4; continue; fi
  echo "$out" | grep -E '"Success"|"ErrorCount"'
  echo "$out" | grep -oE 'Assets[\\/][^"]*\.cs\([0-9]+,[0-9]+\): error CS[0-9]+: [^"]*' | head -20
  break
done
```

Expected: `"Success": true`, `"ErrorCount": 0`, no error lines.

- [ ] **Step 6: 전체 EditMode 테스트**

Run:
```bash
for i in $(seq 1 30); do
  out=$(uloop run-tests --test-mode EditMode 2>&1)
  if echo "$out" | grep -qiE "Domain Reload|already in progress|compiling scripts|server is not|main thread|Internal error"; then sleep 4; continue; fi
  echo "$out"; break
done
```

Expected: `"TestCount": 347, "PassedCount": 341, "FailedCount": 0`.

- [ ] **Step 7: 커밋**

Run:
```bash
git add Assets/Scripts/Component/Settings/SettingPanelView.cs
git commit -m "$(cat <<'EOF'
feat(skin): integrate skin selection into shared SettingPanelView

SettingPanelView gains a SerializeField SkinSelectView and an [Inject] ISkinService,
matching its existing self-contained pattern (Music/SFX/Haptic/Language read
services directly, no presenter). Awake wires selection routing + current-skin
mirroring; Show rebuilds the grid from AvailableSkins. Lobby and ingame share
this same view, so both surfaces get skin selection from one place.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Lobby/Ingame의 잔존 스킨 선택 배선 제거

**Files:**
- Modify: `Assets/Scripts/Scene/Lobby/LobbyComponent.cs`
- Modify: `Assets/Scripts/Scene/Lobby/LobbyPresenter.cs`
- Modify: `Assets/Scripts/Scene/Ingame/IngameComponent.cs`
- Modify: `Assets/Scripts/Scene/Ingame/IngamePresenter.cs`

> 인게임의 **live re-skin**(`CurrentSpriteSet` 구독 → `Component.ApplySpriteSet`)은 표시 갱신이므로 **유지**한다. SELECTION 배선만 제거.

- [ ] **Step 1: LobbyComponent — skin 필드/메서드/usings 제거**

Edit `Assets/Scripts/Scene/Lobby/LobbyComponent.cs`.

(a) using 제거 (Task 1에서 추가한 `using Component.Skin;`, 그리고 Task 8의 `using Data.Skin;`/`using Model.Skin;`):

old:
```csharp
using System.Collections.Generic;
using Component.Achievement;
using Component.CodeInput;
using Component.Settings;
using Component.Skin;
using Data.Skin;
using Gateway.Snapshot;
using Model.Game;
using Model.Skin;
using R3;
```

new:
```csharp
using System.Collections.Generic;
using Component.Achievement;
using Component.CodeInput;
using Component.Settings;
using Gateway.Snapshot;
using Model.Game;
using R3;
```

(b) `[Header("Skin")]` SerializeField 블록 제거:

old:
```csharp
        [Header("Skin")]
        [SerializeField] private SkinSelectView skinSelectView;

        [Header("Localized Strings")]
```

new:
```csharp
        [Header("Localized Strings")]
```

(c) 3개 스킨 forward 메서드 제거:

old:
```csharp
        public Observable<SkinId> OnSkinSelectedObservable
            => skinSelectView != null ? skinSelectView.OnSkinSelectedObservable() : Observable.Empty<SkinId>();

        public void BuildSkinSelect(IReadOnlyList<SkinInfo> skins) => skinSelectView?.Build(skins);

        public void SetSelectedSkin(SkinId currentId) => skinSelectView?.SetSelected(currentId);

        public void ShowAchievementPanel() => achievementPanelView?.Show();
```

new:
```csharp
        public void ShowAchievementPanel() => achievementPanelView?.Show();
```

- [ ] **Step 2: LobbyPresenter — 스킨 build/route 블록 + ISkinService 주입 제거**

Edit `Assets/Scripts/Scene/Lobby/LobbyPresenter.cs`.

(a) `using Service.SkinService;` 제거 (스킨 선택을 LobbyPresenter가 더 이상 다루지 않음).

(b) `[Inject] private ISkinService SkinService { get; set; }` 한 줄 제거.

(c) Start() 안의 스킨 build/route 블록 4개 호출(`Component.BuildSkinSelect(...)`, `Component.SetSelectedSkin(...)`, `SkinService.CurrentSkinId.Subscribe(...).AddTo(...)`, `Component.OnSkinSelectedObservable.Subscribe(id => { ... SkinService.SelectSkinAsync(id).Forget(); }).AddTo(...)`) 제거. 정확한 텍스트는 다음을 그대로 삭제:

```csharp
            Component.BuildSkinSelect(SkinService.AvailableSkins);
            Component.SetSelectedSkin(SkinService.CurrentSkinId.CurrentValue);

            SkinService.CurrentSkinId
                .Subscribe(id => Component.SetSelectedSkin(id))
                .AddTo(Component);

            Component.OnSkinSelectedObservable
                .Subscribe(id => SkinService.SelectSkinAsync(id).Forget())
                .AddTo(Component);
```

(다른 구독 블록들은 그대로 두고 이 4개 호출만 삭제. 정확한 줄 번호는 파일에 따라 다르므로 위 텍스트로 찾기.)

- [ ] **Step 3: IngameComponent — skin 필드/메서드/usings 제거 (ApplySpriteSet은 유지)**

Edit `Assets/Scripts/Scene/Ingame/IngameComponent.cs`.

(a) using 제거 (Task 1·8b가 추가한 4개):

old:
```csharp
using R3;
using Scene.Ingame.View;
using Component.Skin;
using Data.Skin;
using Model.Skin;
using System.Collections.Generic;
using UnityEngine;
```

new:
```csharp
using R3;
using Scene.Ingame.View;
using UnityEngine;
```

(b) `[Header("Skin")]` SerializeField 블록 제거:

old:
```csharp
        [SerializeField] private ToastView toastView;

        [Header("Skin")]
        [SerializeField] private SkinSelectView skinSelectView;

        [Header("Input")]
```

new:
```csharp
        [SerializeField] private ToastView toastView;

        [Header("Input")]
```

(c) 3개 스킨 forward 메서드 제거(`OnSkinSelectedObservable`, `BuildSkinSelect`, `SetSelectedSkin`):

old:
```csharp
        public void HideSettingPanel() => settingPanelView?.Hide();

        public Observable<SkinId> OnSkinSelectedObservable
            => skinSelectView != null ? skinSelectView.OnSkinSelectedObservable() : Observable.Empty<SkinId>();

        public void BuildSkinSelect(IReadOnlyList<SkinInfo> skins) => skinSelectView?.Build(skins);

        public void SetSelectedSkin(SkinId currentId) => skinSelectView?.SetSelected(currentId);
```

new:
```csharp
        public void HideSettingPanel() => settingPanelView?.Hide();
```

(d) `ApplySpriteSet`은 **그대로 둔다** — live re-skin이 사용.

- [ ] **Step 4: IngamePresenter — 스킨 SELECTION build/route만 제거 (CurrentSpriteSet 구독은 유지)**

Edit `Assets/Scripts/Scene/Ingame/IngamePresenter.cs`.

Start() 안의 다음 4개 호출(스킨 선택 배선)만 제거:

```csharp
            Component.BuildSkinSelect(SkinService.AvailableSkins);
            Component.SetSelectedSkin(SkinService.CurrentSkinId.CurrentValue);
            SkinService.CurrentSkinId
                .Subscribe(id => Component.SetSelectedSkin(id))
                .AddTo(Component);
            Component.OnSkinSelectedObservable
                .Subscribe(id =>
                {
                    AudioService.Play(AudioCatalog.UI.Click);
                    SkinService.SelectSkinAsync(id).Forget();
                })
                .AddTo(Component);
```

**유지해야 하는 것** (인게임 live re-skin):
```csharp
            SkinService.CurrentSpriteSet
                .Where(set => set != null)
                .Subscribe(set => Component.ApplySpriteSet(set))
                .AddTo(Component);
```

`[Inject] private ISkinService SkinService { get; set; }`도 **유지**(`CurrentSpriteSet` 구독에 필요). `using Service.SkinService;`도 **유지**.

- [ ] **Step 5: 강제 임포트 + 컴파일**

Run:
```bash
uloop execute-dynamic-code --code "using UnityEditor; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); return \"ok\";"
for i in $(seq 1 60); do
  out=$(uloop compile 2>&1)
  if echo "$out" | grep -qiE "Domain Reload|already in progress|compiling scripts|server is not"; then sleep 4; continue; fi
  echo "$out" | grep -E '"Success"|"ErrorCount"'
  echo "$out" | grep -oE 'Assets[\\/][^"]*\.cs\([0-9]+,[0-9]+\): error CS[0-9]+: [^"]*' | head -20
  break
done
```

Expected: `"Success": true`, `"ErrorCount": 0`, no error lines.

- [ ] **Step 6: 전체 EditMode 테스트**

Run:
```bash
for i in $(seq 1 30); do
  out=$(uloop run-tests --test-mode EditMode 2>&1)
  if echo "$out" | grep -qiE "Domain Reload|already in progress|compiling scripts|server is not|main thread|Internal error"; then sleep 4; continue; fi
  echo "$out"; break
done
```

Expected: `"TestCount": 347, "PassedCount": 341, "FailedCount": 0`.

- [ ] **Step 7: 커밋**

Run:
```bash
git add Assets/Scripts/Scene/Lobby/LobbyComponent.cs        Assets/Scripts/Scene/Lobby/LobbyPresenter.cs \
        Assets/Scripts/Scene/Ingame/IngameComponent.cs      Assets/Scripts/Scene/Ingame/IngamePresenter.cs
git commit -m "$(cat <<'EOF'
refactor(skin): remove redundant skin-select wiring from lobby/ingame

SettingPanelView now owns skin selection (in both lobby and ingame scenes since
they share the panel). Drops the per-component fields, forwards, and presenter
wiring added in Task 8/8b. IngamePresenter keeps the CurrentSpriteSet
subscription — that's display (live re-skin), not selection. IngameComponent
keeps ApplySpriteSet for the same reason.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## 사용자 수작업 (구현 계획 후, 에디터에서)

코드 변경은 위 3 태스크로 끝. 그 후 에디터에서:

1. `SkinSelectView` 컴포넌트를 SettingPanelView 프리팹/씬 안에 배치(현재 Music/SFX/Haptic/Language 행 다음). `tileParent`/`tilePrefab` 바인딩(이미 만드신 `Assets/Prefabs/Skin/SkinTile.prefab` 사용).
2. SettingPanelView Inspector의 새 `Skin Select View` 필드에 위 SkinSelectView를 드래그.
3. 씬 저장.
4. Play 모드 스모크: 로비 → ⚙ → "Skin" 영역에 classic 타일 표시·강조. 인게임 진입 시 classic 스프라이트. 인게임 ⚙ → 스킨 클릭하면 카드 즉시 변경. 재시작 후 마지막 선택 복원.

---

## Self-Review (작성자 점검 결과)

**Spec coverage** — 스펙 각 절 대응:
- §3 UI 배치 상세 → Task 2(SettingPanelView 통합) + 사용자 수작업.
- §4 어셈블리/네임스페이스 이동 → Task 1.
- §5 SettingPanelView 배선 → Task 2.
- §6 Task 8/8b 코드 제거 → Task 3.
- §7 유지되는 부분 → Task 3의 명시적 "유지" 표기.
- §8 검증 → 각 태스크의 compile + run-tests 단계.

누락 없음.

**Placeholder scan** — "TBD/적절히 처리" 류 없음. 모든 코드 변경 단계에 정확한 old/new 텍스트 또는 정확한 코드 블록 포함. uloop 명령·예상 출력 명시.

**Type consistency** — 사용된 타입은 스펙과 기존 코드와 일치: `SkinSelectView`(이동 후 ns `Component.Skin`), `SkinTileView`(동), `ISkinService`/`SkinService.AvailableSkins`/`CurrentSkinId.CurrentValue`/`CurrentSkinId.Subscribe`/`SelectSkinAsync(id).Forget()`/`CurrentSpriteSet`, `ApplySpriteSet`, R3 `Observable.Empty<SkinId>()`. 모두 기존 시그니처 그대로.
