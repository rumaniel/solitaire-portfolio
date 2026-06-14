# Solitaire Project — Architecture & Conventions Guide

## Project Overview

A Unity-based Solitaire card game implementing **clean layered architecture** with clear separation of concerns. The project features multiple game types (Klondike, Easthaven, Pyramid, TriPeaks, and board variants), reactive state management, dependency injection, and comprehensive testing.

**Key Stats:**
- ~260 C# files across 11 architectural layers (exact counts drift — don't trust hard numbers in this doc)
- Unit tests: ~50 EditMode test suites (NUnit)
- Build system: Multiple assembly definitions (asmdef) for layer isolation
- Package dependencies: VContainer (DI), R3 (reactive), UniTask, NaughtyAttributes

---

## Architecture Overview

11 distinct layers with specific responsibilities and clear dependency flows:

1. **Model** — Pure domain models, no Unity dependencies
2. **Data** — ScriptableObject configuration assets
3. **Core** — Base classes only (ComponentBase, SceneBase)
4. **Service** — Business logic and state management
5. **Gateway** — External integrations (auth, persistence, snapshots, Addressables)
6. **Component** — Reusable UI components
7. **Scene** — Scene-specific MVP implementation
8. **App** — Root DI setup and bootstrap
9. **Editor** — Editor-only utilities
10. **Shared** — Cross-layer utilities
11. **Audio** — AudioSystem / AudioSourcePlayer (depends on Core, Data, Service)

> Per-layer file counts removed — they rot fast. For current totals run:
> `find Assets/Scripts -name '*.cs' | wc -l`
> ⚠ `Assets/Scripts/Debug/` and `Assets/Scripts/Generated/` have **no asmdef** → they fall into `Assembly-CSharp` (layering escape hatch; keep them debug/generated-only).

---

## Layer Details

### 1. Model (Pure Domain)
**Namespaces:** Model.Card, Model.Game, Model.Stats, Model.User, Model.Board, Model.Achievement
**Immutable exemplars (copy these):** PlayingCard, PileId, PileState, BoardState, CellId
**Legacy mutable (do NOT imitate — slated for refactor):** LifetimeStats, SessionStats, DailyStats, AchievementStatus, User; TableState still needs IEquatable/GetHashCode
**Pattern:** IEquatable<T> + GetHashCode, no UnityEngine references, readonly/init-only fields, With-pattern for state changes

### 2. Data (Configuration Assets)
**Namespaces:** Data.Card, Data.Audio, Data.Game, Data.Stats
**Key Classes:** CardSpriteSet, AudioDatabaseAsset, DealRuleAsset, ScoreRuleAsset
**Pattern:** ScriptableObject with [CreateAssetMenu], lazy-initialized lookups

### 3. Core (Base Classes)
**Namespaces:** Core
**Key Classes:** ComponentBase, SceneBase
**Pattern:** ComponentBase auto-injects in Awake via LifetimeScope.Find<SceneBase>()
**Note:** Audio (`AudioSystem`/`AudioSourcePlayer`) moved OUT to the dedicated **Audio** assembly — not in Core.

### 4. Service (Business Logic)
**Subdomains:**
- AudioService: IAudioService, Observable<string> OnPlay
- CardService: ICardService validates moves
- GameService: IGameService orchestrates state, Subject<TableState> OnTableStateChanged
- HintService: IHintService suggests valid moves
- RouteService: IRouteService async scene navigation
- StatsService: ISessionStatsService and ILifetimeStatsService
- UserService: IUserService identity management

**Pattern:** Interface-first, **constructor injection** (standard — services take deps as ctor params; a few legacy services still use `[Inject]`), composable, Lifetime.Scoped/Singleton

### 5. Gateway (External Integrations)
**Namespaces:** Gateway.Auth, Gateway.Stats
**Key Classes:** IAuthGateway, IStatsRepository with implementations
**Pattern:** Encapsulates external complexity, allows easy swaps, UniTask async

### 6. Component (Reusable UI)
**Namespaces:** Component.Card, Component.Audio, Component.Core, Component.Game
**Key Classes:** UICard (IPointerClickHandler, IDragHandler), UICardsController, CardMoveAnimator
**Pattern:** Event-driven via R3 Observable<T>, UnityEvent backing, EventSystem interfaces

### 7. Scene (MVP Implementation)
**Namespaces:** Scene.Ingame, Scene.Login, Scene.Lobby, Scene.Board
**Pattern:**
- Scene extends SceneBase, configures DI
- Presenter implements IStartable, ITickable, IDisposable
- Component extends ComponentBase, exposes Observables
- View classes handle UI details (HudView, PanelView, etc.)

