# Board Mode Foundation + Pyramid — Design Spec

- **Date**: 2026-06-07
- **Status**: Draft (브레인스토밍 합의 완료, 사용자 spec 리뷰 대기)
- **Scope**: 첫 슬라이스 = **공통 보드 기반(F) + Pyramid**. 마작(B)·TriPeaks는 후속 슬라이스.
- **Topic**: 신규 게임 모드 — "마작 솔리테어를 트럼프 카드로". 그 정체성인 *층층 보드 + 자유 타일 매칭*을 카드 자산 위에서 구현하기 위한 공통 보드 기반을 먼저 만든다.

---

## 1. 배경 / 동기

기존 게임 모드(Klondike, Easthaven, 그리고 enum 스텁 Spider/Pyramid/TriPeaks)는 **전부 단일 카드 슬라이스를 공유**한다: 하나의 `SolitaireGameService : IGameService`를 `IDealRule`로만 분기하고, `TableState`(Stock/Waste/Foundation/Tableau + `PlayingCard`), 단일 `IngameScene/Presenter/Component`, 파일-앵커 + 선형 오프셋 + **드래그-투-파일** 렌더링(`UICardsController`)을 쓴다.

문제: `PileId = (PileType, Index)`에는 **좌표·레이어·"덮임(covered)" 개념이 전혀 없다.** 그래서

- "마작 솔리테어(카드판)" = 층층 보드 + 자유 타일 페어 매칭,
- 이미 계획됐다 **보류된 Pyramid/TriPeaks**

가 셋 다 같은 결손에 막혀 있었다. 세 게임 모두 **좌표/레이어 보드 + 자유 판정 + 탭-투-선택**을 요구한다.

→ 이 공통 기반(이하 **F**)을 한 번 만들면 Pyramid·마작·TriPeaks가 각자 "레이아웃 데이터 + 매칭 규칙"만 얹어 완성된다. F가 비용의 ~65%를 차지하고 셋이 공유한다.

### 로드맵 (우선순위 확정)
1. **F + Pyramid** ← 이 spec. 가장 단순한 Pyramid로 F를 끝까지 검증.
2. 🀄 **마작(B)** — 검증된 F 위에 z레이어·멀티덱·풀이가능딜 추가.
3. **TriPeaks** — F 위 저렴한 추가(시퀀스 입력 변형).

---

## 2. 목표 / 비목표

### 목표 (이 슬라이스)
- 카드 자산을 재사용하는 **층층 보드 게임 공통 기반(F)** 구축.
- **Pyramid** 한 종을 Standard 파리티로 플레이 가능하게: 딜 → 탭 매칭 → 승/패, **Undo · Hint · Stats(점수/시간/이동) · Snapshot(저장·이어하기) · Stuck 패널**.
- 기존 인게임 UI를 **prefab variant로 재사용**(UI 두 번 만들지 않음, 일관성 유지).
- F는 후속 마작(z레이어)·TriPeaks(시퀀스 입력)를 **재작업 없이** 수용하도록 설계.

### 비목표 (후속 슬라이스로 보류)
- 마작(B), TriPeaks 구현 자체.
- Daily 챌린지 / 공유 코드(시드) — Pyramid에는 다음 슬라이스에서.
- 비주얼 레이아웃 에디터(그리드 저작 툴) — 마작 144칸 저작이 필요해지면.
- 풀이가능 딜 보장(솔버) — Pyramid는 **클래식(언위너블 허용)**.
- 리모트 에셋(addressable) — Pyramid 레이아웃은 코드 생성이라 불필요.

### 확정된 설계 결정 (브레인스토밍 합의)
| 항목 | 결정 |
|---|---|
| 방향 | 마작-with-카드(B)가 헤드라인. 첫 슬라이스는 F+Pyramid. |
| 아키텍처 | **병렬 보드 스택**(카드 코드 무손상, 인프라 재사용). |
| 기능 범위 | **Standard 파리티**(코어+Undo+Hint+Stats+Snapshot+Stuck). Daily/코드 보류. |
| Pyramid 딜 | **클래식**(언위너블 허용, 솔버 없음). |
| 레이아웃 저작 | **데이터+코드생성**, 비주얼 에디터 보류. |
| 입력 | **탭-투-선택**(드래그 아님). |
| UI 재사용 | **공유 셸 컴포넌트**(`IngameShellView` 추출) + **prefab variant**. |

---

## 3. 아키텍처 — 병렬 보드 스택

성숙한 카드 코드(`SolitaireGameService`/`TableState`/`UICardsController`/`IngamePresenter`)는 **손대지 않는다.** 보드 게임용 수직 슬라이스를 새로 두되, 인프라는 재사용한다.

