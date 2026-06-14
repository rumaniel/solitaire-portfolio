# 스킨 교체 기능 설계 (Addressables 기반)

- **작성일**: 2026-05-28
- **상태**: 설계 승인 완료 → 구현 계획 대기
- **관련 레이어**: Model, Data, Gateway, Service, App, Scene/Component, Tests, Addressables

---

## 1. 목표

플레이어가 카드 스킨(앞면 52장 + 뒷면을 한 단위로 묶은 비주얼 세트)을 선택해 교체할 수 있게 한다. 스킨 리소스는 Unity Addressables 키 기반으로 로드한다. 지금은 로컬 번들로 제공하되, 코드는 Addressables 키로 동작하므로 추후 원격(CDN) 전환 시 게이트웨이 한 곳만 바꾸면 된다.

## 2. 확정된 요구사항

| 항목 | 결정 |
|---|---|
| 스킨 단위 | `CardSpriteSet` 전체(앞 52장 + 뒷면)를 한 단위로 교체 |
| 선택 위치 | 로비(Lobby) + 인게임(Ingame). 인게임에서는 즉시 반영(live re-skin) |
| 로딩 위치 | 로컬 번들. Addressables 키 기반이라 원격 전환 시 코드 무변경 |
| 목록 구성 | 하이브리드 — `SkinCatalogAsset`이 메타데이터(id·표시이름·썸네일)와 Addressable 키 관리, 실제 `CardSpriteSet`은 키로 비동기 로드 |
| 영속 | PlayerPrefs에 skinId 저장, 재시작 시 복원 |
| 메모리 | 스킨 교체 시 이전 Addressable 핸들 Release |
| 초기값 | 프리팹에 박힌 기존 `CardSpriteSet`을 Addressable로 마킹해 `classic`으로 마이그레이션. 저장값이 없을 때의 초기 선택값 |
| 실패 처리 | **failover 없음.** 로드 실패는 근본 원인(데이터/빌드 버그)을 노출. 조용한 되돌리기/우회로 금지 |

## 3. 비목표 (현 범위 제외)

- 원격 CDN 다운로드 인프라(카탈로그/번들 호스팅). 단, 코드 구조는 원격 대비.
- 카드 외 배경/테이블 펠트/기타 UI 테마 교체. 스킨 단위는 카드 `CardSpriteSet` 한정.
- 스킨 구매/잠금/언락 등 상점·소유권 로직.
- 볼륨 슬라이더류 무관 기능.

## 4. 현재 코드 기준선 (조사 결과)

- `Component/Card/UICard.cs:32` — `[SerializeField] private CardSpriteSet cardSpriteSet`가 Card 프리팹에 직접 바인딩됨. `SetSpriteSet(CardSpriteSet)`(UICard.cs:101)이 존재하지만 **현재 어디서도 호출되지 않음**.
- `Data/Card/CardSpriteSet.cs` — `backSprite` + suit/rank별 front 스프라이트 리스트를 가진 ScriptableObject. `OnValidate`에서 중복 검증 + lookup 구축.
- `Component/Card/UICardsController.cs:192` — `SpawnCard`가 `cardPrefab`을 Instantiate하고 `SetCard`만 호출. 스프라이트셋은 프리팹에 박힌 값을 그대로 사용(런타임 재지정 없음).
- Addressables 2.4.1 설치됨, 그룹 설정 존재(현재 Localization만 사용). **카드 스프라이트 로딩에는 코드 사용 0건**.
- 영속 패턴 선례: `LayoutService`, `LocalizationService` 모두 PlayerPrefs + R3 `Subject`/`ReactiveProperty` + `Observable` 사용.
- `CardSpriteSet`의 DI 등록은 현재 없음.

## 5. 접근법 결정

**전용 `ISkinService`(App 싱글톤) + `ISkinAssetGateway`로 Addressables 격리.**

