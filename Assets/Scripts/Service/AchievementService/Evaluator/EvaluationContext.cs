using Model.Game;
using Model.Stats;

namespace Service.AchievementService.Evaluator
{
    /// <summary>Snapshot of stats sources for one evaluation. <see cref="Session"/> is null except on game-end.</summary>
    public readonly struct EvaluationContext
    {
        public readonly GameType GameType;
        public readonly LifetimeStats Lifetime;
        public readonly SessionStats Session;
        public readonly DailyStats Daily;

        public EvaluationContext(GameType gameType, LifetimeStats lifetime, SessionStats session, DailyStats daily)
        {
            GameType = gameType;
            Lifetime = lifetime;
            Session = session;
            Daily = daily;
        }
    }
}
