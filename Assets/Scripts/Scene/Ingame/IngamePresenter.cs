using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Component.Card.Events;
using Cysharp.Threading.Tasks;
using Data.Audio;
using Model.Achievement;
using Model.App;
using Gateway.Snapshot;
using Model.Card;
using Model.Game;
using Model.Stats;
using UnityEngine.Localization;
using R3;
using Scene.Board;
using Scene.Shared;
using Service.AchievementService;
using Service.AudioService;
using Service.CardService;
using Service.DailyService;
using Service.GameService;
using Service.HapticService;
using Service.LocalizationService;
using Scene.Ingame.View;
using Shared;
using Service.RouteService;
using Service.HintService;
using Service.StatsService;
using Service.SnapshotService;
using Service.SkinService;
using UnityEngine;
using UnityEngine.Networking;
using VContainer;
using VContainer.Unity;

namespace Scene.Ingame
{
    public class IngamePresenter : IStartable, ITickable, IDisposable
    {
        [Inject] private IngameComponent Component { get; set; }
        [Inject] private IngameShellView Shell { get; set; }
        [Inject] private IRouteService RouteService { get; set; }
        [Inject] private IDealRuleFactory DealRuleFactory { get; set; }
        [Inject] private ICardService CardService { get; set; }
        [Inject] private IGameService GameService { get; set; }
        [Inject] private IAudioService AudioService { get; set; }
        [Inject] private IHapticService HapticService { get; set; }
        [Inject] private ISessionStatsService SessionStats { get; set; }
        [Inject] private ILifetimeStatsService LifetimeStats { get; set; }
        [Inject] private IScoreRuleFactory ScoreRuleFactory { get; set; }
        [Inject] private IHintService HintService { get; set; }
        [Inject] private IGameSnapshotService SnapshotService { get; set; }
        [Inject] private IDailyStatsService DailyStats { get; set; }
        [Inject] private IAchievementService AchievementService { get; set; }
        [Inject] private ILocalizationService LocalizationService { get; set; }
        [Inject] private IAppConfig AppConfig { get; set; }
        [Inject] private ShuffleStrategyProvider ShuffleProvider { get; set; }
        [Inject] private ISkinService SkinService { get; set; }
        [Inject] private ISolvableSeedPrefetchService SeedPrefetch { get; set; }
        [Inject] private BoardViewSet BoardViews { get; set; }

        private ShellFlowController flow;
        private CompositeDisposable shellSubscriptions = new();
        private CompositeDisposable cardEventSubscriptions = new();
        private CompositeDisposable gameStateSubscriptions = new();
        private CancellationTokenSource initCts;
        private TableState prevState;
        private GameType currentGameType;
        private SnapshotKey currentSnapshotKey;
        private IReadOnlyList<HintMove> currentHints;
        private int hintIndex;
        private bool autoCompleteInProgress;
        private bool skipNextAnimation;
        private bool isActive;
        // Runs auto-collected during UpdateTableState (which fires before the triggering RecordMove).
        // Applied via AddScoreToLastMove after the move site calls RecordMove so they share one
        // undoable delta instead of inflating MoveCount.
        private int pendingCollectedRuns;

        /// <summary>Pinned UTC date for daily result attribution across midnight.</summary>
        private DateTime? dailyStartUtc;

        public void Start()
        {
            Component.SetCardService(CardService);

            flow = new ShellFlowController(
                Shell,
                AudioService,
                SessionStats,
                LifetimeStats,
                RouteService,
                LocalizationService,
                new ShellFlowCallbacks
                {
                    GetGameType = () => currentGameType,
                    CanUndo = () => GameService.CanUndo,
                    PerformUndo = () =>
                    {
                        AudioService.Play(AudioCatalog.Game.Undo);
                        GameService.Undo();
                        SessionStats.RecordMove(new ScoredMoveInfo(MoveType.Undo));
                    },
                    StartNewGame = () => StartNewGameAsync().Forget(),
                    StartRestart = () => StartRestartAsync().Forget(),
                    HandleHint = () => HandleHint(),
                    FlushSnapshot = () => SnapshotService.FlushAsync(),
                });

            // ReactiveProperty replays current value on subscribe, so the active skin is applied
            // before the first deal. Subsequent in-game skin changes re-skin live.
            SkinService.CurrentSpriteSet
                .Where(set => set != null)
                .Subscribe(set => Component.ApplySpriteSet(set))
                .AddTo(Component);

            RouteService.OnSamePathNavigated
                .Subscribe(_ => EvaluateOwnership())
                .AddTo(Shell);

            RefreshEvents();

            Shell.OnRefreshEventsObservable()
                .Subscribe(OnRefreshEvents)
                .AddTo(Shell);

            EvaluateOwnership();
        }

        private void EvaluateOwnership()
        {
            var query = new IngameQuery(RouteService.CurrentQuery);
            bool mine = !query.GameType.IsBoardMode();
            if (!mine) { Release(); return; }
            // Abandoning an in-progress game of our own (New Game / Restart re-entry) counts as a loss.
            // isActive-guarded so a cross-owner switch never abandons the OTHER presenter's session —
            // both presenters react to the same navigation event in arbitrary order.
            if (isActive && SessionStats.Current.MoveCount > 0 && !SessionStats.Current.IsFinished)
            {
                SessionStats.MarkLost();
                HandleGameEndAsync().Forget();
            }
            Claim();
            InitializeGameAsync().Forget();
        }

        private void Claim()
        {
            if (!isActive)
            {
                isActive = true;
                shellSubscriptions = new CompositeDisposable();
                flow.Wire(shellSubscriptions);
                WireOwnedShellEvents();
            }
            // Root switch: the claiming presenter sets the final root state.
            Shell.SetCardTableActive(true);
            foreach (var view in BoardViews.All) view.gameObject.SetActive(false);
        }

