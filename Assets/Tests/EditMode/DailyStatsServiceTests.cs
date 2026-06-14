using System;
using Model.App;
using Model.Stats;
using NUnit.Framework;
using Service.StatsService;

namespace Tests.EditMode
{
    [TestFixture]
    public class DailyStatsServiceTests
    {
        private MockDailyStatsRepository _repository;
        private MockAppConfig _appConfig;
        private DailyStatsService _service;

        private static readonly DateTime Day1 = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Day2 = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Day3 = new DateTime(2026, 4, 17, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime DayGap = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            _repository = new MockDailyStatsRepository();
            _appConfig = new MockAppConfig { DailyStatsHistoryLimit = 30 };
            _service = new DailyStatsService(_repository, _appConfig);
        }

        [Test]
        public void IsCompletedToday_FreshService_ReturnsFalse()
        {
            Assert.IsFalse(_service.IsCompletedToday(Day1));
        }

        [Test]
        public void RecordResultAsync_Win_IncrementsStreakAndFlagsCompletion()
        {
            _service.RecordResultAsync(Day1, won: true, Session(100, 80, 120f)).GetAwaiter().GetResult();

            Assert.AreEqual(1, _service.Stats.CurrentStreak);
            Assert.AreEqual(1, _service.Stats.BestStreak);
            Assert.AreEqual(1, _service.Stats.TotalCompleted);
            Assert.AreEqual(1, _service.Stats.TotalAttempted);
            Assert.IsTrue(_service.IsCompletedToday(Day1));
            Assert.IsTrue(_service.IsAttemptedToday(Day1));
        }

        [Test]
        public void RecordResultAsync_SameDayTwice_IsIdempotent()
        {
            _service.RecordResultAsync(Day1, won: true, Session(100)).GetAwaiter().GetResult();
            _service.RecordResultAsync(Day1, won: true, Session(200)).GetAwaiter().GetResult();

            Assert.AreEqual(1, _service.Stats.TotalCompleted);
            Assert.AreEqual(1, _service.Stats.TotalAttempted);
            Assert.AreEqual(1, _service.Stats.History.Count);
            Assert.AreEqual(100, _service.Stats.History[0].Score);
        }

        [Test]
        public void RecordResultAsync_ConsecutiveWins_StreakGrows()
        {
            _service.RecordResultAsync(Day1, true, Session(100)).GetAwaiter().GetResult();
            _service.RecordResultAsync(Day2, true, Session(100)).GetAwaiter().GetResult();
            _service.RecordResultAsync(Day3, true, Session(100)).GetAwaiter().GetResult();

            Assert.AreEqual(3, _service.Stats.CurrentStreak);
            Assert.AreEqual(3, _service.Stats.BestStreak);
        }

        [Test]
        public void RecordResultAsync_Loss_ResetsStreak()
        {
            _service.RecordResultAsync(Day1, true, Session(100)).GetAwaiter().GetResult();
            _service.RecordResultAsync(Day2, false, Session(0)).GetAwaiter().GetResult();

            Assert.AreEqual(0, _service.Stats.CurrentStreak);
            Assert.AreEqual(1, _service.Stats.BestStreak);
            Assert.AreEqual(1, _service.Stats.TotalCompleted);
            Assert.AreEqual(2, _service.Stats.TotalAttempted);
        }

        [Test]
        public void RecordResultAsync_NonConsecutiveWin_ResetsStreak()
        {
            _service.RecordResultAsync(Day1, true, Session(100)).GetAwaiter().GetResult();
            // Skip Day2 entirely (no record). Coming back on DayGap should reset streak to 1.
            _service.RecordResultAsync(DayGap, true, Session(200)).GetAwaiter().GetResult();

            Assert.AreEqual(1, _service.Stats.CurrentStreak);
            Assert.AreEqual(1, _service.Stats.BestStreak);
        }

        [Test]
        public void RecordResultAsync_Win_PersistsToRepository()
        {
            _service.RecordResultAsync(Day1, true, Session(100)).GetAwaiter().GetResult();
            Assert.AreEqual(1, _repository.SaveCount);
            Assert.IsNotNull(_repository.Snapshot());
            Assert.AreEqual(1, _repository.Snapshot().TotalCompleted);
        }

        [Test]
        public void LoadAsync_RestoresPriorStats()
        {
            _service.RecordResultAsync(Day1, true, Session(100)).GetAwaiter().GetResult();

            // Create a second service using the same repository
            var reloaded = new DailyStatsService(_repository, _appConfig);
            reloaded.LoadAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, reloaded.Stats.CurrentStreak);
            Assert.IsTrue(reloaded.IsCompletedToday(Day1));
        }

        [Test]
        public void TrimHistory_RespectsLimit()
        {
            _appConfig.DailyStatsHistoryLimit = 2;
            var day = Day1;
            for (int i = 0; i < 5; i++)
            {
                _service.RecordResultAsync(day, true, Session(100 + i)).GetAwaiter().GetResult();
                day = day.AddDays(1);
            }
            Assert.AreEqual(2, _service.Stats.History.Count);
            // Most recent records kept.
            Assert.AreEqual(103, _service.Stats.History[0].Score);
            Assert.AreEqual(104, _service.Stats.History[1].Score);
        }

        private static SessionStats Session(int score, int moves = 0, float elapsed = 0f)
            => new SessionStats { Score = score, MoveCount = moves, ElapsedSeconds = elapsed };
    }
}
