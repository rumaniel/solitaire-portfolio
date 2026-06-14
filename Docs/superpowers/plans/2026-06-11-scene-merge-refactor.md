# Scene Merge Refactor — BoardScene을 Ingame으로 통합

## Context / Motivation

Ingame.unity(카드 게임: Klondike/Easthaven)와 BoardScene.unity(보드 게임: Pyramid/TriPeaks)는
**같은 IngameShell 프리팹**을 인스턴스화하면서 씬·프리젠터·라우트는 이중화되어 있다.
이 이중화가 만든 실제 비용:

- **DealLoadingOverlay 버그** (2026-06-11): 오버레이를 Ingame 씬 인스턴스에만 와이어링 →
  BoardScene은 조용히 미표시. 프리팹 소유로 핫픽스했지만(`ab14310`), "shell에 무언가 추가하면
  두 곳을 챙겨야 한다"는 구조적 함정은 남아 있음.
- **프리젠터 중복**: IngamePresenter와 BoardPresenter가 shell 이벤트 와이어링(pause/win/new-game/
  restart/share), seed resolve + prefetch + 150ms 오버레이 로직(`ResolveWithLoadingAsync` /
  `ResolveBoardSeedAsync`)을 각각 복제.
- **라우트 분기**: Lobby가 GameType에 따라 "Ingame" / Board 라우트로 갈라짐. GameCode 재입장,
  새 게임 내비게이션도 두 갈래.

목표: **한 씬(Ingame.unity)이 모든 게임 타입을 호스팅**하고, shell·공통 플로우는 단일 지점에서
관리한다. 단, 한 번에 합치지 않고 코드 레벨 수렴(Phase A) → 씬 레벨 통합(Phase B) 순서로
리스크를 분리한다.

**선행 조건**: PR #105 (winnability 파이프라인) 머지 완료 후 별도 브랜치/PR로 진행.
이 리팩터를 #105에 섞지 않는다.

---

## Phase A — Shell 공통 로직 추출 (코드 레벨, 씬 변경 없음) · PR 1개

