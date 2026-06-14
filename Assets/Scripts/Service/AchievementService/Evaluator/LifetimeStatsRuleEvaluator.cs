using Model.Achievement;

namespace Service.AchievementService.Evaluator
{
    /// <summary>Handles rules that read exclusively from <see cref="Model.Stats.LifetimeStats"/>.</summary>
    public class LifetimeStatsRuleEvaluator : IAchievementRuleEvaluator
    {
        public bool CanHandle(AchievementRuleType type) => type switch
        {
            AchievementRuleType.FirstWin => true,
            AchievementRuleType.TotalWinsAtLeast => true,
            AchievementRuleType.WinStreakAtLeast => true,
            AchievementRuleType.ShortestWinUnderSeconds => true,
            AchievementRuleType.NoHintWin => true,
            AchievementRuleType.NoUndoWin => true,
            _ => false,
        };

        public EvaluationResult Evaluate(IAchievementDefinition def, AchievementStatus current, EvaluationContext ctx)
        {
            var lifetime = ctx.Lifetime;
            if (lifetime == null) return EvaluationResult.NoChange(current.CurrentProgress);

            return def.RuleType switch
            {
                AchievementRuleType.FirstWin => BooleanResult(lifetime.TotalGamesWon >= 1),
                AchievementRuleType.TotalWinsAtLeast => ThresholdResult(lifetime.TotalGamesWon, def.TargetInt),
                AchievementRuleType.WinStreakAtLeast => ThresholdResult(lifetime.BestWinStreak, def.TargetInt),
                AchievementRuleType.ShortestWinUnderSeconds => BooleanResult(
                    lifetime.TotalGamesWon >= 1 && lifetime.ShortestWinTime <= def.TargetFloat),
                AchievementRuleType.NoHintWin => BooleanResult(lifetime.GamesWonWithoutHints >= 1),
                AchievementRuleType.NoUndoWin => BooleanResult(lifetime.GamesWonWithoutUndo >= 1),
                _ => EvaluationResult.NoChange(current.CurrentProgress),
            };
        }

        private static EvaluationResult BooleanResult(bool condition)
            => new(condition ? 1 : 0, condition);

        private static EvaluationResult ThresholdResult(int current, int target)
        {
            int clamped = current > target ? target : current;
            return new EvaluationResult(clamped, current >= target);
        }
    }
}
