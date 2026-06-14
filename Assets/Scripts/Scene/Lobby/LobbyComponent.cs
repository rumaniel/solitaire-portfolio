using System.Collections.Generic;
using Component.Achievement;
using Component.CodeInput;
using Component.Settings;
using Gateway.Snapshot;
using Model.Game;
using R3;
using Scene.Ingame.View;
using Scene.Lobby.View;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Scene.Lobby
{
    public class LobbyComponent : MonoBehaviour
    {
        [SerializeField] private GameTileView[] tiles;
        [SerializeField] private DailyTileView dailyTile;

        [Header("Daily slots — active above game grid, expired below")]
        [SerializeField] private RectTransform dailyActiveSlot;
        [SerializeField] private RectTransform dailyExpiredSlot;

        [Header("Daily Results Panel")]
        [SerializeField] private DailyWinPanelView dailyResultsPanel;

        [Header("Settings")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingPanelView settingPanelView;

        [Header("Achievements")]
        [SerializeField] private Button achievementsButton;
        [SerializeField] private AchievementPanelView achievementPanelView;

        [Header("Play with Code")]
        [SerializeField] private Button playWithCodeButton;
        [SerializeField] private CodeInputView codeInputView;

        [Header("Localized Strings")]
        [SerializeField] private LocalizedString errorCodeInvalid;
        [SerializeField] private LocalizedString dailyShareTemplate;

        public LocalizedString ErrorCodeInvalid => errorCodeInvalid;
        public LocalizedString DailyShareTemplate => dailyShareTemplate;

        private readonly Subject<TileSelection> tileSelectedSubject = new();
        private readonly Subject<Unit> dailyTileSelectedSubject = new();
        private readonly Subject<Unit> settingsRequestedSubject = new();
        private readonly Subject<Unit> achievementsRequestedSubject = new();
        private readonly Subject<Unit> playWithCodeRequestedSubject = new();

        public Observable<TileSelection> OnTileSelectedObservable => tileSelectedSubject;
        public Observable<Unit> OnDailyTileSelectedObservable => dailyTileSelectedSubject;
        public Observable<Unit> OnSettingsRequestedObservable => settingsRequestedSubject;
        public Observable<Unit> OnAchievementsRequestedObservable => achievementsRequestedSubject;
        public Observable<Unit> OnPlayWithCodeRequestedObservable => playWithCodeRequestedSubject;

        public Observable<string> OnCodeSubmittedObservable
            => codeInputView != null ? codeInputView.OnPlayWithCodeObservable : Observable.Empty<string>();

        public void ShowCodeInput(string prefill = "") => codeInputView?.Show(prefill);
        public void HideCodeInput() => codeInputView?.Hide();
        public void ShowCodeInputError(string message) => codeInputView?.ShowError(message);

        public void ShowSettingPanel() => settingPanelView?.Show();
        public void HideSettingPanel() => settingPanelView?.Hide();

        public void ShowAchievementPanel() => achievementPanelView?.Show();
        public void HideAchievementPanel() => achievementPanelView?.Hide();

        public DailyTileView DailyTile => dailyTile;

        public void ShowDailyResultsPanel(int score, int moves, float elapsedSeconds, int streak, string shareText, string dateLabel)
            => dailyResultsPanel?.Show(score, moves, elapsedSeconds, streak, shareText, dateLabel);

        public void HideDailyResultsPanel()
            => dailyResultsPanel?.Hide();

        public string DailyResultsShareText
            => dailyResultsPanel != null ? dailyResultsPanel.ShareText : string.Empty;

        public Observable<Unit> OnDailyResultsCopyObservable()
            => dailyResultsPanel != null ? dailyResultsPanel.OnCopyObservable : Observable.Empty<Unit>();

        public Observable<Unit> OnDailyResultsTwitterObservable()
            => dailyResultsPanel != null ? dailyResultsPanel.OnTwitterObservable : Observable.Empty<Unit>();

        public Observable<Unit> OnDailyResultsCloseObservable()
            => dailyResultsPanel != null ? dailyResultsPanel.OnLobbyObservable : Observable.Empty<Unit>();

        public void PlaceDailyTile(bool expired)
        {
            if (dailyTile == null) return;
            var target = expired ? dailyExpiredSlot : dailyActiveSlot;
            if (target == null) return;
            var tileRt = dailyTile.transform as RectTransform;
            if (tileRt == null) return;
            if (tileRt.parent != target)
                tileRt.SetParent(target, worldPositionStays: false);
        }

        private void Awake()
        {
            if (tiles != null)
            {
                foreach (var tile in tiles)
                {
                    if (tile == null)
                        continue;

                    var captured = tile;
                    captured.OnClickObservable
                        .Subscribe(_ =>
                        {
                            if (!IsPlayable(captured))
                            {
                                UnityEngine.Debug.LogWarning(
                                    $"[Lobby] Ignored tile tap on '{captured.name}': no valid GameVariant assigned.");
                                return;
                            }
                            tileSelectedSubject.OnNext(
                                new TileSelection(captured.GameType, captured.VariantId));
                        })
                        .AddTo(this);
                }
            }

            if (dailyTile != null)
            {
                dailyTile.OnClickObservable
                    .Subscribe(_ => dailyTileSelectedSubject.OnNext(Unit.Default))
                    .AddTo(this);
            }

            settingsButton?.OnClickAsObservable()
                .Subscribe(settingsRequestedSubject.OnNext)
                .AddTo(this);

            achievementsButton?.OnClickAsObservable()
                .Subscribe(achievementsRequestedSubject.OnNext)
                .AddTo(this);

            playWithCodeButton?.OnClickAsObservable()
                .Subscribe(playWithCodeRequestedSubject.OnNext)
                .AddTo(this);
        }

        public IEnumerable<SnapshotKey> GetActiveSnapshotKeys()
        {
            if (tiles == null)
                yield break;

            foreach (var tile in tiles)
            {
                if (!IsPlayable(tile))
                    continue;
                yield return new SnapshotKey(tile.GameType, tile.VariantId);
            }
        }

        public void ApplySnapshots(IReadOnlyDictionary<SnapshotKey, float> elapsedByKey)
        {
            if (tiles == null)
                return;

            foreach (var tile in tiles)
            {
                if (tile == null || tile.IsComingSoon)
                    continue;

                if (!IsPlayable(tile))
                {
                    tile.HideContinueBadge();
                    continue;
                }

                var key = new SnapshotKey(tile.GameType, tile.VariantId);
                if (elapsedByKey != null && elapsedByKey.TryGetValue(key, out var elapsed))
                    tile.ShowContinueBadge(elapsed);
                else
                    tile.HideContinueBadge();
            }
        }

        private static bool IsPlayable(GameTileView tile)
        {
            if (tile == null) return false;
            if (tile.IsComingSoon) return false;
            if (tile.GameType == GameType.None) return false;
            if (tile.VariantId < 1) return false;
            return true;
        }

        private void OnDestroy()
        {
            tileSelectedSubject.Dispose();
            dailyTileSelectedSubject.Dispose();
            settingsRequestedSubject.Dispose();
            achievementsRequestedSubject.Dispose();
            playWithCodeRequestedSubject.Dispose();
        }
    }

    public readonly struct TileSelection
    {
        public readonly GameType GameType;
        public readonly int Variant;

        public TileSelection(GameType gameType, int variant)
        {
            GameType = gameType;
            Variant = variant;
        }
    }
}
