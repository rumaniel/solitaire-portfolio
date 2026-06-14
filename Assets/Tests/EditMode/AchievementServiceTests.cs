using Model.Achievement;
using Model.Game;
using Model.Stats;
using NUnit.Framework;
using R3;
using Service.AchievementService;
using Service.StatsService;

namespace Tests.EditMode
{
    [TestFixture]
    public class AchievementServiceTests
    {
        private MockAchievementGateway _gateway;
        private MockStatsRepository _lifetimeRepo;
        private LifetimeStatsService _lifetime;
        private MockDailyStatsRepository _dailyRepo;
        private MockAppConfig _appConfig;
        private DailyStatsService _daily;
        private StubAchievementCatalog _catalog;

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
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Dispose();
        }

        private AchievementService BuildService()
        {
            var svc = new AchievementService(_catalog, _gateway, _lifetime, _daily);
            svc.InitializeAsync().GetAwaiter().GetResult();
            return svc;
        }

        private static SessionStats Win(bool undoUsed = false, bool hintUsed = false, float time = 300f)
            => new() { IsWon = true, UndoUsed = undoUsed, HintUsed = hintUsed, ElapsedSeconds = time, Score = 100, MoveCount = 50 };

        private static StubAchievementDefinition Def(string id, AchievementRuleType rule,
            int tInt = 0, float tFloat = 0f, bool hidden = false, bool incremental = false,
            GameType scope = GameType.Klondike)
            => new()
            {
                Id = id, RuleType = rule, TargetInt = tInt, TargetFloat = tFloat,
                IsHidden = hidden, IsIncremental = incremental, ScopeGameType = scope,
            };

