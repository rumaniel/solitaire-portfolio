using Model.Achievement;

namespace Service.AchievementService.Evaluator
{
    /// <summary>Handles rules that read from <see cref="Model.Stats.DailyStats"/>.</summary>
    public class DailyStatsRuleEvaluator : IAchievementRuleEvaluator
    {
        public bool CanHandle(AchievementRuleType type) => type switch
        {
            AchievementRuleType.DailyCompletedAtLeast => true,
            AchievementRuleType.DailyStreakAtLeast => true,
            _ => false,
        };

        public EvaluationResult Evaluate(IAchievementDefinition def, AchievementStatus current, EvaluationContext ctx)
        {
            var daily = ctx.Daily;
            if (daily == null) return EvaluationResult.NoChange(current.CurrentProgress);

            return def.RuleType switch
            {
                AchievementRuleType.DailyCompletedAtLeast => ThresholdResult(daily.TotalCompleted, def.TargetInt),
                AchievementRuleType.DailyStreakAtLeast => ThresholdResult(daily.BestStreak, def.TargetInt),
                _ => EvaluationResult.NoChange(current.CurrentProgress),
            };
        }

        private static EvaluationResult ThresholdResult(int current, int target)
        {
            int clamped = current > target ? target : current;
            return new EvaluationResult(clamped, current >= target);
        }
    }
}
