using MemoryPack;
using Model.Stats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Covers the LifetimeStats MemoryPack serialization contract used by
    /// LocalStatsRepository. File I/O itself is exercised via manual play-mode
    /// verification (see plan).
    /// </summary>
    [TestFixture]
    public class LifetimeStatsMigrationTests
    {
        [Test]
        public void MemoryPack_RoundTrip_PreservesAllFields()
        {
            var original = MakeStats();

            var bytes = MemoryPackSerializer.Serialize(original);
            var restored = MemoryPackSerializer.Deserialize<LifetimeStats>(bytes);

            Assert.IsNotNull(restored);
            AssertEquivalent(original, restored);
        }

        [Test]
        public void ComputedProperties_NotSerialized()
        {
            var original = MakeStats();
            var bytes = MemoryPackSerializer.Serialize(original);
            var restored = MemoryPackSerializer.Deserialize<LifetimeStats>(bytes);

            // Computed properties should derive from persisted fields alone.
            Assert.AreEqual(original.WinRate, restored.WinRate, 0.0001f);
            Assert.AreEqual(original.AverageWinTime, restored.AverageWinTime, 0.0001f);
            Assert.AreEqual(original.AverageWinMoves, restored.AverageWinMoves, 0.0001f);
            Assert.AreEqual(original.AverageScore, restored.AverageScore, 0.0001f);
        }

        private static LifetimeStats MakeStats() => new LifetimeStats
        {
            TotalGamesPlayed = 10,
            TotalGamesWon = 6,
            TotalGamesLost = 4,
            ShortestWinTime = 42.5f,
            LongestWinTime = 300.25f,
            TotalWinTime = 900f,
            MinWinMoves = 70,
            MaxWinMoves = 180,
            TotalWinMoves = 600,
            GamesWonWithoutUndo = 3,
            GamesWonWithoutHints = 4,
            HighScore = 1500,
            TotalScore = 7500,
            CurrentWinStreak = 2,
            BestWinStreak = 5,
        };

        private static void AssertEquivalent(LifetimeStats expected, LifetimeStats actual)
        {
            Assert.AreEqual(expected.TotalGamesPlayed, actual.TotalGamesPlayed);
            Assert.AreEqual(expected.TotalGamesWon, actual.TotalGamesWon);
            Assert.AreEqual(expected.TotalGamesLost, actual.TotalGamesLost);
            Assert.AreEqual(expected.ShortestWinTime, actual.ShortestWinTime);
            Assert.AreEqual(expected.LongestWinTime, actual.LongestWinTime);
            Assert.AreEqual(expected.TotalWinTime, actual.TotalWinTime);
            Assert.AreEqual(expected.MinWinMoves, actual.MinWinMoves);
            Assert.AreEqual(expected.MaxWinMoves, actual.MaxWinMoves);
            Assert.AreEqual(expected.TotalWinMoves, actual.TotalWinMoves);
            Assert.AreEqual(expected.GamesWonWithoutUndo, actual.GamesWonWithoutUndo);
            Assert.AreEqual(expected.GamesWonWithoutHints, actual.GamesWonWithoutHints);
            Assert.AreEqual(expected.HighScore, actual.HighScore);
            Assert.AreEqual(expected.TotalScore, actual.TotalScore);
            Assert.AreEqual(expected.CurrentWinStreak, actual.CurrentWinStreak);
            Assert.AreEqual(expected.BestWinStreak, actual.BestWinStreak);
        }
    }
}
