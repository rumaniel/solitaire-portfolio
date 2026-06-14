using System.Threading;
using Component.CodeInput;
using Component.Game;
using Component.Settings;
using Cysharp.Threading.Tasks;
using Model.Stats;
using R3;
using Scene.Ingame.View;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using NaughtyAttributes;

namespace Scene.Ingame
{
    public class IngameShellView : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private IngameHudView hudView;
        [SerializeField] private WinPanelView winPanelView;
        [SerializeField] private DailyWinPanelView dailyWinPanelView;
        [SerializeField] private StatsPanelView statsPanelView;
        [SerializeField] private CodeInputView codeInputView;
        [SerializeField] private StuckPanelView stuckPanelView;
        [SerializeField] private PausePanelView pausePanelView;
        [SerializeField] private SettingPanelView settingPanelView;
        [SerializeField] private ToastView toastView;
        [SerializeField] private WinEffectView winEffectView;

        [Header("Input")]
        [SerializeField] private GameObject inputBlocker;

        [Header("Deal Loading")]
        [SerializeField] private GameObject dealLoadingOverlay;
        [SerializeField] private GameObject cardTableRoot;

        [Header("Localized Strings")]
        [SerializeField] private LocalizedString toastCopied;
        [SerializeField] private LocalizedString toastCodeCopied;
        [SerializeField] private LocalizedString toastAchievementUnlocked;
        [SerializeField] private LocalizedString errorCodeInvalid;
        [SerializeField] private LocalizedString challengeWonTemplate;
        [SerializeField] private LocalizedString challengeLoseTemplate;
        [SerializeField] private LocalizedString dailyShareTemplate;

        public LocalizedString ToastCopied => toastCopied;
        public LocalizedString ToastCodeCopied => toastCodeCopied;
        public LocalizedString ToastAchievementUnlocked => toastAchievementUnlocked;
        public LocalizedString ErrorCodeInvalid => errorCodeInvalid;
        public LocalizedString ChallengeWonTemplate => challengeWonTemplate;
        public LocalizedString ChallengeLoseTemplate => challengeLoseTemplate;
        public LocalizedString DailyShareTemplate => dailyShareTemplate;

        [Header("Events")]
        [SerializeField] public UnityEvent OnWin;

        private readonly Subject<Unit> onRefreshEventsSubject = new Subject<Unit>();
        private readonly Subject<Unit> onUndoSubject = new Subject<Unit>();
        private readonly Subject<Unit> onNewGameSubject = new Subject<Unit>();
        private readonly Subject<Unit> onPauseSubject = new Subject<Unit>();
        private readonly Subject<bool> onApplicationPauseSubject = new Subject<bool>();
        private readonly Subject<Unit> onStatsSubject = new Subject<Unit>();
        private readonly Subject<Unit> onHintSubject = new Subject<Unit>();

        public Observable<Unit> OnRefreshEventsObservable() => onRefreshEventsSubject;
        public Observable<Unit> OnUndoObservable() => onUndoSubject;
        public Observable<Unit> OnNewGameObservable() => onNewGameSubject;
        public Observable<Unit> OnPauseObservable() => onPauseSubject;
        public Observable<bool> OnApplicationPauseObservable() => onApplicationPauseSubject;
        public Observable<Unit> OnStatsObservable() => onStatsSubject;
        public Observable<Unit> OnHintObservable() => onHintSubject;

        [Button("Refresh Events")]
        public void RefreshEvents()
        {
            Debug.Log("Refreshing event Button");
            onRefreshEventsSubject.OnNext(Unit.Default);
        }

        [Button("Undo")]
        public void Undo()
        {
            onUndoSubject.OnNext(Unit.Default);
        }

        [Button("New Game")]
        public void NewGame()
        {
            onNewGameSubject.OnNext(Unit.Default);
        }

        [Button("Pause")]
        public void Pause()
        {
            onPauseSubject.OnNext(Unit.Default);
        }