        [Test]
        public void FirstWin_UnlocksAfterFirstGameEnd()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin));
            var service = BuildService();

            int unlockCount = 0;
            service.OnAchievementUnlocked.Subscribe(_ => unlockCount++);

            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            Assert.AreEqual(AchievementState.Unlocked, service.GetStatus("first").State);
            Assert.GreaterOrEqual(unlockCount, 1);
        }

        [Test]
        public void SameEvaluation_Twice_EmitsUnlockOnce()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin));
            var service = BuildService();

            int unlockCount = 0;
            service.OnAchievementUnlocked.Subscribe(_ => unlockCount++);

            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());

            Assert.AreEqual(1, unlockCount, "Unlock should fire exactly once for the same achievement.");
        }

        [Test]
        public void TotalWinsAtLeast_Incremental_ReportsProgressAndUnlocks()
        {
            _catalog.Add(Def("wins10", AchievementRuleType.TotalWinsAtLeast, tInt: 10, incremental: true));
            var service = BuildService();

            for (int i = 0; i < 5; i++)
                _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());
            Assert.AreEqual(AchievementState.Locked, service.GetStatus("wins10").State);
            Assert.AreEqual(5, service.GetStatus("wins10").CurrentProgress);

            for (int i = 0; i < 5; i++)
                _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());
            Assert.AreEqual(AchievementState.Unlocked, service.GetStatus("wins10").State);
            Assert.AreEqual(10, service.GetStatus("wins10").CurrentProgress);
        }

        [Test]
        public void PerfectRun_UnlocksOnPerfectGameEnd()
        {
            _catalog.Add(Def("perfect", AchievementRuleType.PerfectRun, hidden: true));
            var service = BuildService();

            var session = Win(undoUsed: false, hintUsed: false);
            _lifetime.RecordGameResultAsync(GameType.Klondike, session).GetAwaiter().GetResult();
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), session);

            Assert.AreEqual(AchievementState.Unlocked, service.GetStatus("perfect").State);
        }

        [Test]
        public void PerfectRun_DoesNotUnlock_WhenUndoUsed()
        {
            _catalog.Add(Def("perfect", AchievementRuleType.PerfectRun, hidden: true));
            var service = BuildService();

            var session = Win(undoUsed: true, hintUsed: false);
            _lifetime.RecordGameResultAsync(GameType.Klondike, session).GetAwaiter().GetResult();
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), session);

            Assert.AreEqual(AchievementState.Locked, service.GetStatus("perfect").State);
        }

        [Test]
        public void NoUndoWin_And_PerfectRun_AreIndependent()
        {
            _catalog.Add(Def("noUndo", AchievementRuleType.NoUndoWin));
            _catalog.Add(Def("perfect", AchievementRuleType.PerfectRun, hidden: true));
            var service = BuildService();

            // A win using undo — neither rule should fire.
            var session = Win(undoUsed: true, hintUsed: false);
            _lifetime.RecordGameResultAsync(GameType.Klondike, session).GetAwaiter().GetResult();
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), session);

            Assert.AreEqual(AchievementState.Locked, service.GetStatus("noUndo").State);
            Assert.AreEqual(AchievementState.Locked, service.GetStatus("perfect").State);
        }

        [Test]
        public void Retroactive_UnlocksBasedOnExistingStats_WithRetroactiveFlag()
        {
            // Pre-populate stats so that on Initialize the service sees existing wins.
            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();

            _catalog.Add(Def("first", AchievementRuleType.FirstWin));

            int retroactiveUnlocks = 0;
            int immediateUnlocks = 0;
            var service = new AchievementService(_catalog, _gateway, _lifetime, _daily);
            service.OnAchievementUnlocked.Subscribe(e =>
            {
                if (e.Retroactive) retroactiveUnlocks++;
                else immediateUnlocks++;
            });
            service.InitializeAsync().GetAwaiter().GetResult();

            Assert.AreEqual(AchievementState.Unlocked, service.GetStatus("first").State);
            Assert.AreEqual(1, retroactiveUnlocks);
            Assert.AreEqual(0, immediateUnlocks);
        }

        [Test]
        public void Persistence_SurvivesServiceRestart()
        {
            _catalog.Add(Def("first", AchievementRuleType.FirstWin));

            var s1 = BuildService();
            _lifetime.RecordGameResultAsync(GameType.Klondike, Win()).GetAwaiter().GetResult();
            s1.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win());
            Assert.AreEqual(AchievementState.Unlocked, s1.GetStatus("first").State);

            // New service instance reads from the same gateway.
            var s2 = new AchievementService(_catalog, _gateway, _lifetime, _daily);
            s2.InitializeAsync().GetAwaiter().GetResult();
            Assert.AreEqual(AchievementState.Unlocked, s2.GetStatus("first").State);
        }

        [Test]
        public void Daily_StreakAtLeast_UnlocksWhenBestStreakReachesTarget()
        {
            _catalog.Add(Def("daily7", AchievementRuleType.DailyStreakAtLeast, tInt: 7, incremental: true, scope: GameType.None));
            var service = BuildService();

            // Simulate 7 consecutive daily wins.
            var start = new System.DateTime(2026, 4, 14, 0, 0, 0, System.DateTimeKind.Utc);
            for (int i = 0; i < 7; i++)
            {
                _daily.RecordResultAsync(start.AddDays(i), won: true, Win()).GetAwaiter().GetResult();
            }

            Assert.AreEqual(AchievementState.Unlocked, service.GetStatus("daily7").State);
        }

        [Test]
        public void Hidden_Unlock_StillEmitsEvent()
        {
            _catalog.Add(Def("secret", AchievementRuleType.PerfectRun, hidden: true));
            var service = BuildService();

            string unlockedId = null;
            service.OnAchievementUnlocked.Subscribe(e => unlockedId = e.Id);

            var session = Win(undoUsed: false, hintUsed: false);
            _lifetime.RecordGameResultAsync(GameType.Klondike, session).GetAwaiter().GetResult();
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), session);

            Assert.AreEqual("secret", unlockedId);
        }

        [Test]
        public void GetAll_ReturnsAllCatalogEntries_WithStatus()
        {
            _catalog.Add(Def("a", AchievementRuleType.FirstWin));
            _catalog.Add(Def("b", AchievementRuleType.NoHintWin));
            var service = BuildService();

            var all = service.GetAll();
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual("a", all[0].Definition.Id);
            Assert.AreEqual("b", all[1].Definition.Id);
            Assert.AreEqual(AchievementState.Locked, all[0].Status.State);
        }

        [Test]
        public void ShortestWinUnder_UnlocksWhenGameFastEnough()
        {
            _catalog.Add(Def("speed", AchievementRuleType.ShortestWinUnderSeconds, tFloat: 180f));
            var service = BuildService();

            _lifetime.RecordGameResultAsync(GameType.Klondike, Win(time: 150f)).GetAwaiter().GetResult();
            service.EvaluateOnGameEnd(GameType.Klondike, _lifetime.GetStats(GameType.Klondike), Win(time: 150f));

            Assert.AreEqual(AchievementState.Unlocked, service.GetStatus("speed").State);
        }
    }
}
