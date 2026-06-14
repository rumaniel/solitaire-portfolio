using Model.Achievement;
using Model.Game;
using Model.Stats;
using NUnit.Framework;
using Service.AchievementService.Evaluator;

namespace Tests.EditMode
{
    [TestFixture]
    public class DailyStatsRuleEvaluatorTests
    {
        private DailyStatsRuleEvaluator _evaluator;

        [SetUp]
        public void SetUp() => _evaluator = new DailyStatsRuleEvaluator();

        private static EvaluationContext Ctx(DailyStats daily)
            => new(GameType.None, null, null, daily);

        [Test]
        public void DailyCompletedAtLeast_BelowTarget_NoUnlock()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.DailyCompletedAtLeast, TargetInt = 5 };
            var result = _evaluator.Evaluate(def, new AchievementStatus { Id = def.Id },
                Ctx(new DailyStats { TotalCompleted = 2 }));

            Assert.IsFalse(result.ShouldUnlock);
            Assert.AreEqual(2, result.NewProgress);
        }

        [Test]
        public void DailyCompletedAtLeast_AtTarget_Unlocks()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.DailyCompletedAtLeast, TargetInt = 1 };
            var result = _evaluator.Evaluate(def, new AchievementStatus { Id = def.Id },
                Ctx(new DailyStats { TotalCompleted = 1 }));

            Assert.IsTrue(result.ShouldUnlock);
        }

        [Test]
        public void DailyStreakAtLeast_UsesBestStreak()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.DailyStreakAtLeast, TargetInt = 7 };
            var result = _evaluator.Evaluate(def, new AchievementStatus { Id = def.Id },
                Ctx(new DailyStats { CurrentStreak = 0, BestStreak = 7 }));

            Assert.IsTrue(result.ShouldUnlock);
        }

        [Test]
        public void DailyStreak_BelowTarget_Progress()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.DailyStreakAtLeast, TargetInt = 7 };
            var result = _evaluator.Evaluate(def, new AchievementStatus { Id = def.Id },
                Ctx(new DailyStats { BestStreak = 3 }));

            Assert.IsFalse(result.ShouldUnlock);
            Assert.AreEqual(3, result.NewProgress);
        }

        [Test]
        public void CanHandle_DailyRulesOnly()
        {
            Assert.IsTrue(_evaluator.CanHandle(AchievementRuleType.DailyCompletedAtLeast));
            Assert.IsTrue(_evaluator.CanHandle(AchievementRuleType.DailyStreakAtLeast));
            Assert.IsFalse(_evaluator.CanHandle(AchievementRuleType.FirstWin));
            Assert.IsFalse(_evaluator.CanHandle(AchievementRuleType.PerfectRun));
        }

        [Test]
        public void NullDaily_ReturnsNoChange()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.DailyStreakAtLeast, TargetInt = 7 };
            var result = _evaluator.Evaluate(def, new AchievementStatus { Id = def.Id, CurrentProgress = 2 },
                new EvaluationContext(GameType.None, null, null, null));

            Assert.IsFalse(result.ShouldUnlock);
            Assert.AreEqual(2, result.NewProgress);
        }
    }
}
