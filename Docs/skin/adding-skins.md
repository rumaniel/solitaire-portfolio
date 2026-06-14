# 스킨 추가 & 배포 가이드

스킨 한 개를 더하거나 향후 원격으로 옮길 때 무엇을 하는지 정리한 운영 문서. 코드 작업은 없고 **에디터·Addressables 설정·자산 파이프라인** 중심.

## 0. 한 줄 요약

코드는 이미 키 기반(remote-ready)이라 **로컬 → 원격 전환에 C# 변경 없음**. 새 스킨을 추가하든 원격으로 옮기든 결국 건드리는 건 다음 셋뿐:

1. `CardSpriteSet` 에셋 (아트 작업)
2. Addressables 그룹/엔트리 (어드레스 + 라벨)
3. `SkinCatalog.asset` (한 줄 추가)

---

## 1. 새 스킨 1개 추가 (현재 = 로컬 번들)

### Step 1 — `CardSpriteSet` 에셋 만들기

1. Project 창에서 `Assets/ScriptableObjects/Skins/Cards/` 우클릭 → `Create > Solitaire > Card > Sprite Set`.
2. 이름은 사람이 알아보는 이름으로 (예: `Neon Card.asset`). **파일명은 자유**, id와 분리됨.
3. Inspector에서:
   - `Back Sprite`: 카드 뒷면 Sprite 드래그.
   - `Front Sprites`: 52장의 Suit/Rank ↔ Sprite 매핑. `OnValidate`가 중복 항목을 콘솔에 경고하므로 누락·중복 즉시 확인.

> 모든 52장이 채워져 있어야 카드가 정상 표시됩니다. 누락 시 해당 카드는 sprite 없이 그려집니다(런타임 예외는 없지만 시각적으로 빈 카드).

### Step 2 — 에셋을 Addressable로 마킹

1. 새 `CardSpriteSet` 에셋 선택.
2. Inspector 상단 **Addressable** 체크박스 ON.
3. 어드레스를 **`skin/<id>`** 형식으로 (예: `skin/neon`). 카탈로그 항목의 `id`와 같은 문자열을 쓰세요.
4. Label에 **`skin`** 추가 (이후 라벨 쿼리 확장 시 유용).
5. 같은 화면 우측의 그룹 드롭다운에서 **`Skins`** 그룹 선택.

> `skin/<id>`는 강제는 아닙니다 — `SkinService`는 카탈로그의 `AssetReference`를 그대로 사용하므로 어드레스 문자열을 직접 참조하지 않습니다. 다만 사람·디버깅용 명명 컨벤션으로 권장.

### Step 3 — `SkinCatalog.asset`에 항목 추가

1. `Assets/ScriptableObjects/Skins/SkinCatalog.asset` 열기.
2. Inspector의 `Skins` 리스트에 **항목 추가** (`+` 버튼).
3. 새 항목 필드:
   - **Id**: `neon` 등 — Addressable 어드레스의 `skin/` 뒤 부분과 같게.
   - **Display Name Key**: Localization 키 (예: `skin.neon`). 키만 적고 실제 번역은 Localization 테이블에 별도로 추가.
   - **Thumbnail**: 작은 미리보기 Sprite. **권장: 그 스킨 CardSpriteSet의 `Back Sprite`**(현재 classic도 그렇게 자동 설정됨, 커밋 `423071d`).
   - **Sprite Set Ref**: Step 1에서 만든 `CardSpriteSet` 에셋을 드래그. (Inspector가 Addressable로 마킹된 에셋만 받습니다.)
4. 중복 id가 있으면 `SkinCatalogLookup`이 콘솔에 경고하고 첫 항목만 살아남으니, id는 반드시 고유하게.

### Step 4 — 검증

- **에디터 Play**: Addressables 콘텐츠 빌드 불필요(기본 Play 모드 = `Use Asset Database` — 어셋데이터베이스에서 직접 로드).
  - Settings 패널 열기 → 스킨 행에 새 타일 노출 확인.
  - 새 타일 탭 → 화면 카드가 새 스킨으로 변경 확인.
  - 앱 재시작 → 마지막 선택 복원 확인.
- 모바일/PC **빌드 테스트**가 필요하면 `Window > Asset Management > Addressables > Groups > Build > New Build > Default Build Script` 한 번 돌리고 Player 빌드.

### Step 5 — 커밋 대상

```
Assets/ScriptableObjects/Skins/Cards/<new-cardset>.asset (+ .meta)
Assets/AddressableAssetsData/AddressableAssetSettings.asset  (수정)
Assets/AddressableAssetsData/AssetGroups/Skins.asset           (수정)
Assets/ScriptableObjects/Skins/SkinCatalog.asset               (수정)
(그 스킨이 새 Sprite 텍스처를 쓰면 그 텍스처들 + .meta)
```

---

## 2. 로컬 → 원격 전환 (향후)

### 코드 변경

