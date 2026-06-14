namespace Service.AchievementService.Evaluator
{
    /// <summary>Output of one evaluation: updated incremental progress and whether the unlock threshold was reached.</summary>
    public readonly struct EvaluationResult
    {
        public readonly int NewProgress;
        public readonly bool ShouldUnlock;

        public EvaluationResult(int newProgress, bool shouldUnlock)
        {
            NewProgress = newProgress;
            ShouldUnlock = shouldUnlock;
        }

        public static EvaluationResult NoChange(int currentProgress) => new(currentProgress, false);
    }
}