### 8. App (Bootstrap)
**Classes:** AppLifetimeScope (DI root), AppPresenter (startup orchestration)
**Pattern:** Registers singletons, initializes services, routes to first scene

### 9. Editor (Editor-Only)
**Pattern:** asmdef with Platform: Editor

### 10. Shared (Cross-Layer)
**Pattern:** Minimal dependencies, utility classes

---

## Dependency Injection: VContainer

Two styles, picked by injection site:

**Constructor injection — Services / Gateways / factories (plain classes).** The standard for services; a few legacy services (`UserService`, `*SnapshotService`) still use `[Inject]`.
```csharp
public sealed class HintService : IHintService
{
    private readonly ICardService cardService;
    public HintService(ICardService cardService) => this.cardService = cardService;
}
```

**`[Inject]` injection (field or property) — Presenters (entry points) and MonoBehaviour Components** (and the legacy services above):
```csharp
[Inject] private IGameService GameService { get; set; }   // property
[Inject] private IAudioService audioService;              // field — both work
```

**Lifetime Management:**
- `Lifetime.Singleton` — App-wide (AppLifetimeScope)
- `Lifetime.Scoped` — Per-scene (SceneBase subclasses)
- `Lifetime.Transient` — New instance per resolve

**Registration:**
```csharp
builder.Register<MyService>(Lifetime.Singleton).As<IMyService>();
builder.RegisterComponent(gameObject);
builder.RegisterInstance<T>(data);
builder.RegisterEntryPoint<MyPresenter>();
```

**Automatic Injection:**
All ComponentBase subclasses auto-inject in Awake:
```csharp
LifetimeScope.Find<SceneBase>(scene)?.Container.Inject(this);
```

---

## Reactive Framework: R3

**Observable Pattern:** Push-based event streams

**Key Usage:**
```csharp
// Services expose Observables:
public Observable<TableState> OnTableStateChanged { get; }

// Presenters subscribe:
gameStateSubject.Subscribe(state => RefreshUI(state)).AddTo(disposable);
```

**Common Operators:** `.Subscribe()`, `.Select()`, `.Where()`, `.WithLatestFrom()`

**Subject Types:**
- `Subject<T>` — Manual push control (`stateSubject.OnNext(value)`)
- `ReactiveProperty<T>` — State variable observable
- `CompositeDisposable` — Batch subscription cleanup

---

## C# Naming & Code Conventions

**Enforced via .editorconfig:**
- Classes/Types: PascalCase (PlayingCard, IngamePresenter)
- Methods/Properties: PascalCase (ExecuteMove(), IsOpen)
- Private fields: camelCase (_history, cardData)
- Constants: PascalCase (MaxCardsPerPile)
- Interfaces: I prefix (IGameService, ICardService)

**File Organization:**
- One public class per file
- File name matches class name
- Namespace mirrors folder structure

**Code Style:**
- Indentation: 4 spaces
- Line endings: CRLF
- Charset: UTF-8
- Avoid 'this.' unless necessary
- Prefer auto-properties
- Encourage null coalescing (?? and ?.)
- Always parenthesize binary operators

**주석 원칙 — "코드가 곧 주석":**
- 주석은 **최소한**으로. 변수명과 함수명을 명료하게 작성해 코드 자체가 의도를 전달하도록 한다.
- 함수에는 **JSDoc 스타일 XML summary**만 간결하게: `<summary>`, `<param>`, `<returns>`. 한 줄이면 한 줄로.
- **한눈에 파악할 수 없는 복잡한 동작**만 인라인 주석으로 배경/이유를 설명. "무엇을 하는지"가 아니라 "왜 이렇게 하는지"를 적는다.
- 자명한 코드에 주석을 달지 않는다 (예: `// 리스트에 추가`, `// null 체크`).

---

## Testing Strategy

**Framework:** NUnit (Unity Test Framework)
**Location:** Assets/Tests/EditMode/

**Test Suites (~50 total; foundational examples):**
1. DealBuilderTests — Game deal generation
2. DeckFactoryTests — Deck initialization
3. GameCodeTests — Code generation/parsing
4. HintServiceTests — Hint move calculation
5. LifetimeStatsServiceTests — Persistent stats
6. SessionStatsServiceTests — Session scoring
7. ShuffleDistributionTests — Randomness validation
8. SolitaireCardServiceTests — Move validation

