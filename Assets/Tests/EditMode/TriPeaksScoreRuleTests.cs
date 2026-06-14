using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksScoreRuleTests
    {
        [Test]
        public void PointsForStreak_Is50TimesStreak_ByDefault()
        {
            var rule = new TriPeaksScoreRule();
            Assert.AreEqual(50, rule.PointsForStreak(1));
            Assert.AreEqual(100, rule.PointsForStreak(2));
            Assert.AreEqual(500, rule.PointsForStreak(10));
            Assert.AreEqual(0, rule.PointsForStreak(0));
        }

        [Test]
        public void PeakBonus_Is500_1000_5000_ByOrdinal()
        {
            var rule = new TriPeaksScoreRule();
            Assert.AreEqual(500, rule.PeakBonus(1));
            Assert.AreEqual(1000, rule.PeakBonus(2));
            Assert.AreEqual(5000, rule.PeakBonus(3));
            Assert.AreEqual(0, rule.PeakBonus(4)); // out of range
            Assert.AreEqual(0, rule.PeakBonus(0));
        }

        [Test]
        public void StockDrawPenalty_IsMinusFive_ByDefault()
        {
            Assert.AreEqual(-5, new TriPeaksScoreRule().StockDrawPenalty);
        }

        [Test]
        public void CustomConstants_AreHonored()
        {
            var rule = new TriPeaksScoreRule(pointsPerStreakStep: 10, stockDrawPenalty: -1,
                firstPeakBonus: 1, secondPeakBonus: 2, thirdPeakBonus: 3);
            Assert.AreEqual(30, rule.PointsForStreak(3));
            Assert.AreEqual(-1, rule.StockDrawPenalty);
            Assert.AreEqual(2, rule.PeakBonus(2));
        }
    }
}
