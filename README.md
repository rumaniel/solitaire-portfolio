# Solitaire (Unity)

> **TL;DR** — A production-grade mobile solitaire built as an engineering showcase: **five game families on one shared card engine, provably-solvable deal generation, and a full CI → Google Play release pipeline.** ~270 C# files across **13 compiler-isolated assemblies**, **~60 EditMode test suites**, Unity 6.3.
>
> **요약** — 포트폴리오용으로 만든 프로덕션급 모바일 솔리테어. **하나의 공용 카드 엔진 위에 5개 게임군, "반드시 풀리는" 딜 생성, GitHub Actions → Google Play 배포 파이프라인**까지 갖췄다. C# 약 270개 파일, **컴파일러로 격리된 13개 어셈블리**, **EditMode 테스트 스위트 약 60개**, Unity 6.3.

> **ℹ️ About this repository / 이 저장소에 대하여**
>
> This is a **curated, source-only mirror** of a private Unity project, published as a code portfolio. It carries the C# source (`Assets/Scripts` + EditMode tests), assembly definitions, design docs, CI/CD workflows, and dependency manifests — it is **not a runnable Unity project**. Third-party SDKs (Firebase, Google Play Games, NuGet), binary assets (sprites, audio, fonts), signing config, and Unity `.meta` files are intentionally omitted. Card & UI art in the full game is [Kenney.nl](https://kenney.nl/) (CC0). Read this as architecture, not as something to build.
>
> 비공개 Unity 프로젝트의 **소스 전용 큐레이션 미러**다 (코드 포트폴리오용). C# 소스(`Assets/Scripts` + EditMode 테스트), 어셈블리 정의, 설계 문서, CI/CD 워크플로, 의존성 매니페스트를 담았다 — **실행 가능한 Unity 프로젝트는 아니다**. 서드파티 SDK(Firebase·Google Play Games·NuGet), 바이너리 에셋(스프라이트·오디오·폰트), 서명 설정, Unity `.meta` 파일은 의도적으로 제외했다. 전체 게임의 카드·UI 아트는 [Kenney.nl](https://kenney.nl/)(CC0). 빌드 대상이 아니라 아키텍처로 읽어 달라.

---

## 📖 Case Study / 사례 연구

### Why solitaire? / 왜 솔리테어인가

**EN** — Solitaire is a *deliberately* solved domain. That is the point: a problem simple enough that engineering rigor has nowhere to hide. The interesting work is not the card rules — it is the solvability solver, the shared multi-variant engine, the strict assembly layering, and the release pipeline built around them.

**KR** — 솔리테어는 *의도적으로* 단순한 도메인이다. 문제 자체가 쉬워서 엔지니어링 완성도가 숨을 곳이 없다는 게 핵심이다. 흥미로운 부분은 카드 규칙이 아니라 그 주위를 둘러싼 **solvability 솔버, 공용 멀티-변형 엔진, 엄격한 어셈블리 레이어링, 배포 파이프라인**이다.

### Engineering highlights / 엔지니어링 하이라이트

1. **Provably-solvable deals — the hard part.**
   New games are *guaranteed winnable*. A runtime solver (`KlondikeFastSolver`) uses **bit-packed state keys** and prunes the empty-foundation symmetry a naive pile-indexed search wastes time on. `SolvableSeedResolver` deterministically resamples seeds until one passes, then **records the verified seed in the shareable GameCode** — so a replayed or shared deal *never re-runs the solver* and stays identical across app versions even if the solver changes. An `[Explicit]` benchmark gates solve latency; a background `SolverScheduler` prefetch hides it behind the deal animation. Klondike/Easthaven, Pyramid and TriPeaks each have solvers; an editor-only DFS `KlondikeSolver` acts as a reference oracle that cross-checks the fast runtime one.
   *“반드시 풀리는” 딜 — 가장 어려운 부분.* 새 게임은 **항상 클리어 가능**하다. 런타임 솔버(`KlondikeFastSolver`)는 **비트팩 상태 키**로 빈 foundation 대칭성을 가지치기한다. `SolvableSeedResolver`가 결정론적으로 시드를 재샘플링해 통과하는 시드를 찾고 **검증된 시드를 공유 GameCode에 기록**한다 — 따라서 재생·공유 딜은 솔버를 다시 돌리지 않으며, 솔버가 바뀌어도 앱 버전 간 동일한 딜이 보장된다. `[Explicit]` 벤치마크가 지연을 게이트하고, 백그라운드 `SolverScheduler` 프리페치가 딜 애니메이션 뒤로 지연을 숨긴다.