> **상태: 완료 (PR #107, 2026-06-11).** `Scene.Shared.ShellFlowController` + `ShellFlowCallbacks`
> (콜백 7종 fail-fast 검증 포함). 의도된 동작 변화 3건: Ingame pause 시 snapshot flush,
> pause-new-game 직접 호출, play-with-code 크로스 씬 라우팅(잠재 버그 수정).
> LobbyPresenter는 `Scene.Shared` 네임스페이스가 루트 `Shared`를 가려 2줄 수정.

목적: 씬을 건드리기 전에 두 프리젠터의 중복을 한 곳으로 수렴. Phase B가 실패해도
이 단계 단독으로 가치가 있다.

### A-1. ShellPresenterBase (또는 composition helper) 도입
`Assets/Scripts/Scene/Shared/` (asmdef 영향 확인 — Scene assembly 내부면 무리 없음):

- 공통 shell 이벤트 와이어링: OnPause/OnPauseToGame/OnPauseNewGame/OnPauseRestart/
  OnPauseLobby/OnWin*/OnApplicationPause + 오디오 재생까지 — 현재 양쪽 프리젠터의
  Start()에 복제된 블록.
- `ResolveWithLoadingAsync(Func<int> resolver, CancellationToken)` — 이미 Func 기반으로
  일반화됨. IngamePresenter에서 베이스로 이동, BoardPresenter의 `ResolveBoardSeedAsync` 제거.
- 검증 포인트: 두 프리젠터의 wiring 블록을 diff해서 **실제로 동일한 것만** 추출.
  다른 부분(예: Ingame의 hint/undo, Board의 cell tap)은 각자 유지.

### A-2. 테스트/검증
- `uloop compile` 0 errors, 전체 EditMode 스위트 PASS (530+).
- 에디터 스모크: 카드/보드 각 1게임 — pause/restart/new-game/오버레이 동작 동일.

추정 규모: C# 4~6파일, 씬/프리팹 변경 없음.

---

## Phase B — 씬 통합 (씬 + 라우트 레벨) · PR 1개

> **사전 인벤토리 결과 (2026-06-11, 읽기 전용 조사):** 아래 B-1~B-3의 실측 데이터.
> 원안 대비 단순화: 보드 프리팹화는 이미 되어 있어 "프리팹 신규 2개" 작업이 사라짐.

### B-1. 씬 구조
Ingame.unity 안에 게임 보드 루트 3종을 형제로 배치:

```
IngameShell (prefab instance — 공통 HUD/패널/오버레이)
├─ Table             (기존 Ingame 카드 테이블: UICardsController + CardMoveAnimator)
├─ PyramidBoard      (prefab instance — BoardScene과 동일 소스)
└─ TriPeaksBoard     (prefab instance — BoardScene과 동일 소스)
```

- **이미 프리팹임**: `Assets/Prefabs/Board/PyramidBoard.prefab`(GUID f6b8bbc4…) /
  `TriPeaksBoard.prefab`(GUID 8f735e83…)이 존재하고, BoardScene.unity는 이 둘을
  IngameShell PrefabInstance의 `m_AddedGameObjects`로 인스턴스 중. Ingame.unity에서도
  같은 패턴으로 IngameShell RectTransform 아래 `Table`의 형제로 인스턴스만 추가하면 됨.
- 시작 시 보드 2종 비활성(`TriPeaksBoard`는 BoardScene에서도 `m_IsActive: 0`),
  `IngameQuery.GameType`으로 해당 루트만 SetActive. 카드 게임 시 `Table` 활성.
- 메모리: 비활성 계층 공존 비용은 카드 스프라이트 공유로 미미. 측정 후 문제 시 lazy load.

### B-2. DI 통합 (IngameScene.Configure)
실측 충돌 목록 (BoardScene.cs ↔ IngameScene.cs Configure):

| 등록 | BoardScene | IngameScene | 처리 |
|------|-----------|-------------|------|
| `SessionStatsService` as `ISessionStatsService` | :46 | :71 | 1개만 유지 |
| `RegisterComponent(IngameShellView)` | :31 | :43 | 1개만 유지 |
| Audio BuildCallback (`AddDatabase`) | :35–39 | :46–51 | scene audio DB 2개를 합치거나 둘 다 Add |
| `FisherYatesShuffleStrategy` as `IShuffleStrategy` (Board) vs `ShuffleStrategyProvider` (Ingame) | :42 | :58 | 충돌 아님 — 보드 쪽이 Provider를 쓰도록 정리 검토 |

이관 대상 (Board 고유): `BoardViewSet` RegisterInstance, `PyramidGameService`,
`TriPeaksGameService`, `BoardGameServiceFactory`, `IBoardSnapshotService`,
`RegisterEntryPoint<BoardPresenter>`. IngameScene에 `[SerializeField] UIBoardController` 2종 추가
(BoardViewSet 생성용 — 생성자 주입 plain class라 그대로 재사용).

- 프리젠터: **병합하지 않는다.** IngamePresenter(카드)와 BoardPresenter(보드)를 둘 다
  EntryPoint로 등록하고, 각자 시작 시 `query.GameType`이 자기 담당이 아니면 즉시 no-op
  리턴. god-presenter 방지가 목적. (Phase A의 ShellFlowController가 공통부를 이미 흡수;
  no-op 가드는 **구독 자체를 건너뛰어** 이벤트 이중 처리를 차단해야 함 — 특히 둘 다
  flow.Wire를 호출하면 shell 이벤트가 2번 처리되므로, 활성 프리젠터만 Wire.)

### B-3. 라우트 통합
실측 터치포인트 (RouteService는 매핑 테이블 없이 scene 이름을 `SceneManager.LoadSceneAsync`에
그대로 전달 — AppPresenter.cs:41–44):

| 파일 | 라인 | 내용 |
|------|------|------|
| Scene/Lobby/LobbyPresenter.cs | 39, 203, 338 | const + 게임선택/이어하기 분기 |
| Scene/Board/BoardPresenter.cs | 32, 456, 476 | const + 재시작 2종 |
| Scene/Shared/ShellFlowController.cs | 253 | **인라인 리터럴** (const 미사용 — 누락 주의) |
| ProjectSettings/EditorBuildSettings.asset | index 4 | BoardScene.unity 엔트리 |

- 위 호출처를 전부 "Ingame"으로 변경 (GameType 쿼리 파라미터는 이미 존재).
- 라우트 이름은 외부에 영속되지 않음 (GameRouteParams는 쿼리 키만, SnapshotKey는
  GameType 기반) → **리다이렉트 유지 불필요**, 같은 릴리스에서 일괄 전환 가능.
  단, BoardScene.unity 자체는 한 커밋 뒤에 삭제(롤백 경계).

### B-4. BoardScene 제거
- 라우트 전환 커밋과 분리된 마지막 커밋: BoardScene.unity + BoardScene.cs 삭제,
  Build Settings 정리, BoardSceneName const 제거.

### B-5. 검증
- 전체 EditMode 스위트 PASS.
- 에디터 스모크 매트릭스: 5게임 × {새 게임, 이어하기(스냅샷), GameCode 재입장, restart,
  daily(Klondike)} + 게임 간 전환(카드→보드→카드) 시 루트 활성화/해제 누수 확인.
- 씬 전환 시 LifetimeScope dispose 정상 여부 (기존 Ingame↔Lobby 패턴과 동일해야 함).

추정 규모(수정): C# 6~10파일 + Ingame.unity 1회 편집 + Build Settings. 프리팹 신규 없음.

### B-6. 신규 게임 타입 확장 경로 (Spider / FreeCell 대비)
Spider/FreeCell은 **카드 게임** — 보드 루트가 아니라 카드 테이블(`Table`) 재사용:
- 추가 비용 = `IDealRule` asset + 점수 규칙 + (필요 시) SolitaireCardService 이동 규칙 확장
  + DealRuleFactory variants 등록. 씬/프리팹 작업 없음.
- 단 FreeCell의 free cell 4칸, Spider의 10-컬럼 테이블은 `Table` 레이아웃 변형이 필요할 수
  있음 → 그 경우 Table도 프리팹화해서 게임 타입별 variant를 두는 것을 검토 (Phase B에서
  Table을 그대로 두되, 프리팹화 여부는 Spider 착수 시 결정).
- 새 **보드** 게임(예: Golf)은 = 보드 프리팹 1개 + `BoardViewSet` 필드 1개 +
  `BoardGameServiceFactory` 분기 1개 + Configure 등록 1줄.

---

## 리스크 / 완화

| 리스크 | 완화 |
|--------|------|
| Ingame.unity가 모든 UI 작업의 단일 충돌 지점化 | 보드 계층을 프리팹으로 분리해 씬 자체는 배치만 담당 |
| 프리젠터 공존 시 이벤트 이중 구독 | no-op 가드를 Start() 최상단에서 GameType으로 일괄 처리, 구독 자체를 건너뜀 |
| Board 라우트 딥링크/스냅샷 호환 | 한 릴리스 리다이렉트 유지 후 제거 |
| 씬 YAML 대량 변경으로 리뷰 불가 | B-1을 프리팹화 커밋 → 씬 배치 커밋으로 분할 |

## Out of scope
- 게임 타입 추가 구현 자체 (Spider/FreeCell) — 단, B-6의 확장 경로 확보는 Phase B 범위
- 보드 게임 daily 모드
- IngameShell 내부 패널 구조 개편 (현 구조 유지)

## Rollback
- Phase A: 코드만 — revert 한 번.
- Phase B: BoardScene 삭제를 마지막 커밋으로 분리 — 문제가 나오면 라우트만 되돌려
  BoardScene 경로 복원 가능.