대안 비교:
- (채택) 서비스가 현재 스킨 상태·선택·영속을 소유하고, Addressables 로드/해제는 Gateway 뒤로 숨김 → 기존 Gateway 패턴(Auth, Stats)과 일치, 핵심 로직 EditMode 테스트 가능, 원격 전환 시 Gateway 한 곳만 변경.
- (기각) 서비스가 Addressables 직접 호출 → 클래스는 줄지만 핵심 로직이 Addressables 런타임에 묶여 EditMode 테스트 불가.
- (기각) 카탈로그가 모든 `CardSpriteSet`을 직접 참조 → Addressables 요구·원격 대비 무산, 모든 스킨이 동시에 메모리 상주.

## 6. 도메인 모델 — `Model/Skin/` (도메인 모델 우선)

모든 모델은 immutable + `IEquatable<T>` + `GetHashCode`.

- **`SkinId`** — `readonly struct`, 단일 `string Value`. `IEquatable<SkinId>` 구현. PlayerPrefs 키 및 카탈로그 조회 키로 사용.
- **`SkinInfo`** — immutable: `SkinId Id`, `string DisplayNameKey`(Localization 키), `Sprite Thumbnail`. 로비 그리드 표시용 경량 메타데이터. 무거운 `CardSpriteSet`은 포함하지 않는다.

`CardSpriteSet`은 이미 Data 레이어의 시각 에셋이므로 그대로 두고, "현재 적용된 스킨의 스프라이트"로만 흐른다.

## 7. Data — `Data/Skin/`

- **`SkinCatalogEntry`** (`[Serializable]`):
  - `SkinId id`
  - `string displayNameKey`
  - `Sprite thumbnail` — 카탈로그에 직접 참조(경량, 번들 상주). 로비 그리드가 비동기 로드 없이 표시.
  - `AssetReferenceT<CardSpriteSet> spriteSetRef` — 무거운 `CardSpriteSet`은 키로 비동기 로드.
  - Inspector 드래그·드롭으로 바인딩(인스펙터 바인딩 우선 원칙).
- **`SkinCatalogAsset : ScriptableObject`** (`[CreateAssetMenu(menuName = "Solitaire/Skin/Catalog")]`):
  - `List<SkinCatalogEntry> skins`
  - `OnValidate`에서 중복 `SkinId` 경고 + lookup 구축 (`CardSpriteSet`의 중복 검증과 동일 패턴)
  - API: `IReadOnlyList<SkinInfo> Skins`, `bool TryGet(SkinId, out SkinCatalogEntry)`, `bool Contains(SkinId)`

## 8. Gateway — `Gateway/Skin/`

Addressables를 외부 통합으로 격리 (기존 Auth·Stats Gateway 패턴).

- **`ISkinAssetGateway`**
  - `UniTask<CardSpriteSet> LoadAsync(AssetReferenceT<CardSpriteSet> reference)` — 핸들을 내부에서 추적.
  - `void Release(AssetReferenceT<CardSpriteSet> reference)` — 해당 핸들 Release.
- **`AddressableSkinAssetGateway`** — `Addressables.LoadAssetAsync` 래핑, reference→handle 매핑 보관. 로드 실패 시 예외를 그대로 전파(failover 없음).

## 9. Service — `Service/SkinService/`

- **`ISkinService`**
  - `IReadOnlyList<SkinInfo> AvailableSkins`
  - `ReadOnlyReactiveProperty<SkinId> CurrentSkinId`
  - `ReadOnlyReactiveProperty<CardSpriteSet> CurrentSpriteSet` — 인게임이 구독해 live re-skin
  - `UniTask InitializeAsync()` — 프리퍼런스에서 skinId 복원(없으면 `classic`), 카탈로그 존재 검증, 초기 `CardSpriteSet` 로드
  - `UniTask SelectSkinAsync(SkinId id)`
- **`SkinService`** (Lifetime.Singleton). `SelectSkinAsync` 순서:
  1. `id == CurrentSkinId.Value`면 no-op.
  2. 새 `CardSpriteSet`을 게이트웨이로 **먼저** 로드(빈 프레임/깜빡임 방지).
  3. 이전 핸들 Release → `CurrentSpriteSet`·`CurrentSkinId` 갱신 → 프리퍼런스 저장.
  - 현재 핸들/참조 등 누적·캐시 상태는 Model이 아니라 서비스의 private 필드에 보관(불변 Model 원칙 준수). 외부 노출(`ReactiveProperty`)은 immutable 타입(`SkinId`) 또는 시각 에셋(`CardSpriteSet`)만.