| 레이어 | 재사용 (그대로) | 신규 (보드 전용) |
|---|---|---|
| Model | `PlayingCard`/`Rank`/`Suit`, `SessionStats`(데이터), `GameType` | `Model.Board`: `CellId`, `BoardCell`, `BoardLayout`, `BoardState`, 자유판정 규칙, `BoardSnapshot` DTO |
| Data | `CardSpriteSet`/Skin | `BoardLayoutAsset`(마작용; Pyramid는 코드 생성), `GameVariant`(Pyramid), Pyramid `BoardScoreRuleAsset` |
| Gateway | `IGameSnapshotRepository`(파일 저장소, `SnapshotKey`) | — (직렬화 모델만 `BoardSnapshot`) |
| Service | `RouteService`, `LifetimeStatsService`, `AudioService`, `SkinService`, `SessionStatsService`(시간/이동/힌트) | `IBoardGameService`, `IBoardMatchRule`(+factory), 보드 스코어링, 보드 auto-save |
| Component | `UICard`(+스킨), HUD/패널/Toast **프리팹** | `UIBoardController`(좌표·z·탭선택·free 강조·제거 애니); `IngameShellView`(기존 UI 추출·양 씬 공유, §7) |
| Scene | Win/Stuck/Pause/Setting/Stats/HUD **프리팹**, Lobby 진입·Route | `BoardScene`/`BoardPresenter`/`BoardComponent` |

### 기각한 대안
- **IngamePresenter 확장**: 이미 큰 카드/드래그 전용 Presenter에 보드 패러다임을 끼우면 거대 결합 Presenter(단일책임 위반·회귀 위험).
- **TableState/IGameService 일반화**: 성숙·동작 중인 카드 코드 대수술. 파일+드래그 vs 좌표+탭은 본질이 달라 통합 추상화는 과설계·고위험(YAGNI).

### Assembly 배치 (기존 DAG 유지)
- `Model.Board` → `Model`만 의존.
- 보드 서비스 → `Service` 어셈블리 내(`Service.BoardGameService`), 기존 `Service`가 `Model`/`Gateway`/`Data` 의존하므로 사이클 없음.
- `UIBoardController`/`IngameShellView` → `Component` 어셈블리.
- `BoardScene` → `Scene` 어셈블리(기존 Ingame과 동일 의존).
- 새 어셈블리는 추가하지 않는다(기존 레이어에 네임스페이스로 편입). 필요시에만 분리.

---

## 4. F 도메인 모델 (`Model.Board`)

CLAUDE.md 준수: **immutable + `IEquatable<T>` + `GetHashCode`**, Model에 **UnityEngine 미참조**, 상태 변경은 **With-패턴**, 누적 상태는 Model이 아닌 Service.

### 4.1 핵심 개념 — "자유(free)" 술어 (cover 기반)
보드는 "셀에 카드가 놓인 것"의 집합. 셀의 선택 가능 여부 = **자유**.

```
IsFree(cell) = (cell의 모든 cover-blocker가 제거됨)
```

- **Pyramid/TriPeaks**: cover-blocker = 위로 겹치는 카드(Pyramid는 대각 아래 두 장). 이 슬라이스의 자유 판정은 cover만으로 결정된다.
- **마작 (후속, YAGNI 보류)**: 마작은 cover에 더해 "좌/우 한쪽 열림" side 룰이 필요하다. 그 side 룰과 좌표/레이어는 **마작 슬라이스에서** 추가한다 — 지금 base Model에 미리 넣지 않는다(과도 개발 방지). 마작 커버/사이드는 좌표에서 기하로 유도하는 형태가 될 수 있어, 표현 자체가 달라질 수 있다.

cover 그래프는 **레이아웃이 정적으로 보유**한다.

### 4.2 타입
- **`CellId`** — readonly struct, `IEquatable`. 레이아웃 내 위치의 안정적 식별자(정수 인덱스 권장).
- **`BoardCell`** — immutable **논리 토폴로지**(cover 그래프만; 렌더 좌표·레이어·마작 side 룰은 보유하지 않음):
  - `CellId Id`,
  - `IReadOnlyList<CellId> CoverBlockers` (모두 제거돼야 자유).
  - **위치는 Model이 아니라 View가 소유**: Pyramid/TriPeaks는 프리팹 고정 앵커(`CellId` 순서). 마작의 좌표·레이어·side 룰은 base Model이 아닌 **마작 데이터/뷰**에 둔다(마작 슬라이스에서 결정 — 지금은 미리 만들지 않음).