        private void Release()
        {
            if (!isActive) return;
            isActive = false;
            initCts?.Cancel();
            initCts?.Dispose();
            initCts = null;
            // Capture-then-detach: the snapshot is built synchronously before the claiming presenter
            // can re-initialize the shared SessionStats (lobby-exit semantics — the game stays resumable).
            SnapshotService.FlushAndStopAsync().Forget();
            gameStateSubscriptions.Dispose();
            gameStateSubscriptions = new CompositeDisposable();
            shellSubscriptions.Dispose();
            shellSubscriptions = new CompositeDisposable();
            Component.DespawnAllCards();
            // No SessionStats reset: the claiming presenter's init always Restores or Initializes
            // the shared session, and its abandon check is isActive-guarded — order-independent.
        }

        private void WireOwnedShellEvents()
        {
            // These subs fire against card-game state (currentGameType, GameService.CurrentSeed, etc.).
            // Moving them into shellSubscriptions (Claim/Release lifecycle) ensures they are only live
            // when this presenter owns the shell — the win panel is shared with board games, so a
            // permanently-wired OnShare would fire with stale card-game state during a board game.
            Shell.OnDailyCopyObservable()
                .Subscribe(_ =>
                {
                    var text = Shell.DailyShareText;
                    if (string.IsNullOrEmpty(text)) return;
                    Shell.CopyToClipboard(text);
                    flow.ShowLocalizedToastAsync(Shell.ToastCopied).Forget();
                    AudioService.Play(AudioCatalog.UI.Click);
                })
                .AddTo(shellSubscriptions);

            Shell.OnDailyTwitterObservable()
                .Subscribe(_ =>
                {
                    var text = Shell.DailyShareText;
                    if (string.IsNullOrEmpty(text)) return;
                    var url = $"https://twitter.com/intent/tweet?text={UnityWebRequest.EscapeURL(text)}";
                    Application.OpenURL(url);
                    AudioService.Play(AudioCatalog.UI.Click);
                })
                .AddTo(shellSubscriptions);

            Shell.OnDailyWinLobbyObservable()
                .Subscribe(_ =>
                {
                    AudioService.Play(AudioCatalog.UI.Click);
                    RouteService.GoBackAsync().Forget();
                })
                .AddTo(shellSubscriptions);

            // Retroactive unlocks (on Initialize) carry Retroactive=true and are suppressed here
            // to avoid toast-spam after app updates — users see them via the Lobby panel instead.
            AchievementService.OnAchievementUnlocked
                .Where(e => !e.Retroactive)
                .Subscribe(e =>
                {
                    var def = AchievementService.GetDefinition(e.Id);
                    if (def == null) return;
                    ShowAchievementUnlockedToastAsync(def).Forget();
                })
                .AddTo(shellSubscriptions);

            // Todo: separate share vs play-with-code into different UI flows to avoid code reuse awkwardness (e.g. showing code input error in share flow)
            Shell.OnShareObservable()
                .Subscribe(_ =>
                {
                    var code = GameCode.Encode(currentGameType, GameService.CurrentSeed.Value);
                    BuildAndCopyShareTextAsync(code, SessionStats.Current).Forget();
                })
                .AddTo(shellSubscriptions);
        }

        private async UniTaskVoid ShowAchievementUnlockedToastAsync(IAchievementDefinition def)
        {
            // Test stubs lack the concrete SO — fall back to a table lookup by TitleKey.
            var defAsset = def as Data.Achievement.AchievementDefinitionAsset;
            var titleText = defAsset != null
                ? await LocalizationService.GetStringAsync(defAsset.TitleEntry)
                : await LocalizationService.GetStringAsync("Achievements", def.TitleKey);
            var msg = await LocalizationService.GetStringAsync(Shell.ToastAchievementUnlocked, titleText);
            if (!string.IsNullOrEmpty(msg)) Shell.ShowToast(msg);
        }

        private async UniTaskVoid BuildAndCopyShareTextAsync(string gameCode, SessionStats stats)
        {
            var url = AppConfig != null ? AppConfig.ChallengePlayUrl : string.Empty;
            string shareText;
            if (!stats.IsWon)
            {
                shareText = await LocalizationService.GetStringAsync(
                    Shell.ChallengeLoseTemplate, gameCode, url);
            }
            else
            {
                var timeStr = TimeFormatHelper.Format(stats.ElapsedSeconds);
                shareText = await LocalizationService.GetStringAsync(
                    Shell.ChallengeWonTemplate, stats.Score, timeStr, stats.MoveCount, gameCode, url);
            }
            Shell.CopyToClipboard(shareText);
        }