2. **Five game families, one engine.**
   Klondike, Easthaven, Spider, Pyramid and TriPeaks all run on one flag-extended card engine. Rules are injected as strategies (`IDealRule` + factory), not branched with `if/else`. Adding a variant means a rule object plus assets — not engine surgery.
   *5개 게임군, 하나의 엔진.* 5종 모두 플래그로 확장된 단일 엔진 위에서 동작한다. 규칙은 `if/else` 분기가 아니라 전략(`IDealRule` + 팩토리)으로 주입된다. 변형 추가 = 규칙 객체 + 에셋, 엔진 수술이 아니다.

3. **Compiler-enforced architecture.**
   13 assemblies form a clean DAG (Model/Core/Shared → Data → Gateway → Service → {Audio, Component} → Scene/App). An illegal cross-layer reference is a **compile error**, not a review comment. Domain models are immutable value objects (`IEquatable`, with-pattern).
   *컴파일러가 강제하는 아키텍처.* 13개 어셈블리가 깨끗한 DAG를 이룬다. 레이어 위반은 리뷰 코멘트가 아니라 **컴파일 에러**다. 도메인 모델은 불변 값 객체(`IEquatable`, with-패턴).

4. **Tested like production.**
   ~60 EditMode NUnit suites: move validation, per-variant scorers, solvers, seed determinism, snapshot round-trips, localization, GDPR consent, achievements.
   *프로덕션처럼 테스트.* EditMode NUnit 스위트 약 60개 — 이동 검증, 변형별 점수, 솔버, 시드 결정성, 스냅샷 왕복, 현지화, GDPR 동의, 업적.

5. **A real release pipeline.**
   GitHub Actions (self-hosted macOS) → EditMode tests + Android build verification → signed AAB → Play Console internal track. `versionCode` is pulled from the Play Publishing API as the source of truth; release notes are auto-assembled from merged PR titles (en-US/ko-KR).
   *실제 배포 파이프라인.* GitHub Actions(self-hosted macOS) → 테스트 + 안드로이드 빌드 검증 → 서명 AAB → Play Console 내부 트랙. `versionCode`는 Play API가 source of truth, 릴리스 노트는 머지된 PR 제목에서 자동 생성(en-US/ko-KR).

### Key decisions & tradeoffs / 핵심 결정과 트레이드오프

- **13 assemblies for a card game — over-engineered?** Deliberate, and I will defend it: the asmdef wiring cost buys *compiler-enforced* layering and fast incremental compiles. I would make the same call on a team codebase; on a one-off prototype I would not. Naming the tradeoff is the senior part.
  *카드 게임에 13개 어셈블리, 과한가?* 의도적이다. asmdef 배선 비용을 치르는 대신 **컴파일러가 강제하는** 레이어링과 빠른 증분 컴파일을 얻는다. 팀 코드베이스라면 같은 선택을, 일회성 프로토타입이라면 안 했을 것이다.

- **Persist the *resolved* seed, not the input seed.** Shared and replayed deals must be reproducible forever. Depending on a live solver verdict at replay time would let a future solver change silently alter an old shared deal. So: verify once, persist the proof.
  *입력 시드가 아니라 검증된 시드를 저장.* 공유·재생 딜은 영구적으로 재현 가능해야 한다. 재생 시점에 솔버 판정을 다시 의존하면 솔버 변경이 옛 딜을 조용히 바꿀 수 있다. → 한 번 검증하고 그 증거를 저장한다.

- **Immutable models + R3 reactive streams.** All mutation flows through services; state changes are observable, which makes undo/snapshot natural — at the cost of allocations. Worth it for correctness.
  *불변 모델 + R3 리액티브.* 모든 변경은 서비스를 통과하고 상태 변화는 관찰 가능 — undo/스냅샷이 자연스러워진다. 할당 비용은 감수한다.

- **Root-cause over failover.** A standing project rule (see [`CLAUDE.md`](CLAUDE.md)): no "if-A-fails-try-B" layers stacked on symptoms; fix the contract instead.
  *우회 대신 근본 원인.* 프로젝트 규칙 — 증상 위에 "A 실패하면 B" 레이어를 쌓지 않고 계약을 고친다.

### At a glance / 요약 지표

| | |
|---|---|
| Engine / 엔진 | Unity 6.3 |
| Code / 코드 | ~270 C# files · 13 assemblies |
| Services / 서비스 | 20 interface-backed services |
| Game families / 게임군 | 5 (Klondike, Easthaven, Spider, Pyramid, TriPeaks) |
| Tests / 테스트 | ~60 EditMode NUnit suites |
| Infra / 인프라 | VContainer (DI) · R3 (reactive) · UniTask (async) · Addressables · Localization |
| Delivery / 배포 | GitHub Actions → signed AAB → Google Play internal track |

> Architecture deep-dive and developer workflows follow below. / 아래는 아키텍처 상세와 개발 워크플로.

## Table of Contents

