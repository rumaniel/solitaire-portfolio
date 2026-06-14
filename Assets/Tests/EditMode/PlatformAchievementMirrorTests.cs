using Model.Achievement;
using Model.Game;
using Model.Stats;
using NUnit.Framework;
using Service.AchievementService;
using Service.StatsService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PlatformAchievementMirrorTests
    {
        private MockAchievementGateway _gateway;
        private MockStatsRepository _lifetimeRepo;
        private LifetimeStatsService _lifetime;
        private MockDailyStatsRepository _dailyRepo;
        private MockAppConfig _appConfig;
        private DailyStatsService _daily;
        private StubAchievementCatalog _catalog;
        private MockPlatformAchievementService _platform;

        [SetUp]
        public void SetUp()
        {
            _gateway = new MockAchievementGateway();
            _lifetimeRepo = new MockStatsRepository();
            _lifetime = new LifetimeStatsService(_lifetimeRepo);
            _lifetime.InitializeAsync().GetAwaiter().GetResult();
            _dailyRepo = new MockDailyStatsRepository();
            _appConfig = new MockAppConfig { DailyStatsHistoryLimit = 30 };
            _daily = new DailyStatsService(_dailyRepo, _appConfig);
            _catalog = new StubAchievementCatalog();
            _platform = new MockPlatformAchievementService();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Dispose();
        }

        private static SessionStats Win()
            => new() { IsWon = true, ElapsedSeconds = 300, Score = 100, MoveCount = 50 };

        private static StubAchievementDefinition Def(string id, AchievementRuleType rule,
            string gpgsId = "", int tInt = 0, bool incremental = false,
            GameType scope = GameType.Klondike)
            => new()
            {
                Id = id, RuleType = rule, GooglePlayId = gpgsId,
                TargetInt = tInt, IsIncremental = incremental, ScopeGameType = scope,
            };

        /// <summary>Attach mirror before AchievementService init so the retroactive sweep is captured.</summary>
        private (AchievementService svc, PlatformAchievementMirror mirror) BuildPair()
        {
            var svc = new AchievementService(_catalog, _gateway, _lifetime, _daily);
            var mirror = new PlatformAchievementMirror(svc, _platform);
            mirror.AttachSubscriptions();
            svc.InitializeAsync().GetAwaiter().GetResult();
            return (svc, mirror);
        }

        [Test]
        public void Unlock_PushesPlatformId()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));
            var (svc, _) = BuildPair();

            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            CollectionAssert.AreEqual(new[] { "gp_first" }, _platform.Unlocked);
        }

        [Test]
        public void Unlock_WithEmptyPlatformId_DoesNotPush()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: ""));
            var (svc, _) = BuildPair();

            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            Assert.AreEqual(0, _platform.Unlocked.Count);
        }

        [Test]
        public void Unlock_Retroactive_StillPushes()
        {
            // Pre-populate stats so the sweep at Initialize sees an existing win.
            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));

            var (_, _) = BuildPair();

            CollectionAssert.AreEqual(new[] { "gp_first" }, _platform.Unlocked);
        }

        [Test]
        public void IncrementalProgress_PushesOnDelta()
        {
            _catalog.Add(Def("wins10", AchievementRuleType.TotalWinsAtLeast,
                gpgsId: "gp_wins10", tInt: 10, incremental: true));
            var (svc, _) = BuildPair();

            for (int i = 0; i < 5; i++)
                _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            for (int i = 0; i < 3; i++)
                _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            // Distinct (current, total) pairs are what matters; mirror dedupes redundant pulses.
            Assert.That(_platform.Progress, Has.Some.EqualTo(("gp_wins10", 5, 10)));
            Assert.That(_platform.Progress, Has.Some.EqualTo(("gp_wins10", 8, 10)));
        }

        [Test]
        public void IncrementalProgress_SameValue_DedupedAcrossPulses()
        {
            _catalog.Add(Def("wins10", AchievementRuleType.TotalWinsAtLeast,
                gpgsId: "gp_wins10", tInt: 10, incremental: true));
            var (svc, _) = BuildPair();

            for (int i = 0; i < 5; i++)
                _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();

            int countAfterFirstBatch = _platform.Progress.Count;
            // EvaluateOnGameEnd with the same stats — progress 5→5 is a no-op so nothing is pushed.
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            Assert.AreEqual(countAfterFirstBatch, _platform.Progress.Count,
                "No additional progress push expected when value did not change.");
        }

        [Test]
        public void Unlock_SuppressesLaterProgressPushForSameId()
        {
            _catalog.Add(Def("wins2", AchievementRuleType.TotalWinsAtLeast,
                gpgsId: "gp_wins2", tInt: 2, incremental: true));
            var (svc, _) = BuildPair();

            for (int i = 0; i < 2; i++)
                _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            int progressAtUnlock = _platform.Progress.Count;
            // Post-unlock progress tick should be skipped — the entry is already fully reported.
            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            Assert.AreEqual(progressAtUnlock, _platform.Progress.Count,
                "Progress pushes for an unlocked id should be suppressed.");
            CollectionAssert.AreEqual(new[] { "gp_wins2" }, _platform.Unlocked);
        }

        [Test]
        public void Dispose_StopsForwarding()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));
            var (svc, mirror) = BuildPair();

            mirror.Dispose();

            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            Assert.AreEqual(0, _platform.Unlocked.Count);
        }

        // -- Race-scenario regression tests ----------------------------------------

        [Test]
        public void LateAttach_MissesRetroactiveSweep_ByDesign()
        {
            // Pre-populate stats so InitializeAsync emits a retroactive unlock for "first".
            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));

            var svc = new AchievementService(_catalog, _gateway, _lifetime, _daily);
            // Subscribe AFTER Initialize: the sweep already fired, mirror has no chance to catch it.
            // This documents why AppPresenter calls AttachSubscriptions BEFORE Initialize.
            svc.InitializeAsync().GetAwaiter().GetResult();
            var mirror = new PlatformAchievementMirror(svc, _platform);
            mirror.AttachSubscriptions();

            Assert.AreEqual(0, _platform.Unlocked.Count,
                "Late attach must not retroactively replay the sweep — that's why AppPresenter " +
                "subscribes before AchievementService.InitializeAsync.");
        }

        [Test]
        public void AttachSubscriptions_TwiceIsNoop()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));

            var svc = new AchievementService(_catalog, _gateway, _lifetime, _daily);
            var mirror = new PlatformAchievementMirror(svc, _platform);
            mirror.AttachSubscriptions();
            mirror.AttachSubscriptions();
            svc.InitializeAsync().GetAwaiter().GetResult();

            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            Assert.AreEqual(1, _platform.Unlocked.Count,
                "A double Attach must not duplicate forwarding subscriptions.");
        }

        [Test]
        public void AttachSubscriptions_KicksPlatformInitOnce()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));
            var svc = new AchievementService(_catalog, _gateway, _lifetime, _daily);
            var mirror = new PlatformAchievementMirror(svc, _platform);

            mirror.AttachSubscriptions();
            mirror.AttachSubscriptions();

            Assert.AreEqual(1, _platform.InitializeCalls,
                "Platform InitializeAsync should be invoked exactly once across multiple Attach calls.");
        }

        [Test]
        public void DisposeBeforeAttach_DoesNotThrow_AndStaysSilent()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));
            var svc = new AchievementService(_catalog, _gateway, _lifetime, _daily);
            var mirror = new PlatformAchievementMirror(svc, _platform);

            Assert.DoesNotThrow(() => mirror.Dispose());

            svc.InitializeAsync().GetAwaiter().GetResult();
            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            Assert.AreEqual(0, _platform.Unlocked.Count);
        }

        // -- FlushAllToPlatformAsync (late-sign-in catch-up) -----------------------

        [Test]
        public void FlushAllToPlatformAsync_OnUnattachedMirror_IsNoop()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));
            var svc = new AchievementService(_catalog, _gateway, _lifetime, _daily);
            var mirror = new PlatformAchievementMirror(svc, _platform);

            Assert.DoesNotThrow(() => mirror.FlushAllToPlatformAsync().GetAwaiter().GetResult());
            Assert.AreEqual(0, _platform.Unlocked.Count);
        }

        [Test]
        public void FlushAllToPlatformAsync_WhenPlatformUnavailable_IsNoop()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));
            _platform.IsAvailable = false;
            var (svc, mirror) = BuildPair();
            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            mirror.FlushAllToPlatformAsync().GetAwaiter().GetResult();

            Assert.AreEqual(0, _platform.Unlocked.Count,
                "FlushAll must early-return when the platform is unavailable.");
        }

        [Test]
        public void SignedOutWindow_DoesNotPoisonDedupeCache()
        {
            // The bug: if mirror updated lastPushedProgress while signed out, a later FlushAll
            // would dedupe-block the very pushes meant to backfill the signed-out window.
            _catalog.Add(Def("wins10", AchievementRuleType.TotalWinsAtLeast,
                gpgsId: "gp_wins10", tInt: 10, incremental: true));
            _platform.IsAvailable = false;
            var (svc, mirror) = BuildPair();

            for (int i = 0; i < 5; i++)
                _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());
            Assert.AreEqual(0, _platform.Progress.Count, "No push expected while signed out.");

            _platform.IsAvailable = true;
            mirror.FlushAllToPlatformAsync().GetAwaiter().GetResult();

            Assert.That(_platform.Progress, Has.Some.EqualTo(("gp_wins10", 5, 10)),
                "Backfill must replay incremental progress earned during the signed-out window.");
        }

        [Test]
        public void FlushAllToPlatformAsync_BackfillsUnlocksAfterLateSignIn()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin, gpgsId: "gp_first"));
            _platform.IsAvailable = false;
            var (svc, mirror) = BuildPair();
            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            svc.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());
            Assert.AreEqual(0, _platform.Unlocked.Count, "No push expected while signed out.");

            _platform.IsAvailable = true;
            mirror.FlushAllToPlatformAsync().GetAwaiter().GetResult();

            CollectionAssert.Contains(_platform.Unlocked, "gp_first",
                "Backfill must push the unlock that fired during the signed-out window.");
        }
    }
}