- **`ISkinPreferenceStore`** — `SkinId? Load()` / `void Save(SkinId)`. PlayerPrefs 정적 API를 인터페이스 뒤로 분리해 `SkinService`를 EditMode에서 격리 테스트 가능하게 함.
- **`PlayerPrefsSkinPreferenceStore`** — 기본 구현. 키 예: `"SelectedSkinId"`.

서비스 의존성: `ISkinAssetGateway` + `SkinCatalogAsset` + `ISkinPreferenceStore` (3개).

## 10. DI 배선 — `App/AppLifetimeScope.cs`

`SkinCatalogAsset`은 스코프에 `[SerializeField]`로 두고 Inspector 바인딩(인스펙터 우선 원칙).

```csharp
builder.RegisterInstance(skinCatalog).As<SkinCatalogAsset>();
builder.Register<AddressableSkinAssetGateway>(Lifetime.Singleton).As<ISkinAssetGateway>();
builder.Register<PlayerPrefsSkinPreferenceStore>(Lifetime.Singleton).As<ISkinPreferenceStore>();
builder.Register<SkinService>(Lifetime.Singleton).As<ISkinService>();
```

## 11. 앱 초기화 — `App/AppPresenter`

기존 스타트업 오케스트레이션에서 첫 씬 라우팅 **전에** `await SkinService.InitializeAsync()` 호출:
- 프리퍼런스 로드 → 없으면 `classic` → 카탈로그 존재 검증 → 초기 `CardSpriteSet` 로드 → `CurrentSpriteSet.Value` 세팅.
- 인게임 진입 시 스킨이 이미 준비된 상태가 됨.

## 12. 인게임 live re-skin — `Scene.Ingame` + `UICardsController`

- `IngamePresenter`(IStartable)에 `[Inject] ISkinService`. Start에서:
  ```csharp
  SkinService.CurrentSpriteSet
      .Subscribe(set => uiCardsController.ApplySpriteSet(set))
      .AddTo(disposable);
  ```
  `ReactiveProperty`라 구독 즉시 현재 스킨이 적용됨(딜 이전).
- `UICardsController.ApplySpriteSet(CardSpriteSet set)` 신설: `_currentSpriteSet` 저장 + 활성 카드 전체에 `card.SetSpriteSet(set)`(기존 메서드 재사용). `SpawnCard`도 새 카드에 `_currentSpriteSet` 적용.
- `UICard.SetSpriteSet`은 `Image.sprite`만 교체 → 드래그·애니메이션 중에도 안전, 별도 게이팅 불필요.
- 프리팹의 기존 `cardSpriteSet` 직렬화 필드는 **에디터 프리뷰용 기본값**으로만 남기고 런타임은 서비스가 덮어쓴다.

## 13. 로비 선택 UI — `Scene.Lobby`

- `SkinSelectPresenter` + `SkinSelectView`: `AvailableSkins`(카탈로그 썸네일)로 그리드 구성, `CurrentSkinId` 강조, 탭 시 `SelectSkinAsync(id)`.
- 반복 항목은 base "Skin Grid Item" prefab → variant로 배치(Prefab Variant 우선 원칙, 기존 Lobby Grid Item과 동일).
- 썸네일은 카탈로그에 직접 참조돼 있어 무거운 로드 없이 표시. 로비에서의 적용은 서비스 상태·영속만 갱신하고, 실제 카드 스프라이트는 인게임에서 반영.

## 14. 에러 처리 (failover 없음 — 근본 원인 수정 원칙)

- **로드 실패**: 예외 전파/로그. 조용히 이전 스킨으로 되돌리는 fallback 없음 — 로컬 번들 참조라 실패는 데이터/빌드 버그이므로 근본 원인을 노출한다.
- **저장된 skinId가 카탈로그에 없음**: `InitializeAsync`의 **경계 입력 검증**으로 `classic` 기본 + 경고 로그. 이는 영속 사용자 상태(경계)의 검증이며 코드 버그를 가리는 failover가 아니다 — CLAUDE.md가 경계 검증을 명시적으로 허용한다.
- **핸들 수명**: 새 셋 로드 성공 **후** 이전 핸들 Release(빈 프레임/깜빡임 방지).