- **`BoardLayout`** — immutable 토폴로지 설정:
  - `GameType GameType`, `int Variant`, `IReadOnlyList<BoardCell> Cells`,
  - 빠른 조회를 위한 사전계산 룩업(예: `IReadOnlyDictionary<CellId, BoardCell>`).
  - **Pyramid는 `PyramidLayoutFactory`가 코드 생성**, 마작은 `BoardLayoutAsset`에서 로드(후속).
  - 동치성은 `(GameType, Variant)` 키 기준(대형 config라 구조적 비교 불필요).
- **`BoardState`** — immutable 런타임 상태:
  - 남아있는 셀→카드 매핑(`IReadOnlyDictionary<CellId, PlayingCard>` 또는 동등한 immutable 표현),
  - (Pyramid용) 경량 stock/waste(immutable 카드 리스트; `PileState` 재사용 가능성은 구현 시 판단),
  - With-패턴: `WithRemoved(params CellId[])`, `WithStockDrawn()`,
  - `IEquatable` + `GetHashCode`.
- **`IBoardMatchRule`** — 게임별 매칭 전략(factory 주입, 기존 `IDealRule` 패턴):
  - 선택 크기(페어/단일)와 유효성, 제거 대상 산출을 캡슐화.
  - Pyramid = 두 장 합13(또는 K 단독), 마작 = 자유 두 장 동일 랭크, TriPeaks = 웨이스트±1 단일.

### 4.3 순수 규칙 함수 (`BoardRules`, 상태 없음)
- `bool IsFree(BoardLayout, BoardState, CellId)` — 위 통일 술어.
- `IEnumerable<CellId> FreeCells(BoardLayout, BoardState)` — 자유 셀 열거(Hint/Stuck/렌더 공통).
- 매칭 유효성은 `IBoardMatchRule`에 위임.

---

## 5. F 서비스

### 5.1 `IBoardGameService` (기존 `IGameService` 형태 미러링)
```
Observable<BoardState> OnBoardStateChanged
BoardState CurrentState        // Initialize 전 null
BoardLayout Layout
int? CurrentSeed

void Initialize(BoardLayout layout, IBoardMatchRule rule, int? seed = null)  // 셔플은 생성자 주입 IShuffleStrategy
void SelectCell(CellId id)     // 탭 흐름: 잠정 선택 누적 → 유효 매칭 시 제거+publish
void DrawFromStock()           // Pyramid: stock→waste 1장 공개 (클래식 패스 정책)
bool IsWon(BoardState state)   // 보드 클리어
bool HasAnyMove(BoardState state)  // Stuck 판정: 자유셀+waste/stock로 가능한 수 존재?

bool CanUndo { get; }
void Undo()
IReadOnlyCollection<BoardState> UndoHistory
void Restore(BoardLayout, IBoardMatchRule, int seed, BoardState, IReadOnlyList<BoardState> undoHistory)
```

- **선택 누적자**(잠정 선택된 `CellId`들)는 **서비스의 private 필드** — Model에 두지 않는다(CLAUDE.md).
- `SelectCell`: 자유 셀만 받음. 규칙상 매칭 완성 → 제거→새 `BoardState` publish→스코어/이동 기록. 무효 → 선택 해제/교체.
- 셔플은 기존 `IShuffleStrategy`를 **생성자로 주입**. 서비스가 `Initialize`에서 셔플된 덱을 셀(`layout.Count`개)에 채우고 나머지를 stock으로 — 별도 `IBoardDealer` 타입은 없음.

### 5.2 보드 스코어링
`IScoreRule`/`ScoredMoveInfo`/`MoveType`은 파일 전용(Waste/Foundation/Tableau, StockRecycle)이라 **그대로 못 쓴다.** 보드용 경량 스코어 룰을 신규:
- `IBoardScoreRule`(인터페이스) — Pyramid는 POCO `PyramidScoreRule`(카드당 점수 + 클리어 보너스). Inspector 튜닝용 ScriptableObject 래퍼는 필요 시 2c에서.
- 시간/이동/힌트/승패 트래킹은 `SessionStatsService` 재사용(보드 서비스가 점수 델타를 명시적으로 전달하는 진입점 추가; 파일타입 추론 경로는 우회).

### 5.3 보드 스냅샷
`GameSnapshot`은 카드 전용(`TableStateDto`/`DrawCount`/`SessionStatsDto`)이라 보드용 신규:
- `BoardSnapshot` DTO(MemoryPack): 레이아웃 키(`GameType`,`Variant`) + 남은 셀 카드 + stock/waste + UndoHistory + `SessionStatsDto` + savedAt.
- **저장소는 동일 `IGameSnapshotRepository` 재사용**(`SnapshotKey` 키잉). 보드 auto-save 서비스(디바운스)는 `GameSnapshotService`의 보드판.

