# Spider Solitaire — 설계 스펙

## 결정 사항 (사용자 확정)

| 항목 | 결정 |
|------|------|
| Variant 범위 | **1-suit만 출시** (♠×8벌, 104장). 2/4-suit는 데이터(asset)만으로 추가 가능하게 구조 준비 |
| Winnability | **랜덤 딜** — Spider 솔버/resolve-then-share는 범위 외 (GameCode는 resolved seed 저장 방식이라 후장착 호환) |
| 스톡 딜 규칙 | **클래식: 빈 컬럼 존재 시 딜 금지** (거부 피드백) |
| 점수 모델 | **기존 IScoreRule 틀 재사용** — Spider용 ScoreRuleAsset 값 세팅, 표준 Spider 점수(500−무브) 미도입 |
| 테이블 레이아웃 | **레이아웃 루트 스와프** — Table 아래 KlondikeLayout/SpiderLayout 자식 루트, UICardsController가 활성 세트 교체 |
| 규칙 엔진 | **A안: 플래그 확장 단일 엔진** — IDealRule 멤버 추가, SolitaireCardService/GameService가 플래그 분기 (Easthaven 선례) |

## Spider 규칙 요약 (1-suit)

- 104장 = ♠ A–K × 8벌. 테이블로 10컬럼: 앞 4컬럼 6장, 뒤 6컬럼 5장 (54장), 각 컬럼 톱 1장만 페이스업. 스톡 50장.
- 픽업: **동일 수트 + 연속 하강** 런만 집을 수 있음 (1-suit에선 사실상 연속 하강).
- 드롭: 타깃 톱카드보다 **랭크 1 낮으면 수트 무관** (빈 컬럼은 아무 카드/런).
- 스톡 탭: 10컬럼에 1장씩 페이스업 분배. **빈 컬럼 있으면 금지.** 리사이클 없음, waste 없음.
- K→A 동일 수트 13장이 테이블로에 완성되면 **자동으로 파운데이션에 수거**. 파운데이션 직접 드롭은 불가.
- 승리: 파운데이션 8칸 × 13장.

## 1. 도메인 모델 (Model.Game)

`IDealRule` 신규 멤버 — 기존 asset이 기본값으로 현행 동작 유지:

```csharp
int DeckCount { get; }            // 52장 덱 수. 기본 1, Spider 2
int SuitCount { get; }            // 사용 수트 수(1/2/4). 기본 4. 덱을 첫 N개 수트로 결정론 리맵
TableauRunRule RunRule { get; }   // enum { AlternatingColor, SameSuit } — 다중 카드 픽업 적법성
TableauDropRule DropRule { get; } // enum { AlternatingColor, AnySuit } — 드롭 타깃 매칭
bool StockDealRequiresNoEmptyColumn { get; }
bool AutoCollectCompletedRuns { get; }
```

- enum 2종은 `Model.Game`에 신규 파일.
- `PlayingCard` 무변경. 중복 카드(동일 Rank+Suit 2장+)의 value-equality 안전 근거:
  UI 바인딩은 UICard 참조 키(`UICardsController.cardBindingMap`), 카드 탐색은 `PileId+index`
  (`FindCard`), 솔버는 Spider 비대상(`KlondikeFastSolver`의 rule-shape 가드가 거부).
- `DealRuleAsset`에 직렬화 필드 6종 추가 (기본값 = 위 기본값).

## 2. 덱 구성 / 딜

- `DeckFactory.CreateShuffled(int seed, IDealRule rule)` 오버로드 신설.
  `CreateOrdered(rule)` = DeckCount×52장 생성 후 각 카드 수트를 첫 SuitCount개 수트로
  결정론 리맵 (랭크 보존). 기존 `CreateShuffled(int seed)`는 1덱·4수트로 시그니처/동작 보존
  — 기존 호출처(Klondike/Easthaven/일일/솔버 테스트) 무변경.
- `DealBuilder`는 이미 rule-driven (TableauCount/InitialCardCounts/FoundationCount) — 무변경.
- 신규 asset:
  - `Assets/ScriptableObjects/DealRule/Spider 1Suit.asset` — tableauCount 10,
    foundationCount 8, initialCardCounts [6,6,6,6,5,5,5,5,5,5], initialFaceUpPerColumn 1,
    hasWaste false, canRecycleStock false, stockDealsToTableau true, stockDrawCount 0,
    onlyKingOnEmptyTableau false, deckCount 2, suitCount 1, runRule SameSuit,
    dropRule AnySuit, stockDealRequiresNoEmptyColumn true, autoCollectCompletedRuns true
  - `Assets/ScriptableObjects/GameVariants/Spider_1Suit.asset` — GameType.Spider, VariantId 1

## 3. 이동 규칙 (SolitaireCardService + 드래그 거부)

- 픽업(다중 카드) 검증: `RunRule` 스위치 — SameSuit면 동일 수트+연속 하강, AlternatingColor면 현행.
- 드롭 검증: `DropRule` 스위치 — AnySuit면 랭크 하강만, AlternatingColor면 현행.
- 빈 테이블로: 기존 `OnlyKingOnEmptyTableau` 플래그 그대로 (Spider=false).
- 파운데이션 타깃 무브: `AutoCollectCompletedRuns=true`면 **거부** (수거 전용 — 직접 드롭 금지).
- `UICardsController.IsValidTableauPickupSequence`의 클라이언트 측 색-교대 하드와이어 제거,
  CardService 검증 위임으로 단일화 (root-cause: 규칙 이중 정의 제거).

