using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Audio;
using Model.Game;
using R3;
using Scene.Ingame;
using Service.AudioService;
using Service.GameService;
using Service.LocalizationService;
using Service.RouteService;
using Service.StatsService;
using UnityEngine.Localization;

namespace Scene.Shared
{
    /// <summary>
    /// Composition helper that wires all shared IngameShell event subscriptions and hosts
    /// the deal-loading overlay helper. Constructed directly by presenters in Start() —
    /// NOT a MonoBehaviour and NOT DI-registered.
    /// </summary>
    public sealed class ShellFlowController
    {
        private readonly IngameShellView _shell;
        private readonly IAudioService _audio;
        private readonly ISessionStatsService _sessionStats;
        private readonly ILifetimeStatsService _lifetimeStats;
        private readonly IRouteService _routeService;
        private readonly ILocalizationService _localization;
        private readonly ShellFlowCallbacks _callbacks;

        public ShellFlowController(
            IngameShellView shell,
            IAudioService audio,
            ISessionStatsService sessionStats,
            ILifetimeStatsService lifetimeStats,
            IRouteService routeService,
            ILocalizationService localization,
            ShellFlowCallbacks callbacks)
        {
            _shell = shell;
            _audio = audio;
            _sessionStats = sessionStats;
            _lifetimeStats = lifetimeStats;
            _routeService = routeService;
            _localization = localization;
            _callbacks = ValidateCallbacks(callbacks);
        }

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> for any null delegate so a wiring
        /// mistake surfaces at construction rather than as an NRE when the shell event
        /// first fires, long after the mistake was made.
        /// </summary>
        private static ShellFlowCallbacks ValidateCallbacks(ShellFlowCallbacks callbacks)
        {
            if (callbacks == null) throw new ArgumentNullException(nameof(callbacks));
            if (callbacks.GetGameType == null) throw new ArgumentNullException($"{nameof(callbacks)}.{nameof(ShellFlowCallbacks.GetGameType)}");
            if (callbacks.CanUndo == null) throw new ArgumentNullException($"{nameof(callbacks)}.{nameof(ShellFlowCallbacks.CanUndo)}");
            if (callbacks.PerformUndo == null) throw new ArgumentNullException($"{nameof(callbacks)}.{nameof(ShellFlowCallbacks.PerformUndo)}");
            if (callbacks.StartNewGame == null) throw new ArgumentNullException($"{nameof(callbacks)}.{nameof(ShellFlowCallbacks.StartNewGame)}");
            if (callbacks.StartRestart == null) throw new ArgumentNullException($"{nameof(callbacks)}.{nameof(ShellFlowCallbacks.StartRestart)}");
            if (callbacks.HandleHint == null) throw new ArgumentNullException($"{nameof(callbacks)}.{nameof(ShellFlowCallbacks.HandleHint)}");
            if (callbacks.FlushSnapshot == null) throw new ArgumentNullException($"{nameof(callbacks)}.{nameof(ShellFlowCallbacks.FlushSnapshot)}");
            return callbacks;
        }