---

## 6. F 렌더링 — `UIBoardController` (`Component.Board`)

`UICardsController`의 보드판. **드래그 없음**. **위치는 Model이 아니라 View가 공급** — Pyramid/TriPeaks는 프리팹 고정 앵커(`CellId.Value → anchor[Value]`, 기존 `UIPlaceHolder` 패턴), 마작은 좌표 데이터. 렌더러는 위치 출처에 무관(앵커/좌표를 입력으로 받음).
- 각 `CellId`를 그 위치(고정 앵커 또는 좌표)에 `UICard` 인스턴스로 배치, **레이어별 z정렬**(siblingIndex/Canvas sorting; 마작 한정).
- `UICard` 프리팹 + **스킨 자동 적용**(`ApplySpriteSet` 재사용 → 스킨 기능이 보드에도 공짜로 적용).
- **탭-투-선택**: `UICard.OnPointerClickEvent`(기존) 구독 → `CellId`를 Presenter/Service로 전달. **자유 셀만** 상호작용/강조.
- 상태 렌더: `BoardState` 변경마다 자유=부각(살짝 올림/아웃라인), 잠김=디밍. 매칭 제거 = fade+shrink 애니(드래그/스택/드롭 로직 불필요 → 입력 측이 카드 컨트롤러보다 단순).
- 반응형 배치: 레이아웃 좌표 → 화면 맞춤 스케일(기존 `LobbyResponsiveLayout`/`HandednessLayout` 패턴 참고).

---

## 7. UI 재사용 — 공유 셸 + Prefab Variant

현재 `SettingPanel`/`CodeInputPanel`/`DailyResultsPanel`/`AchievementPanel`만 프리팹이고 **HUD·Stuck·Pause·Win·Stats·Toast는 `Ingame.unity`에 직접 박혀 있다.** 그대로면 Board 씬에서 재제작이 필요 → 회피.

### 7.1 `IngameShellView` 추출 (공유 셸 컴포넌트)
게임 무관 UI와 그 옵저버블을 한 컴포넌트로 추출:
- 보유: HUD(score/time/moves), Win/Stuck/Pause/Setting/Stats 패널, Toast, 입력 블로커, 하단 바.
- 노출 옵저버블: `OnUndo`, `OnHint`, `OnPause`, `OnNewGame`, `OnStats`, `OnApplicationPause`, 패널별 버튼 이벤트, `Show*/Hide*` API.
- `IngameComponent`(카드)는 이 셸에 **위임**하도록 리팩터(카드 전용 = 플레이영역/드래그 옵저버블만 남김). **347 EditMode 테스트가 안전망.**
- `BoardComponent`도 동일 `IngameShellView`를 참조 → **UI·와이어링 중복 0.**

### 7.2 Base 프리팹 + Variant + PlayArea 슬롯
- 인게임 UI를 **base 프리팹**(`IngameShell.prefab`)으로 추출: Canvas + 셸 패널들 + 빈 **`PlayArea`** 컨테이너 + `IngameShellView`.
- 기존 Ingame 씬: base 프리팹 인스턴스의 `PlayArea`에 `UICardsController` 배치(카드 전용 컴포넌트는 씬 레벨에 부착).
- **Board 씬: base의 prefab variant** — `PlayArea`에 `UIBoardController` 배치. 셸은 그대로 상속(오버라이드 최소).
- 게임별 루트 컴포넌트(`IngameComponent`/`BoardComponent`)·Presenter는 씬 레벨에서 부착, 셸 인스턴스를 참조. (CLAUDE.md Prefab Variant 우선; 기존 `UIPlaceHolder`/`Lobby Grid Item` 패턴과 동일.)

---

## 8. Pyramid 세부