        [Button("Stats")]
        public void Stats()
        {
            onStatsSubject.OnNext(Unit.Default);
        }

        [Button("Hint")]
        public void Hint()
        {
            onHintSubject.OnNext(Unit.Default);
        }

        public void TriggerWin()
        {
            SetInputBlocker(true);
            OnWin?.Invoke();
        }

        public UniTask PlayWinEffectAsync(CancellationToken ct = default)
            => winEffectView != null ? winEffectView.PlayAsync(ct) : UniTask.CompletedTask;

        public void SetInputBlocker(bool active)
        {
            if (inputBlocker != null) inputBlocker.SetActive(active);
        }

        public void ShowDealLoading()
        {
            if (dealLoadingOverlay != null) dealLoadingOverlay.SetActive(true);
        }

        public void HideDealLoading()
        {
            if (dealLoadingOverlay != null) dealLoadingOverlay.SetActive(false);
        }

        /// <summary>Shows or hides the card-table root. The merged Ingame scene hosts card and board roots side by side; the presenter that claims the current game type switches roots.</summary>
        public void SetCardTableActive(bool active)
        {
            if (cardTableRoot != null) cardTableRoot.SetActive(active);
        }

        // --- HUD ---

        public void UpdateHudScore(int score) => hudView.SetScore(score);
        public void UpdateHudMoves(int moves) => hudView.SetMoves(moves);
        public void UpdateHudTime(float seconds) => hudView.SetTime(seconds);
        public void ResetHud() => hudView.ResetDisplay();

        // --- Win Panel ---

        public void ShowWinPanel(int score, int moves, float elapsedSeconds, string gameCode)
            => winPanelView.Show(score, moves, elapsedSeconds, gameCode);

        public void HideWinPanel()
        {
            SetInputBlocker(false);
            winPanelView.Hide();
        }

        public Observable<Unit> OnShareObservable()
            => winPanelView != null ? winPanelView.OnShareObservable : Observable.Empty<Unit>();

        /// <summary>
        /// Emitted with the current game code when the user taps the code
        /// label in the Win panel. Presenter wires this to
        /// <see cref="CopyToClipboard"/> + <see cref="ShowToast"/>.
        /// </summary>
        public Observable<string> OnCopyCodeObservable()
            => winPanelView != null ? winPanelView.OnCopyCodeObservable : Observable.Empty<string>();

        // --- Pause Panel ---

        public void ShowPausePanel() => pausePanelView?.Show();
        public void HidePausePanel() => pausePanelView?.Hide();

        public Observable<Unit> OnPauseToGameObservable()
            => pausePanelView != null ? pausePanelView.OnToGameObservable : Observable.Empty<Unit>();

        public Observable<Unit> OnPauseNewGameObservable()
            => pausePanelView != null ? pausePanelView.OnNewGameObservable : Observable.Empty<Unit>();

        /// <summary>
        /// Emitted when the user taps "Restart" on the Pause panel. Distinct
        /// from <see cref="OnPauseNewGameObservable"/> — Restart re-deals
        /// with the current seed, New Game rolls a fresh random deal.
        /// </summary>
        public Observable<Unit> OnPauseRestartObservable()
            => pausePanelView != null ? pausePanelView.OnRestartObservable : Observable.Empty<Unit>();

        public Observable<Unit> OnPauseLobbyObservable()
            => pausePanelView != null ? pausePanelView.OnLobbyObservable : Observable.Empty<Unit>();

        public Observable<Unit> OnPausePlayWithCodeObservable()
            => pausePanelView != null ? pausePanelView.OnPlayWithCodeObservable : Observable.Empty<Unit>();

        // --- Setting Panel ---

        /// <summary>
        /// Invoked from the Bottom Panel "Setting" Button via UnityEvent.
        /// Public + <see cref="UnityEngine.UI.Button"/> so Inspector can wire it directly.
        /// </summary>
        [Button("Show Setting Panel")]
        public void ShowSettingPanel() => settingPanelView?.Show();

