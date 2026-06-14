using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Model.Achievement;
using R3;
using UnityEngine;

namespace Service.AchievementService
{
    /// <summary>
    /// Forwards local achievement unlocks/progress to a platform service (GPGS / Game Center / Steam).
    /// Local stays the Source of Truth; platform failures are absorbed.
    /// AppPresenter MUST call <see cref="AttachSubscriptions"/> before
    /// <see cref="IAchievementService.InitializeAsync"/> so the retroactive sweep is captured.
    /// </summary>
    public sealed class PlatformAchievementMirror : IDisposable
    {
        private readonly IAchievementService achievements;
        private readonly IPlatformAchievementService platform;
        private readonly CompositeDisposable disposables = new();
        private readonly Dictionary<string, int> lastPushedProgress = new();
        // Per-achievement single-flight: a stalled GPGS callback would otherwise let every
        // subsequent tap re-enqueue the same push.
        private readonly HashSet<string> inFlightPushes = new();
        private bool attached;
        private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(5);

        public PlatformAchievementMirror(
            IAchievementService achievements,
            IPlatformAchievementService platform)
        {
            this.achievements = achievements;
            this.platform = platform;
        }

        /// <summary>Subscribe to achievement events and start platform init. Idempotent.</summary>
        public void AttachSubscriptions()
        {
            if (attached) return;
            attached = true;

            platform.InitializeAsync().Forget();

            achievements.OnAchievementUnlocked
                .Subscribe(OnUnlocked)
                .AddTo(disposables);

            achievements.OnProgressChanged
                .Subscribe(_ => OnProgressPulse())
                .AddTo(disposables);
        }

        private void OnUnlocked(AchievementUnlockedEvent e)
        {
            var def = achievements.GetDefinition(e.Id);
            if (def == null) return;
            var platformId = def.GooglePlayId;
            if (string.IsNullOrWhiteSpace(platformId)) return;

            UnlockSafeAsync(platformId, e.Id, def.IsIncremental ? def.TargetInt : 0).Forget();
        }

        private void OnProgressPulse()
        {
            foreach (var (def, status) in achievements.GetAll())
            {
                if (!def.IsIncremental) continue;
                if (status.State == AchievementState.Unlocked) continue;
                var platformId = def.GooglePlayId;
                if (string.IsNullOrWhiteSpace(platformId)) continue;

                var current = status.CurrentProgress;
                // Cache only the *successful* push so transient failures retry on the next pulse.
                if (lastPushedProgress.TryGetValue(def.Id, out var last) && last >= current) continue;

                SetProgressSafeAsync(platformId, def.Id, current, def.TargetInt).Forget();
            }
        }

        /// <summary>
        /// Replay every locally-unlocked or in-progress achievement to the platform. Call after
        /// the platform becomes available later than startup (e.g., user-initiated sign-in via
        /// <see cref="IPlatformAchievementService.EnsureSignedInAsync"/>) so unlocks earned
        /// during the signed-out window are not lost. Idempotent on the GPGS side.
        /// </summary>
        public async UniTask FlushAllToPlatformAsync()
        {
            if (!attached || !platform.IsAvailable) return;
            var pushes = new List<UniTask>();
            foreach (var (def, status) in achievements.GetAll())
            {
                var platformId = def.GooglePlayId;
                if (string.IsNullOrWhiteSpace(platformId)) continue;
                if (status.State == AchievementState.Unlocked)
                {
                    pushes.Add(UnlockSafeAsync(platformId, def.Id, def.IsIncremental ? def.TargetInt : 0));
                }
                else if (def.IsIncremental && status.CurrentProgress > 0)
                {
                    pushes.Add(SetProgressSafeAsync(platformId, def.Id, status.CurrentProgress, def.TargetInt));
                }
            }
            if (pushes.Count == 0) return;
            try
            {
                await UniTask.WhenAll(pushes).Timeout(FlushTimeout);
            }
            catch (TimeoutException)
            {
                Debug.LogWarning($"[PlatformAchievement] Catch-up flush exceeded {FlushTimeout.TotalSeconds}s; opening UI anyway.");
            }
        }

        private async UniTask UnlockSafeAsync(string platformId, string achievementId, int incrementalTarget)
        {
            if (!inFlightPushes.Add(achievementId)) return;
            try
            {
                await platform.InitializeAsync();
                // Skip dedupe update while signed out — otherwise FlushAll would dedupe-block the
                // very pushes that are supposed to backfill the signed-out window.
                if (!platform.IsAvailable) return;
                await platform.UnlockAsync(platformId).Timeout(PushTimeout);
                if (incrementalTarget > 0) lastPushedProgress[achievementId] = incrementalTarget;
            }
            catch (TimeoutException)
            {
                Debug.LogWarning($"[PlatformAchievement] Unlock push exceeded {PushTimeout.TotalSeconds}s for '{platformId}'; will retry on next pulse.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlatformAchievement] Unlock failed for '{platformId}'.");
                Debug.LogException(ex);
            }
            finally
            {
                inFlightPushes.Remove(achievementId);
            }
        }

        private async UniTask SetProgressSafeAsync(string platformId, string achievementId, int current, int total)
        {
            if (!inFlightPushes.Add(achievementId)) return;
            try
            {
                await platform.InitializeAsync();
                if (!platform.IsAvailable) return;
                await platform.SetProgressAsync(platformId, current, total).Timeout(PushTimeout);
                lastPushedProgress[achievementId] = current;
            }
            catch (TimeoutException)
            {
                Debug.LogWarning($"[PlatformAchievement] Progress push exceeded {PushTimeout.TotalSeconds}s for '{platformId}'; will retry on next pulse.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlatformAchievement] Progress push failed for '{platformId}'.");
                Debug.LogException(ex);
            }
            finally
            {
                inFlightPushes.Remove(achievementId);
            }
        }

        public void Dispose() => disposables.Dispose();
    }
}