**없음.** `AddressableSkinAssetGateway`는 `reference.LoadAssetAsync<CardSpriteSet>()`을 호출하고 Addressables가 카탈로그 매핑에 따라 로컬 번들이든 원격 URL이든 알아서 fetch합니다.

### 에디터/배포 변경

1. **그룹 빌드/로드 경로를 원격으로**
   - `Assets/AddressableAssetsData/AssetGroups/Skins.asset` 선택.
   - `BundledAssetGroupSchema`의 **Build Path** = `RemoteBuildPath`, **Load Path** = `RemoteLoadPath`.
   - (선택) `Use Asset Bundle Crc`, `Use Asset Bundle Cache` 기본값 유지 권장.

2. **AddressableAssetSettings에서 원격 카탈로그 설정**
   - `Assets/AddressableAssetsData/AddressableAssetSettings.asset`:
     - `Build Remote Catalog` = ON
     - `Remote Catalog Build Path` = `RemoteBuildPath`
     - `Remote Catalog Load Path` = `RemoteLoadPath`

3. **Profile에 실제 CDN URL 매핑**
   - Addressables Profiles 창 (`Window > Asset Management > Addressables > Profiles`)에서 환경별(dev/staging/prod) Profile 만들기.
   - `RemoteBuildPath`(예: `ServerData/[BuildTarget]`)와 `RemoteLoadPath`(예: `https://cdn.example.com/skins/[BuildTarget]`)를 환경별로 설정.
   - 빌드 직전 Profile을 prod로 전환.

4. **콘텐츠 빌드 → 업로드**
   - `Addressables Groups > Build > New Build > Default Build Script` → 결과물이 `ServerData/<platform>/`에 생성.
   - 생성된 `catalog_*.json`, `catalog_*.hash`, `*.bundle` 파일을 CDN에 그대로 업로드.
   - 디렉터리 구조와 파일 이름은 Addressables가 만든 그대로 유지. (해시·CRC가 묶여 있어 이름 바꾸면 못 찾음.)

5. **앱 빌드**
   - Player 빌드 시 카탈로그가 원격을 가리키도록 빌드된 카탈로그가 같이 들어감.
   - 추후 스킨 추가/교체 시 **앱 재빌드 없이** Addressables만 다시 빌드해 CDN 갱신 → 사용자는 다음 실행 시 새 카탈로그를 fetch.

### 보존되는 동작

- 코드/Inspector 측 변경 없음.
- `SkinService.SelectSkinAsync`의 await가 로컬에서 즉시 끝나던 게 원격에선 네트워크 시간만큼 늘어남 — UI에 로딩 인디케이터를 띄울지 고민 필요(현재는 await 동안 카드 그대로 표시되니 깜빡임 없음, 이건 의도된 설계).
- 실패 시 예외 전파(failover 없음) 정책 그대로 — 네트워크 에러는 호출자(현재 `SkinService.SelectSkinAsync.Forget()`)에서 로그만 남고 사용자 선택 상태는 이전 값 유지. 명시적 사용자 안내 토스트가 필요하면 `SelectSkinAsync` 호출부에 try/await 패턴으로 보강.

### 어디까지 “원격”인가

- **스킨 그룹만 원격**: 다른 Addressables 그룹(예: Localization)은 로컬 유지. 그룹별 schema가 독립적이라 안전.
- **로컬 폴백 없음**: 메모리 피드백("failover 금지")에 따라 일부러 안 둡니다. CDN 장애 = 명시적 에러(=원인 노출). 임의의 안전망을 두지 않습니다.

---

## 3. 빌드 포함 vs 제외를 스킨별로 제어

§2의 전환은 **모든 스킨을 원격으로 일괄 이동**하는 경우입니다. 일부는 Player에 동봉(=무조건 로컬, 오프라인 동작), 일부는 원격(시즌·프리미엄·확장) 식으로 **스킨별 포함/제외**가 필요하면 그룹을 둘로 나눠야 합니다.

핵심: **그룹을 둘로 나누고 스킨을 옮긴다.** 코드·카탈로그·`SkinTile.prefab`은 한 줄도 안 바뀝니다.

### 권장 구조

```
Skins-Local   (Local Build/Load Path)   ← 기본·필수 스킨 (classic 등)
Skins-Remote  (Remote Build/Load Path)  ← 시즌·프리미엄·확장 스킨
```

`SkinCatalog.asset`은 **하나 그대로** — 로컬·원격 항목이 한 카탈로그에 섞여도 됩니다. `spriteSetRef`(AssetReference)는 GUID라 어느 그룹에 들어 있든 `gateway.LoadAsync(reference)`가 알아서 라우팅합니다.

### 그룹 만들고 옮기는 절차

1. Addressables Groups 창 → `Create > Group > Packed Assets` → 이름 `Skins-Remote`.
2. 새 그룹의 `BundledAssetGroupSchema`:
   - Build Path = `RemoteBuildPath`
   - Load Path = `RemoteLoadPath`