**Pattern:**
[TestFixture] class with [SetUp] and [Test] methods
Assert.IsTrue(), Assert.IsFalse(), Assert.AreEqual()

**Running Tests:**
uloop run-tests --test-mode EditMode

---

## Build & Compilation

**Assembly Definitions — dependency graph (verified from `.asmdef` references):**

The earlier `Data → Service` and `Core → Service`/`Data` inversions were removed (see `Docs/superpowers/plans/2026-05-29-assembly-layering-refactor.md`): `IDealRule` moved to `Model.Game`, and audio (`AudioSystem`/`AudioSourcePlayer`) moved out of `Core` into a dedicated `Audio` assembly. Because `Data` now depends only on `Model`, `Gateway`/`Service` can reference `Data` without a cycle — so the skin Addressable gateway lives in `Gateway` and the skin service in `Service` (no separate Skin assembly). The graph is a clean DAG with `Model`/`Core`/`Shared` at the base.

Project-layer dependencies (`A — B` means "A references B"):
- Model.asmdef — (no project deps)
- Shared.asmdef — (no project deps)
- Core.asmdef — (no project deps; `ComponentBase`, `SceneBase`)
- Data.asmdef — Model
- Gateway.asmdef — Model, Data  *(includes `Gateway.Skin` Addressable skin gateway)*
- Service.asmdef — Model, Gateway, Shared, Data  *(includes `Service.SkinService`)*
- Audio.asmdef — Core, Data, Service  *(AudioSystem/AudioSourcePlayer)*
- Component.asmdef — Core, Data, Service, Model, Shared
- Scene.asmdef — Component, Core, Data, Service, Gateway, Model, Shared, Audio
- App.asmdef — Service, Gateway, Data, Core, Model, Audio, Service.AchievementService.Google
- Editor.asmdef — Data, Scene (Editor-only platform)

Bottom→top order: **{Model, Core, Shared} → Data → Gateway → Service → {Audio, Component} → Scene/App.**

**Compilation:**
uloop compile [--force-recompile true] [--wait-for-domain-reload true]
Output: JSON with ErrorCount and WarningCount

> ⚠️ `uloop compile` / `run-tests` can report a **STALE** "ErrorCount: 0" / old test count when the
> editor hasn't actually recompiled — e.g. right after adding new `.cs` files, or when an assembly
> **cycle** blocks the whole build. If results look wrong, force a real rebuild:
> `uloop execute-dynamic-code --code "using UnityEditor; using UnityEditor.Compilation; AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); CompilationPipeline.RequestScriptCompilation(); return \"ok\";"`
> then poll `uloop compile` until it stops returning "Unity is compiling scripts" / "Domain Reload".

---

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| jp.hadashikick.vcontainer | Git | Dependency injection |
| com.cysharp.r3 | Git | Reactive Observables |
| com.cysharp.unitask | Git | Async/await for Unity |
| com.dbrizov.naughtyattributes | Git | Inspector extensions |
| com.unity.test-framework | 1.4.6 | NUnit runner |
| com.unity.textmeshpro | 3.0.9 | UI text |
| com.unity.addressables | 2.4.1 | Asset management |
| com.unity.localization | 1.5.2 | Multi-language |

---

## 외부 참조 (Unity Editor 관련 작업 시 필수)

Unity API · Inspector 커스터마이징 · Editor 자동화 · ScriptableObject Editor 등을 작성할 때는 추측하지 말고 다음 공식 레퍼런스를 **먼저** 확인한다.

### 1. UnityCsReference — Unity 엔진 C# 공식 소스
- **URL**: https://github.com/Unity-Technologies/UnityCsReference
- **용도**: UnityEngine / UnityEditor API의 정확한 시그니처 · 동작 · 내부 구현 확인. `docs.unity3d.com`이 모호하거나 최신 API에 대한 정보가 없을 때의 ground truth.
- **확인이 필요한 시점**:
  - Editor 스크립트 작성 (`[CustomEditor]`, `[CustomPropertyDrawer]`, `EditorWindow`)
  - `SerializedObject` / `SerializedProperty` 다루기
  - URP / Cinemachine 3.x / Input System / 2D 패키지의 내부 API 사용
  - Unity 6 변경된 API 확인 (예: `Rigidbody2D.velocity` → `linearVelocity`, Cinemachine 2.x → 3.x 마이그레이션 차이)
  - deprecated 경고가 뜨거나 새로 추가된 API의 정확한 동작이 의심될 때