- **레이아웃(논리)**: 28장 7행 피라미드, `PyramidLayoutFactory` **코드 생성**. cover-blocker = 대각 아래 두 장, side-blocker 없음. 렌더 위치는 프리팹 고정 앵커(삼각 배치)로 별도 — 팩토리는 위치를 만들지 않음.
- **딜**: 단일 52장 셔플(기존 `IShuffleStrategy`+seed) → 28장 피라미드, 24장 Stock, Waste 비움.
- **매칭 규칙**(`PyramidMatchRule : IBoardMatchRule`): 두 장 **합 13**(A=1, J=11, Q=12, K=13), **K 단독** 제거. 대상 = 자유 피라미드 카드 + Waste-top.
- **Stock/Waste**: Stock 탭 → top을 Waste로 공개(Waste-top 선택 가능). **클래식 = 1패스, 리사이클 없음**(variant 파라미터로 분리 → 후속 조정 용이).
- **승리**: 피라미드 28장 클리어(잔여 stock/waste 무관).
- **Stuck**: 자유쌍 없음 + Stock 소진 → Stuck 패널(Undo/Restart/New Game 재사용). 클래식이라 언위너블 허용.
- **스코어**: 보드용 `IBoardScoreRule`(페어당 점수 + 클리어 보너스 등). 시간/이동/힌트는 `SessionStatsService` 재사용.
- **Snapshot**: `BoardSnapshot` → 동일 `IGameSnapshotRepository`로 저장/재개.
- **진입/셋업**: Pyramid `GameVariant`(GameType.Pyramid, v1) → Lobby 타일 → **`BoardScene` 라우팅**(query `GameType=Pyramid`). 새 씬을 **Build Settings + App 씬로더에 등록**(에디터 셋업 태스크).

---

## 9. 후속 호환성 제약 (F가 지금 지켜야 할 것)

- **마작(z레이어·side룰)**: 레이어·cover/side·좌표는 base Model에 **미리 넣지 않는다** — 마작 슬라이스에서 추가(아마 좌표에서 기하 유도). 현재 base는 **cover-only 2D**. 멀티덱 딜·풀이가능딜(기존 `ReversePlayShuffleStrategy`/`KlondikeSolver` 패턴 차용)·stuck시 셔플도 마작 슬라이스에서.
- **TriPeaks(시퀀스 입력)**: `IBoardMatchRule`이 **단일 선택(웨이스트로 이동)** 도 표현 가능해야 함(페어 전용으로 좁히지 말 것). 선택 크기와 "대상=waste-top" 개념을 규칙이 정의.
- **레이아웃 소스 이원화**: F는 "코드 생성 레이아웃"과 "에셋 로드 레이아웃" 둘 다 받도록(`BoardLayout`은 출처 불문 동일 타입).

---

## 10. 테스트 전략 (EditMode, NUnit)

순수 로직 위주(기존 테스트 패턴):
- `BoardRulesTests` — `IsFree`: cover 기반 자유판정(Pyramid 덮임/해제), 미지 CellId 무시. (마작 side룰은 마작 슬라이스 테스트로.)
- `PyramidLayoutFactoryTests` — 28셀/7행/blocker 그래프 정확성.
- `PyramidMatchRuleTests` — 합13/K단독/무효 조합, waste-top 포함.
- `BoardGameServiceTests` — 딜(시드 결정성), 선택→매칭→제거, Undo, `IsWon`/`HasAnyMove`(Stuck), Restore 라운드트립.
- `BoardSnapshotTests` — DTO 직렬화 라운드트립.
- 목표: 기존 347 그린 유지 + 보드 신규 스위트 추가.

---

## 11. 리스크 / 열린 질문

- **IngameComponent 리팩터 회귀**: 셸 추출 시 카드 게임 UI 와이어링이 깨질 수 있음 → 347 테스트 + 수동 플레이 검증으로 가드. 가장 큰 단일 리스크.
- **반응형 보드 배치**: 좌표 레이아웃의 화면 맞춤(다양한 종횡비)·핸드니스 — 기존 패턴 재사용으로 완화하나 튜닝 필요.
- **Stock 패스 정책**: 클래식 1패스로 시작하되 variant 파라미터로 노출(추후 3패스/리사이클 추가 용이).
- **보드 스코어 룰 형태**: 점수 공식은 플레이 감 보고 조정(자산화로 유연).
- **씬 등록 수작업**: Build Settings/씬로더 등록은 에디터 자동화 또는 수동.

---

## 12. 산출물 (이 슬라이스)

신규: `Model.Board`(CellId/BoardCell/BoardLayout/BoardState/BoardRules/IBoardMatchRule + BoardSnapshot DTO), `IBoardGameService`+impl, `IBoardMatchRule` factory + `PyramidMatchRule`, `PyramidLayoutFactory`, `IBoardScoreRule`+자산, 보드 auto-save 서비스, `UIBoardController`, `IngameShellView`(추출), `BoardComponent`/`BoardPresenter`/`BoardScene`, `IngameShell.prefab`(+Board variant), Pyramid `GameVariant`/스코어 자산, Lobby 진입·Route·BuildSettings, EditMode 테스트.

변경: `IngameComponent`/`IngameScene`(셸 위임 리팩터, 동작 불변), Lobby 타일/라우팅, `GameType` 이미 `Pyramid` 보유(추가 불필요).