3. (선택) 기존 `Skins`를 `Skins-Local`로 rename — Build/Load Path는 Local 그대로 둠.
4. 원격으로 빼고 싶은 `CardSpriteSet` 에셋을 `Skins-Remote` 그룹으로 드래그. 어드레스(`skin/<id>`)·label(`skin`)은 그대로 유지.
5. `AddressableAssetSettings`에서 `Build Remote Catalog = ON` + RemoteCatalog Build/Load Path 설정(§2와 동일).

### ⚠️ Thumbnail 함정 (가장 잘 빠지는 곳)

`SkinCatalogEntry.thumbnail`은 `AssetReference`가 아니라 **직접 `Sprite` 참조**입니다. Addressables는 빌드 시 원격 그룹의 에셋을 Player에서 제거하므로, thumbnail이 원격 그룹 sprite를 가리키면 빌드 후 missing이 되어 타일이 비어 보입니다. 특히 classic은 현재 `CardSpriteSet.BackSprite`를 thumbnail로 재활용 중인데, 그 CardSpriteSet을 원격으로 옮기면 thumbnail도 같이 깨집니다.

처리법 (택1):

- **A. 권장 — 코드 무변경**: 원격 스킨용 작은 미리보기 sprite를 별도 만들어 **비-Addressable** 또는 **`Skins-Local`** 그룹에 두고, 카탈로그 entry의 `thumbnail`에 그것을 드래그.
- **B. 코드 변경 (YAGNI)**: `SkinCatalogEntry.thumbnail`을 `AssetReferenceSprite`로 바꾸고 `SkinTileView`를 비동기 thumbnail 로드로 수정. 현재 1-N 정적 매핑이라 미루는 게 맞음.

### "아예 빌드에서 숨기기" (카탈로그에도 노출 X)

빌드별로 다른 스킨 셋을 노출하려면:

- 카탈로그 자체를 여러 버전 만들어 `AppLifetimeScope.skinCatalog`에 환경별로 다른 카탈로그를 바인딩 (build profile / variant 활용).
- 더 단순: 카탈로그는 하나로 두고 entry에 잠금/언락 메타 추가, UI에서 필터링. (게임플레이 기능이라 별도 스펙.)

### 검증

- **에디터 Play (기본 = Use Asset Database)**: 그룹 상관없이 모두 보임. **분리 효과는 빌드 후에만 드러납니다** — 에디터로만 확인하면 안 됨.
- **Packed Play Mode**: Addressables Profile/Play Mode를 "Use Existing Build"로 바꾼 뒤 Addressables Build → Play → 진짜 원격 흐름 재현.
- **실기기 빌드 + 비행기 모드**: Local 스킨 동작, Remote 스킨 선택 시 명시적 로드 실패 (no-failover 정책 그대로).

### 운영 체크리스트 — 스킨 1개를 원격 전용으로 옮길 때

1. `CardSpriteSet`을 `Skins-Remote` 그룹으로 이동.
2. (필요 시) 작은 thumbnail sprite를 별도 만들어 카탈로그 entry에 연결(위 "Thumbnail 함정" A안).
3. Addressables Build (Default Build Script).
4. `ServerData/<platform>/`의 `catalog_*.json` / `.hash` / `.bundle`을 CDN에 업로드.
5. 다음 Player 빌드부터 그 스킨은 Player에 동봉되지 않음.

---

## 4. 자주 빠지는 함정

- **`SkinCatalogEntry.id`와 `Addressable Address`가 달라도 동작**: 카탈로그의 `AssetReference`가 GUID로 직접 묶이므로 어드레스는 자유. 하지만 디버깅 통일성을 위해 `skin/<id>` 컨벤션 유지 권장.
- **`thumbnail`을 비우면 타일이 시각적으로 비어 보임**: 카탈로그에 항목 추가 시 잊지 말 것. 권장 기본값은 그 스킨의 `BackSprite`.
- **카탈로그가 미바인딩**: `AppLifetimeScope`의 `Skin > Skin Catalog` 필드가 비어 있으면 시작에서 `InvalidOperationException` (fail-fast). 게임이 켜지면 이 단계는 통과한 것.
- **새 스킨이 카탈로그에 추가됐는데 안 보임**: 카탈로그 `OnValidate`/lookup이 빌드되는 타이밍 이슈일 때가 있음 — 카탈로그 에셋을 한 번 비활성→활성하거나 에디터 재포커스로 강제 리빌드.
- **원격 전환 후 첫 실행이 느림**: 정상. Addressables가 카탈로그·번들을 처음 fetch한 뒤엔 캐시.

---

## 5. 추후 자동화 여지 (선택)

- 새 `CardSpriteSet`을 우클릭해 자동 마킹 + 카탈로그 등록까지 처리하는 Editor MenuItem (`Solitaire > Skin > Register Selected CardSpriteSet`). 현재 수작업이라 실수 여지 있음.
- 빌드 파이프라인 (CI)에서 Addressables 빌드 → S3/GCS 업로드 단계 추가. 원격 전환 결정 시 같이 도입.
- 둘 다 필요해지면 별도 스펙으로 분리.
