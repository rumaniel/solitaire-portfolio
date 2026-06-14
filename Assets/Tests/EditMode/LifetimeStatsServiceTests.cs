using NUnit.Framework;
using Model.Game;
using Model.Stats;
using Service.StatsService;

namespace Tests.EditMode
{
    [TestFixture]
    public class LifetimeStatsServiceTests
    {
        private LifetimeStatsService _service;
        private MockStatsRepository _repo;

        [SetUp]
        public void SetUp()
        {
            _repo = new MockStatsRepository();
            _service = new LifetimeStatsService(_repo);
            _service.InitializeAsync().GetAwaiter().GetResult();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        private SessionStats MakeWin(int score = 100, int moves = 50, float time = 300f,
            bool undoUsed = false, bool hintUsed = false)
        {
            return new SessionStats
            {
                Score = score,
                MoveCount = moves,
                ElapsedSeconds = time,
                UndoUsed = undoUsed,
                HintUsed = hintUsed,
                IsWon = true
            };
        }

        private SessionStats MakeLoss(int score = 30, int moves = 20, float time = 120f)
        {
            return new SessionStats
            {
                Score = score,
                MoveCount = moves,
                ElapsedSeconds = time,
                UndoUsed = false,
                IsWon = false
            };
        }

        // --- Basic Counting ---

        [Test]
        public void RecordWin_IncrementsTotalGamesAndWins()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(1, stats.TotalGamesPlayed);
            Assert.AreEqual(1, stats.TotalGamesWon);
            Assert.AreEqual(0, stats.TotalGamesLost);
        }

        [Test]
        public void RecordLoss_IncrementsTotalGamesAndLosses()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeLoss()).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(1, stats.TotalGamesPlayed);
            Assert.AreEqual(0, stats.TotalGamesWon);
            Assert.AreEqual(1, stats.TotalGamesLost);
        }

        // --- Time Stats ---

        [Test]
        public void RecordWin_TracksShortestAndLongestTime()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(time: 200f)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(time: 100f)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(time: 300f)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(100f, stats.ShortestWinTime, 0.001f);
            Assert.AreEqual(300f, stats.LongestWinTime, 0.001f);
        }

        [Test]
        public void RecordWin_ComputesAverageTime()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(time: 100f)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(time: 200f)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(150f, stats.AverageWinTime, 0.001f);
        }

        // --- Move Stats ---

        [Test]
        public void RecordWin_TracksMinAndMaxMoves()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(moves: 80)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(moves: 50)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(moves: 120)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(50, stats.MinWinMoves);
            Assert.AreEqual(120, stats.MaxWinMoves);
        }

        [Test]
        public void RecordWin_ComputesAverageMoves()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(moves: 60)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(moves: 100)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(80f, stats.AverageWinMoves, 0.001f);
        }

        // --- Undo Tracking ---

        [Test]
        public void RecordWin_WithoutUndo_IncrementsNoUndoWins()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(undoUsed: false)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(1, stats.GamesWonWithoutUndo);
        }

        [Test]
        public void RecordWin_WithUndo_DoesNotIncrementNoUndoWins()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(undoUsed: true)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(0, stats.GamesWonWithoutUndo);
        }

        // --- Hint Tracking ---

        [Test]
        public void RecordWin_WithoutHints_IncrementsNoHintWins()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(hintUsed: false)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(1, stats.GamesWonWithoutHints);
        }

        [Test]
        public void RecordWin_WithHints_DoesNotIncrementNoHintWins()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(hintUsed: true)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(0, stats.GamesWonWithoutHints);
        }

        [Test]
        public void RecordLoss_DoesNotAffectNoHintWins()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeLoss()).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(0, stats.GamesWonWithoutHints);
        }

        // --- Score Stats ---

        [Test]
        public void RecordWin_TracksHighScore()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(score: 200)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(score: 500)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(score: 300)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(500, stats.HighScore);
        }

        [Test]
        public void RecordGames_ComputesAverageScore()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(score: 100)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeLoss(score: 50)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(75f, stats.AverageScore, 0.001f); // (100+50)/2
        }

        // --- Win Streaks ---

        [Test]
        public void WinStreak_IncreasesOnConsecutiveWins()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(3, stats.CurrentWinStreak);
            Assert.AreEqual(3, stats.BestWinStreak);
        }

        [Test]
        public void WinStreak_ResetsOnLoss()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeLoss()).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(0, stats.CurrentWinStreak);
            Assert.AreEqual(2, stats.BestWinStreak);
        }

        [Test]
        public void WinStreak_BestPreservedAcrossMultipleStreaks()
        {
            // Streak of 3
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeLoss()).GetAwaiter().GetResult();

            // Streak of 2
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(2, stats.CurrentWinStreak);
            Assert.AreEqual(3, stats.BestWinStreak); // best preserved
        }

        // --- WinRate ---

        [Test]
        public void WinRate_ComputedCorrectly()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeLoss()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin()).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Klondike, MakeLoss()).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(0.5f, stats.WinRate, 0.001f);
        }

        // --- Per-GameType Isolation ---

        [Test]
        public void Stats_SeparatePerGameType()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(score: 100)).GetAwaiter().GetResult();
            _service.RecordGameResultAsync(GameType.Easthaven, MakeWin(score: 200)).GetAwaiter().GetResult();

            var klondike = _service.GetStats(GameType.Klondike);
            var easthaven = _service.GetStats(GameType.Easthaven);

            Assert.AreEqual(1, klondike.TotalGamesPlayed);
            Assert.AreEqual(100, klondike.TotalScore);
            Assert.AreEqual(1, easthaven.TotalGamesPlayed);
            Assert.AreEqual(200, easthaven.TotalScore);
        }

        // --- GetStats for unknown type ---

        [Test]
        public void GetStats_ReturnsEmptyForUnknownType()
        {
            var stats = _service.GetStats(GameType.None);
            Assert.AreEqual(0, stats.TotalGamesPlayed);
        }

        // --- Persistence (round-trip via mock) ---

        [Test]
        public void Stats_PersistedViaRepository()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeWin(score: 150)).GetAwaiter().GetResult();

            // Create a new service with the same repo to simulate restart
            var service2 = new LifetimeStatsService(_repo);
            service2.InitializeAsync().GetAwaiter().GetResult();

            var stats = service2.GetStats(GameType.Klondike);
            Assert.AreEqual(1, stats.TotalGamesPlayed);
            Assert.AreEqual(1, stats.TotalGamesWon);
            Assert.AreEqual(150, stats.TotalScore);

            service2.Dispose();
        }

        // --- Loss does not update win-only fields ---

        [Test]
        public void RecordLoss_DoesNotUpdateWinTimeOrMoves()
        {
            _service.RecordGameResultAsync(GameType.Klondike, MakeLoss(score: 30)).GetAwaiter().GetResult();

            var stats = _service.GetStats(GameType.Klondike);
            Assert.AreEqual(float.MaxValue, stats.ShortestWinTime);
            Assert.AreEqual(0f, stats.LongestWinTime);
            Assert.AreEqual(int.MaxValue, stats.MinWinMoves);
            Assert.AreEqual(0, stats.MaxWinMoves);
            Assert.AreEqual(0, stats.HighScore); // high score only tracks wins
        }
    }
}
