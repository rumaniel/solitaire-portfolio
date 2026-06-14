using Model.Achievement;

namespace Service.AchievementService.Evaluator
{
    /// <summary>Handles rules that depend on per-game <see cref="Model.Stats.SessionStats"/>; only evaluates when ctx.Session is supplied.</summary>
    public class SessionAwareRuleEvaluator : IAchievementRuleEvaluator
    {
        public bool CanHandle(AchievementRuleType type) => type == AchievementRuleType.PerfectRun;

        public EvaluationResult Evaluate(IAchievementDefinition def, AchievementStatus current, EvaluationContext ctx)
        {
            if (def.RuleType != AchievementRuleType.PerfectRun)
                return EvaluationResult.NoChange(current.CurrentProgress);

            var session = ctx.Session;
            if (session == null)
                return EvaluationResult.NoChange(current.CurrentProgress);

            bool unlocked = session.IsWon && !session.UndoUsed && !session.HintUsed;
            return new EvaluationResult(unlocked ? 1 : 0, unlocked);
        }
    }
}
