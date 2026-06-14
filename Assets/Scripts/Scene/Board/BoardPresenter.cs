using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Component.Board;
using Cysharp.Threading.Tasks;
using Data.Audio;
using Gateway.Snapshot;
using Model.Board;
using Service.SnapshotService;
using Model.Game;
using Model.Stats;
using R3;
using Scene.Ingame;
using Scene.Shared;
using Service.AudioService;
using Service.BoardGameService;
using Service.GameService;
using Service.LocalizationService;
using Service.RouteService;
using Service.SkinService;
using Service.StatsService;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.Board
{
    /// <summary>Coordinates the layered-board games (Pyramid, TriPeaks): deal → play → win/stuck → undo, with a live HUD score. Resolves the active service via IBoardGameServiceFactory and the matching board view via BoardViewSet.</summary>
    public sealed class BoardPresenter : IStartable, ITickable, IDisposable
    {
        [Inject] private IngameShellView Shell { get; set; }
        [Inject] private BoardViewSet BoardViews { get; set; }
        private UIBoardController BoardController { get; set; } // active board view, resolved per-init
        private IBoardGameService BoardGameService { get; set; } // resolved per-init from the factory
        [Inject] private IBoardGameServiceFactory BoardGameServiceFactory { get; set; }
        [Inject] private ISessionStatsService SessionStats { get; set; }
        [Inject] private IRouteService RouteService { get; set; }
        [Inject] private IAudioService AudioService { get; set; }
        [Inject] private ISkinService SkinService { get; set; }
        [Inject] private ILifetimeStatsService LifetimeStats { get; set; }
        [Inject] private IBoardSnapshotService SnapshotService { get; set; }
        [Inject] private ILocalizationService LocalizationService { get; set; }
        [Inject] private ISolvableSeedPrefetchService SeedPrefetch { get; set; }

        private ShellFlowController flow;
        private readonly CompositeDisposable disposable = new();
        private CompositeDisposable shellSubscriptions = new();
        private CompositeDisposable gameSubscriptions = new();
        private readonly ZeroScoreRule zeroScoreRule = new();

        private GameType currentGameType;
        private IBoardScorer scorer;
        private BoardState prevState;
        private SnapshotKey currentSnapshotKey;
        private IReadOnlyList<BoardHint> currentHints;
        private int hintIndex;
        private bool undoInProgress;
        private CancellationTokenSource lifeCts;
        private bool isActive;

        public void Start()
        {
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
                    CanUndo = () => BoardGameService.CanUndo,
                    PerformUndo = () => PerformUndo(),
                    StartNewGame = () => StartNewGameAsync().Forget(),
                    StartRestart = () => StartRestartAsync().Forget(),
                    HandleHint = () => HandleHint(),
                    FlushSnapshot = () => SnapshotService.FlushAsync(),
                });

            WireBoardInput();

            SkinService.CurrentSpriteSet
                .Where(set => set != null)
                .Subscribe(set => { foreach (var view in BoardViews.All) view.ApplySpriteSet(set); })
                .AddTo(disposable);

            RouteService.OnSamePathNavigated
                .Subscribe(_ => EvaluateOwnership())
                .AddTo(disposable);

            EvaluateOwnership();
        }

        private void EvaluateOwnership()
        {
            var query = new IngameQuery(RouteService.CurrentQuery);
            bool mine = query.GameType.IsBoardMode();
            if (!mine) { Release(); return; }
            // Abandoning an in-progress game of our own (New Game / Restart re-entry) counts as a loss —
            // mirrors the card stack; a won/lost game already has IsFinished set, so no double-count.
            // isActive-guarded so a cross-owner switch never abandons the OTHER presenter's session.
            if (isActive && SessionStats.Current.MoveCount > 0 && !SessionStats.Current.IsFinished)
            {
                SessionStats.MarkLost();
                HandleGameEndAsync().Forget();
            }
            Claim();
            InitializeGame();
        }

        private void Claim()
        {
            if (!isActive)
            {
                isActive = true;
                shellSubscriptions = new CompositeDisposable();
                flow.Wire(shellSubscriptions);
            }
            // Root switch: card table off, board view activation happens per-init inside InitializeGameAsync.
            Shell.SetCardTableActive(false);
        }

        private void Release()
        {
            if (!isActive) return;
            isActive = false;
            lifeCts?.Cancel();
            lifeCts?.Dispose();
            lifeCts = null;
            // Capture-then-detach: the snapshot is built synchronously before the claiming presenter
            // can re-initialize the shared SessionStats (lobby-exit semantics — the game stays resumable).
            SnapshotService.FlushAndStopAsync().Forget();
            gameSubscriptions.Dispose();
            gameSubscriptions = new CompositeDisposable();
            shellSubscriptions.Dispose();
            shellSubscriptions = new CompositeDisposable();
            BoardController?.DespawnAll();
            foreach (var view in BoardViews.All) view.gameObject.SetActive(false);
            // No SessionStats reset: the claiming presenter's init always Restores or Initializes
            // the shared session, and its abandon check is isActive-guarded — order-independent.
        }

        public void Tick()
        {
            if (!isActive) return;
            SessionStats.Tick(Time.unscaledDeltaTime);
        }

        // --- Setup ---

        private void InitializeGame() => InitializeGameAsync().Forget();

        private async UniTaskVoid InitializeGameAsync()
        {
            lifeCts?.Cancel();
            lifeCts?.Dispose();
            lifeCts = new CancellationTokenSource();
            // Capture token and per-init fields as locals so a stale continuation never reads
            // fields overwritten by a newer re-entrant call — mirrors IngamePresenter.
            var ct = lifeCts.Token;

            var query = new IngameQuery(RouteService.CurrentQuery);
            currentGameType = query.GameType;
            int variant = query.Variant ?? 1;
            currentSnapshotKey = new SnapshotKey(currentGameType, variant);

            BoardGameService = BoardGameServiceFactory.Create(currentGameType);

            // Activate the board view matching the game type and hide the others (each board prefab has its
            // own UIBoardController; only the active one renders/receives taps).
            BoardController = BoardViews.For(currentGameType);
            if (BoardController == null)
                throw new System.InvalidOperationException(
                    $"No board view is configured for {currentGameType}. Assign its UIBoardController in IngameScene.");
            foreach (var view in BoardViews.All) view.gameObject.SetActive(view == BoardController);

            var gameType = currentGameType;
            var snapshotKey = currentSnapshotKey;
            var gameService = BoardGameService;
            var controller = BoardController;

            BoardLayout layout;
            IBoardMatchRule matchRule;
            int maxRecycles;
            switch (gameType)
            {
                case GameType.TriPeaks:
                    layout = TriPeaksLayoutFactory.Create(variant);
                    matchRule = new TriPeaksMatchRule();
                    scorer = new TriPeaksScorer(new TriPeaksScoreRule(), TriPeaksLayoutFactory.ApexCellIds);
                    maxRecycles = 0; // single pass, no recycle
                    break;
                case GameType.Pyramid:
                default:
                    layout = PyramidLayoutFactory.Create(variant);
                    matchRule = new PyramidMatchRule();
                    scorer = new PyramidScorer(new PyramidScoreRule());
                    maxRecycles = 3; // MS-style: recycle the waste back into the stock up to 3 times
                    break;
            }
            var scorerLocal = scorer;

            gameSubscriptions.Dispose();
            gameSubscriptions = new CompositeDisposable();

            // Reset the HUD + hide panels BEFORE setting game state, so a restore's stats emission is the
            // final word on the HUD (otherwise ResetHud would wipe the restored score/moves back to 0).
            Shell.HideWinPanel();
            Shell.HideStuckPanel();
            Shell.HidePausePanel();
            Shell.HideStatsPanel();
            Shell.ResetHud();
            Shell.SetInputBlocker(false);

            // Resume only an explicit continue with no forced seed; otherwise deal fresh and drop any
            // stale save (covers New Game / Restart, which re-enter here without the continue flag).
            bool canLoad = query.IsContinue && query.Seed == null;
            BoardSnapshot snapshot = canLoad ? await SnapshotService.LoadSnapshotAsync(snapshotKey) : null;
            if (ct.IsCancellationRequested) return;
            bool restored = false;
            if (snapshot != null)
            {
                try
                {
                    gameService.Restore(layout, matchRule, snapshot.Seed,
                        BoardSnapshotConverter.ToBoardState(snapshot.CurrentState),
                        BoardSnapshotConverter.ToHistory(snapshot.UndoHistory), maxRecycles);
                    SessionStats.Restore(zeroScoreRule, BoardSnapshotConverter.ToSessionStats(snapshot.Stats));
                    restored = true;
                }
                catch (Exception e)
                {
                    // A corrupt or layout-incompatible snapshot degrades to a fresh deal rather than
                    // aborting init (InitializeGameAsync is a fire-and-forget UniTaskVoid).
                    Debug.LogException(e);
                }
            }
            if (!restored)
            {
                await SnapshotService.ClearSnapshotAsync(snapshotKey);
                if (ct.IsCancellationRequested) return;

                // Seeded entries (GameCode share / restart) replay the exact seed; only fresh deals
                // go through the solver so the board is (almost always) proven winnable.
                int? dealSeed = query.Seed;
                bool resolveSolvableSeed = (query.Seed == null);
                if (resolveSolvableSeed)
                {
                    int? prefetched = SeedPrefetch.TryConsume(gameType, variant);
                    if (prefetched.HasValue)
                    {
                        dealSeed = prefetched.Value;
                    }
                    else
                    {
                        int inputSeed = DeckFactory.CreateRandomSeed();
                        BoardSolvableSeedResolver.ResolveResult result;
                        try
                        {
                            // TriPeaks: ~5-30 ms; Pyramid: ~150-250 ms
                            result = await flow.ResolveWithLoadingAsync(
                                c => SolverScheduler.ResolveBoardAsync(inputSeed, gameType, layout, c), ct);
                        }
                        catch (OperationCanceledException) { return; }
                        if (ct.IsCancellationRequested) return;
                        dealSeed = result.Seed;
                        Debug.Log($"[SolvableSeed] board {gameType} input={inputSeed} resolved={result.Seed} attempts={result.Attempts} proven={result.Proven}");
                    }
                }

                gameService.Initialize(layout, matchRule, dealSeed, maxRecycles);
                SessionStats.Initialize(zeroScoreRule);

                if (resolveSolvableSeed)
                    SeedPrefetch.PrefetchBoard(gameType, variant, layout);
            }

            controller.DespawnAll();
            var state = gameService.CurrentState;
            controller.RenderBoard(state);
            RefreshHighlights(state);
            controller.SetSelection(SelectionSnapshot.Empty);
            prevState = state;
            scorerLocal.Reset(state);
            currentHints = null;
            hintIndex = 0;

            gameService.OnBoardStateChanged
                .Subscribe(OnBoardStateChanged)
                .AddTo(gameSubscriptions);
            gameService.OnSelectionChanged
                .Subscribe(sel =>
                {
                    controller.SetSelection(sel);
                    controller.SetStockHighlight(false); // a real selection supersedes any draw/recycle hint glow
                    // A non-empty emission means a free card was just picked → select feedback.
                    // Match / clear / undo all clear the selection first and emit Empty, so this never
                    // fires for them — hence no undoInProgress guard is needed here (unlike the scoring
                    // branch, where an undo of a stock draw would otherwise be miscounted).
                    if (sel.Cells.Count > 0 || sel.WasteSelected)
                        AudioService.Play(AudioCatalog.Card.Place);
                })
                .AddTo(gameSubscriptions);

            gameService.OnInvalidTap
                .Subscribe(id =>
                {
                    controller.ShakeCell(id);
                    AudioService.Play(AudioCatalog.Card.MoveRejected);
                })
                .AddTo(gameSubscriptions);

            SnapshotService.StartAutoSave(snapshotKey, gameService.CurrentSeed.Value,
                gameService, SessionStats);
        }

        // --- Input ---

        private void WireBoardInput()
        {
            // Wire BOTH board views' taps to the active service. Only the active board is enabled, so the
            // idle one emits nothing; the lambdas read the per-init BoardGameService at tap time.
            foreach (var view in BoardViews.All)
            {
                view.OnCellTapped.Subscribe(id => BoardGameService.SelectCell(id)).AddTo(disposable);
                view.OnWasteTapped.Subscribe(_ => BoardGameService.SelectWasteTop()).AddTo(disposable);
                view.OnStockTapped.Subscribe(_ =>
                {
                    // Tap draws while the stock has cards; once empty it recycles the waste (if a pass remains).
                    if (BoardGameService.CurrentState.Stock.Count > 0) BoardGameService.DrawFromStock();
                    else BoardGameService.RecycleStock();
                }).AddTo(disposable);
            }
        }

        // --- State / scoring / win / stuck ---

        private void OnBoardStateChanged(BoardState next)
        {
            // undoInProgress → render without the spin-out (undo restores/reverts, it doesn't "remove").
            BoardController.RenderBoard(next, !undoInProgress, BoardGameService.CanRecycle(next));
            RefreshHighlights(next);

            // Any move invalidates a shown hint. The service clears its selection on every state change,
            // so re-asserting Empty here drops a stale match-hint glow (the OnSelectionChanged stream may
            // skip its own Empty emission when the selection was already empty, e.g. a stock draw).
            currentHints = null;
            hintIndex = 0;
            BoardController.SetSelection(SelectionSnapshot.Empty);
            BoardController.SetStockHighlight(false);

            bool won = BoardGameService.IsWon(next);

            if (!SessionStats.Current.IsFinished && !undoInProgress)
            {
                // The scorer interprets the transition in game-specific terms (Pyramid: cards removed;
                // TriPeaks: a play-to-waste streak or a draw). Penalties are floored at 0 by
                // RecordScoreDelta; an undo refunds via the single-level lastScoreDelta and is excluded
                // here by the undoInProgress guard.
                var outcome = scorer.Evaluate(prevState, next, won);
                if (outcome.Points != 0) SessionStats.RecordScoreDelta(outcome.Points);
                PlayMoveSound(outcome.Event);
            }
            else if (undoInProgress)
            {
                // An undo reverted one forward move; revert the scorer's internal accumulators in lock-step
                // (a stateful scorer like TriPeaks would otherwise keep a stale streak / peak ordinal).
                scorer.Undo();
            }
            prevState = next; // advance the diff base even when scoring is skipped (finished / undo)

            if (won && !SessionStats.Current.IsFinished)
            {
                HandleWin();
                return;
            }

            if (!SessionStats.Current.IsFinished && !BoardGameService.HasAnyMove(next))
            {
                SnapshotService.ClearSnapshotAsync(currentSnapshotKey).Forget(); // stuck = game over; don't resume
                AudioService.Play(AudioCatalog.Game.Stuck);
                Shell.ShowStuckPanel(BoardGameService.CanUndo);
            }
        }

        private void HandleWin()
        {
            SnapshotService.ClearSnapshotAsync(currentSnapshotKey).Forget(); // a finished game must not resume
            SessionStats.MarkWon();
            HandleGameEndAsync().Forget();
            Shell.SetInputBlocker(true);
            AudioService.Play(AudioCatalog.Game.Win);
            PlayWinCelebrationAsync().Forget();
        }

        private async UniTaskVoid PlayWinCelebrationAsync()
        {
            var ct = lifeCts.Token;
            try { await Shell.PlayWinEffectAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { Debug.LogException(e); }
            if (ct.IsCancellationRequested) return;

            var c = SessionStats.Current;
            var code = GameCode.Encode(currentGameType, BoardGameService.CurrentSeed ?? 0);
            Shell.ShowWinPanel(c.Score, c.MoveCount, c.ElapsedSeconds, code);
            Shell.TriggerWin();
        }

        /// <summary>Records the finished session (win, or abandon-loss) into LifetimeStats. The snapshot
        /// captures the post-MarkWon/MarkLost state. Mirrors IngamePresenter; achievements are out of scope.</summary>
        private async UniTaskVoid HandleGameEndAsync()
        {
            var snapshot = SessionStats.Current.Snapshot();
            try
            {
                await LifetimeStats.RecordGameResultAsync(currentGameType, snapshot);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void RefreshHighlights(BoardState state)
        {
            var free = new HashSet<CellId>();
            foreach (var id in BoardRules.FreeCells(BoardGameService.Layout, state)) free.Add(id);
            BoardController.SetFreeCells(free);
        }

        private void PlayMoveSound(BoardScoreEvent ev)
        {
            switch (ev)
            {
                case BoardScoreEvent.Cleared: AudioService.Play(AudioCatalog.Card.FoundationPlace); break;
                case BoardScoreEvent.Recycle: AudioService.Play(AudioCatalog.Card.Refresh); break;
                case BoardScoreEvent.Draw: AudioService.Play(AudioCatalog.Card.Flip); break;
            }
        }

        /// <summary>Cycle the available board hints: glow a removable match, or the stock pile for a
        /// draw/recycle suggestion; buzz when stuck. Highlight only — the player still taps to act.</summary>
        private void HandleHint()
        {
            if (currentHints == null || hintIndex >= currentHints.Count)
            {
                currentHints = BoardGameService.GetHints(BoardGameService.CurrentState);
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

            if (hint.Kind == BoardHintKind.Match)
            {
                BoardController.SetSelection(hint.Targets);
                BoardController.SetStockHighlight(false);
            }
            else // Draw or Recycle → the stock pile is the affordance
            {
                BoardController.SetSelection(SelectionSnapshot.Empty);
                BoardController.SetStockHighlight(true);
            }
        }

        /// <summary>Runs an undo and records it as one move, flagging the resulting state-change so the
        /// state-diff scorer in <see cref="OnBoardStateChanged"/> doesn't also count it (an undo of a
        /// stock draw nets zero card-total change and would otherwise be mis-scored as a fresh draw).</summary>
        private void PerformUndo()
        {
            AudioService.Play(AudioCatalog.Game.Undo);
            undoInProgress = true;
            BoardGameService.Undo();
            SessionStats.RecordMove(new ScoredMoveInfo(MoveType.Undo));
            undoInProgress = false;
        }

        private async UniTaskVoid StartNewGameAsync()
        {
            try
            {
                var query = new IngameQuery(RouteService.CurrentQuery);
                var variantStr = (query.Variant ?? 1).ToString(CultureInfo.InvariantCulture);
                var q = new Dictionary<string, string>
                {
                    { GameRouteParams.GameType, query.GameType.ToString() },
                    { GameRouteParams.Variant, variantStr },
                };
                await RouteService.NavigateAsync("Ingame", q);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private async UniTaskVoid StartRestartAsync()
        {
            try
            {
                var seed = BoardGameService.CurrentSeed;
                if (!seed.HasValue) { StartNewGameAsync().Forget(); return; }

                var query = new IngameQuery(RouteService.CurrentQuery);
                var variantStr = (query.Variant ?? 1).ToString(CultureInfo.InvariantCulture);
                var q = new Dictionary<string, string>
                {
                    { GameRouteParams.GameType, query.GameType.ToString() },
                    { GameRouteParams.Variant, variantStr },
                    { GameRouteParams.Seed, seed.Value.ToString(CultureInfo.InvariantCulture) },
                };
                await RouteService.NavigateAsync("Ingame", q);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        public void Dispose()
        {
            SnapshotService.StopAutoSave();
            lifeCts?.Cancel();
            lifeCts?.Dispose();
            gameSubscriptions.Dispose();
            shellSubscriptions.Dispose();
            disposable.Dispose();
        }
    }
}