        public void HideSettingPanel() => settingPanelView?.Hide();

        // --- Win Panel Lobby ---

        public Observable<Unit> OnWinLobbyObservable()
            => winPanelView != null ? winPanelView.OnLobbyObservable : Observable.Empty<Unit>();

        // --- Daily Win Panel ---

        public void ShowDailyWinPanel(int score, int moves, float elapsedSeconds, int streak, string shareText, string dateLabel)
        {
            SetInputBlocker(true);
            dailyWinPanelView?.Show(score, moves, elapsedSeconds, streak, shareText, dateLabel);
        }

        public void HideDailyWinPanel()
        {
            SetInputBlocker(false);
            dailyWinPanelView?.Hide();
        }

        /// <summary>Last rendered Daily share text. Used by Presenter when handling copy/twitter.</summary>
        public string DailyShareText => dailyWinPanelView != null ? dailyWinPanelView.ShareText : string.Empty;

        public Observable<Unit> OnDailyCopyObservable()
            => dailyWinPanelView != null ? dailyWinPanelView.OnCopyObservable : Observable.Empty<Unit>();

        public Observable<Unit> OnDailyTwitterObservable()
            => dailyWinPanelView != null ? dailyWinPanelView.OnTwitterObservable : Observable.Empty<Unit>();

        public Observable<Unit> OnDailyWinLobbyObservable()
            => dailyWinPanelView != null ? dailyWinPanelView.OnLobbyObservable : Observable.Empty<Unit>();

        // --- Toast ---

        /// <summary>
        /// Enqueues a transient toast message. Safe to call concurrently —
        /// <see cref="ToastView"/> uses a FIFO queue. No-op if toastView is unwired.
        /// </summary>
        public void ShowToast(string message, float? durationSeconds = null)
            => toastView?.Show(message, durationSeconds);

        // --- Code Input ---

        public void ShowCodeInput(string prefill = "") => codeInputView?.Show(prefill);
        public void HideCodeInput() => codeInputView?.Hide();
        public void ShowCodeInputError(string message) => codeInputView?.ShowError(message);

        public Observable<string> OnPlayWithCodeObservable()
            => codeInputView != null ? codeInputView.OnPlayWithCodeObservable : Observable.Empty<string>();

        // --- Stuck Panel ---

        public void ShowStuckPanel(bool canUndo) => stuckPanelView?.Show(canUndo);
        public void HideStuckPanel() => stuckPanelView?.Hide();

        public Observable<Unit> OnStuckNewGameObservable()
            => stuckPanelView != null ? stuckPanelView.OnNewGameObservable : Observable.Empty<Unit>();

        public Observable<Unit> OnStuckRestartObservable()
            => stuckPanelView != null ? stuckPanelView.OnRestartObservable : Observable.Empty<Unit>();

        public Observable<Unit> OnStuckUndoObservable()
            => stuckPanelView != null ? stuckPanelView.OnUndoObservable : Observable.Empty<Unit>();

        // --- Clipboard ---

        public void CopyToClipboard(string text) => GUIUtility.systemCopyBuffer = text;

        // --- Stats Panel ---

        public void ShowStatsPanel(LifetimeStats stats) => statsPanelView.Show(stats);
        public void HideStatsPanel() => statsPanelView.Hide();

        private void OnApplicationPause(bool pauseStatus)
        {
            onApplicationPauseSubject.OnNext(pauseStatus);
        }

        private void OnDestroy()
        {
            onRefreshEventsSubject?.Dispose();
            onUndoSubject?.Dispose();
            onNewGameSubject?.Dispose();
            onPauseSubject?.Dispose();
            onApplicationPauseSubject?.Dispose();
            onStatsSubject?.Dispose();
            onHintSubject?.Dispose();
        }
    }
}