        /// <summary>
        /// Subscribes all shared shell event handlers to <paramref name="disposable"/>.
        /// Call once in the presenter's Start(), passing a CompositeDisposable that is
        /// disposed when the scene is torn down.
        /// </summary>
        public void Wire(CompositeDisposable disposable)
        {
            _shell.OnUndoObservable()
                .Subscribe(_ =>
                {
                    if (!_callbacks.CanUndo()) return;
                    _callbacks.PerformUndo();
                })
                .AddTo(disposable);

            _shell.OnNewGameObservable()
                .Subscribe(_ =>
                {
                    _audio.Play(AudioCatalog.Game.New);
                    _callbacks.StartNewGame();
                })
                .AddTo(disposable);

            // Unified behavior: always flush snapshot on pause (BoardPresenter flushed;
            // IngamePresenter did not — flush is the safer choice).
            _shell.OnPauseObservable()
                .Subscribe(_ =>
                {
                    _audio.Play(AudioCatalog.UI.Open);
                    _sessionStats.Pause();
                    _audio.Pause();
                    _callbacks.FlushSnapshot().Forget();
                    _shell.ShowPausePanel();
                })
                .AddTo(disposable);

            _shell.OnPauseToGameObservable()
                .Subscribe(_ =>
                {
                    _audio.Play(AudioCatalog.UI.Close);
                    _shell.HidePausePanel();
                    _sessionStats.Resume();
                    _audio.UnPause();
                })
                .AddTo(disposable);

            // Unified behavior: call StartNewGame() directly rather than re-emitting OnNewGame
            // (IngamePresenter previously called Shell.NewGame() which re-emits — net effect identical).
            _shell.OnPauseNewGameObservable()
                .Subscribe(_ =>
                {
                    _shell.HidePausePanel();
                    _sessionStats.Resume();
                    _audio.UnPause();
                    _audio.Play(AudioCatalog.Game.New);
                    _callbacks.StartNewGame();
                })
                .AddTo(disposable);

            _shell.OnPauseRestartObservable()
                .Subscribe(_ =>
                {
                    _shell.HidePausePanel();
                    _sessionStats.Resume();
                    _audio.UnPause();
                    _audio.Play(AudioCatalog.Game.New);
                    _callbacks.StartRestart();
                })
                .AddTo(disposable);

            _shell.OnPauseLobbyObservable()
                .Subscribe(_ =>
                {
                    _shell.HidePausePanel();
                    _sessionStats.Resume();
                    _audio.UnPause();
                    _audio.Play(AudioCatalog.UI.Click);
                    _routeService.GoBackAsync().Forget();
                })
                .AddTo(disposable);

            _shell.OnApplicationPauseObservable()
                .Subscribe(paused =>
                {
                    if (paused)
                    {
                        _sessionStats.Pause();
                        _audio.Pause();
                        _callbacks.FlushSnapshot().Forget();
                    }
                    else
                    {
                        _sessionStats.Resume();
                        _audio.UnPause();
                    }
                })
                .AddTo(disposable);

            _shell.OnWinLobbyObservable()
                .Subscribe(_ =>
                {
                    _audio.Play(AudioCatalog.UI.Click);
                    _routeService.GoBackAsync().Forget();
                })
                .AddTo(disposable);

            _shell.OnCopyCodeObservable()
                .Subscribe(code =>
                {
                    if (string.IsNullOrEmpty(code)) return;
                    _shell.CopyToClipboard(code);
                    ShowLocalizedToastAsync(_shell.ToastCodeCopied).Forget();
                    _audio.Play(AudioCatalog.UI.Click);
                })
                .AddTo(disposable);

            _shell.OnStatsObservable()
                .Subscribe(_ => _shell.ShowStatsPanel(_lifetimeStats.GetStats(_callbacks.GetGameType())))
                .AddTo(disposable);

            _sessionStats.OnStatsChanged
                .Subscribe(s =>
                {
                    _shell.UpdateHudScore(s.Score);
                    _shell.UpdateHudMoves(s.MoveCount);
                    _shell.UpdateHudTime(s.ElapsedSeconds);
                })
                .AddTo(disposable);

            _shell.OnHintObservable()
                .Subscribe(_ => _callbacks.HandleHint())
                .AddTo(disposable);

            _shell.OnStuckNewGameObservable()
                .Subscribe(_ =>
                {
                    _shell.HideStuckPanel();
                    _audio.Play(AudioCatalog.Game.New);
                    _callbacks.StartNewGame();
                })
                .AddTo(disposable);

            _shell.OnStuckRestartObservable()
                .Subscribe(_ =>
                {
                    _shell.HideStuckPanel();
                    _audio.Play(AudioCatalog.Game.New);
                    _callbacks.StartRestart();
                })
                .AddTo(disposable);

            _shell.OnStuckUndoObservable()
                .Subscribe(_ =>
                {
                    if (!_callbacks.CanUndo()) return;
                    _shell.HideStuckPanel();
                    _callbacks.PerformUndo();
                })
                .AddTo(disposable);

            _shell.OnPausePlayWithCodeObservable()
                .Subscribe(_ =>
                {
                    // Pause panel stays visible underneath — closing the code input returns here.
                    _audio.Play(AudioCatalog.UI.Click);
                    _shell.ShowCodeInput(ClipboardGameCodeReader.ReadOrEmpty());
                })
                .AddTo(disposable);

            _shell.OnPlayWithCodeObservable()
                .Subscribe(code =>
                {
                    var result = GameCode.Decode(code);
                    if (result == null)
                    {
                        ShowCodeInputErrorAsync().Forget();
                        return;
                    }

                    var (gameType, seed) = result.Value;
                    _shell.HideCodeInput();
                    // Code entry is only reachable from the pause panel — leave the pause context
                    // before navigating, or the audio/timer would stay paused into the new game.
                    _shell.HidePausePanel();
                    _sessionStats.Resume();
                    _audio.UnPause();
                    // All game types live in the Ingame scene; a cross-game code re-enters the
                    // same route and the presenters re-evaluate ownership by GameType.
                    _routeService.NavigateAsync("Ingame", new Dictionary<string, string>
                    {
                        { GameRouteParams.GameType, gameType.ToString() },
                        { GameRouteParams.Seed, seed.ToString(CultureInfo.InvariantCulture) },
                    }).Forget();
                })
                .AddTo(disposable);
        }

