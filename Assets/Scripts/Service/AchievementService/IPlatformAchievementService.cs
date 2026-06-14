using Cysharp.Threading.Tasks;

namespace Service.AchievementService
{
    /// <summary>
    /// Platform-side achievement bridge (Google Play Games / Apple Game Center / Steam).
    /// Local is the Source of Truth; this is a one-way mirror — push only, no pull/sync.
    /// All methods MUST be silent-fail safe so platform failures never break local achievement flow.
    /// </summary>
    public interface IPlatformAchievementService
    {
        /// <summary>Initialize platform SDK and attempt silent sign-in. Always completes; never throws.</summary>
        UniTask InitializeAsync();

        /// <summary>
        /// Surface the platform's interactive sign-in UI when the user has no cached session.
        /// MUST be triggered by an explicit user gesture — never from app startup. Returns true if
        /// the user is signed in (already or after consenting), false if they declined / cancelled
        /// / the platform is unavailable.
        /// </summary>
        UniTask<bool> EnsureSignedInAsync();

        /// <summary>True after successful platform sign-in; false on Editor / WebGL / signed-out devices.</summary>
        bool IsAvailable { get; }

        /// <summary>Mirror an unlock to the platform. Empty/null platformId is a legal no-op.</summary>
        UniTask UnlockAsync(string platformId);

        /// <summary>Mirror incremental progress. No-op when platformId is empty or totalSteps &lt;= 0.</summary>
        UniTask SetProgressAsync(string platformId, int currentSteps, int totalSteps);

        /// <summary>
        /// Open the platform's native achievements UI.
        /// Returns true if the native UI was actually shown, false if unavailable or the call failed
        /// — callers should fall back to a local view when false.
        /// </summary>
        UniTask<bool> ShowAchievementsUiAsync();
    }
}
