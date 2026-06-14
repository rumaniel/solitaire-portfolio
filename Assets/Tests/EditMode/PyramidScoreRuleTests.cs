using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PyramidScoreRuleTests
    {
        [Test]
        public void ScoreForRemoval_IsPointsPerClearedCard()
        {
            var rule = new PyramidScoreRule(perCard: 5);
            Assert.AreEqual(10, rule.ScoreForRemoval(2)); // a sum-13 pair
            Assert.AreEqual(5, rule.ScoreForRemoval(1));  // a King alone
        }

        [Test]
        public void BoardClearedBonus_IsConfigured()
        {
            var rule = new PyramidScoreRule(perCard: 5, boardClearedBonus: 100);
            Assert.AreEqual(100, rule.BoardClearedBonus);
        }

        [Test]
        public void DefaultEfficiencyPenalties_DrawAndRecycleCostPoints()
        {
            var rule = new PyramidScoreRule();
            Assert.AreEqual(-2, rule.ScoreForStockDraw);  // each draw costs points
            Assert.AreEqual(-10, rule.ScoreForRecycle);   // a recycle (full re-pass) costs more
        }

        [Test]
        public void CustomPenalties_AreHonored()
        {
            var rule = new PyramidScoreRule(perCard: 5, boardClearedBonus: 100,
                stockDrawPenalty: -1, recyclePenalty: -7);
            Assert.AreEqual(-1, rule.ScoreForStockDraw);
            Assert.AreEqual(-7, rule.ScoreForRecycle);
        }
    }
}