        /// <summary>
        /// Awaits <paramref name="resolver"/> (SolverScheduler picks thread pool vs
        /// WebGL PlayerLoop slicing), showing the deal-loading overlay only when the
        /// resolve takes longer than 150 ms. Returns the resolved value.
        /// </summary>
        public async UniTask<T> ResolveWithLoadingAsync<T>(
            Func<CancellationToken, UniTask<T>> resolver, CancellationToken ct)
        {
            var resolveTask = resolver(ct);

            // A detached delay flips flags rather than racing resolveTask: a UniTask supports
            // only one pending continuation, so WhenAny-ing and re-awaiting it would throw.
            bool resolveCompleted = false;
            bool loadingShown = false;
            UniTask.Void(async () =>
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(150), cancellationToken: ct)
                    .SuppressCancellationThrow();
                if (resolveCompleted || ct.IsCancellationRequested) return;
                _shell.ShowDealLoading();
                loadingShown = true;
            });

            try
            {
                return await resolveTask;
            }
            finally
            {
                resolveCompleted = true;
                if (loadingShown) _shell.HideDealLoading();
            }
        }

        /// <summary>Looks up a localized string and shows it as a transient toast.</summary>
        public async UniTaskVoid ShowLocalizedToastAsync(LocalizedString entry)
        {
            var text = await _localization.GetStringAsync(entry);
            if (!string.IsNullOrEmpty(text)) _shell.ShowToast(text);
        }

        /// <summary>Shows the localized "invalid code" message in the code-input dialog.</summary>
        public async UniTaskVoid ShowCodeInputErrorAsync()
        {
            var text = await _localization.GetStringAsync(_shell.ErrorCodeInvalid);
            _shell.ShowCodeInputError(text);
        }
    }

    /// <summary>Per-presenter callbacks passed to <see cref="ShellFlowController"/>.</summary>
    public sealed class ShellFlowCallbacks
    {
        /// <summary>Returns the active game type at event time.</summary>
        public Func<GameType> GetGameType { get; set; }

        /// <summary>Returns whether an undo move is currently available.</summary>
        public Func<bool> CanUndo { get; set; }

        /// <summary>
        /// Performs an undo. Responsible for playing the undo sound and recording the move.
        /// The controller does NOT add any sound or recording around this call.
        /// </summary>
        public Action PerformUndo { get; set; }

        /// <summary>
        /// Starts a new game. The controller plays <see cref="AudioCatalog.Game.New"/> before
        /// invoking this; the callback must NOT play it again.
        /// </summary>
        public Action StartNewGame { get; set; }

        /// <summary>
        /// Restarts the current game with the same seed. The controller plays
        /// <see cref="AudioCatalog.Game.New"/> before invoking this; the callback must NOT play it again.
        /// </summary>
        public Action StartRestart { get; set; }

        /// <summary>Handles a hint request.</summary>
        public Action HandleHint { get; set; }

        /// <summary>Flushes the current game snapshot to persistent storage.</summary>
        public Func<UniTask> FlushSnapshot { get; set; }
    }
}
