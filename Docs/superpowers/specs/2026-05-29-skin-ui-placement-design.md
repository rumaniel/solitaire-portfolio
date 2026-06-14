# 스킨 선택 UI 배치 설계 — Settings 패널 통합 (Option A)

- **작성일**: 2026-05-29
- **상태**: 설계 승인 완료 → 구현 계획 대기
- **선행 스펙**: `Docs/superpowers/specs/2026-05-28-skin-swap-design.md`
- **관련 코드 상태**: 스킨 기능 코드는 모두 구현·테스트·커밋 완료(브랜치 `feature/skin-swap`, 347 EditMode green). 본 스펙은 그 위에 얹는 **UI 배치 결정 + 부분 리팩터링**이다.

---

## 1. 배경 & 문제

스킨 기능의 코드 레벨은 끝났으나, 선택 UI를 **어디에 둘지** 계획이 없어 사용자가 prefab 배치 단계에서 막혔다. Task 8/8b는 `LobbyComponent`/`IngameComponent`에 각각 `SkinSelectView` SerializeField를 두는 형태로 배선했지만 **배치 위치 자체는 미정**이었다.

## 2. 결정 (Option A)

**스킨 선택을 기존 `SettingPanelView`(공유 Settings 패널) 안에 통합한다.**

근거:
- `SettingPanelView`는 이미 **로비·인게임이 공유**하는 공통 패널이라 한 곳만 수정하면 양쪽이 동시에 켜진다.
- 스킨은 의미적으로 사용자 환경설정(Music/SFX/Haptic/Left-handed/Language)과 같은 카테고리. 기존 패턴과 일관.
- 추가 버튼/패널/창이 필요 없어 UI 면적·복잡도 최소.

기각된 대안:
- B. 전용 Skin 패널 + 새 토글 버튼 — Achievements 같은 격이지만 로비·인게임 양쪽에 버튼·패널 prefab 신설이 필요해 비용 ↑, 발견성 이득은 작음.
- C. 로비 메인 인라인 + 인게임은 Settings — 로비·인게임 배치 비대칭, 로비 화면 공간 차지.

## 3. UI 배치 상세

`SettingPanelView`의 토글 영역 끝(언어 드롭다운 다음)에 새 행 추가:

```
[ 🎵 Music ] ●━━○
[ 🔊 SFX  ] ●━━○
[ 📳 Haptic ] ●━━○
[ ↔ Left-handed ] ○━━●
[ 🌐 Language  ] [ Korean ▾ ]
[ 🎴 Skin     ] [ ▭ classic ✓ ] [ ▭ … ]   ← NEW
```

- 라벨: "Skin" (Localization 키는 향후 추가, 우선 영문 하드코드 라벨도 무방 — 기존 Language 행은 LocalizedString을 쓰지 않고 dropdown만 있음).
- 우측 영역: `SkinSelectView`(그리드/가로 스크롤). 현재 스킨이 1개(classic)뿐이라 인라인 그리드로 충분. 5+ 개로 늘면 가로 스크롤로 확장(별도 변경 없이 GridLayoutGroup → HorizontalLayoutGroup + ScrollRect로 조정).

## 4. 어셈블리/네임스페이스 이동 — 필수

`SettingPanelView`는 **`Component` 어셈블리**(`Component.Settings` ns), 현재 `SkinSelectView`/`SkinTileView`는 **`Scene` 어셈블리**(`Scene.Lobby.View` ns). Component는 Scene을 참조하지 않으며 거꾸로도 불가(레이어 역전).

이동:
- `Assets/Scripts/Scene/Lobby/View/SkinTileView.cs` → `Assets/Scripts/Component/Skin/SkinTileView.cs`. 네임스페이스 `Scene.Lobby.View` → `Component.Skin`.
- `Assets/Scripts/Scene/Lobby/View/SkinSelectView.cs` → `Assets/Scripts/Component/Skin/SkinSelectView.cs`. 네임스페이스 동일하게 변경.

Component asmdef 의존성(`Model`, `Data`, `Core`, `Service`, `Shared`, R3, UniTask, …) 모두 충족. 추가 asmdef 변경 불필요.

**기존 `SkinTile.prefab`은 영향 없음** — Unity는 스크립트를 GUID로 참조한다. 파일이 이동해도 스크립트 GUID는 .meta가 따라가므로 prefab의 컴포넌트 참조가 끊어지지 않는다.