- 📖 [Case Study / 사례 연구](#-case-study--사례-연구)
- 📂 [Reading Guide / 읽기 가이드](#-reading-guide--읽기-가이드)
- 🏗 [Architecture Overview](#-architecture-overview)
  - 📐 [Layer Diagram](#-layer-diagram)
  - 🔗 [Assembly Dependency Flow](#-assembly-dependency-flow)
- 🧱 [Layer Details](#-layer-details)
  - [Model](#-model-순수-도메인) · [Data](#-data-설정-에셋) · [Service](#-service-비즈니스-로직) · [Gateway](#-gateway-외부-통합) · [Component](#-component-ui-컴포넌트) · [Core](#-core-기반-클래스) · [Scene](#-scene-mvp-패턴) · [App](#-app-부트스트랩) · [Shared / Editor](#-shared--editor)
- 🎨 [Core Patterns](#-core-patterns)
  - 🎭 [MVP Scene Pattern](#-mvp-model-view-presenter-scene-pattern)
  - ⚡ [Reactive State (R3)](#-reactive-state-r3)
  - 💉 [VContainer DI Registration](#-vcontainer-di-registration)
  - 🏭 [Strategy / Factory Pattern](#-strategy--factory-pattern)
- 📦 [External Dependencies](#-external-dependencies)
- 🌳 [Project Structure](#-project-structure)
- 🧪 [Testing](#-testing)
- 🤖 [CI / CD (GitHub Actions)](#-ci--cd-github-actions)
  - 🚢 [Release Pipeline](#-release-pipeline-v-tag--play-console)
  - 📝 [Implementation Notes](#-implementation-notes)
  - 🔐 [Required Secrets / Variables](#-required-secrets--variables)
- 🔨 [Developer Workflows](#-developer-workflows)
  - ✨ [새 서비스 추가](#-새-서비스-추가)
  - 🎥 [새 씬 추가](#-새-씬-추가)
  - 🧭 [RouteService 사용](#-routeservice-사용)
  - 📌 [릴리스 절차](#-릴리스-절차)
  - 🔄 [CI 도입 이후 달라진 점](#-ci-도입-이후-달라진-점)
- 🆘 [Troubleshooting](#-troubleshooting)

---

## 📂 Reading Guide / 읽기 가이드

This repo is meant to be **read**, not run. Suggested path:

1. **Start with the [Case Study](#-case-study--사례-연구) above** — the *why*, the hard parts, the tradeoffs.
2. **The interesting code** — provably-solvable deals live in [`Assets/Scripts/Service/GameService/`](Assets/Scripts/Service/GameService) (`KlondikeFastSolver`, `SolvableSeedResolver`, `SolverScheduler`, `GameCode`).
3. **Architecture as compiler-enforced contracts** — read the `*.asmdef` files; the dependency graph is the [Assembly Dependency Flow](#-assembly-dependency-flow) below.
4. **The pipeline** — [`.github/workflows/`](.github/workflows) (`build`, `test`, `release`).

> 이 저장소는 실행이 아니라 **읽기**용이다. 추천 순서: ① 위 [사례 연구](#-case-study--사례-연구)로 *왜*와 트레이드오프를 본다 → ② 핵심 코드는 [`Assets/Scripts/Service/GameService/`](Assets/Scripts/Service/GameService)의 솔버/시드 → ③ `*.asmdef`로 컴파일러가 강제하는 아키텍처 → ④ [`.github/workflows/`](.github/workflows)의 파이프라인.

---

## 🏗 Architecture Overview

### 📐 Layer Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          App (Bootstrap)                            │
│              AppLifetimeScope · AppPresenter                        │
│         DI 등록, 서비스 초기화, 앱 진입점                              │
└──────────┬──────────┬──────────┬──────────┬─────────────────────────┘
           │          │          │          │
           ▼          ▼          ▼          ▼
┌──────────────┐ ┌─────────┐ ┌─────────┐ ┌──────────┐
│    Scene     │ │ Service │ │ Gateway │ │   Core   │
│  (MVP 패턴)  │ │ (비즈니스 │ │ (외부    │ │ (기반    │
│  Login       │ │  로직)   │ │  통합)   │ │  클래스) │
│  Lobby       │ │ 9 services│ │ Auth    │ │ SceneBase│
│  Ingame      │ │ Interface │ │ Stats   │ │ Component│
│  + Views     │ │  + Impl  │ │ Snapshot│ │  Base    │
└──┬───┬───┬───┘ └────┬─────┘ └────┬────┘ └────┬────┘
   │   │   │          │            │            │
   ▼   ▼   ▼          ▼            │            │
┌───────────┐  ┌───────────┐       │            │
│ Component │  │   Data    │       │            │
│ (UI 컴포넌트)│  │(ScriptableObject)│  │            │
│ UICard     │  │ CardSprite│       │            │
│ Controller │  │ DealRule  │       │            │
│ Animator   │  │ AudioDB   │       │            │
└─────┬─────┘  └─────┬─────┘       │            │
      │               │            │            │
      ▼               ▼            ▼            │
   ┌──────────────────────────────────────┐     │
   │            Model (순수 도메인)          │     │
   │  PlayingCard · TableState · PileState │     │
   │  SessionStats · LifetimeStats · User  │     │
   │         (No Unity dependencies)       │     │
   └───────────────────────────────────────┘     │
                                                 │
   ┌─────────────┐    ┌──────────┐               │
   │   Shared    │    │  Editor  │◄──────────────┘
   │  (유틸리티)  │    │(에디터 전용)│
   └─────────────┘    └──────────┘
```

### 🔗 Assembly Dependency Flow

```
                    ┌─────────┐
                    │  Model  │  ◄── 순수 도메인 (의존성 없음)
                    └────┬────┘
                         │
           ┌─────────────┼─────────────┐
           │             │             │
           ▼             ▼             ▼
      ┌─────────┐  ┌──────────┐  ┌──────────┐
      │ Gateway │  │   Data   │  │ Service  │
      │         │  │          │◄──│          │
      └────┬────┘  └──────────┘  └────┬─────┘
           │                          │
           └──────────┬───────────────┘
                      │
                      ▼
                ┌───────────┐     ┌──────────┐
                │ Component │     │   Core   │
                └─────┬─────┘     └────┬─────┘
                      │                │
                      └──────┬─────────┘
                             │
                             ▼
                       ┌───────────┐
                       │   Scene   │  ◄── 모든 레이어 참조 가능
                       └─────┬─────┘
                             │
                             ▼
                       ┌───────────┐
                       │    App    │  ◄── Composition Root
                       └───────────┘
```

> **원칙**: 의존성은 항상 아래에서 위로 흐릅니다. Model은 아무것도 참조하지 않고, App은 모든 것을 조합합니다.

---

## 🧱 Layer Details

### 🃏 Model (순수 도메인)

Unity 의존성이 없는 순수 C# 도메인 모델. `IEquatable<T>` 구현, 불변 스타일.

| 도메인 | 클래스 | 역할 |
|--------|--------|------|
| Card | `PlayingCard`, `Rank`, `Suit`, `CardDto` | 카드 값 객체 |
| Game | `TableState`, `PileState`, `PileId`, `PileType`, `GameType`, `GameSnapshot` | 게임 보드 상태 |
| Stats | `SessionStats`, `LifetimeStats`, `IScoreRule`, `MoveType`, `ScoredMoveInfo` | 점수/통계 |
| User | `User` | 사용자 정보 |

### 📊 Data (설정 에셋)

ScriptableObject 기반 설정 데이터. Inspector에서 편집 가능.

| 에셋 | 역할 |
|------|------|
| `CardSpriteSet` / `CardSpriteLookup` | 카드 스프라이트 매핑 (Rank+Suit → Sprite) |
| `DealRuleAsset` | 게임 규칙 (DrawCount, RecycleAllowed 등) |
| `AudioDatabaseAsset` / `AudioCatalog` | 오디오 클립 레지스트리 |
| `ScoreRuleAsset` | 점수 계산 규칙 |

### 🔩 Service (비즈니스 로직)

총 **20개**의 서비스가 모두 인터페이스(`I*Service`)를 노출하고 VContainer로 DI 등록됩니다. 아래 표는 핵심 9개이며, 이 외에 Skin · Localization · Haptic · Consent · Achievement(+Google Play 미러) · Layout · DailyStats · BoardGame · SolvableSeedPrefetch 서비스가 있습니다.

| 서비스 | 인터페이스 | 핵심 책임 |
|--------|-----------|----------|
| **GameService** | `IGameService` | 보드 상태 관리, 이동 실행, Undo 히스토리 |
| **CardService** | `ICardService` | 카드 이동 유효성 검증 (IDealRule 위임) |
| **HintService** | `IHintService` | 힌트 제안, 자동 완성 감지 |
| **AudioService** | `IAudioService` | 음악/SFX 재생, Observable 채널 |
| **RouteService** | `IRouteService` | 씬 네비게이션, 히스토리 스택 |
| **UserService** | `IUserService` | 사용자 인증, Login/Logout |
| **SessionStatsService** | `ISessionStatsService` | 현재 게임 점수, 이동 수, 시간 |
| **LifetimeStatsService** | `ILifetimeStatsService` | 누적 통계 (승률, 연승, 최고 점수) |
| **SnapshotService** | `IGameSnapshotService` | 게임 상태 자동 저장/복원 |

### 🚪 Gateway (외부 통합)

인터페이스 기반 외부 시스템 연동. 구현체 교체 가능.

| 인터페이스 | 구현체 | 역할 |
|-----------|--------|------|
| `IAuthGateway` | `UnityAuthGateway`, `FirebaseAuthGateway` | UUID 생성 (로컬/Firebase) |
| `IStatsRepository` | `LocalStatsRepository` | LifetimeStats PlayerPrefs 저장 |
| `IGameSnapshotRepository` | `LocalGameSnapshotRepository` | 게임 스냅샷 로컬 저장 |

### 🧩 Component (UI 컴포넌트)

MonoBehaviour 기반 재사용 UI 컴포넌트. EventSystem 인터페이스와 R3 Observable로 이벤트 전달.

| 컴포넌트 | 역할 |
|----------|------|
| `UICard` | 카드 스프라이트, 드래그/드롭/클릭 핸들링 |
| `UICardsController` | 카드 인스턴스 풀 관리, 플레이스홀더 |
| `UIPlaceHolder` | 파일(Stock/Waste/Foundation/Tableau) 앵커 |
| `CardMoveAnimator` | 카드 이동 애니메이션 (트윈) |
| `AudioPlayer` | 오디오 재생 래퍼 |

### 🧠 Core (기반 클래스)

VContainer 자동 주입을 위한 베이스 클래스.

| 클래스 | 역할 |
|--------|------|
| `SceneBase` | VContainer `LifetimeScope` 확장, 씬 DI 설정 |
| `ComponentBase` | `Awake`에서 자동으로 `LifetimeScope.Find<SceneBase>()` → `Inject(this)` |
| (`AudioSystem`) | 싱글턴 오디오 시스템 — 현재는 전용 **Audio** 어셈블리로 분리됨 (Core 아님) |

### 🎬 Scene (MVP 패턴)

각 씬은 Scene(DI) + Presenter(로직) + Component(UI) 구조.

| 씬 | Presenter | Component | 설명 |
|----|-----------|-----------|------|
| **Login** | `LoginPresenter` | `LoginComponent` | 로그인 화면 |
| **Lobby** | `LobbyPresenter` | `LobbyComponent` | 게임 선택, 이어하기/새 게임 |
| **Ingame** | `IngamePresenter` | `IngameComponent` | 게임 플레이 + HudView, WinPanel, StatsPanel 등 |

### 🚦 App (부트스트랩)

| 클래스 | 역할 |
|--------|------|
| `AppLifetimeScope` | 전역 싱글턴 서비스 DI 등록 |
| `AppPresenter` | 앱 진입점, 서비스 초기화, 첫 씬 네비게이션 |

### 🛎 Shared / Editor

- **Shared**: `ReadOnlyAttribute`, `DontDestroy` 등 범용 유틸리티
- **Editor**: Inspector 커스텀 드로어, 에디터 전용 도구 (Editor 플랫폼 한정)

---

## 🎨 Core Patterns

### 🎭 MVP (Model-View-Presenter) Scene Pattern

```
┌─ YourScene.cs (SceneBase) ─────────────────┐
│  Configure(IContainerBuilder builder)       │
│  ├── builder.RegisterComponent(component)   │
│  └── builder.RegisterEntryPoint<Presenter>()│
└─────────────────────────────────────────────┘
          │                     │
          ▼                     ▼
┌─ YourComponent.cs ─┐  ┌─ YourPresenter.cs ──────────┐
│  [SerializeField]   │  │  IStartable, IInitializable │
│  UI References      │  │  ITickable, IDisposable     │
│  Observable Events  │◄─│  서비스 주입 + 구독          │
└─────────────────────┘  └─────────────────────────────┘
```

### ⚡ Reactive State (R3)

```csharp
// Service: 상태를 Observable로 노출
public Observable<TableState> OnTableStateChanged { get; }

// Presenter: 구독 + 자동 해제
gameService.OnTableStateChanged
    .Subscribe(state => RefreshUI(state))
    .AddTo(component);  // 씬 언로드 시 자동 해제
```

### 💉 VContainer DI Registration

```csharp
// AppLifetimeScope.cs — 전역 싱글턴
builder.Register<UserService>(Lifetime.Singleton).As<IUserService>();
builder.Register<RouteService>(Lifetime.Singleton).As<IRouteService>();
builder.RegisterEntryPoint<AppPresenter>().As<AppPresenter>();

// Scene LifetimeScope — 씬 스코프
builder.RegisterComponent(component);          // SerializeField 컴포넌트
builder.RegisterEntryPoint<IngamePresenter>();  // 씬 진입점
builder.RegisterInstance(dealRuleAsset);        // ScriptableObject 데이터
```

### 🏭 Strategy / Factory Pattern

```
GameType.Klondike ──▶ IDealRuleFactory.Create() ──▶ IDealRule (규칙 인터페이스)
GameType.Easthaven ─┘                               │
                                                     ▼
                                              CardService.TryMove()
                                              (규칙에 위임하여 검증)
```

---

## 📦 External Dependencies

| 패키지 | 용도 |
|--------|------|
| **VContainer** (`jp.hadashikick.vcontainer`) | 의존성 주입 컨테이너 |
| **UniTask** (`com.cysharp.unitask`) | Unity 호환 async/await |
| **R3** (`com.cysharp.r3`) | Reactive Observable 스트림 |
| **NaughtyAttributes** (`com.dbrizov.naughtyattributes`) | Inspector 확장 |
| **MemoryPack** | 고성능 바이너리 직렬화 (Model/Gateway) |
| **TextMeshPro** (`com.unity.textmeshpro`) | UI 텍스트 렌더링 |
| **Addressables** (`com.unity.addressables`) | 에셋 관리 |
| **Localization** (`com.unity.localization`) | 다국어 지원 |

---

## 🌳 Project Structure

```
Assets/Scripts/
├── App/                    # 부트스트랩 (AppLifetimeScope, AppPresenter)
├── Model/                  # 순수 도메인 모델
│   ├── Card/               #   PlayingCard, Rank, Suit
│   ├── Game/               #   TableState, PileState, GameType, GameSnapshot
│   ├── Stats/              #   SessionStats, LifetimeStats, IScoreRule
│   └── User/               #   User
├── Data/                   # ScriptableObject 설정
│   ├── Card/               #   CardSpriteSet, CardSpriteLookup
│   ├── Game/               #   DealRuleAsset
│   ├── Audio/              #   AudioDatabaseAsset, AudioCatalog
│   └── Stats/              #   ScoreRuleAsset
├── Core/                   # 베이스 클래스
│   ├── ComponentBase.cs    #   자동 DI 주입
│   ├── SceneBase.cs        #   LifetimeScope 확장
│   └── Audio/              #   AudioSystem (싱글턴)
├── Service/                # 비즈니스 로직 (9개 서비스)
│   ├── AudioService/       #   IAudioService
│   ├── CardService/        #   ICardService, MoveCardRequest/Result
│   ├── GameService/        #   IGameService, IDealRule, DealBuilder, DeckFactory
│   ├── HintService/        #   IHintService, HintMove
│   ├── RouteService/       #   IRouteService, GameRouteParams
│   ├── UserService/        #   IUserService
│   ├── StatsService/       #   ISessionStatsService, ILifetimeStatsService
│   └── SnapshotService/    #   IGameSnapshotService
├── Gateway/                # 외부 통합
│   ├── Auth/               #   IAuthGateway → UnityAuth / Firebase
│   ├── Stats/              #   IStatsRepository → LocalStatsRepository
│   └── Snapshot/           #   IGameSnapshotRepository → LocalGameSnapshotRepository
├── Component/              # UI 컴포넌트
│   ├── Card/               #   UICard, UICardsController, UIPlaceHolder, CardMoveAnimator
│   │   └── Events/         #   CardClicked, CardDragStarted, CardDroppedOnPile
│   ├── Audio/              #   AudioPlayer
│   └── Core/               #   NonDrawingGraphic
├── Scene/                  # MVP 씬
│   ├── Login/              #   LoginScene, LoginPresenter, LoginComponent
│   ├── Lobby/              #   LobbyScene, LobbyPresenter, LobbyComponent
│   └── Ingame/             #   IngameScene, IngamePresenter, IngameComponent
│       └── View/           #   IngameHudView, WinPanelView, StatsPanelView, StuckPanelView
├── Shared/                 # 범용 유틸리티
└── Editor/                 # 에디터 전용 도구

Assets/Tests/EditMode/      # NUnit 테스트 (~60개 테스트 스위트)
```

---

## 🧪 Testing

약 **60개**의 EditMode NUnit 테스트 스위트가 있습니다. 아래는 대표 예시이며, 이 외에 변형별(Spider/Pyramid/TriPeaks/Easthaven) GameService·Scorer·Solver, 시드 결정성(`SolvableSeedResolver`/`DailySeedResolverV2`), 스냅샷, 현지화, 동의(Consent), 업적, 스킨 테스트 등이 포함됩니다:

| 테스트 | 검증 대상 |
|--------|----------|
| `DealBuilderTests` | 게임 딜 생성 |
| `DeckFactoryTests` | 덱 초기화 |
| `GameCodeTests` | 게임 코드 인코딩/파싱 |
| `HintServiceTests` | 힌트 이동 계산 |
| `LifetimeStatsServiceTests` | 누적 통계 |
| `SessionStatsServiceTests` | 세션 점수 |
| `ShuffleDistributionTests` | 셔플 랜덤성 |
| `SolitaireCardServiceTests` | 이동 유효성 |

```bash
# 테스트 실행
uloop run-tests --mode EditMode

# 컴파일 확인
uloop compile
```

---

## 🤖 CI / CD (GitHub Actions)

Self-hosted **macOS runner** (mac-mini 1대) — 동시 실행 1개로 직렬화됩니다. PR 푸시가 빠르게 이어질 때만 superseded run을 자동 취소하고, `main` push와 수동 실행은 끝까지 돌립니다.

| 워크플로 | 트리거 | 역할 |
|----------|--------|------|
| **Test** ([test.yml](.github/workflows/test.yml)) | PR → main, 수동 | EditMode NUnit 테스트 (Unity batchmode), 결과 XML/log 업로드 |
| **Build** ([build.yml](.github/workflows/build.yml)) | PR → main, push → main, 수동 | Android 빌드 검증 (`CIBuilder.BuildAndroid`). 수동 실행 시에만 AAB artifact 보관 |
| **Release** ([release.yml](.github/workflows/release.yml)) | `v*` 태그 push, 수동 | 서명된 AAB 빌드 + Play Console **internal** 트랙 **draft** 업로드 |

### 🚢 Release Pipeline (`v*` tag → Play Console)

1. `git tag v0.x.y && git push --tags` — `versionName`은 `ProjectSettings.bundleVersion`을 그대로 사용
2. **versionCode 자동 증가**: Play Console Publishing API에서 모든 트랙의 max `versionCode` 조회 → +1 (Play Console이 source of truth, `ProjectSettings.asset`에 직접 올리지 않음)
3. **Release notes 자동 생성**: 직전 `v*` 태그 이후 머지된 PR 제목을 모아 `distribution/whatsnew/whatsnew-{en-US,ko-KR}` 작성 (Play Store 500자 제한 고려, 줄 단위 truncate)
4. **AAB 서명·업로드**: `CIBuilder.BuildAndroidRelease` → `r0adkll/upload-google-play@v1` → internal track **draft** (수동 승급 필요)
5. AAB와 빌드 로그는 30일간 artifact로 보존

### 📝 Implementation Notes

- **Self-hosted macOS** 사용 이유: GameCI `unity-test-runner@v4`가 macOS 호스트를 미지원해 Unity를 직접 호출
- **Node 24 강제**: GitHub Node 20 deprecation(2026-09-16) 대비 `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true` 설정. `actions/upload-artifact@v5`가 Node 24 네이티브 릴리스를 내면 제거 가능
- **NuGet runtime strip**: NuGetForUnity가 `Assets/Packages/{pkg}/runtimes/<rid>/native/*`로 모든 RID 바이너리를 풀어놓아 Android 빌드 시 iOS Mach-O `.a`에서 ld.lld가 깨짐. 빌드 직전 타겟 외 RID 폴더를 강제 삭제
- **WebGL 보류**: 같은 NuGet 네이티브 의존성 문제로 Emscripten 링크 실패. `PluginImporter` 헬퍼로 WebGL 비호환 플래그 적용 후 재추가 예정
- **Release 직렬화**: 서로 다른 ref에서 release.yml이 동시에 돌면 Play API에 같은 `versionCode`를 묻고 충돌. 단일 concurrency group + `cancel-in-progress: false`로 직렬화

### 🔐 Required Secrets / Variables

| 종류 | 키 | 용도 |
|------|----|------|
| Secret | `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEYALIAS_PASSWORD` | AAB 서명 (누락 시 `CIBuilder.ConfigureAndroidSigning`이 debug keystore로 fallback. release.yml도 빌드는 통과하지만 debug-signed AAB는 Play Console 업로드 단계에서 거부됨) |
| Secret | `PLAY_CONSOLE_SERVICE_ACCOUNT` | Play Console Publishing API JSON 키 (`androidpublisher` scope) |
| Variable | `ANDROID_KEYSTORE_PATH`, `ANDROID_KEYALIAS_NAME` | mac-mini 로컬 keystore 경로 / alias 이름 |
| Variable | `PLAY_CONSOLE_PACKAGE_NAME` | `com.example.solitaire` 형태의 패키지 이름 |

---

## 🔨 Developer Workflows

### ✨ 새 서비스 추가

1. `Assets/Scripts/Service/YourService/IYourService.cs` 인터페이스 생성
2. `YourService.cs` 구현
3. `AppLifetimeScope.Configure()`에 등록:
   ```csharp
   builder.Register<YourService>(Lifetime.Singleton).As<IYourService>();
   ```
4. Presenter에서 주입: `[Inject] private IYourService YourService { get; set; }`

### 🎥 새 씬 추가

1. `Assets/Scenes/`에 씬 파일 생성
2. `Assets/Scripts/Scene/YourScene/` 폴더에 3개 파일:
   - `YourSceneScene.cs` — `SceneBase` 확장, `Configure()` 오버라이드
   - `YourScenePresenter.cs` — `IStartable`, `IInitializable` 구현
   - `YourSceneComponent.cs` — `ComponentBase` 확장, UI 참조
3. `RouteService.NavigateAsync("YourScene")`으로 네비게이션

### 🧭 RouteService 사용

```csharp
// SceneManager를 직접 호출하지 마세요!
// 항상 RouteService를 통해 네비게이션합니다.
await RouteService.NavigateAsync("Ingame", new Dictionary<string, string>
{
    { GameRouteParams.GameType, "Klondike" }
});
await RouteService.GoBackAsync();
```

### 📌 릴리스 절차

1. `main`을 최신화하고 `ProjectSettings.bundleVersion`(versionName)이 다음 릴리스와 일치하는지 확인 — 변경이 필요하면 별도 PR로 머지
2. `git tag v0.x.y && git push origin v0.x.y` — 이후는 `release.yml`이 자동 처리
3. Actions 탭에서 `Release Android` job 통과 확인 → Play Console **Internal testing → Drafts**에 draft 릴리스 생성됨
4. release notes(`whatsnew-en-US`/`whatsnew-ko-KR`)와 versionCode 확인 후 Play Console에서 수동 **Review release → Start rollout**
5. 잘못된 태그를 밀었으면 태그를 삭제(`git push --delete origin v0.x.y`)하고 다시 만들면 됨 — Play Console의 draft는 콘솔에서 폐기

> `versionCode`는 절대 손으로 올리지 마세요. Play Console의 max + 1이 source of truth이며, `OverrideAndroidVersionCode`가 빌드 직전 `PlayerSettings.Android.bundleVersionCode`를 덮어씁니다.

### 🔄 CI 도입 이후 달라진 점

- **모든 PR에서 EditMode 테스트가 자동 실행됩니다.** push 전 `uloop run-tests --mode EditMode`로 먼저 통과시키는 습관 유지
- **PR/main push에서 Android 빌드도 검증됩니다.** Editor 전용 코드(`#if UNITY_EDITOR`), 플랫폼 분기, 새 NuGet 패키지 추가 시 build.yml의 `Strip non-target runtime binaries` 단계 영향을 검토
- **PR 제목이 곧 릴리스 노트입니다.** 머지 PR 제목은 사용자가 읽을 한 줄로 작성. Conventional Commits prefix(`fix:`, `feat:` 등)는 그대로 노출되니 prefix 뒤 본문이 의미 있는지 확인
- **Self-hosted runner 1대**라 long-running PR이 다른 PR을 블록할 수 있습니다. 빌드 시간이 오래 걸리는 변경은 main 머지 직전에만 push, 또는 작은 단위로 분할
- **새 빌드 타겟 추가** (예: iOS, WebGL 재추가) 시: `CIBuilder`에 method 추가 → `build.yml` matrix에 entry 추가 → runtime strip 매핑(`keep_prefix`) 갱신

---

## 🆘 Troubleshooting

| 문제 | 해결 |
|------|------|
| "RouteService not initialized" | `AppPresenter.Start()`에서 `RouteService.Initialize(sceneLoader)` 확인 |
| 어셈블리 참조 에러 | `.asmdef`에 참조 추가 (Unity Inspector) |
| 서비스 주입 실패 | `AppLifetimeScope` 또는 씬 `LifetimeScope`에 등록 확인 |
| 씬 로드 실패 | Build Settings에 씬 추가 + 경로 대소문자 정확히 일치 |
| 컴파일 에러 | `uloop compile` 실행 후 에러 확인 |
| CI에서 Android 빌드만 실패 (`ld.lld: unknown file type`) | 새로 추가한 NuGet 패키지가 cross-platform native binary를 가져왔을 가능성. `Assets/Packages/<pkg>/runtimes/`에 어떤 RID가 있는지 확인하고 `build.yml`의 `Strip non-target runtime binaries` 매핑 갱신 |
| `Release` 워크플로 `gh pr list` 단계 실패 | `pull-requests: read` 권한 또는 `GITHUB_TOKEN` 만료. release.yml `permissions:` 블록 확인 |
| Play Console 업로드 시 `versionCode already used` | 같은 ref에서 release.yml이 두 번 도는 경우. 단일 concurrency group이 직렬화하지만, 콘솔에서 수동 업로드한 build와 충돌 시 다음 태그를 push |
| Release 태그를 잘못 밀었음 | `git push --delete origin vX.Y.Z` 후 태그 재작성 → 다시 push. Play Console draft는 콘솔에서 수동 삭제 |
