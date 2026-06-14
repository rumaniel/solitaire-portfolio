using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Data.Audio;
using Gateway.Snapshot;
using Model.App;
using Model.Game;
using R3;
using Scene.Lobby.View;
using Service.AchievementService;
using Service.AudioService;
using Service.DailyService;
using Service.GameService;
using Service.LocalizationService;
using Service.RouteService;
using Service.StatsService;
using Shared;
using UnityEngine;
using UnityEngine.Networking;
using VContainer;
using VContainer.Unity;

namespace Scene.Lobby
{
    public class LobbyPresenter : IStartable, IDisposable
    {
        [Inject] private LobbyComponent Component { get; set; }
        [Inject] private IGameSnapshotRepository SnapshotRepository { get; set; }
        [Inject] private IBoardSnapshotRepository BoardSnapshotRepository { get; set; }
        [Inject] private IRouteService RouteService { get; set; }
        [Inject] private IAudioService AudioService { get; set; }
        [Inject] private IDailyStatsService DailyStats { get; set; }
        [Inject] private IAppConfig AppConfig { get; set; }
        [Inject] private ILocalizationService LocalizationService { get; set; }
        [Inject] private IPlatformAchievementService PlatformAchievementService { get; set; }
        [Inject] private PlatformAchievementMirror AchievementMirror { get; set; }


        private readonly CompositeDisposable disposable = new();
        // Presence = a resumable save exists for that key; value = its elapsed seconds (for the badge).
        // Type-agnostic so board and card saves share one path.
        private readonly Dictionary<SnapshotKey, float> snapshotElapsed = new();

        private UniTask loadSnapshotsTask;
        private GameSnapshot dailySnapshot;

        private static readonly SnapshotKey DailySnapshotKey =
            new SnapshotKey(GameType.Klondike, 1, GameRouteParams.ModeDaily);

        public void Start()
        {
            Component.OnTileSelectedObservable
                .Subscribe(OnTileSelected)
                .AddTo(disposable);

            Component.OnDailyTileSelectedObservable
                .Subscribe(_ => OnDailyTileSelected())
                .AddTo(disposable);

            DailyStats.OnStatsChanged
                .Subscribe(_ => RefreshDailyTile())
                .AddTo(disposable);

            Component.OnDailyResultsCopyObservable()
                .Subscribe(_ =>
                {
                    var text = Component.DailyResultsShareText;
                    if (string.IsNullOrEmpty(text)) return;
                    GUIUtility.systemCopyBuffer = text;
                    AudioService.Play(AudioCatalog.UI.Click);
                })
                .AddTo(disposable);

            Component.OnDailyResultsTwitterObservable()
                .Subscribe(_ =>
                {
                    var text = Component.DailyResultsShareText;
                    if (string.IsNullOrEmpty(text)) return;
                    Application.OpenURL($"https://twitter.com/intent/tweet?text={UnityWebRequest.EscapeURL(text)}");
                    AudioService.Play(AudioCatalog.UI.Click);
                })
                .AddTo(disposable);

            Component.OnDailyResultsCloseObservable()
                .Subscribe(_ =>
                {
                    Component.HideDailyResultsPanel();
                    AudioService.Play(AudioCatalog.UI.Click);
                })
                .AddTo(disposable);

            Component.OnSettingsRequestedObservable
                .Subscribe(_ =>
                {
                    AudioService.Play(AudioCatalog.UI.Click);
                    Component.ShowSettingPanel();
                })
                .AddTo(disposable);

            Component.OnAchievementsRequestedObservable
                .Subscribe(_ =>
                {
                    AudioService.Play(AudioCatalog.UI.Click);
                    ShowAchievementsAsync().Forget();
                })
                .AddTo(disposable);

            Component.OnPlayWithCodeRequestedObservable
                .Subscribe(_ =>
                {
                    AudioService.Play(AudioCatalog.UI.Click);
                    Component.ShowCodeInput(ClipboardGameCodeReader.ReadOrEmpty());
                })
                .AddTo(disposable);

            Component.OnCodeSubmittedObservable
                .Subscribe(OnCodeSubmitted)
                .AddTo(disposable);

            loadSnapshotsTask = LoadSnapshotsAsync();
            loadSnapshotsTask.Forget();

            InitializeDailyTileAsync().Forget();
        }

