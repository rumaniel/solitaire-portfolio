using Model.Game;
using Model.Stats;

namespace Service.StatsService
{
    public interface IScoreRuleFactory
    {
        IScoreRule Create(GameType gameType);
    }
}
