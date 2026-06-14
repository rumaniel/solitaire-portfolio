using App.View;
using Audio;
using Data.Achievement;
using Data.App;
using Gateway.Achievement;
using Gateway.Analytics;
using Gateway.Auth;
using Gateway.Snapshot;
using Gateway.Stats;
using Model.Achievement;
using Model.App;
using Service.AchievementService;
using Service.AudioService;
using Service.ConsentService;
using Service.GameService;
using Service.HapticService;
using Service.LayoutService;
using Service.LocalizationService;
using Service.StatsService;
using Service.SkinService;
using Data.Skin;
using Gateway.Skin;
using VContainer;
using VContainer.Unity;
using Service.UserService;
using Service.RouteService;
using UnityEngine;

namespace App
{
    public class AppLifetimeScope : LifetimeScope
    {
        [Header("Audio")]
        [SerializeField] private AudioSystem audioSystem;

        [Header("Navigation")]
        [SerializeField] private NavigationBlocker navigationBlocker;

        [Header("App Config")]
        [SerializeField] private AppConfig appConfig;

        [Header("Achievement")]
        [SerializeField] private AchievementCatalogAsset achievementCatalog;

        [Header("Skin")]
        [SerializeField] private SkinCatalogAsset skinCatalog;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<UserService>(Lifetime.Singleton).As<IUserService>();
            builder.Register<RouteService>(Lifetime.Singleton).As<IRouteService>();
            builder.Register<LocalizationService>(Lifetime.Singleton).As<ILocalizationService>();
#if UNITY_WEBGL
            builder.Register<LocalAuthGateway>(Lifetime.Singleton).As<IAuthGateway>();
#else
            builder.Register<FirebaseAuthGateway>(Lifetime.Singleton).As<IAuthGateway>();
#endif

            // Audio — persistence handled by DontDestroy component on the AudioSystem prefab.
            builder.Register<AudioService>(Lifetime.Singleton).As<IAudioService>();
            if (audioSystem == null)
                Debug.LogError("[AppLifetimeScope] audioSystem reference is missing — wire it in the App scene or prefab.");
            else
                builder.RegisterComponent(audioSystem);

            // Haptic — VibrationEffect on Android, no-op everywhere else.
#if UNITY_ANDROID && !UNITY_EDITOR
            builder.Register<AndroidVibratorBridge>(Lifetime.Singleton).As<IVibratorBridge>();
#else
            builder.Register<NoopVibratorBridge>(Lifetime.Singleton).As<IVibratorBridge>();
#endif
            builder.Register<HapticService>(Lifetime.Singleton).As<IHapticService>();

            // Layout (left/right-handed mode)
            builder.Register<LayoutService>(Lifetime.Singleton).As<ILayoutService>();

            // Persistent raycast blocker — persistence handled by DontDestroy component on the prefab.
            if (navigationBlocker != null)
                builder.RegisterComponent(navigationBlocker);

            // App config (singleton instance for DI-based access). Fail-fast:
            // DailyStatsService requires IAppConfig, so missing config would
            // surface as a later VContainer resolution crash. Throwing here gives
            // a clear, immediate error pointing to the unwired reference.
            if (appConfig == null)
            {
                throw new System.InvalidOperationException(
                    "[AppLifetimeScope] appConfig reference is missing — wire AppConfig.asset in the App scene/prefab.");
            }
            // Register both the concrete SO (Inspector-driven consumers) and the
            // IAppConfig contract (Service-layer consumers; avoids Service→Data dep).
            builder.RegisterInstance(appConfig).AsSelf().As<IAppConfig>();

            // Stats
            builder.Register<LifetimeStatsService>(Lifetime.Singleton).As<ILifetimeStatsService>();
            builder.Register<LocalStatsRepository>(Lifetime.Singleton).As<IStatsRepository>();