        private async UniTask LoadSnapshotsAsync()
        {
            snapshotElapsed.Clear();

            var keys = new List<SnapshotKey>();
            foreach (var key in Component.GetActiveSnapshotKeys())
                keys.Add(key);

            if (keys.Count == 0)
            {
                Component.ApplySnapshots(snapshotElapsed);
                return;
            }

            var tasks = new UniTask<(SnapshotKey, float?)>[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                tasks[i] = LoadOneAsync(keys[i]);

            var results = await UniTask.WhenAll(tasks);
            foreach (var (key, elapsed) in results)
            {
                if (elapsed.HasValue)
                    snapshotElapsed[key] = elapsed.Value;
            }

            Component.ApplySnapshots(snapshotElapsed);
        }

        private async UniTask<(SnapshotKey key, float? elapsed)> LoadOneAsync(SnapshotKey key)
        {
            try
            {
                if (key.GameType.IsBoardMode())
                {
                    var board = await BoardSnapshotRepository.LoadAsync(key);
                    return (key, board?.Stats?.ElapsedSeconds);
                }

                var snapshot = await SnapshotRepository.LoadAsync(key);
                return (key, snapshot?.Stats?.ElapsedSeconds);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Lobby] Failed to load snapshot for {key}: {ex.Message}");
                return (key, null);
            }
        }

        private async void OnTileSelected(TileSelection selection)
        {
            AudioService.Play(AudioCatalog.UI.Click);

            try
            {
                await loadSnapshotsTask;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Lobby] Snapshot scan failed before tile selection: {ex.Message}");
            }

            var key = new SnapshotKey(selection.GameType, selection.Variant);
            var hasMatchingSnapshot = snapshotElapsed.ContainsKey(key);

            var variantString = selection.Variant.ToString(CultureInfo.InvariantCulture);
            var query = new Dictionary<string, string>
            {
                { GameRouteParams.GameType, selection.GameType.ToString() },
                { GameRouteParams.Variant, variantString },
                { GameRouteParams.DrawCount, variantString },
            };

            if (hasMatchingSnapshot)
                query[GameRouteParams.Continue] = "true";

            RouteService.NavigateAsync("Ingame", query).Forget();
        }

