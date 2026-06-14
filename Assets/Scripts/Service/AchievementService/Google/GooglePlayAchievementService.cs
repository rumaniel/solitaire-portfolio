#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using Cysharp.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

namespace Service.AchievementService.Google
{
    /// <summary>Google Play Games Services adapter for Android device builds.</summary>
    public sealed class GooglePlayAchievementService : IPlatformAchievementService
    {
        // Shared by mirror + AppPresenter; only resolved after signedIn flips so awaiters
        // don't proceed before the retroactive sweep can be mirrored.
        private UniTaskCompletionSource initTcs;
        // Single-flight: prevents a late stale callback from overwriting a fresh sign-in result.
        private UniTaskCompletionSource<bool> ensureTcs;
        private bool signedIn;
        private static readonly TimeSpan InitWaitTimeout = TimeSpan.FromSeconds(2);
        // Worst-case bound: GPGS silent callback usually fires sub-second, but slow networks
        // legitimately take 5-10s. A tighter bound would early-release retroactive-sweep mirror
        // pushes before signedIn flips, leaving local unlocks unreplayed until the next
        // user-initiated FlushAll.
        private static readonly TimeSpan SignInCallbackTimeout = TimeSpan.FromSeconds(10);

        public bool IsAvailable => signedIn;

        public UniTask InitializeAsync()
        {
            if (initTcs != null) return initTcs.Task;
            initTcs = new UniTaskCompletionSource();
            try
            {
                PlayGamesPlatform.Activate();
                PlayGamesPlatform.Instance.Authenticate(status =>
                {
                    try { ApplySignInResult(status); }
                    catch (Exception e) { Debug.LogException(e); }
                    initTcs.TrySetResult();
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                initTcs.TrySetResult();
                return initTcs.Task;
            }
            // IPlatformAchievementService contract requires "Always completes"; bound the wait
            // so a stalled GPGS callback can't freeze AppPresenter.Start at boot.
            return ResolveOnTimeoutAsync(initTcs, SignInCallbackTimeout);
        }

        private static async UniTask ResolveOnTimeoutAsync(UniTaskCompletionSource tcs, TimeSpan bound)
        {
            try { await tcs.Task.Timeout(bound); }
            catch (TimeoutException)
            {
                Debug.LogWarning($"[GPGS] Silent sign-in callback exceeded {bound.TotalSeconds}s; completing init anyway.");
                tcs.TrySetResult();
            }
        }

        public async UniTask<bool> EnsureSignedInAsync()
        {
            if (signedIn) return true;
            if (ensureTcs != null) return await ensureTcs.Task;
            var pending = new UniTaskCompletionSource<bool>();
            ensureTcs = pending;
            try
            {
                try { await InitializeAsync().Timeout(InitWaitTimeout); }
                catch (TimeoutException) { }
                if (signedIn) { pending.TrySetResult(true); return true; }

                var manual = new UniTaskCompletionSource<SignInStatus>();
                try
                {
                    // Apply inside the callback so a late success past our await bound still
                    // flips signedIn (monotonic) — the next user gesture sees true and routes
                    // straight into the platform UI.
                    PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
                    {
                        try { ApplySignInResult(status); }
                        catch (Exception e) { Debug.LogException(e); }
                        manual.TrySetResult(status);
                    });
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    manual.TrySetResult(SignInStatus.InternalError);
                }
                try { await manual.Task.Timeout(SignInCallbackTimeout); }
                catch (TimeoutException)
                {
                    Debug.LogWarning($"[GPGS] Manual sign-in exceeded {SignInCallbackTimeout.TotalSeconds}s; lobby falls back to local panel. A late callback still updates signedIn in the background.");
                }
                pending.TrySetResult(signedIn);
                return signedIn;
            }
            catch (Exception e)
            {
                // Outer guard so concurrent awaiters can't hang on the shared TCS.
                Debug.LogException(e);
                pending.TrySetResult(false);
                return false;
            }
            finally
            {
                ensureTcs = null;
            }
        }

        // signedIn is monotonic on purpose: a stale silent callback arriving after a successful
        // ManuallyAuthenticate must not downgrade us. There's no exposed sign-out path.
        private void ApplySignInResult(SignInStatus status)
        {
            if (status == SignInStatus.Success)
            {
                signedIn = true;
                Debug.Log("[GPGS] Sign-in success");
            }
            else
            {
                Debug.LogWarning($"[GPGS] Sign-in failed: {status}");
            }
        }

        public UniTask UnlockAsync(string platformId)
        {
            if (!signedIn || string.IsNullOrWhiteSpace(platformId)) return UniTask.CompletedTask;
            return ReportProgressAsync(platformId, 100.0);
        }

        public UniTask SetProgressAsync(string platformId, int currentSteps, int totalSteps)
        {
            if (!signedIn || string.IsNullOrWhiteSpace(platformId) || totalSteps <= 0)
                return UniTask.CompletedTask;
            var percent = Math.Clamp((double)currentSteps / totalSteps * 100.0, 0.0, 100.0);
            return ReportProgressAsync(platformId, percent);
        }

        public UniTask<bool> ShowAchievementsUiAsync()
        {
            // PlayGamesPlatform.ShowAchievementsUI swallows its callback when its own
            // IsAuthenticated() is false, which would orphan the TCS if our cached flag drifted.
            if (!signedIn || !PlayGamesPlatform.Instance.IsAuthenticated())
                return UniTask.FromResult(false);
            var tcs = new UniTaskCompletionSource<bool>();
            try
            {
                // UiBusy on rapid re-taps means the prior overlay is still showing — also "shown".
                PlayGamesPlatform.Instance.ShowAchievementsUI(status =>
                    tcs.TrySetResult(status == UIStatus.Valid
                                     || status == UIStatus.UserClosedUI
                                     || status == UIStatus.UiBusy));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                tcs.TrySetResult(false);
            }
            return tcs.Task;
        }

        // Surface failures as exceptions so the mirror skips its dedupe-cache update.
        private UniTask ReportProgressAsync(string platformId, double percent)
        {
            var tcs = new UniTaskCompletionSource();
            try
            {
                PlayGamesPlatform.Instance.ReportProgress(platformId, percent, success =>
                {
                    if (success) tcs.TrySetResult();
                    else tcs.TrySetException(new GooglePlayAchievementException(platformId, percent));
                });
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
            return tcs.Task;
        }
    }

    internal sealed class GooglePlayAchievementException : Exception
    {
        public GooglePlayAchievementException(string platformId, double percent)
            : base($"GPGS rejected ReportProgress('{platformId}', {percent:F1})") { }
    }
}
#endif