            // Daily stats
            builder.Register<LocalDailyStatsRepository>(Lifetime.Singleton).As<IDailyStatsRepository>();
            builder.Register<DailyStatsService>(Lifetime.Singleton).As<IDailyStatsService>();

            // Snapshot
            builder.Register<LocalGameSnapshotRepository>(Lifetime.Singleton).As<IGameSnapshotRepository>();
            builder.Register<LocalBoardSnapshotRepository>(Lifetime.Singleton).As<IBoardSnapshotRepository>();

            // Achievement
            if (achievementCatalog == null)
            {
                throw new System.InvalidOperationException(
                    "[AppLifetimeScope] achievementCatalog reference is missing — wire AchievementCatalog.asset in the App scene/prefab.");
            }
            builder.RegisterInstance(achievementCatalog).AsSelf().As<IAchievementCatalog>();
            builder.Register<LocalAchievementGateway>(Lifetime.Singleton).As<IAchievementGateway>();
            builder.Register<AchievementService>(Lifetime.Singleton).As<IAchievementService>();

#if UNITY_ANDROID && !UNITY_EDITOR
            builder.Register<Service.AchievementService.Google.GooglePlayAchievementService>(Lifetime.Singleton)
                   .As<IPlatformAchievementService>();
#else
            builder.Register<NoopPlatformAchievementService>(Lifetime.Singleton).As<IPlatformAchievementService>();
#endif
            // Mirror is a plain singleton, not an EntryPoint: AppPresenter calls AttachSubscriptions()
            // before AchievementService.InitializeAsync() so the retroactive sweep is observed.
            builder.Register<PlatformAchievementMirror>(Lifetime.Singleton);

            // Skin — App-scoped so Lobby and Ingame share one selection/state.
            if (skinCatalog == null)
            {
                throw new System.InvalidOperationException(
                    "[AppLifetimeScope] skinCatalog reference is missing — wire SkinCatalog.asset in the App scene/prefab.");
            }
            builder.RegisterInstance(skinCatalog).AsSelf().As<ISkinCatalog>();
            builder.Register<AddressableSkinAssetGateway>(Lifetime.Singleton).As<ISkinAssetGateway>();
            builder.Register<PlayerPrefsSkinPreferenceStore>(Lifetime.Singleton).As<ISkinPreferenceStore>();
            builder.Register<SkinService>(Lifetime.Singleton).As<ISkinService>();

            // Consent service — singleton at App scope so the gate (LoginPresenter) and any
            // future revoke flow share the same persistence. ConsentDialogView lives in the
            // Login scene scope instead, removing the App→Component asmdef dependency.
            builder.Register<ConsentService>(Lifetime.Singleton).As<IConsentService>();

            // Prefetch service — resolves a solver-verified seed in the background between
            // deals so the next fresh Klondike game starts without a visible freeze.
            builder.Register<SolvableSeedPrefetchService>(Lifetime.Singleton).As<ISolvableSeedPrefetchService>();

            // Firebase Analytics + Crashlytics auto-collection toggles. Manifest defaults
            // both SDKs to off (see Plugins/Android/FirebaseAnalyticsConsent.androidlib);
            // LoginPresenter flips them on once the user accepts the consent dialog.
#if UNITY_WEBGL
            builder.Register<NoopAnalyticsCollectionGateway>(Lifetime.Singleton).As<IAnalyticsCollectionGateway>();
            builder.Register<NoopCrashlyticsCollectionGateway>(Lifetime.Singleton).As<ICrashlyticsCollectionGateway>();
#else
            builder.Register<FirebaseAnalyticsCollectionGateway>(Lifetime.Singleton).As<IAnalyticsCollectionGateway>();
            builder.Register<FirebaseCrashlyticsCollectionGateway>(Lifetime.Singleton).As<ICrashlyticsCollectionGateway>();
#endif

            builder.RegisterEntryPoint<AppPresenter>().As<AppPresenter>();
        }

        protected override void Awake()
        {
            base.Awake();
            EnqueueParent(this);
        }
    }
}
