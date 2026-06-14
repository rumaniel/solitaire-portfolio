using Model.Achievement;

namespace Service.AchievementService.Evaluator
{
    /// <summary>Evaluates one achievement definition against a stats context. Service dispatches by <see cref="CanHandle"/>.</summary>
    public interface IAchievementRuleEvaluator
    {
        bool CanHandle(AchievementRuleType type);
        EvaluationResult Evaluate(IAchievementDefinition def, AchievementStatus current, EvaluationContext ctx);
    }
}