## 5. `SettingPanelView` 배선 (self-contained 패턴 유지)

기존 패턴(Music/SFX/Haptic/Language → 서비스 직접 주입, presenter 없음)을 그대로 적용한다.

```csharp
// SettingPanelView additions
[SerializeField] private SkinSelectView skinSelectView;
[Inject] private ISkinService SkinService { get; set; }

// Awake() — selection 라우팅 + 현재 선택 반영
skinSelectView?.OnSkinSelectedObservable()
    .Subscribe(id => SkinService?.SelectSkinAsync(id).Forget())
    .AddTo(disposable);
SkinService?.CurrentSkinId
    .Subscribe(id => skinSelectView?.SetSelected(id))
    .AddTo(disposable);

// Show() — 매번 카탈로그로 빌드 + 현재 강조 (Language 드롭다운과 동일)
if (skinSelectView != null && SkinService != null)
{
    skinSelectView.Build(SkinService.AvailableSkins);
    skinSelectView.SetSelected(SkinService.CurrentSkinId.CurrentValue);
}
```

UI 클릭 사운드는 `SkinSelectView`/`SkinTileView` 내부 또는 SettingPanelView에서 `Subscribe` 람다에 `AudioService.Play(AudioCatalog.UI.Click)`을 추가할 수 있다. 기존 패턴(SettingPanelView의 다른 인터랙션은 사운드 없이 동작)과 일관되게, **우선 사운드 없이** 진행한다(필요 시 후속 작업).

## 6. Task 8/8b에서 제거되는 코드

`SettingPanelView`가 스킨 선택을 담당하므로 component·presenter 레벨의 스킨 선택 배선은 중복·죽은 코드가 된다. 다음을 제거:

- `LobbyComponent.cs`: `[SerializeField] SkinSelectView skinSelectView`, `OnSkinSelectedObservable`, `BuildSkinSelect`, `SetSelectedSkin` (+ 관련 using).
- `IngameComponent.cs`: 동일.
- `LobbyPresenter.cs`: Start의 스킨 build/route 블록 제거. 다른 곳에서 `ISkinService`를 더 쓰지 않으면 `[Inject]`도 제거.
- `IngamePresenter.cs`: 스킨 **선택** build/route 블록만 제거. **live re-skin 구독(`CurrentSpriteSet → Component.ApplySpriteSet`)은 유지** — 이는 표시 갱신이지 선택이 아니다. `[Inject] ISkinService`도 그대로 유지.

## 7. 변하지 않는 부분

- `UICardsController.ApplySpriteSet` / `IngameComponent.ApplySpriteSet` / `UICard.SetSpriteSet`: 표시 측 코드, 그대로.
- `IngamePresenter`의 `CurrentSpriteSet` 구독: 그대로 (인게임 live re-skin).
- `SkinService` / `ISkinCatalog` / Addressable Gateway / 모든 단위 테스트: 그대로.
- AppLifetimeScope의 등록 + AppPresenter의 `InitializeAsync`: 그대로.
- 사용자가 만든 `Assets/Prefabs/Skin/SkinTile.prefab`: 그대로(스크립트 GUID 추적).

## 8. 검증

- `uloop compile` → ErrorCount 0, 순환 없음(이미 정렬된 그래프 유지).
- `uloop run-tests --test-mode EditMode` → 347 green(기존 단위 테스트는 영향 받지 않음).
- Play 모드 스모크:
  - 로비 → ⚙ Settings 열기 → "Skin" 행 보임 → classic 강조.
  - 인게임 진입 → 카드가 classic 스프라이트로 그려짐.
  - 인게임 ⚙ Settings 열기 → "Skin" 행 보임 → 클릭 시 화면 카드 즉시 변경(live re-skin).
  - 앱 재시작 → 마지막 선택 복원.

## 9. 범위 외(따로 처리)

- 추가 스킨 에셋·썸네일 sprite — 스킨 콘텐츠 작업.
- "Skin" 라벨의 Localization 키 추가 — 현 패턴이 Language도 라벨 없이 dropdown만 두므로 일치.
- Task 5의 빠른 연타 race 가드 — 별도 옵션(이미 보고됨).
- 스킨 콘텐츠 빌드 / CDN 원격 전환 — 별도 작업.