        private async UniTaskVoid InitializeDailyTileAsync()
        {
            try
            {
                await DailyStats.LoadAsync();

                // Resolve via the same frozen v2 chain so the expected seed matches what
                // IngamePresenter will produce — a plain DailySeed.For value would differ.
                var utcNowForResolve = DateTime.UtcNow;
                var expectedSeed = await UniTask.RunOnThreadPool(
                    () => DailySeedResolverV2.ResolveForKlondikeDrawOne(utcNowForResolve));
                var loaded = await SnapshotRepository.LoadAsync(DailySnapshotKey);
                if (loaded != null && loaded.Seed != expectedSeed)
                {
                    try
                    {
                        await SnapshotRepository.DeleteAsync(DailySnapshotKey);
                    }
                    catch (Exception deleteEx)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[Lobby] Failed to delete stale daily snapshot: {deleteEx.Message}");
                    }
                    loaded = null;
                }
                dailySnapshot = loaded;

                RefreshDailyTile();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Lobby] Daily init failed: {ex.Message}");
            }
        }

        private void RefreshDailyTile()
        {
            var tile = Component.DailyTile;
            if (tile == null) return;

            var utcNow = DateTime.UtcNow;
            var state = ComputeDailyState(utcNow);
            var dateLabel = DailySeed.DateKey(utcNow);
            var streak = DailyStats.Stats?.CurrentStreak ?? 0;
            float elapsed = dailySnapshot?.Stats?.ElapsedSeconds ?? -1f;
            var record = state == DailyTileView.DailyState.Completed
                ? DailyStats.GetTodayRecord(utcNow) : null;
            tile.Apply(state, dateLabel, streak, record, elapsed);

            Component.PlaceDailyTile(state == DailyTileView.DailyState.Completed);
        }

        private DailyTileView.DailyState ComputeDailyState(DateTime utcNow)
        {
            if (DailyStats.IsCompletedToday(utcNow))
                return DailyTileView.DailyState.Completed;
            return dailySnapshot != null
                ? DailyTileView.DailyState.InProgress
                : DailyTileView.DailyState.NotStarted;
        }

        private void OnDailyTileSelected()
        {
            AudioService.Play(AudioCatalog.UI.Click);

            var utcNow = DateTime.UtcNow;
            if (DailyStats.IsCompletedToday(utcNow))
            {
                ShowDailyResultsPanel(utcNow);
                return;
            }

            var variantString = DailySnapshotKey.VariantId.ToString(CultureInfo.InvariantCulture);
            var query = new Dictionary<string, string>
            {
                { GameRouteParams.GameType, DailySnapshotKey.GameType.ToString() },
                { GameRouteParams.Variant, variantString },
                { GameRouteParams.DrawCount, variantString },
                { GameRouteParams.Mode, GameRouteParams.ModeDaily },
            };
            RouteService.NavigateAsync("Ingame", query).Forget();
        }

        private void ShowDailyResultsPanel(DateTime utcNow)
        {
            ShowDailyResultsPanelAsync(utcNow).Forget();
        }

        private async UniTaskVoid ShowDailyResultsPanelAsync(DateTime utcNow)
        {
            var record = DailyStats.GetTodayRecord(utcNow);
            if (record == null) return;

            var streak = DailyStats.Stats?.CurrentStreak ?? 0;
            var url = AppConfig?.DailyPlayUrl ?? string.Empty;

            // Positional {0..5}: date, time, score, moves, streak, url.
            var shareText = await LocalizationService.GetStringAsync(
                Component.DailyShareTemplate,
                DailySeed.DateKey(utcNow),
                TimeFormatHelper.Format(record.ElapsedSeconds),
                record.Score, record.MoveCount, streak, url);

            Component.ShowDailyResultsPanel(
                record.Score, record.MoveCount, record.ElapsedSeconds, streak, shareText,
                DailySeed.DateKey(utcNow));
        }

        private void OnCodeSubmitted(string code)
        {
            var result = GameCode.Decode(code?.Trim());
            if (result == null)
            {
                ShowLocalizedCodeInputErrorAsync().Forget();
                return;
            }

            HandleValidCode(result.Value);
        }

        private void HandleValidCode((GameType gameType, int seed) result)
        {
            var (gameType, seed) = result;
            Component.HideCodeInput();
            AudioService.Play(AudioCatalog.UI.Click);

            var query = new Dictionary<string, string>
            {
                { GameRouteParams.GameType, gameType.ToString() },
                { GameRouteParams.Seed, seed.ToString(CultureInfo.InvariantCulture) },
            };
            RouteService.NavigateAsync("Ingame", query).Forget();
        }

        private async UniTaskVoid ShowAchievementsAsync()
        {
            if (await PlatformAchievementService.EnsureSignedInAsync())
            {
                await AchievementMirror.FlushAllToPlatformAsync();
                if (await PlatformAchievementService.ShowAchievementsUiAsync()) return;
            }
            Component.ShowAchievementPanel();
        }

        private async UniTaskVoid ShowLocalizedCodeInputErrorAsync()
        {
            var msg = await LocalizationService.GetStringAsync(Component.ErrorCodeInvalid);
            Component.ShowCodeInputError(msg);
        }

        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}