## 15. 테스트 — `Assets/Tests/EditMode/`

- **`SkinIdTests`** — equality·hashcode·inequality.
- **`SkinCatalogTests`** — `TryGet`/`Contains`/중복 `SkinId` 경고/`Skins` 순서.
- **`SkinServiceTests`** (FakeSkinAssetGateway + in-memory PreferenceStore):
  - 저장값 없을 때 `classic`으로 초기화
  - 저장값 복원
  - `SelectSkinAsync`가 `CurrentSkinId`·`CurrentSpriteSet` 갱신
  - 같은 id 선택 시 no-op
  - 교체 시 이전 reference로 `Release` 호출 (순서: 새 로드 후 해제)
  - 프리퍼런스 저장 확인
  - 저장된 id가 카탈로그에 없을 때 `classic`으로 경계 검증

## 16. Addressables 셋업

- "Skins" 그룹 신설(로컬 빌드, 기본 로컬 그룹 설정).
- 각 스킨 `CardSpriteSet` 에셋을 addressable로 마킹 + label `"skin"`. 기존 classic `CardSpriteSet`도 마킹.
- 카탈로그 엔트리는 `AssetReferenceT<CardSpriteSet>`로 참조.

## 17. 변경되는 레이어 요약

| 레이어 | 신규/변경 |
|---|---|
| Model | `Skin/SkinId`, `Skin/SkinInfo` (신규) |
| Data | `Skin/SkinCatalogEntry`, `Skin/SkinCatalogAsset` (신규) |
| Gateway | `Skin/ISkinAssetGateway`, `AddressableSkinAssetGateway` (신규) |
| Service | `SkinService/ISkinService`, `SkinService`, `ISkinPreferenceStore`, `PlayerPrefsSkinPreferenceStore` (신규) |
| App | `AppLifetimeScope` 등록, `AppPresenter` 초기화 (변경) |
| Scene/Component | `IngamePresenter` 구독, `UICardsController.ApplySpriteSet` (변경), Lobby `SkinSelectPresenter/View` (신규) |
| Tests | `SkinIdTests`, `SkinCatalogTests`, `SkinServiceTests` (신규) |
| Addressables | "Skins" 그룹 + 에셋 마킹 |

## 18. asmdef 의존성 영향

- `Gateway.asmdef`는 현재 `Model`만 참조. `ISkinAssetGateway`가 `Data.Card`(`CardSpriteSet`)와 Addressables를 참조하므로 `Gateway.asmdef`에 `Data` 및 Addressables 어셈블리 참조 추가 필요.
- `Service.asmdef`는 `Model`, `Core`, `Gateway` 참조. `SkinService`가 `Data.Card`(`CardSpriteSet`)·`Data.Skin`을 참조하므로 `Service.asmdef`에 `Data` 참조 추가 필요.
- 구현 시 각 asmdef의 실제 참조 목록을 확인하고 누락분만 추가한다.

## 19. 데이터 흐름 요약

```
[앱 시작]
AppPresenter → SkinService.InitializeAsync()
  → ISkinPreferenceStore.Load() (없으면 classic)
  → 카탈로그 존재 검증
  → ISkinAssetGateway.LoadAsync(ref)
  → CurrentSpriteSet.Value 설정

[로비 선택]
SkinSelectView (카탈로그 썸네일 + CurrentSkinId 강조)
  → 탭 → SkinService.SelectSkinAsync(id)
  → 새 로드 → 이전 핸들 Release → CurrentSkinId/CurrentSpriteSet 갱신 → 프리퍼런스 저장

[인게임 진입 / live re-skin]
IngamePresenter → CurrentSpriteSet 구독
  → UICardsController.ApplySpriteSet(set)
  → 활성 카드 re-skin + 이후 SpawnCard에 적용
```