        /// <summary>
        /// Records the finished session into LifetimeStats and evaluates session-scoped achievements
        /// (e.g. PerfectRun) once stats persistence completes. Snapshot captures post-MarkWon/Lost state.
        /// </summary>
        private async UniTaskVoid HandleGameEndAsync()
        {
            var sessionSnapshot = SessionStats.Current.Snapshot();
            try
            {
                await LifetimeStats.RecordGameResultAsync(currentGameType, sessionSnapshot);
                AchievementService?.EvaluateOnGameEnd(
                    currentGameType,
                    LifetimeStats.GetStats(currentGameType),
                    sessionSnapshot);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Run the cascade first so the foundation cards visibly fly off-screen before the win
        // panel covers the board. Stats / snapshot / audio / haptic already fired inline at the
        // call site; only the panel display is gated on the celebration.
        private async UniTaskVoid PlayWinCelebrationAsync(IngameQuery query, CancellationToken ct)
        {
            try
            {
                await UniTask.WhenAll(Component.PlayWinCascadeAsync(ct), Shell.PlayWinEffectAsync(ct));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (ct.IsCancellationRequested) return;

            if (query.IsDaily)
            {
                HandleDailyWinAsync().Forget();
                return;
            }

            var gameCode = GameCode.Encode(currentGameType, GameService.CurrentSeed.Value);
            Shell.ShowWinPanel(
                SessionStats.Current.Score,
                SessionStats.Current.MoveCount,
                SessionStats.Current.ElapsedSeconds,
                gameCode);
            Shell.TriggerWin();
        }

        private async UniTaskVoid HandleDailyWinAsync()
        {
            try
            {
                var recordDate = dailyStartUtc ?? DateTime.UtcNow;
                await DailyStats.RecordResultAsync(recordDate, won: true, SessionStats.Current);

                var stats = SessionStats.Current;
                var streak = DailyStats.Stats.CurrentStreak;
                var url = AppConfig != null ? AppConfig.DailyPlayUrl : string.Empty;
                // Positional {0..5}: date, time, score, moves, streak, url.
                var shareText = await LocalizationService.GetStringAsync(
                    Shell.DailyShareTemplate,
                    DailySeed.DateKey(recordDate),
                    TimeFormatHelper.Format(stats.ElapsedSeconds),
                    stats.Score, stats.MoveCount, streak, url);

                Shell.ShowDailyWinPanel(
                    stats.Score, stats.MoveCount, stats.ElapsedSeconds, streak, shareText,
                    DailySeed.DateKey(recordDate));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async UniTaskVoid StartNewGameAsync()
        {
            try
            {
                var query = new IngameQuery(RouteService.CurrentQuery);
                string mode = query.IsDaily ? GameRouteParams.ModeDaily : null;
                var key = new SnapshotKey(query.GameType, query.Variant ?? 1, mode);
                await SnapshotService.ClearSnapshotAsync(key);

                var variantStr = (query.Variant ?? 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                var newQuery = new Dictionary<string, string>
                {
                    { GameRouteParams.GameType, query.GameType.ToString() },
                    { GameRouteParams.Variant, variantStr },
                    { GameRouteParams.DrawCount, variantStr },
                };
                if (query.IsDaily) newQuery[GameRouteParams.Mode] = GameRouteParams.ModeDaily;
                await RouteService.NavigateAsync("Ingame", newQuery);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async UniTaskVoid StartRestartAsync()
        {
            try
            {
                var currentSeed = GameService.CurrentSeed;
                if (!currentSeed.HasValue)
                {
                    Debug.LogWarning("[IngamePresenter] Restart requested but no current seed — falling back to new game.");
                    StartNewGameAsync().Forget();
                    return;
                }

                var query = new IngameQuery(RouteService.CurrentQuery);
                string mode = query.IsDaily ? GameRouteParams.ModeDaily : null;
                var key = new SnapshotKey(query.GameType, query.Variant ?? 1, mode);

                await SnapshotService.ClearSnapshotAsync(key);

                var variantString = (query.Variant ?? 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                var newQuery = new Dictionary<string, string>
                {
                    { GameRouteParams.GameType, query.GameType.ToString() },
                    { GameRouteParams.Variant,  variantString },
                    { GameRouteParams.DrawCount, variantString },
                };
                if (query.IsDaily)
                {
                    newQuery[GameRouteParams.Mode] = GameRouteParams.ModeDaily;
                }
                else
                {
                    newQuery[GameRouteParams.Seed] = currentSeed.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                }
                await RouteService.NavigateAsync("Ingame", newQuery);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async UniTaskVoid InitializeGameAsync(bool skipSnapshot = false)
        {
            initCts?.Cancel();
            initCts?.Dispose();
            initCts = new CancellationTokenSource();
            var ct = initCts.Token;

            try
            {
                SnapshotService.StopAutoSave();

                gameStateSubscriptions.Dispose();
                gameStateSubscriptions = new CompositeDisposable();
                gameStateSubscriptions.AddTo(Component);

                var query = new IngameQuery(RouteService.CurrentQuery);
                currentGameType = query.GameType;
                Component.ActivateLayout(currentGameType);

                // All paths use FisherYates; daily v2 derives its seed via DailySeedResolverV2
                // (solver-resolved chain) so every client gets the same proven-winnable deal.
                ShuffleProvider.Current = new FisherYatesShuffleStrategy();

                int? effectiveSeed = query.Seed;
                string mode = query.IsDaily ? GameRouteParams.ModeDaily : null;
                if (query.IsDaily)
                {
                    dailyStartUtc = DateTime.UtcNow;
                }
                else
                {
                    dailyStartUtc = null;
                }

                var requestedVariant = query.Variant ?? 1;

                // Daily v2 (Klondike draw-1): resolve the frozen contract seed on the thread
                // pool before the stale-check so the snapshot comparison uses the same resolved
                // seed the deal will use.
                if (query.IsDaily && query.GameType == GameType.Klondike)
                {
                    var capturedDate = dailyStartUtc.Value;
                    effectiveSeed = await flow.ResolveWithLoadingAsync(
                        c => SolverScheduler.ResolveDailyKlondikeDrawOneAsync(capturedDate, c), ct);
                    Debug.Log($"[DailyV2] date={DailySeed.DateKey(capturedDate)} resolved={effectiveSeed}");
                }

                if (ct.IsCancellationRequested) return;

                var snapshotKey = new SnapshotKey(query.GameType, requestedVariant, mode);
                GameSnapshot snapshot = null;
                bool canLoadSnapshot = !skipSnapshot && (query.IsContinue || query.IsDaily) && query.Seed == null;
                if (canLoadSnapshot)
                    snapshot = await SnapshotService.LoadSnapshotAsync(snapshotKey);

                if (ct.IsCancellationRequested) return;

                if (query.IsDaily && snapshot != null && effectiveSeed.HasValue && snapshot.Seed != effectiveSeed.Value)
                {
                    Debug.Log($"[Daily] Dropping stale snapshot (seed {snapshot.Seed} != today {effectiveSeed.Value}).");
                    await SnapshotService.ClearSnapshotAsync(snapshotKey);
                    snapshot = null;
                }

                var scoreRule = ScoreRuleFactory.Create(currentGameType);
                var effectiveVariant = snapshot?.DrawCount ?? requestedVariant;
                var dealRule = DealRuleFactory.Create(query.GameType, effectiveVariant);
                currentSnapshotKey = new SnapshotKey(query.GameType, effectiveVariant, mode);

                Debug.Log(snapshot != null
                    ? $"RestoreGame. Key={currentSnapshotKey}, Seed={snapshot.Seed}"
                    : $"InitializeGame. Key={currentSnapshotKey}, Seed={effectiveSeed?.ToString() ?? "random"}");

                Shell.HideWinPanel();
                Shell.HideDailyWinPanel();
                Shell.HideStatsPanel();
                Shell.ResetHud();

                if (snapshot != null)
                {
                    var restoredState = GameSnapshotConverter.ToTableState(snapshot.CurrentState);
                    var restoredHistory = GameSnapshotConverter.ToHistory(snapshot.UndoHistory);
                    var restoredStats = GameSnapshotConverter.ToSessionStats(snapshot.Stats);
                    GameService.Restore(dealRule, snapshot.Seed, restoredState, restoredHistory);
                    SessionStats.Restore(scoreRule, restoredStats);
                }
                else
                {
                    int? dealSeed = effectiveSeed;

                    // Fresh non-Daily Klondike/Easthaven deals route through the solver-verified
                    // resolver so the played seed is (almost always) proven winnable.
                    // Seeded entries (GameCode share / restart) skip it: their seed is
                    // already a resolved one, and replays must reproduce it exactly.
                    bool resolveSolvableSeed = !query.IsDaily
                        && (query.GameType == GameType.Klondike || query.GameType == GameType.Easthaven)
                        && (query.Seed == null);
                    if (resolveSolvableSeed)
                    {
                        int? prefetched = SeedPrefetch.TryConsume(query.GameType, effectiveVariant);
                        if (prefetched.HasValue)
                        {
                            dealSeed = prefetched.Value;
                        }
                        else
                        {
                            int inputSeed = DeckFactory.CreateRandomSeed();
                            dealSeed = await flow.ResolveWithLoadingAsync(
                                async c => (await SolverScheduler.ResolveAsync(inputSeed, dealRule, c)).Seed, ct);
                            Debug.Log($"[SolvableSeed] input={inputSeed} resolved={dealSeed}");
                        }
                    }

                    GameService.Initialize(dealRule, dealSeed);
                    SessionStats.Initialize(scoreRule);

                    if (resolveSolvableSeed)
                        SeedPrefetch.Prefetch(query.GameType, effectiveVariant, dealRule);
                }

                CardService.Initialize(dealRule);
                HintService.Initialize(dealRule);
                currentHints = null;
                hintIndex = 0;
                autoCompleteInProgress = false;
                skipNextAnimation = false;
                pendingCollectedRuns = 0; // never carry a prior game's undrained collection bonus in

                prevState = null;
                Component.DespawnAllCards();
                RenderAllPiles(GameService.CurrentState);

                GameService.OnTableStateChanged
                    .Subscribe(UpdateTableState)
                    .AddTo(gameStateSubscriptions);

                SnapshotService.StartAutoSave(currentSnapshotKey, GameService.CurrentSeed.Value,
                    GameService, SessionStats);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void RenderAllPiles(TableState state)
        {
            RenderPile(state.Stock);
            RenderPile(state.Waste, wasteFanCount: state.WasteFanCount);
            foreach (var foundation in state.Foundations) RenderPile(foundation);
            foreach (var tableau in state.Tableaus) RenderPile(tableau);
            prevState = state;
        }

        private void UpdateTableState(TableState next)
        {
            if (prevState == null)
            {
                Component.DespawnAllCards();
                RenderAllPiles(next);
                return;
            }

            Component.MoveAnimator?.CancelAll();

            var animDataList = skipNextAnimation ? null : DetectMoveAnimation(prevState, next);
            skipNextAnimation = false;

            // A collection transition is compound (trigger move + 13-card run removal):
            // DetectMoveAnimation mis-attributes the source, so render it without fly animations.
            if (animDataList != null && GameService.DealRule.AutoCollectCompletedRuns
                && next.Foundations.Sum(f => f.Cards.Count)
                    - prevState.Foundations.Sum(f => f.Cards.Count) >= 13)
            {
                animDataList = null;
            }

            DiffAndRenderPile(prevState.Stock, next.Stock);
            DiffAndRenderPile(prevState.Waste, next.Waste, next.WasteFanCount);
            for (int i = 0; i < next.Foundations.Count; i++)
                DiffAndRenderPile(prevState.Foundations[i], next.Foundations[i]);
            for (int i = 0; i < next.Tableaus.Count; i++)
                DiffAndRenderPile(prevState.Tableaus[i], next.Tableaus[i]);

            if (animDataList is { Count: > 0 } && Component.MoveAnimator != null)
            {
                foreach (var anim in animDataList)
                {
                    var targetCard = Component.FindCard(anim.TargetPileId, anim.TargetIndex);
                    targetCard?.SetVisible(false);

                    var toWorld = Component.GetCardWorldPosition(anim.TargetPileId, anim.TargetIndex);
                    Component.MoveAnimator.AnimateMove(anim.Card, anim.FaceUp, anim.FromWorld, toWorld,
                        () => targetCard?.SetVisible(true));
                }
            }

            // Auto-collected runs are a side effect of the triggering move.  Detect them here (where
            // the state diff is available) but do NOT call RecordMove — that would inflate MoveCount.
            // Instead, store the count and fold their score into the triggering move's undoable delta
            // via ApplyPendingCollectionScore(), which each move site calls after its own RecordMove.
            if (GameService.DealRule.AutoCollectCompletedRuns && prevState != null)
            {
                int prevFoundation = prevState.Foundations.Sum(f => f.Cards.Count);
                int nextFoundation = next.Foundations.Sum(f => f.Cards.Count);
                int runs = 0;
                for (int gained = nextFoundation - prevFoundation; gained >= 13; gained -= 13)
                {
                    AudioService.Play(AudioCatalog.Card.FoundationPlace);
                    runs++;
                }
                pendingCollectedRuns += runs;
            }

            prevState = next;
            Component.ClearHintHighlight();
            currentHints = null;
            hintIndex = 0;
            Component.SetStockRestoreVisible(next.Stock.Cards.Count == 0);
            if (GameService.IsWon(next) && !SessionStats.Current.IsFinished)
            {
                SessionStats.MarkWon();
                Shell.SetInputBlocker(true);
                HandleGameEndAsync().Forget();
                SnapshotService.ClearSnapshotAsync(currentSnapshotKey).Forget();
                HapticService.Trigger(HapticTier.Heavy);
                AudioService.Play(AudioCatalog.Game.Win);

                var query = new IngameQuery(RouteService.CurrentQuery);
                var ct = initCts?.Token ?? Shell.destroyCancellationToken;
                PlayWinCelebrationAsync(query, ct).Forget();
                return;
            }

            if (!SessionStats.Current.IsFinished && !autoCompleteInProgress)
            {
                if (HintService.CanAutoComplete(next))
                {
                    RunAutoCompleteAsync().Forget();
                }
                else if (!HintService.HasAnyMove(next))
                {
                    AudioService.Play(AudioCatalog.Game.Stuck);
                    Shell.ShowStuckPanel(GameService.CanUndo);
                }
            }
        }

        private void DiffAndRenderPile(PileState prev, PileState next, int wasteFanCount = 0)
        {
            if (prev.Equals(next)) return;
            Component.DespawnPile(next.Id);

            bool animateReveal = IsReveal(prev, next);

            if (animateReveal)
                AudioService.Play(AudioCatalog.Card.Flip);

            RenderPile(next, animateReveal, wasteFanCount);
        }

        private const float WasteFanOffsetX = 30f;

        private void RenderPile(PileState pile, bool animateNewlyRevealedTop = false, int wasteFanCount = 0)
        {
            for (int i = 0; i < pile.Cards.Count; i++)
            {
                var uiCard = Component.SpawnCard(pile.Cards[i], pile.Id, i);
                bool isTop = i == pile.Cards.Count - 1;

                if (pile.Id.Type == PileType.Waste && wasteFanCount > 1)
                {
                    int fanStart = pile.Cards.Count - wasteFanCount;
                    if (i >= fanStart)
                    {
                        int fanIdx = i - fanStart;
                        float x = (fanIdx - (wasteFanCount - 1)) * WasteFanOffsetX;
                        uiCard.rectTransform.anchoredPosition = new Vector2(x, 0);
                    }
                    else
                    {
                        uiCard.rectTransform.anchoredPosition = Vector2.zero;
                    }
                }

                if (pile.IsFaceUp(i))
                {
                    if (animateNewlyRevealedTop && isTop)
                        uiCard.Open();
                    else
                        uiCard.OpenImmediate();
                }
                else
                {
                    uiCard.Close();
                }

                bool shouldEnable = pile.Id.Type == PileType.Tableau ? pile.IsFaceUp(i) : isTop;
                if (shouldEnable) uiCard.Enable(); else uiCard.Disable();

                uiCard.IsDraggable = pile.Id.Type != PileType.Stock;
            }
        }

        public void RefreshEvents()
        {
            cardEventSubscriptions.Dispose();
            cardEventSubscriptions = new();
            cardEventSubscriptions.AddTo(Component);

            Debug.Log("Refreshing event subscriptions...");
            Component.OnCardDragStartedAsObservable()
                .Subscribe(OnCardDragStarted)
                .AddTo(cardEventSubscriptions);

            Component.OnCardDragCanceledAsObservable()
                .Subscribe(OnCardDragCanceled)
                .AddTo(cardEventSubscriptions);

            Component.OnCardDroppedOnPileAsObservable()
                .Subscribe(OnCardDroppedOnPile)
                .AddTo(cardEventSubscriptions);

            Component.OnCardClickedAsObservable()
                .Subscribe(OnCardClicked)
                .AddTo(cardEventSubscriptions);

            Component.OnPlaceHolderClickedAsObservable()
                .Subscribe(OnPlaceHolderClicked)
                .AddTo(cardEventSubscriptions);
        }

        private void OnRefreshEvents(Unit unit)
        {
            RefreshEvents();
        }

        private void OnPlaceHolderClicked(PileId pileId)
        {
            if (pileId.Type == PileType.Stock)
                HandleStockDraw();
        }

        private void OnCardClicked(CardClicked clicked)
        {
            if (SessionStats.Current.IsFinished || autoCompleteInProgress) return;

            if (clicked.SourcePileId.Type == PileType.Stock)
            {
                HandleStockDraw();
                return;
            }

            var pile = GetPileFromState(GameService.CurrentState, clicked.SourcePileId);
            if (pile == null || !pile.IsFaceUp(clicked.SourceIndex)) return;

            if (!TryExecuteTapMove(clicked))
            {
                AudioService.Play(AudioCatalog.Game.NoHint);
                HapticService.Trigger(HapticTier.Selection);
                var uiCard = Component.FindCard(clicked.SourcePileId, clicked.SourceIndex);
                if (uiCard != null)
                    Component.MoveAnimator?.AnimateRejectShake(uiCard.rectTransform);
            }
        }

        private void HandleStockDraw()
        {
            // Spider: an illegal tableau deal (empty column / exhausted stock) must give the
            // same reject feedback as an illegal move — the service guard alone is a silent no-op.
            if (GameService.DealRule.StockDealsToTableau && !GameService.CanDealStock)
            {
                AudioService.Play(AudioCatalog.Card.MoveRejected);
                HapticService.Trigger(HapticTier.Selection);
                return;
            }

            var stock = GameService.CurrentState.Stock;
            var waste = GameService.CurrentState.Waste;
            var isRecycle = stock.Cards.Count == 0;
            if (isRecycle && (!GameService.DealRule.CanRecycleStock || waste.Cards.Count == 0)) return;

            GameService.DrawFromStock();

            if (isRecycle)
            {
                SessionStats.RecordMove(new ScoredMoveInfo(MoveType.StockRecycle));
                AudioService.Play(AudioCatalog.Card.Refresh);
            }
            else
            {
                SessionStats.RecordMove(new ScoredMoveInfo(MoveType.StockDraw));
                AudioService.Play(AudioCatalog.Card.Flip);
            }
            ApplyPendingCollectionScore();
        }

        public void Tick()
        {
            if (!isActive) return;
            SessionStats.Tick(Time.unscaledDeltaTime);
        }

        public void Dispose()
        {
            initCts?.Cancel();
            initCts?.Dispose();
            SnapshotService.StopAutoSave();
            shellSubscriptions.Dispose();
            gameStateSubscriptions.Dispose();
            cardEventSubscriptions.Dispose();
        }

        private void HandleHint()
        {
            if (SessionStats.Current.IsFinished || autoCompleteInProgress) return;

            Component.MoveAnimator?.CancelAll();
            Component.ClearHintHighlight();

            if (currentHints == null || hintIndex >= currentHints.Count)
            {
                currentHints = HintService.GetHints(GameService.CurrentState);
                hintIndex = 0;
            }

            if (currentHints.Count == 0)
            {
                AudioService.Play(AudioCatalog.Game.NoHint);
                return;
            }

            var hint = currentHints[hintIndex];
            hintIndex++;
            SessionStats.RecordHintUsed();
            AudioService.Play(AudioCatalog.Game.Hint);

            PileId sourcePileId;
            int sourceIndex;
            PileId targetPileId;

            if (hint.MoveType == HintMoveType.StockDraw)
            {
                var stockCount = GameService.CurrentState.Stock.Cards.Count;
                var wasteCount = GameService.CurrentState.Waste.Cards.Count;
                if (stockCount == 0)
                {
                    // Recycle: waste cards return to stock
                    sourcePileId = new PileId(PileType.Waste, 0);
                    sourceIndex = Mathf.Max(0, wasteCount - 1);
                    targetPileId = new PileId(PileType.Stock, 0);
                }
                else
                {
                    // Draw: top stock card dealt to waste
                    sourcePileId = new PileId(PileType.Stock, 0);
                    sourceIndex = stockCount - 1;
                    targetPileId = new PileId(PileType.Waste, 0);
                }
            }
            else
            {
                sourcePileId = hint.Request.SourcePileId;
                sourceIndex = hint.Request.SourceIndex;
                targetPileId = hint.Request.TargetPileId;
            }

            Component.ShowHintHighlight(sourcePileId, sourceIndex, targetPileId);

            if (Component.MoveAnimator != null)
            {
                var state = GameService.CurrentState;
                var sourcePile = GetPileFromState(state, sourcePileId);
                if (sourcePile?.Cards != null && sourceIndex < sourcePile.Cards.Count)
                {
                    var card = sourcePile.Cards[sourceIndex];
                    bool faceUp = sourcePile.IsFaceUp(sourceIndex);
                    var sourceWorld = Component.GetCardWorldPosition(sourcePileId, sourceIndex);

                    var targetPile = GetPileFromState(state, targetPileId);
                    int targetIdx = targetPile?.Cards?.Count ?? 0;
                    var targetWorld = Component.GetCardWorldPosition(targetPileId, targetIdx);

                    Component.MoveAnimator.AnimateHintPreview(card, faceUp, sourceWorld, targetWorld);
                }
            }
        }

        private async UniTaskVoid RunAutoCompleteAsync()
        {
            if (autoCompleteInProgress || SessionStats.Current.IsFinished) return;

            try
            {
                autoCompleteInProgress = true;
                AudioService.PlayMusic(AudioCatalog.Music.AutoComplete);
                Shell.SetInputBlocker(true);

                // initCts so a re-init or cross-owner Release stops the loop — it executes moves and
                // records them into the SHARED SessionStats, which the next owner re-initializes.
                var ct = initCts?.Token ?? Component.destroyCancellationToken;
                while (true)
                {
                    if (GameService.IsWon(GameService.CurrentState)) break;

                    var moves = HintService.GetAutoCompleteMoves(GameService.CurrentState);
                    if (moves.Count == 0) break;

                    var move = moves[0];
                    var validation = CardService.TryMove(move.Request, GameService.CurrentState);
                    if (!validation.IsSuccess) break;

                    AudioService.Play(AudioCatalog.Card.FoundationPlace, move.Request.TargetPileId.Index);
                    GameService.ExecuteMove(move.Request);
                    SessionStats.RecordMove(new ScoredMoveInfo(
                        MoveType.CardMove,
                        move.Request.SourcePileId.Type,
                        move.Request.TargetPileId.Type));
                    // Drain pending collection onto this move too, so an auto-collecting game
                    // (none today: collect-games disable auto-complete) can't strand a bonus.
                    ApplyPendingCollectionScore();
                    await UniTask.Delay(150, cancellationToken: ct);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                AudioService.StopMusic();
                autoCompleteInProgress = false;
                // Win paths keep the blocker on until the panel is dismissed.
                if (Shell != null && !GameService.IsWon(GameService.CurrentState))
                    Shell.SetInputBlocker(false);
            }
        }

        private void OnCardDragStarted(CardDragStarted dragStarted)
        {
            if (SessionStats.Current.IsFinished || autoCompleteInProgress) return;
            Debug.Log($"Card drag started: {dragStarted.Card} from pile {dragStarted.SourcePileId} at index {dragStarted.SourceIndex}");
            Component.MoveAnimator?.CancelAll();
            Component.ClearHintHighlight();
            AudioService.Play(AudioCatalog.Card.DragStart);
            HapticService.Trigger(HapticTier.Light);
        }

        private void OnCardDragCanceled(CardDragCanceled dragCanceled)
        {
            Debug.Log($"Card drag canceled: {dragCanceled.Card} from pile {dragCanceled.SourcePileId} at index {dragCanceled.SourceIndex}");
            AudioService.Play(AudioCatalog.Card.DragCancel);
            Component.RevertCardDrop(dragCanceled.CardView);
        }

        private void OnCardDroppedOnPile(CardDroppedOnPile droppedOnPile)
        {
            if (SessionStats.Current.IsFinished || autoCompleteInProgress)
            {
                Component.RevertCardDrop(droppedOnPile.CardView);
                return;
            }
            Debug.Log($"Card dropped on pile: {droppedOnPile.Card} from pile {droppedOnPile.SourcePileId} at index {droppedOnPile.SourceIndex} to pile {droppedOnPile.TargetPileId}");
            var request = new MoveCardRequest(
                droppedOnPile.Card,
                droppedOnPile.SourcePileId,
                droppedOnPile.SourceIndex,
                droppedOnPile.TargetPileId,
                droppedOnPile.Count);

            // [Gateway] 서버 전환 시 아래 두 호출이 await gateway.ExecuteMoveAsync(request) 하나로 통합됨.
            // 서버가 검증 + 실행 + 새 TableState 반환을 모두 처리.
            var validation = CardService.TryMove(request, GameService.CurrentState);
            if (!validation.IsSuccess)
            {
                Debug.Log($"Card move rejected: {validation.Message}");
                AudioService.Play(AudioCatalog.Card.MoveRejected);
                HapticService.Trigger(HapticTier.Selection);
                Component.RevertCardDrop(droppedOnPile.CardView);
                return;
            }

            if (droppedOnPile.TargetPileId.Type == PileType.Foundation)
            {
                AudioService.Play(AudioCatalog.Card.FoundationPlace, droppedOnPile.TargetPileId.Index);
                HapticService.Trigger(HapticTier.Medium);
            }
            else
            {
                AudioService.Play(AudioCatalog.Card.Place);
            }

            skipNextAnimation = true; // Drag already moved the card visually
            var stateBefore = GameService.CurrentState;
            GameService.ExecuteMove(request);

            var causedReveal = DetectTableauReveal(stateBefore, GameService.CurrentState, request.SourcePileId);
            SessionStats.RecordMove(new ScoredMoveInfo(
                MoveType.CardMove,
                request.SourcePileId.Type,
                request.TargetPileId.Type,
                causedReveal));
            ApplyPendingCollectionScore();
            // Success: OnTableStateChanged → RenderTableState (이미 구독됨)
        }

        /// <summary>Folds score for any auto-collected runs (detected in UpdateTableState) into the
        /// most recently recorded move. Must be called immediately after each RecordMove at a move site.
        /// One run = one TableauToFoundation score unit; no extra MoveCount increment.</summary>
        private void ApplyPendingCollectionScore()
        {
            if (pendingCollectedRuns <= 0) return;
            int runs = pendingCollectedRuns;
            pendingCollectedRuns = 0;

            // Reuse the same per-run value as CalculateCardMoveScore(Tableau, Foundation) so the
            // score for a collected run is identical to manually placing a card on a foundation.
            var rule = ScoreRuleFactory.Create(currentGameType);
            int delta = rule.TableauToFoundation * runs;
            if (delta != 0) SessionStats.AddScoreToLastMove(delta);
        }

        private bool DetectTableauReveal(TableState before, TableState after, PileId sourcePileId)
        {
            if (sourcePileId.Type != PileType.Tableau) return false;
            var prev = GetPileFromState(before, sourcePileId);
            var next = GetPileFromState(after, sourcePileId);
            if (prev == null || next == null) return false;
            return IsReveal(prev, next);
        }

        private static bool IsReveal(PileState prev, PileState next)
        {
            return next.Cards.Count > 0
                && next.Cards.Count < prev.Cards.Count
                && !prev.IsFaceUp(next.Cards.Count - 1)
                && next.IsFaceUp(next.Cards.Count - 1);
        }

        private static PileState GetPileFromState(TableState state, PileId pileId)
        {
            return pileId.Type switch
            {
                PileType.Stock => state.Stock,
                PileType.Waste => state.Waste,
                PileType.Foundation => state.Foundations.FirstOrDefault(p => p.Id.Equals(pileId)),
                PileType.Tableau => state.Tableaus.FirstOrDefault(p => p.Id.Equals(pileId)),
                _ => null
            };
        }

        // ─── Move Animation ─────────────────────────────────────────

        private readonly struct MoveAnimationData
        {
            public PlayingCard Card { get; }
            public bool FaceUp { get; }
            public Vector3 FromWorld { get; }
            public PileId TargetPileId { get; }
            public int TargetIndex { get; }

            public MoveAnimationData(PlayingCard card, bool faceUp, Vector3 fromWorld, PileId targetPileId, int targetIndex)
            {
                Card = card;
                FaceUp = faceUp;
                FromWorld = fromWorld;
                TargetPileId = targetPileId;
                TargetIndex = targetIndex;
            }
        }

        private List<MoveAnimationData> DetectMoveAnimation(TableState prev, TableState next)
        {
            var result = new List<MoveAnimationData>();

            PileState sourcePrev = default;
            int sourceLoss = 0;
            var targets = new List<PileState>();

            void Check(PileState p, PileState n)
            {
                if (p == null || n == null || p.Cards == null || n.Cards == null) return;
                int diff = n.Cards.Count - p.Cards.Count;
                if (diff < 0 && sourcePrev == null)
                {
                    sourcePrev = p;
                    sourceLoss = -diff;
                }
                else if (diff > 0)
                {
                    targets.Add(n);
                }
            }

            Check(prev.Stock, next.Stock);
            Check(prev.Waste, next.Waste);
            for (int i = 0; i < prev.Foundations.Count && i < next.Foundations.Count; i++)
                Check(prev.Foundations[i], next.Foundations[i]);
            for (int i = 0; i < prev.Tableaus.Count && i < next.Tableaus.Count; i++)
                Check(prev.Tableaus[i], next.Tableaus[i]);

            if (sourcePrev == null || targets.Count == 0 || sourceLoss <= 0)
                return result;

            // Suppress Stock <-> Waste animation (Klondike draw / recycle are handled by the deck visual).
            if (targets.Count == 1 &&
                ((sourcePrev.Id.Type == PileType.Stock && targets[0].Id.Type == PileType.Waste) ||
                 (sourcePrev.Id.Type == PileType.Waste && targets[0].Id.Type == PileType.Stock)))
                return result;

            if (targets.Count == 1)
            {
                // Contiguous slice move: top `sourceLoss` cards from source land at the end of the target.
                var target = targets[0];
                int sourceIndex = sourcePrev.Cards.Count - sourceLoss;
                int targetIndex = target.Cards.Count - sourceLoss;
                for (int j = 0; j < sourceLoss; j++)
                {
                    int si = sourceIndex + j;
                    var card = sourcePrev.Cards[si];
                    bool faceUp = sourcePrev.IsFaceUp(si);
                    var fromWorld = Component.GetCardWorldPosition(sourcePrev.Id, si);
                    result.Add(new MoveAnimationData(card, faceUp, fromWorld, target.Id, targetIndex + j));
                }
            }
            else
            {
                // Single source distributed to multiple targets (EastHaven Stock deal):
                // SolitaireGameService.DealStockToTableaus pops from source top into targets[0..n-1]
                // in order, so prev.Stock[count-1-i] is the card that ended up at targets[i].
                int maxN = Math.Min(sourceLoss, targets.Count);
                for (int i = 0; i < maxN; i++)
                {
                    int si = sourcePrev.Cards.Count - 1 - i;
                    var card = sourcePrev.Cards[si];
                    bool faceUp = sourcePrev.IsFaceUp(si);
                    var fromWorld = Component.GetCardWorldPosition(sourcePrev.Id, si);
                    var target = targets[i];
                    int targetIndex = target.Cards.Count - 1;
                    result.Add(new MoveAnimationData(card, faceUp, fromWorld, target.Id, targetIndex));
                }
            }

            return result;
        }

        // ─── Tap-to-Move ────────────────────────────────────────────

        private bool TryExecuteTapMove(CardClicked clicked)
        {
            var state = GameService.CurrentState;
            var hints = HintService.GetHints(state);

            foreach (var hint in hints)
            {
                if (hint.MoveType == HintMoveType.StockDraw) continue;
                if (!hint.Request.SourcePileId.Equals(clicked.SourcePileId)) continue;
                if (hint.Request.SourceIndex != clicked.SourceIndex) continue;

                return ExecuteHintMove(hint);
            }

            return false;
        }

        private bool ExecuteHintMove(HintMove hint)
        {
            if (hint.MoveType == HintMoveType.StockDraw)
            {
                HandleStockDraw();
                return true;
            }

            var validation = CardService.TryMove(hint.Request, GameService.CurrentState);
            if (!validation.IsSuccess) return false;

            if (hint.Request.TargetPileId.Type == PileType.Foundation)
            {
                AudioService.Play(AudioCatalog.Card.FoundationPlace, hint.Request.TargetPileId.Index);
                HapticService.Trigger(HapticTier.Medium);
            }
            else
            {
                AudioService.Play(AudioCatalog.Card.Place);
            }

            var stateBefore = GameService.CurrentState;
            GameService.ExecuteMove(hint.Request);

            var causedReveal = DetectTableauReveal(stateBefore, GameService.CurrentState, hint.Request.SourcePileId);
            SessionStats.RecordMove(new ScoredMoveInfo(
                MoveType.CardMove,
                hint.Request.SourcePileId.Type,
                hint.Request.TargetPileId.Type,
                causedReveal));
            ApplyPendingCollectionScore();
            return true;
        }
    }
}
