using Model.Achievement;
using Model.Game;
using Model.Stats;
using NUnit.Framework;
using Service.AchievementService.Evaluator;

namespace Tests.EditMode
{
    [TestFixture]
    public class SessionAwareRuleEvaluatorTests
    {
        private SessionAwareRuleEvaluator _evaluator;

        [SetUp]
        public void SetUp() => _evaluator = new SessionAwareRuleEvaluator();

        private static StubAchievementDefinition PerfectRunDef()
            => new() { Id = "perfect", RuleType = AchievementRuleType.PerfectRun };

        private static EvaluationContext CtxWith(SessionStats session)
            => new(GameType.Klondike, null, session, null);

        [Test]
        public void PerfectRun_WonWithoutUndoOrHint_Unlocks()
        {
            var session = new SessionStats { IsWon = true, UndoUsed = false, HintUsed = false };
            var result = _evaluator.Evaluate(PerfectRunDef(), new AchievementStatus { Id = "perfect" }, CtxWith(session));

            Assert.IsTrue(result.ShouldUnlock);
        }

        [Test]
        public void PerfectRun_WonWithUndo_NoUnlock()
        {
            var session = new SessionStats { IsWon = true, UndoUsed = true, HintUsed = false };
            var result = _evaluator.Evaluate(PerfectRunDef(), new AchievementStatus { Id = "perfect" }, CtxWith(session));

            Assert.IsFalse(result.ShouldUnlock);
        }

        [Test]
        public void PerfectRun_WonWithHint_NoUnlock()
        {
            var session = new SessionStats { IsWon = true, UndoUsed = false, HintUsed = true };
            var result = _evaluator.Evaluate(PerfectRunDef(), new AchievementStatus { Id = "perfect" }, CtxWith(session));

            Assert.IsFalse(result.ShouldUnlock);
        }

        [Test]
        public void PerfectRun_Lost_NoUnlock()
        {
            var session = new SessionStats { IsWon = false, UndoUsed = false, HintUsed = false };
            var result = _evaluator.Evaluate(PerfectRunDef(), new AchievementStatus { Id = "perfect" }, CtxWith(session));

            Assert.IsFalse(result.ShouldUnlock);
        }

        [Test]
        public void PerfectRun_NullSession_ReturnsNoChange()
        {
            var status = new AchievementStatus { Id = "perfect", CurrentProgress = 0 };
            var result = _evaluator.Evaluate(PerfectRunDef(), status,
                new EvaluationContext(GameType.Klondike, null, null, null));

            Assert.IsFalse(result.ShouldUnlock);
            Assert.AreEqual(0, result.NewProgress);
        }

        [Test]
        public void CanHandle_OnlyPerfectRun()
        {
            Assert.IsTrue(_evaluator.CanHandle(AchievementRuleType.PerfectRun));
            Assert.IsFalse(_evaluator.CanHandle(AchievementRuleType.FirstWin));
            Assert.IsFalse(_evaluator.CanHandle(AchievementRuleType.NoUndoWin));
        }
    }
}