## 4. 게임 서비스 (SolitaireGameService)

- `bool CanDealStock` 쿼리 추가: `StockDealRequiresNoEmptyColumn=true`이고 빈 테이블로가 있으면
  false. 프리젠터 `HandleStockDraw`가 체크 후 거부 피드백 (기존 recycle 거부 패턴과 동일:
  NoHint 사운드 + 셰이크 없음 또는 MoveRejected — 구현 시 기존 거부 UX 따름).
- 런 자동 수거: `ExecuteMove`/`DealStockToTableaus` 적용 직후, 각 테이블로 톱에서
  K→A 동일 수트 13장 연속(페이스업) 검사 → 해당 13장 제거 → 첫 빈 파운데이션에 적재.
  여러 런 동시 완성 가능(스톡 딜) — 전부 수거.
  **무브+수거 = 단일 undo 히스토리 엔트리** (수거는 무브의 원자적 결과; undo 시 수거 전 상태가
  아니라 무브 전 상태로 복원).
- `IsWon` 무변경: 모든 파운데이션 카드 수 == PerSuitCardCount (8×13 충족).

## 5. 힌트 / 오토컴플리트 (HintService / MoveEnumerator)

- `MoveEnumerator`: RunRule/DropRule 인지 무브 열거. 우선순위 가중은 기존 체계 안에서
  동일-수트 연결 무브를 상위로 (세부 가중치는 구현 재량).
- 스톡 딜 힌트: `CanDealStock=false`면 StockDraw 힌트 제외.
- `CanAutoComplete`: `AutoCollectCompletedRuns=true`면 항상 false (자동 수거가 종반을 대체).

## 6. 점수 / 통계 / 스냅샷 / 코드

- `Assets/ScriptableObjects/ScoreRule/Spider.asset` (ScoreRuleAsset) 신설 + `IngameScene`
  scoreRuleMap에 `GameType.Spider` 등록 (현재 미등록 시 `ScoreRuleFactory.Create` throw).
  값: TableauReveal(뒤집기), TableauToFoundation(런 완성 1회분) 중심 — 수치는 asset에서 튜닝.
- 런 수거 기록: 프리젠터 `UpdateTableState`가 파운데이션 증가 diff를 감지해
  `RecordMove(MoveType.CardMove, Tableau→Foundation)` 1회 기록 (기존 기록 경로와 일관).
- 스냅샷: `GameSnapshotConverter`는 파일 수 가변 직렬화 — Spider 10T/8F 그대로 통과 (검증 항목).
- GameCode: 프리픽스 "SPI" 자동 생성, 시드 재현은 `CreateShuffled(seed, rule)` 결정론으로 보장.
- `IngamePresenter.resolveSolvableSeed` 게이트는 Klondike/Easthaven 한정 유지 — Spider는
  랜덤 시드 직행 (솔버 범위 외).

## 7. UI / 씬

- `Table` 아래 자식 루트 2개: `KlondikeLayout`(기존 placeholder 7T+4F+1S+1W 이동),
  `SpiderLayout`(10T+8F+1S 신설). 직렬화 `LayoutSet`(루트 GO + placeholder 리스트) 구조로
  `UICardsController`에 등록, init 시 GameType으로 활성 레이아웃 스와프
  (비활성 루트는 SetActive(false)).
- 스톡 딜 애니메이션: `DetectMoveAnimation`의 다중 타깃 분기(Easthaven 10→7 확장) 재사용.
- 카드 스폰: 동적 스폰/풀 — 104장 동작 확인 항목.
- 로비: `GameTileView`에 Spider GameVariant asset 연결, `spider.png` 스프라이트 (이미 존재).
- 씬/프리팹 편집은 Phase B와 동일하게 에디터 스크립트로 수행.

## 8. 테스트 (EditMode)

- DeckFactoryTests 확장: rule 오버로드 — 104장, ♠13×8 구성, 시드 결정론, 기존 1덱 경로 회귀.
- SolitaireCardServiceTests 확장: SameSuit 픽업 허용/혼합 수트 거부, AnySuit 드롭,
  빈 컬럼 드롭, 파운데이션 직접 드롭 거부.
- SolitaireGameServiceTests(신규): CanDealStock 빈 컬럼 가드, 런 자동 수거(단일/복수),
  무브+수거 undo 원자성, IsWon 8×13.
- DealBuilderTests 확장: Spider 레이아웃 (54/50 분할, 페이스업 1).
- HintServiceTests 확장: Spider 무브 열거, 스톡 힌트 가드, CanAutoComplete=false.

## 9. 범위 외

- 2/4-suit asset 추가 (구조만 준비)
- Spider 솔버 / winnability 파이프라인 / 프리페치
- Spider daily
- 표준 Spider 점수 모델(500−무브)
- FreeCell (후속 설계 — 본 작업의 LayoutSet/규칙 플래그 구조를 재사용 예정)

## 리스크 / 완화

| 리스크 | 완화 |
|--------|------|
| 단일 엔진 분기 누적 | 플래그가 3겹 이상 중첩되는 메서드가 나오면 C안(IMoveRuleSet 전략 주입)으로 해당 부분만 추출 |
| 수거 로직이 OnTableStateChanged 구독자(애니메이션 diff)에 복수 카드 이동을 노출 | 수거 후 상태를 단일 방출로 — 13장 이동은 diff 렌더가 처리 (Easthaven 다중 타깃 경로 검증) |
| 104장 렌더/스폰 성능 | 스모크에서 확인; 문제 시 페이스다운 카드 스프라이트 간소화 |