- **읽는 방법**: 해당 클래스/API 이름으로 GitHub 검색 → `.cs` 소스 직접 확인.

### 2. NaughtyAttributes — Inspector 확장 (프로젝트에 import됨)
- **URL**: https://github.com/dbrizov/NaughtyAttributes
- **용도**: `[Button]`, `[ShowIf]` / `[HideIf]` / `[EnableIf]`, `[Required]`, `[Foldout]`, `[ValidateInput]`, `[OnValueChanged]` 등 attribute의 정확한 사용법·제약·조합 규칙.
- **확인이 필요한 시점**:
  - 인스펙터에 디버깅용 액션 버튼 노출 (`[Button]`)
  - 필드 조건부 표시 / 활성화 (`[ShowIf]` 등) — 조건 함수의 시그니처·인자 제약 확인
  - SerializeReference / nested ScriptableObject 등 NaughtyAttributes가 작동/미작동하는 경계 확인
  - 새 attribute 도입 전 README + 샘플 씬으로 동작 확인

---

## 참조 우선순위

1. 위 두 GitHub repo (정확성 최우선)
2. `docs.unity3d.com` 공식 매뉴얼 (보조)
3. 자체 추론 — 1·2에서 확인 안 되는 경우만. 비자명한 동작은 코드에 *왜* 그렇게 했는지 한 줄 주석으로 남긴다.

**검증 없이 Unity API를 호출하지 않는다.** 헛 짚으면 컴파일은 통과해도 런타임/Editor에서 실패하므로, 위 레퍼런스로 시그니처를 확인한 뒤 작성한다.

---

## Useful Commands

| Task | Command |
|------|---------|
| Compile & check | uloop compile |
| Force recompile | uloop compile --force-recompile |
| Run tests | uloop run-tests --test-mode EditMode |
| View logs | uloop get-logs |
| Get hierarchy | uloop get-hierarchy |
| Screenshot | uloop screenshot |
| Play/pause | uloop control-play-mode [play\|pause\|stop] |

---

## Development Best Practices

### Before Starting a Feature (도메인 모델 먼저)
1. 이 기능이 다루는 데이터를 식별한다.
2. 각 데이터의 invariant와 lifecycle을 정의한다.
3. Model 클래스를 작성한다 — readonly/init-only 프로퍼티, `IEquatable<T>` + `GetHashCode`, 변경은 With-패턴.
4. 누적·카운터·캐시가 필요하면 Service의 private 필드로 분리한다 (Model에 두지 않는다).
5. 위 4단계가 끝나야 Service/Presenter/Component 작성을 시작한다.

**체크리스트:**
- [ ] 모든 필드가 readonly 또는 init-only인가?
- [ ] `IEquatable<T>` + `GetHashCode` 구현했는가?
- [ ] 상태 변경 메서드가 새 인스턴스를 반환하는가?
- [ ] `Subject<T>`/`Observable<T>`로 노출하는 타입이 immutable Model인가?
- [ ] 누적·카운터 상태가 Model이 아니라 Service의 private 필드에 있는가?

### Add a New Service
1. Create `IMyService` in `Service/MyService/`
2. Create `MyService` implementation
3. Register in LifetimeScope: `builder.Register<MyService>(Lifetime.Singleton).As<IMyService>()`
4. Consume from a Service via constructor params (standard); from a Presenter/Component via `[Inject]` (field or property)

