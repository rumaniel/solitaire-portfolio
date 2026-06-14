using Model.Achievement;
using Model.Game;
using Model.Stats;
using NUnit.Framework;
using Service.AchievementService.Evaluator;

namespace Tests.EditMode
{
    [TestFixture]
    public class LifetimeStatsRuleEvaluatorTests
    {
        private LifetimeStatsRuleEvaluator _evaluator;

        [SetUp]
        public void SetUp() => _evaluator = new LifetimeStatsRuleEvaluator();

        private static EvaluationContext Ctx(LifetimeStats lifetime)
            => new(GameType.Klondike, lifetime, null, null);

        [Test]
        public void FirstWin_NoWins_NoUnlock()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.FirstWin };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status, Ctx(new LifetimeStats { TotalGamesWon = 0 }));

            Assert.IsFalse(result.ShouldUnlock);
            Assert.AreEqual(0, result.NewProgress);
        }

        [Test]
        public void FirstWin_OneWin_Unlocks()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.FirstWin };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status, Ctx(new LifetimeStats { TotalGamesWon = 1 }));

            Assert.IsTrue(result.ShouldUnlock);
        }

        [Test]
        public void TotalWinsAtLeast_BelowTarget_Progress()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.TotalWinsAtLeast, TargetInt = 100 };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status, Ctx(new LifetimeStats { TotalGamesWon = 50 }));

            Assert.IsFalse(result.ShouldUnlock);
            Assert.AreEqual(50, result.NewProgress);
        }

        [Test]
        public void TotalWinsAtLeast_AtTarget_Unlocks()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.TotalWinsAtLeast, TargetInt = 100 };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status, Ctx(new LifetimeStats { TotalGamesWon = 100 }));

            Assert.IsTrue(result.ShouldUnlock);
            Assert.AreEqual(100, result.NewProgress);
        }

        [Test]
        public void TotalWinsAtLeast_OverTarget_ClampsProgress()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.TotalWinsAtLeast, TargetInt = 10 };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status, Ctx(new LifetimeStats { TotalGamesWon = 25 }));

            Assert.IsTrue(result.ShouldUnlock);
            Assert.AreEqual(10, result.NewProgress, "Progress should be clamped to target");
        }

        [Test]
        public void WinStreakAtLeast_UsesBestWinStreak()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.WinStreakAtLeast, TargetInt = 5 };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status, Ctx(new LifetimeStats { CurrentWinStreak = 2, BestWinStreak = 5 }));

            Assert.IsTrue(result.ShouldUnlock);
        }

        [Test]
        public void ShortestWinUnder_NoWin_NoUnlock()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.ShortestWinUnderSeconds, TargetFloat = 180f };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status,
                Ctx(new LifetimeStats { TotalGamesWon = 0, ShortestWinTime = float.MaxValue }));

            Assert.IsFalse(result.ShouldUnlock);
        }

        [Test]
        public void ShortestWinUnder_FastEnough_Unlocks()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.ShortestWinUnderSeconds, TargetFloat = 180f };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status,
                Ctx(new LifetimeStats { TotalGamesWon = 1, ShortestWinTime = 150f }));

            Assert.IsTrue(result.ShouldUnlock);
        }

        [Test]
        public void ShortestWinUnder_TooSlow_NoUnlock()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.ShortestWinUnderSeconds, TargetFloat = 180f };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status,
                Ctx(new LifetimeStats { TotalGamesWon = 1, ShortestWinTime = 300f }));

            Assert.IsFalse(result.ShouldUnlock);
        }

        [Test]
        public void NoHintWin_Unlocks()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.NoHintWin };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status, Ctx(new LifetimeStats { GamesWonWithoutHints = 1 }));

            Assert.IsTrue(result.ShouldUnlock);
        }

        [Test]
        public void NoUndoWin_Unlocks()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.NoUndoWin };
            var status = new AchievementStatus { Id = def.Id };
            var result = _evaluator.Evaluate(def, status, Ctx(new LifetimeStats { GamesWonWithoutUndo = 1 }));

            Assert.IsTrue(result.ShouldUnlock);
        }

        [Test]
        public void CanHandle_MatchesOwnRuleTypes_RejectsOthers()
        {
            Assert.IsTrue(_evaluator.CanHandle(AchievementRuleType.FirstWin));
            Assert.IsTrue(_evaluator.CanHandle(AchievementRuleType.TotalWinsAtLeast));
            Assert.IsTrue(_evaluator.CanHandle(AchievementRuleType.NoHintWin));
            Assert.IsFalse(_evaluator.CanHandle(AchievementRuleType.PerfectRun));
            Assert.IsFalse(_evaluator.CanHandle(AchievementRuleType.DailyStreakAtLeast));
        }

        [Test]
        public void NullLifetime_ReturnsNoChange()
        {
            var def = new StubAchievementDefinition { RuleType = AchievementRuleType.FirstWin };
            var status = new AchievementStatus { Id = def.Id, CurrentProgress = 7 };
            var result = _evaluator.Evaluate(def, status, new EvaluationContext(GameType.Klondike, null, null, null));

            Assert.IsFalse(result.ShouldUnlock);
            Assert.AreEqual(7, result.NewProgress);
        }
    }
}