### Add a Scene
1. Create `Scene/MyScene/` folder
2. `MyScene.cs` extends `SceneBase` with `Configure()`
3. `MyPresenter.cs` implements `IStartable`, `ITickable`, `IDisposable`
4. `MyComponent.cs` extends `ComponentBase`
5. Link in `MyScene.Configure()`
6. Add the scene to Build Settings (else `RouteService` can't load it by name)

### Emit Component Event
```csharp
private Subject<MyEvent> myEventSubject = new();
public Observable<MyEvent> OnMyEventAsObservable() => myEventSubject;
// trigger:
myEventSubject.OnNext(new MyEvent(...));
```

### Add Unit Test
1. Create `Assets/Tests/EditMode/MyServiceTests.cs`
2. `[TestFixture] public class MyServiceTests`
3. `[SetUp] void SetUp() { ... }`
4. `[Test] void MyTest() { Assert.IsTrue(...); }`
5. `uloop run-tests --test-mode EditMode`

---

## Architecture Principles

1. **Inspector 바인딩 우선** — 유니티 인스펙터 바인딩(SerializeField, UnityEvent, ScriptableObject 등)으로 코드의 복잡도를 줄일 수 있으면 그 방법을 우선 선택한다. 코드로 해결하기 전에 Inspector에서 설정할 수 있는지 먼저 검토할 것.
2. **Prefab Variant 우선** — Scene 작업 시 반복되는 요소들은 먼저 base prefab으로 만들고, scene에 배치할 때는 variant prefab으로 작업하는 것을 우선한다. 예: `UIPlaceHolder`, `Lobby Grid Item`. 이를 통해 변경 사항을 한 곳에서 관리하고 일관성을 유지할 수 있다.
3. **Separation of Concerns** — Each layer single responsibility
4. **Dependency Inversion** — Depend on interfaces, not implementations
5. **Domain-Model-First** — 기능 작업 전 도메인 모델부터 설계 (Service/Presenter/Component는 모델 확정 후 작성). Domain logic in Model, UI in Component. 상세 절차는 `Development Best Practices > Before Starting a Feature` 절 참조.
6. **Observable Streams** — Unidirectional data flow via R3
7. **Immutable Model** — 모든 Model 클래스는 immutable이며 `IEquatable<T>`를 구현한다. 필드는 readonly 또는 init-only, 상태 변경은 With-패턴(`WithX(newValue) => new Self(...)`). Service 내부의 누적·캐시는 mutable private 필드로 허용한다 — 단, `Subject<T>`/`Observable<T>`/public API로 노출되는 타입은 항상 immutable Model.
8. **Interface-Based** — Services expose interfaces for testability
9. **Factory Pattern** — Rule factories (IDealRuleFactory) for strategy injection
10. **Async/Await** — Non-blocking operations via UniTask
11. **Root-cause fixes, not failovers** — 버그를 만나면 fallback / try-fallback / "graceful degradation" 같은 우회로를 쌓지 말고 **근본 원인**을 고친다. 인터페이스 계약(예: "always silent", "always completes")을 어기는 워크어라운드는 금지 — 새로운 의미가 필요하면 별도 메서드로 분리한다. 방어 코드는 명확히 경계가 정의된 한 곳의 try/catch에서만 허용한다.

---

## File Structure Summary

Assets/Scripts/
├── App/                    # Bootstrap (AppLifetimeScope, AppPresenter)
├── Model/                  # Domain models (PlayingCard, PileState, BoardState, etc.)
├── Data/                   # ScriptableObjects (CardSpriteSet, AudioDatabase)
├── Core/                   # Base classes ONLY (ComponentBase, SceneBase)
├── Service/                # Business logic (Game, Card, Hints, Stats, Skin, etc.)
├── Gateway/                # External integrations (Auth, Stats, Snapshot, Addressables)
├── Component/              # Reusable UI (UICard, UICardsController, etc.)
├── Scene/                  # MVP scenes (Ingame, Login, Lobby, Board)
├── Audio/                  # AudioSystem / AudioSourcePlayer (own asmdef)
├── Editor/                 # Editor utilities (Editor-only asmdef)
├── Shared/                 # Cross-layer utilities
├── Debug/                  # ⚠ no asmdef → Assembly-CSharp (debug overlay only)
└── Generated/              # ⚠ no asmdef → Assembly-CSharp (generated GPGS ids)

Assets/Tests/EditMode/      # NUnit test suites (~50 suites + test doubles)

---

## Important Notes

> Inspector-우선 / Prefab-Variant-우선 / Immutable-Model 원칙은 **Architecture Principles** 절이 정본(canonical). 여기서는 중복하지 않는다.

- Assembly definitions enforce layer isolation — changes may require updating asmdef references
- VContainer DI: **Services use constructor injection** (standard; a few legacy services — `UserService`, `*SnapshotService` — still use `[Inject]`). **Presenters and MonoBehaviour Components use `[Inject]` (field or property)**. VContainer supports both.
- R3 Observables are fundamental — understand `Subject<T>` pattern before adding services
- Scene unloading properly disposes `LifetimeScope` and `IDisposable` instances
- Always `.AddTo()` Observable subscriptions for automatic unsubscribe
- Add-on namespaces follow existing layer structure (e.g., `Service.NewDomain`)
- Services expose interfaces, implementations are internal details
- Presenters coordinate between Services and Components via Observables
- `NaughtyAttributes`의 `[Button]`, `[Required]` 등을 활용해 Inspector에서 디버깅·검증 가능하게 한다
